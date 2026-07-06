---
phase: 57-add-an-npc-directory-dm-only-creation-of-group-bound-npcs-na
plan: 02
subsystem: database
tags: [ef-core, entity, migration, multi-tenancy, query-filter]

# Dependency graph
requires: []
provides:
  - ContactEntity, ContactImageEntity, ContactNoteEntity EF Core entities
  - QuestBoardContext DbSet registrations and fail-closed group query filters for all three
  - AddContactsFeature migration creating Contacts/ContactImages/ContactNotes tables
affects: [57-03, 57-04, 57-05, 57-06]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Fail-closed group-scoped HasQueryFilter (no SuperAdmin bypass) mirroring CharacterEntity's Phase 49/55 shape"
    - "1:1 FK-as-PK image table (ContactImageEntity), verbatim-renamed from CharacterImageEntity"
    - "Authored+timestamped note child collection (ContactNoteEntity) with Cascade-on-parent-delete + NoAction-on-author-delete"

key-files:
  created:
    - QuestBoard.Repository/Entities/ContactEntity.cs
    - QuestBoard.Repository/Entities/ContactImageEntity.cs
    - QuestBoard.Repository/Entities/ContactNoteEntity.cs
    - QuestBoard.Repository/Migrations/20260706193921_AddContactsFeature.cs
    - QuestBoard.Repository/Migrations/20260706193921_AddContactsFeature.Designer.cs
  modified:
    - QuestBoard.Repository/Entities/QuestBoardContext.cs
    - QuestBoard.Repository/Migrations/QuestBoardContextModelSnapshot.cs

key-decisions:
  - "Contact entity family named per D-01/D-02 locked convention (ContactEntity/ContactImageEntity/ContactNoteEntity), never NPC"
  - "CreatedByUserId (not OwnerId) per D-07 — carries no authorization meaning, unlike Character's Owner"
  - "No SuperAdmin cross-group bypass on any Contact filter — same per-group-roster shape as CharacterEntity"

patterns-established:
  - "Contact -> Group uses NoAction (mirrors Character -> Group) to avoid cascade cycles"
  - "ContactNote -> Contact uses Cascade; ContactNote -> Author (UserEntity) uses NoAction to avoid a cascade cycle through UserEntity"

requirements-completed: []

# Metrics
duration: 3min
completed: 2026-07-06
status: complete
---

# Phase 57 Plan 02: Contacts Data Layer Summary

**Three new EF Core entities (ContactEntity, ContactImageEntity, ContactNoteEntity) mirroring the Character entity family, wired into QuestBoardContext with fail-closed group-scoped query filters and a matching AddContactsFeature migration.**

## Performance

- **Duration:** 3 min
- **Started:** 2026-07-06T19:37:11Z
- **Completed:** 2026-07-06T19:40:06Z
- **Tasks:** 3
- **Files modified:** 6 (3 created entities, 1 modified context, 2 migration files + snapshot)

## Accomplishments
- Created `ContactEntity` (Name, Description, TownCity, SubLocation, IsRevealed, CreatedByUserId, CreatedAt, ProfileImage, Notes collection, GroupId) mirroring `CharacterEntity` conventions without carrying over Character-specific fields (Level/SheetLink/Backstory/Status/Role)
- Created `ContactImageEntity` (1:1 FK-as-PK image table, verbatim-renamed from `CharacterImageEntity`)
- Created `ContactNoteEntity` (authored + timestamped freeform note child table, per RESEARCH.md Pattern 3)
- Registered all three DbSets on `QuestBoardContext`, added fail-closed `HasQueryFilter` registrations (no SuperAdmin bypass), and configured the Notes cascade / Author NoAction relationships
- Generated and inspected the `AddContactsFeature` migration — creates exactly three new tables with correct cascade/NoAction FK behaviors

## Task Commits

Each task was committed atomically:

1. **Task 1: Create ContactEntity, ContactImageEntity, ContactNoteEntity** - `4cb29a6` (feat)
2. **Task 2: Register entities + fail-closed query filters + relationships in QuestBoardContext** - `6bd0be5` (feat)
3. **Task 3: Generate the AddContactsFeature migration** - `0cd71ad` (feat)

_Note: this SUMMARY/plan-metadata commit follows separately per worktree execution protocol._

## Files Created/Modified
- `QuestBoard.Repository/Entities/ContactEntity.cs` - Contact EF entity (Name/Description/TownCity/SubLocation/IsRevealed/CreatedByUserId/Notes/GroupId)
- `QuestBoard.Repository/Entities/ContactImageEntity.cs` - 1:1 FK-as-PK profile image table
- `QuestBoard.Repository/Entities/ContactNoteEntity.cs` - authored+timestamped note child table
- `QuestBoard.Repository/Entities/QuestBoardContext.cs` - DbSets, fail-closed query filters, Notes/Author/Group relationships for all three Contact entities
- `QuestBoard.Repository/Migrations/20260706193921_AddContactsFeature.cs` + `.Designer.cs` - creates Contacts/ContactImages/ContactNotes tables
- `QuestBoard.Repository/Migrations/QuestBoardContextModelSnapshot.cs` - updated model snapshot (purely additive, no drift)

## Decisions Made
- Followed the plan and 57-PATTERNS.md exactly for entity shape, FK behaviors, and query filter placement.
- Also added an explicit `Contact -> Group` `NoAction` relationship (mirroring the existing `Character -> Group` NoAction config) to keep the cascade-cycle-prevention convention consistent across all group-scoped entities, even though the plan's task text didn't call it out by name — same intent as the existing Character block immediately above it in `QuestBoardContext.cs`.

## Deviations from Plan

None - plan executed exactly as written. The one additional `Contact -> Group` NoAction registration is a direct application of the existing documented convention (mirroring `CharacterEntity -> Group`) already present in the same file section, not a new pattern or scope change.

## Issues Encountered

None. `dotnet ef migrations add AddContactsFeature` (run from `QuestBoard.Service/`) succeeded on the first attempt with no drift — the generated migration and updated model snapshot contain only Contact-related additions.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- `ContactEntity`/`ContactImageEntity`/`ContactNoteEntity` and the `AddContactsFeature` migration are ready for the Domain layer (`Contact` domain model, `IContactRepository`/`ContactRepository`, `IContactService`/`ContactService`) that subsequent plans in this phase will build on top of.
- `QuestBoard.Repository` builds cleanly with 0 warnings/errors.
- No blockers.

---
*Phase: 57-add-an-npc-directory-dm-only-creation-of-group-bound-npcs-na*
*Completed: 2026-07-06*
