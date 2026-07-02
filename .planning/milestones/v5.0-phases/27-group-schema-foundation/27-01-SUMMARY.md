---
phase: 27-group-schema-foundation
plan: 01
subsystem: database
tags: [efcore, sqlserver, multitenancy, group, migration, automapper]

# Dependency graph
requires: []
provides:
  - GroupRole enum (Player=0, DungeonMaster=1, Admin=2) in QuestBoard.Domain.Enums
  - GroupEntity EF entity with Id/Name/CreatedAt and UserGroups navigation
  - UserGroupEntity EF entity with UserId/GroupId/GroupRole(int) and FK navs
  - Group and UserGroup domain models
  - GroupId non-nullable FK on QuestEntity and ShopItemEntity
  - UserGroups navigation collection on UserEntity
  - QuestBoardContext DbSets for Groups and UserGroups with unique indexes and FK delete behaviors
  - EntityProfile mappings for GroupEntity<->Group and UserGroupEntity<->UserGroup with GroupRole int<->enum
affects: [27-group-schema-foundation/27-02, 28-tenant-isolation, 29-superadmin, 30-group-ux]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - Junction entity with auto-increment PK and composite unique index (not composite PK) — mirrors PlayerDateVoteEntity pattern
    - GroupRole stored as int on entity, cast to/from enum at AutoMapper boundary
    - NoAction delete behavior for Quest/ShopItem->Group FKs to prevent cascade cycles
    - Cascade delete behavior for UserGroup->User and UserGroup->Group FKs

key-files:
  created:
    - QuestBoard.Domain/Enums/GroupRole.cs
    - QuestBoard.Repository/Entities/GroupEntity.cs
    - QuestBoard.Repository/Entities/UserGroupEntity.cs
    - QuestBoard.Domain/Models/Group.cs
    - QuestBoard.Domain/Models/UserGroup.cs
  modified:
    - QuestBoard.Repository/Entities/QuestEntity.cs
    - QuestBoard.Repository/Entities/ShopItemEntity.cs
    - QuestBoard.Repository/Entities/UserEntity.cs
    - QuestBoard.Repository/Entities/QuestBoardContext.cs
    - QuestBoard.Repository/Automapper/EntityProfile.cs

key-decisions:
  - "GroupRole stored as int on UserGroupEntity; enum cast happens at AutoMapper boundary (consistent with SignupRole, CharacterStatus patterns)"
  - "UserGroupEntity uses auto-increment int PK with composite unique index on (UserId, GroupId), not a composite PK — mirrors PlayerDateVoteEntity pattern, avoids EF composite-PK pitfall"
  - "Quest/ShopItem->Group FK uses NoAction delete to prevent cascade cycles with SQL Server multiple-path delete rule"
  - "UserGroup->User and UserGroup->Group FKs use Cascade delete so removing a user or group cleans up memberships automatically"
  - "Groups.Name has a unique index enforced at DB layer (D-08)"

patterns-established:
  - "Group membership junction: auto-PK + composite unique index, not composite PK"
  - "Per-group role stored as int entity property, GroupRole enum lives only in Domain"

requirements-completed: [GROUP-01, GROUP-02, GROUP-03]

# Metrics
duration: 15min
completed: 2026-06-30
---

# Phase 27 Plan 01: Group Schema Foundation Summary

**EF Core multi-group model: GroupRole enum, GroupEntity/UserGroupEntity with FK delete rules, GroupId on QuestEntity/ShopItemEntity, and GroupRole int<->enum AutoMapper mapping — solution builds with zero errors, no migration yet.**

## Performance

- **Duration:** ~15 min
- **Started:** 2026-06-30T05:33:00Z
- **Completed:** 2026-06-30T05:48:03Z
- **Tasks:** 2
- **Files modified:** 10 (5 created, 5 modified)

## Accomplishments

- Created GroupRole enum (Player=0, DungeonMaster=1, Admin=2) and Group/UserGroup domain models in the Domain layer
- Created GroupEntity and UserGroupEntity with proper FK patterns, attributes, and navigation properties in the Repository layer
- Added GroupId non-nullable FK and Group navigation to QuestEntity and ShopItemEntity; added UserGroups navigation collection to UserEntity (no scalar column added to AspNetUsers)
- Registered Groups and UserGroups DbSets in QuestBoardContext, configured unique indexes and four FK delete behaviors
- Added EntityProfile mappings with GroupRole int<->enum cast, consistent with existing ShopItem enum patterns
- Full solution (6 projects) builds with zero errors

## Task Commits

Each task was committed atomically:

1. **Task 1: Add GroupRole enum, Group/UserGroup entities and domain models, and GroupId FK/nav properties** - `a88fe75` (feat)
2. **Task 2: Register DbSets, configure relationships/indexes in QuestBoardContext, and add EntityProfile GroupRole mapping** - `1444c9e` (feat)

**Plan metadata:** (docs commit follows)

## Files Created/Modified

- `QuestBoard.Domain/Enums/GroupRole.cs` - GroupRole enum with Player=0, DungeonMaster=1, Admin=2
- `QuestBoard.Repository/Entities/GroupEntity.cs` - [Table("Groups")], IEntity, Name unique index, UserGroups navigation
- `QuestBoard.Repository/Entities/UserGroupEntity.cs` - [Table("UserGroups")], IEntity, UserId/GroupId/GroupRole(int) with FK navs
- `QuestBoard.Domain/Models/Group.cs` - Group domain model implementing IModel
- `QuestBoard.Domain/Models/UserGroup.cs` - UserGroup domain model with GroupRole enum property
- `QuestBoard.Repository/Entities/QuestEntity.cs` - Added GroupId int FK and Group nav property
- `QuestBoard.Repository/Entities/ShopItemEntity.cs` - Added GroupId int FK and Group nav property
- `QuestBoard.Repository/Entities/UserEntity.cs` - Added UserGroups navigation collection only (no scalar column)
- `QuestBoard.Repository/Entities/QuestBoardContext.cs` - Added Groups/UserGroups DbSets and six OnModelCreating configurations
- `QuestBoard.Repository/Automapper/EntityProfile.cs` - Added Group ReverseMap and UserGroup<->UserGroupEntity GroupRole int<->enum maps

## Decisions Made

- GroupRole stored as int on UserGroupEntity; enum cast at AutoMapper boundary matches existing SignupRole and CharacterStatus patterns
- UserGroupEntity uses auto-increment int PK with composite unique index on (UserId, GroupId) — not a composite PK, per the research anti-pattern note (D-06)
- Quest/ShopItem->Group FKs use NoAction delete behavior (D-10) to prevent SQL Server cascade cycle errors
- UserGroup->User and UserGroup->Group FKs use Cascade (D-09) so membership rows are cleaned up automatically

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

None.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- EF model complete; QuestBoardContext is the single source of truth for the migration scaffolding
- Plan 02 can now run `dotnet ef migrations add AddGroupSchema` against this model state
- The GroupId FK on QuestEntity and ShopItemEntity is non-nullable with default 0 — the migration (plan 02) must handle seeding a default group before adding the NOT NULL column to existing rows

---
*Phase: 27-group-schema-foundation*
*Completed: 2026-06-30*

## Self-Check: PASSED

Files verified:
- FOUND: QuestBoard.Domain/Enums/GroupRole.cs
- FOUND: QuestBoard.Repository/Entities/GroupEntity.cs
- FOUND: QuestBoard.Repository/Entities/UserGroupEntity.cs
- FOUND: QuestBoard.Domain/Models/Group.cs
- FOUND: QuestBoard.Domain/Models/UserGroup.cs

Commits verified:
- FOUND: a88fe75 (Task 1)
- FOUND: 1444c9e (Task 2)

No migration file created in QuestBoard.Repository/Migrations/ — confirmed correct.
