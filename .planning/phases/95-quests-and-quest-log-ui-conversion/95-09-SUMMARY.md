---
phase: 95-quests-and-quest-log-ui-conversion
plan: 09
subsystem: ui-quest-board-phone
tags: [quest-board, phone, stretched-link, empty-state, layered-css, contrast, tap-target]
requires:
  - phase: 95-08
    provides: poster wording (DM:, Quest Date:, Selected Adventurers:, Adventurers signed up:), QuestBoardEmptyStateTests, stretched-link ring pattern
  - phase: 95-03
    provides: Gate 1 answers (item 9: phone empty heading stays "No Quests Available")
provides:
  - phone Quest/Index.Mobile on the foundation, each card one stretched real link, no inline handler
  - adventurer count line and labelled DM and date lines on the phone card, same words as the poster
  - foundation empty state on the phone board with today's copy
  - home.mobile.css rewritten into the pages layer on tokens only
affects: [95-12 closure selector scan, Gate 2 sheet]
tech-stack:
  added: []
  patterns: [anchor itself min-height 44px so the stretched link measures as a real target, ring drawn on the card via :has(), probe accepts an outline on a phone card host]
key-files:
  created: []
  modified:
    - QuestBoard.Service/Views/Quest/Index.Mobile.cshtml
    - QuestBoard.Service/wwwroot/css/home.mobile.css
    - QuestBoard.IntegrationTests/Mobile/MobileViewsTests.cs
    - QuestBoard.IntegrationTests/Tests/QuestBoardEmptyStateTests.cs
    - QuestBoard.IntegrationTests/Tests/QuestsConversionGuardTests.cs
key-decisions:
  - "Status and Signed up badges keep bg-success, bg-primary and bg-secondary: their foundation fills held 4.5:1 in the sweep, so no assertion was rewritten"
  - "Title anchor is display:flex with min-height var(--qb-hit-min): a stretched pseudo-element is not part of the anchor's own box, so without it a tool measuring the link still saw a 27px target"
  - "CR badge is a flex item of the meta column (own line, natural width) rather than inside a paragraph"
  - "Gate 1 item 9 honoured: phone empty heading stays No Quests Available"
requirements-completed: [D-06, D-09, D-16, D-24, DoC-1, DoC-2, DoC-3, DoC-4, DoC-5, DoC-7, DoC-8]
duration: ~1 h
completed: 2026-11-11
status: complete
actuals:
  tokens: 4600
  tasks: 2
  commits: 3
---

# Phase 95 Plan 09: Phone quest board on the foundation Summary

The phone board keeps its glass cards and text status badges, but each card is now one real link stretched over the whole card (no inline handler), carries the poster's DM, date and adventurer-count lines, rings as a whole card on keyboard focus, and the sheet is in `@layer pages` on tokens only. Zero text-contrast failures remain on any board row under either User-Agent (27 per crowded phone row before).

## What changed

**Card markup (`704c4e1d`, `ac7c903f`).** `quest-card-mobile modern-card modern-card--interactive` (no backdrop filter on a repeated item); the `onclick` that interpolated a URL into script is gone; the title is an `h2` holding exactly `<a class="row-nav-link" href="@navUrl">`; a visually hidden `h1` "Quest Board". Lines: `DM:` (crown `aria-hidden`, `Unknown DM` when missing or blank), `Quest Date:` with `Html.WallClock(..., "date-time")` and `Selected Adventurers: N` on a finalized quest with a date, `Adventurers signed up: N` otherwise, none of the count lines on a Campaign board, CR badge as before (hidden on Campaign). Status and Signed up badges unchanged in words and fills, their icons now `aria-hidden`. Every `ViewBag.BoardToday` read kept.

**Sheet.** `home.mobile.css` is one `@layer pages` block, no colour literal, no important flag: the card as positioning context with `min-height: var(--qb-hit-min)`, the `::after` stretched link, the ring on `.quest-card-mobile:has(.row-nav-link:focus-visible)` with the focus tokens (the link itself shows none), the title anchor at least 44px tall, label and meta sizes from the token steps. The old unlayered glass, text-shadow, colour literals and `!important` rules are gone; nothing unlayered reaches the cards (`mobile.css` holds only container padding and the Move reason rule).

**Empty state (Task 2).** `<div class="empty-state">` with `fas fa-scroll empty-state-icon`, `h2.empty-state-heading` "No Quests Available" (Gate 1 item 9: keep) and the unchanged sentence. The empty state went in with the markup in the first commit, so Task 2's commit is the facts and guard entries.

**Facts.** New `MobileHome_MobileUserAgent_QuestCardShowsAdventurerCount` (open quest with one signup shows `Adventurers signed up:</strong> 1`, `DM:` line, no `onclick`; a quest on a seeded Campaign group shows the DM line and no count line). New `PhoneBoard_WithNoQuests_RendersTheVisibleEmptyState` in `QuestBoardEmptyStateTests`. `Quest/Index.Mobile.cshtml` registered in `ConvertedViews`, `ViewSheetLinks` (`home.mobile.css`) and `PhaseSourceFiles`; `home.mobile.css` in `ConvertedSheets` and `PhaseSourceFiles`. Every pre-existing phone board fact passes unchanged.

## Measured (sweep `~/qb-ui-review/phase-95/after/95-09`, mobile UA, 390 and 412)

| Measure | BEFORE | AFTER |
|---------|--------|-------|
| Card heights, `board-crowded` / `board-dm` (26 cards) | not recorded | 167 to 363px at 390; 167 to 339px at 412 |
| Card height, `board-campaign` (1 card) | not recorded | 111px at 390 and 412 |
| Title anchor height | 27px (5 small targets at 390, 7 at 412) | 44px minimum (48px on Campaign); `smallTargets` empty on all 8 phone records |
| Text-contrast failures, `board-crowded` / `board-dm` | 27 per record (`small.text-muted`, the DM line, 3.94:1 measured) | 0 |
| Text-contrast failures, `board-campaign` | 1 | 0 |
| `board-empty` | 0 | 0 |
| Horizontal overflow, console errors, failed requests | none | none (scroll width equals window width at 390 and 412) |
| `onclick` attributes on the page | one per card | 0 |

Keyboard focus (Tab onto the first card link, 500ms after, so the box-shadow transition has settled): the card draws `outline: solid 2px` dark ink plus a `0 0 0 2px` gold shadow; the link shows `outline: none`; the evidence frame `evidence/board-crowded-focus.png` shows the ring around the whole card. `elementFromPoint` at the bottom-right corner of the first card resolves to the link on every row, so the whole card is the tap target.

`readability-compare` (phone rows, `--zero-contrast` over all four board rows with `--exceptions`): `ok`, 0 problems, 0 residual contrast, 0 poster-glyph violations.

### Fixture drift versus real regressions

BEFORE phone records predate the follow-up fixtures from earlier plans: the board now also lists `992030` "The Ember Gate Assize" and `992031` its follow-up, which BEFORE never had. They are new controls, not lost ones, and the compare ignores additions. The one thing that did change in the compare was real and expected: the `main` landmark's accessible name now begins with the added count and label text (and, on the empty row, with the hidden `h1`), so those four `main` keys went into `after/95-09/allow-phone.json` as removed. Nothing else needed an allow entry. No real regression was found on the phone.

Two comparator issues were handled as tooling, not as regressions. The 95-08 `allow.json` renames desktop link keys, and the comparator applies one allow file per row id to both User-Agents, so applying it to the phone rows reported 10 controls "lost" that were not. The sweep was split per User-Agent (`after/95-09-desktop`, `after/95-09-phone`, against `before-desktop-board` and `before-phone-board`), each with its own allow file.

### Desktop rows (not changed by this plan, checked for cascade)

Desktop board rows compared against `before-desktop-board` with the 95-08 allow file: 0 problems, 0 residual contrast. The only failure is the two Poster5 floor violations at the 320 window (`board-crowded`, `board-dm`), already open as WINDOWS.md #30 for the operator and left alone.

## Verification

`dotnet build` 0 errors. `dotnet test` (full): unit 1312 passed; integration 1782 passed, 11 skipped (pre-existing), 0 failed. `AmbientClockSeamTests` pass. Working-tree app restarted on `http://localhost:8000` with this build and left running; port 8001 untouched.

## Deviations from Plan

**1. [Rule 1 - Bug] The stretched link still measured as a 27px target.** A `::after` pseudo-element is not part of the anchor's own bounding box, so the sweep's small-target probe (and any tool measuring the link) still saw one text line. The anchor is now `display: flex; min-height: var(--qb-hit-min)` so the link itself is a real 44px target as well as covering the card. Cards get about 17px taller on one-line titles as a result.

**2. [Rule 3 - Blocking] Scratch focus probe extended** (`tools/probes.mjs`): a link inside a `.quest-card-mobile` counts as ringed only when the card has an outline (the card has a resting shadow, so a box-shadow test would pass trivially). Same shape as the poster host check from the desktop plan.

**3. [Judgement] Test seeds a Campaign group.** The board type is read from the active group's stored row, not from the test group context's `BoardType` alone, so the Campaign half of the new fact seeds a Campaign group (id 2), switches the active group to it, and restores group 1 and OneShot in `finally`.

**4. [Judgement] CR badge placement.** Moved out of a paragraph into the meta column as a flex item, because the paragraph's line box left uneven space under the badge.

**5. [Judgement] Task 2 commit holds tests only.** The empty-state markup was part of the card rewrite (same file, one coherent view), so the Task 2 commit carries the phone fact and guard entries; the sweep and measurement for Task 2 ran on the committed code.

## Auth gates

None.

## Known Stubs

None.

## Threat Flags

None. The link target rule (Manage for the quest's own DM, Details otherwise) is unchanged and pinned by `RowNavigationAccessibilityTests`; titles and DM names go through Razor encoding only; the inline handler that interpolated a URL into script is removed.

## State left behind

Working-tree app on `http://localhost:8000`. Scratch: `after/95-09` (sweep, evidence frames, `measure.json`, `allow.json`, `allow-phone.json`), `after/95-09-desktop`, `after/95-09-phone`, `before-phone-board`, `tools/measure-phone-board.mjs`.

## Self-Check: PASSED

Verified: `Index.Mobile.cshtml`, `home.mobile.css` and the three test files exist and are modified; commits `704c4e1d`, `42230b82` and `ac7c903f` exist; full `dotnet test` green; `home.mobile.css` opens with `@layer pages {` and contains `.row-nav-link::after`; `Index.Mobile.cshtml` contains `Adventurers signed up`, `Html.WallClock` and `empty-state-heading`.
