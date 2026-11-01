using System.Text.RegularExpressions;
using FluentAssertions;

namespace QuestBoard.IntegrationTests.Tests;

/// <summary>
/// Pins the structure of the desktop-side layouts and the desktop footer: a skip link first in the body, the
/// header, navigation and main landmarks with their names, a named menu toggler, no inline style or script, and
/// a footer link that has a name and cannot hand the opener a reference to this page. A rendered-markup suite
/// would have to log in under every role to reach each layout; a scan of the sources covers all three.
/// </summary>
public class UiFoundationDesktopShellGuardTests
{
    private const string FooterFile = "QuestBoard.Service/Views/Shared/_Footer.cshtml";

    // Each desktop-side layout, the name its navigation landmark must carry, and the id of the collapse its toggler
    // controls (null when the bar has no menu to collapse).
    private static readonly (string Path, string NavLabel, string? CollapseId)[] Layouts =
    [
        ("QuestBoard.Service/Views/Shared/_Layout.cshtml", "Main", "navbar"),
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

    // The opening tags of one element name, in document order.
    private static IEnumerable<string> OpeningTags(string markup, string name) =>
        Regex.Matches(markup, @"<" + name + @"\b[^>]*>").Select(m => m.Value);

    [Fact]
    public void DesktopLayouts_StartBodyWithSkipLink()
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
    public void DesktopLayouts_HaveLandmarks()
    {
        foreach (var (path, navLabel, _) in Layouts)
        {
            var markup = Markup(path);
            OpeningTags(markup, "header").Should().HaveCount(1, $"{path} must have one header landmark");

            var navs = OpeningTags(markup, "nav").ToList();
            navs.Should().HaveCount(1, $"{path} must have one navigation landmark");
            navs[0].Should().Contain($"aria-label=\"{navLabel}\"", $"{path} must name its navigation landmark");

            var mains = OpeningTags(markup, "main").ToList();
            mains.Should().HaveCount(1, $"{path} must have one main landmark");
            mains[0].Should().Contain("id=\"main-content\"", $"{path} main is the skip link target");
            mains[0].Should().Contain("tabindex=\"-1\"", $"{path} main must take focus when the skip link is followed");
        }
    }

    [Fact]
    public void DesktopLayout_NamesTheToggler()
    {
        foreach (var (path, _, collapseId) in Layouts)
        {
            var markup = Markup(path);
            var togglers = OpeningTags(markup, "button").Where(t => t.Contains("navbar-toggler")).ToList();

            if (collapseId == null)
            {
                togglers.Should().BeEmpty($"{path} has no collapsing menu");
                continue;
            }

            togglers.Should().HaveCount(1, $"{path} must have one menu toggler");
            togglers[0].Should().Contain("aria-label=\"Toggle navigation\"");
            togglers[0].Should().Contain($"aria-controls=\"{collapseId}\"");
            togglers[0].Should().Contain("aria-expanded=\"false\"");
            togglers[0].Should().Contain($"data-bs-target=\"#{collapseId}\"");
            markup.Should().Contain($"id=\"{collapseId}\"", "the toggler must control an element that exists");
        }
    }

    [Fact]
    public void DesktopLayouts_CarryNoInlineStyleOrScript()
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
    public void DesktopFooter_GitHubLinkIsNamedAndSafe()
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
    public void DesktopFooter_TextUnchanged()
    {
        Markup(FooterFile).Should().Contain("D&amp;D Quest Board v@(AppVersion.Current). Created by Theun Schut.");
    }
    [Fact]
    public void DesktopLayouts_WearTheShellClasses()
    {
        foreach (var (path, _, _) in Layouts)
        {
            var markup = Markup(path);
            OpeningTags(markup, "body").Single().Should().Contain("shell-fixed-nav",
                $"{path} has a fixed bar, so its body leaves room for it");

            var nav = OpeningTags(markup, "nav").Single();
            nav.Should().Contain("chrome", $"{path} bar takes the shell look from the chrome class");
            nav.Should().Contain("fixed-top", $"{path} bar is fixed to the top of the window");
            nav.Should().NotContain("bg-dark", $"{path} bar is drawn by the shell, not by a Bootstrap fill");
        }
    }

    [Fact]
    public void DesktopFooter_WearsTheChromeClass()
    {
        OpeningTags(Markup(FooterFile), "footer").Single().Should().Contain("chrome-footer");
    }
}
