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
        // The area is read alongside the other two rather than dropped: the registry is keyed on
        // all three, so an area route that happens to reuse a registered controller and action
        // name cannot borrow that route's lookup kind. It is null on the default route.
        var area = context.GetRouteValue("area") as string;
        var controller = context.GetRouteValue("controller") as string;
        var action = context.GetRouteValue("action") as string;
        var idRaw = context.GetRouteValue("id") as string;

        if (int.TryParse(idRaw, out var id)
            && CrossBoardLinkRegistry.TryGetLookupKind(area, controller, action, out var kind))
        {
            target = new CrossBoardRouteTarget(kind, id);
            return true;
        }

        target = null;
        return false;
    }

    // A second, narrower gate than the caller's own local-URL check -- it does not replace that
    // check, it simply refuses to reason about any shape it does not recognise. The value is not
    // URL-decoded here: it arrives model-bound and already decoded by ASP.NET Core, so decoding it
    // a second time is exactly how an encoded separator (e.g. "%2F") could turn into a real path
    // separator that was never present in the original URL.
    internal static bool TryFromLocalUrl(string? url, out CrossBoardRouteTarget? target)
    {
        target = null;

        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        // Only a single-slash site-relative path is accepted. An absolute URL never starts with
        // '/' at all; "//host/..." is protocol-relative; "/\host/..." is a backslash-prefixed
        // variant some browsers still resolve as a host. All three resolve to nothing here.
        if (url[0] != '/' || (url.Length > 1 && (url[1] == '/' || url[1] == '\\')))
        {
            return false;
        }

        // A query string or fragment makes the value unparseable as a plain path, so it is cut
        // off rather than treated as part of the route -- the caller only cares what page the
        // link points at, not what it was carrying alongside that.
        var path = url;
        var cutIndex = path.IndexOfAny(['?', '#']);
        if (cutIndex >= 0)
        {
            path = path[..cutIndex];
        }

        // Exactly three non-empty segments: controller, action, id. This application's default
        // route pattern never produces anything shorter (a list route like the quest index has
        // one segment) or longer (a deeper path is not a shape the default route produces). An
        // area route puts the area first and so produces four, which is refused here too.
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length != 3)
        {
            return false;
        }

        if (!int.TryParse(segments[2], out var id))
        {
            return false;
        }

        // Three segments can only have come from the default route, which has no area -- so no
        // area is a statement about the shape already established above, not an assumption.
        if (!CrossBoardLinkRegistry.TryGetLookupKind(null, segments[0], segments[1], out var kind))
        {
            return false;
        }

        target = new CrossBoardRouteTarget(kind, id);
        return true;
    }
}
