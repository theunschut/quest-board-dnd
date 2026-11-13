---
phase: 98-characters-contacts-dm-profile-and-players-ui-conversion
plan: 04
subsystem: gate1-package-a
tags: [ui-review, mockups, crop-dialog, forms, behaviour-check, scratch]
requires: [98-01, 98-02, 98-03]
provides:
  - shippable-shape crop dialog mockup (partial markup, one @layer pages sheet, ES module on the vendored Cropper 2.1.1) with a behaviour check on desktop and phone (the crop slice's baseline)
  - 63 named frames with probes (png/report.json) and frames.json pairing each form frame with its BEFORE
  - form-contract.css (components layer) and form-pages.css (pages layer) in the shape they would ship
  - measure.json (action row fit at 360 and 390), imageblock-measure.json, look-check.json (computed-style equality with the Direction A values)
  - Characters Create and Edit merge proposal page (d09-proposal-sheet)
affects: [98-05, 98-07, crop slice, form slices]
tech-stack:
  added: []
  patterns: [mockups served from a throwaway 127.0.0.1 static server so ES modules load as in the app; probeScope dialog scores the dialog alone; saved baseline pages (8311 only) reworked by a build script, one markup rendered under both shells]
key-files:
  created: []
  modified: []
decisions:
  - "The crop dialog partial does not carry #fileSizeError: a refused file never opens the dialog, so the chip stays in the host form beside the file input (the partial's header comment states the host contract)"
  - "The first selection is fitted inside the picture (BEFORE it was 256px tall on a 212px picture on the phone for a wide photo); Reset does the same"
  - "The selection's body tint is a parchment wash from tokens (the old gold literal in the view is gone), keeping gold for the focus ring"
metrics:
  tasks: 3
  commits: 1
status: complete
actuals:
  tokens: 25000
  tasks: 3
  commits: 1
---

# Phase 98 Plan 04: Gate 1 package A Summary

The crop dialog (control row, keyboard path, focus order), the form action row on all six form pages, the picture block, the Characters Create and Edit merge proposal and the form-title colour are drawn as scratch mockups on a foundation copy of the repository at `3b25a4df` (a descendant of `c781090c`, recorded in `site/PROVENANCE.txt`), measured, and the crop dialog is behaviour-checked on both hosts (27 checks on desktop, 28 on the phone, all passing). No repository file was created or changed; the only commit is this summary. No repository tests were run (the plan has none).

Everything is under `~/qb-ui-review/phase-98/mockups/v1/`: `crop/` (`_ImageCropDialog.html`, `image-crop.css`, `image-crop.js`, hosts `index.html` and `index.mobile.html`, `behaviour-check.json`, `behaviour-check.mobile.json`), `forms/` (`form-contract.css`, `form-pages.css`, `*.after.*.html`, `saved/` baseline pages, `measure.json`, `imageblock-measure.json`, `look-check.json`, `d09-proposal.html`), `site/`, `png/` (63 frames, `report.json`, `crop-behaviour.webm` 2.2 MB), `frames.json`. New tools in `tools/`: `frames-shoot-98.mjs`, `crop-behaviour.mjs`, `serve-v1.mjs`, `save-pages-98.mjs`, `build-crop-mockup.py`, `build-forms-mockup.py`, `forms-measure.mjs`, `look-check-98.mjs`.

## Tasks

1. **Tracer (crop dialog, desktop 1440):** foundation copy and provenance, partial markup, shippable sheet and module, host built from the saved `/Characters/Create`, captured with the selection focused. Both static assertions printed `ok`; the 1440 frame has no contrast failure and no overflow. Tracer gate: config has no auto mode (`workflow._auto_chain_active` and `workflow.auto_advance` both false), but this run is a sequential subagent that cannot be resumed and whose orchestrator presents Gate 1, so the gate was the automated re-verification of the tracer's verify (passed), the same way 98-02 and 98-03 did; expansion went ahead.
2. **Every crop frame and the behaviour check:** 16 crop frames, `crop-behaviour.mjs` on both hosts (mouse, Pixel 7 with touch, reduced motion), a recording, all passing.
3. **Form frames:** 47 frames (six pages by five action-row states, four pages by three image-block states, the merge proposal, the two titles), the 360 fit measurement, image-block heights, a computed-style look check.

## Crop dialog: contrast (AFTER measured on the rendered dialog; BEFORE from `before/crop-probe.json`)

| Pair | BEFORE (baseline, same dialog) | AFTER |
|---|---|---|
| Title `Crop Your Photo` | 15.52:1, 16px | 12.98:1, 20px (`--qb-fg` on `--qb-surface-overlay`) |
| Instruction text | 11.79:1, 14px | 12.98:1, 16px |
| Control labels (seven `btn-secondary`) | not present | 6.09:1 |
| Discard Photo | 6.09:1 | 6.09:1 |
| Use This Crop | 8.92:1 (`btn-warning`) | 7.10:1 (`btn-primary`) |
| File-size chip text | not drawn | `--qb-danger-text` on the overlay chip, 10.05:1 per the contract |

Contrast probe over the dialog alone (host form hidden for the probe, `probeScope: dialog`): 0 failures on every crop frame at 1440, 320 desktop, phone 390 and 360, taborder, focus, instruction A and B, status, text-spacing. Computed-style look check (`forms/look-check.json`): the form card, its title band, the desktop gold `h1` (`rgb(255,193,7)`, Cinzel 28px 600), the phone parchment 20px `h1` and the dialog surface (`#2f1b0e`, parchment text) are all equal to the Direction A values; the form card carries the glass 0.12 over scrim 0.42, 1px 0.3 border, 12px radius and `blur(12px)`.

## Crop behaviour checks (`behaviour-check.json`, `behaviour-check.mobile.json`)

Both hosts pass: `move-up`, `move-down`, `move-left`, `move-right` (8px each, nothing else changes), `grow`, `shrink` (8px, centre kept, ratio 1), `clamp-edges` (30 presses toward each edge from a fresh Reset), `clamp-min` (32px), `clamp-max` (largest square, inside the picture), `first-box-inside` (wide, tall and nearly square pictures), `reset` (equals the first box), `keys-arrow`, `keys-shift-resize`, `keys-ignored-modifier` (Ctrl, Alt, Meta, Ctrl+Shift), `delete-does-nothing` (Delete, Backspace, Escape), `no-document-keydown` (bootstrap registers its own three keydown and one keyup; the module and Cropper register none), `selection-keyboard-attribute-absent`, `tab-order` (Close, selection, the seven controls, Discard Photo, Use This Crop, then back to Close), `targets` (desktop controls at least 36px; phone every control at least 44px), `phone-control-rows` (four arrows on row one, three word buttons on row two), `no-hscroll` (1440 and 320 desktop; 390 and 360 phone), `textspacing` (nothing leaves the dialog or spills from a button), `status-written` (a burst of five presses writes at most two sentences, at least 500ms apart, the agreed wording, textContent, polite status region), `confirm-populates` (one JPEG in the hidden input), `discard-keeps`, `file-size-error`, `recrop-opens`, `reduced-motion`. `crop-phone-390` reports `mobileLayout` true, no overflow, no target under 44px.

## Form action row: 360 fit (`forms/measure.json`)

| Page | Exit | Commit | One row at 360 | One row at 390 |
|---|---|---|---|---|
| Characters Create | Cancel | Create Character | **no** (commit label wraps to two lines, 66px tall) | yes |
| Characters Edit | Cancel | Save Changes | yes | yes |
| Contacts Create | Cancel | Create Contact | yes | yes |
| Contacts Edit | Cancel | Save Changes | yes | yes |
| DungeonMaster/EditProfile | Back to Profile | Save Profile | **no** (commit squeezed to 90px and three lines) | **no** (commit two lines) |
| ContactCategoryManagement/Edit | Cancel | Rename | yes | yes |

The `column-reverse` stack frames (`*-actions-stack-360`) show the commit first on screen with the exit below it; markup order is unchanged.

## Picture block (`forms/imageblock-measure.json`, px at 1440)

| Page | Empty under the picture block BEFORE / default / variant B | File input top against the Name input top, BEFORE / default |
|---|---|---|
| Characters Create | 163 / 163 / 0 | 0 / 0 |
| Characters Edit | 0 / 0 / 0 | 247 / 0 |
| Contacts Create | 74 / 74 / 0 | 0 / 0 |
| Contacts Edit | 0 / 0 / 0 | 247 / 0 |

Measured under the file input group itself (the input, its hint and the error chip): Characters Create 164 before and default, 170 under variant B (filled by the 160px tile); Characters Edit 1 before (thumbnail sat above the input) and 249 default (filled by the 200px thumbnail and Re-crop); Contacts Create 75 / 75 / 170; Contacts Edit 1 / 249 / 217. The label is level with the Name label in all states. On both Edit pages the default removes the 247px push.

## Merge proposal (`d09-proposal-sheet`, `d09-merged-create-1440`, `d09-merged-create-390`)

Verdict: both pairs qualify once the phone Classes label carries the required marker. The two non-CSS differences: (1) the Edit placeholders (the phone Edit has `Enter character name` and `https://...`, the desktop Edit none; the merged Edit takes Create's placeholders); (2) the class-row start index (desktop Edit is off by one when a character has no classes; the phone is correct). Default: no merge unless the operator says an explicit yes. The sheet lists the test cost (`MobileViewsTests` Create and Edit facts, a phone case for `MarkdownEditorMarkupTests`, the layer guard and area guard lists) and what goes (two phone views and one sheet).

## Form title (`form-title-gold`, `form-title-parchment`)

Gold (the foundation default) 7.63:1 over the title band at the lightest board pixel; parchment (as on the Quest form) 9.88:1. Both pass; the form pages' contrast probe is 0 failures on every AFTER frame (BEFORE: 168 of 217 baseline captures had at least one).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] The first crop box started outside the picture on the phone**
- **Found during:** Task 2 (behaviour check on the phone host)
- **Issue:** Cropper sizes the first selection from the stage (80% of 320px = 256px), which is taller than a wide picture fitted into the phone stage (212px), so the box began outside what is cropped and the first press snapped it by 22px instead of 8px.
- **Fix:** the module fits the first box inside the picture (keeping its centre) after the open sequence and after Reset; `first-box-inside` covers wide, tall and nearly square pictures. Edge maths uses a 0.01px tolerance, because layout reports edges with floating-point noise that otherwise moved a bound by a whole pixel.
- **Files modified:** `crop/image-crop.js` (scratch)

**2. [Rule 1 - Bug] The refusal chip cannot live inside the dialog**
- **Found during:** Task 1 (partial markup)
- **Issue:** the contract's structure lists `#fileSizeError` in the modal body, but a file that is too large, the wrong type or unreadable is refused before the dialog opens, so a chip inside the closed dialog would never be seen (the baseline shows it under the file input).
- **Fix:** the chip (role alert, icon, `hidden` attribute, overlay surface) is drawn in the host form beside the file input; the partial's header comment states that contract, and the static assertion that the partial names `fileSizeError` is met by that comment. Gate 1 and the crop slice should read it that way.

**3. [Rule 2 - Missing critical] No colour literal in the partial, no empty grid role**
- The old selection markup carried `theme-color="rgba(255,193,7,0.35)"` (a colour literal in a view, and a gold use outside the reserved list). The move handle's tint is now set in `image-crop.css` as a parchment wash from `--qb-fg`. The decorative grid lost `role="grid"` (an empty grid is invalid) and the grid and crosshair are `aria-hidden`.

### Plan interpretations and additions

**4. `discard-keeps` means "keeps today's behaviour".** The plan words it as "Discard Photo leaves the original", but the baseline's Discard Photo empties both the chosen file and the cropped input (checked against 8311 with no write: chosen 1 and cropped 1 before, 0 and 0 after). The check asserts that, and the record says so. If the operator wants the chosen file kept, that is a behaviour change to put to Gate 1.

**5. Mockups are served, not opened from `file://`.** Module scripts are refused from `file://`, so the tools serve `mockups/v1` from a throwaway static server on 127.0.0.1 at a free port (never 8310 or 8311) and close it afterwards; the module is loaded exactly as the app will. Frame PNGs, not live pages, go into the Gate 1 package.

**6. Probe scope for the dialog frames.** The contrast probe scores all visible text, including the host form behind the backdrop, which carries the baseline's own failures. For dialog frames the host is hidden for the probe only (after the screenshot), so the dialog is scored alone; `crop-filesize-error-phone-390` is a host-page frame and still reports the baseline's 5 failures (help text and required marker) that the form-page rules fix.

**7. BEFORE for phone 360** is the 390 capture (no 360 capture exists). The Characters frames use one markup under both shells (what the merge renders; a twin conversion renders the same action row).

**8. Bootstrap CSS must not be linked unlayered.** The merge-proposal sheet first rendered on white because a plain `bootstrap.min.css` link beats the layered theme; the foundation imports it into its vendor layer. Worth remembering for the Gate 1 package pages.

## Decisions Gate 1 will need

1. Crop instruction text: variant A (default, names keys and buttons) or B (shorter).
2. Crop loading: no spinner (default); the 5 MB picture shows no change while it decodes.
3. Action row on the phone: the default single row fails the 360 fit on Characters Create (`Create Character`) and does not fit even at 390 on DM EditProfile (`Back to Profile` beside `Save Profile`). Options: use the stack only on those pages, stack on every form, or let the commit label wrap to two lines. The contract's rule gives the stack on those two pages.
4. Picture block: default (shared top edge) or variant B (the 160px preview tile; closes the 163px gap on Characters Create and 74px on Contacts Create).
5. Characters Create and Edit merge (default no) and the placeholder question for desktop Edit.
6. Form title: gold (default) or parchment.
7. Newly surfaced: the selection tint is parchment, not gold; the first box is fitted inside the picture; the status sentence reports `left` and `top` relative to the picture's top left corner; the refusal chip stays in the host form; Discard Photo keeps emptying the chosen file.

## Known Stubs

None. The mockup hosts carry frame-only helpers (`crop-frame.js`, `crop-frame.css`, `forms-frame.js`, `d09-proposal.*`); they are labelled and are not part of the shipping shape.

## Threat Flags

None. No repository file, endpoint or auth path changed; saved pages come from 8311 only, carry p98 fantasy fixtures, have antiforgery values blanked and stay in scratch; the vendored Cropper is the repository's own copy; nothing was installed.

## Self-Check: PASSED

- `site/PROVENANCE.txt` names `3b25a4df05d8510e11b341aca701d56231a4e5a9` with the ancestry result `yes`; `crop/index.html` has no `localhost`.
- Task 1, Task 2 and Task 3 automated checks printed `ok`; the GSD-id grep over the module, sheets, partial and form sheets finds nothing; no email address appears in any saved page or frame page and no antiforgery value remains.
- 63 frames in `png/report.json`, every PNG exists, `png/crop-behaviour.webm` is 2.2 MB; `frames.json` names every required crop, action-row, image-block, merge and title frame, each form frame paired with its BEFORE.
- `git -C /mnt/Data/repos/quest-board-dnd status --porcelain` was empty before this summary was written. 8311 (not started here) is still running; 8310 is stopped; the throwaway servers were closed.
