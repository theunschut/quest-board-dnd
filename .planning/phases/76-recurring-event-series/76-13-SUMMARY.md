---
phase: 76-recurring-event-series
plan: 13
subsystem: ui
tags: [razor, mvc-views, mobile, integration-tests, xunit]

requires:
  - phase: 76-recurring-event-series (76-10)
    provides: CalendarController.Index populating SeriesBelowRunway view-agnostically; desktop horizon banner in Index.cshtml
provides:
  - First automated coverage of the low-runway horizon banner on either calendar surface (CalendarHorizonBannerTests, 6 facts)
  - Mobile calendar (Index.Mobile.cshtml) now renders the same manager-gated horizon banner the desktop calendar already had
affects: [76-recurring-event-series (remaining gap-closure plans), verification]

tech-stack:
  added: []
  patterns:
    - "Mobile-view-selection-by-user-agent test pattern: GetWithUserAgentAsync harness copied from EventSeriesTenantIsolationTests/LayoutNavigationTests, anchoring every mobile assertion on the agenda-card-mobile marker so a NotContain assertion cannot pass for free against a login redirect"

key-files:
  created:
    - QuestBoard.IntegrationTests/Controllers/CalendarHorizonBannerTests.cs
  modified:
    - QuestBoard.Service/Views/Calendar/Index.Mobile.cshtml

key-decisions:
  - "Ported the desktop banner block verbatim (same gate expression, same copy, same series links) rather than introducing a shared partial, matching the plan's explicit instruction not to add a new flag, ViewBag entry or view-model member"
  - "Used mb-3 spacing on the mobile banner instead of the desktop's m-3 mb-0, because the mobile card already supplies horizontal padding via container-fluid px-2"

requirements-completed: [EVTRECUR-03]

coverage:
  - id: D1
    description: "A DM who works from a phone sees the low-runway warning, naming the affected series, on the calendar they actually open"
    requirement: "EVTRECUR-03"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/CalendarHorizonBannerTests.cs#MobileCalendar_DmWithSeriesBelowRunway_RendersHorizonBanner"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/CalendarHorizonBannerTests.cs#MobileCalendar_DmWithTwoSeriesBelowRunway_RendersMultiSeriesBanner"
        status: pass
    human_judgment: false
  - id: D2
    description: "A DM sees the same warning wording on mobile as on desktop"
    requirement: "EVTRECUR-03"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/CalendarHorizonBannerTests.cs#DesktopCalendar_DmWithSeriesBelowRunway_RendersHorizonBanner"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/CalendarHorizonBannerTests.cs#MobileCalendar_DmWithSeriesBelowRunway_RendersHorizonBanner"
        status: pass
    human_judgment: false
  - id: D3
    description: "A player never sees the warning on either surface"
    requirement: "EVTRECUR-03"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/CalendarHorizonBannerTests.cs#MobileCalendar_PlayerWithSeriesBelowRunway_DoesNotRenderHorizonBanner"
        status: pass
    human_judgment: false
  - id: D4
    description: "The warning is reachable on a Campaign board as well as a One-Shot board"
    requirement: "EVTRECUR-03"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/CalendarHorizonBannerTests.cs#MobileCalendar_CampaignBoardDm_RendersHorizonBanner"
        status: pass
    human_judgment: false

duration: 20min
completed: 2026-08-28
status: complete
---

# Phase 76 Plan 13: Mobile Horizon Banner Summary

**Ported the DM low-runway horizon banner into the mobile calendar view and added the first automated test coverage for it on either calendar surface.**

## Performance

- **Duration:** ~20 min
- **Started:** 2026-08-28T23:28:00+02:00
- **Completed:** 2026-08-28T23:30:26+02:00
- **Tasks:** 2
- **Files modified:** 2 (1 created, 1 modified)

## Accomplishments

- Wrote `CalendarHorizonBannerTests` (6 facts) — the first automated coverage of the horizon banner on either calendar surface — and proved the mobile gap was real: `MobileCalendar_DmWithSeriesBelowRunway_RendersHorizonBanner`, `MobileCalendar_DmWithTwoSeriesBelowRunway_RendersMultiSeriesBanner`, and `MobileCalendar_CampaignBoardDm_RendersHorizonBanner` all failed against the pre-fix mobile view, while `DesktopCalendar_DmWithSeriesBelowRunway_RendersHorizonBanner` (the already-shipped desktop behavior) and the two negative facts (player absence, at-runway-target absence) passed — confirming the defect was a mobile view-layer omission and nothing deeper.
- Ported the desktop's `Model.CanManage && Model.SeriesBelowRunway.Any()`-gated banner block into `Index.Mobile.cshtml`, positioned between the month-navigation bar and the agenda list, with identical copy, identical single/multi-series branches, and identical series-detail links.
- All six facts now pass with zero changes to the test file, closing the observable half of EVTRECUR-03: a DM working from a phone now gets the same rolling-window signal a DM on desktop already had.

## Task Commits

Each task was committed atomically:

1. **Task 1: Write the horizon banner render tests, proving the mobile facts fail today** - `d6c884a` (test)
2. **Task 2: Render the horizon banner on the mobile calendar** - `ae8e345` (feat)

**Plan metadata:** (this commit)

## Files Created/Modified

- `QuestBoard.IntegrationTests/Controllers/CalendarHorizonBannerTests.cs` - New test class with 6 facts covering DM/player visibility, single/multi-series wording, desktop regression guard, at-runway-target absence, and Campaign board reachability
- `QuestBoard.Service/Views/Calendar/Index.Mobile.cshtml` - Added the manager-gated horizon banner block, ported verbatim from `Index.cshtml` with `mb-3` spacing instead of `m-3 mb-0`

## Decisions Made

- Reused the existing `SeriesBelowRunway`/`CanManage` view-model members with no new flag, ViewBag entry, or second source of truth for the manager gate — as the plan's threat model required (T-76-52).
- Followed the `EventSeriesTenantIsolationTests`/`LayoutNavigationTests` harness shape exactly (mobile/desktop user-agent constants, `GetWithUserAgentAsync` overload) rather than inventing a new one, so the mobile-view-selection-by-user-agent behavior is exercised the same proven way.

## Deviations from Plan

None - plan executed exactly as written. Both tasks matched their acceptance criteria on the first implementation pass; no auto-fixes, no architectural questions, no auth gates.

## Issues Encountered

None.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- The horizon banner half of EVTRECUR-03's user-facing gap is closed. `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~CalendarHorizonBannerTests|FullyQualifiedName~MobileViewsTests"` passes 59/59 (6 new + 53 pre-existing mobile calendar facts), with no regression to the existing mobile agenda coverage.
- The verification report's second gap (Campaign boards cannot reach the calendar through normal navigation, per `76-VERIFICATION.md`) is out of scope for this plan and is handled by a sibling gap-closure plan in the same wave.
- `QuestBoard.Service/Views/Calendar/Index.cshtml` (desktop) was not touched — confirmed via `git diff` — so the already-verified desktop behavior carries no risk from this change.

---
*Phase: 76-recurring-event-series*
*Completed: 2026-08-28*
