---
phase: 99-shop-and-shop-management-ui-conversion
plan: 06
subsystem: gate-1-mockups
tags: [ui-review, gate-1, mockups, shop, emblem, preview-dialog, look-check, proposals]
requires: ["99-05"]
provides:
  - Gate 1 package B drawn as scratch mockups under ~/qb-ui-review/phase-99/mockups/v1/ (the emblem tile on both layouts, the Preview dialog, the look-check pages, nine proposals and the table header alternative)
  - draft shop-form.css (Create and Edit, the Preview card replica) and the emblem, details and management additions to shop-pages.css and shop-pages.mobile.css
  - whole-picture.mjs, whole-batch-99.mjs, preview-focus-99.mjs, build-package-b.py as scratch tools; lookcheck-99.mjs now also compares structure against the quest pages under the navy theme
affects: [Gate 1 package assembly, the details partial slice, the management slices, the form slice]
tech-stack:
  added: []
  patterns: [a tile sized only by a minimum block size and flex centring, whole-picture measurements of the glyph box and the image box, the real Bootstrap modal in the Preview drawings, a reference page given the navy theme for structure-only checks]
key-files:
  created: []
  modified: []
decisions:
  - "No operator-only choice is recorded as chosen; each is a Gate 1 item with its default drawn beside the alternative"
  - "Three look-check differences beyond the eight listed ones are named in lookcheck.json (beyondEight) instead of redrawing the mock against a stale reference"
  - "The proposals draw today's behaviour as the contract's default and the proposal as the alternative; where today's behaviour is not a picture (a native confirm, a missing alert) it is drawn as a plain framed sentence"
metrics:
  tasks: 3
  commits: 1
status: complete
actuals:
  tokens: 60000
  tasks: 3
  commits: 1
---

# Phase 99 Plan 06: Gate 1 Package B Summary

Gate 1 package B is drawn: the emblem tile in the details dialog and on Shop/Details (desktop and phone, five rarities, four type glyphs, a long name, a wide Markdown image, the text-spacing stress), the Preview dialog over a filled, a blank, a long-name and a Legendary Create form, the look checks for Details, the management list and a management form, and every copy-changing or control-removing proposal beside today's behaviour. No repository source file was created or changed; the only commit is this docs commit.

Package path: `~/qb-ui-review/phase-99/mockups/v1/` (`emblem/`, `preview/`, `look-details/`, `look-mgmt/`, `look-form/`, `proposals/`, `site/`, `png/`, `png/pairs/`, `frames.json`, `lookcheck.json`). Package A is in the same tree.

## What was built

- **Emblem tile (`shop-pages.css`).** `.emblem` is a flex-centred block with `min-block-size` 144px (120px under `.mobile-layout`), full column width, `--qb-surface-scrim` fill, a 2px border and `--qb-radius-lg`, a 64px glyph (48px on the phone). One modifier per rarity (`.emblem--common` to `--legendary`) sets two local custom properties from the fill and `-text` token pairs. `.item-details-content .markdown-content img` holds `max-inline-size: 100%; block-size: auto`. No `aspect-ratio`, `object-fit`, `overflow: hidden`, literal height or `style` attribute exists in any shop sheet or mock (`grep -cE` on the three sheets prints 0; `mock-scan.py` reports 95 files, 0 hits).
- **The shared details body.** One partial drawing (`details_partial` in `build-package-b.py`) serves the dialog (item name and card titles as `h3` under the dialog's `h2`), Shop/Details (title card `h1`, type chip right, `h2` card titles) and the phone page. The phone dialog keeps `Back to Shop` (the default); the dialog fragment drawn for the page behind it is Shop/Index.
- **Management and form pages.** Desktop: dashboard card `h1`, three `.modern-card--aside` sections with the foundation `.table` in `.table-responsive`, the shared status badge, type chips, icon-only filled actions with `aria-label`s, the denial block with its 4px danger stripe. Phone: slips with 44px actions and a visually hidden `h1`. Create and Edit follow the form contract (required markers, price field with helper buttons on desktop only, availability window as an `h2`, Edit status alert, review notice).
- **Preview dialog.** Drawn with the real Bootstrap bundle and `mock-preview.js` (clones a template, writes `textContent` and `classList`, no markup from strings): the card replica is computed from the form's current values, so the four states are the dialog's own output.

## Frames by section (frame name, BEFORE manifest id)

All 76 package B frame definitions are in `tools/frame-defs-99.json`; every frame records `overflowX: false` and no console, page or request error. 96 of 110 frames have a side-by-side pair in `png/pairs/` (the proposals that have no baseline equivalent do not).

**Emblem (53 frames, BEFORE `shop-modal` for the dialog, `details-*` for the page).** `emblem-{page|dialog}-{key}-{desktop|phone}` for the five rarities (`common`, `uncommon`, `rare`, `veryrare`, `legendary`), the four type glyphs (`equipment` fa-hammer, `magic` fa-magic, `quest` fa-scroll, `box` fa-box), `longname` (the 66-character gauntlets name) and `wideimage` (a description holding `/images/notice-board-1600.webp`), plus `emblem-{page|dialog}-{longname|wideimage}-spacing-{desktop|phone}` (line height 1.5, letter spacing 0.12em, word spacing 0.16em applied by the capture tool) and the tracer frame `emblem-dialog-legendary-1440`. The `box` glyph is the partial's fallback for a type outside the enum; the enum has three values, so that frame labels its chip `Equipment` and exists to show the fallback tile.

**Preview (BEFORE `mgmt-create`).** `preview-filled`, `preview-blank`, `preview-longname`, `preview-legendary`, each opened by clicking Preview in the real page.

**Look check (pages BEFORE `details-published`, `mgmt-index`, `mgmt-edit-draft`).** `look-details-desktop`, `look-details-phone`, `look-details-dialog-desktop`, `look-details-dialog-phone`, `look-mgmt-desktop`, `look-mgmt-phone`, `look-form-desktop`, `look-form-phone`.

**Proposals.** `proposal-08` (Sell row confirmation), `proposal-09` (desktop client alerts), `proposal-10` (Denied and Archived notice, four panels), `proposal-12` (phone dialog Back to Shop, 390), `proposal-13` (hidden against visible phone `h1`, empty-state button secondary against primary, 390), `proposal-14` (dialog load error), `proposal-21` (back control label, desktop), `proposal-21-phone` (the same at 360 with the row-fit measurement), `proposal-22` (Deny dialog dismiss), `proposal-04-edit` (Preview on Edit), `mockup-h-header` (wood header beside the steel-blue `--qb-action` header; contrast wood 5.63, steel 6.46, recorded in `frames.json`). Each page's `h1` names its Gate 1 item number and each column is labelled Today or Proposed.

## Whole-picture results (`emblem/whole-*.json`, 54 records)

Every record has `glyphInside: true` and `glyphSmaller: true` for its tile (computed `aspect-ratio: auto`, `overflow: visible`, `minBlockSize` 144px desktop and 120px phone, glyph 64px and 48px) and every image `renderedWidth <= containerWidth` with the natural proportions kept (`ratioKept: true`, computed `aspect-ratio: auto`, `object-fit: fill`). The wide image (1600x1067) renders at 290px wide in the phone dialog (container 290), 274px on the phone page under the stress, and 1196px on the desktop page (container 1196), never wider. Under the stress the glyph box grows to 53.8x72 at 48px and still lies inside the 120px tile. Every dialog record has `bodyScrolls: true` with `overflow-y: auto` and `horizontalScroll: false`, and the dialog lies inside the viewport at 390. A negative control (a tile forced to 40px high with hidden overflow, in a scratch file outside the package) is reported by the tool as `glyphOutside=1`, so the check can fail.

## Preview focus order (`preview/focus-order.json`, real Bootstrap modal, filled state)

`entersOnOpen: true` (focus lands on the dialog element, `#itemPreviewModal`), `tabStaysInside: true` (order: dialog, header Close, footer Close, then the document body as the wrap stop, then header Close again; no control of the page behind it took focus), `shiftTabStaysInside: true`, `escReturnsToPreview: true` and `closeReturnsToPreview: true` (focus is back on `#previewButton` after Esc and after the Close button). The body stop between the last and first control is the browser's own wrap point; it is recorded as `bodyWrapStops`.

## Look-check results (`lookcheck.json`, 9 package B entries, 313 rows)

| Entry | Rows | Differences | Pass |
|-------|------|-------------|------|
| `emblem-dialog-legendary-1440` | 41 | `legendary-glow`, `btn-border` | yes |
| `look-details-desktop` | 33 | none | yes |
| `look-details-phone` | 29 | `phone-navy` | yes |
| `look-details-dialog-desktop` | 41 | `legendary-glow`, `btn-border` | yes |
| `look-details-dialog-phone` | 41 | `phone-navy`, `legendary-glow`, `btn-border` | yes |
| `look-mgmt-desktop` | 44 | `legendary-glow` | yes |
| `look-mgmt-phone` | 16 | `phone-navy`, `legendary-glow` | yes |
| `look-form-desktop` | 34 | three beyond the eight (below) | yes |
| `look-form-phone` | 34 | `phone-navy`, two beyond the eight | yes |

Theme properties (card, nested aside card, dialog overlay and commit button, chips, mgmt slip) are compared with `shop-a`; structure properties (title band, page title, aside title, form label, help text, action row) with `quest-details*` and `quest-form*` with `data-theme="shop"` applied to the reference. Each difference is equal or a named id; the ids are listed per entry.

## Gate 1 decision items found while drawing

1. **Three look-check differences are not among the eight listed ones** (named in `lookcheck.json` under `beyondEight`; each traces to the shipped foundation, not to the shop mock): (a) the Quest form keeps a parchment page title by its own foundation rule, the shop forms draw the gold page title the contract gives converted pages (operator: gold as drawn, or parchment like the Quest form); (b) the foundation form label is 14px, the Direction A quest-form render draws 16px (the contract table says 14px); (c) the foundation action row spaces the exit left and the commit right, the render's row is start-aligned. Redrawing against the stale render would contradict the contract, so (b) and (c) are recorded for acceptance and (a) is a real choice.
2. **Phone back control at 360px.** `proposal-21-phone` measures the action row: today's Create row (`Cancel` + `Create Item`) fits on one row, but `Back to Management` + `Save Changes` (today's Edit) already squeezes the commit button to 59px, and `Back to Shop Management` squeezes the commit to 44px on both pages. Only the foundation's stacked form (`form-actions--stack`) fits, and it is drawn. The operator may want the stacked form on Edit and Create phone whichever label wins item 21.
3. **Preview with no rarity chosen.** The blank state hides the rarity chip (a card has one only once a rarity is picked) and shows `0.00 gp` and `1 left` (the Create default quantity is 1). Alternatives: show a neutral chip, or leave the quantity blank.
4. **Hint under the price field on Edit.** The mock fills the rarity guidance on load (as the contract says the module does), so the first line `Select a rarity to see pricing suggestions.` shows only on a rarity-less form.
5. **Deny dialog commit label.** The contract text says `Deny`; the view's existing word is `Deny Item`, which the mock keeps (no new string).
6. **Coverage of the Gate 1 items.** Items 8, 9, 10, 12, 13, 14, 21, 22 and 4 are drawn here with their default beside the alternative; the other Gate 1 items (1 to 3, 5 to 7, 11, 15 to 20) are in package A or are text decisions needing no drawing. The `Nov 5` change (item 11) shows in `emblem-dialog-legendary-1440` (`Available from Nov 5 to Nov 19, 2026`).

## Deviations from Plan

**1. [Rule 3 - Blocking] Tracer feedback gate satisfied by an automated end-to-end check**
- **Issue:** this sequential run cannot be resumed after a human-verify checkpoint (same as plans 99-04 and 99-05).
- **Fix:** the tracer's `<verify>` was run end to end (scan, whole-picture, the look check) and the frame was read before any expansion task. Tracer verified end-to-end, expanding.

**2. [Rule 3 - Blocking] The plan names `lookcheck.mjs --out`, the Phase 98 tool**
- **Fix:** package A's `lookcheck-99.mjs` was used and extended (a `kind: "struct"` row type, a reference page with the navy theme applied, a `beyondEight` list); entries are keyed by frame name, so `emblem-dialog-legendary-1440` is the key the verify reads.

**3. [Rule 1 - Bug] Two builder defects found by reading the first renders**
- A teaser was HTML-escaped twice (`&#x27;`) and the Edit price hint ignored the chosen rarity. Both fixed and every package B frame re-captured.

**4. Look-check differences beyond the eight**
- See decision item 1. The plan says a difference outside the eight fails the mock; three exist because the Direction A quest-form render predates the shipped foundation and the contract. They are named, not hidden.

**5. Additional frames beyond the plan's list**
- `look-details-dialog-desktop|phone`, `proposal-21-phone`, `emblem-*-spacing-*` for both contexts, and the type glyph frames in both contexts (the plan's minimum was 14 emblem frames; there are 53).

## Known Stubs

None in the repository (no repository file was created or changed). The mocks use `href="#"` and `action="#"` on purpose; the Markdown editor is drawn as a plain textarea because it is a shared component the phase does not restyle.

## Threat Flags

None. The mocks hold only fixture item names and amounts, DM names are invented (`Archmage Pell`, `Brother Alder`), and a grep of the package's HTML, CSS and scripts for `localhost`, `127.0.0.1` and `@example` finds nothing.

## Self-Check: PASSED

Present: `mockups/v1/frames.json` (110 frames), `lookcheck.json` (32 entries, the 9 package B ones all pass), `emblem/whole-*.json` (54), `preview/focus-order.json`, `site/shop-form.css`, `tools/whole-picture.mjs`, `tools/mock-scan.py` (95 files, 0 hits). All 16 frames the Task 3 verify names exist and no `look-` entry fails. `git status --porcelain -- QuestBoard.Service QuestBoard.IntegrationTests` prints nothing.
