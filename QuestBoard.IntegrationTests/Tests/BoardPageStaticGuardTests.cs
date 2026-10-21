using System.Text.RegularExpressions;

namespace QuestBoard.IntegrationTests.Tests;

/// <summary>
/// Keeps planning and tracking references out of the source the per-board page selection added or
/// changed. Those references go stale the moment the work closes, and an integration suite reading
/// rendered markup cannot see a comment, so only a scan of the files themselves can hold the line.
/// Tests are not scanned, since they describe behaviour and are not shipped; generated migration
/// files are not scanned either, because the tool that writes them adds no comments.
/// </summary>
public class BoardPageStaticGuardTests
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

    // Written as regex data, with each forbidden literal built from fragments, so this file never
    // holds the shapes it forbids elsewhere.
    private static readonly (string Label, Regex Pattern)[] PlanningReferencePatterns =
    [
        ("a decision id", new Regex(@"\bD-(0[1-9]|1[0-9]|2[01])\b")),
        ("a phase reference", new Regex("Pha" + "se\\s+9" + "3\\b", RegexOptions.IgnoreCase)),
        ("a plan id", new Regex(@"\b9" + @"3-(0[1-9]|1[0-5])\b")),
        ("a planning document name", new Regex(
            "CONT" + "EXT\\.md|RESE" + "ARCH\\.md|UI-" + "SPEC|PATT" + "ERNS\\.md|VALID" + "ATION\\.md")),
    ];

    // The source files this feature added or changed. A file that does not exist is skipped, so
    // the list can name a file before the change that introduces it has merged.
    private static readonly string[] FeatureSourceFiles =
    [
        // Domain
        "QuestBoard.Domain/Enums/BoardPage.cs",
        "QuestBoard.Domain/Enums/MoveStatus.cs",
        "QuestBoard.Domain/Extensions/BoardPagePolicy.cs",
        "QuestBoard.Domain/Extensions/ServiceExtensions.cs",
        "QuestBoard.Domain/Interfaces/IBoardPageResolver.cs",
        "QuestBoard.Domain/Interfaces/IBoardPageSettingsService.cs",
        "QuestBoard.Domain/Interfaces/IEventRepository.cs",
        "QuestBoard.Domain/Interfaces/IQuestRepository.cs",
        "QuestBoard.Domain/Models/Group.cs",
        "QuestBoard.Domain/Models/GroupWithMemberCount.cs",
        "QuestBoard.Domain/Models/LinkedBoard.cs",
        "QuestBoard.Domain/Models/LinkedEventEntry.cs",
        "QuestBoard.Domain/Models/LinkedQuestEntry.cs",
        "QuestBoard.Domain/Services/BoardPageSettingsService.cs",
        "QuestBoard.Domain/Services/EventMoveService.cs",
        "QuestBoard.Domain/Services/LinkedCalendarService.cs",
        "QuestBoard.Domain/Services/MoveControlService.cs",
        "QuestBoard.Domain/Services/QuestMoveService.cs",

        // Repository
        "QuestBoard.Repository/Automapper/EntityProfile.cs",
        "QuestBoard.Repository/Entities/GroupEntity.cs",
        "QuestBoard.Repository/EventRepository.cs",
        "QuestBoard.Repository/GroupRepository.cs",
        "QuestBoard.Repository/LinkedBoardSetRepository.cs",
        "QuestBoard.Repository/QuestRepository.cs",

        // Service: wiring, filter, helpers, resolver
        "QuestBoard.Service/Program.cs",
        "QuestBoard.Service/Filters/BoardPageGateFilter.cs",
        "QuestBoard.Service/Helpers/BoardPageRoutes.cs",
        "QuestBoard.Service/Extensions/BoardPageAvailabilityExtensions.cs",
        "QuestBoard.Service/Services/BoardPageResolver.cs",

        // Service: controllers
        "QuestBoard.Service/Controllers/Shop/ShopController.cs",
        "QuestBoard.Service/Controllers/Shop/ShopManagementController.cs",
        "QuestBoard.Service/Controllers/QuestBoard/PlayersController.cs",
        "QuestBoard.Service/Controllers/DungeonMaster/DungeonMasterController.cs",
        "QuestBoard.Service/Controllers/Characters/CharactersController.cs",
        "QuestBoard.Service/Controllers/Contacts/ContactsController.cs",
        "QuestBoard.Service/Controllers/Contacts/ContactCategoryManagementController.cs",
        "QuestBoard.Service/Controllers/QuestBoard/CalendarController.cs",
        "QuestBoard.Service/Controllers/Events/EventsController.cs",
        "QuestBoard.Service/Controllers/Events/SeriesController.cs",
        "QuestBoard.Service/Controllers/QuestBoard/QuestController.cs",
        "QuestBoard.Service/Controllers/QuestBoard/QuestLogController.cs",
        "QuestBoard.Service/Controllers/Admin/AdminController.cs",
        "QuestBoard.Service/Controllers/MoveController.cs",
        "QuestBoard.Service/Controllers/QuestBoard/HomeController.cs",
        "QuestBoard.Service/Controllers/GroupPickerController.cs",
        "QuestBoard.Service/Areas/Platform/Controllers/GroupController.cs",

        // Service: view models
        "QuestBoard.Service/ViewModels/PlatformViewModels/GroupEditViewModel.cs",
        "QuestBoard.Service/ViewModels/PlatformViewModels/BoardPageRowViewModel.cs",
        "QuestBoard.Service/ViewModels/MoveViewModels/MovePageViewModel.cs",

        // Service: views
        "QuestBoard.Service/Views/_ViewImports.cshtml",
        "QuestBoard.Service/Views/Shared/_Layout.cshtml",
        "QuestBoard.Service/Views/Shared/_Layout.Mobile.cshtml",
        "QuestBoard.Service/Views/Shared/_LinkedDayEntries.cshtml",
        "QuestBoard.Service/Views/Shared/_LinkedAlsoThatDay.cshtml",
        "QuestBoard.Service/Views/Calendar/Index.cshtml",
        "QuestBoard.Service/Views/Calendar/Index.Mobile.cshtml",
        "QuestBoard.Service/Views/Agenda/Index.cshtml",
        "QuestBoard.Service/Views/Agenda/Index.Mobile.cshtml",
        "QuestBoard.Service/Views/Events/Create.cshtml",
        "QuestBoard.Service/Views/Events/Edit.cshtml",
        "QuestBoard.Service/Views/Players/Index.cshtml",
        "QuestBoard.Service/Views/Players/Index.Mobile.cshtml",
        "QuestBoard.Service/Views/Quest/Details.cshtml",
        "QuestBoard.Service/Views/Quest/Details.Mobile.cshtml",
        "QuestBoard.Service/Views/DungeonMaster/Profile.cshtml",
        "QuestBoard.Service/Views/DungeonMaster/Profile.Mobile.cshtml",
        "QuestBoard.Service/Views/Account/Profile.cshtml",
        "QuestBoard.Service/Views/Account/Profile.Mobile.cshtml",
        "QuestBoard.Service/Views/Move/Index.cshtml",
        "QuestBoard.Service/Views/Move/Index.Mobile.cshtml",
        "QuestBoard.Service/Areas/Platform/Views/Group/Edit.cshtml",
        "QuestBoard.Service/Areas/Platform/Views/Group/Edit.Mobile.cshtml",
        "QuestBoard.Service/Areas/Platform/Views/Group/Index.cshtml",
        "QuestBoard.Service/Areas/Platform/Views/Group/Index.Mobile.cshtml",

        // Service: styles
        "QuestBoard.Service/wwwroot/css/platform-group.mobile.css",
        "QuestBoard.Service/wwwroot/css/dm-profile.mobile.css",
    ];

    private static IEnumerable<string> PresentFiles() =>
        FeatureSourceFiles
            .Select(relative => (Relative: relative, Full: ResolveRepoFile(relative.Split('/'))))
            .Where(file => File.Exists(file.Full))
            .Select(file => file.Relative);

    public static IEnumerable<object[]> PresentFeatureFiles() =>
        PresentFiles().Select(relative => new object[] { relative });

    [Fact]
    public void FeatureSourceFiles_ReachThePageSelectionFeature()
    {
        var present = PresentFiles().ToList();

        present.Count.Should().BeGreaterThanOrEqualTo(50,
            because: "the scan must actually reach the files it claims to guard");
        present.Should().Contain("QuestBoard.Service/Filters/BoardPageGateFilter.cs");
        present.Should().Contain("QuestBoard.Service/Services/BoardPageResolver.cs");
        present.Should().Contain("QuestBoard.Service/Areas/Platform/Views/Group/Edit.Mobile.cshtml");
    }

    [Theory]
    [MemberData(nameof(PresentFeatureFiles))]
    public void FeatureSourceFile_CarriesNoPlanningOrTrackingReference(string relativePath)
    {
        var lines = File.ReadAllLines(ResolveRepoFile(relativePath.Split('/')));

        var violations = new List<string>();
        for (var index = 0; index < lines.Length; index++)
        {
            foreach (var (label, pattern) in PlanningReferencePatterns)
            {
                if (pattern.IsMatch(lines[index]))
                    violations.Add($"line {index + 1}: {label}");
            }
        }

        violations.Should().BeEmpty(
            because: $"'{relativePath}' must explain its rules in plain language that stays true after " +
                     "the work closes; it carries " + string.Join(", ", violations));
    }
}
