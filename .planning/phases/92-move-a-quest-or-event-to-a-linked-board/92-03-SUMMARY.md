---
phase: 92-move-a-quest-or-event-to-a-linked-board
plan: 03
subsystem: board-move
tags: [move, eligibility, preview, stamp, domain]
requires:
  - phase: 92-01
    provides: tracer quest move (QuestMoveService, MoveController, IQuestMoveRepository.ApplyAsync)
provides:
  - MoveEligibility.ForQuest / ForEvent (pure, board date as a parameter)
  - MovePreviewStamp.Compute (SHA-256 change detector)
  - IQuestMoveService.PreviewAsync and the stamp-checked MoveAsync
  - MoveCopy.ControlReason / ToastReason
  - Blocked and Changed branches on MoveController.ConfirmQuest
affects: [92-04, 92-05, 92-06, 92-07, 92-08]
tech-stack:
  added: []
  patterns:
    - "One preview object doubles as the outcome; every POST rebuilds it from the store"
    - "Eligibility takes the board-local date as a parameter and never reads a clock"
key-files:
  created:
    - QuestBoard.Domain/Extensions/MoveEligibility.cs
    - QuestBoard.Domain/Extensions/MovePreviewStamp.cs
    - QuestBoard.Service/Helpers/MoveCopy.cs
    - QuestBoard.UnitTests/Extensions/BoardMoveEligibilityTests.cs
    - QuestBoard.UnitTests/Services/BoardMoveQuestPreviewTests.cs
  modified:
    - QuestBoard.Domain/Interfaces/IQuestMoveService.cs
    - QuestBoard.Domain/Services/QuestMoveService.cs
    - QuestBoard.Service/Controllers/MoveController.cs
    - QuestBoard.IntegrationTests/Tests/BoardMoveApplyTests.cs
key-decisions:
  - "IBoardClock is injected before the logger in QuestMoveService; today is read only from IBoardClock.Today"
  - "stamp is bound as string? on the POST and treated as empty when absent, so a missing field reads as a stale stamp"
patterns-established:
  - "Stamp is a change detector only; authorization rests on the server recompute"
status: complete
actuals:
  tokens: 14000
  tasks: 1
  commits: 2
---

# Phase 92 Plan 03: Preview, stamp and eligibility Summary

**Domain preview contract for the quest move: a SHA-256 stamp over the consequence set, a pure eligibility rule for quests and events, and a POST that recomputes everything and applies only when the stamp still matches.**

## Accomplishments
- `MoveEligibility.ForQuest` (closed first, then played) and `ForEvent` (series first, then past; cancelled events stay movable), both taking the board date as a parameter.
- `PreviewAsync` with the order: no board, no quest, eligibility, linked set, target preselection (one other board) or `ChooseTarget` (several), fresh target check, plan and stamp.
- `MoveAsync` rebuilds the preview, returns refusals unchanged, returns `Changed` on a stale stamp without applying, and on a repository `Changed` rebuilds and returns the fresh preview as `Changed` (or its refusal status).
- `MoveController.ConfirmQuest` takes the `stamp` form field and redirects to Details with the Blocked and Changed toasts.

## Task Commits
1. RED: failing eligibility, preview and stamp tests - `6b57c3c6`
2. GREEN: implementation, controller branches and integration facts - `e7d7d0b1`

## Verification
- Unit: `BoardMove*` and `Architecture` filters, 110 passed (includes `AmbientClockSeamTests`).
- Integration: `BoardMoveApply` and `AntiForgeryTokenCoverage`, 8 passed.
- No planning references in added source lines.

## Deviations from Plan
None - plan executed as written. The integration closed and played facts post a placeholder stamp because the quest is refused before the stamp is compared, which is the behavior under test.

## Known Stubs
None. `Changed` currently redirects to Details with a toast; the Move page re-render is owned by the plan that builds that page.

## Self-Check: PASSED
- Files and both commits confirmed present.
