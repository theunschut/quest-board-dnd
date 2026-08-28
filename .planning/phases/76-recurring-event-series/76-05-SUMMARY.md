---
phase: 76-recurring-event-series
plan: 05
subsystem: infra
tags: [hangfire, background-job, recurring-job, tenant-isolation, xunit, nsubstitute, fluentassertions]

# Dependency graph
requires:
  - phase: 76-04
    provides: "IEventSeriesService.TopUpAsync (single idempotent materializer) and GetActiveSeriesForActiveGroupAsync"
provides:
  - "RecurringOccurrenceTopUpJob — nightly Hangfire recurring job (03:00 server local, id `recurring-occurrence-top-up`) that tops every active series on every board back up to its runway with no manual re-extension"
  - "Corrected IActiveGroupContext/ActiveGroupContextService doc comments stating a null active group yields zero rows, not every board's rows"
  - "RecurringOccurrenceTopUpJobTests — unit proof that the job sets a real per-board group id before every write and degrades one-board-at-a-time on failure"
affects: [76-recurring-event-series (calendar/runway-banner plans still to come in this phase)]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "A writing cross-board Hangfire job enumerates the tenant boundary table (groups) with exactly one null-group scope call, then opens a fresh HangfireJobHelper.RunInScopeAsync(scopeFactory, board.Id, ...) scope per board — this is now the second job in the codebase (after DailyReminderJob's read-only cross-group sweep) but the first one that writes, so the per-board scope is a correctness requirement, not a stylistic choice"
    - "Per-board failure isolation: a try/catch around each board's scope call logs and continues, then a single aggregate InvalidOperationException is thrown after the loop if any board failed, so Hangfire's global 5-attempt retry filter still applies to the sweep as a whole while a bad board never starves the others"

key-files:
  created:
    - QuestBoard.Service/Jobs/RecurringOccurrenceTopUpJob.cs
    - QuestBoard.UnitTests/Services/RecurringOccurrenceTopUpJobTests.cs
  modified:
    - QuestBoard.Service/Program.cs
    - QuestBoard.Service/Services/ActiveGroupContextService.cs
    - QuestBoard.Domain/Interfaces/IActiveGroupContext.cs

key-decisions:
  - "The job is registered but not added to the DI container, matching the existing DailyReminderJob precedent — Hangfire's ASP.NET Core job activator constructs an unregistered concrete type from the scope's services, and registering one recurring job type but not the other would be an inconsistency with no benefit."
  - "The board enumeration call is the one and only null-group scope in the file; every other repository call happens inside a real per-board scope. This is enforced both by a grep acceptance criterion and by a direct unit-test assertion on the group id present during enumeration."
  - "Test 3 (throwing board) and the aggregate-throw fact are proven together: the job must both continue past a failing board (captured group ids include all three boards, in order) and still surface a failure afterward (the retry policy applies), rather than swallowing the error or aborting early."

patterns-established:
  - "For unit-testing a Hangfire job that calls HangfireJobHelper.RunInScopeAsync with a real (non-null) group id, the mocked IServiceProvider must resolve the concrete ActiveGroupContextService type (not the IActiveGroupContext interface), constructed over a substituted IHttpContextAccessor — SetGroupId calls are then observed by reading ActiveGroupContextService.ActiveGroupId from inside a substituted downstream call's Returns callback, rather than by asserting scope-creation counts alone."

requirements-completed: [EVTRECUR-03, EVTRECUR-07]

coverage:
  - id: D1
    description: "RecurringOccurrenceTopUpJob enumerates real boards via IGroupRepository.GetAllWithMemberCountAsync (one null-group scope), then opens one real-group-id scope per board and calls IEventSeriesService.TopUpAsync for every active series — the same materializer the controller path calls, so there is one implementation of runway top-up"
    requirement: "EVTRECUR-03"
    verification:
      - kind: unit
        ref: "QuestBoard.UnitTests/Services/RecurringOccurrenceTopUpJobTests.cs#ExecuteAsync_WithThreeBoards_SetsGroupContextOncePerBoardWithRealIds"
        status: pass
      - kind: unit
        ref: "QuestBoard.UnitTests/Services/RecurringOccurrenceTopUpJobTests.cs#ExecuteAsync_WithThreeBoardsEachTwoActiveSeries_InvokesTopUpAsyncOncePerSeriesPerBoard"
        status: pass
      - kind: unit
        ref: "QuestBoard.UnitTests/Services/RecurringOccurrenceTopUpJobTests.cs#ExecuteAsync_EnumeratesBoardsExactlyOnceWithNoGroupSelected"
        status: pass
    human_judgment: false
  - id: D2
    description: "A single board's scope failure is logged and does not prevent the remaining boards from being processed, but the sweep still throws afterward so Hangfire's global retry filter applies — a retry is additive because every write is the slot-keyed idempotent TopUpAsync"
    requirement: "EVTRECUR-07"
    verification:
      - kind: unit
        ref: "QuestBoard.UnitTests/Services/RecurringOccurrenceTopUpJobTests.cs#ExecuteAsync_WhenOneBoardScopeThrows_ProcessesRemainingBoardsAndStillThrowsAfterward"
        status: pass
      - kind: unit
        ref: "QuestBoard.UnitTests/Services/RecurringOccurrenceTopUpJobTests.cs#ExecuteAsync_WithZeroBoards_CompletesWithoutInvokingSeriesServiceOrThrowing"
        status: pass
    human_judgment: false
  - id: D3
    description: "The stale 'null active group means see all' documentation is corrected on both IActiveGroupContext and ActiveGroupContextService to state that a null value yields zero rows from every tenant-scoped query filter, with no behavior change"
    verification:
      - kind: unit
        ref: "grep -c 'see all' / 'zero rows' assertions on both files; dotnet build exit 0"
        status: pass
    human_judgment: false
  - id: D4
    description: "The job is registered as a sibling Hangfire recurring job (id recurring-occurrence-top-up, cron 0 3 * * *) after ConfigureDatabase() so migrations have run before it can fire, at an off-peak hour distinct from the 09:00 reminder sweep"
    requirement: "EVTRECUR-03"
    verification:
      - kind: unit
        ref: "grep assertions in Program.cs for RecurringJob.AddOrUpdate<RecurringOccurrenceTopUpJob>, the job id literal, and the cron string; dotnet build exit 0"
        status: pass
    human_judgment: false

# Metrics
duration: ~20min
completed: 2026-08-28
status: complete
---

# Phase 76 Plan 05: Nightly Recurring-Series Top-Up Job Summary

**`RecurringOccurrenceTopUpJob` — a per-board-scoped Hangfire recurring job (03:00 daily) that tops every active series back to its runway via the same `IEventSeriesService.TopUpAsync` the controller calls, proven safe against one-board failure and a corrected "null group means zero rows, not everything" doc fix.**

## Performance

- **Duration:** ~20 min
- **Started:** 2026-08-28T13:32:00Z (approx.)
- **Completed:** 2026-08-28T13:52:35Z
- **Tasks:** 3
- **Files modified:** 5 (2 created, 3 modified)

## Accomplishments

- `RecurringOccurrenceTopUpJob.ExecuteAsync` enumerates real boards through exactly one null-group `HangfireJobHelper.RunInScopeAsync` call (the group table is the tenant boundary itself and carries no query filter), then opens a fresh scope with a real, non-null board id per board — inside which it resolves `IEventSeriesService`, calls `GetActiveSeriesForActiveGroupAsync`, and tops up every returned series via `TopUpAsync`.
- Per-board failure isolation: a try/catch around each board's scope call logs the board id and continues to the next board; after the loop, if any board failed, the job throws `InvalidOperationException` naming the failure count so Hangfire's existing global 5-attempt retry filter still applies to the sweep, and a retry is additive rather than duplicating because every write goes through the slot-keyed idempotent `TopUpAsync`.
- Registered in `Program.cs` immediately after the existing `DailyReminderJob` registration, inside the same non-Testing branch and after `ConfigureDatabase()`, as `"recurring-occurrence-top-up"` on cron `"0 3 * * *"` — a distinct off-peak hour from the 09:00 reminder sweep, and daily rather than weekly so a failed run self-heals the next night.
- Corrected the stale "null active group means see all" doc comment on both `IActiveGroupContext` and `ActiveGroupContextService` (class summary and `ActiveGroupId` property summary) to state that a null value yields zero rows from every tenant-scoped query filter (each filter requires a non-null exact match) — with zero behavior change, verified by `git diff --stat` showing only comment lines touched.
- `RecurringOccurrenceTopUpJobTests` (5 facts) mirrors `DailyReminderJobTests`'s mocked scope-factory chain but resolves a real `ActiveGroupContextService` instance (constructed over a substituted `IHttpContextAccessor`) so `SetGroupId` calls are observable directly: one call per board with the real board ids in order; `TopUpAsync` invoked exactly once per series per board (by real series id, six calls across three boards of two series each); a throwing board does not block the remaining boards while the sweep still throws afterward; zero boards completes cleanly with no series-service calls at all; and the board enumeration call is proven to be the only call made with no group selected.

## Task Commits

Each task was committed atomically:

1. **Task 1: Create the per-group recurring top-up job and register it beside the existing daily job** - `9a38b16` (feat)
2. **Task 2: Correct the stale "null means see all" documentation on the group context** - `738e332` (docs)
3. **Task 3: Unit-test that the job scopes per board and never sweeps across boards in one call** - `76a939e` (test)

**Plan metadata:** committed alongside this SUMMARY (worktree mode — orchestrator finalizes the metadata commit after merge)

## Files Created/Modified

- `QuestBoard.Service/Jobs/RecurringOccurrenceTopUpJob.cs` - New Hangfire job: null-group board enumeration, per-board real-group-id scope, per-board try/catch with aggregate throw, summary log line
- `QuestBoard.Service/Program.cs` - Registered `RecurringJob.AddOrUpdate<RecurringOccurrenceTopUpJob>("recurring-occurrence-top-up", ..., "0 3 * * *")` beside the existing daily reminder registration
- `QuestBoard.Service/Services/ActiveGroupContextService.cs` - Corrected class and `ActiveGroupId` property doc comments; no behavior change
- `QuestBoard.Domain/Interfaces/IActiveGroupContext.cs` - Corrected interface summary doc comment; no behavior change
- `QuestBoard.UnitTests/Services/RecurringOccurrenceTopUpJobTests.cs` - New: 5 xUnit facts proving per-board scoping, per-series top-up counts, failure isolation with aggregate throw, zero-board short-circuit, and single-null-scope enumeration

## Decisions Made

- The job is deliberately not registered in the DI container, matching `DailyReminderJob`'s existing precedent (Hangfire's ASP.NET Core activator constructs unregistered concrete job types from the scope).
- Test group ids are captured by reading `ActiveGroupContextService.ActiveGroupId` from inside the substituted `IEventSeriesService`/`IGroupRepository` `Returns` callbacks, rather than counting `CreateAsyncScope()` invocations — this proves a real board id reached the group context on every call, which is the actual fact under test, not merely that N scopes were created.
- The aggregate failure test asserts both outcomes together (all three boards attempted in order, and the sweep still throws) in one fact, since the plan's behavior block ties "does not block remaining boards" and "still reports failure" to the same scenario.

## Deviations from Plan

None - plan executed exactly as written. All acceptance-criteria greps, `dotnet build`, and both task-specified `dotnet test --filter` verify commands passed on first attempt; the full `QuestBoard.UnitTests` suite (385 tests) also passed with no regressions.

## Issues Encountered

None.

## TDD Gate Compliance

Task 3 carries `tdd="true"`. Per the plan's own task ordering, the production code (`RecurringOccurrenceTopUpJob`) was built and committed in Task 1, and this task's purpose was to add proof against already-correct code — the same shape as 76-04's Task 2. A literal RED-then-GREEN cycle was not applicable: all 5 facts passed on first run against the Task-1 implementation with no fix-up required, so there was no unexpected-pass-during-RED case to investigate. The task was executed as a single `test(...)` commit after write-verify, matching the plan's task ordering (job code in Task 1, tests in Task 3).

## User Setup Required

None - no external service configuration required. No migration in this plan.

## Next Phase Readiness

- The nightly top-up job now closes the loop the plan's `must_haves` describe: an open-ended series never needs a manual re-extension, because a daily 03:00 sweep tops every active series on every board back to its runway using the exact same idempotent materializer the controller path uses.
- The corrected `IActiveGroupContext`/`ActiveGroupContextService` documentation removes a latent trap for any future background job author who might otherwise assume a null active group is a safe cross-board read.
- No blockers. Remaining Phase 76 plans (calendar UI, runway banner, series lifecycle controller/views) can proceed independently — this plan touched no controller, view, or ViewModel files.

---
*Phase: 76-recurring-event-series*
*Completed: 2026-08-28*

## Self-Check: PASSED

- FOUND: QuestBoard.Service/Jobs/RecurringOccurrenceTopUpJob.cs
- FOUND: QuestBoard.UnitTests/Services/RecurringOccurrenceTopUpJobTests.cs
- FOUND: commit 9a38b16 (Task 1)
- FOUND: commit 738e332 (Task 2)
- FOUND: commit 76a939e (Task 3)
