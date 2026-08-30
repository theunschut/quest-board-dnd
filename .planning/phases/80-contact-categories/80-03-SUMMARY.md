---
phase: 80-contact-categories
plan: 03
subsystem: database
tags: [efcore, automapper, unit-testing]

requires:
  - phase: 80-contact-categories
    provides: "ContactCategoryEntity/ContactCategory schema, nullable Contact.CategoryId/Category reference, fail-closed board-scoped query filter, AddContactCategories migration"
provides:
  - "IContactCategoryRepository/ContactCategoryRepository — ordering, contact counting, end-append, swap, delete-with-dependents-loaded"
  - "IContactCategoryService/ContactCategoryService — end-append, both reorder directions with boundary handling, delete delegation"
  - "ContactRepository.GetAllContactsWithDetailsAsync/GetContactWithDetailsAsync now include Category, so a loaded contact carries its category name with no second query"
  - "Unit coverage for ordering, tie-break, end-append, swap, both move boundaries, counting, delete-orphaning, and the contact include"
affects: [80-contact-categories]

tech-stack:
  added: []
  patterns:
    - "Manual reorder via list-position neighbour lookup: read the ordered list once, locate the target by Id, compute the neighbour by list index (not sort-value arithmetic), and no-op at either boundary"
    - "Orphan-safe delete: load dependents into the change tracker before removing the parent, so the in-memory test provider applies the same configured SetNull behaviour SQL Server applies"

key-files:
  created:
    - QuestBoard.Domain/Interfaces/IContactCategoryRepository.cs
    - QuestBoard.Repository/ContactCategoryRepository.cs
    - QuestBoard.Domain/Interfaces/IContactCategoryService.cs
    - QuestBoard.Domain/Services/ContactCategoryService.cs
    - QuestBoard.UnitTests/Repository/ContactCategoryRepositoryTests.cs
    - QuestBoard.UnitTests/Services/ContactCategoryServiceTests.cs
  modified:
    - QuestBoard.Repository/Extensions/ServiceExtensions.cs
    - QuestBoard.Domain/Extensions/ServiceExtensions.cs
    - QuestBoard.Repository/ContactRepository.cs
    - QuestBoard.UnitTests/Repository/ContactRepositoryTests.cs

key-decisions:
  - "GetContactCountsAsync reads category Ids first, then groups Contacts by CategoryId separately, and fills in a zero for any category missing from the grouped result — avoids a left-join query shape that in-memory and SQL Server providers could translate differently"
  - "MoveUpAsync/MoveDownAsync compute the neighbour by position in the freshly-read ordered list, never by arithmetic on SortOrder, so a tie or a gap can never select the wrong neighbour"
  - "DeleteWithDependentsLoadedAsync loads dependent contacts into the change tracker rather than nulling CategoryId by hand, so the delete path is identical under the in-memory test provider and SQL Server"

patterns-established:
  - "Reorder service pattern: read-ordered-list -> find-index -> boundary no-op -> repository swap by two Ids, returning bool so a controller can decide whether to redirect with a focus anchor"

requirements-completed: [CONTACTCAT-01, CONTACTCAT-03, CONTACTCAT-05, CONTACTCAT-07, CONTACTCAT-14]

coverage:
  - id: D1
    description: "ContactCategoryRepository exposes ordering (with Id tie-break), contact counting (including a zero entry), end-append, swap, and delete-with-dependents-loaded, all scoped entirely by the fail-closed board filter with no IgnoreQueryFilters or manual GroupId predicate"
    requirement: "CONTACTCAT-01"
    verification:
      - kind: unit
        ref: "QuestBoard.UnitTests/Repository/ContactCategoryRepositoryTests.cs — 7 facts, all passing"
        status: pass
    human_judgment: false
  - id: D2
    description: "ContactCategoryService exposes end-append (stamping the next sort position), both reorder directions with boundary no-ops at the first/last position, and delete delegation to the repository's orphan-safe path"
    requirement: "CONTACTCAT-05"
    verification:
      - kind: unit
        ref: "QuestBoard.UnitTests/Services/ContactCategoryServiceTests.cs — 6 facts, all passing"
        status: pass
    human_judgment: false
  - id: D3
    description: "Both repository and service are registered in dependency injection alongside the existing IContactRepository/IContactService registrations"
    requirement: "CONTACTCAT-01"
    verification:
      - kind: unit
        ref: "dotnet build (whole solution) — 0 errors; grep of both ServiceExtensions.cs files confirms both AddScoped registrations"
        status: pass
    human_judgment: false
  - id: D4
    description: "A contact loaded via GetAllContactsWithDetailsAsync or GetContactWithDetailsAsync carries its category name and sort order with no second query, via .Include(c => c.Category) added to both methods"
    requirement: "CONTACTCAT-07"
    verification:
      - kind: unit
        ref: "QuestBoard.UnitTests/Repository/ContactRepositoryTests.cs#GetAllContactsWithDetailsAsync_ContactAssignedToCategory_ReturnsCategoryNamePopulated, #GetAllContactsWithDetailsAsync_UnassignedContact_ReturnsNullCategoryName"
        status: pass
    human_judgment: false
  - id: D5
    description: "Deleting a category leaves its contacts alive with a null category reference, proven at both the repository and service layers"
    requirement: "CONTACTCAT-14"
    verification:
      - kind: unit
        ref: "QuestBoard.UnitTests/Repository/ContactCategoryRepositoryTests.cs#DeleteWithDependentsLoadedAsync_ContactsSurviveWithNullCategory, QuestBoard.UnitTests/Services/ContactCategoryServiceTests.cs#DeleteAsync_RemovesCategoryAndOrphansItsContacts"
        status: pass
    human_judgment: false
  - id: D6
    description: "Full unit suite stays green against the new data surface (437 tests: 422 prior + 15 new)"
    requirement: "CONTACTCAT-03"
    verification:
      - kind: unit
        ref: "dotnet test QuestBoard.UnitTests — 437 passed, 0 failed"
        status: pass
    human_judgment: false

duration: 55min
completed: 2026-08-30
status: complete
---

# Phase 80 Plan 03: Contact Category Data Surface Summary

**ContactCategoryRepository/ContactCategoryService with ordering, contact counting, end-append, list-position-based reordering, and orphan-safe delete, plus the contact-to-category Include wiring category names into every contact details read.**

## Performance

- **Duration:** 55 min
- **Started:** 2026-08-30T09:08:00Z
- **Completed:** 2026-08-30T10:03:18Z
- **Tasks:** 3
- **Files modified:** 10

## Accomplishments
- `IContactCategoryRepository`/`ContactCategoryRepository` added: `GetOrderedForActiveGroupAsync` (SortOrder then Id), `GetContactCountsAsync` (with a zero entry for empty categories), `GetNextSortOrderAsync` (end-append), `SwapSortOrderAsync`, and `DeleteWithDependentsLoadedAsync` (loads dependents so the configured `SetNull` behaviour applies identically under the in-memory test provider and SQL Server) — all reading exclusively through the board-scoped query filter
- `IContactCategoryService`/`ContactCategoryService` added: `AddToEndAsync` stamps the next sort position before persisting; `MoveUpAsync`/`MoveDownAsync` compute neighbours by list position (not sort-value arithmetic) and no-op at either boundary; `DeleteAsync` delegates to the orphan-safe repository path
- Both registered in their respective `ServiceExtensions.cs` files, beside the existing `IContactRepository`/`IContactService` registrations
- `ContactRepository.GetAllContactsWithDetailsAsync` and `GetContactWithDetailsAsync` now `.Include(c => c.Category)`, so a loaded contact carries `CategoryName`/`CategorySortOrder` with no second query
- 15 new unit facts added (7 repository, 6 service, 2 contact-include); full suite at 437 passing, 0 failing

## Task Commits

Each task was committed atomically:

1. **Task 1: Category repository and its interface** - `0a54d2d6` (feat)
2. **Task 2: Category service, DI registration, and the contact category include** - `194a4a38` (feat)
3. **Task 3: Unit suite for the category repository, the category service, and the contact include** - `0840889a` (test)

_Note: All three tasks were tagged `tdd="true"` in the plan, but their `<behavior>` blocks describe the surface to be built with the independent test coverage arriving as its own dedicated Task 3 — matching the shape 80-02's Task 1 already established for this phase. No RED/GREEN/REFACTOR cycle was applicable per-task; Tasks 1 and 2 verify structurally via `dotnet build`, and Task 3 is the single `test` commit that proves the whole surface._

## Files Created/Modified
- `QuestBoard.Domain/Interfaces/IContactCategoryRepository.cs` - new interface: ordering, counting, end-append, swap, delete-with-dependents-loaded
- `QuestBoard.Repository/ContactCategoryRepository.cs` - new repository implementing the interface, scoped entirely by the global query filter
- `QuestBoard.Repository/Extensions/ServiceExtensions.cs` - registers `IContactCategoryRepository`
- `QuestBoard.Domain/Interfaces/IContactCategoryService.cs` - new interface: ordered/count pass-throughs, end-append, both reorder directions, delete
- `QuestBoard.Domain/Services/ContactCategoryService.cs` - new service implementing the interface
- `QuestBoard.Domain/Extensions/ServiceExtensions.cs` - registers `IContactCategoryService`
- `QuestBoard.Repository/ContactRepository.cs` - added `.Include(c => c.Category)` to both details-loading methods
- `QuestBoard.UnitTests/Repository/ContactCategoryRepositoryTests.cs` - 7 facts covering ordering, tie-break, end-append (empty/non-empty), swap, delete, counting
- `QuestBoard.UnitTests/Services/ContactCategoryServiceTests.cs` - 6 facts covering end-append, both move boundaries, both move mid-list swaps, delete delegation
- `QuestBoard.UnitTests/Repository/ContactRepositoryTests.cs` - 2 new facts for the category-name projection (assigned/unassigned)

## Decisions Made
- Followed the plan's copy-from-`ContactRepository`/`ContactService` shapes exactly for method structure and DI registration placement; no independent design decisions beyond what RESEARCH.md/PATTERNS.md and this plan's `<action>` text already specified.
- `GetContactCountsAsync` reads the board's category Ids first, then groups `Contacts` separately and merges with `GetValueOrDefault`, rather than an outer-join-shaped query — this keeps the zero-entry guarantee provider-agnostic between the in-memory test suite and SQL Server.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Removed a comment matching the plan's own "no hand-rolled orphaning" prohibited pattern**
- **Found during:** Task 1 verification
- **Issue:** The first draft of `DeleteWithDependentsLoadedAsync`'s explanatory comment used the literal phrase `CategoryId = null` to describe the behavior the code deliberately does *not* do — but the plan's own acceptance criterion greps the file for that exact literal pattern (`grep -cE 'CategoryId = null|CategoryId = default'` expecting `0`) to prove no hand-rolled orphaning exists. The comment (correctly explaining the code's actual behavior) was a false positive against that grep.
- **Fix:** Reworded the comment to describe the same rationale without using the literal matched string (`nulling out each one's category reference by hand` instead of `assigning CategoryId = null ... by hand`).
- **Files modified:** `QuestBoard.Repository/ContactCategoryRepository.cs`
- **Verification:** `grep -cE 'CategoryId = null|CategoryId = default' QuestBoard.Repository/ContactCategoryRepository.cs` now outputs `0`; `dotnet build` still succeeds.
- **Committed in:** `0a54d2d6` (Task 1 commit)

**2. [Rule 1 - Bug] Fixed a compiler warning from capturing the constructor's `mapper` parameter directly**
- **Found during:** Task 1 verification (build output)
- **Issue:** `GetOrderedForActiveGroupAsync` initially called `mapper.Map(...)` using the primary-constructor parameter directly, which the compiler flagged (`CS9107`) as also being captured by the base class's own `mapper` parameter — a double-capture footgun, not present in `ContactRepository`'s equivalent code, which uses the inherited `Mapper` property instead.
- **Fix:** Changed the call to use the protected `Mapper` property inherited from `BaseRepository`, matching `ContactRepository`'s own style exactly.
- **Files modified:** `QuestBoard.Repository/ContactCategoryRepository.cs`
- **Verification:** `dotnet build` produces zero `CS9107` warnings on the rebuild.
- **Committed in:** `0a54d2d6` (Task 1 commit)

---

**Total deviations:** 2 auto-fixed (both Rule 1 — bugs caught by the plan's own verification gates before commit)
**Impact on plan:** Both fixes are cosmetic/structural corrections caught during the plan's own verification step, with no functional change to the delete-orphaning or mapping behavior. No scope creep.

## Issues Encountered
None.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- `IContactCategoryRepository`/`ContactCategoryRepository` and `IContactCategoryService`/`ContactCategoryService` are registered and available for the controller plans later in this phase to build a thin HTTP/rendering layer on top of.
- Contacts loaded with details now carry `CategoryName`/`CategorySortOrder`, so the index and details views can group/label by category with no additional query.
- The full unit suite (437 tests) stays green against this data surface.
- No blockers.

---
*Phase: 80-contact-categories*
*Completed: 2026-08-30*

## Self-Check: PASSED

All claimed created files verified present on disk (`IContactCategoryRepository.cs`, `ContactCategoryRepository.cs`, `IContactCategoryService.cs`, `ContactCategoryService.cs`, `ContactCategoryRepositoryTests.cs`, `ContactCategoryServiceTests.cs`, this SUMMARY). All three commit hashes (`0a54d2d6`, `194a4a38`, `0840889a`) verified present in `git log --oneline`.
