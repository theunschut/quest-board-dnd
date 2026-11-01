using System.Text.RegularExpressions;
using FluentAssertions;

namespace QuestBoard.IntegrationTests.Tests;

/// <summary>
/// Pins the structure of the phone layouts and the phone footer: a skip link first in the body, the header,
/// menu navigation and main landmarks with their names, a named menu button and close button, no inline style or
/// script, a footer link that has a name and cannot hand the opener a reference to this page, and the shell
/// stylesheet's 44px touch-target rule and lighter board photo. A rendered-markup suite would have to log in
/// under every role to reach each layout; a scan of the sources covers them all.
/// </summary>
public class UiFoundationMobileShellGuardTests
{
    private const string FooterFile = "QuestBoard.Service/Views/Shared/_Footer.Mobile.cshtml";
    private const string CssRoot = "QuestBoard.Service/wwwroot/css/";

    // Each phone layout, the name its menu landmark must carry, and the id of the offcanvas its button opens.
    private static readonly (string Path, string NavLabel, string PanelId)[] Layouts =
    [
        ("QuestBoard.Service/Views/Shared/_Layout.Mobile.cshtml", "Main menu", "mobileNav"),
        ("QuestBoard.Service/Areas/Platform/Views/Shared/_Layout.Platform.Mobile.cshtml", "Platform", "platformMobileNav"),
    ];

    private static string ReadRepoFile(string relativePath)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !dir.EnumerateFiles("*.slnx").Any() && !dir.EnumerateDirectories(".git").Any())
            dir = dir.Parent;

        dir.Should().NotBeNull("the repository root must be found walking up from the test binaries");
        return File.ReadAllText(Path.Combine(dir!.FullName, Path.Combine(relativePath.Split('/'))));
    }

    // The file without its Razor comments, which describe the markup in the same words it uses.
    private static string Markup(string relativePath) =>
        Regex.Replace(ReadRepoFile(relativePath), @"@\*[\s\S]*?\*@", " ");

    // The stylesheet without its comments, which describe the rules in the same words they use.
    private static string Css(string fileName) =>
        Regex.Replace(ReadRepoFile(CssRoot + fileName), @"/\*[\s\S]*?\*/", " ");

    // The opening tags of one element name, in document order.
    private static IEnumerable<string> OpeningTags(string markup, string name) =>
        Regex.Matches(markup, @"<" + name + @"\b[^>]*>").Select(m => m.Value);

    [Fact]
    public void MobileLayouts_StartBodyWithSkipLink()
    {
        foreach (var (path, _, _) in Layouts)
        {
            Regex.IsMatch(
                    Markup(path),
                    @"<body\b[^>]*>\s*<a\b[^>]*\bclass=""[^""]*\bskip-link\b[^""]*""[^>]*\bhref=""#main-content""[^>]*>Skip to main content</a>")
                .Should().BeTrue($"{path} must open its body with the skip link to #main-content");
        }
    }

    [Fact]
    public void MobileLayouts_HaveLandmarks()
    {
        foreach (var (path, navLabel, _) in Layouts)
        {
            var markup = Markup(path);
            OpeningTags(markup, "header").Should().HaveCount(1, $"{path} must have one header landmark");

            var navs = OpeningTags(markup, "nav").ToList();
            navs.Should().HaveCount(1, $"{path} must have one navigation landmark, the menu panel");
            navs[0].Should().Contain($"aria-label=\"{navLabel}\"", $"{path} must name its navigation landmark");
            navs[0].Should().Contain("offcanvas-body", $"{path} navigation landmark is the menu panel's scrolling body");

            var mains = OpeningTags(markup, "main").ToList();
            mains.Should().HaveCount(1, $"{path} must have one main landmark");
            mains[0].Should().Contain("id=\"main-content\"", $"{path} main is the skip link target");
            mains[0].Should().Contain("tabindex=\"-1\"", $"{path} main must take focus when the skip link is followed");
        }
    }

    [Fact]
    public void MobileLayouts_NameTheTogglerAndClose()
    {
        foreach (var (path, _, panelId) in Layouts)
        {
            var markup = Markup(path);
            var togglers = OpeningTags(markup, "button").Where(t => t.Contains("navbar-toggler")).ToList();
            togglers.Should().HaveCount(1, $"{path} must have one menu button");
            togglers[0].Should().Contain("aria-label=\"Open navigation menu\"");
            togglers[0].Should().Contain($"aria-controls=\"{panelId}\"");
            togglers[0].Should().Contain("aria-expanded=\"false\"");
            togglers[0].Should().Contain($"data-bs-target=\"#{panelId}\"");
            markup.Should().Contain($"id=\"{panelId}\"", "the button must control an element that exists");

            var closers = OpeningTags(markup, "button").Where(t => t.Contains("btn-close")).ToList();
            closers.Should().HaveCount(1, $"{path} must have one close button");
            closers[0].Should().Contain("aria-label=\"Close navigation menu\"");
        }
    }

    [Fact]
    public void MobileLayouts_CarryNoInlineStyleOrScript()
    {
        foreach (var path in Layouts.Select(l => l.Path).Append(FooterFile))
        {
            var markup = Markup(path);
            Regex.Matches(markup, @"\sstyle\s*=").Should().BeEmpty($"{path} must carry no inline style attribute");
            Regex.Matches(markup, @"<style\b").Should().BeEmpty($"{path} must carry no style block");
            Regex.Matches(markup, @"<script\b(?![^>]*\bsrc=)[^>]*>").Should().BeEmpty(
                $"{path} must carry no inline script: only scripts loaded from a file");
        }
    }

    [Fact]
    public void MobileLayouts_WearTheShellClasses()
    {
        foreach (var (path, _, _) in Layouts)
        {
            var markup = Markup(path);
            OpeningTags(markup, "body").Single().Should().Contain("mobile-layout",
                $"{path} body is what the phone shell rules are scoped to");

            var bar = OpeningTags(markup, "div").Single(t => Regex.IsMatch(t, @"class=""(?:[^""]*\s)?navbar(?:\s[^""]*)?"""));
            bar.Should().Contain("chrome", $"{path} bar takes the shell look from the chrome class");
            bar.Should().NotContain("bg-dark", $"{path} bar is drawn by the shell, not by a Bootstrap fill");
            bar.Should().NotContain("fixed-top", $"{path} bar scrolls away with the page");
            bar.Should().NotContain("sticky-top", $"{path} bar scrolls away with the page");
        }

        OpeningTags(Markup(FooterFile), "footer").Single().Should().Contain("chrome-footer");
    }

    [Fact]
    public void MobileShell_DeclaresTouchTargetRule()
    {
        var css = Css("shell.mobile.css");
        var rule = Regex.Match(css,
            @"\.mobile-layout\s*:where\(\s*\.btn,\s*\.nav-link,\s*\.dropdown-item,\s*\.page-link,\s*\.btn-close,\s*select,\s*input:not\(\[type=checkbox\]\):not\(\[type=radio\]\),\s*textarea\s*\)\s*\{[^}]*\}");
        rule.Success.Should().BeTrue("the touch-target rule covers the listed controls at zero specificity");
        rule.Value.Should().Contain("min-height: var(--qb-hit-min)");
        rule.Value.Should().Contain("min-width: var(--qb-hit-min)");

        ReadRepoFile(CssRoot + "tokens.css").Should().Contain("--qb-hit-min: 44px");

        var checkRow = Regex.Match(css, @"\.mobile-layout\s+\.form-check\s*\{[^}]*\}").Value;
        checkRow.Should().Contain("min-height: var(--qb-hit-min)", "a check row is at least 44px tall");
        var checkLabel = Regex.Match(css, @"\.mobile-layout\s+\.form-check\s+\.form-check-label\s*\{[^}]*\}").Value;
        checkLabel.Should().Contain("display: block", "the label covers the whole row");
        checkLabel.Should().Contain("min-height: var(--qb-hit-min)");
    }

    [Fact]
    public void MobileShell_NamesTheLighterPhotoOnTheRootAndRetiresTheOldBackdrop()
    {
        var shell = Css("shell.mobile.css");
        Regex.IsMatch(shell, @":root\s*\{\s*--qb-board-photo:\s*url\(""\.\./images/notice-board-900\.webp""\)\s*;?\s*\}")
            .Should().BeTrue("the phone shell names the 900px derivative on the root, so a theme on the html element still wins");
        shell.Should().NotContain("--qb-board-image",
            "the photo is named one step behind the image token, which a theme replaces whole");
        Regex.IsMatch(shell, @"(?<![\w-])body\s*\{[^}]*--qb-board").Should().BeFalse(
            "a value on the body would override a theme on the html element");

        var legacy = Css("mobile.css");
        legacy.Should().NotContain("body::before", "the backdrop is drawn once, in base.css");
        legacy.Should().NotContain("Notice Board", "the 3000px source is no longer requested by the phone layouts");
    }

    [Fact]
    public void MobileBackdropImage_IsASmallDerivativeOfTheSourceArtwork()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !dir.EnumerateFiles("*.slnx").Any() && !dir.EnumerateDirectories(".git").Any())
            dir = dir.Parent;

        var images = Path.Combine(dir!.FullName, "QuestBoard.Service", "wwwroot", "images");
        var derivative = new FileInfo(Path.Combine(images, "notice-board-900.webp"));
        derivative.Exists.Should().BeTrue();
        derivative.Length.Should().BeInRange(1, 100 * 1024, "a phone downloads this on every first page");
    }

    [Fact]
    public void MobileFooter_GitHubLinkIsNamedAndSafe()
    {
        var markup = Markup(FooterFile);
        var link = OpeningTags(markup, "a").Single(t => t.Contains("github.com"));
        link.Should().Contain("aria-label=\"Quest Board source on GitHub\"");
        link.Should().Contain("target=\"_blank\"");
        link.Should().Contain("rel=\"noopener noreferrer\"");
        OpeningTags(markup, "i").Should().Contain(t => t.Contains("fa-github") && t.Contains("aria-hidden=\"true\""),
            "the icon is decoration; the link's own name says what it is");
    }

    [Fact]
    public void MobileFooter_TextUnchanged()
    {
        Markup(FooterFile).Should().Contain("D&amp;D QB v@(AppVersion.Current).");
    }
}
