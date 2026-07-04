# Stack Research

**Domain:** Adding client-side image-crop UI + dual-image persistence + server-side image processing + iOS Safari CSS fix, to an existing server-rendered ASP.NET Core 10 MVC app (v7.0 Backlog Cleanup milestone)
**Researched:** 2026-07-04
**Confidence:** MEDIUM (web-search-derived; no Context7/official-docs MCP available this session — versions cross-checked against NuGet/npm pages directly, but treat as verify-before-pinning)

## Recommended Stack

### Core Technologies

| Technology | Version | Purpose | Why Recommended |
|------------|---------|---------|-----------------|
| Cropper.js | 2.1.1 (npm `cropperjs`) | Client-side drag/resize crop UI over an uploaded image | Current de-facto standard vanilla-JS cropper (961+ dependent npm packages), framework-agnostic, ships as native Web Components (`<cropper-canvas>`, `<cropper-image>`, `<cropper-selection>`) that drop into a plain `.cshtml` view with a `<script type="module">` import — no build step, no SPA framework, no jQuery (v1's old dependency) required. Actively published (latest release ~3 months old at research time). |
| SixLabors.ImageSharp | 4.0.0 (targets net8.0+, works on net10.0) | Server-side re-encode/resize/validate of the cropped image before storage | Fully managed .NET code — **zero native binary dependency**, so nothing to verify on the Ubuntu 24.04 LXC host (no `.so` loading, no `ldd` checks, no apt packages). This directly resolves the year-old deferred SkiaSharp blocker by sidestepping the native-dependency question entirely. Apache-2.0 licensed for this project (org revenue well under the Six Labors Split License's $1M/year commercial threshold). See "SkiaSharp re-evaluation" below for why it's not the pick despite being viable now. |

### Supporting Libraries

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| (none — no additional NuGet package needed beyond ImageSharp) | — | — | Standard `IFormFile` → `Stream` → `Image.Load()` → `.Resize()`/`.Crop()` → `.SaveAsJpeg()`/`.SaveAsPng()` covers this feature's full server-side need. |
| (none — no additional npm package needed beyond cropperjs) | — | — | Cropper.js v2 ships its own Web Component styling; no separate CSS framework integration needed beyond loading its stylesheet. |

### Development Tools

| Tool | Purpose | Notes |
|------|---------|-------|
| Browser DevTools (iOS Safari via BrowserStack/real device, or Safari Technology Preview responsive mode) | Verify the CSS fixed-background fix actually renders fixed on iOS | `background-attachment: fixed` bugs are notoriously inconsistent across iOS Safari point releases — desktop Safari does NOT reproduce the bug, so a real iOS device or an iOS simulator/BrowserStack session is required for verification, not just resizing a desktop browser window. |

## Installation

```bash
# Client-side crop UI (npm not currently used in this repo — see integration note below)
npm install cropperjs@2.1.1

# Server-side image processing (NuGet, add to QuestBoard.Domain — keep out of QuestBoard.Service per the EF-package layering convention; ImageSharp itself has no EF dependency, but Domain is where business logic like "produce a stored cropped/original image pair" belongs)
dotnet add QuestBoard.Domain package SixLabors.ImageSharp --version 4.0.0
```

**Integration note — no npm/bundler pipeline exists in this repo today.** `wwwroot` is plain static files served by ASP.NET Core's static file middleware; there is no `package.json`/webpack/vite build step evident in the codebase. Two integration options:
1. **Recommended:** Download the `cropperjs` UMD/ESM build's static distribution files (`cropper.js` + its CSS, or the ESM `cropper.esm.js`) and drop them into `wwwroot/lib/cropperjs/` by hand (same pattern likely already used for Bootstrap/FontAwesome in this project — check `wwwroot/lib/` before implementation). Reference via `<script type="module" src="~/lib/cropperjs/cropper.esm.js">` in the two upload views (`Character` create/edit, `EditDMProfile`). This avoids introducing a Node/npm build pipeline into a project that has none.
2. **Alternative:** Pull from a CDN (`unpkg.com/cropperjs@2.1.1` or jsDelivr) with a Subresource Integrity hash — simplest, but adds an external runtime dependency for a self-hosted app; given this app already appears to vendor its own front-end assets, option 1 is more consistent with existing conventions. **Verify the `wwwroot/lib/` convention against `_Layout.cshtml`'s existing `<script>`/`<link>` tags before deciding.**

## Alternatives Considered

| Recommended | Alternative | When to Use Alternative |
|-------------|-------------|--------------------------|
| Cropper.js v2 | Cropper.js v1.6.x | Only if the Web Components approach proves awkward to wire into Razor views (e.g. CSP/module-script friction) — v1 is the older canvas+`<img>`+absolute-positioned-div implementation, still works, but is effectively in maintenance mode versus v2's active development. Prefer v2 unless a concrete blocker appears during implementation. |
| Cropper.js | `react-easy-crop`, `ngx-image-cropper` | Never for this project — both require React/Angular. This app has no SPA framework and none should be introduced for a scoped crop-before-save feature. |
| Cropper.js | Native `<input type="file">` + raw `<canvas>` + manual pointer-event math (no library) | Only if the team wants zero third-party JS at all costs. Not recommended: reimplementing drag-handles, aspect-lock, touch support, and pinch-zoom by hand is a multi-day effort Cropper.js already solved and battle-tested; not worth it for a "scoped feature" per the milestone framing. |
| SixLabors.ImageSharp | SkiaSharp 4.148.0 | If a future feature needs actual rendering/compositing beyond simple resize/crop/re-encode (e.g. drawing text/shapes onto images, complex canvas-like operations) — SkiaSharp is a rendering engine, not primarily an image-processing library, and its API is more verbose for basic resize/crop tasks. Also reconsider if ImageSharp's commercial license threshold ever becomes a concern (it won't at this project's scale). |
| SixLabors.ImageSharp | Magick.NET | If ImageMagick-specific format support or filters are needed (this app only handles JPEG/PNG photo uploads, so no need). Magick.NET is free/OSS with no revenue-based licensing caveat, but it's a native-backed wrapper (bundles its own native ImageMagick binaries per-RID) and is the slowest of the three in resize benchmarks — worse fit than ImageSharp's fully-managed simplicity for this narrow use case. |
| Pure-CSS `::before`/pseudo-element fixed layer | JS scroll-listener background-position hack | Never for this project — the JS approach is the older, higher-maintenance workaround for the same problem; modern CSS (`position: fixed` on a layered pseudo-element) fully solves it with no JS and no scroll-jank risk. Only fall back to JS if a future requirement needs parallax-style *speed* differences (not just "stay fixed"), which is out of scope here. |

## What NOT to Use

| Avoid | Why | Use Instead |
|-------|-----|--------------|
| SkiaSharp for this feature | The exact concern that paused this feature for a year (native `libSkiaSharp.so` availability on the deploy host) is now moot only because we're picking a library that has no native dependency at all — re-litigating SkiaSharp's Ubuntu 24.04 compatibility (it likely IS fine now; Ubuntu 24.04 is glibc-based like the originally-planned Debian Bookworm target, and `SkiaSharp.NativeAssets.Linux.NoDependencies` exists specifically for minimal-dependency Linux hosts, with the standard variant needing only `apt install libfontconfig1`) is unnecessary extra deployment-verification work when ImageSharp needs zero verification. Don't reopen this investigation just because the constraint changed — the simpler library is now clearly correct given the app's modest image-processing needs (resize + re-encode, not compositing/rendering). | SixLabors.ImageSharp 4.0.0 |
| A full-blown SPA framework (React/Vue/Angular) "just for the cropper" | This app is 100% server-rendered Razor MVC with zero client-side framework anywhere; introducing one for a single crop widget would be wildly disproportionate and break the project's stated "no framework changes" constraint. | Cropper.js v2 as a standalone Web Component, loaded via `<script type="module">` in the existing `.cshtml` upload forms |
| Base64-encoding the cropped image into a hidden `<input type="text">` field | Works, but bloats form payload by ~33% (base64 overhead) and requires manual `Convert.FromBase64String` server-side parsing instead of using the framework's native `IFormFile` model binding. | `canvas.toBlob()` → append the `Blob` to a `FormData`/hidden `<input type="file">` (via `DataTransfer`) so the existing `IFormFile` controller-action parameter pattern keeps working unchanged |
| JS-based `background-attachment: fixed` polyfills/scroll-listeners | Deprecated pattern now that a pure-CSS fix exists; adds scroll-event listener overhead and jank risk on mobile Safari, which is exactly the performance-sensitive context you're trying to fix. | CSS-only `::before`/`div` layer with `position: fixed` |

## Stack Patterns by Variant

**If the crop UI needs a fixed aspect ratio (e.g. portrait crop for a character photo):**
- Use Cropper.js v2's `<cropper-selection aspect-ratio="...">` attribute
- Because it constrains the drag-handles declaratively without custom JS math — directly matches the milestone's stated example ("make a portrait crop from a full-body photo")

**If original and cropped images must both persist per upload:**
- Submit both the original `File` (from the `<input type="file">` the user picked) AND the cropped `Blob` (from `$toCanvas()` + `toBlob()`) in the same `FormData` as two distinct `IFormFile` parameters on the controller action
- Because this avoids a round-trip re-upload of the original — the browser already has the original `File` object in memory from the initial file-picker selection; no need to re-derive or re-fetch it

**If the character/DM-profile upload forms currently post as standard (non-AJAX) MVC form submissions:**
- Keep them as standard form submissions; wire Cropper.js into the same submit flow by intercepting the "Save" button click, populating a hidden `<input type="file">` (via `DataTransfer.items.add(croppedFileFromBlob)`) with the generated cropped `File`, then letting the existing native form `submit()` proceed
- Because this requires the least controller/action-signature change — `IFormFile OriginalImage` + `IFormFile CroppedImage` bind exactly like the existing single `IFormFile` parameter does today, no fetch/AJAX rewrite needed

**Existing storage shape needs to change regardless of library choice (found during codebase check, not a library concern but affects rollout):**
- `CharacterImageEntity`/`DungeonMasterProfileImageEntity` today are 1-row-per-owner tables (`[Key][ForeignKey(nameof(Character))] public int Id`) storing exactly one `byte[]`. Storing both an original and a cropped image needs either two columns (`OriginalImageData`/`CroppedImageData` on the same row) or a discriminator/second table — a migration either way, independent of which crop/image library is chosen.

## Version Compatibility

| Package A | Compatible With | Notes |
|-----------|------------------|-------|
| SixLabors.ImageSharp 4.0.0 | .NET 10 (net10.0), QuestBoard.Service's `net10.0` TargetFramework | ImageSharp's minimum target is net8.0; net10.0 is forward-compatible. No known compatibility issues. |
| Cropper.js 2.1.1 | Any modern evergreen browser (Chrome/Edge/Safari/Firefox, incl. iOS Safari) supporting native Web Components/Custom Elements | No IE11 support in v2 (already dropped in the v1→v2 rewrite) — irrelevant for this app's user base. No legacy-browser support requirement is stated in PROJECT.md. |
| `canvas.toBlob()` | All target browsers | Universally supported in evergreen browsers; no polyfill needed. |

## Sources

- https://www.npmjs.com/package/cropperjs — version 2.1.1 confirmed as latest published (MEDIUM confidence, websearch-derived, cross-checked against GitHub repo)
- https://fengyuanchen.github.io/cropperjs/v2/guide.html — Web Component tag list, ESM import pattern (MEDIUM confidence)
- https://github.com/fengyuanchen/cropperjs/discussions/1264 — v2 `$toCanvas()` API replacing v1 `getCroppedCanvas()`, `canvas.toBlob()` chaining (MEDIUM confidence)
- https://www.nuget.org/packages/SkiaSharp — version 4.148.0 confirmed, target frameworks incl. net10.0 (MEDIUM confidence)
- https://www.nuget.org/packages/SkiaSharp.NativeAssets.Linux.NoDependencies — native dependency list (libpthread/libdl/libm/libc/ld-linux) for minimal Linux hosts (MEDIUM confidence)
- https://www.nuget.org/packages/SixLabors.ImageSharp — version 4.0.0 confirmed, targets net8.0+, stable (MEDIUM confidence)
- https://sixlabors.com/posts/license-changes/ and https://github.com/SixLabors/ImageSharp/blob/main/LICENSE — Six Labors Split License terms, $1M revenue threshold for commercial license requirement (MEDIUM confidence)
- https://anthonysimmon.com/benchmarking-dotnet-libraries-for-image-resizing/ — ImageSharp vs SkiaSharp vs Magick.NET vs NetVips performance/memory comparison (MEDIUM confidence)
- https://dev.to/saint_vandora/the-ultimate-guide-choosing-between-sixlaborsimagesharp-and-skiasharp-for-net-image-processing-17hi — API complexity and deployment-dependency comparison (MEDIUM confidence)
- https://juand89.hashnode.dev/troubleshooting-background-attachment-fixed-bug-in-ios-safari and https://css-tricks.com/the-fixed-background-attachment-hack/ — pure-CSS pseudo-element/fixed-layer fix for iOS Safari (MEDIUM confidence, cross-checked across multiple independent sources)
- Direct codebase inspection (`QuestBoard.Service.csproj`, `QuestBoard.Repository/Entities/CharacterImageEntity.cs`, `wwwroot/css/site.css`, `wwwroot/css/mobile.css`) — HIGH confidence, primary source; confirms .NET 10 target, current single-image storage shape, and the exact `background-attachment: fixed` declarations needing the CSS fix

---
*Stack research for: D&D Quest Board v7.0 Backlog Cleanup — image crop UI, dual-image storage, server-side image processing, iOS Safari CSS fix*
*Researched: 2026-07-04*
