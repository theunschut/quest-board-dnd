using System.Text.RegularExpressions;

namespace QuestBoard.IntegrationTests.Tests;

/// <summary>
/// Static guard for the account and board-picker views once they are on the shared foundation: no inline
/// style, script or handler in a converted view, the retired phone twins stay gone, every page links its
/// own sheet through its Styles section, the picker rows stay native submit buttons, and no planning
/// reference is left in the area's files. An integration suite reading rendered markup cannot see a comment,
/// so only a scan of the files themselves holds those lines.
/// </summary>
public class AccountConversionGuardTests
{
    // The views already converted. Later conversions of the area add their views here.
    private static readonly string[] ConvertedViews =
    [
        "QuestBoard.Service/Views/GroupPicker/Index.cshtml",
        "QuestBoard.Service/Views/Account/Profile.cshtml",
        "QuestBoard.Service/Views/Account/Profile.Mobile.cshtml",
        "QuestBoard.Service/Views/Account/Login.cshtml",
        "QuestBoard.Service/Views/Account/ForgotPassword.cshtml",
        "QuestBoard.Service/Views/Account/SetPassword.cshtml",
        "QuestBoard.Service/Views/Account/ChangePassword.cshtml",
        "QuestBoard.Service/Views/Account/Edit.cshtml",
        "QuestBoard.Service/Views/Account/Edit.Mobile.cshtml",
        "QuestBoard.Service/Views/Shared/AccessDenied.cshtml",
        "QuestBoard.Service/Views/Home/Index.cshtml",
    ];

    // Phone twins that were merged into their desktop view and must not come back.
    private static readonly string[] RetiredFiles =
    [
        "QuestBoard.Service/Views/GroupPicker/Index.Mobile.cshtml",
        "QuestBoard.Service/Views/Home/Index.Mobile.cshtml",
        "QuestBoard.Service/Views/Account/Login.Mobile.cshtml",
        "QuestBoard.Service/Views/Account/ForgotPassword.Mobile.cshtml",
        "QuestBoard.Service/Views/Account/SetPassword.Mobile.cshtml",
        "QuestBoard.Service/Views/Account/ChangePassword.Mobile.cshtml",
        "QuestBoard.Service/wwwroot/css/account.mobile.css",
    ];

    // Each view with the sheet it must link through its own Styles section.
    private static readonly (string View, string Sheet)[] SheetLinks =
    [
        ("QuestBoard.Service/Views/GroupPicker/Index.cshtml", "group-picker.css"),
        ("QuestBoard.Service/Views/Account/Profile.cshtml", "account.css"),
        ("QuestBoard.Service/Views/Account/Profile.Mobile.cshtml", "account.css"),
        ("QuestBoard.Service/Views/Account/Login.cshtml", "account.css"),
        ("QuestBoard.Service/Views/Account/ForgotPassword.cshtml", "account.css"),
        ("QuestBoard.Service/Views/Account/SetPassword.cshtml", "account.css"),
        ("QuestBoard.Service/Views/Account/ChangePassword.cshtml", "account.css"),
        ("QuestBoard.Service/Views/Account/Edit.cshtml", "account.css"),
        ("QuestBoard.Service/Views/Account/Edit.Mobile.cshtml", "account.css"),
        ("QuestBoard.Service/Views/Shared/AccessDenied.cshtml", "account.css"),
        ("QuestBoard.Service/Views/Home/Index.cshtml", "account.css"),
    ];

    // The area's sheets, scanned for planning references with the converted views.
    private static readonly string[] AreaSheets =
    [
        "QuestBoard.Service/wwwroot/css/group-picker.css",
        "QuestBoard.Service/wwwroot/css/account.css",
    ];

    // The area's page scripts, scanned for planning references and held to the Profile script facts below.
    private const string ProfileScriptPath = "QuestBoard.Service/wwwroot/js/profile-subscriptions.js";

    private static readonly string[] ProfileViews =
    [
        "QuestBoard.Service/Views/Account/Profile.cshtml",
        "QuestBoard.Service/Views/Account/Profile.Mobile.cshtml",
    ];

    // Written as regex data, with each forbidden literal built from fragments, so this file never holds the
    // shapes it forbids elsewhere.
    private static readonly (string Label, Regex Pattern)[] PlanningReferencePatterns =
    [
        ("a decision id", new Regex(@"\bD-(0[1-9]|1[0-9]|2[01])\b")),
        ("a question id", new Regex(@"\bQ-(0[1-9]|1[0-9])\b")),
        ("a definition-of-converted id", new Regex("\\bDo" + "C-\\d+\\b")),
        ("a finding id", new Regex("\\bA-acc" + "ount-\\d+")),
        ("a phase reference", new Regex("Pha" + "se\\s+9" + "6\\b", RegexOptions.IgnoreCase)),
        ("a plan id", new Regex(@"\b9" + @"6-(0[1-9]|1[0-2])\b")),
        ("a planning document name", new Regex(
            "CONT" + "EXT\\.md|RESE" + "ARCH\\.md|UI-" + "SPEC|PATT" + "ERNS\\.md|VALID" + "ATION\\.md")),
    ];

    private static readonly (string Label, Regex Pattern)[] InlineCodePatterns =
    [
        ("an inline style attribute", new Regex(@"\sstyle\s*=", RegexOptions.IgnoreCase)),
        ("a style block", new Regex(@"<style\b", RegexOptions.IgnoreCase)),
        ("an inline script", new Regex(@"<script\b(?![^>]*\bsrc=)[^>]*>", RegexOptions.IgnoreCase)),
        ("an inline event handler", new Regex(@"\son[a-z]+\s*=", RegexOptions.IgnoreCase)),
    ];

    // The repository root is the directory holding the solution file, found before any path is joined, because
    // bin/ holds extension-less apphost binaries named like the project folders.
    private static string ResolveRepoFile(string relativePath) =>
        Path.Combine(FindRepositoryRoot().FullName, Path.Combine(relativePath.Split('/')));

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

    // The file without its Razor comments, which describe the markup in the same words it uses.
    private static string Markup(string relativePath) =>
        Regex.Replace(File.ReadAllText(ResolveRepoFile(relativePath)), @"@\*[\s\S]*?\*@", " ");

    // A quoted string, a template literal, a line comment or a block comment, matched left to right so a comment marker
    // inside a string is kept and an apostrophe inside a comment is dropped with it.
    private static readonly Regex ScriptTokens = new(
        @"'(?:\\.|[^'\\\n])*'|""(?:\\.|[^""\\\n])*""|`(?:\\.|[^`\\])*`|//[^\n]*|/\*[\s\S]*?\*/");

    // The script without its comments, which describe the code in the same words it uses.
    private static string Script(string relativePath) =>
        ScriptTokens.Replace(
            File.ReadAllText(ResolveRepoFile(relativePath)),
            match => match.Value.StartsWith("//", StringComparison.Ordinal) || match.Value.StartsWith("/*", StringComparison.Ordinal)
                ? " "
                : match.Value);

    [Fact]
    public void ConvertedViews_CarryNoInlineStyleScriptOrHandler()
    {
        ConvertedViews.Should().NotBeEmpty("the guard must cover at least the converted picker");

        foreach (var path in ConvertedViews)
        {
            File.Exists(ResolveRepoFile(path)).Should().BeTrue($"{path} is a converted view and must exist");
            var markup = Markup(path);
            foreach (var (label, pattern) in InlineCodePatterns)
                pattern.IsMatch(markup).Should().BeFalse($"{path} must not contain {label}");
        }
    }

    [Fact]
    public void RetiredFiles_AreGone()
    {
        RetiredFiles.Should().NotBeEmpty();

        foreach (var path in RetiredFiles)
            File.Exists(ResolveRepoFile(path)).Should().BeFalse($"{path} was merged into its desktop view and must stay deleted");
    }

    [Fact]
    public void AreaViews_LinkTheirSheetThroughTheStylesSection()
    {
        SheetLinks.Should().NotBeEmpty();

        foreach (var (view, sheet) in SheetLinks)
        {
            var markup = Markup(view);
            var section = Regex.Match(markup, @"@section\s+Styles\s*\{(?<body>[^}]*)\}");
            section.Success.Should().BeTrue($"{view} must declare a Styles section");
            section.Groups["body"].Value.Should().Contain($"~/css/{sheet}", $"{view} links its sheet through its Styles section");
            Regex.Matches(markup, Regex.Escape($"~/css/{sheet}")).Count.Should().Be(1, $"{view} links {sheet} exactly once");
        }
    }

    [Fact]
    public void Picker_RowsAreNativeSubmitButtonsInTheSelectGroupForm()
    {
        var markup = Markup("QuestBoard.Service/Views/GroupPicker/Index.cshtml");

        markup.Should().Contain("<button type=\"submit\"", "a board is chosen with a native submit button");
        markup.Should().Contain("group-picker-row");
        markup.Should().Contain("asp-action=\"SelectGroup\"");
        Regex.Matches(markup, "name=\"returnUrl\"").Count.Should().Be(1, "the return address is echoed in its hidden input only");
        markup.Should().NotContain("formaction", "the row submits its own form and never overrides the target");
        markup.Should().NotContain("data-return-url", "the return address is not echoed in an attribute");
    }

    [Fact]
    public void AreaFiles_CarryNoPlanningReference()
    {
        foreach (var path in ConvertedViews.Concat(AreaSheets).Append(ProfileScriptPath))
        {
            if (!File.Exists(ResolveRepoFile(path)))
                continue;

            var text = File.ReadAllText(ResolveRepoFile(path));
            foreach (var (label, pattern) in PlanningReferencePatterns)
                pattern.IsMatch(text).Should().BeFalse($"{path} must not carry {label}");
        }
    }

    // The wrapper class of the retired phone card, written from fragments so this file never holds it whole.
    private static readonly string RetiredPhoneWrapper = "account-" + "card-" + "mobile";

    [Theory]
    [InlineData("QuestBoard.Service/Views/Account/Profile.cshtml")]
    [InlineData("QuestBoard.Service/Views/Account/Profile.Mobile.cshtml")]
    public void Profile_KeepsItsDateHelpersAndRetiresThePhoneWrapper(string view)
    {
        var markup = Markup(view);

        Regex.Matches(markup, Regex.Escape("Html.LocalTime(subscription.CreatedAt, \"date\")")).Count.Should().Be(1,
            $"{view} renders the creation date through the instant helper exactly once");
        Regex.Matches(markup, Regex.Escape("Html.LocalTime(subscription.LastFetchedAt.Value, \"date-time\")")).Count.Should().Be(1,
            $"{view} renders the last-fetched time through the instant helper exactly once");
        markup.Should().NotContain(RetiredPhoneWrapper, $"{view} sits on the shared card");
        markup.Should().NotContain("account.mobile.css", $"{view} no longer links the phone account sheet");
    }

    [Theory]
    [InlineData("QuestBoard.Service/Views/Account/Profile.cshtml")]
    [InlineData("QuestBoard.Service/Views/Account/Profile.Mobile.cshtml")]
    public void ProfileViews_LoadTheSharedScriptAsAModule(string view)
    {
        var markup = Markup(view);
        var section = Regex.Match(markup, @"@section\s+Scripts\s*\{(?<body>[^}]*)\}");

        section.Success.Should().BeTrue($"{view} must declare a Scripts section");
        section.Groups["body"].Value.Should().Contain(
            "<script type=\"module\" src=\"~/js/profile-subscriptions.js\"",
            $"{view} loads the shared Profile script as a module");
        Regex.Matches(markup, Regex.Escape("~/js/profile-subscriptions.js")).Count.Should().Be(1,
            $"{view} loads the shared script exactly once");
    }

    [Fact]
    public void ProfileScript_FillsTheDialogNameAsTextOnly()
    {
        var script = Script(ProfileScriptPath);

        script.Should().Contain("data-subscription-name-target", "the script finds the dialog's name slot");
        Regex.IsMatch(
                script,
                @"querySelector\('\[data-subscription-name-target\]'\)\.textContent\s*=\s*trigger\.dataset\.subscriptionName")
            .Should().BeTrue("the dialog's name slot is filled with textContent from the trigger's dataset");
        script.Should().NotContain("confirm(", "the browser prompt is replaced by the dialog");

        foreach (var sink in new[] { "insertAdjacentHTML", "document.write", "createContextualFragment" })
            script.Should().NotContain(sink, $"{sink} would turn a member-supplied value into markup");

        // Markup is only ever assigned from the two literal restore and success strings the script itself holds.
        var allowedMarkup = new Regex(@"^(?:restHtml|iconOnly\s*\?\s*'[^']*'\s*:\s*'[^']*')$");
        var assignments = Regex.Matches(script, @"\.(?:innerHTML|outerHTML)\s*=(?!=)(?<value>[^;]*);");
        assignments.Should().NotBeEmpty("the Copy button's own restore and success markup is assigned somewhere");
        foreach (Match assignment in assignments)
            allowedMarkup.IsMatch(assignment.Groups["value"].Value.Trim()).Should().BeTrue(
                $"only the script's own literal markup may be assigned, never a dataset value: {assignment.Value.Trim()}");
    }

    [Theory]
    [InlineData("QuestBoard.Service/Views/Account/Profile.cshtml")]
    [InlineData("QuestBoard.Service/Views/Account/Profile.Mobile.cshtml")]
    public void ProfileDelete_StillPostsWithoutScript(string view)
    {
        var markup = Markup(view);

        Regex.Matches(markup, "calendar-subscription-delete-form").Count.Should().Be(1,
            $"{view} carries the per-row class once, in the row template, so the dialog's own revoke form never has it");

        var form = Regex.Match(
            markup,
            "<form\\b(?=[^>]*asp-action=\"RevokeCalendarSubscription\")(?=[^>]*calendar-subscription-delete-form)[^>]*>(?<body>.*?)</form>",
            RegexOptions.Singleline);

        form.Success.Should().BeTrue($"{view} keeps each row's own revoke form");
        var body = form.Groups["body"].Value;

        body.Should().Contain("@Html.AntiForgeryToken()", "the row's form keeps its antiforgery token");
        body.Should().Contain("name=\"id\" value=\"@subscription.Id\"", "the row's form carries the subscription id");
        Regex.IsMatch(body, "<button type=\"submit\"[^>]*data-subscription-id=\"@subscription.Id\"").Should().BeTrue(
            "Delete stays a real submit button, so it posts with no script");
        body.Should().NotContain("data-bs-toggle", "Bootstrap must never open the dialog while the form still posts");
        body.Should().NotContain("data-bs-target", "Bootstrap must never open the dialog while the form still posts");
    }

    [Fact]
    public void ProfileDelete_OnlyTheScriptOpensTheDialog()
    {
        var script = Script(ProfileScriptPath);

        script.Should().Contain("getOrCreateInstance(", "the script opens the dialog through the Modal API");
        script.Should().Contain("preventDefault()", "the script cancels the row's post in the listener that opens the dialog");
        script.Should().Contain("hidden.bs.modal", "the script returns focus itself, as the API open gives none");

        foreach (var view in ProfileViews)
            Markup(view).Should().NotContain("data-bs-target=\"#deleteSubscriptionModal\"",
                $"{view} must not let a data attribute open the dialog by itself");
    }

    [Theory]
    [InlineData("QuestBoard.Service/Views/Account/Profile.cshtml")]
    [InlineData("QuestBoard.Service/Views/Account/Profile.Mobile.cshtml")]
    public void ProfileDeleteDialog_CarriesEveryHookTheScriptReads(string view)
    {
        var markup = Markup(view);

        Regex.Matches(markup, "id=\"deleteSubscriptionModal\"").Count.Should().Be(1,
            $"{view} carries the delete dialog once; without it the row's form would post with no confirmation");

        var dialog = Regex.Match(markup, "id=\"deleteSubscriptionModal\"(?<block>[\\s\\S]*?)id=\"renameSubscriptionModal\"");
        dialog.Success.Should().BeTrue($"{view} keeps the delete dialog ahead of the rename dialog");
        var block = dialog.Groups["block"].Value;

        block.Should().Contain("data-subscription-name-target", "the script fills the member's subscription name here");
        block.Should().Contain("data-keep-subscription", "the script focuses the safe choice when the dialog opens");

        var form = Regex.Match(
            block,
            "<form\\b(?=[^>]*asp-action=\"RevokeCalendarSubscription\")[^>]*>(?<body>.*?)</form>",
            RegexOptions.Singleline);
        form.Success.Should().BeTrue($"{view}: the dialog posts through its own revoke form");
        form.Groups["body"].Value.Should().Contain("@Html.AntiForgeryToken()", "the dialog's form carries its own antiforgery token");
        Regex.IsMatch(form.Groups["body"].Value, "<input\\b(?=[^>]*type=\"hidden\")(?=[^>]*name=\"id\")[^>]*>").Should().BeTrue(
            $"{view}: the dialog's form holds the id field the script fills");

        var script = Script(ProfileScriptPath);
        script.Should().Contain("getElementById('deleteSubscriptionModal')");
        script.Should().Contain("[data-subscription-name-target]");
        script.Should().Contain("input[name=\"id\"]");
        script.Should().Contain("[data-keep-subscription]");
    }

    [Theory]
    [InlineData("QuestBoard.Service/Views/Account/Profile.cshtml")]
    [InlineData("QuestBoard.Service/Views/Account/Profile.Mobile.cshtml")]
    public void ProfileRows_NameTheirActionsAndKeepTheHiddenAddressOutOfTheTabOrder(string view)
    {
        var markup = Markup(view);

        Regex.IsMatch(markup, "<input(?=[^>]*calendar-subscription-address)(?=[^>]*tabindex=\"-1\")[^>]*>").Should().BeTrue(
            $"{view} keeps the hidden address field out of the tab order");
        markup.Should().Contain("aria-label=\"Copy address for @subscription.Name\"");
        markup.Should().Contain("aria-label=\"Rename @subscription.Name\"");
        markup.Should().Contain("aria-label=\"Delete @subscription.Name\"");
        markup.Should().Contain("Keep Current Name");
        markup.Should().Contain("Save Name");
        markup.Should().Contain("data-copy-status", $"{view} carries the status element that announces Copy results");
        markup.Should().NotContain("<h5", $"{view} titles its dialogs with a heading of the right level");
    }

    // Each credential view with the autofill tokens its fields must carry, so a password manager knows
    // which field is which.
    private static readonly (string View, string[] Tokens)[] CredentialViews =
    [
        ("QuestBoard.Service/Views/Account/Login.cshtml", ["username", "current-password"]),
        ("QuestBoard.Service/Views/Account/ForgotPassword.cshtml", ["email"]),
        ("QuestBoard.Service/Views/Account/SetPassword.cshtml", ["new-password"]),
        ("QuestBoard.Service/Views/Account/ChangePassword.cshtml", ["current-password", "new-password"]),
        ("QuestBoard.Service/Views/Account/Edit.cshtml", ["name", "email"]),
        ("QuestBoard.Service/Views/Account/Edit.Mobile.cshtml", ["name", "email"]),
    ];

    [Fact]
    public void CredentialViews_CarryTheirAutocompleteTokensAndOneHeading()
    {
        CredentialViews.Should().NotBeEmpty();

        foreach (var (view, tokens) in CredentialViews)
        {
            var markup = Markup(view);
            foreach (var token in tokens)
                markup.Should().Contain($"autocomplete=\"{token}\"", $"{view} names its field with the {token} token");

            Regex.Matches(markup, @"<h1\b").Count.Should().Be(1, $"{view} carries exactly one page heading");
        }
    }

    // The area's sheets that must not animate every property at once.
    private static readonly string[] TransitionGuardedSheets =
    [
        "QuestBoard.Service/wwwroot/css/account.css",
        "QuestBoard.Service/wwwroot/css/group-picker.css",
    ];

    [Fact]
    public void AccountSheets_DeclareNoTransitionOnAll()
    {
        var transitionOnAll = new Regex(@"transition(-property)?\s*:\s*all\b", RegexOptions.IgnoreCase);

        foreach (var path in TransitionGuardedSheets)
        {
            File.Exists(ResolveRepoFile(path)).Should().BeTrue($"{path} is a live sheet of the area and must exist");

            var css = Regex.Replace(File.ReadAllText(ResolveRepoFile(path)), @"/\*[\s\S]*?\*/", " ");
            css.Should().Contain("{", $"{path} must hold at least one rule, or the check proves nothing");
            transitionOnAll.IsMatch(css).Should().BeFalse($"{path} must not declare a transition on every property");
        }
    }

    // Every folder whose views belong to the area; a view added later must be listed above or this fails.
    private static readonly string[] AreaViewFolders =
    [
        "QuestBoard.Service/Views/Account",
        "QuestBoard.Service/Views/GroupPicker",
        "QuestBoard.Service/Views/Home",
    ];

    private const string PickerView = "QuestBoard.Service/Views/GroupPicker/Index.cshtml";

    [Fact]
    public void TheWholeAreaIsConverted()
    {
        var root = FindRepositoryRoot().FullName;
        var existing = AreaViewFolders
            .SelectMany(folder => Directory.EnumerateFiles(Path.Combine(root, Path.Combine(folder.Split('/'))), "*.cshtml"))
            .Select(file => Path.GetRelativePath(root, file).Replace('\\', '/'))
            .Append("QuestBoard.Service/Views/Shared/AccessDenied.cshtml")
            .ToList();

        existing.Should().NotBeEmpty();

        foreach (var view in existing)
        {
            ConvertedViews.Should().Contain(view, $"{view} is in the area and must be listed as converted");

            var markup = Markup(view);
            foreach (var (label, pattern) in InlineCodePatterns)
                pattern.IsMatch(markup).Should().BeFalse($"{view} must not contain {label}");

            if (view != PickerView)
                SheetLinks.Should().Contain(link => link.View == view && link.Sheet == "account.css",
                    $"{view} links the account sheet through its Styles section");
        }

        foreach (var path in RetiredFiles)
            File.Exists(ResolveRepoFile(path)).Should().BeFalse($"{path} is retired and must stay deleted");

        var colourLiteral = new Regex(@"#[0-9a-fA-F]{3,8}\b|\brgba?\(", RegexOptions.IgnoreCase);
        var importantFlag = new Regex(@"!\s*important", RegexOptions.IgnoreCase);
        foreach (var sheet in AreaSheets)
        {
            var css = Regex.Replace(File.ReadAllText(ResolveRepoFile(sheet)), @"/\*[\s\S]*?\*/", " ");
            colourLiteral.IsMatch(css).Should().BeFalse($"{sheet} takes every colour from a token");
            importantFlag.IsMatch(css).Should().BeFalse($"{sheet} outranks Bootstrap through the pages layer, not an important flag");
        }
    }

    [Fact]
    public void SetPassword_KeepsItsHiddenIdentityAndTokenInputsAndRendersTheTokenNowhereElse()
    {
        var markup = Markup("QuestBoard.Service/Views/Account/SetPassword.cshtml");

        markup.Should().Contain("<input type=\"hidden\" asp-for=\"UserId\" />");
        markup.Should().Contain("<input type=\"hidden\" asp-for=\"Token\" />");
        Regex.Matches(markup, @"Model\.Token|asp-for=""Token""").Count.Should().Be(1,
            "the reset token reaches the page only through its hidden input");
    }

    [Fact]
    public void NoAreaViewLinksARetiredSheet()
    {
        var views = Path.Combine(FindRepositoryRoot().FullName, "QuestBoard.Service", "Views");
        var retiredSheets = RetiredFiles.Where(f => f.EndsWith(".css", StringComparison.Ordinal)).Select(Path.GetFileName).ToArray();
        retiredSheets.Should().NotBeEmpty("at least the phone account sheet is retired");

        foreach (var file in Directory.EnumerateFiles(views, "*.cshtml", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(file);
            foreach (var sheet in retiredSheets)
                text.Should().NotContain(sheet!, $"{Path.GetRelativePath(views, file)} must not link the retired sheet {sheet}");
        }
    }
}
