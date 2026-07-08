# Phase 56: Allow Admins to Edit Characters Owned by Other Players - Research

**Researched:** 2026-07-06
**Domain:** ASP.NET Core MVC authorization (GroupRole/SuperAdmin ownership-OR-admin pattern), EF Core tenant-scoped query filters
**Confidence:** HIGH

## User Constraints

No CONTEXT.md exists for this phase (added directly via `/gsd-phase`, not through `/gsd:discuss-phase`). There are no locked decisions, discretion areas, or deferred ideas to copy verbatim. The phase's only input is the user's own framing:

> "I need the ability as an admin (and thus superadmin) to be able to edit a character from another player."

This research treats that sentence as the scope boundary: **Admin (per-group) and SuperAdmin (global) should be able to edit any character; DungeonMaster and Player roles should not** gain any new access by this phase. This reading should be confirmed with the user during `/gsd:discuss-phase` before planning locks it in — flagged in Open Questions below.

## Phase Requirements

No REQ-IDs apply — this phase was added directly to the roadmap (see `.planning/STATE.md` "Roadmap Evolution": "Phase 56 added: Allow admins to edit characters owned by other players"), not derived from `.planning/REQUIREMENTS.md`. It is an ad-hoc feature-addition phase, the same pattern as Phases 48-52.

| Decision | Description | Research Support |
|----------|-------------|-------------------|
| (none — no REQ-ID or CONTEXT.md decision exists yet) | Admin/SuperAdmin can edit any character in their active group | Full pattern found and documented below: `DungeonMasterController.EditProfile`'s ownership-OR-admin shape, `GetEffectiveGroupRoleAsync` helper, `CharacterEntity`'s fail-closed tenant filter |

## Project Constraints (from CLAUDE.md)

- **No EF packages in `QuestBoard.Service`** — this phase touches only the controller/ViewModel/view layer; no new entity or migration is needed (see Summary).
- **Modern card UI pattern required** for any new/modified views (`modern-card`, `modern-card-header`, `modern-card-body`, FontAwesome icons with `me-2`, filled buttons, `<hr>` before button section) — the Details/Edit views already follow this pattern; any new admin-only UI element must match.
- **No requirement-ID/phase-number references in code comments** — any new inline comments explaining the ownership-OR-admin check must describe *why* in plain language (mirroring the existing style in `DungeonMasterController.cs` lines 152-157 and `QuestBoardContext.cs` lines 297-300), not reference "Phase 56."
- **Branching:** this work must land on a feature or milestone branch, never directly on `main` — repo is currently on `milestone/v7-backlog-cleanup`, which is an acceptable target.
- **EF Core migrations auto-apply on startup** — not relevant here since no schema change is needed (confirmed below).

## Summary

This phase is a **pure mechanical migration onto an already-proven pattern** — the exact same shape used by `DungeonMasterController.EditProfile` (ownership-OR-admin, target-user variant) already exists in this codebase, post-dating the Phase 34.3 authorization regression fix. `GuildMembersController`'s `Edit` (GET/POST), `Delete`, and `ToggleRetirement` actions currently hard-code `character.OwnerId != currentUser.Id` as the *only* check, with no Admin/SuperAdmin bypass at all — this is not a broken-but-partially-working check like the Phase 34.3 bugs, it is simply a check that was never given an admin escape hatch in the first place.

The fix is small and self-contained: inject `IActiveGroupContext` into `GuildMembersController` (not currently a constructor dependency), compute `GetEffectiveGroupRoleAsync(User, activeGroupContext.RequireActiveGroupId())`, and OR it into the existing ownership checks (`role == GroupRole.Admin`). No new authorization policy, no new middleware, no schema change, and — critically — **no separate cross-tenant guard needs to be hand-rolled**, because `CharacterEntity`'s existing EF Core global query filter (hardened in Phase 49, confirmed fail-closed even for SuperAdmin with no active group) already scopes every `DbContext.Characters` query to the caller's active group. An Admin in Group A querying a character ID that belongs to Group B gets `null` back from `GetCharacterWithDetailsAsync`, which the controller already turns into `NotFound()` — this happens automatically, with zero new code, exactly the same way it already protects `DungeonMasterController`'s target-user profile edits (which *do* need an explicit `IsTargetInActiveGroupAsync` check only because `UserEntity` has no query filter — `CharacterEntity` doesn't have that gap).

The one UI change beyond the controller is `Details.cshtml`/`Details.Mobile.cshtml`: the entire "Actions" card (Edit/Retire/Delete buttons) is currently gated behind `Model.IsOwner` only. The `CharacterViewModel.IsOwner` flag needs to become `IsOwner || isAdminOrSuperAdmin` (or the `Details` action needs to set a new `CanEdit`-style flag) so an Admin sees the Edit button on another player's character page. Whether the Delete/Retire buttons should also unlock for Admin, or only Edit (per the user's literal wording, which mentions only "edit"), is the primary open question for `/gsd:discuss-phase` to resolve — this research documents the codebase-consistency argument for extending all three uniformly (see Open Questions).

**Primary recommendation:** Mirror `DungeonMasterController.EditProfile`'s ownership-OR-admin pattern exactly — inject `IActiveGroupContext`, call `userService.GetEffectiveGroupRoleAsync(User, activeGroupContext.RequireActiveGroupId())`, OR the result (`role == GroupRole.Admin`) into `GuildMembersController.Edit` (GET/POST). Do not add a separate tenant-boundary check — `CharacterEntity`'s existing fail-closed query filter already provides it for free.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Ownership-OR-admin authorization check | API / Backend (`GuildMembersController.Edit`) | — | Same tier as every other ownership check in this codebase (`QuestController`, `DungeonMasterController`) — inline controller logic, not a policy attribute, because it combines a data-dependent ownership comparison with a role check |
| Effective role resolution (SuperAdmin + per-group Admin) | API / Backend (`IUserService.GetEffectiveGroupRoleAsync`) | — | Already-built shared helper; this phase is a consumer, not a builder |
| Tenant/group isolation on character reads | Database / Storage (EF Core `HasQueryFilter` on `CharacterEntity`) | — | Already fail-closed (Phase 49); this phase relies on it, does not modify it |
| "Show Edit button" UI gate | Browser / Client (Razor view conditional) | API / Backend (`CharacterViewModel.IsOwner`/new flag, set in controller) | View-level rendering decision fed by a controller-computed flag — same shape as `DMProfileViewModel.CanEdit` |

## Standard Stack

No new packages. This phase exclusively reuses APIs and helpers already in production use in this exact codebase.

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Microsoft.AspNetCore.Authorization | bundled with net10.0 shared framework [VERIFIED: QuestBoard.Service.csproj target framework] | `ClaimsPrincipal.IsInRole`, existing `IUserService.GetEffectiveGroupRoleAsync` helper | Already the app's sole authorization mechanism; no new policy needed |
| Microsoft.EntityFrameworkCore | 10.0.9 [VERIFIED: dotnet --version confirms net10.0.301 SDK; matches Phase 55 research's confirmed EF Core version] | `HasQueryFilter` on `CharacterEntity` (already fail-closed) | Already the app's sole ORM; zero changes needed to the filter itself |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Inline ownership-OR-admin check in the controller (matches every existing analog) | A new `[Authorize(Policy = "CharacterOwnerOrAdmin")]` custom policy/handler | Rejected — every existing ownership-OR-admin check in this codebase (`QuestController`, `DungeonMasterController`) is inline, not a policy, because the ownership half of the check is data-dependent (requires loading the specific character first) and policies can't easily express "compare to a loaded resource's OwnerId." Introducing a new pattern here would be inconsistent with the established convention and add ceremony for no benefit. |

**Installation:** None required — no new NuGet packages, no schema migration, no `dotnet ef migrations add`.

**Version verification:** `net10.0` SDK confirmed via `dotnet --version` (10.0.301) in this environment. `QuestBoard.Repository.csproj` targets EF Core 10.0.9 per Phase 55's research (same repo, same day). No package references need to change for this phase.

## Package Legitimacy Audit

Not applicable — this phase installs no external packages. All APIs used already ship in `Microsoft.AspNetCore.App` / `Microsoft.EntityFrameworkCore`, already referenced by the existing `.csproj` files.

**Packages removed due to slopcheck [SLOP] verdict:** none (no packages evaluated — none proposed)
**Packages flagged as suspicious [SUS]:** none

## Architecture Patterns

### System Architecture Diagram

```
GET/POST /GuildMembers/Edit/{id}
       │
       ▼
GuildMembersController.Edit
       │
       ├─► characterService.GetCharacterWithDetailsAsync(id)
       │        │
       │        ▼
       │   CharacterRepository → DbContext.Characters
       │        │  (EF Core global query filter applies HERE, automatically)
       │        │  activeGroupContext.ActiveGroupId != null
       │        │    && e.GroupId == activeGroupContext.ActiveGroupId
       │        │
       │        ├─ character belongs to caller's active group → returned
       │        └─ character belongs to a DIFFERENT group, or caller has
       │           no active group at all → null returned (query filter
       │           silently excludes it — no explicit code needed)
       │
       ├─► if null → NotFound()  (already existing code — now also covers
       │                          the cross-tenant case automatically)
       │
       ├─► currentUser = userService.GetUserAsync(User)
       ├─► role = userService.GetEffectiveGroupRoleAsync(
       │             User, activeGroupContext.RequireActiveGroupId())
       │             (SuperAdmin bypass baked into this ONE call —
       │              no separate IsInRole("SuperAdmin") check needed
       │              at the call site, unlike the older AdminHandler-era
       │              pattern)
       │
       ├─► if currentUser == null
       │        || (character.OwnerId != currentUser.Id
       │            && role != GroupRole.Admin)     ◄── THIS PHASE'S CHANGE
       │      → Forbid()
       │
       └─► else → proceed with existing Edit logic (unchanged)

Details page rendering (GET /GuildMembers/Details/{id}):
       │
       ├─► viewModel.IsOwner = currentUser != null && character.OwnerId == currentUser.Id
       ├─► NEW: viewModel.CanEdit (or reuse IsOwner, see Open Questions) =
       │        IsOwner || role == GroupRole.Admin
       │
       ▼
Details.cshtml / Details.Mobile.cshtml
       │
       └─► @if (Model.CanEdit) { <Actions card with Edit button (at minimum)> }
```

### Recommended Project Structure

No new files/folders. All changes are in-place edits to existing files:
```
QuestBoard.Service/
├── Controllers/Characters/
│   └── GuildMembersController.cs      # inject IActiveGroupContext; update Edit(GET), Edit(POST) ownership checks; update Details to compute admin-aware flag; decide Delete/ToggleRetirement scope (see Open Questions)
├── ViewModels/CharacterViewModels/
│   └── CharacterViewModel.cs          # IsOwner stays as-is (true ownership) OR add a new CanEdit/IsAdminEditor flag — planner's naming call, see Open Questions
├── Views/GuildMembers/
│   ├── Details.cshtml                 # gate Edit button (and optionally Retire/Delete) on the new admin-aware flag, not IsOwner alone
│   ├── Details.Mobile.cshtml          # identical change, mobile variant
│   ├── Edit.cshtml                    # no gating logic inside the form itself today — likely no change needed (see Common Pitfalls)
│   └── Edit.Mobile.cshtml             # same
QuestBoard.IntegrationTests/
└── Controllers/
    └── GuildMembersControllerIntegrationTests.cs   # extend with new Admin/SuperAdmin/cross-tenant test cases (currently has ZERO authorization tests — see Common Pitfalls)
```

### Pattern 1: Ownership-OR-Admin inline check (the pattern to mirror)

**What:** A controller action that must allow either the resource's owner OR a caller whose effective group role is Admin (with SuperAdmin transparently included via the same helper call).

**When to use:** Exactly `GuildMembersController.Edit` (GET and POST) — and, pending the Open Questions resolution, potentially `Delete` and `ToggleRetirement` too.

**Example — the exact analog, already in production, post-Phase-34.3-fix:**
```csharp
// Source: QuestBoard.Service/Controllers/DungeonMaster/DungeonMasterController.cs:60-69
var targetUser = id.HasValue ? await userService.GetByIdAsync(id.Value, token) : currentUser;
if (targetUser == null) return NotFound();

if (!await IsTargetInActiveGroupAsync(targetUser.Id)) return NotFound();

var role = await GetEffectiveRoleAsync();
if (currentUser.Id != targetUser.Id && role != GroupRole.Admin)
{
    return Forbid();
}

// Source: DungeonMasterController.cs:147-150 — the effective-role helper this phase should call directly
private async Task<GroupRole?> GetEffectiveRoleAsync() =>
    User.IsInRole("SuperAdmin")
        ? GroupRole.Admin
        : await userService.GetEffectiveGroupRoleAsync(User, activeGroupContext.RequireActiveGroupId());
```

**Important simplification for this phase:** `DungeonMasterController`'s local `GetEffectiveRoleAsync()` helper duplicates a SuperAdmin short-circuit that `IUserService.GetEffectiveGroupRoleAsync` *already performs internally* (confirmed by reading `UserService.cs:67-73` — `if (user.IsInRole("SuperAdmin")) return GroupRole.Admin;`). The local wrapper in `DungeonMasterController` predates that consolidation or is simply redundant belt-and-suspenders. **This phase does not need to duplicate that local wrapper** — call `userService.GetEffectiveGroupRoleAsync(User, activeGroupContext.RequireActiveGroupId())` directly, since it already returns `GroupRole.Admin` for any SuperAdmin.

**Target shape for `GuildMembersController.Edit` (GET), current code at lines 146-165:**
```csharp
// CURRENT (owner-only, no admin bypass):
var currentUser = await userService.GetUserAsync(User);
if (currentUser == null || character.OwnerId != currentUser.Id)
{
    return Forbid();
}

// TARGET:
var currentUser = await userService.GetUserAsync(User);
if (currentUser == null)
{
    return Forbid();
}

var role = await userService.GetEffectiveGroupRoleAsync(User, activeGroupContext.RequireActiveGroupId());
if (character.OwnerId != currentUser.Id && role != GroupRole.Admin)
{
    return Forbid();
}
```
Identical transformation applies to `Edit`(POST), lines 167-186. `RequireActiveGroupId()` is safe to call fail-hard here (not the fail-soft `AccountController` variant) because `GuildMembersController` is NOT on `GroupSessionMiddleware`'s exempt-path list (confirmed in Phase 55's research route enumeration table — `GuildMembersController` is explicitly listed as "Yes" group-scoped), so a non-exempt authenticated request reaching this action is guaranteed to have a non-null `ActiveGroupId` already.

**`viewModel.IsOwner = true;` at line 162/198 also needs attention** — currently hard-coded `true` unconditionally after the ownership-or-forbid check passes, which was correct when the check was owner-only, but is now misleading once Admin can also pass the guard while not being the owner. This should become `viewModel.IsOwner = character.OwnerId == currentUser.Id;` (reflecting actual ownership) with a separate flag (or reused `IsOwner` semantics, per the view's needs) driving whether the Edit view shows an "editing as admin" indicator — see Open Questions for whether the Edit view needs to visually distinguish this case at all.

### Pattern 2: `Details` action's `IsOwner`-gated Actions card needs to become admin-aware too

**Current code, `GuildMembersController.Details` (lines 50-63):**
```csharp
var currentUser = await userService.GetUserAsync(User);
var viewModel = mapper.Map<CharacterViewModel>(character);
viewModel.IsOwner = currentUser != null && character.OwnerId == currentUser.Id;
```

**Target shape (mirrors `DMProfileViewModel.CanEdit`'s existing analog, `DungeonMasterController.Profile` line 43):**
```csharp
// Source: QuestBoard.Service/Controllers/DungeonMaster/DungeonMasterController.cs:43 — CanEdit analog
var currentUser = await userService.GetUserAsync(User);
var viewModel = mapper.Map<CharacterViewModel>(character);
var isOwner = currentUser != null && character.OwnerId == currentUser.Id;
var role = currentUser != null
    ? await userService.GetEffectiveGroupRoleAsync(User, activeGroupContext.RequireActiveGroupId())
    : null;
viewModel.IsOwner = isOwner;
viewModel.CanEdit = isOwner || role == GroupRole.Admin;   // new flag name — planner's call, see Open Questions
```
Note the `currentUser != null` short-circuit guard BEFORE calling `RequireActiveGroupId()` — `Details` has no `[Authorize]` attribute today (confirmed: no attribute above the action or class-level beyond the class-level `[Authorize]` seen at line 12 — wait, `GuildMembersController` DOES have class-level `[Authorize]`, so `Details` is never anonymous-reachable in practice, but defensively keeping the `currentUser != null` guard costs nothing and matches the established defensive-coding style elsewhere in this codebase — see Common Pitfalls for the class-level `[Authorize]` confirmation).

### Anti-Patterns to Avoid
- **Re-deriving a manual cross-tenant/group check for characters** (e.g. hand-rolling `character.GroupId == activeGroupContext.ActiveGroupId`): unnecessary and risks drifting out of sync with the canonical filter. `CharacterEntity`'s `HasQueryFilter` already makes this structurally impossible to get wrong — a cross-group character ID simply doesn't exist from the querying admin's perspective. Adding a redundant manual check (unlike `DungeonMasterController`'s `IsTargetInActiveGroupAsync`, which exists ONLY because `UserEntity` has no filter) would be dead code that could rot out of sync with the real mechanism.
- **Duplicating the SuperAdmin `IsInRole` short-circuit inline**: `GetEffectiveGroupRoleAsync` already performs it internally (confirmed in `UserService.cs`). Adding `User.IsInRole("SuperAdmin") ? GroupRole.Admin : await ...GetEffectiveGroupRoleAsync(...)` around the call, as `DungeonMasterController`'s local wrapper does, is redundant for a *new* call site — harmless if copied, but not necessary, and this phase should prefer the simpler direct call unless matching that controller's exact local style is deemed more important than avoiding redundancy (a minor stylistic judgment call for the planner).
- **Introducing a brand-new `[Authorize(Policy = "...")]` for this**: existing ownership-OR-admin checks in this codebase are uniformly inline controller code, never a custom policy, because the ownership half requires a loaded resource. Don't introduce a new authorization architecture pattern for one controller.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| "Is this caller Admin-or-SuperAdmin for their active group" check | A new `IsInRole("Admin") \|\| IsInRole("SuperAdmin")` inline check, or a fresh helper method | `IUserService.GetEffectiveGroupRoleAsync(User, groupId)` (already exists, already handles the SuperAdmin bypass internally) | Exact purpose-built primitive from Phase 34.3; re-deriving it would risk missing the SuperAdmin-as-Admin-equivalent semantics or the per-group nuance (a user can be Admin in one group and Player in another) |
| "Does this character belong to the caller's active group" cross-tenant check | A manual `character.GroupId == activeGroupContext.ActiveGroupId` guard in the controller | Nothing — `CharacterEntity`'s existing `HasQueryFilter` (Phase 49, confirmed fail-closed) already guarantees this; a cross-group id simply returns `null` from `GetCharacterWithDetailsAsync` | Already enforced at the data-access layer; a redundant controller-level check adds no safety and is a second place to keep in sync if the filter's semantics ever change |

**Key insight:** Every primitive this phase needs already exists in this codebase, built and hardened by two prior phases (34.3's `GetEffectiveGroupRoleAsync`, 49's fail-closed `CharacterEntity` filter). This is a "wire an existing owner-only check to accept one more caller type" phase, not new architecture.

## Common Pitfalls

### Pitfall 1: `Delete` and `ToggleRetirement` share the identical owner-only check but were not named in the user's request
**What goes wrong:** A plan might update only `Edit` (matching the user's literal wording — "edit a character") and leave `Delete`/`ToggleRetirement` owner-only, creating an inconsistent experience where an Admin can edit a character's stats but can't retire or delete it — or the reverse risk: a plan silently extends all three without the user having explicitly asked for delete/retire admin access, which is a bigger privilege grant than requested.
**Why it happens:** `GuildMembersController.Edit`, `Delete`, and `ToggleRetirement` all use byte-for-byte the same `currentUser == null || character.OwnerId != currentUser.Id` guard shape (confirmed at lines 155-159, 183-186, 263-267, 284-288) — a mechanical "fix the pattern everywhere it appears" instinct would touch all three, but the user's stated need is narrower ("edit a character").
**How to avoid:** Surface this explicitly during `/gsd:discuss-phase` — do not silently expand scope to Delete/Retire without user confirmation, but also flag that leaving them inconsistent (Admin can edit but a broken character can never be un-retired or removed by anyone but its owner) may itself be a rough edge worth asking about.
**Warning signs:** Plan only touches `Edit` GET/POST — verify this was a deliberate scope decision recorded in CONTEXT.md, not an oversight.

### Pitfall 2: `viewModel.IsOwner = true;` hard-coded after the guard in `Edit`(GET)/(POST) becomes semantically wrong once Admin can pass the same guard
**What goes wrong:** Lines 162 and 198 in `GuildMembersController.cs` set `viewModel.IsOwner = true;` unconditionally once the ownership-or-forbid check passes. Once Admin is allowed through without being the owner, this line would incorrectly report `IsOwner = true` for an Admin editing someone else's character, potentially causing any view logic (or a future view change) keyed on `IsOwner` to misbehave.
**Why it happens:** The line was written when the guard's only passing condition WAS ownership — true was a safe shortcut. Adding the admin bypass invalidates that shortcut's correctness silently (it doesn't throw or fail a test today because there IS no test checking `IsOwner`'s value for the admin-editing-non-owned-character case).
**How to avoid:** Change to `viewModel.IsOwner = character.OwnerId == currentUser.Id;` (the same computation already used correctly in `Details`, line 60) rather than hard-coding `true`.
**Warning signs:** Grep for `IsOwner = true` after the change — it should not appear unconditionally anywhere in this controller once the admin bypass exists.

### Pitfall 3: `RequireActiveGroupId()` throws if called on a genuinely group-agnostic path — but this controller is not one of those, so fail-hard is correct, not a pitfall to avoid
**What goes wrong (if avoided incorrectly):** A planner unfamiliar with the D-03/D-04 distinction from Phase 34.3 might defensively use the fail-soft `if (activeGroupContext.ActiveGroupId is { } groupId)` pattern (`AccountController`'s style) "to be safe," when the fail-hard `RequireActiveGroupId()` (`QuestController`/`DungeonMasterController`'s style) is actually correct and expected here.
**Why it happens:** Without checking Phase 55's route-enumeration research, it's not obvious which controllers are exempt from `GroupSessionMiddleware`'s group-required gate (only `AccountController`, `GroupPickerController`, and the `/platform` area are exempt).
**How to avoid:** `GuildMembersController` is explicitly confirmed group-scoped (non-exempt) in Phase 55's research controller table — `RequireActiveGroupId()` (fail-hard, throws `InvalidOperationException` if null) is the correct choice here, matching `QuestController`/`DungeonMasterController`, not the fail-soft `AccountController` variant.
**Warning signs:** Using `activeGroupContext.ActiveGroupId is { } groupId` instead of `activeGroupContext.RequireActiveGroupId()` in this controller would be an unnecessary deviation from the established convention, though not itself a bug (both handle the null case, just with different failure modes — throw vs. silently degrade, and this controller is never reached with a null ActiveGroupId in production due to the middleware gate).

### Pitfall 4: Zero existing authorization tests for `GuildMembersController` — this phase must establish the pattern from scratch, not extend one
**What goes wrong:** `GuildMembersControllerIntegrationTests.cs` (4 existing tests, all for `Index`) has no `Edit`/`Delete`/authorization-focused tests at all today. A plan that says "extend the existing authorization tests" is working from a false premise — there is nothing to extend for this specific concern; new test methods must be written following the `AdminHandlerIntegrationTests.cs`-style pattern (allow/deny/SuperAdmin-bypass/cross-tenant cases) referenced in Phase 34.3's pattern map, not extending an existing `Edit`-authorization suite.
**Why it happens:** `GuildMembersController` predates the Phase 34.3 authorization-hardening sweep and was never in scope for it (it was already using the (soon-to-be-buggy-elsewhere, but here simply narrow) owner-only pattern, which wasn't broken in the "false negative on legitimate admin access to a DIFFERENT reason" sense the 34.3 sweep targeted — it just never had an admin bypass at all).
**How to avoid:** Plan for net-new test methods in `GuildMembersControllerIntegrationTests.cs`: Admin-editing-another's-character succeeds (200/302 not 403), Player-editing-another's-character still fails (403/Forbid, regression guard), SuperAdmin-with-no-active-group case (see Pitfall 5), and a cross-tenant case (Admin in Group A cannot reach a Group B character's edit page — expect 404, not 403, per this codebase's established "hide existence" convention seen in Phase 55's D-05).
**Warning signs:** A plan step titled "extend GuildMembersControllerIntegrationTests.cs authorization tests" implying such tests already exist for Edit — they do not.

### Pitfall 5: SuperAdmin's `RequireActiveGroupId()` in this controller depends on the SuperAdmin actually having selected a group first — this is now a mandatory precondition, not an edge case, per Phase 55's D-01
**What goes wrong:** A test or manual QA pass might try to verify SuperAdmin-can-edit-any-character by hitting `/GuildMembers/Edit/{id}` with a SuperAdmin account that has never selected an active group, expecting it to just work because "SuperAdmin sees everything." Per Phase 55's D-01 (extending `GroupSessionMiddleware`'s group-required gate to SuperAdmin on all group-scoped routes, including `GuildMembers`), a SuperAdmin with no `ActiveGroupId` hitting this controller gets redirected to `/groups/pick` by the middleware *before the controller action even runs* — this is expected, correct behavior post-Phase-55, not a bug in this phase's work.
**Why it happens:** Confusion between "SuperAdmin as a global role" and "SuperAdmin still needs an active group selected to operate on group-scoped resources," which is exactly what Phase 55 establishes/hardens. Phase 55 and Phase 56 are listed independently in the roadmap with no stated dependency ordering, so if Phase 56 executes before Phase 55, the OLD (buggy) middleware behavior is in effect — SuperAdmin bypasses the group gate entirely — meaning a SuperAdmin with no active group would reach this phase's new controller code, call `RequireActiveGroupId()`, and get an `InvalidOperationException` (500 error), not a redirect.
**How to avoid:** Confirm Phase 55's execution status before writing SuperAdmin test cases for this phase. If Phase 55 has not yet shipped, this phase's SuperAdmin integration tests must explicitly select a group first (e.g. via the test's `MutableGroupContext`/`TestGroupContext` override, matching Phase 55's own test-harness pattern) rather than relying on the middleware to force it — and should note in a code comment that this test assumption will strengthen once Phase 55 ships. If Phase 55 has already shipped by the time this phase executes, this is a non-issue — the middleware guarantees a non-null `ActiveGroupId` for any SuperAdmin reaching this controller.
**Warning signs:** An `InvalidOperationException` ("Active group context is not initialized...") in a SuperAdmin-focused integration test — this means either Phase 55 hasn't shipped yet and the test needs to seed a group explicitly, or (if Phase 55 has shipped) something is wrong with the middleware gate.

## Code Examples

### Target `GuildMembersController` constructor (adds `IActiveGroupContext`)

```csharp
// Source: this research, mirroring QuestController.cs:14-23 and DungeonMasterController.cs:12-17's
// existing constructor-injection convention for IActiveGroupContext
[Authorize]
public class GuildMembersController(
    ICharacterService characterService,
    IUserService userService,
    IActiveGroupContext activeGroupContext,
    IMapper mapper) : Controller
```
Note: `IActiveGroupContext activeGroupContext` is **already** a constructor parameter in the current `GuildMembersController.cs` (confirmed, line 16) — used today only in `Create`(POST) to tag new characters with `character.GroupId = activeGroupContext.RequireActiveGroupId();` (line 139). No new DI wiring is needed; the dependency already exists in this file and just needs to be read from in two more places (`Edit`, `Details`).

### Target `Edit`(GET) — full updated action

```csharp
// Source: this research, applying the DungeonMasterController.EditProfile pattern to
// GuildMembersController's existing structure (current code at lines 146-165)
[HttpGet]
public async Task<IActionResult> Edit(int id, CancellationToken token = default)
{
    var character = await characterService.GetCharacterWithDetailsAsync(id, token);
    if (character == null)
    {
        return NotFound();
    }

    var currentUser = await userService.GetUserAsync(User);
    if (currentUser == null)
    {
        return Forbid();
    }

    // An Admin (per-group role, or the global SuperAdmin role — both resolved by
    // GetEffectiveGroupRoleAsync) may edit any character in their active group, not just
    // their own, so a DM can be helped with sheet/class corrections without needing the
    // player's own login.
    var role = await userService.GetEffectiveGroupRoleAsync(User, activeGroupContext.RequireActiveGroupId());
    if (character.OwnerId != currentUser.Id && role != GroupRole.Admin)
    {
        return Forbid();
    }

    var viewModel = mapper.Map<CharacterViewModel>(character);
    viewModel.IsOwner = character.OwnerId == currentUser.Id;

    return View(viewModel);
}
```

### Target `Edit`(POST) — identical guard shape, same file, lines 167-186

```csharp
// Source: this research — same transformation applied to the POST twin
var currentUser = await userService.GetUserAsync(User);
if (currentUser == null)
{
    return Forbid();
}

var role = await userService.GetEffectiveGroupRoleAsync(User, activeGroupContext.RequireActiveGroupId());
if (existingCharacter.OwnerId != currentUser.Id && role != GroupRole.Admin)
{
    return Forbid();
}
```
The rest of the POST action body (class-level validation, profile picture handling, `SetAsMainCharacterAsync`/`UpdateAsync` dispatch) is unaffected and should not change.

### Existing integration test pattern to mirror for new Edit-authorization tests

```csharp
// Source: QuestBoard.IntegrationTests/Helpers/AuthenticationHelper.cs (confirmed present) +
// QuestBoard.IntegrationTests/Helpers/TestDataHelper.cs:123-149 (CreateTestCharacterAsync)
[Fact]
public async Task Edit_AdminEditingAnotherPlayersCharacter_ShouldSucceed()
{
    await TestDataHelper.ClearDatabaseAsync(factory.Services);

    var (adminClient, adminUser) = await AuthenticationHelper.CreateAuthenticatedAdminClientAsync(factory);
    var owner = await AuthenticationHelper.CreateTestUserAsync(
        factory.Services, "owner1", "owner1@example.com", "Test123!", "Character Owner");
    var character = await TestDataHelper.CreateTestCharacterAsync(factory.Services, owner.Id, "Owned Character");

    var response = await adminClient.GetAsync($"/GuildMembers/Edit/{character.Id}", TestContext.Current.CancellationToken);

    response.StatusCode.Should().Be(HttpStatusCode.OK);
}

[Fact]
public async Task Edit_PlayerEditingAnotherPlayersCharacter_ShouldBeForbidden()
{
    await TestDataHelper.ClearDatabaseAsync(factory.Services);

    var (playerClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(factory);
    var owner = await AuthenticationHelper.CreateTestUserAsync(
        factory.Services, "owner2", "owner2@example.com", "Test123!", "Character Owner Two");
    var character = await TestDataHelper.CreateTestCharacterAsync(factory.Services, owner.Id, "Someone Else's Character");

    var response = await playerClient.GetAsync($"/GuildMembers/Edit/{character.Id}", TestContext.Current.CancellationToken);

    response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
}

[Fact]
public async Task Edit_AdminEditingCharacterInDifferentGroup_ShouldReturnNotFound()
{
    await TestDataHelper.ClearDatabaseAsync(factory.Services);

    var (adminClient, _) = await AuthenticationHelper.CreateAuthenticatedAdminClientAsync(factory); // seeded to GroupId=1
    var owner = await AuthenticationHelper.CreateTestUserAsync(
        factory.Services, "owner3", "owner3@example.com", "Test123!", "Other Group Owner");
    var character = await TestDataHelper.CreateTestCharacterAsync(
        factory.Services, owner.Id, "Other Group's Character", groupId: 2); // different group

    var response = await adminClient.GetAsync($"/GuildMembers/Edit/{character.Id}", TestContext.Current.CancellationToken);

    response.StatusCode.Should().Be(HttpStatusCode.NotFound);
}
```

## State of the Art

No frameworks or approaches have changed. This is the same ownership-OR-admin authorization shape used throughout this codebase since Phase 34.3 (2026-07-02), applied to one controller that was never brought into that sweep because it was out of scope at the time (it wasn't broken relative to its own, narrower, owner-only design goal).

**Deprecated/outdated:** None. `GuildMembersController`'s current owner-only check isn't deprecated — it's simply narrower than what this phase's requirement now asks for.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | The user's phrase "as an admin (and thus superadmin)" means: grant this to `GroupRole.Admin` and the global `SuperAdmin` role, but NOT to `GroupRole.DungeonMaster` | Summary, User Constraints | Medium — if the user actually intends DMs to also get this access (DMs commonly help players with character sheets in this app's domain), the scope is too narrow. This should be explicitly confirmed in `/gsd:discuss-phase` before planning. |
| A2 | `Delete` and `ToggleRetirement` should NOT be extended to Admin in this phase (only `Edit`, matching the user's literal wording) | Common Pitfalls (Pitfall 1) | Medium — if left inconsistent, an Admin could edit a character's stats but not retire/delete it, which may itself be perceived as an unfinished feature. Needs explicit user confirmation either way. |
| A3 | The `Details` page's Edit button (and only the Edit button, not necessarily Retire/Delete) should become visible to Admin viewing another player's character | Pattern 2, Common Pitfalls | Low-Medium — directly follows from A2; if A2's scope changes, this follows automatically. |
| A4 | No new authorization policy or `[Authorize(Policy=...)]` attribute is warranted — the existing inline-check convention should be followed | Architecture Patterns (Anti-Patterns) | Low — this is a strong pattern match across three other controllers in this exact codebase; very unlikely to be wrong, but flagged since it's a design choice, not a hard technical constraint. |

## Open Questions

1. **Should `Delete` and `ToggleRetirement` also gain the Admin bypass, or only `Edit`?**
   - What we know: The user's literal request says "edit a character." All three actions (`Edit`, `Delete`, `ToggleRetirement`) currently share byte-identical owner-only guard code.
   - What's unclear: Whether extending only `Edit` leaves a confusing half-finished admin experience (can fix a typo in a name, but can't retire a dead/inactive character on a player's behalf), or whether the user specifically wants Delete/Retire to remain owner-only as a deliberate safety boundary (destructive actions are more sensitive than edits).
   - Recommendation: Raise explicitly in `/gsd:discuss-phase` as a locked decision (D-01-style) before planning. Default-safe planning assumption if not raised: extend only `Edit` per literal wording, leave `Delete`/`ToggleRetirement` untouched, and note this as a documented scope boundary (not a gap) in PROJECT.md.

2. **Should DungeonMaster (not just Admin/SuperAdmin) also get this capability?**
   - What we know: This app's domain has DMs actively running games and coaching players on character builds; the existing `DungeonMasterOnly` policy already covers `DungeonMaster || Admin`. The user specifically said "admin (and thus superadmin)," not mentioning DM.
   - What's unclear: Whether the omission of DM was deliberate (admin-only oversight/correction capability) or simply because the user, in describing their own need, is themselves an Admin/SuperAdmin and didn't think to mention the DM case.
   - Recommendation: Ask directly in `/gsd:discuss-phase`. If DM should be included, the guard becomes `role != GroupRole.Admin && role != GroupRole.DungeonMaster` instead — a one-line change, but the test matrix (Pitfall 4) needs a DM-specific case either way (DM should currently be denied, confirming today's narrower scope, if DM is excluded).

3. **Naming for the new admin-aware view flag: reuse `IsOwner`'s existing consumers differently, or add a distinct `CanEdit`/`IsAdminEditor` property?**
   - What we know: `DMProfileViewModel` already has a precedent named `CanEdit` for the identical ownership-OR-admin view-gating concept (`DungeonMasterController.cs` line 43).
   - What's unclear: Whether `CharacterViewModel.IsOwner`'s existing name should be preserved with strictly-true-ownership semantics (Pitfall 2) and a new field added, or whether `IsOwner` itself should be repurposed to mean "can act on this character" (blurring "owner" and "authorized editor").
   - Recommendation: Add a new `CanEdit` boolean to `CharacterViewModel` (matching the `DMProfileViewModel.CanEdit` naming precedent) and keep `IsOwner` strictly meaning true ownership — avoids semantic drift and keeps both concepts independently queryable in the view (e.g., an "editing as admin" banner could check `CanEdit && !IsOwner`).

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET SDK | Build/test | Yes | 10.0.301 (confirmed via `dotnet --version`) | — |
| SQL Server | Integration tests (WebApplicationFactory-backed) | Not directly probed in this research pass (Windows host service per CLAUDE.md) | — | Existing integration test suite already depends on it; no new dependency introduced by this phase |

No new external dependencies are introduced by this phase — no new packages, no new services, no new infrastructure.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit v3 (3.2.2), `Microsoft.AspNetCore.Mvc.Testing` 10.0.9 [VERIFIED: QuestBoard.IntegrationTests.csproj] |
| Config file | `QuestBoard.IntegrationTests/xunit.runner.json` |
| Quick run command | `dotnet test QuestBoard.IntegrationTests --filter FullyQualifiedName~GuildMembersControllerIntegrationTests` |
| Full suite command | `dotnet test` |

### Phase Requirements → Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| (none — no REQ-ID) | Admin can edit another player's character (same group) | integration | `dotnet test --filter Edit_AdminEditingAnotherPlayersCharacter_ShouldSucceed` | ❌ Wave 0 — net-new test |
| (none — no REQ-ID) | SuperAdmin can edit another player's character (with active group selected) | integration | `dotnet test --filter Edit_SuperAdminEditingAnotherPlayersCharacter_ShouldSucceed` | ❌ Wave 0 — net-new test |
| (none — no REQ-ID) | Player still cannot edit another player's character (regression guard) | integration | `dotnet test --filter Edit_PlayerEditingAnotherPlayersCharacter_ShouldBeForbidden` | ❌ Wave 0 — net-new test |
| (none — no REQ-ID) | Admin in Group A cannot reach (edit) a character in Group B — 404 not 403 | integration | `dotnet test --filter Edit_AdminEditingCharacterInDifferentGroup_ShouldReturnNotFound` | ❌ Wave 0 — net-new test |
| (none — no REQ-ID) | Owner can still edit their own character (regression guard) | integration | existing coverage implicit via manual QA; no automated test currently asserts this either — recommend adding | ❌ Wave 0 — net-new test, but lower priority (pre-existing, unchanged behavior) |
| (none — no REQ-ID) | Details page shows Edit button to Admin viewing another's character | integration (content assertion) | `dotnet test --filter Details_AdminViewingAnotherPlayersCharacter_ShowsEditButton` | ❌ Wave 0 — net-new test |

### Sampling Rate
- **Per task commit:** `dotnet test QuestBoard.IntegrationTests --filter FullyQualifiedName~GuildMembersControllerIntegrationTests`
- **Per wave merge:** `dotnet test`
- **Phase gate:** Full suite green before `/gsd:verify-work`

### Wave 0 Gaps
- [ ] `QuestBoard.IntegrationTests/Controllers/GuildMembersControllerIntegrationTests.cs` — needs six new test methods (Admin success, SuperAdmin success, Player-denied regression, cross-tenant 404, owner-still-works regression, Details-page-content assertion) — zero authorization tests exist for this controller today (Pitfall 4)
- [ ] No new fixtures or framework install needed — `AuthenticationHelper`, `TestDataHelper`, `WebApplicationFactoryBase` all already support every scenario this phase needs

## Security Domain

No `security_enforcement` key is set in `.planning/config.json` (absent = enabled per protocol).

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | No | Unchanged — this phase adds no new authentication surface |
| V3 Session Management | No | Unchanged — reuses existing `ActiveGroupId` session mechanism, no new session state |
| V4 Access Control | Yes | Inline ownership-OR-`GetEffectiveGroupRoleAsync`-Admin check, mirroring `DungeonMasterController.EditProfile` — the established, already-audited pattern in this codebase (post-Phase-34.3) |
| V5 Input Validation | No | Unchanged — no new user input fields introduced; existing `CharacterViewModel` validation attributes untouched |
| V6 Cryptography | No | Not applicable to this phase |

### Known Threat Patterns for this stack

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Insecure Direct Object Reference (IDOR) — an Admin from Group A guessing a Group B character's numeric ID | Elevation of Privilege | Already mitigated structurally by `CharacterEntity`'s fail-closed EF Core `HasQueryFilter` (Phase 49) — a cross-group ID resolves to `null`/404 regardless of the caller's role, with zero new code needed in this phase |
| Broken access control via inconsistent authorization checks across sibling actions (Edit hardened, Delete/Retire left owner-only, creating confusion about the actual security boundary) | Elevation of Privilege / Tampering (inverse risk: under-hardening leaves inconsistent UX, not a vulnerability per se) | Explicit scope decision required in `/gsd:discuss-phase` (Open Question 1) — document the boundary deliberately rather than let it emerge as an accidental inconsistency |
| Privilege check computed but not enforced (e.g., a plan that sets a `CanEdit` view flag but forgets the matching controller-side guard, allowing a direct POST to bypass a hidden button) | Elevation of Privilege | The controller-side `Edit`(POST) guard is the actual security boundary; the `Details` page's `CanEdit` flag is UI-only convenience and must never be treated as the enforcement point — both must be updated together, but the POST guard is non-negotiable even if the UI change were somehow skipped |

## Sources

### Primary (HIGH confidence)
- Direct codebase inspection (Read/Grep tool calls) — `GuildMembersController.cs`, `DungeonMasterController.cs`, `QuestController.cs`, `AdminHandler.cs`, `DungeonMasterHandler.cs`, `UserService.cs`, `IUserService.cs`, `CharacterEntity.cs`, `CharacterRepository.cs`, `CharacterService.cs`, `QuestBoardContext.cs`, `Role.cs`, `GroupRole.cs`, `Program.cs`, `CharacterViewModel.cs`, `Details.cshtml`, `Details.Mobile.cshtml`, `Edit.cshtml`, `Edit.Mobile.cshtml`, `GuildMembersControllerIntegrationTests.cs`, `AuthenticationHelper.cs`, `TestDataHelper.cs` — all read in full or targeted ranges this session, 2026-07-06.
- `.planning/milestones/v5.0-phases/34.3-group-role-authorization-regression-fix-inline-ownership-che/34.3-PATTERNS.md` — the exact canonical pattern this phase mirrors, read in full.
- `.planning/phases/55-fix-cross-tenant-quest-leak-on-quest-board-quests-from-anoth/55-RESEARCH.md` — controller route-enumeration table (confirms `GuildMembersController` is group-scoped/non-exempt) and confirms EF Core 10.0.9 version, read (partial, first 552 of 651 lines — sufficient for all claims cited).
- `dotnet --version` — confirmed 10.0.301 SDK in this environment.

### Secondary (MEDIUM confidence)
None — no external/web sources were needed for this phase; it is entirely an internal-codebase-pattern-mirroring exercise.

### Tertiary (LOW confidence)
None.

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — zero new packages, all APIs directly confirmed present and in use in this exact codebase
- Architecture: HIGH — the target pattern (`DungeonMasterController.EditProfile`) was read in full and is a proven, already-shipped, already-battle-tested analog in the same codebase
- Pitfalls: HIGH — every pitfall traces to a specific, directly-read line range in this codebase (not speculative), except the scope-boundary questions (Open Questions 1-2), which are correctly flagged as needing user input rather than asserted as fact

**Research date:** 2026-07-06
**Valid until:** 30 days (stable internal pattern, low churn risk) — but re-verify Pitfall 5's Phase 55 dependency status at planning time, since Phase 55's execution order relative to Phase 56 directly affects SuperAdmin test setup.
