using System.Net;
using System.Text.RegularExpressions;
using QuestBoard.Domain.Enums;
using QuestBoard.IntegrationTests.Helpers;

namespace QuestBoard.IntegrationTests.Tests;

/// <summary>
/// The operator's walk, driven only through the super admin's Platform Edit form: a board is set to
/// open on its calendar with Shop and Contacts switched off, a player then lives on it (the root
/// address, both menus, the brand link, the refused pages), and switching everything back restores
/// the board exactly as it was. Every settings change goes through the form's own posted fields, so
/// the form, the rules, the storage, the resolver, the gate, the menus and the home redirect are
/// exercised together. Each walk runs on the desktop and the mobile user agent because the two
/// layouts are separate files that drift apart.
///
/// What these facts do NOT establish: how the pages look, or how the writes translate to SQL. They
/// read markup and wording, and the shared harness uses the EF Core InMemory provider.
/// </summary>
public class BoardPageEndToEndTests(WebApplicationFactoryBase factory)
    : IClassFixture<WebApplicationFactoryBase>, IAsyncLifetime
{
    private const string DesktopUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

    private const string MobileUserAgent =
        "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1";

    private const int OpenedBoard = 1;
    private const int CampaignBoard = 2;
    private const string OpenedBoardName = "EuphoriaInn";
    private const string CampaignBoardName = "Campaign Board Two";
    private const string ShopItemName = "Lantern of Keeping";
    private const string ContactName = "Mirela the Cartographer";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static readonly Regex BrandTag =
        new(@"<a\b[^>]*class=""[^""]*navbar-brand[^""]*""[^>]*>", RegexOptions.Compiled);

    private static readonly Regex HrefAttribute = new(@"href=""([^""]*)""", RegexOptions.Compiled);

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        await BoardPageSeed.ResetAsync(factory, OpenedBoard);
        await BoardPageSeed.ResetAsync(factory, CampaignBoard);
        factory.TestGroupContext.ActiveGroupId = 1;
        factory.TestGroupContext.BoardType = BoardType.OneShot;
    }

    private static async Task<(HttpResponseMessage Response, string Html)> GetAsync(
        HttpClient client, string url, string userAgent)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("User-Agent", userAgent);
        var response = await client.SendAsync(request, Token);
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync(Token));
        return (response, html);
    }

    private static string Location(HttpResponseMessage response) => response.Headers.Location!.OriginalString;

    // Everything before the page body, so a link inside the page itself can never be mistaken for a
    // menu entry. Both layouts render their navigation ahead of the main element.
    private static string NavigationOf(string html)
    {
        var mainStart = html.IndexOf("<main", StringComparison.Ordinal);
        mainStart.Should().BeGreaterThan(0, "the layout renders a main element after its navigation");
        return html[..mainStart];
    }

    private static string BrandHref(string nav)
    {
        var tag = BrandTag.Match(nav);
        tag.Success.Should().BeTrue("the layout renders a brand link");
        var href = HrefAttribute.Match(tag.Value);
        href.Success.Should().BeTrue("the brand link has a destination");
        return href.Groups[1].Value;
    }

    private static string QuestsItemId(string userAgent) =>
        userAgent == MobileUserAgent ? "id=\"nav-quests-mobile\"" : "id=\"nav-quests\"";

    // A desktop row is a <tr>, a mobile row is a group-card-mobile block; the one that names the
    // board and carries its Open Board button is the board's own row on either layout.
    private static string BoardListRow(string html, string boardName)
    {
        var rows = Regex.Split(html, "<tr[ >]|class=\"group-card-mobile");
        var row = rows.SingleOrDefault(r => r.Contains(boardName) && r.Contains("Open Board"));
        row.Should().NotBeNull($"the board list should hold a row for '{boardName}'");
        return row!;
    }

    // What a browser sends back from the Edit form: the hidden page name of every unlocked row, the
    // checkbox value when it is ticked and the hidden false that always follows it, plus the home
    // page select. Switches not named in the disabled set are posted ticked.
    private static async Task<HttpResponseMessage> SubmitEditFormAsync(
        HttpClient admin, int boardId, string boardName, string userAgent, BoardPageSet disabled, BoardPage home)
    {
        var (edit, html) = await GetAsync(admin, $"/platform/Group/Edit/{boardId}", userAgent);
        edit.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("Pages");

        var form = new List<KeyValuePair<string, string>>
        {
            new("Id", boardId.ToString()),
            new("Name", boardName),
        };

        var pageFields = Regex.Matches(html, @"<input[^>]*name=""(Pages\[\d+\]\.Page)""[^>]*value=""(\w+)""[^>]*>");
        pageFields.Count.Should().Be(9, "a one-shot board offers a switch for every page");
        foreach (Match field in pageFields)
        {
            var page = Enum.Parse<BoardPage>(field.Groups[2].Value);
            var enabledName = field.Groups[1].Value.Replace(".Page", ".Enabled");
            form.Add(new(field.Groups[1].Value, page.ToString()));
            if (!disabled.Contains(page))
            {
                form.Add(new(enabledName, "true"));
            }

            form.Add(new(enabledName, "false"));
        }

        form.Add(new("HomePage", home.ToString()));

        var post = new HttpRequestMessage(HttpMethod.Post, "/platform/Group/Edit")
        {
            Content = new FormUrlEncodedContent(form)
        };
        post.Headers.TryAddWithoutValidation("User-Agent", userAgent);
        return await admin.SendAsync(post, Token);
    }

    private async Task SeedBoardsAsync()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.ActiveGroupId = OpenedBoard;
        factory.TestGroupContext.BoardType = BoardType.OneShot;
        await BoardMoveSeed.SeedBoardAsync(factory, CampaignBoard, CampaignBoardName, BoardType.Campaign);
    }

    [Theory]
    [InlineData(DesktopUserAgent)]
    [InlineData(MobileUserAgent)]
    public async Task SuperAdmin_OpensABoardOnItsCalendarWithPagesTrimmed_ThenPutsItBackExactlyAsItWas(string userAgent)
    {
        var key = userAgent == MobileUserAgent ? "bpe2e_m" : "bpe2e_d";
        await SeedBoardsAsync();

        var (_, dm) = await AuthenticationHelper.CreateAuthenticatedDMClientAsync(
            factory, $"{key}_dm", $"{key}_dm@example.com");
        await TestDataHelper.CreateShopItemAsync(factory.Services, dm.Id, name: ShopItemName);
        await TestDataHelper.CreateTestContactAsync(factory.Services, dm.Id, ContactName, isRevealed: true);

        var (player, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, $"{key}_player", $"{key}_player@example.com");
        var (admin, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(
            factory, $"{key}_admin", $"{key}_admin@example.com");

        // The board as it starts: no configuration, so the front door leads to the quest list.
        var startRoot = await player.GetAsync("/", Token);
        startRoot.StatusCode.Should().Be(HttpStatusCode.Redirect);
        Location(startRoot).Should().Be("/quests");

        // The operator trims the board through the Platform form.
        var trimmed = BoardPageSet.Empty.With(BoardPage.Shop).With(BoardPage.Contacts);
        var save = await SubmitEditFormAsync(admin, OpenedBoard, OpenedBoardName, userAgent, trimmed, BoardPage.Calendar);
        save.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found);
        Location(save).Should().Be("/platform");

        var (listResponse, listHtml) = await GetAsync(admin, Location(save), userAgent);
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        listHtml.Should().Contain("Board settings saved.");
        BoardListRow(listHtml, OpenedBoardName).Should().Contain("Home: Calendar");
        BoardListRow(listHtml, CampaignBoardName).Should().NotContain("Home:",
            because: "only the configured board carries the home badge");

        // The player's board now opens on its calendar, with the unused pages gone.
        var trimmedRoot = await player.GetAsync("/", Token);
        trimmedRoot.StatusCode.Should().Be(HttpStatusCode.Redirect);
        Location(trimmedRoot).Should().Be("/Calendar");

        var (calendar, calendarHtml) = await GetAsync(player, "/Calendar", userAgent);
        calendar.StatusCode.Should().Be(HttpStatusCode.OK);
        var nav = NavigationOf(calendarHtml);
        BrandHref(nav).Should().Be("/Calendar");
        nav.Should().Contain(QuestsItemId(userAgent), because: "the quest list is no longer the home, so it needs its own menu item");
        var questsTag = Regex.Match(nav, @"<a\b[^>]*" + Regex.Escape(QuestsItemId(userAgent)) + @"[^>]*>");
        HrefAttribute.Match(questsTag.Value).Groups[1].Value.Should().Be("/quests");
        nav.Should().NotContain("href=\"/Shop\"");
        nav.Should().NotContain("href=\"/Contacts\"");
        nav.Should().Contain("href=\"/QuestLog\"", because: "pages left on keep their menu items");

        // The server refuses what the menus hide, and says why on the page it lands on.
        var shop = await GetAsync(player, "/Shop", userAgent);
        shop.Response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        Location(shop.Response).Should().Be("/Calendar");
        var (shopLanding, shopLandingHtml) = await GetAsync(player, Location(shop.Response), userAgent);
        shopLanding.StatusCode.Should().Be(HttpStatusCode.OK);
        shopLandingHtml.Should().Contain("Shop isn't used on this board.");

        var contacts = await GetAsync(player, "/Contacts", userAgent);
        contacts.Response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        Location(contacts.Response).Should().Be("/Calendar");

        // A Campaign board offers fewer pages, and says so on its own Edit page.
        var (campaignEdit, campaignHtml) = await GetAsync(admin, $"/platform/Group/Edit/{CampaignBoard}", userAgent);
        campaignEdit.StatusCode.Should().Be(HttpStatusCode.OK);
        Regex.Matches(campaignHtml, @"Not available on Campaign boards\.").Count.Should().Be(3,
            because: "Shop, Players and DM profile are greyed out with their reason");
        foreach (var page in new[] { BoardPage.Shop, BoardPage.Players, BoardPage.DungeonMasterProfile })
        {
            var input = Regex.Match(campaignHtml, $"<input[^>]*id=\"page-{page}\"[^>]*>");
            input.Success.Should().BeTrue($"the Campaign form carries a row for {page}");
            input.Value.Should().Contain("disabled");
        }

        // Switching everything back restores the board exactly.
        var restore = await SubmitEditFormAsync(admin, OpenedBoard, OpenedBoardName, userAgent, BoardPageSet.Empty, BoardPage.Quests);
        restore.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found);

        var (restoredList, restoredListHtml) = await GetAsync(admin, "/platform/Group", userAgent);
        restoredList.StatusCode.Should().Be(HttpStatusCode.OK);
        BoardListRow(restoredListHtml, OpenedBoardName).Should().NotContain("Home:");

        var restoredRoot = await player.GetAsync("/", Token);
        restoredRoot.StatusCode.Should().Be(HttpStatusCode.Redirect);
        Location(restoredRoot).Should().Be("/quests");

        var (quests, questsHtml) = await GetAsync(player, "/quests", userAgent);
        quests.StatusCode.Should().Be(HttpStatusCode.OK);
        var restoredNav = NavigationOf(questsHtml);
        BrandHref(restoredNav).Should().Be("/quests");
        restoredNav.Should().NotContain("id=\"nav-quests", because: "with the quest list as home it needs no menu item of its own");
        restoredNav.Should().Contain("href=\"/Shop\"");
        restoredNav.Should().Contain("href=\"/Contacts\"");
        restoredNav.Should().Contain("href=\"/QuestLog\"");

        var (shopBack, shopBackHtml) = await GetAsync(player, "/Shop", userAgent);
        shopBack.StatusCode.Should().Be(HttpStatusCode.OK);
        shopBackHtml.Should().Contain(ShopItemName, because: "switching a page off never discards what was on it");

        var (contactsBack, contactsBackHtml) = await GetAsync(player, "/Contacts", userAgent);
        contactsBack.StatusCode.Should().Be(HttpStatusCode.OK);
        contactsBackHtml.Should().Contain(ContactName);
    }
}
