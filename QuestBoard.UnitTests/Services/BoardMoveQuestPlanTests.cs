using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Extensions;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.Domain.Models.BoardMove;
using QuestBoard.Domain.Models.QuestBoard;
using QuestBoard.Domain.Services;
using QuestBoard.UnitTests.Helpers;

namespace QuestBoard.UnitTests.Services;

// The quest preview's plan against the target board: who stays, who goes, which seats open and
// who fills them, which chain links are cut, and whether the Dungeon Master can run the quest there.
public class BoardMoveQuestPlanTests
{
    private const int SourceBoard = 1;
    private const int Bravo = 2;
    private const int QuestId = 40;
    private const int ActorId = 9;
    private const int DmId = 5;
    private const int FinalizedPdId = 70;

    private static readonly DateTime Session = new(2031, 6, 20, 19, 0, 0);
    private static readonly DateTime T0 = new(2031, 1, 1, 8, 0, 0);

    private readonly IQuestRepository _questRepository = Substitute.For<IQuestRepository>();
    private readonly IActiveGroupContext _activeGroupContext = Substitute.For<IActiveGroupContext>();
    private readonly ILinkedBoardSetRepository _linkedBoards = Substitute.For<ILinkedBoardSetRepository>();
    private readonly IQuestMoveRepository _moveRepository = Substitute.For<IQuestMoveRepository>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IIdentityService _identityService = Substitute.For<IIdentityService>();
    private readonly IQuestEmailDispatcher _dispatcher = Substitute.For<IQuestEmailDispatcher>();
    private readonly FakeBoardClock _clock = new() { Today = new DateOnly(2031, 6, 15) };
    private readonly QuestMoveService _sut;

    public BoardMoveQuestPlanTests()
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

        _sut = new QuestMoveService(
            _questRepository, _activeGroupContext, _linkedBoards, _moveRepository, _clock,
            _userRepository, _identityService, _dispatcher, NullLogger<QuestMoveService>.Instance);
    }

    private static PlayerSignup Signup(
        int id, string name, SignupRole role = SignupRole.Player, bool selected = false,
        VoteType? vote = null, int? characterId = null, DateTime? signedUp = null, DateTime? voteChanged = null)
    {
        var signup = new PlayerSignup
        {
            Id = id,
            Player = new User { Id = id + 1000, Name = name, Email = $"{name}@test.com", EmailConfirmed = true },
            Quest = new Quest { Id = QuestId },
            Role = role,
            IsSelected = selected,
            CharacterId = characterId,
            SignupTime = signedUp ?? T0,
            LastVoteChangeTime = voteChanged
        };
        if (vote != null)
        {
            signup.DateVotes = [new PlayerDateVote { ProposedDateId = FinalizedPdId, Vote = vote }];
        }

        return signup;
    }

    private static Quest Quest(bool finalized, params PlayerSignup[] signups)
    {
        var quest = new Quest
        {
            Id = QuestId,
            Title = "Goblin Market",
            Description = "D",
            DungeonMasterId = DmId,
            DungeonMaster = new User { Id = DmId, Name = "Dana" },
            IsFinalized = finalized,
            FinalizedDate = finalized ? Session : null,
            ProposedDates = [new ProposedDate { Id = FinalizedPdId, QuestId = QuestId, Date = Session }]
        };
        foreach (var signup in signups)
        {
            quest.PlayerSignups.Add(signup);
        }

        return quest;
    }

    private void Given(Quest quest, params PlayerSignup[] targetMembers)
    {
        _questRepository.GetQuestWithDetailsAsync(QuestId, Arg.Any<CancellationToken>()).Returns(quest);
        _userRepository.GetAllGroupMembers(Bravo, Arg.Any<CancellationToken>())
            .Returns(targetMembers.Select(s => s.Player).ToList());
    }

    private Task<QuestMovePreview> PreviewAsync() =>
        _sut.PreviewAsync(QuestId, Bravo, ActorId, TestContext.Current.CancellationToken);

    [Fact]
    public async Task Preview_TargetMembersAreKeptAndNonMembersOfEveryRoleDropped()
    {
        var kept = Signup(1, "Kept");
        var droppedPlayer = Signup(2, "Zed", selected: true);
        var droppedAssistant = Signup(3, "Amy", SignupRole.AssistantDM);
        var droppedSpectator = Signup(4, "Bob", SignupRole.Spectator);
        Given(Quest(false, kept, droppedPlayer, droppedAssistant, droppedSpectator), kept);

        var preview = await PreviewAsync();

        preview.Status.Should().Be(MoveStatus.Ready);
        preview.KeptCount.Should().Be(1);
        preview.Plan!.KeptSignupIds.Should().Equal(1);
        preview.Plan.DroppedSignupIds.Should().BeEquivalentTo([2, 3, 4]);
    }

    [Fact]
    public async Task Preview_DroppedPeopleListSelectedFirstThenByNameIgnoringCase()
    {
        var a = Signup(1, "carl");
        var b = Signup(2, "Zed", selected: true);
        var c = Signup(3, "alice");
        var d = Signup(4, "Bea", selected: true);
        Given(Quest(false, a, b, c, d));

        var preview = await PreviewAsync();

        preview.Dropped.Select(p => p.Name).Should().Equal("Bea", "Zed", "alice", "carl");
        preview.Dropped.Select(p => p.IsSelected).Should().Equal(true, true, false, false);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public async Task Preview_CharactersClearedOnlyWhenAKeptSignupHasACharacter(bool hasCharacter, bool expected)
    {
        var kept = Signup(1, "Kept", characterId: hasCharacter ? 77 : null);
        var droppedWithCharacter = Signup(2, "Gone", characterId: 78);
        Given(Quest(false, kept, droppedWithCharacter), kept);

        var preview = await PreviewAsync();

        (preview.CharactersCleared > 0).Should().Be(expected);
    }

    [Fact]
    public async Task Preview_OneSelectedPlayerDropped_PromotesTheTopKeptYesVoter()
    {
        var gone = Signup(1, "Gone", selected: true);
        var yes = Signup(2, "Yes", vote: VoteType.Yes);
        var maybe = Signup(3, "Maybe", vote: VoteType.Maybe);
        Given(Quest(true, gone, yes, maybe), yes, maybe);

        var preview = await PreviewAsync();

        preview.SeatsOpened.Should().Be(1);
        preview.PromotedCount.Should().Be(1);
        preview.Plan!.PromotedSignupIds.Should().Equal(2);
    }

    [Fact]
    public async Task Preview_TwoSeatsFreed_PromotesTheTopTwoInWaitlistOrder()
    {
        var goneA = Signup(1, "GoneA", selected: true);
        var goneB = Signup(2, "GoneB", selected: true);
        var maybe = Signup(3, "Maybe", vote: VoteType.Maybe, signedUp: T0);
        var lateYes = Signup(4, "LateYes", vote: VoteType.Yes, signedUp: T0.AddHours(5));
        var earlyYes = Signup(5, "EarlyYes", vote: VoteType.Yes, signedUp: T0.AddHours(1));
        Given(Quest(true, goneA, goneB, maybe, lateYes, earlyYes), maybe, lateYes, earlyYes);

        var preview = await PreviewAsync();

        preview.Plan!.PromotedSignupIds.Should().Equal(5, 4);
    }

    [Fact]
    public async Task Preview_NoVoteNoVoteAndAssistantDmAreNeverPromoted()
    {
        var gone = Signup(1, "Gone", selected: true);
        var no = Signup(2, "No", vote: VoteType.No);
        var none = Signup(3, "None");
        var assistant = Signup(4, "Assistant", SignupRole.AssistantDM, vote: VoteType.Yes);
        Given(Quest(true, gone, no, none, assistant), no, none, assistant);

        var preview = await PreviewAsync();

        preview.SeatsOpened.Should().Be(1);
        preview.PromotedCount.Should().Be(0);
    }

    [Fact]
    public async Task Preview_OpenQuest_PromotesNobodyButStillCountsTheOpenedSeats()
    {
        var gone = Signup(1, "Gone", selected: true);
        var yes = Signup(2, "Yes", vote: VoteType.Yes);
        Given(Quest(false, gone, yes), yes);

        var preview = await PreviewAsync();

        preview.SeatsOpened.Should().Be(1);
        preview.PromotedCount.Should().Be(0);
    }

    [Fact]
    public async Task Preview_FinalizedDateMatchingNoProposedDate_PromotesNobody()
    {
        var gone = Signup(1, "Gone", selected: true);
        var yes = Signup(2, "Yes", vote: VoteType.Yes);
        var quest = Quest(true, gone, yes);
        quest.ProposedDates = [new ProposedDate { Id = FinalizedPdId, QuestId = QuestId, Date = Session.AddDays(3) }];
        Given(quest, yes);

        var preview = await PreviewAsync();

        preview.SeatsOpened.Should().Be(1);
        preview.PromotedCount.Should().Be(0);
    }

    [Fact]
    public async Task Preview_DroppedNonPlayerOrUnselectedSignups_OpenNoSeat()
    {
        var spectator = Signup(1, "Spec", SignupRole.Spectator, selected: true);
        var waiting = Signup(2, "Waiting");
        var yes = Signup(3, "Yes", vote: VoteType.Yes);
        Given(Quest(true, spectator, waiting, yes), yes);

        var preview = await PreviewAsync();

        preview.SeatsOpened.Should().Be(0);
        preview.PromotedCount.Should().Be(0);
    }

    [Fact]
    public async Task Preview_DmWithNoRowOnTheTarget_IsNotEligible()
    {
        Given(Quest(false));
        _userRepository.GetGroupRoleAsync(DmId, Bravo).Returns((GroupRole?)null);

        var preview = await PreviewAsync();

        preview.Status.Should().Be(MoveStatus.DungeonMasterNotEligible);
        preview.Plan.Should().BeNull();
        preview.Stamp.Should().BeEmpty();
        preview.DmName.Should().Be("Dana");
        preview.Target!.Id.Should().Be(Bravo);
    }

    [Fact]
    public async Task Preview_DmWithOnlyAPlayerRow_IsNotEligible()
    {
        Given(Quest(false));
        _userRepository.GetGroupRoleAsync(DmId, Bravo).Returns(GroupRole.Player);

        var preview = await PreviewAsync();

        preview.Status.Should().Be(MoveStatus.DungeonMasterNotEligible);
    }

    [Theory]
    [InlineData(GroupRole.DungeonMaster)]
    [InlineData(GroupRole.Admin)]
    public async Task Preview_DmWhoIsDungeonMasterOrAdminOnTheTarget_IsReady(GroupRole role)
    {
        Given(Quest(false));
        _userRepository.GetGroupRoleAsync(DmId, Bravo).Returns(role);

        var preview = await PreviewAsync();

        preview.Status.Should().Be(MoveStatus.Ready);
    }

    [Fact]
    public async Task Preview_SuperAdminDmWithNoRowOnTheTarget_IsReady()
    {
        Given(Quest(false));
        _userRepository.GetGroupRoleAsync(DmId, Bravo).Returns((GroupRole?)null);
        _identityService.IsSuperAdminAsync(DmId).Returns(true);

        var preview = await PreviewAsync();

        preview.Status.Should().Be(MoveStatus.Ready);
    }

    [Fact]
    public async Task Move_DmNotEligible_NeverApplies()
    {
        Given(Quest(false));
        _userRepository.GetGroupRoleAsync(DmId, Bravo).Returns(GroupRole.Player);

        var outcome = await _sut.MoveAsync(QuestId, Bravo, "anything", ActorId, TestContext.Current.CancellationToken);

        outcome.Status.Should().Be(MoveStatus.DungeonMasterNotEligible);
        await _moveRepository.DidNotReceiveWithAnyArgs().ApplyAsync(default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Preview_QuestThatFollowsAnother_ClearsItsOriginalLink()
    {
        var quest = Quest(false);
        quest.OriginalQuestId = 12;
        quest.OriginalQuest = new Quest { Id = 12, Title = "Part One" };
        Given(quest);

        var preview = await PreviewAsync();

        preview.Plan!.ClearOriginalLink.Should().BeTrue();
        preview.OriginalQuestTitle.Should().Be("Part One");
        preview.Plan.FollowUpQuestIdsToUnlink.Should().BeEmpty();
    }

    [Fact]
    public async Task Preview_QuestWithAFollowUp_ListsTheFollowUp()
    {
        var quest = Quest(false);
        quest.FollowUpQuest = new Quest { Id = 13, Title = "Part Three" };
        Given(quest);

        var preview = await PreviewAsync();

        preview.Plan!.ClearOriginalLink.Should().BeFalse();
        preview.Plan.FollowUpQuestIdsToUnlink.Should().Equal(13);
        preview.FollowUpQuestTitle.Should().Be("Part Three");
    }

    [Fact]
    public async Task Preview_MiddleOfAChain_LosesBothLinks()
    {
        var quest = Quest(false);
        quest.OriginalQuestId = 12;
        quest.OriginalQuest = new Quest { Id = 12, Title = "Part One" };
        quest.FollowUpQuest = new Quest { Id = 13, Title = "Part Three" };
        Given(quest);

        var preview = await PreviewAsync();

        preview.Plan!.ClearOriginalLink.Should().BeTrue();
        preview.Plan.FollowUpQuestIdsToUnlink.Should().Equal(13);
    }

    [Fact]
    public async Task Preview_StampChangesWhenThePromotedSetOrTheUnlinkedSetChanges()
    {
        var gone = Signup(1, "Gone", selected: true);
        var yes = Signup(2, "Yes", vote: VoteType.Yes);
        Given(Quest(true, gone, yes), yes);
        var withPromotion = await PreviewAsync();

        var yesNo = Signup(2, "Yes", vote: VoteType.No);
        Given(Quest(true, Signup(1, "Gone", selected: true), yesNo), yesNo);
        var withoutPromotion = await PreviewAsync();

        var chained = Quest(true);
        chained.FollowUpQuest = new Quest { Id = 13, Title = "Next" };
        Given(chained);
        var withFollowUp = await PreviewAsync();
        Given(Quest(true));
        var withoutFollowUp = await PreviewAsync();

        withPromotion.Stamp.Should().NotBe(withoutPromotion.Stamp);
        withFollowUp.Stamp.Should().NotBe(withoutFollowUp.Stamp);
    }
}
