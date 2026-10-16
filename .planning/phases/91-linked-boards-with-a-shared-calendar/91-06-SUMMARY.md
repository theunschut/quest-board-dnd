---
phase: 91-linked-boards-with-a-shared-calendar
plan: 06
subsystem: ui
tags: [razor, mobile, platform, linked-boards, css]
status: complete

requires:
  - phase: 91-linked-boards-with-a-shared-calendar
    provides: LinkedBoardsController, desktop Linked Boards page (plans 01 and 03)
provides:
  - Mobile Linked Boards page with create, rename, add, remove and delete parity with desktop
  - Linked Boards entry button on the Platform Group index, desktop and mobile
affects: [platform-ui]

actuals:
  tokens: 11000
  tasks: 2
  commits: 2

tech-stack:
  added: []
  patterns:
    - "Confirm handler built as a C# string through Json.Serialize and written via onsubmit=@handler so Razor HTML-encodes it"

key-files:
  created:
    - QuestBoard.Service/Areas/Platform/Views/LinkedBoards/Index.Mobile.cshtml
    - QuestBoard.Service/wwwroot/css/platform-linked-boards.mobile.css
  modified:
    - QuestBoard.Service/Areas/Platform/Views/Group/Index.cshtml
    - QuestBoard.Service/Areas/Platform/Views/Group/Index.Mobile.cshtml
    - QuestBoard.IntegrationTests/Controllers/LinkedBoardsPlatformIntegrationTests.cs

key-decisions:
  - "A .linked-boards-mobile wrapper class on the card carries the 44px button rule, because mobile.css only sizes inputs, not buttons"
  - "Mobile confirm handlers use the string-then-onsubmit form from the desktop page, not the inline Json.Serialize form in the plan"

requirements-completed: []

duration: 15min
completed: 2026-10-16
---

# Phase 91 Plan 06: Mobile Linked Boards Page Summary

Mobile twin of the Platform Linked Boards page plus "Linked Boards" entry buttons on both Group index twins, with every desktop action, copy line, confirm prompt and empty/inactive state mirrored.

## Accomplishments

- `Index.Mobile.cshtml` uses `platform-group-card-mobile` and `group-card-mobile`, with stacked full-width create and add forms. Each set has a rename input-group, board rows with a right-aligned Remove Board button, and a full-width Delete Set button.
- `platform-linked-boards.mobile.css` has no media query. It styles `.linked-set-board-row` (wrapping names, 44px non-shrinking buttons) and a 44px rule for the page's buttons.
- A `btn btn-info` Linked Boards button sits beside Create Group and Manage Users on desktop, and a `btn-sm` one on mobile.
- 10 new integration tests: 3 in Task 1 (entry-point theory for desktop and mobile, mobile layout, mobile create+add) and 7 in Task 2 (empty page, one-board and empty-set Inactive states, no-eligible-board picker, two-board rows, confirm escaping, mobile RemoveBoard).

## Task Commits

1. Task 1 (tracer): `735516b8` - mobile page, CSS, entry points, create+add facts
2. Task 2: `ad0f3532` - rename/remove/delete with confirm, empty and inactive states

Tracer verified end-to-end: 67 tests passed in the plan's filter before Task 2 started.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Confirm handler form**
- **Found during:** Task 1 planning (instructed by the orchestrator, mirroring the 91-03 fix)
- **Issue:** `return confirm(@Json.Serialize(...))` inline breaks the attribute on names containing a double quote.
- **Fix:** Handler built as a C# string and written with `onsubmit="@handler"`.
- **Files modified:** Index.Mobile.cshtml
- **Commit:** ad0f3532

**2. [Rule 3 - Blocking] Test data for the no-eligible-board fact**
- **Found during:** Task 2
- **Issue:** The test database keeps a default One-Shot board, so a One-Shot set always has a candidate.
- **Fix:** The fact uses a Campaign set with only a One-Shot board unlinked.
- **Commit:** ad0f3532

## Known Stubs

None.

## Threat Flags

None. The mobile view adds no endpoint; it posts to the same `SuperAdminOnly` actions with form tag helpers emitting antiforgery tokens, `Json.Serialize` confirm text, and no `Html.Raw` or inline `style`.

## Verification

`dotnet test QuestBoard.IntegrationTests --filter "LinkedBoardsPlatformIntegrationTests|GroupManagementIntegrationTests|MobileCssTests|AntiForgeryTokenCoverageTests"`: 75 passed, 0 failed. The 390px phone-width layout is a backstop item and was not checked in a browser.

## Self-Check: PASSED

Created files exist, and commits 735516b8 and ad0f3532 are on the branch.
