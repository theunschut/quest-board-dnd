using System.Net;
using QuestBoard.Domain.Enums;
using QuestBoard.IntegrationTests.Helpers;

namespace QuestBoard.IntegrationTests.Controllers;

/// <summary>
/// Covers campaign quest Close/Reopen authorization, redirect-to-Manage behavior, and
/// campaign-mode Create posting without proposed dates.
/// </summary>
public class QuestCloseTests(WebApplicationFactoryBase factory) : IClassFixture<WebApplicationFactoryBase>
{
    [Fact]
    public async Task Close_OwningDm_RedirectsToManage_AndClosesQuest()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (client, dm) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "closedm1", "closedm1@example.com", roles: ["DungeonMaster"]);
        var quest = await TestDataHelper.CreateTestQuestAsync(
            factory.Services, dm.Id, "Close Test Quest 1");

        // Act
        var response = await client.PostAsync($"/Quest/Close/{quest.Id}", content: null, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.Should().Contain($"/Quest/Manage/{quest.Id}");

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
        var persisted = await context.Quests.FindAsync([quest.Id], TestContext.Current.CancellationToken);
        persisted.Should().NotBeNull();
        persisted!.IsClosed.Should().BeTrue();
    }

    [Fact]
    public async Task Reopen_OwningDm_RedirectsToManage_AndReopensQuest()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (client, dm) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "reopendm1", "reopendm1@example.com", roles: ["DungeonMaster"]);
        var quest = await TestDataHelper.CreateTestQuestAsync(
            factory.Services, dm.Id, "Reopen Test Quest 1", isClosed: true, closedDate: DateTime.UtcNow);

        // Act
        var response = await client.PostAsync($"/Quest/Reopen/{quest.Id}", content: null, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.Should().Contain($"/Quest/Manage/{quest.Id}");

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
        var persisted = await context.Quests.FindAsync([quest.Id], TestContext.Current.CancellationToken);
        persisted.Should().NotBeNull();
        persisted!.IsClosed.Should().BeFalse();
    }

    [Fact]
    public async Task Close_NonOwnerNonAdmin_IsForbidden_AndQuestRemainsOpen()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var dm = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "closeowner1", "closeowner1@example.com");
        var quest = await TestDataHelper.CreateTestQuestAsync(
            factory.Services, dm.Id, "Close Test Quest 2");

        var (otherClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "closeotherdm1", "closeotherdm1@example.com", roles: ["DungeonMaster"]);

        // Act
        var response = await otherClient.PostAsync($"/Quest/Close/{quest.Id}", content: null, TestContext.Current.CancellationToken);

        // Assert — Forbid() resolves through the Identity.Application forbid scheme in tests,
        // which redirects rather than returning a raw 403; match the codebase's established
        // authorization-regression-test convention (see QuestControllerAuthorizationRegressionTests).
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.Redirect, HttpStatusCode.Unauthorized);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
        var persisted = await context.Quests.FindAsync([quest.Id], TestContext.Current.CancellationToken);
        persisted.Should().NotBeNull();
        persisted!.IsClosed.Should().BeFalse();
    }

    [Fact]
    public async Task Campaign_Create_WithNoProposedDates_Persists()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        await TestDataHelper.SeedCampaignGroupAsync(factory.Services, groupId: 2);

        var (client, dm) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "campaigncreatedm1", "campaigncreatedm1@example.com", roles: ["DungeonMaster"]);

        // The DM must also hold a DungeonMaster/Admin GroupRole membership in the campaign
        // group itself — CreateAuthenticatedClientWithUserAsync only seeds group 1 membership.
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
            context.UserGroups.Add(new UserGroupEntity
            {
                UserId = dm.Id,
                GroupId = 2,
                GroupRole = (int)GroupRole.DungeonMaster
            });
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        factory.TestGroupContext.ActiveGroupId = 2;
        try
        {
            var formData = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Title"] = "Campaign Quest No Dates",
                ["Description"] = "A campaign quest posted with no proposed dates",
                ["ChallengeRating"] = "1",
            });

            // Act
            var response = await client.PostAsync("/Quest/Create", formData, TestContext.Current.CancellationToken);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Redirect);

            using var scope = factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
            var persisted = context.Quests
                .Include(q => q.ProposedDates)
                .FirstOrDefault(q => q.Title == "Campaign Quest No Dates");
            persisted.Should().NotBeNull();
            persisted!.ProposedDates.Should().BeEmpty();
        }
        finally
        {
            factory.TestGroupContext.ActiveGroupId = 1;
        }
    }

    [Fact]
    public async Task OneShot_Create_WithNoProposedDates_ReRendersWithValidationError()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(factory.Services);

        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "oneshotcreatedm1", "oneshotcreatedm1@example.com", roles: ["DungeonMaster"]);

        var formData = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Title"] = "OneShot Quest No Dates",
            ["Description"] = "A one-shot quest posted with no proposed dates",
            ["ChallengeRating"] = "1",
        });

        // Act
        var response = await client.PostAsync("/Quest/Create", formData, TestContext.Current.CancellationToken);

        // Assert — re-renders the form (200), does not persist
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
        var persisted = context.Quests.FirstOrDefault(q => q.Title == "OneShot Quest No Dates");
        persisted.Should().BeNull();
    }
}
