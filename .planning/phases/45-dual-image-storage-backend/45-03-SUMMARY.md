---
phase: 45-dual-image-storage-backend
plan: 03
subsystem: image-upload
tags: [validation, ef-core, tdd, controllers, security]

# Dependency graph
requires:
  - phase: 45-02
    provides: "ICharacterService/IContactService.UpdateAsync(model, hasNewOriginalUpload, token) overload; renamed original-read service methods; DungeonMasterProfileService.UpsertProfileAsync's existing imageBytes signal"
provides:
  - "Shared QuestBoard.Domain.Interfaces.IImageValidationService / ImageValidationService validating an original+optional-cropped image pair (MIME allowlist, extension allowlist, 5 MB size limit)"
  - "All five image-upload controller actions (Characters Create/Edit, Contacts Create/Edit, DungeonMaster EditProfile) route through the shared validator instead of duplicated inline blocks"
  - "CharactersController.Edit and ContactsController.Edit call the new hasNewOriginalUpload-aware UpdateAsync overload, making Plan 02's clear-stale-crop branch reachable in production"
  - "Controller-level integration regression coverage proving the clear-stale-crop / preserve-stored-crop behavior through the real Edit POST action path"
affects: [46-client-side-crop-ui]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Primitive ImageFileInput record (Length/ContentType/FileName/FieldName) passed into the validator instead of IFormFile -- keeps the validator trivially unit-testable with no upload fakes and keeps the Domain layer's dependency on ASP.NET Core upload types unnecessary for this service, even though QuestBoard.Domain.csproj does have the FrameworkReference available"
    - "Hoisted single hasNewOriginalUpload local reused both to gate the CopyToAsync byte-extraction and to pass into the new service overload, so the two checks can never independently drift apart"
    - "MultipartFormDataContent (not FormUrlEncodedContent) in integration tests to exercise a real file-upload Edit POST end-to-end"

key-files:
  created:
    - QuestBoard.UnitTests/Services/ImageValidationServiceTests.cs
    - QuestBoard.Domain/Interfaces/IImageValidationService.cs
    - QuestBoard.Domain/Services/ImageValidationService.cs
  modified:
    - QuestBoard.Domain/Extensions/ServiceExtensions.cs
    - QuestBoard.Service/Controllers/Characters/CharactersController.cs
    - QuestBoard.Service/Controllers/Contacts/ContactsController.cs
    - QuestBoard.Service/Controllers/DungeonMaster/DungeonMasterController.cs
    - QuestBoard.IntegrationTests/Controllers/CharactersControllerIntegrationTests.cs
    - QuestBoard.IntegrationTests/Controllers/ContactsControllerIntegrationTests.cs

key-decisions:
  - "IImageValidationService.ValidateImagePair takes primitive ImageFileInput records, not IFormFile, per the plan's locked interface-parameter decision -- keeps the Domain service unit-testable without constructing upload fakes."
  - "ValidateSingle returns after the first failing check (MIME, then extension, then size) rather than accumulating all three, so a file failing multiple rules produces exactly one error per field -- matches the plan's 'exactly one error' acceptance criteria."
  - "hasNewOriginalUpload is computed once as a hoisted local in each Edit POST, reused both to gate the existing CopyToAsync block and as the new service-call argument, per the plan's explicit anti-drift requirement."
  - "DungeonMasterController.EditProfile's service call (UpsertProfileAsync) was left completely unchanged -- only its upload path now routes through the shared validator, gaining MIME/extension checks it previously lacked (it only checked size)."

requirements-completed: [IMAGE-02, IMAGE-03]

# Metrics
duration: 35min
completed: 2026-07-07
---

# Phase 45 Plan 03: Consolidate Upload Validation and Wire New-Upload Signal Summary

**A single shared `IImageValidationService` replaces five duplicated inline validation blocks across three controllers, and the Character/Contact Edit POST actions now call Plan 02's `UpdateAsync(model, hasNewOriginalUpload, token)` overload with a hoisted, un-driftable file-present signal — proven end-to-end by new controller-level integration tests, not just Plan 02's service-level unit test.**

## Performance

- **Duration:** ~35 min
- **Started:** 2026-07-07 (Wave 3 execution)
- **Completed:** 2026-07-07
- **Tasks:** 3 (RED, GREEN validator + controller wiring, controller-level regression tests)
- **Files modified:** 9 (3 created, 6 modified)

## Accomplishments
- New `IImageValidationService`/`ImageValidationService` in the Domain layer validates an original-plus-optional-cropped image pair against the shared MIME allowlist (`image/jpeg`, `image/png`, `image/gif`), extension allowlist (`.jpg`, `.jpeg`, `.png`, `.gif`), and 5 MB size limit — copied verbatim from the previously-duplicated inline blocks, covered by 12 Theory-driven unit tests (MIME, extension, size, and the one/both/neither-file matrix)
- Registered via `services.AddScoped<IImageValidationService, ImageValidationService>()` in `ServiceExtensions.AddDomainServices`
- All five upload actions (`CharactersController` Create+Edit, `ContactsController` Create+Edit, `DungeonMasterController.EditProfile`) now build an `ImageFileInput` from the bound `IFormFile` and call `ValidateImagePair`, replacing the five inline `allowedMimeTypes`/size blocks — `DungeonMasterController.EditProfile` gains MIME/extension checks it previously lacked (it only checked size)
- `CharactersController.Edit` and `ContactsController.Edit` now hoist a single `hasNewOriginalUpload` local (the same `IFormFile != null && .Length > 0` check that already gated `CopyToAsync`) and pass it into Plan 02's new `UpdateAsync(model, hasNewOriginalUpload, token)` overload, making the clear-stale-crop branch reachable in production
- Three new controller-level integration tests (`Edit_NewOriginalPhotoUpload_ClearsStaleCroppedImage`, `Edit_NoNewPhoto_PreservesStoredCroppedImage` for Character; `Edit_NewOriginalImageUpload_ClearsStaleCroppedImage` for Contact) prove the wiring through the real `/Characters/Edit` and `/Contacts/Edit` POST actions using `MultipartFormDataContent` file uploads, not just Plan 02's isolated service-level unit test
- Full suite: 225 unit + 364 integration tests passing (222 + 364 baseline, plus 12 new unit + 3 new integration = 234 total new/changed test assertions across the plan)

## Task Commits

Each task was committed atomically:

1. **Task 1a: Write failing ImageValidationService tests (RED)** - `f3c4f3e` (test)
2. **Task 1b: Implement the shared IImageValidationService (GREEN)** - `6c9fdc4` (feat)
3. **Task 2: Route all five upload actions through the shared validator, wire hasNewOriginalUpload** - `598d9b8` (feat)
4. **Task 3: Controller-level regression tests for clear-stale-crop wiring** - `6ccdb6c` (test)

**Plan metadata:** (this commit) - `docs: complete plan`

## Files Created/Modified
- `QuestBoard.UnitTests/Services/ImageValidationServiceTests.cs` - new file, 12 `[Theory]`/`[Fact]` cases covering MIME, extension, size, and the one/both/neither-file matrix
- `QuestBoard.Domain/Interfaces/IImageValidationService.cs` - new interface + `ImageFileInput`/`ImageValidationError` records
- `QuestBoard.Domain/Services/ImageValidationService.cs` - new `internal` implementation with a private `ValidateSingle` helper structured for a possible future magic-byte check (not added this phase, per plan's scope note)
- `QuestBoard.Domain/Extensions/ServiceExtensions.cs` - `AddScoped<IImageValidationService, ImageValidationService>()` registration
- `QuestBoard.Service/Controllers/Characters/CharactersController.cs` - `IImageValidationService` constructor parameter; Create/Edit inline validation blocks replaced with `ValidateImagePair` calls; Edit POST hoists `hasNewOriginalUpload` and calls the new 3-arg `UpdateAsync` overload
- `QuestBoard.Service/Controllers/Contacts/ContactsController.cs` - same shape as Characters: constructor parameter, Create/Edit validator calls, Edit POST `hasNewOriginalUpload` wiring
- `QuestBoard.Service/Controllers/DungeonMaster/DungeonMasterController.cs` - `IImageValidationService` constructor parameter; `EditProfile`'s size-only inline block replaced with `ValidateImagePair`; `UpsertProfileAsync` call itself unchanged (DM's own `imageBytes != null` signal was already wired in Plan 02)
- `QuestBoard.IntegrationTests/Controllers/CharactersControllerIntegrationTests.cs` - added `Edit_NewOriginalPhotoUpload_ClearsStaleCroppedImage` and `Edit_NoNewPhoto_PreservesStoredCroppedImage`
- `QuestBoard.IntegrationTests/Controllers/ContactsControllerIntegrationTests.cs` - added `Edit_NewOriginalImageUpload_ClearsStaleCroppedImage`

## Decisions Made
- Followed the plan's locked interface-parameter decision: `ValidateImagePair` takes `ImageFileInput` primitives, not `IFormFile`, keeping the validator trivially testable with `new ImageValidationService()` and no DI/mocking.
- `ValidateSingle` returns immediately after the first failing check per file (MIME first, then extension, then size) so a file that fails on multiple axes still produces exactly one `ImageValidationError`, matching the plan's "exactly one error" acceptance criteria for the mixed valid-original/invalid-cropped case.
- Both Edit POST actions hoist `hasNewOriginalUpload` into a single local computed once, before the upload block, and reuse it for both the `CopyToAsync` guard and the service call argument — exactly as the plan's "NEW-UPLOAD SIGNAL WIRING" section specified, so the two checks structurally cannot diverge.
- The Contact regression test seeds the stale-crop image row directly via a `QuestBoardContext` scope (rather than through `TestDataHelper.CreateTestContactAsync`'s `imageData` parameter) so the original and cropped bytes are deliberately distinct, matching the Character test's arrange step.

## Deviations from Plan

None — plan executed exactly as written. `CharactersController.GetProfilePicture`, `ContactsController.GetContactImage`, and `DungeonMasterController.GetDMProfilePicture` already called the correct renamed original-read methods (Plan 02 had already made this minimal rename fix as its own documented deviation), so no further serving-action repoint was needed in this plan beyond confirming the call sites were already correct.

## Issues Encountered

None. All three tasks passed their `<verify>` steps on the first attempt.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- All five image-upload controller actions validate through the single shared `IImageValidationService`; no inline `allowedMimeTypes` blocks remain anywhere in `QuestBoard.Service/Controllers/`.
- The Character and Contact Edit POST actions call Plan 02's `UpdateAsync(model, hasNewOriginalUpload, token)` overload — the dual-image atomicity guarantee (a new original never coexists with a stale crop) now holds through the real controller path, not just a service-level unit test.
- `DungeonMasterController.EditProfile`'s upload path gained MIME/extension validation it was previously missing; its `UpsertProfileAsync` call and `IsTargetInActiveGroupAsync`/`Forbid()` auth checks are unchanged.
- No `.cshtml` view files were touched this plan (D-04 honored) — Phase 46 is what will introduce the crop UI and consume the `CroppedImageData` column client-side.
- No server-side image-processing library referenced anywhere in the solution (`grep -rniE "SkiaSharp|ImageSharp|Magick" --include=*.csproj .` returns zero matches).
- Phase 45 (Dual-Image Storage Backend) is now complete across all 3 plans/waves: schema (45-01), repository/service widening (45-02), and this plan's controller wiring + validation consolidation (45-03).
- No blockers for Phase 46 (Client-Side Crop UI).

## Self-Check: PASSED

- All 3 new files confirmed present on disk (ImageValidationServiceTests.cs, IImageValidationService.cs, ImageValidationService.cs)
- SUMMARY.md itself confirmed present on disk
- All 4 task commit hashes (f3c4f3e, 6c9fdc4, 598d9b8, 6ccdb6c) confirmed present in git history
