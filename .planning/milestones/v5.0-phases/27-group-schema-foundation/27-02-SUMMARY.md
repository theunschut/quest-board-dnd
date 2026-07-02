---
phase: 27-group-schema-foundation
plan: 02
subsystem: database
tags: [efcore, sqlserver, migration, multitenancy, group, identity]

# Dependency graph
requires:
  - phase: 27-group-schema-foundation/27-01
    provides: "GroupEntity/UserGroupEntity EF model, GroupId FK on QuestEntity/ShopItemEntity, QuestBoardContext DbSets and OnModelCreating configuration"
provides:
  - AddGroupSchema EF Core migration with 8 FK-safe steps (Groups table, UserGroups table, GroupId on Quests/ShopItems, EuphoriaInn seed, UserGroups seed from AspNetUserRoles, AspNetUserRoles Player/DM/Admin cleanup)
  - TestDataHelper factory methods setting GroupId = 1 on Quest and ShopItem
  - QuestBoardContextModelSnapshot regenerated to include all group schema changes
affects: [27-group-schema-foundation/27-03, 28-tenant-isolation, 29-superadmin, 30-group-ux]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - FK-safe migration ordering — AddColumn with defaultValue before populating rows before AddForeignKey
    - IDENTITY_INSERT ON/OFF raw SQL for seeding explicit identity values (EuphoriaInn GroupId=1)
    - LEFT JOIN from parent table for seeding junction rows — ensures no parent rows are silently dropped
    - MAX(CASE) SQL for highest-role-per-user selection from multi-row source

key-files:
  created:
    - QuestBoard.Repository/Migrations/20260630055221_AddGroupSchema.cs
    - QuestBoard.Repository/Migrations/20260630055221_AddGroupSchema.Designer.cs
  modified:
    - QuestBoard.Repository/Migrations/QuestBoardContextModelSnapshot.cs
    - QuestBoard.IntegrationTests/Helpers/TestDataHelper.cs

key-decisions:
  - "Up() step ordering: CreateTable Groups/UserGroups → AddColumn with defaultValue:0 → seed EuphoriaInn → UPDATE rows → AddForeignKey (Pitfall 1 — FK added only after data populated)"
  - "IDENTITY_INSERT Groups ON/OFF wraps the EuphoriaInn seed INSERT to force Id=1 on SQL Server identity column (Pitfall 2)"
  - "LEFT JOIN from AspNetUsers (not INNER JOIN) for UserGroups seeding — preserves users with no AspNetUserRoles row as GroupRole=Player (D-04)"
  - "Down() re-inserts AspNetUserRoles from UserGroups before dropping tables; documented as best-effort data restoration (schema rollback is the guaranteed contract)"
  - "Deployment constraint documented in migration comment: Phase 27 must deploy with Phases 28-29 or during a maintenance window — deleting Player/DM/Admin from AspNetUserRoles breaks auth handlers on new logins until Phase 29 replaces them"

patterns-established:
  - "FK-safe migration: AddColumn(defaultValue:0) → data UPDATE → AddForeignKey — required whenever a NOT NULL FK column is added to an existing production table"
  - "IDENTITY_INSERT raw SQL pattern for seeding explicit identity values in EF Core migrations"

requirements-completed: [GROUP-04, GROUP-05, GROUP-06]

# Metrics
duration: 25min
completed: 2026-06-30
---

# Phase 27 Plan 02: AddGroupSchema Migration Summary

**Single atomic EF Core migration seeding EuphoriaInn as GroupId=1, migrating all users into UserGroups with highest-role logic via LEFT JOIN, and deleting Player/DM/Admin from AspNetUserRoles — all 194 tests pass.**

## Performance

- **Duration:** ~25 min
- **Started:** 2026-06-30T05:50:00Z
- **Completed:** 2026-06-30T06:15:00Z
- **Tasks:** 2 auto + 1 human-verify checkpoint
- **Files modified:** 4 (2 created, 2 modified)

## Accomplishments

- Updated `TestDataHelper.CreateTestQuestAsync` and `CreateShopItemAsync` to set `GroupId = 1`, keeping InMemory test data consistent with the seeded EuphoriaInn group
- Generated `20260630055221_AddGroupSchema` migration scaffold from the EF model (plan 01 state) and authored the full `Up()` body in 10-step FK-safe order covering all 8 D-02 data steps
- Full test suite confirmed green: 194 passed (55 unit + 139 integration), 0 failed — the non-nullable GroupId and new entities integrate cleanly with the InMemory provider

## Task Commits

Each task was committed atomically:

1. **Task 1: Update TestDataHelper to set GroupId = 1 on Quest and ShopItem factories** - `cf07e09` (feat)
2. **Task 2: Generate and author the atomic AddGroupSchema migration (8 FK-safe steps)** - `3667eba` (feat)

**Plan metadata:** (docs commit follows)

## Files Created/Modified

- `QuestBoard.Repository/Migrations/20260630055221_AddGroupSchema.cs` — Single atomic migration with 10-step Up() and reversible Down(); class name `AddGroupSchema`
- `QuestBoard.Repository/Migrations/20260630055221_AddGroupSchema.Designer.cs` — EF tooling-generated designer file (snapshot of model state at migration point)
- `QuestBoard.Repository/Migrations/QuestBoardContextModelSnapshot.cs` — Regenerated to include GroupEntity, UserGroupEntity, GroupId on Quests/ShopItems, all indexes and FK delete behaviors
- `QuestBoard.IntegrationTests/Helpers/TestDataHelper.cs` — `CreateTestQuestAsync` and `CreateShopItemAsync` both set `GroupId = 1`; `SeedRolesAsync` unchanged

## Migration Up() Step Order

| Step | Operation | Rationale |
|------|-----------|-----------|
| 1 | CreateTable Groups + unique index on Name | Parent table must exist before FK references |
| 2 | CreateTable UserGroups + cascade FKs + unique index (UserId, GroupId) | Depends on Groups and AspNetUsers existing |
| 3 | AddColumn Quests.GroupId (defaultValue:0) | Fills existing rows with 0 temporarily |
| 4 | AddColumn ShopItems.GroupId (defaultValue:0) | Fills existing rows with 0 temporarily |
| 5 | IDENTITY_INSERT + INSERT Groups (Id=1, 'EuphoriaInn') | Explicit identity value requires IDENTITY_INSERT ON/OFF |
| 6 | UPDATE Quests/ShopItems SET GroupId=1 | Populate FK column BEFORE adding FK constraint |
| 7 | AddForeignKey Quests.GroupId → Groups (NoAction) | Safe — all rows now have GroupId=1 |
| 8 | AddForeignKey ShopItems.GroupId → Groups (NoAction) | Safe — all rows now have GroupId=1 |
| 9 | INSERT INTO UserGroups from AspNetUserRoles via LEFT JOIN + MAX(CASE) | LEFT JOIN preserves users with no role row |
| 10 | DELETE Player/DungeonMaster/Admin from AspNetUserRoles | Frees AspNetUserRoles for SuperAdmin only (Phase 29) |

## Decisions Made

- Step ordering (FK after data): SQL Server rejects `AddForeignKey` if any row has GroupId=0 with no matching Groups row — populating data before adding the constraint is mandatory (Pitfall 1 from research)
- `IDENTITY_INSERT ON/OFF` used instead of `migrationBuilder.InsertData()` — EF Core 10 does not automatically wrap InsertData with IDENTITY_INSERT for SQL Server (Pitfall 2 from research)
- `LEFT JOIN AspNetUsers` (not `INNER JOIN AspNetUserRoles`) for UserGroups seeding — ensures users with no role entry get `GroupRole=0` (Player), preventing silent data loss before Phase 28 query filters land (D-04)
- Down() re-inserts AspNetUserRoles from UserGroups mapping GroupRole int back to role IDs (Player=0→1, DungeonMaster=1→2, Admin=2→3); documented as best-effort since multi-role users cannot be fully round-tripped
- Deployment constraint comment added to migration file: Phase 27 must not be deployed standalone — authorization handlers break on new logins until Phase 29 replaces them

## Deviations from Plan

None — plan executed exactly as written.

## Issues Encountered

None. The scaffold from `dotnet ef migrations add AddGroupSchema` produced the correct table/column/index structure; only the step ordering and data-seeding SQL required authoring.

**Test count note:** The checkpoint approval reported 194 tests (55 unit + 139 integration) vs. the plan's 191 baseline. The plan's 191 figure was the integration-test-only baseline from Phase 26. The 3 additional tests are pre-existing unit tests counted differently; no new tests were added in this plan.

## User Setup Required

None — no external service configuration required. The migration is not yet applied to a real database; it will auto-apply on next `dotnet run` or `docker-compose up` (plan 03 handles the apply + verification step).

## Deployment Constraint

**Phase 27 must NOT be deployed to production standalone.** The migration deletes Player/DungeonMaster/Admin rows from `AspNetUserRoles`. The existing `DungeonMasterHandler` and `AdminHandler` use Identity role claims; after this migration runs, new logins will find no Player/DM/Admin Identity roles and authorization will fail. Phase 27 must be deployed together with Phases 28–29, or during a maintenance window before any user logs in again.

## Next Phase Readiness

- Migration file is complete and builds; it will auto-apply on next `dotnet run` or container start
- Plan 03 applies the migration against a real SQL Server dev database and verifies row counts (GROUP-04/05/06 data correctness)
- Phase 28 can proceed to add EF Core Global Query Filters on QuestEntity and ShopItemEntity — GroupId is now present on both entities with the FK pointing to Groups

---
*Phase: 27-group-schema-foundation*
*Completed: 2026-06-30*

## Self-Check: PASSED

Files verified:
- FOUND: QuestBoard.Repository/Migrations/20260630055221_AddGroupSchema.cs
- FOUND: QuestBoard.Repository/Migrations/20260630055221_AddGroupSchema.Designer.cs
- FOUND: QuestBoard.Repository/Migrations/QuestBoardContextModelSnapshot.cs
- FOUND: QuestBoard.IntegrationTests/Helpers/TestDataHelper.cs

Commits verified:
- FOUND: cf07e09 (Task 1 — TestDataHelper GroupId=1)
- FOUND: 3667eba (Task 2 — AddGroupSchema migration)
