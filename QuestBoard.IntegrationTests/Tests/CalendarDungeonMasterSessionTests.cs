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
}
