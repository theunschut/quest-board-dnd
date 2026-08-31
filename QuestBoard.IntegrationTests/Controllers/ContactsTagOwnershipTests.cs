using System.Net;
using QuestBoard.IntegrationTests.Helpers;

namespace QuestBoard.IntegrationTests.Controllers;

// Proves the ownership x Show Hidden toggle x role x surface matrix for contact tags: an owning
// DM-tier viewer always sees their own tag chips and filter options, a non-owning DM-tier viewer
// sees neither until the shared Show Hidden toggle is on, a Player sees no tag markup at all
// regardless of ownership, and the Create/Edit authoring suggestion list stays deliberately
// board-wide even while the Index filter row is narrowed. Follows this suite's established
// clear-seed-authenticate rhythm; requests are made with the test host's default client (no
// explicit User-Agent), which the mobile detection middleware already treats as desktop
// elsewhere in this suite.
public class ContactsTagOwnershipTests(WebApplicationFactoryBase factory) : IClassFixture<WebApplicationFactoryBase>
{
    [Fact]
    public async Task Index_OwningDungeonMaster_StillSeesOwnTagChipsAndFilterOption()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (ownerClient, owner) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "tagown_owner_index", "tagown_owner_index@example.com", roles: ["DungeonMaster"]);
        var contact = await TestDataHelper.CreateTestContactAsync(
            factory.Services, owner.Id, "Owner's Contact", isRevealed: true, groupId: 1);
        await TestDataHelper.CreateTestContactTagAsync(factory.Services, "owner-only-tag", groupId: 1, contact.Id);

        var response = await ownerClient.GetAsync("/Contacts/Index", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        html.Should().Contain("contact-tag-chip");
        html.Should().Contain("owner-only-tag");
        html.Should().Contain("contact-filter-row");
    }

    [Fact]
    public async Task Index_NonOwningDungeonMaster_SeesNeitherChipsNorFilterOptionWhileShowHiddenIsOff()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var owner = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "tagown_owner_nonowning", "tagown_owner_nonowning@example.com", "Test123!", "Contact Owner");
        var contact = await TestDataHelper.CreateTestContactAsync(
            factory.Services, owner.Id, "Owner's Contact", isRevealed: true, groupId: 1);
        await TestDataHelper.CreateTestContactTagAsync(factory.Services, "owner-only-tag", groupId: 1, contact.Id);

        var (nonOwnerClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "tagown_nonowner_off", "tagown_nonowner_off@example.com", roles: ["DungeonMaster"]);

        var response = await nonOwnerClient.GetAsync("/Contacts/Index", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        html.Should().NotContain("contact-tag-chip");
        html.Should().NotContain("owner-only-tag");
        html.Should().Contain("contact-filter-empty");
    }

    [Fact]
    public async Task Index_NonOwningDungeonMaster_SeesChipsAndFilterOptionAfterTogglingShowHidden()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var owner = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "tagown_owner_toggle", "tagown_owner_toggle@example.com", "Test123!", "Contact Owner");
        var contact = await TestDataHelper.CreateTestContactAsync(
            factory.Services, owner.Id, "Owner's Contact", isRevealed: true, groupId: 1);
        await TestDataHelper.CreateTestContactTagAsync(factory.Services, "owner-only-tag", groupId: 1, contact.Id);

        var (nonOwnerClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "tagown_nonowner_toggle", "tagown_nonowner_toggle@example.com", roles: ["DungeonMaster"]);

        var beforeResponse = await nonOwnerClient.GetAsync("/Contacts/Index", TestContext.Current.CancellationToken);
        beforeResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var beforeHtml = await beforeResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        beforeHtml.Should().NotContain("contact-tag-chip");
        beforeHtml.Should().NotContain("owner-only-tag");

        var toggleOnResponse = await nonOwnerClient.PostAsync(
            "/Contacts/ToggleShowHidden", new FormUrlEncodedContent([]), TestContext.Current.CancellationToken);
        toggleOnResponse.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.OK);

        var afterOnResponse = await nonOwnerClient.GetAsync("/Contacts/Index", TestContext.Current.CancellationToken);
        afterOnResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var afterOnHtml = await afterOnResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        afterOnHtml.Should().Contain("contact-tag-chip");
        afterOnHtml.Should().Contain("owner-only-tag");
        afterOnHtml.Should().Contain("contact-filter-row");

        var toggleOffResponse = await nonOwnerClient.PostAsync(
            "/Contacts/ToggleShowHidden", new FormUrlEncodedContent([]), TestContext.Current.CancellationToken);
        toggleOffResponse.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.OK);

        var afterOffResponse = await nonOwnerClient.GetAsync("/Contacts/Index", TestContext.Current.CancellationToken);
        afterOffResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var afterOffHtml = await afterOffResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        afterOffHtml.Should().NotContain("contact-tag-chip");
        afterOffHtml.Should().NotContain("owner-only-tag");
    }

    [Fact]
    public async Task Details_NonOwningDungeonMaster_SeesNoTagChipsUntilShowHiddenIsOn()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var owner = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "tagown_owner_details", "tagown_owner_details@example.com", "Test123!", "Contact Owner");
        var contact = await TestDataHelper.CreateTestContactAsync(
            factory.Services, owner.Id, "Owner's Contact", isRevealed: true, groupId: 1);
        await TestDataHelper.CreateTestContactTagAsync(factory.Services, "owner-only-tag", groupId: 1, contact.Id);

        var (nonOwnerClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "tagown_nonowner_details", "tagown_nonowner_details@example.com", roles: ["DungeonMaster"]);

        var beforeResponse = await nonOwnerClient.GetAsync($"/Contacts/Details/{contact.Id}", TestContext.Current.CancellationToken);
        beforeResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var beforeHtml = await beforeResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        beforeHtml.Should().NotContain("contact-tag-chip");

        var toggleResponse = await nonOwnerClient.PostAsync(
            "/Contacts/ToggleShowHidden", new FormUrlEncodedContent([]), TestContext.Current.CancellationToken);
        toggleResponse.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.OK);

        var afterResponse = await nonOwnerClient.GetAsync($"/Contacts/Details/{contact.Id}", TestContext.Current.CancellationToken);
        afterResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var afterHtml = await afterResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        afterHtml.Should().Contain("contact-tag-chip");
        afterHtml.Should().Contain("owner-only-tag");
    }

    [Fact]
    public async Task Details_OwningDungeonMaster_StillSeesOwnTagChips()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (ownerClient, owner) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "tagown_owner_details_self", "tagown_owner_details_self@example.com", roles: ["DungeonMaster"]);
        var contact = await TestDataHelper.CreateTestContactAsync(
            factory.Services, owner.Id, "Owner's Own Contact", isRevealed: true, groupId: 1);
        await TestDataHelper.CreateTestContactTagAsync(factory.Services, "owner-only-tag", groupId: 1, contact.Id);

        var response = await ownerClient.GetAsync($"/Contacts/Details/{contact.Id}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        html.Should().Contain("contact-tag-chip");
        html.Should().Contain("owner-only-tag");
    }

    [Fact]
    public async Task Index_Player_StillSeesNoTagMarkupRegardlessOfOwnership()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var owner = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "tagown_owner_player", "tagown_owner_player@example.com", "Test123!", "Contact Owner");
        var contact = await TestDataHelper.CreateTestContactAsync(
            factory.Services, owner.Id, "Owner's Contact", isRevealed: true, groupId: 1);
        await TestDataHelper.CreateTestContactTagAsync(factory.Services, "owner-only-tag", groupId: 1, contact.Id);

        var (playerClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "tagown_player", "tagown_player@example.com", roles: ["Player"]);

        var response = await playerClient.GetAsync("/Contacts/Index", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        html.Should().NotContain("contact-tag-chip");
        html.Should().NotContain("contact-filter-row");
        html.Should().NotContain("contact-filter-empty");
        html.Should().NotContain("owner-only-tag");
    }

    // The authoring autocomplete is intentionally board-wide, so a DM can reuse a colleague's
    // existing tag name when writing a new contact, while the filter row is intentionally
    // viewer-scoped, so a filter option never re-discloses a tag name a viewer's chips already
    // withhold. This fact exists to keep the two from being "unified" by mistake later.
    [Fact]
    public async Task CreateAndEditForms_NonOwningDungeonMaster_StillSuggestEveryBoardTagName()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var firstOwner = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "tagown_asym_first", "tagown_asym_first@example.com", "Test123!", "First Owner");
        var otherContact = await TestDataHelper.CreateTestContactAsync(
            factory.Services, firstOwner.Id, "Other DM's Contact", isRevealed: true, groupId: 1);
        await TestDataHelper.CreateTestContactTagAsync(factory.Services, "shared-vocab-tag", groupId: 1, otherContact.Id);

        var (requesterClient, requester) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "tagown_asym_requester", "tagown_asym_requester@example.com", roles: ["DungeonMaster"]);
        var ownContact = await TestDataHelper.CreateTestContactAsync(
            factory.Services, requester.Id, "Requester's Own Contact", isRevealed: true, groupId: 1);

        var createResponse = await requesterClient.GetAsync("/Contacts/Create", TestContext.Current.CancellationToken);
        createResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var createHtml = await createResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        createHtml.Should().Contain("shared-vocab-tag");

        var editResponse = await requesterClient.GetAsync($"/Contacts/Edit/{ownContact.Id}", TestContext.Current.CancellationToken);
        editResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var editHtml = await editResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        editHtml.Should().Contain("shared-vocab-tag");

        var indexResponse = await requesterClient.GetAsync("/Contacts/Index", TestContext.Current.CancellationToken);
        indexResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var indexHtml = await indexResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        indexHtml.Should().NotContain("shared-vocab-tag");
    }
}
