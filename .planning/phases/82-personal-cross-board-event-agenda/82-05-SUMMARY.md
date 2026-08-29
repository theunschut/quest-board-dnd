---
phase: 82-personal-cross-board-event-agenda
plan: 05
subsystem: web
tags: [navigation, razor, mvc, layout]

requires:
  - phase: 82-personal-cross-board-event-agenda
    provides: 82-03 (AgendaController and the /Agenda route)
provides:
  - Unconditional "My Agenda" nav entry in the desktop user dropdown and the mobile offcanvas
  - Convenience cross-links from Events/Index and Calendar/Index (desktop + mobile) to the agenda
  - EventsController.Details origin marker (from=agenda) and its display-only ViewBag flag
  - Conditional "Back to My Agenda" link on Events/Details.cshtml
  - Four new LayoutNavigationTests cases, including the unresolved-board-type case
affects: []

tech-stack:
  added: []
  patterns:
    - "Nav entry placed outside every board-type condition, unlike the Calendar dropdown it sits
       beside, because the page it points at is scoped by membership, not by a resolved board
       type"
    - "Query-string origin marker used only to set a display-only ViewBag flag, never to widen
       any data access -- the guarded read underneath is unchanged"

key-files:
  created: []
  modified:
    - QuestBoard.Service/Views/Shared/_Layout.cshtml
    - QuestBoard.Service/Views/Shared/_Layout.Mobile.cshtml
    - QuestBoard.Service/Views/Events/Index.cshtml
    - QuestBoard.Service/Views/Events/Index.Mobile.cshtml
    - QuestBoard.Service/Views/Calendar/Index.cshtml
    - QuestBoard.Service/Views/Calendar/Index.Mobile.cshtml
    - QuestBoard.Service/Views/Events/Details.cshtml
    - QuestBoard.Service/Controllers/Events/EventsController.cs
    - QuestBoard.IntegrationTests/Controllers/LayoutNavigationTests.cs

key-decisions:
  - "The My Agenda nav entry sits outside the activeBoardType is BoardType.OneShot or
     BoardType.Campaign gate that wraps the Calendar dropdown -- it is the one unconditional
     path into a page that has no active board type of its own, so gating it there would hide
     it in exactly the situation it exists for"
  - "The four cross-links on the availability overview and the calendar are convenience only;
     both surfaces already sit behind the resolved-board-type gate, so removing or weakening
     that gate was explicitly out of scope"
  - "The details back-link does not switch the active board back -- two board switches per
     answer is state that is easy to get wrong and hard to notice when it is, so this is a
     plain navigation link, not a second SelectGroup call"
  - "The from=agenda marker on EventsController.Details sets a display-only ViewBag flag; the
     event itself is still fetched through the same board-scoped read as before, so the marker
     cannot make another board's event visible"

requirements-completed:
  - EVTAGENDA-05
  - EVTAGENDA-06

coverage:
  - id: T1
    description: "Every authenticated user can reach the agenda from the user menu on both
      layouts, whatever their role and whether or not a board type has resolved"
    requirement: "EVTAGENDA-05"
    verification:
      - kind: integration
        ref: "LayoutNavigationTests.Nav_OneShotAuthenticated_MyAgendaLinkPresent"
        status: pass
      - kind: integration
        ref: "LayoutNavigationTests.Nav_CampaignAuthenticated_MyAgendaLinkPresent"
        status: pass
      - kind: integration
        ref: "LayoutNavigationTests.Nav_UnresolvedBoardTypeAuthenticated_MyAgendaLinkPresentAndPageReachable"
        status: pass
    human_judgment: false
  - id: T2
    description: "The agenda entry never leaks into the public/anonymous navigation"
    requirement: "EVTAGENDA-05"
    verification:
      - kind: integration
        ref: "LayoutNavigationTests.Nav_Anonymous_MyAgendaLinkAbsent"
        status: pass
    human_judgment: false
  - id: T3
    description: "The availability overview and the calendar both link across to the agenda,
      without either becoming the only way in"
    requirement: "EVTAGENDA-06"
    verification:
      - kind: integration
        ref: "EventsControllerIntegrationTests (29 facts, unaffected)"
        status: pass
      - kind: integration
        ref: "CalendarControllerIntegrationTests (12 facts, unaffected)"
        status: pass
    human_judgment: false
  - id: T4
    description: "A viewer who reached an event's details from the agenda gets a visible way
      back to it, and the write path on that view is untouched"
    requirement: "EVTAGENDA-06"
    verification:
      - kind: integration
        ref: "EventDetailsAvailabilityRenderTests (9 facts, unaffected)"
        status: pass
      - kind: manual
        ref: "git diff --stat Events/Details.cshtml shows insertions only (12 insertions, 0 deletions)"
        status: pass
    human_judgment: false

duration: ~40min
completed: 2026-08-29
status: complete
---

# Phase 82 Plan 05: Agenda Navigation Round Trip Summary

**Adds the one unconditional "My Agenda" nav entry to both layouts (outside every board-type gate), four convenience cross-links from the overview and calendar pages, and a conditional "Back to My Agenda" link on event details driven by a display-only origin marker.**

## Performance

- **Duration:** ~40 min
- **Completed:** 2026-08-29
- **Tasks:** 3
- **Files modified:** 9

## Accomplishments

- `_Layout.cshtml` gained a new `<li>` in the user dropdown, immediately after "Switch Group" and before the logout divider, sitting outside the `activeBoardType is BoardType.OneShot or BoardType.Campaign` gate that wraps the Calendar dropdown above it. `_Layout.Mobile.cshtml` gained the same entry as a flat `<li class="nav-item">` sibling -- this layout has no dropdowns anywhere, so no dropdown behavior was introduced.
- Both entries use `fa-calendar-days text-purple me-2` and the label "My Agenda" -- a different icon from the availability overview's `fa-calendar-check` so the two entries stay visually distinct in the same menu.
- Four cross-link buttons (`btn btn-secondary btn-sm`, filled per the project's button convention) were added: `Events/Index.cshtml` and `Index.Mobile.cshtml` gained a header-area link to the agenda; `Calendar/Index.cshtml` and `Index.Mobile.cshtml` gained a link beside the existing Availability Overview cross-link. All four sit inside the existing resolved-board-type gate on their respective pages -- no gate was removed or weakened.
- `EventsController.Details` gained an optional `string? from = null` parameter and sets `ViewBag.ReturnedFromAgenda` from it. The event is still fetched through the same board-scoped `GetEventWithDetailsAsync` call as before -- the marker is a display-only hint and grants no access.
- `Events/Details.cshtml` gained a conditional block above the outer card, rendering "Back to My Agenda" only when `ViewBag.ReturnedFromAgenda == true`. The active board is deliberately not switched back on this link.
- `LayoutNavigationTests.cs` gained four new theory cases (8 facts across the two user agents): one-shot authenticated, campaign authenticated, an unresolved-board-type case that requests `/Agenda` directly and proves both the nav entry renders and the page itself is reachable with no active board type, and an anonymous case proving the entry never appears in the public navigation.

## Task Commits

Each task was committed atomically:

1. **Task 1: Unconditional agenda entries in both navigations** - `84ce3cf2` (feat)
2. **Task 2: Cross-links from the overview and calendar, and the details back-link** - `6b58fdd2` (feat)
3. **Task 3: Navigation tests, including an unresolved board type** - `57214f9d` (test)

_No plan-metadata commit in worktree mode -- STATE.md/ROADMAP.md are owned by the orchestrator._

## Files Created/Modified

- `QuestBoard.Service/Views/Shared/_Layout.cshtml` - adds the desktop dropdown "My Agenda" entry, outside every board-type gate
- `QuestBoard.Service/Views/Shared/_Layout.Mobile.cshtml` - adds the flat mobile "My Agenda" entry
- `QuestBoard.Service/Views/Events/Index.cshtml` - adds a header cross-link to the agenda
- `QuestBoard.Service/Views/Events/Index.Mobile.cshtml` - adds the mobile equivalent
- `QuestBoard.Service/Views/Calendar/Index.cshtml` - adds a header cross-link beside the existing Availability Overview link
- `QuestBoard.Service/Views/Calendar/Index.Mobile.cshtml` - adds the mobile equivalent
- `QuestBoard.Service/Views/Events/Details.cshtml` - adds the conditional back-link block above the card
- `QuestBoard.Service/Controllers/Events/EventsController.cs` - adds the `from` parameter and `ViewBag.ReturnedFromAgenda` flag on `Details`
- `QuestBoard.IntegrationTests/Controllers/LayoutNavigationTests.cs` - adds four new theory cases covering the agenda nav entry

## Decisions Made

- The nav entry's placement outside the board-type gate is the load-bearing design choice of this plan: it is what keeps the agenda reachable when a board type has not resolved, which is exactly the case the page exists to serve (D-08/D-10).
- The four cross-links are explicitly convenience only. Both host pages already sit behind the resolved-board-type gate, so neither could ever be the unconditional way in regardless of how they were styled; no gate was added, relaxed, or removed around them.
- The details back-link does not re-switch the active board. Two board switches per answer (one to open the event, one implicitly on the way back) was judged to be state that is easy to get wrong and hard to notice, so the link is a plain, unconditional navigation to `/Agenda`.
- The origin marker on `Details` is deliberately display-only. It was verified that the same board-scoped `GetEventWithDetailsAsync` call runs regardless of the marker's value, so a forged or stale `from=agenda` query parameter cannot expose anything the guarded read would not already allow.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] The mobile nav comment initially reintroduced the literal word "dropdown"**
- **Found during:** Task 1 acceptance-criteria verification
- **Issue:** The first draft of the explanatory comment above the new mobile "My Agenda" entry used the word "dropdown" ("This is a flat sibling, not a dropdown -- this layout has none"), which broke the acceptance criterion requiring `grep -c 'dropdown'` on `_Layout.Mobile.cshtml` to remain `0` -- that criterion exists specifically to prove no dropdown markup was introduced into an offcanvas that has none.
- **Fix:** Reworded the comment to say "This is a flat sibling entry, matching every other item in this offcanvas list" without using the word "dropdown" anywhere in the file.
- **Files modified:** `QuestBoard.Service/Views/Shared/_Layout.Mobile.cshtml`
- **Commit:** folded into `84ce3cf2` (caught before the commit was made)

No other deviations. Plan executed as written otherwise.

## Issues Encountered

None. `grep -c` counts for all acceptance criteria were double-checked with the sandboxed `Grep` tool per this project's known CRLF-counting caveat with the shell's `rtk`-proxied `grep`.

## Known Stubs

None -- every added link and block renders real, functioning markup with no placeholder content.

## Threat Flags

None. All new surface (the `from` query marker, the four cross-links, the two nav entries) was already covered by this plan's own threat model (T-82-05-01 through T-82-05-04), and no additional surface outside that register was introduced.

## Next Phase Readiness

The agenda's navigation loop is complete: it is reachable unconditionally from the user menu on both layouts, has convenience cross-links from the two board-scoped surfaces that motivated it, and a viewer who follows an event out of it has a signposted way back. `LayoutNavigationTests` now has 40 passing facts (up from 32), and the full integration suite (579 facts) is green.

## Self-Check: PASSED

- FOUND: QuestBoard.Service/Views/Shared/_Layout.cshtml (My Agenda entry present)
- FOUND: QuestBoard.Service/Views/Shared/_Layout.Mobile.cshtml (My Agenda entry present)
- FOUND: QuestBoard.Service/Views/Events/Index.cshtml (cross-link present)
- FOUND: QuestBoard.Service/Views/Events/Index.Mobile.cshtml (cross-link present)
- FOUND: QuestBoard.Service/Views/Calendar/Index.cshtml (cross-link present)
- FOUND: QuestBoard.Service/Views/Calendar/Index.Mobile.cshtml (cross-link present)
- FOUND: QuestBoard.Service/Views/Events/Details.cshtml (back-link block present)
- FOUND: QuestBoard.Service/Controllers/Events/EventsController.cs (from param + ViewBag flag present)
- FOUND: QuestBoard.IntegrationTests/Controllers/LayoutNavigationTests.cs (4 new theories present)
- FOUND: 84ce3cf2 (Task 1 commit)
- FOUND: 6b58fdd2 (Task 2 commit)
- FOUND: 57214f9d (Task 3 commit)

---
*Phase: 82-personal-cross-board-event-agenda*
*Completed: 2026-08-29*
