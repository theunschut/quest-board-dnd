using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Repository;
using QuestBoard.Repository.Entities;

namespace QuestBoard.UnitTests.Repository;

// Proves the repository constraints the rest of the recurring-series feature depends on: the
// slot-existence query stays correct no matter where an occurrence has moved, the runway
// measure counts live upcoming sessions rather than a date horizon, the two removal outcomes
// each do exactly what they promise, and every read stays scoped to the caller's own board.
public class EventSeriesRepositoryTests
{
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

    private static async Task EnsureGroupAsync(QuestBoardContext context, int groupId)
    {
        if (!await context.Groups.AnyAsync(g => g.Id == groupId))
        {
            context.Groups.Add(new GroupEntity { Id = groupId, Name = $"Test Group {groupId}" });
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
    }

    private static async Task<int> SeedSeriesAsync(QuestBoardContext context, int groupId, DateOnly? endDate = null)
    {
        await EnsureGroupAsync(context, groupId);

        var series = new EventSeriesEntity
        {
            Title = "Test Series",
            AnchorDate = DateOnly.FromDateTime(DateTime.Today),
            IntervalWeeks = 1,
            WeekDay = (int)DateTime.Today.DayOfWeek,
            CycleMask = "1",
            EndDate = endDate,
            GroupId = groupId
        };
        context.EventSeries.Add(series);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return series.Id;
    }

    private static async Task<EventEntity> SeedOccurrenceAsync(QuestBoardContext context, int seriesId, int groupId, DateOnly date, int? slotIndex, bool cancelled = false)
    {
        await EnsureGroupAsync(context, groupId);

        var entity = new EventEntity
        {
            Title = "Occurrence",
            Date = date,
            SeriesId = seriesId,
            SeriesSlotIndex = slotIndex,
            CancelledAt = cancelled ? DateTime.UtcNow : null,
            GroupId = groupId
        };
        context.Events.Add(entity);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return entity;
    }

    // -------------------------------------------------------------------
    // GetSlotIndexesForSeriesAsync
    // -------------------------------------------------------------------

    [Fact]
    public async Task GetSlotIndexesForSeriesAsync_OccurrenceMovedFarOutsideRunway_MoveThenRunStillReturnsItsSlot()
    {
        var dbName = nameof(GetSlotIndexesForSeriesAsync_OccurrenceMovedFarOutsideRunway_MoveThenRunStillReturnsItsSlot);
        var groupContext = new MutableTestGroupContext { ActiveGroupId = 1 };
        await using var context = CreateContext(dbName, groupContext);
        var seriesId = await SeedSeriesAsync(context, groupId: 1);
        // Two years beyond every other occurrence -- well outside any runway window.
        await SeedOccurrenceAsync(context, seriesId, 1, DateOnly.FromDateTime(DateTime.Today.AddYears(2)), slotIndex: 3);
        var repository = new EventSeriesRepository(context, CreateMapper(), new EventRepository(context, CreateMapper()));

        var slots = await repository.GetSlotIndexesForSeriesAsync(seriesId, TestContext.Current.CancellationToken);

        slots.Should().Contain(3);
    }

    [Fact]
    public async Task GetSlotIndexesForSeriesAsync_CancelledOccurrence_ReturnsItsSlot()
    {
        var dbName = nameof(GetSlotIndexesForSeriesAsync_CancelledOccurrence_ReturnsItsSlot);
        var groupContext = new MutableTestGroupContext { ActiveGroupId = 1 };
        await using var context = CreateContext(dbName, groupContext);
        var seriesId = await SeedSeriesAsync(context, groupId: 1);
        await SeedOccurrenceAsync(context, seriesId, 1, DateOnly.FromDateTime(DateTime.Today), slotIndex: 5, cancelled: true);
        var repository = new EventSeriesRepository(context, CreateMapper(), new EventRepository(context, CreateMapper()));

        var slots = await repository.GetSlotIndexesForSeriesAsync(seriesId, TestContext.Current.CancellationToken);

        slots.Should().Contain(5);
    }

    // -------------------------------------------------------------------
    // CountLiveFutureOccurrencesAsync
    // -------------------------------------------------------------------

    [Fact]
    public async Task CountLiveFutureOccurrencesAsync_ExcludesCancelledAndPast_IncludesToday()
    {
        var dbName = nameof(CountLiveFutureOccurrencesAsync_ExcludesCancelledAndPast_IncludesToday);
        var groupContext = new MutableTestGroupContext { ActiveGroupId = 1 };
        await using var context = CreateContext(dbName, groupContext);
        var seriesId = await SeedSeriesAsync(context, groupId: 1);
        var today = DateOnly.FromDateTime(DateTime.Today);

        await SeedOccurrenceAsync(context, seriesId, 1, today, slotIndex: 0);
        await SeedOccurrenceAsync(context, seriesId, 1, today.AddDays(7), slotIndex: 1);
        await SeedOccurrenceAsync(context, seriesId, 1, today.AddDays(14), slotIndex: 2, cancelled: true);
        await SeedOccurrenceAsync(context, seriesId, 1, today.AddDays(-7), slotIndex: 3);

        var repository = new EventSeriesRepository(context, CreateMapper(), new EventRepository(context, CreateMapper()));

        var count = await repository.CountLiveFutureOccurrencesAsync(seriesId, today, TestContext.Current.CancellationToken);

        count.Should().Be(2);
    }

    // -------------------------------------------------------------------
    // GetSeriesBelowRunwayAsync
    // -------------------------------------------------------------------

    [Fact]
    public async Task GetSeriesBelowRunwayAsync_ReturnsSeriesBelowTarget_OmitsSeriesAtTarget_OmitsEndedSeries()
    {
        var dbName = nameof(GetSeriesBelowRunwayAsync_ReturnsSeriesBelowTarget_OmitsSeriesAtTarget_OmitsEndedSeries);
        var groupContext = new MutableTestGroupContext { ActiveGroupId = 1 };
        await using var context = CreateContext(dbName, groupContext);
        var today = DateOnly.FromDateTime(DateTime.Today);

        var seriesBelowTarget = await SeedSeriesAsync(context, groupId: 1);
        for (var i = 0; i < 3; i++)
        {
            await SeedOccurrenceAsync(context, seriesBelowTarget, 1, today.AddDays(7 * i), slotIndex: i);
        }

        var seriesAtTarget = await SeedSeriesAsync(context, groupId: 1);
        for (var i = 0; i < 20; i++)
        {
            await SeedOccurrenceAsync(context, seriesAtTarget, 1, today.AddDays(7 * i), slotIndex: i);
        }

        var endedSeries = await SeedSeriesAsync(context, groupId: 1, endDate: today.AddDays(-1));

        var repository = new EventSeriesRepository(context, CreateMapper(), new EventRepository(context, CreateMapper()));

        var results = await repository.GetSeriesBelowRunwayAsync(today, 20, TestContext.Current.CancellationToken);

        results.Should().ContainSingle(r => r.SeriesId == seriesBelowTarget);
        results.Should().NotContain(r => r.SeriesId == seriesAtTarget);
        results.Should().NotContain(r => r.SeriesId == endedSeries);
    }

    // -------------------------------------------------------------------
    // GetRemovalImpactAsync
    // -------------------------------------------------------------------

    [Fact]
    public async Task GetRemovalImpactAsync_SplitsPastAndFuture_CountsOnlyAnsweredSignups()
    {
        var dbName = nameof(GetRemovalImpactAsync_SplitsPastAndFuture_CountsOnlyAnsweredSignups);
        var groupContext = new MutableTestGroupContext { ActiveGroupId = 1 };
        await using var context = CreateContext(dbName, groupContext);
        var today = DateOnly.FromDateTime(DateTime.Today);
        var seriesId = await SeedSeriesAsync(context, groupId: 1);

        var past1 = await SeedOccurrenceAsync(context, seriesId, 1, today.AddDays(-14), slotIndex: 0);
        await SeedOccurrenceAsync(context, seriesId, 1, today.AddDays(-7), slotIndex: 1);
        var future1 = await SeedOccurrenceAsync(context, seriesId, 1, today.AddDays(7), slotIndex: 2);

        context.UserEntities.Add(new UserEntity { Id = 101, Name = "User 101", Email = "user101@test.com" });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // A real answer -- UpdatedAt set -- must count.
        context.EventSignups.Add(new EventSignupEntity { EventId = past1.Id, UserId = 101, Availability = 2, UpdatedAt = DateTime.UtcNow });
        // An automatic pass row -- UpdatedAt null -- must not count.
        context.EventSignups.Add(new EventSignupEntity { EventId = future1.Id, UserId = 101, Availability = 2, UpdatedAt = null });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var repository = new EventSeriesRepository(context, CreateMapper(), new EventRepository(context, CreateMapper()));

        var impact = await repository.GetRemovalImpactAsync(seriesId, today, TestContext.Current.CancellationToken);

        impact.PastCount.Should().Be(2);
        impact.FutureCount.Should().Be(1);
        impact.AnsweredCount.Should().Be(1);
    }

    // -------------------------------------------------------------------
    // SetEndDateAsync
    // -------------------------------------------------------------------

    [Fact]
    public async Task SetEndDateAsync_WithRemoval_DeletesOnlyOccurrencesAfterEndDate()
    {
        var dbName = nameof(SetEndDateAsync_WithRemoval_DeletesOnlyOccurrencesAfterEndDate);
        var groupContext = new MutableTestGroupContext { ActiveGroupId = 1 };
        await using var context = CreateContext(dbName, groupContext);
        var today = DateOnly.FromDateTime(DateTime.Today);
        var seriesId = await SeedSeriesAsync(context, groupId: 1);
        var endDate = today.AddDays(14);

        var kept1 = await SeedOccurrenceAsync(context, seriesId, 1, today, slotIndex: 0);
        var kept2 = await SeedOccurrenceAsync(context, seriesId, 1, endDate, slotIndex: 1); // on the end date -- kept
        var removed = await SeedOccurrenceAsync(context, seriesId, 1, endDate.AddDays(7), slotIndex: 2); // after -- removed

        var repository = new EventSeriesRepository(context, CreateMapper(), new EventRepository(context, CreateMapper()));

        var removedCount = await repository.SetEndDateAsync(seriesId, endDate, true, TestContext.Current.CancellationToken);

        removedCount.Should().Be(1);
        var remainingIds = await context.Events.Where(e => e.SeriesId == seriesId).Select(e => e.Id).ToListAsync(TestContext.Current.CancellationToken);
        remainingIds.Should().BeEquivalentTo([kept1.Id, kept2.Id]);
        (await context.Events.AnyAsync(e => e.Id == removed.Id, TestContext.Current.CancellationToken)).Should().BeFalse();
    }

    // -------------------------------------------------------------------
    // DeleteWithOccurrencesAsync
    // -------------------------------------------------------------------

    [Fact]
    public async Task DeleteWithOccurrencesAsync_LeavesNoOccurrenceAndNoSeriesRow()
    {
        var dbName = nameof(DeleteWithOccurrencesAsync_LeavesNoOccurrenceAndNoSeriesRow);
        var groupContext = new MutableTestGroupContext { ActiveGroupId = 1 };
        await using var context = CreateContext(dbName, groupContext);
        var seriesId = await SeedSeriesAsync(context, groupId: 1);
        await SeedOccurrenceAsync(context, seriesId, 1, DateOnly.FromDateTime(DateTime.Today), slotIndex: 0);
        await SeedOccurrenceAsync(context, seriesId, 1, DateOnly.FromDateTime(DateTime.Today).AddDays(7), slotIndex: 1);

        var repository = new EventSeriesRepository(context, CreateMapper(), new EventRepository(context, CreateMapper()));

        await repository.DeleteWithOccurrencesAsync(seriesId, TestContext.Current.CancellationToken);

        (await context.Events.AnyAsync(e => e.SeriesId == seriesId, TestContext.Current.CancellationToken)).Should().BeFalse();
        (await context.EventSeries.AnyAsync(s => s.Id == seriesId, TestContext.Current.CancellationToken)).Should().BeFalse();
    }

    // -------------------------------------------------------------------
    // DetachOccurrencesAndDeleteAsync
    // -------------------------------------------------------------------

    [Fact]
    public async Task DetachOccurrencesAndDeleteAsync_LeavesOccurrencesWithNullSeriesColumns_KeepsCancelledMarker()
    {
        var dbName = nameof(DetachOccurrencesAndDeleteAsync_LeavesOccurrencesWithNullSeriesColumns_KeepsCancelledMarker);
        var groupContext = new MutableTestGroupContext { ActiveGroupId = 1 };
        await using var context = CreateContext(dbName, groupContext);
        var seriesId = await SeedSeriesAsync(context, groupId: 1);
        var live = await SeedOccurrenceAsync(context, seriesId, 1, DateOnly.FromDateTime(DateTime.Today), slotIndex: 0);
        var cancelled = await SeedOccurrenceAsync(context, seriesId, 1, DateOnly.FromDateTime(DateTime.Today).AddDays(7), slotIndex: 1, cancelled: true);

        var repository = new EventSeriesRepository(context, CreateMapper(), new EventRepository(context, CreateMapper()));

        await repository.DetachOccurrencesAndDeleteAsync(seriesId, TestContext.Current.CancellationToken);

        var liveAfter = await context.Events.SingleAsync(e => e.Id == live.Id, TestContext.Current.CancellationToken);
        var cancelledAfter = await context.Events.SingleAsync(e => e.Id == cancelled.Id, TestContext.Current.CancellationToken);

        liveAfter.SeriesId.Should().BeNull();
        liveAfter.SeriesSlotIndex.Should().BeNull();
        cancelledAfter.SeriesId.Should().BeNull();
        cancelledAfter.SeriesSlotIndex.Should().BeNull();
        cancelledAfter.CancelledAt.Should().NotBeNull();
        (await context.EventSeries.AnyAsync(s => s.Id == seriesId, TestContext.Current.CancellationToken)).Should().BeFalse();
    }

    // -------------------------------------------------------------------
    // SetCancelledAsync (EventRepository)
    // -------------------------------------------------------------------

    [Fact]
    public async Task SetCancelledAsync_SetsThenClearsMarker_WithoutDisturbingSignups()
    {
        var dbName = nameof(SetCancelledAsync_SetsThenClearsMarker_WithoutDisturbingSignups);
        var groupContext = new MutableTestGroupContext { ActiveGroupId = 1 };
        await using var context = CreateContext(dbName, groupContext);
        await EnsureGroupAsync(context, 1);
        var occurrence = new EventEntity { Title = "Occurrence", Date = DateOnly.FromDateTime(DateTime.Today), GroupId = 1 };
        context.Events.Add(occurrence);
        context.UserEntities.Add(new UserEntity { Id = 101, Name = "User 101", Email = "user101@test.com" });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        context.EventSignups.Add(new EventSignupEntity { EventId = occurrence.Id, UserId = 101, Availability = 2, UpdatedAt = DateTime.UtcNow });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var eventRepository = new EventRepository(context, CreateMapper());

        // EF's identity map returns the same tracked instance on every query against this
        // context, so the entity's CancelledAt is read out into a value immediately after each
        // write rather than holding onto the entity reference across both writes.
        var setResult = await eventRepository.SetCancelledAsync(occurrence.Id, DateTime.UtcNow, TestContext.Current.CancellationToken);
        var cancelledAtAfterSet = (await context.Events.SingleAsync(e => e.Id == occurrence.Id, TestContext.Current.CancellationToken)).CancelledAt;

        var clearResult = await eventRepository.SetCancelledAsync(occurrence.Id, null, TestContext.Current.CancellationToken);
        var cancelledAtAfterClear = (await context.Events.SingleAsync(e => e.Id == occurrence.Id, TestContext.Current.CancellationToken)).CancelledAt;

        setResult.Should().BeTrue();
        cancelledAtAfterSet.Should().NotBeNull();
        clearResult.Should().BeTrue();
        cancelledAtAfterClear.Should().BeNull();

        var signupCount = await context.EventSignups.CountAsync(s => s.EventId == occurrence.Id, TestContext.Current.CancellationToken);
        signupCount.Should().Be(1);
    }

    // -------------------------------------------------------------------
    // ApplyTemplateToOccurrencesAsync (EventRepository)
    // -------------------------------------------------------------------

    [Fact]
    public async Task ApplyTemplateToOccurrencesAsync_UpdatesOnlyGivenIds_ReturnsCount()
    {
        var dbName = nameof(ApplyTemplateToOccurrencesAsync_UpdatesOnlyGivenIds_ReturnsCount);
        var groupContext = new MutableTestGroupContext { ActiveGroupId = 1 };
        await using var context = CreateContext(dbName, groupContext);
        await EnsureGroupAsync(context, 1);
        var target1 = new EventEntity { Title = "Old", Date = DateOnly.FromDateTime(DateTime.Today), GroupId = 1 };
        var target2 = new EventEntity { Title = "Old", Date = DateOnly.FromDateTime(DateTime.Today).AddDays(7), GroupId = 1 };
        var untouched = new EventEntity { Title = "Old", Date = DateOnly.FromDateTime(DateTime.Today).AddDays(14), GroupId = 1 };
        context.Events.AddRange(target1, target2, untouched);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var eventRepository = new EventRepository(context, CreateMapper());

        var updatedCount = await eventRepository.ApplyTemplateToOccurrencesAsync(
            [target1.Id, target2.Id], "New Title", "New Description", new TimeOnly(19, 0), TestContext.Current.CancellationToken);

        updatedCount.Should().Be(2);
        (await context.Events.SingleAsync(e => e.Id == target1.Id, TestContext.Current.CancellationToken)).Title.Should().Be("New Title");
        (await context.Events.SingleAsync(e => e.Id == target2.Id, TestContext.Current.CancellationToken)).Title.Should().Be("New Title");
        (await context.Events.SingleAsync(e => e.Id == untouched.Id, TestContext.Current.CancellationToken)).Title.Should().Be("Old");
    }

    // -------------------------------------------------------------------
    // CountLiveSiblingsOnDateAsync (EventRepository)
    // -------------------------------------------------------------------

    [Fact]
    public async Task CountLiveSiblingsOnDateAsync_CountsLiveSibling_IgnoresCancelledSibling_IgnoresExcludedEvent()
    {
        var dbName = nameof(CountLiveSiblingsOnDateAsync_CountsLiveSibling_IgnoresCancelledSibling_IgnoresExcludedEvent);
        var groupContext = new MutableTestGroupContext { ActiveGroupId = 1 };
        await using var context = CreateContext(dbName, groupContext);
        var seriesId = await SeedSeriesAsync(context, groupId: 1);
        var date = DateOnly.FromDateTime(DateTime.Today).AddDays(7);

        var moved = await SeedOccurrenceAsync(context, seriesId, 1, date, slotIndex: 0);
        await SeedOccurrenceAsync(context, seriesId, 1, date, slotIndex: 1);
        await SeedOccurrenceAsync(context, seriesId, 1, date, slotIndex: 2, cancelled: true);

        var eventRepository = new EventRepository(context, CreateMapper());

        var count = await eventRepository.CountLiveSiblingsOnDateAsync(seriesId, date, moved.Id, TestContext.Current.CancellationToken);

        count.Should().Be(1);
    }

    // -------------------------------------------------------------------
    // Cross-board isolation
    // -------------------------------------------------------------------

    [Fact]
    public async Task EventSeries_SeededOnGroup2_IsInvisibleWhenActiveGroupIs1()
    {
        var dbName = nameof(EventSeries_SeededOnGroup2_IsInvisibleWhenActiveGroupIs1);

        int seriesId;
        var seedGroupContext = new MutableTestGroupContext { ActiveGroupId = null };
        await using (var seedContext = CreateContext(dbName, seedGroupContext))
        {
            seriesId = await SeedSeriesAsync(seedContext, groupId: 2);
            await SeedOccurrenceAsync(seedContext, seriesId, 2, DateOnly.FromDateTime(DateTime.Today), slotIndex: 0);
        }

        var activeGroupContext = new MutableTestGroupContext { ActiveGroupId = 1 };
        await using var context = CreateContext(dbName, activeGroupContext);
        var repository = new EventSeriesRepository(context, CreateMapper(), new EventRepository(context, CreateMapper()));

        var series = await repository.GetSeriesAsync(seriesId, TestContext.Current.CancellationToken);
        var occurrences = await new EventRepository(context, CreateMapper()).GetOccurrencesForSeriesAsync(seriesId, TestContext.Current.CancellationToken);
        var horizon = await repository.GetSeriesBelowRunwayAsync(DateOnly.FromDateTime(DateTime.Today), 100, TestContext.Current.CancellationToken);

        series.Should().BeNull();
        occurrences.Should().BeEmpty();
        horizon.Should().NotContain(r => r.SeriesId == seriesId);
    }

    private sealed class MutableTestGroupContext : IActiveGroupContext
    {
        public int? ActiveGroupId { get; set; }
    }
}
