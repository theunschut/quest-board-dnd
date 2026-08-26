---
phase: 74-event-schema-crud-and-calendar-display
plan: 03
subsystem: database
tags: [automapper, dependency-injection, tenant-isolation, clean-architecture]

# Dependency graph
requires:
  - phase: 74-event-schema-crud-and-calendar-display (plan 02)
    provides: EventEntity/EventSeriesEntity/EventSignupEntity, fail-closed HasQueryFilter tenant scoping, AddCalendarEventsFeature migration
provides:
  - Event domain model (Id, Title, Description, Date, StartTime, SeriesId, SeriesSlotIndex, CreatedAt, GroupId)
  - IEventRepository / IEventService with GetEventsForCalendarAsync, GetEventWithDetailsAsync, GetSeriesGroupIdAsync
  - EventRepository / EventService implementations, group-scoped entirely via query filters on reads
  - EntityProfile Event <-> EventEntity mappings with Group/Series navigations ignored on the reverse map
  - DI registrations for IEventRepository and IEventService
affects: [74-04, 75-event-availability, 76-event-recurrence, 77-availability-overview]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Series-owner lookup (GetSeriesGroupIdAsync) as a fail-closed second layer for write-side board checks, independent of the entity query filter"

key-files:
  created:
    - QuestBoard.Domain/Models/Event.cs
    - QuestBoard.Domain/Interfaces/IEventRepository.cs
    - QuestBoard.Domain/Interfaces/IEventService.cs
    - QuestBoard.Repository/EventRepository.cs
    - QuestBoard.Domain/Services/EventService.cs
  modified:
    - QuestBoard.Repository/Automapper/EntityProfile.cs
    - QuestBoard.Repository/Extensions/ServiceExtensions.cs
    - QuestBoard.Domain/Extensions/ServiceExtensions.cs

key-decisions:
  - "CLAUDE.md documents EntityProfile.cs as living in QuestBoard.Domain/Automapper/ (the Entity <-> DomainModel boundary description), but the file physically lives at QuestBoard.Repository/Automapper/EntityProfile.cs, matching the plan's files_modified list and every existing mapping (Contact, Character, Group, etc). Edited the file at its actual location — CLAUDE.md's prose description is stale, not the plan."

patterns-established:
  - "Thin pass-through domain service (EventService) mirroring ContactService's shape: constructor-injected repository interface, three single-line /// <inheritdoc/> methods, no business logic beyond the repository call"

requirements-completed: [EVENT-01, EVENT-02]

coverage:
  - id: D1
    description: "Event domain model exists with the exact CLR shape of EventEntity (DateOnly Date, TimeOnly? StartTime, nullable SeriesId/SeriesSlotIndex, no author column, single StringLength on Title)"
    requirement: "EVENT-01"
    verification:
      - kind: unit
        ref: "dotnet build QuestBoard.Domain/QuestBoard.Domain.csproj (exit 0)"
        status: pass
      - kind: other
        ref: "grep acceptance criteria for Task 1 (DateOnly Date=1, TimeOnly? StartTime=1, StringLength=1, GetSeriesGroupIdAsync signature present on both interfaces, no EntityFrameworkCore reference, no groupId parameter) — all pass"
        status: pass
    human_judgment: false
  - id: D2
    description: "IEventRepository/IEventService expose exactly three feature methods beyond base CRUD (calendar read, single-event read, series-owner lookup), implemented with tenant scoping enforced entirely by the entity query filter on reads, and GetSeriesGroupIdAsync giving the controller a fail-closed second layer for writes"
    requirement: "EVENT-02"
    verification:
      - kind: integration
        ref: "dotnet test --filter \"FullyQualifiedName~TenantIsolationTests\" — 5/5 passed"
        status: pass
      - kind: other
        ref: "grep acceptance criteria for Task 2 (DI registrations present, both AutoMapper directions registered with Group/Series ignored, zero manual GroupId== filters, zero IgnoreQueryFilters, EventService has exactly three pass-through methods) — all pass"
        status: pass
    human_judgment: false

duration: ~20min
completed: 2026-08-26
status: complete
---

# Phase 74 Plan 03: Event Domain and Repository Layer Summary

**Event domain model, IEventRepository/IEventService with a calendar read, single-event read, and series-owner lookup, EventRepository/EventService implementations relying entirely on EF Core query filters for tenant scoping, and both AutoMapper directions and DI registrations wired.**

## Performance

- **Duration:** ~20 min
- **Started:** 2026-08-26T13:50Z (approx)
- **Completed:** 2026-08-26T14:10Z
- **Tasks:** 2/2
- **Files modified:** 8 (5 created, 3 edited)

## Accomplishments
- `Event` domain model mirroring `EventEntity`'s CLR types exactly — `DateOnly Date`, `TimeOnly? StartTime`, nullable `SeriesId`/`SeriesSlotIndex`, no author column, no navigation objects
- `IEventRepository`/`IEventService` each expose exactly three feature methods beyond base CRUD, all with house-style XML doc comments: `GetEventsForCalendarAsync` (fetch-all, month filtering left to the view model), `GetEventWithDetailsAsync` (single read), `GetSeriesGroupIdAsync` (returns the owning board of a repeating-schedule row, null treated as a rejection by callers)
- `EventRepository` reads (`DbContext.Events`, `DbContext.EventSeries`) rely entirely on the entity's fail-closed query filter — no manual `GroupId ==` condition anywhere, and no `IgnoreQueryFilters()` escape hatch
- `EventService` is a thin three-method pass-through mirroring `ContactService`'s shape, with no image handling or extra business logic
- `EntityProfile` gained `CreateMap<EventEntity, Event>()` and `CreateMap<Event, EventEntity>()` with `Group` and `Series` navigations ignored on the reverse map, so `BaseRepository.UpdateAsync` mapping a domain model onto a tracked entity can never null out a loaded navigation
- Both DI extension methods (`AddRepositoryServices`, `AddDomainServices`) register the new repository/service pair alongside the existing entries

## Task Commits

Each task was committed atomically:

1. **Task 1: Add the Event domain model and the two interfaces** - `49da2c5` (feat)
2. **Task 2: Implement EventRepository and EventService, wire AutoMapper and DI** - `f0d4c43` (feat)

_This is a worktree-mode execution; the plan-metadata commit (SUMMARY.md) is committed separately per the worktree protocol — no STATE.md/ROADMAP.md changes are made here._

## Files Created/Modified
- `QuestBoard.Domain/Models/Event.cs` - Domain model with `IModel`, mirroring `EventEntity`'s field shape one-for-one
- `QuestBoard.Domain/Interfaces/IEventRepository.cs` - Three-method interface extending `IBaseRepository<Event>`
- `QuestBoard.Domain/Interfaces/IEventService.cs` - Three-method interface extending `IBaseService<Event>`, identical signatures/docs to the repository interface
- `QuestBoard.Repository/EventRepository.cs` - `internal class EventRepository : BaseRepository<Event, EventEntity>, IEventRepository` — calendar read, single-event read, series-owner lookup, all group-scoped via the query filter alone
- `QuestBoard.Domain/Services/EventService.cs` - `internal class EventService : BaseService<Event>, IEventService` — thin pass-through to the repository
- `QuestBoard.Repository/Automapper/EntityProfile.cs` - Added the Event mapping block (`EventEntity <-> Event`, `Group`/`Series` ignored on the reverse map)
- `QuestBoard.Repository/Extensions/ServiceExtensions.cs` - Registered `IEventRepository`/`EventRepository`
- `QuestBoard.Domain/Extensions/ServiceExtensions.cs` - Registered `IEventService`/`EventService`

## Decisions Made
- CLAUDE.md's architecture section describes the Entity <-> DomainModel AutoMapper boundary as living in `QuestBoard.Domain/Automapper/EntityProfile.cs`, but that file does not exist in the codebase — the real file is `QuestBoard.Repository/Automapper/EntityProfile.cs`, which already holds every other Entity <-> DomainModel mapping (Contact, Character, Group, Quest, etc.) and matches the plan's own `files_modified` list. Edited the file at its actual, verified location rather than CLAUDE.md's stale prose description. No functional impact — the mapping still crosses the correct Entity <-> DomainModel boundary, just documented in the wrong project name in CLAUDE.md.

## Deviations from Plan

None - plan executed exactly as written. Both tasks matched their `read_first` references and `action` specs precisely; every grep-based acceptance criterion passed on the first attempt with no fix-up needed.

## Issues Encountered

None. `dotnet build` succeeded on the first attempt for both tasks (only pre-existing, unrelated NU1608 NuGet warnings about `AngleSharp` version constraints, present before this plan). `dotnet test --filter "FullyQualifiedName~TenantIsolationTests"` passed 5/5 with the new Event mappings and DI registrations in place, confirming the fail-closed query filter behaviour from plan 74-02 is undisturbed.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- `IEventService` fully resolves from DI with a calendar read, a single-event read, and the series-owner lookup the write-side board check needs — plan 74-04's `EventsController` can now be built directly against it.
- No Domain-layer EF dependency was introduced (`grep -rn 'EntityFrameworkCore' QuestBoard.Domain --include=*.cs` returns no matches) and layering (`Service -> Domain -> Repository`) remains strictly one-way.
- Views, controllers, `EventViewModel`, and calendar-view integration are still not built — they belong to plan 74-04, which can now proceed with a complete, tested Domain/Repository layer underneath it.
- No blockers or concerns.

---
*Phase: 74-event-schema-crud-and-calendar-display*
*Completed: 2026-08-26*

## Self-Check: PASSED

- FOUND: QuestBoard.Domain/Models/Event.cs
- FOUND: QuestBoard.Domain/Interfaces/IEventRepository.cs
- FOUND: QuestBoard.Domain/Interfaces/IEventService.cs
- FOUND: QuestBoard.Repository/EventRepository.cs
- FOUND: QuestBoard.Domain/Services/EventService.cs
- FOUND commit 49da2c5 (Task 1)
- FOUND commit f0d4c43 (Task 2)
