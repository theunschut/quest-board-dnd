using QuestBoard.Domain.Enums;
using QuestBoard.IntegrationTests.Helpers;
using System.Net;
using System.Net.Http.Headers;

namespace QuestBoard.IntegrationTests.Tests;

/// <summary>
/// Locks the one rule a board applies to its DM-only sessions on every calendar surface: a
/// viewer whose role on the board is Player never sees them, while a DungeonMaster, an Admin
/// and a SuperAdmin (who counts as Admin) do. A DM-only session a viewer may see renders like
/// any other quest, so the rule is proved by presence and absence of the quest's title alone.
///
/// Every fact also asserts the ordinary quest's title is present, so an error page or a login
/// redirect cannot satisfy the absence assertion for free.
/// </summary>
public class CalendarDungeonMasterSessionTests(WebApplicationFactoryBase factory)
    : IClassFixture<WebApplicationFactoryBase>, IAsyncLifetime
{
    private const string MobileUserAgent =
        "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1";

    private const string DesktopUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    // Other classes read the shared group context singleton, so it is restored once this
    // class finishes running.
    public ValueTask DisposeAsync()
    {
        factory.TestGroupContext.ActiveGroupId = 1;
        factory.TestGroupContext.BoardType = BoardType.OneShot;
        return ValueTask.CompletedTask;
    }

    private async Task<(HttpResponseMessage Response, string Html)> GetWithUserAgentAsync(
        string url, string userAgent, AuthenticationHeaderValue? authorization)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("User-Agent", userAgent);
        if (authorization != null)
        {
            request.Headers.Authorization = authorization;
        }
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        return (response, html);
    }

    // Seeds one DM-only finalized quest and one ordinary finalized quest on board 1, both on
    // the same date, so a single calendar request can prove or disprove either one's presence.
    private async Task<(string DmOnlyTitle, string OpenTitle, DateTime Date)> SeedFinalizedQuestsAsync(string suffix)
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);

        var date = DateTime.Today.AddDays(7);
        var dmOnlyTitle = $"DmOnlySession{suffix}";
        var openTitle = $"OpenTable{suffix}";

        var dm = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, $"sessiondm{suffix}", $"sessiondm{suffix}@test.com");

        var dmOnly = await TestDataHelper.CreateTestQuestAsync(
            factory.Services, dm.Id, dmOnlyTitle, "DM-only calendar visibility test quest", 5,
            isFinalized: true, dungeonMasterSession: true);
        var open = await TestDataHelper.CreateTestQuestAsync(
            factory.Services, dm.Id, openTitle, "Ordinary calendar visibility test quest", 5,
            isFinalized: true);

        // A proposed date matching the finalized date, then the finalized date set on the
        // tracked row, is the proven recipe for a quest that actually renders on the calendar.
        foreach (var quest in new[] { dmOnly, open })
        {
            await TestDataHelper.CreateProposedDateAsync(factory.Services, quest.Id, date);

            using var scope = factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
            var toUpdate = await context.Quests.FindAsync([quest.Id], TestContext.Current.CancellationToken);
            if (toUpdate != null)
            {
                toUpdate.FinalizedDate = date;
                await context.SaveChangesAsync(TestContext.Current.CancellationToken);
            }
        }

        return (dmOnlyTitle, openTitle, date);
    }

    [Theory]
    [InlineData("Player", false)]
    [InlineData("Player", true)]
    public async Task Calendar_PlayerViewer_DoesNotSeeDmOnlySession(string role, bool mobile)
    {
        var suffix = $"{role}{(mobile ? "Mobile" : "Desktop")}";
        var (dmOnlyTitle, openTitle, date) = await SeedFinalizedQuestsAsync(suffix);
        var (authClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, $"sessionview{suffix}", $"sessionview{suffix}@test.com", roles: [role]);

        var (response, html) = await GetWithUserAgentAsync(
            $"/Calendar?year={date.Year}&month={date.Month}",
            mobile ? MobileUserAgent : DesktopUserAgent,
            authClient.DefaultRequestHeaders.Authorization);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain(openTitle);
        html.Should().NotContain(dmOnlyTitle);
    }

    [Theory]
    [InlineData("DungeonMaster", false)]
    [InlineData("DungeonMaster", true)]
    [InlineData("Admin", false)]
    [InlineData("Admin", true)]
    [InlineData("SuperAdmin", false)]
    [InlineData("SuperAdmin", true)]
    public async Task Calendar_DmTierViewer_SeesDmOnlySession(string role, bool mobile)
    {
        var suffix = $"{role}{(mobile ? "Mobile" : "Desktop")}";
        var (dmOnlyTitle, openTitle, date) = await SeedFinalizedQuestsAsync(suffix);
        var (authClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, $"sessionview{suffix}", $"sessionview{suffix}@test.com", roles: [role]);

        var (response, html) = await GetWithUserAgentAsync(
            $"/Calendar?year={date.Year}&month={date.Month}",
            mobile ? MobileUserAgent : DesktopUserAgent,
            authClient.DefaultRequestHeaders.Authorization);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain(openTitle);
        html.Should().Contain(dmOnlyTitle);
    }

    // Seeds two open quests with a proposed date on the same day, so both land on the same
    // calendar month: an ordinary one and a DM-only one. Open, not finalized, because a
    // finalized quest's Details page never renders the calendar.
    private async Task<(int OpenId, string OpenTitle, int DmOnlyId, string DmOnlyTitle)> SeedOpenQuestsAsync(string suffix)
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);

        var date = DateTime.Today.AddDays(10);
        var openTitle = $"OpenDetailsQuest{suffix}";
        var dmOnlyTitle = $"DmOnlyDetailsQuest{suffix}";

        var dm = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, $"detailsdm{suffix}", $"detailsdm{suffix}@test.com");

        var open = await TestDataHelper.CreateTestQuestAsync(
            factory.Services, dm.Id, openTitle, "Ordinary open quest");
        var dmOnly = await TestDataHelper.CreateTestQuestAsync(
            factory.Services, dm.Id, dmOnlyTitle, "DM-only open quest", dungeonMasterSession: true);

        await TestDataHelper.CreateProposedDateAsync(factory.Services, open.Id, date);
        await TestDataHelper.CreateProposedDateAsync(factory.Services, dmOnly.Id, date);

        return (open.Id, openTitle, dmOnly.Id, dmOnlyTitle);
    }

    [Fact]
    public async Task QuestDetails_PlayerViewer_DoesNotSeeOtherDmOnlySessionOnCalendar()
    {
        var (openId, openTitle, _, dmOnlyTitle) = await SeedOpenQuestsAsync("PlayerDesktop");
        var (authClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "detailsviewPlayerDesktop", "detailsviewPlayerDesktop@test.com", roles: ["Player"]);

        var (response, html) = await GetWithUserAgentAsync(
            $"/Quest/Details/{openId}", DesktopUserAgent, authClient.DefaultRequestHeaders.Authorization);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain(openTitle);
        html.Should().NotContain(dmOnlyTitle);
    }

    [Fact]
    public async Task QuestDetails_PlayerViewer_Mobile_DoesNotSeeOtherDmOnlySession()
    {
        var (openId, openTitle, _, dmOnlyTitle) = await SeedOpenQuestsAsync("PlayerMobile");
        var (authClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "detailsviewPlayerMobile", "detailsviewPlayerMobile@test.com", roles: ["Player"]);

        var (response, html) = await GetWithUserAgentAsync(
            $"/Quest/Details/{openId}", MobileUserAgent, authClient.DefaultRequestHeaders.Authorization);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain(openTitle);
        html.Should().NotContain(dmOnlyTitle);
    }

    [Fact]
    public async Task QuestDetails_DungeonMasterViewer_SeesOtherDmOnlySessionOnCalendar()
    {
        var (openId, openTitle, _, dmOnlyTitle) = await SeedOpenQuestsAsync("DmDesktop");
        var (authClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "detailsviewDmDesktop", "detailsviewDmDesktop@test.com", roles: ["DungeonMaster"]);

        var (response, html) = await GetWithUserAgentAsync(
            $"/Quest/Details/{openId}", DesktopUserAgent, authClient.DefaultRequestHeaders.Authorization);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain(openTitle);
        html.Should().Contain(dmOnlyTitle);
    }

    [Fact]
    public async Task QuestDetails_PlayerOpensDmOnlyQuestByUrl_DesktopCalendarKeepsItsOwnDates()
    {
        var (_, _, dmOnlyId, dmOnlyTitle) = await SeedOpenQuestsAsync("OwnDesktop");
        var (authClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "detailsviewOwnDesktop", "detailsviewOwnDesktop@test.com", roles: ["Player"]);

        var (response, html) = await GetWithUserAgentAsync(
            $"/Quest/Details/{dmOnlyId}", DesktopUserAgent, authClient.DefaultRequestHeaders.Authorization);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain(dmOnlyTitle);
        html.Should().Contain("current-quest");
    }

    [Fact]
    public async Task QuestDetails_PlayerOpensDmOnlyQuestByUrl_MobileCalendarKeepsItsOwnDates()
    {
        var (_, _, dmOnlyId, dmOnlyTitle) = await SeedOpenQuestsAsync("OwnMobile");
        var (authClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "detailsviewOwnMobile", "detailsviewOwnMobile@test.com", roles: ["Player"]);

        var (response, html) = await GetWithUserAgentAsync(
            $"/Quest/Details/{dmOnlyId}", MobileUserAgent, authClient.DefaultRequestHeaders.Authorization);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain(dmOnlyTitle);
        html.Should().Contain("calendar-date-entry-mobile");
    }
}
