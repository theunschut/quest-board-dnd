---
phase: 92-move-a-quest-or-event-to-a-linked-board
plan: 08
subsystem: board-move
tags: [move, details, razor, toast, mobile, super-admin]
requires:
  - phase: 92-01
    provides: BoardMoveSeed, MoveController
  - phase: 92-03
    provides: MoveEligibility, MoveCopy.ControlReason
  - phase: 92-04
    provides: TempDataKeys.MovedMessage / MovedEntryUrl / MovedTargetName
  - phase: 92-06
    provides: Event Move actions and the Moved toast helper
provides:
  - MoveControlState, IMoveControlService / MoveControlService
  - ViewBag.MoveControl on QuestController.Details and EventsController.Details
  - "Move to Linked Board" control on Quest Details (desktop and mobile) and Event Details
  - Moved toast block in _Toasts.cshtml
  - .move-control-reason mobile rule
affects: [92-07, 92-09]
tech-stack:
  added: []
  patterns:
    - "Controllers ask a small domain service for the control state, so guarded controllers gain no clock read and no repository dependency"
    - "Disabled, not hidden: a button type=button with aria-disabled and aria-describedby plus a reason paragraph"
key-files:
  created:
    - QuestBoard.Domain/Models/BoardMove/MoveControlState.cs
    - QuestBoard.Domain/Interfaces/IMoveControlService.cs
    - QuestBoard.Domain/Services/MoveControlService.cs
    - QuestBoard.UnitTests/Services/BoardMoveControlServiceTests.cs
    - QuestBoard.IntegrationTests/Tests/BoardMoveControlTests.cs
  modified:
    - QuestBoard.Domain/Extensions/ServiceExtensions.cs
    - QuestBoard.Service/Controllers/QuestBoard/QuestController.cs
    - QuestBoard.Service/Controllers/Events/EventsController.cs
    - QuestBoard.Service/Views/Quest/Details.cshtml
    - QuestBoard.Service/Views/Quest/Details.Mobile.cshtml
    - QuestBoard.Service/Views/Events/Details.cshtml
    - QuestBoard.Service/Views/Shared/_Toasts.cshtml
    - QuestBoard.Service/wwwroot/css/mobile.css
key-decisions:
  - "The Moved toast reuses the board-switch toast's CSS class for its opaque background and mobile offset instead of adding a rule to modern-card.css, which is outside this plan's files"
  - "On mobile Quest Details a disabled control drops its own mb-2 and the reason carries the spacing, so the gap between button and reason is not doubled"
patterns-established:
  - "Control state keyed on User.IsInRole(\"SuperAdmin\") in the controller, never on CanManage"
status: complete
actuals:
  tokens: 30000
  tasks: 2
  commits: 2
---

# Phase 92 Plan 08: Move control and toast Summary

A super admin on a linked board now finds "Move to Linked Board" on Quest Details (both layouts) and Event Details, enabled or disabled with a one-line reason, and a member-of-target move ends in a "Moved" toast that links straight to the entry on its new board.

## What was built

- `MoveControlService` returns null when there is no active board or the linked set read fresh on every call has no other board. Otherwise it returns `MoveControlState(MoveEligibility.ForQuest/ForEvent(entry, boardClock.Today))`. Registered in `AddDomainServices`.
- `QuestController.Details` and `EventsController.Details` set `ViewBag.MoveControl` only when `User.IsInRole("SuperAdmin")`. No other line of either action changed, `ViewBag.BoardToday` and `CanManage` are untouched, and neither controller gained an ambient clock read.
- The control markup in three views: an `a.btn.btn-info.w-100` with `fa-right-left` linking `/Move/Quest/{id}` or `/Move/Event/{id}`, or a `button type="button"` with `disabled`, `aria-disabled="true"` and `aria-describedby="move-reason"` followed by `p.move-control-reason#move-reason`. Null state renders nothing, not even a wrapper. Placements: desktop DM Controls card below Manage Quest (`mt-2`), mobile directly after Manage Quest, Events Actions card last item (`mt-2`).
- `_Toasts.cshtml`: a block shown when `MovedMessage` is set, with a `bg-success` header, `fa-check-circle`, the title "Moved", `data-bs-autohide="false"`, and a filled `btn btn-sm btn-dark` "Open it on {Board}" under the sentence. The link renders only when the entry URL is a string accepted by `Url.IsLocalUrl`. Every TempData value goes through Razor encoding. The body and button use `text-break` so long titles and board names wrap.
- `mobile.css`: `.move-control-reason` at 14px in `#D9CDA8`.

## Verification

- Unit: `BoardMoveControlServiceTests` (9 facts) plus `BoardMove`, `Architecture` and `AmbientClockSeamTests` filters, 200 passed. Full unit suite 1063 passed.
- Integration: `BoardMoveControlTests` 22 passed, covering desktop and mobile user agents, the four disabled reasons, no control on a board outside any set or in a set of one, no control for a board Admin or DM, direct GET refusal, member toast links for quest and event, non-member Success toast, and the encoded-title fact. The wider filter (QuestDetails, MobileCss, MobileViews, EventDetailsAvailabilityRender, LinkedBoardQuestDetails, BoardMoveApply, BoardMoveEvent, LinkedBoardStaticGuard, AntiForgeryTokenCoverage) 187 passed. Full integration suite 1200 passed, 11 skipped (SQL Server-only facts, pre-existing).
- No planning references in added source lines (count 0).

## Deviations from Plan

None - plan executed as written, with two small choices inside the plan's discretion:

- The toast carries the `board-switch-toast` class rather than a new one, to get the same opaque white background (which the filled dark button's contrast relies on) and the narrow-viewport offset without touching `modern-card.css`.
- On mobile Quest Details the disabled button has no `mb-2` and its reason is `mt-2 mb-2`, so the spacing before the next control is one gap, not two.

Process note: tests and implementation were committed together per task rather than as separate RED and GREEN commits.

## Not established

- Visual rendering. Wrapping in the narrow desktop column, the toast body wrap and the 14px/`#D9CDA8` reason colour are asserted by markup and CSS rule only; nothing was viewed in a browser.
- The desktop `.move-control-reason` size comes from the Move styling plan's stylesheet, which this plan does not touch; until that is linked on Quest and Event Details the desktop reason renders at the page's normal size.
- The harness uses the EF Core InMemory provider, so SQL translation of the set read is not exercised.

## Known Stubs

None.

## Threat Flags

None. The surface is the one the plan's threat model covers (T-92-31 to T-92-34): the control is set only for a SuperAdmin in the controller, TempData values are Razor-encoded (encoded-title fact), and the link is gated by `Url.IsLocalUrl`.

## Commits

- 4578a298: control state service wired into quest and event Details
- 91cea6c2: move control on Details pages and the Moved toast

## Self-Check: PASSED

All created files exist and both commits are on the branch.
