using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using FluentAssertions.Execution;
using QuestBoard.Domain.Enums;
using QuestBoard.IntegrationTests.Helpers;

namespace QuestBoard.IntegrationTests.Tests;

/// <summary>
/// Proves that a shop item's availability window means the same thing on the shop list, the details page and dialog,
/// a purchase and a sale to the shop, judged on the board's own clock rather than on UTC.
/// </summary>
/// <remarks>
/// The whole class is pinned to 2026-09-05T22:30Z with the board in Europe/Amsterdam (+02:00 in September), so the
/// board reads 00:30 on the 6th while UTC is still on the 5th. A window that opens at midnight on the 6th has opened
/// for the board but not for UTC, and windows that closed at 00:29 on the 6th or 23:59 on the 5th have closed for the
/// board but not for UTC, so a UTC comparison gives the opposite answer for each of them.
/// </remarks>
public class ShopAvailabilityWindowTests(WebApplicationFactoryBase factory) : IClassFixture<WebApplicationFactoryBase>
{
    private const string BoardZoneId = "Europe/Amsterdam";

    private const string DesktopUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

    private const string MobileUserAgent =
        "Mozilla/5.0 (Linux; Android 14; Pixel 7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Mobile Safari/537.36";

    private const string NotAvailableNotice = "This item is not currently available for purchase.";

    private static readonly DateTimeOffset PinnedNow = new(2026, 9, 5, 22, 30, 0, TimeSpan.Zero);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    // One shape per boundary, all as board-local wall-clock values the way a Dungeon Master types them.
    private enum Shape { NoWindow, OpensNow, OpensAtMidnight, OpensNextMinute, ClosesNow, ClosedLastMinute, ClosedLastNight }

    private static readonly (Shape Shape, string Name, DateTime? From, DateTime? Until)[] Shapes =
    [
        (Shape.NoWindow, "Lantern Always", null, null),
        (Shape.OpensNow, "Lantern Opening Now", new DateTime(2026, 9, 6, 0, 30, 0), null),
        (Shape.OpensAtMidnight, "Lantern Opened At Midnight", new DateTime(2026, 9, 6, 0, 0, 0), null),
        (Shape.OpensNextMinute, "Lantern Opening Soon", new DateTime(2026, 9, 6, 0, 31, 0), null),
        (Shape.ClosesNow, "Lantern Closing Now", null, new DateTime(2026, 9, 6, 0, 30, 0)),
        (Shape.ClosedLastMinute, "Lantern Closed A Minute Ago", null, new DateTime(2026, 9, 6, 0, 29, 0)),
        (Shape.ClosedLastNight, "Lantern Closed Last Night", null, new DateTime(2026, 9, 5, 23, 59, 0)),
    ];

    private static bool IsOnSale(Shape shape) =>
        shape is Shape.NoWindow or Shape.OpensNow or Shape.OpensAtMidnight or Shape.ClosesNow;

    private static bool HasClosed(Shape shape) => shape is Shape.ClosedLastMinute or Shape.ClosedLastNight;

    private WebApplicationFactory<Program> CreatePinnedFactory()
    {
        return factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configBuilder) =>
            {
                configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["TimeZone:BoardTimeZoneId"] = BoardZoneId
                });
            });

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(new FixedTimeProvider(PinnedNow));
            });
        });
    }

    private sealed record Board(
        WebApplicationFactory<Program> Pinned,
        HttpClient KeeperClient,
        Dictionary<Shape, int> Ids);

    // Clears the store and seeds the seven items, stock 5 each, made by a Dungeon Master of board 1.
    private async Task<Board> SeedBoardAsync(string handle)
    {
        var pinned = CreatePinnedFactory();
        await TestDataHelper.ClearDatabaseAsync(pinned.Services);
        factory.TestGroupContext.ActiveGroupId = 1;

        var (keeperClient, keeper) = await AuthenticationHelper.CreateAuthenticatedDMClientAsync(
            pinned, $"{handle}_keeper", $"{handle}_keeper@example.com");
        var ids = new Dictionary<Shape, int>();
        foreach (var (shape, name, from, until) in Shapes)
        {
            var item = await TestDataHelper.CreateShopItemAsync(pinned.Services, keeper.Id, name, 40m, 5);
            using var scope = pinned.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
            var tracked = await context.ShopItems.IgnoreQueryFilters().SingleAsync(i => i.Id == item.Id, Token);
            tracked.AvailableFrom = from;
            tracked.AvailableUntil = until;
            await context.SaveChangesAsync(Token);
            ids[shape] = item.Id;
        }

        return new Board(pinned, keeperClient, ids);
    }

    private static async Task<(HttpClient Client, UserEntity User)> CreateMemberAsync(Board board, string handle) =>
        await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            board.Pinned, $"{handle}_member", $"{handle}_member@example.com");

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method, string url, string userAgent, HttpContent? content = null)
    {
        var request = new HttpRequestMessage(method, url) { Content = content };
        request.Headers.TryAddWithoutValidation("User-Agent", userAgent);
        return await client.SendAsync(request, Token);
    }

    private static bool HasPurchaseForm(string html) =>
        Regex.IsMatch(html, @"<form\b[^>]*\baction=""/Shop/Purchase/\d+""") && html.Contains("Purchase Item");

    private static bool HasSellToShopForm(string html) =>
        Regex.IsMatch(html, @"<form\b[^>]*\baction=""/Shop/SellToShop/\d+""") || Regex.IsMatch(html, @"\bdata-sell-form\b");

    private static IEnumerable<(string Label, string Url, string UserAgent)> DetailsViews(int itemId) =>
    [
        ("desktop page", $"/Shop/Details/{itemId}", DesktopUserAgent),
        ("phone page", $"/Shop/Details/{itemId}", MobileUserAgent),
        ("dialog", $"/Shop/Details/{itemId}?isModal=true", DesktopUserAgent),
    ];

    private async Task<int> MissingIdAsync()
    {
        await using var ctx = factory.Database.CreateContext();
        return await ctx.ShopItems.IgnoreQueryFilters().MaxAsync(i => i.Id, Token) + 1000;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShopIndex_ListsExactlyTheItemsOnSaleByTheBoardsClock(bool mobile)
    {
        var board = await SeedBoardAsync($"shopwin_index_{(mobile ? "m" : "d")}");
        var (member, _) = await CreateMemberAsync(board, $"shopwin_index_{(mobile ? "m" : "d")}");

        var response = await SendAsync(member, HttpMethod.Get, "/Shop", mobile ? MobileUserAgent : DesktopUserAgent);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync(Token));
        using var scope = new AssertionScope();
        foreach (var (shape, name, _, _) in Shapes)
        {
            html.Contains(name).Should().Be(IsOnSale(shape), $"'{name}' is {(IsOnSale(shape) ? "" : "not ")}on sale at 00:30 board time");
        }
    }

    [Fact]
    public async Task ShopDetails_ForAMember_FollowsTheWindowOnThePageThePhonePageAndTheDialog()
    {
        var board = await SeedBoardAsync("shopwin_details");
        var (member, _) = await CreateMemberAsync(board, "shopwin_details");
        var missingId = await MissingIdAsync();

        foreach (var (shape, name, _, _) in Shapes)
        {
            foreach (var (label, url, userAgent) in DetailsViews(board.Ids[shape]))
            {
                var response = await SendAsync(member, HttpMethod.Get, url, userAgent);
                var body = await response.Content.ReadAsStringAsync(Token);

                if (shape == Shape.OpensNextMinute)
                {
                    var missingUrl = url.Replace($"/Shop/Details/{board.Ids[shape]}", $"/Shop/Details/{missingId}");
                    var missing = await SendAsync(member, HttpMethod.Get, missingUrl, userAgent);
                    response.StatusCode.Should().Be(HttpStatusCode.NotFound, $"an item whose sale has not started is hidden from a member ({label})");
                    missing.StatusCode.Should().Be(HttpStatusCode.NotFound);
                    body.Should().Be(await missing.Content.ReadAsStringAsync(Token),
                        $"a guessed id must not tell a scheduled item from one that does not exist ({label})");
                    continue;
                }

                response.StatusCode.Should().Be(HttpStatusCode.OK, $"'{name}' opens for a member ({label})");
                var html = WebUtility.HtmlDecode(body);
                html.Should().Contain(name);
                if (IsOnSale(shape))
                {
                    HasPurchaseForm(html).Should().BeTrue($"'{name}' is on sale ({label})");
                    HasSellToShopForm(html).Should().BeTrue($"'{name}' can be sold to the shop ({label})");
                    html.Should().NotContain(NotAvailableNotice);
                }
                else
                {
                    HasClosed(shape).Should().BeTrue();
                    HasPurchaseForm(html).Should().BeFalse($"'{name}' is no longer on sale ({label})");
                    HasSellToShopForm(html).Should().BeFalse($"the shop no longer trades '{name}' ({label})");
                    html.Should().Contain(NotAvailableNotice, $"a closed item says why it cannot be bought ({label})");
                }
            }
        }
    }

    [Theory]
    [InlineData("creator", false)]
    [InlineData("creator", true)]
    [InlineData("dm", false)]
    [InlineData("dm", true)]
    [InlineData("admin", false)]
    [InlineData("admin", true)]
    [InlineData("superadmin", false)]
    [InlineData("superadmin", true)]
    public async Task ShopDetails_AnItemWhoseSaleHasNotStarted_OpensForReviewersWithoutBuyOrSellForms(string viewer, bool dialog)
    {
        var handle = $"shopwin_review_{viewer}_{(dialog ? "dlg" : "page")}";
        var board = await SeedBoardAsync(handle);
        var userName = $"{handle}_viewer";
        var email = $"{userName}@example.com";
        var client = viewer switch
        {
            "creator" => board.KeeperClient,
            "dm" => (await AuthenticationHelper.CreateAuthenticatedDMClientAsync(board.Pinned, userName, email)).client,
            "admin" => (await AuthenticationHelper.CreateAuthenticatedAdminClientAsync(board.Pinned, userName, email)).client,
            "superadmin" => (await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(board.Pinned, userName, email)).client,
            _ => throw new ArgumentOutOfRangeException(nameof(viewer), viewer, "unknown reviewer kind"),
        };
        var itemId = board.Ids[Shape.OpensNextMinute];

        var response = await SendAsync(client, HttpMethod.Get,
            dialog ? $"/Shop/Details/{itemId}?isModal=true" : $"/Shop/Details/{itemId}", DesktopUserAgent);

        response.StatusCode.Should().Be(HttpStatusCode.OK, "whoever reviews items in shop management may open a scheduled one");
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync(Token));
        html.Should().Contain("Lantern Opening Soon");
        html.Should().Contain(NotAvailableNotice);
        HasPurchaseForm(html).Should().BeFalse("the server would refuse the purchase");
        HasSellToShopForm(html).Should().BeFalse("the server would refuse the sale");
    }

    private async Task<(int Stock, int Rows)> ReadStockAndRowsAsync(int userId, int itemId)
    {
        await using var ctx = factory.Database.CreateContext();
        var stock = await ctx.ShopItems.IgnoreQueryFilters().Where(i => i.Id == itemId).Select(i => i.Quantity).SingleAsync(Token);
        var rows = await ctx.UserTransactions.IgnoreQueryFilters().CountAsync(t => t.UserId == userId && t.ShopItemId == itemId, Token);
        return (stock, rows);
    }

    // Posts with a token read from the windowless item's details page, which always opens, because a refused id may
    // answer not found.
    private static async Task<HttpResponseMessage> PostWithTokenAsync(HttpClient client, int tokenItemId, string url)
    {
        var page = await client.GetAsync($"/Shop/Details/{tokenItemId}", Token);
        page.StatusCode.Should().Be(HttpStatusCode.OK);
        var (token, _) = await AntiForgeryHelper.ExtractAntiForgeryTokenAsync(page);
        var content = AntiForgeryHelper.CreateFormContentWithAntiForgeryToken(new Dictionary<string, string> { ["quantity"] = "1" }, token);
        return await client.PostAsync(url, content, Token);
    }

    [Fact]
    public async Task PurchaseAndSellToShop_MoveStockOnlyForItemsOnSaleByTheBoardsClock()
    {
        var board = await SeedBoardAsync("shopwin_trade");
        var (member, user) = await CreateMemberAsync(board, "shopwin_trade");
        var tokenItemId = board.Ids[Shape.NoWindow];

        using var scope = new AssertionScope();
        foreach (var (shape, name, _, _) in Shapes)
        {
            var itemId = board.Ids[shape];

            var purchase = await PostWithTokenAsync(member, tokenItemId, $"/Shop/Purchase/{itemId}");
            purchase.StatusCode.Should().Be(HttpStatusCode.Redirect);
            var afterPurchase = await ReadStockAndRowsAsync(user.Id, itemId);
            afterPurchase.Should().Be(IsOnSale(shape) ? (4, 1) : (5, 0), $"buying '{name}' at 00:30 board time");
            if (HasClosed(shape))
            {
                purchase.Headers.Location!.OriginalString.Should().Be($"/Shop/Details/{itemId}");
                var landing = await member.GetAsync(purchase.Headers.Location.OriginalString, Token);
                landing.StatusCode.Should().Be(HttpStatusCode.OK, "a refused purchase of a closed item lands on its details page");
                WebUtility.HtmlDecode(await landing.Content.ReadAsStringAsync(Token)).Should().Contain("Item is not available for purchase.");
            }

            var sale = await PostWithTokenAsync(member, tokenItemId, $"/Shop/SellToShop/{itemId}");
            sale.StatusCode.Should().Be(HttpStatusCode.Redirect);
            var afterSale = await ReadStockAndRowsAsync(user.Id, itemId);
            afterSale.Should().Be(IsOnSale(shape) ? (5, 2) : (5, 0), $"selling '{name}' to the shop at 00:30 board time");
            if (HasClosed(shape))
            {
                var landing = await member.GetAsync(sale.Headers.Location!.OriginalString, Token);
                landing.StatusCode.Should().Be(HttpStatusCode.OK, "a refused sale of a closed item lands on its details page");
                WebUtility.HtmlDecode(await landing.Content.ReadAsStringAsync(Token)).Should().Contain("Item is not available for sale.");
            }
        }
    }

    [Fact]
    public async Task HistoryReturn_OfAnItemWhoseSaleHasClosed_StillRefundsAndRestocks()
    {
        var board = await SeedBoardAsync("shopwin_return");
        var (member, user) = await CreateMemberAsync(board, "shopwin_return");
        var itemId = board.Ids[Shape.ClosedLastNight];
        int purchaseId;
        await using (var ctx = factory.Database.CreateContext())
        {
            // The purchase moment is a real UTC instant an hour back, well inside the return window.
            var purchaseRow = new UserTransactionEntity
            {
                UserId = user.Id,
                ShopItemId = itemId,
                TransactionType = (int)TransactionType.Purchase,
                Price = 80m,
                Quantity = 2,
                TransactionDate = DateTime.UtcNow.AddHours(-1),
            };
            ctx.UserTransactions.Add(purchaseRow);
            await ctx.SaveChangesAsync(Token);
            purchaseId = purchaseRow.Id;
        }

        var page = await SendAsync(member, HttpMethod.Get, "/Shop", DesktopUserAgent);
        page.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await page.Content.ReadAsStringAsync(Token);
        var slip = Regex.Matches(html, @"<form\b[^>]*\baction=""/Shop/Sell""[^>]*>[\s\S]*?</form>")
            .Select(m => m.Value)
            .Where(f => Regex.IsMatch(f, @"\bname=""id""[^>]*\bvalue=""" + purchaseId + @"""|\bvalue=""" + purchaseId + @"""[^>]*\bname=""id"""))
            .Where(f => Regex.IsMatch(Regex.Match(f, @"<button\b[\s\S]*?</button>").Value, @"\bReturn\b"))
            .Should().ContainSingle("the history offers a Return slip for the purchase").Subject;
        var fields = Regex.Matches(slip, @"<input\b[^>]*>")
            .Select(i => (Name: Regex.Match(i.Value, @"\bname=""([^""]*)""").Groups[1].Value,
                          Value: WebUtility.HtmlDecode(Regex.Match(i.Value, @"\bvalue=""([^""]*)""").Groups[1].Value)))
            .Where(f => f.Name.Length > 0)
            .ToDictionary(f => f.Name, f => f.Value);

        var response = await SendAsync(member, HttpMethod.Post, "/Shop/Sell", DesktopUserAgent, new FormUrlEncodedContent(fields));

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        await using var check = factory.Database.CreateContext();
        var refund = await check.UserTransactions.AsNoTracking().IgnoreQueryFilters()
            .SingleAsync(t => t.OriginalTransactionId == purchaseId, Token);
        refund.Price.Should().Be(80m, "a return inside the window refunds the full price, whatever the item's sale window");
        refund.Quantity.Should().Be(2);
        var stock = await check.ShopItems.AsNoTracking().IgnoreQueryFilters().Where(i => i.Id == itemId).Select(i => i.Quantity).SingleAsync(Token);
        stock.Should().Be(7, "the handed-back items go back on the shelf");
    }
}
