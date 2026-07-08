---
phase: 61-allow-dms-to-edit-finalized-quest-details-excluding-proposed
plan: 01
subsystem: testing
tags: [integration-tests, xunit, quest-edit, tdd-red]

# Dependency graph
requires: []
provides:
  - "QuestFinalizedEditTests.cs — 8 failing integration tests pinning the finalized-quest Edit/Manage behavior plan 61-02 must implement"
affects: [61-02]

# Tech tracking
tech-stack:
  added: []
  patterns: []

key-files:
  created:
    - QuestBoard.IntegrationTests/Controllers/QuestFinalizedEditTests.cs
  modified: []

key-decisions:
  - "Test file structure copied verbatim from QuestCampaignUiParityTests (IClassFixture pattern, MobileUserAgent constant, mobile request-building block)"
  - "Selected-player-count and roster assertions built against QuestJoinFinalizedQuestTests' scope-based re-read pattern (fresh QuestBoardContext scope after POST)"
  - "POST form data omits Quest.DungeonMasterId (not rendered as a form field by Edit.cshtml today); confirmed safe by the pre-existing CampaignEdit_InvalidModelState_Returns200_DoesNotThrow test, which does the same and asserts 200 OK"

patterns-established: []

requirements-completed: []

# Metrics
duration: 12min
completed: 2026-07-07
---

# Phase 61 Plan 1: Wave-0 Failing Tests for Finalized-Quest Edit Summary

**8 RED integration tests in QuestFinalizedEditTests.cs pinning the finalized-quest Edit/Manage behavior (Proposed Dates hidden, Total Player Count floor, roster preservation, new Manage entry point) that plan 61-02 must turn GREEN**

## Performance

- **Duration:** 12 min
- **Started:** 2026-07-07T06:58:00Z
- **Completed:** 2026-07-07T07:10:00Z
- **Tasks:** 1 completed
- **Files modified:** 1 (new file)

## Accomplishments
- Authored `QuestFinalizedEditTests.cs` with 8 `[Fact]` tests covering: Edit GET returning 200 instead of 400 BadRequest on a finalized quest, Proposed Dates hidden while Challenge Rating/Total Player Count/DM-Session remain visible (desktop + mobile), a non-finalized regression guard, the Total Player Count floor guard (D-01) rejecting a lowered count below the selected-player total without persisting, a valid Title edit persisting without wiping the selected roster or `FinalizedDate`, and the new "Edit Quest" link appearing on the finalized-quest Manage page (desktop + mobile)
- Confirmed the suite is RED against the current tree: 7/8 tests fail (the 8th, the non-finalized regression guard, correctly passes since that behavior already exists)
- Verified zero production code was touched — `git status --short` shows only the new test file

## Task Commits

1. **Task 1: Write finalized-quest Edit + Manage failing integration tests** - `6a0c080` (test)

**Plan metadata:** committed alongside this SUMMARY

## Files Created/Modified
- `QuestBoard.IntegrationTests/Controllers/QuestFinalizedEditTests.cs` - 8 integration tests pinning finalized-quest Edit/Manage target behavior; RED until plan 61-02 implements the feature

## Decisions Made
- Followed the plan's exact test list and read-first analogs (`QuestCampaignUiParityTests` for structure, `QuestJoinFinalizedQuestTests` for finalized-quest-with-selected-players setup and scope-based re-read assertions)
- Omitted `Quest.DungeonMasterId` from POST form bodies since `Edit.cshtml` never renders it as a field and the existing `CampaignEdit_InvalidModelState_Returns200_DoesNotThrow` test already establishes this is safe (asserts 200 OK without it)

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

None. All 8 tests compiled on first pass; `dotnet build` and `dotnet test --filter "FullyQualifiedName~QuestFinalizedEditTests"` both ran cleanly, confirming the RED state matches the plan's acceptance criteria precisely (`FinalizedEdit_Get_Desktop_Returns200_NotBadRequest` fails with the current 400 BadRequest response, as expected).

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

Plan 61-02 can proceed directly to implementation: remove the `IsFinalized` BadRequest blocks from `QuestController.Edit` GET/POST, add the `IsFinalized`-conditional Proposed Dates hiding to `Edit.cshtml`/`Edit.Mobile.cshtml`, add the D-01 Total Player Count floor validation, swap `updateProposedDates` to `false` for finalized-quest edits, and add the "Edit Quest" button to `Manage.cshtml`/`Manage.Mobile.cshtml`'s finalized-OneShot button row. All 8 tests in this file should flip to GREEN once that implementation lands — no test file changes anticipated in 61-02 unless an edge case surfaces during implementation.

---
*Phase: 61-allow-dms-to-edit-finalized-quest-details-excluding-proposed*
*Completed: 2026-07-07*
