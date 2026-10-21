using System.Net;
using QuestBoard.Domain.Enums;
using QuestBoard.IntegrationTests.Helpers;

namespace QuestBoard.IntegrationTests.Controllers;

/// <summary>
/// Proves a super admin changes a board's pages and home page through the Platform Edit form only
/// by the settings rules: what the form never posted stays as stored, what the board type forbids
/// is ignored whatever is posted, and a refusal re-renders the form with nothing saved.
/// </summary>
public class BoardPagePlatformTests : IClassFixture<WebApplicationFactoryBase>, IAsyncLifetime
{
    private const int OneShotBoardId = 1;
    private const int CampaignBoardId = 2;

    private readonly WebApplicationFactoryBase _factory;

    public BoardPagePlatformTests(WebApplicationFactoryBase factory)
    {
        _factory = factory;
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        await BoardPageSeed.ResetAsync(_factory, OneShotBoardId);
        await BoardPageSeed.ResetAsync(_factory, CampaignBoardId);
        _factory.TestGroupContext.ActiveGroupId = 1;
        _factory.TestGroupContext.BoardType = BoardType.OneShot;
    }

    private async Task<HttpClient> StartAsync(bool withCampaignBoard = false)
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        if (withCampaignBoard)
        {
            await TestDataHelper.SeedCampaignGroupAsync(_factory.Services, CampaignBoardId);
        }

        var (client, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(_factory);
        return client;
    }

    private static Task<HttpResponseMessage> PostEditAsync(HttpClient client, int boardId, string name, params (string Key, string Value)[] fields)
    {
        var form = new List<KeyValuePair<string, string>>
        {
            new("Id", boardId.ToString()),
            new("Name", name)
        };
        form.AddRange(fields.Select(f => new KeyValuePair<string, string>(f.Key, f.Value)));

        return client.PostAsync("/platform/Group/Edit", new FormUrlEncodedContent(form), Token);
    }

    // The posted shape of one switch row: the page name and the value the checkbox would send.
    private static (string, string)[] Row(int index, BoardPage page, bool enabled) =>
    [
        ($"Pages[{index}].Page", page.ToString()),
        ($"Pages[{index}].Enabled", enabled ? "true" : "false")
    ];

    private async Task<(int Disabled, int Home)> StoredSettingsAsync(int boardId)
    {
        await using var ctx = _factory.Database.CreateContext();
        var group = await ctx.Groups.SingleAsync(g => g.Id == boardId, Token);
        return (group.DisabledPages, group.HomePage);
    }

    private async Task<string> ReadDecodedAsync(HttpResponseMessage response) =>
        WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync(Token));

    private async Task SeedEventAsync(int groupId, DateOnly date)
    {
        await using var ctx = _factory.Database.CreateContext();
        ctx.Events.Add(new EventEntity { GroupId = groupId, Title = $"Event {date}", Date = date });
        await ctx.SaveChangesAsync(Token);
    }

    private async Task SeedSeriesAsync(int groupId, int count)
    {
        await using var ctx = _factory.Database.CreateContext();
        for (var i = 0; i < count; i++)
        {
            ctx.EventSeries.Add(new EventSeriesEntity
            {
                GroupId = groupId,
                Title = $"Series {i}",
                AnchorDate = new DateOnly(2030, 1, 1),
                IntervalWeeks = 1,
                CycleMask = "1",
                EndDate = null
            });
        }

        await ctx.SaveChangesAsync(Token);
    }

    private static readonly int ShopBit = BoardPageSet.Empty.With(BoardPage.Shop).Mask;
    private static readonly int CalendarBit = BoardPageSet.Empty.With(BoardPage.Calendar).Mask;
    private static readonly int EventsBit = BoardPageSet.Empty.With(BoardPage.Events).Mask;

    [Fact]
    public async Task Edit_PostingOnlyIdAndName_LeavesSettingsAsStored()
    {
        var client = await StartAsync();
        await BoardPageSeed.SetAsync(_factory, OneShotBoardId, BoardPageSet.Empty.With(BoardPage.Shop), BoardPage.Calendar);

        var response = await PostEditAsync(client, OneShotBoardId, "Renamed Board");

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found);
        var stored = await StoredSettingsAsync(OneShotBoardId);
        stored.Disabled.Should().Be(ShopBit);
        stored.Home.Should().Be((int)BoardPage.Calendar);
    }

    [Fact]
    public async Task Edit_PostingSwitchAndHome_SavesBothAndShowsTheSuccessToast()
    {
        var client = await StartAsync();

        var response = await PostEditAsync(client, OneShotBoardId, "EuphoriaInn",
            [.. Row(0, BoardPage.Shop, false), ("HomePage", nameof(BoardPage.Calendar))]);

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found);
        var stored = await StoredSettingsAsync(OneShotBoardId);
        stored.Disabled.Should().Be(ShopBit);
        stored.Home.Should().Be((int)BoardPage.Calendar);

        var followed = await client.GetAsync(response.Headers.Location, Token);
        (await ReadDecodedAsync(followed)).Should().Contain("Board settings saved.");
    }

    [Fact]
    public async Task Edit_CampaignBoard_PostedSwitchesForTypeForbiddenPagesAreIgnored()
    {
        var client = await StartAsync(withCampaignBoard: true);

        var response = await PostEditAsync(client, CampaignBoardId, "Campaign Test Group",
            [.. Row(0, BoardPage.Shop, true), .. Row(1, BoardPage.Players, false), .. Row(2, BoardPage.DungeonMasterProfile, false)]);

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found);
        var stored = await StoredSettingsAsync(CampaignBoardId);
        stored.Disabled.Should().Be(0);
    }

    [Fact]
    public async Task Edit_SwitchingOffTheStoredHome_IsRefusedAndNothingIsSaved()
    {
        var client = await StartAsync();
        await BoardPageSeed.SetAsync(_factory, OneShotBoardId, BoardPageSet.Empty, BoardPage.Calendar);

        var response = await PostEditAsync(client, OneShotBoardId, "Renamed Board", Row(0, BoardPage.Calendar, false));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadDecodedAsync(response)).Should().Contain("Calendar is this board's home page; pick another home page first.");
        var stored = await StoredSettingsAsync(OneShotBoardId);
        stored.Disabled.Should().Be(0);
        stored.Home.Should().Be((int)BoardPage.Calendar);
        await using var ctx = _factory.Database.CreateContext();
        (await ctx.Groups.SingleAsync(g => g.Id == OneShotBoardId, Token)).Name.Should().Be("EuphoriaInn");
    }

    [Fact]
    public async Task Edit_RefusalOnACampaignBoard_RerendersTheRealBoardType()
    {
        var client = await StartAsync(withCampaignBoard: true);
        await BoardPageSeed.SetAsync(_factory, CampaignBoardId, BoardPageSet.Empty, BoardPage.Calendar);

        var response = await PostEditAsync(client, CampaignBoardId, "Campaign Test Group", Row(0, BoardPage.Calendar, false));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await ReadDecodedAsync(response);
        html.Should().Contain("Calendar is this board's home page; pick another home page first.");
        html.Should().MatchRegex("<option selected=\"selected\" value=\"1\">Campaign</option>");
    }

    [Fact]
    public async Task Edit_SwitchingEventsOff_WithAnUpcomingEvent_IsRefusedAndNothingIsSaved()
    {
        var client = await StartAsync();
        await SeedEventAsync(OneShotBoardId, new DateOnly(2031, 1, 10));

        var response = await PostEditAsync(client, OneShotBoardId, "EuphoriaInn", Row(0, BoardPage.Events, false));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadDecodedAsync(response)).Should().Contain("1 upcoming event");
        (await StoredSettingsAsync(OneShotBoardId)).Disabled.Should().Be(0);
    }

    [Fact]
    public async Task Edit_SwitchingEventsOff_WithOnlyPastEvents_Saves()
    {
        var client = await StartAsync();
        await SeedEventAsync(OneShotBoardId, new DateOnly(2020, 1, 10));

        var response = await PostEditAsync(client, OneShotBoardId, "EuphoriaInn", Row(0, BoardPage.Events, false));

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found);
        (await StoredSettingsAsync(OneShotBoardId)).Disabled.Should().Be(EventsBit);
    }

    [Fact]
    public async Task Edit_SwitchingEventsOff_WithARunningSeries_IsRefusedAndNothingIsSaved()
    {
        var client = await StartAsync();
        await SeedSeriesAsync(OneShotBoardId, count: 2);

        var response = await PostEditAsync(client, OneShotBoardId, "EuphoriaInn", Row(0, BoardPage.Events, false));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadDecodedAsync(response)).Should().Contain("End each series first.");
        (await StoredSettingsAsync(OneShotBoardId)).Disabled.Should().Be(0);
    }

    [Fact]
    public async Task Edit_SwitchingEventsOff_WithOneRunningSeries_AsksToEndTheSeries()
    {
        var client = await StartAsync();
        await SeedSeriesAsync(OneShotBoardId, count: 1);

        var response = await PostEditAsync(client, OneShotBoardId, "EuphoriaInn", Row(0, BoardPage.Events, false));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadDecodedAsync(response)).Should().Contain("End the series first.");
    }

    [Fact]
    public async Task Edit_EventsRefusal_CountsTheEditedBoardNotTheActiveBoard()
    {
        var client = await StartAsync(withCampaignBoard: true);
        _factory.TestGroupContext.ActiveGroupId = OneShotBoardId;
        await SeedEventAsync(OneShotBoardId, new DateOnly(2031, 1, 10));
        await SeedEventAsync(OneShotBoardId, new DateOnly(2031, 1, 11));
        await SeedEventAsync(CampaignBoardId, new DateOnly(2031, 2, 10));

        var response = await PostEditAsync(client, CampaignBoardId, "Campaign Test Group", Row(0, BoardPage.Events, false));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await ReadDecodedAsync(response);
        html.Should().Contain("1 upcoming event");
        html.Should().NotContain("2 upcoming events");
        html.Should().NotContain("3 upcoming events");
    }

    [Fact]
    public async Task Edit_EditingABoardWithNoEventsOfItsOwn_IgnoresTheActiveBoardsEvents()
    {
        var client = await StartAsync(withCampaignBoard: true);
        _factory.TestGroupContext.ActiveGroupId = OneShotBoardId;
        await SeedEventAsync(OneShotBoardId, new DateOnly(2031, 1, 10));

        var response = await PostEditAsync(client, CampaignBoardId, "Campaign Test Group", Row(0, BoardPage.Events, false));

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found);
        (await StoredSettingsAsync(CampaignBoardId)).Disabled.Should().Be(EventsBit);
    }

    [Fact]
    public async Task Edit_BoardInALinkedSet_KeepsItsLinkedBoardSetId()
    {
        var client = await StartAsync();
        int setId;
        await using (var ctx = _factory.Database.CreateContext())
        {
            var set = new LinkedBoardSetEntity { Name = "Platform Pages Set" };
            ctx.LinkedBoardSets.Add(set);
            await ctx.SaveChangesAsync(Token);
            setId = set.Id;

            var group = await ctx.Groups.SingleAsync(g => g.Id == OneShotBoardId, Token);
            group.LinkedBoardSetId = setId;
            await ctx.SaveChangesAsync(Token);
        }

        var response = await PostEditAsync(client, OneShotBoardId, "EuphoriaInn", Row(0, BoardPage.Calendar, false));

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found);
        await using var verify = _factory.Database.CreateContext();
        var stored = await verify.Groups.SingleAsync(g => g.Id == OneShotBoardId, Token);
        stored.LinkedBoardSetId.Should().Be(setId);
        stored.DisabledPages.Should().Be(CalendarBit);
    }

    [Fact]
    public async Task Edit_SwitchingShopOffAndOn_KeepsTheShopItemsAndListsThemAgain()
    {
        var client = await StartAsync();
        var (_, dm) = await AuthenticationHelper.CreateAuthenticatedDMClientAsync(_factory);
        await TestDataHelper.CreateShopItemAsync(_factory.Services, dm.Id, name: "Lantern of Keeping");

        await PostEditAsync(client, OneShotBoardId, "EuphoriaInn", Row(0, BoardPage.Shop, false));
        (await StoredSettingsAsync(OneShotBoardId)).Disabled.Should().Be(ShopBit);
        await PostEditAsync(client, OneShotBoardId, "EuphoriaInn", Row(0, BoardPage.Shop, true));
        (await StoredSettingsAsync(OneShotBoardId)).Disabled.Should().Be(0);

        await using (var ctx = _factory.Database.CreateContext())
        {
            (await ctx.ShopItems.IgnoreQueryFilters().CountAsync(Token)).Should().Be(1);
        }

        var shop = await client.GetAsync("/Shop", Token);
        shop.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadDecodedAsync(shop)).Should().Contain("Lantern of Keeping");
    }

    [Fact]
    public async Task Create_NewBoard_StartsWithEveryPageOnAndTheQuestListAsHome()
    {
        var client = await StartAsync();
        var name = "Fresh Board " + Guid.NewGuid().ToString("N")[..8];

        var response = await client.PostAsync("/platform/Group/Create", new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("Name", name),
            new KeyValuePair<string, string>("BoardType", ((int)BoardType.OneShot).ToString())
        ]), Token);

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found);
        await using var ctx = _factory.Database.CreateContext();
        var created = await ctx.Groups.SingleAsync(g => g.Name == name, Token);
        created.DisabledPages.Should().Be(0);
        created.HomePage.Should().Be(0);
    }
}
