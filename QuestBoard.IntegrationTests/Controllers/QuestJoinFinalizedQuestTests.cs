using QuestBoard.Domain.Enums;
using QuestBoard.IntegrationTests.Helpers;
using System.Net;

namespace QuestBoard.IntegrationTests.Controllers;

public class QuestJoinFinalizedQuestTests(WebApplicationFactoryBase factory) : IClassFixture<WebApplicationFactoryBase>
{
    private readonly HttpClient _client = factory.CreateNonRedirectingClient();

    [Fact]
    public async Task JoinFinalizedQuest_Post_WhenQuestFullAndRoleIsPlayer_CreatesWaitlistedSignup()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var dm = await AuthenticationHelper.CreateTestUserAsync(factory.Services, "joindm1", "joindm1@example.com");
        var quest = await TestDataHelper.CreateTestQuestAsync(
            factory.Services, dm.Id, "Full Quest", isFinalized: true, finalizedDate: DateTime.UtcNow.AddDays(7));
        await TestDataHelper.CreateProposedDateAsync(factory.Services, quest.Id, quest.FinalizedDate!.Value);

        // Fill quest to TotalPlayerCount (4, TestDataHelper default) with selected Player signups
        for (var i = 0; i < 4; i++)
        {
            var seatedPlayer = await AuthenticationHelper.CreateTestUserAsync(factory.Services, $"seated{i}", $"seated{i}@example.com");
            await TestDataHelper.CreatePlayerSignupAsync(factory.Services, quest.Id, seatedPlayer.Id, isSelected: true);
        }

        var (playerClient, newJoiner) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "newjoiner1", "newjoiner1@example.com");

        var formContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["questId"] = quest.Id.ToString(),
            ["selectedRole"] = "0" // Player
        });

        // Act
        var response = await playerClient.PostAsync("/Quest/JoinFinalizedQuest", formContent, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Redirect, HttpStatusCode.Found);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
        var signup = await context.PlayerSignups
            .FirstOrDefaultAsync(s => s.QuestId == quest.Id && s.PlayerId == newJoiner.Id, TestContext.Current.CancellationToken);
        signup.Should().NotBeNull();
        signup!.IsSelected.Should().BeFalse();
    }

    [Fact]
    public async Task JoinFinalizedQuest_Post_WhenQuestHasSpaceAndRoleIsPlayer_CreatesSeatedSignup()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var dm = await AuthenticationHelper.CreateTestUserAsync(factory.Services, "joindm2", "joindm2@example.com");
        var quest = await TestDataHelper.CreateTestQuestAsync(
            factory.Services, dm.Id, "Open Quest", isFinalized: true, finalizedDate: DateTime.UtcNow.AddDays(7));
        await TestDataHelper.CreateProposedDateAsync(factory.Services, quest.Id, quest.FinalizedDate!.Value);

        var (playerClient, newJoiner) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "newjoiner2", "newjoiner2@example.com");

        var formContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["questId"] = quest.Id.ToString(),
            ["selectedRole"] = "0" // Player
        });

        // Act
        var response = await playerClient.PostAsync("/Quest/JoinFinalizedQuest", formContent, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Redirect, HttpStatusCode.Found);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
        var signup = await context.PlayerSignups
            .FirstOrDefaultAsync(s => s.QuestId == quest.Id && s.PlayerId == newJoiner.Id, TestContext.Current.CancellationToken);
        signup.Should().NotBeNull();
        signup!.IsSelected.Should().BeTrue();
    }

    [Fact]
    public async Task JoinFinalizedQuest_Post_WhenQuestFullAndRoleIsAssistantDM_CreatesSeatedSignup()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var dm = await AuthenticationHelper.CreateTestUserAsync(factory.Services, "joindm3", "joindm3@example.com");
        var quest = await TestDataHelper.CreateTestQuestAsync(
            factory.Services, dm.Id, "Full Quest ADM", isFinalized: true, finalizedDate: DateTime.UtcNow.AddDays(7));
        await TestDataHelper.CreateProposedDateAsync(factory.Services, quest.Id, quest.FinalizedDate!.Value);

        for (var i = 0; i < 4; i++)
        {
            var seatedPlayer = await AuthenticationHelper.CreateTestUserAsync(factory.Services, $"seatedadm{i}", $"seatedadm{i}@example.com");
            await TestDataHelper.CreatePlayerSignupAsync(factory.Services, quest.Id, seatedPlayer.Id, isSelected: true);
        }

        var (playerClient, newJoiner) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "newjoiner3", "newjoiner3@example.com");

        var formContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["questId"] = quest.Id.ToString(),
            ["selectedRole"] = "2" // AssistantDM
        });

        // Act
        var response = await playerClient.PostAsync("/Quest/JoinFinalizedQuest", formContent, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Redirect, HttpStatusCode.Found);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
        var signup = await context.PlayerSignups
            .FirstOrDefaultAsync(s => s.QuestId == quest.Id && s.PlayerId == newJoiner.Id, TestContext.Current.CancellationToken);
        signup.Should().NotBeNull();
        signup!.IsSelected.Should().BeTrue();
    }

    [Fact]
    public async Task JoinFinalizedQuest_Post_WhenQuestFullAndRoleIsSpectator_CreatesSeatedSignup()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var dm = await AuthenticationHelper.CreateTestUserAsync(factory.Services, "joindm4", "joindm4@example.com");
        var quest = await TestDataHelper.CreateTestQuestAsync(
            factory.Services, dm.Id, "Full Quest Spectator", isFinalized: true, finalizedDate: DateTime.UtcNow.AddDays(7));
        await TestDataHelper.CreateProposedDateAsync(factory.Services, quest.Id, quest.FinalizedDate!.Value);

        for (var i = 0; i < 4; i++)
        {
            var seatedPlayer = await AuthenticationHelper.CreateTestUserAsync(factory.Services, $"seatedspec{i}", $"seatedspec{i}@example.com");
            await TestDataHelper.CreatePlayerSignupAsync(factory.Services, quest.Id, seatedPlayer.Id, isSelected: true);
        }

        var (playerClient, newJoiner) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "newjoiner4", "newjoiner4@example.com");

        var formContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["questId"] = quest.Id.ToString(),
            ["selectedRole"] = "1" // Spectator
        });

        // Act
        var response = await playerClient.PostAsync("/Quest/JoinFinalizedQuest", formContent, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Redirect, HttpStatusCode.Found);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
        var signup = await context.PlayerSignups
            .FirstOrDefaultAsync(s => s.QuestId == quest.Id && s.PlayerId == newJoiner.Id, TestContext.Current.CancellationToken);
        signup.Should().NotBeNull();
        signup!.IsSelected.Should().BeTrue();
    }
}
