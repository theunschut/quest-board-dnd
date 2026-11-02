using System.Net;
using System.Net.Http.Headers;

namespace QuestBoard.IntegrationTests.Mobile;

/// <summary>
/// Tests for mobile.css content integrity and link presence in mobile responses.
///
/// Two test kinds:
/// 1. File-content tests — read the phone shell sheet and mobile.css from disk and assert the 44px touch-target
///    rule and the 16px body size
/// 2. Link-presence integration tests — mobile UA response links mobile.css; desktop UA response does not
/// </summary>
public class MobileCssTests : IClassFixture<WebApplicationFactoryBase>
{
    private const string MobileUserAgent =
        "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1";

    private const string DesktopUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

    private readonly HttpClient _client;

    public MobileCssTests(WebApplicationFactoryBase factory)
    {
        _client = factory.CreateClient();
    }

    private async Task<(HttpResponseMessage Response, string Html)> GetWithUserAgentAsync(string userAgent)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.TryAddWithoutValidation("User-Agent", userAgent);
        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        return (response, html);
    }

    /// <summary>
    /// Resolves the path to a stylesheet under QuestBoard.Service/wwwroot/css by walking up from
    /// AppContext.BaseDirectory until a directory holding it is found.
    /// Fails with a descriptive message if the path cannot be resolved.
    /// </summary>
    private static string ResolveCssPath(string fileName)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        // Walk up the directory tree to find the repo root — identified by
        // the presence of the stylesheet under an "QuestBoard.Service" child directory.
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "QuestBoard.Service", "wwwroot", "css", fileName);
            if (File.Exists(candidate))
                return candidate;

            dir = dir.Parent;
        }

        // Compute the attempted path for the failure message.
        var attemptedBase = AppContext.BaseDirectory;
        var attemptedPath = Path.Combine(attemptedBase, "QuestBoard.Service", "wwwroot", "css", fileName);
        throw new FileNotFoundException(
            $"{fileName} not found. Searched upward from '{attemptedBase}'. " +
            $"Last attempted path: '{attemptedPath}'. " +
            $"Ensure QuestBoard.Service/wwwroot/css/{fileName} exists in the repo.",
            attemptedPath);
    }

    /// <summary>
    /// File-content test: the phone shell sheet must declare the 44px touch-target rule over the control list,
    /// sized from the shared token, and the token must be 44px.
    /// Fails with a clear path-naming message if a file cannot be located.
    /// </summary>
    [Fact]
    public void MobileCss_File_Contains44pxTouchTargetRule()
    {
        // Arrange
        var shellPath = ResolveCssPath("shell.mobile.css");
        var shell = File.ReadAllText(shellPath);
        var tokensPath = ResolveCssPath("tokens.css");
        var tokens = File.ReadAllText(tokensPath);

        // Assert
        shell.Should().Contain(".mobile-layout :where(",
            because: $"shell.mobile.css at '{shellPath}' must scope the touch-target rule to the mobile layout at zero specificity");
        shell.Should().Contain("min-height: var(--qb-hit-min)",
            because: $"shell.mobile.css at '{shellPath}' must enforce a minimum touch target tall enough");
        shell.Should().Contain("min-width: var(--qb-hit-min)",
            because: $"shell.mobile.css at '{shellPath}' must enforce a minimum touch target wide enough");
        tokens.Should().Contain("--qb-hit-min: 44px",
            because: $"tokens.css at '{tokensPath}' defines the touch target size the shell rule reads");
    }

    /// <summary>
    /// File-content test: mobile.css must contain the 16px body font-size rule
    /// to prevent iOS input-focus auto-zoom.
    /// </summary>
    [Fact]
    public void MobileCss_File_Contains16pxBodyFontSize()
    {
        // Arrange
        var cssPath = ResolveCssPath("mobile.css");
        var css = File.ReadAllText(cssPath);

        // Assert
        css.Should().Contain("font-size: 16px",
            because: $"mobile.css at '{cssPath}' must set 16px body font-size to prevent iOS auto-zoom");
    }

    /// <summary>
    /// Link-presence test: a mobile User-Agent GET "/" response must link mobile.css
    /// (rendered by _Layout.Mobile.cshtml via asp-append-version).
    /// </summary>
    [Fact]
    public async Task MobileCss_MobileUserAgent_ResponseLinksStylesheet()
    {
        // Arrange + Act
        var (response, html) = await GetWithUserAgentAsync(MobileUserAgent);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("mobile.css",
            because: "a mobile-UA response must include the mobile.css stylesheet link");
    }

    /// <summary>
    /// Parity test: a desktop User-Agent GET "/" response must NOT link mobile.css
    /// and MUST still link its desktop legacy stylesheet, legacy-pages.css — confirming the desktop layout is unchanged.
    /// </summary>
    [Fact]
    public async Task MobileCss_DesktopUserAgent_ResponseDoesNotLinkMobileStylesheet()
    {
        // Arrange + Act
        var (response, html) = await GetWithUserAgentAsync(DesktopUserAgent);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().NotContain("mobile.css",
            because: "a desktop-UA response must not include the mobile.css stylesheet link");
        html.Should().Contain("legacy-pages.css",
            because: "a desktop-UA response must still link legacy-pages.css — desktop layout is unchanged");
    }
}
