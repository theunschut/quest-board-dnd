---
phase: 98-characters-contacts-dm-profile-and-players-ui-conversion
plan: 09
subsystem: crop-dialog
tags: [ui-conversion, crop-dialog, es-module, accessibility, keyboard, shared-partial]
requires: [98-08]
provides:
  - "Views/Shared/_ImageCropDialog.cshtml: the one crop dialog (opaque overlay, control row, focusable selection, polite status line)"
  - "image-crop.js as a self-initialising ES module driven by data-crop-input, data-crop-output-name, data-crop-aspect-ratio and data-crop-original-url"
  - "image-crop.css as one @layer pages block (stage, control row, refusal chip, phone rules)"
  - "ImageCropDialogRenderTests: five host pages under both User-Agents"
  - "crop-behaviour.mjs --row <id> --port N live mode (scratch tool)"
affects: [98-10, 98-13, 98-14, 98-15, 98-16, 98-17]
tech-stack:
  added: []
  patterns: [one shared partial instead of ten copies; module wires itself from data attributes with no global function; keys heard on the selection element only; refusal chip hidden by the hidden attribute]
key-files:
  created:
    - QuestBoard.Service/Views/Shared/_ImageCropDialog.cshtml
    - QuestBoard.IntegrationTests/Tests/ImageCropDialogRenderTests.cs
  modified:
    - QuestBoard.Service/wwwroot/js/image-crop.js
    - QuestBoard.Service/wwwroot/css/image-crop.css
    - QuestBoard.Service/Views/Characters/Create.cshtml
    - QuestBoard.Service/Views/Characters/Create.Mobile.cshtml
    - QuestBoard.Service/Views/Characters/Edit.cshtml
    - QuestBoard.Service/Views/Characters/Edit.Mobile.cshtml
    - QuestBoard.Service/Views/Contacts/Create.cshtml
    - QuestBoard.Service/Views/Contacts/Create.Mobile.cshtml
    - QuestBoard.Service/Views/Contacts/Edit.cshtml
    - QuestBoard.Service/Views/Contacts/Edit.Mobile.cshtml
    - QuestBoard.Service/Views/DungeonMaster/EditProfile.cshtml
    - QuestBoard.Service/Views/DungeonMaster/EditProfile.Mobile.cshtml
decisions:
  - "Ported the Gate 1 approved dialog (v1) unchanged: instruction A, status sentence relative to the picture, no spinner, parchment selection tint, first box fitted inside the picture, refusal chip stays in the host form, Discard Photo still empties the chosen file and the cropped input"
  - "The refusal chip in each host form becomes the approved overlay chip (class crop-error, role alert, icon, hidden attribute) because the module toggles hidden, not an inline display style"
  - "No transitional wrapper for the removed global initialiser: all ten hosts swap in the one commit"
metrics:
  tasks: 3
  commits: 1
status: complete
actuals:
  tokens: 40000
  tasks: 3
  commits: 1
---

# Phase 98 Plan 09: Crop dialog core Summary

One shared crop dialog partial, a self-initialising ES module with a seven-button control row and a keyboard path, and a layered sheet now serve all ten host views (Characters, Contacts and DM profile, each with its phone twin), proven by rendered tests under both User-Agents and the live behaviour check on every host.

## What changed

Commit `331e7fd8` is the only code commit for this plan (Tasks 1 to 3 landed together, as the plan requires, so no committed state has a host calling the removed initialiser).

- **Partial** `Views/Shared/_ImageCropDialog.cshtml`: ported from the approved drawing. Opaque modal surface with no colour utility, no white close variant and no style attribute; `h2` "Crop Your Photo" bound by `aria-labelledby`; the approved instruction text; the class-sized `crop-stage`; a `cropper-selection` with `tabindex="0"`, `role="group"` and a name; the `Adjust crop` group with Move up, Move down, Move left, Move right (named by `aria-label`), Larger, Smaller and Reset; a polite visually hidden status line; Discard Photo (`cropCancelBtn`) and Use This Crop (`cropConfirmBtn`).
- **Module** `image-crop.js`: a header comment states what it does and that it exposes nothing globally and builds no markup from strings. Moves 8px per control or arrow key, resizes 8px with Larger, Smaller or Shift plus an arrow (keeps centre and ratio), clamps inside the picture between 32px and the largest square, Reset reruns the open sequence, writes the throttled status sentence with `textContent`, listens for keys on the selection only and never sets Cropper's `keyboard` option. Keeps the 5 MB and type checks, the prepare-and-blob path, the hidden-input population, the discard path and the re-crop path from the stored original. The four old global functions are gone.
- **Sheet** `image-crop.css`: one `@layer pages` block, no important flag, no colour literal; phone stage height and touch guard under `.mobile-layout` (now applying to the phone twins for the first time), the wrapping control row, the refusal chip.
- **Ten hosts**: partial tag in place of the dialog copy; `data-crop-input`, `data-crop-output-name="CroppedPictureFile"` and `data-crop-aspect-ratio="1"` on the picture file input; `data-crop-original-url` on the Re-crop button (the URL the inline call passed); the cropped-file input hidden with `hidden` and never disabled; the sheet linked from `@section Styles`; the classic Cropper script then the module in `@section Scripts`; the inline initialisation removed. Class-row and Tagify scripts untouched. Controllers, validators, the crop output format and all POST fields are untouched.
- **Tests** `ImageCropDialogRenderTests` (46 cases, written first and seen failing 46 of 46): for each of Characters Create, Characters Edit, Contacts Create, Contacts Edit and DungeonMaster/EditProfile under a desktop and a phone User-Agent it asserts the dialog title, control-row names, focusable selection, footer buttons, status region, no colour utility or style attribute, the sheet link, the module script, the classic Cropper script, no inline initialiser, the data attributes on the file input, the cropped input hidden by attribute and not disabled, the refusal chip hidden with `role="alert"`; and on the three Edit pages under both User-Agents the Re-crop button carries a stored-original URL.

## Live behaviour check (crop-behaviour.mjs, Gate 1 baseline)

`crop-behaviour.mjs` gained a live mode (`--row <manifest id> --port N`, scratch tool outside the repo): it opens the real page of a manifest row with that row's role state, generates the test picture in the page and runs the same checks as the mockup baseline. Results in `~/qb-ui-review/phase-98/after/98-09/behaviour-<row>-<ua>.json`.

| Row | Desktop | Phone (Pixel 7, touch) |
|---|---|---|
| crop-char-create | 26 pass, 0 fail | 27 pass, 0 fail |
| crop-char-edit-recrop | 27 pass, 0 fail | 28 pass, 0 fail |
| crop-contact-create | 26 pass, 0 fail | 27 pass, 0 fail |
| crop-contact-edit | 27 pass, 0 fail | 28 pass, 0 fail |
| crop-dm-edit | 27 pass, 0 fail | 28 pass, 0 fail |

The baseline has 27 desktop and 28 phone checks; the host pages without a Re-crop button (the two Create pages) have no `recrop-opens` check, hence one fewer. All checks match the baseline by name: moves, grow and shrink, clamps, first box inside, reset, arrow and Shift-arrow keys, ignored modifiers, Delete does nothing, no document key listener, tab order, targets, status sentence, confirm populates, discard keeps, file-size error, no horizontal scroll at 1440 and 320 (390 and 360 on the phone), text-spacing stress, reduced motion. A separate run without CSP bypass on Characters Create under both User-Agents logged no console errors, and the screenshots of the open dialog match the approved drawing.

## Test results

- Unit: 1416 passed, 0 failed, 0 skipped, 1416 total.
- Integration: 2074 passed, 0 failed, 16 skipped, 2090 total (2028 before plus the 46 new cases).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Refusal chip markup changed in all ten hosts**
- **Found during:** Task 1
- **Issue:** The plan's host-edit list does not name the `#fileSizeError` element, but the approved module shows and hides it with the `hidden` attribute, and the hosts' chip carried `style="display: none;"` (an inline style that would keep it hidden forever and that the no-inline-style rule forbids).
- **Fix:** Replaced the element in each host with the approved chip (`class="crop-error" role="alert" hidden`, an icon and a `data-crop-error-text` span). Position in the form is unchanged (the size chip stays in the host form, Gate 1).
- **Files modified:** the ten host views. **Commit:** `331e7fd8`

**2. [Rule 3 - Blocking] crop-behaviour.mjs extended for live pages**
- **Found during:** Task 1
- **Issue:** The plan's verify and acceptance criteria call `crop-behaviour.mjs --row <id> --port N`, which the Gate 1 tool did not support (mockup files only).
- **Fix:** Added a live mode to the scratch tool (role state and board from the manifest, path placeholders from `fixtures.json`, the frame driver's picture helpers injected, the input found through `data-crop-input`, CSP bypassed only for the text-spacing probe's style tag). Mockup mode is unchanged.
- **Files modified:** `~/qb-ui-review/phase-98/tools/crop-behaviour.mjs` (scratch, not in the repo).

**3. [Rule 1 - Naming] Module-private helper renamed**
- **Found during:** final acceptance grep
- **Issue:** The acceptance grep requires no top-level `function setCroppedFileInput` (and the other three names) in the module; the approved module kept a module-scope (not global) function of that name.
- **Fix:** Renamed it `fillFileInput` (private to the module, behaviour identical); the live behaviour check was re-run on the phone Edit page afterwards (28 pass).

**Commit shape:** the plan asks for one commit at the end of Task 3 and that is what was made (amended once to include deviation 3 before anything was pushed; nothing is pushed).

## Authentication gates

None.

## Known Stubs

None.

## Threat Flags

None. No new endpoint, field or trust boundary; the module writes text only through `textContent` and builds no markup from strings (T-98-23), the client pre-check and the server validators are unchanged (T-98-24), the re-crop URL is the same authenticated picture route rendered through Razor's attribute encoding (T-98-26), and all ten hosts moved together (T-98-58: no view contains the removed initialiser, checked by grep and by walking the commits since the plan start).

## Notes for the next plans

- `_Layout.cshtml` still links `image-crop.css` globally; it leaves in 98-10 together with the guard facts, the layer-guard arrays and the contrast probe. The hosts now link it themselves, so the removal is safe.
- The still-unconverted hosts keep their own inline class-row and Tagify scripts for their own slices.

## Self-Check: PASSED

- Files exist: `_ImageCropDialog.cshtml`, `ImageCropDialogRenderTests.cs`, `image-crop.js`, `image-crop.css`, all ten host views.
- Commit `331e7fd8` exists on `milestone/v9-rolling-improvements`.
- `grep -rc initImageCrop QuestBoard.Service/Views` is 0 for every file; 10 of 10 host views link `image-crop.css`; 0 top-level legacy functions in the module; no planning reference in the partial, module, sheet or tests.
