---
phase: 92-move-a-quest-or-event-to-a-linked-board
plan: 04
subsystem: board-move
tags: [move, razor, mobile, authorization, super-admin]
requires:
  - phase: 92-01
    provides: MoveController, BoardMoveSeed
  - phase: 92-03
    provides: PreviewAsync, stamp, MoveCopy
provides:
  - GET /Move/Quest/{id} Move page (desktop and mobile)
  - MovePageViewModel
  - ConfirmQuest Changed -> 200 re-render
  - TempDataKeys.MovedMessage / MovedEntryUrl / MovedTargetName
  - BoardMovePageTests, BoardMoveAuthorizationTests, BoardMoveTargetTests
affects: [92-05, 92-06, 92-07, 92-08]
tech-stack:
  added: []
  patterns:
    - "Picker is a plain GET form with a fragment; confirm is a plain POST carrying id, target and stamp"
key-files:
  created:
    - QuestBoard.Service/ViewModels/MoveViewModels/MovePageViewModel.cs
    - QuestBoard.Service/Views/Move/Index.cshtml
    - QuestBoard.Service/Views/Move/Index.Mobile.cshtml
    - QuestBoard.IntegrationTests/Tests/BoardMovePageTests.cs
    - QuestBoard.IntegrationTests/Tests/BoardMoveAuthorizationTests.cs
    - QuestBoard.IntegrationTests/Tests/BoardMoveTargetTests.cs
  modified:
    - QuestBoard.Service/Controllers/MoveController.cs
    - QuestBoard.Service/Constants/TempDataKeys.cs
    - QuestBoard.IntegrationTests/Tests/BoardMoveApplyTests.cs
key-decisions:
  - "A tampered target with exactly one linked board still shows the picker so the viewer can choose from the list"
  - "Changed renders as Ready plus the warning notice, with the fresh stamp"
status: complete
actuals:
  tokens: 26000
  tasks: 2
  commits: 2
---

# Phase 92 Plan 04: Move page Summary

A super admin can open `/Move/Quest/{id}`, pick or see the target board, read what is kept and who is mailed, and confirm; a stale preview re-shows the page (HTTP 200, fresh stamp) instead of moving.

## What was built
- `MoveController.Quest` GET: NotFound, Blocked and NoLinkedBoards redirect to Details with the specified Error toasts; every other status renders the page.
- `MovePageViewModel` with the picker, preview and confirm visibility rules and the kept and email sentences.
- `Views/Move/Index.cshtml` and `Index.Mobile.cshtml`: summary, intro, picker or "Moving to" line, preview section with kept and emails rows, DM-refusal alert, changed notice, footer with Back and the confirm form (rendered only when a target is ready). Razor encoding only; dates only through `Html.WallClock`. No stylesheet linked yet.
- `ConfirmQuest`: `Changed` re-renders the page; a successful move by an actor who is a member of the target sets the three Moved TempData keys, otherwise the standard Success toast.
- Tests: 14 page facts, authorization (Admin and DM of both boards, Player, anonymous; GET and POST; GroupId asserted unchanged), target facts (stale stamp 200 re-render, outside and self target, no set and set of one, non-member Success toast).

## Verification
- Integration filter (BoardMoveAuthorization, BoardMoveTarget, BoardMovePage, BoardMoveApply, AntiForgeryTokenCoverage, CrossBoard): 96 passed.
- Task 1 filter plus MobileViewLocationExpander: 27 passed; Architecture unit tests: 72 passed.
- No planning references in added source lines (count 0).

## Deviations from Plan

**1. [Rule 3 - Blocking] Updated the stale-stamp fact in BoardMoveApplyTests**
- **Issue:** the fact asserted a redirect for a stale stamp; the stale stamp now returns 200 by design.
- **Fix:** removed the status assertion so it asserts only the stored state, as the plan's acceptance text describes.
- **Files modified:** QuestBoard.IntegrationTests/Tests/BoardMoveApplyTests.cs
- **Commit:** b2e53cc7

**2. [Rule 2 - Missing functionality] Picker shown for a tampered target with one linked board**
- The plan's picker rule (more than one board) would leave a viewer with a tampered target and one linked board with no way to choose; the picker also shows in the TargetNotLinked state.

## Known Stubs
None. The member-link toast data is set but its markup arrives with the shared toast block in a later plan, as recorded in the plan; until then a member actor sees no toast. The `move-*` classes are intentionally unstyled until the styling plan.

## Threat Flags
None.

## Commits
- e5a31dd8 feat(92-04): add Move page preview with desktop and mobile views
- b2e53cc7 feat(92-04): re-show the Move page on change and add authorization and target proofs

## Self-Check: PASSED
All created files exist and both commits are on the branch.
