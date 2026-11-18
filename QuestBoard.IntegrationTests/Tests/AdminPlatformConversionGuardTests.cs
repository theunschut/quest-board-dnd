using System.Text.RegularExpressions;
using FluentAssertions;

namespace QuestBoard.IntegrationTests.Tests;

/// <summary>
/// Pins the Admin and Platform pages as they are moved onto the layered foundation, and the server rules that sit
/// behind them. The list below holds every source and test file this work creates or edits, and none of those files
/// may name the planning that produced it: a comment that cites a requirement or a plan goes stale the moment that
/// work closes. The list only ever grows as each file is touched; a file a later change deletes is removed from it
/// in the same commit. An integration suite reading rendered markup cannot see what a comment says, so only a scan of
/// the sources can hold the line.
/// </summary>
public class AdminPlatformConversionGuardTests
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

    // Every repository source or test file the Admin and Platform work creates or edits. Append-only.
    private static readonly string[] PhaseSourceFiles =
    [
        "QuestBoard.IntegrationTests/Controllers/AdminAccountReachTests.cs",
        "QuestBoard.IntegrationTests/Controllers/AdminRoleGuardTests.cs",
        "QuestBoard.IntegrationTests/Controllers/AdminQuestDeleteTests.cs",
        "QuestBoard.IntegrationTests/Controllers/AdminControllerRateLimitIntegrationTests.cs",
        "QuestBoard.IntegrationTests/Controllers/GroupManagementIntegrationTests.cs",
        "QuestBoard.IntegrationTests/Tests/AdminPlatformConversionGuardTests.cs",
        "QuestBoard.Domain/Enums/AccountReach.cs",
        "QuestBoard.Domain/Interfaces/IUserService.cs",
        "QuestBoard.Domain/Services/UserService.cs",
        "QuestBoard.Domain/Interfaces/IUserRepository.cs",
        "QuestBoard.Repository/UserRepository.cs",
        "QuestBoard.Service/Controllers/Admin/AdminController.cs",
        "QuestBoard.Service/ViewModels/AdminViewModels/AdminActionMessages.cs",
        "QuestBoard.UnitTests/Services/UserServiceTests.cs",
        "QuestBoard.Domain/Interfaces/IIdentityService.cs",
        "QuestBoard.Repository/IdentityService.cs",
        "QuestBoard.Service/Program.cs",
        "QuestBoard.Domain/Models/EmailSettings.cs",
        "QuestBoard.Service/Services/ResendBaseAddress.cs",
        "QuestBoard.Service/Services/AdminRateLimitKeys.cs",
        "QuestBoard.IntegrationTests/Helpers/CannedResendHandler.cs",
        "QuestBoard.IntegrationTests/Helpers/EmailStatsWebApplicationFactory.cs",
        "QuestBoard.IntegrationTests/Tests/EmailStatsRenderTests.cs",
        "QuestBoard.Repository/QuestRepository.cs",
        "QuestBoard.IntegrationTests/Tests/QuestDeleteSqlServerTests.cs",
        "QuestBoard.Domain/Enums/MemberChangeOutcome.cs",
        "QuestBoard.Domain/Interfaces/IGroupService.cs",
        "QuestBoard.Domain/Services/GroupService.cs",
        "QuestBoard.Domain/Interfaces/IGroupRepository.cs",
        "QuestBoard.Repository/GroupRepository.cs",
        "QuestBoard.UnitTests/Services/GroupServiceTests.cs",
        "QuestBoard.Service/Areas/Platform/Controllers/GroupController.cs",
        "QuestBoard.IntegrationTests/Controllers/AdminControllerIntegrationTests.cs",
        "QuestBoard.Service/ViewModels/AdminViewModels/AdminPlatformDisplay.cs",
        "QuestBoard.Service/ViewModels/AdminViewModels/QuestStatusBadgeModel.cs",
        "QuestBoard.Service/Views/Shared/_QuestStatusBadge.cshtml",
        "QuestBoard.Service/wwwroot/css/components.css",
        "QuestBoard.Service/wwwroot/css/admin-pages.css",
        "QuestBoard.Service/wwwroot/css/admin-pages.mobile.css",
        "QuestBoard.Service/Views/Admin/Quests.cshtml",
        "QuestBoard.Service/Views/Admin/Quests.Mobile.cshtml",
        "QuestBoard.IntegrationTests/Tests/AdminPlatformRenderTests.cs",
        "QuestBoard.IntegrationTests/Tests/UiFoundationLayerGuardTests.cs",
        "QuestBoard.IntegrationTests/Mobile/MobileViewsTests.cs",
        "QuestBoard.IntegrationTests/Controllers/QuestStatusBoardClockTests.cs",
    ];

    // Written as regex data, each forbidden shape built from fragments, so this file never holds the shapes it
    // forbids elsewhere.
    private static readonly (string Label, Regex Pattern)[] PlanningReferencePatterns =
    [
        ("a decision id", new Regex(@"\bD" + @"-(?:0[1-9]|[1-9][0-9])\b")),
        ("a question id", new Regex(@"\b(?:O)?Q" + @"-\d{1,3}\b")),
        ("an operator question id", new Regex(@"\bO" + @"-(?:[1-9]|1[0-9])\b")),
        ("a gap id", new Regex(@"\bG" + @"-\d{2}\b")),
        ("a merge or relayout id", new Regex(@"\bR" + @"-[1-9]\b")),
        ("a definition-of-converted id", new Regex(@"\bDo" + @"C-\d{1,2}\b")),
        ("an audit finding id", new Regex(@"\bA" + @"-(?:admin|shell|shop)-\d{3}\b")),
        ("a threat id", new Regex(@"\bT" + @"-\d{2,3}-(?:\d{2}|SC)\b")),
        ("a ledger number", new Regex("WIND" + "OWS\\s+(?:entry\\s+)?#?\\d+", RegexOptions.IgnoreCase)),
        ("a phase reference", new Regex("Pha" + "se\\s+(?:9[4-9]|10[0-9])\\b", RegexOptions.IgnoreCase)),
        ("a plan id", new Regex(@"\b(?:9[4-9]|10[0-9])" + @"-(?:\d\d|[A-Z]{2,})")),
        ("a planning document name", new Regex(
            "CONT" + "EXT\\.md|RESE" + "ARCH\\.md|UI-" + "SPEC|PATT" + "ERNS\\.md|VALID" + "ATION\\.md|AUD" + "IT\\.md|SECU" + "RITY\\.md|deferred-" + "items")),
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
        // the scanner is shown to fire here and to stay quiet on a clean sample.
        var planning = "// see " + "D" + "-04 and " + "Q" + "-51, " + "O" + "-3, " + "G" + "-01, " + "Pha" + "se 100" + ", 100" + "-05 in "
            + "RESE" + "ARCH.md, " + "R" + "-2, " + "Do" + "C-8, " + "A" + "-admin-012, " + "T" + "-1" + "00-13, " + "WIND" + "OWS #31";
        PlanningReferencePatterns.Count(p => p.Pattern.IsMatch(planning)).Should().Be(PlanningReferencePatterns.Length);
        PlanningReferencePatterns.Count(p => p.Pattern.IsMatch("// the board's Admin may reset a Player's password"))
            .Should().Be(0);
        PlanningReferencePatterns.Count(p => p.Pattern.IsMatch("// kept in " + "SECU" + "RITY.md and " + "deferred-" + "items"))
            .Should().Be(1);
    }

    [Fact]
    public void PhaseSourceFiles_AreAllListedOnDisk()
    {
        PhaseSourceFiles.Should().OnlyHaveUniqueItems();
        foreach (var path in PhaseSourceFiles)
            File.Exists(ResolveRepoFile(path.Split('/'))).Should().BeTrue($"{path} is listed, so it must exist");
    }

    // The Resend client's address is decided in one place, by the helper that honours a configured address only on a
    // Development host. The fixed address is written once, in the registration, and the setting is read nowhere else.
    [Fact]
    public void ResendClient_BaseAddressIsDecidedOnlyByTheDevelopmentGatedHelper()
    {
        var program = ReadRepoFile("QuestBoard.Service/Program.cs");

        Regex.Matches(program, Regex.Escape("https://api.resend.com/")).Count.Should().Be(1);
        Regex.Matches(program, @"\.ResendBaseUrl\b").Count.Should().Be(1);
        program.Should().MatchRegex(@"ResendBaseAddress\.Resolve\([^;]*ResendBaseUrl[^;]*https://api\.resend\.com/");

        var helper = ReadRepoFile("QuestBoard.Service/Services/ResendBaseAddress.cs");
        helper.Should().Contain("IsDevelopment()", "the configured address is honoured only on a Development host");
    }
}
