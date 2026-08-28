---
phase: 76-recurring-event-series
plan: 04
subsystem: domain
tags: [dateonly, cycle-mask, recurrence, idempotency, ef-core-inmemory, xunit, nsubstitute, fluentassertions]

# Dependency graph
requires:
  - phase: 76-01
    provides: "EventSeriesDateGenerator (GenerateSlots, DateForSlot, ParseMask/TryParseMask/FormatMask, MaxSlotScan)"
  - phase: 76-03
    provides: "IEventSeriesRepository/EventSeriesRepository, IEventRepository additions (SetCancelledAsync, ApplyTemplateToOccurrencesAsync, CountLiveSiblingsOnDateAsync, GetOccurrencesForSeriesAsync), IEventService.SetCancelledAsync"
provides:
  - "IEventSeriesService/EventSeriesService — the single Domain orchestration point for preview, first-pass creation, idempotent runway top-up, series lifecycle (End/Delete/Detach), and the this-and-future edit scope sweep"
  - "EventSeriesOptions registered with DI defaults (RunwaySize 20, PreviewCount 10) so no deployment config change is required"
  - "EventSeriesMaterializationTests — 14 in-memory tests proving the idempotency, cancel, move, mirrored-mask and edit-scope guarantees against real repository/DbContext code"
affects: [76-recurring-event-series (controller/job/view plans still to come in this phase)]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "TopUpAsync's slot-membership check runs immediately before every single occurrence write inside the generation loop, not once at the top of the method — this is what keeps a retry after a mid-run crash monotonic (creates only the remainder, never a duplicate)"
    - "ApplyTemplateToFutureAsync computes occurrence eligibility against the OLD template values before writing the new ones to the series row, so the separately-edited exclusion cannot be defeated by comparing against the just-written new template"
    - "IEventSeriesService deliberately does not derive from IBaseService<EventSeries> — the generic add/update/remove members that interface would add offer a second, ungoverned way to remove a series that bypasses the deliberate Delete-versus-Detach split"

key-files:
  created:
    - QuestBoard.Domain/Interfaces/IEventSeriesService.cs
    - QuestBoard.Domain/Services/EventSeriesService.cs
    - QuestBoard.UnitTests/Repository/EventSeriesMaterializationTests.cs
  modified:
    - QuestBoard.Domain/Extensions/ServiceExtensions.cs

key-decisions:
  - "PreviewAsync materializes the generator's firing-slot output once into a list, then derives both the anchor-relative window (for AnchorFullyInPast) and the today-or-later window (the actual preview dates) from that single materialized list, rather than re-running GenerateSlots twice."
  - "TopUpAsync and CreateWithFirstPassAsync both resolve the active group via IActiveGroupContext.RequireActiveGroupId() rather than trusting the loaded series' own GroupId column — this matches how the future Hangfire job will call TopUpAsync (RunInScopeAsync sets the group context per iteration) and fails closed the same way CreateWithFirstPassAsync must for a fresh save with no board selected."
  - "CreateWithFirstPassAsync leaves each built occurrence's SeriesId unset (the repository's CreateWithOccurrencesAsync stamps it after the series row gets its id), while TopUpAsync sets occurrence.SeriesId explicitly before its own direct AddAsync/AddWithCampaignFanOutAsync call, since it does not go through CreateWithOccurrencesAsync — the two callers of the same private BuildOccurrenceFromTemplate helper differ only in this one line, deliberately, because the series id does not exist yet on the first-pass path."

patterns-established:
  - "Every read-side delegate method on EventSeriesService (GetSeriesBelowRunwayAsync, GetActiveSeriesForActiveGroupAsync, GetRemovalImpactAsync) resolves DateOnly.FromDateTime(DateTime.Today) itself and passes it down — the generator and the repository stay clock-free, matching the phase's established pattern from 76-01."

requirements-completed: [EVTRECUR-02, EVTRECUR-03, EVTRECUR-05, EVTRECUR-06, EVTRECUR-07]

coverage:
  - id: D1
    description: "IEventSeriesService/EventSeriesService give the phase its single materialization path (TopUpAsync) and first-pass creation path (CreateWithFirstPassAsync), both consuming EventSeriesDateGenerator and the plan-03 repository with no reimplementation of date math or the idempotency query"
    requirement: "EVTRECUR-03"
    verification:
      - kind: unit
        ref: "dotnet build (0 errors); grep assertions per plan acceptance criteria (interface shape, no IBaseService inheritance outside a comment, AddWithCampaignFanOutAsync/GetSlotIndexesForSeriesAsync present, no UpdatedAt reference, DI registration lines present, zero GSD-reference strings)"
        status: pass
    human_judgment: false
  - id: D2
    description: "PreviewAsync runs the exact same generator TopUpAsync/CreateWithFirstPassAsync later materialize from, with zero database access, so the live preview cannot disagree with what actually gets created"
    requirement: "EVTRECUR-02"
    verification:
      - kind: unit
        ref: "QuestBoard.UnitTests/Repository/EventSeriesMaterializationTests.cs#PreviewAsync_SameCadenceAsMaterialization_ReturnsExactDatesTopUpWouldMaterialize"
        status: pass
    human_judgment: false
  - id: D3
    description: "Re-running TopUpAsync never duplicates (double-run idempotency), never resurrects a cancelled occurrence, and never recreates a moved occurrence — including one moved two years beyond the runway — because the slot-membership check runs before every single write with no date predicate"
    requirement: "EVTRECUR-07"
    verification:
      - kind: unit
        ref: "dotnet test QuestBoard.UnitTests --filter FullyQualifiedName~Idempotency: 1/1 pass"
        status: pass
      - kind: unit
        ref: "dotnet test QuestBoard.UnitTests --filter FullyQualifiedName~MoveThenRun: 3/3 pass"
        status: pass
      - kind: unit
        ref: "QuestBoard.UnitTests/Repository/EventSeriesMaterializationTests.cs#TopUpAsync_CancelOccurrenceThenRun_Cancel_CreatesOneAtNextUnseenSlotAndKeepsCancelledRowWithSignups"
        status: pass
      - kind: unit
        ref: "dotnet test QuestBoard.UnitTests --filter FullyQualifiedName~TopUpAsync: 10/10 pass"
        status: pass
    human_judgment: false
  - id: D4
    description: "Campaign-board top-up occurrences carry one automatic signup per member with the answered marker left null (not stamped); one-shot-board occurrences carry no signup rows at all — asserted directly on persisted EventSignupEntity rows, not just row counts"
    requirement: "EVTRECUR-03"
    verification:
      - kind: unit
        ref: "QuestBoard.UnitTests/Repository/EventSeriesMaterializationTests.cs#TopUpAsync_CampaignBoard_OccurrencesCarryOneSignupPerMemberWithAnsweredMarkerUnset"
        status: pass
      - kind: unit
        ref: "QuestBoard.UnitTests/Repository/EventSeriesMaterializationTests.cs#TopUpAsync_OneShotBoard_OccurrencesCarryNoSignupRows"
        status: pass
    human_judgment: false
  - id: D5
    description: "Two series on two different boards with mirrored cycle masks, same anchor and interval, produce zero shared occurrence dates once both are topped up"
    requirement: "EVTRECUR-05"
    verification:
      - kind: unit
        ref: "QuestBoard.UnitTests/Repository/EventSeriesMaterializationTests.cs#TopUpAsync_TwoSeriesOnTwoBoardsWithMirroredMask_MirroredMask_ProduceZeroSharedDatesAfterTopUp"
        status: pass
    human_judgment: false
  - id: D6
    description: "ApplyTemplateToFutureAsync (the this-and-future edit scope) updates the series template and every future untouched occurrence, while leaving past occurrences, the just-edited occurrence, a cancelled occurrence, a separately-moved occurrence, and a separately-edited occurrence all untouched"
    requirement: "EVTRECUR-06"
    verification:
      - kind: unit
        ref: "dotnet test QuestBoard.UnitTests --filter FullyQualifiedName~EditScope: 3/3 pass"
        status: pass
    human_judgment: false

# Metrics
duration: ~25min
completed: 2026-08-28
status: complete
---

# Phase 76 Plan 04: Series Materialization Orchestration Summary

**`EventSeriesService` — the single Domain orchestration point (preview, idempotent runway top-up, first-pass creation, lifecycle, this-and-future edit scope) built directly on the phase's pure generator and persistence layer, proven by 14 in-memory tests including the load-bearing double-run, cancel-then-run, move-then-run and mirrored-mask non-collision facts.**

## Performance

- **Duration:** ~25 min
- **Started:** 2026-08-28T15:30:00+02:00 (approx.)
- **Completed:** 2026-08-28T15:41:34+02:00
- **Tasks:** 2
- **Files modified:** 4 (3 created, 1 modified)

## Accomplishments

- `IEventSeriesService`/`EventSeriesService` implement every orchestration method the phase's later controller and Hangfire job plans need: `PreviewAsync` (zero-DB, shares the generator), `CreateWithFirstPassAsync` (one transaction for the series row plus its first runway of occurrences), `TopUpAsync` (the single idempotent materializer both the controller path and the nightly job will call), the six read/lifecycle delegates (`GetSeriesAsync`, `GetActiveSeriesForActiveGroupAsync`, `GetOccurrencesAsync`, `GetSeriesBelowRunwayAsync`, `GetRemovalImpactAsync`, `CountLiveSiblingsOnDateAsync`, `EndAsync`, `DeleteAsync`, `DetachAsync`), and `ApplyTemplateToFutureAsync` (the this-and-future scope sweep).
- `EventSeriesOptions` is registered via `AddOptions<EventSeriesOptions>().BindConfiguration(...)` and `IEventSeriesService` via `AddScoped`, both in `ServiceExtensions.cs` beside the existing service registrations — a deployment with no matching configuration section still gets a runway of 20 and a preview of 10.
- `TopUpAsync`'s generation loop checks slot membership immediately before every single write (not once at the top), so a crash mid-run and a subsequent retry finds the earlier occurrences already present and only creates the remainder — proven directly by the double-run idempotency test and the two move-then-run tests (one moving an occurrence to a nearby date, one moving it two full years beyond the runway).
- `ApplyTemplateToFutureAsync` computes occurrence eligibility against the series' *old* template values before overwriting the series row, so a separately-edited occurrence's title mismatch is still detectable at eligibility-check time — proven by three tests covering the "updates future untouched" case, the "skips past/edited/cancelled" case, and the "skips separately-moved/separately-edited" case independently.
- 14 tests in `EventSeriesMaterializationTests` build a real `EventSeriesService` over real `EventSeriesRepository`/`EventRepository` instances backed by an in-memory `QuestBoardContext`, with `IBoardTypeResolver`/`IUserRepository` as NSubstitute substitutes and a small `RunwaySize = 5` for speed — including a direct assertion that a campaign board's automatic signup rows leave `UpdatedAt` null and a two-board mirrored-mask (`1,1,0,0` vs `0,0,1,1`) top-up sharing zero dates.

## Task Commits

Each task was committed atomically:

1. **Task 1: Create IEventSeriesService and EventSeriesService with preview, first-pass creation, and idempotent top-up** - `f745a41` (feat)
2. **Task 2: Prove idempotency with in-memory materialization tests** - `19db969` (test)

**Plan metadata:** committed alongside this SUMMARY (worktree mode — orchestrator finalizes the metadata commit after merge)

## Files Created/Modified

- `QuestBoard.Domain/Interfaces/IEventSeriesService.cs` - New standalone interface (not `IBaseService<EventSeries>`), 12 members
- `QuestBoard.Domain/Services/EventSeriesService.cs` - New Domain service implementing every member; `BuildOccurrenceFromTemplate` private helper shared by `CreateWithFirstPassAsync` and `TopUpAsync`
- `QuestBoard.Domain/Extensions/ServiceExtensions.cs` - Registered `EventSeriesOptions` (code-defaulted) and `IEventSeriesService`
- `QuestBoard.UnitTests/Repository/EventSeriesMaterializationTests.cs` - 14 new xUnit facts covering fresh-series top-up, idempotency, cancel-then-run, two move-then-run cases, end-date-passed, end-date-mid-window, campaign fan-out, one-shot no-fan-out, mirrored-mask two-board, three edit-scope cases, and a preview-matches-materialization check

## Decisions Made

- `PreviewAsync` materializes the generator's firing-slot sequence into a single list once, then derives both the anchor-relative window (for `AnchorFullyInPast`) and the today-or-later window (the returned dates) from that one list, avoiding a second `GenerateSlots` enumeration.
- Both `TopUpAsync` and `CreateWithFirstPassAsync` resolve the active group through `IActiveGroupContext.RequireActiveGroupId()` rather than trusting the already-loaded series' `GroupId` column, so the same fail-closed behavior applies uniformly whether the caller is a controller request or (in a later plan) the nightly job iterating groups via `HangfireJobHelper.RunInScopeAsync`.
- `CreateWithFirstPassAsync` deliberately leaves each built occurrence's `SeriesId` unset (the repository's `CreateWithOccurrencesAsync` stamps it once the series row has an id), while `TopUpAsync` sets `occurrence.SeriesId` explicitly before its own direct write, since it does not route through `CreateWithOccurrencesAsync`.

## Deviations from Plan

None - plan executed exactly as written. Both tasks' acceptance criteria (interface/service shape via grep, DI registration, zero GSD-reference strings, `dotnet build` exit 0, the four named-filter test-count minimums) were verified directly.

One self-caught test-authoring bug (not a deviation from the plan, not a defect in the service under test): the mirrored-mask two-board test's final verification queried through a context with `ActiveGroupId = null`, which the app's fail-closed `HasQueryFilter` on `EventEntity`/`EventSeriesEntity` correctly turns into "return nothing" rather than "return everything" — the test was fixed to read each board's occurrences back through that board's own group-scoped context instead, since a null active group is a fail-closed sentinel in this codebase's convention (from Phase 55), not a cross-group escape hatch.

## Issues Encountered

None blocking. The one test-authoring fix above was caught by the test itself on first run and corrected before committing Task 2.

## TDD Gate Compliance

Task 2 carries `tdd="true"`. Its production code (`EventSeriesService`, all members) was already built and committed in Task 1 — this task's purpose was to add proof against already-correct code, matching the same shape as plan 76-03's Task 3. A literal RED-then-GREEN cycle was not applicable: the methods under test were already correct by the time this task ran, so the fail-fast rule's "a test passing unexpectedly during RED means investigate" case did not arise in the sense of a missing feature — the one genuine unexpected-failure case (the mirrored-mask test's null-group read) was investigated and fixed before commit, consistent with that rule's intent. The task was executed as a single `test(...)` commit after write-fix-verify, matching the plan's own task ordering (service code in Task 1, tests in Task 2).

## User Setup Required

None - no external service configuration required. No migration in this plan (schema already shipped in 76-02).

## Next Phase Readiness

- `EventSeriesDateGenerator` (76-01), the persistence layer (76-03), and this plan's `EventSeriesService` together give the phase's remaining plans (the Hangfire top-up job, the controller/view surface, the series lifecycle UI) every Domain-layer operation they need with no further orchestration work — the job needs only to iterate groups via `HangfireJobHelper.RunInScopeAsync` and call `TopUpAsync` per active series, and the controller needs only to call `PreviewAsync`/`CreateWithFirstPassAsync`/`ApplyTemplateToFutureAsync` directly.
- No blockers.

---
*Phase: 76-recurring-event-series*
*Completed: 2026-08-28*

## Self-Check: PASSED

- FOUND: QuestBoard.Domain/Interfaces/IEventSeriesService.cs
- FOUND: QuestBoard.Domain/Services/EventSeriesService.cs
- FOUND: QuestBoard.UnitTests/Repository/EventSeriesMaterializationTests.cs
- FOUND: commit f745a41 (Task 1)
- FOUND: commit 19db969 (Task 2)
