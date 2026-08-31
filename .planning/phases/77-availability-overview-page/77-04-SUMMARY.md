---
phase: 77-availability-overview-page
plan: 04
subsystem: testing
tags: [xunit, integration-tests, tenant-isolation, ef-core, multi-tenancy]

requires:
  - phase: 77-01
    provides: IEventRepository.GetUpcomingWithSignupsAsync, IEventService.GetAvailabilityOverviewAsync (the aggregating read this plan proves is tenant-isolated)
  - phase: 77-03
    provides: EventsController.Index at GET /Events, Index.cshtml/_AvailabilityCounts.cshtml rendered markup this plan's assertions target
provides:
  - EventsOverviewTenantIsolationTests (5 facts proving the availability overview never crosses a second board)
  - Phase-wide audit confirming no phase-77 production file bypasses the ambient EF Core query filters
affects: []

tech-stack:
  added: []
  patterns:
    - "Occurrence counting (body.Split(name).Length - 1) instead of Contain/NotContain when two boards can legitimately share a display name -- containment alone cannot detect a leaked duplicate column"
    - "Leak-detection sanity check: temporarily collapse the seeded second board onto board 1, confirm the primary fact fails, then restore -- proves the test can actually fail before trusting it to pass"

key-files:
  created:
    - QuestBoard.IntegrationTests/Tests/EventsOverviewTenantIsolationTests.cs
  modified: []

key-decisions:
  - "The board-1 signup in each case is seeded via the same SeedSignupAsync helper used for board-2 (a freshly created board-1 member), not the HTTP-authenticated caller's own row -- mirrors 77-03's EventsOverviewControllerIntegrationTests pattern of a separate viewer client plus separately seeded members, and keeps the seeding helper shape identical to the copied sibling file's three-helper contract"
  - "Case 3's count assertion checks the rendered shape '<strong>1</strong> Yes' / '<strong>3</strong> Yes' rather than a bare digit, per the plan's explicit instruction, because a bare digit would match unrelated markup elsewhere on the page"

patterns-established: []

requirements-completed: [EVTVIEW-04]

coverage:
  - id: D1
    description: "A two-group integration test seeds a genuine second board through the unfiltered seeding context and proves the overview page shows neither that board's events nor its members"
    requirement: "EVTVIEW-04"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Tests/EventsOverviewTenantIsolationTests.cs#Overview_ContainsOnlyActiveBoardEventsAndMembers"
        status: pass
    human_judgment: false
  - id: D2
    description: "A member who exists on both boards under the same display name appears exactly once on the axis, proven by occurrence counting rather than containment"
    requirement: "EVTVIEW-04"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Tests/EventsOverviewTenantIsolationTests.cs#Overview_SameNamedMemberOnAnotherBoard_AppearsOnlyOnce"
        status: pass
    human_judgment: false
  - id: D3
    description: "Another board's signup rows never contribute to any count on this page, even for an event on the same date"
    requirement: "EVTVIEW-04"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Tests/EventsOverviewTenantIsolationTests.cs#Overview_OtherBoardEventOnSameDate_DoesNotContributeToCounts"
        status: pass
    human_judgment: false
  - id: D4
    description: "Widening the page size through the take query string does not widen the tenant boundary"
    requirement: "EVTVIEW-04"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Tests/EventsOverviewTenantIsolationTests.cs#Overview_LargeTakeParameter_DoesNotWidenBeyondActiveBoard"
        status: pass
    human_judgment: false
  - id: D5
    description: "With no active board selected the page shows nothing from any board, because the query filters are fail-closed"
    requirement: "EVTVIEW-04"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Tests/EventsOverviewTenantIsolationTests.cs#Overview_WithNoActiveBoardSelected_ShowsNothingFromEitherBoard"
        status: pass
    human_judgment: false
  - id: D6
    description: "The dedicated isolation test can actually detect a leak (not merely pass by construction): temporarily collapsing the seeded second board's GroupId from 2 to 1 makes Overview_ContainsOnlyActiveBoardEventsAndMembers fail"
    requirement: "EVTVIEW-04"
    verification:
      - kind: other
        ref: "Manual sanity check performed during execution: reverted GroupId 2->1 in SeedOtherBoardEventAsync, re-ran the test (failed as expected), restored, re-ran (passed) -- not a persisted artifact, procedure documented in this SUMMARY"
        status: pass
    human_judgment: false
  - id: D7
    description: "No production file added by this phase (EventRepository.cs, EventService.cs, EventsController.cs) bypasses the ambient EF Core query filters; the full solution test suite remains green"
    requirement: "EVTVIEW-04"
    verification:
      - kind: other
        ref: "grep -c 'IgnoreQueryFilters' on all three files == 0; dotnet test (full solution) == 953 passed, 0 failed; dotnet test --filter EventsOverview == 32 passed; dotnet test --filter LayoutNavigationTests == 32 passed, 0 failed"
        status: pass
    human_judgment: false

duration: 25min
completed: 2026-08-29
status: complete
---

# Phase 77 Plan 04: Availability Overview Tenant Isolation Tests and Filter-Bypass Audit Summary

**Five-fact `EventsOverviewTenantIsolationTests` class proving the `GET /Events` aggregating read never leaks a second board's events, members, or signup counts, plus a phase-wide audit confirming zero `IgnoreQueryFilters` bypasses across the three production files this phase added.**

## Performance

- **Duration:** 25 min
- **Completed:** 2026-08-29
- **Tasks:** 2
- **Files modified:** 1 (created)

## Accomplishments
- `EventsOverviewTenantIsolationTests` — 5 integration facts, copying the class shape of the sibling `EventAvailabilityTenantIsolationTests` (`IClassFixture<WebApplicationFactoryBase>, IAsyncLifetime`, `DisposeAsync` resetting the shared `TestGroupContext` singleton back to board 1), seeding a genuine second board through `factory.Database.CreateContext()` rather than the request pipeline
- Proved both leak surfaces the page joins across: another board's event never appears in the list, and another board's member (even one sharing the exact display name "Shared Overview Name" with a board-1 member) never appears on the axis — the same-name case counts occurrences rather than using containment, since with identical names a leaked column is invisible to a plain `Contain`/`NotContain` check
- Proved another board's signup rows never contribute to this page's headline counts, even for an event dated identically to a board-1 event, by asserting the rendered `<strong>1</strong> Yes` shape rather than a bare digit
- Proved widening `?take=100000` does not widen the tenant boundary, and that a null active board (fail-closed filters) shows nothing from either board
- Ran the leak-detection sanity check required by the plan's acceptance criteria: temporarily collapsed the seeded second board's `GroupId` from 2 to 1, re-ran `Overview_ContainsOnlyActiveBoardEventsAndMembers`, confirmed it failed (proving the test is capable of catching a real leak), then restored the fix and confirmed all 5 facts pass again
- Ran the phase-wide filter-bypass audit: `grep -c 'IgnoreQueryFilters'` is `0` for `EventRepository.cs`, `EventService.cs`, and `EventsController.cs` — the three production files this phase's plans added or modified
- Full solution test suite green: 399 unit + 554 integration = 953 tests, 0 failures. `EventsOverview` filter: 32 tests (aggregation + view-model mapping + controller + tenant isolation). `LayoutNavigationTests` filter: 32 tests, 0 failed

## Task Commits

Each task was committed atomically:

1. **Task 1: Two-group tenant isolation test for the availability overview** - `4946f391` (test)
2. **Task 2: Phase-wide filter-bypass audit and full-suite gate** - no commit (audit only, no production code changed; results recorded in this SUMMARY per the plan's own instruction that "no production code should need to change")

## Files Created/Modified
- `QuestBoard.IntegrationTests/Tests/EventsOverviewTenantIsolationTests.cs` - 5 tenant isolation facts for the availability overview page, plus 3 private seeding helpers (`SeedOtherBoardEventAsync`, `SeedGroupOneEventAsync`, `SeedSignupAsync`) copied from the sibling `EventAvailabilityTenantIsolationTests` and extended with availability/answered parameters

## Decisions Made
- Board-1 signups in each case are seeded via `SeedSignupAsync` (a freshly created board-1 member), not the HTTP-authenticated request's own user — this mirrors 77-03's `EventsOverviewControllerIntegrationTests` pattern (a separate viewer client issuing the GET, separately seeded members populating the grid) and keeps the three-helper shape identical to the copied sibling file rather than introducing a fourth helper
- Case 3's count assertions check the rendered shape `<strong>1</strong> Yes` / `<strong>3</strong> Yes` (matching `_AvailabilityCounts.cshtml`'s actual markup) rather than a bare digit, per the plan's explicit instruction that a bare digit would match unrelated page markup

## Deviations from Plan

None - plan executed exactly as written.

**Audit observation (not a deviation, informational):** The plan's Task 2 acceptance criteria state that `grep -rn 'IgnoreQueryFilters' QuestBoard.Repository QuestBoard.Domain QuestBoard.Service --include=*.cs` "returns only the pre-existing `GroupRepository` occurrence." Running that exact grep also surfaces one additional pre-existing occurrence in `QuestBoard.Repository/QuestRepository.cs` (`GetQuestsForTomorrowAllGroupsAsync`, a public method whose name and inline comment — "Explicit cross-group intent" — already declare its deliberate all-groups scope for a scheduled reminder job). This method predates phase 77, was not touched by any phase-77 plan, and is out of this phase's scope per the executor's scope-boundary rule (only auto-fix issues directly caused by the current task's changes). The security-relevant claim this task exists to prove — that no file phase 77 added or edited bypasses the filters, and that the `GroupRepository` exception specific to this phase's read path has not grown a new caller — holds fully: both `GroupRepository` methods (`GetFutureEventIdsForGroupIgnoringActiveBoardAsync`, `GetEventSignupsForMemberIgnoringActiveBoardAsync`) remain `private`, each still pins its own group predicate from an explicit `groupId` argument, and neither gained a new caller. One nuance versus the plan's read_first description: the two `GroupRepository` methods serve the join flow (`AddMemberAsync`, campaign auto-signup) and the leave flow (`RemoveMemberAsync`, membership cleanup) respectively, not both "the leave-board cleanup" as the plan's read_first phrased it — confirmed by reading both call sites.

## Issues Encountered

None.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- EVTVIEW-04 is proven, not assumed: the phase's single named risk (a cross-board leak on the one page joining events, signups, and members) now has a dedicated test that has been shown able to fail
- Phase 77 (availability-overview-page) is complete: all four plans (domain aggregate + aggregation, CSS/nav, view models/views/controller, tenant isolation) are implemented and proven
- No blockers for subsequent phases

---
*Phase: 77-availability-overview-page*
*Completed: 2026-08-29*

## Self-Check: PASSED

Created file verified present on disk (`QuestBoard.IntegrationTests/Tests/EventsOverviewTenantIsolationTests.cs`). Task commit verified present in git log (`4946f391`).
