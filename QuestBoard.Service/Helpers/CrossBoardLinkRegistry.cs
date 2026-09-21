using QuestBoard.Domain.Enums;
using QuestBoard.Service.Controllers.QuestBoard;

namespace QuestBoard.Service.Helpers;

/// <summary>
/// The closed table of (controller, action) routes that can be resolved across boards. A route
/// absent from this table can never be resolved across boards -- adding an action to the
/// application later never silently gains the ability to repoint a viewer's session; it has to
/// be added here first.
/// </summary>
internal static class CrossBoardLinkRegistry
{
    private static readonly Dictionary<string, CrossBoardLookupKind> Routes = new(StringComparer.OrdinalIgnoreCase)
    {
        [$"{ControllerNameOf<QuestController>()}/{nameof(QuestController.Details)}"] = CrossBoardLookupKind.Quest
    };

    // Copied from GroupSessionMiddleware rather than shared, so a rename of either controller
    // remains visible in both files independently -- the call site keeps compiling, but a
    // rename tool will surface this file too instead of silently breaking a runtime lookup.
    private static string ControllerNameOf<TController>() where TController : Microsoft.AspNetCore.Mvc.Controller
    {
        const string suffix = "Controller";
        var name = typeof(TController).Name;
        return name.EndsWith(suffix, StringComparison.Ordinal) ? name[..^suffix.Length] : name;
    }

    internal static bool TryGetLookupKind(string? controller, string? action, out CrossBoardLookupKind kind)
    {
        if (controller == null || action == null)
        {
            kind = default;
            return false;
        }

        return Routes.TryGetValue($"{controller}/{action}", out kind);
    }

    internal static IReadOnlyCollection<string> RegisteredRoutes => Routes.Keys;
}
