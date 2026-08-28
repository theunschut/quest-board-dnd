---
phase: 76-recurring-event-series
plan: 02
subsystem: database
tags: [ef-core, sql-server, automapper, migrations, entities]

# Dependency graph
requires: []
provides:
  - EventSeriesEntity template columns (Title, Description, StartTime, EndDate)
  - EventEntity/Event CancelledAt tombstone marker (with computed IsCancelled)
  - EventSeries domain model and its EntityProfile AutoMapper pair
  - SeriesRunwayStatus, SeriesRemovalImpact, EventSeriesOptions supporting domain types
  - EventEditScope enum
  - Filtered unique index IX_Events_SeriesId_SeriesSlotIndex
  - AddSeriesRecurrence EF Core migration
affects: [76-03, 76-04, 76-05, 76-06, 76-07, 76-08, 76-09, 76-10, 76-11, 76-12]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Nullable timestamp (CancelledAt) as a tombstone marker, matching the EventSignupEntity.UpdatedAt/HasAnswered precedent, instead of a boolean flag"
    - "Filtered unique index with an explicit HasFilter string rather than relying on the provider's implicit nullable-column handling"
    - "Explicitly declaring the FK's own single-column index alongside a new composite index that shares its leading column, so EF Core's migration tooling does not treat the single-column index as redundant and drop it"

key-files:
  created:
    - QuestBoard.Domain/Models/EventSeries.cs
    - QuestBoard.Domain/Models/SeriesRunwayStatus.cs
    - QuestBoard.Domain/Models/SeriesRemovalImpact.cs
    - QuestBoard.Domain/Models/EventSeriesOptions.cs
    - QuestBoard.Domain/Enums/EventEditScope.cs
    - QuestBoard.Repository/Migrations/20260828130415_AddSeriesRecurrence.cs
    - QuestBoard.Repository/Migrations/20260828130415_AddSeriesRecurrence.Designer.cs
  modified:
    - QuestBoard.Repository/Entities/EventSeriesEntity.cs
    - QuestBoard.Repository/Entities/EventEntity.cs
    - QuestBoard.Repository/Entities/QuestBoardContext.cs
    - QuestBoard.Domain/Models/Event.cs
    - QuestBoard.Repository/Automapper/EntityProfile.cs
    - QuestBoard.Repository/Migrations/QuestBoardContextModelSnapshot.cs

key-decisions:
  - "EventSeriesOptions is a plain class (not a record like EmailSettings) per the plan's explicit shape instruction, matching the options-binding convention used elsewhere in the codebase"
  - "Explicitly declared modelBuilder.Entity<EventEntity>().HasIndex(e => e.SeriesId) so the new composite (SeriesId, SeriesSlotIndex) unique index does not make EF Core's migration generator treat the FK's conventional index as redundant and auto-drop it"

requirements-completed: [EVTRECUR-04, EVTRECUR-07]

coverage:
  - id: D1
    description: "EventSeriesEntity carries a Title/Description/StartTime template and an EndDate; the stale 'no code reads or writes it yet' class comment is replaced"
    requirement: "EVTRECUR-04"
    verification:
      - kind: unit
        ref: "dotnet build (0 errors) + grep assertions on EventSeriesEntity.cs per plan acceptance criteria"
        status: pass
    human_judgment: false
  - id: D2
    description: "EventEntity/Event gain a CancelledAt tombstone timestamp; Event.IsCancelled is a get-only computed property"
    requirement: "EVTRECUR-04"
    verification:
      - kind: unit
        ref: "dotnet build (0 errors); grep assertions on EventEntity.cs and Event.cs per plan acceptance criteria"
        status: pass
    human_judgment: false
  - id: D3
    description: "New domain models (EventSeries, SeriesRunwayStatus, SeriesRemovalImpact, EventSeriesOptions) and EventEditScope enum exist and compile; EntityProfile wires the EventSeriesEntity<->EventSeries AutoMapper pair"
    requirement: "EVTRECUR-04"
    verification:
      - kind: unit
        ref: "dotnet build (0 errors); grep assertions confirming file existence and CreateMap<EventSeriesEntity, EventSeries>()/CreateMap<EventSeries, EventSeriesEntity>() per plan acceptance criteria"
        status: pass
    human_judgment: false
  - id: D4
    description: "Filtered unique index IX_Events_SeriesId_SeriesSlotIndex declared in OnModelCreating and created by the AddSeriesRecurrence migration; FK_Events_EventSeries_SeriesId delete behaviour left unchanged (no cascade); IX_Events_SeriesId preserved"
    requirement: "EVTRECUR-07"
    verification:
      - kind: unit
        ref: "QuestBoard.UnitTests QuestBoardContextFilterTests (8/8 pass); grep assertions on migration file (unique: true, HasFilter, AddColumn entries, defaultValue empty string, no cascade) per plan acceptance criteria"
        status: pass
      - kind: integration
        ref: "dotnet test (full suite): QuestBoard.UnitTests 333/333 pass, QuestBoard.IntegrationTests 498/498 pass — no regressions from the entity/mapping/migration changes"
        status: pass
    human_judgment: false

# Metrics
duration: ~20min
completed: 2026-08-28
status: complete
---

# Phase 76 Plan 02: Series Schema Foundation Summary

**EventSeries/EventEntity extended with template fields, an end date, and a cancelled-occurrence tombstone; a filtered unique (SeriesId, SeriesSlotIndex) index and the AddSeriesRecurrence EF Core migration close the idempotency gap Hangfire's global retry policy would otherwise reopen.**

## Performance

- **Duration:** ~20 min
- **Started:** 2026-08-28T15:00:00+02:00 (approx.)
- **Completed:** 2026-08-28T15:06:34+02:00
- **Tasks:** 2
- **Files modified:** 13 (6 modified, 7 created)

## Accomplishments
- `EventSeriesEntity` now carries the template (`Title`, `Description`, `StartTime`) and lifecycle (`EndDate`) columns every generated occurrence is stamped from; the stale "no code reads or writes it yet" class comment is gone
- `EventEntity`/`Event` gained a `CancelledAt` tombstone timestamp (`Event.IsCancelled` is get-only, so it can never be bound from a form)
- New domain types landed: `EventSeries`, `SeriesRunwayStatus`, `SeriesRemovalImpact`, `EventSeriesOptions`, and the `EventEditScope` enum, plus the `EventSeriesEntity <-> EventSeries` AutoMapper pair
- Declared and migrated the filtered unique index `IX_Events_SeriesId_SeriesSlotIndex` — the database-level backstop against a duplicate occurrence surviving a mid-run Hangfire retry
- `AddSeriesRecurrence` migration generated, inspected, and confirmed to add all five columns, declare the index correctly, and leave `FK_Events_EventSeries_SeriesId`'s delete behaviour untouched

## Task Commits

Each task was committed atomically:

1. **Task 1: Extend the entities, add the EventSeries domain model and supporting types, and wire the AutoMapper pair** - `27b3ee2` (feat)
2. **Task 2: Declare the filtered unique idempotency index and generate the AddSeriesRecurrence migration** - `79838c2` (feat)

## Files Created/Modified
- `QuestBoard.Repository/Entities/EventSeriesEntity.cs` - added `Title`, `Description`, `StartTime`, `EndDate`; rewrote stale class comment
- `QuestBoard.Repository/Entities/EventEntity.cs` - added `CancelledAt`
- `QuestBoard.Domain/Models/Event.cs` - added `CancelledAt` and computed `IsCancelled`
- `QuestBoard.Domain/Models/EventSeries.cs` - new domain model mirroring the entity plus validation attributes
- `QuestBoard.Domain/Models/SeriesRunwayStatus.cs` - new projection for the calendar horizon banner
- `QuestBoard.Domain/Models/SeriesRemovalImpact.cs` - new projection for the series removal confirm
- `QuestBoard.Domain/Models/EventSeriesOptions.cs` - new options class (`RunwaySize`, `PreviewCount`)
- `QuestBoard.Domain/Enums/EventEditScope.cs` - new enum (`OnlyThisEvent`, `ThisAndFutureEvents`)
- `QuestBoard.Repository/Automapper/EntityProfile.cs` - added the `EventSeriesEntity <-> EventSeries` map pair
- `QuestBoard.Repository/Entities/QuestBoardContext.cs` - declared the filtered unique index and an explicit `IX_Events_SeriesId` index
- `QuestBoard.Repository/Migrations/20260828130415_AddSeriesRecurrence.cs` + `.Designer.cs` - new migration
- `QuestBoard.Repository/Migrations/QuestBoardContextModelSnapshot.cs` - regenerated snapshot

## Decisions Made
- `EventSeriesOptions` is a plain class, matching the plan's explicit instruction, rather than the `record` shape `EmailSettings` uses — both are valid options-binding shapes in this codebase; the plan specified `class` for this one.
- Explicitly declared `modelBuilder.Entity<EventEntity>().HasIndex(e => e.SeriesId).HasDatabaseName("IX_Events_SeriesId")` before the new composite index. Without this, EF Core's migration generator treats the plain FK index as covered by the new composite index's leading column and silently drops it — contradicting the plan's explicit "leave `IX_Events_SeriesId` in place" instruction. Verified by regenerating the migration both ways and confirming the `DropIndex` for `IX_Events_SeriesId` disappears once the index is declared explicitly.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] EF Core's migration generator was auto-dropping `IX_Events_SeriesId`**
- **Found during:** Task 2 (migration generation)
- **Issue:** The first `dotnet ef migrations add` run produced a migration that included `DropIndex(name: "IX_Events_SeriesId", ...)`. EF Core's convention-based index deduplication saw that the new composite `(SeriesId, SeriesSlotIndex)` index shares `SeriesId` as its leading column and treated the plain FK index as redundant, removing it automatically. This directly contradicted the plan's action text: "Leave `IX_Events_SeriesId` in place — it is the FK's conventional index and removing it is churn this phase does not need."
- **Fix:** Removed the first migration (`dotnet ef migrations remove`), added an explicit `modelBuilder.Entity<EventEntity>().HasIndex(e => e.SeriesId).HasDatabaseName("IX_Events_SeriesId")` declaration in `OnModelCreating` immediately before the new composite index, then regenerated the migration. The regenerated migration no longer drops the index.
- **Files modified:** `QuestBoard.Repository/Entities/QuestBoardContext.cs`, `QuestBoard.Repository/Migrations/20260828130415_AddSeriesRecurrence.cs` (and `.Designer.cs`), `QuestBoard.Repository/Migrations/QuestBoardContextModelSnapshot.cs`
- **Verification:** Inspected the regenerated migration file — no `DropIndex` for `IX_Events_SeriesId` appears; `dotnet build` exits 0; full test suite (831 tests) passes with no regressions.
- **Committed in:** `79838c2` (Task 2 commit)

---

**Total deviations:** 1 auto-fixed (1 bug)
**Impact on plan:** Necessary correction to honor an explicit plan instruction that EF Core's default conventions would otherwise have silently violated. No scope creep — same migration, same columns, same index shape; only the FK index's survival was affected.

## Issues Encountered
None beyond the deviation documented above.

## User Setup Required
None - no external service configuration required. Migrations are auto-applied on startup via `context.Database.Migrate()`.

## Next Phase Readiness
- The schema foundation (template fields, end date, cancelled marker, idempotency index, domain model, AutoMapper pair) is in place for every later plan in Phase 76 to build against.
- No blockers. The `EventSeriesRepository`, `IEventSeriesService`, `EventSeriesDateGenerator`, and all controller/view work listed in the phase's "Artifacts this phase produces" section remain for subsequent plans.

---
*Phase: 76-recurring-event-series*
*Completed: 2026-08-28*

## Self-Check: PASSED

- FOUND: QuestBoard.Domain/Models/EventSeries.cs
- FOUND: QuestBoard.Domain/Models/SeriesRunwayStatus.cs
- FOUND: QuestBoard.Domain/Models/SeriesRemovalImpact.cs
- FOUND: QuestBoard.Domain/Models/EventSeriesOptions.cs
- FOUND: QuestBoard.Domain/Enums/EventEditScope.cs
- FOUND: QuestBoard.Repository/Migrations/20260828130415_AddSeriesRecurrence.cs
- FOUND: commit 27b3ee2 (Task 1)
- FOUND: commit 79838c2 (Task 2)
- FOUND: commit e8b7d6b (docs: complete plan)
