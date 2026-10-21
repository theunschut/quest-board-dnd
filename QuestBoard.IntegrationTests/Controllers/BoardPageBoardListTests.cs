using System.Net;
using System.Text.RegularExpressions;
using QuestBoard.Domain.Enums;
using QuestBoard.IntegrationTests.Helpers;

namespace QuestBoard.IntegrationTests.Controllers;

/// <summary>
/// Proves the Platform board list marks a board whose stored home is not the quest list, on both
/// the desktop and the mobile layout, and leaves every board on the default home untouched.
/// </summary>
public class BoardPageBoardListTests : IClassFixture<WebApplicationFactoryBase>, IAsyncLifetime
{
    private const string MobileUserAgent =
        "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1";

    private const string DesktopUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

    private const string ConfiguredBoardName = "EuphoriaInn";
    private const string DefaultBoardName = "Campaign Test Group";
    private const int ConfiguredBoardId = 1;
    private const int DefaultBoardId = 2;

    private readonly WebApplicationFactoryBase _factory;

    public BoardPageBoardListTests(WebApplicationFactoryBase factory)
    {
        _factory = factory;
    }

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    // The stored settings are shared across the whole class fixture, so this puts both boards back
    // to their unconfigured state and no test's premise depends on which test ran before it.
    public async ValueTask DisposeAsync()
    {
        await BoardPageSeed.ResetAsync(_factory, ConfiguredBoardId);
        await BoardPageSeed.ResetAsync(_factory, DefaultBoardId);
    }

    public static TheoryData<string> UserAgents => new() { DesktopUserAgent, MobileUserAgent };

    [Theory]
    [MemberData(nameof(UserAgents))]
    public async Task BoardList_BoardWithStoredHome_ShowsHomeBadgeOnItsRow(string userAgent)
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        await TestDataHelper.SeedCampaignGroupAsync(_factory.Services, DefaultBoardId);
        await BoardPageSeed.SetAsync(_factory, ConfiguredBoardId, BoardPageSet.Empty, BoardPage.Calendar);

        var html = await GetBoardListAsync(userAgent);

        RowFor(html, ConfiguredBoardName).Should().Contain("Home: Calendar");
    }

    [Theory]
    [MemberData(nameof(UserAgents))]
    public async Task BoardList_BoardOnDefaultHome_ShowsNoHomeBadgeOnItsRow(string userAgent)
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        await TestDataHelper.SeedCampaignGroupAsync(_factory.Services, DefaultBoardId);
        await BoardPageSeed.SetAsync(_factory, ConfiguredBoardId, BoardPageSet.Empty, BoardPage.Calendar);

        var html = await GetBoardListAsync(userAgent);

        RowFor(html, DefaultBoardName).Should().NotContain("Home:");
    }

    private async Task<string> GetBoardListAsync(string userAgent)
    {
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(_factory);
        var request = new HttpRequestMessage(HttpMethod.Get, "/platform/Group");
        request.Headers.TryAddWithoutValidation("User-Agent", userAgent);

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
    }

    // A desktop row is a <tr>, a mobile row is a group-card-mobile block; the one that names the
    // board and carries its Open Board button is the board's own row on either layout.
    private static string RowFor(string html, string boardName)
    {
        var rows = Regex.Split(html, "<tr[ >]|class=\"group-card-mobile");
        var row = rows.SingleOrDefault(r => r.Contains(boardName) && r.Contains("Open Board"));
        row.Should().NotBeNull($"the board list should hold a row for '{boardName}'");
        return row!;
    }
}
