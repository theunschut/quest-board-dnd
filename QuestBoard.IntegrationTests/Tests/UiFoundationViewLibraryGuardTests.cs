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

        partial.Should().Contain("~/lib/easymde-2.21.0/easymde.min.css",
            because: "the quest form must load the editor stylesheet from the versioned wwwroot/lib directory");
        partial.Should().Contain("~/lib/easymde-2.21.0/easymde.min.js",
            because: "the quest form must load the editor script from the versioned wwwroot/lib directory");
        partial.Should().NotContain(JsDelivrHost,
            because: "the quest form must not request the editor from a CDN");
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
