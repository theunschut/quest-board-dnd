---
phase: 36-campaign-quest-posting-closing
plan: 04
subsystem: ui
tags: [razor, mvc, bootstrap, quest-board]

# Dependency graph
requires:
  - phase: 36-campaign-quest-posting-closing (plan 03)
    provides: Close/Reopen controller actions, ViewBag.BoardType threading, relaxed Create validation
provides:
  - Campaign board card rendering (Open/Closed wax seal, no CR badge, no signup line)
  - Campaign Create form stripped to Title + Description
  - Manage/Details Close/Reopen buttons and CR/signup/date-voting removal for campaign
affects: [36-05, quest-log-views]

# Tech tracking
tech-stack:
  added: []
  patterns: ["Conditional Razor rendering keyed on ViewBag.BoardType, no new CSS/views"]

key-files:
  created: []
  modified:
    - QuestBoard.Service/Views/Quest/Index.cshtml
    - QuestBoard.Service/Views/Quest/Index.Mobile.cshtml
    - QuestBoard.Service/Views/Quest/Manage.cshtml
    - QuestBoard.Service/Views/Quest/Manage.Mobile.cshtml
    - QuestBoard.Service/Views/Quest/Details.cshtml
    - QuestBoard.Service/Views/Quest/Details.Mobile.cshtml
    - QuestBoard.Service/Views/Quest/Create.cshtml
    - QuestBoard.Service/Views/Quest/Create.Mobile.cshtml

key-decisions:
  - "Campaign board wax seal always renders with the visible 'finalized-seal' styling rather than being keyed off IsClosed, since closed campaign quests are already excluded from the active board query and the IsClosed branch could never render visibly (found during human-verify checkpoint)."

patterns-established: []

requirements-completed: [CQUEST-01, CQUEST-02, CQUEST-03, CQUEST-04]

# Metrics
duration: 45min
completed: 2026-07-03
---

# Phase 36: Campaign Quest Board/Manage/Details/Create Views Summary

**Campaign quest board, Manage, Details, and Create views (desktop + mobile) render conditionally on `ViewBag.BoardType` — no CR badge, no signup/date-voting, Close/Reopen buttons on Manage, stripped Create form — with one-shot rendering unchanged.**

## Performance

- **Duration:** ~45 min (including human-verify checkpoint)
- **Completed:** 2026-07-03
- **Tasks:** 3/3
- **Files modified:** 8

## Accomplishments
- Campaign board card shows an Open/Closed wax seal instead of the one-shot Finalized seal, with CR badge and signup line removed so the description fills the space
- Campaign Create form (desktop + mobile) shows only Title + Description; one-shot form unchanged
- Manage and Details views hide CR badge, signup, and date-voting for campaign, and Manage gains Close/Reopen buttons (CSRF-protected, no confirm dialog)

## Task Commits

Each task was committed atomically:

1. **Task 1: Campaign board card + Create form conditional rendering (desktop + mobile)** - `cf808a9` (feat)
2. **Task 2: Manage/Details Close/Reopen buttons and CR/signup removal (desktop + mobile)** - `0a8b6f2` (feat)
3. **Task 3: Human-verify campaign board / Manage / Details / Create rendering** - checkpoint, approved after one fix (below)

**Fix committed during checkpoint:** `b9ebd59` (fix)

## Files Created/Modified
- `QuestBoard.Service/Views/Quest/Index.cshtml` - Campaign board card: Open/Closed seal, no CR/signup line
- `QuestBoard.Service/Views/Quest/Index.Mobile.cshtml` - Campaign board card mobile equivalent
- `QuestBoard.Service/Views/Quest/Manage.cshtml` - Close/Reopen buttons, CR/signup/date-voting hidden for campaign
- `QuestBoard.Service/Views/Quest/Manage.Mobile.cshtml` - Mobile equivalent
- `QuestBoard.Service/Views/Quest/Details.cshtml` - CR/signup/date-voting hidden for campaign
- `QuestBoard.Service/Views/Quest/Details.Mobile.cshtml` - Mobile equivalent, status badge reads Open/Closed
- `QuestBoard.Service/Views/Quest/Create.cshtml` - CR/Total Players/DM Session/Proposed Dates removed for campaign
- `QuestBoard.Service/Views/Quest/Create.Mobile.cshtml` - Mobile equivalent

## Decisions Made
- Campaign board wax seal always uses the visible `finalized-seal` CSS class instead of being keyed off `IsClosed` — see "Deviations from Plan" below.

## Deviations from Plan

### Auto-fixed Issues

**1. [Found at human-verify checkpoint] Campaign board wax seal never visible**
- **Found during:** Task 3 (human-verify checkpoint)
- **Issue:** The seal's visible/hidden CSS class was keyed on `quest.IsClosed` for campaign. Closed campaign quests are already excluded from the active board query (Plan 36-02's visibility filters), so `IsClosed` is always `false` for any quest rendered on the board — the seal always fell through to the hidden `open-seal` class and never appeared.
- **Fix:** Changed the class condition so campaign board cards always render the visible `finalized-seal` styling, regardless of `IsClosed`. Alt text still reflects Open/Closed for accessibility correctness if the filter behavior ever changes.
- **Files modified:** `QuestBoard.Service/Views/Quest/Index.cshtml`
- **Verification:** Human re-verified the seal renders on campaign board cards; full test suite still green.
- **Committed in:** `b9ebd59`

---

**Total deviations:** 1 auto-fixed (visual bug found during human verification)
**Impact on plan:** Necessary correctness fix for the feature to be usable. No scope creep.

## Issues Encountered
None beyond the deviation above.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- Plan 36-05 (Quest Log views) can proceed — no shared files with this plan.
- Human-verify checkpoint approved by user after the wax-seal fix; all 9 verification steps confirmed on desktop, mobile, and one-shot regression check.

---
*Phase: 36-campaign-quest-posting-closing*
*Completed: 2026-07-03*
