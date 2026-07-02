using System.Net;
using QuestBoard.Domain.Enums;
using QuestBoard.IntegrationTests.Helpers;

namespace QuestBoard.IntegrationTests.Controllers;

/// <summary>
/// Integration tests for the /platform/Group management endpoints.
/// Covers groups index, create group, delete group, add member, remove member.
/// </summary>
public class GroupManagementIntegrationTests : IClassFixture<WebApplicationFactoryBase>
{
    private readonly WebApplicationFactoryBase _factory;

    public GroupManagementIntegrationTests(WebApplicationFactoryBase factory)
    {
        _factory = factory;
    }

    // Groups index returns 200 and contains expected heading
    [Fact]
    public async Task GroupsIndex_WhenSuperAdmin_ShouldReturn200WithGroupManagementHeading()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(_factory);

        var response = await client.GetAsync("/platform/Group/Index", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        content.Should().Contain("Group Management");
    }

    // Groups index shows EuphoriaInn seeded by TestDataHelper
    [Fact]
    public async Task GroupsIndex_ShouldShowSeededGroup()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(_factory);

        var response = await client.GetAsync("/platform/Group/Index", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        content.Should().Contain("EuphoriaInn");
    }

    // Create group with valid name redirects (302) or returns 200 on success
    [Fact]
    public async Task CreateGroup_WithValidName_ShouldRedirectOrReturn200()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(_factory);
        var uniqueName = "TestGroup_" + Guid.NewGuid().ToString("N")[..8];
        var formData = new Dictionary<string, string> { ["Name"] = uniqueName };

        var response = await client.PostAsync("/platform/Group/Create",
            new FormUrlEncodedContent(formData), TestContext.Current.CancellationToken);

        // Should redirect to Index after successful creation
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.OK);
    }

    // After creating a group, it appears in the index
    [Fact]
    public async Task CreateGroup_AfterCreation_ShouldAppearInIndex()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(_factory);
        var uniqueName = "NewGroup_" + Guid.NewGuid().ToString("N")[..8];
        var formData = new Dictionary<string, string> { ["Name"] = uniqueName };

        await client.PostAsync("/platform/Group/Create",
            new FormUrlEncodedContent(formData), TestContext.Current.CancellationToken);

        var indexResponse = await client.GetAsync("/platform/Group/Index", TestContext.Current.CancellationToken);
        var content = await indexResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        content.Should().Contain(uniqueName);
    }

    // Delete empty group succeeds (GET Delete shows delete confirmation page)
    [Fact]
    public async Task DeleteGroup_WhenEmpty_ShouldShowDeleteConfirmation()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(_factory);

        // Create a new empty group to delete
        var uniqueName = "DeleteMe_" + Guid.NewGuid().ToString("N")[..8];
        var createData = new Dictionary<string, string> { ["Name"] = uniqueName };
        await client.PostAsync("/platform/Group/Create",
            new FormUrlEncodedContent(createData), TestContext.Current.CancellationToken);

        // Get groups to find the new group id
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
        var group = dbContext.Groups.FirstOrDefault(g => g.Name == uniqueName);
        group.Should().NotBeNull();

        // GET /platform/Group/Delete/{id} should return 200 (delete confirmation page)
        var deleteResponse = await client.GetAsync($"/platform/Group/Delete/{group!.Id}", TestContext.Current.CancellationToken);
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // Delete empty group via POST succeeds
    [Fact]
    public async Task DeleteGroup_WhenEmpty_PostShouldRedirectToIndex()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(_factory);

        // Create a new empty group to delete
        var uniqueName = "DelPost_" + Guid.NewGuid().ToString("N")[..8];
        var createData = new Dictionary<string, string> { ["Name"] = uniqueName };
        await client.PostAsync("/platform/Group/Create",
            new FormUrlEncodedContent(createData), TestContext.Current.CancellationToken);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
        var group = dbContext.Groups.FirstOrDefault(g => g.Name == uniqueName);
        group.Should().NotBeNull();

        // POST /platform/Group/Delete/{id}
        var deleteResponse = await client.PostAsync($"/platform/Group/Delete/{group!.Id}",
            new FormUrlEncodedContent(new Dictionary<string, string>()), TestContext.Current.CancellationToken);
        deleteResponse.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.OK);
    }

    // Delete GET is blocked (redirects to Index) when group has members
    [Fact]
    public async Task DeleteGroup_WhenHasMembers_ShouldRedirectToIndex()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(_factory);

        // Create a group and add a member to it so HasMembersAsync returns true
        var uniqueName = "HasMembers_" + Guid.NewGuid().ToString("N")[..8];
        var createData = new Dictionary<string, string> { ["Name"] = uniqueName };
        await client.PostAsync("/platform/Group/Create",
            new FormUrlEncodedContent(createData), TestContext.Current.CancellationToken);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
        var group = dbContext.Groups.FirstOrDefault(g => g.Name == uniqueName);
        group.Should().NotBeNull();

        // Create a user and add them to this group as a member
        var (_, memberUser) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, "groupmember", "groupmember@test.com", roles: ["Player"]);
        // Add them directly to the new group
        var groupId = group!.Id;
        using var scope2 = _factory.Services.CreateScope();
        var dbContext2 = scope2.ServiceProvider.GetRequiredService<QuestBoardContext>();
        dbContext2.UserGroups.Add(new UserGroupEntity
        {
            UserId = memberUser.Id,
            GroupId = groupId,
            GroupRole = (int)GroupRole.Player
        });
        await dbContext2.SaveChangesAsync(TestContext.Current.CancellationToken);

        // GET /platform/Group/Delete/{id} when group has members should redirect to Index
        var deleteResponse = await client.GetAsync($"/platform/Group/Delete/{groupId}", TestContext.Current.CancellationToken);
        deleteResponse.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found);
    }

    // Members page for existing group returns 200
    [Fact]
    public async Task MembersPage_ForExistingGroup_ShouldReturn200()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(_factory);

        // Group 1 (EuphoriaInn) is seeded by ClearDatabaseAsync via SeedDefaultGroupAsync
        var response = await client.GetAsync("/platform/Group/Members/1", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // Add member adds a UserGroups row and succeeds
    [Fact]
    public async Task AddMember_ValidUserAndGroup_ShouldAddUserGroupsRow()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(_factory);

        // Create a user to add as a member (without UserGroups row for group 1)
        var (_, newUser) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, "newmember", "newmember@test.com", roles: []);

        // Verify not yet a member of group 1
        using var scopeBefore = _factory.Services.CreateScope();
        var dbBefore = scopeBefore.ServiceProvider.GetRequiredService<QuestBoardContext>();
        var membershipBefore = dbBefore.UserGroups.FirstOrDefault(ug => ug.UserId == newUser.Id && ug.GroupId == 1);
        // Note: CreateAuthenticatedClientWithUserAsync with empty roles still seeds a Player UserGroups row
        // If that happens, remove it first for a clean test
        if (membershipBefore != null)
        {
            dbBefore.UserGroups.Remove(membershipBefore);
            await dbBefore.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Add them to group 1 as Player via the AddMember POST.
        // GroupController.AddMember binds its model parameter with [Bind(Prefix = "AddMember")]
        // (The Members view renders AddMemberViewModel fields with an
        // "AddMember." prefix via nested asp-for), so posted fields must use that prefix or
        // the model binds to defaults (UserId=0) and no UserGroups row is created.
        var formData = new Dictionary<string, string>
        {
            ["AddMember.UserId"] = newUser.Id.ToString(),
            ["AddMember.Role"] = ((int)GroupRole.Player).ToString()
        };
        var response = await client.PostAsync("/platform/Group/AddMember/1",
            new FormUrlEncodedContent(formData), TestContext.Current.CancellationToken);

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.OK);

        // Verify the UserGroups row was added
        using var scopeAfter = _factory.Services.CreateScope();
        var dbAfter = scopeAfter.ServiceProvider.GetRequiredService<QuestBoardContext>();
        var membershipAfter = dbAfter.UserGroups.FirstOrDefault(ug => ug.UserId == newUser.Id && ug.GroupId == 1);
        membershipAfter.Should().NotBeNull();
    }

    // Remove member deletes UserGroups row
    [Fact]
    public async Task RemoveMember_ExistingMember_ShouldDeleteUserGroupsRow()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(_factory);

        // Create a user and add them to group 1 directly
        var (_, memberUser) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, "removemember", "removemember@test.com", roles: ["Player"]);

        // Verify membership exists (seeded by CreateAuthenticatedClientWithUserAsync)
        using var scopeBefore = _factory.Services.CreateScope();
        var dbBefore = scopeBefore.ServiceProvider.GetRequiredService<QuestBoardContext>();
        var membershipBefore = dbBefore.UserGroups.FirstOrDefault(ug => ug.UserId == memberUser.Id && ug.GroupId == 1);
        membershipBefore.Should().NotBeNull("User should be a member before removal");

        // Remove via POST /platform/Group/RemoveMember/1 with userId form field
        var formData = new Dictionary<string, string>
        {
            ["userId"] = memberUser.Id.ToString()
        };
        var response = await client.PostAsync("/platform/Group/RemoveMember/1",
            new FormUrlEncodedContent(formData), TestContext.Current.CancellationToken);

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.OK);

        // Verify the UserGroups row was removed
        using var scopeAfter = _factory.Services.CreateScope();
        var dbAfter = scopeAfter.ServiceProvider.GetRequiredService<QuestBoardContext>();
        var membershipAfter = dbAfter.UserGroups.FirstOrDefault(ug => ug.UserId == memberUser.Id && ug.GroupId == 1);
        membershipAfter.Should().BeNull("UserGroups row should have been deleted");
    }
}
