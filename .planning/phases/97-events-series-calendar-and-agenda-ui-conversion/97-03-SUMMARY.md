---
phase: 97-events-series-calendar-and-agenda-ui-conversion
plan: 03
subsystem: review-mockups
tags: [ui-conversion, calendar, mockup, gate-1, playwright, scratch-only]
requires: ["97-02"]
provides:
  - "~/qb-ui-review/phase-97/mockups/v1/calendar/ (index.html, calendar.css, calendar-month.js, frames.json, fit-check.json, truncation.json/.html, behaviour-check.json, contrast.json, month-data.json)"
  - "~/qb-ui-review/phase-97/mockups/v1/site/ (post-v6.0.2 foundation copy, PROVENANCE.txt at bf1865db) reused by 97-05"
  - "~/qb-ui-review/phase-97/mockups/v1/png/ (37 frames, truncation strips, report.json, behaviour.webm 3.97 MB)"
  - "tools: calendar-data.mjs, build-calendar-mockup.py, calendar-frames.py, calendar-shoot.mjs, calendar-fitcheck.py, build-truncation.py, calendar-truncation.mjs, calendar-behaviour.mjs, calendar-record.mjs, calendar-shell-head/foot.html"
affects: [97-05, 97-13, 97-14, 97-15]
key-files:
  created: []
  modified: []
decisions:
  - "The desktop header is two rows at every width from 992px: the legend never fits beside the title, buttons and month navigation, even on a board with no linked boards and at the widest container (needs about 1380px, the card is 1294px)"
  - "The page block padding below the month card is 0 (8px above stays), the only way a six-week month with the two-row header keeps 3 slots at 1280x720"
  - "The mockup loads calendar-month.js as a classic deferred script because a browser refuses module scripts from file://; the file has no import or export and is verified identical as a module"
metrics:
  duration: "about 3 hours"
  completed: 2026-11-12
status: complete
actuals:
  tokens: 32500
  tasks: 3
  commits: 1
---

# Phase 97 Plan 03: Gate 1 calendar mockups Summary

The desktop calendar port is drawn with real baseline data on the post-v6.0.2 foundation, as shippable-shape scratch files: a
generated month page in the planned `_Calendar` markup shape, a one-block `@layer pages` sheet, an ES-module-shaped script, 37
measured frames, the D-14 truncation strips and a 58-check behaviour suite. Everything is under `~/qb-ui-review/phase-97/`;
no repository source file was created or changed (`git status --porcelain` empty apart from this summary).

## What was built

**Foundation copy.** `mockups/v1/site/` is `git archive` of `wwwroot/css`, `wwwroot/lib` and the two board images at
`bf1865dbec711a250a5b3e93449ac99208360b74` (feature/phase-97 HEAD when copied; `git merge-base --is-ancestor c781090c` = yes),
recorded in `site/PROVENANCE.txt`. The page links `app.css, tokens.css, base.css, components.css, shell.css, v4-shims`, then
`calendar.css`, in the layouts' order. The real shell is reproduced: fixed navbar, `.container.mt-3.flex-grow-1 > main`, footer
(the footer and the 32px gap above it stay below the fold).

**Data (T-97-07).** `calendar-data.mjs` scraped the baseline on 8301 as `p97player` (Audit Board, May 2027, January 2027,
January 2029, November 2026) and as `p97dm2` on Saltmarsh Concord (May 2027, runway alert, Board Availability button), fantasy
fixture text only. The calendar page does not print the viewer's vote, so the three vote markers (Yes 11 May, Maybe 13 May,
No 20 May) come from the same quest's Details page for the same viewer. Boards scraped: Audit Board (as p97player) and Saltmarsh Concord (as p97dm2); linked entries come from Emberfall Wardens (the
viewer is a member, so open) and Saltmarsh Concord (not a member, so static), plus the Audit Board's earlier linked boards.
Entries in the crowded month (34): 19 own events (1 cancelled; the weekly series occurrences and one vigil are all-day), 5 own
quests (3 proposed, all with the no-building-key marker and the Yes, Maybe and No vote markers; 2 finalized, 1 of them with the
no-key marker), and 10 linked entries (5 open, 5 static; 2 of them cancelled events). Today (Nov 2026): 52 entries, the
today ring on 12 Nov. Empty month: January 2029. Five-week month: January 2027 (5 events).

**Markup.** Server-shaped: every entry is an `li` with the hook classes and `cal-entry`, its `a.cal-entry__body` (own and
member-linked) or `div.cal-entry__body` (static linked, no link, no tabindex, no data attribute, no id, no "vote" word), the
status marker, time, title, trailing `cal-entry__flags` (no-key, vote, link glyph) and its own hidden `div.cal-entry-detail`.
Cells carry the visually hidden `Today.` and full date text. Five variants (`crowded`, `fiveweek`, `empty`, `today`, `dm`) live
in one `index.html`; `calendar-frame.js` (mockup only) keeps the one a frame's query asks for, the default needs no script.

**Sheet.** `calendar.css`: header comment, one `@layer pages { }` block, everything scoped under `.calendar-page`, tokens only,
no `!important`, no colour literal, no `transition: all`, a `prefers-reduced-motion` block. `--cal-slots` is set by
`(min-width: 992px) and (min-height: 712px|850px|980px)` queries, `--cal-cell-min` is computed from it, rows are
`minmax(var(--cal-cell-min), 1fr)`, the weekday header is `--qb-wood` with `--qb-fg`, today is the parchment ring and pill, the
header padding override and `.calendar-page .modern-card-header h1 { color: var(--qb-fg) }` are in the layer, the month card
stays `overflow: visible`.

**Module.** `calendar-month.js` reads `--cal-slots`, hides entries beyond `slots - 1` with `hidden`, adds `li > button.cal-more`
(never "+1 more"), clones the hidden detail block into one body-mounted card and builds one body-mounted non-modal day panel
from the entries (text via `textContent`/`cloneNode` only; the only style written is `--pop-x`/`--pop-y`). Functions the slice ports:
`readSlots`, `applySlots`, `showCard` (spec name `openCard`), `closeCard`, `openPanel`, `closePanel`.

## Fit results (fit-check.json, 24 records, 0 failing)

No page overshoot (the month block ends inside the window) and no cell with overflowing content at all seven sizes in the
six-week crowded month and the five-week month, plus empty, today, DM and linked-board headers:

| Size | Slots | Six-week cell height | Notes |
|---|---|---|---|
| 1280x720 | 3 | 90.2px (needs 90; 0.2px slack) | header 73px, tightest case |
| 1366x768 | 3 | fits | |
| 1440x900 / 1536x864 | 4 | fits | |
| 1920x1080 / 1280x1024 | 5 | fits | |
| 1280x700 | 2 | fits | |
| 992x768, 1100x800 (extra) | 3 | 94.0 / 99.3px | legend wraps to two lines, header 98px |

Header: row 1 = 45px (36px buttons, 4+4 padding, 1px border), legend row 28px, whole header 73px at every width from 1280.
Weekday row 24px. Day-number row 20px, entry rows 20px, "+N more" 20px, panel rows 48px+. Smallest rendered text 14px in every frame.
Cell widths: 159.1px at a 1280 window, 184.9px at 1440 and above (see Findings 2).

**Runway alert (Gate 1 item 6).** Default (alert above the month, grid keeps its minimum cell height): the page overshoots by
**73px** at 1280x720 (alert 57px plus 16px margin); nothing clips. Alternative (one fewer slot while the alert shows): 3 slots
become 2 and the month fits again (overshoot 0).

## Contrast (contrast.json; probes.mjs, glass stack, 37 frames, 0 failures)

Weekday header `--qb-fg` on `--qb-wood` **5.63:1**; Calendar `h1` parchment **9.88:1** over its band (probe scoring; UI-SPEC
computes 8.05) against the foundation gold `h1` **7.63:1** (frame `gold-title-1280x720`, the h1 reads `accent` there and `fg` in
every other frame); detail card and panel text **12.98:1**; "+N more" 9.88 (6.2 hovered); entry titles on cancelled/linked
paper 7.72; entry time 7.72; empty-month note 6.7; day numbers 9.88. Focus ring missing on zero controls; text spacing and
overflow probes clean; the 320px frame reports `overflow: false`.

## Truncation (truncation.json, 34 Audit Board fixture titles, real font)

Title space after marker, time and gaps; average characters shown over all 34 titles (titles truncated):

| Cell | Time + title 14px | Title only 14px | Linked 14px | Both markers 14px | Time + title 13px |
|---|---|---|---|---|---|
| 1280 production (159.1px cell) | 8.6 (33) | 14.1 (28) | 6.0 (34) | 3.6 (34) | 9.9 (33) |
| 1440 production (184.9px cell) | 12.1 (32) | 17.2 (20) | 9.7 (33) | 6.9 (34) | 13.8 (29) |
| fit-mockup 1280 (179px, full window) | 11.3 (33) | 16.6 (21) | 8.8 (33) | 6.0 (34) | 12.7 (32) |
| fit-mockup 1440 (202px, full window) | 14.7 (26) | 18.8 (17) | 11.9 (32) | 9.2 (33) | 16.1 (22) |

Research's figures (12.6 and 15.7 at 13px, full-window cells) reproduce: 12.7 and 16.1 here at the same widths and size, and the
14px figures (11.3, 14.7) match its 11.6 and 14.5 forecast. Dropping the time buys 5.5 chars at 1280 and 5.1 at 1440 and
rescues 5 titles (15%) at 1280, 12 (35%) at 1440; the 13px size buys 1.3 and 1.7; the link glyph costs 2.6; both trailing markers cost 5.0.
Questions with defaults are in `truncation.json` (`default: time`, 14px): keep the time; keep the glyph; keep both markers.

## Behaviour check (behaviour-check.json: 58 of 58 pass)

Ported from the phase-94 suite (46): 36 kept (5 renamed: `card-esc-dismiss` to `esc-card`, `click-mode-no-hover-open` to
`panel-no-hover-open`, `panel-rows-full-width` to `panel-rows-48` (44 to 48px), `panel-esc-focus-return` to `focus-return`,
`panel-keyboard` to `tab-loop`). **Dropped 10** hover-mode checks (`hover-mode-not-before-350`, `-opens-after-350`,
`-no-focus-theft`, `-stays-over-panel`, `-moving-inside`, `-closes-on-leave`, `-esc`, `-quick-pass`, `-enter-pins`,
`-enter-esc-return`). **Added 22:** `title-attribute-removed`, `card-on-inert-hover`, `inert-markup`, `no-mode-switcher`,
`panel-dialog-semantics`, `panel-hides-card`, `panel-close-36`, `panel-space-opens`, `overlays-on-body` (card and panel are children
of `document.body`, the card lands 8px below or above its entry and the panel overlaps its cell, six-week month at 1280x720),
`one-panel`, `legend-popover`, `no-plus-one` (8 sizes, 3 months), `slots-follow-height`, `fit-off-below-992`, `resize-recomputes`,
`second-tap-follows`, `tap-inert-card-then-nothing`, `nojs-all-visible` (all 125 entries visible, no "+N more", crowded cell
scrolls), `module-form`, `no-planning-refs`, `no-transition-all`, `reduced-motion-block`. `behaviour.webm` (3.97 MB) shows hover card,
inert card, focus card, panel with Tab and Esc, the legend popover.

## Frames (37; `frames.json` names, PNGs and `report.json` in `mockups/v1/png/`)

`crowded-{1280x720, 1366x768, 1440x900, 1536x864, 1920x1080, 1280x1024, 1280x700}`, `fiveweek-` same seven sizes,
`empty-{1280x720,1440x900}`, `today-{1280x720,1440x900}`, `dm-header-1280x720`, `linked-header-1280x720`, `header-onerow-1280x720`,
`header-tworow-1280x720`, `alert-scroll-1280x720`, `alert-fewer-slots-1280x720`, `narrow-991`, `narrow-768` (768x900), `narrow-320`,
`legend-open-1280x720`, `gold-title-1280x720`, `card-own-1280x720/1440x900`, `card-inert-1280x720`, `card-member-1280x720`,
`focus-entry-1280x720`, `panel-1280x720/1440x900/1920x1080`; plus `truncation-strips.png` (`truncation.html`, four variants, 14px and 13px).

## Findings the Gate 1 sheet must carry (each differs from what UI-SPEC assumed)

1. **The header is always two rows from 992px.** The legend strip plus "What do these mean?" needs about 1380px beside the title, buttons
   and month navigation; the card is at most 1294px (1116px at a 1280 window). `header-onerow` therefore shows a board without linked
   boards and it is also two rows (shorter legend); `header-tworow` is a Dungeon Master on a linked board. The 45px row-1 figure holds
   (at most 48), the whole header is 73px (98px when the legend wraps to two lines at 992 to about 1200px).
2. **The page container narrows the cells.** The layout's `.container` (1140px, 1320px) minus its 12px side padding gives 159.1px
   cells at 1280 and 184.9px at 1440 and above, against the 179px and 202px of the phase-94 mockup (full window). Titles show about
   2.7 characters fewer at 1280 and 2.6 fewer at 1440 (table above). If the operator wants the old figures the Calendar page would
   need a full-width container, which the spec does not propose.
3. **Both trailing markers are costly at the real width:** 3.6 characters of title at 1280 (6.9 at 1440), so Gate 1 item 8's fallback
   (keep the key marker, move the vote to the card and panel) is worth showing.
4. **Bottom page padding is 0** (top 8px stays). With 8px below, a six-week month with the two-row header at 1280x720 overshoots by
   about 7px (computed: 88.8px rows against 90px); at 0 it fits with 0.2px slack. The card's bottom edge then meets the window's bottom edge at that one size.
5. **768 to 991px:** entries cannot hold marker, time, title and two trailing markers on one line in cells of 99px, so the mockup
   lets an entry take two or three lines (markers up beside the time, the title wrapping underneath with `overflow-wrap: break-word`);
   UI-SPEC says one line with an ellipsis. Cells grow there anyway; frames `narrow-768` and `narrow-991` show it. Operator can choose
   tiles from 992px down instead.
6. **Weekday names** shorten to three letters (the full word stays for assistive technology) under 768px; at 320px the full names
   broke mid-word.

## Deviations from Plan

**1. [Rule 3 - Blocking] Classic script instead of `<script type="module">`.** Chromium refuses module scripts from `file://`,
and the operator reviews from local files (orchestrator override). `calendar-month.js` has no import or export and is loaded with
`<script defer src>`; the `module-form` check loads the same file as `type="module"` (with a file-access flag) and gets the same
result. The shipping view loads it as a module.

**2. [Rule 3 - Blocking] Own shoot driver.** `mockup-shoot.mjs` (copied in 97-01) is picker-specific (rows, panel, landing
geometry); `calendar-shoot.mjs` reuses its probes (`runProbes`, stack scoring) and the frames.json contract, and measures fit in the
page (`pageScrolls` means the month's block ends below the window; the footer and its gap are by design below the fold).
`headerHeight` is the whole header (73), `headerRow1Height` the row-1 figure (45).

**3. [Rule 1 - Bug, in the drawing] fixes found by the frames:** the h1 overflowed at 320px (nowrap now only from 992px), the
weekday names broke mid-word at 320px, trailing markers spilled out of the entry at 768 to 991px, the legend row came out 31px
instead of at most 28px (button line height), the grid did not stretch to the window (height is now definite with
`min-height: min-content`), a no-script cell grew its row instead of scrolling (`contain: size` from 992px), and the legend
popover heading used the display face.

**4. [Planner assumption 1, narrowed]** The vote marker is not rendered on today's calendar page (the view only reads it on
Quest Details); the slice must supply the viewer's vote to `_Calendar` for the non-details page.

**5. Header fits one row nowhere (Finding 1) and the `calendar-head--wrap` modifier was dropped** after it proved unnecessary.

## Known Stubs

None. The mockup links point at `#` anchors on purpose (second-tap follows `#open-quest-...`); no hard-coded empty data flows to a
rendered page.

## Threat Flags

None: scratch files only, no network surface; baseline pages were read as review accounts.

## Self-Check: PASSED

Verified present: `mockups/v1/calendar/{index.html,calendar.css,calendar-month.js,calendar-frame.js,frame-extras.css,frames.json,
fit-check.json,truncation.json,truncation.html,behaviour-check.json,contrast.json,month-data.json}`, `mockups/v1/site/PROVENANCE.txt`,
`mockups/v1/png/{report.json,behaviour.webm,truncation-strips.png}` plus a PNG per frame; the three task verifies each printed
`ok` twice; `grep -c` for planning references in `calendar-month.js` prints 0; `git status --porcelain` showed no repository change
other than this file.
