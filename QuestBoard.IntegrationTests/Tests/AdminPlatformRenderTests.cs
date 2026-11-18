using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using QuestBoard.Domain.Enums;
using QuestBoard.IntegrationTests.Helpers;
using QuestBoard.Service.ViewModels.AdminViewModels;

namespace QuestBoard.IntegrationTests.Tests;

/// <summary>
/// Pins what the Admin and Platform pages render once they sit on the layered foundation, on both layouts. The
/// layout is chosen from the request's User-Agent header, so every fact states which one it sends. Each page that is
/// converted adds its facts here; the rules a source scan can hold live in the conversion guard instead.
/// </summary>
public class AdminPlatformRenderTests(WebApplicationFactoryBase factory)
    : IClassFixture<WebApplicationFactoryBase>, IAsyncLifetime
{
    private const string DesktopUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

    private const string MobileUserAgent =
        "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1";

    private const string DeleteConfirmSentence = "Are you sure you want to delete this quest? This action cannot be undone.";

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public ValueTask DisposeAsync()
    {
        factory.TestGroupContext.ActiveGroupId = 1;
        factory.TestGroupContext.BoardType = BoardType.OneShot;
        return ValueTask.CompletedTask;
    }

    private static string AgentFor(bool mobile) => mobile ? MobileUserAgent : DesktopUserAgent;

    private static async Task<string> GetAsync(HttpClient client, string url, string userAgent)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("User-Agent", userAgent);
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
    }

    // A status badge as the shared partial writes it: the fill class, the icon class and the word.
    private static readonly Regex StatusBadge = new(
        "<span class=\"badge (?<fill>[\\w-]+)\"><i class=\"fas (?<icon>[\\w-]+) me-1\" aria-hidden=\"true\"></i>(?<word>[^<]+)</span>",
        RegexOptions.Compiled);

    private static List<(string Fill, string Icon, string Word)> StatusBadges(string html) =>
        StatusBadge.Matches(html)
            .Select(m => (m.Groups["fill"].Value, m.Groups["icon"].Value, m.Groups["word"].Value.Trim()))
            .ToList();

    private static int HeadingOneCount(string html) => Regex.Matches(html, @"<h1\b").Count;

    private async Task<HttpClient> SeedBoardAsync(BoardType boardType, string userName, params (string Title, bool Finalized, bool Closed)[] quests)
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.BoardType = boardType;

        var (client, admin) = await AuthenticationHelper.CreateAuthenticatedAdminClientAsync(
            factory, userName, userName + "@example.com", name: "Vashti Emberweir");

        foreach (var quest in quests)
        {
            await TestDataHelper.CreateTestQuestAsync(
                factory.Services, admin.Id, title: quest.Title,
                isFinalized: quest.Finalized, finalizedDate: quest.Finalized ? DateTime.UtcNow.AddDays(30) : null,
                isClosed: quest.Closed, closedDate: quest.Closed ? DateTime.UtcNow : null);
        }

        return client;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AdminQuests_OnACampaignBoard_ShowsOpenAndClosedFromTheSameMapping(bool mobile)
    {
        var client = await SeedBoardAsync(BoardType.Campaign, mobile ? "apr_camp_m" : "apr_camp_d",
            ("Open Campaign Quest", false, false), ("Closed Campaign Quest", false, true));

        var html = await GetAsync(client, "/Admin/Quests", AgentFor(mobile));

        var badges = StatusBadges(html);
        badges.Should().Contain(("bg-success", "fa-clock", "Open"));
        badges.Should().Contain(("bg-dark", "fa-flag-checkered", "Closed"));
        badges.Should().HaveCount(2);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AdminQuests_OnAOneShotBoard_ShowsFinalizedAndOpenFromTheSameMapping(bool mobile)
    {
        var client = await SeedBoardAsync(BoardType.OneShot, mobile ? "apr_one_m" : "apr_one_d",
            ("Open One-Shot Quest", false, false), ("Finalized One-Shot Quest", true, false));

        var html = await GetAsync(client, "/Admin/Quests", AgentFor(mobile));

        var badges = StatusBadges(html);
        badges.Should().Contain(("bg-primary", "fa-check-circle", "Finalized"));
        badges.Should().Contain(("bg-success", "fa-clock", "Open"));
        badges.Should().HaveCount(2);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AdminQuests_HasOnePageTitleInTheCardHeader_AndLinksItsSheets(bool mobile)
    {
        var client = await SeedBoardAsync(BoardType.OneShot, mobile ? "apr_title_m" : "apr_title_d", ("A Quest", false, false));

        var html = await GetAsync(client, "/Admin/Quests", AgentFor(mobile));

        HeadingOneCount(html).Should().Be(1);
        html.Should().MatchRegex(@"modern-card-header[^>]*>\s*<h1[^>]*>(?:\s*<i[^>]*></i>)?\s*Quest Management\s*</h1>");
        html.Should().Contain("/css/admin-pages.css");
        if (mobile)
            html.Should().Contain("/css/admin-pages.mobile.css");
        else
            html.Should().NotContain("/css/admin-pages.mobile.css");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AdminQuests_KeepsTheDeleteFormAndItsConfirmSentence(bool mobile)
    {
        var client = await SeedBoardAsync(BoardType.OneShot, mobile ? "apr_del_m" : "apr_del_d", ("A Quest", false, false));

        var html = await GetAsync(client, "/Admin/Quests", AgentFor(mobile));

        var deleteForm = Regex.Match(html, @"<form\b[^>]*action=""/Admin/DeleteQuest/\d+""[^>]*>").Value;
        deleteForm.Should().NotBeEmpty();
        deleteForm.Should().Contain("method=\"post\"");
        html.Should().Contain("data-confirm-message=\"" + DeleteConfirmSentence + "\"");
        html.Should().Contain("name=\"__RequestVerificationToken\"");
        html.Should().Contain("/js/confirm-submit.js");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AdminQuests_WithNoQuests_ShowsTheEmptyStateInEachLayoutsWords(bool mobile)
    {
        var client = await SeedBoardAsync(BoardType.OneShot, mobile ? "apr_empty_m" : "apr_empty_d");

        var html = await GetAsync(client, "/Admin/Quests", AgentFor(mobile));

        html.Should().Contain("class=\"empty-state\"");
        html.Should().MatchRegex(@"<h2 class=""empty-state-heading"">\s*No quests found\.\s*</h2>");
        if (mobile)
            html.Should().MatchRegex(@"<p class=""empty-state-text"">\s*Quests appear here once created by a DM\.\s*</p>");
        else
            html.Should().NotContain("empty-state-text");
    }

    [Fact]
    public async Task AdminQuests_OnThePhone_ShowsEachQuestAsASlip_WithoutDescriptionOrDmEmail()
    {
        var client = await SeedBoardAsync(BoardType.OneShot, "apr_slip_m", ("Slip Quest", false, false));

        var html = await GetAsync(client, "/Admin/Quests", MobileUserAgent);

        html.Should().Contain("class=\"row-slip\"");
        html.Should().Contain("row-slip__actions");
        html.Should().Contain("DM: Vashti Emberweir");
        html.Should().NotContain("Test Description", "the phone list carries no description");
        html.Should().NotContain("mailto:", "the phone list carries no address");
    }

    [Fact]
    public async Task AdminQuests_OnDesktop_ShowsTheDescriptionExcerptAndTheDmAddress()
    {
        var client = await SeedBoardAsync(BoardType.OneShot, "apr_cols_d", ("Table Quest", false, false));

        var html = await GetAsync(client, "/Admin/Quests", DesktopUserAgent);

        html.Should().Contain("quest-excerpt");
        html.Should().Contain("Test Description");
        html.Should().MatchRegex(@"mailto:apr_cols_d[^""]*@example\.com");
        html.Should().NotContain("style=\"");
    }

    // ---- Admin/Users -------------------------------------------------------------------------------

    private const string SelfReason = "This is your own account, so you cannot change your role or remove yourself here.";

    private async Task<UserEntity> AddMemberAsync(string tag, string name, GroupRole role, bool emailConfirmed = true, string? email = null)
    {
        var account = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, $"apu_{tag}", email ?? $"apu_{tag}@example.com", "Test123!", name);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
        if (!emailConfirmed)
        {
            var tracked = await context.Users.FindAsync([account.Id], TestContext.Current.CancellationToken);
            tracked!.EmailConfirmed = false;
        }

        context.UserGroups.Add(new UserGroupEntity { UserId = account.Id, GroupId = 1, GroupRole = (int)role });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return account;
    }

    // The part of the Users page that belongs to one person: their table row on desktop, their slip on the phone.
    private static string UserRow(string html, string name, bool mobile)
    {
        // The signed-in person's name also sits in the navigation bar, so the search starts at the page content.
        var at = html.IndexOf(name, html.IndexOf("<main", StringComparison.Ordinal), StringComparison.Ordinal);
        at.Should().BeGreaterThan(-1, $"{name} must be listed");
        if (!mobile)
        {
            var start = html.LastIndexOf("<tr", at, StringComparison.Ordinal);
            return html[start..(html.IndexOf("</tr>", at, StringComparison.Ordinal) + 5)];
        }

        var marker = new Regex("class=\"row-slip\"");
        var slip = marker.Matches(html).Select(m => m.Index).Last(i => i < at);
        var next = marker.Matches(html).Select(m => m.Index).FirstOrDefault(i => i > at, html.Length);
        return html[slip..next];
    }

    private static readonly Regex FormElement = new(@"<form\b.*?</form>", RegexOptions.Compiled | RegexOptions.Singleline);
    private static readonly Regex RefusedButton = new(@"<button\b[^>]*aria-disabled=""true""[^>]*>.*?</button>", RegexOptions.Compiled | RegexOptions.Singleline);

    // Every refused control of a row: not in a form, a plain button, no title, and pointing at a reason line that exists.
    private static List<string> RefusedControls(string row)
    {
        var outsideForms = FormElement.Replace(row, "");
        var refused = RefusedButton.Matches(outsideForms).Select(m => m.Value).ToList();
        Regex.Matches(row, "aria-disabled=\"true\"").Count.Should().Be(refused.Count, "a refused control sits outside every form");
        foreach (var button in refused)
        {
            button.Should().Contain("type=\"button\"");
            button.Should().NotContain("title=");
            var id = Regex.Match(button, "aria-describedby=\"([^\"]+)\"").Groups[1].Value;
            id.Should().NotBeEmpty();
            row.Should().MatchRegex($"<p class=\"control-reason\" id=\"{id}\"><i class=\"fas fa-lock\" aria-hidden=\"true\"></i><span>[^<]+</span></p>");
        }

        return refused;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AdminUsers_OnTheSignedInAdminsOwnRow_RefusesTheRoleButtonAndRemove_WithTheSelfReason(bool mobile)
    {
        var client = await SeedBoardAsync(BoardType.OneShot, mobile ? "apu_self_m" : "apu_self_d");
        await AddMemberAsync(mobile ? "peer_m" : "peer_d", "Isolde Brackenfell", GroupRole.Admin);

        var html = await GetAsync(client, "/Admin/Users", AgentFor(mobile));

        var row = UserRow(html, "Vashti Emberweir", mobile);
        var refused = RefusedControls(row);
        refused.Should().HaveCount(2);
        refused.Should().Contain(b => b.Contains("Demote to DM"));
        refused.Should().Contain(b => b.Contains("Remove from Group"));
        refused.Should().OnlyContain(b => Regex.IsMatch(b, "aria-describedby=\"reason-self-\\d+\""));
        Regex.Matches(row, "class=\"control-reason\"").Count.Should().Be(1, "one reason line serves both refused controls");
        row.Should().Contain(SelfReason);
        row.Should().MatchRegex("EditUser\\?userId=\\d+", "editing one's own name stays possible");
        FormElement.Replace(row, "").Should().NotContain("DemoteFromAdmin");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AdminUsers_OnOtherMembersRows_KeepTheirWorkingFormsAndShowNoReason(bool mobile)
    {
        var client = await SeedBoardAsync(BoardType.OneShot, mobile ? "apu_keep_m" : "apu_keep_d");
        await AddMemberAsync(mobile ? "keeppeer_m" : "keeppeer_d", "Isolde Brackenfell", GroupRole.Admin);
        await AddMemberAsync(mobile ? "keepdm_m" : "keepdm_d", "Orrin Duskmantle", GroupRole.DungeonMaster);

        var html = await GetAsync(client, "/Admin/Users", AgentFor(mobile));

        var peer = UserRow(html, "Isolde Brackenfell", mobile);
        RefusedControls(peer).Should().BeEmpty();
        peer.Should().NotContain("control-reason");
        peer.Should().Contain("action=\"/Admin/DemoteFromAdmin\"");
        peer.Should().Contain("action=\"/Admin/DeleteUser/");

        var dm = UserRow(html, "Orrin Duskmantle", mobile);
        RefusedControls(dm).Should().BeEmpty();
        dm.Should().Contain("action=\"/Admin/PromoteToAdmin\"");
        dm.Should().Contain("action=\"/Admin/DemoteToPlayer\"");
    }

    private const string LastAdminReason = "This person is the only one who can manage this board. Give someone else that role first.";
    private const string ReachReason = "Only a SuperAdmin can change this account.";

    private async Task MakePlatformAdministratorAsync(UserEntity account)
    {
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<UserEntity>>();
        var tracked = await userManager.FindByIdAsync(account.Id.ToString());
        (await userManager.AddToRoleAsync(tracked!, "SuperAdmin")).Succeeded.Should().BeTrue();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AdminUsers_ForTheOnlyAdmin_RefusesTheDemotionAndRemove_WithTheLastAdminReason(bool mobile)
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(
            factory, mobile ? "apu_last_m" : "apu_last_d", (mobile ? "apu_last_m" : "apu_last_d") + "@example.com", name: "Platform Overseer");
        var only = await AddMemberAsync(mobile ? "only_m" : "only_d", "Vashti Emberweir", GroupRole.Admin);
        await AddMemberAsync(mobile ? "lastdm_m" : "lastdm_d", "Orrin Duskmantle", GroupRole.DungeonMaster);

        var html = await GetAsync(client, "/Admin/Users", AgentFor(mobile));

        var row = UserRow(html, "Vashti Emberweir", mobile);
        var refused = RefusedControls(row);
        refused.Should().HaveCount(2);
        refused.Should().Contain(b => b.Contains("Demote to DM"));
        refused.Should().Contain(b => b.Contains("Remove from Group"));
        refused.Should().OnlyContain(b => b.Contains($"aria-describedby=\"reason-last-{only.Id}\""));
        row.Should().Contain(LastAdminReason);
        row.Should().NotContain(SelfReason);
        Regex.Matches(row, "class=\"control-reason\"").Count.Should().Be(1);
        row.Should().MatchRegex("EditUser\\?userId=\\d+");

        RefusedControls(UserRow(html, "Orrin Duskmantle", mobile)).Should().BeEmpty("the DM is not the last Admin");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AdminUsers_ForAnAccountOutsideTheActorsReach_RefusesEditAndResend_WithTheReachReason(bool mobile)
    {
        var client = await SeedBoardAsync(BoardType.OneShot, mobile ? "apu_reach_m" : "apu_reach_d");
        var elevated = await AddMemberAsync(mobile ? "elev_m" : "elev_d", "Orrin Duskmantle", GroupRole.Player, emailConfirmed: false);
        await MakePlatformAdministratorAsync(elevated);

        var html = await GetAsync(client, "/Admin/Users", AgentFor(mobile));

        var row = UserRow(html, "Orrin Duskmantle", mobile);
        var refused = RefusedControls(row);
        refused.Should().HaveCount(2);
        refused.Should().Contain(b => b.Contains("Edit"));
        refused.Should().Contain(b => b.Contains("Resend Welcome Email"));
        refused.Should().OnlyContain(b => b.Contains($"aria-describedby=\"reason-reach-{elevated.Id}\""));
        row.Should().Contain(ReachReason);
        Regex.Matches(row, "class=\"control-reason\"").Count.Should().Be(1);
        FormElement.Replace(row, "").Should().NotContain("SendConfirmationEmail").And.NotContain("EditUser?");
        row.Should().Contain("action=\"/Admin/PromoteToDM\"", "the role buttons are not part of the reach rule");
        row.Should().Contain("action=\"/Admin/DeleteUser/");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AdminUsers_ShowsTheRoleBadgeFromTheSharedMapping_WithTheApprovedWords(bool mobile)
    {
        var client = await SeedBoardAsync(BoardType.OneShot, mobile ? "apu_badge_m" : "apu_badge_d");
        await AddMemberAsync(mobile ? "badgedm_m" : "badgedm_d", "Orrin Duskmantle", GroupRole.DungeonMaster);
        await AddMemberAsync(mobile ? "badgepl_m" : "badgepl_d", "Brindle Oakenfeather", GroupRole.Player);

        var html = await GetAsync(client, "/Admin/Users", AgentFor(mobile));

        var badges = StatusBadges(html);
        badges.Should().Contain(("bg-danger", "fa-shield-alt", "Admin"));
        badges.Should().Contain(("bg-warning", "fa-crown", "Dungeon Master"));
        badges.Should().Contain(("bg-primary", "fa-dice-d20", "Player"));
        badges.Should().HaveCount(3);
        html.Should().NotContain("Administrator");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AdminUsers_HasOnePageTitleWithTheCreateButton_AndLinksItsSheets(bool mobile)
    {
        var client = await SeedBoardAsync(BoardType.OneShot, mobile ? "apu_title_m" : "apu_title_d");

        var html = await GetAsync(client, "/Admin/Users", AgentFor(mobile));

        HeadingOneCount(html).Should().Be(1);
        html.Should().MatchRegex(@"modern-card-header[^>]*>\s*<h1[^>]*>(?:\s*<i[^>]*></i>)?\s*User Management\s*</h1>");
        Regex.Matches(html, @"<a[^>]*class=""btn btn-primary""[^>]*>[^<]*<i[^>]*></i>Create User\s*</a>").Count.Should().Be(1);
        html.Should().Contain("/css/admin-pages.css");
        html.Should().NotContain("admin-users.mobile.css");
        if (mobile)
            html.Should().Contain("/css/admin-pages.mobile.css");
        else
            html.Should().NotContain("/css/admin-pages.mobile.css");
    }

    [Fact]
    public async Task AdminUsers_OnDesktop_ShowsTheEmailUnderTheName_InAColumnOfNameRoleAndActions()
    {
        var client = await SeedBoardAsync(BoardType.OneShot, "apu_cols_d");
        var noAddress = await AddMemberAsync("noaddr_d", "Orrin Duskmantle", GroupRole.Player);
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
            (await context.Users.FindAsync([noAddress.Id], TestContext.Current.CancellationToken))!.Email = null;
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var html = await GetAsync(client, "/Admin/Users", DesktopUserAgent);

        Regex.Matches(html, "<th scope=\"col\"[^>]*>([^<]+)</th>").Select(m => m.Groups[1].Value)
            .Should().Equal("Name", "Current Role", "Actions");
        html.Should().Contain("<th scope=\"col\" class=\"th-actions\">Actions</th>");
        html.Should().Contain("<td class=\"actions-cell\">");
        html.Should().MatchRegex(@"user-cell__name"">Vashti Emberweir</span>\s*<a href=""mailto:apu_cols_d[^""]*@example\.com"" class=""email-link user-cell__email"">");
        UserRow(html, "Orrin Duskmantle", false).Should().Contain("<span class=\"user-cell__email user-cell__email--none\">No email provided</span>")
            .And.NotContain("mailto:");
        html.Should().NotContain("style=\"");
    }

    [Fact]
    public async Task AdminUsers_OnThePhone_ShowsEachPersonAsASlip_WithoutTheirAddress()
    {
        var client = await SeedBoardAsync(BoardType.OneShot, "apu_slip_m");
        await AddMemberAsync("slip2_m", "Orrin Duskmantle", GroupRole.Player);

        var html = await GetAsync(client, "/Admin/Users", MobileUserAgent);

        Regex.Matches(html, "class=\"row-slip\"").Count.Should().Be(2);
        html.Should().Contain("row-slip__head").And.Contain("row-slip__actions");
        html.Should().NotContain("mailto:", "the phone list carries no address");
        html.Should().NotContain("<table");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AdminUsers_OffersResendWelcomeEmailOnlyToAnUnconfirmedPersonWithAnAddress(bool mobile)
    {
        var client = await SeedBoardAsync(BoardType.OneShot, mobile ? "apu_resend_m" : "apu_resend_d");
        await AddMemberAsync(mobile ? "unconf_m" : "unconf_d", "Tamsin Hollowreed", GroupRole.Player, emailConfirmed: false);
        await AddMemberAsync(mobile ? "conf_m" : "conf_d", "Brindle Oakenfeather", GroupRole.Player);

        var html = await GetAsync(client, "/Admin/Users", AgentFor(mobile));

        var unconfirmed = UserRow(html, "Tamsin Hollowreed", mobile);
        unconfirmed.Should().Contain("action=\"/Admin/SendConfirmationEmail\"");
        unconfirmed.Should().MatchRegex(@"<i class=""fas fa-envelope me-2"" aria-hidden=""true""></i>\s*Resend Welcome Email");
        UserRow(html, "Brindle Oakenfeather", mobile).Should().NotContain("SendConfirmationEmail");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AdminUsers_KeepsTheControlsInOneOrder_AndTheRemoveFormWithItsConfirmSentence(bool mobile)
    {
        var client = await SeedBoardAsync(BoardType.OneShot, mobile ? "apu_order_m" : "apu_order_d");
        await AddMemberAsync(mobile ? "ordr_m" : "ordr_d", "Tamsin Hollowreed", GroupRole.DungeonMaster, emailConfirmed: false);

        var html = await GetAsync(client, "/Admin/Users", AgentFor(mobile));

        var row = UserRow(html, "Tamsin Hollowreed", mobile);
        var order = new[] { "PromoteToAdmin", "DemoteToPlayer", "SendConfirmationEmail", "EditUser?", "DeleteUser/" }
            .Select(marker => row.IndexOf(marker, StringComparison.Ordinal)).ToList();
        order.Should().OnlyContain(at => at > -1);
        order.Should().BeInAscendingOrder("promote, demote, Resend Welcome Email, Edit, Remove from Group keep one order");
        row.Should().Contain("data-confirm-message=\"Are you sure you want to remove this user from the group? Their availability answers for events on this board will be deleted and cannot be recovered. They will keep their account and any other group memberships.\"");
        row.Should().Contain("name=\"__RequestVerificationToken\"");
        html.Should().Contain("/js/confirm-submit.js");
        RefusedControls(row).Should().BeEmpty();
        row.Should().NotContain("title=");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AdminUsers_WithNoMembers_ShowsTheEmptyStateInEachLayoutsWords(bool mobile)
    {
        // A platform administrator who is not a member of the board sees a board nobody has joined yet.
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(
            factory, mobile ? "apu_empty_m" : "apu_empty_d", (mobile ? "apu_empty_m" : "apu_empty_d") + "@example.com");
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
            context.UserGroups.RemoveRange(context.UserGroups);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var response = await client.SendAsync(
            new HttpRequestMessage(HttpMethod.Get, "/Admin/Users") { Headers = { { "User-Agent", AgentFor(mobile) } } },
            TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        html.Should().Contain("class=\"empty-state\"");
        html.Should().MatchRegex(@"<h2 class=""empty-state-heading"">\s*No users found\.\s*</h2>");
        if (mobile)
            html.Should().MatchRegex(@"<p class=""empty-state-text"">\s*Users appear here once registered\.\s*</p>");
        else
            html.Should().NotContain("empty-state-text");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AdminUsers_ListsALongNameInFull_AndAPersonWithNoNameWithoutBreakingTheRow(bool mobile)
    {
        const string longName = "Lysandra Thistledown-Verrenmoor of the Seven Silverbirch Hollows and Beyond";
        var client = await SeedBoardAsync(BoardType.OneShot, mobile ? "apu_long_m" : "apu_long_d");
        await AddMemberAsync(mobile ? "long_m" : "long_d", longName, GroupRole.Player);
        var nameless = await AddMemberAsync(mobile ? "blank_m" : "blank_d", "Placeholder Name", GroupRole.Player);
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
            (await context.Users.FindAsync([nameless.Id], TestContext.Current.CancellationToken))!.Name = "";
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var html = await GetAsync(client, "/Admin/Users", AgentFor(mobile));

        html.Should().Contain(longName);
        var blankRow = Regex.Match(html, $@"<form[^>]*action=""/Admin/DeleteUser/{nameless.Id}""").Success;
        blankRow.Should().BeTrue("a person with no name keeps their row and their controls");
        html.Should().MatchRegex(mobile
            ? @"user-slip__name""><i class=""fas fa-user me-2"" aria-hidden=""true""></i></span>"
            : @"user-cell__name""></span>");
    }

    // ---- Admin/EditUser, CreateUser and ResetPassword ----------------------------------------------

    private async Task<(HttpClient Client, UserEntity Target)> SeedFormAsync(string tag, string targetName = "Brindle Oakenfeather")
    {
        var client = await SeedBoardAsync(BoardType.OneShot, $"apf_{tag}");
        var target = await AddMemberAsync($"t_{tag}", targetName, GroupRole.Player);
        return (client, target);
    }

    // The card header and its one title, as the converted forms write them.
    private static string CardHeaderTitle(string title) =>
        @"modern-card-header[^>]*>\s*<h1[^>]*>(?:\s*<i[^>]*></i>)?\s*" + Regex.Escape(title) + @"\s*</h1>";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AdminEditUser_HasOneTitleInTheCardHeader_AndWrapsItsFormInTheFormPage(bool mobile)
    {
        var (client, target) = await SeedFormAsync(mobile ? "edit_m" : "edit_d");

        var html = await GetAsync(client, $"/Admin/EditUser?userId={target.Id}", AgentFor(mobile));

        HeadingOneCount(html).Should().Be(1);
        html.Should().MatchRegex(CardHeaderTitle("Edit User"));
        html.Should().MatchRegex(@"<form[^>]*class=""form-page""");
        html.Should().MatchRegex(@"<div class=""form-actions"">\s*<a[^>]*class=""btn btn-secondary""[^>]*>(?:\s*<i[^>]*></i>)?\s*Back to Users\s*</a>\s*<button type=""submit"" class=""btn btn-primary"">(?:\s*<i[^>]*></i>)?\s*Save Changes\s*</button>");
        html.Should().Contain("/css/admin-pages.css");
        if (mobile)
            html.Should().Contain("/css/admin-pages.mobile.css");
        html.Should().NotContain("admin-form.mobile.css");
        html.Should().NotContain("admin-form-card-mobile");
        html.Should().NotContain("style=\"");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AdminEditUser_ShowsPasswordManagementWithAFilledResetLink_OnBothLayouts(bool mobile)
    {
        var (client, target) = await SeedFormAsync(mobile ? "pwm_m" : "pwm_d");

        var html = await GetAsync(client, $"/Admin/EditUser?userId={target.Id}", AgentFor(mobile));

        html.Should().MatchRegex(@"<h2 class=""form-section-title"">(?:\s*<i[^>]*></i>)?\s*Password Management\s*</h2>");
        WebUtility.HtmlDecode(html).Should().Contain("Reset this user's password (they will need to use the new password to log in).", "the sentence is the same on both layouts");
        html.Should().MatchRegex($@"<a[^>]*class=""btn btn-danger""[^>]*href=""/Admin/ResetPassword\?userId={target.Id}""|<a[^>]*href=""/Admin/ResetPassword\?userId={target.Id}""[^>]*class=""btn btn-danger""");
        html.Should().NotContain("btn-outline");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AdminEditUser_MarksNameRequired_KeepsEmailOptional_AndLeavesBothOutOfAutofill(bool mobile)
    {
        var (client, target) = await SeedFormAsync(mobile ? "req_m" : "req_d");

        var html = await GetAsync(client, $"/Admin/EditUser?userId={target.Id}", AgentFor(mobile));

        html.Should().MatchRegex(@"<label[^>]*for=""Name""[^>]*>\s*Name <span class=""text-danger"" aria-hidden=""true"">\*</span><span class=""visually-hidden""> \(required\)</span></label>");
        html.Should().MatchRegex(@"<label[^>]*for=""Email""[^>]*>\s*Email\s*</label>");
        html.Should().MatchRegex(@"<input[^>]*id=""Name""[^>]*autocomplete=""off""|<input[^>]*autocomplete=""off""[^>]*id=""Name""");
        html.Should().MatchRegex(@"<input[^>]*id=""Email""[^>]*autocomplete=""off""|<input[^>]*autocomplete=""off""[^>]*id=""Email""");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AdminEditUser_WithAnInvalidPost_ShowsTheMessageOnTheFoundationTreatment(bool mobile)
    {
        var (client, target) = await SeedFormAsync(mobile ? "inv_m" : "inv_d");

        var html = await PostFormAsync(client, "/Admin/EditUser", AgentFor(mobile), new Dictionary<string, string>
        {
            ["Id"] = target.Id.ToString(),
            ["Name"] = "",
            ["Email"] = "not-an-address",
        });

        html.Should().MatchRegex(@"<span[^>]*class=""field-validation-error""[^>]*data-valmsg-for=""Name""|<span[^>]*data-valmsg-for=""Name""[^>]*class=""field-validation-error""");
        html.Should().NotMatchRegex(@"<span[^>]*class=""text-danger[^""]*""[^>]*data-valmsg-for");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AdminCreateUser_HasOneTitleInTheCardHeader_AndTheFormContract(bool mobile)
    {
        var client = await SeedBoardAsync(BoardType.OneShot, mobile ? "apf_create_m" : "apf_create_d");

        var html = await GetAsync(client, "/Admin/CreateUser", AgentFor(mobile));

        HeadingOneCount(html).Should().Be(1);
        html.Should().MatchRegex(CardHeaderTitle("Create New User"));
        html.Should().MatchRegex(@"<form[^>]*class=""form-page""");
        html.Should().MatchRegex(@"<div class=""form-actions"">\s*<a[^>]*class=""btn btn-secondary""[^>]*>(?:\s*<i[^>]*></i>)?\s*Back to Users\s*</a>\s*<button type=""submit"" class=""btn btn-primary"">(?:\s*<i[^>]*></i>)?\s*Create User\s*</button>");
        foreach (var field in new[] { "Email", "GroupRole" })
        {
            html.Should().MatchRegex(@"<label[^>]*for=""" + field + @"""[^>]*>[^<]+<span class=""text-danger"" aria-hidden=""true"">\*</span><span class=""visually-hidden""> \(required\)</span></label>");
        }
        html.Should().MatchRegex(@"<label[^>]*for=""Name""[^>]*>Display Name <span class=""text-danger"" aria-hidden=""true"">\*</span>");
        html.Should().MatchRegex(@"<input[^>]*id=""Email""[^>]*autocomplete=""off""|<input[^>]*autocomplete=""off""[^>]*id=""Email""");
        html.Should().MatchRegex(@"<input[^>]*id=""Name""[^>]*autocomplete=""off""|<input[^>]*autocomplete=""off""[^>]*id=""Name""");
        html.Should().Contain("/css/admin-pages.css");
        html.Should().NotContain("admin-form.mobile.css");
        html.Should().NotContain("admin-form-card-mobile");
        html.Should().NotContain("style=\"");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AdminCreateUser_BuildsTheRoleSelectFromTheSharedMapping_KeepingTheOptionValues(bool mobile)
    {
        var client = await SeedBoardAsync(BoardType.OneShot, mobile ? "apf_role_m" : "apf_role_d");

        var html = await GetAsync(client, "/Admin/CreateUser", AgentFor(mobile));

        var select = Regex.Match(html, @"<select[^>]*id=""GroupRole""[^>]*>.*?</select>", RegexOptions.Singleline).Value;
        select.Should().NotBeEmpty();
        var options = Regex.Matches(select, @"<option[^>]*value=""(?<value>\d+)""[^>]*>(?<text>[^<]+)</option>")
            .Select(m => (m.Groups["value"].Value, m.Groups["text"].Value.Trim()))
            .ToList();
        options.Should().Equal(
            AdminPlatformDisplay.GroupRoleOptions().Select(o => (o.Value, o.Text)));
        options.Select(o => o.Item2).Should().BeEquivalentTo(["Admin", "Dungeon Master", "Player"]);
        Regex.Match(select, @"<option[^>]*selected=""selected""[^>]*>(?<text>[^<]+)</option>").Groups["text"].Value
            .Should().Be("Player", "a new person starts as a Player");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AdminCreateUser_WithAnInvalidPost_ShowsTheMessagesOnTheFoundationTreatment(bool mobile)
    {
        var client = await SeedBoardAsync(BoardType.OneShot, mobile ? "apf_createbad_m" : "apf_createbad_d");

        var html = await PostFormAsync(client, "/Admin/CreateUser", AgentFor(mobile), new Dictionary<string, string>
        {
            ["Email"] = "not-an-address",
            ["Name"] = "",
            ["GroupRole"] = ((int)GroupRole.Player).ToString(),
        });

        html.Should().MatchRegex(@"<span[^>]*data-valmsg-for=""Email""[^>]*>[^<]*valid e-mail address");
        html.Should().NotMatchRegex(@"<span[^>]*class=""text-danger[^""]*""[^>]*data-valmsg-for");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AdminResetPassword_NamesThePersonInTheTitle_AndOffersNewPasswordInputsThatAllowPaste(bool mobile)
    {
        var (client, target) = await SeedFormAsync(mobile ? "reset_m" : "reset_d", "Lysandra Thistledown-Verrenmoor");

        var html = await GetAsync(client, $"/Admin/ResetPassword?userId={target.Id}", AgentFor(mobile));

        HeadingOneCount(html).Should().Be(1);
        html.Should().MatchRegex(CardHeaderTitle("Reset Password for Lysandra Thistledown-Verrenmoor"));
        html.Should().MatchRegex(@"<form[^>]*class=""form-page""");
        foreach (var field in new[] { "NewPassword", "ConfirmPassword" })
        {
            var input = Regex.Match(html, @"<input[^>]*id=""" + field + @"""[^>]*>").Value;
            input.Should().Contain("type=\"password\"").And.Contain("autocomplete=\"new-password\"");
            input.Should().NotContain("onpaste").And.NotContain("autocomplete=\"off\"");
            html.Should().MatchRegex(@"<label[^>]*for=""" + field + @"""[^>]*>[^<]+<span class=""text-danger"" aria-hidden=""true"">\*</span><span class=""visually-hidden""> \(required\)</span></label>");
        }
        html.Should().MatchRegex(@"<div class=""form-actions( form-actions--stack)?"">\s*<a[^>]*class=""btn btn-secondary""[^>]*>(?:\s*<i[^>]*></i>)?\s*Back to Users\s*</a>\s*<button type=""submit"" class=""btn btn-primary"">(?:\s*<i[^>]*></i>)?\s*Reset Password\s*</button>");
        html.Contains("form-actions--stack").Should().Be(mobile, "only the phone stacks the long commit label under its exit");
        html.Should().NotContain("admin-form.mobile.css");
        html.Should().NotContain("admin-form-card-mobile");
        html.Should().Contain("<div class=\"alert alert-warning\">");
        WebUtility.HtmlDecode(html).Should().Contain("This will reset the password for Lysandra Thistledown-Verrenmoor. The user will need to use the new password to log in.");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AdminResetPassword_ShowsThePasswordRuleHint_AlignedToTheServerPolicy(bool mobile)
    {
        var (client, target) = await SeedFormAsync(mobile ? "hint_m" : "hint_d");

        var html = await GetAsync(client, $"/Admin/ResetPassword?userId={target.Id}", AgentFor(mobile));

        html.Should().MatchRegex(@"<div class=""form-text"" id=""NewPassword-hint"">At least 8 characters, with an upper-case letter, a lower-case letter and a digit\.</div>");
        Regex.Match(html, @"<input[^>]*id=""NewPassword""[^>]*>").Value.Should().Contain("aria-describedby=\"NewPassword-hint NewPassword-error\"");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AdminResetPassword_WithASevenCharacterPassword_ShowsTheLengthMessageWithTheHint(bool mobile)
    {
        var (client, target) = await SeedFormAsync(mobile ? "short_m" : "short_d");

        var html = await PostFormAsync(client, "/Admin/ResetPassword", AgentFor(mobile), new Dictionary<string, string>
        {
            ["UserId"] = target.Id.ToString(),
            ["UserName"] = "Brindle Oakenfeather",
            ["NewPassword"] = "Abcdef1",
            ["ConfirmPassword"] = "Abcdef1",
        });

        WebUtility.HtmlDecode(html).Should().MatchRegex(@"<span[^>]*data-valmsg-for=""NewPassword""[^>]*>[^<]*must be at least 8 and at max 100 characters long");
        html.Should().Contain("id=\"NewPassword-hint\"");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AdminResetPassword_WithMismatchedPasswords_ShowsTheMessageOnTheFoundationTreatment(bool mobile)
    {
        var (client, target) = await SeedFormAsync(mobile ? "mism_m" : "mism_d");

        var html = await PostFormAsync(client, "/Admin/ResetPassword", AgentFor(mobile), new Dictionary<string, string>
        {
            ["UserId"] = target.Id.ToString(),
            ["UserName"] = "Brindle Oakenfeather",
            ["NewPassword"] = "Abcdefg1h",
            ["ConfirmPassword"] = "Different1x",
        });

        html.Should().MatchRegex(@"<span[^>]*data-valmsg-for=""ConfirmPassword""[^>]*>[^<]*do not match");
        html.Should().Contain("validation-summary-errors");
        html.Should().NotMatchRegex(@"<span[^>]*class=""text-danger[^""]*""[^>]*data-valmsg-for");
        html.Should().NotMatchRegex(@"class=""text-danger mb-3""");
    }

    private const int OneShotBoardId = 71;
    private const int CampaignBoardId = 72;
    private const int EmptyBoardId = 73;
    private const int EmptyCampaignBoardId = 74;

    // Seeds a One-Shot board with one member, a Campaign board with one member, an empty One-Shot board and an empty Campaign board, and signs in
    // as a platform administrator who belongs to none of them.
    private async Task<HttpClient> SeedBoardListAsync(string tag)
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(
            factory, "bl_" + tag, "bl_" + tag + "@example.com");
        await BoardMoveSeed.SeedBoardAsync(factory, OneShotBoardId, "Silverbrook Table", BoardType.OneShot);
        await BoardMoveSeed.SeedBoardAsync(factory, CampaignBoardId, "Ironvale Chronicle", BoardType.Campaign);
        await BoardMoveSeed.SeedBoardAsync(factory, EmptyBoardId, "Quiet Hollow", BoardType.OneShot);
        await BoardMoveSeed.SeedBoardAsync(factory, EmptyCampaignBoardId, "Fallow Reach", BoardType.Campaign);
        var oneShotMember = await BoardMoveSeed.CreateUserAsync(factory, "bl_a_" + tag, "Brindle Oakenfeather");
        var campaignMember = await BoardMoveSeed.CreateUserAsync(factory, "bl_b_" + tag, "Corwin Ashdown");
        await BoardMoveSeed.SeedMembershipAsync(factory, oneShotMember.Id, OneShotBoardId, GroupRole.Player);
        await BoardMoveSeed.SeedMembershipAsync(factory, campaignMember.Id, CampaignBoardId, GroupRole.Player);
        return client;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PlatformGroupIndex_ShowsEachBoardTypeThroughTheSharedBadge(bool mobile)
    {
        var client = await SeedBoardListAsync(mobile ? "type_m" : "type_d");

        var html = await GetAsync(client, "/platform/Group", AgentFor(mobile));

        BoardListRow(html, "Silverbrook Table").Should().Contain(
            "<span class=\"badge bg-primary\"><i class=\"fas fa-dice-d20 me-1\" aria-hidden=\"true\"></i>One-Shot</span>");
        BoardListRow(html, "Ironvale Chronicle").Should().Contain(
            "<span class=\"badge bg-info\"><i class=\"fas fa-book-open me-1\" aria-hidden=\"true\"></i>Campaign</span>");
        BoardListRow(html, "Fallow Reach").Should().Contain("Campaign</span>");
        BoardListRow(html, "Quiet Hollow").Should().Contain("One-Shot</span>");
        html.Should().NotContain("text-dark");
    }

    // A desktop row is a <tr>, a phone row is a row-slip block; the one that names the board and carries its Open Board
    // button is the board's own row on either layout.
    private static string BoardListRow(string html, string boardName)
    {
        var rows = Regex.Split(html, "<tr[ >]|class=\"row-slip\"");
        var row = rows.SingleOrDefault(r => r.Contains(boardName) && r.Contains("Open Board"));
        row.Should().NotBeNull($"the board list should hold a row for '{boardName}'");
        return row!;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PlatformGroupDelete_ShowsTheBoardTypeThroughTheSharedBadge(bool mobile)
    {
        var client = await SeedBoardListAsync(mobile ? "deltype_m" : "deltype_d");

        var html = await GetAsync(client, $"/platform/Group/Delete/{EmptyCampaignBoardId}", AgentFor(mobile));

        StatusBadges(html).Should().Equal(("bg-info", "fa-book-open", "Campaign"));
        html.Should().NotContain("text-dark");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PlatformGroupIndex_LinksThePlatformSheets(bool mobile)
    {
        var client = await SeedBoardListAsync(mobile ? "sheet_m" : "sheet_d");

        var html = await GetAsync(client, "/platform/Group", AgentFor(mobile));

        html.Should().Contain("/css/platform-pages.css");
        if (mobile)
            html.Should().Contain("/css/platform-pages.mobile.css");
        else
            html.Should().NotContain("/css/platform-pages.mobile.css");
        html.Should().NotContain("platform-group.mobile.css");
    }

    // Posts a form to the page's own URL with the antiforgery token the page issued, under the given User-Agent.
    private static async Task<string> PostFormAsync(HttpClient client, string url, string userAgent, Dictionary<string, string> fields)
    {
        var getUrl = url + (fields.TryGetValue("Id", out var id) ? $"?userId={id}" : fields.TryGetValue("UserId", out var uid) ? $"?userId={uid}" : "");
        var page = await GetAsync(client, getUrl, userAgent);
        var token = Regex.Match(page, @"name=""__RequestVerificationToken""[^>]*value=""([^""]+)""").Groups[1].Value;
        token.Should().NotBeEmpty();
        fields["__RequestVerificationToken"] = token;
        var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = new FormUrlEncodedContent(fields) };
        request.Headers.TryAddWithoutValidation("User-Agent", userAgent);
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK, "an invalid post shows the form again");
        return await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
    }
}
