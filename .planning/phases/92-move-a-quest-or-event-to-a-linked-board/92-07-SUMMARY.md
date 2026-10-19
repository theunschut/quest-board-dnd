---
phase: 92-move-a-quest-or-event-to-a-linked-board
plan: 07
subsystem: ui
tags: [razor, css, move-board, consequence-list, mobile-twin]
requires:
  - phase: 92-04
    provides: Move page frame (MovePageViewModel, Index and Index.Mobile views)
  - phase: 92-05
    provides: quest preview data (dropped people, seats, chain titles, characters cleared)
  - phase: 92-06
    provides: event preview data (dropped answers, Campaign add count)
provides:
  - Full quest and event consequence rows on the Move page, desktop and mobile
  - Removed-people list with role annotation and Selected mark
  - Event summary lines (wall-clock date, time label, Cancelled)
  - move-entry.css (desktop, linked in layout) and move-entry.mobile.css (mobile, via Styles section)
  - BoardMoveViewsTests
affects: [92-08, 92-09]
tech-stack:
  added: []
  patterns:
    - "Sentences that wrap a board name in strong.move-strong are asserted on tag-stripped text"
    - "Source-hygiene facts read views and stylesheets from disk with an upward-walk path resolver"
key-files:
  created:
    - QuestBoard.Service/wwwroot/css/move-entry.css
    - QuestBoard.Service/wwwroot/css/move-entry.mobile.css
    - QuestBoard.IntegrationTests/Tests/BoardMoveViewsTests.cs
  modified:
    - QuestBoard.Service/Views/Move/Index.cshtml
    - QuestBoard.Service/Views/Move/Index.Mobile.cshtml
    - QuestBoard.Service/ViewModels/MoveViewModels/MovePageViewModel.cs
    - QuestBoard.Service/Views/Shared/_Layout.cshtml
key-decisions:
  - "The shared unlinking hint sits inside the last chain row (follow-up row, or the original row when there is no follow-up) instead of a separate list item, so one row equals one bordered consequence"
  - "move-control-reason is styled in move-entry.css only; the mobile rule is left to the Details-control plan"
  - "Comments in the stylesheets say 'No media queries' without the at-sign so the no-media-query fact can scan for the literal rule"
patterns-established:
  - "MovePageViewModel exposes ShowCharactersCleared, ShowSeats, ShowCalendarApps, ShowCampaignRow, EventTimeLabel and RoleLabel so desktop and mobile views share one rule per row"
requirements-completed: [D-03, D-05, D-06, D-08, D-10, D-12, D-14, D-18, D-21]
duration: 35min
completed: 2026-10-19
status: complete
actuals:
  tokens: 12000
  tasks: 2
  commits: 1
---

# Phase 92 Plan 07: Move page consequence rows Summary

**The Move page now lists everything a move will do before the confirm button, on desktop and the phone twin: removed players by name with seats marked, the waitlist outcome, cut chain links, automatic Campaign Yes rows, cancelled-stays-cancelled and the calendar-apps effect.**

## Performance

- **Duration:** about 35 min
- **Tasks:** 2 of 2
- **Files:** 3 created, 4 modified

## Accomplishments

- Quest rows in contract order: kept, characters cleared, removed signups with `ul.move-people`, seats, one row per known chain title with the permanence hint, calendar apps (One-Shot target only), emails. Zero-count rows are omitted.
- Event rows: kept, removed answers (names only), Campaign automatic Yes, cancelled, calendar apps, emails; event summary card shows `Html.WallClock` date, `HH:mm` or `All day`, and `Cancelled`.
- The DM refusal from the earlier frame renders for a real refusal and no confirm form is posted.
- `move-entry.css` linked once in `_Layout.cshtml`; `move-entry.mobile.css` linked only through `@section Styles` in `Index.Mobile.cshtml`. Neither has a media query.
- 28 new `BoardMoveViewsTests` facts (both user agents where relevant) cover row order, singular and plural copy, selected-first list, role annotations, omitted rows, stylesheet linking, event summary, refusal, encoding of a markup name, and view/stylesheet hygiene. The wider run (BoardMove, MobileCss, MobileViews, LayoutNavigation, WallClockUnmoved) passes: 195 passed, 0 failed.

## Task Commits

Both tasks touch the same two views and the same test file, so they landed in one commit:

1. **Tasks 1 and 2: quest rows, event rows, event summary, stylesheets, layout link, tests** - `7ee71ece` (feat)

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Role enum member name**
- **Found during:** build after Task 1 helpers
- **Issue:** the plan's interface notes name `SignupRole.AssistantDungeonMaster`; the enum member is `AssistantDM`
- **Fix:** used `AssistantDM` in `MovePageViewModel.RoleLabel` and the test
- **Commit:** 7ee71ece

**2. [Rule 3 - Blocking] Stylesheet comment tripped the no-media-query fact**
- **Found during:** first test run
- **Issue:** the header comment "No @media queries" contained the literal rule start the hygiene fact scans for
- **Fix:** reworded to "No media queries"
- **Commit:** 7ee71ece

### Structure choices (not defects)

- The two tasks were committed together rather than as two commits, since they edit the same view and test files and were developed in one pass.
- Sentences that put the board or a title in `strong.move-strong` are asserted on tag-stripped text; markup facts (classes, aria-label, links) assert on the raw HTML.

## Known Stubs

None.

## Browser-only checks (not covered by tests)

Two items from the contract are marked as needing a person with a browser and were not exercised here: at 390px with long board names the target select and the "What will happen on {Board}" heading wrap with no horizontal scroll; and a long board name in "Move to {Board}" wraps, stays at least 44px tall and causes no horizontal scroll.

## Threat Flags

None. Every name and title is Razor-encoded (no `Html.Raw`); a fact seeds a markup person name and asserts it comes back encoded.

## Self-Check: PASSED

- FOUND: QuestBoard.Service/wwwroot/css/move-entry.css
- FOUND: QuestBoard.Service/wwwroot/css/move-entry.mobile.css
- FOUND: QuestBoard.IntegrationTests/Tests/BoardMoveViewsTests.cs
- FOUND: commit 7ee71ece
