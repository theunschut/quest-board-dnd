using QuestBoard.IntegrationTests.Helpers;
using System.Net;

namespace QuestBoard.IntegrationTests.Controllers;

/// <summary>
/// Integration tests for GroupSessionMiddleware: redirects an authenticated
/// user whose group session has expired (no ActiveGroupId) to the group picker, exempts
/// SuperAdmin and the picker/auth/platform/error paths from the redirect, and passes requests
/// through untouched when an active group is present.
///
/// TestGroupContext (MutableGroupContext) is a shared singleton registered on the factory, so
/// each test explicitly sets factory.TestGroupContext.ActiveGroupId at the start and restores
/// it to 1 in a finally block to avoid cross-test bleed (mirrors TenantIsolationTests convention).
/// </summary>
public class GroupSessionMiddlewareIntegrationTests(WebApplicationFactoryBase factory) : IClassFixture<WebApplicationFactoryBase>
{
    // An authenticated non-SuperAdmin user with no active group session is redirected
    // by the middleware to the hardcoded /groups/pick path before reaching the controller.
    [Fact]
    public async Task AuthenticatedUser_NoActiveGroup_RedirectsToGroupPick()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "sessionrecoveryuser", "sessionrecovery@example.com", roles: ["Player"]);

        factory.TestGroupContext.ActiveGroupId = null;
        try
        {
            var response = await client.GetAsync("/quests", TestContext.Current.CancellationToken);

            response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found);
            var location = response.Headers.Location?.ToString() ?? string.Empty;
            location.Should().Contain("/groups/pick");
        }
        finally
        {
            factory.TestGroupContext.ActiveGroupId = 1;
        }
    }

    // SuperAdmin has no special exemption from the "must have an active group" gate — a
    // null ActiveGroupId is ambiguous (which group's data should render?), so SuperAdmin is
    // redirected to the picker exactly like every other role, on every group-scoped route.
    [Fact]
    public async Task SuperAdmin_NoActiveGroup_RedirectsToGroupPick()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(factory);

        factory.TestGroupContext.ActiveGroupId = null;
        try
        {
            var response = await client.GetAsync("/quests", TestContext.Current.CancellationToken);

            response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found);
            var location = response.Headers.Location?.ToString() ?? string.Empty;
            location.Should().Contain("/groups/pick");
        }
        finally
        {
            factory.TestGroupContext.ActiveGroupId = 1;
        }
    }

    // /groups/pick itself is on the exempt-path list — a user with no active group
    // hitting the picker must never be looped back to the picker by the middleware. The
    // picker controller's own single-group auto-redirect logic sends them onward to
    // the board instead, which is a different mechanism from the middleware under test here.
    [Fact]
    public async Task GroupPickPath_NoActiveGroup_NotLooped()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "grouppickpathuser", "grouppickpath@example.com", roles: ["Player"]);

        factory.TestGroupContext.ActiveGroupId = null;
        try
        {
            var response = await client.GetAsync("/groups/pick", TestContext.Current.CancellationToken);

            // Never looped back to /groups/pick — either 200 (the picker page itself) or a
            // redirect onward to the board for a single-group user, never a redirect
            // whose Location is /groups/pick again.
            if (response.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.Found)
            {
                var location = response.Headers.Location?.ToString() ?? string.Empty;
                location.Should().NotContain("/groups/pick");
            }
            else
            {
                response.StatusCode.Should().Be(HttpStatusCode.OK);
            }
        }
        finally
        {
            factory.TestGroupContext.ActiveGroupId = 1;
        }
    }

    // With an active group present, the middleware passes the request through
    // untouched and the authenticated user reaches the board normally.
    [Fact]
    public async Task AuthenticatedUser_WithActiveGroup_ReachesPage()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "activegroupuser", "activegroup@example.com", roles: ["Player"]);

        factory.TestGroupContext.ActiveGroupId = 1;
        try
        {
            var response = await client.GetAsync("/quests", TestContext.Current.CancellationToken);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }
        finally
        {
            factory.TestGroupContext.ActiveGroupId = 1;
        }
    }

    // The exempt-path skip-list is a hand-maintained literal array with no
    // compile-time link to the newly-protected controller areas. This test
    // pins down that Calendar, DungeonMaster, and QuestLog — none of which appear on the
    // exempt list — are actually gated by the middleware when no active group is selected, so
    // silent drift (an overly-broad future exempt-list edit accidentally covering one of these
    // routes) is caught by a failing test rather than discovered in production.
    [Theory]
    [InlineData("/Calendar")]
    [InlineData("/DungeonMaster/EditProfile")]
    [InlineData("/QuestLog")]
    public async Task AuthenticatedUser_NoActiveGroup_ProtectedAreaRedirectsToGroupPick(string path)
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "protectedareauser", "protectedarea@example.com", roles: ["DungeonMaster"]);

        factory.TestGroupContext.ActiveGroupId = null;
        try
        {
            var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

            response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found);
            var location = response.Headers.Location?.ToString() ?? string.Empty;
            location.Should().Contain("/groups/pick");
        }
        finally
        {
            factory.TestGroupContext.ActiveGroupId = 1;
        }
    }

    // Same protected-area sweep as above, but for SuperAdmin: with no
    // exempt-list entry covering Calendar/DungeonMaster/QuestLog, SuperAdmin must be gated on
    // these routes exactly like any other role when no active group is selected.
    [Theory]
    [InlineData("/Calendar")]
    [InlineData("/DungeonMaster/EditProfile")]
    [InlineData("/QuestLog")]
    public async Task SuperAdmin_NoActiveGroup_ProtectedAreaRedirectsToGroupPick(string path)
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(factory);

        factory.TestGroupContext.ActiveGroupId = null;
        try
        {
            var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

            response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found);
            var location = response.Headers.Location?.ToString() ?? string.Empty;
            location.Should().Contain("/groups/pick");
        }
        finally
        {
            factory.TestGroupContext.ActiveGroupId = 1;
        }
    }

    // A non-idempotent request (POST) hitting the group-gate while no active
    // group is selected must NOT be silently redirected — Response.Redirect emits a 302, which
    // browsers re-issue as a GET, silently dropping the submitted form body. The middleware must
    // instead return a distinguishable failure (409 Conflict) so the caller can detect and
    // surface a "please pick a group and retry" message instead of losing data silently.
    [Fact]
    public async Task AuthenticatedUser_NoActiveGroup_PostRequestReturnsConflictInsteadOfSilentRedirect()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "postgateuser", "postgate@example.com", roles: ["Player"]);

        factory.TestGroupContext.ActiveGroupId = null;
        try
        {
            var response = await client.PostAsync(
                "/Quest/Create", new FormUrlEncodedContent([]), TestContext.Current.CancellationToken);

            response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        }
        finally
        {
            factory.TestGroupContext.ActiveGroupId = 1;
        }
    }

    // The middleware's redirect to the group picker must preserve the
    // original deep-linked destination as a returnUrl so GroupPickerController can send the
    // user back to what they originally requested instead of silently teleporting them home.
    [Fact]
    public async Task AuthenticatedUser_NoActiveGroup_RedirectPreservesReturnUrl()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "returnurluser", "returnurl@example.com", roles: ["Player"]);

        factory.TestGroupContext.ActiveGroupId = null;
        try
        {
            var response = await client.GetAsync("/Calendar?year=2026&month=7", TestContext.Current.CancellationToken);

            response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found);
            var location = response.Headers.Location?.ToString() ?? string.Empty;
            location.Should().Contain("/groups/pick");
            location.Should().Contain(Uri.EscapeDataString("/Calendar?year=2026&month=7"));
        }
        finally
        {
            factory.TestGroupContext.ActiveGroupId = 1;
        }
    }
}
