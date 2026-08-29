---
phase: 82-personal-cross-board-event-agenda
plan: 03
subsystem: web
tags: [mvc, razor, session-filter, tenant-isolation, cross-board-read]

requires:
  - phase: 82-personal-cross-board-event-agenda
    provides: 82-02 (IEventService.GetCrossBoardAgendaAsync and AgendaOptions)
provides:
  - AgendaController with a fresh-membership, filter-intersecting Index action at /Agenda
  - GroupSessionMiddleware exemption so the agenda is reachable with no active board
  - Five agenda view models plus their AutoMapper projections
  - Desktop Views/Agenda/Index.cshtml with rosters inline, board identity, and a shared
    switch-confirm modal reusing GroupPickerController.SelectGroup unchanged
  - AgendaControllerIntegrationTests (11 facts) covering the happy path, all three empty
    states, the no-active-board case, multi-board filtering and the foreign-id case
affects: [82-04, 82-05 (mobile view and nav entries), any future page needing the agenda route]

tech-stack:
  added: []
  patterns:
    - "Session-stored CSV filter (SessionKeys.AgendaBoardFilter) narrowed by intersection
       against a fresh membership read on every request, never trusted on its own"
    - "Raw IQueryCollection read for a repeated-key filter parameter, because ASP.NET Core's
       scalar string model binder only keeps the first value of a repeated query key and
       coerces an empty value to null"
    - "GroupSessionMiddleware route exemption via the existing ControllerNameOf<T>() helper,
       scoped to exactly one new controller"

key-files:
  created:
    - QuestBoard.Service/ViewModels/AgendaViewModels/AgendaViewModel.cs
    - QuestBoard.Service/ViewModels/AgendaViewModels/AgendaRowViewModel.cs
    - QuestBoard.Service/ViewModels/AgendaViewModels/AgendaRosterEntryViewModel.cs
    - QuestBoard.Service/ViewModels/AgendaViewModels/AgendaBoardOptionViewModel.cs
    - QuestBoard.Service/ViewModels/AgendaViewModels/AgendaEmptyState.cs
    - QuestBoard.Service/Controllers/AgendaController.cs
    - QuestBoard.Service/Views/Agenda/Index.cshtml
    - QuestBoard.Service/wwwroot/css/agenda.css
    - QuestBoard.IntegrationTests/Tests/AgendaControllerIntegrationTests.cs
  modified:
    - QuestBoard.Service/Constants/SessionKeys.cs
    - QuestBoard.Service/Automapper/ViewModelProfile.cs
    - QuestBoard.Service/Middleware/GroupSessionMiddleware.cs
    - QuestBoard.Service/Views/Shared/_Layout.cshtml

key-decisions:
  - "Membership is read fresh from the database on every request in AgendaController.Index
     and never taken from session or claims -- it is the page's authorisation, not a display
     preference"
  - "The requested/stored board filter is intersected against that fresh membership read
     before it can reach the query, on every branch, with no code path that skips the
     intersection -- this is what makes a stale or foreign board id structurally unable to
     widen the result rather than merely unlikely to"
  - "AgendaController.Index reads the 'boards' filter from the raw query collection
     (Request.Query.TryGetValue) instead of trusting the bound scalar `boards` parameter's
     value for the three-state branch, because ASP.NET Core's SimpleTypeModelBinder keeps
     only the first value of a repeated query key and converts an empty value to null --
     which would otherwise make the filter form's leading hidden empty field silently
     discard every checked box behind it"
  - "GroupSessionMiddleware's exemption list gained exactly one new entry, derived the same
     nameof-based way as the existing two, so the agenda is reachable with no active board
     without loosening the middleware for any other path"
  - "No SuperAdmin branch in AgendaController -- the board picker's all-groups read is never
     called from this controller, proven both by a static grep in the plan's acceptance
     criteria and by an integration fact using a SuperAdmin client with memberships removed"

patterns-established:
  - "Raw-query-collection read for any future filter parameter that a form submits as
     repeated same-named fields (checkboxes) plus a leading empty sentinel field -- do not
     bind that shape to a scalar string action parameter"

requirements-completed:
  - EVTAGENDA-01
  - EVTAGENDA-02
  - EVTAGENDA-03
  - EVTAGENDA-04
  - EVTAGENDA-06
  - EVTAGENDA-08
  - EVTAGENDA-09
  - EVTAGENDA-10

coverage:
  - id: T1
    description: "A signed-in member can open /Agenda and see every upcoming event across all of their boards, one row per event, with the board named and the whole roster on the row"
    requirement: "EVTAGENDA-01"
    verification:
      - kind: integration
        ref: "AgendaControllerIntegrationTests.Agenda_MemberOfOneBoardWithOneUpcomingEvent_ShowsTitleBoardNameOwnAnswerAndRoster"
        status: pass
    human_judgment: false
  - id: T2
    description: "The page loads when the viewer has no active board selected, instead of diverting to the board picker"
    requirement: "EVTAGENDA-06"
    verification:
      - kind: integration
        ref: "AgendaControllerIntegrationTests.Agenda_NoActiveBoardSelected_RendersAgenda_NotRedirectToPicker"
        status: pass
      - kind: integration
        ref: "GroupSessionMiddlewareIntegrationTests (12 facts, unaffected)"
        status: pass
    human_judgment: false
  - id: T3
    description: "A board id the viewer does not belong to can never enter the query, whether on the query string or a stale session value"
    requirement: "EVTAGENDA-02"
    verification:
      - kind: integration
        ref: "AgendaControllerIntegrationTests.Agenda_ForeignBoardIdInFilter_NeverWidensTheResult"
        status: pass
      - kind: integration
        ref: "AgendaControllerIntegrationTests.Agenda_SuperAdminWithNoMemberships_ShowsNoBoardsEmptyState_NotOtherBoardsEvents"
        status: pass
    human_judgment: false
  - id: T4
    description: "A row on a non-active board prompts before switching via the existing board-selection action; a row on the active board goes straight to the event"
    requirement: "EVTAGENDA-04"
    verification:
      - kind: integration
        ref: "AgendaControllerIntegrationTests.Agenda_RowOnActiveBoard_RendersDirectLink_RowOnOtherBoard_RendersSwitchModalControl"
        status: pass
      - kind: integration
        ref: "GroupPickerControllerIntegrationTests (8 facts, unaffected -- SelectGroup reused unchanged)"
        status: pass
    human_judgment: false
  - id: T5
    description: "The three empty states are told apart, and the recoverable one carries its own reset control"
    requirement: "EVTAGENDA-03"
    verification:
      - kind: integration
        ref: "AgendaControllerIntegrationTests (NoBoards / NoUpcomingEvents / AllBoardsFiltered facts, 3 of the 11)"
        status: pass
    human_judgment: false
  - id: T6
    description: "The board filter narrows correctly for multiple checked boxes submitted alongside the form's leading empty hidden field"
    requirement: "EVTAGENDA-02"
    verification:
      - kind: integration
        ref: "AgendaControllerIntegrationTests.Agenda_MultipleBoardsChecked_NarrowsToExactlyThoseBoards"
        status: pass
    human_judgment: false

duration: ~65min
completed: 2026-08-29
status: complete
---

# Phase 82 Plan 03: Cross-Board Agenda Controller, Views and Integration Suite Summary

**AgendaController reads the viewer's board memberships fresh on every request, intersects the remembered filter against them before any predicate sees it, and renders a desktop page with inline rosters, per-row board identity, and a shared switch-confirm modal that reuses the existing board-selection action unchanged.**

## Performance

- **Duration:** ~65 min
- **Completed:** 2026-08-29
- **Tasks:** 3
- **Files modified:** 13 (9 created, 4 modified)

## Accomplishments

- Five agenda view models (`AgendaViewModel`, `AgendaRowViewModel`, `AgendaRosterEntryViewModel`, `AgendaBoardOptionViewModel`, `AgendaEmptyState`) and their AutoMapper projections, with the controller-only fields (`BoardName`, `BoardType`, `IsActiveBoard`) explicitly `.Ignore()`d in the profile rather than left to convention.
- `SessionKeys.AgendaBoardFilter` -- the first session key in the file to hold more than one value, kept as a plain comma-separated string rather than introducing a serializer.
- `AgendaController.Index` -- reads memberships fresh via `IGroupService.GetGroupsForUserAsync` on every request, intersects the requested/stored filter against that fresh set before it reaches `GetCrossBoardAgendaAsync`, clamps `take` server-side, and distinguishes the three empty states without a second probe query. No SuperAdmin branch exists.
- `GroupSessionMiddleware` gained exactly one new exempt-path entry (`ControllerNameOf<AgendaController>()`), so the agenda is reachable with no active board without touching the redirect, 409, or membership-revalidation behavior for any other path.
- `Views/Agenda/Index.cshtml` -- the desktop page: `modern-card` shell, per-row board identity (name + shipped type badge + Active badge), the viewer's own answer and full roster rendered through the shared `_AvailabilityCell.cshtml` partial by full path, a single focusable action control per row (a real `<a>` on the active board, a real `<button>` opening the shared switch-confirm modal on any other board), the three empty states, and filter-preserving paging.
- `agenda.css` and one added `<link>` in `_Layout.cshtml`, touching nothing else in that file (the nav entry is a later wave's edit).
- `AgendaControllerIntegrationTests` -- 11 facts: the happy path, an event with no viewer signup, a cancelled event's absence, all three empty states, the no-active-board case, a SuperAdmin with memberships forcibly removed, the two row-action variants, a multi-board filter selection, and a foreign board id proven not to widen the result.

## Task Commits

Each task was committed atomically:

1. **Task 1: Agenda view models, mapping and the session key** - `99895118` (feat)
2. **Task 2: AgendaController and the no-active-board exemption** - `7ca60311` (feat)
3. **Task 3: Desktop agenda view, switch-confirm modal, stylesheet, and the integration suite** - `293c11f6` (feat)
4. **Additional test: foreign board id never widens the result** - `3d3a8701` (test)

_No plan-metadata commit in worktree mode -- STATE.md/ROADMAP.md are owned by the orchestrator._

## Files Created/Modified

- `QuestBoard.Service/ViewModels/AgendaViewModels/AgendaViewModel.cs` - the whole agenda container, mirroring `EventOverviewViewModel`'s `CanShowMore` shape
- `QuestBoard.Service/ViewModels/AgendaViewModels/AgendaRowViewModel.cs` - one row: event, board, viewer's cell, roster
- `QuestBoard.Service/ViewModels/AgendaViewModels/AgendaRosterEntryViewModel.cs` - one roster member's answer
- `QuestBoard.Service/ViewModels/AgendaViewModels/AgendaBoardOptionViewModel.cs` - one filter checklist entry
- `QuestBoard.Service/ViewModels/AgendaViewModels/AgendaEmptyState.cs` - the four-value empty-state enum
- `QuestBoard.Service/Constants/SessionKeys.cs` - adds `AgendaBoardFilter`
- `QuestBoard.Service/Automapper/ViewModelProfile.cs` - adds the two agenda maps, ignoring controller-set fields explicitly
- `QuestBoard.Service/Controllers/AgendaController.cs` - the `/Agenda` route, membership-fresh read, filter intersection, empty-state resolution
- `QuestBoard.Service/Middleware/GroupSessionMiddleware.cs` - adds the agenda's exempt-path entry
- `QuestBoard.Service/Views/Agenda/Index.cshtml` - the desktop page
- `QuestBoard.Service/wwwroot/css/agenda.css` - the row/roster layout rules
- `QuestBoard.Service/Views/Shared/_Layout.cshtml` - one added stylesheet `<link>`
- `QuestBoard.IntegrationTests/Tests/AgendaControllerIntegrationTests.cs` - the integration suite

## Decisions Made

- Membership is read fresh from the database on every request in `AgendaController.Index`, never from session or claims -- membership is this page's authorisation, so a board the viewer has left must disappear on the very next page load.
- The requested or stored filter selection is intersected against that fresh membership read on every branch before it reaches the query, with no code path that can skip the intersection. This is the property the plan's threat model requires and the new `Agenda_ForeignBoardIdInFilter_NeverWidensTheResult` fact proves directly.
- No SuperAdmin branch exists in `AgendaController` -- the board picker's all-groups read (`GetAllWithMemberCountAsync`) is never called from this controller. Proven both by a static grep (an acceptance criterion) and by an integration fact using a SuperAdmin client whose membership was forcibly removed after creation.
- `GroupSessionMiddleware`'s exempt-path list gained exactly one new entry, derived through the same `ControllerNameOf<T>()` helper as the two existing entries, so a future rename stays visible here without loosening the middleware for any other path.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] `AgendaController.Index` bound the `boards` filter incorrectly for its own specified multi-select shape**
- **Found during:** Task 3, while writing the "all boards deselected" integration fact -- it failed because the effective filter resolved to "show every board" instead of "show none".
- **Issue:** The plan's own UI markup emits a leading empty `<input type="hidden" name="boards" value="" />` followed by one checkbox per board, all sharing the name `boards`, so a real submission produces a repeated query key (e.g. `boards=&boards=1&boards=2`). ASP.NET Core's `SimpleTypeModelBinder` for a scalar `string?` action parameter keeps only the *first* value of a repeated query key and additionally coerces an empty first value to `null` (the built-in `ConvertEmptyStringToNull` behavior for string binding). Because the hidden field is emitted first in the markup, the bound `boards` parameter was **always** either `null` (any real filter submission, checked boxes included) or itself empty-turned-null (the single "deselect all" case) -- both indistinguishable from "the parameter was never supplied", so the filter could never actually narrow anything and the "deselect all" empty state could never trigger.
- **Fix:** `AgendaController.Index` now reads the raw query collection via `Request.Query.TryGetValue("boards", out var rawBoardsValues)` and uses `rawBoardsValues.ToString()` (which joins repeated values with commas) for the three-state branch, instead of trusting the bound scalar parameter. `boardsProvided` (from `TryGetValue`'s bool) replaces the old `boards == null` presence check so "key absent" and "key present with an empty value" are distinguishable, which the null-vs-empty session/reset branches depend on. The `boards` action parameter is kept in the signature for its documentary/routing value; the actual branch logic no longer reads it directly.
- **Verified by:** `Agenda_ViewerDeselectedEveryBoard_ShowsAllBoardsFilteredOutEmptyState_WithResetControl` (single empty value) and the new `Agenda_MultipleBoardsChecked_NarrowsToExactlyThoseBoards` (repeated values alongside the hidden field, mirroring the real form submission shape) -- both pass with the fix, and were confirmed to fail without it via a throwaway diagnostic fact run before the fix and removed afterward.
- **Files modified:** `QuestBoard.Service/Controllers/AgendaController.cs`
- **Commit:** `293c11f6` (fix folded into the Task 3 commit, since the integration suite that surfaced it and the fix landed together); the follow-up foreign-id proof landed separately as `3d3a8701`.

## Issues Encountered

None beyond the bug documented above. The `rtk`-proxied shell `grep -c` continues to be unreliable against these CRLF files in this session (confirmed again while double-checking `ControllerNameOf<` counts); every acceptance-criteria count in this plan was verified with the sandboxed `Grep` tool instead.

## Known Stubs

None -- every view path renders real controller-supplied data; no hardcoded empty values or placeholder copy reach the page.

## Next Phase Readiness

`/Agenda` is a fully working desktop surface: reachable with no active board, correctly scoped and filterable by fresh membership, and backed by an 11-fact integration suite. Phase 82 Plan 04/05 (mobile view and nav entries) can build on this controller and its view models without further changes to this layer -- the only shared file touched here, `_Layout.cshtml`, was edited in exactly the stylesheet region this plan owns, leaving the nav block for the next wave.

## Self-Check: PASSED

- FOUND: QuestBoard.Service/Controllers/AgendaController.cs
- FOUND: QuestBoard.Service/Views/Agenda/Index.cshtml
- FOUND: QuestBoard.Service/wwwroot/css/agenda.css
- FOUND: QuestBoard.IntegrationTests/Tests/AgendaControllerIntegrationTests.cs
- FOUND: 99895118 (Task 1 commit)
- FOUND: 7ca60311 (Task 2 commit)
- FOUND: 293c11f6 (Task 3 commit)
- FOUND: 3d3a8701 (additional test commit)

---
*Phase: 82-personal-cross-board-event-agenda*
*Completed: 2026-08-29*
