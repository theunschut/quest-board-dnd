---
phase: 33-session-persistence-persist-activegroupid-across-app-restart
plan: 01
subsystem: infra
tags: [aspnet-core-session, distributed-cache, sqlserver, ef-core-migration, dependency-injection]

# Dependency graph
requires: []
provides:
  - Microsoft.Extensions.Caching.SqlServer 10.0.9 package reference in QuestBoard.Service
  - AddSessionStateTable migration provisioning dbo.AspNetSessionState (case-sensitive collation, PK, index)
  - AddDistributedSqlServerCache (non-Testing) / AddDistributedMemoryCache (Testing) registration in Program.cs, before AddSession
affects: [33-02, 33-03]

# Tech tracking
tech-stack:
  added: [Microsoft.Extensions.Caching.SqlServer 10.0.9]
  patterns: ["Testing-environment guard for external-dependency DI registrations (mirrors existing Hangfire branch)"]

key-files:
  created:
    - QuestBoard.Repository/Migrations/20260701163850_AddSessionStateTable.cs
    - QuestBoard.Repository/Migrations/20260701163850_AddSessionStateTable.Designer.cs
  modified:
    - QuestBoard.Service/QuestBoard.Service.csproj
    - QuestBoard.Service/Program.cs

key-decisions:
  - "Distributed cache registered before AddSession, Testing-guarded exactly like the existing Hangfire branch — no new structural pattern introduced"
  - "ExpiredItemsDeletionInterval left unset (framework default 30 min) per plan D-04"

patterns-established:
  - "Raw-SQL EF migration with IF NOT EXISTS guard for non-entity schema objects managed by external ADO.NET consumers (SqlServerCache)"

requirements-completed: [SESSION-01, SESSION-02]

# Metrics
duration: 12min
completed: 2026-07-01
status: complete
---

# Phase 33 Plan 01: Session Persistence — SQL Server Distributed Cache Summary

**Backed ASP.NET Core Session with `AddDistributedSqlServerCache` against the existing SQL Server connection, so `ActiveGroupId` survives app restarts without requiring new infrastructure.**

## Performance

- **Duration:** 12 min
- **Started:** 2026-07-01T16:33:29Z
- **Completed:** 2026-07-01T16:42:03Z
- **Tasks:** 3
- **Files modified:** 4 (1 package reference, 2 new migration files, 1 Program.cs edit)

## Accomplishments
- Added `Microsoft.Extensions.Caching.SqlServer` 10.0.9 to `QuestBoard.Service.csproj` (verified first-party Microsoft package from nuget.org)
- Created `AddSessionStateTable` raw-SQL migration provisioning `dbo.AspNetSessionState` with the exact case-sensitive-collation schema `SqlServerCache` expects (PK, `Index_ExpiresAtTime`), with no EF entity added
- Registered `AddDistributedSqlServerCache` (non-Testing) / `AddDistributedMemoryCache` (Testing) before `AddSession` in `Program.cs`, confirmed via 196 passing integration tests that the Testing environment never touches the real SQL Server for session storage

## Task Commits

Each task was committed atomically:

1. **Task 1: Add Microsoft.Extensions.Caching.SqlServer package** - `b49607f` (feat)
2. **Task 2: Create AddSessionStateTable raw-SQL migration** - `57170ce` (feat)
3. **Task 3: Register AddDistributedSqlServerCache (Testing-guarded) before AddSession** - `06ee0b5` (feat)

**Plan metadata:** pending (docs: complete plan)

## Files Created/Modified
- `QuestBoard.Service/QuestBoard.Service.csproj` - Added `Microsoft.Extensions.Caching.SqlServer` 10.0.9 PackageReference
- `QuestBoard.Repository/Migrations/20260701163850_AddSessionStateTable.cs` - Raw-SQL migration creating `dbo.AspNetSessionState` (case-sensitive collation, clustered PK on `Id`, nonclustered index on `ExpiresAtTime`)
- `QuestBoard.Repository/Migrations/20260701163850_AddSessionStateTable.Designer.cs` - Tool-generated migration snapshot companion
- `QuestBoard.Service/Program.cs` - Inserted Testing-guarded `AddDistributedSqlServerCache`/`AddDistributedMemoryCache` block before the existing `AddSession` block

## Decisions Made
- Followed the plan's D-01/D-02/D-04 decisions exactly: cache registration precedes `AddSession`; `SchemaName`/`TableName`/`ConnectionString` set explicitly (no defaults); `ExpiredItemsDeletionInterval` left unset at the 30-minute framework default.
- Restored the pre-existing `dotnet-ef` local tool (declared in `.config/dotnet-tools.json`, version 9.0.6) via `dotnet tool restore` — this was a blocking issue (Rule 3) preventing the migration-add command from running; no new tool was installed, only the already-manifested version was fetched.

## Deviations from Plan

None beyond the Rule 3 blocking-issue fix above (restoring the already-declared `dotnet-ef` tool) — plan executed exactly as written otherwise.

## Issues Encountered
- `dotnet ef migrations add` initially failed with "Run 'dotnet tool restore' to make the 'dotnet-ef' command available" — resolved by running `dotnet tool restore`, which fetched the version already pinned in `.config/dotnet-tools.json` (9.0.6). No package substitution or version change was made.

## User Setup Required

None - no external service configuration required. The migration auto-applies on next app startup via `context.Database.Migrate()` per project convention.

## Next Phase Readiness

- `dbo.AspNetSessionState` schema is defined and will auto-provision on next deploy/restart.
- Manual verification (SESSION-01: restart-and-reload group persistence; SESSION-02: inspect table schema in SQL Server) is explicitly deferred to Plan 03's human-verify checkpoint per this plan's `<verification>` section — no blocker for Plan 02.
- Plan 02 (admin email rate limiting) has no dependency on this plan's changes and can proceed independently.

---
*Phase: 33-session-persistence-persist-activegroupid-across-app-restart*
*Completed: 2026-07-01*

## Self-Check: PASSED

All created/modified files confirmed present on disk; all task commits (b49607f, 57170ce, 06ee0b5) and the summary commit (7ccffe4) confirmed in git log.
