---
phase: 46-client-side-crop-ui
verified: 2026-07-07T20:31:00Z
status: human_needed
score: 3/4 must-haves verified
behavior_unverified: 0
overrides_applied: 0
overrides:
  - must_have: "Roadmap Success Criterion #3 literal text: 'the character details page and DM profile details page display the original, unmodified image'"
    reason: "D-03 (recorded in 46-CONTEXT.md, discussed and confirmed interactively with the user during discuss-phase) explicitly supersedes REQUIREMENTS.md's literal IMAGE-04 wording: the DM Profile page shows the cropped image, not the original, because no DM-facing page needs the original and DM's existing circular-crop presentation is unaffected either way. Character Details and Contact Details remain the only two original-only pages, exactly as implemented. This is a scope-narrowing decision made with the user's explicit sign-off during planning, not an executor shortcut, and is called out three separate times in 46-CONTEXT.md as intentionally overriding the roadmap/requirement text."
    accepted_by: "user (via 46-CONTEXT.md discuss-phase transcript)"
    accepted_at: "2026-07-07 (discuss-phase, prior to execution)"
human_verification:
  - test: "Confirm the DM-profile-shows-cropped (not original) deviation from ROADMAP.md Success Criterion #3's literal text is an acceptable permanent product decision, not something that should be corrected before this phase is considered closed."
    expected: "Human either (a) confirms D-03 as the final intended behavior and the roadmap/requirements wording should be updated to match, or (b) determines DM Profile should in fact show the original and requests a follow-up fix."
    why_human: "This is a product-scope decision already made once during discuss-phase (documented in 46-CONTEXT.md), but it directly contradicts the literal text of both ROADMAP.md's Success Criterion #3 and REQUIREMENTS.md's IMAGE-04 description, both of which remain unedited. A verifier cannot unilaterally decide whether the roadmap text or the shipped behavior is the one that should change."
  - test: "On a real touchscreen device, confirm the crop frame responds correctly to drag and pinch gestures."
    expected: "Smooth, precise response to touch gestures with no missed input."
    why_human: "Explicitly deferred per user decision at 46-07's device-access checkpoint (device access was not available). Documented as a known, tracked gap in 46-07-SUMMARY.md's 'Next Phase Readiness' section, not a silent omission."
  - test: "Upload a real phone-camera portrait photo (with EXIF orientation metadata) through any of the 6 crop-enabled forms on a real iOS Safari device; confirm it saves and displays right-side-up."
    expected: "Photo displays with correct orientation everywhere the cropped/original image is shown."
    why_human: "Same deferred real-device checkpoint as above. `createImageBitmap({imageOrientation:'from-image'})` is implemented and unit-testable in isolation, but EXIF-bearing camera photos and iOS Safari's specific orientation handling cannot be produced or verified from a desktop/CI environment."
  - test: "Upload a full-resolution (12MP+) camera photo through the crop flow on real iOS Safari; confirm the crop canvas does not crash or go blank."
    expected: "Canvas renders and remains interactive; no crash, no blank canvas."
    why_human: "iOS Safari's ~16.7M-pixel canvas memory ceiling is invisible in desktop Chrome or devtools mobile emulation. The 2400px downscale-before-crop mitigation is implemented in code (`prepareImageForCropper`, `image-crop.js:7-41`) but its real-device effectiveness is unverified per the same deferred checkpoint."
---

# Phase 46: Client-Side Crop UI Verification Report

**Phase Goal:** Users see and control exactly how their character and DM profile photos are framed before saving, and the rest of the app shows the right version (cropped vs. original) in the right place
**Verified:** 2026-07-07T20:31:00Z
**Status:** human_needed
**Re-verification:** No — initial verification

## Goal Achievement

### Observable Truths

| # | Truth (Roadmap Success Criterion) | Status | Evidence |
|---|---|---|---|
| 1 | On every image-upload field (character photo, DM profile photo — create and edit, desktop and mobile), the user sees an interactive crop frame (Cropper.js v2.1.1) they can drag, resize, and zoom over their photo before saving | ✓ VERIFIED | All 10 upload views (`Characters/Create(.Mobile)`, `Characters/Edit(.Mobile)`, `Contacts/Create(.Mobile)`, `Contacts/Edit(.Mobile)`, `DungeonMaster/EditProfile(.Mobile)`) contain identical `cropPhotoModal` markup with `cropper-canvas`/`cropper-image`/`cropper-selection` + 8 resize handles (n/e/s/w/ne/nw/se/sw) + a move handle, Cropper.js v2.1.1 pinned via CDN with SRI hash, and an `initImageCrop(...)` call. The canvas-level pan bug found during live desktop verification (46-07) is fixed in code (`cropper-handle action="move"` at e.g. `Characters/Create.cshtml:159`, not the buggy `action="select"` originally shipped). |
| 2 | Saving a photo submits both the original and the cropped result in one ordinary form submission, with no separate upload step or page reload | ✓ VERIFIED | `image-crop.js`'s `setCroppedFileInput` populates a hidden `CroppedPictureFile` file input via `DataTransfer` immediately on modal-shown (before any user interaction, per D-04) and again on "Use This Crop." All three controllers (`CharactersController`, `ContactsController`, `DungeonMasterController`) read `viewModel.CroppedPictureFile` in the same POST action as the original photo, call `imageValidationService.ValidateImagePair`, and persist both via the widened 4-arg `UpdateAsync`/`UpsertProfileAsync` (Edit) or the new 3-arg `AddAsync` (Create, closed by Plan 46-08). No AJAX/separate endpoint involved — confirmed by grep: no `fetch`/`XMLHttpRequest` calls in `image-crop.js`. |
| 3 | The guild-member list page displays the cropped image for each character; the character details page and DM profile details page display the original, unmodified image | ⚠️ DEVIATION (see override + human verification) | Characters/Contacts index, Quest Details/Manage/_QuestCard, and QuestLog Details all repointed to `GetCroppedPicture`/`GetCroppedContactImage` (verified via grep across 9 view files). Character Details and Contact Details deliberately left on `GetProfilePicture`/`GetContactImage` (original), confirmed unchanged. **However, `DungeonMaster/Profile.cshtml` (the DM's "details" page) calls `GetDMProfilePicture`, which serves `CroppedImageData ?? OriginalImageData` — i.e. the cropped image, not the original** — directly contradicting this Success Criterion's literal text. This is a documented, user-confirmed decision (D-03 in `46-CONTEXT.md`), not an oversight; see override entry below. |
| 4 | On a real touchscreen device, the crop frame responds correctly to drag and pinch gestures, a real phone-camera photo crops with correct orientation, and a full-resolution camera photo does not crash or blank the crop canvas on iOS Safari — each verified on a real device | ? UNCERTAIN — explicitly deferred | Device access was confirmed unavailable at 46-07's Task 1 checkpoint. Desktop-equivalent code paths exist (`createImageBitmap({imageOrientation:'from-image'})` for EXIF, 2400px downscale for the canvas-memory ceiling) and are unit-exercised, but real-device behavior is unverified. Documented as an explicit, tracked gap in `46-07-SUMMARY.md`'s "Next Phase Readiness" section — not a silent omission. Routed to human verification below. |

**Score:** 3/4 truths verified as literally stated (Truth 3 verified against the *implemented and user-approved* D-03 rule, not against the roadmap's literal original text — flagged, not silently passed)

### Required Artifacts

| Artifact | Expected | Status | Details |
|---|---|---|---|
| `QuestBoard.Domain/Services/CharacterService.cs` | 4-arg `UpdateAsync` + 3-arg `AddAsync` with crop resolution | ✓ VERIFIED | Three-branch crop resolution present (lines 79-106); `AddAsync` overload present (lines 109-120) and called from `CharactersController.Create` |
| `QuestBoard.Domain/Services/ContactService.cs` | Same shape as CharacterService | ✓ VERIFIED | Confirmed via `contactService.AddAsync(contact, croppedImageData, token)` call in `ContactsController.cs:145` |
| `QuestBoard.Domain/Services/DungeonMasterProfileService.cs` | `UpsertProfileAsync` widened with `newCroppedImageData` | ✓ VERIFIED | `DungeonMasterController.cs:145` passes `newCroppedImageData: newCroppedImageData` |
| `QuestBoard.Service/ViewModels/CharacterViewModels/CharacterViewModel.cs` | `CroppedPictureFile IFormFile` property | ✓ VERIFIED | Line 43 |
| `QuestBoard.Service/wwwroot/css/characters.css`, `contacts.css` | 1:1 square aspect-ratio list-card box, no fixed mobile height override | ✓ VERIFIED | `aspect-ratio: 1 / 1; height: auto;` present; `@media (max-width: 768px)` block contains no `.character-image`/`.contact-image` height override |
| `QuestBoard.Service/Controllers/Characters/CharactersController.cs` | `GetCroppedPicture` action + dual-file Create/Edit wiring | ✓ VERIFIED | Line 409; `ValidateImagePair` called in both Create (143) and Edit (284) POSTs |
| `QuestBoard.Service/Controllers/Contacts/ContactsController.cs` | `GetCroppedContactImage` action with `IsVisibleTo` gate | ✓ VERIFIED | Line 382-397, gate at line 394 mirrors `GetContactImage`'s gate at line 367 |
| `QuestBoard.Service/Controllers/DungeonMaster/DungeonMasterController.cs` | `GetDMProfilePicture` repointed to cropped-or-fallback read | ✓ VERIFIED | Line 155: `dmProfileService.GetCroppedPictureAsync(id, token)` |
| `QuestBoard.Service/wwwroot/js/image-crop.js` | `initImageCrop` + EXIF/downscale + `$toCanvas` extraction + `DataTransfer` population | ✓ VERIFIED | 270 lines (min 60 required); all named functions present and wired |
| `QuestBoard.Service/wwwroot/css/image-crop.css` | Crop-stage modal layout | ✓ VERIFIED | Included once globally via `_Layout.cshtml:22`, not duplicated in Mobile views (WR-04 fix confirmed) |

### Key Link Verification

| From | To | Via | Status | Details |
|---|---|---|---|---|
| `CharacterService.UpdateAsync` (4-arg) | `repository.UpdateWithProfileImageAsync` | resolved `croppedImageData` argument | ✓ WIRED | Line 105 |
| `DungeonMasterProfileService.UpsertProfileAsync` | `repository.UpdateBioWithProfileImageAsync` | `newCroppedImageData` argument | ✓ WIRED | Confirmed via controller call chain |
| `CharactersController.GetCroppedPicture` | `characterService.GetCharacterCroppedPictureAsync` | read action returns cropped-or-fallback bytes | ✓ WIRED | Confirmed |
| `ContactsController.GetCroppedContactImage` | `IsVisibleTo(contact, ...)` | visibility gate replicated | ✓ WIRED | Line 394, identical gate logic to sibling original-read action |
| Create/Edit POST | `service.UpdateAsync`/`AddAsync(..., newCroppedImageData, ...)` | widened call with real cropped byte[] | ✓ WIRED | Confirmed in all 3 controllers, both Create and Edit paths |
| read-only avatar `<img>` src | `GetCroppedPicture` / `GetCroppedContactImage` | `Url.Action` name change | ✓ WIRED | Confirmed across Characters/Contacts Index (+Mobile), Quest Details/Manage/_QuestCard, QuestLog Details (+Mobile) — 9 files, 11 occurrences |
| file input `change` | `initImageCrop(...)` in `@section Scripts` | `image-crop.js` loaded per view | ✓ WIRED | Confirmed in all 10 upload views |
| hidden `CroppedPictureFile` input | `ViewModel.CroppedPictureFile` binding | `name=CroppedPictureFile` | ✓ WIRED | Confirmed |
| current-photo preview src | `GetCroppedPicture`/`GetCroppedContactImage`/`GetDMProfilePicture` | preview repoint per D-03 | ✓ WIRED | Confirmed |

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
|---|---|---|---|
| Solution builds clean | `dotnet build --nologo -v quiet` | 0 Warning(s), 0 Error(s) | ✓ PASS |
| Full unit test suite | `dotnet test` (QuestBoard.UnitTests) | 235/235 passed | ✓ PASS |
| Full integration test suite | `dotnet test` (QuestBoard.IntegrationTests) | 372/372 passed | ✓ PASS |
| `GetCroppedPicture`/`GetCroppedContactImage` integration coverage exists | grep for test methods | `GetCroppedPicture_CropStored_ReturnsOkWithContent`, `GetCroppedContactImage_HiddenContact_PlayerGetsNotFound`, `GetCroppedContactImage_VisibleContact_ReturnsOkWithContent` all present | ✓ PASS |
| Interactive crop UI (drag/resize/zoom, real browser) | N/A — requires running app + browser | Not re-run by this verifier (already live-verified per 46-07-SUMMARY.md with two real bugs found and fixed, both confirmed present in code above) | ? SKIP (previously human-verified, code confirmed to match) |

Independently confirmed the claimed "235 unit + 372 integration = 607/607" test count matches exactly — not just trusted from SUMMARY.md.

### Requirements Coverage

| Requirement | Source Plan(s) | Description | Status | Evidence |
|---|---|---|---|---|
| IMAGE-01 | 46-01, 46-05, 46-06, 46-07, 46-08 | User can interactively drag/resize/zoom a crop frame (Cropper.js v2.1.1) over an uploaded photo before saving | ✓ SATISFIED | Crop modal wired into all 10 views; pan/selection bugs found and fixed during live verification; confirmed present in current code |
| IMAGE-04 | 46-02, 46-03, 46-04 | Guild-member list page displays the cropped image; character/DM details pages display the original | ⚠️ SATISFIED WITH DOCUMENTED DEVIATION | Character/Contact Details show original as required. DM "details" page (Profile.cshtml) shows cropped, not original — deliberate D-03 override, confirmed in code and in `46-CONTEXT.md`. REQUIREMENTS.md's IMAGE-04 text itself is not updated to reflect this narrowing. |
| IMAGE-05 | 46-01, 46-03, 46-06, 46-08 | Crop UI applies to every image-upload field in the app (character photo, DM profile photo) | ✓ SATISFIED | All 3 upload surfaces (Character, Contact, DM) × Create/Edit × desktop/mobile = 10 views, all confirmed wired |

No orphaned requirements — REQUIREMENTS.md maps exactly IMAGE-01, IMAGE-04, IMAGE-05 to Phase 46, and all three appear in at least one plan's `requirements:` frontmatter.

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|---|---|---|---|---|
| — | — | No `TBD`/`FIXME`/`XXX`/`TODO`/`HACK` debt markers found in any phase-modified file | — | — |
| — | — | `character-placeholder`/`contact-placeholder` CSS class name matches are legitimate "no photo" UI classes, not stub markers | info | none |

All 5 code-review warnings (WR-01 through WR-05) from `46-REVIEW.md` are confirmed fixed in the current codebase (commit `e7b51eb`, verified directly, not trusted from `46-REVIEW-FIX.md`'s claims):
- WR-01 (GIF animation silently collapsed to static JPEG): fixed — `image-crop.js:191-199` skips the crop flow entirely for `image/gif`
- WR-02 (extension/format mismatch): fixed — `setCroppedFileInput` now always emits `.jpg`
- WR-03 (validation logic duplicated across 10 views): fixed — consolidated into `initImageCrop`'s `maxFileSizeBytes`/`allowedTypes` config
- WR-04 (duplicate CSS include on Mobile views): fixed — confirmed zero occurrences of a per-view `image-crop.css` link in the 5 Mobile views
- WR-05 (dead `id="croppedPictureFileInput"` attribute): fixed — confirmed zero occurrences across all 10 views

The 3 Info findings (IN-01, IN-02, IN-03) were left unaddressed per the reviewer's own "no action needed" / "out of scope" conclusion — not gaps against this phase's must-haves.

## Deviations from Roadmap Contract

**D-03 (DM Profile shows cropped, not original) directly contradicts the literal text of ROADMAP.md's Phase 46 Success Criterion #3** ("...the character details page and DM profile details page display the original, unmodified image") and REQUIREMENTS.md's IMAGE-04 wording ("...character/DM details pages display the original"). Both documents remain unedited to reflect this.

This was not an executor shortcut — it is documented three times in `46-CONTEXT.md` as a decision reached interactively with the user during discuss-phase, with an explicit statement that "the user confirmed DM Profile should show the cropped image, not the original." The implementation faithfully follows D-03, and D-03 is faithfully followed by every downstream plan (46-03, 46-04, 46-06 all reference D-03 by name when repointing DM's endpoint).

Because this is a genuine discrepancy between the roadmap contract text and delivered behavior — even though it traces to a documented decision — it is surfaced here as a human-verification item rather than silently accepted, so a human can either (a) confirm this is correct and update ROADMAP.md/REQUIREMENTS.md wording to match, or (b) determine the roadmap's literal wording should have been followed and request a fix.

## Human Verification Required

See frontmatter `human_verification` block for the four items:
1. D-03 DM-Profile-shows-cropped deviation from literal roadmap/requirement wording (needs a scope decision, not a bug fix)
2. Real touchscreen drag/pinch gesture response (deferred, device access unavailable)
3. Real phone-camera EXIF orientation correctness (deferred, device access unavailable)
4. Real iOS Safari full-resolution photo canvas-memory ceiling (deferred, device access unavailable)

Items 2-4 are a known, explicitly tracked gap (Success Criterion #4), not a silent omission — confirmed documented in `46-07-SUMMARY.md`'s "Next Phase Readiness" section and in this phase's own `must_haves.truths` for Plan 46-07 ("Real-device access is explicitly confirmed with the user before any device verification is attempted").

## Gaps Summary

No code-level gaps (missing/stub artifacts, broken wiring, unresolved debt markers) were found. All 8 plans' must-haves are backed by code that actually exists, is substantive, and is wired end-to-end; the full test suite (607/607) passes independently-verified, and the 5 code-review warnings are confirmed fixed in the current tree.

The phase's only open item is a genuine tension between the roadmap's literal Success Criterion #3 text and the implemented (and user-approved-during-planning) D-03 display rule for DM Profile. This is not a "gap" in the sense of missing work — the DM repoint is intentional, complete, and consistent everywhere it's used — but it is a discrepancy against the ROADMAP.md contract as currently worded, which this verifier cannot resolve unilaterally. Routed to human verification rather than either a silent PASS (would hide the roadmap/implementation mismatch) or a blocking FAIL (would contradict the documented, already-made product decision).

Success Criterion #4 (real-device checks) remains an explicitly deferred, tracked gap per prior user decision — not newly discovered by this verification.

---

*Verified: 2026-07-07T20:31:00Z*
*Verifier: Claude (gsd-verifier)*
