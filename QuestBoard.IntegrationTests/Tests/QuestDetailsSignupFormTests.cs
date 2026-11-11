using System.Net;
using System.Text.RegularExpressions;
using FluentAssertions;
using QuestBoard.IntegrationTests.Helpers;

namespace QuestBoard.IntegrationTests.Tests;

/// <summary>
/// Pins what the open-quest signup form renders. Choosing Spectator must hide only the date voting,
/// so the voting sits inside one marked wrapper that the page script toggles and that no other card
/// carries. Hiding itself needs a browser; what a rendered request can hold is that the marker is
/// in the right place and the instruction line is a polite live region.
/// </summary>
public class QuestDetailsSignupFormTests : IClassFixture<WebApplicationFactoryBase>
{
    private const string DesktopUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

    private static readonly Regex SignupFormStart = new(
        @"<form\b[^>]*\baction=""/Quest/Details(/\d+)?""[^>]*>",
        RegexOptions.Compiled);

    private readonly WebApplicationFactoryBase _factory;

    public QuestDetailsSignupFormTests(WebApplicationFactoryBase factory)
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

    private static string SignupForm(string html)
    {
        var start = SignupFormStart.Match(html);
        start.Success.Should().BeTrue("the open quest renders the signup form");
        var end = html.IndexOf("</form>", start.Index, StringComparison.Ordinal);
        end.Should().BeGreaterThan(start.Index);
        return html.Substring(start.Index, end - start.Index);
    }

    private async Task<int> SeedOpenQuestAsync(string dmName, string title)
    {
        var dm = await AuthenticationHelper.CreateTestUserAsync(
            _factory.Services, dmName, $"{dmName}@test.com", name: dmName);
        var quest = await TestDataHelper.CreateTestQuestAsync(_factory.Services, dm.Id, title);
        await TestDataHelper.CreateProposedDateAsync(_factory.Services, quest.Id, DateTime.UtcNow.AddDays(10));
        await TestDataHelper.CreateProposedDateAsync(_factory.Services, quest.Id, DateTime.UtcNow.AddDays(11));
        return quest.Id;
    }

    [Fact]
    public async Task DesktopOpenQuest_VoteSectionIsOneWrapperInsideTheSignupFormAndOutsideDmControls()
    {
        var questId = await SeedOpenQuestAsync("signupform_dm1", "Signup Form Desktop Quest");
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedAdminClientAsync(
            _factory, "signupform_admin1", "signupform_admin1@test.com", name: "Signupform Admin1");

        var html = await GetHtmlAsync(client, $"/Quest/Details/{questId}", DesktopUserAgent);

        Regex.Matches(html, "data-vote-section").Count.Should().Be(1, "one wrapper marks the date voting");
        SignupForm(html).Should().Contain("data-vote-section");

        var dmControls = html.IndexOf("DM Controls", StringComparison.Ordinal);
        dmControls.Should().BeGreaterThan(-1, "an admin who is not the quest's DM still sees the DM Controls card");
        html[dmControls..].Should().NotContain("data-vote-section");

        Regex.IsMatch(html, @"id=""votingInstruction""[^>]*role=""status""|role=""status""[^>]*id=""votingInstruction""")
            .Should().BeTrue("the instruction line is a polite live region");
    }
}
