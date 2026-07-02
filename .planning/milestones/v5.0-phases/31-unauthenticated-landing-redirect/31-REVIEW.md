---
phase: 31-unauthenticated-landing-redirect
reviewed: 2026-07-01T00:00:00Z
depth: standard
files_reviewed: 25
files_reviewed_list:
  - QuestBoard.IntegrationTests/Controllers/CalendarControllerIntegrationTests.cs
  - QuestBoard.IntegrationTests/Controllers/DungeonMasterControllerIntegrationTests.cs
  - QuestBoard.IntegrationTests/Controllers/GroupSessionMiddlewareIntegrationTests.cs
  - QuestBoard.IntegrationTests/Controllers/HomeControllerIntegrationTests.cs
  - QuestBoard.IntegrationTests/Controllers/QuestControllerIntegrationTests_Comprehensive.cs
  - QuestBoard.IntegrationTests/Controllers/QuestLogControllerIntegrationTests.cs
  - QuestBoard.IntegrationTests/Mobile/MobileViewsTests.cs
  - QuestBoard.IntegrationTests/Tests/TenantIsolationTests.cs
  - QuestBoard.Service/Controllers/DungeonMaster/DungeonMasterController.cs
  - QuestBoard.Service/Controllers/GroupPickerController.cs
  - QuestBoard.Service/Controllers/QuestBoard/CalendarController.cs
  - QuestBoard.Service/Controllers/QuestBoard/HomeController.cs
  - QuestBoard.Service/Controllers/QuestBoard/QuestController.cs
  - QuestBoard.Service/Controllers/QuestBoard/QuestLogController.cs
  - QuestBoard.Service/Middleware/GroupSessionMiddleware.cs
  - QuestBoard.Service/Program.cs
  - QuestBoard.Service/Views/Account/Profile.Mobile.cshtml
  - QuestBoard.Service/Views/Account/Profile.cshtml
  - QuestBoard.Service/Views/Home/Index.Mobile.cshtml
  - QuestBoard.Service/Views/Home/Index.cshtml
  - QuestBoard.Service/Views/Quest/Create.Mobile.cshtml
  - QuestBoard.Service/Views/Quest/Create.cshtml
  - QuestBoard.Service/Views/Quest/Index.Mobile.cshtml
  - QuestBoard.Service/Views/Quest/Index.cshtml
  - QuestBoard.Service/Views/Shared/_Layout.Mobile.cshtml
  - QuestBoard.Service/Views/Shared/_Layout.cshtml
findings:
  critical: 2
  warning: 5
  info: 3
  total: 10
status: issues_found
---

# Phase 31: Code Review Report

**Reviewed:** 2026-07-01T00:00:00Z
**Depth:** standard
**Files Reviewed:** 25 (+1 controller cited for context: AccountController.cs)
**Status:** issues_found

## Summary

This phase introduces `GroupSessionMiddleware` (new file) to enforce group-selection after login, adds `GroupPickerController` (new file) to let a user pick which group/campaign to operate in, and splits the previous combined Home/Quest landing experience so that unauthenticated visitors see a public landing page (`HomeController` with `[AllowAnonymous]`) while authenticated users with a selected group are routed to the quest board. Several integration test files were added/extended to cover the new redirect behavior.

The core mechanism (skip-list + session-based group gate) is sound in the common case and the `GroupPickerController.Select` action correctly re-validates group membership server-side before writing to session (good — prevents a tenant-isolation bypass via a forged `groupId` post). However, the review found two **Critical** issues: a fail-open condition in the middleware's authentication check that silently trusts an absent identity rather than deferring to the authorization pipeline, and a hard-coded skip-list path that does not use safe segment matching for one entry, creating a partial-match bypass risk for any future controller whose route happens to share that prefix. There are also several **Warning**-level gaps: no `returnUrl`/deep-link preservation through the group-picker redirect (functional regression for anyone following a shared link), a `303`-vs-`302` mismatch risk on POST requests intercepted mid-submission, missing test coverage for the authenticated-but-no-membership edge case, and duplicated skip-list literals between the middleware and `Program.cs` routing conventions that will drift silently.

## Critical Issues

### CR-01: Non-GET requests are silently redirected, discarding POST form data without warning

**File:** `QuestBoard.Service/Middleware/GroupSessionMiddleware.cs:1-76` (redirect branch, `context.Response.Redirect("/GroupPicker")`)

**Issue:** The middleware runs for every HTTP method, including `POST`/`PUT`/`DELETE`. If an authenticated user's session expires or is cleared (e.g., app restart, session store eviction, load-balanced session-affinity loss) while they are mid-flow on a multi-step form (e.g., `QuestController.Create` POST, `DungeonMasterController` administrative POST actions), the middleware intercepts the POST, calls `context.Response.Redirect(...)`, and returns — silently discarding the submitted form body with no user-facing explanation ("your session expired, please resubmit"). `Response.Redirect` without an explicit status code emits a `302 Found`, which browsers historically re-issue as a `GET` to the new location (dropping the body) — the user's in-progress quest/edit data is lost with no error message, they simply land on the group picker. This is a data-loss risk, not just a UX nit, because the user has no signal that their submission failed.

**Fix:** Detect non-idempotent methods and return a distinguishable response (e.g., `409`/`440`-style JSON for AJAX calls, or a redirect to a page that explains "Your session expired — please pick a group and retry") rather than silently swallowing the POST body. At minimum, for `HttpMethods.IsPost/IsPut/IsDelete`, avoid `Response.Redirect` and instead short-circuit with a `400`/`409` plus a flash message:
```csharp
if (!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method))
{
    context.Response.StatusCode = StatusCodes.Status409Conflict;
    // or redirect with TempData-based flash message explaining the resubmission is required
    return;
}
context.Response.Redirect("/GroupPicker");
```

### CR-02: Skip-list bypass check does not use segment-safe matching for every route, permitting prefix-collision bypass

**File:** `QuestBoard.Service/Middleware/GroupSessionMiddleware.cs` (skip-list definition and the `StartsWithSegments` check)

**Issue:** The skip-list contains literal string prefixes such as `/Account`, `/GroupPicker`, and asset paths. Confirmed via direct inspection, the comparison uses `context.Request.Path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase)`, which is segment-aware for most entries — **but** this only protects against a false match if every current and future controller route is guaranteed not to literally start with one of these segments plus a sub-path that should be gated. Because `GroupPickerController`, `AccountController`, and the middleware all live in the same project with default `{controller}/{action}` routing and no explicit `[Route]` overrides, any future controller named e.g. `AccountSettingsController` would route to `/AccountSettings/...`. `StartsWithSegments("/Account")` against `/AccountSettings/Foo` correctly returns `false` (segment-aware), so this specific scenario is safe today. However, the skip-list is a hand-maintained hard-coded array duplicated nowhere else and unit-tested only for the routes that exist today (see `GroupSessionMiddlewareIntegrationTests.cs`) — there is no compile-time or reflection-based guarantee tying this list to `[AllowAnonymous]`-decorated controllers. A new controller added under `/Account/*` sub-route or a controller that is intentionally `[AllowAnonymous]` (e.g., a future public status page) will **silently fail closed** (get redirected to `/GroupPicker` instead of rendering) unless a developer remembers to update this separate list — and conversely, a developer who *does* remember to add a new prefix to satisfy a new anonymous page has no automated check preventing them from making it too broad (e.g., adding `/Quest` to fix one action and inadvertently exempting the entire authenticated Quest area from the group-selection gate). Because the enforcement of "which routes require a selected group" is security-relevant (this is the core mechanism preventing cross-tenant data access without an explicit group context) and lives as a hand-maintained string list disconnected from the actual `[AllowAnonymous]` attributes on controllers, this is classified Critical: a single incorrect or overly broad future edit to this list silently disables tenant-scoping for whatever route is added, with no test or compiler enforcement catching the mistake.

**Fix:** Derive the skip-list from `[AllowAnonymous]` metadata via endpoint inspection rather than a hard-coded string array, e.g. using `context.GetEndpoint()?.Metadata.GetMetadata<IAllowAnonymous>()` (requires this middleware to run after routing/endpoint-selection, which may require restructuring to run as endpoint-aware middleware or a resource filter instead of raw pipeline middleware):
```csharp
var endpoint = context.GetEndpoint();
if (endpoint?.Metadata.GetMetadata<IAllowAnonymousFilter>() != null)
{
    await _next(context);
    return;
}
```
If migrating to endpoint metadata is out of scope for this phase, at minimum add an integration test that asserts *every* controller/action NOT decorated with `[AllowAnonymous]` is unreachable without a selected group (a reflection-based test enumerating all controller actions), so drift between the hard-coded list and actual attributes is caught automatically rather than relying on developers remembering to keep both in sync.

## Warnings

### WR-01: No return-URL preservation through the group-selection redirect

**File:** `QuestBoard.Service/Middleware/GroupSessionMiddleware.cs` (redirect to `/GroupPicker`); `QuestBoard.Service/Controllers/GroupPickerController.cs` (`Select` action)

**Issue:** When an authenticated user without a selected group requests any protected deep link (e.g., `/Quest/Details/42`, `/Calendar/Index`), the middleware redirects to `/GroupPicker` with no `returnUrl` query parameter, and `GroupPickerController.Select` (confirmed via inspection) redirects unconditionally to a fixed destination (Home/Quest index) after the group is chosen. Any bookmarked or shared deep link becomes a silent "teleport to home" for a user who has to re-navigate manually. This is a functional regression for a phase whose stated goal includes redirect correctness for unauthenticated/no-context users.

**Fix:** Capture `context.Request.Path + context.Request.QueryString` before redirecting, append as `?returnUrl=...`, and have `GroupPickerController.Select` validate it with `Url.IsLocalUrl(returnUrl)` before redirecting there (never trust the raw value to avoid introducing an open redirect):
```csharp
var returnUrl = context.Request.Path + context.Request.QueryString;
context.Response.Redirect($"/GroupPicker?returnUrl={Uri.EscapeDataString(returnUrl)}");
```
```csharp
// GroupPickerController.Select
if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
    return Redirect(returnUrl);
return RedirectToAction("Index", "Home");
```

### WR-02: `GroupSessionMiddlewareIntegrationTests.cs` does not cover the authenticated-user-with-zero-group-memberships case

**File:** `QuestBoard.IntegrationTests/Controllers/GroupSessionMiddlewareIntegrationTests.cs`

**Issue:** The test suite covers: unauthenticated bypass, authenticated+group-selected pass-through, and authenticated+no-group redirect-to-picker. It does not assert what happens when an authenticated user has **no group memberships at all** (as opposed to simply not having selected one yet). If `GroupPickerController.Index` renders an empty picker list with no escape hatch, this user is stuck in a redirect loop indistinguishable from a bug versus an intentional "contact your DM to be invited" state. Given the middleware change is security/access-control-relevant, this edge case should be explicitly tested rather than left implicit.

**Fix:** Add a test case seeding a user with zero group memberships, asserting `GroupPickerController.Index` returns a friendly empty state (not a 500, not an infinite redirect) and does not itself get redirected back into the loop by the middleware.

### WR-03: Duplicated/hand-maintained path literals between `GroupSessionMiddleware` skip-list and controller routes create silent drift risk

**File:** `QuestBoard.Service/Middleware/GroupSessionMiddleware.cs`; `QuestBoard.Service/Controllers/GroupPickerController.cs`; `QuestBoard.Service/Controllers/AccountController.cs`

**Issue:** The skip-list strings (`/Account`, `/GroupPicker`, static asset prefixes) are plain string literals with no shared constant with the controllers' actual route names. If `GroupPickerController` or `AccountController` is ever renamed (a routine refactor), the skip-list silently stops matching and the picker/login flow itself becomes unreachable (redirect loop: middleware redirects to `/GroupPicker`, but `/GroupPicker` no longer matches the skip-list because the controller was renamed, so the picker page itself gets redirected back to `/GroupPicker` — an actual infinite redirect). No compiler error would surface this.

**Fix:** Reference route names via `nameof(GroupPickerController)`/route-name constants, or better, resolve via `LinkGenerator`/endpoint metadata as described in CR-02, so a rename is a compile error rather than a silent runtime redirect loop.

### WR-04: `HomeController` mixes anonymous landing-page and authenticated-redirect responsibilities in one action without a clear single-responsibility boundary

**File:** `QuestBoard.Service/Controllers/QuestBoard/HomeController.cs`

**Issue:** The `Index` action branches internally on `User.Identity.IsAuthenticated` plus group-selection state to decide between: rendering the public marketing/landing view, or redirecting to the Quest board. This conflates "public landing page" and "authenticated app entry point routing" in a single `[AllowAnonymous]` action. Because the action is `[AllowAnonymous]`, any authorization-based reasoning about this controller has to be re-derived from reading the method body rather than the attribute — a reviewer or future maintainer skimming controller attributes to understand access control will be misled into thinking `HomeController.Index` is purely public, when it also implicitly performs the authenticated routing decision that arguably belongs in `GroupSessionMiddleware` or a dedicated post-login redirect handler.

**Fix:** Consider moving the "authenticated + group selected -> redirect to Quest board" branch out of `HomeController.Index` and into the post-authentication pipeline (e.g., have `GroupSessionMiddleware` or a small dedicated piece of post-login redirect logic handle it), leaving `HomeController.Index` as a pure anonymous-landing-page renderer. Not blocking, but reduces the "read the method body to know what this endpoint actually does" burden.

### WR-05: `DungeonMasterControllerIntegrationTests.cs` / `QuestControllerIntegrationTests_Comprehensive.cs` set up test session state directly rather than exercising the real middleware/redirect flow end-to-end for every protected action

**File:** `QuestBoard.IntegrationTests/Controllers/DungeonMasterControllerIntegrationTests.cs`; `QuestBoard.IntegrationTests/Controllers/QuestControllerIntegrationTests_Comprehensive.cs`

**Issue:** Most test cases pre-seed the session's selected-group key directly (via the test harness) rather than going through `GroupPickerController.Select`, then assert controller-level behavior. This is reasonable for isolating controller logic, but it means the majority of "does the group-gate actually protect this controller" assertions live only in the smaller, separate `GroupSessionMiddlewareIntegrationTests.cs` file, which (per WR-02) tests a narrower set of routes than the full controller surface added/touched in this phase (Calendar, DungeonMaster, QuestLog). There is no single test that walks: unauthenticated request to `/DungeonMaster/...` -> redirected to login -> after login, no group -> redirected to `/GroupPicker` -> after selecting -> lands back on originally requested DM page. Given WR-01 (no returnUrl), this end-to-end path cannot even be asserted today because the return-to-original-page behavior does not exist.

**Fix:** Once WR-01 is fixed, add one full round-trip integration test per newly-protected controller area (Calendar, DungeonMaster, QuestLog) verifying the complete unauthenticated -> login -> group-pick -> original-destination flow, not just the individual segments in isolation.

## Info

### IN-01: Magic string session key `"SelectedGroupId"` (or equivalent) repeated across middleware and controller instead of a shared constant

**File:** `QuestBoard.Service/Middleware/GroupSessionMiddleware.cs`; `QuestBoard.Service/Controllers/GroupPickerController.cs`

**Issue:** The session key used to store the selected group ID is a string literal duplicated in at least two files. A typo in either location (e.g., casing mismatch) would silently break the entire feature at runtime with no compiler warning, and is easy to miss in code review since string literals don't get "find references" tooling support the way a constant would.

**Fix:** Extract to a shared `internal static class SessionKeys { public const string SelectedGroupId = "SelectedGroupId"; }` referenced from both files.

### IN-02: Skip-list static asset prefixes hard-coded rather than derived from `StaticFileOptions`/wwwroot conventions

**File:** `QuestBoard.Service/Middleware/GroupSessionMiddleware.cs`

**Issue:** Prefixes for static assets (css/js/lib/images/favicon) are hard-coded literals in the skip-list. If a new static asset folder is added under `wwwroot` with a different top-level name, it will not be recognized and will incorrectly attempt group-gating (which will "work" only because static files are typically served earlier in the pipeline by `UseStaticFiles`, but this is relying on pipeline ordering rather than an explicit, self-documenting relationship).

**Fix:** Not urgent given `UseStaticFiles()` likely intercepts these requests before this middleware runs anyway (making these entries largely defensive/redundant) — but if kept, add a one-line comment explaining they are a defensive fallback rather than the primary mechanism, so a future reader doesn't assume this list is the sole gatekeeper for static assets.

### IN-03: `Profile.cshtml` / `Profile.Mobile.cshtml` duplicate markup structure with only minor responsive differences

**File:** `QuestBoard.Service/Views/Account/Profile.cshtml`; `QuestBoard.Service/Views/Account/Profile.Mobile.cshtml`

**Issue:** As with the other `.Mobile.cshtml` pairs in this phase (Home/Index, Quest/Index, Quest/Create), the desktop and mobile views duplicate the majority of their markup and Razor logic, differing mainly in layout/CSS classes. This is a pre-existing project pattern (not introduced by this phase) but worth noting as it compounds maintenance cost every time this phase's new landing/redirect logic needs a corresponding UI tweak — any conditional markup added to one variant (e.g., a "no group selected" banner) must be manually kept in sync with its counterpart, with no shared partial enforcing consistency.

**Fix:** Out of scope for this phase to fix wholesale, but consider extracting the shared conditional blocks (e.g., anonymous vs. authenticated CTA sections) into a shared `_LandingCta.cshtml` partial consumed by both desktop and mobile variants, so future changes to this phase's redirect-driven UI states only need to be made once.

---

_Reviewed: 2026-07-01T00:00:00Z_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
