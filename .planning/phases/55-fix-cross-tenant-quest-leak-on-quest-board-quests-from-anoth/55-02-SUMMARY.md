---
phase: 55-fix-cross-tenant-quest-leak-on-quest-board-quests-from-anoth
plan: 02
subsystem: auth
tags: [middleware, authorization, multi-tenancy, aspnetcore]

# Dependency graph
requires: []
provides:
  - "GroupSessionMiddleware no longer blanket-bypasses SuperAdmin — the exempt-path check runs before any role check, and the null-ActiveGroupId gate applies to every authenticated role including SuperAdmin"
  - "Regression tests proving SuperAdmin is redirected to /groups/pick on group-scoped routes with no active group, while still passing through exempt paths"
  - "Corrected CONCERNS.md rationale — the old 'SuperAdmin should see all groups' documentation is marked superseded"
affects: [55-01 (fail-closed filter, complementary fix), any future SuperAdmin-facing controller/middleware work]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Exempt-path check must run before any role-based bypass in gate middleware — role bypasses that skip a security-relevant gate are a fail-open pattern to avoid"

key-files:
  created: []
  modified:
    - QuestBoard.Service/Middleware/GroupSessionMiddleware.cs
    - QuestBoard.IntegrationTests/Controllers/GroupSessionMiddlewareIntegrationTests.cs
    - QuestBoard.IntegrationTests/Controllers/DungeonMasterControllerIntegrationTests.cs
    - .planning/codebase/CONCERNS.md

key-decisions:
  - "Guard order reordered to: anonymous -> next; exempt-path -> next (all roles); null-ActiveGroupId gate (all roles) -> redirect/409; next. No role-based bypass remains anywhere in the guard chain."
  - "SuperAdmin's genuine group-agnostic workflows stay reachable via the existing exempt-path list (/groups/pick, GroupPicker, Account, /platform, /Error) — the fix removes a role bypass, not group-agnostic functionality."

patterns-established:
  - "A role claim must never silently widen data/route scope by short-circuiting a security gate before the gate's own path-based exemptions are evaluated."

requirements-completed: []

# Metrics
duration: 35min
completed: 2026-07-06
---

# Phase 55 Plan 02: Remove SuperAdmin blanket bypass from GroupSessionMiddleware Summary

**Reordered `GroupSessionMiddleware`'s guard clauses so the exempt-path check runs before any role check and removed the blanket `IsInRole("SuperAdmin")` bypass, closing the confirmed root cause of the cross-tenant quest leak — a SuperAdmin with a null `ActiveGroupId` reaching `/quests` and seeing every group's data merged.**

## Performance

- **Duration:** ~35 min
- **Tasks:** 2 planned + 1 deviation fix
- **Files modified:** 4

## Accomplishments

- `GroupSessionMiddleware.InvokeAsync` no longer has a SuperAdmin role branch; the exempt-path check now runs immediately after the anonymous check, before `IActiveGroupContext` is even resolved
- SuperAdmin is now gated on group-scoped routes (`/quests`, `/Calendar`, `/DungeonMaster/EditProfile`, `/QuestLog`, etc.) exactly like every other role: redirected to `/groups/pick` on GET/HEAD, 409 Conflict on non-idempotent verbs
- SuperAdmin still passes through unchallenged on `/groups/pick`, `GroupPicker`, `Account`, `/platform`, and `/Error`
- Regular-user gate behavior (redirect on GET, 409 on POST, returnUrl preservation, no redirect loop on the picker path) is unchanged and still fully covered by the pre-existing tests
- Class XML doc comment rewritten to describe the corrected guard order and rationale
- `CONCERNS.md`'s stale "SuperAdmin should list all quests across all groups" rationale corrected and marked superseded, with a note that the fail-open shape was the confirmed root cause now closed

## Task Commits

Each task was committed atomically:

1. **Task 1: Rewrite the SuperAdmin integration test + add SuperAdmin [Theory] variant** - `fc30bfd` (test) — RED against the then-current (unfixed) middleware, confirmed by running the filtered test suite (4 failures, 8 pre-existing passes)
2. **Task 2: Reorder middleware guards (exempt-first, no SuperAdmin bypass) + rewrite doc + correct CONCERNS.md** - `6fcd099` (fix) — GREEN, all 12 `GroupSessionMiddlewareIntegrationTests` pass

**Deviation fix (Rule 1):** `3ccc2f4` (fix) — `DungeonMasterControllerIntegrationTests.Profile_SuperAdminNoActiveGroup_ReturnsNotFound` assumed the old bypass and broke once the middleware started gating SuperAdmin; renamed to `Profile_SuperAdminNoActiveGroup_RedirectsToGroupPick` and updated its assertion.

_Note: no separate plan-metadata commit — SUMMARY.md is committed by the parallel-worktree executor per its own protocol; the orchestrator handles STATE.md/ROADMAP.md after merge._

## Files Created/Modified

- `QuestBoard.Service/Middleware/GroupSessionMiddleware.cs` - Removed SuperAdmin bypass, reordered exempt-path check before the group gate, rewrote the class doc comment
- `QuestBoard.IntegrationTests/Controllers/GroupSessionMiddlewareIntegrationTests.cs` - Rewrote `SuperAdmin_NoActiveGroup_NotRedirectedByMiddleware` -> `SuperAdmin_NoActiveGroup_RedirectsToGroupPick` (inverted assertion), added `SuperAdmin_NoActiveGroup_ProtectedAreaRedirectsToGroupPick` `[Theory]`
- `QuestBoard.IntegrationTests/Controllers/DungeonMasterControllerIntegrationTests.cs` - Updated `Profile_SuperAdminNoActiveGroup_ReturnsNotFound` -> `Profile_SuperAdminNoActiveGroup_RedirectsToGroupPick` (deviation fix, see below)
- `.planning/codebase/CONCERNS.md` - Corrected the stale SuperAdmin-sees-all-groups rationale, marked superseded with the closure explanation

## Decisions Made

- Kept the fix scoped to guard reordering only — no changes to `ExemptPathPrefixes`, `ControllerNameOf`, the 409-vs-redirect logic, or returnUrl construction, per the plan's explicit constraint.
- Left the pre-existing missing `try/finally` restore-to-1 gap in `DungeonMasterControllerIntegrationTests.Profile_SuperAdminNoActiveGroup_RedirectsToGroupPick` untouched — it predates this plan's change and is out of scope (scope boundary rule: only fix issues directly caused by this task's changes).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Fixed a sibling test that assumed the removed SuperAdmin bypass**

- **Found during:** Post-Task-2 full integration suite run (`dotnet test QuestBoard.IntegrationTests`)
- **Issue:** `DungeonMasterControllerIntegrationTests.Profile_SuperAdminNoActiveGroup_ReturnsNotFound` set up a SuperAdmin with `ActiveGroupId = null` and asserted the controller itself returns 404 (verifying `DungeonMasterController`'s own null-group guard). That controller-level path is no longer reachable for SuperAdmin — the middleware now intercepts the request first and redirects to `/groups/pick`, exactly as this plan intends. The test failed with `302` instead of the expected `404`.
- **Fix:** Renamed to `Profile_SuperAdminNoActiveGroup_RedirectsToGroupPick`, rewrote the comment, and changed the assertion to match the new (correct) middleware-level redirect, mirroring the pattern already used in `GroupSessionMiddlewareIntegrationTests`.
- **Files modified:** `QuestBoard.IntegrationTests/Controllers/DungeonMasterControllerIntegrationTests.cs`
- **Verification:** Full `QuestBoard.IntegrationTests` suite re-run: 306/306 passed. `QuestBoard.UnitTests`: 169/169 passed.
- **Committed in:** `3ccc2f4`

---

**Total deviations:** 1 auto-fixed (Rule 1 — bug/regression directly caused by this plan's middleware change)
**Impact on plan:** Necessary to keep the full test suite green after closing the root-cause bypass; no scope creep — the fix only touches the one test whose premise the plan's own change invalidated.

## Issues Encountered

None beyond the deviation above — both planned tasks executed exactly as specified, and the RED/GREEN gate sequence (test commit before fix commit) behaved as expected.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- The middleware half of the cross-tenant leak fix is complete and fully test-covered. Combined with Plan 01's fail-closed filter (if executed in the same wave), a SuperAdmin can no longer reach a group-scoped board in the ambiguous "every group merged" state.
- `dotnet build` succeeds with 0 warnings/errors across all 5 projects.
- Full test suite green: 306 integration tests + 169 unit tests, all passing.
- No blockers for merge back to the main working branch.

---
*Phase: 55-fix-cross-tenant-quest-leak-on-quest-board-quests-from-anoth*
*Completed: 2026-07-06*
