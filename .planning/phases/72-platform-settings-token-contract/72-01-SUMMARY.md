---
phase: 72-platform-settings-token-contract
plan: 01
subsystem: database
tags: [ef-core, automapper, sql-server, key-value-store, migrations]

# Dependency graph
requires: []
provides:
  - "PlatformSetting domain model + OmphalosSettings resolved DTO"
  - "PlatformSettingKeys constants (OmphalosUrl, OmphalosSharedSecret, OmphalosEnabled)"
  - "IPlatformSettingRepository / IPlatformSettingService interface contracts (cascade/scope/upsert/clear surface)"
  - "PlatformSettingEntity EF entity + QuestBoardContext.PlatformSettings DbSet"
  - "AddPlatformSettings migration (table + two filtered unique indexes)"
affects: [72-02, 72-03, 72-04, 72-05, 73-token-generator]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Generic key-value settings table (Key/Value/nullable GroupId) instead of a fixed-column singleton row"
    - "Two filtered unique indexes (HasFilter \"[GroupId] IS NULL\" / \"IS NOT NULL\") to enforce one instance-default row per key and one override row per (key, group) on a nullable-column composite"
    - "Scope-oriented service interface (not IBaseService<T>) for a settings surface resolved by (key, scope) rather than by Id"

key-files:
  created:
    - QuestBoard.Domain/Models/PlatformSetting.cs
    - QuestBoard.Domain/Models/OmphalosSettings.cs
    - QuestBoard.Domain/Constants/PlatformSettingKeys.cs
    - QuestBoard.Domain/Interfaces/IPlatformSettingRepository.cs
    - QuestBoard.Domain/Interfaces/IPlatformSettingService.cs
    - QuestBoard.Repository/Entities/PlatformSettingEntity.cs
    - QuestBoard.Repository/Migrations/20260711143220_AddPlatformSettings.cs
  modified:
    - QuestBoard.Repository/Entities/QuestBoardContext.cs
    - QuestBoard.Repository/Automapper/EntityProfile.cs
    - QuestBoard.Repository/Migrations/QuestBoardContextModelSnapshot.cs

key-decisions:
  - "Cascade delete on PlatformSettingEntity.GroupId FK — a single, non-cyclic FK into a settings table, so deleting a group takes its own overrides with it without a cascade cycle."
  - "No HasQueryFilter registered on PlatformSettingEntity — instance-default rows (GroupId == null) must stay visible regardless of the active group, same as GroupEntity/UserEntity."

patterns-established:
  - "Filtered unique index pair for nullable-composite uniqueness: plain composite unique indexes treat every NULL as distinct under SQL Server, so a single-column filtered index (Key, filter GroupId IS NULL) plus a composite filtered index (Key+GroupId, filter GroupId IS NOT NULL) is the correct EF Core Fluent API shape for this codebase's first key-value + scope table."

requirements-completed: [SETT-06, SETT-08]

coverage:
  - id: D1
    description: "PlatformSetting domain model, OmphalosSettings DTO, and PlatformSettingKeys constants compile and expose the locked shape (Id/Key/Value/GroupId, Url/SharedSecret/IsEnabled/HasSecret)"
    requirement: SETT-06
    verification:
      - kind: unit
        ref: "dotnet build QuestBoard.Domain"
        status: pass
    human_judgment: false
  - id: D2
    description: "IPlatformSettingRepository and IPlatformSettingService declare the full cascade/scope/upsert/clear method surface for 72-03 to implement against"
    requirement: SETT-06
    verification:
      - kind: unit
        ref: "dotnet build QuestBoard.Domain"
        status: pass
    human_judgment: false
  - id: D3
    description: "PlatformSettingEntity, QuestBoardContext.PlatformSettings DbSet, and the AddPlatformSettings migration create the PlatformSettings table with two filtered unique indexes and a nullable cascading Group FK"
    requirement: SETT-08
    verification:
      - kind: unit
        ref: "dotnet build QuestBoard.Repository"
        status: pass
      - kind: other
        ref: "grep -rl CreateTable QuestBoard.Repository/Migrations | xargs grep -l PlatformSettings"
        status: pass
    human_judgment: false

# Metrics
duration: 24min
completed: 2026-07-11
status: complete
---

# Phase 72 Plan 01: Platform Settings Persistence Foundation Summary

**Generic key-value `PlatformSettingEntity` (Key/Value/nullable GroupId) with two filtered unique indexes enforcing exactly one instance-default row per key and one override row per (key, group), plus the full Domain contract surface (`IPlatformSettingRepository`/`IPlatformSettingService`) for the cascade lookup 72-03 implements.**

## Performance

- **Duration:** 24 min
- **Started:** 2026-07-11T14:09:xx
- **Completed:** 2026-07-11T14:33:27Z
- **Tasks:** 2
- **Files modified:** 8 (5 created in Task 1, 3 created + 3 modified in Task 2)

## Accomplishments
- `PlatformSetting` (IModel-implementing domain model) and `OmphalosSettings` (resolved-settings DTO with `HasSecret` computed property, doc comment discipline on `SharedSecret` so controllers know never to map it into a ViewModel)
- `PlatformSettingKeys` constants class — the single source of truth for the three persisted key strings
- `IPlatformSettingRepository`/`IPlatformSettingService` — full cascade/scope/upsert/clear method surface, service interface deliberately not extending `IBaseService<T>` since a key-value settings row is resolved by (key, scope), not by Id
- `PlatformSettingEntity` EF entity + `QuestBoardContext.PlatformSettings` DbSet + nullable cascading Group FK + two filtered unique indexes (`[GroupId] IS NULL` for the instance default, `[GroupId] IS NOT NULL` for group overrides)
- `EntityProfile` AutoMapper maps in both directions, with the `Group` navigation ignored on the `PlatformSetting -> PlatformSettingEntity` direction
- `AddPlatformSettings` EF Core migration generated and verified to create the table with both filtered unique indexes and the FK

## Task Commits

Each task was committed atomically:

1. **Task 1: Domain contracts — model, DTO, key constants, and repository/service interfaces** - `ca6be6c4` (feat)
2. **Task 2: EF entity, AutoMapper map, DbContext registration, and AddPlatformSettings migration** - `ff608354` (feat)

**Plan metadata:** pending (docs: complete plan)

## Files Created/Modified
- `QuestBoard.Domain/Models/PlatformSetting.cs` - domain twin of the EF entity (Id, Key, Value, int? GroupId)
- `QuestBoard.Domain/Models/OmphalosSettings.cs` - resolved-settings DTO (Url, SharedSecret, IsEnabled, computed HasSecret)
- `QuestBoard.Domain/Constants/PlatformSettingKeys.cs` - the three persisted key-string constants
- `QuestBoard.Domain/Interfaces/IPlatformSettingRepository.cs` - cascade/scope/upsert/clear repository contract
- `QuestBoard.Domain/Interfaces/IPlatformSettingService.cs` - scope-oriented service contract, not IBaseService<T>
- `QuestBoard.Repository/Entities/PlatformSettingEntity.cs` - `[Table("PlatformSettings")]` EF entity with nullable Group FK
- `QuestBoard.Repository/Entities/QuestBoardContext.cs` - added PlatformSettings DbSet, FK config, two filtered unique indexes
- `QuestBoard.Repository/Automapper/EntityProfile.cs` - PlatformSettingEntity <-> PlatformSetting maps
- `QuestBoard.Repository/Migrations/20260711143220_AddPlatformSettings.cs` - generated migration
- `QuestBoard.Repository/Migrations/QuestBoardContextModelSnapshot.cs` - EF-generated snapshot update

## Decisions Made
- Cascade delete on the `GroupId` FK, documented inline as safe (single, non-cyclic FK into a settings table)
- No `HasQueryFilter` on `PlatformSettingEntity`, documented inline, matching `GroupEntity`/`UserEntity`'s unfiltered shape so instance-default rows always remain visible

## Deviations from Plan

None - plan executed exactly as written. Both tasks' acceptance criteria were verified directly against the plan's automated checks (`dotnet build` for each project, plus the migration's `CreateTable`/filter-clause grep check) with zero errors.

## Issues Encountered
None.

## User Setup Required

None - no external service configuration required. The migration auto-applies on next app startup via `context.Database.Migrate()`, per project convention; no manual `dotnet ef database update` needed.

## Next Phase Readiness
- The full Domain contract surface (`IPlatformSettingRepository`, `IPlatformSettingService`) is ready for 72-03 to implement (repository/service implementation classes, DI registration).
- The `PlatformSettings` table, its cascade FK, and its two filtered unique indexes are ready to receive rows from 72-03 onward; no behavior wiring landed in this plan by design.
- 72-02 (running in parallel, no file overlap) is unaffected by this plan's changes.

---
*Phase: 72-platform-settings-token-contract*
*Completed: 2026-07-11*

## Self-Check: PASSED

All 7 created files verified present on disk; all 3 task/summary commit hashes (`ca6be6c4`, `ff608354`, `997bb61f`) verified present in git log.
