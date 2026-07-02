using QuestBoard.IntegrationTests.Helpers;
using System.Net;

namespace QuestBoard.IntegrationTests.Controllers;

/// <summary>
/// Regression coverage for QuestController's GroupRole-based Admin/DM authorization.
/// Every test here seeds a quest owned by a DIFFERENT dungeon master than the authenticated
/// Admin under test, so the assertions exercise the "Admin who is not the quest's DM" path —
/// the exact scenario that was broken when these checks still read the (now-empty) AspNetUserRoles.
/// </summary>
public class QuestControllerAuthorizationRegressionTests(WebApplicationFactoryBase factory) : IClassFixture<WebApplicationFactoryBase>
{
    [Fact]
    public async Task Details_NonOwnerAdmin_SeesManageQuestLink()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var dm = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "regressiondm1", "regressiondm1@example.com");
        var quest = await TestDataHelper.CreateTestQuestAsync(
            factory.Services, dm.Id, "Regression Quest 1");

        var (adminClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "regressionadmin1", "regressionadmin1@example.com", roles: ["Admin"]);

        // Act
        var response = await adminClient.GetAsync($"/Quest/Details/{quest.Id}", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        content.Should().Contain("Manage Quest");
    }

    [Fact]
    public async Task Details_Anonymous_DoesNotSeeManageQuestLinkAndDoesNotThrow()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var dm = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "regressiondm2", "regressiondm2@example.com");
        var quest = await TestDataHelper.CreateTestQuestAsync(
            factory.Services, dm.Id, "Regression Quest 2");

        var anonClient = factory.CreateNonRedirectingClient();

        // Act
        var response = await anonClient.GetAsync($"/Quest/Details/{quest.Id}", TestContext.Current.CancellationToken);

        // Assert — no 500, and the DM Controls card (which contains the Manage Quest link) is absent.
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        content.Should().NotContain("Manage Quest");
    }

    [Fact]
    public async Task Manage_NonOwnerAdmin_IsNotForbiddenAndSeesAdminMarker()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var dm = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "regressiondm3", "regressiondm3@example.com");
        var quest = await TestDataHelper.CreateTestQuestAsync(
            factory.Services, dm.Id, "Regression Quest 3");
        await TestDataHelper.CreateProposedDateAsync(factory.Services, quest.Id, DateTime.Today.AddDays(7));

        var (adminClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "regressionadmin3", "regressionadmin3@example.com", roles: ["Admin"]);

        // Act
        var response = await adminClient.GetAsync($"/Quest/Manage/{quest.Id}", TestContext.Current.CancellationToken);

        // Assert — 200, not the ViewBag.IsAuthorized-gated "Access Denied" content.
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        content.Should().NotContain("Access Denied");
        content.Should().Contain("Finalize Quest");
    }

    [Fact]
    public async Task Edit_NonOwnerAdmin_IsNotForbidden()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var dm = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "regressiondm4", "regressiondm4@example.com");
        var quest = await TestDataHelper.CreateTestQuestAsync(
            factory.Services, dm.Id, "Regression Quest 4");

        var (adminClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "regressionadmin4", "regressionadmin4@example.com", roles: ["Admin"]);

        // Act
        var response = await adminClient.GetAsync($"/Quest/Edit/{quest.Id}", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Delete_NonOwnerAdmin_IsNotForbidden()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var dm = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "regressiondm5", "regressiondm5@example.com");
        var quest = await TestDataHelper.CreateTestQuestAsync(
            factory.Services, dm.Id, "Regression Quest 5");

        var (adminClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "regressionadmin5", "regressionadmin5@example.com", roles: ["Admin"]);

        // Act
        var response = await adminClient.DeleteAsync($"/Quest/Delete/{quest.Id}", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Finalize_NonOwnerAdmin_IsNotForbidden()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var dm = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "regressiondm6", "regressiondm6@example.com");
        var quest = await TestDataHelper.CreateTestQuestAsync(
            factory.Services, dm.Id, "Regression Quest 6");
        var proposedDate = await TestDataHelper.CreateProposedDateAsync(factory.Services, quest.Id, DateTime.Today.AddDays(7));

        var (adminClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "regressionadmin6", "regressionadmin6@example.com", roles: ["Admin"]);

        var formContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["SelectedDateId"] = proposedDate.Id.ToString()
        });

        // Act
        var response = await adminClient.PostAsync($"/Quest/Finalize/{quest.Id}", formContent, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Open_NonOwnerAdmin_IsNotForbidden()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var dm = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "regressiondm7", "regressiondm7@example.com");
        var quest = await TestDataHelper.CreateTestQuestAsync(
            factory.Services, dm.Id, "Regression Quest 7", isFinalized: true,
            finalizedDate: DateTime.Today.AddDays(-7));
        await TestDataHelper.CreateProposedDateAsync(factory.Services, quest.Id, DateTime.Today.AddDays(-7));

        var (adminClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "regressionadmin7", "regressionadmin7@example.com", roles: ["Admin"]);

        // Act
        var response = await adminClient.PostAsync($"/Quest/Open/{quest.Id}", content: null, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateFollowUp_Get_NonOwnerAdmin_IsNotForbidden()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var dm = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "regressiondm8", "regressiondm8@example.com");
        var quest = await TestDataHelper.CreateTestQuestAsync(
            factory.Services, dm.Id, "Regression Quest 8", isFinalized: true,
            finalizedDate: DateTime.Today.AddDays(-7));

        var (adminClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "regressionadmin8", "regressionadmin8@example.com", roles: ["Admin"]);

        // Act
        var response = await adminClient.GetAsync($"/Quest/CreateFollowUp/{quest.Id}", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task SendReminder_NonOwnerAdmin_IsNotForbidden()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var dm = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "regressiondm9", "regressiondm9@example.com");
        var quest = await TestDataHelper.CreateTestQuestAsync(
            factory.Services, dm.Id, "Regression Quest 9", isFinalized: true,
            finalizedDate: DateTime.Today.AddDays(-7));
        await TestDataHelper.CreateProposedDateAsync(factory.Services, quest.Id, DateTime.Today.AddDays(-7));

        var (adminClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "regressionadmin9", "regressionadmin9@example.com", roles: ["Admin"]);

        // Act
        var response = await adminClient.PostAsync($"/Quest/SendReminder/{quest.Id}", content: null, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Edit_PlayerNotOwnerNorAdmin_IsForbiddenOrRedirected()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var dm = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "regressiondm10", "regressiondm10@example.com");
        var quest = await TestDataHelper.CreateTestQuestAsync(
            factory.Services, dm.Id, "Regression Quest 10");

        var (playerClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "regressionplayer10", "regressionplayer10@example.com", roles: ["Player"]);

        // Act
        var response = await playerClient.GetAsync($"/Quest/Edit/{quest.Id}", TestContext.Current.CancellationToken);

        // Assert — the fix must not over-grant: a Player who is neither owner nor Admin stays denied.
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.Redirect, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Delete_SuperAdmin_IsNotForbidden()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var dm = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "regressiondm11", "regressiondm11@example.com");
        var quest = await TestDataHelper.CreateTestQuestAsync(
            factory.Services, dm.Id, "Regression Quest 11");

        var (superAdminClient, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(factory);

        // Act
        var response = await superAdminClient.DeleteAsync($"/Quest/Delete/{quest.Id}", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
    }
}
