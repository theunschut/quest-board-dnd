using System.Net;
using System.Reflection;
using QuestBoard.Domain.Enums;
using QuestBoard.Service.Helpers;

namespace QuestBoard.IntegrationTests.Helpers;

/// <summary>
/// One route of the cross-board registry as the agreement matrix sees it: which family of records it
/// opens, the record states worth trying, and the one state in which an Admin of the board plainly
/// can open it. Every status list, role check and date rule stays out of here on purpose: the page
/// itself is asked, and the matrix only compares the switch's answer with the page's.
/// </summary>
public sealed record MatrixRoute(string Key, string Family, string[] States, string PresentState);

/// <summary>A single matrix row: a route, the viewer's role on the target board, and the state of the record.</summary>
public sealed record MatrixRow(string Route, string Role, string State);

/// <summary>
/// Everything one matrix row needs after seeding: the viewer with board A active (the deep link), the
/// same viewer with the target board active (the oracle), and a second board-A session used as the
/// control for a nonexistent id. Board A is the default board the viewer is always a Player of.
/// </summary>
public sealed record MatrixScenario(
    HttpClient DeepLinkClient,
    HttpClient OracleClient,
    HttpClient ControlClient,
    string Url,
    string MissingUrl,
    int RecordId,
    string TargetBoardName,
    BoardPage Page);

/// <summary>
/// Seeds one agreement-matrix row on fresh boards with unique names, so rows never share state, and
/// classifies a response of the real page as served or refused.
/// </summary>
public static class CrossBoardMatrixSeeder
{
    public const string ActiveBoardName = "EuphoriaInn";

    public const string PageOffState = "PageOff";

    public static readonly string[] Roles = ["Player", "DungeonMaster", "Admin", "SuperAdmin"];

    private static readonly string[] ShopStates = ["Draft", "Denied", "Scheduled", "Published", "Archived"];

    /// <summary>Every route the matrix covers. A registry route missing from this list fails the coverage fact.</summary>
    public static readonly MatrixRoute[] Routes =
    [
        new("/Shop/Details", "ShopItem", ShopStates, "Published"),
        new("/ShopManagement/Edit", "ShopItem", ShopStates, "Published"),
    ];

    /// <summary>Every row of the matrix: each route with each role and each of its states, plus the page-off board.</summary>
    public static IEnumerable<MatrixRow> Rows()
    {
        foreach (var route in Routes)
        {
            foreach (var role in Roles)
            {
                foreach (var state in route.States)
                {
                    yield return new MatrixRow(route.Key, role, state);
                }
            }
        }
    }

    public static MatrixRoute RouteOf(string key) => Routes.Single(r => r.Key == key);

    public static HttpRequestMessage NavigationGet(string url)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("Sec-Fetch-Dest", "document");
        request.Headers.Add("Sec-Fetch-Mode", "navigate");
        request.Headers.Add("Accept", "text/html");
        return request;
    }

    public static async Task<HttpResponseMessage> GetAsync(HttpClient client, string url) =>
        await client.SendAsync(NavigationGet(url), TestContext.Current.CancellationToken);

    /// <summary>The routes the registry holds, read as it is so a route added later shows up here without an edit.</summary>
    public static IReadOnlyList<string> RegisteredRoutes()
    {
        var registry = RegistryType();
        var property = registry.GetProperty("RegisteredRoutes", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("The registry no longer exposes its registered routes.");
        return ((IReadOnlyCollection<string>)property.GetValue(null)!).ToList();
    }

    /// <summary>The board page the registry says a route belongs to.</summary>
    public static BoardPage PageOf(string routeKey)
    {
        var segments = routeKey.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var method = RegistryType().GetMethod("TryGetRouteEntry", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("The registry no longer exposes its route lookup.");
        var arguments = new object?[] { null, segments[0], segments[1], null };
        var found = (bool)method.Invoke(null, arguments)!;
        found.Should().BeTrue($"{routeKey} must be in the registry");
        return (BoardPage)arguments[3]!.GetType().GetProperty("Page")!.GetValue(arguments[3])!;
    }

    private static Type RegistryType() =>
        typeof(Program).Assembly.GetType("QuestBoard.Service.Helpers.CrossBoardLinkRegistry", throwOnError: true)!;

    /// <summary>
    /// Whether the page served the request: a success, or a redirect that is neither the access-denied
    /// page nor the page gate turning the visitor away (recognised by the notice the gate queues, which
    /// the next page then shows). Anything else is a refusal.
    /// </summary>
    public static async Task<bool> IsServedAsync(HttpClient client, HttpResponseMessage response, BoardPage page)
    {
        var code = (int)response.StatusCode;
        if (code is >= 200 and < 300)
        {
            return true;
        }

        if (code is not (>= 300 and < 400))
        {
            return false;
        }

        var location = response.Headers.Location;
        if (location is null)
        {
            return false;
        }

        var target = location.IsAbsoluteUri ? location.PathAndQuery : location.OriginalString;
        if (target.StartsWith("/Account/AccessDenied", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        using var followed = await GetAsync(client, target);
        var body = WebUtility.HtmlDecode(await followed.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        return !body.Contains(BoardPageRoutes.NotUsedMessage(page), StringComparison.Ordinal);
    }

    /// <summary>
    /// Seeds boards A (the default board) and B, the viewer's memberships, and the record on B in the row's
    /// state, and returns the three sessions of the same person. With the page-off state the record is in its
    /// plainly openable state and board B does not offer the route's page.
    /// </summary>
    public static async Task<MatrixScenario> SeedAsync(CrossBoardWebApplicationFactory factory, MatrixRow row)
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);

        var route = RouteOf(row.Route);
        var page = PageOf(route.Key);
        var pageOff = row.State == PageOffState;
        var state = pageOff ? route.PresentState : row.State;
        var suffix = Guid.NewGuid().ToString("N")[..8];

        var other = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, $"matrixother{suffix}", $"matrixother{suffix}@example.com", name: $"MatrixOther{suffix}");

        var (client, viewer) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, $"matrixviewer{suffix}", $"matrixviewer{suffix}@example.com",
            roles: [row.Role == "SuperAdmin" ? "SuperAdmin" : "Player"]);

        var targetName = $"MatrixBoardB{suffix}";
        int targetBoardId;
        await using (var ctx = factory.Database.CreateContext())
        {
            var target = new GroupEntity
            {
                Name = targetName,
                CreatedAt = DateTime.UtcNow,
                DisabledPages = pageOff ? BoardPageSet.Empty.With(page).Mask : 0
            };
            ctx.Groups.Add(target);
            await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
            targetBoardId = target.Id;

            // A SuperAdmin is a plain Player member of the board: the role claim is what is being tried.
            var roleOnTarget = row.Role switch
            {
                "DungeonMaster" => GroupRole.DungeonMaster,
                "Admin" => GroupRole.Admin,
                _ => GroupRole.Player
            };
            ctx.UserGroups.Add(new UserGroupEntity { UserId = viewer.Id, GroupId = targetBoardId, GroupRole = (int)roleOnTarget });
            await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var recordId = await SeedRecordAsync(factory, route, state, suffix, targetBoardId, viewer.Id, other.Id);

        var oracle = factory.CreateCookieClient();
        oracle.DefaultRequestHeaders.Authorization = client.DefaultRequestHeaders.Authorization;
        await SelectBoardAsync(oracle, targetBoardId);
        if (route.Family == "Contact")
        {
            // Whether a Dungeon Master sees an unrevealed contact is the page's own show-hidden switch, which the
            // lookup never reads; the oracle turns it on (it has no effect for anyone the page does not offer it to).
            await oracle.PostAsync("/Contacts/ToggleShowHidden", new FormUrlEncodedContent([]), TestContext.Current.CancellationToken);
        }

        var control = factory.CreateCookieClient();
        control.DefaultRequestHeaders.Authorization = client.DefaultRequestHeaders.Authorization;
        await SelectBoardAsync(control, 1);
        await SelectBoardAsync(client, 1);

        return new MatrixScenario(
            client, oracle, control, $"{route.Key}/{recordId}", $"{route.Key}/{int.MaxValue}", recordId, targetName, page);
    }

    public static Task<HttpResponseMessage> SelectBoardAsync(HttpClient client, int boardId) =>
        client.PostAsync(
            "/GroupPicker/SelectGroup",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["groupId"] = boardId.ToString() }),
            TestContext.Current.CancellationToken);

    private static async Task<int> SeedRecordAsync(
        CrossBoardWebApplicationFactory factory, MatrixRoute route, string state, string suffix, int boardId, int viewerId, int otherId)
    {
        switch (route.Family)
        {
            case "ShopItem":
                return await SeedShopItemAsync(factory, state, suffix, boardId, otherId);
            default:
                throw new InvalidOperationException($"No seeding for the {route.Family} family.");
        }
    }

    private static async Task<int> SeedShopItemAsync(CrossBoardWebApplicationFactory factory, string state, string suffix, int boardId, int createdBy)
    {
        var status = state switch
        {
            "Draft" => ItemStatus.Draft,
            "Denied" => ItemStatus.Denied,
            "Archived" => ItemStatus.Archived,
            _ => ItemStatus.Published
        };

        await using var ctx = factory.Database.CreateContext();
        var item = new ShopItemEntity
        {
            Name = $"MatrixShopItem{suffix}",
            Description = "A shop item seeded for the agreement matrix.",
            Type = 0,
            Rarity = 0,
            Price = 10m,
            Quantity = 1,
            Status = (int)status,
            AvailableFrom = state == "Scheduled" ? DateTime.UtcNow.AddDays(30) : null,
            CreatedByDmId = createdBy,
            GroupId = boardId,
            CreatedAt = DateTime.UtcNow
        };
        ctx.ShopItems.Add(item);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        return item.Id;
    }
}
