using System.Net;
using QuestBoard.IntegrationTests.Helpers;

namespace QuestBoard.IntegrationTests.Controllers;

/// <summary>
/// Wave-0 failing tests pinning the finalized-quest Edit/Manage behaviors this phase must
/// deliver: relaxing the finalized-quest Edit block, hiding Proposed Dates on finalized
/// quests (desktop + mobile), the Total Player Count floor guard, the no-roster-wipe
/// guarantee, and the new Edit Quest entry point on Manage. These tests are authored before
/// any production code changes and are expected to fail against the current tree, which
/// returns 400 BadRequest for a finalized-quest Edit GET/POST.
/// </summary>
public class QuestFinalizedEditTests(WebApplicationFactoryBase factory) : IClassFixture<WebApplicationFactoryBase>
{
    private const string MobileUserAgent =
        "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1";

    // -----------------------------------------------------------------------
    // Edit GET — the core RED assertion (currently 400 BadRequest)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task FinalizedEdit_Get_Desktop_Returns200_NotBadRequest()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (client, dm) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "finedit1", "finedit1@example.com", roles: ["DungeonMaster"]);
        var quest = await TestDataHelper.CreateTestQuestAsync(
            factory.Services, dm.Id, "Finalized Edit Quest 1", isFinalized: true, finalizedDate: DateTime.UtcNow.AddDays(7));
        await TestDataHelper.CreateProposedDateAsync(factory.Services, quest.Id, quest.FinalizedDate!.Value);

        // Act
        var response = await client.GetAsync($"/Quest/Edit/{quest.Id}", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // -----------------------------------------------------------------------
    // Edit GET — hides Proposed Dates, keeps other OneShot fields (desktop + mobile)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task FinalizedEdit_Get_Desktop_HidesProposedDates_ShowsOneShotFields()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (client, dm) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "finedit2", "finedit2@example.com", roles: ["DungeonMaster"]);
        var quest = await TestDataHelper.CreateTestQuestAsync(
            factory.Services, dm.Id, "Finalized Edit Quest 2", isFinalized: true, finalizedDate: DateTime.UtcNow.AddDays(7));
        await TestDataHelper.CreateProposedDateAsync(factory.Services, quest.Id, quest.FinalizedDate!.Value);

        // Act
        var response = await client.GetAsync($"/Quest/Edit/{quest.Id}", TestContext.Current.CancellationToken);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Assert
        content.Should().NotContain("Proposed Dates");
        content.Should().Contain("Challenge Rating");
        content.Should().Contain("Total Player Count");
        content.Should().Contain("Dungeon Master Session Only");
    }

    [Fact]
    public async Task FinalizedEdit_Get_Mobile_HidesProposedDates_ShowsOneShotFields()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (client, dm) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "finedit3", "finedit3@example.com", roles: ["DungeonMaster"]);
        var quest = await TestDataHelper.CreateTestQuestAsync(
            factory.Services, dm.Id, "Finalized Edit Quest 3", isFinalized: true, finalizedDate: DateTime.UtcNow.AddDays(7));
        await TestDataHelper.CreateProposedDateAsync(factory.Services, quest.Id, quest.FinalizedDate!.Value);

        // Act
        var request = new HttpRequestMessage(HttpMethod.Get, $"/Quest/Edit/{quest.Id}");
        request.Headers.TryAddWithoutValidation("User-Agent", MobileUserAgent);
        request.Headers.Authorization = client.DefaultRequestHeaders.Authorization;
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Assert
        content.Should().NotContain("Proposed Dates");
        content.Should().Contain("Challenge Rating");
        content.Should().Contain("Total Player Count");
        content.Should().Contain("Dungeon Master Session Only");
    }

    // -----------------------------------------------------------------------
    // Regression guard — non-finalized quest still shows Proposed Dates
    // -----------------------------------------------------------------------

    [Fact]
    public async Task NonFinalizedEdit_Get_Desktop_StillShowsProposedDates()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (client, dm) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "finedit4", "finedit4@example.com", roles: ["DungeonMaster"]);
        var quest = await TestDataHelper.CreateTestQuestAsync(
            factory.Services, dm.Id, "Non-Finalized Edit Quest", isFinalized: false);

        // Act
        var response = await client.GetAsync($"/Quest/Edit/{quest.Id}", TestContext.Current.CancellationToken);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        content.Should().Contain("Proposed Dates");
    }

    // -----------------------------------------------------------------------
    // Edit POST — Total Player Count floor guard (D-01)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task FinalizedEdit_Post_LoweringPlayerCountBelowSelected_ReRendersAndDoesNotPersist()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (client, dm) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "finedit5", "finedit5@example.com", roles: ["DungeonMaster"]);
        var quest = await TestDataHelper.CreateTestQuestAsync(
            factory.Services, dm.Id, "Finalized Edit Quest 5", isFinalized: true, finalizedDate: DateTime.UtcNow.AddDays(7));
        await TestDataHelper.CreateProposedDateAsync(factory.Services, quest.Id, quest.FinalizedDate!.Value);

        for (var i = 0; i < 3; i++)
        {
            var seatedPlayer = await AuthenticationHelper.CreateTestUserAsync(
                factory.Services, $"finedit5seated{i}", $"finedit5seated{i}@example.com");
            await TestDataHelper.CreatePlayerSignupAsync(
                factory.Services, quest.Id, seatedPlayer.Id, signupRole: 0, isSelected: true);
        }

        var getResponse = await client.GetAsync($"/Quest/Edit/{quest.Id}", TestContext.Current.CancellationToken);
        var (token, cookieValue) = await AntiForgeryHelper.ExtractAntiForgeryTokenAsync(getResponse);

        if (!string.IsNullOrEmpty(cookieValue))
        {
            client.DefaultRequestHeaders.Remove("Cookie");
            client.DefaultRequestHeaders.Add("Cookie", $".AspNetCore.Antiforgery={cookieValue}");
        }

        var formContent = AntiForgeryHelper.CreateFormContentWithAntiForgeryToken(
            new Dictionary<string, string>
            {
                ["Id"] = quest.Id.ToString(),
                ["Quest.Id"] = quest.Id.ToString(),
                ["Quest.Title"] = quest.Title,
                ["Quest.Description"] = quest.Description,
                ["Quest.ChallengeRating"] = "5",
                ["Quest.DungeonMasterSession"] = "false",
                ["Quest.TotalPlayerCount"] = "2",
            },
            token);

        // Act
        var response = await client.PostAsync($"/Quest/Edit/{quest.Id}", formContent, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
        var persistedQuest = await context.Quests.FirstAsync(q => q.Id == quest.Id, TestContext.Current.CancellationToken);
        persistedQuest.TotalPlayerCount.Should().Be(4);
    }

    // -----------------------------------------------------------------------
    // Edit POST — valid edit persists without wiping roster/FinalizedDate (D-04)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task FinalizedEdit_Post_ValidTitleEdit_PersistsWithoutWipingRoster()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (client, dm) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "finedit6", "finedit6@example.com", roles: ["DungeonMaster"]);
        var quest = await TestDataHelper.CreateTestQuestAsync(
            factory.Services, dm.Id, "Finalized Edit Quest 6", isFinalized: true, finalizedDate: DateTime.UtcNow.AddDays(7));
        await TestDataHelper.CreateProposedDateAsync(factory.Services, quest.Id, quest.FinalizedDate!.Value);

        for (var i = 0; i < 2; i++)
        {
            var seatedPlayer = await AuthenticationHelper.CreateTestUserAsync(
                factory.Services, $"finedit6seated{i}", $"finedit6seated{i}@example.com");
            await TestDataHelper.CreatePlayerSignupAsync(
                factory.Services, quest.Id, seatedPlayer.Id, signupRole: 0, isSelected: true);
        }

        var getResponse = await client.GetAsync($"/Quest/Edit/{quest.Id}", TestContext.Current.CancellationToken);
        var (token, cookieValue) = await AntiForgeryHelper.ExtractAntiForgeryTokenAsync(getResponse);

        if (!string.IsNullOrEmpty(cookieValue))
        {
            client.DefaultRequestHeaders.Remove("Cookie");
            client.DefaultRequestHeaders.Add("Cookie", $".AspNetCore.Antiforgery={cookieValue}");
        }

        var formContent = AntiForgeryHelper.CreateFormContentWithAntiForgeryToken(
            new Dictionary<string, string>
            {
                ["Id"] = quest.Id.ToString(),
                ["Quest.Id"] = quest.Id.ToString(),
                ["Quest.Title"] = "Renamed Finalized Quest",
                ["Quest.Description"] = quest.Description,
                ["Quest.ChallengeRating"] = "5",
                ["Quest.DungeonMasterSession"] = "false",
                ["Quest.TotalPlayerCount"] = "4",
            },
            token);

        // Act
        var response = await client.PostAsync($"/Quest/Edit/{quest.Id}", formContent, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.OK);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
        var persistedQuest = await context.Quests.FirstAsync(q => q.Id == quest.Id, TestContext.Current.CancellationToken);

        persistedQuest.Title.Should().Be("Renamed Finalized Quest");
        persistedQuest.IsFinalized.Should().BeTrue();
        persistedQuest.FinalizedDate.Should().NotBeNull();
        context.PlayerSignups.Count(s => s.QuestId == quest.Id && s.IsSelected).Should().Be(2);
    }

    // -----------------------------------------------------------------------
    // Manage page — new Edit Quest entry point for finalized quests (desktop + mobile)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task FinalizedManage_Desktop_RendersEditQuestLink()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (client, dm) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "finedit7", "finedit7@example.com", roles: ["DungeonMaster"]);
        var quest = await TestDataHelper.CreateTestQuestAsync(
            factory.Services, dm.Id, "Finalized Manage Quest 1", isFinalized: true, finalizedDate: DateTime.UtcNow.AddDays(7));
        await TestDataHelper.CreateProposedDateAsync(factory.Services, quest.Id, quest.FinalizedDate!.Value);

        // Act
        var response = await client.GetAsync($"/Quest/Manage/{quest.Id}", TestContext.Current.CancellationToken);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        content.Should().Contain("Edit Quest");
        content.Should().Contain($"/Quest/Edit/{quest.Id}");
    }

    [Fact]
    public async Task FinalizedManage_Mobile_RendersEditQuestLink()
    {
        // Arrange
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (client, dm) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "finedit8", "finedit8@example.com", roles: ["DungeonMaster"]);
        var quest = await TestDataHelper.CreateTestQuestAsync(
            factory.Services, dm.Id, "Finalized Manage Quest 2", isFinalized: true, finalizedDate: DateTime.UtcNow.AddDays(7));
        await TestDataHelper.CreateProposedDateAsync(factory.Services, quest.Id, quest.FinalizedDate!.Value);

        // Act
        var request = new HttpRequestMessage(HttpMethod.Get, $"/Quest/Manage/{quest.Id}");
        request.Headers.TryAddWithoutValidation("User-Agent", MobileUserAgent);
        request.Headers.Authorization = client.DefaultRequestHeaders.Authorization;
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        content.Should().Contain("Edit Quest");
        content.Should().Contain($"/Quest/Edit/{quest.Id}");
    }
}
