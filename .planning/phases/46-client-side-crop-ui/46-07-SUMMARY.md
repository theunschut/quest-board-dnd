---
phase: 46-client-side-crop-ui
plan: 07
subsystem: ui
tags: [cropperjs, bootstrap-modal, javascript, cache-busting]

requires:
  - phase: 46-client-side-crop-ui (Plans 01-06, 08)
    provides: dual-image storage backend, crop pipeline, crop-modal wiring across all 10 upload views
provides:
  - Live-verified desktop crop flow (pan, zoom, default-crop centering, D-03 display rule)
  - Two real bugs found and fixed during verification (not part of the original plan set)
affects: [image-crop.js, all 10 crop-modal views]

tech-stack:
  added: []
  patterns:
    - "Re-run Cropper.js v2 auto-fit/auto-size calls after confirming a Bootstrap modal is actually visible (shown.bs.modal), not immediately after modal.show() returns"
    - "asp-append-version=\"true\" required on every script tag that may need a mid-milestone hotfix"

key-files:
  modified:
    - QuestBoard.Service/wwwroot/js/image-crop.js
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
    - .planning/phases/46-client-side-crop-ui/46-UI-SPEC.md

key-decisions:
  - "Real-device access deferred (user chose 'not available yet' at Task 1's blocking checkpoint) -- Task 3 (iOS Safari EXIF/canvas-memory/touch checks) skipped, recorded as an outstanding item"
  - "Desktop verification (Task 2) surfaced two real bugs live, both fixed and re-verified in this same plan rather than deferred, since they blocked the feature from being usable at all"
  - "Root-caused both bugs by pulling the actual cropperjs@2.1.1 UMD source locally and reading CropperImage/CropperSelection/CropperCanvas's real event-dispatch logic directly, rather than trusting WebFetch's lossy AI-summarized read of the library's docs site (which gave an actively wrong answer on the first bug)"

patterns-established:
  - "For Bootstrap-modal-hosted Cropper.js v2 instances: never trust the library's own initial-coverage/$center('contain') auto-sizing -- always re-trigger it from a shown.bs.modal-gated callback"
  - "Any wwwroot script that isn't guaranteed byte-stable for the life of the project needs asp-append-version=\"true\""

requirements-completed: [IMAGE-01, IMAGE-04, IMAGE-05]

duration: ~90min
completed: 2026-07-07
status: complete
---

# Phase 46 Plan 07: Real-Device Verification (Desktop-Only) Summary

**Desktop crop flow live-verified end-to-end after fixing two real Cropper.js v2 integration bugs found during verification; real-device (iOS/touch) checks deferred per user decision.**

## Performance

- **Duration:** ~90 min (includes live debugging, not just the plan's own scope)
- **Tasks:** 3 (1 device-access decision, 1 desktop verification, 1 real-device check — skipped)
- **Files modified:** 12 (1 JS file, 10 view script tags, 1 UI-SPEC.md)

## Accomplishments
- Task 1 (device-access decision) resolved: user selected "not available yet" — real-device checks deferred, not blocking the rest of the phase.
- Task 2 (desktop verification) approved by the user after two live-discovered bugs were fixed:
  1. **Crop-image pan was non-functional.** The canvas-level `<cropper-handle action="select" plain>` (copied verbatim from Cropper.js's own official demo markup) intercepts every canvas-background drag and always redraws/repositions the selection, so `cropper-image[translatable]`'s pan branch never fires. Combined with unbounded wheel-zoom (no min/max scale, no bounds-checking anywhere in the library), a user could zoom the image out of the visible crop stage with no way to drag it back. Fixed by changing the handle's `action` to `"move"` instead — verified directly against the official `cropperjs` GitHub repo's `docs/guide.md`, which shows this exact pattern for a pannable image, and against the actual `CropperImage.$handleAction`/`CropperSelection.$handleAction` dispatch logic in `cropperjs@2.1.1`'s source.
  2. **Crop selection was stuck at a zero-size box in the top-left corner** (and the image itself displayed unscaled at native pixel size, overflowing the crop stage). Root cause: both `CropperSelection`'s one-time `initial-coverage` sizing (fires in `connectedCallback()`, the instant the still-hidden Bootstrap modal first parses into the DOM) and `CropperImage`'s automatic `$center('contain')` (fires on the `<img>` load event, which usually completes before Bootstrap's deferred `display:block` actually applies) run against a canvas that reports `0` width/height. Fixed by explicitly re-running `cropperImageEl.$center('contain')` and `cropperSelectionEl.$initSelection(true, true)` once the modal is confirmed visible (immediately if already open, otherwise via a one-time `shown.bs.modal` listener).
- Separately discovered and fixed: none of the 10 `image-crop.js` `<script>` tags had `asp-append-version="true"` (unlike `site.js`, which already uses it everywhere) — meaning a stale cached copy of the script could keep serving indefinitely across deploys. This is what made bug #2 so difficult to reproduce reliably during live verification (a `window.location.reload()` was not enough to guarantee the current code was actually running). Added to all 10 tags.
- Full regression suite green throughout: 235 unit + 372 integration = 607/607, 0 build warnings/errors, after every fix.

## Task Commits

1. **Task 1: device-access decision** — no commit (pure conversational checkpoint, `device-deferred` selected)
2. **Task 2: desktop verification + live bug fixes**:
   - `501acd6` fix(46-06): remove canvas-level select handle blocking crop-image pan — *later found incomplete, see below*
   - `8958dcb` fix(46-06): fix crop-selection sizing and cache-busting on image-crop.js — corrects `501acd6`'s handle fix (should have changed `action` to `"move"`, not removed the handle entirely) and fixes the selection-timing bug plus the cache-busting gap
3. **Task 3: real-device checks** — skipped (device-deferred)

Note: `501acd6` was a genuine deviation caught mid-flight by the user's own live re-test ("The image pan doesn't work still... This did not really change any behaviour"), which is why a second, corrective commit followed rather than a clean single fix.

## Files Created/Modified
- `QuestBoard.Service/wwwroot/js/image-crop.js` — added `shown.bs.modal`-gated re-fit/re-center logic for both the image and the selection
- 10× crop-modal view files — canvas-level handle `action="select"` → `action="move"`; added `asp-append-version="true"` to the `image-crop.js` script tag
- `.planning/phases/46-client-side-crop-ui/46-UI-SPEC.md` — corrected the crop-modal reference markup and Trigger Interaction section to match the real, verified behavior (the original spec's step 4 claiming Cropper.js "auto-applies" the default crop with "no additional JS needed" was factually wrong)

## Decisions Made
- Fixed both live-discovered bugs in this same plan rather than filing them as follow-up work, since either one alone made the shipped crop feature non-functional (pan) or actively broken-looking (selection stuck in a corner) — not a polish issue.
- Diagnosed via direct inspection of the actual pinned `cropperjs@2.1.1` source (downloaded locally and read directly) rather than continuing to trust WebFetch's AI-summarized reads of the library's JS-rendered docs site, after the first summarized answer turned out to be wrong and cost a full incorrect-fix round-trip.

## Deviations from Plan

### Auto-fixed Issues

**1. [Live bug, found during Task 2] Crop-image pan non-functional**
- **Found during:** Task 2 (desktop verification) — user reported: "the image is zoomable... the image can be out of bounds of the modal... i cant drag it to the correct location"
- **Issue:** Canvas-level `cropper-handle[action="select"]` (copied from the official Cropper.js demo) captured every background drag for selection redraw, never letting the image's own `translatable` pan handler fire
- **Fix (round 1, wrong):** Removed the handle entirely — left a dead zone (no `action` attribute means no interaction registers there at all), user confirmed no change in behavior
- **Fix (round 2, correct):** Changed the handle's `action` to `"move"` instead of removing it, verified against the official `cropperjs` GitHub repo guide and the library's actual dispatch source
- **Files modified:** 10 crop-modal views, `46-UI-SPEC.md`
- **Verification:** User's own live re-test in their VS-hosted session confirmed panning now works
- **Committed in:** `501acd6` (wrong), corrected alongside `8958dcb`

**2. [Live bug, found during Task 2] Crop selection stuck at zero-size top-left corner; image unscaled**
- **Found during:** Task 2 (desktop verification) — user reported: "the crop is now stuck in the top left corner and i need to drag the width first, then the height"
- **Issue:** Cropper.js v2's automatic selection-sizing (`initial-coverage`) and image-centering (`$center('contain')`) both run once, too early — before the Bootstrap modal's `display:block` actually takes effect — so both compute against a `0`x`0` canvas
- **Fix:** Explicitly re-run `$center('contain')` / `$initSelection(true, true)` once the modal is confirmed visible, via a `shown.bs.modal` listener (or immediately if already open)
- **Files modified:** `image-crop.js`
- **Verification:** Reproduced directly in an authenticated browser session (user shared their login), confirmed via live DOM inspection (selection/image dimensions before and after), then re-confirmed by the user in their own VS session
- **Committed in:** `8958dcb`

**3. [Related infra gap, found while debugging #2] Missing cache-busting on image-crop.js**
- **Found during:** Debugging bug #2 — a `console.log` added to the file never appeared in the console despite multiple `window.location.reload()` calls, which was the tell that the browser was running a stale cached copy
- **Issue:** All 10 `<script src="~/js/image-crop.js">` tags lacked `asp-append-version="true"`, unlike `site.js` (which uses it everywhere) — any future edit to this file risked being silently ignored by browsers with a cached copy
- **Fix:** Added `asp-append-version="true"` to all 10 tags
- **Files modified:** 10 crop-modal views
- **Verification:** Confirmed the rendered `<script>` tag now carries a `?v=` hash query string; confirmed a plain page reload (no hard-refresh) picks up subsequent edits
- **Committed in:** `8958dcb`

---

**Total deviations:** 3 (2 live-discovered functional bugs, 1 related infra gap) — all fixed and re-verified in this plan.
**Impact on plan:** Necessary — bug #1 and #2 made the shipped crop UI non-functional; #3 is what made #2 so hard to verify reliably and is a real regression risk for any future edit to this file.

## Issues Encountered
- An initial WebFetch-based read of Cropper.js v2's source (via URL fetch + AI summarization) gave an actively incorrect explanation of the `action="select"` handle's behavior, leading to a wrong first fix for bug #1. Corrected by downloading the actual pinned `cropperjs@2.1.1` UMD bundle locally and reading the real dispatch logic directly (`CropperImage.$handleAction`, `CropperSelection.$handleAction`, `CropperCanvas.$handlePointerDown`), cross-checked against the official `cropperjs` GitHub repo's own guide markdown.
- Local `dotnet build`/`dotnet test` runs were blocked twice by file locks: once from the user's Visual Studio debugger, once from this session's own `preview_start`-launched dev server — both resolved by stopping the relevant process before retrying, per this project's own CLAUDE.md guidance.
- Live browser verification required an authenticated session; the user shared their own logged-in preview session (same browser instance the assistant's preview tools already controlled) rather than issuing separate test credentials, which let direct DOM-level reproduction and fix verification happen without a second account.

## User Setup Required
None — no external service configuration required.

## Next Phase Readiness
- Phase 46's code-complete work (Plans 01-06, 08) plus this plan's two bug fixes are all verified and committed on `milestone/v7-backlog-cleanup`.
- **Outstanding, not resolved by this plan:** Real-device verification (EXIF orientation on a real phone-camera photo, iOS Safari's ~16.7M-pixel canvas-memory ceiling with a full-resolution photo, touch-drag/pinch precision on a real touchscreen) is still pending — device access was confirmed unavailable at this session's Task 1 checkpoint. Also outstanding: confirming the target iOS version is 16+ (required for `createImageBitmap({imageOrientation:'from-image'})` support per RESEARCH.md Assumption A2 — on iOS 15 or older, EXIF correction would silently fail and the documented fallback parser would become a required follow-up).
- Phase 46's own success criterion #4 ("real-device checks pass or are explicitly recorded as outstanding") is satisfied via this deferral — phase completion should proceed, with the real-device item tracked as a known gap rather than a blocker.

---
*Phase: 46-client-side-crop-ui*
*Completed: 2026-07-07*
