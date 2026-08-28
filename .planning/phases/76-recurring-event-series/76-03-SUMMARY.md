---
phase: 76-recurring-event-series
plan: 03
subsystem: persistence
tags: [ef-core, repository-pattern, automapper, dependency-injection, in-memory-tests]

# Dependency graph
requires:
  - EventSeriesEntity template columns (Title, Description, StartTime, EndDate)
  - EventEntity/Event CancelledAt tombstone marker
  - EventSeries domain model and its EntityProfile AutoMapper pair
  - SeriesRunwayStatus, SeriesRemovalImpact supporting domain types
  - Filtered unique index IX_Events_SeriesId_SeriesSlotIndex
provides:
  - IEventSeriesRepository / EventSeriesRepository (registered in DI)
  - EventRepository.SetCancelledAsync / ApplyTemplateToOccurrencesAsync / CountLiveSiblingsOnDateAsync / GetOccurrencesForSeriesAsync
  - IEventService.SetCancelledAsync pass-through
  - EventSeriesRepositoryTests proving the idempotency, runway, removal and isolation constraints
affects: [76-04, 76-05, 76-06, 76-07, 76-08, 76-09, 76-10, 76-11, 76-12]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "EventSeries gets its own repository/interface triple, not folded into IEventRepository -- mirrors the IEventSignupService-beside-IEventService precedent"
    - "GroupJoin (join ... into) used for GetSeriesBelowRunwayAsync to get one round trip instead of a per-series count query"
    - "Conditional relational transaction (Database.IsRelational() ? BeginTransactionAsync : null) so the same code path works against both SQL Server and the in-memory test provider"

key-files:
  created:
    - QuestBoard.Domain/Interfaces/IEventSeriesRepository.cs
    - QuestBoard.Repository/EventSeriesRepository.cs
    - QuestBoard.UnitTests/Repository/EventSeriesRepositoryTests.cs
  modified:
    - QuestBoard.Domain/Interfaces/IEventRepository.cs
    - QuestBoard.Domain/Interfaces/IEventService.cs
    - QuestBoard.Domain/Services/EventService.cs
    - QuestBoard.Repository/EventRepository.cs
    - QuestBoard.Repository/Extensions/ServiceExtensions.cs

key-decisions:
  - "GetSeriesBelowRunwayAsync uses a GroupJoin (join ... into) rather than a correlated subquery inside Select, to stay reliably translatable across both the SQL Server and in-memory EF Core providers while still keeping the query to one round trip"
  - "GetRemovalImpactAsync is two queries (occurrence dates, then a signup count keyed on those occurrence ids) rather than one join, favoring straightforward correctness over the marginal round-trip saving at this scale"

requirements-completed: [EVTRECUR-04, EVTRECUR-05, EVTRECUR-06, EVTRECUR-07]

coverage:
  - id: D1
    description: "EventRepository gains four narrow methods (SetCancelledAsync, ApplyTemplateToOccurrencesAsync, CountLiveSiblingsOnDateAsync, GetOccurrencesForSeriesAsync); only SetCancelledAsync is surfaced on IEventService/EventService"
    requirement: "EVTRECUR-06"
    verification:
      - kind: unit
        ref: "dotnet build (0 errors); grep assertions per plan acceptance criteria (UpdateAsync absent, no GSD refs)"
        status: pass
      - kind: unit
        ref: "EventSeriesRepositoryTests: SetCancelledAsync, ApplyTemplateToOccurrencesAsync, CountLiveSiblingsOnDateAsync each independently covered"
        status: pass
    human_judgment: false
  - id: D2
    description: "IEventSeriesRepository/EventSeriesRepository exist, are registered in DI, and implement the idempotency (date-free slot query), runway, removal-impact, end-date and two-outcome-removal queries without any query-filter bypass"
    requirement: "EVTRECUR-04, EVTRECUR-05, EVTRECUR-07"
    verification:
      - kind: unit
        ref: "dotnet build (0 errors); grep assertions confirming interface/class shape, no IgnoreQueryFilters, IsRelational() present, AddWithCampaignFanOutAsync reused, DI registration line present"
        status: pass
    human_judgment: false
  - id: D3
    description: "Repository constraints are proven by passing in-memory tests: the moved-far-outside-runway slot survives (MoveThenRun), a cancelled slot is still returned, runway counting excludes cancelled/past and includes today, the horizon query respects both the target and the end date, removal impact splits past/future and counts only real answers, end-date removal only touches occurrences after the end date, delete removes everything, detach nulls both columns while preserving the cancelled marker, and a series/occurrence pair seeded on another board is invisible"
    requirement: "EVTRECUR-07"
    verification:
      - kind: unit
        ref: "dotnet test QuestBoard.UnitTests --filter FullyQualifiedName~EventSeriesRepositoryTests: 12/12 pass; --filter FullyQualifiedName~MoveThenRun: 1/1 pass"
        status: pass
      - kind: integration
        ref: "dotnet test (full suite): QuestBoard.UnitTests 366/366 pass, QuestBoard.IntegrationTests 498/498 pass -- no regressions from the new repository or the EventRepository additions"
        status: pass
    human_judgment: false

# Metrics
duration: ~35min
completed: 2026-08-28
status: complete
---

# Phase 76 Plan 03: Series Persistence Layer Summary

**A dedicated `EventSeriesRepository`/`IEventSeriesRepository` pair plus four narrow `EventRepository` writes give the rest of Phase 76 every read and write it needs — the date-free slot-existence query, the live-occurrence runway measure, the two deliberate removal outcomes, and the transactional first-generation-pass write — all proven against an in-memory database including the exact moved-occurrence scenario the idempotency guarantee depends on.**

## Performance

- **Duration:** ~35 min
- **Started:** 2026-08-28T15:10:00+02:00 (approx.)
- **Completed:** 2026-08-28T15:45:00+02:00
- **Tasks:** 3
- **Files modified:** 8 (5 modified, 3 created)

## Accomplishments

- `EventRepository` gained four narrow methods — `SetCancelledAsync`, `ApplyTemplateToOccurrencesAsync`, `CountLiveSiblingsOnDateAsync`, `GetOccurrencesForSeriesAsync` — each a scalar or batch write/read through the filtered `DbSet`, never through `BaseRepository.UpdateAsync`; only `SetCancelledAsync` is surfaced on `IEventService`/`EventService` since it's the only one a controller calls directly
- `EventSeries` got its own repository/interface pair — `IEventSeriesRepository`/`EventSeriesRepository` — registered in DI beside `IEventRepository`, deliberately not folded into `IEventService`
- The slot-existence query (`GetSlotIndexesForSeriesAsync`) carries no date parameter by construction — an interface-signature grep pins it, and a test seeding an occurrence two years past the runway proves the guarantee directly
- The runway measure (`CountLiveFutureOccurrencesAsync`) and horizon banner query (`GetSeriesBelowRunwayAsync`) both count live, non-cancelled, today-or-later occurrences rather than a date horizon
- Delete (`DeleteWithOccurrencesAsync`) and detach (`DetachOccurrencesAndDeleteAsync`) are two separate, deliberate multi-step operations; detach nulls both `SeriesId` and `SeriesSlotIndex` and leaves a cancelled occurrence's marker untouched
- `CreateWithOccurrencesAsync` wraps the series row and its first generation pass in one relational transaction (skipped for the in-memory test provider) and reuses `AddWithCampaignFanOutAsync` rather than reimplementing the fan-out
- 12 new tests in `EventSeriesRepositoryTests` prove every constraint named above, including the load-bearing moved-occurrence case (`MoveThenRun`) and cross-board invisibility for a series seeded on another board

## Task Commits

Each task was committed atomically:

1. **Task 1: Add the narrow occurrence-write and sibling-count methods to EventRepository** - `2325667` (feat)
2. **Task 2: Create IEventSeriesRepository and EventSeriesRepository with the idempotency, runway and lifecycle queries** - `662b70a` (feat)
3. **Task 3: Prove the repository constraints with in-memory tests** - `8bf14f0` (test)

## Files Created/Modified

- `QuestBoard.Domain/Interfaces/IEventRepository.cs` - added `SetCancelledAsync`, `ApplyTemplateToOccurrencesAsync`, `CountLiveSiblingsOnDateAsync`, `GetOccurrencesForSeriesAsync`
- `QuestBoard.Domain/Interfaces/IEventService.cs` - added `SetCancelledAsync`
- `QuestBoard.Domain/Services/EventService.cs` - added the `SetCancelledAsync` pass-through
- `QuestBoard.Repository/EventRepository.cs` - implemented the four new methods
- `QuestBoard.Domain/Interfaces/IEventSeriesRepository.cs` - new interface, 11 members plus `IBaseRepository<EventSeries>`
- `QuestBoard.Repository/EventSeriesRepository.cs` - new repository implementing every member
- `QuestBoard.Repository/Extensions/ServiceExtensions.cs` - registered `IEventSeriesRepository`
- `QuestBoard.UnitTests/Repository/EventSeriesRepositoryTests.cs` - 12 new tests

## Decisions Made

- `GetSeriesBelowRunwayAsync` uses a `join ... into` group join rather than a correlated subquery written directly inside a `Select` projection. Both patterns are valid EF Core idioms, but the group join is the more conservative choice for staying translatable identically across the SQL Server provider (production) and the in-memory provider (tests) while still costing one round trip rather than one query per series.
- `GetRemovalImpactAsync` is implemented as two sequential queries (occurrence dates, then a signup count filtered on those occurrence ids) instead of a single join. At this table's scale the extra round trip is immaterial, and the two-query shape is simpler to read and verify correct than a three-way join mixing a date split and an `UpdatedAt` filter.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Test bug: identity-mapped entity reference read after a second mutating call**
- **Found during:** Task 3 (first test run)
- **Issue:** `SetCancelledAsync_SetsThenClearsMarker_WithoutDisturbingSignups` captured the entity returned by a query (`afterSet = await context.Events.SingleAsync(...)`) immediately after the first `SetCancelledAsync` call, then made a second `SetCancelledAsync` call before asserting on `afterSet.CancelledAt`. Because the in-memory provider (like SQL Server) returns the same tracked instance from a second query against an already-tracked entity (EF Core's identity map), `afterSet` and the entity mutated by the second call were the same object reference — the assertion on `afterSet` was reading the state *after* the clear, not after the set, and failed.
- **Fix:** Read `.CancelledAt` into a local `DateTime?` value immediately after each write, before the next write runs, rather than holding a reference to the mutable entity across both writes.
- **Files modified:** `QuestBoard.UnitTests/Repository/EventSeriesRepositoryTests.cs`
- **Verification:** `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~EventSeriesRepositoryTests"` — 12/12 pass after the fix.
- **Committed in:** `8bf14f0` (Task 3 commit; this was fixed before the first commit of the test file, so no separate fix-up commit exists)

---

**Total deviations:** 1 auto-fixed (1 test bug, caught by the test itself before commit)
**Impact on plan:** No scope change. This was a test-authoring mistake (identity-map reference aliasing) rather than a defect in the repository code under test; the repository implementation was correct throughout.

## Issues Encountered

None beyond the deviation documented above.

## TDD Gate Compliance

Task 3 is tagged `tdd="true"`, but its production code (`EventSeriesRepository`, the `EventRepository` additions) was already built and committed in Tasks 1 and 2 — this task's sole purpose was to add proof, not to drive new implementation. A literal RED-then-GREEN cycle was not applicable: the methods under test were already correct by the time this task ran, so a first test run would pass immediately rather than fail, which the RED phase's fail-fast rule treats as a signal to investigate rather than a gate to force through artificially. The task was executed as a single `test(...)` commit after write-fix-verify, which is consistent with the plan's own task ordering (repository code lands in Tasks 1–2, tests land in Task 3) and with the fail-fast rule's intent — the one place a test genuinely failed unexpectedly (the identity-map aliasing bug above) was investigated and fixed before commit, exactly as instructed.

## User Setup Required

None — no external service configuration required. No migration in this plan (schema already shipped in 76-02).

## Next Phase Readiness

- Every read and write the rest of Phase 76 needs against `EventSeries` and its occurrences now exists, is registered in DI, and is proven correct against an in-memory database, including the two hard idempotency guarantees (no date predicate, cancelled slots included).
- `EventSeriesDateGenerator` (76-01) and this plan's repository together give the next plan (the Domain-layer `EventSeriesService`/generation orchestration) everything it needs without reimplementing any query or write.
- No blockers.

---
*Phase: 76-recurring-event-series*
*Completed: 2026-08-28*
