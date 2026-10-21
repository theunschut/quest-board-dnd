using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Extensions;
using QuestBoard.Service.Filters;
using QuestBoard.Service.Helpers;

namespace QuestBoard.IntegrationTests.Security;

/// <summary>
/// Reflection-only closed list proving every controller action is classified: it is gated by a
/// board page, exempt from the gate, or lives on a controller that is deliberately always on. A new
/// action that is none of those fails here, so a page can never be reachable by typed URL by
/// accident.
/// </summary>
public class BoardPageCoverageTests
{
    // Controllers that serve no board page: they work on every board whatever its page settings.
    private static readonly HashSet<string> AlwaysOnControllers =
    [
        "HomeController",
        "AccountController",
        "AgendaController",
        "GroupPickerController",
        "CalendarFeedController",
        "MarkdownController",
        "EmailPreviewController",
        "AdminController",
        "GroupController",
        "LinkedBoardsController",
        "UsersController"
    ];

    // The page each gated controller serves, declared once on the class.
    private static readonly Dictionary<string, BoardPage[]> ClassLevelPages = new()
    {
        ["ShopController"] = [BoardPage.Shop],
        ["ShopManagementController"] = [BoardPage.Shop],
        ["PlayersController"] = [BoardPage.Players],
        ["CharactersController"] = [BoardPage.Characters],
        ["ContactsController"] = [BoardPage.Contacts],
        ["ContactCategoryManagementController"] = [BoardPage.Contacts],
        ["CalendarController"] = [BoardPage.Calendar],
        ["EventsController"] = [BoardPage.Events],
        ["SeriesController"] = [BoardPage.Events],
        ["QuestController"] = [BoardPage.Quests],
        ["QuestLogController"] = [BoardPage.QuestLog]
    };

    // Pages declared on single actions of controllers that serve several pages or none.
    private static readonly Dictionary<(string Controller, string Action), BoardPage[]> ActionLevelPages = new()
    {
        [("DungeonMasterController", "Profile")] = [BoardPage.DungeonMasterProfile],
        [("DungeonMasterController", "EditProfile")] = [BoardPage.DungeonMasterProfile],
        [("AdminController", "Quests")] = [BoardPage.Quests],
        [("AdminController", "DeleteQuest")] = [BoardPage.Quests],
        [("MoveController", "Quest")] = [BoardPage.Quests],
        [("MoveController", "ConfirmQuest")] = [BoardPage.Quests],
        [("MoveController", "Event")] = [BoardPage.Events],
        [("MoveController", "ConfirmEvent")] = [BoardPage.Events],
        [("QuestController", "UpdateSignupCharacter")] = [BoardPage.Characters]
    };

    // Endpoints that must keep answering when their page is switched off, because other pages embed them.
    private static readonly HashSet<(string Controller, string Action)> ExemptActions =
    [
        ("CharactersController", "GetProfilePicture"),
        ("CharactersController", "GetCroppedPicture"),
        ("DungeonMasterController", "GetDMProfilePicture"),
        ("DungeonMasterController", "GetOriginalDMProfilePicture")
    ];

    private static IReadOnlyList<Type> ControllerTypes { get; } = typeof(Program).Assembly.GetTypes()
        .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(ControllerBase).IsAssignableFrom(t))
        .ToList();

    private static IEnumerable<MethodInfo> ActionsOf(Type controller) => controller
        .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
        .Where(m => !m.IsSpecialName && m.GetCustomAttribute<NonActionAttribute>() is null);

    private static BoardPage[] ClassPagesOf(Type controller) => controller
        .GetCustomAttributes<BoardPageAttribute>(inherit: true)
        .Select(a => a.Page)
        .ToArray();

    private static BoardPage[] ActionPagesOf(MethodInfo action) => action
        .GetCustomAttributes<BoardPageAttribute>(inherit: true)
        .Select(a => a.Page)
        .ToArray();

    [Fact]
    public void EveryAction_IsGatedExemptOrOnAnAlwaysOnController()
    {
        var violations = new List<string>();

        foreach (var controller in ControllerTypes)
        {
            if (AlwaysOnControllers.Contains(controller.Name))
            {
                continue;
            }

            var gatedByClass = ClassPagesOf(controller).Length > 0;
            foreach (var action in ActionsOf(controller))
            {
                var gated = gatedByClass || ActionPagesOf(action).Length > 0;
                var exempt = action.GetCustomAttribute<BoardPageExemptAttribute>() is not null;
                if (!gated && !exempt)
                {
                    violations.Add($"{controller.Name}.{action.Name}");
                }
            }
        }

        violations.Should().BeEmpty(
            "every action must carry [BoardPage] (or sit on a controller that does), carry [BoardPageExempt], " +
            "or belong to a controller on the always-on list; add the right marker to: " +
            string.Join(", ", violations));
    }

    [Fact]
    public void AlwaysOnControllers_CarryNoClassLevelPage_AndAllExist()
    {
        var names = ControllerTypes.Select(t => t.Name).ToHashSet();

        AlwaysOnControllers.Except(names).Should().BeEmpty("an always-on entry that names no controller hides a typo");

        var gatedAlwaysOn = ControllerTypes
            .Where(t => AlwaysOnControllers.Contains(t.Name) && ClassPagesOf(t).Length > 0)
            .Select(t => t.Name);
        gatedAlwaysOn.Should().BeEmpty("a controller cannot be both always on and gated");
    }

    [Fact]
    public void EveryGatedController_CarriesExactlyItsPage()
    {
        var actual = ControllerTypes
            .Where(t => ClassPagesOf(t).Length > 0)
            .ToDictionary(t => t.Name, t => ClassPagesOf(t).OrderBy(p => p).ToArray());

        actual.Keys.Should().BeEquivalentTo(ClassLevelPages.Keys,
            "the set of controllers gated at class level is pinned so a new one is a deliberate choice");
        foreach (var (name, pages) in ClassLevelPages)
        {
            actual[name].Should().Equal(pages.OrderBy(p => p), $"{name} serves {string.Join(", ", pages)}");
        }
    }

    [Fact]
    public void ActionLevelPages_AreExactlyThePinnedOnes()
    {
        var actual = new Dictionary<(string, string), BoardPage[]>();
        foreach (var controller in ControllerTypes)
        {
            foreach (var action in ActionsOf(controller))
            {
                var declared = action.GetCustomAttributes<BoardPageAttribute>(inherit: false)
                    .Select(a => a.Page)
                    .OrderBy(p => p)
                    .ToArray();
                if (declared.Length > 0)
                {
                    actual[(controller.Name, action.Name)] = declared;
                }
            }
        }

        actual.Keys.Should().BeEquivalentTo(ActionLevelPages.Keys,
            "the set of actions gated individually is pinned so a new one is a deliberate choice");
        foreach (var (key, pages) in ActionLevelPages)
        {
            actual[key].Should().Equal(pages.OrderBy(p => p), $"{key.Controller}.{key.Action} serves {string.Join(", ", pages)}");
        }
    }

    [Fact]
    public void UpdateSignupCharacter_NeedsBothTheQuestListAndCharacters()
    {
        var action = typeof(QuestBoard.Service.Controllers.QuestBoard.QuestController)
            .GetMethod("UpdateSignupCharacter", BindingFlags.Public | BindingFlags.Instance)!;

        var effective = ClassPagesOf(action.DeclaringType!).Concat(ActionPagesOf(action)).Distinct();

        effective.Should().BeEquivalentTo([BoardPage.Quests, BoardPage.Characters]);
    }

    [Fact]
    public void ExemptActions_AreExactlyThePictureEndpoints()
    {
        var actual = ControllerTypes
            .SelectMany(c => ActionsOf(c)
                .Where(a => a.GetCustomAttribute<BoardPageExemptAttribute>() is not null)
                .Select(a => (Controller: c.Name, Action: a.Name)))
            .ToHashSet();

        actual.Should().BeEquivalentTo(ExemptActions);
    }

    [Fact]
    public void EveryHomeCandidate_RoutesToAControllerCarryingThatPage()
    {
        foreach (var page in BoardPagePolicy.HomeCandidates)
        {
            var (controllerName, actionName) = BoardPageRoutes.RouteFor(page);

            var controller = ControllerTypes.SingleOrDefault(t => t.Name == controllerName + "Controller");
            controller.Should().NotBeNull($"{page} routes to the {controllerName} controller");
            ClassPagesOf(controller!).Should().Contain(page, $"the {controllerName} controller serves {page}");
            ActionsOf(controller!).Should().Contain(m => m.Name == actionName, $"{page} routes to {controllerName}.{actionName}");
        }
    }
}
