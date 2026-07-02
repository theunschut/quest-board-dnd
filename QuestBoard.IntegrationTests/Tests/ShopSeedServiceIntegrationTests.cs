using QuestBoard.Domain.Interfaces;
using QuestBoard.IntegrationTests.Helpers;

namespace QuestBoard.IntegrationTests.Tests;

/// <summary>
/// Proves ShopSeedService.SeedBasicEquipmentAsync correctly assigns GroupId to seeded items,
/// so they are visible under the real EF Core group query filter and are not silently
/// re-seeded as duplicates on every restart.
/// </summary>
public class ShopSeedServiceIntegrationTests(WebApplicationFactoryBase factory)
    : IClassFixture<WebApplicationFactoryBase>, IAsyncLifetime
{
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public ValueTask DisposeAsync()
    {
        factory.TestGroupContext.ActiveGroupId = 1;
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task SeedBasicEquipmentAsync_SeededItemsAreVisibleUnderTheirOwnGroupOnly()
    {
        // Arrange — clean slate with roles and default Group 1 seeded
        await TestDataHelper.ClearDatabaseAsync(factory.Services);

        var dm = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "shopseeddm1", "shopseeddm1@example.com");

        await using (var ctx = factory.Database.CreateContext()) // ActiveGroupId = null (sees all for seeding)
        {
            ctx.Groups.Add(new GroupEntity { Id = 2, Name = "OtherGroup", CreatedAt = DateTime.UtcNow });
            await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Act — seed only Group 1
        using (var scope = factory.Services.CreateScope())
        {
            var shopSeedService = scope.ServiceProvider.GetRequiredService<IShopSeedService>();
            await shopSeedService.SeedBasicEquipmentAsync(dm.Id, groupId: 1);
        }

        // Assert — items are visible when scoped to Group 1...
        // factory.Database.CreateContext() always uses ActiveGroupId = null (see-all), so the
        // group-scoped query filter must be exercised through a DI-resolved context instead,
        // which honors factory.TestGroupContext (registered as the IActiveGroupContext singleton).
        factory.TestGroupContext.ActiveGroupId = 1;
        using (var group1Scope = factory.Services.CreateScope())
        {
            var group1Ctx = group1Scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
            group1Ctx.ShopItems.Should().NotBeEmpty(because: "seeded items must be visible to the group they were seeded for");
        }

        // ...but not when scoped to Group 2, which received no seed data.
        factory.TestGroupContext.ActiveGroupId = 2;
        using (var group2Scope = factory.Services.CreateScope())
        {
            var group2Ctx = group2Scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
            group2Ctx.ShopItems.Should().BeEmpty(because: "Group 1's seeded items must not leak into another group's view");
        }
    }

    [Fact]
    public async Task SeedBasicEquipmentAsync_RunTwiceForSameGroup_DoesNotDuplicate()
    {
        // Arrange — clean slate with roles and default Group 1 seeded
        await TestDataHelper.ClearDatabaseAsync(factory.Services);

        var dm = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "shopseeddm2", "shopseeddm2@example.com");

        // Act — simulate two application restarts seeding the same group
        using (var scope = factory.Services.CreateScope())
        {
            var shopSeedService = scope.ServiceProvider.GetRequiredService<IShopSeedService>();
            await shopSeedService.SeedBasicEquipmentAsync(dm.Id, groupId: 1);
        }

        int countAfterFirstSeed;
        await using (var ctx = factory.Database.CreateContext())
        {
            countAfterFirstSeed = ctx.ShopItems.Count();
        }
        countAfterFirstSeed.Should().BeGreaterThan(0);

        using (var scope = factory.Services.CreateScope())
        {
            var shopSeedService = scope.ServiceProvider.GetRequiredService<IShopSeedService>();
            await shopSeedService.SeedBasicEquipmentAsync(dm.Id, groupId: 1);
        }

        // Assert — the second run must be a no-op, not a duplicate insert
        await using (var ctx = factory.Database.CreateContext())
        {
            ctx.ShopItems.Count().Should().Be(countAfterFirstSeed,
                because: "re-running the seed for a group that already has published items must not insert duplicates");
        }
    }

    [Fact]
    public async Task SeedBasicEquipmentAsync_OtherGroupAlreadySeeded_StillSeedsThisGroup()
    {
        // Arrange — clean slate with roles and default Group 1 seeded
        await TestDataHelper.ClearDatabaseAsync(factory.Services);

        var dm = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "shopseeddm3", "shopseeddm3@example.com");

        await using (var ctx = factory.Database.CreateContext())
        {
            ctx.Groups.Add(new GroupEntity { Id = 2, Name = "OtherGroup", CreatedAt = DateTime.UtcNow });
            await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using (var scope = factory.Services.CreateScope())
        {
            var shopSeedService = scope.ServiceProvider.GetRequiredService<IShopSeedService>();
            await shopSeedService.SeedBasicEquipmentAsync(dm.Id, groupId: 1);
        }

        // Act — seeding Group 2 must not be blocked by Group 1 already having published items
        using (var scope = factory.Services.CreateScope())
        {
            var shopSeedService = scope.ServiceProvider.GetRequiredService<IShopSeedService>();
            await shopSeedService.SeedBasicEquipmentAsync(dm.Id, groupId: 2);
        }

        // Assert
        factory.TestGroupContext.ActiveGroupId = 2;
        using (var group2Scope = factory.Services.CreateScope())
        {
            var group2Ctx = group2Scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
            group2Ctx.ShopItems.Should().NotBeEmpty(because: "Group 2 must get its own seeded items regardless of Group 1's seed state");
        }
    }
}
