using System.Text.RegularExpressions;
using FluentAssertions;

namespace QuestBoard.IntegrationTests.Tests;

/// <summary>
/// Pins how the vendor stylesheets enter the cascade and how the page-level rules re-theme them:
/// Bootstrap and Font Awesome are imported into the lowest layer by app.css, every layout preloads
/// the same URLs without a version query, no layout links a vendor stylesheet directly, and
/// Bootstrap is re-themed only by setting its own variables. An integration suite reading rendered
/// markup cannot see which layer a rule sits in or where a stylesheet was requested from, so only a
/// scan of the sources can hold the line.
/// </summary>
public class UiFoundationBaseGuardTests
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

    // The five layouts, as repository-relative paths.
    private static readonly string[] LayoutFiles =
    [
        "QuestBoard.Service/Views/Shared/_Layout.cshtml",
        "QuestBoard.Service/Views/Shared/_Layout.Mobile.cshtml",
        "QuestBoard.Service/Views/Shared/_Layout.GroupPicker.cshtml",
        "QuestBoard.Service/Areas/Platform/Views/Shared/_Layout.Platform.cshtml",
        "QuestBoard.Service/Areas/Platform/Views/Shared/_Layout.Platform.Mobile.cshtml",
    ];

    private static string ReadRepoFile(string relativePath) =>
        File.ReadAllText(ResolveRepoFile(relativePath.Split('/')));

    private static string StripComments(string css) => Regex.Replace(css, @"/\*[\s\S]*?\*/", " ");

    private static readonly Regex VendorImport = new(
        @"@import\s+url\(\s*""(?<path>[^""]+)""\s*\)\s*layer\(vendor\)\s*;", RegexOptions.Compiled);

    private static readonly Regex AnyImport = new(@"@import\b", RegexOptions.Compiled);

    private static readonly Regex PreloadLink = new(
        @"<link\b(?=[^>]*\brel=""preload"")[^>]*>", RegexOptions.Compiled);

    private static readonly Regex StylesheetLink = new(
        @"<link\b(?=[^>]*\brel=""stylesheet"")[^>]*>", RegexOptions.Compiled);

    private const string CinzelLatinFile = "~/lib/cinzel/cinzel-latin-wght.woff2";

    // The two imports in the order app.css declares them, as the path relative to wwwroot/css.
    private static List<string> VendorImportTargets()
    {
        var css = StripComments(ReadRepoFile(CssRoot + "/app.css"));
        return VendorImport.Matches(css).Select(m => m.Groups["path"].Value).ToList();
    }

    [Fact]
    public void AppCss_ImportsVendorIntoVendorLayer()
    {
        var css = StripComments(ReadRepoFile(CssRoot + "/app.css"));

        var imports = VendorImport.Matches(css);
        imports.Should().HaveCount(2, because: "app.css imports exactly Bootstrap and Font Awesome into the vendor layer");
        AnyImport.Matches(css).Should().HaveCount(2,
            because: "no stylesheet may be imported outside the vendor layer, or it would sit above the foundation");

        imports[0].Groups["path"].Value.Should().MatchRegex(@"^\.\./lib/bootstrap-5\.3\.\d+/css/bootstrap\.min\.css$");
        imports[1].Groups["path"].Value.Should().MatchRegex(@"^\.\./lib/fontawesome-6\.\d+\.\d+/css/all\.min\.css$");

        var layerStatement = css.IndexOf("@layer vendor, tokens, base, components, pages, utilities;", StringComparison.Ordinal);
        layerStatement.Should().BeGreaterThanOrEqualTo(0, because: "the layer order statement must be present");
        foreach (Match import in imports)
        {
            import.Index.Should().BeGreaterThan(layerStatement,
                because: "an import must follow the layer order statement, so the layer ranks are fixed before any file is read");
            css[..import.Index].Should().NotContain("@font-face",
                because: "an import is only valid before every rule except @charset and @layer statements");
        }

        var cssFolder = ResolveRepoFile(CssRoot.Split('/'));
        foreach (var target in imports.Select(m => m.Groups["path"].Value))
        {
            File.Exists(Path.GetFullPath(Path.Combine(cssFolder, target.Replace('/', Path.DirectorySeparatorChar))))
                .Should().BeTrue(because: $"app.css imports {target}, which must exist under wwwroot/lib");
        }
    }

    [Fact]
    public void Layouts_PreloadVendorWithoutVersionQuery()
    {
        // The preload URL must equal the import URL, with no query string, or the browser fetches the
        // stylesheet twice: once for the hint and once for the import.
        var expectedStyles = VendorImportTargets()
            .Select(target => "~/" + target.Replace("../", string.Empty, StringComparison.Ordinal))
            .ToList();
        expectedStyles.Should().HaveCount(2);

        foreach (var layout in LayoutFiles)
        {
            var preloads = PreloadLink.Matches(ReadRepoFile(layout)).Select(m => m.Value).ToList();

            foreach (var preload in preloads)
            {
                preload.Should().NotContain("asp-append-version",
                    because: $"{layout} must not fingerprint a preload: the URL would differ from the import and the file would download twice");
                preload.Should().NotMatchRegex(@"href=""[^""]*\?",
                    because: $"{layout} must preload a URL with no query string");
            }

            foreach (var expected in expectedStyles)
            {
                preloads.Should().ContainSingle(p => p.Contains($"href=\"{expected}\""),
                    because: $"{layout} must preload {expected}, the same URL app.css imports");
                preloads.Single(p => p.Contains($"href=\"{expected}\"")).Should().Contain("as=\"style\"");
            }

            var font = preloads.Where(p => p.Contains($"href=\"{CinzelLatinFile}\"")).ToList();
            font.Should().ContainSingle(because: $"{layout} must preload the Cinzel latin file so the display face is not late");
            font[0].Should().Contain("as=\"font\"");
            font[0].Should().Contain("type=\"font/woff2\"");
            font[0].Should().MatchRegex(@"\bcrossorigin\b",
                because: "a font preload is fetched in anonymous mode even from the same origin, and without the attribute it is fetched twice");
        }
    }

    [Fact]
    public void Layouts_LinkNoVendorStylesheetDirectly()
    {
        // Linked directly, a vendor stylesheet would be unlayered and would outrank every layer. The one
        // exception is the old-class-name compatibility sheet, which only restyles icon names.
        foreach (var layout in LayoutFiles)
        {
            foreach (Match link in StylesheetLink.Matches(ReadRepoFile(layout)))
            {
                if (!link.Value.Contains("~/lib/", StringComparison.Ordinal))
                    continue;

                link.Value.Should().Contain("/css/v4-shims.min.css",
                    because: $"{layout} must reach Bootstrap and Font Awesome through app.css: {link.Value}");
            }
        }
    }

    [Fact]
    public void BaseCss_MapsBootstrapVariables()
    {
        var css = StripComments(ReadRepoFile(CssRoot + "/base.css"));
        var root = Regex.Match(css, @":root\s*\{(?<body>[^}]*)\}");
        root.Success.Should().BeTrue(because: "base.css sets the Bootstrap variables on the root element");
        var body = root.Groups["body"].Value;

        var expected = new Dictionary<string, string>
        {
            ["--bs-body-font-family"] = "--qb-font-body",
            ["--bs-secondary-color"] = "--qb-fg-muted",
            ["--bs-success-rgb"] = "--qb-success-text-rgb",
            ["--bs-danger-rgb"] = "--qb-danger-text-rgb",
            ["--bs-warning-rgb"] = "--qb-warning-text-rgb",
            ["--bs-info-rgb"] = "--qb-info-text-rgb",
            ["--bs-border-radius"] = "--qb-radius-md",
            ["--bs-border-radius-sm"] = "--qb-radius-sm",
            ["--bs-border-radius-lg"] = "--qb-radius-lg",
            ["--bs-headings-font-weight"] = "--qb-weight-strong",
            ["--bs-link-color"] = "--qb-link",
            ["--bs-link-hover-color"] = "--qb-link-hover",
        };

        foreach (var (variable, token) in expected)
        {
            body.Should().MatchRegex($@"{Regex.Escape(variable)}\s*:\s*var\({Regex.Escape(token)}\)\s*;",
                because: $"base.css must map {variable} onto {token} so Bootstrap follows the tokens");
        }
    }

    [Fact]
    public void ComponentsCss_MapsBootstrapVariablesPerSurface()
    {
        // The variable mapping is what keeps muted, danger and dark-fill utilities readable once Bootstrap's
        // important utilities outrank the page's own important rules, so each surface that depends on it is pinned.
        var css = StripComments(ReadRepoFile(CssRoot + "/components.css"));

        string Block(string selectorFragment)
        {
            var match = Regex.Match(css, @"(?<sel>[^{}]*" + Regex.Escape(selectorFragment) + @"[^{}]*)\{(?<body>[^}]*)\}");
            match.Success.Should().BeTrue(because: $"components.css must carry a variable mapping for {selectorFragment}");
            return match.Groups["body"].Value;
        }

        Block(".modern-card .table").Should().Contain("--bs-secondary-color: var(--qb-ink-muted)");
        Block(".modern-card .table").Should().Contain("--bs-danger-rgb: var(--qb-ink-rgb)");
        Block(".modern-card .table").Should().Contain("--bs-link-color-rgb: var(--qb-wood-rgb)");
        Block(".navbar.bg-dark").Should().Contain("--bs-bg-opacity: 0");
        Block(".offcanvas-header.bg-dark").Should().Contain("--bs-dark-rgb: var(--qb-wood-rgb)");
        Block(".navbar .nav-link").Should().Contain("--bs-danger-rgb: var(--qb-fg-rgb)");
        Block(".bg-info.text-dark").Should().Contain("--bs-dark-rgb: var(--qb-on-fill-rgb)");
        Block(".bg-danger").Should().Contain("--bs-danger-rgb: var(--qb-danger-rgb)");
        Block(".bg-success").Should().Contain("--bs-success-rgb: var(--qb-success-rgb)");
    }

    [Fact]
    public void Bootstrap_ReadsTheVariablesTheMappingSets()
    {
        // The mapping above only works while Bootstrap's utilities read those variables. A release that
        // stopped reading one would silently turn the mapping into dead declarations.
        var libRoot = ResolveRepoFile("QuestBoard.Service", "wwwroot", "lib");
        var bootstrap = Directory.EnumerateDirectories(libRoot, "bootstrap-5.3.*").Single();
        var css = File.ReadAllText(Path.Combine(bootstrap, "css", "bootstrap.min.css"));

        css.Should().Contain(".text-muted{--bs-text-opacity:1;color:var(--bs-secondary-color)!important}");
        css.Should().Contain("color:rgba(var(--bs-danger-rgb),var(--bs-text-opacity))!important");
        css.Should().Contain("background-color:rgba(var(--bs-dark-rgb),var(--bs-bg-opacity))!important");
        css.Should().Contain("a{color:rgba(var(--bs-link-color-rgb),var(--bs-link-opacity,1))");
    }

    // ---- Rule parsing --------------------------------------------------------------------------
    // A rule that has no nested block, with the preludes of every block that encloses it. The
    // brace-depth walk is what lets a rule inside an @media or @layer block be found; a regex for
    // "selector { body }" cannot cross the outer braces.
    private sealed record LeafRule(IReadOnlyList<string> Ancestors, string Selector, string Body);

    private sealed class Frame(string prelude)
    {
        public string Prelude { get; } = Regex.Replace(prelude.Trim(), @"\s+", " ");
        public System.Text.StringBuilder Body { get; } = new();
        public bool HasChildren { get; set; }
    }

    private static List<LeafRule> ParseLeafRules(string css)
    {
        var rules = new List<LeafRule>();
        var stack = new List<Frame> { new("") };
        var pending = new System.Text.StringBuilder();

        foreach (var c in StripComments(css))
        {
            switch (c)
            {
                case '{':
                    stack[^1].HasChildren = true;
                    stack.Add(new Frame(pending.ToString()));
                    pending.Clear();
                    break;
                case '}':
                    stack[^1].Body.Append(pending);
                    pending.Clear();
                    var done = stack[^1];
                    stack.RemoveAt(stack.Count - 1);
                    if (!done.HasChildren)
                        rules.Add(new LeafRule(stack.Skip(1).Select(f => f.Prelude).ToList(), done.Prelude, done.Body.ToString()));
                    break;
                case ';':
                    stack[^1].Body.Append(pending).Append(';');
                    pending.Clear();
                    break;
                default:
                    pending.Append(c);
                    break;
            }
        }

        return rules;
    }

    private static string Flat(string text) => Regex.Replace(text, @"\s+", " ").Trim();

    private static bool InReducedMotion(LeafRule rule) =>
        rule.Ancestors.Any(a => Regex.IsMatch(a, @"^@media\s*\(\s*prefers-reduced-motion\s*:\s*reduce\s*\)$"));

    // Outline removals that are not replaced by a ring drawn from the focus tokens in the same rule.
    private static List<string> UnreplacedOutlineRemovals(string css) =>
        ParseLeafRules(css)
            .Where(r => Regex.IsMatch(r.Body, @"outline\s*:\s*(none|0)\s*(;|$)")
                        && !r.Body.Contains("var(--qb-focus-", StringComparison.Ordinal))
            .Select(r => r.Selector)
            .ToList();

    private static readonly string[] FocusRingSelectors =
    [
        "a", "button", "input", "select", "textarea", "summary", "[tabindex]", ".btn", ".nav-link",
        ".dropdown-item", ".page-link", ".btn-close", ".form-check-input", ".row-nav-link",
    ];

    [Fact]
    public void BaseCss_DeclaresTwoToneFocusRingForInteractiveSelectors()
    {
        var rules = ParseLeafRules(ReadRepoFile(CssRoot + "/base.css"));

        var ring = rules.Where(r => r.Selector.Contains(":focus-visible", StringComparison.Ordinal)
                                    && r.Selector.StartsWith(":where(", StringComparison.Ordinal)).ToList();
        ring.Should().ContainSingle(because: "base.css declares the focus ring once, for the whole selector list");

        var listMatch = Regex.Match(ring[0].Selector, @"^:where\((?<list>.*?)\):focus-visible");
        listMatch.Success.Should().BeTrue();
        var listed = listMatch.Groups["list"].Value.Split(',').Select(s => s.Trim()).ToList();
        foreach (var selector in FocusRingSelectors)
        {
            listed.Should().Contain(selector, because: $"{selector} must show the focus ring");
        }

        var body = Flat(ring[0].Body);
        body.Should().Contain("var(--qb-focus-outer)", because: "the outer stroke is the dark one");
        body.Should().Contain("var(--qb-focus-inner)", because: "the inner band is the gold one");
        body.Should().Contain("var(--qb-focus-width)");
        body.Should().Contain("var(--qb-focus-offset)");

        // Bootstrap hard-codes a blue shadow and no outline on these; the same ring must replace it.
        foreach (var hardCoded in new[] { ".form-control:focus", ".form-select:focus", ".form-check-input:focus" })
        {
            ring[0].Selector.Should().Contain(hardCoded,
                because: $"Bootstrap's own {hardCoded} look is a blue shadow with no outline and must be replaced");
        }

        rules.Should().Contain(
            r => r.Selector == ".btn"
                 && Flat(r.Body).Contains("--bs-btn-focus-box-shadow: 0 0 0 var(--qb-focus-offset) var(--qb-focus-inner)"),
            because: "Bootstrap buttons draw their focus shadow from this variable");
    }

    [Fact]
    public void BaseCss_RemovesNoOutlineWithoutReplacement()
    {
        UnreplacedOutlineRemovals(ReadRepoFile(CssRoot + "/base.css")).Should().BeEmpty(
            because: "removing the outline without drawing a focus ring from the focus tokens leaves keyboard users blind");

        // The scan must fire on a known-bad sample, or an empty result would prove nothing.
        UnreplacedOutlineRemovals(".x:focus { outline: none; } .y:focus { outline: 0; box-shadow: 0 0 0 2px var(--qb-focus-inner); }")
            .Should().Equal(".x:focus");
    }

    [Fact]
    public void BaseCss_HasReducedMotionBlock()
    {
        var css = ReadRepoFile(CssRoot + "/base.css");
        var rules = ParseLeafRules(css);

        var everything = rules.Where(r => InReducedMotion(r) && r.Selector.StartsWith('*')).ToList();
        everything.Should().NotBeEmpty(because: "a prefers-reduced-motion block must switch off motion for every element");
        var body = Flat(string.Join(" ", everything.Select(r => r.Body)));
        body.Should().MatchRegex(@"animation-duration:\s*0\.01ms\s*!important");
        body.Should().MatchRegex(@"transition-duration:\s*0\.01ms\s*!important");
        body.Should().MatchRegex(@"animation-iteration-count:\s*1\s*!important");
        body.Should().MatchRegex(@"scroll-behavior:\s*auto\s*!important");

        rules.Should().Contain(r => InReducedMotion(r) && Regex.IsMatch(r.Body, @"transform:\s*none\s*!important"),
            because: "hover and press lifts must not move anything for a reduced-motion user");

        css.Should().NotContainEquivalentOf("infinite",
            because: "the base layer adds no animation, and certainly no endless one");
    }

    [Fact]
    public void BaseCss_MapsHeadingScale()
    {
        var rules = ParseLeafRules(ReadRepoFile(CssRoot + "/base.css"));

        string Body(string selector)
        {
            // A selector may be declared by more than one rule (the body has a custom-property block and a type block).
            var matching = rules.Where(r => r.Selector == selector && r.Ancestors.Count == 1).ToList();
            matching.Should().NotBeEmpty(because: $"base.css must map {selector} onto the type scale");
            return string.Join(" ", matching.Select(r => Flat(r.Body)));
        }

        Body("body").Should().Contain("font-size: var(--qb-text-base)");
        Body("body").Should().Contain("line-height: var(--qb-leading-body)");
        Body("b, strong").Should().Contain("font-weight: var(--qb-weight-strong)");
        Body("h1, .h1").Should().Contain("font-size: var(--qb-text-xl)");
        Body("h2, .h2, h3, .h3").Should().Contain("font-size: var(--qb-text-lg)");
        Body("h4, .h4, h5, .h5, h6, .h6").Should().Contain("font-size: var(--qb-text-base)");
        Body("h4, .h4, h5, .h5, h6, .h6").Should().Contain("font-weight: var(--qb-weight-strong)");
        Body(".mobile-layout h1, .mobile-layout .h1").Should().Contain("font-size: var(--qb-text-lg)");
        Body("h1, .h1, .navbar-brand").Should().Contain("font-family: var(--qb-font-display)");

        // Only the two weights of the scale: every weight declared here is one of the two tokens.
        foreach (var rule in rules)
        {
            foreach (Match m in Regex.Matches(rule.Body, @"font-weight\s*:\s*(?<value>[^;]+)"))
            {
                m.Groups["value"].Value.Trim().Should().MatchRegex(@"^var\(--qb-weight-(regular|strong)\)$",
                    because: $"{rule.Selector} must use only the two weights of the type scale");
            }
        }
    }
}
