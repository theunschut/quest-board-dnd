---
phase: 91-linked-boards-with-a-shared-calendar
plan: 05
subsystem: calendar
tags: [linked-boards, calendar, overlay, razor, read-only, tenant-isolation]
status: complete

requires:
  - phase: 91-linked-boards-with-a-shared-calendar
    provides: "91-02 own-calendar DM-only clause; 91-03 ILinkedBoardSetService; 91-04 linked board lookup and pinned event/quest reads"
provides:
  - "ILinkedCalendarService.GetForViewerAsync: fresh per-request overlay of the other boards in the active board's set"
  - "CalendarViewModel.LinkedQuests/LinkedEvents/LinkedBoards, CalendarDay.Linked, LinkedDayEntries, LinkedEventOnDay, LinkedQuestOnDay"
  - "_LinkedDayEntries partial, linked-day-count badge and Details-page note slot in _Calendar.cshtml"
affects: [91-07, 91-08, 91-09, 91-10]

tech-stack:
  added: []
  patterns:
    - "Linked entries are a separately typed read-only overlay, never merged into the own Quest/Event collections"
    - "Fail-closed re-check after materialization over the same id set the query used, logged as an error on a drop"

key-files:
  created:
    - QuestBoard.Domain/Interfaces/ILinkedCalendarService.cs
    - QuestBoard.Domain/Services/LinkedCalendarService.cs
    - QuestBoard.Service/ViewModels/CalendarViewModels/LinkedDayEntries.cs
    - QuestBoard.Service/ViewModels/CalendarViewModels/LinkedEventOnDay.cs
    - QuestBoard.Service/ViewModels/CalendarViewModels/LinkedQuestOnDay.cs
    - QuestBoard.Service/Views/Shared/_LinkedDayEntries.cshtml
    - QuestBoard.UnitTests/Services/LinkedCalendarServiceTests.cs
    - QuestBoard.IntegrationTests/Tests/LinkedBoardCalendarTenantIsolationTests.cs
  modified:
    - QuestBoard.Domain/Extensions/ServiceExtensions.cs
    - QuestBoard.Service/Controllers/QuestBoard/CalendarController.cs
    - QuestBoard.Service/ViewModels/CalendarViewModels/CalendarViewModel.cs
    - QuestBoard.Service/ViewModels/CalendarViewModels/CalendarDay.cs
    - QuestBoard.Service/Views/Shared/_Calendar.cshtml

key-decisions:
  - "SuperAdmin is a separate flag to the overlay service: it counts as Admin for DM-only visibility but never as membership, so a site admin's linked chips render as plain blocks unless they hold a UserGroups row"
  - "The DateTime-to-DateOnly conversion in CalendarViewModel moved into one private static helper used by both own and linked event placement, carrying the existing single-place comment"
  - "The chip body is a Razor templated delegate shared by the anchor and div branches so member and non-member chips cannot drift apart"

patterns-established:
  - "Linked chips render through their own partial and model, with no form controls, element ids or data attributes"

requirements-completed: []

duration: 45min
completed: 2026-10-16
actuals:
  tokens: 15400
  tasks: 3
  commits: 4
---

# Phase 91 Plan 05: Linked calendar overlay on the desktop calendar Summary

**The desktop /calendar now shows the other boards in the active board's linked set after its own entries, read-only: links for members, plain blocks for everyone else, DM-only sessions decided by the viewer's role on the owning board, and foreign rows dropped and logged before anything renders.**

## Accomplishments

- `LinkedCalendarService` does a fresh set lookup per request, short-circuits with no cross-board read when there is no active board, no viewer or no other board, re-checks both reads against the same linked id set (error log with counts on a drop), stamps board name and membership, and skips the quest read for a Campaign set.
- DM-only linked quests are kept only for a DungeonMaster or Admin of the owning board, or a site admin; the viewer's role on the viewed board does not matter.
- `CalendarController` builds the [first of month, first of next month) window from the requested year and month (no ambient clock read) and fills the linked view-model collections.
- `CalendarViewModel` places linked events (all-day first, then time, then title) and linked quests (same open and finalized placement rules as own quests) into `CalendarDay.Linked`.
- `_LinkedDayEntries.cshtml` renders member chips as one plain anchor to `/Events/Details/{id}` or `/Quest/Details/{id}` and non-member chips as inert divs, with title line, aria-hidden detail line, visually-hidden status text, tooltip and cancelled modifier.
- `_Calendar.cshtml` gains the `linked-day-count` badge outside `day-cell-items`, the linked slot after the own-quest block, and the Details-page `calendar-linked-note`.

## Task Commits

1. Task 1 (tracer): events end to end - `d63e74e5`
2. Task 2 RED: failing linked quest facts - `d2920b8a`
3. Task 2 GREEN: linked quests, DM-only role rule, Campaign skip - `77469996`
4. Task 3: unit-level fail-closed proofs - `db1beb91`

## Verification

- Tracer gate (autonomous form): after the Task 1 commit, `LinkedBoardCalendarTenantIsolationTests` plus the calendar and tenant-isolation suites passed (70) and the Architecture seam tests passed (72), then execution continued.
- Task 2 RED: 10 of the new facts failed before the implementation (Campaign passed by construction because quests were absent).
- `dotnet test` over the whole solution: UnitTests 905 passed; IntegrationTests 1015 passed, 11 skipped (pre-existing skips).
- No diff under `QuestBoard.UnitTests/Architecture/`; `LinkedCalendarService.cs` contains no filter-bypass method name; `_LinkedDayEntries.cshtml` contains none of the banned substrings and no `<input` or `<form`.
- Not proven by InMemory: SQL translation of the linked reads (Plan 04's note, covered by the local SQL Server checkpoint in the final plan).

## Deviations from Plan

None - plan executed exactly as written. Two small test-side adjustments: the isolation tests take the seeded event id from the seeding helper instead of re-querying, because the unfiltered seeding context still applies the fail-closed board filter to Events; the board-type column is also set on the boards a helper puts into a set so a Campaign fact matches the lookup's type check.

## Known Stubs

None. `calendar.css` styling for `linked-entry*`, `linked-day-entries`, `linked-day-count` and `calendar-linked-note`, and the Legend, are owned by Plan 07; the markup and class names are in place. `LinkedBoards` on the Quest Details month models is filled by Plan 09; the note renders only when it is set.

## Threat Flags

None beyond the plan's threat model (T-91-40, T-91-13, T-91-14, T-91-15, T-91-17, T-91-20 mitigated as planned).

## Self-Check: PASSED

- All eight created files exist; commits `d63e74e5`, `d2920b8a`, `77469996`, `db1beb91` are in git log.
