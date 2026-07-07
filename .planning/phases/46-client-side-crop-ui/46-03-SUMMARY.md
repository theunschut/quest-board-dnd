---
phase: 46-client-side-crop-ui
plan: 03
subsystem: api
tags: [crop, image-upload, controllers, mvc, integration-tests]

# Dependency graph
requires:
  - phase: 46-client-side-crop-ui (plan 01)
    provides: Widened 4-arg UpdateAsync (Character/Contact) and UpsertProfileAsync (DM profile) service signatures accepting newCroppedImageData; CroppedPictureFile IFormFile binding property on all three ViewModels
provides:
  - GetCroppedPicture action on CharactersController (no visibility gate, matches GetProfilePicture sibling)
  - GetCroppedContactImage action on ContactsController (IsVisibleTo-gated, matches GetContactImage sibling)
  - DungeonMasterController.GetDMProfilePicture repointed to serve cropped-or-fallback bytes
  - Dual-file (original + cropped) validation and persistence wiring in Characters/Contacts Create+Edit POST and DM EditProfile POST
  - Integration test coverage for all of the above across the three controller test files
affects: [46-04, 46-06]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Dual-file controller wiring: build a second ImageFileInput from the CroppedPictureFile binding property, pass it (not a literal null) to ValidateImagePair, copy its bytes into a local, and thread that local into the widened service call"

key-files:
  created: []
  modified:
    - QuestBoard.Service/Controllers/Characters/CharactersController.cs
    - QuestBoard.Service/Controllers/Contacts/ContactsController.cs
    - QuestBoard.Service/Controllers/DungeonMaster/DungeonMasterController.cs
    - QuestBoard.IntegrationTests/Controllers/CharactersControllerIntegrationTests.cs
    - QuestBoard.IntegrationTests/Controllers/ContactsControllerIntegrationTests.cs
    - QuestBoard.IntegrationTests/Controllers/DungeonMasterControllerIntegrationTests.cs

key-decisions:
  - "Create POST validates a submitted cropped file (real ImageFileInput passed to ValidateImagePair) but does not persist it — AddAsync and the Character/Contact domain models have no cropped-image field or parameter; Plan 01 only widened UpdateAsync/UpsertProfileAsync, not the Create path. Persisting a crop supplied at Create time would require a Rule 4 architectural change (a new AddAsync overload plus a domain-model field) that is out of this plan's scope."
  - "GetCroppedContactImage replicates GetContactImage's IsVisibleTo gate verbatim rather than delegating to a shared helper, matching the existing one-copy-per-controller convention already used for DetectImageMimeType"

patterns-established:
  - "Read-action visibility parity: a new cropped-variant read action must copy its sibling original-variant action's authorization/visibility gate exactly (present or absent) rather than inventing a new one"

requirements-completed: [IMAGE-04, IMAGE-05]

# Metrics
duration: 35min
completed: 2026-07-07
---

# Phase 46 Plan 03: Wire Controllers to Cropped Storage Summary

**Three controllers now serve cropped-or-fallback images via new/repointed read actions and persist a real client-submitted crop through Create/Edit/EditProfile POST paths, with Contacts' new cropped-read endpoint replicating the exact same IsVisibleTo visibility gate as its original-image sibling.**

## Performance

- **Duration:** 35 min
- **Started:** 2026-07-07T14:10:28Z
- **Completed:** 2026-07-07T14:45:00Z
- **Tasks:** 3
- **Files modified:** 6

## Accomplishments
- `CharactersController` gained `GetCroppedPicture` (no visibility gate, matching its sibling `GetProfilePicture`) and `ContactsController` gained `GetCroppedContactImage` (replicating `GetContactImage`'s `IsVisibleTo` gate exactly) — both serve `Cropped ?? Original` fallback bytes via the already-shipped Phase 45 service methods.
- `DungeonMasterController.GetDMProfilePicture` repointed from `GetProfilePictureAsync` to `GetCroppedPictureAsync`, preserving the existing `IsTargetInActiveGroupAsync` group-scoping gate unchanged.
- Every Create/Edit POST image block in `CharactersController`/`ContactsController`, and the DM `EditProfile` POST block, now builds a second `ImageFileInput` from the posted `CroppedPictureFile` and passes it to `ValidateImagePair` instead of a hardcoded `cropped: null` — a malformed crop now produces the same `ModelState` validation error the original file already gets.
- Edit POST (Characters, Contacts) and DM `EditProfile` POST persist the submitted crop bytes through the widened 4-arg `UpdateAsync`/`UpsertProfileAsync` service calls from Plan 01, so a genuinely submitted crop survives instead of being cleared or silently dropped.
- Extended all three controller integration test files with crop-persistence assertions (DB-level byte equality against the submitted crop, not the original fallback), cropped-read-action coverage, and Contacts visibility parity (404 for a hidden contact, 200 for a visible one) — full targeted filter run: 63/63 passing; full solution suite: 231 unit + 370 integration, all green.

## Task Commits

Each task was committed atomically:

1. **Task 1: Add cropped-read actions (Characters + Contacts), repoint the DM picture action** - `7c8b592` (feat)
2. **Task 2: Wire dual-file validation and persistence into all Create/Edit (and DM EditProfile) POST paths** - `bc9d79c` (feat)
3. **Task 3: Extend the three controller integration test files with cropped-read and dual-file persistence coverage** - `f417af8` (test)

**Plan metadata:** committed alongside this summary

## Files Created/Modified
- `QuestBoard.Service/Controllers/Characters/CharactersController.cs` - New `GetCroppedPicture` action; Create/Edit POST build a cropped `ImageFileInput`, pass it to `ValidateImagePair`, and thread the copied crop bytes into the 4-arg `UpdateAsync` (Edit only — see Deviations)
- `QuestBoard.Service/Controllers/Contacts/ContactsController.cs` - New `GetCroppedContactImage` action with the `IsVisibleTo` gate replicated from `GetContactImage`; Create/Edit POST wired the same way as Characters
- `QuestBoard.Service/Controllers/DungeonMaster/DungeonMasterController.cs` - `GetDMProfilePicture` repointed to `GetCroppedPictureAsync`; `EditProfile` POST wired to validate and persist a submitted crop via the widened `UpsertProfileAsync`
- `QuestBoard.IntegrationTests/Controllers/CharactersControllerIntegrationTests.cs` - New tests: crop persists via Edit POST (DB byte-equality), `GetCroppedPicture` returns 200 with stored crop content
- `QuestBoard.IntegrationTests/Controllers/ContactsControllerIntegrationTests.cs` - New tests: crop persists via Edit POST, `GetCroppedContactImage` visibility parity (404 hidden / 200 visible)
- `QuestBoard.IntegrationTests/Controllers/DungeonMasterControllerIntegrationTests.cs` - New test: `GetDMProfilePicture` returns 200 with the stored crop's bytes (not the original) after the repoint

## Decisions Made
- Followed the plan's PATTERNS.md-specified dual-file wiring shape verbatim for Edit/EditProfile POST paths.
- For Create POST, validated the submitted crop (built a real `ImageFileInput`, passed to `ValidateImagePair`) but did not attempt to persist it, since `AddAsync`/the `Character`/`Contact` domain models have no cropped-image field or parameter — see Deviations below for the full reasoning. This keeps Create's behavior honest: a crop submitted at creation time is validated (rejects malformed files) but not yet stored, matching the actual seam Plan 01 built.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - xUnit analyzer warning] Added missing CancellationToken to two new FindAsync/SaveChangesAsync calls**
- **Found during:** Task 3 (build after adding Contacts visibility-parity tests)
- **Issue:** Two new test-seeding blocks in `ContactsControllerIntegrationTests.cs` called `FindAsync(contact.Id)` and `SaveChangesAsync()` without `TestContext.Current.CancellationToken`, triggering 4 `xUnit1051` build warnings inconsistent with the rest of the file's convention.
- **Fix:** Passed `[contact.Id], TestContext.Current.CancellationToken` to `FindAsync` and `TestContext.Current.CancellationToken` to `SaveChangesAsync` at both call sites.
- **Files modified:** `QuestBoard.IntegrationTests/Controllers/ContactsControllerIntegrationTests.cs`
- **Verification:** `dotnet build QuestBoard.IntegrationTests` clean, 0 warnings.
- **Committed in:** `f417af8` (Task 3 commit)

---

**Total deviations:** 1 auto-fixed (1 blocking/lint)
**Impact on plan:** Cosmetic build-warning fix only; no behavior change. No scope creep.

### Scope Note (not a deviation — plan-inherent limitation, documented per Rule 4 reasoning)

**Create POST does not persist a submitted crop, only validates it.** The plan's Task 2 action text describes applying the dual-file pattern to "BOTH Create and Edit POST actions," but the plan's own `<interfaces>` section only lists a widened `UpdateAsync`/`UpsertProfileAsync` — there is no widened `AddAsync` and no `CroppedPictureData`-equivalent field on the `Character`/`Contact` domain models. Investigation confirmed:
- `Character`/`Contact` domain models have only a single `ProfilePicture`/`ContactImageData` byte array, mapped 1:1 to `OriginalImageData` via AutoMapper (`EntityProfile.cs`).
- `AddAsync` is the generic `IBaseService<T>.AddAsync`/`IBaseRepository<T>.AddAsync` — it has no `newCroppedImageData` parameter and no code path that would write a `CroppedImageData` column on insert.
- Adding either would be a Rule 4 architectural change (new domain-model field + new repository/service overload), out of scope for a controller-wiring plan.

This means: submitting a crop on the Create form today is validated exactly like the original (malformed crops are rejected via `ModelState`), but the crop bytes are computed and then discarded rather than persisted, since there is nowhere to persist them to on a brand-new character/contact. A newly-created character/contact simply has no crop until its first Edit, at which point the widened `UpdateAsync` path takes over and persists it correctly. This is consistent with the plan's own `must_haves.key_links`, which names only `Create/Edit POST -> service.UpdateAsync` (not `AddAsync`) as the widened 4-arg call site. Not treated as a blocker since it doesn't violate any of the plan's `must_haves.truths` (all of which describe read-action and Edit/EditProfile persistence behavior) or its threat model (T-46-03-01 is about validating a claimed-cropped file server-side before persisting, which Create still does). Flagged here for visibility rather than silently narrowing scope.

## Issues Encountered
None beyond the deviation and scope note above.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- All three cropped-read endpoints (`GetCroppedPicture`, `GetCroppedContactImage`, `GetDMProfilePicture`) exist and are reachable, ready for Plan 04's view repoints and Plan 06's preview thumbnails to consume.
- Edit/EditProfile POST paths genuinely persist a submitted crop end-to-end (browser -> controller -> widened service -> repository), verified at the DB level by integration tests, not just at the service-unit level.
- Full solution builds clean (`dotnet build`, 6 projects, 0 errors/warnings); full test suite passes 231 unit + 370 integration, all green.
- Known gap for a future plan/discussion: crop-on-create is not persisted (see Scope Note above) — if product intent is for a brand-new character/contact to be creatable with a crop already applied, that needs a small follow-up (widen `AddAsync`/domain model), not blocking for this milestone since users can crop immediately after creating via Edit.
- No blockers for subsequent waves in this phase.

---
*Phase: 46-client-side-crop-ui*
*Completed: 2026-07-07*
