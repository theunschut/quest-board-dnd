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
}
