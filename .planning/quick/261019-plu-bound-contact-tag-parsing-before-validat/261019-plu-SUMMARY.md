---
phase: quick-261019-plu
plan: 01
subsystem: contacts
tags: [security, denial-of-service, contacts, tags, validation]
requires: []
provides:
  - Contacts Create/Edit POSTs refuse to split an over-long tag field
  - Linear-time ParseTagNames
  - Cross-board tag no-op repository fact that can fail
affects: [QuestBoard.Service, QuestBoard.Domain, QuestBoard.UnitTests, QuestBoard.IntegrationTests]
tech-stack:
  added: []
  patterns: [single shared length constant, fail-closed ModelState fallback, HashSet de-duplication]
key-files:
  created: []
  modified:
    - QuestBoard.Service/ViewModels/ContactViewModels/ContactViewModel.cs
    - QuestBoard.Service/Controllers/Contacts/ContactsController.cs
    - QuestBoard.Domain/Services/ContactService.cs
    - QuestBoard.IntegrationTests/Controllers/ContactsControllerIntegrationTests.cs
    - QuestBoard.UnitTests/Services/ContactServiceTests.cs
    - QuestBoard.UnitTests/Repository/ContactRepositoryTests.cs
decisions:
  - Bound lives on the view model as TagsInputMaxLength; attribute message uses the {1} placeholder so rendered text is unchanged
  - Controller adds a fail-closed TagsInput ModelState error if the attribute's error is ever absent on the over-bound path
  - "Not parsed" is proven over HTTP (no per-name length error from an over-bound payload), not by timing
metrics:
  tasks: 3
  commits: 4
status: complete
actuals:
  tokens: 14000
  tasks: 3
  commits: 4
---

# Phase quick-261019-plu Plan 01: Bound contact tag parsing before validation Summary

Contacts Create and Edit now refuse to split a tag field longer than 1000 characters (shared `TagsInputMaxLength`), and `ParseTagNames` de-duplicates through a case-insensitive `HashSet` in one linear pass.

## What was done

- **Task 1 (RED + fix):** Four integration facts added. Three over-bound facts failed on the current code, because the payload was parsed and produced the per-name length error. After the fix, both POSTs call `ParseTagsWithinBound`, which skips parsing and per-name validation for an over-bound value and fails closed by adding a `TagsInput` error if none is present. The over-bound re-render still repopulates suggestions and persists nothing.
- **Task 2:** Interleaved-casing characterization fact passed on the old implementation, then the list scan was replaced by a `HashSet`; the 100,000-name fact was added and passes.
- **Task 3:** The cross-board no-op repository fact now reads `ContactTags` through `IgnoreQueryFilters()`, and the misleading "see-all" remark was replaced by an accurate comment. Full sweep run.

## Deviations from Plan

None - plan executed exactly as written. The Edit/Create facts read the database with `IgnoreQueryFilters()` where the plan said plain assertions, so a board-filter quirk in the test scope cannot hide a row (strictly stronger than specified).

## Evidence for closing T-81-12

**Closing commits**
- RED: `bc4e4460` test(261019-plu): add failing facts for the tag field length bound
- fix: `c9cf438d` fix(261019-plu): refuse to parse an over-long contact tag field
- perf: `609ebe5e` perf(261019-plu): de-duplicate parsed contact tag names in linear time
- test: `1435c819` test(261019-plu): assert the cross-board tag no-op past the board filter

**Code locations**
- Helper `ParseTagsWithinBound`: `QuestBoard.Service/Controllers/Contacts/ContactsController.cs:615`
- Call sites: `ContactsController.cs:177` (Create POST) and `ContactsController.cs:300` (Edit POST); `contactService.ParseTagNames(` is now reached only from the helper
- Constant: `QuestBoard.Service/ViewModels/ContactViewModels/ContactViewModel.cs:61` (`TagsInputMaxLength = 1000`), used by the attribute at line 66
- `HashSet`: `QuestBoard.Domain/Services/ContactService.cs:31`

**Tests**
- Integration: `Create_TagsInputOverLengthBound_RerendersWithoutParsingOrPersisting`, `Edit_TagsInputOverLengthBound_RerendersWithoutParsingOrChangingContact`, `Edit_TagsInputOneCharacterOverBound_IsNotParsed`, `Edit_TagsInputExactlyAtBound_SavesEveryTag`
- Unit: `ParseTagNames_InterleavedCaseVariants_KeepsFirstCasingAndInputOrder`, `ParseTagNames_LargeDistinctInputWithRepeats_ReturnsEachNameOnceInOrder`
- Strengthened: `ReplaceContactTagsAsync_ContactOnAnotherBoard_IsSilentNoOp` (asserts through `IgnoreQueryFilters()`)

**Measurements (observed, not asserted)**
- RED run (Task 1): 3 failed (the three over-bound facts, on the `30-character limit` assertion); the at-bound fact passed.
- `ParseTagNames_LargeDistinctInputWithRepeats_ReturnsEachNameOnceInOrder` (100,000 distinct names plus 100,000 upper-cased repeats): 56 ms observed after the change.

**Results**
- ContactsControllerIntegrationTests + ContactsTagsFormMarkupTests (after fix): 71 passed, 0 failed, 0 skipped
- Unit targeted (ContactServiceTests, ContactRepositoryTests, CrossBoardIgnoreQueryFiltersSeamTests): 50 passed, 0 failed, 0 skipped
- Integration targeted (ContactsControllerIntegrationTests, ContactsTags*, AntiForgeryTokenCoverageTests): 91 passed, 0 failed, 0 skipped
- Full unit suite: 1107 passed, 0 failed, 0 skipped
- Full integration suite: 1328 passed, 0 failed, 11 skipped (same 11 skips as the baseline recorded in the register)
- Planning-reference gate over the diff against e99ca74b: prints nothing; exactly the six planned files changed.

**Residual note:** an over-bound re-render still echoes the posted value back into the input's `value` attribute. This is the pre-existing 1:1 reflection of the DM's own payload, DM-tier only, bounded by the form limits, and costs no parsing.

**Proposed register update (for the orchestrator):** set T-81-12 status to `closed` with the evidence above (over-bound value never parsed, proven over HTTP on Create, Edit and at 1001 characters; `ParseTagNames` linear via `HashSet`), add an audit-trail row dated 2026-10-19, and remove the two now-resolved residual notes (the T-81-12 fix direction and the weak test).

## Known Stubs

None.

## Threat Flags

None - no new endpoints, auth paths or trust-boundary changes.

## Self-Check: PASSED

All six modified files exist and the four commits (`bc4e4460`, `c9cf438d`, `609ebe5e`, `1435c819`) are on the branch.
