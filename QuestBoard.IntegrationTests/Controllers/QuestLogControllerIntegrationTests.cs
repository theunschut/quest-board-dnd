using QuestBoard.IntegrationTests.Helpers;
using System.Net;

namespace QuestBoard.IntegrationTests.Controllers;

public class QuestLogControllerIntegrationTests(WebApplicationFactoryBase factory) : IClassFixture<WebApplicationFactoryBase>
{
    // QuestLog now requires authentication ([Authorize] added to the controller).
    // Unauthenticated requests must redirect (or 401), never return the quest log directly.
    [Fact]
    public async Task Index_WhenNotAuthenticated_ShouldRedirect()
    {
        // Act
        var response = await factory.CreateNonRedirectingClient()
            .GetAsync("/QuestLog", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Index_ShouldReturnQuestLogPage()
    {
        // Arrange
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(factory);

        // Act
        var response = await client.GetAsync("/QuestLog", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        content.Should().ContainAny("Quest", "Log");
    }

    [Fact]
    public async Task Index_WithCompletedQuests_ShouldDisplayQuests()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var dm = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "logdm", "log@example.com");
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "logviewer", "logviewer@example.com");

        var quest1 = await TestDataHelper.CreateTestQuestAsync(
            factory.Services, dm.Id, "Completed Quest 1", "Description 1", 5, isFinalized: true);
        var quest2 = await TestDataHelper.CreateTestQuestAsync(
            factory.Services, dm.Id, "Completed Quest 2", "Description 2", 8, isFinalized: true);

        // Set finalized dates
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
            var q1 = await context.Quests.FindAsync([quest1.Id], TestContext.Current.CancellationToken);
            var q2 = await context.Quests.FindAsync([quest2.Id], TestContext.Current.CancellationToken);
            q1?.FinalizedDate = DateTime.Today.AddDays(-7);
            q2?.FinalizedDate = DateTime.Today.AddDays(-14);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Act
        var response = await client.GetAsync("/QuestLog", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        content.Should().Contain("Completed Quest 1");
        content.Should().Contain("Completed Quest 2");
    }

    [Fact]
    public async Task Details_WithValidQuestId_ShouldReturnQuestDetails()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var dm = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "detailslogdm", "detailslog@example.com");
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "detailslogviewer", "detailslogviewer@example.com");

        var quest = await TestDataHelper.CreateTestQuestAsync(
            factory.Services, dm.Id, "Quest With Details", "Detailed description", 10, isFinalized: true);

        // Set finalized date to at least 1 day in the past
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
            var questToUpdate = await context.Quests.FindAsync([quest.Id], TestContext.Current.CancellationToken);
            if (questToUpdate != null)
            {
                questToUpdate.FinalizedDate = DateTime.UtcNow.AddDays(-2);
                await context.SaveChangesAsync(TestContext.Current.CancellationToken);
            }
        }

        // Act
        var response = await client.GetAsync($"/QuestLog/Details/{quest.Id}", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        content.Should().Contain("Quest With Details");
        content.Should().Contain("Detailed description");
    }

    [Fact]
    public async Task Details_WithInvalidQuestId_ShouldReturn404()
    {
        // Arrange
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(factory);

        // Act
        var response = await client.GetAsync("/QuestLog/Details/99999", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Index_ShouldOnlyShowFinalizedQuests()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var dm = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "filterlogdm", "filterlog@example.com");
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "filterlogviewer", "filterlogviewer@example.com");

        var finalizedQuest = await TestDataHelper.CreateTestQuestAsync(
            factory.Services, dm.Id, "Finalized Quest", "Done", 5, isFinalized: true);
        var activeQuest = await TestDataHelper.CreateTestQuestAsync(
            factory.Services, dm.Id, "Active Quest", "Not done", 5, isFinalized: false);

        // Set finalized date for the finalized quest
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
            var questToUpdate = await context.Quests.FindAsync([finalizedQuest.Id], TestContext.Current.CancellationToken);
            if (questToUpdate != null)
            {
                questToUpdate.FinalizedDate = DateTime.UtcNow.AddDays(-2);
                await context.SaveChangesAsync(TestContext.Current.CancellationToken);
            }
        }

        // Act
        var response = await client.GetAsync("/QuestLog", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        content.Should().Contain("Finalized Quest");
        content.Should().NotContain("Active Quest");
    }

    [Fact]
    public async Task Index_ShouldNotShowFinalizedDmSessions()
    {
        // Arrange — #89: DM sessions should be excluded from the quest log even when finalized
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var dm = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "dmsessiondm", "dmsession@example.com");
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "dmsessionviewer", "dmsessionviewer@example.com");

        var regularQuest = await TestDataHelper.CreateTestQuestAsync(
            factory.Services, dm.Id, "Regular Finalized Quest", "Desc", 5, isFinalized: true, dungeonMasterSession: false);
        var dmSession = await TestDataHelper.CreateTestQuestAsync(
            factory.Services, dm.Id, "DM Session Quest", "Private session", 5, isFinalized: true, dungeonMasterSession: true);

        // Set finalized dates for both
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
            var q1 = await context.Quests.FindAsync([regularQuest.Id], TestContext.Current.CancellationToken);
            var q2 = await context.Quests.FindAsync([dmSession.Id], TestContext.Current.CancellationToken);
            if (q1 != null) q1.FinalizedDate = DateTime.UtcNow.AddDays(-2);
            if (q2 != null) q2.FinalizedDate = DateTime.UtcNow.AddDays(-2);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Act
        var response = await client.GetAsync("/QuestLog", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        content.Should().Contain("Regular Finalized Quest");
        content.Should().NotContain("DM Session Quest");
    }

    [Fact]
    public async Task Details_WithDmSessionQuestId_ShouldReturn404()
    {
        // Arrange — #89: DM session details should not be accessible via quest log
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var dm = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "dmsessiondetailsdm", "dmsessiondetails@example.com");
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "dmsessiondetailsviewer", "dmsessiondetailsviewer@example.com");

        var dmSession = await TestDataHelper.CreateTestQuestAsync(
            factory.Services, dm.Id, "DM Session", "Private", 5, isFinalized: true, dungeonMasterSession: true);

        // Set finalized date
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
            var questToUpdate = await context.Quests.FindAsync([dmSession.Id], TestContext.Current.CancellationToken);
            if (questToUpdate != null)
            {
                questToUpdate.FinalizedDate = DateTime.UtcNow.AddDays(-2);
                await context.SaveChangesAsync(TestContext.Current.CancellationToken);
            }
        }

        // Act
        var response = await client.GetAsync($"/QuestLog/Details/{dmSession.Id}", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // Regression: a real Admin who is not the quest's DM must see the recap-edit form
    // (ViewBag.CanEditRecap driven by GetEffectiveGroupRoleAsync, not the empty AspNetUserRoles).
    [Fact]
    public async Task Details_NonOwnerAdmin_SeesRecapEditMarker()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var dm = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "recapregressiondm", "recapregressiondm@example.com");
        var quest = await TestDataHelper.CreateTestQuestAsync(
            factory.Services, dm.Id, "Recap Regression Quest", "Desc", 5, isFinalized: true);

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
            var questToUpdate = await context.Quests.FindAsync([quest.Id], TestContext.Current.CancellationToken);
            if (questToUpdate != null)
            {
                questToUpdate.FinalizedDate = DateTime.UtcNow.AddDays(-2);
                await context.SaveChangesAsync(TestContext.Current.CancellationToken);
            }
        }

        var (adminClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "recapregressionadmin", "recapregressionadmin@example.com", roles: ["Admin"]);

        // Act
        var response = await adminClient.GetAsync($"/QuestLog/Details/{quest.Id}", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        content.Should().Contain("Save Recap");
    }

    // Regression: UpdateRecap must not Forbid() a non-owner Admin.
    [Fact]
    public async Task UpdateRecap_NonOwnerAdmin_IsNotForbidden()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var dm = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "recapupdatedm", "recapupdatedm@example.com");
        var quest = await TestDataHelper.CreateTestQuestAsync(
            factory.Services, dm.Id, "Recap Update Quest", "Desc", 5, isFinalized: true);

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
            var questToUpdate = await context.Quests.FindAsync([quest.Id], TestContext.Current.CancellationToken);
            if (questToUpdate != null)
            {
                questToUpdate.FinalizedDate = DateTime.UtcNow.AddDays(-2);
                await context.SaveChangesAsync(TestContext.Current.CancellationToken);
            }
        }

        var (adminClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "recapupdateadmin", "recapupdateadmin@example.com", roles: ["Admin"]);

        var formContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["recap"] = "Updated by a non-owner admin."
        });

        // Act
        var response = await adminClient.PostAsync($"/QuestLog/UpdateRecap/{quest.Id}", formContent, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
    }

    // A Player who is neither the quest's DM nor an Admin stays denied on UpdateRecap
    // (proves the fix did not over-grant).
    [Fact]
    public async Task UpdateRecap_Player_IsForbiddenOrRedirected()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var dm = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "recapplayerdm", "recapplayerdm@example.com");
        var quest = await TestDataHelper.CreateTestQuestAsync(
            factory.Services, dm.Id, "Recap Player Quest", "Desc", 5, isFinalized: true);

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
            var questToUpdate = await context.Quests.FindAsync([quest.Id], TestContext.Current.CancellationToken);
            if (questToUpdate != null)
            {
                questToUpdate.FinalizedDate = DateTime.UtcNow.AddDays(-2);
                await context.SaveChangesAsync(TestContext.Current.CancellationToken);
            }
        }

        var (playerClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "recapplayeruser", "recapplayeruser@example.com", roles: ["Player"]);

        var formContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["recap"] = "Should not be allowed."
        });

        // Act
        var response = await playerClient.PostAsync($"/QuestLog/UpdateRecap/{quest.Id}", formContent, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.Redirect, HttpStatusCode.Unauthorized);
    }

    // A Player who is neither the quest's DM nor an Admin must be denied direct-URL access
    // to the dedicated recap-edit page (D-04: Forbid(), not the cross-tenant 404 convention).
    [Fact]
    public async Task EditRecap_Player_IsForbidden()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var dm = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "editrecapplayerdm", "editrecapplayerdm@example.com");
        var quest = await TestDataHelper.CreateTestQuestAsync(
            factory.Services, dm.Id, "Edit Recap Player Quest", "Desc", 5, isFinalized: true);

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
            var questToUpdate = await context.Quests.FindAsync([quest.Id], TestContext.Current.CancellationToken);
            if (questToUpdate != null)
            {
                questToUpdate.FinalizedDate = DateTime.UtcNow.AddDays(-2);
                await context.SaveChangesAsync(TestContext.Current.CancellationToken);
            }
        }

        var (playerClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "editrecapplayeruser", "editrecapplayeruser@example.com", roles: ["Player"]);

        // Act
        var response = await playerClient.GetAsync($"/QuestLog/EditRecap/{quest.Id}", TestContext.Current.CancellationToken);

        // Assert — the app's cookie DefaultForbidScheme redirects instead of returning a literal 403.
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.Redirect, HttpStatusCode.Unauthorized);
    }

    // A non-owner Admin must be able to reach the dedicated recap-edit page.
    [Fact]
    public async Task EditRecap_NonOwnerAdmin_ReturnsOk()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var dm = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "editrecapadmindm", "editrecapadmindm@example.com");
        var quest = await TestDataHelper.CreateTestQuestAsync(
            factory.Services, dm.Id, "Edit Recap Admin Quest", "Desc", 5, isFinalized: true);

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
            var questToUpdate = await context.Quests.FindAsync([quest.Id], TestContext.Current.CancellationToken);
            if (questToUpdate != null)
            {
                questToUpdate.FinalizedDate = DateTime.UtcNow.AddDays(-2);
                await context.SaveChangesAsync(TestContext.Current.CancellationToken);
            }
        }

        var (adminClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "editrecapadminuser", "editrecapadminuser@example.com", roles: ["Admin"]);

        // Act
        var response = await adminClient.GetAsync($"/QuestLog/EditRecap/{quest.Id}", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // A non-owner Admin's POST to the dedicated recap-edit page must persist and redirect to Details.
    [Fact]
    public async Task EditRecap_Post_NonOwnerAdmin_RedirectsToDetails()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var dm = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "editrecapadminpostdm", "editrecapadminpostdm@example.com");
        var quest = await TestDataHelper.CreateTestQuestAsync(
            factory.Services, dm.Id, "Edit Recap Admin Post Quest", "Desc", 5, isFinalized: true);

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
            var questToUpdate = await context.Quests.FindAsync([quest.Id], TestContext.Current.CancellationToken);
            if (questToUpdate != null)
            {
                questToUpdate.FinalizedDate = DateTime.UtcNow.AddDays(-2);
                await context.SaveChangesAsync(TestContext.Current.CancellationToken);
            }
        }

        var (adminClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "editrecapadminpostuser", "editrecapadminpostuser@example.com", roles: ["Admin"]);

        var formContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["recap"] = "Recap set via dedicated edit page."
        });

        // Act
        var response = await adminClient.PostAsync($"/QuestLog/EditRecap/{quest.Id}", formContent, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found);
    }
}
