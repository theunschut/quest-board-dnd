using QuestBoard.Domain.Enums;
using QuestBoard.IntegrationTests.Helpers;
using System.Net;
using System.Net.Http.Headers;

namespace QuestBoard.IntegrationTests.Controllers;

/// <summary>
/// Locks the calendar's board-type-aware quest load: a campaign board's calendar renders
/// events only, a one-shot board's calendar renders both quests and events unchanged, and a
/// board type that cannot be resolved is treated the same as one-shot rather than collapsed
/// onto campaign.
/// </summary>
public class CalendarBoardTypeScopeTests(WebApplicationFactoryBase factory)
    : IClassFixture<WebApplicationFactoryBase>, IAsyncLifetime
{
    private const string MobileUserAgent =
        "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1";

    private const string DesktopUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    // Several facts here mutate the shared singleton the rest of the suite reads, so both
    // fields are restored once this class finishes running.
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

    // Clears the database, then seeds one plain event and one finalized quest onto board 1,
    // both landing on the same date so a single calendar request can prove or disprove either
    // one's presence. Titles carry a per-fact suffix so no two facts can collide on the page.
    private async Task<(string EventTitle, string QuestTitle, DateTime Date)> SeedEventAndQuestAsync(string suffix)
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);

        var date = DateTime.Today.AddDays(7);
        var eventTitle = $"ScopeEvent{suffix}";
        var questTitle = $"ScopeQuest{suffix}";

        await using (var seedCtx = factory.Database.CreateContext())
        {
            seedCtx.Events.Add(new EventEntity
            {
                Title = eventTitle,
                GroupId = 1,
                Date = DateOnly.FromDateTime(date),
                CreatedAt = DateTime.UtcNow
            });
            await seedCtx.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var dm = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, $"scopedm{suffix}", $"scopedm{suffix}@test.com");

        var quest = await TestDataHelper.CreateTestQuestAsync(
            factory.Services,
            dm.Id,
            questTitle,
            "Board-type calendar scope test quest",
            5,
            isFinalized: true);

        // Add a proposed date that matches the finalized date, then set the finalized date on
        // the tracked row -- the proven recipe for a quest that actually renders on the
        // calendar.
        await TestDataHelper.CreateProposedDateAsync(factory.Services, quest.Id, date);

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
            var questToUpdate = await context.Quests.FindAsync([quest.Id], TestContext.Current.CancellationToken);
            if (questToUpdate != null)
            {
                questToUpdate.FinalizedDate = date;
                await context.SaveChangesAsync(TestContext.Current.CancellationToken);
            }
        }

        return (eventTitle, questTitle, date);
    }

    [Fact]
    public async Task Calendar_CampaignBoard_DesktopAgent_RendersEventsWithoutQuests()
    {
        var (eventTitle, questTitle, date) = await SeedEventAndQuestAsync("CampaignDesktop");
        factory.TestGroupContext.BoardType = BoardType.Campaign;
        var (authClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "scopeview_campaign_desktop", "scopeview_campaign_desktop@test.com");

        var (response, html) = await GetWithUserAgentAsync(
            $"/Calendar?year={date.Year}&month={date.Month}", DesktopUserAgent, authClient.DefaultRequestHeaders.Authorization);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        // The event title doubles as the anchor proving the page actually rendered, so an
        // error page or a login redirect cannot make the quest-absence assertion pass for free.
        html.Should().Contain(eventTitle);
        html.Should().NotContain(questTitle);
        html.Should().NotContain("agenda-card-mobile");
    }

    [Fact]
    public async Task Calendar_CampaignBoard_MobileAgent_RendersEventsWithoutQuests()
    {
        var (eventTitle, questTitle, date) = await SeedEventAndQuestAsync("CampaignMobile");
        factory.TestGroupContext.BoardType = BoardType.Campaign;
        var (authClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "scopeview_campaign_mobile", "scopeview_campaign_mobile@test.com");

        var (response, html) = await GetWithUserAgentAsync(
            $"/Calendar?year={date.Year}&month={date.Month}", MobileUserAgent, authClient.DefaultRequestHeaders.Authorization);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain(eventTitle);
        html.Should().NotContain(questTitle);
        html.Should().Contain("agenda-card-mobile");
    }

    [Fact]
    public async Task Calendar_OneShotBoard_DesktopAgent_RendersQuestsAndEvents()
    {
        var (eventTitle, questTitle, date) = await SeedEventAndQuestAsync("OneShotDesktop");
        factory.TestGroupContext.BoardType = BoardType.OneShot;
        var (authClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "scopeview_oneshot_desktop", "scopeview_oneshot_desktop@test.com");

        var (response, html) = await GetWithUserAgentAsync(
            $"/Calendar?year={date.Year}&month={date.Month}", DesktopUserAgent, authClient.DefaultRequestHeaders.Authorization);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain(eventTitle);
        html.Should().Contain(questTitle);
        html.Should().NotContain("agenda-card-mobile");
    }

    [Fact]
    public async Task Calendar_OneShotBoard_MobileAgent_RendersQuestsAndEvents()
    {
        var (eventTitle, questTitle, date) = await SeedEventAndQuestAsync("OneShotMobile");
        factory.TestGroupContext.BoardType = BoardType.OneShot;
        var (authClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "scopeview_oneshot_mobile", "scopeview_oneshot_mobile@test.com");

        var (response, html) = await GetWithUserAgentAsync(
            $"/Calendar?year={date.Year}&month={date.Month}", MobileUserAgent, authClient.DefaultRequestHeaders.Authorization);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain(eventTitle);
        html.Should().Contain(questTitle);
        html.Should().Contain("agenda-card-mobile");
    }

    [Fact]
    public async Task Calendar_UnresolvedBoardType_RendersQuestsAndEvents()
    {
        var (eventTitle, questTitle, date) = await SeedEventAndQuestAsync("Unresolved");
        // An unresolved board type is its own state and must not be collapsed onto campaign --
        // it renders the same as one-shot.
        factory.TestGroupContext.BoardType = null;
        var (authClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "scopeview_unresolved", "scopeview_unresolved@test.com");

        var (response, html) = await GetWithUserAgentAsync(
            $"/Calendar?year={date.Year}&month={date.Month}", DesktopUserAgent, authClient.DefaultRequestHeaders.Authorization);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain(eventTitle);
        html.Should().Contain(questTitle);
        html.Should().NotContain("agenda-card-mobile");
    }
}
