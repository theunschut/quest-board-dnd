# Architecture Research

**Domain:** Client-side image crop-before-upload for a server-rendered ASP.NET Core 10 MVC app (no SPA, no bundler)
**Researched:** 2026-07-04
**Confidence:** HIGH (codebase integration points — verified by direct file read) / MEDIUM (crop-library API choice — cross-verified across two independent sources)

## Standard Architecture

### System Overview

```
┌─────────────────────────────────────────────────────────────────────┐
│  Browser (no build step — plain <script> tags)                      │
│  ┌───────────────────────┐   ┌──────────────────────────────────┐  │
│  │ <input type="file">   │──▶│ Cropper.js v1.6.2 (CDN, vendored) │  │
│  │ (existing form field) │   │ new Cropper(imgEl, opts)          │  │
│  └───────────────────────┘   │ .getCroppedCanvas()               │  │
│                                └──────────────┬───────────────────┘  │
│                                               │ canvas.toBlob()      │
│                                               ▼                      │
│                                ┌──────────────────────────────────┐ │
│                                │ DataTransfer trick: replace the   │ │
│                                │ <input type="file">'s FileList    │ │
│                                │ with the cropped Blob → File      │ │
│                                └──────────────┬───────────────────┘ │
│                                               │ normal <form> POST   │
│                                               │ (multipart/form-data)│
├───────────────────────────────────────────────┼──────────────────────┤
│  QuestBoard.Service (MVC)                    ▼                      │
│  GuildMembersController / DungeonMasterController                   │
│  Edit/Create POST actions — model-bind IFormFile (unchanged shape)  │
│  + read ORIGINAL bytes from a second posted IFormFile               │
├───────────────────────────────────────────────────────────────────────┤
│  QuestBoard.Domain (business logic — no EF, no System.Drawing IO)   │
│  ICharacterService / IDungeonMasterProfileService                   │
│  + new: ImageUploadValidator (size/type/dimension checks)           │
├───────────────────────────────────────────────────────────────────────┤
│  QuestBoard.Repository (EF Core — byte[] I/O only)                   │
│  CharacterImageEntity / DungeonMasterProfileImageEntity              │
│  + ADD: OriginalImageData byte[] column (existing ImageData column   │
│    is repurposed to mean "cropped/display" bytes — see Recommended   │
│    Schema Shape below)                                               │
│  SQL Server — migration auto-applied on startup                     │
└─────────────────────────────────────────────────────────────────────┘
```

**Key point:** No server-side image-processing library is required. Cropping happens entirely client-side (canvas). The server's job is unchanged in kind — it still just reads an `IFormFile` into a `byte[]` — the only new work is reading a *second* `IFormFile` (for the original) and validating that the crop a client claims to have applied is sane. No `System.Drawing`, `ImageSharp`, or `SkiaSharp` needs to enter the Domain or Repository layer for this milestone. This directly avoids the exact problem — `SkiaSharp` native-library availability on the deployment host — that caused this feature to be paused since v1.0 (see PROJECT.md Key Decisions: "Profile picture crop paused... SkiaSharp native lib availability on deployment host unverified").

### Component Responsibilities

| Component | Responsibility | Typical Implementation |
|-----------|----------------|-------------------------|
| Cropper.js v1.6.2 (vendored JS+CSS in `wwwroot/lib/cropperjs/`) | Renders the crop UI over the just-selected image, exposes `getCroppedCanvas()` | Plain `<script>`/`<link>` tags, no npm — see Library Choice below |
| Crop-init JS (new `wwwroot/js/image-crop.js`) | Wires `<input type="file">` `change` event → shows a crop modal → on "Confirm crop" rewrites the form's file input(s) via `DataTransfer` so a normal POST carries the cropped bytes | Vanilla JS, follows existing `site.js` script-tag convention (no module bundler) |
| `CharacterViewModel` / `EditDMProfileViewModel` (Service layer) | Model-binds the posted file(s); unchanged `[MaxFileSize]`/`[AllowedExtensions]` attributes still apply to whichever file is now "the" `ProfilePictureFile` | Add a second nullable `IFormFile? OriginalPictureFile` property |
| `GuildMembersController` / `DungeonMasterController` (Service layer) | Reads both `IFormFile`s into `byte[]`, delegates size/type/dimension validation to Domain, calls the (widened) upsert service method with both byte arrays | Same `CopyToAsync(memoryStream)` pattern already used 3x in the codebase — just called twice |
| New `IImageValidationService` (Domain layer, recommended) | Centralizes size/MIME validation that today is duplicated inline in 3 controller call sites (`GuildMembersController.Create`, `.Edit`, `DungeonMasterController.EditProfile`) | Plain C# — no image-processing dependency needed for a size/MIME check |
| `CharacterService` / `DungeonMasterProfileService` (Domain layer) | Orchestrates the upsert call, unchanged responsibility, wider signature (`byte[] cropped, byte[] original`) | Extend existing `UpsertProfileAsync`/character-picture methods |
| `CharacterRepository` / `DungeonMasterProfileRepository` (Repository layer) | Persists both byte arrays into the (modified) image entity; pure EF Core I/O, no processing | Extend existing `UpdateProfileImageAsync`/`UpsertProfileImageAsync` to accept and set two `byte[]` properties on the same row |
| `CharacterImageEntity` / `DungeonMasterProfileImageEntity` (Repository layer) | Adds `OriginalImageData byte[]?` column alongside existing `ImageData` (kept as the display/cropped column — see schema section) | EF Core migration, `dotnet ef migrations add` |
| `GetProfilePicture` / `GetDMProfilePicture` actions (Service layer) | Unchanged for the character-card/DM-list "cropped" thumbnail use. New action added to serve the **original** for the one place that needs it — the character details page | `return File(bytes, contentType)`, same pattern, new repository method `GetCharacterOriginalPictureAsync` |

## Recommended Project Structure

```
QuestBoard.Service/
├── wwwroot/
│   ├── lib/
│   │   └── cropperjs/                # NEW — vendored (not CDN-only; see rationale)
│   │       ├── cropper.min.js        # v1.6.2, pinned
│   │       └── cropper.min.css
│   ├── js/
│   │   ├── site.js                   # existing — untouched
│   │   └── image-crop.js             # NEW — shared crop-modal wiring, reused by all 3 forms
│   └── css/
│       └── image-crop.css            # NEW — crop modal chrome, follows modern-card conventions
├── ViewModels/
│   ├── CharacterViewModels/
│   │   └── CharacterViewModel.cs     # MODIFIED — add OriginalPictureFile IFormFile?
│   └── DungeonMasterViewModels/
│       └── EditDMProfileViewModel.cs # MODIFIED — add OriginalPictureFile IFormFile?
├── Controllers/
│   ├── Characters/
│   │   └── GuildMembersController.cs # MODIFIED — Create/Edit POST actions; new GetOriginalPicture action
│   └── DungeonMaster/
│       └── DungeonMasterController.cs # MODIFIED — EditProfile POST; new GetOriginalPicture action
└── Views/
    ├── GuildMembers/
    │   ├── Create.cshtml / .Mobile.cshtml   # MODIFIED — crop modal markup + script include
    │   ├── Edit.cshtml / .Mobile.cshtml     # MODIFIED — same
    │   └── Details.cshtml                   # MODIFIED — switch <img> src to GetOriginalPicture (issue #78)
    └── DungeonMaster/
        └── EditProfile.cshtml / .Mobile.cshtml # MODIFIED — crop modal markup + script include

QuestBoard.Domain/
├── Interfaces/
│   ├── ICharacterService.cs                    # MODIFIED — widen picture-upsert signature; add GetCharacterOriginalPictureAsync
│   ├── IDungeonMasterProfileService.cs         # MODIFIED — same
│   └── IImageValidationService.cs              # NEW — optional but recommended (see Patterns)
├── Services/
│   ├── CharacterService.cs                     # MODIFIED
│   ├── DungeonMasterProfileService.cs          # MODIFIED
│   └── ImageValidationService.cs               # NEW
└── Models/
    ├── Character.cs                            # MODIFIED — add OriginalProfilePicture byte[]?
    └── DungeonMasterProfile.cs                 # MODIFIED — same

QuestBoard.Repository/
├── Entities/
│   ├── CharacterImageEntity.cs                 # MODIFIED — add OriginalImageData byte[]?
│   └── DungeonMasterProfileImageEntity.cs      # MODIFIED — same
├── CharacterRepository.cs                      # MODIFIED — UpdateProfileImageAsync takes 2 byte[]?; add GetCharacterOriginalPictureAsync
├── DungeonMasterProfileRepository.cs            # MODIFIED — UpsertProfileImageAsync takes 2 byte[]?; add GetOriginalPictureAsync
├── Automapper/EntityProfile.cs                 # MODIFIED — map new byte[]? properties
└── Migrations/
    └── {timestamp}_AddOriginalImageColumn.cs    # NEW — single additive migration
```

### Structure Rationale

- **Vendor the JS/CSS instead of pure CDN `<script src="https://...">`:** Bootstrap/FontAwesome are loaded via CDN in `_Layout.cshtml` today, so a CDN reference would be *consistent* with existing convention — but Cropper.js is load-bearing for a core upload flow, not decorative chrome. Vendor the two files into `wwwroot/lib/cropperjs/` so the crop feature does not silently break if the CDN is unreachable from the small self-hosted LXC deployment's network path. This mirrors how the project already treats `site.js` (self-hosted, not CDN).
- **One shared `image-crop.js`, not three duplicated inline scripts:** All 3 upload forms (`GuildMembers/Create`, `GuildMembers/Edit`, `DungeonMaster/EditProfile`) currently duplicate an inline file-size/type-validation `<script>` block per view (verified: `Edit.cshtml` lines 144-174 and equivalents in `Create.cshtml`/`EditProfile.cshtml`). Adding crop-modal wiring is the right moment to extract a shared script, matching this project's own precedent of consolidating duplicated JS (Phase 42 collapsed 4 duplicated toast-init scripts into `site.js`).
- **`IImageValidationService` in Domain, not inline in controllers:** Today, MIME/size validation is duplicated 3× directly in controller methods (`GuildMembersController.Create`, `.Edit`, `DungeonMasterController.EditProfile`) — a known duplication pattern in this codebase already (PROJECT.md's Known Issues separately calls out a 3× duplication of `GetActiveBoardTypeAsync` as tech debt). Adding a second file input is a natural moment to centralize this in one Domain-layer service rather than adding a 4th inline copy.

## Architectural Patterns

### Pattern 1: Client posts TWO files, not crop coordinates

**What:** On crop-confirm, the browser produces a cropped `Blob` via `canvas.toBlob()`. Rather than sending crop coordinates (x/y/w/h) to the server and asking it to re-crop the original — which would require a server-side image-processing library, exactly what caused this feature's original pause — the browser sends **both** the original file bytes (unmodified, from the original `File` object still held in memory) and the cropped blob as two separate `IFormFile` entries in the same `multipart/form-data` POST.

**When to use:** Any time "keep both variants" is a hard requirement (it is here — the character details page must keep showing the original per issue #78) and a server-side image pipeline is explicitly undesirable (it is — SkiaSharp was paused for exactly this).

**Trade-offs:**
- Zero new server dependencies; Domain/Repository layering stays exactly as strict as it is today (no image-processing package needed in any project).
- Sidesteps the SkiaSharp-on-Linux-host verification blocker entirely — this is the single most important architectural consequence of this choice.
- Server-side validation is still meaningful: it can check both files are valid image MIME types and within the existing 5 MB cap without ever decoding pixels.
- Slightly larger request payload (transmits original bytes even though the server already effectively "has" them from a prior upload on Edit) — acceptable at this app's scale (17 members, occasional profile edits, not a hot path).
- Server cannot mathematically re-verify that the "cropped" blob is actually a crop of the "original" blob without decoding both — accepted risk; this is a trusted internal tool for a friend group, not a public upload surface.

**Example (client-side, vanilla JS, no bundler):**
```javascript
// wwwroot/js/image-crop.js
function wireCropInput(inputId, previewImgId, cropperOptions) {
    const input = document.getElementById(inputId);
    input.addEventListener('change', (e) => {
        const file = e.target.files[0];
        if (!file) return;

        const reader = new FileReader();
        reader.onload = (evt) => {
            const img = document.getElementById(previewImgId);
            img.src = evt.target.result;
            openCropModal(img, file, input, cropperOptions);
        };
        reader.readAsDataURL(file);
    });
}

function openCropModal(imgEl, originalFile, fileInputEl, options) {
    const cropper = new Cropper(imgEl, { aspectRatio: 1, viewMode: 1, ...options });

    document.getElementById('confirmCropBtn').onclick = () => {
        cropper.getCroppedCanvas({ width: 512, height: 512 }).toBlob((croppedBlob) => {
            const croppedFile = new File([croppedBlob], originalFile.name, { type: 'image/jpeg' });

            // Replace the visible file input's FileList with the cropped result
            // so the existing <form> posts the cropped bytes as ProfilePictureFile —
            // no JS-driven fetch/AJAX needed, the form submits normally.
            const croppedDT = new DataTransfer();
            croppedDT.items.add(croppedFile);
            fileInputEl.files = croppedDT.files;

            // Also stash the untouched original into a second hidden file input
            // so both ride along in the same multipart/form-data POST.
            const originalInput = document.getElementById('originalPictureInput');
            const originalDT = new DataTransfer();
            originalDT.items.add(originalFile);
            originalInput.files = originalDT.files;

            cropper.destroy();
            closeCropModal();
        }, 'image/jpeg', 0.92);
    };
}
```

```html
<!-- Existing field, now populated with the CROPPED result before submit -->
<input type="file" asp-for="ProfilePictureFile" class="form-control" accept="image/*" id="profilePictureInput" />

<!-- New hidden field carrying the untouched ORIGINAL -->
<input type="file" asp-for="OriginalPictureFile" class="d-none" id="originalPictureInput" />
```

This is the cleanest way to wire a vanilla-JS/CDN-delivered crop library into an existing upload `<form>` so the cropped result — not the raw file — is what gets posted: **no AJAX, no fetch, no JS-driven `FormData` submit.** The existing Razor `<form method="post" enctype="multipart/form-data">` and `asp-for` model binding continue to work completely unchanged; only the *contents* of the `FileList` on two `<input type="file">` elements are swapped via `DataTransfer` immediately before the user clicks the existing "Save" submit button.

### Pattern 2: Validation lives in Domain, byte[] I/O stays in Repository

**What:** The Domain layer's `IImageValidationService` (or inline checks in `CharacterService`/`DungeonMasterProfileService`) validates business rules purely from the `byte[]` payloads it's handed — file size and allowed MIME type (logic that already exists 3× in controllers today, just needs de-duplicating). The Repository layer never validates anything; it only reads/writes the two `byte[]` columns.

**When to use:** Any validation that depends on business meaning (file size limits, allowed formats) belongs in Domain because it's a business rule, not a storage concern — consistent with this codebase's existing rule that "business logic lives in services, not controllers" (validated requirement in PROJECT.md, Phase 02).

**Trade-offs:**
- Matches the project's existing hard boundary: "Domain layer must not depend directly on Repository entities" and "EF packages only in Repository" — this pattern requires zero EF references and zero image-processing package references in Domain.
- Removes the current 3x duplication of MIME/size-check logic sitting directly in controllers today (verified in `GuildMembersController.Create`/`.Edit` and `DungeonMasterController.EditProfile`). Fixing this existing duplication is optional scope, not required by the four backlog items, but this phase is a natural moment to do it since the validation surface is being touched anyway.
- Controllers still do the actual `IFormFile.CopyToAsync(memoryStream)` byte-extraction (unavoidable — `IFormFile` is an ASP.NET Core Service-layer type and must not leak into Domain).

**Example:**
```csharp
// QuestBoard.Domain/Interfaces/IImageValidationService.cs
public interface IImageValidationService
{
    /// <summary>
    /// Returns null if the image passes business rules (size cap, allowed MIME type),
    /// or an error message describing the failure.
    /// </summary>
    string? ValidateImage(byte[] imageBytes, string contentType, long maxSizeBytes);
}
```

### Pattern 3: Both file inputs are lost on server-side validation failure — same as today, now doubled

**What:** When `ModelState.IsValid` is false or an inline check fails, the controller currently does `return View(viewModel)`. With two file inputs, both must be surfaced consistently — but `IFormFile` values are never round-tripped to the client on a failed POST (browsers cannot pre-populate `<input type="file">` for security reasons), and this is already true today for the single-file case in this codebase. No new problem is introduced, but the UI should tell the user their crop selection was lost and prompt them to re-select/re-crop, rather than assuming either file persists.

**When to use:** Any validation-failure branch in `GuildMembersController.Create/Edit` or `DungeonMasterController.EditProfile`.

## Data Flow

### Upload + Crop Request Flow

```
User selects a file in <input type="file">
    v
image-crop.js 'change' handler -> FileReader.readAsDataURL -> shows Cropper.js modal
    v
User adjusts crop box, clicks "Confirm Crop" (NEW button, not the form's Save button)
    v
cropper.getCroppedCanvas().toBlob() -> cropped File built via DataTransfer,
replaces ProfilePictureFile's FileList; original File placed into a new
hidden OriginalPictureFile input via a second DataTransfer
    v
User clicks existing "Save Changes" button -> normal <form> POST (multipart/form-data)
    v
Controller (GuildMembersController.Edit / DungeonMasterController.EditProfile)
    reads BOTH IFormFiles into byte[] (existing CopyToAsync pattern, called twice)
    v
Domain service (CharacterService / DungeonMasterProfileService)
    validates via IImageValidationService, then calls repository upsert with 2 byte[]
    v
Repository (CharacterRepository.UpdateProfileImageAsync / DungeonMasterProfileRepository.UpsertProfileImageAsync)
    writes CharacterImageEntity.ImageData (cropped) + .OriginalImageData (original)
    v
SQL Server -- EF Core SaveChangesAsync
    v
RedirectToAction (unchanged)
```

### Display Flow (existing thumbnail vs. new "show original")

```
Guild list / character card / DM directory
    -> <img src="GetProfilePicture/{id}">  (unchanged -- always serves the CROPPED/display image)

Character details page (the one place that must show the ORIGINAL per issue #78)
    -> <img src="GetOriginalPicture/{id}"> (NEW action, mirrors GetProfilePicture exactly,
       reads the new OriginalImageData column instead, falling back to ImageData if
       OriginalImageData is NULL for pre-existing rows)
```

## Recommended Schema Shape

**Decision: one entity, two `byte[]` columns — NOT two related rows.**

```csharp
// QuestBoard.Repository/Entities/CharacterImageEntity.cs — MODIFIED
[Table("CharacterImages")]
public class CharacterImageEntity : IEntity
{
    [Key]
    [ForeignKey(nameof(Character))]
    public int Id { get; set; }

    [Required]
    public byte[] ImageData { get; set; } = [];          // EXISTING — repurposed as "display/cropped" bytes

    public byte[]? OriginalImageData { get; set; }        // NEW — nullable so existing rows migrate cleanly

    public virtual CharacterEntity Character { get; set; } = null!;
}
```

Same change mirrored on `DungeonMasterProfileImageEntity`.

**Why one entity/two columns, not two related rows:**

1. **Cardinality is fixed and 1:1, not 1:many.** There is exactly one "current cropped" and exactly one "current original" per character/profile at any time — never a history, never multiple originals. A second related entity (e.g. `CharacterOriginalImageEntity`) would just duplicate `CharacterImageEntity`'s existing `PK=FK` pattern a second time for no benefit: a second table, a second cascade-delete relationship to configure, and a second `Include()` everywhere the image is loaded, all to store what is fundamentally the same row's data.
2. **They are always read/written together.** Every existing repository method (`UpdateProfileImageAsync`, `UpsertProfileImageAsync`) already loads the image row with `.Include(c => c.ProfileImage)` and treats it as a single atomic unit (null → create, exists → update in place). Splitting into two rows would only add join overhead for zero query-pattern benefit — there is no scenario in this app where you fetch the cropped image without potentially needing the original.
3. **Matches the existing codebase convention exactly.** An earlier migration (`MoveCharacterImagesToSeparateTable`) already established the precedent that image bytes live in a dedicated 1-row-per-owner table, kept separate from the owning entity purely so image bytes aren't loaded on every non-image query of `CharacterEntity`/`DungeonMasterProfileEntity`. Adding a second nullable column to that *same* dedicated table preserves that exact isolation property (a `Characters` list query still never touches `CharacterImages` at all) while avoiding a third table.
4. **Migration is trivially additive and reversible.** `ALTER TABLE CharacterImages ADD OriginalImageData varbinary(max) NULL` — no data loss risk, no backfill required (nullable, so all pre-existing rows simply have `OriginalImageData = NULL` until the owner next edits their photo; `GetOriginalPicture` should fall back to `ImageData` when `OriginalImageData IS NULL`, so old photos don't visually "lose" their original in the UI — they just show the same image for both).

**When two related rows WOULD be justified (and why not here):** If the requirement were "keep a history of every crop ever made" (1:many) or "support multiple pending uploads before one is chosen" a related entity would be correct. Issue #78 explicitly wants exactly one original + one cropped, replaced together on every edit — a two-column single row is the minimal correct shape.

## Library Choice: Cropper.js v1.6.2 (not v2)

Cropper.js is the de facto standard vanilla-JS, canvas-based, touch-friendly crop library — CDN-deliverable with no build step, which fits this project's constraint exactly.

**Version finding (cross-verified across the official homepage/guide and the dedicated v1 docs page — MEDIUM confidence, since both sources are web-fetched docs rather than a package registry, but the two independent fetches agree):** Cropper.js v2 (`https://unpkg.com/cropperjs`, current default distribution) is a ground-up rewrite onto a **custom-elements/Web Components API** (`<cropper-canvas>`, `<cropper-image>`, `<cropper-selection>`) — a materially different, heavier integration surface than the classic v1 API. Cropper.js v1.6.2 retains the simple, extremely well-documented `new Cropper(imageElement, options)` class with a `.getCroppedCanvas()` method that hands back a plain `<canvas>` — the pattern used in Pattern 1 above and in the vast majority of "crop before multipart upload" tutorials/integrations across server-rendered frameworks.

**Recommendation: use v1.6.2, pinned, vendored — not v2.** Reasons:
- v1's plain class API maps directly onto `getCroppedCanvas().toBlob()` with zero framework/component-model learning curve, for a team with no other web-component usage anywhere in this codebase.
- v2's custom-elements API would be the only web component in the entire application — a one-off architectural inconsistency for a vanilla-JS, no-bundler codebase that otherwise uses plain DOM APIs (`document.getElementById`, `addEventListener`) everywhere (verified in `Edit.cshtml`'s existing inline script).
- v1 remains distributed under its own dist-tag (cdnjs/jsdelivr/unpkg), and vendoring a specific pinned file protects against any future v2-only breaking changes regardless.

**Installation (vendored, recommended):**
```html
<!-- In the 3 form views (desktop + mobile = 6 files) -->
<link rel="stylesheet" href="~/lib/cropperjs/cropper.min.css" />
<script src="~/lib/cropperjs/cropper.min.js"></script>
<script src="~/js/image-crop.js"></script>
```
Download `cropper.min.js` + `cropper.min.css` from the `v1` release assets (jsdelivr `cropperjs@1.6.2/dist/cropper.min.js` and `.../cropper.min.css`) once, commit them into `wwwroot/lib/cropperjs/`, exactly as Bootstrap/FontAwesome are referenced today.

## Scaling Considerations

| Scale | Architecture Adjustments |
|-------|---------------------------|
| Current (17 members, occasional profile edits) | Exactly as described above — no queueing, no async processing; synchronous crop+upload on the request thread is trivial at this volume. |
| Moderate growth (100s of members) | No change needed. Image upload is not a hot path; `byte[]` columns on a dedicated 1-row table remain fine. |
| Out of scope for this milestone | Image blob storage migration to external storage — already explicitly listed in PROJECT.md's "Out of Scope": "Image blob storage migration — performance acceptable at current scale." Do not let this phase creep into that. |

## Anti-Patterns

### Anti-Pattern 1: Re-introducing a server-side image-processing library to "properly" re-crop from coordinates

**What people do:** Send crop rectangle (x/y/w/h) to the server and use `SkiaSharp`/`ImageSharp`/`System.Drawing` to perform the actual crop server-side, treating the client-side crop UI as "just a preview."

**Why it's wrong:** This is precisely the approach that was paused in v1.0 because `SkiaSharp`'s native library availability on the `aspnet:10` Debian Bookworm deployment target was never verified (PROJECT.md Key Decisions: "Profile picture crop paused — SkiaSharp native lib availability on deployment host unverified"). Re-introducing any native-dependent image library reopens exactly the blocker this milestone should retire, and it couples business logic to platform-specific native binaries with no natural home in this project's strict layering.

**Do this instead:** Crop entirely client-side with canvas (`getCroppedCanvas().toBlob()`), and post the already-cropped pixels as ordinary `byte[]`. The server only ever validates and stores bytes it's handed — this is the whole point of Pattern 1 above.

### Anti-Pattern 2: AJAX/fetch-based crop-then-upload flow

**What people do:** Intercept the form's `submit` event with JS, build a `FormData` object manually, and `fetch()` it to the controller, bypassing the native `<form>` POST.

**Why it's wrong:** This is unnecessary complexity for this codebase. Every other form in the app (all 6 upload views) is a plain server-rendered `<form method="post">` with full-page redirect-after-post; introducing one AJAX-driven exception creates an inconsistent UX (no full-page navigation feedback, needs manual error-display JS, breaks the existing `asp-validation-summary`/`asp-validation-for` tag helpers which rely on a real postback) for zero added benefit — the `DataTransfer`-swap trick (Pattern 1) achieves an identical visual result using the existing plain-POST mechanism.

**Do this instead:** Swap the `<input type="file">`'s `FileList` via `DataTransfer` before the user submits the existing form normally, as shown in Pattern 1.

### Anti-Pattern 3: Storing the crop rectangle instead of the actual cropped pixels

**What people do:** Store `CropX`, `CropY`, `CropWidth`, `CropHeight` as new columns alongside the original image, and compute the "cropped" view on every read.

**Why it's wrong:** This reintroduces a server-side (or repeated client-side) re-crop operation on every page that shows the thumbnail, which is both slower and reopens the "does this need an image library" question this milestone is trying to avoid. It also doesn't match issue #78's explicit requirement — "both the original and cropped image are stored."

**Do this instead:** Store the already-rasterized cropped bytes directly (Recommended Schema Shape above), computed once client-side at upload time.

## Integration Points

### Internal Boundaries (files touched, by layer)

| File | Layer | Change |
|------|-------|--------|
| `QuestBoard.Repository/Entities/CharacterImageEntity.cs` | Repository | Add `OriginalImageData byte[]?` |
| `QuestBoard.Repository/Entities/DungeonMasterProfileImageEntity.cs` | Repository | Add `OriginalImageData byte[]?` |
| `QuestBoard.Repository/Migrations/{new}_AddOriginalImageColumn.cs` | Repository | New additive migration (nullable column, no backfill) |
| `QuestBoard.Repository/CharacterRepository.cs` | Repository | `UpdateProfileImageAsync` accepts `(byte[]? cropped, byte[]? original)`; add `GetCharacterOriginalPictureAsync` |
| `QuestBoard.Repository/DungeonMasterProfileRepository.cs` | Repository | `UpsertProfileImageAsync` accepts `(byte[]? cropped, byte[]? original)`; add `GetOriginalPictureAsync` |
| `QuestBoard.Repository/Automapper/EntityProfile.cs` | Repository | Map new `byte[]?` properties Entity ↔ DomainModel |
| `QuestBoard.Domain/Models/Character.cs` | Domain | Add `OriginalProfilePicture byte[]?` |
| `QuestBoard.Domain/Models/DungeonMasterProfile.cs` | Domain | Add `OriginalProfilePicture byte[]?` |
| `QuestBoard.Domain/Interfaces/ICharacterService.cs` | Domain | Add `GetCharacterOriginalPictureAsync`; widen picture-upsert signature |
| `QuestBoard.Domain/Interfaces/IDungeonMasterProfileService.cs` | Domain | Same shape of change |
| `QuestBoard.Domain/Interfaces/IImageValidationService.cs` (new, recommended) | Domain | Centralize size/type validation, remove 3x duplication |
| `QuestBoard.Domain/Services/CharacterService.cs` | Domain | Wire validation + widened upsert call |
| `QuestBoard.Domain/Services/DungeonMasterProfileService.cs` | Domain | Same |
| `QuestBoard.Service/ViewModels/CharacterViewModels/CharacterViewModel.cs` | Service | Add `IFormFile? OriginalPictureFile` |
| `QuestBoard.Service/ViewModels/DungeonMasterViewModels/EditDMProfileViewModel.cs` | Service | Add `IFormFile? OriginalPictureFile` |
| `QuestBoard.Service/Controllers/Characters/GuildMembersController.cs` | Service | `Create`/`Edit` POST actions read both files; new `GetOriginalPicture` action |
| `QuestBoard.Service/Controllers/DungeonMaster/DungeonMasterController.cs` | Service | `EditProfile` POST reads both files; new `GetOriginalPicture` action |
| `QuestBoard.Service/Views/GuildMembers/Create.cshtml` + `.Mobile.cshtml` | Service | Add crop modal markup, hidden original input, script includes |
| `QuestBoard.Service/Views/GuildMembers/Edit.cshtml` + `.Mobile.cshtml` | Service | Same |
| `QuestBoard.Service/Views/DungeonMaster/EditProfile.cshtml` + `.Mobile.cshtml` | Service | Same |
| `QuestBoard.Service/Views/GuildMembers/Details.cshtml` | Service | Switch `<img>` src to `GetOriginalPicture` per issue #78 |
| `QuestBoard.Service/wwwroot/lib/cropperjs/*` (new) | Service (static assets) | Vendored Cropper.js v1.6.2 files |
| `QuestBoard.Service/wwwroot/js/image-crop.js` (new) | Service (static assets) | Shared crop-modal wiring, used by all 3 forms |
| `QuestBoard.Service/wwwroot/css/image-crop.css` (new) | Service (static assets) | Crop modal styling |

### External Services

None. This feature has zero external service dependencies — it is entirely client-side JS + existing EF Core/SQL Server.

## Suggested Build Order

1. **Schema first (Repository layer):** Add `OriginalImageData` column to both image entities, generate and verify the EF Core migration applies cleanly (additive, nullable — low risk). Nothing else can be built/tested end-to-end until this exists, since controllers need somewhere to persist the second byte array.
2. **Repository + Domain plumbing:** Widen `UpdateProfileImageAsync`/`UpsertProfileImageAsync` signatures, add `GetCharacterOriginalPictureAsync`/`GetOriginalPictureAsync`, update `Character`/`DungeonMasterProfile` domain models and AutoMapper profiles, update `ICharacterService`/`IDungeonMasterProfileService` and their implementations. This can be built and unit-tested independently of any UI change (pass two `byte[]` literals in a test, assert both columns are set).
3. **ViewModel + controller wiring (still without crop UI):** Add `OriginalPictureFile` to both ViewModels, update the 3 POST actions to read and forward both files, add the two `GetOriginalPicture` actions. At this point the feature is fully functional via manual two-file-input testing (e.g. Postman) even before any JS crop UI exists — a good integration checkpoint.
4. **Client-side crop UI (Service layer, static assets):** Vendor Cropper.js v1.6.2, build `image-crop.js`, wire it into the 3 form views (desktop + mobile = 6 view files), add the hidden `OriginalPictureFile` input and crop-modal markup.
5. **Character details page original-image display:** Point the one view that must show the original (per issue #78) at the new `GetOriginalPicture` endpoint.
6. **Manual UAT across all 3 forms x desktop/mobile:** Confirm crop -> save -> thumbnail (cropped) shows correctly everywhere, and the character details page specifically shows the original.

This order front-loads the EF migration (step 1) because every later step depends on the column existing, and defers the riskiest/most novel piece (a brand-new client-side library integration, step 4) until the data path underneath it is already proven correct via direct testing — so if the crop UI has any integration issues, they're isolated to JS/markup, not conflated with schema or Domain logic bugs.

## Sources

- `QuestBoard.Repository/Entities/CharacterImageEntity.cs` — verified by direct read (HIGH)
- `QuestBoard.Repository/Entities/DungeonMasterProfileImageEntity.cs` — verified by direct read (HIGH)
- `QuestBoard.Repository/CharacterRepository.cs` — verified by direct read (HIGH)
- `QuestBoard.Repository/DungeonMasterProfileRepository.cs` — verified by direct read (HIGH)
- `QuestBoard.Service/Controllers/Characters/GuildMembersController.cs` — verified by direct read (HIGH)
- `QuestBoard.Service/Controllers/DungeonMaster/DungeonMasterController.cs` — verified by direct read (HIGH)
- `QuestBoard.Service/ViewModels/CharacterViewModels/CharacterViewModel.cs` — verified by direct read (HIGH)
- `QuestBoard.Service/Views/GuildMembers/Edit.cshtml` — verified by direct read (HIGH), including existing inline JS validation pattern to be consolidated
- `.planning/PROJECT.md` — verified by direct read (HIGH); source of the SkiaSharp-pause decision and issue #78 requirements
- `.planning/milestones/v1.0-phases/07-dm-profile-page/07-RESEARCH.md` — verified by direct read (HIGH); prior research establishing the `CharacterImageEntity` PK=FK pattern this milestone extends
- `.planning/codebase/ARCHITECTURE.md` — verified by direct read (HIGH); confirms strict Service→Domain→Repository layering and "EF packages only in Repository" rule
- [Cropper.js homepage/guide](https://fengyuanchen.github.io/cropperjs/) — WebFetch (MEDIUM, cross-verified against v1 docs page); confirms v2 uses a custom-elements API
- [Cropper.js v1 docs](https://fengyuanchen.github.io/cropperjs/v1/) — WebFetch (MEDIUM, cross-verified against homepage); confirms v1.6.2 retains the classic class API with `getCroppedCanvas()`
- [Cropper.js GitHub repo](https://github.com/fengyuanchen/cropperjs) — WebSearch (LOW individually, corroborates the above)
- General `canvas.toBlob()` + `IFormFile` crop-before-upload pattern — WebSearch results across Telerik/GemBox/community sources (LOW individually; pattern is well-established, cross-referenced against the `DataTransfer` FileList-replacement technique which is standard/documented browser API usage, not vendor-specific)

---
*Architecture research for: D&D Quest Board — v7.0 client-side image crop-before-save*
*Researched: 2026-07-04*
