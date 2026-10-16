using System.Net;
using System.Text.RegularExpressions;
using QuestBoard.Domain.Enums;
using QuestBoard.IntegrationTests.Helpers;

namespace QuestBoard.IntegrationTests.Controllers;

/// <summary>
/// Integration tests for the /platform/LinkedBoards page: the super admin creates sets and
/// manages their boards, and the server holds the same-type and one-set-per-board rules.
/// </summary>
public class LinkedBoardsPlatformIntegrationTests : IClassFixture<WebApplicationFactoryBase>
{
    private readonly WebApplicationFactoryBase _factory;

    public LinkedBoardsPlatformIntegrationTests(WebApplicationFactoryBase factory)
    {
        _factory = factory;
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static FormUrlEncodedContent Form(params (string Key, string Value)[] fields)
        => new(fields.ToDictionary(f => f.Key, f => f.Value));

    private async Task SeedBoardAsync(int id, string name, BoardType boardType = BoardType.OneShot, int? linkedBoardSetId = null)
    {
        await using var context = _factory.Database.CreateContext();
        context.Groups.Add(new GroupEntity
        {
            Id = id,
            Name = name,
            BoardType = (int)boardType,
            CreatedAt = DateTime.UtcNow,
            LinkedBoardSetId = linkedBoardSetId
        });
        await context.SaveChangesAsync(Token);
    }

    private async Task<int> SeedSetAsync(string name, BoardType boardType = BoardType.OneShot)
    {
        await using var context = _factory.Database.CreateContext();
        var set = new LinkedBoardSetEntity { Name = name, BoardType = (int)boardType, CreatedAt = DateTime.UtcNow };
        context.LinkedBoardSets.Add(set);
        await context.SaveChangesAsync(Token);
        return set.Id;
    }

    private async Task<int?> GetBoardSetIdAsync(int groupId)
    {
        await using var context = _factory.Database.CreateContext();
        return (await context.Groups.SingleAsync(g => g.Id == groupId, Token)).LinkedBoardSetId;
    }

    [Fact]
    public async Task Index_WhenSuperAdmin_ShouldReturn200WithLinkedBoardsHeading()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(_factory);

        var response = await client.GetAsync("/platform/LinkedBoards", Token);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(Token)).Should().Contain("Linked Boards");
    }

    [Fact]
    public async Task CreateSetAndAddTwoBoards_StoresTheLinkAndListsBothBoardsAsActive()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(_factory);
        await SeedBoardAsync(11, "Friday Table");
        await SeedBoardAsync(12, "Saturday Table");

        var create = await client.PostAsync("/platform/LinkedBoards/Create",
            Form(("Name", "Weekend Set"), ("BoardType", ((int)BoardType.OneShot).ToString())), Token);
        create.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found);

        int setId;
        await using (var context = _factory.Database.CreateContext())
        {
            var set = await context.LinkedBoardSets.SingleAsync(s => s.Name == "Weekend Set", Token);
            setId = set.Id;
        }

        foreach (var groupId in new[] { 11, 12 })
        {
            var add = await client.PostAsync($"/platform/LinkedBoards/AddBoard/{setId}",
                Form(("groupId", groupId.ToString())), Token);
            add.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found);
        }

        (await GetBoardSetIdAsync(11)).Should().Be(setId);
        (await GetBoardSetIdAsync(12)).Should().Be(setId);

        var index = await client.GetAsync("/platform/LinkedBoards", Token);
        var html = await index.Content.ReadAsStringAsync(Token);
        html.Should().Contain("Weekend Set");
        html.Should().Contain("Friday Table");
        html.Should().Contain("Saturday Table");
        html.Should().Contain("badge bg-success\">Active");
    }

    private async Task<string> GetIndexHtmlAsync(HttpClient client)
    {
        var response = await client.GetAsync("/platform/LinkedBoards", Token);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadAsStringAsync(Token);
    }

    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string url, params (string Key, string Value)[] fields)
        => await client.PostAsync(url, Form(fields), Token);

    // ---------------------------------------------------------------------------
    // Access: only a SuperAdmin reaches the page
    // ---------------------------------------------------------------------------

    [Theory]
    [InlineData("Admin")]
    [InlineData("DungeonMaster")]
    [InlineData("Player")]
    public async Task Page_WhenNotSuperAdmin_DeniesGetAndPostAndLeavesTheBoardUnchanged(string role)
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, "deniedrole", "denied@test.com", roles: [role]);
        await SeedBoardAsync(20, "Guarded Table");
        var setId = await SeedSetAsync("Guarded Set");

        var get = await client.GetAsync("/platform/LinkedBoards", Token);
        var add = await PostAsync(client, $"/platform/LinkedBoards/AddBoard/{setId}", ("groupId", "20"));
        var create = await PostAsync(client, "/platform/LinkedBoards/Create",
            ("Name", "Sneaky"), ("BoardType", ((int)BoardType.OneShot).ToString()));
        var delete = await PostAsync(client, $"/platform/LinkedBoards/Delete/{setId}");

        foreach (var response in new[] { get, add, create, delete })
        {
            response.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.Redirect, HttpStatusCode.Found);
        }

        (await GetBoardSetIdAsync(20)).Should().BeNull();
        await using var context = _factory.Database.CreateContext();
        (await context.LinkedBoardSets.CountAsync(Token)).Should().Be(1);
    }

    // ---------------------------------------------------------------------------
    // Refusals hold when the form is posted directly
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task AddBoard_WhenCampaignBoardPostedIntoOneShotSet_IsRefusedWithTheTypeMismatchToast()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(_factory);
        await SeedBoardAsync(21, "Long Campaign", BoardType.Campaign);
        var setId = await SeedSetAsync("Friday Set", BoardType.OneShot);

        var response = await PostAsync(client, $"/platform/LinkedBoards/AddBoard/{setId}", ("groupId", "21"));

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found);
        (await GetBoardSetIdAsync(21)).Should().BeNull();
        var html = await GetIndexHtmlAsync(client);
        html.Should().Contain("is a Campaign board, but this set links One-Shot boards. Only boards of the same type can be linked.");
    }

    [Fact]
    public async Task AddBoard_WhenOneShotBoardPostedIntoEmptyCampaignSet_IsRefused()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(_factory);
        await SeedBoardAsync(22, "Quick Table", BoardType.OneShot);
        var setId = await SeedSetAsync("Campaign Set", BoardType.Campaign);

        await PostAsync(client, $"/platform/LinkedBoards/AddBoard/{setId}", ("groupId", "22"));

        (await GetBoardSetIdAsync(22)).Should().BeNull();
    }

    [Fact]
    public async Task AddBoard_WhenBoardIsAlreadyInAnotherSet_IsRefusedAndKeepsItsOriginalSet()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(_factory);
        var originalSetId = await SeedSetAsync("Original Set");
        var otherSetId = await SeedSetAsync("Other Set");
        await SeedBoardAsync(23, "Already Linked Table", linkedBoardSetId: originalSetId);

        await PostAsync(client, $"/platform/LinkedBoards/AddBoard/{otherSetId}", ("groupId", "23"));

        (await GetBoardSetIdAsync(23)).Should().Be(originalSetId);
        var html = await GetIndexHtmlAsync(client);
        html.Should().Contain("is already in a linked set. A board can be in only one set.");
    }

    [Fact]
    public async Task AddBoard_WhenBoardIsAlreadyInTheSameSet_IsRefused()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(_factory);
        var setId = await SeedSetAsync("Only Set");
        await SeedBoardAsync(24, "Linked Table", linkedBoardSetId: setId);

        await PostAsync(client, $"/platform/LinkedBoards/AddBoard/{setId}", ("groupId", "24"));

        var html = await GetIndexHtmlAsync(client);
        html.Should().Contain("is already in a linked set.");
    }

    [Fact]
    public async Task AddBoard_WhenSetOrBoardIsGone_ShowsTheNoLongerExistsToast()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(_factory);

        await PostAsync(client, "/platform/LinkedBoards/AddBoard/9999", ("groupId", "9998"));

        var html = await GetIndexHtmlAsync(client);
        html.Should().Contain("That set or board no longer exists.");
    }

    [Fact]
    public async Task Create_WhenNameIsMissing_ShowsTheNameToastAndCreatesNothing()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(_factory);

        await PostAsync(client, "/platform/LinkedBoards/Create",
            ("Name", "   "), ("BoardType", ((int)BoardType.OneShot).ToString()));

        var html = await GetIndexHtmlAsync(client);
        html.Should().Contain("Enter a set name of up to 100 characters.");
        await using var context = _factory.Database.CreateContext();
        (await context.LinkedBoardSets.CountAsync(Token)).Should().Be(0);
    }

    [Fact]
    public async Task Create_WhenNameIsOver100Characters_ShowsTheNameToastAndCreatesNothing()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(_factory);

        await PostAsync(client, "/platform/LinkedBoards/Create",
            ("Name", new string('n', 101)), ("BoardType", ((int)BoardType.OneShot).ToString()));

        var html = await GetIndexHtmlAsync(client);
        html.Should().Contain("Enter a set name of up to 100 characters.");
        await using var context = _factory.Database.CreateContext();
        (await context.LinkedBoardSets.CountAsync(Token)).Should().Be(0);
    }

    [Fact]
    public async Task Create_WhenBoardTypeIsMissing_ShowsTheBoardTypeToast()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(_factory);

        await PostAsync(client, "/platform/LinkedBoards/Create", ("Name", "Friday Set"));

        var html = await GetIndexHtmlAsync(client);
        html.Should().Contain("Choose the board type for the set.");
        await using var context = _factory.Database.CreateContext();
        (await context.LinkedBoardSets.CountAsync(Token)).Should().Be(0);
    }

    [Fact]
    public async Task Create_WhenNameMatchesAnExistingSetIgnoringCase_IsRefusedWithTheDuplicateToast()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(_factory);
        await SeedSetAsync("Friday Set");

        await PostAsync(client, "/platform/LinkedBoards/Create",
            ("Name", "FRIDAY set"), ("BoardType", ((int)BoardType.OneShot).ToString()));

        var html = await GetIndexHtmlAsync(client);
        html.Should().Contain("already exists. Choose a different name.");
        await using var context = _factory.Database.CreateContext();
        (await context.LinkedBoardSets.CountAsync(Token)).Should().Be(1);
    }

    // ---------------------------------------------------------------------------
    // Rename, remove, delete
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task Rename_ChangesTheSetNameAndKeepsItsBoards()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(_factory);
        var setId = await SeedSetAsync("Old Name");
        await SeedBoardAsync(30, "Member Table", linkedBoardSetId: setId);

        var response = await PostAsync(client, $"/platform/LinkedBoards/Rename/{setId}", ("name", "  New Name "));

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found);
        var html = await GetIndexHtmlAsync(client);
        html.Should().Contain("New Name");
        html.Should().NotContain("Old Name");
        (await GetBoardSetIdAsync(30)).Should().Be(setId);
    }

    [Fact]
    public async Task Rename_WhenNameBelongsToAnotherSet_IsRefused()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(_factory);
        var setId = await SeedSetAsync("First Set");
        await SeedSetAsync("Second Set");

        await PostAsync(client, $"/platform/LinkedBoards/Rename/{setId}", ("name", "second set"));

        var html = await GetIndexHtmlAsync(client);
        html.Should().Contain("already exists. Choose a different name.");
        await using var context = _factory.Database.CreateContext();
        (await context.LinkedBoardSets.SingleAsync(s => s.Id == setId, Token)).Name.Should().Be("First Set");
    }

    [Fact]
    public async Task RemoveBoard_LeavesAOneBoardSetListedAsInactiveWithTheNote()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(_factory);
        var setId = await SeedSetAsync("Pair Set");
        await SeedBoardAsync(31, "Stays Table", linkedBoardSetId: setId);
        await SeedBoardAsync(32, "Leaves Table", linkedBoardSetId: setId);

        var response = await PostAsync(client, $"/platform/LinkedBoards/RemoveBoard/{setId}", ("groupId", "32"));

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found);
        (await GetBoardSetIdAsync(32)).Should().BeNull();
        (await GetBoardSetIdAsync(31)).Should().Be(setId);
        var html = await GetIndexHtmlAsync(client);
        html.Should().Contain("Pair Set");
        html.Should().Contain("badge bg-secondary\">Inactive");
        html.Should().Contain("Inactive: add at least one more board to share calendars.");
    }

    [Fact]
    public async Task RemoveBoard_WhenBoardIsInADifferentSet_IsRefusedAndWritesNothing()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(_factory);
        var setId = await SeedSetAsync("Set A");
        var otherSetId = await SeedSetAsync("Set B");
        await SeedBoardAsync(33, "B Table", linkedBoardSetId: otherSetId);

        await PostAsync(client, $"/platform/LinkedBoards/RemoveBoard/{setId}", ("groupId", "33"));

        (await GetBoardSetIdAsync(33)).Should().Be(otherSetId);
        var html = await GetIndexHtmlAsync(client);
        html.Should().Contain("That set or board no longer exists.");
    }

    [Fact]
    public async Task Index_ForAnEmptySet_ShowsTheEmptySetNote()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(_factory);
        await SeedSetAsync("Empty Set");

        var html = await GetIndexHtmlAsync(client);

        html.Should().Contain("No boards in this set yet.");
        html.Should().Contain("badge bg-secondary\">Inactive");
    }

    [Fact]
    public async Task DeleteSet_UnlinksEveryBoardAndRemovesTheSetWithoutDeletingBoards()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(_factory);
        var setId = await SeedSetAsync("Doomed Set");
        await SeedBoardAsync(34, "First Table", linkedBoardSetId: setId);
        await SeedBoardAsync(35, "Second Table", linkedBoardSetId: setId);

        var response = await PostAsync(client, $"/platform/LinkedBoards/Delete/{setId}");

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found);
        (await GetBoardSetIdAsync(34)).Should().BeNull();
        (await GetBoardSetIdAsync(35)).Should().BeNull();
        await using (var context = _factory.Database.CreateContext())
        {
            (await context.LinkedBoardSets.AnyAsync(s => s.Id == setId, Token)).Should().BeFalse();
        }
        var html = await GetIndexHtmlAsync(client);
        html.Should().Contain("Its boards keep their own calendars.");
        html.Should().Contain("No linked sets yet.");
    }

    // ---------------------------------------------------------------------------
    // Board delete and rename keep sets consistent
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task DeletingALinkedBoard_LeavesTheSetAndItsOtherBoardsIntact()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(_factory);
        var setId = await SeedSetAsync("Survivor Set");
        await SeedBoardAsync(36, "Doomed Table", linkedBoardSetId: setId);
        await SeedBoardAsync(37, "Surviving Table", linkedBoardSetId: setId);

        var response = await PostAsync(client, "/platform/Group/Delete/36");

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found);
        await using (var context = _factory.Database.CreateContext())
        {
            (await context.Groups.AnyAsync(g => g.Id == 36, Token)).Should().BeFalse();
            (await context.LinkedBoardSets.AnyAsync(s => s.Id == setId, Token)).Should().BeTrue();
        }
        (await GetBoardSetIdAsync(37)).Should().Be(setId);
        var html = await GetIndexHtmlAsync(client);
        html.Should().Contain("Survivor Set");
        html.Should().Contain("Surviving Table");
        html.Should().NotContain("Doomed Table");
    }

    [Fact]
    public async Task RenamingALinkedBoard_KeepsItInItsSet()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(_factory);
        var setId = await SeedSetAsync("Rename Set");
        await SeedBoardAsync(38, "Before Name", linkedBoardSetId: setId);

        var response = await PostAsync(client, "/platform/Group/Edit", ("Id", "38"), ("Name", "After Name"));

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found);
        await using (var context = _factory.Database.CreateContext())
        {
            (await context.Groups.SingleAsync(g => g.Id == 38, Token)).Name.Should().Be("After Name");
        }
        (await GetBoardSetIdAsync(38)).Should().Be(setId);
        var html = await GetIndexHtmlAsync(client);
        html.Should().Contain("After Name");
    }

    // ---------------------------------------------------------------------------
    // View behaviour
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task AddBoardPicker_ListsOnlySameTypeBoardsThatAreInNoSet()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(_factory);
        var setId = await SeedSetAsync("Picker Set", BoardType.OneShot);
        var otherSetId = await SeedSetAsync("Taken Set", BoardType.OneShot);
        await SeedBoardAsync(40, "Free OneShot Board");
        await SeedBoardAsync(41, "Free Campaign Board", BoardType.Campaign);
        await SeedBoardAsync(42, "Taken OneShot Board", linkedBoardSetId: otherSetId);

        var html = await GetIndexHtmlAsync(client);

        var select = Regex.Match(html, $"<select id=\"add-board-{setId}\".*?</select>", RegexOptions.Singleline);
        select.Success.Should().BeTrue();
        select.Value.Should().Contain("Free OneShot Board");
        select.Value.Should().NotContain("Free Campaign Board");
        select.Value.Should().NotContain("Taken OneShot Board");
    }

    [Fact]
    public async Task AddBoardPicker_WhenNoBoardIsAvailable_DisablesTheControlsAndSaysSo()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(_factory);
        var setId = await SeedSetAsync("Campaign Set", BoardType.Campaign);

        var html = await GetIndexHtmlAsync(client);

        html.Should().Contain("No unlinked Campaign boards are available.");
        var select = Regex.Match(html, $"<select id=\"add-board-{setId}\"[^>]*>");
        select.Value.Should().Contain("disabled");
    }

    [Fact]
    public async Task Index_WhenNoSetsExist_ShowsTheEmptyState()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(_factory);

        var html = await GetIndexHtmlAsync(client);

        html.Should().Contain("No linked sets yet.");
        html.Should().Contain("Create a set above, then add boards of the same type to share their calendars.");
    }

    [Fact]
    public async Task Index_ListsSetsInNameOrder()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(_factory);
        await SeedSetAsync("Zulu Set");
        await SeedSetAsync("Alpha Set");

        var html = await GetIndexHtmlAsync(client);

        html.IndexOf("Alpha Set", StringComparison.Ordinal).Should().BeLessThan(html.IndexOf("Zulu Set", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ConfirmPrompts_EscapeQuotesAndApostrophesInNamesForJavaScript()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(_factory);
        const string trickyName = "O'Brien \"Q\"";
        var setId = await SeedSetAsync(trickyName);
        await SeedBoardAsync(50, trickyName, linkedBoardSetId: setId);

        var html = await GetIndexHtmlAsync(client);

        html.Should().NotContain("O'Brien", "every name is HTML-encoded wherever it is written");
        var handlers = Regex.Matches(html, "onsubmit=\"([^\"]*)\"")
            .Select(m => WebUtility.HtmlDecode(m.Groups[1].Value))
            .Where(h => h.Contains("confirm("))
            .ToList();
        handlers.Should().HaveCount(2);
        foreach (var handler in handlers)
        {
            handler.Should().StartWith("return confirm(\"").And.EndWith("\");");
            handler.Should().Contain("O\\u0027Brien");
            handler.Should().Contain("\\u0022Q\\u0022");
            handler.Should().NotContain("O'Brien");
        }
    }

    // ---------------------------------------------------------------------------
    // Mobile twin and the Group index entry points
    // ---------------------------------------------------------------------------

    private const string MobileUserAgent =
        "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1";

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method, string url, string? userAgent, HttpContent? content = null)
    {
        var request = new HttpRequestMessage(method, url) { Content = content };
        if (userAgent is not null)
        {
            request.Headers.TryAddWithoutValidation("User-Agent", userAgent);
        }
        request.Headers.Authorization = client.DefaultRequestHeaders.Authorization;
        return await client.SendAsync(request, Token);
    }

    private static async Task<string> GetMobileIndexHtmlAsync(HttpClient client)
    {
        var response = await SendAsync(client, HttpMethod.Get, "/platform/LinkedBoards", MobileUserAgent);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadAsStringAsync(Token);
    }

    private static async Task<HttpResponseMessage> MobilePostAsync(HttpClient client, string url, params (string Key, string Value)[] fields)
        => await SendAsync(client, HttpMethod.Post, url, MobileUserAgent, Form(fields));

    [Theory]
    [InlineData(null)]
    [InlineData(MobileUserAgent)]
    public async Task GroupIndex_ForSuperAdmin_LinksToTheLinkedBoardsPage(string? userAgent)
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(_factory);

        var response = await SendAsync(client, HttpMethod.Get, "/platform/Group/Index", userAgent);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync(Token);
        html.Should().Contain("/platform/LinkedBoards");
        html.Should().Contain("Linked Boards");
        if (userAgent is not null)
        {
            html.Should().Contain("platform-group-card-mobile", "the mobile layout must be the one rendered");
        }
    }

    [Fact]
    public async Task MobileIndex_RendersTheMobileLayoutWithItsStylesheetAndCreateForm()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(_factory);

        var html = await GetMobileIndexHtmlAsync(client);

        html.Should().Contain("platform-group-card-mobile");
        html.Should().Contain("platform-linked-boards.mobile.css");
        html.Should().Contain("Create Set");
    }

    [Fact]
    public async Task MobileCreateAndAddBoard_ListsTheBoardInsideABoardRow()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(_factory);
        await SeedBoardAsync(60, "Phone Table");

        var create = await MobilePostAsync(client, "/platform/LinkedBoards/Create",
            ("Name", "Phone Set"), ("BoardType", ((int)BoardType.OneShot).ToString()));
        create.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found);
        int setId;
        await using (var context = _factory.Database.CreateContext())
        {
            setId = (await context.LinkedBoardSets.SingleAsync(s => s.Name == "Phone Set", Token)).Id;
        }

        var add = await MobilePostAsync(client, $"/platform/LinkedBoards/AddBoard/{setId}", ("groupId", "60"));
        add.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found);

        (await GetBoardSetIdAsync(60)).Should().Be(setId);
        var html = await GetMobileIndexHtmlAsync(client);
        html.Should().Contain("Phone Set");
        Regex.IsMatch(html, "linked-set-board-row\".*?Phone Table", RegexOptions.Singleline).Should().BeTrue();
    }

    [Fact]
    public async Task MobileIndex_WhenNoSetsExist_ShowsTheEmptyState()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(_factory);

        var html = await GetMobileIndexHtmlAsync(client);

        html.Should().Contain("No linked sets yet.");
        html.Should().Contain("Create a set above, then add boards of the same type to share their calendars.");
    }

    [Fact]
    public async Task MobileIndex_ForAOneBoardSet_ShowsInactiveAndTheNote()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(_factory);
        var setId = await SeedSetAsync("Lonely Set");
        await SeedBoardAsync(61, "Lonely Table", linkedBoardSetId: setId);

        var html = await GetMobileIndexHtmlAsync(client);

        html.Should().Contain("badge bg-secondary\">Inactive");
        html.Should().Contain("Inactive: add at least one more board to share calendars.");
    }

    [Fact]
    public async Task MobileIndex_ForAnEmptySet_ShowsTheEmptySetNoteAndInactive()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(_factory);
        await SeedSetAsync("Hollow Set");

        var html = await GetMobileIndexHtmlAsync(client);

        html.Should().Contain("No boards in this set yet.");
        html.Should().Contain("badge bg-secondary\">Inactive");
    }

    [Fact]
    public async Task MobileAddBoardPicker_WhenOnlyOtherTypeBoardsAreUnlinked_DisablesTheControlsAndSaysSo()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(_factory);
        var setId = await SeedSetAsync("Campaign Set", BoardType.Campaign);
        await SeedBoardAsync(62, "Only One-Shot Board", BoardType.OneShot);

        var html = await GetMobileIndexHtmlAsync(client);

        html.Should().Contain("No unlinked Campaign boards are available.");
        var select = Regex.Match(html, $"<select id=\"add-board-{setId}\"[^>]*>");
        select.Value.Should().Contain("disabled");
        Regex.Match(html, $"<select id=\"add-board-{setId}\".*?</select>", RegexOptions.Singleline)
            .Value.Should().NotContain("Only One-Shot Board");
    }

    [Fact]
    public async Task MobileIndex_ForATwoBoardSet_RendersARowAndRemoveFormPerBoard()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(_factory);
        var setId = await SeedSetAsync("Pair Set");
        await SeedBoardAsync(63, "First Phone Table", linkedBoardSetId: setId);
        await SeedBoardAsync(64, "Second Phone Table", linkedBoardSetId: setId);

        var html = await GetMobileIndexHtmlAsync(client);

        Regex.Matches(html, "class=\"linked-set-board-row\"").Should().HaveCount(2);
        Regex.Matches(html, $"action=\"/platform/LinkedBoards/RemoveBoard/{setId}\"").Should().HaveCount(2);
        html.Should().Contain("badge bg-success\">Active");
    }

    [Fact]
    public async Task MobileConfirmPrompts_EscapeQuotesAndApostrophesInNamesForJavaScript()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(_factory);
        const string trickyName = "O'Brien \"Q\"";
        var setId = await SeedSetAsync(trickyName);
        await SeedBoardAsync(65, trickyName, linkedBoardSetId: setId);

        var html = await GetMobileIndexHtmlAsync(client);

        html.Should().NotContain("O'Brien", "every name is HTML-encoded wherever it is written");
        var handlers = Regex.Matches(html, "onsubmit=\"([^\"]*)\"")
            .Select(m => WebUtility.HtmlDecode(m.Groups[1].Value))
            .Where(h => h.Contains("confirm("))
            .ToList();
        handlers.Should().HaveCount(2);
        foreach (var handler in handlers)
        {
            handler.Should().StartWith("return confirm(\"").And.EndWith("\");");
            handler.Should().Contain("O\\u0027Brien");
            handler.Should().Contain("\\u0022Q\\u0022");
            handler.Should().NotContain("O'Brien");
        }
    }

    [Fact]
    public async Task MobileRemoveBoard_LeavesTheSetShownAsInactive()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(_factory);
        var setId = await SeedSetAsync("Shrinking Set");
        await SeedBoardAsync(66, "Stays Phone Table", linkedBoardSetId: setId);
        await SeedBoardAsync(67, "Leaves Phone Table", linkedBoardSetId: setId);

        var response = await MobilePostAsync(client, $"/platform/LinkedBoards/RemoveBoard/{setId}", ("groupId", "67"));

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found);
        (await GetBoardSetIdAsync(67)).Should().BeNull();
        var html = await GetMobileIndexHtmlAsync(client);
        html.Should().Contain("badge bg-secondary\">Inactive");
        html.Should().Contain("Inactive: add at least one more board to share calendars.");
    }
}
