using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.Domain.Services;
using QuestBoard.Repository;
using QuestBoard.Repository.Entities;

namespace QuestBoard.UnitTests.Repository;

// Proves the guarantee the whole recurring-series feature depends on: re-running the
// generator against real, persisted data never duplicates, resurrects a cancelled
// occurrence, or recreates one that has been moved -- including one moved far outside the
// runway -- and that the this-and-future edit scope only ever touches rows nobody has
// separately moved, edited or cancelled.
public class EventSeriesMaterializationTests
{
    private const int RunwaySize = 5;

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

    private static EventSeriesService CreateService(
        QuestBoardContext context,
        IActiveGroupContext groupContext,
        IBoardTypeResolver boardTypeResolver,
        IUserRepository userRepository,
        int runwaySize = RunwaySize,
        int previewCount = 3)
    {
        var mapper = CreateMapper();
        var eventRepository = new EventRepository(context, mapper);
        var seriesRepository = new EventSeriesRepository(context, mapper, eventRepository);
        var seriesOptions = Options.Create(new EventSeriesOptions { RunwaySize = runwaySize, PreviewCount = previewCount });

        return new EventSeriesService(seriesRepository, eventRepository, userRepository, boardTypeResolver, groupContext, seriesOptions);
    }

    private static IBoardTypeResolver OneShotResolver()
    {
        var resolver = Substitute.For<IBoardTypeResolver>();
        resolver.GetBoardTypeAsync(Arg.Any<CancellationToken>()).Returns(BoardType.OneShot);
        return resolver;
    }

    private static IBoardTypeResolver CampaignResolver()
    {
        var resolver = Substitute.For<IBoardTypeResolver>();
        resolver.GetBoardTypeAsync(Arg.Any<CancellationToken>()).Returns(BoardType.Campaign);
        return resolver;
    }

    private static IUserRepository NoMembersRepository()
    {
        var repository = Substitute.For<IUserRepository>();
        repository.GetAllGroupMembers(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(new List<User>());
        return repository;
    }

    private static IUserRepository MembersRepository(int groupId, params int[] userIds)
    {
        var repository = Substitute.For<IUserRepository>();
        var members = userIds.Select(id => new User { Id = id, Name = $"Member {id}" }).ToList();
        repository.GetAllGroupMembers(groupId, Arg.Any<CancellationToken>()).Returns(members);
        return repository;
    }

    private static async Task EnsureGroupAsync(QuestBoardContext context, int groupId)
    {
        if (!await context.Groups.AnyAsync(g => g.Id == groupId))
        {
            context.Groups.Add(new GroupEntity { Id = groupId, Name = $"Test Group {groupId}" });
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
    }

    private static async Task EnsureUsersAsync(QuestBoardContext context, params int[] userIds)
    {
        foreach (var userId in userIds)
        {
            if (!await context.UserEntities.AnyAsync(u => u.Id == userId))
            {
                context.UserEntities.Add(new UserEntity { Id = userId, Name = $"Member {userId}", Email = $"member{userId}@test.com" });
            }
        }
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<int> SeedSeriesAsync(
        QuestBoardContext context,
        int groupId,
        DateOnly anchorDate,
        int intervalWeeks,
        string cycleMask,
        DateOnly? endDate = null,
        string title = "Test Series")
    {
        await EnsureGroupAsync(context, groupId);

        var series = new EventSeriesEntity
        {
            Title = title,
            AnchorDate = anchorDate,
            IntervalWeeks = intervalWeeks,
            WeekDay = (int)anchorDate.DayOfWeek,
            CycleMask = cycleMask,
            EndDate = endDate,
            GroupId = groupId
        };
        context.EventSeries.Add(series);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return series.Id;
    }

    private static async Task<EventEntity> SeedOccurrenceAsync(
        QuestBoardContext context,
        int seriesId,
        int groupId,
        DateOnly date,
        int? slotIndex,
        bool cancelled = false,
        string title = "Test Series")
    {
        await EnsureGroupAsync(context, groupId);

        var entity = new EventEntity
        {
            Title = title,
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
    // TopUpAsync -- fresh series, idempotency, end date
    // -------------------------------------------------------------------

    [Fact]
    public async Task TopUpAsync_FreshSeries_CreatesRunwayCountOfDistinctFiringOccurrencesDatedTodayOrLater()
    {
        var dbName = nameof(TopUpAsync_FreshSeries_CreatesRunwayCountOfDistinctFiringOccurrencesDatedTodayOrLater);
        var groupContext = new MutableTestGroupContext { ActiveGroupId = 1 };
        await using var context = CreateContext(dbName, groupContext);
        var today = DateOnly.FromDateTime(DateTime.Today);
        var seriesId = await SeedSeriesAsync(context, 1, today, 1, "1");
        var service = CreateService(context, groupContext, OneShotResolver(), NoMembersRepository());

        var created = await service.TopUpAsync(seriesId, TestContext.Current.CancellationToken);

        created.Should().Be(RunwaySize);
        var occurrences = await context.Events.Where(e => e.SeriesId == seriesId).ToListAsync(TestContext.Current.CancellationToken);
        occurrences.Should().HaveCount(RunwaySize);
        occurrences.Should().OnlyContain(o => o.Date >= today);
        occurrences.Select(o => o.SeriesSlotIndex).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task TopUpAsync_RunTwiceImmediately_Idempotency_CreatesNoAdditionalOccurrences()
    {
        var dbName = nameof(TopUpAsync_RunTwiceImmediately_Idempotency_CreatesNoAdditionalOccurrences);
        var groupContext = new MutableTestGroupContext { ActiveGroupId = 1 };
        await using var context = CreateContext(dbName, groupContext);
        var today = DateOnly.FromDateTime(DateTime.Today);
        var seriesId = await SeedSeriesAsync(context, 1, today, 1, "1");
        var service = CreateService(context, groupContext, OneShotResolver(), NoMembersRepository());

        var firstRun = await service.TopUpAsync(seriesId, TestContext.Current.CancellationToken);
        var secondRun = await service.TopUpAsync(seriesId, TestContext.Current.CancellationToken);

        firstRun.Should().Be(RunwaySize);
        secondRun.Should().Be(0);
        var totalRows = await context.Events.CountAsync(e => e.SeriesId == seriesId, TestContext.Current.CancellationToken);
        totalRows.Should().Be(RunwaySize);
    }

    [Fact]
    public async Task TopUpAsync_EndDateAlreadyPassed_ProducesZeroOccurrences()
    {
        var dbName = nameof(TopUpAsync_EndDateAlreadyPassed_ProducesZeroOccurrences);
        var groupContext = new MutableTestGroupContext { ActiveGroupId = 1 };
        await using var context = CreateContext(dbName, groupContext);
        var today = DateOnly.FromDateTime(DateTime.Today);
        var seriesId = await SeedSeriesAsync(context, 1, today.AddDays(-70), 1, "1", endDate: today.AddDays(-7));
        var service = CreateService(context, groupContext, OneShotResolver(), NoMembersRepository());

        var created = await service.TopUpAsync(seriesId, TestContext.Current.CancellationToken);

        created.Should().Be(0);
        var totalRows = await context.Events.CountAsync(e => e.SeriesId == seriesId, TestContext.Current.CancellationToken);
        totalRows.Should().Be(0);
    }

    [Fact]
    public async Task TopUpAsync_EndDateMidWindow_TruncatesWithNoOccurrenceAfterEndDate()
    {
        var dbName = nameof(TopUpAsync_EndDateMidWindow_TruncatesWithNoOccurrenceAfterEndDate);
        var groupContext = new MutableTestGroupContext { ActiveGroupId = 1 };
        await using var context = CreateContext(dbName, groupContext);
        var today = DateOnly.FromDateTime(DateTime.Today);
        // End date falls between the 2nd and 3rd weekly slot, well short of the runway of 5.
        var endDate = today.AddDays(10);
        var seriesId = await SeedSeriesAsync(context, 1, today, 1, "1", endDate: endDate);
        var service = CreateService(context, groupContext, OneShotResolver(), NoMembersRepository());

        var created = await service.TopUpAsync(seriesId, TestContext.Current.CancellationToken);

        created.Should().BeLessThan(RunwaySize);
        var occurrences = await context.Events.Where(e => e.SeriesId == seriesId).ToListAsync(TestContext.Current.CancellationToken);
        occurrences.Should().OnlyContain(o => o.Date <= endDate);
    }

    // -------------------------------------------------------------------
    // TopUpAsync -- cancel then run
    // -------------------------------------------------------------------

    [Fact]
    public async Task TopUpAsync_CancelOccurrenceThenRun_Cancel_CreatesOneAtNextUnseenSlotAndKeepsCancelledRowWithSignups()
    {
        var dbName = nameof(TopUpAsync_CancelOccurrenceThenRun_Cancel_CreatesOneAtNextUnseenSlotAndKeepsCancelledRowWithSignups);
        var groupContext = new MutableTestGroupContext { ActiveGroupId = 1 };
        await using var context = CreateContext(dbName, groupContext);
        await EnsureUsersAsync(context, 201, 202);
        var today = DateOnly.FromDateTime(DateTime.Today);
        var seriesId = await SeedSeriesAsync(context, 1, today, 1, "1");
        var service = CreateService(context, groupContext, CampaignResolver(), MembersRepository(1, 201, 202));

        await service.TopUpAsync(seriesId, TestContext.Current.CancellationToken);
        var beforeCancel = await context.Events.Where(e => e.SeriesId == seriesId).OrderBy(e => e.SeriesSlotIndex).ToListAsync(TestContext.Current.CancellationToken);
        beforeCancel.Should().HaveCount(RunwaySize);
        var toCancel = beforeCancel.First();

        var eventRepository = new EventRepository(context, CreateMapper());
        await eventRepository.SetCancelledAsync(toCancel.Id, DateTime.UtcNow, TestContext.Current.CancellationToken);

        var createdAfterCancel = await service.TopUpAsync(seriesId, TestContext.Current.CancellationToken);

        createdAfterCancel.Should().Be(1);
        var afterRun = await context.Events.Where(e => e.SeriesId == seriesId).ToListAsync(TestContext.Current.CancellationToken);
        afterRun.Should().HaveCount(RunwaySize + 1);

        var newSlotIndexes = afterRun.Select(o => o.SeriesSlotIndex).ToList();
        // Distinct slot indexes overall, and the cancelled slot appears exactly once --
        // it is never reused for the newly created occurrence.
        newSlotIndexes.Should().OnlyHaveUniqueItems();
        newSlotIndexes.Count(s => s == toCancel.SeriesSlotIndex).Should().Be(1);

        var cancelledAfter = await context.Events.SingleAsync(e => e.Id == toCancel.Id, TestContext.Current.CancellationToken);
        cancelledAfter.CancelledAt.Should().NotBeNull();
        cancelledAfter.SeriesSlotIndex.Should().Be(toCancel.SeriesSlotIndex);

        var signupCount = await context.EventSignups.CountAsync(s => s.EventId == toCancel.Id, TestContext.Current.CancellationToken);
        signupCount.Should().Be(2);
    }

    // -------------------------------------------------------------------
    // TopUpAsync -- move then run
    // -------------------------------------------------------------------

    [Fact]
    public async Task TopUpAsync_MoveOccurrenceToDifferentDateThenRun_MoveThenRun_CreatesNothingOnOriginalDateAndKeepsMovedRow()
    {
        var dbName = nameof(TopUpAsync_MoveOccurrenceToDifferentDateThenRun_MoveThenRun_CreatesNothingOnOriginalDateAndKeepsMovedRow);
        var groupContext = new MutableTestGroupContext { ActiveGroupId = 1 };
        await using var context = CreateContext(dbName, groupContext);
        var today = DateOnly.FromDateTime(DateTime.Today);
        var seriesId = await SeedSeriesAsync(context, 1, today, 1, "1");
        var service = CreateService(context, groupContext, OneShotResolver(), NoMembersRepository());

        await service.TopUpAsync(seriesId, TestContext.Current.CancellationToken);
        var occurrences = await context.Events.Where(e => e.SeriesId == seriesId).OrderBy(e => e.SeriesSlotIndex).ToListAsync(TestContext.Current.CancellationToken);
        var moved = occurrences.First();
        var originalDate = moved.Date;
        var newDate = originalDate.AddDays(30);
        moved.Date = newDate;
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var createdAfterMove = await service.TopUpAsync(seriesId, TestContext.Current.CancellationToken);

        // The moved slot is still in the existing-slot set (no date predicate), so the
        // top-up never regenerates it. Fresh slots may still be created to satisfy the
        // runway, but never one dated on the vacated original date.
        var afterRun = await context.Events.Where(e => e.SeriesId == seriesId).ToListAsync(TestContext.Current.CancellationToken);
        afterRun.Should().NotContain(o => o.Date == originalDate && o.Id != moved.Id);
        var movedRow = await context.Events.SingleAsync(e => e.Id == moved.Id, TestContext.Current.CancellationToken);
        movedRow.Date.Should().Be(newDate);
        movedRow.SeriesSlotIndex.Should().Be(moved.SeriesSlotIndex);
        _ = createdAfterMove; // top-up may add slots to reach the runway target; the assertion above is what matters
    }

    [Fact]
    public async Task TopUpAsync_MoveOccurrenceTwoYearsBeyondRunwayThenRun_MoveThenRun_CreatesNothingForThatSlot()
    {
        var dbName = nameof(TopUpAsync_MoveOccurrenceTwoYearsBeyondRunwayThenRun_MoveThenRun_CreatesNothingForThatSlot);
        var groupContext = new MutableTestGroupContext { ActiveGroupId = 1 };
        await using var context = CreateContext(dbName, groupContext);
        var today = DateOnly.FromDateTime(DateTime.Today);
        var seriesId = await SeedSeriesAsync(context, 1, today, 1, "1");
        // Slot 2's occurrence exists but has been dragged two years into the future --
        // far outside any runway window a naive existence check might use.
        var movedOccurrence = await SeedOccurrenceAsync(context, seriesId, 1, today.AddYears(2), slotIndex: 2);
        var service = CreateService(context, groupContext, OneShotResolver(), NoMembersRepository());

        await service.TopUpAsync(seriesId, TestContext.Current.CancellationToken);

        var slot2Occurrences = await context.Events
            .Where(e => e.SeriesId == seriesId && e.SeriesSlotIndex == 2)
            .ToListAsync(TestContext.Current.CancellationToken);

        // Exactly the one, already-moved row -- the generator must not create a second
        // occurrence at slot 2 on its original in-window date.
        slot2Occurrences.Should().ContainSingle(o => o.Id == movedOccurrence.Id);
    }

    // -------------------------------------------------------------------
    // TopUpAsync -- campaign fan-out and one-shot no fan-out
    // -------------------------------------------------------------------

    [Fact]
    public async Task TopUpAsync_CampaignBoard_OccurrencesCarryOneSignupPerMemberWithAnsweredMarkerUnset()
    {
        var dbName = nameof(TopUpAsync_CampaignBoard_OccurrencesCarryOneSignupPerMemberWithAnsweredMarkerUnset);
        var groupContext = new MutableTestGroupContext { ActiveGroupId = 1 };
        await using var context = CreateContext(dbName, groupContext);
        await EnsureUsersAsync(context, 301, 302, 303);
        var today = DateOnly.FromDateTime(DateTime.Today);
        var seriesId = await SeedSeriesAsync(context, 1, today, 1, "1");
        var service = CreateService(context, groupContext, CampaignResolver(), MembersRepository(1, 301, 302, 303));

        await service.TopUpAsync(seriesId, TestContext.Current.CancellationToken);

        var occurrenceIds = await context.Events.Where(e => e.SeriesId == seriesId).Select(e => e.Id).ToListAsync(TestContext.Current.CancellationToken);
        occurrenceIds.Should().HaveCount(RunwaySize);

        var signups = await context.EventSignups.Where(s => occurrenceIds.Contains(s.EventId)).ToListAsync(TestContext.Current.CancellationToken);
        signups.Should().HaveCount(RunwaySize * 3);
        // The null answered marker is the whole mechanism that later tells an automatic
        // pass apart from a real answer -- a test that only counted rows would miss a
        // regression that stamps it.
        signups.Should().OnlyContain(s => s.UpdatedAt == null);
    }

    [Fact]
    public async Task TopUpAsync_OneShotBoard_OccurrencesCarryNoSignupRows()
    {
        var dbName = nameof(TopUpAsync_OneShotBoard_OccurrencesCarryNoSignupRows);
        var groupContext = new MutableTestGroupContext { ActiveGroupId = 1 };
        await using var context = CreateContext(dbName, groupContext);
        var today = DateOnly.FromDateTime(DateTime.Today);
        var seriesId = await SeedSeriesAsync(context, 1, today, 1, "1");
        var service = CreateService(context, groupContext, OneShotResolver(), NoMembersRepository());

        await service.TopUpAsync(seriesId, TestContext.Current.CancellationToken);

        var occurrenceIds = await context.Events.Where(e => e.SeriesId == seriesId).Select(e => e.Id).ToListAsync(TestContext.Current.CancellationToken);
        var signupCount = await context.EventSignups.CountAsync(s => occurrenceIds.Contains(s.EventId), TestContext.Current.CancellationToken);

        signupCount.Should().Be(0);
    }

    // -------------------------------------------------------------------
    // TopUpAsync -- mirrored masks on two boards
    // -------------------------------------------------------------------

    [Fact]
    public async Task TopUpAsync_TwoSeriesOnTwoBoardsWithMirroredMask_MirroredMask_ProduceZeroSharedDatesAfterTopUp()
    {
        var dbName = nameof(TopUpAsync_TwoSeriesOnTwoBoardsWithMirroredMask_MirroredMask_ProduceZeroSharedDatesAfterTopUp);
        var today = DateOnly.FromDateTime(DateTime.Today);

        // Seed both boards through a context with no active group, so seeding itself is
        // not filtered to either board.
        var seedContext = new MutableTestGroupContext { ActiveGroupId = null };
        await using var seedDbContext = CreateContext(dbName, seedContext);
        var seriesOnBoard1 = await SeedSeriesAsync(seedDbContext, 1, today, 1, "1,1,0,0", title: "Board 1 Series");
        var seriesOnBoard2 = await SeedSeriesAsync(seedDbContext, 2, today, 1, "0,0,1,1", title: "Board 2 Series");

        var board1Context = new MutableTestGroupContext { ActiveGroupId = 1 };
        await using var board1DbContext = CreateContext(dbName, board1Context);
        var board1Service = CreateService(board1DbContext, board1Context, OneShotResolver(), NoMembersRepository(), runwaySize: 8);
        await board1Service.TopUpAsync(seriesOnBoard1, TestContext.Current.CancellationToken);

        var board2Context = new MutableTestGroupContext { ActiveGroupId = 2 };
        await using var board2DbContext = CreateContext(dbName, board2Context);
        var board2Service = CreateService(board2DbContext, board2Context, OneShotResolver(), NoMembersRepository(), runwaySize: 8);
        await board2Service.TopUpAsync(seriesOnBoard2, TestContext.Current.CancellationToken);

        // The query filter fails closed on a null active group (returns nothing, not "every
        // board"), so each board's occurrences are read back through that board's own scoped
        // context rather than a no-group one.
        var board1Dates = await board1DbContext.Events.Where(e => e.SeriesId == seriesOnBoard1).Select(e => e.Date).ToListAsync(TestContext.Current.CancellationToken);
        var board2Dates = await board2DbContext.Events.Where(e => e.SeriesId == seriesOnBoard2).Select(e => e.Date).ToListAsync(TestContext.Current.CancellationToken);

        board1Dates.Should().NotBeEmpty();
        board2Dates.Should().NotBeEmpty();
        board1Dates.Intersect(board2Dates).Should().BeEmpty();
    }

    // -------------------------------------------------------------------
    // ApplyTemplateToFutureAsync -- this-and-future edit scope
    // -------------------------------------------------------------------

    [Fact]
    public async Task ApplyTemplateToFutureAsync_EditScope_UpdatesFutureUntouchedOccurrencesAndSeriesTemplate()
    {
        var dbName = nameof(ApplyTemplateToFutureAsync_EditScope_UpdatesFutureUntouchedOccurrencesAndSeriesTemplate);
        var groupContext = new MutableTestGroupContext { ActiveGroupId = 1 };
        await using var context = CreateContext(dbName, groupContext);
        var today = DateOnly.FromDateTime(DateTime.Today);
        var seriesId = await SeedSeriesAsync(context, 1, today, 1, "1", title: "Old Title");
        var editedEvent = await SeedOccurrenceAsync(context, seriesId, 1, today, slotIndex: 0, title: "Old Title");
        var untouchedFuture = await SeedOccurrenceAsync(context, seriesId, 1, today.AddDays(7), slotIndex: 1, title: "Old Title");

        var service = CreateService(context, groupContext, OneShotResolver(), NoMembersRepository());

        var sweptCount = await service.ApplyTemplateToFutureAsync(seriesId, editedEvent.Id, "New Title", "New Description", new TimeOnly(19, 0), TestContext.Current.CancellationToken);

        sweptCount.Should().Be(1);
        var updated = await context.Events.SingleAsync(e => e.Id == untouchedFuture.Id, TestContext.Current.CancellationToken);
        updated.Title.Should().Be("New Title");

        var series = await context.EventSeries.SingleAsync(s => s.Id == seriesId, TestContext.Current.CancellationToken);
        series.Title.Should().Be("New Title");
        series.Description.Should().Be("New Description");
        series.StartTime.Should().Be(new TimeOnly(19, 0));
    }

    [Fact]
    public async Task ApplyTemplateToFutureAsync_EditScope_SkipsPastOccurrenceEditedOccurrenceAndCancelledOccurrence()
    {
        var dbName = nameof(ApplyTemplateToFutureAsync_EditScope_SkipsPastOccurrenceEditedOccurrenceAndCancelledOccurrence);
        var groupContext = new MutableTestGroupContext { ActiveGroupId = 1 };
        await using var context = CreateContext(dbName, groupContext);
        var today = DateOnly.FromDateTime(DateTime.Today);
        var seriesId = await SeedSeriesAsync(context, 1, today, 1, "1", title: "Old Title");
        var pastOccurrence = await SeedOccurrenceAsync(context, seriesId, 1, today.AddDays(-7), slotIndex: 0, title: "Old Title");
        var editedEvent = await SeedOccurrenceAsync(context, seriesId, 1, today, slotIndex: 1, title: "Old Title");
        var cancelledOccurrence = await SeedOccurrenceAsync(context, seriesId, 1, today.AddDays(14), slotIndex: 2, title: "Old Title", cancelled: true);

        var service = CreateService(context, groupContext, OneShotResolver(), NoMembersRepository());

        var sweptCount = await service.ApplyTemplateToFutureAsync(seriesId, editedEvent.Id, "New Title", "New Description", new TimeOnly(19, 0), TestContext.Current.CancellationToken);

        sweptCount.Should().Be(0);
        (await context.Events.SingleAsync(e => e.Id == pastOccurrence.Id, TestContext.Current.CancellationToken)).Title.Should().Be("Old Title");
        (await context.Events.SingleAsync(e => e.Id == editedEvent.Id, TestContext.Current.CancellationToken)).Title.Should().Be("Old Title");
        (await context.Events.SingleAsync(e => e.Id == cancelledOccurrence.Id, TestContext.Current.CancellationToken)).Title.Should().Be("Old Title");
    }

    [Fact]
    public async Task ApplyTemplateToFutureAsync_EditScope_SkipsSeparatelyMovedAndSeparatelyEditedOccurrences()
    {
        var dbName = nameof(ApplyTemplateToFutureAsync_EditScope_SkipsSeparatelyMovedAndSeparatelyEditedOccurrences);
        var groupContext = new MutableTestGroupContext { ActiveGroupId = 1 };
        await using var context = CreateContext(dbName, groupContext);
        var today = DateOnly.FromDateTime(DateTime.Today);
        var seriesId = await SeedSeriesAsync(context, 1, today, 1, "1", title: "Old Title");
        var editedEvent = await SeedOccurrenceAsync(context, seriesId, 1, today, slotIndex: 0, title: "Old Title");
        // Slot 1's natural date would be today+7; it has been dragged to today+30, so its
        // stored date no longer matches DateForSlot -- a deliberate move that must survive.
        var movedOccurrence = await SeedOccurrenceAsync(context, seriesId, 1, today.AddDays(30), slotIndex: 1, title: "Old Title");
        // Slot 2 sits on its natural date but its title was already changed independently.
        var separatelyEditedOccurrence = await SeedOccurrenceAsync(context, seriesId, 1, today.AddDays(14), slotIndex: 2, title: "Somebody Already Renamed This");

        var service = CreateService(context, groupContext, OneShotResolver(), NoMembersRepository());

        var sweptCount = await service.ApplyTemplateToFutureAsync(seriesId, editedEvent.Id, "New Title", "New Description", new TimeOnly(19, 0), TestContext.Current.CancellationToken);

        sweptCount.Should().Be(0);
        (await context.Events.SingleAsync(e => e.Id == movedOccurrence.Id, TestContext.Current.CancellationToken)).Title.Should().Be("Old Title");
        (await context.Events.SingleAsync(e => e.Id == separatelyEditedOccurrence.Id, TestContext.Current.CancellationToken)).Title.Should().Be("Somebody Already Renamed This");
    }

    // -------------------------------------------------------------------
    // PreviewAsync -- shares the generator materialization later uses
    // -------------------------------------------------------------------

    [Fact]
    public async Task PreviewAsync_SameCadenceAsMaterialization_ReturnsExactDatesTopUpWouldMaterialize()
    {
        var dbName = nameof(PreviewAsync_SameCadenceAsMaterialization_ReturnsExactDatesTopUpWouldMaterialize);
        var groupContext = new MutableTestGroupContext { ActiveGroupId = 1 };
        await using var context = CreateContext(dbName, groupContext);
        var today = DateOnly.FromDateTime(DateTime.Today);
        var seriesId = await SeedSeriesAsync(context, 1, today, 1, "1");
        var service = CreateService(context, groupContext, OneShotResolver(), NoMembersRepository(), runwaySize: 3, previewCount: 3);

        var (previewDates, anchorFullyInPast) = await service.PreviewAsync(today, 1, "1", null, TestContext.Current.CancellationToken);
        await service.TopUpAsync(seriesId, TestContext.Current.CancellationToken);
        var materializedDates = await context.Events
            .Where(e => e.SeriesId == seriesId)
            .OrderBy(e => e.Date)
            .Select(e => e.Date)
            .ToListAsync(TestContext.Current.CancellationToken);

        anchorFullyInPast.Should().BeFalse();
        previewDates.Should().BeEquivalentTo(materializedDates);
    }

    private sealed class MutableTestGroupContext : IActiveGroupContext
    {
        public int? ActiveGroupId { get; set; }
    }
}
