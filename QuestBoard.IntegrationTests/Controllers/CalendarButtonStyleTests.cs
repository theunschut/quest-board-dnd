using System.Net;
using System.Net.Http.Headers;
using QuestBoard.IntegrationTests.Helpers;

namespace QuestBoard.IntegrationTests.Controllers;

/// <summary>
/// Verifies where the Board Availability and My Agenda links live relative to the Calendar page. On
/// both layouts the page itself carries neither button (its own buttons are the filled month
/// buttons, never outline variants, following the UI/UX design guidelines); the desktop navigation
/// bar and the phone navigation menu hold both links, Board Availability for a Dungeon Master only
/// and My Agenda for every member. Proving presence for a DM and absence for a Player is what shows
/// the Board Availability link lives inside a Dungeon-Master-only condition -- presence alone could
/// not tell the difference.
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
    public async Task DesktopCalendar_Page_CarriesNoBoardAvailabilityOrAgendaButton_DmReachesThemFromTheNavigationBar()
    {
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedDMClientAsync(
            factory, "cal_desktop_dm", "cal_desktop_dm@example.com");

        var (response, html) = await GetWithUserAgentAsync(client, "/Calendar", DesktopUserAgent);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = DesktopPageRegions.PageContent(html);
        // The page proves it rendered through its own filled buttons, so an empty region cannot pass.
        page.Should().Contain("""btn btn-secondary""");
        page.Should().NotContain("""btn btn-outline-secondary""").And
            .NotContain("""btn btn-outline-primary""").And
            .NotContain("""btn btn-outline-danger""");
        page.Should().NotContain("Board Availability");
        page.Should().NotContain("My Agenda");
        page.Should().NotContain("href=\"/Events\"");
        page.Should().NotContain("href=\"/Agenda\"");

        // Both stay one click away in the navigation bar.
        var bar = DesktopPageRegions.NavigationBar(html);
        bar.Should().Contain("href=\"/Events\"");
        bar.Should().Contain("Board Availability");
        bar.Should().Contain("href=\"/Agenda\"");
        bar.Should().Contain("My Agenda");
    }

    [Fact]
    public async Task MobileCalendar_Page_CarriesNoBoardAvailabilityOrAgendaButton_DmReachesThemFromTheNavigationMenu()
    {
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedDMClientAsync(
            factory, "cal_mobile_dm", "cal_mobile_dm@example.com");

        var (response, html) = await GetWithUserAgentAsync(
            client, "/Calendar", MobileUserAgent);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = PhonePageRegions.PageContent(html);
        // The page proves it rendered through its own filled buttons, so an empty region cannot pass.
        page.Should().Contain("""btn btn-secondary""");
        page.Should().NotContain("""btn btn-outline-secondary""").And
            .NotContain("""btn btn-outline-primary""").And
            .NotContain("""btn btn-outline-danger""");
        page.Should().NotContain("Board Availability");
        page.Should().NotContain("My Agenda");
        page.Should().NotContain("href=\"/Events\"");
        page.Should().NotContain("href=\"/Agenda\"");

        // Both stay one tap away in the navigation menu.
        var menu = PhonePageRegions.NavigationMenu(html);
        menu.Should().Contain("href=\"/Events\"");
        menu.Should().Contain("Board Availability");
        menu.Should().Contain("href=\"/Agenda\"");
        menu.Should().Contain("My Agenda");
    }

    [Fact]
    public async Task DesktopCalendar_NavigationBar_ShowsAPlayerMyAgendaButNoBoardAvailability()
    {
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "cal_player", "cal_player@example.com", roles: ["Player"]);

        var (response, html) = await GetWithUserAgentAsync(client, "/Calendar", DesktopUserAgent);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = DesktopPageRegions.PageContent(html);
        page.Should().NotContain("Board Availability");
        page.Should().NotContain("My Agenda");

        // A player never gets the Board Availability link, and the My Agenda link stays in the bar for
        // every member -- this catches the failure mode where the wrong one of the two was gated.
        var bar = DesktopPageRegions.NavigationBar(html);
        bar.Should().NotContain("Board Availability");
        bar.Should().Contain("My Agenda");
    }

    [Fact]
    public async Task MobileCalendar_NavigationMenu_ShowsAPlayerMyAgendaButNoBoardAvailability()
    {
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "cal_player_mobile", "cal_player_mobile@example.com", roles: ["Player"]);

        var (response, html) = await GetWithUserAgentAsync(client, "/Calendar", MobileUserAgent);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = PhonePageRegions.PageContent(html);
        page.Should().NotContain("Board Availability");
        page.Should().NotContain("My Agenda");

        // A player never gets the Board Availability link; My Agenda stays in the menu for every member.
        var menu = PhonePageRegions.NavigationMenu(html);
        menu.Should().NotContain("Board Availability");
        menu.Should().Contain("My Agenda");
    }
}
