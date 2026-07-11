using System.Net;
using QuestBoard.IntegrationTests.Helpers;

namespace QuestBoard.IntegrationTests.Controllers;

/// <summary>
/// Integration tests for the group-override Omphalos Integrations settings page under /AdminIntegrations.
/// Covers the AdminOnly policy (group-scoped): a group Admin can reach the page; a DungeonMaster
/// is explicitly excluded; a Player cannot reach it; SuperAdmin bypasses via the existing AdminOnly
/// policy; an unauthenticated user is redirected.
/// </summary>
public class AdminIntegrationsAuthorizationTests : IClassFixture<WebApplicationFactoryBase>
{
    private readonly WebApplicationFactoryBase _factory;

    public AdminIntegrationsAuthorizationTests(WebApplicationFactoryBase factory)
    {
        _factory = factory;
    }

    // AdminOnly allows GroupRole.Admin
    [Fact]
    public async Task GroupIntegrations_WhenGroupAdmin_ShouldReturn200()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, "groupadmin", "groupadmin@test.com", roles: ["Admin"]);

        var response = await client.GetAsync("/AdminIntegrations/Index", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // DungeonMaster is explicitly excluded from the group-override page
    [Fact]
    public async Task GroupIntegrations_WhenDungeonMaster_ShouldDeny()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, "groupdm", "groupdm@test.com", roles: ["DungeonMaster"]);

        var response = await client.GetAsync("/AdminIntegrations/Index", TestContext.Current.CancellationToken);

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.Redirect);
    }

    // Player is denied
    [Fact]
    public async Task GroupIntegrations_WhenPlayer_ShouldDeny()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, "groupplayer", "groupplayer@test.com", roles: ["Player"]);

        var response = await client.GetAsync("/AdminIntegrations/Index", TestContext.Current.CancellationToken);

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.Redirect);
    }

    // SuperAdmin bypasses AdminOnly, consistent with AdminHandler
    [Fact]
    public async Task GroupIntegrations_WhenSuperAdmin_ShouldReturn200()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(_factory);

        var response = await client.GetAsync("/AdminIntegrations/Index", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // Unauthenticated users are redirected from the group-override page
    [Fact]
    public async Task GroupIntegrations_WhenNotAuthenticated_ShouldRedirect()
    {
        var unauthClient = _factory.CreateNonRedirectingClient();

        var response = await unauthClient.GetAsync("/AdminIntegrations/Index", TestContext.Current.CancellationToken);

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.Unauthorized);
    }
}
