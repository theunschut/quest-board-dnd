---
phase: 93-per-board-page-selection-and-home-page
plan: 05
subsystem: ui
tags: [aspnet-mvc, razor, linked-boards, calendar, board-settings]

requires:
  - phase: 93-01
    provides: BoardPageSet, BoardPagePolicy.IsAvailable, GroupEntity.DisabledPages, BoardPageSeed
provides:
  - LinkedBoard.DisabledPages read with the existing linked set query
  - LinkedEventEntry.CanOpen and LinkedQuestEntry.CanOpen decided in LinkedCalendarService
  - Linked calendar entries from a board whose Events or Quests page is off render as plain text on the desktop grid, the phone agenda and the Also that day line
affects: [93-08, 93-09]

actuals:
  tokens: 14000
  tasks: 2
  commits: 4
plan_head_before: ea940b94de16c837b9aa72134023388d4f4b72c2

tech-stack:
  added: []
  patterns:
    - "A per-entry CanOpen flag beside ViewerIsMember: membership keeps its meaning, link eligibility also asks the owning board's page availability"

key-files:
  created:
    - QuestBoard.IntegrationTests/Tests/BoardPageLinkedCalendarTests.cs
  modified:
    - QuestBoard.Domain/Models/LinkedBoard.cs
    - QuestBoard.Domain/Models/LinkedEventEntry.cs
    - QuestBoard.Domain/Models/LinkedQuestEntry.cs
    - QuestBoard.Repository/LinkedBoardSetRepository.cs
    - QuestBoard.Domain/Services/LinkedCalendarService.cs
    - QuestBoard.Service/Views/Shared/_LinkedDayEntries.cshtml
    - QuestBoard.Service/Views/Shared/_LinkedAlsoThatDay.cshtml
    - QuestBoard.Service/Views/Calendar/Index.Mobile.cshtml
    - QuestBoard.UnitTests/Services/LinkedCalendarServiceTests.cs
    - QuestBoard.UnitTests/Repository/LinkedCalendarReadTests.cs

key-decisions:
  - "Inert text, not a redirect, for a linked entry whose owning board no longer offers the page (reuses the non-member markup; avoids a cross-board session switch that ends in a refusal)"
  - "CanOpen added as a new flag rather than folding page availability into ViewerIsMember"
  - "The mask rides on the Groups row the set read already selects; no extra query"

patterns-established:
  - "Views decide link vs plain text from entry.CanOpen; the service decides CanOpen from membership and BoardPagePolicy"

requirements-completed: [D-04, D-07, D-08, D-09]

coverage:
  - id: D1
    description: "A member's event from a board with Events off renders as plain text on the desktop calendar and the phone agenda, still listed with title, board and time"
    requirement: "D-07"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Tests/BoardPageLinkedCalendarTests.cs#Calendar_EventsOffOnTheOwningBoard_EventIsPlainTextForAMember"
        status: pass
    human_judgment: false
  - id: D2
    description: "A member's quest from a board with Quests off renders as plain text on the desktop calendar and the phone agenda"
    requirement: "D-04"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Tests/BoardPageLinkedCalendarTests.cs#Calendar_QuestsOffOnTheOwningBoard_QuestIsPlainTextForAMember"
        status: pass
    human_judgment: false
  - id: D3
    description: "Quest Details on both layouts (desktop linked entries, phone Also that day line) shows entries from switched-off pages as text and keeps them listed"
    requirement: "D-07"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Tests/BoardPageLinkedCalendarTests.cs#QuestDetails_OwningBoardPagesOff_LinkedEntriesAreTextButStillListed"
        status: pass
    human_judgment: false
  - id: D4
    description: "With default settings a member keeps both links and a non-member keeps plain text, on both user agents"
    requirement: "D-09"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Tests/BoardPageLinkedCalendarTests.cs#Calendar_DefaultSettings_MemberStillGetsBothLinks"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Tests/BoardPageLinkedCalendarTests.cs#Calendar_DefaultSettings_NonMemberGetsPlainText"
        status: pass
    human_judgment: false
  - id: D5
    description: "LinkedBoard.DisabledPages comes from the Groups row and CanOpen is decided per owning board, for events and quests, in OneShot and Campaign sets"
    requirement: "D-08"
    verification:
      - kind: unit
        ref: "QuestBoard.UnitTests/Services/LinkedCalendarServiceTests.cs#GetForViewer_EventEntry_CanOpenOnlyForAMemberWhoseBoardStillOffersEvents"
        status: pass
      - kind: unit
        ref: "QuestBoard.UnitTests/Repository/LinkedCalendarReadTests.cs#GetLinkedBoardsForViewer_OtherBoardHasPagesSwitchedOff_CarriesItsDisabledPages"
        status: pass
    human_judgment: false

duration: 20min
completed: 2026-10-21
status: complete
---

# Phase 93 Plan 05: Linked calendars and switched-off pages Summary

**A linked board's calendar now shows another board's events and quests as plain text whenever that board has switched the entry's page off, driven by a per-entry CanOpen flag that combines membership with BoardPagePolicy availability, with no extra query.**

## Performance

- **Duration:** 20 min
- **Started:** 2026-10-21T11:34:00Z
- **Completed:** 2026-10-21T11:55:00Z
- **Tasks:** 2
- **Files modified:** 11

## Accomplishments
- `LinkedBoard.DisabledPages` flows from the `Groups` row the linked set read already selects; `LinkedEventEntry.CanOpen` and `LinkedQuestEntry.CanOpen` are decided in `LinkedCalendarService` as membership plus `BoardPagePolicy.IsAvailable` for Events or Quests.
- The desktop month grid (`_LinkedDayEntries`), the phone agenda rows and the Also that day line (`_LinkedAlsoThatDay`) branch on `CanOpen`; the inert markup is the existing non-member markup, unchanged.
- Entries stay visible with title, board and time; nothing is filtered. `ViewerIsMember` keeps its meaning, so `MoveController` is untouched.

## Task Commits

1. **Task 1 RED: facts for DisabledPages and CanOpen (model properties declared)** - `2faf4a12` (test)
2. **Task 1 GREEN: repository projection and service decision** - `54d016fc` (feat)
3. **Task 2 RED: rendered proof on both user agents** - `1d03ca6a` (test)
4. **Task 2 GREEN: views read CanOpen** - `7e795108` (feat)

**Plan metadata:** recorded in the docs commit carrying this file.

## Files Created/Modified
- `QuestBoard.Domain/Models/LinkedBoard.cs` - `DisabledPages`
- `QuestBoard.Domain/Models/LinkedEventEntry.cs`, `LinkedQuestEntry.cs` - `CanOpen`
- `QuestBoard.Repository/LinkedBoardSetRepository.cs` - projects `Groups.DisabledPages` into `LinkedBoard`
- `QuestBoard.Domain/Services/LinkedCalendarService.cs` - sets `CanOpen` for events and (OneShot only) quests
- `QuestBoard.Service/Views/Shared/_LinkedDayEntries.cshtml`, `_LinkedAlsoThatDay.cshtml`, `Calendar/Index.Mobile.cshtml` - link branch tests `CanOpen`
- `QuestBoard.UnitTests/Services/LinkedCalendarServiceTests.cs`, `Repository/LinkedCalendarReadTests.cs` - new facts
- `QuestBoard.IntegrationTests/Tests/BoardPageLinkedCalendarTests.cs` - 12 theory cases across desktop and mobile user agents

## Decisions Made
Followed the plan's recorded choices (inert text, separate flag, no new query). The EF in-memory provider translated `new BoardPageSet(r.DisabledPages)` after materialisation as written, so no workaround was needed.

## Deviations from Plan

None - plan executed exactly as written.

For the TDD gate: the RED commit for Task 1 also declares the three new model properties, because the tests cannot compile without them; the facts then failed on assertions (six failures, 37 passes), not on compile errors. For Task 2 the view edits were made first, then temporarily reverted from a saved patch to observe RED (six assertion failures, six positive controls passing) and re-applied before the commit.

## Issues Encountered
None.

## Known Stubs

None.

## Threat Flags

None. The mask only decides whether a link renders and is read only for boards already in the viewer's linked set.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- Plan 09 (move page) can rely on `LinkedBoard.DisabledPages` being populated by `GetLinkedBoardsForViewerAsync`.
- Verified: `dotnet test QuestBoard.UnitTests` 1120 passed; the integration filter from the plan (BoardPageLinkedCalendar, LinkedBoard, Calendar, MobileViews) 418 passed.

## Self-Check: PASSED

- `BoardPageLinkedCalendarTests.cs` exists on disk; commits `2faf4a12`, `54d016fc`, `1d03ca6a`, `7e795108` exist; `git rev-list --count ea940b94..HEAD` was 4 before this commit.
- No added source line carries a planning reference (grep count 0 before each commit).
- Line endings preserved (CRLF) on all edited files.

---
*Phase: 93-per-board-page-selection-and-home-page*
*Completed: 2026-10-21*
