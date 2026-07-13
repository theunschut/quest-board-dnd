# Quick Task 260713-js8: Add re-crop trigger for existing profile images - Context

**Gathered:** 2026-07-13
**Status:** Ready for planning

<domain>
## Task Boundary

Photos can be uploaded in exactly 3 places, each with its own controller + view + service/repository stack:

1. **Character profile picture** — `CharactersController` (Create/Edit), `Views/Characters/Create.cshtml` + `Edit.cshtml` (+ `.Mobile.cshtml` variants), `CharacterService`, `CharacterRepository`.
2. **Contact image** — `ContactsController` (Create/Edit), `Views/Contacts/Create.cshtml` + `Edit.cshtml` (+ `.Mobile.cshtml` variants), `ContactService`, `ContactRepository`.
3. **DM profile photo** — `DungeonMasterController.EditProfile`, `Views/DungeonMaster/EditProfile.cshtml` (+ `.Mobile.cshtml`), `DungeonMasterProfileService`, `DungeonMasterProfileRepository`.

All three already store an **Original** image and a separately-cropped **Cropped** image (dual-image storage, shipped in v7.0 milestone phases 45/46). The intent (confirmed by the user) was to let a user re-open the cropper against the **already-stored original** without re-uploading a file — i.e. fix a bad crop after the fact. That trigger does not exist today; only Create/Edit's file-input `change` event opens the crop modal (shared `wwwroot/js/image-crop.js`, `initImageCrop()`).

Re-crop only applies on **Edit** views (Create has no existing image yet).

</domain>

<decisions>
## Implementation Decisions

### Scope
- Fix all three locations (Character, Contact, DM Profile) in this task — confirmed with user via AskUserQuestion, "Fix all three" selected over "Character + Contact only."
- DM Profile requires materially more work than the other two (new endpoint + service/repository fix) — see Specific Ideas below. Do not treat it as a copy-paste of the Character/Contact pattern.

### Re-crop source image
- Re-crop must load the **original** (uncropped) stored image into the cropper, not the previously-saved crop. Re-cropping the already-cropped square would prevent recovering parts of the photo that a bad first crop cut away, which defeats the point of the feature.
- Character and Contact already expose original-image endpoints (`GetProfilePicture`, `GetContactImage`) — reuse them as the fetch source for the re-crop button.
- DM Profile has **no such endpoint today** (`GetDMProfilePicture` only returns the cropped/fallback image). A new endpoint exposing the DM profile's original image must be added, with the same authorization/visibility checks as the existing `GetDMProfilePicture` (see `IsTargetInActiveGroupAsync` guard at `DungeonMasterController.cs:153`).

### GIF handling (Claude's discretion — not discussed with user, use judgment during planning/execution)
- On upload, GIFs intentionally skip the crop modal entirely (`image-crop.js` lines ~191-199: cropping would collapse an animated GIF to a static JPEG frame). No stored crop is ever created for a GIF-only original.
- The re-crop trigger should not silently convert a stored animated GIF into a static cropped JPEG. Recommended approach: detect content-type at click-time from the fetched original-image response (`Content-Type` header) before opening the modal — mirrors the existing `file.type === 'image/gif'` check in the upload path — and show a message instead of opening the modal when the original is a GIF, rather than adding a new server-rendered flag to each ViewModel.

### UI pattern
- Add a small button/link (e.g. "Edit Crop" / "Re-crop Photo", icon `fa-crop-alt` to match the existing modal's icon) next to the existing photo preview `<img>` in each Edit view (desktop + mobile variants — 3 features x up to 2 view files each, check whether `.Mobile.cshtml` variants share the photo markup or duplicate it).
- Clicking the button fetches the original image, feeds it into the same shared crop modal/pipeline (`#cropPhotoModal`, `image-crop.js`), and on confirm populates the same hidden `CroppedPictureFile` input already present in each form — the user still clicks the existing "Save"/"Save Changes"/"Save Profile" submit button to persist it. Do not auto-submit the form from the crop-confirm handler; keep the existing save flow untouched.
- The button only makes sense when a photo already exists (`Model.HasProfilePicture` is already the guard used for the existing preview `<img>` in all 3 views) — gate the button on the same flag.

</decisions>

<specifics>
## Specific Ideas

### Confirmed backend gaps (from direct investigation of the codebase, not assumption)

**Character & Contact (lower risk):**
- `CharacterService.UpdateAsync(model, hasNewOriginalUpload, newCroppedImageData, token)` at `QuestBoard.Domain/Services/CharacterService.cs:79` already branches on `newCroppedImageData != null` **independent of** `hasNewOriginalUpload` — a crop-only submission (no new original) is already handled correctly at the service+repository layer. Same pattern in `ContactService.UpdateAsync`.
- BUT `CharactersController.Edit` (`QuestBoard.Service/Controllers/Characters/CharactersController.cs:269-307`) and `ContactsController.Edit` (similar structure, ~line 200-244) only read `viewModel.CroppedPictureFile` **inside** the `if (hasNewOriginalUpload)` block. A submitted crop-only file is silently dropped by the controller today — this must be restructured so `CroppedPictureFile` is read (and validated, and copied to bytes) regardless of whether `ProfilePictureFile`/`ContactImageFile` was also submitted, then passed through as `newCroppedImageData` to the existing service call.
- `ImageValidationService.ValidateImagePair(original, cropped)` currently requires an `original` `ImageFileInput` to validate a `cropped` one (see call sites) — check whether a crop-only submission needs a different validation path (e.g. `ValidateSingle` on just the cropped file) since there's no new `original` `IFormFile` to build an `ImageFileInput` from in that case.

**DM Profile (higher risk — do not copy the Character/Contact pattern blindly):**
- `DungeonMasterProfileService.UpsertProfileAsync` (`QuestBoard.Domain/Services/DungeonMasterProfileService.cs:17`) computes `var updateImage = imageBytes != null || removeImage;` — this ignores `newCroppedImageData` entirely, so a crop-only submission would currently be a no-op even if the controller passed it through.
- `DungeonMasterProfileRepository.ApplyProfileImage` (`QuestBoard.Repository/DungeonMasterProfileRepository.cs:107`) treats `originalImageData == null` as "**delete the entire stored image**" (`entity.ProfileImage = null`). Character/Contact avoid this because their service layer always re-fetches the existing original from the repository on a no-upload edit before calling `ApplyProfileImage` (e.g. `CharacterService.UpdateAsync` line ~110-112: `await repository.GetCharacterOriginalPictureAsync(...)`). `DungeonMasterProfileService` has no equivalent re-fetch step today — wiring `newCroppedImageData` through naively (with `imageBytes == null`) would **wipe the DM's stored photo**. The fix must add a re-fetch-existing-original step (there's already `GetOriginalPictureAsync` on `DungeonMasterProfileRepository`/`IDungeonMasterProfileRepository`, currently used elsewhere) so `originalImageData` passed to `ApplyProfileImage` is never null on a crop-only edit.
- No endpoint exposes the DM profile's original (uncropped) image today. `DungeonMasterController.GetDMProfilePicture` (`QuestBoard.Service/Controllers/DungeonMaster/DungeonMasterController.cs:151`) only returns `dmProfileService.GetCroppedPictureAsync(id, ...)`. A new action (e.g. `GetOriginalDMProfilePicture`) is needed, following the same `IsTargetInActiveGroupAsync` guard and MIME-sniff pattern already used in that controller.

### Existing test conventions to follow
- Unit tests: `QuestBoard.UnitTests/Services/CharacterServiceTests.cs`, `ContactServiceTests.cs`, `DungeonMasterProfileServiceTests.cs` already have crop-related test names to pattern-match against, e.g. `UpdateAsync_NewCropSupplied_PersistsCrop`, `UpdateAsync_NoNewFile_RefetchesAndPassesThroughExistingCrop`. A new crop-only-no-original-upload scenario should get an equivalent test in each.
- Integration tests: `QuestBoard.IntegrationTests/Controllers/CharactersControllerIntegrationTests.cs` and `ContactsControllerIntegrationTests.cs` already cover `Edit_NewOriginalAndCroppedPhotoUpload_PersistsSubmittedCrop` — a crop-only equivalent (no new original file in the request) should be added per controller, plus a DM Profile integration test that specifically asserts the photo is **not wiped** on a crop-only save (given the risk identified above).
- `image-crop.js` is one shared file across all 3 views (`initImageCrop()` is called with per-view element IDs/config) — any client-side changes to support loading a fetched image (vs. a locally-picked `File`) belong there once, not duplicated per view.

</specifics>

<canonical_refs>
## Canonical References

- `.planning/milestones/v7.0-phases/45-dual-image-storage-backend/` — original backend design for Original+Cropped dual storage.
- `.planning/milestones/v7.0-phases/46-client-side-crop-ui/` — original client-side Cropper.js integration design (the modal/JS this task extends).

</canonical_refs>
