using System.Net;
using System.Text.RegularExpressions;
using QuestBoard.IntegrationTests.Helpers;
using QuestBoard.Repository.Entities;

namespace QuestBoard.IntegrationTests.Tests;

/// <summary>
/// Proves the contact details page is one view for both layouts: the phone and the desktop user agents get the
/// same title card, actions, description and notes, the notes are slips on one glass card with named icon controls,
/// the delete questions go through the shared confirm module rather than inline handlers, and every empty or
/// restricted state renders the same wording on both.
/// </summary>
public class ContactDetailsRenderTests : IClassFixture<WebApplicationFactoryBase>, IAsyncLifetime
{
    private const string DesktopUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

    private const string PhoneUserAgent =
        "Mozilla/5.0 (Linux; Android 13; Pixel 7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Mobile Safari/537.36";

    private const string NoteSentence = "Delete this note? This action cannot be undone.";

    private const string ContactSentence = "Are you sure you want to delete this contact? This action cannot be undone.";

    private readonly WebApplicationFactoryBase _factory;

    public ContactDetailsRenderTests(WebApplicationFactoryBase factory)
    {
        _factory = factory;
    }

    public static TheoryData<string, bool> UserAgents => new()
    {
        { DesktopUserAgent, false },
        { PhoneUserAgent, true },
    };

    public async ValueTask InitializeAsync() => await TestDataHelper.ClearDatabaseAsync(_factory.Services);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private sealed record Seeded(HttpClient Client, int ContactId, string Name);

    private static string Suffix(bool isPhone) => isPhone ? "p" : "d";

    private async Task<Seeded> SeedAsync(
        string key,
        bool full = true,
        bool revealed = true,
        string? description = null,
        params string[] notes)
    {
        var (client, dm) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, $"contdet{key}", $"contdet{key}@example.com", name: $"Detail DM {key}", roles: ["DungeonMaster"]);
        var name = $"Aurelia {key} Fenwhistle";
        var contact = await TestDataHelper.CreateTestContactAsync(
            _factory.Services,
            dm.Id,
            name,
            townCity: full ? "Emberford" : null,
            subLocation: full ? "The Gilded Lantern" : null,
            isRevealed: revealed,
            groupId: 1,
            imageData: full ? [1, 2, 3, 4] : null);

        if (full)
        {
            var category = await TestDataHelper.CreateTestContactCategoryAsync(_factory.Services, $"Allies {key}", 0, 1);
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
            var entity = await context.Contacts.SingleAsync(c => c.Id == contact.Id, TestContext.Current.CancellationToken);
            entity.CategoryId = category.Id;
            entity.Description = description ?? "Keeper of the **Lantern Gate**.";
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
            await TestDataHelper.CreateTestContactTagAsync(_factory.Services, $"lantern{key}", 1, contact.Id);
            await TestDataHelper.CreateTestContactTagAsync(_factory.Services, $"giver{key}", 1, contact.Id);
        }

        foreach (var note in notes)
        {
            await TestDataHelper.CreateTestContactNoteAsync(_factory.Services, contact.Id, dm.Id, note);
        }

        return new Seeded(client, contact.Id, name);
    }

    private static async Task<(HttpStatusCode Status, string Html)> GetDetailsAsync(
        HttpClient client, int contactId, string userAgent)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/Contacts/Details/{contactId}");
        request.Headers.TryAddWithoutValidation("User-Agent", userAgent);
        request.Headers.Authorization = client.DefaultRequestHeaders.Authorization;
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        return (response.StatusCode, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    private static string WithoutComments(string html) => Regex.Replace(html, @"<!--[\s\S]*?-->", " ");

    [Theory]
    [MemberData(nameof(UserAgents))]
    public async Task Details_DungeonMaster_ServesOneViewWithTitleCategoryNotesAndFormOnBothLayouts(string userAgent, bool isPhone)
    {
        var contact = await SeedAsync($"one{Suffix(isPhone)}", notes: ["First note", "Second note"]);

        var (status, html) = await GetDetailsAsync(contact.Client, contact.ContactId, userAgent);

        status.Should().Be(HttpStatusCode.OK);
        if (isPhone)
            html.Should().Contain("mobile-layout");
        else
            html.Should().NotContain("mobile-layout");

        Regex.IsMatch(html, @"<h1\b[^>]*>\s*" + Regex.Escape(contact.Name) + @"\s*</h1>").Should().BeTrue(
            "the contact's name is the page title");
        html.Should().Contain("contact-info-value");
        html.Should().Contain("fa-tag me-1");
        html.Should().Contain("Allies one");
        html.Should().Contain("modern-card--aside");
        Regex.Matches(html, @"<li class=""notes-slip""").Should().HaveCount(2);
        html.Should().Contain("action=\"/Contacts/AddNote\"");
        html.Should().Contain("contact-detail.css");
        html.Should().NotContain("contact-detail.mobile.css", "the phone has no separate sheet");
        html.Should().NotContain("contact-detail-card", "the phone's own card markup is gone");
        html.Should().NotContain("contacts-page", "the page wrapper is the details page's own");
        Regex.IsMatch(html, @"<script\b[^>]*\btype=""module""[^>]*\bsrc=""/js/contact-notes\.js[^""]*""").Should().BeTrue(
            "the page loads the notes module");
        Regex.IsMatch(html, @"<script\b[^>]*\btype=""module""[^>]*\bsrc=""/js/confirm-submit\.js[^""]*""").Should().BeTrue(
            "the page loads the confirm module");
    }

    [Theory]
    [MemberData(nameof(UserAgents))]
    public async Task Details_SectionsFollowTheTitleActionsDescriptionNotesBackOrder(string userAgent, bool isPhone)
    {
        var contact = await SeedAsync($"ord{Suffix(isPhone)}", notes: ["An ordered note"]);

        var (_, html) = await GetDetailsAsync(contact.Client, contact.ContactId, userAgent);

        var title = html.IndexOf("<h1", StringComparison.Ordinal);
        var actions = html.IndexOf("id=\"actions-title\"", StringComparison.Ordinal);
        var description = html.IndexOf("id=\"description-title\"", StringComparison.Ordinal);
        var notes = html.IndexOf("id=\"notes-title\"", StringComparison.Ordinal);
        var back = html.IndexOf("Back to Contacts", StringComparison.Ordinal);

        new[] { title, actions, description, notes, back }.Should().OnlyContain(i => i > 0);
        title.Should().BeLessThan(actions);
        actions.Should().BeLessThan(description);
        description.Should().BeLessThan(notes);
        notes.Should().BeLessThan(back);
    }

    [Theory]
    [MemberData(nameof(UserAgents))]
    public async Task Details_Notes_AreSlipsWithNamedIconControlsAndAHiddenEditForm(string userAgent, bool isPhone)
    {
        var contact = await SeedAsync($"slip{Suffix(isPhone)}", notes: ["Slip one", "Slip two"]);

        var (_, html) = await GetDetailsAsync(contact.Client, contact.ContactId, userAgent);
        var page = WithoutComments(html);

        Regex.Matches(page, @"aria-label=""Edit note""").Should().HaveCount(2);
        Regex.Matches(page, @"aria-label=""Delete note""").Should().HaveCount(2);
        page.Should().Contain("btn btn-warning btn-sm note-edit-btn");
        page.Should().Contain("btn btn-danger btn-sm");
        page.Should().Contain("btn btn-secondary btn-sm note-cancel-btn");
        page.Should().Contain("btn btn-success btn-sm");
        Regex.Matches(page, @"<form\b[^>]*\bclass=""notes-edit-form[^""]*""[^>]*\bhidden\b[^>]*>").Should().HaveCount(2,
            "every edit form is rendered hidden");
        Regex.Matches(page, @".{0,60}\sstyle="".{0,60}").Select(m => m.Value).Should().BeEmpty("the page holds no inline style");
        page.Should().NotContain("btn-outline", "the note controls are filled");
        page.Should().NotContain("note-item", "the note rows are slips");
        Regex.Matches(page, @"\sdata-note-id=""\d+""").Should().HaveCount(2);
    }

    [Theory]
    [MemberData(nameof(UserAgents))]
    public async Task Details_AddNoteButton_IsThePrimaryActionAndSitsAboveTheSlips(string userAgent, bool isPhone)
    {
        var contact = await SeedAsync($"add{Suffix(isPhone)}", notes: ["Below the form"]);

        var (_, html) = await GetDetailsAsync(contact.Client, contact.ContactId, userAgent);

        var form = html.IndexOf("action=\"/Contacts/AddNote\"", StringComparison.Ordinal);
        var firstSlip = html.IndexOf("class=\"notes-slip\"", StringComparison.Ordinal);
        form.Should().BeGreaterThan(0);
        firstSlip.Should().BeGreaterThan(form, "the add-note form comes first in the Notes card");
        Regex.IsMatch(html, @"<button type=""submit"" class=""btn btn-primary"">\s*<i class=""fas fa-plus me-2"" aria-hidden=""true""></i>Add Note").Should().BeTrue(
            "Add Note is the one commit action of the card");
    }

    [Theory]
    [MemberData(nameof(UserAgents))]
    public async Task Details_DeleteForms_AskTheirQuestionsThroughTheSharedModule(string userAgent, bool isPhone)
    {
        var contact = await SeedAsync($"del{Suffix(isPhone)}", notes: ["One", "Two"]);

        var (_, html) = await GetDetailsAsync(contact.Client, contact.ContactId, userAgent);
        var page = WithoutComments(html);

        Regex.Matches(page, $"data-confirm-message=\"{Regex.Escape(NoteSentence)}\"").Should().HaveCount(2);
        Regex.Matches(page, $"data-confirm-message=\"{Regex.Escape(ContactSentence)}\"").Should().HaveCount(1);
        Regex.Matches(page, @"\son[a-z]+\s*=", RegexOptions.IgnoreCase).Select(m => m.Value).Should().BeEmpty(
            "the page holds no inline event handler");
        page.Should().NotContain("onsubmit");
        Regex.Matches(page, @"<script\b(?![^>]*\bsrc=)[^>]*>").Should().BeEmpty("the page holds no inline script");
    }

    [Theory]
    [MemberData(nameof(UserAgents))]
    public async Task Details_BareContact_ShowsThePlaceholderAndTheVerbatimEmpties(string userAgent, bool isPhone)
    {
        var contact = await SeedAsync($"bare{Suffix(isPhone)}", full: false);

        var (status, html) = await GetDetailsAsync(contact.Client, contact.ContactId, userAgent);

        status.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("contact-detail-portrait--placeholder");
        html.Should().NotContain($"/Contacts/GetContactImage/{contact.ContactId}");
        html.Should().Contain("No description provided.");
        html.Should().Contain("No Notes Yet");
        html.Should().Contain("Be the first to jot something down about this contact.");
        html.Should().NotContain("Ungrouped");
        html.Should().NotContain("fa-tag");
        html.Should().NotContain("notes-slip");
    }

    [Theory]
    [MemberData(nameof(UserAgents))]
    public async Task Details_HiddenContact_ShowsTheHiddenBadgeWithIconAndWord(string userAgent, bool isPhone)
    {
        var contact = await SeedAsync($"hid{Suffix(isPhone)}", revealed: false);

        var (_, html) = await GetDetailsAsync(contact.Client, contact.ContactId, userAgent);

        Regex.IsMatch(html, @"<span class=""badge bg-secondary"">\s*<i class=""fas fa-eye-slash me-1"" aria-hidden=""true""></i>Hidden\s*</span>").Should().BeTrue(
            "the badge carries the icon and the word");
        html.Should().Contain("Reveal Contact");
    }

    [Theory]
    [MemberData(nameof(UserAgents))]
    public async Task Details_RevealedContact_ShowsNoHiddenBadge(string userAgent, bool isPhone)
    {
        var contact = await SeedAsync($"rev{Suffix(isPhone)}");

        var (_, html) = await GetDetailsAsync(contact.Client, contact.ContactId, userAgent);

        html.Should().NotContain("Reveal Contact");
        html.Should().Contain("Hide Contact");
        Regex.IsMatch(html, @"badge bg-secondary[^>]*>\s*<i class=""fas fa-eye-slash").Should().BeFalse();
    }

    [Theory]
    [MemberData(nameof(UserAgents))]
    public async Task Details_Player_SeesNoManagerActionsAndTheSameNoteControls(string userAgent, bool isPhone)
    {
        var owner = await SeedAsync($"plo{Suffix(isPhone)}", notes: ["A player-visible note"]);
        var (player, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, $"contdetplayer{Suffix(isPhone)}", $"contdetplayer{Suffix(isPhone)}@example.com",
            name: "Detail Player", roles: ["Player"]);

        var (status, html) = await GetDetailsAsync(player, owner.ContactId, userAgent);

        status.Should().Be(HttpStatusCode.OK);
        html.Should().NotContain("id=\"actions-title\"");
        html.Should().NotContain("Edit Contact");
        html.Should().NotContain("Delete Contact");
        html.Should().NotContain(ContactSentence);
        html.Should().NotContain("contact-tag-chip");
        html.Should().Contain("A player-visible note");
        html.Should().Contain("aria-label=\"Edit note\"");
        html.Should().Contain("aria-label=\"Delete note\"");
        html.Should().Contain(NoteSentence);
    }

    [Theory]
    [MemberData(nameof(UserAgents))]
    public async Task Details_LongNoteAndUnbrokenString_RenderInsideTheSlip(string userAgent, bool isPhone)
    {
        var unbroken = new string('W', 160) + "Lanternwick";
        var longText = string.Join(" ", Enumerable.Repeat("The ledger records every name after dusk.", 30));
        var contact = await SeedAsync($"lng{Suffix(isPhone)}", notes: [longText, unbroken]);

        var (_, html) = await GetDetailsAsync(contact.Client, contact.ContactId, userAgent);

        html.Should().Contain(unbroken);
        Regex.Matches(html, @"<li class=""notes-slip""").Should().HaveCount(2);
    }

    [Theory]
    [MemberData(nameof(UserAgents))]
    public async Task Details_NoteTimestamp_IsARealInstantThroughTheLocalTimeHelper(string userAgent, bool isPhone)
    {
        var contact = await SeedAsync($"ts{Suffix(isPhone)}", notes: ["Timed note"]);

        var (_, html) = await GetDetailsAsync(contact.Client, contact.ContactId, userAgent);

        Regex.IsMatch(html, @"<time class=""local-time""[^>]*\sdatetime=""[^""]+Z""").Should().BeTrue(
            "the note time is a UTC instant rendered through the local-time helper");
    }
}
