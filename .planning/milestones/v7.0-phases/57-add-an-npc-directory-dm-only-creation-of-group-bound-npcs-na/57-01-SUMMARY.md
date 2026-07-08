---
phase: 57-add-an-npc-directory-dm-only-creation-of-group-bound-npcs-na
plan: 01
subsystem: testing
tags: [xunit, ef-core-inmemory, webapplicationfactory, tdd-red-scaffold, contacts]

# Dependency graph
requires: []
provides:
  - "QuestBoard.UnitTests/Repository/ContactRepositoryTests.cs — RED unit scaffold for ContactRepository (ordering, group scoping, image round-trip, note add/delete)"
  - "QuestBoard.IntegrationTests/Controllers/ContactsControllerIntegrationTests.cs — RED integration scaffold for the full D-09b/D-12/D-13/D-15/D-15b/D-09 visibility-and-auth matrix"
affects: [57-02, 57-03, 57-04]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Wave 0 test-first scaffold: tests reference not-yet-created production types/routes; compile-fail (unit) or 404-at-runtime (integration) is the intended RED state until later plans land"

key-files:
  created:
    - QuestBoard.UnitTests/Repository/ContactRepositoryTests.cs
    - QuestBoard.IntegrationTests/Controllers/ContactsControllerIntegrationTests.cs
  modified: []

key-decisions:
  - "ContactRepositoryTests mirrors CharacterRepositoryTests structure exactly (MutableTestGroupContext, InMemory DbContext, AutoMapper EntityProfile) rather than inventing a new fixture harness"
  - "ContactsControllerIntegrationTests targets ContactsController routes as string literals (not C# symbols), so the file itself compiles today but every [Fact] will 404 until the controller exists — a deliberately different RED shape from the unit test file, since integration tests hit HTTP routes rather than referencing types directly"
  - "The D-15b per-group toggle test relies on the same HttpClient instance carrying the ASP.NET Core Session cookie across sequential requests (confirmed viable per WebApplicationFactoryBase/GroupPickerControllerIntegrationTests precedent), combined with factory.TestGroupContext.ActiveGroupId swaps to simulate switching active groups"
  - "TestDataHelper needs new CreateTestContactAsync/CreateTestContactNoteAsync helper methods (mirroring CreateTestCharacterAsync) — deliberately left unimplemented in this plan since they depend on the not-yet-created ContactEntity/ContactNoteEntity; this is the same intentional Wave 0 compile-fail dependency chain the plan calls out for the unit test file"

patterns-established:
  - "Note ordering test seeds Id order and CreatedAt order in deliberate disagreement, proving the sort key is CreatedAt not Id (mirrors the plan's explicit acceptance criterion)"

requirements-completed: []

# Metrics
duration: 25min
completed: 2026-07-06
status: complete
---

# Phase 57 Plan 01: Wave 0 RED Test Scaffolds Summary

**Two failing test files (ContactRepositoryTests.cs, ContactsControllerIntegrationTests.cs) specify the entire Contact feature's executable contract — ordering, group scoping, image round-trip, and the full D-09b/D-12/D-13/D-15/D-15b/D-09 visibility-and-auth matrix — before any Contact production code exists.**

## Performance

- **Duration:** 25 min
- **Started:** 2026-07-06T19:14:00Z
- **Completed:** 2026-07-06T19:39:14Z
- **Tasks:** 2
- **Files modified:** 2 (both newly created)

## Accomplishments
- `ContactRepositoryTests.cs` specifies 7 facts: alphabetical Index ordering (D-17), newest-first note ordering by `CreatedAt` not `Id` (D-10), group-scoped `GetContactWithDetailsAsync`/`GetAllContactsWithDetailsAsync` cross-group exclusion, image round-trip via `GetContactImageAsync`, and `AddNoteAsync`/`DeleteNoteAsync` round-trip.
- `ContactsControllerIntegrationTests.cs` specifies 21 facts covering: D-09b role gating (Player blocked, DungeonMaster/Admin succeed) across Create/Edit/Delete/ToggleReveal; D-14 default-hidden Contact on Create; D-12/D-13 hidden-Contact 404 and Index exclusion for Players; the full D-15 three-branch visibility rule (creator exception, DM-tier toggle, Player-never); D-15b per-group session-scoped toggle isolation; D-09 note collaboration (any group member can add/edit/delete any note, no ownership guard); and cross-tenant IDOR 404 on `Details/{id}`.
- Both files verified to compile-fail (unit) / fail-to-build-due-to-missing-helpers (integration) for exactly the reasons expected — missing Contact production types and the not-yet-added `TestDataHelper.CreateTestContactAsync`/`CreateTestContactNoteAsync` helpers — with no unrelated typos or harness-usage errors.

## Task Commits

Each task was committed atomically:

1. **Task 1: Author ContactRepositoryTests.cs (RED unit scaffold)** - `9f97488` (test)
2. **Task 2: Author ContactsControllerIntegrationTests.cs (RED integration scaffold)** - `77a187b` (test)

_No TDD GREEN/REFACTOR commits in this plan — this plan's tasks are `type="auto"`, not `tdd="true"`; the RED-only nature is a deliberate Wave 0 scaffold, not a TDD cycle within this plan itself._

## Files Created/Modified
- `QuestBoard.UnitTests/Repository/ContactRepositoryTests.cs` - 7 `[Fact]` methods specifying ordering, group scoping, image round-trip, and note add/delete behavior against not-yet-created `ContactRepository`/`ContactEntity`/`ContactImageEntity`/`ContactNoteEntity`/`Contact`/`ContactNote`
- `QuestBoard.IntegrationTests/Controllers/ContactsControllerIntegrationTests.cs` - 21 `[Fact]` methods specifying the full auth/visibility/note-collaboration matrix against not-yet-created `ContactsController` routes and `TestDataHelper` Contact helpers

## Decisions Made
- Mirrored `CharacterRepositoryTests.cs`'s exact fixture pattern (`MutableTestGroupContext : IActiveGroupContext`, `CreateContext`/`CreateMapper` helpers, seed-with-null-active-group-then-restore convention) rather than introducing any new test infrastructure, per the plan's explicit instruction.
- For the integration test file, used route string literals (`/Contacts/Create`, `/Contacts/Index`, etc.) rather than any compile-time reference to `ContactsController`, since the plan noted this file has no direct compile dependency on controller symbols — it will build today for any part that doesn't also touch `TestDataHelper`/`QuestBoardContext.Contacts`, but will 404 at runtime for every fact once it does build (post Plan 04).
- Added references to two new `TestDataHelper` methods (`CreateTestContactAsync`, `CreateTestContactNoteAsync`) that don't exist yet — this is a deliberate, in-scope RED dependency: the plan's own artifact list defers all Contact production types (including test-helper seed methods that touch those types) to Plans 02-04, and `TestDataHelper` extension is naturally part of that later work, not a gap introduced by this plan.
- Modeled the D-15b per-group toggle isolation test using `factory.TestGroupContext.ActiveGroupId` swaps between requests on the same authenticated `HttpClient`, combined with a real POST to `/Contacts/ToggleShowHidden` — relying on the same HttpClient's cookie jar to carry the ASP.NET Core Session cookie across sequential calls (an established, already-viable pattern per `WebApplicationFactoryBase`'s default client configuration, exercised implicitly by `GroupPickerControllerIntegrationTests.SelectGroup_ShouldPersistActiveGroupInSession`).

## Deviations from Plan

None - plan executed exactly as written. Both tasks specified in 57-01-PLAN.md were completed with no scope changes; the acceptance criteria (5+ facts per unit test class, 9 named behaviors per integration test class, no new test harness/framework introduced) were met with 7 and 21 facts respectively — the integration file's higher count is a decomposition of the plan's 9 numbered behaviors into more granular, single-assertion `[Fact]` methods (e.g. splitting the D-09b block-check into separate Player/DM/Admin facts per action) rather than added scope.

## Issues Encountered

**Build verification confirmed the intended RED state, not a false pass or an unrelated failure:**
- `dotnet build QuestBoard.UnitTests` fails with 30 errors, all `CS0246`/`CS1061` for `ContactEntity`, `ContactImageEntity`, `ContactNoteEntity`, `ContactRepository`, `Contact`, `ContactNote`, `Contacts`/`ContactNotes` DbSets — exactly the symbols the plan says will not exist until Plans 02-03.
- `dotnet build QuestBoard.IntegrationTests` fails with 20 errors, all `CS0117`/`CS1061` for `TestDataHelper.CreateTestContactAsync`, `TestDataHelper.CreateTestContactNoteAsync`, and `QuestBoardContext.Contacts`/`ContactNotes` — again, exclusively Contact-production-type-dependent, no stray typos or incorrect harness usage.
- No other compile errors were present in either file, satisfying the plan's acceptance criterion: "Files compile-fail only because the Contact production types do not yet exist... No other compile errors (typos, wrong harness usage) are acceptable."

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- Plan 02 (entities: `ContactEntity`, `ContactImageEntity`, `ContactNoteEntity`) can proceed immediately; `ContactRepositoryTests.cs` pins the exact shape expected (`GroupId` scoping, `ContactImage` 1:1 navigation, `Notes` collection with `CreatedAt`/`AuthorUserId`/`Text`).
- Plan 03 (domain/repository: `Contact`, `ContactNote`, `IContactRepository`, `ContactRepository`, `ContactService`) has its method surface fully specified by the unit tests: `GetAllContactsWithDetailsAsync`, `GetContactWithDetailsAsync`, `GetContactImageAsync`, `AddNoteAsync`, `DeleteNoteAsync`.
- Plan 04 (service layer: `ContactsController`, ViewModels, `SessionKeys.ShowHiddenContactsKey`) has its full route surface and authorization/visibility matrix specified by the integration tests: `/Contacts/{Index,Details,Create,Edit,Delete,ToggleReveal,ToggleShowHidden,AddNote,EditNote,DeleteNote}`.
- `TestDataHelper.CreateTestContactAsync`/`CreateTestContactNoteAsync` are referenced but not yet implemented — whichever of Plans 02-04 first needs group-scoped Contact test fixtures should add these two helper methods to `QuestBoard.IntegrationTests/Helpers/TestDataHelper.cs`, mirroring the existing `CreateTestCharacterAsync` signature/shape.
- No blockers. Both scaffold files are committed and ready to guide the remaining three plans in this phase.

---
*Phase: 57-add-an-npc-directory-dm-only-creation-of-group-bound-npcs-na*
*Completed: 2026-07-06*

## Self-Check: PASSED

- FOUND: QuestBoard.UnitTests/Repository/ContactRepositoryTests.cs
- FOUND: QuestBoard.IntegrationTests/Controllers/ContactsControllerIntegrationTests.cs
- FOUND: .planning/phases/57-add-an-npc-directory-dm-only-creation-of-group-bound-npcs-na/57-01-SUMMARY.md
- FOUND commit: 9f97488
- FOUND commit: 77a187b
- FOUND commit: 38b88d4
