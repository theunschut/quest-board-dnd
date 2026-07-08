using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Service.Constants;
using QuestBoard.Service.Controllers;
using QuestBoard.Service.Controllers.Admin;

namespace QuestBoard.Service.Middleware;

/// <summary>
/// Session-recovery middleware. Redirects an authenticated user whose
/// group session has expired (no ActiveGroupId) to the group picker instead of letting the
/// request fall through to a broken, group-scoped page.
///
/// Guard order matters:
///   1. Anonymous requests pass through — [Authorize] handles the login redirect.
///   2. Exempt paths (the picker itself, auth, platform, error routes) pass through for
///      every authenticated role, including SuperAdmin — these are the genuine
///      group-agnostic workflows (picking a group, managing the account, platform-wide
///      administration) that must never be gated on having an active group.
///   3. Otherwise, resolve IActiveGroupContext; if ActiveGroupId is null, the request is
///      gated exactly the same way regardless of role — a null active group is ambiguous
///      (which group's data should render?) and must never be silently treated as "show
///      everything" or "show nothing":
///        - GET/HEAD requests are redirected to the hardcoded literal "/groups/pick"
///          (never a user-supplied URL — open-redirect mitigation), preserving the original
///          path+query as ?returnUrl= so GroupPickerController can send the user back
///          (validated there via Url.IsLocalUrl before use).
///        - Non-idempotent requests (POST/PUT/PATCH/DELETE) are NOT redirected, because
///          Response.Redirect (302) causes browsers to re-issue the request as a GET,
///          silently dropping the submitted body with no user-facing error. Instead we
///          short-circuit with 409 Conflict so the caller gets a distinguishable failure
///          signal rather than a silent data loss.
///   4. With a non-null ActiveGroupId, membership is periodically re-checked so a user removed
///      from their active group mid-session doesn't keep board access indefinitely just because
///      their session still holds a group id that happens to still exist. If more than
///      MembershipRevalidationInterval has elapsed since the last check (or the timestamp is
///      missing/unparseable), membership is re-verified via GetGroupRoleByIdAsync. A no-longer-member
///      is gated out using the exact same GET/HEAD-redirect vs. POST-409 branch as step 3. SuperAdmin
///      is excluded — their group selection was never membership-gated, so there is no membership
///      row that can go stale.
///   5. Otherwise, the request proceeds with a resolved, still-valid active group.
/// </summary>
public class GroupSessionMiddleware(RequestDelegate next)
{
    // "/GroupPicker" and "/Account" are derived from nameof(...) rather than
    // typed as raw literals so that renaming either controller is a compile-time-visible change
    // here (the call site keeps compiling, but a `git grep`/refactor-rename tool will surface
    // this file too) instead of a silent runtime redirect loop. ControllerNameOf strips the
    // conventional "Controller" suffix the same way ASP.NET Core's routing does.
    //
    // "/groups/pick" (GroupPickerController's explicit custom [Route] attribute) and
    // "/platform"/"/Error" (an MVC area prefix and the exception-handler path, neither of which
    // is a controller-name-derived route) cannot be derived from nameof(...) and remain literals.
    private static readonly string[] ExemptPathPrefixes =
    [
        "/groups/pick",
        $"/{ControllerNameOf<GroupPickerController>()}",
        $"/{ControllerNameOf<AccountController>()}",
        "/platform",
        "/Error"
    ];

    private static string ControllerNameOf<TController>() where TController : Microsoft.AspNetCore.Mvc.Controller
    {
        const string suffix = "Controller";
        var name = typeof(TController).Name;
        return name.EndsWith(suffix, StringComparison.Ordinal) ? name[..^suffix.Length] : name;
    }

    // Matches the app's existing 5-minute security-stamp staleness bound (see
    // SecurityStampValidatorOptions.ValidationInterval in Program.cs) for a consistent staleness
    // window across the app, not because the two mechanisms share a code path — ActiveGroupId
    // lives in Session, not in the auth cookie's claims, so it needs its own independent check.
    private static readonly TimeSpan MembershipRevalidationInterval = TimeSpan.FromMinutes(5);

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            await next(context);
            return;
        }

        if (ExemptPathPrefixes.Any(prefix => context.Request.Path.StartsWithSegments(prefix)))
        {
            await next(context);
            return;
        }

        var groupContext = context.RequestServices.GetRequiredService<IActiveGroupContext>();
        if (groupContext.ActiveGroupId == null)
        {
            if (!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method))
            {
                // Don't silently redirect a non-idempotent request — Response.Redirect emits a
                // 302, which browsers re-issue as a GET, dropping the submitted body with no
                // error shown to the user. Fail loudly instead so the client can surface a
                // "your session expired, please retry" message.
                context.Response.StatusCode = StatusCodes.Status409Conflict;
                return;
            }

            var returnUrl = context.Request.Path + context.Request.QueryString;
            context.Response.Redirect($"/groups/pick?returnUrl={Uri.EscapeDataString(returnUrl)}");
            return;
        }

        var validatedAtRaw = context.Session.GetString(SessionKeys.ActiveGroupValidatedAtUtc);
        var needsRevalidation = validatedAtRaw == null
            || !DateTime.TryParse(validatedAtRaw, System.Globalization.CultureInfo.InvariantCulture,
                   System.Globalization.DateTimeStyles.RoundtripKind, out var validatedAt)
            || DateTime.UtcNow - validatedAt > MembershipRevalidationInterval;

        if (needsRevalidation && !context.User.IsInRole("SuperAdmin"))
        {
            var userService = context.RequestServices.GetRequiredService<IUserService>();
            var userId = int.Parse(context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
            var role = await userService.GetGroupRoleByIdAsync(userId, groupContext.ActiveGroupId!.Value);

            if (role == null)
            {
                // No longer a member of the group their session still points to — clear the
                // stale group and gate the request exactly like the null-ActiveGroupId case above
                // (redirect on GET/HEAD, 409 on non-idempotent verbs, for the same body-loss reason).
                context.Session.Remove(SessionKeys.ActiveGroupId);
                context.Session.Remove(SessionKeys.ActiveGroupName);
                context.Session.Remove(SessionKeys.ActiveGroupValidatedAtUtc);

                if (!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method))
                {
                    context.Response.StatusCode = StatusCodes.Status409Conflict;
                    return;
                }

                var returnUrl = context.Request.Path + context.Request.QueryString;
                context.Response.Redirect($"/groups/pick?returnUrl={Uri.EscapeDataString(returnUrl)}");
                return;
            }

            context.Session.SetString(SessionKeys.ActiveGroupValidatedAtUtc, DateTime.UtcNow.ToString("O"));
        }

        await next(context);
    }
}
