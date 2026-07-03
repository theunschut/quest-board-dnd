# Phase 37: Navigation & Access Control - Research

**Researched:** 2026-07-03
**Domain:** ASP.NET Core 10 MVC — conditional nav rendering, policy-based authorization, cookie AccessDeniedPath wiring
**Confidence:** HIGH

## Summary

This phase is pure Service-layer surface work: no new controllers, no new DB tables, no new NuGet packages. Two independent changes land on the existing `_Layout.cshtml`/`_Layout.Mobile.cshtml` nav partial and `AdminController`: (1) five nav items become visible only when the active group's `BoardType` resolves to `OneShot`, following an **allowlist** shape already locked by CONTEXT.md's D-01, and (2) `EmailStats` is narrowed from `AdminOnly` to `SuperAdminOnly`, paired with a real `/Account/AccessDenied` action + view.

The key open question — how to expose `BoardType` to `_Layout.cshtml` — resolves clearly in favor of **extending `IActiveGroupContext`** (Option 2 in CONTEXT.md) rather than mirroring a new value into Session (Option 1). The deciding factor is not staleness risk (both are equally safe, since `BoardType` is immutable per BOARD-02) but **testability**: the integration test suite's `WebApplicationFactoryBase` replaces `IActiveGroupContext` wholesale with a settable `MutableGroupContext` singleton in the `Testing` environment, and no existing test ever writes to `HttpContext.Session` directly. A Session-mirrored `BoardType` would be invisible to every existing test-authentication helper; an `IActiveGroupContext`-based `BoardType?` member slots directly into the established test-double pattern (`MutableGroupContext.BoardType` becomes a settable property, mirroring `ActiveGroupId`).

For the Access Denied wiring, a second discovery not surfaced in CONTEXT.md: **`QuestBoard.Service/Views/Shared/AccessDenied.cshtml` already exists**, dating back to the very first authentication commit, already follows the modern-card pattern, but is completely orphaned — no controller action has ever returned it, and its copy is hardcoded to DM-only wording ("Dungeon Master Access Required"). The planner should reuse and generalize this view rather than create a new one, and must add `ConfigureApplicationCookie` (or an equivalent `.AddIdentityCookies` post-configure) to make `AccessDeniedPath` land somewhere real — Identity's default cookie scheme is currently registered with zero cookie configuration overrides in `Program.cs`, so its baked-in default `AccessDeniedPath` (`/Account/AccessDenied`) resolves to a 404 today. A confirmed ASP.NET Core semantics pitfall also applies to D-06: method-level `[Authorize]` attributes do **not override** class-level ones — both are ANDed. This happens to produce the correct behavior for `EmailStats` (every SuperAdmin already passes `AdminOnly` via `AdminHandler`'s Step-1 bypass) but the planner must not describe it as an "override."

**Primary recommendation:** Extend `IActiveGroupContext` with a `BoardType?` member backed by a single per-request `IGroupService.GetByIdAsync` lookup (mirroring the exact pattern already used in `QuestController.GetActiveBoardTypeAsync`/`QuestLogController.GetActiveBoardTypeAsync`), inject `IActiveGroupContext` into `_Layout.cshtml`/`_Layout.Mobile.cshtml` via `@inject`, and gate the five allowlisted nav items behind `boardType == BoardType.OneShot`. Wire a real `AccessDenied` action on `AccountController` (already exempt from `GroupSessionMiddleware`), reuse/generalize the existing orphaned view, and add `ConfigureApplicationCookie` in `Program.cs` to point `AccessDeniedPath` there.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Nav item visibility gating (NAV-01..06) | Frontend Server (SSR) | — | `_Layout.cshtml`/`_Layout.Mobile.cshtml` render server-side on every request; no client-side JS involved anywhere in this app's nav |
| BoardType exposure to layout | API / Backend (via Domain interface) | Frontend Server (SSR) | `IActiveGroupContext` is a Domain-layer interface implemented by a Service-layer class reading session + DB; `_Layout.cshtml` (SSR) consumes it via DI |
| EmailStats access restriction (ACCESS-01) | API / Backend | — | Enforced at the `[Authorize(Policy=...)]` layer on the controller action — this is the authoritative gate; nav hiding is cosmetic only |
| AccessDenied page | Frontend Server (SSR) | — | New MVC action + Razor view; no API/JSON surface |
| Cookie AccessDeniedPath config | API / Backend | — | `Program.cs` authentication middleware configuration, app-wide |

## User Constraints (from CONTEXT.md)

### Locked Decisions

- **D-01:** The 5 campaign-gated items (Calendar, Shop, Manage Shop, Edit My Profile, Players) are implemented as a **"show only when confirmed One-Shot" allowlist**, not a "hide only when Campaign" blocklist. This single condition naturally covers every indeterminate case (anonymous visitor, authenticated user with no active group yet) without a separate null-handling branch.
- **D-02:** SuperAdmin's nav follows whatever group is currently active (`ActiveGroupId`) — no special-casing. If a SuperAdmin's active group is Campaign, they see the stripped-down nav like any DM/Player in that group; if no group is active, they fall into D-03's hidden state along with everyone else.
- **D-03:** For an authenticated user who hasn't picked a group yet (on GroupPicker, no `ActiveGroupId` set), the 5 allowlisted items are **hidden**.
- **D-04:** The Calendar nav link is now also wrapped in the existing `IsAuthenticated` check for anonymous (logged-out) visitors, closing a pre-existing gap where it rendered as a visible-but-dead link (`CalendarController` requires `[Authorize]`).
- **D-05:** Guild Members (NAV-03) and Quest Log are **not** part of the allowlist — they stay visible to all authenticated users regardless of board type or active-group state, unchanged from today.
- **D-06:** `AdminController.EmailStats` gets its authorization tightened from the class-level `AdminOnly` policy to `SuperAdminOnly` via a method-level `[Authorize]` override. The Email Stats nav link in the Admin dropdown is gated the same way.
- **D-07:** Add a real Access Denied page (new route/view) now. Because `AccessDeniedPath` is a single app-wide cookie-auth setting, this is inherently an app-wide fix — every `AdminOnly`/`DungeonMasterOnly`/`SuperAdminOnly` policy failure across the whole app gets a proper page instead of a 404, not just Email Stats.

### Claude's Discretion

- **Mechanism for exposing the active group's `BoardType` to `_Layout.cshtml`:** RESOLVED below in favor of extending `IActiveGroupContext` — see Architecture Patterns and Pitfall 1.
- **Exact wording/styling of the new Access Denied page:** follow CLAUDE.md's modern-card pattern (`modern-card`, `modern-card-header`, `modern-card-body`); exact copy left to planner/implementer. Note: an existing orphaned `AccessDenied.cshtml` already follows this pattern and should be generalized rather than replaced (see Pitfall 3).
- **Where the AccessDenied action lives** (existing `AccountController` vs. a new controller): RESOLVED below in favor of `AccountController` — see Architecture Patterns.

### Deferred Ideas (OUT OF SCOPE)

None — both adjacent items that came up (anonymous Calendar link, Access Denied page) were explicitly pulled into this phase's scope by the user rather than deferred. No scope creep occurred.

## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| NAV-01 | Calendar nav item is hidden for campaign-type groups | Allowlist gate in `_Layout.cshtml`/`_Layout.Mobile.cshtml`; also needs D-04's `IsAuthenticated` wrap |
| NAV-02 | Shop nav item is hidden for campaign-type groups | Same allowlist gate, both layouts |
| NAV-03 | Guild member directory nav item remains visible for all board types (no change) | No code change — verify existing `@if (User.Identity?.IsAuthenticated == true)`-only guard is untouched |
| NAV-04 | "Manage Shop" nav item (DM dropdown) is hidden for campaign-type groups | Same allowlist gate, nested inside existing `DungeonMasterOnly` `@if` block, both layouts |
| NAV-05 | "Edit My Profile" nav item (DM dropdown) is hidden for campaign-type groups | Same allowlist gate, nested inside existing `DungeonMasterOnly` `@if` block, both layouts |
| NAV-06 | "Players" nav item is hidden for campaign-type groups | Same allowlist gate, both layouts |
| ACCESS-01 | "Email Stats" page and nav item are restricted to SuperAdmin only | Method-level `[Authorize(Policy = "SuperAdminOnly")]` on `AdminController.EmailStats`; nav link gated on `SuperAdminOnly` policy check (not `AdminOnly`) in both layouts; new `AccessDenied` action + `ConfigureApplicationCookie` wiring so the failure is a real page, not a 404 |

## Standard Stack

No new packages required. This phase is 100% composed of existing framework primitives already in use elsewhere in the codebase:

| Component | Already Used In | Reused For |
|-----------|-----------------|------------|
| `[Authorize(Policy = "SuperAdminOnly")]` | `Platform/GroupController` (class-level), Hangfire dashboard nav link check | `AdminController.EmailStats` method-level override |
| `IActiveGroupContext.ActiveGroupId` pattern | `QuestController`, `QuestLogController`, `AdminController` | Extended with `BoardType?` |
| `GetActiveBoardTypeAsync()` pattern (per-action `groupService.GetByIdAsync` + null-coalesce to `OneShot`) | `QuestController.cs:1022`, `QuestLogController.cs:130` | Moves into `ActiveGroupContextService` as the canonical single source, both call sites can eventually delegate to it (out of scope to refactor them in this phase — safe to leave as-is, but do not contradict them) |
| `ConfigureApplicationCookie` | Not yet used anywhere in this codebase | New — sets `AccessDeniedPath` |
| `Views/Shared/AccessDenied.cshtml` | Orphaned since `13382cf Added authentication` | Reused, generalized (remove DM-specific copy) |

**No installation step needed** — every symbol used already exists in referenced NuGet packages (`Microsoft.AspNetCore.Identity` 10.0.9, `Microsoft.AspNetCore.Authorization`).

## Package Legitimacy Audit

Not applicable — this phase installs no new packages. All work uses framework APIs already present in the project's existing dependency graph (verified via `.planning/codebase/STACK.md` and direct inspection of `Program.cs`/`.csproj` references).

## Architecture Patterns

### System Architecture Diagram

```
                     ┌─────────────────────────────┐
                     │   Browser (any page)        │
                     └──────────────┬──────────────┘
                                    │ GET any route
                                    ▼
                     ┌─────────────────────────────┐
                     │  _Layout.cshtml /            │
                     │  _Layout.Mobile.cshtml       │  <- renders on EVERY page
                     │  (SSR, Razor)                │
                     └──────────────┬──────────────┘
                                    │ @inject IActiveGroupContext
                                    ▼
                     ┌─────────────────────────────┐
                     │ IActiveGroupContext          │
                     │  .ActiveGroupId (existing)   │
                     │  .BoardType (NEW)             │
                     └──────────────┬──────────────┘
                                    │ implemented by
                                    ▼
                     ┌─────────────────────────────┐
                     │ ActiveGroupContextService     │
                     │  reads Session.ActiveGroupId  │
                     │  BoardType: 1 lookup via       │
                     │  IGroupService.GetByIdAsync    │
                     │  (mirrors QuestController's    │
                     │  GetActiveBoardTypeAsync)       │
                     └──────────────┬──────────────┘
                                    │
                                    ▼
                     ┌─────────────────────────────┐
                     │ Nav gate:                     │
                     │ @if (boardType == OneShot)     │
                     │   render Calendar/Shop/         │
                     │   ManageShop/EditProfile/       │
                     │   Players                        │
                     └─────────────────────────────┘

                    ── separately ──

  Any [Authorize(Policy=...)] failure anywhere in the app
                │
                ▼
  ASP.NET Core Identity cookie auth middleware
  challenges → redirects to AccessDeniedPath
                │
                ▼
  Program.cs: builder.Services.ConfigureApplicationCookie(
      opts => opts.AccessDeniedPath = "/Account/AccessDenied")
                │
                ▼
  AccountController.AccessDenied() [AllowAnonymous]
                │
                ▼
  Views/Shared/AccessDenied.cshtml (existing, generalized)
```

### Recommended Project Structure

No new folders. Touched files only:
```
QuestBoard.Domain/
├── Interfaces/
│   └── IActiveGroupContext.cs        # add BoardType? member
QuestBoard.Service/
├── Services/
│   └── ActiveGroupContextService.cs  # implement BoardType?, inject IGroupService
├── Controllers/Admin/
│   ├── AdminController.cs            # EmailStats: add [Authorize(Policy="SuperAdminOnly")]
│   └── AccountController.cs          # add AccessDenied() [AllowAnonymous]
├── Views/Shared/
│   ├── _Layout.cshtml                # allowlist gate + D-04 fix + EmailStats gate
│   ├── _Layout.Mobile.cshtml         # same
│   └── AccessDenied.cshtml           # generalize copy (remove DM-only wording)
├── Program.cs                        # add ConfigureApplicationCookie
QuestBoard.IntegrationTests/
├── Helpers/
│   └── MutableGroupContext.cs        # add settable BoardType property, default OneShot
```

### Pattern 1: Extend IActiveGroupContext with BoardType (chosen mechanism)

**What:** Add `BoardType? BoardType { get; }` to `IActiveGroupContext`. `ActiveGroupContextService` implements it by resolving the active group via `IGroupService.GetByIdAsync(groupId)` when `ActiveGroupId` is set, defaulting to `null` when it isn't (mirrors D-03: no active group → hidden).

**When to use:** Any place needing BoardType outside a per-action `ViewBag` flow — `_Layout.cshtml` is the motivating case, but this also becomes available to any future cross-cutting SSR concern.

**Why over Session mirroring:** Both mechanisms are equally safe against staleness (BoardType is immutable per BOARD-02 — a session mirror could only go stale by switching groups, which already re-writes Session at the same two `GroupPickerController` call sites). The deciding factor is testability:

```csharp
// QuestBoard.IntegrationTests/Helpers/MutableGroupContext.cs — extend, don't replace
public class MutableGroupContext : IActiveGroupContext
{
    public int? ActiveGroupId { get; set; } = 1;
    public BoardType? BoardType { get; set; } = QuestBoard.Domain.Enums.BoardType.OneShot; // default keeps existing nav-visible tests green
}
```

Because `WebApplicationFactoryBase.ConfigureTestServices` registers `MutableGroupContext` as a singleton replacement for `IActiveGroupContext` (`services.AddSingleton<IActiveGroupContext>(TestGroupContext)`), and because **no existing integration test ever writes to `HttpContext.Session`** for `ActiveGroupId`/`ActiveGroupName` (grep across `QuestBoard.IntegrationTests` confirms zero `Session.Set`/`Session.Get` calls), a Session-based BoardType mirror would have **no way to be set from a test** short of manipulating the test server's cookie container directly — a significantly heavier lift than adding one settable property to an existing test double. `IActiveGroupContext` extension slots into the exact pattern the test suite already uses for `ActiveGroupId`.

**Example (production implementation, following the existing `GetActiveBoardTypeAsync` precedent almost verbatim):**
```csharp
// Source: pattern from QuestBoard.Service/Controllers/QuestBoard/QuestController.cs:1022 (existing code)
public class ActiveGroupContextService(
    IHttpContextAccessor httpContextAccessor,
    IGroupService groupService) : IActiveGroupContext
{
    // ... existing ActiveGroupId, SetGroupId unchanged ...

    public async Task<BoardType?> GetBoardTypeAsync(CancellationToken token = default)
    {
        if (ActiveGroupId is not { } groupId) return null;
        var group = await groupService.GetByIdAsync(groupId, token);
        return group?.BoardType;
    }
}
```

**IMPORTANT — sync/async mismatch pitfall:** `IActiveGroupContext.ActiveGroupId` is a **synchronous property** (no I/O — Session reads are synchronous). Adding `BoardType` as a property (not a method) would force either (a) a blocking `.Result`/`.GetAwaiter().GetResult()` call inside a property getter — deadlock risk on the ASP.NET Core Kestrel synchronization context in edge cases and always an anti-pattern — or (b) an eager `Task` field populated in the constructor, which cannot await mid-constructor. **Recommendation:** expose it as `Task<BoardType?> GetBoardTypeAsync(CancellationToken token = default)` on the interface, not a synchronous property, and call it once per render from `_Layout.cshtml` with `@await`. This is the one place this pattern diverges from `ActiveGroupId`'s existing shape, and the planner must call this out explicitly rather than copy the property shape blindly.

### Pattern 2: AccessDenied action placement

**What:** Add `[AllowAnonymous] [HttpGet] public IActionResult AccessDenied() => View();` to the existing `AccountController` (`QuestBoard.Service/Controllers/Admin/AccountController.cs`), which is `Admin`-namespaced but not `[Area]`-scoped, so it resolves at `/Account/AccessDenied` under the default route — matching Identity's default `AccessDeniedPath` exactly with zero extra `Program.cs` routing.

**Why AccountController over a new controller:**
1. `AccountController` already hosts `Login`, `ForgotPassword`, `SetPassword` — i.e., the pre-authentication/authorization-boundary pages. `AccessDenied` belongs in the same conceptual bucket.
2. `AccountController`'s route prefix (`/Account`) is already in `GroupSessionMiddleware.ExemptPathPrefixes` (derived via `nameof(AccountController)`), so `AccessDenied` is automatically exempt from the group-session redirect-to-picker logic — no middleware changes needed. A new controller would need to be added to that allowlist manually, which is an easy step to forget (silent redirect loop risk if forgotten: an unauthorized Campaign-group user redirected to `/Account/AccessDenied` would otherwise get bounced to `/groups/pick` first).
3. No `[Authorize]` attribute exists at the class level on `AccountController` today (individual actions opt in), so adding an unauthenticated-accessible `AccessDenied` action requires no `[AllowAnonymous]` override of a class-level policy — it's simply absent by default, consistent with `Login`/`ForgotPassword`.

**Wiring in Program.cs (new — required):**
```csharp
// Source: pattern per Microsoft Learn — Identity cookie configuration
// Add BEFORE builder.Build() (near the other builder.Services.Configure<...> calls)
builder.Services.ConfigureApplicationCookie(options =>
{
    options.AccessDeniedPath = "/Account/AccessDenied";
});
```
This is app-wide by nature (single cookie-scheme setting) — matches D-07's explicitly-approved wider blast radius. No other `AddCookie`/`ConfigureApplicationCookie` call exists in `Program.cs` today, so there is no conflicting configuration to worry about overriding.

### Pattern 3: Generalizing the existing orphaned AccessDenied.cshtml

**What:** `QuestBoard.Service/Views/Shared/AccessDenied.cshtml` already exists, already uses `modern-card`/`modern-card-header`/`modern-card-body`, but its body copy is hardcoded to "Dungeon Master Access Required" — written when DM-gating was the only policy in the app (per `13382cf Added authentication`). Since this phase's AccessDenied page will now also serve `SuperAdminOnly` and `AdminOnly` failures, the copy must be generalized to something policy-agnostic (e.g., "You don't have permission to view this page") rather than DM-specific.

**Where the view resolves from:** Placing the controller action on `AccountController` means MVC's view-location search order looks for `Views/Account/AccessDenied.cshtml` first, then falls back to `Views/Shared/AccessDenied.cshtml` — the existing shared file will be picked up automatically with **no `return View("~/Views/Shared/AccessDenied.cshtml")` override needed**, since `Views/Account/AccessDenied.cshtml` doesn't exist. Verify this resolves correctly in dev before assuming it; if there's ever ambiguity, an explicit `return View("~/Views/Shared/AccessDenied.cshtml");` is the safe fallback.

### Pattern 4: Nav allowlist gating (D-01 shape)

**What:** Wrap each of the 5 items in a single condition testing for confirmed One-Shot, not the inverse.

```csharp
// Source: pattern derived from existing QuestLog/Quest views' `boardType != BoardType.Campaign` checks,
// inverted per D-01's "show only when confirmed One-Shot" allowlist framing
@{
    var boardType = await ActiveGroupContext.GetBoardTypeAsync();
}
@if (boardType == BoardType.OneShot)
{
    <li class="nav-item">
        <a class="nav-link" asp-controller="Shop" asp-action="Index">
            <i class="fas fa-store me-1"></i>Shop
        </a>
    </li>
}
```
Note this is the **opposite polarity** from the existing `Quest`/`QuestLog` views' `boardType != BoardType.Campaign` checks (which are blocklist-shaped, appropriate there because those views only ever render when a group IS active — no anonymous/no-group case reaches them). `_Layout.cshtml` is different: it renders unconditionally for anonymous visitors and no-group-yet users, which is exactly why D-01 mandates the allowlist shape here specifically — `boardType == BoardType.OneShot` naturally evaluates `false` (hidden) when `boardType` is `null`, with no extra branch.

### Anti-Patterns to Avoid

- **Property-shaped async BoardType accessor:** Do not add `BoardType? BoardType { get; }` as a synchronous property if it requires a blocking DB call under the hood. Use an async method (see Pattern 1).
- **Blocklist shape in `_Layout.cshtml`:** Do not write `@if (boardType != BoardType.Campaign)` for the nav gate — this passes for `null` (no active group), which contradicts D-03's explicit "hidden" requirement. Must be `@if (boardType == BoardType.OneShot)`.
- **Assuming method-level `[Authorize]` overrides class-level:** `[Authorize(Policy = "SuperAdminOnly")]` on `EmailStats` does not replace `AdminOnly` — both apply, ANDed. Document this precisely in the plan/commit rather than describing it as "narrowing via override."
- **New controller for AccessDenied:** Skip `GroupSessionMiddleware.ExemptPathPrefixes` maintenance by putting the action on `AccountController`, not a new controller.
- **Rewriting the DM-focused Access Denied view from scratch:** The file already exists and already matches CLAUDE.md's modern-card convention — edit its copy, don't replace it wholesale (avoids losing the FontAwesome/Bootstrap structure that already passes the project's UI convention).

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| SuperAdmin-only gating | A new custom `IAuthorizationHandler`/requirement | Existing `SuperAdminOnly` policy (`Program.cs:81-82`, `policy.RequireRole("SuperAdmin")`) | Already defined, already used by `Platform/GroupController` and the Hangfire dashboard link — zero new authorization code needed |
| Cookie-auth "access denied" redirect | Custom middleware intercepting 403s | `ConfigureApplicationCookie(options => options.AccessDeniedPath = ...)` | Built into `Microsoft.AspNetCore.Identity`; this is precisely the extension point Identity's cookie scheme provides for this exact problem |
| BoardType lookup caching | A new in-memory cache layer for group lookups | Direct `IGroupService.GetByIdAsync` call, same as `QuestController`/`QuestLogController` already do per-render | BoardType is immutable (BOARD-02) and groups are a tiny table (likely single digits to low tens of rows) — premature caching adds complexity for a lookup that's already cheap and already happens once per render elsewhere in the app without issue |

**Key insight:** Every piece of this phase already has a nearly-identical precedent living in the codebase (`GetActiveBoardTypeAsync`, `SuperAdminOnly` policy, `modern-card` AccessDenied view). The work is almost entirely "generalize an existing pattern to a new call site," not "invent a new pattern."

## Common Pitfalls

### Pitfall 1: Session-mirror BoardType breaks existing test infrastructure
**What goes wrong:** If the planner chooses to mirror `BoardType` into `Session` (CONTEXT.md's Option 1) instead of extending `IActiveGroupContext`, every integration test asserting nav-item visibility per board type has no way to set that session value, because `WebApplicationFactoryBase` replaces `IActiveGroupContext` wholesale with `MutableGroupContext` and never touches `HttpContext.Session` for `ActiveGroupId`/`ActiveGroupName`.
**Why it happens:** The test factory's group-context override was designed before BoardType existed; it intentionally bypasses Session as a testing shortcut.
**How to avoid:** Extend `IActiveGroupContext` (Pattern 1) instead. `MutableGroupContext` then just gets one new settable property, consistent with how `ActiveGroupId` is already tested.
**Warning signs:** If a plan task says "write BoardType to Session in GroupPickerController," check whether a corresponding integration test setup story explains how tests will set it — if not, this pitfall is already manifesting.

### Pitfall 2: Method-level `[Authorize]` does not override class-level policy — it ANDs
**What goes wrong:** Assuming `[Authorize(Policy = "SuperAdminOnly")]` on `EmailStats` "replaces" the controller's `[Authorize(Policy = "AdminOnly")]`, when in fact ASP.NET Core evaluates **both** policies and requires both to succeed.
**Why it happens:** Older ASP.NET MVC (System.Web) supported `[OverrideAuthorization]`; ASP.NET Core's policy-based system deliberately does not carry this semantic forward.
**How to avoid:** [VERIFIED: learn.microsoft.com/aspnet/core/security/authorization/policies, aspnetcore-10.0] — "If multiple policies are applied at the controller and action levels, all policies must pass before access is granted." For this specific case the net result is correct regardless (every `SuperAdmin` already passes `AdminOnly` via `AdminHandler`'s Step 1 role-claim bypass — no DB call), so functionally D-06 works as intended. But if a future phase ever needs a method-level policy to be **broader** than a class-level one, this AND semantics would silently block legitimate users; that pattern must never be introduced without restructuring the controller.
**Warning signs:** A method-level `[Authorize]` attribute that should have wider access than its class but is unexpectedly rejecting users who satisfy only the narrower method-level policy.

### Pitfall 3: AccessDeniedPath is unconfigured — every current 403 already silently 404s
**What goes wrong:** Assuming a policy failure today returns a proper 403 (which the app could theoretically already have a custom handler for). It does not — no `ConfigureApplicationCookie`, no `UseStatusCodePages`, no `UseExceptionHandler` catches an authorization challenge redirect to `/Account/AccessDenied`, so MVC's default router resolves the unregistered path as a 404. This means EVERY existing `AdminOnly`/`DungeonMasterOnly`/`SuperAdminOnly` failure in the app today already produces a 404, not a 403 — this phase's fix widens beyond just EmailStats (D-07 already flags and approves this, but the planner must implement `ConfigureApplicationCookie` globally, not scope it narrowly to an EmailStats-specific redirect).
**Why it happens:** `AddIdentity<UserEntity, IdentityRole<int>>(...)` in `Program.cs` configures password/lockout/user options only — it never touches the cookie scheme's `AccessDeniedPath`, `LoginPath`, or `LogoutPath`, so Identity's baked-in defaults apply silently.
**How to avoid:** Add the single `ConfigureApplicationCookie` call (Pattern 2). Verify with a manual test that hitting `/Admin/EmailStats` as a non-SuperAdmin now returns the AccessDenied view content, not a 404 — and that existing `AdminOnly`/`DungeonMasterOnly` failures elsewhere ALSO now hit the new page (expected side effect per D-07, not a regression).
**Warning signs:** Existing integration tests asserting `HttpStatusCode.Forbidden` OR `Redirect`/`Found`/`Unauthorized` on authorization failure (see `AdminControllerIntegrationTests.EmailStats_WhenNotAdmin_ShouldReturnForbidden`) may need their assertions widened to also accept a 200 (from a non-redirecting client following through to the AccessDenied view) once `AccessDeniedPath` is wired — a non-redirecting `HttpClient` (as used throughout `AdminControllerIntegrationTests`) will see a `302 Found` to `/Account/AccessDenied`, which already satisfies the existing `BeOneOf(Forbidden, Redirect, Found, Unauthorized)` assertions without any test changes. Confirm this remains true rather than assuming it.

### Pitfall 4: Forgetting the D-04 Calendar fix must land in BOTH desktop and mobile layouts
**What goes wrong:** `_Layout.cshtml:126-131` renders Calendar completely outside any `@if (User.Identity?.IsAuthenticated == true)` block (it's in the `ms-auto` nav group alongside the login/profile dropdown, structurally separate from the `me-auto` authenticated-items block). `_Layout.Mobile.cshtml:110-115` has an identical unconditional render, structurally separate from its own authenticated block (comment: `@* Calendar — available to all *@`). Both must be fixed identically, and independently from the OneShot-allowlist gate — Calendar needs BOTH `IsAuthenticated` (D-04) AND the BoardType allowlist (NAV-01) as nested conditions.
**Why it happens:** The two layouts are maintained as separate, non-DRY files (no shared partial for nav items) — an easy place for one file to get the fix and the other to be missed.
**How to avoid:** Grep both files for `asp-controller="Calendar"` before considering the task complete; verify the nesting order (`IsAuthenticated` outer, `BoardType == OneShot` inner, or combined into one `&&` condition) produces identical visible behavior in both.
**Warning signs:** Manual test showing Calendar still visible to anonymous users in one layout but not the other.

### Pitfall 5: Nav hiding is cosmetic only — direct URL access to hidden pages is unaffected (by design, not a bug)
**What goes wrong:** Assuming NAV-01/02/04/05/06 also block direct URL access to `/Calendar`, `/Shop`, `/ShopManagement`, `/DungeonMaster/EditProfile`, `/Players` for Campaign-group users. They do not — `CalendarController`, `ShopController`, `ShopManagementController`, `DungeonMasterController`, and (very likely) `PlayersController` are gated only by `[Authorize]` or `DungeonMasterOnly`/generic-authenticated checks, none of which are BoardType-aware. A Campaign-group DM who bookmarks `/ShopManagement` before this phase can still reach it after.
**Why it happens:** REQUIREMENTS.md explicitly scopes this phase to nav visibility (NAV-01..06) plus the one access-control requirement that DOES need server-side enforcement (ACCESS-01, Email Stats). The "Out of Scope" table in REQUIREMENTS.md doesn't call this out explicitly, but the phase boundary/success-criteria wording ("hide", "render... as before") is unambiguous — this is nav-only work, contrasted deliberately with ACCESS-01's "a direct URL request is rejected, not just hidden."
**How to avoid:** Do not add BoardType authorization checks to `CalendarController`/`ShopController`/etc. in this phase — that would be scope creep beyond NAV-01..06's stated boundary and isn't listed in REQUIREMENTS.md. If this asymmetry is a concern, it belongs in a future phase's backlog, not silently bundled into 37.
**Warning signs:** A plan task proposing to add `[Authorize]`-style BoardType gating to `CalendarController` or similar — flag for descoping unless the user explicitly re-opens this in a future CONTEXT.md.

## Code Examples

### BoardType-gated nav item (desktop layout)
```csharp
// Source: derived pattern, QuestBoard.Service/Views/Shared/_Layout.cshtml
@{
    var activeBoardType = await ActiveGroupContext.GetBoardTypeAsync();
}
@if (activeBoardType == BoardType.OneShot)
{
    <li class="nav-item">
        <a class="nav-link" asp-controller="Players" asp-action="Index">
            <i class="fas fa-users me-1"></i>Players
        </a>
    </li>
}
```

### Email Stats nav link — SuperAdmin-only gate (both layouts)
```csharp
// Source: existing pattern already used one line above for Hangfire ("Background Jobs")
// in _Layout.cshtml:57-64 — reuse the identical User.IsInRole check for Email Stats
@if (User.IsInRole("SuperAdmin"))
{
    <li>
        <a class="dropdown-item" asp-controller="Admin" asp-action="EmailStats">
            <i class="fas fa-envelope-open-text me-2"></i>Email Stats
        </a>
    </li>
}
```
Note: `_Layout.cshtml` already has an identical `User.IsInRole("SuperAdmin")` check immediately above (for the Background Jobs/Hangfire link) — the Email Stats link just needs the exact same guard applied, no new pattern.

### Method-level authorization override on EmailStats
```csharp
// Source: QuestBoard.Service/Controllers/Admin/AdminController.cs — existing class declaration
[Authorize(Policy = "AdminOnly")]
public class AdminController(...) : Controller
{
    // ... other actions unaffected, still governed by class-level AdminOnly ...

    [HttpGet]
    [Authorize(Policy = "SuperAdminOnly")]  // ADD — combines with (ANDs with) class-level AdminOnly
    public async Task<IActionResult> EmailStats(bool force = false, CancellationToken token = default)
    {
        // body unchanged
    }
}
```

### AccessDenied action
```csharp
// Source: new — placed on existing AccountController per Pattern 2
[HttpGet]
[AllowAnonymous]
public IActionResult AccessDenied()
{
    return View();
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Policy-gated pages 404 on authorization failure | Policy-gated pages redirect to a real AccessDenied page | This phase | All `AdminOnly`/`DungeonMasterOnly`/`SuperAdminOnly` failures app-wide, not just EmailStats |
| `ViewBag.BoardType` set per-action in `QuestController`/`QuestLogController` only | `IActiveGroupContext.GetBoardTypeAsync()` available anywhere via DI, including `_Layout.cshtml` | This phase | Future phases needing BoardType outside a per-action flow no longer need a new threading mechanism |

**Deprecated/outdated:** None — no library API deprecations involved; this is purely internal architecture evolution.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | Placing `AccessDenied()` on `AccountController` resolves the view via the default `Views/{Controller}/{Action}.cshtml` → `Views/Shared/{Action}.cshtml` fallback with no explicit view-path override needed | Pattern 3 | Low — if wrong, a one-line `return View("~/Views/Shared/AccessDenied.cshtml")` fixes it; verify in dev before considering the task done |
| A2 | `PlayersController` (not directly read in this research session) follows the same "no BoardType awareness" pattern as `CalendarController`/`ShopController`/`ShopManagementController`/`DungeonMasterController` | Pitfall 5 | Low — even if `PlayersController` happens to already have some group-role gating, it does not affect this phase's nav-only scope; only relevant if a future phase adds server-side BoardType enforcement |

## Open Questions

None outstanding — the phase's one explicitly-flagged open question (BoardType exposure mechanism) is resolved above with concrete testability evidence, and the AccessDenied placement/styling discretion items are both resolved with a recommended concrete implementation.

## Environment Availability

Skipped — this phase has no external dependencies. It touches only existing controllers, views, `Program.cs` configuration, and the Domain interface layer; no new services, tools, or runtimes are introduced.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit 3.2.2 + Microsoft.AspNetCore.Mvc.Testing 10.0.9 (WebApplicationFactory) |
| Config file | `QuestBoard.IntegrationTests/QuestBoard.IntegrationTests.csproj`; no separate test-runner config file |
| Quick run command | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~AdminControllerIntegrationTests"` |
| Full suite command | `dotnet test` |

### Phase Requirements → Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| NAV-01 | Calendar hidden in Campaign group nav (desktop + mobile) | integration (HTML content assertion via `html.Should().NotContain(...)`) | `dotnet test --filter "FullyQualifiedName~LayoutNavigationTests"` | ❌ Wave 0 — new test class needed, following `MobileViewsTests.cs`'s `GetWithUserAgentAsync` + `html.Should().Contain/NotContain` pattern |
| NAV-02 | Shop hidden in Campaign group nav | integration | same new test class | ❌ Wave 0 |
| NAV-03 | Guild Members remains visible regardless of board type | integration (regression check) | same new test class | ❌ Wave 0 |
| NAV-04 | "Manage Shop" hidden in Campaign group nav | integration | same new test class | ❌ Wave 0 |
| NAV-05 | "Edit My Profile" hidden in Campaign group nav | integration | same new test class | ❌ Wave 0 |
| NAV-06 | "Players" hidden in Campaign group nav | integration | same new test class | ❌ Wave 0 |
| ACCESS-01 (nav) | Email Stats nav link hidden for Admin (non-SuperAdmin) | integration | same new test class or extend `AdminControllerIntegrationTests` | ❌ Wave 0 (nav-link assertion) |
| ACCESS-01 (page) | Direct `/Admin/EmailStats` GET rejected for Admin, allowed for SuperAdmin | integration | `dotnet test --filter "FullyQualifiedName~AdminControllerIntegrationTests.EmailStats"` | ✅ Partial — `EmailStats_WhenNotAdmin_ShouldReturnForbidden` exists but tests Player role, not Admin-but-not-SuperAdmin; needs a new `EmailStats_WhenAdminNotSuperAdmin_ShouldBeRejected` test |
| ACCESS-01 (AccessDenied page) | AccessDenied action returns 200 with generalized copy, no crash on anonymous or authenticated hit | integration | new test in an `AccountControllerIntegrationTests` or extend existing | ❌ Wave 0 |
| D-04 | Calendar hidden for anonymous visitors in both layouts | integration | same new test class | ❌ Wave 0 |

### Sampling Rate
- **Per task commit:** `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~AdminControllerIntegrationTests|FullyQualifiedName~LayoutNavigationTests"`
- **Per wave merge:** `dotnet test`
- **Phase gate:** Full suite green before `/gsd:verify-work`

### Wave 0 Gaps
- [ ] `QuestBoard.IntegrationTests/Controllers/LayoutNavigationTests.cs` (or similar name) — covers NAV-01..06, D-04, ACCESS-01's nav-link visibility. Needs to exercise both desktop and mobile user agents (reuse `MobileViewsTests.GetWithUserAgentAsync` helper) and both board types (via `MutableGroupContext.BoardType` once Pattern 1 lands).
- [ ] `MutableGroupContext.BoardType` settable property — prerequisite test infrastructure change before any nav test can vary board type.
- [ ] `AdminControllerIntegrationTests.EmailStats_WhenAdminNotSuperAdmin_ShouldBeRejected` — the one gap in existing EmailStats coverage (current test only covers Player role, not the specific Admin-vs-SuperAdmin distinction this phase introduces).
- [ ] AccessDenied action test coverage — new action, zero existing coverage.
- Framework install: none — xUnit/Mvc.Testing already fully configured.

## Security Domain

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | No | Unchanged — no auth flow modified |
| V3 Session Management | No | Session mechanism itself untouched (BoardType read is via `IActiveGroupContext`, not a new session write in the chosen approach) |
| V4 Access Control | Yes | Policy-based authorization (`[Authorize(Policy = "SuperAdminOnly")]`), already-established pattern; ACCESS-01 is fundamentally a V4 requirement |
| V5 Input Validation | No | No new user input surfaces in this phase |
| V6 Cryptography | No | Not applicable |

### Known Threat Patterns for this stack

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Client-side-only access control (nav hiding mistaken for security boundary) | Elevation of Privilege | ACCESS-01 explicitly requires server-side `[Authorize(Policy=...)]` enforcement in addition to nav hiding — already the plan; Pitfall 5 documents that the OTHER 5 nav items (NAV-01..06) are intentionally nav-only per phase scope, not a security gap for THIS phase since they were never access-controlled pre-Campaign-mode either |
| Open redirect via unvalidated `AccessDeniedPath`/`returnUrl` | Tampering | `AccessDeniedPath` is a fixed literal string in `ConfigureApplicationCookie`, not user-influenced — no open-redirect surface introduced |
| Authorization policy combination confusion (Pitfall 2) | Elevation of Privilege (if misunderstood in a future phase) | Document AND semantics explicitly; verified this phase's specific combination (AdminOnly AND SuperAdminOnly) produces the intended narrower-is-subset result |

## Sources

### Primary (HIGH confidence)
- `learn.microsoft.com/en-us/aspnet/core/security/authorization/policies?view=aspnetcore-10.0` — confirmed AND-combination semantics for multiple `[Authorize]` policies at controller+action level (fetched directly, aspnetcore-10.0 moniker section read)
- Direct codebase inspection (Read tool) of: `IActiveGroupContext.cs`, `ActiveGroupContextService.cs`, `GroupPickerController.cs`, `SessionKeys.cs`, `BoardType.cs`, `Group.cs`, `Program.cs`, `AdminController.cs`, `AccountController.cs`, `AdminHandler.cs`, `AdminRequirement.cs`, `GroupSessionMiddleware.cs`, `GroupController.cs` (Platform area), `_Layout.cshtml`, `_Layout.Mobile.cshtml`, `_ViewImports.cshtml`, `AccessDenied.cshtml`, `QuestController.cs` (GetActiveBoardTypeAsync), `QuestLogController.cs`, `WebApplicationFactoryBase.cs`, `MutableGroupContext.cs`, `AdminControllerIntegrationTests.cs`, `MobileViewsTests.cs`
- `git log --oneline -- QuestBoard.Service/Views/Shared/AccessDenied.cshtml` — confirmed the view's origin at the first authentication commit (`13382cf`) with no subsequent controller wiring ever added

### Secondary (MEDIUM confidence)
- WebSearch confirming `IdentityConstants.ApplicationScheme`'s default `AccessDeniedPath` of `/Account/AccessDenied` — cross-referenced against the direct `learn.microsoft.com` policies fetch and the absence of any `ConfigureApplicationCookie`/`AddCookie` call in `Program.cs` (verified directly)

### Tertiary (LOW confidence)
- None — every claim in this document was either verified directly against the codebase or against official Microsoft documentation fetched during this research session.

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — no new packages, every API already in use elsewhere in this exact codebase
- Architecture: HIGH — BoardType-exposure mechanism decision is grounded in direct inspection of the test infrastructure that would otherwise silently break
- Pitfalls: HIGH — authorization AND-semantics verified against official aspnetcore-10.0 docs; AccessDeniedPath default and current 404 behavior verified against actual `Program.cs` contents; orphaned AccessDenied.cshtml verified via git log and direct file read

**Research date:** 2026-07-03
**Valid until:** 2026-08-03 (30 days — stable internal codebase research, no fast-moving external dependency)
