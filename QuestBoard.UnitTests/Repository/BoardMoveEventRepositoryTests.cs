using Microsoft.EntityFrameworkCore;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models.BoardMove;
using QuestBoard.Repository;
using QuestBoard.Repository.Entities;

namespace QuestBoard.UnitTests.Repository;

// Proves the single-save reconciliation of an event move against the in-memory store. Each fact
// seeds through one context and applies the plan through another, with the source board active,
// the way a request does.
public class BoardMoveEventRepositoryTests
{
    private const int Source = 1;
    private const int Target = 2;

    private static readonly DateTime AnsweredAt = new(2031, 1, 3, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime CreatedAt = new(2031, 1, 2, 8, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime CancelledOn = new(2031, 2, 1, 8, 0, 0, DateTimeKind.Utc);

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

    private sealed record Seeded(int EventId, int KeptAnswered, int KeptAutomatic, int Dropped);

    private static async Task<Seeded> SeedAsync(Store store)
    {
        await using var context = store.NewContext();
        context.Groups.AddRange(
            new GroupEntity { Id = Source, Name = "Alpha" },
            new GroupEntity { Id = Target, Name = "Bravo" });
        for (var id = 1; id <= 6; id++)
        {
            context.UserEntities.Add(new UserEntity { Id = id, Name = $"User {id}", Email = $"u{id}@test.com", UserName = $"u{id}" });
        }

        // Users 2 and 3 answered and are members of the target, as are 5 and 6, who have no row
        // yet. User 4 is not a member of the target.
        foreach (var userId in new[] { 2, 3, 5, 6 })
        {
            context.UserGroups.Add(new UserGroupEntity { UserId = userId, GroupId = Target, GroupRole = (int)GroupRole.Player });
        }

        await context.SaveChangesAsync(Token);

        var evt = new EventEntity
        {
            Title = "Harvest Fair",
            GroupId = Source,
            Date = new DateOnly(2031, 3, 10),
            StartTime = new TimeOnly(18, 30),
            CancelledAt = CancelledOn
        };
        context.Events.Add(evt);
        await context.SaveChangesAsync(Token);

        var keptAnswered = new EventSignupEntity
        {
            EventId = evt.Id, UserId = 2, Availability = (int)VoteType.Maybe, CreatedAt = CreatedAt, UpdatedAt = AnsweredAt
        };
        var keptAutomatic = new EventSignupEntity
        {
            EventId = evt.Id, UserId = 3, Availability = (int)VoteType.Yes, CreatedAt = CreatedAt, UpdatedAt = null
        };
        var dropped = new EventSignupEntity
        {
            EventId = evt.Id, UserId = 4, Availability = (int)VoteType.No, CreatedAt = CreatedAt, UpdatedAt = AnsweredAt
        };
        context.EventSignups.AddRange(keptAnswered, keptAutomatic, dropped);
        await context.SaveChangesAsync(Token);

        return new Seeded(evt.Id, keptAnswered.Id, keptAutomatic.Id, dropped.Id);
    }

    private static EventMovePlan FullPlan(Seeded s, params int[] campaignAdds) => new(
        s.EventId, Source, Target,
        KeptSignupIds: [s.KeptAnswered, s.KeptAutomatic],
        DroppedSignupIds: [s.Dropped],
        CampaignAddUserIds: campaignAdds,
        SignupBasis: Basis(s));

    private static EventSignupBasis[] Basis(Seeded s) =>
    [
        new(s.KeptAnswered, 2),
        new(s.KeptAutomatic, 3),
        new(s.Dropped, 4)
    ];

    private static async Task MutateAsync(Store store, Action<QuestBoardContext> change)
    {
        await using var context = store.NewContext();
        change(context);
        await context.SaveChangesAsync(Token);
    }

    private static async Task<MoveStatus> ApplyAsync(Store store, EventMovePlan plan, Action<QuestBoardContext>? onContext = null)
    {
        await using var context = store.NewContext();
        store.Groups.ActiveGroupId = Source;
        onContext?.Invoke(context);
        return await new EventMoveRepository(context).ApplyAsync(plan, Token);
    }

    private static async Task<EventEntity> ReadEventAsync(Store store, int id)
    {
        await using var context = store.NewContext();
        return await context.Events.IgnoreQueryFilters().AsNoTracking().SingleAsync(e => e.Id == id, Token);
    }

    private static async Task<List<EventSignupEntity>> ReadSignupsAsync(Store store, int eventId)
    {
        await using var context = store.NewContext();
        return await context.EventSignups.IgnoreQueryFilters().AsNoTracking()
            .Where(s => s.EventId == eventId).OrderBy(s => s.Id).ToListAsync(Token);
    }

    [Fact]
    public async Task Apply_KeptRows_KeepAvailabilityAndBothTimestampsExactly()
    {
        var store = new Store();
        var seeded = await SeedAsync(store);

        var status = await ApplyAsync(store, FullPlan(seeded));

        status.Should().Be(MoveStatus.Moved);
        var signups = await ReadSignupsAsync(store, seeded.EventId);
        var answered = signups.Single(s => s.Id == seeded.KeptAnswered);
        answered.Availability.Should().Be((int)VoteType.Maybe);
        answered.CreatedAt.Should().Be(CreatedAt);
        answered.UpdatedAt.Should().Be(AnsweredAt);

        var automatic = signups.Single(s => s.Id == seeded.KeptAutomatic);
        automatic.Availability.Should().Be((int)VoteType.Yes);
        automatic.CreatedAt.Should().Be(CreatedAt);
        automatic.UpdatedAt.Should().BeNull();
    }

    [Fact]
    public async Task Apply_DroppedRows_AreGone()
    {
        var store = new Store();
        var seeded = await SeedAsync(store);

        await ApplyAsync(store, FullPlan(seeded));

        var signups = await ReadSignupsAsync(store, seeded.EventId);
        signups.Select(s => s.Id).Should().BeEquivalentTo([seeded.KeptAnswered, seeded.KeptAutomatic]);
    }

    [Fact]
    public async Task Apply_CampaignAdds_GetAutomaticYesRowsWithNoAnsweredMarker()
    {
        var store = new Store();
        var seeded = await SeedAsync(store);

        await ApplyAsync(store, FullPlan(seeded, 5, 6));

        var signups = await ReadSignupsAsync(store, seeded.EventId);
        signups.Should().HaveCount(4);
        foreach (var userId in new[] { 5, 6 })
        {
            var row = signups.Single(s => s.UserId == userId);
            row.Availability.Should().Be((int)VoteType.Yes);
            row.UpdatedAt.Should().BeNull();
            row.CreatedAt.Should().NotBe(default);
        }
    }

    [Fact]
    public async Task Apply_DuplicatedCampaignAddId_WritesOneRow()
    {
        var store = new Store();
        var seeded = await SeedAsync(store);

        await ApplyAsync(store, FullPlan(seeded, 5, 5));

        (await ReadSignupsAsync(store, seeded.EventId)).Count(s => s.UserId == 5).Should().Be(1);
    }

    [Fact]
    public async Task Apply_MovesTheEventAndRaisesItsFeedRevisionByOne()
    {
        var store = new Store();
        var seeded = await SeedAsync(store);
        (await ReadEventAsync(store, seeded.EventId)).FeedRevision.Should().Be(1);

        await ApplyAsync(store, FullPlan(seeded));

        var evt = await ReadEventAsync(store, seeded.EventId);
        evt.GroupId.Should().Be(Target);
        evt.FeedRevision.Should().Be(2);
        evt.CancelledAt.Should().Be(CancelledOn);
        evt.SeriesId.Should().BeNull();
        evt.Date.Should().Be(new DateOnly(2031, 3, 10));
        evt.StartTime.Should().Be(new TimeOnly(18, 30));
    }

    [Fact]
    public async Task Apply_FiresExactlyOneSavedChangesEvent()
    {
        var store = new Store();
        var seeded = await SeedAsync(store);
        var saves = 0;

        await ApplyAsync(store, FullPlan(seeded, 5), context => context.SavedChanges += (_, _) => saves++);

        saves.Should().Be(1);
    }

    [Fact]
    public async Task Apply_AfterTheMove_OnlyTheTargetBoardSeesTheEvent()
    {
        var store = new Store();
        var seeded = await SeedAsync(store);

        await ApplyAsync(store, FullPlan(seeded));

        store.Groups.ActiveGroupId = Source;
        await using (var source = store.NewContext())
        {
            (await source.Events.AnyAsync(e => e.Id == seeded.EventId, Token)).Should().BeFalse();
        }

        store.Groups.ActiveGroupId = Target;
        await using var target = store.NewContext();
        (await target.Events.AnyAsync(e => e.Id == seeded.EventId, Token)).Should().BeTrue();
    }

    [Fact]
    public async Task Apply_PlanMissingALoadedRow_IsChangedAndTouchesNothing()
    {
        var store = new Store();
        var seeded = await SeedAsync(store);
        var stale = new EventMovePlan(seeded.EventId, Source, Target, [seeded.KeptAnswered], [seeded.Dropped], [], Basis(seeded));

        var status = await ApplyAsync(store, stale);

        status.Should().Be(MoveStatus.Changed);
        (await ReadEventAsync(store, seeded.EventId)).GroupId.Should().Be(Source);
        (await ReadSignupsAsync(store, seeded.EventId)).Should().HaveCount(3);
    }

    [Fact]
    public async Task Apply_PlanNamingARowThatIsGone_IsChanged()
    {
        var store = new Store();
        var seeded = await SeedAsync(store);
        var stale = new EventMovePlan(seeded.EventId, Source, Target,
            [seeded.KeptAnswered, seeded.KeptAutomatic, 9999], [seeded.Dropped], [], Basis(seeded));

        (await ApplyAsync(store, stale)).Should().Be(MoveStatus.Changed);
        (await ReadEventAsync(store, seeded.EventId)).GroupId.Should().Be(Source);
    }

    [Fact]
    public async Task Apply_AddForAUserWhoAlreadyHasARow_IsChangedAndTouchesNothing()
    {
        var store = new Store();
        var seeded = await SeedAsync(store);

        // User 2 already answered, so a second automatic row for them would break the unique pair.
        var status = await ApplyAsync(store, FullPlan(seeded, 2, 5));

        status.Should().Be(MoveStatus.Changed);
        (await ReadEventAsync(store, seeded.EventId)).GroupId.Should().Be(Source);
        (await ReadSignupsAsync(store, seeded.EventId)).Should().HaveCount(3);
    }

    [Fact]
    public async Task Apply_DroppedPersonWhoJoinedTheTargetSincePlanning_IsChangedAndTouchesNothing()
    {
        var store = new Store();
        var seeded = await SeedAsync(store);
        await MutateAsync(store, c => c.UserGroups.Add(new UserGroupEntity { UserId = 4, GroupId = Target, GroupRole = (int)GroupRole.Player }));

        var status = await ApplyAsync(store, FullPlan(seeded));

        status.Should().Be(MoveStatus.Changed);
        (await ReadEventAsync(store, seeded.EventId)).GroupId.Should().Be(Source);
        (await ReadSignupsAsync(store, seeded.EventId)).Should().HaveCount(3);
    }

    [Fact]
    public async Task Apply_KeptPersonWhoLeftTheTargetSincePlanning_IsChangedAndTouchesNothing()
    {
        var store = new Store();
        var seeded = await SeedAsync(store);
        await MutateAsync(store, c => c.UserGroups.RemoveRange(c.UserGroups.Where(ug => ug.UserId == 2 && ug.GroupId == Target)));

        var status = await ApplyAsync(store, FullPlan(seeded));

        status.Should().Be(MoveStatus.Changed);
        (await ReadEventAsync(store, seeded.EventId)).GroupId.Should().Be(Source);
        (await ReadSignupsAsync(store, seeded.EventId)).Should().HaveCount(3);
    }

    [Fact]
    public async Task Apply_AddForSomeoneWhoIsNotAMemberOfTheTarget_IsChangedAndTouchesNothing()
    {
        var store = new Store();
        var seeded = await SeedAsync(store);

        var status = await ApplyAsync(store, FullPlan(seeded, 1));

        status.Should().Be(MoveStatus.Changed);
        (await ReadEventAsync(store, seeded.EventId)).GroupId.Should().Be(Source);
        (await ReadSignupsAsync(store, seeded.EventId)).Should().HaveCount(3);
    }

    [Fact]
    public async Task Apply_RowThatNowBelongsToSomeoneElse_IsChangedAndTouchesNothing()
    {
        var store = new Store();
        var seeded = await SeedAsync(store);
        var plan = FullPlan(seeded) with { SignupBasis = [new(seeded.KeptAnswered, 5), new(seeded.KeptAutomatic, 3), new(seeded.Dropped, 4)] };

        var status = await ApplyAsync(store, plan);

        status.Should().Be(MoveStatus.Changed);
        (await ReadEventAsync(store, seeded.EventId)).GroupId.Should().Be(Source);
        (await ReadSignupsAsync(store, seeded.EventId)).Should().HaveCount(3);
    }

    [Fact]
    public async Task Apply_PlanWithoutABasisForARow_IsChangedAndTouchesNothing()
    {
        var store = new Store();
        var seeded = await SeedAsync(store);
        var plan = FullPlan(seeded) with { SignupBasis = [new(seeded.KeptAnswered, 2), new(seeded.KeptAutomatic, 3)] };

        (await ApplyAsync(store, plan)).Should().Be(MoveStatus.Changed);
        (await ReadEventAsync(store, seeded.EventId)).GroupId.Should().Be(Source);
    }

    [Fact]
    public async Task Apply_EventOnAnotherBoard_IsNotFound()
    {
        var store = new Store();
        var seeded = await SeedAsync(store);
        var plan = FullPlan(seeded) with { SourceGroupId = Target };

        // The filter shows the source board's events only, so a plan naming another source reads
        // as a mismatch.
        (await ApplyAsync(store, plan)).Should().Be(MoveStatus.NotFound);
        (await ReadEventAsync(store, seeded.EventId)).GroupId.Should().Be(Source);
    }

    [Fact]
    public async Task Apply_EventNotVisibleUnderTheActiveBoard_IsNotFound()
    {
        var store = new Store();
        var seeded = await SeedAsync(store);

        await using var context = store.NewContext();
        store.Groups.ActiveGroupId = 3;
        var status = await new EventMoveRepository(context).ApplyAsync(FullPlan(seeded), Token);

        status.Should().Be(MoveStatus.NotFound);
    }
}
