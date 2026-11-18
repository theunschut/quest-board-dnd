using System.Text.RegularExpressions;
using FluentAssertions;

namespace QuestBoard.IntegrationTests.Tests;

/// <summary>
/// Pins the Admin and Platform pages as they are moved onto the layered foundation, and the server rules that sit
/// behind them. The list below holds every source and test file this work creates or edits, and none of those files
/// may name the planning that produced it: a comment that cites a requirement or a plan goes stale the moment that
/// work closes. The list only ever grows as each file is touched; a file a later change deletes is removed from it
/// in the same commit. An integration suite reading rendered markup cannot see what a comment says, so only a scan of
/// the sources can hold the line.
/// </summary>
public class AdminPlatformConversionGuardTests
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

    // Every repository source or test file the Admin and Platform work creates or edits. Append-only.
    private static readonly string[] PhaseSourceFiles =
    [
        "QuestBoard.IntegrationTests/Controllers/AdminAccountReachTests.cs",
        "QuestBoard.IntegrationTests/Controllers/AdminRoleGuardTests.cs",
        "QuestBoard.IntegrationTests/Controllers/AdminQuestDeleteTests.cs",
        "QuestBoard.IntegrationTests/Controllers/AdminControllerRateLimitIntegrationTests.cs",
        "QuestBoard.IntegrationTests/Controllers/GroupManagementIntegrationTests.cs",
        "QuestBoard.IntegrationTests/Tests/AdminPlatformConversionGuardTests.cs",
        "QuestBoard.Domain/Enums/AccountReach.cs",
        "QuestBoard.Domain/Interfaces/IUserService.cs",
        "QuestBoard.Domain/Services/UserService.cs",
        "QuestBoard.Domain/Interfaces/IUserRepository.cs",
        "QuestBoard.Repository/UserRepository.cs",
        "QuestBoard.Service/Controllers/Admin/AdminController.cs",
        "QuestBoard.Service/ViewModels/AdminViewModels/AdminActionMessages.cs",
        "QuestBoard.UnitTests/Services/UserServiceTests.cs",
        "QuestBoard.Domain/Interfaces/IIdentityService.cs",
        "QuestBoard.Repository/IdentityService.cs",
        "QuestBoard.Service/Program.cs",
        "QuestBoard.Domain/Models/EmailSettings.cs",
        "QuestBoard.Service/Services/ResendBaseAddress.cs",
        "QuestBoard.Service/Services/AdminRateLimitKeys.cs",
        "QuestBoard.IntegrationTests/Helpers/CannedResendHandler.cs",
        "QuestBoard.IntegrationTests/Helpers/EmailStatsWebApplicationFactory.cs",
        "QuestBoard.IntegrationTests/Tests/EmailStatsRenderTests.cs",
        "QuestBoard.Repository/QuestRepository.cs",
        "QuestBoard.IntegrationTests/Tests/QuestDeleteSqlServerTests.cs",
        "QuestBoard.Domain/Enums/MemberChangeOutcome.cs",
        "QuestBoard.Domain/Interfaces/IGroupService.cs",
        "QuestBoard.Domain/Services/GroupService.cs",
        "QuestBoard.Domain/Interfaces/IGroupRepository.cs",
        "QuestBoard.Repository/GroupRepository.cs",
        "QuestBoard.UnitTests/Services/GroupServiceTests.cs",
        "QuestBoard.Service/Areas/Platform/Controllers/GroupController.cs",
        "QuestBoard.IntegrationTests/Controllers/AdminControllerIntegrationTests.cs",
        "QuestBoard.Service/ViewModels/AdminViewModels/AdminPlatformDisplay.cs",
        "QuestBoard.Service/ViewModels/AdminViewModels/QuestStatusBadgeModel.cs",
        "QuestBoard.Service/Views/Shared/_QuestStatusBadge.cshtml",
        "QuestBoard.Service/wwwroot/css/components.css",
        "QuestBoard.Service/wwwroot/css/admin-pages.css",
        "QuestBoard.Service/wwwroot/css/admin-pages.mobile.css",
        "QuestBoard.Service/Views/Admin/Quests.cshtml",
        "QuestBoard.Service/Views/Admin/Quests.Mobile.cshtml",
        "QuestBoard.IntegrationTests/Tests/AdminPlatformRenderTests.cs",
        "QuestBoard.IntegrationTests/Tests/UiFoundationLayerGuardTests.cs",
        "QuestBoard.IntegrationTests/Mobile/MobileViewsTests.cs",
        "QuestBoard.IntegrationTests/Controllers/QuestStatusBoardClockTests.cs",
        "QuestBoard.IntegrationTests/Tests/AdminPlatformDisplayTests.cs",
        "QuestBoard.Service/ViewModels/AdminViewModels/UserManagementViewModel.cs",
        "QuestBoard.Service/Views/Admin/Users.cshtml",
        "QuestBoard.Service/Views/Admin/Users.Mobile.cshtml",
        "QuestBoard.Service/Views/Shared/_RoleBadge.cshtml",
        "QuestBoard.Service/Views/Admin/EditUser.cshtml",
        "QuestBoard.Service/Views/Admin/EditUser.Mobile.cshtml",
        "QuestBoard.Service/Views/Admin/CreateUser.cshtml",
        "QuestBoard.Service/Views/Admin/CreateUser.Mobile.cshtml",
        "QuestBoard.Service/Views/Admin/ResetPassword.cshtml",
        "QuestBoard.Service/Views/Admin/ResetPassword.Mobile.cshtml",
        "QuestBoard.Service/ViewModels/AdminViewModels/ResetPasswordViewModel.cs",
        "QuestBoard.Service/Views/Shared/_BoardTypeBadge.cshtml",
        "QuestBoard.Service/Areas/Platform/Views/Group/Index.cshtml",
        "QuestBoard.Service/Areas/Platform/Views/Group/Index.Mobile.cshtml",
        "QuestBoard.Service/Areas/Platform/Views/Group/Delete.cshtml",
        "QuestBoard.Service/Areas/Platform/Views/Group/Delete.Mobile.cshtml",
        "QuestBoard.Service/Areas/Platform/Views/Group/_BoardDeleteBody.cshtml",
        "QuestBoard.Service/Areas/Platform/Views/Group/Create.cshtml",
        "QuestBoard.Service/Areas/Platform/Views/Group/Create.Mobile.cshtml",
        "QuestBoard.Service/Areas/Platform/Views/Group/Edit.cshtml",
        "QuestBoard.Service/Areas/Platform/Views/Group/Edit.Mobile.cshtml",
        "QuestBoard.Service/wwwroot/css/platform-pages.css",
        "QuestBoard.Service/wwwroot/css/platform-pages.mobile.css",
        "QuestBoard.IntegrationTests/Controllers/BoardPageBoardListTests.cs",
        "QuestBoard.IntegrationTests/Tests/BoardPageEndToEndTests.cs",
        "QuestBoard.IntegrationTests/Controllers/LinkedBoardsPlatformIntegrationTests.cs",
        "QuestBoard.Service/Areas/Platform/Views/LinkedBoards/Index.cshtml",
        "QuestBoard.Service/Areas/Platform/Views/LinkedBoards/Index.Mobile.cshtml",
        "QuestBoard.IntegrationTests/Controllers/UsersControllerIntegrationTests.cs",
        "QuestBoard.Service/Areas/Platform/Views/Users/Index.cshtml",
        "QuestBoard.Service/Areas/Platform/Views/Users/Index.Mobile.cshtml",
        "QuestBoard.Service/Areas/Platform/Views/Group/Members.cshtml",
        "QuestBoard.Service/Areas/Platform/Views/Group/Members.Mobile.cshtml",
        "QuestBoard.IntegrationTests/Tests/BoardPageStaticGuardTests.cs",
        "QuestBoard.Service/Views/Admin/EmailStats.cshtml",
        "QuestBoard.IntegrationTests/Controllers/BoardPageGateMatrixTests.cs",
        "QuestBoard.IntegrationTests/Tests/LinkedBoardStaticGuardTests.cs",
    ];

    // Written as regex data, each forbidden shape built from fragments, so this file never holds the shapes it
    // forbids elsewhere.
    private static readonly (string Label, Regex Pattern)[] PlanningReferencePatterns =
    [
        ("a decision id", new Regex(@"\bD" + @"-(?:0[1-9]|[1-9][0-9])\b")),
        ("a question id", new Regex(@"\b(?:O)?Q" + @"-\d{1,3}\b")),
        ("an operator question id", new Regex(@"\bO" + @"-(?:[1-9]|1[0-9])\b")),
        ("a gap id", new Regex(@"\bG" + @"-\d{2}\b")),
        ("a merge or relayout id", new Regex(@"\bR" + @"-[1-9]\b")),
        ("a definition-of-converted id", new Regex(@"\bDo" + @"C-\d{1,2}\b")),
        ("an audit finding id", new Regex(@"\bA" + @"-(?:admin|shell|shop)-\d{3}\b")),
        ("a threat id", new Regex(@"\bT" + @"-\d{2,3}-(?:\d{2}|SC)\b")),
        ("a ledger number", new Regex("WIND" + "OWS\\s+(?:entry\\s+)?#?\\d+", RegexOptions.IgnoreCase)),
        ("a phase reference", new Regex("Pha" + "se\\s+(?:9[4-9]|10[0-9])\\b", RegexOptions.IgnoreCase)),
        ("a plan id", new Regex(@"\b(?:9[4-9]|10[0-9])" + @"-(?:\d\d|[A-Z]{2,})")),
        ("a planning document name", new Regex(
            "CONT" + "EXT\\.md|RESE" + "ARCH\\.md|UI-" + "SPEC|PATT" + "ERNS\\.md|VALID" + "ATION\\.md|AUD" + "IT\\.md|SECU" + "RITY\\.md|deferred-" + "items")),
    ];

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
        // the scanner is shown to fire here and to stay quiet on a clean sample.
        var planning = "// see " + "D" + "-04 and " + "Q" + "-51, " + "O" + "-3, " + "G" + "-01, " + "Pha" + "se 100" + ", 100" + "-05 in "
            + "RESE" + "ARCH.md, " + "R" + "-2, " + "Do" + "C-8, " + "A" + "-admin-012, " + "T" + "-1" + "00-13, " + "WIND" + "OWS #31";
        PlanningReferencePatterns.Count(p => p.Pattern.IsMatch(planning)).Should().Be(PlanningReferencePatterns.Length);
        PlanningReferencePatterns.Count(p => p.Pattern.IsMatch("// the board's Admin may reset a Player's password"))
            .Should().Be(0);
        PlanningReferencePatterns.Count(p => p.Pattern.IsMatch("// kept in " + "SECU" + "RITY.md and " + "deferred-" + "items"))
            .Should().Be(1);
    }

    [Fact]
    public void PhaseSourceFiles_AreAllListedOnDisk()
    {
        PhaseSourceFiles.Should().OnlyHaveUniqueItems();
        foreach (var path in PhaseSourceFiles)
            File.Exists(ResolveRepoFile(path.Split('/'))).Should().BeTrue($"{path} is listed, so it must exist");
    }

    // The Resend client's address is decided in one place, by the helper that honours a configured address only on a
    // Development host. The fixed address is written once, in the registration, and the setting is read nowhere else.
    [Fact]
    public void ResendClient_BaseAddressIsDecidedOnlyByTheDevelopmentGatedHelper()
    {
        var program = ReadRepoFile("QuestBoard.Service/Program.cs");

        Regex.Matches(program, Regex.Escape("https://api.resend.com/")).Count.Should().Be(1);
        Regex.Matches(program, @"\.ResendBaseUrl\b").Count.Should().Be(1);
        program.Should().MatchRegex(@"ResendBaseAddress\.Resolve\([^;]*ResendBaseUrl[^;]*https://api\.resend\.com/");

        var helper = ReadRepoFile("QuestBoard.Service/Services/ResendBaseAddress.cs");
        helper.Should().Contain("IsDevelopment()", "the configured address is honoured only on a Development host");
    }

    // ---- The area's views, sheets and modules ------------------------------------------------------

    // The pages and partials of the Admin and Platform area that are converted: no inline code, their sheets linked from
    // a Styles section, one page title each. Grows as each slice converts its views.
    private static readonly string[] ConvertedViews =
    [
        "QuestBoard.Service/Views/Admin/Quests.cshtml",
        "QuestBoard.Service/Views/Admin/Quests.Mobile.cshtml",
        "QuestBoard.Service/Views/Shared/_QuestStatusBadge.cshtml",
        "QuestBoard.Service/Views/Admin/Users.cshtml",
        "QuestBoard.Service/Views/Admin/Users.Mobile.cshtml",
        "QuestBoard.Service/Views/Shared/_RoleBadge.cshtml",
        "QuestBoard.Service/Views/Admin/EditUser.cshtml",
        "QuestBoard.Service/Views/Admin/EditUser.Mobile.cshtml",
        "QuestBoard.Service/Views/Admin/CreateUser.cshtml",
        "QuestBoard.Service/Views/Admin/CreateUser.Mobile.cshtml",
        "QuestBoard.Service/Views/Admin/ResetPassword.cshtml",
        "QuestBoard.Service/Views/Admin/ResetPassword.Mobile.cshtml",
        "QuestBoard.Service/Views/Shared/_BoardTypeBadge.cshtml",
        "QuestBoard.Service/Areas/Platform/Views/Group/Index.cshtml",
        "QuestBoard.Service/Areas/Platform/Views/Group/Index.Mobile.cshtml",
        "QuestBoard.Service/Areas/Platform/Views/Group/Delete.cshtml",
        "QuestBoard.Service/Areas/Platform/Views/Group/Delete.Mobile.cshtml",
        "QuestBoard.Service/Areas/Platform/Views/Group/_BoardDeleteBody.cshtml",
        "QuestBoard.Service/Areas/Platform/Views/Group/Create.cshtml",
        "QuestBoard.Service/Areas/Platform/Views/Group/Create.Mobile.cshtml",
        "QuestBoard.Service/Areas/Platform/Views/Group/Edit.cshtml",
        "QuestBoard.Service/Areas/Platform/Views/Group/Edit.Mobile.cshtml",
        "QuestBoard.Service/Areas/Platform/Views/LinkedBoards/Index.cshtml",
        "QuestBoard.Service/Areas/Platform/Views/LinkedBoards/Index.Mobile.cshtml",
        "QuestBoard.Service/Areas/Platform/Views/Users/Index.cshtml",
        "QuestBoard.Service/Areas/Platform/Views/Users/Index.Mobile.cshtml",
        "QuestBoard.Service/Areas/Platform/Views/Group/Members.cshtml",
        "QuestBoard.Service/Areas/Platform/Views/Group/Members.Mobile.cshtml",
        "QuestBoard.Service/Views/Admin/EmailStats.cshtml",
    ];

    // Every other view of the area, still waiting for its slice. A slice moves its views from here to the converted list
    // in the commit that converts them.
    private static readonly string[] PendingViews =
    [
    ];

    // For each converted view, the sheets it must link from its Styles section, by file name. A partial links nothing:
    // the pages that render it link what it needs.
    private static readonly Dictionary<string, string[]> ViewSheetLinks = new()
    {
        ["QuestBoard.Service/Views/Admin/Quests.cshtml"] = ["admin-pages.css"],
        ["QuestBoard.Service/Views/Admin/Quests.Mobile.cshtml"] = ["admin-pages.css", "admin-pages.mobile.css"],
        ["QuestBoard.Service/Views/Admin/EmailStats.cshtml"] = ["admin-pages.css"],
        ["QuestBoard.Service/Views/Admin/Users.cshtml"] = ["admin-pages.css"],
        ["QuestBoard.Service/Views/Admin/Users.Mobile.cshtml"] = ["admin-pages.css", "admin-pages.mobile.css"],
        ["QuestBoard.Service/Views/Admin/EditUser.cshtml"] = ["admin-pages.css"],
        ["QuestBoard.Service/Views/Admin/EditUser.Mobile.cshtml"] = ["admin-pages.css", "admin-pages.mobile.css"],
        ["QuestBoard.Service/Views/Admin/CreateUser.cshtml"] = ["admin-pages.css"],
        ["QuestBoard.Service/Views/Admin/CreateUser.Mobile.cshtml"] = ["admin-pages.css", "admin-pages.mobile.css"],
        ["QuestBoard.Service/Views/Admin/ResetPassword.cshtml"] = ["admin-pages.css"],
        ["QuestBoard.Service/Views/Admin/ResetPassword.Mobile.cshtml"] = ["admin-pages.css", "admin-pages.mobile.css"],
        ["QuestBoard.Service/Areas/Platform/Views/Group/Index.cshtml"] = ["platform-pages.css"],
        ["QuestBoard.Service/Areas/Platform/Views/Group/Index.Mobile.cshtml"] = ["platform-pages.css", "platform-pages.mobile.css"],
        ["QuestBoard.Service/Areas/Platform/Views/Group/Create.cshtml"] = ["platform-pages.css"],
        ["QuestBoard.Service/Areas/Platform/Views/Group/Create.Mobile.cshtml"] = ["platform-pages.css", "platform-pages.mobile.css"],
        ["QuestBoard.Service/Areas/Platform/Views/Group/Edit.cshtml"] = ["platform-pages.css"],
        ["QuestBoard.Service/Areas/Platform/Views/Group/Edit.Mobile.cshtml"] = ["platform-pages.css", "platform-pages.mobile.css"],
        ["QuestBoard.Service/Areas/Platform/Views/LinkedBoards/Index.cshtml"] = ["platform-pages.css"],
        ["QuestBoard.Service/Areas/Platform/Views/LinkedBoards/Index.Mobile.cshtml"] = ["platform-pages.css", "platform-pages.mobile.css"],
        ["QuestBoard.Service/Areas/Platform/Views/Users/Index.cshtml"] = ["platform-pages.css"],
        ["QuestBoard.Service/Areas/Platform/Views/Users/Index.Mobile.cshtml"] = ["platform-pages.css", "platform-pages.mobile.css", "platform-users-boards.mobile.css"],
        ["QuestBoard.Service/Areas/Platform/Views/Group/Members.cshtml"] = ["platform-pages.css"],
        ["QuestBoard.Service/Areas/Platform/Views/Group/Members.Mobile.cshtml"] = ["platform-pages.css", "platform-pages.mobile.css"],
        ["QuestBoard.Service/Areas/Platform/Views/Group/Delete.cshtml"] = ["platform-group-delete.css"],
        ["QuestBoard.Service/Areas/Platform/Views/Group/Delete.Mobile.cshtml"] = ["platform-group-delete.mobile.css"],
    };

    // The sheets that follow the layered rules: one pages block, tokens only, no endless motion, no cropped picture. The
    // three board deletion sheets were converted earlier and are held to the same rules here.
    private static readonly string[] AreaSheets =
    [
        "QuestBoard.Service/wwwroot/css/admin-pages.css",
        "QuestBoard.Service/wwwroot/css/admin-pages.mobile.css",
        "QuestBoard.Service/wwwroot/css/platform-group-delete.css",
        "QuestBoard.Service/wwwroot/css/platform-group-delete.mobile.css",
        "QuestBoard.Service/wwwroot/css/platform-users-boards.mobile.css",
        "QuestBoard.Service/wwwroot/css/platform-pages.css",
        "QuestBoard.Service/wwwroot/css/platform-pages.mobile.css",
    ];

    // The unlayered phone sheets of the area that are still in use. A slice deletes its sheet with its last consumer and
    // moves the name to the retired list.
    private static readonly string[] PendingSheets = [];

    // Every script file the area's pages load.
    private static readonly string[] AreaModules =
    [
        "QuestBoard.Service/wwwroot/js/confirm-submit.js",
    ];

    // Files this work retires; each is deleted and must stay deleted, because its content moved into the layered sheets.
    private static readonly string[] RetiredFiles =
    [
        "QuestBoard.Service/wwwroot/css/admin-quests.mobile.css",
        "QuestBoard.Service/wwwroot/css/admin-users.mobile.css",
        "QuestBoard.Service/wwwroot/css/admin-form.mobile.css",
        "QuestBoard.Service/wwwroot/css/platform-linked-boards.mobile.css",
        "QuestBoard.Service/wwwroot/css/platform-users.mobile.css",
        "QuestBoard.Service/wwwroot/css/platform-group.mobile.css",
    ];

    private static readonly string[] LayoutFiles =
    [
        "QuestBoard.Service/Views/Shared/_Layout.cshtml",
        "QuestBoard.Service/Views/Shared/_Layout.Mobile.cshtml",
        "QuestBoard.Service/Areas/Platform/Views/Shared/_Layout.Platform.cshtml",
        "QuestBoard.Service/Areas/Platform/Views/Shared/_Layout.Platform.Mobile.cshtml",
    ];

    private static readonly (string Label, Regex Pattern)[] InlineCodePatterns =
    [
        ("a style attribute", new Regex(@"\sstyle\s*=", RegexOptions.IgnoreCase)),
        ("a style element", new Regex(@"<style\b", RegexOptions.IgnoreCase)),
        ("an inline script element", new Regex(@"<script\b(?![^>]*\bsrc\s*=)[^>]*>", RegexOptions.IgnoreCase)),
        ("an inline event handler", new Regex(@"\son[a-z]+\s*=", RegexOptions.IgnoreCase)),
    ];

    // ---- Scanners ---------------------------------------------------------------------------------

    private static string StripRazorAndHtmlComments(string view)
    {
        var withoutRazor = Regex.Replace(view, @"@\*[\s\S]*?\*@", " ");
        return Regex.Replace(withoutRazor, @"<!--[\s\S]*?-->", " ");
    }

    private static string StripCssComments(string css) => Regex.Replace(css, @"/\*[\s\S]*?\*/", " ");

    private static List<string> InlineCode(string view)
    {
        var markup = StripRazorAndHtmlComments(view);
        return InlineCodePatterns.SelectMany(p => p.Pattern.Matches(markup).Select(m => p.Label + ": " + m.Value.Trim())).ToList();
    }

    // The opening tags of the post forms in a view that carry no antiforgery token: a post form is covered when its
    // tag names a controller or an action (the form tag helper then writes the token) or when a token call appears
    // before the form closes.
    private static List<string> PostFormsWithoutAToken(string view)
    {
        var markup = StripRazorAndHtmlComments(view);
        var uncovered = new List<string>();
        foreach (Match form in Regex.Matches(markup, @"<form\b[^>]*>", RegexOptions.IgnoreCase))
        {
            if (!Regex.IsMatch(form.Value, @"\bmethod\s*=\s*""post""", RegexOptions.IgnoreCase))
                continue;

            if (Regex.IsMatch(form.Value, @"\basp-(?:action|controller)\b", RegexOptions.IgnoreCase))
                continue;

            var rest = markup[(form.Index + form.Length)..];
            var close = rest.IndexOf("</form>", StringComparison.OrdinalIgnoreCase);
            var body = close < 0 ? rest : rest[..close];
            if (!body.Contains("AntiForgeryToken", StringComparison.Ordinal))
                uncovered.Add(form.Value);
        }

        return uncovered;
    }

    // The declarations in a stylesheet or a view that crop or reshape a picture. Comments are removed first so a
    // sentence that explains the rule does not count as the rule.
    private static List<string> WholeImageBreaks(string text)
    {
        var withoutComments = StripCssComments(StripRazorAndHtmlComments(text));
        return Regex.Matches(withoutComments, @"(?<![\w-])(aspect-ratio|object-fit)\s*:")
            .Select(m => m.Groups[1].Value).ToList();
    }

    // What a layered stylesheet must not hold, by label: a colour written out instead of a token, an important flag,
    // a transition of every property, keyframes, and an animation that never ends.
    private static List<string> SheetBreaks(string css)
    {
        var text = StripCssComments(css);
        var breaks = new List<string>();
        if (Regex.IsMatch(text, @"[:\s,(]#[0-9a-fA-F]{3,8}(?![\w-])|(?<![\w-])(?:rgb|rgba|hsl|hsla|hwb)\("))
            breaks.Add("a colour literal");
        if (Regex.IsMatch(text, @"!\s*important", RegexOptions.IgnoreCase))
            breaks.Add("an important flag");
        if (Regex.IsMatch(text, @"(?<![\w-])transition(?:-property)?\s*:\s*all\b"))
            breaks.Add("transition: all");
        if (Regex.IsMatch(text, @"@keyframes\b"))
            breaks.Add("@keyframes");
        if (Regex.IsMatch(text, @"(?<![\w-])animation(?:-iteration-count)?\s*:[^;}""]*\binfinite\b"))
            breaks.Add("an infinite animation");
        return breaks;
    }

    // The ways a script can hand a string to the markup parser.
    private static List<string> MarkupFromStrings(string script) =>
        Regex.Matches(script, @"\.(?:innerHTML|outerHTML)\b|insertAdjacentHTML|document\.write")
            .Select(m => m.Value).ToList();

    // The shapes the old delete handlers used: a delete verb on a form, a script that fetches, a native alert.
    private static List<string> OldHandlerShapes(string view) =>
        Regex.Matches(StripRazorAndHtmlComments(view), @"method\s*=\s*""delete""|\bfetch\s*\(|\balert\s*\(", RegexOptions.IgnoreCase)
            .Select(m => m.Value).ToList();

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

    // The shapes a view must not hold because the role and board type words come from one mapping: a comparison with
    // an enum member, a printed enum name, an enum-built option list.
    private static List<string> EnumDisplayBranches(string view) =>
        Regex.Matches(StripRazorAndHtmlComments(view),
                @"(?:==|!=|\bis\b|\bcase\b)\s*(?:GroupRole|BoardType)\.\w+|(?:GroupRole|BoardType)\.\w+\s*(?:==|!=)" +
                @"|\b(?:GroupRole|BoardType)\s*\.ToString\(|GetEnumSelectList\s*<\s*(?:GroupRole|BoardType)|Enum\.GetValues\s*<\s*(?:GroupRole|BoardType)")
            .Select(m => m.Value).ToList();

    private static int HeadingOneCount(string view) =>
        Regex.Matches(StripRazorAndHtmlComments(view), @"<h1\b", RegexOptions.IgnoreCase).Count;

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

    private static int StylesSectionCount(string view) =>
        Regex.Matches(StripRazorAndHtmlComments(view), @"@section\s+Styles\b").Count;

    // The repository-relative paths of the files in a folder that match a pattern, in a stable order.
    private static List<string> FilesIn(string folder, string pattern) =>
        Directory.GetFiles(ResolveRepoFile(folder.Split('/')), pattern)
            .Select(file => folder + "/" + Path.GetFileName(file)).OrderBy(name => name, StringComparer.Ordinal).ToList();

    // Every view the area owns: the pages in the Admin folder, the Platform Group, Users and LinkedBoards folders, and
    // the shared partials the area's status badges are drawn from.
    private static List<string> AreaViewsOnDisk() =>
        FilesIn("QuestBoard.Service/Views/Admin", "*.cshtml")
            .Concat(FilesIn("QuestBoard.Service/Areas/Platform/Views/Group", "*.cshtml"))
            .Concat(FilesIn("QuestBoard.Service/Areas/Platform/Views/Users", "*.cshtml"))
            .Concat(FilesIn("QuestBoard.Service/Areas/Platform/Views/LinkedBoards", "*.cshtml"))
            .Concat(FilesIn("QuestBoard.Service/Views/Shared", "_QuestStatusBadge*.cshtml"))
            .Concat(FilesIn("QuestBoard.Service/Views/Shared", "_RoleBadge*.cshtml"))
            .Concat(FilesIn("QuestBoard.Service/Views/Shared", "_BoardTypeBadge*.cshtml")).ToList();

    // The ways the lists can stop matching the views the area owns: a view on disk that is in no list, a list entry
    // that is not an area view, a list entry twice.
    private static List<string> ScopeBreaks(IEnumerable<string> onDisk, IReadOnlyCollection<string> listed)
    {
        var disk = onDisk.ToList();
        var breaks = disk.Where(view => !listed.Contains(view)).Select(view => $"{view} is an area view but is in no list").ToList();
        breaks.AddRange(listed.Where(view => !disk.Contains(view)).Select(view => $"{view} is listed but is not an area view"));
        breaks.AddRange(listed.GroupBy(view => view).Where(g => g.Count() > 1).Select(g => $"{g.Key} is listed twice"));
        return breaks;
    }

    // ---- Facts ------------------------------------------------------------------------------------

    [Fact]
    public void ConvertedViews_CarryNoInlineStyleScriptOrHandler()
    {
        ConvertedViews.Should().NotBeEmpty();
        foreach (var path in ConvertedViews)
            InlineCode(ReadRepoFile(path)).Should().BeEmpty(
                $"{path} must carry no inline code; it belongs in a stylesheet or a script file");
    }

    [Fact]
    public void InlineCodeScanner_FlagsEachShapeAndIgnoresCommentsAndSrcScripts()
    {
        InlineCode("<p style=\"max-width: 300px;\">x</p>").Should().ContainSingle(m => m.StartsWith("a style attribute"));
        InlineCode("<style>.a { color: red; }</style>").Should().ContainSingle(m => m.StartsWith("a style element"));
        InlineCode("<script>alert(1)</script>").Should().ContainSingle(m => m.StartsWith("an inline script element"));
        InlineCode("<button onclick=\"go()\">x</button>").Should().ContainSingle(m => m.StartsWith("an inline event handler"));
        InlineCode("<form onsubmit=\"return ok()\"></form>").Should().ContainSingle(m => m.StartsWith("an inline event handler"));
        InlineCode("<script type=\"module\" src=\"~/js/a.js\"></script>").Should().BeEmpty("a script with a source is a file");
        InlineCode("@* <p style=\"x\"> *@ <!-- <style> --> <p class=\"a\">x</p>").Should().BeEmpty("a comment is not markup");
    }

    [Fact]
    public void ConvertedViews_LinkTheirSheets()
    {
        ViewSheetLinks.Keys.Should().BeSubsetOf(ConvertedViews);
        foreach (var (path, sheets) in ViewSheetLinks)
        {
            var view = ReadRepoFile(path);
            foreach (var sheet in sheets)
                LinksSheet(view, sheet).Should().BeTrue(
                    $"{path} must link {sheet} from its Styles section so the sheet loads only where it is used");
        }

        foreach (var path in ConvertedViews.Where(p => !Path.GetFileName(p).StartsWith('_')))
            ViewSheetLinks.Should().ContainKey(path, "every converted page names the sheets it links");
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
    public void ConvertedPages_HaveOneStylesSection_AndTheLayoutsLinkNoAreaSheet()
    {
        foreach (var path in ConvertedViews.Where(p => !Path.GetFileName(p).StartsWith('_')))
            StylesSectionCount(ReadRepoFile(path)).Should().Be(1, $"{path} links its sheets from exactly one Styles section");

        foreach (var layout in LayoutFiles)
            ReadRepoFile(layout).Should().NotContain("admin-pages", $"{layout} must not load a page sheet for every page");

        StylesSectionCount("@section Styles { }\n@section Styles { }").Should().Be(2);
        StylesSectionCount("@* @section Styles { } *@").Should().Be(0);
    }

    [Fact]
    public void AreaSheets_HoldNoLiteralImportantFlagMotionOrCroppedPicture()
    {
        foreach (var path in AreaSheets)
        {
            var css = ReadRepoFile(path);
            SheetBreaks(css).Should().BeEmpty(
                $"{path} holds tokens only: no colour literal, no important flag, no transition of every property, no keyframes, nothing endless");
            WholeImageBreaks(css).Should().BeEmpty($"{path} crops or reshapes no picture");
            Regex.Matches(css, @"@layer\s+pages\s*\{").Count.Should().Be(1, $"{path} is a single pages layer block");
        }
    }

    [Fact]
    public void SheetScanner_FlagsEachBreakAndAllowsTokensAndComments()
    {
        SheetBreaks(".a { color: #f4e4bc; }").Should().ContainSingle("a colour literal");
        SheetBreaks(".a { color: rgba(0, 0, 0, 0.5); }").Should().ContainSingle("a colour literal");
        SheetBreaks(".a { color: var(--qb-fg) !important; }").Should().ContainSingle("an important flag");
        SheetBreaks(".a { transition: all 0.3s ease; }").Should().ContainSingle("transition: all");
        SheetBreaks("@keyframes spin { to { transform: none; } }").Should().ContainSingle("@keyframes");
        SheetBreaks(".a { animation: spin 1s linear infinite; }").Should().ContainSingle("an infinite animation");
        SheetBreaks(".a { animation-iteration-count: infinite; }").Should().ContainSingle("an infinite animation");
        SheetBreaks(".a { color: var(--qb-fg); background: color-mix(in oklch, var(--qb-wood) 50%, transparent); transition: color 0.2s; }").Should().BeEmpty();
        SheetBreaks("/* #fff, !important, transition: all, @keyframes, infinite */ .a { --note: 1; }").Should().BeEmpty("a comment is not a declaration");
        SheetBreaks("#main-content .a { margin: 0; }").Should().BeEmpty("an id selector is not a colour");
    }

    [Fact]
    public void WholeImageScanner_FlagsBothDeclarationsAndIgnoresCommentsAndOtherNames()
    {
        WholeImageBreaks(".thumb { aspect-ratio: 4 / 3; object-fit: cover; }").Should().BeEquivalentTo("aspect-ratio", "object-fit");
        WholeImageBreaks(".thumb { aspect-ratio: 1; }").Should().ContainSingle();
        WholeImageBreaks(".thumb { width: 100%; height: auto; }").Should().BeEmpty();
        WholeImageBreaks("/* never use aspect-ratio: or object-fit: here */ .thumb { max-width: 100%; }").Should().BeEmpty();
        WholeImageBreaks(".thumb { --my-aspect-ratio: 1; }").Should().BeEmpty("a custom property that merely contains the name is not the declaration");
        WholeImageBreaks("<img class=\"a\" />").Should().BeEmpty();
    }

    [Fact]
    public void AreaSheetsAndConvertedViews_CropNoPicture()
    {
        foreach (var path in AreaSheets.Concat(PendingSheets).Concat(ConvertedViews))
            WholeImageBreaks(ReadRepoFile(path)).Should().BeEmpty($"{path} must keep every picture whole");
    }

    [Fact]
    public void AreaModules_BuildNoMarkupFromStrings()
    {
        AreaModules.Should().NotBeEmpty();
        foreach (var path in AreaModules)
            MarkupFromStrings(ReadRepoFile(path)).Should().BeEmpty($"{path} sets text and attributes, never markup");

        MarkupFromStrings("el.innerHTML = name;").Should().ContainSingle();
        MarkupFromStrings("el.insertAdjacentHTML('beforeend', x); document.write(y);").Should().HaveCount(2);
        MarkupFromStrings("el.textContent = name;").Should().BeEmpty();
    }

    [Fact]
    public void ConvertedViews_EveryPostFormCarriesATokenSource()
    {
        foreach (var path in ConvertedViews)
            PostFormsWithoutAToken(ReadRepoFile(path)).Should().BeEmpty(
                $"{path} posts only forms that carry an antiforgery token, from the form tag helper or an explicit call");
    }

    [Fact]
    public void TokenScanner_FlagsAPostFormWithAnExplicitActionAndNoToken()
    {
        PostFormsWithoutAToken("<form action=\"/x\" method=\"post\"><button>go</button></form>").Should().HaveCount(1);
        PostFormsWithoutAToken("<form asp-action=\"Delete\" method=\"post\"></form>").Should().BeEmpty();
        PostFormsWithoutAToken("<form action=\"/x\" method=\"post\">@Html.AntiForgeryToken()</form>").Should().BeEmpty();
        PostFormsWithoutAToken("<form action=\"/x\" method=\"get\"></form>").Should().BeEmpty();
        PostFormsWithoutAToken("@* <form action=\"/x\" method=\"post\"></form> *@").Should().BeEmpty();
    }

    [Fact]
    public void ConvertedViews_FormatNoRawDate()
    {
        foreach (var path in ConvertedViews)
            RawDateFormats(ReadRepoFile(path)).Should().BeEmpty($"{path} renders a date through the date helpers");

        RawDateFormats("@x.ToString(\"dd MMM yyyy\")").Should().ContainSingle();
        RawDateFormats("@x.ToString(\"d\")").Should().ContainSingle();
        RawDateFormats("value=\"@x.ToString(\"yyyy-MM-dd\")\"").Should().BeEmpty("a date input's machine value is allowed");
        RawDateFormats("@* @x.ToString(\"dd MMM\") *@").Should().BeEmpty();
    }

    [Fact]
    public void ConvertedPages_HaveOneH1_AndPartialsNone()
    {
        foreach (var path in ConvertedViews)
            HeadingOneCount(ReadRepoFile(path)).Should().Be(
                Path.GetFileName(path).StartsWith('_') ? 0 : 1,
                $"{path}: a page has one h1 in its card header; a partial has none");

        HeadingOneCount("<h1>a</h1><h1>b</h1>").Should().Be(2);
        HeadingOneCount("@* <h1>a</h1> *@<h2>b</h2>").Should().Be(0);
    }

    [Fact]
    public void ConvertedViews_BranchOnNoRoleOrBoardTypeForDisplayText()
    {
        foreach (var path in ConvertedViews)
            EnumDisplayBranches(ReadRepoFile(path)).Should().BeEmpty(
                $"{path} takes role and board type words, badges and options from the display mapping");

        EnumDisplayBranches("@if (item.BoardType == BoardType.Campaign) { }").Should().NotBeEmpty();
        EnumDisplayBranches("@if (GroupRole.Admin == member.GroupRole) { }").Should().NotBeEmpty();
        EnumDisplayBranches("@member.GroupRole.ToString()").Should().NotBeEmpty();
        EnumDisplayBranches("asp-items=\"Html.GetEnumSelectList<GroupRole>()\"").Should().NotBeEmpty();
        EnumDisplayBranches("@foreach (var role in Enum.GetValues<GroupRole>())").Should().NotBeEmpty();
        EnumDisplayBranches("var boardType = (BoardType)ViewBag.BoardType;").Should().BeEmpty("a cast that hands the type to the mapping is not a branch");
        EnumDisplayBranches("@* if (x == BoardType.Campaign) *@").Should().BeEmpty();
    }

    [Fact]
    public void ConvertedViews_CarryNoDeleteVerbFetchOrAlert()
    {
        foreach (var path in ConvertedViews)
            OldHandlerShapes(ReadRepoFile(path)).Should().BeEmpty($"{path} confirms through a posted form and the confirm module");

        OldHandlerShapes("<form method=\"delete\">").Should().ContainSingle();
        OldHandlerShapes("fetch('/Admin/DeleteQuest/1', { method: 'DELETE' })").Should().ContainSingle();
        OldHandlerShapes("alert('done')").Should().ContainSingle();
        OldHandlerShapes("@* alert('x') *@").Should().BeEmpty();
    }

    [Fact]
    public void RetiredFiles_AreGone()
    {
        RetiredFiles.Should().NotBeEmpty();
        foreach (var path in RetiredFiles)
            File.Exists(ResolveRepoFile(path.Split('/'))).Should().BeFalse(
                $"{path} was folded into the layered admin sheets and must not come back");
    }

    [Fact]
    public void PendingSheets_StillExist_AndEveryAreaSheetOnDiskIsListed()
    {
        foreach (var path in PendingSheets)
            File.Exists(ResolveRepoFile(path.Split('/'))).Should().BeTrue(
                $"{path} is listed as still in use; a slice that deletes it moves it to the retired list");

        var onDisk = FilesIn("QuestBoard.Service/wwwroot/css", "admin-*.css")
            .Concat(FilesIn("QuestBoard.Service/wwwroot/css", "platform-*.css")).ToList();
        onDisk.Where(file => !AreaSheets.Contains(file) && !PendingSheets.Contains(file)).Should().BeEmpty(
            "a sheet of the area left out of both lists would escape the scans");
    }

    [Fact]
    public void PhaseScope_EveryAreaViewIsConvertedOrPending()
    {
        ScopeBreaks(AreaViewsOnDisk(), ConvertedViews.Concat(PendingViews).ToList()).Should().BeEmpty(
            "every Admin and Platform view and shared status partial is in exactly one of the converted and pending lists");
    }

    [Fact]
    public void ScopeScanner_FlagsAnUnlistedViewAForeignEntryAndADuplicate()
    {
        ScopeBreaks(["a", "b"], ["a", "b"]).Should().BeEmpty();
        ScopeBreaks(["a", "b"], ["a"]).Should().ContainSingle(b => b.StartsWith("b "));
        ScopeBreaks(["a"], ["a", "Quest/Index.cshtml"]).Should().ContainSingle(b => b.StartsWith("Quest/"));
        ScopeBreaks(["a"], ["a", "a"]).Should().ContainSingle(b => b.Contains("twice"));
    }

    [Fact]
    public void PhaseScope_ConvertedViewsAreNotAlsoPending()
    {
        ConvertedViews.Intersect(PendingViews).Should().BeEmpty("a view is converted or pending, never both");
    }

    // The finished state: nothing waits for conversion, every view the area owns is in the converted list, the six old
    // phone sheets are gone and each is named in the retired list, and no unlayered sheet of the area remains.
    [Fact]
    public void PhaseScope_IsFullyConverted()
    {
        PendingViews.Should().BeEmpty("every view of the area is converted");
        PendingSheets.Should().BeEmpty("no unlayered sheet of the area is still in use");

        var views = AreaViewsOnDisk();
        views.Should().HaveCountGreaterThanOrEqualTo(29, "the area's 26 views and its three shared status partials are all scanned");
        ScopeBreaks(views, ConvertedViews).Should().BeEmpty("every area view is in the converted list and no entry is foreign");

        string[] oldPhoneSheets =
        [
            "admin-form.mobile.css", "admin-quests.mobile.css", "admin-users.mobile.css",
            "platform-group.mobile.css", "platform-linked-boards.mobile.css", "platform-users.mobile.css",
        ];
        foreach (var sheet in oldPhoneSheets)
        {
            var path = "QuestBoard.Service/wwwroot/css/" + sheet;
            RetiredFiles.Should().Contain(path, $"{sheet} is one of the six retired phone sheets");
            File.Exists(ResolveRepoFile(path.Split('/'))).Should().BeFalse($"{path} must stay deleted");
        }

        foreach (var sheet in FilesIn("QuestBoard.Service/wwwroot/css", "admin-*.css")
                     .Concat(FilesIn("QuestBoard.Service/wwwroot/css", "platform-*.css")))
            AreaSheets.Should().Contain(sheet, "every sheet of the area is layered and held to the sheet rules");
    }

    [Fact]
    public void PhaseSourceFiles_ListEveryConvertedViewAndAdminSheet()
    {
        var files = ConvertedViews
            .Concat(FilesIn("QuestBoard.Service/wwwroot/css", "admin-pages*.css")).ToList();

        files.Where(file => !PhaseSourceFiles.Contains(file)).Should().BeEmpty(
            "a converted view or admin sheet left out of the list would escape the planning-reference scan");
    }

    // ---- Reproduction facts and verbs ---------------------------------------------------------------

    // The test files that held a fact written ahead of its fix, skipped with one fixed reason until the fix landed.
    private static readonly string[] ReproductionFactFiles =
    [
        "QuestBoard.IntegrationTests/Controllers/AdminAccountReachTests.cs",
        "QuestBoard.IntegrationTests/Controllers/AdminRoleGuardTests.cs",
        "QuestBoard.IntegrationTests/Controllers/AdminQuestDeleteTests.cs",
        "QuestBoard.IntegrationTests/Controllers/AdminControllerRateLimitIntegrationTests.cs",
        "QuestBoard.IntegrationTests/Controllers/GroupManagementIntegrationTests.cs",
    ];

    // The skip reason those facts carried, built from two pieces so this file never holds the reason it forbids.
    private const string ReproductionSkipReason = "Reproduces an open " + "gap";

    // A skip attribute argument that carries the reproduction reason; a skip for any other reason is not matched.
    private static List<string> ReproductionSkips(string source) =>
        Regex.Matches(source, @"Skip\s*=\s*""[^""]*" + Regex.Escape(ReproductionSkipReason))
            .Select(m => m.Value).ToList();

    [Fact]
    public void ReproductionFacts_AreAllEnabled()
    {
        foreach (var path in ReproductionFactFiles)
            ReproductionSkips(ReadRepoFile(path)).Should().BeEmpty(
                $"{path} holds no fact still skipped as a reproduction: each was enabled by the change that fixed it");
    }

    [Fact]
    public void ReproductionSkipScanner_FlagsTheReasonAndLeavesOtherSkipsAlone()
    {
        var seeded = "[Fact(Skip = \"" + ReproductionSkipReason + "; enabled by its fix\")]";
        ReproductionSkips(seeded).Should().ContainSingle();
        ReproductionSkips("[Theory(Skip=\"" + ReproductionSkipReason + "\")]").Should().ContainSingle();
        ReproductionSkips("[Fact(Skip = \"Needs a SQL Server instance\")]").Should().BeEmpty("a skip for another reason is left alone");
        ReproductionSkips("[Fact] // " + ReproductionSkipReason).Should().BeEmpty("a mention outside a skip attribute is not a skip");
    }

    // The controllers of the area: the Admin controller and the three Platform controllers.
    private static readonly string[] AreaControllers =
    [
        "QuestBoard.Service/Controllers/Admin/AdminController.cs",
        "QuestBoard.Service/Areas/Platform/Controllers/GroupController.cs",
        "QuestBoard.Service/Areas/Platform/Controllers/LinkedBoardsController.cs",
        "QuestBoard.Service/Areas/Platform/Controllers/UsersController.cs",
    ];

    // A delete verb on an action: the attribute, written bare, with a route template or among others in one bracket.
    private static List<string> DeleteVerbs(string source) =>
        Regex.Matches(source, @"\bHttpDelete(?:Attribute)?\b").Select(m => m.Value).ToList();

    [Fact]
    public void AreaControllers_UseNoDeleteVerb()
    {
        foreach (var path in AreaControllers)
            DeleteVerbs(StripCodeComments(ReadRepoFile(path))).Should().BeEmpty(
                $"{path} removes through a posted form with an antiforgery token, never a delete verb");
    }

    [Fact]
    public void DeleteVerbScanner_FlagsTheAttributeAndIgnoresPostAndComments()
    {
        DeleteVerbs("[HttpDelete]").Should().ContainSingle();
        DeleteVerbs("[HttpDelete(\"Quest/{id}\")]").Should().ContainSingle();
        DeleteVerbs("[Authorize, HttpDelete]").Should().ContainSingle();
        DeleteVerbs("[Authorize(Policy = \"AdminOnly\"), HttpDelete(\"x\")]").Should().ContainSingle();
        DeleteVerbs("[HttpPost, ValidateAntiForgeryToken]").Should().BeEmpty();
        DeleteVerbs(StripCodeComments("// [HttpDelete]\n/* [HttpDelete] */")).Should().BeEmpty("a comment is not an attribute");
    }

    private static string StripCodeComments(string source) =>
        Regex.Replace(Regex.Replace(source, @"/\*[\s\S]*?\*/", " "), @"//[^\r\n]*", " ");

    // The Email Stats tiles are siblings in one grid: the sheet holds the grid and the tile rules, and the view holds no
    // Bootstrap status colour utility. A file name in an alert's code element keeps the alert's light text.
    [Fact]
    public void EmailStatsSheetRules_HoldTheTileGridAndTheAlertCodeColour()
    {
        var sheet = ReadRepoFile("QuestBoard.Service/wwwroot/css/admin-pages.css");

        sheet.Should().Contain("repeat(auto-fit, minmax(140px, 1fr))");
        sheet.Should().MatchRegex(@"\.stat-tile\s*\{[^}]*background-color:\s*var\(--qb-surface-overlay\)");
        sheet.Should().MatchRegex(@"\.alert code\s*\{\s*color:\s*inherit;");

        var view = ReadRepoFile("QuestBoard.Service/Views/Admin/EmailStats.cshtml");
        view.Should().NotMatchRegex(@"text-(primary|success|warning|danger|info)\b");
        view.Should().NotContain("modern-card text-center");
    }
}
