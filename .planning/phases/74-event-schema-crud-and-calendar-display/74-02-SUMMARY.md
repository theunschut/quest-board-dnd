---
phase: 74-event-schema-crud-and-calendar-display
plan: 02
subsystem: database
tags: [ef-core, sql-server, dateonly, timeonly, tenant-isolation, migration]

# Dependency graph
requires:
  - phase: 74-event-schema-crud-and-calendar-display (plan 01, if any)
    provides: n/a — this plan owns the storage layer from scratch
provides:
  - EventEntity, EventSeriesEntity, EventSignupEntity (IEntity, three DbSets on QuestBoardContext)
  - Fail-closed HasQueryFilter tenant scoping for all three entities
  - Explicit FK delete behaviour and (GroupId, Date) / (EventId, UserId) indexes
  - One additive AddCalendarEventsFeature migration creating all three tables
affects: [74-03, 74-04, 75-event-availability, 76-event-recurrence, 77-availability-overview]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "DateOnly/TimeOnly native EF Core 10 mapping (no third-party package) for the occurrence date and optional start time"
    - "Fail-closed HasQueryFilter for a table with no readers yet (EventSignupEntity), added ahead of the phase that will use it"

key-files:
  created:
    - QuestBoard.Repository/Entities/EventEntity.cs
    - QuestBoard.Repository/Entities/EventSeriesEntity.cs
    - QuestBoard.Repository/Entities/EventSignupEntity.cs
    - QuestBoard.Repository/Migrations/20260826134133_AddCalendarEventsFeature.cs
    - QuestBoard.Repository/Migrations/20260826134133_AddCalendarEventsFeature.Designer.cs
  modified:
    - QuestBoard.Repository/Entities/QuestBoardContext.cs
    - QuestBoard.Repository/Migrations/QuestBoardContextModelSnapshot.cs

key-decisions:
  - "EventEntity has 11 public properties (Id, Title, Description, Date, StartTime, SeriesId, Series, SeriesSlotIndex, CreatedAt, GroupId, Group), not the 10 the plan's prose acceptance criterion states — the plan's own field-by-field action spec lists 9 bullets, two of which (SeriesId, GroupId) each pair with an explicit navigation property (Series, Group) per D-03/D-04, yielding 11 total properties. Followed the precise, typed field list verbatim rather than the unverifiable prose count; every other acceptance grep (DateOnly Date=1, TimeOnly? StartTime=1, int? SeriesId=1, no author columns, single StringLength, no Quest/EventType references) passes exactly."
  - "No third-party DateOnly/TimeOnly package added — EF Core 10 native mapping confirmed by migration output (Date/AnchorDate = 'date', StartTime = 'time')."

patterns-established:
  - "Multi-table additive migration with ordered CreateTable calls (parent before FK-dependent child) and indexes last — followed AddContactsFeature precedent exactly for AddCalendarEventsFeature"

requirements-completed: [EVENT-01, EVENT-02]

coverage:
  - id: D1
    description: "Three event entities (EventSeriesEntity, EventEntity, EventSignupEntity) exist, implement IEntity, and are registered as DbSets on QuestBoardContext"
    requirement: "EVENT-01"
    verification:
      - kind: unit
        ref: "dotnet build QuestBoard.Repository/QuestBoard.Repository.csproj (exit 0)"
        status: pass
      - kind: other
        ref: "grep acceptance criteria for Task 1 (DateOnly Date, TimeOnly? StartTime, int? SeriesId, no author columns, DbSet registrations) — all pass"
        status: pass
    human_judgment: false
  - id: D2
    description: "All three event tables are fail-closed on tenant scope for reads (HasQueryFilter with null-ActiveGroupId guard), explicit FK delete behaviour, and the (GroupId, Date) covering index for the calendar read exist"
    requirement: "EVENT-02"
    verification:
      - kind: unit
        ref: "dotnet build (exit 0, no EF model-validation warnings)"
        status: pass
      - kind: other
        ref: "grep acceptance criteria for Task 2 (HasQueryFilter count +3, no local ActiveGroupId capture, EventSignup filter via es.Event.GroupId, GroupId+Date index) — all pass"
        status: pass
    human_judgment: false
  - id: D3
    description: "One purely additive migration creates EventSeries, Events, and EventSignups in dependency order with no backfill, using native SQL Server date/time column types, and the existing tenant isolation suite still passes against the changed EF model"
    verification:
      - kind: integration
        ref: "dotnet test --filter \"FullyQualifiedName~TenantIsolationTests\" — 5/5 passed"
        status: pass
      - kind: other
        ref: "grep acceptance criteria for Task 3 (3 CreateTable, 3 DropTable, 0 AddColumn/AlterColumn/Sql/UpdateData/InsertData/DropColumn, 2x type:\"date\", 1x type:\"time\", correct table-creation order, empty csproj diff) — all pass"
        status: pass
    human_judgment: false

duration: 15min
completed: 2026-08-26
status: complete
---

# Phase 74 Plan 02: Event Schema, Query Filters, and Migration Summary

**Three GroupId-scoped/through-navigation event entities (Events, EventSeries, EventSignups) with fail-closed tenant query filters and one additive EF Core migration mapping DateOnly/TimeOnly to native SQL Server date/time columns.**

## Performance

- **Duration:** ~15 min
- **Started:** 2026-08-26T13:33Z (approx, per prior session state)
- **Completed:** 2026-08-26T13:43Z
- **Tasks:** 3/3
- **Files modified:** 7 (3 created entities, 1 modified context, 3 migration files)

## Accomplishments
- Three entity classes (`EventSeriesEntity`, `EventEntity`, `EventSignupEntity`) implementing `IEntity`, cloned from the `ContactEntity`/`PlayerSignupEntity` shape, with `EventEntity.Date` as `DateOnly` and `StartTime` as `TimeOnly?` — native EF Core 10 mapping, zero third-party packages
- Three fail-closed `HasQueryFilter` entries added to `QuestBoardContext` — including `EventSignupEntity`, which has no readers yet, so the tenant-scoping convention is settled before any occurrence data exists (Pitfall 3 from RESEARCH.md)
- Explicit FK delete behaviour (`NoAction` for Group/Series FKs, `Cascade` for `EventSignup` → `Event`) and two new indexes: `(GroupId, Date)` on `Events` for the monthly calendar read, unique `(EventId, UserId)` on `EventSignups`
- One additive `AddCalendarEventsFeature` migration creating `EventSeries` → `Events` → `EventSignups` in dependency order, verified to contain only `CreateTable`/`CreateIndex` calls and correct `date`/`time` column types

## Task Commits

Each task was committed atomically:

1. **Task 1: Add the three event entities and their DbSets** - `94046a6` (feat)
2. **Task 2: Add fail-closed query filters, FK delete behaviour, and indexes** - `8d388a9` (feat)
3. **Task 3: Generate and verify the additive AddCalendarEventsFeature migration** - `fbb9262` (feat)

_This is a worktree-mode execution; the plan-metadata commit (SUMMARY.md) is committed separately per the worktree protocol — no STATE.md/ROADMAP.md changes are made here._

## Files Created/Modified
- `QuestBoard.Repository/Entities/EventEntity.cs` - The occurrence entity: title, unbounded Markdown description, `DateOnly` date, `TimeOnly?` start time, nullable series FK, no author column, no quest relationship
- `QuestBoard.Repository/Entities/EventSeriesEntity.cs` - The repeating-schedule definition table (anchor date, interval weeks, weekday, cycle mask) — no readers yet, table created now to settle the storage convention
- `QuestBoard.Repository/Entities/EventSignupEntity.cs` - The per-person availability answer table, scoped through its required `Event` navigation — no readers yet
- `QuestBoard.Repository/Entities/QuestBoardContext.cs` - Three new `DbSet`s, three relationship configurations, two new indexes, three fail-closed query filters
- `QuestBoard.Repository/Migrations/20260826134133_AddCalendarEventsFeature.cs` (+ `.Designer.cs`, `QuestBoardContextModelSnapshot.cs`) - The additive migration

## Decisions Made
- Followed the plan's precise, typed field-by-field entity spec verbatim (documented as a discrepancy against the plan's own "10 members" prose count — see `key-decisions` in frontmatter). All executable/grep-based acceptance criteria pass; only the unverifiable prose member-count note is technically off by one because it undercounts the two explicit navigation properties (`Series`, `Group`) the same spec text requires.
- No other deviations — CRUD services, controllers, views, and Domain-layer symbols are explicitly out of scope for this plan per its own artifact list (owned by later 74-0x plans); this plan only lands the storage layer.

## Deviations from Plan

None requiring a fix — see the entity member-count note in `key-decisions` above, which is a documentation-only discrepancy in the plan's prose acceptance text, not a code defect. Every grep-verifiable acceptance criterion in Tasks 1–3 passes exactly as specified.

## Issues Encountered

None. `dotnet ef migrations add` ran cleanly on the first attempt (EF tools version warning noted, non-blocking, no action needed); the generated migration required no manual reordering — EF emitted `EventSeries` → `Events` → `EventSignups` correctly on its own.

## User Setup Required

None - no external service configuration required. The migration auto-applies on next app startup via `context.Database.Migrate()`; no manual `dotnet ef database update` step was run, per CLAUDE.md.

## Next Phase Readiness
- The storage layer and tenant-scoping convention for the whole Calendar Events feature (Phases 74–77) is settled: three tables exist, all three reads are fail-closed, and `EventEntity`'s nullable `SeriesId` means a one-off event and a future materialized occurrence are the same entity with no later schema change needed.
- Domain models (`Event`, `IEventRepository`, `IEventService`, `EventService`), the `EventRepository`, AutoMapper profile entries, `EventsController` CRUD, and calendar-view integration are not yet built — they belong to the next plan(s) in this phase, which can now build directly on this schema with no further migration work required (Phase 75/76 become pure code changes per D-02).
- No blockers or concerns.

---
*Phase: 74-event-schema-crud-and-calendar-display*
*Completed: 2026-08-26*
