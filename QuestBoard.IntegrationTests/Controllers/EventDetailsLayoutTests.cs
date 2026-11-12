using System.Net;
using System.Text.RegularExpressions;
using QuestBoard.Domain.Enums;
using QuestBoard.IntegrationTests.Helpers;

namespace QuestBoard.IntegrationTests.Controllers;

// Pins the reading order of the event details page. The answer card is the page's main action, so
// it comes first after the title block; the cancelled notice sits directly above it; Description and
// the roster follow. The page has one markup order for both layouts, so what a keyboard or screen
// reader meets is what the eye meets. A Dungeon Master's Actions card stays in the left column on
// the phone too, which puts it before the answer card there and beside it on the desktop.
public class EventDetailsLayoutTests(WebApplicationFactoryBase factory)
    : IClassFixture<WebApplicationFactoryBase>, IAsyncLifetime
{
    private const string MobileUserAgent =
        "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1";

    private const string DesktopUserAgent =
        "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0 Safari/537.36";

    private static readonly Regex CardWithHeading = new(
        @"<div class=""(?<cls>card modern-card[^""]*)"">\s*<div class=""card-header modern-card-header"">\s*<h(?<level>[12])[^>]*>(?<inner>.*?)</h\k<level>>",
        RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex Tag = new("<[^>]+>", RegexOptions.Compiled);

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public ValueTask DisposeAsync()
    {
        factory.TestGroupContext.ActiveGroupId = 1;
        factory.TestGroupContext.BoardType = BoardType.OneShot;
        return ValueTask.CompletedTask;
    }

    private async Task<int> SeedEventAsync(bool cancelled = false)
    {
        await using var ctx = factory.Database.CreateContext();
        var eventEntity = new EventEntity
        {
            Title = "Council Session",
            Description = "Bring the maps.",
            GroupId = 1,
            Date = DateOnly.FromDateTime(DateTime.Today),
            CancelledAt = cancelled ? DateTime.UtcNow : null,
            CreatedAt = DateTime.UtcNow
        };
        ctx.Events.Add(eventEntity);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);

        return eventEntity.Id;
    }

    private async Task<string> GetBodyAsync(HttpClient client, int eventId, string userAgent)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/Events/Details/{eventId}");
        request.Headers.TryAddWithoutValidation("User-Agent", userAgent);
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
    }

    private static string Text(string html) => WebUtility.HtmlDecode(Tag.Replace(html, string.Empty)).Trim();

    private static int IndexOfHeading(string body, string headingText)
    {
        var match = CardWithHeading.Matches(body).FirstOrDefault(m => Text(m.Groups["inner"].Value) == headingText);
        match.Should().NotBeNull($"the page must render a card titled '{headingText}'");
        return match!.Index;
    }

    [Fact]
    public async Task Cards_FollowTheCardRoles()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var eventId = await SeedEventAsync();
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "evtlayout_roles_dm", "evtlayout_roles_dm@example.com", roles: ["DungeonMaster"]);

        foreach (var userAgent in new[] { DesktopUserAgent, MobileUserAgent })
        {
            var body = await GetBodyAsync(client, eventId, userAgent);
            var cards = CardWithHeading.Matches(body)
                .Select(m => (Cls: m.Groups["cls"].Value, Level: m.Groups["level"].Value, Title: Text(m.Groups["inner"].Value)))
                .ToList();

            var titleCard = cards.Should().ContainSingle(c => c.Level == "1").Subject;
            titleCard.Title.Should().Be("Council Session");
            titleCard.Cls.Should().NotContain("modern-card--aside", "the title card is the page title card");

            foreach (var title in new[] { "Actions", "Your Availability", "Description", "Who's Coming" })
            {
                var card = cards.Should().ContainSingle(c => c.Title == title).Subject;
                card.Level.Should().Be("2");
                card.Cls.Should().Contain("modern-card--aside", $"'{title}' is a section card, so its title is the body-face step");
            }
        }
    }

    [Theory]
    [InlineData("desktop")]
    [InlineData("phone")]
    public async Task Player_SeesAvailabilityBeforeDescription(string layout)
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var eventId = await SeedEventAsync();
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "evtlayout_player_" + layout, $"evtlayout_player_{layout}@example.com");

        var body = await GetBodyAsync(client, eventId, layout == "phone" ? MobileUserAgent : DesktopUserAgent);

        var title = body.IndexOf("<h1", StringComparison.Ordinal);
        var availability = IndexOfHeading(body, "Your Availability");
        var description = IndexOfHeading(body, "Description");
        var roster = IndexOfHeading(body, "Who's Coming");

        title.Should().BeGreaterThan(-1);
        title.Should().BeLessThan(availability);
        availability.Should().BeLessThan(description);
        description.Should().BeLessThan(roster);
        body.Should().NotContain(">Actions<", "a player has no Actions card");
        CardWithHeading.Matches(body).Select(m => Text(m.Groups["inner"].Value)).Should().NotContain("Actions");
    }

    [Fact]
    public async Task CancelledEvent_AlertSitsDirectlyAboveAvailability()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var eventId = await SeedEventAsync(cancelled: true);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "evtlayout_cancelled", "evtlayout_cancelled@example.com");

        var body = await GetBodyAsync(client, eventId, DesktopUserAgent);

        var alert = body.IndexOf("This session has been cancelled.", StringComparison.Ordinal);
        var availability = IndexOfHeading(body, "Your Availability");

        alert.Should().BeGreaterThan(-1);
        alert.Should().BeLessThan(availability);

        // Nothing but the alert's own closing markup sits between the alert and the availability card.
        var between = body[alert..availability];
        between.Should().NotContain("<h1").And.NotContain("<h2").And.NotContain("modern-card-header");
        IndexOfHeading(body, "Description").Should().BeGreaterThan(availability);
    }

    [Fact]
    public async Task DungeonMaster_PhoneOrderFollowsTheApprovedChoice()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var eventId = await SeedEventAsync();
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "evtlayout_phone_dm", "evtlayout_phone_dm@example.com", roles: ["DungeonMaster"]);

        var body = await GetBodyAsync(client, eventId, MobileUserAgent);

        // The approved order keeps the left column first on the phone: title card, Actions, then
        // Your Availability, in one markup order so focus order equals what is seen.
        var title = body.IndexOf("<h1", StringComparison.Ordinal);
        var actions = IndexOfHeading(body, "Actions");
        var availability = IndexOfHeading(body, "Your Availability");

        title.Should().BeLessThan(actions);
        actions.Should().BeLessThan(availability);
    }

    [Fact]
    public async Task DungeonMaster_DesktopKeepsActionsInTheLeftColumn()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var eventId = await SeedEventAsync();
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "evtlayout_desktop_dm", "evtlayout_desktop_dm@example.com", roles: ["DungeonMaster"]);

        var body = await GetBodyAsync(client, eventId, DesktopUserAgent);

        var left = body.IndexOf("col-lg-4", StringComparison.Ordinal);
        var right = body.IndexOf("col-lg-8", StringComparison.Ordinal);
        var actions = IndexOfHeading(body, "Actions");
        var availability = IndexOfHeading(body, "Your Availability");

        left.Should().BeGreaterThan(-1);
        left.Should().BeLessThan(actions);
        actions.Should().BeLessThan(right, "Actions belongs to the left column");
        right.Should().BeLessThan(availability, "the answer card opens the right column");
    }

    [Fact]
    public async Task Page_HasOneH1AndSectionH2s()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var eventId = await SeedEventAsync();
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "evtlayout_headings", "evtlayout_headings@example.com", roles: ["DungeonMaster"]);

        var body = await GetBodyAsync(client, eventId, DesktopUserAgent);
        var main = body[body.IndexOf("id=\"main-content\"", StringComparison.Ordinal)..];

        Regex.Matches(main, "<h1[ >]").Count.Should().Be(1);
        Regex.Matches(main, "<h4[ >]").Count.Should().Be(0);
        var h2s = Regex.Matches(main, "<h2[^>]*>(?<inner>.*?)</h2>", RegexOptions.Singleline)
            .Select(m => Text(m.Groups["inner"].Value))
            .ToList();
        h2s.Should().Equal("Actions", "Your Availability", "Description", "Who's Coming");
    }
}
