using System.Text.RegularExpressions;
using FluentAssertions;

namespace QuestBoard.IntegrationTests.Tests;

/// <summary>
/// Pins how the page-level views load their third-party front-end files. The markdown editor, the
/// tag input and the image cropper are served from versioned directories under wwwroot/lib, so a
/// form never needs a CDN to work and a release that loses one of those files fails here before it
/// fails in production. An integration suite reading rendered markup cannot tell whether a tag
/// would resolve to a committed file, so only a scan of the view sources can hold the line. The
/// layouts are covered by their own guard.
/// </summary>
public class UiFoundationViewLibraryGuardTests
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

    private const string LibRoot = "QuestBoard.Service/wwwroot/lib";

    // The host is built from fragments so this file never holds the string it forbids elsewhere.
    private static readonly string JsDelivrHost = "cdn.jsde" + "livr.net";

    private static readonly Regex LibUrl = new(@"~/lib/[^""'\s)?#]+", RegexOptions.Compiled);

    // Every view source under the two view roots, except the layouts, which have their own guard.
    private static IEnumerable<string> NonLayoutViews()
    {
        string[][] roots =
        [
            ["QuestBoard.Service", "Views"],
            ["QuestBoard.Service", "Areas"],
        ];

        foreach (var root in roots)
        {
            var folder = ResolveRepoFile(root);
            if (!Directory.Exists(folder))
                continue;

            foreach (var file in Directory.EnumerateFiles(folder, "*.cshtml", SearchOption.AllDirectories))
            {
                if (!Path.GetFileName(file).StartsWith("_Layout", StringComparison.Ordinal))
                    yield return file;
            }
        }
    }

    [Fact]
    public void QuestFormPartial_LoadsEditorFromLib()
    {
        var partial = File.ReadAllText(
            ResolveRepoFile("QuestBoard.Service", "Views", "Quest", "_QuestFormScripts.cshtml"));

        partial.Should().Contain("~/css/easymde-vendor.css",
            because: "the editor stylesheet reaches the page through the wrapper that imports it into the vendor layer");
        partial.Should().NotContain("~/lib/easymde-2.21.0/easymde.min.css",
            because: "linking the library stylesheet directly would leave it unlayered, where it outranks every layered rule");
        partial.Should().Contain("~/lib/easymde-2.21.0/easymde.min.js",
            because: "the quest form must load the editor script from the versioned wwwroot/lib directory");
        partial.Should().NotContain(JsDelivrHost,
            because: "the quest form must not request the editor from a CDN");

        var wrapper = File.ReadAllText(
            ResolveRepoFile("QuestBoard.Service", "wwwroot", "css", "easymde-vendor.css"));
        wrapper.Should().Contain("../lib/easymde-2.21.0/easymde.min.css\") layer(vendor)",
            because: "the wrapper must import the versioned wwwroot/lib stylesheet into the vendor layer");
        wrapper.Should().NotContain(JsDelivrHost,
            because: "the editor stylesheet must not be requested from a CDN");
        File.Exists(ResolveRepoFile("QuestBoard.Service", "wwwroot", "lib", "easymde-2.21.0", "easymde.min.css"))
            .Should().BeTrue(because: "the wrapper imports a committed file");
    }

    [Fact]
    public void TagifyVendorWrapper_ImportsTheVendoredSheetIntoTheVendorLayer()
    {
        var wrapper = File.ReadAllText(
            ResolveRepoFile("QuestBoard.Service", "wwwroot", "css", "tagify-vendor.css"));
        wrapper.Should().Contain("../lib/tagify-4.38.0/tagify.css\") layer(vendor)",
            because: "the wrapper must import the versioned wwwroot/lib stylesheet into the vendor layer");
        wrapper.Should().NotContain(JsDelivrHost,
            because: "the tag stylesheet must not be requested from a CDN");
        File.Exists(ResolveRepoFile("QuestBoard.Service", "wwwroot", "lib", "tagify-4.38.0", "tagify.css"))
            .Should().BeTrue(because: "the wrapper imports a committed file");

        foreach (var view in new[] { "Create.cshtml", "Create.Mobile.cshtml", "Edit.cshtml", "Edit.Mobile.cshtml" })
        {
            var text = File.ReadAllText(ResolveRepoFile("QuestBoard.Service", "Views", "Contacts", view));
            text.Should().Contain("~/css/tagify-vendor.css",
                because: $"{view} must load the tag stylesheet through the wrapper");
            text.Should().NotContain("~/lib/tagify-4.38.0/tagify.css",
                because: $"{view} linking the library stylesheet directly would leave it unlayered");
        }
    }

    [Fact]
    public void Views_LoadNoLibraryFromJsDelivr()
    {
        foreach (var file in NonLayoutViews())
        {
            File.ReadAllText(file).Should().NotContain(JsDelivrHost,
                because: $"{Path.GetFileName(file)} must load its libraries from wwwroot/lib, not from a CDN");
        }
    }

    [Fact]
    public void ViewLibraryReferences_ResolveToCommittedFiles()
    {
        var libRoot = ResolveRepoFile(LibRoot.Split('/'));

        foreach (var file in NonLayoutViews())
        {
            foreach (Match match in LibUrl.Matches(File.ReadAllText(file)))
            {
                var relative = match.Value["~/lib/".Length..];
                File.Exists(Path.Combine(libRoot, relative.Replace('/', Path.DirectorySeparatorChar)))
                    .Should().BeTrue(
                        because: $"{Path.GetFileName(file)} references {match.Value}, which must exist under {LibRoot}");
            }
        }
    }
}
