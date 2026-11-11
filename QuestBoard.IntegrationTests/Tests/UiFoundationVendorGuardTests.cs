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

    private static string ReadAppCss() =>
        File.ReadAllText(ResolveRepoFile("QuestBoard.Service", "wwwroot", "css", "app.css"));

    [Fact]
    public void Layouts_LoadBootstrapFromAVersionedLibDirectory()
    {
        // The stylesheet is imported into the vendor layer by app.css, so the layouts carry only the
        // behaviour bundle; both come from a versioned directory under wwwroot/lib.
        ReadAppCss().Should().MatchRegex(
            @"@import\s+url\(""\.\./lib/bootstrap-\d+\.\d+\.\d+/css/bootstrap\.min\.css""\)\s+layer\(vendor\)",
            because: "app.css must import the Bootstrap stylesheet from a versioned wwwroot/lib directory");

        foreach (var layout in LayoutFiles)
        {
            var text = ReadLayout(layout);

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
        ReadAppCss().Should().MatchRegex(
            @"@import\s+url\(""\.\./lib/fontawesome-\d+\.\d+\.\d+/css/all\.min\.css""\)\s+layer\(vendor\)",
            because: "app.css must import Font Awesome from a versioned wwwroot/lib directory");

        foreach (var layout in LayoutFiles)
        {
            ReadLayout(layout).Should().NotContain(FontAwesomeCdnHost,
                because: $"{layout} must not request the Font Awesome stylesheet from a CDN");
        }
    }

    [Fact]
    public void LibDirectories_HoldOneBootstrap5AndOneFontAwesome6()
    {
        // Both libraries stay inside their current major version: a new major changes the markup and
        // the variable names the stylesheets map onto, so it is a migration, not a bump.
        var names = Directory.EnumerateDirectories(ResolveRepoFile(LibRoot.Split('/')))
            .Select(directory => Path.GetFileName(directory)!)
            .ToList();

        names.Where(name => Regex.IsMatch(name, @"^bootstrap-")).Should().ContainSingle()
            .Which.Should().MatchRegex(@"^bootstrap-5\.3\.\d+$",
                because: "Bootstrap stays on the 5.3 line, and an older directory must be removed when the version moves");
        names.Where(name => Regex.IsMatch(name, @"^fontawesome-")).Should().ContainSingle()
            .Which.Should().MatchRegex(@"^fontawesome-6\.\d+\.\d+$",
                because: "Font Awesome stays on version 6, and an older directory must be removed when the version moves");
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
    public void AppCss_DeclaresTheLayerOrderFirst()
    {
        var appCss = File.ReadAllText(ResolveRepoFile("QuestBoard.Service", "wwwroot", "css", "app.css"));

        var withoutComments = Regex.Replace(appCss, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline).TrimStart();
        var firstStatement = withoutComments[..(withoutComments.IndexOf(';') + 1)];

        firstStatement.Should().Be("@layer vendor, tokens, base, components, pages, utilities;",
            because: "the cascade layer order is fixed by the first place a layer name is seen, so the " +
                     "statement that declares it must come before any rule in the first stylesheet");
    }

    [Fact]
    public void Layouts_ReferenceNoThirdPartyHost()
    {
        var tag = new Regex(@"<(?:link|script)\b[^>]*>", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        foreach (var layout in LayoutFiles)
        {
            foreach (Match match in tag.Matches(ReadLayout(layout)))
            {
                match.Value.Should().NotMatchRegex(@"https?://",
                    because: $"{layout} must not request a stylesheet or script from another host: {match.Value}");
                match.Value.Should().NotContainEquivalentOf("integrity=",
                    because: $"{layout} serves its files from this origin, so no tag carries a subresource integrity attribute: {match.Value}");
            }
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
        File.Exists(appCss).Should().BeTrue(
            because: "app.css is the stylesheet that names the vendored files, so a missing one must fail rather than skip the check");

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
    public void FontAwesomeCss_WoffFontFilesAreCommitted()
    {
        // The vendored stylesheet names each font twice, as woff2 and as ttf; browsers that support woff2 never ask
        // for the ttf, and only the woff2 files are committed. The one omission is the legacy v4 compatibility
        // family, which no markup uses; adding a use of it means committing its files first.
        const string omittedFamily = "fa-v4compatibility";
        var css = ResolveRepoFile((LibRoot + "/fontawesome-6.7.2/css/all.min.css").Split('/'));
        File.Exists(css).Should().BeTrue(because: "the Font Awesome stylesheet is the file whose font references are checked");

        var fonts = Regex.Matches(File.ReadAllText(css), @"url\(\s*[""']?(\.\./webfonts/[^""')?#]+\.woff2)")
            .Select(m => m.Groups[1].Value)
            .Distinct()
            .ToList();
        fonts.Should().NotBeEmpty(because: "the stylesheet must name the woff2 fonts it draws icons from");

        foreach (var font in fonts.Where(f => !f.Contains(omittedFamily, StringComparison.Ordinal)))
        {
            var target = Path.GetFullPath(Path.Combine(
                Path.GetDirectoryName(css)!, font.Replace('/', Path.DirectorySeparatorChar)));
            File.Exists(target).Should().BeTrue(
                because: $"all.min.css references {font}, which must be committed next to the stylesheet");
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
