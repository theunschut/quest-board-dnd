using System.Text.RegularExpressions;

namespace QuestBoard.IntegrationTests.Tests;

/// <summary>
/// Keeps planning and tracking references out of the source the board-move feature added or
/// changed. Those references go stale the moment the work closes, and an integration suite reading
/// rendered markup cannot see a comment, so only a scan of the files themselves can hold the line.
/// </summary>
public class BoardMoveStaticGuardTests
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
        ("a phase reference", new Regex("Pha" + "se\\s+9" + "2\\b", RegexOptions.IgnoreCase)),
        ("a plan id", new Regex(@"\b9" + @"2-(0\d|10)\b")),
        ("a planning document name", new Regex(
            "CONT" + "EXT\\.md|RESE" + "ARCH\\.md|UI-" + "SPEC|PATT" + "ERNS\\.md|VALID" + "ATION\\.md")),
    ];

    // The source files this feature added or changed. A file that does not exist is skipped, so
    // the list can name a file before the change that introduces it has merged.
    private static readonly string[] FeatureSourceFiles =
    [
        "QuestBoard.Domain/Enums/MoveBlockReason.cs",
        "QuestBoard.Domain/Enums/MoveStatus.cs",
        "QuestBoard.Domain/Enums/QuestMovedAudience.cs",
        "QuestBoard.Domain/Extensions/MoveEligibility.cs",
        "QuestBoard.Domain/Extensions/MovePreviewStamp.cs",
        "QuestBoard.Domain/Extensions/ServiceExtensions.cs",
        "QuestBoard.Domain/Extensions/WaitlistOrdering.cs",
        "QuestBoard.Domain/Interfaces/IEventMoveRepository.cs",
        "QuestBoard.Domain/Interfaces/IEventMoveService.cs",
        "QuestBoard.Domain/Interfaces/IIdentityService.cs",
        "QuestBoard.Domain/Interfaces/IMoveControlService.cs",
        "QuestBoard.Domain/Interfaces/IQuestEmailDispatcher.cs",
        "QuestBoard.Domain/Interfaces/IQuestMoveRepository.cs",
        "QuestBoard.Domain/Interfaces/IQuestMoveService.cs",
        "QuestBoard.Domain/Models/BoardMove/EventMovePlan.cs",
        "QuestBoard.Domain/Models/BoardMove/EventMovePreview.cs",
        "QuestBoard.Domain/Models/BoardMove/MoveControlState.cs",
        "QuestBoard.Domain/Models/BoardMove/MovePerson.cs",
        "QuestBoard.Domain/Models/BoardMove/MovePreview.cs",
        "QuestBoard.Domain/Models/BoardMove/QuestMovePlan.cs",
        "QuestBoard.Domain/Models/BoardMove/QuestMovePreview.cs",
        "QuestBoard.Domain/Services/EventMoveService.cs",
        "QuestBoard.Domain/Services/MoveControlService.cs",
        "QuestBoard.Domain/Services/QuestMoveService.cs",
        "QuestBoard.Repository/EventMoveRepository.cs",
        "QuestBoard.Repository/Extensions/ServiceExtensions.cs",
        "QuestBoard.Repository/IdentityService.cs",
        "QuestBoard.Repository/QuestMoveRepository.cs",
        "QuestBoard.Service/Components/Emails/QuestMoved.razor",
        "QuestBoard.Service/Components/Emails/WaitlistPromoted.razor",
        "QuestBoard.Service/Constants/TempDataKeys.cs",
        "QuestBoard.Service/Controllers/Admin/EmailPreviewController.cs",
        "QuestBoard.Service/Controllers/Events/EventsController.cs",
        "QuestBoard.Service/Controllers/MoveController.cs",
        "QuestBoard.Service/Controllers/QuestBoard/QuestController.cs",
        "QuestBoard.Service/Helpers/MoveCopy.cs",
        "QuestBoard.Service/Jobs/QuestMovedEmailJob.cs",
        "QuestBoard.Service/Jobs/QuestWaitlistPromotedEmailJob.cs",
        "QuestBoard.Service/Services/HangfireQuestEmailDispatcher.cs",
        "QuestBoard.Service/Services/NullQuestEmailDispatcher.cs",
        "QuestBoard.Service/ViewModels/MoveViewModels/MovePageViewModel.cs",
        "QuestBoard.Service/Views/Events/Details.cshtml",
        "QuestBoard.Service/Views/Move/Index.cshtml",
        "QuestBoard.Service/Views/Move/Index.Mobile.cshtml",
        "QuestBoard.Service/Views/Quest/Details.cshtml",
        "QuestBoard.Service/Views/Quest/Details.Mobile.cshtml",
        "QuestBoard.Service/Views/Shared/_Layout.cshtml",
        "QuestBoard.Service/Views/Shared/_Toasts.cshtml",
        "QuestBoard.Service/wwwroot/css/mobile.css",
        "QuestBoard.Service/wwwroot/css/move-entry.css",
        "QuestBoard.Service/wwwroot/css/move-entry.mobile.css",
    ];

    private static IEnumerable<string> PresentFiles() =>
        FeatureSourceFiles
            .Select(relative => (Relative: relative, Full: ResolveRepoFile(relative.Split('/'))))
            .Where(file => File.Exists(file.Full))
            .Select(file => file.Relative);

    public static IEnumerable<object[]> PresentFeatureFiles() =>
        PresentFiles().Select(relative => new object[] { relative });

    [Fact]
    public void FeatureSourceFiles_ReachTheMoveFeature()
    {
        var present = PresentFiles().ToList();

        present.Should().Contain("QuestBoard.Service/Controllers/MoveController.cs",
            because: "the scan must actually reach the files it claims to guard");
        present.Should().Contain("QuestBoard.Domain/Services/QuestMoveService.cs");
        present.Should().Contain("QuestBoard.Service/Views/Move/Index.Mobile.cshtml");
    }

    [Theory]
    [MemberData(nameof(PresentFeatureFiles))]
    public void FeatureSourceFile_CarriesNoPlanningOrTrackingReference(string relativePath)
    {
        var content = File.ReadAllText(ResolveRepoFile(relativePath.Split('/')));

        var violations = PlanningReferencePatterns
            .Where(entry => entry.Pattern.IsMatch(content))
            .Select(entry => entry.Label)
            .ToList();

        violations.Should().BeEmpty(
            because: $"'{relativePath}' must explain its rules in plain language that stays true after " +
                     "the work closes; it carries " + string.Join(", ", violations));
    }
}
