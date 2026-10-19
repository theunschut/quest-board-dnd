---
phase: quick-261019-gff
plan: 01
subsystem: quest-manage-ui
tags: [board-move, razor, mobile, integration-tests]
requires:
  - Move control service and Move page already shipped for the Details pages
provides:
  - Move to Linked Board control on Quest Manage, desktop and mobile
affects:
  - QuestBoard.Service/Controllers/QuestBoard/QuestController.cs
  - QuestBoard.Service/Views/Quest/Manage.cshtml
  - QuestBoard.Service/Views/Quest/Manage.Mobile.cshtml
tech-stack:
  added: []
  patterns: [duplicate-consistently markup instead of shared partial]
key-files:
  created: []
  modified:
    - QuestBoard.Service/Controllers/QuestBoard/QuestController.cs
    - QuestBoard.Service/Views/Quest/Manage.cshtml
    - QuestBoard.Service/Views/Quest/Manage.Mobile.cshtml
    - QuestBoard.Service/wwwroot/css/mobile.css
    - QuestBoard.IntegrationTests/Tests/BoardMoveControlTests.cs
    - QuestBoard.IntegrationTests/Tests/BoardMoveStaticGuardTests.cs
decisions:
  - "One control block per layout, placed directly beneath whichever action row the quest state renders, not inside each row"
  - "No shared partial; markup duplicated per view, eligibility and reason text stay single-sourced"
  - "Control keyed on the SuperAdmin role itself, never on board-level authorization"
metrics:
  tasks: 2
  commits: 2
  tests_added: 22
status: complete
actuals:
  tokens: 14000
  tasks: 2
  commits: 2
---

# Quick 261019-gff: Move to Linked Board on Quest Manage Summary

A platform super admin can start a quest move from Quest Manage in both layouts, with the same enabled-link / disabled-button-plus-reason behaviour Quest Details already has; nobody else sees it.

## What was built

- `QuestController.Manage` now takes a `CancellationToken`, and sets `ViewBag.MoveControl` only under `User.IsInRole("SuperAdmin")` via `moveControlService.GetForQuestAsync(quest, currentUser.Id, token)`. No clock read was added; the service reads the board clock itself.
- `Manage.cshtml` (desktop) and `Manage.Mobile.cshtml` render the control once, inside an `@if (moveControl != null)` so a null state renders nothing:
  - can move: enabled `btn btn-info` link to `/Move/Quest/{id}`;
  - cannot move: `button type="button"` with `aria-disabled="true"` and `aria-describedby="move-reason"`, followed by `p.move-control-reason#move-reason` carrying `MoveCopy.ControlReason`.
  - Desktop is auto-width; mobile is `w-100` to match the full-width stacks.
- `mobile.css` comment now says Details or Manage page (no rule values changed).
- `BoardMoveControlTests` gained 22 Manage cases (8 enabled across open / no-dates / finalized / campaign, 6 disabled across closed / closed-campaign / played, 4 off-set, 4 board-role), each under desktop and mobile user agents. `SeedScenarioAsync` gained a defaulted `boardType` parameter.
- `BoardMoveStaticGuardTests.FeatureSourceFiles` now scans both Manage views.

## Placement decision

Manage has four mutually exclusive action areas per layout (open with no proposed dates, open with proposed dates / Finalize row, finalized, campaign). The control sits as one block at the end of the container, directly beneath whichever action row rendered, rather than inside each row. That needs two copies instead of eight, avoids overflowing the non-wrapping desktop button groups (finalized state already holds four buttons plus Refresh Data), gives the disabled reason a line of its own, and keeps it out of the Finalize form. Mobile `mt-2` equals the stacks' `gap-2`, so it reads as one more button.

## Tracer approval

Task 1 was the tracer (desktop Manage slice, commit a9ab89b0). The run was interactive, so it stopped after the tracer for a human check; the user approved ("Approve, continue") and Task 2 proceeded.

## Verification

- `dotnet build`: 0 errors.
- `BoardMoveControlTests`: 44 passed (22 pre-existing + 22 new).
- Wider integration filter (BoardMove, QuestDetails, QuestFinalizedEdit, QuestCampaignUiParity, QuestStatusBoardClock, WallClockUnmoved, MobileViews, MobileCss): 329 passed.
- `AmbientClockSeamTests`: passed (63 in the filtered run during Task 1).
- Full unit suite: 1103 passed. Full integration suite: 1318 passed, 11 skipped (SQL Server-only), 0 failed.
- Details.cshtml, Details.Mobile.cshtml and Events/Details.cshtml unchanged.
- Planning-reference grep over the touched source files prints nothing.
- Not established: visual rendering. Spacing and colours are asserted by markup and existing CSS only; nothing was viewed in a browser.

## Deviations from Plan

None - plan executed exactly as written. The tracer human-verify gate paused the run once between tasks.

## Known Stubs

None.

## Threat Flags

None. The control is a link or inert button; MoveController still re-checks role and eligibility server-side.

## Commits

- a9ab89b0 feat(261019-gff): add Move to Linked Board control to desktop Quest Manage
- ea699408 feat(261019-gff): add Move to Linked Board control to mobile Quest Manage

## Self-Check: PASSED
