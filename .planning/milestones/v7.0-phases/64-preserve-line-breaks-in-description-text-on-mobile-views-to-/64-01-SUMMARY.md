---
phase: 64-preserve-line-breaks-in-description-text-on-mobile-views-to-
plan: 01
subsystem: ui
tags: [css, razor, quest-log, quest-board]

# Dependency graph
requires: []
provides:
  - "white-space: pre-wrap on .quest-description-box (desktop QuestLog Original Quest Description box, D-02)"
  - "white-space: pre-wrap on .modern-card .card-text (shared quest-board list-card description preview, D-04)"
affects: [64-02, 64-03]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Boxed free-text callouts get white-space: pre-wrap on the CSS class itself, matching the established .recap-display-box convention"

key-files:
  created: []
  modified:
    - QuestBoard.Service/wwwroot/css/quests.css
    - QuestBoard.Service/wwwroot/css/site.css
    - QuestBoard.Service/Views/QuestLog/Details.cshtml

key-decisions:
  - "Extended the existing .quest-description-box and .modern-card .card-text rules in place rather than introducing new selectors, per plan instruction"
  - "Removed the now-redundant inline style=\"white-space: pre-wrap;\" on the Rewards box in QuestLog/Details.cshtml since the class now provides it (optional cleanup from the plan, applied)"

patterns-established: []

requirements-completed: []

# Metrics
duration: 6min
completed: 2026-07-07
status: complete
---

# Phase 64 Plan 1: Desktop QuestLog description box and shared quest-card preview line-break fix Summary

**Added `white-space: pre-wrap` to `.quest-description-box` (quests.css) and `.modern-card .card-text` (site.css), closing the desktop QuestLog "Original Quest Description" line-break bug (D-02) and the shared quest-board list-card preview line-break bug (D-04, fixes both desktop and mobile via one shared CSS rule).**

## Performance

- **Duration:** 6 min
- **Started:** 2026-07-07T21:06:00Z
- **Completed:** 2026-07-07T21:12:49Z
- **Tasks:** 2 completed
- **Files modified:** 3

## Accomplishments
- `.quest-description-box` in `quests.css` now includes `white-space: pre-wrap;`, matching the sibling `.recap-display-box` rule that already had it — the desktop QuestLog "Original Quest Description" box now preserves typed line breaks.
- `.modern-card .card-text` in `site.css` now includes `white-space: pre-wrap;` — the quest-board list-card description preview (shared by desktop and mobile via `Quest/_QuestCard.cshtml`) now preserves typed line breaks on both platforms from a single CSS-class edit.
- Removed the now-redundant inline `style="white-space: pre-wrap;"` on the Rewards box in `QuestLog/Details.cshtml`, since `.quest-description-box` now provides it at the class level.

## Task Commits

Each task was committed atomically:

1. **Task 1: Add pre-wrap to .quest-description-box (D-02 — QuestLog desktop)** - `fcb0398` (fix)
2. **Task 2: Add pre-wrap to .modern-card .card-text (D-04 — shared quest-board card preview)** - `078c301` (fix)

_Note: no plan-metadata commit in this worktree — the orchestrator commits STATE.md/ROADMAP.md centrally after wave merge._

## Files Created/Modified
- `QuestBoard.Service/wwwroot/css/quests.css` - added `white-space: pre-wrap;` to the existing `.quest-description-box` rule
- `QuestBoard.Service/wwwroot/css/site.css` - added `white-space: pre-wrap;` to the existing `.modern-card .card-text` rule
- `QuestBoard.Service/Views/QuestLog/Details.cshtml` - removed redundant inline `style="white-space: pre-wrap;"` on the Rewards box (class now provides it)

## Decisions Made
- Applied the plan's optional cleanup (removing the redundant inline style on the Rewards div) since it makes the two boxes in the same view consistent and the class-level fix supersedes the inline override — no behavior change, pure duplication removal.
- No other decisions — plan executed exactly as written, both CSS edits extended existing rule blocks in place with no new selectors.

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

None.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- Plan 1 of Phase 64 is complete; both D-02 and D-04 are closed.
- `dotnet build` succeeds with 0 warnings / 0 errors after these changes.
- No blockers for the remaining plans in this phase (D-01 Characters mobile, D-03 Shop item description), which are independent CSS/view edits in different files.

---
*Phase: 64-preserve-line-breaks-in-description-text-on-mobile-views-to-*
*Completed: 2026-07-07*

## Self-Check: PASSED

All created/modified files and both task commits verified present on disk / in git history.
