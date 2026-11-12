using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using QuestBoard.IntegrationTests.Helpers;

namespace QuestBoard.IntegrationTests.Controllers;

/// <summary>
/// Proves that the calendar marks the board's own today, on the desktop month grid and on the
/// phone agenda, rather than the host's today.
/// </summary>
/// <remarks>
/// The clock is pinned to an instant at which the two answers differ: at 2026-09-05T23:30Z the
/// board (Europe/Amsterdam, +02:00 in September) has already turned over to the 6th, while the
/// container clock is still on the 5th. Read against the host's date, neither the 5th nor the 6th
/// of September 2026 is marked today (the real date is nowhere near it), so every fact below
/// fails until the board's today is what the page compares against.
/// </remarks>
public class CalendarBoardClockTests(WebApplicationFactoryBase factory) : IClassFixture<WebApplicationFactoryBase>
{
    private const string BoardZoneId = "Europe/Amsterdam";
    private const string CalendarUrl = "/Calendar?year=2026&month=9";
    private const string DesktopUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";
    private const string MobileUserAgent =
        "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1";

    private static readonly DateTimeOffset PinnedNow = new(2026, 9, 5, 23, 30, 0, TimeSpan.Zero);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private WebApplicationFactory<Program> CreatePinnedFactory()
    {
        return factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configBuilder) =>
            {
                configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["TimeZone:BoardTimeZoneId"] = BoardZoneId
                });
            });

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(new FixedTimeProvider(PinnedNow));
            });
        });
    }

    private async Task<string> GetCalendarAsync(string userName, string userAgent)
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var pinnedFactory = CreatePinnedFactory();

        var (client, _) = await AuthenticationHelper.CreateAuthenticatedAdminClientAsync(
            pinnedFactory, userName, $"{userName}@example.com");

        // An entry on each of the two days, so the phone agenda has a section for both.
        await BoardMoveSeed.SeedEventAsync(factory, 1, "Board Clock Fifth", new DateOnly(2026, 9, 5));
        await BoardMoveSeed.SeedEventAsync(factory, 1, "Board Clock Sixth", new DateOnly(2026, 9, 6));

        var request = new HttpRequestMessage(HttpMethod.Get, CalendarUrl);
        request.Headers.TryAddWithoutValidation("User-Agent", userAgent);
        request.Headers.Authorization = client.DefaultRequestHeaders.Authorization;
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
    }

    // The opening tag of the first element of the given class prefix whose own text matches.
    private static readonly Regex DayNumberSix = new("class=\"day-number[^\"]*\"[^>]*>\\s*6\\b", RegexOptions.Singleline);
    private static readonly Regex DayNumberFive = new("class=\"day-number[^\"]*\"[^>]*>\\s*5\\b", RegexOptions.Singleline);

    // Each chunk starts just after a day cell's opening class attribute, so its head still holds
    // the rest of that opening tag. The day number is looked up in the chunk, not matched across
    // chunks, so the fact survives a rewrite of the cell's markup.
    private static string DayCellChunk(string html, Regex dayNumber) =>
        Regex.Split(html, "class=\"calendar-day(?=[\" ])")
            .Skip(1)
            .First(chunk => dayNumber.IsMatch(chunk));

    private static string OpeningTagRemainder(string chunk)
    {
        var end = chunk.IndexOf('>');
        return end < 0 ? chunk : chunk[..end];
    }

    [Fact]
    public async Task DesktopMonth_MarksTheBoardsTodayNotTheHostsToday()
    {
        var html = await GetCalendarAsync("cbc_desktop", DesktopUserAgent);

        var sixth = OpeningTagRemainder(DayCellChunk(html, DayNumberSix));
        sixth.Should().Contain("today",
            because: "the board has already turned over to the 6th even though the host clock is still on the 5th");
        sixth.Should().Contain("aria-current=\"date\"");

        var fifth = OpeningTagRemainder(DayCellChunk(html, DayNumberFive));
        fifth.Should().NotContain("today");
        fifth.Should().NotContain("aria-current");
    }

    // The phone agenda lists one section per day with entries; the label's wording and case are
    // free to change, so sections are matched case-insensitively on the month and day.
    private static string[] AgendaSections(string html) =>
        html.Split("class=\"agenda-day-section", StringSplitOptions.None).Skip(1).ToArray();

    [Fact]
    public async Task PhoneAgenda_MarksTheBoardsTodaySection()
    {
        var html = await GetCalendarAsync("cbc_phone", MobileUserAgent);

        var sections = AgendaSections(html);
        sections.Should().NotBeEmpty(because: "the phone layout must be the one rendered");

        var marked = sections.Where(s => OpeningTagRemainder(s).Contains("aria-current=\"date\"")).ToList();
        marked.Should().ContainSingle();
        marked[0].Should().MatchRegex(new Regex("september\\s+6\\b", RegexOptions.IgnoreCase));

        var fifth = sections.Single(s => Regex.IsMatch(s, "september\\s+5\\b", RegexOptions.IgnoreCase));
        OpeningTagRemainder(fifth).Should().NotContain("aria-current");
    }
}
