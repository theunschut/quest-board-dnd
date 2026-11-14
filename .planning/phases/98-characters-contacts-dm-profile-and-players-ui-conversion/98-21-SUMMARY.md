---
phase: 98-characters-contacts-dm-profile-and-players-ui-conversion
plan: 21
subsystem: players-list
tags: [ui-conversion, razor, stretched-link, twin-views, empty-states, guard-tests]
requires: [98-20]
provides:
  - "Players/Index (desktop) and Index.Mobile (phone) on the converted list contract: h1 Players page-title card, Dungeon Masters and Registered Players as h2 aside cards, paper tables in .table-responsive with 24px wood name links on the desktop, stretched-link member rows with an aria-hidden chevron on the phone, inert no-link rows kept"
  - "Both empty states in the shared .empty-state shape with an h2 on both layouts, proven by rendered tests on seeded boards and live on the audit boards"
  - "players.css (new, desktop link height) and players.mobile.css (rewritten) as one pages-layer block each, tokens only, no important flag"
  - "PlayersIndexRenderTests (13 facts, both layouts), CharactersTwinAnswersTests.Players_HasThePageHeading (both layouts), rewritten MobileViewsTests.GetMobilePage_PlayersIndex_ReturnsSuccessAndMobileLayout"
affects: [98-22, 98-23]
tech-stack:
  added: []
  patterns: [member-cards.css row reused for the phone Players rows; one view per layout with its own layered sheet from the Styles section; the desktop view links its small sheet and the phone view links the shared card sheet then its own]
key-files:
  created:
    - QuestBoard.IntegrationTests/Tests/PlayersIndexRenderTests.cs
    - QuestBoard.Service/wwwroot/css/players.css
  modified:
    - QuestBoard.Service/Views/Players/Index.cshtml
    - QuestBoard.Service/Views/Players/Index.Mobile.cshtml
    - QuestBoard.Service/wwwroot/css/players.mobile.css
    - QuestBoard.IntegrationTests/Tests/CharactersTwinAnswersTests.cs
    - QuestBoard.IntegrationTests/Mobile/MobileViewsTests.cs
    - QuestBoard.IntegrationTests/Tests/CharactersConversionGuardTests.cs
    - QuestBoard.IntegrationTests/Tests/UiFoundationLayerGuardTests.cs
decisions:
  - "Gate 1 headings.playersH1 is true with explicitYes true (the operator's explicit yes at item 10): both layouts gain the one h1 Players, so no contrast-exceptions decision entry was needed"
  - "Gate 1 emptyActionRole (btn-secondary) did not apply: neither Players empty state carries an action, as before"
  - "The approved drawings were ported as drawn, including the crown and users glyphs on the empty states (they were a dragon and a single user) and the section order and ids"
  - "A Dungeon Master on a board that does not serve the Dungeon Master page is plain text in the table cell on the desktop and an inert no-link row on the phone, exactly as the view decided before"
metrics:
  tasks: 3
  commits: 2
status: complete
actuals:
  tokens: 60000
  tasks: 3
  commits: 2
---

# Phase 98 Plan 21: Players list twins Summary

Both Players views are converted, which finishes the area's twin pairs: the Dungeon Master names are underlined wood links at least 24px tall on the desktop, the phone rows are 44px stretched-link rows (no inline handler, no inline colour), rows without a page stay inert, both empty states render in the shared shape, and the pair gains the page heading the operator approved.

## Outcome applied

From `gate1/answers.json`: `headings.playersH1` = true with `explicitYes` true, `headings.emptyActionRole` = `btn-secondary` (no Players empty state has an action, so it did not come into play), `lookCheck` approved v1. The approved drawings and drafts (`mockups/v1/look/players/`: `index.html`, `index.mobile.html`, `empty.html`, `empty.mobile.html`, `players.css`, `players.mobile.css`) were ported faithfully; `member-cards.css` is reused for the phone rows, not forked.

## Heading outcome

`playersH1` is true, quoted from the answers file: `{"playersH1": true, "explicitYes": true, "phoneHeadings": "as written", "emptyActionRole": "btn-secondary"}`. Both layouts carry exactly one `h1` reading `Players` in a page-title card; the two list titles stay `h2` section titles under it. `CharactersTwinAnswersTests.Players_HasThePageHeading` pins it under both User-Agents (one h1, the words, and the two h2 titles). The tracer verify's answers-file check printed `ok`. Because the heading was approved, `contrast-exceptions.json` `decisions` stays empty.

## Task 1 (tracer, commit `735680d8`): desktop end to end

- `Index.cshtml`: `players-page` wrapper; page-title card with `h1` Players; two `section.modern-card--aside` cards with `aria-labelledby` and an `h2` (icon `aria-hidden`), each holding `.table-responsive` with the striped paper table and a `Name` column header. A Dungeon Master name is `a.players-name-link` to the same `DungeonMaster/Profile` route; with the page off it is plain text; player names are plain cells as before. Empty states are `.empty-state` blocks with an `h2`, verbatim sentences. `@section Styles` links `players.css`.
- `players.css` (new): one `@layer pages` block, `.players-name-link` is `inline-block` with `min-height: var(--qb-space-lg)`. The link colour and underline come from the table's own link rule (wood on paper).
- Tests: `PlayersIndexRenderTests` desktop facts (structure and paper tables, own sheet only, links vs plain cells and long names whole, page off lists plain text with no anchor, both empty states on boards whose only member is the other kind) and the heading fact.
- Tracer gate: no auto flag is set and a sequential run cannot be resumed, so the gate was the automated re-run (117 passed) plus a live stack-mode capture of `players-index`, `players-index-empty` and `players-index-nodm` on 8310 (200, no console error, 0 desktop contrast failures); the desktop capture was viewed and matches the approved drawing.

## Task 2 (commit `a8315dd4`): phone, sheet, guards

- `Index.Mobile.cshtml`: `players-page` wrapper; page-title card with `h1` Players; two `modern-card--aside` `players-section` cards with `h2` titles. A Dungeon Master row on a board that serves the page is `players-row member-row modern-card modern-card--interactive` holding an `h3.players-name` with the one stretched `a.row-nav-link` and an `aria-hidden` `fa-chevron-right.players-chevron`. Every other row is `players-row member-row modern-card no-link` with a `p.players-name`: no link, no tab stop, no chevron. The inline `onclick` and the inline colour `style` are gone (baseline count 2, now 0). Empty states verbatim in `.empty-state` with an `h2`. Styles: `member-cards.css` then `players.mobile.css`.
- `players.mobile.css` rewritten: one `@layer pages` block, `.mobile-layout` scope, tokens only (`--qb-text-base`, `--qb-weight-strong`, `--qb-fg-muted`, `--qb-space-md`), `!important` count 0, no media query, no keyframes. The old glass panel, the shadow-heavy name colours and the `!important` rules are deleted.
- Facts: phone render facts in `PlayersIndexRenderTests` (title and cards, sheet order, one stretched link and hidden chevron per link row, inert rows with no link/tab stop/chevron, page-off rows, both empty states), the phone case of `Players_HasThePageHeading`, and `MobileViewsTests.GetMobilePage_PlayersIndex_ReturnsSuccessAndMobileLayout` rewritten to the converted class and sheet names (name kept; logged in `tests-renamed.md`).
- Guards: both Players views added to `ConvertedViews`, `ViewSheetLinks` (desktop `players.css`; phone `member-cards.css`, `players.mobile.css`) and `PhaseSourceFiles`; `players.css` and `players.mobile.css` added to `ConvertedSheets`, `PageOnlySheets`, `AreaSheets`, `LayeredFiles` and `NoLiteralFiles`; both views added to `DateCheckedViews`. `legacy-collision.py` printed nothing for both views.

## Task 3: suites, sweep, look check, focus

- **Unit** 1416 passed, 0 failed, 0 skipped (1416 total). **Integration** 2280 passed, 0 failed, 16 skipped (2296 total): 2265 at the last green plus 15 new facts (13 render facts, 2 heading cases). `TestHostFileWatching.cs`, the controllers and the repository are unchanged since PLAN_START (`git diff --quiet` exit 0).
- **Sweep** (`~/qb-ui-review/phase-98/after/98-21/`, `--stack` glass-stack contrast scoring as the Characters, Contacts and Dungeon Master slices used): `players-index`, `players-index-nolink`, `players-index-empty`, `players-index-nodm` under both User-Agents at every width (16 captures, 0 not 200, 0 contrast failures, 0 small targets, 0 missing focus rings, no overflow at 320 or 390, no console or page error, every row assert passed). `readability-compare.py` against BEFORE with `allow.json`: 16 compared, 0 failing rows, 0 poster-glyph violations, `ok`. The one control key that changed on every row is the `main` landmark (its text now starts with the new heading), recorded as a rename in `allow.json`.
- **Both empty states live**: `players-index-nodm` (board P98 Audit No Dungeon Masters) shows `No Dungeon Masters Found` with its sentence, `players-index-empty` (board P98 Audit Empty) shows `No Players Found` with its sentence, on desktop and phone; captures viewed. The phone empty card keeps the other section populated.
- **Link and row measures** (`players-measure.json`, from `players-measure-98-21.mjs`): desktop Dungeon Master link 24px tall, `rgb(139, 69, 19)` (`--qb-wood` `#8b4513`), `underline`, focus ring (2px dark outline, 2px gold inner stroke) shown on the link. Phone rows 44px tall; the link row has one link and one chevron (`--qb-fg-muted` tone), the no-link row has no link, no tab stop and no chevron, on both the populated board and the page-off board `players-index-nolink` (no `DungeonMaster/Profile` anchor on the page, both layouts).
- **Look check** (`lookcheck.json`): desktop against `quest-details.html`, 29 properties, 1 intended difference (glass hover value; no repeated item in the page under test to compare), pass; phone against `quest-details.mobile.html`, 31 properties, 2 intended differences (glass hover value, button border), pass.
- **Reduced motion and focus** (`motion-focus.json`, `motion-focus-focus-*.png`): with `reducedMotion: 'reduce'` nothing animates and no row lifts, `document.getAnimations()` is empty after load and after hover on both layouts, no element has an infinite animation. On the phone, Tab to the first link row draws the two-tone ring on the row (2px dark outline at 2px offset plus the 2px gold inner stroke) and none on the link, not clipped by an ancestor.
- Viewed: gold Cinzel `Players` title on desktop and phone, aside cards with crown and users icons, the wood table header with the underlined wood name link on parchment, the 44px glass rows with a chevron for the Dungeon Master and a plain row for each player, and both empty cards centred with icon, heading and sentence.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Heading fact split across the two tasks**
- **Found during:** Task 1
- **Issue:** the plan puts the heading fact in Task 1 and the phone view in Task 2, but a both-layouts fact would fail on the old phone view and leave the tracer commit red.
- **Fix:** the first commit carries the desktop case of `Players_HasThePageHeading`; the second adds the phone case.
- **Files modified:** `QuestBoard.IntegrationTests/Tests/CharactersTwinAnswersTests.cs`
- **Commits:** `735680d8`, `a8315dd4`

**2. [Rule 3 - Blocking] Sweep comparison needed a landmark rename in allow.json**
- **Found during:** Task 3
- **Issue:** `readability-compare.py` reported the `main` landmark as lost on every row because its accessible-text key now begins with `Players` (the intended new heading).
- **Fix:** `allow.json` maps the old desktop and phone `main` keys to the new ones for each row; no page content was changed.

**3. [Process] Desktop view written alongside its tests**
- The desktop render facts and the desktop view were written in one pass, so the desktop facts were not observed failing first. The phone facts and the phone heading case were observed failing against the old phone view (the h1 count was 0) before the phone conversion.

Plan said the empty-state icons are not specified; the approved drawings use the crown and users glyphs instead of the old dragon and user glyphs, and they were ported as drawn.

No bugs or missing-functionality fixes were needed outside the plan.

## Known Stubs

None.

## Threat Flags

None. No controller, view model or query changed (T-98-48, T-98-64, T-98-65): the no-link rows keep no link and no tab stop and the page-off facts run under both User-Agents, names reach the markup through Razor encoding (no `Html.Raw`), the stretched link is a plain anchor to the existing route, and `PlayersController`, `UserService` and the repository are untouched (acceptance diff exit 0).

## Self-Check: PASSED

- `PlayersIndexRenderTests.cs`, `players.css`, the two converted views and the rewritten `players.mobile.css` exist; commits `735680d8` and `a8315dd4` exist.
- `report.json`, sweep captures, `allow.json`, `lookcheck.json`, `motion-focus.json`, `players-measure.json` and the focus screenshots exist under `~/qb-ui-review/phase-98/after/98-21/`.
