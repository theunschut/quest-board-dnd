using QuestBoard.IntegrationTests.Helpers;
using System.Net;

namespace QuestBoard.IntegrationTests.Controllers;

public class DungeonMasterControllerIntegrationTests(WebApplicationFactoryBase factory) : IClassFixture<WebApplicationFactoryBase>
{
    // Profile GET no longer carries [AllowAnonymous] — an unauthenticated request
    // must redirect to login rather than exposing DM profile data.
    [Fact]
    public async Task Profile_WhenNotAuthenticated_ShouldRedirect()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (_, dm) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, roles: ["DungeonMaster"]);

        // Act
        var response = await factory.CreateNonRedirectingClient()
            .GetAsync($"/DungeonMaster/Profile/{dm.Id}", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.Unauthorized);
    }

    // Profile page returns 200 for a valid DM user id
    [Fact]
    public async Task Profile_WithValidDmUserId_ReturnsOk()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (client, user) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, roles: ["DungeonMaster"]);

        var response = await client.GetAsync($"/DungeonMaster/Profile/{user.Id}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        content.Should().Contain(user.Name);
    }

    // Profile page renders placeholder state when DM has no saved profile yet (no 404)
    [Fact]
    public async Task Profile_WithNoSavedProfile_RendersPlaceholderNotNotFound()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (client, user) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, roles: ["DungeonMaster"]);

        // DM has never saved a profile — profile row does not exist in DB
        var response = await client.GetAsync($"/DungeonMaster/Profile/{user.Id}", TestContext.Current.CancellationToken);

        // Must NOT return 404 — graceful null handling required
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        content.Should().Contain("No bio provided yet.");
    }

    // Profile page returns 404 for a non-existent user id
    [Fact]
    public async Task Profile_WithNonExistentUserId_ReturnsNotFound()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(factory);

        var response = await client.GetAsync("/DungeonMaster/Profile/999999", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // DM can GET EditProfile for their own profile without being redirected or forbidden
    [Fact]
    public async Task EditProfile_OwnProfile_ReturnsOk()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (client, user) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, roles: ["DungeonMaster"]);

        var response = await client.GetAsync($"/DungeonMaster/EditProfile/{user.Id}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        content.Should().Contain("Edit DM Profile");
    }

    // Admin can GET EditProfile for another DM's profile
    [Fact]
    public async Task EditProfile_AdminEditingOtherDm_ReturnsOk()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var targetDm = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "targetdm", "targetdm@example.com");

        var (adminClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, roles: ["Admin"]);

        var response = await adminClient.GetAsync($"/DungeonMaster/EditProfile/{targetDm.Id}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // Non-admin DM gets 403 when trying to edit another DM's profile
    [Fact]
    public async Task EditProfile_NonAdminDmEditingOtherDm_ReturnsForbidden()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var otherDm = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "otherdm", "otherdm@example.com");

        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, roles: ["DungeonMaster"]);

        var response = await client.GetAsync($"/DungeonMaster/EditProfile/{otherDm.Id}", TestContext.Current.CancellationToken);

        // Forbid() with Identity.Application scheme redirects to /Account/AccessDenied (302)
        // This is the standard project behavior — mirrors QuestControllerIntegrationTests pattern
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.Redirect, HttpStatusCode.Unauthorized);
    }

    // Regression: a real Admin who is not the profile owner must see the Edit Profile
    // link (Model.CanEdit driven by GetEffectiveGroupRoleAsync, not the empty AspNetUserRoles).
    [Fact]
    public async Task Profile_NonOwnerAdmin_SeesEditProfileMarker()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var targetDm = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "canedittargetdm", "canedittargetdm@example.com");

        var (adminClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "caneditadmin", "caneditadmin@example.com", roles: ["Admin"]);

        var response = await adminClient.GetAsync($"/DungeonMaster/Profile/{targetDm.Id}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        content.Should().Contain("Edit Profile");
    }

    // Regression: EditProfile GET must not Forbid() a non-owner Admin (already covered
    // by EditProfile_AdminEditingOtherDm_ReturnsOk above — this test names the regression
    // explicitly and asserts the not-Forbidden contract directly).
    [Fact]
    public async Task EditProfile_NonOwnerAdmin_IsNotForbidden()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var targetDm = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "editprofiletargetdm", "editprofiletargetdm@example.com");

        var (adminClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "editprofileadmin", "editprofileadmin@example.com", roles: ["Admin"]);

        var response = await adminClient.GetAsync($"/DungeonMaster/EditProfile/{targetDm.Id}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
    }

    // A Player who is neither the profile owner nor an Admin stays denied
    // (proves the fix did not over-grant).
    [Fact]
    public async Task EditProfile_Player_IsForbiddenOrRedirected()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var targetDm = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "editprofileplayertarget", "editprofileplayertarget@example.com");

        var (playerClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "editprofileplayer", "editprofileplayer@example.com", roles: ["Player"]);

        var response = await playerClient.GetAsync($"/DungeonMaster/EditProfile/{targetDm.Id}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.Redirect, HttpStatusCode.Unauthorized);
    }
}
