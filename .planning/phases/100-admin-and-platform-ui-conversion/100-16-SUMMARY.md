---
phase: 100-admin-and-platform-ui-conversion
plan: 16
subsystem: ui
tags: [razor, platform, group-index, board-type-badge, layered-css, guard-tests, mobile-twins]
requires:
  - phase: 100-12
    provides: Gate 1 answers (fixed left-aligned action slots, One-Shot wording, button roles)
  - phase: 100-13
    provides: AdminPlatformDisplay board-type mapping, the area guard lists, the row-slip classes
  - phase: 100-15
    provides: guard lists as they stood, the form contract
provides:
  - Views/Shared/_BoardTypeBadge, the one board-type badge (word, fill and icon from the display mapping)
  - Platform Group/Index on both layouts: wrapping page-title header, one primary action, left-aligned fixed action slots, phone row slips, empty-state block
  - Group/Delete desktop title raised to h1; the delete partial uses the shared badge (no text-dark)
  - platform-pages.css and platform-pages.mobile.css as layered page sheets, in the layer and literal guards
affects: [100-17, 100-18, 100-19]
tech-stack:
  added: []
  patterns: [Platform page sheet is self-contained (page-bar, name cell, board-actions, empty-state override) so a Platform page links platform-pages.css and never admin-pages.css; a phone Platform page links both platform sheets]
key-files:
  created:
    - QuestBoard.Service/Views/Shared/_BoardTypeBadge.cshtml
    - QuestBoard.Service/wwwroot/css/platform-pages.css
    - QuestBoard.Service/wwwroot/css/platform-pages.mobile.css
  modified:
    - QuestBoard.Service/Areas/Platform/Views/Group/Index.cshtml
    - QuestBoard.Service/Areas/Platform/Views/Group/Index.Mobile.cshtml
    - QuestBoard.Service/Areas/Platform/Views/Group/Delete.cshtml
    - QuestBoard.Service/Areas/Platform/Views/Group/_BoardDeleteBody.cshtml
    - QuestBoard.IntegrationTests/Tests/AdminPlatformRenderTests.cs
    - QuestBoard.IntegrationTests/Tests/AdminPlatformConversionGuardTests.cs
    - QuestBoard.IntegrationTests/Tests/UiFoundationLayerGuardTests.cs
    - QuestBoard.IntegrationTests/Controllers/BoardPageBoardListTests.cs
    - QuestBoard.IntegrationTests/Tests/BoardPageEndToEndTests.cs
    - QuestBoard.IntegrationTests/Controllers/LinkedBoardsPlatformIntegrationTests.cs
key-decisions:
  - "Gate 1 item 16 left: the Actions header and cells are left-aligned; Open Board, Members and Edit keep their position whether or not Delete (last, only for an empty board) is present"
  - "Gate 1 item 6a yes: the badge word is One-Shot, taken from AdminPlatformDisplay; Campaign is the info fill with its own readable label and no text-dark utility"
  - "Open Board is btn-success on both layouts (as drawn); Create Group is the page's single btn-primary"
  - "platform-pages.css copies the few shared rules it needs (page-bar, name cell, empty-state contrast override) instead of linking admin-pages.css, so the two area sheets stay independent; the board action row uses its own class (board-actions) because its alignment differs from the Admin right-aligned row"
  - "The phone Group index no longer links platform-group.mobile.css; the sheet stays on disk and in the pending list until its last consumer converts"
patterns-established:
  - "A board-type word, fill and icon is rendered only through <partial name=\"_BoardTypeBadge\" model=\"boardType\" />"
requirements-completed: [D-09, D-15, D-16, D-19, D-21, DoC-1, DoC-2, DoC-3, DoC-4, DoC-8, DoC-9, DoC-10]
duration: ~1h
completed: 2026-11-18
status: complete
actuals:
  tokens: 22000
  tasks: 3
  commits: 3
---

# Phase 100 Plan 16: Platform Group index and delete Summary

**The Platform board list now reads at every width: one page title in the card header with the three header actions wrapping beneath it, Open Board, Members, Edit and Delete in left-aligned fixed slots, phone row slips, and the board type drawn by one shared badge that the delete page uses as well.**

## What was built

- `_BoardTypeBadge` (model `BoardType`) renders `<span class="badge ...">` with icon and word from `AdminPlatformDisplay.BoardTypeBadge`. Group/Index (both layouts) and `_BoardDeleteBody` use it; the delete partial's own Campaign badge (which carried `text-dark`) is gone. The home-page badge on the list is unchanged.
- Group/Index desktop: card header `page-bar` with the one `h1`, header actions in a wrapping `page-bar__actions` group (Create Group primary, Manage Users secondary, Linked Boards info), table with Name, Board Type, Members, Created (`Html.LocalTime` of the real instant) and Actions; Actions left-aligned in `board-actions`. Open Board stays a POST form to the group picker with an antiforgery token. Empty state is `.empty-state` with the same two sentences.
- Group/Index phone: `.row-slip` per board with name, board-type badge and home badge in the head, the Members and Created line, and the actions at the shared 44px target size.
- Group/Delete desktop title raised from h2 to h1 to match its phone twin. Nothing else in the pair, its sheets, its script or the board-deletion code changed (diff against the plan start is empty for the two Phase 102 sheets and the module).
- Guard lists: the Index pair, the Delete pair, the delete partial and the new badge partial moved from pending to converted with their sheet links; the two new sheets are layered, literal-free and held to the area-sheet rules; every file touched is in the planning-reference scan list.

## Verification

- Build: 0 errors. Unit tests: 1636 passed. Integration tests: 2961 passed, 19 skipped (the same 19 reproduction and open-gap facts as before), 0 failed.
- New render facts (both layouts, 18 cases in `AdminPlatformRenderTests`): badge word, fill and icon on the list and the delete page with no `text-dark`; sheet links (and no retired phone sheet); one h1 in the card header; header action group and the single primary; row action order including Delete only for an empty board; Open Board as a POST form with token and board id; Created as a local-time element and the member count; empty-state words on an empty database; delete page title and name-confirmation form.
- Sweep against BEFORE (`group-index` and `group-delete`, both User-Agents, widths 320/1024/1440 desktop and 390/412 phone): 9 captures compared, 0 failing rows, 0 console or page errors, 0 failed requests, 0 small targets, 0 missing focus rings. Group/Index: the horizontal overflow BEFORE had at 320 on the desktop layout (page scroll width 369) is gone (scroll width 320, the table scrolls inside its own container); the phone layout had 78 contrast failures BEFORE and has 0; the title is now an h1 on both layouts. Group/Delete: unchanged from BEFORE (see Open items).
- Group/Index shows every board of the install, so its captures stay under the scratch root and this summary is structural only.

## Task commits

1. Task 1 (tracer): `96103244` feat(100-16): shared board-type badge, Group index on both layouts with fixed left-aligned action slots. The tracer gate (badge facts, delete page facts, sheet links under both User-Agents, board-deletion and group-management suites) passed before expanding.
2. Task 2: `fd5fae9e` feat(100-16): Group delete title is the page h1 on desktop, Delete pair and Index pair join the converted view lists.
3. Follow-up: `bf63ecde` test(100-16): the phone Group index fact looks for its new sheet; row-finder tests join the scanned list.
4. Task 3: the docs commit carrying this summary, STATE.md and ROADMAP.md.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Three existing facts pinned the retired phone markup**
- **Found during:** Tasks 1 and 3
- **Issue:** `BoardPageBoardListTests` and `BoardPageEndToEndTests` found a phone board row by the `group-card-mobile` class, and a `LinkedBoardsPlatformIntegrationTests` fact asserted `platform-group-card-mobile` on the phone Group index. Both markers left with the conversion.
- **Fix:** the row finders split on `row-slip`; the phone fact asserts `platform-pages.mobile.css`. Same intent, assertions unchanged; recorded in the scratch root `tests-renamed.md`. The three files joined the planning-reference scan list.
- **Files modified:** the three test files above
- **Commits:** `96103244`, `bf63ecde`

**2. [Plan wording] The sweep needed no allow list**
- The compare reported 0 problems with an empty `allow.json`, so none of the expected renames (Open Board role, Delete form) were flagged; an empty file is kept for the verify command.

None of the plan's prohibitions were crossed: no change to the Phase 102 sheets, script, controller or service code, or to the Delete pair's form and wording.

## Inherited questions kept

- Q-109: the Group/Delete twin stays, no merge (Q-108 and Q-110 likewise for Create and Edit in 100-17).
- Q-91: the duplicate desktop h1 of CreateUser and EditUser is not copied to the phone; 100-15 removed it on desktop.

## Open items

- Group/Delete keeps one contrast failure it already had BEFORE: the help line under the name field (`.form-text` on the glass card, 4.25:1 against the 4.5:1 bar). It is the same pattern as the empty-state sentence fixed in the shared sheets. It is outside this plan's prohibitions (the Delete pair's sheets and form are not to change) and was not made worse; a rule in `platform-group-delete.css` (or an override linked by the Delete pair) would close it in a later slice.
- At 1440 a Delete button wraps onto a second line inside the Actions cell for empty boards, because the table's Actions column is narrow beside long names; the first three slots do not move, as item 16 requires.

## Known Stubs

None.

## Threat Flags

None. Open Board keeps its antiforgery token (checked by a render fact and the guard's post-form scanner); the board-deletion pieces changed only by the badge line and the title level.

## Self-Check: PASSED

- Files exist: `_BoardTypeBadge.cshtml`, `platform-pages.css`, `platform-pages.mobile.css`, this summary.
- Commits exist: `96103244`, `fd5fae9e`, `bf63ecde`.
