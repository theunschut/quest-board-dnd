---
phase: 36-campaign-quest-posting-closing
plan: 05
subsystem: ui
tags: [razor, mvc, bootstrap, quest-log]

# Dependency graph
requires:
  - phase: 36-campaign-quest-posting-closing (plan 03)
    provides: Close/Reopen controller actions, ViewBag.BoardType threading, closed-campaign-quest Quest Log guard fixes
provides:
  - Quest Log Index/Details campaign simplification (no CR badge, no Adventurers count, ClosedDate-driven completed date)
affects: []

# Tech tracking
tech-stack:
  added: []
  patterns: ["Conditional Razor rendering keyed on ViewBag.BoardType, no new CSS/views"]

key-files:
  created: []
  modified:
    - QuestBoard.Service/Views/QuestLog/Index.cshtml
    - QuestBoard.Service/Views/QuestLog/Index.Mobile.cshtml
    - QuestBoard.Service/Views/QuestLog/Details.cshtml
    - QuestBoard.Service/Views/QuestLog/Details.Mobile.cshtml

key-decisions: []

patterns-established: []

requirements-completed: [CQUEST-05]

# Metrics
duration: 20min
completed: 2026-07-03
---

# Phase 36: Campaign Quest Log Views Summary

**Quest Log Index and Details (desktop + mobile) drop the CR badge and Adventurers count for campaign-closed entries, show the completed date from `ClosedDate` (falling back to `FinalizedDate` for one-shot), and keep the Session Recap flow working unchanged.**

## Performance

- **Duration:** ~20 min (including human-verify checkpoint)
- **Completed:** 2026-07-03
- **Tasks:** 2/2
- **Files modified:** 4

## Accomplishments
- Campaign Quest Log entries show no CR badge and no "Adventurers: N" line
- Completed date sourced from `ClosedDate` for campaign, `FinalizedDate` for one-shot
- Session Recap add/edit confirmed working unchanged for closed campaign quests
- One-shot Quest Log rendering unregressed

## Task Commits

Each task was committed atomically:

1. **Task 1: Quest Log Index + Details campaign simplification (desktop + mobile)** - `3c44ee7` (feat)
2. **Task 2: Human-verify campaign Quest Log entry + recap flow** - checkpoint, approved with no issues

## Files Created/Modified
- `QuestBoard.Service/Views/QuestLog/Index.cshtml` - Campaign entries: no CR/Adventurers, ClosedDate-driven completed date
- `QuestBoard.Service/Views/QuestLog/Index.Mobile.cshtml` - Mobile equivalent
- `QuestBoard.Service/Views/QuestLog/Details.cshtml` - Campaign entries: no CR/selected-players list
- `QuestBoard.Service/Views/QuestLog/Details.Mobile.cshtml` - Mobile equivalent

## Decisions Made
None - followed plan as specified.

## Deviations from Plan
None - plan executed exactly as written. Human-verify checkpoint approved on first pass.

## Issues Encountered
None.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- All 5 plans in Phase 36 complete. Ready for post-wave gates (code review, regression, schema drift, phase verification).

---
*Phase: 36-campaign-quest-posting-closing*
*Completed: 2026-07-03*
