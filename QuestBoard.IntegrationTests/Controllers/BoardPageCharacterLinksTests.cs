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

    private const string MobileUserAgent =
        "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1";

    private const string DesktopUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

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

    private async Task<string> GetHtmlAsync(HttpClient client, string url, string userAgent)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("User-Agent", userAgent);
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
    }

    // Everything after the layout's navigation, so a link in the menu can never be mistaken for one
    // in the page itself.
    private static string PageBodyOf(string html)
    {
        var mainStart = html.IndexOf("<main", StringComparison.Ordinal);
        mainStart.Should().BeGreaterThan(0, "the layout renders a main element after its navigation");
        return html[mainStart..];
    }

    private async Task<(HttpClient Client, UserEntity Player, QuestEntity Quest, CharacterEntity Character)> SeedFinalizedQuestAsync(
        string suffix, bool playerHasSignup, bool signupHasCharacter, string characterName)
    {
        var dm = await AuthenticationHelper.CreateTestUserAsync(_factory.Services, $"charviewdm{suffix}", $"charviewdm{suffix}@example.com");
        var quest = await TestDataHelper.CreateTestQuestAsync(
            _factory.Services, dm.Id, $"Rendered Quest {suffix}", isFinalized: true, finalizedDate: DateTime.UtcNow.AddDays(7));
        await TestDataHelper.CreateProposedDateAsync(_factory.Services, quest.Id, quest.FinalizedDate!.Value);
        var (client, player) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, $"charview{suffix}", $"charview{suffix}@example.com");
        var character = await TestDataHelper.CreateTestCharacterAsync(_factory.Services, player.Id, characterName);
        if (playerHasSignup)
        {
            await TestDataHelper.CreatePlayerSignupAsync(
                _factory.Services, quest.Id, player.Id, isSelected: true, characterId: signupHasCharacter ? character.Id : null);
        }

        return (client, player, quest, character);
    }

    [Theory]
    [InlineData(DesktopUserAgent)]
    [InlineData(MobileUserAgent)]
    public async Task CharactersOff_FinalizedJoin_OffersNoCharacterChoiceAndEmitsNoScriptForIt(string userAgent)
    {
        // Arrange
        var (client, _, quest, _) = await SeedFinalizedQuestAsync("1", playerHasSignup: false, signupHasCharacter: false, "Hidden Hero");
        await SwitchCharactersOffAsync();

        // Act
        var body = PageBodyOf(await GetHtmlAsync(client, $"/Quest/Details/{quest.Id}", userAgent));

        // Assert
        body.Should().Contain("JoinFinalizedQuest", "the player can still join without a character");
        body.Should().NotContain("finalizedQuestCharacter");
        body.Should().NotContain("characterSelectModal");
        body.Should().NotContain("Hidden Hero");
    }

    [Theory]
    [InlineData(DesktopUserAgent, "id=\"finalizedQuestCharacter\"")]
    [InlineData(MobileUserAgent, "id=\"finalizedQuestCharacterMobile\"")]
    public async Task CharactersOn_FinalizedJoin_OffersTheSelectItsScriptAndTheModal(string userAgent, string selectMarkup)
    {
        // Arrange
        var (client, _, quest, _) = await SeedFinalizedQuestAsync("2", playerHasSignup: false, signupHasCharacter: false, "Visible Hero");

        // Act
        var body = PageBodyOf(await GetHtmlAsync(client, $"/Quest/Details/{quest.Id}", userAgent));

        // Assert
        body.Should().Contain(selectMarkup);
        body.Should().Contain("getElementById('finalizedQuestCharacter");
        body.Should().Contain("id=\"characterSelectModal\"");
        body.Should().Contain("Visible Hero");
    }

    [Theory]
    [InlineData(DesktopUserAgent)]
    [InlineData(MobileUserAgent)]
    public async Task CharactersOff_OpenQuest_OffersNoCharacterSelectOrModal(string userAgent)
    {
        // Arrange
        var dm = await AuthenticationHelper.CreateTestUserAsync(_factory.Services, "charviewdm3", "charviewdm3@example.com");
        var quest = await TestDataHelper.CreateTestQuestAsync(_factory.Services, dm.Id, "Open Rendered Quest 3");
        var (client, player) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, "charview3", "charview3@example.com");
        await TestDataHelper.CreateTestCharacterAsync(_factory.Services, player.Id, "Unoffered Hero");
        await SwitchCharactersOffAsync();

        // Act
        var body = PageBodyOf(await GetHtmlAsync(client, $"/Quest/Details/{quest.Id}", userAgent));

        // Assert
        body.Should().NotContain("name=\"CharacterId\"");
        body.Should().NotContain("characterSelectModal");
        body.Should().NotContain("Unoffered Hero");
    }

    [Fact]
    public async Task CharactersOn_OpenQuestOnDesktop_OffersTheCharacterSelect()
    {
        // Arrange
        var dm = await AuthenticationHelper.CreateTestUserAsync(_factory.Services, "charviewdm4", "charviewdm4@example.com");
        var quest = await TestDataHelper.CreateTestQuestAsync(_factory.Services, dm.Id, "Open Rendered Quest 4");
        var (client, player) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, "charview4", "charview4@example.com");
        await TestDataHelper.CreateTestCharacterAsync(_factory.Services, player.Id, "Offered Hero");

        // Act
        var body = PageBodyOf(await GetHtmlAsync(client, $"/Quest/Details/{quest.Id}", DesktopUserAgent));

        // Assert
        body.Should().Contain("name=\"CharacterId\"");
        body.Should().Contain("Offered Hero");
    }

    [Theory]
    [InlineData(DesktopUserAgent)]
    [InlineData(MobileUserAgent)]
    public async Task CharactersOff_ExistingSignupWithCharacter_KeepsNameAndPortraitButOffersNoChange(string userAgent)
    {
        // Arrange
        var (client, _, quest, _) = await SeedFinalizedQuestAsync("5", playerHasSignup: true, signupHasCharacter: true, "Veteran Hero");
        await SwitchCharactersOffAsync();

        // Act
        var body = PageBodyOf(await GetHtmlAsync(client, $"/Quest/Details/{quest.Id}", userAgent));

        // Assert
        body.Should().Contain("Veteran Hero");
        body.Should().NotContain("characterSelectModal");
        body.Should().NotContain("Change character");
        if (userAgent == DesktopUserAgent)
        {
            body.Should().Contain("/Characters/GetCroppedPicture/");
        }
    }

    [Theory]
    [InlineData(DesktopUserAgent)]
    [InlineData(MobileUserAgent)]
    public async Task CharactersOn_ExistingSignupWithCharacter_OffersTheChangeButtonAndModal(string userAgent)
    {
        // Arrange
        var (client, _, quest, _) = await SeedFinalizedQuestAsync("6", playerHasSignup: true, signupHasCharacter: true, "Veteran Hero");

        // Act
        var body = PageBodyOf(await GetHtmlAsync(client, $"/Quest/Details/{quest.Id}", userAgent));

        // Assert
        body.Should().Contain("Veteran Hero");
        body.Should().Contain("data-bs-target=\"#characterSelectModal\"");
        body.Should().Contain("Change character");
        body.Should().Contain("id=\"characterSelectModal\"");
    }

    [Theory]
    [InlineData(DesktopUserAgent)]
    [InlineData(MobileUserAgent)]
    public async Task CharactersOff_ExistingSignupWithoutCharacter_ShowsNoCharacterTextButNoAddButton(string userAgent)
    {
        // Arrange
        var (client, _, quest, _) = await SeedFinalizedQuestAsync("7", playerHasSignup: true, signupHasCharacter: false, "Benched Hero");
        await SwitchCharactersOffAsync();

        // Act
        var body = PageBodyOf(await GetHtmlAsync(client, $"/Quest/Details/{quest.Id}", userAgent));

        // Assert
        body.Should().Contain("No character");
        body.Should().NotContain("Add character");
        body.Should().NotContain("characterSelectModal");
    }

    [Theory]
    [InlineData(DesktopUserAgent)]
    [InlineData(MobileUserAgent)]
    public async Task CharactersOn_ExistingSignupWithoutCharacter_OffersTheAddButton(string userAgent)
    {
        // Arrange
        var (client, _, quest, _) = await SeedFinalizedQuestAsync("8", playerHasSignup: true, signupHasCharacter: false, "Benched Hero");

        // Act
        var body = PageBodyOf(await GetHtmlAsync(client, $"/Quest/Details/{quest.Id}", userAgent));

        // Assert
        body.Should().Contain("No character");
        body.Should().Contain("Add character");
    }

    private async Task<(HttpClient Client, UserEntity DungeonMaster)> SeedDungeonMasterViewerAsync()
    {
        return await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, "charlinksdmview", "charlinksdmview@example.com", name: "Ariadne Stormcaller", roles: ["DungeonMaster"]);
    }

    [Theory]
    [InlineData(DesktopUserAgent)]
    [InlineData(MobileUserAgent)]
    public async Task DungeonMasterProfileOff_PlayersPage_ListsDungeonMasterNamesAsPlainText(string userAgent)
    {
        // Arrange
        var (client, dm) = await SeedDungeonMasterViewerAsync();
        await BoardPageSeed.SetAsync(_factory, 1, BoardPageSet.Empty.With(BoardPage.DungeonMasterProfile), BoardPage.Quests);

        // Act
        var body = PageBodyOf(await GetHtmlAsync(client, "/Players", userAgent));

        // Assert
        body.Should().Contain("Ariadne Stormcaller");
        body.Should().NotContain("DungeonMaster/Profile");
        body.Should().NotContain("fa-chevron-right");
    }

    [Theory]
    [InlineData(DesktopUserAgent)]
    [InlineData(MobileUserAgent)]
    public async Task DungeonMasterProfileOn_PlayersPage_LinksDungeonMasterNamesToTheirProfile(string userAgent)
    {
        // Arrange
        var (client, dm) = await SeedDungeonMasterViewerAsync();

        // Act
        var body = PageBodyOf(await GetHtmlAsync(client, "/Players", userAgent));

        // Assert
        body.Should().Contain("Ariadne Stormcaller");
        body.Should().Contain($"/DungeonMaster/Profile/{dm.Id}");
    }
}
