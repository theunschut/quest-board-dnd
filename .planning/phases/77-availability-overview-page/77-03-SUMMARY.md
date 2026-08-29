---
phase: 77-availability-overview-page
plan: 03
subsystem: ui
tags: [razor, bootstrap5, automapper, mvc, xunit, availability]

requires:
  - phase: 77-01
    provides: EventAvailabilityOverview/EventAvailabilityRow/AvailabilityMember domain models, IEventService.GetAvailabilityOverviewAsync, EventsOverviewOptions
  - phase: 77-02
    provides: events-overview.css / events-overview.mobile.css cell vocabulary and count-block classes, nav entries and cross-links pointing at EventsController.Index
provides:
  - OverviewMemberViewModel / EventOverviewRowViewModel / EventOverviewViewModel and their AutoMapper entries
  - _AvailabilityCell.cshtml (five-state chip vocabulary, single source for both surfaces)
  - _AvailabilityCounts.cshtml (three-figure count block)
  - Views/Events/Index.cshtml (desktop sticky grid + legend) and Index.Mobile.cshtml (collapsible per-event cards)
  - EventsController.Index(int? take, CancellationToken) at GET /Events, server-side clamped page size
affects: [77-04]

tech-stack:
  added: []
  patterns:
    - "Controller assembles the container view model by hand (EventOverviewViewModel), mirroring CalendarController's CalendarViewModel assembly, with no AutoMapper entry for the container itself"
    - "Single shared _AvailabilityCell partial rendered by both the desktop grid and the mobile roster list so the two surfaces can never diverge on chip vocabulary"

key-files:
  created:
    - QuestBoard.Service/ViewModels/EventViewModels/OverviewMemberViewModel.cs
    - QuestBoard.Service/ViewModels/EventViewModels/EventOverviewRowViewModel.cs
    - QuestBoard.Service/ViewModels/EventViewModels/EventOverviewViewModel.cs
    - QuestBoard.Service/Views/Events/_AvailabilityCell.cshtml
    - QuestBoard.Service/Views/Events/_AvailabilityCounts.cshtml
    - QuestBoard.Service/Views/Events/Index.cshtml
    - QuestBoard.Service/Views/Events/Index.Mobile.cshtml
    - QuestBoard.UnitTests/ViewModels/EventsOverviewViewModelMappingTests.cs
    - QuestBoard.IntegrationTests/Controllers/EventsOverviewControllerIntegrationTests.cs
  modified:
    - QuestBoard.Service/Automapper/ViewModelProfile.cs
    - QuestBoard.Service/Controllers/Events/EventsController.cs

key-decisions:
  - "EventsController.Index carries class-level [Authorize] only, no policy attribute and no IsDmTierAsync/GetEffectiveRoleAsync call -- the same per-event availability is already visible one event at a time on Details, so gating the aggregate would restrict information that is already public per-event"
  - "No active-group check in the action body: GroupSessionMiddleware already redirects an authenticated GET with no active group to the group picker before the action runs, confirmed by Index_Get_SuperAdminWithNoActiveGroup_DoesNotThrow"
  - "take is clamped with Math.Clamp(take ?? DefaultTake, 1, MaxTake) rather than BadRequest, because the value normally arrives from the page's own Show More link and a clamp keeps a bookmarked or hand-edited URL working"

patterns-established:
  - "IOptions<EventsOverviewOptions> injected directly into the MVC controller (rather than into the domain service) since the clamp is a presentation-boundary concern, matching how EventSeriesOptions is consumed in the domain layer for a domain-layer concern"

requirements-completed: [EVTVIEW-01, EVTVIEW-02, EVTVIEW-03]

coverage:
  - id: D1
    description: "Three view models and two AutoMapper entries project the domain aggregate onto the presentation layer without a reverse map; counts and cell order survive the mapping"
    requirement: "EVTVIEW-02"
    verification:
      - kind: unit
        ref: "QuestBoard.UnitTests/ViewModels/EventsOverviewViewModelMappingTests.cs (3 tests)"
        status: pass
    human_judgment: false
  - id: D2
    description: "Five distinct cell renderings (ConfirmedYes/Maybe/No, UnconfirmedYes muted chip, Empty bare dash) rendered from one shared partial on both desktop and mobile"
    requirement: "EVTVIEW-02"
    verification:
      - kind: integration
        ref: "EventsOverviewControllerIntegrationTests#Index_UnconfirmedDefault_RendersMutedChip, Index_ConfirmedAnswer_RendersSolidChip, Index_MemberWithNoRowForOneEvent_RendersEmptyCell"
        status: pass
      - kind: other
        ref: "grep -c 'avail-cell-yes-muted|fa-clock|<em>Yes</em>|avail-cell-empty|&mdash;|bg-success|bg-warning text-dark|bg-danger' _AvailabilityCell.cshtml, all present"
        status: pass
    human_judgment: false
  - id: D3
    description: "Three-figure count block (headline Yes total, confirmed subset, Maybe) renders on every row/card"
    requirement: "EVTVIEW-03"
    verification:
      - kind: integration
        ref: "EventsOverviewControllerIntegrationTests#Index_RendersAllThreeCounts"
        status: pass
    human_judgment: false
  - id: D4
    description: "GET /Events returns 200 for any authenticated board member (no role gate), the take parameter is clamped server-side to EventsOverviewOptions.MaxTake, and a SuperAdmin with no active group never 500s"
    requirement: "EVTVIEW-01"
    verification:
      - kind: integration
        ref: "EventsOverviewControllerIntegrationTests#Index_PlayerOnCampaignBoard_ReturnsOk, Index_TakeAboveMax_IsClampedAndStillReturnsOk, Index_TakeZeroOrNegative_StillReturnsOk, Index_Get_SuperAdminWithNoActiveGroup_DoesNotThrow"
        status: pass
      - kind: other
        ref: "grep -c 'IgnoreQueryFilters' EventsController.cs == 0; grep -c 'DungeonMasterOnly' EventsController.cs unchanged at 10"
        status: pass
    human_judgment: false
  - id: D5
    description: "The whole event row/card navigates to Details; no form, POST, SetAvailability or Withdraw control anywhere on the page; Show More appears only when more events exist"
    requirement: "EVTVIEW-01"
    verification:
      - kind: integration
        ref: "EventsOverviewControllerIntegrationTests#Index_MoreEventsThanTake_ShowsShowMoreControl"
        status: pass
      - kind: other
        ref: "grep -Ec '<form|SetAvailability|method=\"post\"' Index.cshtml and Index.Mobile.cshtml both == 0"
        status: pass
    human_judgment: false
  - id: D6
    description: "Cancelled occurrences excluded from the list; an event dated today with a past start time is still listed; a board with no upcoming events renders the empty state"
    requirement: "EVTVIEW-01"
    verification:
      - kind: integration
        ref: "EventsOverviewControllerIntegrationTests#Index_CancelledOccurrence_IsNotListed, Index_EventDatedToday_IsListed, Index_NoUpcomingEvents_RendersEmptyState"
        status: pass
    human_judgment: false
  - id: D7
    description: "Mobile per-event cards lead with counts and keep the per-member breakdown behind a collapse toggle whose tap does not also trigger the card's navigation; muted-vs-confirmed cell reads as visually distinct"
    verification: []
    human_judgment: true
    rationale: "Mobile views in this app are user-agent selected, not breakpoint-driven -- devtools emulation does not exercise them, and perceived colour/shape contrast is a human judgement call, matching 77-VALIDATION.md's Manual-Only Verifications table."

duration: 45min
completed: 2026-08-29
status: complete
---

# Phase 77 Plan 03: Availability Overview View Models, Views, and Controller Action Summary

**EventsController.Index at GET /Events assembling a server-clamped EventOverviewViewModel, rendered by a shared five-state chip partial across a sticky desktop grid and a collapsible mobile card list, proven by 3 unit + 13 integration tests with zero write surface.**

## Performance

- **Duration:** 45 min
- **Started:** 2026-08-29T00:00:00Z (approx, worktree spawn)
- **Completed:** 2026-08-29
- **Tasks:** 3
- **Files modified:** 11 (9 created, 2 modified)

## Accomplishments
- Three view models (`OverviewMemberViewModel`, `EventOverviewRowViewModel`, `EventOverviewViewModel`) and two read-only AutoMapper entries projecting the domain aggregate onto the presentation layer with no reverse map, proven by 3 unit tests covering count/identity/cell-order survival
- `_AvailabilityCell.cshtml` — the single source of the five-state chip vocabulary (three confirmed vote badges, the dashed-border/clock-icon/italic muted-Yes default, and the bare-text empty cell), rendered identically by the desktop grid, the mobile roster list, and the legend on both surfaces
- `_AvailabilityCounts.cshtml` — the two-line three-figure count block (headline Yes total, confirmed subset, Maybe), used identically on both surfaces
- `Index.cshtml` — the desktop sticky grid (`avail-grid`) with two pinned leading columns, one clickable row per event, the viewer's own column highlighted, a legend card, an empty state and a conditional Show More control
- `Index.Mobile.cshtml` — the mobile per-event card list with counts leading every card, a collapsed roster behind a `stopPropagation`-guarded toggle, and a collapsed legend
- `EventsController.Index(int? take, CancellationToken)` at `GET /Events` — class-level `[Authorize]` only, no DM-tier gate, `take` clamped server-side via `Math.Clamp(take ?? DefaultTake, 1, MaxTake)`, no active-group assertion (the session middleware already handles that redirect)
- 13 new `EventsOverviewControllerIntegrationTests` covering all-members access, one-row-per-event/one-column-per-member rendering, all three cell-vs-count facts, the cancelled/today date boundary, the empty state, both take-clamp directions, the Show More trigger, and the SuperAdmin-no-active-group edge case
- Full solution build clean; full test suite green (399 unit + 549 integration, 0 failures)

## Task Commits

Each task was committed atomically:

1. **Task 1: Overview view models and their AutoMapper entries** - `88e12be9` (feat)
2. **Task 2: Desktop grid, mobile card list, and the two shared partials** - `df083972` (feat)
3. **Task 3: EventsController.Index with a clamped page size, plus its integration tests** - `d074b529` (feat)

## Files Created/Modified
- `QuestBoard.Service/ViewModels/EventViewModels/OverviewMemberViewModel.cs` - One column of the grid: UserId + Name
- `QuestBoard.Service/ViewModels/EventViewModels/EventOverviewRowViewModel.cs` - One row: event identity, three counts, positionally-aligned Cells list
- `QuestBoard.Service/ViewModels/EventViewModels/EventOverviewViewModel.cs` - The container: Members, Rows, HasMore, Take, NextTake, CurrentUserId
- `QuestBoard.Service/Automapper/ViewModelProfile.cs` - Added `AvailabilityMember -> OverviewMemberViewModel` and `EventAvailabilityRow -> EventOverviewRowViewModel` maps, no reverse map
- `QuestBoard.Service/Views/Events/_AvailabilityCell.cshtml` - Five-state chip vocabulary, single source for both surfaces
- `QuestBoard.Service/Views/Events/_AvailabilityCounts.cshtml` - Three-figure count block
- `QuestBoard.Service/Views/Events/Index.cshtml` - Desktop sticky grid + legend
- `QuestBoard.Service/Views/Events/Index.Mobile.cshtml` - Mobile per-event cards with collapsible roster
- `QuestBoard.Service/Controllers/Events/EventsController.cs` - Added `Index` action with `IOptions<EventsOverviewOptions>` clamp
- `QuestBoard.UnitTests/ViewModels/EventsOverviewViewModelMappingTests.cs` - 3 tests covering the mapping contract
- `QuestBoard.IntegrationTests/Controllers/EventsOverviewControllerIntegrationTests.cs` - 13 tests covering the controller action end to end

## Decisions Made
- `EventsController.Index` carries no `[Authorize(Policy = "DungeonMasterOnly")]` and never calls `IsDmTierAsync`/`GetEffectiveRoleAsync` — the same availability data is already visible one event at a time on `Details`, so gating the aggregate would restrict information already public per-event
- No active-group assertion in the action body: `GroupSessionMiddleware` already redirects an authenticated GET with no active group to `/groups/pick` before the action runs, so a SuperAdmin with no active board never reaches an exception path — proven by `Index_Get_SuperAdminWithNoActiveGroup_DoesNotThrow`
- `IOptions<EventsOverviewOptions>` is injected directly into the controller (a presentation-boundary clamp) rather than routed through the domain service, since the domain service's own `take` parameter is already an int and the clamp's only job is protecting the HTTP boundary

## Deviations from Plan

None - plan executed exactly as written. One test-writing note: `FluentAssertions`'s numeric assertion method is `BeLessThanOrEqualTo`, not `BeLessOrEqualTo` as might be assumed from other assertion libraries — caught immediately by the Task 3 build gate and fixed inline before commit (not a deviation from plan intent, just an API-name correction during test authoring).

## Issues Encountered

None beyond the FluentAssertions method-name correction noted above.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- `/Events` is fully wired end to end for a single board member's own group: view models, views, and controller action all exist and are covered by tests
- 77-02's nav entries and cross-links (added in the prior wave) now resolve to a real, working route
- Plan 77-04 (tenant isolation) can proceed: `EventsController.Index` performs no manual `GroupId` filtering and no `IgnoreQueryFilters()` call, relying entirely on the ambient fail-closed query filters already proven by 77-01's repository tests — the cross-board leak surface for 77-04 to verify is the aggregate read path itself, not this plan's controller code
- Manual mobile verification (real user agent) of the card-leads-with-counts / collapse-does-not-navigate behavior, and the visual muted-vs-confirmed distinction, remain open per `77-VALIDATION.md`'s Manual-Only Verifications table — flagged as `human_judgment: true` in the coverage block above

---
*Phase: 77-availability-overview-page*
*Completed: 2026-08-29*

## Self-Check: PASSED

All 9 created files verified present on disk (OverviewMemberViewModel.cs, EventOverviewRowViewModel.cs, EventOverviewViewModel.cs, _AvailabilityCell.cshtml, _AvailabilityCounts.cshtml, Index.cshtml, Index.Mobile.cshtml, EventsOverviewViewModelMappingTests.cs, EventsOverviewControllerIntegrationTests.cs). All 3 task commits verified present in git log (88e12be9, df083972, d074b529).
