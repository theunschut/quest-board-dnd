using System.Text.RegularExpressions;
using FluentAssertions;

namespace QuestBoard.IntegrationTests.Tests;

/// <summary>
/// Pins the shop and shop management pages as they are moved onto the layered foundation: the page theme is carried
/// by the html element of both layouts, every post form carries an antiforgery token, no shop sheet holds a rule
/// that crops or reshapes a picture, the shop scripts build no markup from strings, and none of the files this work
/// touches names the planning that produced it. The lists below only ever grow as each view is converted; a file a
/// later change deletes is removed from them in the same commit. An integration suite reading rendered markup cannot
/// see what a comment says or which rule a sheet holds, so only a scan of the sources can hold the line.
/// </summary>
public class ShopConversionGuardTests
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

    private static readonly string[] Layouts =
    [
        "QuestBoard.Service/Views/Shared/_Layout.cshtml",
        "QuestBoard.Service/Views/Shared/_Layout.Mobile.cshtml",
    ];

    // The ten shop and shop management views and the details content they share, repository-relative.
    private static readonly string[] ShopViews =
    [
        "QuestBoard.Service/Views/Shop/Index.cshtml",
        "QuestBoard.Service/Views/Shop/Index.Mobile.cshtml",
        "QuestBoard.Service/Views/Shop/Details.cshtml",
        "QuestBoard.Service/Views/Shop/Details.Mobile.cshtml",
        "QuestBoard.Service/Views/ShopManagement/Index.cshtml",
        "QuestBoard.Service/Views/ShopManagement/Index.Mobile.cshtml",
        "QuestBoard.Service/Views/ShopManagement/Create.cshtml",
        "QuestBoard.Service/Views/ShopManagement/Create.Mobile.cshtml",
        "QuestBoard.Service/Views/ShopManagement/Edit.cshtml",
        "QuestBoard.Service/Views/ShopManagement/Edit.Mobile.cshtml",
        "QuestBoard.Service/Views/Shared/_ShopItemDetailsContent.cshtml",
        "QuestBoard.Service/Views/Shared/_ShopManagementDialogs.cshtml",
        "QuestBoard.Service/Views/Shared/_ShopManagementStockBadge.cshtml",
    ];

    // Every stylesheet that can reach a shop page. A sheet that is not on disk yet is skipped, never removed from the
    // list, so the guard covers it from the commit that creates it.
    private static readonly string[] ShopSheets =
    [
        "QuestBoard.Service/wwwroot/css/shop.css",
        "QuestBoard.Service/wwwroot/css/shop.mobile.css",
        "QuestBoard.Service/wwwroot/css/shop-details.mobile.css",
        "QuestBoard.Service/wwwroot/css/shop-management-index.mobile.css",
        "QuestBoard.Service/wwwroot/css/shop-management-create.mobile.css",
        "QuestBoard.Service/wwwroot/css/shop-management-edit.mobile.css",
        "QuestBoard.Service/wwwroot/css/legacy-pages.css",
        "QuestBoard.Service/wwwroot/css/shop-pages.css",
        "QuestBoard.Service/wwwroot/css/shop-pages.mobile.css",
        "QuestBoard.Service/wwwroot/css/shop-form.css",
    ];

    // Every script file the shop pages load. Grows as each slice adds a module.
    private static readonly string[] ShopModules =
    [
        "QuestBoard.Service/wwwroot/js/shop-item-sell.js",
        "QuestBoard.Service/wwwroot/js/shop-item-form.js",
        "QuestBoard.Service/wwwroot/js/shop-item-preview.js",
        "QuestBoard.Service/wwwroot/js/shop-management.js",
    ];

    // Views and partials that carry no inline code, repository-relative. Grows as each view is converted.
    private static readonly string[] ConvertedViews =
    [
        "QuestBoard.Service/Views/Shop/Details.cshtml",
        "QuestBoard.Service/Views/Shop/Details.Mobile.cshtml",
        "QuestBoard.Service/Views/Shared/_ShopItemDetailsContent.cshtml",
        "QuestBoard.Service/Views/ShopManagement/Edit.cshtml",
        "QuestBoard.Service/Views/ShopManagement/Create.cshtml",
        "QuestBoard.Service/Views/ShopManagement/Create.Mobile.cshtml",
        "QuestBoard.Service/Views/ShopManagement/Edit.Mobile.cshtml",
        "QuestBoard.Service/Views/ShopManagement/Index.cshtml",
        "QuestBoard.Service/Views/ShopManagement/Index.Mobile.cshtml",
        "QuestBoard.Service/Views/Shared/_ShopManagementDialogs.cshtml",
        "QuestBoard.Service/Views/Shared/_ShopManagementStockBadge.cshtml",
    ];

    // For each view, the sheets it must link from its Styles section, by file name. The details partial links nothing:
    // the pages that render it, and the shop pages that load it into the dialog, link the sheets it needs.
    private static readonly Dictionary<string, string[]> ViewSheetLinks = new()
    {
        ["QuestBoard.Service/Views/Shop/Details.cshtml"] = ["shop-pages.css", "markdown-content.css"],
        ["QuestBoard.Service/Views/Shop/Details.Mobile.cshtml"] = ["shop-pages.css", "shop-pages.mobile.css", "markdown-content.css"],
        ["QuestBoard.Service/Views/Shop/Index.cshtml"] = ["shop-pages.css", "markdown-content.css"],
        ["QuestBoard.Service/Views/Shop/Index.Mobile.cshtml"] = ["shop-pages.css", "shop-pages.mobile.css", "markdown-content.css"],
        ["QuestBoard.Service/Views/ShopManagement/Index.cshtml"] = ["shop-pages.css"],
        ["QuestBoard.Service/Views/ShopManagement/Index.Mobile.cshtml"] = ["shop-pages.css", "shop-pages.mobile.css"],
        ["QuestBoard.Service/Views/ShopManagement/Edit.cshtml"] = ["shop-pages.css", "shop-form.css"],
        ["QuestBoard.Service/Views/ShopManagement/Create.cshtml"] = ["shop-pages.css", "shop-form.css"],
        ["QuestBoard.Service/Views/ShopManagement/Create.Mobile.cshtml"] = ["shop-pages.css", "shop-form.css", "shop-pages.mobile.css"],
        ["QuestBoard.Service/Views/ShopManagement/Edit.Mobile.cshtml"] = ["shop-pages.css", "shop-form.css", "shop-pages.mobile.css"],
    };

    // Files this work retires; each is deleted and must stay deleted, because its content moved into the shared sheets.
    private static readonly string[] RetiredFiles =
    [
        "QuestBoard.Service/wwwroot/css/shop-details.mobile.css",
        "QuestBoard.Service/wwwroot/css/shop-management-create.mobile.css",
        "QuestBoard.Service/wwwroot/css/shop-management-edit.mobile.css",
        "QuestBoard.Service/wwwroot/css/shop-management-index.mobile.css",
    ];

    private static readonly (string Label, Regex Pattern)[] InlineCodePatterns =
    [
        ("a style attribute", new Regex(@"\sstyle\s*=", RegexOptions.IgnoreCase)),
        ("a style element", new Regex(@"<style\b", RegexOptions.IgnoreCase)),
        ("an inline script element", new Regex(@"<script\b(?![^>]*\bsrc\s*=)[^>]*>", RegexOptions.IgnoreCase)),
        ("an inline event handler", new Regex(@"\son[a-z]+\s*=", RegexOptions.IgnoreCase)),
    ];

    // Every source file this work creates or edits, repository-relative, scanned for planning references. This test
    // file is left out because it has to name the shapes it forbids.
    private static readonly string[] PhaseSourceFiles =
    [
        "QuestBoard.Service/Views/Shared/_Layout.cshtml",
        "QuestBoard.Service/Views/Shared/_Layout.Mobile.cshtml",
        "QuestBoard.Service/Views/Shop/Details.cshtml",
        "QuestBoard.Service/Views/Shop/Details.Mobile.cshtml",
        "QuestBoard.Service/Views/Shop/Index.cshtml",
        "QuestBoard.Service/Views/Shop/Index.Mobile.cshtml",
        "QuestBoard.Service/Views/Shared/_ShopItemDetailsContent.cshtml",
        "QuestBoard.Service/wwwroot/js/shop-item-sell.js",
        "QuestBoard.IntegrationTests/Tests/ShopRenderTests.cs",
        "QuestBoard.IntegrationTests/Tests/KeyboardScriptGuardTests.cs",
        "QuestBoard.Service/ViewModels/ShopViewModels/ShopItemDisplay.cs",
        "QuestBoard.Service/Views/Shared/_ShopRarityChip.cshtml",
        "QuestBoard.Service/wwwroot/css/shop-pages.css",
        "QuestBoard.Service/wwwroot/css/shop.css",
        "QuestBoard.Service/wwwroot/css/legacy-pages.css",
        "QuestBoard.Service/wwwroot/css/shop.mobile.css",
        "QuestBoard.IntegrationTests/Tests/UiFoundationLayerGuardTests.cs",
        "QuestBoard.Service/ViewModels/ShopViewModels/ShopItemViewModel.cs",
        "QuestBoard.Service/Views/Shared/_ShopStatusBadge.cshtml",
        "QuestBoard.Service/Views/ShopManagement/Index.cshtml",
        "QuestBoard.Service/Views/ShopManagement/Index.Mobile.cshtml",
        "QuestBoard.IntegrationTests/Tests/ShopItemDisplayTests.cs",
        "QuestBoard.Service/Views/Shared/_ShopTypeChip.cshtml",
        "QuestBoard.Service/Views/ShopManagement/Edit.cshtml",
        "QuestBoard.Service/Views/ShopManagement/Edit.Mobile.cshtml",
        "QuestBoard.Service/Views/ShopManagement/Create.cshtml",
        "QuestBoard.Service/Views/ShopManagement/Create.Mobile.cshtml",
        "QuestBoard.Service/wwwroot/css/shop-pages.mobile.css",
        "QuestBoard.IntegrationTests/Mobile/MobileViewsTests.cs",
        "QuestBoard.Service/wwwroot/css/shop-form.css",
        "QuestBoard.Service/wwwroot/js/shop-item-form.js",
        "QuestBoard.Service/wwwroot/js/shop-item-preview.js",
        "QuestBoard.Service/wwwroot/js/shop-management.js",
        "QuestBoard.Service/Views/Shared/_ShopManagementDialogs.cshtml",
        "QuestBoard.Service/Views/Shared/_ShopManagementStockBadge.cshtml",
        "QuestBoard.Service/Views/Shared/_MarkdownEditor.cshtml",
    ];

    // Written as regex data, each forbidden shape built from fragments, so this file never holds the shapes it
    // forbids elsewhere.
    private static readonly (string Label, Regex Pattern)[] PlanningReferencePatterns =
    [
        ("a decision id", new Regex(@"\bD" + @"-(?:0[1-9]|[1-9][0-9])\b")),
        ("a question id", new Regex(@"\b(?:O)?Q" + @"-\d{1,3}\b")),
        ("a merge or relayout id", new Regex(@"\bR" + @"-[1-9]\b")),
        ("a definition-of-converted id", new Regex(@"\bDo" + @"C-\d{1,2}\b")),
        ("an audit finding id", new Regex(@"\bA" + @"-shop-\d{3}\b")),
        ("a threat id", new Regex(@"\bT" + @"-99-\d{2}\b")),
        ("a ledger number", new Regex("WIND" + "OWS\\s+(?:entry\\s+)?#?\\d+", RegexOptions.IgnoreCase)),
        ("a phase reference", new Regex("Pha" + "se\\s+9" + "9\\b", RegexOptions.IgnoreCase)),
        ("a plan id", new Regex(@"\b9" + @"9-(?:\d\d|[A-Z]{2,})")),
        ("a planning document name", new Regex(
            "CONT" + "EXT\\.md|RESE" + "ARCH\\.md|UI-" + "SPEC|PATT" + "ERNS\\.md|VALID" + "ATION\\.md|AUD" + "IT\\.md")),
    ];

    // ---- Scanners ---------------------------------------------------------------------------------

    private static string StripRazorAndHtmlComments(string view)
    {
        var withoutRazor = Regex.Replace(view, @"@\*[\s\S]*?\*@", " ");
        return Regex.Replace(withoutRazor, @"<!--[\s\S]*?-->", " ");
    }

    // The opening tags of the post forms in a view that carry no antiforgery token: a post form is covered when its
    // tag names a controller or an action (the form tag helper then writes the token) or when a token call appears
    // before the form closes.
    private static List<string> PostFormsWithoutAToken(string view)
    {
        var uncovered = new List<string>();
        foreach (Match form in Regex.Matches(StripRazorAndHtmlComments(view), @"<form\b[^>]*>", RegexOptions.IgnoreCase))
        {
            if (!Regex.IsMatch(form.Value, @"\bmethod\s*=\s*""post""", RegexOptions.IgnoreCase))
                continue;

            if (Regex.IsMatch(form.Value, @"\basp-(?:action|controller)\b", RegexOptions.IgnoreCase))
                continue;

            var rest = StripRazorAndHtmlComments(view)[(form.Index + form.Length)..];
            var close = rest.IndexOf("</form>", StringComparison.OrdinalIgnoreCase);
            var body = close < 0 ? rest : rest[..close];
            if (!body.Contains("AntiForgeryToken", StringComparison.Ordinal))
                uncovered.Add(form.Value);
        }

        return uncovered;
    }

    // The declarations in a stylesheet that crop or reshape a picture. Comments are removed first so a sentence
    // that explains the rule does not count as the rule.
    private static List<string> WholeImageBreaks(string css)
    {
        var withoutComments = Regex.Replace(css, @"/\*[\s\S]*?\*/", " ");
        return Regex.Matches(withoutComments, @"(?<![\w-])(aspect-ratio|object-fit)\s*:")
            .Select(m => m.Groups[1].Value).ToList();
    }

    // The ways a script can hand a string to the markup parser.
    private static List<string> MarkupFromStrings(string script) =>
        Regex.Matches(script, @"\.(?:innerHTML|outerHTML)\b|insertAdjacentHTML|document\.write")
            .Select(m => m.Value).ToList();

    // The legendary chip colour the operator chose: "amber" fills it with the warning token and its dark label, "gold"
    // with the accent token and its ink label. Changing the answer means changing this constant and the sheet together.
    private const string RecordedLegendaryColour = "amber";

    private static readonly Regex PerRarityClass = new(
        @"\.rarity-(?:common|uncommon|rare|veryrare|legendary)(?![\w-])", RegexOptions.Compiled);

    // The selector parts in a stylesheet that name a per-rarity class without excluding the new chip. Each selector
    // list is split at its commas so a part that is narrowed cannot cover one beside it that is not.
    private static List<string> RarityRulesThatCanReachANewChip(string css)
    {
        var withoutComments = Regex.Replace(css, @"/\*[\s\S]*?\*/", " ");
        var offenders = new List<string>();
        foreach (Match rule in Regex.Matches(withoutComments, @"(?<selectors>[^{}]+)\{"))
        foreach (var part in rule.Groups["selectors"].Value.Split(','))
            if (PerRarityClass.IsMatch(part) && !part.Contains(":not(.badge-rarity)", StringComparison.Ordinal))
                offenders.Add(part.Trim());

        return offenders;
    }

    // The selector parts that name a per-rarity class at all.
    private static List<string> RarityRuleSelectors(string css)
    {
        var withoutComments = Regex.Replace(css, @"/\*[\s\S]*?\*/", " ");
        return Regex.Matches(withoutComments, @"(?<selectors>[^{}]+)\{")
            .SelectMany(rule => rule.Groups["selectors"].Value.Split(','))
            .Where(part => PerRarityClass.IsMatch(part)).Select(part => part.Trim()).ToList();
    }

    // What is wrong with the chip rules of the shop page sheet, given the recorded legendary colour: the legendary
    // rule must pair its fill and label tokens, no other chip rule may use the gold accent, and nothing may animate.
    private static List<string> LegendaryChipBreaks(string css, string colour)
    {
        var (fill, label) = colour == "gold"
            ? ("--qb-accent", "--qb-accent-ink")
            : ("--qb-warning", "--qb-warning-fg");
        var breaks = new List<string>();
        var withoutComments = Regex.Replace(css, @"/\*[\s\S]*?\*/", " ");
        var rules = Regex.Matches(withoutComments, @"(?<selector>[^{}]+)\{(?<body>[^{}]*)\}")
            .Select(m => (Selector: m.Groups["selector"].Value.Trim(), Body: m.Groups["body"].Value)).ToList();

        var legendary = rules.Where(r => r.Selector.Contains(".rarity-legendary", StringComparison.Ordinal)).ToList();
        if (legendary.Count != 1)
            breaks.Add($"expected one legendary chip rule, found {legendary.Count}");
        else
        {
            if (!Regex.IsMatch(legendary[0].Body, @"background-color\s*:\s*var\(" + Regex.Escape(fill) + @"\)"))
                breaks.Add($"the legendary fill is not {fill}");
            if (!Regex.IsMatch(legendary[0].Body, @"(?<![\w-])color\s*:\s*var\(" + Regex.Escape(label) + @"\)"))
                breaks.Add($"the legendary label is not {label}");
        }

        foreach (var rule in rules.Where(r => r.Selector.Contains(".badge-rarity", StringComparison.Ordinal)
                     && !r.Selector.Contains(".rarity-legendary", StringComparison.Ordinal)
                     && r.Body.Contains("--qb-accent", StringComparison.Ordinal)))
            breaks.Add($"{rule.Selector} uses the gold accent");

        if (Regex.IsMatch(withoutComments, @"@keyframes|(?<![\w-])animation\s*:"))
            breaks.Add("a chip animates");

        return breaks;
    }

    // The new shop sheets, which hold no endless animation and no catch-all transition. A sheet that is not on disk yet
    // is skipped here and covered from the commit that creates it.
    private static readonly string[] NewShopSheets =
    [
        "QuestBoard.Service/wwwroot/css/shop-pages.css",
        "QuestBoard.Service/wwwroot/css/shop-pages.mobile.css",
        "QuestBoard.Service/wwwroot/css/shop-form.css",
    ];

    // The declarations in a stylesheet that animate forever or transition every property.
    private static List<string> EndlessMotionBreaks(string css)
    {
        var withoutComments = Regex.Replace(css, @"/\*[\s\S]*?\*/", " ");
        return Regex.Matches(withoutComments,
                @"(?<![\w-])animation(?:-iteration-count)?\s*:[^;}]*\binfinite\b|(?<![\w-])transition(?:-property)?\s*:\s*all\b")
            .Select(m => m.Value.Trim()).ToList();
    }

    // The shapes a shop view must not hold because the status words, fills and icons come from one mapping: a
    // replacement that renames the Published word, or a switch of its own over a status.
    private static List<string> StatusMappingBreaks(string view)
    {
        var markup = StripRazorAndHtmlComments(view);
        return Regex.Matches(markup,
                @"\.Replace\(\s*""Published""|@switch\s*\(\s*[\w.]*Status\s*\)|(?<![\w])[\w.]*Status\s+switch\b|fa-@[\w.]*Status")
            .Select(m => m.Value).ToList();
    }

    // A date written through a format string in a view bypasses the date helpers; the two machine value formats that
    // fill a date input or a time element's datetime attribute are the only strings allowed.
    private static readonly Regex DateFormatCall = new(
        @"\.ToString\(\s*""(?<format>[^""]*(?:yy|MM|dd|HH|hh|mm|ss|tt)[^""]*|[dDfFgGmMoOrRsStTuUyY])""",
        RegexOptions.Compiled);

    private static readonly string[] MachineValueFormats = ["yyyy-MM-dd", "yyyy-MM-ddTHH:mm"];

    private static List<string> RawDateFormats(string view) =>
        DateFormatCall.Matches(StripRazorAndHtmlComments(view))
            .Select(m => m.Groups["format"].Value)
            .Where(f => !MachineValueFormats.Contains(f))
            .ToList();

    // The text of a view's Styles section, from its opening brace to the matching closing brace.
    private static string StylesSection(string view)
    {
        var start = view.IndexOf("@section Styles", StringComparison.Ordinal);
        if (start < 0)
            return string.Empty;

        var open = view.IndexOf('{', start);
        if (open < 0)
            return string.Empty;

        var depth = 0;
        for (var i = open; i < view.Length; i++)
        {
            if (view[i] == '{')
                depth++;
            else if (view[i] == '}' && --depth == 0)
                return view[open..(i + 1)];
        }

        return view[open..];
    }

    private static bool LinksSheet(string view, string sheetName) =>
        Regex.IsMatch(
            StylesSection(StripRazorAndHtmlComments(view)),
            @"<link\b[^>]*\bhref=""~/css/" + Regex.Escape(sheetName) + @"""[^>]*>");

    // The rule that keeps a picture inside a description whole: it may shrink to its column and keeps its own
    // proportions. Returns what is missing from the first rule that names the description's images.
    private static List<string> WholeImageRuleGaps(string css)
    {
        var withoutComments = Regex.Replace(css, @"/\*[\s\S]*?\*/", " ");
        var rule = Regex.Match(withoutComments,
            @"\.item-details-content\s+\.markdown-content\s+img\s*\{(?<body>[^{}]*)\}");
        if (!rule.Success)
            return ["no rule names the images inside the item details content"];

        var gaps = new List<string>();
        if (!Regex.IsMatch(rule.Groups["body"].Value, @"(?<![\w-])max-inline-size\s*:\s*100%"))
            gaps.Add("max-inline-size: 100% is missing");
        if (!Regex.IsMatch(rule.Groups["body"].Value, @"(?<![\w-])block-size\s*:\s*auto"))
            gaps.Add("block-size: auto is missing");
        return gaps;
    }

    // ---- Guards ---------------------------------------------------------------------------------

    [Fact]
    public void ThemeSeam_BothLayoutsEmitTheThemeOnHtmlNotBody()
    {
        foreach (var path in Layouts)
        {
            var layout = ReadRepoFile(path);
            var lines = layout.Split('\n');
            var htmlLine = lines.Single(l => l.TrimStart().StartsWith("<html", StringComparison.OrdinalIgnoreCase));
            var bodyLine = lines.Single(l => l.TrimStart().StartsWith("<body", StringComparison.OrdinalIgnoreCase));

            htmlLine.Should().Contain("data-theme", $"{path} carries the page theme on the html element");
            htmlLine.Should().Contain("ViewData[\"Theme\"]", $"{path} takes the theme from the page's own choice");
            bodyLine.Should().NotContain("data-theme", $"{path} must not put the theme on the body, which cannot recolour the fallback background");
            Regex.Matches(layout, "data-theme").Should().HaveCount(1, $"{path} names the theme attribute once");
        }
    }

    [Fact]
    public void ShopPostForms_EachCarryAnAntiforgeryToken()
    {
        foreach (var path in ShopViews)
            PostFormsWithoutAToken(ReadRepoFile(path)).Should().BeEmpty(
                $"{path} must give every post form a token, or the post is refused with a 400");
    }

    [Fact]
    public void PostFormScanner_FlagsATokenlessFormAndAllowsTheCoveredOnes()
    {
        PostFormsWithoutAToken("<form method=\"post\" action=\"@Url.Action(\"Sell\", \"Shop\")\" class=\"d-inline\"><button>Go</button></form>")
            .Should().HaveCount(1, "an explicit action with no token call is the shape that posts with a 400");
        PostFormsWithoutAToken("<form method=\"post\" asp-controller=\"Shop\" asp-action=\"Sell\"><button>Go</button></form>")
            .Should().BeEmpty("the form tag helper writes the token");
        PostFormsWithoutAToken("<form method=\"post\" action=\"/x\">@Html.AntiForgeryToken()<button>Go</button></form>")
            .Should().BeEmpty("an explicit token call covers the form");
        PostFormsWithoutAToken("<form method=\"get\" action=\"/x\"><button>Go</button></form>")
            .Should().BeEmpty("a form that reads has nothing to forge");
        PostFormsWithoutAToken("<form method=\"post\" action=\"/a\"><button>Go</button></form><form method=\"post\" action=\"/b\">@Html.AntiForgeryToken()</form>")
            .Should().HaveCount(1, "a token in a later form does not cover an earlier one");
    }

    [Fact]
    public void ShopSheets_HoldNoAspectRatioOrObjectFit()
    {
        var checkedSheets = 0;
        foreach (var path in ShopSheets)
        {
            if (!File.Exists(ResolveRepoFile(path.Split('/'))))
                continue;

            checkedSheets++;
            WholeImageBreaks(File.ReadAllText(ResolveRepoFile(path.Split('/')))).Should().BeEmpty(
                $"{path} must not crop or reshape a picture: it is always shown whole");
        }

        checkedSheets.Should().BeGreaterThan(0, "the guard must cover at least one sheet");
    }

    [Fact]
    public void WholeImageScanner_FlagsBothDeclarationsAndIgnoresCommentsAndOtherNames()
    {
        WholeImageBreaks(".thumb { aspect-ratio: 4 / 3; object-fit: cover; }")
            .Should().BeEquivalentTo("aspect-ratio", "object-fit");
        WholeImageBreaks(".thumb { width: 100%; height: auto; }").Should().BeEmpty();
        WholeImageBreaks("/* never use aspect-ratio: or object-fit: here */ .thumb { max-width: 100%; }").Should().BeEmpty();
        WholeImageBreaks(".thumb { --my-aspect-ratio: 1; }").Should().BeEmpty("a custom property that merely contains the name is not the declaration");
    }

    [Fact]
    public void ShopModules_WriteNoMarkupFromStrings()
    {
        ShopModules.Should().NotBeEmpty();
        foreach (var path in ShopModules)
            MarkupFromStrings(ReadRepoFile(path)).Should().BeEmpty($"{path} must build no markup from strings");
    }

    [Fact]
    public void ModuleScanner_FlagsEveryWayToHandAStringToTheParser()
    {
        MarkupFromStrings("el.innerHTML = html; el.outerHTML = x; el.insertAdjacentHTML('beforeend', y); document.write(z);")
            .Should().HaveCount(4);
        MarkupFromStrings("el.textContent = total; form.dataset.itemName;").Should().BeEmpty();
    }

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
        // each scanner is shown to fire here and to stay quiet on a clean sample.
        var planning = "// see " + "D" + "-04 and " + "Q" + "-51, " + "Pha" + "se 99" + ", 99" + "-05 in " + "RESE" + "ARCH.md, "
            + "R" + "-2, " + "Do" + "C-8, " + "A" + "-shop-012, " + "T" + "-99-11, " + "WIND" + "OWS #31";
        PlanningReferencePatterns.Count(p => p.Pattern.IsMatch(planning)).Should().Be(PlanningReferencePatterns.Length);
        PlanningReferencePatterns.Count(p => p.Pattern.IsMatch("// the total follows the quantity the buyer types"))
            .Should().Be(0);
    }

    [Fact]
    public void ShopSheets_LegendaryChipFollowsTheRecordedColour()
    {
        RecordedLegendaryColour.Should().BeOneOf("gold", "amber");

        LegendaryChipBreaks(ReadRepoFile("QuestBoard.Service/wwwroot/css/shop-pages.css"), RecordedLegendaryColour)
            .Should().BeEmpty("the legendary chip uses the token pair of the recorded colour and nothing else uses the gold accent");
    }

    [Fact]
    public void LegendaryChipScanner_FlagsTheWrongPairAGoldNeighbourAndAnAnimation()
    {
        const string good = ".badge-rarity.rarity-legendary { background-color: var(--qb-warning); color: var(--qb-warning-fg); box-shadow: 0 0 4px var(--qb-warning); }"
            + " .badge-rarity.rarity-rare { background-color: var(--qb-info); color: var(--qb-info-fg); }";
        LegendaryChipBreaks(good, "amber").Should().BeEmpty();
        LegendaryChipBreaks(good, "gold").Should().NotBeEmpty("the amber sheet does not satisfy the gold answer");

        LegendaryChipBreaks(good.Replace("--qb-warning-fg", "--qb-info-fg"), "amber").Should().ContainSingle(b => b.Contains("label"));
        LegendaryChipBreaks(good + " .badge-rarity.rarity-rare { border-color: var(--qb-accent); }", "amber")
            .Should().ContainSingle(b => b.Contains("gold accent"));
        LegendaryChipBreaks(good + " @keyframes pulse { from { opacity: 1; } }", "amber").Should().ContainSingle(b => b.Contains("animates"));
        LegendaryChipBreaks(".badge-rarity.rarity-rare { color: var(--qb-info-fg); }", "amber").Should().NotBeEmpty("a sheet with no legendary rule fails");
    }

    [Fact]
    public void ShopSheets_HoldNoRarityRuleOutsideTheChipSheet()
    {
        foreach (var path in new[]
                 {
                     "QuestBoard.Service/wwwroot/css/shop.css",
                     "QuestBoard.Service/wwwroot/css/legacy-pages.css",
                     "QuestBoard.Service/wwwroot/css/shop.mobile.css",
                 })
        {
            var css = ReadRepoFile(path);
            RarityRuleSelectors(css).Should().BeEmpty($"{path} no longer styles a rarity: the chip sheet does");
            Regex.IsMatch(css, @"\.item-rarity\b|legendary-glow").Should().BeFalse(
                $"{path} holds no rule for the old rarity text or its glow");
        }
    }

    [Fact]
    public void RarityRuleScanner_FlagsAnUnnarrowedPartEvenBesideANarrowedOne()
    {
        RarityRulesThatCanReachANewChip(".rarity-rare { color: red; }").Should().ContainSingle();
        RarityRulesThatCanReachANewChip(".rarity-rare:not(.badge-rarity) { color: red; }").Should().BeEmpty();
        RarityRulesThatCanReachANewChip(".card .rarity-veryrare, .rarity-rare:not(.badge-rarity) { color: red; }")
            .Should().ContainSingle(part => part.Contains("veryrare"), "a comma-separated part is judged on its own");
        RarityRulesThatCanReachANewChip("/* .rarity-rare { color: red; } */ .item-rarity { margin: 0; }").Should().BeEmpty();
        RarityRulesThatCanReachANewChip("@keyframes glow { 0% { opacity: 0; } }").Should().BeEmpty();
        RarityRuleSelectors(".shop-card .rarity-legendary { color: gold; }").Should().ContainSingle();
        RarityRuleSelectors(".item-rarity { margin: 0; }").Should().BeEmpty();
    }

    [Fact]
    public void ShopViews_UseTheSharedStatusMapping()
    {
        foreach (var path in ShopViews)
            StatusMappingBreaks(ReadRepoFile(path)).Should().BeEmpty(
                $"{path} must show a status through the shared badge and mapping, not a rule of its own");
    }

    [Fact]
    public void StatusMappingScanner_FlagsOwnWordsOwnSwitchesAndBuiltIconsOnly()
    {
        StatusMappingBreaks("@Model.Status.ToString().Replace(\"Published\", \"Available\")").Should().HaveCount(1);
        StatusMappingBreaks("@switch (item.Status) { case ItemStatus.Draft: break; }").Should().HaveCount(1);
        StatusMappingBreaks("@(Model.Status switch { ItemStatus.Published => \"bg-success\", _ => \"\" })").Should().HaveCount(1);
        StatusMappingBreaks("<i class=\"fas fa-@item.Status.ToString().ToLower()\"></i>").Should().HaveCount(1);
        StatusMappingBreaks("<partial name=\"_ShopStatusBadge\" model=\"item.Status\" />").Should().BeEmpty();
        StatusMappingBreaks("@if (item.Status == ItemStatus.Published) { <b>x</b> }").Should().BeEmpty("a condition is not a status mapping");
        StatusMappingBreaks("<!-- @switch (item.Status) { } -->").Should().BeEmpty("a comment is not markup");
    }

    [Fact]
    public void ShopSheets_NewSheetsHoldNoEndlessAnimationOrTransitionAll()
    {
        var checkedSheets = 0;
        foreach (var path in NewShopSheets)
        {
            if (!File.Exists(ResolveRepoFile(path.Split('/'))))
                continue;

            checkedSheets++;
            EndlessMotionBreaks(ReadRepoFile(path)).Should().BeEmpty(
                $"{path} must not animate forever or transition every property");
        }

        checkedSheets.Should().BeGreaterThan(0, "at least the shop page sheet exists");
    }

    [Fact]
    public void EndlessMotionScanner_FlagsASeededRuleAndAllowsQuietMotion()
    {
        EndlessMotionBreaks(".chip { animation: glow 2s ease-in-out infinite alternate; }").Should().HaveCount(1);
        EndlessMotionBreaks(".chip { animation-iteration-count: infinite; }").Should().HaveCount(1);
        EndlessMotionBreaks(".chip { transition: all 0.3s ease; }").Should().HaveCount(1);
        EndlessMotionBreaks(".chip { transition-property: all; }").Should().HaveCount(1);
        EndlessMotionBreaks(".chip { transition: color 0.2s ease, box-shadow 0.2s ease; animation: fade 1s ease 1; }").Should().BeEmpty();
        EndlessMotionBreaks("/* transition: all 1s; */ .chip { box-shadow: 0 0 4px var(--qb-warning); }").Should().BeEmpty();
    }

    [Fact]
    public void ConvertedViews_CarryNoInlineStyleScriptOrHandler()
    {
        ConvertedViews.Should().NotBeEmpty();
        foreach (var path in ConvertedViews)
        {
            var view = StripRazorAndHtmlComments(ReadRepoFile(path));
            foreach (var (label, pattern) in InlineCodePatterns)
                pattern.Matches(view).Select(m => m.Value).Should().BeEmpty(
                    $"{path} must carry no inline code; {label} belongs in a stylesheet or a script file");
        }
    }

    [Fact]
    public void ConvertedViews_LinkTheirSheets()
    {
        foreach (var (path, sheets) in ViewSheetLinks)
        {
            var view = ReadRepoFile(path);
            foreach (var sheet in sheets)
                LinksSheet(view, sheet).Should().BeTrue(
                    $"{path} must link {sheet} from its Styles section so the sheet loads only where it is used");
        }
    }

    [Fact]
    public void ViewSheetLinkScanner_ReadsOnlyTheStylesSection()
    {
        const string linked = "@section Styles {\n    <link rel=\"stylesheet\" href=\"~/css/a.css\" asp-append-version=\"true\" />\n}\n<p>x</p>";
        LinksSheet(linked, "a.css").Should().BeTrue();
        LinksSheet(linked, "b.css").Should().BeFalse();
        LinksSheet("<link rel=\"stylesheet\" href=\"~/css/a.css\" />", "a.css").Should().BeFalse("a link outside the Styles section does not count");
        LinksSheet("@* @section Styles { <link href=\"~/css/a.css\" /> } *@", "a.css").Should().BeFalse("a comment is not a section");
    }

    [Fact]
    public void RetiredFiles_AreGone()
    {
        RetiredFiles.Should().NotBeEmpty();
        foreach (var path in RetiredFiles)
            File.Exists(ResolveRepoFile(path.Split('/'))).Should().BeFalse(
                $"{path} was folded into the shared shop sheets and must not come back");
    }

    [Fact]
    public void ShopViews_RenderNoRawDateFormatting()
    {
        foreach (var path in ShopViews)
            RawDateFormats(ReadRepoFile(path)).Should().BeEmpty(
                $"{path} must render dates through the date helpers, never a format string");
    }

    [Fact]
    public void RawDateScanner_FlagsFormatStringsAndAllowsMachineValuesAndNumbers()
    {
        RawDateFormats("@Model.AvailableFrom.Value.ToString(\"MMM dd\")").Should().HaveCount(1);
        RawDateFormats("@x.ToString(\"d\")").Should().HaveCount(1);
        RawDateFormats("value=\"@x.ToString(\"yyyy-MM-dd\")\"").Should().BeEmpty("the value of a date input is a machine format");
        RawDateFormats("@((Model.Price / 2).ToString(\"N0\"))").Should().BeEmpty("a number format is not a date");
        RawDateFormats("@* @x.ToString(\"MMM dd\") *@").Should().BeEmpty("a comment is not markup");
    }

    [Fact]
    public void ShopSheets_HoldTheWholeImageRule()
    {
        WholeImageRuleGaps(ReadRepoFile("QuestBoard.Service/wwwroot/css/shop-pages.css")).Should().BeEmpty(
            "a picture in a description shrinks to its column and keeps its proportions");
    }

    [Fact]
    public void WholeImageRuleScanner_FlagsAMissingRuleAndMissingDeclarations()
    {
        WholeImageRuleGaps(".item-details-content .markdown-content img { max-inline-size: 100%; block-size: auto; }").Should().BeEmpty();
        WholeImageRuleGaps(".item-details-content .markdown-content img { max-inline-size: 100%; }").Should().ContainSingle(g => g.Contains("block-size"));
        WholeImageRuleGaps(".item-details-content .markdown-content img { block-size: auto; }").Should().ContainSingle(g => g.Contains("max-inline-size"));
        WholeImageRuleGaps(".other img { max-inline-size: 100%; block-size: auto; }").Should().ContainSingle();
        WholeImageRuleGaps("/* .item-details-content .markdown-content img { max-inline-size: 100%; block-size: auto; } */").Should().ContainSingle();
    }

    [Fact]
    public void EmblemTile_HoldsNoDeclarationThatCouldClipTheGlyph()
    {
        foreach (var path in new[] { "QuestBoard.Service/wwwroot/css/shop-pages.css", "QuestBoard.Service/wwwroot/css/shop-pages.mobile.css" })
        {
            var css = Regex.Replace(ReadRepoFile(path), @"/\*[\s\S]*?\*/", " ");
            foreach (Match rule in Regex.Matches(css, @"(?<selector>[^{}]*\.emblem[^{}]*)\{(?<body>[^{}]*)\}"))
                Regex.Matches(rule.Groups["body"].Value, @"(?<![\w-])(?:overflow(?:-[xy])?|block-size|height|max-block-size|max-height)\s*:")
                    .Select(m => m.Value).Should().BeEmpty(
                        $"{path}: {rule.Groups["selector"].Value.Trim()} sizes the tile by a minimum only, so it grows with its content and never clips");
        }
    }
}
