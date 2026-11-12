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
}
