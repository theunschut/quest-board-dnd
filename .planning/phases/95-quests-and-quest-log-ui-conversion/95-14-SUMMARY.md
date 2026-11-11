---
phase: 95-quests-and-quest-log-ui-conversion
plan: 14
subsystem: ui-quest-log
tags: [quest-log, foundation, pages-layer, view-merge, stretched-link, contrast, tracer]
requires:
  - phase: 95-13
    provides: manifest rows, allow-file chain, per-User-Agent readability compare
  - phase: 95-10
    provides: Edit merge, the named EditRecap contrast carry-over
provides:
  - Quest Log index on the foundation on both layouts, one stretched link per card with the ring on the whole card
  - QuestLog/Details as one view serving both layouts (phone twin and its sheet deleted)
  - QuestLog/EditRecap as one view serving both layouts (phone twin and quest-edit.mobile.css deleted)
  - quest-log.css and quest-log.mobile.css as @layer pages sheets on tokens only
  - zero text-contrast failures on every Quest Log state, both layouts, with no exception entry
affects: [closure plan: quests.css now holds only the layered poster block and the .quest-card / .quest-status-* / .cr-badge remnant]
tech-stack:
  added: []
  patterns: [stretched row-nav-link with :has(:focus-visible) ring on the card, one view for both layouts with the Bootstrap grid stacking and phone tweaks under .mobile-layout, written text on a scrim box, help and tagline text taking the card colour on a page scope]
key-files:
  created:
    - QuestBoard.Service/wwwroot/css/quest-log.css
  modified:
    - QuestBoard.Service/Views/QuestLog/Index.cshtml
    - QuestBoard.Service/Views/QuestLog/Index.Mobile.cshtml
    - QuestBoard.Service/Views/QuestLog/Details.cshtml
    - QuestBoard.Service/Views/QuestLog/EditRecap.cshtml
    - QuestBoard.Service/wwwroot/css/quest-log.mobile.css
    - QuestBoard.Service/wwwroot/css/quests.css
    - QuestBoard.Service/wwwroot/css/legacy-pages.css
    - QuestBoard.Service/wwwroot/css/quest-details.css
    - QuestBoard.IntegrationTests/Mobile/MobileViewsTests.cs
    - QuestBoard.IntegrationTests/Tests/QuestsConversionGuardTests.cs
  deleted:
    - QuestBoard.Service/Views/QuestLog/Details.Mobile.cshtml
    - QuestBoard.Service/Views/QuestLog/EditRecap.Mobile.cshtml
    - QuestBoard.Service/wwwroot/css/quest-log-detail.mobile.css
    - QuestBoard.Service/wwwroot/css/quest-edit.mobile.css
key-decisions:
  - "Quest Log Details and EditRecap merged (approved at the first sign-off, no new checkpoint). Each is one view for both layouts; phone-only rules sit under .mobile-layout, never a media query."
  - "The Add/Edit Recap button moved from the Session Recap section into Quick Actions, as the plan specifies; the Razor condition (ViewBag.CanEditRecap) and the link target are unchanged."
  - "Side-column cards on Details use an h2 inside the card body, not a card header, so the display step stays reserved for the page title."
  - "The tagline, the empty-state sentence and the editor help lines take the card colour (--qb-fg) on the Quest Log scope: the muted tone measures 4.25:1 on one glass card."
  - "quest-edit.mobile.css deleted: Edit.Mobile.cshtml went in the Edit merge and EditRecap.Mobile.cshtml went here, so nothing links it."
requirements-completed: [D-16, D-21, D-24, DoC-1, DoC-2, DoC-3, DoC-4, DoC-5, DoC-8, DoC-9]
duration: ~2 h
completed: 2026-11-11
status: complete
actuals:
  tokens: 52000
  tasks: 3
  commits: 3
---

# Phase 95 Plan 14: Quest Log on the foundation, Details and EditRecap merged Summary

The Quest Log index is converted on both layouts with one real link per card and a ring around the whole card on keyboard focus, QuestLog/Details and QuestLog/EditRecap are each one view serving both layouts, the Quest Log rules left the unlayered sheets for `@layer pages`, and every Quest Log state measures zero text-contrast failures with nothing excepted.

## What changed

**Task 1, index (`a1819eeb`, tracer).**
- Desktop: a page-header card (`h1` Quest Log, the tagline verbatim in `.header-subtitle`), then a grid of `.modern-card.modern-card--interactive` cards (no backdrop filter). Each card has exactly one `<a class="row-nav-link" href=".../QuestLog/Details/{id}">` around the `h2` title, the CR `.badge` (not on Campaign boards), `DM:` with the name or `Unknown DM`, `Completed:` through `Html.WallClock` (finalized) or `Html.LocalTime` (closed) or `Unknown Date`, the desktop-only Adventurers count, the description preview, the `Session Recap Available` badge (`fa-scroll`) and the desktop-only "Click to view details" cue (recorded as deliberate, Q-36 and Q-37). No `onclick`.
- Phone: the same cards in one column keeping `quest-log-item`, the same labels, no Adventurers count and no cue.
- Empty state on both layouts: the foundation `.empty-state` with `fa-book-open`, "No Completed Quests Yet" and today's sentence.
- `quest-log.css` (one `@layer pages` block, tokens only) holds the grid, the stretched `.row-nav-link::after`, the `:has(.row-nav-link:focus-visible)` card ring and the meta sizes. `quest-log.mobile.css` is rewritten into `@layer pages` with the same link and ring, keeping its link from the phone view.
- Guard: both views in `ConvertedViews` and `ViewSheetLinks`, both sheets in `ConvertedSheets`; new facts `UnlayeredSheets_DoNotStyleTheQuestLog` and `QuestLogIndex_EachCardHasOneStretchedLinkAndNoClickHandler`.

**Task 2, Details merge (`d5966944`).**
- One `Details.cshtml`: the main `.modern-card` has the quest title as `h1` in its header (CR badge beside it); `col-md-8` holds Quest Information, Original Quest Description, Rewards, Adventurers and Session Recap (each an `h2`, separated by `<hr>`); `col-md-4` holds Quick Actions (`Back to Quest Log` `btn-secondary`, `Manage Quest` `btn-info` for the DM, `Add/Edit Recap` `btn-primary`, all `w-100`) and Quest Statistics. The grid stacks on the phone with no media query.
- Conditions are copied unchanged (`CanManageQuest`, `CanEditRecap`, Campaign, finalized/closed dates); dates keep their `Html.WallClock` and `Html.LocalTime` calls. Portraits are `avatar-32` with the hidden placeholder and `avatar-fallback.js`; a signup with no character renders the name only (the old "()" is gone).
- Deleted `Details.Mobile.cshtml` and `quest-log-detail.mobile.css`. `GetMobilePage_QuestLogDetails_ReturnsSuccessAndMobileLayout` rewritten to the same intent: under the phone User-Agent the page answers 200 with the `mobile-layout` body class, the quest title and the Session Recap section, links `quest-log.css` and does not link the deleted sheet.

**Task 3, EditRecap merge (`b84f339c`).**
- One `EditRecap.cshtml`: `.modern-card` with an `h1` `Edit Recap: {title}`, the `_MarkdownEditor` partial (label `Recap`, placeholder unchanged), the page's own `.form-text` sentence after the editor hint, `<hr>`, `Cancel` (`btn-secondary`, `fa-times`, to the log Details) left and `Save Recap` (`btn-primary`, `fa-save`) right; `col-lg-8` on large screens, full width below; on the phone the two buttons share the row and are at least 44px. `_QuestFormScripts` stays in Scripts.
- Deleted `EditRecap.Mobile.cshtml` and `quest-edit.mobile.css`. **`quest-edit.mobile.css` was deleted because `Edit.Mobile.cshtml` no longer exists (removed in the Edit merge) and `EditRecap.Mobile.cshtml` was its last consumer; a repo-wide search finds no other reference.**
- New fact `QuestLogMergedViews_LeaveNoPhoneTwinOrPhoneSheet` fails if any of the four retired files returns.

## Deleted files

`QuestLog/Details.Mobile.cshtml`, `QuestLog/EditRecap.Mobile.cshtml`, `wwwroot/css/quest-log-detail.mobile.css`, `wwwroot/css/quest-edit.mobile.css`.

## Removed unlayered rules

- `quests.css` (unlayered part, 587 lines out): the whole Quest Log index block (container, header, grid, card, card header/body/footer, meta, shimmer and glow, empty state, the "override legacy" block at the end), the Quest Log Details block (`.quest-log-details*`), `.quest-description-box`, `.recap-display-box`, `.player-badge`, and the `.quest-signup-*` / `.character-mini-avatar*` block with its 576px query. The signup block was kept by 95-07 for Quest Log and Manage; Manage no longer uses it (95-13 moved it to `avatar-32`), and its Quest Log use ended here, so all of it went. What stays unlayered: `.quest-card`, `.quest-status-*`, `.player-list`, `.cr-badge` (still used by `Quest/_QuestCard.cshtml`), the `.quest-dm`/`.quest-date` family and the small-screen `.quest-description` / `.quest-title` queries, all for the closure plan.
- `legacy-pages.css` (53 lines out): the "Quest Log Styles" block (container, header, grid, card). The dead `.vote-btn` stays for the closure plan.
- `quest-log-detail.mobile.css` (120 lines), `quest-edit.mobile.css` (43 lines) and the old `quest-log.mobile.css` unlayered rules (rewritten, not kept).
- The guard fact fails if `legacy-pages.css` or `quests.css` style a `.quest-log-*`, `.empty-quest-log`, `.recap-badge` or `.view-details-text` selector again.

## Contrast (text contrast failures, measured over the lightest board pixel)

| Row | BEFORE desktop | BEFORE phone (390 / 412) | AFTER (both User-Agents, all widths) |
|-----|----------------|--------------------------|--------------------------------------|
| log-index | 0 | 5 (`small.text-muted` meta lines, recap badge, dates; worst 3.29:1) | 0 |
| log-empty | 0 | 0 | 0 |
| log-details-recap | 1 (`p.text-muted`, 4.25:1) | 15 (meta lines, bold labels, dates; worst 3.94:1) | 0 |
| log-details-norecap | 2 (`p.text-muted`, 4.25:1) | 16 (same, plus the "No recap" text) | 0 |
| log-editrecap | 1 (`small.form-text`, 4.25:1) | 2 (`label.form-label` and `small.form-text`, 2.99:1) | 0 |

**The EditRecap carry-over is cleared.** The two `log-editrecap` / `div#recap-help` entries (desktop and phone) were removed from `contrast-exceptions.json`. The file was re-read immediately before writing (the orchestrator had added a Poster5 floor entry), a JSON round trip reproduced the file byte for byte, and exactly those two entries went: 14 entries became 12 (five Quest Details calendar-day-header entries and seven poster-glyph-floor entries, including the new Poster5 320px one, all untouched). After the removal `readability-compare --zero-contrast` over `log-index, log-empty, log-details-recap, log-details-norecap, log-editrecap`, both User-Agents, every width: 20 records, 0 problems against BEFORE, 0 residual failures, 0 poster-glyph violations, `ok`. The 95-14 `allow.json` is `{}`: no relabel or merged-away marker needed an allowance.

Fixes that got there: the tagline and the empty-state sentence (muted tone 4.25:1) and the editor help lines now take `--qb-fg` on the Quest Log scope; the description, rewards and recap sit on a scrim box so their colour never depends on the glass; the old phone meta lines, bold labels and dates (3.29 to 3.94:1) are gone with the old sheets.

## Verification

- `dotnet build`: 0 errors. `dotnet test` (full): unit 1312 passed; integration 1794 passed, 11 skipped (pre-existing), 0 failed. One earlier full run ended with `Internal CLR error (0x80131506)` (a known flake) and passed on the single re-run. Task 1 and Task 2 filter (`QuestLogControllerIntegrationTests`, `MobileViewsTests`, `WallClockUnmovedTests`, `QuestsConversionGuardTests`): 92 passed. Task 3 filter (plus `MarkdownEditorMarkupTests`): 95 passed. `QuestLogControllerIntegrationTests` and the Quest Log phone facts pass unchanged except the one rewritten fact; no test was deleted.
- Tracer gate (no human present): the index sweep, readability compare and tests were re-run on the committed code path before Task 2 and passed, so the run continued.
- Sweep `after/95-14`: `log-index`, `log-empty`, `log-details-recap`, `log-details-norecap`, `log-editrecap`; desktop 1440 and 320, phone 412 and 390; 20 records, all 200, no horizontal overflow (the desktop index at 320 had it BEFORE; it is gone), no new small target (the seven 44px-short editor toolbar buttons on the phone are as BEFORE), no console error, no missing focus ring, no poster violation.
- Editor Tab probe on EditRecap under both User-Agents: before, Tab never left the editor and it had no label or description; now focus leaves, the editor is named `Recap` and described by `recap-help` (better than BEFORE on all four records).
- Recap save: scratch `recap-save-probe.mjs` submitted the real EditRecap form as the DM on the Audit Board fixture under both User-Agents with the recap text it already held (stored text unchanged); each landed on `/QuestLog/Details/992003` (200), with the recap shown and no page error.
- Frames: `~/qb-ui-review/phase-95/after/95-14/{desktop,mobile}/`.

## Deviations from Plan

**1. [Rule 1 - Bug] Phone overflow of the merged Details and EditRecap.** The first merged phone captures overflowed by 4px: the Bootstrap `.row` gutter margin (-12px) is wider than the phone container's own 8px padding. `.mobile-layout :is(.quest-log-detail-page, .quest-log-edit-page) .row` now sets `--bs-gutter-x: 0`. Fixed before the Task 2 commit.

**2. [Rule 1 - Bug] Rewards box on Quest Details.** `.quest-description-box` is also used by the Rewards block in `Quest/Details.cshtml`, so deleting it from the unlayered `quests.css` would have left that block unstyled. A layered rule (scrim, border, radius, padding) was added to `quest-details.css`, which that view already links. Not in the plan's file list; no Quest Details test or sweep row changed.

**3. [Rule 3 - Blocking] Scratch tooling.** `tools/probes.mjs` now treats `.quest-log-card` and `.quest-log-item` as focus-ring hosts, like the poster and phone quest card (a stretched link draws no ring of its own); without it the compare reported the stretched titles as ringless. Added `tools/recap-save-probe.mjs`. No manifest change was needed (the five Quest Log rows already existed).

**4. [Judgement] Add/Edit Recap sits in Quick Actions.** It used to sit under the recap text. The plan places it in Quick Actions; the link target and the Razor condition are unchanged and `QuestLogControllerIntegrationTests` pins the link.

**5. [Judgement] Portrait images have `alt=""`.** The character name is written next to the image, so the portrait is decorative.

**6. [Judgement] Unused antiforgery token dropped from Details.** The old views fetched and stored a token they never used; the merged view does not.

**7. [Judgement] Stale foundation rule left.** `components.css` still holds a `.quest-log-details { --bs-white-rgb }` rule for an element that no longer exists. `components.css` is outside this plan's files, so it is left for the closure plan.

## Notes

- WINDOWS #30 waived by user decision (c + a), 2026-11-11. The Poster5 contrast breach at the 304px card is accepted as part of the Gate 1 item 2 heavier-ink exception, held to an explicit 320px-window floor of 2.03 / 1.92 (today's 2.28 / 2.17 minus 0.25). The 3.10 / 1.90 floor still holds at every wider card. Commit 36f07669.
- The Quest Log "Click to view details" cue and the desktop Adventurers count stay desktop-only, as the authoritative twin answers (Q-36, Q-37) say; Q-38 added "Unknown DM" to the desktop.

## Auth gates

None.

## Known Stubs

None.

## Threat Flags

None new. T-95-34: the Manage Quest and Add/Edit Recap conditions are copied unchanged (`ViewBag.CanManageQuest`, `ViewBag.CanEditRecap`); EditRecap authorises server-side; `QuestLogControllerIntegrationTests` (recap link for a non-owner Admin, EditRecap 200 for Player and Admin, 404 for a quest that is not completed, and the non-owner Admin POST) pass unchanged. T-95-35: `Html.Markdown` output and its sanitising are untouched.

## State left behind

- Working-tree app running on `http://localhost:8000` on this build; port 8001 untouched; WINDOWS.md #30 left alone.
- The recap probe re-saved the Audit Board fixture's own recap text unchanged; no other data was changed.

## Self-Check: PASSED

Verified: `quest-log.css` exists and opens `@layer pages`; `quest-log.mobile.css` opens `@layer pages`; `Details.Mobile.cshtml`, `EditRecap.Mobile.cshtml`, `quest-log-detail.mobile.css` and `quest-edit.mobile.css` are gone; `Details.cshtml` contains `col-md-8` and `avatar-32`; both index views contain `class="row-nav-link"` and `empty-state`; `EditRecap.cshtml` contains `Save Recap`, `btn-primary` and `Cancel`; commits `a1819eeb`, `d5966944` and `b84f339c` exist; full build and test green; `readability-compare` with zero-contrast over the five Quest Log rows printed `ok` with the two EditRecap exceptions removed.
