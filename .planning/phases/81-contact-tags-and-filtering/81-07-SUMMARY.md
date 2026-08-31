---
phase: 81-contact-tags-and-filtering
plan: 07
subsystem: ui
tags: [aspnetcore-mvc, razor, contact-tags, filtering, markup-tests]

# Dependency graph
requires:
  - phase: 81-06
    provides: "ContactViewModel.TagsInput/AvailableTagNames wired end to end through Create/Edit, and the pinned Tagify entry widget on all four contact forms"
provides:
  - "contacts.css: .contact-tag-list/.contact-tag-chip and .contact-filter-* classes (row, label, tag-group, check-label, apply-btn, clear-btn, empty), all scoped under .contacts-page"
  - "Details.cshtml: a muted, escaped tag line (icon + chips) between SubLocation and the hidden badge, for DM-tier viewers with at least one tag"
  - "Index.cshtml: the desktop tag filter row (populated or disabled-hint state), tag chips on every card, hidden inputs carrying the selection through the Show Hidden toggle, and a three-branch empty state (populated / filtered-to-nothing / genuinely empty)"
  - "ContactsTagsDesktopMarkupTests -- 10 integration tests covering the audience gate on both index and details, viewer-scoped filter vocabulary before/after Show Hidden, both empty-state branches, the toggle round trip, and tag-name escaping"
  - "Fixed ApplyTagFilter so a selection matching zero visible contacts resolves to an empty result instead of silently falling back to the full list"
affects: []

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Index.cshtml reads Model.ViewerIsDmTier into a single local variable once, and reuses that local for both the header controls' existing conditional and the new filter row's conditional, so the two share one audience-gate evaluation instead of two independently derived checks; the per-card chip gate inside the RenderContactCard local function still reads Model.ViewerIsDmTier directly since it is the second and only other audience check in the file"
    - "The three-branch empty state (HasCategories-with-contacts / flat-with-contacts / filtered-to-nothing / genuinely-empty) is expressed as a flat @if / else-if chain rather than nested conditionals, mirroring the file's existing style"

key-files:
  created:
    - QuestBoard.IntegrationTests/Controllers/ContactsTagsDesktopMarkupTests.cs
  modified:
    - QuestBoard.Service/wwwroot/css/contacts.css
    - QuestBoard.Service/Views/Contacts/Details.cshtml
    - QuestBoard.Service/Views/Contacts/Index.cshtml
    - QuestBoard.Service/Controllers/Contacts/ContactsController.cs
    - QuestBoard.IntegrationTests/Controllers/ContactsControllerIntegrationTests.cs

key-decisions:
  - "Read Model.ViewerIsDmTier into one local variable and reused it for the header controls' conditional and the new filter row's conditional, rather than writing a second, independently-derived @if -- satisfies the plan's 'exactly one DM-tier conditional guards the header controls and the filter row' acceptance criterion without changing the existing header/title layout"
  - "Fixed ApplyTagFilter's zero-match fallback (see Deviations) rather than leaving the no-match empty-state branch unreachable"

requirements-completed: [CONTACTTAG-01, CONTACTTAG-07, CONTACTTAG-08, CONTACTTAG-10, CONTACTTAG-11, CONTACTTAG-14, CONTACTTAG-15, CONTACTTAG-16, CONTACTTAG-17]

coverage:
  - id: D1
    description: "contacts.css defines the chip, filter row, filter control, and disabled-filter styles under the .contacts-page scope; Details.cshtml renders a muted, escaped tag line between SubLocation and the hidden badge for DM-tier viewers with tags"
    requirement: CONTACTTAG-15
    verification:
      - kind: unit
        ref: "grep-verified: contacts.css contains all 9 required selectors under .contacts-page; Details.cshtml contains contact-tag-list/contact-tag-chip/fas fa-tags; Html.Markdown count unchanged at 2"
        status: pass
      - kind: unit
        ref: "dotnet build (0 errors)"
        status: pass
    human_judgment: false
  - id: D2
    description: "Index.cshtml renders the filter row (populated or disabled-hint), the per-card tag chips, the Show Hidden hidden inputs carrying the selection, and the three-branch empty state, all inside the existing ViewerIsDmTier conditional"
    requirement: CONTACTTAG-14
    verification:
      - kind: unit
        ref: "grep-verified: all 8 required class names present, name=\"tag\" appears twice, exact copy strings present, Model.ViewerIsDmTier appears exactly twice, zero Html.Raw/Html.Markdown occurrences"
        status: pass
      - kind: unit
        ref: "dotnet build (0 errors)"
        status: pass
    human_judgment: false
  - id: D3
    description: "The desktop audience gate, the viewer-scoped filter vocabulary (including the Show Hidden round trip), both empty-state branches, the toggle carrying selected tag ids, and tag-name escaping are proved by automated markup tests"
    requirement: CONTACTTAG-16
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/ContactsTagsDesktopMarkupTests.cs (10 tests, all pass)"
        status: pass
      - kind: integration
        ref: "dotnet test (full suite): 454 unit + 702 integration tests pass"
        status: pass
    human_judgment: false
  - id: D4
    description: "A filter selection matching zero visible contacts narrows to an empty result (no-match branch) rather than silently widening back to the full unfiltered list"
    requirement: CONTACTTAG-17
    verification:
      - kind: integration
        ref: "ContactsTagsDesktopMarkupTests#Index_FilterMatchesNothing_RendersNoMatchBranchNotEmptyListBranch"
        status: pass
      - kind: integration
        ref: "ContactsControllerIntegrationTests#Index_UnknownTagId_ReturnsNoMatchBranchWithoutError, #Index_TagIdFromAnotherBoard_MatchesNothingAndNeverLeaksTheOtherBoard"
        status: pass
    human_judgment: false
  - id: D5
    description: "Visual polish of the chip/filter styling (color, spacing, hover states) against the UI-SPEC's exact values, and real-browser rendering of the filter row and chips"
    verification: []
    human_judgment: true
    rationale: "No automated test in this codebase asserts computed CSS values or rendered visual appearance; this is deferred to manual/browser verification per this project's established convention for CSS-only acceptance criteria."

# Metrics
duration: ~40min
completed: 2026-08-31
status: complete
---

# Phase 81 Plan 07: Desktop Tag Filter, Chips, and Empty States Summary

**The desktop Contacts index and details views now render a working, DM-only tag filter (checkbox row, Apply/Clear, a disabled discoverability state before any tag exists), tag chips on every card and the details page, and a three-branch empty state that tells "filtered to nothing" apart from "genuinely no contacts" -- with a pre-existing filter bug fixed along the way that had made the no-match branch unreachable in production.**

## Performance

- **Duration:** ~40 min
- **Completed:** 2026-08-31
- **Tasks:** 3 completed
- **Files modified:** 5 (1 created, 4 modified)

## Accomplishments
- `contacts.css` gained nine new selectors (`.contact-tag-list`, `.contact-tag-chip`, and seven `.contact-filter-*` classes), all scoped under `.contacts-page`, matching the UI-SPEC's exact spacing/color/typography values and reusing only two font weights
- `Details.cshtml` renders a muted `fas fa-tags` + chip line between the sub-location paragraph and the hidden badge, gated on the same manage-permission conditional the page already uses, guarded additionally on the contact having at least one tag
- `Index.cshtml` renders the desktop filter row (checkbox-per-tag, Apply Filters, conditional Clear Filters) or, before any tag exists, the same container in a disabled, discoverable hint state -- both inside the single `ViewerIsDmTier` conditional that also gates the header controls
- Tag chips render on every contact card after town/sub-location, gated on `Model.ViewerIsDmTier` and the contact having tags
- The Show Hidden toggle form now carries the currently selected tag ids as hidden inputs, so flipping it never silently drops the active filter
- The empty state is now three branches: the populated grid/category view (unchanged), a distinct "No contacts match your filters" message with a Clear filters action, and the original "No Contacts Yet" message verbatim for a genuinely empty board
- `ContactsTagsDesktopMarkupTests` (10 tests) proves the DM/player audience gate on both the index and details pages, viewer-scoped filter vocabulary before and after Show Hidden, the disabled-filter hint on a tagless board, both empty-state branches, the toggle carrying selected tag ids, and escaping of a tag name containing markup characters

## Task Commits

Each task was committed atomically; task 3 additionally required a fix to pre-existing controller logic discovered while writing its tests:

1. **Task 1: Chip and filter styles, and the details tag line** - `205b073f` (feat)
2. **Task 2: Desktop index filter row, chips, toggle round trip, and empty states** - `3a8f3703` (feat)
3. **Task 3: Desktop markup tests for audience, vocabulary scoping, empty states, and escaping**
   - `2bee884c` (fix) -- corrected `ApplyTagFilter`'s zero-match fallback, discovered while writing the no-match test (see Deviations)
   - `3e2624f3` (test) -- `ContactsTagsDesktopMarkupTests.cs`, all 10 tests passing

**Plan metadata:** worktree mode -- SUMMARY.md committed by this agent; STATE.md/ROADMAP.md updates and the final metadata commit are the orchestrator's responsibility after merge.

## Files Created/Modified
- `QuestBoard.Service/wwwroot/css/contacts.css` - tag chip and filter row/control styles under `.contacts-page`
- `QuestBoard.Service/Views/Contacts/Details.cshtml` - muted, escaped tag line for DM-tier viewers
- `QuestBoard.Service/Views/Contacts/Index.cshtml` - filter row, per-card chips, Show Hidden hidden inputs, three-branch empty state
- `QuestBoard.Service/Controllers/Contacts/ContactsController.cs` - `ApplyTagFilter` no longer falls back to the unfiltered list on zero matches
- `QuestBoard.IntegrationTests/Controllers/ContactsControllerIntegrationTests.cs` - two pre-existing tests updated to assert the corrected (empty-on-no-match) behavior instead of the old fallback
- `QuestBoard.IntegrationTests/Controllers/ContactsTagsDesktopMarkupTests.cs` - new: 10 markup/audience-gate/escaping tests

## Decisions Made
- Read `Model.ViewerIsDmTier` into a single local variable (`viewerIsDmTier`) at the top of `Index.cshtml` and reused it for both the header controls' existing conditional and the new filter row's conditional, so the plan's "exactly one DM-tier conditional guards the header controls and the filter row" acceptance criterion (`grep -c 'Model.ViewerIsDmTier'` == 2, the other occurrence being the per-card chip gate) is satisfied without restructuring the existing header/title layout
- Fixed `ApplyTagFilter`'s fallback-to-full-list-on-zero-matches (see Deviations) rather than leaving the plan's own required no-match empty-state branch permanently unreachable in production

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] `ApplyTagFilter` silently widened the result back to the full visible list whenever the selection matched nothing**
- **Found during:** Task 3, while writing `Index_FilterMatchesNothing_RendersNoMatchBranchNotEmptyListBranch`
- **Issue:** A prior plan's `ApplyTagFilter` (in `ContactsController.cs`, not touched by plan 07's own file list) contained `return matched.Count == 0 ? visibleContacts : matched;` -- meaning any selection that matched zero currently-visible contacts (an unknown id, a foreign-board id, or a real tag id belonging only to a contact this viewer cannot currently see) fell back to showing every visible contact, unfiltered. This directly contradicted the current plan's own must-have truth ("A filter matching nothing shows a distinct message with a clear action") and its D-25 requirement ("the filter narrows and never widens") -- with this fallback in place, the "no contacts match your filters" branch built in Task 2 was permanently unreachable in production whenever the board had any visible contact at all
- **Fix:** Removed the fallback so the filter always returns the (possibly empty) matched set; an unknown/foreign/currently-invisible tag id now silently resolves to zero results rather than silently showing everything, which is what "silently match nothing" (this phase's own D-06) actually means
- **Files modified:** `QuestBoard.Service/Controllers/Contacts/ContactsController.cs`, `QuestBoard.IntegrationTests/Controllers/ContactsControllerIntegrationTests.cs`
- **Verification:** Updated the two pre-existing tests that had asserted the old fallback (`Index_UnknownTagId_ReturnsFullVisibleListWithoutError` → renamed `Index_UnknownTagId_ReturnsNoMatchBranchWithoutError`, and `Index_TagIdFromAnotherBoard_ReturnsOwnBoardListOnly` → renamed `Index_TagIdFromAnotherBoard_MatchesNothingAndNeverLeaksTheOtherBoard`) to assert the corrected behavior; full suite green at 454 unit + 702 integration tests
- **Committed in:** `2bee884c`

---

**Total deviations:** 1 auto-fixed (Rule 1 -- a pre-existing bug in code outside this plan's own file list, which directly blocked this plan's own required no-match empty-state branch from ever being reachable)
**Impact on plan:** Necessary for correctness -- without this fix, Task 2's three-branch empty state would have shipped with one branch that could never render in production, and Task 3's own required test could never pass honestly. No scope creep beyond the two dependent tests that needed updating to match.

## Issues Encountered

None beyond the `ApplyTagFilter` fix documented above, found and resolved within task 3's own test-writing pass.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- The desktop half of the tag surface (filter row, chips, both empty-state branches, the toggle round trip, and escaping) is complete and proven by automated tests
- `dotnet test` (full suite) is green at 454 unit + 702 integration tests
- Manual/browser verification of the chip and filter row's exact visual appearance (color, spacing, hover states) against the UI-SPEC remains a human-judgment item, consistent with this project's established convention for CSS-only acceptance criteria
- Remaining phase 81 work (mobile index filter/offcanvas, if scoped to a later plan) can proceed independently of this plan's desktop-only surface

---
*Phase: 81-contact-tags-and-filtering*
*Completed: 2026-08-31*
