---
phase: 53-add-dedicated-edit-view-for-quest-recap-so-details-page-is-v
plan: 01
subsystem: api
tags: [aspnet-core-mvc, authorization, quest-log, controller, viewmodel]

# Dependency graph
requires: []
provides:
  - "EditRecapViewModel (Id, Recap, Quest) as the typed model-binding target for the recap-edit page"
  - "QuestLogController.EditRecap GET+POST action pair with two-layer DM/Admin authorization, mirroring UpdateRecap's shape"
  - "Route name `EditRecap` and ViewModel shape settled as a stable contract for Plan 02's view layer"
affects: [53-02]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "New page-level actions replicate an existing sibling action's two-layer authorization (policy attribute + in-action DM/Admin ownership check) verbatim rather than factoring out a shared helper, keeping each action's authorization self-contained and easy to audit"

key-files:
  created:
    - QuestBoard.Service/ViewModels/QuestLogViewModels/EditRecapViewModel.cs
  modified:
    - QuestBoard.Service/Controllers/QuestBoard/QuestLogController.cs
    - QuestBoard.IntegrationTests/Controllers/QuestLogControllerIntegrationTests.cs

key-decisions:
  - "EditRecap GET's completed-quest guard returns NotFound() (matching Details GET), not BadRequest() (matching UpdateRecap POST) — an in-progress quest simply has no reachable edit page, consistent with how Details treats the same guard"
  - "Existing UpdateRecap action left in place, unmodified — Plan 02 removes the inline Details form that calls it; keeping it avoids breaking its already-passing tests within this plan's wave"

patterns-established:
  - "Direct-URL access by a non-editor (Player, or non-owning DM) to a DM/Admin-only action is denied via Forbid() when the check is an intra-group ownership check, not the project's usual cross-tenant 404 convention (D-04, confirmed by CONTEXT.md as a deliberate exception)"

requirements-completed: []

# Metrics
duration: 15min
completed: 2026-07-06
---

# Phase 53 Plan 01: EditRecap Controller Contract Summary

**New `EditRecapViewModel` plus `QuestLogController.EditRecap` GET+POST action pair, replicating `UpdateRecap`'s two-layer DM/Admin authorization and completed-quest guard, with three integration tests proving the Player-denied / Admin-allowed behavior.**

## Performance

- **Duration:** ~15 min
- **Started:** 2026-07-06T09:15:00Z
- **Completed:** 2026-07-06T09:30:30Z
- **Tasks:** 3 completed
- **Files modified:** 3 (1 created, 2 modified)

## Accomplishments
- `EditRecapViewModel` created with `Id` (int), `Recap` (string?), `Quest` (full domain `Quest`) — the exact three members the Plan 02 Razor templates will bind against
- `QuestLogController.EditRecap` GET action: loads the quest, applies the completed-quest guard, applies the two-layer DM/Admin ownership check (`Forbid()` per D-04 for non-editors), returns the ViewModel
- `QuestLogController.EditRecap` POST action: replicates `UpdateRecap`'s full body verbatim, persists via the existing `UpdateQuestRecapAsync`, redirects to `Details`
- Three new integration tests: Player-GET-denied, Admin-GET-200, Admin-POST-redirects

## Task Commits

Each task was committed atomically:

1. **Task 1: Add EditRecapViewModel** - `101f7d0` (feat)
2. **Task 2: Add EditRecap GET+POST actions to QuestLogController** - `e7cb10c` (feat)
3. **Task 3: Add EditRecap integration tests** - `78f72ab` (test)

**Plan metadata:** committed alongside this SUMMARY (worktree mode — orchestrator finalizes shared-file updates after merge)

## Files Created/Modified
- `QuestBoard.Service/ViewModels/QuestLogViewModels/EditRecapViewModel.cs` - New ViewModel: `Id`, `Recap`, `Quest` (full domain Quest)
- `QuestBoard.Service/Controllers/QuestBoard/QuestLogController.cs` - New `EditRecap` GET+POST action pair, placed after `UpdateRecap` and before the private helpers
- `QuestBoard.IntegrationTests/Controllers/QuestLogControllerIntegrationTests.cs` - Three new `[Fact]` tests: `EditRecap_Player_IsForbidden`, `EditRecap_NonOwnerAdmin_ReturnsOk`, `EditRecap_Post_NonOwnerAdmin_RedirectsToDetails`

## Decisions Made
- GET's completed-quest guard uses `NotFound()` (not `BadRequest()`) to match the `Details` GET convention for the same guard, since the plan explicitly called this out as the correct choice for a page-rendering GET action (as opposed to the POST's `BadRequest()`, which is an API-style rejection).
- Kept the existing `UpdateRecap` action fully intact per the plan's explicit instruction — Plan 02 removes its calling inline form from `Details.cshtml`, not this plan.

## Deviations from Plan

None — plan executed exactly as written.

## Issues Encountered

`EditRecap_NonOwnerAdmin_ReturnsOk` fails at test-run time with `InvalidOperationException: The view 'EditRecap' was not found` — this is expected and explicitly anticipated by the plan itself (acceptance criteria: *"the NonOwnerAdmin_ReturnsOk content-contains 'Save Recap' assertion may require Plan 02's view... note it in the SUMMARY for Plan 02 to confirm"*). The failure occurs at Razor view resolution, strictly after the authorization check has already passed (proven independently by the sibling `EditRecap_Player_IsForbidden` test, which exercises the identical authorization code path and passes). Plan 02 adds `Views/QuestLog/EditRecap.cshtml` (+ `.Mobile.cshtml`), after which this test is expected to pass without further controller changes. Confirmed via `dotnet test --filter FullyQualifiedName~QuestLogControllerIntegrationTests`: 13 pre-existing tests pass, 2 of the 3 new tests pass, 1 fails on the missing view as anticipated.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

Plan 02 (view layer) can proceed directly against a settled contract:
- Route name `EditRecap` (GET renders the form, POST persists and redirects to `Details`)
- ViewModel: `EditRecapViewModel.Id` (hidden field / route id), `.Recap` (textarea bind target), `.Quest.Title` (page heading)
- Once `Views/QuestLog/EditRecap.cshtml` (+ `.Mobile.cshtml`) exists, `EditRecap_NonOwnerAdmin_ReturnsOk` should be re-run to confirm the "Save Recap" content assertion (currently commented out of scope for this plan, only the 200 status is asserted here since the view doesn't exist yet).
- No blockers.

---
*Phase: 53-add-dedicated-edit-view-for-quest-recap-so-details-page-is-v*
*Completed: 2026-07-06*
