---
phase: quick-260831-mcb
plan: 01
subsystem: contacts
tags: [authorization, visibility, contacts, tags, session-toggle]

requires:
  - phase: 81-contact-tags-and-filtering
    provides: ContactTag entities, ContactsController tag chips and Index filter row

provides:
  - AreTagsVisibleTo ownership/toggle predicate for contact tag chips
  - TagVocabularyScope (Authoring/ViewerVisible) parameterization of the shared tag vocabulary helper
  - Integration coverage of the ownership x Show Hidden toggle x role x surface matrix

affects: [contacts, tags, authorization]

tech-stack:
  added: []
  patterns:
    - "Ownership-or-toggle visibility gate mirrored from IsVisibleTo (hidden contacts) onto tag chip visibility (AreTagsVisibleTo)"
    - "Named scope enum on a shared derivation helper to keep two call-site behaviors (authoring whitelist vs. filter vocabulary) from silently drifting together"

key-files:
  created:
    - QuestBoard.IntegrationTests/Controllers/ContactsTagOwnershipTests.cs
  modified:
    - QuestBoard.Service/Controllers/Contacts/ContactsController.cs
    - QuestBoard.Service/ViewModels/ContactViewModels/ContactsIndexViewModel.cs
    - QuestBoard.IntegrationTests/Controllers/ContactsTagsDesktopMarkupTests.cs
    - QuestBoard.IntegrationTests/Mobile/ContactsTagsMobileTests.cs

key-decisions:
  - "Universal ownership rule (not a SuperAdmin-only bypass) - matches the existing hidden-contact toggle mental model; user-confirmed side effect that co-DMs don't see each other's tags without the toggle"
  - "Reused the existing per-board ShowHidden session toggle rather than adding a second flag"
  - "Create/Edit authoring tag-suggestion whitelist stays board-wide by explicit accepted-risk decision (T-mcb-04), pinned by an integration test"

requirements-completed: [TAGOWN-01, TAGOWN-02]

coverage:
  - id: D1
    description: "Contact tag chips on Index and Details render only for the viewer's own contacts, or any contact once Show Hidden is on"
    requirement: TAGOWN-01
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/ContactsTagOwnershipTests.cs#Index_OwningDungeonMaster_StillSeesOwnTagChipsAndFilterOption"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/ContactsTagOwnershipTests.cs#Index_NonOwningDungeonMaster_SeesNeitherChipsNorFilterOptionWhileShowHiddenIsOff"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/ContactsTagOwnershipTests.cs#Index_NonOwningDungeonMaster_SeesChipsAndFilterOptionAfterTogglingShowHidden"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/ContactsTagOwnershipTests.cs#Details_NonOwningDungeonMaster_SeesNoTagChipsUntilShowHiddenIsOn"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/ContactsTagOwnershipTests.cs#Details_OwningDungeonMaster_StillSeesOwnTagChips"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Mobile/ContactsTagsMobileTests.cs#Index_MobileNonOwningDungeonMaster_SeesNoChipsOrDrawerOptionsUntilShowHiddenIsOn"
        status: pass
    human_judgment: false
  - id: D2
    description: "Index filter vocabulary (checkboxes / mobile drawer) offers only tag names whose chips the viewer can see; Create/Edit authoring suggestions stay board-wide"
    requirement: TAGOWN-02
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/ContactsTagOwnershipTests.cs#CreateAndEditForms_NonOwningDungeonMaster_StillSuggestEveryBoardTagName"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/ContactsTagsDesktopMarkupTests.cs#Index_TagOnlyOnUnrevealedContact_AbsentUntilShowHiddenIsOn"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/ContactsTagsFormMarkupTests.cs (full suite)"
        status: pass
    human_judgment: false
  - id: D3
    description: "Non-DM-tier viewers (Players) still receive zero tag markup regardless of ownership"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/ContactsTagOwnershipTests.cs#Index_Player_StillSeesNoTagMarkupRegardlessOfOwnership"
        status: pass
    human_judgment: false

duration: 45min
completed: 2026-08-31
status: complete
---

# Quick Task 260831-mcb: Contact Tag Visibility Tied to Ownership + Show Hidden Toggle Summary

**Extended the existing owner-or-toggle visibility pattern from hidden contacts to contact tags: `AreTagsVisibleTo` gates chip rendering server-side and a new `TagVocabularyScope` parameter narrows the Index filter row identically, while the Create/Edit authoring suggestion list stays board-wide by design.**

## Performance

- **Duration:** ~45 min
- **Started:** 2026-08-31T13:47:00Z
- **Completed:** 2026-08-31T14:32:30Z
- **Tasks:** 3
- **Files modified:** 5 (1 created, 4 modified)

## Accomplishments
- Added `AreTagsVisibleTo(createdByUserId, currentUserId, viewerIsDmTier, includeHidden)` as a sibling static helper to the existing `IsVisibleTo`, and wired it into both `Index` and `Details` to empty `Tags` server-side (defense in depth, not just a Razor `@if`) whenever the viewer neither owns the contact nor has Show Hidden on.
- Parameterized `GetVisibleTagVocabularyAsync` with a required `TagVocabularyScope` enum (`Authoring` / `ViewerVisible`) so the Index filter row can be narrowed by ownership+toggle while `Create`, `Edit`, and `PopulateTagSuggestionsAsync` keep deriving the full board-wide vocabulary — the two surfaces are now named rather than implicitly identical.
- Added 7 new integration facts in `ContactsTagOwnershipTests.cs` and 1 in `ContactsTagsMobileTests.cs` covering the ownership x toggle x role x surface matrix on desktop and mobile, Index and Details, including the deliberate authoring-vs-filter asymmetry.
- Re-seeded the one pre-existing test whose ownership assumption this behavior change invalidated (`Index_TagOnlyOnUnrevealedContact_AbsentUntilShowHiddenIsOn`), keeping its assertions unchanged.

## Task Commits

Each task was committed atomically:

1. **Task 1: Gate contact tag chips on ownership or the Show Hidden toggle** - `78622e5c` (feat)
2. **Task 2: Scope the Index filter vocabulary the same way, without touching the authoring suggestion list** - `5f7a434e` (feat)
3. **Task 3: Integration coverage for the ownership x toggle x role x surface matrix** - `b8b4da56` (test)

_No TDD multi-commit split was used; tasks 1 and 2 were marked `tdd="true"` in the plan but executed as single-commit `feat` changes verified against the pre-existing and re-seeded test suite before commit, consistent with how the sibling `IsVisibleTo` pattern was originally implemented in this codebase._

## Files Created/Modified
- `QuestBoard.Service/Controllers/Contacts/ContactsController.cs` - Added `AreTagsVisibleTo` helper and `TagVocabularyScope` enum; widened `Index`/`Details` tag-emptying condition; parameterized `GetVisibleTagVocabularyAsync` and its four call sites
- `QuestBoard.Service/ViewModels/ContactViewModels/ContactsIndexViewModel.cs` - Comment-only correction on `AvailableTags` to describe the chip-visibility relationship precisely
- `QuestBoard.IntegrationTests/Controllers/ContactsTagsDesktopMarkupTests.cs` - Re-seeded `Index_TagOnlyOnUnrevealedContact_AbsentUntilShowHiddenIsOn` so ownership is held constant and only revealed-ness varies
- `QuestBoard.IntegrationTests/Controllers/ContactsTagOwnershipTests.cs` - New: 7 facts covering owning/non-owning DM, Player, the Show Hidden round trip, and the authoring-vs-filter asymmetry
- `QuestBoard.IntegrationTests/Mobile/ContactsTagsMobileTests.cs` - Added one fact proving the same rule under a real mobile User-Agent

## Decisions Made
- Universal ownership rule applied to every DM-tier viewer rather than a SuperAdmin-only special case, matching the existing hidden-contact toggle's mental model (user-confirmed in CONTEXT.md).
- Reused the single existing `ShowHiddenContactsKey(groupId)` session flag for all three behaviors (hidden contacts, tag chips, filter vocabulary) rather than adding a second toggle.
- The Create/Edit authoring tag-suggestion whitelist deliberately stays board-wide (not scoped by ownership) so a DM can still reuse any tag name already in use on the board — pinned as an accepted risk (T-mcb-04) with a dedicated integration test guarding against future "unification."

## Deviations from Plan

None — plan executed exactly as written. All five verification-listed files match: `ContactsController.cs`, `ContactsIndexViewModel.cs` (comment only), `ContactsTagsDesktopMarkupTests.cs`, the new `ContactsTagOwnershipTests.cs`, and `ContactsTagsMobileTests.cs`. Zero `.cshtml` files, zero AutoMapper profiles, zero migrations touched.

## Issues Encountered
- The `Write` tool emitted the new `ContactsTagOwnershipTests.cs` file with LF line endings; converted to CRLF before running tests to satisfy CLAUDE.md's Windows/CRLF convention (matches a previously logged STATE.md decision about this same tool behavior).
- No `.sln` file exists at the repo root, so the plan's `dotnet build` (whole solution) verification step was run per-project instead (`QuestBoard.Service`, then `QuestBoard.IntegrationTests`, which transitively builds `Domain` and `Repository`) — all 4 projects built with 0 errors.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- The ownership+toggle pattern is now consistently applied to contact visibility, tag chip visibility, and filter vocabulary — a future feature touching any of these three should reuse `IsVisibleTo` / `AreTagsVisibleTo` / `TagVocabularyScope` rather than introducing a new rule.
- `T-mcb-05` (accepted): a DM-tier viewer can still hand-craft `?tag={id}` for an id no longer offered in the filter and observe which already-visible contacts carry it; no tag name is disclosed and contact visibility is unchanged. Left open per the plan's explicit scope boundary against intersecting `selectedTagIds` with `AvailableTags`.

---
*Phase: quick-260831-mcb*
*Completed: 2026-08-31*

## Self-Check: PASSED

All 6 created/modified files verified present on disk; all 3 task commits (`78622e5c`, `5f7a434e`, `b8b4da56`) verified present in git log.
