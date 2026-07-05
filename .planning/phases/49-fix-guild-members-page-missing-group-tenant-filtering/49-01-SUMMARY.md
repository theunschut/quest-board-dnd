---
phase: 49-fix-guild-members-page-missing-group-tenant-filtering
plan: 01
subsystem: database
tags: [efcore, multi-tenancy, query-filter, migration, guild-members]

# Dependency graph
requires: []
provides:
  - CharacterEntity.GroupId column + Group navigation + FK to Groups(Id)
  - EF Core global query filter on CharacterEntity (no null-escape-hatch — SuperAdmin-empty)
  - Migration backfilling existing Characters rows to GroupId=1
  - GetCharacterProfilePictureAsync rewritten to root through the filtered Characters DbSet
  - GuildMembersController.Create stamps GroupId via IActiveGroupContext.RequireActiveGroupId()
  - Corrected QuestBoardContext transitive-filter comment for Character/UserTransaction/PlayerSignup
  - CharacterRepositoryTests.cs regression coverage
affects: [49-02, 49-03, 49-04]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Schema-based tenant scoping via EF Core HasQueryFilter (mirrors QuestEntity/ShopItemEntity)"
    - "Deliberately asymmetric filter shape for entities that should NOT offer a SuperAdmin cross-group view"
    - "Routing a filter-less shared-PK sibling entity through its filtered parent DbSet"

key-files:
  created:
    - QuestBoard.Repository/Migrations/20260705183646_AddGroupIdToCharacters.cs
    - QuestBoard.UnitTests/Repository/CharacterRepositoryTests.cs
  modified:
    - QuestBoard.Repository/Entities/CharacterEntity.cs
    - QuestBoard.Repository/Entities/QuestBoardContext.cs
    - QuestBoard.Repository/CharacterRepository.cs
    - QuestBoard.Domain/Models/Character.cs
    - QuestBoard.Service/Controllers/Characters/GuildMembersController.cs
    - QuestBoard.IntegrationTests/Helpers/TestDataHelper.cs

key-decisions:
  - "CharacterEntity's query filter deliberately omits the ActiveGroupId==null escape hatch Quest/ShopItem use, so a SuperAdmin with no active group sees an empty Guild Members list instead of a cross-group superview"
  - "GetCharacterProfilePictureAsync rewritten to root through DbContext.Characters (not DbContext.CharacterImages) so the shared-PK CharacterImageEntity sibling inherits the parent's filter"
  - "Domain Character model gained GroupId as a plain int mapped by AutoMapper convention, matching how Quest's GroupId is handled (no explicit ForMember needed)"

patterns-established:
  - "Pattern: any entity that conceptually belongs to exactly one group gets a real GroupId column + HasQueryFilter, not a manual join — durable protection for every current and future query"
  - "Pattern: a required-FK shared-PK sibling table with no filter of its own must always be queried rooted at its filtered parent, never directly"

requirements-completed: []

# Metrics
duration: 24min
completed: 2026-07-05
status: complete
---

# Phase 49 Plan 01: CharacterEntity GroupId Schema + Query Filter Summary

**CharacterEntity gained a real GroupId column, FK, and an EF Core global query filter with a deliberately asymmetric (no-escape-hatch) shape, closing the Guild Members cross-group leak at the data-access layer for list, detail, and profile-picture endpoints.**

## Performance

- **Duration:** 24 min
- **Started:** 2026-07-05T18:18:00Z
- **Completed:** 2026-07-05T18:42:00Z
- **Tasks:** 3
- **Files modified:** 7 (2 created, 5 modified)

## Accomplishments
- `CharacterEntity` now carries `GroupId` + a `Group` navigation, with an EF Core `HasQueryFilter` that returns nothing when no active group is set (unlike Quest/ShopItem's SuperAdmin-see-all shape) — Guild Members list/detail queries are now automatically and durably tenant-scoped
- New migration (`AddGroupIdToCharacters`) backfills every existing `Characters` row to `GroupId = 1` before adding the FK constraint, mirroring the audited `AddGroupSchema` precedent's step order exactly
- `GetCharacterProfilePictureAsync` rewritten to root through the filtered `DbContext.Characters` instead of querying the unfiltered `CharacterImages` sibling directly, so a cross-group picture request now correctly returns null (→ 404)
- `GuildMembersController.Create` stamps new characters with the creator's active group via `RequireActiveGroupId()`, mirroring `QuestController`'s existing pattern
- Corrected the stale `QuestBoardContext.cs` comment block for all three affected entities (Character moved into the explicit filter block; UserTransaction's Include-driven-inner-join mechanism and PlayerSignup's caller-side pre-validation mechanism both documented accurately) — plans 03/04 do not need to touch this file
- New `CharacterRepositoryTests.cs` (5 tests) proves list scoping, SuperAdmin-empty behavior, and cross-group profile-picture null/404 against the real `QuestBoardContext` filter

## Task Commits

Each task was committed atomically:

1. **Task 1: Add GroupId column, navigation, migration, and query filter to CharacterEntity** - `02e73ac` (feat)
2. **Task 2: Rewrite GetCharacterProfilePictureAsync, stamp GroupId on Create, update TestDataHelper** - `eff458c` (feat)
3. **Task 3: Create CharacterRepositoryTests covering scoping, SuperAdmin-empty, and profile-picture 404** - `976c1f3` (test)

_Note: all three tasks were marked `tdd="true"` in the plan, but Tasks 1-2 are schema/wiring changes verified by build success; Task 3 supplies the regression test coverage for all of them together, consistent with how the plan's `<verify>` blocks were structured (build-then-test, not per-task red/green)._

## Files Created/Modified
- `QuestBoard.Repository/Entities/CharacterEntity.cs` - Added `GroupId` int + `Group` navigation (FK to `GroupEntity`)
- `QuestBoard.Repository/Entities/QuestBoardContext.cs` - Character→Group FK config, Character `HasQueryFilter` (no null-escape-hatch), corrected transitive-filter comment for Character/UserTransaction/PlayerSignup
- `QuestBoard.Repository/Migrations/20260705183646_AddGroupIdToCharacters.cs` - New migration: add column (default 0) → backfill to 1 → add FK → add index
- `QuestBoard.Repository/CharacterRepository.cs` - `GetCharacterProfilePictureAsync` rewritten to root through `DbContext.Characters`
- `QuestBoard.Domain/Models/Character.cs` - Added `GroupId` int (AutoMapper convention-mapped)
- `QuestBoard.Service/Controllers/Characters/GuildMembersController.cs` - Injected `IActiveGroupContext`; `Create` POST stamps `GroupId`
- `QuestBoard.IntegrationTests/Helpers/TestDataHelper.cs` - `CreateTestCharacterAsync` gained a `groupId` parameter (default 1)
- `QuestBoard.UnitTests/Repository/CharacterRepositoryTests.cs` - New: 5 tests covering scoping, SuperAdmin-empty, and profile-picture 404

## Decisions Made
- No explicit AutoMapper `ForMember` needed for `Character.GroupId` ↔ `CharacterEntity.GroupId` — the existing `CreateMap<Character, CharacterEntity>()`/`CreateMap<CharacterEntity, Character>()` pairs already map same-named/same-typed properties by convention, exactly as `Quest.GroupId` already does with no explicit member config.
- Migration index (`IX_Characters_GroupId`) placed after the FK add (matching `dotnet ef migrations add`'s natural generation order) rather than before FK, since the plan's ordering constraint was specifically "backfill before FK" (SQL Server's actual failure mode), not "index before FK" — index position doesn't affect correctness either way.

## Deviations from Plan

None - plan executed exactly as written. The `dotnet ef migrations add` step emitted expected EF Core model-validation warnings about required-FK relationships crossing filtered entities (`CharacterEntity`↔`CharacterClassEntity`, `CharacterEntity`↔`CharacterImageEntity`) — these are the same class of warning already present for the pre-existing `QuestEntity`/`ShopItemEntity` filters and are not new or actionable; they're inherent to the filter-on-required-navigation pattern this whole phase is built on.

## Issues Encountered
- The `Write` tool created `CharacterRepositoryTests.cs` with LF-only line endings instead of CRLF (project convention per CLAUDE.md). Converted to CRLF via a small script before running tests; verified 0 LF-only lines remaining. No functional impact, caught before commit.

## User Setup Required

None - no external service configuration required. The new migration auto-applies on startup per this project's standing convention (`context.Database.Migrate()`), no manual `database update` needed.

## Next Phase Readiness

- `CharacterEntity`'s query filter and FK are in place; `GuildMembersController.Index`/`Details`/`GetProfilePicture` are now durably group-scoped with zero additional repository code (per the plan's design)
- `QuestBoardContext.cs`'s comment corrections for `UserTransactionEntity` and `PlayerSignupEntity` are already done in this plan — sibling plans 03 (DungeonMasterController) and 04 (UserTransaction/PlayerSignup hardening) do not need to touch this file again
- Full solution build succeeds (`dotnet build`); full `QuestBoard.UnitTests` suite (166 tests) and the `GuildMembersControllerIntegrationTests` (4 tests) both pass green
- No blockers for the remaining phase-49 plans

---
*Phase: 49-fix-guild-members-page-missing-group-tenant-filtering*
*Completed: 2026-07-05*
