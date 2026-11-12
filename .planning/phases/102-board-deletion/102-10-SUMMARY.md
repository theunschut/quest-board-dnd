---
phase: 102-board-deletion
plan: 10
subsystem: platform-board-deletion
tags: [verification, sql-server, uat]
status: complete
requires: [102-05, 102-08, 102-09]
provides:
  - full-suite gate result
  - required gated SQL Server run result
affects: []
key-files:
  created: []
  modified: []
decisions: []
actuals:
  tokens: 0
  tasks: 3
  commits: 1
---

# Phase 102 Plan 10: Verification and sign-off Summary

**Status: COMPLETE. Tasks 1 and 2 by the executor; Task 3 run on 2026-11-12 (all 6 UAT items passed, see `102-UAT.md`).**

Verification-only plan: no source code changed. The full suite is green, and the required gated SQL Server run against the reachable local server reports 2 passed, which proves the self-referencing chain ordering, the cascades and the rollback on real SQL Server.

## Task 1: Full build and test gate - DONE

| Command | Result |
|---------|--------|
| `dotnet build` | 0 errors (64 pre-existing xUnit analyzer warnings) |
| `dotnet test QuestBoard.UnitTests` | Passed 1366, Failed 0, Skipped 0, Total 1366 |
| `dotnet test QuestBoard.IntegrationTests` with `QUESTBOARD_MIGRATOR_TEST_CONNECTION` and `QUESTBOARD_MIGRATOR_TEST_REQUIRED=1` (gated facts enabled) | Passed 1851, Failed 0, Skipped 0, Total 1851 |
| `dotnet test QuestBoard.IntegrationTests` without the variables | Passed 1838, Failed 0, Skipped 13, Total 1851 |
| `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~Architecture"` | Passed 92, Failed 0, Skipped 0 |

The 13 skips in the no-variable run are the two `BoardDeletionSqlServerTests` facts
(`DeletingAFullyPopulatedBoard_SucceedsAndLeavesTheOtherBoardIntact`, `AFailureInsideTheSave_RollsEveryRowBack`)
plus the pre-existing `MigrationRunnerSqlServerTests` facts. No CLR flake occurred and no retry was needed.

## Task 2: REQUIRED gated SQL Server run - DONE

Authorised by the user's coordinating session and run against the reachable server at localhost:1433 with the
existing user-secrets SQL login (credentials never printed or written):

```
QUESTBOARD_MIGRATOR_TEST_REQUIRED=1 QUESTBOARD_MIGRATOR_TEST_CONNECTION=<redacted> \
  dotnet test QuestBoard.IntegrationTests --no-build --filter "FullyQualifiedName~BoardDeletionSqlServer"
```

Summary line (evidence):

```
Passed!  - Failed:     0, Passed:     2, Skipped:     0, Total:     2, Duration: 14 s - QuestBoard.IntegrationTests.dll (net10.0)
```

Research assumption A1 holds: EF Core orders the self-referencing chains without a circular-dependency error, so the
documented gap-plan fallback is not needed. The tests only use their own `QuestBoardMigratorProbe_<guid>` scratch
database (InitialCatalog forced to master, dropped on dispose); the app's QuestBoard database was not touched.
A read-only leftover-database check was skipped (no `sqlcmd` available in this environment).

## Task 3: Real SQL Server delete and UI backstops - DONE (2026-11-12)

Run by Claude for the user against the local SQL Server, app on localhost:8000, driven with Playwright as the
qb-audit super admin. Re-run first: full suite with SQL Server required, 1389 unit + 1855 integration passed, 0 skipped.
"UAT Doomed Board" (13 quests, dates, votes, a signup, an event series, a character, a contact with image and tag) and
"UAT Survivor Board" were created through the app and deleted through the typed-name page with the toast and no error
page. All six items in `102-UAT.md` pass, with evidence there. Not exercised in the browser: the cross-board
"Kept, but unlinked" rows (the forms cannot create that data; covered by the integration and SQL Server tests) and the
shop purchase step (shop item creation fails on an older, separate bug in `ShopManagementController.Create`).

Backstops left from 102-08 for this UAT: bfcache restore state (button state matches input after Back) and the no-JS
path (button never unlocks without JavaScript; the rendered disabled state is already covered by a test).

## Deviations from Plan

None - Tasks 1 and 2 executed as written, with the gated run executed by the executor on the coordinator's authorisation instead of by the user's shell.

## Known Stubs

None.

## Self-Check: PASSED

Verification-only plan; no files created besides this summary.
