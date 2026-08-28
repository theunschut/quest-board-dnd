---
phase: 76-recurring-event-series
plan: 01
subsystem: domain
tags: [dateonly, cycle-mask, recurrence, pure-domain-service, xunit, fluentassertions]

# Dependency graph
requires: []
provides:
  - "EventSeriesDateGenerator.GenerateSlots — the single cycle-mask date-math implementation for the recurring series feature"
  - "EventSeriesDateGenerator.TryParseMask/ParseMask/FormatMask — the server-side cycle-mask validator and round-trip formatter"
  - "EventSeriesDateGenerator.DateForSlot — the single slot-to-date arithmetic point"
affects: [76-recurring-event-series (all subsequent plans in this phase)]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Pure, dependency-free static Domain service (no DI, no interface, no clock read) — first of its kind in this codebase"
    - "Guard clauses in a non-iterator wrapper method around a private iterator, so ArgumentException/ArgumentOutOfRangeException throw at call time rather than at first enumeration"

key-files:
  created:
    - QuestBoard.Domain/Services/EventSeriesDateGenerator.cs
    - QuestBoard.UnitTests/Services/EventSeriesDateGeneratorTests.cs
  modified: []

key-decisions:
  - "Followed 76-CONTEXT.md D-01 exactly: date(N) = AnchorDate + (N x IntervalWeeks) weeks, fires(N) = mask[N mod mask.Length] — every slot is yielded whether or not it fires, so SeriesSlotIndex stays permanently stable."
  - "MaxCycleLength = 100 and MaxSlotScan = 10_000 implemented as hard ceilings per D-03 and the DoS threat in the plan's threat model (T-76-01), independent of any UI-level cap."
  - "TryParseMask rejects an all-zero mask (no '1' anywhere) — load-bearing per the plan, since a never-firing mask would make a future top-up job scan to MaxSlotScan every run."

patterns-established:
  - "Pure algorithm classes for this codebase live as public static classes with no constructor/interface/DI, guard clauses split into a non-iterator wrapper, and no clock or I/O access — future recurrence-adjacent logic should follow this shape rather than forcing a repository/service DI wrapper around it."

requirements-completed: [EVTRECUR-01, EVTRECUR-08]

coverage:
  - id: D1
    description: "EventSeriesDateGenerator.GenerateSlots implements the D-01 cycle-mask cadence arithmetic (slot index counts every step including non-firing ones, date = anchor + slot*interval*7 days), with end-date truncation and a hard MaxSlotScan iteration ceiling"
    requirement: "EVTRECUR-01"
    verification:
      - kind: unit
        ref: "QuestBoard.UnitTests/Services/EventSeriesDateGeneratorTests.cs#GenerateSlots_TwoOnTwoOffWeekly_ProducesExpectedDatesAndFiringSlots"
        status: pass
      - kind: unit
        ref: "QuestBoard.UnitTests/Services/EventSeriesDateGeneratorTests.cs#GenerateSlots_FortnightlySingleOnMask_EveryStepFiresAndDatesStepByFourteenDays"
        status: pass
      - kind: unit
        ref: "QuestBoard.UnitTests/Services/EventSeriesDateGeneratorTests.cs#GenerateSlots_EndDateSet_TruncatesSequenceWithNoDateAfterEndDate"
        status: pass
      - kind: unit
        ref: "QuestBoard.UnitTests/Services/EventSeriesDateGeneratorTests.cs#GenerateSlots_MaxSlotsLargerThanScanCeiling_YieldsAtMostMaxSlotScan"
        status: pass
    human_judgment: false
  - id: D2
    description: "Mirrored cycle masks (1,1,0,0 and 0,0,1,1) on the same anchor and interval share the full date grid but produce zero overlapping firing dates across 40 slots"
    requirement: "EVTRECUR-08"
    verification:
      - kind: unit
        ref: "QuestBoard.UnitTests/Services/EventSeriesDateGeneratorTests.cs#GenerateSlots_MirroredMasksOnSameAnchorAndInterval_ShareNoFiringDate"
        status: pass
    human_judgment: false
  - id: D3
    description: "TryParseMask/ParseMask/FormatMask validate and round-trip a comma-delimited cycle-mask string with a strict 0/1 token allowlist, a 100-position ceiling, and rejection of a mask with no firing position"
    verification:
      - kind: unit
        ref: "QuestBoard.UnitTests/Services/EventSeriesDateGeneratorTests.cs#TryParseMask_ValidMasks_ReturnsTrueWithNoError"
        status: pass
      - kind: unit
        ref: "QuestBoard.UnitTests/Services/EventSeriesDateGeneratorTests.cs#TryParseMask_InvalidMasks_ReturnsFalseWithError"
        status: pass
      - kind: unit
        ref: "QuestBoard.UnitTests/Services/EventSeriesDateGeneratorTests.cs#FormatMask_RoundTripsThroughParseMask"
        status: pass
    human_judgment: false

duration: 30min
completed: 2026-08-28
status: complete
---

# Phase 76 Plan 01: Pure Cycle-Mask Date Generator Summary

**`EventSeriesDateGenerator` static Domain class implementing the D-01 cycle-mask cadence arithmetic (slot-not-week stepping, non-firing slots still counted) plus strict server-side mask parsing/validation, proven by 21 dependency-free xUnit tests including the mirrored-mask non-collision proof.**

## Performance

- **Duration:** ~30 min
- **Started:** 2026-08-28T12:33:00Z (approx, from worktree HEAD assertion)
- **Completed:** 2026-08-28T13:03:36Z
- **Tasks:** 2
- **Files modified:** 2 (both new files)

## Accomplishments
- `EventSeriesDateGenerator.GenerateSlots` implements the exact D-01 formula — `date(N) = AnchorDate + (N x IntervalWeeks) weeks`, `fires(N) = mask[N mod mask.Length]` — with every slot yielded regardless of firing, an `endDate` truncation via `yield break`, and a hard `MaxSlotScan = 10_000` ceiling independent of caller-supplied `maxSlots`.
- `TryParseMask`/`ParseMask`/`FormatMask` give the create-path and top-up job a single, strict, server-side mask validator (`0`/`1` tokens only, 100-position ceiling matching the `nvarchar(200)` column, all-zero rejection) that round-trips to the exact storage format.
- 21 unit tests prove the two-on-two-off literal date/slot grid, the fortnightly single-on cadence, the anchor-weekday invariant across intervals 1/2/3, the mirrored-mask EVTRECUR-08 non-collision guarantee across 40 slots, end-date truncation, the `MaxSlotScan` ceiling, eager `ArgumentOutOfRangeException` on `intervalWeeks < 1`, and every `TryParseMask` accept/reject case — with zero database dependency (`UseInMemoryDatabase` count is 0).

## Task Commits

Each task was committed atomically:

1. **Task 1: Create the pure cycle-mask date generator with mask parsing and validation** - `54ae8e3` (feat)
2. **Task 2: Unit-test the generator's mask arithmetic, mirrored-mask non-collision, and mask validation** - `d1bac17` (test)

**Plan metadata:** committed alongside this SUMMARY (worktree mode — orchestrator finalizes the metadata commit after merge)

## Files Created/Modified
- `QuestBoard.Domain/Services/EventSeriesDateGenerator.cs` - Pure static Domain class: `MaxCycleLength`, `MaxSlotScan`, `DateForSlot`, `GenerateSlots`, `TryParseMask`, `ParseMask`, `FormatMask`
- `QuestBoard.UnitTests/Services/EventSeriesDateGeneratorTests.cs` - 21 xUnit facts/theories covering cadence arithmetic, mirrored-mask non-collision, and mask validation

## Decisions Made
- None beyond what 76-CONTEXT.md D-01/D-03 and the plan's `<action>` blocks already specified — implementation followed the plan's exact method signatures and validation error messages verbatim.

## Deviations from Plan

None - plan executed exactly as written. Both tasks' acceptance criteria (file existence, exact signature fragments, zero `DateTime.Today` references, zero GSD-reference strings, zero `UseInMemoryDatabase` references, `dotnet build`/`dotnet test` exit 0) were verified directly rather than assumed.

## TDD Gate Compliance

Task 2 carries `tdd="true"`, but the plan's own task ordering places the implementation (Task 1) before the test file (Task 2) — the generator already existed when the test file was authored, so this was not a classic RED-then-GREEN cycle at the task-commit level. This is a property of how the plan was written (type: execute, not type: tdd), not a deviation introduced during execution. One test (`DateForSlot_MatchesGenerateSlotsArithmetic`) initially had an incorrect expected value (arithmetic error in the test itself, not the generator) and was corrected before the commit — the corrected test passed against the already-correct generator without any generator changes.

## Issues Encountered

None blocking. One self-caught test-authoring arithmetic error (expected date for `DateForSlot(anchor, intervalWeeks: 2, slotIndex: 3)` — corrected from Oct 31 to the correct Oct 17 before committing).

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- `EventSeriesDateGenerator` is ready to be consumed by the materialization/orchestration layer (`EventSeriesService`, the create-path first generation pass, and the nightly top-up job) that later plans in this phase will build.
- No blockers. The generator's public surface (`GenerateSlots`, `DateForSlot`, `TryParseMask`, `ParseMask`, `FormatMask`, `MaxCycleLength`, `MaxSlotScan`) matches exactly what 76-PATTERNS.md's Pattern 1 and the phase's later plans expect to call.

---
*Phase: 76-recurring-event-series*
*Completed: 2026-08-28*
