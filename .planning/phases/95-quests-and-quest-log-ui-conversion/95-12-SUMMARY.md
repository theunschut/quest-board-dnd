---
phase: 95-quests-and-quest-log-ui-conversion
plan: 12
subsystem: ui-quest-manage-scripts
tags: [quest-manage, es-module, data-attributes, no-inline-code, wall-clock, avatar]
requires:
  - phase: 95-07
    provides: quest-details.js action pattern (delegated data-action, antiforgery meta), avatar-fallback.js and the avatar-32 classes
  - phase: 95-11
    provides: shared files in their current state (quest-form.css, conversion guard lists)
provides:
  - quest-manage.js, one module for both Manage layouts (delete quest, remove player, reload, Open Quest confirm, availability status, player count)
  - Manage.cshtml and Manage.Mobile.cshtml with no inline style, script or handler
  - proposed and finalized dates on Manage through Html.WallClock with the year on both layouts
  - conversion guard entries for both Manage views and the module
affects: [95-13 Manage layout and contrast]
tech-stack:
  added: []
  patterns: [delegated click and submit listeners keyed on data-action and data-confirm-message, player limit read from data-max-players on the Finalize form, status span built with createElement and textContent]
key-files:
  created:
    - QuestBoard.Service/wwwroot/js/quest-manage.js
  modified:
    - QuestBoard.Service/Views/Quest/Manage.cshtml
    - QuestBoard.Service/Views/Quest/Manage.Mobile.cshtml
    - QuestBoard.IntegrationTests/Controllers/QuestCampaignUiParityTests.cs
    - QuestBoard.IntegrationTests/Controllers/WallClockUnmovedTests.cs
    - QuestBoard.IntegrationTests/Tests/QuestsConversionGuardTests.cs
key-decisions:
  - "Delete controls are button type=button (they were anchors with an empty href), same classes and labels, carrying data-action, data-quest-id and data-confirm-message=Are you sure?"
  - "The Open Quest confirm is a delegated submit listener reading the submitter's data-confirm-message, so any later submit that names a sentence gets the same guard"
  - "The player limit moved from an interpolated number in script to data-max-players on the Finalize form"
  - "Confirm and alert dialogs stay native confirm() and alert() with today's wording; modal replacements remain not converted"
requirements-completed: [D-16, D-24, DoC-1, DoC-8, DoC-9]
duration: ~1 h
completed: 2026-11-11
status: complete
actuals:
  tokens: 11300
  tasks: 2
  commits: 2
---

# Phase 95 Plan 12: Quest Manage scripts into one module Summary

Every Manage action on both layouts runs from `quest-manage.js` through data attributes, neither view carries inline code, a player's name reaches the remove confirmation only as plain text from `data-player-name`, and proposed and finalized dates show the year on the phone as well.

## What changed

**Task 1, tracer (`05bd16e5`).** The three Delete controls on each view became `<button type="button" ... data-action="delete-quest" data-quest-id="@Model.Id" data-confirm-message="Are you sure?">`. Each view's `Scripts` section emits `<meta name="request-verification-token">` and loads `quest-manage.js` (an ES module with one delegated click listener). The module sends the same `DELETE /Quest/Delete/{id}` with the `RequestVerificationToken` header read from the meta and redirects to the origin as before; the inline `deleteQuest` is gone from both views. The two `QuestCampaignUiParityTests` assertions on the inline call now assert `data-action="delete-quest"` and `data-quest-id="{quest.Id}"` (the desktop fact was renamed `CampaignManage_Desktop_RendersDeleteControlWiredToThisQuest`); fact count is unchanged.

**Task 2 (`a6af459b`).**
- Remove buttons (player, assistant, spectator, participant on desktop; player, assistant, spectator on the phone) carry `data-action="remove-player"`, `data-signup-id` and a Razor-encoded `data-player-name`. The module builds today's sentence with the name as text and sends the same `DELETE /Quest/RemovePlayerSignup/{id}` with the same header, reload and error alerts.
- Refresh Data buttons (3 per view) carry `data-action="reload"`.
- Open Quest: the inline `return confirm(...)` is replaced by `data-confirm-message` with the same sentence verbatim; the module cancels the submit unless confirmed.
- `updatePlayerAvailability` and the player-count listener are ported with the same behaviour. The status is a span made with `createElement`, a class and `textContent` (`text-success`, `text-warning`, `text-danger`, `text-muted`, same words). The `.date-selector`, `.player-item`, `.player-checkbox`, `.availability-status`, `.date-option` and `.manage-date-option` hooks are untouched.
- Both inline scripts are deleted. The `display: inline` forms use `d-inline`; the two avatar blocks use `avatar-32`, a hidden `avatar-32 avatar-32--placeholder` sibling and `avatar-fallback.js` (loaded from the desktop Scripts section; the phone view has no avatars).
- Dates: finalized and proposed dates `Html.WallClock(value, "date-time")` on both layouts, the desktop Best Date `"date-time-compact"`. Instants stay `Html.LocalTime`. DM Controls class strings, the disabled Move control and `ViewBag.BoardToday` are untouched.
- Guard: both Manage views are in `ConvertedViews`, `quest-manage.js` in `PhaseSourceFiles`, and a new fact `QuestManageModule_WritesNoMarkupFromStrings` fails on any `innerHTML`, `outerHTML`, `insertAdjacentHTML` or `document.write` in the module.

## Verification

- `dotnet build`: 0 errors. `dotnet test` (full): unit 1312 passed; integration 1783 passed, 11 skipped (pre-existing), 0 failed. Task 1 filter: 67 passed. Task 2 filter: 126 passed (after the `WallClockUnmovedTests` rewrite below). `AmbientClockSeamTests`: 65 passed.
- Greps: `innerHTML` in `quest-manage.js` = 0. `data-player-name` = 4 desktop, 3 phone. `Html.WallClock` = 3 desktop, 2 phone. No `onclick`, `<script>` without src, `style=` or `onerror` left in either view.
- Live delete probe (`tools/delete-probe.mjs`, as `dm`, Audit Board, throwaway quests created through the Create form): under the desktop User-Agent (quest 992032) and the phone User-Agent with `mobile-layout` (quest 992033), the control is a `BUTTON` with the quest id and "Are you sure?"; dismissing the confirm sent no DELETE and Manage still answered 200; accepting sent exactly one DELETE, the page landed on `/quests`, Manage answered 404 and the board no longer listed the title. Both rows are in `fixtures.json` as `quest.deleteProbe.desktop` and `quest.deleteProbe.mobile`, `created: true`, `deleted: true`.
- Availability and count (`tools/manage-probe.mjs`, open quest `quest.signedUp`, both User-Agents): selecting the three proposed dates in turn set the status to `- Available (Yes)` (`text-success`, ticked), `- Maybe available` (`text-warning`, ticked) and `- Not available (No)` (`text-danger`, unticked); the status is a span child of `.availability-status`. Ticking past the limit (the limit rewritten to 0 in a probe-only response, nothing saved, `tools/manage-probe2.mjs`) alerted "Cannot select more than 0 players for this quest." and left the box unticked. The player limit of 5 is read from `data-max-players`.
- Remove player (`manage-probe2.mjs`, as `admin`, DELETE intercepted): the confirm read "Are you sure you want to remove Audit Player from this quest? This will delete their signup and all associated votes."; dismiss sent nothing; accept sent `DELETE /Quest/RemovePlayerSignup/16160` with the antiforgery header present, on both User-Agents. No signup was removed.
- Open Quest (`manage-probe.mjs`, finalized quest, POST intercepted): the confirm carries today's sentence; dismiss posted nothing, accept posted once, on both User-Agents.
- Sweep `~/qb-ui-review/phase-95/after/95-12` (`manage-open`, `manage-finalized`, `manage-campaign`; desktop 1440 and 320, phone 390 and 412; 12 captures, all 200). `readability-compare` against BEFORE with `after/95-12/allow.json`: 0 problems. No new overflow, small target, console error or failed request. The desktop console shows one 404 on the finalized page, the character picture request that the avatar module turns into the placeholder (the fixture has no portrait); the compare did not flag it against BEFORE.
- Frame check: `after/95-12/desktop/manage-finalized.png` shows the placeholder avatar, the Open Quest button beside Edit Quest, and "26 Nov 2026, 19:00" with the year.

## Contrast carry-over (named)

Five desktop and phone Manage rows still have the failures BEFORE had, with identical ratios and colours; none was introduced here, and the date strings are the only ones whose key changed:
- `time.wall-clock` proposed and finalized dates on `manage-open` (4.19:1 desktop, 4.34:1 phone, 4 dates) and `manage-finalized` phone (4.34:1, 1 date). BEFORE had the same dates as `strong` with the old format at the same ratios, from the unlayered `.date-option` / `.manage-date-option` surface in `legacy-pages.css`, which this plan does not touch.
- The other carried ones (`strong` Yes/Maybe/No vote labels at 3.19 to 3.63:1, `small.text-success` 3.56:1, gold Description toggle 2.78:1, `small` 2.99:1, `span.text-muted` separator 3.94:1, outline Refresh Data 1.17:1) are unchanged.

All are in the next plan's zero-contrast scope (95-13 moves the date-option and vote-summary rules out of `legacy-pages.css` and `quests.css` and requires zero text-contrast failures on every Manage state). No contrast exception was added or changed.

## Deviations from Plan

**1. [Rule 1 - Bug] Existing facts pinned the old Manage date format.** `WallClockUnmovedTests.ProposedDateOnManagePage_RendersIdenticalWallClockText_AcrossTwoDifferentBoardZones` expected "dddd, MMMM dd, yyyy 'at' h:mm tt". Rewritten to the same intent (the proposed date reads identically under two board zones) with the `date-time` style text, as the finalized-date fact already does. Not in the plan's file list; no fact was deleted.

**2. [Rule 2 - Missing critical] Null guard in the availability loop.** The inline version dereferenced `statusSpan` and `checkbox` on every `.player-item`, including the spectator rows that have neither, which would throw after the last real player. The module skips an item with no checkbox or status. Behaviour for real players is unchanged.

**3. [Rule 2 - Missing critical] Guard fact for the module.** The plan relied on "the guard and the SUMMARY grep" for the innerHTML threat (T-95-30) but no scan of the module existed; `QuestManageModule_WritesNoMarkupFromStrings` was added so the mitigation is enforced, not only recorded.

**4. [Rule 3 - Blocking] Scratch tooling.** `readability-compare.py` gained an allow-file key `contrastCarried` (`[[selector, sample], ...]`) for a failure BEFORE already had on the same element whose text sample changed only because the date was re-rendered; without it a re-rendered date can never match its BEFORE key. It lists only the five date strings above. New scratch scripts: `tools/delete-probe.mjs`, `tools/manage-probe.mjs`, `tools/manage-probe2.mjs`. `after/95-12/allow.json` renames the Delete anchors (`a||||#|Delete`, `a||||#|Delete Quest`) to their buttons.

**5. [Judgement] Delete control element.** Anchors with `href="#"` became buttons with the same classes, as the plan allows; they still read as "Delete" / "Delete Quest", unchanged labels, and the plan's later Delete Quest wording change is 95-13's.

**6. [Judgement] Weekday dropped from Manage dates.** `date-time` is "MMM d, yyyy, h:mm tt" (no weekday, no "at"), as the plan specifies; the desktop Manage date therefore reads "26 Nov 2026, 19:00" in the sweep's locale rendering.

## Auth gates

None.

## Known Stubs

None.

## Threat Flags

None new. T-95-29: the name is a Razor-encoded `data-player-name` read as text; the three inline handlers that embedded it in a script string are gone. T-95-30: the status is a created span with `textContent`; guard fact and grep (0) confirm. T-95-31: same endpoints, verbs and header; the token comes from a Razor-encoded meta; server-side antiforgery and DM/Admin authorisation untouched.

## Notes for later slices

- Not converted and left on native `confirm()` and `alert()`: Delete Quest, Remove player, Open Quest, the player-limit alert and the failure alerts.
- `legacy-pages.css` and `quests.css` still hold the `.date-option` rules; 95-13 owns them.
- Working-tree app left running on `http://localhost:8000` on the build with these views. Port 8001 untouched.
- Two throwaway quests were created and deleted on the Audit Board; no other data changed.

## Self-Check: PASSED

Verified: `quest-manage.js` exists; commits `05bd16e5` and `a6af459b` exist; full `dotnet build` and `dotnet test` green; both views contain `data-action="delete-quest"`, `data-player-name` and `Html.WallClock`; `innerHTML` count in the module is 0; `readability-compare` on the three Manage rows with `after/95-12/allow.json` is ok; `fixtures.json` has both delete-probe rows.
