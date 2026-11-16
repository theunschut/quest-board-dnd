using System.Net;
using System.Text.RegularExpressions;
using QuestBoard.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using QuestBoard.IntegrationTests.Helpers;
using QuestBoard.Service.ViewModels.ShopViewModels;

namespace QuestBoard.IntegrationTests.Tests;

/// <summary>
/// Pins what the shop pages render, on both layouts: the navy theme is carried by the html element and nowhere
/// else, the Sell to Shop form hands its item name to the page script through an encoded data attribute instead of
/// an inline handler, and the purchase history's Return and Sell forms carry an antiforgery token so they post. The
/// layout is chosen from the request's User-Agent header, so every fact states which one it sends.
/// </summary>
public class ShopRenderTests(WebApplicationFactoryBase factory) : IClassFixture<WebApplicationFactoryBase>
{
    private const string DesktopUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

    private const string MobileUserAgent =
        "Mozilla/5.0 (Linux; Android 14; Pixel 7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Mobile Safari/537.36";

    private const string AwkwardName = "Wizard's \"Staff\"";

    private static string AgentFor(bool mobile) => mobile ? MobileUserAgent : DesktopUserAgent;

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method, string url, string userAgent, HttpContent? content = null)
    {
        var request = new HttpRequestMessage(method, url) { Content = content };
        request.Headers.TryAddWithoutValidation("User-Agent", userAgent);
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static async Task<string> GetAsync(HttpClient client, string url, string userAgent)
    {
        var response = await SendAsync(client, HttpMethod.Get, url, userAgent);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
    }

    // A signed-in board member and one Published item with the given name.
    private async Task<(HttpClient Client, UserEntity User, int ItemId)> SeedMemberAndItemAsync(
        string handle, string itemName = "Plain Lantern", decimal price = 40m)
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.ActiveGroupId = 1;

        var shopkeeper = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, $"{handle}_keeper", $"{handle}_keeper@example.com");
        var item = await TestDataHelper.CreateShopItemAsync(factory.Services, shopkeeper.Id, itemName, price, 5);
        var (client, user) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, handle, $"{handle}@example.com", name: "Shop Render Member");
        return (client, user, item.Id);
    }

    private static string HtmlOpeningTag(string html)
    {
        var match = Regex.Match(html, @"<html\b[^>]*>", RegexOptions.IgnoreCase);
        match.Success.Should().BeTrue("the page has an html element");
        return match.Value;
    }

    private static string BodyOpeningTag(string html)
    {
        var match = Regex.Match(html, @"<body\b[^>]*>", RegexOptions.IgnoreCase);
        match.Success.Should().BeTrue("the page has a body element");
        return match.Value;
    }

    // The opening tag of the sell form: the one that carries data-sell-form.
    private static string SellFormTag(string html)
    {
        var match = Regex.Match(html, @"<form\b[^>]*\bdata-sell-form\b[^>]*>", RegexOptions.IgnoreCase);
        match.Success.Should().BeTrue("the Sell to Shop form carries data-sell-form");
        return match.Value;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShopDetails_BothLayouts_CarryTheShopThemeOnHtmlNotBody(bool mobile)
    {
        var (client, _, itemId) = await SeedMemberAndItemAsync($"shopr_theme_{(mobile ? "m" : "d")}");

        var html = await GetAsync(client, $"/Shop/Details/{itemId}", AgentFor(mobile));

        HtmlOpeningTag(html).Should().Contain("data-theme=\"shop\"", "the navy theme sits on the html element");
        BodyOpeningTag(html).Should().NotContain("data-theme", "a theme on the body cannot recolour the page's fallback background");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PageOutsideTheShop_BothLayouts_RenderNoTheme(bool mobile)
    {
        var (client, _, _) = await SeedMemberAndItemAsync($"shopr_notheme_{(mobile ? "m" : "d")}");

        var html = await GetAsync(client, "/Agenda", AgentFor(mobile));

        HtmlOpeningTag(html).Should().NotContain("data-theme", "a page that sets no theme renders none");
        BodyOpeningTag(html).Should().NotContain("data-theme");
    }

    [Theory]
    [InlineData("/Shop/Details/{0}")]
    [InlineData("/Shop/Details/{0}?isModal=true")]
    public async Task DesktopDetailsAndDialog_SellForm_CarriesDataAttributesAndNoInlineHandler(string urlFormat)
    {
        var (client, _, itemId) = await SeedMemberAndItemAsync("shopr_sellform", price: 40m);

        var html = await GetAsync(client, string.Format(urlFormat, itemId), DesktopUserAgent);

        var form = SellFormTag(html);
        form.Should().Contain("data-item-name=\"Plain Lantern\"");
        form.Should().MatchRegex(@"data-unit-price=""20(\.0+)?""", "the unit price is half the shop price");
        form.Should().NotContain("onsubmit");

        Regex.IsMatch(html, @"<input\b[^>]*\bdata-sell-quantity\b[^>]*>").Should().BeTrue("the quantity input is found by its data attribute");
        Regex.IsMatch(html, @"<output\b[^>]*\bdata-sell-total\b[^>]*>").Should().BeTrue("the total is an output element");
        Regex.IsMatch(html, @"<output\b[^>]*\bfor=""sellQuantity""[^>]*>").Should().BeTrue("the total names the quantity it is computed from");
        Regex.IsMatch(html, @"\sonchange=", RegexOptions.IgnoreCase).Should().BeFalse("no inline change handler");
        Regex.IsMatch(html, @"\sonsubmit=", RegexOptions.IgnoreCase).Should().BeFalse("no inline submit handler");
    }

    [Fact]
    public async Task DialogFragment_HoldsNoScriptElement()
    {
        var (client, _, itemId) = await SeedMemberAndItemAsync("shopr_fragment");

        var html = await GetAsync(client, $"/Shop/Details/{itemId}?isModal=true", DesktopUserAgent);

        html.Should().NotContain("<script", "a fragment written into the dialog never runs its scripts, so it carries none");
    }

    [Theory]
    [InlineData("/Shop/Details/{0}")]
    [InlineData("/Shop/Details/{0}?isModal=true")]
    public async Task ItemNameWithApostropheAndQuotes_TravelsOnlyAsAnEncodedDataAttribute(string urlFormat)
    {
        var (client, _, itemId) = await SeedMemberAndItemAsync("shopr_awkward", AwkwardName);

        var html = await GetAsync(client, string.Format(urlFormat, itemId), DesktopUserAgent);

        var form = SellFormTag(html);
        form.Should().Contain("data-item-name=\"Wizard&#x27;s &quot;Staff&quot;\"", "the name is encoded by Razor");
        form.Should().NotContain("onsubmit");
        html.Should().NotContain("confirmSell", "no inline handler interpolates the name into script text");
    }

    [Theory]
    [InlineData("/Shop", false)]
    [InlineData("/Shop", true)]
    [InlineData("/Shop/Details/{0}", false)]
    [InlineData("/Shop/Details/{0}", true)]
    public async Task ShopPagesThatShowTheDetails_LoadTheSellModule(string urlFormat, bool mobile)
    {
        var (client, _, itemId) = await SeedMemberAndItemAsync($"shopr_module_{(mobile ? "m" : "d")}");

        var html = await GetAsync(client, string.Format(urlFormat, itemId), AgentFor(mobile));

        Regex.IsMatch(html, @"<script\b[^>]*\btype=""module""[^>]*\bsrc=""/js/shop-item-sell\.js(?:\?[^""]*)?""").Should().BeTrue(
            "the sell confirmation and total come from one module");
    }

    // A purchase the member made the given number of hours ago, still fully in hand.
    private async Task SeedPurchaseAsync(int userId, int itemId, double hoursAgo, int quantity = 2, decimal price = 80m)
    {
        await using var ctx = factory.Database.CreateContext();
        ctx.UserTransactions.Add(new UserTransactionEntity
        {
            UserId = userId,
            ShopItemId = itemId,
            TransactionType = (int)TransactionType.Purchase,
            Price = price,
            Quantity = quantity,
            TransactionDate = DateTime.UtcNow.AddHours(-hoursAgo),
        });
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    // Every post form on the page that targets the shop's Sell action, with its fields as the browser would send them.
    private static List<(string Label, Dictionary<string, string> Fields)> RowForms(string html)
    {
        var forms = new List<(string, Dictionary<string, string>)>();
        foreach (Match form in Regex.Matches(html, @"<form\b[^>]*\baction=""/Shop/Sell""[^>]*>([\s\S]*?)</form>", RegexOptions.IgnoreCase))
        {
            var body = form.Groups[1].Value;
            var fields = new Dictionary<string, string>();
            foreach (Match input in Regex.Matches(body, @"<input\b[^>]*>", RegexOptions.IgnoreCase))
            {
                var name = Regex.Match(input.Value, @"\bname=""([^""]*)""").Groups[1].Value;
                var value = System.Net.WebUtility.HtmlDecode(Regex.Match(input.Value, @"\bvalue=""([^""]*)""").Groups[1].Value);
                if (name.Length > 0)
                    fields[name] = value;
            }

            var label = body.Contains("Return (", StringComparison.Ordinal) ? "Return" : body.Contains("Sell (", StringComparison.Ordinal) ? "Sell" : "unknown";
            forms.Add((label, fields));
        }

        return forms;
    }

    [Theory]
    [InlineData(2, "Return")]
    [InlineData(30, "Sell")]
    public async Task HistoryRowForm_CarriesAnAntiforgeryTokenAndPostsToARedirect(double hoursAgo, string expectedKind)
    {
        var (client, user, itemId) = await SeedMemberAndItemAsync($"shopr_row_{expectedKind.ToLowerInvariant()}", price: 40m);
        await SeedPurchaseAsync(user.Id, itemId, hoursAgo);

        var html = await GetAsync(client, "/Shop", DesktopUserAgent);

        var rows = RowForms(html);
        rows.Should().ContainSingle(r => r.Label == expectedKind, $"a purchase made {hoursAgo} hours ago offers {expectedKind}");
        var (_, fields) = rows.Single(r => r.Label == expectedKind);
        fields.Should().ContainKey("__RequestVerificationToken", "the form posts with an antiforgery token");
        fields.Should().ContainKey("id");
        fields.Should().ContainKey("quantity");

        var response = await SendAsync(client, HttpMethod.Post, "/Shop/Sell", DesktopUserAgent, new FormUrlEncodedContent(fields));

        response.StatusCode.Should().NotBe(HttpStatusCode.BadRequest, "a missing token is answered with 400");
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found);
    }

    // ---- Rarity chips --------------------------------------------------------------------------------

    private static readonly (ItemRarity Rarity, string Key, string Word)[] Rarities =
    [
        (ItemRarity.Common, "common", "Common"),
        (ItemRarity.Uncommon, "uncommon", "Uncommon"),
        (ItemRarity.Rare, "rare", "Rare"),
        (ItemRarity.VeryRare, "veryrare", "Very Rare"),
        (ItemRarity.Legendary, "legendary", "Legendary"),
    ];

    // Every rarity chip in the markup as (key, word), in page order.
    private static List<(string Key, string Word)> RarityChips(string html) =>
        Regex.Matches(html,
                @"<span\s+class=""badge badge-rarity rarity-(?<key>[a-z]+)"">\s*<i\b[^>]*aria-hidden=""true""[^>]*></i>\s*(?<word>[A-Za-z ]+?)\s*</span>")
            .Select(m => (m.Groups["key"].Value, m.Groups["word"].Value)).ToList();

    // The chips that sit inside item cards: the text after each card's opening marker, up to the next card.
    private static List<(string Key, string Word)> CardChips(string html, string cardMarker) =>
        html.Split(cardMarker).Skip(1).Select(chunk => RarityChips(chunk).First()).ToList();

    private async Task<(HttpClient Client, int FirstItemId)> SeedOneItemPerRarityAsync(string handle)
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.ActiveGroupId = 1;

        var shopkeeper = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, $"{handle}_keeper", $"{handle}_keeper@example.com");
        var firstId = 0;
        foreach (var (rarity, _, word) in Rarities)
        {
            var item = await TestDataHelper.CreateShopItemAsync(factory.Services, shopkeeper.Id, $"{word} Trinket", 40m, 5, rarity);
            if (firstId == 0)
                firstId = item.Id;
        }

        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, handle, $"{handle}@example.com", name: "Shop Render Member");
        return (client, firstId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShopIndex_OneItemOfEachRarity_RendersFiveChipsWhoseKeysAndWordsMatchTheMapping(bool mobile)
    {
        var (client, _) = await SeedOneItemPerRarityAsync($"shopr_chips_{(mobile ? "m" : "d")}");

        var html = await GetAsync(client, "/Shop", AgentFor(mobile));

        var cards = CardChips(html, mobile ? "class=\"shop-item-card-mobile\"" : "class=\"item-card\"");
        cards.Should().HaveCount(Rarities.Length, "each rarity has one card");
        cards.Select(c => c.Key).Should().BeEquivalentTo(Rarities.Select(r => ShopItemDisplay.RarityKey(r.Rarity)));
        foreach (var (rarity, key, word) in Rarities)
        {
            ShopItemDisplay.RarityKey(rarity).Should().Be(key);
            ShopItemDisplay.RarityName(rarity).Should().Be(word);
            cards.Should().Contain((key, word), $"the {word} card carries the {word} chip");
        }

        html.Should().NotContain("item-rarity", "the old chip markup is gone from the shop page");
    }

    [Theory]
    [InlineData("/Shop/Details/{0}", false)]
    [InlineData("/Shop/Details/{0}", true)]
    [InlineData("/Shop/Details/{0}?isModal=true", false)]
    public async Task ShopDetails_PageAndDialog_RenderOneChipForTheItemsRarity(string urlFormat, bool mobile)
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.ActiveGroupId = 1;
        var shopkeeper = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "shopr_det_keeper", "shopr_det_keeper@example.com");
        var item = await TestDataHelper.CreateShopItemAsync(
            factory.Services, shopkeeper.Id, "Dawn Blade", 900m, 1, ItemRarity.Legendary);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, $"shopr_det_{(mobile ? "m" : "d")}", $"shopr_det_{(mobile ? "m" : "d")}@example.com", name: "Shop Render Member");

        var html = await GetAsync(client, string.Format(urlFormat, item.Id), AgentFor(mobile));

        RarityChips(html).Should().Equal([("legendary", "Legendary")], "the details show the rarity once, as the solid chip");
        html.Should().NotContain("item-rarity");
    }

    [Fact]
    public async Task ShopDetails_VeryRareItem_RendersTheVeryRareKeyAndWords()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.ActiveGroupId = 1;
        var shopkeeper = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "shopr_vr_keeper", "shopr_vr_keeper@example.com");
        var item = await TestDataHelper.CreateShopItemAsync(
            factory.Services, shopkeeper.Id, "Stormcaller", 500m, 1, ItemRarity.VeryRare);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "shopr_vr", "shopr_vr@example.com", name: "Shop Render Member");

        var html = await GetAsync(client, $"/Shop/Details/{item.Id}", DesktopUserAgent);

        RarityChips(html).Should().Equal([("veryrare", "Very Rare")]);
    }

    [Theory]
    [InlineData("/Shop", false)]
    [InlineData("/Shop", true)]
    [InlineData("/Shop/Details/{0}", false)]
    [InlineData("/Shop/Details/{0}", true)]
    public async Task ShopPages_LinkTheShopPageSheet(string urlFormat, bool mobile)
    {
        var (client, _, itemId) = await SeedMemberAndItemAsync($"shopr_sheet_{(mobile ? "m" : "d")}");

        var html = await GetAsync(client, string.Format(urlFormat, itemId), AgentFor(mobile));

        Regex.IsMatch(html, @"<link\b[^>]*\bhref=""/css/shop-pages\.css(?:\?[^""]*)?""").Should().BeTrue(
            "the chips are styled by the shop page sheet");
    }

    // The shop item with the given status, written straight to the store because the helper only makes Published ones.
    private async Task SetStatusAsync(int itemId, ItemStatus status)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
        var tracked = await context.ShopItems.IgnoreQueryFilters().SingleAsync(i => i.Id == itemId, TestContext.Current.CancellationToken);
        tracked.Status = (int)status;
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    // The status badge a partial renders: its fill, icon and word, in that order.
    private static bool HasStatusBadge(string html, string fill, string icon, string word) =>
        Regex.IsMatch(html,
            @"<span\s+class=""badge " + Regex.Escape(fill) + @"""><i\s+class=""fas " + Regex.Escape(icon)
            + @" me-1""\s+aria-hidden=""true""></i>" + Regex.Escape(word) + @"</span>");

    // A signed-in Dungeon Master and a board with every status: the DM's own Draft, Published, Archived and Denied
    // items, a Draft by another keeper (the review table) and a Published one by that keeper (the other-items table).
    private async Task<HttpClient> SeedManagementBoardAsync(string handle)
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.ActiveGroupId = 1;
        var keeper = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, $"{handle}_keeper", $"{handle}_keeper@example.com");
        var (client, dm) = await AuthenticationHelper.CreateAuthenticatedDMClientAsync(factory, handle, $"{handle}@example.com");

        var own = new (string Name, ItemStatus Status, ItemRarity Rarity, ItemType Type)[]
        {
            ("Own Draft", ItemStatus.Draft, ItemRarity.Rare, ItemType.Equipment),
            ("Own Published", ItemStatus.Published, ItemRarity.Legendary, ItemType.MagicItem),
            ("Own Archived", ItemStatus.Archived, ItemRarity.VeryRare, ItemType.QuestItems),
            ("Own Denied", ItemStatus.Denied, ItemRarity.Uncommon, ItemType.Equipment),
        };
        foreach (var (name, status, rarity, type) in own)
        {
            var item = await TestDataHelper.CreateShopItemAsync(factory.Services, dm.Id, name, 10m, 1, rarity, type);
            await SetStatusAsync(item.Id, status);
        }

        var review = await TestDataHelper.CreateShopItemAsync(factory.Services, keeper.Id, "Review Me", 10m, 1, ItemRarity.Rare);
        await SetStatusAsync(review.Id, ItemStatus.Draft);
        await TestDataHelper.CreateShopItemAsync(factory.Services, keeper.Id, "Other Published", 10m, 1, ItemRarity.Common);
        return client;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShopManagementList_BothLayouts_ShowTheSharedStatusVocabulary(bool mobile)
    {
        var client = await SeedManagementBoardAsync($"shopr_status_{(mobile ? "m" : "d")}");

        var html = await GetAsync(client, "/ShopManagement", AgentFor(mobile));

        HasStatusBadge(html, "bg-warning", "fa-hourglass-half", "Pending Review").Should().BeTrue("a draft reads Pending Review with its icon");
        HasStatusBadge(html, "bg-success", "fa-check-circle", "Available").Should().BeTrue("a published item reads Available with its icon");
        HasStatusBadge(html, "bg-secondary", "fa-box-archive", "Archived").Should().BeTrue();
        HasStatusBadge(html, "bg-danger", "fa-ban", "Denied").Should().BeTrue();
        html.Should().NotContain(">Active<", "the phone list no longer has its own word for a published item");
        html.Should().NotContain(">Draft<", "a draft is shown as Pending Review everywhere");
        html.Should().NotContain("fa-published", "no icon is built from the status name");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShopManagementList_BothLayouts_ShowTheSharedRarityChip(bool mobile)
    {
        var client = await SeedManagementBoardAsync($"shopr_chip_{(mobile ? "m" : "d")}");

        var html = await GetAsync(client, "/ShopManagement", AgentFor(mobile));

        var chips = RarityChips(html);
        chips.Should().Contain(("legendary", "Legendary"));
        chips.Should().Contain(("veryrare", "Very Rare"));
        chips.Should().Contain(("uncommon", "Uncommon"));
        chips.Should().Contain(("rare", "Rare"));
        chips.Should().Contain(("common", "Common"));
        html.Should().NotContain("item-rarity", "the old chip markup is gone from the management list");
        Regex.IsMatch(html, @"<link\b[^>]*\bhref=""/css/shop-pages\.css(?:\?[^""]*)?""").Should().BeTrue(
            "the chips are styled by the shop page sheet");
    }

    [Fact]
    public async Task ShopManagementList_Desktop_ShowsTypesAsWordsOnOneNeutralChip()
    {
        var client = await SeedManagementBoardAsync("shopr_types");

        var html = await GetAsync(client, "/ShopManagement", DesktopUserAgent);

        Regex.IsMatch(html, @"<span\s+class=""badge bg-secondary""><i\s+class=""fas fa-magic me-1""\s+aria-hidden=""true""></i>Magic Item</span>")
            .Should().BeTrue("a magic item row shows the type word");
        Regex.IsMatch(html, @"<span\s+class=""badge bg-secondary""><i\s+class=""fas fa-scroll me-1""\s+aria-hidden=""true""></i>Quest Item</span>")
            .Should().BeTrue("a quest item row shows the type word");
        Regex.IsMatch(html, @"<span\s+class=""badge bg-secondary""><i\s+class=""fas fa-hammer me-1""\s+aria-hidden=""true""></i>Equipment</span>")
            .Should().BeTrue();
        html.Should().NotMatchRegex(@"type-(equipment|magic-item|quest-item)", "no element carries a type class");
    }

    [Theory]
    [InlineData(false, ItemStatus.Draft, "bg-warning", "fa-hourglass-half", "Pending Review")]
    [InlineData(true, ItemStatus.Draft, "bg-warning", "fa-hourglass-half", "Pending Review")]
    [InlineData(false, ItemStatus.Published, "bg-success", "fa-check-circle", "Available")]
    [InlineData(true, ItemStatus.Published, "bg-success", "fa-check-circle", "Available")]
    [InlineData(false, ItemStatus.Archived, "bg-secondary", "fa-box-archive", "Archived")]
    [InlineData(true, ItemStatus.Denied, "bg-danger", "fa-ban", "Denied")]
    public async Task ShopManagementEdit_BothLayouts_StatusAlertShowsTheSharedBadge(
        bool mobile, ItemStatus status, string fill, string icon, string word)
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.ActiveGroupId = 1;
        var (client, dm) = await AuthenticationHelper.CreateAuthenticatedDMClientAsync(
            factory, $"shopr_edit_{(mobile ? "m" : "d")}_{status}", $"shopr_edit_{(mobile ? "m" : "d")}_{status}@example.com");
        var item = await TestDataHelper.CreateShopItemAsync(factory.Services, dm.Id, "Edit Status", 10m, 1, ItemRarity.Rare);
        await SetStatusAsync(item.Id, status);

        var html = await GetAsync(client, $"/ShopManagement/Edit/{item.Id}", AgentFor(mobile));

        HasStatusBadge(html, fill, icon, word).Should().BeTrue($"the status alert shows {word} through the shared badge");
    }
}
