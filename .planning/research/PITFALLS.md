# Pitfalls Research

**Domain:** Client-side image crop-before-upload UI + new server-side image-processing dependency (SkiaSharp) + iOS-Safari-specific CSS/JS bug fixing, added to an existing server-rendered ASP.NET Core 10 MVC app with a mobile-view split, deployed via bare `dotnet run` on a non-Docker Ubuntu 24.04 LXC host
**Researched:** 2026-07-04
**Confidence:** HIGH (grounded in this codebase's actual source — `CharacterImageEntity.cs`, `DungeonMasterProfileImageEntity.cs`, `site.css`/`mobile.css`'s still-broken `background-attachment: fixed` rule, the single-file `wwwroot/js/site.js` with no bundler — cross-checked against `.planning/PROJECT.md`'s decision log and `.planning/codebase/CONCERNS.md`, plus current (2026) sourced findings on SkiaSharp Linux native deployment, canvas EXIF handling, and iOS Safari devtools-emulation gaps)

## Critical Pitfalls

### Pitfall 1: SkiaSharp Is Committed to Before It's Verified on the Actual Deployment Host

**What goes wrong:**
`libSkiaSharp.so` is a native (non-.NET) shared library, not managed code — NuGet restoring `SkiaSharp` only drops the correct `runtimes/linux-x64/native/libSkiaSharp.so` into the publish output; it does **not** guarantee the shared library actually loads at runtime on a given Linux box. On a bare (non-Docker) host, the two most common failure modes are (a) `DllNotFoundException: Unable to load shared library 'libSkiaSharp' or one of its dependencies` because `libfontconfig.so.1` (and its own transitive deps: `libfreetype`, `libexpat`) isn't installed, and (b) the wrong native asset gets restored/published (e.g. a `dotnet publish` without an explicit `--runtime linux-x64` pulling in the wrong RID-specific folder, or a framework-dependent publish that never resolves a RID-specific native asset at all). This project's own `.planning/PROJECT.md` Key Decisions table already documents this exact risk being paused once ("SkiaSharp native lib availability on deployment host unverified") — the mistake to avoid is un-pausing it by writing the crop UI and the crop *service* first, and only trying `dotnet publish` + `dotnet run` on the real LXC host at the very end, when a missing `.so` blocks the whole feature after all the UI work is already sunk cost.

**Why it happens:**
Every other dependency in this stack (EF Core, Hangfire, ASP.NET Identity) is pure managed .NET and "just works" cross-platform with zero extra host setup — SkiaSharp is the first native-interop dependency this project would introduce. Docker-based projects get this verification for free because the base image (e.g. `mcr.microsoft.com/dotnet/aspnet`) is curated and frequently already has `fontconfig`; a bare LXC container built from a minimal Ubuntu 24.04 cloud image has no such guarantee and may be missing `fontconfig`, `libfreetype6`, or even basic X11-adjacent libraries entirely.

**How to avoid:**
- **Do this as Phase 0, before any UI or crop-service code is written.** SSH into the actual LXC host (not a local Windows dev machine, not a differently-provisioned VM) and run a minimal spike: `dotnet new console`, add the `SkiaSharp` package, write a 5-line program that loads a JPEG, calls `SKBitmap.Decode`, resizes, and re-encodes — `dotnet publish -r linux-x64 --self-contained false` (matching how this project already deploys, per `docker-compose`/`dotnet run` conventions in PROJECT.md) and run it on the host.
- Check `ldd` against the published `libSkiaSharp.so` on the host (`ldd runtimes/linux-x64/native/libSkiaSharp.so | grep "not found"`) to see missing dependencies directly, rather than waiting for a runtime exception.
- If `fontconfig`/`libfontconfig1` is missing, install it via `apt install libfontconfig1` — this requires host access this project's Linux deployment doc (`docs/server-setup.md`, referenced in Key Decisions) should be updated to include as a one-time prerequisite, since there's no Dockerfile layer to bake it into.
- Only after the spike succeeds on the real host should the actual crop-service implementation begin. If it fails and can't be resolved quickly, treat this as a real go/no-go gate — the milestone context explicitly frames this feature as "paused pending image-tooling verification," so a second failed verification should route back to picking a different library (see Recovery Strategies) rather than persisting effort into UI work built on an unverified foundation.

**Warning signs:**
- Any PLAN.md for this milestone that sequences "build crop service" before "verify SkiaSharp on host."
- `dotnet build`/`dotnet test` passing locally (Windows dev machine) being treated as evidence the Linux host will work — SkiaSharp's Windows native asset is a completely different binary with different dependencies.
- No entry in `docs/server-setup.md` (or equivalent ops runbook) documenting the required `apt install` step for whatever native package SkiaSharp needs on this specific host.

**Phase to address:** A dedicated Phase 0 spike, gated before any crop-UI or crop-service phase begins. Should produce a go/no-go decision plus an ops-runbook update, not application code.

---

### Pitfall 2: EXIF Orientation Is Silently Discarded by Canvas-Based Cropping, Producing a Sideways or Upside-Down Saved Image

**What goes wrong:**
Phones (especially iPhones) very often save photos with the pixel data in landscape/sensor-native orientation and an EXIF `Orientation` tag telling viewers to rotate it for display — the pixels themselves are not physically rotated. An `<img>` tag in a modern browser respects this tag and displays the photo correctly on screen, which lulls testers into thinking everything is fine. But `CanvasRenderingContext2D.drawImage()` does **not** read or apply EXIF orientation — it draws the raw, unrotated pixel data exactly as stored. If the crop UI shows the user a correctly-oriented `<img>` preview to draw a crop frame over, then hands the *coordinates* off to a `<canvas>` that draws from the same raw image source, the canvas output is silently sideways or upside-down relative to what the user saw and selected — and this corrupted result is what gets uploaded and permanently stored as the "cropped" variant.

**Why it happens:**
This bug is invisible on every desktop test photo (desktop screenshots/downloads almost never carry orientation EXIF, since they aren't captured by a physically-rotatable sensor) and invisible when testing with an already-corrected image. It only manifests with photos taken directly on a phone in portrait orientation (front camera and rear camera in tall/portrait grip both commonly trigger `Orientation: 6` or `8`) and then uploaded from that same phone without ever passing through an intermediate editor that would have baked in the rotation. Since this project's whole reason for building this feature is mobile character/profile photo uploads (`.Mobile.cshtml` views exist specifically because users are on phones), this is the primary, not edge-case, user path.

**How to avoid:**
- Read the EXIF `Orientation` tag client-side before drawing to canvas (a small, dependency-free EXIF parser reading the JPEG APP1 segment is sufficient — no npm package needed given this project has no JS build pipeline; a single vendored `<script>` file such as the widely-used `exif-js`/`blueimp-load-image`-style snippet, or a hand-rolled ~40-line orientation reader, dropped into `wwwroot/js/` like `site.js` already is).
- Apply the corresponding canvas transform (`ctx.rotate()`/`ctx.scale()` combination per orientation value 1–8) **before** drawing the image, so the crop-frame coordinates the user selected against the correctly-displayed preview line up with what canvas actually rasterizes.
- After correcting orientation once at crop time, strip/normalize the EXIF `Orientation` tag on the output (canvas `toBlob()`/`toDataURL()` output has no EXIF at all, so this happens for free — but confirm the *original* image being preserved unmodified per this milestone's "keep the original" requirement is stored as-is, EXIF included, since the character-details page presumably still relies on the browser's own `<img>`-tag EXIF handling to display the original correctly).
- Test explicitly with a photo taken directly on an iPhone in portrait grip (not a screenshot, not a re-saved/re-exported image) — this is the one case that reliably reproduces the bug.

**Warning signs:**
- The crop UI's `<canvas>` draws directly from an `Image` object loaded via `img.src = URL.createObjectURL(file)` with no orientation-correction step in between.
- Any manual test of the crop feature was done with existing sample images, screenshots, or desktop-sourced photos rather than a phone-camera photo taken specifically for the test.
- The cropped output looks fine when re-opened in an OS photo viewer (which reads its own file's now-EXIF-less pixels honestly) but looked wrong during the actual crop step, or vice versa.

**Phase to address:** The crop-UI implementation phase — this must be a named acceptance criterion ("crop a portrait iPhone photo end-to-end and verify orientation"), not an incidental discovery during human-verify.

---

### Pitfall 3: Large Mobile-Camera Photos Exceed iOS Safari's Canvas Memory/Size Limits, Crashing the Crop UI Silently

**What goes wrong:**
iOS Safari enforces a hard canvas area ceiling (16,777,216 total pixels, i.e. width × height) and a total canvas memory budget that has historically ranged roughly 224–384MB across WebKit versions, both well below what a modern phone's camera produces — a single 12MP+ photo (4032×3024 or higher on newer iPhones) is already ~12.2 million pixels, and if the crop UI creates more than one canvas at once (e.g. a full-size hidden canvas plus a live-preview canvas plus an output canvas), the *combined* memory across all of them can exceed the ceiling well before any single canvas looks obviously oversized. When the limit is hit, WebKit doesn't throw a catchable JS exception in most cases — it silently produces a blank/black canvas, or the tab reloads/crashes, with no error surfaced to the page's own code. A crop feature built and tested only against downscaled sample images (or desktop-emulated "mobile" resolutions) will never trigger this and will appear to work perfectly until a real user uploads a real phone photo.

**Why it happens:**
Desktop Chrome and Chrome's device-emulation mode both use the desktop machine's actual memory and GPU canvas backend — they never apply WebKit's iOS-specific canvas ceiling, so this class of bug is structurally invisible to any test that isn't running actual Safari on actual iOS. This project has already been burned by exactly this category of gap once (the `background-attachment: fixed` bug, Pitfall 5 below), so it should be treated as an established pattern for this codebase, not a one-off surprise.

**How to avoid:**
- Downscale the source image to a bounded maximum dimension (e.g. 2000–2500px on the long edge — comfortably under both the pixel-area ceiling and a sane upload size) in a single pass immediately after file selection, before ever drawing it into a full-resolution canvas for interactive cropping. Do the crop-frame interaction against this downscaled working copy, not the raw multi-megapixel original.
- Never allocate more than one full-resolution canvas at a time; reuse a single canvas element for preview and output rather than creating several.
- Explicitly free canvases that are no longer needed (`canvas.width = 0; canvas.height = 0;` or set to 1×1) rather than relying on garbage collection, since WebKit is known to hold onto canvas memory longer than expected until prompted.
- Since the "store both original and cropped image" requirement means the *original* full-resolution file still needs to reach the server, do that as a plain file upload (no canvas involved) in parallel with/independent of the canvas-based crop preview — never route the original through canvas at all, only the crop preview/output needs canvas.

**Warning signs:**
- Manual testing only used sample images under ~1MB or pre-resized to typical desktop screenshot dimensions.
- The crop UI creates a new `<canvas>` element (rather than reusing one) for each interaction step (load, live preview, final crop output).
- No explicit resize/downscale step exists between "user selected a file" and "image is drawn to canvas."

**Phase to address:** The crop-UI implementation phase, verified specifically on a real iPhone with an actual (non-downscaled-for-testing) camera photo, not just a curated test asset.

---

### Pitfall 4: Touch-Drag Crop Frames Built Only With Mouse Events Silently Fail (or "Half Work") on Real Touchscreens

**What goes wrong:**
A crop-frame drag/resize interaction implemented with only `mousedown`/`mousemove`/`mouseup` will often appear to "sort of work" on a touchscreen because mobile Safari and Chrome synthesize compatibility mouse events for simple taps — but dragging, especially a resize-handle drag near the frame edge, frequently breaks down: the synthesized events don't fire reliably during a sustained drag, the page itself scrolls underneath the finger while the user is trying to drag the crop handle (because `touchmove` was never intercepted with `preventDefault()`), and small crop handles sized for a mouse cursor are difficult-to-impossible to grab precisely with a fingertip on a small phone screen. This is exactly the class of interaction this project's own `mobile.css` already anticipates for other controls (`min-height: 44px` touch-target sizing is already an established convention in this codebase) but a crop-frame's resize handles are a new, more precision-demanding interaction than a button tap.

**Why it happens:**
Building and testing on a desktop with a mouse is the natural default when there's no JS framework/library steering the implementation toward touch-aware event handling; Chrome's devtools mobile emulation *does* translate mouse actions into synthetic touch events reasonably well for simple taps, which can create false confidence that touch support "already works," while a real finger drag on real glass has different precision, contact-area, and scroll-conflict characteristics that emulation doesn't reproduce.

**How to avoid:**
- Implement drag/resize handling against Pointer Events (`pointerdown`/`pointermove`/`pointerup` with `setPointerCapture`) rather than separate mouse and touch handlers — Pointer Events unify both input types in one code path and are supported in iOS Safari, avoiding a maintained-twice mouse/touch branch.
- Call `touch-action: none` (CSS) on the crop-frame/handle elements, or `event.preventDefault()` on the relevant pointer/touch events, to stop the page from scrolling while the user is mid-drag on the crop frame.
- Size resize handles for a fingertip, not a cursor tip — at least 24×24px hit target (ideally matching or exceeding the existing 44px touch-target convention where the design allows), even if the visual handle graphic is smaller, by giving the handle a larger invisible hit-area than its drawn size.
- Test the actual drag-and-resize gesture (not just "does tapping do something") on a real phone — this is the interaction most likely to reveal problems invisible in devtools emulation.

**Warning signs:**
- Event listeners named/typed as `mousedown`/`mousemove` with no corresponding `touchstart`/`pointerdown` handling.
- The page scrolls or bounces while attempting to drag a crop handle on a real phone.
- Crop handles that are comfortable to grab with a mouse cursor but require multiple attempts to grab with a finger during manual phone testing.

**Phase to address:** The crop-UI implementation phase — should be verified on a real touchscreen device as an explicit acceptance step, not inferred from devtools emulation passing.

---

### Pitfall 5: iOS-Safari-Specific CSS/Behavior Bugs Are Declared "Fixed" After Only Desktop and Devtools-Emulation Verification

**What goes wrong:**
This exact failure already happened once in this codebase: `background-attachment: fixed` scrolls the background with the page content on iOS Safari (a long-documented WebKit quirk where `fixed` attachment doesn't work as spec'd unless the element is the `<html>` root, or in some iOS versions doesn't work at all on `body`), but works correctly on desktop Chrome, desktop Safari, and even Chrome's mobile-device-emulation mode — because none of those actually run WebKit's iOS rendering/compositing engine, only WebKit's desktop macOS engine or an entirely different engine altogether. Both `site.css` and `mobile.css` currently contain the identical broken rule (`background-attachment: fixed` appears in both, confirmed unfixed as of this research). The pitfall for *this* milestone is fixing the CSS, testing it in desktop Chrome/Firefox/devtools "iPhone" emulation, seeing it look right, and shipping — without ever confirming on an actual iPhone, which is the only environment that can validate the fix, because it's the only environment that reproduces the original bug.

**Why it happens:**
Devtools "device emulation" modes (Chrome, Firefox, Edge) change the viewport size and user-agent string, and in some cases pixel density, but they still render using the desktop browser engine underneath — they do not swap in Apple's actual WebKit-for-iOS compositor, scroll/rubber-banding behavior, or memory model. Any bug rooted in how iOS Safari specifically composites layers (as `background-attachment: fixed` is) is categorically invisible to any tool that isn't running real iOS WebKit. This project's own prior discovery of this bug (a real iPhone caught it after desktop Chrome and devtools emulation did not) is the single strongest piece of evidence this project has that this class of gap is real and recurring, not hypothetical.

**How to avoid:**
- Any fix for #116 must be verified on a real iOS Safari session (a physical iPhone, or at minimum Xcode's iOS Simulator running actual WebKit — noting the Simulator is closer to real but still not bit-identical to real hardware for compositor/scroll-performance bugs, so a physical device remains the gold standard when available). BrowserStack/Sauce Labs-style real-device cloud testing is an acceptable substitute if no physical iPhone is available to the team.
- The standard fix for this specific bug is to avoid `background-attachment: fixed` on `body` entirely for mobile and instead use a fixed-position pseudo-element or dedicated `<div>` sized to `100vh`/`100dvh` with `position: fixed; z-index: -1` behind the content, which sidesteps the iOS compositing limitation rather than fighting it — verify this approach specifically, since some naive "fixes" (e.g. just adding `-webkit-` prefixes, which don't exist for this property) do nothing.
- Because both `site.css` (desktop) and `mobile.css` (mobile) currently carry the identical broken rule, and this project maintains genuinely separate `.Mobile.cshtml` views/stylesheets (not one responsive stylesheet), the fix likely needs applying/verifying in both files — confirm whether desktop Safari on iPad (which also runs iOS/iPadOS WebKit, not desktop WebKit) is in scope too, since it would share the same underlying bug via `site.css` if the desktop layout is ever served to an iPad.
- Structurally: for *any* future CSS or JS change explicitly motivated by "iOS Safari does X differently," add a line to the phase's verification checklist that names the exact device/method used to confirm it (e.g. "verified on physical iPhone 13, iOS 18, Safari" or "verified via BrowserStack real-device iPhone session") — a generic "tested on mobile" checkbox is not sufficient evidence given this project's own history.

**Warning signs:**
- A PLAN.md or verification checklist for issue #116 that only lists desktop browsers and/or "Chrome devtools mobile view" as tested environments.
- The fix is verified by resizing a desktop browser window rather than by loading the actual deployed page on an iPhone.
- Any new CSS/JS work motivated by mobile-specific behavior ships without a named real-device (or real-device-cloud) verification step, repeating this exact gap for a *different* bug in the future.

**Phase to address:** The #116 background-fix phase directly, but also treat as a standing verification-protocol update: any phase touching CSS/JS with mobile- or iOS-specific intent should require real-device confirmation as an explicit gate, not an optional nice-to-have.

---

## Technical Debt Patterns

Shortcuts that seem reasonable but create long-term problems.

| Shortcut | Immediate Benefit | Long-term Cost | When Acceptable |
|----------|--------------------|-----------------|------------------|
| Vendoring a small hand-rolled EXIF-orientation-reader script into `wwwroot/js/` instead of adding an npm-managed dependency | Matches this project's existing no-build-pipeline convention (`site.js` is the only JS file) | No update path if a bug is found in the vendored snippet; must be manually re-patched | Acceptable and preferred here — introducing npm/webpack for one ~40-line utility would be a much larger architectural change than this milestone should trigger |
| Skipping the SkiaSharp host-verification spike and writing the crop UI first because "it's just image resizing, it'll probably work" | Faster perceived progress on the visible feature | Repeats the exact reason this feature was already paused once; risks a second stalled milestone if the native lib doesn't load | Never — this project has already documented this exact risk in Key Decisions; skipping the spike ignores its own prior lesson |
| Testing the crop feature and the iOS background fix only via Chrome devtools device emulation | Fast, free, no extra hardware/service needed | Both bug classes in this milestone (EXIF orientation, canvas memory limits, `background-attachment` compositing) are specifically invisible to devtools emulation — shipping on emulation-only testing risks repeating the #116 discovery pattern (bug ships, only a real device catches it later) | Acceptable only as a first-pass sanity check before a mandatory real-device pass; never as the sole verification |
| Storing the cropped variant without ever re-deriving it if the user re-uploads a new original | Simpler upload flow — one write path | Original and cropped can silently diverge (see Pitfall 6 below); user sees a crop that no longer corresponds to their current original | Acceptable only if re-upload always clears/regenerates both variants atomically in the same transaction — never acceptable as a partial update that touches only one column |

## Integration Gotchas

Common mistakes when connecting to existing internal seams.

| Integration | Common Mistake | Correct Approach |
|-------------|------------------|--------------------|
| `CharacterImageEntity`/`DungeonMasterProfileImageEntity` (`byte[] ImageData`, single column per row, `[Key][ForeignKey]` 1:1 with owner) | Adding a second `byte[]` column (e.g. `CroppedImageData`) directly to the existing entity without an EF Core migration, or forgetting the migration is auto-applied on startup (per this project's established convention) and must be generated via `dotnet ef migrations add` from `QuestBoard.Service/` | Add the new column via a proper EF Core migration (`dotnet ef migrations add AddCroppedCharacterImage --project ../QuestBoard.Repository`), keep the existing `ImageData` column semantics unchanged (still "the original," per the milestone's explicit requirement that character details keeps showing the original) |
| Existing `wwwroot/js/site.js` single-file convention | Adding a large, monolithic third-party cropping library (e.g. a full Cropper.js bundle) as a new `<script src>` without checking it doesn't conflict with existing global functions/variables in `site.js`, or duplicating toast/init patterns `site.js` already centralizes (per the recently-consolidated toast-init pattern from the v6.1 milestone) | Prefer either a small vendored single-purpose script matching the project's existing minimal-dependency convention, or if a library is used, load it as its own separate `<script>` tag (not merged into `site.js`) so it can be independently versioned/removed later |
| `.Mobile.cshtml` view-location expander split | Building the crop UI once and assuming it "just works" on the mobile view because it's "just JavaScript" — the mobile view is a *separate* `.cshtml` file that must independently include the crop UI's markup/script references; a change only made to the desktop view's `.cshtml` silently never reaches mobile users | Any new upload-with-crop markup must be added to both the desktop and `.Mobile.cshtml` variant of every affected view (character create/edit, DM profile edit) — verify by grepping for the view name pair, not by testing only one |
| SkiaSharp server-side resize/crop service | Placing image-processing code directly in a Controller or ViewModel, coupling `QuestBoard.Service` to SkiaSharp and violating the Service → Domain → Repository dependency direction this codebase enforces everywhere else | Put the crop/resize logic behind a Domain-layer service interface (e.g. `IImageProcessingService` in `QuestBoard.Domain`), with the SkiaSharp-specific implementation registered via DI — mirrors how `IEmailService`/`IDungeonMasterProfileService` are already structured |

## Performance Traps

Patterns that work at small scale but fail as usage grows.

| Trap | Symptoms | Prevention | When It Breaks |
|------|----------|------------|-----------------|
| Storing both original (potentially multi-megabyte, full camera resolution) and cropped `byte[]` blobs per upload, with no server-side downscale of the "original" before storing it | SQL Server row/page size and total DB size grow roughly 2x per image field compared to today's single-blob-per-image scheme; this project already has `Image blob storage migration — performance acceptable at current scale` listed as explicitly Out of Scope | Downscale the "original" to a sane maximum resolution (e.g. long edge ≤ 2000–3000px) server-side with SkiaSharp before storing it — camera-native originals (4000px+, several MB each) provide no visible benefit over a still-high-quality downscaled version for a character-photo/profile-photo use case, and this keeps both blobs bounded | At 17 members with a handful of characters/profiles each, this is not an urgent breaking point, but doubling image storage without any size cap compounds every future upload — bound it now while the schema change (new column) is already happening, rather than retrofitting a cap later |
| Serving the full-resolution stored blob (original or cropped) inline on pages that only need a thumbnail (e.g. guild member list, quest signup avatars) | Slow page loads as more characters/images accumulate — this project's own `CONCERNS.md` already flags "N+1 query risk in character + image loading" and lazy per-image HTTP fetches as a Medium-priority pre-existing issue | If SkiaSharp is being added anyway for crop support, use it to also generate/cache a small thumbnail variant for list views, rather than only solving the crop-specific need — cheap opportunistic win given the dependency is already being introduced | Already visible today per `CONCERNS.md`; will get proportionally worse once a second blob column is added unless list views are explicit about which variant they request |

## Security Mistakes

Domain-specific security issues beyond general web security, specific to this app's history.

| Mistake | Risk | Prevention |
|---------|------|------------|
| Trusting the client-supplied "cropped" image blindly, with no server-side re-validation of file type/size/dimensions | A modified client request could upload an arbitrarily large file, a non-image file with an image extension, or a malformed image crafted to exploit a decoder bug in whatever library parses it server-side (including SkiaSharp itself, or any server-side re-encode step) | Re-validate both uploaded blobs server-side (real content-type sniffing via magic bytes, not just the browser-supplied MIME type; a maximum dimension/file-size cap) before persisting either blob — this is a good use for the SkiaSharp dependency: decode-then-re-encode server-side (rather than storing the client's raw bytes verbatim) both validates the file is genuinely a well-formed image and normalizes it to a known-safe format |
| EXIF metadata (which can include GPS location data on some phone photos) preserved unmodified in the stored "original" blob and served back to any authenticated group member | A photo taken at a player's home and uploaded as a character/profile picture could leak GPS coordinates embedded in EXIF to every other member of the group who views/downloads the original image, even though the app's UI never displays that metadata | Strip GPS/location EXIF tags (while still correctly applying/baking in *orientation* per Pitfall 2) during the server-side re-encode step — decide explicitly whether to preserve any EXIF at all in the stored original, given this is a small trusted group but photos may still be personal |
| Uploading crop coordinates/dimensions with no server-side bounds checking (e.g. a crop rectangle that requests coordinates outside the source image's bounds) | A malformed or tampered request could cause a server-side crop operation to throw an unhandled exception, or in worse cases an out-of-bounds read depending on the underlying native library's behavior | Clamp/validate all client-supplied crop coordinates against the actual decoded source image's real dimensions server-side before calling into SkiaSharp, never trust the client-reported source dimensions |

## UX Pitfalls

| Pitfall | User Impact | Better Approach |
|---------|-------------|-------------------|
| Crop UI defaults to a fixed aspect ratio without accounting for whether the source photo is portrait or landscape, or forces a square/portrait frame onto a landscape source that awkwardly crops the subject | Users on mobile take predominantly portrait photos (natural phone grip); a crop tool that assumes landscape source dimensions (or vice versa) produces a frame that doesn't sensibly fit the photo, forcing an unusable crop | Detect source image orientation (width vs. height, after EXIF correction) and initialize the crop frame's starting size/position sensibly for that orientation — center a reasonably-sized frame respecting the target aspect ratio (e.g. square for profile photos) rather than a fixed pixel-coordinate default that assumes one orientation |
| No visible loading/processing state while a large mobile photo is being downscaled/processed client-side before the crop frame appears | On an older/slower iPhone, decoding and downscaling a large camera photo can take a perceptible moment; with no feedback, the page appears frozen or broken, and users may tap repeatedly or navigate away | Show an explicit "Processing image..." state between file selection and crop-frame display, matching the project's existing toast/notification visual language |
| Silent failure when a photo exceeds a size/dimension the crop UI can't handle on an older or memory-constrained iPhone | User sees a blank canvas or the crop UI simply doesn't appear, with no explanation — indistinguishable from "this feature is broken" | Wrap the canvas-drawing step in an explicit try/catch (where WebKit does raise a catchable error) and detect the black/blank-canvas failure mode where possible, surfacing a clear message ("This photo is too large — please choose a smaller photo or take a new one") rather than a silent dead end |

## "Looks Done But Isn't" Checklist

- [ ] **SkiaSharp dependency:** Often "works" because it was verified via `dotnet build`/`dotnet test` on the developer's Windows machine or in CI — verify explicitly on the actual bare Ubuntu 24.04 LXC production host via a real `dotnet publish -r linux-x64` + `dotnet run` cycle, not just a green build elsewhere.
- [ ] **Crop-before-upload feature:** Often tested only with desktop-sourced sample images (screenshots, stock photos) — verify with a photo taken directly on a real iPhone in portrait grip, checking the final stored crop is correctly oriented, not sideways/upside-down.
- [ ] **Crop-before-upload feature:** Often tested only via Chrome devtools mobile emulation — verify the actual touch-drag-resize interaction on a real touchscreen device, since emulation does not reproduce touch-drag precision or page-scroll conflicts.
- [ ] **Crop-before-upload feature:** Often missing a maximum-dimension downscale step before canvas operations — verify with a full-resolution (12MP+) unmodified camera photo on an actual iPhone, watching for the WebKit "canvas memory exceeds limit" failure mode (blank/black output, silent crash).
- [ ] **Original + cropped storage:** Often missing a defined behavior for what happens to the cropped variant when a user re-uploads a new original — verify re-upload/re-crop atomically replaces both variants together, never leaving a cropped image that was derived from a now-replaced original.
- [ ] **iOS Safari CSS fix (#116):** Often verified only on desktop browsers and devtools emulation, since that's exactly how the bug escaped detection the first time — verify explicitly on a physical iPhone (or a real-device cloud service), and record which device/method was used as part of the verification evidence.
- [ ] **Mobile view parity:** Often the crop UI is wired into the desktop `.cshtml` view but the corresponding `.Mobile.cshtml` view is missed or only partially updated — verify both view variants render and function identically for character-photo and DM-profile-photo upload flows.
- [ ] **Server-side re-validation:** Often the server trusts whatever blob(s) the client posts as "the crop" without independently re-decoding/re-validating them — verify the server actually re-processes the uploaded bytes through SkiaSharp (or equivalent) rather than storing client-supplied bytes verbatim.

## Recovery Strategies

| Pitfall | Recovery Cost | Recovery Steps |
|---------|----------------|------------------|
| SkiaSharp fails to load on the production LXC host after the feature is otherwise complete | HIGH | Roll back to feature-flag-disabled state (ship the crop UI behind a config toggle from the start, see Pitfall-to-Phase Mapping); investigate missing native deps via `ldd`; if unresolvable in reasonable time, fall back to `System.Drawing.Common` (Windows-only, not viable on Linux) is not an option — the realistic fallback is `ImageSharp` (fully managed, no native interop, slightly slower) as a drop-in replacement behind the same `IImageProcessingService` interface, since that abstraction boundary (Pitfall — Integration Gotchas) was built specifically to make this swap possible without touching controllers/views |
| A cropped image was stored with incorrect (EXIF-ignorant) orientation before the bug was caught | MEDIUM | Existing bad rows can be identified by querying for rows where `CroppedImageData` was written before the orientation fix's deploy date; affected users need either a one-time re-crop prompt or an admin-triggered batch reprocessing job (server-side, using the now-fixed SkiaSharp pipeline reading each stored original's own EXIF) — feasible specifically because the original is retained per this milestone's own design |
| The `background-attachment: fixed` fix is shipped but still doesn't work correctly on some iOS Safari version (partial fix) | LOW–MEDIUM | CSS-only rollback is cheap (revert the changed rule); investigate WebKit version-specific behavior differences (the bug's manifestation has changed across iOS/WebKit releases historically) and re-verify against the specific iOS version(s) affected, ideally via a real-device cloud service that offers multiple iOS versions rather than a single physical device |
| Touch-drag crop interaction is unusable on some (especially older) iPhones due to canvas memory pressure mid-gesture | LOW | Since crop coordinates should be computed against a downscaled working copy (Pitfall 3's prevention), tightening that downscale ceiling further is a config-only change, not a code rewrite — confirms the value of building the downscale step as a tunable constant rather than a hardcoded one-off value |

## Pitfall-to-Phase Mapping

How roadmap phases should address these pitfalls.

| Pitfall | Prevention Phase | Verification |
|---------|--------------------|----------------|
| Pitfall 1 — SkiaSharp unverified on bare LXC host | Phase 0 spike (before any crop UI/service code) | Real `dotnet publish -r linux-x64` + `dotnet run` on the actual production host, decoding/resizing/re-encoding a real image, before the phase is marked done |
| Pitfall 2 — EXIF orientation ignored by canvas | Crop-UI implementation phase | Manual test with a real portrait iPhone camera photo, uploaded end-to-end, confirming stored crop is correctly oriented |
| Pitfall 3 — iOS Safari canvas memory/size limit | Crop-UI implementation phase | Manual test with a full-resolution (12MP+) unmodified iPhone camera photo on a real device, confirming no blank/black canvas or crash |
| Pitfall 4 — Touch-drag crop frame usability | Crop-UI implementation phase | Manual drag/resize test on a real touchscreen device (not devtools emulation) |
| Pitfall 5 — iOS Safari CSS fix verified only on desktop/emulation | #116 background-fix phase | Real iPhone (or real-device cloud) verification recorded explicitly in the phase's verification evidence, naming the device/method used |
| Original/cropped storage divergence on re-upload | Crop-UI + storage implementation phase | Test: upload original A + crop A', then re-upload original B — verify both stored variants become (B, B') atomically, never a stale mix of A'/B |
| Server trusting client-supplied crop blob without re-validation | Crop-UI implementation phase (server-side portion) | Code review: confirm the server independently re-decodes/re-validates dimensions and content-type via SkiaSharp rather than persisting raw posted bytes unchecked |
| Mobile view parity for the new upload UI | Crop-UI implementation phase | Explicit check that both the desktop `.cshtml` and `.Mobile.cshtml` variants of every affected view (Character Create/Edit, DM Profile Edit) were updated and manually tested |

## Sources

- `QuestBoard.Repository/Entities/CharacterImageEntity.cs`, `DungeonMasterProfileImageEntity.cs` — current `byte[]`-blob image storage schema (1:1 `[Key][ForeignKey]`, no processing pipeline)
- `QuestBoard.Service/wwwroot/css/site.css` (line 382) and `mobile.css` (line 21) — confirmed both still carry the identical unfixed `background-attachment: fixed` rule as of this research
- `QuestBoard.Service/wwwroot/js/site.js` — confirmed sole JS file, no bundler/npm pipeline, establishing the "vendor small scripts directly" convention this feature must follow
- `.planning/PROJECT.md` — Key Decisions log: "Profile picture crop paused — SkiaSharp native lib availability on deployment host unverified... Pending: verify libSkiaSharp on aspnet:10 Debian Bookworm before resuming" (note: PROJECT.md's Docker-base-image framing predates this milestone's confirmed bare-LXC, non-Docker deployment target — the verification must be re-scoped to the actual Ubuntu 24.04 LXC host, not a Debian Bookworm container image)
- `.planning/codebase/CONCERNS.md` — "Profile picture crop/avatar selection (issue #78)" tech-debt entry; "N+1 query risk in character + image loading" performance note
- [SkiaSharp GitHub Issue #1999 — Unable to load shared library 'libSkiaSharp' or one of its dependencies](https://github.com/mono/SkiaSharp/issues/1999) — confirms `libfontconfig.so.1` as the most common missing native dependency on Linux
- [SkiaSharp.NativeAssets.Linux.NoDependencies on NuGet](https://www.nuget.org/packages/SkiaSharp.NativeAssets.Linux.NoDependencies/) — documents the fontconfig-excluded variant and its tradeoffs for slim/manual deployments
- [SkiaSharp Native Assets for Linux wiki](https://github.com/mono/SkiaSharp/wiki/SkiaSharp-Native-Assets-for-Linux) — RID-specific native asset publishing requirements
- [Total Canvas Memory Use Exceeds The Maximum Limit — PQINA](https://pqina.nl/blog/total-canvas-memory-use-exceeds-the-maximum-limit/) — iOS Safari canvas memory ceiling history and mitigation patterns
- [WebKit Bugzilla #195325 — Canvas context allocation fails](https://bugs.webkit.org/show_bug.cgi?id=195325) — confirms the 16,777,216-pixel canvas area ceiling
- [Croppie GitHub Issue #31 — EXIF rotation data isn't taken into account for uploaded images](https://github.com/Foliotek/Croppie/issues/31) — canvas-based crop libraries hitting this exact bug in production
- [Cropper.js GitHub Issue #120 — Handling EXIF Image Orientation](https://github.com/fengyuanchen/cropper/issues/120) — corroborates the same root cause across a different popular crop library
- [BrowserStack — How to Debug on iPhone Safari](https://www.browserstack.com/guide/how-to-debug-on-iphone) — documents devtools-emulation vs. real-device gap for touch interactions and device-specific rendering
- [Apple Developer Documentation — Responsive Design Mode](https://developer.apple.com/documentation/safari-developer-tools/responsive-design-mode) — confirms Safari's own responsive mode does not fully replicate real device/touch behavior

---
*Pitfalls research for: crop-before-upload image UI + SkiaSharp server dependency + iOS Safari CSS/JS bug fixing (D&D Quest Board v7.0 Backlog Cleanup milestone)*
*Researched: 2026-07-04*
