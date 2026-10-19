using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.Domain.Models.BoardMove;
using QuestBoard.Domain.Models.QuestBoard;
using QuestBoard.Domain.Services;
using QuestBoard.UnitTests.Helpers;

namespace QuestBoard.UnitTests.Services;

// Who is mailed when a quest moves: once each, at a confirmed address, under the target board,
// and only after the move is committed.
public class BoardMoveQuestMailTests
{
    private const int SourceBoard = 1;
    private const int Bravo = 2;
    private const int QuestId = 40;
    private const int ActorId = 9;
    private const int DmId = 5;
    private const int FinalizedPdId = 70;

    private static readonly DateTime Session = new(2031, 6, 20, 19, 0, 0);

    private readonly IQuestRepository _questRepository = Substitute.For<IQuestRepository>();
    private readonly IActiveGroupContext _activeGroupContext = Substitute.For<IActiveGroupContext>();
    private readonly ILinkedBoardSetRepository _linkedBoards = Substitute.For<ILinkedBoardSetRepository>();
    private readonly IQuestMoveRepository _moveRepository = Substitute.For<IQuestMoveRepository>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IIdentityService _identityService = Substitute.For<IIdentityService>();
    private readonly IQuestEmailDispatcher _dispatcher = Substitute.For<IQuestEmailDispatcher>();
    private readonly FakeBoardClock _clock = new() { Today = new DateOnly(2031, 6, 15) };
    private readonly QuestMoveService _sut;

    public BoardMoveQuestMailTests()
    {
        _activeGroupContext.ActiveGroupId.Returns(SourceBoard);
        _linkedBoards.GetBoardAsync(SourceBoard, Arg.Any<CancellationToken>())
            .Returns(new LinkedBoardCandidate { Id = SourceBoard, Name = "Board Alpha" });
        _linkedBoards.GetLinkedBoardsForViewerAsync(SourceBoard, ActorId, Arg.Any<CancellationToken>())
            .Returns(new ViewerLinkedBoards
            {
                SetBoardType = BoardType.OneShot,
                Boards = [new LinkedBoard { Id = Bravo, Name = "Board Bravo", ViewerRole = GroupRole.Admin }]
            });
        _userRepository.GetGroupRoleAsync(DmId, Bravo).Returns(GroupRole.DungeonMaster);
        _moveRepository.ApplyAsync(Arg.Any<QuestMovePlan>(), Arg.Any<CancellationToken>()).Returns(MoveStatus.Moved);

        _sut = new QuestMoveService(
            _questRepository, _activeGroupContext, _linkedBoards, _moveRepository, _clock,
            _userRepository, _identityService, _dispatcher, NullLogger<QuestMoveService>.Instance);
    }

    private static PlayerSignup Signup(
        int id, string name, SignupRole role = SignupRole.Player, bool selected = false,
        VoteType? vote = null, bool confirmed = true, string? email = "")
    {
        var signup = new PlayerSignup
        {
            Id = id,
            Player = new User
            {
                Id = id + 1000, Name = name, EmailConfirmed = confirmed,
                Email = email == "" ? $"{name.ToLowerInvariant()}@test.com" : email
            },
            Quest = new Quest { Id = QuestId },
            Role = role,
            IsSelected = selected
        };
        if (vote != null)
        {
            signup.DateVotes = [new PlayerDateVote { ProposedDateId = FinalizedPdId, Vote = vote }];
        }

        return signup;
    }

    private void Given(bool finalized, PlayerSignup[] signups, PlayerSignup[] targetMembers, User? dm = null)
    {
        var quest = new Quest
        {
            Id = QuestId,
            Title = "Goblin Market",
            Description = "A market",
            ChallengeRating = 4,
            DungeonMasterId = DmId,
            DungeonMaster = dm ?? new User { Id = DmId, Name = "Dana", Email = "dana@test.com", EmailConfirmed = true },
            IsFinalized = finalized,
            FinalizedDate = finalized ? Session : null,
            ProposedDates = [new ProposedDate { Id = FinalizedPdId, QuestId = QuestId, Date = Session }]
        };
        foreach (var signup in signups)
        {
            quest.PlayerSignups.Add(signup);
        }

        _questRepository.GetQuestWithDetailsAsync(QuestId, Arg.Any<CancellationToken>()).Returns(quest);
        _userRepository.GetAllGroupMembers(Bravo, Arg.Any<CancellationToken>())
            .Returns(targetMembers.Select(s => s.Player).ToList());
    }

    private async Task<QuestMovePreview> MoveAsync()
    {
        var token = TestContext.Current.CancellationToken;
        var preview = await _sut.PreviewAsync(QuestId, Bravo, ActorId, token);
        return await _sut.MoveAsync(QuestId, Bravo, preview.Stamp, ActorId, token);
    }

    private void ReceivedMoved(QuestMovedAudience audience, string email, DateTime? sessionDate, int times = 1) =>
        _dispatcher.Received(times).EnqueueQuestMovedEmail(
            QuestId, Bravo, audience, email, "Goblin Market", "Dana", "Board Bravo", sessionDate);

    [Fact]
    public async Task Moved_MailsTheDmEachKeptPlayerAndEachRemovedPlayerUnderTheTargetBoard()
    {
        var kept = Signup(1, "Kept");
        var removed = Signup(2, "Removed");
        Given(true, [kept, removed], [kept]);

        var outcome = await MoveAsync();

        outcome.Status.Should().Be(MoveStatus.Moved);
        ReceivedMoved(QuestMovedAudience.DungeonMaster, "dana@test.com", Session);
        ReceivedMoved(QuestMovedAudience.Player, "kept@test.com", Session);
        ReceivedMoved(QuestMovedAudience.Removed, "removed@test.com", Session);
        _dispatcher.ReceivedWithAnyArgs(3).EnqueueQuestMovedEmail(default, default, default, default!, default!, default!, default!, default);
    }

    [Fact]
    public async Task Moved_OpenQuest_CarriesNoSessionDate()
    {
        var kept = Signup(1, "Kept");
        Given(false, [kept], [kept]);

        await MoveAsync();

        ReceivedMoved(QuestMovedAudience.DungeonMaster, "dana@test.com", null);
        ReceivedMoved(QuestMovedAudience.Player, "kept@test.com", null);
    }

    [Fact]
    public async Task Moved_PromotedPlayer_GetsOnlyTheAfterMoveWaitlistMail()
    {
        var gone = Signup(1, "Gone", selected: true);
        var promoted = Signup(2, "Promoted", vote: VoteType.Yes);
        Given(true, [gone, promoted], [promoted]);

        await MoveAsync();

        _dispatcher.Received(1).EnqueueWaitlistPromotedAfterMoveEmail(
            QuestId, Bravo, Session, "promoted@test.com", "Promoted", "Goblin Market", "Dana", "A market", 4, "Board Bravo");
        _dispatcher.DidNotReceive().EnqueueQuestMovedEmail(
            Arg.Any<int>(), Arg.Any<int>(), QuestMovedAudience.Player, "promoted@test.com",
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateTime?>());
    }

    [Fact]
    public async Task Moved_UnconfirmedOrEmptyAddresses_GetNothing()
    {
        var unconfirmed = Signup(1, "Unconfirmed", confirmed: false);
        var noEmail = Signup(2, "NoEmail", email: null);
        var emptyEmail = Signup(3, "EmptyEmail", email: " ");
        emptyEmail.Player.Email = string.Empty;
        var removedUnconfirmed = Signup(4, "RemovedUnconfirmed", confirmed: false);
        Given(true, [unconfirmed, noEmail, emptyEmail, removedUnconfirmed], [unconfirmed, noEmail, emptyEmail]);

        await MoveAsync();

        _dispatcher.ReceivedWithAnyArgs(1).EnqueueQuestMovedEmail(default, default, default, default!, default!, default!, default!, default);
        ReceivedMoved(QuestMovedAudience.DungeonMaster, "dana@test.com", Session);
    }

    [Fact]
    public async Task Moved_TheActingSuperAdmin_IsNeverMailedAsDmOrSignup()
    {
        var actorSignup = Signup(1, "Actor");
        actorSignup.Player.Id = ActorId;
        var actorDm = new User { Id = ActorId, Name = "Dana", Email = "dana@test.com", EmailConfirmed = true };
        _userRepository.GetGroupRoleAsync(ActorId, Bravo).Returns(GroupRole.DungeonMaster);
        Given(true, [actorSignup], [actorSignup], dm: actorDm);
        _questRepository.GetQuestWithDetailsAsync(QuestId, Arg.Any<CancellationToken>()).Returns(callInfo =>
        {
            var quest = new Quest
            {
                Id = QuestId, Title = "Goblin Market", Description = "A market", DungeonMasterId = ActorId,
                DungeonMaster = actorDm, IsFinalized = true, FinalizedDate = Session,
                ProposedDates = [new ProposedDate { Id = FinalizedPdId, QuestId = QuestId, Date = Session }]
            };
            quest.PlayerSignups.Add(actorSignup);
            return quest;
        });

        await MoveAsync();

        _dispatcher.DidNotReceiveWithAnyArgs().EnqueueQuestMovedEmail(default, default, default, default!, default!, default!, default!, default);
        _dispatcher.DidNotReceiveWithAnyArgs().EnqueueWaitlistPromotedAfterMoveEmail(default, default, default, default!, default!, default!, default!, default!, default, default!);
    }

    [Fact]
    public async Task Moved_DmWhoAlsoHoldsASignup_GetsOnlyTheDungeonMasterMail()
    {
        var dmSignup = Signup(1, "Dana", SignupRole.AssistantDM);
        dmSignup.Player.Id = DmId;
        dmSignup.Player.Email = "dana@test.com";
        Given(true, [dmSignup], [dmSignup]);

        await MoveAsync();

        ReceivedMoved(QuestMovedAudience.DungeonMaster, "dana@test.com", Session);
        _dispatcher.ReceivedWithAnyArgs(1).EnqueueQuestMovedEmail(default, default, default, default!, default!, default!, default!, default);
    }

    [Theory]
    [InlineData(MoveStatus.Changed)]
    [InlineData(MoveStatus.Failed)]
    public async Task NotMoved_NoMailIsEnqueued(MoveStatus applyOutcome)
    {
        var kept = Signup(1, "Kept");
        Given(true, [kept], [kept]);
        _moveRepository.ApplyAsync(Arg.Any<QuestMovePlan>(), Arg.Any<CancellationToken>()).Returns(applyOutcome);

        await MoveAsync();

        AssertNothingEnqueued();
    }

    [Fact]
    public async Task Refused_NoMailIsEnqueued()
    {
        var kept = Signup(1, "Kept");
        Given(true, [kept], [kept]);
        _userRepository.GetGroupRoleAsync(DmId, Bravo).Returns(GroupRole.Player);

        var outcome = await _sut.MoveAsync(QuestId, Bravo, "anything", ActorId, TestContext.Current.CancellationToken);

        outcome.Status.Should().Be(MoveStatus.DungeonMasterNotEligible);
        AssertNothingEnqueued();
    }

    [Fact]
    public async Task StaleStamp_NoMailIsEnqueued()
    {
        var kept = Signup(1, "Kept");
        Given(true, [kept], [kept]);

        await _sut.MoveAsync(QuestId, Bravo, "stale", ActorId, TestContext.Current.CancellationToken);

        AssertNothingEnqueued();
    }

    [Fact]
    public async Task Moved_DispatcherThrowingOnOneCall_StillMovedAndTheOthersAreSent()
    {
        var first = Signup(1, "First");
        var second = Signup(2, "Second");
        Given(true, [first, second], [first, second]);
        _dispatcher
            .When(d => d.EnqueueQuestMovedEmail(
                Arg.Any<int>(), Arg.Any<int>(), QuestMovedAudience.Player, "first@test.com",
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateTime?>()))
            .Throw(new InvalidOperationException("queue down"));

        var outcome = await MoveAsync();

        outcome.Status.Should().Be(MoveStatus.Moved);
        ReceivedMoved(QuestMovedAudience.Player, "second@test.com", Session);
        ReceivedMoved(QuestMovedAudience.DungeonMaster, "dana@test.com", Session);
    }

    [Fact]
    public async Task Moved_EnqueueRunsOnlyAfterTheSaveReturned()
    {
        var kept = Signup(1, "Kept");
        Given(true, [kept], [kept]);
        _moveRepository.ApplyAsync(Arg.Any<QuestMovePlan>(), Arg.Any<CancellationToken>()).Returns(callInfo =>
        {
            _dispatcher.ReceivedWithAnyArgs(0).EnqueueQuestMovedEmail(default, default, default, default!, default!, default!, default!, default);
            return MoveStatus.Moved;
        });

        var outcome = await MoveAsync();

        outcome.Status.Should().Be(MoveStatus.Moved);
        _dispatcher.ReceivedWithAnyArgs().EnqueueQuestMovedEmail(default, default, default, default!, default!, default!, default!, default);
    }

    private void AssertNothingEnqueued()
    {
        _dispatcher.DidNotReceiveWithAnyArgs().EnqueueQuestMovedEmail(default, default, default, default!, default!, default!, default!, default);
        _dispatcher.DidNotReceiveWithAnyArgs().EnqueueWaitlistPromotedAfterMoveEmail(default, default, default, default!, default!, default!, default!, default!, default, default!);
    }
}
