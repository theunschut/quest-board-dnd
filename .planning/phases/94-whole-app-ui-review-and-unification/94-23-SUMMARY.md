---
phase: 94-whole-app-ui-review-and-unification
plan: 23
subsystem: ui-foundation
tags: [css, legacy-retirement, cascade, rename, guard-tests, readability]
requires:
  - phase: 94-22
    provides: "phone shell, shell.mobile.css, one body::before backdrop with the 900px WebP"
  - phase: 94-21
    provides: "desktop shell (chrome, menus), tokens-driven fills"
  - phase: 94-20
    provides: "component layer (buttons, forms, alerts, cards) in components.css"
provides:
  - "legacy-pages.css: the unlayered, page-specific remainder of the old desktop global sheet (1,329 lines, was 1,644); no file named site.css remains"
  - "body carries the light board text colour once (base.css); paragraphs, lists, spans and headings inherit it"
  - "mobile.css reduced to 26 lines of phone page rules (container padding, bold headings, move-control reason)"
  - "UiFoundationLayerGuardTests.LegacyDesktopSheet_IsUnlayeredAndRenamed"
affects: [94-24, 94-25, 94-26, 94-27]
tech-stack:
  added: []
  patterns:
    - "Surface colour comes from the surface: a panel that is not the board sets its own text colour, descendants inherit it"
key-files:
  created: []
  modified:
    - QuestBoard.Service/wwwroot/css/legacy-pages.css (renamed from site.css)
    - QuestBoard.Service/wwwroot/css/mobile.css
    - QuestBoard.Service/wwwroot/css/base.css
    - QuestBoard.Service/wwwroot/css/shell.css
    - QuestBoard.Service/wwwroot/css/account.mobile.css
    - QuestBoard.Service/wwwroot/css/quests.css
    - QuestBoard.Service/wwwroot/css/markdown-content.css
    - QuestBoard.Service/wwwroot/css/markdown-editor.css
    - QuestBoard.Service/Views/Shared/_Layout.cshtml
    - QuestBoard.Service/Views/Shared/_Layout.GroupPicker.cshtml
    - QuestBoard.Service/Areas/Platform/Views/Shared/_Layout.Platform.cshtml
    - QuestBoard.IntegrationTests/Mobile/MobileCssTests.cs
    - QuestBoard.IntegrationTests/Tests/CalendarSubscriptionStaticGuardTests.cs
    - QuestBoard.IntegrationTests/Tests/UiFoundationLayerGuardTests.cs
    - QuestBoard.IntegrationTests/Tests/UiFoundationDesktopShellGuardTests.cs
decisions:
  - "The menu fill moved into shell.css as a plain layered rule once the unlayered div/ul background resets were gone; the --qb-menu-fill indirection and the unlayered restatement rule are deleted"
  - "mobile.css is kept (not deleted): three page rules remain, and it stays linked from both phone layouts"
  - "mobile.css keeps h1-h6 { font-weight: bold } only, because at the 20px card-title size only bold text counts as large text for contrast; it goes when the shared heading scale lands"
  - "account.mobile.css gains one declaration (.account-card-mobile h5 bold) for the same large-text reason"
metrics:
  duration: "two sessions (first executor stopped during the final test run; this run verified and finished)"
  completed: 2026-11-06
status: complete
actuals:
  tokens: 25000
  tasks: 3
  commits: 3
---

# Phase 94 Plan 23: Retire the Global Element, Component and Phone Families Summary

The unlayered global rules that beat the layered foundation are gone from both old global sheets: bare-board text now inherits one colour from `body`, Bootstrap components come from the component layer, and what is left of the desktop sheet carries its honest name, `legacy-pages.css`.

## Commits

| Task | Commit | What |
|------|--------|------|
| 1 (tracer) | `1dab39c4` | `body { color: var(--qb-fg-on-board) }` in base.css. Deleted from the old desktop sheet: `p, li, span, small` colour family, `h1`-`h6` colour, `.card-body`, `div` and `.container` background resets, `ul, ol, li { background: none }`, global `.text-muted` override, the Quest Log reset rules and the unlayered `.chrome .navbar-collapse, .chrome .dropdown-menu` restatement rule. `git mv site.css legacy-pages.css` with its three layout links, MobileCssTests, CalendarSubscriptionStaticGuardTests and the new `LegacyDesktopSheet_IsUnlayeredAndRenamed` guard in one commit |
| 2 | `09c41f6f` | Bootstrap component re-themes retired from `legacy-pages.css`: `.btn` and the primary/secondary/warning/danger/info variants, `.form-control`/`.form-select` (and focus, disabled, `bg-dark`), `.form-check-*`, `.form-label`, `.form-text`, `.alert`/`.alert-info`/`.alert-danger`, `.card`/`.card-header`, and the `:root` `--card-width` family (tokens.css owns it; quests.css keeps its own copy until the Quests area phase). 239 lines removed |
| 3 | `f5ecf9d7` | Phone `mobile.css`: body size, heading colour and shadow, `.text-muted`, `.btn-primary` and `.alert-danger` rules gone. `MobileCssTests` re-pinned: 16px fact reads base.css (`font-size: var(--qb-text-base)`) and tokens.css (`--qb-text-base: 1rem`); the mobile response fact asserts `shell.mobile.css`; still four facts |

The wave notes asked for a separate commit per task; all three landed as such. The first executor was stopped during its final `dotnet test`; this continuation fast-forwarded onto its three commits unchanged and verified them. No gap needed an additional code commit.

## Verification

- **Plan checks, against the code as it stands:** no `site.css` under wwwroot/css; no view or test references it (the only mention is the new guard asserting the file does not exist); `body::before` appears exactly once across wwwroot/css (base.css:53); no `--card-width` definition in legacy-pages.css; `modern-card.css` byte-identical to the base (empty `git diff`); no `--qb-menu-fill` or `.chrome .navbar-collapse, .chrome .dropdown-menu` restatement rule anywhere; no `ul, ol, li { background }` or `p, li, span, small` colour rule; mobile.css has no heading colour rule. MobileCssTests keeps 4 facts, CalendarSubscriptionStaticGuardTests keeps 5.
- **Desktop menus keep their fill (computed style, app on 8023):** the user dropdown computes `rgb(47, 27, 14)` (opaque overlay surface, no image); at 800px width the opened hamburger menu (`.navbar-collapse`) computes `rgb(47, 27, 14)`.
- **Request list (player role; desktop and Pixel 7):** desktop `/`, `/Shop`, `/Calendar` fetch only `notice-board-1600.webp`; phone `/`, `/Shop`, `/Calendar` fetch only `notice-board-900.webp`; no JPEG on any of them. (`/Account` carries no backdrop request under either agent in this probe; it is not a regression of this plan.)
- **`dotnet build`:** 0 errors. **Full `dotnet test`:** UnitTests 1,300 passed; IntegrationTests 1,749 passed, 11 skipped (the SQL Server migrator facts, which need a live server), 0 failed. No CLR flake hit.
- **Readability sweeps** (`ret3-final`, 120 records, 0 non-200, full manifest, both User-Agents; `ret3-after` had been deleted as stale, so `ret3-final` is the after-state for all three commits):
  - `ret2-after -> ret3-final`: FAIL on 4 rows, all triaged as false positives (below).
  - `ret1-before -> ret3-final` (cumulative): the same 4 rows and nothing else. No Quest Log row, no alert-heading row, no new contrast failure on any other page.

### The 4 flagged rows are data drift, not regressions

`Agenda-Index` (desktop and mobile) and `Events-Index` (desktop and mobile) fail on `interactiveLost` and, for Agenda mobile, one `newContrastFailures` row. The sweeps were taken four days apart (2026-11-02 and 2026-11-06) against the shared local DB, and the board's weekly event "Weekly Lantern Rounds" (Events/Details/3, Tue Nov 3) is now in the past, so it left the Agenda and Events lists. Evidence:

- The "lost" controls are exactly the links and buttons of that one event (`/Events/Details/3` Open Event / title link, one `Show players` button, and "Show More Events", which only renders when more than five events are listed; the list is now five).
- The remaining interactive controls moved up one slot and are identical otherwise (same tags, boxes, names).
- The Agenda mobile contrast row is `small.text-muted` "Tue, Dec 1 - 19:00" at 3.94:1. The same selector fails at the same 3.94:1 in `ret1-before` and `ret2-after` for "Tue, Nov 3". The report caps the failure list at five samples per selector, and the window slid forward by one event, so a new date text entered the sample while the old one left. It is a pre-existing failure, ratio unchanged, and not caused by this plan (font weight on that text went 600 -> 400 with the retired global muted-text override, and the ratio did not move).

## Deviations from Plan

None. The plan executed as written; the one-declaration page fix the plan allows was used once.

### Page fixes (allowed one-declaration page fix)

- `account.mobile.css`: `.account-card-mobile h5 { font-weight: bold; }`. With the global heading rule gone the picker card title was no longer bold, and at 20px only bold text qualifies as large text for the 3:1 bar. Committed with Task 1.

### Bootstrap-class mentions remaining in legacy-pages.css (all scoped under a page class)

`.purchase-details .text-muted`, `.purchase-item small.text-muted`, `.return-sell-buttons .btn` (and `:hover`, `.btn-success`, `.btn-warning`), `.btn-sm.btn-action` (compound with the page class `.btn-action`), `.vote-btn.btn-success/.btn-warning/.btn-danger`, `.calendar-subscription-actions .btn`. None starts with a bare Bootstrap component class.

## For the orchestrator: ledger entries (WINDOWS.md is not edited here)

- **15 (bare `div` background rule):** the `div` rule, the `.container` reset and the Quest Log exception below it are gone. Closable for site.css. The `.toast` background restatement in modern-card.css remains on purpose and goes with modern-card.css in 94-24, so keep 15 open for that one item or close it and rely on 94-24's scope.
- **20 (widened: `ul, ol, li` background, `p, li, span, small` colour, menu restatement rule):** all three removed, and the `--qb-menu-fill` seam with them. Closable. The footer/brand span-to-div changes from 94-21 were left as they are (harmless; no revert needed).
- **23 (phone h1-h6 colour rule):** the colour rule is gone from mobile.css. Closable for the colour. The offcanvas title is still `div.h5` (not reverted to `h5`); it may stay. mobile.css still carries `h1-h6 { font-weight: bold }` as a stopgap that belongs to the heading scale (entries 11 and 16, held for 94-24); I did not add any heading size.
- **17 (alerts ship as a parchment panel):** design not changed. Would a dark-panel alert heading pass now? Very likely yes. No rule in any sheet colours `.alert-heading` any more (grep), so an `h6.alert-heading` inherits the alert's own text colour exactly as Bootstrap intends; on a dark panel with light alert text it would be light on dark. The measured alert contrast failures in the sweeps are unchanged and are all the plain `alert-info` text (3.3-3.4:1, white on its info fill) and `ShopManagement-Edit`'s `h6.alert-heading` (light text, 2.69:1, identical in `ret1-before`; its panel background was not investigated); those are pre-existing page/surface colour issues, not the retired heading rule. Worth confirming visually at the 94-24 operator review before returning to the dark alert.

## Notes

- The two 2.5 MB `Notice Board-blank*.jpg` files under `QuestBoard.Service/wwwroot/images` are unreferenced encode sources; left untouched as instructed.
- `modern-card.css` is untouched, including its `.toast` restatement (retired in 94-24).
- Mobile Cinzel hinting contrast notes remain accepted (entry 7). The shared readability tool matches sample-less failures by selector and bar only (entries 14, 19); samples drifted as described above.
- Scratch sweeps `ret1-before`, `ret1-after`, `ret2-after`, `ret3-final` stay under `~/qb-ui-review/phase-94`. Port 8023 is stopped and no wait loops are running.

## Known Stubs

None.

## Threat Flags

None. The change removes CSS rules and renames a stylesheet; no new endpoint, auth path or schema.

## TDD Gate Compliance

Not applicable (execute plan, not tdd).
