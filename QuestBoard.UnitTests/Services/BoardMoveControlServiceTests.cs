using NSubstitute;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.Domain.Models.BoardMove;
using QuestBoard.Domain.Models.QuestBoard;
using QuestBoard.Domain.Services;
using QuestBoard.UnitTests.Helpers;

namespace QuestBoard.UnitTests.Services;

public class BoardMoveControlServiceTests
{
    private const int SourceBoard = 1;
    private const int ActorId = 9;

    private readonly IActiveGroupContext _activeGroupContext = Substitute.For<IActiveGroupContext>();
    private readonly ILinkedBoardSetRepository _linkedBoards = Substitute.For<ILinkedBoardSetRepository>();
    private readonly FakeBoardClock _clock = new() { Today = new DateOnly(2031, 6, 15) };
    private readonly MoveControlService _sut;

    public BoardMoveControlServiceTests()
    {
        _activeGroupContext.ActiveGroupId.Returns(SourceBoard);
        SetLinkedBoards(Board(2, "Board Bravo"));
        _sut = new MoveControlService(_linkedBoards, _activeGroupContext, _clock);
    }

    private static LinkedBoard Board(int id, string name) =>
        new() { Id = id, Name = name, ViewerRole = GroupRole.Admin };

    private void SetLinkedBoards(params LinkedBoard[] boards) =>
        _linkedBoards.GetLinkedBoardsForViewerAsync(SourceBoard, ActorId, Arg.Any<CancellationToken>())
            .Returns(new ViewerLinkedBoards { SetBoardType = BoardType.OneShot, Boards = boards });

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task NoActiveBoard_ReturnsNull_ForQuestAndEvent()
    {
        _activeGroupContext.ActiveGroupId.Returns((int?)null);

        Assert.Null(await _sut.GetForQuestAsync(new Quest { Title = "Q" }, ActorId, Token));
        Assert.Null(await _sut.GetForEventAsync(new Event { Title = "E" }, ActorId, Token));
        await _linkedBoards.DidNotReceiveWithAnyArgs().GetLinkedBoardsForViewerAsync(default, default, default);
    }

    [Fact]
    public async Task BoardInNoSetOrSetOfOne_ReturnsNull()
    {
        SetLinkedBoards();

        Assert.Null(await _sut.GetForQuestAsync(new Quest { Title = "Q" }, ActorId, Token));
        Assert.Null(await _sut.GetForEventAsync(new Event { Title = "E", Date = _clock.Today.AddDays(2) }, ActorId, Token));
    }

    [Fact]
    public async Task LinkedBoard_OpenQuest_CanMove()
    {
        var state = await _sut.GetForQuestAsync(new Quest { Title = "Q" }, ActorId, Token);

        Assert.NotNull(state);
        Assert.Equal(MoveBlockReason.None, state.BlockReason);
        Assert.True(state.CanMove);
    }

    [Fact]
    public async Task LinkedBoard_ClosedQuest_IsClosed()
    {
        var state = await _sut.GetForQuestAsync(new Quest { Title = "Q", IsClosed = true }, ActorId, Token);

        Assert.NotNull(state);
        Assert.Equal(MoveBlockReason.Closed, state.BlockReason);
        Assert.False(state.CanMove);
    }

    [Fact]
    public async Task LinkedBoard_QuestFinalizedYesterday_IsPlayed()
    {
        var quest = new Quest
        {
            Title = "Q",
            IsFinalized = true,
            FinalizedDate = _clock.Today.AddDays(-1).ToDateTime(new TimeOnly(19, 0))
        };

        var state = await _sut.GetForQuestAsync(quest, ActorId, Token);

        Assert.NotNull(state);
        Assert.Equal(MoveBlockReason.Played, state.BlockReason);
    }

    [Fact]
    public async Task LinkedBoard_SeriesOccurrence_IsSeriesOccurrence()
    {
        var evt = new Event { Title = "E", Date = _clock.Today.AddDays(3), SeriesId = 5, SeriesSlotIndex = 0 };

        var state = await _sut.GetForEventAsync(evt, ActorId, Token);

        Assert.NotNull(state);
        Assert.Equal(MoveBlockReason.SeriesOccurrence, state.BlockReason);
    }

    [Fact]
    public async Task LinkedBoard_PastEvent_IsPast()
    {
        var state = await _sut.GetForEventAsync(new Event { Title = "E", Date = _clock.Today.AddDays(-1) }, ActorId, Token);

        Assert.NotNull(state);
        Assert.Equal(MoveBlockReason.Past, state.BlockReason);
    }

    [Fact]
    public async Task LinkedBoard_CancelledFutureEvent_CanMove()
    {
        var evt = new Event { Title = "E", Date = _clock.Today.AddDays(3), CancelledAt = new DateTime(2031, 6, 1, 0, 0, 0, DateTimeKind.Utc) };

        var state = await _sut.GetForEventAsync(evt, ActorId, Token);

        Assert.NotNull(state);
        Assert.Equal(MoveBlockReason.None, state.BlockReason);
    }

    [Fact]
    public async Task LinkedSet_IsReadOnEveryCall()
    {
        await _sut.GetForQuestAsync(new Quest { Title = "Q" }, ActorId, Token);
        await _linkedBoards.Received(1).GetLinkedBoardsForViewerAsync(SourceBoard, ActorId, Arg.Any<CancellationToken>());

        await _sut.GetForEventAsync(new Event { Title = "E", Date = _clock.Today.AddDays(2) }, ActorId, Token);
        await _linkedBoards.Received(2).GetLinkedBoardsForViewerAsync(SourceBoard, ActorId, Arg.Any<CancellationToken>());
    }
}
