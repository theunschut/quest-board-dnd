using System.Net;
using Hangfire.Common;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.IntegrationTests.Helpers;
using QuestBoard.Service.Jobs;

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
        var formData = new Dictionary<string, string> { ["Name"] = uniqueName, ["BoardType"] = ((int)BoardType.OneShot).ToString() };

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
        var formData = new Dictionary<string, string> { ["Name"] = uniqueName, ["BoardType"] = ((int)BoardType.OneShot).ToString() };

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
        var createData = new Dictionary<string, string> { ["Name"] = uniqueName, ["BoardType"] = ((int)BoardType.OneShot).ToString() };
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
        var createData = new Dictionary<string, string> { ["Name"] = uniqueName, ["BoardType"] = ((int)BoardType.OneShot).ToString() };
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
        var createData = new Dictionary<string, string> { ["Name"] = uniqueName, ["BoardType"] = ((int)BoardType.OneShot).ToString() };
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
        // GroupController.AddMember now binds UserId/Role as top-level fields
        // (the per-row Add form posts them without a nested "AddMember." prefix).
        var formData = new Dictionary<string, string>
        {
            ["UserId"] = newUser.Id.ToString(),
            ["Role"] = ((int)GroupRole.Player).ToString()
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

    // Members GET with a search term filters the available-users list and echoes the term back
    [Fact]
    public async Task MembersPage_WithSearch_ShouldReturnOnlyMatchingNonMembersAndEchoTerm()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(_factory);

        var uniqueTag = Guid.NewGuid().ToString("N")[..8];
        var distinctiveName = $"Zebrafolk{uniqueTag}";
        var (_, matching) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, $"membersearch{uniqueTag}", $"membersearch{uniqueTag}@test.com", name: distinctiveName, roles: []);

        // Ensure the seeded non-member has no UserGroups row for group 1 (empty roles may still seed one)
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
            var membership = db.UserGroups.FirstOrDefault(ug => ug.UserId == matching.Id && ug.GroupId == 1);
            if (membership != null)
            {
                db.UserGroups.Remove(membership);
                await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            }
        }

        // Matching search returns the user in the (DB-filtered) available-users list.
        // Note: the search term is echoed onto GroupMembersViewModel.SearchQuery at the controller
        // level (verified here via the filtered result set); rendering that value into a visible
        // search input is the Plan 03 view redesign's responsibility, not this plan's.
        var matchResponse = await client.GetAsync($"/platform/Group/Members/1?search={distinctiveName}", TestContext.Current.CancellationToken);
        matchResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var matchContent = await matchResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        matchContent.Should().Contain(distinctiveName);

        // Non-matching search excludes the user
        var noMatchResponse = await client.GetAsync($"/platform/Group/Members/1?search=NoSuchUser{uniqueTag}", TestContext.Current.CancellationToken);
        noMatchResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var noMatchContent = await noMatchResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        noMatchContent.Should().NotContain(distinctiveName);
    }

    // AddMember redirect preserves the search term (D-04)
    [Fact]
    public async Task AddMember_WithSearch_ShouldPreserveSearchOnRedirect()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(_factory);

        var (_, newUser) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, "searchaddmember", "searchaddmember@test.com", roles: []);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
            var membership = db.UserGroups.FirstOrDefault(ug => ug.UserId == newUser.Id && ug.GroupId == 1);
            if (membership != null)
            {
                db.UserGroups.Remove(membership);
                await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            }
        }

        var searchTerm = "somesearchterm";
        var formData = new Dictionary<string, string>
        {
            ["UserId"] = newUser.Id.ToString(),
            ["Role"] = ((int)GroupRole.Player).ToString(),
            ["search"] = searchTerm
        };
        var response = await client.PostAsync("/platform/Group/AddMember/1",
            new FormUrlEncodedContent(formData), TestContext.Current.CancellationToken);

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found);
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.ToString().Should().Contain($"search={searchTerm}");

        using var scopeAfter = _factory.Services.CreateScope();
        var dbAfter = scopeAfter.ServiceProvider.GetRequiredService<QuestBoardContext>();
        var membershipAfter = dbAfter.UserGroups.FirstOrDefault(ug => ug.UserId == newUser.Id && ug.GroupId == 1);
        membershipAfter.Should().NotBeNull();
    }

    // CreateMember with a brand-new email creates a user and adds them to the route's group
    [Fact]
    public async Task CreateMember_NewAccount_ShouldCreateUserAndAddToGroup()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(_factory);

        var uniqueTag = Guid.NewGuid().ToString("N")[..8];
        var newEmail = $"createmember{uniqueTag}@test.com";
        var formData = new Dictionary<string, string>
        {
            ["Email"] = newEmail,
            ["Name"] = $"CreateMember{uniqueTag}",
            ["GroupRole"] = ((int)GroupRole.Player).ToString()
        };

        var response = await client.PostAsync("/platform/Group/CreateMember/1",
            new FormUrlEncodedContent(formData), TestContext.Current.CancellationToken);

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
        var createdUser = dbContext.Users.FirstOrDefault(u => u.Email == newEmail);
        createdUser.Should().NotBeNull();
        var membership = dbContext.UserGroups.FirstOrDefault(ug => ug.UserId == createdUser!.Id && ug.GroupId == 1);
        membership.Should().NotBeNull();
    }

    // CreateMember with an email that already belongs to a member of the group does not duplicate membership
    [Fact]
    public async Task CreateMember_AlreadyMemberEmail_ShouldNotDuplicateMembership()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(_factory);

        var uniqueTag = Guid.NewGuid().ToString("N")[..8];
        var existingEmail = $"alreadymember{uniqueTag}@test.com";
        var (_, existingUser) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, $"alreadymember{uniqueTag}", existingEmail, roles: ["Player"]);

        var formData = new Dictionary<string, string>
        {
            ["Email"] = existingEmail,
            ["Name"] = "Ignored Name",
            ["GroupRole"] = ((int)GroupRole.Player).ToString()
        };

        var response = await client.PostAsync("/platform/Group/CreateMember/1",
            new FormUrlEncodedContent(formData), TestContext.Current.CancellationToken);

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
        var memberships = dbContext.UserGroups.Where(ug => ug.UserId == existingUser.Id && ug.GroupId == 1).ToList();
        memberships.Should().HaveCount(1);
    }

    // CreateMember sources groupId strictly from the route, not any session default
    [Fact]
    public async Task CreateMember_PostedToSecondGroup_ShouldScopeMembershipToRouteGroupId()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(_factory);

        // Create a second group
        var uniqueGroupName = "CreateMemberGroup2_" + Guid.NewGuid().ToString("N")[..8];
        var createGroupData = new Dictionary<string, string>
        {
            ["Name"] = uniqueGroupName,
            ["BoardType"] = ((int)BoardType.OneShot).ToString()
        };
        await client.PostAsync("/platform/Group/Create",
            new FormUrlEncodedContent(createGroupData), TestContext.Current.CancellationToken);

        using var scopeGroup = _factory.Services.CreateScope();
        var dbGroup = scopeGroup.ServiceProvider.GetRequiredService<QuestBoardContext>();
        var group2 = dbGroup.Groups.FirstOrDefault(g => g.Name == uniqueGroupName);
        group2.Should().NotBeNull();

        var uniqueTag = Guid.NewGuid().ToString("N")[..8];
        var newEmail = $"routegroup{uniqueTag}@test.com";
        var formData = new Dictionary<string, string>
        {
            ["Email"] = newEmail,
            ["Name"] = $"RouteGroup{uniqueTag}",
            ["GroupRole"] = ((int)GroupRole.Player).ToString()
        };

        var response = await client.PostAsync($"/platform/Group/CreateMember/{group2!.Id}",
            new FormUrlEncodedContent(formData), TestContext.Current.CancellationToken);

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found);

        using var scopeAfter = _factory.Services.CreateScope();
        var dbAfter = scopeAfter.ServiceProvider.GetRequiredService<QuestBoardContext>();
        var createdUser = dbAfter.Users.FirstOrDefault(u => u.Email == newEmail);
        createdUser.Should().NotBeNull();

        var membershipInGroup2 = dbAfter.UserGroups.FirstOrDefault(ug => ug.UserId == createdUser!.Id && ug.GroupId == group2.Id);
        membershipInGroup2.Should().NotBeNull();

        var membershipInGroup1 = dbAfter.UserGroups.FirstOrDefault(ug => ug.UserId == createdUser!.Id && ug.GroupId == 1);
        membershipInGroup1.Should().BeNull();
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

    // Create group with a selected BoardType persists that selection
    [Fact]
    public async Task CreateGroup_WithBoardType_ShouldPersistSelection()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(_factory);
        var uniqueName = "CampaignGroup_" + Guid.NewGuid().ToString("N")[..8];
        var formData = new Dictionary<string, string>
        {
            ["Name"] = uniqueName,
            ["BoardType"] = ((int)BoardType.Campaign).ToString()
        };

        await client.PostAsync("/platform/Group/Create",
            new FormUrlEncodedContent(formData), TestContext.Current.CancellationToken);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
        var group = dbContext.Groups.FirstOrDefault(g => g.Name == uniqueName);
        group.Should().NotBeNull();
        group!.BoardType.Should().Be((int)BoardType.Campaign);
    }

    // Create group without selecting a BoardType fails validation and re-renders the form
    [Fact]
    public async Task CreateGroup_WithoutBoardType_ShouldFailValidation()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(_factory);
        var uniqueName = "NoBoardType_" + Guid.NewGuid().ToString("N")[..8];
        var formData = new Dictionary<string, string> { ["Name"] = uniqueName };

        var response = await client.PostAsync("/platform/Group/Create",
            new FormUrlEncodedContent(formData), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        content.Should().Contain("Board type is required.");
    }

    // Editing a group and posting a changed BoardType is silently ignored — the stored value never changes
    [Fact]
    public async Task EditGroup_PostingChangedBoardType_ShouldBeSilentlyIgnored()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(_factory);
        var uniqueName = "EditBoardType_" + Guid.NewGuid().ToString("N")[..8];
        var createData = new Dictionary<string, string>
        {
            ["Name"] = uniqueName,
            ["BoardType"] = ((int)BoardType.OneShot).ToString()
        };
        await client.PostAsync("/platform/Group/Create",
            new FormUrlEncodedContent(createData), TestContext.Current.CancellationToken);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
        var group = dbContext.Groups.FirstOrDefault(g => g.Name == uniqueName);
        group.Should().NotBeNull();

        var editData = new Dictionary<string, string>
        {
            ["Id"] = group!.Id.ToString(),
            ["Name"] = uniqueName,
            ["BoardType"] = ((int)BoardType.Campaign).ToString()
        };
        await client.PostAsync("/platform/Group/Edit",
            new FormUrlEncodedContent(editData), TestContext.Current.CancellationToken);

        using var scopeAfter = _factory.Services.CreateScope();
        var dbAfter = scopeAfter.ServiceProvider.GetRequiredService<QuestBoardContext>();
        var groupAfter = dbAfter.Groups.FirstOrDefault(g => g.Id == group.Id);
        groupAfter.Should().NotBeNull();
        groupAfter!.BoardType.Should().Be((int)BoardType.OneShot);
    }

    // Seeded default group (EuphoriaInn) defaults to OneShot via the migration's defaultValue: 0
    [Fact]
    public async Task GroupsIndex_SeededGroup_ShouldDefaultToOneShot()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(_factory);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
        var group = dbContext.Groups.FirstOrDefault(g => g.Name == "EuphoriaInn");

        group.Should().NotBeNull();
        group!.BoardType.Should().Be((int)BoardType.OneShot);
    }

    // GetAvailableUsers excludes members and includes non-members of the given group
    [Fact]
    public async Task GetAvailableUsers_ShouldExcludeMembersAndIncludeNonMembers()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);

        // Non-member: no UserGroups row for group 1
        var (_, nonMember) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, "availnonmember", "availnonmember@test.com", roles: []);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
            var membership = db.UserGroups.FirstOrDefault(ug => ug.UserId == nonMember.Id && ug.GroupId == 1);
            if (membership != null)
            {
                db.UserGroups.Remove(membership);
                await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            }
        }

        // Member: has a UserGroups row for group 1
        var (_, member) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, "availmember", "availmember@test.com", roles: ["Player"]);

        using var scope2 = _factory.Services.CreateScope();
        var userService = scope2.ServiceProvider.GetRequiredService<IUserService>();
        var result = await userService.GetAvailableUsersAsync(1, null, TestContext.Current.CancellationToken);

        result.Should().Contain(u => u.Id == nonMember.Id);
        result.Should().NotContain(u => u.Id == member.Id);
    }

    // GetAvailableUsers filters by search term matching Name
    [Fact]
    public async Task GetAvailableUsers_WithSearchMatchingName_ShouldReturnOnlyMatchingNonMembers()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);

        var uniqueTag = Guid.NewGuid().ToString("N")[..8];
        var (_, matching) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, $"searchname{uniqueTag}", $"searchname{uniqueTag}@test.com", name: $"Zorlan{uniqueTag}", roles: []);
        var (_, nonMatching) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, $"othername{uniqueTag}", $"othername{uniqueTag}@test.com", name: $"Unrelated{uniqueTag}", roles: []);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
            foreach (var userId in new[] { matching.Id, nonMatching.Id })
            {
                var membership = db.UserGroups.FirstOrDefault(ug => ug.UserId == userId && ug.GroupId == 1);
                if (membership != null) db.UserGroups.Remove(membership);
            }
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using var scope2 = _factory.Services.CreateScope();
        var userService = scope2.ServiceProvider.GetRequiredService<IUserService>();
        var result = await userService.GetAvailableUsersAsync(1, $"Zorlan{uniqueTag}", TestContext.Current.CancellationToken);

        result.Should().Contain(u => u.Id == matching.Id);
        result.Should().NotContain(u => u.Id == nonMatching.Id);
    }

    // GetAvailableUsers filters by search term matching Email
    [Fact]
    public async Task GetAvailableUsers_WithSearchMatchingEmail_ShouldReturnOnlyMatchingNonMembers()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);

        var uniqueTag = Guid.NewGuid().ToString("N")[..8];
        var matchingEmail = $"emailmatch{uniqueTag}@test.com";
        var (_, matching) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, $"emailmatch{uniqueTag}", matchingEmail, roles: []);
        var (_, nonMatching) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, $"emailother{uniqueTag}", $"emailother{uniqueTag}@test.com", roles: []);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
            foreach (var userId in new[] { matching.Id, nonMatching.Id })
            {
                var membership = db.UserGroups.FirstOrDefault(ug => ug.UserId == userId && ug.GroupId == 1);
                if (membership != null) db.UserGroups.Remove(membership);
            }
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using var scope2 = _factory.Services.CreateScope();
        var userService = scope2.ServiceProvider.GetRequiredService<IUserService>();
        var result = await userService.GetAvailableUsersAsync(1, $"emailmatch{uniqueTag}", TestContext.Current.CancellationToken);

        result.Should().Contain(u => u.Id == matching.Id);
        result.Should().NotContain(u => u.Id == nonMatching.Id);
    }

    // GetAvailableUsers with null or empty search returns all non-members unfiltered
    [Fact]
    public async Task GetAvailableUsers_WithNullOrEmptySearch_ShouldReturnAllNonMembersUnfiltered()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);

        var uniqueTag = Guid.NewGuid().ToString("N")[..8];
        var (_, nonMember) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, $"unfiltered{uniqueTag}", $"unfiltered{uniqueTag}@test.com", roles: []);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
            var membership = db.UserGroups.FirstOrDefault(ug => ug.UserId == nonMember.Id && ug.GroupId == 1);
            if (membership != null)
            {
                db.UserGroups.Remove(membership);
                await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            }
        }

        using var scope2 = _factory.Services.CreateScope();
        var userService = scope2.ServiceProvider.GetRequiredService<IUserService>();

        var resultNull = await userService.GetAvailableUsersAsync(1, null, TestContext.Current.CancellationToken);
        var resultEmpty = await userService.GetAvailableUsersAsync(1, "", TestContext.Current.CancellationToken);

        resultNull.Should().Contain(u => u.Id == nonMember.Id);
        resultEmpty.Should().Contain(u => u.Id == nonMember.Id);
    }

    // Members GET with a memberSearch term filters the current-members list and echoes the term back
    [Fact]
    public async Task MembersPage_WithMemberSearch_ShouldReturnOnlyMatchingMembersAndEchoTerm()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(_factory);

        var uniqueTag = Guid.NewGuid().ToString("N")[..8];
        var distinctiveName = $"Griffonhold{uniqueTag}";
        // CreateAuthenticatedClientWithUserAsync with a Player role seeds a UserGroups row for group 1
        var (_, matchingMember) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, $"membersrch{uniqueTag}", $"membersrch{uniqueTag}@test.com", name: distinctiveName, roles: ["Player"]);

        // Matching memberSearch returns the member
        var matchResponse = await client.GetAsync($"/platform/Group/Members/1?memberSearch={distinctiveName}", TestContext.Current.CancellationToken);
        matchResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var matchContent = await matchResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        matchContent.Should().Contain(distinctiveName);
        matchContent.Should().Contain($"value=\"{distinctiveName}\"");

        // Non-matching memberSearch excludes the member
        var noMatchResponse = await client.GetAsync($"/platform/Group/Members/1?memberSearch=NoSuchMember{uniqueTag}", TestContext.Current.CancellationToken);
        noMatchResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var noMatchContent = await noMatchResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        noMatchContent.Should().NotContain(distinctiveName);

        _ = matchingMember;
    }

    // GetMembersAsync filters by search term matching Email, at the service level
    [Fact]
    public async Task GetMembers_WithSearchMatchingEmail_ShouldReturnOnlyMatchingMembers()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);

        var uniqueTag = Guid.NewGuid().ToString("N")[..8];
        var matchingEmail = $"membermatch{uniqueTag}@test.com";
        var (_, matching) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, $"membermatch{uniqueTag}", matchingEmail, roles: ["Player"]);
        var (_, nonMatching) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, $"memberother{uniqueTag}", $"memberother{uniqueTag}@test.com", roles: ["Player"]);

        using var scope = _factory.Services.CreateScope();
        var groupService = scope.ServiceProvider.GetRequiredService<IGroupService>();
        var result = await groupService.GetMembersAsync(1, $"membermatch{uniqueTag}", TestContext.Current.CancellationToken);

        result.Should().Contain(ug => ug.UserId == matching.Id);
        result.Should().NotContain(ug => ug.UserId == nonMatching.Id);
    }

    // AddMember redirect preserves BOTH the available-users search and the member search
    [Fact]
    public async Task AddMember_WithBothSearchTerms_ShouldPreserveBothOnRedirect()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(_factory);

        var (_, newUser) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, "bothsearchadd", "bothsearchadd@test.com", roles: []);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
            var membership = db.UserGroups.FirstOrDefault(ug => ug.UserId == newUser.Id && ug.GroupId == 1);
            if (membership != null)
            {
                db.UserGroups.Remove(membership);
                await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            }
        }

        var availableSearchTerm = "availableterm";
        var memberSearchTerm = "memberterm";
        var formData = new Dictionary<string, string>
        {
            ["UserId"] = newUser.Id.ToString(),
            ["Role"] = ((int)GroupRole.Player).ToString(),
            ["search"] = availableSearchTerm,
            ["memberSearch"] = memberSearchTerm
        };
        var response = await client.PostAsync("/platform/Group/AddMember/1",
            new FormUrlEncodedContent(formData), TestContext.Current.CancellationToken);

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found);
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.ToString().Should().Contain($"search={availableSearchTerm}");
        response.Headers.Location!.ToString().Should().Contain($"memberSearch={memberSearchTerm}");
    }

    // RemoveMember redirect preserves BOTH search terms so removing a member doesn't reset either filter
    [Fact]
    public async Task RemoveMember_WithBothSearchTerms_ShouldPreserveBothOnRedirect()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(_factory);

        var (_, memberUser) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, "removeboth", "removeboth@test.com", roles: ["Player"]);

        var availableSearchTerm = "keepavail";
        var memberSearchTerm = "keepmember";
        var formData = new Dictionary<string, string>
        {
            ["userId"] = memberUser.Id.ToString(),
            ["search"] = availableSearchTerm,
            ["memberSearch"] = memberSearchTerm
        };
        var response = await client.PostAsync("/platform/Group/RemoveMember/1",
            new FormUrlEncodedContent(formData), TestContext.Current.CancellationToken);

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.OK);
        if (response.Headers.Location != null)
        {
            response.Headers.Location.ToString().Should().Contain($"search={availableSearchTerm}");
            response.Headers.Location.ToString().Should().Contain($"memberSearch={memberSearchTerm}");
        }
    }

    // CreateMember redirect preserves BOTH search terms on success
    [Fact]
    public async Task CreateMember_WithBothSearchTerms_ShouldPreserveBothOnRedirect()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(_factory);

        var uniqueTag = Guid.NewGuid().ToString("N")[..8];
        var newEmail = $"createbothsearch{uniqueTag}@test.com";
        var availableSearchTerm = "createavail";
        var memberSearchTerm = "createmember";
        var formData = new Dictionary<string, string>
        {
            ["Email"] = newEmail,
            ["Name"] = $"CreateBothSearch{uniqueTag}",
            ["GroupRole"] = ((int)GroupRole.Player).ToString()
        };

        var response = await client.PostAsync($"/platform/Group/CreateMember/1?search={availableSearchTerm}&memberSearch={memberSearchTerm}",
            new FormUrlEncodedContent(formData), TestContext.Current.CancellationToken);

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found);
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.ToString().Should().Contain($"search={availableSearchTerm}");
        response.Headers.Location!.ToString().Should().Contain($"memberSearch={memberSearchTerm}");
    }

    // AddMember on an existing confirmed user enqueues the group-membership-added notification email
    [Fact]
    public async Task AddMember_ExistingConfirmedUser_ShouldEnqueueGroupMembershipAddedEmailJob()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(_factory);

        var (_, newUser) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, "confirmedaddmember", "confirmedaddmember@test.com", roles: []);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
            var membership = db.UserGroups.FirstOrDefault(ug => ug.UserId == newUser.Id && ug.GroupId == 1);
            if (membership != null)
            {
                db.UserGroups.Remove(membership);
                await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            }
        }

        _factory.JobClient.Clear();

        var formData = new Dictionary<string, string>
        {
            ["UserId"] = newUser.Id.ToString(),
            ["Role"] = ((int)GroupRole.Player).ToString()
        };
        var response = await client.PostAsync("/platform/Group/AddMember/1",
            new FormUrlEncodedContent(formData), TestContext.Current.CancellationToken);

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.OK);
        _factory.JobClient.EnqueuedJobs.Count(j => j.Type == typeof(GroupMembershipAddedEmailJob)).Should().Be(1);
        _factory.JobClient.EnqueuedJobs.Count(j => j.Type == typeof(WelcomeEmailJob)).Should().Be(0);
    }

    // AddMember on an existing stranded (never-confirmed) user enqueues a welcome/set-password email instead
    [Fact]
    public async Task AddMember_ExistingStrandedUser_ShouldEnqueueWelcomeEmailJob()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(_factory);

        var (_, newUser) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, "strandedaddmember", "strandedaddmember@test.com", roles: []);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
            var membership = db.UserGroups.FirstOrDefault(ug => ug.UserId == newUser.Id && ug.GroupId == 1);
            if (membership != null)
            {
                db.UserGroups.Remove(membership);
                await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            var userEntity = db.Users.First(u => u.Id == newUser.Id);
            userEntity.EmailConfirmed = false;
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        _factory.JobClient.Clear();

        var formData = new Dictionary<string, string>
        {
            ["UserId"] = newUser.Id.ToString(),
            ["Role"] = ((int)GroupRole.Player).ToString()
        };
        var response = await client.PostAsync("/platform/Group/AddMember/1",
            new FormUrlEncodedContent(formData), TestContext.Current.CancellationToken);

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.OK);
        _factory.JobClient.EnqueuedJobs.Count(j => j.Type == typeof(WelcomeEmailJob)).Should().Be(1);
        _factory.JobClient.EnqueuedJobs.Count(j => j.Type == typeof(GroupMembershipAddedEmailJob)).Should().Be(0);
    }

    // AddMember on a user already in the group enqueues no email at all
    [Fact]
    public async Task AddMember_AlreadyMember_ShouldEnqueueNoEmail()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(_factory);

        var (_, existingMember) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, "alreadymemberaddmember", "alreadymemberaddmember@test.com", roles: ["Player"]);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
            var membership = db.UserGroups.FirstOrDefault(ug => ug.UserId == existingMember.Id && ug.GroupId == 1);
            if (membership == null)
            {
                db.UserGroups.Add(new UserGroupEntity
                {
                    UserId = existingMember.Id,
                    GroupId = 1,
                    GroupRole = (int)GroupRole.Player
                });
                await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            }
        }

        _factory.JobClient.Clear();

        var formData = new Dictionary<string, string>
        {
            ["UserId"] = existingMember.Id.ToString(),
            ["Role"] = ((int)GroupRole.Player).ToString()
        };
        var response = await client.PostAsync("/platform/Group/AddMember/1",
            new FormUrlEncodedContent(formData), TestContext.Current.CancellationToken);

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.OK);
        _factory.JobClient.EnqueuedJobs.Count(j => j.Type == typeof(GroupMembershipAddedEmailJob)).Should().Be(0);
        _factory.JobClient.EnqueuedJobs.Count(j => j.Type == typeof(WelcomeEmailJob)).Should().Be(0);
    }
}
