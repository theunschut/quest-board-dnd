using System.Text.RegularExpressions;

namespace QuestBoard.UnitTests.Architecture;

// AmbientClockSeamTests asks "do these listed files still avoid the banned shape". This test
// asks the stronger, inverted question: "is the banned shape confined to these listed files,
// anywhere in production source". IgnoreQueryFilters() bypasses every tenant query filter in
// the application, so the set of files allowed to call it must be a closed, explicit list that
// fails the build the moment a new call site appears anywhere else -- rather than something a
// future contributor could copy-paste into a repository because it made a query work.
public class CrossBoardIgnoreQueryFiltersSeamTests
{
    // The complete set of places in production code allowed to read past the board filters.
    // Adding a sixth is a deliberate act that has to be argued for here, not a line someone
    // copy-pastes into a repository because it made a query work.
    private static readonly string[] AllowedCallSites =
    [
        "QuestBoard.Repository/EventSignupRepository.cs",
        "QuestBoard.Repository/EventRepository.cs",
        "QuestBoard.Repository/GroupRepository.cs",
        "QuestBoard.Repository/QuestRepository.cs",
        "QuestBoard.Repository/CrossBoardLinkRepository.cs",
    ];

    // Seeding and asserting against rows on a board the test client is not currently on is the
    // ordinary, correct way to write a test in this codebase, so the two test projects are
    // deliberately excluded from the scan. Including them would turn the allowlist into a list
    // of test files that grows with every new test and stops being a guardrail.
    private static readonly string[] ScannedProductionRoots =
    [
        "QuestBoard.Repository",
        "QuestBoard.Domain",
        "QuestBoard.Service",
    ];

    private const string BannedCallShape = "IgnoreQueryFilters";

    /// <summary>
    /// Resolves the repository root by walking up from AppContext.BaseDirectory until a
    /// directory containing a "QuestBoard.Service" subdirectory is found -- the same upward-walk
    /// idea AmbientClockSeamTests uses to resolve a single file, adapted here to resolve a
    /// project directory instead.
    /// </summary>
    private static DirectoryInfo ResolveRepoRootDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "QuestBoard.Service");
            if (Directory.Exists(candidate))
                return dir;

            dir = dir.Parent;
        }

        var attemptedBase = AppContext.BaseDirectory;
        throw new DirectoryNotFoundException(
            $"Could not resolve the repository root. Searched upward from '{attemptedBase}' for a " +
            "directory containing a 'QuestBoard.Service' subdirectory.");
    }

    // Strips every comment form production source can carry, so a plain-language comment that
    // happens to discuss the bypass can never fail the confinement fact, and a real call can
    // never hide inside one: C#-style line comments (//), C#-style block comments (/* ... */),
    // and Razor comment blocks (@* ... *@). Both block forms are removed in full, across lines,
    // before the line-based scan below runs, since a real call site can never span either form.
    private static string StripComments(string source)
    {
        // Razor blocks are stripped before C-style blocks so a design note's own prose (which
        // may itself mention "/* like this */" as an example) cannot confuse the C-style
        // stripper into removing a wider span than the Razor comment actually covered.
        var withoutRazorBlocks = Regex.Replace(source, @"@\*.*?\*@", string.Empty, RegexOptions.Singleline);
        var withoutBlockComments = Regex.Replace(withoutRazorBlocks, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);

        var lines = withoutBlockComments.Replace("\r\n", "\n").Split('\n');
        var kept = new List<string>(lines.Length);
        foreach (var line in lines)
        {
            var trimmed = line.TrimStart();

            // A leftover block-comment continuation line -- e.g. " * some prose" -- whose
            // opener/closer were already stripped above by the block-comment regex, kept only
            // as a defensive second pass for a line whose sole content is a continuation marker.
            if (trimmed.StartsWith('*'))
                continue;

            var lineCommentIndex = line.IndexOf("//", StringComparison.Ordinal);
            kept.Add(lineCommentIndex >= 0 ? line[..lineCommentIndex] : line);
        }

        return string.Join('\n', kept);
    }

    // Enumerates every *.cs file under the scanned production roots, skipping any path segment
    // named "obj" or "bin" so build output never pollutes the scan, and returns each file's
    // path relative to the repository root using forward slashes -- matching the style
    // AllowedCallSites is written in.
    private static IReadOnlyList<string> EnumerateProductionSourceFiles(DirectoryInfo repoRoot)
    {
        var files = new List<string>();

        foreach (var scannedRoot in ScannedProductionRoots)
        {
            var rootPath = Path.Combine(repoRoot.FullName, scannedRoot);
            if (!Directory.Exists(rootPath))
                continue;

            foreach (var filePath in Directory.EnumerateFiles(rootPath, "*.cs", SearchOption.AllDirectories))
            {
                var segments = filePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (segments.Any(segment => segment is "obj" or "bin"))
                    continue;

                var relativePath = Path.GetRelativePath(repoRoot.FullName, filePath)
                    .Replace(Path.DirectorySeparatorChar, '/');
                files.Add(relativePath);
            }
        }

        return files;
    }

    // The confinement fact: enumerate every production .cs file under the three scanned roots,
    // strip comments, and collect every file that still contains the banned call shape. The
    // resulting set must equal the allowlist exactly -- not a subset, not a superset -- so an
    // addition anywhere else in production code fails here, and a stale entry that no longer
    // needs the bypass is caught too.
    [Fact]
    public void IgnoreQueryFilters_IsConfinedToTheAllowlist()
    {
        var repoRoot = ResolveRepoRootDirectory();
        var offendingFiles = new List<string>();

        foreach (var relativePath in EnumerateProductionSourceFiles(repoRoot))
        {
            var fullPath = Path.Combine(repoRoot.FullName, relativePath.Replace('/', Path.DirectorySeparatorChar));
            var stripped = StripComments(File.ReadAllText(fullPath));

            if (stripped.Contains(BannedCallShape, StringComparison.Ordinal))
                offendingFiles.Add(relativePath);
        }

        offendingFiles.Should().BeEquivalentTo(AllowedCallSites,
            because: "IgnoreQueryFilters() bypasses every tenant query filter in the application, so it " +
                     "may only appear in the five explicitly allowlisted files -- a new call site anywhere " +
                     "else must be added to AllowedCallSites deliberately, with a why-comment, not copied " +
                     "in because it made a query work. Offending or missing files are named above by the " +
                     "equivalence assertion.");
    }

    // The allowlist-is-live fact: a stale entry for a file that no longer needs the bypass is
    // itself a defect, because it silently re-permits the call there even though nothing
    // exercises it anymore.
    [Theory]
    [MemberData(nameof(AllowedCallSitesData))]
    public void AllowlistedFile_ExistsAndActuallyContainsTheCall(string relativePath)
    {
        var repoRoot = ResolveRepoRootDirectory();
        var fullPath = Path.Combine(repoRoot.FullName, relativePath.Replace('/', Path.DirectorySeparatorChar));

        File.Exists(fullPath).Should().BeTrue(
            because: $"'{relativePath}' is listed in the allowlist and must exist on disk");

        var stripped = StripComments(File.ReadAllText(fullPath));
        stripped.Should().Contain(BannedCallShape,
            because: $"'{relativePath}' is listed in the allowlist because it genuinely calls " +
                     $"{BannedCallShape}() -- a stale entry would silently re-permit the call there");
    }

    public static IEnumerable<object[]> AllowedCallSitesData() =>
        AllowedCallSites.Select(path => new object[] { path });

    // The comment-immunity fact: StripComments applied to a small inline string containing the
    // banned call shape inside a "//" line comment, a "/* */" block, and a "@* *@" Razor block
    // must leave none of them -- so a file that merely discusses the bypass in prose can never
    // fail the confinement fact, and a real call can never hide inside a comment form.
    [Fact]
    public void StripComments_RemovesTheCallShapeFromEveryCommentForm()
    {
        const string source = """
            // A design note that mentions IgnoreQueryFilters in a line comment.
            var a = 1;
            /* A block comment mentioning IgnoreQueryFilters across
               multiple lines. */
            var b = 2;
            @* A Razor comment block mentioning IgnoreQueryFilters. *@
            var c = 3;
            """;

        var stripped = StripComments(source);

        stripped.Should().NotContain(BannedCallShape,
            because: "a comment merely discussing the bypass, in any of the three comment forms this " +
                     "codebase uses, must never be mistaken for a real call site");
    }
}
