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
        "QuestBoard.Service/Views/Calendar/Index.Mobile.cshtml",
        "QuestBoard.Service/Views/Shared/_Calendar.Mobile.cshtml",
        "QuestBoard.Service/Views/Shared/_LinkedAlsoThatDay.cshtml",
        "QuestBoard.Service/Views/Calendar/Index.cshtml",
        "QuestBoard.Service/Views/Shared/_Calendar.cshtml",
        "QuestBoard.Service/Views/Shared/_LinkedDayEntries.cshtml",
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
        "calendar.mobile.css",
        "calendar.css",
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
        ["QuestBoard.Service/Views/Calendar/Index.Mobile.cshtml"] = ["calendar.mobile.css"],
        ["QuestBoard.Service/Views/Calendar/Index.cshtml"] = ["calendar.css"],
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
        "QuestBoard.Service/wwwroot/js/calendar-month.js",
        "QuestBoard.Service/wwwroot/css/events-overview.css",
        "QuestBoard.Service/wwwroot/css/events-overview.mobile.css",
        "QuestBoard.Service/wwwroot/css/event-forms.css",
        "QuestBoard.Service/wwwroot/css/agenda.css",
        "QuestBoard.Service/wwwroot/css/agenda.mobile.css",
        "QuestBoard.Service/wwwroot/css/calendar.mobile.css",
        "QuestBoard.Service/wwwroot/css/calendar.css",
        "QuestBoard.Service/Views/Events/_AvailabilityCell.cshtml",
        "QuestBoard.Service/Views/Events/_AvailabilityCounts.cshtml",
        ".claude/ui-guidelines.md",
        "QuestBoard.UnitTests/Extensions/WallClockAreaStylesTests.cs",
        "QuestBoard.IntegrationTests/Tests/EventsDateRenderTests.cs",
        "QuestBoard.IntegrationTests/Controllers/CalendarBoardClockTests.cs",
        "QuestBoard.IntegrationTests/Tests/CalendarPhoneAgendaTests.cs",
        "QuestBoard.IntegrationTests/Tests/CalendarMonthMarkupTests.cs",
        "QuestBoard.Service/Views/Shared/_LinkedDayEntries.cshtml",
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
    public void CalendarSheet_LoadsOnlyWhereItIsUsed()
    {
        // The month and Quest Details are the only pages that draw the calendar grid, so the sheet is
        // linked from their own Styles sections and not from the shared layout.
        ReadRepoFile("QuestBoard.Service/Views/Shared/_Layout.cshtml").Should().NotContain("calendar.css",
            "the calendar sheet is page-specific and must not load on every page");
        ReadRepoFile("QuestBoard.Service/Views/Shared/_Layout.Mobile.cshtml").Should().NotContain("calendar.css");

        LinksSheet(StripRazorAndHtmlComments(ReadRepoFile("QuestBoard.Service/Views/Quest/Details.cshtml")), "calendar.css")
            .Should().BeTrue("Quest Details draws the same grid and links the sheet itself");
        ReadRepoFile("QuestBoard.Service/Views/Calendar/Index.cshtml").Should().Contain("calendar-page",
            "the month rules are scoped to the page wrapper");
    }

    [Fact]
    public void CalendarSheet_HasNoTransitionAll()
    {
        Regex.IsMatch(StripCssComments(ReadRepoFile(SheetPath("calendar.css"))), @"transition\s*:\s*all\b")
            .Should().BeFalse("a transition names the properties it animates");
    }

    [Fact]
    public void CalendarSheet_KeepsMonthAndQuestDetailsRulesApart()
    {
        // Every rule is either the shared weekday header, the month's own wrapper (the page root that holds
        // its sizing variables), sits under one of the two page scopes, or belongs to the month's floating card
        // or day panel, which the script mounts on the body (so they cannot sit under the page wrapper) and
        // which no other page renders. A rule written for the month can therefore never reach Quest Details
        // and the other way round. The steps of the fade keyframes are not selectors.
        var allowedStarts = new[] { ".calendar-page ", ".details-page ", ".calendar-grid.details-page", ".quest-details-page " };
        var overlayRoots = new[] { ".cal-card", ".cal-panel" };
        var keyframeSteps = new[] { "from", "to" };
        var shared = new[] { ".calendar-header", ".calendar-day-header", ".calendar-day-header:last-child" };

        var unscoped = CssRuleSelectors(StripCssComments(ReadRepoFile(SheetPath("calendar.css"))))
            .Where(sel => !shared.Contains(sel) && sel != ".calendar-page" && !keyframeSteps.Contains(sel)
                          && !allowedStarts.Any(start => sel.StartsWith(start, StringComparison.Ordinal))
                          && !overlayRoots.Any(root => sel.StartsWith(root, StringComparison.Ordinal)))
            .ToList();

        unscoped.Should().BeEmpty("only the weekday header and the month's own floating card and panel may sit outside the two page scopes");
    }

    [Fact]
    public void CalendarWeekdayHeader_IsAWoodBandWithParchmentText()
    {
        var css = StripCssComments(ReadRepoFile(SheetPath("calendar.css")));

        Regex.IsMatch(css, @"\.calendar-header\s*\{[^}]*background-color:\s*var\(--qb-wood\)[^}]*color:\s*var\(--qb-fg\)")
            .Should().BeTrue("the weekday band is the wood fill with parchment text, which holds 4.5:1 on both pages");
        Regex.IsMatch(css, @"\.calendar-day-header\s*\{[^}]*color:\s*var\(--qb-fg\)")
            .Should().BeTrue("each weekday name takes the parchment colour");
        Regex.IsMatch(css, @"\.calendar-day-header\s*\{[^}]*text-shadow").Should().BeFalse(
            "a shadow behind text on a solid fill only blurs it");
    }

    // The declaration block of the one rule whose whole selector is the given text, in a stylesheet with its
    // comments stripped.
    private static string RuleBodyOf(string strippedCss, string selector) =>
        CssRule.Matches(strippedCss)
            .Where(m => m.Groups["selector"].Value.Trim() == selector)
            .Select(m => m.Groups["body"].Value)
            .Single();

    [Fact]
    public void CalendarTitle_StaysParchment()
    {
        var css = StripCssComments(ReadRepoFile(SheetPath("calendar.css")));

        OpensPagesLayer(css).Should().BeTrue("the rule belongs to the pages layer, which beats the foundation's gold heading");
        var body = RuleBodyOf(css, ".calendar-page .modern-card-header h1");

        Regex.Replace(body, @"\s+", "").Should().Be("color:var(--qb-fg);",
            "the title keeps the parchment colour through the token and declares nothing else on a card title");
    }

    [Fact]
    public void CalendarHeader_UsesTheCompactBandPadding()
    {
        var css = StripCssComments(ReadRepoFile(SheetPath("calendar.css")));

        var body = RuleBodyOf(css, ".calendar-page .modern-card-header");

        body.Should().Contain("padding-block: var(--qb-space-xs)");
        body.Should().Contain("padding-inline: var(--qb-space-md)");
    }

    // The selector of every rule in a stylesheet (comments stripped), one entry per comma-separated selector;
    // a layer or media wrapper is entered, other at-rules are skipped.
    private static IEnumerable<string> CssRuleSelectors(string css)
    {
        var result = new List<string>();
        var start = 0;
        for (var i = 0; i < css.Length; i++)
        {
            if (css[i] == '{')
            {
                var prelude = css[start..i].Trim();
                if (!prelude.StartsWith('@'))
                    result.AddRange(prelude.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim()));

                start = i + 1;
            }
            else if (css[i] == '}' || css[i] == ';')
            {
                start = i + 1;
            }
        }

        return result;
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

    private const string CalendarMonthScript = "QuestBoard.Service/wwwroot/js/calendar-month.js";

    // The text of the braces that open at the first '{' at or after the given index, counted to the matching close.
    private static string BracedBlockFrom(string text, int index)
    {
        var open = text.IndexOf('{', index);
        open.Should().BeGreaterThan(-1);
        var depth = 0;
        for (var i = open; i < text.Length; i++)
        {
            if (text[i] == '{') depth++;
            else if (text[i] == '}' && --depth == 0) return text[open..(i + 1)];
        }

        throw new InvalidOperationException("Unbalanced braces in the scanned script.");
    }

    // The text of the call whose opening parenthesis is the first one at or after the given index.
    private static string CallArgumentsFrom(string text, int index)
    {
        var open = text.IndexOf('(', index);
        open.Should().BeGreaterThan(-1);
        var depth = 0;
        for (var i = open; i < text.Length; i++)
        {
            if (text[i] == '(') depth++;
            else if (text[i] == ')' && --depth == 0) return text[open..(i + 1)];
        }

        throw new InvalidOperationException("Unbalanced parentheses in the scanned script.");
    }

    private static string FunctionBody(string script, string name)
    {
        var start = script.IndexOf("function " + name + "(", StringComparison.Ordinal);
        start.Should().BeGreaterThan(-1, $"the script defines {name}");
        return BracedBlockFrom(script, script.IndexOf(')', start));
    }

    [Fact]
    public void CalendarMonthModule_WritesNoMarkupFromStrings()
    {
        // The month script copies server-encoded nodes and sets text, so it builds every node with
        // createElement, cloneNode and textContent, and the only style it writes is a custom property.
        var script = ReadRepoFile(CalendarMonthScript);
        Regex.Matches(script, @"\.(?:innerHTML|outerHTML)\b|insertAdjacentHTML|document\.write|cssText")
            .Select(m => m.Value).Should().BeEmpty("calendar-month.js must build no markup from strings");
        Regex.Matches(script, @"\.style\.(?!setProperty\b|removeProperty\b)\w+")
            .Select(m => m.Value).Should().BeEmpty("the only style the month script writes is a custom property");
        Regex.Matches(script, @"\.style\.setProperty\(\s*'(?!--pop-[xy]')[^']*'")
            .Select(m => m.Value).Should().BeEmpty("the card and panel are positioned only through --pop-x and --pop-y");
    }

    [Fact]
    public void CalendarMonthModule_ReadsTheSlotCountAndHasNoHoverOpen()
    {
        var script = ReadRepoFile(CalendarMonthScript);
        script.Should().Contain("getPropertyValue('--cal-slots')", "the slot count comes from the stylesheet's height bands");

        // The day panel opens from a click, a tap, Enter or Space on "+N more" and from nothing else: no hover
        // listener reaches the opener, and neither the opener nor the closer waits on a timer.
        foreach (Match listener in Regex.Matches(script, @"addEventListener\(\s*'(?:mouseenter|mouseover|mousemove|pointerenter|pointerover|pointermove)'"))
        {
            CallArgumentsFrom(script, listener.Index).Should().NotContain("openPanel",
                "a hover must never open the day panel");
        }

        foreach (var name in new[] { "openPanel", "closePanel" })
        {
            FunctionBody(script, name).Should().NotMatchRegex(@"setTimeout|setInterval|requestAnimationFrame",
                $"{name} must not wait on a timer");
        }

        script.Should().NotMatchRegex(@"data-open-mode|cal-mode", "there is no hover or click mode switch");
    }

    [Fact]
    public void CalendarMonthModule_MountsOverlaysOnBody()
    {
        // The month card is a blurred panel, and a fixed-position child of a blurred element is positioned
        // against that element and not the window, so the card and the panel move to the body at start-up and
        // are placed from viewport rectangles.
        var script = ReadRepoFile(CalendarMonthScript);
        script.Should().MatchRegex(@"document\.body\.(?:append|appendChild)\(", "the overlays are mounted on the body");
        script.Should().Contain("getBoundingClientRect()", "the overlays are placed from viewport rectangles");
        script.Should().Contain("cloneNode(true)", "the card shows a copy of the entry's server-rendered detail");
    }

    // A view or sheet the area keeps on purpose without converting, with the reason. It stays empty: the area is
    // converted in full, and an entry here would be a deliberate exception that has to be argued for.
    private static readonly Dictionary<string, string> UnconvertedByDecision = new();

    // The area's own folders under the views folder: a view added to one of them has to join the guard lists.
    private static readonly string[] AreaViewFolders = ["Calendar", "Agenda", "Events", "Series"];

    // The calendar, overview and agenda sheets with their phone twins, plus the shared event form sheet.
    private static readonly string[] AreaSheets =
    [
        "calendar.css",
        "calendar.mobile.css",
        "events-overview.css",
        "events-overview.mobile.css",
        "agenda.css",
        "agenda.mobile.css",
        "event-forms.css",
    ];

    [Fact]
    public void PhaseScope_IsFullyConverted()
    {
        var views = AreaFiles
            .Select(f => ViewsRoot + f)
            .Where(f => File.Exists(ResolveRepoFile(f.Split('/'))))
            .Where(f => !UnconvertedByDecision.ContainsKey(f))
            .ToList();
        var sheets = AreaSheets
            .Where(n => File.Exists(ResolveRepoFile(SheetPath(n).Split('/'))))
            .Where(n => !UnconvertedByDecision.ContainsKey(n))
            .ToList();

        views.Should().NotBeEmpty("the scan must find the area's views, or it guards nothing");
        sheets.Should().HaveCount(AreaSheets.Length, "every area sheet is present and none was dropped from the area");
        views.Except(ConvertedViews).Should().BeEmpty(
            "every area view that still exists must be guarded as converted, or listed with the reason it is not");
        sheets.Except(ConvertedSheets).Should().BeEmpty(
            "every area sheet that still exists must be guarded as converted, or listed with the reason it is not");

        // A view dropped into one of the area's folders must join AreaFiles, or it would escape this scan.
        var onDisk = AreaViewFolders
            .SelectMany(folder => Directory.EnumerateFiles(
                ResolveRepoFile((ViewsRoot + folder).Split('/')), "*.cshtml"))
            .Select(path => Path.GetRelativePath(ResolveRepoFile(ViewsRoot.TrimEnd('/').Split('/')), path)
                .Replace('\\', '/'))
            .ToList();
        onDisk.Should().NotBeEmpty();
        onDisk.Except(AreaFiles).Should().BeEmpty("every view in the area's folders must be listed in AreaFiles");

        UnconvertedByDecision.Should().BeEmpty("the area is converted in full, with no view or sheet kept back");
    }

    [Fact]
    public void Layouts_LinkNoAreaSheet()
    {
        // The area's sheets load from the Styles section of the pages that use them, never for every page.
        foreach (var layout in new[]
                 {
                     "QuestBoard.Service/Views/Shared/_Layout.cshtml",
                     "QuestBoard.Service/Views/Shared/_Layout.Mobile.cshtml",
                 })
        {
            var text = StripRazorAndHtmlComments(ReadRepoFile(layout));
            foreach (var sheet in AreaSheets)
                text.Should().NotContain("css/" + sheet,
                    $"{layout} must not link {sheet}: it is page-specific and belongs in the Styles section of its pages");
        }
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
