using System.Net;
using System.Net.Http.Headers;
using QuestBoard.IntegrationTests.Helpers;

namespace QuestBoard.IntegrationTests.Controllers;

// Proves the tag entry field and its pinned library reach a DM's contact create/edit forms on
// both a desktop and a real mobile User-Agent, that the edit form pre-fills a contact's existing
// tags as a comma-separated value, that a player reaches neither form, and that the suggestion
// list emitted for a DM never carries another board's tag names. The mobile split is driven
// entirely by the request's User-Agent header, not by viewport size, so these tests build the
// request by hand and set the header without validation, matching the existing mobile-view test
// convention -- viewport emulation would never exercise the middleware that actually picks the
// mobile view.
public class ContactsTagsFormMarkupTests(WebApplicationFactoryBase factory) : IClassFixture<WebApplicationFactoryBase>
{
    private const string MobileUserAgent =
        "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1";

    private const string DesktopUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

    private static async Task<(HttpStatusCode StatusCode, string Html)> GetAsync(
        HttpClient client, string url, string userAgent, AuthenticationHeaderValue? authorization)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("User-Agent", userAgent);
        if (authorization != null)
        {
            request.Headers.Authorization = authorization;
        }
        // Use the caller's own client (created with AllowAutoRedirect = false) rather than a
        // fresh factory.CreateClient(), so a DM-tier refusal surfaces as the real 302 to
        // /Account/AccessDenied instead of being silently followed to a 200 OK.
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        return (response.StatusCode, html);
    }

    [Fact]
    public async Task CreateForm_DesktopUserAgent_RendersTagInputAndPinnedLibrary()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (dmClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "tagform_desktop_create", "tagform_desktop_create@example.com", roles: ["DungeonMaster"]);

        var (statusCode, html) = await GetAsync(
            dmClient, "/Contacts/Create", DesktopUserAgent, dmClient.DefaultRequestHeaders.Authorization);

        statusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("id=\"TagsInput\"");
        html.Should().Contain("@yaireo/tagify@4.38.0");
        html.Should().Contain("integrity=\"sha384-");
        html.Should().Contain("initContactTags(");
    }

    [Fact]
    public async Task CreateForm_MobileUserAgent_RendersTagInputAndPinnedLibrary()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (dmClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "tagform_mobile_create", "tagform_mobile_create@example.com", roles: ["DungeonMaster"]);

        var (statusCode, html) = await GetAsync(
            dmClient, "/Contacts/Create", MobileUserAgent, dmClient.DefaultRequestHeaders.Authorization);

        statusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("id=\"TagsInput\"");
        html.Should().Contain("@yaireo/tagify@4.38.0");
        html.Should().Contain("integrity=\"sha384-");
        html.Should().Contain("initContactTags(");
    }

    [Fact]
    public async Task EditForm_ContactWithTags_PreFillsCommaSeparatedValue()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (dmClient, dmUser) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "tagform_edit_prefill", "tagform_edit_prefill@example.com", roles: ["DungeonMaster"]);
        var contact = await TestDataHelper.CreateTestContactAsync(factory.Services, dmUser.Id, "Tagged Contact", groupId: 1);
        await TestDataHelper.CreateTestContactTagAsync(factory.Services, "shopkeeper", groupId: 1, contact.Id);
        await TestDataHelper.CreateTestContactTagAsync(factory.Services, "quest giver", groupId: 1, contact.Id);

        var (desktopStatus, desktopHtml) = await GetAsync(
            dmClient, $"/Contacts/Edit/{contact.Id}", DesktopUserAgent, dmClient.DefaultRequestHeaders.Authorization);
        var (mobileStatus, mobileHtml) = await GetAsync(
            dmClient, $"/Contacts/Edit/{contact.Id}", MobileUserAgent, dmClient.DefaultRequestHeaders.Authorization);

        desktopStatus.Should().Be(HttpStatusCode.OK);
        mobileStatus.Should().Be(HttpStatusCode.OK);
        // TagsInput pre-fills in the contact's own already-alphabetical tag order.
        desktopHtml.Should().Contain("value=\"quest giver, shopkeeper\"");
        mobileHtml.Should().Contain("value=\"quest giver, shopkeeper\"");
    }

    [Fact]
    public async Task EditForm_PlayerTier_IsRefusedOnBothUserAgents()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (dmClient, dmUser) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "tagform_player_refused_owner", "tagform_player_refused_owner@example.com", roles: ["DungeonMaster"]);
        var contact = await TestDataHelper.CreateTestContactAsync(factory.Services, dmUser.Id, "Contact", groupId: 1);

        var (playerClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "tagform_player_refused", "tagform_player_refused@example.com", roles: ["Player"]);

        var (createDesktopStatus, _) = await GetAsync(
            playerClient, "/Contacts/Create", DesktopUserAgent, playerClient.DefaultRequestHeaders.Authorization);
        var (createMobileStatus, _) = await GetAsync(
            playerClient, "/Contacts/Create", MobileUserAgent, playerClient.DefaultRequestHeaders.Authorization);
        var (editDesktopStatus, _) = await GetAsync(
            playerClient, $"/Contacts/Edit/{contact.Id}", DesktopUserAgent, playerClient.DefaultRequestHeaders.Authorization);
        var (editMobileStatus, _) = await GetAsync(
            playerClient, $"/Contacts/Edit/{contact.Id}", MobileUserAgent, playerClient.DefaultRequestHeaders.Authorization);

        createDesktopStatus.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.Redirect, HttpStatusCode.Unauthorized);
        createMobileStatus.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.Redirect, HttpStatusCode.Unauthorized);
        editDesktopStatus.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.Redirect, HttpStatusCode.Unauthorized);
        editMobileStatus.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.Redirect, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task EditForm_SuggestionList_ContainsOnlyOwnBoardTagNames()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        await TestDataHelper.SeedCampaignGroupAsync(factory.Services, 2);

        var (dmClient, dmUser) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "tagform_vocab_dm", "tagform_vocab_dm@example.com", roles: ["DungeonMaster"]);
        var contact = await TestDataHelper.CreateTestContactAsync(factory.Services, dmUser.Id, "Own Board Contact", groupId: 1);
        await TestDataHelper.CreateTestContactTagAsync(factory.Services, "shopkeeper", groupId: 1, contact.Id);

        var otherGroupOwner = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "tagform_vocab_other", "tagform_vocab_other@example.com", "Test123!", "Other Group Owner");
        var otherContact = await TestDataHelper.CreateTestContactAsync(
            factory.Services, otherGroupOwner.Id, "Other Board Contact", groupId: 2);

        // CreateTestContactTagAsync's join-attach step re-queries the contact through the
        // fail-closed group filter, so the shared TestGroupContext singleton must point at
        // group 2 while seeding the other board's tag -- restored to 1 before the DM's own
        // request, mirroring GroupSessionMiddlewareIntegrationTests' convention.
        factory.TestGroupContext.ActiveGroupId = 2;
        try
        {
            await TestDataHelper.CreateTestContactTagAsync(factory.Services, "another-board-tag", groupId: 2, otherContact.Id);
        }
        finally
        {
            factory.TestGroupContext.ActiveGroupId = 1;
        }

        var (statusCode, html) = await GetAsync(
            dmClient, "/Contacts/Create", DesktopUserAgent, dmClient.DefaultRequestHeaders.Authorization);

        statusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("shopkeeper");
        html.Should().NotContain("another-board-tag");
    }
}
