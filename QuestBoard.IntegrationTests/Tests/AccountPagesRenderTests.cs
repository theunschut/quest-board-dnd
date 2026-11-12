using System.Net;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using QuestBoard.IntegrationTests.Helpers;

namespace QuestBoard.IntegrationTests.Tests;

/// <summary>
/// Server-rendered checks of the account pages under both user agents: the merged access-denied page, the
/// wording the Profile twins share, the document titles of the twin pairs, the refusal line a failed log in
/// shows, and the retirement of the old Profile rules from the legacy sheet.
/// </summary>
public class AccountPagesRenderTests(WebApplicationFactoryBase factory) : IClassFixture<WebApplicationFactoryBase>
{
    private const string PhoneAgent =
        "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1";

    private const string DesktopAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

    private static async Task<(HttpResponseMessage Response, string Html)> SendAsync(
        HttpClient client, string url, string agent, AuthenticationHeaderValue? authorization = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("User-Agent", agent);
        if (authorization != null)
        {
            request.Headers.Authorization = authorization;
        }

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        return (response, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(DesktopAgent, false)]
    [InlineData(PhoneAgent, true)]
    public async Task AccessDenied_SignedOut_OffersTheLogInLinkWithTheLayoutOfTheUserAgent(string agent, bool phone)
    {
        var (response, html) = await SendAsync(factory.CreateClient(), "/Account/AccessDenied", agent);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().MatchRegex("<title>\\s*Access Denied - ");
        html.Should().Contain("account.css");
        Regex.Matches(html, "<h1\\b").Count.Should().Be(1);
        html.Should().Contain("href=\"/Account/Login\"");
        html.Should().Contain("to access this page");
        html.Should().NotContain("please contact an administrator");
        html.Contains("mobile-layout").Should().Be(phone);
    }

    [Theory]
    [InlineData(DesktopAgent, false)]
    [InlineData(PhoneAgent, true)]
    public async Task AccessDenied_SignedIn_AsksTheMemberToContactAnAdministratorInsteadOfOfferingLogIn(string agent, bool phone)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (authClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, $"denied_{suffix}", $"denied_{suffix}@test.com");

        var (response, html) = await SendAsync(factory.CreateClient(), "/Account/AccessDenied", agent,
            authClient.DefaultRequestHeaders.Authorization);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("please contact an administrator");
        html.Should().NotContain("to access this page");
        Regex.Matches(html, "<h1\\b").Count.Should().Be(1);
        html.Contains("mobile-layout").Should().Be(phone);
    }

    [Theory]
    [InlineData(DesktopAgent, "Administrator")]
    [InlineData(PhoneAgent, "Admin")]
    public async Task Profile_AdministratorRoleReadsInTheWordingOfItsLayout(string agent, string label)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (authClient, _) = await AuthenticationHelper.CreateAuthenticatedAdminClientAsync(
            factory, $"profadmin_{suffix}", $"profadmin_{suffix}@test.com");

        var (response, html) = await SendAsync(factory.CreateClient(), "/Account/Profile", agent,
            authClient.DefaultRequestHeaders.Authorization);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().MatchRegex($"shield-alt[^>]*></i>{label}</dd>");
    }

    [Theory]
    [InlineData(DesktopAgent)]
    [InlineData(PhoneAgent)]
    public async Task ProfileAndEdit_CarryTheirTitleAndTheBuildingKeyWordingUnderBothAgents(string agent)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (authClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, $"twin_{suffix}", $"twin_{suffix}@test.com");
        var client = factory.CreateClient();
        var authorization = authClient.DefaultRequestHeaders.Authorization;

        var (_, profile) = await SendAsync(client, "/Account/Profile", agent, authorization);
        var (_, edit) = await SendAsync(client, "/Account/Edit", agent, authorization);

        profile.Should().MatchRegex("<title>\\s*Profile - ");
        profile.Should().MatchRegex("(Has|No) Building Key");
        edit.Should().MatchRegex("<title>\\s*Edit Profile - ");
        edit.Should().Contain("Has Building Key");
        edit.Should().Contain("btn btn-primary");
        Regex.Matches(profile, "<h1\\b").Count.Should().Be(1);
        Regex.Matches(edit, "<h1\\b").Count.Should().Be(1);
    }

    [Theory]
    [InlineData(DesktopAgent, false)]
    [InlineData(PhoneAgent, true)]
    public async Task Login_WrongCredentials_ShowsTheRefusalLineAsAnAlertInTheLayoutOfTheUserAgent(string agent, bool phone)
    {
        var client = factory.CreateNonRedirectingClient();
        var get = await client.GetAsync("/Account/Login", TestContext.Current.CancellationToken);
        var (token, cookieValue) = await AntiForgeryHelper.ExtractAntiForgeryTokenAsync(get);

        var request = new HttpRequestMessage(HttpMethod.Post, "/Account/Login")
        {
            Content = AntiForgeryHelper.CreateFormContentWithAntiForgeryToken(new Dictionary<string, string>
            {
                ["Email"] = "nobody@example.com",
                ["Password"] = "Wrong-Password-1!",
                ["RememberMe"] = "false",
            }, token),
        };
        request.Headers.TryAddWithoutValidation("User-Agent", agent);
        if (!string.IsNullOrEmpty(cookieValue))
        {
            request.Headers.Add("Cookie", $".AspNetCore.Antiforgery={cookieValue}");
        }

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().MatchRegex("role=\"alert\"[^>]*class=\"refusal-line|class=\"refusal-line[^>]*role=\"alert\"");
        html.Should().Contain("Invalid login attempt");
        html.Contains("mobile-layout").Should().Be(phone);
    }

    [Fact]
    public void LegacySheet_NoLongerCarriesTheProfileOrCalendarSubscriptionRules()
    {
        var path = Path.Combine(FindRepoRoot(), "QuestBoard.Service", "wwwroot", "css", "legacy-pages.css");
        var css = File.ReadAllText(path);

        css.Should().NotContain("calendar-subscription");
        css.Should().NotContainEquivalentOf("profile-");
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "QuestBoard.sln"))
               && !File.Exists(Path.Combine(dir.FullName, "QuestBoard.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
