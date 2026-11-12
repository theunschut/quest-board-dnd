using System.Net;
using System.Text.RegularExpressions;
using QuestBoard.Domain.Enums;
using QuestBoard.IntegrationTests.Helpers;

namespace QuestBoard.IntegrationTests.Tests;

/// <summary>
/// Pins the cross-board agenda after it was moved onto the layered foundation, on both layouts: the
/// board-switch dialog is filled by a script file rather than inline code, it is named by its own title
/// and sits on the foundation's modal surface, both layouts share one heading outline, the three empty
/// states keep their copy, the phone roster opens without a handler, and no button on the page itself is
/// the primary one. The layout is chosen from the request's User-Agent header, so every fact states
/// which one it sends.
/// </summary>
public class AgendaConversionTests(WebApplicationFactoryBase factory)
    : IClassFixture<WebApplicationFactoryBase>, IAsyncLifetime
{
    private const string DesktopUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

    private const string MobileUserAgent =
        "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1";

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

    private async Task SeedGroupAsync(int groupId, string name)
    {
        await using var ctx = factory.Database.CreateContext();
        if (!ctx.Groups.Any(g => g.Id == groupId))
        {
            ctx.Groups.Add(new GroupEntity
            {
                Id = groupId,
                Name = name,
                CreatedAt = DateTime.UtcNow,
                BoardType = (int)BoardType.OneShot
            });
            await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
    }

    private async Task SeedMembershipAsync(int userId, int groupId)
    {
        await using var ctx = factory.Database.CreateContext();
        if (!ctx.UserGroups.Any(ug => ug.UserId == userId && ug.GroupId == groupId))
        {
            ctx.UserGroups.Add(new UserGroupEntity
            {
                UserId = userId,
                GroupId = groupId,
                GroupRole = (int)GroupRole.Player
            });
            await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
    }

    private async Task<int> SeedEventAsync(int groupId, string title, int daysAhead, TimeOnly? start = null)
    {
        await using var ctx = factory.Database.CreateContext();
        var newEvent = new EventEntity
        {
            Title = title,
            GroupId = groupId,
            Date = DateOnly.FromDateTime(DateTime.Today).AddDays(daysAhead),
            StartTime = start,
            CreatedAt = DateTime.UtcNow
        };
        ctx.Events.Add(newEvent);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        return newEvent.Id;
    }

    // A player who is on the active board and on a second board, with an upcoming event on each, so the
    // page carries both a row that opens its event and a row that asks to switch board first.
    private async Task<(HttpClient Client, int ActiveEventId, int OtherEventId)> SeedTwoBoardPlayerAsync(string handle)
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.ActiveGroupId = 1;

        var (client, user) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, handle, $"{handle}@example.com", name: "Agenda Convert Player");
        await SeedGroupAsync(2, "Conversion Other Board");
        await SeedMembershipAsync(user.Id, 2);
        var activeEventId = await SeedEventAsync(1, "Conversion Active Session", 1, new TimeOnly(19, 0));
        var otherEventId = await SeedEventAsync(2, "Conversion Other Session", 2, new TimeOnly(18, 30));
        return (client, activeEventId, otherEventId);
    }

    private static readonly Regex InlineScript = new(
        @"<script\b(?![^>]*\bsrc\s*=)[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // The opening tag of the element carrying the given id.
    private static string OpeningTag(string html, string tag, string idValue)
    {
        var match = Regex.Match(
            html, $@"<{tag}\b[^>]*\bid=""{Regex.Escape(idValue)}""[^>]*>", RegexOptions.IgnoreCase);
        match.Success.Should().BeTrue($"the page must carry <{tag} id=\"{idValue}\">");
        return match.Value;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Agenda_BothLayouts_LoadTheModuleAndNoInlineScript(bool mobile)
    {
        var (client, _, _) = await SeedTwoBoardPlayerAsync($"agconv_module_{(mobile ? "m" : "d")}");

        var html = await GetAsync(client, "/Agenda", AgentFor(mobile));

        InlineScript.Matches(html).Select(m => m.Value).Should().BeEmpty(
            "the board-switch dialog is filled by a script file, so the page carries no inline script");
        Regex.IsMatch(html, @"<script\b[^>]*\btype=""module""[^>]*\bsrc=""/js/agenda\.js(?:\?[^""]*)?""").Should().BeTrue(
            "the agenda script is loaded as a module");
        html.Should().NotContain("switchBoardGroupId').value", "the fill logic lives in the module, not in the page");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SwitchDialog_IsNamedAndOnTheFoundationSurface(bool mobile)
    {
        var (client, _, otherEventId) = await SeedTwoBoardPlayerAsync($"agconv_dialog_{(mobile ? "m" : "d")}");

        var html = await GetAsync(client, "/Agenda", AgentFor(mobile));

        var dialog = OpeningTag(html, "div", "switchBoardModal");
        var labelledBy = Regex.Match(dialog, @"aria-labelledby=""(?<id>[^""]+)""").Groups["id"].Value;
        labelledBy.Should().NotBeEmpty("the dialog is named through aria-labelledby");
        Regex.IsMatch(
            html,
            $@"<h2\b[^>]*class=""[^""]*\bmodal-title\b[^""]*""[^>]*\bid=""{Regex.Escape(labelledBy)}""|<h2\b[^>]*\bid=""{Regex.Escape(labelledBy)}""[^>]*class=""[^""]*\bmodal-title\b")
            .Should().BeTrue("the name is the dialog's own h2 title");

        var content = Regex.Match(html, @"<div class=""modal-content[^""]*""").Value;
        content.Should().NotContain("bg-dark", "the dialog sits on the foundation's overlay surface");
        content.Should().NotContain("text-light");
        html.Should().NotContain("border-secondary", "the dialog borders come from the foundation modal");
        html.Should().Contain("aria-label=\"Close\"");
        html.Should().Contain("Switch &amp; Continue");

        // The trigger keeps everything the module reads and the post needs.
        html.Should().Contain("data-group-id=\"2\"");
        html.Should().Contain("data-board-name=\"Conversion Other Board\"");
        html.Should().Contain($"data-return-url=\"/Events/Details/{otherEventId}?from=agenda\"");
        html.Should().Contain("action=\"/GroupPicker/SelectGroup\"");
    }

    // The page's own markup, without the layout's navigation and footer.
    private static string Main(string html)
    {
        var match = Regex.Match(html, @"<main\b[\s\S]*</main>", RegexOptions.IgnoreCase);
        match.Success.Should().BeTrue("the layout wraps the page in <main>");
        return match.Value;
    }

    // The page's own markup with the dialog removed, so a check on the page's buttons never reads the dialog footer.
    private static string MainWithoutDialog(string html)
    {
        var page = Main(html);
        var dialogAt = page.IndexOf("id=\"switchBoardModal\"", StringComparison.Ordinal);
        return dialogAt < 0 ? page : page[..dialogAt];
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Agenda_BothLayouts_ShareTheHeadingOutline(bool mobile)
    {
        var (client, _, _) = await SeedTwoBoardPlayerAsync($"agconv_outline_{(mobile ? "m" : "d")}");

        var page = Main(await GetAsync(client, "/Agenda", AgentFor(mobile)));

        Regex.Matches(page, @"<h1\b").Should().HaveCount(1, "the page has one title");
        Regex.IsMatch(
            page,
            @"<div class=""card-header modern-card-header[^""]*"">[\s\S]*?<h1\b[^>]*>[\s\S]*?My Agenda\s*</h1>[\s\S]*?<p class=""header-subtitle[^""]*"">Upcoming events across all your boards</p>")
            .Should().BeTrue("the title and its subtitle sit in the page card's header");
        Regex.IsMatch(page, @"<h2\b[^>]*>\s*Conversion Active Session\s*</h2>").Should().BeTrue(
            "a row title is a second-level heading on both layouts");
        Regex.Matches(page, @"<h[3-6]\b").Should().BeEmpty("the outline has no deeper level than the row titles");
        page.Should().NotContain("<h4", "the phone page no longer titles itself with a fourth-level heading");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Agenda_EmptyStates_AreVerbatim(bool mobile)
    {
        var agent = AgentFor(mobile);

        // No board at all.
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var (noBoards, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, $"agconv_noboards_{(mobile ? "m" : "d")}", $"agconv_noboards_{(mobile ? "m" : "d")}@example.com", roles: []);
        AssertEmptyState(
            Main(await GetAsync(noBoards, "/Agenda", agent)),
            "No Boards Yet",
            "You're not a member of any board yet. Once you join one, your upcoming events will appear here.");

        // A board with nothing scheduled.
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.ActiveGroupId = 1;
        var (quiet, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, $"agconv_quiet_{(mobile ? "m" : "d")}", $"agconv_quiet_{(mobile ? "m" : "d")}@example.com");
        AssertEmptyState(
            Main(await GetAsync(quiet, "/Agenda", agent)),
            "No Upcoming Events",
            "None of your boards have anything scheduled yet. Once a DM schedules a session, it will appear here.");

        // Every board filtered out, with the way back.
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.ActiveGroupId = 1;
        var (filtered, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, $"agconv_filtered_{(mobile ? "m" : "d")}", $"agconv_filtered_{(mobile ? "m" : "d")}@example.com");
        await SeedEventAsync(1, "Conversion Hidden Session", 1);
        var page = Main(await GetAsync(filtered, "/Agenda?boards=", agent));
        AssertEmptyState(
            page,
            "All Boards Filtered Out",
            "You've hidden every board from view. Adjust the filter above, or");
        Regex.IsMatch(page, @"<a\b[^>]*class=""btn btn-secondary[^""]*""[^>]*>[\s\S]*?Show All Boards\s*</a>")
            .Should().BeTrue("the way back is a secondary button");
    }

    private static void AssertEmptyState(string page, string heading, string sentence)
    {
        Regex.IsMatch(page, @"<div class=""empty-state[\s""]").Should().BeTrue("an empty list uses the shared empty state");
        Regex.IsMatch(page, $@"<h2 class=""empty-state-heading"">{Regex.Escape(heading)}</h2>").Should().BeTrue(
            $"the empty state is headed \"{heading}\"");
        WebUtility.HtmlDecode(page).Should().Contain(sentence);
    }

    [Fact]
    public async Task Agenda_PhoneRoster_OpensWithoutAnyHandler()
    {
        var (client, activeEventId, _) = await SeedTwoBoardPlayerAsync("agconv_roster");

        var html = await GetAsync(client, "/Agenda", MobileUserAgent);

        var toggle = Regex.Match(html, @"<button\b[^>]*agenda-roster-toggle[^>]*>").Value;
        toggle.Should().NotBeEmpty("each phone row has a roster toggle");
        toggle.Should().Contain("data-bs-toggle=\"collapse\"");
        toggle.Should().Contain($"data-bs-target=\"#roster-{activeEventId}\"");
        toggle.Should().NotContain("onclick");

        var roster = OpeningTag(html, "div", $"roster-{activeEventId}");
        roster.Should().NotContain("onclick");
        html.Should().NotContain("stopPropagation", "no row-level handler sits above the toggle, so nothing needs to stop a click");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Agenda_ButtonRoles_LeaveNoPagePrimary(bool mobile)
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        factory.TestGroupContext.ActiveGroupId = 1;
        var (client, user) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, $"agconv_roles_{(mobile ? "m" : "d")}", $"agconv_roles_{(mobile ? "m" : "d")}@example.com");
        await SeedGroupAsync(2, "Conversion Other Board");
        await SeedMembershipAsync(user.Id, 2);
        for (var i = 1; i <= 6; i++)
            await SeedEventAsync(1, $"Conversion Roles Session {i}", i);
        await SeedEventAsync(2, "Conversion Roles Other Session", 1);

        var html = await GetAsync(client, "/Agenda?boards=1&boards=2", AgentFor(mobile));
        var page = MainWithoutDialog(html);

        page.Should().Contain("Show More Events");
        page.Should().NotContain("btn-primary", "the page itself has no primary button; the dialog's confirm is its own context");

        Regex.Matches(page, @"<a\b[^>]*>\s*<i\b[^>]*></i>Open Event\s*</a>").Should().NotBeEmpty();
        foreach (Match open in Regex.Matches(page, @"<a\b[^>]*>(?=\s*<i\b[^>]*></i>Open Event)"))
            open.Value.Should().Contain("btn btn-info btn-sm");
        foreach (Match trigger in Regex.Matches(page, @"<button\b[^>]*data-bs-target=""#switchBoardModal""[^>]*>"))
            trigger.Value.Should().Contain("btn btn-info btn-sm");
        Regex.IsMatch(page, @"<a\b[^>]*class=""btn btn-secondary[^""]*""[^>]*>\s*<i\b[^>]*></i>Show More Events").Should().BeTrue(
            "loading more is a secondary action");
        Regex.IsMatch(page, @"<button type=""submit"" class=""btn btn-secondary btn-sm w-100"">Apply</button>").Should().BeTrue(
            "applying the filter is a secondary action");

        // The dialog keeps the one primary action.
        var dialog = html[html.IndexOf("id=\"switchBoardModal\"", StringComparison.Ordinal)..];
        dialog.Should().Contain("btn btn-primary");
    }
}
