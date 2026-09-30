using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.Repository;
using QuestBoard.Repository.Entities;
using QuestBoard.UnitTests.Helpers;

namespace QuestBoard.UnitTests.Repository;

// Proves the store-side revision rules field by field. Every fact seeds through one context and
// mutates through a second, so the original values the stamper compares against come from the
// store the way production reads them, and every instant is fixed so nothing reads the real clock.
public class FeedRevisionStamperTests
{
    private static readonly DateTime SeededAt = new(2026, 1, 5, 9, 30, 0, DateTimeKind.Utc);
    private static readonly DateTime ClockNow = new(2026, 9, 30, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime FinalizedFor = new(2026, 10, 10, 19, 0, 0);

    private const int BoardId = 1;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    // A clock the test can move between saves.
    private sealed class SettableTimeProvider(DateTime now) : TimeProvider
    {
        public DateTime Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => new(DateTime.SpecifyKind(Now, DateTimeKind.Utc));
    }

    private sealed class MutableGroupContext : IActiveGroupContext
    {
        public int? ActiveGroupId { get; set; } = BoardId;
    }

    // One in-memory store shared by every context a fact creates.
    private sealed class Store
    {
        private readonly DbContextOptions<QuestBoardContext> options = new DbContextOptionsBuilder<QuestBoardContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        private readonly MutableGroupContext groups = new();

        public SettableTimeProvider Clock { get; } = new(ClockNow);

        public QuestBoardContext NewContext() => new(options, groups, Clock);
    }

    private static IMapper CreateMapper() =>
        new MapperConfiguration(cfg => cfg.AddProfile<QuestBoard.Repository.Automapper.EntityProfile>(), NullLoggerFactory.Instance)
            .CreateMapper();

    // ---- Seeding and reading ----

    private static async Task<int> SeedEventAsync(Store store, Action<EventEntity>? adjust = null)
    {
        await using var context = store.NewContext();
        var entity = new EventEntity
        {
            Title = "Original",
            Date = new DateOnly(2026, 10, 1),
            StartTime = new TimeOnly(19, 0),
            GroupId = BoardId,
            CreatedAt = SeededAt,
        };
        adjust?.Invoke(entity);
        context.Events.Add(entity);
        await context.SaveChangesAsync(Token);
        return entity.Id;
    }

    private static async Task<int> SeedQuestAsync(Store store, bool finalized = true, Action<QuestEntity>? adjust = null)
    {
        await using var context = store.NewContext();
        var entity = new QuestEntity
        {
            Title = "Original",
            Description = "Original description",
            GroupId = BoardId,
            DungeonMasterId = 1,
            CreatedAt = SeededAt,
            IsFinalized = finalized,
            FinalizedDate = finalized ? FinalizedFor : null,
        };
        adjust?.Invoke(entity);
        context.Quests.Add(entity);
        await context.SaveChangesAsync(Token);
        return entity.Id;
    }

    private static async Task<EventEntity> ReadEventAsync(Store store, int id)
    {
        await using var context = store.NewContext();
        return await context.Events.IgnoreQueryFilters().AsNoTracking().SingleAsync(e => e.Id == id, Token);
    }

    private static async Task<QuestEntity> ReadQuestAsync(Store store, int id)
    {
        await using var context = store.NewContext();
        return await context.Quests.IgnoreQueryFilters().AsNoTracking().SingleAsync(q => q.Id == id, Token);
    }

    // Loads the event in a fresh context, applies the change, saves, and returns.
    private static async Task ChangeEventAsync(Store store, int id, Action<EventEntity> change)
    {
        await using var context = store.NewContext();
        var entity = await context.Events.IgnoreQueryFilters().SingleAsync(e => e.Id == id, Token);
        change(entity);
        await context.SaveChangesAsync(Token);
    }

    private static async Task ChangeQuestAsync(Store store, int id, Action<QuestEntity> change)
    {
        await using var context = store.NewContext();
        var entity = await context.Quests.IgnoreQueryFilters().SingleAsync(q => q.Id == id, Token);
        change(entity);
        await context.SaveChangesAsync(Token);
    }

    // ---- New rows ----

    [Fact]
    public async Task NewEvent_SavedWithNoRevisionValues_IsStoredAtRevisionOneStampedWithItsCreationTime()
    {
        var store = new Store();

        var id = await SeedEventAsync(store);

        var stored = await ReadEventAsync(store, id);
        stored.FeedRevision.Should().Be(1);
        stored.FeedRevisedAt.Should().Be(SeededAt);
    }

    [Fact]
    public async Task NewQuest_SavedWithNoRevisionValues_IsStoredAtRevisionOneStampedWithItsCreationTime()
    {
        var store = new Store();

        var id = await SeedQuestAsync(store);

        var stored = await ReadQuestAsync(store, id);
        stored.FeedRevision.Should().Be(1);
        stored.FeedRevisedAt.Should().Be(SeededAt);
    }

    [Fact]
    public async Task NewEvent_SavedWithARevisionBelowOne_IsStoredAtRevisionOne()
    {
        var store = new Store();

        var id = await SeedEventAsync(store, e => e.FeedRevision = 0);

        (await ReadEventAsync(store, id)).FeedRevision.Should().Be(1);
    }

    // ---- Feed-visible fields raise the revision by exactly one ----

    private static void ChangeEventFeedField(EventEntity entity, string field)
    {
        switch (field)
        {
            case nameof(EventEntity.Title): entity.Title = "Renamed"; break;
            case nameof(EventEntity.Date): entity.Date = entity.Date.AddDays(2); break;
            case nameof(EventEntity.StartTime): entity.StartTime = new TimeOnly(20, 30); break;
            case nameof(EventEntity.CancelledAt): entity.CancelledAt = ClockNow; break;
            case nameof(EventEntity.GroupId): entity.GroupId = 2; break;
            default: throw new ArgumentOutOfRangeException(nameof(field), field, null);
        }
    }

    [Theory]
    [InlineData(nameof(EventEntity.Title))]
    [InlineData(nameof(EventEntity.Date))]
    [InlineData(nameof(EventEntity.StartTime))]
    [InlineData(nameof(EventEntity.CancelledAt))]
    [InlineData(nameof(EventEntity.GroupId))]
    public async Task Event_ChangingAFeedVisibleField_RaisesTheRevisionByOneAndStampsTheClock(string field)
    {
        var store = new Store();
        var id = await SeedEventAsync(store);

        await ChangeEventAsync(store, id, e => ChangeEventFeedField(e, field));

        var stored = await ReadEventAsync(store, id);
        stored.FeedRevision.Should().Be(2);
        stored.FeedRevisedAt.Should().Be(ClockNow);
    }

    [Fact]
    public async Task Event_ClearingTheCancellation_RaisesTheRevisionAgain()
    {
        var store = new Store();
        var id = await SeedEventAsync(store);
        await ChangeEventAsync(store, id, e => e.CancelledAt = ClockNow);

        await ChangeEventAsync(store, id, e => e.CancelledAt = null);

        (await ReadEventAsync(store, id)).FeedRevision.Should().Be(3);
    }

    private static void ChangeQuestFeedField(QuestEntity entity, string field)
    {
        switch (field)
        {
            case nameof(QuestEntity.Title): entity.Title = "Renamed"; break;
            case nameof(QuestEntity.FinalizedDate): entity.FinalizedDate = FinalizedFor.AddDays(2); break;
            case nameof(QuestEntity.IsFinalized): entity.IsFinalized = true; entity.FinalizedDate = FinalizedFor; break;
            case nameof(QuestEntity.GroupId): entity.GroupId = 2; break;
            default: throw new ArgumentOutOfRangeException(nameof(field), field, null);
        }
    }

    [Theory]
    [InlineData(nameof(QuestEntity.Title), true)]
    [InlineData(nameof(QuestEntity.FinalizedDate), true)]
    [InlineData(nameof(QuestEntity.IsFinalized), false)]
    [InlineData(nameof(QuestEntity.GroupId), true)]
    public async Task Quest_ChangingAFeedVisibleField_RaisesTheRevisionByOneAndStampsTheClock(string field, bool seedFinalized)
    {
        var store = new Store();
        var id = await SeedQuestAsync(store, seedFinalized);

        await ChangeQuestAsync(store, id, q => ChangeQuestFeedField(q, field));

        var stored = await ReadQuestAsync(store, id);
        stored.FeedRevision.Should().Be(2);
        stored.FeedRevisedAt.Should().Be(ClockNow);
    }

    [Fact]
    public async Task Quest_Unfinalizing_RaisesTheRevisionByOne()
    {
        var store = new Store();
        var id = await SeedQuestAsync(store);

        await ChangeQuestAsync(store, id, q =>
        {
            q.IsFinalized = false;
            q.FinalizedDate = null;
        });

        (await ReadQuestAsync(store, id)).FeedRevision.Should().Be(2);
    }

    [Fact]
    public async Task Event_ChangingTwoFeedFieldsInOneSave_RaisesTheRevisionByExactlyOne()
    {
        var store = new Store();
        var id = await SeedEventAsync(store);

        await ChangeEventAsync(store, id, e =>
        {
            e.Title = "Renamed";
            e.Date = e.Date.AddDays(1);
        });

        (await ReadEventAsync(store, id)).FeedRevision.Should().Be(2);
    }

    [Fact]
    public async Task Event_ChangingFeedFieldsInTwoSeparateSaves_RaisesTheRevisionByTwo()
    {
        var store = new Store();
        var id = await SeedEventAsync(store);

        await ChangeEventAsync(store, id, e => e.Title = "Renamed");
        await ChangeEventAsync(store, id, e => e.Date = e.Date.AddDays(1));

        (await ReadEventAsync(store, id)).FeedRevision.Should().Be(3);
    }

    // ---- Fields the feed does not show leave the revision alone ----

    private static void ChangeEventNonFeedField(EventEntity entity, string field)
    {
        switch (field)
        {
            case nameof(EventEntity.Description): entity.Description = "A new description"; break;
            case nameof(EventEntity.SeriesId): entity.SeriesId = 5; break;
            case nameof(EventEntity.SeriesSlotIndex): entity.SeriesSlotIndex = 3; break;
            default: throw new ArgumentOutOfRangeException(nameof(field), field, null);
        }
    }

    [Theory]
    [InlineData(nameof(EventEntity.Description))]
    [InlineData(nameof(EventEntity.SeriesId))]
    [InlineData(nameof(EventEntity.SeriesSlotIndex))]
    public async Task Event_ChangingOnlyAFieldTheFeedDoesNotShow_LeavesTheRevisionAndStampUntouched(string field)
    {
        var store = new Store();
        var id = await SeedEventAsync(store);

        await ChangeEventAsync(store, id, e => ChangeEventNonFeedField(e, field));

        var stored = await ReadEventAsync(store, id);
        stored.FeedRevision.Should().Be(1);
        stored.FeedRevisedAt.Should().Be(SeededAt);
    }

    private static void ChangeQuestNonFeedField(QuestEntity entity, string field)
    {
        switch (field)
        {
            case nameof(QuestEntity.Description): entity.Description = "A new description"; break;
            case nameof(QuestEntity.Recap): entity.Recap = "What happened"; break;
            case nameof(QuestEntity.Rewards): entity.Rewards = "A sword"; break;
            case nameof(QuestEntity.ChallengeRating): entity.ChallengeRating = 9; break;
            case nameof(QuestEntity.FinalizedEmailSentForDate): entity.FinalizedEmailSentForDate = FinalizedFor; break;
            case nameof(QuestEntity.IsClosed): entity.IsClosed = true; entity.ClosedDate = ClockNow; break;
            default: throw new ArgumentOutOfRangeException(nameof(field), field, null);
        }
    }

    [Theory]
    [InlineData(nameof(QuestEntity.Description))]
    [InlineData(nameof(QuestEntity.Recap))]
    [InlineData(nameof(QuestEntity.Rewards))]
    [InlineData(nameof(QuestEntity.ChallengeRating))]
    [InlineData(nameof(QuestEntity.FinalizedEmailSentForDate))]
    [InlineData(nameof(QuestEntity.IsClosed))]
    public async Task Quest_ChangingOnlyAFieldTheFeedDoesNotShow_LeavesTheRevisionAndStampUntouched(string field)
    {
        var store = new Store();
        var id = await SeedQuestAsync(store);

        await ChangeQuestAsync(store, id, q => ChangeQuestNonFeedField(q, field));

        var stored = await ReadQuestAsync(store, id);
        stored.FeedRevision.Should().Be(1);
        stored.FeedRevisedAt.Should().Be(SeededAt);
    }

    [Fact]
    public async Task AssigningAFeedFieldItsCurrentValue_LeavesTheRevisionAndStampUntouched()
    {
        var store = new Store();
        var eventId = await SeedEventAsync(store);
        var questId = await SeedQuestAsync(store);

        await ChangeEventAsync(store, eventId, e =>
        {
            e.Title = e.Title;
            e.Date = new DateOnly(2026, 10, 1);
            e.StartTime = new TimeOnly(19, 0);
        });
        await ChangeQuestAsync(store, questId, q =>
        {
            q.Title = q.Title;
            q.FinalizedDate = FinalizedFor;
        });

        var storedEvent = await ReadEventAsync(store, eventId);
        var storedQuest = await ReadQuestAsync(store, questId);
        storedEvent.FeedRevision.Should().Be(1);
        storedEvent.FeedRevisedAt.Should().Be(SeededAt);
        storedQuest.FeedRevision.Should().Be(1);
        storedQuest.FeedRevisedAt.Should().Be(SeededAt);
    }

    // ---- The store owns the revision ----

    [Fact]
    public async Task WritingTheRevisionDirectlyOnATrackedEntity_IsUndoneOnSave()
    {
        var store = new Store();
        var eventId = await SeedEventAsync(store);
        var questId = await SeedQuestAsync(store);

        await ChangeEventAsync(store, eventId, e =>
        {
            e.FeedRevision = 99;
            e.FeedRevisedAt = new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        });
        await ChangeQuestAsync(store, questId, q =>
        {
            q.FeedRevision = 99;
            q.FeedRevisedAt = new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        });

        var storedEvent = await ReadEventAsync(store, eventId);
        var storedQuest = await ReadQuestAsync(store, questId);
        storedEvent.FeedRevision.Should().Be(1);
        storedEvent.FeedRevisedAt.Should().Be(SeededAt);
        storedQuest.FeedRevision.Should().Be(1);
        storedQuest.FeedRevisedAt.Should().Be(SeededAt);
    }

    [Fact]
    public async Task TrackedEntity_AfterASaveThatUndidADirectRevisionWrite_ReportsTheStoredValues()
    {
        var store = new Store();
        var id = await SeedEventAsync(store);

        await using var context = store.NewContext();
        var tracked = await context.Events.IgnoreQueryFilters().SingleAsync(e => e.Id == id, Token);
        tracked.FeedRevision = 99;
        tracked.FeedRevisedAt = new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        await context.SaveChangesAsync(Token);

        tracked.FeedRevision.Should().Be(1);
        tracked.FeedRevisedAt.Should().Be(SeededAt);
    }

    [Fact]
    public async Task WritingTheRevisionDirectlyAlongsideAFeedChange_StoresTheStoredValuePlusOne()
    {
        var store = new Store();
        var eventId = await SeedEventAsync(store);
        var questId = await SeedQuestAsync(store);

        await ChangeEventAsync(store, eventId, e =>
        {
            e.FeedRevision = 99;
            e.FeedRevisedAt = new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            e.Title = "Renamed";
        });
        await ChangeQuestAsync(store, questId, q =>
        {
            q.FeedRevision = 99;
            q.FeedRevisedAt = new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            q.Title = "Renamed";
        });

        var storedEvent = await ReadEventAsync(store, eventId);
        var storedQuest = await ReadQuestAsync(store, questId);
        storedEvent.FeedRevision.Should().Be(2);
        storedEvent.FeedRevisedAt.Should().Be(ClockNow);
        storedQuest.FeedRevision.Should().Be(2);
        storedQuest.FeedRevisedAt.Should().Be(ClockNow);
    }

    [Fact]
    public async Task EventRepositoryUpdate_WithADomainModelCarryingNoRevision_StoresTheStoredValuePlusOne()
    {
        var store = new Store();
        var id = await SeedEventAsync(store);

        await using (var context = store.NewContext())
        {
            var repository = new EventRepository(context, CreateMapper());
            var model = new Event
            {
                Id = id,
                Title = "Renamed through the form",
                Date = new DateOnly(2026, 10, 1),
                StartTime = new TimeOnly(19, 0),
                GroupId = BoardId,
                CreatedAt = SeededAt,
                FeedRevision = 0,
                FeedRevisedAt = default,
            };

            await repository.UpdateAsync(model, Token);
        }

        var stored = await ReadEventAsync(store, id);
        stored.FeedRevision.Should().Be(2);
        stored.FeedRevisedAt.Should().Be(ClockNow);
    }

    // ---- The stamp never goes backwards ----

    [Fact]
    public async Task ClockReadingEarlierThanTheStoredStamp_RaisesTheRevisionAndKeepsTheStoredStamp()
    {
        var store = new Store();
        var id = await SeedEventAsync(store);
        await ChangeEventAsync(store, id, e => e.Title = "First change");

        store.Clock.Now = ClockNow.AddDays(-20);
        await ChangeEventAsync(store, id, e => e.Title = "Second change");

        var stored = await ReadEventAsync(store, id);
        stored.FeedRevision.Should().Be(3);
        stored.FeedRevisedAt.Should().Be(ClockNow);
    }

    // ---- Both save overloads ----

    [Fact]
    public async Task SynchronousSaveChanges_AppliesTheSameRules()
    {
        var store = new Store();
        var eventId = await SeedEventAsync(store);
        var questId = await SeedQuestAsync(store);

        using (var context = store.NewContext())
        {
            context.Events.IgnoreQueryFilters().Single(e => e.Id == eventId).Title = "Renamed";
            context.Quests.IgnoreQueryFilters().Single(q => q.Id == questId).Description = "Only the description";
            context.SaveChanges();
        }

        var storedEvent = await ReadEventAsync(store, eventId);
        var storedQuest = await ReadQuestAsync(store, questId);
        storedEvent.FeedRevision.Should().Be(2);
        storedEvent.FeedRevisedAt.Should().Be(ClockNow);
        storedQuest.FeedRevision.Should().Be(1);
        storedQuest.FeedRevisedAt.Should().Be(SeededAt);
    }

    // ---- The write paths the application really uses ----

    private static QuestRepository CreateQuestRepository(QuestBoardContext context) =>
        new(context, CreateMapper(), new FakeBoardClock());

    [Fact]
    public async Task QuestRepositoryFinalize_RaisesTheRevisionByOne()
    {
        var store = new Store();
        var id = await SeedQuestAsync(store, finalized: false);

        await using (var context = store.NewContext())
        {
            await CreateQuestRepository(context).FinalizeQuestAsync(id, FinalizedFor, [], Token);
        }

        (await ReadQuestAsync(store, id)).FeedRevision.Should().Be(2);
    }

    [Fact]
    public async Task QuestRepositoryOpen_RaisesTheRevisionByOne()
    {
        var store = new Store();
        var id = await SeedQuestAsync(store);

        await using (var context = store.NewContext())
        {
            await CreateQuestRepository(context).OpenQuestAsync(id, Token);
        }

        (await ReadQuestAsync(store, id)).FeedRevision.Should().Be(2);
    }

    [Fact]
    public async Task QuestRepositoryPropertyUpdate_ChangingTheTitle_RaisesTheRevisionByOne()
    {
        var store = new Store();
        var id = await SeedQuestAsync(store);

        await using (var context = store.NewContext())
        {
            await CreateQuestRepository(context).UpdateQuestPropertiesWithNotificationsAsync(
                id, "Renamed", "Original description", null, 1, 0, false, token: Token);
        }

        (await ReadQuestAsync(store, id)).FeedRevision.Should().Be(2);
    }

    [Fact]
    public async Task QuestRepositoryPropertyUpdate_ChangingOnlyTheDescription_RaisesNothing()
    {
        var store = new Store();
        var id = await SeedQuestAsync(store);

        await using (var context = store.NewContext())
        {
            await CreateQuestRepository(context).UpdateQuestPropertiesWithNotificationsAsync(
                id, "Original", "A different description", null, 1, 0, false, token: Token);
        }

        var stored = await ReadQuestAsync(store, id);
        stored.Description.Should().Be("A different description");
        stored.FeedRevision.Should().Be(1);
        stored.FeedRevisedAt.Should().Be(SeededAt);
    }

    [Fact]
    public async Task QuestRepositoryRecordingTheFinalizedEmail_RaisesNothing()
    {
        var store = new Store();
        var id = await SeedQuestAsync(store);

        await using (var context = store.NewContext())
        {
            await CreateQuestRepository(context).SetFinalizedEmailSentForDateAsync(id, FinalizedFor, Token);
        }

        var stored = await ReadQuestAsync(store, id);
        stored.FinalizedEmailSentForDate.Should().Be(FinalizedFor);
        stored.FeedRevision.Should().Be(1);
        stored.FeedRevisedAt.Should().Be(SeededAt);
    }

    [Fact]
    public async Task EventRepositoryTemplateSweep_RaisesOnlyTheOccurrencesWhoseTitleOrStartTimeChanged()
    {
        var store = new Store();
        var retitled = await SeedEventAsync(store);
        var moved = await SeedEventAsync(store, e =>
        {
            e.Title = "Session";
            e.StartTime = new TimeOnly(18, 0);
        });
        var describedOnly = await SeedEventAsync(store, e =>
        {
            e.Title = "Session";
            e.StartTime = new TimeOnly(20, 0);
            e.Description = "Old description";
        });

        await using (var context = store.NewContext())
        {
            var repository = new EventRepository(context, CreateMapper());
            await repository.ApplyTemplateToOccurrencesAsync(
                [retitled, moved, describedOnly], "Session", "New description", new TimeOnly(20, 0), Token);
        }

        (await ReadEventAsync(store, retitled)).FeedRevision.Should().Be(2);
        (await ReadEventAsync(store, moved)).FeedRevision.Should().Be(2);
        var unchanged = await ReadEventAsync(store, describedOnly);
        unchanged.Description.Should().Be("New description");
        unchanged.FeedRevision.Should().Be(1);
        unchanged.FeedRevisedAt.Should().Be(SeededAt);
    }
}
