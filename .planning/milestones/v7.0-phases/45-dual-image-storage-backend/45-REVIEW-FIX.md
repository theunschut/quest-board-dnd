---
phase: 45-dual-image-storage-backend
fixed_at: 2026-07-07T00:00:00Z
review_path: .planning/phases/45-dual-image-storage-backend/45-REVIEW.md
iteration: 1
findings_in_scope: 2
fixed: 2
skipped: 0
status: all_fixed
---

# Phase 45: Code Review Fix Report

**Fixed at:** 2026-07-07T00:00:00Z
**Source review:** .planning/phases/45-dual-image-storage-backend/45-REVIEW.md
**Iteration:** 1

**Summary:**
- Findings in scope: 2 (WR-01, WR-02 — `fix_scope: critical_warning`)
- Fixed: 2
- Skipped: 0

Note: `fix_scope` for this run is `critical_warning`, so the 2 Info findings (IN-01, IN-02 — stale/GSD-tagged comments) were intentionally left untouched. They are documented for visibility only, not as skipped-with-reason failures.

## Fixed Issues

### WR-01: Image write and entity write are two un-transacted SaveChanges calls — a mid-request failure leaves a partial update

**Files modified:** `QuestBoard.Domain/Services/CharacterService.cs`, `QuestBoard.Domain/Services/ContactService.cs`, `QuestBoard.Domain/Services/DungeonMasterProfileService.cs`, `QuestBoard.Domain/Interfaces/ICharacterRepository.cs`, `QuestBoard.Domain/Interfaces/IContactRepository.cs`, `QuestBoard.Domain/Interfaces/IDungeonMasterProfileRepository.cs`, `QuestBoard.Repository/CharacterRepository.cs`, `QuestBoard.Repository/ContactRepository.cs`, `QuestBoard.Repository/DungeonMasterProfileRepository.cs`
**Commit:** 1a9d931
**Applied fix:** The review's suggested fix (`dbContext.Database.BeginTransactionAsync`) was adapted rather than applied literally: all repository/service unit tests construct `QuestBoardContext` against EF Core's InMemory provider, which throws `InvalidOperationException` on `BeginTransactionAsync` — the literal suggestion would have broken every existing `CharacterServiceTests`/`ContactServiceTests`/`DungeonMasterProfileServiceTests`/`*RepositoryTests` test that exercises these update paths (the Pitfall 4/5 regression tests called out in the fix instructions).

Instead, each repository now exposes one new combined method (`CharacterRepository.UpdateWithProfileImageAsync`, `ContactRepository.UpdateWithProfileImageAsync`, `DungeonMasterProfileRepository.UpdateBioWithProfileImageAsync`) that loads the entity once, applies both the profile-image mutation and the scalar-field mapping to the same tracked graph, and calls `SaveChangesAsync` exactly once. `CharacterService.UpdateAsync`, `ContactService.UpdateAsync`, and `DungeonMasterProfileService.UpsertProfileAsync` now call this single combined method instead of two independent repository calls, so a failure partway through can no longer leave the image durably committed while the rest of the entity's fields are left stale (or vice versa). This works identically on both the InMemory test provider and SQL Server in production, with no transaction-API dependency and no risk to the existing test suite.

The pre-existing standalone `UpdateProfileImageAsync`/`UpsertProfileImageAsync` methods were left with their original public contract intact (only their inline body was factored into a shared `ApplyProfileImage` private helper), since `CharacterRepositoryTests`/`ContactRepositoryTests`/`DungeonMasterProfileRepositoryTests` call them directly as standalone, immediately-persisting methods independent of a following `UpdateAsync` call.

### WR-02: Split un-transacted SaveChanges in repository UpdateProfileImageAsync/UpdateAsync pairs

**Files modified:** Same file set as WR-01 (both findings describe the same two-write defect from the repository-layer and service-layer perspective respectively, and are fixed by the same code change).
**Commit:** 1a9d931
**Applied fix:** See WR-01 above — the new combined repository methods coalesce what were two independently-saved calls into a single `SaveChangesAsync`, which is exactly WR-02's suggested fix (avoiding a redundant SQL Server round-trip in addition to closing the partial-write window).

## Verification

- `dotnet build`: 6 projects, 0 errors, 0 warnings
- `dotnet test QuestBoard.UnitTests` (targeted: CharacterServiceTests, ContactServiceTests, DungeonMasterProfileServiceTests, CharacterRepositoryTests, ContactRepositoryTests, DungeonMasterProfileRepositoryTests): 34/34 passed
- `dotnet test QuestBoard.UnitTests` (full suite): 225/225 passed
- `dotnet test QuestBoard.IntegrationTests` (targeted: CharactersControllerIntegrationTests, ContactsControllerIntegrationTests): 42/42 passed

## Skipped Issues

None — both in-scope findings (WR-01, WR-02) were fixed.

**Out of scope for this run (not attempted):** IN-01 (planning-doc pitfall references in service comments) and IN-02 (stale "Wave 0 RED scaffold" header in `ContactRepositoryTests.cs`) were excluded by `fix_scope: critical_warning`. As a side effect of the WR-01/WR-02 fix, the `(Pitfall 4)`/`(Pitfall 5)` parenthetical references called out in IN-01 were already removed from `CharacterService.cs` and `ContactService.cs` while rewriting the surrounding comments for the new single-call flow (the `DungeonMasterProfileService.cs` pitfall references were also dropped for the same reason). `IN-01`'s comment in `CharacterRepository.cs` line 93 (referencing "the prior UpdateProfileImageAsync call in CharacterService.UpdateAsync") was also corrected since it became factually stale once the service was changed to call `UpdateWithProfileImageAsync` instead — this was a necessary accuracy fix to the code touched by WR-01, not a deliberate attempt at IN-01. `IN-02` (the `ContactRepositoryTests.cs` header) was not touched.

---

_Fixed: 2026-07-07T00:00:00Z_
_Fixer: Claude (gsd-code-fixer)_
_Iteration: 1_
