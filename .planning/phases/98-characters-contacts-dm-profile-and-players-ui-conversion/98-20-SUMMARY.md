---
phase: 98-characters-contacts-dm-profile-and-players-ui-conversion
plan: 20
subsystem: dm-profile
tags: [ui-conversion, razor, wall-clock, stretched-link, twin-views, guard-tests]
requires: [98-19]
provides:
  - "DungeonMaster/Profile (desktop) and Profile.Mobile (phone) on the converted contract: full-width profile card with the h1 name, About and Quest History as h2 aside cards, quest table in .table-responsive on the desktop, member-row quest slips with one stretched row-nav-link on the phone"
  - "Finalized quest dates through Html.WallClock(value, \"date\") and Date TBD for an undated quest on both layouts"
  - "dm-profile.css and dm-profile.mobile.css as one pages-layer block each (tokens only, no important flag); dm-profile.css out of the global layout list, so no layout links any of the four area sheets"
  - "DmProfileRenderTests (both layouts), DesktopDmProfile_ShowsDateTbdForAnUndatedQuest, PhoneDmProfile_RendersTheSameDates, AreaViews_RenderNoRawDateFormatting"
affects: [98-21, 98-22, 98-23]
tech-stack:
  added: []
  patterns: [member-cards.css row reused for the phone quest slips; one view per layout with its own layered sheet from the Styles section]
key-files:
  created:
    - QuestBoard.IntegrationTests/Tests/DmProfileRenderTests.cs
  modified:
    - QuestBoard.Service/Views/DungeonMaster/Profile.cshtml
    - QuestBoard.Service/Views/DungeonMaster/Profile.Mobile.cshtml
    - QuestBoard.Service/wwwroot/css/dm-profile.css
    - QuestBoard.Service/wwwroot/css/dm-profile.mobile.css
    - QuestBoard.Service/Views/Shared/_Layout.cshtml
    - QuestBoard.IntegrationTests/Tests/CharactersTwinAnswersTests.cs
    - QuestBoard.IntegrationTests/Mobile/MobileViewsTests.cs
    - QuestBoard.IntegrationTests/Tests/CharactersConversionGuardTests.cs
    - QuestBoard.IntegrationTests/Tests/UiFoundationLayerGuardTests.cs
decisions:
  - "Gate 1 dmDate applied: wallClock accept and dateTbd accept. The date goes through Html.WallClock on both layouts, and the desktop empty cell for an undated quest now reads Date TBD"
  - "Gate 1 a1 applied as drawn: a full-width profile card on the desktop with About (one third) and Quest History (two thirds) in a row under it"
  - "The profile photo is decorative (empty alt) because the h1 beside it names the Dungeon Master; the drawing did the same"
  - "A quest on a board that does not serve the Quests page keeps its title as plain text (table cell and slip), with the slip losing its interactive lift"
metrics:
  tasks: 3
  commits: 2
status: complete
actuals:
  tokens: 52000
  tasks: 3
  commits: 2
---

# Phase 98 Plan 20: Dungeon Master profile twins Summary

Both Dungeon Master profile views are converted: a full-width profile card with an h1 name, readable table links, wall-clock dates and Date TBD on both layouts, phone quest slips with one stretched link each, the nested container and the inline handler and colour gone, and `dm-profile.css` out of the global layout list. After this slice no layout links an area sheet; the Players list (next plan) is the last twin pair.

## Outcome applied

From `gate1/answers.json`: `dmDate` = `wallClock: accept`, `dateTbd: accept`; `a1` = full-width profile card with About and Quest History under it; `lookCheck` approved. The approved drafts (`dm-profile.css`, `dm-profile.mobile.css`) and drawings (`mockups/v1/look/dm/`) were ported; `member-cards.css` from the Characters list slice is reused for the phone slips, not forked.

## Task 1 (tracer, commit `dfdfb247`): desktop end to end

- `Profile.cshtml`: `dm-profile-page` wrapper (the nested `container mt-4` is gone, the layout supplies it). Profile card with the h1 name, the 128px `dm-profile-photo` or `dm-profile-placeholder` tile, the role line and `Edit Profile` as `btn-warning` for the owner (and an admin) only. `About` (`h2`, Markdown bio or `No bio provided yet.`) and `Quest History` (`h2`) are `modern-card--aside` sections. The table (Title, Date, Difficulty with `scope="col"`) sits in `.table-responsive`; titles are links (wood, underlined by the shared table rules) when the board serves Quests, plain text otherwise; the CR value is the neutral `badge bg-secondary`. The date cell is `Html.WallClock(quest.Date.Value, "date")` or `Date TBD`. The empty state is the shared `.empty-state` with an `h2` and the sentence verbatim.
- `dm-profile.css`: one `@layer pages` block, tokens only, `!important` 0 (was 0 at HEAD). The unused purple badge rule is deleted. Removed from `_Layout.cshtml`.
- Tests: `DmProfileRenderTests` (cards, sheets, photo and Markdown bio, owner and non-owner edit action, wall-clock date, Date TBD, responsive table and long names, empty states), `DesktopDmProfile_ShowsDateTbdForAnUndatedQuest`, `AreaViews_RenderNoRawDateFormatting` with its scanner self-test, and `dm-profile.css` joined `PageOnlySheets` (so `Layouts_LinkNoAreaSheet` now lists all four sheets), `ConvertedSheets`, `ConvertedViews`, `ViewSheetLinks`, `LayeredFiles`, `NoLiteralFiles` and `PhaseSourceFiles`.
- Tracer gate: no auto flag is set and a sequential subagent cannot be resumed, so the gate was the automated re-run (150 passed) plus a live capture of `dm-profile-full` before the phone slice (stack-mode sweep, one row, 200, no console error).

## Task 2 (commit `675af675`): the phone view

- `Profile.Mobile.cshtml`: the same card and aside structure on the phone; each quest is a `dm-quest-row member-row modern-card modern-card--interactive` slip with the title as the one stretched `a.row-nav-link` inside an `h3`, a `fa-calendar-check` line with the wall-clock date or `Date TBD`, and the CR badge. No `style` attribute, no `on*` attribute (was 2). A board without the Quests page gets a static slip.
- `dm-profile.mobile.css`: one `@layer pages` block, no media query, `.mobile-layout` scope, tokens only, `!important` 0 (was 11).
- `MobileViewsTests.MobileDmProfile_MobileUserAgent_RendersGlassCardLayout` keeps its name and intent with the converted names (logged in `tests-renamed.md`); `PhoneDmProfile_RendersTheSameDates` and the phone slip fact (three slips, one stretched link each, no table, long names whole) added; both views are in every area guard list. `legacy-collision.py` prints nothing for either view.

## Task 3: suites, sweep and look check

- **Unit**: 1416 passed, 0 failed, 0 skipped, 1416 total. **Integration**: 2265 passed, 0 failed, 16 skipped, 2281 total (98-19: 2245 passed; +20 new facts). `TestHostFileWatching.cs`, `QuestBoard.Service/Controllers` and `HtmlHelperExtensions.cs` are untouched (git diff against PLAN_START is empty).
- **Sweep** (`~/qb-ui-review/phase-98/after/98-20/`, `--stack` glass-stack contrast scoring as in the Characters and Contacts slices): `dm-profile-full`, `dm-profile-own`, `dm-profile-bare` under both User-Agents at the widths the rows name (12 captures, 0 not 200). `readability-compare.py` against BEFORE with `allow.json`: 12 compared, 0 failing rows, 0 poster-glyph violations, `ok`.
  - Contrast failures: BEFORE 1 to 3 per desktop row and 5 to 14 per phone row; AFTER 0 on every row.
  - Overflow: none before or after (320 desktop, 390 and 412 phone). Small phone targets: BEFORE 3 on the full and own rows; AFTER 0. No missing focus ring, no console or page error.
- **Allowed differences** (`allow.json`, no control lost): one h1 name (was h2 desktop, h5 phone), h2 section titles, decorative photo (empty alt), a phone quest is one stretched link named by the title only (it was a clickable row), the wall-clock helper date, Date TBD where the desktop cell was empty.
- **Look check** (`lookcheck.json`): desktop against `quest-details.html`, 29 properties, 1 intended difference (glass hover value; the desktop player view has no button and no repeated item to compare), pass; phone against `quest-details.mobile.html`, 31 properties, 2 intended differences (glass hover value, button border), pass.
- **Reduced motion and focus** (`motion-focus.json`, `motion-focus-focus-mobile.png`): with reduced motion nothing animates and no slip lifts; no infinite animation on the page. Tab to the first slip draws the two-tone ring on the slip (2px dark outline at 2px offset plus the 2px gold inner stroke) and none on the link, not clipped. Viewed. On the desktop the quest links carry the ring (sweep `focusRingMissing` is empty); the tool's `clippedByAncestor: true` for the table row is an artefact of measuring the row (its box equals the responsive wrapper's), the link sits 8px inside the cell so its 4px ring is not cut.
- I viewed the desktop captures (gold Cinzel h1 in a full-width title card with photo, role line and the amber Edit Profile for the owner; About and Quest History cards under it, parchment table with wood underlined links and slate CR badges) and the phone captures (centred 128px photo, aside cards, three slips with title, date and CR badge, focus ring around the whole slip).

## Date text before and after (Gate 2 item)

Recorded by `dm-dates.mjs` (BEFORE, `before/dm-dates.json`) and `dm-dates-after.mjs` (AFTER, `after/98-20/dm-dates.json`). Server first paint is the helper's invariant text; after the page script runs, `site.js` rewrites the wall-clock text in the viewer's own locale (the existing helper behaviour, never the timezone). The sweep browser reports `en-GB`.

| Quest | BEFORE desktop | BEFORE phone | AFTER first paint (both) | AFTER in the `en-GB` browser (both) |
|-------|----------------|--------------|--------------------------|-------------------------------------|
| The Lantern Gate Vigil | Dec 04, 2026 | Dec 04, 2026 | Dec 4, 2026 | 4 Dec 2026 |
| The Unscheduled Embermarch | (empty cell) | Date TBD | Date TBD | Date TBD |
| The Hollow Bridge Crossing | Sept 30, 2026 | Sept 30, 2026 | Sep 30, 2026 | 30 Sept 2026 |

The one listed change is the move to the helper: leading zero dropped and Sept to Sep on first paint, Date TBD on the desktop. A viewer with an English-US browser reads `Dec 4, 2026` and `Sep 30, 2026`; an English-UK browser reads the day-first form above (`Sept` is that locale's own abbreviation). That is how every other wall-clock date in the application already reads.

### Release check (open caveat on Gate 1 assumption 7)

Production `LANG` was not inspected and is still not. The approved wording for the first-paint text (`Sep`) does not depend on it (the helper formats with the invariant culture for the `date` style), but the BEFORE text `Sept` came from the server's current culture, so the production server culture should be read before release to confirm the BEFORE column on the live instance. Record this again as a release check.

## Deviations from Plan

### Plan interpretations

1. **[Tracer gate]** No auto flag is set and a sequential subagent cannot be resumed, so the tracer gate was the automated re-run plus a live capture (as in earlier slices).
2. **[Contrast scoring mode]** The sweep ran with `--stack`, the mode the Characters and Contacts slices used for nested glass; the plan's command has no flag for it.
3. **[Test split across commits]** The phone variants of the render facts, the phone twin fact and the phone slip fact were added in the second commit, with the phone view, so the first commit is green on its own.
4. **[Red step]** The new facts were written before the views but the first run happened after the desktop conversion; the old views differ from every asserted fact (nested container, raw date format, empty cell), so they could not have passed. Two assertion slips in my own new tests (a seed with two undated quests, and a `because` string read as a second expected value) were fixed in the same commit.
5. **[Tools]** `list-motion-focus.mjs` (scratch, outside the repository) now resolves `{fixture}` placeholders in a row path; `dm-dates-after.mjs` is new. Neither touches the repository.
6. **[Plan text]** The plan names `AreaViews_RenderNoRawDateFormatting` for the Characters guard; it existed only in the Events guard, so a copy scoped to this area's views (with its own scanner self-test) was added.

No bugs or missing-functionality fixes were needed outside the plan.

## Known Stubs

None.

## Threat Flags

None. No controller, view model, query or helper changed (T-98-49, T-98-50): names and bio reach the markup through Razor encoding and the existing Markdown pipeline (no `Html.Raw` added), and the finalized date goes through `Html.WallClock`, which takes no zone and cannot convert; `HtmlHelperExtensions.cs` is unchanged (git diff against PLAN_START is empty).

## Self-Check: PASSED

- `DmProfileRenderTests.cs`, both converted views and both converted sheets exist; commits `dfdfb247` and `675af675` exist.
- Report, sweep captures, `allow.json`, `lookcheck.json`, `motion-focus.json`, `dm-dates.json` and the focus screenshot exist under `~/qb-ui-review/phase-98/after/98-20/`.
