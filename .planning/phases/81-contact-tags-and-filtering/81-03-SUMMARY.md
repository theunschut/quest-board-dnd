---
phase: 81-contact-tags-and-filtering
plan: 03
subsystem: database
tags: [efcore, contact-tags, many-to-many, multi-tenancy, sql-server]

# Dependency graph
requires:
  - phase: 81-02
    provides: ContactTagEntity, ContactTag domain model, Contact.Tags navigation, ContactContactTags join table, board-scoped unique index and query filter
provides:
  - "ContactRepository.GetAllContactsWithDetailsAsync/GetContactWithDetailsAsync load tags alphabetically under AsSplitQuery"
  - "ContactRepository.ReplaceContactTagsAsync: case-insensitive reconcile-by-name against a board-filtered vocabulary query, creating rows only for unmatched names"
  - "ContactRepository.RemoveAsync override + shared PruneOrphanedTagsAsync helper: orphaned tags deleted on both contact save and contact delete"
  - "ContactService.ReplaceContactTagsAsync pass-through and ContactService.ParseTagNames comma-list parser"
  - "14 new unit tests proving reuse, in-submission dedup, both prune paths, fresh-id-after-prune, cross-board no-op, and parser edge cases against QuestBoardContext directly"
affects: [81-04, 81-05, 81-06]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Board vocabulary resolved via a real filtered query (DbContext.ContactTags.ToListAsync), never by primary-key change-tracker lookup, so a foreign-board tag id/name is structurally unreachable"
    - "In-memory name matching via StringComparer.OrdinalIgnoreCase rather than an in-query equality check, so behavior doesn't depend on provider-specific collation handling"
    - "Shared private prune helper used by both the save path and the delete-override path so orphan pruning cannot drift between the two call sites"

key-files:
  created: []
  modified:
    - QuestBoard.Repository/ContactRepository.cs
    - QuestBoard.Domain/Interfaces/IContactRepository.cs
    - QuestBoard.Domain/Interfaces/IContactService.cs
    - QuestBoard.Domain/Services/ContactService.cs
    - QuestBoard.UnitTests/Repository/ContactRepositoryTests.cs
    - QuestBoard.UnitTests/Services/ContactServiceTests.cs

key-decisions:
  - "Task 1 left ReplaceContactTagsAsync/ParseTagNames as NotImplementedException stubs so the interface contracts could compile and the split-query change could be verified independently before the reconciliation logic landed in task 2"
  - "Orphan pruning re-checks Contacts.Count == 0 across the full previous-tag-id snapshot rather than computing an explicit removed-ids diff first -- functionally identical (a tag still in the new set still has the contact attached) and simpler"

requirements-completed: [CONTACTTAG-03, CONTACTTAG-04, CONTACTTAG-05, CONTACTTAG-13]

coverage:
  - id: D1
    description: "Contacts load their tags alphabetically alongside notes, under AsSplitQuery so the two collections don't cross-join"
    requirement: CONTACTTAG-03
    verification:
      - kind: unit
        ref: "QuestBoard.UnitTests/Repository/ContactRepositoryTests.cs#GetAllContactsWithDetailsAsync_MultipleContacts_ReturnsOrderedAlphabeticallyByName (regression, unaffected by tags)"
        status: pass
      - kind: unit
        ref: "QuestBoard.UnitTests/Repository/ContactRepositoryTests.cs#ReplaceContactTagsAsync_DuplicateCaseVariantsInSameSubmission_CreatesSingleRow (reads back via GetContactWithDetailsAsync-equivalent Include)"
        status: pass
    human_judgment: false
  - id: D2
    description: "A contact's tag list can be reconciled from submitted names: new names create rows, case-variant names reuse the existing row, and duplicates within one submission collapse to a single row"
    requirement: CONTACTTAG-04
    verification:
      - kind: unit
        ref: "QuestBoard.UnitTests/Repository/ContactRepositoryTests.cs#ReplaceContactTagsAsync_NewNames_CreatesTagRowsAndAssociations"
        status: pass
      - kind: unit
        ref: "QuestBoard.UnitTests/Repository/ContactRepositoryTests.cs#ReplaceContactTagsAsync_CaseVariantOfExistingName_ReusesRow"
        status: pass
      - kind: unit
        ref: "QuestBoard.UnitTests/Repository/ContactRepositoryTests.cs#ReplaceContactTagsAsync_DuplicateCaseVariantsInSameSubmission_CreatesSingleRow"
        status: pass
    human_judgment: false
  - id: D3
    description: "Orphaned tags are pruned on both contact save and contact delete, through one shared helper, and a cross-board contact id is a silent no-op"
    requirement: CONTACTTAG-05
    verification:
      - kind: unit
        ref: "QuestBoard.UnitTests/Repository/ContactRepositoryTests.cs#ReplaceContactTagsAsync_LastContactDropsTag_DeletesRow"
        status: pass
      - kind: unit
        ref: "QuestBoard.UnitTests/Repository/ContactRepositoryTests.cs#ReplaceContactTagsAsync_TagStillHeldByAnotherContact_SurvivesPrune"
        status: pass
      - kind: unit
        ref: "QuestBoard.UnitTests/Repository/ContactRepositoryTests.cs#ReplaceContactTagsAsync_ContactOnAnotherBoard_IsSilentNoOp"
        status: pass
      - kind: unit
        ref: "QuestBoard.UnitTests/Repository/ContactRepositoryTests.cs#ReplaceContactTagsAsync_ReAddingPrunedName_MintsNewId"
        status: pass
      - kind: unit
        ref: "QuestBoard.UnitTests/Repository/ContactRepositoryTests.cs#RemoveAsync_ContactWasSoleTagHolder_PrunesTag"
        status: pass
      - kind: unit
        ref: "QuestBoard.UnitTests/Repository/ContactRepositoryTests.cs#RemoveAsync_ContactTagsSharedWithAnotherContact_SurvivesDelete"
        status: pass
    human_judgment: false
  - id: D4
    description: "ContactService.ParseTagNames splits a comma-separated string into trimmed, case-insensitively de-duplicated names, and ReplaceContactTagsAsync passes through to the repository"
    requirement: CONTACTTAG-13
    verification:
      - kind: unit
        ref: "QuestBoard.UnitTests/Services/ContactServiceTests.cs#ParseTagNames_WhitespaceAndDuplicates_TrimsDropsEmptyAndDedupesCaseInsensitively"
        status: pass
      - kind: unit
        ref: "QuestBoard.UnitTests/Services/ContactServiceTests.cs#ParseTagNames_NullInput_ReturnsEmptyList"
        status: pass
      - kind: unit
        ref: "QuestBoard.UnitTests/Services/ContactServiceTests.cs#ParseTagNames_EmptyStringInput_ReturnsEmptyList"
        status: pass
      - kind: unit
        ref: "QuestBoard.UnitTests/Services/ContactServiceTests.cs#ParseTagNames_CommasOnlyInput_ReturnsEmptyList"
        status: pass
      - kind: unit
        ref: "QuestBoard.UnitTests/Services/ContactServiceTests.cs#ReplaceContactTagsAsync_DelegatesToRepository_ReconcilesContactTags"
        status: pass
    human_judgment: false

# Metrics
duration: 15min
completed: 2026-08-31
status: complete
---

# Phase 81 Plan 03: Contact Tag Reconciliation, Pruning, and Parsing Summary

**ContactRepository.ReplaceContactTagsAsync reconciles a contact's tags against a board-filtered vocabulary query (case-insensitive reuse, board-scoped creation), a shared PruneOrphanedTagsAsync helper deletes orphaned tags on both save and delete, and ContactService.ParseTagNames turns a comma-separated string into a trimmed, de-duplicated name list.**

## Performance

- **Duration:** ~15 min
- **Completed:** 2026-08-31
- **Tasks:** 3 completed
- **Files modified:** 6

## Accomplishments
- Both contact detail-fetch methods (`GetAllContactsWithDetailsAsync`, `GetContactWithDetailsAsync`) now load tags alphabetically alongside notes, under `AsSplitQuery()` to avoid the two-collection cartesian blow-up
- `ReplaceContactTagsAsync` resolves every submitted name through a real, board-filtered `ContactTags` query -- never a change-tracker primary-key lookup, never `IgnoreQueryFilters()` -- so a foreign-board tag id or name is structurally unreachable, not just filtered at read time
- Orphaned tags are pruned through one shared private helper used by both `ReplaceContactTagsAsync` (on save) and a new `RemoveAsync` override (on contact delete), so the two paths cannot drift apart
- `ContactService.ParseTagNames` splits, trims, drops empties, and case-insensitively de-duplicates a raw comma-separated string while preserving first-seen casing and input order
- 14 new unit tests assert directly against `QuestBoardContext.ContactTags` (not the returned domain model) for every behavior in the plan's behavior block: new-name creation, case-variant reuse, in-submission dedup, both prune paths, fresh-id-after-prune, and the cross-board silent no-op

## Task Commits

Each task was committed atomically:

1. **Task 1: Load tags with contacts, split-queried, through both interfaces** - `aff76e50` (feat)
2. **Task 2: Tag reconciliation, orphan pruning, and comma-list parsing** - `502f8234` (feat)
3. **Task 3: Unit tests for reuse, pruning, and parsing** - `2fb21583` (test)

**Plan metadata:** worktree mode -- SUMMARY.md committed by this agent; STATE.md/ROADMAP.md updates and the final metadata commit are the orchestrator's responsibility after merge.

## Files Created/Modified
- `QuestBoard.Repository/ContactRepository.cs` - `.Include(c => c.Tags)` + `.AsSplitQuery()` on both detail-fetch methods; `ReplaceContactTagsAsync`; `RemoveAsync` override; shared `PruneOrphanedTagsAsync` helper
- `QuestBoard.Domain/Interfaces/IContactRepository.cs` - Updated XML docs on the detail-fetch methods; declared `ReplaceContactTagsAsync`
- `QuestBoard.Domain/Interfaces/IContactService.cs` - Updated XML docs on the detail-fetch methods; declared `ReplaceContactTagsAsync` and `ParseTagNames`
- `QuestBoard.Domain/Services/ContactService.cs` - `ReplaceContactTagsAsync` pass-through; `ParseTagNames` implementation
- `QuestBoard.UnitTests/Repository/ContactRepositoryTests.cs` - 9 new tests covering reuse, pruning, and the cross-board no-op
- `QuestBoard.UnitTests/Services/ContactServiceTests.cs` - 5 new tests covering `ParseTagNames` edge cases and the service pass-through

## Decisions Made
- Task 1 left `ReplaceContactTagsAsync`/`ParseTagNames` as `NotImplementedException` stubs on both `ContactRepository` and `ContactService` so the interface contracts could compile and the split-query change could be verified independently, per the plan's explicit "implementations land in task 2" instruction
- Orphan pruning re-checks `Contacts.Count == 0` across the full previous-tag-id snapshot rather than first computing an explicit removed-ids diff -- functionally identical, since a tag still in the new set still has the contact attached, and simpler to read
- Board-vocabulary name matching uses `StringComparer.OrdinalIgnoreCase.Equals(t.Name, name)` evaluated in memory (post-`ToListAsync`), not an in-query `string.Equals` with a comparison argument, since EF Core cannot translate the latter to SQL and the plan explicitly calls out that in-query comparisons would behave differently under the in-memory test provider than under SQL Server's real column collation

## Deviations from Plan

None - plan executed exactly as written. Task 1's stub implementations were explicitly anticipated by the plan text ("Implementations land in task 2; this task may leave them as the minimum needed to compile"), not an improvised deviation.

## Issues Encountered

Two test-writing passes hit `CS0136` ("local ... cannot be declared in this scope") when a fresh verification `QuestBoardContext` was declared with `await using var context = ...;` (pattern-based, method-scoped) after an earlier `await using (var context = ...) { }` (explicit-block-scoped) had already used the same name in the same method. Fixed by wrapping every post-arrange verification read in its own explicit `await using (var context = ...) { ... }` block, keeping the variable name `context` block-scoped throughout -- this also satisfies the plan's acceptance criterion requiring the literal string `context.ContactTags` to appear at least four times in the test file.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- The repository/service write path (`ReplaceContactTagsAsync`, `ParseTagNames`) is ready for the controller and view layer (plan 04/05) to build the actual tag-editing UI and index filter on top of
- `dotnet test` (full suite) is green at 454 unit + 674 integration tests -- the new split query and tag Include do not regress any existing contact or note assertion

---
*Phase: 81-contact-tags-and-filtering*
*Completed: 2026-08-31*
