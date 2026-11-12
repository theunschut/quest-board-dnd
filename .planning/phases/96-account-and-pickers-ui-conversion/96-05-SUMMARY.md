---
phase: 96-account-and-pickers-ui-conversion
plan: 05
subsystem: ui
tags: [picker, keyboard, a11y, razor, css-layers, guard-tests, tracer]
requires:
  - phase: 96-04
    provides: Gate 1 approved answers (flat rows, chevron on, scrim hover, phone title step) and the approved picker sheet
provides:
  - one GroupPicker/Index view for both User-Agents, every board a native submit button in its own SelectGroup form
  - group-picker.css on the foundation (flat rows, chevron, scrim hover, phone title step, 44px floor), loaded through the view's Styles section
  - AccountConversionGuardTests, the static guard every later account slice extends
  - picker facts for both User-Agents, landmark and third-party-host guards
affects: [96-06, 96-07, 96-08, 96-09, 96-10, 96-12]
tech-stack:
  added: []
  patterns: [page sheet linked through the view's Styles section, picker rows as submit buttons in per-row forms, static guard lists extended per slice]
key-files:
  created:
    - QuestBoard.IntegrationTests/Tests/AccountConversionGuardTests.cs
  modified:
    - QuestBoard.Service/Views/GroupPicker/Index.cshtml
    - QuestBoard.Service/wwwroot/css/group-picker.css
    - QuestBoard.Service/Views/Shared/_Layout.GroupPicker.cshtml
    - QuestBoard.IntegrationTests/Mobile/MobileViewsTests.cs
    - QuestBoard.IntegrationTests/Tests/UiFoundationDesktopShellGuardTests.cs
  deleted:
    - QuestBoard.Service/Views/GroupPicker/Index.Mobile.cshtml
key-decisions:
  - "Merge, flat-row relayout and keyboard fix landed in one commit (f29749a2), as the approved merge requires"
  - "The picker sheet moved from the layout's global list into the view's Styles section; the layout's Styles render point now sits directly after legacy-pages.css, so the cascade position is unchanged"
  - "The landmark and third-party-host facts are theories over the two User-Agents (mobile, desktop) instead of four separate facts; the names are as planned"
requirements-completed: [D-01, D-06, DoC-1, DoC-2, DoC-3, DoC-4, DoC-5, DoC-7, DoC-8, DoC-10, X-1]
duration: ~1.2 h
completed: 2026-11-12
status: complete
actuals:
  tokens: 21000
  tasks: 2
  commits: 1
---

# Phase 96 Plan 05: Board picker conversion (the tracer) Summary

The board picker is one view for both User-Agents. Every board is a native submit button in its own SelectGroup form, so a keyboard, switch or screen-reader user can choose a board and enter the app. The keyboard pass is green under both User-Agents, and the sweep shows zero contrast failures on every picker state.

## What was built

- **View.** `GroupPicker/Index.cshtml` is the single view. It has one `h1`, the Go to Platform link for super admins unchanged, and one `.modern-card` panel. The panel holds the unchanged no-boards alert or a `ul.group-picker-list[role=list]`. Each `li` holds `<form asp-action="SelectGroup" method="post">` with `@Html.AntiForgeryToken()`, the hidden `groupId` and then the hidden `returnUrl` byte for byte as before, and one `button.group-picker-row`. The button holds `.group-picker-text` (name, then the member count) and the chevron. Decorative icons are `aria-hidden`. There is no inline style, handler or tabindex.
- **Deleted.** `Views/GroupPicker/Index.Mobile.cshtml`. The view-location expander serves the single file to phones.
- **Moved sheet link.**
  - In: `@section Styles` in the view links `~/css/group-picker.css` once, with `asp-append-version`.
  - Out: line 16 of `_Layout.GroupPicker.cshtml`.
  - The layout's `RenderSectionAsync("Styles")` line is now directly after the `legacy-pages.css` link, so the cascade position is unchanged.
  - The layout's only other edit is the removed redundant `role="main"`. The body keeps `group-picker`, the layout stays footerless and the brand stays a non-link.
  - Rendered order is checked in the tests under both User-Agents: `group-picker.css` appears once and after `legacy-pages.css`.
- **Sheet.** `group-picker.css` is the approved v1 mockup sheet (`@layer pages`, tokens only):
  - flat rows with a hairline between them and a chevron;
  - hover at 60% of the page scrim and press at the full scrim;
  - the focus ring is the foundation's, and the focused row only lifts with `position: relative; z-index: 1`;
  - the phone block sets the title to 20px in parchment and keeps the `.group-picker .btn` 44px floor.

## Facts rewritten, renamed and added

- `MobileViewsTests.MobileGroupPicker_MobileUserAgent_RendersGroupCardsAndStylesSection` keeps its name and seeding. It now asserts 200, one `<button type="submit" class="group-picker-row"` per seeded board, `SelectGroup`, the sheet linked once and after `legacy-pages.css`, no `mobile-layout` and no account phone sheet. Its summary now explains the white-page guard.
- New `MobileGroupPicker_DesktopUserAgent_RendersTheSameSubmitRows` makes the same assertions under the desktop User-Agent.
- New `GroupPicker_BothUserAgents_RenderTheirLandmarks` (A-account-034) covers both User-Agents. It checks one skip link to `#main-content`, one labelled `nav`, one `main#main-content` and no `role="main"`.
- New `GroupPicker_BothUserAgents_RequestNoThirdPartyHost` (A-account-037) covers both User-Agents. It collects `src` and `href` on `link`, `script` and `img` tags, requires at least five and rejects any `http:`, `https:` or `//`.
- A shared seeding helper (`SeedPickerMemberAsync`) and `AssertPickerRows` carry the common code.
- `UiFoundationDesktopShellGuardTests.GroupPickerLayout_LinksItsOwnSheetAfterTheLegacyOnes` was rewritten in place and renamed `GroupPickerPage_LinksItsOwnSheetAfterTheLegacyOnes`:
  - the body keeps `group-picker`;
  - the layout no longer links the sheet;
  - the Styles render point is after `legacy-pages.css`;
  - the view sets the picker layout and links the sheet in its Styles section.
- New `AccountConversionGuardTests` has the lists `ConvertedViews`, `RetiredFiles`, `SheetLinks` and `AreaSheets`. Its facts are `ConvertedViews_CarryNoInlineStyleScriptOrHandler`, `RetiredFiles_AreGone`, `AreaViews_LinkTheirSheetThroughTheStylesSection`, `Picker_RowsAreNativeSubmitButtonsInTheSelectGroupForm` and `AreaFiles_CarryNoPlanningReference`. Planning-reference patterns are built from fragments.
- `GroupPickerControllerIntegrationTests` and `CrossBoardPickerSkipTests` pass unchanged. No test was deleted.

## Keyboard pass

`tools/keyboard-picker.mjs`, as the multi-board account on the real app, under the desktop and the Pixel 7 (touch) User-Agents:

- Tab order: skip link, Logout, then the three board rows in list order, with nothing between.
- Enter on the first row leaves `/groups/pick`.
- After a fresh sign-in, Space on the second row leaves `/groups/pick`.
- The script printed `ok`. It never prints board names.

## Sweep and readability-compare

`sweep.mjs` was run with stack mode on (`--stack`, a one-line scratch-tool switch passed to the contrast probe). It covered `picker-multi`, `picker-superadmin`, `picker-noboard` and `picker-keyboard`: 16 records, with 0 failed requests, and 0 console or page errors.

- **readability-compare** (`--zero-contrast` on all four rows, with the empty exceptions file): `failingRows: 0`, exit 0, `ok`.
- **Contrast.** There are 0 contrast failures on every record. Measured minimums: member count 6.09, board name 6.70 (matches the design's 6.09 and 6.70).
- **Overflow.** `overflow` and `textSpacingOverflow` are false at 1440, 320, 390 and 412, including the 60-character board name row. `focusRingMissing` is empty everywhere.
- **Targets.** `smallTargets` is an empty list on every phone record (multi-board, super admin, no boards, and the keyboard row, at 390 and 412).
- **Keyboard row.** `picker-keyboard` evaluated 1 assert with no failure (BEFORE had one).
- **A-account-036.** The bar's Logout measures 85x44 on phones, BEFORE and AFTER. The foundation's `.group-picker .btn` floor already held it at 44, so the 87x33 in the finding predates the foundation. The rule is kept in the phone block of `group-picker.css`, and the layout keeps the body class that reaches it.
- **allow.json.** `after/96-05/allow.json` maps one renamed control per row: the `main` landmark's role in the probe moved from `main` to `generic` once the redundant `role="main"` attribute was removed. The element is still the native main landmark, and the clickable card divs were never listed as controls, so nothing else needed an entry.

## diff.py overlays against the approved mockup

Overlays are in `~/qb-ui-review/phase-96/after/96-05/diff/` (`multi-d1440`, `multi-m390`, `multi-d320`, `noboard-d1440`, `noboard-m390`, `noboard-d320`, `super-d1440`, `super-m390`).

- Multi-board at 1440, 390 and 320 and no-boards at 1440, 390 and 320 are the same size as the mockup, with 0.04% to 0.37% of pixels differing. The differences are the member-count digits (the mockup is hand-typed, the review board's real counts are 8, 2 and 1), plus a few glyphs in the alert line. There is no layout, spacing, colour or chevron difference.
- Super admin is not comparable: it lists the real boards, so row count and height differ from the mockup's fixed list (40% and 50% differing, all from the extra rows). The frame itself was checked to match the approved shape. Nothing is carried to Gate 2.

## Findings closed

- **A-account-031:** every board is a native submit button reached by Tab and Space or Enter (the S1 keyboard blocker), under both User-Agents.
- **A-account-032:** the rows no longer stack a glass card on the glass panel, and contrast measures 6.09 and 6.70 with 0 failures.
- **A-account-033:** the per-board-card backdrop blur is gone, since the rows take no glass fill of their own (`group-picker.css` has no `backdrop-filter`).
- **A-account-034:** the redundant `role="main"` is gone and the landmarks are pinned on the rendered page. The brand stays a non-link, deliberately: the picker is shown before any board is chosen, so the brand has nowhere to lead. This is recorded as the starting state and is open to the operator at Gate 2.
- **A-account-035:** closed as deliberate by the inherited footerless answer, with no footer change.
- **A-account-036:** closed by the kept phone `.group-picker .btn` rule (see above).
- **A-account-037:** a regression guard. The starting state was already 0 third-party hosts. The fact fails on fewer than five asset references.

## Deviations from Plan

### Environment substitutions (operator-approved, see ENVIRONMENT.md)

- Port 8000 stands in for 8096 and the baseline on 8001 for 8097. `python3` replaces `python`. Chromium is the bundled browser, and `qb-audit-*` accounts replace `qb96-*`. SQL-backed tests ran against the docker `mssql-dev` through user-secrets.

### Auto-fixed issues

**1. [Rule 3 - Blocking] The scratch sweep had no way to switch the contrast probe to stack mode.**
- **Found during:** Task 2
- **Issue:** Without stack mode the panel's 14px muted text is misreported at 4.25 and trips `--zero-contrast` (see 96-03).
- **Fix:** `sweep.mjs` takes a `--stack` flag that it passes to `runProbes`. The scratch tools live under `~/qb-ui-review/phase-96/tools/`, so no repository file changed.

**2. [Rule 1 - Bug] The first keyboard script navigated away mid-redirect.**
- **Found during:** Task 1
- **Issue:** Right after Enter the script waited on `networkidle` only, so on the phone run the next `goto` aborted.
- **Fix:** It now waits for the URL to leave `/groups/pick`, then for network idle. This is a scratch-tool fix.

### Plan wording

- The landmark and third-party-host facts are `[Theory]` with the two User-Agents as inline data (mobile and desktop) instead of two `[Fact]`s each. Names and assertions are as planned.
- Task 2 needed no edit to the view or the sheet: the first sweep passed every check apart from the `main` role relabel. The task has no commit of its own; its output is the sweep, the compare, the overlays and this summary.

## Known Stubs

None.

## Threat Flags

None. The `SelectGroup` form keeps its antiforgery token. The hidden `groupId` and `returnUrl` inputs are byte for byte as before, with `returnUrl` written once and no `formaction` or `data-return-url`, so the membership-oracle comparison in `CrossBoardPickerSkipTests` still passes unchanged (T-96-12, T-96-13, T-96-14).

## Verification results

- `dotnet build`: 0 errors.
- Filtered run (picker, cross-board, mobile views, foundation guards, new guard): 182 passed.
- Full `dotnet test` with the SQL-backed tests required: 1389 unit and 1872 integration tests passed, 0 failed.
- App on 8000 restarted on the new code and left running.

## Self-Check: PASSED

- Files present: the view, `group-picker.css`, `AccountConversionGuardTests.cs` and the edited layout. `Index.Mobile.cshtml` is gone.
- Commit f29749a2 exists on `milestone/v9-rolling-improvements`.
- The plan's first verify command printed `ok`. The keyboard script printed `ok`. The sweep checks (smallTargets and readability-compare) printed `ok`.
