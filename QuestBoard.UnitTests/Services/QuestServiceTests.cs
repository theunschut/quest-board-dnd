using AutoMapper;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.Domain.Models.QuestBoard;
using QuestBoard.Domain.Services;
using NSubstitute;

namespace QuestBoard.UnitTests.Services;

public class QuestServiceTests
{
    private readonly IQuestRepository _repository;
    private readonly IPlayerSignupRepository _playerSignupRepository;
    private readonly IQuestEmailDispatcher _dispatcher;
    private readonly IMapper _mapper;
    private readonly QuestService _sut;

    public QuestServiceTests()
    {
        _repository = Substitute.For<IQuestRepository>();
        _playerSignupRepository = Substitute.For<IPlayerSignupRepository>();
        _dispatcher = Substitute.For<IQuestEmailDispatcher>();
        _mapper = Substitute.For<IMapper>();

        _sut = new QuestService(_repository, _playerSignupRepository, _dispatcher, _mapper);
    }

    // Helper: create a quest with specified signups
    private static Quest MakeQuest(int id, IList<PlayerSignup>? signups = null) =>
        new()
        {
            Id = id,
            Title = "Test Quest",
            Description = "A quest",
            DungeonMaster = new User { Id = 1, Name = "DM Dave", Email = "dm@example.com" },
            PlayerSignups = signups ?? [],
            ProposedDates = []
        };

    private static PlayerSignup MakeSignup(int id, string email, SignupRole role = SignupRole.Player, bool isSelected = true, bool emailConfirmed = true) =>
        new()
        {
            Id = id,
            Role = role,
            IsSelected = isSelected,
            Player = new User { Id = id + 10, Name = $"Player {id}", Email = email, EmailConfirmed = emailConfirmed },
            Quest = new Quest { Id = 1, Title = "T", Description = "D" }
        };

    // ---------------------------------------------------------------------------
    // FinalizeQuestAsync
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task FinalizeQuestAsync_WhenQuestReFetchReturnsNull_SendsNoEmails()
    {
        // Arrange
        _repository.FinalizeQuestAsync(Arg.Any<int>(), Arg.Any<DateTime>(), Arg.Any<IList<int>>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _repository.GetQuestWithDetailsAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns((Quest?)null);

        // Act
        await _sut.FinalizeQuestAsync(1, DateTime.UtcNow, [42], TestContext.Current.CancellationToken);

        // Assert: dispatcher is not called when quest re-fetch returns null
        _dispatcher.DidNotReceive().EnqueueFinalizedEmail(
            Arg.Any<int>(), Arg.Any<int>(), Arg.Any<DateTime>(), Arg.Any<string[]>(), Arg.Any<string[]>(),
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>());
    }

    [Fact]
    public async Task FinalizeQuestAsync_WithMixedSelectedAndSpectatorSignups_DispatchesJobWithBothEmails()
    {
        // Arrange: signup 1 is selected player, signup 2 is spectator (auto-included), signup 3 is unselected player
        var signups = new List<PlayerSignup>
        {
            MakeSignup(1, "player1@x.com", SignupRole.Player),
            MakeSignup(2, "spectator@x.com", SignupRole.Spectator),
            MakeSignup(3, "player3@x.com", SignupRole.Player) // NOT in selectedPlayerIds
        };
        var quest = MakeQuest(1, signups);

        _repository.FinalizeQuestAsync(Arg.Any<int>(), Arg.Any<DateTime>(), Arg.Any<IList<int>>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _repository.GetQuestWithDetailsAsync(1, Arg.Any<CancellationToken>())
            .Returns(quest);

        var selectedIds = new List<int> { 1 }; // Only signup id=1; spectator id=2 is auto-included

        // Act
        await _sut.FinalizeQuestAsync(1, DateTime.UtcNow, selectedIds, TestContext.Current.CancellationToken);

        // Assert: dispatcher receives one enqueue call with 2 emails (player1 + spectator), NOT player3
        _dispatcher.Received(1).EnqueueFinalizedEmail(
            1,
            Arg.Any<int>(),       // groupId — not checked in this test
            Arg.Any<DateTime>(),
            Arg.Is<string[]>(emails => emails.Contains("player1@x.com") && emails.Contains("spectator@x.com") && !emails.Contains("player3@x.com")),
            Arg.Any<string[]>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<int>());
    }

    // ---------------------------------------------------------------------------
    // UpdateQuestPropertiesWithNotificationsAsync
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task UpdateQuestPropertiesWithNotificationsAsync_WithNoAffectedPlayers_ReturnsOkZero()
    {
        // Arrange
        _repository.UpdateQuestPropertiesWithNotificationsAsync(
                Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(),
                Arg.Any<int>(), Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<IList<DateTime>?>(),
                Arg.Any<CancellationToken>())
            .Returns([]);

        // Act
        var result = await _sut.UpdateQuestPropertiesWithNotificationsAsync(1, "Title", "Desc", 5, 4, false, token: TestContext.Current.CancellationToken);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().Be(0);
        _dispatcher.DidNotReceive().EnqueueDateChangedEmail(
            Arg.Any<int>(), Arg.Any<string[]>(), Arg.Any<string[]>(),
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<DateTime>());
    }

    [Fact]
    public async Task UpdateQuestPropertiesWithNotificationsAsync_WithAffectedPlayers_DispatchesJobAndReturnsCount()
    {
        // Arrange
        var affectedPlayers = new List<User>
        {
            new() { Id = 1, Name = "Alice",   Email = "alice@x.com", EmailConfirmed = true  },
            new() { Id = 2, Name = "Bob",     Email = "bob@x.com",   EmailConfirmed = true  },
            new() { Id = 3, Name = "NoEmail", Email = "",            EmailConfirmed = true  } // should be skipped (empty email)
        };

        _repository.UpdateQuestPropertiesWithNotificationsAsync(
                Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(),
                Arg.Any<int>(), Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<IList<DateTime>?>(),
                Arg.Any<CancellationToken>())
            .Returns(affectedPlayers);

        var quest = MakeQuest(1);
        _repository.GetQuestWithDetailsAsync(1, Arg.Any<CancellationToken>())
            .Returns(quest);

        // Act
        var result = await _sut.UpdateQuestPropertiesWithNotificationsAsync(1, "Title", "Desc", 5, 4, false, token: TestContext.Current.CancellationToken);

        // Assert: only players with non-empty email are included in the dispatch
        result.Success.Should().BeTrue();
        result.Data.Should().Be(2, "only Alice and Bob have non-empty emails");

        _dispatcher.Received(1).EnqueueDateChangedEmail(
            1,
            Arg.Is<string[]>(emails => emails.Contains("alice@x.com") && emails.Contains("bob@x.com") && !emails.Contains("")),
            Arg.Is<string[]>(names => names.Contains("Alice") && names.Contains("Bob")),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<DateTime>(),
            Arg.Any<DateTime>());
    }

    // ---------------------------------------------------------------------------
    // CloseQuestAsync / ReopenQuestAsync
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task CloseQuestAsync_DelegatesToRepository_AndSendsNoEmail()
    {
        // Arrange
        _repository.CloseQuestAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        // Act
        await _sut.CloseQuestAsync(1, TestContext.Current.CancellationToken);

        // Assert
        await _repository.Received(1).CloseQuestAsync(1, Arg.Any<CancellationToken>());
        _dispatcher.DidNotReceiveWithAnyArgs().EnqueueFinalizedEmail(
            Arg.Any<int>(), Arg.Any<int>(), Arg.Any<DateTime>(), Arg.Any<string[]>(), Arg.Any<string[]>(),
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>());
    }

    [Fact]
    public async Task ReopenQuestAsync_DelegatesToRepository_AndSendsNoEmail()
    {
        // Arrange
        _repository.ReopenQuestAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        // Act
        await _sut.ReopenQuestAsync(1, TestContext.Current.CancellationToken);

        // Assert
        await _repository.Received(1).ReopenQuestAsync(1, Arg.Any<CancellationToken>());
        _dispatcher.DidNotReceiveWithAnyArgs().EnqueueFinalizedEmail(
            Arg.Any<int>(), Arg.Any<int>(), Arg.Any<DateTime>(), Arg.Any<string[]>(), Arg.Any<string[]>(),
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>());
    }

    // ---------------------------------------------------------------------------
    // GetCompletedQuestsAsync
    // ---------------------------------------------------------------------------

    private static Quest MakeCompletedQuestCandidate(
        int id, bool isFinalized, DateTime? finalizedDate, bool isClosed, DateTime? closedDate, bool dungeonMasterSession = false) =>
        new()
        {
            Id = id,
            Title = $"Quest {id}",
            Description = "A quest",
            IsFinalized = isFinalized,
            FinalizedDate = finalizedDate,
            IsClosed = isClosed,
            ClosedDate = closedDate,
            DungeonMasterSession = dungeonMasterSession,
            PlayerSignups = [],
            ProposedDates = []
        };

    [Fact]
    public async Task GetCompletedQuestsAsync_IncludesClosedCampaignQuest_WithNoNextDayWait()
    {
        // Arrange: closed today, never finalized — must still appear immediately (no next-day wait)
        var closedToday = MakeCompletedQuestCandidate(1, isFinalized: false, finalizedDate: null, isClosed: true, closedDate: DateTime.UtcNow);

        _repository.GetQuestsWithDetailsAsync(Arg.Any<CancellationToken>())
            .Returns((IList<Quest>)[closedToday]);

        // Act
        var result = await _sut.GetCompletedQuestsAsync(TestContext.Current.CancellationToken);

        // Assert
        result.Should().ContainSingle(q => q.Id == 1);
    }

    [Fact]
    public async Task GetCompletedQuestsAsync_PreservesOneShotNextDayWait()
    {
        // Arrange: one-shot finalized yesterday (included), one-shot finalized today (excluded — next-day wait)
        var finalizedYesterday = MakeCompletedQuestCandidate(1, isFinalized: true, finalizedDate: DateTime.UtcNow.AddDays(-2), isClosed: false, closedDate: null);
        var finalizedToday = MakeCompletedQuestCandidate(2, isFinalized: true, finalizedDate: DateTime.UtcNow, isClosed: false, closedDate: null);

        _repository.GetQuestsWithDetailsAsync(Arg.Any<CancellationToken>())
            .Returns((IList<Quest>)[finalizedYesterday, finalizedToday]);

        // Act
        var result = await _sut.GetCompletedQuestsAsync(TestContext.Current.CancellationToken);

        // Assert
        result.Should().ContainSingle(q => q.Id == 1);
        result.Should().NotContain(q => q.Id == 2);
    }

    [Fact]
    public async Task GetCompletedQuestsAsync_OrdersClosedAndFinalizedQuestsTogether_ClosedNotSortedAsNull()
    {
        // Arrange: an older one-shot finalized quest and a just-closed campaign quest (ClosedDate=now, FinalizedDate=null)
        // The closed quest must sort by ClosedDate, not fall to the bottom as a null FinalizedDate.
        var olderFinalized = MakeCompletedQuestCandidate(1, isFinalized: true, finalizedDate: DateTime.UtcNow.AddDays(-5), isClosed: false, closedDate: null);
        var justClosed = MakeCompletedQuestCandidate(2, isFinalized: false, finalizedDate: null, isClosed: true, closedDate: DateTime.UtcNow);

        _repository.GetQuestsWithDetailsAsync(Arg.Any<CancellationToken>())
            .Returns((IList<Quest>)[olderFinalized, justClosed]);

        // Act
        var result = await _sut.GetCompletedQuestsAsync(TestContext.Current.CancellationToken);

        // Assert: newest first — justClosed (ClosedDate=now) before olderFinalized (FinalizedDate=5 days ago)
        result.Should().HaveCount(2);
        result[0].Id.Should().Be(2);
        result[1].Id.Should().Be(1);
    }

    // ---------------------------------------------------------------------------
    // ChangeVoteAsync / RevokeSignupAsync — waitlist auto-promotion
    // ---------------------------------------------------------------------------

    private static readonly DateTime FinalizedDate = new(2026, 7, 10);

    private static Quest MakeFinalizedQuest(int id, IList<PlayerSignup> signups, int totalPlayerCount, int finalizedProposedDateId = 100) =>
        new()
        {
            Id = id,
            Title = "Test Quest",
            Description = "A quest",
            DungeonMaster = new User { Id = 1, Name = "DM Dave", Email = "dm@example.com" },
            IsFinalized = true,
            FinalizedDate = FinalizedDate,
            TotalPlayerCount = totalPlayerCount,
            GroupId = 1,
            ChallengeRating = 3,
            PlayerSignups = signups,
            ProposedDates = [new ProposedDate { Id = finalizedProposedDateId, Date = FinalizedDate }]
        };

    [Fact]
    public async Task ChangeVoteAsync_SelectedPlayerVotesNo_PromotesTopWaitlistedCandidate()
    {
        // Arrange: a seat just freed (voter's ChangeVoteAsync returns true), and a top waitlisted
        // candidate is available with a confirmed email.
        var candidate = MakeSignup(2, "candidate@x.com", emailConfirmed: true);
        var quest = MakeFinalizedQuest(1, [MakeSignup(1, "voter@x.com"), candidate], totalPlayerCount: 4);

        _playerSignupRepository.ChangeVoteAsync(1, 100, VoteType.No, Arg.Any<CancellationToken>())
            .Returns(true);
        _repository.GetQuestWithDetailsAsync(1, Arg.Any<CancellationToken>())
            .Returns(quest);
        _playerSignupRepository.GetTopWaitlistedCandidateAsync(1, 100, Arg.Any<CancellationToken>())
            .Returns(candidate);

        // Act
        await _sut.ChangeVoteAsync(1, 1, VoteType.No, 100, TestContext.Current.CancellationToken);

        // Assert
        candidate.IsSelected.Should().BeTrue();
        await _playerSignupRepository.Received(1).UpdateAsync(candidate, Arg.Any<CancellationToken>());
        _dispatcher.Received(1).EnqueueWaitlistPromotedEmail(
            Arg.Any<int>(), Arg.Any<int>(), Arg.Any<DateTime>(),
            candidate.Player.Email!, candidate.Player.Name,
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>());
    }

    [Fact]
    public async Task ChangeVoteAsync_SelectedPlayerVotesMaybe_DoesNotPromote()
    {
        // Arrange: a Maybe vote never frees a seat — the repository's ChangeVoteAsync returns false.
        _playerSignupRepository.ChangeVoteAsync(1, 100, VoteType.Maybe, Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        await _sut.ChangeVoteAsync(1, 1, VoteType.Maybe, 100, TestContext.Current.CancellationToken);

        // Assert
        _dispatcher.DidNotReceive().EnqueueWaitlistPromotedEmail(
            Arg.Any<int>(), Arg.Any<int>(), Arg.Any<DateTime>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>());
    }

    [Fact]
    public async Task ChangeVoteAsync_WaitlistedPlayerVotesMaybe_SelectsWhenSeatAvailable()
    {
        // Arrange: a waitlisted (not-yet-selected) player votes Maybe while a seat is free —
        // a Maybe vote must be able to fill an open seat the same way a Yes vote does.
        var voterSignup = MakeSignup(1, "voter@x.com", isSelected: false);
        var quest = MakeFinalizedQuest(1, [voterSignup], totalPlayerCount: 4);

        _playerSignupRepository.ChangeVoteAsync(1, 100, VoteType.Maybe, Arg.Any<CancellationToken>())
            .Returns(false);
        _repository.GetQuestWithDetailsAsync(1, Arg.Any<CancellationToken>())
            .Returns(quest);

        // Act
        await _sut.ChangeVoteAsync(1, 1, VoteType.Maybe, 100, TestContext.Current.CancellationToken);

        // Assert
        voterSignup.IsSelected.Should().BeTrue();
        await _playerSignupRepository.Received(1).UpdateAsync(voterSignup, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ChangeVoteAsync_SelectedSpectatorVotesNo_DoesNotPromote()
    {
        // Arrange: a selected Spectator votes No. Spectator seats are not part of
        // TotalPlayerCount, so the repository correctly reports no seat freed (false), and
        // QuestService must not attempt a promotion off that signal.
        _playerSignupRepository.ChangeVoteAsync(1, 100, VoteType.No, Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        await _sut.ChangeVoteAsync(1, 1, VoteType.No, 100, TestContext.Current.CancellationToken);

        // Assert
        await _playerSignupRepository.DidNotReceive().UpdateAsync(Arg.Any<PlayerSignup>(), Arg.Any<CancellationToken>());
        _dispatcher.DidNotReceive().EnqueueWaitlistPromotedEmail(
            Arg.Any<int>(), Arg.Any<int>(), Arg.Any<DateTime>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>());
    }

    [Fact]
    public async Task ChangeVoteAsync_WaitlistedPlayerVotesNo_StaysWaitlistedEvenWithSeatAvailable()
    {
        // Arrange: a waitlisted player votes No while a seat is free — a No vote must never
        // grant a seat, regardless of available capacity.
        var voterSignup = MakeSignup(1, "voter@x.com", isSelected: false);
        var quest = MakeFinalizedQuest(1, [voterSignup], totalPlayerCount: 4);

        _playerSignupRepository.ChangeVoteAsync(1, 100, VoteType.No, Arg.Any<CancellationToken>())
            .Returns(false);
        _repository.GetQuestWithDetailsAsync(1, Arg.Any<CancellationToken>())
            .Returns(quest);

        // Act
        await _sut.ChangeVoteAsync(1, 1, VoteType.No, 100, TestContext.Current.CancellationToken);

        // Assert
        voterSignup.IsSelected.Should().BeFalse();
        await _playerSignupRepository.DidNotReceive().UpdateAsync(Arg.Any<PlayerSignup>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RevokeSignupAsync_WhenRevokedSignupWasWaitlisted_DoesNotPromote()
    {
        // Arrange: the signup being revoked is waitlisted (IsSelected=false) — no seat was freed.
        var waitlisted = MakeSignup(1, "waitlisted@x.com", isSelected: false);
        var quest = MakeFinalizedQuest(1, [waitlisted], totalPlayerCount: 4);

        _repository.GetQuestWithDetailsAsync(1, Arg.Any<CancellationToken>())
            .Returns(quest);

        // Act
        await _sut.RevokeSignupAsync(1, 1, TestContext.Current.CancellationToken);

        // Assert
        await _playerSignupRepository.Received(1).RemoveAsync(waitlisted, Arg.Any<CancellationToken>());
        _dispatcher.DidNotReceive().EnqueueWaitlistPromotedEmail(
            Arg.Any<int>(), Arg.Any<int>(), Arg.Any<DateTime>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>());
    }

    [Fact]
    public async Task RevokeSignupAsync_WhenRevokedSignupWasSelectedAssistantDM_DoesNotPromote()
    {
        // Arrange: the revoked signup is a selected Assistant DM, not a Player — Assistant DM
        // seats are not part of TotalPlayerCount, so no Player seat was actually freed.
        var revoked = MakeSignup(1, "assistantdm@x.com", role: SignupRole.AssistantDM, isSelected: true);
        var waitlistedPlayer = MakeSignup(2, "waitlisted@x.com", isSelected: false);
        var quest = MakeFinalizedQuest(1, [revoked, waitlistedPlayer], totalPlayerCount: 4);

        _repository.GetQuestWithDetailsAsync(1, Arg.Any<CancellationToken>())
            .Returns(quest);

        // Act
        await _sut.RevokeSignupAsync(1, 1, TestContext.Current.CancellationToken);

        // Assert: no promotion attempted for the unrelated waitlisted Player
        await _playerSignupRepository.Received(1).RemoveAsync(revoked, Arg.Any<CancellationToken>());
        await _playerSignupRepository.DidNotReceive().UpdateAsync(Arg.Any<PlayerSignup>(), Arg.Any<CancellationToken>());
        _dispatcher.DidNotReceive().EnqueueWaitlistPromotedEmail(
            Arg.Any<int>(), Arg.Any<int>(), Arg.Any<DateTime>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>());
    }

    [Fact]
    public async Task RevokeSignupAsync_WhenRevokedSignupWasSelected_Promotes()
    {
        // Arrange: the signup being revoked was selected — a seat frees and a candidate is promoted.
        var revoked = MakeSignup(1, "revoked@x.com", isSelected: true);
        var candidate = MakeSignup(2, "candidate@x.com", emailConfirmed: true, isSelected: false);
        var quest = MakeFinalizedQuest(1, [revoked, candidate], totalPlayerCount: 4);

        _repository.GetQuestWithDetailsAsync(1, Arg.Any<CancellationToken>())
            .Returns(quest);
        _playerSignupRepository.GetTopWaitlistedCandidateAsync(1, 100, Arg.Any<CancellationToken>())
            .Returns(candidate);

        // Act
        await _sut.RevokeSignupAsync(1, 1, TestContext.Current.CancellationToken);

        // Assert
        candidate.IsSelected.Should().BeTrue();
        _dispatcher.Received(1).EnqueueWaitlistPromotedEmail(
            Arg.Any<int>(), Arg.Any<int>(), Arg.Any<DateTime>(),
            candidate.Player.Email!, candidate.Player.Name,
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>());
    }

    [Fact]
    public async Task PromoteNextWaitlisted_NeverEmailsTheFreeingPlayer()
    {
        // Arrange: the top waitlisted candidate happens to be the same signup that just freed
        // the seat (edge case) — promotion must be skipped entirely, no email sent.
        var freeingSignup = MakeSignup(1, "freeing@x.com");
        var quest = MakeFinalizedQuest(1, [freeingSignup], totalPlayerCount: 4);

        _playerSignupRepository.ChangeVoteAsync(1, 100, VoteType.No, Arg.Any<CancellationToken>())
            .Returns(true);
        _repository.GetQuestWithDetailsAsync(1, Arg.Any<CancellationToken>())
            .Returns(quest);
        _playerSignupRepository.GetTopWaitlistedCandidateAsync(1, 100, Arg.Any<CancellationToken>())
            .Returns(freeingSignup); // top candidate id == freeing signup id

        // Act
        await _sut.ChangeVoteAsync(1, 1, VoteType.No, 100, TestContext.Current.CancellationToken);

        // Assert: no promotion, no email — the freeing player is never the recipient
        await _playerSignupRepository.DidNotReceive().UpdateAsync(Arg.Any<PlayerSignup>(), Arg.Any<CancellationToken>());
        _dispatcher.DidNotReceive().EnqueueWaitlistPromotedEmail(
            Arg.Any<int>(), Arg.Any<int>(), Arg.Any<DateTime>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>());

        // Separately, with a distinct candidate, the promoted email must be the candidate's — never the freeing player's
        var distinctCandidate = MakeSignup(2, "distinctcandidate@x.com", emailConfirmed: true);
        var quest2 = MakeFinalizedQuest(1, [freeingSignup, distinctCandidate], totalPlayerCount: 4);
        _repository.GetQuestWithDetailsAsync(1, Arg.Any<CancellationToken>()).Returns(quest2);
        _playerSignupRepository.GetTopWaitlistedCandidateAsync(1, 100, Arg.Any<CancellationToken>())
            .Returns(distinctCandidate);

        await _sut.ChangeVoteAsync(1, 1, VoteType.No, 100, TestContext.Current.CancellationToken);

        _dispatcher.Received(1).EnqueueWaitlistPromotedEmail(
            Arg.Any<int>(), Arg.Any<int>(), Arg.Any<DateTime>(),
            distinctCandidate.Player.Email!, distinctCandidate.Player.Name,
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>());
        _dispatcher.DidNotReceive().EnqueueWaitlistPromotedEmail(
            Arg.Any<int>(), Arg.Any<int>(), Arg.Any<DateTime>(),
            freeingSignup.Player.Email!, Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>());
    }
}
