---
phase: 77-availability-overview-page
plan: 10
subsystem: ui
tags: [accessibility, razor, css, mvc-views]

# Dependency graph
requires:
  - phase: 77-availability-overview-page
    provides: the availability overview desktop and mobile clickable-row idiom the review flagged as mouse-only (IN-06)
provides:
  - a shared .row-nav-link CSS class in modern-card.css usable by any future clickable row/card
  - thirteen keyboard-reachable anchors across eleven view files, one per existing mouse-only click handler
  - RowNavigationAccessibilityTests.cs proving a focusable link exists on a desktop and a mobile surface
affects: [any future phase touching Events, QuestLog, Quest, Calendar, Players, Characters, Contacts, or DungeonMaster profile views]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Additive keyboard-reachable anchor: wrap only the primary text node of a mouse-click-handled row/card in an `<a class=\"row-nav-link\" href=\"...\">`, written class-then-href, leaving the existing onclick handler untouched."

key-files:
  created:
    - QuestBoard.IntegrationTests/Controllers/RowNavigationAccessibilityTests.cs
  modified:
    - QuestBoard.Service/wwwroot/css/modern-card.css
    - QuestBoard.Service/Views/Events/Index.cshtml
    - QuestBoard.Service/Views/QuestLog/Index.cshtml
    - QuestBoard.Service/Views/Quest/Index.cshtml
    - QuestBoard.Service/Views/Events/Index.Mobile.cshtml
    - QuestBoard.Service/Views/Calendar/Index.Mobile.cshtml
    - QuestBoard.Service/Views/Players/Index.Mobile.cshtml
    - QuestBoard.Service/Views/Characters/Index.Mobile.cshtml
    - QuestBoard.Service/Views/Contacts/Index.Mobile.cshtml
    - QuestBoard.Service/Views/QuestLog/Index.Mobile.cshtml
    - QuestBoard.Service/Views/DungeonMaster/Profile.Mobile.cshtml
    - QuestBoard.Service/Views/Quest/Index.Mobile.cshtml

key-decisions:
  - "Used a plain href with @Url.Action(...) instead of the anchor tag helper, because the tag helper appends the generated href after author-written attributes, breaking the required class-then-href attribute order."
  - "Quest/Index.cshtml repeats the identical ownership conditional (currentUserId.Value == quest.DungeonMaster?.Id) in both the handler and the new anchor rather than factoring it out, so the two evaluations are provably the same expression, not merely equivalent."
  - "Quest/Index.Mobile.cshtml reuses the precomputed navUrl local for the anchor's href instead of recomputing the conditional, so the owner-versus-reader destination cannot drift from what the handler uses."

patterns-established:
  - "Row/card navigation idiom: mouse convenience handler stays on the container element; a real anchor wraps only the primary text node for keyboard/AT reachability. Any new clickable row or card should follow this shape rather than reintroducing an onclick-only pattern."

requirements-completed: [EVTVIEW-01]

coverage:
  - id: D1
    description: "Shared .row-nav-link CSS class added to modern-card.css (loaded by both desktop and mobile layouts), styling the new anchors identically to the surrounding text with a visible focus-visible ring."
    requirement: "EVTVIEW-01"
    verification:
      - kind: unit
        ref: "grep -c 'row-nav-link' QuestBoard.Service/wwwroot/css/modern-card.css == 4; grep -c 'focus-visible' == 1"
        status: pass
    human_judgment: false
  - id: D2
    description: "Thirteen keyboard-reachable anchors added across eleven view files (three desktop, ten mobile sites across eight files), each pointing at the same destination its row/card's existing click handler already navigates to, including the two ownership-conditional sites which keep their conditional target."
    requirement: "EVTVIEW-01"
    verification:
      - kind: unit
        ref: "grep -rl '<a class=\"row-nav-link\" href=' QuestBoard.Service/Views/ | wc -l == 11 files; grep -ro same pattern | wc -l == 13 anchors; grep -ro 'window.location.href' QuestBoard.Service/Views/ | wc -l == 15 (unchanged from pre-plan baseline)"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/EventsOverviewControllerIntegrationTests.cs (22 passed, 0 failed, unaffected)"
        status: pass
    human_judgment: false
  - id: D3
    description: "RowNavigationAccessibilityTests.cs proves a focusable anchor to the same destination exists on the desktop availability overview and on the mobile availability overview card."
    requirement: "EVTVIEW-01"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/RowNavigationAccessibilityTests.cs#Desktop_ClickableRow_ExposesFocusableLinkToSameDestination"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/RowNavigationAccessibilityTests.cs#Mobile_ClickableCard_ExposesFocusableLinkToSameDestination"
        status: pass
    human_judgment: false
  - id: D4
    description: "Full solution builds and the full test suite (968 tests: 408 unit + 560 integration) passes with no regressions."
    verification:
      - kind: unit
        ref: "dotnet build (0 errors); dotnet test (968 passed, 0 failed)"
        status: pass
    human_judgment: false
  - id: D5
    description: "Human spot-check: tab through the availability overview and the quest index on a keyboard and confirm each row's title takes focus with a visible ring and activates with Enter."
    verification: []
    human_judgment: true
    rationale: "Keyboard focus order, visible-ring rendering, and Enter-key activation are real-browser interaction facts that automated integration tests (which only inspect rendered HTML) cannot exercise; this is flagged in the plan's own <verification> section as recorded for /gsd-verify-work, not gating this plan."

# Metrics
duration: 20min
completed: 2026-08-29
status: complete
---

# Phase 77 Plan 10: Row Navigation Accessibility Summary

**Thirteen keyboard-reachable anchors added across eleven view files (desktop and mobile), each pointing at the same destination its existing mouse-only click handler already uses, backed by a shared `.row-nav-link` CSS class and two new integration tests.**

## Performance

- **Duration:** ~20 min
- **Completed:** 2026-08-29
- **Tasks:** 3
- **Files modified:** 11 views + 1 stylesheet
- **Files created:** 1 test file

## Accomplishments
- Added `.row-nav-link` (base, `:hover`/`:focus`, `:focus-visible`) to `modern-card.css`, the one stylesheet both the desktop and mobile layouts load
- Added a keyboard-reachable anchor to each of the three desktop clickable surfaces (Events overview row, Quest Log card, Quest poster card — the last keeping its ownership-conditional destination)
- Added anchors to ten mobile sites across eight views (Events overview card, Calendar agenda event and quest entries, Players DM row, Characters roster rows ×2, Contacts row, Quest Log item, DM profile quest history item, Quest card — the last reusing the precomputed `navUrl` local)
- Added `RowNavigationAccessibilityTests.cs` with two facts proving a focusable link to the same destination exists on a representative desktop surface and a representative mobile surface
- Verified the full solution builds and the full test suite (968 tests) is green, with the pre-existing `EventsOverview`-filtered suite (22 tests) and the mobile-user-agent facts from plan 77-09 unaffected

## Task Commits

Each task was committed atomically:

1. **Task 1: Add the shared link style and the three desktop surfaces** - `8172e179` (feat)
2. **Task 2: Add anchors to the first group of mobile surfaces** - `a146d8d3` (feat)
3. **Task 3: Add anchors to the remaining mobile surfaces and prove a focusable link exists** - `32583cfa` (feat)

_This plan runs in a worktree; the orchestrator applies the plan-metadata commit after merge._

## Files Created/Modified
- `QuestBoard.Service/wwwroot/css/modern-card.css` - added `.row-nav-link` base/hover-focus/focus-visible rules
- `QuestBoard.Service/Views/Events/Index.cshtml` - anchor on the desktop clickable row's event title
- `QuestBoard.Service/Views/QuestLog/Index.cshtml` - anchor on the quest log card's title
- `QuestBoard.Service/Views/Quest/Index.cshtml` - anchor on the poster card's title, ownership-conditional href
- `QuestBoard.Service/Views/Events/Index.Mobile.cshtml` - anchor on the availability card's title
- `QuestBoard.Service/Views/Calendar/Index.Mobile.cshtml` - anchors on the agenda event title and the agenda quest title
- `QuestBoard.Service/Views/Players/Index.Mobile.cshtml` - anchor on the dungeon master row's name
- `QuestBoard.Service/Views/Characters/Index.Mobile.cshtml` - anchors on both character list rows' names
- `QuestBoard.Service/Views/Contacts/Index.Mobile.cshtml` - anchor on the contact row's name
- `QuestBoard.Service/Views/QuestLog/Index.Mobile.cshtml` - anchor on the quest log item's title
- `QuestBoard.Service/Views/DungeonMaster/Profile.Mobile.cshtml` - anchor on the quest history item's title
- `QuestBoard.Service/Views/Quest/Index.Mobile.cshtml` - anchor on the quest card's title, reusing the precomputed `navUrl`
- `QuestBoard.IntegrationTests/Controllers/RowNavigationAccessibilityTests.cs` - new test file, two facts

## Decisions Made
- Plain `href="@Url.Action(...)"` used instead of the anchor tag helper (`asp-action`/`asp-route-id`) across all thirteen sites, because the tag helper appends its generated `href` after author-written attributes, which would break the mandated `class` then `href` attribute order the tests assert on.
- The two conditional-destination sites (`Quest/Index.cshtml` and `Quest/Index.Mobile.cshtml`) keep their conditional target by either repeating the identical ownership expression verbatim (desktop) or reusing the precomputed `navUrl` local (mobile) rather than recomputing or simplifying it, so a non-owner's anchor cannot diverge from the handler's read-surface destination.
- The new `RowNavigationAccessibilityTests.cs` regex asserts on the anchor's class, its attribute order, and the destination event id — not the full href string — so the fact survives a routing change but still fails if the anchor is removed, renamed, or repointed.

## Deviations from Plan

None - plan executed exactly as written. One documentation-only discrepancy was observed and is noted below for transparency, not treated as a deviation requiring action:

- The plan's Task 1 and Task 2 acceptance criteria state `dotnet test ... --filter "FullyQualifiedName~EventsOverview"` should report "27 passed, 0 failed". The actual count in this codebase state is 22 passed, 0 failed (verified by `--list-tests`: 17 tests in `EventsOverviewControllerIntegrationTests` + 5 in `EventsOverviewTenantIsolationTests`). This is a stale expected count in the plan text, not a regression introduced by this plan — the filtered suite is green both before and after every task in this plan, and the plan's own overall `<verification>` section (which does not hardcode this figure) is the binding contract.

## Issues Encountered
None.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- All thirteen mouse-only click destinations identified by code review finding IN-06 are now keyboard-reachable; no known remaining mouse-only clickable row or card in the application.
- The `.row-nav-link` class and the wrap-only-the-primary-text-node pattern are available for any future clickable row/card so the mouse-only idiom is not reintroduced.
- Human spot-check of keyboard tab order and focus-ring visibility on the availability overview and quest index is recorded for `/gsd-verify-work` per the plan's `<verification>` section; it does not block this plan's completion.

---
*Phase: 77-availability-overview-page*
*Completed: 2026-08-29*
