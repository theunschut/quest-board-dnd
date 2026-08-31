using System.Net;
using QuestBoard.IntegrationTests.Helpers;

namespace QuestBoard.IntegrationTests.Controllers;

/// <summary>
/// Verifies that Platform area views render with all required stylesheets linked,
/// particularly the modern-card.css stylesheet that six Platform views depend on.
/// </summary>
public class PlatformAreaLayoutTests(WebApplicationFactoryBase factory)
    : IClassFixture<WebApplicationFactoryBase>
{
    [Fact]
    public async Task PlatformGroupIndex_RendersModernCardCssLink()
    {
        // The Platform area's Group management page displays cards that depend on modern-card.css.
        // Verify that the stylesheet link is present in the rendered HTML so cards render
        // with proper styling.
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "platform_user", "platform_user@example.com", roles: ["SuperAdmin"]);

        var response = await client.GetAsync("/Platform/Group", TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        // The stylesheet link must be present in the rendered HTML.
        html.Should().Contain("modern-card.css");
    }
}
