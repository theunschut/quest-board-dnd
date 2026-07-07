---
phase: 46-client-side-crop-ui
plan: 06
subsystem: ui
tags: [cropperjs, razor-views, image-upload, crop-modal, ui-wiring]

# Dependency graph
requires:
  - phase: 46-client-side-crop-ui (plan 01)
    provides: CroppedPictureFile IFormFile binding property on CharacterViewModel/ContactViewModel/EditDMProfileViewModel
  - phase: 46-client-side-crop-ui (plan 03)
    provides: GetCroppedPicture (Characters), GetCroppedContactImage (Contacts), and DungeonMasterController.GetDMProfilePicture repointed to cropped-or-fallback
  - phase: 46-client-side-crop-ui (plan 05)
    provides: wwwroot/js/image-crop.js (initImageCrop global initializer) and wwwroot/css/image-crop.css
provides:
  - "Crop modal markup (id=cropPhotoModal), Cropper.js v2.1.1 CDN script (pinned + real SRI hash), image-crop.js include, and initImageCrop() call wired into all 10 upload views"
  - "Hidden CroppedPictureFile file input on all 10 upload views"
  - "Current-photo preview repoint to cropped-read endpoints on Character/Contact Create+Edit forms (DM preview already repointed at the controller in Plan 03, no view change needed)"
affects: [46-07]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Per-view crop modal instance (not a shared global modal) since only one upload field is ever active per page"
    - "Mobile .Mobile.cshtml variants use 320px cropper-canvas height and drop modal-lg; desktop uses 400px + modal-lg"
    - "Real SRI hash computed by downloading the actual served CDN bytes and hashing with openssl dgst -sha384, not guessed or left as a placeholder"

key-files:
  created: []
  modified:
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

key-decisions:
  - "Real SRI hash for cropperjs@2.1.1/dist/cropper.min.js computed by downloading the file from jsDelivr and hashing with `openssl dgst -sha384 -binary | openssl base64 -A`, then re-downloading and re-hashing to confirm determinism before using the same hash across all 10 views: sha384-pDSc1bjpfKbaO0DjoZ/uKmzKaARM4658N3xT1ARgy5AKyR6O2UrecaAO8fdv39y9"
  - "DungeonMaster/EditProfile(.Mobile).cshtml's error-message div was renamed from dmFileSizeError to fileSizeError (matching the other 8 views' convention) because image-crop.js's shared error-surfacing code hardcodes getElementById('fileSizeError') -- without this rename the 'This image couldn't be read' error copy would have silently never displayed on the two DM views"

requirements-completed: [IMAGE-01, IMAGE-05]

# Metrics
duration: 40min
completed: 2026-07-07
---

# Phase 46 Plan 06: Wire Crop UI Into Upload Views Summary

**All 10 photo-upload views (Character/Contact Create+Edit + Mobile, DM EditProfile + Mobile) now include the crop modal, a pinned Cropper.js v2.1.1 CDN script with a real (non-placeholder) SRI hash, the shared image-crop.js pipeline, a hidden CroppedPictureFile input, and — where applicable — a current-photo preview repointed to the cropped-read endpoint.**

## Performance

- **Duration:** ~40 min
- **Tasks:** 3
- **Files modified:** 10

## Accomplishments
- Added the UI-SPEC.md section 1 crop modal markup verbatim (id `cropPhotoModal`, `data-bs-backdrop="static" data-bs-keyboard="false"`, dark Bootstrap chrome, full `cropper-canvas`/`cropper-image`/`cropper-selection` Web Component tree, amber `theme-color="rgba(255,193,7,0.35)"` move handle, "Discard Photo"/"Use This Crop" buttons) to all 10 upload views — desktop variants use a 400px crop stage with `modal-lg`, the 5 `.Mobile.cshtml` variants use 320px and no `modal-lg`.
- Every view now loads `image-crop.css`, the pinned Cropper.js v2.1.1 CDN `<script>` with a real SHA-384 Subresource Integrity hash (computed directly from the served bytes, verified deterministic across two independent downloads), `~/js/image-crop.js`, and calls `initImageCrop({ fileInputId, hiddenCroppedInputName: 'CroppedPictureFile', aspectRatio: 1 })` with that view's real file-input id (`profilePictureInput` for Character, `contactImageInput` for Contact, `dmProfilePictureInput` for DM).
- Added a hidden `<input type="file" name="CroppedPictureFile" id="croppedPictureFileInput" style="display:none" />` directly after each visible file input on all 10 views, binding to the `CroppedPictureFile` property Plan 01 already added to all three ViewModels.
- Repointed the current-photo preview `<img>` from the original-read action to the cropped-read action on Character/Contact Create+Edit forms (`GetProfilePicture` → `GetCroppedPicture`, `GetContactImage` → `GetCroppedContactImage`); `Create.Mobile.cshtml` has no existing-photo preview on either form so no repoint was needed there. DM's preview intentionally stays on `GetDMProfilePicture`, which Plan 03 already repointed at the controller to serve cropped-or-fallback bytes.
- Preserved every existing client-side file-size/type `change` listener unchanged on all 10 views — the new crop pipeline runs strictly after that pre-check passes.
- Verified via grep that `image-crop.js` is included in exactly 10/10 target views (mobile-parity gate) and that `dotnet build` succeeds for the full solution.

## Task Commits

Each task was committed atomically:

1. **Task 1: Wire crop modal, CDN + image-crop.js, hidden input, and preview repoint into the 4 Character upload views** - `a9a8147` (feat)
2. **Task 2: Wire crop modal into the 4 Contact upload views** - `94ae4fd` (feat)
3. **Task 3: Wire crop modal into the 2 DM EditProfile views and verify all-10 coverage** - `cb38834` (feat)

## Files Created/Modified
- `QuestBoard.Service/Views/Characters/Create.cshtml` / `Create.Mobile.cshtml` / `Edit.cshtml` / `Edit.Mobile.cshtml` - Crop modal, assets, hidden input, initImageCrop wired with `fileInputId: 'profilePictureInput'`; preview repointed to `GetCroppedPicture` on Create/Edit/Edit.Mobile
- `QuestBoard.Service/Views/Contacts/Create.cshtml` / `Create.Mobile.cshtml` / `Edit.cshtml` / `Edit.Mobile.cshtml` - Crop modal, assets, hidden input, initImageCrop wired with `fileInputId: 'contactImageInput'`; preview repointed to `GetCroppedContactImage` on Edit/Edit.Mobile
- `QuestBoard.Service/Views/DungeonMaster/EditProfile.cshtml` / `EditProfile.Mobile.cshtml` - Crop modal, assets, hidden input, initImageCrop wired with `fileInputId: 'dmProfilePictureInput'`; preview left unchanged on `GetDMProfilePicture`; error-message div id renamed `dmFileSizeError` → `fileSizeError`

## Decisions Made
- Computed the real Cropper.js v2.1.1 SRI hash directly rather than trusting any third-party hash-generator page: downloaded `dist/cropper.min.js` from the pinned jsDelivr URL, hashed it with `openssl dgst -sha384 -binary | openssl base64 -A`, then re-downloaded and re-hashed independently to confirm the two runs produced an identical digest before using it across all 10 views (`sha384-pDSc1bjpfKbaO0DjoZ/uKmzKaARM4658N3xT1ARgy5AKyR6O2UrecaAO8fdv39y9`).
- `Characters/Create.cshtml` actually serves both the Create and Edit routes (branches on `isEdit`), so the Task 1 acceptance criteria's "Create.cshtml preview repoint" was applied inside its existing `isEdit` preview block, not as a separate always-on preview.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Fixed DM EditProfile views' error-message div id mismatch with image-crop.js**
- **Found during:** Task 3, while reading `DungeonMaster/EditProfile.cshtml` and `EditProfile.Mobile.cshtml` before wiring
- **Issue:** `image-crop.js` (Plan 05)'s `getErrorDiv()` helper looks up the "image couldn't be read" error target via a hardcoded `document.getElementById('fileSizeError')`. Both DM EditProfile views used `id="dmFileSizeError"` for their existing file-size/type error div (a DM-specific naming convention, unlike all 8 other upload views which already used the shared `fileSizeError` id). Left as-is, a corrupt/undecodable photo selected on either DM view would fail to decode in `prepareImageForCropper()` but the resulting error copy would silently never render, since `getElementById('fileSizeError')` would return `null` on these two views.
- **Fix:** Renamed `dmFileSizeError` → `fileSizeError` (id and all `getElementById` references) in both `EditProfile.cshtml` and `EditProfile.Mobile.cshtml`, matching the convention every other upload view already uses. The existing file-size/type check logic (`DM_MAX_FILE_SIZE`, `DM_ALLOWED_TYPES`, `dmProfilePictureInput`) was left untouched — only the error-div id changed.
- **Files modified:** `QuestBoard.Service/Views/DungeonMaster/EditProfile.cshtml`, `QuestBoard.Service/Views/DungeonMaster/EditProfile.Mobile.cshtml`
- **Verification:** `dotnet build QuestBoard.Service` succeeds; grep confirms `id="fileSizeError"` present exactly once in each of the two DM views and no remaining `dmFileSizeError` references.
- **Commit:** `cb38834`

---

**Total deviations:** 1 auto-fixed (1 bug)
**Impact on plan:** Cosmetic/behavioral fix scoped entirely to the two files this task already modifies — no new files touched, no architectural change. Without this fix the crop pipeline's read-error UX would have been silently broken on exactly the 2 views (out of 10) that didn't already follow the shared `fileSizeError` id convention.

## Issues Encountered

None beyond the deviation documented above.

## User Setup Required

None — no external service configuration required. The Cropper.js CDN script is publicly served with no API key/auth.

## Next Phase Readiness

- All 10 upload views now have the full client-side crop pipeline wired end-to-end: file select -> EXIF-correct/downscale -> Cropper.js modal -> dual-file (original + cropped) submission -> existing server-side validation/persistence from Plans 01/03/08.
- `dotnet build` (full solution, 5 projects) succeeds with 0 warnings/0 errors.
- Interactive crop behavior (drag/resize/zoom) and real-device EXIF/canvas-memory/touch-drag verification remain gated behind Plan 07's device-access checkpoint, per CONTEXT.md's Pre-Execution Blocker — nothing in this plan attempted to simulate or skip that verification.
- No blockers for Plan 07.

---
*Phase: 46-client-side-crop-ui*
*Completed: 2026-07-07*
