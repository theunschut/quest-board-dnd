using System.Net;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using QuestBoard.IntegrationTests.Helpers;

namespace QuestBoard.IntegrationTests.Mobile;

public class QuestDetailsMobileCharacterControlTests : IClassFixture<WebApplicationFactoryBase>
{
    private const string MobileUserAgent =
        "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1";

    private const string DesktopUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

    private readonly WebApplicationFactoryBase _factory;
    private readonly HttpClient _client;

    public QuestDetailsMobileCharacterControlTests(WebApplicationFactoryBase factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<(HttpResponseMessage Response, string Html)> GetQuestDetailsAsync(
        int questId, string userAgent, AuthenticationHeaderValue? authorization = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/Quest/Details/{questId}");
        request.Headers.TryAddWithoutValidation("User-Agent", userAgent);
        if (authorization != null)
        {
            request.Headers.Authorization = authorization;
        }
        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        return (response, html);
    }

    /// <summary>
    /// The mobile/desktop split is driven entirely by the request's User-Agent string, not by
    /// viewport size. A browser device-toolbar emulation check would never exercise the
    /// middleware or view-location expander that actually pick which file renders, so this
    /// test sends a real mobile User-Agent header and proves the mobile markup is the one
    /// genuinely served, then proves a desktop User-Agent does not receive it.
    /// </summary>
    [Fact]
    public async Task MobileDetails_MobileUserAgentSelectsTheMobileView_AndDesktopUserAgentDoesNot()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var dm = await AuthenticationHelper.CreateTestUserAsync(_factory.Services, "mccdm1", "mccdm1@example.com");
        var quest = await TestDataHelper.CreateTestQuestAsync(
            _factory.Services, dm.Id, "Mobile Split Quest", isFinalized: true, finalizedDate: DateTime.UtcNow.AddDays(7));
        await TestDataHelper.CreateProposedDateAsync(_factory.Services, quest.Id, quest.FinalizedDate!.Value);

        var (authClient, player) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, "mccplayer1", "mccplayer1@example.com");
        await TestDataHelper.CreatePlayerSignupAsync(_factory.Services, quest.Id, player.Id, isSelected: true);

        var (mobileResponse, mobileHtml) = await GetQuestDetailsAsync(
            quest.Id, MobileUserAgent, authClient.DefaultRequestHeaders.Authorization);
        var (desktopResponse, desktopHtml) = await GetQuestDetailsAsync(
            quest.Id, DesktopUserAgent, authClient.DefaultRequestHeaders.Authorization);

        mobileResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        desktopResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        mobileHtml.Should().Contain("participant-row");
        desktopHtml.Should().NotContain("participant-row");
    }

    [Fact]
    public async Task MobileDetails_MobileUserAgent_WhenOwnSignupHasCharacter_RendersTriggerCarryingThatCharacterId()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var dm = await AuthenticationHelper.CreateTestUserAsync(_factory.Services, "mccdm2", "mccdm2@example.com");
        var quest = await TestDataHelper.CreateTestQuestAsync(
            _factory.Services, dm.Id, "Mobile Trigger Quest", isFinalized: true, finalizedDate: DateTime.UtcNow.AddDays(7));
        await TestDataHelper.CreateProposedDateAsync(_factory.Services, quest.Id, quest.FinalizedDate!.Value);

        var (authClient, player) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, "mccplayer2", "mccplayer2@example.com");
        var character = await TestDataHelper.CreateTestCharacterAsync(_factory.Services, player.Id, "Trigger Bearer");
        await TestDataHelper.CreatePlayerSignupAsync(
            _factory.Services, quest.Id, player.Id, isSelected: true, characterId: character.Id);

        var (response, html) = await GetQuestDetailsAsync(
            quest.Id, MobileUserAgent, authClient.DefaultRequestHeaders.Authorization);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain($"data-current-character-id=\"{character.Id}\"");
        html.Should().Contain("data-bs-target=\"#characterSelectModal\"");
    }

    [Fact]
    public async Task MobileDetails_MobileUserAgent_WhenOwnSignupHasNoCharacter_RendersAddTriggerWithEmptyCurrentCharacterId()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var dm = await AuthenticationHelper.CreateTestUserAsync(_factory.Services, "mccdm3", "mccdm3@example.com");
        var quest = await TestDataHelper.CreateTestQuestAsync(
            _factory.Services, dm.Id, "Mobile Add Quest", isFinalized: true, finalizedDate: DateTime.UtcNow.AddDays(7));
        await TestDataHelper.CreateProposedDateAsync(_factory.Services, quest.Id, quest.FinalizedDate!.Value);

        var (authClient, player) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, "mccplayer3", "mccplayer3@example.com");
        await TestDataHelper.CreateTestCharacterAsync(_factory.Services, player.Id, "Owned But Unassigned");
        await TestDataHelper.CreatePlayerSignupAsync(_factory.Services, quest.Id, player.Id, isSelected: true);

        var (response, html) = await GetQuestDetailsAsync(
            quest.Id, MobileUserAgent, authClient.DefaultRequestHeaders.Authorization);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("data-current-character-id=\"\"");
    }

    [Fact]
    public async Task MobileDetails_MobileUserAgent_RendersTheSharedModalExactlyOnce()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var dm = await AuthenticationHelper.CreateTestUserAsync(_factory.Services, "mccdm4", "mccdm4@example.com");
        var quest = await TestDataHelper.CreateTestQuestAsync(
            _factory.Services, dm.Id, "Mobile Modal Count Quest", isFinalized: true, finalizedDate: DateTime.UtcNow.AddDays(7));
        await TestDataHelper.CreateProposedDateAsync(_factory.Services, quest.Id, quest.FinalizedDate!.Value);

        var (authClient, player) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, "mccplayer4", "mccplayer4@example.com");
        var character = await TestDataHelper.CreateTestCharacterAsync(_factory.Services, player.Id, "Modal Count Character");
        await TestDataHelper.CreatePlayerSignupAsync(
            _factory.Services, quest.Id, player.Id, isSelected: true, characterId: character.Id);

        var (response, html) = await GetQuestDetailsAsync(
            quest.Id, MobileUserAgent, authClient.DefaultRequestHeaders.Authorization);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var modalRootIdMatches = Regex.Matches(html, "id=\"characterSelectModal\"");
        modalRootIdMatches.Count.Should().Be(1);
    }

    [Fact]
    public async Task MobileDetails_MobileUserAgent_WhenSignupHoldsARetiredCharacter_RendersTheTriggerCarryingThatCharacterId()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var dm = await AuthenticationHelper.CreateTestUserAsync(_factory.Services, "mccdm5", "mccdm5@example.com");
        var quest = await TestDataHelper.CreateTestQuestAsync(
            _factory.Services, dm.Id, "Mobile Retired Quest", isFinalized: true, finalizedDate: DateTime.UtcNow.AddDays(7));
        await TestDataHelper.CreateProposedDateAsync(_factory.Services, quest.Id, quest.FinalizedDate!.Value);

        var (authClient, player) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, "mccplayer5", "mccplayer5@example.com");
        var retiredCharacter = await TestDataHelper.CreateTestCharacterAsync(
            _factory.Services, player.Id, "Retired Trigger Bearer", status: 1);
        await TestDataHelper.CreatePlayerSignupAsync(
            _factory.Services, quest.Id, player.Id, isSelected: true, characterId: retiredCharacter.Id);

        var (response, html) = await GetQuestDetailsAsync(
            quest.Id, MobileUserAgent, authClient.DefaultRequestHeaders.Authorization);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain($"data-current-character-id=\"{retiredCharacter.Id}\"");
        html.Should().Contain("(Retired)");
    }

    [Fact]
    public async Task MobileDetails_MobileUserAgent_ForAWaitlistedSignupWithCharacter_RendersTheTrigger()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var dm = await AuthenticationHelper.CreateTestUserAsync(_factory.Services, "mccdm6", "mccdm6@example.com");
        var quest = await TestDataHelper.CreateTestQuestAsync(
            _factory.Services, dm.Id, "Mobile Waitlist Quest", isFinalized: true, finalizedDate: DateTime.UtcNow.AddDays(7));
        await TestDataHelper.CreateProposedDateAsync(_factory.Services, quest.Id, quest.FinalizedDate!.Value);

        var (authClient, player) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, "mccplayer6", "mccplayer6@example.com");
        var character = await TestDataHelper.CreateTestCharacterAsync(_factory.Services, player.Id, "Waitlist Trigger Bearer");
        await TestDataHelper.CreatePlayerSignupAsync(
            _factory.Services, quest.Id, player.Id, isSelected: false, characterId: character.Id);

        var (response, html) = await GetQuestDetailsAsync(
            quest.Id, MobileUserAgent, authClient.DefaultRequestHeaders.Authorization);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain($"data-current-character-id=\"{character.Id}\"");
    }

    [Fact]
    public async Task MobileDetails_MobileUserAgent_ForAnotherPlayersRow_RendersNoTriggerForThatRow()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var dm = await AuthenticationHelper.CreateTestUserAsync(_factory.Services, "mccdm7", "mccdm7@example.com");
        var quest = await TestDataHelper.CreateTestQuestAsync(
            _factory.Services, dm.Id, "Mobile Other Player Quest", isFinalized: true, finalizedDate: DateTime.UtcNow.AddDays(7));
        await TestDataHelper.CreateProposedDateAsync(_factory.Services, quest.Id, quest.FinalizedDate!.Value);

        var otherPlayer = await AuthenticationHelper.CreateTestUserAsync(_factory.Services, "mccother7", "mccother7@example.com");
        var otherCharacter = await TestDataHelper.CreateTestCharacterAsync(_factory.Services, otherPlayer.Id, "Other Player's Character");
        await TestDataHelper.CreatePlayerSignupAsync(
            _factory.Services, quest.Id, otherPlayer.Id, isSelected: true, characterId: otherCharacter.Id);

        var (authClient, viewer) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, "mccviewer7", "mccviewer7@example.com");
        var viewerCharacter = await TestDataHelper.CreateTestCharacterAsync(_factory.Services, viewer.Id, "Viewer's Own Character");
        await TestDataHelper.CreatePlayerSignupAsync(
            _factory.Services, quest.Id, viewer.Id, isSelected: true, characterId: viewerCharacter.Id);

        var (response, html) = await GetQuestDetailsAsync(
            quest.Id, MobileUserAgent, authClient.DefaultRequestHeaders.Authorization);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var currentCharacterIdMatches = Regex.Matches(html, "data-current-character-id=\"(\\d+)\"");
        var renderedCharacterIds = currentCharacterIdMatches
            .Select(m => int.Parse(m.Groups[1].Value))
            .ToList();
        renderedCharacterIds.Should().NotContain(otherCharacter.Id);
        renderedCharacterIds.Should().Contain(viewerCharacter.Id);
    }
}
