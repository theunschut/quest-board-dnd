using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using QuestBoard.Domain.Models;
using QuestBoard.IntegrationTests.Helpers;
using QuestBoard.Service.Services;

namespace QuestBoard.IntegrationTests.Tests;

/// <summary>
/// Renders the Email Stats page through the real statistics client, aggregator and view, with the network replaced by
/// a canned handler and the key by a stub value, so the four figures can be measured without any real key. Also pins
/// where the named Resend client may point: the fixed Resend address everywhere except a Development host that
/// asks for another one.
/// </summary>
public class EmailStatsRenderTests :
    IClassFixture<EmailStatsWebApplicationFactory>,
    IClassFixture<WebApplicationFactoryBase>,
    IAsyncLifetime
{
    private static readonly Uri FixedAddress = new("https://api.resend.com/");

    private readonly EmailStatsWebApplicationFactory _factory;
    private readonly WebApplicationFactoryBase _defaultFactory;

    public EmailStatsRenderTests(EmailStatsWebApplicationFactory factory, WebApplicationFactoryBase defaultFactory)
    {
        _factory = factory;
        _defaultFactory = defaultFactory;
    }

    // Each host starts from the seeded default board, which the signed-in account needs to reach any page.
    public async ValueTask InitializeAsync()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        await TestDataHelper.ClearDatabaseAsync(_defaultFactory.Services);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static async Task<string> GetPageAsync(HttpClient client, string url = "/Admin/EmailStats")
    {
        var response = await client.GetAsync(url, TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    // The number shown in the tile that carries the given label, read from the markup rather than from any style.
    private static int TileFigure(string html, string label)
    {
        var match = Regex.Match(html, @"<div class=""fs-1[^""]*"">\s*(\d+)\s*</div>\s*<div class=""small text-muted"">\s*" + Regex.Escape(label) + @"\s*</div>");
        match.Success.Should().BeTrue($"the page must show a tile labelled '{label}'");
        return int.Parse(match.Groups[1].Value);
    }

    private async Task<HttpClient> SuperAdminAsync(string key)
    {
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(
            _factory, $"emailstats{key}", $"emailstats{key}@example.com");
        return client;
    }

    [Fact]
    public async Task EmailStats_WithCannedRecords_ShowsTheFourFiguresAndTheAsOfLine()
    {
        // Arrange — one in transit, three delivered (delivered, opened, clicked all count), two bounced, four failed.
        _factory.Handler.RespondWithRecords(
            "sent",
            "delivered", "opened", "clicked",
            "bounced", "bounced",
            "failed", "failed", "failed", "failed");
        var client = await SuperAdminAsync("figures");

        // Act
        var html = await GetPageAsync(client, "/Admin/EmailStats?force=true");

        // Assert
        TileFigure(html, "In Transit").Should().Be(1);
        TileFigure(html, "Delivered").Should().Be(3);
        TileFigure(html, "Bounced").Should().Be(2);
        TileFigure(html, "Failed").Should().Be(4);
        html.Should().Contain("Stats as of");
        html.Should().MatchRegex(@"Stats as of\s*<time class=""local-time""");
        html.Should().NotContain("not configured");
        html.Should().NotContain("Could not fetch email stats");
    }

    [Fact]
    public async Task EmailStats_UsesTheStubKeyAndNeverLeavesTheCannedHandler()
    {
        // Arrange
        _factory.Handler.RespondWithRecords("delivered");
        var client = await SuperAdminAsync("stubkey");

        // Act
        await GetPageAsync(client, "/Admin/EmailStats?force=true");

        // Assert — the only request seen carried the stub key, so nothing reached a real service with a real one.
        var last = _factory.Handler.Requests.Last();
        last.Authorization.Should().Be($"Bearer {EmailStatsWebApplicationFactory.StubApiKey}");
        last.Uri!.AbsoluteUri.Should().Be("https://api.resend.com/emails?limit=100");
    }

    [Fact]
    public async Task EmailStats_WhenTheServiceAnswersAnError_ShowsTheErrorAlertInsteadOfFigures()
    {
        // Arrange
        _factory.Handler.RespondWithStatus(HttpStatusCode.InternalServerError);
        var client = await SuperAdminAsync("apierror");

        // Act
        var html = await GetPageAsync(client, "/Admin/EmailStats?force=true");

        // Assert
        html.Should().Contain("Could not fetch email stats");
        html.Should().NotContain("Stats as of");
        html.Should().NotContain("In Transit");
    }

    [Fact]
    public async Task EmailStats_WithoutAConfiguredKey_ShowsTheNotConfiguredAlert()
    {
        // Arrange — the shared harness never sets a key.
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(
            _defaultFactory, "emailstatsnokey", "emailstatsnokey@example.com");

        // Act
        var html = await GetPageAsync(client);

        // Assert
        html.Should().Contain("ResendApiKey not configured");
        html.Should().NotContain("In Transit");
    }

    [Fact]
    public async Task EmailStats_ForceRefresh_BypassesTheCachedFigures()
    {
        // Arrange — a first forced read caches two delivered mails.
        _factory.Handler.RespondWithRecords("delivered", "delivered");
        var client = await SuperAdminAsync("force");
        TileFigure(await GetPageAsync(client, "/Admin/EmailStats?force=true"), "Delivered").Should().Be(2);

        // Act — the canned data changes; a plain read is served from the cache, a forced read is not.
        _factory.Handler.RespondWithRecords("delivered", "delivered", "delivered", "delivered", "delivered");
        var cached = await GetPageAsync(client);
        var forced = await GetPageAsync(client, "/Admin/EmailStats?force=true");

        // Assert
        TileFigure(cached, "Delivered").Should().Be(2, "within five minutes the page answers from the cache");
        TileFigure(forced, "Delivered").Should().Be(5, "a forced refresh must read the service again");
    }

    // ---- Where the named Resend client may point ---------------------------------------------------

    [Fact]
    public void TheDefaultTestingHost_UsesTheFixedResendAddress()
    {
        var client = _defaultFactory.Services.GetRequiredService<IHttpClientFactory>().CreateClient("Resend");

        client.BaseAddress.Should().Be(FixedAddress);
    }

    [Fact]
    public void ATestingHost_WithTheBaseUrlSettingPresent_StillUsesTheFixedResendAddress()
    {
        // The setting must really reach the host, or the fact below would pass for the wrong reason.
        using var factory = new EmailStatsWebApplicationFactory { ResendBaseUrlSetting = "http://localhost:59999/" };

        factory.Services.GetRequiredService<IOptions<EmailSettings>>().Value.ResendBaseUrl
            .Should().Be("http://localhost:59999/");
        factory.Services.GetRequiredService<IHttpClientFactory>().CreateClient("Resend").BaseAddress
            .Should().Be(FixedAddress, "only a Development host may honour the setting");
    }

    // A Production host cannot be started by this harness (it needs the real database and the mail settings the
    // startup check demands), so the Production rule is pinned on the decision itself, which the registration calls.
    [Theory]
    [InlineData("Production", "http://localhost:59999/", "https://api.resend.com/")]
    [InlineData("Testing", "http://localhost:59999/", "https://api.resend.com/")]
    [InlineData("Staging", "http://localhost:59999/", "https://api.resend.com/")]
    [InlineData("Development", null, "https://api.resend.com/")]
    [InlineData("Development", "   ", "https://api.resend.com/")]
    [InlineData("Development", "not an address", "https://api.resend.com/")]
    [InlineData("Development", "ftp://localhost/", "https://api.resend.com/")]
    [InlineData("Development", "http://localhost:59999/", "http://localhost:59999/")]
    public void ResendBaseAddress_IsTheConfiguredOneOnlyInDevelopment(string environmentName, string? configured, string expected)
    {
        var resolved = ResendBaseAddress.Resolve(new NamedEnvironment(environmentName), configured, FixedAddress);

        resolved.Should().Be(new Uri(expected));
    }

    private sealed class NamedEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "QuestBoard.Service";
        public string ContentRootPath { get; set; } = string.Empty;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
