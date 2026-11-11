using System.Text.RegularExpressions;
using FluentAssertions;
using QuestBoard.Domain.Extensions;

namespace QuestBoard.IntegrationTests.Tests;

/// <summary>
/// Pins the board delete confirmation page and the files that came with it. The views carry no inline
/// code and render no unencoded HTML; each layout loads its own sheet and the typed-name gate module;
/// the new sheets open the pages layer, name colours by token and the phone sheets use no media query;
/// the gate module writes no markup from strings and re-syncs on a back-forward restore; and none of
/// the files names the planning work that produced them. A rendered-markup test cannot see which sheet
/// a rule came from or what a comment says, so only a scan of the sources can hold the line.
/// </summary>
public class BoardDeletionStaticGuardTests
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

    private const string ViewsRoot = "QuestBoard.Service/Areas/Platform/Views/Group";
    private const string CssRoot = "QuestBoard.Service/wwwroot/css";
    private const string GateModule = "QuestBoard.Service/wwwroot/js/platform-board-delete.js";

    private const string DesktopTwin = ViewsRoot + "/Delete.cshtml";
    private const string PhoneTwin = ViewsRoot + "/Delete.Mobile.cshtml";
    private const string BodyPartial = ViewsRoot + "/_BoardDeleteBody.cshtml";

    private static readonly string[] DeleteViews = [DesktopTwin, PhoneTwin, BodyPartial];

    private static readonly string[] NewSheets =
    [
        CssRoot + "/platform-group-delete.css",
        CssRoot + "/platform-group-delete.mobile.css",
        CssRoot + "/platform-users-boards.mobile.css",
    ];

    private static readonly string[] PhoneSheets =
    [
        CssRoot + "/platform-group-delete.mobile.css",
        CssRoot + "/platform-users-boards.mobile.css",
    ];

    // Every file the board deletion work added on the C# side, scanned for planning references.
    private static readonly string[] NewCSharpFiles =
    [
        "QuestBoard.Service/ViewModels/PlatformViewModels/BoardDeletePageViewModel.cs",
        "QuestBoard.Service/ViewModels/PlatformViewModels/BoardDeleteRowViewModel.cs",
        "QuestBoard.Service/Extensions/ActiveBoardSessionExtensions.cs",
        "QuestBoard.Domain/Enums/BoardDeleteStatus.cs",
        "QuestBoard.Domain/Enums/BoardContentKind.cs",
        "QuestBoard.Domain/Extensions/BoardNameText.cs",
    ];

    private static string ReadRepoFile(string relativePath)
    {
        var path = ResolveRepoFile(relativePath.Split('/'));
        File.Exists(path).Should().BeTrue(
            $"{relativePath} is listed in this guard, so it must exist; remove it from the list in the commit that deletes it");
        return File.ReadAllText(path);
    }

    private static IEnumerable<string> DomainDeletionFiles()
    {
        var folder = ResolveRepoFile("QuestBoard.Domain", "Models", "BoardDeletion");
        Directory.Exists(folder).Should().BeTrue("the board deletion domain models live in their own folder");
        return Directory.EnumerateFiles(folder, "*.cs", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(FindRepositoryRoot().FullName, f).Replace('\\', '/'))
            .OrderBy(f => f, StringComparer.Ordinal);
    }

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

    // A name is user-authored text, so a view must let Razor encode it.
    private static readonly Regex UnencodedHtmlPattern = new(
        @"Html\.Raw\s*\(|new\s+HtmlString\b|\bIHtmlContent\b", RegexOptions.Compiled);

    private static bool OpensPagesLayer(string strippedCss) =>
        strippedCss.TrimStart().StartsWith("@layer pages {", StringComparison.Ordinal);

    private static readonly Regex ImportantFlag = new(@"!\s*important", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex MediaQuery = new(@"@media\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Written as regex data so this file never holds a literal it forbids elsewhere: a hex colour
    // of 3, 4, 6 or 8 digits (not an id selector with a longer name), and the colour functions.
    private static readonly (string Label, Regex Pattern)[] ColourLiteralPatterns =
    [
        ("a hex colour", new Regex(
            @"#(?:[0-9a-fA-F]{8}|[0-9a-fA-F]{6}|[0-9a-fA-F]{4}|[0-9a-fA-F]{3})(?![\w-])", RegexOptions.Compiled)),
        ("an rgb() colour", new Regex(@"\brgba?\(", RegexOptions.Compiled | RegexOptions.IgnoreCase)),
        ("an hsl() colour", new Regex(@"\bhsla?\(", RegexOptions.Compiled | RegexOptions.IgnoreCase)),
    ];

    // The sinks that turn a string into markup, none of which the gate module may call.
    private static readonly Regex MarkupSinkPattern = new(
        @"\.(?:innerHTML|outerHTML)\s*=|insertAdjacentHTML|document\.write|createContextualFragment|DOMParser",
        RegexOptions.Compiled);

    // Whether the view loads the gate module as a module script.
    private static readonly Regex ModuleScriptPattern = new(
        @"<script\b(?=[^>]*\btype=""module"")[^>]*\bsrc=""~/js/platform-board-delete\.js""[^>]*>", RegexOptions.Compiled);

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
        ("a decision id", new Regex(@"\bD" + @"-(?:0[1-9]|1[0-9]|2[0-9])\b")),
        ("a phase reference", new Regex("Pha" + "se\\s+10" + "2\\b", RegexOptions.IgnoreCase)),
        ("a plan id", new Regex(@"\b10" + @"2-(?:0\d|[A-Z]{2,})")),
        ("a threat id", new Regex(@"\bT" + @"-1" + @"02-\d+")),
        ("a planning document name", new Regex(
            "CONT" + "EXT\\.md|RESE" + "ARCH\\.md|UI-" + "SPEC|PATT" + "ERNS\\.md|VALID" + "ATION\\.md|SP" + "EC\\.md|PL" + "AN\\.md")),
    ];

    // ---- Guards ---------------------------------------------------------------------------------

    [Fact]
    public void DeleteViews_CarryNoInlineStyleScriptOrHandler()
    {
        foreach (var path in DeleteViews)
        {
            var view = StripRazorAndHtmlComments(ReadRepoFile(path));
            foreach (var (label, pattern) in InlineCodePatterns)
                pattern.Matches(view).Select(m => m.Value).Should().BeEmpty(
                    $"{path} must carry no inline code; {label} belongs in a stylesheet or a script file");
        }
    }

    [Fact]
    public void DeleteViews_NeverRenderUnencodedHtml()
    {
        // Board, quest, event and contact names are typed by people, so every one goes through Razor encoding.
        foreach (var path in DeleteViews)
            UnencodedHtmlPattern.Matches(StripRazorAndHtmlComments(ReadRepoFile(path))).Select(m => m.Value).Should().BeEmpty(
                $"{path} must let Razor encode every name it shows");
    }

    [Fact]
    public void DeleteTwins_LinkTheirOwnSheetAndTheGateModule()
    {
        var desktop = ReadRepoFile(DesktopTwin);
        LinksSheet(desktop, "platform-group-delete.css").Should().BeTrue("the desktop twin loads its own sheet");
        LinksSheet(desktop, "platform-group-delete.mobile.css").Should().BeFalse("the desktop twin must not load the phone sheet");

        var phone = ReadRepoFile(PhoneTwin);
        LinksSheet(phone, "platform-group-delete.mobile.css").Should().BeTrue("the phone twin loads its own sheet");
        LinksSheet(phone, "platform-group-delete.css").Should().BeFalse("the phone twin does not need the desktop sheet");
        LinksSheet(phone, "platform-group.mobile.css").Should().BeFalse("the phone twin must not load the legacy group phone sheet");

        foreach (var (path, view) in new[] { (DesktopTwin, desktop), (PhoneTwin, phone) })
            ModuleScriptPattern.IsMatch(StripRazorAndHtmlComments(view)).Should().BeTrue(
                $"{path} must load the typed-name gate module as a module script");
    }

    [Fact]
    public void NewSheets_OpenThePagesLayer()
    {
        foreach (var path in NewSheets)
            OpensPagesLayer(StripCssComments(ReadRepoFile(path))).Should().BeTrue(
                $"{path} must open the pages layer so the foundation's cascade order applies to it");
    }

    [Fact]
    public void NewSheets_HaveNoPriorityFlagOrColourLiteral()
    {
        foreach (var path in NewSheets)
        {
            var css = StripUrls(StripCssComments(ReadRepoFile(path)));
            ImportantFlag.Matches(css).Should().BeEmpty($"{path} sits in a layer, where a priority flag is never needed");
            foreach (var (label, pattern) in ColourLiteralPatterns)
                pattern.Matches(css).Select(m => m.Value).Should().BeEmpty(
                    $"{path} must name colours by token; {label} belongs in the token file");
        }
    }

    [Fact]
    public void PhoneSheets_HaveNoMediaQuery()
    {
        // The phone layout is chosen by the device, so a viewport query in a phone sheet would never be the right test.
        foreach (var path in PhoneSheets)
            MediaQuery.Matches(StripCssComments(ReadRepoFile(path))).Should().BeEmpty(
                $"{path} is a phone sheet and carries no media query");
    }

    [Fact]
    public void GateModule_WritesNoMarkupFromStrings()
    {
        // The module shows a status line next to an admin-authored board name, so it only writes elements and text.
        var script = StripJsComments(ReadRepoFile(GateModule));
        MarkupSinkPattern.Matches(script).Select(m => m.Value).Should().BeEmpty(
            "the gate module must build nodes with createElement and text nodes");
    }

    [Fact]
    public void GateModule_ResyncsOnPageshow()
    {
        // site.js unlocks submit buttons on a back-forward restore; the module registers after it and must win.
        var script = StripJsComments(ReadRepoFile(GateModule));
        Regex.IsMatch(script, @"addEventListener\(\s*'pageshow'").Should().BeTrue(
            "the gate module must re-sync the button when the page is restored from the back-forward cache");
    }

    [Fact]
    public void GateModule_TrimsExactlyTheEdgeWhitespaceTheServerTrims()
    {
        // The button and the server must agree on what counts as edge whitespace, or a name could unlock the
        // button and then be refused. String.prototype.trim() strips a different set from .NET's Trim(), so
        // the module carries the server's set as escapes, character for character, and never calls trim().
        var script = StripJsComments(ReadRepoFile(GateModule));
        var serverSet = string.Concat(BoardNameText.EdgeWhitespace.Select(c => $"\\u{(int)c:X4}"));

        script.Should().Contain($"'{serverSet}'", "the gate module must carry the server's edge whitespace set");
        script.Should().NotContain(".trim(", "trim() strips a different set of characters from the server's");
        script.Should().Contain("trimEdges(input.value) === trimEdges(input.dataset.boardDeleteName)",
            "both the typed text and the board name lose their edge whitespace before they are compared");
    }

    [Fact]
    public void NewFiles_CarryNoPlanningReferences()
    {
        var files = DeleteViews
            .Concat(NewSheets)
            .Append(GateModule)
            .Concat(NewCSharpFiles)
            .Concat(DomainDeletionFiles())
            .Distinct()
            .ToList();

        files.Count.Should().BeGreaterThanOrEqualTo(15, "the scan must cover the views, sheets, module and C# files");

        foreach (var path in files)
        {
            var text = ReadRepoFile(path);
            foreach (var (label, pattern) in PlanningReferencePatterns)
                pattern.Matches(text).Select(m => m.Value).Should().BeEmpty(
                    $"{path} must not carry {label}: planning references go stale when the work closes");
        }
    }

    private static string StripJsComments(string script) =>
        Regex.Replace(Regex.Replace(script, @"/\*[\s\S]*?\*/", " "), @"(?m)^\s*//.*$", " ");

    [Fact]
    public void Scanners_FireOnKnownBadSamples()
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

        UnencodedHtmlPattern.IsMatch("@Html.Ra" + "w(Model.Name)").Should().BeTrue();
        UnencodedHtmlPattern.IsMatch("@Model.Name").Should().BeFalse();

        OpensPagesLayer(StripCssComments("/* note */ @layer pages { .a { top: 0 } }")).Should().BeTrue();
        OpensPagesLayer(".a { top: 0 }").Should().BeFalse();

        ImportantFlag.Matches(".a { top: 0 ! important; }").Should().HaveCount(1);
        ImportantFlag.Matches(".a { top: 0; }").Should().BeEmpty();

        var sample = StripUrls(StripCssComments(
            ".a { color: #abc; background: rgba(0,0,0,.5); border-color: hsl(1,2%,3%); " +
            "background-image: url(\"x#fff.png\"); } /* #123456 */ #main-content { top: 0 }"));
        ColourLiteralPatterns.Select(p => p.Pattern.Matches(sample).Count).Should().Equal(1, 1, 1);

        MediaQuery.Matches("@med" + "ia (max-width: 10px) { .a { top: 0 } }").Should().HaveCount(1);
        MediaQuery.Matches(StripCssComments("/* no @med" + "ia here */ .a { top: 0 }")).Should().BeEmpty();

        MarkupSinkPattern.Matches("el.inner" + "HTML = x; el.insertAdjacent" + "HTML('a', b); document.wri" + "te(c);")
            .Should().HaveCount(3);
        MarkupSinkPattern.Matches("el.textContent = x; el.replaceChildren(icon);").Should().BeEmpty();

        var withLink = "@section Styles {\n    <link rel=\"stylesheet\" href=\"~/css/a.css\" />\n}\n<link href=\"~/css/b.css\" />";
        LinksSheet(withLink, "a.css").Should().BeTrue();
        LinksSheet(withLink, "b.css").Should().BeFalse("a link outside the Styles section does not count");

        ModuleScriptPattern.IsMatch("<scr" + "ipt type=\"module\" src=\"~/js/platform-board-delete.js\"></scr" + "ipt>").Should().BeTrue();
        ModuleScriptPattern.IsMatch("<scr" + "ipt src=\"~/js/platform-board-delete.js\"></scr" + "ipt>").Should().BeFalse();

        var planning = "// " + "D-" + "08, " + "Pha" + "se 10" + "2, 10" + "2-07, T-1" + "02-22 and " + "UI-" + "SPEC";
        PlanningReferencePatterns.Count(p => p.Pattern.IsMatch(planning)).Should().Be(PlanningReferencePatterns.Length);
        PlanningReferencePatterns.Count(p => p.Pattern.IsMatch("// the button unlocks when the typed name matches"))
            .Should().Be(0);
    }
}
