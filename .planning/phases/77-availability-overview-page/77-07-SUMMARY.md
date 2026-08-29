---
phase: 77-availability-overview-page
plan: 07
subsystem: api
tags: [ef-core, dependency-injection, options-validation, timeprovider, dotnet10]

# Dependency graph
requires:
  - phase: 77-availability-overview-page
    provides: EventService.GetAvailabilityOverviewAsync, EventsOverviewOptions, EventsController.Index (plans 01-06)
provides:
  - EventService reads the upcoming-window boundary from an injected UTC clock instead of the server-local date
  - EventsOverviewOptions.IsValid() names the page-size validity rule as a directly testable predicate
  - AddDomainServices validates EventsOverviewOptions at application start (ValidateOnStart)
  - EventsController.Index clamp upper bound cannot throw even with a hostile options value
affects: [77-availability-overview-page, future-phases-touching-eventservice-constructor]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "TimeProvider injected via primary constructor, registered with TryAddSingleton(TimeProvider.System) so a future test host can override it"
    - "Hand-written FixedTimeProvider test double instead of a testing-time-provider package, to hold the phase's install-nothing position"
    - "Named validity predicate (IsValid()) on an options class wired through AddOptions<T>().Validate(...).ValidateOnStart()"

key-files:
  created:
    - QuestBoard.UnitTests/Extensions/EventsOverviewOptionsValidationTests.cs
  modified:
    - QuestBoard.Domain/Services/EventService.cs
    - QuestBoard.Domain/Extensions/ServiceExtensions.cs
    - QuestBoard.Domain/Models/EventsOverviewOptions.cs
    - QuestBoard.Service/Controllers/Events/EventsController.cs
    - QuestBoard.UnitTests/Services/EventsOverviewAggregationTests.cs

key-decisions:
  - "EventService's upcoming-window date now reads timeProvider.GetUtcNow().UtcDateTime instead of DateTime.Today, closing the TZ/testability gap review findings IN-08 and WR-04 raised."
  - "IsValid() lives on EventsOverviewOptions itself rather than as an inline lambda, so the predicate is unit-testable independent of the DI wiring."
  - "The controller's clamp keeps Math.Max(1, options.MaxTake) as a defensive second layer behind start-time validation, per the plan's explicit belt-and-braces framing."

patterns-established:
  - "Options classes needing startup-fail-fast validation add a named IsValid() predicate consumed by .Validate(o => o.IsValid(), \"message\").ValidateOnStart()."

requirements-completed: [EVTVIEW-01]

coverage:
  - id: D1
    description: "EventService reads the upcoming-window boundary from an injected UTC clock; the boundary is pinned to move exactly at UTC midnight regardless of host time zone"
    requirement: "EVTVIEW-01"
    verification:
      - kind: unit
        ref: "QuestBoard.UnitTests/Services/EventsOverviewAggregationTests.cs#EventsOverviewAggregation_UpcomingBoundary_AdvancesExactlyAtUtcMidnight"
        status: pass
      - kind: unit
        ref: "QuestBoard.UnitTests/Services/EventsOverviewAggregationTests.cs#EventsOverviewAggregation_RequestsTakePlusOneFromRepository_AndUtcDateOnly"
        status: pass
    human_judgment: false
  - id: D2
    description: "A misconfigured EventsOverview page-size ceiling fails the application at start instead of 500ing every request; the runtime clamp cannot throw even if a host bypasses validation"
    requirement: "EVTVIEW-01"
    verification:
      - kind: unit
        ref: "QuestBoard.UnitTests/Extensions/EventsOverviewOptionsValidationTests.cs (8 facts: 6 predicate + 2 wiring)"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/EventsOverviewControllerIntegrationTests.cs (FullyQualifiedName~EventsOverview)"
        status: pass
    human_judgment: false

duration: 8min
completed: 2026-08-29
status: complete
---

# Phase 77 Plan 07: Availability Overview Gap Closure Summary

**Injected a UTC `TimeProvider` into `EventService` to fix the upcoming-window TZ bug, and added start-time validation plus a defensive clamp for the overview page-size ceiling**

## Performance

- **Duration:** ~8 min (commit-to-commit)
- **Started:** 2026-08-29T12:38Z (branch base)
- **Completed:** 2026-08-29T12:45:44+02:00
- **Tasks:** 2
- **Files modified:** 5 modified, 1 created

## Accomplishments
- `EventService.GetAvailabilityOverviewAsync` now decides "upcoming" from `timeProvider.GetUtcNow().UtcDateTime` instead of `DateTime.Today`, so a container whose local time zone differs from the group's can no longer move an event in or out of the list — closing review finding IN-08.
- Two unit facts pin the boundary at exact instants: a rewritten `_AndUtcDateOnly` fact plus a new UTC-midnight boundary fact that fails if the service ever reverts to a non-UTC read.
- `EventsOverviewOptions.IsValid()` names the page-size validity rule (`DefaultTake`, `MaxTake`, `PageIncrement` all ≥ 1, `DefaultTake` ≤ `MaxTake`) and is wired into `AddDomainServices` via `.Validate(...).ValidateOnStart()`, so a bad `EventsOverview:MaxTake` in the server env file now fails the application at boot with a named message instead of 500ing every request — closing review finding WR-04.
- `EventsController.Index`'s clamp upper bound is floored with `Math.Max(1, options.MaxTake)` as a defensive second layer that cannot throw even if a host somehow bypasses startup validation.

## Task Commits

1. **Task 1: Inject a clock into EventService and pin the upcoming-window boundary in tests** - `1d8a6553` (fix)
2. **Task 2: Validate the overview options at start and make the clamp unable to throw** - `0c348958` (fix)

**Plan metadata:** (pending — this SUMMARY commit)

## Files Created/Modified
- `QuestBoard.Domain/Services/EventService.cs` — takes an injected `TimeProvider`; reads the upcoming-window date in UTC.
- `QuestBoard.Domain/Extensions/ServiceExtensions.cs` — registers `TimeProvider.System` via `TryAddSingleton`; adds `.Validate(o => o.IsValid(), ...).ValidateOnStart()` to the `EventsOverviewOptions` registration.
- `QuestBoard.Domain/Models/EventsOverviewOptions.cs` — new `IsValid()` predicate.
- `QuestBoard.Service/Controllers/Events/EventsController.cs` — clamp upper bound floored with `Math.Max(1, options.MaxTake)`.
- `QuestBoard.UnitTests/Services/EventsOverviewAggregationTests.cs` — hand-written `FixedTimeProvider` test double, optional clock parameter on `CreateService`, rewritten and new boundary facts.
- `QuestBoard.UnitTests/Extensions/EventsOverviewOptionsValidationTests.cs` (new) — six predicate facts plus two DI-wiring facts.

## Decisions Made
- Kept the `FixedTimeProvider` test double hand-written (nine lines, private sealed, overrides `GetUtcNow()`) rather than adding a testing-time-provider package, per the plan's explicit package-legitimacy constraint.
- The DI wiring test needed an explicit `services.AddSingleton(configuration)` registration before calling `AddDomainServices`, since `BindConfiguration` resolves `IConfiguration` from the container rather than capturing the parameter directly — not stated in the plan's action steps but required for the wiring test to run standalone.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Registered `IConfiguration` in the wiring test's `ServiceCollection`**
- **Found during:** Task 2 (wiring test authoring)
- **Issue:** `services.AddDomainServices(configuration)` followed by `provider.GetRequiredService<IOptions<EventsOverviewOptions>>().Value` threw `InvalidOperationException: No service for type 'Microsoft.Extensions.Configuration.IConfiguration' has been registered` — `BindConfiguration` resolves `IConfiguration` from the container at options-creation time, not from the parameter passed into `AddDomainServices`.
- **Fix:** Added `services.AddSingleton(configuration)` before calling `AddDomainServices` in both wiring facts.
- **Files modified:** `QuestBoard.UnitTests/Extensions/EventsOverviewOptionsValidationTests.cs`
- **Verification:** Both wiring facts pass; full 8-fact class passes.
- **Committed in:** `0c348958` (Task 2 commit)

---

**Total deviations:** 1 auto-fixed (1 blocking)
**Impact on plan:** Necessary for the wiring test to actually exercise `AddDomainServices`; no scope creep, no production code affected.

## Issues Encountered
None beyond the auto-fixed item above.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- Both defects from `77-REVIEW.md` (IN-08, WR-04) are closed with test coverage that would fail if either regressed.
- No `.csproj` was touched (verified via `git diff --name-only` before each commit); the phase's "installs nothing" position holds.
- Full solution build: 0 errors. Full unit suite: 408/408 passed. Full integration suite: 554/554 passed.
- `EventService`'s constructor now takes three parameters; the only direct construction site outside the container (`EventsOverviewAggregationTests.CreateService`) was updated in the same commit as the constructor change, so nothing else in the solution needed touching.

---
*Phase: 77-availability-overview-page*
*Completed: 2026-08-29*

## Self-Check: PASSED

All created/modified files verified present on disk; commits `1d8a6553`, `0c348958` verified in git log.
