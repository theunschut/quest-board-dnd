---
phase: 46-client-side-crop-ui
fixed_at: 2026-07-07T22:30:00+02:00
review_path: .planning/phases/46-client-side-crop-ui/46-REVIEW.md
iteration: 1
findings_in_scope: 5
fixed: 5
skipped: 0
status: all_fixed
---

# Phase 46: Code Review Fix Report

**Fixed at:** 2026-07-07T22:30:00+02:00
**Source review:** .planning/phases/46-client-side-crop-ui/46-REVIEW.md
**Iteration:** 1

**Summary:**
- Findings in scope: 5 (all Warnings; the 3 Info findings were left as-is per the reviewer's own "no action required" conclusion)
- Fixed: 5
- Skipped: 0

**Note on process:** Applied directly by the orchestrator rather than dispatched to `gsd-code-fixer`, since WR-01's fix required a genuine product decision (skip-crop-for-GIFs vs. accept-static-frame vs. defer) that the user made explicitly via a checkpoint before any code was touched. All 5 fixes landed in a single combined commit rather than one-per-finding, since they were small, interdependent (WR-03's refactor absorbed the file-validation logic WR-01/WR-02 also touch), and verified together as one unit.

## Fixed Issues

### WR-01: Cropped image is always JPEG-encoded regardless of source format, silently dropping GIF animation

**Files modified:** `QuestBoard.Service/wwwroot/js/image-crop.js`
**Commit:** `e7b51eb`
**Applied fix:** User selected "skip auto-crop for GIFs" (recommended option) over accepting the static-frame tradeoff or deferring. `initImageCrop`'s file-input `change` handler now checks `file.type === 'image/gif'` after validation passes and, if true, calls `resetCropState()` and returns without opening the crop modal — the original animated GIF submits untouched via the visible file input, and the server-side `CroppedImageData ?? OriginalImageData` fallback (established in Phase 45) displays the original everywhere since no crop is ever saved. Verified live: a synthetic GIF upload no longer opens the crop modal, leaves `CroppedPictureFile` empty, and the original file remains selected on the input.

### WR-02: Cropped file's declared extension no longer matches its actual re-encoded format

**Files modified:** `QuestBoard.Service/wwwroot/js/image-crop.js`
**Commit:** `e7b51eb`
**Applied fix:** `setCroppedFileInput` now derives the cropped file's name from the original filename's base (stripped of its extension) plus a hardcoded `.jpg`, matching `extractCroppedBlob`'s always-JPEG output, instead of reusing the original file's extension. Verified live: uploading `test.jpg` produces a hidden input file named `cropped-test.jpg` with `type: image/jpeg` — extension and content now agree.

### WR-03: File-size/type validation logic duplicated (and already drifted) across 10 view files

**Files modified:** `QuestBoard.Service/wwwroot/js/image-crop.js`, and all 10 crop-modal views (`Characters/Create(.Mobile).cshtml`, `Characters/Edit(.Mobile).cshtml`, `Contacts/Create(.Mobile).cshtml`, `Contacts/Edit(.Mobile).cshtml`, `DungeonMaster/EditProfile(.Mobile).cshtml`)
**Commit:** `e7b51eb`
**Applied fix:** Extracted the size/type pre-check into `initImageCrop` itself as a single shared implementation (`maxFileSizeBytes`/`allowedTypes` config options, defaulting to the same 5 MB / jpeg,jpg,png,gif policy all 10 views previously hand-duplicated), reusing the same `showError`/`getErrorDiv` machinery the crop pipeline's own error handling already used. Removed all 10 views' independent inline validation `<script>` blocks (desktop and mobile had drifted in check-order; the DM profile pair additionally had inconsistently-prefixed `DM_`-named constants and a `.toFixed(1)` vs. everyone else's `.toFixed(2)` size-message formatting difference — both drifts are gone now that there's one implementation). Verified live: both a bad file type and an oversized file are still correctly rejected with the same user-facing error text as before.

### WR-04: `image-crop.css` loaded twice on every Mobile crop view

**Files modified:** `Characters/Create.Mobile.cshtml`, `Characters/Edit.Mobile.cshtml`, `Contacts/Create.Mobile.cshtml`, `Contacts/Edit.Mobile.cshtml`, `DungeonMaster/EditProfile.Mobile.cshtml`
**Commit:** `e7b51eb`
**Applied fix:** Removed the redundant per-view `<link rel="stylesheet" href="~/css/image-crop.css" />` from each Mobile view's `@section Styles` block, now that `_Layout.cshtml` includes it globally (added during the earlier hand-patch session in this same phase).

### WR-05: Orphaned `id="croppedPictureFileInput"` attribute never referenced by any script

**Files modified:** all 10 crop-modal views' hidden `CroppedPictureFile` inputs
**Commit:** `e7b51eb`
**Applied fix:** Removed the dead `id="croppedPictureFileInput"` attribute from all 10 hidden inputs — `initImageCrop` has only ever looked this element up by `name`, never by `id`.

## Skipped Issues

None — all 5 in-scope Warning findings were fixed. The 3 Info findings (IN-01 event-race edge case, IN-02 `resetCropState` not resetting selection geometry, IN-03 pre-existing `AllowedExtensionsAttribute`/`MaxFileSizeAttribute` duplication predating this phase) were left untouched per the reviewer's own "no action required" / "out of phase scope" conclusions.

**Post-fix verification:** Full solution build (0 warnings/errors) and full test suite (235 unit + 372 integration = 607/607) both green after the fix commit. Live-verified in browser: normal JPEG crop flow, GIF-skip path, and file-type/size rejection all behave correctly.

---

_Fixed: 2026-07-07T22:30:00+02:00_
_Fixer: Claude (orchestrator, direct application)_
_Iteration: 1_
