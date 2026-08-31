using QuestBoard.Domain.Enums;
using QuestBoard.IntegrationTests.Helpers;
using System.Net;

namespace QuestBoard.IntegrationTests.Controllers;

// Guards against a partial rename leaving two names for one page. When a board-scoped
// surface's label changes, every rendered route that carries it has to change together, or a
// reader can land on one page still showing the retired name while every other page and every
// nav entry already shows the new one. The navigation suite cannot see this: its cases assert
// against the shared layout on a single route, so a stale label left behind in a page's own
// body -- on a route the navigation suite never fetches -- would slip through unnoticed. This
// class instead fetches each affected surface directly and checks its own rendered body.
// Authenticating as a Dungeon Master is deliberate: that is the one role that sees every
// affected surface and every cross-link between them, so any label this rename missed is
// somewhere inside a Dungeon Master's rendered output. Keeping the internal `Overview` domain
// vocabulary in C# type names, view-model names and the domain service does not put this class
// in tension with the rename -- internal identifiers never reach rendered HTML, so the two
// facts are independent.
public class StaleAvailabilityOverviewLabelGuardTests(WebApplicationFactoryBase factory)
    : IClassFixture<WebApplicationFactoryBase>, IAsyncLifetime
{
    // Mobile views in this app are selected from the request's user agent, so a request
    // without this header renders the desktop view instead. "Both user agents" in this class
    // means two real user-agent strings; there is no viewport or breakpoint mechanism to
    // exercise instead.
    private const string DesktopUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

    private const string MobileUserAgent =
        "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1";

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public ValueTask DisposeAsync()
    {
        factory.TestGroupContext.ActiveGroupId = 1;
        factory.TestGroupContext.BoardType = BoardType.OneShot;
        return ValueTask.CompletedTask;
    }

    private async Task<(HttpResponseMessage Response, string Html)> GetWithUserAgentAsync(
        HttpClient client, string url, string userAgent)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("User-Agent", userAgent);
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        return (response, html);
    }

    [Theory]
    [InlineData(DesktopUserAgent)]
    [InlineData(MobileUserAgent)]
    public async Task BoardAvailabilityPage_DoesNotRenderRetiredLabel(string userAgent)
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedDMClientAsync(
            factory, "stalelabel_events", "stalelabel_events@example.com");

        var (response, html) = await GetWithUserAgentAsync(client, "/Events", userAgent);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("Board Availability");
        html.Should().NotContain("Availability Overview");
    }

    [Theory]
    [InlineData(DesktopUserAgent)]
    [InlineData(MobileUserAgent)]
    public async Task MyAgendaPage_DoesNotRenderRetiredLabel(string userAgent)
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedDMClientAsync(
            factory, "stalelabel_agenda", "stalelabel_agenda@example.com");

        var (response, html) = await GetWithUserAgentAsync(client, "/Agenda", userAgent);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("My Agenda");
        html.Should().NotContain("Availability Overview");
    }

    [Theory]
    [InlineData(DesktopUserAgent)]
    [InlineData(MobileUserAgent)]
    public async Task CalendarPage_DoesNotRenderRetiredLabel(string userAgent)
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedDMClientAsync(
            factory, "stalelabel_calendar", "stalelabel_calendar@example.com");

        var (response, html) = await GetWithUserAgentAsync(client, "/Calendar", userAgent);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("Calendar");
        html.Should().NotContain("Availability Overview");
    }
}
