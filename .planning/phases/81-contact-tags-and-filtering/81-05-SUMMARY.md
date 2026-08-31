---
phase: 81-contact-tags-and-filtering
plan: 05
subsystem: contacts
tags: [aspnetcore-mvc, automapper, entity-framework, contact-tags, tenant-isolation]

# Dependency graph
requires:
  - phase: 81-03
    provides: IContactService.ParseTagNames, IContactService.ReplaceContactTagsAsync, the board-filtered tag reconciliation and orphan-pruning in ContactRepository
  - phase: 81-04
    provides: ContactViewModel.Tags/TagsInput/AvailableTagNames, BuildTagVocabulary, IsVisibleTo, the ContactsController.Index tag-filter read path
provides:
  - "GetVisibleTagVocabularyAsync -- one shared helper Index, Create GET, and Edit GET all call to derive the tag suggestion list from the viewer's visible-but-unfiltered contacts"
  - "Create GET/POST and Edit GET/POST now read and write ContactViewModel.TagsInput end to end: Create/Edit GET populate AvailableTagNames (Edit GET also pre-fills TagsInput), Create/Edit POST parse it through ParseTagNames and persist through ReplaceContactTagsAsync"
  - "A shared 30-character tag-name-length guard (ValidateTagNameLengths) and a shared suggestion-list repopulation helper (PopulateTagSuggestionsAsync) used across every invalid-model re-render path on both posts"
  - "6 new integration tests proving the no-script comma path, case-insensitive reuse/de-duplication, pruning through the form, the over-long-name rejection, and the cross-board write refusal -- all asserted against QuestBoardContext"
affects: [81-06]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "GetVisibleTagVocabularyAsync is the single derivation point for the tag vocabulary across Index/Create GET/Edit GET -- no second vocabulary query exists anywhere in the controller"
    - "Tag names are parsed and length-validated before any write on both posts, with the length error routed through the existing ModelState.IsValid re-render rather than a second check-and-return"
    - "Every invalid-model re-render on Create/Edit (initial ModelState failure, image-validation failure, category failure, tag-length failure) repopulates AvailableTagNames through one shared private method"
    - "The base repository's id-propagation on AddAsync means Create POST can call ReplaceContactTagsAsync against the newly created contact's id in the same request, with no re-fetch"

key-files:
  created: []
  modified:
    - QuestBoard.Service/Controllers/Contacts/ContactsController.cs
    - QuestBoard.IntegrationTests/Controllers/ContactsControllerIntegrationTests.cs

key-decisions:
  - "Routed the category-acceptable-check re-render path through PopulateTagSuggestionsAsync too, even though task 2's action text names only three failure paths (model-state, image-validation, tag-length) -- the category-invalid path is also an invalid-model re-render, and leaving it out would have been the exact drift the shared-method instruction exists to prevent"
  - "Placed the tag-name parse and length validation immediately after the ModelState-independent early guards (Challenge/RedirectToAction/NotFound) but before the first ModelState.IsValid check on both posts, so an over-long name surfaces through the plan's existing invalid-model re-render path rather than a second, parallel one"

requirements-completed: [CONTACTTAG-01, CONTACTTAG-03, CONTACTTAG-04, CONTACTTAG-05, CONTACTTAG-13]

coverage:
  - id: D1
    description: "Create GET and Edit GET populate a viewer-scoped tag suggestion list (AvailableTagNames) derived from the same visible-contact set the index filter uses; Edit GET also pre-fills TagsInput with the contact's current tags"
    requirement: CONTACTTAG-01
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/ContactsControllerIntegrationTests.cs#Create_Get_DungeonMasterAccess_ShouldSucceed"
        status: pass
      - kind: unit
        ref: "dotnet build (0 errors) -- Create GET signature and GetVisibleTagVocabularyAsync verified present via grep per plan acceptance criteria"
        status: pass
    human_judgment: false
  - id: D2
    description: "Posting a plain comma-separated TagsInput on Create attaches exactly those tags to the new contact, with no client script involved"
    requirement: CONTACTTAG-03
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/ContactsControllerIntegrationTests.cs#Create_CommaSeparatedTags_AttachesBothTags"
        status: pass
    human_judgment: false
  - id: D3
    description: "A case variant of an existing tag reuses the existing row rather than creating a second one; repeated casings of one name collapse to a single tag and association"
    requirement: CONTACTTAG-04
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/ContactsControllerIntegrationTests.cs#Edit_CaseVariantOfExistingTag_ReusesExistingRow"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/ContactsControllerIntegrationTests.cs#Edit_RepeatedCasingsOfOneName_CreatesSingleTag"
        status: pass
    human_judgment: false
  - id: D4
    description: "Clearing TagsInput on edit removes every tag from the contact, and any tag left with no contacts is pruned from the database"
    requirement: CONTACTTAG-04
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/ContactsControllerIntegrationTests.cs#Edit_EmptyTagsInput_RemovesTagsAndPrunesOrphan"
        status: pass
    human_judgment: false
  - id: D5
    description: "A tag name over 30 characters is rejected with a visible ModelState error naming the field, creates no tag row, and leaves the contact's existing tags untouched -- no truncation"
    requirement: CONTACTTAG-05
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/ContactsControllerIntegrationTests.cs#Edit_TagNameOverLengthLimit_RerendersWithValidationError"
        status: pass
    human_judgment: false
  - id: D6
    description: "A tag name that exists only on another board creates a fresh row on the caller's own board and never attaches the other board's row -- the controller never resolves a tag itself, only through the board-filtered reconciliation"
    requirement: CONTACTTAG-13
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/ContactsControllerIntegrationTests.cs#Edit_TagNameExistingOnlyOnAnotherBoard_CreatesOwnBoardRow"
        status: pass
    human_judgment: false

# Metrics
duration: 20min
completed: 2026-08-31
status: complete
---

# Phase 81 Plan 05: Contact Tag Write Path (Create/Edit Form Persistence) Summary

**Create and Edit now read and write `TagsInput` end to end -- a viewer-scoped suggestion list on both GET forms, board-filtered comma-separated tag persistence on both POSTs via `IContactService.ParseTagNames`/`ReplaceContactTagsAsync`, a 30-character length guard that rejects rather than truncates, and 6 new integration tests proving the no-script write path and the cross-board write refusal against the database.**

## Performance

- **Duration:** ~20 min
- **Completed:** 2026-08-31
- **Tasks:** 3 completed
- **Files modified:** 2

## Accomplishments
- New shared helper `GetVisibleTagVocabularyAsync(currentUserId, viewerIsDmTier, token)` derives the tag suggestion vocabulary from the same visible-but-unfiltered contact set the index filter uses; `Index`, `Create` GET, and `Edit` GET all call it, so there is no second vocabulary query anywhere in the controller
- `Create` GET is now async, resolves the viewer, and populates `AvailableTagNames`; `Edit` GET additionally pre-fills `TagsInput` from the contact's own (already-alphabetical) tag names
- `Create` POST and `Edit` POST parse `TagsInput` via `ParseTagNames` before any write, validate every parsed name against a shared 30-character length guard (`ValidateTagNameLengths`), and on success persist through `ReplaceContactTagsAsync` against the contact's id -- Create relies on the base repository's id-propagation on `AddAsync`, needing no re-fetch
- An over-long name adds a targeted `ModelState` error naming the tag and the limit, falling through to the existing invalid-model re-render rather than a parallel check-and-return; no truncation, since there is no rename path to repair a silently shortened tag afterward
- Every invalid-model re-render on both posts (initial `ModelState` failure, image-validation failure, category-invalid failure, and the new tag-length failure) now repopulates `AvailableTagNames` through one shared `PopulateTagSuggestionsAsync` method, so a future added guard cannot ship a form with an empty suggestion list
- 6 new integration tests cover the no-script comma path, case-insensitive reuse, de-duplication of repeated casings, pruning through the form, the over-long-name rejection, and the cross-board write refusal -- every assertion reads `QuestBoardContext` directly (the surviving `ContactTags` rows and the contact's own `Tags` collection), never the rendered page

## Task Commits

Each task was committed atomically:

1. **Task 1: Viewer-scoped suggestion list on the create and edit forms** - `1101977b` (feat)
2. **Task 2: Persist the submitted tag value on create and edit** - `86518242` (feat)
3. **Task 3: Integration tests for the no-script write path and the cross-board refusal** - `b6b6f567` (test)

**Plan metadata:** worktree mode -- SUMMARY.md committed by this agent; STATE.md/ROADMAP.md updates and the final metadata commit are the orchestrator's responsibility after merge.

## Files Created/Modified
- `QuestBoard.Service/Controllers/Contacts/ContactsController.cs` - `GetVisibleTagVocabularyAsync`, `ValidateTagNameLengths`, `PopulateTagSuggestionsAsync`; `Create`/`Edit` GET populate the suggestion list (and pre-fill `TagsInput` on Edit); `Create`/`Edit` POST parse, validate, and persist the submitted tag value
- `QuestBoard.IntegrationTests/Controllers/ContactsControllerIntegrationTests.cs` - 6 new tests: `Create_CommaSeparatedTags_AttachesBothTags`, `Edit_CaseVariantOfExistingTag_ReusesExistingRow`, `Edit_RepeatedCasingsOfOneName_CreatesSingleTag`, `Edit_EmptyTagsInput_RemovesTagsAndPrunesOrphan`, `Edit_TagNameOverLengthLimit_RerendersWithValidationError`, `Edit_TagNameExistingOnlyOnAnotherBoard_CreatesOwnBoardRow`

## Decisions Made
- Routed the category-acceptable-check re-render path through `PopulateTagSuggestionsAsync` too, beyond the three failure paths task 2's action text names explicitly -- it is also an invalid-model re-render, and omitting it would have reproduced the exact suggestion-list drift the shared-method instruction exists to prevent
- Parsed and length-validated `TagsInput` immediately after the early Challenge/redirect/not-found guards but before the first `ModelState.IsValid` check on both posts, so an over-long name surfaces through the plan's existing invalid-model re-render rather than a second, parallel check-and-return

## Deviations from Plan

None - plan executed exactly as written. The category-re-render routing above is an application of the plan's own stated intent ("a future added guard cannot forget it"), not a deviation from it.

## Issues Encountered

None. Build and every relevant test filter were run directly from the worktree root (no `cd`), per the guard the previous plan (81-04) left behind after that exact mistake silently built the main repo checkout instead of this worktree.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- The controller-side write path for tags is complete and proven server-side: Create/Edit accept and persist a plain comma-separated value with no client script, reuse and de-duplicate case-insensitively, prune orphans, reject over-long names, and never attach across boards
- Plan 06 (the CDN chips widget and its styling) can now wire `TagsInput`/`AvailableTagNames` into the actual form markup; the manual verification for the widget itself and its blocked-CDN degradation is deferred to that plan per `81-VALIDATION.md`
- `dotnet test` (full suite) is green at 454 unit + 687 integration tests

---
*Phase: 81-contact-tags-and-filtering*
*Completed: 2026-08-31*
