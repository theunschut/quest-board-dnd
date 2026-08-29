---
phase: 77-availability-overview-page
plan: 11
subsystem: ui
tags: [css, razor, wcag-aa, mobile, bootstrap]

# Dependency graph
requires:
  - phase: 77-availability-overview-page
    provides: Index.Mobile.cshtml and events-overview.mobile.css shipped by the phase's earlier waves; the 77-UAT.md gap report that identified the visual defects
provides:
  - Mobile availability card rendered on the app's standard translucent glass surface (matches .modern-card byte for byte)
  - Count block, meta line and roster names on the mobile card readable at a measured 5.07:1 worst-case contrast, clearing the 4.5:1 WCAG AA floor
  - Legend disclosure and per-card roster toggle rendered as filled btn-secondary controls instead of outline
affects: [77-availability-overview-page follow-on plans, any future UAT audit of the mobile overview]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Glass surface declarations written directly into a component class (.avail-card) rather than added via the shared .modern-card class, when the shared class would cascade an !important color rule onto nested badge/span elements that must stay untouched"
    - "Scoped, high-specificity color overrides (.avail-card .avail-x-y { color: ... !important }) used only where a shared partial's Bootstrap .text-muted must be beaten on a page-specific dark surface"

key-files:
  created: []
  modified:
    - QuestBoard.Service/wwwroot/css/events-overview.mobile.css
    - QuestBoard.Service/Views/Events/Index.Mobile.cshtml

key-decisions:
  - "Wrote the five .modern-card glass declarations directly into .avail-card instead of adding the modern-card class to the card element, because modern-card.css's `.modern-card span { color: #F4E4BC !important }` would have repainted the nested vote-chip labels, which were explicitly out of scope and already approved in UAT."
  - "Used #FFFFFF (5.07:1 worst-case contrast) for the count block, meta line and roster names instead of the app's standard cream #F4E4BC (4.02:1), because cream fails the 4.5:1 normal-text floor at the brightest point of the background image; cream was kept only on the 20px/600 card title where the 3.0:1 large-text floor applies."

requirements-completed: [EVTVIEW-01, EVTVIEW-03]

coverage:
  - id: D1
    description: "Mobile availability card renders on the app's translucent glass surface (same five declarations as .modern-card), matching the legend card on the same page"
    requirement: "EVTVIEW-01"
    verification:
      - kind: unit
        ref: "grep-based structural checks — no #343a40/#495057 in events-overview.mobile.css; background/backdrop-filter/border declarations present and byte-identical to modern-card.css"
        status: pass
    human_judgment: false
  - id: D2
    description: "Count block, meta line and roster names on the mobile card clear the WCAG AA 4.5:1 normal-text contrast floor against the notice-board backdrop"
    requirement: "EVTVIEW-03"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests --filter FullyQualifiedName~EventsOverview (22 facts, including Index_MobileUserAgent_* facts)"
        status: pass
    human_judgment: true
    rationale: "Contrast ratios were computed by hand from the plan's measured backdrop luminance data, not re-derived by an automated contrast checker in this run; a human/visual pass on a real device is the strongest final confirmation for a rendered color/contrast claim."
  - id: D3
    description: "Legend disclosure button and per-card roster toggle render as filled btn-secondary controls instead of outline, per project UI guidelines"
    requirement: "EVTVIEW-01"
    verification:
      - kind: unit
        ref: "grep-based checks — zero btn-outline- occurrences, two btn btn-sm btn-secondary occurrences in Index.Mobile.cshtml"
      - kind: integration
        ref: "QuestBoard.IntegrationTests --filter FullyQualifiedName~EventsOverview (22 facts)"
        status: pass
    human_judgment: false

duration: 25min
completed: 2026-08-29
status: complete
---

# Phase 77 Plan 11: Mobile Availability Card Glass Surface & Contrast Fix Summary

**Rewrote `.avail-card` to use the app's standard glass surface (matching `.modern-card` byte for byte), gave every normal-size text run inside the card an explicit `#FFFFFF` color measured at 5.07:1 worst-case contrast, and converted the two outline buttons on the mobile overview to filled `btn-secondary`.**

## Performance

- **Duration:** 25 min
- **Started:** 2026-08-29T21:21:00Z (approx.)
- **Completed:** 2026-08-29T21:46:18Z
- **Tasks:** 3
- **Files modified:** 2

## Accomplishments
- Replaced the opaque `#343a40` slab background and `border-left` accent on `.avail-card` with the five glass declarations `.modern-card` uses (`background: rgba(255,255,255,0.15)`, `backdrop-filter: blur(15px)`, `border: 1px solid rgba(255,255,255,0.3)`, `border-radius: 12px`, `box-shadow: 0 8px 32px rgba(0,0,0,0.2)`), and updated `.avail-card:active` to the lifted-glass pressed value `rgba(255,255,255,0.25)`.
- Added `.avail-card-meta` and `.avail-roster-name` class hooks in the view and four new scoped CSS rules that give the count headline, count detail, meta line and roster name text an explicit `#FFFFFF` color with a dark text-shadow halo — replacing the inherited dark text that measured 1.34:1 against the dark card background.
- Converted the legend disclosure button and the per-card "Show players" toggle from `btn-outline-secondary` to `btn-secondary`, matching the precedent already established for the calendar cross-links.

## Task Commits

Each task was committed atomically:

1. **Task 1: Put the mobile availability card on the glass surface** - `bc249924` (fix)
2. **Task 2: Make every normal-size run of card text clear the 4.5:1 AA floor** - `24fc518e` (fix)
3. **Task 3: Replace the two outline controls with filled buttons** - `4219d1f6` (fix)

_Note: no plan metadata commit is included in this list — the orchestrator owns the final docs commit for this plan._

## Files Created/Modified
- `QuestBoard.Service/wwwroot/css/events-overview.mobile.css` - `.avail-card`/`.avail-card:active` rewritten to the glass surface; four new scoped color rules for count headline, count detail, card meta and roster name text
- `QuestBoard.Service/Views/Events/Index.Mobile.cshtml` - added `avail-card-meta` and `avail-roster-name` class hooks; two buttons changed from `btn-outline-secondary` to `btn-secondary`

## Decisions Made
- Glass declarations written directly into `.avail-card` rather than via the `modern-card` class, to avoid cascading `.modern-card span { color: #F4E4BC !important }` onto the nested vote-chip badges (out of scope, already UAT-approved). See `key-decisions` in frontmatter for full rationale.
- `#FFFFFF` chosen over the app's standard cream `#F4E4BC` for all normal-size text runs on the card, because cream measures only 4.02:1 at the worst-case backdrop pixel (below the 4.5:1 normal-text floor) while white reaches 5.07:1. Cream was kept only on the 20px/600 card title, which is WCAG large text (3.0:1 floor) and clears it at 4.02:1.

## Deviations from Plan

None - plan executed exactly as written. All acceptance-criteria greps matched the plan's expected counts on first attempt; no auto-fixes were required.

## Issues Encountered
None.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- The mobile availability overview now matches the visual language of the rest of the app and passes the WCAG AA normal-text contrast floor on every text run inside the card, closing the critical gap recorded in `77-UAT.md` test 1.
- A regression guard covering this styling contract (the phase's earlier gap report noted the button-convention test covers only the calendar views, and the UI safety gate did not enforce the UI-SPEC against this implementation) is explicitly deferred to plan 77-12, per the plan's own artifacts note. No blockers for that follow-on plan.
- Full solution build succeeded and the full test suite passed (422 unit + 612 integration = 1034 total, 0 failures) before the final task commit.

---
*Phase: 77-availability-overview-page*
*Completed: 2026-08-29*
