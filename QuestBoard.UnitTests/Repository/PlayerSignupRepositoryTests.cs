using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Repository;
using QuestBoard.Repository.Entities;

namespace QuestBoard.UnitTests.Repository;

public class PlayerSignupRepositoryTests
{
    private static QuestBoardContext CreateContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<QuestBoardContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        return new QuestBoardContext(options, new TestActiveGroupContext());
    }

    private static QuestBoardContext CreateContext(string databaseName, IActiveGroupContext activeGroupContext)
    {
        var options = new DbContextOptionsBuilder<QuestBoardContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        return new QuestBoardContext(options, activeGroupContext);
    }

    private static IMapper CreateMapper()
    {
        var configuration = new MapperConfiguration(cfg => cfg.AddProfile<QuestBoard.Repository.Automapper.EntityProfile>(), NullLoggerFactory.Instance);
        return configuration.CreateMapper();
    }

    // Seeds the minimal Group/User/Quest graph a PlayerSignupEntity needs so AutoMapper can
    // populate the domain model's required Player/Quest navigation properties.
    private static async Task SeedQuestAndUserAsync(QuestBoardContext context, int questId, int playerId, int groupId = 1)
    {
        if (!await context.Groups.AnyAsync(g => g.Id == groupId))
        {
            context.Groups.Add(new GroupEntity { Id = groupId, Name = $"Test Group {groupId}" });
        }

        if (!await context.UserEntities.AnyAsync(u => u.Id == playerId))
        {
            context.UserEntities.Add(new UserEntity { Id = playerId, Name = $"Player {playerId}", Email = $"player{playerId}@test.com" });
        }

        if (!await context.Quests.AnyAsync(q => q.Id == questId))
        {
            context.Quests.Add(new QuestEntity
            {
                Id = questId,
                Title = "Test Quest",
                Description = "A quest",
                DungeonMasterId = playerId,
                GroupId = groupId
            });
        }

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static PlayerSignupEntity MakeSignupEntity(int id, int questId, bool isSelected, SignupRole role = SignupRole.Player, DateTime? signupTime = null)
    {
        return new PlayerSignupEntity
        {
            Id = id,
            QuestId = questId,
            PlayerId = id + 100,
            IsSelected = isSelected,
            SignupRole = (int)role,
            SignupTime = signupTime ?? DateTime.UtcNow,
            DateVotes = []
        };
    }

    // -------------------------------------------------------------------
    // ChangeVoteAsync
    // -------------------------------------------------------------------

    [Fact]
    public async Task ChangeVoteAsync_WithVoteYes_PersistsVoteAsTwo()
    {
        // Arrange
        await using var context = CreateContext(nameof(ChangeVoteAsync_WithVoteYes_PersistsVoteAsTwo));
        await SeedQuestAndUserAsync(context, questId: 1, playerId: 101);
        context.Add(new ProposedDateEntity { Id = 5, QuestId = 1, Date = DateTime.UtcNow });
        var signup = MakeSignupEntity(1, questId: 1, isSelected: false);
        context.PlayerSignups.Add(signup);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var repository = new PlayerSignupRepository(context, CreateMapper());

        // Act
        await repository.ChangeVoteAsync(1, proposedDateId: 5, VoteType.Yes, TestContext.Current.CancellationToken);

        // Assert: the persisted int must be 2 (VoteType.Yes), never 0 (the pre-existing bug)
        var persisted = await context.Set<PlayerDateVoteEntity>().FirstAsync(dv => dv.PlayerSignupId == 1, TestContext.Current.CancellationToken);
        persisted.Vote.Should().Be(2);
    }

    [Fact]
    public async Task ChangeVoteAsync_AnyVote_SetsLastVoteChangeTimeToRecentNonNullValue()
    {
        // Arrange
        await using var context = CreateContext(nameof(ChangeVoteAsync_AnyVote_SetsLastVoteChangeTimeToRecentNonNullValue));
        await SeedQuestAndUserAsync(context, questId: 1, playerId: 101);
        var signup = MakeSignupEntity(1, questId: 1, isSelected: false);
        context.PlayerSignups.Add(signup);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var repository = new PlayerSignupRepository(context, CreateMapper());
        var before = DateTime.UtcNow;

        // Act
        await repository.ChangeVoteAsync(1, proposedDateId: 5, VoteType.Maybe, TestContext.Current.CancellationToken);

        // Assert
        var persisted = await context.PlayerSignups.FirstAsync(ps => ps.Id == 1, TestContext.Current.CancellationToken);
        persisted.LastVoteChangeTime.Should().NotBeNull();
        persisted.LastVoteChangeTime.Should().BeOnOrAfter(before);
    }

    [Fact]
    public async Task ChangeVoteAsync_SelectedSignupVotesMaybe_KeepsIsSelectedTrueAndReturnsFalse()
    {
        // Arrange
        await using var context = CreateContext(nameof(ChangeVoteAsync_SelectedSignupVotesMaybe_KeepsIsSelectedTrueAndReturnsFalse));
        await SeedQuestAndUserAsync(context, questId: 1, playerId: 101);
        var signup = MakeSignupEntity(1, questId: 1, isSelected: true);
        context.PlayerSignups.Add(signup);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var repository = new PlayerSignupRepository(context, CreateMapper());

        // Act
        var seatFreed = await repository.ChangeVoteAsync(1, proposedDateId: 5, VoteType.Maybe, TestContext.Current.CancellationToken);

        // Assert: VOTE-05 — Maybe keeps the seat, no promotion signal
        seatFreed.Should().BeFalse();
        var persisted = await context.PlayerSignups.FirstAsync(ps => ps.Id == 1, TestContext.Current.CancellationToken);
        persisted.IsSelected.Should().BeTrue();
    }

    [Fact]
    public async Task ChangeVoteAsync_WaitlistedSignupVotesNo_KeepsIsSelectedFalseAndReturnsFalse()
    {
        // Arrange
        await using var context = CreateContext(nameof(ChangeVoteAsync_WaitlistedSignupVotesNo_KeepsIsSelectedFalseAndReturnsFalse));
        await SeedQuestAndUserAsync(context, questId: 1, playerId: 101);
        var signup = MakeSignupEntity(1, questId: 1, isSelected: false);
        context.PlayerSignups.Add(signup);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var repository = new PlayerSignupRepository(context, CreateMapper());

        // Act
        var seatFreed = await repository.ChangeVoteAsync(1, proposedDateId: 5, VoteType.No, TestContext.Current.CancellationToken);

        // Assert: VOTE-06 — waitlisted signup voting No stays on the waitlist, no seat freed
        seatFreed.Should().BeFalse();
        var persisted = await context.PlayerSignups.FirstAsync(ps => ps.Id == 1, TestContext.Current.CancellationToken);
        persisted.IsSelected.Should().BeFalse();
    }

    [Fact]
    public async Task ChangeVoteAsync_SelectedSignupVotesNo_SetsIsSelectedFalseAndReturnsTrue()
    {
        // Arrange
        await using var context = CreateContext(nameof(ChangeVoteAsync_SelectedSignupVotesNo_SetsIsSelectedFalseAndReturnsTrue));
        await SeedQuestAndUserAsync(context, questId: 1, playerId: 101);
        var signup = MakeSignupEntity(1, questId: 1, isSelected: true);
        context.PlayerSignups.Add(signup);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var repository = new PlayerSignupRepository(context, CreateMapper());

        // Act
        var seatFreed = await repository.ChangeVoteAsync(1, proposedDateId: 5, VoteType.No, TestContext.Current.CancellationToken);

        // Assert: VOTE-04 — selected signup voting No frees the seat and signals promotion
        seatFreed.Should().BeTrue();
        var persisted = await context.PlayerSignups.FirstAsync(ps => ps.Id == 1, TestContext.Current.CancellationToken);
        persisted.IsSelected.Should().BeFalse();
    }

    [Fact]
    public async Task ChangeVoteAsync_SelectedAssistantDMVotesNo_SetsIsSelectedFalseButReturnsFalse()
    {
        // Arrange: a selected Assistant DM signup votes No. Assistant DM seats are not part of
        // TotalPlayerCount, so the seat-freed signal must not fire even though IsSelected still
        // clears for the signup itself.
        await using var context = CreateContext(nameof(ChangeVoteAsync_SelectedAssistantDMVotesNo_SetsIsSelectedFalseButReturnsFalse));
        await SeedQuestAndUserAsync(context, questId: 1, playerId: 101);
        var signup = MakeSignupEntity(1, questId: 1, isSelected: true, role: SignupRole.AssistantDM);
        context.PlayerSignups.Add(signup);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var repository = new PlayerSignupRepository(context, CreateMapper());

        // Act
        var seatFreed = await repository.ChangeVoteAsync(1, proposedDateId: 5, VoteType.No, TestContext.Current.CancellationToken);

        // Assert: no promotion signal for a non-Player role, even though the signup itself unselects
        seatFreed.Should().BeFalse();
        var persisted = await context.PlayerSignups.FirstAsync(ps => ps.Id == 1, TestContext.Current.CancellationToken);
        persisted.IsSelected.Should().BeFalse();
    }

    [Fact]
    public async Task ChangeVoteAsync_SelectedSpectatorVotesNo_SetsIsSelectedFalseButReturnsFalse()
    {
        // Arrange: a selected Spectator signup votes No. Spectator seats are not part of
        // TotalPlayerCount, so the seat-freed signal must not fire.
        await using var context = CreateContext(nameof(ChangeVoteAsync_SelectedSpectatorVotesNo_SetsIsSelectedFalseButReturnsFalse));
        await SeedQuestAndUserAsync(context, questId: 1, playerId: 101);
        var signup = MakeSignupEntity(1, questId: 1, isSelected: true, role: SignupRole.Spectator);
        context.PlayerSignups.Add(signup);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var repository = new PlayerSignupRepository(context, CreateMapper());

        // Act
        var seatFreed = await repository.ChangeVoteAsync(1, proposedDateId: 5, VoteType.No, TestContext.Current.CancellationToken);

        // Assert: no promotion signal for a non-Player role, even though the signup itself unselects
        seatFreed.Should().BeFalse();
        var persisted = await context.PlayerSignups.FirstAsync(ps => ps.Id == 1, TestContext.Current.CancellationToken);
        persisted.IsSelected.Should().BeFalse();
    }

    // -------------------------------------------------------------------
    // GetTopWaitlistedCandidateAsync
    // -------------------------------------------------------------------

    [Fact]
    public async Task GetTopWaitlistedCandidateAsync_OrdersByVotePriorityThenTimestamp()
    {
        // Arrange: three waitlisted signups voting No/Maybe/Yes for the finalized date
        await using var context = CreateContext(nameof(GetTopWaitlistedCandidateAsync_OrdersByVotePriorityThenTimestamp));
        await SeedQuestAndUserAsync(context, questId: 1, playerId: 101);
        await SeedQuestAndUserAsync(context, questId: 1, playerId: 102);
        await SeedQuestAndUserAsync(context, questId: 1, playerId: 103);

        var noVoter = MakeSignupEntity(1, questId: 1, isSelected: false);
        noVoter.DateVotes.Add(new PlayerDateVoteEntity { ProposedDateId = 5, PlayerSignupId = 1, Vote = (int)VoteType.No });

        var maybeVoter = MakeSignupEntity(2, questId: 1, isSelected: false);
        maybeVoter.DateVotes.Add(new PlayerDateVoteEntity { ProposedDateId = 5, PlayerSignupId = 2, Vote = (int)VoteType.Maybe });

        var yesVoter = MakeSignupEntity(3, questId: 1, isSelected: false);
        yesVoter.DateVotes.Add(new PlayerDateVoteEntity { ProposedDateId = 5, PlayerSignupId = 3, Vote = (int)VoteType.Yes });

        context.PlayerSignups.AddRange(noVoter, maybeVoter, yesVoter);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var repository = new PlayerSignupRepository(context, CreateMapper());

        // Act
        var candidate = await repository.GetTopWaitlistedCandidateAsync(1, finalizedProposedDateId: 5, TestContext.Current.CancellationToken);

        // Assert: the Yes voter outranks Maybe and No
        candidate.Should().NotBeNull();
        candidate!.Id.Should().Be(3);
    }

    [Fact]
    public async Task GetTopWaitlistedCandidateAsync_SameVote_OrdersByLastVoteChangeTimeFallingBackToSignupTime()
    {
        // Arrange: two Yes voters — one changed vote recently, one signed up earlier and never changed vote
        await using var context = CreateContext(nameof(GetTopWaitlistedCandidateAsync_SameVote_OrdersByLastVoteChangeTimeFallingBackToSignupTime));
        await SeedQuestAndUserAsync(context, questId: 1, playerId: 101);
        await SeedQuestAndUserAsync(context, questId: 1, playerId: 102);

        var earlySignup = MakeSignupEntity(1, questId: 1, isSelected: false, signupTime: DateTime.UtcNow.AddDays(-2));
        earlySignup.LastVoteChangeTime = null; // never changed vote since signup
        earlySignup.DateVotes.Add(new PlayerDateVoteEntity { ProposedDateId = 5, PlayerSignupId = 1, Vote = (int)VoteType.Yes });

        var lateChanger = MakeSignupEntity(2, questId: 1, isSelected: false, signupTime: DateTime.UtcNow.AddDays(-1));
        lateChanger.LastVoteChangeTime = DateTime.UtcNow; // changed vote just now
        lateChanger.DateVotes.Add(new PlayerDateVoteEntity { ProposedDateId = 5, PlayerSignupId = 2, Vote = (int)VoteType.Yes });

        context.PlayerSignups.AddRange(earlySignup, lateChanger);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var repository = new PlayerSignupRepository(context, CreateMapper());

        // Act
        var candidate = await repository.GetTopWaitlistedCandidateAsync(1, finalizedProposedDateId: 5, TestContext.Current.CancellationToken);

        // Assert: earlySignup's effective ordering timestamp (SignupTime, 2 days ago) is earlier
        // than lateChanger's (LastVoteChangeTime, just now) — earlySignup wins the tiebreak
        candidate.Should().NotBeNull();
        candidate!.Id.Should().Be(1);
    }

    [Fact]
    public async Task GetTopWaitlistedCandidateAsync_NewJoinerFromJoinFinalizedQuest_OrdersCorrectlyAmongExistingWaitlist()
    {
        // Arrange: a pre-existing waitlisted Yes-voter (signed up 2 days ago, never changed vote)
        // vs. a brand-new JoinFinalizedQuest-created joiner (signed up just now, also Yes, never changed vote)
        await using var context = CreateContext(nameof(GetTopWaitlistedCandidateAsync_NewJoinerFromJoinFinalizedQuest_OrdersCorrectlyAmongExistingWaitlist));
        await SeedQuestAndUserAsync(context, questId: 1, playerId: 101);
        await SeedQuestAndUserAsync(context, questId: 1, playerId: 102);

        var existingWaitlisted = MakeSignupEntity(1, questId: 1, isSelected: false, signupTime: DateTime.UtcNow.AddDays(-2));
        existingWaitlisted.LastVoteChangeTime = null;
        existingWaitlisted.DateVotes.Add(new PlayerDateVoteEntity { ProposedDateId = 5, PlayerSignupId = 1, Vote = (int)VoteType.Yes });

        // Shape of a signup JoinFinalizedQuest creates when waitlisted: IsSelected = false,
        // LastVoteChangeTime never set, SignupTime = entity default (DateTime.UtcNow at creation)
        var newJoiner = MakeSignupEntity(2, questId: 1, isSelected: false, signupTime: DateTime.UtcNow);
        newJoiner.LastVoteChangeTime = null;
        newJoiner.DateVotes.Add(new PlayerDateVoteEntity { ProposedDateId = 5, PlayerSignupId = 2, Vote = (int)VoteType.Yes });

        context.PlayerSignups.AddRange(existingWaitlisted, newJoiner);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var repository = new PlayerSignupRepository(context, CreateMapper());

        // Act
        var candidate = await repository.GetTopWaitlistedCandidateAsync(1, finalizedProposedDateId: 5, TestContext.Current.CancellationToken);

        // Assert: existingWaitlisted (earlier SignupTime) wins the same-vote tiebreak — the new
        // joiner participates correctly in the existing ordering, no special-casing needed
        candidate.Should().NotBeNull();
        candidate!.Id.Should().Be(1);
    }

    [Fact]
    public async Task GetTopWaitlistedCandidateAsync_NoWaitlistedPlayers_ReturnsNull()
    {
        // Arrange
        await using var context = CreateContext(nameof(GetTopWaitlistedCandidateAsync_NoWaitlistedPlayers_ReturnsNull));
        var selectedSignup = MakeSignupEntity(1, questId: 1, isSelected: true);
        context.PlayerSignups.Add(selectedSignup);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var repository = new PlayerSignupRepository(context, CreateMapper());

        // Act
        var candidate = await repository.GetTopWaitlistedCandidateAsync(1, finalizedProposedDateId: 5, TestContext.Current.CancellationToken);

        // Assert
        candidate.Should().BeNull();
    }

    // -------------------------------------------------------------------
    // GetByIdWithQuestAsync (cross-group regression coverage — RemovePlayerSignup's lookup)
    // -------------------------------------------------------------------

    [Fact]
    public async Task GetByIdWithQuestAsync_ForSignupOnOtherGroupsQuest_ReturnsNullWhenActiveGroupDiffers()
    {
        // Arrange: seed the group-2 quest/signup via a null-active-group context (sees all),
        // then re-read through a context whose active group is 1.
        var seedGroupContext = new MutableTestGroupContext { ActiveGroupId = null };
        await using (var seedContext = CreateContext(nameof(GetByIdWithQuestAsync_ForSignupOnOtherGroupsQuest_ReturnsNullWhenActiveGroupDiffers), seedGroupContext))
        {
            await SeedQuestAndUserAsync(seedContext, questId: 2, playerId: 201, groupId: 2);
            seedContext.PlayerSignups.Add(MakeSignupEntity(1, questId: 2, isSelected: false));
            await seedContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var activeGroupContext = new MutableTestGroupContext { ActiveGroupId = 1 };
        await using var context = CreateContext(nameof(GetByIdWithQuestAsync_ForSignupOnOtherGroupsQuest_ReturnsNullWhenActiveGroupDiffers), activeGroupContext);
        var repository = new PlayerSignupRepository(context, CreateMapper());

        // Act
        var result = await repository.GetByIdWithQuestAsync(1, TestContext.Current.CancellationToken);

        // Assert: the required Quest Include folds the Quest global query filter into the join,
        // so a signup on an out-of-group quest is invisible through this lookup
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdWithQuestAsync_ForSignupInActiveGroup_ReturnsSignupWithQuestGroupId()
    {
        // Arrange
        var seedGroupContext = new MutableTestGroupContext { ActiveGroupId = null };
        await using (var seedContext = CreateContext(nameof(GetByIdWithQuestAsync_ForSignupInActiveGroup_ReturnsSignupWithQuestGroupId), seedGroupContext))
        {
            await SeedQuestAndUserAsync(seedContext, questId: 1, playerId: 101, groupId: 1);
            seedContext.PlayerSignups.Add(MakeSignupEntity(1, questId: 1, isSelected: false));
            await seedContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var activeGroupContext = new MutableTestGroupContext { ActiveGroupId = 1 };
        await using var context = CreateContext(nameof(GetByIdWithQuestAsync_ForSignupInActiveGroup_ReturnsSignupWithQuestGroupId), activeGroupContext);
        var repository = new PlayerSignupRepository(context, CreateMapper());

        // Act
        var result = await repository.GetByIdWithQuestAsync(1, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result!.Quest.GroupId.Should().Be(1);
    }

    [Fact]
    public async Task GetByIdAsync_ForSignupOnOtherGroupsQuest_ReturnsNullViaPlayerSignupQueryFilter()
    {
        // Arrange: seed the group-2 quest/signup via a null-active-group context (sees all).
        var seedGroupContext = new MutableTestGroupContext { ActiveGroupId = null };
        await using (var seedContext = CreateContext(nameof(GetByIdAsync_ForSignupOnOtherGroupsQuest_ReturnsNullViaPlayerSignupQueryFilter), seedGroupContext))
        {
            await SeedQuestAndUserAsync(seedContext, questId: 2, playerId: 201, groupId: 2);
            seedContext.PlayerSignups.Add(MakeSignupEntity(1, questId: 2, isSelected: false));
            await seedContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // PlayerSignupEntity now carries its own HasQueryFilter (scoped through the required Quest
        // navigation), so even the base GetByIdAsync (BaseRepository's DbSet.FindAsync, which has no
        // explicit Include on Quest) is automatically group-scoped — EF Core applies global query
        // filters regardless of which navigations a specific query includes. Callers no longer need
        // to pre-validate the target via a filtered quest.PlayerSignups navigation for this to be safe.
        var activeGroupContext = new MutableTestGroupContext { ActiveGroupId = 1 };
        await using var context = CreateContext(nameof(GetByIdAsync_ForSignupOnOtherGroupsQuest_ReturnsNullViaPlayerSignupQueryFilter), activeGroupContext);
        var repository = new PlayerSignupRepository(context, CreateMapper());

        // Act
        var result = await repository.GetByIdAsync(1, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeNull();
    }

    // Tests using the single-arg CreateContext(databaseName) overload seed and query through the
    // same context instance, so ActiveGroupId must be a concrete value the group-scoped filters
    // let through — matching SeedQuestAndUserAsync's own default groupId of 1 — rather than null,
    // which now yields zero rows fail-closed instead of every row fail-open.
    private sealed class TestActiveGroupContext : IActiveGroupContext
    {
        public int? ActiveGroupId => 1;
    }

    private sealed class MutableTestGroupContext : IActiveGroupContext
    {
        public int? ActiveGroupId { get; set; }
    }
}
