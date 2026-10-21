using System.Net;
using QuestBoard.Domain.Enums;
using QuestBoard.IntegrationTests.Helpers;

namespace QuestBoard.IntegrationTests.Controllers;

/// <summary>
/// Proves the places that lead "home" all land on the board's chosen home page: the root address,
/// the board picker (and so login) and the account page's back link, per board and never on a page
/// the board has switched off or cannot offer.
/// </summary>
public class BoardPageHomeTests : IClassFixture<WebApplicationFactoryBase>, IAsyncLifetime
{
    private const string MobileUserAgent =
        "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1";

    private const string DesktopUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

    private readonly WebApplicationFactoryBase _factory;

    public BoardPageHomeTests(WebApplicationFactoryBase factory)
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
        await BoardPageSeed.ResetAsync(_factory, 2);
    }

    private static string Location(HttpResponseMessage response) => response.Headers.Location!.OriginalString;

    [Theory]
    [InlineData(BoardPage.Quests, "/quests")]
    [InlineData(BoardPage.Calendar, "/Calendar")]
    [InlineData(BoardPage.Events, "/Events")]
    [InlineData(BoardPage.QuestLog, "/QuestLog")]
    [InlineData(BoardPage.Characters, "/Characters")]
    [InlineData(BoardPage.Contacts, "/Contacts")]
    [InlineData(BoardPage.Players, "/Players")]
    [InlineData(BoardPage.Shop, "/Shop")]
    public async Task Root_LandsOnTheStoredHomePage(BoardPage home, string expectedPath)
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        await BoardPageSeed.SetAsync(_factory, 1, BoardPageSet.Empty, home);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(_factory);

        var response = await client.GetAsync("/", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        Location(response).Should().Be(expectedPath);
    }

    [Fact]
    public async Task Root_WithDefaultSettings_LandsOnTheQuestList()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(_factory);

        var response = await client.GetAsync("/", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        Location(response).Should().Be("/quests");
    }

    [Fact]
    public async Task Root_WhenTheStoredHomeIsSwitchedOff_FallsBackToTheQuestList()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        await BoardPageSeed.SetAsync(_factory, 1, BoardPageSet.Empty.With(BoardPage.Calendar), BoardPage.Calendar);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(_factory);

        var response = await client.GetAsync("/", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        Location(response).Should().Be("/quests");
    }

    [Fact]
    public async Task Root_OnACampaignBoardStoringShopAsHome_FallsBackToTheQuestList()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        _factory.TestGroupContext.BoardType = BoardType.Campaign;
        await BoardPageSeed.SetAsync(_factory, 1, BoardPageSet.Empty, BoardPage.Shop);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(_factory);

        var response = await client.GetAsync("/", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        Location(response).Should().Be("/quests");
    }

    [Fact]
    public async Task Root_ForASuperAdminWithNoActiveBoard_GoesToTheBoardPicker()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(_factory);
        _factory.TestGroupContext.ActiveGroupId = null;

        var response = await client.GetAsync("/", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        Location(response).Should().StartWith("/groups/pick");
    }

    [Fact]
    public async Task Root_ForASignedOutVisitor_ShowsTheLandingPage()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        await BoardPageSeed.SetAsync(_factory, 1, BoardPageSet.Empty.With(BoardPage.Shop), BoardPage.Calendar);
        var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PickingABoardWithNoReturnUrl_GoesToTheRoot()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(_factory);

        var response = await client.PostAsync(
            "/GroupPicker/SelectGroup",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["groupId"] = "1" }),
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        Location(response).Should().Be("/");
    }

    [Fact]
    public async Task PickingABoardWithALocalReturnUrl_GoesThereInsteadOfHome()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        await BoardPageSeed.SetAsync(_factory, 1, BoardPageSet.Empty, BoardPage.Calendar);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(_factory);

        var response = await client.PostAsync(
            "/GroupPicker/SelectGroup",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["groupId"] = "1", ["returnUrl"] = "/Contacts" }),
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        Location(response).Should().Be("/Contacts");
    }

    [Fact]
    public async Task PickingABoardWithAnExternalReturnUrl_IgnoresItAndGoesToTheRoot()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(_factory);

        var response = await client.PostAsync(
            "/GroupPicker/SelectGroup",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["groupId"] = "1", ["returnUrl"] = "https://evil.example/" }),
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        Location(response).Should().Be("/");
    }

    [Fact]
    public async Task TwoBoardsInOneLinkedSet_KeepTheirOwnHomePages()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        await BoardMoveSeed.SeedBoardAsync(_factory, 2, "Second Board", BoardType.OneShot);
        await BoardMoveSeed.LinkAsync(_factory, BoardType.OneShot, 1, 2);
        await BoardPageSeed.SetAsync(_factory, 1, BoardPageSet.Empty, BoardPage.Calendar);
        await BoardPageSeed.SetAsync(_factory, 2, BoardPageSet.Empty, BoardPage.Events);
        var (client, user) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(_factory);
        await BoardMoveSeed.SeedMembershipAsync(_factory, user.Id, 2, GroupRole.Player);

        _factory.TestGroupContext.ActiveGroupId = 1;
        var onFirst = await client.GetAsync("/", TestContext.Current.CancellationToken);
        _factory.TestGroupContext.ActiveGroupId = 2;
        var onSecond = await client.GetAsync("/", TestContext.Current.CancellationToken);

        onFirst.StatusCode.Should().Be(HttpStatusCode.Redirect);
        Location(onFirst).Should().Be("/Calendar");
        onSecond.StatusCode.Should().Be(HttpStatusCode.Redirect);
        Location(onSecond).Should().Be("/Events");
    }

    [Theory]
    [InlineData(DesktopUserAgent)]
    [InlineData(MobileUserAgent)]
    public async Task AccountProfile_BackLinkTargetsTheRoot(string userAgent)
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(_factory);
        var request = new HttpRequestMessage(HttpMethod.Get, "/Account/Profile");
        request.Headers.TryAddWithoutValidation("User-Agent", userAgent);

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        html.Should().Contain("""<a class="btn btn-warning" href="/">""");
    }
}
