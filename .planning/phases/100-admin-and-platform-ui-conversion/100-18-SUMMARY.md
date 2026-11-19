---
phase: 100-admin-and-platform-ui-conversion
plan: 18
subsystem: ui
tags: [razor, platform, linked-boards, platform-users, confirm-submit, refused-control, layered-css, guard-tests, mobile-twins]
requires:
  - phase: 100-12
    provides: Gate 1 answers (refused-control look, button roles, accessible names, decoration)
  - phase: 100-13
    provides: AdminPlatformDisplay mapping (account, set state and board type badges), area guard lists, .control-reason, .row-slip
  - phase: 100-16
    provides: platform-pages.css and platform-pages.mobile.css, _BoardTypeBadge
  - phase: 100-17
    provides: the form contract on Platform forms, the guard-list state
provides:
  - Platform LinkedBoards on both layouts: one h1 in the title card, the Create set form with the board-type select from the shared mapping, each set as one .modern-card--aside card with an h2, the shared type badge and the state badge with its icon, Delete Set and Remove Board confirmed through server-rendered data-confirm-message attributes (no inline handler, names Razor-encoded), disabled Add a board described by its .form-text, empty state with its words
  - Platform Users on both layouts: one h1 "User Accounts", Confirmed and Status badges from the mapping, Disable confirmed through data-confirm-message, the signed-in account's Disable as the refused control (aria-disabled, outside any form, aria-describedby to a .control-reason with "You cannot disable your own account.", no title), email links at least 24px (desktop) and 44px (phone), phone .row-slip rows with the address on its own line
  - platform-linked-boards.mobile.css and platform-users.mobile.css deleted; the LinkedBoards phone view no longer links the old Group phone sheet
affects: [100-19, 100-21]
tech-stack:
  added: []
  patterns: [a confirm sentence with a stored name is written as literal text plus a Razor expression inside data-confirm-message, so the name is HTML-encoded and read back through dataset; a refused account action is a type=button aria-disabled control plus one .control-reason line keyed reason-own-<id>; set and Actions-cell rules live in platform-pages.css]
key-files:
  created: []
  modified:
    - QuestBoard.Service/Areas/Platform/Views/LinkedBoards/Index.cshtml
    - QuestBoard.Service/Areas/Platform/Views/LinkedBoards/Index.Mobile.cshtml
    - QuestBoard.Service/Areas/Platform/Views/Users/Index.cshtml
    - QuestBoard.Service/Areas/Platform/Views/Users/Index.Mobile.cshtml
    - QuestBoard.Service/wwwroot/css/platform-pages.css
    - QuestBoard.Service/wwwroot/css/platform-pages.mobile.css
    - QuestBoard.IntegrationTests/Controllers/LinkedBoardsPlatformIntegrationTests.cs
    - QuestBoard.IntegrationTests/Controllers/UsersControllerIntegrationTests.cs
    - QuestBoard.IntegrationTests/Tests/AdminPlatformConversionGuardTests.cs
    - QuestBoard.IntegrationTests/Tests/AdminPlatformRenderTests.cs
key-decisions:
  - "Create Set is the page's one btn-primary on both layouts (was btn-success), as drawn; Add Board stays success, Rename warning, deletes danger"
  - "The whole LinkedBoards pair was converted in the first commit (the tracer), because one view file holds both the confirmation and the page shape; the second commit is Platform Users"
  - "The Users empty state became the shared .empty-state with its existing words only (heading 'No users found.'); no new sentence was written"
  - "The phone Boards cell keeps its Phase 102 markup (details.user-boards, '0 boards'); only the spacing utility classes were dropped because the slip's gap now spaces its parts"
  - "Group/Delete #board-delete-help (4.25:1) NOT fixed: Group/Delete links only the board deletion sheets, and this plan owns none of them"
requirements-completed: [D-03, D-09, D-13, D-15, D-16, D-19, D-21, DoC-1, DoC-4, DoC-8, DoC-10]
duration: ~1h20m
completed: 2026-11-18
status: complete
actuals:
  tokens: 95000
  tasks: 3
  commits: 2
---

# Phase 100 Plan 18: Platform Users and Linked Boards Summary

**No stored set, board or account name can break a confirmation any more: every Platform confirm is an encoded `data-confirm-message` read by `confirm-submit.js`, the own-account Disable explains itself with a visible reason, and both pages read on the shared look at every width with two more phone sheets gone.**

## What was built
- **LinkedBoards (both layouts):** title card with one `h1`, the explanation, and the Create set form (required markers, `autocomplete="off"`, board-type select from `AdminPlatformDisplay.BoardTypeOptions()` keeping the numeric values and the placeholder, help as `.form-text` through `aria-describedby`). Each set is `section.modern-card.modern-card--aside` labelled by its `h2`; the header carries `_BoardTypeBadge` and the state badge from `LinkedSetState` (icon and word). Rename keeps its input group; the board table has a right-aligned Actions column (phone: `.row-slip` per board); Add a board is described by `#no-candidates-<id>` only when no candidate exists; Delete Set sits in `.set-delete` (full width on the phone). Remove Board and Delete Set are POST forms whose `data-confirm-message` holds today's sentence with the names as Razor-encoded text; `confirm-submit.js` loads as a module in `@section Scripts` on both. The empty state keeps its heading and sentence on the shared `.empty-state`.
- **Platform Users (both layouts):** one `h1`; the desktop table keeps Name (with `fa-user`), Email, Boards (cell unchanged), Confirmed, Status and a right-aligned Actions column; Confirmed and Status badges come from `AccountConfirmation` and `AccountState`; Enable is a POST form; Disable is a POST form with today's sentence; the signed-in account's Disable is `<button type="button" class="btn btn-sm btn-danger disabled" aria-disabled="true" aria-describedby="reason-own-<id>">` outside any form with the existing words in a `.control-reason` (the `title` is gone). Email is an `email-link` (padding gives at least 24px desktop; 44px on the phone) or "No email provided". Phone rows are `.row-slip` with the address on its own `.slip-line`, the Phase 102 `details.user-boards` untouched, Enable/Disable in `.row-slip__actions`.
- **Sheets:** `platform-pages.css` gained `.row-actions`, `.actions-cell`, `.th-actions`, the Actions-cell reason rule, `.set-header__badges`, `.set-form`/`.set-block`, `.set-delete`, `.email-link` and `.email-none`; `platform-pages.mobile.css` gained the finger-sized set forms and Delete Set, the 44px email link and the user slip name. `platform-linked-boards.mobile.css` and `platform-users.mobile.css` are deleted; `platform-users-boards.mobile.css` and the controllers are untouched.
- **Guards and facts:** the four views moved from PendingViews to ConvertedViews, ViewSheetLinks has all four, both sheets moved to RetiredFiles (PendingSheets now holds only platform-group.mobile.css), PhaseSourceFiles got the four views and UsersControllerIntegrationTests. 22 new cases in all: 10 LinkedBoards and 10 Platform Users render cases in AdminPlatformRenderTests (5 facts each, both layouts), and 2 facts in LinkedBoardsPlatformIntegrationTests that post the rendered Delete Set form on each layout.

## Verification
- Unit 1636 passed. Integration 3009 passed, 19 skipped (the same 19 reproduction and SQL facts as before), 0 failed.
- Sweep of `platform-users` and `linked-boards` under both User-Agents (5 captures each, 320 to 1440): 10 compared, 0 failing against BEFORE. Contrast failures 0, overflow none (320 scroll width 320), small targets 0, missing focus rings 0, no console or page errors. The only allow entries are accessible-name renames: the own Disable button (was named by its title, now "Disable" with the reason as its description) and the two Create set controls (now carry the required marker text). Phone email links measure 44px (50px when the address wraps); desktop 32px.
- Click-through under both User-Agents on p100 fixtures: Remove Board for P100 Audit Campaign Two in P100 Audit Set and Delete Set for P100 Audit Lone Set dismissed; the native dialog text equals today's sentence and the attribute, nothing was sent, the rows stayed. A Disable confirm on Platform Users was dismissed the same way (no write). `fixtures.mjs --check` passed under both User-Agents. Record: `~/qb-ui-review/phase-100/after/100-18/clicks.json`.
- Platform Users and LinkedBoards list real accounts and sets: captures stay local and this summary is structural (D-09).

## Deviations from Plan
- **Task split:** the tracer commit converted the whole LinkedBoards pair (not only the set card and confirmations), because the view is one file and a half-converted page would not pass its own facts; Task 2 then covered Platform Users only. No behaviour differs from the plan.
- **Extra click step:** the click-through also dismisses a Disable confirm on Platform Users, which the plan did not require; it sends nothing.
- **[Rule 1 - Bug] Test pins updated:** seven LinkedBoardsPlatformIntegrationTests facts pinned the old badge text (`badge bg-success">Active`) and the phone row marker; they were moved to the shared badge markup and `row-slip` with the same assertions. Recorded in `tests-renamed.md`. None deleted.
- **Open item from 100-16 (Group/Delete `#board-delete-help`, 4.25:1):** not fixed. Group/Delete links only `platform-group-delete.css` and its phone twin; this plan owns no sheet that page links.

## Known Stubs
None.

## Threat Flags
None. T-100-60 (names in inline handler strings) is closed by the attribute confirmation and the hostile-name facts on both layouts; T-100-61 keeps the server refusal and now shows the refused control with its reason; T-100-62 held (local captures, structural report).

## Self-Check: PASSED
- Commits 881e250b and fd068a91 exist; the two retired sheets are gone from disk; both suites green.
