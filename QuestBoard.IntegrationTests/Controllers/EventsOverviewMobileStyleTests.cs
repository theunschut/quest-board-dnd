using QuestBoard.Domain.Enums;
using QuestBoard.IntegrationTests.Helpers;
using System.Net;

namespace QuestBoard.IntegrationTests.Controllers;

/// <summary>
/// Guards the mobile availability overview's styling contract. The mobile card list at
/// /Events is selected by the request's user agent rather than by a CSS breakpoint, and its
/// styling was not covered by the calendar or Platform-area style-conformance tests already in
/// this project. This class pins the card's glass surface, the explicit light colour on every
/// text run inside it, the filled-button convention, and the tap-target floor on the roster
/// toggle, so a regression in any of them turns a test red instead of only being caught by eye.
/// </summary>
public class EventsOverviewMobileStyleTests(WebApplicationFactoryBase factory)
    : IClassFixture<WebApplicationFactoryBase>, IAsyncLifetime
{
    private const string MobileUserAgent =
        "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1";

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public ValueTask DisposeAsync()
    {
        factory.TestGroupContext.ActiveGroupId = 1;
        factory.TestGroupContext.BoardType = BoardType.OneShot;
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Resolves the path to events-overview.mobile.css by walking up from
    /// AppContext.BaseDirectory until a "QuestBoard.Service" child directory is found. Fails
    /// with a descriptive message naming the attempted path if it cannot be resolved, so the
    /// facts below work from any working directory.
    /// </summary>
    private static string ResolveOverviewMobileCssPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir != null)
        {
            var candidate = Path.Combine(
                dir.FullName, "QuestBoard.Service", "wwwroot", "css", "events-overview.mobile.css");
            if (File.Exists(candidate))
                return candidate;

            dir = dir.Parent;
        }

        var attemptedBase = AppContext.BaseDirectory;
        var attemptedPath = Path.Combine(
            attemptedBase, "QuestBoard.Service", "wwwroot", "css", "events-overview.mobile.css");
        throw new FileNotFoundException(
            $"events-overview.mobile.css not found. Searched upward from '{attemptedBase}'. " +
            $"Last attempted path: '{attemptedPath}'. " +
            "Ensure QuestBoard.Service/wwwroot/css/events-overview.mobile.css exists in the repo.",
            attemptedPath);
    }

    /// <summary>
    /// Extracts the body of a single CSS rule given the exact "selector {" text that opens it,
    /// so an assertion can be scoped to that one rule instead of matching a declaration that
    /// happens to repeat elsewhere in the file. Returns an empty string when the selector is
    /// not found, so a deleted rule fails the containing assertion instead of throwing.
    /// </summary>
    private static string ExtractCssRule(string css, string selectorWithOpenBrace)
    {
        var start = css.IndexOf(selectorWithOpenBrace, StringComparison.Ordinal);
        if (start < 0)
            return string.Empty;

        var end = css.IndexOf('}', start);
        return end < 0 ? string.Empty : css[start..(end + 1)];
    }

    // Attaches the mobile user agent header to a request and sends it through the supplied
    // authenticated client, so the client's default authorization header still applies.
    private async Task<(HttpResponseMessage Response, string Html)> GetMobileAsync(HttpClient client, string url)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("User-Agent", MobileUserAgent);
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        return (response, html);
    }

    private async Task<int> SeedEventAsync(string title, DateOnly date)
    {
        await using var ctx = factory.Database.CreateContext();
        var eventEntity = new EventEntity
        {
            Title = title,
            GroupId = 1,
            Date = date,
            CreatedAt = DateTime.UtcNow
        };
        ctx.Events.Add(eventEntity);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        return eventEntity.Id;
    }

    private async Task<int> SeedMemberAsync(string userNamePrefix, string name)
    {
        var user = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, userNamePrefix, $"{userNamePrefix}@example.com", name: name);

        await using var ctx = factory.Database.CreateContext();
        ctx.UserGroups.Add(new UserGroupEntity { UserId = user.Id, GroupId = 1, GroupRole = (int)GroupRole.Player });
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        return user.Id;
    }

    private async Task SeedSignupAsync(int eventId, int userId, VoteType availability, bool confirmed)
    {
        await using var ctx = factory.Database.CreateContext();
        ctx.EventSignups.Add(new EventSignupEntity
        {
            EventId = eventId,
            UserId = userId,
            Availability = (int)availability,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = confirmed ? DateTime.UtcNow : null
        });
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public void MobileOverviewCss_AvailCard_UsesGlassSurfaceNotOpaqueSlab()
    {
        var cssPath = ResolveOverviewMobileCssPath();
        var css = File.ReadAllText(cssPath);
        var lowerCss = css.ToLowerInvariant();

        css.Should().Contain(
            "background: rgba(255, 255, 255, 0.15);",
            because: $"events-overview.mobile.css at '{cssPath}' must put the mobile card on the app's shared glass surface");
        css.Should().Contain(
            "backdrop-filter: blur(15px);",
            because: $"events-overview.mobile.css at '{cssPath}' must apply the same blur the rest of the app's glass cards use");
        lowerCss.Should().NotContain(
            "#343a40",
            because: "the opaque slab background the card used before the fix must not be reintroduced");
        lowerCss.Should().NotContain(
            "#495057",
            because: "the opaque pressed-state background the card used before the fix must not be reintroduced");
    }

    [Fact]
    public void MobileOverviewCss_CountBlock_SetsExplicitLightColour()
    {
        var cssPath = ResolveOverviewMobileCssPath();
        var css = File.ReadAllText(cssPath);

        var headlineRule = ExtractCssRule(css, ".avail-card .avail-count-headline {");
        headlineRule.Should().Contain(
            "color: #FFFFFF;",
            because: $"the count headline rule in '{cssPath}' must state its own colour instead of inheriting the desktop table cell's dark text");

        var detailRule = ExtractCssRule(css, ".avail-card .avail-count-detail {");
        detailRule.Should().Contain(
            "color: #FFFFFF !important;",
            because: $"the count detail rule in '{cssPath}' must beat Bootstrap's .text-muted on this dark surface, or the page repeats its measured 1.34:1 contrast failure");
    }

    [Fact]
    public void MobileOverviewCss_ExpandToggle_KeepsMinimumTapTarget()
    {
        var cssPath = ResolveOverviewMobileCssPath();
        var css = File.ReadAllText(cssPath);

        css.Should().Contain(
            "min-height: 44px",
            because: $"the roster expand toggle rule in '{cssPath}' must keep the 44px iOS/Android tap-target floor");
    }

    [Fact]
    public async Task MobileOverview_RenderedPage_UsesFilledButtonsNotOutline()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.BoardType = BoardType.Campaign;
        var eventOneId = await SeedEventAsync(
            "Style Guard Session One", DateOnly.FromDateTime(DateTime.Today.AddDays(1)));
        await SeedEventAsync(
            "Style Guard Session Two", DateOnly.FromDateTime(DateTime.Today.AddDays(2)));
        var memberId = await SeedMemberAsync("evtoverview_mstylebtn", "Style Guard Member");
        await SeedSignupAsync(eventOneId, memberId, VoteType.Yes, confirmed: true);

        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "evtoverview_mstylebtn_viewer", "evtoverview_mstylebtn_viewer@example.com");

        var (response, html) = await GetMobileAsync(client, "/Events");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("btn btn-sm btn-secondary");
        html.Should().NotContain("btn-outline-");
    }

    [Fact]
    public async Task MobileOverview_RenderedPage_EmitsStyledCardClasses()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.BoardType = BoardType.Campaign;
        var eventOneId = await SeedEventAsync(
            "Style Guard Class Session One", DateOnly.FromDateTime(DateTime.Today.AddDays(1)));
        await SeedEventAsync(
            "Style Guard Class Session Two", DateOnly.FromDateTime(DateTime.Today.AddDays(2)));
        var memberId = await SeedMemberAsync("evtoverview_mstylecls", "Style Guard Class Member");
        await SeedSignupAsync(eventOneId, memberId, VoteType.Yes, confirmed: true);

        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "evtoverview_mstylecls_viewer", "evtoverview_mstylecls_viewer@example.com");

        var (response, html) = await GetMobileAsync(client, "/Events");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("avail-card");
        html.Should().Contain("avail-count-summary");
        html.Should().Contain("avail-card-meta");
        html.Should().Contain("avail-roster-name");
    }
}
