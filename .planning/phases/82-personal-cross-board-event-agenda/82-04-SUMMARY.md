---
phase: 82-personal-cross-board-event-agenda
plan: 04
subsystem: ui
tags: [razor, mobile-view, bootstrap-collapse, availability-chips]

requires:
  - phase: 82-personal-cross-board-event-agenda
    provides: 82-03 (AgendaController, view models, desktop Index.cshtml, AgendaControllerIntegrationTests)
provides:
  - Views/Agenda/Index.Mobile.cshtml -- a real user-agent-selected mobile agenda view
  - wwwroot/css/agenda.mobile.css -- duplicated availability chip rules plus card/toggle styles
  - AgendaMobileRenderTests -- 9 facts proving the mobile view under a real mobile User-Agent
affects: [82-05 (nav entries and cross-links, running concurrently on shared layout files)]

tech-stack:
  added: []
  patterns:
    - "Roster disclosure carries onclick=\"event.stopPropagation();\" on both the toggle button
       and the .collapse container itself, not the toggle alone -- copied from the availability
       overview's own gap-closure fix rather than repeating the toggle-only regression"
    - "Mobile page stylesheet duplicates the desktop availability cell-state rules verbatim,
       because _Layout.Mobile.cshtml renders no page-level Styles link beyond each view's own
       @section Styles block"

key-files:
  created:
    - QuestBoard.Service/Views/Agenda/Index.Mobile.cshtml
    - QuestBoard.Service/wwwroot/css/agenda.mobile.css
    - QuestBoard.IntegrationTests/Tests/AgendaMobileRenderTests.cs
  modified: []

key-decisions:
  - "The mobile card mirrors the desktop row's action contract exactly (asp-route-from=\"agenda\"
     on the details link, from=agenda on the switch-confirm modal's return url), so the D-13
     back-link on Events/Details fires identically regardless of which surface the reader used"
  - "Board identity (name + type badge + Active badge) is stacked in a single text-end column
     rather than placed inline, since the mobile card is narrower than the desktop row and the
     UI-SPEC calls for these to read as a stacked, small-text block on mobile"
  - "The filter panel is a Bootstrap collapse triggered by a plain button, never a dropdown --
     _Layout.Mobile.cshtml has zero dropdowns anywhere and this page does not introduce the
     first one for a page-level control"

patterns-established: []

requirements-completed:
  - EVTAGENDA-02
  - EVTAGENDA-03
  - EVTAGENDA-04
  - EVTAGENDA-06

coverage:
  - id: D1
    description: "A phone renders the agenda as cards, with each event's roster behind its own disclosure control rather than always expanded"
    requirement: "EVTAGENDA-02"
    verification:
      - kind: integration
        ref: "AgendaMobileRenderTests.Agenda_MobileUserAgent_RendersCardLayout_NotDesktopList"
        status: pass
      - kind: integration
        ref: "AgendaMobileRenderTests.Agenda_MobileRoster_CollapsedByDefaultAndContainsMemberNames"
        status: pass
    human_judgment: false
  - id: D2
    description: "The roster disclosure and the row's action control are two separate, non-overlapping tap targets, and a tap inside an expanded roster never navigates"
    requirement: "EVTAGENDA-04"
    verification:
      - kind: integration
        ref: "AgendaMobileRenderTests.Agenda_MobileRowOnActiveBoard_RendersDetailsLink"
        status: pass
      - kind: integration
        ref: "AgendaMobileRenderTests.Agenda_MobileRowOnOtherBoard_RendersSwitchModalTriggerWithGroupIdAndReturnUrl"
        status: pass
    human_judgment: true
    rationale: "The stopPropagation guard and tap-target spacing are structural (proven by the grep-based acceptance criteria and the CSS min-height rule), but whether a real thumb can hit either control unambiguously on an actual phone is a physical-ergonomics judgment automation cannot render a verdict on."
  - id: D3
    description: "The mobile page renders the five-state availability chips correctly even though the mobile layout does not load the desktop availability stylesheet"
    requirement: "EVTAGENDA-02"
    verification:
      - kind: integration
        ref: "AgendaMobileRenderTests.Agenda_MobileUserAgent_RendersCardLayout_NotDesktopList"
        status: pass
    human_judgment: true
    rationale: "The integration suite proves the chip markup and the duplicated CSS rules exist and are linked via @section Styles, but only a real mobile browser render can confirm the chips are visually styled rather than just present in the DOM."
  - id: D4
    description: "The board filter is reachable on mobile without introducing a dropdown into a layout that has none"
    requirement: "EVTAGENDA-03"
    verification:
      - kind: integration
        ref: "AgendaMobileRenderTests.Agenda_MobileNoBoardsEmptyState_RendersOwnCopy"
        status: pass
      - kind: integration
        ref: "AgendaMobileRenderTests.Agenda_MobileNoUpcomingEventsEmptyState_RendersOwnCopy"
        status: pass
      - kind: integration
        ref: "AgendaMobileRenderTests.Agenda_MobileAllBoardsFilteredEmptyState_RendersOwnCopyWithResetControl"
        status: pass
    human_judgment: false

duration: ~35min
completed: 2026-08-29
status: complete
---

# Phase 82 Plan 04: Mobile Agenda View and Stylesheet Summary

**Real user-agent-selected `Index.Mobile.cshtml` rendering one card per event, with the roster behind a two-guard disclosure control and the row's action control kept physically apart below a divider.**

## Performance

- **Duration:** ~35 min
- **Completed:** 2026-08-29
- **Tasks:** 2
- **Files modified:** 3 (3 created, 0 modified)

## Accomplishments

- `Views/Agenda/Index.Mobile.cshtml` -- mirrors the desktop `Index.cshtml` (from `82-03`) row-for-row: same view model, same empty states, same switch-confirm modal and its `show.bs.modal` listener, same filter-preserving paging -- but as a card layout with a Bootstrap-collapse filter panel (no dropdown) and a per-card roster disclosure carrying `onclick="event.stopPropagation();"` on both the toggle button and the `.collapse` container, matching the availability overview's own gap-closure fix rather than the toggle-only shape that regressed there once already.
- `wwwroot/css/agenda.mobile.css` -- duplicates the `avail-cell-yes-muted`, `avail-cell-yes-muted em` and `avail-cell-empty` rules verbatim from `events-overview.mobile.css` (the mobile layout links no page stylesheet of its own beyond each view's `@section Styles`), plus `.agenda-card`, `.agenda-card-title` and `.agenda-roster-toggle` with a `min-height: 44px` tap-target floor. No class name collides with the unrelated `agenda-*` family already in `calendar.mobile.css`.
- `AgendaMobileRenderTests` -- 9 facts, all driven through a real mobile `User-Agent` header (never viewport emulation): mobile-vs-desktop layout distinction in both directions, the collapsed roster carrying seeded member names, both row-action variants (direct link on the active board, modal trigger with `data-group-id`/return-url on any other board), board identity (name + type badge), and all three empty states with their own copy.

## Task Commits

Each task was committed atomically:

1. **Task 1: Mobile agenda view and its stylesheet** - `c38590b3` (feat)
2. **Task 2: Mobile render tests under a real mobile user agent** - `642f8226` (test)

_No plan-metadata commit in worktree mode -- STATE.md/ROADMAP.md are owned by the orchestrator._

## Files Created/Modified

- `QuestBoard.Service/Views/Agenda/Index.Mobile.cshtml` - the mobile card layout
- `QuestBoard.Service/wwwroot/css/agenda.mobile.css` - duplicated chip rules plus card/toggle styles
- `QuestBoard.IntegrationTests/Tests/AgendaMobileRenderTests.cs` - the 9-fact mobile render suite

## Decisions Made

- The mobile action control uses the exact same `asp-route-from="agenda"` / `from=agenda` return-url contract as the desktop view (verified against the real `82-03` desktop `Index.cshtml`, which already carries this contract), so the `Events/Details` back-link behaves identically regardless of which surface the reader arrived from.
- Board identity is rendered as a single stacked `text-end small` block (name, then badges) rather than inline, since the UI-SPEC calls for a "stacked small" presentation on the narrower mobile card.
- The board filter is a `.collapse` panel behind a plain button, never a dropdown, consistent with `_Layout.Mobile.cshtml` having zero dropdowns anywhere in the app.

## Deviations from Plan

None - plan executed exactly as written. The two "inherited fixes" called out in the plan's objective (the two-guard `stopPropagation()` shape, and the duplicated chip CSS) were built in from the start rather than discovered as regressions, since the plan's `<action>` block specified them explicitly and the current `Views/Events/Index.Mobile.cshtml:76-93` was re-verified at execution time to still carry the two-guard shape before copying it.

## Issues Encountered

None. The `rtk`-proxied shell `grep -c` was not used for any acceptance-criteria count in this plan -- every count was verified with the sandboxed `Grep` tool per the project's known CRLF-counting gap.

## Known Stubs

None -- every card renders real controller-supplied data (`AgendaRowViewModel`/`AgendaRosterEntryViewModel`), and the empty states and paging control are the same server-computed values the desktop view already uses.

## Next Phase Readiness

The mobile agenda surface is fully working and covered by its own integration suite (9 facts, all passing alongside the existing 27 Agenda-suite facts and the full 580-fact integration run). Plan `82-05` (nav entries and cross-links) can proceed without any further change to this view or its controller -- this plan touched no shared layout file, staying entirely within `Views/Agenda/Index.Mobile.cshtml`, `wwwroot/css/agenda.mobile.css` and the new test file.

## Self-Check: PASSED

- FOUND: QuestBoard.Service/Views/Agenda/Index.Mobile.cshtml
- FOUND: QuestBoard.Service/wwwroot/css/agenda.mobile.css
- FOUND: QuestBoard.IntegrationTests/Tests/AgendaMobileRenderTests.cs
- FOUND: c38590b3 (Task 1 commit)
- FOUND: 642f8226 (Task 2 commit)

---
*Phase: 82-personal-cross-board-event-agenda*
*Completed: 2026-08-29*
