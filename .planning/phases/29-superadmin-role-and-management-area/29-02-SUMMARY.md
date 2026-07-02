---
phase: 29-superadmin-role-and-management-area
plan: 02
subsystem: database
tags: [ef-core, migrations, aspnet-identity, roles, sql-server]

# Dependency graph
requires:
  - phase: 29-superadmin-role-and-management-area-plan-01
    provides: SuperAdminOnly policy and auth handler infrastructure
provides:
  - EF Core migration that seeds AspNetRoles with Id=4, Name=SuperAdmin, NormalizedName=SUPERADMIN
  - Deterministic migration with static GUID ConcurrencyStamp
  - Correct Down() rollback via DeleteData(keyValue: 4)
affects:
  - 29-superadmin-role-and-management-area-plan-03
  - 29-superadmin-role-and-management-area-plan-04
  - 29-superadmin-role-and-management-area-plan-05

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "InsertData migration for Identity role seeding (consistent with ConvertIsDungeonMasterToRoles pattern)"
    - "Static GUID ConcurrencyStamp for deterministic, reproducible migrations"

key-files:
  created:
    - QuestBoard.Repository/Migrations/20260630132256_AddSuperAdminRole.cs
    - QuestBoard.Repository/Migrations/20260630132256_AddSuperAdminRole.Designer.cs
  modified:
    - .planning/STATE.md (D-11 manual SQL step documented)

key-decisions:
  - "Static GUID literal f3a9d2b1-7c4e-4d8a-9b6f-2e1c0a5d3f7e used as ConcurrencyStamp — not Guid.NewGuid() at runtime — makes migration deterministic and reproducible across environments"
  - "D-11: First SuperAdmin user assignment is a manual post-deploy SQL INSERT into AspNetUserRoles(UserId=<id>, RoleId=4) — no startup automation"

patterns-established:
  - "Role seeding via InsertData: Id, Name, NormalizedName, ConcurrencyStamp (static GUID)"

requirements-completed:
  - AUTH-01

# Metrics
duration: 5min
completed: 2026-06-30
---

# Phase 29 Plan 02: SuperAdmin Role Migration Summary

**EF Core migration seeds AspNetRoles with SuperAdmin role (Id=4) via InsertData, with deterministic GUID ConcurrencyStamp and correct DeleteData rollback**

## Performance

- **Duration:** ~5 min
- **Started:** 2026-06-30T13:20:00Z
- **Completed:** 2026-06-30T13:25:00Z
- **Tasks:** 1
- **Files modified:** 3 (2 new migration files + STATE.md)

## Accomplishments

- Generated AddSuperAdminRole EF Core migration via `dotnet ef migrations add`
- Replaced empty scaffold Up()/Down() with the SuperAdmin seed: Id=4, Name="SuperAdmin", NormalizedName="SUPERADMIN"
- Used a static GUID string literal as ConcurrencyStamp (not `Guid.NewGuid()`) to ensure deterministic migrations
- Added correct Down() rollback: `DeleteData(table: "AspNetRoles", keyColumn: "Id", keyValue: 4)`
- Documented D-11 manual SQL post-deploy step in STATE.md
- Full solution builds with 0 errors

## Task Commits

1. **Task 1: Generate and verify the AddSuperAdminRole EF Core migration** - `6343dbe` (feat)

**Plan metadata:** (docs commit follows)

## Files Created/Modified

- `QuestBoard.Repository/Migrations/20260630132256_AddSuperAdminRole.cs` - Migration Up() InsertData AspNetRoles Id=4 SuperAdmin; Down() DeleteData keyValue=4
- `QuestBoard.Repository/Migrations/20260630132256_AddSuperAdminRole.Designer.cs` - Auto-generated EF Core migration designer file
- `.planning/STATE.md` - D-11 manual SQL INSERT instruction for first SuperAdmin user assignment

## Decisions Made

- Static GUID `f3a9d2b1-7c4e-4d8a-9b6f-2e1c0a5d3f7e` embedded as ConcurrencyStamp literal — consistent with producing reproducible migration files (Guid.NewGuid() at runtime would produce different values on every `dotnet ef migrations add` run)
- QuestBoardContextModelSnapshot.cs was NOT updated by dotnet ef — expected for data-only migrations (InsertData does not change schema; snapshot tracks schema only)

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

None. The `dotnet ef migrations add AddSuperAdminRole` command succeeded on the first run. The scaffold generated empty Up()/Down() bodies as expected for a data-only migration (no schema changes), and these were replaced with the InsertData/DeleteData per the plan specification.

## Threat Surface Scan

No new network endpoints, auth paths, file access patterns, or schema changes at trust boundaries were introduced. The migration adds only a data row to AspNetRoles — no structural changes. This is within the scope of T-29-M-01 (accepted: role definition row only, no user assignment).

## Known Stubs

None.

## User Setup Required

**Post-deploy manual step (D-11):** After deploying the migration, assign the first SuperAdmin user by running once on the production database:

```sql
-- Find the userId in AspNetUsers WHERE UserName = '<username>'
INSERT INTO AspNetUserRoles (UserId, RoleId)
VALUES (<userId>, 4);
```

This is intentional — no user is automatically assigned to SuperAdmin by the migration.

## Next Phase Readiness

- AUTH-01 satisfied: AspNetRoles row Id=4 SuperAdmin exists when migration is applied
- Phases 29-03 through 29-05 can proceed (this migration is independent of all code changes)
- The migration auto-applies on application startup via `context.Database.Migrate()`

## Self-Check: PASSED

- `QuestBoard.Repository/Migrations/20260630132256_AddSuperAdminRole.cs` — FOUND
- `QuestBoard.Repository/Migrations/20260630132256_AddSuperAdminRole.Designer.cs` — FOUND
- Commit `6343dbe` — FOUND (git log confirmed)
- Build exits 0 — CONFIRMED

---
*Phase: 29-superadmin-role-and-management-area*
*Completed: 2026-06-30*
