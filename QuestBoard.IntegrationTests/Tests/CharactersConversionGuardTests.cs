using System.Text.RegularExpressions;
using FluentAssertions;

namespace QuestBoard.IntegrationTests.Tests;

/// <summary>
/// Pins the characters, contacts, Dungeon Master profile and players views as they are moved onto
/// the layered foundation. A character's picture is requested through one shared avatar partial
/// only on the quest pages, and none of the files this work touches names the planning that
/// produced it. The lists below only ever grow as each view is converted; a file a later change
/// deletes is removed from them in the same commit. An integration suite reading rendered markup
/// cannot see what a comment says or which view built a picture address, so only a scan of the
/// sources can hold the line.
/// </summary>
public class CharactersConversionGuardTests
{
    // The repository root is the directory holding the solution file, found before any path is
    // joined, because bin/ holds extension-less apphost binaries named like the project folders.
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

    private const string ViewsRoot = "QuestBoard.Service/Views";
    private const string AvatarPartial = "QuestBoard.Service/Views/Shared/_CharacterAvatar.cshtml";

    // Every source file this work creates or edits, repository-relative, scanned for planning
    // references. This test file is left out because it has to name the shapes it forbids.
    private static readonly string[] PhaseSourceFiles =
    [
        "QuestBoard.Repository/QuestRepository.cs",
        "QuestBoard.Service/Views/Shared/_CharacterAvatar.cshtml",
        "QuestBoard.Service/ViewModels/Shared/CharacterAvatarViewModel.cs",
        "QuestBoard.Service/Views/Quest/Details.cshtml",
        "QuestBoard.Service/Views/Quest/Manage.cshtml",
        "QuestBoard.Service/Views/QuestLog/Details.cshtml",
        "QuestBoard.IntegrationTests/Tests/CharacterAvatarRenderTests.cs",
        "QuestBoard.UnitTests/Repository/QuestRepositoryPictureFlagTests.cs",
    ];

    // Written as regex data, each forbidden shape built from fragments, so this file never holds
    // the shapes it forbids elsewhere.
    private static readonly (string Label, Regex Pattern)[] PlanningReferencePatterns =
    [
        ("a decision id", new Regex(@"\bD" + @"-(?:0[1-9]|[1-9][0-9])\b")),
        ("a question id", new Regex(@"\b(?:O)?Q" + @"-\d{1,3}\b")),
        ("a merge or relayout id", new Regex(@"\bR" + @"-[1-9]\b")),
        ("a definition-of-converted id", new Regex(@"\bDo" + @"C-\d{1,2}\b")),
        ("an audit finding id", new Regex(@"\bA" + @"-(?:characters|quests|contacts|players)-\d{3}\b")),
        ("a threat id", new Regex(@"\bT" + @"-98-\d{2}\b")),
        ("a ledger number", new Regex("WIND" + "OWS\\s+(?:entry\\s+)?#?\\d+", RegexOptions.IgnoreCase)),
        ("a phase reference", new Regex("Pha" + "se\\s+9" + "8\\b", RegexOptions.IgnoreCase)),
        ("a plan id", new Regex(@"\b9" + @"8-(?:\d\d|[A-Z]{2,})")),
        ("a planning document name", new Regex(
            "CONT" + "EXT\\.md|RESE" + "ARCH\\.md|UI-" + "SPEC|PATT" + "ERNS\\.md|VALID" + "ATION\\.md|AUD" + "IT\\.md")),
    ];

    private static string ReadRepoFile(string relativePath)
    {
        var path = ResolveRepoFile(relativePath.Split('/'));
        File.Exists(path).Should().BeTrue(
            $"{relativePath} is listed in this guard, so it must exist; remove it from the list in the commit that deletes it");
        return File.ReadAllText(path);
    }

    // A view builds a character picture address by asking the characters controller for the cropped picture.
    private static int PictureUrlBuilds(string view) =>
        Regex.Matches(view, @"""GetCroppedPicture""").Count;

    private static IEnumerable<string> ViewsUnder(string folder) =>
        Directory.EnumerateFiles(ResolveRepoFile(ViewsRoot, folder), "*.cshtml", SearchOption.AllDirectories);

    // ---- Guards ---------------------------------------------------------------------------------

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
    public void Scanners_FlagWhatTheyForbidAndAllowWhatTheyPermit()
    {
        // Without a known-bad sample the guards above would pass on an empty list whether or not a
        // scanner worked, so each scanner is shown to fire here and to stay quiet on a clean sample.
        var planning = "// see " + "D" + "-04 and " + "Q" + "-51, " + "Pha" + "se 98" + ", 98" + "-05 in " + "RESE" + "ARCH.md, "
            + "R" + "-2, " + "Do" + "C-8, " + "A" + "-characters-012, " + "T" + "-98-11, " + "WIND" + "OWS #31";
        PlanningReferencePatterns.Count(p => p.Pattern.IsMatch(planning)).Should().Be(PlanningReferencePatterns.Length);
        PlanningReferencePatterns.Count(p => p.Pattern.IsMatch("// the placeholder shows when a character has no picture"))
            .Should().Be(0);

        PictureUrlBuilds("<img src=\"@Url.Action(\"GetCroppedPicture\", \"Characters\", new { id = 1 })\" />")
            .Should().Be(1);
        PictureUrlBuilds("@await Html.PartialAsync(\"_CharacterAvatar\", new CharacterAvatarViewModel())")
            .Should().Be(0);
        PictureUrlBuilds("<img src=\"@Url.Action(\"GetProfilePicture\", \"Characters\")\" />").Should().Be(0);
    }

    [Fact]
    public void QuestViews_RequestCharacterPicturesOnlyThroughTheAvatarPartial()
    {
        var offenders = ViewsUnder("Quest").Concat(ViewsUnder("QuestLog"))
            .Where(path => PictureUrlBuilds(File.ReadAllText(path)) > 0)
            .Select(path => Path.GetRelativePath(FindRepositoryRoot().FullName, path).Replace('\\', '/'))
            .ToList();
        offenders.Should().BeEmpty(
            "a quest page asks for a character picture only through the shared avatar partial, which knows whether one exists");

        PictureUrlBuilds(ReadRepoFile(AvatarPartial)).Should().Be(1,
            "the avatar partial builds the picture address in exactly one place");
    }
}
