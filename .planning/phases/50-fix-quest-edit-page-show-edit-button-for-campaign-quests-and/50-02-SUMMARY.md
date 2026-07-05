---
phase: 50-fix-quest-edit-page-show-edit-button-for-campaign-quests-and
plan: 02
subsystem: ui
tags: [razor, mvc-views, quest-manage, campaign-board-type]

# Dependency graph
requires:
  - phase: 50-01
    provides: BoardType-aware Manage/Edit view scaffolding and the CampaignManage_* / CampaignEdit_* wave-0 regression tests
provides:
  - Edit Quest and Delete affordances on the desktop Campaign Manage action row (D-01/D-02/D-03)
  - Edit Quest and Delete Quest affordances on the mobile Campaign Manage action row (D-01/D-03 mobile parity)
affects: [quest-manage, campaign-board-type-ui]

# Tech tracking
tech-stack:
  added: []
  patterns: [reuse-existing-markup-verbatim, inline-anchor-not-partial]

key-files:
  created: []
  modified:
    - QuestBoard.Service/Views/Quest/Manage.cshtml
    - QuestBoard.Service/Views/Quest/Manage.Mobile.cshtml

key-decisions:
  - "Mobile Edit Quest button uses btn-secondary (not UI-SPEC's literal btn-primary) to match the real existing mobile OneShot Edit Quest button convention, per RESEARCH.md Pitfall 1"
  - "Omitted ms-2 spacing utility on new desktop anchors since the parent d-flex gap-2 already provides spacing, per RESEARCH.md Open Question 2"

patterns-established: []

requirements-completed: [D-01, D-02, D-03]

# Metrics
duration: 12min
completed: 2026-07-05
status: complete
---

# Phase 50 Plan 02: Campaign Manage Action Row Edit/Delete Parity Summary

**Added Edit Quest and Delete/Delete Quest links to the desktop and mobile Campaign Manage action rows, reusing the existing Edit GET action and deleteQuest JS verbatim — no new controller, JS, or CSS.**

## Performance

- **Duration:** 12 min
- **Started:** 2026-07-05T19:43:00Z
- **Completed:** 2026-07-05T19:55:05Z
- **Tasks:** 2 completed
- **Files modified:** 2

## Accomplishments
- Desktop Campaign action row (`Manage.cshtml`) now renders Edit Quest (btn-primary, before Close/Reopen) and Delete (btn-danger, after Close/Reopen, wired to `deleteQuest(@Model.Id)`), matching the OneShot row's affordances.
- Mobile Campaign action row (`Manage.Mobile.cshtml`) now renders Edit Quest (btn-secondary flex-fill, before Close/Reopen) and Delete Quest (btn-danger w-100, after Close/Reopen, before Refresh Data, wired to `deleteQuest(@Model.Id)`).
- All 3 `CampaignManage_*` wave-0 tests (`CampaignManage_Desktop_RendersEditQuestLink`, `CampaignManage_Desktop_RendersDeleteLinkWiredToDeleteQuest`, `CampaignManage_Mobile_RendersEditQuestAndDeleteQuestLinks`) now PASS.

## Task Commits

Each task was committed atomically:

1. **Task 1: Add Edit Quest + Delete to the desktop Campaign action row (D-01, D-02, D-03)** - `21d3be0` (feat)
2. **Task 2: Add Edit Quest + Delete Quest to the mobile Campaign action row (D-01, D-03 mobile parity)** - `9d71625` (feat)

_Note: no TDD tasks in this plan; both are direct feat commits._

## Files Created/Modified
- `QuestBoard.Service/Views/Quest/Manage.cshtml` - Added Edit Quest (`btn btn-primary`) as first child and Delete (`btn btn-danger`) as last child of the Campaign action row's inner `d-flex gap-2` div; reuses `Url.Action("Edit", "Quest", ...)` and the existing `deleteQuest(@Model.Id)` JS function.
- `QuestBoard.Service/Views/Quest/Manage.Mobile.cshtml` - Added Edit Quest (`btn btn-secondary flex-fill`) as first child and Delete Quest (`btn btn-danger w-100`) after Close/Reopen (before Refresh Data) of the mobile Campaign action row's `d-flex flex-wrap gap-2 mt-3` div; same reuse pattern.

## Decisions Made
- Followed RESEARCH.md Pitfall 1: used `btn-secondary` (not UI-SPEC.md's literal `btn-primary`) for the mobile Edit Quest button, since UI-SPEC's stated intent was "verbatim reuse of the existing mobile pattern" and the real mobile OneShot Edit Quest button already uses `btn-secondary flex-fill`.
- Followed RESEARCH.md Open Question 2: omitted `ms-2` on the two new desktop anchors since the parent `d-flex gap-2` div already supplies spacing between children.

## Deviations from Plan

None - plan executed exactly as written. Both `read_first` corrections (mobile button color, spacing) were already specified in the plan's `<action>` blocks and were followed directly; they are not unplanned deviations.

## Issues Encountered

None. `dotnet build` succeeded with 0 warnings/errors after each task. The two pre-existing `CampaignEdit_Desktop_HidesFourOneShotFields` / `CampaignEdit_Mobile_HidesFourOneShotFields` test failures observed when running the full `QuestCampaignUiParity` filter are out of this plan's scope — they exercise `Quest/Edit.cshtml` (plan 50-01's `files_modified`), not `Manage.cshtml`/`Manage.Mobile.cshtml`, and are unrelated to the changes made here. All 3 `CampaignManage_*` tests targeted by this plan pass in isolation.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- Both Manage-page action rows now have full Edit/Delete parity with the OneShot row for Campaign quests. No blockers for downstream work.
- The pre-existing `CampaignEdit_*` test failures (Edit.cshtml field-visibility, plan 50-01 scope) remain open and are not addressed by this plan.

---
*Phase: 50-fix-quest-edit-page-show-edit-button-for-campaign-quests-and*
*Completed: 2026-07-05*
