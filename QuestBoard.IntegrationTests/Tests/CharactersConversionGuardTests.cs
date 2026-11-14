using System.Text.RegularExpressions;
using FluentAssertions;

namespace QuestBoard.IntegrationTests.Tests;

/// <summary>
/// Pins the characters, contacts, Dungeon Master profile and players views as they are moved onto
/// the layered foundation. A character's picture is requested through one shared avatar partial
/// only on the quest pages, and none of the files this work touches names the planning that
/// produced it. The lists below only ever grow as each view is converted; a file a later change
/// deletes is removed from them in the same commit. An integration suite reading rendered markup
/// cannot see what a comment says or which view built a picture address, so only a scan of the
/// sources can hold the line.
/// </summary>
public class CharactersConversionGuardTests
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

    private const string ViewsRoot = "QuestBoard.Service/Views";
    private const string AvatarPartial = "QuestBoard.Service/Views/Shared/_CharacterAvatar.cshtml";

    // Views that carry no inline code, repository-relative. Grows as each view is converted.
    private static readonly string[] ConvertedViews =
    [
        "QuestBoard.Service/Views/ContactCategoryManagement/Edit.cshtml",
        "QuestBoard.Service/Views/ContactCategoryManagement/Manage.cshtml",
        "QuestBoard.Service/Views/ContactCategoryManagement/Manage.Mobile.cshtml",
        "QuestBoard.Service/Views/Characters/Index.cshtml",
        "QuestBoard.Service/Views/Characters/Index.Mobile.cshtml",
        "QuestBoard.Service/Views/Characters/Details.cshtml",
        "QuestBoard.Service/Views/Characters/Create.cshtml",
        "QuestBoard.Service/Views/Characters/Edit.cshtml",
        "QuestBoard.Service/Views/Characters/_CharacterClassRows.cshtml",
        "QuestBoard.Service/Views/Contacts/Create.cshtml",
        "QuestBoard.Service/Views/Contacts/Edit.cshtml",
        "QuestBoard.Service/Views/Contacts/Details.cshtml",
        "QuestBoard.Service/Views/Contacts/Index.cshtml",
        "QuestBoard.Service/Views/Contacts/Index.Mobile.cshtml",
        "QuestBoard.Service/Views/DungeonMaster/EditProfile.cshtml",
    ];

    // Page sheets under the css folder that open the pages layer, by file name. Grows as each sheet is converted.
    private static readonly string[] ConvertedSheets = ["character-detail.css", "character-form.css", "contact-form.css", "contact-detail.css", "dm-profile-form.css", "contact-categories.css", "contact-categories.mobile.css", "member-cards.css", "characters.css", "characters.mobile.css", "contacts.css", "contacts.mobile.css"];

    // For each view, the sheets it must link from its Styles section, by file name. An empty set means the view
    // is styled by the shared components alone.
    private static readonly Dictionary<string, string[]> ViewSheetLinks = new()
    {
        ["QuestBoard.Service/Views/ContactCategoryManagement/Edit.cshtml"] = [],
        ["QuestBoard.Service/Views/ContactCategoryManagement/Manage.cshtml"] = ["contact-categories.css"],
        ["QuestBoard.Service/Views/ContactCategoryManagement/Manage.Mobile.cshtml"] = ["contact-categories.mobile.css"],
        ["QuestBoard.Service/Views/Characters/Index.cshtml"] = ["member-cards.css", "characters.css"],
        ["QuestBoard.Service/Views/Characters/Index.Mobile.cshtml"] = ["member-cards.css", "characters.mobile.css"],
        ["QuestBoard.Service/Views/Characters/Details.cshtml"] = ["markdown-content.css", "character-detail.css"],
        ["QuestBoard.Service/Views/Characters/Create.cshtml"] = ["character-form.css", "image-crop.css"],
        ["QuestBoard.Service/Views/Characters/Edit.cshtml"] = ["character-form.css", "image-crop.css"],
        ["QuestBoard.Service/Views/Characters/_CharacterClassRows.cshtml"] = [],
        ["QuestBoard.Service/Views/Contacts/Create.cshtml"] = ["tagify-vendor.css", "contact-form.css", "image-crop.css"],
        ["QuestBoard.Service/Views/Contacts/Edit.cshtml"] = ["tagify-vendor.css", "contact-form.css", "image-crop.css"],
        ["QuestBoard.Service/Views/Contacts/Details.cshtml"] = ["markdown-content.css", "contact-detail.css"],
        ["QuestBoard.Service/Views/Contacts/Index.cshtml"] = ["member-cards.css", "contacts.css"],
        ["QuestBoard.Service/Views/Contacts/Index.Mobile.cshtml"] = ["member-cards.css", "contacts.mobile.css"],
        ["QuestBoard.Service/Views/DungeonMaster/EditProfile.cshtml"] = ["dm-profile-form.css", "image-crop.css"],
    };

    // Files this work retires; each is deleted and must stay deleted, because its view was merged into the
    // twin that serves both layouts.
    private static readonly string[] RetiredFiles =
    [
        "QuestBoard.Service/Views/ContactCategoryManagement/Edit.Mobile.cshtml",
        "QuestBoard.Service/Views/Characters/Details.Mobile.cshtml",
        "QuestBoard.Service/wwwroot/css/character-detail.mobile.css",
        "QuestBoard.Service/Views/Contacts/Create.Mobile.cshtml",
        "QuestBoard.Service/Views/Contacts/Edit.Mobile.cshtml",
        "QuestBoard.Service/wwwroot/css/contact-form.mobile.css",
        "QuestBoard.Service/Views/Contacts/Details.Mobile.cshtml",
        "QuestBoard.Service/wwwroot/css/contact-detail.mobile.css",
        "QuestBoard.Service/Views/DungeonMaster/EditProfile.Mobile.cshtml",
        "QuestBoard.Service/wwwroot/css/dm-editprofile.mobile.css",
        "QuestBoard.Service/Views/Characters/Create.Mobile.cshtml",
        "QuestBoard.Service/Views/Characters/Edit.Mobile.cshtml",
        "QuestBoard.Service/wwwroot/css/character-form.mobile.css",
    ];

    private const string CropDialogPartial = "QuestBoard.Service/Views/Shared/_ImageCropDialog.cshtml";
    private const string CropModule = "QuestBoard.Service/wwwroot/js/image-crop.js";

    // Page sheets this work converts that load from the Styles section of the pages that use them, never from a
    // layout. Later slices append the characters, contacts and Dungeon Master profile sheets.
    private static readonly string[] PageOnlySheets = ["image-crop.css", "character-detail.css", "character-form.css", "contact-form.css", "contact-detail.css", "dm-profile-form.css", "contact-categories.css", "contact-categories.mobile.css", "member-cards.css", "characters.css", "characters.mobile.css", "contacts.css", "contacts.mobile.css"];

    private static readonly (string Label, Regex Pattern)[] InlineCodePatterns =
    [
        ("a style attribute", new Regex(@"\sstyle\s*=", RegexOptions.IgnoreCase)),
        ("a style element", new Regex(@"<style\b", RegexOptions.IgnoreCase)),
        ("an inline script element", new Regex(@"<script\b(?![^>]*\bsrc\s*=)[^>]*>", RegexOptions.IgnoreCase)),
        ("an inline event handler", new Regex(@"\son[a-z]+\s*=", RegexOptions.IgnoreCase)),
    ];

    // Every source file this work creates or edits, repository-relative, scanned for planning
    // references. This test file is left out because it has to name the shapes it forbids.
    private static readonly string[] PhaseSourceFiles =
    [
        "QuestBoard.Repository/QuestRepository.cs",
        "QuestBoard.Service/Views/Shared/_CharacterAvatar.cshtml",
        "QuestBoard.Service/ViewModels/Shared/CharacterAvatarViewModel.cs",
        "QuestBoard.Service/Views/Quest/Details.cshtml",
        "QuestBoard.Service/Views/Quest/Manage.cshtml",
        "QuestBoard.Service/Views/QuestLog/Details.cshtml",
        "QuestBoard.IntegrationTests/Tests/CharacterAvatarRenderTests.cs",
        "QuestBoard.UnitTests/Repository/QuestRepositoryPictureFlagTests.cs",
        "QuestBoard.Service/wwwroot/css/components.css",
        "QuestBoard.Service/Views/ContactCategoryManagement/Edit.cshtml",
        "QuestBoard.IntegrationTests/Controllers/ContactCategoryManagementControllerIntegrationTests.cs",
        "QuestBoard.Service/Views/Shared/_ImageCropDialog.cshtml",
        "QuestBoard.Service/Views/Shared/_Layout.cshtml",
        "QuestBoard.Service/wwwroot/js/image-crop.js",
        "QuestBoard.Service/wwwroot/css/image-crop.css",
        "QuestBoard.IntegrationTests/Tests/ImageCropDialogRenderTests.cs",
        "QuestBoard.Service/Views/Characters/Create.cshtml",
        "QuestBoard.Service/Views/Characters/Edit.cshtml",
        "QuestBoard.Service/Views/Characters/_CharacterClassRows.cshtml",
        "QuestBoard.Service/wwwroot/css/character-form.css",
        "QuestBoard.Service/wwwroot/js/character-form.js",
        "QuestBoard.IntegrationTests/Tests/CharacterFormRenderTests.cs",
        "QuestBoard.Service/wwwroot/js/contact-tags.js",
        "QuestBoard.Service/wwwroot/css/tagify-vendor.css",
        "QuestBoard.Service/Views/Contacts/Create.cshtml",
        "QuestBoard.Service/Views/Contacts/Edit.cshtml",
        "QuestBoard.Service/wwwroot/css/contact-form.css",
        "QuestBoard.Service/wwwroot/css/contacts.css",
        "QuestBoard.IntegrationTests/Controllers/ContactsFormMergedViewTests.cs",
        "QuestBoard.Service/Views/DungeonMaster/EditProfile.cshtml",
        "QuestBoard.Service/Views/Characters/Details.cshtml",
        "QuestBoard.Service/wwwroot/css/character-detail.css",
        "QuestBoard.Service/wwwroot/js/confirm-submit.js",
        "QuestBoard.IntegrationTests/Tests/CharacterDetailsRenderTests.cs",
        "QuestBoard.Service/Views/Contacts/Details.cshtml",
        "QuestBoard.Service/wwwroot/css/contact-detail.css",
        "QuestBoard.Service/wwwroot/js/contact-notes.js",
        "QuestBoard.IntegrationTests/Tests/ContactDetailsRenderTests.cs",
        "QuestBoard.Service/wwwroot/css/dm-profile-form.css",
        "QuestBoard.IntegrationTests/Tests/DmEditProfileRenderTests.cs",
        "QuestBoard.IntegrationTests/Mobile/MobileViewsTests.cs",
        "QuestBoard.IntegrationTests/Tests/ContactCategoryMobileRenderTests.cs",
        "QuestBoard.Service/Views/ContactCategoryManagement/Manage.cshtml",
        "QuestBoard.Service/Views/ContactCategoryManagement/Manage.Mobile.cshtml",
        "QuestBoard.Service/wwwroot/css/contact-categories.css",
        "QuestBoard.Service/wwwroot/css/contact-categories.mobile.css",
        "QuestBoard.Service/wwwroot/css/contacts.mobile.css",
        "QuestBoard.IntegrationTests/Tests/CategoryManageRenderTests.cs",
        "QuestBoard.IntegrationTests/Tests/CharactersTwinAnswersTests.cs",
        "QuestBoard.IntegrationTests/Tests/ContactCategoryContrastGuardTests.cs",
        "QuestBoard.IntegrationTests/Controllers/ContactsTagsDesktopMarkupTests.cs",
        "QuestBoard.IntegrationTests/Tests/MarkdownEditorMarkupTests.cs",
        "QuestBoard.Service/Views/Characters/Index.cshtml",
        "QuestBoard.Service/wwwroot/css/member-cards.css",
        "QuestBoard.Service/wwwroot/css/characters.css",
        "QuestBoard.Service/wwwroot/css/characters.mobile.css",
        "QuestBoard.Service/Views/Characters/Index.Mobile.cshtml",
        "QuestBoard.IntegrationTests/Tests/CharactersIndexRenderTests.cs",
        "QuestBoard.IntegrationTests/Tests/UiFoundationLayerGuardTests.cs",
        "QuestBoard.Service/Views/Contacts/Index.cshtml",
        "QuestBoard.IntegrationTests/Tests/ContactsIndexRenderTests.cs",
        "QuestBoard.Service/Views/Contacts/Index.Mobile.cshtml",
        "QuestBoard.IntegrationTests/Mobile/ContactsTagsMobileTests.cs",
    ];

    // Character views that show a picture, repository-relative. Each asks for the picture only when one is stored.
    // The form and list views join this list as they are converted; a file not yet on disk is skipped.
    private static readonly string[] CharacterPictureViews =
    [
        "QuestBoard.Service/Views/Characters/Index.cshtml",
        "QuestBoard.Service/Views/Characters/Index.Mobile.cshtml",
        "QuestBoard.Service/Views/Characters/Details.cshtml",
        "QuestBoard.Service/Views/Characters/Create.cshtml",
        "QuestBoard.Service/Views/Characters/Edit.cshtml",
    ];

    // Written as regex data, each forbidden shape built from fragments, so this file never holds
    // the shapes it forbids elsewhere.
    private static readonly (string Label, Regex Pattern)[] PlanningReferencePatterns =
    [
        ("a decision id", new Regex(@"\bD" + @"-(?:0[1-9]|[1-9][0-9])\b")),
        ("a question id", new Regex(@"\b(?:O)?Q" + @"-\d{1,3}\b")),
        ("a merge or relayout id", new Regex(@"\bR" + @"-[1-9]\b")),
        ("a definition-of-converted id", new Regex(@"\bDo" + @"C-\d{1,2}\b")),
        ("an audit finding id", new Regex(@"\bA" + @"-(?:characters|quests|contacts|players)-\d{3}\b")),
        ("a threat id", new Regex(@"\bT" + @"-98-\d{2}\b")),
        ("a ledger number", new Regex("WIND" + "OWS\\s+(?:entry\\s+)?#?\\d+", RegexOptions.IgnoreCase)),
        ("a phase reference", new Regex("Pha" + "se\\s+9" + "8\\b", RegexOptions.IgnoreCase)),
        ("a plan id", new Regex(@"\b9" + @"8-(?:\d\d|[A-Z]{2,})")),
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

    // A view builds a character picture address by asking the characters controller for the cropped picture.
    private static int PictureUrlBuilds(string view) =>
        Regex.Matches(view, @"""GetCroppedPicture""").Count;

    private static string StripRazorAndHtmlComments(string view)
    {
        var withoutRazor = Regex.Replace(view, @"@\*[\s\S]*?\*@", " ");
        return Regex.Replace(withoutRazor, @"<!--[\s\S]*?-->", " ");
    }

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

    private static IEnumerable<string> ViewsUnder(string folder) =>
        Directory.EnumerateFiles(ResolveRepoFile(ViewsRoot, folder), "*.cshtml", SearchOption.AllDirectories);

    // ---- Guards ---------------------------------------------------------------------------------

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
        var planning = "// see " + "D" + "-04 and " + "Q" + "-51, " + "Pha" + "se 98" + ", 98" + "-05 in " + "RESE" + "ARCH.md, "
            + "R" + "-2, " + "Do" + "C-8, " + "A" + "-characters-012, " + "T" + "-98-11, " + "WIND" + "OWS #31";
        PlanningReferencePatterns.Count(p => p.Pattern.IsMatch(planning)).Should().Be(PlanningReferencePatterns.Length);
        PlanningReferencePatterns.Count(p => p.Pattern.IsMatch("// the placeholder shows when a character has no picture"))
            .Should().Be(0);

        PictureUrlBuilds("<img src=\"@Url.Action(\"GetCroppedPicture\", \"Characters\", new { id = 1 })\" />")
            .Should().Be(1);
        PictureUrlBuilds("@await Html.PartialAsync(\"_CharacterAvatar\", new CharacterAvatarViewModel())")
            .Should().Be(0);
        PictureUrlBuilds("<img src=\"@Url.Action(\"GetProfilePicture\", \"Characters\")\" />").Should().Be(0);
    }

    [Fact]
    public void QuestViews_RequestCharacterPicturesOnlyThroughTheAvatarPartial()
    {
        var offenders = ViewsUnder("Quest").Concat(ViewsUnder("QuestLog"))
            .Where(path => PictureUrlBuilds(File.ReadAllText(path)) > 0)
            .Select(path => Path.GetRelativePath(FindRepositoryRoot().FullName, path).Replace('\\', '/'))
            .ToList();
        offenders.Should().BeEmpty(
            "a quest page asks for a character picture only through the shared avatar partial, which knows whether one exists");

        PictureUrlBuilds(ReadRepoFile(AvatarPartial)).Should().Be(1,
            "the avatar partial builds the picture address in exactly one place");
    }

    [Fact]
    public void ConvertedViews_CarryNoInlineStyleScriptOrHandler()
    {
        ConvertedViews.Should().NotBeEmpty("the guard must cover at least one converted view");
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
        ViewSheetLinks.Keys.Should().BeEquivalentTo(ConvertedViews,
            "every converted view states which sheets it links, an empty set meaning the shared components only");

        foreach (var (path, sheets) in ViewSheetLinks)
        {
            var view = StripRazorAndHtmlComments(ReadRepoFile(path));
            foreach (var sheet in sheets)
                LinksSheet(view, sheet).Should().BeTrue(
                    $"{path} must link {sheet} from its Styles section so the sheet loads only where it is used");

            Regex.Matches(StylesSection(view), @"href=""~/css/([^""]+)""").Select(m => m.Groups[1].Value)
                .Should().BeEquivalentTo(sheets, $"{path} links exactly the sheets listed for it");
        }
    }

    [Fact]
    public void ConvertedSheets_AreNamedInThePagesLayer()
    {
        // The list starts empty and grows with the page sheets this work converts; each one opens the pages layer.
        foreach (var name in ConvertedSheets)
            StripCssCommentsFor(ReadRepoFile("QuestBoard.Service/wwwroot/css/" + name)).Should().Contain("@layer pages",
                $"{name} must open the pages layer so the foundation's cascade order applies to it");
    }

    [Fact]
    public void CropDialogPartial_UsesTheOverlaySurface()
    {
        // The dialog sits on the shared opaque overlay surface, so it names no colour of its own: no dark
        // background or light text utility, no white close-button variant and no style attribute.
        var partial = StripRazorAndHtmlComments(ReadRepoFile(CropDialogPartial));
        foreach (var forbidden in new[] { "bg-dark", "text-light", "btn-close-white" })
            partial.Should().NotContain(forbidden, $"the crop dialog must not use {forbidden}");
        Regex.Matches(partial, @"\sstyle\s*=", RegexOptions.IgnoreCase).Select(m => m.Value).Should().BeEmpty(
            "the crop dialog carries no style attribute");
    }

    [Fact]
    public void ImageCropModule_DrivesTheSelectionThroughItsPublicApi()
    {
        var script = ReadRepoFile(CropModule);

        // The selection is moved and resized only through Cropper's own methods.
        foreach (var method in new[] { "$moveTo", "$change", "$initSelection" })
            script.Should().Contain(method + "(", $"the module drives the selection through {method}");

        // Keys are heard on the selection element only, and Cropper's document-wide keyboard handling stays off
        // (it would let Delete remove the selection).
        Regex.Matches(script, @"\b(?:document|window|globalThis)\s*\.\s*addEventListener\(\s*['""`]key")
            .Select(m => m.Value).Should().BeEmpty("the module holds no document-level key listener");
        Regex.Matches(script, @"\bkeyboard\s*[:=]|setAttribute\(\s*['""]keyboard['""]|\.keyboard\b")
            .Select(m => m.Value).Should().BeEmpty("the module never switches on Cropper's keyboard option");

        // Text goes in through textContent; no markup is built from strings.
        Regex.Matches(script, @"\.(?:innerHTML|outerHTML)\b|insertAdjacentHTML|document\.write")
            .Select(m => m.Value).Should().BeEmpty("the module must build no markup from strings");

        // Nothing is exposed globally.
        Regex.Matches(script, @"\b(?:window|globalThis)\s*(?:\.\s*\w+|\[[^\]]*\])\s*=(?!=)")
            .Select(m => m.Value).Should().BeEmpty("the module defines no global");
    }

    [Fact]
    public void CropModuleScanners_FlagWhatTheyForbid()
    {
        // A known-bad sample shows each pattern above fires, so the module facts cannot pass on a dead regex.
        var bad = "document.addEventListener('keydown', f); cropper.keyboard = true; el.innerHTML = x; window.initCrop = f;";
        Regex.IsMatch(bad, @"\b(?:document|window|globalThis)\s*\.\s*addEventListener\(\s*['""`]key").Should().BeTrue();
        Regex.IsMatch(bad, @"\bkeyboard\s*[:=]|\.keyboard\b").Should().BeTrue();
        Regex.IsMatch(bad, @"\.(?:innerHTML|outerHTML)\b").Should().BeTrue();
        Regex.IsMatch(bad, @"\b(?:window|globalThis)\s*(?:\.\s*\w+|\[[^\]]*\])\s*=(?!=)").Should().BeTrue();
        Regex.IsMatch("selection.addEventListener('keydown', f); window.setTimeout(write, 5);",
            @"\b(?:document|window|globalThis)\s*\.\s*addEventListener\(\s*['""`]key|\b(?:window|globalThis)\s*(?:\.\s*\w+|\[[^\]]*\])\s*=(?!=)")
            .Should().BeFalse();
    }

    [Fact]
    public void Layouts_LinkNoAreaSheet()
    {
        // These sheets load from the Styles section of the pages that use them, never for every page.
        foreach (var layout in new[]
                 {
                     "QuestBoard.Service/Views/Shared/_Layout.cshtml",
                     "QuestBoard.Service/Views/Shared/_Layout.Mobile.cshtml",
                 })
        {
            var text = StripRazorAndHtmlComments(ReadRepoFile(layout));
            foreach (var sheet in PageOnlySheets)
                text.Should().NotContain("css/" + sheet,
                    $"{layout} must not link {sheet}: it is page-specific and belongs in the Styles section of its pages");
        }
    }

    [Fact]
    public void ConfirmSubmitModule_WritesNoMarkupFromStrings()
    {
        // The module hands a server-rendered sentence to confirm() as plain text and builds no markup.
        var script = ReadRepoFile("QuestBoard.Service/wwwroot/js/confirm-submit.js");
        Regex.Matches(script, @"\.(?:innerHTML|outerHTML)\s*=|insertAdjacentHTML|document\.write")
            .Select(m => m.Value).Should().BeEmpty("confirm-submit.js must build no markup from strings");
        Regex.Matches(script, @"\b(?:window|globalThis)\s*(?:\.\s*\w+|\[[^\]]*\])\s*=(?!=)")
            .Select(m => m.Value).Should().BeEmpty("confirm-submit.js defines no global");
    }

    [Fact]
    public void ContactTagsModule_ReadsItsDataFromTheInput()
    {
        // The suggestion list reaches the module as an encoded attribute on the tag input, parsed as
        // JSON; the module builds no markup from strings and defines no global.
        var script = ReadRepoFile("QuestBoard.Service/wwwroot/js/contact-tags.js");
        script.Should().Contain("data-whitelist", "the module reads the suggestion list from the input's attribute");
        script.Should().Contain("JSON.parse", "the attribute holds JSON");
        script.Should().Contain("removeAttribute('data-whitelist')",
            "the library reads an attribute of that name itself and would split the JSON on its commas");
        Regex.Matches(script, @"innerHTML|outerHTML|insertAdjacentHTML|document\.write")
            .Select(m => m.Value).Should().BeEmpty("contact-tags.js must build no markup from strings");
        Regex.Matches(script, @"\b(?:window|globalThis)\s*(?:\.\s*\w+|\[[^\]]*\])\s*=(?!=)")
            .Select(m => m.Value).Should().BeEmpty("contact-tags.js defines no global");

        foreach (var view in new[] { "Create", "Edit" })
        {
            var text = ReadRepoFile($"QuestBoard.Service/Views/Contacts/{view}.cshtml");
            text.Should().NotContain("Html.Raw", $"{view} must not print the suggestion list as raw output");
            text.Should().NotContain("initContactTags", $"{view} must not call a global initialiser");
        }
    }

    [Fact]
    public void TagifyTheme_LivesInTheContactFormSheetAsCustomProperties()
    {
        // The tag box is themed through the library's own custom properties from tokens, in the one contact form
        // sheet; the list sheet no longer holds any rule for the library's elements.
        var form = StripCssCommentsFor(ReadRepoFile("QuestBoard.Service/wwwroot/css/contact-form.css"));
        foreach (var property in new[] { "--tag-bg", "--tag-text-color", "--tags-border-color", "--tags-focus-border-color", "--input-color" })
            form.Should().Contain(property + ":", $"contact-form.css sets the library's {property} from a token");
        form.Should().Contain(":focus-within", "the tag box shows the shared two-tone focus ring while any part of it has focus");
        Regex.Matches(form, @"!important").Should().BeEmpty("contact-form.css holds no important flag");

        var list = StripCssCommentsFor(ReadRepoFile("QuestBoard.Service/wwwroot/css/contacts.css"));
        list.Should().NotContain("tagify", "contacts.css no longer carries rules for the tag entry library");
        Regex.Matches(list, @"!important").Should().BeEmpty("the tag overrides were the only important flags in contacts.css");
    }

    [Fact]
    public void ContactDetailSheet_HasNoBlurOnSlips()
    {
        // A note is a slip on the Notes card's scrim, not a card of its own, so it takes no backdrop blur however
        // many repeat; the sheet holds no blur at all outside a value of none.
        var css = StripCssCommentsFor(ReadRepoFile("QuestBoard.Service/wwwroot/css/contact-detail.css"));
        Regex.Matches(css, @"backdrop-filter\s*:\s*(?!none\b)[^;]+")
            .Select(m => m.Value).Should().BeEmpty("contact-detail.css blurs nothing behind a note slip");
        Regex.Matches(css, @"\bfilter\s*:\s*[^;]*blur").Select(m => m.Value).Should().BeEmpty("no slip is blurred");
        Regex.Matches(css, @"!important").Should().BeEmpty("contact-detail.css holds no important flag");
        css.Should().Contain("--qb-surface-scrim", "a slip sits on the shared scrim token");
    }

    [Fact]
    public void CharacterFormModule_WritesNoMarkupFromStrings()
    {
        // A class row is a clone of the row template the server rendered, with only its name, id and for values
        // rewritten; the module builds no markup from strings and defines no global.
        var script = ReadRepoFile("QuestBoard.Service/wwwroot/js/character-form.js");
        script.Should().Contain("cloneNode", "a new row is a clone of the server-rendered template");
        script.Should().Contain("class-row-template", "the module finds the template by its id");
        Regex.Matches(script, @"innerHTML|outerHTML|insertAdjacentHTML|document\.write|createContextualFragment|\beval\s*\(|new\s+Function\b")
            .Select(m => m.Value).Should().BeEmpty("character-form.js must build no markup from strings");
        Regex.Matches(script, @"\b(?:window|globalThis)\s*(?:\.\s*\w+|\[[^\]]*\])\s*=(?!=)")
            .Select(m => m.Value).Should().BeEmpty("character-form.js defines no global");
        Regex.Matches(script, @"\.style\b|setAttribute\(\s*['""]style['""]").Select(m => m.Value).Should().BeEmpty(
            "the module sets no inline style");

        foreach (var view in new[] { "Create", "Edit" })
        {
            var text = StripRazorAndHtmlComments(ReadRepoFile($"QuestBoard.Service/Views/Characters/{view}.cshtml"));
            Regex.Matches(text, @"<script\b(?![^>]*\bsrc\s*=)[^>]*>").Select(m => m.Value).Should().BeEmpty(
                $"{view} carries no inline class-row script");
        }
    }

    [Fact]
    public void ContactNotesModule_WritesNoMarkupFromStrings()
    {
        // The note edit toggle only changes the hidden attribute and moves focus; it reads no text into markup,
        // builds none from strings and defines no global of its own.
        var script = ReadRepoFile("QuestBoard.Service/wwwroot/js/contact-notes.js");
        Regex.Matches(script, @"innerHTML|outerHTML|insertAdjacentHTML|document\.write|createContextualFragment|\beval\s*\(|new\s+Function\b")
            .Select(m => m.Value).Should().BeEmpty("contact-notes.js must build no markup from strings");
        Regex.Matches(script, @"\b(?:window|globalThis)\s*(?:\.\s*\w+|\[[^\]]*\])\s*=(?!=)")
            .Select(m => m.Value).Should().BeEmpty("contact-notes.js defines no global");
        script.Should().Contain(".hidden", "the toggle works through the hidden attribute");
        Regex.Matches(script, @"\.style\b|style\.display").Select(m => m.Value).Should().BeEmpty("the toggle sets no inline style");
    }

    [Fact]
    public void CharacterDetailSheet_HasNoAnimation()
    {
        // The Main badge is a still ornament and nothing on the page moves.
        var css = StripCssCommentsFor(ReadRepoFile("QuestBoard.Service/wwwroot/css/character-detail.css"));
        Regex.Matches(css, @"@keyframes|\banimation(?:-[a-z]+)?\s*:|\btransition(?:-[a-z]+)?\s*:")
            .Select(m => m.Value).Should().BeEmpty("character-detail.css holds no animation, keyframes or transition");
    }

    // The matching closing position of the bracket opened at the given index, or -1.
    private static int MatchingClose(string text, int open, char openChar, char closeChar)
    {
        var depth = 0;
        for (var i = open; i < text.Length; i++)
        {
            if (text[i] == openChar)
                depth++;
            else if (text[i] == closeChar && --depth == 0)
                return i;
        }

        return -1;
    }

    // The body spans of every @if block whose condition names HasProfilePicture.
    private static List<(int Start, int End)> HasPictureBlocks(string view)
    {
        var blocks = new List<(int, int)>();
        foreach (Match start in Regex.Matches(view, @"@if\s*\("))
        {
            var openParen = start.Index + start.Length - 1;
            var closeParen = MatchingClose(view, openParen, '(', ')');
            if (closeParen < 0 || !view[openParen..closeParen].Contains("HasProfilePicture", StringComparison.Ordinal))
                continue;

            var openBrace = view.IndexOf('{', closeParen);
            if (openBrace < 0)
                continue;

            var closeBrace = MatchingClose(view, openBrace, '{', '}');
            if (closeBrace > openBrace)
                blocks.Add((openBrace, closeBrace));
        }

        return blocks;
    }

    // Picture addresses a view builds outside every block that is conditioned on the has-picture value.
    private static List<string> PictureUrlsOutsideHasPictureBlocks(string view)
    {
        if (view.Contains("_CharacterAvatar", StringComparison.Ordinal))
            return [];

        var blocks = HasPictureBlocks(view);
        return Regex.Matches(view, @"""(?:GetProfilePicture|GetCroppedPicture)""")
            .Where(m => !blocks.Any(b => m.Index > b.Start && m.Index < b.End))
            .Select(m => m.Value)
            .ToList();
    }

    [Fact]
    public void CharacterViews_RequestPicturesOnlyBehindTheHasPictureValue()
    {
        CharacterPictureViews.Should().NotBeEmpty();
        foreach (var path in CharacterPictureViews.Where(p => File.Exists(ResolveRepoFile(p.Split('/')))))
        {
            var view = StripRazorAndHtmlComments(ReadRepoFile(path));
            Regex.Matches(view, @"\sonerror\s*=", RegexOptions.IgnoreCase).Select(m => m.Value).Should().BeEmpty(
                $"{path} must not hide a failed picture with an error handler; it renders the placeholder when none is stored");
            PictureUrlsOutsideHasPictureBlocks(view).Should().BeEmpty(
                $"{path} builds a picture address only inside a block conditioned on HasProfilePicture");
        }
    }

    [Fact]
    public void PictureScanner_FlagsAnAddressOutsideTheBlockAndAllowsOneInside()
    {
        // A known-bad sample shows the scanner fires, so the fact above cannot pass on a dead pattern.
        var inside = "@if (Model.HasProfilePicture) { <img src=\"@Url.Action(\"GetProfilePicture\", new { id = 1 })\" /> } else { <i></i> }";
        var outside = "<img src=\"@Url.Action(\"GetProfilePicture\", new { id = 1 })\" />";
        var wrongBlock = "@if (Model.CanEdit) { <img src=\"@Url.Action(\"GetCroppedPicture\")\" /> }";
        PictureUrlsOutsideHasPictureBlocks(inside).Should().BeEmpty();
        PictureUrlsOutsideHasPictureBlocks(outside).Should().HaveCount(1);
        PictureUrlsOutsideHasPictureBlocks(wrongBlock).Should().HaveCount(1);
        PictureUrlsOutsideHasPictureBlocks("@await Html.PartialAsync(\"_CharacterAvatar\", m)").Should().BeEmpty();
    }

    // Every page sheet of the characters, contacts, Dungeon Master profile and players areas, by file name; one that is
    // not on disk is skipped.
    private static readonly string[] AreaSheets =
    [
        "characters.css", "characters.mobile.css", "member-cards.css", "contacts.css", "contacts.mobile.css",
        "dm-profile.css", "dm-profile.mobile.css", "players.mobile.css",
    ];

    [Fact]
    public void AreaSheets_HaveNoInfiniteAnimation()
    {
        // Nothing in the area may animate without end. Every area sheet, converted or not, is scanned for an endless
        // iteration; a converted sheet also holds no keyframes block at all.
        var converted = ConvertedSheets.ToHashSet();
        var names = AreaSheets.Concat(ConvertedSheets).Distinct().ToList();
        var scanned = 0;
        foreach (var name in names)
        {
            var path = ResolveRepoFile("QuestBoard.Service", "wwwroot", "css", name);
            if (!File.Exists(path))
                continue;

            scanned++;
            var css = StripCssCommentsFor(File.ReadAllText(path));
            Regex.Matches(css, @"\binfinite\b").Select(m => m.Value).Should().BeEmpty(
                $"{name} must not repeat an animation without end, by iteration count or in the shorthand");
            if (converted.Contains(name))
                Regex.Matches(css, @"@(?:-webkit-)?keyframes").Select(m => m.Value).Should().BeEmpty(
                    $"{name} is converted and holds no keyframes block");
        }

        scanned.Should().BeGreaterThan(5, "the guard must cover the area's sheets");
    }

    [Fact]
    public void AnimationScanner_FlagsWhatItForbidsAndAllowsWhatItPermits()
    {
        Regex.IsMatch("animation: pulse 2s ease-in-out infinite;", @"\binfinite\b").Should().BeTrue();
        Regex.IsMatch("animation-iteration-count: infinite;", @"\binfinite\b").Should().BeTrue();
        Regex.IsMatch("@keyframes pulse { to { opacity: 1; } }", @"@(?:-webkit-)?keyframes").Should().BeTrue();
        Regex.IsMatch("transition: transform var(--qb-dur-base) var(--qb-ease);", @"\binfinite\b|@(?:-webkit-)?keyframes")
            .Should().BeFalse();
    }

    [Fact]
    public void RetiredFiles_AreGone()
    {
        RetiredFiles.Should().NotBeEmpty();
        foreach (var path in RetiredFiles)
            File.Exists(ResolveRepoFile(path.Split('/'))).Should().BeFalse(
                $"{path} was merged into the view that serves both layouts and must not come back");
    }

    private static string StripCssCommentsFor(string css) => Regex.Replace(css, @"/\*[\s\S]*?\*/", " ");
}
