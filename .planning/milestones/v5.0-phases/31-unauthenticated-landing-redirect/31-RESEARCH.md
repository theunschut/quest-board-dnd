# Phase 31: Unauthenticated Landing Redirect - Research

**Researched:** 2026-06-30
**Domain:** ASP.NET Core 10 MVC — authorization, middleware, controller/view refactoring
**Confidence:** HIGH

---

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**Route Lockdown**
- D-01: Add `[Authorize]` to `HomeController`, `CalendarController`, and `QuestLogController` at the class level. Matches existing pattern used by `ShopController` and `GuildMembersController`. No global fallback policy.
- D-02: Remove `[AllowAnonymous]` from the DM profile actions in `DungeonMasterController`. DMs are group-bound members — their profiles are group-private. Class-level `[Authorize]` already applies once the exemptions are removed.
- D-03: Only two categories of routes stay publicly accessible: (1) Auth routes: `/Account/Login`, `/Account/Logout`; (2) Error/infrastructure pages. All quest board, calendar, quest log, shop, guild, and DM profile routes require authentication.

**Root URL (/) Redesign**
- D-04: `HomeController.Index` becomes a simple public landing page — app name/tagline and "Log in" button. No `[Authorize]` on HomeController. The controller is intentionally public.
- D-05: Quest board moves to new `QuestController.Index` action at route `/quests`. Logic from `HomeController.Index` moves verbatim. `QuestController` already has `[Authorize]` on individual actions — confirm class-level or add to the new Index action.
- D-06: `Views/Home/Index.cshtml` and `Views/Home/Index.Mobile.cshtml` move to `Views/Quest/Index.cshtml` and `Views/Quest/Index.Mobile.cshtml`. New `Views/Home/Index.cshtml` and `Views/Home/Index.Mobile.cshtml` are created for the landing page.
- D-07: `GroupPickerController.SelectGroup` POST changes its fallback redirect from `RedirectToAction("Index", "Home")` to `RedirectToAction("Index", "Quest")` (i.e. `/quests`). All returnUrl logic preserved.
- D-08: Any nav links or `RedirectToAction("Index", "Home")` calls in the codebase that pointed to the quest board must be updated to point to `("/quests")` or `RedirectToAction("Index", "Quest")`.

**Expired Session Recovery Middleware**
- D-09: Add middleware AFTER `UseAuthentication` (and after `UseSession`) in Program.cs: authenticated user + no `ActiveGroupId` in `ISession` + non-exempt path → redirect to `/groups/pick`.
- D-10: Exempt routes for the session recovery middleware: `/groups/pick`, `/Account/Login`, `/Account/Logout`, `/platform/*`, error/infrastructure routes, static files (already handled before middleware by `UseStaticFiles`).
- D-11: Behavior by user type: single-group user auto-selects and redirects to `/quests` (seamless); multi-group sees picker; SuperAdmin lands on picker. Middleware must NOT fire for SuperAdmin — check `User.IsInRole("SuperAdmin")` alongside path exemptions.

### Claude's Discretion

- Exact name for the new middleware class or inline lambda in Program.cs (e.g. `GroupSessionMiddleware` or inline `app.Use(...)`)
- Whether `QuestController.Index` gets a class-level `[Authorize]` added or just an action-level `[Authorize]` on the new Index action (depends on existing controller structure — planner should check)
- Visual design of the public landing page at `/` — simple card with app name and login button; exact copy and styling at planner's discretion

### Deferred Ideas (OUT OF SCOPE)

None — discussion stayed within phase scope.
</user_constraints>

---

## Summary

Phase 31 is a refactoring phase with three distinct workstreams: (1) class-level authorization lockdown on currently-open controllers; (2) moving the quest board view from `HomeController.Index` to a new `QuestController.Index`, replacing the home route with a public landing page; and (3) introducing session-recovery middleware that catches authenticated users with an expired group session and redirects them to the group picker.

All three workstreams are purely additive or surgical — no schema changes, no new services, no new npm packages. The code changes are confined to `QuestBoard.Service`. The main complexity is the completeness sweep: every `RedirectToAction("Index", "Home")` call and every `asp-controller="Home" asp-action="Index"` reference in the codebase must be updated so no link sends users back to the landing page instead of `/quests`.

The session-recovery middleware is the most behaviorally subtle piece. It must cooperate correctly with ASP.NET Identity's own auth challenge (which also redirects to login) and must not loop for SuperAdmin users, who legitimately have `ActiveGroupId = null`. The existing Hangfire path guard in Program.cs is the direct pattern to follow.

**Primary recommendation:** Implement as three sequential plans: (31-01) controller auth lockdown + DM profile cleanup, (31-02) quest board migration + landing page + Home/Quest reference updates, (31-03) session-recovery middleware + tests.

---

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Route authentication (unauthenticated redirect) | API / Backend (MVC controllers) | — | `[Authorize]` attribute on controller class; ASP.NET Identity handles the redirect to `/Account/Login` |
| Session-recovery redirect (expired group session) | API / Backend (middleware) | — | Custom middleware in the request pipeline reads session and redirects before the controller executes |
| Public landing page at `/` | Frontend Server (Razor view) | — | `HomeController.Index` is now `[AllowAnonymous]`; the view renders without group-scoped data |
| Quest board at `/quests` | Frontend Server (Razor view) | API / Backend (controller) | `QuestController.Index` — same logic as old `HomeController.Index`, now requires auth |
| Navigation link updates | Frontend Server (Razor views) | — | Layout files and Cancel buttons need `asp-controller="Quest"` or `/quests` |

---

## Standard Stack

This phase installs no external packages. All work uses existing project dependencies.

### Core (already installed)

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Microsoft.AspNetCore.Authorization | Built into ASP.NET Core 10 | `[Authorize]` class-level attributes | Framework built-in |
| Microsoft.AspNetCore.Http | Built into ASP.NET Core 10 | `ISession`, `RequestDelegate`, `HttpContext` in middleware | Framework built-in |
| Microsoft.AspNetCore.Mvc.Razor | Built into ASP.NET Core 10 | Razor views for landing page and quest board | Framework built-in |

### Package Legitimacy Audit

No external packages are installed in this phase. This section is not applicable.

---

## Architecture Patterns

### System Architecture Diagram

```
Unauthenticated request to /quests (or /Calendar, etc.)
         │
         ▼
UseStaticFiles ────► (static assets served and short-circuit here)
         │
UseRouting
         │
UseSession
         │
UseAuthentication ──► identity cookie / header resolved
         │
[NEW] GroupSessionMiddleware
    ├── authenticated? No  ──► continue (MVC [Authorize] handles redirect)
    ├── IsSuperAdmin?  Yes ──► continue (SuperAdmin has null ActiveGroupId by design)
    ├── path exempt?   Yes ──► continue (/Account/*, /groups/pick, /platform/*, /Error)
    ├── ActiveGroupId in session? Yes ──► continue
    └── none of the above ──► redirect to /groups/pick
         │
UseAuthorization
    └── [Authorize] on controller ──► unauthenticated → redirect /Account/Login
         │
Controller action executes
    ├── GET /  ──► HomeController.Index (public landing page)
    ├── GET /quests ──► QuestController.Index (authenticated, group-scoped)
    └── GET /GroupPicker/Index ──► GroupPickerController.Index
              ├── single-group: auto-select + redirect to /quests
              ├── multi-group: show picker
              └── SuperAdmin: show all groups + platform link
```

### Recommended Project Structure (changes only)

```
QuestBoard.Service/
├── Controllers/
│   └── QuestBoard/
│       ├── HomeController.cs            ← MODIFY: remove quest logic, add landing page action
│       ├── QuestController.cs           ← MODIFY: add Index action with migrated quest logic
│       ├── CalendarController.cs        ← MODIFY: add class-level [Authorize]
│       └── QuestLogController.cs        ← MODIFY: add class-level [Authorize]
│   ├── GroupPickerController.cs         ← MODIFY: update fallback redirect in RedirectToLocal
│   └── DungeonMaster/
│       └── DungeonMasterController.cs   ← MODIFY: remove [AllowAnonymous] from Profile + GetDMProfilePicture
├── Middleware/
│   └── MobileDetectionMiddleware.cs     ← EXISTING (reference for middleware class pattern)
│   └── GroupSessionMiddleware.cs        ← NEW (or inline lambda in Program.cs)
├── Views/
│   └── Home/
│       ├── Index.cshtml                 ← REPLACE: new public landing page
│       └── Index.Mobile.cshtml          ← REPLACE: new public landing page (mobile)
│   └── Quest/
│       ├── Index.cshtml                 ← NEW: migrated from Views/Home/Index.cshtml
│       └── Index.Mobile.cshtml          ← NEW: migrated from Views/Home/Index.Mobile.cshtml
│   └── Shared/
│       ├── _Layout.cshtml               ← MODIFY: navbar brand link → /quests
│       └── _Layout.Mobile.cshtml        ← MODIFY: navbar brand link → /quests
│   └── Quest/
│       ├── Create.cshtml                ← MODIFY: Cancel button link → /quests
│       └── Create.Mobile.cshtml         ← MODIFY: Cancel button link → /quests
└── Program.cs                           ← MODIFY: insert GroupSessionMiddleware
```

### Pattern 1: Class-Level `[Authorize]` (existing)

**What:** Apply `[Authorize]` on the controller class; individual actions needing stricter policies add their own `[Authorize(Policy = "...")]`.
**When to use:** Controller where all actions require the same base auth level.
**Example (from ShopController — the reference pattern):**

```csharp
// Source: QuestBoard.Service/Controllers/Shop/ShopController.cs
[Authorize]                // class-level — all actions require auth
public class ShopController(IShopService shopService, IUserService userService, IMapper mapper) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(...) { ... }  // inherits [Authorize]
}
```

Apply identically to `HomeController` (for the new landing — DO NOT add `[Authorize]`, see D-04), `CalendarController`, and `QuestLogController`.

### Pattern 2: Inline Middleware Lambda (existing — Hangfire guard)

**What:** `app.Use(async (context, next) => { ... await next(); })` in Program.cs for a path-specific gate.
**When to use:** Simple request-pipeline logic that doesn't warrant a full class.
**Example (Hangfire guard in Program.cs — direct reference pattern):**

```csharp
// Source: QuestBoard.Service/Program.cs lines 180–204
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/hangfire"))
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            context.Response.Redirect("/Account/Login");
            return;
        }
        if (!context.User.IsInRole("Admin") && !context.User.IsInRole("SuperAdmin"))
        {
            context.Response.Redirect("/Account/Login");
            return;
        }
    }
    await next();
});
```

The session-recovery middleware follows the same inline pattern. The guard logic is: `IsAuthenticated == true AND NOT SuperAdmin AND NOT path-exempt AND ActiveGroupId == null → redirect to /groups/pick`.

### Pattern 3: Named Middleware Class (existing — MobileDetectionMiddleware)

**What:** A class with `InvokeAsync(HttpContext context)` registered via `app.UseMiddleware<T>()`.
**When to use:** Middleware with logic complex enough to warrant its own file and testability.
**Example:**

```csharp
// Source: QuestBoard.Service/Middleware/MobileDetectionMiddleware.cs
public class MobileDetectionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        // ... logic ...
        await next(context);
    }
}
```

If the session-recovery logic is more complex than the inline Hangfire guard, place it in `QuestBoard.Service/Middleware/GroupSessionMiddleware.cs` and register with `app.UseMiddleware<GroupSessionMiddleware>()`.

### Anti-Patterns to Avoid

- **Adding `[Authorize]` to `HomeController`:** D-04 is explicit — the landing page at `/` must be public. Do not add `[Authorize]` at the class level or on the `Index` action.
- **Returning `[AllowAnonymous]` on DM profile actions:** After D-02, the class-level `[Authorize]` on `DungeonMasterController` already covers these. The `[AllowAnonymous]` on `Profile` and `GetDMProfilePicture` must be removed.
- **Redirecting SuperAdmin to `/groups/pick` from middleware:** SuperAdmin users have `ActiveGroupId = null` by design. Check `User.IsInRole("SuperAdmin")` BEFORE checking the session. Failure to do so causes an infinite loop: middleware redirects → user arrives at `/groups/pick` → middleware fires again → loop.
- **Missing the `/groups/pick` path in exempt routes:** The destination of the middleware redirect must itself be exempt — `context.Request.Path.StartsWithSegments("/groups/pick")` must be an early-out in the guard.
- **Leaving `RedirectToAction("Index", "Home")` in `QuestController.Create` POST:** Line 69 of `QuestController.cs` currently returns `RedirectToAction("Index", "Home")` after creating a quest. This sends the DM to the public landing page instead of the quest board. Must be updated to `RedirectToAction("Index")` (within the same controller) or `RedirectToAction("Index", "Quest")`.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Auth challenge (unauthenticated redirect) | Custom redirect logic in each controller | `[Authorize]` attribute | ASP.NET Identity redirects to configured login path automatically |
| Session key constants | Hardcoded strings | `SessionKeys.ActiveGroupId` (already exists in `QuestBoard.Service/Constants/SessionKeys.cs`) | Typo-safe; one place to change |
| Mobile view parity | Trying to skip Mobile variant | Follow existing `.Mobile.cshtml` pattern for every new view | `MobileViewLocationExpander` selects it automatically based on user-agent — missing it causes desktop view on mobile |

**Key insight:** The ASP.NET Identity pipeline already handles the redirect to `/Account/Login` when an unauthenticated user hits `[Authorize]`. The middleware only needs to handle the separate case of an *authenticated* user with no group session.

---

## Complete Reference: All Files That Must Change

This is the exhaustive list the planner must translate into tasks.

### Controllers (C# files)

| File | Change | Decision |
|------|--------|----------|
| `Controllers/QuestBoard/HomeController.cs` | Replace quest board action with simple landing page action returning no model or a trivial model | D-04, D-05 |
| `Controllers/QuestBoard/QuestController.cs` | Add new `Index` `[HttpGet]` action; migrate `HomeController.Index` logic verbatim; update `Create POST` redirect from `"Home"` to `"Quest"` | D-05, D-08 |
| `Controllers/QuestBoard/CalendarController.cs` | Add `[Authorize]` at class level | D-01 |
| `Controllers/QuestBoard/QuestLogController.cs` | Add `[Authorize]` at class level | D-01 |
| `Controllers/DungeonMaster/DungeonMasterController.cs` | Remove `[AllowAnonymous]` from `Profile` and `GetDMProfilePicture` actions | D-02 |
| `Controllers/GroupPickerController.cs` | Change `RedirectToAction("Index", "Home")` in `RedirectToLocal` to `RedirectToAction("Index", "Quest")` | D-07 |

### Views (Razor files)

| File | Change | Decision |
|------|--------|----------|
| `Views/Home/Index.cshtml` | Replace quest board content with simple landing page HTML (app name + Login button) | D-04, D-06 |
| `Views/Home/Index.Mobile.cshtml` | Replace quest board content with simple mobile landing page | D-04, D-06 |
| `Views/Quest/Index.cshtml` | NEW FILE — copy of current `Views/Home/Index.cshtml` (quest board with poster cards) | D-05, D-06 |
| `Views/Quest/Index.Mobile.cshtml` | NEW FILE — copy of current `Views/Home/Index.Mobile.cshtml` (quest list mobile) | D-05, D-06 |
| `Views/Shared/_Layout.cshtml` | Line 24: `asp-controller="Home" asp-action="Index"` → `asp-controller="Quest" asp-action="Index"` | D-08 |
| `Views/Shared/_Layout.Mobile.cshtml` | Line 20: `asp-controller="Home" asp-action="Index"` → `asp-controller="Quest" asp-action="Index"` | D-08 |
| `Views/Quest/Create.cshtml` | Line 84: `Url.Action("Index", "Home")` → `Url.Action("Index", "Quest")` | D-08 |
| `Views/Quest/Create.Mobile.cshtml` | Line 100: `Url.Action("Index", "Home")` → `Url.Action("Index", "Quest")` | D-08 |

### Program.cs

| Change | Decision |
|--------|----------|
| Insert session-recovery middleware AFTER `app.UseAuthentication()` and BEFORE `app.UseAuthorization()` | D-09 |

### Integration Tests

| File | Change |
|------|--------|
| `Controllers/HomeControllerIntegrationTests.cs` | Three existing tests break: `Index_ShouldReturnSuccessStatusCode`, `Index_ShouldReturnHtmlContent`, `Index_WithQuests_ShouldDisplayQuestList` — must be updated (the home page no longer shows quests). Also update `NonExistentRoute_ShouldReturn404`. |
| `Controllers/CalendarControllerIntegrationTests.cs` | All tests currently use unauthenticated client — after D-01 they will get 302 instead of 200. Tests need authenticated clients. |
| `Controllers/QuestLogControllerIntegrationTests.cs` | All tests currently use unauthenticated client — after D-01 they will get 302 instead of 200. Tests need authenticated clients. |
| NEW: `Controllers/QuestControllerIntegrationTests.cs` (or add to existing) | Add tests for `GET /quests` — authenticated returns 200, unauthenticated returns 302. |
| NEW: `Controllers/HomeControllerIntegrationTests.cs` | Replace quest-display tests with landing page tests: unauthenticated GET `/` returns 200, content contains login button. |
| NEW: Middleware tests | `GET /Quest` (authenticated, no group session) → 302 to `/groups/pick`; `GET /groups/pick` (authenticated, no group session) → 200 (exempt); `GET /Quest` (SuperAdmin, no group session) → NOT redirected by middleware. |

---

## Exact Code Inspection Results

### `HomeController.cs` (current state — full content)

```csharp
// Source: QuestBoard.Service/Controllers/QuestBoard/HomeController.cs
public class HomeController(IQuestService questService, IUserService userService) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken token = default)
    {
        // Get current user if authenticated to check if they're a DM and for signup status
        string? currentUserName = null;
        int? currentUserId = null;
        Role userRole = Role.Player;

        if (User.Identity?.IsAuthenticated == true)
        {
            var userEntity = await userService.GetUserAsync(User);
            if (userEntity != null)
            {
                var user = await userService.GetByIdAsync(userEntity.Id, token);
                currentUserName = user?.Name;
                currentUserId = user?.Id;
                var isAdmin = await userService.IsInRoleAsync(User, "Admin");
                var isDungeonMaster = await userService.IsInRoleAsync(User, "DungeonMaster");
                if (isAdmin) userRole = Role.Admin;
                else if (isDungeonMaster) userRole = Role.DungeonMaster;
            }
        }

        var isAdminOrDm = userRole == Role.Admin || userRole == Role.DungeonMaster;
        var quests = await questService.GetQuestsWithSignupsForRoleAsync(isAdminOrDm, token);

        ViewBag.CurrentUserName = currentUserName;
        ViewBag.CurrentUserId = currentUserId;
        return View(quests);
    }
}
```

**After Phase 31:** This action becomes a simple landing page. The new `QuestController.Index` takes the quest logic above verbatim. The new `HomeController.Index` returns an empty view or a minimal view model (no model needed, or perhaps a trivial anonymous object). No `[Authorize]` on HomeController.

### `QuestController.cs` — current state relevant to this phase

- No class-level `[Authorize]` — all existing actions have individual `[Authorize]` or `[Authorize(Policy = ...)]` attributes.
- No `Index` action exists — adding one is new. Route will be `/Quest/Index` by default MVC convention, but `/quests` requires either a route template or a global route alias.
- **Route resolution for `/quests`:** The default MVC route is `{controller}/{action}/{id?}`. For `QuestController.Index`, the route would be `/Quest` or `/Quest/Index` under the default convention. To get `/quests`, either: (a) add `[Route("quests")]` attribute on the new action, or (b) add a custom route mapping in `Program.cs` before the default route. Option (a) is simpler and self-contained.
- `QuestController.Create POST` (line 69) currently returns `RedirectToAction("Index", "Home")` — must change to `RedirectToAction("Index")` (same controller, resolves to `QuestController.Index`).

### `DungeonMasterController.cs` — exact attributes to remove

```csharp
// Class has [Authorize] at line 9
[Authorize]
public class DungeonMasterController(...) : Controller

// REMOVE [AllowAnonymous] from:
[HttpGet]
[AllowAnonymous]   ← REMOVE THIS
public async Task<IActionResult> Profile(int id, ...) { ... }

// REMOVE [AllowAnonymous] from:
[HttpGet]
[AllowAnonymous]   ← REMOVE THIS
public async Task<IActionResult> GetDMProfilePicture(int id, ...) { ... }

// These two stay as-is (already use DungeonMasterOnly policy):
[HttpGet]
[Authorize(Policy = "DungeonMasterOnly")]
public async Task<IActionResult> EditProfile(...) { ... }

[HttpPost]
[ValidateAntiForgeryToken]
[Authorize(Policy = "DungeonMasterOnly")]
public async Task<IActionResult> EditProfile(...) { ... }
```

### `GroupPickerController.cs` — exact change

```csharp
// BEFORE (line 59):
private IActionResult RedirectToLocal(string? returnUrl)
{
    if (Url.IsLocalUrl(returnUrl))
        return Redirect(returnUrl);
    else
        return RedirectToAction("Index", "Home");   // ← CHANGE THIS
}

// AFTER:
private IActionResult RedirectToLocal(string? returnUrl)
{
    if (Url.IsLocalUrl(returnUrl))
        return Redirect(returnUrl);
    else
        return RedirectToAction("Index", "Quest");  // ← NEW
}
```

### `Program.cs` — middleware insertion point

```csharp
// Current pipeline order (lines 168–176):
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseMiddleware<MobileDetectionMiddleware>();
app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();   // ← insert middleware BEFORE this line

// Required insertion:
app.UseSession();
app.UseAuthentication();
// INSERT HERE: session-recovery middleware
app.UseAuthorization();
```

**Session-recovery middleware logic:**

```csharp
// Option A: inline lambda (follows Hangfire guard pattern)
app.Use(async (context, next) =>
{
    if (context.User.Identity?.IsAuthenticated == true
        && !context.User.IsInRole("SuperAdmin")
        && !context.Request.Path.StartsWithSegments("/groups/pick")
        && !context.Request.Path.StartsWithSegments("/Account")
        && !context.Request.Path.StartsWithSegments("/platform")
        && !context.Request.Path.StartsWithSegments("/Error")
        && context.Session.GetInt32(SessionKeys.ActiveGroupId) == null)
    {
        context.Response.Redirect("/groups/pick");
        return;
    }
    await next();
});
```

Note: `context.Session` is accessible here because `UseSession()` runs before this middleware.

### Layout files — exact references to update

**`_Layout.cshtml` line 24:**
```html
<!-- BEFORE -->
<a class="navbar-brand" asp-controller="Home" asp-action="Index">

<!-- AFTER -->
<a class="navbar-brand" asp-controller="Quest" asp-action="Index">
```

**`_Layout.Mobile.cshtml` line 20:**
```html
<!-- BEFORE -->
<a class="navbar-brand" asp-controller="Home" asp-action="Index">

<!-- AFTER -->
<a class="navbar-brand" asp-controller="Quest" asp-action="Index">
```

**`Views/Quest/Create.cshtml` line 84:**
```html
<!-- BEFORE -->
<a href="@Url.Action("Index", "Home")" class="btn btn-secondary">

<!-- AFTER -->
<a href="@Url.Action("Index", "Quest")" class="btn btn-secondary">
```

**`Views/Quest/Create.Mobile.cshtml` line 100:**
```html
<!-- BEFORE -->
<a href="@Url.Action("Index", "Home")" class="btn btn-secondary flex-fill">

<!-- AFTER -->
<a href="@Url.Action("Index", "Quest")" class="btn btn-secondary flex-fill">
```

---

## Common Pitfalls

### Pitfall 1: Route for `/quests` Is Not Automatic
**What goes wrong:** The default MVC route maps `QuestController.Index` to `/Quest` or `/Quest/Index`, not `/quests`. The app works but links written as `/quests` 404.
**Why it happens:** Default route template is `{controller}/{action}/{id?}`, which produces `/Quest/Index` for `QuestController.Index`.
**How to avoid:** Add `[Route("quests")]` on the new `QuestController.Index` action to produce the exact URL `/quests`.
**Warning signs:** `Url.Action("Index", "Quest")` generates `/Quest/Index` instead of `/quests`.

### Pitfall 2: SuperAdmin Gets Stuck in Redirect Loop
**What goes wrong:** SuperAdmin navigates to any group-scoped page, middleware fires (ActiveGroupId is null), redirects to `/groups/pick`, middleware fires again, infinite loop.
**Why it happens:** SuperAdmin has `ActiveGroupId = null` by design (see `ActiveGroupContextService`). If the middleware only checks path and not role, it catches SuperAdmin users.
**How to avoid:** Check `User.IsInRole("SuperAdmin")` as the FIRST condition in the middleware. If true, call `await next()` immediately.
**Warning signs:** SuperAdmin session gets "Maximum request redirect" browser error, or middleware test for SuperAdmin fails.

### Pitfall 3: Session Not Available Before `UseSession()` Runs
**What goes wrong:** `context.Session.GetInt32(...)` throws `InvalidOperationException: Session has not been configured`.
**Why it happens:** Session middleware must call `UseSession()` before the custom middleware accesses `context.Session`.
**How to avoid:** Insert the session-recovery middleware AFTER `app.UseSession()`. The current pipeline already has `UseSession` before `UseAuthentication`, so inserting after `UseAuthentication` is correct.
**Warning signs:** Runtime exception on any request when middleware runs.

### Pitfall 4: Integration Tests for CalendarController and QuestLogController Break
**What goes wrong:** All existing tests for `CalendarController` and `QuestLogController` use an unauthenticated `HttpClient` (created via `factory.CreateNonRedirectingClient()` without an auth header). After D-01 adds `[Authorize]` at class level, these requests return 302 instead of 200, and all test assertions fail.
**Why it happens:** Tests were written for the pre-Phase-31 behavior where these controllers were publicly accessible.
**How to avoid:** Update test classes to use `AuthenticationHelper.CreateAuthenticatedClientWithUserAsync` instead of the unauthenticated factory client.
**Warning signs:** `response.StatusCode.Should().Be(HttpStatusCode.OK)` failures on Calendar and QuestLog tests after controller changes.

### Pitfall 5: DM Profile Test Breaks After `[AllowAnonymous]` Removal
**What goes wrong:** `DungeonMasterControllerIntegrationTests` has tests for `Profile` that hit `/DungeonMaster/Profile/{id}`. After removing `[AllowAnonymous]`, unauthenticated access returns 302/401 instead of 200.
**Why it happens:** The existing test `Profile_WithValidDmUserId_ReturnsOk` uses an authenticated client (already does `CreateAuthenticatedClientWithUserAsync`) so it should survive. But `Profile_WithNonExistentUserId_ReturnsNotFound` uses an authenticated client too — should be fine. Check if any test hits `GetDMProfilePicture` without auth.
**How to avoid:** Verify all DungeonMasterController tests use authenticated clients before removing `[AllowAnonymous]`.
**Warning signs:** `Profile_` tests returning 302 instead of 200/404.

### Pitfall 6: Landing Page Navbar Shows Quest Board Links for Unauthenticated Users
**What goes wrong:** `_Layout.cshtml` shows "Shop", "Quest Log", "Guild Members", etc. for unauthenticated users if the `@if (User.Identity?.IsAuthenticated == true)` guard in the navbar is absent or incorrect.
**Why it happens:** The layout already guards all authenticated nav items — but if the navbar brand link is updated to `/quests` and not `/`, an unauthenticated user clicking "D&D Quest Board" in the nav gets redirected to login.
**How to avoid:** Update navbar brand to point to `/quests` (as per D-08), which is correct — authenticated users expect to go to the quest board. For the unauthenticated state, the landing page itself provides the login CTA.
**Warning signs:** Unauthenticated users clicking the navbar brand get an unexpected redirect to login instead of viewing the landing page.

### Pitfall 7: `/groups/pick` Route Must Match Exactly
**What goes wrong:** The middleware checks `StartsWithSegments("/groups/pick")` but GroupPickerController uses the route `/GroupPicker/Index` by convention.
**Why it happens:** MVC default route template produces `/GroupPicker/Index` for `GroupPickerController.Index`. The application already has this established — checking `/groups/pick` in the middleware exempt list would not match.
**How to avoid:** In D-09, the CONTEXT.md says "exempt from middleware: `/groups/pick`". However the actual route is `/GroupPicker` (or `/GroupPicker/Index`). The middleware exempt path must match the actual route. Verify `GroupPickerController.cs` route and ensure the exempt path check uses the actual URL, e.g. `StartsWithSegments("/GroupPicker")`.
**Warning signs:** Users are redirected from `/GroupPicker/Index` in an infinite loop — the GroupPicker page itself triggers the middleware.

---

## Code Examples

### New `QuestController.Index` action

```csharp
// Action-level [Authorize] (since QuestController has no class-level [Authorize])
[HttpGet]
[Route("quests")]      // produces /quests instead of /Quest/Index
[Authorize]
public async Task<IActionResult> Index(CancellationToken token = default)
{
    // Get current user — same logic as old HomeController.Index
    string? currentUserName = null;
    int? currentUserId = null;
    Role userRole = Role.Player;

    var userEntity = await userService.GetUserAsync(User);
    if (userEntity != null)
    {
        var user = await userService.GetByIdAsync(userEntity.Id, token);
        currentUserName = user?.Name;
        currentUserId = user?.Id;

        var isAdmin = await userService.IsInRoleAsync(User, "Admin");
        var isDungeonMaster = await userService.IsInRoleAsync(User, "DungeonMaster");

        if (isAdmin) userRole = Role.Admin;
        else if (isDungeonMaster) userRole = Role.DungeonMaster;
    }

    var isAdminOrDm = userRole == Role.Admin || userRole == Role.DungeonMaster;
    var quests = await questService.GetQuestsWithSignupsForRoleAsync(isAdminOrDm, token);

    ViewBag.CurrentUserName = currentUserName;
    ViewBag.CurrentUserId = currentUserId;
    return View(quests);
}
```

Note: Since the user is now always authenticated (action has `[Authorize]`), the `if (User.Identity?.IsAuthenticated == true)` guard from `HomeController.Index` is no longer needed — the null checks on `userEntity` are still good practice.

### New `HomeController.Index` (public landing page)

```csharp
// No [Authorize] — intentionally public (D-04)
public class HomeController : Controller
{
    [HttpGet]
    public IActionResult Index()
    {
        return View();
    }
}
```

No service dependencies needed. The landing page view is static HTML.

### New `Views/Home/Index.cshtml` (landing page)

```html
@{
    ViewData["Title"] = "Welcome";
}
<div class="row justify-content-center mt-5">
    <div class="col-md-6 col-lg-4">
        <div class="card modern-card text-center">
            <div class="card-header modern-card-header">
                <h2 class="mb-0">
                    <i class="fas fa-dice-d20 text-warning me-2"></i>
                    D&D Quest Board
                </h2>
            </div>
            <div class="card-body modern-card-body">
                <p class="lead mb-4">Your campaign management hub.</p>
                <hr>
                <div class="d-grid">
                    <a asp-controller="Account" asp-action="Login" class="btn btn-warning btn-lg">
                        <i class="fas fa-sign-in-alt me-2"></i>Log In
                    </a>
                </div>
            </div>
        </div>
    </div>
</div>
```

### New `Views/Home/Index.Mobile.cshtml` (landing page mobile)

```html
@{
    ViewData["Title"] = "Welcome";
}
<div class="container-fluid px-2 mt-4">
    <div class="text-center mb-4">
        <i class="fas fa-dice-d20 fa-3x text-warning mb-3"></i>
        <h4>D&D Quest Board</h4>
        <p class="text-muted">Your campaign management hub.</p>
    </div>
    <div class="d-grid">
        <a asp-controller="Account" asp-action="Login" class="btn btn-warning btn-lg">
            <i class="fas fa-sign-in-alt me-2"></i>Log In
        </a>
    </div>
</div>
```

---

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Public quest board at `/` (authenticated optional) | Public landing page at `/`, quest board at `/quests` (auth required) | Phase 31 | Unauthenticated users see landing page, not empty/broken quest board |
| DM profiles publicly accessible via `[AllowAnonymous]` | DM profiles require auth (group-private) | Phase 31 | Consistent with group-isolation model from Phase 28/30 |
| Authenticated user with no group session hits quest board, gets broken UI | Middleware catches this case, redirects to group picker | Phase 31 | Seamless session recovery without exposing broken state |

---

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | Route `/groups/pick` does NOT exist as a custom route; actual route for `GroupPickerController.Index` is `/GroupPicker/Index` | Pitfall 7 | Middleware exempt-path check would use wrong string; SuperAdmin/group-picker users get looped |
| A2 | `QuestController` has no class-level `[Authorize]`; all authorization is per-action | Code Examples | If there IS a class-level `[Authorize]`, the new `Index` action inherits it and no additional attribute needed |

> **A1 must be verified by the planner** before writing the session-recovery middleware. Run: `grep -r "Route\|groups/pick" QuestBoard.Service/Controllers/GroupPickerController.cs`. The actual URL from the default route template is `/GroupPicker` (since action=Index is the default).

---

## Open Questions (RESOLVED)

1. **`/groups/pick` vs `/GroupPicker/Index` — which URL does the middleware actually need to exempt?**
   - What we know: `GroupPickerController.cs` has no `[Route]` attribute. Default MVC route produces `/GroupPicker/Index` (or `/GroupPicker` since Index is the default action).
   - What's unclear: The CONTEXT.md says exempt `/groups/pick`, but that URL doesn't match the actual controller route without a custom route attribute.
   - RESOLVED: Add `[Route("groups/pick")]` to `GroupPickerController.Index` so the vanity URL becomes real. Middleware exempts BOTH `/groups/pick` and `/GroupPicker` via `StartsWithSegments` to cover both the conventional and vanity routes. Implemented in Plan 31-03.

2. **`QuestController` — class-level or action-level `[Authorize]` for the new `Index`?**
   - What we know: All existing actions in `QuestController` have individual `[Authorize]` or `[Authorize(Policy = ...)]` attributes. There is no class-level `[Authorize]`.
   - What's unclear: Should Phase 31 add a class-level `[Authorize]` to `QuestController` (covering `Details` GET which currently has no auth attribute and works for unauthenticated users), or add an action-level `[Authorize]` only to the new `Index` action?
   - RESOLVED: Action-level `[Authorize]` on the new `Index` action only. Class-level would change auth behavior of `QuestController.Details` GET beyond Phase 31 scope. Implemented in Plan 31-02.

---

## Environment Availability

Step 2.6: SKIPPED — this phase involves only code/config changes within the existing project. No external dependencies, databases, or CLI tools beyond the existing dotnet toolchain are required.

---

## Validation Architecture

### Test Framework

| Property | Value |
|----------|-------|
| Framework | xUnit v3 + FluentAssertions + ASP.NET Core integration test host |
| Config file | `QuestBoard.IntegrationTests/QuestBoard.IntegrationTests.csproj` |
| Quick run command | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~HomeController\|QuestController\|Calendar\|QuestLog\|DungeonMaster\|GroupPicker\|GroupSession" --no-build` |
| Full suite command | `dotnet test` |

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| D-01 | `/Calendar` requires auth — unauthenticated → 302 | integration | `dotnet test --filter "CalendarController"` | Exists — update |
| D-01 | `/QuestLog` requires auth — unauthenticated → 302 | integration | `dotnet test --filter "QuestLogController"` | Exists — update |
| D-02 | `/DungeonMaster/Profile/{id}` requires auth — unauthenticated → 302 | integration | `dotnet test --filter "DungeonMasterController"` | Exists — check |
| D-04 | `GET /` returns 200 for unauthenticated user | integration | `dotnet test --filter "HomeController"` | Exists — update |
| D-04 | Landing page contains Login button, no quest cards | integration | `dotnet test --filter "HomeController"` | Exists — update |
| D-05 | `GET /quests` returns 200 for authenticated user | integration | `dotnet test --filter "QuestController"` | Exists — add test |
| D-05 | `GET /quests` returns 302 for unauthenticated user | integration | `dotnet test --filter "QuestController"` | Exists — add test |
| D-07 | `POST /GroupPicker/SelectGroup` with no returnUrl redirects to `/quests` | integration | `dotnet test --filter "GroupPickerController"` | Exists — add assertion |
| D-09 | Authenticated user with no session accessing `/quests` → 302 to `/GroupPicker` | integration | `dotnet test --filter "GroupSession"` | NEW FILE |
| D-09 | SuperAdmin with no session accessing `/quests` → NOT redirected by middleware | integration | `dotnet test --filter "GroupSession"` | NEW FILE |
| D-10 | `/GroupPicker/Index` with no session → 200 (picker shown, not looped) | integration | `dotnet test --filter "GroupPicker"` | Exists — verify |

### Sampling Rate

- **Per task commit:** `dotnet test QuestBoard.IntegrationTests --filter "HomeController\|QuestController" --no-build`
- **Per wave merge:** `dotnet test`
- **Phase gate:** Full suite green before `/gsd-verify-work`

### Wave 0 Gaps

- [ ] `QuestBoard.IntegrationTests/Controllers/GroupSessionMiddlewareTests.cs` — covers D-09, D-10, D-11
- [ ] Update `HomeControllerIntegrationTests.cs` — replace quest-display tests with landing page tests
- [ ] Update `CalendarControllerIntegrationTests.cs` — add auth header to unauthenticated client
- [ ] Update `QuestLogControllerIntegrationTests.cs` — add auth header to unauthenticated client

---

## Security Domain

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | yes | ASP.NET Identity `[Authorize]` attribute + Identity middleware |
| V3 Session Management | yes | ASP.NET Core Session (`ISession`) + `SessionKeys.ActiveGroupId` |
| V4 Access Control | yes | Per-controller `[Authorize]` + policy attributes |
| V5 Input Validation | no | No user input in this phase |
| V6 Cryptography | no | No cryptographic operations in this phase |

### Known Threat Patterns for this stack

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Open redirect in session-recovery middleware | Spoofing | Redirect to hardcoded `/groups/pick`, never to a user-supplied URL |
| SuperAdmin bypass of middleware | Elevation of Privilege | Explicit `IsInRole("SuperAdmin")` check before the session check |
| Unauthenticated access to group-scoped data via public endpoints | Information Disclosure | `[Authorize]` at class level on affected controllers |

---

## Sources

### Primary (HIGH confidence)

- Direct codebase reads — all findings reflect actual source files in the repository as of 2026-06-30:
  - `QuestBoard.Service/Controllers/QuestBoard/HomeController.cs` — quest board logic to migrate
  - `QuestBoard.Service/Controllers/QuestBoard/QuestController.cs` — target controller; no existing Index action confirmed
  - `QuestBoard.Service/Controllers/GroupPickerController.cs` — exact fallback redirect on line 59
  - `QuestBoard.Service/Controllers/DungeonMaster/DungeonMasterController.cs` — exact `[AllowAnonymous]` placements identified
  - `QuestBoard.Service/Program.cs` — exact middleware pipeline order confirmed
  - `QuestBoard.Service/Views/Home/Index.cshtml` and `Index.Mobile.cshtml` — full content read
  - `QuestBoard.Service/Views/Shared/_Layout.cshtml` and `_Layout.Mobile.cshtml` — navbar brand links confirmed
  - `QuestBoard.Service/Views/Quest/Create.cshtml` and `Create.Mobile.cshtml` — Cancel button links confirmed
  - `QuestBoard.Service/Middleware/MobileDetectionMiddleware.cs` — middleware class pattern
  - `QuestBoard.Service/Constants/SessionKeys.cs` — key constants confirmed
  - `QuestBoard.IntegrationTests/` — all test files read to identify what breaks and what's new

### Secondary (MEDIUM confidence)

- `.planning/phases/31-unauthenticated-landing-redirect/31-CONTEXT.md` — user decisions from discuss-phase session

---

## Metadata

**Confidence breakdown:**
- Controller changes: HIGH — read every file, exact line numbers identified
- View changes: HIGH — read every file, exact anchors identified
- Middleware: HIGH — Hangfire guard is the exact pattern; pattern confirmed in source
- Test updates: HIGH — read all affected test files; know exactly which tests break and why
- Route for `/quests`: MEDIUM — `[Route("quests")]` is the standard ASP.NET Core approach; the question of whether the CONTEXT.md's `/groups/pick` URL is a vanity route or the actual GroupPicker route is flagged as A1 (assumed, needs planner verification)

**Research date:** 2026-06-30
**Valid until:** 2026-07-30 (stable — no external dependencies that change)
