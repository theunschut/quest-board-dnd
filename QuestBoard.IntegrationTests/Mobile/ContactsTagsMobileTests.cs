using System.Net;
using System.Net.Http.Headers;
using QuestBoard.IntegrationTests.Helpers;

namespace QuestBoard.IntegrationTests.Mobile;

// This app selects its mobile views from the request's User-Agent header, not from viewport
// size, so a browser device-toolbar emulation check would never exercise the middleware that
// actually picks which file renders. Every assertion here sends a real mobile (or desktop)
// User-Agent header by hand, matching the existing mobile-view test convention, and one test
// sends the exact same url under both headers to prove the two genuinely select different
// markup rather than merely happening to pass against whichever file rendered.
public class ContactsTagsMobileTests(WebApplicationFactoryBase factory) : IClassFixture<WebApplicationFactoryBase>
{
    private const string MobileUserAgent =
        "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1";

    private const string DesktopUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

    private static async Task<(HttpStatusCode StatusCode, string Html)> SendAsync(
        HttpClient client, string url, string userAgent, AuthenticationHeaderValue? authorization)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("User-Agent", userAgent);
        if (authorization != null)
        {
            request.Headers.Authorization = authorization;
        }
        // Use the caller's own client (created with AllowAutoRedirect = false) rather than a
        // fresh factory.CreateClient(), so a refusal surfaces as the real redirect/forbidden
        // status instead of being silently followed to a 200 OK.
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        return (response.StatusCode, html);
    }

    [Fact]
    public async Task Index_MobileUserAgent_RendersFilterTriggerAndDrawer()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (dmClient, dmUser) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "mobtag_trigger_dm", "mobtag_trigger_dm@example.com", roles: ["DungeonMaster"]);
        var contact = await TestDataHelper.CreateTestContactAsync(factory.Services, dmUser.Id, "Trigger Contact", isRevealed: true);
        var tag1 = await TestDataHelper.CreateTestContactTagAsync(factory.Services, "shopkeeper", contactIds: [contact.Id]);
        var tag2 = await TestDataHelper.CreateTestContactTagAsync(factory.Services, "quest giver", contactIds: [contact.Id]);

        var (statusCode, html) = await SendAsync(
            dmClient, "/Contacts/Index", MobileUserAgent, dmClient.DefaultRequestHeaders.Authorization);

        statusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("Filter Tags");
        html.Should().Contain("contactFilterOffcanvas");
        html.Should().Contain("Filter by Tag");
        html.Should().Contain($"value=\"{tag1.Id}\"");
        html.Should().Contain($"value=\"{tag2.Id}\"");
        html.Should().Contain("shopkeeper");
        html.Should().Contain("quest giver");
        html.Should().NotContain("disabled>");
    }

    // The mobile-only drawer id and the desktop-only card grid class each appear under exactly
    // one of the two headers -- that pairing is what actually proves the middleware chose a
    // different file per request rather than the assertions merely passing coincidentally.
    [Fact]
    public async Task Index_MobileAndDesktopUserAgents_SelectDifferentLayouts()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (dmClient, dmUser) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "mobtag_split_dm", "mobtag_split_dm@example.com", roles: ["DungeonMaster"]);
        var contact = await TestDataHelper.CreateTestContactAsync(factory.Services, dmUser.Id, "Split Contact", isRevealed: true);
        await TestDataHelper.CreateTestContactTagAsync(factory.Services, "split-tag", contactIds: [contact.Id]);

        var (mobileStatus, mobileHtml) = await SendAsync(
            dmClient, "/Contacts/Index", MobileUserAgent, dmClient.DefaultRequestHeaders.Authorization);
        var (desktopStatus, desktopHtml) = await SendAsync(
            dmClient, "/Contacts/Index", DesktopUserAgent, dmClient.DefaultRequestHeaders.Authorization);

        mobileStatus.Should().Be(HttpStatusCode.OK);
        desktopStatus.Should().Be(HttpStatusCode.OK);
        mobileHtml.Should().Contain("contactFilterOffcanvas");
        mobileHtml.Should().NotContain("contact-card");
        desktopHtml.Should().NotContain("contactFilterOffcanvas");
        desktopHtml.Should().Contain("contact-card");
    }

    [Fact]
    public async Task Index_MobilePlayer_ReceivesNoTagMarkupAtAll()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var dm = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "mobtag_player_owner", "mobtag_player_owner@example.com", "Test123!", "Contact Owner");
        var contact = await TestDataHelper.CreateTestContactAsync(factory.Services, dm.Id, "Player View Contact", isRevealed: true);
        await TestDataHelper.CreateTestContactTagAsync(factory.Services, "hidden-from-player", contactIds: [contact.Id]);

        var (playerClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "mobtag_player", "mobtag_player@example.com", roles: ["Player"]);

        var (statusCode, html) = await SendAsync(
            playerClient, "/Contacts/Index", MobileUserAgent, playerClient.DefaultRequestHeaders.Authorization);

        statusCode.Should().Be(HttpStatusCode.OK);
        html.Should().NotContain("contactFilterOffcanvas");
        html.Should().NotContain("contact-tag-chip");
        html.Should().NotContain("contact-tag-list");
        html.Should().NotContain("Filter Tags");
        html.Should().NotContain("hidden-from-player");
    }

    [Fact]
    public async Task Details_MobileDungeonMaster_RendersTagChips()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (dmClient, dmUser) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "mobtag_details_dm", "mobtag_details_dm@example.com", roles: ["DungeonMaster"]);
        var contact = await TestDataHelper.CreateTestContactAsync(factory.Services, dmUser.Id, "Detail Contact", isRevealed: true);
        await TestDataHelper.CreateTestContactTagAsync(factory.Services, "artisan", contactIds: [contact.Id]);

        var (dmStatus, dmHtml) = await SendAsync(
            dmClient, $"/Contacts/Details/{contact.Id}", MobileUserAgent, dmClient.DefaultRequestHeaders.Authorization);

        dmStatus.Should().Be(HttpStatusCode.OK);
        dmHtml.Should().Contain("contact-tag-chip");
        dmHtml.Should().Contain("artisan");

        var (playerClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "mobtag_details_player", "mobtag_details_player@example.com", roles: ["Player"]);

        var (playerStatus, playerHtml) = await SendAsync(
            playerClient, $"/Contacts/Details/{contact.Id}", MobileUserAgent, playerClient.DefaultRequestHeaders.Authorization);

        playerStatus.Should().Be(HttpStatusCode.OK);
        playerHtml.Should().NotContain("contact-tag-chip");
        playerHtml.Should().NotContain("artisan");
    }

    [Fact]
    public async Task Index_MobileBoardWithNoTags_RendersDisabledTriggerAndHint()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (dmClient, dmUser) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "mobtag_notags_dm", "mobtag_notags_dm@example.com", roles: ["DungeonMaster"]);
        await TestDataHelper.CreateTestContactAsync(factory.Services, dmUser.Id, "No Tags Contact", isRevealed: true);

        var (statusCode, html) = await SendAsync(
            dmClient, "/Contacts/Index", MobileUserAgent, dmClient.DefaultRequestHeaders.Authorization);

        statusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("Filter Tags");
        html.Should().Contain("disabled>");
        html.Should().Contain("No tags yet. Add tags when creating or editing a contact to start filtering.");
        // The trigger's data-bs-target keeps referencing the drawer's id even while disabled
        // (harmless, since the button cannot be clicked), so the absence check targets the
        // drawer element itself rather than every occurrence of the id string.
        html.Should().NotContain("class=\"offcanvas offcanvas-bottom\" id=\"contactFilterOffcanvas\"");
    }

    [Fact]
    public async Task Index_MobileActiveFilter_ShowsActiveBadgeAndCarriesSelectionOnToggleForm()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (dmClient, dmUser) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "mobtag_active_dm", "mobtag_active_dm@example.com", roles: ["DungeonMaster"]);
        var contact = await TestDataHelper.CreateTestContactAsync(factory.Services, dmUser.Id, "Active Filter Contact", isRevealed: true);
        var tag = await TestDataHelper.CreateTestContactTagAsync(factory.Services, "active-tag", contactIds: [contact.Id]);

        var (statusCode, html) = await SendAsync(
            dmClient, $"/Contacts/Index?tag={tag.Id}", MobileUserAgent, dmClient.DefaultRequestHeaders.Authorization);

        statusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("badge bg-dark ms-1\">Active");
        html.Should().Contain($"<input type=\"hidden\" name=\"tag\" value=\"{tag.Id}\" />");
    }

    [Fact]
    public async Task Index_MobileTagOnlyOnUnrevealedContact_AbsentFromDrawer()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (dmClient, dmUser) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "mobtag_unrevealed_dm", "mobtag_unrevealed_dm@example.com", roles: ["DungeonMaster"]);
        var otherOwner = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "mobtag_unrevealed_owner", "mobtag_unrevealed_owner@example.com", "Test123!", "Other Owner");

        var visibleContact = await TestDataHelper.CreateTestContactAsync(factory.Services, dmUser.Id, "Visible Contact", isRevealed: true);
        await TestDataHelper.CreateTestContactTagAsync(factory.Services, "visible-tag", contactIds: [visibleContact.Id]);

        var unrevealedContact = await TestDataHelper.CreateTestContactAsync(factory.Services, otherOwner.Id, "Unrevealed Contact", isRevealed: false);
        await TestDataHelper.CreateTestContactTagAsync(factory.Services, "secret-tag", contactIds: [unrevealedContact.Id]);

        var (statusCode, html) = await SendAsync(
            dmClient, "/Contacts/Index", MobileUserAgent, dmClient.DefaultRequestHeaders.Authorization);

        statusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("visible-tag");
        html.Should().NotContain("secret-tag");
    }

    // The shared filter helper falls back to the full visible list whenever a selection matches
    // zero contacts, so the no-match branch is only reachable when the board has no visible
    // contacts at all -- an empty board carrying a stale or fabricated tag id in the query
    // string is exactly that case, and is what this test sets up.
    [Fact]
    public async Task Index_MobileActiveFilterNoMatches_RendersNoMatchHeading()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (dmClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "mobtag_nomatch_dm", "mobtag_nomatch_dm@example.com", roles: ["DungeonMaster"]);

        var (statusCode, html) = await SendAsync(
            dmClient, "/Contacts/Index?tag=999999", MobileUserAgent, dmClient.DefaultRequestHeaders.Authorization);

        statusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("No contacts match your filters");
        html.Should().Contain("Try selecting different tags.");
        html.Should().NotContain("No one has added a contact yet. DMs can create the first one to start building out the world.");
    }

    // The mobile view is selected by the request's User-Agent header, so the ownership x
    // toggle rule for tag chips and filter options is re-proven here under a real mobile
    // User-Agent rather than assumed from the desktop coverage alone.
    [Fact]
    public async Task Index_MobileNonOwningDungeonMaster_SeesNoChipsOrDrawerOptionsUntilShowHiddenIsOn()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var owner = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "mobtag_ownership_owner", "mobtag_ownership_owner@example.com", "Test123!", "Contact Owner");
        var contact = await TestDataHelper.CreateTestContactAsync(factory.Services, owner.Id, "Owner's Mobile Contact", isRevealed: true);
        await TestDataHelper.CreateTestContactTagAsync(factory.Services, "owner-only-mobile-tag", contactIds: [contact.Id]);

        var (nonOwnerClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "mobtag_ownership_nonowner", "mobtag_ownership_nonowner@example.com", roles: ["DungeonMaster"]);

        var (beforeStatus, beforeHtml) = await SendAsync(
            nonOwnerClient, "/Contacts/Index", MobileUserAgent, nonOwnerClient.DefaultRequestHeaders.Authorization);

        beforeStatus.Should().Be(HttpStatusCode.OK);
        beforeHtml.Should().NotContain("contact-tag-chip");
        beforeHtml.Should().NotContain("owner-only-mobile-tag");
        beforeHtml.Should().Contain("No tags yet. Add tags when creating or editing a contact to start filtering.");

        var toggleResponse = await nonOwnerClient.PostAsync(
            "/Contacts/ToggleShowHidden", new FormUrlEncodedContent([]), TestContext.Current.CancellationToken);
        toggleResponse.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.OK);

        var (afterStatus, afterHtml) = await SendAsync(
            nonOwnerClient, "/Contacts/Index", MobileUserAgent, nonOwnerClient.DefaultRequestHeaders.Authorization);

        afterStatus.Should().Be(HttpStatusCode.OK);
        afterHtml.Should().Contain("contact-tag-chip");
        afterHtml.Should().Contain("owner-only-mobile-tag");
        afterHtml.Should().Contain("contactFilterOffcanvas");
    }
}
