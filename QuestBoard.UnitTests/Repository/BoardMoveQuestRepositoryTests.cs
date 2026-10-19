using Microsoft.EntityFrameworkCore;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models.BoardMove;
using QuestBoard.Repository;
using QuestBoard.Repository.Entities;

namespace QuestBoard.UnitTests.Repository;

// Proves the single-save reconciliation of a quest move against the in-memory store. Each fact
// seeds through one context and applies the plan through another, with the source board active,
// the way a request does.
public class BoardMoveQuestRepositoryTests
{
    private const int Source = 1;
    private const int Target = 2;

    private static readonly DateTime FinalizedFor = new(2031, 3, 10, 19, 0, 0);
    private static readonly DateTime SignedUpAt = new(2031, 1, 2, 8, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime VoteChangedAt = new(2031, 1, 3, 9, 0, 0, DateTimeKind.Utc);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private sealed class MutableGroupContext : IActiveGroupContext
    {
        public int? ActiveGroupId { get; set; } = Source;
    }

    private sealed class Store
    {
        private readonly DbContextOptions<QuestBoardContext> options = new DbContextOptionsBuilder<QuestBoardContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        public MutableGroupContext Groups { get; } = new();

        public QuestBoardContext NewContext() => new(options, Groups);
    }

    private sealed record Seeded(
        int QuestId,
        int KeptSignup,
        int DroppedSelected,
        int DroppedSpectator,
        int WaitlistedSignup,
        int FollowUpId,
        int OriginalId,
        int ProposedDateId);

    private static async Task<Seeded> SeedAsync(Store store, bool withChain = true)
    {
        await using var context = store.NewContext();
        context.Groups.AddRange(
            new GroupEntity { Id = Source, Name = "Alpha" },
            new GroupEntity { Id = Target, Name = "Bravo" });
        for (var id = 1; id <= 6; id++)
        {
            context.UserEntities.Add(new UserEntity { Id = id, Name = $"User {id}", Email = $"u{id}@test.com", UserName = $"u{id}" });
        }

        await context.SaveChangesAsync(Token);

        QuestEntity? original = null;
        if (withChain)
        {
            original = new QuestEntity { Title = "Original", Description = "d", GroupId = Source, DungeonMasterId = 1 };
            context.Quests.Add(original);
            await context.SaveChangesAsync(Token);
        }

        var quest = new QuestEntity
        {
            Title = "Moved", Description = "d", GroupId = Source, DungeonMasterId = 1,
            IsFinalized = true, FinalizedDate = FinalizedFor,
            OriginalQuestId = original?.Id,
            ProposedDates = [new ProposedDateEntity { Date = FinalizedFor }]
        };
        context.Quests.Add(quest);
        await context.SaveChangesAsync(Token);

        QuestEntity? followUp = null;
        if (withChain)
        {
            followUp = new QuestEntity { Title = "Follow", Description = "d", GroupId = Source, DungeonMasterId = 1, OriginalQuestId = quest.Id };
            context.Quests.Add(followUp);
        }

        var pd = quest.ProposedDates.Single();
        var character = new CharacterEntity { Name = "Hero", OwnerId = 2, GroupId = Source };
        context.Characters.Add(character);
        await context.SaveChangesAsync(Token);

        PlayerSignupEntity Signup(int playerId, SignupRole role, bool selected, int? characterId, VoteType vote) => new()
        {
            QuestId = quest.Id, PlayerId = playerId, SignupRole = (int)role, IsSelected = selected,
            CharacterId = characterId, SignupTime = SignedUpAt, LastVoteChangeTime = VoteChangedAt,
            DateVotes = [new PlayerDateVoteEntity { ProposedDateId = pd.Id, Vote = (int)vote }]
        };

        var kept = Signup(2, SignupRole.Player, true, character.Id, VoteType.Yes);
        var droppedSelected = Signup(3, SignupRole.Player, true, null, VoteType.Yes);
        var droppedSpectator = Signup(4, SignupRole.Spectator, false, null, VoteType.Maybe);
        var waitlisted = Signup(5, SignupRole.Player, false, null, VoteType.Yes);
        context.PlayerSignups.AddRange(kept, droppedSelected, droppedSpectator, waitlisted);
        context.ReminderLogs.Add(new ReminderLogEntity { QuestId = quest.Id, PlayerId = 2 });
        await context.SaveChangesAsync(Token);

        return new Seeded(quest.Id, kept.Id, droppedSelected.Id, droppedSpectator.Id, waitlisted.Id,
            followUp?.Id ?? 0, original?.Id ?? 0, pd.Id);
    }

    private static QuestMovePlan FullPlan(Seeded s, bool withChain = true) => new(
        s.QuestId, Source, Target,
        KeptSignupIds: [s.KeptSignup, s.WaitlistedSignup],
        DroppedSignupIds: [s.DroppedSelected, s.DroppedSpectator],
        PromotedSignupIds: [s.WaitlistedSignup],
        ClearOriginalLink: withChain,
        FollowUpQuestIdsToUnlink: withChain ? [s.FollowUpId] : []);

    private static async Task<MoveStatus> ApplyAsync(Store store, QuestMovePlan plan, Action<QuestBoardContext>? onSaved = null)
    {
        await using var context = store.NewContext();
        store.Groups.ActiveGroupId = Source;
        onSaved?.Invoke(context);
        return await new QuestMoveRepository(context).ApplyAsync(plan, Token);
    }

    private static async Task<QuestEntity> ReadQuestAsync(Store store, int id)
    {
        await using var context = store.NewContext();
        return await context.Quests.IgnoreQueryFilters().AsNoTracking().SingleAsync(q => q.Id == id, Token);
    }

    private static async Task<List<PlayerSignupEntity>> ReadSignupsAsync(Store store, int questId)
    {
        await using var context = store.NewContext();
        return await context.PlayerSignups.IgnoreQueryFilters().AsNoTracking()
            .Include(ps => ps.DateVotes).Where(ps => ps.QuestId == questId).OrderBy(ps => ps.Id).ToListAsync(Token);
    }

    private static async Task<int> CountVotesAsync(Store store)
    {
        await using var context = store.NewContext();
        return await context.Set<PlayerDateVoteEntity>().IgnoreQueryFilters().CountAsync(Token);
    }

    [Fact]
    public async Task Apply_KeptSignup_KeepsEverythingButTheCharacter()
    {
        var store = new Store();
        var seeded = await SeedAsync(store);

        var status = await ApplyAsync(store, FullPlan(seeded));

        status.Should().Be(MoveStatus.Moved);
        var signups = await ReadSignupsAsync(store, seeded.QuestId);
        var kept = signups.Single(s => s.Id == seeded.KeptSignup);
        kept.SignupRole.Should().Be((int)SignupRole.Player);
        kept.IsSelected.Should().BeTrue();
        kept.SignupTime.Should().Be(SignedUpAt);
        kept.LastVoteChangeTime.Should().Be(VoteChangedAt);
        kept.CharacterId.Should().BeNull();
        kept.DateVotes.Should().ContainSingle().Which.Vote.Should().Be((int)VoteType.Yes);
    }

    [Fact]
    public async Task Apply_DroppedSignupsOfEveryRole_AreRemovedWithTheirVotes()
    {
        var store = new Store();
        var seeded = await SeedAsync(store);

        await ApplyAsync(store, FullPlan(seeded));

        var signups = await ReadSignupsAsync(store, seeded.QuestId);
        signups.Select(s => s.Id).Should().BeEquivalentTo([seeded.KeptSignup, seeded.WaitlistedSignup]);
        (await CountVotesAsync(store)).Should().Be(2);
    }

    [Fact]
    public async Task Apply_PromotedSignup_BecomesSelectedAndNothingElseOnItChanges()
    {
        var store = new Store();
        var seeded = await SeedAsync(store);

        await ApplyAsync(store, FullPlan(seeded));

        var promoted = (await ReadSignupsAsync(store, seeded.QuestId)).Single(s => s.Id == seeded.WaitlistedSignup);
        promoted.IsSelected.Should().BeTrue();
        promoted.SignupTime.Should().Be(SignedUpAt);
        promoted.LastVoteChangeTime.Should().Be(VoteChangedAt);
        promoted.SignupRole.Should().Be((int)SignupRole.Player);
    }

    [Fact]
    public async Task Apply_MiddleOfChain_LosesBothLinks()
    {
        var store = new Store();
        var seeded = await SeedAsync(store);

        await ApplyAsync(store, FullPlan(seeded));

        (await ReadQuestAsync(store, seeded.QuestId)).OriginalQuestId.Should().BeNull();
        (await ReadQuestAsync(store, seeded.FollowUpId)).OriginalQuestId.Should().BeNull();
    }

    [Fact]
    public async Task Apply_FollowUpWhoseOnlyChangeIsItsLink_KeepsItsFeedRevision()
    {
        var store = new Store();
        var seeded = await SeedAsync(store);

        await ApplyAsync(store, FullPlan(seeded));

        (await ReadQuestAsync(store, seeded.FollowUpId)).FeedRevision.Should().Be(1);
        (await ReadQuestAsync(store, seeded.QuestId)).FeedRevision.Should().Be(2);
    }

    [Fact]
    public async Task Apply_ProposedDatesAndReminderLogs_StayAttachedToTheQuest()
    {
        var store = new Store();
        var seeded = await SeedAsync(store);

        await ApplyAsync(store, FullPlan(seeded));

        await using var context = store.NewContext();
        (await context.Set<ProposedDateEntity>().IgnoreQueryFilters()
            .CountAsync(p => p.QuestId == seeded.QuestId, Token)).Should().Be(1);
        (await context.ReminderLogs.IgnoreQueryFilters()
            .CountAsync(r => r.QuestId == seeded.QuestId, Token)).Should().Be(1);
    }

    [Fact]
    public async Task Apply_FiresExactlyOneSavedChangesEvent()
    {
        var store = new Store();
        var seeded = await SeedAsync(store);
        var saves = 0;

        await ApplyAsync(store, FullPlan(seeded), context => context.SavedChanges += (_, _) => saves++);

        saves.Should().Be(1);
    }

    [Fact]
    public async Task Apply_AfterTheMove_OnlyTheTargetBoardSeesTheQuest()
    {
        var store = new Store();
        var seeded = await SeedAsync(store);

        await ApplyAsync(store, FullPlan(seeded));

        store.Groups.ActiveGroupId = Source;
        await using (var source = store.NewContext())
        {
            (await source.Quests.AnyAsync(q => q.Id == seeded.QuestId, Token)).Should().BeFalse();
        }

        store.Groups.ActiveGroupId = Target;
        await using var target = store.NewContext();
        var moved = await target.Quests.Include(q => q.PlayerSignups).SingleAsync(q => q.Id == seeded.QuestId, Token);
        moved.PlayerSignups.Select(s => s.Id).Should().BeEquivalentTo([seeded.KeptSignup, seeded.WaitlistedSignup]);
    }

    [Fact]
    public async Task Apply_OpenQuestWithoutChain_MovesWithNothingElseCut()
    {
        var store = new Store();
        var seeded = await SeedAsync(store, withChain: false);
        var plan = FullPlan(seeded, withChain: false) with { PromotedSignupIds = [] };

        var status = await ApplyAsync(store, plan);

        status.Should().Be(MoveStatus.Moved);
        (await ReadQuestAsync(store, seeded.QuestId)).GroupId.Should().Be(Target);
    }

    public static IEnumerable<object[]> MismatchedPlans() =>
    [
        [(Func<QuestMovePlan, QuestMovePlan>)(p => p with { KeptSignupIds = [.. p.KeptSignupIds, 9999] })],
        [(Func<QuestMovePlan, QuestMovePlan>)(p => p with { KeptSignupIds = [p.KeptSignupIds[0]], PromotedSignupIds = [] })],
        [(Func<QuestMovePlan, QuestMovePlan>)(p => p with { FollowUpQuestIdsToUnlink = [] })],
        [(Func<QuestMovePlan, QuestMovePlan>)(p => p with { FollowUpQuestIdsToUnlink = [.. p.FollowUpQuestIdsToUnlink, 9999] })],
        [(Func<QuestMovePlan, QuestMovePlan>)(p => p with { ClearOriginalLink = false })],
        [(Func<QuestMovePlan, QuestMovePlan>)(p => p with { PromotedSignupIds = [.. p.DroppedSignupIds] })],
    ];

    [Theory]
    [MemberData(nameof(MismatchedPlans))]
    public async Task Apply_PlanThatNoLongerMatches_ReturnsChangedAndLeavesEveryRowUntouched(Func<QuestMovePlan, QuestMovePlan> mismatch)
    {
        var store = new Store();
        var seeded = await SeedAsync(store);
        var before = await ReadQuestAsync(store, seeded.QuestId);
        var signupsBefore = await ReadSignupsAsync(store, seeded.QuestId);
        var votesBefore = await CountVotesAsync(store);

        var status = await ApplyAsync(store, mismatch(FullPlan(seeded)));

        status.Should().Be(MoveStatus.Changed);
        var after = await ReadQuestAsync(store, seeded.QuestId);
        after.GroupId.Should().Be(Source);
        after.FeedRevision.Should().Be(before.FeedRevision);
        after.OriginalQuestId.Should().Be(seeded.OriginalId);
        (await ReadQuestAsync(store, seeded.FollowUpId)).OriginalQuestId.Should().Be(seeded.QuestId);
        var signupsAfter = await ReadSignupsAsync(store, seeded.QuestId);
        signupsAfter.Should().HaveCount(signupsBefore.Count);
        signupsAfter.Select(s => (s.Id, s.IsSelected, s.CharacterId))
            .Should().Equal(signupsBefore.Select(s => (s.Id, s.IsSelected, s.CharacterId)));
        (await CountVotesAsync(store)).Should().Be(votesBefore);
    }
}
