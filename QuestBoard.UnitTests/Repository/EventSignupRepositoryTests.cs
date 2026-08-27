using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Repository;
using QuestBoard.Repository.Entities;

namespace QuestBoard.UnitTests.Repository;

// This class protects the one rule the rest of the feature leans on: a null answered
// timestamp means an automatic pass created the row, and a non-null one means a person
// deliberately chose it - including on the very click that created the row. It also
// protects the data-tier half of the two-layer defence against writing a signup against
// an event that is not on the caller's board.
public class EventSignupRepositoryTests
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

    // Seeds the minimal Group/User/Event graph a signup needs.
    private static async Task SeedEventAndUsersAsync(QuestBoardContext context, int eventId, int[] userIds, int groupId = 1)
    {
        if (!await context.Groups.AnyAsync(g => g.Id == groupId))
        {
            context.Groups.Add(new GroupEntity { Id = groupId, Name = $"Test Group {groupId}" });
        }

        foreach (var userId in userIds)
        {
            if (!await context.UserEntities.AnyAsync(u => u.Id == userId))
            {
                context.UserEntities.Add(new UserEntity { Id = userId, Name = $"User {userId}", Email = $"user{userId}@test.com" });
            }
        }

        if (!await context.Events.AnyAsync(e => e.Id == eventId))
        {
            context.Events.Add(new EventEntity
            {
                Id = eventId,
                Title = "Test Event",
                Date = DateOnly.FromDateTime(DateTime.Today),
                GroupId = groupId
            });
        }

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    // -------------------------------------------------------------------
    // SetAvailabilityAsync
    // -------------------------------------------------------------------

    [Fact]
    public async Task SetAvailabilityAsync_NoExistingRow_CreatesRowWithAvailabilityAndNonNullUpdatedAt()
    {
        // Arrange
        await using var context = CreateContext(nameof(SetAvailabilityAsync_NoExistingRow_CreatesRowWithAvailabilityAndNonNullUpdatedAt));
        await SeedEventAndUsersAsync(context, eventId: 1, userIds: [101]);
        var repository = new EventSignupRepository(context, CreateMapper());

        // Act
        await repository.SetAvailabilityAsync(1, 101, VoteType.Yes, TestContext.Current.CancellationToken);

        // Assert
        var persisted = await context.EventSignups.SingleAsync(es => es.EventId == 1 && es.UserId == 101, TestContext.Current.CancellationToken);
        persisted.Availability.Should().Be((int)VoteType.Yes);
        persisted.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task SetAvailabilityAsync_ExistingRow_UpdatesAvailabilityAndAdvancesUpdatedAtWithoutDuplicating()
    {
        // Arrange
        await using var context = CreateContext(nameof(SetAvailabilityAsync_ExistingRow_UpdatesAvailabilityAndAdvancesUpdatedAtWithoutDuplicating));
        await SeedEventAndUsersAsync(context, eventId: 1, userIds: [101]);
        var repository = new EventSignupRepository(context, CreateMapper());
        await repository.SetAvailabilityAsync(1, 101, VoteType.Maybe, TestContext.Current.CancellationToken);
        var firstUpdatedAt = (await context.EventSignups.SingleAsync(es => es.EventId == 1 && es.UserId == 101, TestContext.Current.CancellationToken)).UpdatedAt;

        // Act
        await repository.SetAvailabilityAsync(1, 101, VoteType.Yes, TestContext.Current.CancellationToken);

        // Assert
        var rows = await context.EventSignups.Where(es => es.EventId == 1 && es.UserId == 101).ToListAsync(TestContext.Current.CancellationToken);
        rows.Should().HaveCount(1);
        rows[0].Availability.Should().Be((int)VoteType.Yes);
        rows[0].UpdatedAt.Should().NotBeNull();
        rows[0].UpdatedAt.Should().BeOnOrAfter(firstUpdatedAt!.Value);
    }

    [Fact]
    public async Task SetAvailabilityAsync_ExistingRow_LeavesCreatedAtUnchanged()
    {
        // Arrange
        await using var context = CreateContext(nameof(SetAvailabilityAsync_ExistingRow_LeavesCreatedAtUnchanged));
        await SeedEventAndUsersAsync(context, eventId: 1, userIds: [101]);
        var repository = new EventSignupRepository(context, CreateMapper());
        await repository.SetAvailabilityAsync(1, 101, VoteType.Maybe, TestContext.Current.CancellationToken);
        var createdAt = (await context.EventSignups.SingleAsync(es => es.EventId == 1 && es.UserId == 101, TestContext.Current.CancellationToken)).CreatedAt;

        // Act
        await repository.SetAvailabilityAsync(1, 101, VoteType.Yes, TestContext.Current.CancellationToken);

        // Assert
        var persisted = await context.EventSignups.SingleAsync(es => es.EventId == 1 && es.UserId == 101, TestContext.Current.CancellationToken);
        persisted.CreatedAt.Should().Be(createdAt);
    }

    [Fact]
    public async Task SetAvailabilityAsync_EventNotVisibleThroughActiveBoard_ThrowsAndWritesNoRow()
    {
        // Arrange: the event lives under group 2, but the repository's context resolves the
        // active board to group 1.
        var seedGroupContext = new MutableTestGroupContext { ActiveGroupId = null };
        await using (var seedContext = CreateContext(nameof(SetAvailabilityAsync_EventNotVisibleThroughActiveBoard_ThrowsAndWritesNoRow), seedGroupContext))
        {
            await SeedEventAndUsersAsync(seedContext, eventId: 1, userIds: [101], groupId: 2);
        }

        var activeGroupContext = new MutableTestGroupContext { ActiveGroupId = 1 };
        await using var context = CreateContext(nameof(SetAvailabilityAsync_EventNotVisibleThroughActiveBoard_ThrowsAndWritesNoRow), activeGroupContext);
        var repository = new EventSignupRepository(context, CreateMapper());

        // Act
        var act = () => repository.SetAvailabilityAsync(1, 101, VoteType.Yes, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();

        var unfilteredContext = new MutableTestGroupContext { ActiveGroupId = null };
        await using var verifyContext = CreateContext(nameof(SetAvailabilityAsync_EventNotVisibleThroughActiveBoard_ThrowsAndWritesNoRow), unfilteredContext);
        var rows = await verifyContext.EventSignups.ToListAsync(TestContext.Current.CancellationToken);
        rows.Should().BeEmpty();
    }

    [Fact]
    public async Task AutomaticPassRow_ReadsAsNotAnswered_UntilSetAvailabilityAsyncTouchesIt()
    {
        // Arrange: insert a row directly with UpdatedAt left at null, standing in for a row an
        // automatic signup pass created rather than a person.
        await using var context = CreateContext(nameof(AutomaticPassRow_ReadsAsNotAnswered_UntilSetAvailabilityAsyncTouchesIt));
        await SeedEventAndUsersAsync(context, eventId: 1, userIds: [101]);
        context.EventSignups.Add(new EventSignupEntity
        {
            EventId = 1,
            UserId = 101,
            Availability = (int)VoteType.Yes,
            UpdatedAt = null
        });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var repository = new EventSignupRepository(context, CreateMapper());

        // Act
        var beforeTouch = await repository.GetRosterForEventAsync(1, TestContext.Current.CancellationToken);

        // Assert: the automatic-pass row reads as not-answered
        beforeTouch.Should().ContainSingle();
        beforeTouch[0].HasAnswered.Should().BeFalse();

        // Act: a person now sets their availability, touching the same row
        await repository.SetAvailabilityAsync(1, 101, VoteType.No, TestContext.Current.CancellationToken);
        var afterTouch = await repository.GetRosterForEventAsync(1, TestContext.Current.CancellationToken);

        // Assert: the same row now reads as answered
        afterTouch.Should().ContainSingle();
        afterTouch[0].Availability.Should().Be(VoteType.No);
        afterTouch[0].HasAnswered.Should().BeTrue();
    }

    // -------------------------------------------------------------------
    // WithdrawAsync
    // -------------------------------------------------------------------

    [Fact]
    public async Task WithdrawAsync_RowExists_RemovesItAndReturnsTrue()
    {
        // Arrange
        await using var context = CreateContext(nameof(WithdrawAsync_RowExists_RemovesItAndReturnsTrue));
        await SeedEventAndUsersAsync(context, eventId: 1, userIds: [101]);
        var repository = new EventSignupRepository(context, CreateMapper());
        await repository.SetAvailabilityAsync(1, 101, VoteType.Yes, TestContext.Current.CancellationToken);

        // Act
        var result = await repository.WithdrawAsync(1, 101, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeTrue();
        var rows = await context.EventSignups.Where(es => es.EventId == 1 && es.UserId == 101).ToListAsync(TestContext.Current.CancellationToken);
        rows.Should().BeEmpty();
    }

    [Fact]
    public async Task WithdrawAsync_NoRowExists_ReturnsFalseAndRemovesNothing()
    {
        // Arrange
        await using var context = CreateContext(nameof(WithdrawAsync_NoRowExists_ReturnsFalseAndRemovesNothing));
        await SeedEventAndUsersAsync(context, eventId: 1, userIds: [101]);
        var repository = new EventSignupRepository(context, CreateMapper());

        // Act
        var result = await repository.WithdrawAsync(1, 101, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeFalse();
        var rows = await context.EventSignups.ToListAsync(TestContext.Current.CancellationToken);
        rows.Should().BeEmpty();
    }

    // -------------------------------------------------------------------
    // GetRosterForEventAsync
    // -------------------------------------------------------------------

    [Fact]
    public async Task GetRosterForEventAsync_ReturnsAllRowsWithUserNamePopulated_OrderedAlphabetically_RegardlessOfHasAnswered()
    {
        // Arrange
        await using var context = CreateContext(nameof(GetRosterForEventAsync_ReturnsAllRowsWithUserNamePopulated_OrderedAlphabetically_RegardlessOfHasAnswered));
        context.Groups.Add(new GroupEntity { Id = 1, Name = "Test Group 1" });
        context.UserEntities.Add(new UserEntity { Id = 201, Name = "Zed", Email = "zed@test.com" });
        context.UserEntities.Add(new UserEntity { Id = 202, Name = "Anna", Email = "anna@test.com" });
        context.Events.Add(new EventEntity { Id = 1, Title = "Test Event", Date = DateOnly.FromDateTime(DateTime.Today), GroupId = 1 });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        context.EventSignups.Add(new EventSignupEntity { EventId = 1, UserId = 201, Availability = (int)VoteType.Yes, UpdatedAt = DateTime.UtcNow });
        context.EventSignups.Add(new EventSignupEntity { EventId = 1, UserId = 202, Availability = (int)VoteType.No, UpdatedAt = null });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var repository = new EventSignupRepository(context, CreateMapper());

        // Act
        var roster = await repository.GetRosterForEventAsync(1, TestContext.Current.CancellationToken);

        // Assert
        roster.Should().HaveCount(2);
        roster[0].UserName.Should().Be("Anna");
        roster[1].UserName.Should().Be("Zed");
        roster.Should().Contain(r => r.HasAnswered == false);
        roster.Should().Contain(r => r.HasAnswered == true);
    }

    [Fact]
    public async Task GetRosterForEventAsync_ReturnsRowsForRequestedEventOnly()
    {
        // Arrange
        await using var context = CreateContext(nameof(GetRosterForEventAsync_ReturnsRowsForRequestedEventOnly));
        context.Groups.Add(new GroupEntity { Id = 1, Name = "Test Group 1" });
        context.UserEntities.Add(new UserEntity { Id = 101, Name = "User 101", Email = "user101@test.com" });
        context.Events.Add(new EventEntity { Id = 1, Title = "Event One", Date = DateOnly.FromDateTime(DateTime.Today), GroupId = 1 });
        context.Events.Add(new EventEntity { Id = 2, Title = "Event Two", Date = DateOnly.FromDateTime(DateTime.Today), GroupId = 1 });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        context.EventSignups.Add(new EventSignupEntity { EventId = 1, UserId = 101, Availability = (int)VoteType.Yes, UpdatedAt = DateTime.UtcNow });
        context.EventSignups.Add(new EventSignupEntity { EventId = 2, UserId = 101, Availability = (int)VoteType.No, UpdatedAt = DateTime.UtcNow });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var repository = new EventSignupRepository(context, CreateMapper());

        // Act
        var roster = await repository.GetRosterForEventAsync(1, TestContext.Current.CancellationToken);

        // Assert
        roster.Should().ContainSingle();
        roster[0].EventId.Should().Be(1);
    }

    // Tests using the single-arg CreateContext(databaseName) overload seed and query through the
    // same context instance, so ActiveGroupId must be a concrete value the group-scoped filters
    // let through — matching SeedEventAndUsersAsync's own default groupId of 1 — rather than null,
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
