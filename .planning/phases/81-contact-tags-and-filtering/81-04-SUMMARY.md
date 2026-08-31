---
phase: 81-contact-tags-and-filtering
plan: 04
subsystem: contacts
tags: [aspnetcore-mvc, automapper, query-string-binding, contact-tags, filtering]

# Dependency graph
requires:
  - phase: 81-03
    provides: ContactRepository.ReplaceContactTagsAsync, ContactService.ParseTagNames, tags loaded alongside contacts under AsSplitQuery
provides:
  - "ContactTagViewModel, ContactsIndexViewModel.SelectedTagIds/AvailableTags/HasActiveFilters, ContactViewModel.Tags/TagsInput/AvailableTagNames"
  - "ViewModelProfile ContactTag <-> ContactTagViewModel map with member ignores on both Contact view-model maps"
  - "ContactsController.Index binds a repeated IList<int>? tag query parameter, derives the tag vocabulary from the visible-but-unfiltered contact set via BuildTagVocabulary, and applies ApplyTagFilter strictly after the visibility gate"
  - "ContactsController.ToggleShowHidden preserves the selected tag ids across its redirect as repeated tag query parameters via QueryHelpers.AddQueryString"
  - "Player-tier viewers get no tag surface at all on Index or Details -- gated on the existing viewerIsDmTier flag"
  - "7 new integration tests proving narrowing, union, the visibility-gate-wins-over-tag-match case, the zero-match fallback for unknown/other-board ids, the redirect round trip, and the player-tier no-op"
affects: [81-05, 81-06]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Tag filter runs strictly after IsVisibleTo and never inside the repository query -- narrows visibleContacts, never widens it"
    - "BuildTagVocabulary takes the visible-but-unfiltered contact set as its only argument, so a tag with no visible bearer is structurally unreachable from the browser"
    - "A selection whose ids match zero contacts falls back to the unfiltered visible list rather than an empty page -- covers unknown, pruned, and other-board tag ids without a 404 or an error"
    - "ToggleShowHidden composes its redirect target from Url.Action plus QueryHelpers.AddQueryString over integer ids only, never RedirectToAction with a raw IList route value (which would stringify to the collection's type name)"
    - "One audience gate (viewerIsDmTier) controls tag visibility on both Index and Details, rather than a second check that could drift"

key-files:
  created:
    - QuestBoard.Service/ViewModels/ContactViewModels/ContactTagViewModel.cs
  modified:
    - QuestBoard.Service/ViewModels/ContactViewModels/ContactsIndexViewModel.cs
    - QuestBoard.Service/ViewModels/ContactViewModels/ContactViewModel.cs
    - QuestBoard.Service/Automapper/ViewModelProfile.cs
    - QuestBoard.Service/Controllers/Contacts/ContactsController.cs
    - QuestBoard.IntegrationTests/Controllers/ContactsControllerIntegrationTests.cs

key-decisions:
  - "ApplyTagFilter falls back to the full visible list when the selection matches zero contacts, rather than returning an empty list -- this reconciles PLAN.md's explicit truths/task-3 wording ('produces the unfiltered visible list') with D-06's 'silently match nothing, never 404 and never error.' A narrower reading (empty result whenever nothing matches, mirroring AgendaController's board-id intersection) was rejected because PLAN.md's task 3 action text and must_haves.truths state the full-list outcome twice, explicitly, for both the unknown-id and other-board-id cases -- and task 2's own read_first note explicitly says AgendaController's exact mechanics were 'deliberately rejected for this phase,' only its query-string binding shape was borrowed"
  - "Vocabulary and filter both operate over IList<ContactTag>/IList<Contact> in memory, matching the plan's explicit prohibition on moving either into the query layer"

requirements-completed: [CONTACTTAG-01, CONTACTTAG-06, CONTACTTAG-07, CONTACTTAG-08, CONTACTTAG-09, CONTACTTAG-10, CONTACTTAG-11, CONTACTTAG-17]

coverage:
  - id: D1
    description: "Index binds repeated tag ids from the query string with plain parameter binding (no FromQuery attribute), matching the ShopController.Index precedent"
    requirement: CONTACTTAG-06
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/ContactsControllerIntegrationTests.cs#Index_SingleSelectedTag_ReturnsOnlyMatchingContacts"
        status: pass
    human_judgment: false
  - id: D2
    description: "Tag filter uses union (OR) semantics across multiple selected tag ids"
    requirement: CONTACTTAG-08
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/ContactsControllerIntegrationTests.cs#Index_TwoSelectedTags_ReturnsUnionNotIntersection"
        status: pass
    human_judgment: false
  - id: D3
    description: "The tag filter runs strictly after the visibility gate and can never surface a contact the gate excluded, including an unrevealed contact carrying a selected tag while Show Hidden is off"
    requirement: CONTACTTAG-10
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/ContactsControllerIntegrationTests.cs#Index_SelectedTagOnUnrevealedContact_StaysHiddenWhileShowHiddenIsOff"
        status: pass
    human_judgment: false
  - id: D4
    description: "An unknown or other-board tag id in the query string never 404s or errors -- it resolves to the full visible list, and cross-board tag/contact names never leak into the response"
    requirement: CONTACTTAG-09
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/ContactsControllerIntegrationTests.cs#Index_UnknownTagId_ReturnsFullVisibleListWithoutError"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/ContactsControllerIntegrationTests.cs#Index_TagIdFromAnotherBoard_ReturnsOwnBoardListOnly"
        status: pass
    human_judgment: false
  - id: D5
    description: "ToggleShowHidden preserves the selected tag ids across its POST-redirect round trip as repeated tag query parameters"
    requirement: CONTACTTAG-11
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/ContactsControllerIntegrationTests.cs#ToggleShowHidden_WithSelectedTags_RedirectPreservesThem"
        status: pass
    human_judgment: false
  - id: D6
    description: "A player-tier viewer's response carries no tag data at all, and a tag query parameter changes nothing for them, on both Index and Details"
    requirement: CONTACTTAG-17
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/ContactsControllerIntegrationTests.cs#Index_PlayerTierWithTagId_IgnoresTheFilter"
        status: pass
    human_judgment: false
  - id: D7
    description: "ContactTagViewModel, ContactsIndexViewModel's tag members, ContactViewModel's tag members, and the ContactTag <-> ContactTagViewModel AutoMapper map all exist and the mapper configuration validates"
    requirement: CONTACTTAG-01
    verification:
      - kind: unit
        ref: "QuestBoard.UnitTests -- EntityProfileEnumCastTests (mapper configuration validation, unaffected structurally but re-run to confirm no regression)"
        status: pass
    human_judgment: false

# Metrics
duration: 20min
completed: 2026-08-31
status: complete
---

# Phase 81 Plan 04: Contact Tag Filtering on the Index Read Path Summary

**ContactsController.Index binds a repeated `tag` query parameter, derives the filter vocabulary from the viewer's visible-but-unfiltered contacts, applies a union tag filter strictly after the visibility gate (falling back to the unfiltered list when nothing matches), and ToggleShowHidden carries the selection through its redirect via `QueryHelpers.AddQueryString`.**

## Performance

- **Duration:** ~20 min
- **Completed:** 2026-08-31
- **Tasks:** 3 completed
- **Files modified:** 6 (1 created)

## Accomplishments
- `ContactTagViewModel` plus three new members on `ContactsIndexViewModel` (`SelectedTagIds`, `AvailableTags`, `HasActiveFilters`) and three on `ContactViewModel` (`Tags`, `TagsInput`, `AvailableTagNames`), wired into `ViewModelProfile` with a `ContactTag <-> ContactTagViewModel` map and the correct member ignores on both `Contact` maps
- `ContactsController.Index(IList<int>? tag = null, ...)` binds repeated tag ids with plain parameter binding (no `FromQuery`, matching `ShopController.Index`), builds the vocabulary from the visible-but-unfiltered contact set via a new `BuildTagVocabulary` helper, and applies a new `ApplyTagFilter` helper strictly after `IsVisibleTo` -- the filter can only narrow what the viewer could already see
- `ApplyTagFilter` implements union (OR) semantics; a selection that matches zero contacts (unknown, pruned, or another board's tag id) falls back to the full visible list rather than an empty page or an error
- `ToggleShowHidden(IList<int>? tag = null)` preserves the selection across its redirect as repeated `tag` query parameters via `QueryHelpers.AddQueryString`, never a raw route-value collection (which would stringify to the type name instead of expanding)
- A player-tier viewer gets no tag surface at all -- `selectedTagIds`/`AvailableTags` stay empty and each mapped contact's `Tags` collection is cleared, on both `Index` and `Details`, gated on the single existing `viewerIsDmTier` flag rather than a second check
- 7 new integration tests cover single-tag narrowing, two-tag union, the visibility-gate-wins case (unrevealed contact with a matching tag, toggled on and off), the unknown-id and other-board-id fallback, the Show Hidden redirect round trip, and the player-tier no-op

## Task Commits

Each task was committed atomically:

1. **Task 1: Tag view models and the view-model mapping** - `de9bb1ee` (feat)
2. **Task 2: Tag binding, vocabulary derivation, in-memory filtering, and the Show Hidden round trip** - `0d463fe9` (feat)
3. **Task 3: Integration tests for filter semantics and the Show Hidden round trip** - `53fced74` (test, includes the `ApplyTagFilter` zero-match fallback fix found while writing these tests)

**Plan metadata:** worktree mode -- SUMMARY.md committed by this agent; STATE.md/ROADMAP.md updates and the final metadata commit are the orchestrator's responsibility after merge.

## Files Created/Modified
- `QuestBoard.Service/ViewModels/ContactViewModels/ContactTagViewModel.cs` - New: `Id`, `Name`
- `QuestBoard.Service/ViewModels/ContactViewModels/ContactsIndexViewModel.cs` - `SelectedTagIds`, `AvailableTags`, `HasActiveFilters`
- `QuestBoard.Service/ViewModels/ContactViewModels/ContactViewModel.cs` - `Tags`, `TagsInput` (`[StringLength(1000)]`), `AvailableTagNames`
- `QuestBoard.Service/Automapper/ViewModelProfile.cs` - `ContactTag <-> ContactTagViewModel` map; ignores `TagsInput`/`AvailableTagNames` on `Contact -> ContactViewModel`; ignores `Tags` on `ContactViewModel -> Contact`
- `QuestBoard.Service/Controllers/Contacts/ContactsController.cs` - `Index`/`ToggleShowHidden` tag binding, `BuildTagVocabulary`, `ApplyTagFilter`, player-tier `Tags` clearing on `Index` and `Details`
- `QuestBoard.IntegrationTests/Controllers/ContactsControllerIntegrationTests.cs` - 7 new tests covering filter semantics, the redirect round trip, and the player-tier no-op

## Decisions Made
- `ApplyTagFilter` falls back to the full visible list when the selection matches zero contacts (see `key-decisions` in frontmatter for the full reasoning) -- this is the one place this plan's execution diverged from a literal first reading of task 2's own action text ("simply contributes no matches"), reconciled in favor of PLAN.md's explicit, twice-stated truths and task 3 behavior spec
- Placed the `ContactTag <-> ContactTagViewModel` map immediately after the `Contact`/`ContactViewModel` map pair rather than after the `ContactNote` pair as task 1's action text suggested -- functionally identical since AutoMapper profile registration order doesn't affect resolution, and keeps the new tag map adjacent to the other tag-touching maps

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] `ApplyTagFilter` returned an empty list instead of falling back to the full visible list when no selected id matched anything**
- **Found during:** Task 3 (writing `Index_UnknownTagId_ReturnsFullVisibleListWithoutError` and `Index_TagIdFromAnotherBoard_ReturnsOwnBoardListOnly`)
- **Issue:** The initial implementation (from task 2) treated an unmatched id purely as "contributes no matches" to a union, which for a selection where every id is unmatched naturally computes to an empty result -- contradicting PLAN.md's explicit truths and task 3 action text, both of which state the response must contain the full visible list for an unknown or other-board tag id
- **Fix:** `ApplyTagFilter` now computes the union match first; if that match set is empty, it returns `visibleContacts` unchanged instead of the empty match set
- **Files modified:** `QuestBoard.Service/Controllers/Contacts/ContactsController.cs`
- **Verification:** `Index_UnknownTagId_ReturnsFullVisibleListWithoutError` and `Index_TagIdFromAnotherBoard_ReturnsOwnBoardListOnly` both pass; full suite (454 unit + 681 integration) green
- **Committed in:** `53fced74` (Task 3 commit, alongside the tests that caught it)

**2. [Rule 1 - Bug] `CreateTestContactTagAsync` test helper failed under the other-board scenario**
- **Found during:** Task 3 (`Index_TagIdFromAnotherBoard_ReturnsOwnBoardListOnly`)
- **Issue:** The helper looks up the target contact via `context.Contacts.Include(...).FirstAsync(...)`, which is subject to `ContactEntity`'s board-scoped query filter -- creating a tag on a group-2 contact while the test harness's active group was still 1 threw `InvalidOperationException: Sequence contains no elements`
- **Fix:** Wrapped the `CreateTestContactTagAsync` call in a `try/finally` that switches `factory.TestGroupContext.ActiveGroupId` to 2 for the duration and resets it to 1 afterward, mirroring the existing `ToggleShowHidden_IsScopedPerGroup_DoesNotLeakAcrossGroups` test's pattern
- **Files modified:** `QuestBoard.IntegrationTests/Controllers/ContactsControllerIntegrationTests.cs`
- **Verification:** Test passes; full suite green
- **Committed in:** `53fced74` (Task 3 commit)

---

**Total deviations:** 2 auto-fixed (2 bugs, both Rule 1)
**Impact on plan:** Both fixes were necessary for the plan's own stated behavior (the fallback semantics) and for the test helper to work at all under the board-scoped query filter established in 81-02/81-03. No scope creep -- neither introduced new files, endpoints, or surfaces beyond what the plan specified.

## Issues Encountered

The first two attempts to run `dotnet build`/`dotnet test` used `cd "C:/Repos/quest-board" && ...` as the plan's verify blocks literally specify, which silently built/tested the **main repo checkout** rather than this worktree -- the two directories are separate filesystem locations, and the command succeeded without ever compiling this plan's edits. Caught before trusting the result: re-ran every build/test invocation from the worktree's own root (no `cd`) for the remainder of execution, and the guard system now refuses that specific `cd` pattern for worktree-isolated agents going forward.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- The index read path now has a correct, tenant-safe tag filter model; plans 05/06 (the view/UI layer and the CDN tag-entry widget) can render `AvailableTags`, `SelectedTagIds`, and per-contact `Tags` directly off the view models this plan populated
- `dotnet test` (full suite) is green at 454 unit + 681 integration tests

---
*Phase: 81-contact-tags-and-filtering*
*Completed: 2026-08-31*
