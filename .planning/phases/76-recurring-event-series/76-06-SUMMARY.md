---
phase: 76-recurring-event-series
plan: 06
subsystem: ui
tags: [aspnet-core-mvc, razor, automapper, bootstrap5, fetch, debounce]

# Dependency graph
requires:
  - phase: 76-04
    provides: "IEventSeriesService (PreviewAsync, CreateWithFirstPassAsync, GetOccurrencesAsync) — the single Domain orchestration point this plan's controller calls"
  - phase: 76-01
    provides: "EventSeriesDateGenerator (TryParseMask, ParseMask, FormatMask, MaxCycleLength) — the pure cadence parser both the preview and create paths validate against"
provides:
  - "EventViewModel extended with the recurrence form inputs (IsRecurring, IntervalWeeks, CycleMask, SeriesEndDate) and the display/scope members (SeriesId, CancelledAt, IsCancelled, EditScope) later plans' Details/Edit/Series surfaces read"
  - "SeriesPreviewRequestViewModel — the bound model for the debounced preview POST"
  - "EventsController.PreviewSeries — a DM-only, antiforgery-protected read endpoint that returns up to 10 server-computed dates from the same generator CreateWithFirstPassAsync uses"
  - "EventsController.Create branch on IsRecurring that validates cadence server-side and creates the series plus its first generation pass in one transaction, with the one-off path left byte-for-byte unchanged"
  - "The Create Event form's repeats toggle, cycle-mask toggle strip and live preview panel, plus the .cycle-mask-strip/.cycle-mask-cell/.text-purple CSS this phase's later Series Details page will reuse for its read-only mask variant"
affects: [76-07, 76-08, 76-09 (Edit form scope dialog, Event Details cancel/series-link, Series Details page — all read the view model members and reuse the cycle-mask CSS this plan adds)]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "The Create POST branches on viewModel.IsRecurring before the existing one-off code runs, delegating to a private CreateSeriesAsync helper so the shipped one-off path stays untouched rather than being threaded with conditionals"
    - "The preview endpoint and the create branch both call EventSeriesDateGenerator.TryParseMask independently of ModelState/DataAnnotations, because the browser's 24-cell strip cap is convenience only — the true ~100-position ceiling is enforced server-side on every path that accepts a mask"
    - "The recurrence form's JS keeps a single source of truth (a cycleMaskPositions array) and re-renders both the visual strip and the hidden CycleMask input from it on every change, rather than trying to keep two representations in sync by hand"
    - "The debounced preview fetch guards against out-of-order responses with a monotonically increasing request counter compared at response time, not a naive last-response-wins"

key-files:
  created:
    - QuestBoard.Service/ViewModels/EventViewModels/SeriesPreviewRequestViewModel.cs
    - QuestBoard.Service/Views/Events/_SeriesFormScripts.cshtml
  modified:
    - QuestBoard.Service/ViewModels/EventViewModels/EventViewModel.cs
    - QuestBoard.Service/Automapper/ViewModelProfile.cs
    - QuestBoard.Service/Controllers/Events/EventsController.cs
    - QuestBoard.Service/Views/Events/Create.cshtml
    - QuestBoard.Service/wwwroot/css/modern-card.css

key-decisions:
  - "CreateSeriesAsync re-validates IntervalWeeks range server-side (viewModel.IntervalWeeks is < 1 or > 52) in addition to the [Range] attribute already enforced by ModelState.IsValid earlier in Create, because the plan's acceptance criteria call for the recurring branch to independently reject an out-of-range interval rather than relying solely on the model binder having already run."
  - "The Create redirect after a successful series save takes the earliest generated occurrence's date via a second eventSeriesService.GetOccurrencesAsync call rather than reusing the in-memory first-pass list, since CreateWithFirstPassAsync returns only the series row -- this keeps the controller from needing to change that service method's return shape for one redirect target."
  - "PreviewSeries returns success:false with status 200 (never a 4xx) on a validation failure, matching the plan's explicit rationale that a bad cadence is a normal form state the browser renders inline, not a transport failure."

patterns-established:
  - "A form's recurrence-toggle behavior (checkbox reveals a d-none-toggled section, not per-field disabled attributes) lives in its own partial view scoped to that page's element ids, matching how _QuestFormScripts.cshtml is already scoped to the quest form -- later plans adding an Edit-side or Series-side variant of this widget should follow the same isolation rather than growing this partial to cover multiple pages."

requirements-completed: [EVTRECUR-01, EVTRECUR-02]

coverage:
  - id: D1
    description: "EventViewModel carries the four recurrence form inputs plus SeriesId/CancelledAt/IsCancelled/EditScope, and the AutoMapper profile ignores every field a submitted form must never be allowed to set (including the newly added CancelledAt on the reverse map)"
    requirement: "EVTRECUR-01"
    verification:
      - kind: unit
        ref: "dotnet test QuestBoard.UnitTests --filter FullyQualifiedName~EntityProfileEnumCastTests -- 43/43 pass"
        status: pass
      - kind: integration
        ref: "dotnet test (full suite) -- QuestBoard.UnitTests 380/380 pass, QuestBoard.IntegrationTests 498/498 pass"
        status: pass
    human_judgment: false
  - id: D2
    description: "POST /Events/PreviewSeries is DM-only, antiforgery-protected, calls IEventSeriesService.PreviewAsync (the same generator path CreateWithFirstPassAsync uses) and returns up to 10 dates plus the AnchorFullyInPast flag with no database write"
    requirement: "EVTRECUR-02"
    verification:
      - kind: integration
        ref: "dotnet test QuestBoard.IntegrationTests --filter FullyQualifiedName~EventTenantIsolationTests -- 8/8 pass"
        status: pass
      - kind: manual_procedural
        ref: "grep: PreviewSeries preceded by [Authorize(Policy = \"DungeonMasterOnly\")] and [ValidateAntiForgeryToken]; file contains CreateWithFirstPassAsync and TryParseMask"
        status: pass
    human_judgment: false
  - id: D3
    description: "A recurring Create save validates the cadence server-side independent of the browser's 24-cell cap, and creates the series plus its whole first generation pass in one transaction with no background-job enqueue; the one-off Create path is unchanged"
    requirement: "EVTRECUR-01"
    verification:
      - kind: manual_procedural
        ref: "grep -v '//' EventsController.cs | grep -c backgroundJobClient -- returns 0; file contains literal \"Series created successfully.\" and \"Event created successfully.\""
        status: pass
      - kind: integration
        ref: "dotnet build (0 errors); full test suite unaffected (878/878 pass across both test projects)"
        status: pass
    human_judgment: false
  - id: D4
    description: "The Create form reveals a cadence section (repeats toggle, interval, derived weekday text, clickable cycle-mask strip with +/- and a 24-cell UI cap, optional end date, live preview panel) and the preview debounces 400ms, guards out-of-order responses, and degrades to inline text on failure with no page reload"
    verification:
      - kind: manual_procedural
        ref: "grep: Create.cshtml contains id=\"enableRecurrence\", id=\"recurrenceSection\", class=\"d-none\"; _SeriesFormScripts.cshtml contains /Events/PreviewSeries, __RequestVerificationToken, a 400ms debounce, zero location.reload, and the literal Maximum cycle length reached. / Couldn't generate a preview strings; modern-card.css contains .cycle-mask-strip/.cycle-mask-cell/.cycle-mask-cell.on/.text-purple with zero added modern-card-body references"
        status: pass
    human_judgment: true
    rationale: "The toggle strip, debounced preview and past-anchor messaging are interactive/visual behavior that grep and dotnet test can confirm exists and is wired to the right endpoint, but cannot confirm renders and feels correct in a real browser -- this needs a human (or a browser-driving UAT pass) to click the checkbox, toggle mask cells, and watch the preview list update."

# Metrics
duration: ~12min
completed: 2026-08-28
status: complete
---

# Phase 76 Plan 06: Create Event Form Recurrence Summary

**The Create Event form gained a "repeats" toggle, a clickable cycle-mask strip and a 400ms-debounced server-computed live preview, backed by a `PreviewSeries` endpoint and a `Create` branch that both call the same Domain generator as materialization, so the preview can never disagree with what a save actually creates.**

## Performance

- **Duration:** ~12 min
- **Started:** 2026-08-28T15:45:48+02:00 (approx., base commit timestamp)
- **Completed:** 2026-08-28T15:56:33+02:00
- **Tasks:** 3
- **Files modified:** 7 (2 created, 5 modified)

## Accomplishments

- `EventViewModel` now carries `IsRecurring`, `IntervalWeeks`, `CycleMask`, `SeriesEndDate` (form inputs) and `SeriesId`, `CancelledAt`, `IsCancelled`, `EditScope` (display/scope members), with the AutoMapper profile ignoring every recurrence field on the read-back map and ignoring `CancelledAt` (alongside the existing `GroupId`/`SeriesId`/`SeriesSlotIndex`/`CreatedAt`) on the write map so a submitted form can never forge a cancellation.
- `POST /Events/PreviewSeries` (DM-only, antiforgery-protected) validates the mask and interval, then calls `IEventSeriesService.PreviewAsync` and returns up to 10 dates as both an ISO value and a `dddd, d MMMM yyyy` label plus the `anchorFullyInPast` flag, entirely without touching the database.
- `EventsController.Create` branches on `viewModel.IsRecurring`: the one-off path is untouched byte-for-byte, and the new `CreateSeriesAsync` helper independently re-validates the cadence server-side, builds an `EventSeries` from the fields the DM already typed, and calls `CreateWithFirstPassAsync` inside one transaction — a mid-save exception adds a model error and returns the view with nothing persisted, and no background job is ever enqueued on this path.
- The Create form reveals a "Make this a recurring series" checkbox that toggles a `d-none` section containing the interval input, a derived read-only weekday line, the cycle-mask toggle strip (32px cells, `+`/`-` controls, 24-cell UI cap with a "Maximum cycle length reached." tooltip), an optional end date, and a "Next occurrences" preview list.
- `_SeriesFormScripts.cshtml` implements the toggle, the mask-strip click/add/remove behavior (single `cycleMaskPositions` array as source of truth, synced into a hidden `CycleMask` input), the derived-weekday computation, and the 400ms-debounced `fetch` to `PreviewSeries` guarded by a request counter against out-of-order responses, with "Calculating…", past-anchor and failure messaging matching the UI-SPEC copy exactly.
- `modern-card.css` gained `.cycle-mask-strip`, `.cycle-mask-cell`, `.cycle-mask-cell.on` and the `.text-purple` utility, with no change to `.modern-card-body` padding.

## Task Commits

Each task was committed atomically:

1. **Task 1: Extend EventViewModel with the series form and display fields and pin the mapping guards** - `61a81f4` (feat)
2. **Task 2: Add the PreviewSeries endpoint and the series branch on Create** - `21168fb` (feat)
3. **Task 3: Build the repeats toggle, cycle-mask strip and live preview panel on the Create form** - `1e901f0` (feat)

**Plan metadata:** committed alongside this SUMMARY (worktree mode — orchestrator finalizes the metadata commit after merge)

## Files Created/Modified

- `QuestBoard.Service/ViewModels/EventViewModels/EventViewModel.cs` - Added `IsRecurring`, `IntervalWeeks`, `CycleMask`, `SeriesEndDate`, `SeriesId`, `CancelledAt`, `IsCancelled`, `EditScope`
- `QuestBoard.Service/ViewModels/EventViewModels/SeriesPreviewRequestViewModel.cs` - New bound model for the preview POST (`AnchorDate`, `IntervalWeeks`, `CycleMask`, `EndDate`)
- `QuestBoard.Service/Automapper/ViewModelProfile.cs` - Ignore the four recurrence fields and `EditScope` on `Event -> EventViewModel`; ignore `CancelledAt` on `EventViewModel -> Event`
- `QuestBoard.Service/Controllers/Events/EventsController.cs` - Injected `IEventSeriesService`; added `PreviewSeries` action and the `CreateSeriesAsync` branch off `Create`
- `QuestBoard.Service/Views/Events/Create.cshtml` - Repeats checkbox, revealed recurrence section, antiforgery token registration, new script partial render
- `QuestBoard.Service/Views/Events/_SeriesFormScripts.cshtml` - New partial: toggle, cycle-mask strip, derived weekday, debounced preview fetch
- `QuestBoard.Service/wwwroot/css/modern-card.css` - `.cycle-mask-strip`, `.cycle-mask-cell`, `.cycle-mask-cell.on`, `.text-purple`

## Decisions Made

- `CreateSeriesAsync` independently checks `IntervalWeeks` is within 1–52 (not just relying on the `[Range]` attribute already validated earlier in `Create`), matching the plan's instruction that the interval check "must also reject an interval outside the view model's range" as part of the recurring branch's own validation.
- The redirect target after a successful series save is computed from a follow-up `GetOccurrencesAsync` call (earliest occurrence date, falling back to the anchor date if none were generated) rather than reusing `CreateWithFirstPassAsync`'s return value, since that method returns only the `EventSeries` row.
- `PreviewSeries` always returns HTTP 200, even on a validation failure, with a `success: false` flag and an error string — matching the plan's explicit rationale that a bad cadence is a normal, inline-rendered form state rather than a transport error.

## Deviations from Plan

None - plan executed exactly as written. All three tasks' acceptance criteria (grep assertions for exact strings/ids, `dotnet build` exit 0, the two named test-filter runs, and the full test suite) were verified directly.

## Issues Encountered

None. The full test suite (`QuestBoard.UnitTests`: 380/380, `QuestBoard.IntegrationTests`: 498/498) passed with no changes needed beyond what the plan specified.

## User Setup Required

None - no external service configuration required. No migration in this plan (schema already shipped in 76-02).

## Next Phase Readiness

- The extended `EventViewModel` (`SeriesId`, `CancelledAt`, `IsCancelled`, `EditScope`) is ready for the Edit-form scope dialog and Event Details cancel/series-link work later in this phase.
- `.cycle-mask-strip` / `.cycle-mask-cell` are ready to be reused, read-only, on the Series Details page (D-10) without new CSS.
- No blockers. `_SeriesFormScripts.cshtml` is scoped to the Create page's element ids; a later plan adding recurrence-adjacent JS to another page should give it its own partial rather than extending this one.

---
*Phase: 76-recurring-event-series*
*Completed: 2026-08-28*
