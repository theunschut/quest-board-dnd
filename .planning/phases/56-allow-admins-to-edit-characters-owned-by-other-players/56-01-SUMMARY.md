---
phase: 56-allow-admins-to-edit-characters-owned-by-other-players
plan: 01
subsystem: auth
tags: [aspnet-core-mvc, ef-core, authorization, groupRole, characters]

# Dependency graph
requires:
  - phase: 34.3-group-role-authorization-regression-fix
    provides: GetEffectiveGroupRoleAsync helper (SuperAdmin-as-Admin resolution)
  - phase: 49-fix-guild-members-page-missing-group-tenant-filtering
    provides: CharacterEntity fail-closed EF Core query filter (GroupId scoping)
provides:
  - Admin/SuperAdmin ownership-OR-admin bypass on GuildMembersController Edit/Delete/ToggleRetirement
  - CanEdit flag on CharacterViewModel driving Details Actions card visibility
  - Fix for a pre-existing CharacterRepository.UpdateAsync concurrency bug (Classes/ProfileImage navigation replacement)
affects: [guild-members, character-management]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Ownership-OR-admin inline guard (mirrors DungeonMasterController.EditProfile): resolve role via GetEffectiveGroupRoleAsync, OR into the owner check, never re-derive SuperAdmin short-circuit at the call site"
    - "Reconcile EF Core child navigation collections by Id after AutoMapper.Map(model, entity) instead of trusting AutoMapper's default List<T> replacement, when the destination entity's child collections were loaded via Include"

key-files:
  created: []
  modified:
    - QuestBoard.Service/Controllers/Characters/GuildMembersController.cs
    - QuestBoard.Service/ViewModels/CharacterViewModels/CharacterViewModel.cs
    - QuestBoard.Service/Views/GuildMembers/Details.cshtml
    - QuestBoard.Service/Views/GuildMembers/Details.Mobile.cshtml
    - QuestBoard.IntegrationTests/Controllers/GuildMembersControllerIntegrationTests.cs
    - QuestBoard.Repository/CharacterRepository.cs

key-decisions:
  - "Admin (per-group) and SuperAdmin (resolved to Admin-equivalent via GetEffectiveGroupRoleAsync) can Edit, Delete, and Retire/Reactivate any character in their active group; DungeonMaster and Player remain owner-only, per the plan's locked scope (D-01/D-02)."
  - "No hand-rolled cross-tenant guard added — CharacterEntity's existing fail-closed EF Core query filter already returns 404 for a cross-group character id (D-03)."
  - "CanEdit added as a new CharacterViewModel boolean, distinct from IsOwner, which keeps its strict true-ownership meaning (D-04)."
  - "Denied-access test assertions use BeOneOf(Forbidden, Redirect, Unauthorized) instead of a bare Forbidden, matching this suite's established convention — Forbid() under the cookie authentication scheme redirects to /Account/AccessDenied (302) rather than returning a raw 403."

patterns-established:
  - "When overriding BaseRepository<T,TEntity>.UpdateAsync for an entity with EF-tracked child collections, load the child navigations via Include and reconcile by Id after Mapper.Map(model, entity), rather than letting AutoMapper's List<T> mapping silently replace tracked instances."

requirements-completed: [D-01, D-02, D-03, D-04]

# Metrics
duration: 55min
completed: 2026-07-06
status: complete
---

# Phase 56 Plan 01: Allow Admins to Edit Characters Owned by Other Players Summary

**Admin/SuperAdmin ownership-OR-admin bypass added to GuildMembersController's Edit/Delete/ToggleRetirement, mirroring DungeonMasterController.EditProfile's existing pattern, plus a pre-existing EF Core child-collection concurrency bug fixed along the way.**

## Performance

- **Duration:** ~55 min
- **Tasks:** 3 completed
- **Files modified:** 6

## Accomplishments
- Admin and SuperAdmin (with an active group selected) can now Edit, Delete, and Retire/Reactivate any character in their active group, not just their own
- `CanEdit` boolean added to `CharacterViewModel`, distinct from `IsOwner`; Details Actions card now gates on `CanEdit` in both desktop and mobile views
- 11 new authorization integration tests covering Admin/SuperAdmin success, Player-denied regression, cross-group 404, owner-still-works regression, and Details-button-content assertion
- Fixed a pre-existing latent `DbUpdateConcurrencyException` bug in `CharacterRepository.UpdateAsync` that blocked any successful Delete/ToggleRetirement/Edit-with-classes flow

## Task Commits

Each task was committed atomically:

1. **Task 1: Wave 0 — write the 11 failing authorization integration tests** - `45d04e3` (test)
2. **Task 2: Add CanEdit flag and apply the Ownership-OR-Admin guard to all three actions + Details** - `5362198` (feat)
3. **Task 3: Gate the Details Actions card on CanEdit in both desktop and mobile views** - `f92c7e3` (feat)

_Task 2's commit includes the CharacterRepository.UpdateAsync bug fix (Rule 1 deviation), not a separate commit — it was required to make Task 2's own acceptance-criteria tests pass._

## Files Created/Modified
- `QuestBoard.Service/Controllers/Characters/GuildMembersController.cs` - Ownership-OR-admin guard on Edit (GET/POST), Delete, ToggleRetirement; CanEdit computed in Details
- `QuestBoard.Service/ViewModels/CharacterViewModels/CharacterViewModel.cs` - New `CanEdit` boolean
- `QuestBoard.Service/Views/GuildMembers/Details.cshtml` - Actions card gated on `Model.CanEdit` instead of `Model.IsOwner`
- `QuestBoard.Service/Views/GuildMembers/Details.Mobile.cshtml` - Same gate change, mobile variant
- `QuestBoard.IntegrationTests/Controllers/GuildMembersControllerIntegrationTests.cs` - 11 new authorization tests (Edit/Delete/ToggleRetirement/Details, Admin/SuperAdmin/Player/cross-group/owner cases)
- `QuestBoard.Repository/CharacterRepository.cs` - `UpdateAsync` override reconciling `Classes`/`ProfileImage` navigations by Id instead of trusting AutoMapper's default list replacement

## Decisions Made
- Extended the Admin bypass to all three actions (Edit, Delete, ToggleRetirement) uniformly, per the plan's locked D-01, rather than only Edit — avoiding an inconsistent half-finished admin experience.
- Kept `IsOwner` strictly meaning true ownership; added `CanEdit` as a new, independently queryable flag (matches the existing `DMProfileViewModel.CanEdit` precedent).
- Test assertions for denied access use `BeOneOf(Forbidden, Redirect, Unauthorized)` rather than a bare `Forbidden`, matching the established codebase convention for this test harness's cookie-scheme `Forbid()` behavior.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Test assertions for denied-access cases needed to match actual `Forbid()` behavior under the test harness's cookie auth scheme**
- **Found during:** Task 1 (writing the 11 failing tests)
- **Issue:** The plan's literal wording said to assert `HttpStatusCode.Forbidden` for denied-access cases. In this codebase's integration test harness, `Forbid()` under the `Identity.Application` cookie scheme redirects to `/Account/AccessDenied` (302), not a raw 403 — confirmed as the established pattern in `DungeonMasterControllerIntegrationTests` and `QuestControllerAuthorizationRegressionTests`, which both assert `BeOneOf(Forbidden, Redirect, Unauthorized)` for identical cases.
- **Fix:** Changed the 3 denied-access assertions (`Edit_PlayerEditingAnotherPlayersCharacter_ShouldBeForbidden`, `Delete_PlayerDeletingAnotherPlayersCharacter_ShouldBeForbidden`, `ToggleRetirement_PlayerTogglingAnotherPlayersCharacter_ShouldBeForbidden`) to `BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.Redirect, HttpStatusCode.Unauthorized)`.
- **Files modified:** `QuestBoard.IntegrationTests/Controllers/GuildMembersControllerIntegrationTests.cs`
- **Committed in:** `45d04e3` (Task 1 commit)

**2. [Rule 1 - Bug] Delete/ToggleRetirement success assertions strengthened to distinguish a real success-redirect from a Forbid-redirect**
- **Found during:** Task 1 (writing the 11 failing tests)
- **Issue:** A bare `response.StatusCode.Should().Be(HttpStatusCode.Redirect)` for the admin-success Delete/ToggleRetirement tests would pass for the wrong reason during the RED phase, since `Forbid()` under the cookie scheme also produces a 302. This was caught when the "should succeed" tests initially passed even before the admin bypass was implemented.
- **Fix:** Added a `Location` header check (`Should().NotContain("AccessDenied")`) plus a direct database assertion confirming the character was actually deleted / status actually flipped to `Retired`.
- **Files modified:** `QuestBoard.IntegrationTests/Controllers/GuildMembersControllerIntegrationTests.cs`
- **Committed in:** `45d04e3` (Task 1 commit)

**3. [Rule 1 - Bug] Fixed a pre-existing `DbUpdateConcurrencyException` in `CharacterRepository.UpdateAsync`**
- **Found during:** Task 2 (making `ToggleRetirement_AdminTogglingAnotherPlayersCharacter_ShouldSucceed` pass)
- **Issue:** `CharacterRepository.UpdateAsync`'s base implementation (`DbSet.FindAsync` + `Mapper.Map(model, entity)`) doesn't load the `Classes`/`ProfileImage` navigations before mapping. AutoMapper's default list/object mapping then replaces those tracked navigations with brand-new CLR instances carrying the same primary-key `Id` as existing rows. EF Core's change tracker treats those as detached objects it never queried, and the InMemory test provider rejects the resulting save with `DbUpdateConcurrencyException: Attempted to update or delete an entity that does not exist in the store`. This was a genuinely pre-existing, latent bug (traced with a scratch diagnostic test calling `CharacterService.UpdateAsync` directly, with no HTTP or controller code involved) — it was previously unreachable by any automated test because zero success-path tests existed for `ToggleRetirement`, `Delete`-with-classes, or a plain `Edit POST` before this phase (confirmed in RESEARCH.md Pitfall 4).
- **Fix:** Overrode `UpdateAsync` in `CharacterRepository` to load `Classes` and `ProfileImage` via `Include` before mapping, then reconcile the `Classes` collection by `Id` afterward (updating existing tracked rows in place, removing stale ones, adding genuinely new ones) and restore the tracked `ProfileImage` reference (which `UpdateProfileImageAsync` already persists correctly in a prior step of `CharacterService.UpdateAsync`).
- **Files modified:** `QuestBoard.Repository/CharacterRepository.cs`
- **Verification:** Confirmed via a throwaway diagnostic test calling `CharacterService.UpdateAsync` directly (removed before commit); all 15 `GuildMembersControllerIntegrationTests` pass; full solution suite (183 unit + 329 integration tests) green.
- **Committed in:** `5362198` (Task 2 commit)

---

**Total deviations:** 3 auto-fixed (2 test-assertion bugs, 1 pre-existing EF Core concurrency bug)
**Impact on plan:** All three fixes were necessary for the plan's own acceptance criteria to be verifiable/passable. No scope creep — the repository fix touches a file outside the plan's declared `<files>` list, but is a direct, minimal, targeted fix for a bug blocking Task 2's stated acceptance criteria (ToggleRetirement/Delete success tests passing), not a broader refactor.

## Issues Encountered

Diagnosing the `DbUpdateConcurrencyException` required writing a throwaway diagnostic xUnit test that called `CharacterService.UpdateAsync` directly (bypassing HTTP/controller layers) to get past the app's production-style `UseExceptionHandler("/Error")` masking the real exception in the Testing environment. The diagnostic test was removed before the final commit; the root cause (AutoMapper's `List<T>`/object mapping replacing EF-tracked child navigations with untracked instances of the same Id) was confirmed via `DbUpdateException.Entries` inspection.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

Phase 56 is fully self-contained (single plan, no dependents declared in the roadmap). No blockers. The `CharacterRepository.UpdateAsync` fix is a general-purpose correctness fix that also silently benefits the pre-existing `Edit(POST)` path (which shares the same underlying bug, previously untested for the success case).

---
*Phase: 56-allow-admins-to-edit-characters-owned-by-other-players*
*Completed: 2026-07-06*
