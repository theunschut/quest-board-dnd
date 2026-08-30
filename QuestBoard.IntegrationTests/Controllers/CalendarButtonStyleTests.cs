using System.Net;
using System.Net.Http.Headers;
using QuestBoard.IntegrationTests.Helpers;

namespace QuestBoard.IntegrationTests.Controllers;

/// <summary>
/// Verifies the Calendar page's cross-link to Board Availability: it uses filled button
/// classes (btn-secondary) rather than outline variants (btn-outline-), following the
/// UI/UX design guidelines that require filled colored buttons, and it is present for a
/// Dungeon Master but absent for a Player. The link's site was role-blind before this link
/// became gated, so proving both presence for a DM and absence for a Player is what shows
/// the link now lives inside a Dungeon-Master-only condition -- presence alone could not
/// tell the difference. If the site this link renders from ever becomes role-aware for a
/// different reason, this role flip stops proving placement and the suite needs rethinking.
/// </summary>
public class CalendarButtonStyleTests(WebApplicationFactoryBase factory)
    : IClassFixture<WebApplicationFactoryBase>
{
    private const string MobileUserAgent =
        "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1";

    private const string DesktopUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

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
    public async Task DesktopCalendar_BoardAvailabilityLink_DmSeesFilled_NotOutline()
    {
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedDMClientAsync(
            factory, "cal_desktop_dm", "cal_desktop_dm@example.com");

        var response = await client.GetAsync("/Calendar", TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        // The Board Availability link must use btn-secondary (filled) class.
        html.Should().Contain("""btn btn-secondary""");
        html.Should().Contain("Board Availability");
        // Verify it does NOT use the outline variant.
        html.Should().NotContain("""btn btn-outline-secondary""").And
            .NotContain("""btn btn-outline-primary""").And
            .NotContain("""btn btn-outline-danger""");
    }

    [Fact]
    public async Task MobileCalendar_BoardAvailabilityLink_DmSeesFilled_NotOutline()
    {
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedDMClientAsync(
            factory, "cal_mobile_dm", "cal_mobile_dm@example.com");

        var (response, html) = await GetWithUserAgentAsync(
            client, "/Calendar", MobileUserAgent);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        // The Board Availability link on mobile must use btn-secondary (filled) class.
        html.Should().Contain("""btn btn-secondary""");
        html.Should().Contain("Board Availability");
        // Verify it does NOT use the outline variant.
        html.Should().NotContain("""btn btn-outline-secondary""").And
            .NotContain("""btn btn-outline-primary""").And
            .NotContain("""btn btn-outline-danger""");
    }

    [Theory]
    [InlineData(DesktopUserAgent)]
    [InlineData(MobileUserAgent)]
    public async Task Calendar_BoardAvailabilityLink_AbsentForPlayer(string userAgent)
    {
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "cal_player", "cal_player@example.com", roles: ["Player"]);

        var (response, html) = await GetWithUserAgentAsync(client, "/Calendar", userAgent);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().NotContain("Board Availability");
        // A Player must still see the adjacent My Agenda button -- this catches the failure
        // mode where the wrong one of the two adjacent buttons was gated.
        html.Should().Contain("My Agenda");
    }
}
