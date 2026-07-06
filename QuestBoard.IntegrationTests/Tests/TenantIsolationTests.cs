using QuestBoard.Domain.Extensions;
using QuestBoard.IntegrationTests.Helpers;

namespace QuestBoard.IntegrationTests.Tests;

/// <summary>
/// Cross-group tenant isolation tests.
/// Proves that the EF Core HasQueryFilter correctly scopes quests to the active group.
/// </summary>
public class TenantIsolationTests(WebApplicationFactoryBase factory)
    : IClassFixture<WebApplicationFactoryBase>, IAsyncLifetime
{
    // IAsyncLifetime — reset singleton group context after each test class run so that
    // test state does not bleed into subsequently-executed test classes.
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public ValueTask DisposeAsync()
    {
        factory.TestGroupContext.ActiveGroupId = 1;
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// A quest seeded with GroupId=2 must NOT appear in the response when the active group is 1.
    /// The quest board moved from / (now the public landing page, no auth) to
    /// /quests (authenticated) — use an authenticated client against /quests so this test
    /// still exercises the query-filter behavior rather than trivially passing against a
    /// landing page that never shows quest content for any group.
    /// </summary>
    [Fact]
    public async Task GroupFilter_HidesQuestFromOtherGroup()
    {
        // Arrange — clean slate with roles and default Group 1 seeded
        await TestDataHelper.ClearDatabaseAsync(factory.Services);

        // Seed Group 2 and a DM user, then add a quest belonging to Group 2
        var dm = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "isolationdm1", "isolationdm1@example.com");

        await using var ctx = factory.Database.CreateContext(); // ActiveGroupId = null (sees all for seeding)
        ctx.Groups.Add(new GroupEntity { Id = 2, Name = "OtherGroup", CreatedAt = DateTime.UtcNow });
        ctx.Quests.Add(new QuestEntity
        {
            Title = "GroupTwoQuest",
            Description = "This quest belongs to Group 2 and must be hidden from Group 1 views.",
            GroupId = 2,
            DungeonMasterId = dm.Id,
            ChallengeRating = 3,
            TotalPlayerCount = 4,
            CreatedAt = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act — request the quest board (authenticated) with the singleton stub scoped to Group 1
        factory.TestGroupContext.ActiveGroupId = 1;
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "isolationviewer1", "isolationviewer1@example.com");
        var response = await client.GetAsync("/quests", TestContext.Current.CancellationToken);

        // Assert — the Group-2 quest must not appear in the response body
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().NotContain("GroupTwoQuest");
    }

    /// <summary>
    /// A quest seeded with GroupId=1 MUST appear in the response when the active group is 1.
    /// The quest board moved from / to /quests (authenticated) — see note on
    /// GroupFilter_HidesQuestFromOtherGroup above.
    /// </summary>
    [Fact]
    public async Task GroupFilter_ShowsQuestFromSameGroup()
    {
        // Arrange — clean slate with roles and default Group 1 seeded
        await TestDataHelper.ClearDatabaseAsync(factory.Services);

        var dm = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "isolationdm2", "isolationdm2@example.com");

        await using var ctx = factory.Database.CreateContext(); // ActiveGroupId = null (sees all for seeding)
        ctx.Quests.Add(new QuestEntity
        {
            Title = "GroupOneQuest",
            Description = "This quest belongs to Group 1 and must be visible for Group 1 views.",
            GroupId = 1,
            DungeonMasterId = dm.Id,
            ChallengeRating = 3,
            TotalPlayerCount = 4,
            CreatedAt = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act — request the quest board (authenticated) with the singleton stub scoped to Group 1
        factory.TestGroupContext.ActiveGroupId = 1;
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "isolationviewer2", "isolationviewer2@example.com");
        var response = await client.GetAsync("/quests", TestContext.Current.CancellationToken);

        // Assert — the Group-1 quest IS returned in the response body
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().Contain("GroupOneQuest");
    }

    /// <summary>
    /// When ActiveGroupId is null on the TestDatabase context, the fail-closed query filter
    /// returns zero rows rather than every group's rows — a null ActiveGroupId must never
    /// leak cross-tenant data, even from a direct DbContext query.
    /// </summary>
    [Fact]
    public async Task GroupFilter_NullGroupIdShowsNoGroups()
    {
        // Arrange — clean slate with roles and default Group 1 seeded
        await TestDataHelper.ClearDatabaseAsync(factory.Services);

        var dm = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "isolationdm3", "isolationdm3@example.com");

        await using (var ctx = factory.Database.CreateContext()) // ActiveGroupId = null (sees all)
        {
            ctx.Groups.Add(new GroupEntity { Id = 2, Name = "OtherGroup", CreatedAt = DateTime.UtcNow });
            ctx.Quests.Add(new QuestEntity
            {
                Title = "GroupOneVisible",
                Description = "Quest in Group 1.",
                GroupId = 1,
                DungeonMasterId = dm.Id,
                ChallengeRating = 3,
                TotalPlayerCount = 4,
                CreatedAt = DateTime.UtcNow
            });
            ctx.Quests.Add(new QuestEntity
            {
                Title = "GroupTwoVisible",
                Description = "Quest in Group 2.",
                GroupId = 2,
                DungeonMasterId = dm.Id,
                ChallengeRating = 3,
                TotalPlayerCount = 4,
                CreatedAt = DateTime.UtcNow
            });
            await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Act — query via TestDatabase.CreateContext() which uses MutableGroupContext { ActiveGroupId = null }
        // This exercises the fail-closed predicate in HasQueryFilter directly.
        await using var readCtx = factory.Database.CreateContext();
        var allQuests = readCtx.Quests.ToList();

        // Assert — neither group's quests are visible when ActiveGroupId is null (fail-closed)
        allQuests.Should().NotContain(q => q.Title == "GroupOneVisible",
            because: "null ActiveGroupId must not leak Group 1 quests");
        allQuests.Should().NotContain(q => q.Title == "GroupTwoVisible",
            because: "null ActiveGroupId must not leak Group 2 quests");
    }

    /// <summary>
    /// A quest seeded with GroupId=1 must NOT appear when the active group is set to a
    /// non-existent group (999). Proves the query filter returns empty rather than
    /// leaking another group's data when ActiveGroupId points at nothing.
    /// </summary>
    [Fact]
    public async Task GroupFilter_NonExistentGroup_ReturnsEmpty()
    {
        // Arrange — clean slate with roles and default Group 1 seeded
        await TestDataHelper.ClearDatabaseAsync(factory.Services);

        var dm = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "isolationdm4", "isolationdm4@example.com");

        await using var ctx = factory.Database.CreateContext(); // ActiveGroupId = null (sees all for seeding)
        ctx.Quests.Add(new QuestEntity
        {
            Title = "OnlyGroupOneQuest",
            Description = "This quest belongs to Group 1 and must be hidden when scoped to a non-existent group.",
            GroupId = 1,
            DungeonMasterId = dm.Id,
            ChallengeRating = 3,
            TotalPlayerCount = 4,
            CreatedAt = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act — request the quest board (authenticated) with the singleton stub scoped to a
        // group that does not exist in the database
        factory.TestGroupContext.ActiveGroupId = 999;
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "isolationviewer4", "isolationviewer4@example.com");
        var response = await client.GetAsync("/quests", TestContext.Current.CancellationToken);

        // Assert — the Group-1 quest must not appear for a non-existent active group
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().NotContain("OnlyGroupOneQuest");
    }

    /// <summary>
    /// Companion to the ActiveGroupContextExtensionsTests unit test: proves RequireActiveGroupId()
    /// throws for a null context and returns the value for a set context, without touching the
    /// existing null-see-all query filter behavior proven above.
    /// </summary>
    [Fact]
    public void RequireActiveGroupId_NullContext_Throws()
    {
        // Arrange — null ActiveGroupId (the SuperAdmin/seeding "see all" scope)
        factory.TestGroupContext.ActiveGroupId = null;

        // Act
        var act = () => factory.TestGroupContext.RequireActiveGroupId();

        // Assert — the guard fails fast rather than silently proceeding with no group scope
        act.Should().Throw<InvalidOperationException>();

        // Arrange — a concrete group is set
        factory.TestGroupContext.ActiveGroupId = 1;

        // Act
        var result = factory.TestGroupContext.RequireActiveGroupId();

        // Assert
        result.Should().Be(1);
    }
}
