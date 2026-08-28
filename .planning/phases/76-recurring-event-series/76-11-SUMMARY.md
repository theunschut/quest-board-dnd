---
phase: 76-recurring-event-series
plan: 11
subsystem: testing
tags: [xunit, integration-tests, tenant-isolation, ef-core-inmemory]

# Dependency graph
requires:
  - phase: 76-05
    provides: "IEventSeriesRepository/EventSeriesRepository and the EventSeries domain model this plan's seeding helper writes directly against"
  - phase: 76-07
    provides: "EventsController's Cancel/Restore/PreviewSeries/CheckOccurrenceCollision actions and the recurring branch of Create, this plan's write-side facts post against"
  - phase: 76-08
    provides: "The scope-aware Edit POST (EventEditScope.ThisAndFutureEvents) and the Delete-refuses-a-series-occurrence rule this plan proves through the real pipeline"
  - phase: 76-09
    provides: "SeriesController (Details/End/Delete/Detach) at /Series/Details/{id}, the route this plan's series-scoped facts target"
  - phase: 76-10
    provides: "The cancelled-state calendar/agenda rendering this plan's desktop and mobile visibility facts assert against"
provides:
  - "EventSeriesTenantIsolationTests -- a dedicated two-board integration class proving every read and write surface Phase 76 adds is board-scoped, and that both server-side refusals (Delete-on-a-series-occurrence, Cancel-on-a-one-off) hold when posted directly"
  - "Three new EventsControllerIntegrationTests facts closing a genuine automated-evidence gap on POST /Events/PreviewSeries (success shape, mask-validation-error passthrough, DM-only access), found while confirming the validation map's per-requirement filters"
  - "Per-requirement automated pass counts for every Phase 76 requirement, recorded below for the phase's verification step"
affects: [76-recurring-event-series (this is the phase's closing validation plan)]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "The seeding helper for a second board writes through a context with no active board selected (ActiveGroupId = null), which is what lets it write rows for a board the request pipeline itself could never read or select -- and it captures every seeded id straight off the tracked entities rather than re-querying afterwards, because the same null-ActiveGroupId filter that made the write possible also hides the just-inserted rows from any query run on that same context."
    - "Every mutating-action isolation fact asserts on outcome (target row read back through a fresh context, unchanged) rather than a specific status code, since a cross-board refusal is legitimately expressed as either not-found (EventEntity/EventSeriesEntity query filter fires first) or bad-request (the controller's explicit second-layer board comparison fires first) depending on which check a given action hits first."

key-files:
  created:
    - QuestBoard.IntegrationTests/Tests/EventSeriesTenantIsolationTests.cs
  modified:
    - QuestBoard.IntegrationTests/Controllers/EventsControllerIntegrationTests.cs

key-decisions:
  - "Cross-board mutating-action facts (Cancel, End, Delete, Detach, future-scope Edit) assert response.IsSuccessStatusCode.Should().BeFalse() and response.StatusCode.Should().NotBe(Redirect) rather than a single expected status code, because the two independent defense layers (EF Core's query filter returning NotFound, versus the controller's explicit SeriesIsOnActiveBoardAsync/GetSeriesGroupIdAsync comparison returning BadRequest) legitimately fire first depending on the action -- the plan's own acceptance criteria calls this out explicitly, and hard-coding one status per fact would make the test brittle against which layer happens to run first."
  - "The two own-board refusal facts (Delete on a series occurrence, Cancel on a one-off) seed their fixture directly through the database context on the acting board (GroupId = 1) rather than through the seeding helper meant for the other board, since these facts are deliberately not about cross-board isolation -- they prove the two server-side rules hold on the acting board's own data, posted directly with no view involved."

patterns-established:
  - "A dedicated two-board integration test class per phase (EventTenantIsolationTests, EventAvailabilityTenantIsolationTests, and now EventSeriesTenantIsolationTests) is the established shape for proving tenant isolation on a feature area, because the shared harness's single mutable group context makes an ordinary same-class test structurally blind to a cross-board leak."

requirements-completed: [EVTRECUR-04, EVTRECUR-05, EVTRECUR-06, EVTRECUR-07, EVTRECUR-08]

coverage:
  - id: D1
    description: "A series and its occurrences on one board are invisible from another board's desktop calendar and mobile agenda (real mobile User-Agent), and a direct GET of the occurrence or series details page for a cross-board identifier returns not-found"
    requirement: "EVTRECUR-04"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Tests/EventSeriesTenantIsolationTests.cs#GroupFilter_HidesSeriesFromOtherGroupOnDesktopCalendar"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Tests/EventSeriesTenantIsolationTests.cs#GroupFilter_HidesSeriesFromOtherGroupOnMobileAgenda"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Tests/EventSeriesTenantIsolationTests.cs#Details_OccurrenceFromOtherGroup_ReturnsNotFound"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Tests/EventSeriesTenantIsolationTests.cs#Details_SeriesFromOtherGroup_ReturnsNotFound"
        status: pass
    human_judgment: false
  - id: D2
    description: "Every series-mutating action this phase adds (Cancel, End, Delete, Detach, and a future-scope Edit) rejects an identifier belonging to another board, with the target row read back through a fresh context and proven unchanged rather than merely trusting a failing status code"
    requirement: "EVTRECUR-04"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Tests/EventSeriesTenantIsolationTests.cs#Cancel_Post_OccurrenceFromOtherGroup_DoesNotSucceedAndLeavesRowUnchanged"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Tests/EventSeriesTenantIsolationTests.cs#End_Post_SeriesFromOtherGroup_DoesNotSucceedAndLeavesSeriesAndOccurrencesUnchanged"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Tests/EventSeriesTenantIsolationTests.cs#Delete_Post_SeriesFromOtherGroup_DoesNotSucceedAndLeavesSeriesAndOccurrencesPresent"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Tests/EventSeriesTenantIsolationTests.cs#Detach_Post_SeriesFromOtherGroup_DoesNotSucceedAndLeavesSeriesAndOccurrencesPresent"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Tests/EventSeriesTenantIsolationTests.cs#Edit_Post_FutureScopeForOccurrenceFromOtherGroup_DoesNotSucceedAndRewritesNothing"
        status: pass
    human_judgment: false
  - id: D3
    description: "A posted board identifier cannot override the server-side board stamp on a recurring create -- every row of a spoofed-board series creation lands on the acting board"
    requirement: "EVTRECUR-04"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Tests/EventSeriesTenantIsolationTests.cs#Create_Post_RecurringWithPostedGroupIdIsIgnored_ServerStampsActiveBoard"
        status: pass
    human_judgment: false
  - id: D4
    description: "Delete is refused for a series occurrence and Cancel is refused for a one-off event, posted directly through the real pipeline with no view involved, with the target row's state read back unchanged"
    requirement: "EVTRECUR-04"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Tests/EventSeriesTenantIsolationTests.cs#Delete_Post_SeriesOccurrenceOnActiveBoard_ReturnsBadRequestAndOccurrenceStillExists"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Tests/EventSeriesTenantIsolationTests.cs#Cancel_Post_OneOffEventOnActiveBoard_ReturnsBadRequestAndCancelledMarkerStillUnset"
        status: pass
    human_judgment: false
  - id: D5
    description: "The full suite is green across both test projects and every named validation-map filter (mask, mirrored-mask, preview, top-up, cancel, move-then-run, edit-scope, idempotency, series tenant-isolation, job) resolves to at least one passing test; a genuine zero-test gap found for the preview filter was closed rather than left in place"
    requirement: "EVTRECUR-05"
    verification:
      - kind: integration
        ref: "dotnet test (full suite) -- QuestBoard.UnitTests 385/385, QuestBoard.IntegrationTests 513/513, 0 failures"
        status: pass
      - kind: integration
        ref: "dotnet test --filter FullyQualifiedName~PreviewSeries -- 3/3 pass (EventsControllerIntegrationTests, added this plan)"
        status: pass
    human_judgment: false
---

# Phase 76 Plan 11: Cross-Board Isolation and Coverage Verification Summary

**A dedicated two-board `EventSeriesTenantIsolationTests` class (12 facts) proving every read/write surface Phase 76 adds is board-scoped and both server-side refusals hold posted directly, plus three new `EventsControllerIntegrationTests` facts closing a genuine automated-evidence gap on `POST /Events/PreviewSeries` found while auditing the phase's per-requirement test filters.**

## Performance

- **Duration:** ~35 min
- **Started:** 2026-08-28T17:10:00+02:00 (approx, base commit 6188538)
- **Completed:** 2026-08-28T17:45:00+02:00
- **Tasks:** 2
- **Files modified:** 2 (1 created, 1 modified)

## Accomplishments

- `EventSeriesTenantIsolationTests` (12 facts, all passing) mirrors `EventTenantIsolationTests`'s structure exactly: `IClassFixture<WebApplicationFactoryBase>` + `IAsyncLifetime` resetting the shared board context to 1, the same mobile User-Agent constant, and a `SeedOtherBoardSeriesAsync` helper that writes a realistic series plus a chosen number of occurrences (template fields, anchor, interval, mask, distinct slot indexes) through a context with no active board selected.
- Four read-surface facts prove a board-2 series and its occurrences are invisible from board 1's desktop calendar and mobile agenda HTML, and that a direct GET of the occurrence or series details page for a board-2 identifier returns not-found.
- Five write-surface facts post Cancel, End, Delete, Detach and a future-scope Edit from board 1 against board-2 identifiers and prove the target row is read back unchanged through a fresh context -- outcome-based rather than status-code-based, since the query filter (NotFound) and the controller's explicit second-layer check (BadRequest) are two independent defenses that legitimately fire first depending on the action.
- One create-surface fact posts a recurring create from board 1 with a spoofed `GroupId=2` field and proves the created series and every one of its occurrences carry board 1.
- Two refusal facts post Delete for a series occurrence and Cancel for a one-off event on the acting board directly (no view involved) and prove both return `BadRequest` with the target row's state unchanged.
- Confirmed the full suite green (`QuestBoard.UnitTests` 385/385, `QuestBoard.IntegrationTests` 510/510 before this plan's own additions, 513/513 after) and ran every validation-map filter individually. All resolved to at least one passing test except `PreviewSeries`, which resolved to zero -- the underlying `PreviewAsync` cadence math was already covered at the service layer under a different test name, but the controller action itself (`POST /Events/PreviewSeries`'s JSON response shape, mask-validation-error passthrough, and DM-only access) had no automated coverage under any name. Added three facts to the existing `EventsControllerIntegrationTests` class to close that gap.

## Task Commits

Each task was committed atomically:

1. **Task 1: Write the two-board series isolation and refusal tests** - `ae949162` (test)
2. **Task 2: Run the full suite and record the phase's automated coverage** - `7c2fc0e6` (test) -- closed the `PreviewSeries` filter gap found during verification

**Plan metadata:** committed alongside this SUMMARY (worktree mode -- orchestrator finalizes the metadata commit after merge)

## Files Created/Modified

- `QuestBoard.IntegrationTests/Tests/EventSeriesTenantIsolationTests.cs` - New: 12 facts covering series/occurrence read visibility, mutating-action refusal, spoofed-board create rejection, and the two direct-post refusal rules
- `QuestBoard.IntegrationTests/Controllers/EventsControllerIntegrationTests.cs` - Added 3 facts: `PreviewSeries_Post_ValidCadence_ReturnsSuccessWithGeneratedDates`, `PreviewSeries_Post_InvalidMask_ReturnsSuccessFalseWithError`, `PreviewSeries_Post_PlayerAccess_ShouldBeBlocked`

## Decisions Made

- Cross-board mutating-action facts assert on outcome (`IsSuccessStatusCode` false, not a redirect, and the target row unchanged when read back through a fresh context on board 2) rather than a single hard-coded status code, per the plan's own guidance that a refusal could reasonably surface as either not-found or bad-request depending on which of the two independent defense layers (EF Core's query filter vs. the controller's explicit board comparison) fires first for a given action.
- The two own-board refusal facts (`Delete_Post_SeriesOccurrenceOnActiveBoard...`, `Cancel_Post_OneOffEventOnActiveBoard...`) seed their fixtures directly on board 1 rather than reusing the cross-board seeding helper, since they are deliberately testing a different rule (Cancel-vs-Delete enforcement) that has nothing to do with board isolation.
- `SeedOtherBoardSeriesAsync` captures every seeded occurrence id from the tracked `EventEntity` objects themselves (populated by the identity generator on `SaveChangesAsync`) rather than re-querying the same context afterward -- an early version tried the re-query and got an empty list every time, because the seeding context runs with `ActiveGroupId = null`, and the same query filter that makes the cross-board write possible also hides those same rows from any read on that context.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 - Missing Critical] Added automated coverage for `POST /Events/PreviewSeries`**
- **Found during:** Task 2 (running every named validation-map filter individually per the plan's explicit instruction)
- **Issue:** The `PreviewSeries` filter named in `76-VALIDATION.md` for EVTRECUR-02 resolved to zero tests. The underlying `EventSeriesService.PreviewAsync` cadence math already had automated coverage (`EventSeriesMaterializationTests.PreviewAsync_SameCadenceAsMaterialization_ReturnsExactDatesTop...`), but the controller action itself -- its JSON success/error response shape, its mask-validation-error passthrough, and its DM-only access gate -- had zero coverage under any test name across either project.
- **Fix:** Added three facts to `EventsControllerIntegrationTests.cs` (the existing, natural home for an `EventsController` action test): a valid-cadence success-shape fact, an all-zero-mask validation-error fact, and a player-blocked access fact.
- **Files modified:** `QuestBoard.IntegrationTests/Controllers/EventsControllerIntegrationTests.cs`
- **Verification:** `dotnet test --filter FullyQualifiedName~PreviewSeries` -- 3/3 pass; full suite re-run green after the addition (513/513 integration tests)
- **Committed in:** `7c2fc0e6`

---

**Total deviations:** 1 auto-fixed (1 missing critical test coverage)
**Impact on plan:** The plan's own Task 2 action text explicitly anticipated and authorized this exact scenario ("add the missing test to whichever of the four existing test classes it belongs in rather than leaving the gap"). No scope creep beyond closing the one genuine automated-evidence gap the plan's own verification step was designed to surface.

## Issues Encountered

None beyond the seeding-helper self-check described in Decisions Made above, caught and fixed before any task's acceptance criteria were checked.

## User Setup Required

None - no external service configuration required. No migration in this plan.

## Next Phase Readiness

- Every Phase 76 requirement now has a named, discoverable automated filter that resolves to at least one passing test: `EventSeriesDateGeneratorTests` (21), `MirroredMask` (2), `PreviewSeries` (3, integration, added this plan), `TopUpAsync` (11), `Cancel` (7 unit + 2 integration = 9), `MoveThenRun` (3), `EditScope` (3), `Idempotency` (1), `EventSeriesTenantIsolationTests` (12, added this plan), `RecurringOccurrenceTopUpJobTests` (5).
- Full suite green: `QuestBoard.UnitTests` 385/385, `QuestBoard.IntegrationTests` 513/513, 0 failures, both test projects.
- The phase's `76-VALIDATION.md` per-requirement map is now fully backed by passing, discoverable automated tests -- no requirement carries a zero-test filter into the phase's verification step.
- This is the phase's closing validation plan; no further plans depend on this one. No blockers for merge -- this plan touched only test files (its own declared file plus one pre-existing controller test class extended per its own Task 2's explicit instruction) and did not modify any production code.

---
*Phase: 76-recurring-event-series*
*Completed: 2026-08-28*

## Self-Check: PASSED

- FOUND: QuestBoard.IntegrationTests/Tests/EventSeriesTenantIsolationTests.cs
- FOUND: QuestBoard.IntegrationTests/Controllers/EventsControllerIntegrationTests.cs
- FOUND: commit ae949162 (Task 1)
- FOUND: commit 7c2fc0e6 (Task 2)
