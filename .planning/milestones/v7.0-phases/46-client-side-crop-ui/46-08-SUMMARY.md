---
phase: 46-client-side-crop-ui
plan: 08
subsystem: api
tags: [crop, image-upload, create, services, repositories, integration-tests]

# Dependency graph
requires:
  - phase: 46-client-side-crop-ui (plan 01)
    provides: Widened 4-arg UpdateAsync (Character/Contact) accepting newCroppedImageData; CroppedPictureFile IFormFile binding property on both ViewModels
  - phase: 46-client-side-crop-ui (plan 03)
    provides: Create/Edit POST dual-file validation wiring; documented the known gap this plan closes (Create validated a submitted crop but discarded it)
provides:
  - AddAsync(Character model, byte[]? newCroppedImageData, CancellationToken) overload on ICharacterService/CharacterService
  - AddAsync(Contact model, byte[]? newCroppedImageData, CancellationToken) overload on IContactService/ContactService
  - CharactersController.Create and ContactsController.Create now persist a submitted crop instead of discarding it
  - Bug fix in CharacterRepository/ContactRepository Update paths preventing a null-navigation row loss
affects: [46-06]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Additive service overload for Create-time side effects: base AddAsync populates model.Id, then a second repository call (UpdateWithProfileImageAsync) only fires when the caller actually supplied the extra data, keeping the plain AddAsync path byte-for-byte unchanged"
    - "Reference-navigation preservation on in-place AutoMapper updates: any Mapper.Map(model, trackedEntity) call must restore every tracked reference-navigation property the source model doesn't reliably populate (not just child collections/ProfileImage), or the InMemory provider can silently drop the row"

key-files:
  created: []
  modified:
    - QuestBoard.Domain/Interfaces/ICharacterService.cs
    - QuestBoard.Domain/Services/CharacterService.cs
    - QuestBoard.Domain/Interfaces/IContactService.cs
    - QuestBoard.Domain/Services/ContactService.cs
    - QuestBoard.Service/Controllers/Characters/CharactersController.cs
    - QuestBoard.Service/Controllers/Contacts/ContactsController.cs
    - QuestBoard.Repository/CharacterRepository.cs
    - QuestBoard.Repository/ContactRepository.cs
    - QuestBoard.UnitTests/Services/CharacterServiceTests.cs
    - QuestBoard.UnitTests/Services/ContactServiceTests.cs
    - QuestBoard.IntegrationTests/Controllers/CharactersControllerIntegrationTests.cs
    - QuestBoard.IntegrationTests/Controllers/ContactsControllerIntegrationTests.cs

key-decisions:
  - "Reused Phase 45's existing UpdateWithProfileImageAsync repository method exactly as the plan specified — no new repository method or domain-model field was needed, contrary to Plan 03's initial Rule-4 assessment."
  - "Fixed a real, previously-latent bug in CharacterRepository/ContactRepository's UpdateAsync and UpdateWithProfileImageAsync: Mapper.Map(model, entity) was trusted to update the tracked entity in place, but it also overwrote the tracked Owner/Group (Character) and CreatedByUser/Group (Contact) reference navigations with whatever the caller's domain model happened to have set — null, for any model built without a prior fetch (exactly the shape a freshly-constructed Create-time Character/Contact has, and exactly what the new 3-arg AddAsync now calls into). EF's InMemory provider treated the nulled FK navigation as an orphan and silently dropped the row on save. Fixed by restoring the tracked Owner/Group/CreatedByUser instances after the map, mirroring the existing ProfileImage-preservation pattern already in both methods."

patterns-established:
  - "When adding a new call path into an existing in-place-AutoMapper-update method, verify every reference navigation the tracked entity depends on is preserved, not just the one the current feature touches — a partial fix (ProfileImage only) left a real bug for the very next caller."

requirements-completed: [IMAGE-01, IMAGE-05]

# Metrics
duration: 45min
completed: 2026-07-07
---

# Phase 46 Plan 08: Persist a Crop Submitted at Create Time Summary

**A new 3-arg `AddAsync(model, newCroppedImageData, token)` overload on `ICharacterService`/`IContactService` closes the gap Plan 03 flagged — a crop chosen while creating a brand-new Character or Contact is now persisted instead of silently discarded — and along the way surfaced and fixed a real data-loss bug in both repositories' in-place AutoMapper update path.**

## Performance

- **Duration:** ~45 min
- **Tasks:** 3
- **Files modified:** 12

## Accomplishments
- `ICharacterService`/`IContactService` gained an additive `AddAsync(model, byte[]? newCroppedImageData, token)` overload; the existing 2-arg `AddAsync` inherited from `BaseService<T>` is untouched and still used by every other caller.
- `CharacterService`/`ContactService` implement the new overload by calling the base `AddAsync` (unchanged — creates the row and populates `model.Id`), then, only when a crop was actually supplied, `repository.UpdateWithProfileImageAsync(model, originalBytes, croppedBytes, token)` to set the crop on the freshly-created image row. No new domain-model field, no new repository method — exactly as planned.
- `CharactersController.Create` and `ContactsController.Create` now extract the submitted `CroppedPictureFile`'s bytes (mirroring the existing original-file extraction pattern) and call the 3-arg `AddAsync`. No-crop and no-photo submissions still pass `null` and behave byte-for-byte as before.
- While building the unit tests for the new Create path (a domain model with only `OwnerId`/`GroupId` set, no `Owner`/`Group` navigation populated — the exact shape the real controller flow produces), discovered and fixed a genuine data-loss bug: `UpdateAsync`/`UpdateWithProfileImageAsync` in both repositories mapped the caller's model onto the tracked entity via `Mapper.Map(model, entity)`, which also nulled the tracked `Owner`/`Group`/`CreatedByUser` navigation whenever the model didn't have them populated — causing EF's InMemory provider to silently drop the entire row on save. Fixed by restoring those tracked navigation instances after the map, the same way `ProfileImage` was already being preserved.
- Added 2 unit tests each to `CharacterServiceTests`/`ContactServiceTests` (crop-supplied persists; no-crop falls back to original) using the existing InMemory-DB + real-repository convention, and 1 integration test each to `CharactersControllerIntegrationTests`/`ContactsControllerIntegrationTests` that POSTs a real multipart Create request with both an original and a cropped file and asserts the DB-level image row.
- Full solution: `dotnet build` clean (6 projects, 0 errors/warnings); `dotnet test` 235 unit + 372 integration, all green — no regressions from the repository fix.

## Task Commits

Each task was committed atomically:

1. **Task 1: Add the 3-arg AddAsync overload to ICharacterService/CharacterService and IContactService/ContactService** - `f15aeb2` (feat)
2. **Task 2: Wire CharactersController.Create and ContactsController.Create to extract and pass the cropped bytes** - `bc646b2` (feat)
3. **Task 3: Add unit and integration test coverage for the Create-time crop persistence** - `7c842b2` (test)

**Plan metadata:** committed alongside this summary

## Files Created/Modified
- `QuestBoard.Domain/Interfaces/ICharacterService.cs` / `CharacterService.cs` - New additive `AddAsync(model, newCroppedImageData, token)` overload
- `QuestBoard.Domain/Interfaces/IContactService.cs` / `ContactService.cs` - New additive `AddAsync(model, newCroppedImageData, token)` overload
- `QuestBoard.Service/Controllers/Characters/CharactersController.cs` - Create POST extracts cropped-file bytes and calls the 3-arg `AddAsync`
- `QuestBoard.Service/Controllers/Contacts/ContactsController.cs` - Create POST extracts cropped-file bytes and calls the 3-arg `AddAsync`
- `QuestBoard.Repository/CharacterRepository.cs` - Bug fix: `UpdateAsync`/`UpdateWithProfileImageAsync` now restore tracked `Owner`/`Group` navigations after `Mapper.Map(model, entity)`
- `QuestBoard.Repository/ContactRepository.cs` - Bug fix: `UpdateAsync`/`UpdateWithProfileImageAsync` now restore tracked `CreatedByUser`/`Group` navigations after `Mapper.Map(model, entity)`
- `QuestBoard.UnitTests/Services/CharacterServiceTests.cs` / `ContactServiceTests.cs` - New `AddAsync_NewCropSupplied_PersistsCrop` / `AddAsync_NoCropSupplied_FallsBackToOriginal` tests
- `QuestBoard.IntegrationTests/Controllers/CharactersControllerIntegrationTests.cs` / `ContactsControllerIntegrationTests.cs` - New `Create_WithCroppedPhoto_PersistsCroppedImage` tests

## Decisions Made
- Followed the plan's `<interfaces>` section verbatim for the new `AddAsync` overload shape and controller wiring — no deviation needed there.
- The Owner/Group navigation-preservation fix was applied to both `UpdateAsync` and `UpdateWithProfileImageAsync` in each repository (not just `UpdateWithProfileImageAsync`, which is the only one this plan's new code path calls), since both methods share the identical `Mapper.Map(model, entity)` pattern and the same bug — leaving `UpdateAsync` unfixed would have left a known-identical landmine for the very next caller that doesn't pre-fetch `Owner`/`Group`.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Fixed a null-navigation row-loss bug in CharacterRepository/ContactRepository's in-place AutoMapper update path**
- **Found during:** Task 3, while writing `AddAsync_NewCropSupplied_PersistsCrop` — the test's `Character`/`Contact` model (constructed fresh with only `OwnerId`/`GroupId` set, matching the real Create-controller flow) caused the row to vanish entirely (not just lose its crop) after the new `AddAsync` overload's second repository call.
- **Issue:** `UpdateAsync` and `UpdateWithProfileImageAsync` in both `CharacterRepository` and `ContactRepository` call `Mapper.Map(model, entity)` to update the EF-tracked entity in place. AutoMapper's convention-based mapping also overwrites `entity.Owner`/`entity.Group` (Character) and `entity.CreatedByUser`/`entity.Group` (Contact) with whatever `model.Owner`/`model.Group`/`model.CreatedByUser` currently holds — which is `null` for any domain model that was constructed directly (only the `*Id` scalar set) rather than fetched via a `GetXWithDetailsAsync` call first. Both methods already special-cased this exact problem for the `ProfileImage`/`Classes` navigations (restoring the tracked instance after the map) but never extended that same protection to `Owner`/`Group`/`CreatedByUser`. EF Core's InMemory provider treats the nulled-out required reference navigation as an orphaned dependent and silently deletes the row on `SaveChangesAsync` — no exception, no warning.
- **Fix:** Captured `entity.Owner`/`entity.Group` (and `entity.CreatedByUser`/`entity.Group` for Contact) before the `Mapper.Map` call, then restored them immediately after, identical to the existing `ProfileImage` restoration pattern.
- **Files modified:** `QuestBoard.Repository/CharacterRepository.cs`, `QuestBoard.Repository/ContactRepository.cs`
- **Verification:** Full solution test suite (235 unit + 372 integration) passes with no regressions; the two new `AddAsync_NewCropSupplied_PersistsCrop` tests specifically exercise the previously-broken path and now pass.
- **Commit:** `7c842b2`

---

**Total deviations:** 1 auto-fixed (1 bug)
**Impact on plan:** The bug was pre-existing and latent (every prior caller of `UpdateAsync`/`UpdateWithProfileImageAsync` happened to always pre-fetch the model via a details query, which populates `Owner`/`Group`/`CreatedByUser`). This plan's own new `AddAsync` overload was the first caller to exercise the vulnerable shape, so fixing it was necessary to make the plan's own tests pass — not scope creep, but a direct consequence of exercising this code path with a Create-shaped (never-fetched) model for the first time.

## Issues Encountered
None beyond the bug documented above, which was fixed within scope.

## User Setup Required
None — no external service configuration required.

## Next Phase Readiness
- Creating a Character or Contact with a submitted crop now genuinely persists that crop end-to-end (controller -> 3-arg `AddAsync` -> `UpdateWithProfileImageAsync`), closing the "create and edit" gap Plan 03 flagged and verified at the DB level by integration tests.
- Creating without a crop (or without any photo) is behaviorally unchanged — proven both by the existing full regression suite (607 tests, all green) and the new dedicated "no crop supplied" unit tests.
- Plan 46-06 (wiring the crop modal into the Character/Contact Create forms) can now rely on Create genuinely honoring a submitted crop rather than shipping a UI that silently discards it.
- No blockers for subsequent waves in this phase.

---
*Phase: 46-client-side-crop-ui*
*Completed: 2026-07-07*

## Self-Check: PASSED

All 12 claimed modified/created files found on disk (4 Domain interface/service files, 2 controller files, 2 repository files, 2 unit test files, 2 integration test files). All 4 commit hashes (`f15aeb2`, `bc646b2`, `7c842b2`, `c3d5e45`) found in git history.
