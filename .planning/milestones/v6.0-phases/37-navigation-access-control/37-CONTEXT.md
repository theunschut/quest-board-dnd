# Phase 37: Navigation & Access Control - Context

**Gathered:** 2026-07-03
**Status:** Ready for planning

<domain>
## Phase Boundary

Campaign-type groups hide the nav items that don't apply to them — Calendar, Shop, "Manage Shop", "Edit My Profile", and "Players" — while Guild Members and Quest Log stay visible for every board type. Separately, the Email Stats page and its nav link become SuperAdmin-only everywhere (currently visible/accessible to the Admin role too), with a real Access Denied experience on a direct unauthorized URL hit instead of the app's current 404. This reuses the existing `_Layout.cshtml`/`_Layout.Mobile.cshtml` nav and `AdminController` — no new controller or Area.

</domain>

<decisions>
## Implementation Decisions

### Nav visibility gating
- **D-01:** The 5 campaign-gated items (Calendar, Shop, Manage Shop, Edit My Profile, Players) are implemented as a **"show only when confirmed One-Shot" allowlist**, not a "hide only when Campaign" blocklist. This single condition naturally covers every indeterminate case (anonymous visitor, authenticated user with no active group yet) without a separate null-handling branch — confirmed by how the user answered the SuperAdmin and no-group questions below.
- **D-02:** SuperAdmin's nav follows whatever group is currently active (`ActiveGroupId`) — no special-casing. If a SuperAdmin's active group is Campaign, they see the stripped-down nav like any DM/Player in that group; if no group is active, they fall into D-03's hidden state along with everyone else.
- **D-03:** For an authenticated user who hasn't picked a group yet (on GroupPicker, no `ActiveGroupId` set), the 5 allowlisted items are **hidden** (user's explicit choice, overriding Claude's "show" recommendation).
- **D-04 (related fix, approved by user — not originally in NAV-01..06 wording):** The Calendar nav link is now also wrapped in the existing `IsAuthenticated` check for **anonymous (logged-out) visitors**, closing a pre-existing gap where it rendered as a visible-but-dead link (`CalendarController` requires `[Authorize]`; clicking it while logged out just bounces to Login). Confirmed during discussion: everything else in the nav (Admin/DM dropdowns, Shop, Quest Log, Guild Members, Players) was already hidden pre-login — only Calendar and Login rendered unconditionally.
- **D-05:** Guild Members (NAV-03) and Quest Log are **not** part of the allowlist — they stay visible to all authenticated users regardless of board type or active-group state, unchanged from today.

### Email Stats access control
- **D-06:** `AdminController.EmailStats` gets its authorization tightened from the class-level `AdminOnly` policy to `SuperAdminOnly` (existing policy already defined in `Program.cs`, already used by `Platform/GroupController` and the Hangfire dashboard nav link) via a method-level `[Authorize]` override. The Email Stats nav link in the Admin dropdown is gated the same way.
- **D-07:** Add a real Access Denied page (new route/view) now. Because `AccessDeniedPath` is a single app-wide cookie-auth setting, this is inherently an app-wide fix — every `AdminOnly`/`DungeonMasterOnly`/`SuperAdminOnly` policy failure across the whole app gets a proper page instead of a 404, not just Email Stats. User explicitly approved this wider blast radius.

### Claude's Discretion
- **Mechanism for exposing the active group's `BoardType` to `_Layout.cshtml`:** not discussed with the user — left to research/planning. `_Layout.cshtml` renders on every page regardless of controller, so Phase 36's `ViewBag.BoardType` pattern (populated per-action in `QuestController`/`QuestLogController` only) doesn't reach it. Strong existing precedent to extend: `ActiveGroupName` is already written to Session at both `GroupPickerController` write sites and read directly via `HttpContextAccessor` in `_Layout.cshtml` (see `<code_context>`) — the same mechanism plausibly extends to a `BoardType` session value, but whether to do it that way vs. extending `IActiveGroupContext` with a DB-backed lookup is an implementation call for research/planning.
- **Exact wording/styling of the new Access Denied page:** follow CLAUDE.md's modern-card pattern (`modern-card`, `modern-card-header`, `modern-card-body`); exact copy left to planner/implementer.
- **Where the AccessDenied action lives** (existing `AccountController` vs. a new controller) — implementation detail, left to planner.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Requirements & roadmap
- `.planning/ROADMAP.md` — Phase 37 section (goal, success criteria, NAV-01..06, ACCESS-01)
- `.planning/REQUIREMENTS.md` — NAV-01 through NAV-06 and ACCESS-01 definitions
- `.planning/PROJECT.md` — Key Decisions table: `BoardType` dispatch via switch expressions; repeated SuperAdmin-bypass precedent for functional checks (`GetEffectiveGroupRoleAsync`/`RequireActiveGroupId()`); `SuperAdminOnly` policy uses `RequireRole("SuperAdmin")` with no custom handler

### Prior phase context
- `.planning/phases/35-board-type-configuration/35-CONTEXT.md` — `BoardType` enum foundation: immutable after group creation, safe to read anywhere
- `.planning/phases/36-campaign-quest-posting-closing/36-CONTEXT.md` — established the `ViewBag.BoardType` per-action threading pattern (does not reach `_Layout.cshtml`)

No external specs/ADRs beyond the project's own planning docs — requirements fully captured in decisions above.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `SuperAdminOnly` policy (`QuestBoard.Service/Program.cs:81-82`) — `.AddPolicy("SuperAdminOnly", policy => policy.RequireRole("SuperAdmin"))` — directly reusable for ACCESS-01, no new policy needed.
- `GroupPickerController` (`QuestBoard.Service/Controllers/GroupPickerController.cs:33-34, 48-49`) — writes `SessionKeys.ActiveGroupId`/`ActiveGroupName` to Session at both group-selection write sites (`Index` auto-select for single-group users, `SelectGroup` POST) — the pattern to mirror if a BoardType session value is the chosen mechanism.
- `_Layout.cshtml:148` — reads `ActiveGroupName` directly via `HttpContextAccessor.HttpContext?.Session?.GetString(SessionKeys.ActiveGroupName)`, with no controller/ViewBag involvement — the same read pattern would apply to a new BoardType session value.
- `IActiveGroupContext` / `ActiveGroupContextService` (`QuestBoard.Domain/Interfaces/IActiveGroupContext.cs`, `QuestBoard.Service/Services/ActiveGroupContextService.cs`) — currently exposes only `ActiveGroupId`, reads Session directly (with a Hangfire-job override path). Could be extended with a `BoardType?`-returning member if research prefers a context-based approach over duplicating the raw session-write pattern.

### Established Patterns
- `_Layout.cshtml` / `_Layout.Mobile.cshtml` (`QuestBoard.Service/Views/Shared/`) — nav items are conditionally rendered via `@if` blocks reading `AuthorizationService.AuthorizeAsync(User, "...")` or `User.IsInRole(...)`; no CSS-hide pattern exists anywhere in the nav. New BoardType gating should follow the same `@if` convention.
- Both desktop (`_Layout.cshtml:126-131`) and mobile (`_Layout.Mobile.cshtml:110-115`) currently render Calendar unconditionally, outside any `IsAuthenticated` check — both need the D-04 fix identically.
- `AdminController` (`QuestBoard.Service/Controllers/Admin/AdminController.cs:20`) — class-level `[Authorize(Policy = "AdminOnly")]`; the `EmailStats` action (line 358) has no method-level override today and needs `[Authorize(Policy = "SuperAdminOnly")]` added directly on the action.
- No `/Account/AccessDenied` action exists anywhere in the codebase today (verified via search across the whole Service project). `AddIdentity`'s default cookie `AccessDeniedPath` (`/Account/AccessDenied`) currently 404s on every policy-gated page's authorization failure, not just Email Stats — confirmed via `Program.cs`, which has no `UseStatusCodePages`/`UseExceptionHandler` catching this either.

### Integration Points
- `QuestBoard.Service/Views/Shared/_Layout.cshtml` and `_Layout.Mobile.cshtml` — where the new BoardType-based `@if` gating wraps Calendar, Shop, Manage Shop, Edit My Profile, Players; and where the Email Stats dropdown item's visibility check changes from `AdminOnly` to `SuperAdminOnly`.
- `QuestBoard.Service/Controllers/Admin/AdminController.cs` — `EmailStats` action gets a method-level `[Authorize(Policy = "SuperAdminOnly")]`.
- `QuestBoard.Service/Controllers/GroupPickerController.cs` — both group-selection write sites need to also stash BoardType (in Session or equivalent), if session-mirror is the chosen mechanism for reaching `_Layout.cshtml`.
- New: an `AccessDenied` action + view (likely on the existing `AccountController` given its `/Account/...` routes, or wherever research determines is the cleanest fit) plus whatever `Program.cs` wiring (if any) is needed to route to it.

</code_context>

<specifics>
## Specific Ideas

- No specific visual mockup was given for the Access Denied page — follow CLAUDE.md's modern-card pattern (`modern-card`, `modern-card-header`, `modern-card-body`).
- The "allowlist, not blocklist" framing (D-01) was a simplification that emerged from the discussion itself — the user's answers to the SuperAdmin question (follow active group) and the no-group question (hide) both point to the same single rule: show the 5 gated items only when the active group's board type resolves to One-Shot; hide in every other case (Campaign, unknown, or absent).

</specifics>

<deferred>
## Deferred Ideas

None — both adjacent items that came up (anonymous Calendar link, Access Denied page) were explicitly pulled into this phase's scope by the user rather than deferred. No scope creep occurred.

</deferred>

---

*Phase: 37-navigation-access-control*
*Context gathered: 2026-07-03*
