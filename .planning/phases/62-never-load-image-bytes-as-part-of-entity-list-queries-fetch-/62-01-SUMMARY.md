---
phase: 62-never-load-image-bytes-as-part-of-entity-list-queries-fetch
plan: 01
subsystem: repository
tags: [ef-core, automapper, performance, image-loading]
dependency-graph:
  requires: []
  provides:
    - "Character.HasProfilePicture bool"
    - "Contact.HasContactImage bool"
    - "DungeonMasterProfile.HasProfilePicture bool"
    - "Character/Contact/DungeonMasterProfile read methods no longer eager-load image bytes"
  affects:
    - "QuestBoard.Repository/CharacterRepository.cs"
    - "QuestBoard.Repository/ContactRepository.cs"
    - "QuestBoard.Repository/DungeonMasterProfileRepository.cs"
tech-stack:
  added: []
  patterns:
    - "Scalar presence-flag projection (`c.ProfileImage != null`) instead of `.Include()`, mirroring QuestRepository.ProjectWithoutCharacterImages"
key-files:
  created: []
  modified:
    - QuestBoard.Domain/Models/Character.cs
    - QuestBoard.Domain/Models/Contact.cs
    - QuestBoard.Domain/Models/DungeonMasterProfile.cs
    - QuestBoard.Repository/Automapper/EntityProfile.cs
    - QuestBoard.Repository/CharacterRepository.cs
    - QuestBoard.Repository/ContactRepository.cs
    - QuestBoard.Repository/DungeonMasterProfileRepository.cs
    - QuestBoard.UnitTests/Repository/CharacterRepositoryTests.cs
    - QuestBoard.UnitTests/Repository/ContactRepositoryTests.cs
    - QuestBoard.UnitTests/Repository/DungeonMasterProfileRepositoryTests.cs
decisions:
  - "Domain models add HasProfilePicture/HasContactImage as additive properties alongside existing byte[] fields — no existing property removed or renamed"
  - "AutoMapper Entity->Domain maps declare the new bools .Ignore() since they're computed by a separate scalar query, not derivable from a single entity field; repository sets them explicitly post-map"
  - "Single-entity read methods (GetCharacterWithDetailsAsync, GetContactWithDetailsAsync) get the identical treatment as their list-method siblings, per CONTEXT.md D-02, even though the phase's original goal text only named the list methods"
  - "DM profile boolean query roots at DungeonMasterProfileImages directly (not DungeonMasterProfiles), matching the existing GetOriginalPictureAsync precedent in the same file — DM profiles are not group-scoped"
metrics:
  duration: "~35min"
  completed: 2026-07-07
status: complete
---

# Phase 62 Plan 01: Backend read-projection foundation Summary

Removed `.Include(...ProfileImage)` from the six Character/Contact/DungeonMasterProfile read methods and replaced each with a `!= null` scalar boolean projection (`HasProfilePicture` / `HasContactImage`) that never selects the underlying image byte columns.

## What Was Built

- **Domain models**: `Character.HasProfilePicture`, `Contact.HasContactImage`, and `DungeonMasterProfile.HasProfilePicture` — plain additive boolean properties alongside the existing `byte[]?` image properties, which are untouched.
- **AutoMapper (`EntityProfile.cs`)**: the three Entity→Domain maps (`CharacterEntity->Character`, `ContactEntity->Contact`, `DungeonMasterProfileEntity->DungeonMasterProfile`) each gained a `.ForMember(dest => dest.HasX, opt => opt.Ignore())` entry, since the boolean is computed by a separate scalar query rather than mapped from a single entity field. Reverse-direction (Domain→Entity) maps were not touched — these flags are read-only display state, never written back.
- **`CharacterRepository`**: `GetAllCharactersWithDetailsAsync`, `GetCharactersByOwnerIdAsync`, and `GetCharacterWithDetailsAsync` no longer `.Include(c => c.ProfileImage)`. Each now runs a scalar `c.ProfileImage != null` projection (a dictionary lookup for the two list methods, a direct `FirstOrDefaultAsync` for the single-entity method) and sets `HasProfilePicture` post-map. `GetMainCharacterForUserAsync` and the write-path methods (`UpdateAsync`, `UpdateWithProfileImageAsync`, `GetCharacterOriginalPictureAsync`, `GetCharacterCroppedPictureAsync`) were left untouched — out of scope.
- **`ContactRepository`**: `GetAllContactsWithDetailsAsync` and `GetContactWithDetailsAsync` no longer `.Include(c => c.ProfileImage)`, same scalar-projection treatment as Character. The list method's existing Notes-reorder `foreach` loop was extended (not duplicated) to also set `HasContactImage`.
- **`DungeonMasterProfileRepository.GetProfileByUserIdAsync`**: no longer `.Include(p => p.ProfileImage)`; `HasProfilePicture` is set from a scalar query rooted at `DungeonMasterProfileImages` directly (matching the file's existing `GetOriginalPictureAsync` non-group-scoped rooting pattern), not at `DungeonMasterProfiles`.
- **Repository unit tests**: added true-with-image / false-without-image coverage across all three test files — `GetAllCharactersWithDetailsAsync_ReflectsHasProfilePicture_TrueWithImage_FalseWithout`, `GetCharacterWithDetailsAsync_ReflectsHasProfilePicture_TrueWithImage`, `GetAllContactsWithDetailsAsync_ReflectsHasContactImage_TrueWithImage_FalseWithout`, `GetContactWithDetailsAsync_ReflectsHasContactImage_TrueWithImage`, `GetProfileByUserIdAsync_ForProfileWithImage_HasProfilePictureIsTrue`, `GetProfileByUserIdAsync_ForProfileWithoutImage_HasProfilePictureIsFalse`.

## How It Works

Each rewritten read method drops the eager `.Include()` on the image navigation and instead runs (or reuses an already-materialized) query shaped as `c.ProfileImage != null`. Because this expression only references navigation existence — never a property of the navigated entity — EF Core translates it to a JOIN/EXISTS check rather than selecting any column from the image table, so `OriginalImageData`/`CroppedImageData` never travel across the wire for these methods. List methods batch this into one dictionary-keyed scalar query (`Select(c => new { c.Id, HasImage = ... }).ToDictionaryAsync(...)`) run once per call rather than N+1 per row; single-entity methods run the equivalent as a single filtered scalar query. The Domain-model boolean is then set on each mapped instance in a `foreach` (list methods) or directly (single-entity methods), after AutoMapper's own Entity→Domain map — which is told via `.Ignore()` to leave the property alone rather than attempt (and fail) to convention-match it against a nonexistent entity property.

## Deviations from Plan

### Auto-fixed Issues

None — plan executed as written; no bugs, missing functionality, or blocking issues were encountered.

### Test seed data adjustment (not a deviation from behavior, but from plan wording)

The plan's `<read_first>` for Task 3 pointed at `SeedTwoGroupCharactersAsync`/`SeedTwoGroupContactsAsync` as the seed helpers to reuse for the new has-image tests. Both existing helpers seed every character/contact WITH a `ProfileImage`, and are also consumed by pre-existing tests that assert exact single-item counts (`ContainSingle()`). Adding a third, image-less entity to those shared helpers would have silently changed those pre-existing tests' underlying data shape. Instead, the two new "true/false" comparison tests (`GetAllCharactersWithDetailsAsync_ReflectsHasProfilePicture_TrueWithImage_FalseWithout`, `GetAllContactsWithDetailsAsync_ReflectsHasContactImage_TrueWithImage_FalseWithout`) seed their own small, dedicated two-entity fixture (one with an image, one without) rather than extending the shared helper — same `CreateContext`/`CreateMapper` pattern, isolated data. The single-entity "true" tests (`GetCharacterWithDetailsAsync_ReflectsHasProfilePicture_TrueWithImage`, `GetContactWithDetailsAsync_ReflectsHasContactImage_TrueWithImage`) do reuse the shared two-group seed helpers as originally intended, since those seeded entities already carry images.

## Verification

- `dotnet build` (full solution, 5 projects): 0 warnings, 0 errors.
- `dotnet test QuestBoard.UnitTests` filtered to Character/Contact/DungeonMasterProfile repository tests: 34/34 passing (28 pre-existing + 6 new).
- `dotnet test QuestBoard.UnitTests` full suite: 241/241 passing — no regressions elsewhere.
- Source assertions confirmed via grep: no remaining `.Include(...ProfileImage)` in any of the six target read methods; the only remaining occurrences are in out-of-scope write methods (`UpdateAsync`, `UpdateWithProfileImageAsync`, `UpsertProfileImageAsync`, `UpdateBioWithProfileImageAsync`) and `GetMainCharacterForUserAsync` (dead code, explicitly out of scope per the plan).
- `ContactRepository.GetAllContactsWithDetailsAsync` still has exactly one `foreach` loop (Notes reorder extended, not duplicated) — confirmed.

## Self-Check: PASSED

Files verified to exist:
- FOUND: QuestBoard.Domain/Models/Character.cs (HasProfilePicture present)
- FOUND: QuestBoard.Domain/Models/Contact.cs (HasContactImage present)
- FOUND: QuestBoard.Domain/Models/DungeonMasterProfile.cs (HasProfilePicture present)
- FOUND: QuestBoard.Repository/Automapper/EntityProfile.cs (three Ignore() entries present)
- FOUND: QuestBoard.Repository/CharacterRepository.cs
- FOUND: QuestBoard.Repository/ContactRepository.cs
- FOUND: QuestBoard.Repository/DungeonMasterProfileRepository.cs
- FOUND: QuestBoard.UnitTests/Repository/CharacterRepositoryTests.cs
- FOUND: QuestBoard.UnitTests/Repository/ContactRepositoryTests.cs
- FOUND: QuestBoard.UnitTests/Repository/DungeonMasterProfileRepositoryTests.cs

Commits verified in git log:
- FOUND: d241325 (feat: Domain model bools + AutoMapper Ignore entries)
- FOUND: 9a93435 (feat: Character/Contact repository changes)
- FOUND: 2bc67e3 (test: DM profile fix + repository test coverage)
