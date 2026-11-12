using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using QuestBoard.Domain.Enums;
using QuestBoard.IntegrationTests.Helpers;

namespace QuestBoard.IntegrationTests.Tests;

/// <summary>
/// Proves the dates in the calendar, agenda, events and series views keep exactly the text they
/// had when each view formatted its own date: every fact computes the expected text with the old
/// format string on the same seeded value, in the same culture, and compares it with what the
/// shared date helper now renders. Each fact runs under the host's culture and under nl-NL, so a
/// helper that quietly formatted with another culture would fail here, and the Dutch run also
/// asserts the text is not the English one so the culture swap is proven to reach the server.
/// </summary>
[CollectionDefinition(nameof(EventsDateRenderTests), DisableParallelization = true)]
public class EventsDateRenderCollection;

[Collection(nameof(EventsDateRenderTests))]
public class EventsDateRenderTests(WebApplicationFactoryBase factory)
    : IClassFixture<WebApplicationFactoryBase>, IAsyncLifetime
{
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

    // The host's culture when the name is empty; otherwise all four culture slots are swapped for
    // the duration of the body, because the in-process server reads the process defaults and the
    // request thread's own culture slots, and restored afterwards whatever happens.
    private static async Task WithCultureAsync(string cultureName, Func<CultureInfo, Task> body)
    {
        var defaultCulture = CultureInfo.DefaultThreadCurrentCulture;
        var defaultUiCulture = CultureInfo.DefaultThreadCurrentUICulture;
        var culture = CultureInfo.CurrentCulture;
        var uiCulture = CultureInfo.CurrentUICulture;
        try
        {
            var active = cultureName.Length == 0 ? CultureInfo.CurrentCulture : new CultureInfo(cultureName);
            if (cultureName.Length > 0)
            {
                CultureInfo.DefaultThreadCurrentCulture = active;
                CultureInfo.DefaultThreadCurrentUICulture = active;
                CultureInfo.CurrentCulture = active;
                CultureInfo.CurrentUICulture = active;
            }

            await body(active);
        }
        finally
        {
            CultureInfo.DefaultThreadCurrentCulture = defaultCulture;
            CultureInfo.DefaultThreadCurrentUICulture = defaultUiCulture;
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = uiCulture;
        }
    }

    private static async Task<string> GetAsync(HttpClient client, string url, string userAgent)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("User-Agent", userAgent);
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK, because: url);
        return await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
    }

    // The text of every time element carrying the given style, in document order.
    private static List<string> TimeTexts(string html, string style) =>
        Regex.Matches(html, "<time\\b[^>]*data-style=\"" + Regex.Escape(style) + "\"[^>]*>(?<t>[^<]*)</time>")
            .Select(m => WebUtility.HtmlDecode(m.Groups["t"].Value))
            .ToList();

    // The visible text of a fragment: tags dropped, entities decoded, whitespace collapsed.
    private static string VisibleText(string fragment) =>
        Regex.Replace(WebUtility.HtmlDecode(Regex.Replace(fragment, "<[^>]+>", string.Empty)), "\\s+", " ").Trim();

    // A weekday style prints a localised day name, so under a non-English culture its text must
    // differ from the English one; proof that the culture reached the server.
    private static void AssertNotEnglish(CultureInfo culture, string actual, DateTime value, string format)
    {
        if (culture.TwoLetterISOLanguageName != "en" && culture.TwoLetterISOLanguageName != "iv")
        {
            actual.Should().NotBe(value.ToString(format, CultureInfo.InvariantCulture));
        }
    }

    // ---- Event details ------------------------------------------------------------------------

    [Theory]
    [InlineData("")]
    [InlineData("nl-NL")]
    public async Task EventDetails_Date_KeepsItsText(string cultureName)
    {
        await WithCultureAsync(cultureName, async culture =>
        {
            await TestDataHelper.ClearDatabaseAsync(factory.Services);
            var date = new DateOnly(2026, 9, 4);
            var eventId = await BoardMoveSeed.SeedEventAsync(factory, 1, "Date Render Council", date);
            var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
                factory, "evdate_details", "evdate_details@example.com");

            var html = await GetAsync(client, $"/Events/Details/{eventId}", DesktopUserAgent);

            var texts = TimeTexts(html, "weekday-long-year");
            texts.Should().ContainSingle();
            texts[0].Should().Be(date.ToString("dddd, MMMM d, yyyy"));
            html.Should().Contain("datetime=\"2026-09-04T00:00\"");
            AssertNotEnglish(culture, texts[0], new DateTime(2026, 9, 4), "dddd, MMMM d, yyyy");
            if (cultureName == "nl-NL")
            {
                texts[0].Should().NotContain("Friday");
            }
        });
    }

    // ---- Calendar -----------------------------------------------------------------------------

    private static readonly DateTime ProposedAt = new(2031, 9, 14, 19, 0, 0);

    private async Task<(HttpClient Client, int QuestId)> SeedProposedQuestAsync(string key)
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (client, user) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, key, $"{key}@example.com", roles: ["Admin"]);
        var quest = await TestDataHelper.CreateTestQuestAsync(factory.Services, user.Id, "Date Render Quest");
        await TestDataHelper.CreateProposedDateAsync(factory.Services, quest.Id, ProposedAt);
        return (client, quest.Id);
    }

    [Theory]
    [InlineData("")]
    [InlineData("nl-NL")]
    public async Task DesktopCalendar_QuestTime_KeepsItsText(string cultureName)
    {
        await WithCultureAsync(cultureName, async _ =>
        {
            var (client, _) = await SeedProposedQuestAsync("evdate_deskcal");

            var html = await GetAsync(client, "/Calendar?year=2031&month=9", DesktopUserAgent);

            var expected = ProposedAt.ToString("HH:mm");
            var times = Regex.Matches(
                    html,
                    "<div class=\"quest-time\">\\s*<time\\b[^>]*data-style=\"time-24h\"[^>]*>(?<t>[^<]*)</time>")
                .Select(m => m.Groups["t"].Value)
                .ToList();
            times.Should().Equal(expected);
            html.Should().Contain($"title=\"Date Render Quest - {expected}\"");
        });
    }

    [Theory]
    [InlineData("")]
    [InlineData("nl-NL")]
    public async Task PhoneCalendar_DayLabelAndTime_KeepTheirText(string cultureName)
    {
        await WithCultureAsync(cultureName, async culture =>
        {
            var (client, _) = await SeedProposedQuestAsync("evdate_phonecal");

            var html = await GetAsync(client, "/Calendar?year=2031&month=9", MobileUserAgent);

            var label = Regex.Matches(
                    html,
                    "<div class=\"agenda-day-label text-uppercase[^\"]*\"[^>]*>\\s*<time\\b[^>]*data-style=\"weekday-long\"[^>]*>(?<t>[^<]*)</time>")
                .Select(m => m.Groups["t"].Value)
                .ToList();
            label.Should().Equal(ProposedAt.Date.ToString("dddd, MMMM d"));
            label[0].Should().NotBe(label[0].ToUpper(culture), because: "the upper case comes from the text-uppercase class, not from the text");
            AssertNotEnglish(culture, label[0], ProposedAt, "dddd, MMMM d");

            Regex.Matches(
                    html,
                    "<small class=\"text-muted ms-2 flex-shrink-0\">\\s*<time\\b[^>]*data-style=\"time-24h\"[^>]*>(?<t>[^<]*)</time>")
                .Select(m => m.Groups["t"].Value)
                .Should().Equal(ProposedAt.ToString("HH:mm"));
        });
    }

    [Theory]
    [InlineData("")]
    [InlineData("nl-NL")]
    public async Task PhoneQuestDetails_VoteRowDateAndTime_KeepTheirText(string cultureName)
    {
        await WithCultureAsync(cultureName, async culture =>
        {
            var (client, questId) = await SeedProposedQuestAsync("evdate_phonevote");

            var html = await GetAsync(client, $"/Quest/Details/{questId}", MobileUserAgent);

            var labels = Regex.Matches(
                    html,
                    "<div class=\"calendar-date-label-mobile[^\"]*\"[^>]*>(?<inner>.*?)</div>",
                    RegexOptions.Singleline)
                .Select(m => VisibleText(m.Groups["inner"].Value))
                .ToList();
            labels.Should().Equal($"{ProposedAt.Date.ToString("dddd, MMMM d")} at {ProposedAt.ToString("HH:mm")}");
            AssertNotEnglish(culture, labels[0], ProposedAt, "dddd, MMMM d");
        });
    }

    // ---- Events overview and agenda -----------------------------------------------------------

    private async Task<(HttpClient Client, DateTime Timed, DateTime AllDay)> SeedUpcomingEventsAsync(string key)
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var timedDay = DateOnly.FromDateTime(DateTime.Today.AddDays(5));
        var allDay = DateOnly.FromDateTime(DateTime.Today.AddDays(6));
        await BoardMoveSeed.SeedEventAsync(factory, 1, "Timed Render Session", timedDay, new TimeOnly(19, 30));
        await BoardMoveSeed.SeedEventAsync(factory, 1, "All Day Render Session", allDay);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, key, $"{key}@example.com");
        return (client, timedDay.ToDateTime(new TimeOnly(19, 30)), allDay.ToDateTime(TimeOnly.MinValue));
    }

    private static void AssertRowDates(string html, CultureInfo culture, DateTime timed, DateTime allDay)
    {
        TimeTexts(html, "weekday-short").Should().BeEquivalentTo(
            new[] { timed.ToString("ddd, MMM d"), allDay.ToString("ddd, MMM d") });
        TimeTexts(html, "time-24h").Should().Equal(timed.ToString("HH:mm"));
        AssertNotEnglish(culture, TimeTexts(html, "weekday-short")[0], timed, "ddd, MMM d");
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("", true)]
    [InlineData("nl-NL", false)]
    [InlineData("nl-NL", true)]
    public async Task EventsIndex_DateAndTime_KeepTheirText(string cultureName, bool mobile)
    {
        await WithCultureAsync(cultureName, async culture =>
        {
            var (client, timed, allDay) = await SeedUpcomingEventsAsync("evdate_index");

            var html = await GetAsync(client, "/Events", mobile ? MobileUserAgent : DesktopUserAgent);

            AssertRowDates(html, culture, timed, allDay);
        });
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("", true)]
    [InlineData("nl-NL", false)]
    [InlineData("nl-NL", true)]
    public async Task Agenda_DateAndTime_KeepTheirText(string cultureName, bool mobile)
    {
        await WithCultureAsync(cultureName, async culture =>
        {
            var (client, timed, allDay) = await SeedUpcomingEventsAsync("evdate_agenda");

            var html = await GetAsync(client, "/Agenda", mobile ? MobileUserAgent : DesktopUserAgent);

            AssertRowDates(html, culture, timed, allDay);
        });
    }

    // ---- Series details -----------------------------------------------------------------------

    [Theory]
    [InlineData("")]
    [InlineData("nl-NL")]
    public async Task SeriesDetails_Dates_KeepTheirText(string cultureName)
    {
        await WithCultureAsync(cultureName, async culture =>
        {
            await TestDataHelper.ClearDatabaseAsync(factory.Services);
            var anchor = new DateOnly(2026, 9, 4);
            var end = new DateOnly(2026, 12, 18);
            var seriesId = await BoardMoveSeed.SeedSeriesAsync(factory, 1, "Date Render Series", anchor);
            await using (var ctx = factory.Database.CreateContext())
            {
                var series = await ctx.EventSeries.IgnoreQueryFilters().FirstAsync(s => s.Id == seriesId, TestContext.Current.CancellationToken);
                series.EndDate = end;
                series.StartTime = new TimeOnly(19, 0);
                await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
            }
            await BoardMoveSeed.SeedEventAsync(
                factory, 1, "Date Render Series", anchor, new TimeOnly(19, 0), seriesId, 0);
            var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
                factory, "evdate_series", "evdate_series@example.com", roles: ["DungeonMaster"]);

            var html = await GetAsync(client, $"/Series/Details/{seriesId}", DesktopUserAgent);

            var texts = TimeTexts(html, "weekday-long-year");
            texts.Should().Equal(
                anchor.ToString("dddd, MMMM d, yyyy"),
                end.ToString("dddd, MMMM d, yyyy"),
                anchor.ToString("dddd, MMMM d, yyyy"));
            AssertNotEnglish(culture, texts[0], anchor.ToDateTime(TimeOnly.MinValue), "dddd, MMMM d, yyyy");
            html.Should().MatchRegex("</time>\\s*&middot;\\s*19:00");
        });
    }
}
