using System.Net;
using System.Net.Http.Headers;
using QuestBoard.IntegrationTests.Helpers;

namespace QuestBoard.IntegrationTests.Controllers;

/// <summary>
/// Verifies that calendar cross-links to the availability overview use filled button
/// classes (btn-secondary) rather than outline variants (btn-outline-), following the
/// UI/UX design guidelines that require filled colored buttons.
/// </summary>
public class CalendarButtonStyleTests(WebApplicationFactoryBase factory)
    : IClassFixture<WebApplicationFactoryBase>
{
    private const string MobileUserAgent =
        "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1";

    private async Task<(HttpResponseMessage Response, string Html)> GetWithUserAgentAsync(
        HttpClient client, string url, string userAgent)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("User-Agent", userAgent);
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        return (response, html);
    }

    [Fact]
    public async Task DesktopCalendar_AvailabilityOverviewLink_UsesFilled_NotOutline()
    {
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "cal_desktop_user", "cal_desktop_user@example.com", roles: ["Player"]);

        var response = await client.GetAsync("/Calendar", TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        // The availability overview link must use btn-secondary (filled) class.
        html.Should().Contain("""btn btn-secondary""");
        html.Should().Contain("Availability Overview");
        // Verify it does NOT use the outline variant.
        html.Should().NotContain("""btn btn-outline-secondary""").And
            .NotContain("""btn btn-outline-primary""").And
            .NotContain("""btn btn-outline-danger""");
    }

    [Fact]
    public async Task MobileCalendar_AvailabilityOverviewLink_UsesFilled_NotOutline()
    {
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "cal_mobile_user", "cal_mobile_user@example.com", roles: ["Player"]);

        var (response, html) = await GetWithUserAgentAsync(
            client, "/Calendar", MobileUserAgent);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        // The availability overview link on mobile must use btn-secondary (filled) class.
        html.Should().Contain("""btn btn-secondary""");
        html.Should().Contain("Availability Overview");
        // Verify it does NOT use the outline variant.
        html.Should().NotContain("""btn btn-outline-secondary""").And
            .NotContain("""btn btn-outline-primary""").And
            .NotContain("""btn btn-outline-danger""");
    }
}
