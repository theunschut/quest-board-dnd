---
phase: 95-quests-and-quest-log-ui-conversion
plan: 08
subsystem: ui-quest-board-desktop
tags: [quest-board, posters, picker, wax-seals, webp, masonry, layered-css, contrast-exception]
requires:
  - phase: 95-03
    provides: Gate 1 answers (pool, geometry, option C text treatment, floor 304), contrast-exceptions.json poster-glyph floors
  - phase: 95-07
    provides: Quest Details on the foundation (board links to it)
provides:
  - QuestPosterCatalog and QuestPosterPicker (stable, fitted poster, title tier and seal per quest)
  - desktop Quest/Index as stretched-link posters, seals as the status marker, bare empty board
  - quest-board.js (CSSOM-only masonry over aspect-ratio heights) and the layered poster block in quests.css
  - WebP derivatives of the six approved posters and four seals beside their PNGs
affects: [95-09 phone board, 95-12 closure selector scan, Gate 2 sheet]
tech-stack:
  added: []
  patterns: [id-mixed stable choice among posters that hold the content, ring drawn on the poster via :has(), CSS column fallback under a masonry class]
key-files:
  created:
    - QuestBoard.Service/Helpers/QuestPosterCatalog.cs
    - QuestBoard.Service/Helpers/QuestPosterPicker.cs
    - QuestBoard.UnitTests/Helpers/QuestPosterPickerTests.cs
    - QuestBoard.Service/wwwroot/js/quest-board.js
    - QuestBoard.IntegrationTests/Tests/QuestBoardEmptyStateTests.cs
  modified:
    - QuestBoard.Service/Views/Quest/Index.cshtml
    - QuestBoard.Service/wwwroot/css/quests.css
    - QuestBoard.Service/wwwroot/css/legacy-pages.css
    - QuestBoard.Service/wwwroot/css/tokens.css
    - QuestBoard.Service/wwwroot/js/site.js
    - QuestBoard.IntegrationTests/Tests/QuestsConversionGuardTests.cs
key-decisions:
  - "Poster text is plain --qb-ink at weight 600 straight on the art: no zone, feather, tint, halo or text-shadow (Gate 1 item 2, option C), ported verbatim"
  - "Picker follows the approved rule (p1 at tier 2 when no poster holds a title at 20px), estimating at the 304px card; body-line width rounded up to 0.57 em"
  - "One new width token, --card-width-floor: 304px (Gate 1 item 12)"
  - "The ring is drawn on the poster (:has(.row-nav-link:focus-visible)); the link itself shows none"
requirements-completed: [D-01, D-02, D-03, D-04, D-05, D-06, D-07, D-08, D-09, D-10, D-11, D-24, DoC-1, DoC-2, DoC-3, DoC-5, DoC-9]
duration: ~2 h
completed: 2026-11-11
status: complete
actuals:
  tokens: 15500
  tasks: 3
  commits: 4
---

# Phase 95 Plan 08: Desktop quest board posters Summary

The desktop board is the operator-approved poster board: a stable, fitted choice among six posters, spoken status in every link name, wax seals as the status marker, option-C ink straight on the artwork, WebP delivery, and a bare empty board. One Gate 1 floor is breached by a real capture and goes to the operator (below).

## Open item for the operator: poster-glyph floor breach (p5, 304px card)

`readability-compare.py --exceptions` reports **2 violations, both Poster5 at the 320 window** (`board-crowded` and `board-dm`, desktop 320): worst-glyph 10th percentile **2.28** against the p5 floor **3.10** (darkest pixel 2.17 holds its 1.90 floor). The glyph is the second title line of "A Debt Owed to the Glass Moth". At 1440 (and 1280, 1200, 992) every pool poster is at or above its floor; p5 measures 3.33 / 3.03 there.

Cause: the floors were measured over the mockup frames at the 352px card. At the 304px floor card the same poster's text box shrinks and the title wraps onto a different part of the art. No number, zone, halo or entry was changed. Options for the operator: (a) widen the p5 floor entry for the 304px card (the brief forbids me editing it), (b) take Poster5 out of the pool, (c) accept at Gate 2. The breach is recorded in `.planning/WINDOWS.md` as an open `unmet-truth`.

## Committed WebP files (quality 82, from `$QBR/tools/webp.py`, PNGs untouched)

| File | PNG bytes | WebP bytes |
|------|-----------|-----------|
| Blanks/Blanks w Shadow/Poster1 | 1,878,817 | 127,138 |
| Blanks/Blanks w Shadow/Poster2 | 982,473 | 91,928 |
| Blanks/Blanks w Shadow/Poster4 | 1,098,741 | 95,158 |
| Blanks/Blanks w Shadow/Poster5 | 485,376 | 45,010 |
| Blanks/Blanks w Shadow/Poster6 | 576,304 | 52,364 |
| Ruined Posters/Ruined w Shadow/Ruined Poster 1 | 986,607 | 70,962 |
| Wax Seals/Crown Seal | 152,990 | 19,036 |
| Wax Seals/Eagle Seal | 569,735 | 62,922 |
| Wax Seals/Flower Seal | 170,383 | 19,342 |
| Wax Seals/Sigil Seal | 175,722 | 21,968 |

No PNG changed since `BASELINE_SHA` (`git diff --name-only BASELINE..HEAD -- '*.png'` is empty). On the live board only WebP files were fetched (7 of 7 resources), no PNG.

## Probe results (sweep `$QBR/after/95-08`, rows board-crowded, board-dm, board-campaign, board-empty)

- **Overflow, contrast, errors:** every desktop record at 1440, 1280, 1200, 992 and 320: no horizontal overflow, zero native contrast failures, no console or page error, no failed request, `posterOverflow` empty. The crowded fixture holds the 40, 90, 160 and 200 character titles (the 200-character one on p1 at tier 2: content 306px in a 353px box at 304px) and the 41-character DM name; both widths clean.
- **Per-glyph minimum per pool poster** (worst glyph p10 / darkest pixel; floor in brackets):

  | Poster | 1440 | 320 | Floor |
  |--------|------|-----|-------|
  | p1 | 2.12 / 1.79 | 2.17 / 1.71 | 1.75 / 1.50 |
  | p2 | 3.50 / 3.01 | 3.56 / 2.99 | 2.95 / 2.70 |
  | p4 | 2.42 / 2.25 | 3.07 / 2.89 | 2.15 / 2.00 |
  | p5 | 3.33 / 3.03 | **2.28 / 2.17** | 3.10 / 1.90 |
  | p6 | 2.69 / 2.37 | 2.98 / 2.42 | 2.40 / 2.10 |
  | r1 | 2.68 / 1.71 (first sweep, before the picker estimate fix) | not placed | 2.25 / 1.55 |

  After the estimate fix the crowded fixture no longer places r1 (its quests have two-line titles and a 24-character DM line), so r1 is held only by the earlier sweep. All figures are below 4.5:1 by Gate 1 item 2.
- **Focus:** Tab onto a poster link draws the two-tone ring on the whole poster (`outline: 2px solid` ink token plus a `2px` gold band); the link itself shows no ring. The sweep's focus probe was extended to accept a ring on the poster (scratch tool), then `focusRingMissing` is empty on every row.
- **Hover and reduced motion:** hover gives `transform: matrix(1,0,0,1,0,-4)` with `transition-property: transform`; under `prefers-reduced-motion: reduce` the transform stays `none`. Frames in `after/95-08/evidence/`.
- **Screen-reader names** (accessibility tree): open `The Ember Gate Assize, Part Two: the Cinder Verdict, open`; open and signed up `The Quiet Tithe of Wren Hollow, open, signed up`; finalized `The Ember Gate Assize, finalized`; finalized and signed up `The Bell Beneath Greywater Stair, finalized, signed up` (seals `Finalized quest` and `You are signed up for this quest`); Campaign `The Sealed Archive of Ashgrave, open` (seal `Open quest`). Chromium inserts a space before the comma in the computed name ("Stair , finalized") because Bootstrap's visually-hidden class is absolutely positioned.
- **readability-compare vs BEFORE** (desktop rows, with `--allow after/95-08/allow.json` for the renamed link names and the main region text, and `--exceptions`): zero problems; the only failures are the two p5 floor violations above. `vs-mockup.html` places the built frames beside the approved mockup frames.

## What changed

**Picker and catalog (commits `2a0ef852` red, `fde6e874` green).** 12 unit facts: stability, pool membership and no Poster3, the 200-character title going to p1 at tier 2, tier-1 results fitting their box, variety over 200 ids (at least four posters, none over half), Campaign budget, seal stability, no throw on empty or null input, a long unbroken word, the catalog's URLs and classes, and the DM-wrap case. The mix is `(uint)id * 2654435761u` with range reduction on the high bits; the seal comes from a second mix.

**Artwork and sheet (`95fb9f33`).** The ten WebP files, the `@layer pages` block at the top of `quests.css` (poster, text box, option-C text, seals, ring, hover, six modifiers with `image-set()` after a plain PNG declaration, paper-slab fallback layer under the text box), `--card-width-floor` in `tokens.css`, and the guard fact `PosterCatalog_AndPosterSheet_AgreeOnKeys`.

**Markup and module (`46be8a5c`).** `Index.cshtml` rewritten (no description, no inline style, handler or data attributes; hidden `h1`; `h2` title holding the single `row-nav-link`; status suffix; seals as `picture` with WebP source; order A; Campaign shows title and DM only). `quest-board.js` replaces the board code in `site.js`. Old board rules left `quests.css` and `legacy-pages.css`. `QuestBoardEmptyStateTests` (4 facts) added; the view, sheet link and sources are registered in the conversion guard.

## Verification

`dotnet build` 0 errors. `dotnet test`: unit 1312 passed; integration 1780 passed, 11 skipped (pre-existing), 0 failed.

## Deviations from Plan

**1. [Rule 1 - Bug] DM line estimate rounded up to 0.57 em (was 0.55).** The approved 0.55 under-counted a 24-character DM line on the narrowest ruined poster (real width 189.6px against a 188px box), so two r1 posters overflowed by 9px at 320 (the plan's overflow backstop). The measured range is 0.507 to 0.564 em; 0.57 is the rounded-up maximum. Side effect: r1 now holds only short-title quests with this DM name, so it is rarely picked in the fixture. Pinned by `Holds_DmLineAsWideAsTheBox_CountsTheLineAsWrapped`.

**2. [Rule 3 - Blocking] Scratch focus probe extended** (`tools/probes.mjs`): a link inside a poster counts as ringed when its poster draws the ring. Without it every poster link was reported as ring-less although the ring is drawn (the specified design).

**3. [Rule 1 - Bug] Plan verify too strict for WebP set.** `git ls-files` WebP equality would fail on the two existing `notice-board-*.webp` files; the check was run excluding them. The committed set equals the approved pool plus the four seals.

**4. [Rule 3 - Blocking] Narrow-window padding.** The unlayered `body:has(.quest-board-container) .container.mt-3` rule (2rem important padding) and `.home-page .container.mt-3` left only 256px at 320, so the 304px card floor could not fit. Both moved into the layered block with 8px padding below 640px; board padding follows.

**5. [Judgement] Shared board-era rules kept.** `.quest-title`, `.quest-description`, `.cr-badge` and `.quest-dm`-style rules in `quests.css` stay because Details, Quest Log, the calendar and `_QuestCard` still use those class names; only classes used solely by the old board (`fantasy-quest-card`, wax seal, challenge-rating, top/middle/bottom, content, header, icon, signup seal) went. Dead `fantasy-quest-card` and seal duplicates in `legacy-pages.css` stay for the closure plan's scan.

**6. [Judgement] Compare against BEFORE restricted to desktop rows.** The phone rows fail the compare because of fixture drift from earlier plans (a finalized quest on the board since the follow-up fixtures) and the phone board belongs to the next plan. Desktop rows were extracted to `before-desktop-board` and `after/95-08-desktop`. The "board unchanged" sweep after Task 2 also failed on that fixture drift, not on any CSS change (the poster rules named no class the old markup used).

**7. [Judgement] Class names.** Labels are `<strong class="quest-poster-label">` (the verbatim treatment targets `strong`); the CR badge is `quest-poster-cr`; masonry state is `is-masonry` with `--poster-x`, `--poster-y`, `--poster-w`, `--board-height`.

## Auth gates

None.

## Known Stubs

None.

## Threat Flags

None. Titles and DM names go through Razor encoding only; the picker reads lengths; the poster link target rule (Manage for the quest's own DM, Details otherwise) is unchanged and pinned by `RowNavigationAccessibilityTests`.

## State left behind

Working-tree app running on `http://localhost:8000` with this build. Port 8001 untouched. Scratch: `after/95-08` (sweep, allow.json, evidence, vs-mockup.html), `tools/poster-diag.mjs`, `poster-evidence.mjs`, `glyph-diag.mjs`.

## Self-Check: PASSED

Verified: catalog, picker, test file, `quest-board.js`, `QuestBoardEmptyStateTests.cs` and the ten WebP files exist; commits `2a0ef852`, `fde6e874`, `95fb9f33`, `46be8a5c` exist; full `dotnet test` green; the catalog-key command printed `ok`; the WebP set check passed; `site.js` no longer defines `layoutMasonry` and `quest-board.js` does. The poster-glyph floor check is NOT passing for p5 at 320 and is reported above, not hidden.
