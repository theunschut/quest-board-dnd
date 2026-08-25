using QuestBoard.IntegrationTests.Helpers;
using System.Net;
using System.Text.RegularExpressions;

namespace QuestBoard.IntegrationTests.Controllers;

public class QuestDetailsCharacterControlTests(WebApplicationFactoryBase factory) : IClassFixture<WebApplicationFactoryBase>
{
    // Retired status per QuestBoard.Domain.Enums.CharacterStatus.
    private const int RetiredStatus = 1;

    // Every trigger that opens the shared character-select modal carries this target,
    // no matter which row or state (filled/empty) it came from.
    private static List<string> ExtractCharacterSelectTriggers(string html) =>
        [.. Regex.Matches(html, @"<button[^>]*data-bs-target=""#characterSelectModal""[^>]*>", RegexOptions.Singleline)
            .Select(m => m.Value)];

    [Fact]
    public async Task Details_Get_WhenOwnSignupHasCharacter_RendersTriggerCarryingThatCharacterId()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var dm = await AuthenticationHelper.CreateTestUserAsync(factory.Services, "detailsdm1", "detailsdm1@example.com");
        var quest = await TestDataHelper.CreateTestQuestAsync(
            factory.Services, dm.Id, "Trigger Quest 1", isFinalized: true, finalizedDate: DateTime.UtcNow.AddDays(7));
        await TestDataHelper.CreateProposedDateAsync(factory.Services, quest.Id, quest.FinalizedDate!.Value);

        var (playerClient, player) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "detailsplayer1", "detailsplayer1@example.com");
        var character = await TestDataHelper.CreateTestCharacterAsync(factory.Services, player.Id, "Trigger Character 1");
        await TestDataHelper.CreatePlayerSignupAsync(factory.Services, quest.Id, player.Id, isSelected: true, characterId: character.Id);

        // Act
        var response = await playerClient.GetAsync($"/Quest/Details/{quest.Id}", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var triggers = ExtractCharacterSelectTriggers(content);
        triggers.Should().Contain(t => t.Contains($"data-current-character-id=\"{character.Id}\""));
    }

    [Fact]
    public async Task Details_Get_WhenOwnSignupHasNoCharacter_RendersAddTriggerWithEmptyCurrentCharacterId()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var dm = await AuthenticationHelper.CreateTestUserAsync(factory.Services, "detailsdm2", "detailsdm2@example.com");
        var quest = await TestDataHelper.CreateTestQuestAsync(
            factory.Services, dm.Id, "Trigger Quest 2", isFinalized: true, finalizedDate: DateTime.UtcNow.AddDays(7));
        await TestDataHelper.CreateProposedDateAsync(factory.Services, quest.Id, quest.FinalizedDate!.Value);

        var (playerClient, player) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "detailsplayer2", "detailsplayer2@example.com");
        await TestDataHelper.CreateTestCharacterAsync(factory.Services, player.Id, "Owned Character 2");
        await TestDataHelper.CreatePlayerSignupAsync(factory.Services, quest.Id, player.Id, isSelected: true, characterId: null);

        // Act
        var response = await playerClient.GetAsync($"/Quest/Details/{quest.Id}", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var triggers = ExtractCharacterSelectTriggers(content);
        triggers.Should().Contain(t => t.Contains("data-current-character-id=\"\""));
    }

    [Fact]
    public async Task Details_Get_RendersTheSharedModalExactlyOnce()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var dm = await AuthenticationHelper.CreateTestUserAsync(factory.Services, "detailsdm3", "detailsdm3@example.com");
        var quest = await TestDataHelper.CreateTestQuestAsync(factory.Services, dm.Id, "Trigger Quest 3");

        var (playerClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "detailsplayer3", "detailsplayer3@example.com");

        // Act
        var response = await playerClient.GetAsync($"/Quest/Details/{quest.Id}", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var modalInstanceCount = Regex.Matches(content, Regex.Escape("id=\"characterSelectModal\"")).Count;
        modalInstanceCount.Should().Be(1);
    }

    [Fact]
    public async Task Details_Get_WhenSignupHoldsARetiredCharacter_RendersTheTriggerCarryingThatCharacterId()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var dm = await AuthenticationHelper.CreateTestUserAsync(factory.Services, "detailsdm4", "detailsdm4@example.com");
        var quest = await TestDataHelper.CreateTestQuestAsync(
            factory.Services, dm.Id, "Trigger Quest 4", isFinalized: true, finalizedDate: DateTime.UtcNow.AddDays(7));
        await TestDataHelper.CreateProposedDateAsync(factory.Services, quest.Id, quest.FinalizedDate!.Value);

        var (playerClient, player) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "detailsplayer4", "detailsplayer4@example.com");
        var retiredCharacter = await TestDataHelper.CreateTestCharacterAsync(
            factory.Services, player.Id, "Retired Character 4", status: RetiredStatus);
        await TestDataHelper.CreatePlayerSignupAsync(factory.Services, quest.Id, player.Id, isSelected: true, characterId: retiredCharacter.Id);

        // Act
        var response = await playerClient.GetAsync($"/Quest/Details/{quest.Id}", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var triggers = ExtractCharacterSelectTriggers(content);
        var retiredTrigger = triggers.SingleOrDefault(t => t.Contains($"data-current-character-id=\"{retiredCharacter.Id}\""));
        retiredTrigger.Should().NotBeNull();
        retiredTrigger.Should().Contain("(Retired)");
    }

    [Fact]
    public async Task Details_Get_WhenSignupHasRetiredCharacterAndNoOtherCharacters_StillRendersTheChangeTrigger()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var dm = await AuthenticationHelper.CreateTestUserAsync(factory.Services, "detailsdm5", "detailsdm5@example.com");
        var quest = await TestDataHelper.CreateTestQuestAsync(
            factory.Services, dm.Id, "Trigger Quest 5", isFinalized: true, finalizedDate: DateTime.UtcNow.AddDays(7));
        await TestDataHelper.CreateProposedDateAsync(factory.Services, quest.Id, quest.FinalizedDate!.Value);

        var (playerClient, player) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "detailsplayer5", "detailsplayer5@example.com");
        var onlyRetiredCharacter = await TestDataHelper.CreateTestCharacterAsync(
            factory.Services, player.Id, "Only Retired Character 5", status: RetiredStatus);
        await TestDataHelper.CreatePlayerSignupAsync(factory.Services, quest.Id, player.Id, isSelected: true, characterId: onlyRetiredCharacter.Id);

        // Act
        var response = await playerClient.GetAsync($"/Quest/Details/{quest.Id}", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var triggers = ExtractCharacterSelectTriggers(content);
        triggers.Should().Contain(t => t.Contains($"data-current-character-id=\"{onlyRetiredCharacter.Id}\""));
    }

    [Fact]
    public async Task Details_Get_ForAWaitlistedSignupWithCharacter_RendersTheTrigger()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var dm = await AuthenticationHelper.CreateTestUserAsync(factory.Services, "detailsdm6", "detailsdm6@example.com");
        var quest = await TestDataHelper.CreateTestQuestAsync(
            factory.Services, dm.Id, "Trigger Quest 6", isFinalized: true, finalizedDate: DateTime.UtcNow.AddDays(7));
        await TestDataHelper.CreateProposedDateAsync(factory.Services, quest.Id, quest.FinalizedDate!.Value);

        var (playerClient, player) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "detailsplayer6", "detailsplayer6@example.com");
        var character = await TestDataHelper.CreateTestCharacterAsync(factory.Services, player.Id, "Waitlist Character 6");
        await TestDataHelper.CreatePlayerSignupAsync(factory.Services, quest.Id, player.Id, isSelected: false, characterId: character.Id);

        // Act
        var response = await playerClient.GetAsync($"/Quest/Details/{quest.Id}", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var triggers = ExtractCharacterSelectTriggers(content);
        triggers.Should().Contain(t => t.Contains($"data-current-character-id=\"{character.Id}\""));
    }

    [Fact]
    public async Task Details_Get_ForAnotherPlayersRow_RendersNoTriggerForThatRow()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var dm = await AuthenticationHelper.CreateTestUserAsync(factory.Services, "detailsdm7", "detailsdm7@example.com");
        var quest = await TestDataHelper.CreateTestQuestAsync(
            factory.Services, dm.Id, "Trigger Quest 7", isFinalized: true, finalizedDate: DateTime.UtcNow.AddDays(7));
        await TestDataHelper.CreateProposedDateAsync(factory.Services, quest.Id, quest.FinalizedDate!.Value);

        var otherPlayer = await AuthenticationHelper.CreateTestUserAsync(factory.Services, "detailsotherplayer7", "detailsotherplayer7@example.com");
        var otherCharacter = await TestDataHelper.CreateTestCharacterAsync(factory.Services, otherPlayer.Id, "Other Player Character 7");
        await TestDataHelper.CreatePlayerSignupAsync(factory.Services, quest.Id, otherPlayer.Id, isSelected: true, characterId: otherCharacter.Id);

        var (viewerClient, viewer) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "detailsviewer7", "detailsviewer7@example.com");
        await TestDataHelper.CreatePlayerSignupAsync(factory.Services, quest.Id, viewer.Id, isSelected: true, characterId: null);

        // Act
        var response = await viewerClient.GetAsync($"/Quest/Details/{quest.Id}", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        content.Should().Contain(otherCharacter.Name);
        content.Should().NotContain($"data-current-character-id=\"{otherCharacter.Id}\"");
    }
}
