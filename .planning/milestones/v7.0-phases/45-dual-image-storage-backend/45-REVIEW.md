---
phase: 45-dual-image-storage-backend
reviewed: 2026-07-07T12:18:44Z
depth: standard
files_reviewed: 34
files_reviewed_list:
  - QuestBoard.Domain/Extensions/ServiceExtensions.cs
  - QuestBoard.Domain/Interfaces/ICharacterRepository.cs
  - QuestBoard.Domain/Interfaces/ICharacterService.cs
  - QuestBoard.Domain/Interfaces/IContactRepository.cs
  - QuestBoard.Domain/Interfaces/IContactService.cs
  - QuestBoard.Domain/Interfaces/IDungeonMasterProfileRepository.cs
  - QuestBoard.Domain/Interfaces/IDungeonMasterProfileService.cs
  - QuestBoard.Domain/Interfaces/IImageValidationService.cs
  - QuestBoard.Domain/Services/CharacterService.cs
  - QuestBoard.Domain/Services/ContactService.cs
  - QuestBoard.Domain/Services/DungeonMasterProfileService.cs
  - QuestBoard.Domain/Services/ImageValidationService.cs
  - QuestBoard.IntegrationTests/Controllers/CharactersControllerIntegrationTests.cs
  - QuestBoard.IntegrationTests/Controllers/ContactsControllerIntegrationTests.cs
  - QuestBoard.IntegrationTests/Helpers/TestDataHelper.cs
  - QuestBoard.Repository/Automapper/EntityProfile.cs
  - QuestBoard.Repository/CharacterRepository.cs
  - QuestBoard.Repository/ContactRepository.cs
  - QuestBoard.Repository/DungeonMasterProfileRepository.cs
  - QuestBoard.Repository/Entities/CharacterImageEntity.cs
  - QuestBoard.Repository/Entities/ContactImageEntity.cs
  - QuestBoard.Repository/Entities/DungeonMasterProfileImageEntity.cs
  - QuestBoard.Repository/Migrations/20260707111803_RenameImageColumnsAddCropped.Designer.cs
  - QuestBoard.Repository/Migrations/20260707111803_RenameImageColumnsAddCropped.cs
  - QuestBoard.Repository/Migrations/QuestBoardContextModelSnapshot.cs
  - QuestBoard.Service/Controllers/Characters/CharactersController.cs
  - QuestBoard.Service/Controllers/Contacts/ContactsController.cs
  - QuestBoard.Service/Controllers/DungeonMaster/DungeonMasterController.cs
  - QuestBoard.UnitTests/Repository/CharacterRepositoryTests.cs
  - QuestBoard.UnitTests/Repository/ContactRepositoryTests.cs
  - QuestBoard.UnitTests/Repository/DungeonMasterProfileRepositoryTests.cs
  - QuestBoard.UnitTests/Services/CharacterServiceTests.cs
  - QuestBoard.UnitTests/Services/ContactServiceTests.cs
  - QuestBoard.UnitTests/Services/DungeonMasterProfileServiceTests.cs
  - QuestBoard.UnitTests/Services/ImageValidationServiceTests.cs
findings:
  critical: 0
  warning: 2
  info: 2
  total: 4
status: issues_found
---

# Phase 45: Code Review Report

**Reviewed:** 2026-07-07T12:18:44Z
**Depth:** standard
**Files Reviewed:** 34
**Status:** issues_found

## Summary

This phase adds a `CroppedImageData` column alongside the renamed `OriginalImageData` column for
`CharacterImages`, `ContactImages`, and `DungeonMasterProfileImages`, threads a controller-supplied
`hasNewOriginalUpload` signal through `CharacterService`/`ContactService` to decide whether a stale
crop should be cleared or preserved, and consolidates ad-hoc MIME/size validation into a shared
`ImageValidationService`. Per the phase's own scope decision (45-CONTEXT.md D-04), this phase makes
zero view/form changes and does not wire any HTTP-facing endpoint to serve the cropped image — that
is explicitly deferred to Phase 46. That split is confirmed intentional and is not flagged as a
defect here.

The core preserve/clear logic (the phase's primary deliverable) is correct and is exercised by
matching unit and integration tests for all three entities (Character, Contact, DungeonMaster
profile). No security vulnerabilities or crash-causing defects were found in the reviewed diff.

Two real issues were found: a data-integrity risk from splitting a single-entity update into two
un-transacted `SaveChangesAsync` calls (the image write can durably commit while the rest of the
entity update subsequently fails, e.g. on a concurrency conflict), and a violation of this repo's own
CLAUDE.md rule against embedding planning-doc references (`Pitfall 4`/`Pitfall 5`, and a stale
"Wave 0 RED scaffold ... will compile-fail" comment that is provably false for the current, compiling,
passing state of the file) directly in shipped source and test code.

## Warnings

### WR-01: Image write and entity write are two un-transacted SaveChanges calls — a mid-request failure leaves a partial update

**File:** `QuestBoard.Domain/Services/CharacterService.cs:75-93`
**Also affects:** `QuestBoard.Domain/Services/ContactService.cs:30-48`, `QuestBoard.Domain/Services/DungeonMasterProfileService.cs:17-42`

**Issue:** `CharacterService.UpdateAsync(model, hasNewOriginalUpload, token)` calls
`repository.UpdateProfileImageAsync(...)` (which internally calls `DbContext.SaveChangesAsync`) and
then, as a separate step, calls `repository.UpdateAsync(model, token)` (a second, independent
`SaveChangesAsync`). If the second call throws — e.g. a concurrency exception, a validation failure
surfaced only at save time, or a transient SQL Server error — the image (original + crop-clear) has
already been durably committed to the database while the rest of the entity's fields (Name, Level,
Status, Role, SheetLink, Description, Backstory, Classes) were never persisted. The character/contact
row is left in an inconsistent state: new photo, stale metadata. The same two-phase-commit shape
exists in `ContactService.UpdateAsync` and `DungeonMasterProfileService.UpsertProfileAsync`. This
phase touched every one of these methods (to add the `hasNewOriginalUpload`/crop-clearing
parameter) without adding transactional safety around the two writes it introduced/modified.

**Fix:** Wrap both operations in a single transaction (or a single `SaveChangesAsync` if the image
and entity updates are combined into one tracked-entity graph) so a failure in either step rolls
back both:
```csharp
public async Task UpdateAsync(Character model, bool hasNewOriginalUpload, CancellationToken token = default)
{
    await using var transaction = await dbContext.Database.BeginTransactionAsync(token);
    try
    {
        if (hasNewOriginalUpload)
            await repository.UpdateProfileImageAsync(model.Id, model.ProfilePicture, croppedImageData: null, token);
        else
        {
            var existingCropped = await repository.GetCharacterCroppedPictureAsync(model.Id, token);
            await repository.UpdateProfileImageAsync(model.Id, model.ProfilePicture, existingCropped, token);
        }

        await repository.UpdateAsync(model, token);
        await transaction.CommitAsync(token);
    }
    catch
    {
        await transaction.RollbackAsync(token);
        throw;
    }
}
```
(Requires exposing the `DbContext`/an execution-strategy-aware transaction helper through the
repository layer, or moving both writes into a single repository method that performs one
`SaveChangesAsync`.)

### WR-02: Split un-transacted SaveChanges in repository UpdateProfileImageAsync/UpdateAsync pairs

**File:** `QuestBoard.Repository/CharacterRepository.cs:141-168` (UpdateProfileImageAsync), `QuestBoard.Repository/CharacterRepository.cs:84-138` (UpdateAsync)
**Also affects:** `QuestBoard.Repository/ContactRepository.cs:70-120`, `QuestBoard.Repository/DungeonMasterProfileRepository.cs:61-88`

**Issue:** This is the repository-layer half of WR-01: each of `UpdateProfileImageAsync` /
`UpsertProfileImageAsync` and the corresponding `UpdateAsync` independently calls
`DbContext.SaveChangesAsync(token)`. Because both methods operate on the same scoped `DbContext`
instance within a request, the two writes could be coalesced into a single `SaveChangesAsync` (by
having the service layer set both sets of changes on the tracked entities before saving once), which
would both fix the partial-write risk and remove a redundant round-trip to SQL Server.

**Fix:** Expose a repository method that mutates the image navigation and the scalar fields on the
same tracked `CharacterEntity`/`ContactEntity` instance, then calls `SaveChangesAsync` exactly once,
instead of two independently-saved repository calls invoked back-to-back from the service layer.

## Info

### IN-01: Planning-doc references embedded in shipped source comments, against CLAUDE.md's own rule

**File:** `QuestBoard.Domain/Services/CharacterService.cs:80,87`
**Also affects:** `QuestBoard.Domain/Services/ContactService.cs:35,42`, `QuestBoard.Domain/Services/DungeonMasterProfileService.cs:20,25`

**Issue:** CLAUDE.md explicitly states: "Never embed GSD planning/tracking references in source
code — no requirement IDs ... phase/plan numbers ... or review-finding IDs ... in comments." These
three service files contain `// ... (Pitfall 5).` and `// ... (Pitfall 4).` comments that reference
pitfall numbers from `.planning/phases/45-dual-image-storage-backend/45-PATTERNS.md:436`. Once this
phase closes and that planning doc is archived/renumbered, "(Pitfall 4)" and "(Pitfall 5)" become
meaningless noise to any future reader who has no access to (or reason to open) the phase's planning
directory.

**Fix:** Drop the parenthetical pitfall references; the surrounding plain-language explanation
already stands on its own, e.g.:
```csharp
// A genuinely new original arrived this request -- clear any stale crop of the
// superseded photo, since it belonged to the photo that's being replaced.
```

### IN-02: Stale "Wave 0 RED scaffold... will compile-fail" header comment is now provably false

**File:** `QuestBoard.UnitTests/Repository/ContactRepositoryTests.cs:11-15`

**Issue:** The file header states: "this file intentionally references production symbols
(ContactEntity, ContactImageEntity, ContactNoteEntity, ContactRepository, Contact, ContactNote) that
do not exist yet. It will compile-fail until Plans 02-03 land." This comment is left over from an
earlier phase (57) that used a test-first RED-scaffold pattern. This phase's own diff added ~130
lines of new, passing tests to this exact file (`GetContactOriginalAndCroppedImageAsync_...`,
`UpdateProfileImageAsync_NewOriginalWithoutCrop_ClearsStaleCropped`, etc.) that exercise the
already-implemented `ContactRepository`/`ContactImageEntity` — the referenced symbols plainly exist
and the file plainly compiles and passes today. Leaving this stale, self-contradicting comment at
the top of the file misleads any future reader into thinking the file is an intentionally-broken
scaffold, when it is now a normal, green test suite. This is also the same class of stale
phase/plan-reference problem as IN-01 (`Phase 57, Plan 01`, `D-17`, `D-10`, `D-08/D-09` sprinkled
throughout the file), which CLAUDE.md's comment-hygiene rule is meant to prevent.

**Fix:** Remove the "Wave 0 RED scaffold" header block entirely (or replace it with a brief,
evergreen note about group-scoping/ordering if the surrounding tests still need context), and
replace the scattered `D-XX` references in individual test comments with plain-language
justifications, e.g. replace `// Arrange: seed names deliberately out of alphabetical order (D-17:
flat list, alphabetical by Name)` with `// Arrange: seed names deliberately out of alphabetical
order to prove the query sorts by Name, not insertion order`.

---

_Reviewed: 2026-07-07T12:18:44Z_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
