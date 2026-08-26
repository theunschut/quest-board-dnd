using QuestBoard.IntegrationTests.Helpers;
using System.Net;

namespace QuestBoard.IntegrationTests.Controllers;

// The character pickers offer every character the caller owns, whatever its status. These
// tests pin the other half of that contract: the signup save paths must accept what the
// pickers offer, so a player who picks a Retired character gets a signup rather than a
// silent no-op with no message.
public class QuestSignupCharacterStatusTests(WebApplicationFactoryBase factory)
    : IClassFixture<WebApplicationFactoryBase>, IAsyncLifetime
{
    private const int RetiredStatus = 1;
    private const int DeadStatus = 2;

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public ValueTask DisposeAsync()
    {
        factory.TestGroupContext.ActiveGroupId = 1;
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task Details_Post_SigningUpWithRetiredCharacter_CreatesSignupWithThatCharacter()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var dm = await AuthenticationHelper.CreateTestUserAsync(factory.Services, "retiredsignupdm", "retiredsignupdm@example.com");
        var quest = await TestDataHelper.CreateTestQuestAsync(factory.Services, dm.Id, "Retired Signup Quest");

        var (playerClient, player) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "retiredsignup", "retiredsignup@example.com");

        var retiredCharacter = await TestDataHelper.CreateTestCharacterAsync(
            factory.Services, player.Id, "Retired Hero", status: RetiredStatus);

        var formContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Quest.Id"] = quest.Id.ToString(),
            ["CharacterId"] = retiredCharacter.Id.ToString(),
            ["selectedRole"] = "0"
        });

        // Act
        var response = await playerClient.PostAsync("/Quest/Details", formContent, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Found);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
        var signup = await context.PlayerSignups
            .FirstOrDefaultAsync(s => s.QuestId == quest.Id && s.PlayerId == player.Id, TestContext.Current.CancellationToken);
        signup.Should().NotBeNull("a Retired character the picker offers must be accepted by the signup save path");
        signup!.CharacterId.Should().Be(retiredCharacter.Id);
    }

    [Fact]
    public async Task JoinFinalizedQuest_Post_WithRetiredCharacter_CreatesSignupWithThatCharacter()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var dm = await AuthenticationHelper.CreateTestUserAsync(factory.Services, "retiredjoindm", "retiredjoindm@example.com");
        var finalizedDate = DateTime.UtcNow.AddDays(7);
        var quest = await TestDataHelper.CreateTestQuestAsync(
            factory.Services, dm.Id, "Retired Join Quest", isFinalized: true, finalizedDate: finalizedDate);
        await TestDataHelper.CreateProposedDateAsync(factory.Services, quest.Id, finalizedDate);

        var (playerClient, player) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "retiredjoin", "retiredjoin@example.com");

        var retiredCharacter = await TestDataHelper.CreateTestCharacterAsync(
            factory.Services, player.Id, "Retired Latecomer", status: RetiredStatus);

        var formContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["questId"] = quest.Id.ToString(),
            ["characterId"] = retiredCharacter.Id.ToString(),
            ["selectedRole"] = "0"
        });

        // Act
        var response = await playerClient.PostAsync("/Quest/JoinFinalizedQuest", formContent, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Found);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
        var signup = await context.PlayerSignups
            .FirstOrDefaultAsync(s => s.QuestId == quest.Id && s.PlayerId == player.Id, TestContext.Current.CancellationToken);
        signup.Should().NotBeNull("a Retired character the picker offers must be accepted by the join save path");
        signup!.CharacterId.Should().Be(retiredCharacter.Id);
    }

    [Fact]
    public async Task JoinFinalizedQuest_Post_WithDeadCharacter_CreatesSignupWithThatCharacter()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var dm = await AuthenticationHelper.CreateTestUserAsync(factory.Services, "deadjoindm", "deadjoindm@example.com");
        var finalizedDate = DateTime.UtcNow.AddDays(9);
        var quest = await TestDataHelper.CreateTestQuestAsync(
            factory.Services, dm.Id, "Dead Join Quest", isFinalized: true, finalizedDate: finalizedDate);
        await TestDataHelper.CreateProposedDateAsync(factory.Services, quest.Id, finalizedDate);

        var (playerClient, player) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "deadjoin", "deadjoin@example.com");

        var deadCharacter = await TestDataHelper.CreateTestCharacterAsync(
            factory.Services, player.Id, "Dead Legend", status: DeadStatus);

        var formContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["questId"] = quest.Id.ToString(),
            ["characterId"] = deadCharacter.Id.ToString(),
            ["selectedRole"] = "0"
        });

        // Act
        var response = await playerClient.PostAsync("/Quest/JoinFinalizedQuest", formContent, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Found);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
        var signup = await context.PlayerSignups
            .FirstOrDefaultAsync(s => s.QuestId == quest.Id && s.PlayerId == player.Id, TestContext.Current.CancellationToken);
        signup.Should().NotBeNull("a Dead character the picker offers must be accepted by the join save path");
        signup!.CharacterId.Should().Be(deadCharacter.Id);
    }

    [Fact]
    public async Task JoinFinalizedQuest_Post_WithAnotherPlayersCharacter_CreatesNoSignup()
    {
        // Arrange — ownership is still a gate even though status no longer is.
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var dm = await AuthenticationHelper.CreateTestUserAsync(factory.Services, "ownergatedm", "ownergatedm@example.com");
        var finalizedDate = DateTime.UtcNow.AddDays(11);
        var quest = await TestDataHelper.CreateTestQuestAsync(
            factory.Services, dm.Id, "Owner Gate Quest", isFinalized: true, finalizedDate: finalizedDate);
        await TestDataHelper.CreateProposedDateAsync(factory.Services, quest.Id, finalizedDate);

        var (playerClient, player) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "ownergate", "ownergate@example.com");
        var otherPlayer = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "ownergateother", "ownergateother@example.com");

        var othersCharacter = await TestDataHelper.CreateTestCharacterAsync(
            factory.Services, otherPlayer.Id, "Not Yours");

        var formContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["questId"] = quest.Id.ToString(),
            ["characterId"] = othersCharacter.Id.ToString(),
            ["selectedRole"] = "0"
        });

        // Act
        var response = await playerClient.PostAsync("/Quest/JoinFinalizedQuest", formContent, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Found);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
        var signup = await context.PlayerSignups
            .FirstOrDefaultAsync(s => s.QuestId == quest.Id && s.PlayerId == player.Id, TestContext.Current.CancellationToken);
        signup.Should().BeNull("ownership remains a gate on the join save path");
    }
}
