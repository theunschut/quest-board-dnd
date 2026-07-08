---
phase: 46-client-side-crop-ui
plan: 05
subsystem: ui
tags: [cropperjs, canvas, createImageBitmap, exif, javascript, css, image-upload]

# Dependency graph
requires:
  - phase: 45-dual-image-storage-backend
    provides: OriginalImageData/CroppedImageData columns and server-side crop-accepting service signatures
provides:
  - "wwwroot/js/image-crop.js: global initImageCrop({fileInputId, hiddenCroppedInputName, aspectRatio}) initializer"
  - "EXIF-correct + downscale pre-processing pipeline (prepareImageForCropper) shared by every upload view"
  - "Cropper.js v2 $toCanvas() extraction + DataTransfer dual-file hidden-input population"
  - "wwwroot/css/image-crop.css: .crop-stage layout + touch-action fix + mobile height override"
affects: [46-06-wire-crop-ui-into-upload-views, 46-07-real-device-verification]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Feature-scoped JS/CSS files included per-view (not merged into site.js), matching existing per-feature CSS convention"
    - "createImageBitmap({imageOrientation:'from-image'}) as the EXIF-orientation fix, run unconditionally before any canvas/Cropper.js work"
    - "DataTransfer-based hidden file input population for a second (cropped) IFormFile alongside the original"

key-files:
  created:
    - QuestBoard.Service/wwwroot/js/image-crop.js
    - QuestBoard.Service/wwwroot/css/image-crop.css
  modified: []

key-decisions:
  - "Error surfacing for a failed createImageBitmap decode reuses the existing #fileSizeError-style div by id lookup, rather than requiring the view to pass an error-element id into config, since every upload view already has this exact element from its file-size/type check"
  - "Default-crop extraction runs immediately after the modal opens (before any drag), and again on Use This Crop click, but never on pointermove -- matches RESEARCH.md's guidance against re-extracting the canvas on every drag frame"
  - "initImageCrop and its DOM lookups all guard with null checks and return silently if required crop-modal markup isn't present, so the script is safe to include speculatively before Plan 06 wires the markup into any view"

requirements-completed: [IMAGE-01]

# Metrics
duration: 15min
completed: 2026-07-07
---

# Phase 46 Plan 05: Client-Side Crop Pipeline (image-crop.js / image-crop.css) Summary

**Shared `initImageCrop()` pipeline implementing EXIF-safe createImageBitmap decode, ≤2400px downscale, Cropper.js v2 `$toCanvas()` extraction, and DataTransfer dual-file hidden-input population, plus the crop-stage CSS with a touch-action fix**

## Performance

- **Duration:** ~15 min
- **Started:** 2026-07-07T13:52:00Z
- **Completed:** 2026-07-07T14:07:47Z
- **Tasks:** 2
- **Files modified:** 2 (both new)

## Accomplishments
- `image-crop.js` exposes a single global `initImageCrop({ fileInputId, hiddenCroppedInputName, aspectRatio })` that any of the 10 upload views (Plan 06) can call with their own element IDs.
- The full EXIF-correct → downscale → Cropper.js 1:1 → `$toCanvas()` → `DataTransfer` dual-file pipeline is implemented exactly per RESEARCH.md Patterns 1-4, with explicit `bitmap.close()`/scratch-canvas dimension reset for iOS Safari canvas-memory safety.
- The hidden cropped input is populated immediately after the modal opens (from the default centered 1:1 selection), satisfying D-04's "never blocks form submission" guarantee before any user interaction occurs.
- `image-crop.css` provides the `.crop-stage` layout wrapper, a `touch-action: none` fix on `cropper-canvas` so touch-drag doesn't fight modal/page scroll, and a mobile `@media` override reducing the crop-stage height toward 320px.

## Task Commits

Each task was committed atomically:

1. **Task 1: Create image-crop.js — EXIF/downscale pipeline, Cropper.js init, extraction, dual-file population** - `c27ab47` (feat)
2. **Task 2: Create image-crop.css — crop-stage modal layout with amber-consistent handles** - `12a3710` (feat)

_Note: Handle amber theming (`theme-color="rgba(255,193,7,0.35)"`) is applied via the markup attribute in Plan 06, per UI-SPEC.md and the plan's own instruction that "any handle/grid color is set via the theme-color attribute in the markup (Plan 06), not here" — image-crop.css intentionally contains no color rules._

## Files Created/Modified
- `QuestBoard.Service/wwwroot/js/image-crop.js` - `initImageCrop` global initializer; `prepareImageForCropper` (EXIF decode + downscale + memory release); `extractCroppedBlob` (`$toCanvas()` + `toBlob`); `setCroppedFileInput` (`DataTransfer` population); change/confirm/cancel event wiring
- `QuestBoard.Service/wwwroot/css/image-crop.css` - `.crop-stage` layout, `cropper-canvas` `touch-action: none`, mobile height override

## Decisions Made
- Reused the existing `#fileSizeError` div convention for the new "This image couldn't be read" error message (looked up by id inside `initImageCrop`, not passed via config), since every one of the 10 target views already renders this exact element for its file-size/type checks — avoids widening the `initImageCrop` config contract for a message that always targets the same element id across every view.
- `initImageCrop` performs all its own null-guarded DOM lookups (file input, modal, cropper elements, hidden input) and returns silently if any are missing, so the script is safe to `<script src>`-include and call before Plan 06's markup exists on a given view, without throwing.

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

None.

## User Setup Required

None - no external service configuration required. The Cropper.js v2.1.1 CDN `<script>` tag (with Subresource Integrity) is wired into view markup in Plan 06, not this plan.

## Next Phase Readiness

- `image-crop.js`/`image-crop.css` are complete, verified (`node --check` passes, all required landmarks present via grep), and ready to be included by Plan 06's 10 upload views (`Characters/{Create,Edit}.cshtml` + `.Mobile`, `Contacts/{Create,Edit}.cshtml` + `.Mobile`, `DungeonMaster/EditProfile.cshtml` + `.Mobile`).
- Plan 06 still needs to: add the Cropper.js v2.1.1 CDN `<script>` tag with SRI hash, add the `#cropPhotoModal` markup (per UI-SPEC.md section 1) to each view, add the hidden `CroppedPictureFile` input, call `initImageCrop({...})` from each view's `@section Scripts` block, and widen each ViewModel with a `CroppedPictureFile` property.
- Interactive drag/resize/zoom behavior and real-device EXIF/canvas-memory/touch-drag verification remain gated behind Plan 07's device-access checkpoint, per RESEARCH.md and CONTEXT.md's Pre-Execution Blocker — no code in this plan attempted to simulate or skip that verification.

---
*Phase: 46-client-side-crop-ui*
*Completed: 2026-07-07*

## Self-Check: PASSED

All claimed files and commits verified present:
- FOUND: QuestBoard.Service/wwwroot/js/image-crop.js
- FOUND: QuestBoard.Service/wwwroot/css/image-crop.css
- FOUND: .planning/phases/46-client-side-crop-ui/46-05-SUMMARY.md
- FOUND: c27ab47 (Task 1 commit)
- FOUND: 12a3710 (Task 2 commit)
- FOUND: e0b40a4 (docs/SUMMARY commit)
