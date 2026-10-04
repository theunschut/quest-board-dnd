---
phase: 89-pull-based-release-deployment
plan: 02
subsystem: release-migrator
tags: [migrator, backup, atomic-apply, sql-server, ci]
status: complete

requires: ["89-01"]
provides:
  - "QuestBoard.Migrator backup --label <label>: copy-only BACKUP, prints {backupName}, exit 0/1/4/5"
  - "QuestBoard.Migrator apply: all-or-nothing migration in one caller transaction, prints {applied}, exit 0/2/3/4/6"
  - "Gated real-SQL-Server integration tests with QUESTBOARD_MIGRATOR_TEST_CONNECTION / QUESTBOARD_MIGRATOR_TEST_REQUIRED"
  - "migrator-sql CI job and workflow_dispatch trigger in dotnet.yml"
affects: [89-04, 89-05, 89-06, 89-07, 89-10]

tech-stack:
  added: []
  patterns:
    - "Caller-owned transaction around Database.Migrate(); MigrationsUserTransactionWarning ignored only in the migrator's options"
    - "SQL Server tests that skip locally and fail (not skip) when required mode is on"

key-files:
  created:
    - QuestBoard.UnitTests/Migrator/MigratorBackupAndApplyCliTests.cs
    - QuestBoard.IntegrationTests/Migrator/MigratorSqlServer.cs
    - QuestBoard.IntegrationTests/Migrator/SqlProbeMigrations.cs
    - QuestBoard.IntegrationTests/Migrator/MigrationRunnerSqlServerTests.cs
  modified:
    - QuestBoard.Migrator/MigrationRunner.cs
    - QuestBoard.Migrator/MigratorCli.cs
    - QuestBoard.IntegrationTests/QuestBoard.IntegrationTests.csproj
    - .github/workflows/dotnet.yml

key-decisions:
  - "Backup checks database existence through master before opening the target connection, so a missing database reports a backup failure (exit 5) rather than a misleading connect failure"
  - "Apply creates a missing database before opening the target connection, outside the transaction; a failure there is an apply failure, a failure to open afterwards is a connect failure"
  - "Statement-phase failures (backup, apply) print the SQL error number and message; connection-phase failures print fixed text and number only"

requirements-completed: [MH-2, MH-6]

duration: n/a
completed: 2026-10-04

actuals:
  tokens: 21000
  tasks: 3
  commits: 3
---

# Phase 89 Plan 02: Migrator backup and atomic apply Summary

**The migrator can now take a parameterised copy-only backup and apply every pending migration inside one caller-owned transaction, refusing unsafe batches before any write; a CI job proves the all-or-nothing guarantee against a real SQL Server.**

## Tasks

| Task | Name | Commit | Type |
| ---- | ---- | ------ | ---- |
| 1 | backup and atomic apply commands | 7f16df3e | auto (tdd) |
| 2 | gated real-SQL proof of atomic apply, refusal and backup | 49d0fc44 | auto (tdd) |
| 3 | CI job that runs the gated SQL proof | bba4a1a6 | auto |

### Task 1

- `MigrationRunner` takes an optional `TimeProvider` (default `TimeProvider.System`) for the backup timestamp.
- `IsValidBackupLabel` (`^[A-Za-z0-9._-]{1,64}$`) and `BuildBackupFileName` (`questboard-premigration-<label>-<yyyyMMddTHHmmssZ>.bak`, invariant culture, UTC).
- `Backup`: validates the label before any connection, `BACKUP DATABASE ... WITH COPY_ONLY, CHECKSUM, INIT, NAME = ...` through `ExecuteSql` interpolation so database, device and set name are all SQL parameters, 30 minute command timeout, never inside a transaction.
- `ApplyAtomically`: status preflight (database-ahead exit 2, non-transactional exit 3, nothing pending returns an empty list), creates a missing database outside the transaction, then `BeginTransaction` / `Migrate` / `Commit` with a 10 minute timeout. Any failure disposes the uncommitted transaction, which rolls back schema and history together. No retrying execution strategy anywhere.
- Four new exception types (`MigratorDatabaseAhead`, `MigratorNonTransactional`, `MigratorBackup`, `MigratorApply`); the CLI maps them to exits 2, 3, 5, 6 and keeps connection failures at exit 4 with no connection details.
- `MigratorBackupAndApplyCliTests`: 17 DB-less tests (label validation matrix, file naming, validation-before-connection with a factory that throws if called, usage on missing label, unreachable server leaks no host, user, password or database name for both commands).

### Task 2

- The integration test project references `QuestBoard.Migrator`; no packages added.
- `MigratorSqlServer` helper reads `QUESTBOARD_MIGRATOR_TEST_CONNECTION`, skips with `Assert.Skip` when it is absent, fails with `Assert.Fail` when `QUESTBOARD_MIGRATOR_TEST_REQUIRED=1`, and waits up to 120 s (2 s retries) for a starting server. `ScratchDatabase` creates and drops a Guid-named database.
- Probe contexts: failing second migration (`THROW 50000`), healthy pair, and a suppressed-transaction migration.
- 11 facts: rollback of schema and history after a failing second migration, healthy apply, nothing-pending no-op, non-transactional refusal with nothing applied, database-ahead status and apply refusal, copy-only backup name and `msdb.dbo.backupset.is_copy_only = 1`, backup CLI output, backup of a missing database (exit 5), apply creating a missing database, and `CanBackup` for the CI login.

### Task 3

- `dotnet.yml` gains `workflow_dispatch` and a `migrator-sql` job (ubuntu-24.04, 20 minute timeout, job-level `contents: read`, SQL Server 2022 service container, pinned checkout and setup-dotnet, required mode on). The existing `build` job and its .NET 8 setup are untouched; the diff has zero removed lines.

## Verification

- `dotnet test QuestBoard.UnitTests --filter FullyQualifiedName~Migrator`: 29 passed.
- `dotnet test QuestBoard.IntegrationTests --filter FullyQualifiedName~Migrator`: 11 skipped, 0 failed (no SQL Server locally).
- `QUESTBOARD_MIGRATOR_TEST_REQUIRED=1 dotnet test QuestBoard.IntegrationTests --filter FullyQualifiedName~Migrator --no-build`: 11 failed, exit 1, so required mode cannot silently skip.
- Full `dotnet test`: 834 unit passed; 952 integration passed, 11 skipped, 0 failed.
- dotnet.yml structural check (workflow_dispatch present, service image, `${{ }}` absent from `run:`, required mode on, build job .NET 8 untouched): ok. Both pinned actions are 40-character commit SHAs.
- Planning-reference grep over `QuestBoard.Migrator`, `QuestBoard.UnitTests/Migrator` and `QuestBoard.IntegrationTests/Migrator`: clean.

## Deviations from Plan

None - plan executed as written. Two implementation details went beyond the letter of the plan but within its intent: the existence check in `Backup` and the `Create()` in `ApplyAtomically` run before the target connection is opened, because opening a connection to a database that does not exist fails with a login error that would otherwise be reported as a connect failure.

## Not verified locally (needs the hosted run)

No SQL Server is reachable at localhost:1433 here and none was provisioned, so the SQL Server behaviour (transaction rollback, `BACKUP` with parameterised names, bare-file-name resolution to the default backup directory, `HAS_PERMS_BY_NAME` probe, service container start-up without a health command) is encoded in tests but has not executed. The `migrator-sql` job must be seen green with executed, not skipped, tests on the hosted run (plan 10).

## Authentication Gates

None.

## Known Stubs

None.

## Threat Flags

None. The new surface (backup and apply commands, CI service container) is covered by the plan's threat model.

## Self-Check: PASSED

- Created files exist: MigratorBackupAndApplyCliTests.cs, MigratorSqlServer.cs, SqlProbeMigrations.cs, MigrationRunnerSqlServerTests.cs.
- Commits 7f16df3e, 49d0fc44 and bba4a1a6 present in `git log`.
