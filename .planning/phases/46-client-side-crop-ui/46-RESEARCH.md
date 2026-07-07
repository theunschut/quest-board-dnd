# Phase 46: Client-Side Crop UI - Research

**Researched:** 2026-07-07
**Domain:** Vanilla-JS client-side image cropping (Cropper.js v2), EXIF/canvas-memory browser pitfalls, ASP.NET Core MVC dual-file form submission, no-bundler front-end asset delivery
**Confidence:** MEDIUM-HIGH (Cropper.js v2 API and native browser APIs verified against official docs/MDN/caniuse; codebase integration points verified by direct file reads; EXIF-orientation fix strategy is a genuinely new finding this session, verified against MDN + a corroborating GitHub issue, not carried over from project-level research)

## Summary

Phase 45 shipped every byte of backend plumbing this phase needs: `OriginalImageData`/`CroppedImageData` columns exist on all three image tables, `GetCharacterCroppedPictureAsync`/`GetContactCroppedImageAsync`/`GetCroppedPictureAsync` service methods already read them with the correct `Cropped ?? Original` fallback, and `IImageValidationService.ValidateImagePair` already accepts an optional cropped-file parameter. But direct inspection of `CharacterService.UpdateAsync`, `ContactService.UpdateAsync`, and `DungeonMasterProfileService.UpsertProfileAsync` found a real gap that neither CONTEXT.md nor prior research flagged explicitly: all three currently **hardcode `croppedImageData = null`** on new-original-upload and have no parameter through which a caller can pass a genuinely new cropped image. This is not a naming/discretion question — it is a required signature change to all three Domain service methods, in addition to the already-anticipated controller/ViewModel/view work. This finding is the single most important addition this research makes to the plan.

The second major finding corrects a load-bearing assumption in the project-level STACK.md: this codebase has **no `wwwroot/lib/` vendoring convention at all**. Bootstrap, FontAwesome, and jQuery are all loaded from public CDNs directly in `_Layout.cshtml`/`_Layout.Mobile.cshtml`/`_Layout.GroupPicker.cshtml` — there is no precedent for downloading and committing a third-party JS file into the repo. This phase should follow the codebase's actual convention (a pinned-version CDN `<script>` tag with a Subresource Integrity hash) rather than inventing a new `wwwroot/lib/` pattern that nothing else in the app uses.

Third, the EXIF-orientation problem has a materially better solution than the hand-rolled ~40-line APP1-segment parser that project-level PITFALLS.md anticipated. `createImageBitmap(source, { imageOrientation: 'from-image' })` is a native, dependency-free browser API (default behavior, Baseline widely available, full iOS Safari support since version 16) that bakes EXIF rotation into pixels with zero vendored code and zero parsing risk. Combined with a single downscaling canvas pass, this becomes one ~20-line pre-processing function with no third-party snippet to audit at all — a stronger answer to "source a specific, auditable approach" than adopting someone else's EXIF parser. A `<canvas>`-based fallback path for pre-2022 browsers is documented below for completeness, but this app's own prior phases (43, 45) already assume a modern-iPhone testing baseline, so the fallback is a defensive addition, not the primary path.

**Primary recommendation:** Vendor Cropper.js v2.1.1 via a pinned CDN `<script>` tag (matching existing Bootstrap/FontAwesome convention, not a new `wwwroot/lib/` folder), pre-process every selected file through a single `createImageBitmap({imageOrientation:'from-image'})` → downscale-to-≤2400px → canvas → `toBlob()` step before Cropper.js ever sees it (this fixes EXIF orientation and the iOS canvas-memory ceiling in one pass, with no vendored EXIF-parsing code), open Cropper.js in a Bootstrap modal on file-select with `aspectRatio` locked to 1, extract the crop via `$toCanvas()` + `canvas.toBlob()`, and submit both files by adding a second hidden `<input type="file">` per form (not a `DataTransfer` swap on the same input) so the ViewModel gains a distinct `CroppedPictureFile` property that binds cleanly via ASP.NET Core's existing `IFormFile` model binding.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Crop frame interaction (drag/resize/zoom) | Browser / Client | — | Cropper.js v2 Web Components render and handle all pointer/touch interaction entirely client-side; no server round-trip during cropping |
| EXIF orientation correction | Browser / Client | — | Must happen before the cropped Blob is produced; `createImageBitmap` runs in the browser, has no server-side equivalent needed (server never decodes images) |
| Canvas-memory-safe downscaling | Browser / Client | — | iOS Safari's canvas ceiling is a client-side runtime constraint; downscaling must happen before any canvas operation, entirely in JS |
| Dual-file form submission | Browser / Client | API / Backend | Client assembles the two-file multipart POST; server-side MVC model binding (API/Backend tier) receives both `IFormFile`s unchanged from today's single-file pattern |
| Original+cropped byte validation | API / Backend | — | `IImageValidationService` (Domain layer) already validates both files server-side; this phase calls it with a real second file instead of `cropped: null` |
| Original+cropped persistence | API / Backend | Database / Storage | Domain service methods (`CharacterService.UpdateAsync` etc.) orchestrate; `byte[]` columns on `CharacterImages`/`ContactImages`/`DungeonMasterProfileImages` (Database tier) already exist from Phase 45 |
| Cropped-vs-original endpoint routing | API / Backend | — | New controller actions (`GetCroppedPicture`-style) and repointed existing actions decide which column each view reads; pure server-side routing decision, no client logic |
| List-card square layout | Browser / Client | — | Pure CSS change (`.character-image`/`.contact-image` box shape); no JS or server involvement |

## User Constraints (from CONTEXT.md)

### Locked Decisions

- **D-01:** All three photo fields — Character, Contact, DM profile — crop to one locked **square (1:1)** aspect ratio in the Cropper.js widget. A square crop still displays as a circle anywhere `border-radius: 50%` is applied (confirmed against the existing DM `rounded-circle` pattern) — no functional conflict with DM's circular presentation.
- **D-02:** Because a square crop shown inside the current landscape list-card box (`.character-image`/`.contact-image` in `characters.css`/`contacts.css` — currently `width: 100%; height: 200px`, fluid width) would get re-cropped top/bottom by `object-fit: cover` to fill the wider box, undoing the user's square framing — the Character and Contact list-card image containers change from a landscape box to a **square aspect-ratio box**. DM's existing circle CSS (`rounded-circle` over a 128×128 box in `dm-profile.css`) needs no change.
- **D-03:** The **original** image displays ONLY on two pages: **Character Details** and **Contact Details** (desktop + mobile both). The **cropped** image displays literally everywhere else a photo appears, including: Characters index / Contacts index (list-card thumbnails), **DM Profile page** (desktop + mobile — supersedes REQUIREMENTS.md's literal IMAGE-04 text), Quest Details participant list (selected + waitlist), Quest Manage participant roster, QuestLog Details recap page, `_QuestCard` partial, and the Character/Contact Create+Edit form's own "current photo" preview thumbnail.
- **D-04:** Selecting a file opens a **modal** with the Cropper.js widget over the photo (not an inline-in-form widget). If the user submits without ever touching the crop frame, a **centered default crop at the locked 1:1 ratio auto-saves** — the modal never blocks form submission waiting for an explicit "confirm crop" click.

### Claude's Discretion

- Exact new controller-action/service-method names for the "read cropped" endpoints — should mirror `GetCharacterOriginalPictureAsync`-style naming already established in Phase 45.
- Whether DM's single existing `GetDMProfilePicture` action/`GetProfilePictureAsync` service method is simply repointed to read `CroppedImageData ?? OriginalImageData`, or whether a separate original-read method is also added for future-proofing. No UI currently needs a DM "original" endpoint given D-03.
- EXIF-orientation-correction snippet sourcing and the canvas-downscale-before-crop implementation — both resolved in this research (see Code Examples and Common Pitfalls below).
- Exact modal markup/styling (Bootstrap modal vs. a custom overlay) — this research recommends Bootstrap modal, since Bootstrap 5.3 is already loaded globally via CDN and the app has zero custom-overlay precedent.
- Cropped-output resolution (fixed pixel size vs. native resolution of the selected crop box) — this research recommends letting `$toCanvas()` produce the crop box's native pixel dimensions (up to the ~2400px downscale bound), since a fixed target size adds a resize step with no clear benefit for this app's scale.

### Deferred Ideas (OUT OF SCOPE)

None — discussion stayed within phase scope. Rotate/flip (IMAGE-06) and multiple aspect-ratio presets (IMAGE-07) are already documented v2 deferrals in REQUIREMENTS.md, not new ideas from this session.

## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| IMAGE-01 | User can interactively drag/resize/zoom a crop frame (Cropper.js v2.1.1) over an uploaded photo before saving | Cropper.js v2 API confirmed (CropperCanvas/CropperImage/CropperSelection Web Components, Pointer-Events-based interaction built in, `aspectRatio` lock, `$toCanvas()` extraction) — see Code Examples |
| IMAGE-04 | Cropped-vs-original display rule across the app (superseded/broadened by 46-CONTEXT.md D-03) | Full endpoint inventory below maps every view to its correct read endpoint per D-03, including the new cropped-read actions this phase must add |
| IMAGE-05 | Crop UI applies to every image-upload field in the app (character photo, DM profile photo, + Contact per Phase 45 D-01) | Full view inventory confirmed against actual `Views/Characters/`, `Views/Contacts/`, `Views/DungeonMaster/` files — matches 46-CONTEXT.md's code_context inventory with no gaps found |

## Standard Stack

### Core

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Cropper.js | 2.1.1 | Client-side drag/resize/zoom crop UI | `[VERIFIED: npm registry]` — `npm view cropperjs version` confirms `2.1.1` is current `dist-tags.latest`; matches the version already locked by prior project research and STATE.md. Vanilla JS, no framework dependency, ships as Web Components requiring no build step. |

### Supporting

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| (none — native browser APIs only) | — | EXIF orientation correction, downscaling | `createImageBitmap()` + `<canvas>` + `Blob` are all native browser APIs, zero dependency, zero vendored snippet needed |
| (none — no CSS file needed) | — | Cropper.js v2 styling | Unlike v1, Cropper.js v2's Web Components carry their own internal styling; no separate `cropper.css` to load (confirmed: `dist/` for v2.1.1 lists no CSS file, only `cropper.js`/`cropper.esm.js` and their minified/type-definition variants) |

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| CDN `<script>` tag | Committing `cropper.min.js` into a new `wwwroot/lib/cropperjs/` folder | Rejected: no precedent exists anywhere in this codebase for vendoring third-party JS into the repo — Bootstrap/FontAwesome/jQuery are all CDN-loaded. Introducing a new pattern for one library adds inconsistency without a clear benefit; a pinned-version CDN URL + Subresource Integrity hash gives the same auditability/reproducibility as a committed file. |
| `createImageBitmap({imageOrientation:'from-image'})` | Hand-rolled JPEG APP1/EXIF byte parser (as project-level PITFALLS.md anticipated) | Only fall back to a hand-rolled parser if a specific target browser lacks `createImageBitmap` orientation support (pre-Safari-16, pre-Chrome-81) — not expected to be needed given this app's real-device testing has used modern iPhones in prior phases (43, 45) |
| Second hidden `<input type="file">` for the cropped Blob | `DataTransfer` FileList-swap onto the *same* input the user picked the original with | Rejected as the primary mechanism: swapping the same input's contents would either lose the original file entirely (if replaced) or require the swap-back-and-forth juggling STACK.md described. A second field cleanly maps to a second `IFormFile` ViewModel property with no juggling, and DataTransfer-to-a-new-input is still needed to *populate* that second hidden input's `.files`, so the technique isn't discarded — just applied to a distinct field. |

**Installation:**
```html
<!-- In _Layout.cshtml or per-view, following the existing CDN <script>/<link> convention -->
<script src="https://cdn.jsdelivr.net/npm/cropperjs@2.1.1/dist/cropper.min.js"
        integrity="sha384-REPLACE_WITH_REAL_HASH_AT_IMPLEMENTATION_TIME"
        crossorigin="anonymous"></script>
```

**Version verification:** `npm view cropperjs version` returned `2.1.1` at research time (2026-07-07), matching `dist-tags.latest`. `npm view cropperjs versions --json` confirms the full release history: v1 series ended at `1.6.2`; v2 series is `2.0.0` → `2.0.1` → `2.1.0` → `2.1.1`, an actively maintained line. `unpkg`/`jsDelivr` both serve `dist/cropper.js` (UMD, 132 kB) and `dist/cropper.min.js` (42.2 kB, minified) as plain global-registering scripts — confirmed reachable (`HTTP/1.1 200 OK` from `unpkg.com/cropperjs@2.1.1/dist/cropper.min.js`).

## Package Legitimacy Audit

| Package | Registry | Age | Downloads | Source Repo | slopcheck | Disposition |
|---------|----------|-----|-----------|-------------|-----------|-------------|
| cropperjs | npm | v2 line since 2025-03-01 (`2.1.1` released 2026-04-06, ~3 months old at research time); overall package first published 2015 | Not independently re-measured this session; STACK.md previously recorded "961+ dependent npm packages" as a popularity signal | `github.com/fengyuanchen/cropperjs` (confirmed via `npm view cropperjs repository.url`) | Not applicable — see note below | Approved |

**slopcheck note:** `slopcheck install cropperjs` was run and returned a `[SLOP]` verdict, but this is a tooling-ecosystem mismatch, not a real finding: slopcheck has no npm/JS mode and unconditionally checks every package name against **PyPI** (`Package 'cropperjs' does not exist on pypi`). Cropper.js is a JavaScript package that has never been and was never intended to be on PyPI — the `[SLOP]` verdict is a false positive produced by checking the wrong registry, not evidence of hallucination. This is exactly the "cross-ecosystem confusion" failure mode the package-legitimacy protocol warns about, except inverted: here the *tool* checked the wrong ecosystem, not the *research*. The ecosystem-correct verification is the direct `npm view cropperjs version`/`npm view cropperjs repository.url` check performed above (Standard Stack section), which is authoritative for this npm-ecosystem package and confirms: real package, real GitHub source, active maintenance, version matches what's being recommended. No postinstall-script check applies since this package is not installed via `npm install` in this project (no `package.json` exists; the file is loaded via CDN `<script>` tag) — there is no install-time script execution surface at all.

**Packages removed due to slopcheck [SLOP] verdict:** none (the one `[SLOP]` result was a cross-registry false positive, explained above, not a genuine hallucination finding — cropperjs is retained)
**Packages flagged as suspicious [SUS]:** none

## Architecture Patterns

### System Architecture Diagram

```
User selects file in <input type="file" id="...OriginalInput">
        |
        v
[change event handler in view's @section Scripts block]
        |
        v
1. createImageBitmap(file, {imageOrientation: 'from-image'})  --> EXIF-correct pixels
        |
        v
2. Draw to a scratch <canvas>, scaled so max(width,height) <= ~2400px
        |
        v
3. canvas.toBlob() --> corrected-and-downscaled Blob
        |
        v
4. Create object URL from Blob, set as <cropper-image src="...">
        |
        v
5. Open Bootstrap modal containing <cropper-canvas>/<cropper-image>/<cropper-selection aspect-ratio="1">
        |
        +--- User drags/resizes/zooms (Cropper.js Pointer-Events handling, built in) ---+
        |                                                                                |
        v                                                                                v
   [user clicks "Use Photo" / never touches frame + clicks form Submit]         [default centered 1:1 crop applies automatically, per D-04]
        |
        v
6. cropperSelection.$toCanvas() --> Promise<HTMLCanvasElement>
        |
        v
7. canvas.toBlob() --> cropped-result Blob
        |
        v
8. Wrap Blob in a File; new DataTransfer(); dt.items.add(file); hiddenCroppedInput.files = dt.files
        |
        v
9. Existing <form method="post" enctype="multipart/form-data"> submits normally
        |         (both OriginalPictureFile input AND new hidden CroppedPictureFile input travel in the same POST)
        v
[ASP.NET Core MVC model binding: CharacterViewModel.ProfilePictureFile + CharacterViewModel.CroppedPictureFile]
        |
        v
[Controller: IImageValidationService.ValidateImagePair(original, cropped) -- both files now real]
        |
        v
[Domain service: UpdateAsync(model, hasNewOriginalUpload, croppedImageBytes, token) -- NEW parameter]
        |
        v
[Repository: UpdateWithProfileImageAsync(model, originalBytes, croppedBytes, token) -- already exists from Phase 45]
        |
        v
[Database: OriginalImageData / CroppedImageData columns -- already exist from Phase 45]
```

### Recommended Project Structure

No new folders needed — this codebase has no per-feature JS/CSS folder convention; it uses one shared `site.js`, per-view `@section Scripts` blocks, and per-view/per-feature CSS files (`characters.css`, `dm-profile.css`, etc.).

```
QuestBoard.Service/
├── wwwroot/
│   ├── js/
│   │   ├── site.js                    # unchanged — global helpers only
│   │   └── image-crop.js              # NEW — shared crop-modal logic (EXIF fix, downscale,
│   │                                  #   Cropper.js init, $toCanvas extraction, dual-file wiring)
│   │                                  #   reused across all 6 upload views via a plain <script> include
│   └── css/
│       ├── characters.css             # MODIFIED — .character-image box: 200px landscape -> square (D-02)
│       ├── contacts.css               # MODIFIED — .contact-image box: 200px landscape -> square (D-02)
│       └── image-crop.css             # NEW — crop modal layout only (Cropper.js v2 needs no base stylesheet)
├── Views/
│   ├── Characters/{Create,Edit}.cshtml + .Mobile variants   # MODIFIED — add crop modal markup + CroppedPictureFile hidden input
│   ├── Contacts/{Create,Edit}.cshtml + .Mobile variants     # MODIFIED — same
│   ├── DungeonMaster/EditProfile.cshtml + .Mobile variant   # MODIFIED — same
│   └── (11 other views)                                     # MODIFIED — endpoint repoint only, no crop UI
```

### Pattern 1: Shared crop-modal script, included per view (not merged into site.js)

**What:** A single `wwwroot/js/image-crop.js` exposes one initializer function (e.g. `initImageCrop({ fileInputId, hiddenCroppedInputName, aspectRatio })`) that each of the 6 upload views calls from their own `@section Scripts` block, passing their specific element IDs.
**When to use:** Any of the 3 Create views + 3 Edit views (desktop and mobile variants share the same script file, called with the same IDs since the mobile views reuse the same `id` attributes per the existing pattern seen in `Edit.cshtml`/`Edit.Mobile.cshtml` both using `profilePictureInput`).
**Why not merge into site.js:** PITFALLS.md's own Integration Gotchas table already flags this exact risk — `site.js` centralizes only cross-cutting helpers (masonry layout, toast init, datetime helpers); a large feature-specific script belongs in its own file, loaded only on the 6 pages that need it, exactly like this project already keeps `site.js` as the only global file and puts everything else inline per-view.
**Example:**
```html
<!-- In Views/Characters/Edit.cshtml, replacing the existing inline size/type-check script -->
@section Scripts {
    <script src="~/js/image-crop.js"></script>
    <script>
        initImageCrop({
            fileInputId: 'profilePictureInput',
            hiddenCroppedInputName: 'CroppedPictureFile',
            aspectRatio: 1
        });
    </script>
}
```

### Pattern 2: EXIF-correct, size-bounded pre-processing before Cropper.js ever initializes

**What:** Intercept the file input's `change` event; before creating any Cropper.js instance, decode the file through `createImageBitmap` with orientation correction, draw to a downscaled canvas, and only then hand a corrected Blob URL to `cropper-image`.
**When to use:** Every file selection, unconditionally — this is not an edge-case path, it is the only path, since a corrected-and-downscaled copy is strictly safe to feed to Cropper.js even for small/already-correct images (no-op resize, no-op rotation).
**Example:**
```javascript
// Source: MDN createImageBitmap() docs (imageOrientation option, Baseline/iOS Safari 16+)
//         + MDN Canvas API (toBlob) -- both native browser APIs, no third-party snippet
async function prepareImageForCropper(file, maxDimension = 2400) {
    // imageOrientation: 'from-image' is a browser default per the CSS `image-orientation`
    // spec, but is set explicitly here because Cropper.js's own internal canvas rendering
    // has a documented interaction bug when the page's CSS sets `image-orientation: none`
    // (fengyuanchen/cropperjs#685) -- baking the correct pixels in ourselves, before Cropper.js
    // ever sees the file, sidesteps that class of bug entirely rather than depending on
    // Cropper.js's own EXIF handling.
    const bitmap = await createImageBitmap(file, { imageOrientation: 'from-image' });

    let { width, height } = bitmap;
    const longEdge = Math.max(width, height);
    if (longEdge > maxDimension) {
        const scale = maxDimension / longEdge;
        width = Math.round(width * scale);
        height = Math.round(height * scale);
    }

    const canvas = document.createElement('canvas');
    canvas.width = width;
    canvas.height = height;
    const ctx = canvas.getContext('2d');
    ctx.drawImage(bitmap, 0, 0, width, height);
    bitmap.close(); // free the ImageBitmap's backing memory promptly (WebKit holds canvas
                     // memory longer than expected -- see Pitfall 2 below)

    return new Promise(resolve => {
        canvas.toBlob(blob => {
            canvas.width = 0; canvas.height = 0; // release this canvas's memory too
            resolve(blob);
        }, file.type === 'image/png' ? 'image/png' : 'image/jpeg', 0.92);
    });
}
```

### Pattern 3: Cropper.js v2 markup + 1:1 lock + extraction

**What:** The Web-Components markup Cropper.js v2 requires, with `aspectRatio` locked to `1`, inside a Bootstrap modal.
**When to use:** Inside the crop modal body, initialized fresh each time the modal opens (destroy/recreate rather than reuse across selections, to avoid stale-image state).
**Example:**
```html
<!-- Source: https://fengyuanchen.github.io/cropperjs/v2/guide.html + v2 API reference pages
     (cropper-canvas, cropper-image, cropper-selection, cropper-grid, cropper-handle) -->
<cropper-canvas background style="width:100%;height:400px;">
    <cropper-image id="cropperImageEl" alt="Photo to crop" rotatable scalable translatable></cropper-image>
    <cropper-shade hidden></cropper-shade>
    <cropper-handle action="select" plain></cropper-handle>
    <cropper-selection id="cropperSelectionEl" initial-coverage="0.8" movable resizable aspect-ratio="1">
        <cropper-grid role="grid" bordered covered></cropper-grid>
        <cropper-crosshair centered></cropper-crosshair>
        <cropper-handle action="move" theme-color="rgba(255,255,255,0.35)"></cropper-handle>
        <cropper-handle action="n-resize"></cropper-handle>
        <cropper-handle action="e-resize"></cropper-handle>
        <cropper-handle action="s-resize"></cropper-handle>
        <cropper-handle action="w-resize"></cropper-handle>
        <cropper-handle action="ne-resize"></cropper-handle>
        <cropper-handle action="nw-resize"></cropper-handle>
        <cropper-handle action="se-resize"></cropper-handle>
        <cropper-handle action="sw-resize"></cropper-handle>
    </cropper-selection>
</cropper-canvas>
```
```javascript
// Source: https://github.com/fengyuanchen/cropperjs/discussions/1264 ($toCanvas replaces v1's
// getCroppedCanvas()) + https://fengyuanchen.github.io/cropperjs/v2/api/cropper-selection.html
async function extractCroppedBlob(selectionElement) {
    const canvas = await selectionElement.$toCanvas(); // returns Promise<HTMLCanvasElement>
    return new Promise(resolve => {
        canvas.toBlob(blob => resolve(blob), 'image/jpeg', 0.9);
    });
}
```

### Pattern 4: Second hidden file input for the cropped Blob (dual-file submission)

**What:** A hidden `<input type="file" name="CroppedPictureFile" style="display:none">` per form, populated via the `DataTransfer` trick, submitted alongside the user-visible original-photo input in the same ordinary POST.
**When to use:** After the user closes the crop modal (either by explicit confirm, or by submitting the form with an untouched default crop per D-04) — populate this hidden input immediately so the value survives even if the user submits the form without any further modal interaction.
**Example:**
```javascript
// Source: https://pqina.nl/blog/set-value-to-file-input/ (DataTransfer constructor pattern,
// Safari 14.1+, confirmed working on iOS Safari without the desktop-Safari filename-display quirk)
function setCroppedFileInput(hiddenInputEl, blob, originalFileName) {
    const croppedFile = new File([blob], `cropped-${originalFileName}`, {
        type: blob.type,
        lastModified: Date.now()
    });
    const dt = new DataTransfer();
    dt.items.add(croppedFile);
    hiddenInputEl.files = dt.files; // assigning a *new* FileList this way is the supported
                                    // workaround for input.files being otherwise read-only
}
```
```html
<!-- Add to each of the 6 upload views, alongside the existing visible file input -->
<input type="file" name="CroppedPictureFile" id="croppedPictureFileInput" style="display:none" />
```

### Anti-Patterns to Avoid

- **Relying on Cropper.js's own EXIF handling instead of pre-processing:** A documented Cropper.js v2 issue (`fengyuanchen/cropperjs#685`) shows orientation/sizing bugs specifically tied to how the library's internal canvas interacts with the page's `image-orientation` CSS setting. Pre-correcting via `createImageBitmap` before Cropper.js ever sees the file avoids depending on library-internal EXIF behavior at all.
- **Using `DataTransfer` to swap the *same* input's contents:** Overwrites the user's original file selection with the cropped Blob, defeating the "both original and cropped submit" requirement (success criterion #2). Always use a second, distinct input for the cropped Blob.
- **Creating a new `<canvas>` element per crop/preview/output step:** PITFALLS.md's Pitfall 3 warns this compounds toward iOS Safari's combined canvas memory ceiling faster than any single canvas looks oversized. Reuse one scratch canvas (reset `width=0;height=0` between uses) for the pre-processing step, separate from whatever internal canvas Cropper.js's own Web Components manage.
- **Vendoring a full hand-rolled EXIF parser "to be safe":** Given `createImageBitmap({imageOrientation:'from-image'})` is a native, zero-dependency, Baseline-available API with confirmed iOS Safari 16+ support, adding ~40 lines of hand-rolled/borrowed JPEG-parsing code is *more* audit surface, not less — prefer the native API as primary; only add a manual-parse fallback if a real compatibility gap is found in the target Safari versions during implementation.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| EXIF orientation reading/correction | A JPEG APP1-segment byte parser (exif-js/blueimp-load-image-style snippet) | `createImageBitmap(file, {imageOrientation:'from-image'})` | Native browser API, zero vendored code, zero parsing-bug surface; confirmed Baseline-available with full iOS Safari support since v16 |
| Drag/resize/zoom crop-frame interaction | Custom pointer-event math for crop handles | Cropper.js v2's built-in `cropper-selection`/`cropper-handle` elements | Already uses Pointer Events internally with mouse/touch fallback (confirmed via source-level web search); reimplementing this is a multi-day effort the library has already solved |
| Cropped-image extraction to a file-uploadable format | Manual pixel-buffer-to-file encoding | `cropperSelection.$toCanvas()` + native `canvas.toBlob()` | `$toCanvas()` is Cropper.js v2's documented replacement for v1's `getCroppedCanvas()`; `toBlob()` is a standard Canvas API method with universal support |
| Populating a file input programmatically | Manually constructing a fake `FileList`-like object | `new DataTransfer(); dt.items.add(file); input.files = dt.files;` | `FileList` has no public constructor; this is the only standards-based way to set `.files` on an input outside of a native user file-picker interaction |

**Key insight:** Every piece of this phase's genuinely novel client-side work (EXIF correction, crop interaction, file-input population) already has a native-browser or library-provided solution verified this session — there is no part of the crop pipeline that legitimately needs hand-rolled parsing/interaction code.

## Common Pitfalls

### Pitfall 1: EXIF orientation ignored by canvas cropping — now with a native fix, but still must be applied unconditionally

**What goes wrong:** `drawImage()` does not read EXIF orientation; a portrait iPhone photo can be cropped sideways/upside-down with no visible error during desktop testing.
**Why it happens:** Desktop test images rarely carry orientation EXIF; the bug only manifests with real phone-camera photos.
**How to avoid:** Run every selected file through `createImageBitmap(file, {imageOrientation:'from-image'})` before any Cropper.js initialization (see Code Examples Pattern 2) — this is a mandatory, unconditional step for every file selection, not a conditional check.
**Warning signs:** Any code path where a `File`/`Blob` is passed directly to `cropper-image`'s `src` (via `URL.createObjectURL(file)`) without first passing through the `createImageBitmap` pre-processing step.

### Pitfall 2: iOS Safari's canvas memory/pixel-area ceiling (16.7M pixels) crashes or blanks the crop UI

**What goes wrong:** A 12MP+ camera photo (4032×3024 = ~12.2M pixels) approaches the ceiling on its own; multiple simultaneous canvases (pre-processing scratch canvas + Cropper.js's internal rendering canvas + a live preview) can exceed it in combination even when no single canvas looks oversized.
**Why it happens:** WebKit's iOS-specific canvas backend enforces this ceiling; desktop Chrome/emulation never does, making the bug invisible until real-device testing.
**How to avoid:** The downscale-to-≤2400px step in Pattern 2 above must run before Cropper.js ever initializes against the image — Cropper.js's own internal `cropper-image` then only ever handles an already-small image, never the raw 12MP original. Explicitly `bitmap.close()` and reset scratch-canvas dimensions to `0` after use (shown in Pattern 2) rather than relying on garbage collection, since WebKit is documented to hold canvas memory longer than expected.
**Warning signs:** No downscale step exists between file-selection and Cropper.js initialization; manual testing only used pre-resized sample images.
**Phase to address:** This phase, verified specifically with a real, non-downscaled-for-testing 12MP+ iPhone camera photo — real-device verification access is flagged as unconfirmed per CONTEXT.md's Pre-Execution Blocker; do not mark this pitfall's verification complete until that access is confirmed.

### Pitfall 3: Touch-drag crop handles — already solved by Cropper.js v2's own event handling, but verify the app's own overlay CSS doesn't break it

**What goes wrong:** A custom crop implementation built with mouse-only events breaks on real touchscreens; Cropper.js v2 avoids this internally (confirmed: uses `pointerdown`/`pointerup`/`pointermove` when available, falling back to touch/mouse events).
**Why it happens:** This is the library's own internal concern, already solved — but a wrapping modal or overlay with its own `touch-action`/`overflow` CSS could still interfere with the crop frame's drag/pinch gestures.
**How to avoid:** Do not add custom `touchstart`/`touchmove` handlers competing with Cropper.js's own; if the modal body scrolls, apply `touch-action: none` to the `cropper-canvas` element specifically (not necessarily the whole modal) so page/modal scroll doesn't fight the crop-frame drag.
**Phase to address:** This phase — verify on a real touchscreen device once access is confirmed (see Pre-Execution Blocker).

### Pitfall 4: Domain service methods currently have no parameter for a caller-supplied cropped image (new finding, not previously documented)

**What goes wrong:** `CharacterService.UpdateAsync(model, hasNewOriginalUpload, token)`, `ContactService.UpdateAsync(model, hasNewOriginalUpload, token)`, and `DungeonMasterProfileService.UpsertProfileAsync(userId, bio, imageBytes, removeImage, token)` all currently hardcode `croppedImageData = null` whenever `hasNewOriginalUpload` is true (or unconditionally pass `croppedImageData: null` in the DM case) — none of the three has a parameter through which a genuinely new cropped Blob's bytes can be threaded through to the repository. If a plan assumes only the controller/ViewModel/view layers need changes (as the CONTEXT.md's "Claude's Discretion" framing might suggest), the new cropped bytes will silently have nowhere to go.
**Why it happens:** Phase 45 built these methods to solve exactly one problem — clearing a stale crop when a new original arrives — because Phase 45 explicitly shipped zero UI/crop capability (D-04 of 45-CONTEXT.md). The "accept a new crop" half of the contract was correctly deferred to this phase, but the deferral is a signature gap, not just a missing caller.
**How to avoid:** All three Domain service method signatures need a new parameter (e.g. `byte[]? newCroppedImageData`) alongside the existing `hasNewOriginalUpload`. Suggested shape: `UpdateAsync(model, hasNewOriginalUpload, newCroppedImageData, token)` — when `newCroppedImageData` is non-null, persist it; when null and `hasNewOriginalUpload` is true, fall back to today's clear-to-null behavior (a new original with no accompanying crop, e.g. an API caller that never went through the crop UI); when null and `hasNewOriginalUpload` is false, preserve today's existing-crop-fetch-and-passthrough behavior unchanged. This is additive to the existing method signatures (interface change), not a new method, since the existing overloads (`UpdateAsync(model, token)` two-arg) should still resolve safely for any other call site.
**Warning signs:** A plan that only touches `CharactersController`/`ContactsController`/`DungeonMasterController` and the three ViewModels, with no corresponding change listed for `ICharacterService`/`IContactService`/`IDungeonMasterProfileService` and their implementations.
**Phase to address:** This phase, as an explicit task — likely the first task in the controller/service wiring wave, since the view-layer crop UI has nothing to submit to without this signature change existing first.

### Pitfall 5: Mobile view parity — 6 desktop views + 6 mobile views all need the crop modal independently

**What goes wrong:** The crop modal markup/script is added to `Create.cshtml`/`Edit.cshtml` but the corresponding `.Mobile.cshtml` variant is missed.
**Why it happens:** This project maintains genuinely separate `.Mobile.cshtml` files (confirmed via `Views/Characters/`, `Views/Contacts/`, `Views/DungeonMaster/` directory listings — each Create/Edit has a distinct `.Mobile.cshtml` sibling), not one responsive stylesheet; a JS/markup change made only to the desktop file silently never reaches mobile users.
**How to avoid:** Confirmed full inventory of views needing the crop-modal UI itself: `Characters/Create.cshtml`, `Characters/Create.Mobile.cshtml`, `Characters/Edit.cshtml`, `Characters/Edit.Mobile.cshtml`, `Contacts/Create.cshtml`, `Contacts/Create.Mobile.cshtml`, `Contacts/Edit.cshtml`, `Contacts/Edit.Mobile.cshtml`, `DungeonMaster/EditProfile.cshtml`, `DungeonMaster/EditProfile.Mobile.cshtml` — exactly 10 views, matching 46-CONTEXT.md's code_context inventory exactly (confirmed by direct `Glob` of all three Views subdirectories; no additional or missing files found).
**Phase to address:** This phase — verify by grepping for the shared `image-crop.js` include across all 10 files before considering the phase done.

## Runtime State Inventory

Not applicable — this is a greenfield feature-addition phase (new UI wired to already-existing backend columns/methods from Phase 45), not a rename/refactor/migration phase. No stored data, live service config, OS-registered state, secrets, or build artifacts carry any string/identity that this phase renames or moves.

## Code Examples

See Architecture Patterns section above (Patterns 1-4) for the full verified pipeline: pre-processing (EXIF + downscale), Cropper.js v2 markup, extraction, and dual-file submission — each snippet is sourced from official docs/MDN/verified GitHub discussions, not training-data recall.

### Server-side: new cropped-read controller action pattern (mirrors existing `GetProfilePicture`)

```csharp
// Source: existing QuestBoard.Service/Controllers/Characters/CharactersController.cs:363-378
// pattern, extended with the already-existing GetCharacterCroppedPictureAsync service method
// from Phase 45 (QuestBoard.Domain/Interfaces/ICharacterService.cs:45)
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

### Server-side: Domain service signature widening (addresses Pitfall 4 above)

```csharp
// QuestBoard.Domain/Interfaces/ICharacterService.cs -- ADD this overload (existing 3-arg
// overload from Phase 45 stays for any caller that doesn't yet supply a new crop)
Task UpdateAsync(Character model, bool hasNewOriginalUpload, byte[]? newCroppedImageData, CancellationToken token = default);
```
```csharp
// QuestBoard.Domain/Services/CharacterService.cs -- new 4-arg implementation
public async Task UpdateAsync(Character model, bool hasNewOriginalUpload, byte[]? newCroppedImageData, CancellationToken token = default)
{
    byte[]? croppedImageData;
    if (newCroppedImageData != null)
    {
        // A genuinely new crop arrived this request (the normal path once the crop UI ships)
        croppedImageData = newCroppedImageData;
    }
    else if (hasNewOriginalUpload)
    {
        // New original with no accompanying crop (e.g. a caller that bypassed the crop UI) --
        // clear the stale crop of the now-superseded original, same as today's behavior.
        croppedImageData = null;
    }
    else
    {
        // No new file at all -- preserve the existing stored crop unchanged.
        croppedImageData = await repository.GetCharacterCroppedPictureAsync(model.Id, token);
    }

    await repository.UpdateWithProfileImageAsync(model, model.ProfilePicture, croppedImageData, token);
}

// Existing 3-arg overload delegates, preserving today's behavior for any caller not yet updated
public Task UpdateAsync(Character model, bool hasNewOriginalUpload, CancellationToken token = default) =>
    UpdateAsync(model, hasNewOriginalUpload, newCroppedImageData: null, token);
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Cropper.js v1 (`getCroppedCanvas()`, jQuery-era `<img>` + absolute-positioned-div implementation) | Cropper.js v2 (Web Components, `$toCanvas()`) | v2.0.0 released 2025-03-01; v1's last commit was 2025-03-08 (over a year stale as of this research) | v2 is the only actively maintained line; the API surface changed (`getCroppedCanvas()` -> `$toCanvas()`, returns a Promise not a synchronous canvas) |
| Hand-rolled/vendored EXIF-orientation JS parsers (exif-js, blueimp-load-image) | Native `createImageBitmap(source, {imageOrientation:'from-image'})` | Baseline "widely available" per MDN, iOS Safari full support from v16 (caniuse: `mdn-api_createimagebitmap_options_imageorientation_parameter_from-image`) | Removes the need to vendor and audit third-party EXIF-parsing code entirely for any project not needing to support pre-2022 Safari |

**Deprecated/outdated:**
- Cropper.js v1.6.x: last release 2024-04-21, branch inactive since 2025-03-08 — do not use for new integrations.
- Hand-rolled EXIF byte-parsing as the *only* option: still valid as a defensive fallback for very old browsers, but no longer the state-of-the-art primary approach given native `createImageBitmap` support.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | Cropper.js v2's internal Web Components (`cropper-image`) correctly render a `src` set from a `Blob`/object-URL exactly like a normal `<img src>` — no special handling needed | Architecture Patterns, Code Examples | LOW — object URLs are treated identically to regular URLs by all evergreen browsers per standard `URL.createObjectURL` semantics; if wrong, the crop preview would visibly fail to load during implementation, an easily-caught bug, not a silent one |
| A2 | `createImageBitmap`'s `imageOrientation: 'from-image'` support on the specific iOS Safari version(s) available for this project's real-device verification is actually ≥16 (not an older cached/enterprise-managed iOS version) | Common Pitfalls (Pitfall 1), Standard Stack | MEDIUM — if the verification device runs iOS 15 or older, EXIF correction would silently fail (image displays uncorrected); must be checked as part of device-access confirmation, not assumed. A vendored fallback parser should be kept as a documented contingency, not written speculatively, unless this check fails. |
| A3 | A pinned-version CDN `<script>` tag with Subresource Integrity is an acceptable substitute for STACK.md's original "vendor into `wwwroot/lib/`" recommendation, given no such folder/convention exists in this codebase today | Standard Stack, Alternatives Considered | LOW — this is a judgment call about matching existing codebase convention (CDN-only, confirmed by direct inspection of `_Layout.cshtml`) rather than introducing a new one; if the team prefers vendoring anyway for offline-resilience reasons, it's a straightforward alternative with no architectural blocker either way |

## Open Questions (RESOLVED)

1. **Exact Cropper.js v2 CSS custom-property/theming needed to match this app's dark fantasy visual style inside the modal**
   - What we know: Cropper.js v2's Web Components ship with sensible default styling and expose `theme-color` attributes on `cropper-handle`/`cropper-grid` elements for basic customization.
   - What's unclear: Whether default styling looks acceptable against this app's existing dark-themed `modern-card` design language, or needs additional CSS overrides.
   - **Q1 RESOLVED:** treated as implementation-time visual-polish (CSS) detail, not a planning blocker — the `image-crop.css` file already scoped in the Recommended Project Structure is the right place to iterate on this. No plan structure or task ordering depends on the answer.

2. **Whether the real-device verification access gap (flagged as a Pre-Execution Blocker in 46-CONTEXT.md) will be resolved before this phase's implementation waves begin**
   - What we know: CONTEXT.md explicitly flags this as NOT YET CONFIRMED and instructs downstream agents not to schedule the verification checkpoint until resolved.
   - What's unclear: Timeline for confirming device access; whether it's the same iPhone from Phase 43, a different device, or a real-device-cloud service.
   - **Q2 RESOLVED:** gated behind Plan 07 Task 1's blocking device-access decision checkpoint, matching CONTEXT.md's Pre-Execution Blocker. This research treats all device-dependent pitfalls (EXIF, canvas memory, touch-drag) as code-complete-but-unverified; the phase is structured so all non-device-dependent work (backend signature changes, endpoint additions, CSS, crop-UI wiring) completes and is reviewed independently of the verification checkpoint, which is its own late-sequenced blocking task/checkpoint per CONTEXT.md's explicit instruction. No planning ambiguity remains — the Plan 07 gate handles the timing.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET SDK | Build/test the Domain/Service signature changes | ✓ | 10.0.301 | — |
| Node.js (dev-machine only, for `npm view` verification, not a runtime dependency of the app) | Verifying `cropperjs` package metadata during research | ✓ | v22.16.0 | — |
| Cropper.js v2.1.1 (CDN: jsDelivr/unpkg) | Crop UI itself | ✓ (confirmed reachable, `HTTP 200`) | 2.1.1 | If the CDN becomes unreachable in production, fall back to downloading `dist/cropper.min.js` and serving it from this app's own `wwwroot/js/` instead — no code change needed beyond the `<script src>` URL, since it's a plain UMD global script either way |
| Real iOS device (or real-device-cloud service) for EXIF/canvas-memory/touch-drag verification | Success Criterion #4 (device-only checks) | ✗ (NOT YET CONFIRMED per 46-CONTEXT.md Pre-Execution Blocker) | — | None documented yet — this is the one dependency in this phase with no code-level fallback; it blocks only the verification checkpoint, not the implementation work itself |

**Missing dependencies with no fallback:**
- Real iOS device / device-cloud access for the mandatory real-device verification step (EXIF orientation from an actual phone photo, iOS Safari canvas-memory ceiling, touch-drag/pinch precision). Per CONTEXT.md, do not schedule this checkpoint until access is explicitly confirmed with the user.

**Missing dependencies with fallback:**
- None beyond the CDN-vs-self-hosted fallback noted above, which is not currently missing (CDN confirmed reachable) — listed only as a documented contingency.

## Validation Architecture

### Test Framework

| Property | Value |
|----------|-------|
| Framework | xUnit v3 (`xunit.v3` 3.2.2, `xunit.runner.visualstudio` 3.1.5), confirmed via `QuestBoard.UnitTests.csproj` |
| Config file | Standard `dotnet test` — no custom xunit.runner.json found; project-level `.csproj` settings only |
| Quick run command | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~ImageValidationService\|FullyQualifiedName~CharacterService\|FullyQualifiedName~ContactService\|FullyQualifiedName~DungeonMasterProfileService"` |
| Full suite command | `dotnet test` (runs both `QuestBoard.UnitTests` and `QuestBoard.IntegrationTests`) |

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| IMAGE-01 (crop-frame interaction) | Client-side drag/resize/zoom exists and produces a cropped Blob | manual-only (real-device/browser interaction — no automated DOM/canvas test exists in this codebase's stack) | N/A — manual verification per D-04's modal flow | N/A |
| IMAGE-01 / signature gap (Pitfall 4) | `CharacterService.UpdateAsync(model, hasNewOriginalUpload, newCroppedImageData, token)` persists a genuinely new crop | unit + integration | `dotnet test QuestBoard.UnitTests --filter FullyQualifiedName~CharacterServiceTests` / `dotnet test QuestBoard.IntegrationTests --filter FullyQualifiedName~CharactersControllerIntegrationTests` | ❌ Wave 0 — new test cases needed, following the exact `Edit_NewOriginalPhotoUpload_ClearsStaleCroppedImage`-style pattern already in `CharactersControllerIntegrationTests.cs:510` |
| IMAGE-04/D-03 (cropped-read endpoints) | New `GetCroppedPicture`/`GetCroppedContactImage`-style actions return `CroppedImageData ?? OriginalImageData` | integration | `dotnet test QuestBoard.IntegrationTests --filter FullyQualifiedName~CharactersControllerIntegrationTests\|FullyQualifiedName~ContactsControllerIntegrationTests` | ❌ Wave 0 — no existing test hits these not-yet-created actions |
| IMAGE-04/D-03 (DM repoint) | `GetDMProfilePicture` returns cropped-or-fallback bytes after repoint | integration | `dotnet test QuestBoard.IntegrationTests --filter FullyQualifiedName~DungeonMasterControllerIntegrationTests` | Check at plan time — confirm whether a `DungeonMasterControllerIntegrationTests.cs` file exists yet |
| IMAGE-05 (validation wiring) | `ValidateImagePair` is called with a real (non-null) cropped `ImageFileInput` once the crop UI submits a genuine cropped file | unit | `dotnet test QuestBoard.UnitTests --filter FullyQualifiedName~ImageValidationServiceTests` | ✅ (`QuestBoard.UnitTests/Services/ImageValidationServiceTests.cs` exists from Phase 45; extend, don't replace) |
| Success Criterion #4 (real-device checks) | EXIF orientation, canvas-memory ceiling, touch-drag precision | manual-only, real-device required | N/A | N/A — explicitly gated behind the Pre-Execution Blocker; do not attempt to automate |

### Sampling Rate
- **Per task commit:** `dotnet test QuestBoard.UnitTests` (fast, no WebApplicationFactory startup cost)
- **Per wave merge:** `dotnet test` (full suite, including `QuestBoard.IntegrationTests`)
- **Phase gate:** Full suite green before `/gsd:verify-work`, plus the real-device manual checklist (blocked pending device-access confirmation)

### Wave 0 Gaps

- [ ] `QuestBoard.UnitTests/Services/CharacterServiceTests.cs` (or equivalent) — new test(s) covering the widened `UpdateAsync(model, hasNewOriginalUpload, newCroppedImageData, token)` signature's three branches (new crop supplied / new original with no crop / no new file at all) — check whether this file already exists and needs extension vs. creation
- [ ] `QuestBoard.UnitTests/Services/ContactServiceTests.cs` / `DungeonMasterProfileServiceTests.cs` — same three-branch coverage for the other two services
- [ ] `QuestBoard.IntegrationTests/Controllers/CharactersControllerIntegrationTests.cs` — extend with a case posting a real `CroppedPictureFile` alongside `ProfilePictureFile` and asserting `CroppedImageData` persists as the submitted crop bytes (not null, not the fallback) — mirrors the existing `Edit_NewOriginalPhotoUpload_ClearsStaleCroppedImage` pattern at line 510
- [ ] Equivalent extensions to `ContactsControllerIntegrationTests.cs`
- [ ] New integration coverage for the `GetCroppedPicture`/equivalent actions once added (currently no test exists because the actions don't exist yet)
- [ ] Confirm whether `DungeonMasterControllerIntegrationTests.cs` exists at all at plan time — if the DM controller has no integration test file yet, this phase's DM repoint work is the first to need one

## Security Domain

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | No | Unchanged — existing `[Authorize]`/policy attributes on all touched controllers remain as-is |
| V3 Session Management | No | Not touched by this phase |
| V4 Access Control | Yes | New cropped-read endpoints must replicate the exact same auth/visibility checks as their sibling original-read endpoints (e.g. `GetCroppedPicture` must apply the same `CanManageCharacterAsync`-adjacent visibility rule `GetProfilePicture` doesn't currently gate — confirm at plan time whether `GetProfilePicture` itself has any visibility check today, since Contact's `GetContactImage` does have one via `IsVisibleTo`) |
| V5 Input Validation | Yes | `IImageValidationService.ValidateImagePair` (existing, Phase 45) — this phase's job is to call it with a real second file, not to build new validation |
| V6 Cryptography | No | Not applicable — no cryptographic operations in this phase |

### Known Threat Patterns for this stack

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Client posts a "cropped" Blob that isn't actually image data (malformed/oversized file crafted to look like a crop result) | Tampering | Already covered: `IImageValidationService.ValidateImagePair` re-validates MIME/extension/size server-side for both files, regardless of what the client claims to have cropped — this phase must ensure the cropped file is passed through this exact same validator, not trusted as "already validated client-side" |
| A new cropped-read endpoint (`GetCroppedPicture` etc.) is added without replicating an existing endpoint's authorization/visibility scoping | Elevation of Privilege / Information Disclosure | New endpoints must be built by copying the exact auth pattern of their sibling original-read endpoint in the same controller — verify this explicitly per-controller at plan/implementation time, since `GetContactImage` has a visibility check (`IsVisibleTo`) that `GetProfilePicture`/`GetDMProfilePicture` may or may not mirror identically |
| Crop coordinates or dimensions from the client are trusted for server-side re-processing | Tampering | Does not apply to this phase's design — the server never re-crops or re-derives dimensions from client-supplied coordinates; it only stores whatever bytes the client's `$toCanvas()`-produced Blob contains, post-validation. No crop-coordinate parameter crosses the network at all. |

## Sources

### Primary (HIGH confidence)
- Direct codebase reads (this session): `QuestBoard.Domain/Interfaces/ICharacterService.cs`, `IContactService.cs`, `IDungeonMasterProfileService.cs`, `QuestBoard.Domain/Services/CharacterService.cs`, `ContactService.cs`, `DungeonMasterProfileService.cs`, `QuestBoard.Domain/Interfaces/IImageValidationService.cs`, `QuestBoard.Repository/CharacterRepository.cs`, `QuestBoard.Service/Controllers/{Characters,Contacts,DungeonMaster}/*.cs`, `QuestBoard.Service/ViewModels/{CharacterViewModels,ContactViewModels,DungeonMasterViewModels}/*.cs`, `QuestBoard.Service/Views/{Characters,Contacts,DungeonMaster,Quest,QuestLog}/*.cshtml`, `QuestBoard.Service/Views/Shared/_Layout*.cshtml`, `QuestBoard.Service/wwwroot/css/{characters,contacts,dm-profile,dm-profile.mobile,dm-editprofile.mobile}.css`, `QuestBoard.Service/wwwroot/js/site.js`, `QuestBoard.IntegrationTests/Controllers/CharactersControllerIntegrationTests.cs`
- `npm view cropperjs version` / `versions --json` / `repository.url` / `dist-tags` — direct npm registry queries, this session
- https://developer.mozilla.org/en-US/docs/Web/API/Window/createImageBitmap — `imageOrientation` option semantics and default value
- https://developer.mozilla.org/en-US/docs/Web/CSS/image-orientation — confirms modern-browser `<img>` default auto-rotation behavior vs. canvas `drawImage()`'s lack thereof
- https://caniuse.com/mdn-api_createimagebitmap_options_imageorientation_parameter_from-image — Safari/iOS Safari version support (16+)
- https://fengyuanchen.github.io/cropperjs/v2/api/cropper-selection.html — `aspectRatio`, `$toCanvas()` signature, `change` event
- https://fengyuanchen.github.io/cropperjs/v2/api/cropper-image.html — `src` attribute behavior, `rotatable`/`scalable` properties

### Secondary (MEDIUM confidence)
- https://github.com/fengyuanchen/cropperjs/discussions/1264 — `$toCanvas()` as v2's replacement for v1's `getCroppedCanvas()`
- https://github.com/fengyuanchen/cropperjs/issues/685 — Cropper.js v2's own EXIF/orientation-CSS interaction bug, informing the "pre-correct before Cropper.js sees it" recommendation
- https://pqina.nl/blog/set-value-to-file-input/ — `DataTransfer`-based file-input population pattern, Safari 14.1+ support confirmed, iOS Safari noted as working without the desktop-Safari-only cosmetic quirk
- https://dev.to/imerljak/building-a-modern-image-cropper-in-react-with-cropperjs-2x-43b1 — corroborating markup pattern (cropper-canvas/cropper-image/cropper-selection/cropper-grid/cropper-handle), cross-checked against the official API docs above
- Web search corroboration for Cropper.js v2's internal Pointer Events usage (`pointerdown`/`pointerup` with touch/mouse fallback) — not reached via a single official doc page, cross-checked across multiple search results describing consistent behavior
- https://app.unpkg.com/cropperjs@2.1.1/files/dist — confirmed `dist/cropper.js`/`cropper.min.js` as plain UMD/global scripts, no separate CSS file shipped for v2

### Tertiary (LOW confidence)
- General web search results on EXIF-parsing libraries (blueimp/JavaScript-Load-Image, exif-js) — used only to confirm these exist as historical hand-rolled options, not adopted as the recommendation given the stronger native-API finding

## Metadata

**Confidence breakdown:**
- Standard stack (Cropper.js v2.1.1 version/API): HIGH — confirmed directly via `npm view`, official API doc pages, and a corroborating GitHub discussion/issue
- EXIF-orientation fix (createImageBitmap): HIGH — MDN is authoritative for the API semantics; caniuse.com is authoritative for browser-version support; this is a materially stronger finding than project-level PITFALLS.md's hand-rolled-parser assumption
- Backend signature gap (Pitfall 4): HIGH — found via direct source reads of the actual shipped Phase 45 code, not inference; this is the most load-bearing finding in this document
- `wwwroot/lib/` convention correction: HIGH — confirmed via direct `_Layout.cshtml` reads and filesystem check (no `wwwroot/lib/` directory exists)
- Dual-file submission mechanics (second hidden input + DataTransfer): MEDIUM-HIGH — pattern is well-documented and cross-verified, but the exact choice of "second input" vs. "swap" is this research's judgment call, not a single canonical source's prescription
- Touch/pointer handling inside Cropper.js v2: MEDIUM — corroborated across multiple search results but not confirmed via a single official "architecture" doc page; low risk since it only affects confidence in *why* it should work, not *whether* the phase needs to build anything extra

**Research date:** 2026-07-07
**Valid until:** 30 days (Cropper.js v2 has an active but not fast-moving release cadence; native browser API support is stable/Baseline; the codebase-specific findings — backend signature gap, no-lib-convention — are current-state facts that only change if Phase 46 itself or a later phase modifies them)
