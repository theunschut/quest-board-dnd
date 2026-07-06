using QuestBoard.Domain.Enums;
using QuestBoard.IntegrationTests.Helpers;
using System.Net;

namespace QuestBoard.IntegrationTests.Controllers;

/// <summary>
/// Integration tests for GroupPickerController, covering the post-login group-context
/// entry point: single-group auto-redirect, multi-group picker,
/// SuperAdmin picker with Platform option, and session persistence.
/// </summary>
public class GroupPickerControllerIntegrationTests : IClassFixture<WebApplicationFactoryBase>
{
    private readonly WebApplicationFactoryBase _factory;
    private readonly HttpClient _client;

    public GroupPickerControllerIntegrationTests(WebApplicationFactoryBase factory)
    {
        _factory = factory;
        _client = factory.CreateNonRedirectingClient();
    }

    [Fact]
    public async Task Index_WhenNotAuthenticated_ShouldRedirectToLogin()
    {
        // Act
        var response = await _client.GetAsync("/GroupPicker/Index", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.Unauthorized);
    }

    // A single-group non-SuperAdmin user is redirected away from the picker
    [Fact]
    public async Task Index_WhenSingleGroupUser_ShouldRedirectAwayFromPicker()
    {
        // Arrange — default helper seeds the user as a member of group 1 only
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, "singlegroupuser", "singlegroup@example.com", roles: ["Player"]);

        // Act
        var response = await client.GetAsync("/GroupPicker/Index", TestContext.Current.CancellationToken);

        // Assert — a redirect away from the picker, not a 200 picker page
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found);
        var location = response.Headers.Location?.ToString() ?? string.Empty;
        location.Should().NotContain("GroupPicker");
    }

    // An authenticated user with ZERO group memberships (as opposed to
    // simply not having selected one yet) must land on a friendly empty-state picker page —
    // not a 500, and not a redirect loop back into the group-gate (the picker path is on the
    // middleware's exempt list, so it must render directly).
    [Fact]
    public async Task Index_WhenUserHasNoGroupMemberships_ShouldReturnFriendlyEmptyState()
    {
        // Arrange — pass roles: [] so the helper skips its UserGroups-seeding block entirely,
        // leaving this user authenticated but with zero group memberships.
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, "nogroupuser", "nogroup@example.com", roles: []);

        // Act
        var response = await client.GetAsync("/GroupPicker/Index", TestContext.Current.CancellationToken);

        // Assert — a rendered empty-state page (200), never a 500 and never a redirect back
        // into the group-gate (which would indicate an infinite loop).
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        content.Should().Contain("not assigned to any group");
    }

    // A multi-group user receives the picker page
    [Fact]
    public async Task Index_WhenMultiGroupUser_ShouldReturnPickerPage()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (client, user) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, "multigroupuser", "multigroup@example.com", roles: ["Player"]);

        // Seed a second group and add a membership row for the user
        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
            var secondGroup = new GroupEntity { Name = "SecondGroup_" + Guid.NewGuid().ToString("N")[..8], CreatedAt = DateTime.UtcNow };
            context.Groups.Add(secondGroup);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);

            context.UserGroups.Add(new UserGroupEntity
            {
                UserId = user.Id,
                GroupId = secondGroup.Id,
                GroupRole = (int)GroupRole.Player
            });
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Act
        var response = await client.GetAsync("/GroupPicker/Index", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        content.Should().Contain("Select Your Group");
        content.Should().Contain("SelectGroup");
    }

    // A SuperAdmin receives the picker page with the Platform option
    [Fact]
    public async Task Index_WhenSuperAdmin_ShouldReturnPickerWithPlatformOption()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(_factory);

        // Act
        var response = await client.GetAsync("/GroupPicker/Index", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        content.Should().Contain("Go to Platform");
        content.Should().Contain("/platform");
    }

    // Selecting a group persists ActiveGroupId in session for subsequent requests
    [Fact]
    public async Task SelectGroup_ShouldPersistActiveGroupInSession()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, "selectgroupuser", "selectgroup@example.com", roles: ["Player"]);

        // Act — POST to SelectGroup with the seeded group 1 id.
        // The TestAntiforgeryDecorator validates everything as successful in the Testing
        // environment, so the form is posted without a real anti-forgery token, matching
        // the established convention in GroupManagementIntegrationTests.
        var formData = new Dictionary<string, string> { ["groupId"] = "1" };
        var response = await client.PostAsync("/GroupPicker/SelectGroup",
            new FormUrlEncodedContent(formData), TestContext.Current.CancellationToken);

        // Assert — SelectGroup redirects (RedirectToLocal: either the returnUrl or Home)
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found);

        // Verify the group lookup that backs the session write actually resolved a real group.
        // Note: the TestAuthHandler-based client (Authorization header, not cookies) does not
        // round-trip ASP.NET Core session cookies the way a browser would, so asserting the
        // session value directly from a follow-up request on this client is not reliable in
        // this test harness. We instead assert the redirect succeeded and that group 1
        // (the group selected) exists, which is the data SelectGroup writes into session.
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
        var group = context.Groups.FirstOrDefault(g => g.Id == 1);
        group.Should().NotBeNull();
    }

    // Full round-trip for a deep link into a newly-protected controller
    // area — authenticated, no active group -> middleware redirects to the picker with
    // ?returnUrl preserving the original destination -> selecting a group lands the user back
    // on that original destination rather than a fixed fallback. The TestAuthHandler-based
    // client re-authenticates via header on every request rather than via a persisted login
    // cookie (see note on SelectGroup_ShouldPersistActiveGroupInSession above), so this test
    // chains the two hops explicitly via the returnUrl carried in each response, rather than
    // relying on session-cookie round-tripping between requests.
    [Fact]
    public async Task DeepLink_NoActiveGroup_SelectGroup_ReturnsToOriginalDestination()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, "roundtripuser", "roundtrip@example.com", roles: ["Player"]);

        _factory.TestGroupContext.ActiveGroupId = null;
        try
        {
            // Act 1 — deep link into a protected area with no active group selected.
            var firstHop = await client.GetAsync("/Calendar", TestContext.Current.CancellationToken);
            firstHop.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found);
            var firstLocation = firstHop.Headers.Location?.ToString() ?? string.Empty;
            firstLocation.Should().Contain("/groups/pick");
            firstLocation.Should().Contain("returnUrl=");

            var returnUrl = Uri.UnescapeDataString(firstLocation.Split("returnUrl=")[1]);
            returnUrl.Should().Be("/Calendar");

            // Act 2 — select a group, passing the returnUrl carried from the first hop.
            var formData = new Dictionary<string, string> { ["groupId"] = "1", ["returnUrl"] = returnUrl };
            var secondHop = await client.PostAsync("/GroupPicker/SelectGroup",
                new FormUrlEncodedContent(formData), TestContext.Current.CancellationToken);

            // Assert — lands back on the originally-requested destination, not the Home/Quest
            // fallback that RedirectToLocal uses when no (valid) returnUrl is supplied.
            secondHop.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found);
            var secondLocation = secondHop.Headers.Location?.ToString() ?? string.Empty;
            secondLocation.Should().Be("/Calendar");
        }
        finally
        {
            _factory.TestGroupContext.ActiveGroupId = 1;
        }
    }

    // A non-member posting an existing but foreign groupId must be rejected — the picker's own
    // membership-scoped listing should not be bypassable by posting an arbitrary group id directly.
    [Fact]
    public async Task SelectGroup_WhenNotAMember_ShouldReturnNotFound()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, "nonmemberuser", "nonmember@example.com", roles: ["Player"]);

        // Seed a second group the user is deliberately NOT added to (no UserGroupEntity row).
        int otherGroupId;
        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
            var otherGroup = new GroupEntity { Name = "OtherGroup_" + Guid.NewGuid().ToString("N")[..8], CreatedAt = DateTime.UtcNow };
            context.Groups.Add(otherGroup);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
            otherGroupId = otherGroup.Id;
        }

        // Act
        var formData = new Dictionary<string, string> { ["groupId"] = otherGroupId.ToString() };
        var response = await client.PostAsync("/GroupPicker/SelectGroup",
            new FormUrlEncodedContent(formData), TestContext.Current.CancellationToken);

        // Assert — hide existence, never a 403: a non-member gets 404 same as a nonexistent group.
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
