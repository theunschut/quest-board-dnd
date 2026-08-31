using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Repository;
using QuestBoard.Repository.Entities;

namespace QuestBoard.UnitTests.Repository;

public class GroupRepositoryTests
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

    // Always seeds BoardType as a real column value on a real row rather than relying on any
    // resolver stub, because AddMemberAsync/RemoveMemberAsync read the group row directly and a
    // stub would leave the test passing against logic that no longer runs.
    private static async Task<GroupEntity> SeedCampaignGroupAsync(QuestBoardContext context, int groupId)
    {
        var group = new GroupEntity { Id = groupId, Name = $"Test Group {groupId}", BoardType = (int)BoardType.Campaign };
        context.Groups.Add(group);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return group;
    }

    private static async Task<GroupEntity> SeedOneShotGroupAsync(QuestBoardContext context, int groupId)
    {
        var group = new GroupEntity { Id = groupId, Name = $"Test Group {groupId}", BoardType = (int)BoardType.OneShot };
        context.Groups.Add(group);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return group;
    }

    private static async Task<EventEntity> SeedEventAsync(QuestBoardContext context, int eventId, int groupId, DateOnly date)
    {
        var evt = new EventEntity { Id = eventId, GroupId = groupId, Title = $"Test Event {eventId}", Date = date };
        context.Events.Add(evt);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return evt;
    }

    private static async Task<UserEntity> SeedUserAsync(QuestBoardContext context, int userId)
    {
        var user = new UserEntity { Id = userId, Name = $"Test User {userId}", Email = $"user{userId}@test.com" };
        context.UserEntities.Add(user);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return user;
    }

    private static async Task SeedMembershipAsync(QuestBoardContext context, int groupId, int userId, GroupRole role = GroupRole.Player)
    {
        context.UserGroups.Add(new UserGroupEntity { GroupId = groupId, UserId = userId, GroupRole = (int)role });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    // -------------------------------------------------------------------
    // AddMemberAsync — campaign backfill
    // -------------------------------------------------------------------

    [Fact]
    public async Task AddMemberAsync_CampaignBoardWithPastPresentAndFutureEvents_BackfillsTodayAndFutureOnly()
    {
        // Arrange
        var dbName = nameof(AddMemberAsync_CampaignBoardWithPastPresentAndFutureEvents_BackfillsTodayAndFutureOnly);
        var today = DateOnly.FromDateTime(DateTime.Today);

        await using (var seedContext = CreateContext(dbName, new MutableTestGroupContext { ActiveGroupId = null }))
        {
            await SeedCampaignGroupAsync(seedContext, groupId: 1);
            await SeedEventAsync(seedContext, eventId: 1, groupId: 1, today.AddDays(-1)); // yesterday — excluded
            await SeedEventAsync(seedContext, eventId: 2, groupId: 1, today); // today — included
            await SeedEventAsync(seedContext, eventId: 3, groupId: 1, today.AddDays(1)); // tomorrow — included
            await SeedUserAsync(seedContext, userId: 201);
        }

        // Act
        await using (var context = CreateContext(dbName, new MutableTestGroupContext { ActiveGroupId = 1 }))
        {
            var repository = new GroupRepository(context, CreateMapper());
            await repository.AddMemberAsync(1, 201, GroupRole.Player, TestContext.Current.CancellationToken);
        }

        // Assert — read back through an unfiltered context so scoping cannot hide a written row
        await using var assertContext = CreateContext(dbName, new MutableTestGroupContext { ActiveGroupId = null });
        var signups = await assertContext.EventSignups
            .IgnoreQueryFilters()
            .Where(es => es.UserId == 201)
            .ToListAsync(TestContext.Current.CancellationToken);

        signups.Should().HaveCount(2);
        signups.Select(s => s.EventId).Should().BeEquivalentTo([2, 3]);
        signups.Should().OnlyContain(s => s.Availability == (int)VoteType.Yes && s.UpdatedAt == null);
    }

    [Fact]
    public async Task AddMemberAsync_OneShotBoardWithFutureEvents_CreatesNoSignupRows()
    {
        // Arrange
        var dbName = nameof(AddMemberAsync_OneShotBoardWithFutureEvents_CreatesNoSignupRows);
        var today = DateOnly.FromDateTime(DateTime.Today);

        await using (var seedContext = CreateContext(dbName, new MutableTestGroupContext { ActiveGroupId = null }))
        {
            await SeedOneShotGroupAsync(seedContext, groupId: 1);
            await SeedEventAsync(seedContext, eventId: 1, groupId: 1, today.AddDays(1));
            await SeedUserAsync(seedContext, userId: 201);
        }

        // Act
        await using (var context = CreateContext(dbName, new MutableTestGroupContext { ActiveGroupId = 1 }))
        {
            var repository = new GroupRepository(context, CreateMapper());
            await repository.AddMemberAsync(1, 201, GroupRole.Player, TestContext.Current.CancellationToken);
        }

        // Assert
        await using var assertContext = CreateContext(dbName, new MutableTestGroupContext { ActiveGroupId = null });
        var signupCount = await assertContext.EventSignups
            .IgnoreQueryFilters()
            .CountAsync(es => es.UserId == 201, TestContext.Current.CancellationToken);

        signupCount.Should().Be(0);
    }

    [Fact]
    public async Task AddMemberAsync_CampaignBoard_DoesNotBackfillEventsOnAnotherBoard()
    {
        // Arrange: a second campaign board holds its own future event
        var dbName = nameof(AddMemberAsync_CampaignBoard_DoesNotBackfillEventsOnAnotherBoard);
        var today = DateOnly.FromDateTime(DateTime.Today);

        await using (var seedContext = CreateContext(dbName, new MutableTestGroupContext { ActiveGroupId = null }))
        {
            await SeedCampaignGroupAsync(seedContext, groupId: 1);
            await SeedCampaignGroupAsync(seedContext, groupId: 2);
            await SeedEventAsync(seedContext, eventId: 1, groupId: 1, today);
            await SeedEventAsync(seedContext, eventId: 2, groupId: 2, today);
            await SeedUserAsync(seedContext, userId: 201);
        }

        // Act
        await using (var context = CreateContext(dbName, new MutableTestGroupContext { ActiveGroupId = 1 }))
        {
            var repository = new GroupRepository(context, CreateMapper());
            await repository.AddMemberAsync(1, 201, GroupRole.Player, TestContext.Current.CancellationToken);
        }

        // Assert
        await using var assertContext = CreateContext(dbName, new MutableTestGroupContext { ActiveGroupId = null });
        var signups = await assertContext.EventSignups
            .IgnoreQueryFilters()
            .Where(es => es.UserId == 201)
            .ToListAsync(TestContext.Current.CancellationToken);

        signups.Should().ContainSingle(s => s.EventId == 1);
    }

    [Fact]
    public async Task AddMemberAsync_BackfillIsUnaffectedByActingCallersSelectedBoard()
    {
        // Arrange: one campaign board with one future event, two prospective members
        var dbName = nameof(AddMemberAsync_BackfillIsUnaffectedByActingCallersSelectedBoard);
        var today = DateOnly.FromDateTime(DateTime.Today);

        await using (var seedContext = CreateContext(dbName, new MutableTestGroupContext { ActiveGroupId = null }))
        {
            await SeedCampaignGroupAsync(seedContext, groupId: 1);
            await SeedEventAsync(seedContext, eventId: 1, groupId: 1, today);
            await SeedUserAsync(seedContext, userId: 201);
            await SeedUserAsync(seedContext, userId: 202);
        }

        // Act: case 1 — the acting caller's active board is an unrelated id
        await using (var context = CreateContext(dbName, new MutableTestGroupContext { ActiveGroupId = 99 }))
        {
            var repository = new GroupRepository(context, CreateMapper());
            await repository.AddMemberAsync(1, 201, GroupRole.Player, TestContext.Current.CancellationToken);
        }

        // Act: case 2 — the acting caller has no active board at all
        await using (var context = CreateContext(dbName, new MutableTestGroupContext { ActiveGroupId = null }))
        {
            var repository = new GroupRepository(context, CreateMapper());
            await repository.AddMemberAsync(1, 202, GroupRole.Player, TestContext.Current.CancellationToken);
        }

        // Assert: both joins backfilled the target board's event regardless of the caller's own
        // selected board — a regression that reintroduced ambient scoping would fail this
        await using var assertContext = CreateContext(dbName, new MutableTestGroupContext { ActiveGroupId = null });
        var signupsUser201 = await assertContext.EventSignups
            .IgnoreQueryFilters()
            .Where(es => es.UserId == 201)
            .ToListAsync(TestContext.Current.CancellationToken);
        var signupsUser202 = await assertContext.EventSignups
            .IgnoreQueryFilters()
            .Where(es => es.UserId == 202)
            .ToListAsync(TestContext.Current.CancellationToken);

        signupsUser201.Should().ContainSingle(s => s.EventId == 1 && s.Availability == (int)VoteType.Yes);
        signupsUser202.Should().ContainSingle(s => s.EventId == 1 && s.Availability == (int)VoteType.Yes);
    }

    [Fact]
    public async Task AddMemberAsync_ExistingMember_ThrowsAndWritesNoSignupRows()
    {
        // Arrange
        var dbName = nameof(AddMemberAsync_ExistingMember_ThrowsAndWritesNoSignupRows);
        var today = DateOnly.FromDateTime(DateTime.Today);

        await using (var seedContext = CreateContext(dbName, new MutableTestGroupContext { ActiveGroupId = null }))
        {
            await SeedCampaignGroupAsync(seedContext, groupId: 1);
            await SeedEventAsync(seedContext, eventId: 1, groupId: 1, today);
            await SeedUserAsync(seedContext, userId: 201);
            await SeedMembershipAsync(seedContext, groupId: 1, userId: 201);
        }

        // Act
        Func<Task> act;
        await using (var context = CreateContext(dbName, new MutableTestGroupContext { ActiveGroupId = 1 }))
        {
            var repository = new GroupRepository(context, CreateMapper());
            act = async () => await repository.AddMemberAsync(1, 201, GroupRole.Player, TestContext.Current.CancellationToken);

            // Assert: the pre-existing race handling still holds
            await act.Should().ThrowAsync<InvalidOperationException>();
        }

        // Assert: no half state — the failed join wrote no signup rows
        await using var assertContext = CreateContext(dbName, new MutableTestGroupContext { ActiveGroupId = null });
        var signupCount = await assertContext.EventSignups
            .IgnoreQueryFilters()
            .CountAsync(es => es.UserId == 201, TestContext.Current.CancellationToken);

        signupCount.Should().Be(0);
    }

    // -------------------------------------------------------------------
    // RemoveMemberAsync — leave cleanup
    // -------------------------------------------------------------------

    [Fact]
    public async Task RemoveMemberAsync_RemovesAllSignupsIncludingPastAndAnswered_AndRemovesMembership()
    {
        // Arrange: a past-event signup and an already-answered signup, both belonging to the leaver
        var dbName = nameof(RemoveMemberAsync_RemovesAllSignupsIncludingPastAndAnswered_AndRemovesMembership);
        var today = DateOnly.FromDateTime(DateTime.Today);

        await using (var seedContext = CreateContext(dbName, new MutableTestGroupContext { ActiveGroupId = null }))
        {
            await SeedCampaignGroupAsync(seedContext, groupId: 1);
            await SeedEventAsync(seedContext, eventId: 1, groupId: 1, today.AddDays(-5));
            await SeedEventAsync(seedContext, eventId: 2, groupId: 1, today.AddDays(5));
            await SeedUserAsync(seedContext, userId: 201);
            await SeedMembershipAsync(seedContext, groupId: 1, userId: 201);
            seedContext.EventSignups.Add(new EventSignupEntity { EventId = 1, UserId = 201, Availability = (int)VoteType.Yes, UpdatedAt = DateTime.UtcNow });
            seedContext.EventSignups.Add(new EventSignupEntity { EventId = 2, UserId = 201, Availability = (int)VoteType.Yes });
            await seedContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Act
        await using (var context = CreateContext(dbName, new MutableTestGroupContext { ActiveGroupId = 1 }))
        {
            var repository = new GroupRepository(context, CreateMapper());
            await repository.RemoveMemberAsync(1, 201, TestContext.Current.CancellationToken);
        }

        // Assert
        await using var assertContext = CreateContext(dbName, new MutableTestGroupContext { ActiveGroupId = null });
        var remainingSignups = await assertContext.EventSignups
            .IgnoreQueryFilters()
            .CountAsync(es => es.UserId == 201, TestContext.Current.CancellationToken);
        var stillAMember = await assertContext.UserGroups
            .AnyAsync(ug => ug.UserId == 201 && ug.GroupId == 1, TestContext.Current.CancellationToken);

        remainingSignups.Should().Be(0);
        stillAMember.Should().BeFalse();
    }

    [Fact]
    public async Task RemoveMemberAsync_LeavesSignupsOnAnotherBoardUntouched()
    {
        // Arrange: the leaving member also holds a signup on a different board
        var dbName = nameof(RemoveMemberAsync_LeavesSignupsOnAnotherBoardUntouched);
        var today = DateOnly.FromDateTime(DateTime.Today);

        await using (var seedContext = CreateContext(dbName, new MutableTestGroupContext { ActiveGroupId = null }))
        {
            await SeedCampaignGroupAsync(seedContext, groupId: 1);
            await SeedCampaignGroupAsync(seedContext, groupId: 2);
            await SeedEventAsync(seedContext, eventId: 1, groupId: 1, today);
            await SeedEventAsync(seedContext, eventId: 2, groupId: 2, today);
            await SeedUserAsync(seedContext, userId: 201);
            await SeedMembershipAsync(seedContext, groupId: 1, userId: 201);
            await SeedMembershipAsync(seedContext, groupId: 2, userId: 201);
            seedContext.EventSignups.Add(new EventSignupEntity { EventId = 1, UserId = 201, Availability = (int)VoteType.Yes });
            seedContext.EventSignups.Add(new EventSignupEntity { EventId = 2, UserId = 201, Availability = (int)VoteType.Yes });
            await seedContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Act: leave board 1 only
        await using (var context = CreateContext(dbName, new MutableTestGroupContext { ActiveGroupId = 1 }))
        {
            var repository = new GroupRepository(context, CreateMapper());
            await repository.RemoveMemberAsync(1, 201, TestContext.Current.CancellationToken);
        }

        // Assert: board 1's signup is gone, board 2's signup survives
        await using var assertContext = CreateContext(dbName, new MutableTestGroupContext { ActiveGroupId = null });
        var remainingSignups = await assertContext.EventSignups
            .IgnoreQueryFilters()
            .Where(es => es.UserId == 201)
            .ToListAsync(TestContext.Current.CancellationToken);

        remainingSignups.Should().ContainSingle(s => s.EventId == 2);
    }

    [Fact]
    public async Task RemoveMemberAsync_LeavesOtherMembersSignupsOnSameBoardUntouched()
    {
        // Arrange: two members of the same board, each with their own signup on the same event
        var dbName = nameof(RemoveMemberAsync_LeavesOtherMembersSignupsOnSameBoardUntouched);
        var today = DateOnly.FromDateTime(DateTime.Today);

        await using (var seedContext = CreateContext(dbName, new MutableTestGroupContext { ActiveGroupId = null }))
        {
            await SeedCampaignGroupAsync(seedContext, groupId: 1);
            await SeedEventAsync(seedContext, eventId: 1, groupId: 1, today);
            await SeedUserAsync(seedContext, userId: 201);
            await SeedUserAsync(seedContext, userId: 202);
            await SeedMembershipAsync(seedContext, groupId: 1, userId: 201);
            await SeedMembershipAsync(seedContext, groupId: 1, userId: 202);
            seedContext.EventSignups.Add(new EventSignupEntity { EventId = 1, UserId = 201, Availability = (int)VoteType.Yes });
            seedContext.EventSignups.Add(new EventSignupEntity { EventId = 1, UserId = 202, Availability = (int)VoteType.Yes });
            await seedContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Act: only user 201 leaves
        await using (var context = CreateContext(dbName, new MutableTestGroupContext { ActiveGroupId = 1 }))
        {
            var repository = new GroupRepository(context, CreateMapper());
            await repository.RemoveMemberAsync(1, 201, TestContext.Current.CancellationToken);
        }

        // Assert
        await using var assertContext = CreateContext(dbName, new MutableTestGroupContext { ActiveGroupId = null });
        var user201Signups = await assertContext.EventSignups
            .IgnoreQueryFilters()
            .CountAsync(es => es.UserId == 201, TestContext.Current.CancellationToken);
        var user202Signups = await assertContext.EventSignups
            .IgnoreQueryFilters()
            .CountAsync(es => es.UserId == 202, TestContext.Current.CancellationToken);

        user201Signups.Should().Be(0);
        user202Signups.Should().Be(1);
    }

    [Fact]
    public async Task RemoveMemberAsync_NonMember_RemovesNothingAndThrowsNothing()
    {
        // Arrange: user 201 exists but never joined the group
        var dbName = nameof(RemoveMemberAsync_NonMember_RemovesNothingAndThrowsNothing);
        var today = DateOnly.FromDateTime(DateTime.Today);

        await using (var seedContext = CreateContext(dbName, new MutableTestGroupContext { ActiveGroupId = null }))
        {
            await SeedCampaignGroupAsync(seedContext, groupId: 1);
            await SeedEventAsync(seedContext, eventId: 1, groupId: 1, today);
            await SeedUserAsync(seedContext, userId: 201);
        }

        // Act & Assert
        await using (var context = CreateContext(dbName, new MutableTestGroupContext { ActiveGroupId = 1 }))
        {
            var repository = new GroupRepository(context, CreateMapper());
            var act = async () => await repository.RemoveMemberAsync(1, 201, TestContext.Current.CancellationToken);
            await act.Should().NotThrowAsync();
        }

        await using var assertContext = CreateContext(dbName, new MutableTestGroupContext { ActiveGroupId = null });
        var signupCount = await assertContext.EventSignups.IgnoreQueryFilters().CountAsync(TestContext.Current.CancellationToken);
        signupCount.Should().Be(0);
    }

    private sealed class TestActiveGroupContext : IActiveGroupContext
    {
        public int? ActiveGroupId => 1;
    }

    private sealed class MutableTestGroupContext : IActiveGroupContext
    {
        public int? ActiveGroupId { get; set; }
    }
}
