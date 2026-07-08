---
phase: 46-client-side-crop-ui
plan: 01
subsystem: api
tags: [crop, image-upload, domain-services, viewmodels, ef-core]

# Dependency graph
requires:
  - phase: 45-dual-image-storage-backend
    provides: OriginalImageData/CroppedImageData dual-column storage, GetCharacterCroppedPictureAsync/GetContactCroppedImageAsync/GetCroppedPictureAsync repository reads, hasNewOriginalUpload signal threaded from controller to service
provides:
  - 4-arg ICharacterService.UpdateAsync(model, hasNewOriginalUpload, newCroppedImageData, token) with three-branch crop resolution
  - 4-arg IContactService.UpdateAsync(model, hasNewOriginalUpload, newCroppedImageData, token) with the identical three-branch resolution
  - IDungeonMasterProfileService.UpsertProfileAsync widened with an optional newCroppedImageData parameter threaded to both repository call sites
  - CroppedPictureFile validated IFormFile property on CharacterViewModel, ContactViewModel, and EditDMProfileViewModel
  - Service unit test coverage for all three crop-resolution branches (new-crop-supplied, new-original-no-crop, no-new-file re-fetch) across all three services
affects: [46-02, 46-03, 46-04, 46-05, 46-06, 46-07]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Additive overload widening: existing N-arg method delegates to a new N+1-arg overload with the new parameter defaulted to null, preserving byte-for-byte behavior for untouched callers"
    - "Three-branch crop resolution: caller-supplied crop wins > new-original-with-no-crop clears > no-new-file re-fetches and passes through the existing stored crop"

key-files:
  created: []
  modified:
    - QuestBoard.Domain/Interfaces/ICharacterService.cs
    - QuestBoard.Domain/Services/CharacterService.cs
    - QuestBoard.Domain/Interfaces/IContactService.cs
    - QuestBoard.Domain/Services/ContactService.cs
    - QuestBoard.Domain/Interfaces/IDungeonMasterProfileService.cs
    - QuestBoard.Domain/Services/DungeonMasterProfileService.cs
    - QuestBoard.Service/ViewModels/CharacterViewModels/CharacterViewModel.cs
    - QuestBoard.Service/ViewModels/ContactViewModels/ContactViewModel.cs
    - QuestBoard.Service/ViewModels/DungeonMasterViewModels/EditDMProfileViewModel.cs
    - QuestBoard.UnitTests/Services/CharacterServiceTests.cs
    - QuestBoard.UnitTests/Services/ContactServiceTests.cs
    - QuestBoard.UnitTests/Services/DungeonMasterProfileServiceTests.cs

key-decisions:
  - "Widened via additive overloads (Character/Contact) and a trailing optional parameter (DM profile) rather than changing existing signatures, so no existing caller needed to change"
  - "CroppedPictureFile uses the identical property name across all three ViewModels so the crop UI's hidden form input binds uniformly regardless of which upload target it's wired to"

patterns-established:
  - "Pattern: widen a CRUD service method to accept a caller-supplied artifact by adding a new overload/optional parameter, with the existing overload becoming a one-line delegating call with the new parameter defaulted to null/no-op"

requirements-completed: [IMAGE-01, IMAGE-05]

# Metrics
duration: 24min
completed: 2026-07-07
---

# Phase 46 Plan 01: Widen Domain Image-Update Methods and Add CroppedPictureFile Summary

**Additive 4-arg UpdateAsync overloads on CharacterService/ContactService plus a widened DungeonMasterProfileService.UpsertProfileAsync now thread a caller-supplied cropped byte[] through to the repository, and all three ViewModels expose a validated CroppedPictureFile binding property — the backend seam the crop UI's later plans submit into.**

## Performance

- **Duration:** 24 min
- **Started:** 2026-07-07T13:46:00Z
- **Completed:** 2026-07-07T14:10:28Z
- **Tasks:** 3
- **Files modified:** 12

## Accomplishments
- `ICharacterService`/`CharacterService` and `IContactService`/`ContactService` each gained an additive 4-arg `UpdateAsync` overload implementing the three-branch crop resolution (caller-supplied crop wins, new-original-no-crop clears, no-new-file re-fetches and preserves); the existing 3-arg overloads now delegate to the 4-arg ones with `newCroppedImageData: null`, so no existing caller changed behavior.
- `IDungeonMasterProfileService.UpsertProfileAsync` widened with a trailing optional `byte[]? newCroppedImageData = null` parameter, threaded into both `UpdateBioWithProfileImageAsync` call sites (lazy-create and update branches) in place of the previously-hardcoded `croppedImageData: null`.
- `CharacterViewModel`, `ContactViewModel`, and `EditDMProfileViewModel` each gained a `CroppedPictureFile` `IFormFile?` property carrying the same `MaxFileSize`/`AllowedExtensions` validation as their existing original-photo field.
- Extended all three service unit-test files with new tests covering the new-crop-supplied and no-new-file-refetch branches (Character/Contact) and the crop-supplied/crop-null branches (DM profile) — 6 new tests, full suite now 231/231 passing (was 225).

## Task Commits

Each task was committed atomically:

1. **Task 1: Widen CharacterService and ContactService UpdateAsync with a caller-supplied crop** - `e3923c7` (feat)
2. **Task 2: Widen DungeonMasterProfileService.UpsertProfileAsync and add CroppedPictureFile to all three ViewModels** - `329d0b9` (feat)
3. **Task 3: Extend the three service unit-test files with three-branch crop-resolution coverage** - `f737645` (test)

**Plan metadata:** committed alongside this summary

## Files Created/Modified
- `QuestBoard.Domain/Interfaces/ICharacterService.cs` - Additive 4-arg `UpdateAsync` overload declaration
- `QuestBoard.Domain/Services/CharacterService.cs` - 4-arg `UpdateAsync` implementation with three-branch crop resolution; 3-arg overload now a one-line delegate
- `QuestBoard.Domain/Interfaces/IContactService.cs` - Additive 4-arg `UpdateAsync` overload declaration (mirrors Character)
- `QuestBoard.Domain/Services/ContactService.cs` - 4-arg `UpdateAsync` implementation (mirrors Character, swapped for `GetContactCroppedImageAsync`/`ContactImageData`)
- `QuestBoard.Domain/Interfaces/IDungeonMasterProfileService.cs` - `UpsertProfileAsync` widened with optional `newCroppedImageData` parameter
- `QuestBoard.Domain/Services/DungeonMasterProfileService.cs` - Both `UpdateBioWithProfileImageAsync` call sites now thread `newCroppedImageData` instead of a literal `null`
- `QuestBoard.Service/ViewModels/CharacterViewModels/CharacterViewModel.cs` - New `CroppedPictureFile` validated property
- `QuestBoard.Service/ViewModels/ContactViewModels/ContactViewModel.cs` - New `CroppedPictureFile` validated property
- `QuestBoard.Service/ViewModels/DungeonMasterViewModels/EditDMProfileViewModel.cs` - New `CroppedPictureFile` validated property
- `QuestBoard.UnitTests/Services/CharacterServiceTests.cs` - Two new tests: new-crop-supplied persists, no-new-file re-fetches and passes through
- `QuestBoard.UnitTests/Services/ContactServiceTests.cs` - Two new tests mirroring CharacterServiceTests
- `QuestBoard.UnitTests/Services/DungeonMasterProfileServiceTests.cs` - Two new tests: crop-supplied persists, crop-null-on-new-image clears; two pre-existing test call sites fixed for the new optional parameter (see Deviations)

## Decisions Made
- Followed the plan's PATTERNS.md-specified widening shape verbatim (additive overload for Character/Contact, optional trailing parameter for DM profile) rather than deriving an alternative shape.
- Named the `token:` argument explicitly in two pre-existing DM profile test call sites rather than reordering the test's argument list, to keep the diff minimal and self-explanatory.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Fixed two pre-existing test call sites broken by the new optional parameter**
- **Found during:** Task 3 (extending DungeonMasterProfileServiceTests)
- **Issue:** `DungeonMasterProfileServiceTests.cs` had two existing calls to `UpsertProfileAsync(1, bio, imageBytes: ..., removeImage: false, TestContext.Current.CancellationToken)` that passed the `CancellationToken` as a positional argument in slot 5. Task 2's newly-inserted `newCroppedImageData` parameter now occupies slot 5, so the `CancellationToken` no longer bound to `token` — `dotnet build` failed with `CS1503: cannot convert from 'CancellationToken' to 'byte[]?'`.
- **Fix:** Named the trailing argument explicitly (`token: TestContext.Current.CancellationToken`) at both call sites so it binds correctly regardless of parameter position.
- **Files modified:** `QuestBoard.UnitTests/Services/DungeonMasterProfileServiceTests.cs`
- **Verification:** `dotnet build QuestBoard.UnitTests` succeeds; both pre-existing tests (`UpsertProfileAsync_BioOnlyEdit_PreservesExistingCroppedImage`, `UpsertProfileAsync_NewImageUpload_ClearsStaleCroppedImage`) still pass.
- **Committed in:** `f737645` (Task 3 commit)

---

**Total deviations:** 1 auto-fixed (1 bug)
**Impact on plan:** Necessary fix directly caused by this plan's own signature widening; no scope creep. All other work matched PATTERNS.md exactly.

## Issues Encountered
None beyond the deviation above.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- All three Domain image-update methods now accept and persist a genuinely new cropped `byte[]`, and all three ViewModels expose a validated `CroppedPictureFile` property — the exact seam later plans (controller wiring in 46-03, crop modal UI in 46-06) need to submit into.
- Full solution builds clean (`dotnet build`, 6 projects, 0 errors/warnings); full unit test suite passes 231/231.
- No blockers for subsequent waves in this phase.

---
*Phase: 46-client-side-crop-ui*
*Completed: 2026-07-07*

## Self-Check: PASSED

All 13 claimed files found on disk; all 3 task commit hashes (`e3923c7`, `329d0b9`, `f737645`) found in git history.
