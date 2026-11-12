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
        // Each sheet must be a stylesheet link in the page, not merely a name that appears somewhere in the HTML
        // (a comment, a script or an attribute would satisfy a bare substring match).
        foreach (var sheet in new[] { "components.css", "tokens.css", "base.css", "app.css" })
        {
            var linked = System.Text.RegularExpressions.Regex.IsMatch(html,
                @"<link\b[^>]*\brel=""stylesheet""[^>]*\bhref=""[^""]*/" + System.Text.RegularExpressions.Regex.Escape(sheet) + @"(?:\?[^""]*)?""");
            linked.Should().BeTrue($"the Platform layout must link {sheet} as a stylesheet so the cards render with their styles");
        }
    }
}
