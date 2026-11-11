---
phase: 102-board-deletion
plan: 10
subsystem: platform-board-deletion
tags: [verification, sql-server, uat]
status: partial
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
  tasks: 2
  commits: 1
---

# Phase 102 Plan 10: Verification and sign-off Summary

**Status: PARTIAL - awaiting human verification (Task 3). Tasks 1 and 2 are done; the phase must not close until the user completes Task 3.**

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

## Task 3: Real SQL Server delete and UI backstops - PENDING (user's hands-on check)

Not run by the executor: no app was started, no browser driven, no boards created. Awaiting the user. Steps:

1. Start the app against the local SQL Server; as super admin create throwaway boards "UAT Doomed Board" and "UAT Survivor Board".
2. On the doomed board add a quest with signup and date vote, a character signed up to a quest on the survivor board, an event series with an occurrence, a contact with image and tag, and a shop item that is bought and returned.
3. On the survivor board create a follow-up quest of a doomed-board quest if the UI allows it.
4. Check /platform/Users Boards column counts, names on hover (desktop) and in the details list (phone User-Agent with touch).
5. Select the doomed board as the active board, remove every member on its Platform Members page, open Delete from the Platform board list.
6. Check per-type counts, collapsed name lists, "Kept, but unlinked", "Not affected" row, Delete button disabled until the exact name is typed, green "Name matches" line.
7. Wrong case keeps the button disabled and Enter does not submit; exact name then Delete board gives the board list with toast and no error page.
8. Any board page sends you to the board picker; survivor board keeps its quest signup (without character) and everything else intact.
9. Phone User-Agent at 390px: very long name and many quests, no horizontal scroll, names wrap; wrong name blocks submit; Back after a cancelled attempt shows button state matching the field.
10. Delete the survivor board to clean up.

Backstops left from 102-08 for this UAT: bfcache restore state (button state matches input after Back) and the no-JS
path (button never unlocks without JavaScript; the rendered disabled state is already covered by a test).

## Deviations from Plan

None - Tasks 1 and 2 executed as written, with the gated run executed by the executor on the coordinator's authorisation instead of by the user's shell.

## Known Stubs

None.

## Self-Check: PASSED

Verification-only plan; no files created besides this summary.
