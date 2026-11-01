using System.Text.RegularExpressions;
using FluentAssertions;

namespace QuestBoard.IntegrationTests.Tests;

/// <summary>
/// Pins the layered foundation stylesheets: the order the five layouts link them in, that every
/// file opens its own cascade layer, and that the layered files stay free of important-flagged
/// declarations, colour literals and planning references. An integration suite reading rendered
/// markup cannot see which stylesheet a rule came from or what a comment says, so only a scan of
/// the sources can hold the line.
/// </summary>
public class UiFoundationLayerGuardTests
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

    private const string CssRoot = "QuestBoard.Service/wwwroot/css";

    // The five layouts, as repository-relative paths, and which of them are served to phones.
    private static readonly (string Path, bool IsMobile)[] Layouts =
    [
        ("QuestBoard.Service/Views/Shared/_Layout.cshtml", false),
        ("QuestBoard.Service/Views/Shared/_Layout.Mobile.cshtml", true),
        ("QuestBoard.Service/Views/Shared/_Layout.GroupPicker.cshtml", false),
        ("QuestBoard.Service/Areas/Platform/Views/Shared/_Layout.Platform.cshtml", false),
        ("QuestBoard.Service/Areas/Platform/Views/Shared/_Layout.Platform.Mobile.cshtml", true),
    ];

    // The foundation, in the order the layouts must link it.
    private static readonly string[] FoundationInOrder = ["app", "tokens", "base", "components", "shell"];

    // Each layered file and the layer it must open.
    private static readonly (string File, string Layer)[] LayeredFiles =
    [
        ("tokens.css", "tokens"),
        ("base.css", "base"),
        ("components.css", "components"),
        ("shell.css", "components"),
        ("shell.mobile.css", "components"),
    ];

    // The layered files that may hold no colour literal: every one except the token file.
    private static readonly string[] NoLiteralFiles = ["base.css", "components.css", "shell.css", "shell.mobile.css"];

    private static readonly Regex LocalStylesheetHref = new(
        @"<link\b[^>]*\brel=""stylesheet""[^>]*\bhref=""~/css/(?<name>[\w.\-]+)\.css""[^>]*>",
        RegexOptions.Compiled);

    private static string ReadRepoFile(string relativePath) =>
        File.ReadAllText(ResolveRepoFile(relativePath.Split('/')));

    private static IEnumerable<string> ExistingCssFiles(IEnumerable<string> names) =>
        names.Where(n => File.Exists(ResolveRepoFile((CssRoot + "/" + n).Split('/'))));

    private static string StripComments(string css) => Regex.Replace(css, @"/\*[\s\S]*?\*/", " ");

    private static string StripUrls(string css) =>
        Regex.Replace(css, @"url\(\s*(?:""[^""]*""|'[^']*'|[^)]*)\s*\)", "url()", RegexOptions.IgnoreCase);

    // The block preludes enclosing each position, outermost first. A position is the index of a
    // character in the text; the prelude of a block is the text between the previous brace or
    // semicolon and its opening brace, so a rule's selector or an at-rule's condition.
    private static List<string> EnclosingPreludes(string css, int position)
    {
        var stack = new List<string>();
        var preludeStart = 0;
        for (var i = 0; i < position && i < css.Length; i++)
        {
            switch (css[i])
            {
                case '{':
                    stack.Add(Regex.Replace(css[preludeStart..i].Trim(), @"\s+", " "));
                    preludeStart = i + 1;
                    break;
                case '}':
                    if (stack.Count > 0)
                        stack.RemoveAt(stack.Count - 1);
                    preludeStart = i + 1;
                    break;
                case ';':
                    preludeStart = i + 1;
                    break;
            }
        }

        return stack;
    }

    private static readonly Regex ReducedMotionPrelude = new(
        @"^@media\s*\(\s*prefers-reduced-motion\s*:\s*reduce\s*\)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Every selector in the innermost rule starts with .visually-hidden.
    private static bool IsVisuallyHiddenRule(string prelude) =>
        prelude.Split(',').Select(s => s.Trim()).All(s => s.StartsWith(".visually-hidden", StringComparison.Ordinal));

    // Positions in the (comment-stripped) text of important-flagged declarations that are not
    // inside the reduced-motion block or a visually-hidden helper rule.
    private static List<int> ImportantOutsideAllowedBlocks(string strippedCss)
    {
        var offenders = new List<int>();
        foreach (Match m in Regex.Matches(strippedCss, @"!\s*important", RegexOptions.IgnoreCase))
        {
            var preludes = EnclosingPreludes(strippedCss, m.Index);
            var allowed = preludes.Any(p => ReducedMotionPrelude.IsMatch(p))
                          || (preludes.Count > 0 && IsVisuallyHiddenRule(preludes[^1]));
            if (!allowed)
                offenders.Add(m.Index);
        }

        return offenders;
    }

    // Written as regex data so this file never holds a literal it forbids elsewhere: a hex colour
    // of 3, 4, 6 or 8 digits (not an id selector with a longer name), and the colour functions.
    private static readonly (string Label, Regex Pattern)[] ColourLiteralPatterns =
    [
        ("a hex colour", new Regex(
            @"#(?:[0-9a-fA-F]{8}|[0-9a-fA-F]{6}|[0-9a-fA-F]{4}|[0-9a-fA-F]{3})(?![\w-])", RegexOptions.Compiled)),
        ("an rgb() colour", new Regex(@"\brgba?\(", RegexOptions.Compiled | RegexOptions.IgnoreCase)),
        ("an hsl() colour", new Regex(@"\bhsla?\(", RegexOptions.Compiled | RegexOptions.IgnoreCase)),
    ];

    // Written as regex data, with each forbidden literal built from fragments, so this file never
    // holds the shapes it forbids elsewhere.
    private static readonly (string Label, Regex Pattern)[] PlanningReferencePatterns =
    [
        ("a decision id", new Regex(@"\bD-(0[1-9]|1[0-9]|2[0-4])\b")),
        ("a sign-off item id", new Regex(@"\bG" + @"1-[OR]\d\b")),
        ("a phase reference", new Regex("Pha" + "se\\s+9" + "4\\b", RegexOptions.IgnoreCase)),
        ("a plan id", new Regex(@"\b9" + @"4-(?:\d\d|[A-Z]{2,})")),
        ("a planning document name", new Regex(
            "CONT" + "EXT\\.md|RESE" + "ARCH\\.md|UI-" + "SPEC|PATT" + "ERNS\\.md|VALID" + "ATION\\.md|AUD" + "IT\\.md")),
    ];

    // The files the planning-reference scan reads, repository-relative. A file that does not exist
    // is skipped, so the list can name a file before the change that introduces it has merged.
    private static IEnumerable<string> PlanningScanFiles() =>
        new[] { "app.css" }
            .Concat(LayeredFiles.Select(l => l.File))
            .Select(n => CssRoot + "/" + n)
            .Concat(Layouts.Select(l => l.Path))
            .Where(p => File.Exists(ResolveRepoFile(p.Split('/'))));

    [Fact]
    public void Layouts_LinkFoundationInCascadeOrder()
    {
        foreach (var (path, isMobile) in Layouts)
        {
            var text = ReadRepoFile(path);
            var links = LocalStylesheetHref.Matches(text)
                .Select(m => (Name: m.Groups["name"].Value, Index: m.Index))
                .ToList();

            int IndexOf(string name)
            {
                var hit = links.FirstOrDefault(l => l.Name == name);
                hit.Name.Should().NotBeNull($"{path} must link ~/css/{name}.css");
                return hit.Index;
            }

            var previous = -1;
            var previousName = "the start of the file";
            foreach (var name in FoundationInOrder)
            {
                var index = IndexOf(name);
                index.Should().BeGreaterThan(previous, $"{path} must link {name}.css after {previousName}");
                previous = index;
                previousName = name + ".css";
            }

            if (isMobile)
            {
                var shellMobile = IndexOf("shell.mobile");
                shellMobile.Should().BeGreaterThan(previous, $"{path} must link shell.mobile.css after shell.css");
                previous = shellMobile;
            }
            else
            {
                links.Any(l => l.Name == "shell.mobile").Should().BeFalse(
                    $"{path} is a desktop layout and must not link shell.mobile.css");
            }

            var foundation = FoundationInOrder.Append("shell.mobile").ToHashSet();
            var firstLegacy = links.FirstOrDefault(l => !foundation.Contains(l.Name));
            firstLegacy.Name.Should().NotBeNull($"{path} must still link its legacy page stylesheets");
            firstLegacy.Index.Should().BeGreaterThan(
                previous, $"{path} must link the foundation before its first legacy sheet ({firstLegacy.Name}.css)");

            var styles = text.IndexOf("RenderSectionAsync(\"Styles\"", StringComparison.Ordinal);
            styles.Should().BeGreaterThan(-1, $"{path} must render the Styles section");
            styles.Should().BeGreaterThan(
                links[^1].Index, $"{path} must render the Styles section after the last stylesheet link");
        }
    }

    [Fact]
    public void DesktopLayouts_LinkNoMobileStylesheet()
    {
        foreach (var (path, _) in Layouts.Where(l => !l.IsMobile))
            ReadRepoFile(path).Should().NotContain("mobile.css",
                $"{path} is served to desktop browsers, which must never be sent a phone stylesheet");
    }

    [Fact]
    public void LayeredFiles_DeclareTheirLayer()
    {
        foreach (var (file, layer) in LayeredFiles)
        {
            var css = StripComments(ReadRepoFile(CssRoot + "/" + file)).TrimStart();
            css.Should().StartWith($"@layer {layer} {{", $"{file} must open the {layer} layer");
        }
    }

    [Fact]
    public void LayeredFiles_HaveNoImportantOutsideAllowedBlocks()
    {
        foreach (var (file, _) in LayeredFiles)
        {
            var css = StripComments(ReadRepoFile(CssRoot + "/" + file));
            var offenders = ImportantOutsideAllowedBlocks(css);
            offenders.Should().BeEmpty(
                $"{file} may use an important flag only inside the reduced-motion block or a visually-hidden helper, " +
                $"found at: {string.Join(" | ", offenders.Select(o => css[Math.Max(0, o - 40)..o].Trim()))}");
        }
    }

    [Fact]
    public void LayeredFiles_HaveNoColourLiteralOutsideTokens()
    {
        foreach (var file in ExistingCssFiles(NoLiteralFiles))
        {
            var css = StripUrls(StripComments(ReadRepoFile(CssRoot + "/" + file)));
            foreach (var (label, pattern) in ColourLiteralPatterns)
                pattern.Matches(css).Select(m => m.Value).Should().BeEmpty(
                    $"{file} must name colours by token; {label} belongs in tokens.css");
        }
    }

    [Fact]
    public void FoundationFiles_CarryNoPlanningReferences()
    {
        foreach (var path in PlanningScanFiles())
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
        // Without a known-bad sample the guards above would pass on an empty file whether or not
        // the scanner worked, so each scanner is shown to fire here.
        ImportantOutsideAllowedBlocks(".a { color: red !important; }").Should().HaveCount(1);
        ImportantOutsideAllowedBlocks(
                "@media (prefers-reduced-motion: reduce) { .a { transition: none !important; } }")
            .Should().BeEmpty();
        ImportantOutsideAllowedBlocks(".visually-hidden, .visually-hidden-focusable { clip: rect(0,0,0,0) !important; }")
            .Should().BeEmpty();
        ImportantOutsideAllowedBlocks("@media (min-width: 600px) { .a { margin: 0 !important; } }")
            .Should().HaveCount(1);

        var sample = StripUrls(StripComments(
            ".a { color: #abc; background: rgba(0,0,0,.5); border-color: hsl(1,2%,3%); " +
            "background-image: url(\"x#fff.png\"); } /* #123456 */ #main-content { top: 0 }"));
        ColourLiteralPatterns.Select(p => p.Pattern.Matches(sample).Count).Should().Equal(1, 1, 1);

        var planning = "// " + "D-" + "06 and " + "Pha" + "se 9" + "4 and 9" + "4-18";
        PlanningReferencePatterns.Count(p => p.Pattern.IsMatch(planning)).Should().BeGreaterThanOrEqualTo(3);
    }
}
