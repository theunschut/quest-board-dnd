---
phase: 55-fix-cross-tenant-quest-leak-on-quest-board-quests-from-anoth
reviewed: 2026-07-06T08:43:32Z
depth: standard
files_reviewed: 12
files_reviewed_list:
  - QuestBoard.IntegrationTests/Controllers/DungeonMasterControllerIntegrationTests.cs
  - QuestBoard.IntegrationTests/Controllers/GroupPickerControllerIntegrationTests.cs
  - QuestBoard.IntegrationTests/Controllers/GroupSessionMiddlewareIntegrationTests.cs
  - QuestBoard.IntegrationTests/Helpers/AuthenticationHelper.cs
  - QuestBoard.IntegrationTests/Tests/TenantIsolationTests.cs
  - QuestBoard.Repository/Entities/QuestBoardContext.cs
  - QuestBoard.Service/Constants/SessionKeys.cs
  - QuestBoard.Service/Controllers/GroupPickerController.cs
  - QuestBoard.Service/Middleware/GroupSessionMiddleware.cs
  - QuestBoard.UnitTests/Middleware/GroupSessionMiddlewareRevalidationTests.cs
  - QuestBoard.UnitTests/Repository/PlayerSignupRepositoryTests.cs
  - QuestBoard.UnitTests/Repository/QuestBoardContextFilterTests.cs
findings:
  critical: 0
  warning: 2
  info: 3
  total: 5
status: issues_found
---

# Phase 55: Code Review Report

**Reviewed:** 2026-07-06T08:43:32Z
**Depth:** standard
**Files Reviewed:** 12
**Status:** issues_found

## Summary

This phase closes a cross-tenant data leak via four coordinated changes: (1) flipping 7 EF Core
global query filters in `QuestBoardContext` from fail-open (`ActiveGroupId == null || ...`) to
fail-closed (`ActiveGroupId != null && ...`), (2) removing the blanket SuperAdmin bypass from
`GroupSessionMiddleware` so a null active group gates every role identically, (3) adding a
target-membership check (`IsTargetInActiveGroupAsync`) to close an IDOR in `GroupPickerController`'s
`SelectGroup` action, and (4) adding interval-gated (5 minute) re-validation of stale group
membership mid-session in `GroupSessionMiddleware`.

I traced all 7 flipped filters against the diff and confirmed each is a mechanical, correctly-scoped
`==` → `!=` / `||` → `&&` flip with no entity missed and no residual fail-open path
(`CharacterEntity`/`CharacterClassEntity`/`CharacterImageEntity` were already fail-closed pre-phase
and correctly left untouched). I confirmed `ActiveGroupContextService` reads `ActiveGroupId` straight
from `Session`, matching what the middleware and `GroupPickerController` mutate, so there is no
split-brain between what the middleware validates and what the query filters enforce. I confirmed
`GroupSessionMiddleware`'s exempt-path list (`/groups/pick`, `/GroupPicker`, `/Account`, `/platform`,
`/Error`) is scoped only to routes that are demonstrably tolerant of a null `ActiveGroupId`
(`AccountController.Profile`/`Edit` degrade to `role = null` rather than throwing) — `AdminController`,
which does require an active group, is correctly NOT on the exempt list. Middleware registration order
(`UseSession` → `UseAuthentication` → `GroupSessionMiddleware` → `UseAuthorization`) is correct for the
session-mutation-then-read pattern the revalidation block depends on.

Two non-blocking issues found in the revalidation logic and one repository-layer caveat that the new
code comments overstate; see Warnings below. A few minor test-hygiene items are listed as Info.

## Warnings

### WR-01: SuperAdmin's `ActiveGroupValidatedAtUtc` timestamp is never refreshed by the middleware, only ever grows staler

**File:** `QuestBoard.Service/Middleware/GroupSessionMiddleware.cs:115-142`
**Issue:** The re-stamp on line 141 (`context.Session.SetString(SessionKeys.ActiveGroupValidatedAtUtc, ...)`)
only executes inside the `if (needsRevalidation && !context.User.IsInRole("SuperAdmin"))` block. For a
SuperAdmin, `needsRevalidation` will be `true` on essentially every request after the first 5 minutes
(since the timestamp is set once at `SelectGroup`/`Index` time and never refreshed), but the `!IsInRole("SuperAdmin")`
guard always short-circuits the block before the timestamp can be re-stamped. The practical effect is that
every single request from a SuperAdmin re-executes the `DateTime.TryParse` + staleness comparison (never
skipping via the "fresh timestamp" fast path that a non-SuperAdmin gets), even though the branch body that
would act on `needsRevalidation` is immediately skipped again by the role check. This is not a security bug
(SuperAdmin correctly never receives the DB membership check, matching its "membership was never gated"
contract) but it means the intended fast-path optimization (skip parsing entirely once revalidated recently)
never actually applies to SuperAdmin sessions, contradicting the apparent intent of the interval-gating design.
**Fix:** Re-stamp the timestamp regardless of role once you've determined a check-or-skip decision, e.g.:
```csharp
if (needsRevalidation)
{
    if (!context.User.IsInRole("SuperAdmin"))
    {
        // ... membership check, possible 409/redirect-and-return ...
    }

    context.Session.SetString(SessionKeys.ActiveGroupValidatedAtUtc, DateTime.UtcNow.ToString("O"));
}
```
This lets SuperAdmin sessions also benefit from the fast "fresh timestamp" path on subsequent requests
instead of re-evaluating `needsRevalidation` (parse + comparison) on every single request indefinitely.

### WR-02: Code comment overstates `FindAsync`-based `GetByIdAsync` as "automatically group-scoped" — true only for untracked entities

**File:** `QuestBoard.Repository/Entities/QuestBoardContext.cs:281-284`
**Issue:** The comment claims: *"This also makes every PlayerSignupRepository method (including the base
GetByIdAsync/FindAsync path) automatically group-scoped, not just the ones that manually re-derive the
target through an already-filtered quest.PlayerSignups navigation first."* `BaseRepository.GetByIdAsync`
(`QuestBoard.Repository/BaseRepository.cs:44-48`) calls `DbSet.FindAsync([id], ...)`. EF Core's `FindAsync`
first checks the context's local identity map (already-tracked entities) before issuing a database query;
global query filters (`HasQueryFilter`) are applied only to the SQL translation path, not to the
in-memory tracked-entity lookup. If a `PlayerSignupEntity` belonging to a different group has already
been attached/tracked earlier in the same request/DbContext scope (e.g. loaded via a raw `Find`,
change-tracked after an unrelated update, or attached by some future code path), a subsequent
`GetByIdAsync` call for that same id can return the tracked entity without re-evaluating the group filter,
silently bypassing the "automatic" scoping the comment promises. No current call site in the reviewed
files demonstrates this bypass in practice (each HTTP request gets a fresh scoped `QuestBoardContext`,
and none of the reviewed repository methods currently pre-track a cross-group signup before calling
`GetByIdAsync`), so this is not an active exploit today — but the comment's blanket "automatically
group-scoped" claim is not accurate for all code paths and could mislead a future maintainer into relying
on `GetByIdAsync` alone as a sufficient tenant boundary in a scenario where the entity is already tracked.
**Fix:** Soften the comment to note the caveat, e.g. append: *"— note this guarantee holds for the
common case where the id has not already been loaded untracked in this DbContext scope; if a caller
ever pre-tracks a cross-group entity by id within the same scope before calling GetByIdAsync, FindAsync's
identity-map lookup can return it without re-running the filter."* Alternatively, harden
`PlayerSignupRepository`'s callers that matter most (e.g. `RemovePlayerSignup`'s lookup) to keep using an
explicit `.Include(ps => ps.Quest)` + `FirstOrDefaultAsync` (as `GetByIdWithQuestAsync` already does)
rather than relying on the base `GetByIdAsync`/`FindAsync` path for anything security-sensitive.

## Info

### IN-01: SuperAdmin test users are seeded with a spurious `GroupRole.Player` UserGroups membership row

**File:** `QuestBoard.IntegrationTests/Helpers/AuthenticationHelper.cs:119-151`
**Issue:** The membership-seeding block guards on `if (roles == null || roles.Length > 0)`. When a caller
passes `roles: ["SuperAdmin"]` (e.g. via `CreateAuthenticatedSuperAdminClientAsync`), this condition is
true (non-empty array), so the block runs, and since `"SuperAdmin"` matches neither `"Admin"` nor
`"DungeonMaster"` in the `else if` chain, it falls through to the `GroupRole.Player` default and adds a
`UserGroupEntity` row with `GroupRole = Player` for group 1. SuperAdmin's authorization never actually
depends on this membership row (SuperAdmin bypasses membership checks entirely, confirmed by
`GroupSessionMiddleware`'s explicit `!IsInRole("SuperAdmin")` exclusion), so this doesn't affect
correctness of any test in the reviewed set — but it is misleading test data: a future test that asserts
"SuperAdmin test users have zero group memberships" or that counts group-1 members would be surprised by
this row.
**Fix:** Add `"SuperAdmin"` alongside the existing role checks so the seeding block is skipped (or
seeds nothing) for SuperAdmin callers, e.g.:
```csharp
if ((roles == null || roles.Length > 0) && (roles == null || !roles.Contains("SuperAdmin")))
```

### IN-02: `int.Parse(NameIdentifier)` and `FindFirst(...)!.Value` assume the claim is always present and numeric, with no defensive check

**File:** `QuestBoard.Service/Middleware/GroupSessionMiddleware.cs:118`
**Issue:** `int.Parse(context.User.FindFirst(ClaimTypes.NameIdentifier)!.Value)` will throw
`NullReferenceException` if the claim is absent, or `FormatException` if it is non-numeric, for any
authenticated principal that reaches this line. This matches the existing convention used elsewhere in
the codebase (e.g. `GroupPickerController.SelectGroup`'s `int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!)`),
so it is not a new pattern introduced by this phase, but this line specifically executes on every
authenticated, non-exempt request once the timestamp goes stale — a wider blast radius than the
controller-level equivalents, which only fire on a single action. An unhandled exception here would
surface as an unstyled 500 rather than the intended 409/redirect UX this middleware exists to provide.
**Fix:** Not blocking for this phase since it mirrors an established codebase convention, but consider a
follow-up hardening pass replacing the bare `int.Parse(...!.Value)` calls app-wide with a
`TryParse`-based helper that Challenges/re-authenticates on failure instead of throwing.

### IN-03: `DateTime.UtcNow - validatedAt > MembershipRevalidationInterval` has no test coverage for the exact-boundary case

**File:** `QuestBoard.Service/Middleware/GroupSessionMiddleware.cs:113`
**Issue:** The comparison is a strict `>`, so a timestamp exactly 5 minutes old (to the tick) is treated
as still fresh and skips revalidation for one more request. This is a reasonable and intentional design
choice (not a bug), but `GroupSessionMiddlewareRevalidationTests.cs` only exercises "10 minutes stale" and
"1 minute fresh" cases — there is no test pinning down the boundary behavior at exactly (or just past)
the 5-minute mark, so a future refactor that accidentally flips `>` to `>=` or vice versa would not be
caught by the existing suite.
**Fix:** Optional: add a boundary test with `DateTime.UtcNow.AddMinutes(-5).AddSeconds(-1)` (should
revalidate) vs `DateTime.UtcNow.AddMinutes(-4).AddSeconds(-59)` (should not) to lock in the intended
semantics.

---

_Reviewed: 2026-07-06T08:43:32Z_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
