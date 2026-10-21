using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.Domain.Models.BoardMove;
using QuestBoard.Domain.Models.QuestBoard;
using QuestBoard.Domain.Services;
using QuestBoard.UnitTests.Helpers;

namespace QuestBoard.UnitTests.Services;

public class BoardMoveTargetPageOffTests
{
    private const int SourceBoard = 1;
    private const int BravoBoard = 2;
    private const int CharlieBoard = 3;
    private const int QuestId = 40;
    private const int EventId = 60;
    private const int ActorId = 9;

    private readonly IQuestRepository _questRepository = Substitute.For<IQuestRepository>();
    private readonly IEventRepository _eventRepository = Substitute.For<IEventRepository>();
    private readonly IEventSignupRepository _signupRepository = Substitute.For<IEventSignupRepository>();
    private readonly IActiveGroupContext _activeGroupContext = Substitute.For<IActiveGroupContext>();
    private readonly ILinkedBoardSetRepository _linkedBoards = Substitute.For<ILinkedBoardSetRepository>();
    private readonly IQuestMoveRepository _questMoveRepository = Substitute.For<IQuestMoveRepository>();
    private readonly IEventMoveRepository _eventMoveRepository = Substitute.For<IEventMoveRepository>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IIdentityService _identityService = Substitute.For<IIdentityService>();
    private readonly IQuestEmailDispatcher _dispatcher = Substitute.For<IQuestEmailDispatcher>();
    private readonly FakeBoardClock _clock = new() { Today = new DateOnly(2031, 6, 15) };
    private readonly QuestMoveService _questService;
    private readonly EventMoveService _eventService;
    private readonly MoveControlService _controlService;

    public BoardMoveTargetPageOffTests()
    {
        _activeGroupContext.ActiveGroupId.Returns(SourceBoard);
        _linkedBoards.GetBoardAsync(SourceBoard, Arg.Any<CancellationToken>())
            .Returns(new LinkedBoardCandidate { Id = SourceBoard, Name = "Board Alpha" });

        _questRepository.GetQuestWithDetailsAsync(QuestId, Arg.Any<CancellationToken>())
            .Returns(new Quest { Id = QuestId, Title = "Goblin Market", Description = "D", DungeonMasterId = 5 });
        _eventRepository.GetEventWithDetailsAsync(EventId, Arg.Any<CancellationToken>())
            .Returns(new Event { Id = EventId, Title = "Harvest Fair", Date = _clock.Today.AddDays(3), GroupId = SourceBoard });
        _signupRepository.GetRosterForEventAsync(EventId, Arg.Any<CancellationToken>())
            .Returns(new List<EventSignup>());

        _userRepository.GetAllGroupMembers(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<User>());
        _userRepository.GetGroupRoleAsync(Arg.Any<int>(), Arg.Any<int>()).Returns(GroupRole.DungeonMaster);

        _questService = new QuestMoveService(
            _questRepository, _activeGroupContext, _linkedBoards, _questMoveRepository, _clock,
            _userRepository, _identityService, _dispatcher,
            NullLogger<QuestMoveService>.Instance);
        _eventService = new EventMoveService(
            _eventRepository, _signupRepository, _userRepository, _activeGroupContext, _linkedBoards,
            _eventMoveRepository, _clock, NullLogger<EventMoveService>.Instance);
        _controlService = new MoveControlService(_linkedBoards, _activeGroupContext, _clock);
    }

    private static LinkedBoard Board(int id, string name, BoardPageSet disabled = default) =>
        new() { Id = id, Name = name, ViewerRole = GroupRole.Admin, DisabledPages = disabled };

    private void SetLinkedBoards(params LinkedBoard[] boards) =>
        _linkedBoards.GetLinkedBoardsForViewerAsync(SourceBoard, ActorId, Arg.Any<CancellationToken>())
            .Returns(new ViewerLinkedBoards { SetBoardType = BoardType.OneShot, Boards = boards });

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task QuestPreview_TargetWithQuestsOff_IsTargetPageOff()
    {
        SetLinkedBoards(Board(BravoBoard, "Board Bravo", BoardPageSet.Empty.With(BoardPage.Quests)));

        var preview = await _questService.PreviewAsync(QuestId, BravoBoard, ActorId, Token);

        preview.Status.Should().Be(MoveStatus.TargetPageOff);
        preview.Target.Should().NotBeNull();
        preview.Target!.Name.Should().Be("Board Bravo");
    }

    [Fact]
    public async Task QuestPreview_OnlyLinkedBoardWithQuestsOff_IsTargetPageOffWithoutATargetId()
    {
        SetLinkedBoards(Board(BravoBoard, "Board Bravo", BoardPageSet.Empty.With(BoardPage.Quests)));

        var preview = await _questService.PreviewAsync(QuestId, null, ActorId, Token);

        preview.Status.Should().Be(MoveStatus.TargetPageOff);
    }

    [Fact]
    public async Task QuestPreview_TargetWithQuestsOn_IsNotRefused()
    {
        SetLinkedBoards(Board(BravoBoard, "Board Bravo", BoardPageSet.Empty.With(BoardPage.Events)));
        _userRepository.GetAllGroupMembers(BravoBoard, Arg.Any<CancellationToken>()).Returns(new List<User>());

        var preview = await _questService.PreviewAsync(QuestId, BravoBoard, ActorId, Token);

        preview.Status.Should().Be(MoveStatus.Ready);
    }

    [Fact]
    public async Task QuestMove_TargetWithQuestsOff_IsRefusedAndNothingIsApplied()
    {
        SetLinkedBoards(Board(BravoBoard, "Board Bravo", BoardPageSet.Empty.With(BoardPage.Quests)));

        var outcome = await _questService.MoveAsync(QuestId, BravoBoard, "any-stamp", ActorId, Token);

        outcome.Status.Should().Be(MoveStatus.TargetPageOff);
        await _questMoveRepository.DidNotReceiveWithAnyArgs().ApplyAsync(default!, default);
    }

    [Fact]
    public async Task EventPreview_TargetWithEventsOff_IsTargetPageOff()
    {
        SetLinkedBoards(Board(BravoBoard, "Board Bravo", BoardPageSet.Empty.With(BoardPage.Events)));

        var preview = await _eventService.PreviewAsync(EventId, BravoBoard, ActorId, Token);

        preview.Status.Should().Be(MoveStatus.TargetPageOff);
        preview.Target.Should().NotBeNull();
        preview.Target!.Name.Should().Be("Board Bravo");
    }

    [Fact]
    public async Task EventPreview_TargetWithEventsOn_IsNotRefused()
    {
        SetLinkedBoards(Board(BravoBoard, "Board Bravo", BoardPageSet.Empty.With(BoardPage.Quests)));

        var preview = await _eventService.PreviewAsync(EventId, BravoBoard, ActorId, Token);

        preview.Status.Should().Be(MoveStatus.Ready);
    }

    [Fact]
    public async Task EventMove_TargetWithEventsOff_IsRefusedAndNothingIsApplied()
    {
        SetLinkedBoards(Board(BravoBoard, "Board Bravo", BoardPageSet.Empty.With(BoardPage.Events)));

        var outcome = await _eventService.MoveAsync(EventId, BravoBoard, "any-stamp", ActorId, Token);

        outcome.Status.Should().Be(MoveStatus.TargetPageOff);
        await _eventMoveRepository.DidNotReceiveWithAnyArgs().ApplyAsync(default!, default);
    }

    [Fact]
    public async Task EventControl_OnlyLinkedBoardWithEventsOff_IsNull()
    {
        SetLinkedBoards(Board(BravoBoard, "Board Bravo", BoardPageSet.Empty.With(BoardPage.Events)));

        var state = await _controlService.GetForEventAsync(
            new Event { Title = "E", Date = _clock.Today.AddDays(2) }, ActorId, Token);

        state.Should().BeNull();
    }

    [Fact]
    public async Task EventControl_SecondLinkedBoardWithEventsOn_IsOffered()
    {
        SetLinkedBoards(
            Board(BravoBoard, "Board Bravo", BoardPageSet.Empty.With(BoardPage.Events)),
            Board(CharlieBoard, "Board Charlie"));

        var state = await _controlService.GetForEventAsync(
            new Event { Title = "E", Date = _clock.Today.AddDays(2) }, ActorId, Token);

        state.Should().NotBeNull();
    }

    [Fact]
    public async Task QuestControl_OnlyLinkedBoardWithQuestsOff_IsNull()
    {
        SetLinkedBoards(Board(BravoBoard, "Board Bravo", BoardPageSet.Empty.With(BoardPage.Quests)));

        var state = await _controlService.GetForQuestAsync(new Quest { Title = "Q" }, ActorId, Token);

        state.Should().BeNull();
    }

    [Fact]
    public async Task QuestControl_SecondLinkedBoardWithQuestsOn_IsOffered()
    {
        SetLinkedBoards(
            Board(BravoBoard, "Board Bravo", BoardPageSet.Empty.With(BoardPage.Quests)),
            Board(CharlieBoard, "Board Charlie"));

        var state = await _controlService.GetForQuestAsync(new Quest { Title = "Q" }, ActorId, Token);

        state.Should().NotBeNull();
    }

    [Fact]
    public async Task Controls_BoardWithOnlyOtherPagesOff_AreStillOffered()
    {
        SetLinkedBoards(Board(BravoBoard, "Board Bravo", BoardPageSet.Empty.With(BoardPage.Shop)));

        (await _controlService.GetForQuestAsync(new Quest { Title = "Q" }, ActorId, Token)).Should().NotBeNull();
        (await _controlService.GetForEventAsync(
            new Event { Title = "E", Date = _clock.Today.AddDays(2) }, ActorId, Token)).Should().NotBeNull();
    }
}
