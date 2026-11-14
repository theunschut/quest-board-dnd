using System.Net;
using System.Text.RegularExpressions;
using QuestBoard.Domain.Enums;
using QuestBoard.IntegrationTests.Helpers;

namespace QuestBoard.IntegrationTests.Tests;

/// <summary>
/// Proves the Players list renders its contract on both layouts: one page title, the Dungeon Masters and Registered
/// Players as section cards with an h2 title, and a name that is a link only when the board serves the Dungeon Master
/// page. The desktop holds the names in paper tables inside a responsive wrapper with a link tall enough to hit; the
/// phone holds them as rows with one stretched link each, and a name that has no page stays an inert row. Both empty
/// states are rendered on a board that has only the other kind of member. The layout is chosen from the request's
/// User-Agent, so every fact states which one it sends.
/// </summary>
public class PlayersIndexRenderTests(WebApplicationFactoryBase factory)
    : IClassFixture<WebApplicationFactoryBase>, IAsyncLifetime
{
    private const string PhoneUserAgent =
        "Mozilla/5.0 (Linux; Android 13; Pixel 7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Mobile Safari/537.36";

    private const string LongDungeonMasterName = "Archmagister Perenelle Ashworth-Thistledown of the Seven Hollow Crossings";
    private const string LongPlayerName = "Tamsin Brightwillow-Underhill the Third of the Misty Weir and Surrounding Marches";

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        factory.TestGroupContext.ActiveGroupId = 1;
        await BoardPageSeed.ResetAsync(factory, 1);
    }

    public static TheoryData<bool> BothLayouts => new() { false, true };

    private static string Tag(bool phone) => phone ? "phone" : "desktop";

    private static async Task<string> GetHtmlAsync(HttpClient client, string url, bool phone)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (phone)
            request.Headers.TryAddWithoutValidation("User-Agent", PhoneUserAgent);
        request.Headers.Authorization = client.DefaultRequestHeaders.Authorization;
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
    }

    private static string MainOf(string html)
    {
        var start = html.IndexOf("<main", StringComparison.Ordinal);
        var end = html.IndexOf("</main>", StringComparison.Ordinal);
        start.Should().BeGreaterThan(-1);
        end.Should().BeGreaterThan(start);
        return html[start..end];
    }

    private sealed record Seeded(HttpClient Client, int DungeonMasterId, int LongDungeonMasterId);

    // A board with two Dungeon Masters (one with a long name), two players (one with a long name) and the viewer, who
    // is a player too.
    private async Task<Seeded> SeedFullBoardAsync(bool phone)
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (_, dm) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, $"playerslistdm{Tag(phone)}", $"playerslistdm{Tag(phone)}@example.com",
            name: "Orlen Vashti of the Ember Weir", roles: ["DungeonMaster"]);
        var (_, longDm) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, $"playerslistlongdm{Tag(phone)}", $"playerslistlongdm{Tag(phone)}@example.com",
            name: LongDungeonMasterName, roles: ["DungeonMaster"]);
        await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, $"playerslistplayer{Tag(phone)}", $"playerslistplayer{Tag(phone)}@example.com",
            name: "Tamsin Brightwillow", roles: ["Player"]);
        await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, $"playerslistlongplayer{Tag(phone)}", $"playerslistlongplayer{Tag(phone)}@example.com",
            name: LongPlayerName, roles: ["Player"]);
        var (viewer, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, $"playerslistviewer{Tag(phone)}", $"playerslistviewer{Tag(phone)}@example.com",
            name: "Viewer Player", roles: ["Player"]);
        return new Seeded(viewer, dm.Id, longDm.Id);
    }

    // A board whose only member is a player, so the Dungeon Masters list is empty.
    private async Task<HttpClient> SeedOnlyAPlayerAsync(bool phone)
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, $"playersonlyplayer{Tag(phone)}", $"playersonlyplayer{Tag(phone)}@example.com",
            name: "Lone Player", roles: ["Player"]);
        return client;
    }

    // A board whose only member is a Dungeon Master, so the Players list is empty.
    private async Task<HttpClient> SeedOnlyADungeonMasterAsync(bool phone)
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, $"playersonlydm{Tag(phone)}", $"playersonlydm{Tag(phone)}@example.com",
            name: "Lone Dungeon Master", roles: ["DungeonMaster"]);
        return client;
    }

    [Fact]
    public async Task DesktopIndex_RendersTheTwoSectionCardsWithPaperTables()
    {
        var seeded = await SeedFullBoardAsync(phone: false);

        var html = await GetHtmlAsync(seeded.Client, "/Players", phone: false);
        var main = MainOf(html);

        html.Should().NotContain("mobile-layout");
        main.Should().Contain("players-page");
        Regex.Matches(main, @"<section\b[^>]*modern-card--aside").Should().HaveCount(2);
        Regex.IsMatch(main, @"<h2\b[^>]*>\s*<i\b[^>]*></i>Dungeon Masters\s*</h2>").Should().BeTrue();
        Regex.IsMatch(main, @"<h2\b[^>]*>\s*<i\b[^>]*></i>Registered Players\s*</h2>").Should().BeTrue();
        Regex.Matches(main, @"<div class=""table-responsive"">\s*<table class=""table table-striped table-hover"">")
            .Should().HaveCount(2, "each list is a paper table inside a responsive wrapper");
        Regex.Matches(main, @"<th scope=""col"">Name</th>").Should().HaveCount(2);
        main.Should().NotContain("<h4").And.NotContain("<h5").And.NotContain("text-warning").And.NotContain("text-primary");
    }

    [Fact]
    public async Task DesktopIndex_LinksItsOwnSheetAndNotThePhoneSheet()
    {
        var seeded = await SeedFullBoardAsync(phone: false);

        var html = await GetHtmlAsync(seeded.Client, "/Players", phone: false);

        html.Should().Contain("css/players.css");
        html.Should().NotContain("players.mobile.css");
    }

    [Fact]
    public async Task DesktopIndex_DungeonMasterNamesAreTallLinksToTheirProfileAndPlayerNamesArePlainCells()
    {
        var seeded = await SeedFullBoardAsync(phone: false);

        var html = await GetHtmlAsync(seeded.Client, "/Players", phone: false);
        var main = MainOf(html);

        Regex.IsMatch(main, $@"<a class=""players-name-link"" href=""/DungeonMaster/Profile/{seeded.DungeonMasterId}"">Orlen Vashti of the Ember Weir</a>")
            .Should().BeTrue("a Dungeon Master's name keeps its destination and carries the target-height class");
        Regex.IsMatch(main, $@"<a class=""players-name-link"" href=""/DungeonMaster/Profile/{seeded.LongDungeonMasterId}"">{LongDungeonMasterName}</a>")
            .Should().BeTrue("a long name is shown whole inside its link");
        Regex.Matches(main, @"<a\b").Should().HaveCount(2, "only the two Dungeon Master names are links");
        main.Should().Contain("<td>Tamsin Brightwillow</td>").And.Contain($"<td>{LongPlayerName}</td>");
        Regex.IsMatch(main, @"\son[a-z]+\s*=", RegexOptions.IgnoreCase).Should().BeFalse();
        Regex.IsMatch(main, @"\sstyle\s*=", RegexOptions.IgnoreCase).Should().BeFalse();
    }

    [Fact]
    public async Task DesktopIndex_DungeonMasterPageOff_ListsTheNamesAsPlainText()
    {
        var seeded = await SeedFullBoardAsync(phone: false);
        await BoardPageSeed.SetAsync(factory, 1, BoardPageSet.Empty.With(BoardPage.DungeonMasterProfile), BoardPage.Quests);

        var main = MainOf(await GetHtmlAsync(seeded.Client, "/Players", phone: false));

        main.Should().Contain("<td>").And.Contain("Orlen Vashti of the Ember Weir");
        Regex.Matches(main, @"<a\b").Should().BeEmpty("no name links to a page the board does not serve");
        main.Should().NotContain("DungeonMaster/Profile").And.NotContain("players-name-link");
    }

    [Fact]
    public async Task DesktopIndex_OnlyAPlayerOnTheBoard_ShowsTheNoDungeonMastersEmptyState()
    {
        var client = await SeedOnlyAPlayerAsync(phone: false);

        var main = MainOf(await GetHtmlAsync(client, "/Players", phone: false));

        Regex.IsMatch(main, @"<h2 class=""empty-state-heading"">No Dungeon Masters Found</h2>").Should().BeTrue();
        Regex.IsMatch(main, @"<p class=""empty-state-text"">No dungeon masters are registered yet\. Brave souls may register as new quest leaders\.</p>")
            .Should().BeTrue();
        main.Should().Contain("Lone Player");
        main.Should().NotContain("No Players Found").And.NotContain("<h4").And.NotContain("<h5");
    }

    [Fact]
    public async Task DesktopIndex_OnlyADungeonMasterOnTheBoard_ShowsTheNoPlayersEmptyState()
    {
        var client = await SeedOnlyADungeonMasterAsync(phone: false);

        var main = MainOf(await GetHtmlAsync(client, "/Players", phone: false));

        Regex.IsMatch(main, @"<h2 class=""empty-state-heading"">No Players Found</h2>").Should().BeTrue();
        Regex.IsMatch(main, @"<p class=""empty-state-text"">No adventurers have registered yet\. Players can register to join quests\.</p>")
            .Should().BeTrue();
        main.Should().Contain("Lone Dungeon Master");
        main.Should().NotContain("No Dungeon Masters Found").And.NotContain("<h4").And.NotContain("<h5");
    }
}
