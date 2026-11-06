using System.Net;
using QuestBoard.IntegrationTests.Helpers;

namespace QuestBoard.IntegrationTests.Controllers;

/// <summary>
/// Verifies that Platform area views render with all required stylesheets linked,
/// particularly the component stylesheet that draws the cards six Platform views depend on.
/// </summary>
public class PlatformAreaLayoutTests(WebApplicationFactoryBase factory)
    : IClassFixture<WebApplicationFactoryBase>
{
    [Fact]
    public async Task PlatformGroupIndex_RendersTheComponentStylesheetLink()
    {
        // The Platform area's Group management page displays cards that depend on the component
        // stylesheet, and that stylesheet only draws them correctly after the token, base and app
        // sheets it builds on. Verify that all four links are present in the rendered HTML so cards
        // render with proper styling.
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "platform_user", "platform_user@example.com", roles: ["SuperAdmin"]);

        var response = await client.GetAsync("/Platform/Group", TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        // The stylesheet links must be present in the rendered HTML.
        foreach (var sheet in new[] { "components.css", "tokens.css", "base.css", "app.css" })
        {
            html.Should().Contain(sheet, because: $"the Platform layout must load {sheet} so the cards render with their styles");
        }
    }
}
