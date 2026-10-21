using System.Net;
using QuestBoard.Domain.Enums;
using QuestBoard.IntegrationTests.Helpers;

namespace QuestBoard.IntegrationTests.Controllers;

/// <summary>
/// Proves the page gate end to end: a board's stored settings decide whether a page is served and
/// where a refused visitor lands, through storage, mapping, policy, resolver and the global filter.
/// </summary>
public class BoardPageGateTests : IClassFixture<WebApplicationFactoryBase>, IAsyncLifetime
{
    private const string ShopToast = "Shop isn't used on this board.";

    private readonly WebApplicationFactoryBase _factory;

    public BoardPageGateTests(WebApplicationFactoryBase factory)
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

    [Fact]
    public async Task Shop_OffWithCalendarHome_RedirectsToCalendarWithToast()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        await BoardPageSeed.SetAsync(_factory, 1, BoardPageSet.Empty.With(BoardPage.Shop), BoardPage.Calendar);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(_factory);

        var response = await client.GetAsync("/Shop", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.Should().StartWith("/Calendar");

        var landed = await client.GetAsync(response.Headers.Location, TestContext.Current.CancellationToken);
        landed.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await landed.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        WebUtility.HtmlDecode(html).Should().Contain(ShopToast);
    }

    [Fact]
    public async Task Shop_WithDefaultSettings_IsServed()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(_factory);

        var response = await client.GetAsync("/Shop", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Shop_OnCampaignBoardWithNothingStored_RedirectsToQuestListWithToast()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        _factory.TestGroupContext.BoardType = BoardType.Campaign;
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(_factory);

        var response = await client.GetAsync("/Shop", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.Should().StartWith("/quests");

        var landed = await client.GetAsync(response.Headers.Location, TestContext.Current.CancellationToken);
        landed.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await landed.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        WebUtility.HtmlDecode(html).Should().Contain(ShopToast);
    }

    [Fact]
    public async Task Shop_Off_IsRefusedForSuperAdminToo()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        await BoardPageSeed.SetAsync(_factory, 1, BoardPageSet.Empty.With(BoardPage.Shop), BoardPage.Calendar);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(_factory);

        var response = await client.GetAsync("/Shop", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.Should().StartWith("/Calendar");
    }

    [Fact]
    public async Task Shop_WithNoResolvedBoardType_AnswersAsItDoesWithShopOn()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        _factory.TestGroupContext.BoardType = null;
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(_factory);

        var withShopOn = await client.GetAsync("/Shop", TestContext.Current.CancellationToken);
        await BoardPageSeed.SetAsync(_factory, 1, BoardPageSet.Empty.With(BoardPage.Shop), BoardPage.Calendar);
        var withShopOff = await client.GetAsync("/Shop", TestContext.Current.CancellationToken);

        withShopOff.StatusCode.Should().Be(withShopOn.StatusCode);
        withShopOff.Headers.Location?.OriginalString.Should().Be(withShopOn.Headers.Location?.OriginalString);
    }
}
