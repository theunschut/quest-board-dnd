# Phase 46: Client-Side Crop UI - Pattern Map

**Mapped:** 2026-07-07
**Files analyzed:** 27 (3 Domain interfaces/services, 3 controllers, 3 ViewModels, 2 new front-end assets, 2 CSS files, 14 views)
**Analogs found:** 27 / 27 (all files have a same-repo analog; this phase is additive/repointing work on an already-established pattern family, not greenfield)

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|--------------------|------|-----------|-----------------|----------------|
| `QuestBoard.Domain/Interfaces/ICharacterService.cs` (widen `UpdateAsync`) | service (interface) | CRUD | same file, existing `UpdateAsync(model, hasNewOriginalUpload, token)` overload | exact — additive overload to an existing interface |
| `QuestBoard.Domain/Services/CharacterService.cs` (widen `UpdateAsync`) | service | CRUD | same file, lines 74-96 | exact |
| `QuestBoard.Domain/Interfaces/IContactService.cs` (widen `UpdateAsync`) | service (interface) | CRUD | `ICharacterService.cs` (sibling, same shape) | exact |
| `QuestBoard.Domain/Services/ContactService.cs` (widen `UpdateAsync`) | service | CRUD | `CharacterService.cs` lines 74-96 (near-identical structure) | exact |
| `QuestBoard.Domain/Interfaces/IDungeonMasterProfileService.cs` (widen `UpsertProfileAsync`) | service (interface) | CRUD | same file, existing signature | exact |
| `QuestBoard.Domain/Services/DungeonMasterProfileService.cs` (widen `UpsertProfileAsync`) | service | CRUD | same file, lines 16-48 | exact |
| `QuestBoard.Service/Controllers/Characters/CharactersController.cs` (new `GetCroppedPicture` action; Create/Edit POST wiring) | controller | request-response (file-serving) + CRUD (form POST) | same file, `GetProfilePicture` (lines 363-373) for the read action; `Edit` POST (lines 190-294) for the dual-file wiring | exact |
| `QuestBoard.Service/Controllers/Contacts/ContactsController.cs` (new `GetCroppedContactImage` action; Create/Edit POST wiring) | controller | request-response (file-serving, with visibility gate) + CRUD | same file, `GetContactImage` (lines 321-346) for the read action w/ `IsVisibleTo` gate; `Edit` POST (lines 150-212) | exact |
| `QuestBoard.Service/Controllers/DungeonMaster/DungeonMasterController.cs` (repoint `GetDMProfilePicture`; `EditProfile` POST wiring) | controller | request-response (file-serving) + CRUD | same file, `GetDMProfilePicture` (lines 134-149), `EditProfile` POST (lines 83-132) | exact |
| `QuestBoard.Service/ViewModels/CharacterViewModels/CharacterViewModel.cs` (add `CroppedPictureFile`) | model (ViewModel) | transform (form binding) | same file, existing `ProfilePictureFile` property (lines 37-39) | exact |
| `QuestBoard.Service/ViewModels/ContactViewModels/ContactViewModel.cs` (add `CroppedPictureFile`) | model (ViewModel) | transform (form binding) | same file, existing `ContactImageFile` property (lines 24-26) | exact |
| `QuestBoard.Service/ViewModels/DungeonMasterViewModels/EditDMProfileViewModel.cs` (add `CroppedPictureFile`) | model (ViewModel) | transform (form binding) | same file, existing `ProfilePictureFile` property (lines 15-17) | exact |
| `QuestBoard.Service/wwwroot/js/image-crop.js` (NEW) | utility (client-side script) | transform (file → blob pipeline) | `site.js` — no direct analog for a Cropper.js pipeline exists; follows `site.js`'s plain-function, no-module convention | no analog (new capability) — style/convention match only |
| `QuestBoard.Service/wwwroot/css/image-crop.css` (NEW) | config (stylesheet) | — | `dm-profile.css` / `dm-profile.mobile.css` (small, feature-scoped CSS files) | role-match |
| `QuestBoard.Service/wwwroot/css/characters.css` (`.character-image` box shape) | config (stylesheet) | — | `contacts.css` `.contact-image` (near-identical rule needing the identical change) | exact (both change in parallel) |
| `QuestBoard.Service/wwwroot/css/contacts.css` (`.contact-image` box shape) | config (stylesheet) | — | `characters.css` `.character-image` | exact |
| `Views/Characters/Create.cshtml` / `Edit.cshtml` + `.Mobile.cshtml` (add crop modal + hidden input) | component (Razor view) | request-response (form) | `Views/Quest/Details.cshtml` `#addCharacterModal` (lines 819-863) for modal chrome; same file's own existing inline `@section Scripts` file-validation block for JS wiring point | exact (modal chrome), exact (form/script structure) |
| `Views/Contacts/Create.cshtml` / `Edit.cshtml` + `.Mobile.cshtml` (add crop modal + hidden input) | component (Razor view) | request-response (form) | `Views/Characters/Create.cshtml`/`Edit.cshtml` (near-identical form shape, same file-input id convention) | exact |
| `Views/DungeonMaster/EditProfile.cshtml` + `.Mobile.cshtml` (add crop modal + hidden input) | component (Razor view) | request-response (form) | `Views/Characters/Edit.cshtml` (same single-file-upload-field form shape) | exact |
| `Views/Characters/Index.cshtml` / `.Mobile.cshtml` (repoint thumbnail `src`) | component (Razor view) | request-response (read-only `<img>`) | same file, existing `<img src="@Url.Action("GetProfilePicture", ...)">` | exact — same file, just repointing the action name |
| `Views/Contacts/Index.cshtml` / `.Mobile.cshtml` (repoint thumbnail `src`) | component (Razor view) | request-response | same file, existing `<img src="@Url.Action("GetContactImage", ...)">` | exact |
| `Views/Quest/Details.cshtml` (2 occurrences, repoint participant avatars) | component (Razor view) | request-response | same file, line 119 `<img src="@Url.Action("GetProfilePicture", "Characters", ...)">` | exact |
| `Views/Quest/Manage.cshtml` (repoint roster avatar) | component (Razor view) | request-response | `Views/Quest/Details.cshtml` line 119 (identical cross-controller `Url.Action` pattern) | exact |
| `Views/Quest/_QuestCard.cshtml` (repoint inline avatar) | component (Razor partial) | request-response | same file, line 61 `<img src="@Url.Action("GetProfilePicture", "Characters", ...)">` | exact |
| `Views/QuestLog/Details.cshtml` / `.Mobile.cshtml` (repoint recap avatar) | component (Razor view) | request-response | `Views/Quest/_QuestCard.cshtml` line 61 (identical cross-controller pattern) | exact |
| `Views/Characters/Details.cshtml` / `Contacts/Details.cshtml` (+ `.Mobile.cshtml`) | component (Razor view) | request-response | **no change** — stays on `GetProfilePicture`/`GetContactImage` (original) per D-03 | not applicable (explicitly unchanged) |

## Pattern Assignments

### `QuestBoard.Domain/Interfaces/ICharacterService.cs` + `CharacterService.cs` (service, CRUD — widen `UpdateAsync`)

**Analog:** same files, existing 3-arg `UpdateAsync` overload (this is a same-family signature-widening change, not a new pattern)

**Existing interface member** (`ICharacterService.cs:53`):
```csharp
Task UpdateAsync(Character model, bool hasNewOriginalUpload, CancellationToken token = default);
```

**Existing implementation** (`CharacterService.cs:74-96`):
```csharp
/// <inheritdoc/>
public async Task UpdateAsync(Character model, bool hasNewOriginalUpload, CancellationToken token = default)
{
    // The image write and the rest of the entity's fields are saved together in a single
    // repository call so a failure in either half cannot leave the character in a
    // half-updated state (new photo, stale metadata, or vice versa).
    byte[]? croppedImageData;
    if (hasNewOriginalUpload)
    {
        // A genuinely new original arrived this request -- clear any stale crop of the
        // superseded photo, since it belonged to the photo that's being replaced.
        croppedImageData = null;
    }
    else
    {
        // No new file; model.ProfilePicture is the round-tripped existing original. Fetch
        // the currently-stored crop and pass it through unchanged so it survives an
        // unrelated-field edit.
        croppedImageData = await repository.GetCharacterCroppedPictureAsync(model.Id, token);
    }

    await repository.UpdateWithProfileImageAsync(model, model.ProfilePicture, croppedImageData, token);
}
```

**Widening pattern to apply** (per RESEARCH.md's already-verified 4-arg design — copy verbatim, adjust for Contact/DM):
```csharp
// New 4-arg overload — additive, does not remove the existing 3-arg one
Task UpdateAsync(Character model, bool hasNewOriginalUpload, byte[]? newCroppedImageData, CancellationToken token = default);
```
```csharp
public async Task UpdateAsync(Character model, bool hasNewOriginalUpload, byte[]? newCroppedImageData, CancellationToken token = default)
{
    byte[]? croppedImageData;
    if (newCroppedImageData != null)
    {
        croppedImageData = newCroppedImageData;
    }
    else if (hasNewOriginalUpload)
    {
        croppedImageData = null;
    }
    else
    {
        croppedImageData = await repository.GetCharacterCroppedPictureAsync(model.Id, token);
    }

    await repository.UpdateWithProfileImageAsync(model, model.ProfilePicture, croppedImageData, token);
}

// Existing 3-arg overload now delegates to the 4-arg one, preserving behavior for any
// caller not yet updated (e.g. ToggleRetirement's 2-arg -> base UpdateAsync path is untouched)
public Task UpdateAsync(Character model, bool hasNewOriginalUpload, CancellationToken token = default) =>
    UpdateAsync(model, hasNewOriginalUpload, newCroppedImageData: null, token);
```

**Apply the identical shape to:**
- `IContactService.cs` / `ContactService.cs` — same 3 branches, swap `GetCharacterCroppedPictureAsync` → `GetContactCroppedImageAsync`, `model.ProfilePicture` → `model.ContactImageData`.
- `IDungeonMasterProfileService.cs` / `DungeonMasterProfileService.cs` — `UpsertProfileAsync` (lines 16-48) currently hardcodes `croppedImageData: null` at both call sites in the `updateImage` branch (lines 42, 46); add a `byte[]? newCroppedImageData = null` parameter and thread it into both `UpdateBioWithProfileImageAsync(...)` calls in place of the literal `null`.

---

### `QuestBoard.Service/Controllers/Characters/CharactersController.cs` (controller, request-response file-serving)

**Analog:** same file, `GetProfilePicture` (lines 363-373)

**Existing read-action pattern to mirror exactly:**
```csharp
[HttpGet]
public async Task<IActionResult> GetProfilePicture(int id, CancellationToken token = default)
{
    var profilePicture = await characterService.GetCharacterOriginalPictureAsync(id, token);
    if (profilePicture == null)
    {
        return NotFound();
    }

    return File(profilePicture, DetectImageMimeType(profilePicture));
}

private static string DetectImageMimeType(byte[] data) =>
    data.Length >= 4 && data[0] == 0x89 && data[1] == 0x50 ? "image/png" :
    data.Length >= 6 && data[0] == 0x47 && data[1] == 0x49 ? "image/gif" :
    "image/jpeg";
```

**New sibling action** — same shape, calls the already-existing `characterService.GetCharacterCroppedPictureAsync` (Phase 45), reuses the same private `DetectImageMimeType` helper (do not duplicate it):
```csharp
[HttpGet]
public async Task<IActionResult> GetCroppedPicture(int id, CancellationToken token = default)
{
    var croppedPicture = await characterService.GetCharacterCroppedPictureAsync(id, token);
    if (croppedPicture == null)
    {
        return NotFound();
    }

    return File(croppedPicture, DetectImageMimeType(croppedPicture));
}
```

**Auth note:** `GetProfilePicture` has **no visibility/ownership check today** — it is a bare `[HttpGet]` under the controller's class-level `[Authorize]`. The new `GetCroppedPicture` must match that exactly (no additional gate) to stay consistent with its sibling — do not invent a new auth check for the cropped variant that the original doesn't have.

**Dual-file wiring pattern for `Create`/`Edit` POST** — extend the existing single-file block (lines 128-147 for Create, 253-273 for Edit) with a second `ImageFileInput` for the cropped file, then pass `newCroppedImageData` into the widened `UpdateAsync`:
```csharp
// Existing single-file pattern (Edit POST, lines 253-273) — the validation call already
// accepts a second (currently-null) cropped parameter; this phase supplies a real one.
var hasNewOriginalUpload = viewModel.ProfilePictureFile != null && viewModel.ProfilePictureFile.Length > 0;
if (hasNewOriginalUpload)
{
    var newProfilePictureFile = viewModel.ProfilePictureFile!;
    var original = new ImageFileInput(newProfilePictureFile.Length, newProfilePictureFile.ContentType,
        newProfilePictureFile.FileName, nameof(viewModel.ProfilePictureFile));

    // NEW: build the cropped ImageFileInput the same way, from the new CroppedPictureFile field
    ImageFileInput? cropped = null;
    byte[]? newCroppedImageData = null;
    if (viewModel.CroppedPictureFile is { Length: > 0 } croppedFile)
    {
        cropped = new ImageFileInput(croppedFile.Length, croppedFile.ContentType, croppedFile.FileName, nameof(viewModel.CroppedPictureFile));
        using var croppedStream = new MemoryStream();
        await croppedFile.CopyToAsync(croppedStream, token);
        newCroppedImageData = croppedStream.ToArray();
    }

    var validationErrors = imageValidationService.ValidateImagePair(original, cropped);
    // ... existing ModelState loop unchanged ...

    using var memoryStream = new MemoryStream();
    await newProfilePictureFile.CopyToAsync(memoryStream, token);
    existingCharacter.ProfilePicture = memoryStream.ToArray();

    await characterService.UpdateAsync(existingCharacter, hasNewOriginalUpload, newCroppedImageData, token);
    // (replaces the current 3-arg UpdateAsync call at line 283)
}
```

**Apply the identical file-serving + dual-file-wiring shape to:**
- `ContactsController.cs` — new `GetCroppedContactImage` action must copy `GetContactImage`'s (lines 321-346) **visibility gate** (`IsVisibleTo(contact, currentUser.Id, includeHidden)`) — this is the one controller where the sibling read action *does* have an authorization/visibility check, so the cropped variant must replicate it, not omit it (flagged as a security-parity requirement in RESEARCH.md's Security Domain section).
- `DungeonMasterController.cs` — per CONTEXT.md's Claude's Discretion, `GetDMProfilePicture` (lines 134-149) is simply repointed in place to call `dmProfileService.GetCroppedPictureAsync` (already exists, already does `Cropped ?? Original` fallback per Phase 45) instead of `GetProfilePictureAsync` — no new sibling action needed since D-03 gives DM no page that needs the true original. Preserve the existing `IsTargetInActiveGroupAsync` gate (line 137) unchanged.

---

### `QuestBoard.Service/ViewModels/CharacterViewModels/CharacterViewModel.cs` (ViewModel, transform)

**Analog:** same file, existing `ProfilePictureFile` property (lines 36-39)

```csharp
public byte[]? ProfilePicture { get; set; }

[MaxFileSize(5 * 1024 * 1024, ErrorMessage = "Profile picture cannot exceed 5 MB")]
[AllowedExtensions(new[] { ".jpg", ".jpeg", ".png", ".gif" }, ErrorMessage = "Only image files (JPG, PNG, GIF) are allowed")]
public IFormFile? ProfilePictureFile { get; set; }
```

**New property to add**, same validation attributes reused (the crop output is still a JPG/PNG under the same 5MB ceiling per RESEARCH.md's `toBlob()` quality setting):
```csharp
[MaxFileSize(5 * 1024 * 1024, ErrorMessage = "Cropped image cannot exceed 5 MB")]
[AllowedExtensions(new[] { ".jpg", ".jpeg", ".png", ".gif" }, ErrorMessage = "Only image files (JPG, PNG, GIF) are allowed")]
public IFormFile? CroppedPictureFile { get; set; }
```

**Apply identically to:**
- `ContactViewModel.cs` (mirrors `ContactImageFile`, lines 24-26) — same two attributes, same naming convention (`CroppedPictureFile`).
- `EditDMProfileViewModel.cs` (mirrors `ProfilePictureFile`, lines 15-17) — same two attributes.

---

### `Views/Characters/Edit.cshtml` (component, request-response — crop modal + dual-file wiring)

**Analog:** `Views/Quest/Details.cshtml` `#addCharacterModal` (lines 819-863) for the dark Bootstrap modal chrome; same file's own existing inline script block (lines 140-174) for the wiring point.

**Existing modal chrome pattern to copy** (only the *chrome* — UI-SPEC.md section "1. Crop Modal" supplies the phase-specific inner markup with `cropper-canvas`/`cropper-selection`, which must be used verbatim over this chrome, not the `characterSelect` form body shown here):
```html
<div class="modal fade" id="addCharacterModal" tabindex="-1" aria-labelledby="addCharacterModalLabel" aria-hidden="true">
    <div class="modal-dialog">
        <div class="modal-content bg-dark text-light">
            <div class="modal-header border-secondary">
                <h5 class="modal-title" id="addCharacterModalLabel">
                    <i class="fas fa-user-plus me-2"></i>Add Character to Signup
                </h5>
                <button type="button" class="btn-close btn-close-white" data-bs-dismiss="modal" aria-label="Close"></button>
            </div>
            <!-- body -->
            <div class="modal-footer border-secondary">
                <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">
                    <i class="fas fa-times me-2"></i>Cancel
                </button>
                <button type="submit" class="btn btn-success">
                    <i class="fas fa-check me-2"></i>Add Character
                </button>
            </div>
        </div>
    </div>
</div>
```
**Deviation required by UI-SPEC.md:** the crop modal adds `data-bs-backdrop="static" data-bs-keyboard="false"` (this existing modal doesn't have these attributes — D-04's "no accidental dismiss loses the pending crop" reasoning is specific to this phase, not a pre-existing convention) and uses `modal-dialog-centered modal-lg` instead of the plain `modal-dialog`. Use UI-SPEC.md's exact markup block verbatim (already fully specified there, including button copy "Discard Photo"/"Use This Crop" and the amber `theme-color` handle) — do not re-derive it from this analog beyond matching the chrome classes (`modal-content bg-dark text-light`, `modal-header border-secondary`, `modal-footer border-secondary`, `btn-close btn-close-white`).

**Existing inline-script wiring point to replace/extend** (`Edit.cshtml:140-174`, `Create.cshtml:143-177` — same shape in both):
```javascript
document.getElementById('profilePictureInput')?.addEventListener('change', function(e) {
    const file = e.target.files[0];
    const errorDiv = document.getElementById('fileSizeError');
    if (file) {
        if (file.size > MAX_FILE_SIZE) { /* existing size-error copy, keep as first-line defense */ }
        if (!ALLOWED_TYPES.includes(file.type)) { /* existing type-error copy */ }
        errorDiv.style.display = 'none';
    }
});
```
This existing `change` listener's client-side size/type pre-check is **not removed** — RESEARCH.md's pipeline runs *after* this check passes (the `createImageBitmap` pre-processing step only receives files that already passed the existing size/type gate). Add the `image-crop.js` include and `initImageCrop(...)` call in the same `@section Scripts` block, after this existing listener, per RESEARCH.md's "Pattern 1: Shared crop-modal script" and the exact call signature shown there (`fileInputId`, `hiddenCroppedInputName`, `aspectRatio: 1`).

**Current-photo preview repoint** (`Edit.cshtml:30`, `Create.cshtml:33`, and both `.Mobile.cshtml` siblings) — change the action name only, no markup change:
```html
<!-- Before -->
<img src="@Url.Action("GetProfilePicture", new { id = Model.Id })" alt="Current" class="img-thumbnail" style="max-width: 200px;" />
<!-- After -->
<img src="@Url.Action("GetCroppedPicture", new { id = Model.Id })" alt="Current" class="img-thumbnail" style="max-width: 200px;" />
```

**Hidden cropped-file input** — add directly after the existing visible `<input type="file" asp-for="ProfilePictureFile" ...>` (line 33/36), per UI-SPEC.md section 5:
```html
<input type="file" name="CroppedPictureFile" id="croppedPictureFileInput" style="display:none" />
```

**Apply the identical four changes (modal include, script wiring, preview repoint, hidden input) to:**
- `Views/Characters/Create.cshtml` + `.Mobile.cshtml`
- `Views/Contacts/Create.cshtml` / `Edit.cshtml` + `.Mobile.cshtml` (file-input id will be `contactImageInput`-equivalent per that form's existing convention — confirm exact id at implementation time by reading the file, since CONTEXT.md's inventory doesn't state it explicitly)
- `Views/DungeonMaster/EditProfile.cshtml` + `.Mobile.cshtml`

---

### `Views/Quest/Details.cshtml` (component, request-response — endpoint repoint only, no crop UI)

**Analog:** same file, line 119

**Existing pattern:**
```html
<img src="@Url.Action("GetProfilePicture", "Characters", new { id = participant.Character.Id })"
     alt="@participant.Character.Name"
     class="character-mini-avatar me-2"
     style="width: 32px; height: 32px; border-radius: 50%; object-fit: cover;"
     onerror="this.style.display='none'; this.nextElementSibling.style.display='inline-flex';" />
```

**Repoint pattern** (action name only — no other attribute changes):
```html
<img src="@Url.Action("GetCroppedPicture", "Characters", new { id = participant.Character.Id })"
     ...
```

**Apply the identical single-attribute repoint to:**
- `Views/Quest/Details.cshtml` — second occurrence (waitlist section, per CONTEXT.md's code_context inventory)
- `Views/Quest/Manage.cshtml` — participant roster avatar
- `Views/Quest/_QuestCard.cshtml` — line 61, identical `Url.Action("GetProfilePicture", "Characters", ...)` call
- `Views/QuestLog/Details.cshtml` + `.Mobile.cshtml` — recap participant avatar

**Views/Characters/Index.cshtml / .Mobile.cshtml and Views/Contacts/Index.cshtml / .Mobile.cshtml:** same single-attribute repoint pattern, but action names are `GetProfilePicture`→`GetCroppedPicture` (Characters, same-controller `Url.Action`) and `GetContactImage`→`GetCroppedContactImage` (Contacts, same-controller `Url.Action`).

**Views NOT to touch (explicit no-op per D-03):** `Views/Characters/Details.cshtml`, `Details.Mobile.cshtml`, `Views/Contacts/Details.cshtml`, `Details.Mobile.cshtml` — these keep calling `GetProfilePicture`/`GetContactImage` (original) exactly as they do today.

---

### `QuestBoard.Service/wwwroot/css/characters.css` (config, stylesheet — list-card square box)

**Analog:** `contacts.css` `.contact-image` (near-identical twin rule that changes in parallel)

**Existing rule** (`characters.css:112-125`):
```css
.characters-page .character-image {
    position: relative;
    width: 100%;
    height: 200px;
    margin-bottom: 1rem;
    border-radius: 8px;
    overflow: hidden;
    background: linear-gradient(135deg, rgba(0, 0, 0, 0.4), rgba(0, 0, 0, 0.2));
    display: flex;
    align-items: center;
    justify-content: center;
    box-shadow: 0 4px 12px rgba(0, 0, 0, 0.4);
    transition: all 0.3s ease;
}
```

**Change per D-02 / UI-SPEC.md section 4** — replace `height: 200px;` with an aspect-ratio box, keep every other property:
```css
.characters-page .character-image {
    position: relative;
    width: 100%;
    aspect-ratio: 1 / 1;
    height: auto;
    margin-bottom: 1rem;
    border-radius: 8px;
    overflow: hidden;
    background: linear-gradient(135deg, rgba(0, 0, 0, 0.4), rgba(0, 0, 0, 0.2));
    display: flex;
    align-items: center;
    justify-content: center;
    box-shadow: 0 4px 12px rgba(0, 0, 0, 0.4);
    transition: all 0.3s ease;
}
```

**Mobile override to remove entirely** — `characters.css:278-285`'s `@media (max-width: 768px) { ... height: 180px; ... }` block's `height: 180px` line (and `contacts.css:218-225`'s identical twin) must be deleted, not just changed, since the `aspect-ratio` rule already handles responsive square sizing at any width (UI-SPEC.md section 4, explicit instruction).

**Apply the identical two changes (box-shape + mobile-override removal) to `contacts.css`'s `.contact-image` rule** (lines 91-121 base rule, 218-225 mobile override).

**No change needed:** `dm-profile.css` / `dm-profile.mobile.css` — DM's 128×128/80×80 `rounded-circle` box is already square, per D-01/D-02.

---

## Shared Patterns

### Magic-byte MIME sniffing for all `File()`-returning read actions
**Source:** `CharactersController.cs:375-378`, duplicated verbatim in `ContactsController.cs:348-351` and inlined in `DungeonMasterController.cs:142-146`
**Apply to:** Every new/modified cropped-read action (`GetCroppedPicture`, `GetCroppedContactImage`) — reuse each controller's own existing private `DetectImageMimeType` (or DM's inline expression) rather than introducing a shared static helper; the codebase already tolerates one copy per controller.
```csharp
private static string DetectImageMimeType(byte[] data) =>
    data.Length >= 4 && data[0] == 0x89 && data[1] == 0x50 ? "image/png" :
    data.Length >= 6 && data[0] == 0x47 && data[1] == 0x49 ? "image/gif" :
    "image/jpeg";
```

### `hasNewOriginalUpload` signal, hoisted as a single local
**Source:** `CharactersController.cs:248-251`, `ContactsController.cs:179-182`
**Apply to:** Any controller wiring that now also needs a `newCroppedImageData` local — compute both once at the top of the upload-handling block so the byte-copy gate and the service-call signal can never drift apart, matching the existing single-source-of-truth comment convention already in both files.
```csharp
var hasNewOriginalUpload = viewModel.ProfilePictureFile != null && viewModel.ProfilePictureFile.Length > 0;
```

### `IImageValidationService.ValidateImagePair(original, cropped)` — now called with a real second argument
**Source:** `QuestBoard.Domain/Interfaces/IImageValidationService.cs`, `QuestBoard.Domain/Services/ImageValidationService.cs` (unchanged this phase — already accepts an optional cropped `ImageFileInput`)
**Apply to:** All three controllers' Create/Edit POST actions — every existing call site passes `cropped: null` today; this phase's only change to the call sites is constructing a real `ImageFileInput` for the cropped file and passing it instead of the literal `null`. Do not modify `ImageValidationService.cs` itself — its per-file validation logic (MIME/extension/size, lines 24-50) is generic and already handles a second real file correctly.

### Bootstrap dark modal chrome (`modal-content bg-dark text-light`)
**Source:** `Views/Quest/Details.cshtml:819-863` (`#addCharacterModal`)
**Apply to:** The crop modal in all 10 upload views — use UI-SPEC.md's fully-specified markup (Layout & Component Specifications, section 1), which already derives from this exact chrome convention plus the phase-specific `data-bs-backdrop="static"`/`modal-lg` additions.

### CDN-only third-party script vendoring (no `wwwroot/lib/`)
**Source:** `Views/Shared/_Layout.cshtml:12,218-219` (Bootstrap CSS/JS, jQuery all CDN `<script>`/`<link>` tags, no committed vendor files)
**Apply to:** The new Cropper.js v2.1.1 `<script>` tag — follow the exact same CDN-with-SRI convention (see RESEARCH.md Standard Stack Installation block for the pinned URL), not a new `wwwroot/lib/cropperjs/` folder.

### Feature-scoped script/CSS files, not merged into `site.js`
**Source:** `wwwroot/js/site.js` (global helpers only: `addProposedDate`/`removeProposedDate`, masonry/toast/datetime helpers per RESEARCH.md) and the existing per-view inline `@section Scripts` convention (`Characters/Create.cshtml:143-217`)
**Apply to:** `wwwroot/js/image-crop.js` (new) — a single shared initializer function included via `<script src="~/js/image-crop.js"></script>` in each of the 10 upload views' own `@section Scripts` block, never merged into `site.js`.

## No Analog Found

| File | Role | Data Flow | Reason |
|------|------|-----------|--------|
| `QuestBoard.Service/wwwroot/js/image-crop.js` | utility (client script) | transform (EXIF-correct → downscale → Cropper.js init → `$toCanvas()` extraction → dual-file population) | This codebase has no prior Cropper.js/canvas-pipeline code — RESEARCH.md's Architecture Patterns 1-4 (fully-sourced against MDN/Cropper.js v2 official docs) are the authoritative reference for this file's contents, not a same-repo analog. Structural convention (one shared file, included per-view) still follows the `site.js`-adjacent pattern noted above. |
| `QuestBoard.Service/wwwroot/css/image-crop.css` | config (stylesheet) | — | No prior modal-specific CSS file exists in this repo (existing modals like `#addCharacterModal` use only Bootstrap utility classes inline, no dedicated CSS file) — RESEARCH.md's Recommended Project Structure scopes this as a new, small, feature-specific file; UI-SPEC.md's Spacing Scale section (400px/320px crop-stage height) is the authoritative content source. |

## Metadata

**Analog search scope:** `QuestBoard.Domain/{Interfaces,Services}/`, `QuestBoard.Repository/*.cs`, `QuestBoard.Service/Controllers/{Characters,Contacts,DungeonMaster}/`, `QuestBoard.Service/ViewModels/{CharacterViewModels,ContactViewModels,DungeonMasterViewModels}/`, `QuestBoard.Service/Views/{Characters,Contacts,DungeonMaster,Quest,QuestLog,Shared}/`, `QuestBoard.Service/wwwroot/{css,js}/`
**Files scanned:** ~30 (all files named in 46-CONTEXT.md's code_context/Integration Points sections, plus their Domain/Repository-layer dependencies)
**Pattern extraction date:** 2026-07-07
