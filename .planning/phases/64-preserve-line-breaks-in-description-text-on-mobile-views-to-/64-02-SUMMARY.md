---
phase: 64-preserve-line-breaks-in-description-text-on-mobile-views-to-
plan: 02
subsystem: ui
tags: [css, razor, cshtml, mobile, rendering]

# Dependency graph
requires: []
provides:
  - Mobile Characters/Details Description and Backstory render typed line breaks (white-space pre-wrap on .character-info-value)
  - Shop item Description renders typed line breaks on both desktop and mobile
affects: [Characters, Shop]

# Tech tracking
tech-stack:
  added: []
  patterns: []

key-files:
  created: []
  modified:
    - QuestBoard.Service/wwwroot/css/character-detail.mobile.css
    - QuestBoard.Service/Views/Shared/_ShopItemDetailsContent.cshtml
    - QuestBoard.Service/Views/Shop/Details.Mobile.cshtml

key-decisions:
  - "Added a standalone .character-info-value { white-space: pre-wrap; } rule rather than adding it to the shared grouped selector, since that selector also covers single-line labels/text-muted content"
  - "Used inline style=\"white-space: pre-wrap;\" on the two shop Description paragraphs rather than extending the shared .parchment-text-muted class, since that class is used by other single-line text elements"

patterns-established: []

requirements-completed: []

# Metrics
duration: 8min
completed: 2026-07-07
status: complete
---

# Phase 64 Plan 2: Fix mobile Character and Shop Description line breaks Summary

**Added `white-space: pre-wrap` to mobile Character Description/Backstory (CSS class) and Shop item Description on both platforms (inline style), closing the two remaining instances of the missing-line-break bug from this phase's investigation.**

## Performance

- **Duration:** 8 min
- **Started:** 2026-07-07T21:05:00Z
- **Completed:** 2026-07-07T21:13:25Z
- **Tasks:** 2
- **Files modified:** 3

## Accomplishments
- Mobile Characters/Details Description and Backstory fields now preserve typed newlines, matching desktop's existing behavior
- Shop item Description now preserves typed newlines on both desktop (shared partial, covers modal and full desktop view) and mobile shop views

## Task Commits

Each task was committed atomically:

1. **Task 1: Add pre-wrap to .character-info-value (D-01 — mobile Characters/Details)** - `b840dbc` (fix)
2. **Task 2: Add inline pre-wrap to Shop item Description on desktop and mobile (D-03)** - `2dc88aa` (fix)

## Files Created/Modified
- `QuestBoard.Service/wwwroot/css/character-detail.mobile.css` - Added standalone `.character-info-value { white-space: pre-wrap; }` rule after the pre-existing grouped muted-text rule
- `QuestBoard.Service/Views/Shared/_ShopItemDetailsContent.cshtml` - Added inline `style="white-space: pre-wrap;"` to the desktop shop Description paragraph
- `QuestBoard.Service/Views/Shop/Details.Mobile.cshtml` - Added inline `style="white-space: pre-wrap;"` to the mobile shop Description paragraph

## Decisions Made
- Followed the plan's specified mechanism exactly: dedicated CSS class for the mobile character view (matching the mobile convention of dedicated classes), inline style for the shop paragraphs (matching the local convention in those files, since the shop paragraphs use no dedicated per-field class and `.parchment-text-muted` is a shared utility that must not be globally altered).

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

None.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

Both remaining instances of the missing-line-break bug from this phase's scope (D-01 and D-03) are closed. `dotnet build` succeeds with 0 warnings/0 errors, confirming the Razor inline-style edits are syntactically valid. All plan-level verification checks pass:
- `grep -c 'white-space: pre-wrap' character-detail.mobile.css` = 1 (standalone rule, grouped rule untouched)
- `grep -c 'white-space: pre-wrap' _ShopItemDetailsContent.cshtml` = 1
- `grep -c 'white-space: pre-wrap' Shop/Details.Mobile.cshtml` = 1
- `shop-details.mobile.css` unchanged (no new `white-space` declaration on `.parchment-text-muted`)
- No GSD phase/plan/requirement IDs in any modified source file

No blockers for the remaining plans in this phase (D-02 QuestLog desktop and D-04 quest card preview, if scoped to a separate plan).

---
*Phase: 64-preserve-line-breaks-in-description-text-on-mobile-views-to-*
*Completed: 2026-07-07*
