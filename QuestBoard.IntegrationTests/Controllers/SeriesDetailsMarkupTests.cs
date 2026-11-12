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
    public async Task Cards_FollowTheCardRoles(string layout)
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var seriesId = await SeedSeriesAsync(withOccurrences: true);
        var client = await DungeonMasterAsync("series_cards_" + layout);

        var body = await GetBodyAsync(client, $"/Series/Details/{seriesId}", AgentFor(layout));

        Regex.Matches(body, "<h1\\b").Should().HaveCount(1, "the page has one top-level heading");
        body.Should().NotContain("<h4", "section titles are second-level headings");
        Regex.Matches(body, "<h2 class=\"mb-0\">").Should().HaveCount(3);
        foreach (var title in new[] { "Recurrence Rule", "Occurrences", "Actions" })
            body.Should().MatchRegex($"<h2\\b[^>]*>\\s*{title}\\s*</h2>");

        // The title card is a plain card; the three section cards are asides.
        var firstCard = Regex.Match(body, "<div class=\"card modern-card[^\"]*\"[^>]*>");
        firstCard.Value.Should().NotContain("modern-card--aside");
        firstCard.Value.Should().NotContain("quest-form-page");
        Regex.Matches(body, "<div class=\"card modern-card modern-card--aside mb-3\">").Should().HaveCount(3);

        var header = Regex.Match(body, "<div class=\"card-header modern-card-header\">\\s*<h1\\b[^>]*>(.*?)</h1>", RegexOptions.Singleline);
        header.Success.Should().BeTrue("the page title sits in the title card's header");
        header.Groups[1].Value.Should().Contain("Cold Hearth Rota").And.Contain("Recurring Series");
        header.Groups[1].Value.Should().Contain("fa-repeat").And.Contain("aria-hidden=\"true\"");
        header.Groups[1].Value.Should().NotContain("text-purple", "the icon inherits the title colour");
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

    [Theory]
    [InlineData("desktop")]
    [InlineData("phone")]
    public async Task ReadOnlyStrip_HasNoInlineStyleAndATextSummary(string layout)
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var seriesId = await SeedSeriesAsync(withOccurrences: true);
        var client = await DungeonMasterAsync("series_strip_" + layout);

        var body = await GetBodyAsync(client, $"/Series/Details/{seriesId}", AgentFor(layout));

        var cells = Regex.Matches(body, "<span class=\"cycle-mask-cell[^\"]*\"[^>]*>");
        cells.Should().HaveCount(3);
        foreach (Match cell in cells)
        {
            cell.Value.Should().Contain("cycle-mask-cell--readonly");
            cell.Value.Should().NotContain("style=");
        }

        cells.Select(c => Regex.IsMatch(c.Value, "class=\"[^\"]*\\bon\\b")).Should().Equal(true, false, true);
        body.Should().MatchRegex("<span class=\"visually-hidden\">Sessions on cycle positions 1, 3\\.</span>");
    }

    [Theory]
    [InlineData("desktop")]
    [InlineData("phone")]
    public async Task OccurrenceLinks_AndHeaderCell_AreNamed(string layout)
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var seriesId = await SeedSeriesAsync(withOccurrences: true);
        var client = await DungeonMasterAsync("series_links_" + layout);

        var body = await GetBodyAsync(client, $"/Series/Details/{seriesId}", AgentFor(layout));

        var table = Region(body, "<table", "</table>");
        var links = Regex.Matches(table, "<a class=\"table-cell-link\" href=\"/Events/Details/\\d+\">(.*?)</a>", RegexOptions.Singleline);
        links.Should().HaveCount(3);
        foreach (Match link in links)
            link.Groups[1].Value.Should().MatchRegex(
                "^View<span class=\"visually-hidden\"> occurrence on <time\\b.*</time></span>$");

        Regex.Matches(table, "<th>").Should().HaveCount(3);
        table.Should().Contain("<th><span class=\"visually-hidden\">Actions</span></th>");
        table.Should().NotContain("class=\"today\"", "the divider row's class had no rule");
        table.Should().MatchRegex("<td colspan=\"3\" class=\"text-center small text-muted\">Today</td>");
        table.Should().Contain("text-decoration-line-through");
        table.Should().Contain("<span class=\"badge bg-secondary\">Cancelled</span>");
    }

    [Theory]
    [InlineData("desktop")]
    [InlineData("phone")]
    public async Task RemoveDialog_IsNamedAndOnTheFoundationSurface(string layout)
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var seriesId = await SeedSeriesAsync(withOccurrences: true);
        var client = await DungeonMasterAsync("series_modal_" + layout);

        var body = await GetBodyAsync(client, $"/Series/Details/{seriesId}", AgentFor(layout));

        var dialogTag = Regex.Match(body, "<div class=\"modal fade\"[^>]*id=\"removeSeriesModal\"[^>]*>");
        dialogTag.Success.Should().BeTrue();
        dialogTag.Value.Should().Contain("aria-labelledby=\"removeSeriesTitle\"");

        var dialog = body[body.IndexOf("id=\"removeSeriesModal\"", StringComparison.Ordinal)..];
        dialog.Should().Contain("<h2 class=\"modal-title\" id=\"removeSeriesTitle\">Remove this series</h2>");
        dialog.Should().MatchRegex("<button type=\"button\" class=\"btn-close\" data-bs-dismiss=\"modal\" aria-label=\"Close\"></button>");
        dialog.Should().NotContain("bg-dark").And.NotContain("text-light")
            .And.NotContain("border-secondary").And.NotContain("btn-close-white");

        dialog.Should().MatchRegex("<button type=\"submit\" class=\"btn btn-warning\">Detach sessions</button>");
        dialog.Should().MatchRegex("<button type=\"submit\" class=\"btn btn-danger\">Delete everything</button>");
        dialog.Should().MatchRegex("<button type=\"button\" class=\"btn btn-secondary\" data-bs-dismiss=\"modal\">Cancel</button>");
        dialog.Should().Contain("action=\"/Series/Detach\"").And.Contain("action=\"/Series/Delete\"");

        // The opener keeps its danger button and names the dialog it opens.
        body.Should().MatchRegex("<button type=\"button\" class=\"btn btn-danger w-100\" data-bs-toggle=\"modal\" data-bs-target=\"#removeSeriesModal\">");
        body.Should().MatchRegex("<button type=\"submit\" class=\"btn btn-warning w-100\">");
    }

    [Theory]
    [InlineData("desktop")]
    [InlineData("phone")]
    public async Task InlineEmpties_StayVerbatim(string layout)
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var seriesId = await SeedSeriesAsync(withOccurrences: false, description: null);
        var client = await DungeonMasterAsync("series_empty_" + layout);

        var body = await GetBodyAsync(client, $"/Series/Details/{seriesId}", AgentFor(layout));

        body.Should().Contain("No description provided.");
        body.Should().Contain("No occurrences have been generated yet.");
        body.Should().NotContain("<table");
    }
}
