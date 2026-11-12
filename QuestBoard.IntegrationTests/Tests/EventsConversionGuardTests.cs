using System.Text.RegularExpressions;
using FluentAssertions;

namespace QuestBoard.IntegrationTests.Tests;

/// <summary>
/// Pins the events, series, calendar and agenda views as they are moved onto the layered
/// foundation: no view in the area formats a date itself (every date goes through the shared
/// date helpers), a converted view carries no style attribute, style element, inline script or
/// inline event handler, a converted sheet opens the pages layer and holds no important flag or
/// colour literal, no area sheet restyles a card title, a view links the sheets it needs from its
/// Styles section, and none of the files this work touches names the planning that produced it.
/// The lists below only ever grow as each view is converted; a file a later change deletes is
/// removed from them in the same commit. An integration suite reading rendered markup cannot see
/// what a comment says, which sheet a rule came from or which expression produced a date, so only
/// a scan of the sources can hold the line.
/// </summary>
public class EventsConversionGuardTests
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

    private const string ViewsRoot = "QuestBoard.Service/Views/";
    private const string CssRoot = "QuestBoard.Service/wwwroot/css";

    // Every view of the area, relative to the views folder. A file that does not exist is skipped
    // so a view a later change deletes does not fail the scan before its entry is removed.
    private static readonly string[] AreaFiles =
    [
        "Calendar/Index.cshtml",
        "Calendar/Index.Mobile.cshtml",
        "Agenda/Index.cshtml",
        "Agenda/Index.Mobile.cshtml",
        "Events/Index.cshtml",
        "Events/Index.Mobile.cshtml",
        "Events/Create.cshtml",
        "Events/Details.cshtml",
        "Events/Edit.cshtml",
        "Series/Details.cshtml",
        "Shared/_Calendar.cshtml",
        "Shared/_Calendar.Mobile.cshtml",
        "Shared/_LinkedDayEntries.cshtml",
        "Shared/_LinkedAlsoThatDay.cshtml",
        "Events/_AvailabilityCell.cshtml",
        "Events/_AvailabilityCounts.cshtml",
    ];

    // Views that carry no inline code, repository-relative. Grows as each view is converted.
    private static readonly string[] ConvertedViews =
    [
        "QuestBoard.Service/Views/Events/Index.cshtml",
        "QuestBoard.Service/Views/Events/Index.Mobile.cshtml",
        "QuestBoard.Service/Views/Events/Create.cshtml",
        "QuestBoard.Service/Views/Events/Details.cshtml",
        "QuestBoard.Service/Views/Events/Edit.cshtml",
        "QuestBoard.Service/Views/Events/_AvailabilityCell.cshtml",
        "QuestBoard.Service/Views/Events/_AvailabilityCounts.cshtml",
        "QuestBoard.Service/Views/Series/Details.cshtml",
        "QuestBoard.Service/Views/Agenda/Index.cshtml",
        "QuestBoard.Service/Views/Agenda/Index.Mobile.cshtml",
    ];

    // Files this work retires; each is deleted and must stay deleted, because its behaviour moved into a script file.
    private static readonly string[] RetiredFiles =
    [
        "QuestBoard.Service/Views/Events/_SeriesFormScripts.cshtml",
    ];

    // Page sheets under the css folder that open the pages layer, by file name.
    private static readonly string[] ConvertedSheets =
    [
        "events-overview.css",
        "events-overview.mobile.css",
        "event-forms.css",
        "agenda.css",
        "agenda.mobile.css",
    ];

    // For each view, the sheets it must link from its Styles section, by file name.
    private static readonly Dictionary<string, string[]> ViewSheetLinks = new()
    {
        ["QuestBoard.Service/Views/Events/Index.cshtml"] = ["events-overview.css"],
        ["QuestBoard.Service/Views/Events/Index.Mobile.cshtml"] = ["events-overview.mobile.css"],
        ["QuestBoard.Service/Views/Events/Create.cshtml"] = ["event-forms.css"],
        ["QuestBoard.Service/Views/Events/Edit.cshtml"] = ["event-forms.css"],
        ["QuestBoard.Service/Views/Agenda/Index.cshtml"] = ["agenda.css"],
        ["QuestBoard.Service/Views/Agenda/Index.Mobile.cshtml"] = ["agenda.mobile.css"],
    };

    // The area files scanned for raw date formatting: all of them.
    private static readonly string[] DateCheckedFiles = AreaFiles;

    // Every source file this work creates or edits, repository-relative, scanned for planning
    // references. This test file is left out because it has to name the shapes it forbids.
    private static readonly string[] PhaseSourceFiles =
    [
        "QuestBoard.Service/Extensions/HtmlHelperExtensions.cs",
        "QuestBoard.Service/ViewModels/CalendarViewModels/QuestOnDay.cs",
        "QuestBoard.Service/Controllers/QuestBoard/CalendarController.cs",
        "QuestBoard.Service/Views/Calendar/Index.cshtml",
        "QuestBoard.Service/Views/Calendar/Index.Mobile.cshtml",
        "QuestBoard.Service/Views/Agenda/Index.cshtml",
        "QuestBoard.Service/Views/Agenda/Index.Mobile.cshtml",
        "QuestBoard.Service/Views/Events/Index.cshtml",
        "QuestBoard.Service/Views/Events/Index.Mobile.cshtml",
        "QuestBoard.Service/Views/Events/Create.cshtml",
        "QuestBoard.Service/Views/Events/Details.cshtml",
        "QuestBoard.Service/Views/Events/Edit.cshtml",
        "QuestBoard.Service/Views/Series/Details.cshtml",
        "QuestBoard.Service/Views/Shared/_Calendar.cshtml",
        "QuestBoard.Service/Views/Shared/_Calendar.Mobile.cshtml",
        "QuestBoard.Service/wwwroot/js/row-link.js",
        "QuestBoard.Service/wwwroot/js/event-details.js",
        "QuestBoard.Service/wwwroot/js/event-series-form.js",
        "QuestBoard.Service/wwwroot/js/agenda.js",
        "QuestBoard.Service/wwwroot/css/events-overview.css",
        "QuestBoard.Service/wwwroot/css/events-overview.mobile.css",
        "QuestBoard.Service/wwwroot/css/event-forms.css",
        "QuestBoard.Service/wwwroot/css/agenda.css",
        "QuestBoard.Service/wwwroot/css/agenda.mobile.css",
        "QuestBoard.Service/Views/Events/_AvailabilityCell.cshtml",
        "QuestBoard.Service/Views/Events/_AvailabilityCounts.cshtml",
        ".claude/ui-guidelines.md",
        "QuestBoard.UnitTests/Extensions/WallClockAreaStylesTests.cs",
        "QuestBoard.IntegrationTests/Tests/EventsDateRenderTests.cs",
        "QuestBoard.IntegrationTests/Controllers/CalendarBoardClockTests.cs",
    ];

    // A date-pattern format string handed to ToString, other than the machine values a date input
    // or an attribute reads back, which have to stay in the pattern they are parsed with.
    private static readonly Regex DateFormatCall = new(
        @"\.ToString\(\s*""(?<format>[^""]*(?:yy|MM|dd|HH|hh|mm|ss|tt)[^""]*)""",
        RegexOptions.Compiled);

    private static readonly string[] MachineValueFormats = ["yyyy-MM-dd", "yyyy-MM-ddTHH:mm"];

    private static List<string> RawDateFormats(string view) =>
        DateFormatCall.Matches(view)
            .Select(m => m.Groups["format"].Value)
            .Where(f => !MachineValueFormats.Contains(f))
            .ToList();

    // Each rule is regex data so this file never holds the markup it forbids elsewhere.
    private static readonly (string Label, Regex Pattern)[] InlineCodePatterns =
    [
        ("a style attribute", new Regex(@"\sstyle\s*=", RegexOptions.IgnoreCase)),
        ("a style element", new Regex(@"<style\b", RegexOptions.IgnoreCase)),
        ("an inline script element", new Regex(@"<script\b(?![^>]*\bsrc\s*=)[^>]*>", RegexOptions.IgnoreCase)),
        ("an inline event handler", new Regex(@"\son[a-z]+\s*=", RegexOptions.IgnoreCase)),
    ];

    private static string SheetPath(string name) => CssRoot + "/" + name;

    private static string StripCssComments(string css) => Regex.Replace(css, @"/\*[\s\S]*?\*/", " ");

    private static string StripUrls(string css) =>
        Regex.Replace(css, @"url\(\s*(?:""[^""]*""|'[^']*'|[^)]*)\s*\)", "url()", RegexOptions.IgnoreCase);

    // Whether the stylesheet text (comments stripped) opens the pages layer first.
    private static bool OpensPagesLayer(string strippedCss) =>
        strippedCss.TrimStart().StartsWith("@layer pages {", StringComparison.Ordinal);

    private static readonly Regex ImportantFlag = new(@"!\s*important", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Written as regex data so this file never holds a literal it forbids elsewhere: a hex colour
    // of 3, 4, 6 or 8 digits (not an id selector with a longer name), and the colour functions.
    private static readonly (string Label, Regex Pattern)[] ColourLiteralPatterns =
    [
        ("a hex colour", new Regex(
            @"#(?:[0-9a-fA-F]{8}|[0-9a-fA-F]{6}|[0-9a-fA-F]{4}|[0-9a-fA-F]{3})(?![\w-])", RegexOptions.Compiled)),
        ("an rgb() colour", new Regex(@"\brgba?\(", RegexOptions.Compiled | RegexOptions.IgnoreCase)),
        ("an hsl() colour", new Regex(@"\bhsla?\(", RegexOptions.Compiled | RegexOptions.IgnoreCase)),
    ];

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
            StylesSection(view),
            @"<link\b[^>]*\bhref=""~/css/" + Regex.Escape(sheetName) + @"""[^>]*>");

    // A rule is a selector and the declarations between the innermost braces; a layer or media wrapper
    // holds braces itself, so it never matches and only the rules inside it do.
    private static readonly Regex CssRule = new(@"(?<selector>[^{}]+)\{(?<body>[^{}]*)\}", RegexOptions.Compiled);

    private static readonly Regex TitleTypography = new(
        @"(?:^|[;\s])(?:font-size|font-family|letter-spacing)\s*:", RegexOptions.Compiled);

    // A selector that styles a card title: the card header itself, or a first or second level heading
    // inside a card.
    private static bool NamesCardTitle(string selector) =>
        selector.Contains("modern-card-header", StringComparison.Ordinal)
        || (selector.Contains("modern-card", StringComparison.Ordinal) && Regex.IsMatch(selector, @"\bh[12]\b"));

    private static List<string> CardTitleTypographyRules(string strippedCss) =>
        CssRule.Matches(strippedCss)
            .Where(m => NamesCardTitle(m.Groups["selector"].Value) && TitleTypography.IsMatch(m.Groups["body"].Value))
            .Select(m => m.Groups["selector"].Value.Trim())
            .ToList();

    // Written as regex data, each forbidden shape built from fragments, so this file never holds
    // the shapes it forbids elsewhere.
    private static readonly (string Label, Regex Pattern)[] PlanningReferencePatterns =
    [
        ("a decision id", new Regex(@"\bD" + @"-(?:0[1-9]|[1-9][0-9])\b")),
        ("a question id", new Regex(@"\bQ" + @"-\d{2,3}\b")),
        ("a sign-off item id", new Regex(@"\bG" + @"[12]-[A-Z]\d\b")),
        ("a definition-of-converted id", new Regex(@"\bDo" + @"C-\d{1,2}\b")),
        ("an audit finding id", new Regex(@"\bA" + @"-events-\d{3}\b")),
        ("a threat id", new Regex(@"\bT" + @"-97-\d{2}\b")),
        ("a phase reference", new Regex("Pha" + "se\\s+9" + "7\\b", RegexOptions.IgnoreCase)),
        ("a plan id", new Regex(@"\b9" + @"7-(?:\d\d|[A-Z]{2,})")),
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

    private static string StripRazorAndHtmlComments(string view)
    {
        var withoutRazor = Regex.Replace(view, @"@\*[\s\S]*?\*@", " ");
        return Regex.Replace(withoutRazor, @"<!--[\s\S]*?-->", " ");
    }

    // ---- Guards ---------------------------------------------------------------------------------

    [Fact]
    public void AreaViews_RenderNoRawDateFormatting()
    {
        var scanned = 0;
        foreach (var relative in DateCheckedFiles)
        {
            var path = ResolveRepoFile((ViewsRoot + relative).Split('/'));
            if (!File.Exists(path))
                continue;

            scanned++;
            RawDateFormats(StripRazorAndHtmlComments(File.ReadAllText(path))).Should().BeEmpty(
                $"{relative} must render dates through the date helpers, not a format string");
        }

        scanned.Should().BeGreaterThan(0, "the scan must find the area's views, or it guards nothing");
    }

    [Fact]
    public void ConvertedViews_CarryNoInlineStyleScriptOrHandler()
    {
        foreach (var path in ConvertedViews)
        {
            var view = StripRazorAndHtmlComments(ReadRepoFile(path));
            foreach (var (label, pattern) in InlineCodePatterns)
                pattern.Matches(view).Select(m => m.Value).Should().BeEmpty(
                    $"{path} must carry no inline code; {label} belongs in a stylesheet or a script file");
        }
    }

    [Fact]
    public void ConvertedSheets_OpenThePagesLayer()
    {
        foreach (var name in ConvertedSheets)
            OpensPagesLayer(StripCssComments(ReadRepoFile(SheetPath(name)))).Should().BeTrue(
                $"{name} must open the pages layer so the foundation's cascade order applies to it");
    }

    [Fact]
    public void ConvertedSheets_HaveNoImportantFlag()
    {
        foreach (var name in ConvertedSheets)
            ImportantFlag.Matches(StripCssComments(ReadRepoFile(SheetPath(name)))).Should().BeEmpty(
                $"{name} sits in a layer, where an important flag is never needed");
    }

    [Fact]
    public void ConvertedSheets_HaveNoColourLiteral()
    {
        foreach (var name in ConvertedSheets)
        {
            var css = StripUrls(StripCssComments(ReadRepoFile(SheetPath(name))));
            foreach (var (label, pattern) in ColourLiteralPatterns)
                pattern.Matches(css).Select(m => m.Value).Should().BeEmpty(
                    $"{name} must name colours by token; {label} belongs in the token file");
        }
    }

    [Fact]
    public void ConvertedViews_LinkTheirSheets()
    {
        foreach (var (path, sheets) in ViewSheetLinks)
        {
            var view = StripRazorAndHtmlComments(ReadRepoFile(path));
            foreach (var sheet in sheets)
                LinksSheet(view, sheet).Should().BeTrue(
                    $"{path} must link {sheet} from its Styles section so the sheet loads only where it is used");
        }
    }

    [Fact]
    public void RowLinkModule_WritesNoMarkupFromStrings()
    {
        // The module only reads an address the server rendered and assigns it to the location.
        var script = ReadRepoFile("QuestBoard.Service/wwwroot/js/row-link.js");
        Regex.Matches(script, @"\.(?:innerHTML|outerHTML)\s*=|insertAdjacentHTML|document\.write")
            .Select(m => m.Value).Should().BeEmpty("row-link.js must build no markup from strings");
    }

    [Fact]
    public void EventDetailsModule_WritesNoMarkupFromStrings()
    {
        // The module posts the viewer's answer and shows server text through alert() and confirm(),
        // which take plain text, so it needs no markup from strings.
        var script = ReadRepoFile("QuestBoard.Service/wwwroot/js/event-details.js");
        Regex.Matches(script, @"\.(?:innerHTML|outerHTML)\s*=|insertAdjacentHTML|document\.write")
            .Select(m => m.Value).Should().BeEmpty("event-details.js must build no markup from strings");
    }

    [Fact]
    public void AgendaModule_WritesNoMarkupFromStrings()
    {
        // The module copies a board id, a return address and a board name from the trigger into the dialog, as
        // field values and text content, so it never hands a string to the parser.
        var script = ReadRepoFile("QuestBoard.Service/wwwroot/js/agenda.js");
        Regex.Matches(script, @"\.(?:innerHTML|outerHTML)\b|insertAdjacentHTML|document\.write")
            .Select(m => m.Value).Should().BeEmpty("agenda.js must build no markup from strings");
    }

    [Fact]
    public void RetiredFiles_AreGone()
    {
        foreach (var path in RetiredFiles)
            File.Exists(ResolveRepoFile(path.Split('/'))).Should().BeFalse(
                $"{path} was folded into a script file and must not come back");
    }

    [Fact]
    public void SeriesFormModule_WritesNoMarkupFromStrings()
    {
        // The module writes dates and notices the server returned into the page, so it builds every node with
        // createElement and textContent and never hands a string to the parser.
        var script = ReadRepoFile("QuestBoard.Service/wwwroot/js/event-series-form.js");
        Regex.Matches(script, @"\.(?:innerHTML|outerHTML)\b|insertAdjacentHTML|document\.write")
            .Select(m => m.Value).Should().BeEmpty("event-series-form.js must build no markup from strings");
    }

    [Fact]
    public void AreaSheets_DoNotRestyleCardTitles()
    {
        // The card title step belongs to the foundation: a page title is the display step, a sidebar card title the
        // heading step. An area sheet may recolour a title but never resize it or change its face or spacing.
        foreach (var name in ConvertedSheets)
            CardTitleTypographyRules(StripCssComments(ReadRepoFile(SheetPath(name)))).Should().BeEmpty(
                $"{name} must not set the size, face or letter spacing of a card title");
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
    public void Scanners_FlagWhatTheyForbidAndAllowWhatTheyPermit()
    {
        // Without a known-bad sample the guards above would pass on an empty list whether or not a
        // scanner worked, so each scanner is shown to fire here and to stay quiet on a clean sample.
        RawDateFormats("@Model.Date.ToString(\"dddd, MMMM d\")").Should().HaveCount(1);
        RawDateFormats("@Model.Date.ToString(\"" + "HH:mm" + "\")").Should().HaveCount(1);
        RawDateFormats("@Model.Date.ToString(\"yyyy-MM-dd\")").Should().BeEmpty();
        RawDateFormats("@Model.Date.ToString(\"yyyy-MM-ddTHH:mm\")").Should().BeEmpty();
        RawDateFormats("@Model.Count.ToString(\"N0\")").Should().BeEmpty();
        RawDateFormats("@flag.ToString().ToLower()").Should().BeEmpty();

        // A commented-out call is not rendered, so it is not flagged once comments are stripped.
        RawDateFormats(StripRazorAndHtmlComments("@* @d.ToString(\"dddd\") *@ <!-- @d.ToString(\"dddd\") -->"))
            .Should().BeEmpty();

        CardTitleTypographyRules("@layer pages { .x .modern-card-header h1 { font-size: 1rem; } }")
            .Should().HaveCount(1);
        CardTitleTypographyRules(".modern-card--aside .modern-card-header h2 { letter-spacing: normal; }")
            .Should().HaveCount(1);
        CardTitleTypographyRules(".calendar-page .modern-card-header h1 { color: var(--qb-fg); }").Should().BeEmpty();
        CardTitleTypographyRules(".avail-count-headline { font-size: 1rem; }").Should().BeEmpty();

        LinksSheet("@section Styles { <link rel=\"stylesheet\" href=\"~/css/a.css\" /> }", "a.css").Should().BeTrue();
        LinksSheet("<link rel=\"stylesheet\" href=\"~/css/a.css\" />", "a.css").Should().BeFalse();

        var planning = "// see " + "D" + "-09 and " + "Q" + "-116, " + "Pha" + "se 97" + ", 97" + "-06 in " + "RESE" + "ARCH.md";
        PlanningReferencePatterns.Count(p => p.Pattern.IsMatch(planning)).Should().BeGreaterThanOrEqualTo(5);
        PlanningReferencePatterns.Count(p => p.Pattern.IsMatch("// keyboard users must be able to leave the field"))
            .Should().Be(0);
    }
}
