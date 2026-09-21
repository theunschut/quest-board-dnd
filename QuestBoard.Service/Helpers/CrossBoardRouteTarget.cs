using Microsoft.AspNetCore.Http;
using QuestBoard.Domain.Enums;

namespace QuestBoard.Service.Helpers;

/// <summary>
/// A parsed, registry-confirmed cross-board lookup target for the current request -- what kind
/// of thing the id points at, and the id itself.
/// </summary>
internal sealed record CrossBoardRouteTarget(CrossBoardLookupKind Kind, int Id)
{
    // Route values are populated by UseRouting(), which runs before this is ever called, so no
    // manual URL parsing is needed here.
    internal static bool TryFromRouteValues(HttpContext context, out CrossBoardRouteTarget? target)
    {
        var controller = context.GetRouteValue("controller") as string;
        var action = context.GetRouteValue("action") as string;
        var idRaw = context.GetRouteValue("id") as string;

        if (int.TryParse(idRaw, out var id)
            && CrossBoardLinkRegistry.TryGetLookupKind(controller, action, out var kind))
        {
            target = new CrossBoardRouteTarget(kind, id);
            return true;
        }

        target = null;
        return false;
    }
}
