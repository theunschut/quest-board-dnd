---
phase: 46-client-side-crop-ui
reviewed: 2026-07-07T22:11:44+02:00
depth: standard
files_reviewed: 38
files_reviewed_list:
  - QuestBoard.Domain/Interfaces/ICharacterService.cs
  - QuestBoard.Domain/Interfaces/IContactService.cs
  - QuestBoard.Domain/Interfaces/IDungeonMasterProfileService.cs
  - QuestBoard.Domain/Services/CharacterService.cs
  - QuestBoard.Domain/Services/ContactService.cs
  - QuestBoard.Domain/Services/DungeonMasterProfileService.cs
  - QuestBoard.IntegrationTests/Controllers/CharactersControllerIntegrationTests.cs
  - QuestBoard.IntegrationTests/Controllers/ContactsControllerIntegrationTests.cs
  - QuestBoard.IntegrationTests/Controllers/DungeonMasterControllerIntegrationTests.cs
  - QuestBoard.Repository/CharacterRepository.cs
  - QuestBoard.Repository/ContactRepository.cs
  - QuestBoard.Service/Controllers/Characters/CharactersController.cs
  - QuestBoard.Service/Controllers/Contacts/ContactsController.cs
  - QuestBoard.Service/Controllers/DungeonMaster/DungeonMasterController.cs
  - QuestBoard.Service/ViewModels/CharacterViewModels/CharacterViewModel.cs
  - QuestBoard.Service/ViewModels/ContactViewModels/ContactViewModel.cs
  - QuestBoard.Service/ViewModels/DungeonMasterViewModels/EditDMProfileViewModel.cs
  - QuestBoard.Service/Views/Characters/Create.Mobile.cshtml
  - QuestBoard.Service/Views/Characters/Create.cshtml
  - QuestBoard.Service/Views/Characters/Edit.Mobile.cshtml
  - QuestBoard.Service/Views/Characters/Edit.cshtml
  - QuestBoard.Service/Views/Characters/Index.Mobile.cshtml
  - QuestBoard.Service/Views/Characters/Index.cshtml
  - QuestBoard.Service/Views/Contacts/Create.Mobile.cshtml
  - QuestBoard.Service/Views/Contacts/Create.cshtml
  - QuestBoard.Service/Views/Contacts/Edit.Mobile.cshtml
  - QuestBoard.Service/Views/Contacts/Edit.cshtml
  - QuestBoard.Service/Views/Contacts/Index.Mobile.cshtml
  - QuestBoard.Service/Views/Contacts/Index.cshtml
  - QuestBoard.Service/Views/DungeonMaster/EditProfile.Mobile.cshtml
  - QuestBoard.Service/Views/DungeonMaster/EditProfile.cshtml
  - QuestBoard.Service/Views/Quest/Details.cshtml
  - QuestBoard.Service/Views/Quest/Manage.cshtml
  - QuestBoard.Service/Views/Quest/_QuestCard.cshtml
  - QuestBoard.Service/Views/QuestLog/Details.Mobile.cshtml
  - QuestBoard.Service/Views/QuestLog/Details.cshtml
  - QuestBoard.Service/Views/Shared/_Layout.cshtml
  - QuestBoard.Service/wwwroot/css/characters.css
  - QuestBoard.Service/wwwroot/css/contacts.css
  - QuestBoard.Service/wwwroot/css/image-crop.css
  - QuestBoard.Service/wwwroot/js/image-crop.js
  - QuestBoard.UnitTests/Services/CharacterServiceTests.cs
  - QuestBoard.UnitTests/Services/ContactServiceTests.cs
  - QuestBoard.UnitTests/Services/DungeonMasterProfileServiceTests.cs
findings:
  critical: 0
  warning: 5
  info: 3
  total: 8
status: issues_found
---

# Phase 46: Code Review Report

**Reviewed:** 2026-07-07T22:11:44+02:00
**Depth:** standard
**Files Reviewed:** 38 (some read-only for context; no unrelated findings reported)
**Status:** issues_found

## Summary

This phase adds a client-side crop-before-save UI (Cropper.js v2) across the Character, Contact,
and DM Profile photo-upload forms, on top of the prior phase's dual original/cropped image storage.
The domain/repository/controller layer that threads `hasNewOriginalUpload` +
`newCroppedImageData` through `AddAsync`/`UpdateAsync`/`UpsertProfileAsync` is careful and
well-tested (unit + integration coverage for preserve/clear/replace crop semantics, including the
IDOR-style visibility-parity regression tests for `GetCroppedContactImage`). No blocker-severity
defects were found in this diff.

The hand-patched `image-crop.js` (the file called out for special attention) is functionally sound
for the two bugs it says it fixes (canvas `action="move"` handle, and the `shown.bs.modal`
re-trigger of `$center`/`$initSelection`). I traced the `change`-event race between the pre-existing
inline per-view validation script and `image-crop.js`'s own listener and confirmed it is *not* a bug
in practice — `input.value = ''` synchronously empties `.files`, so `image-crop.js`'s
`if (!file) return` guard correctly no-ops when the inline script rejects a file first. However,
that duplication is real: file-size/type validation is now hand-copied into 5 separate view
`@section Scripts` blocks (in addition to being centrally enforced server-side by
`ImageValidationService`), which is a maintainability smell — a future change to the size/type
policy has to be made in 6 places to stay consistent, and it has already drifted once (Contacts'
inline script checks size before type; Characters' desktop/Edit inline script also checks size
before type; the Characters/Contacts *Mobile* inline scripts check type before size — order-only
drift so far, but drift nonetheless).

The bigger functional finding is that `image-crop.js` always re-encodes the crop selection as
`image/jpeg` via `$toCanvas()`/`canvas.toBlob(..., 'image/jpeg', ...)`, but
`setCroppedFileInput` names the resulting file `'cropped-' + originalFileName`, preserving the
*original* file's extension (`.gif`, `.png`, etc.). The submitted `CroppedPictureFile` therefore has
a `Content-Type` of `image/jpeg` with a filename extension that can be `.gif`/`.png`. Server-side
`ImageValidationService.ValidateSingle` checks MIME type and extension independently against two
allowlists, both of which pass here (no security bypass), but the stored "cropped" blob's declared
extension no longer matches its actual encoded format.

## Warnings

### WR-01: Cropped image is always JPEG-encoded regardless of source format, silently dropping GIF animation

**File:** `QuestBoard.Service/wwwroot/js/image-crop.js:43-51`
**Issue:** `extractCroppedBlob` hard-codes `canvas.toBlob(fn, 'image/jpeg', 0.9)`. Every view
advertises "JPG, PNG, GIF" as accepted upload types (`ImageValidationService` and every
`AllowedExtensionsAttribute` allow `.gif`), and the *original* image is preserved untouched (see
Summary), but the derived cropped/display image used everywhere in the UI
(`GetCroppedPicture`/`GetCroppedContactImage`/`GetDMProfilePicture`, which fall back only when no
crop was ever saved) is always a static JPEG. A user who uploads an animated GIF portrait and
completes the crop flow (which happens automatically on modal-shown, even without explicit
interaction — see `fitImageAndSelectionToVisibleCanvas` in `image-crop.js:189-198`) will have their
animated avatar silently replaced by a static JPEG frame everywhere the cropped image is displayed,
with no indication to the user that this happened.
**Fix:** Either special-case GIF to skip the auto-populate-crop-on-open behavior (leave
`CroppedPictureFile` unset so the server-side crop stays null and reads fall back to the original),
or explicitly document/accept the tradeoff and preserve `image/gif` as the output type when
`file.type === 'image/gif'` (accepting that only the first frame will be visible in the crop canvas,
which is likely acceptable since `<img>` in a `<canvas>` context can only render gif's first frame
anyway).

### WR-02: Cropped file's declared extension no longer matches its actual re-encoded format

**File:** `QuestBoard.Service/wwwroot/js/image-crop.js:55-68`
**Issue:** `setCroppedFileInput` builds the cropped `File` as `'cropped-' + originalFileName` (e.g.
`cropped-photo.gif` or `cropped-photo.png`) while its `type` (and actual bytes, per WR-01) is always
`image/jpeg`. This isn't currently exploitable — `ImageValidationService.ValidateSingle` validates
MIME type and extension against independent allowlists that both still pass, and the server detects
the real format via magic-byte sniffing (`DetectImageMimeType`) rather than trusting the filename
when serving it back — but it is a latent inconsistency that could bite a future change that starts
trusting the extension (e.g. a future stricter check that the extension matches the sniffed MIME
type).
**Fix:** Derive the cropped file's extension from the actual output MIME type instead of reusing the
original filename's extension, e.g. `'cropped-' + originalFileName.replace(/\.[^.]+$/, '') + '.jpg'`.

### WR-03: File-size/type validation logic duplicated (and already drifted) across 5 view files instead of one shared script

**File:** `QuestBoard.Service/Views/Characters/Create.cshtml:196-226`, `Edit.cshtml:183-205`,
`Create.Mobile.cshtml:176-198`, `Edit.Mobile.cshtml:183-205`,
`QuestBoard.Service/Views/Contacts/Create.cshtml:122-153`, `Edit.cshtml:130-161`,
`Create.Mobile.cshtml:115-140`, `Edit.Mobile.cshtml:122-147`,
`QuestBoard.Service/Views/DungeonMaster/EditProfile.cshtml:123-146`, `EditProfile.Mobile.cshtml:118-141`
**Issue:** Every one of the 10 upload views hand-duplicates a client-side `MAX_FILE_SIZE`/
`ALLOWED_TYPES` check inline in its own `@section Scripts` block, in addition to the identical
policy already enforced centrally by `ImageValidationService`
(`QuestBoard.Domain/Services/ImageValidationService.cs`) and the `MaxFileSizeAttribute`/
`AllowedExtensionsAttribute` data annotations. This is pure client-side UX (fail fast before upload)
so it isn't a security gap, but the duplication has already drifted: the desktop Characters/Contacts
scripts check size before type, while the Mobile Characters/Contacts scripts check type before size,
and the DM profile script's variable names (`DM_MAX_FILE_SIZE`/`DM_ALLOWED_TYPES`) are prefixed
differently than the other 9 copies for no functional reason. A future policy change (e.g. raising
the 5 MB limit) requires updating 6 independent copies of this constant to stay consistent.
**Fix:** Extract the size/type pre-check into `image-crop.js` (or a small shared
`file-upload-validation.js`) as a third parameter to `initImageCrop`, or expose a small
`validateFileClientSide(file, errorDiv)` helper that all 10 views call instead of hand-rolling the
check.

### WR-04: `<link rel="stylesheet" href="~/css/image-crop.css">` loaded twice on every Mobile crop view

**File:** `QuestBoard.Service/Views/Characters/Create.Mobile.cshtml:10`,
`Edit.Mobile.cshtml:10`, `QuestBoard.Service/Views/Contacts/Create.Mobile.cshtml:9`,
`Edit.Mobile.cshtml:9`, `QuestBoard.Service/Views/DungeonMaster/EditProfile.Mobile.cshtml:9`
**Issue:** `_Layout.cshtml:22` now globally includes `<link rel="stylesheet" href="~/css/image-crop.css" asp-append-version="true" />` on every page. Each Mobile crop view's `@section Styles` block
additionally re-declares the same stylesheet (without `asp-append-version`), so it is fetched/parsed
twice on every Mobile Create/Edit page load. Harmless functionally (CSS re-application is
idempotent) but unnecessary and inconsistent with the desktop views, which correctly rely on the
new global include and do not re-declare it.
**Fix:** Remove the per-view `<link rel="stylesheet" href="~/css/image-crop.css" />` line from the
5 Mobile views' `@section Styles` blocks now that `_Layout.cshtml` includes it globally.

### WR-05: Orphaned `id="croppedPictureFileInput"` attribute never referenced by any script

**File:** `QuestBoard.Service/Views/Characters/Create.cshtml:37`, `Edit.cshtml:34`,
`Create.Mobile.cshtml:25`, `Edit.Mobile.cshtml:32`, `QuestBoard.Service/Views/Contacts/Create.cshtml:23`,
`Edit.cshtml:31`, `Create.Mobile.cshtml:23`, `Edit.Mobile.cshtml:30`,
`QuestBoard.Service/Views/DungeonMaster/EditProfile.cshtml:37`, `EditProfile.Mobile.cshtml:41`
**Issue:** All 10 hidden `<input type="file" name="CroppedPictureFile" id="croppedPictureFileInput" ...>`
elements declare an `id` that is never queried by `image-crop.js` — `initImageCrop` locates this
element exclusively by `name` via
`document.querySelector('input[type="file"][name="' + hiddenCroppedInputName + '"]')`
(`image-crop.js:91-93`). The `id` attribute is dead markup left over from an earlier iteration (or
copy-paste from the visible file input's `id` pattern).
**Fix:** Remove the unused `id="croppedPictureFileInput"` attribute from all 10 hidden inputs, or
switch `initImageCrop`'s lookup to use the id for clarity/consistency (either is fine; just pick one
and stop carrying dead markup).

## Info

### IN-01: `initial-coverage="0.8"` and `$initSelection(true, true)` re-run on every file re-selection, discarding prior in-progress crop adjustments without confirmation

**File:** `QuestBoard.Service/wwwroot/js/image-crop.js:189-198`
**Issue:** If a user opens the crop modal, adjusts the selection, then (without confirming or
cancelling) uses the OS file picker again via the still-visible underlying `fileInput` — not
possible through the modal UI itself, but reachable if a user Escapes the modal (blocked here by
`data-bs-keyboard="false"`) or otherwise triggers a second native file-picker dialog — the
`fileInput`'s `change` handler fires again and unconditionally replaces `cropperImageEl.src`, then
re-runs `$initSelection(true, true)`, discarding any in-progress crop adjustment with no warning.
Given `data-bs-backdrop="static"` and `data-bs-keyboard="false"` on the modal, this is a narrow edge
case (the underlying page is not click/Escape dismissible while the modal is open) but the file
input itself is not disabled while the modal is open, so a keyboard-only user (Tab back to the
underlying, now-hidden-behind-backdrop file input, though modal focus-trapping should prevent this
in practice) is the only path. Low practical risk given Bootstrap's modal focus trap, flagged only
because the hand-patched code plus the FORCE-adversarial reading brief called out this exact file.
**Fix:** No action required unless UAT surfaces an actual reachable path; if desired, guard by
disabling `fileInput` while `modalEl` has the `.show` class.

### IN-02: `resetCropState()` does not reset `cropperSelectionEl`'s aspect-ratio/coverage attributes, only the image `src`

**File:** `QuestBoard.Service/wwwroot/js/image-crop.js:143-152`
**Issue:** `resetCropState()` (invoked by the Discard/cancel button) clears `currentObjectUrl`,
removes `cropperImageEl`'s `src`, and empties the hidden `CroppedPictureFile` input, but leaves
`cropperSelectionEl`'s DOM state (position/size from the just-discarded crop) untouched. The next
`change` event still correctly calls `$initSelection(true, true)` after the new image loads, which
re-derives selection geometry from the new image's dimensions, so this does not currently produce a
visible bug — noted only for completeness since `resetCropState` reads as though it fully resets
crop UI state but doesn't touch the selection element at all.
**Fix:** No action needed given `$initSelection(true, true)` on next open recomputes geometry
from scratch; consider a comment noting why `cropperSelectionEl` doesn't need explicit resetting,
to save a future reader the same trace.

### IN-03: `AllowedExtensionsAttribute`/`MaxFileSizeAttribute` classes are duplicated verbatim across two ViewModel files

**File:** `QuestBoard.Service/ViewModels/CharacterViewModels/CharacterViewModel.cs:64-110`,
`QuestBoard.Service/ViewModels/ContactViewModels/ContactViewModel.cs:60-106`
**Issue:** Pre-existing duplication (not introduced by this phase, but both files were touched by
this diff to add `CroppedPictureFile` validation attributes using these same duplicated classes).
`MaxFileSizeAttribute` and `AllowedExtensionsAttribute` are defined identically in both files (the
Contact one even says "mirroring QuestBoard.Service.ViewModels.CharacterViewModels" in a comment,
acknowledging the duplication). `EditDMProfileViewModel.cs` reuses the Character copy via
`using QuestBoard.Service.ViewModels.CharacterViewModels;`, so there are effectively two competing
definitions of the same validation attributes in the codebase, and the choice of which one a given
ViewModel uses is implicit based on `using` order/namespace resolution.
**Fix:** Not a regression introduced by this phase, but since this phase touched both files to wire
up `CroppedPictureFile`, it would have been a low-cost opportunity to extract both attributes into a
single shared location (e.g. `QuestBoard.Service.ViewModels.Validation`) and have all three
ViewModels reference it. Flagged as info/opportunistic rather than warning since it's out of this
phase's stated scope.

---

_Reviewed: 2026-07-07T22:11:44+02:00_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
