using System.Net;
using System.Text.RegularExpressions;
using QuestBoard.IntegrationTests.Helpers;

namespace QuestBoard.IntegrationTests.Tests;

/// <summary>
/// Proves the Contacts list renders its contract: one page title, the Dungeon Master actions with a single primary
/// button, one section card per category group with an h2 title, every contact reachable through exactly one
/// stretched link, a Hidden badge that carries its icon and word, and empty states in the shared shape. The layout is
/// chosen from the request's User-Agent, so every fact states which one it sends.
/// </summary>
public class ContactsIndexRenderTests(WebApplicationFactoryBase factory)
    : IClassFixture<WebApplicationFactoryBase>, IAsyncLifetime
{
    private const string PhoneUserAgent =
        "Mozilla/5.0 (Linux; Android 13; Pixel 7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Mobile Safari/537.36";

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public ValueTask DisposeAsync()
    {
        factory.TestGroupContext.ActiveGroupId = 1;
        return ValueTask.CompletedTask;
    }

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

    private static string ItemClass(bool phone) => phone ? "contact-member-row" : "contact-card";

    // The markup of each contact's card or row, split at the opening of the item's own class attribute.
    private static List<string> Items(string html, bool phone) =>
        Regex.Split(MainOf(html), @"(?=<div class=""" + ItemClass(phone) + @"[ ""])").Skip(1).ToList();

    private static string ItemFor(string html, bool phone, string name) =>
        Items(html, phone).Single(i => i.Contains(">" + name + "</a>", StringComparison.Ordinal));

    private static async Task AssignCategoryAsync(int contactId, int categoryId, IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
        var contact = await context.Contacts.SingleAsync(c => c.Id == contactId, TestContext.Current.CancellationToken);
        contact.CategoryId = categoryId;
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private sealed record Seeded(HttpClient Client, Dictionary<string, int> Ids);

    // A board with two categories, an ungrouped contact, a picture, a long name and one tag, seen by a Dungeon Master.
    private async Task<Seeded> SeedBoardAsync(string tag)
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (client, dm) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, $"contactlist{tag}", $"contactlist{tag}@example.com", roles: ["DungeonMaster"]);
        var ids = new Dictionary<string, int>();

        var allies = await TestDataHelper.CreateTestContactCategoryAsync(factory.Services, "Allies of the Weir", sortOrder: 0);
        var rivals = await TestDataHelper.CreateTestContactCategoryAsync(factory.Services, "Rivals", sortOrder: 1);

        var pictured = await TestDataHelper.CreateTestContactAsync(
            factory.Services, dm.Id, "Pictured Ally", townCity: "Emberford", subLocation: "The Gilded Lantern",
            isRevealed: true, imageData: [1, 2, 3, 4]);
        await AssignCategoryAsync(pictured.Id, allies.Id, factory.Services);
        ids["pictured"] = pictured.Id;

        var plain = await TestDataHelper.CreateTestContactAsync(factory.Services, dm.Id, "Plain Rival", isRevealed: true);
        await AssignCategoryAsync(plain.Id, rivals.Id, factory.Services);
        ids["plain"] = plain.Id;

        var longName = await TestDataHelper.CreateTestContactAsync(
            factory.Services, dm.Id, "Lady Perenelle Ashworth-Thistledown of the Seven Hollow Crossings and the Mist",
            isRevealed: true);
        ids["long"] = longName.Id;

        await TestDataHelper.CreateTestContactTagAsync(factory.Services, "lantern", 1, pictured.Id);
        return new Seeded(client, ids);
    }

    [Fact]
    public async Task DesktopIndex_RendersTheTitleTheActionsAndTheGroupCards()
    {
        var seeded = await SeedBoardAsync("deskstructure");

        var html = await GetHtmlAsync(seeded.Client, "/Contacts", phone: false);
        var main = MainOf(html);

        Regex.Matches(main, @"<h1\b").Should().HaveCount(1, "the page has one title");
        Regex.IsMatch(main, @"<h1\b[^>]*>\s*(?:<i\b[^>]*></i>\s*)?Contacts\s*</h1>").Should().BeTrue();
        Regex.Matches(main, @"class=""[^""]*\bbtn-primary\b").Should().HaveCount(1, "+ Contact is the one primary action");
        Regex.IsMatch(main, @"<a\b[^>]*class=""btn btn-primary""[^>]*>\s*<i\b[^>]*></i>\s*Contact\s*</a>").Should().BeTrue();
        Regex.IsMatch(main, @"<a\b[^>]*class=""btn btn-secondary""[^>]*>\s*<i\b[^>]*></i>\s*Manage Categories\s*</a>")
            .Should().BeTrue("Manage Categories is a secondary action");
        main.Should().NotContain("btn-warning").And.NotContain("btn-outline");

        Regex.Matches(main, @"<section\b[^>]*modern-card--aside").Should().HaveCount(3,
            "each category group and the ungrouped one is a section card");
        foreach (var title in new[] { "Allies of the Weir", "Rivals", "Ungrouped" })
            Regex.IsMatch(main, @"<h2\b[^>]*>\s*<i\b[^>]*></i>" + title + @"\s*</h2>").Should().BeTrue(
                $"the group {title} is titled with an h2");
        main.Should().NotContain("category-heading", "the ungrouped title is the same section title as the others");
    }

    [Fact]
    public async Task DesktopIndex_LinksTheSharedCardSheetBeforeItsOwn()
    {
        var seeded = await SeedBoardAsync("desksheets");

        var html = await GetHtmlAsync(seeded.Client, "/Contacts", phone: false);

        var shared = html.IndexOf("css/member-cards.css", StringComparison.Ordinal);
        var own = html.IndexOf("css/contacts.css", StringComparison.Ordinal);
        shared.Should().BeGreaterThan(-1);
        own.Should().BeGreaterThan(shared, "the list sheet is linked after the shared card sheet");
        html.Should().NotContain("contacts.mobile.css");
    }

    [Fact]
    public async Task DesktopIndex_KeepsTheFilterRowWithSecondaryButtons()
    {
        var seeded = await SeedBoardAsync("deskfilter");

        var html = await GetHtmlAsync(seeded.Client, "/Contacts", phone: false);
        var main = MainOf(html);

        main.Should().Contain("contact-filter-row");
        main.Should().Contain("contact-filter-check-label");
        Regex.IsMatch(main, @"<button\b[^>]*class=""btn btn-secondary btn-sm[^""]*""[^>]*>\s*<i\b[^>]*></i>Apply Filters").Should().BeTrue();
        main.Should().NotContain("contact-filter-clear-btn\"", "the clear button only shows while a filter is on");

        var lantern = await TestDataHelper.CreateTestContactTagAsync(factory.Services, "tavern", 1);
        var filtered = await GetHtmlAsync(seeded.Client, $"/Contacts?tag={lantern.Id}", phone: false);
        Regex.IsMatch(MainOf(filtered), @"<a\b[^>]*class=""btn btn-secondary btn-sm[^""]*""[^>]*>\s*<i\b[^>]*></i>Clear Filters")
            .Should().BeTrue("a filter that is on offers a secondary clear button");
    }

    [Fact]
    public async Task DesktopIndex_GivesEveryContactOneStretchedLinkAndNoInlineHandler()
    {
        var seeded = await SeedBoardAsync("desklinks");

        var html = await GetHtmlAsync(seeded.Client, "/Contacts", phone: false);

        var items = Items(html, phone: false);
        items.Should().HaveCount(3);
        foreach (var item in items)
        {
            Regex.Matches(item, @"<a\b").Should().HaveCount(1, "a card holds one link, the stretched one");
            Regex.Matches(item, @"\bclass=""row-nav-link""").Should().HaveCount(1);
            item.Should().Contain("member-card modern-card modern-card--interactive");
        }

        foreach (var key in new[] { "pictured", "plain", "long" })
            html.Should().Contain($"/Contacts/Details/{seeded.Ids[key]}");
        Regex.IsMatch(MainOf(html), @"\son[a-z]+\s*=", RegexOptions.IgnoreCase).Should().BeFalse(
            "no inline event handler remains on the page");
        Regex.IsMatch(MainOf(html), @"\sstyle\s*=", RegexOptions.IgnoreCase).Should().BeFalse();
    }

    [Fact]
    public async Task DesktopIndex_ShowsThePictureOnlyForAContactThatHasOne()
    {
        var seeded = await SeedBoardAsync("deskpicture");

        var html = await GetHtmlAsync(seeded.Client, "/Contacts", phone: false);

        var pictured = ItemFor(html, phone: false, "Pictured Ally");
        pictured.Should().Contain($"GetCroppedContactImage/{seeded.Ids["pictured"]}");
        pictured.Should().NotContain("member-card-placeholder");

        var plain = ItemFor(html, phone: false, "Plain Rival");
        plain.Should().Contain("member-card-placeholder");
        plain.Should().NotContain("GetCroppedContactImage");
        html.Should().NotContain($"GetCroppedContactImage/{seeded.Ids["plain"]}");
    }

    [Fact]
    public async Task DesktopIndex_ShowsTheLocationLinesAndTheTagChipsToADungeonMaster()
    {
        var seeded = await SeedBoardAsync("desklines");

        var html = await GetHtmlAsync(seeded.Client, "/Contacts", phone: false);

        var pictured = ItemFor(html, phone: false, "Pictured Ally");
        pictured.Should().Contain("Emberford").And.Contain("The Gilded Lantern");
        Regex.IsMatch(pictured, @"<span class=""contact-tag-chip badge bg-secondary"">lantern</span>").Should().BeTrue(
            "a tag is a 14px badge and keeps its hook class");
    }

    [Fact]
    public async Task DesktopIndex_HiddenContact_ShowsTheBadgeWithIconAndWordToADungeonMasterOnly()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (dmClient, dm) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "contactlisthiddendm", "contactlisthiddendm@example.com", roles: ["DungeonMaster"]);
        await TestDataHelper.CreateTestContactAsync(factory.Services, dm.Id, "Secret Informant", isRevealed: false);
        await TestDataHelper.CreateTestContactAsync(factory.Services, dm.Id, "Known Innkeeper", isRevealed: true);
        (await dmClient.PostAsync("/Contacts/ToggleShowHidden", new FormUrlEncodedContent([]),
            TestContext.Current.CancellationToken)).StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.OK);

        var dmHtml = await GetHtmlAsync(dmClient, "/Contacts", phone: false);

        var hidden = ItemFor(dmHtml, phone: false, "Secret Informant");
        Regex.IsMatch(hidden, @"<span class=""badge bg-secondary""><i class=""fas fa-eye-slash me-1"" aria-hidden=""true""></i>Hidden</span>")
            .Should().BeTrue("the badge carries the icon and the word");
        ItemFor(dmHtml, phone: false, "Known Innkeeper").Should().NotContain("Hidden");

        var (playerClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "contactlisthiddenplayer", "contactlisthiddenplayer@example.com", roles: ["Player"]);
        var playerHtml = await GetHtmlAsync(playerClient, "/Contacts", phone: false);
        playerHtml.Should().NotContain("Secret Informant");
        playerHtml.Should().Contain("Known Innkeeper");
        MainOf(playerHtml).Should().NotContain("btn-primary", "a player has no add action");
    }

    [Fact]
    public async Task DesktopIndex_LongNamesWrapInsideTheCard()
    {
        var seeded = await SeedBoardAsync("desklong");

        var html = await GetHtmlAsync(seeded.Client, "/Contacts", phone: false);

        var item = ItemFor(html, phone: false, "Lady Perenelle Ashworth-Thistledown of the Seven Hollow Crossings and the Mist");
        item.Should().Contain("member-card-name");
    }

    [Fact]
    public async Task DesktopIndex_EmptyBoard_ShowsTheNoContactsEmptyState()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "contactlistemptydesk", "contactlistemptydesk@example.com", roles: ["DungeonMaster"]);

        var html = await GetHtmlAsync(client, "/Contacts", phone: false);
        var main = MainOf(html);

        main.Should().Contain("empty-state");
        Regex.IsMatch(main, @"<h2 class=""empty-state-heading"">No Contacts Yet</h2>").Should().BeTrue();
        Regex.IsMatch(main, @"<p class=""empty-state-text"">No one has added a contact yet\. DMs can create the first one to start building out the world\.</p>")
            .Should().BeTrue();
        main.Should().NotContain("<h4").And.NotContain("<h5");
    }

    [Fact]
    public async Task DesktopIndex_FilterThatMatchesNothing_ShowsTheNoMatchEmptyStateWithClearFilters()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "contactlistnomatchdesk", "contactlistnomatchdesk@example.com", roles: ["DungeonMaster"]);

        var html = await GetHtmlAsync(client, "/Contacts?tag=999999", phone: false);
        var main = MainOf(html);

        Regex.IsMatch(main, @"<h2 class=""empty-state-heading"">No contacts match your filters</h2>").Should().BeTrue();
        Regex.IsMatch(main, @"<p class=""empty-state-text"">Try selecting different tags\.</p>").Should().BeTrue();
        Regex.IsMatch(main, @"<a\b[^>]*class=""btn btn-secondary""[^>]*>\s*<i\b[^>]*></i>Clear filters\s*</a>").Should().BeTrue(
            "the desktop button keeps its lower-case label");
        main.Should().NotContain("<h4").And.NotContain("<h5");
    }

    [Fact]
    public async Task PhoneIndex_RendersTheTitleTheActionsAndTheGroupCards()
    {
        var seeded = await SeedBoardAsync("phonestructure");

        var html = await GetHtmlAsync(seeded.Client, "/Contacts", phone: true);
        var main = MainOf(html);

        html.Should().Contain("mobile-layout");
        Regex.Matches(main, @"<h1\b").Should().HaveCount(1, "the page has one title");
        Regex.IsMatch(main, @"<h1\b[^>]*>\s*Contacts\s*</h1>").Should().BeTrue();

        // The filter drawer's Apply button sits in a dialog; outside it + Contact is the one primary action.
        var beforeDrawer = main[..main.IndexOf("id=\"contactFilterOffcanvas\"", StringComparison.Ordinal)];
        Regex.Matches(beforeDrawer, @"class=""[^""]*\bbtn-primary\b").Should().HaveCount(1);
        Regex.IsMatch(beforeDrawer, @"<a\b[^>]*class=""btn btn-primary""[^>]*>\s*<i\b[^>]*></i>Contact\s*</a>").Should().BeTrue();
        beforeDrawer.Should().NotContain("btn-warning").And.NotContain("btn-outline");

        Regex.Matches(main, @"<section\b[^>]*modern-card--aside").Should().HaveCount(3);
        foreach (var title in new[] { "Allies of the Weir", "Rivals", "Ungrouped" })
            Regex.IsMatch(main, @"<h2\b[^>]*>\s*<i\b[^>]*></i>" + title + @"\s*</h2>").Should().BeTrue(
                $"the group {title} is titled with an h2");
        main.Should().NotContain("contact-section-heading", "the ungrouped title is the same section title as the others");
        html.Should().NotContain("contact-card", "the card grid is the desktop layout's");
    }

    [Fact]
    public async Task PhoneIndex_LinksTheSharedCardSheetBeforeItsOwn()
    {
        var seeded = await SeedBoardAsync("phonesheets");

        var html = await GetHtmlAsync(seeded.Client, "/Contacts", phone: true);

        var shared = html.IndexOf("css/member-cards.css", StringComparison.Ordinal);
        var own = html.IndexOf("css/contacts.mobile.css", StringComparison.Ordinal);
        shared.Should().BeGreaterThan(-1);
        own.Should().BeGreaterThan(shared);
        html.Should().NotContain("css/contacts.css");
    }

    [Fact]
    public async Task PhoneIndex_GivesEveryContactOneStretchedLinkAndNoInlineHandler()
    {
        var seeded = await SeedBoardAsync("phonelinks");

        var html = await GetHtmlAsync(seeded.Client, "/Contacts", phone: true);

        var items = Items(html, phone: true);
        items.Should().HaveCount(3);
        foreach (var item in items)
        {
            Regex.Matches(item, @"<a\b").Should().HaveCount(1, "a row holds one link, the stretched one");
            Regex.Matches(item, @"\bclass=""row-nav-link""").Should().HaveCount(1);
            item.Should().Contain("member-row modern-card modern-card--interactive");
        }

        foreach (var key in new[] { "pictured", "plain", "long" })
            html.Should().Contain($"/Contacts/Details/{seeded.Ids[key]}");
        Regex.IsMatch(MainOf(html), @"\son[a-z]+\s*=", RegexOptions.IgnoreCase).Should().BeFalse();
        MainOf(html).Should().NotContain("window.location");
        Regex.IsMatch(MainOf(html), @"\sstyle\s*=", RegexOptions.IgnoreCase).Should().BeFalse();
    }

    [Fact]
    public async Task PhoneIndex_ShowsThePictureOnlyForAContactThatHasOne()
    {
        var seeded = await SeedBoardAsync("phonepicture");

        var html = await GetHtmlAsync(seeded.Client, "/Contacts", phone: true);

        var pictured = ItemFor(html, phone: true, "Pictured Ally");
        pictured.Should().Contain($"GetCroppedContactImage/{seeded.Ids["pictured"]}");
        pictured.Should().Contain("Emberford").And.Contain("The Gilded Lantern");
        pictured.Should().Contain(@"<span class=""contact-tag-chip badge bg-secondary"">lantern</span>");

        var plain = ItemFor(html, phone: true, "Plain Rival");
        plain.Should().Contain("avatar-32--placeholder");
        plain.Should().NotContain("GetCroppedContactImage");
    }

    [Fact]
    public async Task PhoneIndex_KeepsTheFilterTagsButtonAsASecondaryActionWithItsActiveBadge()
    {
        var seeded = await SeedBoardAsync("phonefilter");

        var plain = MainOf(await GetHtmlAsync(seeded.Client, "/Contacts", phone: true));
        var trigger = Regex.Match(plain, @"<button\b[^>]*contact-filter-toggle[^>]*>[\s\S]*?</button>").Value;
        trigger.Should().Contain("btn btn-secondary").And.Contain("Filter Tags");
        trigger.Should().NotContain("Active", "no filter is on");
        trigger.Should().Contain("contactFilterOffcanvas");

        var tag = await TestDataHelper.CreateTestContactTagAsync(factory.Services, "tavern", 1);
        var filtered = MainOf(await GetHtmlAsync(seeded.Client, $"/Contacts?tag={tag.Id}", phone: true));
        Regex.IsMatch(filtered, @"Filter Tags\s*<span class=""badge bg-success ms-1"">Active</span>").Should().BeTrue(
            "a filter that is on is told by a badge with the word Active");
    }

    [Fact]
    public async Task PhoneIndex_NoTags_DisablesTheFilterButton()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (client, dm) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "contactlistnotagsphone", "contactlistnotagsphone@example.com", roles: ["DungeonMaster"]);
        await TestDataHelper.CreateTestContactAsync(factory.Services, dm.Id, "Untagged Contact", isRevealed: true);

        var main = MainOf(await GetHtmlAsync(client, "/Contacts", phone: true));

        var trigger = Regex.Match(main, @"<button\b[^>]*contact-filter-toggle[^>]*>").Value;
        trigger.Should().Contain("btn btn-secondary").And.EndWith("disabled>");
        main.Should().NotContain("class=\"offcanvas offcanvas-bottom\" id=\"contactFilterOffcanvas\"");
    }

    [Fact]
    public async Task PhoneIndex_HiddenContact_ShowsTheBadgeWithIconAndWord()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (dmClient, dm) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "contactlisthiddenphonedm", "contactlisthiddenphonedm@example.com", roles: ["DungeonMaster"]);
        await TestDataHelper.CreateTestContactAsync(factory.Services, dm.Id, "Secret Informant", isRevealed: false);
        (await dmClient.PostAsync("/Contacts/ToggleShowHidden", new FormUrlEncodedContent([]),
            TestContext.Current.CancellationToken)).StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.OK);

        var html = await GetHtmlAsync(dmClient, "/Contacts", phone: true);

        Regex.IsMatch(ItemFor(html, phone: true, "Secret Informant"),
            @"<span class=""badge bg-secondary""><i class=""fas fa-eye-slash me-1"" aria-hidden=""true""></i>Hidden</span>")
            .Should().BeTrue("the badge carries the icon and the word");

        var (playerClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "contactlisthiddenphoneplayer", "contactlisthiddenphoneplayer@example.com", roles: ["Player"]);
        (await GetHtmlAsync(playerClient, "/Contacts", phone: true)).Should().NotContain("Secret Informant");
    }

    [Fact]
    public async Task PhoneIndex_BoardWithoutCategories_KeepsItsContactsSectionTitle()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (dmClient, dm) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "contactlistnocatphone", "contactlistnocatphone@example.com", roles: ["DungeonMaster"]);
        await TestDataHelper.CreateTestContactAsync(factory.Services, dm.Id, "Loose Contact", isRevealed: true);

        var main = MainOf(await GetHtmlAsync(dmClient, "/Contacts", phone: true));

        Regex.IsMatch(main, @"<h2\b[^>]*>\s*<i\b[^>]*></i>Contacts\s*</h2>").Should().BeTrue();
        Items(await GetHtmlAsync(dmClient, "/Contacts", phone: true), phone: true).Should().HaveCount(1);
    }

    [Fact]
    public async Task PhoneIndex_EmptyBoard_ShowsTheNoContactsEmptyState()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "contactlistemptyphone", "contactlistemptyphone@example.com", roles: ["DungeonMaster"]);

        var main = MainOf(await GetHtmlAsync(client, "/Contacts", phone: true));

        Regex.IsMatch(main, @"<h2 class=""empty-state-heading"">No Contacts Yet</h2>").Should().BeTrue();
        Regex.IsMatch(main, @"<p class=""empty-state-text"">No one has added a contact yet\. DMs can create the first one to start building out the world\.</p>")
            .Should().BeTrue();
        main.Should().NotContain("<h5 class=\"empty").And.NotContain("contact-empty-state");
    }

    [Fact]
    public async Task PhoneIndex_FilterThatMatchesNothing_ShowsTheNoMatchEmptyStateWithClearFilters()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "contactlistnomatchphone", "contactlistnomatchphone@example.com", roles: ["DungeonMaster"]);

        var main = MainOf(await GetHtmlAsync(client, "/Contacts?tag=999999", phone: true));

        Regex.IsMatch(main, @"<h2 class=""empty-state-heading"">No contacts match your filters</h2>").Should().BeTrue();
        Regex.IsMatch(main, @"<p class=""empty-state-text"">Try selecting different tags\.</p>").Should().BeTrue();
        Regex.IsMatch(main, @"<a\b[^>]*class=""btn btn-secondary""[^>]*>Clear Filters</a>").Should().BeTrue(
            "the phone button keeps its capitalised label");
    }
}
