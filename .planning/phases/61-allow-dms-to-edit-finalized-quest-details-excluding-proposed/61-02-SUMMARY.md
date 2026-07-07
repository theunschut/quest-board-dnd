---
phase: 61-allow-dms-to-edit-finalized-quest-details-excluding-proposed
plan: 02
subsystem: quests
tags: [aspnet-mvc, razor, quest-edit, finalized-quest, modelstate-validation]

# Dependency graph
requires:
  - phase: 61-01
    provides: QuestFinalizedEditTests.cs (8 RED integration tests pinning this plan's required behavior)
provides:
  - Relaxed finalized-quest Edit GET/POST guard (200 instead of 400 BadRequest)
  - EditQuestViewModel.IsFinalized flag threaded through controller and views
  - D-01 Total Player Count floor guard (ModelState validation, no roster corruption)
  - Conditional updateProposedDates: false path for finalized-quest edits (no date/email mutation)
  - Proposed Dates hidden on finalized quests (Edit.cshtml + Edit.Mobile.cshtml)
  - Edit Quest entry point on the finalized-OneShot Manage row (desktop + mobile)
affects: [quest-management, quest-editing]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "IsFinalized boolean threaded via EditQuestViewModel (not ViewBag), assigned in both Edit GET and POST re-render paths"
    - "ModelState.AddModelError with a literal string key (\"Quest.TotalPlayerCount\") instead of nameof(), to match the nested asp-validation-for binding path"
    - "updateProposedDates boolean parameter on UpdateQuestPropertiesWithNotificationsAsync used as the finalized-quest date-skip seam, no service/repository change required"

key-files:
  created: []
  modified:
    - QuestBoard.Service/ViewModels/QuestViewModels/EditQuestViewModel.cs
    - QuestBoard.Service/Controllers/QuestBoard/QuestController.cs
    - QuestBoard.Service/Views/Quest/Edit.cshtml
    - QuestBoard.Service/Views/Quest/Edit.Mobile.cshtml
    - QuestBoard.Service/Views/Quest/Manage.cshtml
    - QuestBoard.Service/Views/Quest/Manage.Mobile.cshtml

key-decisions:
  - "ModelState.AddModelError key literal (\"Quest.TotalPlayerCount\") used instead of nameof(viewModel.Quest.TotalPlayerCount), since nameof() would only yield \"TotalPlayerCount\" and silently miss the view's asp-validation-for=\"Quest.TotalPlayerCount\" binding"
  - "Desktop Quest Editing Tips sidebar shows a finalized-specific message (pointing to Open Quest) instead of hiding the tips card entirely, keeping the sidebar accurate rather than empty"

patterns-established:
  - "Finalized-state field visibility nests one level inside the existing BoardType-conditional block, narrower in scope than the outer @if (boardType != BoardType.Campaign) check"

requirements-completed: [61]

# Metrics
duration: 25min
completed: 2026-07-07
---

# Phase 61 Plan 02: Allow DMs to edit finalized quest details Summary

**Relaxed QuestController.Edit's finalized-quest block so DMs can edit Title/Description/Rewards/CR/PlayerCount/DM-Session on a finalized OneShot quest without wiping the roster, with a D-01 floor guard on Total Player Count and Proposed Dates hidden on both desktop and mobile Edit views.**

## Performance

- **Duration:** ~25 min
- **Started:** 2026-07-07T06:56:04Z
- **Completed:** 2026-07-07T07:11:28Z
- **Tasks:** 3
- **Files modified:** 6

## Accomplishments
- `QuestController.Edit` GET and POST no longer 400 on a finalized quest; both now render/process the existing Edit form
- Total Player Count cannot be lowered below the currently-selected player count on a finalized quest — rejected via `ModelState.AddModelError` and re-rendered, nothing persists
- Finalized-quest edits call `UpdateQuestPropertiesWithNotificationsAsync` with `updateProposedDates: false` and `proposedDates: null`, so `ProposedDates`/`FinalizedDate`/selected roster are never touched and no date-changed email fires
- Proposed Dates section is hidden on both `Edit.cshtml` and `Edit.Mobile.cshtml` for finalized quests, while Challenge Rating/Total Player Count/DM-Session stay visible and editable
- `Manage.cshtml` and `Manage.Mobile.cshtml` gained an "Edit Quest" link on the finalized-OneShot action row, ordered ahead of the destructive "Open Quest" action

## Task Commits

Each task was committed atomically:

1. **Task 1: Relax finalized-edit block, add IsFinalized plumbing + D-01 guard in controller/ViewModel** - `40354fd` (feat)
2. **Task 2: Hide Proposed Dates on finalized quests in BOTH Edit views (desktop + mobile)** - `8a02753` (feat)
3. **Task 3: Add Edit Quest entry point to the finalized OneShot row in BOTH Manage views (desktop + mobile)** - `bd1c2d1` (feat)

**Plan metadata:** (this commit)

## Files Created/Modified
- `QuestBoard.Service/ViewModels/QuestViewModels/EditQuestViewModel.cs` - added `public bool IsFinalized { get; set; }`
- `QuestBoard.Service/Controllers/QuestBoard/QuestController.cs` - removed both `IsFinalized` BadRequest blocks (GET+POST), added `IsFinalized` assignment in GET's view-model construction and POST's re-render path, added the D-01 Total Player Count floor guard, swapped the hardcoded `true`/`viewModel.Quest.ProposedDates` args for `!existingQuest.IsFinalized`/`existingQuest.IsFinalized ? null : viewModel.Quest.ProposedDates`
- `QuestBoard.Service/Views/Quest/Edit.cshtml` - wrapped the Proposed Dates sub-block in `@if (!Model.IsFinalized)`; sidebar tips now show a finalized-state message when applicable
- `QuestBoard.Service/Views/Quest/Edit.Mobile.cshtml` - wrapped the Proposed Dates sub-block in `@if (!Model.IsFinalized)`; the top-of-page existing-signups alert (entirely date-focused) also gated on `!Model.IsFinalized`
- `QuestBoard.Service/Views/Quest/Manage.cshtml` - added an "Edit Quest" link to the finalized-OneShot button row, ahead of "Open Quest"
- `QuestBoard.Service/Views/Quest/Manage.Mobile.cshtml` - added the matching "Edit Quest" link (with `flex-fill`) to the finalized-OneShot button row, ahead of "Open Quest"

## Decisions Made
- `ModelState.AddModelError` uses the literal string key `"Quest.TotalPlayerCount"` rather than `nameof(viewModel.Quest.TotalPlayerCount)`, per the plan's explicit instruction — `nameof()` would drop the `Quest.` prefix and silently fail to bind to the view's `asp-validation-for="Quest.TotalPlayerCount"` span.
- Desktop's "Quest Editing Tips" sidebar swaps in a finalized-specific tip (pointing DMs to Open Quest for date changes) rather than hiding the tips card body entirely, keeping the sidebar informative instead of blank.

## Deviations from Plan

None - plan executed exactly as written. All acceptance criteria for all three tasks were verified directly (grep confirmations for `IsFinalized` plumbing, zero remaining `"Cannot edit a finalized quest"` occurrences, correct nesting of the `!Model.IsFinalized` view conditions, and the Edit Quest link placement/ordering in both Manage views).

## Issues Encountered
None.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

All 8 integration tests from 61-01's `QuestFinalizedEditTests.cs` pass (verified individually per-task and as a full suite run). Full solution regression check: 191/191 unit tests, 361/361 integration tests, 0 build warnings/errors. This closes out Phase 61 — no further plans needed for this phase.
