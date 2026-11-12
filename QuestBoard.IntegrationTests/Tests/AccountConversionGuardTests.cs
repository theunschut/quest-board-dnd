using System.Text.RegularExpressions;

namespace QuestBoard.IntegrationTests.Tests;

/// <summary>
/// Static guard for the account and board-picker views once they are on the shared foundation: no inline
/// style, script or handler in a converted view, the retired phone twins stay gone, every page links its
/// own sheet through its Styles section, the picker rows stay native submit buttons, and no planning
/// reference is left in the area's files. An integration suite reading rendered markup cannot see a comment,
/// so only a scan of the files themselves holds those lines.
/// </summary>
public class AccountConversionGuardTests
{
    // The views already converted. Later conversions of the area add their views here.
    private static readonly string[] ConvertedViews =
    [
        "QuestBoard.Service/Views/GroupPicker/Index.cshtml",
    ];

    // Phone twins that were merged into their desktop view and must not come back.
    private static readonly string[] RetiredFiles =
    [
        "QuestBoard.Service/Views/GroupPicker/Index.Mobile.cshtml",
    ];

    // Each view with the sheet it must link through its own Styles section.
    private static readonly (string View, string Sheet)[] SheetLinks =
    [
        ("QuestBoard.Service/Views/GroupPicker/Index.cshtml", "group-picker.css"),
    ];

    // The area's sheets, scanned for planning references with the converted views.
    private static readonly string[] AreaSheets =
    [
        "QuestBoard.Service/wwwroot/css/group-picker.css",
    ];

    // Written as regex data, with each forbidden literal built from fragments, so this file never holds the
    // shapes it forbids elsewhere.
    private static readonly (string Label, Regex Pattern)[] PlanningReferencePatterns =
    [
        ("a decision id", new Regex(@"\bD-(0[1-9]|1[0-9]|2[01])\b")),
        ("a question id", new Regex(@"\bQ-(0[1-9]|1[0-9])\b")),
        ("a definition-of-converted id", new Regex("\\bDo" + "C-\\d+\\b")),
        ("a finding id", new Regex("\\bA-acc" + "ount-\\d+")),
        ("a phase reference", new Regex("Pha" + "se\\s+9" + "6\\b", RegexOptions.IgnoreCase)),
        ("a plan id", new Regex(@"\b9" + @"6-(0[1-9]|1[0-2])\b")),
        ("a planning document name", new Regex(
            "CONT" + "EXT\\.md|RESE" + "ARCH\\.md|UI-" + "SPEC|PATT" + "ERNS\\.md|VALID" + "ATION\\.md")),
    ];

    private static readonly (string Label, Regex Pattern)[] InlineCodePatterns =
    [
        ("an inline style attribute", new Regex(@"\sstyle\s*=", RegexOptions.IgnoreCase)),
        ("a style block", new Regex(@"<style\b", RegexOptions.IgnoreCase)),
        ("an inline script", new Regex(@"<script\b(?![^>]*\bsrc=)[^>]*>", RegexOptions.IgnoreCase)),
        ("an inline event handler", new Regex(@"\son[a-z]+\s*=", RegexOptions.IgnoreCase)),
    ];

    // The repository root is the directory holding the solution file, found before any path is joined, because
    // bin/ holds extension-less apphost binaries named like the project folders.
    private static string ResolveRepoFile(string relativePath) =>
        Path.Combine(FindRepositoryRoot().FullName, Path.Combine(relativePath.Split('/')));

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

    // The file without its Razor comments, which describe the markup in the same words it uses.
    private static string Markup(string relativePath) =>
        Regex.Replace(File.ReadAllText(ResolveRepoFile(relativePath)), @"@\*[\s\S]*?\*@", " ");

    [Fact]
    public void ConvertedViews_CarryNoInlineStyleScriptOrHandler()
    {
        ConvertedViews.Should().NotBeEmpty("the guard must cover at least the converted picker");

        foreach (var path in ConvertedViews)
        {
            File.Exists(ResolveRepoFile(path)).Should().BeTrue($"{path} is a converted view and must exist");
            var markup = Markup(path);
            foreach (var (label, pattern) in InlineCodePatterns)
                pattern.IsMatch(markup).Should().BeFalse($"{path} must not contain {label}");
        }
    }

    [Fact]
    public void RetiredFiles_AreGone()
    {
        RetiredFiles.Should().NotBeEmpty();

        foreach (var path in RetiredFiles)
            File.Exists(ResolveRepoFile(path)).Should().BeFalse($"{path} was merged into its desktop view and must stay deleted");
    }

    [Fact]
    public void AreaViews_LinkTheirSheetThroughTheStylesSection()
    {
        SheetLinks.Should().NotBeEmpty();

        foreach (var (view, sheet) in SheetLinks)
        {
            var markup = Markup(view);
            var section = Regex.Match(markup, @"@section\s+Styles\s*\{(?<body>[^}]*)\}");
            section.Success.Should().BeTrue($"{view} must declare a Styles section");
            section.Groups["body"].Value.Should().Contain($"~/css/{sheet}", $"{view} links its sheet through its Styles section");
            Regex.Matches(markup, Regex.Escape($"~/css/{sheet}")).Count.Should().Be(1, $"{view} links {sheet} exactly once");
        }
    }

    [Fact]
    public void Picker_RowsAreNativeSubmitButtonsInTheSelectGroupForm()
    {
        var markup = Markup("QuestBoard.Service/Views/GroupPicker/Index.cshtml");

        markup.Should().Contain("<button type=\"submit\"", "a board is chosen with a native submit button");
        markup.Should().Contain("group-picker-row");
        markup.Should().Contain("asp-action=\"SelectGroup\"");
        Regex.Matches(markup, "name=\"returnUrl\"").Count.Should().Be(1, "the return address is echoed in its hidden input only");
        markup.Should().NotContain("formaction", "the row submits its own form and never overrides the target");
        markup.Should().NotContain("data-return-url", "the return address is not echoed in an attribute");
    }

    [Fact]
    public void AreaFiles_CarryNoPlanningReference()
    {
        foreach (var path in ConvertedViews.Concat(AreaSheets))
        {
            if (!File.Exists(ResolveRepoFile(path)))
                continue;

            var text = File.ReadAllText(ResolveRepoFile(path));
            foreach (var (label, pattern) in PlanningReferencePatterns)
                pattern.IsMatch(text).Should().BeFalse($"{path} must not carry {label}");
        }
    }
}
