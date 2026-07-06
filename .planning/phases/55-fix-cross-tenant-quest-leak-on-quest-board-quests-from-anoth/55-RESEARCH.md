# Phase 55: Fix cross-tenant quest leak on quest board - Research

**Researched:** 2026-07-06
**Domain:** ASP.NET Core MVC middleware ordering, EF Core global query filters, ASP.NET Core Identity `SecurityStampValidator`/session re-validation
**Confidence:** HIGH

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

- **D-00 [informational]:** The user's original hypothesis (session/`AspNetSessionState` row expiration causing a regular user to see another tenant's data) was investigated and ruled out as the mechanism. `GroupSessionMiddleware` already intercepts every request for a non-SuperAdmin user with a null `ActiveGroupId` — GET/HEAD redirects to `/groups/pick`, POST/PUT/PATCH/DELETE return 409 Conflict. There is no code path today where a regular (non-SuperAdmin) user reaches a controller action with a null `ActiveGroupId`. The actual incident required a SuperAdmin account (confirmed with the user) landing on a new device with no `ActiveGroupId` ever set, which is the one role the middleware explicitly bypasses.

- **D-01:** Extend `GroupSessionMiddleware`'s "must have an active group" gate to also apply to SuperAdmin, for **every group-scoped route** (broad fix — not just `/quests`). User's explicit framing: *"a super admin can access everything, but should do so as a normal user... the content should be displayed as if a normal user views the site... this way there cannot be any confusion about what a user sees vs what a super admin sees."* SuperAdmin gets redirected to `/groups/pick` exactly like every other role. Confirmed via grep that no production feature depends on the current "null ActiveGroupId = see everything" behavior — the only real production `IgnoreQueryFilters()` call site is the unrelated Hangfire `DailyReminderJob` cross-group sweep (`QuestRepository.GetQuestsForTomorrowAllGroupsAsync`), which is unaffected by this change.

- **D-02 (kept exempt):** `GroupSessionMiddleware`'s existing exempt-path-prefixes (`/platform`, `/Error`) and the GroupPicker/Account paths stay exempt — SuperAdmin's cross-group platform management (adding/removing groups, managing members) must continue to work without first picking a specific group. Only genuinely group-scoped board pages (quest board, shop, guild members, quest log, calendar, etc.) get the new gate.

- **D-03:** Harden `QuestEntity`, `ShopItemEntity`, `ProposedDateEntity`, `PlayerDateVoteEntity`, and `PlayerSignupEntity`'s `HasQueryFilter` in `QuestBoardContext.cs` to drop the `ActiveGroupId == null ||` escape hatch entirely — matching `CharacterEntity`'s existing fail-closed shape from Phase 49 (D-03 there: `activeGroupContext.ActiveGroupId != null && e.GroupId == activeGroupContext.ActiveGroupId`, no null-passthrough). User confirmed this on top of the middleware fix: *"matches this codebase's own established lesson (Phase 49) that relying on one layer alone has repeatedly proven fragile."* If a future code change ever bypasses the middleware gate, the filter itself must show zero rows for a null `ActiveGroupId`, not every group's rows.
  - Confirmed safe: `DailyReminderJob`'s cross-group sweep uses `.IgnoreQueryFilters()`, which bypasses `HasQueryFilter` entirely regardless of the filter's predicate shape — unaffected by this change.

- **D-04:** Add a membership check to `SelectGroup` — verify the authenticated caller is actually a member of the posted `groupId` (e.g. via the existing `IUserService.GetGroupRoleByIdAsync(userId, groupId)` primitive already established in Phase 49 for the identical purpose) before setting it as the session's `ActiveGroupId`. Currently it only checks the group exists (`GetByIdAsync(groupId)` → `NotFound()` if null), not membership.

- **D-05:** When `SelectGroup` is posted with a `groupId` the caller isn't a member of, return **404 Not Found** — matching this project's established cross-tenant-response convention (Phase 49 D-04/D-09/D-13: hide existence rather than confirm it with 403).

- **D-06:** Add periodic re-validation that an already-active session's `ActiveGroupId` membership is still current — closes the residual gap where a user is removed from a group by an admin mid-session but keeps access until their session naturally re-selects a group. Locked as in-scope. **Mechanism is Claude's Discretion, resolved by this research below** — investigate piggybacking on `SecurityStampValidatorOptions.ValidationInterval` (5 minutes, Phase 41) versus a bespoke periodic check in `GroupSessionMiddleware`/`ActiveGroupContextService`. Prefer reusing the existing mechanism over inventing a second one.

- **D-07 [informational]:** Two alternate theories were raised during investigation and explicitly not pursued: (1) reverse-proxy/CDN response caching serving one client's session cookie to another — no `ResponseCaching`/`OutputCache` middleware exists in `Program.cs`; (2) session fixation on login — not confirmed. Neither is in scope for this phase. If a similar leak is ever reported from a **non-SuperAdmin** account, re-open these two leads.

### Claude's Discretion

- Exact hook point and code shape for D-06's periodic re-validation — **resolved below: a lightweight timestamp-gated check inside `GroupSessionMiddleware` itself**, not the `SecurityStampValidator` pipeline (see Pitfall 1 for why the latter doesn't fit).
- Whether `GroupPickerController.Index`'s own group-listing logic already correctly scopes a regular user's selectable list to their own memberships — **verified below: yes, already correct, no change needed.**
- Whether any additional group-scoped controllers beyond Quest/Shop/vote/PlayerSignup need identical filter treatment — **resolved below: yes, two more entities found (`ReminderLogEntity`, `UserTransactionEntity`) sharing the exact same fail-open shape, not named in CONTEXT.md's list of 5. Both must be hardened for D-03 to be exhaustive.**

### Deferred Ideas (OUT OF SCOPE)

None — all four fixes (D-01/D-02 middleware, D-03 filter hardening, D-04/D-05 SelectGroup membership check, D-06 periodic re-validation) are in scope for this phase, following the same "fold in adjacent confirmed issues" precedent Phase 49 established. The two alternate root-cause theories (reverse-proxy cookie caching, session fixation on login) were investigated, not confirmed as relevant here, and explicitly not pursued — see D-07.
</user_constraints>

<phase_requirements>
## Phase Requirements

No REQ-IDs apply to this phase — it is an ad-hoc security bug-fix phase (same pattern as Phases 47-51), not mapped in `.planning/REQUIREMENTS.md`. The phase's scope is fully defined by CONTEXT.md's D-01 through D-06 decisions above; this research maps directly to those instead of REQ-IDs.

| Decision | Description | Research Support |
|----------|-------------|-------------------|
| D-01/D-02 | Extend `GroupSessionMiddleware` group-gate to SuperAdmin on group-scoped routes only | Exact current code, full group-scoped vs. group-agnostic controller enumeration (below) |
| D-03 | Harden 5 named entity filters + 2 more discovered (`ReminderLogEntity`, `UserTransactionEntity`) to fail-closed | Exact line numbers, exact current/target code for all 7 entities |
| D-04/D-05 | `GroupPickerController.SelectGroup` membership check, 404 on non-member | Exact current code, exact primitive to reuse, confirmed `Index` already safe |
| D-06 | Periodic re-validation of `ActiveGroupId` membership currency | Concrete recommended mechanism + code sketch (below) |
</phase_requirements>

## Summary

This phase is a "wire existing, already-proven primitives together correctly" phase, not a new-technology phase — every mechanism needed already exists in this codebase (from Phase 41's `SecurityStampValidatorOptions`, Phase 49's fail-closed filter shape and 404 convention, and the existing `IUserService.GetGroupRoleByIdAsync` membership-check primitive). Direct source inspection confirmed every claim in CONTEXT.md's `<canonical_refs>` section is accurate at the cited (or near-cited) line numbers, and surfaced one gap: **`ReminderLogEntity` and `UserTransactionEntity` share the exact same fail-open `ActiveGroupId == null ||` filter shape as the 5 entities CONTEXT.md named, but were not mentioned in D-03's list.** Both must be hardened identically for D-03 to actually close the escape hatch exhaustively — leaving either one fail-open would recreate the same bug class one entity over.

For D-01/D-02, the codebase has exactly 12 controllers outside the `/platform` area and outside GroupPicker/Account — this research enumerates all of them and classifies each as group-scoped (needs the new gate) or exempt, with `AdminController` (note: this is the *group-scoped* member-management controller in the default area, distinct from the Platform area's own controllers) being the interesting case: every one of its actions already reads `activeGroupContext.ActiveGroupId` and its `Users()` GET action already has an inline `if (groupId == null) return RedirectToAction("Index", "GroupPicker")` defensive check — direct evidence the middleware gap is real and that this controller needs the new gate just as much as `QuestController`.

For D-06, `SecurityStampValidator`'s `OnValidatePrincipal` hook validates the **auth cookie's security stamp claim**, which has no relationship to `ActiveGroupId` (which lives in ASP.NET Core **Session**, backed by the `AspNetSessionState` SQL table the user's original report named — confirmed via `Program.cs`'s `AddDistributedSqlServerCache` + `AddSession` config, 24-hour idle timeout). Piggybacking a group-membership check onto the security-stamp cookie-validation pipeline would require either (a) encoding group membership into a claim that gets re-validated on the same schedule — a much larger change touching claims/cookie regeneration, or (b) a separate, independent check. This research recommends **option (b): a lightweight timestamp-gated check inside `GroupSessionMiddleware` itself**, reusing the same 5-minute duration value as `SecurityStampValidatorOptions.ValidationInterval` for consistency (so the two "how fast do we notice you've been revoked" windows match), but implemented as its own mechanism rather than literally hooking into `ISecurityStampValidator`. A full code sketch is in Code Examples below.

**Primary recommendation:** Implement D-01 through D-06 exactly as CONTEXT.md specifies, extending D-03's scope to 7 entities (not 5) after confirming `ReminderLogEntity` and `UserTransactionEntity` share the identical fail-open shape, and implement D-06 as a middleware-embedded session-timestamp check (not a `SecurityStampValidator` extension) per the concrete design below.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Group-required gate (SuperAdmin included) | API / Backend (`GroupSessionMiddleware`) | — | Request-level authorization gate; must run before any controller action executes, regardless of role |
| Tenant isolation on data reads | Database / Storage (EF Core `HasQueryFilter`) | API / Backend (repositories that call `.Include()` correctly) | Defense-in-depth: the model-level filter is the last line of defense if a controller/middleware check is ever bypassed or forgotten |
| Group-selection membership validation | API / Backend (`GroupPickerController.SelectGroup`) | Database (`IUserService.GetGroupRoleByIdAsync` → `UserGroups` table) | Authorization decision belongs in the controller layer, backed by a repository-level membership lookup |
| Stale-membership re-validation (D-06) | API / Backend (`GroupSessionMiddleware`, session-timestamp check) | Database (`UserGroups` table via `GetGroupRoleByIdAsync`) | Periodic re-check is a request-pipeline concern (like the existing `SecurityStampValidator`), not a data-layer concern — the data-layer check (`GetGroupRoleByIdAsync`) already exists and is reused, not rebuilt |

## Standard Stack

No new packages, no new libraries. This phase exclusively uses APIs already in production use in this codebase:

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Microsoft.EntityFrameworkCore | 10.0.9 [VERIFIED: QuestBoard.Repository.csproj] | `HasQueryFilter` global query filters | Already the app's sole ORM; D-03's fix only changes the filter *predicate*, not the mechanism |
| Microsoft.AspNetCore.Identity | bundled with net10.0 shared framework | `SecurityStampValidatorOptions` (existing, Phase 41), `UserManager` | Confirmed already configured in `Program.cs:67-70`; D-06 investigates reusing this but concludes a separate mechanism fits better (see Pitfall 1) |
| ASP.NET Core Session (`AddSession`) | bundled with net10.0 | Backs `ActiveGroupId`/`ActiveGroupName` via `HttpContext.Session` | Already configured (`Program.cs:189-194`, `IdleTimeout = 24h`, `SqlServer`-backed distributed cache) — the exact mechanism the user's original "AspNetSessionState" hypothesis named |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Middleware-embedded timestamp check for D-06 | Custom `ISecurityStampValidator` implementation, or a custom claim re-validated via `CookieAuthenticationOptions.Events.OnValidatePrincipal` | Rejected — `ActiveGroupId` lives in Session, not in the auth cookie's claims. Wiring it into the cookie-validation pipeline would require either promoting `ActiveGroupId` to a claim (a much bigger, riskier change touching sign-in/claims-regeneration code) or adding a second, parallel re-validation path anyway — no actual code-reuse benefit over a direct middleware check |

**Installation:** None required — no new NuGet packages, no schema migration.

**Version verification:** `net10.0` / EF Core 10.0.9 confirmed via `QuestBoard.Repository/QuestBoard.Repository.csproj:4,10`. No new package references needed for any of D-01 through D-06.

## Package Legitimacy Audit

No external packages are being installed in this phase. All APIs used ship in the `Microsoft.AspNetCore.App` / `Microsoft.EntityFrameworkCore` references already in the `.csproj` files.

**Packages removed due to slopcheck [SLOP] verdict:** none (no packages evaluated — none proposed)
**Packages flagged as suspicious [SUS]:** none

## Architecture Patterns

### System Architecture Diagram

```
Request pipeline (Program.cs:300-308, confirmed order):
  UseMiddleware<MobileDetectionMiddleware>()
       │
       ▼
  UseSession()                          ← ActiveGroupId lives here (SQL-backed, 24h idle timeout)
       │
       ▼
  UseAuthentication()                   ← resolves ClaimsPrincipal from auth cookie
       │
       ▼
  UseMiddleware<GroupSessionMiddleware>()   ◄── THIS PHASE'S D-01/D-02/D-06 CHANGES
       │
       │  1. Anonymous? → pass through, [Authorize] handles it
       │  2. SuperAdmin? → CURRENTLY: pass through unconditionally (BUG)
       │                   AFTER D-01: only pass through if path is exempt (D-02 list)
       │  3. Exempt path (/groups/pick, /GroupPicker, /Account, /platform, /Error)? → pass through
       │  4. ActiveGroupId == null? → GET/HEAD redirect to /groups/pick; else 409 Conflict
       │  5. AFTER D-06: ActiveGroupId present but stale-check interval elapsed?
       │        → re-validate membership via GetGroupRoleByIdAsync
       │        → if null (no longer a member): treat as (4), same redirect/409 behavior
       │
       ▼
  UseAuthorization()                    ← [Authorize]/[Authorize(Policy=...)] enforced here
       │
       ▼
  Controller action executes
       │
       ▼
  EF Core query via QuestBoardContext   ◄── THIS PHASE'S D-03 CHANGE
       │
       │  HasQueryFilter for Quest/ShopItem/ProposedDate/PlayerDateVote/PlayerSignup
       │  (+ ReminderLog, UserTransaction — discovered by this research, same fix needed)
       │
       │  BEFORE: ActiveGroupId == null || e.GroupId == ActiveGroupId   (fail-OPEN)
       │  AFTER:  ActiveGroupId != null && e.GroupId == ActiveGroupId   (fail-CLOSED)
       │
       ▼
  Response (now correctly scoped even if middleware gate were ever bypassed)


GroupPickerController.SelectGroup (D-04/D-05):
  POST groupId ──► groupService.GetByIdAsync(groupId)   [existing: "does group exist" check]
                        │
                        ▼ NEW CHECK
                   userService.GetGroupRoleByIdAsync(userId, groupId)
                        │
                        ├─ null (not a member) ──► 404 Not Found
                        └─ non-null ──► HttpContext.Session.SetInt32(ActiveGroupId, group.Id) [existing]
```

### Recommended Project Structure

No new files/folders. All changes are in-place edits to existing files:
```
QuestBoard.Service/
├── Middleware/
│   └── GroupSessionMiddleware.cs        # D-01/D-02 restructure + D-06 new re-validation block
├── Controllers/
│   └── GroupPickerController.cs         # D-04/D-05 SelectGroup membership check
QuestBoard.Repository/
├── Entities/
│   └── QuestBoardContext.cs             # D-03: 7 entities' HasQueryFilter hardened (not 5)
QuestBoard.IntegrationTests/
├── Controllers/
│   ├── GroupSessionMiddlewareIntegrationTests.cs   # existing SuperAdmin test REWRITE needed (see Pitfall 2)
│   └── GroupPickerControllerIntegrationTests.cs    # new SelectGroup non-member test
QuestBoard.UnitTests/
├── Repository/
│   └── PlayerSignupRepositoryTests.cs   # pattern to mirror for new filter regression tests (or a new QuestBoardContextFilterTests.cs)
.planning/codebase/
└── CONCERNS.md                          # lines 288-292 stale rationale to correct per canonical_refs
```

### Pattern 1: Restructuring `GroupSessionMiddleware` — SuperAdmin gated like everyone else, but exempt paths still short-circuit first

**What:** Move the exempt-path check *before* (or combine with) the SuperAdmin short-circuit, so SuperAdmin no longer bypasses the group-required gate on group-scoped routes, but still bypasses it on `/platform`, `/Error`, GroupPicker, and Account.

**When to use:** This exact middleware, this exact change.

**Current code (confirmed, `QuestBoard.Service/Middleware/GroupSessionMiddleware.cs:57-96`):**
```csharp
public async Task InvokeAsync(HttpContext context)
{
    if (context.User.Identity?.IsAuthenticated != true)
    {
        await next(context);
        return;
    }

    if (context.User.IsInRole("SuperAdmin"))
    {
        await next(context);   // <-- BUG: bypasses group-gate on EVERY route, not just exempt ones
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
            context.Response.StatusCode = StatusCodes.Status409Conflict;
            return;
        }

        var returnUrl = context.Request.Path + context.Request.QueryString;
        context.Response.Redirect($"/groups/pick?returnUrl={Uri.EscapeDataString(returnUrl)}");
        return;
    }

    await next(context);
}
```

**Recommended fix (D-01/D-02 — swap the order of the two checks):**
```csharp
public async Task InvokeAsync(HttpContext context)
{
    if (context.User.Identity?.IsAuthenticated != true)
    {
        await next(context);
        return;
    }

    // Exempt paths (picker itself, auth, platform, error routes) pass through for EVERY role,
    // including SuperAdmin — these are the genuinely group-agnostic areas (D-02).
    if (ExemptPathPrefixes.Any(prefix => context.Request.Path.StartsWithSegments(prefix)))
    {
        await next(context);
        return;
    }

    // SuperAdmin is no longer exempt from the group-required gate on group-scoped routes (D-01).
    // A SuperAdmin with no ActiveGroupId must pick a group first, exactly like any other role,
    // so the board is never rendered in an ambiguous "every group merged" state.

    var groupContext = context.RequestServices.GetRequiredService<IActiveGroupContext>();
    if (groupContext.ActiveGroupId == null)
    {
        if (!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method))
        {
            context.Response.StatusCode = StatusCodes.Status409Conflict;
            return;
        }

        var returnUrl = context.Request.Path + context.Request.QueryString;
        context.Response.Redirect($"/groups/pick?returnUrl={Uri.EscapeDataString(returnUrl)}");
        return;
    }

    // D-06 — see Code Examples below for the periodic re-validation block inserted here.

    await next(context);
}
```

**Important:** the XML doc comment on the class (lines 9-29) and the inline comment above the `ExemptPathPrefixes` array (lines 32-40) both describe the *old* "SuperAdmin passes through, checked BEFORE the group check to avoid a redirect loop" ordering rationale — these comments must be rewritten as part of this fix, not just the code, or a future reader will be misled about why the ordering is what it is.

### Pattern 2: Group-scoped vs. group-agnostic route enumeration (concrete list for D-01/D-02)

Enumerated every controller in the codebase (excluding the `/platform` Area, which is already exempt via the `/platform` path prefix):

| Controller | Namespace/Area | `[Authorize]` shape | Group-scoped? | Notes |
|------------|----------------|---------------------|---------------|-------|
| `QuestController` | `Controllers/QuestBoard`, `[Route("quests")]` | `[Authorize]` class-level + `DungeonMasterOnly` on mutating actions | **Yes** | The original reported leak surface |
| `CalendarController` | `Controllers/QuestBoard` | `[Authorize]` | **Yes** | Named explicitly in existing middleware test |
| `QuestLogController` | `Controllers/QuestBoard` | `[Authorize]` | **Yes** | Named explicitly in existing middleware test |
| `PlayersController` | `Controllers/QuestBoard` | `[Authorize]` | **Yes** | Lists group's DMs/Players |
| `ShopController` | `Controllers/Shop` | `[Authorize]` | **Yes** | Shop board |
| `ShopManagementController` | `Controllers/Shop` | `[Authorize(Policy = "DungeonMasterOnly")]` | **Yes** | DM shop management within active group |
| `GuildMembersController` | `Controllers/Characters` | `[Authorize]` | **Yes** | Fixed for group scoping in Phase 49; still needs the middleware gate too |
| `DungeonMasterController` | `Controllers/DungeonMaster` | `[Authorize]` + `DungeonMasterOnly` on some actions | **Yes** | Fixed for group scoping in Phase 49 |
| `AdminController` | `Controllers/Admin` (default area, **not** `/platform`) | `[Authorize(Policy = "AdminOnly")]` | **Yes** | Group-scoped member management (promote/demote/create/delete users in the *active* group). Confirmed: every action reads `activeGroupContext.ActiveGroupId`; `Users()` GET already has its own inline `if (groupId == null) return RedirectToAction("Index", "GroupPicker")` guard — direct evidence this controller needs the gate |
| `HomeController` | `Controllers/QuestBoard` | none (no `[Authorize]`) | **No** — but harmless | Only action redirects authenticated users to `Quest/Index` or shows the anonymous landing page; not itself group-scoped data, and an authenticated SuperAdmin hitting `/` with no group would be redirected onward to `/Quest` which then correctly gates |
| `AccountController` | `Controllers/Admin` | `[Authorize]` on most actions (Login/ForgotPassword/AccessDenied/SetPassword are anonymous-reachable) | **No — exempt** | Already on the exempt-path list via `ControllerNameOf<AccountController>()` |
| `EmailPreviewController` | `Controllers/Admin` | `[Authorize(Policy = "AdminOnly")]` | **No** | Renders static email template previews — does not query any group-scoped entity; confirm during planning it truly has zero `IActiveGroupContext`/group-scoped-entity usage before leaving it off the gated list |
| `GroupPickerController` | `Controllers/` (root) | `[Authorize]` | **No — exempt** | Already on the exempt-path list (`/groups/pick` + `ControllerNameOf<GroupPickerController>()`) |
| `GroupController` (Platform area) | `Areas/Platform/Controllers` | `[Authorize(Policy = "SuperAdminOnly")]` | **No — exempt** | Covered by the `/platform` prefix |
| `UsersController` (Platform area) | `Areas/Platform/Controllers` | `[Authorize(Policy = "SuperAdminOnly")]` | **No — exempt** | Covered by the `/platform` prefix |

**Recommendation:** Do not attempt to build an explicit allowlist/denylist of controllers in the middleware — the middleware already works path-prefix-based (exempt list), and D-01's fix is simply "stop bypassing the exempt-path check for SuperAdmin." No middleware code needs a controller-name list; this table exists purely so planning/verification has a concrete, exhaustive checklist of routes to manually verify post-fix (e.g. integration test coverage should touch at least one representative group-scoped route per controller above, extending the existing `[Theory]`-based test in `GroupSessionMiddlewareIntegrationTests.cs`).

### Pattern 3: `GroupPickerController.SelectGroup` membership check (D-04/D-05)

**Current code (confirmed, `QuestBoard.Service/Controllers/GroupPickerController.cs:41-51`):**
```csharp
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> SelectGroup(int groupId, string? returnUrl = null)
{
    var group = await groupService.GetByIdAsync(groupId);
    if (group == null) return NotFound();

    HttpContext.Session.SetInt32(SessionKeys.ActiveGroupId, group.Id);
    HttpContext.Session.SetString(SessionKeys.ActiveGroupName, group.Name);
    return RedirectToLocal(returnUrl);
}
```

**Recommended fix — requires injecting `IUserService` (not currently a constructor dependency) and resolving the current user's id:**
```csharp
// Source: QuestBoard.Domain/Interfaces/IUserService.cs:79 — existing primitive, reused as-is
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> SelectGroup(int groupId, string? returnUrl = null)
{
    var group = await groupService.GetByIdAsync(groupId);
    if (group == null) return NotFound();

    var isSuperAdmin = User.IsInRole("SuperAdmin");
    if (!isSuperAdmin)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var role = await userService.GetGroupRoleByIdAsync(userId, groupId);
        if (role == null) return NotFound();
    }

    HttpContext.Session.SetInt32(SessionKeys.ActiveGroupId, group.Id);
    HttpContext.Session.SetString(SessionKeys.ActiveGroupName, group.Name);
    return RedirectToLocal(returnUrl);
}
```

Note: `Index` (the GET action) already resolves `userId` the same way (`int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!)`, line 20) and already branches on `isSuperAdmin` — the pattern above matches the controller's own existing style exactly, no new pattern introduced. `groupService` is already injected; `userService` (`IUserService`) needs adding to the constructor (currently only `IGroupService groupService`).

**SuperAdmin case:** SuperAdmin's `Index` already lists every group via `GetAllWithMemberCountAsync()` (not membership-scoped), so SuperAdmin legitimately can select any group without a membership row — the `isSuperAdmin` bypass above preserves that, consistent with `GetEffectiveGroupRoleAsync`'s existing "SuperAdmin as automatic Admin-equivalent, no membership required" pattern documented on the interface.

**Confirmed (per CONTEXT.md's "verify during research" item):** `GroupPickerController.Index` (`GroupPickerController.cs:17-39`) already correctly scopes: `isSuperAdmin ? groupService.GetAllWithMemberCountAsync() : groupService.GetGroupsForUserAsync(userId)`. Non-SuperAdmin callers only ever see their own memberships in the picker list today. **No change needed here** — this was already correct before this phase; D-04 makes the `SelectGroup` POST-side consistent with what `Index` already only *offers*.

### Anti-Patterns to Avoid
- **Building a controller-name allowlist inside `GroupSessionMiddleware`:** the middleware is deliberately path-prefix/exempt-list-based, not controller-aware. Don't introduce a second classification mechanism — reordering the two existing checks (exempt-path vs. SuperAdmin) is the entire fix.
- **Leaving `ReminderLogEntity`/`UserTransactionEntity` on the fail-open shape while fixing the other 5:** these share the exact same filter predicate pattern; hardening only the 5 CONTEXT.md named would leave 2 more instances of the identical bug class, discoverable by the same kind of incident (SuperAdmin, null `ActiveGroupId`, any query touching reminders or shop transactions).
- **Hooking D-06 into `ISecurityStampValidator`/claims:** `ActiveGroupId` is Session state, not a claim. Forcing it into the claims/cookie-revalidation pipeline is a much larger, riskier change (touches sign-in, claims transformation, and cookie regeneration) for no benefit over a direct, independent middleware check — see Pitfall 1.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| "Is this user still a member of group X" check | A new repository method / raw `DbContext.UserGroups.Any(...)` query | `IUserService.GetGroupRoleByIdAsync(int userId, int groupId)` | Already exists, already returns `null` for non-members, already proven in Phase 49 for the identical purpose (twice) |
| Periodic re-validation of session-derived authorization state | A new custom `ISecurityStampValidator`, or bespoke cookie claims-refresh middleware | A simple `DateTime` timestamp written to `HttpContext.Session` at group-selection time, checked against `DateTime.UtcNow` in `GroupSessionMiddleware` on each request | The existing `SecurityStampValidator` mechanism solves a *different* problem (cookie/claims staleness) — reusing its *concept* (periodic re-check on a timer) is right, but reusing its *code path* is wrong since `ActiveGroupId` isn't a claim |
| Cross-tenant "does this exist" response | A `Forbid()`/403 | `NotFound()`/404 | Established, locked convention across this codebase (Phase 49 D-04/D-09/D-13, this phase's D-05) |

**Key insight:** Every primitive D-01 through D-06 need already exists in this codebase. This phase is entirely about correctly *sequencing* and *applying* existing primitives (the exempt-path check, the fail-closed filter shape, `GetGroupRoleByIdAsync`, the 404 convention, and a timer-gated re-check pattern conceptually borrowed from `SecurityStampValidatorOptions`) — not building anything new.

## Common Pitfalls

### Pitfall 1: `SecurityStampValidator` cannot directly validate `ActiveGroupId` — it validates a cookie claim, and `ActiveGroupId` is Session state, not a claim
**What goes wrong:** A plan might try to literally extend `SecurityStampValidatorOptions`/`ISecurityStampValidator` to also check group membership, assuming "5-minute re-validation" is a single unified mechanism.
**Why it happens:** `SecurityStampValidator` hooks into `CookieAuthenticationEvents.OnValidatePrincipal` and compares the auth cookie's `SecurityStamp` claim against `AspNetUsers.SecurityStamp` in the database — it only fires with access to the `ClaimsPrincipal`, not `HttpContext.Session`. `ActiveGroupId` lives in `HttpContext.Session` (`ActiveGroupContextService.cs:24`), a completely separate store (SQL-backed distributed cache via `AddDistributedSqlServerCache`, `Program.cs:176-182`) with no claims relationship.
**How to avoid:** Implement D-06 as an independent check inside `GroupSessionMiddleware` (which already has `HttpContext.Session` access via `IActiveGroupContext`), gated by its own timestamp written to session at selection time — reuse the *5-minute duration value* for consistency with `SecurityStampValidatorOptions.ValidationInterval`, but as a separate constant/config, not a shared code path.
**Warning signs:** A plan step that tries to implement `ISecurityStampValidator` or subscribes to `OnValidatePrincipal` for this purpose is over-engineering a much simpler fix.

### Pitfall 2: An existing integration test asserts the exact OLD (buggy) behavior and must be rewritten, not just extended
**What goes wrong:** `QuestBoard.IntegrationTests/Controllers/GroupSessionMiddlewareIntegrationTests.cs` has a test named `SuperAdmin_NoActiveGroup_NotRedirectedByMiddleware` (lines 45-66) whose entire assertion is "SuperAdmin with null `ActiveGroupId` hitting `/quests` must NOT be redirected to `/groups/pick`" — this is precisely the behavior D-01 removes. If a plan only *adds* new tests without touching this one, the test suite will have two directly contradictory tests and CI will fail after the fix ships.
**Why it happens:** The test was written to pin down the pre-fix intentional design ("SuperAdmin passes through unconditionally"), which this phase is deliberately reversing.
**How to avoid:** This test must be rewritten to assert the new behavior: a SuperAdmin with `ActiveGroupId == null` hitting `/quests` (a group-scoped route) **should** now be redirected to `/groups/pick`, matching every other role. Its doc comment (lines 42-44, which explicitly documents the old "avoids a redirect loop" ordering rationale) must also be corrected.
**Warning signs:** Running the full suite after implementing D-01 without touching this file — it will fail immediately, which is actually a useful safety net (if it doesn't fail, the guard-order fix wasn't applied correctly).

### Pitfall 3: `CONCERNS.md` documents the exact pre-fix behavior as a known/accepted gap, and it must be corrected, not just left stale
**What goes wrong:** `.planning/codebase/CONCERNS.md` lines 288-292 explicitly say: *"A SuperAdmin querying `/quests` should list all quests across all groups (via `IgnoreQueryFilters`), not zero quests from a null group"* and recommends adding a test asserting SuperAdmin sees cross-group data as correct behavior. This directly contradicts D-01's fix (SuperAdmin should now be gated to a single group like everyone else) and lines 306-310 (tenant isolation edge-case gap) directly names the exact class of risk this phase closes.
**Why it happens:** `CONCERNS.md` was written before this phase's decisions were made; it accurately described the codebase's *previous* intentional design.
**How to avoid:** CONTEXT.md's canonical_refs explicitly calls this out — update/correct `CONCERNS.md` lines 288-292 during implementation (not a blocking decision, but a documentation-debt item this phase should not skip, since leaving it as-is would actively mislead the next reader into "fixing" this phase's fix back to the old behavior).
**Warning signs:** A future phase citing `CONCERNS.md`'s stale text as justification to re-introduce the SuperAdmin bypass.

### Pitfall 4: `ReminderLogEntity` and `UserTransactionEntity` were not named in D-03's list but share the identical vulnerable shape
**What goes wrong:** Applying D-03's fix only to the 5 named entities (`QuestEntity`, `ShopItemEntity`, `ProposedDateEntity`, `PlayerDateVoteEntity`, `PlayerSignupEntity`) leaves `ReminderLogEntity` (`QuestBoardContext.cs:290-293`) and `UserTransactionEntity` (`QuestBoardContext.cs:323-326`) fail-open, since both use the exact same `activeGroupContext.ActiveGroupId == null || <navigation>.GroupId == activeGroupContext.ActiveGroupId` predicate shape.
**Why it happens:** CONTEXT.md's investigation focused on the entities directly involved in the reported "quest board merged" symptom (Quest/ShopItem/vote/signup chain); `ReminderLogEntity` (email reminder audit log) and `UserTransactionEntity` (shop purchase history) aren't rendered on the quest board itself, so they weren't part of the reported symptom, but they are queried elsewhere (e.g. Admin transaction history views, Hangfire's reminder-dedup logic) and would exhibit the identical SuperAdmin-sees-everything leak if queried with `ActiveGroupId == null`.
**How to avoid:** Extend D-03's fix to both. Verified via direct grep of `QuestBoardContext.cs` (`.HasQueryFilter` occurrences) that these are the only two additional entities sharing this exact shape — `CharacterEntity`, `CharacterClassEntity`, `CharacterImageEntity` are already fail-closed (Phase 49), and `UserEntity` intentionally has no filter at all (breaks ASP.NET Core Identity if filtered).
**Warning signs:** A plan-checker or reviewer that only checks the 5 named entities against the codebase and doesn't independently grep `QuestBoardContext.cs` for the `== null ||` shape will miss this.

### Pitfall 5: `IUserService` is not currently injected into `GroupPickerController` — a new constructor dependency is needed
**What goes wrong:** A plan might assume `GetGroupRoleByIdAsync` can be called without checking DI wiring.
**Why it happens:** `GroupPickerController`'s current constructor is `GroupPickerController(IGroupService groupService)` — only `IGroupService`, confirmed via direct read. `IUserService` needs adding.
**How to avoid:** Add `IUserService userService` to the primary constructor parameter list (already DI-registered app-wide — used throughout `AdminController`, `AccountController`, etc. — no new service registration needed in `Program.cs`, just a new constructor parameter on this one controller).
**Warning signs:** A build error ("no argument given that corresponds to the required parameter") if the constructor change is missed but the method body already calls `userService.GetGroupRoleByIdAsync(...)`.

### Pitfall 6: Test harness limitation — `factory.TestGroupContext` overrides the real DI-registered `IActiveGroupContext`, meaning Session-cookie round-tripping is NOT reliably testable end-to-end
**What goes wrong:** A plan might try to write an integration test for D-06 that relies on the test client's session cookie actually persisting `ActiveGroupId` across two separate HTTP calls to assert re-validation kicks in after a simulated time gap.
**Why it happens:** `GroupPickerControllerIntegrationTests.cs`'s own comments (lines 148-153, 163-167) explicitly document that the `TestAuthHandler`-based client (Authorization header, not cookies) does not round-trip ASP.NET Core session cookies the way a real browser would — tests instead rely on `factory.TestGroupContext.ActiveGroupId` (a shared mutable override) to simulate a given group state, and chain explicit `returnUrl` values across requests rather than relying on real session persistence.
**How to avoid:** Design D-06's test coverage around `factory.TestGroupContext`-style state injection (e.g., a test double or a way to inject a "session was set N minutes ago" timestamp directly, mirroring the existing `MutableGroupContext`/`TestGroupContext` pattern) rather than attempting genuine session-cookie time-travel in an integration test. A unit test against `GroupSessionMiddleware.InvokeAsync` directly (constructing a fake `HttpContext` with a controlled session) may be more reliable than an integration test for the timestamp-elapsed branch specifically.
**Warning signs:** A flaky or unimplementable integration test that tries to `Thread.Sleep`/manipulate real wall-clock time against a live session cookie.

## Code Examples

### D-06 — Recommended periodic re-validation mechanism (concrete sketch)

```csharp
// Source: this research — new code, not from an external reference.
// Rationale: ActiveGroupId lives in HttpContext.Session, not in a claim, so this reuses the
// *concept* of SecurityStampValidatorOptions.ValidationInterval (periodic re-check, bounded
// staleness window) without hooking into the claims/cookie-validation pipeline itself.

// New session key, alongside the existing ones in SessionKeys.cs:
public static class SessionKeys
{
    public const string ActiveGroupId = "ActiveGroupId";
    public const string ActiveGroupName = "ActiveGroupName";
    public const string ActiveGroupValidatedAtUtc = "ActiveGroupValidatedAtUtc"; // NEW
}

// GroupPickerController.SelectGroup and Index's single-group auto-select branch both need to
// stamp this new key whenever ActiveGroupId is (re)written:
HttpContext.Session.SetString(SessionKeys.ActiveGroupValidatedAtUtc, DateTime.UtcNow.ToString("O"));

// GroupSessionMiddleware — new block inserted after the existing null-check, before `await next(context)`:
private static readonly TimeSpan MembershipRevalidationInterval = TimeSpan.FromMinutes(5); // matches SecurityStampValidatorOptions.ValidationInterval (Phase 41) for a consistent staleness bound, not because they share code

// ... inside InvokeAsync, after the existing `if (groupContext.ActiveGroupId == null) { ... }` block:
var session = context.Session;
var validatedAtRaw = session.GetString(SessionKeys.ActiveGroupValidatedAtUtc);
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
        // No longer a member — clear the stale group and treat exactly like the null-ActiveGroupId
        // case above (redirect on GET/HEAD, 409 on mutating verbs).
        session.Remove(SessionKeys.ActiveGroupId);
        session.Remove(SessionKeys.ActiveGroupName);
        session.Remove(SessionKeys.ActiveGroupValidatedAtUtc);

        if (!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method))
        {
            context.Response.StatusCode = StatusCodes.Status409Conflict;
            return;
        }

        var returnUrl = context.Request.Path + context.Request.QueryString;
        context.Response.Redirect($"/groups/pick?returnUrl={Uri.EscapeDataString(returnUrl)}");
        return;
    }

    session.SetString(SessionKeys.ActiveGroupValidatedAtUtc, DateTime.UtcNow.ToString("O"));
}
```

**Design notes:**
- SuperAdmin is excluded from this re-check (`!context.User.IsInRole("SuperAdmin")`) because SuperAdmin's group selection is not membership-gated in the first place (D-04's bypass) — there is no membership row to go stale.
- This does add one extra DB round-trip (`GetGroupRoleByIdAsync`) roughly every 5 minutes per active user, not per-request — negligible load at this app's scale (same reasoning Phase 41 used for its own 5-minute `SecurityStampValidatorOptions` interval, per `Program.cs:65-66`'s comment).
- `IUserService` must be resolved via `context.RequestServices.GetRequiredService<IUserService>()` inside the middleware (constructor injection isn't available for scoped services in singleton middleware) — this mirrors how `IActiveGroupContext` is already resolved in the same method.

### D-03 — Exact filter changes needed for all 7 entities (5 named + 2 discovered)

```csharp
// Source: QuestBoard.Repository/Entities/QuestBoardContext.cs — current (fail-open) vs. target (fail-closed)

// QuestEntity — current lines 251-254:
modelBuilder.Entity<QuestEntity>()
    .HasQueryFilter(e =>
        activeGroupContext.ActiveGroupId == null ||
        e.GroupId == activeGroupContext.ActiveGroupId);
// TARGET:
modelBuilder.Entity<QuestEntity>()
    .HasQueryFilter(e =>
        activeGroupContext.ActiveGroupId != null &&
        e.GroupId == activeGroupContext.ActiveGroupId);

// ShopItemEntity — current lines 256-259 — same transformation.
// ProposedDateEntity — current lines 266-269 (note: filters via pd.Quest.GroupId, not a direct GroupId) — same transformation, keep the navigation.
// PlayerDateVoteEntity — current lines 274-277 (via pdv.ProposedDate.Quest.GroupId) — same transformation, keep the navigation.
// PlayerSignupEntity — current lines 283-286 (via ps.Quest.GroupId) — same transformation, keep the navigation.

// DISCOVERED — NOT in CONTEXT.md's list of 5, but same shape, same fix required:

// ReminderLogEntity — current lines 290-293:
modelBuilder.Entity<ReminderLogEntity>()
    .HasQueryFilter(r =>
        activeGroupContext.ActiveGroupId == null ||
        r.Quest.GroupId == activeGroupContext.ActiveGroupId);
// TARGET:
modelBuilder.Entity<ReminderLogEntity>()
    .HasQueryFilter(r =>
        activeGroupContext.ActiveGroupId != null &&
        r.Quest.GroupId == activeGroupContext.ActiveGroupId);

// UserTransactionEntity — current lines 323-326:
modelBuilder.Entity<UserTransactionEntity>()
    .HasQueryFilter(t =>
        activeGroupContext.ActiveGroupId == null ||
        t.ShopItem.GroupId == activeGroupContext.ActiveGroupId);
// TARGET:
modelBuilder.Entity<UserTransactionEntity>()
    .HasQueryFilter(t =>
        activeGroupContext.ActiveGroupId != null &&
        t.ShopItem.GroupId == activeGroupContext.ActiveGroupId);
```

Also update the explanatory comment block at lines 244-250 (currently states "Null = see all (SuperAdmin/seeding contexts intentionally bypass group scoping)" — this rationale is being deliberately reversed by this phase and must be rewritten to describe the new fail-closed intent, matching `CharacterEntity`'s existing comment style at lines 295-298).

**Confirmed unaffected:** `DailyReminderJob` → `QuestRepository.GetQuestsForTomorrowAllGroupsAsync` (`QuestRepository.cs:264-267`) is the only production `.IgnoreQueryFilters()` call site in the entire repo (grep confirmed zero other production hits — all other matches are `.planning/` docs). `.IgnoreQueryFilters()` bypasses `HasQueryFilter` entirely regardless of predicate shape, so this job is unaffected by the D-03 change.

### Existing filter-regression test pattern to extend (D-03 test coverage)

```csharp
// Source: QuestBoard.UnitTests/Repository/PlayerSignupRepositoryTests.cs:13-29, 399-407 (existing pattern)
private static QuestBoardContext CreateContext(string databaseName, IActiveGroupContext activeGroupContext)
{
    var options = new DbContextOptionsBuilder<QuestBoardContext>()
        .UseInMemoryDatabase(databaseName)
        .Options;

    return new QuestBoardContext(options, activeGroupContext);
}

private sealed class TestActiveGroupContext : IActiveGroupContext
{
    public int? ActiveGroupId => null;   // simulates the SuperAdmin/null-group case
}

private sealed class MutableTestGroupContext : IActiveGroupContext
{
    public int? ActiveGroupId { get; set; }
}

// New regression test shape (one per hardened entity, or parameterized):
// seed two groups' worth of QuestEntity rows, then query with TestActiveGroupContext
// (ActiveGroupId == null) and assert ZERO rows returned (fail-closed), not "every row" (fail-open).
```

## State of the Art

No frameworks or approaches have changed here — EF Core global query filters, ASP.NET Core middleware ordering, and `SecurityStampValidatorOptions` are all stable, long-standing mechanisms, all already in production use in this exact codebase since earlier phases (Phase 28 tenant isolation, Phase 41 security stamp). Nothing to report as "old vs. current approach" — this phase is a correctness fix to existing mechanisms, not an upgrade.

**Deprecated/outdated:**
- `.planning/codebase/CONCERNS.md` lines 288-292's stated expectation ("SuperAdmin should see all groups' quests, not zero") is superseded by this phase's D-01 — must be corrected as part of implementation (see Pitfall 3).

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | The recommended D-06 session-key name (`ActiveGroupValidatedAtUtc`) and the exact middleware code shape in Code Examples | Code Examples, Pattern 1 | None — CONTEXT.md explicitly leaves D-06's exact hook point/shape to Claude's discretion; this is one reasonable implementation, not a locked requirement. Planner is free to adjust naming/placement. |
| A2 | `EmailPreviewController` requires no group-scoped gate because it renders only static email template previews with no `IActiveGroupContext`/group-scoped-entity dependency | Pattern 2 (route enumeration table) | Low — confirmed via constructor signature (`IEmailRenderService`, `IOptions<EmailSettings>` only, no `IActiveGroupContext`), but the controller's action bodies were not read line-by-line in this research pass; planner should do a final confirmation grep for any group-scoped entity usage before excluding it from the gated-route list |
| A3 | 5-minute duration reuse for D-06's re-validation interval (matching `SecurityStampValidatorOptions.ValidationInterval`) is the right choice, rather than a different interval | Summary, Code Examples | Low — CONTEXT.md explicitly frames this as "the same class of correctness gap... just on the time axis," and Phase 41 already established 5 minutes as this app's accepted staleness bound for a comparable "revoked mid-session" scenario; reusing the same number is a reasonable default the user can override during plan review |

## Open Questions

1. **Should D-06's membership re-check apply to EVERY group-scoped request, or only after the interval elapses (as sketched)?**
   - What we know: The sketch above only re-checks after `MembershipRevalidationInterval` has elapsed since the last check, mirroring `SecurityStampValidatorOptions.ValidationInterval`'s own "not on every single request" design (avoids a DB round-trip per request).
   - What's unclear: Whether the user's stated correctness bar ("as if a normal user, no confusion") implies they'd prefer an immediate/every-request check despite the extra DB load, given this app's small scale (~17 users, per prior phase context).
   - Recommendation: Keep the interval-gated design — it directly mirrors an already-accepted pattern (Phase 41) in this same codebase for the same class of problem, and a 5-minute worst-case staleness window is a reasonable, precedented tradeoff. Surface this as a discussion point if the planner wants explicit user sign-off before implementing.

2. **Should `EmailPreviewController` be added to the group-scoped gate list defensively, even though it appears to need no group context today?**
   - What we know: Its constructor has no `IActiveGroupContext` dependency and its purpose (admin-only email template preview) is inherently group-agnostic.
   - What's unclear: Whether a future phase might add group-scoped data to email previews without updating this classification.
   - Recommendation: Leave it off the gated list for this phase (matches its current actual behavior), but note in the plan/PR that this classification should be re-checked if `EmailPreviewController` ever gains group-scoped data dependencies.

## Environment Availability

Skipped — this phase has no external tool/service dependencies beyond the already-running SQL Server and the app's own build/test toolchain, both already verified functional by every prior phase in this milestone (Phases 43-51 completed successfully in the same environment). No new NuGet packages, no new infrastructure.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit v3.2.2 [VERIFIED: QuestBoard.IntegrationTests.csproj] + `Microsoft.AspNetCore.Mvc.Testing` 10.0.9 `WebApplicationFactory`; `Microsoft.EntityFrameworkCore.InMemory` for unit-level filter tests |
| Config file | `QuestBoard.IntegrationTests/QuestBoard.IntegrationTests.csproj`, `QuestBoard.UnitTests/QuestBoard.UnitTests.csproj` (existing, no changes needed) |
| Quick run command | `dotnet test --filter FullyQualifiedName~GroupSessionMiddlewareIntegrationTests` / `~GroupPickerControllerIntegrationTests` / `~PlayerSignupRepositoryTests` |
| Full suite command | `dotnet test` |

### Phase Requirements → Test Map
| Decision | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| D-01/D-02 | SuperAdmin with null `ActiveGroupId` is now redirected/409'd on group-scoped routes, still exempt on `/platform`/`/Error`/GroupPicker/Account | integration | `dotnet test --filter FullyQualifiedName~GroupSessionMiddlewareIntegrationTests` | Partial — existing `SuperAdmin_NoActiveGroup_NotRedirectedByMiddleware` (lines 45-66) must be REWRITTEN (Pitfall 2), not left as-is; existing `[Theory]` route list (Calendar/DungeonMaster/QuestLog) should gain a SuperAdmin-authenticated variant |
| D-03 | Fail-closed filter on all 7 entities returns zero rows (not all-groups rows) when `ActiveGroupId` is null | unit | `dotnet test --filter FullyQualifiedName~PlayerSignupRepositoryTests` (extend) or new `QuestBoardContextFilterTests.cs` | ❌ Wave 0 — no existing test asserts the fail-closed shape for Quest/ShopItem/ProposedDate/PlayerDateVote/PlayerSignup/ReminderLog/UserTransaction with a null `ActiveGroupId`; `TestActiveGroupContext`/`MutableTestGroupContext` pattern already exists to build these on |
| D-04/D-05 | `SelectGroup` POST with a non-member `groupId` returns 404; member `groupId` still succeeds | integration | `dotnet test --filter FullyQualifiedName~GroupPickerControllerIntegrationTests` | ❌ Wave 0 — no existing test posts a `groupId` the caller isn't a member of; existing `SelectGroup_ShouldPersistActiveGroupInSession` test only covers the happy path |
| D-06 | Session's `ActiveGroupId` membership re-validated after the interval elapses; removed member is gated out | unit (middleware-level, per Pitfall 6) | new test file/method against `GroupSessionMiddleware.InvokeAsync` with a fake `HttpContext`/session | ❌ Wave 0 — new test needed; per Pitfall 6, prefer a direct unit test over an integration test given the test harness's session-cookie round-trip limitation |

### Sampling Rate
- **Per task commit:** targeted `dotnet test --filter FullyQualifiedName~{TestClass}`
- **Per wave merge:** `dotnet test` (full suite)
- **Phase gate:** Full suite green before `/gsd:verify-work`

### Wave 0 Gaps
- [ ] `QuestBoard.IntegrationTests/Controllers/GroupSessionMiddlewareIntegrationTests.cs` — rewrite `SuperAdmin_NoActiveGroup_NotRedirectedByMiddleware` to assert the new (opposite) behavior; add SuperAdmin coverage to the existing `[Theory]` protected-route list
- [ ] `QuestBoard.UnitTests/Repository/` — new or extended test file asserting all 7 hardened entity filters return zero rows for a null `ActiveGroupId` (mirror `PlayerSignupRepositoryTests.cs`'s `TestActiveGroupContext`/`MutableTestGroupContext` pattern)
- [ ] `QuestBoard.IntegrationTests/Controllers/GroupPickerControllerIntegrationTests.cs` — new test: authenticated non-member user posts `SelectGroup` for a group they don't belong to, asserts 404
- [ ] New unit test for `GroupSessionMiddleware`'s D-06 re-validation block, constructing a fake `HttpContext` with a controlled session timestamp (per Pitfall 6, avoid relying on integration-test session-cookie round-tripping)

## Security Domain

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | no | No changes to credential handling — this phase is entirely post-authentication authorization |
| V3 Session Management | yes | `ActiveGroupId` is session-derived authorization state; D-06 directly hardens re-validation of session-derived privilege, matching the existing `SecurityStampValidatorOptions` precedent's intent (bound the staleness window of a privilege signal) |
| V4 Access Control | yes | This entire phase is a V4 fix: D-01/D-02 close a broken access-control decision point (middleware gate incorrectly exempting a role from a resource-scoping requirement); D-04/D-05 add a missing object-level authorization check (verify the target resource, not just that the caller is authenticated); D-03 hardens the data-layer authorization enforcement (fail-closed instead of fail-open) |
| V5 Input Validation | no | `groupId`/`userId` are already validated as existing entities before this phase; no new user input surface introduced |
| V6 Cryptography | no | No cryptographic material involved |

### Known Threat Patterns for ASP.NET Core MVC + EF Core multi-tenancy

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Fail-open authorization escape hatch (a role deliberately bypasses a gate meant for "everyone else") | Elevation of Privilege / Information Disclosure | Fail-closed by default; any intentional bypass must be scoped as narrowly as possible (D-02's exempt-path list, not a blanket role bypass) — this phase is the textbook fix for exactly this anti-pattern |
| Insecure Direct Object Reference (IDOR) via `groupId` POST parameter with no ownership check | Tampering / Elevation of Privilege | Explicit membership/ownership check before trusting a client-supplied identifier (`GetGroupRoleByIdAsync`, D-04) — the exact gap `SelectGroup` had |
| Defense-in-depth failure (one layer's bug is fully exploitable because no other layer independently enforces the same invariant) | Information Disclosure | Multiple independent enforcement layers for the same invariant (middleware gate AND fail-closed query filter) — D-01+D-03 together, consistent with this codebase's stated Phase 49 lesson that single-layer enforcement has "repeatedly proven fragile" |
| Stale authorization decision (privilege granted at t=0 not re-checked as time passes) | Elevation of Privilege | Time-bounded re-validation of the authorization decision (D-06), mirroring the existing `SecurityStampValidator` pattern for the analogous "account disabled mid-session" problem (Phase 41) |

## Sources

### Primary (HIGH confidence)
- Direct source inspection: `QuestBoard.Service/Middleware/GroupSessionMiddleware.cs` (full file, 97 lines) — confirmed exact current guard ordering, exempt-path list, comment rationale to be corrected
- Direct source inspection: `QuestBoard.Repository/Entities/QuestBoardContext.cs` (lines 230-327) — confirmed all `HasQueryFilter` definitions; discovered `ReminderLogEntity`/`UserTransactionEntity` share the fail-open shape, not named in CONTEXT.md
- Direct source inspection: `QuestBoard.Service/Controllers/GroupPickerController.cs` (full file, 65 lines) — confirmed exact current `SelectGroup`/`Index` code, confirmed `Index` already correctly scopes non-SuperAdmin callers
- Direct source inspection: `QuestBoard.Domain/Interfaces/IUserService.cs` — confirmed `GetGroupRoleByIdAsync(int userId, int groupId)` signature and null-for-non-member contract
- Direct source inspection: `QuestBoard.Domain/Interfaces/IActiveGroupContext.cs`, `QuestBoard.Service/Services/ActiveGroupContextService.cs`, `QuestBoard.Service/Constants/SessionKeys.cs` — confirmed `ActiveGroupId` is Session-backed, not claims-backed
- Direct source inspection: `QuestBoard.Service/Program.cs` (lines 25-230, 300-345) — confirmed middleware pipeline order, `SecurityStampValidatorOptions` config (Phase 41, unchanged by this phase), `AddSession`/`AddDistributedSqlServerCache` config (24h idle timeout, SQL-backed)
- Direct source inspection: all 12 controllers outside `/platform` area (`QuestController`, `CalendarController`, `HomeController`, `PlayersController`, `ShopController`, `ShopManagementController`, `QuestLogController`, `AccountController`, `AdminController`, `EmailPreviewController`, `GuildMembersController`, `DungeonMasterController`) — confirmed `[Authorize]`/area attributes for the group-scoped vs. exempt classification table
- Direct source inspection: `QuestBoard.IntegrationTests/Controllers/GroupSessionMiddlewareIntegrationTests.cs`, `GroupPickerControllerIntegrationTests.cs`, `QuestBoard.IntegrationTests/Helpers/AuthenticationHelper.cs` — confirmed existing test patterns, the SuperAdmin test that must be rewritten, and the session-cookie round-trip limitation
- Direct source inspection: `QuestBoard.UnitTests/Repository/PlayerSignupRepositoryTests.cs` — confirmed the `TestActiveGroupContext`/`MutableTestGroupContext` InMemory-provider pattern to extend for D-03 regression tests
- Direct grep across full repo for `IgnoreQueryFilters` — confirmed `QuestRepository.cs` (`GetQuestsForTomorrowAllGroupsAsync`) is the only production call site; all other matches are `.planning/` documentation
- `.planning/phases/49-fix-guild-members-page-missing-group-tenant-filtering/49-CONTEXT.md` and `49-RESEARCH.md` — direct precedent for the fail-closed filter shape, the 404 convention, and the `GetGroupRoleByIdAsync` reuse pattern
- `.planning/milestones/v6.1-phases/41-safe-user-removal-account-disable/41-RESEARCH.md` — confirmed `SecurityStampValidatorOptions.ValidationInterval` = 5 minutes, its exact rationale, and its Program.cs location
- [SecurityStampValidatorOptions.ValidationInterval Property (Microsoft.AspNetCore.Identity)](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.identity.securitystampvalidatoroptions.validationinterval) — confirmed the validator hooks `OnValidatePrincipal` on the auth cookie, not session state

### Secondary (MEDIUM confidence)
- WebSearch: "ASP.NET Core Identity custom claims re-validation cookie OnValidatePrincipal vs SecurityStampValidator ValidationInterval extend" — confirmed `ISecurityStampValidator`/`OnValidatePrincipal` mechanism operates on the `ClaimsPrincipal`/cookie, cross-checked against the direct source inspection of this codebase's Session-based `ActiveGroupId` storage (Primary source above) to reach the Pitfall 1 conclusion

### Tertiary (LOW confidence)
- None — all claims either verified directly against this codebase's source or cited to official Microsoft Learn documentation.

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — no new packages; every API already in production use in this exact codebase, confirmed via direct file reads
- Architecture: HIGH — every controller, middleware, and filter file involved was read directly; conventions confirmed by exact line citations; the 2 additional entities (Pitfall 4) were discovered via direct grep, not inference
- Pitfalls: HIGH — the existing-test-contradiction (Pitfall 2) and stale-CONCERNS.md (Pitfall 3) issues were confirmed by direct file reads, not speculation; the SecurityStampValidator/Session mismatch (Pitfall 1) was confirmed by cross-referencing this codebase's own `ActiveGroupContextService.cs` against official Microsoft Learn documentation of the validator's cookie-claims mechanism

**Research date:** 2026-07-06
**Valid until:** 30 days (stable framework APIs — ASP.NET Core Identity, EF Core global query filters, and MVC middleware ordering are all long-stable mechanisms; no fast-moving dependencies in this phase)
