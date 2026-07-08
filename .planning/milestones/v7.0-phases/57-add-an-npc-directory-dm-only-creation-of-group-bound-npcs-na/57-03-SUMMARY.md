---
phase: 57-add-an-npc-directory-dm-only-creation-of-group-bound-npcs-na
plan: 03
subsystem: domain-repository
tags: [ef-core, automapper, repository, service, contacts, multi-tenancy]

# Dependency graph
requires:
  - ContactEntity, ContactImageEntity, ContactNoteEntity EF Core entities (57-02)
  - QuestBoardContext fail-closed group query filters for Contact entities (57-02)
provides:
  - Contact/ContactNote domain models
  - IContactRepository/IContactService interfaces
  - ContactRepository/ContactService implementations
  - EntityProfile Entity<->Domain mappings for Contact/ContactNote
  - DI registrations for IContactService/IContactRepository
affects: [57-04, 57-05, 57-06]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Repository methods carry no visibility parameters (includeHidden/currentUserId/viewerIsDmTier) -- group scoping is enforced entirely by the entity's fail-closed query filter; the hidden/reveal 3-branch visibility rule is deferred to the controller layer (Plan 04+), per RESEARCH.md Pitfall 2"
    - "Dedicated AddNoteAsync/UpdateNoteAsync/DeleteNoteAsync methods manipulate the ContactNotes DbSet directly, avoiding AutoMapper's child-collection replacement problem (RESEARCH.md Pitfall 4)"
    - "Notes ordered newest-first (OrderByDescending(CreatedAt)) applied post-map since AutoMapper doesn't guarantee collection ordering"

key-files:
  created:
    - QuestBoard.Domain/Models/Contact.cs
    - QuestBoard.Domain/Interfaces/IContactRepository.cs
    - QuestBoard.Domain/Interfaces/IContactService.cs
    - QuestBoard.Domain/Services/ContactService.cs
    - QuestBoard.Repository/ContactRepository.cs
  modified:
    - QuestBoard.Repository/Automapper/EntityProfile.cs
    - QuestBoard.Domain/Extensions/ServiceExtensions.cs
    - QuestBoard.Repository/Extensions/ServiceExtensions.cs
    - QuestBoard.UnitTests/Repository/ContactRepositoryTests.cs

key-decisions:
  - "Repository method signatures match the Wave 0 test spec exactly (GetAllContactsWithDetailsAsync(token), GetContactWithDetailsAsync(id, token), GetContactImageAsync(id, token)) rather than the wider includeHidden/currentUserId/viewerIsDmTier signature sketched in 57-03-PLAN.md's artifacts_produced section -- the three-branch hidden/reveal visibility rule is a controller-layer concern (RESEARCH.md Pitfall 2 explicitly says this cannot live in the repository), so it is deferred to the plan that builds ContactsController"
  - "Notes reconciliation intentionally excluded from UpdateAsync -- dedicated Add/Update/Delete methods on the ContactNotes DbSet instead, per RESEARCH.md Pitfall 4"

requirements-completed: []

# Metrics
duration: 12min
completed: 2026-07-06
status: complete
---

# Phase 57 Plan 03: Contacts Domain/Repository Layer Summary

**Contact/ContactNote domain models, IContactRepository/IContactService, ContactRepository (three-branch-ready group-scoped queries + dedicated note methods bypassing AutoMapper's child-collection bug), ContactService, AutoMapper Entity<->Domain wiring, and both DI registrations -- makes ContactRepositoryTests.cs (Plan 01) compile and pass all 7 facts.**

## Performance

- **Duration:** 12 min
- **Started:** 2026-07-06T19:41:00Z
- **Completed:** 2026-07-06T19:53:00Z
- **Tasks:** 2
- **Files modified:** 9 (5 created, 4 modified)

## Accomplishments
- Created `Contact` domain model (Id, Name, ContactImageData, Description, TownCity, SubLocation, IsRevealed, CreatedByUserId, CreatedByUser, CreatedAt, GroupId, Notes) and nested `ContactNote` (Id, ContactId, Text, AuthorUserId, AuthorName, CreatedAt, UpdatedAt), mirroring `Character.cs`'s shape
- Created `IContactRepository`/`IContactService` with the exact method surface `ContactRepositoryTests.cs` requires: `GetAllContactsWithDetailsAsync`, `GetContactWithDetailsAsync`, `GetContactImageAsync`, `UpdateProfileImageAsync`, `AddNoteAsync`, `UpdateNoteAsync`, `DeleteNoteAsync`
- Implemented `ContactRepository`: alphabetical-by-name ordering (D-17), notes materialized newest-first (D-10), group scoping enforced entirely by `ContactEntity`'s Plan 02 fail-closed query filter (no manual `.Where(GroupId ==...)`), dedicated note methods operating directly on the `ContactNotes` DbSet (never routed through `UpdateAsync`, per RESEARCH.md Pitfall 4)
- Implemented `ContactService` as a thin delegating wrapper mirroring `CharacterService`
- Wired `EntityProfile`: `CreateMap<Contact, ContactEntity>`/reverse with image byte projection, `CreateMap<ContactNote, ContactNoteEntity>`/reverse with `AuthorName` projected from the `Author` navigation
- Registered `IContactService`/`IContactRepository` in both `ServiceExtensions.cs` files
- All 7 `ContactRepositoryTests` facts pass; full `QuestBoard.UnitTests` suite (190 tests) passes with zero regressions

## Task Commits

Each task was committed atomically:

1. **Task 1: Contact domain model, IContactRepository, IContactService, AutoMapper EntityProfile mappings** - `39f87df` (feat)
2. **Task 2: ContactRepository/ContactService implementation + DI registration** - `91348f0` (feat)

_Note: this SUMMARY/plan-metadata commit follows separately per worktree execution protocol._

## Files Created/Modified
- `QuestBoard.Domain/Models/Contact.cs` - Contact + ContactNote domain models
- `QuestBoard.Domain/Interfaces/IContactRepository.cs` - repository contract
- `QuestBoard.Domain/Interfaces/IContactService.cs` - service contract
- `QuestBoard.Domain/Services/ContactService.cs` - thin delegating service
- `QuestBoard.Repository/ContactRepository.cs` - three-branch-visibility-ready queries, dedicated note methods, image byte-fetch/update
- `QuestBoard.Repository/Automapper/EntityProfile.cs` - Contact/ContactNote Entity<->Domain mappings
- `QuestBoard.Domain/Extensions/ServiceExtensions.cs` - `IContactService` DI registration
- `QuestBoard.Repository/Extensions/ServiceExtensions.cs` - `IContactRepository` DI registration
- `QuestBoard.UnitTests/Repository/ContactRepositoryTests.cs` - added a missing `using QuestBoard.Domain.Models;` (see Deviations)

## Decisions Made

- **Repository signature matches the Wave 0 test spec, not the plan's artifacts_produced sketch.** 57-03-PLAN.md's `artifacts_produced` section described `GetAllContactsWithDetailsAsync(bool includeHidden, int currentUserId, bool viewerIsDmTier, CancellationToken)` as an explicit three-parameter signature. However, `ContactRepositoryTests.cs` (the actual Wave 0 ground truth this plan is contracted to make pass) calls `GetAllContactsWithDetailsAsync(token)` and `GetContactWithDetailsAsync(id, token)` with no visibility parameters at all, and calls `GetContactImageAsync` directly. RESEARCH.md's own Pitfall 2 explains why: the three-branch hidden/reveal visibility rule (creator-always-sees-own-hidden OR revealed OR DM-tier-with-toggle-on) depends on request-scoped state (current user, resolved DM-tier role, session toggle) that has no business living inside the Repository layer, and must be an explicit `.Where`/filter applied at the controller/service layer once that context is available -- which is Plan 04+'s job (`ContactsController`), not this plan's. This plan therefore implements the two-tier scoping the tests actually specify: (1) group multi-tenancy via the entity's fail-closed query filter (already in place from Plan 02), and (2) `IsRevealed`-aware filtering deferred entirely to the next plan. No behavior was invented beyond what Plans 01/02 already committed to; the interface's XML doc comments spell out this deferral explicitly.
- Followed RESEARCH.md/PATTERNS.md exactly for the ordering/note-method/image-method shapes otherwise.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking fix] Missing `using QuestBoard.Domain.Models;` in ContactRepositoryTests.cs**
- **Found during:** Task 2 verification (`dotnet build QuestBoard.UnitTests`)
- **Issue:** The Wave 0 test file references `ContactNote` (an unqualified type from `QuestBoard.Domain.Models`) at line ~228 but never imported that namespace -- a gap in the Plan 01 scaffold, not a logic error.
- **Fix:** Added the missing `using` directive. No test logic, assertions, or seed data were changed.
- **Files modified:** `QuestBoard.UnitTests/Repository/ContactRepositoryTests.cs`
- **Commit:** `91348f0`

**2. [Repository signature deferral, documented above] Repository methods omit the includeHidden/currentUserId/viewerIsDmTier parameters PLAN.md's artifacts_produced section described.**
- This is not a bug fix but a plan-vs-test-ground-truth reconciliation, fully explained in "Decisions Made" above. No user decision needed -- RESEARCH.md itself (Pitfall 2) already locked this as the correct design, and the Wave 0 test file (which this plan's `must_haves.truths` explicitly requires to "compile and its repository facts pass") is unambiguous about the actual expected signature.

### Known scope boundary (not a deviation, not fixed)

`QuestBoard.IntegrationTests/Controllers/ContactsControllerIntegrationTests.cs` (Plan 01's Wave 0 scaffold) still fails to build -- it references `TestDataHelper.CreateTestContactAsync`/`CreateTestContactNoteAsync`, which don't exist yet. This is explicitly out of scope for this plan: 57-03-PLAN.md's `files_modified` list covers only Domain/Repository files, and Plan 01's own SUMMARY states these two test-helper methods are deferred to "whichever of Plans 02-04 first needs group-scoped Contact test fixtures" -- i.e. the plan that builds `ContactsController` and its ViewModels (Plan 04+). Verified this is a pre-existing gap, not a regression: the only compiler errors in `QuestBoard.IntegrationTests` are the two `TestDataHelper` method-not-found errors, nothing else.

## Issues Encountered

None beyond the deviation documented above. `dotnet build` succeeds for `QuestBoard.Domain`, `QuestBoard.Repository`, `QuestBoard.Service`, and `QuestBoard.UnitTests`. `dotnet test QuestBoard.UnitTests --filter FullyQualifiedName~ContactRepositoryTests` passes 7/7. Full `QuestBoard.UnitTests` suite passes 190/190 with zero regressions.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- `Contact`, `ContactNote`, `IContactRepository`, `IContactService`, `ContactService`, `ContactRepository` are ready for the Service-layer plan (`ContactsController`, ViewModels, session-based "Show Hidden" toggle, `TestDataHelper.CreateTestContactAsync`/`CreateTestContactNoteAsync`).
- The three-branch hidden/reveal visibility rule (D-15) still needs to be implemented as an explicit controller/service-layer filter per RESEARCH.md Pitfall 2 -- this plan deliberately did not attempt it, since it requires request-scoped context (current user, resolved role, session toggle) this layer doesn't have.
- No blockers.

---
*Phase: 57-add-an-npc-directory-dm-only-creation-of-group-bound-npcs-na*
*Completed: 2026-07-06*

## Self-Check: PASSED

- FOUND: QuestBoard.Domain/Models/Contact.cs
- FOUND: QuestBoard.Domain/Interfaces/IContactRepository.cs
- FOUND: QuestBoard.Domain/Interfaces/IContactService.cs
- FOUND: QuestBoard.Domain/Services/ContactService.cs
- FOUND: QuestBoard.Repository/ContactRepository.cs
- FOUND commit: 39f87df
- FOUND commit: 91348f0
- FOUND commit: f8d4f82
