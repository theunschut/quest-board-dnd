using System.Text.RegularExpressions;
using FluentAssertions;

namespace QuestBoard.IntegrationTests.Tests;

/// <summary>
/// Static guards for the themed error pages. The lists below only ever grow as each slice adds files; a file a
/// later change deletes is removed from them in the same commit. An integration suite reading rendered markup cannot
/// see what a comment says or in what order the pipeline is built, so only a scan of the sources can hold the line.
/// </summary>
public class ErrorPagesGuardTests
{
    // The repository root is the directory holding the solution file, found before any path is joined, because bin/
    // holds extension-less apphost binaries named like the project folders.
    private static string ResolveRepoFile(params string[] segments) =>
        Path.Combine(FindRepositoryRoot().FullName, Path.Combine(segments));

    private static DirectoryInfo FindRepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (dir.EnumerateFiles("*.slnx").Any() || dir.EnumerateDirectories(".git").Any())
                return dir;

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException(
            $"Repository root not found: no directory containing a solution file or '.git' was " +
            $"found walking up from '{AppContext.BaseDirectory}'.");
    }

    private static string ReadRepoFile(string relativePath)
    {
        var path = ResolveRepoFile(relativePath.Split('/'));
        File.Exists(path).Should().BeTrue(
            $"{relativePath} is listed in this guard, so it must exist; remove it from the list in the commit that deletes it");
        return File.ReadAllText(path);
    }

    // Every source file this work creates or edits, repository-relative, scanned for planning references. This test
    // file is left out because it has to name the shapes it forbids.
    private static readonly string[] PhaseSourceFiles =
    [
        "QuestBoard.Service/Program.cs",
        "QuestBoard.Service/Helpers/BrowserNavigation.cs",
        "QuestBoard.Service/Middleware/ErrorPageGateMiddleware.cs",
        "QuestBoard.Service/Controllers/ErrorController.cs",
        "QuestBoard.Service/ViewModels/ErrorViewModels/ErrorPageViewModel.cs",
        "QuestBoard.Service/Views/Shared/ErrorPage.cshtml",
        "QuestBoard.Service/Views/Shared/_Layout.Error.cshtml",
        "QuestBoard.UnitTests/Helpers/BrowserNavigationTests.cs",
        "QuestBoard.IntegrationTests/Tests/ErrorPageRenderTests.cs",
        "QuestBoard.IntegrationTests/Tests/ErrorPageNavigationRuleTests.cs",
        "QuestBoard.IntegrationTests/Security/CrossBoardOracleParityTests.cs",
        "QuestBoard.IntegrationTests/Security/CrossBoardHiddenRecordTests.cs",
        "QuestBoard.IntegrationTests/Security/BoardPageCoverageTests.cs",
        "QuestBoard.Service/Controllers/Admin/AccountController.cs",
        "QuestBoard.IntegrationTests/Helpers/ErrorPageComparison.cs",
        "QuestBoard.IntegrationTests/Controllers/AccountControllerIntegrationTests.cs",
        "QuestBoard.IntegrationTests/Controllers/GroupSessionMiddlewareIntegrationTests.cs",
        "QuestBoard.IntegrationTests/Helpers/CapturingLoggerProvider.cs",
        "QuestBoard.IntegrationTests/Helpers/ThrowingServicesWebApplicationFactory.cs",
        "QuestBoard.IntegrationTests/Tests/ErrorPageServerErrorTests.cs",
        "QuestBoard.IntegrationTests/Tests/ErrorPageRateLimitTests.cs",
        "QuestBoard.IntegrationTests/Tests/ErrorPageFallbackLayoutTests.cs",
        "QuestBoard.Domain/Enums/CrossBoardAccess.cs",
        "QuestBoard.Domain/Models/CrossBoardViewerBoard.cs",
        "QuestBoard.Domain/Interfaces/ICrossBoardLinkResolver.cs",
        "QuestBoard.Domain/Interfaces/ICrossBoardLinkRepository.cs",
        "QuestBoard.Domain/Services/CrossBoardLinkResolverService.cs",
        "QuestBoard.Repository/CrossBoardLinkRepository.cs",
        "QuestBoard.Service/Helpers/CrossBoardLinkRegistry.cs",
        "QuestBoard.Service/Helpers/CrossBoardRouteTarget.cs",
        "QuestBoard.Service/Middleware/CrossBoardDeepLinkMiddleware.cs",
        "QuestBoard.Service/Controllers/GroupPickerController.cs",
        "QuestBoard.IntegrationTests/Security/CrossBoardAuthorizationBoundaryTests.cs",
        "QuestBoard.IntegrationTests/Middleware/CrossBoardRouteCoverageTests.cs",
        "QuestBoard.UnitTests/Helpers/CrossBoardLinkRegistryTests.cs",
        "QuestBoard.UnitTests/Helpers/CrossBoardRouteTargetTests.cs",
        "QuestBoard.UnitTests/Services/CrossBoardLinkResolverServiceTests.cs",
        "QuestBoard.Domain/Models/CrossBoardVisibilityScope.cs",
        "QuestBoard.Domain/Models/Shop/ShopItemAvailability.cs",
        "QuestBoard.Service/Controllers/Shop/ShopController.cs",
        "QuestBoard.UnitTests/Repository/CrossBoardLinkVisibilityTests.cs",
        "QuestBoard.UnitTests/Models/ShopItemAvailabilityTests.cs",
        "QuestBoard.IntegrationTests/Helpers/CrossBoardMatrixSeeder.cs",
        "QuestBoard.IntegrationTests/Security/CrossBoardVisibilityMatrixTests.cs",
        "QuestBoard.IntegrationTests/Controllers/CrossBoardPickerSkipTests.cs",
        "docs/server-setup.md",
    ];

    // Written as regex data, each forbidden shape built from fragments, so this file never holds the shapes it
    // forbids elsewhere.
    private static readonly (string Label, Regex Pattern)[] PlanningReferencePatterns =
    [
        ("a decision id", new Regex(@"\bD" + @"-(?:0[1-9]|[1-9][0-9])\b")),
        ("a question id", new Regex(@"\b(?:O)?Q" + @"-\d{1,3}\b")),
        ("a threat id", new Regex(@"\bT" + @"-10" + @"3-(?:\d{2}|SC)\b")),
        ("a phase reference", new Regex("Pha" + "se\\s+10" + "3\\b", RegexOptions.IgnoreCase)),
        ("a plan id", new Regex(@"\b10" + @"3-(?:\d\d|[A-Z]{2,})")),
        ("a requirement-style id", new Regex(@"\b(?:TENANT|EMAIL|SEC|ERR|UI)" + @"-\d{2}\b")),
        ("a planning document name", new Regex(
            "CONT" + "EXT\\.md|RESE" + "ARCH\\.md|UI-" + "SPEC|PATT" + "ERNS\\.md|VALID" + "ATION\\.md|AUD" + "IT\\.md|ROAD" + "MAP\\.md")),
    ];

    [Fact]
    public void PhaseSourceFiles_CarryNoPlanningReferences()
    {
        foreach (var path in PhaseSourceFiles)
        {
            var text = ReadRepoFile(path);
            foreach (var (label, pattern) in PlanningReferencePatterns)
                pattern.Matches(text).Select(m => m.Value).Should().BeEmpty(
                    $"{path} must not carry {label}: planning references go stale when the work closes");
        }
    }

    [Fact]
    public void PlanningScanner_FlagsEveryShapeAndAllowsPlainComments()
    {
        // Without a known-bad sample the guard above would pass on an empty list whether or not a scanner worked, so
        // the scanner is shown to fire on every shape here and to stay quiet on a clean sample.
        var planning = "// see " + "D" + "-07 and " + "Q" + "-12, " + "T" + "-103" + "-13, " + "Pha" + "se 103" + ", 103" + "-04 in "
            + "RESE" + "ARCH.md, " + "SEC" + "-02";
        PlanningReferencePatterns.Count(p => p.Pattern.IsMatch(planning)).Should().Be(PlanningReferencePatterns.Length);
        PlanningReferencePatterns.Count(p => p.Pattern.IsMatch("// every environment gets the themed status pages"))
            .Should().Be(0);
    }

    [Fact]
    public void Program_PlacesStatusPagesForEveryEnvironmentAndTheExceptionHandlerOutsideDevelopment()
    {
        var program = ReadRepoFile("QuestBoard.Service/Program.cs");

        var staticFiles = program.IndexOf("app.UseStaticFiles();", StringComparison.Ordinal);
        var statusPages = program.IndexOf("app.UseStatusCodePagesWithReExecute(", StringComparison.Ordinal);
        var gate = program.IndexOf("app.UseMiddleware<ErrorPageGateMiddleware>();", StringComparison.Ordinal);
        var routing = program.IndexOf("app.UseRouting();", StringComparison.Ordinal);
        var exceptionHandler = program.IndexOf("app.UseExceptionHandler(", StringComparison.Ordinal);
        var handlerOptions = program.IndexOf("ExceptionHandlingPath = \"/Error\"", StringComparison.Ordinal);
        var freshScope = program.IndexOf("CreateScopeForErrors = true", StringComparison.Ordinal);

        new[] { staticFiles, statusPages, gate, routing, exceptionHandler, handlerOptions, freshScope }.Should().OnlyContain(i => i >= 0);
        statusPages.Should().BeGreaterThan(staticFiles, "status pages answer for the files the static-file middleware does not serve");
        gate.Should().BeGreaterThan(statusPages, "the gate flips the feature the status pages middleware created");
        statusPages.Should().BeLessThan(routing, "the re-executed request must be routed again");

        var notDevelopment = program.IndexOf("if (!app.Environment.IsDevelopment())", StringComparison.Ordinal);
        notDevelopment.Should().BeGreaterThanOrEqualTo(0);
        var blockStart = program.IndexOf('{', notDevelopment);
        var blockEnd = BlockEnd(program, blockStart);

        exceptionHandler.Should().BeInRange(blockStart, blockEnd, "the exception handler belongs to the not-Development block");
        handlerOptions.Should().BeInRange(blockStart, blockEnd, "the handler's endpoint is set where the handler is registered");
        freshScope.Should().BeInRange(blockStart, blockEnd, "the handler's fresh scope is set where the handler is registered");
        program.Should().NotContain("UseExceptionHandler(\"/Error\")", "a bare path form would run the page in the failed request's scope");
        program.Should().NotContain("UseDeveloperExceptionPage", "the developer page stays the host's Development answer");
        statusPages.Should().NotBeInRange(blockStart, blockEnd, "status pages run in every environment, Development included");
        gate.Should().NotBeInRange(blockStart, blockEnd, "the gate runs wherever the status pages run");
    }

    private static int BlockEnd(string text, int openBrace)
    {
        var depth = 0;
        for (var i = openBrace; i < text.Length; i++)
        {
            if (text[i] == '{') depth++;
            else if (text[i] == '}' && --depth == 0) return i;
        }

        throw new InvalidOperationException("Unbalanced braces in Program.cs");
    }
}
