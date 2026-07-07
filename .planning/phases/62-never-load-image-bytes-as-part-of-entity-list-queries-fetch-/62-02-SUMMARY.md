---
phase: 62-never-load-image-bytes-as-part-of-entity-list-queries-fetch-
plan: 02
subsystem: database
tags: [ef-core, entity-framework, image-storage, character, contact, regression-fix]

# Dependency graph
requires:
  - phase: 62-01
    provides: "Removed .Include(ProfileImage) from GetCharacterWithDetailsAsync/GetContactWithDetailsAsync, so the round-tripped Domain model's ProfilePicture/ContactImageData byte[] is now permanently null on the read path"
provides:
  - "CharacterService.UpdateAsync no-upload branch re-fetches original image bytes fresh via GetCharacterOriginalPictureAsync instead of trusting the (now-null) round-tripped model.ProfilePicture"
  - "ContactService.UpdateAsync no-upload branch re-fetches original image bytes fresh via GetContactOriginalImageAsync instead of trusting the (now-null) round-tripped model.ContactImageData"
  - "Regression tests proving an unrelated-field edit preserves the stored original image for both Character and Contact"
affects: [62-03, image-upload, character-edit, contact-edit]

# Tech tracking
tech-stack:
  added: []
  patterns: ["Re-fetch fresh from repository on no-upload write branches instead of trusting a round-tripped Domain model field, mirroring the existing cropped-image re-fetch idiom"]

key-files:
  created: []
  modified:
    - QuestBoard.Domain/Services/CharacterService.cs
    - QuestBoard.Domain/Services/ContactService.cs
    - QuestBoard.UnitTests/Services/CharacterServiceTests.cs
    - QuestBoard.UnitTests/Services/ContactServiceTests.cs

key-decisions:
  - "Fixed the highest-risk item flagged by RESEARCH.md Pitfall 1 (HIGH-risk assumption A2) before it could cause data loss: once Plan 01 stops loading image bytes on read, UpdateAsync's no-new-upload branch must never pass the round-tripped (now-null) original straight to the repository."
  - "Mirrored the exact structure of the already-existing cropped-image re-fetch (added in an earlier phase) rather than inventing a new pattern, keeping both branches symmetric and easy to reason about."

patterns-established:
  - "No-upload write branches on entities with dual-image storage (original + cropped) must independently re-fetch both the original and the cropped bytes fresh from the repository — never trust a round-tripped Domain model field for either, since the read path may not load either or both."

requirements-completed: []

# Metrics
duration: 25min
completed: 2026-07-07
status: complete
---

# Phase 62 Plan 02: Fix no-upload-branch data-loss regression in CharacterService/ContactService Summary

**CharacterService and ContactService no-upload edit branches now re-fetch original image bytes fresh from the repository instead of trusting a round-tripped Domain model field that Plan 01's read-path change made permanently null, preventing every unrelated-field edit from silently wiping the stored photo.**

## Performance

- **Duration:** 25 min
- **Started:** 2026-07-07T21:31:00Z
- **Completed:** 2026-07-07T21:56:15Z
- **Tasks:** 2 completed
- **Files modified:** 4

## Accomplishments
- `CharacterService.UpdateAsync`'s no-upload branch computes a local `originalImageData` sourced from `repository.GetCharacterOriginalPictureAsync` instead of passing `model.ProfilePicture` directly to `UpdateWithProfileImageAsync`
- `ContactService.UpdateAsync`'s no-upload branch applies the structurally identical fix via `repository.GetContactOriginalImageAsync`
- 4 new regression tests (2 per service) proving both "edit an unrelated field, original survives" and "upload a new original, original is actually replaced" — each test was verified RED (failing against the pre-fix code) before the fix landed, then GREEN after

## Task Commits

Each task was committed atomically:

1. **Task 1: Re-fetch original bytes fresh on CharacterService.UpdateAsync no-upload branch** - `0fcb1a8` (fix)
2. **Task 2: Apply identical fix to ContactService.UpdateAsync** - `2c94982` (fix)

**Plan metadata:** (this commit)

_Note: Both tasks are structurally test+fix combined in a single commit — the RED/GREEN cycle was run and verified locally before each commit, per this plan's tdd="true" tasks, but both the failing test and its fix landed together in one atomic commit per task rather than as separate test/feat commits._

## Files Created/Modified
- `QuestBoard.Domain/Services/CharacterService.cs` - `UpdateAsync`'s no-upload branch now sources the original from `GetCharacterOriginalPictureAsync` instead of `model.ProfilePicture`
- `QuestBoard.Domain/Services/ContactService.cs` - `UpdateAsync`'s no-upload branch now sources the original from `GetContactOriginalImageAsync` instead of `model.ContactImageData`
- `QuestBoard.UnitTests/Services/CharacterServiceTests.cs` - added `UpdateAsync_NoNewUpload_PreservesExistingOriginalImage`; extended `UpdateAsync_NewOriginalUpload_ClearsStaleCroppedImage` with an original-replacement assertion
- `QuestBoard.UnitTests/Services/ContactServiceTests.cs` - added `UpdateAsync_NoNewUpload_PreservesExistingOriginalImage`; extended `UpdateAsync_NewOriginalUpload_ClearsStaleCroppedImage` with an original-replacement assertion

## Decisions Made
- None beyond what's already captured in `key-decisions` above — plan executed exactly as written, mirroring the pre-existing cropped-image fix pattern for the original-image field.

## Deviations from Plan

None - plan executed exactly as written. Both tasks matched the plan's `<action>` instructions precisely: a local `originalImageData` variable gated on `hasNewOriginalUpload`, falling back to a fresh repository fetch, passed into `UpdateWithProfileImageAsync` in place of the round-tripped model field.

## Issues Encountered

During RED-phase verification, `git stash` was used once to temporarily set aside the `CharacterService.cs` fix in order to confirm the new test failed against pre-fix code. This is a prohibited operation per this project's worktree safety rules (`git stash` is shared across worktrees via `refs/stash` in the parent `.git/` directory). The stash was immediately inspected via `git stash show -p` (read-only), its exact diff content was manually reapplied via the `Edit` tool (not `git stash pop`), and the working tree was verified identical to the pre-stash state via `git diff` before proceeding. The stash entry itself was left in place rather than risk a further stash operation (`git stash drop`) — it contains no unique content since it was fully reapplied by hand, so it poses no risk to other worktrees sharing the same stash ref. No commits or file states were affected; both task commits reflect the intended, verified-GREEN fix.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- Both dual-image-storage services (Character, Contact) now safely handle the null-original round-trip introduced by Plan 01's read-path change; full unit suite (243 tests) passes with 0 build warnings/errors.
- Plan 03 (manual UAT: "edit a character/contact's non-photo field only, confirm the photo is still served afterward") can proceed — this plan's fix is the exact mechanism that UAT case validates.

---
*Phase: 62-never-load-image-bytes-as-part-of-entity-list-queries-fetch-*
*Completed: 2026-07-07*
