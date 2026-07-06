using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.IntegrationTests.Helpers;
using System.Net;

namespace QuestBoard.IntegrationTests.Controllers;

public class DungeonMasterControllerIntegrationTests(WebApplicationFactoryBase factory)
    : IClassFixture<WebApplicationFactoryBase>, IAsyncLifetime
{
    // IAsyncLifetime — reset the singleton group context after each test so cross-group
    // and SuperAdmin-no-group test state does not bleed into subsequently-executed tests.
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public ValueTask DisposeAsync()
    {
        factory.TestGroupContext.ActiveGroupId = 1;
        return ValueTask.CompletedTask;
    }

    // Adds the given user as a member of the given group so it passes the
    // target-group-membership check. CreateTestUserAsync alone creates a user
    // with no group membership at all.
    private static async Task AddUserToGroupAsync(IServiceProvider services, int userId, int groupId, GroupRole role = GroupRole.DungeonMaster)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();

        var existingMembership = context.UserGroups
            .FirstOrDefault(ug => ug.UserId == userId && ug.GroupId == groupId);
        if (existingMembership == null)
        {
            context.UserGroups.Add(new UserGroupEntity
            {
                UserId = userId,
                GroupId = groupId,
                GroupRole = (int)role
            });
            await context.SaveChangesAsync();
        }
    }

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
        await AddUserToGroupAsync(factory.Services, targetDm.Id, groupId: 1);

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
        await AddUserToGroupAsync(factory.Services, otherDm.Id, groupId: 1);

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
        await AddUserToGroupAsync(factory.Services, targetDm.Id, groupId: 1);

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
        await AddUserToGroupAsync(factory.Services, targetDm.Id, groupId: 1);

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
        await AddUserToGroupAsync(factory.Services, targetDm.Id, groupId: 1);

        var (playerClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "editprofileplayer", "editprofileplayer@example.com", roles: ["Player"]);

        var response = await playerClient.GetAsync($"/DungeonMaster/EditProfile/{targetDm.Id}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.Redirect, HttpStatusCode.Unauthorized);
    }

    // Profile(id) must 404 when the target user belongs to a different group than the
    // viewer's active group — the caller-role policy alone does not validate the target.
    [Fact]
    public async Task Profile_CrossGroupTarget_ReturnsNotFound()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        await TestDataHelper.SeedCampaignGroupAsync(factory.Services, groupId: 2);

        var group2Dm = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "profilecrossgroupdm", "profilecrossgroupdm@example.com");
        await AddUserToGroupAsync(factory.Services, group2Dm.Id, groupId: 2);

        factory.TestGroupContext.ActiveGroupId = 1;
        var (adminClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "profilecrossgroupadmin", "profilecrossgroupadmin@example.com", roles: ["Admin"]);

        var response = await adminClient.GetAsync($"/DungeonMaster/Profile/{group2Dm.Id}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // EditProfile GET must 404 for a cross-group target before the existing same-tenant
    // Forbid() logic is reached.
    [Fact]
    public async Task EditProfile_Get_CrossGroupTarget_ReturnsNotFound()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        await TestDataHelper.SeedCampaignGroupAsync(factory.Services, groupId: 2);

        var group2Dm = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "editgetcrossgroupdm", "editgetcrossgroupdm@example.com");
        await AddUserToGroupAsync(factory.Services, group2Dm.Id, groupId: 2);

        factory.TestGroupContext.ActiveGroupId = 1;
        var (adminClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "editgetcrossgroupadmin", "editgetcrossgroupadmin@example.com", roles: ["Admin"]);

        var response = await adminClient.GetAsync($"/DungeonMaster/EditProfile/{group2Dm.Id}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // EditProfile POST must 404 for a cross-group target and must NOT persist the
    // submitted bio — the target check runs before both Forbid() and UpsertProfileAsync.
    [Fact]
    public async Task EditProfile_Post_CrossGroupTarget_ReturnsNotFoundAndDoesNotPersist()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        await TestDataHelper.SeedCampaignGroupAsync(factory.Services, groupId: 2);

        var group2Dm = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "editpostcrossgroupdm", "editpostcrossgroupdm@example.com");
        await AddUserToGroupAsync(factory.Services, group2Dm.Id, groupId: 2);

        factory.TestGroupContext.ActiveGroupId = 1;
        var (adminClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "editpostcrossgroupadmin", "editpostcrossgroupadmin@example.com", roles: ["Admin"]);

        var formContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["DungeonMasterId"] = group2Dm.Id.ToString(),
            ["Bio"] = "Malicious cross-group bio overwrite attempt"
        });

        var response = await adminClient.PostAsync(
            $"/DungeonMaster/EditProfile/{group2Dm.Id}", formContent, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        using var scope = factory.Services.CreateScope();
        var dmProfileService = scope.ServiceProvider.GetRequiredService<IDungeonMasterProfileService>();
        var profile = await dmProfileService.GetProfileByUserIdAsync(group2Dm.Id, TestContext.Current.CancellationToken);
        profile?.Bio.Should().NotBe("Malicious cross-group bio overwrite attempt");
    }

    // GetDMProfilePicture must 404 for a cross-group target.
    [Fact]
    public async Task GetDMProfilePicture_CrossGroupTarget_ReturnsNotFound()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        await TestDataHelper.SeedCampaignGroupAsync(factory.Services, groupId: 2);

        var group2Dm = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "picturecrossgroupdm", "picturecrossgroupdm@example.com");
        await AddUserToGroupAsync(factory.Services, group2Dm.Id, groupId: 2);

        factory.TestGroupContext.ActiveGroupId = 1;
        var (adminClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "picturecrossgroupadmin", "picturecrossgroupadmin@example.com", roles: ["Admin"]);

        var response = await adminClient.GetAsync($"/DungeonMaster/GetDMProfilePicture/{group2Dm.Id}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // When the viewer has no active group selected, GroupSessionMiddleware now gates
    // SuperAdmin exactly like every other role — the request never reaches the controller at
    // all, it is redirected to the group picker before any DM-profile action logic runs.
    [Fact]
    public async Task Profile_SuperAdminNoActiveGroup_RedirectsToGroupPick()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var targetDm = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "superadminnogrouptarget", "superadminnogrouptarget@example.com");
        await AddUserToGroupAsync(factory.Services, targetDm.Id, groupId: 1);

        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "superadminnogroupviewer", "superadminnogroupviewer@example.com", roles: ["SuperAdmin"]);

        factory.TestGroupContext.ActiveGroupId = null;

        var response = await client.GetAsync($"/DungeonMaster/Profile/{targetDm.Id}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found);
        var location = response.Headers.Location?.ToString() ?? string.Empty;
        location.Should().Contain("/groups/pick");
    }
}
