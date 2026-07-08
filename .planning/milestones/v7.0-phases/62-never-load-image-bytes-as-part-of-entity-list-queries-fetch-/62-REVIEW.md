---
phase: 62-never-load-image-bytes-as-part-of-entity-list-queries-fetch
reviewed: 2026-07-08T00:00:00Z
depth: standard
files_reviewed: 37
files_reviewed_list:
  - QuestBoard.Domain/Models/Character.cs
  - QuestBoard.Domain/Models/Contact.cs
  - QuestBoard.Domain/Models/DungeonMasterProfile.cs
  - QuestBoard.Domain/Services/CharacterService.cs
  - QuestBoard.Domain/Services/ContactService.cs
  - QuestBoard.IntegrationTests/Controllers/CharactersControllerIntegrationTests.cs
  - QuestBoard.IntegrationTests/Controllers/ContactsControllerIntegrationTests.cs
  - QuestBoard.IntegrationTests/Controllers/DungeonMasterControllerIntegrationTests.cs
  - QuestBoard.Repository/Automapper/EntityProfile.cs
  - QuestBoard.Repository/CharacterRepository.cs
  - QuestBoard.Repository/ContactRepository.cs
  - QuestBoard.Repository/DungeonMasterProfileRepository.cs
  - QuestBoard.Service/Automapper/ViewModelProfile.cs
  - QuestBoard.Service/Controllers/Characters/CharactersController.cs
  - QuestBoard.Service/Controllers/Contacts/ContactsController.cs
  - QuestBoard.Service/Controllers/DungeonMaster/DungeonMasterController.cs
  - QuestBoard.Service/ViewModels/CharacterViewModels/CharacterViewModel.cs
  - QuestBoard.Service/ViewModels/ContactViewModels/ContactViewModel.cs
  - QuestBoard.Service/ViewModels/DungeonMasterViewModels/EditDMProfileViewModel.cs
  - QuestBoard.Service/Views/Characters/Create.cshtml
  - QuestBoard.Service/Views/Characters/Details.Mobile.cshtml
  - QuestBoard.Service/Views/Characters/Details.cshtml
  - QuestBoard.Service/Views/Characters/Edit.Mobile.cshtml
  - QuestBoard.Service/Views/Characters/Edit.cshtml
  - QuestBoard.Service/Views/Characters/Index.Mobile.cshtml
  - QuestBoard.Service/Views/Characters/Index.cshtml
  - QuestBoard.Service/Views/Contacts/Details.Mobile.cshtml
  - QuestBoard.Service/Views/Contacts/Details.cshtml
  - QuestBoard.Service/Views/Contacts/Edit.Mobile.cshtml
  - QuestBoard.Service/Views/Contacts/Edit.cshtml
  - QuestBoard.Service/Views/Contacts/Index.Mobile.cshtml
  - QuestBoard.Service/Views/Contacts/Index.cshtml
  - QuestBoard.Service/Views/DungeonMaster/EditProfile.Mobile.cshtml
  - QuestBoard.Service/Views/DungeonMaster/EditProfile.cshtml
  - QuestBoard.UnitTests/Repository/CharacterRepositoryTests.cs
  - QuestBoard.UnitTests/Repository/ContactRepositoryTests.cs
  - QuestBoard.UnitTests/Repository/DungeonMasterProfileRepositoryTests.cs
  - QuestBoard.UnitTests/Services/CharacterServiceTests.cs
  - QuestBoard.UnitTests/Services/ContactServiceTests.cs
findings:
  critical: 0
  warning: 0
  info: 2
  total: 2
status: clean
---

# Phase 62: Code Review Report

**Reviewed:** 2026-07-08T00:00:00Z
**Depth:** standard
**Files Reviewed:** 37
**Status:** clean

## Summary

This phase removes `.Include(ProfileImage)` eager-loading from six Character/Contact/DungeonMaster
read methods and replaces the byte[] display path with a projected `HasProfilePicture`/
`HasContactImage` boolean, computed via a `c.ProfileImage != null` scalar query that EF Core
translates to a JOIN/EXISTS without selecting the image byte columns.

The most fragile part of this phase — the write-path interaction — was traced end-to-end and is
correct. Removing the eager-load means `Character.ProfilePicture`/`Contact.ContactImageData` come
back `null` from `GetCharacterWithDetailsAsync`/`GetContactWithDetailsAsync` on every read. The
services (`CharacterService.UpdateAsync`/`ContactService.UpdateAsync`) correctly account for this:
on a "no new upload" edit they re-fetch the original image bytes fresh via
`GetCharacterOriginalPictureAsync`/`GetContactOriginalImageAsync` rather than trusting the
round-tripped (now-null) model property, so an unrelated-field edit no longer wipes the stored
photo. This is exercised by both service-level unit tests (`CharacterServiceTests`,
`ContactServiceTests`) and full HTTP integration tests
(`Edit_NoNewPhoto_PreservesStoredCroppedImage`, etc.), and all pass.

Verified as correctly out of scope and untouched: `CharacterRepository.GetMainCharacterForUserAsync`
still eager-loads `ProfileImage` — this is explicitly called out in `62-CONTEXT.md` as dead code
with zero production callers, not part of this phase's read-path fix, and confirmed to still be
unreferenced by any controller in the current codebase.

AutoMapper wiring was checked in both directions: the three `Entity → Domain` maps correctly
declare `.Ignore()` on the new boolean members (computed by the repository post-map, not
derivable from a single entity field), and the reverse `Domain → Entity` maps correctly have no
entry for them at all (these are read-only display flags, never written back). The
`Character → CharacterViewModel` map relies on AutoMapper's convention-based name/type matching for
`HasProfilePicture` (no explicit `ForMember`), which is safe here since both sides use the identical
name and type, and is proven correct by the passing integration tests that assert the rendered view
actually gates on the boolean (`Index_CharacterWithImage_RendersPortraitEndpoint` /
`..._DoesNotRenderPortraitEndpoint`, and DM/Contact equivalents).

All 37 in-scope files were read in full. `dotnet build` succeeded for `QuestBoard.Repository` and
`QuestBoard.Service` with 0 warnings/errors, and the 48 relevant unit tests (Character/Contact/DM
repository + Character/Contact service tests) all pass. No Critical or Warning-level defects were
found. Two Info-level observations are noted below for awareness; neither affects correctness.

## Info

### IN-01: Redundant explicit ForMember for Contact→ContactViewModel.HasContactImage

**File:** `QuestBoard.Service/Automapper/ViewModelProfile.cs:80`
**Issue:** `CreateMap<Contact, ContactViewModel>()` has an explicit
`.ForMember(dest => dest.HasContactImage, opt => opt.MapFrom(src => src.HasContactImage))`, while
the sibling `CreateMap<Character, CharacterViewModel>()` map relies on AutoMapper's convention-based
matching for the identically-shaped `HasProfilePicture` property (no explicit `ForMember`). Both
approaches produce the same runtime behavior since the source and destination property names/types
are identical, but the inconsistency between the two nearly-identical maps is a minor
maintainability nit — a future reader may wonder why one needs an explicit line and the other
doesn't.
**Fix:** Either remove the explicit `ForMember` for `HasContactImage` (let convention matching
handle it, matching `HasProfilePicture`), or add the equivalent explicit `ForMember` to the
Character map for consistency. Not required before shipping.

### IN-02: No AutoMapper AssertConfigurationIsValid safety net anywhere in the codebase

**File:** `QuestBoard.Service/Automapper/ViewModelProfile.cs`, `QuestBoard.Repository/Automapper/EntityProfile.cs`
**Issue:** This phase adds three new Domain model properties (`HasProfilePicture` x2,
`HasContactImage`) that are mapped via AutoMapper convention-matching at two separate boundaries
(Entity↔Domain and Domain↔ViewModel). Nothing in the codebase calls
`configuration.AssertConfigurationIsValid()` anywhere (verified via full-repo grep), so a future
rename of one side of a convention-matched pair (e.g. renaming `Character.HasProfilePicture` without
updating `CharacterViewModel.HasProfilePicture`) would silently fall back to the destination's
default value at runtime rather than failing fast at startup or in CI. This is a pre-existing gap in
the project's AutoMapper setup, not introduced by this phase, but this phase does add three more
convention-matched properties that would be silently affected by such a gap.
**Fix:** Consider adding `configuration.AssertConfigurationIsValid()` to a startup health-check path
or a dedicated unit test (e.g. `AutoMapperConfigurationTests`) in a future phase. Out of scope for
this phase to fix.

---

_Reviewed: 2026-07-08T00:00:00Z_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
