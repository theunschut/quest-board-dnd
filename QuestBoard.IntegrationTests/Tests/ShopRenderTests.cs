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

        var cards = CardChips(html, mobile ? "class=\"shop-item-card-mobile\"" : "data-item-card");
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
    [InlineData("/Shop/Details/{0}?isModal=true", true)]
    public async Task ShopDetails_PageAndDialog_RenderTheItemsRarityChipBesideTheEmblemAndInTheProperties(string urlFormat, bool mobile)
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

        RarityChips(html).Should().Equal([("legendary", "Legendary"), ("legendary", "Legendary")],
            "the details show the rarity as the solid chip under the emblem and again in the properties list");
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

        RarityChips(html).Should().Equal([("veryrare", "Very Rare"), ("veryrare", "Very Rare")]);
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
    private async Task<HttpClient> SeedManagementBoardAsync(string handle, bool admin = false)
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.ActiveGroupId = 1;
        var keeper = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, $"{handle}_keeper", $"{handle}_keeper@example.com");
        var (client, dm) = admin
            ? await AuthenticationHelper.CreateAuthenticatedAdminClientAsync(factory, handle, $"{handle}@example.com")
            : await AuthenticationHelper.CreateAuthenticatedDMClientAsync(factory, handle, $"{handle}@example.com");

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

    // ---- The management lists: questions, the deny dialog and the bulk notice, on both layouts ---------

    // The four questions the management lists ask before a post, word for word.
    private static readonly (string Action, string Sentence)[] ManagementQuestions =
    [
        ("Publish", "Are you sure you want to publish this item?"),
        ("Archive", "Are you sure you want to archive this item?"),
        ("Reopen", "Are you sure you want to reopen this item?"),
        ("Delete", "Are you sure you want to permanently delete this item? This action cannot be undone."),
    ];

    private static List<(string Tag, string Action)> ManagementPostForms(string html) =>
        Regex.Matches(html, @"<form\b[^>]*\baction=""/ShopManagement/(?<action>Publish|Archive|Reopen|Delete)/\d+""[^>]*>")
            .Select(m => (m.Value, m.Groups["action"].Value)).ToList();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShopManagementList_BothLayouts_AskEachQuestionThroughADataAttributeWithTodaysSentence(bool mobile)
    {
        var client = await SeedManagementBoardAsync($"shopr_confirm_{(mobile ? "m" : "d")}", admin: true);

        var html = await GetAsync(client, "/ShopManagement", AgentFor(mobile));

        var forms = ManagementPostForms(html);
        forms.Select(f => f.Action).Distinct().Should().BeEquivalentTo(ManagementQuestions.Select(q => q.Action),
            "an administrator's board shows every one of the four posting actions");
        foreach (var (tag, action) in forms)
        {
            var sentence = ManagementQuestions.Single(q => q.Action == action).Sentence;
            var message = Regex.Match(tag, @"data-confirm-message=""(?<m>[^""]*)""");
            message.Success.Should().BeTrue($"the {action} form asks its question through a data attribute");
            WebUtility.HtmlDecode(message.Groups["m"].Value).Should().Be(sentence);
        }
        Regex.IsMatch(html, @"\sonsubmit\s*=").Should().BeFalse("no handler is written into the markup");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShopManagementList_BothLayouts_DenyTriggersCarryTheNameAndTheUrlAndTheDialogPostsWithAToken(bool mobile)
    {
        var client = await SeedManagementBoardAsync($"shopr_deny_{(mobile ? "m" : "d")}");

        var html = await GetAsync(client, "/ShopManagement", AgentFor(mobile));

        var triggers = Regex.Matches(html, @"<button\b[^>]*\bdata-bs-target=""#denyModal""[^>]*>").Select(m => m.Value).ToList();
        triggers.Should().NotBeEmpty("a draft can be denied from the list");
        foreach (var trigger in triggers)
        {
            trigger.Should().Contain("data-bs-toggle=\"modal\"");
            trigger.Should().MatchRegex(@"data-deny-url=""/ShopManagement/Deny/\d+""");
            trigger.Should().MatchRegex(@"data-deny-name=""[^""]+""");
            trigger.Should().NotMatchRegex(@"\sonclick\s*=", "the dialog is loaded by the module, not by a handler");
        }
        var dialog = html[html.IndexOf("id=\"denyModal\"", StringComparison.Ordinal)..];
        dialog = dialog[..dialog.IndexOf("</form>", StringComparison.Ordinal)];
        dialog.Should().Contain("__RequestVerificationToken", "the deny form posts with an antiforgery token");
        dialog.Should().Contain("method=\"post\"");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShopManagementList_BothLayouts_LoadTheConfirmAndManagementModulesAndHoldNoInlineCode(bool mobile)
    {
        var client = await SeedManagementBoardAsync($"shopr_mods_{(mobile ? "m" : "d")}");

        var html = await GetAsync(client, "/ShopManagement", AgentFor(mobile));

        Regex.IsMatch(html, @"<script\b[^>]*\btype=""module""[^>]*\bsrc=""/js/confirm-submit\.js(?:\?[^""]*)?""").Should().BeTrue();
        Regex.IsMatch(html, @"<script\b[^>]*\btype=""module""[^>]*\bsrc=""/js/shop-management\.js(?:\?[^""]*)?""").Should().BeTrue();
        var main = html[html.IndexOf("<main", StringComparison.Ordinal)..];
        main.Should().NotMatchRegex(@"\son[a-z]+\s*=", "no event handler is written into the markup");
        Regex.Matches(main, @"<script\b(?![^>]*\bsrc\s*=)[^>]*>").Should().BeEmpty("the page holds no inline script");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShopManagementList_BulkActions_DesktopBindsThreeButtonsByDataAttributeAndPhoneHasNoTrigger(bool mobile)
    {
        var client = await SeedManagementBoardAsync($"shopr_bulk_{(mobile ? "m" : "d")}");

        var html = await GetAsync(client, "/ShopManagement", AgentFor(mobile));

        var actions = Regex.Matches(html, @"data-bulk-action=""(?<a>[a-z]+)""").Select(m => m.Groups["a"].Value).ToList();
        actions.Should().Equal("discontinue", "activate", "restock");
        if (mobile)
            html.Should().NotContain("data-bs-target=\"#bulkActionsModal\"", "the phone list has no Bulk Actions trigger");
        else
            html.Should().Contain("data-bs-target=\"#bulkActionsModal\"");
        html.Should().Contain("Note: Bulk actions will be available in a future update.");
    }

    // The recorded answers on the management list: whether the phone heading is shown, which button role the empty
    // state's action takes, and whether the Deny dialog's dismiss button is renamed. Changing an answer means changing
    // the constant and the markup together.
    private static readonly bool ManagementPhoneHeadingVisible = false;
    private static readonly string ManagementEmptyActionRole = "btn-secondary";
    private static readonly bool DenyDismissRenamed = false;

    private static IEnumerable<string> ControlsLabelled(string html, string label) =>
        Regex.Matches(html, @"<(?:a|button)\b[^>]*\baria-label=""" + Regex.Escape(label) + @"""[^>]*>").Select(m => m.Value);

    [Fact]
    public async Task ShopManagementList_Desktop_ShowsThreeAsideSectionsWithScrollingTablesAndNoRowTints()
    {
        var client = await SeedManagementBoardAsync("shopr_sections");

        var html = await GetAsync(client, "/ShopManagement", DesktopUserAgent);

        var sections = Regex.Matches(html, @"<section class=""card modern-card modern-card--aside mgmt-section""[^>]*>").Count;
        sections.Should().Be(3, "the review, own and other items each sit in an aside card");
        foreach (var title in new[] { "Items Awaiting Review", "My Items", "All Other Published Items" })
            Regex.IsMatch(html, @"<h2\b[^>]*>\s*<i\b[^>]*></i>" + Regex.Escape(title) + "</h2>").Should().BeTrue($"{title} is an h2 card title");
        Regex.Matches(html, @"<div class=""table-responsive"">\s*<table class=""table ").Count.Should().Be(3, "each table scrolls inside its card");
        html.Should().Contain("(1 items need DM votes)").And.Contain("(4 items)").And.Contain("(1 items from other DMs)");
        html.Should().NotMatchRegex(@"table-(warning|danger)", "the badge's word and icon carry the status, not a row tint");
        html.Should().NotContain("btn-action").And.NotContain("btn-outline");
    }

    [Fact]
    public async Task ShopManagementList_Desktop_HeaderActionsTakeTheirRoles()
    {
        var client = await SeedManagementBoardAsync("shopr_header");

        var html = await GetAsync(client, "/ShopManagement", DesktopUserAgent);

        Regex.IsMatch(html, @"<a href=""/ShopManagement/Create"" class=""btn btn-primary"">\s*<i\b[^>]*></i>Add New Item").Should().BeTrue("Add New Item is the page's one primary");
        Regex.IsMatch(html, @"<a href=""/Shop"" class=""btn btn-secondary"">\s*<i\b[^>]*></i>View Shop").Should().BeTrue();
        Regex.IsMatch(html, @"<button type=""button"" class=""btn btn-secondary""[^>]*data-bs-target=""#bulkActionsModal""[^>]*>\s*<i\b[^>]*></i>Bulk Actions").Should().BeTrue();
        var main = html[html.IndexOf("<main", StringComparison.Ordinal)..];
        Regex.Matches(main, @"class=""[^""]*\bbtn-primary\b").Count.Should().Be(1, "no other control on the page is primary");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShopManagementList_BothLayouts_RowActionsAreNamedAndFilledByRole(bool mobile)
    {
        var client = await SeedManagementBoardAsync($"shopr_roles_{(mobile ? "m" : "d")}", admin: true);

        var html = await GetAsync(client, "/ShopManagement", AgentFor(mobile));

        var roles = new (string Label, string Role)[]
        {
            ("View item", "btn-info"), ("Edit item", "btn-warning"), ("Publish item", "btn-success"), ("Reopen item", "btn-success"),
            ("Archive item", "btn-secondary"), ("Deny item", "btn-danger"), ("Delete item", "btn-danger"),
        };
        foreach (var (label, role) in roles)
        {
            var controls = ControlsLabelled(html, label).ToList();
            controls.Should().NotBeEmpty($"the list offers {label}");
            controls.Should().OnlyContain(c => c.Contains(role), $"{label} is a {role} button");
            controls.Should().OnlyContain(c => c.Contains("title=\""), "an icon-only control keeps its title");
        }
        if (!mobile)
            ControlsLabelled(html, "Edit &amp; Resubmit item").Should().NotBeEmpty("a denied item offers Edit & Resubmit as today")
                .And.OnlyContain(c => c.Contains("btn-warning"));
        html.Should().NotMatchRegex(@"class=""btn btn-(?:primary|outline)[^""]*""[^>]*aria-label=""Edit item""");
    }

    [Fact]
    public async Task ShopManagementList_Desktop_DeniedRowShowsTheReasonAndALocalTimeDate()
    {
        var client = await SeedManagementBoardAsync("shopr_denial");
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
            var denied = await context.ShopItems.IgnoreQueryFilters().SingleAsync(i => i.Name == "Own Denied", TestContext.Current.CancellationToken);
            denied.DenialReason = "The damage is far above the price band.";
            denied.DeniedAt = new DateTime(2026, 11, 12, 10, 15, 0, DateTimeKind.Utc);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var html = await GetAsync(client, "/ShopManagement", DesktopUserAgent);

        Regex.IsMatch(html, @"<div class=""mgmt-denial"">[\s\S]*?<strong>Denial Reason:</strong> The damage is far above the price band\.[\s\S]*?Denied on <time class=""local-time""")
            .Should().BeTrue("the reason keeps its words and the date goes through the local-time helper");
    }

    [Fact]
    public async Task ShopManagementList_Phone_ShowsSlipsWithH2NamesChipsAndBadgesAndAHiddenHeading()
    {
        var client = await SeedManagementBoardAsync("shopr_slips");

        var html = await GetAsync(client, "/ShopManagement", MobileUserAgent);

        var heading = Regex.Match(html, @"<h1 class=""([^""]*)"">Shop Management Dashboard</h1>");
        heading.Success.Should().BeTrue("the phone page has an h1");
        heading.Groups[1].Value.Contains("visually-hidden").Should().Be(!ManagementPhoneHeadingVisible);
        Regex.Matches(html, @"<li class=""mgmt-slip"">").Count.Should().Be(6, "one merged list: four own items, one to review, one other");
        Regex.Matches(html, @"<h2 class=""mgmt-slip__name"">").Count.Should().Be(6);
        html.Should().Contain("Own Published").And.Contain("Review Me");
        RarityChips(html).Should().NotBeEmpty();
        HasStatusBadge(html, "bg-success", "fa-check-circle", "Available").Should().BeTrue();
        html.Should().NotContain("data-bs-target=\"#bulkActionsModal\"").And.NotContain("table-responsive");
        Regex.IsMatch(html, @"<a href=""/ShopManagement/Create"" class=""btn btn-primary"">").Should().BeTrue();
        Regex.IsMatch(html, @"<a href=""/Shop"" class=""btn btn-secondary"">").Should().BeTrue();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShopManagementList_BothLayouts_ABoardWithNoItemsShowsOneEmptyStateWithTheAnsweredButton(bool mobile)
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.ActiveGroupId = 1;
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedDMClientAsync(
            factory, $"shopr_empty_{(mobile ? "m" : "d")}", $"shopr_empty_{(mobile ? "m" : "d")}@example.com");

        var html = await GetAsync(client, "/ShopManagement", AgentFor(mobile));

        html.Should().Contain("No Items Yet").And.Contain("You haven't created any shop items yet. Start by adding your first item!");
        Regex.IsMatch(html, @"class=""btn " + ManagementEmptyActionRole + @"""[^>]*>\s*<i\b[^>]*></i>Create First Item").Should().BeTrue();
        html.Should().NotContain("Items Awaiting Review", "the review section is absent when nothing needs review");
        if (!mobile)
            html.Should().Contain("No Other Items");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShopManagementList_BothLayouts_DenyDialogIsTheOpaqueFoundationModalWithItsWordsAndMarker(bool mobile)
    {
        var client = await SeedManagementBoardAsync($"shopr_dlg_{(mobile ? "m" : "d")}");

        var html = await GetAsync(client, "/ShopManagement", AgentFor(mobile));

        var start = html.IndexOf("id=\"denyModal\"", StringComparison.Ordinal);
        var dialog = html[start..html.IndexOf("</form>", start, StringComparison.Ordinal)];
        dialog.Should().NotContain("bg-dark").And.NotContain("btn-close-white").And.NotContain("style=");
        dialog.Should().MatchRegex(@"<h2 class=""modal-title""[^>]*>\s*<i\b[^>]*></i>Deny Item\s*</h2>");
        dialog.Should().Contain("You are about to deny: <strong data-deny-item-name></strong>");
        dialog.Should().Contain("<div class=\"alert alert-warning\">").And.Contain("This item will be marked as denied and the creator will see your reason.");
        dialog.Should().MatchRegex(@"Reason for Denial <span class=""text-danger"" aria-hidden=""true"">\*</span><span class=""visually-hidden""> \(required\)</span>");
        dialog.Should().MatchRegex(@"<textarea\b[^>]*name=""denialReason""[^>]*\brequired\b");
        dialog.Should().Contain(DenyDismissRenamed ? ">Keep Item Pending</button>" : ">Cancel</button>");
        dialog.Should().NotContain(DenyDismissRenamed ? ">Cancel</button>" : "Keep Item Pending");
        dialog.Should().MatchRegex(@"<button type=""submit"" class=""btn btn-danger"">\s*<i\b[^>]*></i>Deny Item");
        dialog.Should().Contain("aria-label=\"Close\"");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShopManagementList_BothLayouts_CarryTheThemeLinkTheirSheetsAndHoldNoInlineStyle(bool mobile)
    {
        var client = await SeedManagementBoardAsync($"shopr_mgsheets_{(mobile ? "m" : "d")}");

        var html = await GetAsync(client, "/ShopManagement", AgentFor(mobile));

        HtmlOpeningTag(html).Should().Contain("data-theme=\"shop\"");
        Regex.IsMatch(html, @"<link\b[^>]*\bhref=""/css/shop-pages\.css(?:\?[^""]*)?""").Should().BeTrue();
        Regex.IsMatch(html, @"<link\b[^>]*\bhref=""/css/shop-pages\.mobile\.css(?:\?[^""]*)?""").Should().Be(mobile);
        html.Should().NotContain("shop-management-index.mobile.css");
        var main = html[html.IndexOf("<main", StringComparison.Ordinal)..];
        main.Should().NotMatchRegex(@"\sstyle\s*=", "no style attribute is written into the list");
        main.Should().NotContain("<style");
    }

    // ---- Item details: one body for the page and the dialog, on both layouts -------------------------

    // The recorded answer on whether a Denied or Archived item's details say it cannot be bought. Changing the answer
    // means changing this constant and the partial together.
    private static readonly bool DeniedArchivedNoticeShown = true;

    private async Task UpdateItemAsync(int itemId, Action<ShopItemEntity> change)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
        var tracked = await context.ShopItems.IgnoreQueryFilters().SingleAsync(i => i.Id == itemId, TestContext.Current.CancellationToken);
        change(tracked);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    // The part of a full page that holds the details body, from its wrapper to the end of the main landmark.
    private static string DetailsBodyOf(string html)
    {
        var start = html.IndexOf("class=\"item-details-content\"", StringComparison.Ordinal);
        start.Should().BeGreaterThan(-1, "the page holds the details wrapper");
        var end = html.IndexOf("</main>", start, StringComparison.Ordinal);
        return html[start..(end < 0 ? html.Length : end)];
    }

    private static readonly Regex EmblemTile = new(
        @"<div class=""emblem emblem--(?<key>[a-z]+)"">\s*<i class=""fas (?<icon>fa-[a-z-]+)"" aria-hidden=""true""></i>\s*</div>", RegexOptions.Compiled);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShopDetailsPage_BothLayouts_HoldEverySectionTheDialogHolds(bool mobile)
    {
        var (client, _, itemId) = await SeedMemberAndItemAsync($"shopr_sections_{(mobile ? "m" : "d")}", "Section Lantern");
        await UpdateItemAsync(itemId, i =>
        {
            i.ReferenceUrl = "https://www.dndbeyond.com/magic-items/lantern";
            i.Quantity = 5;
        });

        var html = await GetAsync(client, $"/Shop/Details/{itemId}", AgentFor(mobile));

        html.Should().Contain("data-sell-form", "Sell to Shop is on the phone page as well as the desktop page");
        html.Should().Contain("Availability");
        html.Should().Contain("Item Properties");
        html.Should().Contain("Description");
        html.Should().Contain("View on D&amp;D Beyond");
        html.Should().MatchRegex(@"<input\b[^>]*\bid=""quantity""[^>]*\bmax=""5""", "the purchase quantity is capped by the stock");
        html.Should().Contain("Max: 5 available");
        Regex.IsMatch(html, @"<link\b[^>]*\bhref=""/css/shop-pages\.css(?:\?[^""]*)?""").Should().BeTrue();
        Regex.IsMatch(html, @"<link\b[^>]*\bhref=""/css/shop-pages\.mobile\.css(?:\?[^""]*)?""").Should().Be(mobile,
            "only the phone layout links the phone sheet");
        html.Should().NotContain("shop-details", "the old phone sheet and its card are gone");
        (html.Contains("mobile-layout", StringComparison.Ordinal)).Should().Be(mobile);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShopDetailsPage_UnlimitedStock_SaysSoNextToTheQuantity(bool mobile)
    {
        var (client, _, itemId) = await SeedMemberAndItemAsync($"shopr_unlimited_{(mobile ? "m" : "d")}");
        await UpdateItemAsync(itemId, i => i.Quantity = -1);

        var html = await GetAsync(client, $"/Shop/Details/{itemId}", AgentFor(mobile));

        html.Should().Contain("Unlimited stock available");
        html.Should().Contain("Unlimited Stock");
        html.Should().MatchRegex(@"<input\b[^>]*\bid=""quantity""[^>]*\bmax=""99""");
    }

    [Theory]
    [InlineData(false, ItemStatus.Draft, "This item is currently under review by the Dungeon Masters and is not yet available for purchase.")]
    [InlineData(true, ItemStatus.Draft, "This item is currently under review by the Dungeon Masters and is not yet available for purchase.")]
    [InlineData(false, ItemStatus.Draft, "This item is currently under review and not available for purchase.")]
    [InlineData(true, ItemStatus.Draft, "This item is currently under review and not available for purchase.")]
    public async Task ShopDetailsPage_DraftItem_ShowsBothReviewSentencesOnBothLayouts(bool mobile, ItemStatus status, string sentence)
    {
        var (client, _, itemId) = await SeedMemberAndItemAsync($"shopr_draft_{(mobile ? "m" : "d")}_{sentence.Length}");
        await SetStatusAsync(itemId, status);

        var html = await GetAsync(client, $"/Shop/Details/{itemId}", AgentFor(mobile));

        html.Should().Contain(sentence);
        html.Should().NotContain("data-sell-form", "a draft cannot be sold back");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShopDetailsPage_SoldOutItem_SaysOutOfStockOnBothLayouts(bool mobile)
    {
        var (client, _, itemId) = await SeedMemberAndItemAsync($"shopr_soldout_{(mobile ? "m" : "d")}");
        await UpdateItemAsync(itemId, i => i.Quantity = 0);

        var html = await GetAsync(client, $"/Shop/Details/{itemId}", AgentFor(mobile));

        html.Should().Contain("This item is currently out of stock.");
        html.Should().Contain("Out of Stock");
        html.Should().NotContain("Purchase Item");
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task ShopDetails_PageAndDialog_BothLayouts_ShowOneEmblemTileAndNoStyleAttribute(bool mobile, bool dialog)
    {
        var (client, _, itemId) = await SeedMemberAndItemAsync($"shopr_emblem_{(mobile ? "m" : "d")}_{(dialog ? "q" : "p")}");
        await UpdateItemAsync(itemId, i =>
        {
            i.Rarity = (int)ItemRarity.Rare;
            i.Type = (int)ItemType.MagicItem;
        });

        var html = await GetAsync(client, $"/Shop/Details/{itemId}{(dialog ? "?isModal=true" : "")}", AgentFor(mobile));

        var tiles = EmblemTile.Matches(html);
        tiles.Should().ContainSingle("the details show one emblem tile");
        tiles[0].Groups["key"].Value.Should().Be("rare");
        tiles[0].Groups["icon"].Value.Should().Be("fa-magic");
        html.Should().NotContain("item-image-placeholder", "the grey block is gone");
        Regex.IsMatch(dialog ? html : DetailsBodyOf(html), @"\sstyle\s*=", RegexOptions.IgnoreCase).Should().BeFalse(
            "the details body carries no style attribute");
    }

    [Theory]
    [InlineData(ItemType.Equipment, "fa-hammer")]
    [InlineData(ItemType.MagicItem, "fa-magic")]
    [InlineData(ItemType.QuestItems, "fa-scroll")]
    public async Task ShopDetails_EmblemGlyph_FollowsTheItemType(ItemType type, string icon)
    {
        var (client, _, itemId) = await SeedMemberAndItemAsync($"shopr_glyph_{type}");
        await UpdateItemAsync(itemId, i => i.Type = (int)type);

        var html = await GetAsync(client, $"/Shop/Details/{itemId}", DesktopUserAgent);

        EmblemTile.Match(html).Groups["icon"].Value.Should().Be(icon);
    }

    [Fact]
    public async Task ShopDetails_WindowedItem_RendersWallClockDatesUnderBothLayouts()
    {
        var (client, _, itemId) = await SeedMemberAndItemAsync("shopr_window");
        await UpdateItemAsync(itemId, i =>
        {
            i.AvailableFrom = new DateTime(2026, 11, 5, 0, 0, 0);
            i.AvailableUntil = new DateTime(2026, 11, 19, 0, 0, 0);
        });

        foreach (var mobile in new[] { false, true })
        {
            var html = await GetAsync(client, $"/Shop/Details/{itemId}", AgentFor(mobile));

            Regex.IsMatch(html, @"Available from\s*<time class=""wall-clock""[^>]*>Nov 5</time>\s*to\s*<time class=""wall-clock""[^>]*>Nov 19, 2026</time>")
                .Should().BeTrue("the window reads Nov 5, not Nov 05, and each date is a wall-clock element");
        }
    }

    [Theory]
    [InlineData(true, false, "Available starting")]
    [InlineData(false, true, "Available until")]
    public async Task ShopDetails_HalfOpenWindow_RendersTheMatchingLine(bool hasFrom, bool hasUntil, string wording)
    {
        var (client, _, itemId) = await SeedMemberAndItemAsync($"shopr_half_{wording.Length}");
        await UpdateItemAsync(itemId, i =>
        {
            i.AvailableFrom = hasFrom ? new DateTime(2026, 12, 1) : null;
            i.AvailableUntil = hasUntil ? new DateTime(2020, 1, 2) : null;
        });

        var html = await GetAsync(client, $"/Shop/Details/{itemId}", MobileUserAgent);

        html.Should().MatchRegex(wording + @"\s*<time class=""wall-clock""");
        html.Should().NotContain("Available from");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShopDetails_ItemWithNoDescriptionLinkOrWindow_RendersWithoutThoseParts(bool mobile)
    {
        var (client, _, itemId) = await SeedMemberAndItemAsync($"shopr_bare_{(mobile ? "m" : "d")}");
        await UpdateItemAsync(itemId, i =>
        {
            i.Description = " ";
            i.ReferenceUrl = null;
        });

        var html = await GetAsync(client, $"/Shop/Details/{itemId}", AgentFor(mobile));

        html.Should().NotContain("D&amp;D Beyond");
        html.Should().NotContain("Available from").And.NotContain("Available starting").And.NotContain("Available until");
        html.Should().Contain("Item Properties");
    }

    [Theory]
    [InlineData(false, ItemStatus.Draft)]
    [InlineData(true, ItemStatus.Draft)]
    [InlineData(false, ItemStatus.Denied)]
    [InlineData(true, ItemStatus.Denied)]
    [InlineData(false, ItemStatus.Archived)]
    [InlineData(true, ItemStatus.Archived)]
    public async Task ShopDetails_EditingDungeonMaster_GetsAnOkPageForEveryStatus(bool mobile, ItemStatus status)
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.ActiveGroupId = 1;
        var (client, dm) = await AuthenticationHelper.CreateAuthenticatedDMClientAsync(
            factory, $"shopr_dmview_{(mobile ? "m" : "d")}_{status}", $"shopr_dmview_{(mobile ? "m" : "d")}_{status}@example.com");
        var item = await TestDataHelper.CreateShopItemAsync(factory.Services, dm.Id, "Reviewed Item", 10m, 1, ItemRarity.Rare);
        await SetStatusAsync(item.Id, status);

        foreach (var url in new[] { $"/Shop/Details/{item.Id}", $"/Shop/Details/{item.Id}?isModal=true" })
        {
            var response = await SendAsync(client, HttpMethod.Get, url, AgentFor(mobile));
            response.StatusCode.Should().Be(HttpStatusCode.OK, $"the Edit page's View Details link for a {status} item must not dead-end");
        }
    }

    [Theory]
    [InlineData(false, ItemStatus.Denied)]
    [InlineData(true, ItemStatus.Denied)]
    [InlineData(false, ItemStatus.Archived)]
    [InlineData(true, ItemStatus.Archived)]
    [InlineData(false, ItemStatus.Published)]
    public async Task ShopDetails_NotAvailableNotice_FollowsTheRecordedAnswer(bool mobile, ItemStatus status)
    {
        var (client, _, itemId) = await SeedMemberAndItemAsync($"shopr_notice_{(mobile ? "m" : "d")}_{status}");
        await SetStatusAsync(itemId, status);

        var html = await GetAsync(client, $"/Shop/Details/{itemId}", AgentFor(mobile));

        const string sentence = "This item is not currently available for purchase.";
        if (status == ItemStatus.Published)
            html.Should().NotContain(sentence);
        else if (DeniedArchivedNoticeShown)
            html.Should().Contain(sentence);
        else
            html.Should().NotContain(sentence);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShopDetails_HeadingLevels_PageIsH1WithH2SectionsAndDialogIsH3(bool mobile)
    {
        var (client, _, itemId) = await SeedMemberAndItemAsync($"shopr_levels_{(mobile ? "m" : "d")}", "Level Lantern");

        var page = DetailsBodyOf(await GetAsync(client, $"/Shop/Details/{itemId}", AgentFor(mobile)));
        var dialog = await GetAsync(client, $"/Shop/Details/{itemId}?isModal=true", AgentFor(mobile));

        Regex.IsMatch(page, @"<h1\b[^>]*>Level Lantern</h1>").Should().BeTrue("the page's item name is the h1");
        foreach (var title in new[] { "Availability", "Item Properties", "Description", "Sell to Shop" })
            Regex.IsMatch(page, @"<h2\b[^>]*>\s*<i\b[^>]*></i>" + title + @"</h2>").Should().BeTrue($"{title} is an h2 on the page");
        Regex.IsMatch(page, @"<h[3-6]\b").Should().BeFalse("no page title sits at h3 to h6");

        Regex.IsMatch(dialog, @"<h3\b[^>]*>Level Lantern</h3>").Should().BeTrue("the dialog's item name is an h3 under its h2");
        foreach (var title in new[] { "Availability", "Item Properties", "Description", "Sell to Shop" })
            Regex.IsMatch(dialog, @"<h3\b[^>]*>\s*<i\b[^>]*></i>" + title + @"</h3>").Should().BeTrue($"{title} is an h3 in the dialog");
        Regex.IsMatch(dialog, @"<h[1256]\b").Should().BeFalse("the dialog holds no h1, h2, h5 or h6");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShopDetails_ButtonsAndLink_CarryTheirRoles(bool mobile)
    {
        var (client, _, itemId) = await SeedMemberAndItemAsync($"shopr_roles_{(mobile ? "m" : "d")}");
        await UpdateItemAsync(itemId, i => i.ReferenceUrl = "https://www.dndbeyond.com/magic-items/lantern");

        var html = await GetAsync(client, $"/Shop/Details/{itemId}", AgentFor(mobile));

        Regex.IsMatch(html, @"<a\b[^>]*\bhref=""https://www\.dndbeyond\.com/magic-items/lantern""[^>]*\btarget=""_blank""[^>]*\brel=""noopener noreferrer""[^>]*\bclass=""btn btn-info""")
            .Should().BeTrue("the reference link is a filled info button that opens a new tab without handing over the opener");
        Regex.IsMatch(html, @"<button\b[^>]*\bclass=""btn btn-primary""[^>]*>\s*<i\b[^>]*></i>Purchase Item").Should().BeTrue();
        Regex.IsMatch(html, @"<button\b[^>]*\bclass=""btn btn-warning""[^>]*>\s*<i\b[^>]*></i>Sell to Shop").Should().BeTrue();
        Regex.IsMatch(html, @"<a\b[^>]*\bclass=""btn btn-secondary""[^>]*>\s*<i\b[^>]*></i>Back to Shop").Should().BeTrue();
        html.Should().NotContain("btn-outline");
    }

    [Fact]
    public async Task ShopDetails_PhoneDialog_KeepsItsBackToShopLink()
    {
        var (client, _, itemId) = await SeedMemberAndItemAsync("shopr_phoneback");

        var html = await GetAsync(client, $"/Shop/Details/{itemId}?isModal=true", MobileUserAgent);

        html.Should().Contain("Back to Shop", "the phone dialog keeps the link it has always had");
    }

    // ---- Desktop Edit: the details link, the status block, the shared form module and the form contract --------

    // The recorded answer on whether the back control reads Back to Shop Management. Changing the answer means
    // changing this constant and the view together.
    private static readonly bool BackToShopManagementLabel = false;

    private async Task<(HttpClient Client, int ItemId)> SeedEditablePageAsync(string handle, ItemStatus status)
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.ActiveGroupId = 1;
        var (client, dm) = await AuthenticationHelper.CreateAuthenticatedDMClientAsync(factory, handle, $"{handle}@example.com");
        var item = await TestDataHelper.CreateShopItemAsync(factory.Services, dm.Id, "Edit Subject", 10m, 1, ItemRarity.Rare);
        await SetStatusAsync(item.Id, status);
        return (client, item.Id);
    }

    [Theory]
    [InlineData(ItemStatus.Draft)]
    [InlineData(ItemStatus.Denied)]
    [InlineData(ItemStatus.Archived)]
    public async Task ShopManagementEdit_Desktop_ViewDetailsPointsAtTheShopDetailsPage(ItemStatus status)
    {
        var (client, itemId) = await SeedEditablePageAsync($"shopr_editlink_{status}", status);

        var html = await GetAsync(client, $"/ShopManagement/Edit/{itemId}", DesktopUserAgent);

        Regex.IsMatch(html, $@"<a\b[^>]*\bhref=""/Shop/Details/{itemId}""[^>]*>\s*<i\b[^>]*></i>View Details\s*</a>")
            .Should().BeTrue("the control stays and points at the page the management lists use");
        html.Should().NotContain("/ShopManagement/Details/", "that target does not exist");

        var details = await SendAsync(client, HttpMethod.Get, $"/Shop/Details/{itemId}", DesktopUserAgent);
        details.StatusCode.Should().Be(HttpStatusCode.OK, $"the link must not dead-end for a {status} item");
    }

    [Fact]
    public async Task ShopManagementEdit_Desktop_WiresThePricingThroughDataAttributesAndTheSharedModule()
    {
        var (client, itemId) = await SeedEditablePageAsync("shopr_editmodule", ItemStatus.Draft);

        var html = await GetAsync(client, $"/ShopManagement/Edit/{itemId}", DesktopUserAgent);

        Regex.IsMatch(html, @"<form\b[^>]*\bdata-shop-item-form\b[^>]*>").Should().BeTrue("the module binds the item form by its data attribute");
        Regex.IsMatch(html, @"<select\b[^>]*\bname=""Rarity""").Should().BeTrue();
        Regex.IsMatch(html, @"<button\b[^>]*\bid=""randomPriceBtn""[^>]*\bdata-random-price\b").Should().BeTrue();
        Regex.IsMatch(html, @"<button\b[^>]*\bid=""calculatedPriceBtn""[^>]*\bdata-calculated-price\b").Should().BeTrue();
        Regex.IsMatch(html, @"<input\b[^>]*\bid=""enableAvailabilityWindow""[^>]*\bdata-availability-toggle\b").Should().BeTrue();
        Regex.IsMatch(html, @"<script\b[^>]*\btype=""module""[^>]*\bsrc=""/js/shop-item-form\.js(?:\?[^""]*)?""").Should().BeTrue(
            "the module is a file loaded by the page, not an inline script");
        Regex.IsMatch(html, @"<div\b[^>]*\bid=""price-suggestion""[^>]*\baria-live=""polite""[^>]*>\s*Select a rarity to see pricing suggestions\.\s*</div>")
            .Should().BeTrue("the hint starts as the short line and is announced politely when the module rewrites it");
        Regex.IsMatch(html, @"class=""form-text shop-form__hint""").Should().BeTrue("the hint is help text in the form sheet's hint layout");
    }

    [Fact]
    public async Task ShopManagementEdit_Desktop_StatusBlockIsAnInfoAlertWithTheSharedBadge()
    {
        var (client, itemId) = await SeedEditablePageAsync("shopr_editstatus", ItemStatus.Draft);

        var html = await GetAsync(client, $"/ShopManagement/Edit/{itemId}", DesktopUserAgent);

        var block = Regex.Match(html, @"<div class=""alert alert-info shop-form__status"">[\s\S]*?Item ID: #\d+</span>\s*</div>");
        block.Success.Should().BeTrue("the status block is an info alert");
        block.Value.Should().MatchRegex(@"<h2>\s*<i\b[^>]*></i>Current Status\s*</h2>");
        HasStatusBadge(block.Value, "bg-warning", "fa-hourglass-half", "Pending Review").Should().BeTrue();
        block.Value.Should().Contain("This item is awaiting DM approval").And.Contain($"Item ID: #{itemId}");
    }

    [Fact]
    public async Task ShopManagementEdit_Desktop_RequiredFieldsCarryTheMarkerAndItsHiddenWord()
    {
        var (client, itemId) = await SeedEditablePageAsync("shopr_editmarks", ItemStatus.Published);

        var html = await GetAsync(client, $"/ShopManagement/Edit/{itemId}", DesktopUserAgent);

        foreach (var label in new[] { "Item Name", "Item Type", "Description", "Rarity", "Price \\(Gold Pieces\\)" })
            Regex.IsMatch(html, @"<label\b[^>]*>\s*" + label + @"\s*<span class=""text-danger"" aria-hidden=""true"">\*</span><span class=""visually-hidden""> \(required\)</span>")
                .Should().BeTrue($"{label} is required and says so to a screen reader too");
        html.Should().Contain("form-page", "the page is a form page, so the marker and the messages sit on the overlay chip");
        Regex.IsMatch(html, @"<span\b(?=[^>]*data-valmsg-for=""Name"")[^>]*\bclass=""field-validation-valid""")
            .Should().BeTrue("the message spans come from the validation tag helper, which marks a field in error with field-validation-error and leaves a valid one unstyled");
    }

    [Theory]
    [InlineData(ItemStatus.Draft, true)]
    [InlineData(ItemStatus.Published, false)]
    public async Task ShopManagementEdit_Desktop_DraftReviewNoticeShowsOnlyForADraft(ItemStatus status, bool shown)
    {
        var (client, itemId) = await SeedEditablePageAsync($"shopr_editnotice_{status}", status);

        var html = await GetAsync(client, $"/ShopManagement/Edit/{itemId}", DesktopUserAgent);

        html.Contains("Item Under Review", StringComparison.Ordinal).Should().Be(shown);
        html.Contains("Changes to rarity may require re-approval", StringComparison.Ordinal).Should().Be(shown);
    }

    // The recorded answer on whether desktop Edit also gets the Preview button and dialog. Changing the answer means
    // changing this constant, the view and the facts below together.
    private static readonly bool PreviewOnEdit = false;

    [Theory]
    [InlineData(ItemStatus.Draft)]
    [InlineData(ItemStatus.Published)]
    public async Task ShopManagementEdit_Desktop_PreviewAppearsOnlyWhenTheRecordedAnswerIsYes(ItemStatus status)
    {
        var (client, itemId) = await SeedEditablePageAsync($"shopr_editpreview_{status}", status);

        var html = await GetAsync(client, $"/ShopManagement/Edit/{itemId}", DesktopUserAgent);

        if (PreviewOnEdit)
        {
            html.Should().Contain("id=\"itemPreviewModal\"").And.Contain("data-bs-target=\"#itemPreviewModal\"");
            Regex.IsMatch(html, @"<script\b[^>]*\btype=""module""[^>]*\bsrc=""/js/shop-item-preview\.js").Should().BeTrue();
        }
        else
        {
            html.Should().NotContain("itemPreviewModal").And.NotContain("shop-item-preview").And.NotContain("data-preview");
            html.Should().NotContain("Preview</button>", "desktop Edit has no Preview button");
        }
    }

    [Fact]
    public async Task ShopManagementEdit_Desktop_ActionRowFollowsTheFormContractAndTheRecordedLabel()
    {
        var (client, itemId) = await SeedEditablePageAsync("shopr_editactions", ItemStatus.Published);

        var html = await GetAsync(client, $"/ShopManagement/Edit/{itemId}", DesktopUserAgent);

        var row = Regex.Match(html, @"<div class=""form-actions"">[\s\S]*?Save Changes\s*</button>");
        row.Success.Should().BeTrue("the action row is the shared form-actions row");
        var back = BackToShopManagementLabel ? "Back to Shop Management" : "Back to Management";
        Regex.IsMatch(row.Value, @"<a href=""/ShopManagement"" class=""btn btn-secondary"">\s*<i\b[^>]*></i>" + back).Should().BeTrue();
        Regex.IsMatch(row.Value, @"class=""btn btn-info"">\s*<i\b[^>]*></i>View Details").Should().BeTrue("View Details is a filled info button");
        Regex.IsMatch(row.Value, @"class=""btn btn-primary"">\s*<i\b[^>]*></i>Save Changes").Should().BeTrue();
        row.Value.Should().NotContain("btn-outline");
    }

    [Fact]
    public async Task ShopManagementEdit_Desktop_CarriesTheThemeLinksItsSheetsAndHoldsNoInlineCode()
    {
        var (client, itemId) = await SeedEditablePageAsync("shopr_editsheets", ItemStatus.Draft);

        var html = await GetAsync(client, $"/ShopManagement/Edit/{itemId}", DesktopUserAgent);

        HtmlOpeningTag(html).Should().Contain("data-theme=\"shop\"");
        Regex.IsMatch(html, @"<link\b[^>]*\bhref=""/css/shop-pages\.css(?:\?[^""]*)?""").Should().BeTrue();
        Regex.IsMatch(html, @"<link\b[^>]*\bhref=""/css/shop-form\.css(?:\?[^""]*)?""").Should().BeTrue();
        var main = html[html.IndexOf("<main", StringComparison.Ordinal)..];
        main.Should().NotContain("<style", "the page's rules live in sheets");
        main.Should().NotMatchRegex(@"\son(?:click|change|submit)\s*=", "no event handler is written into the markup");
        Regex.Matches(main, @"<script\b(?![^>]*\bsrc\s*=)[^>]*>").Should().BeEmpty("the page holds no inline script");
        main.Should().NotContain("legendary-glow");
    }

    // ---- Create: the form contract and the shared module, on both layouts ------------------------------------

    private async Task<HttpClient> SeedCreatePageAsync(string handle)
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.ActiveGroupId = 1;
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedDMClientAsync(factory, handle, $"{handle}@example.com");
        return client;
    }

    private static string RequiredLabel(string text) =>
        @"<label\b[^>]*>\s*" + text + @"\s*<span class=""text-danger"" aria-hidden=""true"">\*</span><span class=""visually-hidden""> \(required\)</span>";

    [Fact]
    public async Task ShopManagementCreate_Desktop_WiresThePricingThroughDataAttributesAndTheSharedModule()
    {
        var client = await SeedCreatePageAsync("shopr_createmodule");

        var html = await GetAsync(client, "/ShopManagement/Create", DesktopUserAgent);

        Regex.IsMatch(html, @"<form\b[^>]*\bdata-shop-item-form\b[^>]*>").Should().BeTrue("the module binds the item form by its data attribute");
        Regex.IsMatch(html, @"<button\b[^>]*\bid=""randomPriceBtn""[^>]*\bdata-random-price\b").Should().BeTrue();
        Regex.IsMatch(html, @"<button\b[^>]*\bid=""calculatedPriceBtn""[^>]*\bdata-calculated-price\b").Should().BeTrue();
        Regex.IsMatch(html, @"<input\b[^>]*\bid=""enableAvailabilityWindow""[^>]*\bdata-availability-toggle\b").Should().BeTrue();
        Regex.IsMatch(html, @"<script\b[^>]*\btype=""module""[^>]*\bsrc=""/js/shop-item-form\.js(?:\?[^""]*)?""").Should().BeTrue(
            "the module is a file loaded by the page, not an inline script");
        Regex.IsMatch(html, @"<div\b[^>]*\bid=""price-suggestion""[^>]*\baria-live=""polite""").Should().BeTrue("the hint is announced politely when the module rewrites it");
        Regex.IsMatch(html, @"<button\b[^>]*\bid=""randomPriceBtn""[^>]*\bdisabled\b").Should().BeTrue("the helpers start disabled until a rarity is chosen");
    }

    [Fact]
    public async Task ShopManagementCreate_Desktop_RequiredFieldsCarryTheMarkerAndItsHiddenWord()
    {
        var client = await SeedCreatePageAsync("shopr_createmarks");

        var html = await GetAsync(client, "/ShopManagement/Create", DesktopUserAgent);

        foreach (var label in new[] { "Item Name", "Item Type", "Description", "Rarity", "Price \\(Gold Pieces\\)" })
            Regex.IsMatch(html, RequiredLabel(label)).Should().BeTrue($"{label} is required and says so to a screen reader too");
        html.Should().Contain("form-page");
        Regex.IsMatch(html, @"<span\b(?=[^>]*data-valmsg-for=""Name"")[^>]*\bclass=""field-validation-valid""").Should().BeTrue(
            "the message spans come from the validation tag helper, which adds field-validation-error only on a field in error");
        html.Should().NotMatchRegex(@"<span\b[^>]*data-valmsg-for=""Name""[^>]*class=""text-danger""");
    }

    [Fact]
    public async Task ShopManagementCreate_Desktop_ActionRowHoldsTheExitPreviewAndTheCommit()
    {
        var client = await SeedCreatePageAsync("shopr_createactions");

        var html = await GetAsync(client, "/ShopManagement/Create", DesktopUserAgent);

        var row = Regex.Match(html, @"<div class=""form-actions"">[\s\S]*?Create Item\s*</button>");
        row.Success.Should().BeTrue("the action row is the shared form-actions row");
        var back = BackToShopManagementLabel ? "Back to Shop Management" : "Cancel";
        Regex.IsMatch(row.Value, @"<a href=""/ShopManagement"" class=""btn btn-secondary"">\s*<i\b[^>]*></i>" + back).Should().BeTrue();
        Regex.IsMatch(row.Value, @"<button type=""button"" class=""btn btn-info""[^>]*>\s*<i class=""fas fa-eye me-2""[^>]*></i>Preview").Should().BeTrue(
            "Preview is a filled info button that opens the dialog by data attributes, with no handler in the markup");
        Regex.IsMatch(row.Value, @"class=""btn btn-primary"">\s*<i\b[^>]*></i>Create Item").Should().BeTrue();
        row.Value.Should().NotContain("btn-outline");
    }

    [Fact]
    public async Task ShopManagementCreate_Desktop_PreviewButtonOpensTheDialogByDataAttributes()
    {
        var client = await SeedCreatePageAsync("shopr_previewbutton");

        var html = await GetAsync(client, "/ShopManagement/Create", DesktopUserAgent);

        var button = Regex.Match(html, @"<button\b(?=[^>]*\bdata-bs-target=""#itemPreviewModal"")[^>]*>");
        button.Success.Should().BeTrue("the Preview button names the dialog it opens");
        button.Value.Should().Contain("data-bs-toggle=\"modal\"").And.Contain("type=\"button\"", "Preview never submits the form");
        button.Value.Should().NotMatchRegex(@"\son[a-z]+\s*=", "no event handler is written into the markup");
        Regex.Matches(html, @"data-bs-target=""#itemPreviewModal""").Should().HaveCount(1);
    }

    [Fact]
    public async Task ShopManagementCreate_Desktop_PreviewDialogHoldsTheNoteTheHostTheTemplateAndClose()
    {
        var client = await SeedCreatePageAsync("shopr_previewdialog");

        var html = await GetAsync(client, "/ShopManagement/Create", DesktopUserAgent);

        var dialog = Regex.Match(html, @"<div\b[^>]*\bid=""itemPreviewModal""[^>]*>[\s\S]*?</template>[\s\S]*?Close\s*</button>");
        dialog.Success.Should().BeTrue("the dialog is on the page");
        var opening = Regex.Match(dialog.Value, @"^<div\b[^>]*>").Value;
        opening.Should().Contain("class=\"modal fade\"").And.Contain("aria-labelledby=\"itemPreviewTitle\"");
        dialog.Value.Should().Contain("modal-dialog modal-dialog-centered");
        Regex.IsMatch(dialog.Value, @"<h2\b[^>]*\bid=""itemPreviewTitle""[^>]*>\s*Item Preview\s*</h2>").Should().BeTrue("the title is an h2 that names the dialog");
        dialog.Value.Should().Contain("This is how the card will look in the shop. Nothing is saved until you create the item.");
        Regex.IsMatch(dialog.Value, @"<button\b[^>]*\bdata-bs-dismiss=""modal""[^>]*\baria-label=""Close""").Should().BeTrue("the header Close has a name");
        Regex.IsMatch(dialog.Value, @"<button\b[^>]*\bclass=""btn btn-secondary""[^>]*\bdata-bs-dismiss=""modal""[^>]*>\s*Close\s*</button>").Should().BeTrue("the footer Close is the neutral button");
        Regex.IsMatch(dialog.Value, @"<div\b[^>]*\bdata-preview-host\b[^>]*></div>").Should().BeTrue("the replica host starts empty");
    }

    [Fact]
    public async Task ShopManagementCreate_Desktop_PreviewTemplateHoldsTheNewCardAndNothingLiveOrOldStyled()
    {
        var client = await SeedCreatePageAsync("shopr_previewtemplate");

        var html = await GetAsync(client, "/ShopManagement/Create", DesktopUserAgent);

        var template = Regex.Match(html, @"<template\b[^>]*\bdata-preview-card\b[^>]*>([\s\S]*?)</template>");
        template.Success.Should().BeTrue("the card is cloned from a server-rendered template");
        var card = template.Groups[1].Value;
        Regex.IsMatch(card, @"class=""modern-card shop-card""").Should().BeTrue("the replica carries the new card classes");
        card.Should().NotContain("item-card", "item-card is the hook of the real grid cards, which the replica does not carry");
        foreach (var slot in new[] { "data-preview-chip", "data-preview-name", "data-preview-excerpt", "data-preview-price", "data-preview-stock" })
            card.Should().Contain(slot);
        Regex.IsMatch(card, @"<span\b[^>]*\bclass=""btn btn-primary shop-card__action""[^>]*\baria-hidden=""true""[^>]*>").Should().BeTrue(
            "Purchase is a span that cannot be activated and is hidden from assistive technology");
        card.Should().NotMatchRegex(@"<(?:a|button|form|input)\b", "the replica holds no link, live button or form");
    }

    [Fact]
    public async Task ShopManagementCreate_Desktop_LoadsThePreviewModuleAndKeepsTheOldPreviewRuleGone()
    {
        var client = await SeedCreatePageAsync("shopr_previewmodule");

        var html = await GetAsync(client, "/ShopManagement/Create", DesktopUserAgent);

        Regex.IsMatch(html, @"<script\b[^>]*\btype=""module""[^>]*\bsrc=""/js/shop-item-preview\.js(?:\?[^""]*)?""").Should().BeTrue();
        html.Should().NotContain("id=\"item-preview\"").And.NotContain("#item-preview", "the old dead preview rule stays gone");
        html.Should().NotContain("previewItem");
    }

    [Fact]
    public async Task ShopManagementCreate_Phone_HasNoPreviewButtonOrDialog()
    {
        var client = await SeedCreatePageAsync("shopr_previewphone");

        var html = await GetAsync(client, "/ShopManagement/Create", MobileUserAgent);

        html.Should().NotContain("itemPreviewModal").And.NotContain("shop-item-preview").And.NotContain("data-preview");
    }

    [Fact]
    public async Task ShopManagementCreate_Desktop_CarriesTheThemeLinksItsSheetsAndHoldsNoInlineCode()
    {
        var client = await SeedCreatePageAsync("shopr_createsheets");

        var html = await GetAsync(client, "/ShopManagement/Create", DesktopUserAgent);

        HtmlOpeningTag(html).Should().Contain("data-theme=\"shop\"");
        Regex.IsMatch(html, @"<link\b[^>]*\bhref=""/css/shop-pages\.css(?:\?[^""]*)?""").Should().BeTrue();
        Regex.IsMatch(html, @"<link\b[^>]*\bhref=""/css/shop-form\.css(?:\?[^""]*)?""").Should().BeTrue();
        var main = html[html.IndexOf("<main", StringComparison.Ordinal)..];
        main.Should().NotContain("<style", "the page's rules live in sheets");
        main.Should().NotMatchRegex(@"\son(?:click|change|submit)\s*=", "no event handler is written into the markup");
        Regex.Matches(main, @"<script\b(?![^>]*\bsrc\s*=)[^>]*>").Should().BeEmpty("the page holds no inline script");
        main.Should().NotContain("legendary-glow").And.NotContain("id=\"item-preview\"").And.NotContain("#item-preview");
    }

    // ---- The phone forms: the same contract and module, with no price helpers and no Preview ------------------

    [Theory]
    [InlineData("/ShopManagement/Create")]
    [InlineData("/ShopManagement/Edit/{id}")]
    public async Task ShopManagementForms_Phone_RunOnTheSharedModuleWithNoPriceHelpersAndNoPreview(string route)
    {
        var (client, itemId) = await SeedEditablePageAsync($"shopr_phoneform_{route.Length}", ItemStatus.Draft);

        var html = await GetAsync(client, route.Replace("{id}", itemId.ToString()), MobileUserAgent);

        html.Should().Contain("mobile-layout");
        HtmlOpeningTag(html).Should().Contain("data-theme=\"shop\"");
        Regex.IsMatch(html, @"<form\b[^>]*\bdata-shop-item-form\b[^>]*>").Should().BeTrue();
        Regex.IsMatch(html, @"<link\b[^>]*\bhref=""/css/shop-form\.css(?:\?[^""]*)?""").Should().BeTrue();
        Regex.IsMatch(html, @"<link\b[^>]*\bhref=""/css/shop-pages\.mobile\.css(?:\?[^""]*)?""").Should().BeTrue();
        Regex.IsMatch(html, @"<script\b[^>]*\btype=""module""[^>]*\bsrc=""/js/shop-item-form\.js(?:\?[^""]*)?""").Should().BeTrue();
        html.Should().NotContain("randomPriceBtn").And.NotContain("calculatedPriceBtn", "the phone forms keep no price helper buttons");
        html.Should().NotContain("Preview", "the phone Create has no Preview and the phone Edit never had one");
        html.Should().NotContain("shop-mgmt-").And.NotContain("shop-management-create.mobile").And.NotContain("shop-management-edit.mobile");
        Regex.IsMatch(html, @"<input\b[^>]*\bid=""enableAvailabilityWindow""[^>]*\bdata-availability-toggle\b").Should().BeTrue();
    }

    [Theory]
    [InlineData("/ShopManagement/Create")]
    [InlineData("/ShopManagement/Edit/{id}")]
    public async Task ShopManagementForms_Phone_StartTheHintOnTheShortLineAndMarkTheRequiredFields(string route)
    {
        var (client, itemId) = await SeedEditablePageAsync($"shopr_phonehint_{route.Length}", ItemStatus.Published);

        var html = await GetAsync(client, route.Replace("{id}", itemId.ToString()), MobileUserAgent);

        Regex.IsMatch(html, @"<div\b[^>]*\bid=""price-suggestion""[^>]*\baria-live=""polite""[^>]*>\s*Select a rarity to see pricing suggestions\.\s*</div>")
            .Should().BeTrue("both phone forms start on the same short line");
        foreach (var label in new[] { "Item Name", "Item Type", "Description", "Rarity", "Price \\(Gold Pieces\\)" })
            Regex.IsMatch(html, RequiredLabel(label)).Should().BeTrue($"{label} is required and says so to a screen reader too");
        html.Should().Contain("form-page");
    }

    [Theory]
    [InlineData("/ShopManagement/Create", "Cancel", "Create Item")]
    [InlineData("/ShopManagement/Edit/{id}", "Back to Management", "Save Changes")]
    public async Task ShopManagementForms_Phone_ActionRowHoldsTheRecordedExitAndOneCommit(string route, string exit, string commit)
    {
        var (client, itemId) = await SeedEditablePageAsync($"shopr_phoneactions_{route.Length}", ItemStatus.Published);

        var html = await GetAsync(client, route.Replace("{id}", itemId.ToString()), MobileUserAgent);

        var row = Regex.Match(html, @"<div class=""form-actions[^""]*"">[\s\S]*?" + commit + @"\s*</button>");
        row.Success.Should().BeTrue("the action row is the shared form-actions row");
        if (BackToShopManagementLabel)
            exit = "Back to Shop Management";
        Regex.IsMatch(row.Value, @"<a href=""/ShopManagement"" class=""btn btn-secondary"">\s*<i\b[^>]*></i>" + exit).Should().BeTrue();
        Regex.IsMatch(row.Value, @"class=""btn btn-primary"">\s*<i\b[^>]*></i>" + commit).Should().BeTrue();
        row.Value.Should().NotContain("btn-outline");
    }

    [Fact]
    public async Task ShopManagementEdit_Phone_StatusBlockShowsTheSharedBadgeAndAReadableItemId()
    {
        var (client, itemId) = await SeedEditablePageAsync("shopr_phonestatus", ItemStatus.Draft);

        var html = await GetAsync(client, $"/ShopManagement/Edit/{itemId}", MobileUserAgent);

        var block = Regex.Match(html, @"<div class=""alert alert-info shop-form__status"">[\s\S]*?Item ID: #\d+</span>\s*</div>");
        block.Success.Should().BeTrue("the phone status block is the same info alert as the desktop one");
        HasStatusBadge(block.Value, "bg-warning", "fa-hourglass-half", "Pending Review").Should().BeTrue();
        block.Value.Should().Contain("This item is awaiting DM approval").And.Contain($"Item ID: #{itemId}");
        block.Value.Should().NotContain("text-muted", "the Item ID line takes the alert's own colour, not a faded one");
        Regex.IsMatch(html, @"<h1\b[^>]*>\s*<i\b[^>]*></i>\s*Edit Item\s*</h1>").Should().BeTrue();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShopManagementCreate_BothLayouts_AnInvalidPostShowsEachMessageOnItsFieldAndSavesNothing(bool mobile)
    {
        var client = await SeedCreatePageAsync($"shopr_createerr_{(mobile ? "m" : "d")}");
        var form = await GetAsync(client, "/ShopManagement/Create", AgentFor(mobile));
        var token = Regex.Match(form, @"name=""__RequestVerificationToken""[^>]*\bvalue=""([^""]+)""").Groups[1].Value;
        token.Should().NotBeEmpty("the form posts with an antiforgery token");

        var fields = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["Name"] = "",
            ["Description"] = "",
            ["Type"] = "",
            ["Rarity"] = "",
            ["Price"] = "5",
            ["Quantity"] = "1",
        };
        var response = await SendAsync(client, HttpMethod.Post, "/ShopManagement/Create", AgentFor(mobile), new FormUrlEncodedContent(fields));
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK, "an invalid form is shown again, not redirected");
        Regex.IsMatch(html, @"<span\b(?=[^>]*data-valmsg-for=""Name"")[^>]*\bclass=""field-validation-error""[^>]*>The Item Name field is required\.</span>")
            .Should().BeTrue("the sentence appears on its field in the validation error class");
        Regex.IsMatch(html, @"<input\b(?=[^>]*\bname=""Name"")[^>]*\binput-validation-error").Should().BeTrue("the field in error carries the error class for its danger border");
        Regex.IsMatch(html, @"<span\b(?=[^>]*data-valmsg-for=""Rarity"")[^>]*\bclass=""field-validation-error""").Should().BeTrue();
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
        (await context.ShopItems.IgnoreQueryFilters().CountAsync(TestContext.Current.CancellationToken)).Should().Be(0, "nothing is saved");
    }

    // ---- Shop browse: the item cards and the details dialog (desktop) ----------------------------------

    // The recorded answer on whether the details dialog shows a sentence and a link when its item cannot be loaded.
    // Changing the answer means changing this constant, the view and the module together.
    private static readonly bool ItemDialogShowsLoadError = false;

    // A signed-in member and the given number of Published items named "Browse Item NN".
    private async Task<HttpClient> SeedBrowseBoardAsync(string handle, int count, Action<int, ShopItemEntity>? shape = null)
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.ActiveGroupId = 1;
        var keeper = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, $"{handle}_keeper", $"{handle}_keeper@example.com");
        for (var i = 1; i <= count; i++)
        {
            var item = await TestDataHelper.CreateShopItemAsync(factory.Services, keeper.Id, $"Browse Item {i:00}", 10m + i, 5);
            if (shape != null)
                await UpdateItemAsync(item.Id, entity => shape(i, entity));
        }

        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, handle, $"{handle}@example.com", name: "Shop Render Member");
        return client;
    }

    // The markup of each grid card: from its marker to the next card, the last one stopping where the pager or the
    // merchant begins.
    private static List<string> GridCards(string html)
    {
        var chunks = html.Split("data-item-card").Skip(1).ToList();
        if (chunks.Count == 0)
            return chunks;

        var last = chunks[^1];
        var stops = new[] { "Shop page navigation", "shop-merchant", "id=\"itemDetailsModal\"" }
            .Select(stop => last.IndexOf(stop, StringComparison.Ordinal)).Where(i => i >= 0).ToList();
        chunks[^1] = stops.Count > 0 ? last[..stops.Min()] : last;
        return chunks;
    }

    [Fact]
    public async Task ShopIndex_Desktop_CarriesTheShopThemeOnHtmlNotBody()
    {
        var client = await SeedBrowseBoardAsync("shopr_idxtheme", 1);

        var html = await GetAsync(client, "/Shop", DesktopUserAgent);

        HtmlOpeningTag(html).Should().Contain("data-theme=\"shop\"");
        BodyOpeningTag(html).Should().NotContain("data-theme");
    }

    [Fact]
    public async Task ShopIndex_Desktop_EachCardIsAKeyboardCardWithANameLinkAndAPurchaseButton()
    {
        var client = await SeedBrowseBoardAsync("shopr_idxcard", 3);
        var html = await GetAsync(client, "/Shop", DesktopUserAgent);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
        var ids = await context.ShopItems.IgnoreQueryFilters().OrderBy(i => i.Name).Select(i => i.Id).ToListAsync(TestContext.Current.CancellationToken);

        var cards = GridCards(html);
        cards.Should().HaveCount(3);
        for (var i = 0; i < cards.Count; i++)
        {
            var card = cards[i];
            var id = ids[i];
            card.Should().Contain($"data-item-url=\"/Shop/Details/{id}?isModal=True\"", "the dialog is filled from the address the server wrote");
            Regex.IsMatch(card, @"<h3\b[^>]*>\s*<a\b(?=[^>]*\bclass=""row-nav-link"")(?=[^>]*\bhref=""/Shop/Details/" + id + @"""" + @")(?=[^>]*\bdata-bs-toggle=""modal"")(?=[^>]*\bdata-bs-target=""#itemDetailsModal"")[^>]*>\s*Browse Item \d\d\s*</a>\s*</h3>")
                .Should().BeTrue("the name is a real link to the details page that also opens the dialog");
            Regex.IsMatch(card, @"<button\b(?=[^>]*\bclass=""btn btn-primary w-100 shop-card__action"")(?=[^>]*\bdata-bs-toggle=""modal"")(?=[^>]*\bdata-bs-target=""#itemDetailsModal"")(?=[^>]*\bdata-item-url="")[^>]*>\s*<i\b[^>]*></i>Purchase\s*</button>")
                .Should().BeTrue("Purchase is a filled primary button that opens the same dialog");
            card.Should().NotMatchRegex(@"\son[a-z]+\s*=", "no inline handler sits on a card");
            card.Should().NotContain("stopPropagation");
        }

        Regex.Matches(html, @"<div\b[^>]*class=""modern-card modern-card--interactive shop-card item-card""[^>]*\bdata-item-card\b").Should().HaveCount(3,
            "each card is an interactive card on the shared card block, hooked by its data attribute");
        html.Should().NotContain("class=\"item-card\"", "the old exact class attribute is gone");
    }

    [Fact]
    public async Task ShopIndex_Desktop_DetailsDialogIsTheFoundationShellNamedByItsTitleWithAScrollingBody()
    {
        var client = await SeedBrowseBoardAsync("shopr_idxdlg", 1);

        var html = await GetAsync(client, "/Shop", DesktopUserAgent);

        var start = html.IndexOf("id=\"itemDetailsModal\"", StringComparison.Ordinal);
        start.Should().BeGreaterThan(-1);
        var end = html.IndexOf("id=\"itemDetailsBody\"", start, StringComparison.Ordinal);
        end.Should().BeGreaterThan(start);
        var shell = html[(start - 40)..(end + 40)];
        shell.Should().Contain("aria-labelledby=\"itemDetailsTitle\"");
        shell.Should().Contain("modal-dialog modal-lg modal-dialog-scrollable", "a tall item scrolls inside the dialog instead of the page");
        shell.Should().MatchRegex(@"<h2 class=""modal-title"" id=""itemDetailsTitle"">Item Details</h2>");
        shell.Should().MatchRegex(@"<button type=""button"" class=""btn-close"" data-bs-dismiss=""modal"" aria-label=""Close""></button>");
        shell.Should().NotContain("bg-dark").And.NotContain("text-light").And.NotContain("border-secondary").And.NotContain("btn-close-white");
        shell.Should().NotContain("style=");
    }

    [Fact]
    public async Task ShopIndex_Desktop_LoadErrorTemplate_FollowsTheRecordedAnswer()
    {
        var client = await SeedBrowseBoardAsync("shopr_idxerr", 1);

        var html = await GetAsync(client, "/Shop", DesktopUserAgent);

        if (ItemDialogShowsLoadError)
        {
            html.Should().Contain("Could not load this item.").And.Contain("Open the item page");
            Regex.IsMatch(html, @"<template\b[^>]*\bdata-item-error\b").Should().BeTrue("the line is cloned from a server-rendered template");
        }
        else
        {
            html.Should().NotContain("Could not load this item.").And.NotContain("Open the item page");
            html.Should().NotContain("data-item-error");
        }
    }

    [Fact]
    public async Task ShopIndex_Desktop_LoadsTheDialogModuleAndKeepsNoDialogCodeInline()
    {
        var client = await SeedBrowseBoardAsync("shopr_idxmod", 1);

        var html = await GetAsync(client, "/Shop", DesktopUserAgent);

        Regex.IsMatch(html, @"<script\b[^>]*\btype=""module""[^>]*\bsrc=""/js/shop-item-modal\.js(?:\?[^""]*)?""").Should().BeTrue(
            "the dialog is filled by a module");
        html.Should().NotContain("show.bs.modal", "the page holds no dialog script of its own");
        html.Should().NotContain("purchase-btn");
        html.Should().NotContain(".innerHTML");
    }
}
