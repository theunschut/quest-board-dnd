using System.Net;
using System.Net.Http.Headers;
using QuestBoard.Domain.Enums;
using QuestBoard.IntegrationTests.Helpers;

namespace QuestBoard.IntegrationTests.Controllers;

/// <summary>
/// Coverage for the low-runway horizon warning banner on both calendar surfaces. The server
/// picks the mobile view off the request user agent, so only a genuine mobile user-agent
/// string ever exercises the mobile view -- a desktop viewport emulation never reaches this
/// markup. Every mobile assertion below also anchors on the mobile-only agenda wrapper, so an
/// absence assertion can never pass for free against a login redirect or an error page.
/// </summary>
public class CalendarHorizonBannerTests(WebApplicationFactoryBase factory)
    : IClassFixture<WebApplicationFactoryBase>, IAsyncLifetime
{
    private const string MobileUserAgent =
        "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1";

    private const string DesktopUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

    // IAsyncLifetime -- reset the singleton group context after each test class run so that
    // test state does not bleed into subsequently-executed test classes.
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public ValueTask DisposeAsync()
    {
        factory.TestGroupContext.ActiveGroupId = 1;
        factory.TestGroupContext.BoardType = BoardType.OneShot;
        return ValueTask.CompletedTask;
    }

    private async Task<(HttpResponseMessage Response, string Html)> GetWithUserAgentAsync(
        string url, string userAgent, AuthenticationHeaderValue? authorization)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("User-Agent", userAgent);
        if (authorization != null)
        {
            request.Headers.Authorization = authorization;
        }
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        return (response, html);
    }

    // Writes one series and the given number of live upcoming occurrences on board 1, with a
    // weekly cadence anchored on today and a single-position cycle mask. Ids are captured off
    // the tracked entities rather than by re-querying, matching the pattern the tenant
    // isolation tests already use for the same reason: a seeding context should never depend
    // on a follow-up read to learn what it just wrote.
    private async Task<int> SeedSeriesWithOccurrencesAsync(string seriesTitle, int occurrenceCount)
    {
        var anchorDate = DateOnly.FromDateTime(DateTime.Today);

        await using var ctx = factory.Database.CreateContext();

        var series = new EventSeriesEntity
        {
            Title = seriesTitle,
            Description = "Weekly session.",
            StartTime = new TimeOnly(19, 0),
            AnchorDate = anchorDate,
            IntervalWeeks = 1,
            WeekDay = (int)anchorDate.DayOfWeek,
            CycleMask = "1",
            EndDate = null,
            GroupId = 1,
            CreatedAt = DateTime.UtcNow
        };
        ctx.EventSeries.Add(series);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);

        for (var slot = 0; slot < occurrenceCount; slot++)
        {
            ctx.Events.Add(new EventEntity
            {
                Title = seriesTitle,
                Description = series.Description,
                GroupId = 1,
                Date = anchorDate.AddDays(slot * series.IntervalWeeks * 7),
                StartTime = series.StartTime,
                SeriesId = series.Id,
                SeriesSlotIndex = slot,
                CreatedAt = DateTime.UtcNow
            });
        }
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);

        return series.Id;
    }

    [Fact]
    public async Task MobileCalendar_DmWithSeriesBelowRunway_RendersHorizonBanner()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);

        var seriesId = await SeedSeriesWithOccurrencesAsync("Waning Moon Council", 3);

        factory.TestGroupContext.ActiveGroupId = 1;
        var (dmClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "horizon_mobile_dm", "horizon_mobile_dm@example.com", roles: ["DungeonMaster"]);

        var (response, html) = await GetWithUserAgentAsync(
            "/Calendar", MobileUserAgent, dmClient.DefaultRequestHeaders.Authorization);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("agenda-card-mobile");
        html.Should().Contain("series is running low");
        html.Should().Contain("upcoming session(s) left");
        html.Should().Contain("Waning Moon Council");
        html.Should().Contain($"/Series/Details/{seriesId}");
    }

    [Fact]
    public async Task MobileCalendar_DmWithTwoSeriesBelowRunway_RendersMultiSeriesBanner()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);

        await SeedSeriesWithOccurrencesAsync("Frayed Banner Watch", 2);
        await SeedSeriesWithOccurrencesAsync("Hollow Reach Vigil", 4);

        factory.TestGroupContext.ActiveGroupId = 1;
        var (dmClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "horizon_mobile_multi_dm", "horizon_mobile_multi_dm@example.com", roles: ["DungeonMaster"]);

        var (response, html) = await GetWithUserAgentAsync(
            "/Calendar", MobileUserAgent, dmClient.DefaultRequestHeaders.Authorization);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("agenda-card-mobile");
        html.Should().Contain("recurring series are running low on upcoming sessions");
        html.Should().Contain("Frayed Banner Watch");
        html.Should().Contain("Hollow Reach Vigil");
    }

    [Fact]
    public async Task MobileCalendar_PlayerWithSeriesBelowRunway_DoesNotRenderHorizonBanner()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);

        await SeedSeriesWithOccurrencesAsync("Ashen Bell Circle", 3);

        factory.TestGroupContext.ActiveGroupId = 1;
        var (playerClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "horizon_mobile_player", "horizon_mobile_player@example.com", roles: ["Player"]);

        var (response, html) = await GetWithUserAgentAsync(
            "/Calendar", MobileUserAgent, playerClient.DefaultRequestHeaders.Authorization);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("agenda-card-mobile");
        html.Should().NotContain("series is running low");
        html.Should().NotContain("recurring series are running low on upcoming sessions");
    }

    [Fact]
    public async Task DesktopCalendar_DmWithSeriesBelowRunway_RendersHorizonBanner()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);

        var seriesId = await SeedSeriesWithOccurrencesAsync("Guttering Watch Fire", 3);

        factory.TestGroupContext.ActiveGroupId = 1;
        var (dmClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "horizon_desktop_dm", "horizon_desktop_dm@example.com", roles: ["DungeonMaster"]);

        var (response, html) = await GetWithUserAgentAsync(
            "/Calendar", DesktopUserAgent, dmClient.DefaultRequestHeaders.Authorization);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().NotContain("agenda-card-mobile");
        html.Should().Contain("series is running low");
        html.Should().Contain("upcoming session(s) left");
        html.Should().Contain("Guttering Watch Fire");
        html.Should().Contain($"/Series/Details/{seriesId}");
    }

    [Fact]
    public async Task MobileCalendar_DmWithSeriesAtRunwayTarget_DoesNotRenderHorizonBanner()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);

        await SeedSeriesWithOccurrencesAsync("Steadfast Lantern Company", 20);

        factory.TestGroupContext.ActiveGroupId = 1;
        var (dmClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "horizon_mobile_attarget_dm", "horizon_mobile_attarget_dm@example.com", roles: ["DungeonMaster"]);

        var (response, html) = await GetWithUserAgentAsync(
            "/Calendar", MobileUserAgent, dmClient.DefaultRequestHeaders.Authorization);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("agenda-card-mobile");
        html.Should().NotContain("series is running low");
    }

    [Fact]
    public async Task MobileCalendar_CampaignBoardDm_RendersHorizonBanner()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);

        var seriesId = await SeedSeriesWithOccurrencesAsync("Long Road Expedition", 3);

        factory.TestGroupContext.ActiveGroupId = 1;
        factory.TestGroupContext.BoardType = BoardType.Campaign;
        var (dmClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "horizon_mobile_campaign_dm", "horizon_mobile_campaign_dm@example.com", roles: ["DungeonMaster"]);

        var (response, html) = await GetWithUserAgentAsync(
            "/Calendar", MobileUserAgent, dmClient.DefaultRequestHeaders.Authorization);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("agenda-card-mobile");
        html.Should().Contain("series is running low");
        html.Should().Contain("Long Road Expedition");
        html.Should().Contain($"/Series/Details/{seriesId}");
    }
}
