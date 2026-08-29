---
phase: 77-availability-overview-page
plan: 02
subsystem: ui
tags: [razor, bootstrap5, css, navigation, integration-tests]

requires:
  - phase: 75-event-availability-signups
    provides: EventSignup.HasAnswered as the D-01/D-04 muted-vs-confirmed input
  - phase: 76-recurring-event-series
    provides: cancelled-occurrence tombstone this page's aggregate excludes
provides:
  - events-overview.css / events-overview.mobile.css cell vocabulary, count block, and grid/card layout rules
  - Desktop Calendar nav entry converted to a dropdown holding Calendar and Availability Overview
  - Mobile flat sibling nav entry for Availability Overview
  - Cross-links from both Calendar views to the overview
  - Four new LayoutNavigationTests theories proving presence for DM/player on both board types and both user agents, and absence for anonymous
affects: [77-01, 77-03, 77-04]

tech-stack:
  added: []
  patterns:
    - "Duplicated desktop/mobile stylesheet pair (events-overview.css / .mobile.css), matching the existing calendar.css / calendar.mobile.css split"
    - "Non-colour signal (dashed border) layered onto a Bootstrap subtle/emphasis colour pair for the muted-Yes chip"

key-files:
  created:
    - QuestBoard.Service/wwwroot/css/events-overview.css
    - QuestBoard.Service/wwwroot/css/events-overview.mobile.css
  modified:
    - QuestBoard.Service/Views/Shared/_Layout.cshtml
    - QuestBoard.Service/Views/Shared/_Layout.Mobile.cshtml
    - QuestBoard.Service/Views/Calendar/Index.cshtml
    - QuestBoard.Service/Views/Calendar/Index.Mobile.cshtml
    - QuestBoard.IntegrationTests/Controllers/LayoutNavigationTests.cs

key-decisions:
  - "Removed the hardcoded #6f42c1 purple hex from the mobile .avail-card border-left (the analog .agenda-event-entry uses it) and replaced it with a neutral translucent border, because the plan explicitly reserves the accent purple for icons only and forbids hardcoding it elsewhere in these two stylesheets."

patterns-established:
  - "Sticky-column background overrides at the same !important specificity as .modern-card .table th/td, one shade more opaque per role (header vs body tint), so scrolled content never bleeds through a sticky column."

requirements-completed: [EVTVIEW-01, EVTVIEW-02]

coverage:
  - id: D1
    description: "events-overview.css and events-overview.mobile.css carry the five-state cell vocabulary (dashed-border muted-Yes, opacity-only empty cell), the two-line count block, and desktop grid / mobile card layout rules"
    requirement: EVTVIEW-02
    verification:
      - kind: other
        ref: "grep verification per plan acceptance criteria (dashed border, avail-count-headline, sticky-col selectors, no re-themed vote colors, no hardcoded #6f42c1, no planning references)"
        status: pass
    human_judgment: false
  - id: D2
    description: "Desktop Calendar nav entry becomes a dropdown (Calendar + Availability Overview), toggle text unchanged; mobile gets a second flat sibling entry with no dropdown behaviour introduced"
    requirement: EVTVIEW-01
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/LayoutNavigationTests.cs#Nav_CampaignDm_AvailabilityOverviewLinkPresent, Nav_CampaignPlayer_AvailabilityOverviewLinkPresent, Nav_OneShotPlayer_AvailabilityOverviewLinkPresent, Nav_CampaignAnonymous_AvailabilityOverviewLinkAbsent"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/LayoutNavigationTests.cs pre-existing Calendar-link theories (all 4) still pass unmodified"
        status: pass
    human_judgment: false
  - id: D3
    description: "Both calendar views (desktop and mobile) link across to the Availability Overview page"
    requirement: EVTVIEW-01
    verification:
      - kind: other
        ref: "grep verification: Calendar/Index.cshtml and Index.Mobile.cshtml both contain asp-controller=\"Events\" asp-action=\"Index\" and 'Availability Overview'"
        status: pass
    human_judgment: false
  - id: D4
    description: "Manual verification that the mobile offcanvas renders two flat entries (Calendar, Availability Overview) with no dropdown interaction, and the calendar cross-link renders correctly on a real mobile user agent"
    verification: []
    human_judgment: true
    rationale: "Mobile views in this app are user-agent selected, not breakpoint-driven — devtools emulation will not exercise them, and this executor has no real mobile device/browser available to drive a live check."

duration: 25min
completed: 2026-08-29
status: complete
---

# Phase 77 Plan 02: Overview stylesheets and navigation entry points Summary

**Two new stylesheets carrying the five-state cell vocabulary (dashed-border muted-Yes, badge-free empty cell) plus every entry point into the Availability Overview page: desktop nav dropdown, mobile flat sibling, and cross-links from both calendar views.**

## Performance

- **Duration:** 25 min
- **Started:** 2026-08-29T00:00:00Z (approx.)
- **Completed:** 2026-08-29T00:25:00Z (approx.)
- **Tasks:** 3
- **Files modified:** 7 (2 created, 5 modified)

## Accomplishments
- `events-overview.css` / `events-overview.mobile.css` — the muted-Yes chip carries a dashed border alongside its subtle fill (surviving greyscale), the empty cell is deliberately not a badge, and the sticky desktop grid columns are opaque at the correct specificity relative to `.modern-card .table th/td`.
- Desktop `_Layout.cshtml` Calendar nav entry is now a dropdown holding Calendar and Availability Overview, with the toggle's visible text unchanged (`Calendar`) so all four pre-existing nav tests stayed green with zero edits.
- Mobile `_Layout.Mobile.cshtml` gained a second flat sibling `<li>` beside Calendar — no dropdown markup introduced anywhere in that file (verified `grep -c 'dropdown'` == 0).
- Both `Calendar/Index.cshtml` and `Index.Mobile.cshtml` now link across to the overview.
- Four new `LayoutNavigationTests` theories (8 test cases) prove the new nav entry is present for DM and player on both board types and both user agents, and absent for an anonymous visitor — all pre-existing Calendar cases remain green and unmodified.

## Task Commits

Each task was committed atomically:

1. **Task 1: The two overview stylesheets — cell states, count block, grid and card** - `627cb8dd` (feat)
2. **Task 2: Desktop navigation menu, mobile flat sibling entry, desktop stylesheet link** - `efe59f09` (feat)
3. **Task 3: Calendar cross-links and navigation test cases** - `1e54808f` (feat)

## Files Created/Modified
- `QuestBoard.Service/wwwroot/css/events-overview.css` - Desktop cell vocabulary, count block, sticky grid layout
- `QuestBoard.Service/wwwroot/css/events-overview.mobile.css` - Mobile-duplicated cell vocabulary/count rules plus card and expand-toggle styles
- `QuestBoard.Service/Views/Shared/_Layout.cshtml` - Calendar nav entry converted to dropdown; new stylesheet linked
- `QuestBoard.Service/Views/Shared/_Layout.Mobile.cshtml` - New flat Availability Overview sibling entry
- `QuestBoard.Service/Views/Calendar/Index.cshtml` - Cross-link button beside month-navigation controls
- `QuestBoard.Service/Views/Calendar/Index.Mobile.cshtml` - Cross-link button below month-nav row, full-width
- `QuestBoard.IntegrationTests/Controllers/LayoutNavigationTests.cs` - 4 new theory tests (8 cases) for the new nav entry

## Decisions Made
- Removed the hardcoded `#6f42c1` hex from the mobile `.avail-card` border (the closest analog, `.agenda-event-entry`, uses that hex for its accent border) and replaced it with a neutral `rgba(255, 255, 255, 0.15)` border, because the plan's action block explicitly reserves the accent purple for icons only via the existing `.text-purple` class and forbids hardcoding the hex anywhere in these two files. This was caught by the plan's own acceptance criterion (`grep -c '#6f42c1'` must output `0` in both files) during self-verification.

## Deviations from Plan

None beyond the one self-caught adjustment documented above under Decisions Made (which is itself a plan-conformance fix, not a scope change) — plan executed exactly as written otherwise.

## Issues Encountered

None. `EventsController.Index` (the controller action the new nav links and cross-links point to) does not yet exist in this worktree — it is listed in the plan's frontmatter as "Created by sibling plans" (77-01/77-03/77-04). The `asp-controller`/`asp-action` tag helpers do not throw at request time when the target route is absent; `dotnet build` for the whole solution and the full `dotnet test` run (385 unit + 536 integration tests) both passed clean in this worktree regardless. The orchestrator's merge of this wave's sibling plans will complete the route before the overview page is reachable end-to-end.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- The presentation vocabulary and every entry point into the Availability Overview page are in place and merge-ready.
- Sibling plans (77-01/77-03/77-04) still need to land `EventsController.Index`, the aggregating query, and the two Razor views (`Events/Index.cshtml` / `Index.Mobile.cshtml`) that consume the classes and markup shipped here.
- Manual mobile verification (real user agent, not devtools emulation) of the offcanvas two-entry layout and the calendar cross-link is still open — flagged as `human_judgment: true` in the coverage block above.

---
*Phase: 77-availability-overview-page*
*Completed: 2026-08-29*
