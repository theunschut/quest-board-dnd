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
}
