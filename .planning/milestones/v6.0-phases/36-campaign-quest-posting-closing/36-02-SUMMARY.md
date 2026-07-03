---
phase: 36-campaign-quest-posting-closing
plan: 02
subsystem: api
tags: [ef-core, nsubstitute, xunit, tdd, campaign-mode, quest-lifecycle]

# Dependency graph
requires:
  - phase: 36-campaign-quest-posting-closing (Plan 01)
    provides: "IsClosed (bool) + ClosedDate (DateTime?) fields on QuestEntity/Quest, migration, TestDataHelper campaign seeding"
provides:
  - "IQuestRepository.CloseQuestAsync / ReopenQuestAsync signatures"
  - "IQuestService.CloseQuestAsync / ReopenQuestAsync signatures"
  - "QuestRepository.CloseQuestAsync / ReopenQuestAsync implementations (mirror OpenQuestAsync, no PlayerSignups include)"
  - "QuestService.CloseQuestAsync / ReopenQuestAsync thin passthroughs with zero IQuestEmailDispatcher references"
  - "!IsClosed AND-ed onto GetQuestsWithSignupsAsync and GetQuestsWithSignupsForRoleAsync board filters"
  - "GetCompletedQuestsAsync OR-branch admitting closed quests immediately, ordered by ClosedDate when closed"
affects: [36-03, 36-04, 36-05]

# Tech tracking
tech-stack:
  added: []
  patterns: ["Thin service passthrough with deliberately absent dispatcher reference as the structural mechanism for zero-email guarantees"]

key-files:
  created: []
  modified:
    - QuestBoard.Domain/Interfaces/IQuestRepository.cs
    - QuestBoard.Domain/Interfaces/IQuestService.cs
    - QuestBoard.Repository/QuestRepository.cs
    - QuestBoard.Domain/Services/QuestService.cs
    - QuestBoard.UnitTests/Services/QuestServiceTests.cs

key-decisions:
  - "CloseQuestAsync/ReopenQuestAsync deliberately omit .Include(q => q.PlayerSignups) — campaign quests never have signups, so there's no deselection side effect to mirror from OpenQuestAsync"
  - "GetCompletedQuestsAsync orders by `q.IsClosed ? q.ClosedDate : q.FinalizedDate` so a just-closed quest sorts correctly instead of falling to the bottom via a null FinalizedDate"

patterns-established:
  - "Zero-email guarantee enforced structurally (no dispatcher field reference in method body) and verified via DidNotReceiveWithAnyArgs() unit tests — same pattern should be used for any future campaign-quest mutation that must never trigger email"

requirements-completed: [CQUEST-03, CQUEST-04, CQUEST-05, CQUEST-06]

# Metrics
duration: 12min
completed: 2026-07-03
---

# Phase 36 Plan 02: Campaign Quest Close/Reopen Business Logic Summary

**CloseQuestAsync/ReopenQuestAsync added to QuestService+QuestRepository as structurally email-free passthroughs, with IsClosed wired into both board-visibility filters and the Quest Log OR-branch, driven by 5 new unit tests written first**

## Performance

- **Duration:** ~12 min
- **Started:** 2026-07-03T13:51:00Z
- **Completed:** 2026-07-03T14:03:25Z
- **Tasks:** 2 completed
- **Files modified:** 5

## Accomplishments
- `IQuestRepository`/`IQuestService` both declare `CloseQuestAsync`/`ReopenQuestAsync` mirroring `OpenQuestAsync`'s exact signature and doc-comment style
- `QuestRepository.CloseQuestAsync` sets `IsClosed=true`/`ClosedDate=DateTime.UtcNow`; `ReopenQuestAsync` sets `IsClosed=false`/`ClosedDate=null` — neither includes the `PlayerSignups` navigation since campaign quests have no signups
- `QuestService.CloseQuestAsync`/`ReopenQuestAsync` are thin passthroughs containing zero references to `dispatcher` — the structural mechanism enforcing CQUEST-06 (no email on close/reopen)
- `GetQuestsWithSignupsAsync` and `GetQuestsWithSignupsForRoleAsync` both AND `!q.IsClosed` onto their existing predicate, hiding closed quests from the active board immediately
- `GetCompletedQuestsAsync` gains an OR-branch admitting `q.IsClosed` quests with no next-day wait, while the existing one-shot finalized-quest branch (and its next-day wait) is untouched; ordering now keys off `ClosedDate` for closed quests so they don't sort to the bottom as a null `FinalizedDate`
- 5 new unit tests (2 delegation/no-email, 3 `GetCompletedQuestsAsync` behavior) all green; full unit suite (123 tests) green

## Task Commits

Each task was committed atomically, following RED/GREEN TDD gates:

1. **Task 1: Add Close/Reopen unit tests + GetCompletedQuestsAsync OR-branch tests (RED)** - `7ab6c95` (test)
2. **Task 2: Implement Close/Reopen + IsClosed-aware filters (GREEN)** - `f5837cd` (feat)

## TDD Gate Compliance

- RED gate: `7ab6c95` — build failed with `CS1061` on `CloseQuestAsync`/`ReopenQuestAsync` not existing, exactly as expected (test-only commit, no implementation).
- GREEN gate: `f5837cd` — implementation added; `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~QuestServiceTests"` passes all 9 tests in that class (123/123 across the full unit suite).
- No REFACTOR commit was needed — the GREEN implementation matched the plan's target shape with no follow-up cleanup required.

## Files Created/Modified
- `QuestBoard.Domain/Interfaces/IQuestRepository.cs` - Added `CloseQuestAsync`/`ReopenQuestAsync` signatures
- `QuestBoard.Domain/Interfaces/IQuestService.cs` - Added `CloseQuestAsync`/`ReopenQuestAsync` signatures
- `QuestBoard.Repository/QuestRepository.cs` - Implemented `CloseQuestAsync`/`ReopenQuestAsync`; added `!q.IsClosed` to both board-filter queries
- `QuestBoard.Domain/Services/QuestService.cs` - Added `CloseQuestAsync`/`ReopenQuestAsync` passthroughs (no dispatcher reference); `GetCompletedQuestsAsync` predicate/ordering updated for the closed-quest OR-branch
- `QuestBoard.UnitTests/Services/QuestServiceTests.cs` - Added `CloseQuestAsync_DelegatesToRepository_AndSendsNoEmail`, `ReopenQuestAsync_DelegatesToRepository_AndSendsNoEmail`, `GetCompletedQuestsAsync_IncludesClosedCampaignQuest_WithNoNextDayWait`, `GetCompletedQuestsAsync_PreservesOneShotNextDayWait`, `GetCompletedQuestsAsync_OrdersClosedAndFinalizedQuestsTogether_ClosedNotSortedAsNull`

## Decisions Made
- Omitted `.Include(q => q.PlayerSignups)` from both new repository methods — campaign quests structurally never have player signups, so there's nothing to deselect (unlike `OpenQuestAsync`, which resets `IsSelected` on reopen)
- Changed `GetCompletedQuestsAsync`'s `OrderByDescending` key from a flat `q.FinalizedDate` to a conditional `q.IsClosed ? q.ClosedDate : q.FinalizedDate` so closed campaign quests (which never set `FinalizedDate`) sort correctly among finalized one-shot quests instead of being pushed to the bottom by a null comparison key — this wasn't explicitly spelled out as a code change in the plan's action text beyond "change the ordering," so this documents the exact expression chosen

## Deviations from Plan

None - plan executed exactly as written. The one addition beyond the plan's four named tests — a fifth test (`GetCompletedQuestsAsync_OrdersClosedAndFinalizedQuestsTogether_ClosedNotSortedAsNull`) — directly operationalizes a `must_haves.truths` requirement from the plan frontmatter ("a closed quest is not sorted to the bottom as a null date") that wasn't yet covered by a named test in the task's `<action>` text; not a deviation from scope, just filling in test coverage the plan's own truths already called for.

## Issues Encountered

None. RED build failure matched the expected `CS1061` errors on `CloseQuestAsync`/`ReopenQuestAsync` exactly; GREEN implementation passed all tests on the first attempt.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- `QuestService.CloseQuestAsync`/`ReopenQuestAsync` are ready for Plan 03 (controller actions + access control) to call
- Board filters and Quest Log are already IsClosed-aware, so Plan 03/04 views will show correct data once wired
- No blockers identified

---
*Phase: 36-campaign-quest-posting-closing*
*Completed: 2026-07-03*

## Self-Check: PASSED

All claimed files found on disk; both task commits (`7ab6c95`, `f5837cd`) verified present in git log.
