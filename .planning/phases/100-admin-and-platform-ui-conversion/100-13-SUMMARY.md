---
phase: 100-admin-and-platform-ui-conversion
plan: 13
subsystem: ui
tags: [display-mapping, razor, layered-css, guard-tests, admin-quests, mobile-twins]
requires:
  - phase: 100-12
    provides: Gate 1 answers (One-Shot, Dungeon Master, Admin wording; Done and Closed outline in phone slips) and the approved drawings
  - phase: 100-08
    provides: DeleteQuest as a POST form with data-confirm-message and the confirm-submit module
provides:
  - AdminPlatformDisplay, the one static mapping for roles, board types, quest status, account and linked-set state
  - _QuestStatusBadge, the one quest status partial both Admin/Quests layouts render
  - .control-reason and .row-slip in components.css (@layer components)
  - admin-pages.css and admin-pages.mobile.css (@layer pages) and the converted Admin/Quests pair
  - the area guard scanners (ConvertedViews / PendingViews) and AdminPlatformRenderTests
affects: [100-14, 100-15, 100-16, 100-17, 100-18, 100-19, 100-20, 100-21, 100-22]
tech-stack:
  added: []
  patterns: [static display mapping with a Badge record, one status partial taking a small model, scanners each with a seeded bad-sample self-test, converted/pending view lists that must cover every area view on disk]
key-files:
  created:
    - QuestBoard.Service/ViewModels/AdminViewModels/AdminPlatformDisplay.cs
    - QuestBoard.Service/ViewModels/AdminViewModels/QuestStatusBadgeModel.cs
    - QuestBoard.Service/Views/Shared/_QuestStatusBadge.cshtml
    - QuestBoard.Service/wwwroot/css/admin-pages.css
    - QuestBoard.Service/wwwroot/css/admin-pages.mobile.css
    - QuestBoard.IntegrationTests/Tests/AdminPlatformRenderTests.cs
    - QuestBoard.IntegrationTests/Tests/AdminPlatformDisplayTests.cs
  modified:
    - QuestBoard.Service/Views/Admin/Quests.cshtml
    - QuestBoard.Service/Views/Admin/Quests.Mobile.cshtml
    - QuestBoard.Service/wwwroot/css/components.css
    - QuestBoard.IntegrationTests/Tests/AdminPlatformConversionGuardTests.cs
    - QuestBoard.IntegrationTests/Tests/UiFoundationLayerGuardTests.cs
    - QuestBoard.IntegrationTests/Mobile/MobileViewsTests.cs
    - QuestBoard.IntegrationTests/Controllers/QuestStatusBoardClockTests.cs
  deleted:
    - QuestBoard.Service/wwwroot/css/admin-quests.mobile.css
key-decisions:
  - "Wording is the Gate 1 answer: Admin (not Administrator), Dungeon Master, One-Shot; written only in AdminPlatformDisplay, enums and option values untouched (option values stay the enum numbers)"
  - "The Done and Closed badge carries a 1px --qb-border-strong outline only inside a phone slip (.mobile-layout .row-slip .badge.bg-dark), per Gate 1 item 18 yes"
  - "The phone Quests view gains the Campaign branch by calling the same partial, so Closed and Open show on both layouts"
  - "The empty-state sentence on the phone Quests card takes the card colour (admin-pages.css) because the shared muted tone measured 4.25:1; the shared .empty-state-text rule is untouched"
patterns-established:
  - "A page converts by moving its views from PendingViews to ConvertedViews and naming its sheets in ViewSheetLinks, in the same commit"
  - "A scanner in AdminPlatformConversionGuardTests is paired with a fact that feeds it a seeded bad string and a clean one"
requirements-completed: [D-15, D-16, D-19, D-21, DoC-1, DoC-2, DoC-3, DoC-4, DoC-8, DoC-9, DoC-10]
duration: ~1h30
completed: 2026-11-18
status: complete
actuals:
  tokens: 19700
  tasks: 3
  commits: 4
---

# Phase 100 Plan 13: Display mapping and Admin Quests Summary

**One static AdminPlatformDisplay mapping and one _QuestStatusBadge partial now drive the quest status on both Admin/Quests layouts, which are converted to the approved drawing on new layered admin sheets, with the area's guard scanners and render tests in place for every later slice.**

## What was built

- **AdminPlatformDisplay** (Service project, plain XML docs): group role (`Admin`, `Dungeon Master`, `Player`), board type (`One-Shot`, `Campaign`), the board home-page badge words, quest status (`QuestStatus(boardType, isClosed, isFinalized, hasBeenPlayed)`), account confirmation and state, linked-set state (with the new `fa-check-circle` and `fa-pause-circle` icons), and option-list helpers (`BoardTypeOptions`, `GroupRoleOptions`) whose values are the enum numbers and whose text is the mapped word. Each badge is one `Badge(Word, FillClass, Icon)` record. The Admin role word is written here and nowhere else (Gate 1 item 6c: `Admin`).
- **_QuestStatusBadge** takes `QuestStatusBadgeModel(Quest, BoardType, BoardToday)`; the day comes from the controller's `ViewBag.BoardToday`, never a clock. Campaign: Closed or Open from `IsClosed`. One-Shot: Done (`HasBeenPlayed(boardToday)`), Finalized, otherwise Open. The phone view now has the Campaign branch it lacked.
- **Shared classes** in `components.css`, `@layer components`, tokens only: `.control-reason` (and its on-paper tone) and `.row-slip` with `__head` and `__actions`, plus the 1px strong outline on `.bg-dark` badges inside a slip. `.move-control-reason` is untouched (still 1 occurrence).
- **Sheets**: `admin-pages.css` (page bar, name cell, person cell, right-aligned Actions cell, two-line description clamp at 320px, empty-state sentence colour) and `admin-pages.mobile.css` (slip name and line), each one `@layer pages` block, tokens only. Both are in `UiFoundationLayerGuardTests` `LayeredFiles` and `NoLiteralFiles`.
- **Admin/Quests pair**: one `h1` "Quest Management" in the page-title card header, desktop table (Title, Description, Dungeon Master with email under the name, Status, right-aligned Actions), phone `.row-slip` rows (no description, no DM email), `.empty-state` with today's words per layout (desktop heading only; phone heading plus "Quests appear here once created by a DM."), `Edit` as `btn-warning`, `Delete` a POST form with the same confirm sentence. No `style` attribute remains. `admin-quests.mobile.css` is deleted in the commit that converts its last consumer.

## Guards and tests

- `AdminPlatformConversionGuardTests` gained `ConvertedViews` (the Quests pair and the status partial), `PendingViews` (every other Admin and Platform view, 24), `ViewSheetLinks`, `AreaSheets` (the two admin sheets and the three board-deletion sheets), `PendingSheets` (the five unlayered phone sheets still in use), `AreaModules` (`confirm-submit.js`), `RetiredFiles` (`admin-quests.mobile.css`). Scanners, each with a self-test on a seeded bad string: inline code (style attribute, style element, inline script, on-event handler), Styles-section links, sheet literals and important flags and `transition: all` and `@keyframes` and infinite animations, whole-image (`aspect-ratio` and `object-fit`, ignoring comments and custom properties), markup built from strings, post forms with an explicit action and no token, raw date formats, one `h1`, enum display branches (a comparison with a role or board type member, a printed enum name, an enum-built option list), old delete handler shapes (`method="delete"`, `fetch(`, `alert(`), and the scope fact that every area view on disk is in exactly one list. Mutation check, tried once: a seeded `style=` attribute, an `onclick`, a second `h1`, an `aspect-ratio`, a hex colour and `transition: all` added to the real Quests view and sheet failed four real facts; the files were restored.
- `AdminPlatformRenderTests` (new; both User-Agents as theories): status words, fills and icons for a Campaign board (Open, Closed) and a One-Shot board (Finalized, Open); one `h1` in the card header; sheet links (phone also links the phone sheet); the Delete form with its confirm sentence, token and module; the empty state per layout; the phone slip (no description, no DM email); the desktop excerpt and DM address.
- `AdminPlatformDisplayTests` (new): every mapped word, fill and icon, the option lists, the home badge.
- Renamed, none deleted (recorded in `~/qb-ui-review/phase-100/tests-renamed.md`): `QuestStatusBoardClockTests.AdminQuests_Mobile_ShowsLastNightsGameNightAsDone` (marker `admin-quests-card-mobile` to `row-slip`), `MobileViewsTests.GetMobilePage_AdminQuests_ReturnsSuccessAndMobileLayout` (marker and sheet to `row-slip` and `admin-pages.mobile.css`).
- Suites: `dotnet build` 0 errors; unit 1636 passed; integration 2899 passed, 19 skipped (2850 + 19 at 100-10; the 49 new facts are this plan's).

## Sweep and unmoved check

- Sweep `admin-quests`, `admin-quests-campaign`, `admin-quests-empty` under both User-Agents at every width into `~/qb-ui-review/phase-100/after/100-13/` (13 captures, all 200). `readability-compare.py` against BEFORE with `after/100-13/allow.json`: 13 compared, 0 failing rows. The allow file holds only the Delete buttons' type change (`button` to `submit`) that the POST form conversion made earlier. `contrast-exceptions.json` does not exist in the scratch root, so `--exceptions` was left off (nothing is excepted).
- The first sweep found one new contrast failure (phone empty-state sentence, 4.25:1 against 4.5); it was fixed in `admin-pages.css`, not allowed, and re-swept clean.
- Unmoved check (copy `tools/unmoved-100.mjs`, p100admin on P100 Audit One-Shot, 8340 against the 8341 baseline, both User-Agents): `/Calendar`, `/Quest/Manage/992060`, `/Quest/Details/992060` and `/` (redirects to `/quests`) show 0 computed-style differences, except the home poster background URLs, which differ only by port (8341 vs 8340). `/Quest` itself returns an empty document on both ports, so it was replaced by the Manage and Details pages.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Phone empty-state sentence under the text bar**
- **Found during:** Task 3 sweep
- **Issue:** `.empty-state-text` uses the muted tone, which measures 4.25:1 on the page card's glass over the lightest board pixel (BEFORE's sentence was 3.94:1 under a different selector, so the compare counted it new).
- **Fix:** `.modern-card .empty-state-text { color: inherit; }` in `admin-pages.css` only, so no shared rule changed.
- **Files modified:** `QuestBoard.Service/wwwroot/css/admin-pages.css`
- **Commit:** 5e992db5

### Plan adjustments (no behaviour change)

- The first commit (7460f130) already carries `admin-quests.mobile.css`'s deletion, the two shared classes, the layer-array entries and the two rewritten phone guards, because converting the phone Quests view (the tracer) removed the sheet's last consumer; Task 2's commit therefore holds only the guard scanners and the mapping facts.
- `_QuestStatusBadge` is in `ConvertedViews`, and the scope fact also covers Shared partials named `_QuestStatusBadge*`, `_RoleBadge*` and `_BoardTypeBadge*`, so the next slices' partials must be listed.
- `AreaSheets` also holds the three board-deletion sheets (clean already) and `PendingSheets` lists the five unlayered phone sheets that still exist (the plan counted six; `admin-quests.mobile.css` is the sixth and is retired).
- The tracer feedback gate: auto mode was off in the workflow configuration but the session ran autonomously, so the tracer's automated verify was re-run end to end (242 passed) and the plan continued instead of returning an interactive checkpoint.

## Known Stubs

None.

## Threat Flags

None. No new endpoint, auth path or schema; the description and title stay Razor-encoded (no `Html.Raw`), and the two new shared classes are new names only, so the computed-style check on Calendar and Quest pages shows nothing moved (T-100-46 to T-100-48).

## Self-Check: PASSED

- Files: AdminPlatformDisplay.cs, QuestStatusBadgeModel.cs, _QuestStatusBadge.cshtml, admin-pages.css, admin-pages.mobile.css, AdminPlatformRenderTests.cs, AdminPlatformDisplayTests.cs found; `admin-quests.mobile.css` absent.
- Commits: 7460f130, 55260f24 and 5e992db5 found in the log.
