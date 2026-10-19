using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.Domain.Models.BoardMove;
using QuestBoard.Domain.Services;
using QuestBoard.UnitTests.Helpers;

namespace QuestBoard.UnitTests.Services;

public class BoardMoveEventServiceTests
{
    private const int SourceBoard = 1;
    private const int BravoBoard = 2;
    private const int CharlieBoard = 3;
    private const int TheEventId = 60;
    private const int ActorId = 9;

    private readonly IEventRepository _eventRepository = Substitute.For<IEventRepository>();
    private readonly IEventSignupRepository _signupRepository = Substitute.For<IEventSignupRepository>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IActiveGroupContext _activeGroupContext = Substitute.For<IActiveGroupContext>();
    private readonly ILinkedBoardSetRepository _linkedBoards = Substitute.For<ILinkedBoardSetRepository>();
    private readonly IEventMoveRepository _moveRepository = Substitute.For<IEventMoveRepository>();
    private readonly FakeBoardClock _clock = new() { Today = new DateOnly(2031, 6, 15) };
    private readonly CapturingLogger _logger = new();
    private readonly EventMoveService _sut;

    private sealed class CapturingLogger : ILogger<EventMoveService>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception)));
    }

    public BoardMoveEventServiceTests()
    {
        _activeGroupContext.ActiveGroupId.Returns(SourceBoard);
        _linkedBoards.GetBoardAsync(SourceBoard, Arg.Any<CancellationToken>())
            .Returns(new LinkedBoardCandidate { Id = SourceBoard, Name = "Board Alpha" });
        SetEvent(MakeEvent());
        SetLinkedBoards(BoardType.OneShot, Board(BravoBoard, "Board Bravo"));
        SetRoster(Row(1, 201, "Player 1"), Row(2, 202, "Player 2"));
        SetMembers(201, 202);

        _sut = new EventMoveService(
            _eventRepository, _signupRepository, _userRepository, _activeGroupContext, _linkedBoards,
            _moveRepository, _clock, _logger);
    }

    private Event MakeEvent(int? seriesId = null, DateOnly? date = null, DateTime? cancelledAt = null) => new()
    {
        Id = TheEventId,
        Title = "Harvest Fair",
        Date = date ?? _clock.Today.AddDays(3),
        SeriesId = seriesId,
        SeriesSlotIndex = seriesId == null ? null : 0,
        CancelledAt = cancelledAt,
        GroupId = SourceBoard
    };

    private static LinkedBoard Board(int id, string name) =>
        new() { Id = id, Name = name, ViewerRole = GroupRole.Admin };

    private static EventSignup Row(int id, int userId, string name) =>
        new() { Id = id, UserId = userId, UserName = name, EventId = TheEventId };

    private void SetEvent(Event? evt) =>
        _eventRepository.GetEventWithDetailsAsync(TheEventId, Arg.Any<CancellationToken>()).Returns(evt);

    private void SetLinkedBoards(BoardType type, params LinkedBoard[] boards) =>
        _linkedBoards.GetLinkedBoardsForViewerAsync(SourceBoard, ActorId, Arg.Any<CancellationToken>())
            .Returns(new ViewerLinkedBoards { SetBoardType = type, Boards = boards });

    private void SetRoster(params EventSignup[] rows) =>
        _signupRepository.GetRosterForEventAsync(TheEventId, Arg.Any<CancellationToken>())
            .Returns(rows.ToList());

    private void SetMembers(params int[] userIds) =>
        _userRepository.GetAllGroupMembers(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(userIds.Select(id => new User { Id = id, Name = $"Member {id}" }).ToList());

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task PreviewAsync_NoActiveBoard_IsNotFound()
    {
        _activeGroupContext.ActiveGroupId.Returns((int?)null);

        (await _sut.PreviewAsync(TheEventId, BravoBoard, ActorId, Token)).Status.Should().Be(MoveStatus.NotFound);
    }

    [Fact]
    public async Task PreviewAsync_EventAbsent_IsNotFound()
    {
        SetEvent(null);

        (await _sut.PreviewAsync(TheEventId, BravoBoard, ActorId, Token)).Status.Should().Be(MoveStatus.NotFound);
    }

    [Fact]
    public async Task PreviewAsync_SeriesOccurrence_IsBlockedAsSeriesOccurrence()
    {
        SetEvent(MakeEvent(seriesId: 7));

        var preview = await _sut.PreviewAsync(TheEventId, BravoBoard, ActorId, Token);

        preview.Status.Should().Be(MoveStatus.Blocked);
        preview.BlockReason.Should().Be(MoveBlockReason.SeriesOccurrence);
    }

    [Fact]
    public async Task PreviewAsync_EventDatedYesterday_IsBlockedAsPast()
    {
        SetEvent(MakeEvent(date: _clock.Today.AddDays(-1)));

        var preview = await _sut.PreviewAsync(TheEventId, BravoBoard, ActorId, Token);

        preview.Status.Should().Be(MoveStatus.Blocked);
        preview.BlockReason.Should().Be(MoveBlockReason.Past);
        preview.SourceBoardName.Should().Be("Board Alpha");
    }

    [Fact]
    public async Task PreviewAsync_CancelledEventDatedToday_IsReady()
    {
        SetEvent(MakeEvent(date: _clock.Today, cancelledAt: new DateTime(2031, 6, 1, 8, 0, 0, DateTimeKind.Utc)));

        (await _sut.PreviewAsync(TheEventId, BravoBoard, ActorId, Token)).Status.Should().Be(MoveStatus.Ready);
    }

    [Fact]
    public async Task PreviewAsync_RosterRows_AreKeptByTargetMembershipAndTheRestDroppedByName()
    {
        SetRoster(Row(1, 201, "Zed"), Row(2, 202, "alice"), Row(3, 203, "Bob"), Row(4, 204, "Cara"));
        SetMembers(201, 203);

        var preview = await _sut.PreviewAsync(TheEventId, BravoBoard, ActorId, Token);

        preview.KeptCount.Should().Be(2);
        preview.Plan!.KeptSignupIds.Should().BeEquivalentTo([1, 3]);
        preview.Plan.DroppedSignupIds.Should().BeEquivalentTo([2, 4]);
        preview.Dropped.Select(p => p.Name).Should().Equal("alice", "Cara");
        preview.Dropped.Should().OnlyContain(p => p.Role == null && !p.IsSelected);
    }

    [Fact]
    public async Task PreviewAsync_CampaignTarget_CountsDistinctMembersWithNoRow()
    {
        SetLinkedBoards(BoardType.Campaign, Board(BravoBoard, "Board Bravo"));
        SetRoster(Row(1, 201, "Player 1"));
        SetMembers(201, 205, 206, 206);

        var preview = await _sut.PreviewAsync(TheEventId, BravoBoard, ActorId, Token);

        preview.CampaignAddCount.Should().Be(2);
        preview.Plan!.CampaignAddUserIds.Should().BeEquivalentTo([205, 206]);
        preview.TargetBoardType.Should().Be(BoardType.Campaign);
    }

    [Fact]
    public async Task PreviewAsync_OneShotTarget_AddsNoRows()
    {
        SetRoster(Row(1, 201, "Player 1"));
        SetMembers(201, 205, 206);

        var preview = await _sut.PreviewAsync(TheEventId, BravoBoard, ActorId, Token);

        preview.CampaignAddCount.Should().Be(0);
        preview.Plan!.CampaignAddUserIds.Should().BeEmpty();
    }

    [Fact]
    public async Task PreviewAsync_EmptyLinkedSet_IsNoLinkedBoards()
    {
        SetLinkedBoards(BoardType.OneShot);

        (await _sut.PreviewAsync(TheEventId, null, ActorId, Token)).Status.Should().Be(MoveStatus.NoLinkedBoards);
    }

    [Fact]
    public async Task PreviewAsync_OneOtherBoardAndNoTarget_PreselectsThatBoard()
    {
        var preview = await _sut.PreviewAsync(TheEventId, null, ActorId, Token);

        preview.Status.Should().Be(MoveStatus.Ready);
        preview.Target!.Id.Should().Be(BravoBoard);
        preview.Stamp.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task PreviewAsync_TwoOtherBoardsAndNoTarget_AsksForATarget()
    {
        SetLinkedBoards(BoardType.OneShot, Board(BravoBoard, "Board Bravo"), Board(CharlieBoard, "Board Charlie"));

        var preview = await _sut.PreviewAsync(TheEventId, null, ActorId, Token);

        preview.Status.Should().Be(MoveStatus.ChooseTarget);
        preview.Target.Should().BeNull();
    }

    [Theory]
    [InlineData(99)]
    [InlineData(SourceBoard)]
    public async Task PreviewAsync_TargetOutsideTheFreshSetOrTheSourceItself_IsTargetNotLinked(int targetBoardId)
    {
        var preview = await _sut.PreviewAsync(TheEventId, targetBoardId, ActorId, Token);

        preview.Status.Should().Be(MoveStatus.TargetNotLinked);
        await _moveRepository.DidNotReceiveWithAnyArgs().ApplyAsync(default!, Token);
    }

    [Fact]
    public async Task PreviewAsync_AnAnswerAppearing_ChangesTheStamp()
    {
        var before = await _sut.PreviewAsync(TheEventId, BravoBoard, ActorId, Token);
        SetRoster(Row(1, 201, "Player 1"), Row(2, 202, "Player 2"), Row(3, 203, "Player 3"));

        var after = await _sut.PreviewAsync(TheEventId, BravoBoard, ActorId, Token);

        after.Stamp.Should().NotBe(before.Stamp);
    }

    [Fact]
    public async Task MoveAsync_StaleStamp_ReturnsChangedAndNeverApplies()
    {
        var preview = await _sut.PreviewAsync(TheEventId, BravoBoard, ActorId, Token);

        var outcome = await _sut.MoveAsync(TheEventId, BravoBoard, "stale", ActorId, Token);

        outcome.Status.Should().Be(MoveStatus.Changed);
        outcome.Stamp.Should().Be(preview.Stamp);
        await _moveRepository.DidNotReceiveWithAnyArgs().ApplyAsync(default!, Token);
    }

    [Fact]
    public async Task MoveAsync_MatchingStamp_AppliesOnceAndLogsCountsOnly()
    {
        SetLinkedBoards(BoardType.Campaign, Board(BravoBoard, "Board Bravo"));
        SetRoster(Row(1, 201, "Secret Name"), Row(2, 202, "Other Name"));
        SetMembers(201, 205);
        var preview = await _sut.PreviewAsync(TheEventId, BravoBoard, ActorId, Token);
        _moveRepository.ApplyAsync(Arg.Any<EventMovePlan>(), Arg.Any<CancellationToken>()).Returns(MoveStatus.Moved);

        var outcome = await _sut.MoveAsync(TheEventId, BravoBoard, preview.Stamp, ActorId, Token);

        outcome.Status.Should().Be(MoveStatus.Moved);
        await _moveRepository.Received(1).ApplyAsync(
            Arg.Is<EventMovePlan>(p => p.EventId == TheEventId && p.TargetGroupId == BravoBoard),
            Arg.Any<CancellationToken>());

        var line = _logger.Entries.Should().ContainSingle(e => e.Level == LogLevel.Information).Subject.Message;
        line.Should().Be("Moved event 60 from board 1 to board 2 by user 9: kept 1, dropped 1, added 1");
        line.Should().NotContain("Name");
    }

    [Fact]
    public async Task MoveAsync_RepositoryReportsChanged_ReturnsChangedWithARecomputedPreview()
    {
        var preview = await _sut.PreviewAsync(TheEventId, BravoBoard, ActorId, Token);
        _moveRepository.ApplyAsync(Arg.Any<EventMovePlan>(), Arg.Any<CancellationToken>()).Returns(MoveStatus.Changed);

        var outcome = await _sut.MoveAsync(TheEventId, BravoBoard, preview.Stamp, ActorId, Token);

        outcome.Status.Should().Be(MoveStatus.Changed);
        outcome.Stamp.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task MoveAsync_ApplyThrows_IsFailed()
    {
        var preview = await _sut.PreviewAsync(TheEventId, BravoBoard, ActorId, Token);
        _moveRepository.ApplyAsync(Arg.Any<EventMovePlan>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("store down"));

        (await _sut.MoveAsync(TheEventId, BravoBoard, preview.Stamp, ActorId, Token)).Status.Should().Be(MoveStatus.Failed);
    }

    [Fact]
    public async Task MoveAsync_SeriesOccurrence_IsBlockedWithoutApplying()
    {
        SetEvent(MakeEvent(seriesId: 7));

        var outcome = await _sut.MoveAsync(TheEventId, BravoBoard, "anything", ActorId, Token);

        outcome.Status.Should().Be(MoveStatus.Blocked);
        await _moveRepository.DidNotReceiveWithAnyArgs().ApplyAsync(default!, Token);
    }

    [Fact]
    public void Constructor_TakesNoMailDispatcher()
    {
        var parameters = typeof(EventMoveService).GetConstructors().Single().GetParameters();

        parameters.Select(p => p.ParameterType).Should().NotContain(typeof(IQuestEmailDispatcher));
    }
}
