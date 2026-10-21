using System.Net;
using QuestBoard.Domain.Enums;
using QuestBoard.IntegrationTests.Helpers;

namespace QuestBoard.IntegrationTests.Controllers;

/// <summary>
/// Proves that a board which switched off Events or the Calendar offers no way into the switched-off
/// page: the calendar stops loading the board's own events and series warnings, and event and series
/// saves land on the Events list instead of a refused calendar.
/// </summary>
public class BoardPageEventsCalendarTests(WebApplicationFactoryBase factory)
    : IClassFixture<WebApplicationFactoryBase>, IAsyncLifetime
{
    private const string MobileUserAgent =
        "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1";

    private const string DesktopUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

    private static readonly BoardPageSet EventsOff = BoardPageSet.Empty.With(BoardPage.Events);
    private static readonly BoardPageSet CalendarOff = BoardPageSet.Empty.With(BoardPage.Calendar);

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    // The board context and the stored settings are shared across the whole class fixture, so this
    // restores the baseline and no test's premise depends on which test ran before it.
    public async ValueTask DisposeAsync()
    {
        factory.TestGroupContext.BoardType = BoardType.OneShot;
        factory.TestGroupContext.ActiveGroupId = 1;
        await BoardPageSeed.ResetAsync(factory, 1);
    }

    private static async Task<(HttpResponseMessage Response, string Html)> GetAsync(
        HttpClient client, string url, string userAgent)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("User-Agent", userAgent);
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        return (response, html);
    }

    private async Task<int> SeedEventAsync(string title, DateOnly date)
    {
        await using var ctx = factory.Database.CreateContext();
        var entity = new EventEntity
        {
            Title = title,
            GroupId = 1,
            Date = date,
            CreatedAt = DateTime.UtcNow
        };
        ctx.Events.Add(entity);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        return entity.Id;
    }

    // One series with the given number of live upcoming occurrences, weekly from today, so a
    // count under the runway target puts the series on the calendar's low-runway banner.
    private async Task<int> SeedSeriesAsync(string title, int occurrenceCount)
    {
        var anchorDate = DateOnly.FromDateTime(DateTime.Today);

        await using var ctx = factory.Database.CreateContext();
        var series = new EventSeriesEntity
        {
            Title = title,
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
                Title = title,
                Description = series.Description,
                GroupId = 1,
                Date = anchorDate.AddDays(slot * 7),
                StartTime = series.StartTime,
                SeriesId = series.Id,
                SeriesSlotIndex = slot,
                CreatedAt = DateTime.UtcNow
            });
        }
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);

        return series.Id;
    }

    [Theory]
    [InlineData(DesktopUserAgent)]
    [InlineData(MobileUserAgent)]
    public async Task Calendar_EventsOff_DoesNotLoadTheBoardsEvents(string userAgent)
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        await SeedEventAsync("Hidden Harvest Feast", new DateOnly(2031, 4, 12));
        await BoardPageSeed.SetAsync(factory, 1, EventsOff, BoardPage.Quests);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedDMClientAsync(factory);

        var (response, html) = await GetAsync(client, "/Calendar?year=2031&month=4", userAgent);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        // The month name doubles as the anchor proving the calendar rendered, so an error page or
        // a redirect cannot make the absence assertion pass for free.
        html.Should().Contain("April 2031");
        html.Should().NotContain("Hidden Harvest Feast");
    }

    [Theory]
    [InlineData(DesktopUserAgent)]
    [InlineData(MobileUserAgent)]
    public async Task Calendar_EventsOn_ShowsTheBoardsEvents(string userAgent)
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        await SeedEventAsync("Visible Harvest Feast", new DateOnly(2031, 4, 12));
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedDMClientAsync(factory);

        var (response, html) = await GetAsync(client, "/Calendar?year=2031&month=4", userAgent);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("Visible Harvest Feast");
    }

    [Theory]
    [InlineData(DesktopUserAgent)]
    [InlineData(MobileUserAgent)]
    public async Task Calendar_EventsOff_ShowsNoSeriesRunwayBanner(string userAgent)
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        await SeedSeriesAsync("Dwindling Lantern Watch", 3);
        await BoardPageSeed.SetAsync(factory, 1, EventsOff, BoardPage.Quests);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedDMClientAsync(factory);

        var (response, html) = await GetAsync(client, "/Calendar", userAgent);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().NotContain("series is running low");
        html.Should().NotContain("/Series/Details/");
    }

    [Theory]
    [InlineData(DesktopUserAgent)]
    [InlineData(MobileUserAgent)]
    public async Task Calendar_EventsOn_ShowsSeriesRunwayBanner(string userAgent)
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var seriesId = await SeedSeriesAsync("Dwindling Lantern Watch", 3);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedDMClientAsync(factory);

        var (response, html) = await GetAsync(client, "/Calendar", userAgent);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("series is running low");
        html.Should().Contain($"/Series/Details/{seriesId}");
    }

    [Fact]
    public async Task Calendar_CampaignBoardWithEventsOff_RendersAnEmptyCalendar()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        await SeedEventAsync("Campaign Hidden Feast", new DateOnly(2031, 4, 12));
        await BoardPageSeed.SetAsync(factory, 1, EventsOff, BoardPage.Quests);
        factory.TestGroupContext.BoardType = BoardType.Campaign;
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedDMClientAsync(factory);

        var (response, html) = await GetAsync(client, "/Calendar?year=2031&month=4", DesktopUserAgent);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("April 2031");
        html.Should().NotContain("Campaign Hidden Feast");
    }

    [Fact]
    public async Task Calendar_EventsSwitchedBackOn_ShowsTheEarlierEventAgain()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        await SeedEventAsync("Round Trip Feast", new DateOnly(2031, 4, 12));
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedDMClientAsync(factory);

        await BoardPageSeed.SetAsync(factory, 1, EventsOff, BoardPage.Quests);
        var (_, whileOff) = await GetAsync(client, "/Calendar?year=2031&month=4", DesktopUserAgent);
        await BoardPageSeed.SetAsync(factory, 1, BoardPageSet.Empty, BoardPage.Quests);
        var (_, afterOn) = await GetAsync(client, "/Calendar?year=2031&month=4", DesktopUserAgent);

        whileOff.Should().NotContain("Round Trip Feast");
        afterOn.Should().Contain("Round Trip Feast");
    }

    [Fact]
    public async Task EventCreate_CalendarOff_RedirectsToTheEventsList()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        await BoardPageSeed.SetAsync(factory, 1, CalendarOff, BoardPage.Quests);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedDMClientAsync(factory);

        var response = await client.PostAsync("/Events/Create", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Title"] = "Calendar Off Create",
            ["Date"] = "2031-04-12"
        }), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.Should().Be("/Events");
    }

    [Fact]
    public async Task EventCreate_CalendarOn_StillRedirectsToTheEventsMonth()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedDMClientAsync(factory);

        var response = await client.PostAsync("/Events/Create", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Title"] = "Calendar On Create",
            ["Date"] = "2031-04-12"
        }), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.Should().Contain("/Calendar");
        response.Headers.Location!.OriginalString.Should().Contain("year=2031");
        response.Headers.Location!.OriginalString.Should().Contain("month=4");
    }

    [Fact]
    public async Task EventEdit_CalendarOff_RedirectsToTheEventsList()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var eventId = await SeedEventAsync("Calendar Off Edit", new DateOnly(2031, 4, 12));
        await BoardPageSeed.SetAsync(factory, 1, CalendarOff, BoardPage.Quests);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedDMClientAsync(factory);

        var response = await client.PostAsync($"/Events/Edit/{eventId}", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Title"] = "Calendar Off Edited",
            ["Date"] = "2031-04-12"
        }), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.Should().Be("/Events");
    }

    [Fact]
    public async Task EventDelete_CalendarOff_RedirectsToTheEventsList()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var eventId = await SeedEventAsync("Calendar Off Delete", new DateOnly(2031, 4, 12));
        await BoardPageSeed.SetAsync(factory, 1, CalendarOff, BoardPage.Quests);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedDMClientAsync(factory);

        var response = await client.PostAsync("/Events/Delete", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["id"] = eventId.ToString()
        }), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.Should().Be("/Events");
    }

    [Theory]
    [InlineData("/Series/Delete")]
    [InlineData("/Series/Detach")]
    public async Task SeriesDeleteAndDetach_CalendarOff_RedirectToTheEventsList(string url)
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var seriesId = await SeedSeriesAsync("Calendar Off Series", 3);
        await BoardPageSeed.SetAsync(factory, 1, CalendarOff, BoardPage.Quests);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedDMClientAsync(factory);

        var response = await client.PostAsync(url, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["id"] = seriesId.ToString()
        }), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.Should().Be("/Events");
    }

    [Theory]
    [InlineData("/Series/Delete")]
    [InlineData("/Series/Detach")]
    public async Task SeriesDeleteAndDetach_CalendarOn_StillRedirectToTheCalendar(string url)
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var seriesId = await SeedSeriesAsync("Calendar On Series", 3);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedDMClientAsync(factory);

        var response = await client.PostAsync(url, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["id"] = seriesId.ToString()
        }), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.Should().Be("/Calendar");
    }
}
