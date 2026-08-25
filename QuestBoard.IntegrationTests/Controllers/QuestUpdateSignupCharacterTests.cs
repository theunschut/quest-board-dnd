using QuestBoard.IntegrationTests.Helpers;
using System.Net;

namespace QuestBoard.IntegrationTests.Controllers;

public class QuestUpdateSignupCharacterTests(WebApplicationFactoryBase factory)
    : IClassFixture<WebApplicationFactoryBase>, IAsyncLifetime
{
    private readonly HttpClient _client = factory.CreateNonRedirectingClient();

    // IAsyncLifetime — reset the singleton group context after each test class run so that
    // state mutated by any test in this class does not bleed into subsequently-executed
    // test classes.
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public ValueTask DisposeAsync()
    {
        factory.TestGroupContext.ActiveGroupId = 1;
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task UpdateSignupCharacter_Post_WithDifferentActiveCharacter_UpdatesSignupCharacterId()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var dm = await AuthenticationHelper.CreateTestUserAsync(factory.Services, "charswapdm1", "charswapdm1@example.com");
        var quest = await TestDataHelper.CreateTestQuestAsync(factory.Services, dm.Id, "Swap Quest 1");

        var (playerClient, player) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "charswap1", "charswap1@example.com");

        var firstCharacter = await TestDataHelper.CreateTestCharacterAsync(factory.Services, player.Id, "First Character");
        var secondCharacter = await TestDataHelper.CreateTestCharacterAsync(factory.Services, player.Id, "Second Character");

        await TestDataHelper.CreatePlayerSignupAsync(factory.Services, quest.Id, player.Id, characterId: firstCharacter.Id);

        var formContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["questId"] = quest.Id.ToString(),
            ["characterId"] = secondCharacter.Id.ToString()
        });

        // Act
        var response = await playerClient.PostAsync("/Quest/UpdateSignupCharacter", formContent, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Redirect, HttpStatusCode.Found);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
        var signup = await context.PlayerSignups
            .FirstOrDefaultAsync(s => s.QuestId == quest.Id && s.PlayerId == player.Id, TestContext.Current.CancellationToken);
        signup.Should().NotBeNull();
        signup!.CharacterId.Should().Be(secondCharacter.Id);
    }

    [Fact]
    public async Task UpdateSignupCharacter_Post_WithNoCharacterIdField_ClearsSignupCharacterIdToNull()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var dm = await AuthenticationHelper.CreateTestUserAsync(factory.Services, "charswapdm2", "charswapdm2@example.com");
        var quest = await TestDataHelper.CreateTestQuestAsync(factory.Services, dm.Id, "Swap Quest 2");

        var (playerClient, player) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "charswap2", "charswap2@example.com");

        var character = await TestDataHelper.CreateTestCharacterAsync(factory.Services, player.Id);

        await TestDataHelper.CreatePlayerSignupAsync(factory.Services, quest.Id, player.Id, characterId: character.Id);

        // A disabled <select> posts no field for that name at all, so the body carries only
        // questId — omit the characterId key entirely rather than sending an empty string.
        var formContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["questId"] = quest.Id.ToString()
        });

        // Act
        var response = await playerClient.PostAsync("/Quest/UpdateSignupCharacter", formContent, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Redirect, HttpStatusCode.Found);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
        var signup = await context.PlayerSignups
            .FirstOrDefaultAsync(s => s.QuestId == quest.Id && s.PlayerId == player.Id, TestContext.Current.CancellationToken);
        signup.Should().NotBeNull();
        signup!.CharacterId.Should().BeNull();
    }

    [Fact]
    public async Task UpdateSignupCharacter_Post_OnFinalizedQuest_UpdatesSignupCharacterId()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var dm = await AuthenticationHelper.CreateTestUserAsync(factory.Services, "charswapdm3", "charswapdm3@example.com");
        var quest = await TestDataHelper.CreateTestQuestAsync(
            factory.Services, dm.Id, "Swap Quest 3", isFinalized: true, finalizedDate: DateTime.UtcNow.AddDays(7));
        await TestDataHelper.CreateProposedDateAsync(factory.Services, quest.Id, quest.FinalizedDate!.Value);

        var (playerClient, player) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "charswap3", "charswap3@example.com");

        var firstCharacter = await TestDataHelper.CreateTestCharacterAsync(factory.Services, player.Id, "First Character");
        var secondCharacter = await TestDataHelper.CreateTestCharacterAsync(factory.Services, player.Id, "Second Character");

        await TestDataHelper.CreatePlayerSignupAsync(factory.Services, quest.Id, player.Id, characterId: firstCharacter.Id);

        var formContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["questId"] = quest.Id.ToString(),
            ["characterId"] = secondCharacter.Id.ToString()
        });

        // Act
        var response = await playerClient.PostAsync("/Quest/UpdateSignupCharacter", formContent, TestContext.Current.CancellationToken);

        // Assert — no finalization cutoff applies to this action
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Redirect, HttpStatusCode.Found);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
        var signup = await context.PlayerSignups
            .FirstOrDefaultAsync(s => s.QuestId == quest.Id && s.PlayerId == player.Id, TestContext.Current.CancellationToken);
        signup.Should().NotBeNull();
        signup!.CharacterId.Should().Be(secondCharacter.Id);
    }

    [Fact]
    public async Task UpdateSignupCharacter_Post_ForWaitlistedSignup_UpdatesSignupCharacterId()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var dm = await AuthenticationHelper.CreateTestUserAsync(factory.Services, "charswapdm4", "charswapdm4@example.com");
        var quest = await TestDataHelper.CreateTestQuestAsync(factory.Services, dm.Id, "Swap Quest 4");

        var (playerClient, player) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "charswap4", "charswap4@example.com");

        var firstCharacter = await TestDataHelper.CreateTestCharacterAsync(factory.Services, player.Id, "First Character");
        var secondCharacter = await TestDataHelper.CreateTestCharacterAsync(factory.Services, player.Id, "Second Character");

        await TestDataHelper.CreatePlayerSignupAsync(
            factory.Services, quest.Id, player.Id, isSelected: false, characterId: firstCharacter.Id);

        var formContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["questId"] = quest.Id.ToString(),
            ["characterId"] = secondCharacter.Id.ToString()
        });

        // Act
        var response = await playerClient.PostAsync("/Quest/UpdateSignupCharacter", formContent, TestContext.Current.CancellationToken);

        // Assert — waitlisted status is unrelated to whether the character swap is allowed
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Redirect, HttpStatusCode.Found);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
        var signup = await context.PlayerSignups
            .FirstOrDefaultAsync(s => s.QuestId == quest.Id && s.PlayerId == player.Id, TestContext.Current.CancellationToken);
        signup.Should().NotBeNull();
        signup!.CharacterId.Should().Be(secondCharacter.Id);
    }

    [Theory]
    [InlineData(0)] // Player
    [InlineData(1)] // Spectator
    [InlineData(2)] // AssistantDM
    public async Task UpdateSignupCharacter_Post_ForEachSignupRole_UpdatesSignupCharacterId(int signupRole)
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var dm = await AuthenticationHelper.CreateTestUserAsync(factory.Services, $"charswapdm5r{signupRole}", $"charswapdm5r{signupRole}@example.com");
        var quest = await TestDataHelper.CreateTestQuestAsync(factory.Services, dm.Id, $"Swap Quest 5 Role {signupRole}");

        var (playerClient, player) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, $"charswap5r{signupRole}", $"charswap5r{signupRole}@example.com");

        var firstCharacter = await TestDataHelper.CreateTestCharacterAsync(factory.Services, player.Id, "First Character");
        var secondCharacter = await TestDataHelper.CreateTestCharacterAsync(factory.Services, player.Id, "Second Character");

        await TestDataHelper.CreatePlayerSignupAsync(
            factory.Services, quest.Id, player.Id, signupRole: signupRole, characterId: firstCharacter.Id);

        var formContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["questId"] = quest.Id.ToString(),
            ["characterId"] = secondCharacter.Id.ToString()
        });

        // Act
        var response = await playerClient.PostAsync("/Quest/UpdateSignupCharacter", formContent, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Redirect, HttpStatusCode.Found);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
        var signup = await context.PlayerSignups
            .FirstOrDefaultAsync(s => s.QuestId == quest.Id && s.PlayerId == player.Id, TestContext.Current.CancellationToken);
        signup.Should().NotBeNull();
        signup!.CharacterId.Should().Be(secondCharacter.Id);
    }

    [Fact]
    public async Task UpdateSignupCharacter_Post_WithAnotherUsersCharacterInSameBoard_ReturnsBadRequestAndLeavesCharacterUnchanged()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var dm = await AuthenticationHelper.CreateTestUserAsync(factory.Services, "charswapdm6", "charswapdm6@example.com");
        var quest = await TestDataHelper.CreateTestQuestAsync(factory.Services, dm.Id, "Swap Quest 6");

        var (playerClient, player) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "charswap6", "charswap6@example.com");
        var ownCharacter = await TestDataHelper.CreateTestCharacterAsync(factory.Services, player.Id, "Own Character");

        var otherUser = await AuthenticationHelper.CreateTestUserAsync(factory.Services, "charswap6other", "charswap6other@example.com");
        var otherCharacter = await TestDataHelper.CreateTestCharacterAsync(factory.Services, otherUser.Id, "Other Player's Character");

        await TestDataHelper.CreatePlayerSignupAsync(factory.Services, quest.Id, player.Id, characterId: ownCharacter.Id);

        var formContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["questId"] = quest.Id.ToString(),
            ["characterId"] = otherCharacter.Id.ToString()
        });

        // Act
        var response = await playerClient.PostAsync("/Quest/UpdateSignupCharacter", formContent, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
        var signup = await context.PlayerSignups
            .FirstOrDefaultAsync(s => s.QuestId == quest.Id && s.PlayerId == player.Id, TestContext.Current.CancellationToken);
        signup.Should().NotBeNull();
        signup!.CharacterId.Should().Be(ownCharacter.Id);
    }
}
