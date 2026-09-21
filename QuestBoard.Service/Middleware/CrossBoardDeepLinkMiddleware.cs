using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Service.Constants;
using QuestBoard.Service.Helpers;
using QuestBoard.Service.Services;
using System.Security.Claims;

namespace QuestBoard.Service.Middleware;

/// <summary>
/// Recovers a deep link into an entity that lives on a board the viewer belongs to but does not
/// currently have active, by repointing the session to that board and letting the same request
/// continue -- rather than the 404 the action would otherwise return.
///
/// Guard order matters:
///   1. Anonymous requests pass through -- there is no session to repoint and no member to check.
///   2. Non-GET/HEAD requests pass through unchanged. This is an explicit verb check rather than
///      something inherited from the header gate below: a form post carries the same navigation
///      headers as a link click, so nothing else here would exclude it, and a write must behave
///      exactly as it does today.
///   3. A route with no place in the closed registry -- no route values, an unparseable id, or
///      an action this middleware has not been told about -- passes through untouched. This runs
///      before any header inspection and before any database access, so the overwhelming
///      majority of requests leave here.
///   4. A request that is not a real top-level browser navigation (see IsRealTopLevelNavigation)
///      passes through untouched.
///   5. No board is currently active. There is nothing to switch from, and the session-recovery
///      middleware ahead of this one has already sent that case to the group picker; this guard
///      is defensive.
///   6. The resolver finds no board the viewer belongs to for this id. The request continues and
///      the action answers exactly as it does today -- deliberately the same path a nonexistent
///      id takes, because a branch here is how this feature would turn into a way to find out
///      which boards exist and who belongs to them.
///   7. The resolved board is already the active one -- nothing to do.
///   Otherwise: the previous board is captured, the switcher repoints the session, a one-shot
///   banner is queued in TempData, and the same request continues into the action with the new
///   board live.
/// </summary>
public class CrossBoardDeepLinkMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            await next(context);
            return;
        }

        if (!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method))
        {
            await next(context);
            return;
        }

        if (!CrossBoardRouteTarget.TryFromRouteValues(context, out var routeTarget) || routeTarget == null)
        {
            await next(context);
            return;
        }

        if (!IsRealTopLevelNavigation(context.Request))
        {
            await next(context);
            return;
        }

        var groupContext = context.RequestServices.GetRequiredService<IActiveGroupContext>();
        if (groupContext.ActiveGroupId == null)
        {
            await next(context);
            return;
        }

        var resolver = context.RequestServices.GetRequiredService<ICrossBoardLinkResolver>();
        var userId = int.Parse(context.User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var target = await resolver.ResolveAsync(routeTarget.Kind, routeTarget.Id, userId);

        if (target == null)
        {
            // Same path a nonexistent id takes -- no branch exists here on purpose.
            await next(context);
            return;
        }

        if (target.GroupId == groupContext.ActiveGroupId)
        {
            await next(context);
            return;
        }

        // Read live, immediately before switching -- never captured earlier and never reused
        // after the switch call.
        var previousGroupId = groupContext.ActiveGroupId;
        var previousGroupName = context.Session.GetString(SessionKeys.ActiveGroupName);

        var switcher = context.RequestServices.GetRequiredService<IActiveBoardSwitcher>();
        await switcher.SwitchAsync(context, target.GroupId, target.GroupName);

        var tempDataFactory = context.RequestServices.GetRequiredService<ITempDataDictionaryFactory>();
        var tempData = tempDataFactory.GetTempData(context);
        tempData[TempDataKeys.BoardSwitchTargetName] = target.GroupName;
        if (previousGroupId != null)
        {
            tempData[TempDataKeys.BoardSwitchPreviousGroupId] = previousGroupId.Value;
            tempData[TempDataKeys.BoardSwitchPreviousGroupName] = previousGroupName;
        }

        // Same request continues -- no redirect, no second round trip.
        await next(context);
    }

    // Stops a viewer's own browser background requests -- an image load, a prefetch, a
    // prerender, a link unfurl -- from moving the board under them, and means an inline
    // reference to one of these URLs on some other site cannot repoint a reader's board. This is
    // not the tenancy boundary; that is the membership-pinned lookup above. Any HTTP client can
    // set these header values itself.
    private static bool IsRealTopLevelNavigation(HttpRequest request)
    {
        if (ContainsPrefetchHint(request.Headers["Sec-Purpose"])
            || ContainsPrefetchHint(request.Headers["Purpose"])
            || ContainsPrefetchHint(request.Headers["X-Moz"]))
        {
            return false;
        }

        return request.Headers["Sec-Fetch-Dest"] == "document"
            && request.Headers["Sec-Fetch-Mode"] == "navigate";
    }

    private static bool ContainsPrefetchHint(Microsoft.Extensions.Primitives.StringValues headerValues)
    {
        return headerValues.Any(value => value != null && value.Contains("prefetch", StringComparison.OrdinalIgnoreCase));
    }
}
