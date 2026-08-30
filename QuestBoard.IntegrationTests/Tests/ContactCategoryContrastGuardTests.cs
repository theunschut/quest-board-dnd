using QuestBoard.IntegrationTests.Helpers;
using System.Net;

namespace QuestBoard.IntegrationTests.Tests;

// Guards styling contracts the rest of the suite provably cannot see. 668 integration tests
// passed while both defects these facts pin were live on the running app -- a server-side
// integration test cannot observe a computed style, but it can prove a scoped CSS rule exists
// with the right colour, that the rule's selector actually reaches the element it targets
// (asserted structurally against rendered HTML, not assumed), and that the mobile view file is
// genuinely the one the server selects under a real User-Agent. The final pixel that paints is
// confirmable only by eye and is tracked separately as a UAT human-verify item.
public class ContactCategoryContrastGuardTests(WebApplicationFactoryBase factory) : IClassFixture<WebApplicationFactoryBase>
{
    private const string MobileUserAgent =
        "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1";

    // Attaches the mobile user agent header to a request and sends it through the supplied
    // authenticated client, so the client's default authorization header still applies.
    private async Task<(HttpResponseMessage Response, string Html)> GetMobileAsync(HttpClient client, string url)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("User-Agent", MobileUserAgent);
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        return (response, html);
    }

    // Resolves a stylesheet's path by walking up from AppContext.BaseDirectory until a
    // "QuestBoard.Service/wwwroot/css/{fileName}" descendant is found. Fails with a descriptive
    // message naming the attempted path when it cannot be resolved, so the facts below work
    // from any working directory.
    private static string ResolveCssPath(string fileName)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "QuestBoard.Service", "wwwroot", "css", fileName);
            if (File.Exists(candidate))
                return candidate;

            dir = dir.Parent;
        }

        var attemptedBase = AppContext.BaseDirectory;
        var attemptedPath = Path.Combine(attemptedBase, "QuestBoard.Service", "wwwroot", "css", fileName);
        throw new FileNotFoundException(
            $"{fileName} not found. Searched upward from '{attemptedBase}'. " +
            $"Last attempted path: '{attemptedPath}'. " +
            $"Ensure QuestBoard.Service/wwwroot/css/{fileName} exists in the repo.",
            attemptedPath);
    }

    // Extracts the body of a single CSS rule given the exact "selector {" text that opens it, so
    // an assertion can be scoped to that one rule instead of matching a declaration that happens
    // to repeat elsewhere in the file. Returns an empty string when the selector is not found, so
    // a deleted rule fails the containing assertion instead of throwing.
    private static string ExtractCssRule(string css, string selectorWithOpenBrace)
    {
        var start = css.IndexOf(selectorWithOpenBrace, StringComparison.Ordinal);
        if (start < 0)
            return string.Empty;

        var end = css.IndexOf('}', start);
        return end < 0 ? string.Empty : css[start..(end + 1)];
    }

    [Fact]
    public async Task ContactCategoryContrastGuard_ManagementPageLabel_RendersInsideScopedFormOnMobileOnly()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (dmClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "contrast_guard_mgmt_dm", "contrast_guard_mgmt_dm@example.com", roles: ["DungeonMaster"]);

        var (mobileResponse, mobileHtml) = await GetMobileAsync(dmClient, "/ContactCategoryManagement");
        mobileResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            because: "a DungeonMaster must be able to load the Manage Categories page under a real mobile User-Agent");

        var formStart = mobileHtml.IndexOf("category-mgmt-add-form", StringComparison.Ordinal);
        formStart.Should().BeGreaterThan(-1,
            because: "the add-category <form> in Manage.Mobile.cshtml must carry the category-mgmt-add-form scoping class, " +
                     "or the new label rule's selector matches nothing -- the precise failure mode of the original defect");

        var formEnd = mobileHtml.IndexOf("</form>", formStart, StringComparison.Ordinal);
        formEnd.Should().BeGreaterThan(-1,
            because: "the scoped add-category form must close with </form> after the class token appears");

        var formSlice = mobileHtml[formStart..(formEnd + "</form>".Length)];

        formSlice.Should().Contain("form-label",
            because: "the label styled by the new rule must actually sit inside the scoped form, not merely exist elsewhere on the page");
        formSlice.Should().Contain("New Category Name",
            because: "the specific label text UAT found unreadable must be the one enclosed by the scoped form");

        var desktopResponse = await dmClient.GetAsync("/ContactCategoryManagement", TestContext.Current.CancellationToken);
        var desktopHtml = await desktopResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        desktopResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        desktopHtml.Should().NotContain("category-mgmt-add-form",
            because: "a request with no mobile User-Agent must select Manage.cshtml, proving the mobile file is the one the fix targets");
    }

    [Fact]
    public void ContactCategoryContrastGuard_MobileAddFormLabelRule_SetsParchmentColourAndShadow()
    {
        var cssPath = ResolveCssPath("contacts.mobile.css");
        var css = File.ReadAllText(cssPath);
        var rule = ExtractCssRule(css, ".category-mgmt-add-form .form-label {");

        rule.Should().NotBeEmpty(
            because: $"contacts.mobile.css at '{cssPath}' must contain a scoped .category-mgmt-add-form .form-label rule -- " +
                     "a bare .form-label rule at file top level would be a failure of this criterion");

        var lowerRule = rule.ToLowerInvariant();
        lowerRule.Should().Contain("#f4e4bc",
            because: "the mobile add-category label must resolve to the parchment token, not Bootstrap's default near-black rgb(33,37,41)");
        lowerRule.Should().Contain("text-shadow",
            because: "the label needs the same drop shadow every sibling mobile .form-label rule uses to stay legible over the notice-board background");
    }
}
