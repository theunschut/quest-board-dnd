using QuestBoard.IntegrationTests.Helpers;
using QuestBoard.Repository.Entities;
using System.Net;

namespace QuestBoard.IntegrationTests.Tests;

// Proves the three mobile surfaces this phase touches -- the category management page, the
// grouped contacts index, and the contact details page -- are the files the server actually
// selects under a real mobile user agent, never devtools/viewport emulation. Each fact pairs a
// mobile request against the same URL requested with no mobile user agent, so a passing
// assertion cannot come from a string both files happen to emit.
public class ContactCategoryMobileRenderTests(WebApplicationFactoryBase factory) : IClassFixture<WebApplicationFactoryBase>
{
    private const string MobileUserAgent =
        "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1";

    // Attaches the mobile user agent header to a request and sends it through the supplied
    // authenticated client, so the client's default authorization header still applies.
    private async Task<(HttpResponseMessage Response, string Html)> GetMobileAsync(HttpClient client, string url)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("User-Agent", MobileUserAgent);
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        return (response, html);
    }

    // TestDataHelper.CreateTestContactAsync has no categoryId parameter -- it predates category
    // grouping. Stamping CategoryId directly through a fresh scope keeps this file's category
    // seeding self-contained without changing a helper other suites also rely on.
    private static async Task AssignContactCategoryAsync(IServiceProvider services, int contactId, int categoryId)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
        var contact = await context.Contacts.SingleAsync(c => c.Id == contactId);
        contact.CategoryId = categoryId;
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task ContactCategoryMobileRender_ManagementPage_MobileUserAgentSelectsMobileFile_DesktopUserAgentDoesNot()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (dmClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "cat_mobile_mgmt_dm", "cat_mobile_mgmt_dm@example.com", roles: ["DungeonMaster"]);
        await TestDataHelper.CreateTestContactCategoryAsync(factory.Services, "Zenith Guild", sortOrder: 0, groupId: 1);

        var (mobileResponse, mobileHtml) = await GetMobileAsync(dmClient, "/ContactCategoryManagement");

        mobileResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        mobileHtml.Should().Contain("category-mgmt-row");

        var desktopResponse = await dmClient.GetAsync("/ContactCategoryManagement", TestContext.Current.CancellationToken);
        var desktopHtml = await desktopResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        desktopResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        desktopHtml.Should().NotContain("category-mgmt-row");
    }

    [Fact]
    public async Task ContactCategoryMobileRender_ContactsIndex_MobileUserAgentSelectsMobileFileWithCategoriesAndUngroupedLast_DesktopUserAgentDoesNot()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (dmClient, dmUser) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "cat_mobile_index_dm", "cat_mobile_index_dm@example.com", roles: ["DungeonMaster"]);

        // Deliberately non-alphabetical sort positions, matching the desktop suite's own
        // ordering-proof convention: "Zenith Guild" sorts first by SortOrder even though "Alley
        // Cats" comes first alphabetically.
        var zenith = await TestDataHelper.CreateTestContactCategoryAsync(factory.Services, "Zenith Guild", sortOrder: 0, groupId: 1);
        var alley = await TestDataHelper.CreateTestContactCategoryAsync(factory.Services, "Alley Cats", sortOrder: 1, groupId: 1);
        var zenithContact = await TestDataHelper.CreateTestContactAsync(
            factory.Services, dmUser.Id, "Zenith Member", groupId: 1, isRevealed: true);
        await AssignContactCategoryAsync(factory.Services, zenithContact.Id, zenith.Id);
        var alleyContact = await TestDataHelper.CreateTestContactAsync(
            factory.Services, dmUser.Id, "Alley Member", groupId: 1, isRevealed: true);
        await AssignContactCategoryAsync(factory.Services, alleyContact.Id, alley.Id);
        // Uncategorised on purpose -- no AssignContactCategoryAsync call for this one.
        await TestDataHelper.CreateTestContactAsync(
            factory.Services, dmUser.Id, "Unaffiliated Wanderer", groupId: 1, isRevealed: true);

        var (mobileResponse, mobileHtml) = await GetMobileAsync(dmClient, "/Contacts/Index");

        mobileResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        mobileHtml.Should().Contain("contact-member-row");
        mobileHtml.Should().Contain("Zenith Guild");
        mobileHtml.Should().Contain("Alley Cats");
        mobileHtml.Should().Contain("Ungrouped");

        var zenithIndex = mobileHtml.IndexOf("Zenith Guild", StringComparison.Ordinal);
        var alleyIndex = mobileHtml.IndexOf("Alley Cats", StringComparison.Ordinal);
        var ungroupedIndex = mobileHtml.IndexOf("Ungrouped", StringComparison.Ordinal);
        ungroupedIndex.Should().BeGreaterThan(zenithIndex);
        ungroupedIndex.Should().BeGreaterThan(alleyIndex);

        var desktopResponse = await dmClient.GetAsync("/Contacts/Index", TestContext.Current.CancellationToken);
        var desktopHtml = await desktopResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        desktopResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        desktopHtml.Should().NotContain("contact-member-row");
    }

    [Fact]
    public async Task ContactCategoryMobileRender_ContactDetails_MobileUserAgentSelectsMobileFileWithCategoryName_DesktopUserAgentDoesNot()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (dmClient, dmUser) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "cat_mobile_details_dm", "cat_mobile_details_dm@example.com", roles: ["DungeonMaster"]);
        var category = await TestDataHelper.CreateTestContactCategoryAsync(factory.Services, "Guild Members", sortOrder: 0, groupId: 1);
        var contact = await TestDataHelper.CreateTestContactAsync(
            factory.Services, dmUser.Id, "Mobile Details Contact", groupId: 1, isRevealed: true);
        await AssignContactCategoryAsync(factory.Services, contact.Id, category.Id);

        var (mobileResponse, mobileHtml) = await GetMobileAsync(dmClient, $"/Contacts/Details/{contact.Id}");

        mobileResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        mobileHtml.Should().Contain("contact-info-value");
        mobileHtml.Should().Contain("Guild Members");

        var desktopResponse = await dmClient.GetAsync($"/Contacts/Details/{contact.Id}", TestContext.Current.CancellationToken);
        var desktopHtml = await desktopResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        desktopResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        desktopHtml.Should().NotContain("contact-info-value");
    }
}
