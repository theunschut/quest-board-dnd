using System.Net;
using QuestBoard.IntegrationTests.Helpers;

namespace QuestBoard.IntegrationTests.Tests;

/// <summary>
/// The error page never errors into a bare response when the services other pages lean on fail: with the user,
/// board-page and group services throwing from every method, a missing page, a direct visit to the error address
/// and a forced server error all still render the themed page.
/// </summary>
public class ErrorPageFallbackLayoutTests(ThrowingLayoutHelpersFactory factory) : IClassFixture<ThrowingLayoutHelpersFactory>
{
    [Fact]
    public async Task MissingPage_ForAnAnonymousViewer_RendersTheNotFoundPage()
    {
        var client = factory.CreateClient();

        var (status, body) = await ErrorPageRenderTests.SendAsync(client, ErrorPageRenderTests.Navigation("/no-such-page"));

        status.Should().Be(HttpStatusCode.NotFound);
        body.Should().Contain("Page not found");
        body.Should().Contain("Error 404");
    }

    [Fact]
    public async Task DirectErrorAddress_ForASignedInViewer_RendersWithoutSwitchBoard()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, $"fallback_{suffix}", $"fallback_{suffix}@test.com");

        var (status, body) = await ErrorPageRenderTests.SendAsync(client, ErrorPageRenderTests.Navigation("/Error/404"));

        status.Should().Be(HttpStatusCode.NotFound);
        body.Should().Contain("Page not found");
        body.Should().NotContain("Switch board");
    }

    [Fact]
    public async Task ForcedServerError_RendersTheServerErrorPage()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, $"fallback500_{suffix}", $"fallback500_{suffix}@test.com");

        var (status, body) = await ErrorPageRenderTests.SendAsync(client, ErrorPageRenderTests.Navigation("/Quest/Details/1"));

        status.Should().Be(HttpStatusCode.InternalServerError);
        body.Should().Contain("Something went wrong");
        body.Should().Contain("Error 500");
        body.Should().NotContain("Switch board");
    }
}
