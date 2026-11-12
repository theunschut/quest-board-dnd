using System.Net;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.IntegrationTests.Helpers;

namespace QuestBoard.IntegrationTests.Mobile;

/// <summary>
/// Integration test stubs for the mobile view requirements.
/// Tests start RED (mobile views do not exist yet) and go GREEN as Wave 1 plans land.
/// This establishes the Nyquist sampling harness before any implementation.
/// </summary>
public class MobileViewsTests : IClassFixture<WebApplicationFactoryBase>
{
    private const string MobileUserAgent =
        "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1";

    private const string DesktopUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

    private readonly WebApplicationFactoryBase _factory;
    private readonly HttpClient _client;

    public MobileViewsTests(WebApplicationFactoryBase factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<(HttpResponseMessage Response, string Html)> GetWithUserAgentAsync(string url, string userAgent)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("User-Agent", userAgent);
        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        return (response, html);
    }

    // /Calendar, /QuestLog, and the quest board (now /quests) all require
    // authentication after lockdown — this overload attaches the Test scheme
    // Authorization header from an authenticated client so mobile-view tests can still assert
    // on the rendered markup instead of hitting the login redirect.
    private async Task<(HttpResponseMessage Response, string Html)> GetWithUserAgentAsync(
        string url, string userAgent, AuthenticationHeaderValue? authorization)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
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
    /// Mobile UA renders the quest-card-mobile list layout instead of poster images.
    /// The board moved from / to /quests and now requires authentication.
    /// </summary>
    [Fact]
    public async Task MobileHome_MobileUserAgent_RendersCardListNotPosterImages()
    {
        // Seed a quest so the card list renders (not the empty state)
        var dm = await AuthenticationHelper.CreateTestUserAsync(_factory.Services, "dm_home01", "dm_home01@test.com", name: "DM Home01");
        await TestDataHelper.CreateTestQuestAsync(_factory.Services, dm.Id, "Open Quest Home01");
        var (authClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, "player_home01", "player_home01@test.com");

        var (response, html) = await GetWithUserAgentAsync("/quests", MobileUserAgent, authClient.DefaultRequestHeaders.Authorization);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("quest-card-mobile");
        html.Should().NotContain("fantasy-quest-card");
        html.Should().NotContain("Blanks w Shadow");
    }

    /// <summary>
    /// Desktop UA does NOT get the mobile card layout.
    /// </summary>
    [Fact]
    public async Task MobileHome_DesktopUserAgent_DoesNotRenderMobileCardList()
    {
        var (response, html) = await GetWithUserAgentAsync("/", DesktopUserAgent);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().NotContain("quest-card-mobile");
    }

    /// <summary>
    /// Quest card shows CR badge and status badge.
    /// The board moved from / to /quests and now requires authentication.
    /// </summary>
    [Fact]
    public async Task MobileHome_MobileUserAgent_QuestCardContainsCrAndStatusBadge()
    {
        var dm = await AuthenticationHelper.CreateTestUserAsync(_factory.Services, "dm_home02", "dm_home02@test.com", name: "DM Home02");
        await TestDataHelper.CreateTestQuestAsync(_factory.Services, dm.Id, "The Lost Mine", challengeRating: 3);
        var (authClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, "player_home02", "player_home02@test.com");

        var (response, html) = await GetWithUserAgentAsync("/quests", MobileUserAgent, authClient.DefaultRequestHeaders.Authorization);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("The Lost Mine");
        html.Should().Contain("CR 3");
        html.Should().ContainAny("bg-success", "bg-primary", "bg-secondary");
    }

    /// <summary>
    /// A phone card carries the adventurer count in the poster's words, and no count on a Campaign board.
    /// </summary>
    [Fact]
    public async Task MobileHome_MobileUserAgent_QuestCardShowsAdventurerCount()
    {
        var dm = await AuthenticationHelper.CreateTestUserAsync(_factory.Services, "dm_home05", "dm_home05@test.com", name: "DM Home05");
        var quest = await TestDataHelper.CreateTestQuestAsync(_factory.Services, dm.Id, "Counted Adventure Home05");
        var (authClient, playerUser) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, "player_home05", "player_home05@test.com");
        await TestDataHelper.CreatePlayerSignupAsync(_factory.Services, quest.Id, playerUser.Id, isSelected: false);

        var (response, html) = await GetWithUserAgentAsync("/quests", MobileUserAgent, authClient.DefaultRequestHeaders.Authorization);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var card = QuestCardContaining(html, "Counted Adventure Home05");
        card.Should().Contain("Adventurers signed up:</strong> 1");
        card.Should().Contain("DM:</strong> DM Home05");
        card.Should().NotContain("onclick");

        // The board type is read from the active group's stored row, so the Campaign case needs a Campaign group.
        await TestDataHelper.SeedCampaignGroupAsync(_factory.Services, groupId: 2);
        await TestDataHelper.CreateTestQuestAsync(_factory.Services, dm.Id, "Campaign Card Home05", groupId: 2);
        try
        {
            _factory.TestGroupContext.ActiveGroupId = 2;
            _factory.TestGroupContext.BoardType = BoardType.Campaign;
            var (campaignResponse, campaignHtml) = await GetWithUserAgentAsync("/quests", MobileUserAgent, authClient.DefaultRequestHeaders.Authorization);
            campaignResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var campaignCard = QuestCardContaining(campaignHtml, "Campaign Card Home05");
            campaignCard.Should().Contain("DM:</strong>");
            campaignCard.Should().NotContain("Adventurers signed up");
            campaignCard.Should().NotContain("Selected Adventurers");
        }
        finally
        {
            _factory.TestGroupContext.ActiveGroupId = 1;
            _factory.TestGroupContext.BoardType = BoardType.OneShot;
        }
    }

    private static string QuestCardContaining(string html, string title)
    {
        var cards = html.Split("<div class=\"quest-card-mobile", StringSplitOptions.None).Skip(1);
        return cards.Single(c => c.Contains(title));
    }

    /// <summary>
    /// Finalized quest shows the primary badge (date confirmed, future date).
    /// Note: repository filters out finalized quests with null or past FinalizedDate;
    /// a future FinalizedDate is required for the quest to appear on the board.
    /// The board moved from / to /quests and now requires authentication.
    /// </summary>
    [Fact]
    public async Task MobileHome_MobileUserAgent_FinalizedQuestShowsDate()
    {
        var dm = await AuthenticationHelper.CreateTestUserAsync(_factory.Services, "dm_home02b", "dm_home02b@test.com", name: "DM Home02b");
        await TestDataHelper.CreateTestQuestAsync(_factory.Services, dm.Id, "Finalized Adventure",
            isFinalized: true, finalizedDate: DateTime.UtcNow.AddDays(7));
        var (authClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, "player_home02b", "player_home02b@test.com");

        var (response, html) = await GetWithUserAgentAsync("/quests", MobileUserAgent, authClient.DefaultRequestHeaders.Authorization);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("Finalized Adventure");
        html.Should().Contain("bg-primary");
    }

    /// <summary>
    /// Quest card links to /Quest/Details/{id} (non-DM user navigates to details).
    /// The board moved from / to /quests and now requires authentication.
    /// </summary>
    [Fact]
    public async Task MobileHome_MobileUserAgent_QuestCardLinksToDetails()
    {
        var dm = await AuthenticationHelper.CreateTestUserAsync(_factory.Services, "dm_home03", "dm_home03@test.com", name: "DM Home03");
        var quest = await TestDataHelper.CreateTestQuestAsync(_factory.Services, dm.Id, "Navigation Quest");
        var (authClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, "player_home03", "player_home03@test.com");

        var (response, html) = await GetWithUserAgentAsync("/quests", MobileUserAgent, authClient.DefaultRequestHeaders.Authorization);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain($"/Quest/Details/{quest.Id}");
    }

    /// <summary>
    /// Signed-up badge appears for authenticated player who has signed up for a quest.
    /// The board moved from / to /quests and now requires authentication.
    /// </summary>
    [Fact]
    public async Task MobileHome_AuthenticatedSignedUpPlayer_ShowsSignedUpBadge()
    {
        var dm = await AuthenticationHelper.CreateTestUserAsync(_factory.Services, "dm_home04", "dm_home04@test.com", name: "DM Home04");
        var quest = await TestDataHelper.CreateTestQuestAsync(_factory.Services, dm.Id, "Signup Badge Quest");
        var (authClient, playerUser) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(_factory, "player_home04", "player_home04@test.com");
        await TestDataHelper.CreatePlayerSignupAsync(_factory.Services, quest.Id, playerUser.Id, isSelected: false);

        var request = new HttpRequestMessage(HttpMethod.Get, "/quests");
        request.Headers.TryAddWithoutValidation("User-Agent", MobileUserAgent);
        request.Headers.Authorization = authClient.DefaultRequestHeaders.Authorization;
        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("Signed up");
    }

    /// <summary>
    /// Vote buttons (Yes/No/Maybe) are present on Quest Details when viewed on mobile.
    /// Also checks that quests.mobile.css is linked.
    /// </summary>
    [Fact]
    public async Task MobileQuestDetails_MobileUserAgent_RendersVoteButtons()
    {
        var dm = await AuthenticationHelper.CreateTestUserAsync(_factory.Services, "dm_qview01", "dm_qview01@test.com", name: "DM Qview01");
        var quest = await TestDataHelper.CreateTestQuestAsync(_factory.Services, dm.Id, "Vote Quest");
        await TestDataHelper.CreateProposedDateAsync(_factory.Services, quest.Id, DateTime.UtcNow.AddDays(7));
        var (authClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(_factory, "player_qview01", "player_qview01@test.com");

        var request = new HttpRequestMessage(HttpMethod.Get, $"/Quest/Details/{quest.Id}");
        request.Headers.TryAddWithoutValidation("User-Agent", MobileUserAgent);
        request.Headers.Authorization = authClient.DefaultRequestHeaders.Authorization;
        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("btn-check");
        html.Should().Contain("quests.mobile.css");
    }

    /// <summary>
    /// Participant list is rendered as stacked rows instead of a responsive table.
    /// </summary>
    [Fact]
    public async Task MobileQuestDetails_MobileUserAgent_ParticipantListIsStacked()
    {
        var dm = await AuthenticationHelper.CreateTestUserAsync(_factory.Services, "dm_qview02", "dm_qview02@test.com", name: "DM Qview02");
        var quest = await TestDataHelper.CreateTestQuestAsync(_factory.Services, dm.Id, "Stacked Quest", isFinalized: true);
        var player = await AuthenticationHelper.CreateTestUserAsync(_factory.Services, "player_qview02a", "player_qview02a@test.com", name: "Player Alpha");
        await TestDataHelper.CreatePlayerSignupAsync(_factory.Services, quest.Id, player.Id, isSelected: true);
        var (authClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(_factory, "player_qview02b", "player_qview02b@test.com");

        var request = new HttpRequestMessage(HttpMethod.Get, $"/Quest/Details/{quest.Id}");
        request.Headers.TryAddWithoutValidation("User-Agent", MobileUserAgent);
        request.Headers.Authorization = authClient.DefaultRequestHeaders.Authorization;
        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("participant-row");
        html.Should().NotContain("table-responsive");
        html.Should().Contain("Player Alpha");
    }

    /// <summary>
    /// Quest Log mobile view renders a list with quest title and DM name.
    /// /QuestLog now requires authentication.
    /// </summary>
    [Fact]
    public async Task MobileQuestLog_MobileUserAgent_RendersListWithTitleAndDmName()
    {
        var dm = await AuthenticationHelper.CreateTestUserAsync(_factory.Services, "dm_qview03", "dm_qview03@test.com", name: "DM Qview03");
        await TestDataHelper.CreateTestQuestAsync(_factory.Services, dm.Id, "Ancient Dungeon", isFinalized: true, finalizedDate: DateTime.UtcNow.AddDays(-2));
        var (authClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, "player_qview03", "player_qview03@test.com");

        var (response, html) = await GetWithUserAgentAsync("/QuestLog", MobileUserAgent, authClient.DefaultRequestHeaders.Authorization);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("quest-log-item");
        html.Should().Contain("Ancient Dungeon");
        html.Should().Contain("DM Qview03");
    }

    /// <summary>
    /// Mobile Quest Log page includes a link to quest-log.mobile.css.
    /// /QuestLog now requires authentication.
    /// </summary>
    [Fact]
    public async Task MobileQuestLog_MobileUserAgent_LoadsMobileCssLink()
    {
        var (authClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, "player_qview03b", "player_qview03b@test.com");

        var (response, html) = await GetWithUserAgentAsync("/QuestLog", MobileUserAgent, authClient.DefaultRequestHeaders.Authorization);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("quest-log.mobile.css");
    }

    /// <summary>
    /// calendar.mobile.css is linked from /Calendar on mobile.
    /// /Calendar now requires authentication.
    /// </summary>
    [Fact]
    public async Task MobileCalendar_MobileUserAgent_LoadsMobileCssLink()
    {
        var (authClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, "player_calcss", "player_calcss@test.com");

        var (response, html) = await GetWithUserAgentAsync("/Calendar", MobileUserAgent, authClient.DefaultRequestHeaders.Authorization);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("calendar.mobile.css");
    }

    /// <summary>
    /// Mobile UA on /Calendar renders agenda list (contains agenda-quest-entry, no calendar-grid).
    /// /Calendar now requires authentication.
    /// </summary>
    [Fact]
    public async Task MobileCalendar_MobileUserAgent_RendersAgendaList()
    {
        var dm = await AuthenticationHelper.CreateTestUserAsync(_factory.Services, "dm_cal01", "dm_cal01@test.com", name: "DM Cal01");
        var quest = await TestDataHelper.CreateTestQuestAsync(_factory.Services, dm.Id, "Calendar Quest CAL01");
        await TestDataHelper.CreateProposedDateAsync(_factory.Services, quest.Id,
            new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 15, 19, 0, 0, DateTimeKind.Utc));
        var (authClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, "player_cal01", "player_cal01@test.com");

        var (response, html) = await GetWithUserAgentAsync(
            $"/Calendar?year={DateTime.UtcNow.Year}&month={DateTime.UtcNow.Month}", MobileUserAgent, authClient.DefaultRequestHeaders.Authorization);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("agenda-quest-entry");
        html.Should().NotContain("calendar-grid");
    }

    /// <summary>
    /// Agenda entry contains day label in uppercase day-name format and time.
    /// /Calendar now requires authentication.
    /// </summary>
    [Fact]
    public async Task MobileCalendar_MobileUserAgent_AgendaEntryContainsDayLabelAndTime()
    {
        var dm = await AuthenticationHelper.CreateTestUserAsync(_factory.Services, "dm_cal02", "dm_cal02@test.com", name: "DM Cal02");
        var quest = await TestDataHelper.CreateTestQuestAsync(_factory.Services, dm.Id, "Calendar Quest CAL02");
        var knownDate = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 15, 19, 0, 0, DateTimeKind.Utc);
        await TestDataHelper.CreateProposedDateAsync(_factory.Services, quest.Id, knownDate);
        var (authClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, "player_cal02", "player_cal02@test.com");

        var (response, html) = await GetWithUserAgentAsync(
            $"/Calendar?year={DateTime.UtcNow.Year}&month={DateTime.UtcNow.Month}", MobileUserAgent, authClient.DefaultRequestHeaders.Authorization);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("agenda-day-label");
        // Day label format is "SATURDAY, JUNE 14" — assert at least the time portion is present
        html.Should().Contain("19:00");
    }

    /// <summary>
    /// Desktop UA on /Calendar does NOT render agenda list.
    /// </summary>
    [Fact]
    public async Task MobileCalendar_DesktopUserAgent_DoesNotRenderAgendaList()
    {
        var (response, html) = await GetWithUserAgentAsync("/Calendar", DesktopUserAgent);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().NotContain("agenda-quest-entry");
    }

    /// <summary>
    /// Agenda entry links to /Quest/Details/{id}.
    /// /Calendar now requires authentication.
    /// </summary>
    [Fact]
    public async Task MobileCalendar_MobileUserAgent_AgendaEntryLinksToDetails()
    {
        var dm = await AuthenticationHelper.CreateTestUserAsync(_factory.Services, "dm_cal04", "dm_cal04@test.com", name: "DM Cal04");
        var quest = await TestDataHelper.CreateTestQuestAsync(_factory.Services, dm.Id, "Calendar Quest CAL04");
        // Use day 5 of the current month so the date stays within the queried month regardless of when the test runs.
        var cal04Date = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 5, 19, 0, 0, DateTimeKind.Utc);
        await TestDataHelper.CreateProposedDateAsync(_factory.Services, quest.Id, cal04Date);
        var (authClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, "player_cal04", "player_cal04@test.com");

        var (response, html) = await GetWithUserAgentAsync(
            $"/Calendar?year={DateTime.UtcNow.Year}&month={DateTime.UtcNow.Month}", MobileUserAgent, authClient.DefaultRequestHeaders.Authorization);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain($"/Quest/Details/{quest.Id}");
    }

    /// <summary>
    /// _Calendar.Mobile.cshtml partial renders per-date vote buttons on Quest Details mobile.
    /// Authenticated player not yet signed up — should see btn-check radio inputs.
    /// </summary>
    [Fact]
    public async Task MobileCalendar_MobileUserAgent_CalendarPartialRendersVoteButtons()
    {
        var dm = await AuthenticationHelper.CreateTestUserAsync(_factory.Services, "dm_cal05", "dm_cal05@test.com", name: "DM Cal05");
        var quest = await TestDataHelper.CreateTestQuestAsync(_factory.Services, dm.Id, "Calendar Quest CAL05");
        await TestDataHelper.CreateProposedDateAsync(_factory.Services, quest.Id, DateTime.UtcNow.AddDays(5));
        var (authClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, "player_cal05", "player_cal05@test.com");

        var request = new HttpRequestMessage(HttpMethod.Get, $"/Quest/Details/{quest.Id}");
        request.Headers.TryAddWithoutValidation("User-Agent", MobileUserAgent);
        request.Headers.Authorization = authClient.DefaultRequestHeaders.Authorization;
        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("btn-check");
        html.Should().Contain("calendar-date-entry-mobile");
    }

    /// <summary>
    /// Quest Create on mobile renders single-column glass card form (dm-create-card-mobile).
    /// Also checks that dm-create.mobile.css is linked.
    /// </summary>
    [Fact]
    public async Task MobileDmCreate_MobileUserAgent_RendersGlassCardForm()
    {
        var (authClient, dmUser) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, "dm_dmview01", "dm_dmview01@test.com", roles: new[] { "DungeonMaster" });

        var request = new HttpRequestMessage(HttpMethod.Get, "/Quest/Create");
        request.Headers.TryAddWithoutValidation("User-Agent", MobileUserAgent);
        request.Headers.Authorization = authClient.DefaultRequestHeaders.Authorization;
        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("dm-create-card-mobile");
        html.Should().Contain("dm-create.mobile.css");
    }

    /// <summary>
    /// Quest Manage on mobile renders condensed vote badges (manage-date-option, dm-vote-summary).
    /// Also checks that dm-manage.mobile.css is linked.
    /// </summary>
    [Fact]
    public async Task MobileDmManage_MobileUserAgent_RendersCondensedVoteBadges()
    {
        var (authClient, dmUser) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, "dm_dmview02", "dm_dmview02@test.com", roles: new[] { "DungeonMaster" });
        var quest = await TestDataHelper.CreateTestQuestAsync(_factory.Services, dmUser.Id, "Manage Mobile Quest DM02");
        await TestDataHelper.CreateProposedDateAsync(_factory.Services, quest.Id, DateTime.UtcNow.AddDays(7));

        var request = new HttpRequestMessage(HttpMethod.Get, $"/Quest/Manage/{quest.Id}");
        request.Headers.TryAddWithoutValidation("User-Agent", MobileUserAgent);
        request.Headers.Authorization = authClient.DefaultRequestHeaders.Authorization;
        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("manage-date-option");
        html.Should().Contain("dm-vote-summary");
        html.Should().Contain("dm-manage.mobile.css");
    }

    /// <summary>
    /// The phone Manage page of an open quest carries a Quick Stats section above the date voting results,
    /// with the same figures the desktop sidebar shows.
    /// </summary>
    [Fact]
    public async Task MobileDmManage_OpenQuest_RendersQuickStatsAboveTheVotingResults()
    {
        var (authClient, dmUser) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, "dm_dmview04", "dm_dmview04@test.com", roles: new[] { "DungeonMaster" });
        var quest = await TestDataHelper.CreateTestQuestAsync(_factory.Services, dmUser.Id, "Manage Mobile Quest DM04");
        await TestDataHelper.CreateProposedDateAsync(_factory.Services, quest.Id, DateTime.UtcNow.AddDays(7));

        var html = await GetManageAsMobileAsync(authClient, quest.Id);

        html.Should().Contain("<h2>Quick Stats</h2>");
        html.Should().Contain("Best Date:");
        html.Should().Contain("players available");
        html.IndexOf("<h2>Quick Stats</h2>", StringComparison.Ordinal)
            .Should().BeLessThan(html.IndexOf("Voting Results</h2>", StringComparison.Ordinal),
                "Quick Stats sits directly above the proposed dates");
    }

    /// <summary>
    /// A finalized quest viewed by an Admin on the phone renders one Remove control for each selected
    /// participant, the same control the desktop table carries.
    /// </summary>
    [Fact]
    public async Task MobileDmManage_FinalizedQuestViewedByAdmin_RendersARemoveControlPerSelectedParticipant()
    {
        var (adminClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, "admin_dmview05", "admin_dmview05@test.com", roles: new[] { "Admin" });
        var dm = await AuthenticationHelper.CreateTestUserAsync(_factory.Services, "dm_dmview05", "dm_dmview05@test.com", name: "DM View05");
        var finalizedDate = DateTime.UtcNow.AddDays(7);
        var quest = await TestDataHelper.CreateTestQuestAsync(_factory.Services, dm.Id, "Manage Mobile Quest DM05", isFinalized: true, finalizedDate: finalizedDate);
        await TestDataHelper.CreateProposedDateAsync(_factory.Services, quest.Id, finalizedDate);
        var first = await AuthenticationHelper.CreateTestUserAsync(_factory.Services, "player_dmview05a", "player_dmview05a@test.com", name: "Player View05A");
        var second = await AuthenticationHelper.CreateTestUserAsync(_factory.Services, "player_dmview05b", "player_dmview05b@test.com", name: "Player View05B");
        var firstSignup = await TestDataHelper.CreatePlayerSignupAsync(_factory.Services, quest.Id, first.Id, isSelected: true);
        var secondSignup = await TestDataHelper.CreatePlayerSignupAsync(_factory.Services, quest.Id, second.Id, isSelected: true);

        var html = await GetManageAsMobileAsync(adminClient, quest.Id);

        html.Should().Contain("<h2>Selected Participants</h2>");
        html.Should().Contain($"data-action=\"remove-player\" data-signup-id=\"{firstSignup.Id}\"");
        html.Should().Contain($"data-action=\"remove-player\" data-signup-id=\"{secondSignup.Id}\"");
        Regex.Matches(html, "data-action=\"remove-player\"").Count.Should().Be(2);
    }

    /// <summary>
    /// The quest's own Dungeon Master is not an Admin, so the same finalized phone page carries no Remove
    /// control for the participants; the server renders it only under the Admin condition.
    /// </summary>
    [Fact]
    public async Task MobileDmManage_FinalizedQuestViewedByItsDungeonMaster_RendersNoRemoveControl()
    {
        var (dmClient, dmUser) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, "dm_dmview06", "dm_dmview06@test.com", roles: new[] { "DungeonMaster" });
        var finalizedDate = DateTime.UtcNow.AddDays(7);
        var quest = await TestDataHelper.CreateTestQuestAsync(_factory.Services, dmUser.Id, "Manage Mobile Quest DM06", isFinalized: true, finalizedDate: finalizedDate);
        await TestDataHelper.CreateProposedDateAsync(_factory.Services, quest.Id, finalizedDate);
        var player = await AuthenticationHelper.CreateTestUserAsync(_factory.Services, "player_dmview06", "player_dmview06@test.com", name: "Player View06");
        await TestDataHelper.CreatePlayerSignupAsync(_factory.Services, quest.Id, player.Id, isSelected: true);

        var html = await GetManageAsMobileAsync(dmClient, quest.Id);

        html.Should().Contain("<h2>Selected Participants</h2>");
        html.Should().NotContain("data-action=\"remove-player\"");
    }

    private async Task<string> GetManageAsMobileAsync(HttpClient authClient, int questId)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/Quest/Manage/{questId}");
        request.Headers.TryAddWithoutValidation("User-Agent", MobileUserAgent);
        request.Headers.Authorization = authClient.DefaultRequestHeaders.Authorization;
        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// DM Profile on mobile renders glass card layout (dm-profile-header-card).
    /// Also checks that dm-profile.mobile.css is linked.
    /// </summary>
    [Fact]
    public async Task MobileDmProfile_MobileUserAgent_RendersGlassCardLayout()
    {
        var (authClient, dmUser) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, "dm_dmview03", "dm_dmview03@test.com", roles: new[] { "DungeonMaster" });

        var request = new HttpRequestMessage(HttpMethod.Get, $"/DungeonMaster/Profile/{dmUser.Id}");
        request.Headers.TryAddWithoutValidation("User-Agent", MobileUserAgent);
        request.Headers.Authorization = authClient.DefaultRequestHeaders.Authorization;
        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("dm-profile-header-card");
        html.Should().Contain("dm-profile.mobile.css");
    }

    // -----------------------------------------------------------------------
    // Finalized-quest "Join This Quest" mobile card (presence/absence/copy)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Authenticated player who has NOT signed up for a finalized quest sees the mobile
    /// "Join This Quest" card with all 3 role-join forms.
    /// </summary>
    [Fact]
    public async Task MobileQuestDetails_FinalizedQuest_AuthenticatedNotSignedUp_RendersJoinCard()
    {
        var dm = await AuthenticationHelper.CreateTestUserAsync(_factory.Services, "dm_joincard01", "dm_joincard01@test.com", name: "DM JoinCard01");
        var quest = await TestDataHelper.CreateTestQuestAsync(_factory.Services, dm.Id, "Join Card Quest", isFinalized: true, finalizedDate: DateTime.UtcNow.AddDays(7));
        await TestDataHelper.CreateProposedDateAsync(_factory.Services, quest.Id, quest.FinalizedDate!.Value);
        var (authClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(_factory, "player_joincard01", "player_joincard01@test.com");

        var request = new HttpRequestMessage(HttpMethod.Get, $"/Quest/Details/{quest.Id}");
        request.Headers.TryAddWithoutValidation("User-Agent", MobileUserAgent);
        request.Headers.Authorization = authClient.DefaultRequestHeaders.Authorization;
        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("Join This Quest");
        html.Should().Contain("JoinFinalizedQuest");
    }

    /// <summary>
    /// A player who is already signed up for the finalized quest must NOT see the mobile
    /// "Join This Quest" card.
    /// </summary>
    [Fact]
    public async Task MobileQuestDetails_FinalizedQuest_AlreadySignedUp_DoesNotRenderJoinCard()
    {
        var dm = await AuthenticationHelper.CreateTestUserAsync(_factory.Services, "dm_joincard02", "dm_joincard02@test.com", name: "DM JoinCard02");
        var quest = await TestDataHelper.CreateTestQuestAsync(_factory.Services, dm.Id, "Join Card Signed Up Quest", isFinalized: true, finalizedDate: DateTime.UtcNow.AddDays(7));
        await TestDataHelper.CreateProposedDateAsync(_factory.Services, quest.Id, quest.FinalizedDate!.Value);
        var (authClient, joinCardPlayer) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(_factory, "player_joincard02", "player_joincard02@test.com");
        await TestDataHelper.CreatePlayerSignupAsync(_factory.Services, quest.Id, joinCardPlayer.Id, signupRole: 0, isSelected: true);

        var request = new HttpRequestMessage(HttpMethod.Get, $"/Quest/Details/{quest.Id}");
        request.Headers.TryAddWithoutValidation("User-Agent", MobileUserAgent);
        request.Headers.Authorization = authClient.DefaultRequestHeaders.Authorization;
        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().NotContain("joinPlayerFormMobile");
    }

    /// <summary>
    /// An unauthenticated visitor must NOT see the mobile "Join This Quest" card.
    /// </summary>
    [Fact]
    public async Task MobileQuestDetails_FinalizedQuest_Unauthenticated_DoesNotRenderJoinCard()
    {
        var dm = await AuthenticationHelper.CreateTestUserAsync(_factory.Services, "dm_joincard03", "dm_joincard03@test.com", name: "DM JoinCard03");
        var quest = await TestDataHelper.CreateTestQuestAsync(_factory.Services, dm.Id, "Join Card Unauth Quest", isFinalized: true, finalizedDate: DateTime.UtcNow.AddDays(7));
        await TestDataHelper.CreateProposedDateAsync(_factory.Services, quest.Id, quest.FinalizedDate!.Value);

        var (response, html) = await GetWithUserAgentAsync($"/Quest/Details/{quest.Id}", MobileUserAgent);

        html.Should().NotContain("joinPlayerFormMobile");
    }

    /// <summary>
    /// When a finalized quest's Player slots are full, the mobile Join card shows waitlist
    /// copy instead of implying a Player join is rejected.
    /// </summary>
    [Fact]
    public async Task MobileQuestDetails_FinalizedQuestFull_RendersWaitlistCopy()
    {
        var dm = await AuthenticationHelper.CreateTestUserAsync(_factory.Services, "dm_joincard04", "dm_joincard04@test.com", name: "DM JoinCard04");
        var quest = await TestDataHelper.CreateTestQuestAsync(_factory.Services, dm.Id, "Join Card Full Quest", isFinalized: true, finalizedDate: DateTime.UtcNow.AddDays(7));
        await TestDataHelper.CreateProposedDateAsync(_factory.Services, quest.Id, quest.FinalizedDate!.Value);

        for (var i = 0; i < 4; i++)
        {
            var seatedPlayer = await AuthenticationHelper.CreateTestUserAsync(_factory.Services, $"joincard04_seated{i}", $"joincard04_seated{i}@test.com");
            await TestDataHelper.CreatePlayerSignupAsync(_factory.Services, quest.Id, seatedPlayer.Id, signupRole: 0, isSelected: true);
        }

        var (authClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(_factory, "player_joincard04", "player_joincard04@test.com");

        var request = new HttpRequestMessage(HttpMethod.Get, $"/Quest/Details/{quest.Id}");
        request.Headers.TryAddWithoutValidation("User-Agent", MobileUserAgent);
        request.Headers.Authorization = authClient.DefaultRequestHeaders.Authorization;
        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("joining as a Player will place you on the waitlist");
    }

    /// <summary>
    /// Regression guard: the "Join This Quest" card is finalized-quest-only. An authenticated,
    /// not-yet-signed-up player viewing an OPEN (non-finalized) quest must NOT see it — only the
    /// pre-finalization "Vote on Proposed Dates" signup section should render.
    /// </summary>
    [Fact]
    public async Task MobileQuestDetails_OpenQuest_AuthenticatedNotSignedUp_DoesNotRenderJoinCard()
    {
        var dm = await AuthenticationHelper.CreateTestUserAsync(_factory.Services, "dm_joincard05", "dm_joincard05@test.com", name: "DM JoinCard05");
        var quest = await TestDataHelper.CreateTestQuestAsync(_factory.Services, dm.Id, "Open Join Card Quest");
        await TestDataHelper.CreateProposedDateAsync(_factory.Services, quest.Id, DateTime.UtcNow.AddDays(7));
        var (authClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(_factory, "player_joincard05", "player_joincard05@test.com");

        var request = new HttpRequestMessage(HttpMethod.Get, $"/Quest/Details/{quest.Id}");
        request.Headers.TryAddWithoutValidation("User-Agent", MobileUserAgent);
        request.Headers.Authorization = authClient.DefaultRequestHeaders.Authorization;
        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().NotContain("joinPlayerFormMobile");
        html.Should().Contain("Vote on Proposed Dates");
    }

    // -----------------------------------------------------------------------
    // Login page renders glass card form on mobile UA
    // -----------------------------------------------------------------------

    /// <summary>
    /// Mobile UA on /Account/Login is served the one shared Login view inside the phone shell: the
    /// phone layout, the shared account sheet, the autofill tokens, one heading, the document title the
    /// desktop page has, and no separate phone account sheet.
    /// </summary>
    [Fact]
    public async Task MobileAccountLogin_MobileUserAgent_RendersGlassCardForm()
    {
        var (response, html) = await GetWithUserAgentAsync("/Account/Login", MobileUserAgent);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("mobile-layout");
        html.Should().Contain("account.css");
        html.Should().NotContain("account.mobile.css");
        html.Should().Contain("autocomplete=\"username\"");
        html.Should().Contain("autocomplete=\"current-password\"");
        html.Should().Contain("<title>Log in - ");
        Regex.Matches(html, "<h1\\b").Count.Should().Be(1);
    }

    /// <summary>
    /// Regression guard: Desktop UA on /Account/Login is served the same view inside the desktop layout:
    /// the shared account sheet, the same title, and none of the phone shell.
    /// </summary>
    [Fact]
    public async Task MobileAccountLogin_DesktopUserAgent_DoesNotRenderGlassCard()
    {
        var (response, html) = await GetWithUserAgentAsync("/Account/Login", DesktopUserAgent);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("account.css");
        html.Should().NotContain("mobile-layout");
        html.Should().Contain("<title>Log in - ");
    }

    /// <summary>
    /// Mobile UA on /Account/ForgotPassword is served the shared view in the phone shell with the
    /// document title the desktop page has and the email autofill token.
    /// </summary>
    [Fact]
    public async Task MobileAccountForgotPassword_MobileUserAgent_RendersTheSharedViewWithItsTitle()
    {
        var (response, html) = await GetWithUserAgentAsync("/Account/ForgotPassword", MobileUserAgent);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("mobile-layout");
        html.Should().Contain("account.css");
        html.Should().NotContain("account.mobile.css");
        html.Should().Contain("<title>Forgot Password - ");
        html.Should().Contain("autocomplete=\"email\"");
    }

    /// <summary>
    /// Mobile UA on /Account/SetPassword is served the shared view in the phone shell with the
    /// document title the desktop page has and both fields marked as a new password.
    /// </summary>
    [Fact]
    public async Task MobileAccountSetPassword_MobileUserAgent_RendersTheSharedViewWithItsTitle()
    {
        var (response, html) = await GetWithUserAgentAsync("/Account/SetPassword?userId=1&token=not-a-token", MobileUserAgent);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("mobile-layout");
        html.Should().Contain("<title>Set Your Password - ");
        Regex.Matches(html, "autocomplete=\"new-password\"").Count.Should().Be(2);
    }

    // -----------------------------------------------------------------------
    // Register page renders glass card form on mobile UA
    // -----------------------------------------------------------------------

    /// <summary>
    /// Public self-registration was removed —
    /// Register.cshtml/Register.Mobile.cshtml and the controller actions are deleted, so the
    /// route now 404s regardless of user agent. This test originally asserted the mobile glass
    /// card rendered; it is updated here to reflect the route's removal rather than weakened.
    /// </summary>
    [Fact]
    public async Task MobileAccountRegister_MobileUserAgent_ShouldReturnNotFound()
    {
        var (response, _) = await GetWithUserAgentAsync("/Account/Register", MobileUserAgent);
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // -----------------------------------------------------------------------
    // Authenticated account pages render glass card on mobile UA
    // -----------------------------------------------------------------------

    /// <summary>
    /// (Edit) Mobile UA on /Account/Edit renders account-card-mobile glass card form.
    /// Uses authenticated request — AccountController.Edit carries [Authorize].
    /// Test starts RED — Edit.Mobile.cshtml does not exist yet.
    /// </summary>
    [Fact]
    public async Task MobileAccountEdit_MobileUserAgent_RendersGlassCardForm()
    {
        var (authClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, "acct_edit16", "acct_edit16@test.com");

        var request = new HttpRequestMessage(HttpMethod.Get, "/Account/Edit");
        request.Headers.TryAddWithoutValidation("User-Agent", MobileUserAgent);
        request.Headers.Authorization = authClient.DefaultRequestHeaders.Authorization;
        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("account-card-mobile");
    }

    /// <summary>
    /// Seeds a user who belongs to two boards, so the picker renders its list instead of redirecting
    /// (a user in exactly one board is sent straight in), and returns that user's authorization header
    /// with the number of boards the picker is expected to list.
    /// </summary>
    private async Task<(AuthenticationHeaderValue? Authorization, int BoardCount)> SeedPickerMemberAsync(string username)
    {
        var (authClient, pickerUser) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, username, $"{username}@test.com");

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();

        // AuthenticationHelper seeds a UserGroups row referencing GroupId=1, but does not
        // guarantee a matching GroupEntity row exists (this test class does not call
        // TestDataHelper.SeedDefaultGroupAsync). Ensure it exists so GroupId=1 is a real,
        // queryable group rather than a dangling FK reference the InMemory provider silently allows.
        if (!await context.Groups.AnyAsync(g => g.Id == 1, TestContext.Current.CancellationToken))
        {
            context.Groups.Add(new GroupEntity { Id = 1, Name = "EuphoriaInn", CreatedAt = DateTime.UtcNow });
        }

        // Use a random large Id for the second group so it cannot collide with Id=1 or with
        // groups created by other tests sharing this class fixture's database.
        var secondGroupId = Random.Shared.Next(100_000, 999_999);
        context.Groups.Add(new GroupEntity { Id = secondGroupId, Name = $"GroupPickerTest_{Guid.NewGuid():N}", CreatedAt = DateTime.UtcNow });

        context.UserGroups.Add(new UserGroupEntity { UserId = pickerUser.Id, GroupId = 1, GroupRole = (int)GroupRole.Player });
        context.UserGroups.Add(new UserGroupEntity { UserId = pickerUser.Id, GroupId = secondGroupId, GroupRole = (int)GroupRole.Player });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return (authClient.DefaultRequestHeaders.Authorization, 2);
    }

    /// <summary>
    /// Asserts the picker body: one submit-button row per board inside a SelectGroup form, the page
    /// sheet linked exactly once and after the legacy sheets, and no phone layout class (the picker
    /// layout serves both User-Agents).
    /// </summary>
    private static void AssertPickerRows(string html, int boardCount)
    {
        var rows = Regex.Matches(html, "<button type=\"submit\" class=\"group-picker-row\"");
        rows.Count.Should().Be(boardCount, "every board the user belongs to is one submit button row");
        html.Should().Contain("SelectGroup");

        var sheet = html.IndexOf("/css/group-picker.css", StringComparison.Ordinal);
        sheet.Should().BeGreaterThan(-1, "the picker's own sheet reaches the page through the view's Styles section");
        html.LastIndexOf("/css/group-picker.css", StringComparison.Ordinal)
            .Should().Be(sheet, "the picker sheet is linked exactly once");
        sheet.Should().BeGreaterThan(
            html.IndexOf("/css/legacy-pages.css", StringComparison.Ordinal),
            "the picker sheet follows the legacy sheets, where it sat when the layout linked it");

        html.Should().NotContain("mobile-layout", "the picker layout serves phones and desktops alike");
        html.Should().NotContain("account.mobile.css", "the merged picker view needs no account phone sheet");
    }

    /// <summary>
    /// Mobile UA on /GroupPicker/Index renders one native submit button per board and links the
    /// picker sheet. Guards against the unrendered-Styles-section white-page regression: the picker's
    /// sheet now reaches the page only through the view's Styles section, so a picker layout that
    /// stopped rendering that section would drop the sheet and fail here. The test user is seeded
    /// into two groups so the controller returns the list instead of auto-redirecting (which happens
    /// when a user belongs to exactly one group).
    /// </summary>
    [Fact]
    public async Task MobileGroupPicker_MobileUserAgent_RendersGroupCardsAndStylesSection()
    {
        var (authorization, boardCount) = await SeedPickerMemberAsync("gp_mobile01");

        var (response, html) = await GetWithUserAgentAsync("/GroupPicker/Index", MobileUserAgent, authorization);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        AssertPickerRows(html, boardCount);
    }

    /// <summary>
    /// Desktop UA on /GroupPicker/Index is served the same view with the same submit rows.
    /// </summary>
    [Fact]
    public async Task MobileGroupPicker_DesktopUserAgent_RendersTheSameSubmitRows()
    {
        var (authorization, boardCount) = await SeedPickerMemberAsync("gp_desktop01");

        var (response, html) = await GetWithUserAgentAsync("/GroupPicker/Index", DesktopUserAgent, authorization);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        AssertPickerRows(html, boardCount);
    }

    /// <summary>
    /// The picker layout's landmarks under both User-Agents: one skip link to the main region, one
    /// labelled navigation, one main element carrying the skip target, and no redundant main role.
    /// </summary>
    [Theory]
    [InlineData("mobile")]
    [InlineData("desktop")]
    public async Task GroupPicker_BothUserAgents_RenderTheirLandmarks(string agent)
    {
        var (authorization, _) = await SeedPickerMemberAsync($"gp_land_{agent}");

        var (response, html) = await GetWithUserAgentAsync(
            "/GroupPicker/Index", agent == "mobile" ? MobileUserAgent : DesktopUserAgent, authorization);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        Regex.Matches(html, "<a\\b[^>]*\\bclass=\"[^\"]*\\bskip-link\\b[^\"]*\"[^>]*\\bhref=\"#main-content\"")
            .Count.Should().Be(1, $"{agent}: one skip link to the main region");
        Regex.Matches(html, "<nav\\b[^>]*\\baria-label=\"[^\"]+\"")
            .Count.Should().Be(1, $"{agent}: one navigation landmark with a name");
        Regex.Matches(html, "<nav\\b").Count.Should().Be(1, $"{agent}: only one navigation element");
        Regex.Matches(html, "<main\\b[^>]*\\bid=\"main-content\"")
            .Count.Should().Be(1, $"{agent}: one main element carrying the skip target");
        Regex.Matches(html, "<main\\b").Count.Should().Be(1, $"{agent}: only one main element");
        html.Should().NotContain("role=\"main\"", "a main element already carries that role");
    }

    /// <summary>
    /// The picker page requests no third-party host under either User-Agent: every asset reference
    /// on its link, script and img elements is a same-origin path.
    /// </summary>
    [Theory]
    [InlineData("mobile")]
    [InlineData("desktop")]
    public async Task GroupPicker_BothUserAgents_RequestNoThirdPartyHost(string agent)
    {
        var (authorization, _) = await SeedPickerMemberAsync($"gp_host_{agent}");

        var (response, html) = await GetWithUserAgentAsync(
            "/GroupPicker/Index", agent == "mobile" ? MobileUserAgent : DesktopUserAgent, authorization);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var references = Regex
            .Matches(html, "<(?:link|script|img)\\b[^>]*?\\b(?:src|href)=\"([^\"]*)\"")
            .Select(m => m.Groups[1].Value)
            .ToList();

        references.Count.Should().BeGreaterThanOrEqualTo(5, $"{agent}: the page's asset references must be found, not an empty parse");
        foreach (var reference in references)
        {
            var offsite = reference.StartsWith("http:", StringComparison.OrdinalIgnoreCase)
                || reference.StartsWith("https:", StringComparison.OrdinalIgnoreCase)
                || reference.StartsWith("//", StringComparison.Ordinal);
            offsite.Should().BeFalse($"{agent}: '{reference}' is not a same-origin path");
        }
    }

    /// <summary>
    /// (Profile) Mobile UA on /Account/Profile renders the phone layout on the shared card, with its document
    /// title, the building-access wording the desktop uses and the area sheet, and no longer links the phone
    /// account sheet. Uses authenticated request — AccountController.Profile carries [Authorize].
    /// </summary>
    [Fact]
    public async Task MobileAccountProfile_MobileUserAgent_RendersGlassCardLayout()
    {
        var (authClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, "acct_prof16", "acct_prof16@test.com");

        var request = new HttpRequestMessage(HttpMethod.Get, "/Account/Profile");
        request.Headers.TryAddWithoutValidation("User-Agent", MobileUserAgent);
        request.Headers.Authorization = authClient.DefaultRequestHeaders.Authorization;
        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("mobile-layout");
        html.Should().Contain("modern-card");
        html.Should().Contain("account.css");
        html.Should().NotContain("account.mobile.css");
        html.Should().MatchRegex("<title>\\s*Profile - ");
        (html.Contains("Has Building Key") || html.Contains("No Building Key")).Should().BeTrue(
            because: "the phone reads the building-access state with the desktop's wording");
    }

    // -----------------------------------------------------------------------
    // Shop index renders item grid and Filter & Sort button on mobile UA
    // -----------------------------------------------------------------------

    /// <summary>
    /// Mobile UA on /Shop renders "Filter & Sort" offcanvas trigger button and links
    /// shop.mobile.css. The shop-item-card-mobile class only renders when items exist; the filter
    /// button and CSS link render unconditionally, so those are the stable smoke-test assertions.
    /// Note: shop-item-card-mobile presence with seeded items is verified by Plan 03's own seeded test.
    /// Test starts RED — Shop/Index.Mobile.cshtml does not exist yet.
    /// </summary>
    [Fact]
    public async Task MobileShopIndex_MobileUserAgent_RendersItemGridAndFilterButton()
    {
        var (authClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, "shop_browse16", "shop_browse16@test.com");

        var request = new HttpRequestMessage(HttpMethod.Get, "/Shop");
        request.Headers.TryAddWithoutValidation("User-Agent", MobileUserAgent);
        request.Headers.Authorization = authClient.DefaultRequestHeaders.Authorization;
        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("Filter &amp; Sort");
        html.Should().Contain("shop.mobile.css");
    }

    /// <summary>
    /// Mobile UA on /Shop must NOT render the purchase-history-panel.
    /// Purchase History side panel is omitted on mobile.
    /// Test starts RED — Shop/Index.Mobile.cshtml does not exist yet.
    /// </summary>
    [Fact]
    public async Task MobileShopIndex_MobileUserAgent_OmitsPurchaseHistoryPanel()
    {
        var (authClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, "shop_d04_16", "shop_d04_16@test.com");

        var request = new HttpRequestMessage(HttpMethod.Get, "/Shop");
        request.Headers.TryAddWithoutValidation("User-Agent", MobileUserAgent);
        request.Headers.Authorization = authClient.DefaultRequestHeaders.Authorization;
        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().NotContain("purchase-history-panel");
    }

    // -----------------------------------------------------------------------
    // Characters index renders list rows on mobile UA
    // -----------------------------------------------------------------------

    /// <summary>
    /// Mobile UA on /Characters links characters.mobile.css. The character-member-row
    /// class only renders when characters exist; the CSS link renders unconditionally, so that is
    /// the stable smoke-test assertion here.
    /// Note: character-member-row presence with seeded characters is verified by Plan 04's own seeded test.
    /// </summary>
    [Fact]
    public async Task MobileCharacters_MobileUserAgent_RendersListRows()
    {
        var (authClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, "char_browse16", "char_browse16@test.com");

        var request = new HttpRequestMessage(HttpMethod.Get, "/Characters");
        request.Headers.TryAddWithoutValidation("User-Agent", MobileUserAgent);
        request.Headers.Authorization = authClient.DefaultRequestHeaders.Authorization;
        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("characters.mobile.css");
    }

    // -----------------------------------------------------------------------
    // Character Details renders glass card layout on mobile UA
    // -----------------------------------------------------------------------

    /// <summary>
    /// Mobile UA on /Characters/Details/{id} renders character-detail-card glass card
    /// and links character-detail.mobile.css.
    /// </summary>
    [Fact]
    public async Task GetMobilePage_CharacterDetails_ReturnsSuccessAndMobileLayout()
    {
        var (authClient, ownerUser) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, "char_det17", "char_det17@test.com");
        var character = await TestDataHelper.CreateTestCharacterAsync(_factory.Services, ownerUser.Id, "Aria Swiftblade");

        var request = new HttpRequestMessage(HttpMethod.Get, $"/Characters/Details/{character.Id}");
        request.Headers.TryAddWithoutValidation("User-Agent", MobileUserAgent);
        request.Headers.Authorization = authClient.DefaultRequestHeaders.Authorization;
        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("character-detail-card");
        html.Should().Contain("character-detail.mobile.css");
    }

    // -----------------------------------------------------------------------
    // Character Create renders glass card form on mobile UA
    // -----------------------------------------------------------------------

    /// <summary>
    /// Mobile UA on /Characters/Create renders character-form-card glass card form
    /// and links character-form.mobile.css.
    /// </summary>
    [Fact]
    public async Task GetMobilePage_CharacterCreate_ReturnsSuccessAndMobileLayout()
    {
        var (authClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, "char_cre17", "char_cre17@test.com");

        var request = new HttpRequestMessage(HttpMethod.Get, "/Characters/Create");
        request.Headers.TryAddWithoutValidation("User-Agent", MobileUserAgent);
        request.Headers.Authorization = authClient.DefaultRequestHeaders.Authorization;
        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("character-form-card");
        html.Should().Contain("character-form.mobile.css");
    }

    // -----------------------------------------------------------------------
    // Character Edit renders glass card form on mobile UA
    // -----------------------------------------------------------------------

    /// <summary>
    /// Mobile UA on /Characters/Edit/{id} renders character-form-card glass card form
    /// and links character-form.mobile.css.
    /// </summary>
    [Fact]
    public async Task GetMobilePage_CharacterEdit_ReturnsSuccessAndMobileLayout()
    {
        var (authClient, ownerUser) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, "char_edi17", "char_edi17@test.com");
        var character = await TestDataHelper.CreateTestCharacterAsync(_factory.Services, ownerUser.Id, "Bram Ironfist");

        var request = new HttpRequestMessage(HttpMethod.Get, $"/Characters/Edit/{character.Id}");
        request.Headers.TryAddWithoutValidation("User-Agent", MobileUserAgent);
        request.Headers.Authorization = authClient.DefaultRequestHeaders.Authorization;
        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("character-form-card");
        html.Should().Contain("character-form.mobile.css");
    }

    // -----------------------------------------------------------------------
    // Players Index renders section list on mobile UA
    // -----------------------------------------------------------------------

    /// <summary>
    /// Mobile UA on /Players renders players-section-card glass card sections
    /// and links players.mobile.css. Test starts RED — Players/Index.Mobile.cshtml does not exist yet.
    /// </summary>
    [Fact]
    public async Task GetMobilePage_PlayersIndex_ReturnsSuccessAndMobileLayout()
    {
        var (authClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, "player_idx17", "player_idx17@test.com");

        var request = new HttpRequestMessage(HttpMethod.Get, "/Players");
        request.Headers.TryAddWithoutValidation("User-Agent", MobileUserAgent);
        request.Headers.Authorization = authClient.DefaultRequestHeaders.Authorization;
        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("players-section-card");
        html.Should().Contain("players.mobile.css");
    }

    // -----------------------------------------------------------------------
    // Quest Edit is one view that serves the phone layout too
    // -----------------------------------------------------------------------

    /// <summary>
    /// Mobile UA on /Quest/Edit/{id} answers 200 with the phone layout (the mobile-layout body class),
    /// the Edit form posting back to the quest, its tips card and the shared quest form sheet.
    /// Requires DM authentication and ownership of the quest.
    /// </summary>
    [Fact]
    public async Task GetMobilePage_QuestEdit_ReturnsSuccessAndMobileLayout()
    {
        var (authClient, dmUser) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, "dm_qedit18", "dm_qedit18@test.com", roles: new[] { "DungeonMaster" });
        var quest = await TestDataHelper.CreateTestQuestAsync(
            _factory.Services, dmUser.Id, "Edit Quest Mobile18");

        var request = new HttpRequestMessage(HttpMethod.Get, $"/Quest/Edit/{quest.Id}");
        request.Headers.TryAddWithoutValidation("User-Agent", MobileUserAgent);
        request.Headers.Authorization = authClient.DefaultRequestHeaders.Authorization;
        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("mobile-layout");
        html.Should().Contain($"/Quest/Edit/{quest.Id}");
        html.Should().Contain("Update Quest");
        html.Should().Contain("Quest Editing Tips");
        html.Should().Contain("quest-form.css");
    }

    // -----------------------------------------------------------------------
    // CreateFollowUp is one view serving the phone layout
    // -----------------------------------------------------------------------

    /// <summary>
    /// Mobile UA on /Quest/CreateFollowUp/{id} answers 200 with the mobile-layout body class, the
    /// follow-up form, the pre-approved players card and its sentence, and links quest-form.css.
    /// Requires DM authentication — CreateFollowUp action enforces DM role.
    /// </summary>
    [Fact]
    public async Task GetMobilePage_QuestCreateFollowUp_ReturnsSuccessAndMobileLayout()
    {
        var (authClient, dmUser) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, "dm_followup18", "dm_followup18@test.com", roles: new[] { "DungeonMaster" });
        var quest = await TestDataHelper.CreateTestQuestAsync(
            _factory.Services, dmUser.Id, "FollowUp Source Quest18", isFinalized: true);

        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
            var q = await context.Quests.FindAsync([quest.Id], TestContext.Current.CancellationToken);
            if (q != null) { q.FinalizedDate = DateTime.UtcNow.AddDays(-2); await context.SaveChangesAsync(TestContext.Current.CancellationToken); }
        }

        var request = new HttpRequestMessage(HttpMethod.Get, $"/Quest/CreateFollowUp/{quest.Id}");
        request.Headers.TryAddWithoutValidation("User-Agent", MobileUserAgent);
        request.Headers.Authorization = authClient.DefaultRequestHeaders.Authorization;
        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("mobile-layout");
        html.Should().Contain($"/Quest/CreateFollowUp/{quest.Id}");
        html.Should().Contain("Save Follow-Up Quest");
        html.Should().Contain("Pre-Approved Players");
        html.Should().Contain("Selected players from the original quest will be automatically added");
        html.Should().Contain("quest-form.css");
    }

    // -----------------------------------------------------------------------
    // DM EditProfile renders glass card form on mobile UA
    // -----------------------------------------------------------------------

    /// <summary>
    /// Mobile UA on /DungeonMaster/EditProfile/{id} renders dm-editprofile-card-mobile
    /// glass card form and links dm-editprofile.mobile.css.
    /// Requires DM authentication — EditProfile enforces DM ownership or Admin role.
    /// </summary>
    [Fact]
    public async Task GetMobilePage_DmEditProfile_ReturnsSuccessAndMobileLayout()
    {
        var (authClient, dmUser) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, "dm_editprof18", "dm_editprof18@test.com", roles: new[] { "DungeonMaster" });

        var request = new HttpRequestMessage(HttpMethod.Get, $"/DungeonMaster/EditProfile/{dmUser.Id}");
        request.Headers.TryAddWithoutValidation("User-Agent", MobileUserAgent);
        request.Headers.Authorization = authClient.DefaultRequestHeaders.Authorization;
        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("dm-editprofile-card-mobile");
        html.Should().Contain("dm-editprofile.mobile.css");
    }

    // -----------------------------------------------------------------------
    // QuestLog Details renders glass card layout on mobile UA
    // -----------------------------------------------------------------------

    /// <summary>
    /// Mobile UA on /QuestLog/Details/{id} answers 200 with the phone layout (the mobile-layout body class),
    /// the quest title and the Session Recap section, and links quest-log.css: Details is one view that
    /// serves both layouts. Quest must be finalized with past FinalizedDate.
    /// /QuestLog now requires authentication.
    /// </summary>
    [Fact]
    public async Task GetMobilePage_QuestLogDetails_ReturnsSuccessAndMobileLayout()
    {
        var dm = await AuthenticationHelper.CreateTestUserAsync(
            _factory.Services, "dm_qldet18", "dm_qldet18@test.com", name: "DM QLogDet18");
        var quest = await TestDataHelper.CreateTestQuestAsync(
            _factory.Services, dm.Id, "Quest Log Detail Mobile18", isFinalized: true);

        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
            var q = await context.Quests.FindAsync([quest.Id], TestContext.Current.CancellationToken);
            if (q != null) { q.FinalizedDate = DateTime.UtcNow.AddDays(-2); await context.SaveChangesAsync(TestContext.Current.CancellationToken); }
        }

        var (authClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, "player_qldet18", "player_qldet18@test.com");

        var request = new HttpRequestMessage(HttpMethod.Get, $"/QuestLog/Details/{quest.Id}");
        request.Headers.TryAddWithoutValidation("User-Agent", MobileUserAgent);
        request.Headers.Authorization = authClient.DefaultRequestHeaders.Authorization;
        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("mobile-layout");
        html.Should().Contain("Quest Log Detail Mobile18");
        html.Should().Contain("Session Recap");
        html.Should().Contain("quest-log.css");
        html.Should().NotContain("quest-log-detail.mobile.css");
    }

    // -----------------------------------------------------------------------
    // Admin Users list renders mobile layout
    // -----------------------------------------------------------------------

    /// <summary>
    /// Mobile UA on /Admin/Users renders admin-users-card-mobile glass card and links
    /// admin-users.mobile.css. Requires Admin role authentication.
    /// Starts RED — Admin/Users.Mobile.cshtml does not exist yet; goes GREEN when Plan 02 lands.
    /// </summary>
    [Fact]
    public async Task GetMobilePage_AdminUsers_ReturnsSuccessAndMobileLayout()
    {
        var (authClient, adminUser) = await AuthenticationHelper.CreateAuthenticatedAdminClientAsync(
            _factory, "admin_users19", "admin_users19@test.com");

        var request = new HttpRequestMessage(HttpMethod.Get, "/Admin/Users");
        request.Headers.TryAddWithoutValidation("User-Agent", MobileUserAgent);
        request.Headers.Authorization = authClient.DefaultRequestHeaders.Authorization;
        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("admin-users-card-mobile");
        html.Should().Contain("admin-users.mobile.css");
    }

    // -----------------------------------------------------------------------
    // Admin EditUser form renders mobile layout
    // -----------------------------------------------------------------------

    /// <summary>
    /// Mobile UA on /Admin/EditUser?userId={id} renders admin-form-card-mobile glass card
    /// and links admin-form.mobile.css. Requires Admin role authentication.
    /// Starts RED — Admin/EditUser.Mobile.cshtml does not exist yet; goes GREEN when Plan 03 lands.
    /// </summary>
    [Fact]
    public async Task GetMobilePage_AdminEditUser_ReturnsSuccessAndMobileLayout()
    {
        var (authClient, adminUser) = await AuthenticationHelper.CreateAuthenticatedAdminClientAsync(
            _factory, "admin_edituser19", "admin_edituser19@test.com");
        var targetUser = await AuthenticationHelper.CreateTestUserAsync(
            _factory.Services, "edit_target19", "edit_target19@test.com");

        // Target must be a group 1 member — AdminController.EditUser rejects out-of-group targets.
        using (var scope = _factory.Services.CreateScope())
        {
            var userService = scope.ServiceProvider.GetRequiredService<IUserService>();
            await userService.SetGroupRoleAsync(targetUser.Id, 1, GroupRole.Player);
        }

        var request = new HttpRequestMessage(HttpMethod.Get, $"/Admin/EditUser?userId={targetUser.Id}");
        request.Headers.TryAddWithoutValidation("User-Agent", MobileUserAgent);
        request.Headers.Authorization = authClient.DefaultRequestHeaders.Authorization;
        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("admin-form-card-mobile");
        html.Should().Contain("admin-form.mobile.css");
    }

    // -----------------------------------------------------------------------
    // Admin Quests list renders mobile layout
    // -----------------------------------------------------------------------

    /// <summary>
    /// Mobile UA on /Admin/Quests renders admin-quests-card-mobile glass card and links
    /// admin-quests.mobile.css. Requires Admin role authentication.
    /// Starts RED — Admin/Quests.Mobile.cshtml does not exist yet; goes GREEN when Plan 02 lands.
    /// </summary>
    [Fact]
    public async Task GetMobilePage_AdminQuests_ReturnsSuccessAndMobileLayout()
    {
        var (authClient, adminUser) = await AuthenticationHelper.CreateAuthenticatedAdminClientAsync(
            _factory, "admin_quests19", "admin_quests19@test.com");
        await TestDataHelper.CreateTestQuestAsync(_factory.Services, adminUser.Id, "Admin Quest Mobile19");

        var request = new HttpRequestMessage(HttpMethod.Get, "/Admin/Quests");
        request.Headers.TryAddWithoutValidation("User-Agent", MobileUserAgent);
        request.Headers.Authorization = authClient.DefaultRequestHeaders.Authorization;
        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("admin-quests-card-mobile");
        html.Should().Contain("admin-quests.mobile.css");
    }

    // -----------------------------------------------------------------------
    // Admin ResetPassword form renders mobile layout
    // -----------------------------------------------------------------------

    /// <summary>
    /// Mobile UA on /Admin/ResetPassword?userId={id} renders admin-form-card-mobile glass
    /// card and links admin-form.mobile.css. Requires Admin role authentication.
    /// Starts RED — Admin/ResetPassword.Mobile.cshtml does not exist yet; goes GREEN when Plan 03 lands.
    /// </summary>
    [Fact]
    public async Task GetMobilePage_AdminResetPassword_ReturnsSuccessAndMobileLayout()
    {
        var (authClient, adminUser) = await AuthenticationHelper.CreateAuthenticatedAdminClientAsync(
            _factory, "admin_resetpw19", "admin_resetpw19@test.com");
        var targetUser = await AuthenticationHelper.CreateTestUserAsync(
            _factory.Services, "resetpw_target19", "resetpw_target19@test.com");

        // Target must be a group 1 member — AdminController.ResetPassword rejects out-of-group targets.
        using (var scope = _factory.Services.CreateScope())
        {
            var userService = scope.ServiceProvider.GetRequiredService<IUserService>();
            await userService.SetGroupRoleAsync(targetUser.Id, 1, GroupRole.Player);
        }

        var request = new HttpRequestMessage(HttpMethod.Get, $"/Admin/ResetPassword?userId={targetUser.Id}");
        request.Headers.TryAddWithoutValidation("User-Agent", MobileUserAgent);
        request.Headers.Authorization = authClient.DefaultRequestHeaders.Authorization;
        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("admin-form-card-mobile");
        html.Should().Contain("admin-form.mobile.css");
    }

    // -----------------------------------------------------------------------
    // ShopManagement Index renders mobile layout
    // -----------------------------------------------------------------------

    /// <summary>
    /// Mobile UA on /ShopManagement renders shop-mgmt-index-card-mobile glass card and
    /// links shop-management-index.mobile.css. Requires DungeonMaster role authentication.
    /// Starts RED — ShopManagement/Index.Mobile.cshtml does not exist yet; goes GREEN when Plan 05 lands.
    /// </summary>
    [Fact]
    public async Task GetMobilePage_ShopManagementIndex_ReturnsSuccessAndMobileLayout()
    {
        var (authClient, dmUser) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, "dm_shopidx19", "dm_shopidx19@test.com", roles: new[] { "DungeonMaster" });

        var request = new HttpRequestMessage(HttpMethod.Get, "/ShopManagement");
        request.Headers.TryAddWithoutValidation("User-Agent", MobileUserAgent);
        request.Headers.Authorization = authClient.DefaultRequestHeaders.Authorization;
        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("shop-mgmt-index-card-mobile");
        html.Should().Contain("shop-management-index.mobile.css");
    }

    // -----------------------------------------------------------------------
    // ShopManagement Create form renders mobile layout
    // -----------------------------------------------------------------------

    /// <summary>
    /// Mobile UA on /ShopManagement/Create renders shop-mgmt-create-card-mobile glass card
    /// and links shop-management-create.mobile.css. Requires DungeonMaster role authentication.
    /// Starts RED — ShopManagement/Create.Mobile.cshtml does not exist yet; goes GREEN when Plan 04 lands.
    /// </summary>
    [Fact]
    public async Task GetMobilePage_ShopManagementCreate_ReturnsSuccessAndMobileLayout()
    {
        var (authClient, dmUser) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, "dm_shopcreate19", "dm_shopcreate19@test.com", roles: new[] { "DungeonMaster" });

        var request = new HttpRequestMessage(HttpMethod.Get, "/ShopManagement/Create");
        request.Headers.TryAddWithoutValidation("User-Agent", MobileUserAgent);
        request.Headers.Authorization = authClient.DefaultRequestHeaders.Authorization;
        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("shop-mgmt-create-card-mobile");
        html.Should().Contain("shop-management-create.mobile.css");
    }

    // -----------------------------------------------------------------------
    // ShopManagement Edit form renders mobile layout
    // -----------------------------------------------------------------------

    /// <summary>
    /// Mobile UA on /ShopManagement/Edit/{id} renders shop-mgmt-edit-card-mobile glass
    /// card and links shop-management-edit.mobile.css. Requires DungeonMaster role authentication.
    /// Starts RED — ShopManagement/Edit.Mobile.cshtml does not exist yet; goes GREEN when Plan 04 lands.
    /// </summary>
    [Fact]
    public async Task GetMobilePage_ShopManagementEdit_ReturnsSuccessAndMobileLayout()
    {
        var (authClient, dmUser) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, "dm_shopedit19", "dm_shopedit19@test.com", roles: new[] { "DungeonMaster" });
        var item = await TestDataHelper.CreateShopItemAsync(
            _factory.Services, dmUser.Id, "Edit Item Mobile19");

        var request = new HttpRequestMessage(HttpMethod.Get, $"/ShopManagement/Edit/{item.Id}");
        request.Headers.TryAddWithoutValidation("User-Agent", MobileUserAgent);
        request.Headers.Authorization = authClient.DefaultRequestHeaders.Authorization;
        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("shop-mgmt-edit-card-mobile");
        html.Should().Contain("shop-management-edit.mobile.css");
    }

    // -----------------------------------------------------------------------
    // Shop Details page renders mobile layout
    // -----------------------------------------------------------------------

    /// <summary>
    /// Mobile UA on /Shop/Details/{id} renders shop-details-card-mobile glass card and
    /// links shop-details.mobile.css. Authenticated as a regular player.
    /// Starts RED — Shop/Details.Mobile.cshtml does not exist yet; goes GREEN when Plan 06 lands.
    /// </summary>
    [Fact]
    public async Task GetMobilePage_ShopDetails_ReturnsSuccessAndMobileLayout()
    {
        var sellerDm = await AuthenticationHelper.CreateTestUserAsync(
            _factory.Services, "seller_dm19", "seller_dm19@test.com");
        var item = await TestDataHelper.CreateShopItemAsync(
            _factory.Services, sellerDm.Id, "Detail Item Mobile19");
        var (authClient, buyerUser) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, "buyer_details19", "buyer_details19@test.com");

        var request = new HttpRequestMessage(HttpMethod.Get, $"/Shop/Details/{item.Id}");
        request.Headers.TryAddWithoutValidation("User-Agent", MobileUserAgent);
        request.Headers.Authorization = authClient.DefaultRequestHeaders.Authorization;
        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("shop-details-card-mobile");
        html.Should().Contain("shop-details.mobile.css");
    }

    // -----------------------------------------------------------------------
    // Mobile agenda events — a day with only an event, all-day wording,
    // event-before-quest ordering, and the neutral empty state
    // -----------------------------------------------------------------------

    /// <summary>
    /// A day with an event but no quest appears in the agenda — this is the case the agenda
    /// could not show before, because the day list was filtered on quests alone.
    /// </summary>
    [Fact]
    public async Task MobileCalendar_DayWithEventButNoQuest_AppearsInAgenda()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);

        var eventDate = DateTime.Today.AddDays(6);
        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
            context.Events.Add(new EventEntity
            {
                Title = "Event Only Day Council",
                GroupId = 1,
                Date = DateOnly.FromDateTime(eventDate),
                CreatedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var (authClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, "mobileagenda_eventonly", "mobileagenda_eventonly@test.com");

        var (response, html) = await GetWithUserAgentAsync(
            $"/Calendar?year={eventDate.Year}&month={eventDate.Month}", MobileUserAgent, authClient.DefaultRequestHeaders.Authorization);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("Event Only Day Council");
        html.Should().Contain("agenda-event-entry");
    }

    /// <summary>
    /// An event with no start time always renders the all-day wording rather than a blank
    /// time slot.
    /// </summary>
    [Fact]
    public async Task MobileCalendar_EventWithoutStartTime_RendersAllDayWording()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);

        var eventDate = DateTime.Today.AddDays(8);
        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
            context.Events.Add(new EventEntity
            {
                Title = "Mobile All Day Council",
                GroupId = 1,
                Date = DateOnly.FromDateTime(eventDate),
                StartTime = null,
                CreatedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var (authClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, "mobileagenda_allday", "mobileagenda_allday@test.com");

        var (response, html) = await GetWithUserAgentAsync(
            $"/Calendar?year={eventDate.Year}&month={eventDate.Month}", MobileUserAgent, authClient.DefaultRequestHeaders.Authorization);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("Mobile All Day Council");
        html.Should().Contain("All day");
    }

    /// <summary>
    /// Within a day that has both an event and a quest, the event entry renders first,
    /// mirroring the desktop day cell's ordering.
    /// </summary>
    [Fact]
    public async Task MobileCalendar_EventAndQuestOnSameDay_EventEntryRendersFirst()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);

        var dm = await AuthenticationHelper.CreateTestUserAsync(
            _factory.Services, "mobileagenda_dm", "mobileagenda_dm@test.com", name: "Mobile Agenda DM");
        var sharedDate = DateTime.Today.AddDays(10);
        var quest = await TestDataHelper.CreateTestQuestAsync(
            _factory.Services, dm.Id, "Mobile Ordering Quest", isFinalized: true);
        await TestDataHelper.CreateProposedDateAsync(_factory.Services, quest.Id, sharedDate);

        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
            var questToUpdate = await context.Quests.FindAsync([quest.Id], TestContext.Current.CancellationToken);
            if (questToUpdate != null)
            {
                questToUpdate.FinalizedDate = sharedDate;
                await context.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            context.Events.Add(new EventEntity
            {
                Title = "Mobile Ordering Event",
                GroupId = 1,
                Date = DateOnly.FromDateTime(sharedDate),
                CreatedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var (authClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, "mobileagenda_orderviewer", "mobileagenda_orderviewer@test.com");

        var (response, html) = await GetWithUserAgentAsync(
            $"/Calendar?year={sharedDate.Year}&month={sharedDate.Month}", MobileUserAgent, authClient.DefaultRequestHeaders.Authorization);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var eventEntryIndex = html.IndexOf("agenda-event-entry", StringComparison.Ordinal);
        var questEntryIndex = html.IndexOf("agenda-quest-entry", StringComparison.Ordinal);
        eventEntryIndex.Should().BeGreaterThan(0);
        eventEntryIndex.Should().BeLessThan(questEntryIndex);
    }

    /// <summary>
    /// A month with neither quests nor events shows the month-neutral empty state, not the
    /// old quest-only heading.
    /// </summary>
    [Fact]
    public async Task MobileCalendar_MonthWithNeitherQuestNorEvent_RendersNeutralEmptyState()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);

        var (authClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, "mobileagenda_empty", "mobileagenda_empty@test.com");

        var farFutureMonth = DateTime.Today.AddYears(5);

        var (response, html) = await GetWithUserAgentAsync(
            $"/Calendar?year={farFutureMonth.Year}&month={farFutureMonth.Month}", MobileUserAgent, authClient.DefaultRequestHeaders.Authorization);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("Nothing This Month");
        html.Should().NotContain("No Quests This Month");
    }
}
