# Project Research Summary

**Project:** D&D Quest Board — Milestone v7.0 (Backlog Cleanup)
**Domain:** Server-rendered ASP.NET Core MVC web app; feature work spans client-side image cropping, dual-image storage, waitlist auto-promotion notifications, and iOS Safari CSS bug fixes
**Researched:** 2026-07-04
**Confidence:** MEDIUM-HIGH (architecture and pitfalls grounded in direct codebase reads; stack/feature findings are web-search-derived but corroborated across multiple independent sources)

## Executive Summary

v7.0 clears four long-standing backlog items, the headline being the crop-before-save avatar feature (#78) that has been paused since v1.0 over an unverified `SkiaSharp` native-library dependency on the deployment host. All four researchers converge on the same resolution: **no server-side image-processing library is needed at all.** Cropping happens entirely client-side via `<canvas>`; the server's job stays exactly what it is today — read an `IFormFile` into a `byte[]` and store it — except now it does that twice (original + cropped) instead of once. This eliminates the SkiaSharp deployment risk by removing the dependency rather than re-verifying it, and it is the direct answer to the question this research was commissioned to resolve.

The recommended approach: extend the existing `CharacterImageEntity`/`DungeonMasterProfileImageEntity` tables with a second nullable `byte[]` column (not a second table — cardinality is strictly 1:1 and always read/written together), post both the original file and a canvas-cropped `Blob` as two `IFormFile`s in the same existing `multipart/form-data` POST via a `DataTransfer` FileList swap (no AJAX, no fetch, the existing Razor `<form>` and model binding are untouched), and wire a vanilla-JS crop library into the three affected upload views (character create/edit, DM profile edit) with zero build tooling. The waitlist auto-promotion feature (#104) reuses 100% existing infrastructure (Hangfire + Razor/HtmlRenderer + Resend SMTP) — the only design risk is getting the trigger condition exactly right (notify only the passively-promoted player, never the player whose own action caused it, never broadcast to the rest of the waitlist).

Key risks are almost all mobile/iOS-specific and share one root cause: **desktop and devtools-emulation testing cannot catch them.** EXIF orientation silently corrupting canvas-cropped photos, iOS Safari's hard canvas memory ceiling crashing on real camera-resolution photos, touch-drag precision issues on real touchscreens, and the recurrence of the exact `background-attachment: fixed` bug class (already proven in this codebase to escape desktop/emulation testing once) all require verification on a real iOS device or device-cloud service as a named, non-optional acceptance step — not an assumed pass because devtools "mobile view" looked fine.
## Key Findings

### Recommended Stack

**Core technologies:**
- **Cropper.js v2.1.1** (vendored into `wwwroot/lib/cropperjs/`, no npm/bundler) — client-side drag/resize/zoom crop UI. See "Library Version Decision" below for why v2.1.1 is picked over the abandoned v1.6.2.
- **No server-side image-processing library** (no SkiaSharp, no ImageSharp, no Magick.NET) — the crop is fully client-side canvas work; the server only validates and stores the two byte arrays it receives. This is the resolution to the year-old SkiaSharp blocker: it is avoided entirely rather than re-verified.
- **Pure-CSS pseudo-element fix** for the iOS Safari `background-attachment: fixed` bug (#116) — replace the broken rule with a `position: fixed`, `100vh`/`100dvh` layered `::before`/`<div>`. No JS scroll-listener workaround needed.
- **Existing infra reused as-is** for waitlist promotion emails — Hangfire background jobs, Razor/HtmlRenderer HTML templates, Resend SMTP relay. No new package needed.

### Library Version Decision: Cropper.js v2.1.1 (not v1.6.2) — REVISED

**This decision was revised after the user pushed back on the original v1.6.2 recommendation and requested direct verification.** Direct fetches of GitHub's release/commit history (not secondhand via research agent) found:

- v1.6.2 was the last v1 release (2024-04-21); the v1 branch's last commit was 2025-03-08 — over a year stale with zero activity since.
- v2 is under active, ongoing maintenance: v2.0.0 (2025-03-01), v2.0.1 (2025-07-25), v2.1.0 (2025-10-19), v2.1.1 (2026-04-06) — a steady release cadence, most recent 3 months ago. npm's `dist-tags.latest` is `2.1.1`, confirming v2 is the actual current supported line, not v1.
- The original architecture-research objection to v2 — "requires hand-authored Web Components markup, a materially heavier integration surface" — does not hold up. A direct fetch of the published `dist/cropper.js` UMD bundle confirms it works via a plain `<script src="...">` tag with zero bundler, registering a global `window.Cropper`, exactly like v1. And v2's own basic-usage documentation shows the same simple imperative pattern as v1 — `new Cropper('#image')` against a plain `<img>` element — not hand-written `<cropper-canvas>` markup. The Web Components exist under the hood but aren't something the integrating code has to author by hand for basic usage.

**Resolution: use Cropper.js v2.1.1, vendored, pinned.** Shipping a new feature in a long-lived production app against a dependency with zero commits in over a year is a worse bet than a v2 API surface that turned out to be nearly as simple to integrate as originally assumed for v1. Confirm the exact cropped-canvas/blob extraction method (likely `.getCropperSelection().$toCanvas()` per earlier STACK.md findings, since v2's `Cropper` instance exposes an underlying `CropperSelection` element) against the official v2 API docs during Phase 3 implementation — this specific method call was not independently re-verified in this correction pass and should be confirmed against source, not assumed.

### Expected Features

**Must have (table stakes) — crop UX:**
- Draggable/resizable crop frame with fixed aspect ratio matching the destination avatar shape
- Live preview of the crop result before submit
- Zoom/pan inside the crop frame
- Touch-usable (drag + pinch) on mobile, verified on a real device
- Original image preserved unmodified; cropped image generated and stored separately (this is a storage/schema requirement, not just a UI one)
- Entirely client-side crop pipeline — no server-side image processing library, by design

**Must have (table stakes) — waitlist promotion:**
- Targeted "you were promoted" email to only the passively-promoted player
- Waitlist/signup status visible in the UI itself, not solely via email

**Should have / nice-to-have (not required this milestone):**
- Rotate/flip control (cheap to bolt on later via Cropper.js's existing API)
- In-app "you were promoted" banner in addition to email

**Defer / do not build:**
- Multiple aspect-ratio presets, image filters/brightness/contrast — scope creep, no second consumer shape exists
- SMS/push notification channels — contradicts this app's existing email-only, batch-first, small-trusted-group design
- Broadcasting a notification to the entire waitlist on any position change — the exact anti-pattern every waitlist UX source warns against, and directly costly given this app's constrained email budget (100/day, 3000/month)
- Server-side re-crop/image-processing pipeline "for robustness" — this is precisely the path that caused the original year-long pause; do not reopen it

### Architecture Approach

The crop feature adds no new infrastructure, no new endpoints, and no new external dependency. The existing plain `<form method="post" enctype="multipart/form-data">` on the three affected views keeps working completely unchanged: a new `image-crop.js` (vendored, no bundler) intercepts the file input's `change` event, opens a Cropper.js modal, and on confirm uses the `DataTransfer` FileList-swap trick to replace the visible file input's contents with the cropped `Blob`-as-`File` while placing the untouched original into a second hidden `<input type="file">`. The user's existing "Save" click then submits both files in one ordinary POST — no AJAX, no fetch, no `asp-validation` disruption.

**Major components:**
1. **Client-side crop UI** (`wwwroot/lib/cropperjs/` + new `wwwroot/js/image-crop.js` + `wwwroot/css/image-crop.css`) — renders the crop modal, produces the cropped `Blob`, performs the `DataTransfer` swap. Shared across all three affected forms rather than duplicated per view.
2. **ViewModel layer widening** (`CharacterViewModel`, `EditDMProfileViewModel`) — add a second nullable `IFormFile? OriginalPictureFile` alongside the existing picture field.
3. **New `IImageValidationService` (Domain layer)** — centralizes size/MIME validation currently duplicated three times inline in controllers; a natural moment to fix this existing tech debt since the validation surface is already being touched.
4. **Widened Repository methods** (`CharacterRepository`, `DungeonMasterProfileRepository`) — existing `UpdateProfileImageAsync`/`UpsertProfileImageAsync` accept two `byte[]?` instead of one; new `GetCharacterOriginalPictureAsync`/`GetOriginalPictureAsync` actions serve the original specifically for the character/DM details page.
5. **Schema change**: one additive nullable column (`OriginalImageData byte[]?`) added to each existing 1-row-per-owner image table — not a new table, not a new relationship. Existing `ImageData` column is repurposed to mean "cropped/display" bytes; old rows simply have `OriginalImageData = NULL` until next edit, with `GetOriginalPicture` falling back to `ImageData` for those rows.
6. **Waitlist promotion trigger + email template** — a new condition inside the existing signup-processing logic plus one new Razor/HtmlRenderer template, following the same pattern as 6+ existing job/template pairs. No new plumbing.

Suggested build order (from ARCHITECTURE.md, front-loads schema since everything downstream depends on it, defers the riskiest/most novel piece — the JS library integration — until the data path is already proven via direct testing): schema migration -> Repository/Domain plumbing -> ViewModel/controller wiring (testable via Postman before any UI exists) -> client-side crop UI -> character-details "show original" wiring -> cross-device/cross-browser UAT.

### Critical Pitfalls

1. **EXIF orientation silently discarded by canvas cropping** — `drawImage()` ignores the EXIF `Orientation` tag that browsers otherwise respect for `<img>` display; a portrait iPhone photo can be saved sideways/upside-down with no visible error during desktop testing. Must read and apply the orientation tag before drawing to canvas, and this must be a named acceptance criterion tested with an actual phone-camera photo, not a sample image.
2. **iOS Safari's hard canvas memory/pixel-area ceiling (16.7M pixels)** — a single modern 12MP+ camera photo can crash or blank the canvas silently on real iOS devices; invisible in desktop Chrome or devtools emulation. Downscale the source to a bounded max dimension (~2000-2500px) immediately after file selection, before any full-resolution canvas work, and verify with a real unmodified iPhone photo.
3. **Touch-drag crop handles built with mouse-only events "half work" on real touchscreens** — synthesized compatibility events mask this in devtools emulation. Use Pointer Events (not separate mouse/touch handlers), disable page scroll during drag (`touch-action: none`), size hit-targets for fingertips, and verify on a real touchscreen.
4. **The `background-attachment: fixed` iOS Safari bug is fixed on paper but only verified via desktop/emulation** — this exact failure mode already happened once in this codebase (the bug shipped once and only a real iPhone caught it; both `site.css` and `mobile.css` still carry the broken rule today). Any fix for #116 must be verified on a real iOS Safari session (physical device or real-device cloud), not devtools "iPhone" emulation, and the verification method/device must be recorded explicitly in the phase's evidence.
5. **Original/cropped image divergence on re-upload** — if a user re-uploads a new original photo but only one column gets updated, the stored cropped avatar can silently no longer correspond to the current original. Re-upload must atomically replace both columns together, never a partial update.
## Implications for Roadmap

Based on research, suggested phase structure:

### Phase 1: Dual-image schema + Repository/Domain plumbing
**Rationale:** Everything downstream (UI, controllers) depends on the new column existing; this is the lowest-risk, most mechanical piece and should land first so later phases build on a proven data path.
**Delivers:** `OriginalImageData byte[]?` column on both `CharacterImageEntity` and `DungeonMasterProfileImageEntity`, additive EF Core migration, widened `UpdateProfileImageAsync`/`UpsertProfileImageAsync` signatures, new `GetCharacterOriginalPictureAsync`/`GetOriginalPictureAsync` repository methods, updated AutoMapper profiles and Domain models.
**Addresses:** the "original + cropped dual storage" table-stakes requirement from FEATURES.md.
**Avoids:** the original/cropped divergence-on-re-upload pitfall — build the atomic-replace behavior into the repository method from the start rather than retrofitting it.

### Phase 2: Controller/ViewModel wiring (no crop UI yet)
**Rationale:** Fully testable end-to-end (e.g. via Postman with two raw file inputs) before any client-side JS exists — isolates schema/Domain bugs from JS/markup bugs, per the architecture research's suggested build order.
**Delivers:** `OriginalPictureFile` added to `CharacterViewModel`/`EditDMProfileViewModel`; `Create`/`Edit`/`EditProfile` POST actions read and forward both files; new `GetOriginalPicture` actions; new `IImageValidationService` in Domain to de-duplicate the existing 3x inline validation.
**Uses:** existing `IFormFile`/`CopyToAsync` pattern, called twice instead of once.
**Implements:** Pattern 2 from ARCHITECTURE.md (validation in Domain, byte[] I/O in Repository only).

### Phase 3: Client-side crop UI
**Rationale:** The riskiest, most novel piece — deferred until the data path underneath it is already proven, so any integration issues are isolated to JS/markup.
**Delivers:** Vendored Cropper.js v2.1.1 (`wwwroot/lib/cropperjs/`), shared `image-crop.js` wiring the crop modal into all three affected forms (character create/edit, DM profile edit — desktop + `.Mobile.cshtml` = 6 view files), the `DataTransfer` FileList-swap logic, EXIF orientation correction, canvas downscale-before-crop, and the character-details page pointed at the new `GetOriginalPicture` endpoint (issue #78's explicit requirement).
**Addresses:** all crop-UX table-stakes features from FEATURES.md (draggable frame, fixed aspect ratio, live preview, zoom/pan, touch support).
**Avoids:** Pitfalls 1-4 (EXIF orientation, canvas memory ceiling, touch-drag precision) — each must be verified on a real device as a named acceptance criterion, not inferred from devtools emulation.

### Phase 4: Waitlist auto-promotion notification
**Rationale:** No dependency on the crop-image work (different tables entirely — quest signup/vote vs. image storage); can be sequenced independently, before or after Phases 1-3.
**Delivers:** New trigger condition in the existing signup/promotion logic (Yes > Maybe > No, then signup time — already internally specified), a new Razor/HtmlRenderer email template, wired through the existing Hangfire job pattern.
**Addresses:** the "targeted promotion email" table-stakes requirement, explicitly excluding self-notification and broadcast-to-all-waitlisted anti-patterns.

### Phase 5: iOS Safari background-attachment fixed fix (#116)
**Rationale:** Independent, small, CSS-only fix; no dependency on the other three items. Sequence last only because it's lowest-effort/lowest-risk, not because of any technical ordering constraint — could equally run in parallel with Phase 4.
**Delivers:** Replace the broken `background-attachment: fixed` rule in both `site.css` and `mobile.css` with a `position: fixed` pseudo-element/layered-div approach.
**Avoids:** Pitfall 5 — must be verified on a real iOS Safari session (physical device or real-device cloud), with the verification device/method recorded explicitly, since this exact bug class already escaped desktop/emulation testing once in this codebase.

### Phase Ordering Rationale

- Schema-first ordering (Phase 1 -> 2 -> 3) is a hard dependency chain: controllers need the column to exist, and the crop UI needs a working two-file POST path to submit into. This isn't a preference — it's the only order that lets each phase be tested in isolation.
- Waitlist promotion (Phase 4) and the CSS fix (Phase 5) are fully independent of the crop work and of each other — they touch unrelated tables/files and can be resequenced or parallelized freely without any coordination cost.
- Deferring the crop UI (Phase 3) to after the data-plumbing phases specifically isolates the riskiest, least-precedented work (a new client-side library, real-device-only bug classes) so failures there don't get conflated with schema or Domain bugs.

### Research Flags

Phases likely needing deeper research during planning:
- **Phase 3 (Client-side crop UI):** Needs a `--research-phase` pass specifically for the EXIF-orientation-correction snippet (no npm package is being introduced, so the ~40-line vendored parser needs to be sourced/verified carefully) and for the canvas-downscale-before-crop implementation details — both are narrow, easy-to-get-subtly-wrong pieces flagged as HIGH-severity pitfalls.

Phases with standard patterns (skip research-phase):
- **Phase 1 (schema/Repository/Domain plumbing):** Standard EF Core additive-migration pattern, already well-precedented in this exact codebase (the prior `MoveCharacterImagesToSeparateTable` migration establishes the convention being extended).
- **Phase 2 (controller/ViewModel wiring):** Mechanical widening of an existing, well-understood `IFormFile` pattern already used 3x in this codebase.
- **Phase 4 (waitlist promotion email):** Reuses an infrastructure pattern already proven 6+ times in this codebase (Hangfire + Razor/HtmlRenderer + Resend); only the trigger-condition logic is new, and it's already fully specified.
- **Phase 5 (CSS fix):** The fix pattern itself (pseudo-element/layered-div replacing `background-attachment: fixed`) is well-documented and cross-verified across multiple independent sources; the only non-standard requirement is mandatory real-device verification, which is a process step, not a research gap.

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| Stack | MEDIUM | Web-search-derived (no Context7/official-docs MCP available this session); versions cross-checked against npm/NuGet registry pages directly. The core "no server-side image library needed" conclusion is HIGH confidence since it's corroborated independently by all four researchers plus direct codebase inspection. |
| Features | MEDIUM | Cross-checked web search across independent queries converging on the same conclusions, but no single official-vendor-doc-tier source reached for either the crop UX or waitlist UX sub-topic. Pattern-level conclusions (fixed aspect ratio + preview + zoom as baseline; targeted-not-broadcast notification) are reliable — corroborated by 3+ independent sources each. |
| Architecture | HIGH for codebase integration points (verified by direct file reads of `CharacterImageEntity.cs`, `CharacterRepository.cs`, controllers, ViewModels, existing views) / MEDIUM for the crop-library API-choice research (cross-verified across two independent doc fetches). |
| Pitfalls | HIGH — grounded in this codebase's actual source (confirmed the `background-attachment: fixed` bug still exists in both `site.css` and `mobile.css`; confirmed the no-bundler `site.js` convention; confirmed PROJECT.md's own prior SkiaSharp-pause decision) cross-checked against current sourced findings on SkiaSharp Linux deployment, canvas EXIF handling, and iOS Safari devtools-emulation gaps. |

**Overall confidence:** MEDIUM-HIGH — the architectural and pitfall findings (the parts that most affect implementation correctness and risk) are grounded in direct codebase evidence; the stack/feature findings rely on web search but converge strongly and consistently across independent sources and researchers.

### Gaps to Address

- **EXIF-orientation-correction snippet sourcing:** No specific vendored library/snippet was pinned down to a specific, audited source — flag this for a focused look during Phase 3 planning (options mentioned: exif-js-style snippet, blueimp-load-image-style snippet, or a hand-rolled ~40-line reader).
- **`wwwroot/lib/` convention confirmation:** STACK.md flagged that the existing convention for vendoring third-party JS/CSS (matching how Bootstrap/FontAwesome are likely already handled) should be verified against `_Layout.cshtml`'s existing `<script>`/`<link>` tags before implementation — not yet directly confirmed in this research pass.
- **Real-device/device-cloud access:** Multiple pitfalls (EXIF, canvas memory, touch-drag, the CSS fix itself) require verification on a physical iPhone or a real-device-cloud service (e.g. BrowserStack). Confirm what device/service access is actually available to the team before phases are scheduled — this is a process/tooling gap, not a research gap, but it blocks the verification step for at least 4 of the 5 suggested phases.
- **Server-side re-validation depth for the two posted images:** PITFALLS.md raises re-validating uploaded bytes via magic-byte content-sniffing (not just posted MIME type) as a security consideration; since no server-side image-decoding library is being introduced, decide during Phase 2 planning exactly how deep this validation goes without pulling in a decoding dependency (e.g. magic-byte check only, vs. skip entirely given this is a small trusted-group internal tool).

## Sources

### Primary (HIGH confidence)
- Direct codebase reads: `QuestBoard.Repository/Entities/CharacterImageEntity.cs`, `DungeonMasterProfileImageEntity.cs`, `CharacterRepository.cs`, `DungeonMasterProfileRepository.cs`, `QuestBoard.Service/Controllers/Characters/GuildMembersController.cs`, `DungeonMaster/DungeonMasterController.cs`, `ViewModels/CharacterViewModels/CharacterViewModel.cs`, `Views/GuildMembers/Edit.cshtml`, `wwwroot/css/site.css` / `mobile.css`, `wwwroot/js/site.js`
- `.planning/PROJECT.md` — Key Decisions log (SkiaSharp pause rationale, issue #78/#104/#116 scope)
- `.planning/codebase/ARCHITECTURE.md`, `.planning/codebase/CONCERNS.md`

### Secondary (MEDIUM confidence)
- https://www.npmjs.com/package/cropperjs and https://fengyuanchen.github.io/cropperjs/v1/ and /v2/guide.html — version/API cross-verification for both Cropper.js major versions
- https://www.nuget.org/packages/SixLabors.ImageSharp, SkiaSharp, SkiaSharp.NativeAssets.Linux.NoDependencies — package/dependency details (informative for the "what we're deliberately not using" record)
- https://anthonysimmon.com/benchmarking-dotnet-libraries-for-image-resizing/ — image library comparison
- https://pqina.nl/blog/total-canvas-memory-use-exceeds-the-maximum-limit/ and WebKit Bugzilla #195325 — iOS Safari canvas memory/pixel-area ceiling
- https://github.com/Foliotek/Croppie/issues/31 and https://github.com/fengyuanchen/cropper/issues/120 — EXIF-orientation bug corroborated across two independent crop libraries
- https://juand89.hashnode.dev/troubleshooting-background-attachment-fixed-bug-in-ios-safari and css-tricks.com — pure-CSS fix pattern for the iOS background-attachment fixed bug
- Waitlist UX sources (DICE waitlist UX deep-dive, Waitlist Me, WaitlistCare) — targeted-not-broadcast notification pattern

### Tertiary (LOW confidence)
- Consumer avatar-cropper marketing pages (avatarcropper.org, Pokecut, ToolPoint) — used only to corroborate "fixed aspect ratio + zoom + live preview" as a cross-product baseline pattern, not for any implementation detail

---
*Research completed: 2026-07-04*
*Ready for roadmap: yes*
