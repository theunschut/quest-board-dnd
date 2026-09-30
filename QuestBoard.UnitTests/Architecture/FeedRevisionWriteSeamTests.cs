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

    // One way of writing a row without the change tracker seeing the property-level change. The
    // sample is a line of source that must trip this shape and no other, so the self-check can
    // prove each detector still fires.
    private sealed record BannedShape(string Name, Regex Pattern, string Sample);

    private static BannedShape Literal(string text) =>
        new(text, new Regex(Regex.Escape(text)), $"var x = context.Set<Thing>(){text}other);");

    // Every shape that writes a row without the change tracker seeing the property-level change.
    // Marking a detached or freshly attached entity as modified is the same failure as attaching
    // it: its original values equal its current values, so no feed field looks changed and the
    // revision is never raised, even though every column is written. Reading an entry's state
    // is legitimate and is not banned; only assigning it is. Reaching an entity through Entry is
    // banned outright, because the only reason to do so in production code is to set its state
    // or its current values by hand.
    private static readonly BannedShape[] BannedWriteShapes =
    [
        Literal("ExecuteUpdate"),
        Literal("ExecuteSql"),
        Literal(".Attach("),
        Literal(".AttachRange("),
        Literal(".Update("),
        Literal(".UpdateRange("),
        Literal(".Entry("),
        Literal(".TrackGraph("),
        new("assigning an entity state", new Regex(@"\.State\s*=(?!=)"), "tracked.State = EntityState.Modified;"),
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

            kept.Add(StripLineComment(line));
        }

        return string.Join('\n', kept);
    }

    // Cuts a line at its first real line comment. A slash pair inside a string literal is text,
    // not a comment, so it must not hide the code after it. A quote that is misread only ever
    // keeps more text than necessary, which can make the scan stricter but never blind.
    private static string StripLineComment(string line)
    {
        var inString = false;
        for (var i = 0; i < line.Length - 1; i++)
        {
            if (line[i] == '"' && (i == 0 || line[i - 1] != '\\'))
            {
                inString = !inString;
            }
            else if (!inString && line[i] == '/' && line[i + 1] == '/')
            {
                return line[..i];
            }
        }

        return line;
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

    private static IReadOnlyList<string> FindBannedShapes(string source)
    {
        var stripped = StripComments(source);
        return BannedWriteShapes
            .Where(shape => shape.Pattern.IsMatch(stripped))
            .Select(shape => shape.Name)
            .ToList();
    }

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
            FindBannedShapes(shape.Sample).Should().Equal([shape.Name]);
            FindBannedShapes($"// discussing {shape.Sample} in prose\nvar y = 1;").Should().BeEmpty();
        }
    }

    // The shapes that mark a row as changed by hand, written the ways a contributor might spell them.
    [Theory]
    [InlineData("context.Entry(detached).State = EntityState.Modified;")]
    [InlineData("Context.Entry(model).State=EntityState.Modified;")]
    [InlineData("entry.State = EntityState.Modified;")]
    [InlineData("entry.State   =   state;")]
    [InlineData("context.Entry(entity).CurrentValues.SetValues(model);")]
    [InlineData("await this.DbContext.Entry(entity).ReloadAsync(token);")]
    [InlineData("context.ChangeTracker.TrackGraph(root, callback);")]
    public void TheDetector_FiresOnEveryWayOfMarkingARowChangedByHand(string source)
    {
        FindBannedShapes(source).Should().NotBeEmpty();
    }

    // The stamper reads entry state to decide what to do, and comparing is not assigning.
    [Theory]
    [InlineData("switch (entry.State) { case EntityState.Modified: break; }")]
    [InlineData("if (entry.State == EntityState.Modified) { }")]
    [InlineData("entry.State is EntityState.Modified or EntityState.Deleted")]
    [InlineData("foreach (var entry in tracker.Entries()) { }")]
    public void TheDetector_LeavesReadingEntityStateAlone(string source)
    {
        FindBannedShapes(source).Should().BeEmpty();
    }

    // A slash pair inside a string literal is not a comment, so a banned call later on the same
    // line must still be seen.
    [Fact]
    public void TheDetector_SeesABannedCallAfterAStringLiteralContainingTwoSlashes()
    {
        FindBannedShapes("var s = \"https://example.test\"; context.Entry(x).State = EntityState.Modified;")
            .Should().NotBeEmpty();
        FindBannedShapes("var s = \"https://example.test\"; // context.Entry(x).State = EntityState.Modified;")
            .Should().BeEmpty();
    }

    [Fact]
    public void TheContext_OverridesBothSaveMethods_AndRunsTheStamperInEach()
    {
        var repoRoot = ResolveRepoRootDirectory();
        var contextPath = Path.Combine(
            repoRoot.FullName, "QuestBoard.Repository", "Entities", "QuestBoardContext.cs");

        var source = StripComments(File.ReadAllText(contextPath));

        source.Should().Contain("override int SaveChanges(bool");
        Regex.IsMatch(source, @"override\s+(async\s+)?Task<int>\s+SaveChangesAsync\(").Should().BeTrue();
        source.Should().Contain("bool acceptAllChangesOnSuccess");
        Regex.Matches(source, @"FeedRevisionStamper\.Apply").Should().HaveCount(2,
            because: "the synchronous and the asynchronous save must each run the stamper before the base save");
    }
}
