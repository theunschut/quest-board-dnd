using QuestBoard.IntegrationTests.Helpers;
using QuestBoard.Repository.Entities;
using System.Net;
using System.Net.Http.Headers;

namespace QuestBoard.IntegrationTests.Controllers;

// Route-level coverage for ContactsController: authorization on the write actions, the
// hidden/reveal visibility model (including the creator exception and the per-group Show Hidden
// toggle), and note authoring. These tests drive string route literals rather than controller
// symbols, so a renamed or removed route fails here at runtime rather than at compile time.
public class ContactsControllerIntegrationTests(WebApplicationFactoryBase factory) : IClassFixture<WebApplicationFactoryBase>
{
    // (1) Player is blocked from Create/Edit/Delete/ToggleReveal; DM-tier succeeds.

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

    // (2) A Contact created via Create POST defaults to IsRevealed == false.

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

    // (3) A hidden Contact is absent from a Player's Index list, and a direct
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

    // Regression guard: GetContactImage must apply the same hidden-Contact
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

    // (4) The creator exception: the DM-tier user who created a hidden Contact
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

    // (5) A different DM-tier user does NOT see the hidden Contact with toggle
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

    // (6) A plain Player never sees the hidden Contact regardless of any toggle state.

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

        // A Player posting ToggleShowHidden should have no visibility effect;
        // this either 403s/redirects to AccessDenied or is a no-op — either way, Index must
        // never show the hidden contact for a plain Player afterward.
        await playerClient.PostAsync(
            "/Contacts/ToggleShowHidden", new FormUrlEncodedContent([]), TestContext.Current.CancellationToken);

        var response = await playerClient.GetAsync("/Contacts/Index", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        content.Should().NotContain("Never Visible To Player");
    }

    // (7) The toggle is per-group and session-scoped: toggling ON for group 1 does not
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

    // (8) Any group member (Player) can POST AddNote, EditNote, and DeleteNote on a note
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

    // Proves the Create POST action -- not just Edit -- persists a crop submitted alongside
    // the original at creation time, closing the gap where a brand-new contact's crop was
    // silently discarded.
    [Fact]
    public async Task Create_WithCroppedPhoto_PersistsCroppedImage()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (dmClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "contact_create_with_crop", "contact_create_with_crop@example.com", roles: ["DungeonMaster"]);

        byte[] originalBytes = [5, 6, 7, 8];
        byte[] submittedCropBytes = [10, 20, 30, 40, 50];

        using var formContent = new MultipartFormDataContent
        {
            { new StringContent("Brand New Contact With A Crop"), "Name" },
            { new StringContent("Waterdeep"), "TownCity" },
            { new StringContent(""), "SubLocation" },
            { new StringContent(""), "Description" }
        };
        var originalFileContent = new ByteArrayContent(originalBytes);
        originalFileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        formContent.Add(originalFileContent, "ContactImageFile", "new.png");

        var croppedFileContent = new ByteArrayContent(submittedCropBytes);
        croppedFileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        formContent.Add(croppedFileContent, "CroppedPictureFile", "new-cropped.png");

        var response = await dmClient.PostAsync(
            "/Contacts/Create", formContent, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.Should().NotContain("AccessDenied");

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
        var persistedContact = context.Contacts.IgnoreQueryFilters()
            .FirstOrDefault(c => c.Name == "Brand New Contact With A Crop");
        persistedContact.Should().NotBeNull();

        var persistedImage = await context.Set<ContactImageEntity>().FindAsync(
            [persistedContact!.Id], TestContext.Current.CancellationToken);
        persistedImage.Should().NotBeNull();
        persistedImage!.CroppedImageData.Should().NotBeNull();
        persistedImage.CroppedImageData.Should().Equal(submittedCropBytes);
        persistedImage.OriginalImageData.Should().Equal(originalBytes);
    }

    // Proves the boolean has-image gate (HasContactImage, projected without eager-loading the
    // byte[] columns) actually drives the Index list rendering end-to-end.
    [Fact]
    public async Task Index_ContactWithImage_RendersPortraitEndpoint()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (dmClient, dmUser) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "contact_dm_indeximage", "contact_dm_indeximage@example.com", roles: ["DungeonMaster"]);
        var contact = await TestDataHelper.CreateTestContactAsync(
            factory.Services, dmUser.Id, "Contact With Portrait", groupId: 1, isRevealed: true,
            imageData: [1, 2, 3, 4]);

        var response = await dmClient.GetAsync("/Contacts/Index", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        content.Should().Contain($"GetCroppedContactImage/{contact.Id}");
    }

    [Fact]
    public async Task Index_ContactWithoutImage_DoesNotRenderPortraitEndpoint()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (dmClient, dmUser) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "contact_dm_indexnoimage", "contact_dm_indexnoimage@example.com", roles: ["DungeonMaster"]);
        var contact = await TestDataHelper.CreateTestContactAsync(
            factory.Services, dmUser.Id, "Contact Without Portrait", groupId: 1, isRevealed: true);

        var response = await dmClient.GetAsync("/Contacts/Index", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        content.Should().NotContain($"GetCroppedContactImage/{contact.Id}");
    }

    // Companion to Create_WithCroppedPhoto_PersistsCroppedImage — proves the original bytes
    // land on the Domain model via the Task 2 local-variable staging fix, not just the crop.
    [Fact]
    public async Task Create_WithPhoto_PersistsOriginalImage()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (dmClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "contact_create_original_persists", "contact_create_original_persists@example.com", roles: ["DungeonMaster"]);

        byte[] originalBytes = [11, 22, 33, 44];

        using var formContent = new MultipartFormDataContent
        {
            { new StringContent("Brand New Contact With Original Photo"), "Name" },
            { new StringContent("Waterdeep"), "TownCity" },
            { new StringContent(""), "SubLocation" },
            { new StringContent(""), "Description" }
        };
        var originalFileContent = new ByteArrayContent(originalBytes);
        originalFileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        formContent.Add(originalFileContent, "ContactImageFile", "new.png");

        var response = await dmClient.PostAsync(
            "/Contacts/Create", formContent, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.Should().NotContain("AccessDenied");

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
        var persistedContact = context.Contacts.IgnoreQueryFilters()
            .FirstOrDefault(c => c.Name == "Brand New Contact With Original Photo");
        persistedContact.Should().NotBeNull();

        var persistedImage = await context.Set<ContactImageEntity>().FindAsync(
            [persistedContact!.Id], TestContext.Current.CancellationToken);
        persistedImage.Should().NotBeNull();
        persistedImage!.OriginalImageData.Should().Equal(originalBytes);
    }

    // Proves a crop-only submission (re-cropping the stored original without re-uploading a new
    // ContactImageFile) is read, validated, and persisted by the controller, and the stored
    // original survives untouched.
    [Fact]
    public async Task Edit_CropOnlyNoNewOriginal_PersistsCropAndPreservesOriginal()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (dmClient, dmUser) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "contact_recrop_only", "contact_recrop_only@example.com", roles: ["DungeonMaster"]);
        var contact = await TestDataHelper.CreateTestContactAsync(
            factory.Services, dmUser.Id, "Contact Getting Re-Cropped", groupId: 1);

        byte[] originalBytes = [1, 2, 3, 4];
        byte[] staleCroppedBytes = [9, 9, 9, 9];
        byte[] newCropBytes = [200, 201, 202, 203];

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
            { new StringContent("Contact Getting Re-Cropped"), "Name" },
            { new StringContent("Waterdeep"), "TownCity" },
            { new StringContent(""), "SubLocation" },
            { new StringContent(""), "Description" }
        };
        // No ContactImageFile part -- this is the distinguishing property of a crop-only save.
        var croppedFileContent = new ByteArrayContent(newCropBytes);
        croppedFileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        formContent.Add(croppedFileContent, "CroppedPictureFile", "re-cropped.png");

        var response = await dmClient.PostAsync(
            $"/Contacts/Edit/{contact.Id}", formContent, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.Should().NotContain("AccessDenied");

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
        var persistedImage = await context.Set<ContactImageEntity>().FindAsync(
            [contact.Id], TestContext.Current.CancellationToken);
        persistedImage.Should().NotBeNull();
        persistedImage!.CroppedImageData.Should().Equal(newCropBytes);
        persistedImage.OriginalImageData.Should().Equal(originalBytes);
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

    // A SuperAdmin has no active group by design. RequireActiveGroupId() previously threw
    // unguarded when the Create POST write-stamp ran for a SuperAdmin with no board selected —
    // this pins down that it now degrades gracefully instead of an unhandled 500.
    [Fact]
    public async Task Create_Post_SuperAdminWithNoActiveGroup_DoesNotThrow()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (superAdminClient, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(factory);

        factory.TestGroupContext.ActiveGroupId = null;
        try
        {
            var formContent = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Name"] = "No Board Contact"
            });

            var response = await superAdminClient.PostAsync("/Contacts/Create", formContent, TestContext.Current.CancellationToken);

            // A non-idempotent request with no active group never gets silently redirected
            // (that would drop the submitted body) — GroupSessionMiddleware returns 409 Conflict,
            // and if that gate is ever bypassed, ContactsController's own write-stamp guard
            // redirects instead of throwing. Either way, it must never be a 500.
            response.StatusCode.Should().NotBe(HttpStatusCode.InternalServerError);
            response.StatusCode.Should().BeOneOf(HttpStatusCode.Conflict, HttpStatusCode.Redirect, HttpStatusCode.Found);
        }
        finally
        {
            factory.TestGroupContext.ActiveGroupId = 1;
        }
    }

    // (10) Category grouping on the index: a heading never renders for a category the viewer
    // cannot see anything under, headings follow the DM's sort order with Ungrouped last, and a
    // board with no categories renders exactly today's flat list.

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

    // Adds the acting user to a second board's membership through a direct row insert, mirroring
    // ToggleShowHidden_IsScopedPerGroup_DoesNotLeakAcrossGroups's own inline pattern above -- the
    // acting DM must be a real member of the board this suite flips into active, not merely
    // authenticated against the default board.
    private static async Task AddUserToGroupAsync(IServiceProvider services, int userId, int groupId)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
        context.UserGroups.Add(new UserGroupEntity
        {
            UserId = userId,
            GroupId = groupId,
            GroupRole = (int)QuestBoard.Domain.Enums.GroupRole.DungeonMaster
        });
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task ContactCategory_EmptyHeadingSuppression_PlayerNeverSeesHeadingForUnrevealedOnlyCategory()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (dmClient, dmUser) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "cat_suppress_dm", "cat_suppress_dm@example.com", roles: ["DungeonMaster"]);
        var category = await TestDataHelper.CreateTestContactCategoryAsync(factory.Services, "Secret Cabal", sortOrder: 0, groupId: 1);
        var contact = await TestDataHelper.CreateTestContactAsync(
            factory.Services, dmUser.Id, "Hidden Cabalist", groupId: 1, isRevealed: false);
        await AssignContactCategoryAsync(factory.Services, contact.Id, category.Id);

        var (playerClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "cat_suppress_player", "cat_suppress_player@example.com", roles: ["Player"]);

        var response = await playerClient.GetAsync("/Contacts/Index", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        content.Should().NotContain("Secret Cabal");
    }

    [Fact]
    public async Task ContactCategory_EmptyHeadingSuppression_DmWithHiddenToggleOnSeesHeading()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (_, dmUser) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "cat_suppress_toggleon_dm", "cat_suppress_toggleon_dm@example.com", roles: ["DungeonMaster"]);
        var category = await TestDataHelper.CreateTestContactCategoryAsync(factory.Services, "Whispering Court", sortOrder: 0, groupId: 1);
        var contact = await TestDataHelper.CreateTestContactAsync(
            factory.Services, dmUser.Id, "Hidden Courtier", groupId: 1, isRevealed: false);
        await AssignContactCategoryAsync(factory.Services, contact.Id, category.Id);

        var (otherDmClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "cat_suppress_toggleon_other", "cat_suppress_toggleon_other@example.com", roles: ["DungeonMaster"]);

        var toggleResponse = await otherDmClient.PostAsync(
            "/Contacts/ToggleShowHidden", new FormUrlEncodedContent([]), TestContext.Current.CancellationToken);
        toggleResponse.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.OK);

        var response = await otherDmClient.GetAsync("/Contacts/Index", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        content.Should().Contain("Whispering Court");
    }

    [Fact]
    public async Task ContactCategory_EmptyHeadingSuppression_DmWithHiddenToggleOffDoesNotSeeHeading()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (_, dmUser) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "cat_suppress_toggleoff_dm", "cat_suppress_toggleoff_dm@example.com", roles: ["DungeonMaster"]);
        var category = await TestDataHelper.CreateTestContactCategoryAsync(factory.Services, "Shadow Conclave", sortOrder: 0, groupId: 1);
        var contact = await TestDataHelper.CreateTestContactAsync(
            factory.Services, dmUser.Id, "Hidden Shadow Agent", groupId: 1, isRevealed: false);
        await AssignContactCategoryAsync(factory.Services, contact.Id, category.Id);

        var (otherDmClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "cat_suppress_toggleoff_other", "cat_suppress_toggleoff_other@example.com", roles: ["DungeonMaster"]);

        var response = await otherDmClient.GetAsync("/Contacts/Index", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        content.Should().NotContain("Shadow Conclave");
    }

    [Fact]
    public async Task ContactsIndex_CategoryOrdering_FollowsSortPositionNotAlphabet()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (dmClient, dmUser) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "cat_order_sort_dm", "cat_order_sort_dm@example.com", roles: ["DungeonMaster"]);

        // Sort position deliberately reversed from alphabetical order: "Zenith Guild" is first by
        // SortOrder even though "Alley Cats" comes first alphabetically.
        var zenith = await TestDataHelper.CreateTestContactCategoryAsync(factory.Services, "Zenith Guild", sortOrder: 0, groupId: 1);
        var alley = await TestDataHelper.CreateTestContactCategoryAsync(factory.Services, "Alley Cats", sortOrder: 1, groupId: 1);
        var zenithContact = await TestDataHelper.CreateTestContactAsync(
            factory.Services, dmUser.Id, "Zenith Member", groupId: 1, isRevealed: true);
        await AssignContactCategoryAsync(factory.Services, zenithContact.Id, zenith.Id);
        var alleyContact = await TestDataHelper.CreateTestContactAsync(
            factory.Services, dmUser.Id, "Alley Member", groupId: 1, isRevealed: true);
        await AssignContactCategoryAsync(factory.Services, alleyContact.Id, alley.Id);

        var response = await dmClient.GetAsync("/Contacts/Index", TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        var zenithIndex = content.IndexOf("Zenith Guild", StringComparison.Ordinal);
        var alleyIndex = content.IndexOf("Alley Cats", StringComparison.Ordinal);
        zenithIndex.Should().BeGreaterThan(-1);
        alleyIndex.Should().BeGreaterThan(-1);
        zenithIndex.Should().BeLessThan(alleyIndex);
    }

    [Fact]
    public async Task ContactsIndex_CategoryOrdering_ContactsWithinCategoryAreAlphabetical()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (dmClient, dmUser) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "cat_order_alpha_dm", "cat_order_alpha_dm@example.com", roles: ["DungeonMaster"]);

        var category = await TestDataHelper.CreateTestContactCategoryAsync(factory.Services, "Adventuring Party", sortOrder: 0, groupId: 1);
        var zed = await TestDataHelper.CreateTestContactAsync(
            factory.Services, dmUser.Id, "Zed the Wanderer", groupId: 1, isRevealed: true);
        await AssignContactCategoryAsync(factory.Services, zed.Id, category.Id);
        var anna = await TestDataHelper.CreateTestContactAsync(
            factory.Services, dmUser.Id, "Anna the Scout", groupId: 1, isRevealed: true);
        await AssignContactCategoryAsync(factory.Services, anna.Id, category.Id);

        var response = await dmClient.GetAsync("/Contacts/Index", TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        var annaIndex = content.IndexOf("Anna the Scout", StringComparison.Ordinal);
        var zedIndex = content.IndexOf("Zed the Wanderer", StringComparison.Ordinal);
        annaIndex.Should().BeGreaterThan(-1);
        zedIndex.Should().BeGreaterThan(-1);
        annaIndex.Should().BeLessThan(zedIndex);
    }

    [Fact]
    public async Task ContactsIndex_CategoryOrdering_UngroupedHeadingAppearsAfterEveryRealCategory()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (dmClient, dmUser) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "cat_order_ungrouped_dm", "cat_order_ungrouped_dm@example.com", roles: ["DungeonMaster"]);

        var categoryOne = await TestDataHelper.CreateTestContactCategoryAsync(factory.Services, "Merchant Guild", sortOrder: 0, groupId: 1);
        var categoryTwo = await TestDataHelper.CreateTestContactCategoryAsync(factory.Services, "Thieves Union", sortOrder: 1, groupId: 1);
        var catOneContact = await TestDataHelper.CreateTestContactAsync(
            factory.Services, dmUser.Id, "Merchant Member", groupId: 1, isRevealed: true);
        await AssignContactCategoryAsync(factory.Services, catOneContact.Id, categoryOne.Id);
        var catTwoContact = await TestDataHelper.CreateTestContactAsync(
            factory.Services, dmUser.Id, "Thief Member", groupId: 1, isRevealed: true);
        await AssignContactCategoryAsync(factory.Services, catTwoContact.Id, categoryTwo.Id);
        // Uncategorised on purpose -- no AssignContactCategoryAsync call for this one.
        await TestDataHelper.CreateTestContactAsync(
            factory.Services, dmUser.Id, "Unaffiliated Wanderer", groupId: 1, isRevealed: true);

        var response = await dmClient.GetAsync("/Contacts/Index", TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        var categoryOneIndex = content.IndexOf("Merchant Guild", StringComparison.Ordinal);
        var categoryTwoIndex = content.IndexOf("Thieves Union", StringComparison.Ordinal);
        var ungroupedIndex = content.IndexOf("Ungrouped", StringComparison.Ordinal);
        categoryOneIndex.Should().BeGreaterThan(-1);
        categoryTwoIndex.Should().BeGreaterThan(-1);
        ungroupedIndex.Should().BeGreaterThan(-1);
        ungroupedIndex.Should().BeGreaterThan(categoryOneIndex);
        ungroupedIndex.Should().BeGreaterThan(categoryTwoIndex);

        // No heading carries a count: a real category name is never followed by a parenthesised
        // number the way a badge or total would render it.
        content.Should().NotMatchRegex(@"Merchant Guild\s*\(\d+\)");
        content.Should().NotMatchRegex(@"Thieves Union\s*\(\d+\)");
    }

    [Fact]
    public async Task ContactsIndex_CategoryOrdering_ZeroCategoryBoardRendersFlatListWithNoHeadings()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (dmClient, dmUser) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "cat_order_flat_dm", "cat_order_flat_dm@example.com", roles: ["DungeonMaster"]);
        await TestDataHelper.CreateTestContactAsync(
            factory.Services, dmUser.Id, "Flat List Contact", groupId: 1, isRevealed: true);

        var response = await dmClient.GetAsync("/Contacts/Index", TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        content.Should().Contain("Flat List Contact");
        content.Should().NotContain("Ungrouped");
        content.Should().NotContain("category-heading");
    }

    [Fact]
    public async Task ContactCategory_NameRendersEscaped_AngleBracketsAreEncoded()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (dmClient, dmUser) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "cat_escape_dm", "cat_escape_dm@example.com", roles: ["DungeonMaster"]);

        var category = await TestDataHelper.CreateTestContactCategoryAsync(
            factory.Services, "<script>alert('x')</script>", sortOrder: 0, groupId: 1);
        var contact = await TestDataHelper.CreateTestContactAsync(
            factory.Services, dmUser.Id, "Marked Contact", groupId: 1, isRevealed: true);
        await AssignContactCategoryAsync(factory.Services, contact.Id, category.Id);

        var response = await dmClient.GetAsync("/Contacts/Index", TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        content.Should().NotContain("<script>alert('x')</script>");
        content.Should().Contain("&lt;script&gt;");
    }

    // (11) Cross-board isolation on the category-assignment surfaces this plan adds: another
    // board's categories are unreachable from the management page, the contact form dropdown, and
    // the index; a post naming a foreign category is refused with the stored reference left
    // untouched either way; and a null active board fails closed rather than merging every
    // board's categories together.

    [Fact]
    public async Task ContactCategory_CrossGroup_ManagementListNeverShowsOtherBoardsCategory()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        await TestDataHelper.SeedCampaignGroupAsync(factory.Services, 2);
        await TestDataHelper.CreateTestContactCategoryAsync(factory.Services, "Board One Exclusive Circle", sortOrder: 0, groupId: 1);
        await TestDataHelper.CreateTestContactCategoryAsync(factory.Services, "Board Two Native Circle", sortOrder: 0, groupId: 2);

        var (dmClient, dmUser) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "cat_crossgroup_mgmt_dm", "cat_crossgroup_mgmt_dm@example.com", roles: ["DungeonMaster"]);
        await AddUserToGroupAsync(factory.Services, dmUser.Id, 2);

        try
        {
            factory.TestGroupContext.ActiveGroupId = 2;
            var response = await dmClient.GetAsync("/ContactCategoryManagement", TestContext.Current.CancellationToken);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            content.Should().NotContain("Board One Exclusive Circle");
            content.Should().Contain("Board Two Native Circle");
        }
        finally
        {
            factory.TestGroupContext.ActiveGroupId = 1;
        }
    }

    [Fact]
    public async Task ContactCategory_CrossGroup_CreateFormDropdownNeverShowsOtherBoardsCategory()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        await TestDataHelper.SeedCampaignGroupAsync(factory.Services, 2);
        await TestDataHelper.CreateTestContactCategoryAsync(factory.Services, "Board One Only Roster", sortOrder: 0, groupId: 1);
        await TestDataHelper.CreateTestContactCategoryAsync(factory.Services, "Board Two Own Roster", sortOrder: 0, groupId: 2);

        var (dmClient, dmUser) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "cat_crossgroup_form_dm", "cat_crossgroup_form_dm@example.com", roles: ["DungeonMaster"]);
        await AddUserToGroupAsync(factory.Services, dmUser.Id, 2);

        try
        {
            factory.TestGroupContext.ActiveGroupId = 2;
            var response = await dmClient.GetAsync("/Contacts/Create", TestContext.Current.CancellationToken);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            content.Should().NotContain("Board One Only Roster");
            content.Should().Contain("Board Two Own Roster");
        }
        finally
        {
            factory.TestGroupContext.ActiveGroupId = 1;
        }
    }

    [Fact]
    public async Task ContactCategory_CrossGroup_IndexNeverShowsOtherBoardsCategory()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        await TestDataHelper.SeedCampaignGroupAsync(factory.Services, 2);
        await TestDataHelper.CreateTestContactCategoryAsync(factory.Services, "Board One Index Cabal", sortOrder: 0, groupId: 1);
        var boardTwoCategory = await TestDataHelper.CreateTestContactCategoryAsync(factory.Services, "Board Two Index Cabal", sortOrder: 0, groupId: 2);

        var (dmClient, dmUser) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "cat_crossgroup_index_dm", "cat_crossgroup_index_dm@example.com", roles: ["DungeonMaster"]);
        await AddUserToGroupAsync(factory.Services, dmUser.Id, 2);

        var boardTwoContact = await TestDataHelper.CreateTestContactAsync(
            factory.Services, dmUser.Id, "Board Two Cabal Member", groupId: 2, isRevealed: true);

        try
        {
            // AssignContactCategoryAsync reads through the DI-registered, board-filtered
            // Contacts DbSet -- the active board must already be 2 before it can find a
            // board-2 contact.
            factory.TestGroupContext.ActiveGroupId = 2;
            await AssignContactCategoryAsync(factory.Services, boardTwoContact.Id, boardTwoCategory.Id);

            var response = await dmClient.GetAsync("/Contacts/Index", TestContext.Current.CancellationToken);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            content.Should().NotContain("Board One Index Cabal");
            content.Should().Contain("Board Two Index Cabal");
        }
        finally
        {
            factory.TestGroupContext.ActiveGroupId = 1;
        }
    }

    [Fact]
    public async Task ContactCategory_CrossGroup_CreatePost_ForeignCategoryId_IsRefusedAndNotStored()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        await TestDataHelper.SeedCampaignGroupAsync(factory.Services, 2);
        var boardOneCategory = await TestDataHelper.CreateTestContactCategoryAsync(factory.Services, "Board One Create Refusal Guild", sortOrder: 0, groupId: 1);

        var (dmClient, dmUser) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "cat_crossgroup_create_dm", "cat_crossgroup_create_dm@example.com", roles: ["DungeonMaster"]);
        await AddUserToGroupAsync(factory.Services, dmUser.Id, 2);

        try
        {
            factory.TestGroupContext.ActiveGroupId = 2;

            var formContent = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Name"] = "Should Not Persist Cross-Board Contact",
                ["CategoryId"] = boardOneCategory.Id.ToString()
            });

            var response = await dmClient.PostAsync("/Contacts/Create", formContent, TestContext.Current.CancellationToken);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            content.Should().Contain("Selected category is not available on this board.");

            await using var ctx = factory.Database.CreateContext();
            var persisted = await ctx.Contacts.IgnoreQueryFilters()
                .FirstOrDefaultAsync(c => c.Name == "Should Not Persist Cross-Board Contact", TestContext.Current.CancellationToken);
            persisted.Should().BeNull();
        }
        finally
        {
            factory.TestGroupContext.ActiveGroupId = 1;
        }
    }

    [Fact]
    public async Task ContactCategory_CrossGroup_EditPost_ForeignCategoryId_IsRefusedAndReferenceUnchanged()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        await TestDataHelper.SeedCampaignGroupAsync(factory.Services, 2);
        var boardOneCategory = await TestDataHelper.CreateTestContactCategoryAsync(factory.Services, "Board One Edit Target Guild", sortOrder: 0, groupId: 1);
        var boardTwoCategory = await TestDataHelper.CreateTestContactCategoryAsync(factory.Services, "Board Two Edit Home Guild", sortOrder: 0, groupId: 2);

        var (dmClient, dmUser) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "cat_crossgroup_edit_dm", "cat_crossgroup_edit_dm@example.com", roles: ["DungeonMaster"]);
        await AddUserToGroupAsync(factory.Services, dmUser.Id, 2);

        var contact = await TestDataHelper.CreateTestContactAsync(
            factory.Services, dmUser.Id, "Board Two Contact Under Edit", groupId: 2, isRevealed: true);

        try
        {
            // AssignContactCategoryAsync reads through the DI-registered, board-filtered
            // Contacts DbSet -- the active board must already be 2 before it can find a
            // board-2 contact.
            factory.TestGroupContext.ActiveGroupId = 2;
            await AssignContactCategoryAsync(factory.Services, contact.Id, boardTwoCategory.Id);

            var formContent = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Id"] = contact.Id.ToString(),
                ["Name"] = "Board Two Contact Under Edit",
                ["CategoryId"] = boardOneCategory.Id.ToString()
            });

            var response = await dmClient.PostAsync($"/Contacts/Edit/{contact.Id}", formContent, TestContext.Current.CancellationToken);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            content.Should().Contain("Selected category is not available on this board.");

            await using var ctx = factory.Database.CreateContext();
            var persisted = await ctx.Contacts.IgnoreQueryFilters()
                .FirstOrDefaultAsync(c => c.Id == contact.Id, TestContext.Current.CancellationToken);
            persisted.Should().NotBeNull();
            persisted!.CategoryId.Should().Be(boardTwoCategory.Id);
        }
        finally
        {
            factory.TestGroupContext.ActiveGroupId = 1;
        }
    }

    [Fact]
    public async Task ContactCategory_CrossGroup_NullActiveBoard_CategoryReadResolvesToNothing()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        await TestDataHelper.SeedCampaignGroupAsync(factory.Services, 2);
        await TestDataHelper.CreateTestContactCategoryAsync(factory.Services, "Board One Null Check Guild", sortOrder: 0, groupId: 1);
        await TestDataHelper.CreateTestContactCategoryAsync(factory.Services, "Board Two Null Check Guild", sortOrder: 0, groupId: 2);

        // TestDatabase.CreateContext() builds its own MutableGroupContext with ActiveGroupId fixed
        // at null, independent of the DI-registered TestGroupContext -- this exercises the
        // fail-closed HasQueryFilter predicate on ContactCategoryEntity directly, the same idiom
        // TenantIsolationTests already uses for QuestEntity's own null-active-group fact.
        await using var readCtx = factory.Database.CreateContext();
        var categories = readCtx.ContactCategories.ToList();

        categories.Should().BeEmpty(
            "a null active board must resolve to zero categories, never every board's merged together");
    }

    // (12) Disabled-select form state: a board with zero categories renders the field disabled
    // with the discovery link; a board with at least one category renders it enabled with no
    // helper link.

    [Fact]
    public async Task ContactCategory_DisabledSelect_ZeroCategoryBoard_CreateFormShowsDisabledSelectAndManagementLink()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (dmClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "cat_disabled_zero_dm", "cat_disabled_zero_dm@example.com", roles: ["DungeonMaster"]);

        var response = await dmClient.GetAsync("/Contacts/Create", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        content.Should().Contain("disabled");
        content.Should().Contain("ContactCategoryManagement");
        content.Should().Contain("No categories yet.");
    }

    [Fact]
    public async Task ContactCategory_DisabledSelect_WithCategoryBoard_CreateFormShowsEnabledSelectNoHelperLink()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        await TestDataHelper.CreateTestContactCategoryAsync(factory.Services, "Available Adventuring Company", sortOrder: 0, groupId: 1);

        var (dmClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "cat_disabled_present_dm", "cat_disabled_present_dm@example.com", roles: ["DungeonMaster"]);

        var response = await dmClient.GetAsync("/Contacts/Create", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        content.Should().Contain("Available Adventuring Company");
        content.Should().NotContain("No categories yet.");
    }

    // Tag filter semantics on the index read path: narrowing by one tag, union across two
    // selected tags, the visibility gate winning over a matching tag on an unrevealed contact,
    // silent handling of unknown and other-board tag ids, the Show Hidden round trip, and the
    // player-tier no-op.

    [Fact]
    public async Task Index_SingleSelectedTag_ReturnsOnlyMatchingContacts()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (dmClient, dmUser) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "contact_tag_single_dm", "contact_tag_single_dm@example.com", roles: ["DungeonMaster"]);

        var tagged = await TestDataHelper.CreateTestContactAsync(
            factory.Services, dmUser.Id, "Tagged Contact Alone", groupId: 1, isRevealed: true);
        var untagged = await TestDataHelper.CreateTestContactAsync(
            factory.Services, dmUser.Id, "Untagged Contact Alone", groupId: 1, isRevealed: true);
        var tag = await TestDataHelper.CreateTestContactTagAsync(
            factory.Services, "Alone Tag", groupId: 1, tagged.Id);

        var response = await dmClient.GetAsync($"/Contacts/Index?tag={tag.Id}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        content.Should().Contain("Tagged Contact Alone");
        content.Should().NotContain("Untagged Contact Alone");
    }

    [Fact]
    public async Task Index_TwoSelectedTags_ReturnsUnionNotIntersection()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (dmClient, dmUser) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "contact_tag_union_dm", "contact_tag_union_dm@example.com", roles: ["DungeonMaster"]);

        var firstTagged = await TestDataHelper.CreateTestContactAsync(
            factory.Services, dmUser.Id, "First Tag Holder", groupId: 1, isRevealed: true);
        var secondTagged = await TestDataHelper.CreateTestContactAsync(
            factory.Services, dmUser.Id, "Second Tag Holder", groupId: 1, isRevealed: true);
        var neitherTagged = await TestDataHelper.CreateTestContactAsync(
            factory.Services, dmUser.Id, "Neither Tag Holder", groupId: 1, isRevealed: true);
        var firstTag = await TestDataHelper.CreateTestContactTagAsync(
            factory.Services, "First Union Tag", groupId: 1, firstTagged.Id);
        var secondTag = await TestDataHelper.CreateTestContactTagAsync(
            factory.Services, "Second Union Tag", groupId: 1, secondTagged.Id);

        var response = await dmClient.GetAsync(
            $"/Contacts/Index?tag={firstTag.Id}&tag={secondTag.Id}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        content.Should().Contain("First Tag Holder");
        content.Should().Contain("Second Tag Holder");
        content.Should().NotContain("Neither Tag Holder");
    }

    [Fact]
    public async Task Index_SelectedTagOnUnrevealedContact_StaysHiddenWhileShowHiddenIsOff()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (creatorClient, creator) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "contact_tag_hidden_creator", "contact_tag_hidden_creator@example.com", roles: ["DungeonMaster"]);
        var hiddenContact = await TestDataHelper.CreateTestContactAsync(
            factory.Services, creator.Id, "Hidden Tagged Contact", groupId: 1, isRevealed: false);
        var tag = await TestDataHelper.CreateTestContactTagAsync(
            factory.Services, "Hidden Contact Tag", groupId: 1, hiddenContact.Id);

        var (otherDmClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "contact_tag_hidden_otherdm", "contact_tag_hidden_otherdm@example.com", roles: ["DungeonMaster"]);

        // Toggle OFF (default): the matching tag cannot pull the unrevealed contact past the
        // visibility gate.
        var beforeToggle = await otherDmClient.GetAsync(
            $"/Contacts/Index?tag={tag.Id}", TestContext.Current.CancellationToken);
        beforeToggle.StatusCode.Should().Be(HttpStatusCode.OK);
        var beforeContent = await beforeToggle.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        beforeContent.Should().NotContain("Hidden Tagged Contact");

        var toggleResponse = await otherDmClient.PostAsync(
            "/Contacts/ToggleShowHidden", new FormUrlEncodedContent([]), TestContext.Current.CancellationToken);
        toggleResponse.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.OK);

        // Toggle ON: the same filtered request now surfaces it, once the visibility gate lets
        // it through.
        var afterToggle = await otherDmClient.GetAsync(
            $"/Contacts/Index?tag={tag.Id}", TestContext.Current.CancellationToken);
        afterToggle.StatusCode.Should().Be(HttpStatusCode.OK);
        var afterContent = await afterToggle.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        afterContent.Should().Contain("Hidden Tagged Contact");
    }

    [Fact]
    public async Task Index_UnknownTagId_ReturnsFullVisibleListWithoutError()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (dmClient, dmUser) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "contact_tag_unknown_dm", "contact_tag_unknown_dm@example.com", roles: ["DungeonMaster"]);
        await TestDataHelper.CreateTestContactAsync(
            factory.Services, dmUser.Id, "First Visible Contact", groupId: 1, isRevealed: true);
        await TestDataHelper.CreateTestContactAsync(
            factory.Services, dmUser.Id, "Second Visible Contact", groupId: 1, isRevealed: true);

        var response = await dmClient.GetAsync("/Contacts/Index?tag=999999", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        content.Should().Contain("First Visible Contact");
        content.Should().Contain("Second Visible Contact");
    }

    [Fact]
    public async Task Index_TagIdFromAnotherBoard_ReturnsOwnBoardListOnly()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        await TestDataHelper.SeedCampaignGroupAsync(factory.Services, 2);

        var (dmClient, dmUser) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "contact_tag_otherboard_dm", "contact_tag_otherboard_dm@example.com", roles: ["DungeonMaster"]);
        await TestDataHelper.CreateTestContactAsync(
            factory.Services, dmUser.Id, "Own Board Contact", groupId: 1, isRevealed: true);

        var otherBoardOwner = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "contact_tag_otherboard_owner", "contact_tag_otherboard_owner@example.com", "Test123!", "Other Board Owner");
        var otherBoardContact = await TestDataHelper.CreateTestContactAsync(
            factory.Services, otherBoardOwner.Id, "Other Board's Contact", groupId: 2, isRevealed: true);

        // ContactEntity's own board-scoped query filter applies to the helper's lookup too, so
        // creating the tag on the other board's contact needs the active group switched there
        // first, exactly as ToggleShowHidden_IsScopedPerGroup does above.
        ContactTagEntity otherBoardTag;
        try
        {
            factory.TestGroupContext.ActiveGroupId = 2;
            otherBoardTag = await TestDataHelper.CreateTestContactTagAsync(
                factory.Services, "Other Board Tag", groupId: 2, otherBoardContact.Id);
        }
        finally
        {
            factory.TestGroupContext.ActiveGroupId = 1;
        }

        var response = await dmClient.GetAsync(
            $"/Contacts/Index?tag={otherBoardTag.Id}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        content.Should().Contain("Own Board Contact");
        content.Should().NotContain("Other Board's Contact");
        content.Should().NotContain("Other Board Tag");
    }

    [Fact]
    public async Task Index_PlayerTierWithTagId_IgnoresTheFilter()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (dmClient, dmUser) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "contact_tag_player_dm", "contact_tag_player_dm@example.com", roles: ["DungeonMaster"]);
        var tagged = await TestDataHelper.CreateTestContactAsync(
            factory.Services, dmUser.Id, "Player View Tagged Contact", groupId: 1, isRevealed: true);
        await TestDataHelper.CreateTestContactAsync(
            factory.Services, dmUser.Id, "Player View Untagged Contact", groupId: 1, isRevealed: true);
        var tag = await TestDataHelper.CreateTestContactTagAsync(
            factory.Services, "Player Ignored Tag", groupId: 1, tagged.Id);

        var (playerClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "contact_tag_player", "contact_tag_player@example.com", roles: ["Player"]);

        var withoutTag = await playerClient.GetAsync("/Contacts/Index", TestContext.Current.CancellationToken);
        withoutTag.StatusCode.Should().Be(HttpStatusCode.OK);
        var withoutTagContent = await withoutTag.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        var withTag = await playerClient.GetAsync(
            $"/Contacts/Index?tag={tag.Id}", TestContext.Current.CancellationToken);
        withTag.StatusCode.Should().Be(HttpStatusCode.OK);
        var withTagContent = await withTag.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // A tag parameter changes nothing for a player-tier viewer: both contacts appear either
        // way, and the two response bodies carry the identical set of contact names.
        withoutTagContent.Should().Contain("Player View Tagged Contact");
        withoutTagContent.Should().Contain("Player View Untagged Contact");
        withTagContent.Should().Contain("Player View Tagged Contact");
        withTagContent.Should().Contain("Player View Untagged Contact");
    }

    [Fact]
    public async Task ToggleShowHidden_WithSelectedTags_RedirectPreservesThem()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (dmClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "contact_toggle_tags_dm", "contact_toggle_tags_dm@example.com", roles: ["DungeonMaster"]);

        var formContent = new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("tag", "5"),
            new KeyValuePair<string, string>("tag", "7")
        ]);

        var response = await dmClient.PostAsync(
            "/Contacts/ToggleShowHidden", formContent, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        var location = response.Headers.Location!.OriginalString;
        location.Should().Contain("tag=5");
        location.Should().Contain("tag=7");
    }

    // Write-path coverage for the tag persistence added on Create/Edit: the no-script comma
    // path, case-insensitive reuse and de-duplication, pruning through the form, the over-long
    // rejection, and the cross-board write refusal. Every outcome is asserted against
    // QuestBoardContext -- the surviving ContactTags rows, their ids, and the contact's own
    // Tags collection -- rather than inferred from the rendered page.

    [Fact]
    public async Task Create_CommaSeparatedTags_AttachesBothTags()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (dmClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "contact_tag_create_comma", "contact_tag_create_comma@example.com", roles: ["DungeonMaster"]);

        var formContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Name"] = "Comma Tagged Contact",
            ["TagsInput"] = "shopkeeper, quest giver"
        });

        var response = await dmClient.PostAsync("/Contacts/Create", formContent, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.Should().NotContain("AccessDenied");

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
        var persisted = await context.Contacts
            .Include(c => c.Tags)
            .SingleAsync(c => c.Name == "Comma Tagged Contact", TestContext.Current.CancellationToken);

        persisted.Tags.Select(t => t.Name).Should().BeEquivalentTo(["shopkeeper", "quest giver"]);
    }

    [Fact]
    public async Task Edit_CaseVariantOfExistingTag_ReusesExistingRow()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (dmClient, dmUser) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "contact_tag_reuse_dm", "contact_tag_reuse_dm@example.com", roles: ["DungeonMaster"]);

        var firstContact = await TestDataHelper.CreateTestContactAsync(
            factory.Services, dmUser.Id, "First Shopkeeper Contact", groupId: 1);
        var existingTag = await TestDataHelper.CreateTestContactTagAsync(
            factory.Services, "shopkeeper", groupId: 1, firstContact.Id);

        var secondContact = await TestDataHelper.CreateTestContactAsync(
            factory.Services, dmUser.Id, "Second Shopkeeper Contact", groupId: 1);

        var formContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Id"] = secondContact.Id.ToString(),
            ["Name"] = "Second Shopkeeper Contact",
            ["TagsInput"] = "Shopkeeper"
        });

        var response = await dmClient.PostAsync(
            $"/Contacts/Edit/{secondContact.Id}", formContent, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.Should().NotContain("AccessDenied");

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
        var persistedSecond = await context.Contacts
            .Include(c => c.Tags)
            .SingleAsync(c => c.Id == secondContact.Id, TestContext.Current.CancellationToken);

        persistedSecond.Tags.Should().ContainSingle(t => t.Id == existingTag.Id);

        var tagCount = await context.ContactTags.CountAsync(
            t => t.Name == "shopkeeper", TestContext.Current.CancellationToken);
        tagCount.Should().Be(1);
    }

    [Fact]
    public async Task Edit_RepeatedCasingsOfOneName_CreatesSingleTag()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (dmClient, dmUser) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "contact_tag_repeatcase_dm", "contact_tag_repeatcase_dm@example.com", roles: ["DungeonMaster"]);
        var contact = await TestDataHelper.CreateTestContactAsync(
            factory.Services, dmUser.Id, "Repeated Casing Contact", groupId: 1);

        var formContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Id"] = contact.Id.ToString(),
            ["Name"] = "Repeated Casing Contact",
            ["TagsInput"] = "shopkeeper, SHOPKEEPER, Shopkeeper"
        });

        var response = await dmClient.PostAsync(
            $"/Contacts/Edit/{contact.Id}", formContent, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.Should().NotContain("AccessDenied");

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
        var persisted = await context.Contacts
            .Include(c => c.Tags)
            .SingleAsync(c => c.Id == contact.Id, TestContext.Current.CancellationToken);

        persisted.Tags.Should().HaveCount(1);
        var tagCount = await context.ContactTags.CountAsync(TestContext.Current.CancellationToken);
        tagCount.Should().Be(1);
    }

    [Fact]
    public async Task Edit_EmptyTagsInput_RemovesTagsAndPrunesOrphan()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (dmClient, dmUser) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "contact_tag_prune_dm", "contact_tag_prune_dm@example.com", roles: ["DungeonMaster"]);
        var contact = await TestDataHelper.CreateTestContactAsync(
            factory.Services, dmUser.Id, "Contact Losing Its Only Tag", groupId: 1);
        var tag = await TestDataHelper.CreateTestContactTagAsync(
            factory.Services, "Orphan Bound Tag", groupId: 1, contact.Id);

        var formContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Id"] = contact.Id.ToString(),
            ["Name"] = "Contact Losing Its Only Tag",
            ["TagsInput"] = "   "
        });

        var response = await dmClient.PostAsync(
            $"/Contacts/Edit/{contact.Id}", formContent, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.Should().NotContain("AccessDenied");

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
        var persisted = await context.Contacts
            .Include(c => c.Tags)
            .SingleAsync(c => c.Id == contact.Id, TestContext.Current.CancellationToken);

        persisted.Tags.Should().BeEmpty();

        var tagStillExists = await context.ContactTags.AnyAsync(
            t => t.Id == tag.Id, TestContext.Current.CancellationToken);
        tagStillExists.Should().BeFalse();
    }

    [Fact]
    public async Task Edit_TagNameOverLengthLimit_RerendersWithValidationError()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (dmClient, dmUser) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "contact_tag_overlong_dm", "contact_tag_overlong_dm@example.com", roles: ["DungeonMaster"]);
        var contact = await TestDataHelper.CreateTestContactAsync(
            factory.Services, dmUser.Id, "Contact Keeping Its Tag", groupId: 1);
        var existingTag = await TestDataHelper.CreateTestContactTagAsync(
            factory.Services, "Untouched Tag", groupId: 1, contact.Id);

        var overLongName = new string('a', 31);
        var formContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Id"] = contact.Id.ToString(),
            ["Name"] = "Contact Keeping Its Tag",
            ["TagsInput"] = overLongName
        });

        var response = await dmClient.PostAsync(
            $"/Contacts/Edit/{contact.Id}", formContent, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        content.Should().Contain("30-character limit");

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();

        var tagCreated = await context.ContactTags.AnyAsync(
            t => t.Name == overLongName, TestContext.Current.CancellationToken);
        tagCreated.Should().BeFalse();

        var persisted = await context.Contacts
            .Include(c => c.Tags)
            .SingleAsync(c => c.Id == contact.Id, TestContext.Current.CancellationToken);
        persisted.Tags.Should().ContainSingle(t => t.Id == existingTag.Id);
    }

    [Fact]
    public async Task Edit_TagNameExistingOnlyOnAnotherBoard_CreatesOwnBoardRow()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        await TestDataHelper.SeedCampaignGroupAsync(factory.Services, 2);

        var otherBoardTag = await TestDataHelper.CreateTestContactTagAsync(
            factory.Services, "Shopkeeper", groupId: 2);

        var (dmClient, dmUser) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "contact_tag_crossboard_dm", "contact_tag_crossboard_dm@example.com", roles: ["DungeonMaster"]);
        var contact = await TestDataHelper.CreateTestContactAsync(
            factory.Services, dmUser.Id, "Cross Board Tag Target", groupId: 1);

        var formContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Id"] = contact.Id.ToString(),
            ["Name"] = "Cross Board Tag Target",
            ["TagsInput"] = "Shopkeeper"
        });

        var response = await dmClient.PostAsync(
            $"/Contacts/Edit/{contact.Id}", formContent, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.Should().NotContain("AccessDenied");

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
        var persisted = await context.Contacts
            .Include(c => c.Tags)
            .SingleAsync(c => c.Id == contact.Id, TestContext.Current.CancellationToken);

        var ownBoardTag = persisted.Tags.Should().ContainSingle(t => t.Name == "Shopkeeper").Subject;
        ownBoardTag.Id.Should().NotBe(otherBoardTag.Id);

        var otherBoardTagStillExists = await context.ContactTags
            .IgnoreQueryFilters()
            .AnyAsync(t => t.Id == otherBoardTag.Id, TestContext.Current.CancellationToken);
        otherBoardTagStillExists.Should().BeTrue();
    }
}
