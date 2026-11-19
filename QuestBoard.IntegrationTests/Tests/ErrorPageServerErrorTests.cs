using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;
using QuestBoard.IntegrationTests.Helpers;

namespace QuestBoard.IntegrationTests.Tests;

/// <summary>
/// An unhandled exception ends on the themed 500 with a reference the member can quote, shows nothing from the
/// exception, and the reference is the request id the handler logged.
/// </summary>
public class ErrorPageServerErrorTests(ThrowingQuestServiceFactory factory) : IClassFixture<ThrowingQuestServiceFactory>
{
    private static readonly Regex ReferenceElement = new(
        "<span class=\"error-reference\">(?<value>[^<]*)</span>", RegexOptions.Compiled);

    private async Task<HttpClient> SignedInClientAsync()
    {
        await TestDataHelper.SeedDefaultGroupAsync(factory.Services);
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, $"serverror_{suffix}", $"serverror_{suffix}@test.com");
        return client;
    }

    [Fact]
    public async Task UnhandledException_AsANavigation_AnswersTheThemedServerErrorPageWithAReference()
    {
        var client = await SignedInClientAsync();

        var (status, body) = await ErrorPageRenderTests.SendAsync(client, ErrorPageRenderTests.Navigation("/Quest/Details/1"));

        status.Should().Be(HttpStatusCode.InternalServerError);
        body.Should().Contain("Something went wrong");
        body.Should().Contain("Error 500");
        body.Should().Contain("Quote this reference if you ask for help.");
        var reference = ReferenceElement.Match(body);
        reference.Success.Should().BeTrue("the reference sits in its own element");
        reference.Groups["value"].Value.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task UnhandledException_ShowsNothingFromTheException()
    {
        var client = await SignedInClientAsync();

        var (_, body) = await ErrorPageRenderTests.SendAsync(client, ErrorPageRenderTests.Navigation("/Quest/Details/1"));

        body.Should().NotContain(ThrowingServicesWebApplicationFactory.FailureSentinel);
        body.Should().NotContain(nameof(SentinelFailureException));
        body.Should().NotContain("InvalidOperationException");
        body.Should().NotContain("   at ");
        body.Should().NotContain("QuestController");
    }

    [Fact]
    public async Task UnhandledException_ForAScriptCaller_AnswersAnEmptyServerError()
    {
        var client = await SignedInClientAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/Quest/Details/1");
        request.Headers.TryAddWithoutValidation("Accept", "*/*");

        var (status, body) = await ErrorPageRenderTests.SendAsync(client, request);

        status.Should().Be(HttpStatusCode.InternalServerError);
        body.Should().BeEmpty();
    }

    [Fact]
    public async Task UnhandledException_ReferenceIsTheTraceIdentifierTheHandlerLogged()
    {
        var client = await SignedInClientAsync();
        factory.SeenTraceIdentifiers.Clear();
        factory.LogCapture.Clear();

        var (_, body) = await ErrorPageRenderTests.SendAsync(client, ErrorPageRenderTests.Navigation("/Quest/Details/1"));

        var reference = ReferenceElement.Match(body).Groups["value"].Value;
        factory.SeenTraceIdentifiers.Should().ContainSingle().Which.Should().Be(reference);

        var handlerRecord = factory.LogCapture.Entries.Should().ContainSingle(entry =>
            entry.Level == LogLevel.Error && entry.Category.Contains("Diagnostics.ExceptionHandler")).Subject;
        handlerRecord.ScopePairs.Should().Contain(pair => pair.Key == "RequestId" && Equals(pair.Value, reference));
    }

    [Fact]
    public async Task ServerErrorReference_AppearsOnNoOtherVariant()
    {
        var client = await SignedInClientAsync();

        var (_, notFound) = await ErrorPageRenderTests.SendAsync(client, ErrorPageRenderTests.Navigation("/Error/404"));
        var (_, forbidden) = await ErrorPageRenderTests.SendAsync(client, ErrorPageRenderTests.Navigation("/Error/403"));

        notFound.Should().NotContain("error-reference");
        forbidden.Should().NotContain("error-reference");
    }
}

/// <summary>
/// The console log carries the request id as a scope only when the console logger is told to include scopes. The
/// service log is where a quoted reference is looked up, so the setup guide names the environment setting that
/// turns that on and this class proves the setting is honoured and written down. No code switches it on.
/// </summary>
public class ErrorPageLogScopeSettingTests(ThrowingQuestServiceWithConsoleScopesFactory factory)
    : IClassFixture<ThrowingQuestServiceWithConsoleScopesFactory>
{
    private const string SettingName = "Logging__Console__IncludeScopes";

    [Fact]
    public void ConsoleLogger_WhenTheDocumentedSettingIsSet_IncludesScopes()
    {
        // The key lives on the console provider's own options in this framework version; the simple formatter
        // reads it from there.
#pragma warning disable CS0618
        var options = factory.Services.GetRequiredService<IOptionsMonitor<ConsoleLoggerOptions>>().CurrentValue;
        options.IncludeScopes.Should().BeTrue();
#pragma warning restore CS0618
    }

    [Fact]
    public void ConsoleLogger_WithoutTheSetting_DoesNotIncludeScopesByDefault()
    {
        using var plain = new ThrowingQuestServiceFactory();
#pragma warning disable CS0618
        var options = plain.Services.GetRequiredService<IOptionsMonitor<ConsoleLoggerOptions>>().CurrentValue;
        options.IncludeScopes.Should().BeFalse(
            "the default console format omits scopes, which is why the setup guide names the setting");
#pragma warning restore CS0618
    }

    [Fact]
    public void SetupGuide_NamesTheSettingInItsEnvironmentFileSectionAndTheDockerPath()
    {
        var guide = File.ReadAllText(RepositoryFile("docs", "server-setup.md"));
        var start = guide.IndexOf("### Create the environment file", StringComparison.Ordinal);
        start.Should().BeGreaterThanOrEqualTo(0);
        var end = guide.IndexOf("### Create the systemd service", StringComparison.Ordinal);
        end.Should().BeGreaterThan(start);

        var section = guide[start..end];
        section.Should().Contain(SettingName + "=true");
        section.Should().Contain("docker-compose.yml");
    }

    [Fact]
    public void Program_AddsNoLoggingOption()
    {
        var program = File.ReadAllText(RepositoryFile("QuestBoard.Service", "Program.cs"));

        program.Should().NotContain("IncludeScopes");
        program.Should().NotContain("AddConsole");
        program.Should().NotContain("SimpleConsole");
    }

    private static string RepositoryFile(params string[] segments)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !dir.EnumerateFiles("*.slnx").Any() && !dir.EnumerateDirectories(".git").Any())
            dir = dir.Parent;

        dir.Should().NotBeNull("the repository root holds the solution file");
        return Path.Combine(dir!.FullName, Path.Combine(segments));
    }
}
