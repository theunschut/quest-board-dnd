using System.Text.RegularExpressions;

namespace QuestBoard.UnitTests.Architecture;

// Enforces a closed, explicit file list at the source level -- the only thing that can stop a
// later phase from reintroducing an ambient clock read once the written classification behind
// today's migration has scrolled out of context. Reads production source files from disk with
// the same upward-walk resolver MobileCssTests uses, so it catches a regression even at a call
// site no behavioural test happens to exercise.
public class AmbientClockSeamTests
{
    // The closed, explicit list that makes this test enforceable rather than aspirational.
    // Adding a call site anywhere outside this list is not caught by this test -- it only
    // re-checks that these paths never regress.
    private static readonly string[] GuardedRelativePaths =
    [
        "QuestBoard.Domain/Extensions/QuestExtensions.cs",
        "QuestBoard.Domain/Services/EventSeriesService.cs",
        "QuestBoard.Domain/Services/QuestService.cs",
        "QuestBoard.Repository/GroupRepository.cs",
        "QuestBoard.Repository/QuestRepository.cs",
        "QuestBoard.Service/Controllers/Admin/AdminController.cs",
        "QuestBoard.Service/Controllers/QuestBoard/CalendarController.cs",
        "QuestBoard.Service/Controllers/QuestBoard/QuestController.cs",
        "QuestBoard.Service/Controllers/QuestBoard/QuestLogController.cs",
        "QuestBoard.Service/Controllers/Events/EventsController.cs",
        "QuestBoard.Service/Controllers/Events/SeriesController.cs",
        "QuestBoard.Service/Jobs/DailyReminderJob.cs",
        "QuestBoard.Service/Views/Admin/Quests.cshtml",
        "QuestBoard.Service/Views/Admin/Quests.Mobile.cshtml",
        "QuestBoard.Service/Views/Quest/Details.cshtml",
        "QuestBoard.Service/Views/Quest/Details.Mobile.cshtml",
        "QuestBoard.Service/Views/Quest/Index.Mobile.cshtml",
        "QuestBoard.Service/Views/Quest/Manage.cshtml",
        "QuestBoard.Service/Views/Quest/_QuestCard.cshtml",
        "QuestBoard.Service/Views/Series/Details.cshtml",
    ];

    // A handful of guarded files carry a deliberate DateTime.UtcNow read of a *real instant* --
    // a moment actually being recorded or measured -- as opposed to the ambient "what day is
    // it" reads this test guards against. Each is exempted by matching the call site's own text
    // on the same line, not by skipping the file, so a second, unrelated DateTime.UtcNow
    // anywhere else in the same file still fails. The exemption never covers DateTime.Today or
    // DateTime.Now, which have no legitimate real-instant use.
    private static readonly Dictionary<string, string[]> RealInstantExemptions = new(StringComparer.Ordinal)
    {
        // The cancellation moment written by SetCancelledAsync's second parameter.
        ["QuestBoard.Service/Controllers/Events/EventsController.cs"] = ["SetCancelledAsync"],
        // The Resend stats window and the "as of" stamp shown beside the figures -- both real
        // instants measured against an external API's own timeline, never against a board date.
        ["QuestBoard.Service/Controllers/Admin/AdminController.cs"] = ["cutoff", "AsOf"],
        // The moment a player's signup was recorded.
        ["QuestBoard.Domain/Services/QuestService.cs"] = ["SignupTime"],
        // The moment a quest was closed.
        ["QuestBoard.Repository/QuestRepository.cs"] = ["ClosedDate"],
    };

    // The .cs files expected to resolve the board's own zone through the seam itself. Listed
    // explicitly rather than derived from the extension, because a guarded file may legitimately
    // take the board-local date as a parameter instead of injecting the clock -- QuestExtensions
    // is exactly that case, and must still never read a clock of its own.
    private static readonly string[] BoardClockConsumerPaths =
    [
        "QuestBoard.Domain/Services/EventSeriesService.cs",
        "QuestBoard.Domain/Services/QuestService.cs",
        "QuestBoard.Repository/GroupRepository.cs",
        "QuestBoard.Repository/QuestRepository.cs",
        "QuestBoard.Service/Controllers/Admin/AdminController.cs",
        "QuestBoard.Service/Controllers/QuestBoard/CalendarController.cs",
        "QuestBoard.Service/Controllers/QuestBoard/QuestController.cs",
        "QuestBoard.Service/Controllers/QuestBoard/QuestLogController.cs",
        "QuestBoard.Service/Controllers/Events/EventsController.cs",
        "QuestBoard.Service/Controllers/Events/SeriesController.cs",
        "QuestBoard.Service/Jobs/DailyReminderJob.cs",
    ];

    // The guarded Razor views, each paired with the token proving it consumes a board-local
    // today handed to it from outside rather than reading a clock of its own. Series/Details
    // takes it on its view model; the quest views take it off ViewBag, which is how those
    // domain-model-bound views already receive every other scalar the controller resolves.
    private static readonly Dictionary<string, string> RoutedTodayViewTokens = new(StringComparer.Ordinal)
    {
        ["QuestBoard.Service/Views/Series/Details.cshtml"] = "Model.Today",
        ["QuestBoard.Service/Views/Admin/Quests.cshtml"] = "ViewBag.BoardToday",
        ["QuestBoard.Service/Views/Admin/Quests.Mobile.cshtml"] = "ViewBag.BoardToday",
        ["QuestBoard.Service/Views/Quest/Details.cshtml"] = "ViewBag.BoardToday",
        ["QuestBoard.Service/Views/Quest/Details.Mobile.cshtml"] = "ViewBag.BoardToday",
        ["QuestBoard.Service/Views/Quest/Index.Mobile.cshtml"] = "ViewBag.BoardToday",
        ["QuestBoard.Service/Views/Quest/Manage.cshtml"] = "ViewBag.BoardToday",
        ["QuestBoard.Service/Views/Quest/_QuestCard.cshtml"] = "ViewBag.BoardToday",
    };

    private const string EmailPreviewControllerRelativePath = "QuestBoard.Service/Controllers/Admin/EmailPreviewController.cs";

    private static readonly string[] AmbientReadShapes = ["DateTime.Today", "DateTime.Now", "DateTime.UtcNow"];

    /// <summary>
    /// Resolves a path relative to the repository root by walking up from
    /// AppContext.BaseDirectory until the target file is found -- the same upward-walk resolver
    /// MobileCssTests uses. Fails with a descriptive message naming the attempted path if the
    /// target file cannot be located.
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

    // Strips every comment form the guarded files can carry, so a plain-language comment that
    // happens to mention "DateTime.Today" can never fail the gate, and a real call can never
    // hide inside one: C#-style line comments (//), C#-style block comments (/* ... */), and
    // Razor comment blocks (@* ... *@). Both block forms are removed in full, across lines,
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

    public static IEnumerable<object[]> GuardedPathsWithoutExemptions() =>
        GuardedRelativePaths
            .Where(path => !RealInstantExemptions.ContainsKey(path))
            .Select(path => new object[] { path });

    public static IEnumerable<object[]> GuardedPathsWithExemptions() =>
        GuardedRelativePaths
            .Where(RealInstantExemptions.ContainsKey)
            .Select(path => new object[] { path });

    public static IEnumerable<object[]> BoardClockConsumers() =>
        BoardClockConsumerPaths.Select(path => new object[] { path });

    public static IEnumerable<object[]> RoutedTodayViews() =>
        RoutedTodayViewTokens.Select(pair => new object[] { pair.Key, pair.Value });

    // Covers every guarded path that carries no exemption with one straightforward rule: after
    // stripping comments, none of the three ambient-clock call shapes may appear anywhere in
    // the file.
    [Theory]
    [MemberData(nameof(GuardedPathsWithoutExemptions))]
    public void GuardedPath_ContainsNoAmbientClockRead(string relativePath)
    {
        var fullPath = ResolveRepoRelativePath(relativePath);
        var stripped = StripComments(File.ReadAllText(fullPath));

        var offenders = AmbientReadShapes
            .Where(shape => stripped.Contains(shape, StringComparison.Ordinal))
            .ToList();

        offenders.Should().BeEmpty(
            because: $"every clock read in '{relativePath}' must go through the board-clock seam " +
                     $"(IBoardClock) instead of reading the ambient system clock directly -- found: " +
                     string.Join(", ", offenders));
    }

    // The same rule for the files carrying a documented real-instant read, checked line by line
    // so the exemption covers only the call site whose own text names it. DateTime.Today and
    // DateTime.Now stay banned outright in these files -- only DateTime.UtcNow can be exempted,
    // and only on a line naming the exempted call site.
    [Theory]
    [MemberData(nameof(GuardedPathsWithExemptions))]
    public void GuardedPathWithExemption_ContainsNoAmbientClockRead_ExceptItsDocumentedRealInstantWrites(string relativePath)
    {
        var fullPath = ResolveRepoRelativePath(relativePath);
        var exemptTokens = RealInstantExemptions[relativePath];
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
                && !exemptTokens.Any(token => line.Contains(token, StringComparison.Ordinal)))
            {
                offendingLines.Add($"line {lineNumber} (DateTime.UtcNow): {line.Trim()}");
            }
        }

        offendingLines.Should().BeEmpty(
            because: $"every clock read in '{relativePath}' must go through the board-clock seam " +
                     "(IBoardClock) instead of reading the ambient system clock directly, except the " +
                     $"documented real-instant writes named by [{string.Join(", ", exemptTokens)}] -- " +
                     "offenders: " + string.Join("; ", offendingLines));
    }

    // Positive assertion so this test cannot pass vacuously: a file that stopped resolving the
    // board's own zone at all (rather than reverting to one of the three literal ambient-read
    // shapes this test scans for) would still fail this one.
    [Theory]
    [MemberData(nameof(BoardClockConsumers))]
    public void BoardClockConsumer_MentionsIBoardClock(string relativePath)
    {
        var fullPath = ResolveRepoRelativePath(relativePath);
        var content = File.ReadAllText(fullPath);

        content.Should().Contain("IBoardClock",
            because: $"'{relativePath}' is expected to resolve the board's own wall-clock zone through IBoardClock");
    }

    // The same positive assertion for the guarded Razor views, which consume a board-local today
    // handed to them from outside instead of mentioning IBoardClock themselves.
    [Theory]
    [MemberData(nameof(RoutedTodayViews))]
    public void RoutedTodayView_ConsumesTheTodayItWasHanded(string relativePath, string expectedToken)
    {
        var fullPath = ResolveRepoRelativePath(relativePath);
        var content = File.ReadAllText(fullPath);

        content.Should().Contain(expectedToken,
            because: $"'{relativePath}' had its own ambient clock read replaced by a board-local today " +
                     $"resolved in the controller and routed into the view, which it reads as '{expectedToken}'");
    }

    // The shared rule itself must take the board-local date as a parameter rather than reaching
    // for a clock -- otherwise every call site that now delegates to it would be reading an
    // ambient clock again at one remove, with nothing in the guarded list to catch it.
    [Fact]
    public void QuestExtensions_TakesTheBoardLocalDateRatherThanResolvingOne()
    {
        var fullPath = ResolveRepoRelativePath("QuestBoard.Domain/Extensions/QuestExtensions.cs");
        var content = File.ReadAllText(fullPath);

        content.Should().Contain("DateOnly boardToday",
            because: "the shared 'has this game night passed' rule is handed the board-local today by its " +
                     "caller, so it holds no clock of its own to read");
        content.Should().NotContain("IBoardClock",
            because: "injecting the clock into the rule would hide a second clock resolution behind every " +
                     "call site that delegates to it");
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
