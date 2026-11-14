using System.Net;
using System.Text.RegularExpressions;
using QuestBoard.IntegrationTests.Helpers;

namespace QuestBoard.IntegrationTests.Tests;

/// <summary>
/// Pins the answers given for the pages of the characters, contacts and players areas that keep a separate phone
/// view: the headings the phone views gained, the markers they show and the dates they word. The layout is chosen
/// from the request's User-Agent header, so every fact states which one it sends.
/// </summary>
public class CharactersTwinAnswersTests(WebApplicationFactoryBase factory)
    : IClassFixture<WebApplicationFactoryBase>, IAsyncLifetime
{
    private const string PhoneUserAgent =
        "Mozilla/5.0 (Linux; Android 13; Pixel 7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Mobile Safari/537.36";

    public async ValueTask InitializeAsync() => await TestDataHelper.ClearDatabaseAsync(factory.Services);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static async Task<string> GetPhoneAsync(HttpClient client, string url)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("User-Agent", PhoneUserAgent);
        request.Headers.Authorization = client.DefaultRequestHeaders.Authorization;
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task PhoneManage_HasThePageHeading()
    {
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "twinanswers_manage_dm", "twinanswers_manage_dm@example.com", roles: ["DungeonMaster"]);

        var html = await GetPhoneAsync(client, "/ContactCategoryManagement");

        html.Should().Contain("mobile-layout");
        Regex.Matches(html, @"<h1\b").Should().HaveCount(1, "the phone page has one page title");
        Regex.IsMatch(html, @"<h1\b[^>]*>\s*Manage Categories\s*</h1>").Should().BeTrue(
            "the phone page carries the same title words as the desktop page");
    }

    [Fact]
    public async Task PhoneCharacters_HasThePageHeading()
    {
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "twinanswers_characters", "twinanswers_characters@example.com");

        var html = await GetPhoneAsync(client, "/Characters");

        html.Should().Contain("mobile-layout");
        Regex.Matches(html, @"<h1\b").Should().HaveCount(1, "the phone page has one page title");
        Regex.IsMatch(html, @"<h1\b[^>]*>\s*Characters\s*</h1>").Should().BeTrue(
            "the phone page carries the same title words as the desktop page");
    }

    [Fact]
    public async Task PhoneContacts_HasThePageHeadingWhenCategoriesExist()
    {
        var (client, user) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "twinanswers_contacts_dm", "twinanswers_contacts_dm@example.com", roles: ["DungeonMaster"]);
        var category = await TestDataHelper.CreateTestContactCategoryAsync(factory.Services, "Allies", sortOrder: 0, groupId: 1);
        var contact = await TestDataHelper.CreateTestContactAsync(factory.Services, user.Id, "Categorised Contact", isRevealed: true);
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
            var stored = await context.Contacts.SingleAsync(c => c.Id == contact.Id, TestContext.Current.CancellationToken);
            stored.CategoryId = category.Id;
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var html = await GetPhoneAsync(client, "/Contacts");

        html.Should().Contain("mobile-layout");
        Regex.Matches(html, @"<h1\b").Should().HaveCount(1, "the phone page has one page title");
        Regex.IsMatch(html, @"<h1\b[^>]*>\s*Contacts\s*</h1>").Should().BeTrue(
            "the phone page carries the same title words as the desktop page");
    }

    [Fact]
    public async Task ContactsFilter_KeepsItsDeliberateTwinDifference()
    {
        var (client, user) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "twinanswers_filter_dm", "twinanswers_filter_dm@example.com", roles: ["DungeonMaster"]);
        var contact = await TestDataHelper.CreateTestContactAsync(factory.Services, user.Id, "Tagged Contact", isRevealed: true);
        var tag = await TestDataHelper.CreateTestContactTagAsync(factory.Services, "tavern", 1, contact.Id);

        var phone = await GetPhoneAsync(client, $"/Contacts?tag={tag.Id}");
        Regex.IsMatch(phone, @"<button\b[^>]*class=""btn btn-secondary contact-filter-toggle""[^>]*>\s*<i\b[^>]*></i>Filter Tags\s*<span class=""badge bg-success ms-1"">Active</span>")
            .Should().BeTrue("the phone keeps its Filter Tags button, recoloured, with its Active badge");
        phone.Should().Contain("id=\"contactFilterOffcanvas\"");
        phone.Should().NotContain("contact-filter-row");

        var desktop = await client.GetStringAsync($"/Contacts?tag={tag.Id}", TestContext.Current.CancellationToken);
        desktop.Should().Contain("contact-filter-row");
        desktop.Should().NotContain("Filter Tags");
        desktop.Should().NotContain("contactFilterOffcanvas");
    }

    [Theory]
    [InlineData("create")]
    [InlineData("edit")]
    public async Task PhoneCharacterForms_MarkClassesRequired(string form)
    {
        var (client, user) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, $"twinanswers_classes_{form}", $"twinanswers_classes_{form}@example.com");

        var url = "/Characters/Create";
        if (form == "edit")
        {
            var character = await TestDataHelper.CreateTestCharacterAsync(factory.Services, user.Id, "Required Marker Hero");
            url = $"/Characters/Edit/{character.Id}";
        }

        var html = await GetPhoneAsync(client, url);

        html.Should().Contain("mobile-layout");
        Regex.IsMatch(
            html,
            @"<label[^>]*>\s*Classes <span class=""text-danger"" aria-hidden=""true"">\*</span><span class=""visually-hidden""> \(required\)</span></label>")
            .Should().BeTrue("the phone form marks Classes as required, as the desktop form does");
    }
}
