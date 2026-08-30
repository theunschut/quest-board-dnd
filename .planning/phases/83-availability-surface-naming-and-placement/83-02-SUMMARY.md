---
phase: 83-availability-surface-naming-and-placement
plan: 02
subsystem: ui
tags: [razor, bootstrap, authorization]

# Dependency graph
requires:
  - phase: 83-availability-surface-naming-and-placement
    provides: "plan 01's Board Availability rename and the shared .header-subtitle CSS rule this plan's My Agenda subtitle depends on"
  - phase: 82-personal-cross-board-event-agenda
    provides: the My Agenda surface this plan adds a DM-only return button to
provides:
  - My Agenda header subtitle stating its cross-board, events-only scope, on both layouts
  - DM-only "Board Availability" return button on My Agenda, on both layouts
  - Calendar page's cross-link renamed to "Board Availability" and gated DM-only, on both layouts
  - CalendarButtonStyleTests re-seeded as a Dungeon Master with a new player-absent theory
affects: [83-03, 83-04]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Inline DungeonMasterOnly authorization check at each call site (no shared partial), reusing the existing AuthorizationService.AuthorizeAsync idiom already used in both layouts"
    - "Role-flip test proof (DM sees it / Player does not, plus a companion Contain assertion for the untouched adjacent button) instead of presence-only assertions"

key-files:
  created: []
  modified:
    - QuestBoard.Service/Views/Agenda/Index.cshtml
    - QuestBoard.Service/Views/Agenda/Index.Mobile.cshtml
    - QuestBoard.Service/Views/Calendar/Index.cshtml
    - QuestBoard.Service/Views/Calendar/Index.Mobile.cshtml
    - QuestBoard.IntegrationTests/Controllers/CalendarButtonStyleTests.cs

key-decisions:
  - "Wrote the new CalendarButtonStyleTests.cs test file with LF line endings via the Write tool, then converted to CRLF in a follow-up pass before running acceptance criteria and tests, to satisfy CLAUDE.md's Windows/CRLF convention."

requirements-completed: [EVTNAME-01, EVTNAME-03, EVTNAME-05, EVTNAME-06]

coverage:
  - id: D1
    description: "My Agenda keeps its name and gains a subtitle stating its cross-board, events-only scope on desktop and mobile"
    requirement: "EVTNAME-03"
    verification:
      - kind: integration
        ref: "grep -c 'Upcoming events across all your boards' Views/Agenda/Index.cshtml + Index.Mobile.cshtml (both 1); dotnet test AgendaControllerIntegrationTests|AgendaMobileRenderTests|AgendaTenantIsolationTests"
        status: pass
    human_judgment: false
  - id: D2
    description: "A Dungeon Master standing on My Agenda has a header button back to Board Availability; a player does not"
    requirement: "EVTNAME-05"
    verification:
      - kind: integration
        ref: "grep -c 'AuthorizeAsync(User, \"DungeonMasterOnly\")' Views/Agenda/Index.cshtml + Index.Mobile.cshtml (both 1); dotnet build (Razor compiles the conditional)"
        status: pass
    human_judgment: true
    rationale: "The role-flip proof (DM sees the My Agenda button / Player does not) is exercised by plan 83-04's dedicated nav test cases, not by this plan's own test filter — this plan only proves the markup compiles and the existing Agenda test classes stay green."
  - id: D3
    description: "The Calendar page's cross-link to the board-scoped grid reads Board Availability and renders only for a Dungeon Master, on both layouts; the adjacent My Agenda button is unchanged and stays visible to everyone"
    requirement: "EVTNAME-01"
    verification:
      - kind: integration
        ref: "grep -c 'Board Availability'/'Availability Overview'/'AuthorizeAsync' on both Calendar views; dotnet test CalendarControllerIntegrationTests|CalendarBoardTypeScopeTests|CalendarHorizonBannerTests"
        status: pass
    human_judgment: false
  - id: D4
    description: "CalendarButtonStyleTests proves both the filled-button styling and the new visibility rule, and is green"
    requirement: "EVTNAME-06"
    verification:
      - kind: integration
        ref: "dotnet test --filter CalendarButtonStyleTests (4/4 passed: 2 DM styling facts + 1 player-absent theory over 2 user agents)"
        status: pass
    human_judgment: false

duration: ~20min
completed: 2026-08-30
status: complete
---

# Phase 83 Plan 02: My Agenda / Board Availability Cross-Link Symmetry Summary

**Made the availability surface pair symmetric: My Agenda gained a subtitle and a DM-only return link to Board Availability, the Calendar page's cross-link was renamed and DM-gated in both layouts, and CalendarButtonStyleTests was re-seeded as a Dungeon Master with a new player-absent theory so the suite never went red.**

## Performance

- **Duration:** ~20 min
- **Completed:** 2026-08-30
- **Tasks:** 3/3
- **Files modified:** 5

## Accomplishments
- Added the `isDm` hoisted local (mirroring `hasOtherBoardRow`) and a static subtitle reading "Upcoming events across all your boards" to both `Agenda/Index.cshtml` and `Index.Mobile.cshtml`, plus a DM-only "Board Availability" return button.
- Renamed the Calendar page's first cross-link from "Availability Overview" to "Board Availability" and wrapped it in the existing `DungeonMasterOnly` authorization idiom on both `Calendar/Index.cshtml` and `Index.Mobile.cshtml`, leaving the adjacent "My Agenda" button untouched and unconditional.
- Re-seeded both existing `CalendarButtonStyleTests` cases as a Dungeon Master (renamed to `DesktopCalendar_BoardAvailabilityLink_DmSeesFilled_NotOutline` / `MobileCalendar_BoardAvailabilityLink_DmSeesFilled_NotOutline`), added a `DesktopUserAgent` constant, and added a new `[Theory]` (`Calendar_BoardAvailabilityLink_AbsentForPlayer`) proving a Player sees "My Agenda" but not "Board Availability" on both user agents.

## Task Commits

Each task was committed atomically:

1. **Task 1: Add the My Agenda subtitle and DM-only return button on BOTH layouts** - `2bb11059` (feat)
2. **Task 2: Rename and DM-gate the Calendar page cross-link on BOTH layouts** - `5141ad4c` (feat)
3. **Task 3: Re-seed CalendarButtonStyleTests as a DM and add the player-absent case** - `590a910e` (test)

**Plan metadata:** committed via the final metadata commit step (see below).

## Files Created/Modified
- `QuestBoard.Service/Views/Agenda/Index.cshtml` - hoisted `isDm`, header wrapped in `d-flex justify-content-between align-items-start`, subtitle paragraph, DM-only Board Availability button
- `QuestBoard.Service/Views/Agenda/Index.Mobile.cshtml` - same four changes in the mobile shape (new wrapper div, `h4 class="mb-0"`, `me-1` icon spacing on the button)
- `QuestBoard.Service/Views/Calendar/Index.cshtml` - first cross-link wrapped in a `DungeonMasterOnly` check, label changed to "Board Availability"
- `QuestBoard.Service/Views/Calendar/Index.Mobile.cshtml` - same gating and rename in the mobile `d-grid` stack
- `QuestBoard.IntegrationTests/Controllers/CalendarButtonStyleTests.cs` - widened class doc comment, added `DesktopUserAgent`, re-seeded both existing cases as a DM, added the player-absent theory

## Decisions Made
- No architectural decisions required — all edits followed the UI-SPEC's Modification 7-10 markup verbatim. The only implementation note worth recording: the new test file needed a post-write CRLF conversion pass (the Write tool emitted LF) to comply with CLAUDE.md's Windows/CRLF requirement; verified before running any acceptance criteria or tests.

## Deviations from Plan

None - plan executed exactly as written. All acceptance criteria greps matched their predicted counts on the first attempt.

## Issues Encountered

The build and all targeted test filters (Agenda: 37/37, Calendar: 23/23, CalendarButtonStyleTests: 4/4, combined final verification: 64/64) passed cleanly on first run. Concurrent-session risk flagged by plan 83-01's summary did not materialize during this plan's execution — `git status --short` was clean before starting and each commit was staged with explicit file paths, verified via `git diff --cached --name-only` before committing.

**Requirement IDs still not minted.** `requirements mark-complete EVTNAME-01 EVTNAME-03 EVTNAME-05 EVTNAME-06` returned all four as `not_found` — this is the same pre-existing gap plan 83-01's summary already flagged: `.planning/REQUIREMENTS.md` has no `EVTNAME-*` section at all. Not fixable at execution time (minting the section is planning-document work outside this plan's file scope); carried forward for whoever closes out phase 83's requirement coverage.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

The availability surface pair is now symmetric end to end: both pages state whose scope they cover and both events-only, and every cross-link between them (Agenda→Events, Calendar→Events) is Dungeon-Master-only while the underlying `/Events` route itself stays open per D-09. `CalendarButtonStyleTests` no longer references the retired "Availability Overview" string anywhere. Plans 83-03 (nav placement) and 83-04 (LayoutNavigationTests role-flip coverage, D-15 guard class) can proceed — nothing in this plan touches `_Layout.cshtml`/`_Layout.Mobile.cshtml` or the DM dropdown/flat-block insertion those plans own.

---
*Phase: 83-availability-surface-naming-and-placement*
*Completed: 2026-08-30*

## Self-Check: PASSED

- FOUND: QuestBoard.Service/Views/Agenda/Index.cshtml
- FOUND: QuestBoard.Service/Views/Agenda/Index.Mobile.cshtml
- FOUND: QuestBoard.Service/Views/Calendar/Index.cshtml
- FOUND: QuestBoard.Service/Views/Calendar/Index.Mobile.cshtml
- FOUND: QuestBoard.IntegrationTests/Controllers/CalendarButtonStyleTests.cs
- FOUND: commit 2bb11059
- FOUND: commit 5141ad4c
- FOUND: commit 590a910e
