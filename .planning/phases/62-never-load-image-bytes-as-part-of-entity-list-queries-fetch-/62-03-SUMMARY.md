---
phase: 62-never-load-image-bytes-as-part-of-entity-list-queries-fetch-
plan: 03
subsystem: ui
tags: [automapper, razor, aspnet-core-mvc, ef-core, viewmodels]

# Dependency graph
requires:
  - phase: 62-01
    provides: HasProfilePicture/HasContactImage boolean projections on Character/Contact/DungeonMasterProfile Domain models, plus the repository query changes that stopped eager-loading image byte[] columns
provides:
  - CharacterViewModel/ContactViewModel/EditDMProfileViewModel display-path byte[] properties replaced with bool
  - Domain->ViewModel AutoMapper maps updated to route the new booleans and ignore the byte[] on the reverse (ViewModel->Domain) direction
  - CharactersController/ContactsController Create POST staging fixed to use a local variable and set the mapped Domain model's byte[] property post-map
  - DungeonMasterController Profile/EditProfile GET sourcing HasProfilePicture from the projected Domain bool instead of the no-longer-eager-loaded byte[]
  - 15 Razor views (Characters, Contacts, DungeonMaster EditProfile — desktop + mobile) gating portrait rendering on the boolean
  - 8 new integration tests covering the boolean gate end-to-end and the Create-POST original-image persistence fix
affects: [63, 64]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "ViewModel display existence-gate as bool, not byte[] null-check — mirrors the DMProfileViewModel.HasProfilePicture pattern that already existed"
    - "Create-POST upload staged in a local variable, assigned to the mapped Domain model's byte[] property after mapper.Map, instead of round-tripping through a ViewModel property"

key-files:
  created: []
  modified:
    - QuestBoard.Service/ViewModels/CharacterViewModels/CharacterViewModel.cs
    - QuestBoard.Service/ViewModels/ContactViewModels/ContactViewModel.cs
    - QuestBoard.Service/ViewModels/DungeonMasterViewModels/EditDMProfileViewModel.cs
    - QuestBoard.Service/Automapper/ViewModelProfile.cs
    - QuestBoard.Service/Controllers/Characters/CharactersController.cs
    - QuestBoard.Service/Controllers/Contacts/ContactsController.cs
    - QuestBoard.Service/Controllers/DungeonMaster/DungeonMasterController.cs
    - QuestBoard.Service/Views/Characters/Index.cshtml
    - QuestBoard.Service/Views/Characters/Index.Mobile.cshtml
    - QuestBoard.Service/Views/Characters/Details.cshtml
    - QuestBoard.Service/Views/Characters/Details.Mobile.cshtml
    - QuestBoard.Service/Views/Characters/Create.cshtml
    - QuestBoard.Service/Views/Characters/Edit.cshtml
    - QuestBoard.Service/Views/Characters/Edit.Mobile.cshtml
    - QuestBoard.Service/Views/Contacts/Index.cshtml
    - QuestBoard.Service/Views/Contacts/Index.Mobile.cshtml
    - QuestBoard.Service/Views/Contacts/Details.cshtml
    - QuestBoard.Service/Views/Contacts/Details.Mobile.cshtml
    - QuestBoard.Service/Views/Contacts/Edit.cshtml
    - QuestBoard.Service/Views/Contacts/Edit.Mobile.cshtml
    - QuestBoard.Service/Views/DungeonMaster/EditProfile.cshtml
    - QuestBoard.Service/Views/DungeonMaster/EditProfile.Mobile.cshtml
    - QuestBoard.IntegrationTests/Controllers/CharactersControllerIntegrationTests.cs
    - QuestBoard.IntegrationTests/Controllers/ContactsControllerIntegrationTests.cs
    - QuestBoard.IntegrationTests/Controllers/DungeonMasterControllerIntegrationTests.cs

key-decisions:
  - "ContactViewModel->Contact reverse map: ContactImageData explicitly Ignore()'d rather than left to convention, since the bool must not attempt to round-trip onto the Domain byte[] property (Pitfall 4)"
  - "CharacterViewModel->Character reverse map: ProfilePicture explicitly Ignore()'d for the same reason — the Create controller sets it directly on the mapped Domain model post-map instead"

patterns-established:
  - "Display-path existence gate as bool sourced from a projected Domain flag, never a byte[] null-check — now consistent across all three portrait-bearing entities (Character, Contact, DungeonMasterProfile)"

requirements-completed: []

# Metrics
duration: 35min
completed: 2026-07-07
status: complete
---

# Phase 62 Plan 03: Display-Path ViewModel Boolean Conversion Summary

**Replaced the raw byte[] display properties on CharacterViewModel/ContactViewModel/EditDMProfileViewModel with HasProfilePicture/HasContactImage booleans, fixed the two Create-POST controllers' upload staging and the DM controller's now-stale byte[] source expressions, and updated all 15 affected Razor views to gate on the boolean.**

## Performance

- **Duration:** ~35 min
- **Started:** 2026-07-07T21:22:00Z (approx, per STATE.md wave-2 dispatch)
- **Completed:** 2026-07-07T21:57:56Z
- **Tasks:** 3 completed
- **Files modified:** 24

## Accomplishments
- No display-path ViewModel property carries raw image bytes anymore — `CharacterViewModel.HasProfilePicture`, `ContactViewModel.HasContactImage`, and `EditDMProfileViewModel.HasProfilePicture` all mirror the pre-existing correct `DMProfileViewModel.HasProfilePicture` pattern
- Fixed the write-path regression risk flagged in 62-RESEARCH.md Pitfall 4: Create POST actions now stage uploaded bytes in a local variable and assign the mapped Domain model's byte[] property post-map, so new-character/new-contact photo uploads still persist correctly
- Fixed the cosmetic regression flagged in Pitfall 2: DM Edit Profile's current-image thumbnail sources `HasProfilePicture` from the projected Domain bool instead of a now-permanently-null byte[] property
- All 15 views (7 Character, 6 Contact, 2 DM EditProfile — desktop + mobile pairs) gate portrait rendering on the boolean; the `<img>` endpoints themselves (Phase 46) were untouched
- 8 new integration tests prove the boolean gate drives rendering end-to-end and that Create-POST still persists the original upload

## Task Commits

Each task was committed atomically:

1. **Task 1: Convert the three display-path ViewModels to booleans and update the two Domain->ViewModel AutoMapper maps** - `1b862351` (feat)
2. **Task 2: Fix the two Create-POST local-variable staging and the DM controller HasProfilePicture source expressions** - `2d17036c` (fix)
3. **Task 3: Update the 15 affected Razor views to the boolean gate and extend controller integration tests** - `4f37b06e` (test)

_Note: Task 1 intentionally left the Service project non-compiling (controller/view references to the removed byte[] properties) — this was explicitly anticipated by the plan's acceptance criteria and resolved by Tasks 2 and 3 in sequence._

## Files Created/Modified
- `QuestBoard.Service/ViewModels/CharacterViewModels/CharacterViewModel.cs` - `ProfilePicture` byte[]? → `HasProfilePicture` bool
- `QuestBoard.Service/ViewModels/ContactViewModels/ContactViewModel.cs` - `ContactImage` byte[]? → `HasContactImage` bool
- `QuestBoard.Service/ViewModels/DungeonMasterViewModels/EditDMProfileViewModel.cs` - `ProfilePicture` byte[]? → `HasProfilePicture` bool
- `QuestBoard.Service/Automapper/ViewModelProfile.cs` - Contact→ContactViewModel maps HasContactImage; both reverse (ViewModel→Domain) maps ignore the byte[] property
- `QuestBoard.Service/Controllers/Characters/CharactersController.cs` - Create POST stages upload in `uploadedOriginalImageData` local, assigns `character.ProfilePicture` post-map
- `QuestBoard.Service/Controllers/Contacts/ContactsController.cs` - identical pattern for `contact.ContactImageData`
- `QuestBoard.Service/Controllers/DungeonMaster/DungeonMasterController.cs` - Profile/EditProfile GET source `HasProfilePicture` from `profile?.HasProfilePicture ?? false`
- 15 Razor views under `Views/Characters`, `Views/Contacts`, `Views/DungeonMaster` - `!= null` / `?.Length > 0` gates replaced with the boolean property name
- 3 integration test files - 8 new tests covering the boolean gate and Create-POST persistence

## Decisions Made
- `ContactViewModel -> Contact` reverse map: `ContactImageData` explicitly `Ignore()`'d (not left to AutoMapper convention) — the bool must never attempt to round-trip onto the Domain byte[] property (Pitfall 4 from research)
- `CharacterViewModel -> Character` reverse map: `ProfilePicture` explicitly `Ignore()`'d for the same reason — Create controller sets it directly on the mapped Domain model after `mapper.Map`

## Deviations from Plan

None - plan executed exactly as written. The plan's own acceptance criteria for Task 1 anticipated the intermediate non-compiling state, so no deviation tracking is needed for that.

## Issues Encountered

None.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- Phase 62 is now fully complete across all 3 plans (62-01 backend read-projection, 62-02 write-path original-image re-fetch fix, 62-03 this display-path ViewModel conversion)
- `grep -rn "ProfilePicture != null|ContactImage != null|ProfilePicture?.Length > 0"` across `Views/Characters`, `Views/Contacts`, `Views/DungeonMaster` returns zero matches — verified directly, not just asserted
- Full solution build: 0 warnings, 0 errors
- Filtered integration test run (Characters/Contacts/DungeonMaster controllers): 75/75 passing, including the 8 new tests this plan added
- Manual UAT items from the plan's `<verification>` section (edit without touching photo still serves the photo; DM Edit Profile thumbnail renders; create-with-photo displays) are covered by the automated integration tests added in Task 3, but a live-browser pass is still recommended before closing the phase per this project's usual verification practice

---
*Phase: 62-never-load-image-bytes-as-part-of-entity-list-queries-fetch-*
*Completed: 2026-07-07*
