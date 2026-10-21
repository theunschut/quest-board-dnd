---
phase: 93-per-board-page-selection-and-home-page
plan: 09
subsystem: board-move
tags: [aspnet-mvc, board-pages, board-move, razor, xunit]

requires:
  - phase: 93-per-board-page-selection-and-home-page
    provides: "BoardPagePolicy.IsAvailable, LinkedBoard.DisabledPages (plan 05), [BoardPage] gates on Move actions (plan 02)"
provides:
  - "MoveStatus.TargetPageOff refusal in both move services, on preview and confirm"
  - "MoveControlService offers the control only when a linked board can take that kind of entry"
  - "Move controller GET redirects and POST messages for the refusal"
  - "Move picker shows a board that cannot take the entry as a disabled option with a suffix, on both layouts"
affects: [93-11, 93-12]

actuals:
  tokens: 14000
  tasks: 2
  commits: 3

tech-stack:
  added: []
  patterns:
    - "Refuse right after the target resolves, before eligibility/roster work, so no write path is reached"
    - "Picker disables rather than hides an unavailable board so the super admin sees why"

key-files:
  created:
    - QuestBoard.UnitTests/Services/BoardMoveTargetPageOffTests.cs
    - QuestBoard.IntegrationTests/Tests/BoardPageMoveTests.cs
  modified:
    - QuestBoard.Domain/Enums/MoveStatus.cs
    - QuestBoard.Domain/Services/QuestMoveService.cs
    - QuestBoard.Domain/Services/EventMoveService.cs
    - QuestBoard.Domain/Services/MoveControlService.cs
    - QuestBoard.Service/Controllers/MoveController.cs
    - QuestBoard.Service/ViewModels/MoveViewModels/MovePageViewModel.cs
    - QuestBoard.Service/Views/Move/Index.cshtml
    - QuestBoard.Service/Views/Move/Index.Mobile.cshtml

key-decisions:
  - "Refusal sits immediately after preview.Target is set, so DM eligibility, roster and plan work never run for an off target"
  - "MoveAsync needed no extra guard: it already applies only a Ready preview"
  - "An only-linked-board that is off also refuses when no target id is posted, because the single board is auto-selected"

patterns-established:
  - "Move refusals are fixed Error toasts redirecting to the entry's Details page; GET and POST use different wording"

requirements-completed: [D-04, D-08, D-09]

duration: 20min
completed: 2026-10-21
status: complete
plan_head_before: 7c452094929a9d1e64472f83d498955cba48d94e

coverage:
  - id: D1
    description: "Moving an event or quest onto a linked board whose Events/Quests page is off is refused with TargetPageOff on preview and confirm; nothing is written"
    requirement: "D-08"
    verification:
      - kind: unit
        ref: "QuestBoard.UnitTests/Services/BoardMoveTargetPageOffTests.cs#EventMove_TargetWithEventsOff_IsRefusedAndNothingIsApplied"
        status: pass
      - kind: unit
        ref: "QuestBoard.UnitTests/Services/BoardMoveTargetPageOffTests.cs#QuestMove_TargetWithQuestsOff_IsRefusedAndNothingIsApplied"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Tests/BoardPageMoveTests.cs#EventConfirm_TargetWithEventsOff_IsRefusedAndTheEventStaysPut"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Tests/BoardPageMoveTests.cs#QuestConfirm_TargetWithQuestsOff_IsRefusedAndTheQuestStaysPut"
        status: pass
    human_judgment: false
  - id: D2
    description: "The Move GET redirects to Details with the reason instead of rendering an unhandled state"
    requirement: "D-08"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Tests/BoardPageMoveTests.cs#EventPreview_TargetWithEventsOff_RedirectsToDetailsWithTheReason"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Tests/BoardPageMoveTests.cs#QuestPreview_TargetWithQuestsOff_RedirectsToDetailsWithTheReason"
        status: pass
    human_judgment: false
  - id: D3
    description: "The picker renders a board that cannot take the entry as a disabled option with the suffix, on desktop and mobile"
    requirement: "D-09"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Tests/BoardPageMoveTests.cs#EventPicker_BoardWithEventsOff_IsADisabledOptionWithTheSuffix"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Tests/BoardPageMoveTests.cs#QuestPicker_BoardWithQuestsOff_IsADisabledOptionWithTheSuffix"
        status: pass
    human_judgment: true
    rationale: "Long board name plus suffix inside a native select at 390px needs a visual check (UI contract backstop)"
  - id: D4
    description: "The Move to Linked Board control appears only when some linked board can take the kind of entry"
    requirement: "D-04"
    verification:
      - kind: unit
        ref: "QuestBoard.UnitTests/Services/BoardMoveTargetPageOffTests.cs#EventControl_OnlyLinkedBoardWithEventsOff_IsNull"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Tests/BoardPageMoveTests.cs#EventDetails_OnlyLinkedBoardWithEventsOff_ShowsNoMoveControl"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Tests/BoardPageMoveTests.cs#QuestDetails_OnlyLinkedBoardWithQuestsOff_ShowsNoMoveControl"
        status: pass
    human_judgment: false
---

# Phase 93 Plan 09: Move refusals for boards that do not use the entry kind Summary

**A quest or event can no longer be moved onto a linked board whose Quests or Events page is off: both move services refuse with `MoveStatus.TargetPageOff` before any write, the Move pages explain it, the picker disables such boards, and the move control needs a board that can take the entry.**

## Performance

- **Duration:** about 20 min
- **Tasks:** 2 (Task 1 TDD: RED then GREEN; Task 2 implementation plus HTTP tests)
- **Files modified:** 10 (2 new test files)

## Accomplishments
- `MoveStatus.TargetPageOff` added; `QuestMoveService` and `EventMoveService` refuse right after the target resolves (checked against `BoardPagePolicy.IsAvailable` with `BoardPage.Quests`/`BoardPage.Events`), so DM eligibility, roster and plan work never run and `ApplyAsync` is never reached. This also covers the case where the single linked board is auto-selected.
- `MoveControlService.HasLinkedBoardTakingAsync` replaces the any-linked-board check, so a board whose only partner has the page off shows no move control.
- `MoveController`: explicit `TargetPageOff` case on both GETs (Error toast, redirect to Details) and a message arm on both POSTs, with the wording from the UI contract.
- `MovePageViewModel.CanTake` / `NotUsedThereSuffix`; both Move/Index twins render an unavailable board as `<option disabled>` with the suffix, never selected.

## Task Commits

1. **Task 1 RED: failing tests** - `70509744` (test) - also adds the enum value so the tests fail on assertions (7 failing, 5 not-refused guards passing), not on compilation
2. **Task 1 GREEN: service refusals and control rule** - `6bf4c82e` (feat)
3. **Task 2: controller, view model, both picker views, HTTP tests** - `7adc581a` (feat)

**Plan metadata:** committed with this SUMMARY (docs).

## Decisions Made
- Refusal placed immediately after `preview.Target = target` as recorded in the plan; `MoveAsync` already applies only a `Ready` preview, so no extra guard was added.
- The picker view reads the board name inside a code block (`if` without `@`) to stay valid Razor inside the `foreach`.

## Deviations from Plan

None - plan executed exactly as written.

One process note: the Task 2 HTTP tests were written after the controller and view changes rather than before, so only Task 1 has a recorded RED step. The new HTTP tests assert exact markup (disabled option, suffix, Error toast wording, GroupId unchanged) and all pass.

## TDD Gate Compliance

Task 1: RED commit `70509744` (`test(93-09)`) precedes GREEN commit `6bf4c82e` (`feat(93-09)`). RED evidence: 7 of 12 `BoardMoveTargetPageOffTests` failed on assertions for the planned behaviour (status was Ready/Changed instead of TargetPageOff; control state non-null); the 5 passing tests are the "unaffected" guards. No REFACTOR commit.

## Issues Encountered
- `MigratorCliTests.Status_WhenServerIsUnreachable_ReturnsCannotConnectWithoutLeakingConnectionDetails` failed once in the full unit run: the known intermittent pre-existing failure already logged in deferred-items.md; unrelated to this plan.

## Verification
- `dotnet build`: succeeded.
- `QuestBoard.UnitTests` filtered `BoardMove|Architecture|AmbientClock`: 254 passed.
- `QuestBoard.IntegrationTests` filtered `BoardPageMove|BoardMove|AntiForgeryTokenCoverage|BoardPageGate|AmbientClock`: 236 passed (includes `BoardMoveEndToEndTests`, `BoardMoveControlTests`).
- Full `QuestBoard.UnitTests`: 1280 passed, 1 known intermittent failure (above).
- No added source line carries a planning reference (grep prints 0).

## Known Stubs

None.

## Threat Flags

None. Mitigations T-93-34 (refusal from a fresh linked-set read before any write; unit tests assert `ApplyAsync` is never called, HTTP tests assert `GroupId` unchanged) and T-93-36 (explicit `TargetPageOff` GET case, tested) are in place; T-93-35 is unchanged (class-level `SuperAdminOnly` and page gates).

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- Move behaviour is complete for the page switches. A visual check of the picker option at 390px with a long board name plus suffix remains a human backstop.

## Self-Check: PASSED

- Created files exist: `BoardMoveTargetPageOffTests.cs`, `BoardPageMoveTests.cs`.
- Commits `70509744`, `6bf4c82e`, `7adc581a` exist on `worktree-agent-p93-09-1792585981`.

---
*Phase: 93-per-board-page-selection-and-home-page*
*Completed: 2026-10-21*
