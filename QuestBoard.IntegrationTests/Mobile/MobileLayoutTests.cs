using System.Net;
using System.Net.Http.Headers;

namespace QuestBoard.IntegrationTests.Mobile;

/// <summary>
/// Integration tests for the mobile layout infrastructure.
/// Drives the live app with mobile and desktop User-Agents to verify:
/// - Mobile UA renders offcanvas nav shell
/// - Mobile UA response includes "mobile-layout" body class
/// - Desktop UA response has no offcanvas/mobile-layout elements (parity)
/// - Mobile UA with no .Mobile.cshtml view still returns 200 with the mobile shell (fallback)
/// </summary>
public class MobileLayoutTests : IClassFixture<WebApplicationFactoryBase>
{
    private const string MobileUserAgent =
        "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1";

    private const string DesktopUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

    private readonly HttpClient _client;

    public MobileLayoutTests(WebApplicationFactoryBase factory)
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
    /// A mobile User-Agent request must render the Bootstrap offcanvas nav element
    /// with id="mobileNav".
    /// </summary>
    [Fact]
    public async Task MobileLayoutOffcanvas_MobileUserAgent_RendersOffcanvasNav()
    {
        // Arrange + Act
        var (response, html) = await GetWithUserAgentAsync(MobileUserAgent);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("offcanvas");
        html.Should().Contain("mobileNav");
    }

    /// <summary>
    /// A mobile User-Agent request must render a body element with the
    /// "mobile-layout" class, confirming _ViewStart.cshtml selected _Layout.Mobile.cshtml.
    /// </summary>
    [Fact]
    public async Task MobileLayout_MobileUserAgent_RendersBodyWithMobileLayoutClass()
    {
        // Arrange + Act
        var (response, html) = await GetWithUserAgentAsync(MobileUserAgent);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("mobile-layout");
    }

    /// <summary>
    /// Parity: A desktop User-Agent request must NOT contain the "mobile-layout"
    /// class or any offcanvas element, and MUST contain the desktop brand "D&amp;D Quest Board"
    /// to confirm the desktop layout rendered unchanged.
    /// </summary>
    [Fact]
    public async Task DesktopLayoutParity_DesktopUserAgent_HasNoMobileLayout()
    {
        // Arrange + Act
        var (response, html) = await GetWithUserAgentAsync(DesktopUserAgent);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().NotContain("mobile-layout");
        html.Should().NotContain("offcanvas");
        html.Should().NotContain("id=\"mobileNav\"");
        // Desktop layout brand text — confirms the desktop layout rendered (mobile shortens to "Quest Board")
        // The raw HTML contains literal "D&D Quest Board" (unencoded &) in the navbar-brand link text
        html.Should().Contain("D&D Quest Board");
    }

    /// <summary>
    /// Fallback path: When no .Mobile.cshtml view exists for the requested route,
    /// the mobile UA still returns HTTP 200 with the mobile shell (the expander falls back to
    /// the original .cshtml rendered inside _Layout.Mobile.cshtml).
    /// The desktop UA returns HTTP 200 with the desktop layout (no mobile-layout class).
    ///
    /// Note: The full assertion — that Index.Mobile.cshtml is served when it exists —
    /// lands when the first .Mobile.cshtml content view is added. This phase ships
    /// zero .Mobile.cshtml content views, so only the fallback path is asserted here.
    /// </summary>
    [Fact]
    public async Task MobileViewResolution_DesktopUserAgent_ServesDesktopView()
    {
        // Desktop: no mobile-layout, standard desktop response
        var (desktopResponse, desktopHtml) = await GetWithUserAgentAsync(DesktopUserAgent);

        desktopResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        desktopHtml.Should().NotContain("mobile-layout");

        // Mobile: 200 with mobile shell (fallback — no .Mobile.cshtml content view yet)
        var (mobileResponse, mobileHtml) = await GetWithUserAgentAsync(MobileUserAgent);

        mobileResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        mobileHtml.Should().Contain("mobile-layout");
    }
}
