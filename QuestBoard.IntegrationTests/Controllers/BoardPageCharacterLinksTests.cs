using System.Net;
using QuestBoard.Domain.Enums;
using QuestBoard.IntegrationTests.Helpers;

namespace QuestBoard.IntegrationTests.Controllers;

/// <summary>
/// Asserts that a board without the Characters page never attaches a character to a signup, even
/// from a stale or crafted form, and that signing up without a character keeps working.
/// </summary>
public class BoardPageCharacterLinksTests : IClassFixture<WebApplicationFactoryBase>, IAsyncLifetime
{
    private const string InvalidCharacterMessage = "Invalid character selection.";

    private readonly WebApplicationFactoryBase _factory;

    public BoardPageCharacterLinksTests(WebApplicationFactoryBase factory)
    {
        _factory = factory;
    }

    // A fresh store with the default board per test, so seeding page settings always finds its board.
    public async ValueTask InitializeAsync() => await TestDataHelper.ClearDatabaseAsync(_factory.Services);

    // The board context and the stored settings are shared across the whole class fixture, so this
    // restores the baseline and no test's premise depends on which test ran before it.
    public async ValueTask DisposeAsync()
    {
        _factory.TestGroupContext.BoardType = BoardType.OneShot;
        _factory.TestGroupContext.ActiveGroupId = 1;
        await BoardPageSeed.ResetAsync(_factory, 1);
    }

    private Task SwitchCharactersOffAsync() =>
        BoardPageSeed.SetAsync(_factory, 1, BoardPageSet.Empty.With(BoardPage.Characters), BoardPage.Quests);

    private async Task<List<PlayerSignupEntity>> SignupsForAsync(int questId, int playerId)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
        return await context.PlayerSignups
            .Where(s => s.QuestId == questId && s.PlayerId == playerId)
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    private async Task<string> FollowRedirectAsync(HttpClient client, HttpResponseMessage response)
    {
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Found, HttpStatusCode.Redirect);
        var followed = await client.GetAsync(response.Headers.Location, TestContext.Current.CancellationToken);
        followed.StatusCode.Should().Be(HttpStatusCode.OK);
        return await followed.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task CharactersOff_OpenSignupPostWithOwnCharacter_IsRefusedAndNoSignupCarriesIt()
    {
        // Arrange
        var dm = await AuthenticationHelper.CreateTestUserAsync(_factory.Services, "charlinksdm1", "charlinksdm1@example.com");
        var quest = await TestDataHelper.CreateTestQuestAsync(_factory.Services, dm.Id, "Characters Off Quest 1");
        var (playerClient, player) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, "charlinks1", "charlinks1@example.com");
        var character = await TestDataHelper.CreateTestCharacterAsync(_factory.Services, player.Id, "Crafted Character");
        await SwitchCharactersOffAsync();

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Id"] = "0",
            ["Quest.Id"] = quest.Id.ToString(),
            ["CharacterId"] = character.Id.ToString(),
            ["selectedRole"] = "0"
        });

        // Act
        var response = await playerClient.PostAsync("/Quest/Details", form, TestContext.Current.CancellationToken);

        // Assert
        var followedHtml = await FollowRedirectAsync(playerClient, response);
        followedHtml.Should().Contain(InvalidCharacterMessage);
        (await SignupsForAsync(quest.Id, player.Id)).Should().BeEmpty();
    }

    [Fact]
    public async Task CharactersOff_FinalizedJoinWithOwnCharacter_IsRefusedAndNoSignupCarriesIt()
    {
        // Arrange
        var dm = await AuthenticationHelper.CreateTestUserAsync(_factory.Services, "charlinksdm2", "charlinksdm2@example.com");
        var quest = await TestDataHelper.CreateTestQuestAsync(
            _factory.Services, dm.Id, "Characters Off Quest 2", isFinalized: true, finalizedDate: DateTime.UtcNow.AddDays(7));
        await TestDataHelper.CreateProposedDateAsync(_factory.Services, quest.Id, quest.FinalizedDate!.Value);
        var (playerClient, player) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, "charlinks2", "charlinks2@example.com");
        var character = await TestDataHelper.CreateTestCharacterAsync(_factory.Services, player.Id, "Crafted Character");
        await SwitchCharactersOffAsync();

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["questId"] = quest.Id.ToString(),
            ["characterId"] = character.Id.ToString(),
            ["selectedRole"] = "0"
        });

        // Act
        var response = await playerClient.PostAsync("/Quest/JoinFinalizedQuest", form, TestContext.Current.CancellationToken);

        // Assert
        var followedHtml = await FollowRedirectAsync(playerClient, response);
        followedHtml.Should().Contain(InvalidCharacterMessage);
        (await SignupsForAsync(quest.Id, player.Id)).Should().BeEmpty();
    }

    [Fact]
    public async Task CharactersOff_OpenSignupPostWithoutCharacter_CreatesSignupWithNoCharacter()
    {
        // Arrange
        var dm = await AuthenticationHelper.CreateTestUserAsync(_factory.Services, "charlinksdm3", "charlinksdm3@example.com");
        var quest = await TestDataHelper.CreateTestQuestAsync(_factory.Services, dm.Id, "Characters Off Quest 3");
        var (playerClient, player) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, "charlinks3", "charlinks3@example.com");
        await SwitchCharactersOffAsync();

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Id"] = "0",
            ["Quest.Id"] = quest.Id.ToString(),
            ["selectedRole"] = "0"
        });

        // Act
        var response = await playerClient.PostAsync("/Quest/Details", form, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Found, HttpStatusCode.Redirect);
        var signups = await SignupsForAsync(quest.Id, player.Id);
        signups.Should().ContainSingle().Which.CharacterId.Should().BeNull();
    }

    [Fact]
    public async Task CharactersOff_FinalizedJoinWithoutCharacter_CreatesSignupWithNoCharacter()
    {
        // Arrange
        var dm = await AuthenticationHelper.CreateTestUserAsync(_factory.Services, "charlinksdm4", "charlinksdm4@example.com");
        var quest = await TestDataHelper.CreateTestQuestAsync(
            _factory.Services, dm.Id, "Characters Off Quest 4", isFinalized: true, finalizedDate: DateTime.UtcNow.AddDays(7));
        await TestDataHelper.CreateProposedDateAsync(_factory.Services, quest.Id, quest.FinalizedDate!.Value);
        var (playerClient, player) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, "charlinks4", "charlinks4@example.com");
        await SwitchCharactersOffAsync();

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["questId"] = quest.Id.ToString(),
            ["selectedRole"] = "0"
        });

        // Act
        var response = await playerClient.PostAsync("/Quest/JoinFinalizedQuest", form, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Found, HttpStatusCode.Redirect);
        var signups = await SignupsForAsync(quest.Id, player.Id);
        signups.Should().ContainSingle().Which.CharacterId.Should().BeNull();
    }

    [Fact]
    public async Task CharactersOff_UpdateSignupCharacter_IsRefusedAndTheSignupKeepsItsCharacter()
    {
        // Arrange
        var dm = await AuthenticationHelper.CreateTestUserAsync(_factory.Services, "charlinksdm5", "charlinksdm5@example.com");
        var quest = await TestDataHelper.CreateTestQuestAsync(_factory.Services, dm.Id, "Characters Off Quest 5");
        var (playerClient, player) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, "charlinks5", "charlinks5@example.com");
        var first = await TestDataHelper.CreateTestCharacterAsync(_factory.Services, player.Id, "First Character");
        var second = await TestDataHelper.CreateTestCharacterAsync(_factory.Services, player.Id, "Second Character");
        await TestDataHelper.CreatePlayerSignupAsync(_factory.Services, quest.Id, player.Id, characterId: first.Id);
        await SwitchCharactersOffAsync();

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["questId"] = quest.Id.ToString(),
            ["characterId"] = second.Id.ToString()
        });

        // Act
        var response = await playerClient.PostAsync("/Quest/UpdateSignupCharacter", form, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Found, HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().Should().EndWith("/quests");
        var signups = await SignupsForAsync(quest.Id, player.Id);
        signups.Should().ContainSingle().Which.CharacterId.Should().Be(first.Id);
    }

    [Fact]
    public async Task CharactersOn_OpenSignupPostWithOwnCharacter_AttachesIt()
    {
        // Arrange
        var dm = await AuthenticationHelper.CreateTestUserAsync(_factory.Services, "charlinksdm6", "charlinksdm6@example.com");
        var quest = await TestDataHelper.CreateTestQuestAsync(_factory.Services, dm.Id, "Characters On Quest 6");
        var (playerClient, player) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, "charlinks6", "charlinks6@example.com");
        var character = await TestDataHelper.CreateTestCharacterAsync(_factory.Services, player.Id, "Brought Character");

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Id"] = "0",
            ["Quest.Id"] = quest.Id.ToString(),
            ["CharacterId"] = character.Id.ToString(),
            ["selectedRole"] = "0"
        });

        // Act
        var response = await playerClient.PostAsync("/Quest/Details", form, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Found, HttpStatusCode.Redirect);
        var signups = await SignupsForAsync(quest.Id, player.Id);
        signups.Should().ContainSingle().Which.CharacterId.Should().Be(character.Id);
    }
}
