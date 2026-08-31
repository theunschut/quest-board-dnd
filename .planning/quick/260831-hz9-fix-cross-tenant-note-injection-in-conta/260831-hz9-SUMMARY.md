---
phase: quick-260831-hz9
plan: 01
subsystem: security
tags: [efcore, query-filters, idor, contacts, aspnetcore]

requires: []
provides:
  - Board-scoped resolve in ContactsController.AddNote before note construction
  - Existence guard in ContactRepository.AddNoteAsync as defence-in-depth against the same hole
  - Route-level and repository-level regression tests for the cross-board AddNote path
affects: [contacts, security]

tech-stack:
  added: []
  patterns:
    - "Note mutations resolve their target through the group-scoped DbSet before acting, mirroring the existing UpdateNoteAsync/DeleteNoteAsync silent-no-op convention"

key-files:
  created: []
  modified:
    - QuestBoard.Service/Controllers/Contacts/ContactsController.cs
    - QuestBoard.Repository/ContactRepository.cs
    - QuestBoard.IntegrationTests/Controllers/ContactsControllerIntegrationTests.cs
    - QuestBoard.UnitTests/Repository/ContactRepositoryTests.cs

key-decisions:
  - "Controller returns NotFound() (404) on a cross-board contactId; repository silently no-ops on an unresolvable ContactId, matching UpdateNoteAsync/DeleteNoteAsync's existing convention for that layer"
  - "Resolve step placed before the ModelState.IsValid check so a foreign id fails closed even when the submitted text is invalid, rather than reaching a redirect built from attacker-controlled input"
  - "Scope intentionally limited to the cross-board write hole only — did not widen to an IsVisibleTo() check, which would change same-board unrevealed-contact behavior (explicitly out of scope per plan)"

patterns-established:
  - "Defence-in-depth: controller-level 404 gate plus a redundant repository-level existence check, so a future caller of AddNoteAsync that skips the controller resolve still cannot commit a cross-board insert"

requirements-completed: [CR-01]

coverage:
  - id: D1
    description: "POST /Contacts/AddNote with a contactId from another board returns 404 and inserts no ContactNote row"
    requirement: "CR-01"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/ContactsControllerIntegrationTests.cs#AddNote_ContactInDifferentGroup_ReturnsNotFoundAndInsertsNoNote"
        status: pass
    human_judgment: false
  - id: D2
    description: "POST /Contacts/AddNote with a contactId on the caller's own board still succeeds exactly as before"
    requirement: "CR-01"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/ContactsControllerIntegrationTests.cs#AddNote_AnyGroupMember_CanAddNoteToVisibleContact"
        status: pass
    human_judgment: false
  - id: D3
    description: "ContactRepository.AddNoteAsync refuses to insert when the target contact does not resolve through the group-scoped Contacts set"
    requirement: "CR-01"
    verification:
      - kind: unit
        ref: "QuestBoard.UnitTests/Repository/ContactRepositoryTests.cs#AddNoteAsync_ContactFromAnotherGroup_InsertsNothing"
        status: pass
    human_judgment: false

duration: 9min
completed: 2026-08-31
status: complete
---

# Quick Task 260831-hz9: Fix cross-tenant note injection in ContactsController.AddNote Summary

**Closed a cross-board IDOR in `ContactsController.AddNote` where a caller-supplied `contactId` bypassed EF Core's `HasQueryFilter` (SELECT-only) and inserted a `ContactNote` against another board's contact — fixed with a controller-level 404 gate plus a redundant repository-level existence check.**

## Performance

- **Duration:** 9 min (13:01 → 13:10 UTC+2, git commit timestamps)
- **Started:** 2026-08-31T13:01:13+02:00
- **Completed:** 2026-08-31T13:09:59+02:00
- **Tasks:** 3
- **Files modified:** 4

## Accomplishments
- `ContactsController.AddNote` now resolves `contactId` through `GetContactWithDetailsAsync` (board-filtered read) and returns `NotFound()` before constructing the note, closing the write path that let an authenticated user on Board A insert a note against a Board B contact by guessing an id from the shared identity sequence.
- `ContactRepository.AddNoteAsync` now runs an `AnyAsync` existence check against the group-scoped `DbContext.Contacts` before inserting, so a future caller that skips the controller-level resolve cannot reintroduce a committed cross-board insert.
- Added a route-level integration test and a repository-level unit test, both confirmed to fail against the unfixed code (manually reverted the two fixes, ran the new tests, saw them fail with a 302 and a persisted foreign-board row respectively, then restored the fixes) before landing.

## Task Commits

Each task was committed atomically:

1. **Task 1: Resolve the contact through the board-filtered read path in AddNote** - `19dbe8dc` (fix)
2. **Task 2: Harden ContactRepository.AddNoteAsync against an unresolvable contact** - `5167167` (fix)
3. **Task 3: Add cross-board regression tests at the route and repository levels** - `f732fb81` (test)

_Note: quick-task docs commit (SUMMARY.md, STATE.md) is handled by the orchestrator, not this executor._

## Files Created/Modified
- `QuestBoard.Service/Controllers/Contacts/ContactsController.cs` - `AddNote` resolves `contactId` via `GetContactWithDetailsAsync` and returns `NotFound()` on a null result, before the `ModelState.IsValid` branch
- `QuestBoard.Repository/ContactRepository.cs` - `AddNoteAsync` performs an `AnyAsync` existence check against `DbContext.Contacts` and no-ops when the contact doesn't resolve
- `QuestBoard.IntegrationTests/Controllers/ContactsControllerIntegrationTests.cs` - added `AddNote_ContactInDifferentGroup_ReturnsNotFoundAndInsertsNoNote`
- `QuestBoard.UnitTests/Repository/ContactRepositoryTests.cs` - added `AddNoteAsync_ContactFromAnotherGroup_InsertsNothing`

## Decisions Made
- Controller returns 404, repository silently no-ops — matches the layer-specific conventions already established by `UpdateNoteAsync`/`DeleteNoteAsync` (silent no-op) and `GetContactImage`/`GetCroppedContactImage` (404 on a null board-filtered read).
- Resolve placed before `ModelState.IsValid` so a foreign id always fails closed regardless of submitted-text validity.
- Deliberately did not widen the gate to an `IsVisibleTo()` check — same-board unrevealed-contact note authoring is existing behavior and out of scope for this fix.

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

None. The plan's revert-check step (Task 3) was performed manually: the two source fixes were temporarily edited back to their unfixed state (not committed), the new tests were run and confirmed to fail (404 assertion got a 302; empty-persistence assertion found the inserted row), then the fixes were restored via the same Edit tool and `git diff` confirmed the restored files were byte-identical to what Tasks 1 and 2 had committed.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- Full solution builds clean (`dotnet build`).
- `QuestBoard.UnitTests` ContactRepositoryTests: 26/26 pass.
- `QuestBoard.IntegrationTests` ContactsControllerIntegrationTests: 62/62 pass.
- No source comment added in this change carries a phase, plan, or review-finding identifier (verified via grep across all three diffs).
- `T-hz9-03` (repudiation — pre-fix notes carry the attacker's real user id) and `T-hz9-04` (404-vs-302 response split) were dispositioned `accept` in the plan's threat register; no further action needed here.

---
*Quick task: 260831-hz9*
*Completed: 2026-08-31*

## Self-Check: PASSED

All 4 modified source/test files and the SUMMARY.md file confirmed present on disk. All 3 task commits (19dbe8dc, 5167167, f732fb81) confirmed present in git log.
