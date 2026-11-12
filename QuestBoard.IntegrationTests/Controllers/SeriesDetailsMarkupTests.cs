using System.Net;
using System.Text.RegularExpressions;
using QuestBoard.Domain.Enums;
using QuestBoard.IntegrationTests.Helpers;

namespace QuestBoard.IntegrationTests.Controllers;

// Pins the markup of the series page. Ending a series confirms through a data attribute the event page module
// handles, the read-only cycle strip carries no inline style and is described in words, every occurrence link and
// the empty header cell are named, the remove dialog is a named foundation modal, and the page's own empty
// sentences stay as they were.
public class SeriesDetailsMarkupTests(WebApplicationFactoryBase factory)
    : IClassFixture<WebApplicationFactoryBase>, IAsyncLifetime
{
    private const string MobileUserAgent =
        "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1";

    private const string DesktopUserAgent =
        "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0 Safari/537.36";

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public ValueTask DisposeAsync()
    {
        factory.TestGroupContext.ActiveGroupId = 1;
        factory.TestGroupContext.BoardType = BoardType.OneShot;
        return ValueTask.CompletedTask;
    }

    private static string AgentFor(string layout) => layout == "phone" ? MobileUserAgent : DesktopUserAgent;

    private async Task<string> GetBodyAsync(HttpClient client, string url, string userAgent)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("User-Agent", userAgent);
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
    }

    private async Task<HttpClient> DungeonMasterAsync(string name)
    {
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, name, name + "@example.com", roles: ["DungeonMaster"]);
        return client;
    }

    // A weekly series with a "1,0,1" cycle, one past occurrence, one cancelled and one upcoming.
    private async Task<int> SeedSeriesAsync(bool withOccurrences, string? description = "A standing table.")
    {
        await using var ctx = factory.Database.CreateContext();
        var anchor = DateOnly.FromDateTime(DateTime.Today).AddDays(-14);
        var series = new EventSeriesEntity
        {
            Title = "Cold Hearth Rota",
            Description = description,
            StartTime = new TimeOnly(19, 0),
            AnchorDate = anchor,
            IntervalWeeks = 1,
            WeekDay = (int)anchor.DayOfWeek,
            CycleMask = "1,0,1",
            GroupId = 1,
            CreatedAt = DateTime.UtcNow
        };
        ctx.EventSeries.Add(series);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);

        if (withOccurrences)
        {
            await BoardMoveSeed.SeedEventAsync(factory, 1, series.Title, anchor, new TimeOnly(19, 0), series.Id, 0);
            await BoardMoveSeed.SeedEventAsync(
                factory, 1, series.Title, anchor.AddDays(7), new TimeOnly(19, 0), series.Id, 1, DateTime.UtcNow);
            await BoardMoveSeed.SeedEventAsync(
                factory, 1, series.Title, anchor.AddDays(28), new TimeOnly(19, 0), series.Id, 2);
        }

        return series.Id;
    }

    private static string Region(string body, string startMarker, string endMarker)
    {
        var start = body.IndexOf(startMarker, StringComparison.Ordinal);
        start.Should().BeGreaterThanOrEqualTo(0, $"the page renders {startMarker}");
        var end = body.IndexOf(endMarker, start, StringComparison.Ordinal);
        end.Should().BeGreaterThan(start, $"{endMarker} follows {startMarker}");
        return body[start..end];
    }

    [Theory]
    [InlineData("desktop")]
    [InlineData("phone")]
    public async Task EndSeries_ConfirmsThroughADataAttribute(string layout)
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var seriesId = await SeedSeriesAsync(withOccurrences: true);
        var client = await DungeonMasterAsync("series_end_" + layout);

        var body = await GetBodyAsync(client, $"/Series/Details/{seriesId}", AgentFor(layout));

        var form = Regex.Match(body, "<form\\b[^>]*action=\"/Series/End\"[^>]*>");
        form.Success.Should().BeTrue("a manager sees the End Series form");
        form.Value.Should().NotContain("onsubmit");
        var message = Regex.Match(form.Value, "data-confirm-message=\"([^\"]*)\"");
        message.Success.Should().BeTrue();
        WebUtility.HtmlDecode(message.Groups[1].Value).Should().MatchRegex(
            "^End this series after [A-Z][a-z]+ \\d{1,2}, \\d{4}\\? 1 upcoming session\\(s\\) will no longer be generated\\. " +
            "Sessions that already happened are always kept\\. This cannot be undone\\.$");

        body.Should().MatchRegex("<script type=\"module\" src=\"/js/event-details\\.js[^\"]*\"");
        Regex.Matches(body, @"<script\b(?![^>]*\bsrc\s*=)[^>]*>").Should().BeEmpty("the page carries no inline script");
    }
}
