using System.Text.RegularExpressions;
using FluentAssertions;

namespace QuestBoard.IntegrationTests.Tests;

/// <summary>
/// Pins how the five layouts load their third-party front-end files. Bootstrap is served from a
/// versioned directory under wwwroot/lib, so a browser never needs a CDN to draw the page, a
/// release that loses one of those files fails here before it fails in production, and every
/// vendored directory carries the licence its authors require. An integration suite reading
/// rendered markup cannot tell where a stylesheet came from, so only a scan of the layout sources
/// can hold the line.
/// </summary>
public class UiFoundationVendorGuardTests
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

    // The five layouts, as repository-relative paths.
    private static readonly string[] LayoutFiles =
    [
        "QuestBoard.Service/Views/Shared/_Layout.cshtml",
        "QuestBoard.Service/Views/Shared/_Layout.Mobile.cshtml",
        "QuestBoard.Service/Views/Shared/_Layout.GroupPicker.cshtml",
        "QuestBoard.Service/Areas/Platform/Views/Shared/_Layout.Platform.cshtml",
        "QuestBoard.Service/Areas/Platform/Views/Shared/_Layout.Platform.Mobile.cshtml",
    ];

    private const string LibRoot = "QuestBoard.Service/wwwroot/lib";

    // Hosts are built from fragments so this file never holds the strings it forbids elsewhere.
    private static readonly string BootstrapCdnHost = "cdn.jsde" + "livr.net/npm/boot" + "strap";

    private static readonly string FontAwesomeCdnHost = "cdnjs.cloud" + "flare.com/ajax/libs/font-" + "awesome/6.4.0/css/all";
    private static readonly string GoogleFontsApiHost = "fonts.goo" + "gleapis.com";
    private static readonly string GoogleFontsFileHost = "fonts.gst" + "atic.com";

    // Every stylesheet link, in document order, whichever order its attributes are written in.
    private static readonly Regex StylesheetLink = new(
        @"<link\b[^>]*\brel=""stylesheet""[^>]*>", RegexOptions.Compiled);

    private static readonly Regex LibUrl =new(@"~/lib/[^""'\s)?#]+", RegexOptions.Compiled);

    private static string ReadLayout(string relativePath) =>
        File.ReadAllText(ResolveRepoFile(relativePath.Split('/')));

    [Fact]
    public void Layouts_LoadBootstrapFromAVersionedLibDirectory()
    {
        foreach (var layout in LayoutFiles)
        {
            var text = ReadLayout(layout);

            text.Should().MatchRegex(
                @"<link[^>]*href=""~/lib/bootstrap-\d+\.\d+\.\d+/css/bootstrap\.min\.css""",
                because: $"{layout} must load the Bootstrap stylesheet from a versioned wwwroot/lib directory");
            text.Should().MatchRegex(
                @"<script[^>]*src=""~/lib/bootstrap-\d+\.\d+\.\d+/js/bootstrap\.bundle\.min\.js""",
                because: $"{layout} must load the Bootstrap bundle from a versioned wwwroot/lib directory");
            text.Should().NotContain(BootstrapCdnHost,
                because: $"{layout} must not request Bootstrap from a CDN");
        }
    }

    [Fact]
    public void Layouts_LoadFontAwesomeFromAVersionedLibDirectory()
    {
        foreach (var layout in LayoutFiles)
        {
            var text = ReadLayout(layout);

            text.Should().MatchRegex(
                @"<link[^>]*href=""~/lib/fontawesome-\d+\.\d+\.\d+/css/all\.min\.css""",
                because: $"{layout} must load Font Awesome from a versioned wwwroot/lib directory");
            text.Should().NotContain(FontAwesomeCdnHost,
                because: $"{layout} must not request the Font Awesome stylesheet from a CDN");
        }
    }

    [Fact]
    public void Layouts_LoadNoJQuery()
    {
        foreach (var layout in LayoutFiles)
        {
            ReadLayout(layout).Should().NotContainEquivalentOf("jquery",
                because: $"{layout} must not load jQuery, nothing in the application uses it");
        }
    }

    [Fact]
    public void Layouts_LoadTheShimLocally()
    {
        // The editor toolbar's preview icon is drawn as an outline only because the compatibility
        // stylesheet sets the regular weight on the old class names, so the two layouts that host
        // an editor keep it, served from the versioned directory rather than a third-party host.
        string[] layoutsWithEditor =
        [
            "QuestBoard.Service/Views/Shared/_Layout.cshtml",
            "QuestBoard.Service/Views/Shared/_Layout.Mobile.cshtml",
        ];

        foreach (var layout in LayoutFiles)
        {
            var text = ReadLayout(layout);

            text.Should().NotContain("cdnjs.cloud" + "flare.com/ajax/libs/font-" + "awesome",
                because: $"{layout} must not request any Font Awesome stylesheet from a CDN");

            if (layoutsWithEditor.Contains(layout))
            {
                text.Should().MatchRegex(
                    @"<link[^>]*href=""~/lib/fontawesome-\d+\.\d+\.\d+/css/v4-shims\.min\.css""",
                    because: $"{layout} must load the compatibility stylesheet from the versioned wwwroot/lib directory");
            }
        }
    }

    [Fact]
    public void Layouts_RequestNoWebFontFromGoogle()
    {
        foreach (var layout in LayoutFiles)
        {
            var text = ReadLayout(layout);

            text.Should().NotContain(GoogleFontsApiHost,
                because: $"{layout} must not request web fonts from Google, the display face is self-hosted");
            text.Should().NotContain(GoogleFontsFileHost,
                because: $"{layout} must not request font files from Google, the display face is self-hosted");
        }
    }

    [Fact]
    public void AppCss_IsTheFirstStylesheetOnEveryLayout()
    {
        foreach (var layout in LayoutFiles)
        {
            var links = StylesheetLink.Matches(ReadLayout(layout));

            links.Should().NotBeEmpty(because: $"{layout} loads stylesheets");
            links[0].Value.Should().Contain("~/css/app.css",
                because: $"{layout} must load wwwroot/css/app.css before every other stylesheet");
        }
    }

    [Fact]
    public void AppCss_DeclaresCinzelLocallyWithSwap()
    {
        var appCss = File.ReadAllText(ResolveRepoFile("QuestBoard.Service", "wwwroot", "css", "app.css"));

        var faces = Regex.Matches(appCss, @"@font-face\s*\{[^}]*\}");
        faces.Should().HaveCount(2, because: "app.css declares one Cinzel face per unicode subset, latin and latin-ext");

        foreach (Match face in faces)
        {
            face.Value.Should().Contain("font-family: 'Cinzel'",
                because: "app.css declares only the Cinzel display face");
            face.Value.Should().Contain("font-display: swap",
                because: "text must stay readable while the display face loads");
            face.Value.Should().Contain("unicode-range",
                because: "each subset file must cover only its own character range");
            face.Value.Should().MatchRegex(@"src:\s*url\(""\.\./lib/cinzel/[^""]+\.woff2""\)",
                because: "the display face must be served from wwwroot/lib/cinzel, not from a third party");
        }
    }

    [Fact]
    public void LibReferences_ResolveToCommittedFiles()
    {
        var libRoot = ResolveRepoFile(LibRoot.Split('/'));

        foreach (var layout in LayoutFiles)
        {
            foreach (Match match in LibUrl.Matches(ReadLayout(layout)))
            {
                var relative = match.Value["~/lib/".Length..];
                File.Exists(Path.Combine(libRoot, relative.Replace('/', Path.DirectorySeparatorChar)))
                    .Should().BeTrue(
                        because: $"{layout} references {match.Value}, which must exist under {LibRoot}");
            }
        }

        var appCss = ResolveRepoFile("QuestBoard.Service", "wwwroot", "css", "app.css");
        if (!File.Exists(appCss))
            return;

        var cssFolder = Path.GetDirectoryName(appCss)!;
        foreach (Match match in Regex.Matches(File.ReadAllText(appCss), @"url\(\s*[""']?(\.\./lib/[^""')?#]+)"))
        {
            var target = Path.GetFullPath(Path.Combine(
                cssFolder, match.Groups[1].Value.Replace('/', Path.DirectorySeparatorChar)));
            File.Exists(target).Should().BeTrue(
                because: $"wwwroot/css/app.css references {match.Groups[1].Value}, which must exist under {LibRoot}");
        }
    }

    [Fact]
    public void VendorDirectories_ShipTheirLicence()
    {
        var libRoot = ResolveRepoFile(LibRoot.Split('/'));
        Directory.Exists(libRoot).Should().BeTrue(because: $"{LibRoot} holds the vendored front-end files");

        string[] licenceNames = ["LICENSE", "LICENSE.txt", "LICENSE.md", "OFL.txt"];

        foreach (var directory in Directory.EnumerateDirectories(libRoot))
        {
            licenceNames.Any(name => File.Exists(Path.Combine(directory, name)))
                .Should().BeTrue(
                    because: $"{LibRoot}/{Path.GetFileName(directory)} must ship its licence file next to the vendored files");
        }
    }
}
