using System.Text.RegularExpressions;
using FluentAssertions;
using QuestBoard.Service.Helpers;

namespace QuestBoard.IntegrationTests.Tests;

/// <summary>
/// Pins the quest board, quest log and shared Markdown editor views and sheets as they are moved
/// onto the layered foundation: a converted view carries no style attribute, style element, inline
/// script or inline event handler and renders no raw date formatting; a converted sheet opens the
/// pages layer and holds no important flag or colour literal; a view links the sheets it needs from
/// its Styles section; and none of the files names the planning work that produced them. The lists
/// below grow as each view or sheet is converted, and a deleted file is removed from them in the
/// same commit. An integration suite reading rendered markup cannot see which sheet a rule came
/// from or what a comment says, so only a scan of the sources can hold the line.
/// </summary>
public class QuestsConversionGuardTests
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

    // Views that carry no inline code and no raw date formatting, repository-relative.
    private static readonly string[] ConvertedViews =
    [
        "QuestBoard.Service/Views/Shared/_MarkdownEditor.cshtml",
        "QuestBoard.Service/Views/Quest/_QuestFormScripts.cshtml",
        "QuestBoard.Service/Views/Shared/_CharacterSelectModal.cshtml",
        "QuestBoard.Service/Views/Quest/Details.cshtml",
        "QuestBoard.Service/Views/Quest/Details.Mobile.cshtml",
        "QuestBoard.Service/Views/Quest/Create.cshtml",
        "QuestBoard.Service/Views/Quest/Create.Mobile.cshtml",
        "QuestBoard.Service/Views/Quest/Edit.cshtml",
        "QuestBoard.Service/Views/Quest/CreateFollowUp.cshtml",
        "QuestBoard.Service/Views/Quest/Index.cshtml",
        "QuestBoard.Service/Views/Quest/Index.Mobile.cshtml",
        "QuestBoard.Service/Views/Quest/Manage.cshtml",
        "QuestBoard.Service/Views/Quest/Manage.Mobile.cshtml",
        "QuestBoard.Service/Views/QuestLog/Index.cshtml",
        "QuestBoard.Service/Views/QuestLog/Index.Mobile.cshtml",
        "QuestBoard.Service/Views/QuestLog/Details.cshtml",
        "QuestBoard.Service/Views/QuestLog/EditRecap.cshtml",
        "QuestBoard.Service/Views/Move/Index.cshtml",
    ];

    // Page sheets under the css folder that open the pages layer, by file name.
    private static readonly string[] ConvertedSheets =
        ["quest-details.css", "quest-vote.css", "quests.mobile.css", "quest-form.css", "dm-create.mobile.css", "home.mobile.css", "quest-manage.css", "dm-manage.mobile.css", "quests.css", "quest-log.css", "quest-log.mobile.css", "move-entry.css", "markdown-editor.css", "markdown-content.css"];

    // For each view, the sheets it must link from its Styles section, by file name.
    private static readonly Dictionary<string, string[]> ViewSheetLinks = new()
    {
        ["QuestBoard.Service/Views/Quest/Details.cshtml"] = ["calendar.css", "quest-details.css", "quest-vote.css"],
        ["QuestBoard.Service/Views/Quest/Details.Mobile.cshtml"] = ["quests.mobile.css", "quest-vote.css"],
        ["QuestBoard.Service/Views/Quest/Create.cshtml"] = ["quest-form.css"],
        ["QuestBoard.Service/Views/Quest/Create.Mobile.cshtml"] = ["quest-form.css", "dm-create.mobile.css"],
        ["QuestBoard.Service/Views/Quest/Edit.cshtml"] = ["quest-form.css"],
        ["QuestBoard.Service/Views/Quest/CreateFollowUp.cshtml"] = ["quest-form.css"],
        ["QuestBoard.Service/Views/Quest/Index.cshtml"] = ["quests.css"],
        ["QuestBoard.Service/Views/Quest/Index.Mobile.cshtml"] = ["home.mobile.css"],
        ["QuestBoard.Service/Views/Quest/Manage.cshtml"] = ["quest-manage.css"],
        ["QuestBoard.Service/Views/Quest/Manage.Mobile.cshtml"] = ["dm-manage.mobile.css"],
        ["QuestBoard.Service/Views/QuestLog/Index.cshtml"] = ["quest-log.css"],
        ["QuestBoard.Service/Views/QuestLog/Index.Mobile.cshtml"] = ["quest-log.mobile.css"],
        ["QuestBoard.Service/Views/QuestLog/Details.cshtml"] = ["quest-log.css"],
        ["QuestBoard.Service/Views/QuestLog/EditRecap.cshtml"] = ["quest-log.css"],
        ["QuestBoard.Service/Views/Move/Index.cshtml"] = ["move-entry.css"],
    };

    // Every source file this work touches, scanned for planning references. This test file is left
    // out because it has to name the shapes it forbids.
    private static readonly string[] PhaseSourceFiles =
    [
        "QuestBoard.Service/Views/Shared/_MarkdownEditor.cshtml",
        "QuestBoard.Service/Views/Quest/_QuestFormScripts.cshtml",
        "QuestBoard.Service/wwwroot/js/quest-form.js",
        "QuestBoard.Service/wwwroot/js/markdown-editor.js",
        "QuestBoard.Service/wwwroot/js/quest-details.js",
        "QuestBoard.Service/wwwroot/js/quest-manage.js",
        "QuestBoard.Service/wwwroot/js/avatar-fallback.js",
        "QuestBoard.Service/wwwroot/css/quest-details.css",
        "QuestBoard.Service/wwwroot/css/quest-vote.css",
        "QuestBoard.Service/wwwroot/css/quests.mobile.css",
        "QuestBoard.Service/wwwroot/css/quest-form.css",
        "QuestBoard.Service/wwwroot/css/dm-create.mobile.css",
        "QuestBoard.Service/wwwroot/css/home.mobile.css",
        "QuestBoard.Service/wwwroot/css/quest-manage.css",
        "QuestBoard.Service/wwwroot/css/dm-manage.mobile.css",
        "QuestBoard.Service/wwwroot/css/quest-log.css",
        "QuestBoard.Service/wwwroot/css/quest-log.mobile.css",
        "QuestBoard.Service/Views/QuestLog/Index.cshtml",
        "QuestBoard.Service/Views/QuestLog/Index.Mobile.cshtml",
        "QuestBoard.Service/Views/QuestLog/Details.cshtml",
        "QuestBoard.Service/Views/QuestLog/EditRecap.cshtml",
        "QuestBoard.Service/Views/Quest/Index.Mobile.cshtml",
        "QuestBoard.Service/Views/Move/Index.cshtml",
        "QuestBoard.Service/wwwroot/css/move-entry.css",
        "QuestBoard.Service/wwwroot/css/quests.css",
        "QuestBoard.Service/wwwroot/css/easymde-vendor.css",
        "QuestBoard.Service/wwwroot/css/markdown-editor.css",
        "QuestBoard.Service/wwwroot/css/markdown-content.css",
        "QuestBoard.Service/wwwroot/js/quest-board.js",
        "QuestBoard.Service/Helpers/QuestPosterCatalog.cs",
        "QuestBoard.Service/Helpers/QuestPosterPicker.cs",
    ];

    private static string ReadRepoFile(string relativePath)
    {
        var path = ResolveRepoFile(relativePath.Split('/'));
        File.Exists(path).Should().BeTrue(
            $"{relativePath} is listed in this guard, so it must exist; remove it from the list in the commit that deletes it");
        return File.ReadAllText(path);
    }

    private static string SheetPath(string name) => CssRoot + "/" + name;

    // ---- Scanners -------------------------------------------------------------------------------

    private static string StripRazorAndHtmlComments(string view)
    {
        var withoutRazor = Regex.Replace(view, @"@\*[\s\S]*?\*@", " ");
        return Regex.Replace(withoutRazor, @"<!--[\s\S]*?-->", " ");
    }

    private static string StripCssComments(string css) => Regex.Replace(css, @"/\*[\s\S]*?\*/", " ");

    private static string StripUrls(string css) =>
        Regex.Replace(css, @"url\(\s*(?:""[^""]*""|'[^']*'|[^)]*)\s*\)", "url()", RegexOptions.IgnoreCase);

    // Each rule is regex data so this file never holds the markup it forbids elsewhere.
    private static readonly (string Label, Regex Pattern)[] InlineCodePatterns =
    [
        ("a style attribute", new Regex(@"\sstyle\s*=", RegexOptions.IgnoreCase)),
        ("a style element", new Regex(@"<style\b", RegexOptions.IgnoreCase)),
        ("an inline script element", new Regex(@"<script\b(?![^>]*\bsrc\s*=)[^>]*>", RegexOptions.IgnoreCase)),
        ("an inline event handler", new Regex(@"\son[a-z]+\s*=", RegexOptions.IgnoreCase)),
    ];

    // A date-pattern format string handed to ToString, other than the machine value a date input
    // reads back, which has to stay in the pattern it is parsed with.
    private static readonly Regex DateFormatCall = new(
        @"\.ToString\(\s*""(?<format>[^""]*(?:yy|MM|dd|HH|hh|mm|ss|tt)[^""]*)""",
        RegexOptions.Compiled);

    private const string MachineValueFormat = "yyyy-MM-ddTHH:mm";

    private static List<string> RawDateFormats(string view) =>
        DateFormatCall.Matches(view)
            .Select(m => m.Groups["format"].Value)
            .Where(f => f != MachineValueFormat)
            .ToList();

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

    // Written as regex data, each forbidden shape built from fragments, so this file never holds
    // the shapes it forbids elsewhere.
    private static readonly (string Label, Regex Pattern)[] PlanningReferencePatterns =
    [
        ("a decision id", new Regex(@"\bD-(0[1-9]|1[0-9]|2[0-9])\b")),
        ("a question id", new Regex(@"\bQ" + @"-\d{2,3}\b")),
        ("a sign-off item id", new Regex(@"\bG" + @"1-[A-Z]\d\b")),
        ("an audit finding id", new Regex(@"\bA" + @"-(?:quests|characters)-\d{3}\b")),
        ("a merge or relayout id", new Regex(@"\bQ" + @"-(?:9[56]|11[1-4])\b")),
        ("a phase reference", new Regex("Pha" + "se\\s+9" + "5\\b", RegexOptions.IgnoreCase)),
        ("a plan id", new Regex(@"\b9" + @"5-(?:\d\d|[A-Z]{2,})")),
        ("a planning document name", new Regex(
            "CONT" + "EXT\\.md|RESE" + "ARCH\\.md|UI-" + "SPEC|PATT" + "ERNS\\.md|VALID" + "ATION\\.md|AUD" + "IT\\.md")),
    ];

    // ---- Guards ---------------------------------------------------------------------------------

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
    public void ConvertedViews_RenderNoRawDateFormatting()
    {
        foreach (var path in ConvertedViews)
            RawDateFormats(ReadRepoFile(path)).Should().BeEmpty(
                $"{path} must render dates through the date helpers, not a format string");
    }

    [Fact]
    public void QuestManageModule_WritesNoMarkupFromStrings()
    {
        // The module shows a player's name and a vote status, so it must only ever write text and elements.
        var script = ReadRepoFile("QuestBoard.Service/wwwroot/js/quest-manage.js");
        Regex.Matches(script, @"\.(?:innerHTML|outerHTML)\s*=|insertAdjacentHTML|document\.write")
            .Select(m => m.Value).Should().BeEmpty("quest-manage.js must build nodes with createElement and textContent");
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
    public void UnlayeredSheets_DoNotStyleTheManageDateOptions()
    {
        // The date options are styled by the layered Manage sheets; an unlayered rule would beat them on every property.
        foreach (var name in new[] { "legacy-pages.css", "quests.css" })
            Regex.Matches(StripCssComments(ReadRepoFile(SheetPath(name))), @"\.(?:date-option|manage-date-option|vote-summary)\b")
                .Select(m => m.Value).Should().BeEmpty(
                    $"{name} is unlayered and would override the layered Manage sheets");
    }

    [Fact]
    public void UnlayeredSheets_DoNotStyleTheQuestLog()
    {
        // The Quest Log is styled by the layered Quest Log sheets; an unlayered rule would beat them on every property.
        foreach (var name in new[] { "legacy-pages.css", "quests.css" })
            Regex.Matches(StripCssComments(ReadRepoFile(SheetPath(name))), @"\.(?:quest-log-[a-z-]+|empty-quest-log|recap-badge|view-details-text)\b")
                .Select(m => m.Value).Should().BeEmpty(
                    $"{name} is unlayered and would override the layered Quest Log sheets");
    }

    // The area as it was scoped: ten view pairs and five partials, and thirteen stylesheets. A file that a merge or a
    // deletion has since removed is not an error; one that still exists must be on the converted lists. A file left
    // unconverted on purpose goes in the exceptions list below with the reason, so that nothing is left out silently.
    private static readonly string[] ScopedViews =
    [
        .. new[] { "Index", "Details", "Create", "Edit", "CreateFollowUp", "Manage" }
            .SelectMany(v => new[] { $"QuestBoard.Service/Views/Quest/{v}.cshtml", $"QuestBoard.Service/Views/Quest/{v}.Mobile.cshtml" }),
        .. new[] { "Index", "Details", "EditRecap" }
            .SelectMany(v => new[] { $"QuestBoard.Service/Views/QuestLog/{v}.cshtml", $"QuestBoard.Service/Views/QuestLog/{v}.Mobile.cshtml" }),
        "QuestBoard.Service/Views/Move/Index.cshtml",
        "QuestBoard.Service/Views/Move/Index.Mobile.cshtml",
        "QuestBoard.Service/Views/Quest/_QuestFormScripts.cshtml",
        "QuestBoard.Service/Views/Quest/_QuestCard.cshtml",
        "QuestBoard.Service/Views/Quest/_QuestSection.cshtml",
        "QuestBoard.Service/Views/Shared/_MarkdownEditor.cshtml",
        "QuestBoard.Service/Views/Shared/_CharacterSelectModal.cshtml",
    ];

    private static readonly string[] ScopedSheets =
    [
        "quests.css", "quests.mobile.css", "quest-edit.mobile.css", "quest-followup.mobile.css", "quest-log.mobile.css",
        "quest-log-detail.mobile.css", "home.mobile.css", "move-entry.css", "move-entry.mobile.css", "markdown-editor.css",
        "markdown-content.css", "dm-create.mobile.css", "dm-manage.mobile.css",
    ];

    // Scoped files deliberately left unconverted, each with the reason. None at present.
    private static readonly Dictionary<string, string> UnconvertedByDecision = new();

    [Fact]
    public void PhaseScope_IsFullyConverted()
    {
        ScopedViews.Should().HaveCount(25, "the area has ten view pairs and five partials");
        ScopedSheets.Should().HaveCount(13, "the area has thirteen stylesheets");

        var views = ScopedViews.Where(v => File.Exists(ResolveRepoFile(v.Split('/'))))
            .Where(v => !UnconvertedByDecision.ContainsKey(v)).ToList();
        var sheets = ScopedSheets.Where(n => File.Exists(ResolveRepoFile(SheetPath(n).Split('/'))))
            .Where(n => !UnconvertedByDecision.ContainsKey(n)).ToList();

        views.Should().NotBeEmpty();
        sheets.Should().NotBeEmpty();
        views.Except(ConvertedViews).Should().BeEmpty(
            "every scoped view that still exists must be guarded as converted, or listed with the reason it is not");
        sheets.Except(ConvertedSheets).Should().BeEmpty(
            "every scoped sheet that still exists must be guarded as converted, or listed with the reason it is not");

        foreach (var removed in new[]
                 {
                     "QuestBoard.Service/Views/Quest/_QuestCard.cshtml",
                     "QuestBoard.Service/Views/Quest/_QuestSection.cshtml",
                 })
            File.Exists(ResolveRepoFile(removed.Split('/'))).Should().BeFalse(
                $"{removed} was an unreferenced partial and must not come back");

        UnconvertedByDecision.Keys.Should().OnlyContain(k => ScopedViews.Contains(k) || ScopedSheets.Contains(k));
    }

    [Fact]
    public void LegacySheet_HoldsNoDeadQuestRules_AndTheSharedContainerRuleIsNotImportant()
    {
        // The quest card, wax seal, custom radio vote and vote button rules matched no view once the board and
        // the vote moved to their layered sheets; an unlayered copy would only beat the layered ones again.
        var legacy = StripCssComments(ReadRepoFile(SheetPath("legacy-pages.css")));
        Regex.Matches(legacy, @"\.(?:quest-card|quest-status-[a-z]+|fantasy-quest-card|quest-title|quest-description|cr-badge|wax-seal|seal-image|custom-radio-[a-z]+|vote-btn)\b")
            .Select(m => m.Value).Should().BeEmpty("legacy-pages.css must not carry the retired quest board and vote rules");

        // The rule other areas still use: an unlayered rule already beats the vendor layer, so it needs no flag.
        var container = Regex.Match(legacy, @"\.container\.mt-4\s*\{([^}]*)\}");
        container.Success.Should().BeTrue("the shared wide-container rule is still used by the group picker layout and the DM profile pages");
        ImportantFlag.IsMatch(container.Groups[1].Value).Should().BeFalse("the container rule beats the vendor layer without an important flag");
    }

    [Theory]
    [InlineData("QuestBoard.Service/Views/QuestLog/Index.cshtml")]
    [InlineData("QuestBoard.Service/Views/QuestLog/Index.Mobile.cshtml")]
    public void QuestLogIndex_EachCardHasOneStretchedLinkAndNoClickHandler(string path)
    {
        var view = StripRazorAndHtmlComments(ReadRepoFile(path));
        Regex.Matches(view, "<a class=\"row-nav-link\" href=").Count.Should().Be(1,
            $"{path} writes the card once, so it holds exactly one link to the log entry");
        view.Should().NotContain("onclick");
    }

    [Theory]
    [InlineData("QuestBoard.Service/Views/QuestLog/Details.Mobile.cshtml")]
    [InlineData("QuestBoard.Service/Views/QuestLog/EditRecap.Mobile.cshtml")]
    [InlineData("QuestBoard.Service/wwwroot/css/quest-log-detail.mobile.css")]
    [InlineData("QuestBoard.Service/wwwroot/css/quest-edit.mobile.css")]
    public void QuestLogMergedViews_LeaveNoPhoneTwinOrPhoneSheet(string path)
    {
        // Details and EditRecap are one view each, served to both layouts; nothing may link or revive the retired files.
        File.Exists(ResolveRepoFile(path.Split('/'))).Should().BeFalse($"{path} was merged into the shared view");
    }

    [Theory]
    [InlineData("QuestBoard.Service/Views/Move/Index.Mobile.cshtml")]
    [InlineData("QuestBoard.Service/wwwroot/css/move-entry.mobile.css")]
    public void MoveMergedView_LeavesNoPhoneTwinOrPhoneSheet(string path)
    {
        // The Move page is one view served to both layouts; nothing may link or revive the retired files.
        File.Exists(ResolveRepoFile(path.Split('/'))).Should().BeFalse($"{path} was merged into the shared view");
    }

    [Fact]
    public void ConvertedViews_LinkTheirSheets()
    {
        foreach (var (path, sheets) in ViewSheetLinks)
        {
            var view = ReadRepoFile(path);
            foreach (var sheet in sheets)
                LinksSheet(view, sheet).Should().BeTrue(
                    $"{path} must link ~/css/{sheet} from its Styles section so the sheet loads only where it is used");
        }
    }

    [Fact]
    public void EditorPartial_LinksTheEditorSheets_AndNoLayoutDoes()
    {
        // The editor theme travels with the editor: every page that renders an editor renders this partial, so
        // the sheets load there and nowhere else.
        var partial = ReadRepoFile("QuestBoard.Service/Views/Quest/_QuestFormScripts.cshtml");
        foreach (var sheet in new[] { "easymde-vendor.css", "markdown-editor.css" })
            Regex.IsMatch(partial, @"<link\b[^>]*\bhref=""~/css/" + Regex.Escape(sheet) + @"""[^>]*>").Should().BeTrue(
                $"the editor partial must link ~/css/{sheet} so the editor sheet loads only where an editor is");

        foreach (var layout in new[] { "_Layout.cshtml", "_Layout.Mobile.cshtml" })
            ReadRepoFile("QuestBoard.Service/Views/Shared/" + layout).Should().NotContain("markdown-editor.css",
                $"{layout} must not load the editor sheet on pages that have no editor");
    }

    [Fact]
    public void MarkdownOutputViews_LinkTheContentSheet_AndNoLayoutDoes()
    {
        // The content sheet loads through each view that renders Markdown output, so a page that has none never
        // fetches it. A partial is satisfied by the views that host it: the shop item details partial is rendered
        // by the Shop details view and loaded into a modal by the two Shop index views.
        var viewsRoot = ResolveRepoFile("QuestBoard.Service", "Views");
        var hosts = Directory.EnumerateFiles(viewsRoot, "*.cshtml", SearchOption.AllDirectories)
            .Where(f => !Path.GetFileName(f).StartsWith('_'))
            .Where(f =>
            {
                var text = File.ReadAllText(f);
                return text.Contains("Html.Markdown(") || text.Contains("\"_ShopItemDetailsContent\"") || text.Contains("isModal = true");
            })
            .ToList();

        hosts.Count.Should().BeGreaterThanOrEqualTo(15, "the search must find every view that renders Markdown output");
        foreach (var view in hosts)
            LinksSheet(File.ReadAllText(view), "markdown-content.css").Should().BeTrue(
                $"{Path.GetRelativePath(viewsRoot, view)} renders Markdown output, so it must link ~/css/markdown-content.css from its Styles section");

        foreach (var layout in new[] { "_Layout.cshtml", "_Layout.Mobile.cshtml" })
            ReadRepoFile("QuestBoard.Service/Views/Shared/" + layout).Should().NotContain("markdown-content.css",
                $"{layout} must not load the content sheet on pages that render no Markdown output");
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

    // The text of the first block in a stylesheet, from its opening brace to the matching closing brace.
    private static string FirstBlock(string css)
    {
        var open = css.IndexOf('{');
        open.Should().BeGreaterThanOrEqualTo(0, "the sheet must open a block");

        var depth = 0;
        for (var i = open; i < css.Length; i++)
        {
            if (css[i] == '{')
                depth++;
            else if (css[i] == '}' && --depth == 0)
                return css[open..(i + 1)];
        }

        return css[open..];
    }

    [Fact]
    public void PosterCatalog_AndPosterSheet_AgreeOnKeys()
    {
        var sheet = StripCssComments(ReadRepoFile(SheetPath("quests.css")));
        OpensPagesLayer(sheet).Should().BeTrue("the poster rules live in the pages layer at the top of quests.css");

        var layered = FirstBlock(sheet);
        var ruleKeys = Regex.Matches(layered, @"\.quest-poster--(?<key>[a-z0-9]+)\s*\{")
            .Select(m => m.Groups["key"].Value)
            .ToList();

        ruleKeys.Should().BeEquivalentTo(
            QuestPosterCatalog.All.Select(p => p.Key),
            "every catalog poster needs its modifier rule and every modifier rule needs a catalog poster");
    }

    [Fact]
    public void Scanners_FlagWhatTheyForbidAndAllowWhatTheyPermit()
    {
        // Without a known-bad sample the guards above would pass on an empty list whether or not a
        // scanner worked, so each scanner is shown to fire here and to stay quiet on a clean sample.
        var bad = StripRazorAndHtmlComments(
            "<div sty" + "le=\"a:b\"><sty" + "le>.a{}</sty" + "le><scr" + "ipt>x()</scr" + "ipt>" +
            "<button on" + "click=\"go()\">x</button></div> @* <scr" + "ipt>hidden</scr" + "ipt> *@");
        InlineCodePatterns.Select(p => p.Pattern.Matches(bad).Count).Should().Equal(1, 1, 1, 1);

        var clean = StripRazorAndHtmlComments(
            "<div class=\"a\"><scr" + "ipt src=\"~/js/a.js\"></scr" + "ipt><input data-on-hold=\"x\" /></div>");
        InlineCodePatterns.Sum(p => p.Pattern.Matches(clean).Count).Should().Be(0);

        RawDateFormats("@Model.Date.ToString(\"MMM d, yyyy\")").Should().HaveCount(1);
        RawDateFormats("@Model.Date.ToString(\"" + MachineValueFormat + "\")").Should().BeEmpty();
        RawDateFormats("@Model.Count.ToString(\"N0\")").Should().BeEmpty();

        OpensPagesLayer(StripCssComments("/* note */ @layer pages { .a { top: 0 } }")).Should().BeTrue();
        OpensPagesLayer(".a { top: 0 }").Should().BeFalse();

        ImportantFlag.Matches(".a { top: 0 ! important; }").Should().HaveCount(1);
        ImportantFlag.Matches(".a { top: 0; }").Should().BeEmpty();

        var sample = StripUrls(StripCssComments(
            ".a { color: #abc; background: rgba(0,0,0,.5); border-color: hsl(1,2%,3%); " +
            "background-image: url(\"x#fff.png\"); } /* #123456 */ #main-content { top: 0 }"));
        ColourLiteralPatterns.Select(p => p.Pattern.Matches(sample).Count).Should().Equal(1, 1, 1);

        var withLink = "@section Styles {\n    <link rel=\"stylesheet\" href=\"~/css/a.css\" />\n}\n<link href=\"~/css/b.css\" />";
        LinksSheet(withLink, "a.css").Should().BeTrue();
        LinksSheet(withLink, "b.css").Should().BeFalse("a link outside the Styles section does not count");

        var planning = "// " + "D-" + "06, " + "Q" + "-39, " + "Pha" + "se 9" + "5, 9" + "5-04 and " + "A" + "-quests-001";
        PlanningReferencePatterns.Count(p => p.Pattern.IsMatch(planning)).Should().BeGreaterThanOrEqualTo(5);
        PlanningReferencePatterns.Count(p => p.Pattern.IsMatch("// keyboard users must be able to leave the field"))
            .Should().Be(0);
    }
}
