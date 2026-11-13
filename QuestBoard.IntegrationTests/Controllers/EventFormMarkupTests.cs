using System.Net;
using System.Text.RegularExpressions;
using QuestBoard.Domain.Enums;
using QuestBoard.IntegrationTests.Helpers;

namespace QuestBoard.IntegrationTests.Controllers;

// Pins the markup of the event forms. The recurrence checkbox is named by its label, the recurring-series
// behaviour and the save-scope dialog run from one script file that reads what it needs from the page, and neither
// form carries inline script. The save-scope field keeps its safe default, so a form submitted without the script
// never sweeps a series.
public class EventFormMarkupTests(WebApplicationFactoryBase factory)
    : IClassFixture<WebApplicationFactoryBase>, IAsyncLifetime
{
    private const string MobileUserAgent =
        "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1";

    private const string DesktopUserAgent =
        "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0 Safari/537.36";

    private static readonly Regex InlineScript = new(
        @"<script\b(?![^>]*\bsrc\s*=)[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public ValueTask DisposeAsync()
    {
        factory.TestGroupContext.ActiveGroupId = 1;
        factory.TestGroupContext.BoardType = BoardType.OneShot;
        return ValueTask.CompletedTask;
    }

    private async Task<string> GetBodyAsync(HttpClient client, string url, string userAgent)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("User-Agent", userAgent);
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
    }

    private async Task<HttpClient> DungeonMasterAsync(string name)
    {
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, name, name + "@example.com", roles: ["DungeonMaster"]);
        return client;
    }

    private static readonly Regex ModalFooter = new(
        @"<div class=""modal-footer"">.*?</div>", RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex Tag = new("<[^>]+>", RegexOptions.Compiled);

    private async Task<int> SeedEventAsync(bool inSeries)
    {
        await using var ctx = factory.Database.CreateContext();
        var date = DateOnly.FromDateTime(DateTime.Today);
        int? seriesId = null;
        if (inSeries)
        {
            var series = new EventSeriesEntity
            {
                Title = "Weekly Muster",
                StartTime = new TimeOnly(19, 0),
                AnchorDate = date,
                IntervalWeeks = 1,
                WeekDay = (int)date.DayOfWeek,
                CycleMask = "1",
                GroupId = 1,
                CreatedAt = DateTime.UtcNow
            };
            ctx.EventSeries.Add(series);
            await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
            seriesId = series.Id;
        }

        var eventEntity = new EventEntity
        {
            Title = inSeries ? "Weekly Muster" : "Council Session",
            GroupId = 1,
            Date = date,
            SeriesId = seriesId,
            SeriesSlotIndex = inSeries ? 0 : null,
            CreatedAt = DateTime.UtcNow
        };
        ctx.Events.Add(eventEntity);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        return eventEntity.Id;
    }

    [Theory]
    [InlineData("desktop")]
    [InlineData("phone")]
    public async Task Create_RecurrenceLabelTargetsTheCheckbox(string layout)
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var client = await DungeonMasterAsync("evform_label_" + layout);

        var body = await GetBodyAsync(client, "/Events/Create", layout == "phone" ? MobileUserAgent : DesktopUserAgent);

        body.Should().Contain("id=\"enableRecurrence\"");
        var label = Regex.Match(body, @"<label\b[^>]*>\s*Make this a recurring series\s*</label>");
        label.Success.Should().BeTrue("the recurrence checkbox has a visible label");
        label.Value.Should().Contain("for=\"enableRecurrence\"", "clicking the label has to toggle the checkbox and name it");
        body.Should().NotContain("for=\"IsRecurring\"", "no input carries that id");
    }

    [Theory]
    [InlineData("desktop")]
    [InlineData("phone")]
    public async Task Create_LoadsTheSeriesModuleAndNoInlineScript(string layout)
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var client = await DungeonMasterAsync("evform_module_" + layout);

        var body = await GetBodyAsync(client, "/Events/Create", layout == "phone" ? MobileUserAgent : DesktopUserAgent);

        body.Should().MatchRegex("<script type=\"module\" src=\"/js/event-series-form\\.js[^\"]*\"");
        InlineScript.Matches(body).Select(m => m.Value).Should().BeEmpty("the page carries no inline script");
        body.Should().NotContain("seriesAntiforgeryToken");
        body.Should().MatchRegex("<meta name=\"request-verification-token\" content=\"[^\"]+\"");
    }

    [Theory]
    [InlineData("desktop")]
    [InlineData("phone")]
    public async Task Edit_SeriesOccurrence_KeepsTheSafeScopeDefaultAndNoInlineScript(string layout)
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var eventId = await SeedEventAsync(inSeries: true);
        var client = await DungeonMasterAsync("evform_scope_" + layout);

        var body = await GetBodyAsync(client, $"/Events/Edit/{eventId}", layout == "phone" ? MobileUserAgent : DesktopUserAgent);

        var form = Regex.Match(body, @"<form\b[^>]*id=""editEventForm""[^>]*>");
        form.Success.Should().BeTrue();
        form.Value.Should().Contain($"data-event-id=\"{eventId}\"");
        form.Value.Should().Contain("data-collision-url=\"/Events/CheckOccurrenceCollision\"");

        body.Should().MatchRegex(
            "<input type=\"hidden\" name=\"EditScope\" id=\"EditScope\" value=\"" + (int)EventEditScope.OnlyThisEvent + "\"",
            "a form submitted without the script must only change this event");

        InlineScript.Matches(body).Select(m => m.Value).Should().BeEmpty("the page carries no inline script");
        body.Should().MatchRegex("<script type=\"module\" src=\"/js/event-series-form\\.js[^\"]*\"");
        body.Should().NotContain("RequestToken");

        var modal = body[body.IndexOf("id=\"saveScopeModal\"", StringComparison.Ordinal)..];
        foreach (var text in new[]
                 {
                     "Save this change",
                     "Update just this event, or this event and every future session in the series?",
                     "Cancel",
                     "This and all future events",
                     "Only this event"
                 })
            modal.Should().Contain(text);

        modal.Should().MatchRegex("id=\"saveScopeOnlyBtn\" data-scope-value=\"" + (int)EventEditScope.OnlyThisEvent + "\"");
        modal.Should().MatchRegex("id=\"saveScopeFutureBtn\" data-scope-value=\"" + (int)EventEditScope.ThisAndFutureEvents + "\"");
        Regex.IsMatch(modal, @"class=""btn btn-primary"" id=""saveScopeOnlyBtn""").Should().BeTrue("Only this event is the dialog's primary action");
    }

    [Theory]
    [InlineData("/Events/Create", "desktop", "Create Event")]
    [InlineData("/Events/Create", "phone", "Create Event")]
    [InlineData("EDIT", "desktop", "Save Changes")]
    [InlineData("EDIT", "phone", "Save Changes")]
    public async Task Forms_HaveOnePrimaryActionAndAnH1(string url, string layout, string commitLabel)
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var client = await DungeonMasterAsync($"evform_primary_{layout}_{commitLabel.Replace(' ', '_')}");
        if (url == "EDIT")
            url = $"/Events/Edit/{await SeedEventAsync(inSeries: true)}";

        var body = await GetBodyAsync(client, url, layout == "phone" ? MobileUserAgent : DesktopUserAgent);

        var outsideDialogs = ModalFooter.Replace(body, string.Empty);
        var primaries = Regex.Matches(outsideDialogs, @"<(?:a|button)\b[^>]*class=""[^""]*\bbtn-primary\b[^""]*""[^>]*>(?<inner>.*?)</(?:a|button)>", RegexOptions.Singleline);
        primaries.Should().ContainSingle("a form has one commit action");
        System.Net.WebUtility.HtmlDecode(Tag.Replace(primaries[0].Groups["inner"].Value, string.Empty)).Trim().Should().Be(commitLabel);

        var cancel = Regex.Match(outsideDialogs, @"<a\b[^>]*class=""btn btn-secondary""[^>]*>(?<inner>.*?)</a>", RegexOptions.Singleline);
        cancel.Success.Should().BeTrue("Cancel is the secondary action");
        Tag.Replace(cancel.Groups["inner"].Value, string.Empty).Trim().Should().Be("Cancel");

        var hr = outsideDialogs.LastIndexOf("<hr", StringComparison.Ordinal);
        hr.Should().BeGreaterThan(-1);
        hr.Should().BeLessThan(cancel.Index, "an hr precedes the action row");
        cancel.Index.Should().BeLessThan(primaries[0].Index, "Cancel sits left of the commit action");

        Regex.Matches(body, "<h1[ >]").Should().HaveCount(1, "the page title is the one h1");
    }

    [Theory]
    [InlineData("/Events/Create", "desktop")]
    [InlineData("EDIT", "desktop")]
    [InlineData("EDIT", "phone")]
    public async Task Forms_TitleColourFollowsTheGateAnswer(string url, string layout)
    {
        // The approved answer is the foundation's gold desktop title, so nothing is added to the page: the
        // quest form's parchment opt-in class must not appear on the event forms.
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var client = await DungeonMasterAsync($"evform_title_{layout}_{(url == "EDIT" ? "edit" : "create")}");
        if (url == "EDIT")
            url = $"/Events/Edit/{await SeedEventAsync(inSeries: false)}";

        var body = await GetBodyAsync(client, url, layout == "phone" ? MobileUserAgent : DesktopUserAgent);

        Regex.IsMatch(body, @"<div class=""card-header modern-card-header"">\s*<h1[ >]").Should().BeTrue(
            "the page title sits in the form card's header");
        body.Should().NotContain("quest-form-page");
    }

    [Theory]
    [InlineData("desktop")]
    [InlineData("phone")]
    public async Task Create_CycleControlsAreNamed(string layout)
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var client = await DungeonMasterAsync("evform_cycle_" + layout);

        var body = await GetBodyAsync(client, "/Events/Create", layout == "phone" ? MobileUserAgent : DesktopUserAgent);

        foreach (var (id, name) in new[]
                 {
                     ("seriesCycleAddBtn", "Add a cycle position"),
                     ("seriesCycleRemoveBtn", "Remove a cycle position")
                 })
        {
            var button = Regex.Match(body, $@"<button\b[^>]*id=""{id}""[^>]*>");
            button.Success.Should().BeTrue();
            button.Value.Should().Contain($"aria-label=\"{name}\"");
            button.Value.Should().Contain($"title=\"{name}\"");
            button.Value.Should().Contain("btn btn-secondary btn-sm", "the cycle buttons are filled secondary buttons");
            button.Value.Should().NotContain("btn-outline");
        }
    }
}
