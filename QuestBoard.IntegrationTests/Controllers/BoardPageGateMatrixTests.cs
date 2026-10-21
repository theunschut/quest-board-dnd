using System.Net;
using QuestBoard.Domain.Enums;
using QuestBoard.IntegrationTests.Helpers;

namespace QuestBoard.IntegrationTests.Controllers;

/// <summary>
/// Proves the page gate for each gated controller over real HTTP: with a page switched off a typed
/// URL or crafted request is refused with the page's toast and lands on the board's home, and with
/// the page on the same request is served as before.
/// </summary>
public class BoardPageGateMatrixTests : IClassFixture<WebApplicationFactoryBase>, IAsyncLifetime
{
    private const string CalendarToast = "The calendar isn't used on this board.";
    private const string EventsToast = "Events aren't used on this board.";

    private readonly WebApplicationFactoryBase _factory;

    public BoardPageGateMatrixTests(WebApplicationFactoryBase factory)
    {
        _factory = factory;
    }

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    // The board context and the stored settings are shared across the whole class fixture, so this
    // restores the baseline and no test's premise depends on which test ran before it.
    public async ValueTask DisposeAsync()
    {
        _factory.TestGroupContext.BoardType = BoardType.OneShot;
        _factory.TestGroupContext.ActiveGroupId = 1;
        await BoardPageSeed.ResetAsync(_factory, 1);
    }

    // Asserts the request was turned away to the expected landing path, and that following the
    // redirect with the same client shows the page's toast.
    private static async Task AssertRefusedAsync(HttpClient client, HttpMethod method, string url, string landingPath, string toast)
    {
        using var request = new HttpRequestMessage(method, url);
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.Should().StartWith(landingPath);

        var landed = await client.GetAsync(response.Headers.Location, TestContext.Current.CancellationToken);
        landed.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await landed.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        WebUtility.HtmlDecode(html).Should().Contain(toast);
    }

    private async Task<int> SeedSeriesAsync()
    {
        await using var ctx = _factory.Database.CreateContext();
        var anchorDate = DateOnly.FromDateTime(DateTime.Today);
        var series = new EventSeriesEntity
        {
            Title = "Gate matrix series",
            Description = "Weekly session.",
            StartTime = new TimeOnly(19, 0),
            AnchorDate = anchorDate,
            IntervalWeeks = 1,
            WeekDay = (int)anchorDate.DayOfWeek,
            CycleMask = "1",
            EndDate = null,
            GroupId = 1,
            CreatedAt = DateTime.UtcNow
        };
        ctx.EventSeries.Add(series);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        return series.Id;
    }

    [Fact]
    public async Task Calendar_Off_RedirectsToQuestListWithToast()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        await BoardPageSeed.SetAsync(_factory, 1, BoardPageSet.Empty.With(BoardPage.Calendar), BoardPage.Quests);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(_factory);

        await AssertRefusedAsync(client, HttpMethod.Get, "/Calendar", "/quests", CalendarToast);
    }

    [Fact]
    public async Task Calendar_WithDefaultSettings_IsServed()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(_factory);

        var response = await client.GetAsync("/Calendar", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("/Events")]
    [InlineData("/Events/Create")]
    public async Task Events_Off_RedirectsDungeonMasterToQuestListWithToast(string url)
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        await BoardPageSeed.SetAsync(_factory, 1, BoardPageSet.Empty.With(BoardPage.Events), BoardPage.Quests);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedDMClientAsync(_factory);

        await AssertRefusedAsync(client, HttpMethod.Get, url, "/quests", EventsToast);
    }

    [Fact]
    public async Task Events_WithDefaultSettings_IsServedToDungeonMaster()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedDMClientAsync(_factory);

        var response = await client.GetAsync("/Events", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Series_Off_RedirectsToQuestListWithToast()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var seriesId = await SeedSeriesAsync();
        await BoardPageSeed.SetAsync(_factory, 1, BoardPageSet.Empty.With(BoardPage.Events), BoardPage.Quests);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(_factory);

        await AssertRefusedAsync(client, HttpMethod.Get, $"/Series/Details/{seriesId}", "/quests", EventsToast);
    }
}
