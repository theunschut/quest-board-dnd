using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.Service.Jobs;
using QuestBoard.Service.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace QuestBoard.UnitTests.Services;

public class RecurringOccurrenceTopUpJobTests
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IGroupRepository _groupRepository;
    private readonly IEventSeriesService _seriesService;
    private readonly ActiveGroupContextService _groupContext;
    private readonly RecurringOccurrenceTopUpJob _sut;

    public RecurringOccurrenceTopUpJobTests()
    {
        _groupRepository = Substitute.For<IGroupRepository>();
        _seriesService = Substitute.For<IEventSeriesService>();

        // HangfireJobHelper resolves the concrete ActiveGroupContextService (not the interface)
        // to call SetGroupId, so the mocked provider must hand back a real instance of that
        // concrete type rather than a substitute of the interface.
        _groupContext = new ActiveGroupContextService(Substitute.For<IHttpContextAccessor>());

        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(IGroupRepository)).Returns(_groupRepository);
        serviceProvider.GetService(typeof(IEventSeriesService)).Returns(_seriesService);
        serviceProvider.GetService(typeof(ActiveGroupContextService)).Returns(_groupContext);

        var scope = Substitute.For<IServiceScope>();
        scope.ServiceProvider.Returns(serviceProvider);

        _scopeFactory = Substitute.For<IServiceScopeFactory>();
        _scopeFactory.CreateAsyncScope().Returns(new AsyncServiceScope(scope));

        var logger = Substitute.For<ILogger<RecurringOccurrenceTopUpJob>>();
        _sut = new RecurringOccurrenceTopUpJob(_scopeFactory, logger);
    }

    private static GroupWithMemberCount MakeBoard(int id) =>
        new()
        {
            Id = id,
            Name = $"Board {id}",
            CreatedAt = DateTime.UtcNow,
            MemberCount = 1,
            BoardType = BoardType.OneShot
        };

    private static EventSeries MakeSeries(int id, int groupId) =>
        new()
        {
            Id = id,
            GroupId = groupId,
            Title = $"Series {id}",
            AnchorDate = DateOnly.FromDateTime(DateTime.Today),
            IntervalWeeks = 1,
            CycleMask = "1"
        };

    // ---------------------------------------------------------------------------
    // Per-board scoping
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WithThreeBoards_SetsGroupContextOncePerBoardWithRealIds()
    {
        // Arrange
        var boards = new List<GroupWithMemberCount> { MakeBoard(10), MakeBoard(20), MakeBoard(30) };
        _groupRepository.GetAllWithMemberCountAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IList<GroupWithMemberCount>>(boards));

        var capturedGroupIds = new List<int?>();
        _seriesService.GetActiveSeriesForActiveGroupAsync(Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedGroupIds.Add(_groupContext.ActiveGroupId);
                return Task.FromResult<IList<EventSeries>>(new List<EventSeries>());
            });

        // Act
        await _sut.ExecuteAsync(TestContext.Current.CancellationToken);

        // Assert: one call per board, with the real board ids, in board order, never a null.
        capturedGroupIds.Should().Equal(10, 20, 30);
    }

    [Fact]
    public async Task ExecuteAsync_WithThreeBoardsEachTwoActiveSeries_InvokesTopUpAsyncOncePerSeriesPerBoard()
    {
        // Arrange
        var boards = new List<GroupWithMemberCount> { MakeBoard(10), MakeBoard(20), MakeBoard(30) };
        _groupRepository.GetAllWithMemberCountAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IList<GroupWithMemberCount>>(boards));

        var seriesByBoard = new Queue<IList<EventSeries>>(
        [
            new List<EventSeries> { MakeSeries(1, 10), MakeSeries(2, 10) },
            new List<EventSeries> { MakeSeries(3, 20), MakeSeries(4, 20) },
            new List<EventSeries> { MakeSeries(5, 30), MakeSeries(6, 30) },
        ]);
        _seriesService.GetActiveSeriesForActiveGroupAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(seriesByBoard.Dequeue()));
        _seriesService.TopUpAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(1));

        // Act
        await _sut.ExecuteAsync(TestContext.Current.CancellationToken);

        // Assert: exactly six calls, one per series per board, by real series id.
        await _seriesService.Received(1).TopUpAsync(1, Arg.Any<CancellationToken>());
        await _seriesService.Received(1).TopUpAsync(2, Arg.Any<CancellationToken>());
        await _seriesService.Received(1).TopUpAsync(3, Arg.Any<CancellationToken>());
        await _seriesService.Received(1).TopUpAsync(4, Arg.Any<CancellationToken>());
        await _seriesService.Received(1).TopUpAsync(5, Arg.Any<CancellationToken>());
        await _seriesService.Received(1).TopUpAsync(6, Arg.Any<CancellationToken>());
        await _seriesService.Received(6).TopUpAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenOneBoardScopeThrows_ProcessesRemainingBoardsAndStillThrowsAfterward()
    {
        // Arrange
        var boards = new List<GroupWithMemberCount> { MakeBoard(10), MakeBoard(20), MakeBoard(30) };
        _groupRepository.GetAllWithMemberCountAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IList<GroupWithMemberCount>>(boards));

        var capturedGroupIds = new List<int?>();
        _seriesService.GetActiveSeriesForActiveGroupAsync(Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedGroupIds.Add(_groupContext.ActiveGroupId);
                if (_groupContext.ActiveGroupId == 20)
                {
                    throw new InvalidOperationException("Simulated failure for board 20.");
                }

                return Task.FromResult<IList<EventSeries>>(new List<EventSeries>());
            });

        // Act
        var act = async () => await _sut.ExecuteAsync(TestContext.Current.CancellationToken);

        // Assert: the sweep still throws so the retry policy applies...
        await act.Should().ThrowAsync<InvalidOperationException>();

        // ...but every board, including the two after the failing one, was still attempted.
        capturedGroupIds.Should().Equal(10, 20, 30);
    }

    [Fact]
    public async Task ExecuteAsync_WithZeroBoards_CompletesWithoutInvokingSeriesServiceOrThrowing()
    {
        // Arrange
        _groupRepository.GetAllWithMemberCountAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IList<GroupWithMemberCount>>(new List<GroupWithMemberCount>()));

        // Act
        var act = async () => await _sut.ExecuteAsync(TestContext.Current.CancellationToken);

        // Assert
        await act.Should().NotThrowAsync();
        await _seriesService.DidNotReceive().GetActiveSeriesForActiveGroupAsync(Arg.Any<CancellationToken>());
        await _seriesService.DidNotReceive().TopUpAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_EnumeratesBoardsExactlyOnceWithNoGroupSelected()
    {
        // Arrange
        var boards = new List<GroupWithMemberCount> { MakeBoard(10) };
        int? enumerationGroupId = -1; // sentinel distinct from both null and any real board id

        _groupRepository.GetAllWithMemberCountAsync(Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                enumerationGroupId = _groupContext.ActiveGroupId;
                return Task.FromResult<IList<GroupWithMemberCount>>(boards);
            });
        _seriesService.GetActiveSeriesForActiveGroupAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IList<EventSeries>>(new List<EventSeries>()));

        // Act
        await _sut.ExecuteAsync(TestContext.Current.CancellationToken);

        // Assert: the board enumeration itself is the only call made without a real board id --
        // every other repository call happens inside a real per-board scope (proven above).
        enumerationGroupId.Should().BeNull();
        await _groupRepository.Received(1).GetAllWithMemberCountAsync(Arg.Any<CancellationToken>());
    }
}
