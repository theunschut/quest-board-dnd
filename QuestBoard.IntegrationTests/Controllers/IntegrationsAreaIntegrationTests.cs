using System.Net;
using QuestBoard.IntegrationTests.Helpers;

namespace QuestBoard.IntegrationTests.Controllers;

/// <summary>
/// Integration tests for the instance-wide Omphalos Integrations settings page under /platform.
/// Covers the SuperAdminOnly policy: a SuperAdmin can reach the page; a group-scoped Admin,
/// a Player, and an unauthenticated user cannot.
/// </summary>
public class IntegrationsAreaIntegrationTests : IClassFixture<WebApplicationFactoryBase>
{
    private readonly WebApplicationFactoryBase _factory;

    public IntegrationsAreaIntegrationTests(WebApplicationFactoryBase factory)
    {
        _factory = factory;
    }

    // SuperAdmin can access the instance-wide Integrations page
    [Fact]
    public async Task Integrations_WhenSuperAdmin_ShouldReturn200()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(_factory);

        var response = await client.GetAsync("/platform/Integrations/Index", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // Non-SuperAdmin (regular player) receives 403 or redirect
    [Fact]
    public async Task Integrations_WhenNotSuperAdmin_ShouldDeny()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, "regularuser", "regular@test.com", roles: ["Player"]);

        var response = await client.GetAsync("/platform/Integrations/Index", TestContext.Current.CancellationToken);

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.Redirect);
    }

    // A group-scoped Admin (not SuperAdmin) does not get access to the instance-wide page
    [Fact]
    public async Task Integrations_WhenAdmin_ShouldDeny()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, "adminuser", "admin@test.com", roles: ["Admin"]);

        var response = await client.GetAsync("/platform/Integrations/Index", TestContext.Current.CancellationToken);

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.Redirect);
    }

    // Unauthenticated users are redirected from the instance-wide page
    [Fact]
    public async Task Integrations_WhenNotAuthenticated_ShouldRedirect()
    {
        var unauthClient = _factory.CreateNonRedirectingClient();

        var response = await unauthClient.GetAsync("/platform/Integrations/Index", TestContext.Current.CancellationToken);

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.Unauthorized);
    }
}
