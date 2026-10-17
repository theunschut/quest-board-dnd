using QuestBoard.Domain.Models;
using QuestBoard.Service.ViewModels.CalendarViewModels;
using System.Reflection;
using System.Text.RegularExpressions;

namespace QuestBoard.IntegrationTests.Tests;

/// <summary>
/// Guards the read-only contract of the linked-board calendar entries from the source itself.
/// A passing integration suite cannot see these: it reads rendered markup for one scenario, not
/// the files that produce it, and it cannot notice a roster-shaped member added to a type that
/// is supposed to carry only a title, a board name and a time.
/// </summary>
public class LinkedBoardStaticGuardTests
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

    private static string ReadView(string fileName) =>
        File.ReadAllText(ResolveRepoFile("QuestBoard.Service", "Views", "Shared", fileName));

    // ---- The two linked partials are read-only markup ----

    // Each group is here for a different reason.
    // Form controls: both partials sit inside, or next to, the form that posts the date answers,
    // so a control here could be submitted with it.
    // Answer vocabulary: the class names and field names that belong to the board's own
    // entries must never appear in markup for another board's entries, or styling and scripts
    // that key on them would pick the linked entries up.
    // Identity attributes: an id or for attribute can collide with, or be targeted like, the
    // answer inputs; the data attributes are what the calendar scripts read an entry from.
    // Raw output: every value in these partials must go through Razor's encoding.
    private static readonly string[] BannedInLinkedPartials =
    [
        "<input", "<form", "<button", "<label", "<select", "<textarea",
        "DateVotes",
        "warning-icon-display", "finalized-icon-display", "current-quest", "quest-event", "calendar-event",
        "data-quest-id", "data-proposed-date-id",
        "Html.Raw",
        " id=\"", " for=\"",
    ];

    private const string PollWord = "vote";

    public static IEnumerable<object[]> LinkedPartials() =>
    [
        ["_LinkedDayEntries.cshtml"],
        ["_LinkedAlsoThatDay.cshtml"],
    ];

    [Theory]
    [MemberData(nameof(LinkedPartials))]
    public void LinkedPartial_ContainsNoFormControlAnswerMarkerIdentityAttributeOrRawOutput(string fileName)
    {
        var source = ReadView(fileName);

        source.Should().NotBeNullOrWhiteSpace();
        foreach (var banned in BannedInLinkedPartials)
        {
            source.Should().NotContain(banned, because: $"{fileName} is read-only markup and must not carry '{banned}'");
        }

        source.Should().NotContainEquivalentOf(PollWord,
            because: $"{fileName} must not use the answer vocabulary of the board's own entries");
    }

    // ---- Desktop linked text is not repainted by the card's span rule ----

    // The desktop calendar sits inside a .modern-card, and modern-card.css paints every span in a
    // card cream with !important. A visible span in a linked chip or in the day count therefore
    // renders cream on the chip's pale surface whatever calendar.css asks for, which no markup
    // assertion would notice. Only text that is hidden from sight may be a span.
    private static readonly Regex SpanTag = new(@"<span\b[^>]*>", RegexOptions.Compiled);

    [Fact]
    public void LinkedDayEntries_UsesSpansOnlyForVisuallyHiddenText()
    {
        var spans = SpanTag.Matches(ReadView("_LinkedDayEntries.cshtml")).Select(m => m.Value).ToList();

        spans.Should().NotBeEmpty(because: "each chip carries visually hidden status text");
        spans.Should().OnlyContain(tag => tag.Contains("class=\"visually-hidden\""),
            because: "a visible span inside the calendar card is repainted cream by the card's !important span rule");
    }

    [Fact]
    public void DesktopCalendar_LinkedDayCountIsNotASpan_AndHidesOnlyItsScreenReaderText()
    {
        var source = ReadView("_Calendar.cshtml");
        var start = source.IndexOf("class=\"linked-day-count\"", StringComparison.Ordinal);
        start.Should().BeGreaterThan(-1);
        var tagStart = source.LastIndexOf('<', start);
        var element = source[tagStart..(source.IndexOf("</div>", start, StringComparison.Ordinal) + "</div>".Length)];

        element.Should().StartWith("<div", because: "a span would be repainted by the card's !important span rule");
        SpanTag.Matches(element).Select(m => m.Value)
            .Should().OnlyContain(tag => tag.Contains("class=\"visually-hidden\""));
    }

    // ---- The partials sit where the contract needs them ----

    [Fact]
    public void DesktopCalendar_PlacesTheLinkedPartialOnceAfterTheOwnQuestAnswerBranch()
    {
        var source = ReadView("_Calendar.cshtml");

        Regex.Matches(source, "_LinkedDayEntries").Count.Should().Be(1);
        var answerBranch = source.LastIndexOf("isDetailsPage && questOnDay.Quest.Id == currentQuestId", StringComparison.Ordinal);
        answerBranch.Should().BeGreaterThan(-1);
        source.IndexOf("_LinkedDayEntries", StringComparison.Ordinal).Should().BeGreaterThan(answerBranch);
    }

    [Fact]
    public void MobileCalendar_PlacesTheLinkedPartialOnceBetweenTheDateLabelAndTheFirstAnswerRow()
    {
        var source = ReadView("_Calendar.Mobile.cshtml");

        Regex.Matches(source, "_LinkedAlsoThatDay").Count.Should().Be(1);
        var label = source.IndexOf("calendar-date-label-mobile", StringComparison.Ordinal);
        var partial = source.IndexOf("_LinkedAlsoThatDay", StringComparison.Ordinal);
        var firstRow = source.IndexOf("calendar-vote-row", StringComparison.Ordinal);
        label.Should().BeGreaterThan(-1);
        partial.Should().BeGreaterThan(label);
        firstRow.Should().BeGreaterThan(partial);
    }

    // ---- The linked model types stay narrow ----

    private static readonly string[] ForbiddenMemberFragments =
        ["Vote", "Signup", "Player", "Roster", "Character", "Description"];

    // The full board-entry types a linked entry must never hold: holding one would give the
    // overlay a path to the other board's signups, answers or people.
    private static readonly string[] ForbiddenMemberTypeNames =
        ["Quest", "Event", "ProposedDate", "PlayerSignup", "PlayerDateVote", "User", "Character"];

    public static IEnumerable<object[]> LinkedModelTypes() =>
    [
        [typeof(LinkedQuestEntry)],
        [typeof(LinkedEventEntry)],
        [typeof(LinkedBoard)],
        [typeof(ViewerLinkedBoards)],
        [typeof(LinkedCalendarData)],
        [typeof(LinkedDayEntries)],
        [typeof(LinkedQuestOnDay)],
        [typeof(LinkedEventOnDay)],
    ];

    private static IEnumerable<Type> Unwrap(Type type)
    {
        yield return type;
        if (type.IsGenericType)
        {
            foreach (var argument in type.GetGenericArguments().SelectMany(Unwrap))
            {
                yield return argument;
            }
        }

        if (type.IsArray)
        {
            foreach (var element in Unwrap(type.GetElementType()!))
            {
                yield return element;
            }
        }
    }

    [Theory]
    [MemberData(nameof(LinkedModelTypes))]
    public void LinkedModelType_ExposesNoAnswerRosterOrDescriptionMember(Type type)
    {
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);

        properties.Should().NotBeEmpty();
        foreach (var property in properties)
        {
            foreach (var fragment in ForbiddenMemberFragments)
            {
                property.Name.Should().NotContainEquivalentOf(fragment,
                    because: $"{type.Name}.{property.Name} must not carry answer, roster or description data");
            }
        }
    }

    [Theory]
    [MemberData(nameof(LinkedModelTypes))]
    public void LinkedModelType_HoldsNoFullQuestEventDateAnswerOrPersonType(Type type)
    {
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);

        foreach (var property in properties)
        {
            foreach (var inner in Unwrap(property.PropertyType))
            {
                var isBoardModel = inner.Namespace != null
                    && inner.Namespace.StartsWith("QuestBoard.Domain.Models", StringComparison.Ordinal);
                var forbidden = isBoardModel && ForbiddenMemberTypeNames.Contains(inner.Name);
                forbidden.Should().BeFalse(
                    because: $"{type.Name}.{property.Name} must not be typed as the full board model '{inner.FullName}'");
            }
        }
    }

    // ---- The deep-link prefix is not reused ----

    private static readonly string[] SourceProjects = ["QuestBoard.Domain", "QuestBoard.Repository", "QuestBoard.Service"];

    private static IEnumerable<string> SourceFiles(string extension, params string[] projects) =>
        projects
            .SelectMany(project => Directory.EnumerateFiles(ResolveRepoFile(project), "*" + extension, SearchOption.AllDirectories))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

    // The prefix belongs to the deep-link resolver that switches a visitor onto the board a link
    // points at. A second "link" concept under the same prefix would be misread as part of it,
    // so linked boards use their own names and this set must stay exactly what it was.
    [Fact]
    public void CrossBoardLinkPrefix_BelongsOnlyToTheDeepLinkResolver()
    {
        var declared = new SortedSet<string>(StringComparer.Ordinal);
        var declaration = new Regex(@"\b(?:class|interface|record|enum|struct)\s+(CrossBoardLink\w*)");

        foreach (var file in SourceFiles(".cs", SourceProjects))
        {
            foreach (Match match in declaration.Matches(File.ReadAllText(file)))
            {
                declared.Add(match.Groups[1].Value);
            }
        }

        declared.Should().BeEquivalentTo(new[]
        {
            "CrossBoardLinkRegistry",
            "CrossBoardLinkRepository",
            "CrossBoardLinkResolverService",
        });
    }

    // ---- No planning reference reached this feature's source ----

    // Written as regex data, with the phase word built from fragments, so this file never holds
    // the literal shapes it forbids elsewhere.
    private static readonly (string Label, Regex Pattern)[] PlanningReferencePatterns =
    [
        ("a decision id", new Regex(@"\bD-(0[1-9]|1[0-9]|20)\b")),
        ("a phase reference", new Regex("Pha" + "se\\s+(91|92)\\b", RegexOptions.IgnoreCase)),
        ("a plan id", new Regex(@"\b91-(0\d|10)\b")),
        ("a planning document name", new Regex(@"CONTEXT\.md|RESEARCH\.md|UI-SPEC|PATTERNS\.md")),
    ];

    // The source files this feature added or changed. A file that does not exist is skipped, so
    // the list can name a file before the change that introduces it has merged.
    private static readonly string[] FeatureSourceFiles =
    [
        "QuestBoard.Domain/Enums/LinkedBoardSetResult.cs",
        "QuestBoard.Domain/Extensions/ServiceExtensions.cs",
        "QuestBoard.Domain/Interfaces/IEventRepository.cs",
        "QuestBoard.Domain/Interfaces/ILinkedBoardSetRepository.cs",
        "QuestBoard.Domain/Interfaces/ILinkedBoardSetService.cs",
        "QuestBoard.Domain/Interfaces/ILinkedCalendarService.cs",
        "QuestBoard.Domain/Interfaces/IQuestRepository.cs",
        "QuestBoard.Domain/Interfaces/IQuestService.cs",
        "QuestBoard.Domain/Models/LinkedBoardCandidate.cs",
        "QuestBoard.Domain/Models/LinkedBoard.cs",
        "QuestBoard.Domain/Models/LinkedBoardSetBoard.cs",
        "QuestBoard.Domain/Models/LinkedBoardSet.cs",
        "QuestBoard.Domain/Models/LinkedBoardSetOutcome.cs",
        "QuestBoard.Domain/Models/LinkedCalendarData.cs",
        "QuestBoard.Domain/Models/LinkedEventEntry.cs",
        "QuestBoard.Domain/Models/LinkedQuestEntry.cs",
        "QuestBoard.Domain/Models/ViewerLinkedBoards.cs",
        "QuestBoard.Domain/Services/LinkedBoardSetService.cs",
        "QuestBoard.Domain/Services/LinkedCalendarService.cs",
        "QuestBoard.Domain/Services/QuestService.cs",
        "QuestBoard.Repository/Automapper/EntityProfile.cs",
        "QuestBoard.Repository/Entities/GroupEntity.cs",
        "QuestBoard.Repository/Entities/LinkedBoardSetEntity.cs",
        "QuestBoard.Repository/Entities/QuestBoardContext.cs",
        "QuestBoard.Repository/EventRepository.cs",
        "QuestBoard.Repository/Extensions/ServiceExtensions.cs",
        "QuestBoard.Repository/LinkedBoardSetRepository.cs",
        "QuestBoard.Repository/QuestRepository.cs",
        "QuestBoard.Service/Areas/Platform/Controllers/LinkedBoardsController.cs",
        "QuestBoard.Service/Areas/Platform/Views/Group/Index.cshtml",
        "QuestBoard.Service/Areas/Platform/Views/Group/Index.Mobile.cshtml",
        "QuestBoard.Service/Areas/Platform/Views/LinkedBoards/Index.cshtml",
        "QuestBoard.Service/Areas/Platform/Views/LinkedBoards/Index.Mobile.cshtml",
        "QuestBoard.Service/Controllers/QuestBoard/CalendarController.cs",
        "QuestBoard.Service/Controllers/QuestBoard/QuestController.cs",
        "QuestBoard.Service/ViewModels/CalendarViewModels/CalendarDay.cs",
        "QuestBoard.Service/ViewModels/CalendarViewModels/CalendarViewModel.cs",
        "QuestBoard.Service/ViewModels/CalendarViewModels/LinkedDayEntries.cs",
        "QuestBoard.Service/ViewModels/CalendarViewModels/LinkedEventOnDay.cs",
        "QuestBoard.Service/ViewModels/CalendarViewModels/LinkedQuestOnDay.cs",
        "QuestBoard.Service/ViewModels/PlatformViewModels/LinkedBoardSetCreateViewModel.cs",
        "QuestBoard.Service/ViewModels/PlatformViewModels/LinkedBoardsIndexViewModel.cs",
        "QuestBoard.Service/Views/Calendar/Index.cshtml",
        "QuestBoard.Service/Views/Calendar/Index.Mobile.cshtml",
        "QuestBoard.Service/Views/Shared/_Calendar.cshtml",
        "QuestBoard.Service/Views/Shared/_Calendar.Mobile.cshtml",
        "QuestBoard.Service/Views/Shared/_LinkedAlsoThatDay.cshtml",
        "QuestBoard.Service/Views/Shared/_LinkedDayEntries.cshtml",
        "QuestBoard.Service/wwwroot/css/calendar.css",
        "QuestBoard.Service/wwwroot/css/calendar.mobile.css",
        "QuestBoard.Service/wwwroot/css/platform-linked-boards.mobile.css",
        "QuestBoard.Service/wwwroot/css/quests.mobile.css",
    ];

    [Fact]
    public void FeatureSourceFiles_CarryNoPlanningOrTrackingReference()
    {
        var present = FeatureSourceFiles
            .Select(relative => ResolveRepoFile(relative.Split('/')))
            .Where(File.Exists)
            .ToList();

        present.Should().Contain(path => path.EndsWith("_LinkedDayEntries.cshtml", StringComparison.Ordinal),
            because: "the scan must actually reach the files it claims to guard");

        // Generated schema files are not hand-written source.
        var migrations = SourceFiles(".cs", "QuestBoard.Repository")
            .Where(f => f.Contains($"{Path.DirectorySeparatorChar}Migrations{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .ToHashSet();

        var violations = new List<string>();
        foreach (var file in present.Where(f => !migrations.Contains(f)))
        {
            var content = File.ReadAllText(file);
            foreach (var (label, pattern) in PlanningReferencePatterns)
            {
                if (pattern.IsMatch(content))
                {
                    violations.Add($"{Path.GetFileName(file)} carries {label}");
                }
            }
        }

        violations.Should().BeEmpty(because: string.Join(", ", violations));
    }
}
