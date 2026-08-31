using System.Net;
using QuestBoard.IntegrationTests.Helpers;

namespace QuestBoard.IntegrationTests.Controllers;

// Proves the desktop tag surfaces render correctly for the audience they are supposed to render
// for: the filter row and its checkboxes, the per-card and per-details chips, the two-branch
// empty state, the Show Hidden round trip, and escaping of a tag name carrying markup
// characters. Follows this suite's established clear-seed-authenticate rhythm; requests are made
// with the test host's default client (no explicit User-Agent), which the mobile detection
// middleware already treats as desktop elsewhere in this suite.
public class ContactsTagsDesktopMarkupTests(WebApplicationFactoryBase factory) : IClassFixture<WebApplicationFactoryBase>
{
    [Fact]
    public async Task Index_DungeonMaster_RendersFilterRowWithEveryVisibleTag()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (dmClient, dmUser) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "tagmark_filterrow_dm", "tagmark_filterrow_dm@example.com", roles: ["DungeonMaster"]);
        var contactA = await TestDataHelper.CreateTestContactAsync(
            factory.Services, dmUser.Id, "Shopkeeper Contact", groupId: 1, isRevealed: true);
        var contactB = await TestDataHelper.CreateTestContactAsync(
            factory.Services, dmUser.Id, "Quest Giver Contact", groupId: 1, isRevealed: true);
        await TestDataHelper.CreateTestContactTagAsync(factory.Services, "shopkeeper", groupId: 1, contactA.Id);
        await TestDataHelper.CreateTestContactTagAsync(factory.Services, "quest giver", groupId: 1, contactB.Id);

        var response = await dmClient.GetAsync("/Contacts/Index", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        html.Should().Contain("contact-filter-row");
        html.Should().Contain("name=\"tag\"");
        html.Should().Contain("shopkeeper");
        html.Should().Contain("quest giver");
    }

    [Fact]
    public async Task Index_Player_ReceivesNoTagMarkupAtAll()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (dmClient, dmUser) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "tagmark_player_index_dm", "tagmark_player_index_dm@example.com", roles: ["DungeonMaster"]);
        var contact = await TestDataHelper.CreateTestContactAsync(
            factory.Services, dmUser.Id, "Tagged Contact", groupId: 1, isRevealed: true);
        await TestDataHelper.CreateTestContactTagAsync(factory.Services, "shopkeeper", groupId: 1, contact.Id);

        var (playerClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "tagmark_player_index", "tagmark_player_index@example.com", roles: ["Player"]);

        var response = await playerClient.GetAsync("/Contacts/Index", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        html.Should().NotContain("contact-filter-row");
        html.Should().NotContain("contact-tag-chip");
        html.Should().NotContain("shopkeeper");
    }

    [Fact]
    public async Task Details_Player_ReceivesNoTagMarkupAtAll()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (dmClient, dmUser) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "tagmark_player_details_dm", "tagmark_player_details_dm@example.com", roles: ["DungeonMaster"]);
        var contact = await TestDataHelper.CreateTestContactAsync(
            factory.Services, dmUser.Id, "Tagged Contact", groupId: 1, isRevealed: true);
        await TestDataHelper.CreateTestContactTagAsync(factory.Services, "shopkeeper", groupId: 1, contact.Id);

        var (playerClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "tagmark_player_details", "tagmark_player_details@example.com", roles: ["Player"]);

        var response = await playerClient.GetAsync($"/Contacts/Details/{contact.Id}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        html.Should().NotContain("contact-tag-chip");
        html.Should().NotContain("shopkeeper");
    }

    [Fact]
    public async Task Details_DungeonMaster_RendersChipsAndBothTagNames()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (dmClient, dmUser) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "tagmark_dm_details_dm", "tagmark_dm_details_dm@example.com", roles: ["DungeonMaster"]);
        var contact = await TestDataHelper.CreateTestContactAsync(
            factory.Services, dmUser.Id, "Doubly Tagged Contact", groupId: 1, isRevealed: true);
        await TestDataHelper.CreateTestContactTagAsync(factory.Services, "shopkeeper", groupId: 1, contact.Id);
        await TestDataHelper.CreateTestContactTagAsync(factory.Services, "quest giver", groupId: 1, contact.Id);

        var response = await dmClient.GetAsync($"/Contacts/Details/{contact.Id}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        html.Should().Contain("contact-tag-chip");
        html.Should().Contain("shopkeeper");
        html.Should().Contain("quest giver");
    }

    [Fact]
    public async Task Index_TagOnlyOnUnrevealedContact_AbsentUntilShowHiddenIsOn()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (_, creator) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "tagmark_vocab_creator", "tagmark_vocab_creator@example.com", roles: ["DungeonMaster"]);
        var visibleContact = await TestDataHelper.CreateTestContactAsync(
            factory.Services, creator.Id, "Town Guard Contact", groupId: 1, isRevealed: true);
        await TestDataHelper.CreateTestContactTagAsync(factory.Services, "town-guard", groupId: 1, visibleContact.Id);

        var hiddenContact = await TestDataHelper.CreateTestContactAsync(
            factory.Services, creator.Id, "Secret Contact", groupId: 1, isRevealed: false);
        await TestDataHelper.CreateTestContactTagAsync(factory.Services, "secret-tag", groupId: 1, hiddenContact.Id);

        // A different DM-tier viewer than the creator, so the creator's own-contact visibility
        // exemption cannot mask the Show Hidden toggle's effect on the filter vocabulary.
        var (otherDmClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "tagmark_vocab_otherdm", "tagmark_vocab_otherdm@example.com", roles: ["DungeonMaster"]);

        var beforeResponse = await otherDmClient.GetAsync("/Contacts/Index", TestContext.Current.CancellationToken);
        beforeResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var beforeHtml = await beforeResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        beforeHtml.Should().Contain("town-guard");
        beforeHtml.Should().NotContain("secret-tag");

        var toggleResponse = await otherDmClient.PostAsync(
            "/Contacts/ToggleShowHidden", new FormUrlEncodedContent([]), TestContext.Current.CancellationToken);
        toggleResponse.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.OK);

        var afterResponse = await otherDmClient.GetAsync("/Contacts/Index", TestContext.Current.CancellationToken);
        afterResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var afterHtml = await afterResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        afterHtml.Should().Contain("secret-tag");
    }

    [Fact]
    public async Task Index_BoardWithNoTags_RendersDisabledFilterHint()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (dmClient, dmUser) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "tagmark_notags_dm", "tagmark_notags_dm@example.com", roles: ["DungeonMaster"]);
        await TestDataHelper.CreateTestContactAsync(
            factory.Services, dmUser.Id, "Untagged Contact", groupId: 1, isRevealed: true);

        var response = await dmClient.GetAsync("/Contacts/Index", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        html.Should().Contain("contact-filter-empty");
        html.Should().Contain("No tags yet. Add tags when creating or editing a contact to start filtering.");
        html.Should().NotContain("type=\"checkbox\" name=\"tag\"");
    }

    [Fact]
    public async Task Index_FilterMatchesNothing_RendersNoMatchBranchNotEmptyListBranch()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (_, creator) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "tagmark_nomatch_creator", "tagmark_nomatch_creator@example.com", roles: ["DungeonMaster"]);
        var visibleContact = await TestDataHelper.CreateTestContactAsync(
            factory.Services, creator.Id, "Visible Shopkeeper", groupId: 1, isRevealed: true);
        await TestDataHelper.CreateTestContactTagAsync(factory.Services, "shopkeeper", groupId: 1, visibleContact.Id);

        // The filtered-on tag belongs only to a contact hidden from the requesting viewer, so no
        // currently visible contact can ever match it -- a real, valid tag id that still narrows
        // the result to zero, rather than an id that does not exist at all.
        var hiddenContact = await TestDataHelper.CreateTestContactAsync(
            factory.Services, creator.Id, "Hidden Quest Giver", groupId: 1, isRevealed: false);
        var questGiverTag = await TestDataHelper.CreateTestContactTagAsync(
            factory.Services, "quest giver", groupId: 1, hiddenContact.Id);

        var (viewerClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "tagmark_nomatch_viewer", "tagmark_nomatch_viewer@example.com", roles: ["DungeonMaster"]);

        var response = await viewerClient.GetAsync(
            $"/Contacts/Index?tag={questGiverTag.Id}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        html.Should().Contain("No contacts match your filters");
        html.Should().Contain("Try selecting different tags.");
        html.Should().NotContain("No Contacts Yet");
    }

    [Fact]
    public async Task Index_BoardWithNoContactsAtAll_RendersGenuinelyEmptyBranch()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (dmClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "tagmark_reallyempty_dm", "tagmark_reallyempty_dm@example.com", roles: ["DungeonMaster"]);

        var response = await dmClient.GetAsync("/Contacts/Index", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        html.Should().Contain("No Contacts Yet");
        html.Should().NotContain("No contacts match your filters");
    }

    [Fact]
    public async Task Index_ActiveFilter_ShowHiddenFormCarriesSelectedTagIds()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (dmClient, dmUser) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "tagmark_carrytoggle_dm", "tagmark_carrytoggle_dm@example.com", roles: ["DungeonMaster"]);
        var contact = await TestDataHelper.CreateTestContactAsync(
            factory.Services, dmUser.Id, "Filterable Contact", groupId: 1, isRevealed: true);
        var tag = await TestDataHelper.CreateTestContactTagAsync(factory.Services, "shopkeeper", groupId: 1, contact.Id);

        var response = await dmClient.GetAsync(
            $"/Contacts/Index?tag={tag.Id}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        html.Should().Contain($"<input type=\"hidden\" name=\"tag\" value=\"{tag.Id}\" />");
    }

    [Fact]
    public async Task Index_TagNameWithMarkupCharacters_IsEscaped()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (dmClient, dmUser) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "tagmark_escape_dm", "tagmark_escape_dm@example.com", roles: ["DungeonMaster"]);
        var contact = await TestDataHelper.CreateTestContactAsync(
            factory.Services, dmUser.Id, "Escaping Contact", groupId: 1, isRevealed: true);
        await TestDataHelper.CreateTestContactTagAsync(factory.Services, "<b>\"", groupId: 1, contact.Id);

        var response = await dmClient.GetAsync("/Contacts/Index", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        html.Should().NotContain("<b>\"");
        html.Should().Contain("&lt;b&gt;&quot;");
    }
}
