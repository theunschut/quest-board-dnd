using System.Net;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
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
    private const string QuestListToast = "The quest list isn't used on this board.";
    private const string QuestLogToast = "The Quest Log isn't used on this board.";
    private const string CharactersToast = "Characters aren't used on this board.";
    private const string ShopToast = "Shop isn't used on this board.";
    private const string PlayersToast = "The Players page isn't used on this board.";
    private const string DmProfileToast = "DM profiles aren't used on this board.";
    private const string ContactsToast = "Contacts aren't used on this board.";

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

    private async Task<int> CountShopItemsAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
        return await ctx.ShopItems.IgnoreQueryFilters().CountAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<HttpResponseMessage> PostNewShopItemAsync(HttpClient client, string token)
    {
        var form = AntiForgeryHelper.CreateFormContentWithAntiForgeryToken(new Dictionary<string, string>
        {
            ["Name"] = "Gate matrix blade",
            ["Description"] = "A blade used to prove the gate.",
            ["Type"] = ((int)ItemType.Equipment).ToString(),
            ["Rarity"] = ((int)ItemRarity.Common).ToString(),
            ["Price"] = "5",
            ["Quantity"] = "1"
        }, token);
        return await client.PostAsync("/ShopManagement/Create", form, TestContext.Current.CancellationToken);
    }

    // Pages that exist on one-shot boards only: a Campaign board never offers them, whatever is stored.
    [Theory]
    [InlineData("/ShopManagement", true, ShopToast)]
    [InlineData("/Players", false, PlayersToast)]
    [InlineData("/DungeonMaster/Profile/{dm}", false, DmProfileToast)]
    [InlineData("/DungeonMaster/EditProfile", true, DmProfileToast)]
    public async Task OneShotOnlyPages_OnCampaignBoard_RedirectToQuestListWithToast(string urlTemplate, bool asDungeonMaster, string toast)
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        _factory.TestGroupContext.BoardType = BoardType.Campaign;
        var (dmClient, dm) = await AuthenticationHelper.CreateAuthenticatedDMClientAsync(_factory);
        var (playerClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(_factory);
        var client = asDungeonMaster ? dmClient : playerClient;

        await AssertRefusedAsync(client, HttpMethod.Get, urlTemplate.Replace("{dm}", dm.Id.ToString()), "/quests", toast);
    }

    [Theory]
    [InlineData("/ShopManagement", true)]
    [InlineData("/Players", false)]
    [InlineData("/DungeonMaster/Profile/{dm}", false)]
    [InlineData("/DungeonMaster/EditProfile", true)]
    public async Task OneShotOnlyPages_OnOneShotBoard_AreServed(string urlTemplate, bool asDungeonMaster)
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (dmClient, dm) = await AuthenticationHelper.CreateAuthenticatedDMClientAsync(_factory);
        var (playerClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(_factory);
        var client = asDungeonMaster ? dmClient : playerClient;

        var response = await client.GetAsync(urlTemplate.Replace("{dm}", dm.Id.ToString()), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("player")]
    [InlineData("dm")]
    [InlineData("admin")]
    [InlineData("superadmin")]
    public async Task Players_Off_RedirectsEveryRoleToQuestListWithToast(string role)
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        await BoardPageSeed.SetAsync(_factory, 1, BoardPageSet.Empty.With(BoardPage.Players), BoardPage.Quests);
        var (client, _) = role switch
        {
            "dm" => await AuthenticationHelper.CreateAuthenticatedDMClientAsync(_factory),
            "admin" => await AuthenticationHelper.CreateAuthenticatedAdminClientAsync(_factory),
            "superadmin" => await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(_factory),
            _ => await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(_factory)
        };

        await AssertRefusedAsync(client, HttpMethod.Get, "/Players", "/quests", PlayersToast);
    }

    [Theory]
    [InlineData("/DungeonMaster/Profile/{dm}", false)]
    [InlineData("/DungeonMaster/EditProfile", true)]
    public async Task DungeonMasterProfile_Off_RedirectsToQuestListWithToast(string urlTemplate, bool asDungeonMaster)
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        await BoardPageSeed.SetAsync(_factory, 1, BoardPageSet.Empty.With(BoardPage.DungeonMasterProfile), BoardPage.Quests);
        var (dmClient, dm) = await AuthenticationHelper.CreateAuthenticatedDMClientAsync(_factory);
        var (playerClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(_factory);
        var client = asDungeonMaster ? dmClient : playerClient;

        await AssertRefusedAsync(client, HttpMethod.Get, urlTemplate.Replace("{dm}", dm.Id.ToString()), "/quests", DmProfileToast);
    }

    [Fact]
    public async Task ShopManagementCreate_PostWithShopOn_AddsTheItem()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedDMClientAsync(_factory);
        var form = await client.GetAsync("/ShopManagement/Create", TestContext.Current.CancellationToken);
        var (token, _) = await AntiForgeryHelper.ExtractAntiForgeryTokenAsync(form);

        var response = await PostNewShopItemAsync(client, token);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.Should().StartWith("/ShopManagement");
        (await CountShopItemsAsync()).Should().Be(1);
    }

    [Fact]
    public async Task ShopManagementCreate_PostWithShopOff_RedirectsHomeAndWritesNothing()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedDMClientAsync(_factory);
        // The antiforgery token comes from the form while Shop is still on; the page is switched
        // off afterwards, so the refusal is the only thing standing between the POST and the write.
        var form = await client.GetAsync("/ShopManagement/Create", TestContext.Current.CancellationToken);
        var (token, _) = await AntiForgeryHelper.ExtractAntiForgeryTokenAsync(form);
        await BoardPageSeed.SetAsync(_factory, 1, BoardPageSet.Empty.With(BoardPage.Shop), BoardPage.Quests);

        var response = await PostNewShopItemAsync(client, token);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.Should().StartWith("/quests");
        (await CountShopItemsAsync()).Should().Be(0);
    }

    // Portraits render on quest pages whatever the profile page's own switch says, so they stay open.
    [Theory]
    [InlineData("GetDMProfilePicture")]
    [InlineData("GetOriginalDMProfilePicture")]
    public async Task DungeonMasterPictures_WhenProfilePageOff_AreStillServed(string action)
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (client, dm) = await AuthenticationHelper.CreateAuthenticatedDMClientAsync(_factory);
        using (var scope = _factory.Services.CreateScope())
        {
            var dmProfileService = scope.ServiceProvider.GetRequiredService<IDungeonMasterProfileService>();
            await dmProfileService.UpsertProfileAsync(
                dm.Id, bio: null, imageBytes: [1, 2, 3, 4], newCroppedImageData: [9, 9, 9, 9, 9],
                token: TestContext.Current.CancellationToken);
        }
        await BoardPageSeed.SetAsync(_factory, 1, BoardPageSet.Empty.With(BoardPage.DungeonMasterProfile), BoardPage.Quests);

        var response = await client.GetAsync($"/DungeonMaster/{action}/{dm.Id}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken)).Should().NotBeEmpty();
    }

    [Theory]
    [InlineData("/Characters")]
    [InlineData("/Characters/Create")]
    public async Task Characters_Off_RedirectsToQuestListWithToast(string url)
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        await BoardPageSeed.SetAsync(_factory, 1, BoardPageSet.Empty.With(BoardPage.Characters), BoardPage.Quests);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(_factory);

        await AssertRefusedAsync(client, HttpMethod.Get, url, "/quests", CharactersToast);
    }

    [Theory]
    [InlineData("/Characters")]
    [InlineData("/Contacts")]
    public async Task CharactersAndContacts_WithDefaultSettings_AreServed(string url)
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(_factory);

        var response = await client.GetAsync(url, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // Portraits render on quest pages whatever the Characters page's own switch says, so they stay open.
    [Theory]
    [InlineData("GetProfilePicture")]
    [InlineData("GetCroppedPicture")]
    public async Task CharacterPictures_WhenCharactersOff_AreStillServed(string action)
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (client, owner) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(_factory);
        var character = await TestDataHelper.CreateTestCharacterAsync(_factory.Services, owner.Id, "Gate portrait character");
        using (var scope = _factory.Services.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
            ctx.Set<CharacterImageEntity>().Add(new CharacterImageEntity
            {
                Id = character.Id,
                OriginalImageData = [1, 2, 3, 4],
                CroppedImageData = [9, 9, 9, 9, 9]
            });
            await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        await BoardPageSeed.SetAsync(_factory, 1, BoardPageSet.Empty.With(BoardPage.Characters), BoardPage.Quests);

        var response = await client.GetAsync($"/Characters/{action}/{character.Id}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken)).Should().NotBeEmpty();
    }

    [Theory]
    [InlineData("/Contacts", false)]
    [InlineData("/ContactCategoryManagement", true)]
    public async Task Contacts_Off_RedirectsToQuestListWithToast(string url, bool asDungeonMaster)
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        await BoardPageSeed.SetAsync(_factory, 1, BoardPageSet.Empty.With(BoardPage.Contacts), BoardPage.Quests);
        var (dmClient, _) = await AuthenticationHelper.CreateAuthenticatedDMClientAsync(_factory);
        var (playerClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(_factory);
        var client = asDungeonMaster ? dmClient : playerClient;

        await AssertRefusedAsync(client, HttpMethod.Get, url, "/quests", ContactsToast);
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

    [Fact]
    public async Task QuestList_Off_RedirectsToEffectiveHomeWithToast()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        // The stored home stays on the quest list, so the board's effective home is the calendar.
        await BoardPageSeed.SetAsync(_factory, 1, BoardPageSet.Empty.With(BoardPage.Quests), BoardPage.Quests);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(_factory);

        await AssertRefusedAsync(client, HttpMethod.Get, "/quests", "/Calendar", QuestListToast);
    }

    [Fact]
    public async Task QuestLog_WhenQuestListOff_RedirectsWhateverItsOwnSwitchSays()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        await BoardPageSeed.SetAsync(_factory, 1, BoardPageSet.Empty.With(BoardPage.Quests), BoardPage.Quests);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(_factory);

        await AssertRefusedAsync(client, HttpMethod.Get, "/QuestLog", "/Calendar", QuestLogToast);
    }

    [Fact]
    public async Task QuestLog_Off_RedirectsToQuestListWithToast()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        await BoardPageSeed.SetAsync(_factory, 1, BoardPageSet.Empty.With(BoardPage.QuestLog), BoardPage.Quests);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(_factory);

        await AssertRefusedAsync(client, HttpMethod.Get, "/QuestLog", "/quests", QuestLogToast);
    }

    [Fact]
    public async Task AdminQuests_WhenQuestListOff_RedirectsAdminToEffectiveHome()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        await BoardPageSeed.SetAsync(_factory, 1, BoardPageSet.Empty.With(BoardPage.Quests), BoardPage.Quests);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedAdminClientAsync(_factory);

        await AssertRefusedAsync(client, HttpMethod.Get, "/Admin/Quests", "/Calendar", QuestListToast);
    }

    [Fact]
    public async Task AdminDeleteQuest_WhenQuestListOff_IsRefusedAndTheQuestStays()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var dm = await AuthenticationHelper.CreateTestUserAsync(_factory.Services, "gatedeletedm", "gatedeletedm@example.com");
        var quest = await TestDataHelper.CreateTestQuestAsync(_factory.Services, dm.Id, "Gate delete quest");
        await BoardPageSeed.SetAsync(_factory, 1, BoardPageSet.Empty.With(BoardPage.Quests), BoardPage.Quests);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedAdminClientAsync(_factory);

        await AssertRefusedAsync(client, HttpMethod.Delete, $"/Admin/DeleteQuest/{quest.Id}", "/Calendar", QuestListToast);

        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
        (await ctx.Quests.AnyAsync(q => q.Id == quest.Id, TestContext.Current.CancellationToken)).Should().BeTrue();
    }

    [Theory]
    [InlineData("/Move/Quest/1", BoardPage.Quests, "/Calendar", QuestListToast)]
    [InlineData("/Move/Event/1", BoardPage.Events, "/quests", EventsToast)]
    public async Task Move_WhenItsPageIsOff_IsRefusedForSuperAdmin(string url, BoardPage disabled, string landing, string toast)
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        await BoardPageSeed.SetAsync(_factory, 1, BoardPageSet.Empty.With(disabled), BoardPage.Quests);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(_factory);

        await AssertRefusedAsync(client, HttpMethod.Get, url, landing, toast);
    }

    [Fact]
    public async Task UpdateSignupCharacter_WhenCharactersOff_RedirectsHomeAndKeepsTheCharacter()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var dm = await AuthenticationHelper.CreateTestUserAsync(_factory.Services, "gatecharsdm", "gatecharsdm@example.com");
        var quest = await TestDataHelper.CreateTestQuestAsync(_factory.Services, dm.Id, "Gate character quest");
        var (client, player) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, "gatecharsplayer", "gatecharsplayer@example.com");
        var first = await TestDataHelper.CreateTestCharacterAsync(_factory.Services, player.Id, "First Character");
        var second = await TestDataHelper.CreateTestCharacterAsync(_factory.Services, player.Id, "Second Character");
        await TestDataHelper.CreatePlayerSignupAsync(_factory.Services, quest.Id, player.Id, characterId: first.Id);
        await BoardPageSeed.SetAsync(_factory, 1, BoardPageSet.Empty.With(BoardPage.Characters), BoardPage.Quests);

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["questId"] = quest.Id.ToString(),
            ["characterId"] = second.Id.ToString()
        });
        var response = await client.PostAsync("/Quest/UpdateSignupCharacter", form, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.Should().StartWith("/quests");
        var landed = await client.GetAsync(response.Headers.Location, TestContext.Current.CancellationToken);
        var html = await landed.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        WebUtility.HtmlDecode(html).Should().Contain(CharactersToast);

        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
        var signup = await ctx.PlayerSignups.SingleAsync(
            s => s.QuestId == quest.Id && s.PlayerId == player.Id, TestContext.Current.CancellationToken);
        signup.CharacterId.Should().Be(first.Id);
    }

    [Fact]
    public async Task UpdateSignupCharacter_WithDefaultSettings_ChangesTheCharacter()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var dm = await AuthenticationHelper.CreateTestUserAsync(_factory.Services, "gatecharsdm2", "gatecharsdm2@example.com");
        var quest = await TestDataHelper.CreateTestQuestAsync(_factory.Services, dm.Id, "Gate character quest on");
        var (client, player) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, "gatecharsplayer2", "gatecharsplayer2@example.com");
        var first = await TestDataHelper.CreateTestCharacterAsync(_factory.Services, player.Id, "First Character");
        var second = await TestDataHelper.CreateTestCharacterAsync(_factory.Services, player.Id, "Second Character");
        await TestDataHelper.CreatePlayerSignupAsync(_factory.Services, quest.Id, player.Id, characterId: first.Id);

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["questId"] = quest.Id.ToString(),
            ["characterId"] = second.Id.ToString()
        });
        var response = await client.PostAsync("/Quest/UpdateSignupCharacter", form, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
        var signup = await ctx.PlayerSignups.SingleAsync(
            s => s.QuestId == quest.Id && s.PlayerId == player.Id, TestContext.Current.CancellationToken);
        signup.CharacterId.Should().Be(second.Id);
    }

    [Theory]
    [InlineData("/quests")]
    [InlineData("/QuestLog")]
    public async Task QuestPages_WithDefaultSettings_AreServed(string url)
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(_factory);

        var response = await client.GetAsync(url, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
