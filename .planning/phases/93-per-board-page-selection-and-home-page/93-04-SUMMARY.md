---
phase: 93-per-board-page-selection-and-home-page
plan: 04
subsystem: domain
tags: [board-settings, domain-service, ef-core, query-filter-bypass, tdd]

requires:
  - phase: 93-01
    provides: BoardPage, BoardPageSet, BoardPagePolicy and the Group.DisabledPages / Group.HomePage fields
provides:
  - IBoardPageSettingsService.EvaluateAsync, the save-time rules for a board's page switches and home page
  - BoardPageSettingsRequest and BoardPageSettingsOutcome records
  - IEventRepository.CountUpcomingForBoardAsync and CountActiveSeriesForBoardAsync with UpcomingEventCount
affects: [93-06, 93-10]

actuals:
  tokens: 21000
  tasks: 2
  commits: 4
plan_head_before: ea940b94de16c837b9aa72134023388d4f4b72c2

tech-stack:
  added: []
  patterns:
    - "Evaluate-then-save: the service returns the resulting mask, home and refusals and writes nothing"
    - "Cross-board count through IgnoreQueryFilters plus an explicit GroupId, kept inside an already-allowlisted repository file"

key-files:
  created:
    - QuestBoard.Domain/Interfaces/IBoardPageSettingsService.cs
    - QuestBoard.Domain/Services/BoardPageSettingsService.cs
    - QuestBoard.UnitTests/Services/BoardPageSettingsServiceTests.cs
    - QuestBoard.UnitTests/Repository/BoardPageCountQueryTests.cs
  modified:
    - QuestBoard.Domain/Interfaces/IEventRepository.cs
    - QuestBoard.Repository/EventRepository.cs
    - QuestBoard.Domain/Extensions/ServiceExtensions.cs
    - QuestBoard.UnitTests/Architecture/AmbientClockSeamTests.cs

key-decisions:
  - "A posted home equal to the stored home is treated as unchanged, so switching that page off reports the 'is this board's home page' refusal rather than the 'can't be the home page' one"
  - "A single running series gets a singular refusal sentence ('1 recurring series still produces ... End the series first.'); the plural form matches the planned wording"
  - "Events refusals call the page by BoardPagePolicy.Label rather than a literal"

patterns-established:
  - "Settings rules live in one internal Domain service reading the date only through IBoardClock, listed on both AmbientClockSeamTests lists"

requirements-completed: [D-01, D-08, D-09, D-10, D-13, D-15, D-19]

coverage:
  - id: D1
    description: "Posted switches for type-disallowed, non-switchable or undefined pages are ignored and the stored bit is kept; absent pages and a null home are unchanged"
    requirement: "D-01"
    verification:
      - kind: unit
        ref: "QuestBoard.UnitTests/Services/BoardPageSettingsServiceTests.cs#Campaign_PostedSwitchesForTypeDisallowedPages_AreIgnored"
        status: pass
      - kind: unit
        ref: "QuestBoard.UnitTests/Services/BoardPageSettingsServiceTests.cs#PageMissingFromTheRequest_KeepsItsStoredBit"
        status: pass
      - kind: unit
        ref: "QuestBoard.UnitTests/Services/BoardPageSettingsServiceTests.cs#UndefinedPageValue_IsIgnored"
        status: pass
    human_judgment: false
  - id: D2
    description: "A Quest Log switch is ignored while Quests is unavailable"
    requirement: "D-19"
    verification:
      - kind: unit
        ref: "QuestBoard.UnitTests/Services/BoardPageSettingsServiceTests.cs#QuestLogSwitchOn_WhileQuestsIsUnavailable_KeepsTheStoredQuestLogBit"
        status: pass
    human_judgment: false
  - id: D3
    description: "A requested home must be an available home candidate; DM profile is refused"
    requirement: "D-10"
    verification:
      - kind: unit
        ref: "QuestBoard.UnitTests/Services/BoardPageSettingsServiceTests.cs#HomePageDungeonMasterProfile_IsRefused"
        status: pass
      - kind: unit
        ref: "QuestBoard.UnitTests/Services/BoardPageSettingsServiceTests.cs#HomePageShop_OnCampaign_IsRefusedAsUnavailable"
        status: pass
    human_judgment: false
  - id: D4
    description: "Switching off the stored home with the home unchanged is refused; moving the home in the same request is accepted"
    requirement: "D-13"
    verification:
      - kind: unit
        ref: "QuestBoard.UnitTests/Services/BoardPageSettingsServiceTests.cs#SwitchingOffTheStoredHome_WithHomeUnchanged_IsRefused"
        status: pass
      - kind: unit
        ref: "QuestBoard.UnitTests/Services/BoardPageSettingsServiceTests.cs#SwitchingOffTheStoredHome_WhileMovingTheHome_IsAccepted"
        status: pass
    human_judgment: false
  - id: D5
    description: "Events cannot be switched off while events dated today or later (cancelled included) or running series exist, counted only on the on-to-off transition from the board clock's today for the edited board"
    requirement: "D-08"
    verification:
      - kind: unit
        ref: "QuestBoard.UnitTests/Services/BoardPageSettingsServiceTests.cs#SwitchingEventsOff_WithUpcomingAndCancelledEvents_IsRefusedWithBothCounts"
        status: pass
      - kind: unit
        ref: "QuestBoard.UnitTests/Services/BoardPageSettingsServiceTests.cs#EventsAlreadyOff_NoCountIsTaken"
        status: pass
      - kind: unit
        ref: "QuestBoard.UnitTests/Services/BoardPageSettingsServiceTests.cs#Counts_UseTheEditedBoardsIdAndTheBoardClockToday"
        status: pass
      - kind: unit
        ref: "QuestBoard.UnitTests/Repository/BoardPageCountQueryTests.cs#CountUpcomingForBoard_TodayOnwardsIncludingCancelled_ExcludesPastAndOtherBoards"
        status: pass
      - kind: unit
        ref: "QuestBoard.UnitTests/Repository/BoardPageCountQueryTests.cs#CountActiveSeriesForBoard_OpenEndedOrEndingTodayOrLater_ExcludesEndedAndOtherBoards"
        status: pass
    human_judgment: false
  - id: D6
    description: "Evaluation writes nothing"
    requirement: "D-09"
    verification:
      - kind: unit
        ref: "QuestBoard.UnitTests/Services/BoardPageSettingsServiceTests.cs#Evaluate_WritesNothing"
        status: pass
    human_judgment: false

duration: 15min
completed: 2026-10-21
status: complete
---

# Phase 93 Plan 04: Board page settings evaluation Summary

**One Domain service decides what a Platform save may change on a board's pages and home page, backed by two cross-board repository counts that stop Events being switched off while upcoming events or running series exist.**

## Performance

- **Duration:** 15 min
- **Tasks:** 2
- **Files modified:** 8

## Accomplishments
- `CountUpcomingForBoardAsync` and `CountActiveSeriesForBoardAsync` read another board's rows through `IgnoreQueryFilters()` pinned to an explicit group id, returning scalars only, inside the already-allowed `EventRepository.cs` (the seam allowlist is unchanged).
- `BoardPageSettingsService.EvaluateAsync` ignores type-disallowed, non-switchable and undefined switches, keeps absent pages as stored, ties the Quest Log switch to the quest list, keeps the home on an available home candidate, and refuses switching Events off with the exact refusal sentences. It writes nothing.
- The service reads the date only through `IBoardClock` and is on both `AmbientClockSeamTests` lists; it is registered in `AddDomainServices`.

## Task Commits

1. **Task 1 RED: failing count tests** - `9254e9e1` (test)
2. **Task 1 GREEN: counts implemented** - `10628ed7` (feat)
3. **Task 2 RED: failing settings service tests** - `24d06d7d` (test)
4. **Task 2 GREEN: evaluation implemented, seam lists updated** - `3b37f56f` (feat)

## Files Created/Modified
- `QuestBoard.Domain/Interfaces/IBoardPageSettingsService.cs` - service contract with `BoardPageSettingsRequest` and `BoardPageSettingsOutcome`
- `QuestBoard.Domain/Services/BoardPageSettingsService.cs` - the save-time rules
- `QuestBoard.Domain/Interfaces/IEventRepository.cs` - two count methods and `UpcomingEventCount`
- `QuestBoard.Repository/EventRepository.cs` - the two counts
- `QuestBoard.Domain/Extensions/ServiceExtensions.cs` - registration
- `QuestBoard.UnitTests/Architecture/AmbientClockSeamTests.cs` - service added to the guarded and consumer lists
- `QuestBoard.UnitTests/Services/BoardPageSettingsServiceTests.cs`, `QuestBoard.UnitTests/Repository/BoardPageCountQueryTests.cs` - 27 and 3 facts

## Decisions Made
- A posted home equal to the stored one counts as unchanged, so the 'is this board's home page; pick another home page first' refusal applies when that page goes off. The 'can't be the home page because it isn't available' refusal applies to a home that differs from the stored one.
- One running series uses a singular sentence; the plural matches the planned wording exactly.

## Deviations from Plan

None - plan executed exactly as written.

## TDD Gate Compliance

Both tasks followed RED then GREEN. A throwing placeholder implementation (`NotImplementedException`) let the tests compile and fail on the missing behaviour: 3 of 3 count tests and 27 of 27 settings tests failed before implementation and passed after. `test(93-04)` precedes `feat(93-04)` for each task. No refactor commit was needed.

## Issues Encountered
- Full `dotnet test` showed one failure, `MigratorBackupAndApplyCliTests.StateChangingCommand_WhenServerIsUnreachable...(apply)`, the intermittent Migrator test already recorded as pre-existing and unrelated by the previous plan. Unit tests otherwise 1138 passed; integration tests 1333 passed, 11 skipped.

## Known Stubs

None.

## Threat Flags

None. The new reads are the planned counts, scoped by explicit group id inside the allowlisted repository file.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- Plan 06 can call `IBoardPageSettingsService.EvaluateAsync` from the Platform edit action and save only when the outcome is valid.
- Plan 10 extends the same service with the live-quest refusal and makes Quests switchable.

## Self-Check: PASSED

- Created files exist on disk (interface, service, both test files).
- Commits `9254e9e1`, `10628ed7`, `24d06d7d` and `3b37f56f` exist; `git rev-list --count ea940b94..HEAD` was 4 before this summary.
- `dotnet build` succeeds; `BoardPageSettingsService`, `BoardPageCountQuery` and Architecture tests pass (125); `CrossBoardIgnoreQueryFiltersSeamTests` and `AmbientClockSeamTests` pass with the allowlist unchanged.
- No added source line carries a planning reference (grep count 0).

---
*Phase: 93-per-board-page-selection-and-home-page*
*Completed: 2026-10-21*
