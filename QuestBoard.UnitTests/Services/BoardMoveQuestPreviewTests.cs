using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Extensions;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.Domain.Models.BoardMove;
using QuestBoard.Domain.Models.QuestBoard;
using QuestBoard.Domain.Services;
using QuestBoard.UnitTests.Helpers;

namespace QuestBoard.UnitTests.Services;

public class BoardMoveQuestPreviewTests
{
    private const int SourceBoard = 1;
    private const int BravoBoard = 2;
    private const int CharlieBoard = 3;
    private const int QuestId = 40;
    private const int ActorId = 9;

    private readonly IQuestRepository _questRepository = Substitute.For<IQuestRepository>();
    private readonly IActiveGroupContext _activeGroupContext = Substitute.For<IActiveGroupContext>();
    private readonly ILinkedBoardSetRepository _linkedBoards = Substitute.For<ILinkedBoardSetRepository>();
    private readonly IQuestMoveRepository _moveRepository = Substitute.For<IQuestMoveRepository>();
    private readonly FakeBoardClock _clock = new() { Today = new DateOnly(2031, 6, 15) };
    private readonly QuestMoveService _sut;

    public BoardMoveQuestPreviewTests()
    {
        _activeGroupContext.ActiveGroupId.Returns(SourceBoard);
        _linkedBoards.GetBoardAsync(SourceBoard, Arg.Any<CancellationToken>())
            .Returns(new LinkedBoardCandidate { Id = SourceBoard, Name = "Board Alpha" });
        _questRepository.GetQuestWithDetailsAsync(QuestId, Arg.Any<CancellationToken>())
            .Returns(MakeQuest());
        SetLinkedBoards(Board(BravoBoard, "Board Bravo"));

        _sut = new QuestMoveService(
            _questRepository, _activeGroupContext, _linkedBoards, _moveRepository, _clock,
            NullLogger<QuestMoveService>.Instance);
    }

    private static LinkedBoard Board(int id, string name) =>
        new() { Id = id, Name = name, ViewerRole = GroupRole.Admin };

    private void SetLinkedBoards(params LinkedBoard[] boards) =>
        _linkedBoards.GetLinkedBoardsForViewerAsync(SourceBoard, ActorId, Arg.Any<CancellationToken>())
            .Returns(new ViewerLinkedBoards { SetBoardType = BoardType.OneShot, Boards = boards });

    private static Quest MakeQuest(int signups = 2, bool isClosed = false)
    {
        var quest = new Quest
        {
            Id = QuestId,
            Title = "Goblin Market",
            Description = "D",
            DungeonMasterId = 5,
            IsClosed = isClosed
        };
        for (var i = 1; i <= signups; i++)
        {
            quest.PlayerSignups.Add(new PlayerSignup
            {
                Id = 100 + i,
                Player = new User { Id = 200 + i, Name = $"Player {i}", Email = $"p{i}@test.com" },
                Quest = quest
            });
        }

        return quest;
    }

    private void SetQuest(Quest? quest) =>
        _questRepository.GetQuestWithDetailsAsync(QuestId, Arg.Any<CancellationToken>()).Returns(quest);

    [Fact]
    public async Task PreviewAsync_NoActiveBoard_IsNotFound()
    {
        _activeGroupContext.ActiveGroupId.Returns((int?)null);

        var preview = await _sut.PreviewAsync(QuestId, BravoBoard, ActorId, TestContext.Current.CancellationToken);

        preview.Status.Should().Be(MoveStatus.NotFound);
    }

    [Fact]
    public async Task PreviewAsync_QuestAbsent_IsNotFound()
    {
        SetQuest(null);

        var preview = await _sut.PreviewAsync(QuestId, BravoBoard, ActorId, TestContext.Current.CancellationToken);

        preview.Status.Should().Be(MoveStatus.NotFound);
    }

    [Fact]
    public async Task PreviewAsync_ClosedQuest_IsBlockedAsClosed()
    {
        SetQuest(MakeQuest(isClosed: true));

        var preview = await _sut.PreviewAsync(QuestId, BravoBoard, ActorId, TestContext.Current.CancellationToken);

        preview.Status.Should().Be(MoveStatus.Blocked);
        preview.BlockReason.Should().Be(MoveBlockReason.Closed);
        preview.EntryTitle.Should().Be("Goblin Market");
        preview.SourceBoardName.Should().Be("Board Alpha");
    }

    [Fact]
    public async Task PreviewAsync_QuestPlayedYesterdayOnTheBoardClock_IsBlockedAsPlayed()
    {
        var quest = MakeQuest();
        quest.IsFinalized = true;
        quest.FinalizedDate = _clock.Today.AddDays(-1).ToDateTime(new TimeOnly(19, 0));
        SetQuest(quest);

        var preview = await _sut.PreviewAsync(QuestId, BravoBoard, ActorId, TestContext.Current.CancellationToken);

        preview.Status.Should().Be(MoveStatus.Blocked);
        preview.BlockReason.Should().Be(MoveBlockReason.Played);
    }

    [Fact]
    public async Task PreviewAsync_QuestFinalizedToday_IsReady()
    {
        var quest = MakeQuest();
        quest.IsFinalized = true;
        quest.FinalizedDate = _clock.Today.ToDateTime(new TimeOnly(19, 0));
        SetQuest(quest);

        var preview = await _sut.PreviewAsync(QuestId, BravoBoard, ActorId, TestContext.Current.CancellationToken);

        preview.Status.Should().Be(MoveStatus.Ready);
    }

    [Fact]
    public async Task PreviewAsync_EmptyLinkedSet_IsNoLinkedBoards()
    {
        SetLinkedBoards();

        var preview = await _sut.PreviewAsync(QuestId, null, ActorId, TestContext.Current.CancellationToken);

        preview.Status.Should().Be(MoveStatus.NoLinkedBoards);
    }

    [Fact]
    public async Task PreviewAsync_OneOtherBoardAndNoTarget_PreselectsThatBoard()
    {
        var preview = await _sut.PreviewAsync(QuestId, null, ActorId, TestContext.Current.CancellationToken);

        preview.Status.Should().Be(MoveStatus.Ready);
        preview.Target.Should().NotBeNull();
        preview.Target!.Id.Should().Be(BravoBoard);
        preview.Stamp.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task PreviewAsync_TwoOtherBoardsAndNoTarget_AsksForATarget()
    {
        SetLinkedBoards(Board(BravoBoard, "Board Bravo"), Board(CharlieBoard, "Board Charlie"));

        var preview = await _sut.PreviewAsync(QuestId, null, ActorId, TestContext.Current.CancellationToken);

        preview.Status.Should().Be(MoveStatus.ChooseTarget);
        preview.LinkedBoards.Select(b => b.Id).Should().BeEquivalentTo([BravoBoard, CharlieBoard]);
        preview.Target.Should().BeNull();
    }

    [Theory]
    [InlineData(99)]
    [InlineData(SourceBoard)]
    public async Task PreviewAsync_TargetOutsideTheFreshSetOrTheSourceItself_IsTargetNotLinked(int targetBoardId)
    {
        var preview = await _sut.PreviewAsync(QuestId, targetBoardId, ActorId, TestContext.Current.CancellationToken);

        preview.Status.Should().Be(MoveStatus.TargetNotLinked);
        await _moveRepository.DidNotReceiveWithAnyArgs().ApplyAsync(default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task PreviewAsync_IdenticalInputs_GiveIdenticalStamps()
    {
        var first = await _sut.PreviewAsync(QuestId, BravoBoard, ActorId, TestContext.Current.CancellationToken);
        var second = await _sut.PreviewAsync(QuestId, BravoBoard, ActorId, TestContext.Current.CancellationToken);

        first.Stamp.Should().Be(second.Stamp);
    }

    [Fact]
    public async Task PreviewAsync_AnAddedSignup_ChangesTheStamp()
    {
        var before = await _sut.PreviewAsync(QuestId, BravoBoard, ActorId, TestContext.Current.CancellationToken);
        SetQuest(MakeQuest(signups: 3));

        var after = await _sut.PreviewAsync(QuestId, BravoBoard, ActorId, TestContext.Current.CancellationToken);

        after.Stamp.Should().NotBe(before.Stamp);
    }

    [Fact]
    public async Task PreviewAsync_ADifferentTarget_ChangesTheStamp()
    {
        SetLinkedBoards(Board(BravoBoard, "Board Bravo"), Board(CharlieBoard, "Board Charlie"));

        var bravo = await _sut.PreviewAsync(QuestId, BravoBoard, ActorId, TestContext.Current.CancellationToken);
        var charlie = await _sut.PreviewAsync(QuestId, CharlieBoard, ActorId, TestContext.Current.CancellationToken);

        charlie.Stamp.Should().NotBe(bravo.Stamp);
    }

    [Fact]
    public async Task MoveAsync_StaleStamp_ReturnsChangedWithAFreshStampAndNeverApplies()
    {
        var preview = await _sut.PreviewAsync(QuestId, BravoBoard, ActorId, TestContext.Current.CancellationToken);

        var outcome = await _sut.MoveAsync(QuestId, BravoBoard, "stale", ActorId, TestContext.Current.CancellationToken);

        outcome.Status.Should().Be(MoveStatus.Changed);
        outcome.Stamp.Should().Be(preview.Stamp);
        await _moveRepository.DidNotReceiveWithAnyArgs().ApplyAsync(default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task MoveAsync_MatchingStamp_AppliesOnce()
    {
        var preview = await _sut.PreviewAsync(QuestId, BravoBoard, ActorId, TestContext.Current.CancellationToken);
        _moveRepository.ApplyAsync(Arg.Any<QuestMovePlan>(), Arg.Any<CancellationToken>()).Returns(MoveStatus.Moved);

        var outcome = await _sut.MoveAsync(QuestId, BravoBoard, preview.Stamp, ActorId, TestContext.Current.CancellationToken);

        outcome.Status.Should().Be(MoveStatus.Moved);
        await _moveRepository.Received(1).ApplyAsync(
            Arg.Is<QuestMovePlan>(p => p.QuestId == QuestId && p.TargetGroupId == BravoBoard),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MoveAsync_RepositoryReportsChanged_ReturnsChangedWithARecomputedPreview()
    {
        var preview = await _sut.PreviewAsync(QuestId, BravoBoard, ActorId, TestContext.Current.CancellationToken);
        _moveRepository.ApplyAsync(Arg.Any<QuestMovePlan>(), Arg.Any<CancellationToken>()).Returns(MoveStatus.Changed);

        var outcome = await _sut.MoveAsync(QuestId, BravoBoard, preview.Stamp, ActorId, TestContext.Current.CancellationToken);

        outcome.Status.Should().Be(MoveStatus.Changed);
        outcome.Stamp.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task MoveAsync_RepositoryReportsChangedAndTheQuestIsNowBlocked_ReturnsTheRefusal()
    {
        var preview = await _sut.PreviewAsync(QuestId, BravoBoard, ActorId, TestContext.Current.CancellationToken);
        _moveRepository.ApplyAsync(Arg.Any<QuestMovePlan>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                SetQuest(MakeQuest(isClosed: true));
                return MoveStatus.Changed;
            });

        var outcome = await _sut.MoveAsync(QuestId, BravoBoard, preview.Stamp, ActorId, TestContext.Current.CancellationToken);

        outcome.Status.Should().Be(MoveStatus.Blocked);
        outcome.BlockReason.Should().Be(MoveBlockReason.Closed);
    }

    [Fact]
    public async Task MoveAsync_ClosedQuest_IsBlockedWithoutApplying()
    {
        SetQuest(MakeQuest(isClosed: true));

        var outcome = await _sut.MoveAsync(QuestId, BravoBoard, "anything", ActorId, TestContext.Current.CancellationToken);

        outcome.Status.Should().Be(MoveStatus.Blocked);
        outcome.BlockReason.Should().Be(MoveBlockReason.Closed);
        await _moveRepository.DidNotReceiveWithAnyArgs().ApplyAsync(default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task MoveAsync_TargetNotLinked_IsRefusedWithoutApplying()
    {
        var outcome = await _sut.MoveAsync(QuestId, CharlieBoard, "anything", ActorId, TestContext.Current.CancellationToken);

        outcome.Status.Should().Be(MoveStatus.TargetNotLinked);
        await _moveRepository.DidNotReceiveWithAnyArgs().ApplyAsync(default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task MoveAsync_ApplyThrows_IsFailed()
    {
        var preview = await _sut.PreviewAsync(QuestId, BravoBoard, ActorId, TestContext.Current.CancellationToken);
        _moveRepository.ApplyAsync(Arg.Any<QuestMovePlan>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("store down"));

        var outcome = await _sut.MoveAsync(QuestId, BravoBoard, preview.Stamp, ActorId, TestContext.Current.CancellationToken);

        outcome.Status.Should().Be(MoveStatus.Failed);
    }

    [Fact]
    public void Stamp_SignupOrderDoesNotMatter()
    {
        var a = MovePreviewStamp.Compute("quest", 1, 2, [3, 1, 2], [], [], [], []);
        var b = MovePreviewStamp.Compute("quest", 1, 2, [1, 2, 3], [], [], [], []);

        a.Should().Be(b);
    }
}
