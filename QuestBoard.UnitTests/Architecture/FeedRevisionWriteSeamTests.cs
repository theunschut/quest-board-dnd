using System.Text.RegularExpressions;

namespace QuestBoard.UnitTests.Architecture;

// The calendar feed revision is raised inside the context's own save methods by reading the
// change tracker, so a write that bypasses the change tracker would change what the feed shows
// without ever raising the revision -- a rescheduled entry would go out unchanged and a calendar
// client would keep its stale copy. This fails the build the moment such a write shape appears
// anywhere in production source, rather than relying on a contributor remembering the hole.
public class FeedRevisionWriteSeamTests
{
    // Files allowed to write past the change tracker. Deliberately empty: adding a file means
    // arguing that its writes cannot touch a column the calendar feed shows, or that the file
    // raises the revision itself.
    private static readonly string[] AllowedWriteSites = [];

    private static readonly string[] ScannedProductionRoots =
    [
        "QuestBoard.Repository",
        "QuestBoard.Domain",
        "QuestBoard.Service",
    ];

    // Migrations describe schema and data changes in SQL and are the one place a bulk write is
    // expected, so they are skipped.
    private const string MigrationsRoot = "QuestBoard.Repository/Migrations/";

    // Every shape that writes a row without the change tracker seeing the property-level change.
    private static readonly string[] BannedWriteShapes =
    [
        "ExecuteUpdate",
        "ExecuteSql",
        ".Attach(",
        ".AttachRange(",
        ".Update(",
        ".UpdateRange(",
    ];

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

        throw new DirectoryNotFoundException(
            $"Could not resolve the repository root. Searched upward from '{AppContext.BaseDirectory}' for a " +
            "directory containing a 'QuestBoard.Service' subdirectory.");
    }

    // Strips every comment form production source can carry, so prose that discusses a bypass
    // can never fail the scan and a real call can never hide inside a comment.
    private static string StripComments(string source)
    {
        var withoutRazorBlocks = Regex.Replace(source, @"@\*.*?\*@", string.Empty, RegexOptions.Singleline);
        var withoutBlockComments = Regex.Replace(withoutRazorBlocks, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);

        var lines = withoutBlockComments.Replace("\r\n", "\n").Split('\n');
        var kept = new List<string>(lines.Length);
        foreach (var line in lines)
        {
            if (line.TrimStart().StartsWith('*'))
                continue;

            var lineCommentIndex = line.IndexOf("//", StringComparison.Ordinal);
            kept.Add(lineCommentIndex >= 0 ? line[..lineCommentIndex] : line);
        }

        return string.Join('\n', kept);
    }

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

                if (relativePath.StartsWith(MigrationsRoot, StringComparison.Ordinal))
                    continue;

                files.Add(relativePath);
            }
        }

        return files;
    }

    private static IReadOnlyList<string> FindBannedShapes(string source) =>
        BannedWriteShapes
            .Where(shape => StripComments(source).Contains(shape, StringComparison.Ordinal))
            .ToList();

    [Fact]
    public void NoProductionSource_WritesRowsPastTheChangeTracker()
    {
        var repoRoot = ResolveRepoRootDirectory();
        var offenders = new List<string>();

        foreach (var relativePath in EnumerateProductionSourceFiles(repoRoot))
        {
            if (AllowedWriteSites.Contains(relativePath, StringComparer.Ordinal))
                continue;

            var fullPath = Path.Combine(repoRoot.FullName, relativePath.Replace('/', Path.DirectorySeparatorChar));
            var shapes = FindBannedShapes(File.ReadAllText(fullPath));
            if (shapes.Count > 0)
                offenders.Add($"{relativePath}: {string.Join(", ", shapes)}");
        }

        offenders.Should().BeEmpty(
            because: "a write that bypasses the change tracker changes what the calendar feed shows without " +
                     "raising the entry's revision, so a subscribed calendar would keep its stale copy");
    }

    // Keeps the scan from passing vacuously: it proves the scan really enumerates production
    // source, and that the detector fires on each banned shape and stays quiet inside comments.
    [Fact]
    public void TheScan_SeesProductionSource_AndTheDetectorFiresOnEveryBannedShape()
    {
        var repoRoot = ResolveRepoRootDirectory();
        EnumerateProductionSourceFiles(repoRoot)
            .Should().Contain("QuestBoard.Repository/Entities/QuestBoardContext.cs");

        foreach (var shape in BannedWriteShapes)
        {
            FindBannedShapes($"var x = context.Set<Thing>(){shape}other);").Should().Contain(shape);
            FindBannedShapes($"// discussing {shape} in prose\nvar y = 1;").Should().BeEmpty();
        }
    }

    [Fact]
    public void TheContext_OverridesBothSaveMethods_AndRunsTheStamperInEach()
    {
        var repoRoot = ResolveRepoRootDirectory();
        var contextPath = Path.Combine(
            repoRoot.FullName, "QuestBoard.Repository", "Entities", "QuestBoardContext.cs");

        var source = StripComments(File.ReadAllText(contextPath));

        source.Should().Contain("override int SaveChanges(bool");
        source.Should().Contain("override Task<int> SaveChangesAsync(");
        source.Should().Contain("bool acceptAllChangesOnSuccess");
        Regex.Matches(source, @"FeedRevisionStamper\.Apply").Should().HaveCount(2,
            because: "the synchronous and the asynchronous save must each run the stamper before the base save");
    }
}
