---
phase: 102-board-deletion
plan: 02
subsystem: session-middleware
tags: [session, middleware, super-admin, board-deletion]
requires: []
provides:
  - ActiveBoardSessionExtensions.ClearActiveBoard / ClearActiveBoardIfAsync
  - super-admin board-exists check in GroupSessionMiddleware
affects: [102-09]
tech-stack:
  added: []
  patterns: [single named place for clearing the three active-board session keys]
key-files:
  created:
    - QuestBoard.Service/Extensions/ActiveBoardSessionExtensions.cs
  modified:
    - QuestBoard.Service/Middleware/GroupSessionMiddleware.cs
    - QuestBoard.Service/Services/IActiveBoardSwitcher.cs
    - QuestBoard.UnitTests/Middleware/GroupSessionMiddlewareRevalidationTests.cs
    - QuestBoard.IntegrationTests/Tests/TenantIsolationTests.cs
decisions:
  - Super admins stay membership-exempt; only the board's existence is verified on the revalidation interval.
metrics:
  duration: ~20min
  completed: 2026-11-11
status: complete
actuals:
  tokens: 9000
  tasks: 2
  commits: 2
---

# Phase 102 Plan 02: Stale active board recovery for super admins Summary

A super admin whose session names a deleted board is now cleared and sent to the board picker (409 on non-GET/HEAD) on the next due revalidation, via one shared helper that clears the three active-board session keys.

## What was built

- `ActiveBoardSessionExtensions.ClearActiveBoard(ISession)` removes `ActiveGroupId`, `ActiveGroupName` and `ActiveGroupValidatedAtUtc` together; `ClearActiveBoardIfAsync(ISession, int, CancellationToken)` clears them only when the session names the given board (for the deleting controller to use).
- `GroupSessionMiddleware`: the duplicated redirect/409 gate is now a private `GateToBoardPicker`; the removed-member arm uses `ClearActiveBoard`; a new `else if (needsRevalidation)` arm (super admins only) looks the board up through `IGroupService.GetByIdAsync`, clears and gates when it is missing, otherwise restamps the timestamp. Super admins are still never membership-checked.
- `IActiveBoardSwitcher` XML doc now points to the clear helper.
- Six new unit facts (deleted board GET/POST, existing board, fresh timestamp, helper match/no-match); existing facts unchanged.
- `TenantIsolationTests.GroupFilter_NonExistentGroup_ReturnsEmpty` rewritten: asserts the 302 to `/groups/pick`, no quest content, and (via a DI scope) that the query filter returns 0 quests for board 999 while `IgnoreQueryFilters` sees 1.

## Verification

- Unit: `GroupSessionMiddlewareRevalidationTests` 11/11; full unit project 1315 passed, 0 skipped.
- Integration: before the test edit 1803 passed, 1 failed (the expected `GroupFilter_NonExistentGroup_ReturnsEmpty`), 11 skipped; after: 1804 passed, 0 failed, 11 skipped. Skipped count unchanged (all `MigrationRunnerSqlServerTests`, SQL Server is down). No other integration file needed a board seeded, so no extra files touched.

## Deviations from Plan

None - plan executed as written. Task 1 was committed as one feat commit (tests and implementation together) rather than separate RED/GREEN commits.

## Known Stubs

None.

## Threat Flags

None.

## Commits

- 15d2f05d feat(102-02): clear a super admin's session when its active board is gone
- 7c3252fe test(102-02): non-existent active board now redirects a super admin to the picker

## Self-Check: PASSED
