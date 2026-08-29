---
phase: 77-availability-overview-page
plan: 01
subsystem: api
tags: [ef-core, automapper, aggregation, xunit, nsubstitute, availability]

requires: []
provides:
  - AvailabilityCellState enum (five-state cell classification)
  - EventAvailabilityOverview / EventAvailabilityRow / AvailabilityMember / EventWithSignups domain models
  - EventsOverviewOptions (DefaultTake/MaxTake/PageIncrement) bound via BindConfiguration
  - IEventRepository.GetUpcomingWithSignupsAsync (single-query bounded aggregate read)
  - IEventService.GetAvailabilityOverviewAsync (in-memory aggregation: member axis, cell states, counts, HasMore)
affects: [77-02, 77-03, 77-04]

tech-stack:
  added: []
  patterns:
    - "Code-default-plus-configuration options class (EventsOverviewOptions), copying EventSeriesOptions shape verbatim"
    - "take+1 fetch-and-trim pattern for HasMore without a second query"
    - "Member axis built as a distinct union of signup rows rather than a membership query"

key-files:
  created:
    - QuestBoard.Domain/Enums/AvailabilityCellState.cs
    - QuestBoard.Domain/Models/AvailabilityMember.cs
    - QuestBoard.Domain/Models/EventWithSignups.cs
    - QuestBoard.Domain/Models/EventAvailabilityRow.cs
    - QuestBoard.Domain/Models/EventAvailabilityOverview.cs
    - QuestBoard.Domain/Models/EventsOverviewOptions.cs
    - QuestBoard.UnitTests/Services/EventsOverviewAggregationTests.cs
  modified:
    - QuestBoard.Domain/Extensions/ServiceExtensions.cs
    - QuestBoard.Domain/Interfaces/IEventRepository.cs
    - QuestBoard.Repository/EventRepository.cs
    - QuestBoard.Domain/Interfaces/IEventService.cs
    - QuestBoard.Domain/Services/EventService.cs

key-decisions:
  - "Member axis order alphabetical by name (OrdinalIgnoreCase), UserId tiebreaker; identical across every row"
  - "Cell classification reads only EventSignup.HasAnswered plus Availability, no board-type branch"
  - "HasMore derived from a take+1 repository fetch and trim, never a second count query"

patterns-established:
  - "AutoMapper composition over two existing maps (Event, EventSignup) rather than a new cross-entity map, for a repository method that returns a paired read shape"

requirements-completed: [EVTVIEW-01, EVTVIEW-02, EVTVIEW-03]

coverage:
  - id: D1
    description: "Domain vocabulary (five-state enum, four models, options class) compiles and options bound in DI with code defaults only"
    requirement: "EVTVIEW-01"
    verification:
      - kind: unit
        ref: "dotnet build QuestBoard.Domain/QuestBoard.Domain.csproj"
        status: pass
    human_judgment: false
  - id: D2
    description: "Single-query bounded aggregate read (GetUpcomingWithSignupsAsync) rides ambient query filters, excludes cancelled events, date-only lower bound, deterministic sort"
    requirement: "EVTVIEW-01"
    verification:
      - kind: unit
        ref: "dotnet build QuestBoard.Repository/QuestBoard.Repository.csproj"
        status: pass
      - kind: other
        ref: "grep -c 'IgnoreQueryFilters' QuestBoard.Repository/EventRepository.cs == 0"
        status: pass
    human_judgment: false
  - id: D3
    description: "GetAvailabilityOverviewAsync produces a stable member axis, five cell states, three per-row counts and HasMore from a single repository call"
    requirement: "EVTVIEW-02"
    verification:
      - kind: unit
        ref: "QuestBoard.UnitTests/Services/EventsOverviewAggregationTests.cs (11 tests)"
        status: pass
    human_judgment: false
  - id: D4
    description: "Three per-row counts (YesCount total, ConfirmedYesCount subset, MaybeCount separate) with No counted nowhere"
    requirement: "EVTVIEW-03"
    verification:
      - kind: unit
        ref: "QuestBoard.UnitTests/Services/EventsOverviewAggregationTests.cs#EventOverviewCounts_* (3 tests)"
        status: pass
    human_judgment: false

duration: 35min
completed: 2026-08-29
status: complete
---

# Phase 77 Plan 01: Availability Overview Aggregate Read and Domain Aggregation Summary

**Single-query EF Core read of the next N live upcoming events with signups, plus an in-memory EventService aggregation producing a five-state cell matrix, three per-row counts, and a stable member axis — proven by 11 unit tests.**

## Performance

- **Duration:** 35 min
- **Started:** 2026-08-29T00:00:00Z (approx, worktree spawn)
- **Completed:** 2026-08-29
- **Tasks:** 3
- **Files modified:** 12 (7 created, 5 modified)

## Accomplishments
- Five new domain types (`AvailabilityCellState`, `AvailabilityMember`, `EventWithSignups`, `EventAvailabilityRow`, `EventAvailabilityOverview`) and one options class (`EventsOverviewOptions`) registered in DI with code defaults only
- `IEventRepository.GetUpcomingWithSignupsAsync` — one EF Core round trip returning the next N live upcoming events with every signup row and member name, scoped entirely by the ambient fail-closed query filters, excluding cancelled occurrences, with a date-only lower bound and a fully deterministic sort (`Date`, `StartTime`, `Id`) ahead of `Take`
- `IEventService.GetAvailabilityOverviewAsync` — builds the member axis as the distinct union of signup holders (alphabetical, `UserId` tiebreaker), classifies every cell into one of five states purely from `EventSignup.HasAnswered` and `Availability`, computes three independent per-row counts, and derives `HasMore` from a `take+1` fetch with no second query
- 11 unit tests in `EventsOverviewAggregationTests` covering all five cell states, all three count rules, member-axis stability, and both `HasMore` branches — written first (RED, confirmed all 11 failing against a `NotImplementedException` stub), then implemented (GREEN, all 11 passing)
- Full solution build clean; full test suite green (396 unit + 528 integration, 0 failures)

## Task Commits

Each task was committed atomically:

1. **Task 1: Domain read models, cell-state enum, and tunable page-size options** - `24244785` (feat)
2. **Task 2: Single-query bounded aggregate read on IEventRepository** - `e184bbe5` (feat)
3. **Task 3: Overview aggregation in EventService, with unit tests** - `e9926518` (test, RED) + `e70845ec` (feat, GREEN)

_TDD task (Task 3) produced two commits: a failing-test commit followed by the implementation commit, per the RED/GREEN gate._

## Files Created/Modified
- `QuestBoard.Domain/Enums/AvailabilityCellState.cs` - Five-state cell enum (Empty, ConfirmedYes, ConfirmedMaybe, ConfirmedNo, UnconfirmedYes)
- `QuestBoard.Domain/Models/AvailabilityMember.cs` - One column of the availability grid
- `QuestBoard.Domain/Models/EventWithSignups.cs` - Repository read-pairing of an event with its signup rows
- `QuestBoard.Domain/Models/EventAvailabilityRow.cs` - One grid row: event, three counts, positionally-aligned cells
- `QuestBoard.Domain/Models/EventAvailabilityOverview.cs` - The whole grid: members, rows, HasMore
- `QuestBoard.Domain/Models/EventsOverviewOptions.cs` - DefaultTake/MaxTake/PageIncrement, code defaults
- `QuestBoard.Domain/Extensions/ServiceExtensions.cs` - Registered `EventsOverviewOptions` via `AddOptions().BindConfiguration`
- `QuestBoard.Domain/Interfaces/IEventRepository.cs` - Added `GetUpcomingWithSignupsAsync` signature + XML doc
- `QuestBoard.Repository/EventRepository.cs` - Implemented the single-query bounded read
- `QuestBoard.Domain/Interfaces/IEventService.cs` - Added `GetAvailabilityOverviewAsync` signature + XML doc
- `QuestBoard.Domain/Services/EventService.cs` - Implemented the in-memory aggregation
- `QuestBoard.UnitTests/Services/EventsOverviewAggregationTests.cs` - 11 tests covering the full behavior contract

## Decisions Made
- Member axis order is alphabetical by name (`StringComparer.OrdinalIgnoreCase`) with `UserId` as a stable tiebreaker, matching the plan's D-13/D-14 rule, and is built solely from signup rows already loaded — never a group-membership query
- Cell classification reads only `EventSignup.HasAnswered` and `Availability`; an unanswered non-Yes row (a shape that cannot arise from either write path) classifies the same as an answered row rather than inventing a sixth state, per the plan's explicit guidance
- `HasMore` is derived from a `take + 1` repository fetch and trim — no second count query, matching D-10

## Deviations from Plan

None - plan executed exactly as written. The RED-phase stub (`NotImplementedException` in `EventService.GetAvailabilityOverviewAsync`) was required by the plan's own TDD instruction ("write tests first, watch them fail, then implement") combined with the compiled-language constraint noted in `77-VALIDATION.md` Wave 0 section — not a deviation, but the mechanism required to let the test file compile while red.

## Issues Encountered
- Initial `EventAvailabilityRow.cs` write omitted the `using QuestBoard.Domain.Enums;` directive needed for `AvailabilityCellState` — caught immediately by the Task 1 build gate and fixed inline before commit.
- `Mapper.Map<IList<EventSignup>>(...)` assigned to an `IReadOnlyList<EventSignup>` property doesn't implicitly convert; switched to `Mapper.Map<List<EventSignup>>(...)` — caught by the Task 2 build gate and fixed inline before commit.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- `EventAvailabilityOverview`/`EventAvailabilityRow`/`AvailabilityMember` and `IEventService.GetAvailabilityOverviewAsync` are ready for plan 77-03's controller/view-model mapping layer to consume
- `EventsOverviewOptions.MaxTake` is in place for 77-03's controller-side clamp (T-77-02 mitigation)
- No blockers for 77-02 (CSS/nav) or 77-03 (controller/views), which depend on this plan's domain and repository surface

---
*Phase: 77-availability-overview-page*
*Completed: 2026-08-29*
