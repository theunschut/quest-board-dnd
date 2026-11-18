using System.Net;
using System.Text.RegularExpressions;
using QuestBoard.Domain.Enums;
using QuestBoard.IntegrationTests.Helpers;

namespace QuestBoard.IntegrationTests.Tests;

/// <summary>
/// Pins what the Admin and Platform pages render once they sit on the layered foundation, on both layouts. The
/// layout is chosen from the request's User-Agent header, so every fact states which one it sends. Each page that is
/// converted adds its facts here; the rules a source scan can hold live in the conversion guard instead.
/// </summary>
public class AdminPlatformRenderTests(WebApplicationFactoryBase factory)
    : IClassFixture<WebApplicationFactoryBase>, IAsyncLifetime
{
    private const string DesktopUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

    private const string MobileUserAgent =
        "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1";

    private const string DeleteConfirmSentence = "Are you sure you want to delete this quest? This action cannot be undone.";

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public ValueTask DisposeAsync()
    {
        factory.TestGroupContext.ActiveGroupId = 1;
        factory.TestGroupContext.BoardType = BoardType.OneShot;
        return ValueTask.CompletedTask;
    }

    private static string AgentFor(bool mobile) => mobile ? MobileUserAgent : DesktopUserAgent;

    private static async Task<string> GetAsync(HttpClient client, string url, string userAgent)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("User-Agent", userAgent);
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
    }

    // A status badge as the shared partial writes it: the fill class, the icon class and the word.
    private static readonly Regex StatusBadge = new(
        "<span class=\"badge (?<fill>[\\w-]+)\"><i class=\"fas (?<icon>[\\w-]+) me-1\" aria-hidden=\"true\"></i>(?<word>[^<]+)</span>",
        RegexOptions.Compiled);

    private static List<(string Fill, string Icon, string Word)> StatusBadges(string html) =>
        StatusBadge.Matches(html)
            .Select(m => (m.Groups["fill"].Value, m.Groups["icon"].Value, m.Groups["word"].Value.Trim()))
            .ToList();

    private static int HeadingOneCount(string html) => Regex.Matches(html, @"<h1\b").Count;

    private async Task<HttpClient> SeedBoardAsync(BoardType boardType, string userName, params (string Title, bool Finalized, bool Closed)[] quests)
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.BoardType = boardType;

        var (client, admin) = await AuthenticationHelper.CreateAuthenticatedAdminClientAsync(
            factory, userName, userName + "@example.com", name: "Vashti Emberweir");

        foreach (var quest in quests)
        {
            await TestDataHelper.CreateTestQuestAsync(
                factory.Services, admin.Id, title: quest.Title,
                isFinalized: quest.Finalized, finalizedDate: quest.Finalized ? DateTime.UtcNow.AddDays(30) : null,
                isClosed: quest.Closed, closedDate: quest.Closed ? DateTime.UtcNow : null);
        }

        return client;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AdminQuests_OnACampaignBoard_ShowsOpenAndClosedFromTheSameMapping(bool mobile)
    {
        var client = await SeedBoardAsync(BoardType.Campaign, mobile ? "apr_camp_m" : "apr_camp_d",
            ("Open Campaign Quest", false, false), ("Closed Campaign Quest", false, true));

        var html = await GetAsync(client, "/Admin/Quests", AgentFor(mobile));

        var badges = StatusBadges(html);
        badges.Should().Contain(("bg-success", "fa-clock", "Open"));
        badges.Should().Contain(("bg-dark", "fa-flag-checkered", "Closed"));
        badges.Should().HaveCount(2);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AdminQuests_OnAOneShotBoard_ShowsFinalizedAndOpenFromTheSameMapping(bool mobile)
    {
        var client = await SeedBoardAsync(BoardType.OneShot, mobile ? "apr_one_m" : "apr_one_d",
            ("Open One-Shot Quest", false, false), ("Finalized One-Shot Quest", true, false));

        var html = await GetAsync(client, "/Admin/Quests", AgentFor(mobile));

        var badges = StatusBadges(html);
        badges.Should().Contain(("bg-primary", "fa-check-circle", "Finalized"));
        badges.Should().Contain(("bg-success", "fa-clock", "Open"));
        badges.Should().HaveCount(2);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AdminQuests_HasOnePageTitleInTheCardHeader_AndLinksItsSheets(bool mobile)
    {
        var client = await SeedBoardAsync(BoardType.OneShot, mobile ? "apr_title_m" : "apr_title_d", ("A Quest", false, false));

        var html = await GetAsync(client, "/Admin/Quests", AgentFor(mobile));

        HeadingOneCount(html).Should().Be(1);
        html.Should().MatchRegex(@"modern-card-header[^>]*>\s*<h1[^>]*>(?:\s*<i[^>]*></i>)?\s*Quest Management\s*</h1>");
        html.Should().Contain("/css/admin-pages.css");
        if (mobile)
            html.Should().Contain("/css/admin-pages.mobile.css");
        else
            html.Should().NotContain("/css/admin-pages.mobile.css");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AdminQuests_KeepsTheDeleteFormAndItsConfirmSentence(bool mobile)
    {
        var client = await SeedBoardAsync(BoardType.OneShot, mobile ? "apr_del_m" : "apr_del_d", ("A Quest", false, false));

        var html = await GetAsync(client, "/Admin/Quests", AgentFor(mobile));

        var deleteForm = Regex.Match(html, @"<form\b[^>]*action=""/Admin/DeleteQuest/\d+""[^>]*>").Value;
        deleteForm.Should().NotBeEmpty();
        deleteForm.Should().Contain("method=\"post\"");
        html.Should().Contain("data-confirm-message=\"" + DeleteConfirmSentence + "\"");
        html.Should().Contain("name=\"__RequestVerificationToken\"");
        html.Should().Contain("/js/confirm-submit.js");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AdminQuests_WithNoQuests_ShowsTheEmptyStateInEachLayoutsWords(bool mobile)
    {
        var client = await SeedBoardAsync(BoardType.OneShot, mobile ? "apr_empty_m" : "apr_empty_d");

        var html = await GetAsync(client, "/Admin/Quests", AgentFor(mobile));

        html.Should().Contain("class=\"empty-state\"");
        html.Should().MatchRegex(@"<h2 class=""empty-state-heading"">\s*No quests found\.\s*</h2>");
        if (mobile)
            html.Should().MatchRegex(@"<p class=""empty-state-text"">\s*Quests appear here once created by a DM\.\s*</p>");
        else
            html.Should().NotContain("empty-state-text");
    }

    [Fact]
    public async Task AdminQuests_OnThePhone_ShowsEachQuestAsASlip_WithoutDescriptionOrDmEmail()
    {
        var client = await SeedBoardAsync(BoardType.OneShot, "apr_slip_m", ("Slip Quest", false, false));

        var html = await GetAsync(client, "/Admin/Quests", MobileUserAgent);

        html.Should().Contain("class=\"row-slip\"");
        html.Should().Contain("row-slip__actions");
        html.Should().Contain("DM: Vashti Emberweir");
        html.Should().NotContain("Test Description", "the phone list carries no description");
        html.Should().NotContain("mailto:", "the phone list carries no address");
    }

    [Fact]
    public async Task AdminQuests_OnDesktop_ShowsTheDescriptionExcerptAndTheDmAddress()
    {
        var client = await SeedBoardAsync(BoardType.OneShot, "apr_cols_d", ("Table Quest", false, false));

        var html = await GetAsync(client, "/Admin/Quests", DesktopUserAgent);

        html.Should().Contain("quest-excerpt");
        html.Should().Contain("Test Description");
        html.Should().MatchRegex(@"mailto:apr_cols_d[^""]*@example\.com");
        html.Should().NotContain("style=\"");
    }
}
