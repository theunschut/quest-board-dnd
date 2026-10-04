---
phase: 89-pull-based-release-deployment
plan: 01
subsystem: release-packaging
tags: [release, packaging, migrator, health, systemd, tracer]
status: complete

requires: []
provides:
  - "Release artifact contract: questboard-vX.Y.Z.zip (app/, migrator/, deploy/, release-manifest.json) plus .sha256"
  - "QuestBoard.Migrator console project with the status command and exit-code contract"
  - "X-QuestBoard-Version header on GET /health"
  - "questboard.service drop-in redirecting the unit into /opt/questboard/current/app"
affects: [89-02, 89-03, 89-04, 89-05, 89-06, 89-07]

tech-stack:
  added: []
  patterns:
    - "Migrator logic lives in its own project so the Repository raw-SQL seam guard stays valid"
    - "DB-less migration inspection through the EF SQL generator plus a text scan"

key-files:
  created:
    - QuestBoard.Migrator/QuestBoard.Migrator.csproj
    - QuestBoard.Migrator/Program.cs
    - QuestBoard.Migrator/MigratorCli.cs
    - QuestBoard.Migrator/MigrationRunner.cs
    - build/package-release.sh
    - build/tests/package-release-smoke-test.sh
    - deploy/systemd/questboard.service.d/10-release-layout.conf
    - QuestBoard.UnitTests/Migrator/ProbeMigrations.cs
    - QuestBoard.UnitTests/Migrator/MigrationPreflightTests.cs
    - QuestBoard.UnitTests/Migrator/MigratorCliTests.cs
    - QuestBoard.IntegrationTests/Controllers/HealthVersionHeaderTests.cs
  modified:
    - QuestBoard.Service/Program.cs
    - QuestBoard.slnx
    - QuestBoard.UnitTests/QuestBoard.UnitTests.csproj

key-decisions:
  - "Migrator reads only ConnectionStrings__DefaultConnection and builds QuestBoardContext with a null active-group stub; no EF package of its own"
  - "No retrying execution strategy in the migrator, so a caller-owned transaction is allowed"
  - "Connection failures print a fixed text plus the SqlException number only; unexpected failures print only the exception type name"
  - "ServiceExtensions.ConfigureDatabase and its startup Migrate() are untouched, so Docker and dev keep migrating on startup"

requirements-completed: [MH-1, MH-2, MH-3, MH-6]

duration: n/a (two executor sessions, split by the tracer checkpoint)
completed: 2026-10-04

actuals:
  tokens: 8700
  tasks: 2
  commits: 2
---

# Phase 89 Plan 01: Packaged release contract and migrator status Summary

**A locally packaged release now carries the app, its own EF migrator and the deploy tooling in a versioned zip, the packaged app reports its version on /health from the `current` symlink layout, and DB-less unit tests guard migration transactionality and the migrator's secret-free failure path.**

## Tasks

| Task | Name | Commit | Type |
| ---- | ---- | ------ | ---- |
| 1 | Tracer: a packaged release reports its own version from the versioned layout | 7be4e2ae | tracer |
| 2 | DB-less migrator guards and the health header regression test | 69022dc0 | auto (tdd) |

### Task 1 (tracer)

Delivered in the first executor session: the `/health` response writer adding `X-QuestBoard-Version`,
the `QuestBoard.Migrator` project (status command, exit codes, DB-less non-transactional detection),
`build/package-release.sh`, the systemd drop-in, and `build/tests/package-release-smoke-test.sh`.

Tracer feedback gate: the interactive human-verify checkpoint after Task 1 was **approved** by the user.
The orchestrator independently re-ran `bash build/tests/package-release-smoke-test.sh`; all 14 checks passed with exit 0.

### Task 2

- `QuestBoard.UnitTests` now references `QuestBoard.Migrator` (no packages added).
- `ProbeMigrations.cs`: three empty-model probe contexts and five hand-written migrations (suppressed transaction, `ALTER DATABASE`, `FULLTEXT`, `MEMORY_OPTIMIZED`, plain `CreateTable`).
- `MigrationPreflightTests`: every shipped migration is transactional, the model has no pending changes, each probe scenario flags correctly, and `ComposeStatus` handles case-insensitive matching, unknown ids and the pending-only non-transactional lookup.
- `MigratorCliTests`: an unreachable server returns exit 4 and neither writer contains host, user, password or database name; unknown command and missing connection string return 1; an unexpected exception prints only its type name.
- `HealthVersionHeaderTests`: `/health` returns 200, the version header equals `AppVersion.Current` and the body still contains `Healthy`.

## Verification

- `dotnet test QuestBoard.UnitTests --filter FullyQualifiedName~Migrator`: 13 passed.
- `dotnet test QuestBoard.IntegrationTests --filter "HealthVersionHeader|BoardTimeZoneHealthCheck"`: 4 passed.
- Full `dotnet test`: 818 unit and 952 integration tests passed, 0 failed.
- Planning-reference grep over the new test files: clean.

## Deviations from Plan

None - plan executed as written. One note on TDD ordering: because the migrator logic shipped with the
tracer in Task 1, the Task 2 tests passed on first run rather than failing first, so there was no
separate RED commit and no `MigrationRunner.cs` change was needed. The tests are regression guards
over already-correct behaviour, committed as a single `test(...)` commit.

## Authentication Gates

None.

## Known Stubs

None.

## Threat Flags

None. The new surface (`/health` version header, migrator stdout/stderr) is the surface the plan's threat model already covers.

## Self-Check: PASSED

- Files created in Task 2 exist: ProbeMigrations.cs, MigrationPreflightTests.cs, MigratorCliTests.cs, HealthVersionHeaderTests.cs.
- Commits 7be4e2ae and 69022dc0 present in `git log`.
