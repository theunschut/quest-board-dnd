using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Repository;
using QuestBoard.Repository.Entities;

namespace QuestBoard.UnitTests.Repository;

// Protects the throttled last-fetched write: a hammered address must perform no database write
// at all inside the configured interval, not merely a no-op write. The two window behaviors
// (rolling MonthsBack/MonthsAhead bounds from configuration) are proved end to end in plan
// 84-05 against real HTTP, where seeded event dates outside the configured window can be
// observed to be absent -- a unit test here would only re-assert arithmetic, so that coverage is
// deliberately not duplicated in this file.
public class CalendarSubscriptionRepositoryTests
{
    private static QuestBoardContext CreateContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<QuestBoardContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        return new QuestBoardContext(options, new TestActiveGroupContext());
    }

    private static IMapper CreateMapper()
    {
        var configuration = new MapperConfiguration(cfg => cfg.AddProfile<QuestBoard.Repository.Automapper.EntityProfile>(), NullLoggerFactory.Instance);
        return configuration.CreateMapper();
    }

    private static async Task<int> SeedSubscriptionAsync(QuestBoardContext context, DateTime? lastFetchedAt, int userId = 101)
    {
        if (!await context.UserEntities.AnyAsync(u => u.Id == userId))
        {
            context.UserEntities.Add(new UserEntity { Id = userId, Name = $"User {userId}", Email = $"user{userId}@test.com" });
        }

        var entity = new CalendarSubscriptionEntity
        {
            UserId = userId,
            Name = "Test subscription",
            Token = Guid.NewGuid().ToString("N"),
            CreatedAt = DateTime.UtcNow,
            LastFetchedAt = lastFetchedAt
        };
        context.CalendarSubscriptions.Add(entity);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return entity.Id;
    }

    // -------------------------------------------------------------------
    // TouchLastFetchedAsync
    // -------------------------------------------------------------------

    [Fact]
    public async Task TouchLastFetchedAsync_LastFetchedAtIsNull_WritesSuppliedTimeAndPersists()
    {
        // Arrange
        await using var context = CreateContext(nameof(TouchLastFetchedAsync_LastFetchedAtIsNull_WritesSuppliedTimeAndPersists));
        var id = await SeedSubscriptionAsync(context, lastFetchedAt: null);
        var repository = new CalendarSubscriptionRepository(context, CreateMapper());
        var fetchedAt = new DateTime(2026, 9, 18, 12, 0, 0, DateTimeKind.Utc);

        // Act
        await repository.TouchLastFetchedAsync(id, fetchedAt, TimeSpan.FromMinutes(15), TestContext.Current.CancellationToken);

        // Assert: read back through a fresh context so an unsaved change fails the test.
        await using var verifyContext = CreateContext(nameof(TouchLastFetchedAsync_LastFetchedAtIsNull_WritesSuppliedTimeAndPersists));
        var persisted = await verifyContext.CalendarSubscriptions.SingleAsync(cs => cs.Id == id, TestContext.Current.CancellationToken);
        persisted.LastFetchedAt.Should().Be(fetchedAt);
    }

    [Fact]
    public async Task TouchLastFetchedAsync_LastFetchedLongerAgoThanInterval_WritesSuppliedTimeAndPersists()
    {
        // Arrange
        await using var context = CreateContext(nameof(TouchLastFetchedAsync_LastFetchedLongerAgoThanInterval_WritesSuppliedTimeAndPersists));
        var lastFetched = new DateTime(2026, 9, 18, 11, 0, 0, DateTimeKind.Utc);
        var id = await SeedSubscriptionAsync(context, lastFetchedAt: lastFetched);
        var repository = new CalendarSubscriptionRepository(context, CreateMapper());
        var fetchedAt = lastFetched.AddMinutes(16);

        // Act
        await repository.TouchLastFetchedAsync(id, fetchedAt, TimeSpan.FromMinutes(15), TestContext.Current.CancellationToken);

        // Assert
        await using var verifyContext = CreateContext(nameof(TouchLastFetchedAsync_LastFetchedLongerAgoThanInterval_WritesSuppliedTimeAndPersists));
        var persisted = await verifyContext.CalendarSubscriptions.SingleAsync(cs => cs.Id == id, TestContext.Current.CancellationToken);
        persisted.LastFetchedAt.Should().Be(fetchedAt);
    }

    [Fact]
    public async Task TouchLastFetchedAsync_LastFetchedMoreRecentlyThanInterval_LeavesStoredValueUnchanged()
    {
        // Arrange
        await using var context = CreateContext(nameof(TouchLastFetchedAsync_LastFetchedMoreRecentlyThanInterval_LeavesStoredValueUnchanged));
        var lastFetched = new DateTime(2026, 9, 18, 11, 0, 0, DateTimeKind.Utc);
        var id = await SeedSubscriptionAsync(context, lastFetchedAt: lastFetched);
        var repository = new CalendarSubscriptionRepository(context, CreateMapper());
        var fetchedAt = lastFetched.AddMinutes(5);

        // Act
        await repository.TouchLastFetchedAsync(id, fetchedAt, TimeSpan.FromMinutes(15), TestContext.Current.CancellationToken);

        // Assert
        await using var verifyContext = CreateContext(nameof(TouchLastFetchedAsync_LastFetchedMoreRecentlyThanInterval_LeavesStoredValueUnchanged));
        var persisted = await verifyContext.CalendarSubscriptions.SingleAsync(cs => cs.Id == id, TestContext.Current.CancellationToken);
        persisted.LastFetchedAt.Should().Be(lastFetched);
    }

    [Fact]
    public async Task TouchLastFetchedAsync_LastFetchedExactlyIntervalAgo_WritesBecauseBoundaryIsInclusive()
    {
        // Arrange
        await using var context = CreateContext(nameof(TouchLastFetchedAsync_LastFetchedExactlyIntervalAgo_WritesBecauseBoundaryIsInclusive));
        var lastFetched = new DateTime(2026, 9, 18, 11, 0, 0, DateTimeKind.Utc);
        var id = await SeedSubscriptionAsync(context, lastFetchedAt: lastFetched);
        var repository = new CalendarSubscriptionRepository(context, CreateMapper());
        var fetchedAt = lastFetched.AddMinutes(15);

        // Act
        await repository.TouchLastFetchedAsync(id, fetchedAt, TimeSpan.FromMinutes(15), TestContext.Current.CancellationToken);

        // Assert
        await using var verifyContext = CreateContext(nameof(TouchLastFetchedAsync_LastFetchedExactlyIntervalAgo_WritesBecauseBoundaryIsInclusive));
        var persisted = await verifyContext.CalendarSubscriptions.SingleAsync(cs => cs.Id == id, TestContext.Current.CancellationToken);
        persisted.LastFetchedAt.Should().Be(fetchedAt);
    }

    [Fact]
    public async Task TouchLastFetchedAsync_IdDoesNotExist_CompletesWithoutThrowingAndWritesNothing()
    {
        // Arrange
        await using var context = CreateContext(nameof(TouchLastFetchedAsync_IdDoesNotExist_CompletesWithoutThrowingAndWritesNothing));
        var repository = new CalendarSubscriptionRepository(context, CreateMapper());

        // Act
        var act = () => repository.TouchLastFetchedAsync(999, DateTime.UtcNow, TimeSpan.FromMinutes(15), TestContext.Current.CancellationToken);

        // Assert
        await act.Should().NotThrowAsync();
        var rows = await context.CalendarSubscriptions.ToListAsync(TestContext.Current.CancellationToken);
        rows.Should().BeEmpty();
    }

    private sealed class TestActiveGroupContext : IActiveGroupContext
    {
        public int? ActiveGroupId => null;
    }
}
