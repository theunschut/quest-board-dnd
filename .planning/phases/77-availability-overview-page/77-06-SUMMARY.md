---
phase: 77-availability-overview-page
plan: 06
subsystem: ui
tags: [css, specificity, custom-properties, availability-overview]

# Dependency graph
requires:
  - phase: 77-availability-overview-page
    provides: the desktop availability grid stylesheet (events-overview.css) and the two sticky-column CSS rules whose !important/specificity shape this plan reuses
provides:
  - a working own-column highlight on the desktop availability grid (header and body cells)
  - a first frozen column with a determinate width, so the second frozen column's offset can no longer be undercut by a long event title
affects: []

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "CSS custom property as single source of truth for two coupled layout values (column width + sibling offset)"
    - "Matching selector specificity/!important shape of a shared base stylesheet when a feature-specific rule must win against it"

key-files:
  created: []
  modified:
    - QuestBoard.Service/wwwroot/css/events-overview.css

key-decisions:
  - "Rewrote the bare .avail-col-self rule as two rules (th.avail-col-self, td.avail-col-self) each qualified with .modern-card .table and !important, matching the existing .avail-sticky-col workaround shape exactly, so the row-hover rule (which has more type selectors) stays the more specific one."
  - "Introduced --avail-event-col-width as a custom property on .avail-grid so the event column's width/min-width/max-width and the attendance column's left offset all read the same value, instead of two independent literals that could drift."

requirements-completed: [EVTVIEW-01]

coverage:
  - id: D1
    description: "The viewer's own column (header and body cells) now paints a visible tint that differs from every other column on the desktop availability grid, instead of being silently overridden by modern-card.css's !important cell backgrounds."
    requirement: "EVTVIEW-01"
    verification:
      - kind: unit
        ref: "grep -c 'modern-card .table th\\.avail-col-self' QuestBoard.Service/wwwroot/css/events-overview.css == 1"
        status: pass
      - kind: unit
        ref: "grep -c 'modern-card .table td\\.avail-col-self' QuestBoard.Service/wwwroot/css/events-overview.css == 1"
        status: pass
      - kind: unit
        ref: "grep -c 'rgba(139, 69, 19, 0.65) !important' QuestBoard.Service/wwwroot/css/events-overview.css == 1"
        status: pass
      - kind: unit
        ref: "grep -c 'rgba(255, 235, 180, 0.95) !important' QuestBoard.Service/wwwroot/css/events-overview.css == 1"
        status: pass
      - kind: integration
        ref: "dotnet test QuestBoard.IntegrationTests --filter FullyQualifiedName~EventsOverview (18 passed, 0 failed)"
        status: pass
    human_judgment: true
    rationale: "CSS specificity/!important precedence is verified structurally by the grep checks and confirmed by a passing build, but whether the tint actually renders as visibly distinct in a browser (against the sticky-column tint and the row-hover tint) is a rendering fact the plan itself defers to a human spot-check in its <verification> section."
  - id: D2
    description: "The first frozen column (event title) has a determinate width (200px) that no longer grows past its declared size on a long title, and the second frozen column's (attendance) left offset reads that same width from one custom property, so the two columns cannot overlap on horizontal scroll."
    requirement: "EVTVIEW-01"
    verification:
      - kind: unit
        ref: "grep -c 'avail-event-col-width' QuestBoard.Service/wwwroot/css/events-overview.css == 5"
        status: pass
      - kind: unit
        ref: "grep -c 'left: 200px' QuestBoard.Service/wwwroot/css/events-overview.css == 0"
        status: pass
      - kind: unit
        ref: "grep -c 'overflow-wrap: anywhere' QuestBoard.Service/wwwroot/css/events-overview.css == 1"
        status: pass
      - kind: unit
        ref: "grep -c 'min-width: 170px' QuestBoard.Service/wwwroot/css/events-overview.css == 1"
        status: pass
      - kind: integration
        ref: "dotnet test QuestBoard.IntegrationTests --filter FullyQualifiedName~EventsOverview (18 passed, 0 failed)"
        status: pass
    human_judgment: true
    rationale: "The grep checks structurally prove both columns read the same custom property, and the mutation-must-break-intent criterion in the plan (changing the property value must move both together) follows directly from the CSS cascade, but confirming the two frozen columns visually stay adjacent on a long-titled event after a sideways scroll is a rendering fact the plan explicitly defers to a human spot-check."

# Metrics
duration: 22min
completed: 2026-08-29
status: complete
---

# Phase 77 Plan 06: Desktop Grid Stylesheet Gap Closure Summary

**Fixed two dead-on-arrival desktop grid affordances by matching the shared card stylesheet's `!important` specificity for the own-column highlight, and by deriving the second frozen column's offset from a single `--avail-event-col-width` custom property instead of a duplicated literal.**

## Performance

- **Duration:** 22 min
- **Started:** 2026-08-29T10:22:00Z
- **Completed:** 2026-08-29T10:44:27Z
- **Tasks:** 2
- **Files modified:** 1

## Accomplishments
- The viewer's own-column highlight (header + body cells) now paints, because both rules are written at the same `.modern-card .table th/td` + `!important` shape the base stylesheet forces — matching the pattern the same stylesheet already used for the sticky columns.
- Row hover still tints the whole row, own column included, because the hover rule's extra type selectors (`tbody`, `tr:hover`) keep it more specific than the two new own-column rules — no change was needed there, just verified it stays true.
- The first frozen column (event title) now has a real fixed width via `width`/`min-width`/`max-width` all reading `--avail-event-col-width`, plus `overflow-wrap: anywhere` so a long title wraps inside the column instead of forcing it wider.
- The second frozen column's (attendance) `left` offset reads the same custom property, so the two frozen columns can no longer drift apart on a long event title.

## Task Commits

Each task was committed atomically:

1. **Task 1: Make the viewer's own-column highlight win against the shared card table rules** - `37d3a264` (fix)
2. **Task 2: Drive the second frozen column's offset from one declared width** - `a89053a9` (fix)

**Plan metadata:** (this commit, following SUMMARY.md creation)

## Files Created/Modified
- `QuestBoard.Service/wwwroot/css/events-overview.css` - Replaced the dead bare `.avail-col-self` rule with two `!important`-qualified rules at the specificity the shared card table forces; introduced `--avail-event-col-width` as the single source for the first frozen column's width and the second frozen column's offset.

## Decisions Made
- Matched the existing `.avail-sticky-col` workaround's exact selector shape (`.modern-card .table th/td.<class>` + `!important`) for the own-column rules, rather than inventing a new specificity strategy, since the codebase already documents and solves this exact collision one block above.
- Used a CSS custom property scoped to `.avail-grid` rather than a Razor-emitted inline style or a SCSS variable, since the project has no CSS preprocessor and a custom property is the plain-CSS mechanism for "one declared value, two consumers."

## Deviations from Plan

None — plan executed exactly as written. Both CSS edits match the plan's `<action>` and `<artifacts>` specifications verbatim, and no markup, view model, controller, or DI changes were made.

One documentation inconsistency was noted, not a deviation: Task 2's acceptance criteria stated `dotnet test ... --filter "FullyQualifiedName~EventsOverviewControllerIntegrationTests"` should show "18 passed" — that narrower filter actually matches 13 tests (all passing). The plan's own top-level `<verification>` section uses the broader filter `FullyQualifiedName~EventsOverview`, which does produce 18 passed, 0 failed, and that is the filter used to confirm this plan's success criteria.

## Issues Encountered

None. Both stylesheet changes built cleanly on the first attempt and all grep-based acceptance criteria matched their expected counts exactly.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- The desktop availability grid's own-column highlight and frozen-column alignment are both now functionally correct per the code review's WR-02 and WR-03 findings.
- The plan's `<verification>` section recommends a human spot-check (board with a long event title and more members than fit the viewport, scrolled sideways) — recorded here for `/gsd-verify-work`, not gating this plan's completion.
- Remaining review findings (CR-01 mobile paging, WR-01 dead "Show More" link, WR-04 through WR-09, IN-01 through IN-08) are out of scope for this plan and are tracked separately in `77-REVIEW.md`.

---
*Phase: 77-availability-overview-page*
*Completed: 2026-08-29*

## Self-Check: PASSED

- FOUND: QuestBoard.Service/wwwroot/css/events-overview.css
- FOUND: commit 37d3a264 (Task 1)
- FOUND: commit a89053a9 (Task 2)
