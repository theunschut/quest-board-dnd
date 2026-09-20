using System.Text.RegularExpressions;

namespace QuestBoard.UnitTests.Architecture;

// Enforces a closed, seven-file list at the source level -- the only thing that can stop a
// later phase from reintroducing an ambient clock read once the written classification behind
// today's migration has scrolled out of context. Reads production source files from disk with
// the same upward-walk resolver MobileCssTests uses, so it catches a regression even at a call
// site no behavioural test happens to exercise.
public class AmbientClockSeamTests
{
    // The closed, explicit list that makes this test enforceable rather than aspirational.
    // Adding an eighth ambient-clock call site anywhere else in the codebase is not caught by
    // this test -- it only re-checks that these seven paths never regress.
    private static readonly string[] GuardedRelativePaths =
    [
        "QuestBoard.Domain/Services/EventSeriesService.cs",
        "QuestBoard.Repository/GroupRepository.cs",
        "QuestBoard.Service/Controllers/QuestBoard/CalendarController.cs",
        "QuestBoard.Service/Controllers/Events/EventsController.cs",
        "QuestBoard.Service/Controllers/Events/SeriesController.cs",
        "QuestBoard.Service/Jobs/DailyReminderJob.cs",
        "QuestBoard.Service/Views/Series/Details.cshtml",
    ];

    // EventsController.cs carries one deliberate DateTime.UtcNow write -- SetCancelledAsync's
    // second parameter, a real instant recorded on cancellation, unrelated to the ambient
    // "what day is it" reads this test guards against. It is exempted by matching the call
    // site's own text, not by skipping the file entirely, so a second, unrelated
    // DateTime.UtcNow anywhere else in this file still fails this test.
    private const string EventsControllerRelativePath = "QuestBoard.Service/Controllers/Events/EventsController.cs";
    private const string EventsControllerExemptToken = "SetCancelledAsync";

    private const string EmailPreviewControllerRelativePath = "QuestBoard.Service/Controllers/Admin/EmailPreviewController.cs";

    /// <summary>
    /// Resolves a path relative to the repository root by walking up from
    /// AppContext.BaseDirectory until a directory named "QuestBoard.Service" is found -- the
    /// same upward-walk resolver MobileCssTests uses. Fails with a descriptive message naming
    /// the attempted path if the target file cannot be located.
    /// </summary>
    private static string ResolveRepoRelativePath(string relativePath)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        var normalizedRelativePath = relativePath.Replace('/', Path.DirectorySeparatorChar);

        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, normalizedRelativePath);
            if (File.Exists(candidate))
                return candidate;

            dir = dir.Parent;
        }

        var attemptedBase = AppContext.BaseDirectory;
        throw new FileNotFoundException(
            $"'{relativePath}' not found. Searched upward from '{attemptedBase}' for a repo root " +
            "containing a 'QuestBoard.Service' directory. Ensure the file exists at that path " +
            "relative to the repo root.",
            relativePath);
    }

    // Strips every comment form the seven guarded files can carry, so a plain-language comment
    // that happens to mention "DateTime.Today" can never fail the gate, and a real call can
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
            // as a defensive second pass for a line whose sole content is a continuation
            // marker.
            if (trimmed.StartsWith('*'))
                continue;

            var lineCommentIndex = line.IndexOf("//", StringComparison.Ordinal);
            kept.Add(lineCommentIndex >= 0 ? line[..lineCommentIndex] : line);
        }

        return string.Join('\n', kept);
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        var count = 0;
        var index = 0;
        while ((index = haystack.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }

        return count;
    }

    public static IEnumerable<object[]> GuardedPathsExceptEventsController()
    {
        foreach (var path in GuardedRelativePaths)
        {
            if (!string.Equals(path, EventsControllerRelativePath, StringComparison.Ordinal))
                yield return [path];
        }
    }

    // Covers six of the seven guarded paths with one straightforward rule: after stripping
    // comments, none of the three ambient-clock call shapes may appear anywhere in the file.
    // EventsController.cs needs its own fact below because of its one documented exception.
    [Theory]
    [MemberData(nameof(GuardedPathsExceptEventsController))]
    public void GuardedPath_ContainsNoAmbientClockRead(string relativePath)
    {
        var fullPath = ResolveRepoRelativePath(relativePath);
        var stripped = StripComments(File.ReadAllText(fullPath));

        var offenders = new List<string>();
        if (stripped.Contains("DateTime.Today", StringComparison.Ordinal)) offenders.Add("DateTime.Today");
        if (stripped.Contains("DateTime.Now", StringComparison.Ordinal)) offenders.Add("DateTime.Now");
        if (stripped.Contains("DateTime.UtcNow", StringComparison.Ordinal)) offenders.Add("DateTime.UtcNow");

        offenders.Should().BeEmpty(
            because: $"every clock read in '{relativePath}' must go through the board-clock seam " +
                     $"(IBoardClock) instead of reading the ambient system clock directly -- found: " +
                     string.Join(", ", offenders));
    }

    [Fact]
    public void EventsController_ContainsNoAmbientClockRead_ExceptTheDocumentedCancelledAtWrite()
    {
        var fullPath = ResolveRepoRelativePath(EventsControllerRelativePath);
        var lines = StripComments(File.ReadAllText(fullPath)).Split('\n');

        var offendingLines = new List<string>();
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var lineNumber = i + 1;

            if (line.Contains("DateTime.Today", StringComparison.Ordinal))
                offendingLines.Add($"line {lineNumber} (DateTime.Today): {line.Trim()}");

            if (line.Contains("DateTime.Now", StringComparison.Ordinal))
                offendingLines.Add($"line {lineNumber} (DateTime.Now): {line.Trim()}");

            if (line.Contains("DateTime.UtcNow", StringComparison.Ordinal)
                && !line.Contains(EventsControllerExemptToken, StringComparison.Ordinal))
            {
                offendingLines.Add($"line {lineNumber} (DateTime.UtcNow): {line.Trim()}");
            }
        }

        offendingLines.Should().BeEmpty(
            because: $"every clock read in '{EventsControllerRelativePath}' must go through the board-clock " +
                     "seam (IBoardClock) instead of reading the ambient system clock directly, except the " +
                     $"documented real-instant write inside {EventsControllerExemptToken} -- offenders: " +
                     string.Join("; ", offendingLines));
    }

    public static IEnumerable<object[]> GuardedCsPaths()
    {
        foreach (var path in GuardedRelativePaths)
        {
            if (path.EndsWith(".cs", StringComparison.Ordinal))
                yield return [path];
        }
    }

    // Positive assertion so this test cannot pass vacuously: a file that stopped resolving the
    // board's own zone at all (rather than reverting to one of the three literal ambient-read
    // shapes this test scans for) would still fail this one.
    [Theory]
    [MemberData(nameof(GuardedCsPaths))]
    public void GuardedCsPath_MentionsIBoardClock(string relativePath)
    {
        var fullPath = ResolveRepoRelativePath(relativePath);
        var content = File.ReadAllText(fullPath);

        content.Should().Contain("IBoardClock",
            because: $"'{relativePath}' is expected to resolve the board's own wall-clock zone through IBoardClock");
    }

    // A second positive assertion for the one guarded path that is a Razor view rather than a
    // C# file: it consumes the controller-filled Today/TodayLabel pair instead of mentioning
    // IBoardClock itself.
    [Fact]
    public void SeriesDetailsView_MentionsModelToday()
    {
        var fullPath = ResolveRepoRelativePath("QuestBoard.Service/Views/Series/Details.cshtml");
        var content = File.ReadAllText(fullPath);

        content.Should().Contain("Model.Today",
            because: "the view's own ambient DateTime.Today read was replaced by the controller filling " +
                     "SeriesDetailsViewModel.Today, which the view consumes instead of reading the clock itself");
    }

    // Documents the boundary rather than leaving it implicit: EmailPreviewController's five
    // DateTime.Today uses generate sample data for the admin preview page, are never compared
    // against a stored board-local date, and are deliberately outside the seam. This file is
    // not in the guarded list at all -- this fact pins the count so a future change either
    // keeps the exemption visible at this exact count or is caught here if the count moves.
    [Fact]
    public void EmailPreviewController_StillContainsFiveCosmeticDateTimeTodayUses()
    {
        var fullPath = ResolveRepoRelativePath(EmailPreviewControllerRelativePath);
        var stripped = StripComments(File.ReadAllText(fullPath));

        CountOccurrences(stripped, "DateTime.Today").Should().Be(5,
            because: "EmailPreviewController's DateTime.Today uses generate cosmetic sample data for the " +
                     "admin preview page, are never compared against a stored board-local date, and are " +
                     "deliberately outside the board-clock seam");
    }
}
