using System.Text.RegularExpressions;

namespace QuestBoard.IntegrationTests.Tests;

// Guards contracts a passing integration suite cannot see, in the spirit of the existing
// ContactCategoryContrastGuardTests: a server-side test reads rendered markup, not computed
// styles, and reads code, not conventions. Copy parity between the two Profile layouts, the
// destructive confirm text's byte-for-byte equality, the forbidden-claim list, the touch-target
// and truncation style rules actually reaching their elements, and the absence of any planning
// or tracking reference in shipped source are all defended here instead.
public class CalendarSubscriptionStaticGuardTests
{
    // Resolves a repo-relative path by first locating the repository root -- the one directory
    // holding the solution file -- and only then joining the requested segments onto it.
    //
    // The root is found first, rather than walking up until the joined path happens to exist,
    // because the test output directory contains build artifacts whose names collide with the
    // project directory names this guard scans. On Linux a project's apphost is an
    // extension-less binary, so a bare "does this path exist" probe inside bin/ matches that
    // binary and stops the walk on a *file* where a directory was meant -- resolving
    // "QuestBoard.Service" to the executable instead of the source folder. On Windows the same
    // apphost carries an .exe suffix and the collision never appears, which is exactly why
    // anchoring on an unambiguous marker matters rather than trusting first-match.
    private static string ResolveRepoFile(params string[] segments)
    {
        var joined = Path.Combine(segments);
        var root = FindRepositoryRoot();
        return Path.Combine(root.FullName, joined);
    }

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

    private static string ReadDesktopView() => File.ReadAllText(
        ResolveRepoFile("QuestBoard.Service", "Views", "Account", "Profile.cshtml"));

    private static string ReadMobileView() => File.ReadAllText(
        ResolveRepoFile("QuestBoard.Service", "Views", "Account", "Profile.Mobile.cshtml"));

    private static string ExtractCssRule(string css, string selectorWithOpenBrace)
    {
        var start = css.IndexOf(selectorWithOpenBrace, StringComparison.Ordinal);
        if (start < 0)
            return string.Empty;

        var end = css.IndexOf('}', start);
        return end < 0 ? string.Empty : css[start..(end + 1)];
    }

    // ---- Copy parity between the layouts ----

    // Every non-interpolated string from the Copywriting Contract, as data rather than as a
    // hand-copied fact per string -- adding a string to the contract means adding one entry here.
    public static IEnumerable<object[]> CopyContractStrings()
    {
        string[] strings =
        [
            "Calendar Subscription",
            "Add Subscription",
            "No calendar subscriptions yet",
            "Get this board's schedule onto your phone's own calendar app. The address below acts like a password",
            "Created ",
            "Never fetched yet",
            "Last fetched",
            "Copy",
            "Copied!",
            "Press Ctrl+C",
            "Open in Calendar App",
            "If nothing happens, copy the address and add it in your calendar app manually.",
            "Show QR Code",
            "Rename Subscription",
            "e.g. My Phone",
            "Delete",
            "? Any device using this address will silently stop receiving updates the next time it checks",
            "this cannot be undone. You can always create a new subscription afterward.",
            "Scan to Subscribe",
            "Point your phone's camera at this code to add ",
        ];

        foreach (var value in strings)
        {
            yield return [value];
        }
    }

    [Theory]
    [MemberData(nameof(CopyContractStrings))]
    public void CopyContractString_AppearsOnBothLayouts(string expected)
    {
        var desktop = ReadDesktopView();
        var mobile = ReadMobileView();

        desktop.Should().Contain(expected,
            because: $"the desktop Profile layout must carry the Copywriting Contract string '{expected}'");
        mobile.Should().Contain(expected,
            because: $"the mobile Profile layout must carry the same Copywriting Contract string '{expected}' -- " +
                     "this codebase has shipped a control on one Profile layout and not the other more than once");
    }

    // ---- The destructive confirm text is byte-identical ----

    [Fact]
    public void DeleteConfirmText_IsByteIdenticalBetweenLayouts()
    {
        var desktopConfirm = ExtractConfirmMessage(ReadDesktopView());
        var mobileConfirm = ExtractConfirmMessage(ReadMobileView());

        desktopConfirm.Should().NotBeNullOrEmpty("the desktop layout must define the delete confirm() message");
        mobileConfirm.Should().NotBeNullOrEmpty("the mobile layout must define the delete confirm() message");
        mobileConfirm.Should().Be(desktopConfirm,
            because: "two hand-maintained copies of a destructive confirmation sentence drift; this is what stops it");
    }

    private static string ExtractConfirmMessage(string source)
    {
        var match = Regex.Match(source, @"var message = 'Delete ""' \+ name \+ '(.*?)';");
        return match.Success ? match.Groups[1].Value : string.Empty;
    }

    // ---- No forbidden claim ----

    // Match case-insensitively on the specific shapes the copy contract forbids. Each is
    // forbidden for a distinct reason: the one-hour block is invented for rendering and the
    // board does not actually know how long an event runs; every refresh signal is chosen by the
    // reading calendar application and is undocumented by its vendor; and at least one major
    // client is reported not to honour the calendar-name property, so promising an automatic
    // rename would be a promise this feature cannot keep.
    //
    // The four-hour quest-session block is invented for exactly the same reason as the event's
    // one-hour block: nothing in the schema records when a session ends, and the block the feed
    // emits exists purely so the entry has a length on a phone. Copy that presents that invented
    // number as something the board knows would be telling a member a fact the board does not
    // have, for the same reason the equivalent claim about an event is already forbidden above.
    // No view ships this phase, so both layouts pass every case below today -- the cases exist to
    // stop a future surface from introducing the claim, not to fix one that exists.
    public static IEnumerable<object[]> ForbiddenClaims() =>
    [
        ["one-hour session"],
        ["one hour session"],
        ["real length"],
        ["actual duration"],
        ["how long the event lasts"],
        ["updates every"],
        ["syncs every"],
        ["syncs within"],
        ["refreshes every"],
        ["checks for updates every"],
        ["will be named"],
        ["automatically renamed"],
        ["renames your calendar"],
        ["names your calendar app"],
        ["4-hour session"],
        ["four-hour session"],
        ["session lasts 4 hours"],
        ["quest runs for 4 hours"],
        ["session ends after 4 hours"],
    ];

    [Theory]
    [MemberData(nameof(ForbiddenClaims))]
    public void NeitherLayout_MakesAForbiddenClaim(string forbiddenPhrase)
    {
        var desktop = ReadDesktopView();
        var mobile = ReadMobileView();

        desktop.Should().NotContainEquivalentOf(forbiddenPhrase,
            because: $"the desktop layout must never claim '{forbiddenPhrase}'");
        mobile.Should().NotContainEquivalentOf(forbiddenPhrase,
            because: $"the mobile layout must never claim '{forbiddenPhrase}'");
    }

    // ---- The mobile touch-target floor exists and reaches its element ----

    [Fact]
    public void MobileTouchTargetFloor_ExistsAndReachesTheIconButtons()
    {
        var cssPath = ResolveRepoFile("QuestBoard.Service", "wwwroot", "css", "account.mobile.css");
        var css = File.ReadAllText(cssPath);
        var rule = ExtractCssRule(css, ".calendar-subscription-icon-btn {");

        rule.Should().NotBeEmpty(
            because: $"account.mobile.css at '{cssPath}' must contain the .calendar-subscription-icon-btn rule");
        rule.Should().Contain("min-height: 44px");
        rule.Should().Contain("min-width: 44px");

        // A rule nothing selects is not a mitigation: confirm the mobile view actually applies
        // the class to its icon-only Copy and Show QR Code controls.
        var mobile = ReadMobileView();
        Regex.Matches(mobile, "calendar-subscription-icon-btn").Count.Should().BeGreaterThanOrEqualTo(2,
            because: "both the icon-only Copy control and the icon-only Show QR Code control must carry the touch-target class");
    }

    // ---- The truncation rule exists and reaches its element ----

    [Fact]
    public void RowLabelTruncationRule_ExistsAndReachesTheNameElement()
    {
        var cssPath = ResolveRepoFile("QuestBoard.Service", "wwwroot", "css", "account.mobile.css");
        var css = File.ReadAllText(cssPath);
        var rule = ExtractCssRule(css, ".calendar-subscription-row .calendar-subscription-name {");

        rule.Should().NotBeEmpty(
            because: $"account.mobile.css at '{cssPath}' must contain the scoped .calendar-subscription-row .calendar-subscription-name rule");
        rule.Should().Contain("text-overflow: ellipsis");

        var mobile = ReadMobileView();
        var rowStart = mobile.IndexOf("calendar-subscription-row", StringComparison.Ordinal);
        rowStart.Should().BeGreaterThan(-1, because: "the mobile layout must render a calendar-subscription-row element");
        var rowEnd = mobile.IndexOf("</div>", rowStart, StringComparison.Ordinal);
        var nameElementIndex = mobile.IndexOf("calendar-subscription-name", rowStart, StringComparison.Ordinal);
        nameElementIndex.Should().BeGreaterThan(-1,
            because: "the truncating class must actually reach the row's own name element, not just exist in the stylesheet");
    }

    // ---- The code sizing rule exists in both stylesheets ----

    [Fact]
    public void QrCodeSizingRule_ExistsInBothStylesheets()
    {
        var desktopCss = File.ReadAllText(ResolveRepoFile("QuestBoard.Service", "wwwroot", "css", "site.css"));
        var mobileCss = File.ReadAllText(ResolveRepoFile("QuestBoard.Service", "wwwroot", "css", "account.mobile.css"));

        var desktopRule = ExtractCssRule(desktopCss, ".calendar-subscription-qr svg {");
        var mobileRule = ExtractCssRule(mobileCss, ".calendar-subscription-qr svg {");

        desktopRule.Should().NotBeEmpty(because: "site.css must size the QR SVG for the desktop layout");
        desktopRule.Should().Contain("max-width: 240px");

        mobileRule.Should().NotBeEmpty(
            because: "account.mobile.css must independently size the QR SVG -- the two layouts load different stylesheets, " +
                     "so a rule present in only one would leave one layout's QR code unsized");
        mobileRule.Should().Contain("max-width: 240px");
    }

    // ---- No planning or tracking reference reached the source ----

    // These references go stale the moment a phase closes and become dead noise a future
    // cleanup phase has to hunt back out of the codebase -- this project has already run that
    // cleanup once. Written as regex data rather than as literals embedded in prose, per
    // CLAUDE.md's own rule against citing phase/requirement/review identifiers in source.
    private static readonly (string Label, Regex Pattern)[] PlanningReferencePatterns =
    [
        ("a requirement id of this phase's family", new Regex(@"CALFEED-\d+")),
        ("a phase reference", new Regex(@"Phase\s+84\b")),
        ("a plan reference", new Regex(@"\b84-0\d\b")),
        ("a review-finding reference", new Regex(@"\b\d+-REVIEW\b")),
    ];

    private static readonly string[] ProjectDirectoriesToScan =
    [
        "QuestBoard.Domain",
        "QuestBoard.Repository",
        "QuestBoard.Service",
        "QuestBoard.UnitTests",
        "QuestBoard.IntegrationTests",
    ];

    private static readonly string[] ScannedExtensions = [".cs", ".cshtml", ".css"];

    // This file's own search patterns above are, necessarily, the literal shapes this guard
    // forbids everywhere else -- excluded from its own walk so the guard does not fail on itself.
    private const string ThisFileName = "CalendarSubscriptionStaticGuardTests.cs";

    [Fact]
    public void NoPlanningOrTrackingReference_ReachedTheSourceTree()
    {
        var violations = new List<string>();

        foreach (var projectName in ProjectDirectoriesToScan)
        {
            var projectDir = ResolveRepoFile(projectName);
            var files = Directory.EnumerateFiles(projectDir, "*.*", SearchOption.AllDirectories)
                .Where(f => ScannedExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
                .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                .Where(f => !Path.GetFileName(f).Equals(ThisFileName, StringComparison.OrdinalIgnoreCase));

            foreach (var file in files)
            {
                var content = File.ReadAllText(file);
                foreach (var (label, pattern) in PlanningReferencePatterns)
                {
                    if (pattern.IsMatch(content))
                    {
                        violations.Add($"{file} carries {label}");
                    }
                }
            }
        }

        violations.Should().BeEmpty(
            because: "planning and tracking references go stale the moment a phase closes; offending files: " +
                      string.Join(", ", violations));
    }
}
