using System.Net;
using System.Text.RegularExpressions;
using QuestBoard.IntegrationTests.Helpers;
using QuestBoard.Repository.Entities;

namespace QuestBoard.IntegrationTests.Tests;

/// <summary>
/// Proves the Manage Categories page on both layouts: one page title, the table kept inside a responsive container
/// in its own card on the desktop, named reorder buttons, the delete question built on the server with the same
/// words the page's script used to build, and the empty state's wording. The layout is chosen from the request's
/// User-Agent header, so every fact states which one it sends.
/// </summary>
public class CategoryManageRenderTests : IClassFixture<WebApplicationFactoryBase>, IAsyncLifetime
{
    private const string DesktopUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

    private const string PhoneUserAgent =
        "Mozilla/5.0 (Linux; Android 13; Pixel 7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Mobile Safari/537.36";

    private readonly WebApplicationFactoryBase _factory;

    public CategoryManageRenderTests(WebApplicationFactoryBase factory)
    {
        _factory = factory;
    }

    public async ValueTask InitializeAsync() => await TestDataHelper.ClearDatabaseAsync(_factory.Services);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private async Task<(HttpClient Client, int UserId)> DungeonMasterAsync(string key)
    {
        var (client, user) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, $"catmanage{key}", $"catmanage{key}@example.com", roles: ["DungeonMaster"]);
        return (client, user.Id);
    }

    private async Task SeedCategoryWithContactsAsync(string name, int sortOrder, int contacts, int createdByUserId)
    {
        var category = await TestDataHelper.CreateTestContactCategoryAsync(_factory.Services, name, sortOrder, groupId: 1);
        for (var i = 0; i < contacts; i++)
        {
            var contact = await TestDataHelper.CreateTestContactAsync(
                _factory.Services, createdByUserId, $"{name} contact {i}", isRevealed: true, groupId: 1);
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
            var entity = await context.Contacts.SingleAsync(c => c.Id == contact.Id, TestContext.Current.CancellationToken);
            entity.CategoryId = category.Id;
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
    }

    private static async Task<string> GetManageAsync(HttpClient client, string userAgent)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/ContactCategoryManagement");
        request.Headers.TryAddWithoutValidation("User-Agent", userAgent);
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
        return Regex.Replace(html[start..end], @"<!--[\s\S]*?-->", " ");
    }

    // The sentence the page's own script built before the confirmation moved to the server, kept here word for word.
    private static string ExpectedSentence(string name, int count) => count > 0
        ? "Delete \"" + name + "\"? This will move " + count + " contact" + (count == 1 ? "" : "s") + " to Ungrouped."
        : "Delete \"" + name + "\"? It has no contacts to move.";

    private static List<string> ConfirmMessages(string page) =>
        Regex.Matches(page, @"<form\b[^>]*\bdata-confirm-message=""(?<m>[^""]*)""")
            .Select(m => WebUtility.HtmlDecode(m.Groups["m"].Value)).ToList();

    [Fact]
    public async Task Manage_Desktop_RendersOneTitleTheTableInAResponsiveCardAndNoInlineStyle()
    {
        var (client, userId) = await DungeonMasterAsync("deskone");
        await SeedCategoryWithContactsAsync("Allies of the Ember Weir", 0, 1, userId);

        var html = await GetManageAsync(client, DesktopUserAgent);
        var page = MainOf(html);

        html.Should().NotContain("mobile-layout");
        Regex.Matches(page, @"<h1\b").Should().HaveCount(1);
        Regex.IsMatch(page, @"<h1\b[^>]*>\s*(?:<i\b[^>]*></i>\s*)?Manage Categories\s*</h1>").Should().BeTrue(
            "the page title is the one heading");
        page.Should().NotContain("<h2", "the table card carries no heading of its own");
        page.Should().Contain("contact-categories-page");
        page.Should().NotContain("contacts-page", "the page wrapper is the category page's own");
        page.Should().Contain("modern-card--aside");

        Regex.IsMatch(page, @"<div class=""table-responsive"">\s*<table\b[^>]*aria-label=""Manage Categories""").Should().BeTrue(
            "the table scrolls inside its card and is named with the page title words");

        var headers = Regex.Matches(page, @"<th\b[^>]*>\s*(?<t>[^<]+?)\s*</th>").Select(m => m.Groups["t"].Value).ToList();
        headers.Should().Equal("Order", "Name", "Contacts", "Actions");

        Regex.Matches(page, @"\sstyle\s*=", RegexOptions.IgnoreCase).Should().BeEmpty("no inline style remains");
        Regex.Matches(page, @"<script\b(?![^>]*\bsrc\s*=)", RegexOptions.IgnoreCase).Should().BeEmpty("no inline script remains");
        html.Should().Contain("contact-categories.css");
        html.Should().NotContain("contact-categories.mobile.css");
        Regex.IsMatch(html, @"<script\b[^>]*\btype=""module""[^>]*\bsrc=""/js/confirm-submit\.js[^""]*""").Should().BeTrue(
            "the page loads the shared confirm module");
    }

    [Fact]
    public async Task Manage_Desktop_ColumnWidthsAreClassesAndTheButtonsKeepTheirRoles()
    {
        var (client, userId) = await DungeonMasterAsync("deskcols");
        await SeedCategoryWithContactsAsync("Rivals", 0, 2, userId);

        var page = MainOf(await GetManageAsync(client, DesktopUserAgent));

        page.Should().Contain("category-col-order");
        page.Should().Contain("category-col-contacts");
        Regex.Matches(page, @"btn btn-secondary btn-sm""\s+aria-label=""Move Rivals (?:up|down)""").Should().HaveCount(2);
        Regex.IsMatch(page, @"btn btn-warning btn-sm me-2""[^>]*>\s*<i class=""fas fa-pen me-1"" aria-hidden=""true""></i>Rename").Should().BeTrue(
            "the rename action keeps its label and the warning role");
        Regex.IsMatch(page, @"btn btn-danger btn-sm"">\s*<i class=""fas fa-trash me-1"" aria-hidden=""true""></i>Delete").Should().BeTrue();
        page.Should().NotContain("btn-outline-secondary");
    }

    [Fact]
    public async Task Manage_Desktop_DeleteQuestionIsBuiltOnTheServerWithTheSameWords()
    {
        var (client, userId) = await DungeonMasterAsync("deskconfirm");
        await SeedCategoryWithContactsAsync("Empty Guild", 0, 0, userId);
        await SeedCategoryWithContactsAsync("Lone Scout", 1, 1, userId);
        await SeedCategoryWithContactsAsync("Wardens of the Lantern Gate", 2, 3, userId);

        var page = MainOf(await GetManageAsync(client, DesktopUserAgent));

        ConfirmMessages(page).Should().Equal(
            ExpectedSentence("Empty Guild", 0),
            ExpectedSentence("Lone Scout", 1),
            ExpectedSentence("Wardens of the Lantern Gate", 3));
        page.Should().NotContain("data-category-name");
        page.Should().NotContain("data-contact-count");
    }

    [Fact]
    public async Task Manage_Desktop_ReorderGroupsAndButtonsAreNamedAfterTheCategory()
    {
        var (client, userId) = await DungeonMasterAsync("deskreorder");
        await SeedCategoryWithContactsAsync("First Hall", 0, 0, userId);
        await SeedCategoryWithContactsAsync("Second Hall", 1, 0, userId);

        var page = MainOf(await GetManageAsync(client, DesktopUserAgent));

        foreach (var name in new[] { "First Hall", "Second Hall" })
        {
            page.Should().Contain($"role=\"group\" aria-label=\"Reorder {name}\"");
            page.Should().Contain($"aria-label=\"Move {name} up\"");
            page.Should().Contain($"aria-label=\"Move {name} down\"");
        }
    }

    [Fact]
    public async Task Manage_Desktop_HostileCategoryName_IsEncodedInTheConfirmationAttribute()
    {
        var (client, _) = await DungeonMasterAsync("deskhostile");
        await TestDataHelper.CreateTestContactCategoryAsync(
            _factory.Services, "\"><script>alert('x')</script>", sortOrder: 0, groupId: 1);

        var html = await GetManageAsync(client, DesktopUserAgent);

        html.Should().NotContain("<script>alert('x')</script>");
        ConfirmMessages(MainOf(html)).Should().ContainSingle().Which.Should().Be(
            ExpectedSentence("\"><script>alert('x')</script>", 0),
            "the attribute decodes back to the exact sentence, the markup never breaks out of it");
    }

    [Fact]
    public async Task Manage_Desktop_NoCategories_RendersTheEmptyStateVerbatim()
    {
        var (client, _) = await DungeonMasterAsync("deskempty");

        var page = MainOf(await GetManageAsync(client, DesktopUserAgent));

        page.Should().Contain("No Categories Yet");
        page.Should().Contain("Add your first category above to start grouping your contacts.");
        page.Should().Contain("empty-state");
        page.Should().NotContain("<table");
    }

    [Fact]
    public async Task Manage_Phone_RendersTheHeadingRowsAndTheAddFormInsideThePhoneLayout()
    {
        var (client, userId) = await DungeonMasterAsync("phoneone");
        await SeedCategoryWithContactsAsync("Allies of the Ember Weir", 0, 1, userId);
        await SeedCategoryWithContactsAsync("Rivals", 1, 2, userId);

        var html = await GetManageAsync(client, PhoneUserAgent);
        var page = MainOf(html);

        html.Should().Contain("mobile-layout");
        Regex.Matches(page, @"<h1\b").Should().HaveCount(1);
        Regex.IsMatch(page, @"<h1\b[^>]*>\s*Manage Categories\s*</h1>").Should().BeTrue(
            "the phone page names itself with the same words as the desktop page");
        Regex.Matches(page, @"class=""category-mgmt-row""").Should().HaveCount(2);
        page.Should().Contain("category-mgmt-add-form");
        page.Should().Contain("New Category Name");
        page.Should().Contain("1 contact<").And.Contain("2 contacts<");
        page.Should().NotContain("<table", "the phone shows slips, not a table");
        page.Should().NotContain("contacts-page");

        html.Should().Contain("contact-categories.mobile.css");
        html.Should().NotContain("contacts.mobile.css");
        Regex.Matches(page, @"\sstyle\s*=", RegexOptions.IgnoreCase).Should().BeEmpty();
        Regex.Matches(page, @"<script\b(?![^>]*\bsrc\s*=)", RegexOptions.IgnoreCase).Should().BeEmpty();
        Regex.IsMatch(html, @"<script\b[^>]*\btype=""module""[^>]*\bsrc=""/js/confirm-submit\.js[^""]*""").Should().BeTrue();
    }

    [Fact]
    public async Task Manage_Phone_DeleteQuestionIsTheSameSentenceAsTheDesktopForEveryCount()
    {
        var (client, userId) = await DungeonMasterAsync("phoneconfirm");
        await SeedCategoryWithContactsAsync("Empty Guild", 0, 0, userId);
        await SeedCategoryWithContactsAsync("Lone Scout", 1, 1, userId);
        await SeedCategoryWithContactsAsync("Wardens of the Lantern Gate", 2, 3, userId);

        var phone = ConfirmMessages(MainOf(await GetManageAsync(client, PhoneUserAgent)));
        var desktop = ConfirmMessages(MainOf(await GetManageAsync(client, DesktopUserAgent)));

        phone.Should().Equal(
            ExpectedSentence("Empty Guild", 0),
            ExpectedSentence("Lone Scout", 1),
            ExpectedSentence("Wardens of the Lantern Gate", 3));
        phone.Should().Equal(desktop);
    }

    [Fact]
    public async Task Manage_Phone_ButtonsKeepTheirRolesAndNames()
    {
        var (client, userId) = await DungeonMasterAsync("phonebuttons");
        await SeedCategoryWithContactsAsync("First Hall", 0, 0, userId);
        await SeedCategoryWithContactsAsync("Second Hall", 1, 0, userId);

        var page = MainOf(await GetManageAsync(client, PhoneUserAgent));

        foreach (var name in new[] { "First Hall", "Second Hall" })
        {
            page.Should().Contain($"role=\"group\" aria-label=\"Reorder {name}\"");
            page.Should().Contain($"aria-label=\"Move {name} up\"");
            page.Should().Contain($"aria-label=\"Move {name} down\"");
        }
        Regex.Matches(page, @"btn btn-secondary""\s+aria-label=""Move ").Should().HaveCount(4);
        Regex.Matches(page, @"btn btn-warning""[^>]*>\s*<i class=""fas fa-pen me-1"" aria-hidden=""true""></i>Rename").Should().HaveCount(2);
        Regex.Matches(page, @"btn btn-danger"">\s*<i class=""fas fa-trash me-1"" aria-hidden=""true""></i>Delete").Should().HaveCount(2);
        Regex.IsMatch(page, @"btn btn-primary"">\s*<i class=""fas fa-plus me-2"" aria-hidden=""true""></i>Add Category").Should().BeTrue();
    }

    [Fact]
    public async Task Manage_Phone_NoCategories_RendersTheEmptyStateVerbatim()
    {
        var (client, _) = await DungeonMasterAsync("phoneempty");

        var page = MainOf(await GetManageAsync(client, PhoneUserAgent));

        page.Should().Contain("No Categories Yet");
        page.Should().Contain("Add your first category above to start grouping your contacts.");
        page.Should().Contain("empty-state");
        page.Should().NotContain("category-mgmt-row");
    }
}
