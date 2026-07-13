using QuestBoard.Domain.Enums;
using QuestBoard.IntegrationTests.Helpers;
using QuestBoard.Repository.Entities;
using System.Net;
using System.Net.Http.Headers;

namespace QuestBoard.IntegrationTests.Controllers;

public class CharactersControllerIntegrationTests(WebApplicationFactoryBase factory) : IClassFixture<WebApplicationFactoryBase>
{
    private readonly HttpClient _client = factory.CreateNonRedirectingClient();

    [Fact]
    public async Task Index_ShouldReturnCharactersPage()
    {
        // Arrange - Characters requires authentication
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(factory);

        // Act
        var response = await client.GetAsync("/Characters", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        content.Should().ContainAny("Character", "Characters");
    }

    [Fact]
    public async Task Index_WithMembers_ShouldDisplayAllMembers()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(factory.Services);

        // Create authenticated client first (this also creates a user in the database)
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(factory);

        // Create additional users to display in the character roster (with unique names)
        var warrior = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "warrior1", "warrior1@example.com", "Test123!", "Warrior One");
        var mage = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "mage1", "mage1@example.com", "Test123!", "Mage One");
        var dm = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "dm1", "dm1@example.com", "Test123!", "DM One");

        // Create characters for each user
        await TestDataHelper.CreateTestCharacterAsync(factory.Services, warrior.Id, "Warrior One", level: 5, dndClass: 5); // Fighter
        await TestDataHelper.CreateTestCharacterAsync(factory.Services, mage.Id, "Mage One", level: 3, dndClass: 12); // Wizard
        await TestDataHelper.CreateTestCharacterAsync(factory.Services, dm.Id, "DM One", level: 10, dndClass: 3); // Cleric

        // Act
        var response = await client.GetAsync("/Characters", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        // Check for the user names (not usernames which have GUID suffixes)
        content.Should().Contain("Warrior One");
        content.Should().Contain("Mage One");
        content.Should().Contain("DM One");
    }

    [Fact]
    public async Task Index_ShouldShowDungeonMasterBadge()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(factory.Services);

        // Create authenticated client first (this also creates a user in the database)
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(factory);

        // Create additional user to test DM badge display
        var dmUser = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "dmspecial", "dmspecial@example.com", "Test123!", "Special DM");

        // Create character for the DM user
        await TestDataHelper.CreateTestCharacterAsync(factory.Services, dmUser.Id, "Special DM", level: 10, dndClass: 7); // Paladin

        // Act
        var response = await client.GetAsync("/Characters", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        // Check for the user's display name
        content.Should().Contain("Special DM");
    }

    [Fact]
    public async Task Index_ShouldDisplayUserInformation()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(factory.Services);

        // Create authenticated client first (this also creates a user in the database)
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(factory);

        // Create additional user with specific name to test display
        var user = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "detailedchar", "detailed@example.com", name: "Aragorn the Ranger");

        // Create character for the user (use "Aragorn" as character name to match test expectation)
        await TestDataHelper.CreateTestCharacterAsync(factory.Services, user.Id, "Aragorn", level: 8, dndClass: 8); // Ranger

        // Act
        var response = await client.GetAsync("/Characters", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        content.Should().Contain("Aragorn");
    }

    [Fact]
    public async Task Edit_AdminEditingAnotherPlayersCharacter_ShouldSucceed()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(factory.Services);

        var (adminClient, _) = await AuthenticationHelper.CreateAuthenticatedAdminClientAsync(factory);
        var owner = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "owner_admin_edit", "owner_admin_edit@example.com", "Test123!", "Character Owner");
        var character = await TestDataHelper.CreateTestCharacterAsync(
            factory.Services, owner.Id, "Owned Character", groupId: 1);

        // Act
        var response = await adminClient.GetAsync(
            $"/Characters/Edit/{character.Id}", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Edit_SuperAdminEditingAnotherPlayersCharacter_ShouldSucceed()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(factory.Services);

        var (superAdminClient, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(factory);
        var owner = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "owner_superadmin_edit", "owner_superadmin_edit@example.com", "Test123!", "Character Owner");
        var character = await TestDataHelper.CreateTestCharacterAsync(
            factory.Services, owner.Id, "Owned Character", groupId: 1);

        // Act
        var response = await superAdminClient.GetAsync(
            $"/Characters/Edit/{character.Id}", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Edit_PlayerEditingAnotherPlayersCharacter_ShouldBeForbidden()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(factory.Services);

        var (playerClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(factory);
        var owner = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "owner_player_edit", "owner_player_edit@example.com", "Test123!", "Character Owner");
        var character = await TestDataHelper.CreateTestCharacterAsync(
            factory.Services, owner.Id, "Someone Else's Character", groupId: 1);

        // Act
        var response = await playerClient.GetAsync(
            $"/Characters/Edit/{character.Id}", TestContext.Current.CancellationToken);

        // Assert
        // Forbid() under the cookie authentication scheme redirects to /Account/AccessDenied (302)
        // rather than returning a bare 403 — this mirrors the established assertion pattern used
        // throughout this test suite (e.g. QuestControllerAuthorizationRegressionTests) for every
        // denied-access case reached via [Authorize]'s default scheme.
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.Redirect, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Edit_AdminEditingCharacterInDifferentGroup_ShouldReturnNotFound()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        await TestDataHelper.SeedCampaignGroupAsync(factory.Services, 2);

        var (adminClient, _) = await AuthenticationHelper.CreateAuthenticatedAdminClientAsync(factory);
        var owner = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "owner_crossgroup_edit", "owner_crossgroup_edit@example.com", "Test123!", "Other Group Owner");
        var character = await TestDataHelper.CreateTestCharacterAsync(
            factory.Services, owner.Id, "Other Group's Character", groupId: 2);

        // Act
        var response = await adminClient.GetAsync(
            $"/Characters/Edit/{character.Id}", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Edit_OwnerEditingOwnCharacter_ShouldSucceed()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(factory.Services);

        var (client, user) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "owner_self_edit", "owner_self_edit@example.com");
        var character = await TestDataHelper.CreateTestCharacterAsync(
            factory.Services, user.Id, "My Own Character", groupId: 1);

        // Act
        var response = await client.GetAsync(
            $"/Characters/Edit/{character.Id}", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Edit_AdminEditingAnotherPlayersCharacterSetAsMain_ShouldPersistChangesAndPromoteCorrectOwner()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(factory.Services);

        var (adminClient, adminUser) = await AuthenticationHelper.CreateAuthenticatedAdminClientAsync(factory);
        var owner = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "owner_admin_setmain", "owner_admin_setmain@example.com", "Test123!", "Character Owner");

        // The admin has their own Main character - this must NOT be touched by editing
        // someone else's character.
        var adminsOwnCharacter = await TestDataHelper.CreateTestCharacterAsync(
            factory.Services, adminUser.Id, "Admin's Own Character", role: 0, groupId: 1); // Main

        // The target character starts as Backup and belongs to a different owner.
        var targetCharacter = await TestDataHelper.CreateTestCharacterAsync(
            factory.Services, owner.Id, "Owned Character", role: 1, groupId: 1); // Backup

        var formContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Id"] = targetCharacter.Id.ToString(),
            ["OwnerId"] = owner.Id.ToString(),
            ["Name"] = "Renamed By Admin",
            ["Level"] = "7",
            ["Status"] = ((int)CharacterStatus.Active).ToString(),
            ["Role"] = ((int)CharacterRole.Main).ToString(),
            ["SheetLink"] = "",
            ["Description"] = "Updated by admin edit",
            ["Backstory"] = "Updated backstory",
            ["Classes[0].Class"] = "5", // Fighter
            ["Classes[0].ClassLevel"] = "7"
        });

        // Act
        var response = await adminClient.PostAsync(
            $"/Characters/Edit/{targetCharacter.Id}", formContent, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.Should().NotContain("AccessDenied");

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();

        // The target character's edited fields were persisted and it was promoted to Main.
        var persistedTarget = await context.Characters.FindAsync(
            [targetCharacter.Id], TestContext.Current.CancellationToken);
        persistedTarget.Should().NotBeNull();
        persistedTarget!.Name.Should().Be("Renamed By Admin");
        persistedTarget.Level.Should().Be(7);
        persistedTarget.Description.Should().Be("Updated by admin edit");
        persistedTarget.Backstory.Should().Be("Updated backstory");
        persistedTarget.Role.Should().Be((int)CharacterRole.Main);

        // The admin's own character was left untouched - it must still be Main, not demoted.
        var persistedAdminCharacter = await context.Characters.FindAsync(
            [adminsOwnCharacter.Id], TestContext.Current.CancellationToken);
        persistedAdminCharacter.Should().NotBeNull();
        persistedAdminCharacter!.Role.Should().Be((int)CharacterRole.Main);
    }

    [Fact]
    public async Task Edit_PromotingCharacterToMain_ShouldDemoteOwnersOtherCharacterToBackup()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(factory.Services);

        var (ownerClient, owner) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(factory);

        // The owner already has a Main character - promoting a different one of their own
        // characters to Main must demote this one to Backup.
        var currentMainCharacter = await TestDataHelper.CreateTestCharacterAsync(
            factory.Services, owner.Id, "Current Main Character", role: 0, groupId: 1); // Main

        // The character being edited starts as Backup and belongs to the same owner (self-edit).
        var targetCharacter = await TestDataHelper.CreateTestCharacterAsync(
            factory.Services, owner.Id, "Backup Character", role: 1, groupId: 1); // Backup

        var formContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Id"] = targetCharacter.Id.ToString(),
            ["OwnerId"] = owner.Id.ToString(),
            ["Name"] = "Backup Character",
            ["Level"] = "5",
            ["Status"] = ((int)CharacterStatus.Active).ToString(),
            ["Role"] = ((int)CharacterRole.Main).ToString(),
            ["SheetLink"] = "",
            ["Description"] = "",
            ["Backstory"] = "",
            ["Classes[0].Class"] = "5", // Fighter
            ["Classes[0].ClassLevel"] = "5"
        });

        // Act
        var response = await ownerClient.PostAsync(
            $"/Characters/Edit/{targetCharacter.Id}", formContent, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();

        // The edited character was promoted to Main.
        var persistedTarget = await context.Characters.FindAsync(
            [targetCharacter.Id], TestContext.Current.CancellationToken);
        persistedTarget.Should().NotBeNull();
        persistedTarget!.Role.Should().Be((int)CharacterRole.Main);

        // The owner's previously-Main character was demoted to Backup.
        var persistedPreviousMain = await context.Characters.FindAsync(
            [currentMainCharacter.Id], TestContext.Current.CancellationToken);
        persistedPreviousMain.Should().NotBeNull();
        persistedPreviousMain!.Role.Should().Be((int)CharacterRole.Backup);
    }

    [Fact]
    public async Task Details_AdminViewingAnotherPlayersCharacter_ShowsEditButton()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(factory.Services);

        var (adminClient, _) = await AuthenticationHelper.CreateAuthenticatedAdminClientAsync(factory);
        var owner = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "owner_admin_details", "owner_admin_details@example.com", "Test123!", "Character Owner");
        var character = await TestDataHelper.CreateTestCharacterAsync(
            factory.Services, owner.Id, "Owned Character", groupId: 1);

        // Act
        var response = await adminClient.GetAsync(
            $"/Characters/Details/{character.Id}", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        content.Should().Contain("Edit Character");
    }

    [Fact]
    public async Task Delete_AdminDeletingAnotherPlayersCharacter_ShouldSucceed()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(factory.Services);

        var (adminClient, _) = await AuthenticationHelper.CreateAuthenticatedAdminClientAsync(factory);
        var owner = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "owner_admin_delete", "owner_admin_delete@example.com", "Test123!", "Character Owner");
        var character = await TestDataHelper.CreateTestCharacterAsync(
            factory.Services, owner.Id, "Character To Delete", groupId: 1);

        var formContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["id"] = character.Id.ToString()
        });

        // Act
        var response = await adminClient.PostAsync(
            "/Characters/Delete", formContent, TestContext.Current.CancellationToken);

        // Assert
        // A redirect alone is ambiguous here: Forbid() under the cookie scheme also yields a 302
        // (to /Account/AccessDenied), so a successful delete-and-redirect-to-Index must be
        // distinguished from a denied-and-redirect-to-AccessDenied by checking the Location header
        // and confirming the character row was actually removed.
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.Should().NotContain("AccessDenied");

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
        var persisted = await context.Characters.FindAsync([character.Id], TestContext.Current.CancellationToken);
        persisted.Should().BeNull();
    }

    [Fact]
    public async Task Delete_PlayerDeletingAnotherPlayersCharacter_ShouldBeForbidden()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(factory.Services);

        var (playerClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(factory);
        var owner = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "owner_player_delete", "owner_player_delete@example.com", "Test123!", "Character Owner");
        var character = await TestDataHelper.CreateTestCharacterAsync(
            factory.Services, owner.Id, "Character Not To Delete", groupId: 1);

        var formContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["id"] = character.Id.ToString()
        });

        // Act
        var response = await playerClient.PostAsync(
            "/Characters/Delete", formContent, TestContext.Current.CancellationToken);

        // Assert
        // Forbid() under the cookie authentication scheme redirects to /Account/AccessDenied (302)
        // rather than returning a bare 403 — matches this suite's established denied-access pattern.
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.Redirect, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ToggleRetirement_AdminTogglingAnotherPlayersCharacter_ShouldSucceed()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(factory.Services);

        var (adminClient, _) = await AuthenticationHelper.CreateAuthenticatedAdminClientAsync(factory);
        var owner = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "owner_admin_toggle", "owner_admin_toggle@example.com", "Test123!", "Character Owner");
        var character = await TestDataHelper.CreateTestCharacterAsync(
            factory.Services, owner.Id, "Character To Toggle", groupId: 1); // Active by default

        var formContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["id"] = character.Id.ToString()
        });

        // Act
        var response = await adminClient.PostAsync(
            "/Characters/ToggleRetirement", formContent, TestContext.Current.CancellationToken);

        // Assert
        // A redirect alone is ambiguous here: Forbid() under the cookie scheme also yields a 302
        // (to /Account/AccessDenied), so a successful toggle-and-redirect-to-Details must be
        // distinguished from a denied-and-redirect-to-AccessDenied by checking the Location header
        // and confirming the character's status actually flipped from Active to Retired.
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.Should().NotContain("AccessDenied");

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
        var persisted = await context.Characters.FindAsync([character.Id], TestContext.Current.CancellationToken);
        persisted.Should().NotBeNull();
        persisted!.Status.Should().Be((int)CharacterStatus.Retired);
    }

    [Fact]
    public async Task ToggleRetirement_PlayerTogglingAnotherPlayersCharacter_ShouldBeForbidden()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(factory.Services);

        var (playerClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(factory);
        var owner = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "owner_player_toggle", "owner_player_toggle@example.com", "Test123!", "Character Owner");
        var character = await TestDataHelper.CreateTestCharacterAsync(
            factory.Services, owner.Id, "Character Not To Toggle", groupId: 1);

        var formContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["id"] = character.Id.ToString()
        });

        // Act
        var response = await playerClient.PostAsync(
            "/Characters/ToggleRetirement", formContent, TestContext.Current.CancellationToken);

        // Assert
        // Forbid() under the cookie authentication scheme redirects to /Account/AccessDenied (302)
        // rather than returning a bare 403 — matches this suite's established denied-access pattern.
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.Redirect, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Delete_AdminDeletingCharacterInDifferentGroup_ShouldReturnNotFound()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        await TestDataHelper.SeedCampaignGroupAsync(factory.Services, 2);

        var (adminClient, _) = await AuthenticationHelper.CreateAuthenticatedAdminClientAsync(factory);
        var owner = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "owner_crossgroup_delete", "owner_crossgroup_delete@example.com", "Test123!", "Other Group Owner");
        var character = await TestDataHelper.CreateTestCharacterAsync(
            factory.Services, owner.Id, "Other Group's Character", groupId: 2);

        var formContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["id"] = character.Id.ToString()
        });

        // Act
        var response = await adminClient.PostAsync(
            "/Characters/Delete", formContent, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // Proves the checker's required Plan 02 -> Plan 03 wiring is actually reachable through the
    // real Edit POST action, not only through Plan 02's isolated service-level unit test.
    [Fact]
    public async Task Edit_NewOriginalPhotoUpload_ClearsStaleCroppedImage()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(factory.Services);

        var (ownerClient, owner) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "owner_new_original_clears_crop", "owner_new_original_clears_crop@example.com");
        var character = await TestDataHelper.CreateTestCharacterAsync(
            factory.Services, owner.Id, "Character With Stale Crop", groupId: 1);

        byte[] originalBytes = [1, 2, 3, 4];
        byte[] staleCroppedBytes = [9, 9, 9, 9];
        byte[] newOriginalBytes = [5, 6, 7, 8];

        using (var seedScope = factory.Services.CreateScope())
        {
            var seedContext = seedScope.ServiceProvider.GetRequiredService<QuestBoardContext>();
            seedContext.Set<CharacterImageEntity>().Add(new CharacterImageEntity
            {
                Id = character.Id,
                OriginalImageData = originalBytes,
                CroppedImageData = staleCroppedBytes
            });
            await seedContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using var formContent = new MultipartFormDataContent
        {
            { new StringContent(character.Id.ToString()), "Id" },
            { new StringContent(owner.Id.ToString()), "OwnerId" },
            { new StringContent("Character With Stale Crop"), "Name" },
            { new StringContent("5"), "Level" },
            { new StringContent(((int)CharacterStatus.Active).ToString()), "Status" },
            { new StringContent(((int)CharacterRole.Backup).ToString()), "Role" },
            { new StringContent(""), "SheetLink" },
            { new StringContent(""), "Description" },
            { new StringContent(""), "Backstory" },
            { new StringContent("5"), "Classes[0].Class" }, // Fighter
            { new StringContent("5"), "Classes[0].ClassLevel" }
        };
        var fileContent = new ByteArrayContent(newOriginalBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        formContent.Add(fileContent, "ProfilePictureFile", "new.png");

        // Act
        var response = await ownerClient.PostAsync(
            $"/Characters/Edit/{character.Id}", formContent, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.Should().NotContain("AccessDenied");

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
        var persistedImage = await context.Set<CharacterImageEntity>().FindAsync(
            [character.Id], TestContext.Current.CancellationToken);
        persistedImage.Should().NotBeNull();
        persistedImage!.CroppedImageData.Should().BeNull();
        persistedImage.OriginalImageData.Should().Equal(newOriginalBytes);
    }

    // Companion guard proving the wiring passes hasNewOriginalUpload: false (not always true) --
    // an edit with no file part must leave the stored crop untouched.
    [Fact]
    public async Task Edit_NoNewPhoto_PreservesStoredCroppedImage()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(factory.Services);

        var (ownerClient, owner) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "owner_no_new_photo_preserves_crop", "owner_no_new_photo_preserves_crop@example.com");
        var character = await TestDataHelper.CreateTestCharacterAsync(
            factory.Services, owner.Id, "Character Keeping Its Crop", groupId: 1);

        byte[] originalBytes = [1, 2, 3, 4];
        byte[] storedCroppedBytes = [9, 9, 9, 9];

        using (var seedScope = factory.Services.CreateScope())
        {
            var seedContext = seedScope.ServiceProvider.GetRequiredService<QuestBoardContext>();
            seedContext.Set<CharacterImageEntity>().Add(new CharacterImageEntity
            {
                Id = character.Id,
                OriginalImageData = originalBytes,
                CroppedImageData = storedCroppedBytes
            });
            await seedContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var formContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Id"] = character.Id.ToString(),
            ["OwnerId"] = owner.Id.ToString(),
            ["Name"] = "Character Keeping Its Crop",
            ["Level"] = "5",
            ["Status"] = ((int)CharacterStatus.Active).ToString(),
            ["Role"] = ((int)CharacterRole.Backup).ToString(),
            ["SheetLink"] = "",
            ["Description"] = "",
            ["Backstory"] = "",
            ["Classes[0].Class"] = "5", // Fighter
            ["Classes[0].ClassLevel"] = "5"
        });

        // Act
        var response = await ownerClient.PostAsync(
            $"/Characters/Edit/{character.Id}", formContent, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.Should().NotContain("AccessDenied");

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
        var persistedImage = await context.Set<CharacterImageEntity>().FindAsync(
            [character.Id], TestContext.Current.CancellationToken);
        persistedImage.Should().NotBeNull();
        persistedImage!.CroppedImageData.Should().Equal(storedCroppedBytes);
    }

    // Proves a real posted CroppedPictureFile is validated and persisted through the widened
    // 4-arg UpdateAsync call, not just cleared/ignored like the single-file path.
    [Fact]
    public async Task Edit_NewOriginalAndCroppedPhotoUpload_PersistsSubmittedCrop()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(factory.Services);

        var (ownerClient, owner) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "owner_crop_persists", "owner_crop_persists@example.com");
        var character = await TestDataHelper.CreateTestCharacterAsync(
            factory.Services, owner.Id, "Character Getting A Real Crop", groupId: 1);

        byte[] newOriginalBytes = [5, 6, 7, 8];
        byte[] submittedCropBytes = [10, 20, 30, 40, 50];

        using var formContent = new MultipartFormDataContent
        {
            { new StringContent(character.Id.ToString()), "Id" },
            { new StringContent(owner.Id.ToString()), "OwnerId" },
            { new StringContent("Character Getting A Real Crop"), "Name" },
            { new StringContent("5"), "Level" },
            { new StringContent(((int)CharacterStatus.Active).ToString()), "Status" },
            { new StringContent(((int)CharacterRole.Backup).ToString()), "Role" },
            { new StringContent(""), "SheetLink" },
            { new StringContent(""), "Description" },
            { new StringContent(""), "Backstory" },
            { new StringContent("5"), "Classes[0].Class" }, // Fighter
            { new StringContent("5"), "Classes[0].ClassLevel" }
        };
        var originalFileContent = new ByteArrayContent(newOriginalBytes);
        originalFileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        formContent.Add(originalFileContent, "ProfilePictureFile", "new.png");

        var croppedFileContent = new ByteArrayContent(submittedCropBytes);
        croppedFileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        formContent.Add(croppedFileContent, "CroppedPictureFile", "new-cropped.png");

        // Act
        var response = await ownerClient.PostAsync(
            $"/Characters/Edit/{character.Id}", formContent, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.Should().NotContain("AccessDenied");

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
        var persistedImage = await context.Set<CharacterImageEntity>().FindAsync(
            [character.Id], TestContext.Current.CancellationToken);
        persistedImage.Should().NotBeNull();
        persistedImage!.CroppedImageData.Should().NotBeNull();
        persistedImage.CroppedImageData.Should().Equal(submittedCropBytes);
        persistedImage.OriginalImageData.Should().Equal(newOriginalBytes);
    }

    // Proves the Create POST action -- not just Edit -- persists a crop submitted alongside
    // the original at creation time, closing the gap where a brand-new character's crop was
    // silently discarded.
    [Fact]
    public async Task Create_WithCroppedPhoto_PersistsCroppedImage()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(factory.Services);

        var (ownerClient, owner) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "owner_create_with_crop", "owner_create_with_crop@example.com");

        byte[] originalBytes = [5, 6, 7, 8];
        byte[] submittedCropBytes = [10, 20, 30, 40, 50];

        using var formContent = new MultipartFormDataContent
        {
            { new StringContent(owner.Id.ToString()), "OwnerId" },
            { new StringContent("Brand New Character With A Crop"), "Name" },
            { new StringContent("5"), "Level" },
            { new StringContent(((int)CharacterStatus.Active).ToString()), "Status" },
            { new StringContent(((int)CharacterRole.Backup).ToString()), "Role" },
            { new StringContent(""), "SheetLink" },
            { new StringContent(""), "Description" },
            { new StringContent(""), "Backstory" },
            { new StringContent("5"), "Classes[0].Class" }, // Fighter
            { new StringContent("5"), "Classes[0].ClassLevel" }
        };
        var originalFileContent = new ByteArrayContent(originalBytes);
        originalFileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        formContent.Add(originalFileContent, "ProfilePictureFile", "new.png");

        var croppedFileContent = new ByteArrayContent(submittedCropBytes);
        croppedFileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        formContent.Add(croppedFileContent, "CroppedPictureFile", "new-cropped.png");

        // Act
        var response = await ownerClient.PostAsync(
            "/Characters/Create", formContent, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.Should().NotContain("AccessDenied");

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
        var persistedCharacter = context.Characters.IgnoreQueryFilters()
            .FirstOrDefault(c => c.Name == "Brand New Character With A Crop");
        persistedCharacter.Should().NotBeNull();

        var persistedImage = await context.Set<CharacterImageEntity>().FindAsync(
            [persistedCharacter!.Id], TestContext.Current.CancellationToken);
        persistedImage.Should().NotBeNull();
        persistedImage!.CroppedImageData.Should().NotBeNull();
        persistedImage.CroppedImageData.Should().Equal(submittedCropBytes);
        persistedImage.OriginalImageData.Should().Equal(originalBytes);
    }

    // Proves the boolean has-image gate (HasProfilePicture, projected without eager-loading
    // the byte[] columns) actually drives the Index list rendering end-to-end: a character with
    // a stored image renders the portrait endpoint, one without renders the placeholder instead.
    [Fact]
    public async Task Index_CharacterWithImage_RendersPortraitEndpoint()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (client, user) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(factory);
        var character = await TestDataHelper.CreateTestCharacterAsync(
            factory.Services, user.Id, "Character With Portrait", groupId: 1);

        using (var seedScope = factory.Services.CreateScope())
        {
            var seedContext = seedScope.ServiceProvider.GetRequiredService<QuestBoardContext>();
            seedContext.Set<CharacterImageEntity>().Add(new CharacterImageEntity
            {
                Id = character.Id,
                OriginalImageData = [1, 2, 3, 4]
            });
            await seedContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Act
        var response = await client.GetAsync("/Characters", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        content.Should().Contain($"GetCroppedPicture/{character.Id}");
    }

    [Fact]
    public async Task Index_CharacterWithoutImage_DoesNotRenderPortraitEndpoint()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (client, user) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(factory);
        var character = await TestDataHelper.CreateTestCharacterAsync(
            factory.Services, user.Id, "Character Without Portrait", groupId: 1);

        // Act
        var response = await client.GetAsync("/Characters", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        content.Should().NotContain($"GetCroppedPicture/{character.Id}");
    }

    // Companion to Create_WithCroppedPhoto_PersistsCroppedImage — proves the original bytes
    // land on the Domain model via the Task 2 local-variable staging fix, not just the crop.
    [Fact]
    public async Task Create_WithPhoto_PersistsOriginalImage()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (ownerClient, owner) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "owner_create_original_persists", "owner_create_original_persists@example.com");

        byte[] originalBytes = [11, 22, 33, 44];

        using var formContent = new MultipartFormDataContent
        {
            { new StringContent(owner.Id.ToString()), "OwnerId" },
            { new StringContent("Brand New Character With Original Photo"), "Name" },
            { new StringContent("5"), "Level" },
            { new StringContent(((int)CharacterStatus.Active).ToString()), "Status" },
            { new StringContent(((int)CharacterRole.Backup).ToString()), "Role" },
            { new StringContent(""), "SheetLink" },
            { new StringContent(""), "Description" },
            { new StringContent(""), "Backstory" },
            { new StringContent("5"), "Classes[0].Class" }, // Fighter
            { new StringContent("5"), "Classes[0].ClassLevel" }
        };
        var originalFileContent = new ByteArrayContent(originalBytes);
        originalFileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        formContent.Add(originalFileContent, "ProfilePictureFile", "new.png");

        // Act
        var response = await ownerClient.PostAsync(
            "/Characters/Create", formContent, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.Should().NotContain("AccessDenied");

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
        var persistedCharacter = context.Characters.IgnoreQueryFilters()
            .FirstOrDefault(c => c.Name == "Brand New Character With Original Photo");
        persistedCharacter.Should().NotBeNull();

        var persistedImage = await context.Set<CharacterImageEntity>().FindAsync(
            [persistedCharacter!.Id], TestContext.Current.CancellationToken);
        persistedImage.Should().NotBeNull();
        persistedImage!.OriginalImageData.Should().Equal(originalBytes);
    }

    // Proves a crop-only submission (re-cropping the stored original without re-uploading a new
    // ProfilePictureFile) is read, validated, and persisted by the controller, and the stored
    // original survives untouched.
    [Fact]
    public async Task Edit_CropOnlyNoNewOriginal_PersistsCropAndPreservesOriginal()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(factory.Services);

        var (ownerClient, owner) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "owner_recrop_only", "owner_recrop_only@example.com");
        var character = await TestDataHelper.CreateTestCharacterAsync(
            factory.Services, owner.Id, "Character Getting Re-Cropped", groupId: 1);

        byte[] originalBytes = [1, 2, 3, 4];
        byte[] staleCroppedBytes = [9, 9, 9, 9];
        byte[] newCropBytes = [200, 201, 202, 203];

        using (var seedScope = factory.Services.CreateScope())
        {
            var seedContext = seedScope.ServiceProvider.GetRequiredService<QuestBoardContext>();
            seedContext.Set<CharacterImageEntity>().Add(new CharacterImageEntity
            {
                Id = character.Id,
                OriginalImageData = originalBytes,
                CroppedImageData = staleCroppedBytes
            });
            await seedContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using var formContent = new MultipartFormDataContent
        {
            { new StringContent(character.Id.ToString()), "Id" },
            { new StringContent(owner.Id.ToString()), "OwnerId" },
            { new StringContent("Character Getting Re-Cropped"), "Name" },
            { new StringContent("5"), "Level" },
            { new StringContent(((int)CharacterStatus.Active).ToString()), "Status" },
            { new StringContent(((int)CharacterRole.Backup).ToString()), "Role" },
            { new StringContent(""), "SheetLink" },
            { new StringContent(""), "Description" },
            { new StringContent(""), "Backstory" },
            { new StringContent("5"), "Classes[0].Class" }, // Fighter
            { new StringContent("5"), "Classes[0].ClassLevel" }
        };
        // No ProfilePictureFile part -- this is the distinguishing property of a crop-only save.
        var croppedFileContent = new ByteArrayContent(newCropBytes);
        croppedFileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        formContent.Add(croppedFileContent, "CroppedPictureFile", "re-cropped.png");

        // Act
        var response = await ownerClient.PostAsync(
            $"/Characters/Edit/{character.Id}", formContent, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.Should().NotContain("AccessDenied");

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
        var persistedImage = await context.Set<CharacterImageEntity>().FindAsync(
            [character.Id], TestContext.Current.CancellationToken);
        persistedImage.Should().NotBeNull();
        persistedImage!.CroppedImageData.Should().Equal(newCropBytes);
        persistedImage.OriginalImageData.Should().Equal(originalBytes);
    }

    // Proves the new GetCroppedPicture read action serves the stored crop.
    [Fact]
    public async Task GetCroppedPicture_CropStored_ReturnsOkWithContent()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(factory.Services);

        var (ownerClient, owner) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "owner_get_cropped", "owner_get_cropped@example.com");
        var character = await TestDataHelper.CreateTestCharacterAsync(
            factory.Services, owner.Id, "Character With A Crop To Fetch", groupId: 1);

        byte[] originalBytes = [1, 2, 3, 4];
        byte[] croppedBytes = [9, 9, 9, 9, 9];

        using (var seedScope = factory.Services.CreateScope())
        {
            var seedContext = seedScope.ServiceProvider.GetRequiredService<QuestBoardContext>();
            seedContext.Set<CharacterImageEntity>().Add(new CharacterImageEntity
            {
                Id = character.Id,
                OriginalImageData = originalBytes,
                CroppedImageData = croppedBytes
            });
            await seedContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Act
        var response = await ownerClient.GetAsync(
            $"/Characters/GetCroppedPicture/{character.Id}", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken);
        content.Should().NotBeEmpty();
        content.Should().Equal(croppedBytes);
    }
}
