using System.Text.RegularExpressions;
using FluentAssertions;

namespace QuestBoard.IntegrationTests.Tests;

/// <summary>
/// Pins the events, series, calendar and agenda views as they are moved onto the layered
/// foundation: no view in the area formats a date itself (every date goes through the shared
/// date helpers), and none of the files this work touches names the planning that produced it.
/// The lists below only ever grow as each view is converted; a file a later change deletes is
/// removed from them in the same commit. An integration suite reading rendered markup cannot see
/// what a comment says or which expression produced a date, so only a scan of the sources can
/// hold the line.
/// </summary>
public class EventsConversionGuardTests
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

    private const string ViewsRoot = "QuestBoard.Service/Views/";

    // Every view of the area, relative to the views folder. A file that does not exist is skipped
    // so a view a later change deletes does not fail the scan before its entry is removed.
    private static readonly string[] AreaFiles =
    [
        "Calendar/Index.cshtml",
        "Calendar/Index.Mobile.cshtml",
        "Agenda/Index.cshtml",
        "Agenda/Index.Mobile.cshtml",
        "Events/Index.cshtml",
        "Events/Index.Mobile.cshtml",
        "Events/Create.cshtml",
        "Events/Details.cshtml",
        "Events/Edit.cshtml",
        "Series/Details.cshtml",
        "Shared/_Calendar.cshtml",
        "Shared/_Calendar.Mobile.cshtml",
        "Shared/_LinkedDayEntries.cshtml",
        "Shared/_LinkedAlsoThatDay.cshtml",
        "Events/_AvailabilityCell.cshtml",
        "Events/_AvailabilityCounts.cshtml",
        "Events/_SeriesFormScripts.cshtml",
    ];

    // The area files scanned for raw date formatting: all of them.
    private static readonly string[] DateCheckedFiles = AreaFiles;

    // Every source file this work creates or edits, repository-relative, scanned for planning
    // references. This test file is left out because it has to name the shapes it forbids.
    private static readonly string[] PhaseSourceFiles =
    [
        "QuestBoard.Service/Extensions/HtmlHelperExtensions.cs",
        "QuestBoard.Service/ViewModels/CalendarViewModels/QuestOnDay.cs",
        "QuestBoard.Service/Controllers/QuestBoard/CalendarController.cs",
        "QuestBoard.Service/Views/Calendar/Index.cshtml",
        "QuestBoard.Service/Views/Calendar/Index.Mobile.cshtml",
        "QuestBoard.Service/Views/Agenda/Index.cshtml",
        "QuestBoard.Service/Views/Agenda/Index.Mobile.cshtml",
        "QuestBoard.Service/Views/Events/Index.cshtml",
        "QuestBoard.Service/Views/Events/Index.Mobile.cshtml",
        "QuestBoard.Service/Views/Events/Details.cshtml",
        "QuestBoard.Service/Views/Series/Details.cshtml",
        "QuestBoard.Service/Views/Shared/_Calendar.cshtml",
        "QuestBoard.Service/Views/Shared/_Calendar.Mobile.cshtml",
        ".claude/ui-guidelines.md",
        "QuestBoard.UnitTests/Extensions/WallClockAreaStylesTests.cs",
        "QuestBoard.IntegrationTests/Tests/EventsDateRenderTests.cs",
        "QuestBoard.IntegrationTests/Controllers/CalendarBoardClockTests.cs",
    ];

    // A date-pattern format string handed to ToString, other than the machine values a date input
    // or an attribute reads back, which have to stay in the pattern they are parsed with.
    private static readonly Regex DateFormatCall = new(
        @"\.ToString\(\s*""(?<format>[^""]*(?:yy|MM|dd|HH|hh|mm|ss|tt)[^""]*)""",
        RegexOptions.Compiled);

    private static readonly string[] MachineValueFormats = ["yyyy-MM-dd", "yyyy-MM-ddTHH:mm"];

    private static List<string> RawDateFormats(string view) =>
        DateFormatCall.Matches(view)
            .Select(m => m.Groups["format"].Value)
            .Where(f => !MachineValueFormats.Contains(f))
            .ToList();

    // Written as regex data, each forbidden shape built from fragments, so this file never holds
    // the shapes it forbids elsewhere.
    private static readonly (string Label, Regex Pattern)[] PlanningReferencePatterns =
    [
        ("a decision id", new Regex(@"\bD" + @"-(?:0[1-9]|[1-9][0-9])\b")),
        ("a question id", new Regex(@"\bQ" + @"-\d{2,3}\b")),
        ("a sign-off item id", new Regex(@"\bG" + @"[12]-[A-Z]\d\b")),
        ("a definition-of-converted id", new Regex(@"\bDo" + @"C-\d{1,2}\b")),
        ("an audit finding id", new Regex(@"\bA" + @"-events-\d{3}\b")),
        ("a threat id", new Regex(@"\bT" + @"-97-\d{2}\b")),
        ("a phase reference", new Regex("Pha" + "se\\s+9" + "7\\b", RegexOptions.IgnoreCase)),
        ("a plan id", new Regex(@"\b9" + @"7-(?:\d\d|[A-Z]{2,})")),
        ("a planning document name", new Regex(
            "CONT" + "EXT\\.md|RESE" + "ARCH\\.md|UI-" + "SPEC|PATT" + "ERNS\\.md|VALID" + "ATION\\.md|AUD" + "IT\\.md")),
    ];

    private static string ReadRepoFile(string relativePath)
    {
        var path = ResolveRepoFile(relativePath.Split('/'));
        File.Exists(path).Should().BeTrue(
            $"{relativePath} is listed in this guard, so it must exist; remove it from the list in the commit that deletes it");
        return File.ReadAllText(path);
    }

    private static string StripRazorAndHtmlComments(string view)
    {
        var withoutRazor = Regex.Replace(view, @"@\*[\s\S]*?\*@", " ");
        return Regex.Replace(withoutRazor, @"<!--[\s\S]*?-->", " ");
    }

    // ---- Guards ---------------------------------------------------------------------------------

    [Fact]
    public void AreaViews_RenderNoRawDateFormatting()
    {
        var scanned = 0;
        foreach (var relative in DateCheckedFiles)
        {
            var path = ResolveRepoFile((ViewsRoot + relative).Split('/'));
            if (!File.Exists(path))
                continue;

            scanned++;
            RawDateFormats(StripRazorAndHtmlComments(File.ReadAllText(path))).Should().BeEmpty(
                $"{relative} must render dates through the date helpers, not a format string");
        }

        scanned.Should().BeGreaterThan(0, "the scan must find the area's views, or it guards nothing");
    }

    [Fact]
    public void PhaseSourceFiles_CarryNoPlanningReferences()
    {
        foreach (var path in PhaseSourceFiles)
        {
            var text = ReadRepoFile(path);
            foreach (var (label, pattern) in PlanningReferencePatterns)
                pattern.Matches(text).Select(m => m.Value).Should().BeEmpty(
                    $"{path} must not carry {label}: planning references go stale when the work closes");
        }
    }

    [Fact]
    public void Scanners_FlagWhatTheyForbidAndAllowWhatTheyPermit()
    {
        // Without a known-bad sample the guards above would pass on an empty list whether or not a
        // scanner worked, so each scanner is shown to fire here and to stay quiet on a clean sample.
        RawDateFormats("@Model.Date.ToString(\"dddd, MMMM d\")").Should().HaveCount(1);
        RawDateFormats("@Model.Date.ToString(\"" + "HH:mm" + "\")").Should().HaveCount(1);
        RawDateFormats("@Model.Date.ToString(\"yyyy-MM-dd\")").Should().BeEmpty();
        RawDateFormats("@Model.Date.ToString(\"yyyy-MM-ddTHH:mm\")").Should().BeEmpty();
        RawDateFormats("@Model.Count.ToString(\"N0\")").Should().BeEmpty();
        RawDateFormats("@flag.ToString().ToLower()").Should().BeEmpty();

        // A commented-out call is not rendered, so it is not flagged once comments are stripped.
        RawDateFormats(StripRazorAndHtmlComments("@* @d.ToString(\"dddd\") *@ <!-- @d.ToString(\"dddd\") -->"))
            .Should().BeEmpty();

        var planning = "// see " + "D" + "-09 and " + "Q" + "-116, " + "Pha" + "se 97" + ", 97" + "-06 in " + "RESE" + "ARCH.md";
        PlanningReferencePatterns.Count(p => p.Pattern.IsMatch(planning)).Should().BeGreaterThanOrEqualTo(5);
        PlanningReferencePatterns.Count(p => p.Pattern.IsMatch("// keyboard users must be able to leave the field"))
            .Should().Be(0);
    }
}
