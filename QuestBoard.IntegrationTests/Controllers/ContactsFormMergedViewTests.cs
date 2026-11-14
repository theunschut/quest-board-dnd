using System.Net;
using System.Text.RegularExpressions;
using QuestBoard.IntegrationTests.Helpers;

namespace QuestBoard.IntegrationTests.Controllers;

// Create and Edit are each one view that serves both the desktop and the phone layout. The layout is
// chosen by the request's User-Agent, so every fact runs once per User-Agent against the same view and
// shows that what differs between them is the layout the shell wraps around the form, not the form.
public class ContactsFormMergedViewTests(WebApplicationFactoryBase factory) : IClassFixture<WebApplicationFactoryBase>
{
    private const string MobileUserAgent =
        "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1";

    private const string DesktopUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

    private static readonly byte[] TinyPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==");

    private static async Task<string> GetAsync(HttpClient client, string url, bool mobile)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("User-Agent", mobile ? MobileUserAgent : DesktopUserAgent);
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK, $"{url} should render for a Dungeon Master");
        return await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
    }

    private static string Tag(bool mobile) => mobile ? "phone" : "desktop";

    // What both forms share: one page title, the Name field with its required marker and a hidden spoken
    // equivalent, the shared form rules wrapper, one sheet, and the action row with the exit first.
    private static void AssertFormContract(string html, bool mobile, string commitLabel)
    {
        if (mobile)
            html.Should().Contain("mobile-layout");
        else
            html.Should().NotContain("mobile-layout");

        Regex.Matches(html, @"<h1\b").Should().HaveCount(1, "a page has exactly one title");
        html.Should().Contain("form-page");
        html.Should().NotContain("contacts-page", "the global contacts sheet must not restyle the form");
        html.Should().NotContain("contact-form-card", "the phone card wrapper is gone with the phone view");

        html.Should().MatchRegex(
            @"<label[^>]*for=""Name""[^>]*>\s*Name\s*<span[^>]*text-danger[^>]*aria-hidden=""true""[^>]*>\*</span>\s*<span class=""visually-hidden""> \(required\)</span>",
            "the required marker has a visually hidden spoken equivalent");

        html.Should().MatchRegex(@"<link[^>]*href=""/css/contact-form\.css");
        html.Should().MatchRegex(@"<link[^>]*href=""/css/tagify-vendor\.css");
        html.Should().MatchRegex(@"<link[^>]*href=""/css/image-crop\.css");
        html.Should().NotContain("contact-form.mobile.css");

        // The exit comes first in the markup and the one commit follows as btn-primary.
        html.Should().MatchRegex(
            @"<div class=""form-actions"">\s*<a [^>]*class=""btn btn-secondary""[^>]*>\s*<i[^>]*></i>Cancel\s*</a>\s*<button type=""submit"" class=""btn btn-primary"">\s*<i[^>]*></i>" + Regex.Escape(commitLabel) + @"\s*</button>");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Create_RendersTheSameFormUnderBothUserAgents(bool mobile)
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        await TestDataHelper.CreateTestContactCategoryAsync(factory.Services, "Allies", sortOrder: 0, groupId: 1);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, $"contact_merge_create_{Tag(mobile)}", $"contact_merge_create_{Tag(mobile)}@example.com", roles: ["DungeonMaster"]);

        var html = await GetAsync(client, "/Contacts/Create", mobile);

        AssertFormContract(html, mobile, "Create Contact");
        html.Should().Contain("Create New Contact");

        // The picture block shares the top edge of the field stack: the file input sits in the first column
        // of the row that holds the Name field, and the field order is unchanged.
        var order = new[] { "ContactImageFile", "id=\"Name\"", "id=\"TownCity\"", "id=\"SubLocation\"", "id=\"TagsInput\"", "id=\"CategoryId\"", "id=\"Description\"" };
        var last = -1;
        foreach (var marker in order)
        {
            var index = html.IndexOf(marker, StringComparison.Ordinal);
            index.Should().BeGreaterThan(last, $"{marker} keeps its place in the field order");
            last = index;
        }

        html.Should().NotContain("form-picture-thumb", "a new contact has no current picture");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Edit_RendersTheSameFormUnderBothUserAgents(bool mobile)
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (client, user) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, $"contact_merge_edit_{Tag(mobile)}", $"contact_merge_edit_{Tag(mobile)}@example.com", roles: ["DungeonMaster"]);
        var contact = await TestDataHelper.CreateTestContactAsync(
            factory.Services, user.Id, "Aurelia Fenwhistle", groupId: 1, imageData: TinyPng);

        var html = await GetAsync(client, $"/Contacts/Edit/{contact.Id}", mobile);

        AssertFormContract(html, mobile, "Save Changes");
        html.Should().Contain("Edit Aurelia Fenwhistle");
        html.Should().Contain($"name=\"Id\"");

        // The current picture is a class under the file input and its hint, never an inline size.
        var input = html.IndexOf("id=\"contactImageInput\"", StringComparison.Ordinal);
        var thumb = html.IndexOf("form-picture-thumb", StringComparison.Ordinal);
        input.Should().BeGreaterThan(-1);
        thumb.Should().BeGreaterThan(input, "the thumbnail sits under the file input");
        html.Should().Contain("form-picture-current");
        html.Should().MatchRegex(@"<button type=""button"" class=""btn btn-sm btn-warning[^""]*"" id=""recropPhotoBtn""");
        html.Should().Contain("Re-crop Photo");
        html.Should().NotContain("max-width: 200px");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Edit_WithoutAPicture_OffersNoThumbnailOrRecrop(bool mobile)
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (client, user) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, $"contact_merge_nopic_{Tag(mobile)}", $"contact_merge_nopic_{Tag(mobile)}@example.com", roles: ["DungeonMaster"]);
        var contact = await TestDataHelper.CreateTestContactAsync(factory.Services, user.Id, "Pictureless", groupId: 1);

        var html = await GetAsync(client, $"/Contacts/Edit/{contact.Id}", mobile);

        html.Should().NotContain("form-picture-thumb");
        html.Should().NotContain("recropPhotoBtn");
    }

    [Theory]
    [InlineData("Create", false)]
    [InlineData("Create", true)]
    [InlineData("Edit", false)]
    [InlineData("Edit", true)]
    public async Task BoardWithNoCategories_ShowsTheDisabledSelectAndTheHintUnderBothUserAgents(string action, bool mobile)
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var name = $"contact_merge_nocat_{action}_{Tag(mobile)}".ToLowerInvariant();
        var (client, user) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, name, $"{name}@example.com", roles: ["DungeonMaster"]);
        var url = "/Contacts/Create";
        if (action == "Edit")
        {
            var contact = await TestDataHelper.CreateTestContactAsync(factory.Services, user.Id, "Uncategorised", groupId: 1);
            url = $"/Contacts/Edit/{contact.Id}";
        }

        var html = await GetAsync(client, url, mobile);

        html.Should().MatchRegex(@"<select class=""form-select"" disabled>\s*<option>— None —</option>");
        html.Should().MatchRegex(
            @"<small class=""form-text"">\s*No categories yet\.\s*<a [^>]*href=""[^""]*ContactCategoryManagement[^""]*"">Manage Categories</a>\s*to create one\.\s*</small>");
        html.Should().Contain("modern-card");
    }
}
