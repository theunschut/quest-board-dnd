using System.Net;
using System.Text.RegularExpressions;
using QuestBoard.IntegrationTests.Helpers;

namespace QuestBoard.IntegrationTests.Tests;

/// <summary>
/// Proves the Characters list renders the same contract under the desktop and the phone User-Agent: one page title
/// with the add button as the primary action, two section cards with h2 titles, every character reachable through
/// exactly one stretched link, a still Main badge and a Dead or Retired badge that carries its icon and word, empty
/// states in the shared shape, and a portrait requested only for a character that has one. The layout is chosen from
/// the request's User-Agent, so every fact states which one it sends.
/// </summary>
public class CharactersIndexRenderTests(WebApplicationFactoryBase factory)
    : IClassFixture<WebApplicationFactoryBase>, IAsyncLifetime
{
    private const string DesktopUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

    private const string PhoneUserAgent =
        "Mozilla/5.0 (Linux; Android 13; Pixel 7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Mobile Safari/537.36";

    private const int Main = 0;
    private const int Backup = 1;
    private const int Active = 0;
    private const int Retired = 1;
    private const int Dead = 2;

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public ValueTask DisposeAsync()
    {
        factory.TestGroupContext.ActiveGroupId = 1;
        return ValueTask.CompletedTask;
    }

    public static TheoryData<bool> BothLayouts => new() { false };

    private static string AgentFor(bool phone) => phone ? PhoneUserAgent : DesktopUserAgent;

    private sealed record Seeded(HttpClient Client, Dictionary<string, int> Ids);

    private async Task<Seeded> SeedAsync(string tag, bool withCharacters)
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (client, user) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, $"charlist{tag}", $"charlist{tag}@example.com");
        var ids = new Dictionary<string, int>();
        if (!withCharacters)
            return new Seeded(client, ids);

        var other = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, $"charlistother{tag}", $"charlistother{tag}@example.com", name: "Roster Owner");

        async Task Add(string key, string name, int owner, int role, int status, bool picture)
        {
            var character = await TestDataHelper.CreateTestCharacterAsync(
                factory.Services, owner, name, level: 4, status: status, role: role, groupId: 1);
            ids[key] = character.Id;
            if (!picture)
                return;

            using var scope = factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
            context.Set<CharacterImageEntity>().Add(new CharacterImageEntity
            {
                Id = character.Id,
                OriginalImageData = [1, 2, 3, 4],
                CroppedImageData = [9, 9, 9, 9, 9]
            });
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await Add("mainPic", "Mine Main Pictured", user.Id, Main, Active, picture: true);
        await Add("backup", "Mine Backup Plain", user.Id, Backup, Active, picture: false);
        await Add("mainDead", "Mine Main Dead", user.Id, Main, Dead, picture: false);
        await Add("retired", "Mine Retired", user.Id, Backup, Retired, picture: false);
        await Add("otherPic", "Roster Pictured", other.Id, Backup, Active, picture: true);
        await Add("otherPlain", "Roster Plain", other.Id, Backup, Active, picture: false);
        await Add("otherDead", "Roster Dead", other.Id, Backup, Dead, picture: true);
        return new Seeded(client, ids);
    }

    private static async Task<string> GetHtmlAsync(Seeded seeded, bool phone)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/Characters");
        request.Headers.TryAddWithoutValidation("User-Agent", AgentFor(phone));
        var response = await seeded.Client.SendAsync(request, TestContext.Current.CancellationToken);
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

    private static string ItemClass(bool phone) => phone ? "character-member-row" : "character-card";

    // The markup of each character's card or row, split at the opening of the item's own class attribute.
    private static List<string> Items(string html, bool phone)
    {
        var parts = Regex.Split(MainOf(html), @"(?=<div class=""" + ItemClass(phone) + @"[ ""])");
        return parts.Skip(1).ToList();
    }

    private static string ItemFor(string html, bool phone, string name) =>
        Items(html, phone).Single(i => i.Contains(">" + name + "</a>", StringComparison.Ordinal));

    [Theory]
    [MemberData(nameof(BothLayouts))]
    public async Task Index_RendersTheTitleTheSectionsAndOneStretchedLinkPerCharacter(bool phone)
    {
        var seeded = await SeedAsync($"links{phone}", withCharacters: true);

        var html = await GetHtmlAsync(seeded, phone);
        var main = MainOf(html);

        if (phone)
            html.Should().Contain("mobile-layout");
        else
            html.Should().NotContain("mobile-layout");

        Regex.Matches(main, @"<h1\b").Should().HaveCount(1, "a page has exactly one title");
        Regex.IsMatch(main, @"<h1\b[^>]*>\s*(?:<i\b[^>]*></i>\s*)?Characters\s*</h1>").Should().BeTrue(
            "the title says Characters on both layouts");
        Regex.IsMatch(main, @"<a\b[^>]*class=""btn btn-primary[^""]*""[^>]*>\s*<i\b[^>]*></i>Create New Character\s*</a>")
            .Should().BeTrue("adding a character is the primary action of the page");
        main.Should().NotContain("btn-warning");
        Regex.IsMatch(main, @"<h2\b[^>]*>\s*(?:<i\b[^>]*></i>\s*)?My Characters\s*</h2>").Should().BeTrue();
        Regex.IsMatch(main, @"<h2\b[^>]*>\s*(?:<i\b[^>]*></i>\s*)?Character Roster\s*</h2>").Should().BeTrue();
        Regex.Matches(main, @"<h[45]\b").Should().BeEmpty("no section or card title skips the heading order");
        Regex.Matches(main, @"modern-card--aside").Count.Should().BeGreaterThanOrEqualTo(2, "both sections are aside cards");

        var items = Items(html, phone);
        items.Should().HaveCount(7);
        foreach (var item in items)
        {
            Regex.Matches(item, @"\brow-nav-link\b").Should().HaveCount(1, "one stretched link makes the whole item the target");
            item.Should().Contain("modern-card--interactive");
        }

        foreach (var (_, id) in seeded.Ids)
            main.Should().Contain($"href=\"/Characters/Details/{id}\"");

        Regex.Matches(html, @"\son[a-z]+\s*=", RegexOptions.IgnoreCase).Should().BeEmpty("no inline event handler");
        html.Should().NotContain("window.location");
    }

    [Fact]
    public async Task DesktopIndex_LinksTheSharedCardSheetThenItsOwnAndNothingGlobal()
    {
        var seeded = await SeedAsync("sheetsdesktop", withCharacters: true);

        var html = await GetHtmlAsync(seeded, phone: false);

        var shared = html.IndexOf("/css/member-cards.css", StringComparison.Ordinal);
        var own = html.IndexOf("/css/characters.css", StringComparison.Ordinal);
        shared.Should().BeGreaterThan(-1);
        own.Should().BeGreaterThan(shared, "the list sheet comes after the shared card sheet");
        html.Should().Contain("characters-page");
        html.Should().NotContain("characters.mobile.css");
        Regex.Matches(html, @"/css/characters\.css").Should().HaveCount(1, "the sheet loads once, from the page");
        Regex.IsMatch(MainOf(html), @"<section\b[^>]*aria-labelledby=").Should().BeTrue("each section is named by its title");
    }

    [Theory]
    [MemberData(nameof(BothLayouts))]
    public async Task Index_BadgesCarryIconAndWordForMainDeadAndRetired(bool phone)
    {
        var seeded = await SeedAsync($"badges{phone}", withCharacters: true);

        var html = await GetHtmlAsync(seeded, phone);

        var main = ItemFor(html, phone, "Mine Main Pictured");
        main.Should().MatchRegex(@"badge badge-main""><i class=""fas fa-star[^""]*""[^>]*></i>Main</span>");
        main.Should().NotContain("fa-skull").And.NotContain("fa-moon");

        var dead = ItemFor(html, phone, "Roster Dead");
        dead.Should().MatchRegex(@"badge bg-secondary""><i class=""fas fa-skull[^""]*""[^>]*></i>Dead</span>");
        dead.Should().Contain(phone ? "member-row--dead" : "member-card--dead");

        var retired = ItemFor(html, phone, "Mine Retired");
        retired.Should().MatchRegex(@"badge bg-info""><i class=""fas fa-moon[^""]*""[^>]*></i>Retired</span>");
        retired.Should().Contain(phone ? "member-row--retired" : "member-card--retired");

        var both = ItemFor(html, phone, "Mine Main Dead");
        both.Should().Contain("badge-main").And.Contain("fa-skull");

        var plain = ItemFor(html, phone, "Mine Backup Plain");
        plain.Should().NotContain("badge");
        plain.Should().NotContain("--dead").And.NotContain("--retired");
    }

    [Theory]
    [MemberData(nameof(BothLayouts))]
    public async Task Index_RequestsAPortraitOnlyForACharacterThatHasOne(bool phone)
    {
        var seeded = await SeedAsync($"pics{phone}", withCharacters: true);

        var html = await GetHtmlAsync(seeded, phone);

        foreach (var key in new[] { "mainPic", "otherPic", "otherDead" })
            html.Should().Contain($"GetCroppedPicture/{seeded.Ids[key]}", $"{key} has a stored picture");

        foreach (var key in new[] { "backup", "mainDead", "retired", "otherPlain" })
        {
            html.Should().NotContain($"GetCroppedPicture/{seeded.Ids[key]}", $"{key} has no stored picture");
            html.Should().NotContain($"GetCroppedPicture?id={seeded.Ids[key]}");
        }

        Regex.Matches(html, @"\sonerror\s*=", RegexOptions.IgnoreCase).Should().BeEmpty();

        var plain = ItemFor(html, phone, "Mine Backup Plain");
        plain.Should().Contain(phone ? "avatar-32 avatar-32--placeholder" : "member-card-placeholder");
        plain.Should().NotContain("<img");

        var pictured = ItemFor(html, phone, "Mine Main Pictured");
        pictured.Should().Contain("<img");
        if (phone)
            pictured.Should().Contain("class=\"avatar-32");
        else
            pictured.Should().Contain("member-card-image");
    }

    [Theory]
    [MemberData(nameof(BothLayouts))]
    public async Task Index_ShowsTheClassLevelAndOwnerLines(bool phone)
    {
        var seeded = await SeedAsync($"lines{phone}", withCharacters: true);

        var html = await GetHtmlAsync(seeded, phone);

        var roster = ItemFor(html, phone, "Roster Plain");
        roster.Should().Contain("Roster Owner");
        roster.Should().Contain("Level 4");
        ItemFor(html, phone, "Mine Backup Plain").Should().NotContain("Roster Owner");
    }

    [Theory]
    [MemberData(nameof(BothLayouts))]
    public async Task Index_EmptyStatesUseTheSharedShapeVerbatim(bool phone)
    {
        var seeded = await SeedAsync($"empty{phone}", withCharacters: false);

        var html = await GetHtmlAsync(seeded, phone);
        var main = MainOf(html);

        Regex.Matches(main, @"class=""empty-state""").Should().HaveCount(2);
        Regex.IsMatch(main, @"<h2 class=""empty-state-heading"">No Characters Yet</h2>").Should().BeTrue();
        main.Should().Contain("Create your first character to get started!");
        Regex.IsMatch(main, @"<a\b[^>]*class=""btn btn-secondary""[^>]*>\s*<i\b[^>]*></i>Create Character\s*</a>")
            .Should().BeTrue("the empty-state action is a secondary button");
        Regex.IsMatch(main, @"<h2 class=""empty-state-heading"">No Other Characters</h2>").Should().BeTrue();
        main.Should().Contain("No other adventurers have created characters yet!");
        main.Should().NotContain("text-muted").And.NotContain("character-empty-state");
        Items(html, phone).Should().BeEmpty();
    }
}
