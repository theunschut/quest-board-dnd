using System.Globalization;
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

    // Each layered file and the layer it must open. The quest board, quest log, move and shared Markdown sheets are
    // page sheets: each opens the pages layer and each is linked by the views that use it, not by a layout.
    private static readonly (string File, string Layer)[] LayeredFiles =
    [
        ("tokens.css", "tokens"),
        ("base.css", "base"),
        ("components.css", "components"),
        ("shell.css", "components"),
        ("shell.mobile.css", "components"),
        ("group-picker.css", "pages"),
        ("quests.css", "pages"),
        ("quest-details.css", "pages"),
        ("quest-vote.css", "pages"),
        ("quests.mobile.css", "pages"),
        ("home.mobile.css", "pages"),
        ("quest-form.css", "pages"),
        ("dm-create.mobile.css", "pages"),
        ("quest-manage.css", "pages"),
        ("dm-manage.mobile.css", "pages"),
        ("quest-log.css", "pages"),
        ("quest-log.mobile.css", "pages"),
        ("move-entry.css", "pages"),
        ("markdown-content.css", "pages"),
        ("markdown-editor.css", "pages"),
    ];

    // The layered files that may hold no colour literal: every one except the token file.
    private static readonly string[] NoLiteralFiles = ["base.css", "components.css", "shell.css", "shell.mobile.css", "group-picker.css",
        "quests.css", "quest-details.css", "quest-vote.css", "quests.mobile.css", "home.mobile.css", "quest-form.css", "dm-create.mobile.css", "quest-manage.css", "dm-manage.mobile.css", "quest-log.css", "quest-log.mobile.css", "move-entry.css", "markdown-content.css", "markdown-editor.css"];

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
            .Append(".claude/ui-guidelines.md")
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
    public void LegacyDesktopSheet_IsUnlayeredAndRenamed()
    {
        // The remainder of the old global desktop sheet keeps beating every layered rule on its own selectors,
        // which is what keeps the pages that have not been converted yet looking as they did.
        File.Exists(ResolveRepoFile((CssRoot + "/legacy-pages.css").Split('/'))).Should().BeTrue(
            "the desktop page rules live in legacy-pages.css");
        StripComments(ReadRepoFile(CssRoot + "/legacy-pages.css")).Should().NotContain("@layer",
            "legacy-pages.css stays unlayered so it keeps winning on its own selectors");
        File.Exists(ResolveRepoFile((CssRoot + "/site.css").Split('/'))).Should().BeFalse(
            "the global sheet was renamed, so no file by the old name may remain");
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

    // ---- Token contrast -------------------------------------------------------------------------

    // The lightest pixel of the board backdrop. Every translucent surface is measured over it,
    // because that is where light text on glass is weakest.
    private const string LightestBoard = "#794317";

    private const double TextBar = 4.5;
    private const double NonTextBar = 3.0;

    private static readonly string[] StatusNames = ["success", "danger", "warning", "info", "event", "neutral"];

    // Text colours that sit on glass, the translucent card surface.
    private static readonly string[] GlassText =
    [
        "--qb-fg", "--qb-fg-heading", "--qb-fg-muted", "--qb-link", "--qb-link-hover", "--qb-accent-text",
        "--qb-success-text", "--qb-danger-text", "--qb-warning-text", "--qb-info-text",
        "--qb-event-text", "--qb-neutral-text",
    ];

    /// <summary>
    /// One row of the contrast table: these foregrounds, on these surfaces, at least this ratio.
    /// A surface is either a token naming an opaque fill, or one of the stacks the shell builds
    /// (glass, glass-hover, glass-nested, chrome, overlay, board, fallback).
    /// </summary>
    private sealed record ContrastPair(
        string Rule, string[] Foregrounds, string[] Surfaces, double Minimum, bool BestOfForegrounds = false);

    // The pair table. Reading it top to bottom is reading the accessibility contract.
    private static readonly ContrastPair[] ContrastTable =
    [
        // Rule                                            Foregrounds                    Surfaces                                                  Min
        new("Text on glass, at rest and nested",           GlassText,                     ["glass", "glass-nested"],                                TextBar),
        new("Text on hovered glass",                       GlassText,                     ["glass-hover"],                                          TextBar),
        new("Parchment on the wood chrome and in overlays", ["--qb-fg"],                  ["chrome", "--qb-surface-overlay"],                       TextBar),
        new("Status text and alert stripes on the overlay", [.. StatusNames.Select(n => $"--qb-{n}-text"), "--qb-accent-text"], ["--qb-surface-overlay"], TextBar),
        new("Light text on the bare board and its fallback", ["--qb-fg-on-board"],        ["board", "fallback"],                                    TextBar),
        new("Ink on paper",                                ["--qb-ink", "--qb-ink-muted"], ["--qb-surface-paper", "--qb-surface-paper-alt"],        TextBar),
        new("Label on gold",                               ["--qb-accent-ink"],           ["--qb-accent", "--qb-accent-strong"],                    TextBar),
        new("Primary action label",                        ["--qb-action-fg"],            ["--qb-action", "--qb-action-hover"],                     TextBar),
        .. StatusNames.Select(n => new ContrastPair(
            $"The {n} label on its fill", [$"--qb-{n}-fg"], [$"--qb-{n}"], TextBar)),
        new("Control border on the control's own surface", ["--qb-control-border"],       ["--qb-control-bg", "--qb-surface-paper", "--qb-surface-paper-alt"], NonTextBar),
        new("Focus ring: the better of its two strokes",   ["--qb-focus-inner", "--qb-focus-outer"],
            ["glass", "glass-hover", "chrome", "--qb-surface-paper", "--qb-surface-paper-alt", "--qb-control-bg", "--qb-surface-overlay", "board"],
            NonTextBar, BestOfForegrounds: true),
    ];

    private static readonly Regex Declaration = new(@"(--[\w-]+)\s*:\s*([^;]+);", RegexOptions.Compiled);

    // The text between the braces of the first block opened by exactly this selector.
    private static string ExtractBlock(string css, string selector)
    {
        var open = Regex.Match(css, @"(?<=^|[\s{};])" + Regex.Escape(selector) + @"\s*\{");
        open.Success.Should().BeTrue($"tokens.css must declare a '{selector}' block");

        var depth = 1;
        var i = open.Index + open.Length;
        var start = i;
        for (; i < css.Length && depth > 0; i++)
        {
            if (css[i] == '{') depth++;
            else if (css[i] == '}') depth--;
        }

        return css[start..(i - 1)];
    }

    private static Dictionary<string, string> ParseDeclarations(string block) =>
        Declaration.Matches(block).ToDictionary(
            m => m.Groups[1].Value,
            m => Regex.Replace(m.Groups[2].Value.Trim(), @"\s+", " "));

    private static (Dictionary<string, string> Root, Dictionary<string, string> Shop) ReadTokens()
    {
        var css = StripComments(ReadRepoFile(CssRoot + "/tokens.css"));
        var root = ParseDeclarations(ExtractBlock(css, ":root"));
        var shop = new Dictionary<string, string>(root);
        foreach (var (name, value) in ParseDeclarations(ExtractBlock(css, "[data-theme='shop']")))
            shop[name] = value;

        return (root, shop);
    }

    // Splits on commas that are not inside parentheses.
    private static List<string> SplitTopLevel(string text)
    {
        var parts = new List<string>();
        var depth = 0;
        var start = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '(') depth++;
            else if (text[i] == ')') depth--;
            else if (text[i] == ',' && depth == 0)
            {
                parts.Add(text[start..i].Trim());
                start = i + 1;
            }
        }

        parts.Add(text[start..].Trim());
        return parts;
    }

    // Reduces a colour expression to a literal WcagContrast can parse: follows var() chains and
    // mixes color-mix(in srgb, ...). Anything else is returned unchanged and rejected by Parse,
    // which is the intended failure for a contrast-bearing token that is not a literal.
    private static string ResolveColour(IReadOnlyDictionary<string, string> tokens, string expression, string owner, int depth = 0)
    {
        depth.Should().BeLessThan(16, $"{owner} must not refer to itself through var()");
        var expr = expression.Trim();

        var variable = Regex.Match(expr, @"^var\(\s*(?<name>--[\w-]+)\s*(?:,(?<fallback>.*))?\)$");
        if (variable.Success)
        {
            var name = variable.Groups["name"].Value;
            if (tokens.TryGetValue(name, out var next))
                return ResolveColour(tokens, next, name, depth + 1);

            if (variable.Groups["fallback"].Success)
                return ResolveColour(tokens, variable.Groups["fallback"].Value, owner, depth + 1);

            throw new InvalidOperationException($"{owner} refers to {name}, which tokens.css does not declare.");
        }

        var mix = Regex.Match(expr, @"^color-mix\(\s*in srgb\s*,(?<args>.*)\)$", RegexOptions.IgnoreCase);
        if (mix.Success)
        {
            var args = SplitTopLevel(mix.Groups["args"].Value);
            if (args.Count != 2)
                throw new InvalidOperationException($"{owner}: color-mix needs exactly two colours, found '{expr}'.");

            (WcagContrast.Rgba Colour, double? Share) Operand(string arg)
            {
                var m = Regex.Match(arg, @"^(?<colour>.+?)(?:\s+(?<pct>\d+(?:\.\d+)?)%)?$");
                var colour = WcagContrast.Parse(ResolveColour(tokens, m.Groups["colour"].Value, owner, depth + 1));
                return (colour, m.Groups["pct"].Success
                    ? double.Parse(m.Groups["pct"].Value, CultureInfo.InvariantCulture) / 100.0
                    : null);
            }

            var (a, shareA) = Operand(args[0]);
            var (b, shareB) = Operand(args[1]);
            var p = shareA ?? (shareB.HasValue ? 1 - shareB.Value : 0.5);
            var q = shareB ?? 1 - p;
            var alpha = a.A * p + b.A * q;
            double Channel(double x, double y) => alpha <= 0 ? 0 : (x * a.A * p + y * b.A * q) / alpha;
            return string.Create(CultureInfo.InvariantCulture,
                $"rgba({Channel(a.R, b.R):0.####}, {Channel(a.G, b.G):0.####}, {Channel(a.B, b.B):0.####}, {alpha:0.####})");
        }

        return expr;
    }

    // The colour stops of a token: one for a flat colour, one per stop for a linear gradient, so a
    // gradient is measured at its worst stop.
    private static List<string> ResolveStops(IReadOnlyDictionary<string, string> tokens, string name)
    {
        tokens.ContainsKey(name).Should().BeTrue($"tokens.css must declare {name}");
        var value = tokens[name];
        var gradient = Regex.Match(value, @"^linear-gradient\((?<args>.*)\)$", RegexOptions.IgnoreCase);
        if (!gradient.Success)
            return [ResolveColour(tokens, value, name)];

        return SplitTopLevel(gradient.Groups["args"].Value)
            .Where(arg => !Regex.IsMatch(arg, @"^(to\s|-?[\d.]+(deg|turn|rad|grad))", RegexOptions.IgnoreCase))
            .Select(arg => Regex.Replace(arg, @"\s+-?[\d.]+(%|px)$", ""))
            .Select(arg => ResolveColour(tokens, arg, name))
            .ToList();
    }

    // The surfaces a key stands for. Each is the layers under the text, bottom first, and the
    // opaque colour they sit on.
    private static List<(string Label, List<string> Layers, string Board)> ResolveSurfaces(
        IReadOnlyDictionary<string, string> tokens, string key)
    {
        string One(string name) => ResolveColour(tokens, $"var({name})", name);

        switch (key)
        {
            case "glass":
                return [("glass", [One("--qb-surface-scrim"), One("--qb-surface-glass")], LightestBoard)];
            case "glass-hover":
                return [("hovered glass", [One("--qb-surface-scrim"), One("--qb-surface-glass-hover")], LightestBoard)];
            case "glass-nested":
                return [("glass inside glass", [One("--qb-surface-scrim"), One("--qb-surface-glass"), One("--qb-surface-scrim")], LightestBoard)];
            case "chrome":
                return ResolveStops(tokens, "--qb-surface-chrome")
                    .Select((stop, i) => ($"chrome stop {i + 1}", new List<string> { stop }, LightestBoard))
                    .ToList();
            case "board":
                return [("the lightest board pixel", [], LightestBoard)];
            case "fallback":
                return [("the fallback colour", [], One("--qb-board-fallback"))];
            default:
                return [(key, [One(key)], LightestBoard)];
        }
    }

    // Every pair in the table that misses its bar for this token set, and how many were measured.
    private static (List<string> Failures, int Measured) ScoreTokenPairs(
        IReadOnlyDictionary<string, string> tokens, string label)
    {
        var failures = new List<string>();
        var measured = 0;
        foreach (var pair in ContrastTable)
        foreach (var surfaceKey in pair.Surfaces)
        foreach (var surface in ResolveSurfaces(tokens, surfaceKey))
        {
            var ratios = pair.Foregrounds
                .Select(fg => (Name: fg, Ratio: WcagContrast.TextOn(
                    ResolveColour(tokens, $"var({fg})", fg), surface.Layers, surface.Board)))
                .ToList();
            var scored = pair.BestOfForegrounds
                ? new List<(string Name, double Ratio)> { ratios.MaxBy(r => r.Ratio) }
                : ratios;
            foreach (var (name, ratio) in scored)
            {
                measured++;
                if (ratio + 1e-9 < pair.Minimum)
                    failures.Add($"[{label}] {pair.Rule}: {name} on {surface.Label} is {ratio:0.00}, needs {pair.Minimum:0.0}");
            }
        }

        return (failures, measured);
    }

    [Fact]
    public void TokenContrast_DeclaredPairsMeetTheBar()
    {
        var (root, shop) = ReadTokens();

        var (rootFailures, rootMeasured) = ScoreTokenPairs(root, "default");
        var (shopFailures, shopMeasured) = ScoreTokenPairs(shop, "shop theme");

        rootMeasured.Should().BeGreaterThanOrEqualTo(55, "the table must actually measure the declared pairs");
        shopMeasured.Should().Be(rootMeasured, "the theme is scored on the same table as the default");
        rootFailures.Concat(shopFailures).Should().BeEmpty(
            "every declared text pair must reach 4.5:1 and every non-text pair 3:1, composited over the lightest board pixel");
    }

    [Fact]
    public void TokenContrast_FailsWhenATextTokenGoesDark()
    {
        var (root, _) = ReadTokens();
        var darkened = new Dictionary<string, string>(root) { ["--qb-fg"] = "#1a0f08" };

        var (failures, _) = ScoreTokenPairs(darkened, "default");

        failures.Should().Contain(f => f.Contains("--qb-fg ") && f.Contains("glass"));
    }

    [Fact]
    public void TokenContrast_RejectsAContrastTokenThatIsNotALiteral()
    {
        var (root, _) = ReadTokens();
        var broken = new Dictionary<string, string>(root) { ["--qb-fg"] = "var(--qb-not-declared)" };

        var score = () => ScoreTokenPairs(broken, "default");

        score.Should().Throw<InvalidOperationException>().WithMessage("*--qb-not-declared*");
    }

    [Fact]
    public void TokenContrast_ResolvesVarChainsAndColourMixes()
    {
        var tokens = new Dictionary<string, string>
        {
            ["--a"] = "#102030",
            ["--b"] = "var(--a)",
            ["--c"] = "color-mix(in srgb, var(--b) 50%, transparent)",
        };

        ResolveColour(tokens, "var(--b)", "--b").Should().Be("#102030");
        WcagContrast.Parse(ResolveColour(tokens, "var(--c)", "--c")).Should().Be(new WcagContrast.Rgba(16, 32, 48, 0.5));
    }

    [Fact]
    public void QbColourChannelTriples_MatchTheTokensTheyAreNamedAfter()
    {
        var (root, shop) = ReadTokens();
        var triples = root.Keys.Where(k => Regex.IsMatch(k, @"^--qb-[\w-]+-rgb$")).ToList();
        triples.Should().NotBeEmpty(because: "surfaces swap Bootstrap's colour utilities through these triples");

        foreach (var (label, tokens) in new[] { ("default", root), ("shop theme", shop) })
            foreach (var triple in triples)
            {
                var named = triple[..^"-rgb".Length];
                var c = WcagContrast.Parse(ResolveColour(tokens, $"var({named})", named));
                var expected = string.Create(CultureInfo.InvariantCulture, $"{c.R:0}, {c.G:0}, {c.B:0}");
                tokens[triple].Should().Be(expected,
                    $"[{label}] {triple} must hold the channels of {named}");
            }
    }

    [Fact]
    public void BootstrapColourChannels_MatchTheFillsTheyStandFor()
    {
        var (root, shop) = ReadTokens();
        var stands = new Dictionary<string, string>
        {
            ["--bs-success-rgb"] = "--qb-success",
            ["--bs-danger-rgb"] = "--qb-danger",
            ["--bs-warning-rgb"] = "--qb-warning",
            ["--bs-info-rgb"] = "--qb-info",
            ["--bs-secondary-rgb"] = "--qb-neutral",
            ["--bs-primary-rgb"] = "--qb-wood",
            ["--bs-dark-rgb"] = "--qb-surface-overlay",
        };

        foreach (var (label, tokens) in new[] { ("default", root), ("shop theme", shop) })
            foreach (var (channels, fill) in stands)
            {
                var c = WcagContrast.Parse(ResolveColour(tokens, $"var({fill})", fill));
                var expected = string.Create(CultureInfo.InvariantCulture, $"{c.R:0}, {c.G:0}, {c.B:0}");
                tokens[channels].Should().Be(expected,
                    $"[{label}] {channels} must stay in step with {fill}, or Bootstrap tints and the tokens drift apart");
            }
    }
}
