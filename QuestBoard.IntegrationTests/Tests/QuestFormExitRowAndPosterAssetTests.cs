using System.Net;
using System.Text.RegularExpressions;
using FluentAssertions;
using QuestBoard.IntegrationTests.Helpers;
using QuestBoard.Service.Helpers;

namespace QuestBoard.IntegrationTests.Tests;

/// <summary>
/// Pins the quest form exit row (a rule between the fields and the buttons, Cancel on the left, the
/// commit button on the right, the same wording on both layouts) and that every poster and seal image
/// the board can name exists on disk in both formats.
/// </summary>
public class QuestFormExitRowAndPosterAssetTests : IClassFixture<WebApplicationFactoryBase>
{
    private const string DesktopUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

    private const string MobileUserAgent =
        "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1";

    private readonly WebApplicationFactoryBase _factory;

    public QuestFormExitRowAndPosterAssetTests(WebApplicationFactoryBase factory)
    {
        _factory = factory;
    }

    private static async Task<string> GetHtmlAsync(HttpClient client, string url, string userAgent)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("User-Agent", userAgent);
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK, $"{url} should render for this user");
        return await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
    }

    private static void AssertExitRow(string html, string cancelTarget)
    {
        var cancel = Regex.Match(html,
            @"<a\b[^>]*\bhref=""(?<href>[^""]*)""[^>]*>\s*<i\b[^>]*></i>\s*Cancel\s*</a>");
        cancel.Success.Should().BeTrue("the form ends in a Cancel link");
        cancel.Groups["href"].Value.Should().Be(cancelTarget, "Cancel leads back where it always did");

        var rule = html.LastIndexOf("<hr", cancel.Index, StringComparison.Ordinal);
        rule.Should().BeGreaterThan(-1, "a rule separates the fields from the buttons");
        rule.Should().BeLessThan(cancel.Index);

        var commit = Regex.Match(html[(cancel.Index + cancel.Length)..], @"<button\b[^>]*\btype=""submit""");
        commit.Success.Should().BeTrue("the commit button follows Cancel, so Cancel sits left and commit right");
        html[cancel.Index..].Should().NotContain("Back to ", "the exit is worded Cancel");
    }

    [Theory]
    [InlineData(DesktopUserAgent, "exitrow_dm1")]
    [InlineData(MobileUserAgent, "exitrow_dm2")]
    public async Task QuestCreate_BothLayouts_EndInARuleThenCancelThenCommit(string userAgent, string dmName)
    {
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedDMClientAsync(
            _factory, dmName, $"{dmName}@test.com", name: dmName);

        var html = await GetHtmlAsync(client, "/Quest/Create", userAgent);

        AssertExitRow(html, "/quests");
    }

    [Theory]
    [InlineData(DesktopUserAgent, "exitrow_dm3")]
    [InlineData(MobileUserAgent, "exitrow_dm4")]
    public async Task QuestEdit_BothLayouts_CancelLeadsToManage(string userAgent, string dmName)
    {
        var (client, dm) = await AuthenticationHelper.CreateAuthenticatedDMClientAsync(
            _factory, dmName, $"{dmName}@test.com", name: dmName);
        var quest = await TestDataHelper.CreateTestQuestAsync(_factory.Services, dm.Id, $"Exit Row Edit {dmName}");

        var html = await GetHtmlAsync(client, $"/Quest/Edit/{quest.Id}", userAgent);

        AssertExitRow(html, $"/Quest/Manage/{quest.Id}");
    }

    private static string RepoFile(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "QuestBoard.Service", "wwwroot")))
        {
            dir = dir.Parent;
        }
        dir.Should().NotBeNull("the repository root is above the test output");
        return Path.Combine(dir!.FullName, "QuestBoard.Service", "wwwroot", relative.TrimStart('/'));
    }

    [Fact]
    public void EveryPosterAndSeal_HasItsPngAndItsWebpOnDisk()
    {
        var urls = QuestPosterCatalog.All.SelectMany(p => new[] { p.PngUrl, p.WebpUrl })
            .Concat(QuestPosterCatalog.Seals.SelectMany(s => new[]
            {
                $"/images/Wax Seals/{s}.png", $"/images/Wax Seals/{s}.webp"
            }))
            .ToList();

        urls.Should().NotBeEmpty();
        foreach (var url in urls)
        {
            File.Exists(RepoFile(url)).Should().BeTrue($"{url} is named by the board and must exist");
        }
    }
}
