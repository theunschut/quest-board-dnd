using QuestBoard.IntegrationTests.Helpers;
using QuestBoard.Repository.Entities;
using System.Net;
using System.Net.Http.Headers;

namespace QuestBoard.IntegrationTests.Controllers;

// Wave 0 RED scaffold (Phase 57, Plan 01): this file intentionally references the not-yet-created
// ContactsController and its routes (/Contacts/Index, /Contacts/Details/{id}, /Contacts/Create,
// /Contacts/Edit/{id}, /Contacts/Delete/{id}, /Contacts/ToggleReveal/{id},
// /Contacts/ToggleShowHidden, /Contacts/AddNote, /Contacts/EditNote, /Contacts/DeleteNote). Since
// this file has no direct compile-time dependency on those controller/action symbols (only
// string-based route literals), it is expected to build cleanly but every [Fact] below MUST fail
// at runtime (404 Not Found) until Plan 04 lands — that is the intended Wave 0 RED state for an
// integration-test scaffold that targets routes rather than C# symbols directly.
//
// D-09b/D-12/D-13/D-14/D-15/D-15b/D-09 refer to decisions in 57-CONTEXT.md.
public class ContactsControllerIntegrationTests(WebApplicationFactoryBase factory) : IClassFixture<WebApplicationFactoryBase>
{
    // (1) D-09b — Player is blocked from Create/Edit/Delete/ToggleReveal; DM-tier succeeds.

    [Fact]
    public async Task Create_Get_PlayerAccess_ShouldBeBlocked()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (playerClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "contact_player_create", "contact_player_create@example.com", roles: ["Player"]);

        var response = await playerClient.GetAsync("/Contacts/Create", TestContext.Current.CancellationToken);

        // Forbid() under the cookie authentication scheme redirects to /Account/AccessDenied (302)
        // rather than returning a bare 403 — matches this suite's established denied-access pattern
        // (see CharactersControllerIntegrationTests).
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.Redirect, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Create_Get_DungeonMasterAccess_ShouldSucceed()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (dmClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "contact_dm_create", "contact_dm_create@example.com", roles: ["DungeonMaster"]);

        var response = await dmClient.GetAsync("/Contacts/Create", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Create_Get_AdminAccess_ShouldSucceed()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (adminClient, _) = await AuthenticationHelper.CreateAuthenticatedAdminClientAsync(factory);

        var response = await adminClient.GetAsync("/Contacts/Create", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Edit_Get_PlayerAccess_ShouldBeBlocked()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (dmClient, dmUser) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "contact_dm_owner_edit", "contact_dm_owner_edit@example.com", roles: ["DungeonMaster"]);
        var contact = await TestDataHelper.CreateTestContactAsync(factory.Services, dmUser.Id, "Editable Contact", groupId: 1);

        var (playerClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "contact_player_edit", "contact_player_edit@example.com", roles: ["Player"]);

        var response = await playerClient.GetAsync($"/Contacts/Edit/{contact.Id}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.Redirect, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Edit_Post_DungeonMasterAccess_ShouldSucceed()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (dmClient, dmUser) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "contact_dm_edit", "contact_dm_edit@example.com", roles: ["DungeonMaster"]);
        var contact = await TestDataHelper.CreateTestContactAsync(factory.Services, dmUser.Id, "Editable Contact", groupId: 1);

        var formContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Id"] = contact.Id.ToString(),
            ["Name"] = "Renamed Contact",
            ["TownCity"] = "Waterdeep",
            ["SubLocation"] = "The Guilded Rose Smithy",
            ["Description"] = "An updated description."
        });

        var response = await dmClient.PostAsync($"/Contacts/Edit/{contact.Id}", formContent, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.Should().NotContain("AccessDenied");
    }

    [Fact]
    public async Task Delete_PlayerAccess_ShouldBeBlocked()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (dmClient, dmUser) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "contact_dm_owner_delete", "contact_dm_owner_delete@example.com", roles: ["DungeonMaster"]);
        var contact = await TestDataHelper.CreateTestContactAsync(factory.Services, dmUser.Id, "Deletable Contact", groupId: 1);

        var (playerClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "contact_player_delete", "contact_player_delete@example.com", roles: ["Player"]);

        var formContent = new FormUrlEncodedContent(new Dictionary<string, string> { ["id"] = contact.Id.ToString() });
        var response = await playerClient.PostAsync("/Contacts/Delete", formContent, TestContext.Current.CancellationToken);

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.Redirect, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Delete_AdminAccess_ShouldSucceed()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (dmClient, dmUser) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "contact_dm_owner_delete2", "contact_dm_owner_delete2@example.com", roles: ["DungeonMaster"]);
        var contact = await TestDataHelper.CreateTestContactAsync(factory.Services, dmUser.Id, "Deletable Contact 2", groupId: 1);

        var (adminClient, _) = await AuthenticationHelper.CreateAuthenticatedAdminClientAsync(factory);

        var formContent = new FormUrlEncodedContent(new Dictionary<string, string> { ["id"] = contact.Id.ToString() });
        var response = await adminClient.PostAsync("/Contacts/Delete", formContent, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.Should().NotContain("AccessDenied");
    }

    [Fact]
    public async Task ToggleReveal_PlayerAccess_ShouldBeBlocked()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (dmClient, dmUser) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "contact_dm_owner_toggle", "contact_dm_owner_toggle@example.com", roles: ["DungeonMaster"]);
        var contact = await TestDataHelper.CreateTestContactAsync(factory.Services, dmUser.Id, "Toggleable Contact", groupId: 1);

        var (playerClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "contact_player_toggle", "contact_player_toggle@example.com", roles: ["Player"]);

        var formContent = new FormUrlEncodedContent(new Dictionary<string, string> { ["id"] = contact.Id.ToString() });
        var response = await playerClient.PostAsync("/Contacts/ToggleReveal", formContent, TestContext.Current.CancellationToken);

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.Redirect, HttpStatusCode.Unauthorized);
    }

    // (2) D-14 — a Contact created via Create POST defaults to IsRevealed == false.

    [Fact]
    public async Task Create_Post_NewContact_DefaultsToHidden()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (dmClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "contact_dm_defaulthidden", "contact_dm_defaulthidden@example.com", roles: ["DungeonMaster"]);

        var formContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Name"] = "Brand New Contact",
            ["TownCity"] = "Baldur's Gate"
        });

        var response = await dmClient.PostAsync("/Contacts/Create", formContent, TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
        var persisted = context.Contacts.FirstOrDefault(c => c.Name == "Brand New Contact");
        persisted.Should().NotBeNull();
        persisted!.IsRevealed.Should().BeFalse();
    }

    // (3) D-12/D-13 — a hidden Contact is absent from a Player's Index list, and a direct
    // Details/{id} GET for that hidden Contact returns NotFound (404) for the Player.

    [Fact]
    public async Task Index_HiddenContact_NotShownToPlayer()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (dmClient, dmUser) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "contact_dm_hiddenindex", "contact_dm_hiddenindex@example.com", roles: ["DungeonMaster"]);
        await TestDataHelper.CreateTestContactAsync(
            factory.Services, dmUser.Id, "Hidden From Players", groupId: 1, isRevealed: false);

        var (playerClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "contact_player_hiddenindex", "contact_player_hiddenindex@example.com", roles: ["Player"]);

        var response = await playerClient.GetAsync("/Contacts/Index", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        content.Should().NotContain("Hidden From Players");
    }

    [Fact]
    public async Task Details_HiddenContact_PlayerGetsNotFound()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (dmClient, dmUser) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "contact_dm_hiddendetails", "contact_dm_hiddendetails@example.com", roles: ["DungeonMaster"]);
        var contact = await TestDataHelper.CreateTestContactAsync(
            factory.Services, dmUser.Id, "Hidden Details Contact", groupId: 1, isRevealed: false);

        var (playerClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "contact_player_hiddendetails", "contact_player_hiddendetails@example.com", roles: ["Player"]);

        var response = await playerClient.GetAsync($"/Contacts/Details/{contact.Id}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // CR-01 regression (code review) — GetContactImage must apply the same hidden-Contact
    // visibility check as Details/Index, not just the group-scoped query filter. Otherwise any
    // authenticated group member can fetch a hidden Contact's portrait by guessing/enumerating
    // its id, bypassing the hidden/reveal model entirely.

    [Fact]
    public async Task GetContactImage_HiddenContact_PlayerGetsNotFound()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (dmClient, dmUser) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "contact_dm_hiddenimage", "contact_dm_hiddenimage@example.com", roles: ["DungeonMaster"]);
        var contact = await TestDataHelper.CreateTestContactAsync(
            factory.Services, dmUser.Id, "Hidden Image Contact", groupId: 1, isRevealed: false,
            imageData: [1, 2, 3, 4]);

        var (playerClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "contact_player_hiddenimage", "contact_player_hiddenimage@example.com", roles: ["Player"]);

        var response = await playerClient.GetAsync($"/Contacts/GetContactImage/{contact.Id}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetContactImage_HiddenContact_CreatorCanFetchOwnImage()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (creatorClient, creator) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "contact_creator_hiddenimage", "contact_creator_hiddenimage@example.com", roles: ["DungeonMaster"]);
        var contact = await TestDataHelper.CreateTestContactAsync(
            factory.Services, creator.Id, "Creator's Hidden Image Contact", groupId: 1, isRevealed: false,
            imageData: [1, 2, 3, 4]);

        var response = await creatorClient.GetAsync($"/Contacts/GetContactImage/{contact.Id}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // (4) D-15 branch 1 — the creator exception: the DM-tier user who created a hidden Contact
    // sees it on their own Index and Details regardless of toggle state.

    [Fact]
    public async Task Details_HiddenContact_CreatorSeesOwnHiddenContactRegardlessOfToggle()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (creatorClient, creator) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "contact_creator_sees_own", "contact_creator_sees_own@example.com", roles: ["DungeonMaster"]);
        var contact = await TestDataHelper.CreateTestContactAsync(
            factory.Services, creator.Id, "Creator's Own Hidden Contact", groupId: 1, isRevealed: false);

        // Toggle deliberately left at its default (OFF) — the creator exception must not depend on it.
        var response = await creatorClient.GetAsync($"/Contacts/Details/{contact.Id}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Index_HiddenContact_CreatorSeesOwnHiddenContactInIndex()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (creatorClient, creator) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "contact_creator_sees_own_index", "contact_creator_sees_own_index@example.com", roles: ["DungeonMaster"]);
        await TestDataHelper.CreateTestContactAsync(
            factory.Services, creator.Id, "Creator's Own Hidden In Index", groupId: 1, isRevealed: false);

        var response = await creatorClient.GetAsync("/Contacts/Index", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        // Razor HTML-encodes the apostrophe (XSS-safe rendering), so the rendered markup contains
        // "Creator&#x27;s..." rather than the raw literal.
        content.Should().Contain("Creator&#x27;s Own Hidden In Index");
    }

    // (5) D-15 branch 2 — a different DM-tier user does NOT see the hidden Contact with toggle
    // OFF, but DOES see it after POSTing ToggleShowHidden (toggle ON).

    [Fact]
    public async Task Index_HiddenContact_DifferentDmTierUser_HiddenByDefault_VisibleAfterToggle()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (creatorClient, creator) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "contact_toggle_creator", "contact_toggle_creator@example.com", roles: ["DungeonMaster"]);
        await TestDataHelper.CreateTestContactAsync(
            factory.Services, creator.Id, "Toggle-Gated Contact", groupId: 1, isRevealed: false);

        var (otherDmClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "contact_toggle_otherdm", "contact_toggle_otherdm@example.com", roles: ["DungeonMaster"]);

        // Toggle OFF (default): the other DM-tier user must not see the hidden contact.
        var beforeToggleResponse = await otherDmClient.GetAsync("/Contacts/Index", TestContext.Current.CancellationToken);
        beforeToggleResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var beforeContent = await beforeToggleResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        beforeContent.Should().NotContain("Toggle-Gated Contact");

        // Toggle ON via POST — same HttpClient instance carries the session cookie forward.
        var toggleResponse = await otherDmClient.PostAsync(
            "/Contacts/ToggleShowHidden", new FormUrlEncodedContent([]), TestContext.Current.CancellationToken);
        toggleResponse.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.OK);

        // Toggle ON: the other DM-tier user now sees the hidden contact.
        var afterToggleResponse = await otherDmClient.GetAsync("/Contacts/Index", TestContext.Current.CancellationToken);
        afterToggleResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var afterContent = await afterToggleResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        afterContent.Should().Contain("Toggle-Gated Contact");
    }

    // (6) D-15 branch 3 — a plain Player never sees the hidden Contact regardless of any toggle state.

    [Fact]
    public async Task Index_HiddenContact_Player_NeverSeesHiddenContactEvenAfterToggleAttempt()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (creatorClient, creator) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "contact_player_never_creator", "contact_player_never_creator@example.com", roles: ["DungeonMaster"]);
        await TestDataHelper.CreateTestContactAsync(
            factory.Services, creator.Id, "Never Visible To Player", groupId: 1, isRevealed: false);

        var (playerClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "contact_player_never", "contact_player_never@example.com", roles: ["Player"]);

        // A Player posting ToggleShowHidden should have no visibility effect (D-15 branch 3);
        // this either 403s/redirects to AccessDenied or is a no-op — either way, Index must
        // never show the hidden contact for a plain Player afterward.
        await playerClient.PostAsync(
            "/Contacts/ToggleShowHidden", new FormUrlEncodedContent([]), TestContext.Current.CancellationToken);

        var response = await playerClient.GetAsync("/Contacts/Index", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        content.Should().NotContain("Never Visible To Player");
    }

    // (7) D-15b — the toggle is per-group and session-scoped: toggling ON for group 1 does not
    // reveal hidden contacts when the active group is group 2.

    [Fact]
    public async Task ToggleShowHidden_IsScopedPerGroup_DoesNotLeakAcrossGroups()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        await TestDataHelper.SeedCampaignGroupAsync(factory.Services, 2);

        var (creatorClient, creator) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "contact_pergroup_creator", "contact_pergroup_creator@example.com", roles: ["DungeonMaster"]);
        await TestDataHelper.CreateTestContactAsync(
            factory.Services, creator.Id, "Group One Hidden Contact", groupId: 1, isRevealed: false);

        var (dmClient, dmUser) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "contact_pergroup_dm", "contact_pergroup_dm@example.com", roles: ["DungeonMaster"]);

        // Add the DM-tier user to group 2 as well so they can view group 2's Contacts board.
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
            context.UserGroups.Add(new QuestBoard.Repository.Entities.UserGroupEntity
            {
                UserId = dmUser.Id,
                GroupId = 2,
                GroupRole = (int)QuestBoard.Domain.Enums.GroupRole.DungeonMaster
            });
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        try
        {
            // Toggle ON while active group is 1.
            factory.TestGroupContext.ActiveGroupId = 1;
            var toggleResponse = await dmClient.PostAsync(
                "/Contacts/ToggleShowHidden", new FormUrlEncodedContent([]), TestContext.Current.CancellationToken);
            toggleResponse.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.OK);

            // Switch active group to 2 — the group-1 toggle must not apply here.
            factory.TestGroupContext.ActiveGroupId = 2;
            var response = await dmClient.GetAsync("/Contacts/Index", TestContext.Current.CancellationToken);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            content.Should().NotContain("Group One Hidden Contact");
        }
        finally
        {
            factory.TestGroupContext.ActiveGroupId = 1;
        }
    }

    // (8) D-09 — any group member (Player) can POST AddNote, EditNote, and DeleteNote on a note
    // authored by a different user — no ownership guard blocks them.

    [Fact]
    public async Task AddNote_AnyGroupMember_CanAddNoteToVisibleContact()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (dmClient, dmUser) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "contact_addnote_dm", "contact_addnote_dm@example.com", roles: ["DungeonMaster"]);
        var contact = await TestDataHelper.CreateTestContactAsync(
            factory.Services, dmUser.Id, "Notable Contact For Add", groupId: 1, isRevealed: true);

        var (playerClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "contact_addnote_player", "contact_addnote_player@example.com", roles: ["Player"]);

        var formContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["contactId"] = contact.Id.ToString(),
            ["Text"] = "A player-authored note."
        });

        var response = await playerClient.PostAsync("/Contacts/AddNote", formContent, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.Should().NotContain("AccessDenied");
    }

    [Fact]
    public async Task EditNote_DifferentGroupMember_CanEditNoteAuthoredByAnotherUser()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (dmClient, dmUser) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "contact_editnote_dm", "contact_editnote_dm@example.com", roles: ["DungeonMaster"]);
        var contact = await TestDataHelper.CreateTestContactAsync(
            factory.Services, dmUser.Id, "Notable Contact For Edit", groupId: 1, isRevealed: true);
        var note = await TestDataHelper.CreateTestContactNoteAsync(
            factory.Services, contact.Id, dmUser.Id, "Original note text.");

        var (playerClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "contact_editnote_player", "contact_editnote_player@example.com", roles: ["Player"]);

        var formContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["id"] = note.Id.ToString(),
            ["contactId"] = contact.Id.ToString(),
            ["Text"] = "Edited by a completely different group member."
        });

        var response = await playerClient.PostAsync("/Contacts/EditNote", formContent, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.Should().NotContain("AccessDenied");
    }

    [Fact]
    public async Task DeleteNote_DifferentGroupMember_CanDeleteNoteAuthoredByAnotherUser()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (dmClient, dmUser) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "contact_deletenote_dm", "contact_deletenote_dm@example.com", roles: ["DungeonMaster"]);
        var contact = await TestDataHelper.CreateTestContactAsync(
            factory.Services, dmUser.Id, "Notable Contact For Delete", groupId: 1, isRevealed: true);
        var note = await TestDataHelper.CreateTestContactNoteAsync(
            factory.Services, contact.Id, dmUser.Id, "Note to be deleted by someone else.");

        var (playerClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "contact_deletenote_player", "contact_deletenote_player@example.com", roles: ["Player"]);

        var formContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["id"] = note.Id.ToString(),
            ["contactId"] = contact.Id.ToString()
        });

        var response = await playerClient.PostAsync("/Contacts/DeleteNote", formContent, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.Should().NotContain("AccessDenied");

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
        var persisted = context.ContactNotes.FirstOrDefault(n => n.Id == note.Id);
        persisted.Should().BeNull();
    }

    // (9) Cross-tenant IDOR — a Details/{id} GET for a Contact belonging to another group
    // returns 404.

    [Fact]
    public async Task Details_ContactInDifferentGroup_ReturnsNotFound()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        await TestDataHelper.SeedCampaignGroupAsync(factory.Services, 2);

        var (adminClient, _) = await AuthenticationHelper.CreateAuthenticatedAdminClientAsync(factory);
        var otherGroupOwner = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "contact_crossgroup_owner", "contact_crossgroup_owner@example.com", "Test123!", "Other Group Owner");
        var contact = await TestDataHelper.CreateTestContactAsync(
            factory.Services, otherGroupOwner.Id, "Other Group's Contact", groupId: 2);

        var response = await adminClient.GetAsync($"/Contacts/Details/{contact.Id}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // Proves the checker's required Plan 02 -> Plan 03 wiring is actually reachable through the
    // real Edit POST action, not only through Plan 02's isolated service-level unit test.
    [Fact]
    public async Task Edit_NewOriginalImageUpload_ClearsStaleCroppedImage()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (dmClient, dmUser) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "contact_new_original_clears_crop", "contact_new_original_clears_crop@example.com", roles: ["DungeonMaster"]);
        var contact = await TestDataHelper.CreateTestContactAsync(
            factory.Services, dmUser.Id, "Contact With Stale Crop", groupId: 1);

        byte[] originalBytes = [1, 2, 3, 4];
        byte[] staleCroppedBytes = [9, 9, 9, 9];
        byte[] newOriginalBytes = [5, 6, 7, 8];

        using (var seedScope = factory.Services.CreateScope())
        {
            var seedContext = seedScope.ServiceProvider.GetRequiredService<QuestBoardContext>();
            seedContext.Set<ContactImageEntity>().Add(new ContactImageEntity
            {
                Id = contact.Id,
                OriginalImageData = originalBytes,
                CroppedImageData = staleCroppedBytes
            });
            await seedContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using var formContent = new MultipartFormDataContent
        {
            { new StringContent(contact.Id.ToString()), "Id" },
            { new StringContent("Contact With Stale Crop"), "Name" },
            { new StringContent("Waterdeep"), "TownCity" },
            { new StringContent(""), "SubLocation" },
            { new StringContent(""), "Description" }
        };
        var fileContent = new ByteArrayContent(newOriginalBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        formContent.Add(fileContent, "ContactImageFile", "new.png");

        var response = await dmClient.PostAsync(
            $"/Contacts/Edit/{contact.Id}", formContent, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.Should().NotContain("AccessDenied");

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
        var persistedImage = await context.Set<ContactImageEntity>().FindAsync(
            [contact.Id], TestContext.Current.CancellationToken);
        persistedImage.Should().NotBeNull();
        persistedImage!.CroppedImageData.Should().BeNull();
        persistedImage.OriginalImageData.Should().Equal(newOriginalBytes);
    }

    // Proves a real posted CroppedPictureFile is validated and persisted through the widened
    // 4-arg UpdateAsync call, not just cleared/ignored like the single-file path.
    [Fact]
    public async Task Edit_NewOriginalAndCroppedImageUpload_PersistsSubmittedCrop()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (dmClient, dmUser) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "contact_crop_persists", "contact_crop_persists@example.com", roles: ["DungeonMaster"]);
        var contact = await TestDataHelper.CreateTestContactAsync(
            factory.Services, dmUser.Id, "Contact Getting A Real Crop", groupId: 1);

        byte[] newOriginalBytes = [5, 6, 7, 8];
        byte[] submittedCropBytes = [10, 20, 30, 40, 50];

        using var formContent = new MultipartFormDataContent
        {
            { new StringContent(contact.Id.ToString()), "Id" },
            { new StringContent("Contact Getting A Real Crop"), "Name" },
            { new StringContent("Waterdeep"), "TownCity" },
            { new StringContent(""), "SubLocation" },
            { new StringContent(""), "Description" }
        };
        var originalFileContent = new ByteArrayContent(newOriginalBytes);
        originalFileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        formContent.Add(originalFileContent, "ContactImageFile", "new.png");

        var croppedFileContent = new ByteArrayContent(submittedCropBytes);
        croppedFileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        formContent.Add(croppedFileContent, "CroppedPictureFile", "new-cropped.png");

        var response = await dmClient.PostAsync(
            $"/Contacts/Edit/{contact.Id}", formContent, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.Should().NotContain("AccessDenied");

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
        var persistedImage = await context.Set<ContactImageEntity>().FindAsync(
            [contact.Id], TestContext.Current.CancellationToken);
        persistedImage.Should().NotBeNull();
        persistedImage!.CroppedImageData.Should().NotBeNull();
        persistedImage.CroppedImageData.Should().Equal(submittedCropBytes);
        persistedImage.OriginalImageData.Should().Equal(newOriginalBytes);
    }

    // Visibility parity: the new GetCroppedContactImage read action must apply the identical
    // IsVisibleTo gate as GetContactImage — a hidden contact returns NotFound even though a
    // crop is stored.
    [Fact]
    public async Task GetCroppedContactImage_HiddenContact_PlayerGetsNotFound()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (dmClient, dmUser) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "contact_dm_hiddencropped", "contact_dm_hiddencropped@example.com", roles: ["DungeonMaster"]);
        var contact = await TestDataHelper.CreateTestContactAsync(
            factory.Services, dmUser.Id, "Hidden Cropped Contact", groupId: 1, isRevealed: false,
            imageData: [1, 2, 3, 4]);

        using (var seedScope = factory.Services.CreateScope())
        {
            var seedContext = seedScope.ServiceProvider.GetRequiredService<QuestBoardContext>();
            var image = await seedContext.Set<ContactImageEntity>().FindAsync(
                [contact.Id], TestContext.Current.CancellationToken);
            image!.CroppedImageData = [9, 9, 9, 9];
            await seedContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var (playerClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "contact_player_hiddencropped", "contact_player_hiddencropped@example.com", roles: ["Player"]);

        var response = await playerClient.GetAsync(
            $"/Contacts/GetCroppedContactImage/{contact.Id}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // Visibility parity: a revealed contact's cropped image is fetchable (200 with content).
    [Fact]
    public async Task GetCroppedContactImage_VisibleContact_ReturnsOkWithContent()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (dmClient, dmUser) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "contact_dm_visiblecropped", "contact_dm_visiblecropped@example.com", roles: ["DungeonMaster"]);
        var contact = await TestDataHelper.CreateTestContactAsync(
            factory.Services, dmUser.Id, "Visible Cropped Contact", groupId: 1, isRevealed: true,
            imageData: [1, 2, 3, 4]);

        byte[] croppedBytes = [9, 9, 9, 9, 9];
        using (var seedScope = factory.Services.CreateScope())
        {
            var seedContext = seedScope.ServiceProvider.GetRequiredService<QuestBoardContext>();
            var image = await seedContext.Set<ContactImageEntity>().FindAsync(
                [contact.Id], TestContext.Current.CancellationToken);
            image!.CroppedImageData = croppedBytes;
            await seedContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var (playerClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "contact_player_visiblecropped", "contact_player_visiblecropped@example.com", roles: ["Player"]);

        var response = await playerClient.GetAsync(
            $"/Contacts/GetCroppedContactImage/{contact.Id}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken);
        content.Should().NotBeEmpty();
        content.Should().Equal(croppedBytes);
    }
}
