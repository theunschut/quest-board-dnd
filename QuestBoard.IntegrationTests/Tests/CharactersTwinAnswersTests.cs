using System.Globalization;
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Players_HasThePageHeading(bool phone)
    {
        var tag = phone ? "phone" : "desktop";
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, $"twinanswers_players_{tag}", $"twinanswers_players_{tag}@example.com");

        var html = phone
            ? await GetPhoneAsync(client, "/Players")
            : await client.GetStringAsync("/Players", TestContext.Current.CancellationToken);

        if (phone)
            html.Should().Contain("mobile-layout");
        Regex.Matches(html, @"<h1\b").Should().HaveCount(1, "the page has one page title");
        Regex.IsMatch(html, @"<h1\b[^>]*>\s*Players\s*</h1>").Should().BeTrue(
            "both layouts carry the page heading the operator approved");
        Regex.Matches(html, @"<h2\b[^>]*>\s*<i\b[^>]*></i>(?:Dungeon Masters|Registered Players)\s*</h2>").Should().HaveCount(2,
            "the two list titles stay section titles under the page heading");
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

    // A Dungeon Master with one finalized quest and one quest that has no date, seen by that Dungeon Master.
    private async Task<(HttpClient Client, int DmId)> SeedDmProfileWithDatesAsync(string tag, DateTime finalized)
    {
        var (client, dm) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, $"twinanswers_dmdates_{tag}", $"twinanswers_dmdates_{tag}@example.com", roles: ["DungeonMaster"]);
        await TestDataHelper.CreateTestQuestAsync(
            factory.Services, dm.Id, "The Lantern Gate Vigil", isFinalized: true, finalizedDate: finalized);
        await TestDataHelper.CreateTestQuestAsync(factory.Services, dm.Id, "The Unscheduled Embermarch");
        return (client, dm.Id);
    }

    // The visible text of each wall-clock time element on the page.
    private static List<string> WallClockTexts(string html) =>
        Regex.Matches(html, @"<time class=""wall-clock""[^>]*>(?<text>[^<]*)</time>")
            .Select(m => m.Groups["text"].Value).ToList();

    [Fact]
    public async Task DesktopDmProfile_ShowsDateTbdForAnUndatedQuest()
    {
        var finalized = new DateTime(2026, 11, 5, 19, 0, 0);
        var (client, id) = await SeedDmProfileWithDatesAsync("desktop", finalized);

        var html = await client.GetStringAsync($"/DungeonMaster/Profile/{id}", TestContext.Current.CancellationToken);

        html.Should().NotContain("mobile-layout");
        var rows = Regex.Matches(html, @"<tr>\s*<td>(?<body>[\s\S]*?)</tr>").Select(m => m.Groups["body"].Value).ToList();
        rows.Should().HaveCount(2);
        var undated = rows.Single(r => r.Contains("The Unscheduled Embermarch", StringComparison.Ordinal));
        undated.Should().Contain("Date TBD", "the desktop table no longer leaves the cell empty for an undated quest");
        var dated = rows.Single(r => r.Contains("The Lantern Gate Vigil", StringComparison.Ordinal));
        dated.Should().NotContain("Date TBD");
        WallClockTexts(html).Should().Equal(
            [finalized.ToString("MMM d, yyyy", CultureInfo.InvariantCulture)],
            "the visible date text is the helper's date format, with no leading zero on the day");
    }

    [Fact]
    public async Task PhoneDmProfile_RendersTheSameDates()
    {
        var finalized = new DateTime(2026, 9, 30, 19, 0, 0);
        var (client, id) = await SeedDmProfileWithDatesAsync("phone", finalized);

        var html = await GetPhoneAsync(client, $"/DungeonMaster/Profile/{id}");

        html.Should().Contain("mobile-layout");
        var slips = Regex.Split(html, @"(?=<div class=""dm-quest-row[ ""])").Skip(1).ToList();
        slips.Should().HaveCount(2);
        slips.Single(s => s.Contains("The Unscheduled Embermarch", StringComparison.Ordinal))
            .Should().Contain("Date TBD");
        WallClockTexts(html).Should().Equal(
            [finalized.ToString("MMM d, yyyy", CultureInfo.InvariantCulture)],
            "the phone words the date exactly as the desktop does, so Sept becomes Sep and the day has no leading zero");
        html.Should().NotContain("Sept 30");
    }
}
