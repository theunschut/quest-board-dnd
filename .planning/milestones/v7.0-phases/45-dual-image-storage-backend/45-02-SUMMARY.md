---
phase: 45-dual-image-storage-backend
plan: 02
subsystem: database
tags: [ef-core, automapper, image-storage, tdd, sql-server]

# Dependency graph
requires:
  - phase: 45-01
    provides: "OriginalImageData (required) + CroppedImageData (nullable) columns on CharacterImages, DungeonMasterProfileImages, ContactImages"
provides:
  - "Widened ICharacterRepository/IContactRepository/IDungeonMasterProfileRepository: two-param atomic upsert (originalImageData, croppedImageData) + distinct GetXOriginalPictureAsync/GetXCroppedPictureAsync reads with query-level CroppedImageData ?? OriginalImageData fallback"
  - "ICharacterService.UpdateAsync(model, hasNewOriginalUpload, token) and IContactService's identical overload -- the controller-supplied signal that lets the service distinguish a genuinely new photo upload from a round-tripped no-change edit"
  - "DungeonMasterProfileService.UpsertProfileAsync widened to pass croppedImageData through its existing imageBytes != null / removeImage signal"
  - "Service-level regression coverage (not just repository-level) proving Pitfall 4 (preserve stored crop on no-upload edit) and Pitfall 5 (clear stale crop on genuine new-original upload) for Character, Contact, and DM"
affects: [45-03, dual-image-storage-backend]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Query-level COALESCE fallback (CroppedImageData ?? OriginalImageData) inside the LINQ Select projection, not in application code"
    - "Controller-supplied boolean signal (hasNewOriginalUpload) threaded through a new service-interface overload, since the base IBaseService<T>.UpdateAsync(model, token) signature is fixed and model.ProfilePicture/ContactImageData is never null on a no-photo-change edit"
    - "InMemory EF Core test database names must be class-qualified (ClassName.MethodName), not bare nameof(Method) -- identical [Fact] names across sibling test classes collide under parallel test execution"

key-files:
  created:
    - QuestBoard.UnitTests/Repository/DungeonMasterProfileRepositoryTests.cs
    - QuestBoard.UnitTests/Services/CharacterServiceTests.cs
    - QuestBoard.UnitTests/Services/ContactServiceTests.cs
    - QuestBoard.UnitTests/Services/DungeonMasterProfileServiceTests.cs
  modified:
    - QuestBoard.Domain/Interfaces/ICharacterRepository.cs
    - QuestBoard.Domain/Interfaces/IDungeonMasterProfileRepository.cs
    - QuestBoard.Domain/Interfaces/IContactRepository.cs
    - QuestBoard.Domain/Interfaces/ICharacterService.cs
    - QuestBoard.Domain/Interfaces/IContactService.cs
    - QuestBoard.Domain/Interfaces/IDungeonMasterProfileService.cs
    - QuestBoard.Repository/CharacterRepository.cs
    - QuestBoard.Repository/DungeonMasterProfileRepository.cs
    - QuestBoard.Repository/ContactRepository.cs
    - QuestBoard.Domain/Services/CharacterService.cs
    - QuestBoard.Domain/Services/ContactService.cs
    - QuestBoard.Domain/Services/DungeonMasterProfileService.cs
    - QuestBoard.Service/Controllers/Characters/CharactersController.cs
    - QuestBoard.Service/Controllers/Contacts/ContactsController.cs
    - QuestBoard.UnitTests/Repository/CharacterRepositoryTests.cs
    - QuestBoard.UnitTests/Repository/ContactRepositoryTests.cs

key-decisions:
  - "Final method names (locked for Plan 03): Character repository/service -- GetCharacterOriginalPictureAsync, GetCharacterCroppedPictureAsync, UpdateProfileImageAsync(id, byte[]? original, byte[]? cropped, token). Contact -- GetContactOriginalImageAsync, GetContactCroppedImageAsync, UpdateProfileImageAsync(id, byte[]? original, byte[]? cropped, token). DM -- GetOriginalPictureAsync (repository)/GetProfilePictureAsync (service, unchanged name), GetCroppedPictureAsync (both layers), UpsertProfileImageAsync(userId, byte[]? original, byte[]? cropped, token)."
  - "New service overload signature Plan 03 must call: Task UpdateAsync(Character model, bool hasNewOriginalUpload, CancellationToken token = default) on ICharacterService, and the identical shape on IContactService for Contact. Controllers derive hasNewOriginalUpload from the exact same IFormFile != null && .Length > 0 check that already gates CopyToAsync."
  - "The inherited base UpdateAsync(model, token) override on Character/Contact services now delegates to UpdateAsync(model, hasNewOriginalUpload: false, token) rather than duplicating logic -- any not-yet-updated caller defaults to the safe preserve-crop behavior."
  - "DungeonMasterProfileService needed no new overload -- UpsertProfileAsync's existing imageBytes != null / removeImage branching already carries the new-upload signal; only its UpsertProfileImageAsync calls needed the croppedImageData parameter threaded (always null, since DM's crop-writing arrives in a later plan)."
  - "Minimal rename fix applied to CharactersController.GetProfilePicture and ContactsController.GetContactImage (call sites only, not the actions' shape/auth checks) so the full solution builds this plan, even though Plan 03 owns the full controller wiring -- required by this plan's own acceptance criteria (dotnet build must succeed)."
  - "InMemory database names in all 5 touched/created test files prefixed with their owning class name (e.g. \"CharacterServiceTests.\" + nameof(...)) after discovering identically-named [Fact]s in different test classes (a pattern this phase's plan itself specified, e.g. UpdateProfileImageAsync_SetsOriginalImageData appearing in both CharacterRepositoryTests and ContactRepositoryTests) collided on the same InMemory database under parallel test execution."

requirements-completed: [IMAGE-03]

# Metrics
duration: 25min
completed: 2026-07-07
---

# Phase 45 Plan 02: Widen Repositories and Services for Dual-Image Storage Summary

**Atomic dual-image upsert plus independently-retrievable original/cropped reads across Character/Contact/DungeonMasterProfile, with a controller-supplied `hasNewOriginalUpload` signal that makes both the preserve-stored-crop (Pitfall 4) and clear-stale-crop (Pitfall 5) behaviors provably reachable through the real service call path, not just a direct repository test.**

## Performance

- **Duration:** ~25 min
- **Started:** 2026-07-07 (Wave 2 execution)
- **Completed:** 2026-07-07
- **Tasks:** 3 (RED, GREEN repository, GREEN service)
- **Files modified:** 20 (4 created, 16 modified)

## Accomplishments
- `ICharacterRepository`/`IContactRepository`/`IDungeonMasterProfileRepository` widened to a two-param atomic upsert (`originalImageData`, `croppedImageData`) that sets both columns on the same tracked entity in a single `SaveChangesAsync`
- Each repository now exposes two distinct read methods: an original-only read (renamed from the old single `GetXProfilePictureAsync`/`GetXImageAsync`) and a new cropped read using the query-level `CroppedImageData ?? OriginalImageData` fallback -- proven independently retrievable and distinct when both are set
- `ICharacterService`/`IContactService` gained a new `UpdateAsync(model, hasNewOriginalUpload, token)` overload; the base `UpdateAsync(model, token)` now delegates to it with `hasNewOriginalUpload: false` (safe default)
- `DungeonMasterProfileService.UpsertProfileAsync`'s existing `imageBytes != null`/`removeImage` branching widened to pass `croppedImageData` through to the repository, clearing any stale crop on a genuine new upload
- Full test coverage: 5 repository-level behaviors x 3 entities (Character/Contact/DM) plus service-level Pitfall 4 (preserve) and Pitfall 5 (clear-stale-crop-through-the-service) regression tests x 3 entities -- 25 new/updated tests, all green
- New `QuestBoard.UnitTests/Services/` directory (first pure-Domain-service unit tests in the codebase) with `CharacterServiceTests.cs`, `ContactServiceTests.cs`, `DungeonMasterProfileServiceTests.cs`, each constructing the concrete service against a real InMemory-backed repository (no mocks) so the clear-stale-crop assertion runs against genuine EF behavior end-to-end

## Task Commits

Each task was committed atomically:

1. **Task 1: Write failing repository + service tests for dual-image storage, including service-level Pitfall 4 AND Pitfall 5 (RED)** - `de9d20f` (test)
2. **Task 2: Widen the three repositories and interfaces to store/read both columns (GREEN)** - `7588d90` (feat)
3. **Task 3: Widen the three services with a controller-supplied new-upload signal (GREEN)** - `db8e314` (feat)

**Plan metadata:** (this commit) - `docs: complete plan`

## Files Created/Modified
- `QuestBoard.UnitTests/Repository/DungeonMasterProfileRepositoryTests.cs` - new file, 7 facts covering the five repository behaviors for DM profile images (no group-scoping, single-profile seed)
- `QuestBoard.UnitTests/Services/CharacterServiceTests.cs`, `ContactServiceTests.cs`, `DungeonMasterProfileServiceTests.cs` - new files, each with a preserve-on-no-upload test and a clear-stale-crop-on-new-upload test constructed against a real repository + InMemory DbContext
- `QuestBoard.UnitTests/Repository/CharacterRepositoryTests.cs`, `ContactRepositoryTests.cs` - extended with 6 new facts each (rename existing cross-group picture tests to `...Original...`, plus `UpdateProfileImageAsync_SetsOriginalImageData`, `GetXCroppedPictureAsync_FallsBackToOriginal_WhenCroppedIsNull`, `GetXOriginalAndCroppedPictureAsync_ReturnDistinctValues`, `UpdateProfileImageAsync_ReplacesBothColumnsAtomically`, `UpdateProfileImageAsync_NewOriginalWithoutCrop_ClearsStaleCropped`)
- `QuestBoard.Domain/Interfaces/ICharacterRepository.cs`, `IDungeonMasterProfileRepository.cs`, `IContactRepository.cs` - widened upsert signature, renamed original read, added cropped read
- `QuestBoard.Repository/CharacterRepository.cs`, `DungeonMasterProfileRepository.cs`, `ContactRepository.cs` - implementation of the above; DM's reads stay rooted at `DungeonMasterProfileImages` directly (no group filter), Character/Contact stay rooted at their owner DbSet (group filter preserved)
- `QuestBoard.Domain/Interfaces/ICharacterService.cs`, `IContactService.cs`, `IDungeonMasterProfileService.cs` - new `UpdateAsync(model, hasNewOriginalUpload, token)` overload (Character/Contact) and new `GetCroppedPictureAsync`/pass-through methods
- `QuestBoard.Domain/Services/CharacterService.cs`, `ContactService.cs`, `DungeonMasterProfileService.cs` - implement the new-upload signal branching described above
- `QuestBoard.Service/Controllers/Characters/CharactersController.cs`, `Contacts/ContactsController.cs` - minimal call-site rename only (`GetCharacterProfilePictureAsync` -> `GetCharacterOriginalPictureAsync`, `GetContactImageAsync` -> `GetContactOriginalImageAsync`) so the full solution builds this plan; Plan 03 owns the real controller wiring (new-upload signal derivation, serving both original and cropped images)

## Decisions Made
- Locked the final method names and the exact `UpdateAsync(model, hasNewOriginalUpload, token)` signature (see frontmatter `key-decisions`) so Plan 03's controllers call them exactly.
- Chose to fetch-and-pass-through the stored cropped value (via the new `GetXCroppedPictureAsync` read) inside the service's no-upload branch, rather than requiring the controller to supply it -- keeps the controller's job limited to the one boolean signal it's uniquely positioned to know.
- Prefixed every InMemory test database name with its owning class name after discovering cross-class name collisions (see Issues Encountered).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Renamed two controller call sites so the full solution builds**
- **Found during:** Task 3 (`dotnet build` verification step)
- **Issue:** `CharactersController.GetProfilePicture` called `characterService.GetCharacterProfilePictureAsync(...)` and `ContactsController.GetContactImage` called `contactService.GetContactImageAsync(...)` -- both renamed by this plan's service-interface widening. Plan 03 owns the full controller rewrite, but this plan's own acceptance criteria requires `dotnet build` to succeed for the whole solution.
- **Fix:** Renamed the two call sites to the new method names (`GetCharacterOriginalPictureAsync`, `GetContactOriginalImageAsync`) with zero other changes to either action -- same auth checks, same MIME-sniffing tail, same behavior. Plan 03 will add the new cropped-serving actions and the `hasNewOriginalUpload` derivation on top of this.
- **Files modified:** `QuestBoard.Service/Controllers/Characters/CharactersController.cs`, `QuestBoard.Service/Controllers/Contacts/ContactsController.cs`
- **Verification:** `dotnet build` succeeds for all 6 projects; full test suite green.
- **Committed in:** `db8e314` (Task 3 commit)

**2. [Rule 1 - Bug] Fixed InMemory EF Core database name collisions across test files**
- **Found during:** Task 3 (running the full `Image|Cropped` filtered test suite together)
- **Issue:** Several `[Fact]`s across `CharacterRepositoryTests`/`ContactRepositoryTests`/`DungeonMasterProfileRepositoryTests`/`CharacterServiceTests`/`ContactServiceTests` share identical method names by design (e.g. `UpdateProfileImageAsync_SetsOriginalImageData` in both `CharacterRepositoryTests` and `ContactRepositoryTests`, mirroring the plan's own instruction to give Contact/DM "the same five repository behavior tests"). Each test's `CreateContext(nameof(TestMethod), ...)` call produced an identical InMemory database name across classes, which under xUnit's parallel test execution caused a `System.ArgumentException: An item with the same key has already been added` when two same-named tests in different classes ran concurrently and both tried to seed `Id = 1` into what EF Core's InMemory provider treated as the same logical database.
- **Fix:** Prefixed every `CreateContext` call's database name with its owning class name (e.g. `"CharacterServiceTests." + nameof(UpdateAsync_NoNewUpload_PreservesExistingCroppedImage)`) across all 5 touched/created test files, guaranteeing global uniqueness regardless of which other classes run concurrently.
- **Files modified:** `QuestBoard.UnitTests/Repository/CharacterRepositoryTests.cs`, `ContactRepositoryTests.cs`, `DungeonMasterProfileRepositoryTests.cs`, `QuestBoard.UnitTests/Services/CharacterServiceTests.cs`, `ContactServiceTests.cs`
- **Verification:** `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~Image|FullyQualifiedName~Cropped"` went from 5 intermittent failures to 25/25 passing; full suite (213 unit + 361 integration) green afterward.
- **Committed in:** `db8e314` (Task 3 commit, bundled with the service widening since both were needed to reach the plan's required green state)

---

**Total deviations:** 2 auto-fixed (1 blocking, 1 bug)
**Impact on plan:** Both fixes were required to satisfy this plan's own stated acceptance criteria (full solution build, fully green `Image|Cropped` filtered test run). No scope creep -- Plan 03's actual controller rewrite (new-upload signal derivation, serving cropped images) is untouched and still pending.

## Issues Encountered

See Deviations above -- both issues were discovered via the plan's own `<verify>` steps and resolved within the same task before moving on, per the fix-attempt-limit guidance (each took one fix, verified, no further attempts needed).

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- All three repositories (Character/Contact/DungeonMasterProfile) store both an original and a cropped image atomically and expose independently-retrievable reads with the correct fallback.
- Plan 03 can now update `CharactersController`/`ContactsController`/`DungeonMasterController` to: (a) derive `hasNewOriginalUpload` from the existing `IFormFile != null && .Length > 0` check and call the new `UpdateAsync(model, hasNewOriginalUpload, token)` overload; (b) call `DungeonMasterProfileService.UpsertProfileAsync` unchanged (its signal was already correct); (c) add new GET actions serving the cropped image alongside the existing original-image actions, reusing each controller's existing auth-check sequence.
- No `.cshtml` view files were touched this plan (D-04 honored) -- visual behavior is unchanged until Plan 03/Phase 46 wire up the crop UI and consume the cropped column.
- No blockers for 45-03.

## Self-Check: PASSED

- All 4 new files confirmed present on disk (DungeonMasterProfileRepositoryTests.cs, CharacterServiceTests.cs, ContactServiceTests.cs, DungeonMasterProfileServiceTests.cs)
- SUMMARY.md itself confirmed present on disk
- All 3 task commit hashes (de9d20f, 7588d90, db8e314) confirmed present in git history
