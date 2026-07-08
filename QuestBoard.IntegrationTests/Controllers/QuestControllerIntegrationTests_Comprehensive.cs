using QuestBoard.Domain.Enums;
using QuestBoard.IntegrationTests.Helpers;
using System.Net;

namespace QuestBoard.IntegrationTests.Controllers;

public class QuestControllerIntegrationTests_Comprehensive(WebApplicationFactoryBase factory) : IClassFixture<WebApplicationFactoryBase>
{
    private readonly HttpClient _client = factory.CreateNonRedirectingClient();

    // /quests is the migrated quest board route — authenticated users get the
    // board, and it renders seeded quest content (proving the migrated board logic runs).
    [Fact]
    public async Task Index_Quests_Authenticated_ReturnsOk()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var dm = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "questsdm", "questsdm@example.com");
        await TestDataHelper.CreateTestQuestAsync(factory.Services, dm.Id, "Quests Route Quest");

        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "questsviewer", "questsviewer@example.com");

        // Act
        var response = await client.GetAsync("/quests", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        content.Should().Contain("Quests Route Quest");
    }

    // Unauthenticated GET /quests must redirect — proves the action-level [Authorize]
    // is enforced (the board is no longer publicly visible on this route either).
    [Fact]
    public async Task Index_Quests_Unauthenticated_Redirects()
    {
        // Act
        var response = await factory.CreateNonRedirectingClient()
            .GetAsync("/quests", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Create_Get_WhenNotAuthenticated_ShouldRedirectToLogin()
    {
        // Act
        var response = await _client.GetAsync("/Quest/Create", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Create_Get_WhenAuthenticatedAsDM_ShouldReturnCreateForm()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedDMClientAsync(factory);

        // Act
        var response = await client.GetAsync("/Quest/Create", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        content.Should().Contain("Create");
        content.Should().Contain("Quest");
    }

    [Fact]
    public async Task Details_WithValidQuestId_ShouldReturnQuestDetails()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var dm = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "questdm", "questdm@example.com");
        var quest = await TestDataHelper.CreateTestQuestAsync(
            factory.Services, dm.Id, "Adventure Quest", "Epic adventure");

        // Act
        var response = await _client.GetAsync($"/Quest/Details/{quest.Id}", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        content.Should().Contain("Adventure Quest");
        content.Should().Contain("Epic adventure");
    }

    [Fact]
    public async Task Details_WithInvalidQuestId_ShouldReturn404()
    {
        // Act
        var response = await _client.GetAsync("/Quest/Details/99999", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Edit_Get_WhenNotQuestOwner_ShouldReturnForbidden()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (_, dm) = await AuthenticationHelper.CreateAuthenticatedDMClientAsync(factory, "originaldm", "originaldm@example.com");
        var quest = await TestDataHelper.CreateTestQuestAsync(factory.Services, dm.Id);

        var (otherClient, _) = await AuthenticationHelper.CreateAuthenticatedDMClientAsync(factory, "otherdm", "otherdm@example.com");

        // Act
        var response = await otherClient.GetAsync($"/Quest/Edit/{quest.Id}", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.Redirect, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Signup_Post_WhenAuthenticated_ShouldAddPlayerToQuest()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var dm = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "signupdm", "signupdm@example.com");
        var quest = await TestDataHelper.CreateTestQuestAsync(factory.Services, dm.Id);

        var (playerClient, player) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "player1", "player1@example.com");

        var formContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Id"] = "0",
            ["Quest.Id"] = quest.Id.ToString(),
            ["selectedRole"] = "0" // Player role
        });

        // Act
        var response = await playerClient.PostAsync($"/Quest/Details", formContent, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Redirect, HttpStatusCode.Found);

        // Verify signup was created
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
        var signup = await context.PlayerSignups
            .FirstOrDefaultAsync(s => s.QuestId == quest.Id && s.PlayerId == player.Id, TestContext.Current.CancellationToken);
        signup.Should().NotBeNull();
    }

    [Fact]
    public async Task Manage_Get_WhenQuestOwner_ShouldReturnManagementPage()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (dmClient, dm) = await AuthenticationHelper.CreateAuthenticatedDMClientAsync(factory);
        var quest = await TestDataHelper.CreateTestQuestAsync(factory.Services, dm.Id);

        // Add proposed dates (required for Manage view)
        await TestDataHelper.CreateProposedDateAsync(factory.Services, quest.Id, DateTime.Today.AddDays(7));
        await TestDataHelper.CreateProposedDateAsync(factory.Services, quest.Id, DateTime.Today.AddDays(14));

        // Add some signups
        var player = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "manageplayer", "manageplayer@example.com");
        await TestDataHelper.CreatePlayerSignupAsync(factory.Services, quest.Id, player.Id);

        // Act
        var response = await dmClient.GetAsync($"/Quest/Manage/{quest.Id}", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        content.Should().Contain("Manage");
    }

    [Fact]
    public async Task Finalize_Post_WhenQuestOwner_ShouldFinalizeQuest()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (dmClient, dm) = await AuthenticationHelper.CreateAuthenticatedDMClientAsync(factory);
        var quest = await TestDataHelper.CreateTestQuestAsync(factory.Services, dm.Id);

        // Add proposed dates
        var proposedDate = await TestDataHelper.CreateProposedDateAsync(factory.Services, quest.Id, DateTime.Today.AddDays(7));

        var player1 = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "finalizeplayer1", "fp1@example.com");
        var player2 = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "finalizeplayer2", "fp2@example.com");

        var signup1 = await TestDataHelper.CreatePlayerSignupAsync(factory.Services, quest.Id, player1.Id);
        var signup2 = await TestDataHelper.CreatePlayerSignupAsync(factory.Services, quest.Id, player2.Id);

        var formData = new List<KeyValuePair<string, string>>
        {
            new KeyValuePair<string, string>("SelectedDateId", proposedDate.Id.ToString()),
            new KeyValuePair<string, string>("SelectedPlayerIds", signup1.Id.ToString()),
            new KeyValuePair<string, string>("SelectedPlayerIds", signup2.Id.ToString())
        };
        var formContent = new FormUrlEncodedContent(formData);

        // Act
        var response = await dmClient.PostAsync($"/Quest/Finalize/{quest.Id}", formContent, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Redirect, HttpStatusCode.Found);

        // Verify quest was finalized
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
        var finalizedQuest = await context.Quests.FindAsync([quest.Id], TestContext.Current.CancellationToken);
        finalizedQuest.Should().NotBeNull();
        finalizedQuest!.IsFinalized.Should().BeTrue();
    }

    [Fact]
    public async Task Delete_Post_WhenNotQuestOwner_ShouldReturnForbidden()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (_, dm) = await AuthenticationHelper.CreateAuthenticatedDMClientAsync(factory, "deletedm", "delete@example.com");
        var quest = await TestDataHelper.CreateTestQuestAsync(factory.Services, dm.Id);

        var (otherClient, _) = await AuthenticationHelper.CreateAuthenticatedDMClientAsync(factory, "otherdeletem", "otherdelete@example.com");

        // Act
        var response = await otherClient.DeleteAsync($"/Quest/Delete/{quest.Id}", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.Redirect, HttpStatusCode.Unauthorized);
    }

    // Regression guard: RemoveAsync's internal re-fetch (GetQuestWithManageDetailsAsync) loads
    // ProposedDates and PlayerSignups as two separate collection Includes in one query, which
    // EF Core cross-joins unless AsSplitQuery() is applied — producing a combinatorial row count
    // and a very slow delete. This test exercises that exact shape (dates + votes + signups
    // together) rather than the empty-graph case the Forbidden test above uses.
    [Fact]
    public async Task Delete_Post_WhenQuestHasDatesVotesAndSignups_RemovesEntireGraph()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (dmClient, dm) = await AuthenticationHelper.CreateAuthenticatedDMClientAsync(factory, "graphdeletedm", "graphdelete@example.com");
        var quest = await TestDataHelper.CreateTestQuestAsync(factory.Services, dm.Id, "Graph Delete Quest");
        var proposedDate = await TestDataHelper.CreateProposedDateAsync(factory.Services, quest.Id, DateTime.UtcNow.AddDays(3));
        var signupA = await TestDataHelper.CreatePlayerSignupAsync(factory.Services, quest.Id, dm.Id);
        var (_, playerB) = await AuthenticationHelper.CreateAuthenticatedDMClientAsync(factory, "graphdeleteplayer", "graphdeleteplayer@example.com");
        var signupB = await TestDataHelper.CreatePlayerSignupAsync(factory.Services, quest.Id, playerB.Id);

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
            context.Set<PlayerDateVoteEntity>().AddRange(
                new PlayerDateVoteEntity { PlayerSignupId = signupA.Id, ProposedDateId = proposedDate.Id, Vote = (int)VoteType.Yes },
                new PlayerDateVoteEntity { PlayerSignupId = signupB.Id, ProposedDateId = proposedDate.Id, Vote = (int)VoteType.Maybe });
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Act
        var response = await dmClient.DeleteAsync($"/Quest/Delete/{quest.Id}", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var verifyScope = factory.Services.CreateScope();
        var verifyContext = verifyScope.ServiceProvider.GetRequiredService<QuestBoardContext>();
        (await verifyContext.Quests.FindAsync([quest.Id], TestContext.Current.CancellationToken)).Should().BeNull();
        (await verifyContext.Set<ProposedDateEntity>().FindAsync([proposedDate.Id], TestContext.Current.CancellationToken)).Should().BeNull();
        (await verifyContext.PlayerSignups.FindAsync([signupA.Id], TestContext.Current.CancellationToken)).Should().BeNull();
        (await verifyContext.PlayerSignups.FindAsync([signupB.Id], TestContext.Current.CancellationToken)).Should().BeNull();
    }
}
