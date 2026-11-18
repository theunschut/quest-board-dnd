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
        "QuestBoard.IntegrationTests/Tests/AdminPlatformConversionGuardTests.cs",
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
}
