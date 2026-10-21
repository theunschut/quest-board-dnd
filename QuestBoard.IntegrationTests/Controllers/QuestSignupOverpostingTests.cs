using System.Net;
using QuestBoard.Domain.Enums;
using QuestBoard.IntegrationTests.Helpers;

namespace QuestBoard.IntegrationTests.Controllers;

/// <summary>
/// Pins that the open-signup POST takes only the character choice and this quest's date votes from
/// the form. Everything else on a new signup is the server's to decide, so a crafted request cannot
/// pre-select itself, backdate its place in the queue, smuggle in a character, or vote on another
/// quest's dates.
/// </summary>
public class QuestSignupOverpostingTests : IClassFixture<WebApplicationFactoryBase>, IAsyncLifetime
{
    private readonly WebApplicationFactoryBase _factory;

    public QuestSignupOverpostingTests(WebApplicationFactoryBase factory)
    {
        _factory = factory;
    }

    public async ValueTask InitializeAsync() => await TestDataHelper.ClearDatabaseAsync(_factory.Services);

    // Page settings live on the shared board, so restore the baseline whichever test ran.
    public async ValueTask DisposeAsync()
    {
        _factory.TestGroupContext.ActiveGroupId = 1;
        await BoardPageSeed.ResetAsync(_factory, 1);
    }

    [Fact]
    public async Task Details_Post_WithCraftedServerOwnedFields_IgnoresThem()
    {
        // Arrange
        var dm = await AuthenticationHelper.CreateTestUserAsync(_factory.Services, "overpostdm1", "overpostdm1@example.com");
        var quest = await TestDataHelper.CreateTestQuestAsync(_factory.Services, dm.Id, "Overpost Quest 1");
        var (playerClient, player) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, "overpost1", "overpost1@example.com");
        var before = DateTime.UtcNow.AddMinutes(-5);

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Id"] = "0",
            ["Quest.Id"] = quest.Id.ToString(),
            ["selectedRole"] = "0",
            ["IsSelected"] = "true",
            ["SignupTime"] = "2000-01-01T00:00:00",
            ["LastVoteChangeTime"] = "2000-01-01T00:00:00"
        });

        // Act
        var response = await playerClient.PostAsync("/Quest/Details", form, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Found, HttpStatusCode.Redirect);
        var signup = (await SignupsForAsync(quest.Id, player.Id)).Should().ContainSingle().Subject;
        signup.IsSelected.Should().BeFalse("only the DM selects players");
        signup.SignupTime.Should().BeAfter(before, "the queue position is the server's time, not the form's");
        signup.LastVoteChangeTime.Should().BeNull();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Details_Post_WithCraftedCharacterGraph_CreatesNoCharacter(bool charactersOff)
    {
        // Arrange
        var dm = await AuthenticationHelper.CreateTestUserAsync(_factory.Services, $"overpostdm2{charactersOff}", $"overpostdm2{charactersOff}@example.com");
        var quest = await TestDataHelper.CreateTestQuestAsync(_factory.Services, dm.Id, "Overpost Quest 2");
        var (playerClient, player) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, $"overpost2{charactersOff}", $"overpost2{charactersOff}@example.com");
        if (charactersOff)
        {
            await BoardPageSeed.SetAsync(_factory, 1, BoardPageSet.Empty.With(BoardPage.Characters), BoardPage.Quests);
        }

        var charactersBefore = await CountCharactersAsync();

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Id"] = "0",
            ["Quest.Id"] = quest.Id.ToString(),
            ["selectedRole"] = "0",
            ["Character.Name"] = "Smuggled Hero",
            ["Character.OwnerId"] = player.Id.ToString(),
            ["Character.GroupId"] = "1"
        });

        // Act
        var response = await playerClient.PostAsync("/Quest/Details", form, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Found, HttpStatusCode.Redirect);
        (await CountCharactersAsync()).Should().Be(charactersBefore, "a signup never creates a character");
        var signup = (await SignupsForAsync(quest.Id, player.Id)).Should().ContainSingle().Subject;
        signup.CharacterId.Should().BeNull();
    }

    [Fact]
    public async Task Details_Post_WithVoteOnAnotherQuestsDate_KeepsOnlyThisQuestsVotes()
    {
        // Arrange
        var dm = await AuthenticationHelper.CreateTestUserAsync(_factory.Services, "overpostdm3", "overpostdm3@example.com");
        var quest = await TestDataHelper.CreateTestQuestAsync(_factory.Services, dm.Id, "Overpost Quest 3");
        var otherQuest = await TestDataHelper.CreateTestQuestAsync(_factory.Services, dm.Id, "Overpost Other Quest");
        var ownDate = await TestDataHelper.CreateProposedDateAsync(_factory.Services, quest.Id, DateTime.UtcNow.AddDays(10));
        var foreignDate = await TestDataHelper.CreateProposedDateAsync(_factory.Services, otherQuest.Id, DateTime.UtcNow.AddDays(11));
        var (playerClient, player) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, "overpost3", "overpost3@example.com");

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Id"] = "0",
            ["Quest.Id"] = quest.Id.ToString(),
            ["selectedRole"] = "0",
            ["DateVotes[0].ProposedDateId"] = ownDate.Id.ToString(),
            ["DateVotes[0].Vote"] = nameof(VoteType.Yes),
            ["DateVotes[1].ProposedDateId"] = foreignDate.Id.ToString(),
            ["DateVotes[1].Vote"] = nameof(VoteType.Yes)
        });

        // Act
        var response = await playerClient.PostAsync("/Quest/Details", form, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Found, HttpStatusCode.Redirect);
        var signup = (await SignupsForAsync(quest.Id, player.Id)).Should().ContainSingle().Subject;
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
        var votedDateIds = await context.Set<PlayerDateVoteEntity>()
            .Where(v => v.PlayerSignupId == signup.Id)
            .Select(v => v.ProposedDateId)
            .ToListAsync(TestContext.Current.CancellationToken);
        votedDateIds.Should().Equal(ownDate.Id);
    }

    private async Task<List<PlayerSignupEntity>> SignupsForAsync(int questId, int playerId)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
        return await context.PlayerSignups
            .IgnoreQueryFilters()
            .Where(s => s.QuestId == questId && s.PlayerId == playerId)
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    private async Task<int> CountCharactersAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
        return await context.Characters.IgnoreQueryFilters().CountAsync(TestContext.Current.CancellationToken);
    }
}
