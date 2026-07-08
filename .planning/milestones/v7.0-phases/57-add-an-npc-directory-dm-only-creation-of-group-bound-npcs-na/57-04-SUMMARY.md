---
phase: 57-add-an-npc-directory-dm-only-creation-of-group-bound-npcs-na
plan: 04
subsystem: api
tags: [aspnet-core-mvc, authorization, session, automapper, contacts]

# Dependency graph
requires:
  - phase: 57-03
    provides: IContactService/IContactRepository, Contact/ContactNote domain models, Entity<->Domain AutoMapper wiring
provides:
  - ContactsController (Index/Details/Create/Edit/Delete/ToggleReveal/ToggleShowHidden/AddNote/EditNote/DeleteNote/GetContactImage)
  - ContactViewModel/ContactNoteViewModel/ContactsIndexViewModel
  - SessionKeys.ShowHiddenContactsKey(groupId) per-group session toggle key
  - Domain<->ViewModel AutoMapper wiring for Contact/ContactNote
  - TestDataHelper.CreateTestContactAsync/CreateTestContactNoteAsync fixtures
affects: [57-05, 57-06]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Flat policy-gate authorization (no owner-or-admin guard) for records with no ownership concept"
    - "Controller-level three-branch visibility check (creator OR revealed OR DM-tier-toggle) returning NotFound() instead of a query-filter-level check"
    - "Per-group session toggle key via RequireActiveGroupId(), never the nullable ActiveGroupId"

key-files:
  created:
    - QuestBoard.Service/Controllers/Contacts/ContactsController.cs
    - QuestBoard.Service/ViewModels/ContactViewModels/ContactViewModel.cs
    - QuestBoard.Service/ViewModels/ContactViewModels/ContactsIndexViewModel.cs
  modified:
    - QuestBoard.Service/Automapper/ViewModelProfile.cs
    - QuestBoard.Service/Constants/SessionKeys.cs
    - QuestBoard.IntegrationTests/Helpers/TestDataHelper.cs

key-decisions:
  - "Reused MaxFileSizeAttribute/AllowedExtensionsAttribute by duplicating them into the Contact ViewModels namespace rather than cross-referencing CharacterViewModels, avoiding a Service-internal namespace coupling between two unrelated features."
  - "Three-branch visibility filtering lives in the controller, not a query filter or a repository parameter, since IContactService's existing GetAllContactsWithDetailsAsync/GetContactWithDetailsAsync signatures (built in 57-03) take no visibility arguments -- the controller composes the group-scoped query filter result with the D-13/D-15 visibility rule itself."
  - "Added TestDataHelper.CreateTestContactAsync/CreateTestContactNoteAsync (flagged as missing by 57-01's own SUMMARY.md) so the Wave 0 integration suite compiles; mirrors CreateTestCharacterAsync's shape exactly."

patterns-established:
  - "Pattern: DungeonMasterOnly policy attribute is the sole authorization boundary for Create/Edit/Delete/ToggleReveal/ToggleShowHidden -- no CanManageCharacterAsync-style per-record owner check, since Contacts carry no ownership/edit-restriction concept (CreatedByUserId is visibility-only, per D-07)."

requirements-completed: []

# Metrics
duration: 45min
completed: 2026-07-06
status: complete
---

# Phase 57 Plan 04: Contacts Controller & ViewModels Summary

**ContactsController with DungeonMasterOnly-gated core-field actions, a controller-level three-branch hidden-Contact 404, and a per-group session-backed Show Hidden toggle — makes 12/20 of Plan 01's integration test matrix pass (the remaining 8 fail only on missing views, which Plan 05 builds).**

## Performance

- **Duration:** ~45 min
- **Tasks:** 2
- **Files modified:** 6 (3 created, 3 modified)

## Accomplishments
- `ContactsController` exposes all 10 required actions with correct per-action authorization split: `[Authorize(Policy = "DungeonMasterOnly")]` on Create/Edit/Delete/ToggleReveal/ToggleShowHidden, plain `[Authorize]` on Index/Details/AddNote/EditNote/DeleteNote
- Three-branch hidden-Contact visibility check (creator sees own hidden Contacts regardless of toggle; revealed Contacts visible to everyone; DM-tier viewers see hidden Contacts only with their per-group Show Hidden toggle on) implemented in the controller and enforced on both Index (filtering) and Details (404)
- `ToggleShowHidden` session key always derived from `RequireActiveGroupId()`, confirmed per-group scoped by test (`ToggleShowHidden_IsScopedPerGroup_DoesNotLeakAcrossGroups` passes the toggle-isolation assertion, fails only on the later Index view-render step)
- `Create` POST stamps `GroupId`/`CreatedByUserId`/`IsRevealed = false` by construction
- Cross-tenant IDOR on `Details/{id}` returns 404 for a Contact in a different group (relies on `ContactEntity`'s existing fail-closed query filter from 57-02, plus the controller's NotFound fallback)
- Added `TestDataHelper.CreateTestContactAsync`/`CreateTestContactNoteAsync` so the Wave 0 `ContactsControllerIntegrationTests.cs` scaffold (Plan 01) finally compiles

## Task Commits

Each task was committed atomically:

1. **Task 1: Create ContactViewModel + ContactsIndexViewModel + ContactNoteViewModel, add ShowHiddenContactsKey, wire ViewModelProfile** - `c8a4737` (feat)
2. **Task 2: Implement ContactsController (policy gating, three-branch 404, session toggle, note actions, image serving)** - `19cf5f3` (feat) — also includes the required `TestDataHelper` additions

**Plan metadata:** (this commit)

## Files Created/Modified
- `QuestBoard.Service/Controllers/Contacts/ContactsController.cs` - Full HTTP surface for Contacts: policy-gated core-field actions, plain-Authorize Index/Details/notes, session toggle read/write, image serving
- `QuestBoard.Service/ViewModels/ContactViewModels/ContactViewModel.cs` - ContactViewModel (Name/Description/TownCity/SubLocation/ContactImage/ContactImageFile/IsRevealed/CreatedByUserId/CanManage/Notes) + ContactNoteViewModel + reused MaxFileSize/AllowedExtensions validators
- `QuestBoard.Service/ViewModels/ContactViewModels/ContactsIndexViewModel.cs` - Flat Contacts list + ShowHidden + ViewerIsDmTier
- `QuestBoard.Service/Automapper/ViewModelProfile.cs` - Contact<->ContactViewModel and ContactNote<->ContactNoteViewModel mappings
- `QuestBoard.Service/Constants/SessionKeys.cs` - `ShowHiddenContactsKey(int groupId)` helper
- `QuestBoard.IntegrationTests/Helpers/TestDataHelper.cs` - `CreateTestContactAsync`/`CreateTestContactNoteAsync` fixtures mirroring `CreateTestCharacterAsync`

## Decisions Made
- Duplicated `MaxFileSizeAttribute`/`AllowedExtensionsAttribute` into the Contact ViewModels namespace rather than referencing `CharacterViewModels` via `using`, per the plan's explicit "either/or" instruction — keeps the Contact feature self-contained.
- The controller (not the repository/service) owns the three-branch visibility filter, since `IContactService`'s methods built in Plan 03 (`GetAllContactsWithDetailsAsync`, `GetContactWithDetailsAsync`) take no `includeHidden`/`currentUserId` parameters — the plan's own artifact signature already fixed this shape in an earlier plan, so Plan 04 composes visibility on top rather than changing the service interface.
- `IsDmTierAsync`/`ReadShowHiddenToggle` are display-only helpers (compute `CanManage`, toggle-visibility) — the actual security boundary for gated actions remains the `[Authorize(Policy = "DungeonMasterOnly")]` attribute alone, per D-09b and RESEARCH.md Pitfall 1.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Added missing TestDataHelper.CreateTestContactAsync/CreateTestContactNoteAsync**
- **Found during:** Task 2 verification (`dotnet build` failed with 18 `CS0117` errors before this fix)
- **Issue:** `ContactsControllerIntegrationTests.cs` (Plan 01's Wave 0 scaffold) references `TestDataHelper.CreateTestContactAsync`/`CreateTestContactNoteAsync`, which did not exist — 57-01-SUMMARY.md explicitly flagged this as work for whichever plan first needs it, and the orchestrator's own objective for this plan called it out directly.
- **Fix:** Added both methods to `TestDataHelper.cs`, mirroring `CreateTestCharacterAsync`'s signature/shape (creates a `ContactEntity`/`ContactNoteEntity` directly via `QuestBoardContext`, returns the persisted entity).
- **Files modified:** `QuestBoard.IntegrationTests/Helpers/TestDataHelper.cs`
- **Verification:** `dotnet build` succeeds; the integration test file now compiles.
- **Committed in:** `19cf5f3` (Task 2 commit)

---

**Total deviations:** 1 auto-fixed (1 blocking)
**Impact on plan:** Necessary to make the pre-existing Wave 0 test scaffold compile at all — no scope creep, this was explicitly directed by the orchestrator's objective.

## Issues Encountered
- 8 of the 20 `ContactsControllerIntegrationTests` still fail after this plan — all 8 fail with `The view 'Index'/'Details'/'Create' was not found`, since `Views/Contacts/*.cshtml` do not exist yet. This is expected: this plan's `files_modified` list is scoped to Controller/ViewModels/AutoMapper/SessionKeys only; Plan 05 (wave 4, `depends_on: ["57-04"]`) owns all four desktop views. The remaining 12 tests — covering every authorization/policy-gating, hidden-Contact-404, cross-tenant-IDOR, and collaborative-notes assertion — all pass, confirming the controller's security-critical logic is correct independent of the view layer.

## Next Phase Readiness
- `ContactsController` and its ViewModels are complete and match every acceptance criterion in the plan (policy placement, three-branch 404, session-scoped toggle key, no `CanManageCharacterAsync`-style guard, antiforgery on every POST).
- Plan 05 can now build `Views/Contacts/Index.cshtml`/`Details.cshtml`/`Edit.cshtml`/`Create.cshtml` against a fully working controller — no further controller changes anticipated.
- Full solution: `dotnet build` succeeds (0 warnings, 0 errors); `dotnet test` — 343/351 passing (534/542 once merged with sibling worktree plans), the 8 remaining Contacts failures resolve once Plan 05's views land; `QuestBoard.UnitTests` 191/191 passing, unaffected by this plan.

---
*Phase: 57-add-an-npc-directory-dm-only-creation-of-group-bound-npcs-na*
*Completed: 2026-07-06*

## Self-Check: PASSED

- FOUND: QuestBoard.Service/Controllers/Contacts/ContactsController.cs
- FOUND: QuestBoard.Service/ViewModels/ContactViewModels/ContactViewModel.cs
- FOUND: QuestBoard.Service/ViewModels/ContactViewModels/ContactsIndexViewModel.cs
- FOUND: commit c8a4737 (Task 1)
- FOUND: commit 19cf5f3 (Task 2)
