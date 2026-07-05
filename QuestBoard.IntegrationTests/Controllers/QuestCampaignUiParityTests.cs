using System.Net;
using QuestBoard.Domain.Enums;
using QuestBoard.IntegrationTests.Helpers;

namespace QuestBoard.IntegrationTests.Controllers;

/// <summary>
/// Wave-0 failing tests pinning the Campaign/OneShot quest UI-parity behaviors this phase
/// must deliver: the Manage page's Edit/Delete affordances for Campaign quests, and the
/// Edit page's board-type-conditional field visibility (mirroring Create). These tests are
/// authored before any production markup changes and are expected to fail against the
/// current tree except where noted as regression guards.
/// </summary>
public class QuestCampaignUiParityTests(WebApplicationFactoryBase factory) : IClassFixture<WebApplicationFactoryBase>
{
    private const string MobileUserAgent =
        "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1";

    // Close/Reopen/Edit/Delete require the active group to resolve to BoardType.Campaign;
    // membership for group 1 is seeded automatically by CreateAuthenticatedClientWithUserAsync,
    // but group 2 (the campaign board used by these tests) needs its own membership row.
    private async Task AddCampaignGroupMembershipAsync(int userId, GroupRole groupRole)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
        var existingMembership = context.UserGroups
            .FirstOrDefault(ug => ug.UserId == userId && ug.GroupId == 2);
        if (existingMembership == null)
        {
            context.UserGroups.Add(new UserGroupEntity
            {
                UserId = userId,
                GroupId = 2,
                GroupRole = (int)groupRole
            });
            await context.SaveChangesAsync();
        }
    }

    // -----------------------------------------------------------------------
    // Manage page — Edit Quest link (D-01/D-02) and Delete link (D-03)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task CampaignManage_Desktop_RendersEditQuestLink()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        await TestDataHelper.SeedCampaignGroupAsync(factory.Services, groupId: 2);
        var (client, dm) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "campmandm1", "campmandm1@example.com", roles: ["DungeonMaster"]);
        await AddCampaignGroupMembershipAsync(dm.Id, GroupRole.DungeonMaster);
        var quest = await TestDataHelper.CreateTestQuestAsync(
            factory.Services, dm.Id, "Campaign Manage Quest 1", groupId: 2);

        factory.TestGroupContext.ActiveGroupId = 2;
        try
        {
            // Act
            var response = await client.GetAsync($"/Quest/Manage/{quest.Id}", TestContext.Current.CancellationToken);
            var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            content.Should().Contain("Edit Quest");
            content.Should().Contain($"/Quest/Edit/{quest.Id}");
        }
        finally
        {
            factory.TestGroupContext.ActiveGroupId = 1;
        }
    }

    [Fact]
    public async Task CampaignManage_Desktop_RendersDeleteLinkWiredToDeleteQuest()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        await TestDataHelper.SeedCampaignGroupAsync(factory.Services, groupId: 2);
        var (client, dm) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "campmandm2", "campmandm2@example.com", roles: ["DungeonMaster"]);
        await AddCampaignGroupMembershipAsync(dm.Id, GroupRole.DungeonMaster);
        var quest = await TestDataHelper.CreateTestQuestAsync(
            factory.Services, dm.Id, "Campaign Manage Quest 2", groupId: 2);

        factory.TestGroupContext.ActiveGroupId = 2;
        try
        {
            // Act
            var response = await client.GetAsync($"/Quest/Manage/{quest.Id}", TestContext.Current.CancellationToken);
            var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            content.Should().Contain($"deleteQuest({quest.Id})");
        }
        finally
        {
            factory.TestGroupContext.ActiveGroupId = 1;
        }
    }

    [Fact]
    public async Task CampaignManage_Mobile_RendersEditQuestAndDeleteQuestLinks()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        await TestDataHelper.SeedCampaignGroupAsync(factory.Services, groupId: 2);
        var (client, dm) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "campmandm3", "campmandm3@example.com", roles: ["DungeonMaster"]);
        await AddCampaignGroupMembershipAsync(dm.Id, GroupRole.DungeonMaster);
        var quest = await TestDataHelper.CreateTestQuestAsync(
            factory.Services, dm.Id, "Campaign Manage Quest 3", groupId: 2);

        factory.TestGroupContext.ActiveGroupId = 2;
        try
        {
            // Act
            var request = new HttpRequestMessage(HttpMethod.Get, $"/Quest/Manage/{quest.Id}");
            request.Headers.TryAddWithoutValidation("User-Agent", MobileUserAgent);
            request.Headers.Authorization = client.DefaultRequestHeaders.Authorization;
            var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
            var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            content.Should().Contain("Edit Quest");
            content.Should().Contain($"/Quest/Edit/{quest.Id}");
            content.Should().Contain($"deleteQuest({quest.Id})");
        }
        finally
        {
            factory.TestGroupContext.ActiveGroupId = 1;
        }
    }

    // -----------------------------------------------------------------------
    // Edit page — field visibility by board type (D-04) and Edit GET ViewBag.BoardType (D-05)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task CampaignEdit_Desktop_HidesFourOneShotFields()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        await TestDataHelper.SeedCampaignGroupAsync(factory.Services, groupId: 2);
        var (client, dm) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "campeditdm1", "campeditdm1@example.com", roles: ["DungeonMaster"]);
        await AddCampaignGroupMembershipAsync(dm.Id, GroupRole.DungeonMaster);
        var quest = await TestDataHelper.CreateTestQuestAsync(
            factory.Services, dm.Id, "Campaign Edit Quest 1", groupId: 2);

        factory.TestGroupContext.ActiveGroupId = 2;
        try
        {
            // Act
            var response = await client.GetAsync($"/Quest/Edit/{quest.Id}", TestContext.Current.CancellationToken);
            var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            content.Should().NotContain("Challenge Rating");
            content.Should().NotContain("Total Player Count");
            content.Should().NotContain("Dungeon Master Session Only");
            content.Should().NotContain("Proposed Dates");
        }
        finally
        {
            factory.TestGroupContext.ActiveGroupId = 1;
        }
    }

    [Fact]
    public async Task OneShotEdit_Desktop_ShowsFourFields()
    {
        // Arrange — group 1 is the default seeded OneShot board, no campaign seed needed.
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (client, dm) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "oneshoteditdm1", "oneshoteditdm1@example.com", roles: ["DungeonMaster"]);
        var quest = await TestDataHelper.CreateTestQuestAsync(
            factory.Services, dm.Id, "OneShot Edit Quest 1", groupId: 1);

        factory.TestGroupContext.ActiveGroupId = 1;

        // Act
        var response = await client.GetAsync($"/Quest/Edit/{quest.Id}", TestContext.Current.CancellationToken);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        content.Should().Contain("Challenge Rating");
        content.Should().Contain("Total Player Count");
        content.Should().Contain("Dungeon Master Session Only");
        content.Should().Contain("Proposed Dates");
    }

    [Fact]
    public async Task CampaignEdit_InvalidModelState_Returns200_DoesNotThrow()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        await TestDataHelper.SeedCampaignGroupAsync(factory.Services, groupId: 2);
        var (client, dm) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "campeditdm2", "campeditdm2@example.com", roles: ["DungeonMaster"]);
        await AddCampaignGroupMembershipAsync(dm.Id, GroupRole.DungeonMaster);
        var quest = await TestDataHelper.CreateTestQuestAsync(
            factory.Services, dm.Id, "Campaign Edit Quest 2", groupId: 2);

        factory.TestGroupContext.ActiveGroupId = 2;
        try
        {
            // Get the Edit form to extract the antiforgery token
            var getResponse = await client.GetAsync($"/Quest/Edit/{quest.Id}", TestContext.Current.CancellationToken);
            var (token, cookieValue) = await AntiForgeryHelper.ExtractAntiForgeryTokenAsync(getResponse);

            if (!string.IsNullOrEmpty(cookieValue))
            {
                client.DefaultRequestHeaders.Remove("Cookie");
                client.DefaultRequestHeaders.Add("Cookie", $".AspNetCore.Antiforgery={cookieValue}");
            }

            var formContent = AntiForgeryHelper.CreateFormContentWithAntiForgeryToken(
                new Dictionary<string, string>
                {
                    ["Id"] = quest.Id.ToString(),
                    ["Quest.Id"] = quest.Id.ToString(),
                    ["Quest.Title"] = "", // invalid — empty title
                    ["Quest.Description"] = "Still has a description",
                },
                token);

            // Act
            var response = await client.PostAsync($"/Quest/Edit/{quest.Id}", formContent, TestContext.Current.CancellationToken);

            // Assert — form re-renders with validation errors, does not throw / 5xx
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }
        finally
        {
            factory.TestGroupContext.ActiveGroupId = 1;
        }
    }

    [Fact]
    public async Task CampaignEdit_Mobile_HidesFourOneShotFields()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        await TestDataHelper.SeedCampaignGroupAsync(factory.Services, groupId: 2);
        var (client, dm) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "campeditdm3", "campeditdm3@example.com", roles: ["DungeonMaster"]);
        await AddCampaignGroupMembershipAsync(dm.Id, GroupRole.DungeonMaster);
        var quest = await TestDataHelper.CreateTestQuestAsync(
            factory.Services, dm.Id, "Campaign Edit Quest 3", groupId: 2);

        factory.TestGroupContext.ActiveGroupId = 2;
        try
        {
            // Act
            var request = new HttpRequestMessage(HttpMethod.Get, $"/Quest/Edit/{quest.Id}");
            request.Headers.TryAddWithoutValidation("User-Agent", MobileUserAgent);
            request.Headers.Authorization = client.DefaultRequestHeaders.Authorization;
            var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
            var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            content.Should().NotContain("Challenge Rating");
            content.Should().NotContain("Total Player Count");
            content.Should().NotContain("Dungeon Master Session Only");
            content.Should().NotContain("Proposed Dates");
        }
        finally
        {
            factory.TestGroupContext.ActiveGroupId = 1;
        }
    }
}
