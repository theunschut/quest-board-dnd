---
phase: 98-characters-contacts-dm-profile-and-players-ui-conversion
plan: 10
subsystem: crop-dialog
tags: [ui-conversion, crop-dialog, guard-tests, layout, contrast-probe, behaviour-check]
requires: [98-09]
provides:
  - "CharactersConversionGuardTests facts CropDialogPartial_UsesTheOverlaySurface, ImageCropModule_DrivesTheSelectionThroughItsPublicApi, CropModuleScanners_FlagWhatTheyForbid and Layouts_LinkNoAreaSheet (image-crop.css first in PageOnlySheets)"
  - "image-crop.css out of _Layout.cshtml; every host links it from its own Styles section (render test asserts exactly one link per page under both User-Agents)"
  - "UiFoundationLayerGuardTests LayeredFiles and NoLiteralFiles list image-crop.css"
  - "PhaseSourceFiles scans the partial, module, sheet, render tests, layout and the ten host views for planning references"
  - "Measured values for Gate 2: dialog contrast, Cropper handle size, 5 MB open time (scratch records in after/98-10)"
affects: [98-13, 98-14, 98-15, 98-16, 98-17]
tech-stack:
  added: []
  patterns: [static source-scan facts with a known-bad self-test so a dead regex cannot pass; page sheet loaded from the Styles section only]
key-files:
  created: []
  modified:
    - QuestBoard.Service/Views/Shared/_Layout.cshtml
    - QuestBoard.IntegrationTests/Tests/CharactersConversionGuardTests.cs
    - QuestBoard.IntegrationTests/Tests/UiFoundationLayerGuardTests.cs
    - QuestBoard.IntegrationTests/Tests/ImageCropDialogRenderTests.cs
decisions:
  - "Added a CropModuleScanners_FlagWhatTheyForbid self-test (not in the plan) so the module regexes are shown to fire on a known-bad sample, matching the existing Scanners_FlagWhatTheyForbid pattern in the same file"
  - "Added the ten host views and _Layout.cshtml to PhaseSourceFiles in addition to the crop files, because 98-09 changed them and the plan asks for every file 98-09 and this plan changed"
metrics:
  tasks: 3
  commits: 1
status: complete
actuals:
  tokens: 9000
  tasks: 3
  commits: 1
---

# Phase 98 Plan 10: Crop guards and layout move Summary

The crop dialog landed in 98-09 is now pinned by source-scan guard facts, its sheet no longer loads on every page, and the dialog is proven on every host under both User-Agents (behaviour check, contrast probe, a 4.9 MB picture) with no regression against BEFORE in the sweep.

## What changed

Commit `806db5e7` is the only code commit. No view, partial, module or sheet changed: every guard fact passed on the 98-09 files.

- **`CropDialogPartial_UsesTheOverlaySurface`**: the partial (Razor comments stripped) holds no `bg-dark`, `text-light`, `btn-close-white` and no style attribute.
- **`ImageCropModule_DrivesTheSelectionThroughItsPublicApi`**: the module calls `$moveTo(`, `$change(` and `$initSelection(`; holds no `document`, `window` or `globalThis` key listener; sets no `keyboard` option; has no `innerHTML`, `outerHTML`, `insertAdjacentHTML` or `document.write`; assigns nothing to `window` or `globalThis`. A companion `CropModuleScanners_FlagWhatTheyForbid` fact runs the same regexes on a known-bad sample and a clean sample.
- **`Layouts_LinkNoAreaSheet`**: written first and seen failing against the unchanged layout (1 failed of 11), then `_Layout.cshtml` lost its `image-crop.css` link and it passed. It reads both main layouts; `image-crop.css` is the first entry of `PageOnlySheets`, where the characters, contacts and DM profile sheets are appended later.
- **Layer guard**: `("image-crop.css", "pages")` in `LayeredFiles`, `image-crop.css` in `NoLiteralFiles`.
- **Render test**: `Host_LinksTheSheetLoadsTheModuleAndCallsNoInlineInitialiser` now asserts exactly one `image-crop.css` link per page (five hosts, both User-Agents), proving the sheet still reaches each host after the global link went.
- **`PhaseSourceFiles`**: gains the partial, module, sheet, render tests, `_Layout.cshtml` and the ten host views; all scan clean.

## Test results

- Unit: 1416 passed, 0 failed, 0 skipped, 1416 total.
- Integration: 2078 passed, 0 failed, 16 skipped, 2094 total (2090 before plus the four new facts). `TestHostFileWatching.cs` untouched.

## Live behaviour check (8310, after the global link went)

`crop-behaviour.mjs --row <id> --port 8310`, records in `~/qb-ui-review/phase-98/after/98-10/behaviour-<row>-<ua>.json`. Every check passes on every host under both User-Agents (same check names as the Gate 1 baseline).

| Row | Desktop | Phone (Pixel 7, touch) |
|---|---|---|
| crop-char-create | 26 pass, 0 fail | 27 pass, 0 fail |
| crop-char-edit-recrop | 27 pass, 0 fail | 28 pass, 0 fail |
| crop-contact-create | 26 pass, 0 fail | 27 pass, 0 fail |
| crop-contact-edit | 27 pass, 0 fail | 28 pass, 0 fail |
| crop-dm-edit | 27 pass, 0 fail | 28 pass, 0 fail |

## Dialog contrast, BEFORE and AFTER (crop-probe.mjs, all ten host and User-Agent combinations)

| Element | BEFORE | AFTER |
|---|---|---|
| Dialog title | 15.52 | 12.98 |
| Instruction | 11.79 | 12.98 |
| Discard Photo label | 6.09 | 6.09 |
| Use This Crop label | 8.92 (gold fill) | 7.10 (wood fill) |
| Modal-wide contrast failures | 0 | 0 |

AFTER is identical on all ten combinations; every figure is above 4.5:1. The dialog was shown on all ten with no console error. Note that the dialog's own title and instruction measured above 4.5:1 in the BEFORE probe too (white on a dark panel); what the plan calls the BEFORE failures are the host-form text in the sweep (below), not the dialog. The dialog gets the shared overlay surface and its confirm button the wood action fill, as Gate 1 approved.

## Cropper handle size (for the closing plan's target-size exception record)

Measured on `crop-char-create` under both User-Agents with the 4000 by 3000 picture open: the four corner handles render 15 by 15 px and the four edge handles are 15 px thick (full side length long), identical on desktop (selection 320 px) and the phone (selection 256 px). That is below the 24 px desktop and 44 px phone targets, which is why the control row of Move, Larger, Smaller and Reset buttons (all at least 42 px tall, 46 px wide) is the non-dragging alternative. The exception record is for the drag handles only.

## 5 MB picture

`picture-5mb.jpg`: a 4000 by 3000 JPEG of 5,117,645 bytes (4.88 MiB, under the 5 MiB limit), opened on `crop-char-create` through the file input, three runs per User-Agent, time from choosing the file to the dialog shown with the picture laid out and the selection placed:

| User-Agent | Runs (ms) | Median |
|---|---|---|
| Desktop | 686, 675, 659 | 675 |
| Phone (Pixel 7, touch, desktop-class CPU in headless Chromium) | 677, 659, 660 | 660 |

Nothing else changes while it decodes: same URL, the refusal chip stays hidden, no horizontal scroll (1440 of 1440 and 412 of 412 px), no console error, and no spinner is added (the approved Gate 1 answer). The picture is downscaled to 2400 px before Cropper sees it. A real phone's CPU will be slower than this headless figure; the number is a floor, not a phone measurement.

## Sweep against BEFORE

Crop rows and `char-create`, `char-edit`, `contact-create`, `contact-edit`, `dm-editprofile` under both User-Agents at every width (40 records, 0 not 200) into `after/98-10`. `readability-compare.py` reports 0 failing rows, 0 poster-floor violations and prints `ok`, with an empty `allow.json`: no regression, so no allowance was needed (the dialog surface, controls and text changes were already inside the rows' tolerance). Per row the contrast-failure count is unchanged from BEFORE (3 to 6 per row, all in the host forms behind the dialog: the required-star `text-danger` marker and the `form-text text-muted` hints), with no overflow, console error or failed action change. Those host-form findings belong to the later host-conversion slices, not to the dialog.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 - Missing critical] Self-test fact for the module scanners**
- **Found during:** Task 1
- **Issue:** The plan lists two guard facts; a regex-only module fact would pass on an empty match whether or not the regex worked.
- **Fix:** Added `CropModuleScanners_FlagWhatTheyForbid`, which runs the same patterns on a known-bad and a clean sample, as the file's existing `Scanners_FlagWhatTheyForbid` does.
- **Files modified:** `CharactersConversionGuardTests.cs`. **Commit:** `806db5e7`

**2. [Rule 3 - Blocking] Task 2 verify command's JSON shape**
- **Found during:** Task 2
- **Issue:** The plan's inline Python reads `crop-probe.json` as an object keyed by row with numeric `titleContrast`; the real file is an array of records whose `titleContrast` is an object with a `ratio`.
- **Fix:** Ran the equivalent check against the real shape (every record: dialog shown, title and instruction ratio at least 4.5, behaviour checks all pass); result recorded above.
- **Files modified:** none (scratch evidence only).

**3. [Rule 3 - Blocking] Measurement script**
- **Found during:** Task 2
- **Issue:** The 5 MB open time and handle size have no existing tool.
- **Fix:** Added `~/qb-ui-review/phase-98/tools/crop-5mb-handles.mjs` (scratch, outside the repo).

**4. Scope note on the contrast acceptance wording:** the plan expects the title and instruction to be at least 4.5:1 "where BEFORE failed"; BEFORE did not fail for these two elements in the probe (see the contrast table), so there was nothing to fix and no host, partial, module or sheet changed.

## Authentication gates

None.

## Known Stubs

None.

## Threat Flags

None. No new endpoint or trust boundary; T-98-25 and T-98-59 are now held by the module facts, and T-98-60 by the one-link render assertion and the post-removal behaviour check.

## Self-Check: PASSED

- Commit `806db5e7` exists on `milestone/v9-rolling-improvements`.
- `grep -c "image-crop.css" QuestBoard.Service/Views/Shared/_Layout.cshtml` prints 0; the three fact names appear in `CharactersConversionGuardTests.cs`; `"image-crop.css"` appears twice in `UiFoundationLayerGuardTests.cs`.
- `git diff --quiet 16b604cd -- QuestBoard.IntegrationTests/TestHostFileWatching.cs QuestBoard.Service/Controllers` exits 0.
- Records exist under `~/qb-ui-review/phase-98/after/98-10/` (behaviour, crop-probe.json, crop-5mb.json, report.json, allow.json).
