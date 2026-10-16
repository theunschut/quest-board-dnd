---
phase: 91-linked-boards-with-a-shared-calendar
plan: 07
subsystem: calendar
tags: [linked-boards, calendar, css, legend, deep-link, tenant-isolation]
status: complete

requires:
  - phase: 91-linked-boards-with-a-shared-calendar
    provides: "91-05 linked overlay markup (_LinkedDayEntries, linked-day-count, calendar-linked-note) and LinkedBoards on CalendarViewModel"
provides:
  - "calendar.css styling for linked chips, count badge, Details note, legend swatch and board names"
  - "Legend 'Linked boards' block on the desktop calendar"
  - "View-model parity, ordering and cancelled proofs; roster no-leak and chip-structure proofs; deep-link member/non-member/SuperAdmin proofs"
affects: [91-08, 91-09, 91-10]

tech-stack:
  added: []
  patterns:
    - "Linked chips have their own selector family and never extend own-chip or vote selectors"
    - "calendar.css changes are additions only, with no media query"

key-files:
  created:
    - QuestBoard.UnitTests/ViewModels/CalendarViewModelLinkedTests.cs
    - QuestBoard.IntegrationTests/Middleware/LinkedBoardDeepLinkTests.cs
  modified:
    - QuestBoard.Service/Views/Calendar/Index.cshtml
    - QuestBoard.Service/wwwroot/css/calendar.css
    - QuestBoard.IntegrationTests/Tests/LinkedBoardCalendarTenantIsolationTests.cs

key-decisions:
  - "Legend board names render in the order LinkedBoards arrives (already name-ordered by the repository), so the view adds no second sort"
  - "The note's text shadow reuses the day-number shadow value; the desktop stylesheet has no month-title rule, only the mobile one carries a title shadow"

patterns-established:
  - "Deep-link proof for a rendered cross-board link: assert the href on the calendar, then follow it with navigation headers on the same cookie client"

requirements-completed: []

duration: 25min
completed: 2026-10-16
actuals:
  tokens: 12500
  tasks: 3
  commits: 3
---

# Phase 91 Plan 07: Desktop linked chip styling, Legend and proofs Summary

**Linked entries on the desktop calendar now read as borrowed bookings: muted dashed chips with a status-coloured edge that reveal board, DM and time on hover or focus, a Legend that names the linked boards, and tests that pin placement parity, ordering, the no-roster-leak rule and member-only links that really switch boards.**

## Accomplishments

- Legend gains a "Linked boards" block (rule, sub-heading, dashed swatch row, one wrapping row per linked board, access note) rendered only when the active board has linked boards. The set name is never shown.
- `calendar.css` gets one commented block: `.linked-day-entries`, `.linked-entry` (own-chip footprint, 0.6 surface, 4px dashed edge), status modifiers, title ellipsis, hidden detail line revealed on hover or `:focus-visible` of an openable chip (grows to 48px), focus ring, static/open cursors, cancelled opacity 0.55 with line-through carried into children, `.linked-day-count` badge and `.calendar-linked-note`. Legend gains `.legend-item.linked` and `.legend-board-name`.
- `CalendarViewModelLinkedTests` (11 facts) proves a linked quest lands on exactly the days and times an own quest with the same data does (open on different days, open twice on one day, finalized with and without a proposed date on the finalized day, other month), events order all-day then time then title, quests order by time then title, default-empty, and cancelled events placed and counted.
- Isolation facts: legend present when linked, absent for no set and for a set of one, alphabetical with two linked boards, no set name; another board's signup name and poll answer never reach the page while its DM name does; the chip carries the detail line, `aria-hidden`, a visually-hidden status ending in "Proposed." and a tooltip with board and DM name.
- `LinkedBoardDeepLinkTests`: a member of both boards follows the chip href and lands on the owning board with `board-switch-toast`; a non-member gets no href and the same URL answers 404; a SuperAdmin without a membership on the linked board gets no href.

## Task Commits

1. Task 1 (tracer): Legend end to end - `5bf8f498`
2. Task 2: chip, badge and note styling plus parity, ordering and no-leak proofs - `e4d947e0`
3. Task 3: deep-link proofs - `da2dbec2`

## Verification

- Tracer gate (autonomous form): `LinkedBoardCalendarTenantIsolationTests` passed (28) after Task 1 before the remaining tasks started.
- `CalendarViewModelLinkedTests` 11 passed; `LinkedBoardCalendarTenantIsolationTests` plus `CalendarButtonStyleTests` 34 passed; `LinkedBoardDeepLinkTests`, `CrossBoardDeepLinkMiddlewareTests`, `CrossBoardLinkRegistryTests` 13 passed.
- Whole solution `dotnet test`: UnitTests 916 passed; IntegrationTests 1036 passed, 11 skipped (pre-existing).
- `calendar.css` diff against the base shows 0 removed lines and no `@media`; `border-left: 4px dashed` appears twice; the six status/state modifiers appear 12 times.
- No diff under `QuestBoard.Service/Middleware` or `CrossBoardLinkRegistry.cs`.
- Not verified here: how the chips look in a browser (no visual check was run); the CSS is checked by the plan's text gates and the markup facts only.

## Deviations from Plan

None - plan executed exactly as written. Two test-side notes: the new roster fact queries the seeded quest with `IgnoreQueryFilters` because the unfiltered seeding context still applies the active-board filter to board B's rows (the same constraint Plan 05 recorded); and the unit tests passed on first run because the view-model placement they pin already existed from Plan 05, so there was no red phase for them.

## Known Stubs

None.

## Threat Flags

None beyond the plan's threat model (T-91-24, T-91-25, T-91-27 mitigated by facts; T-91-26 accepted, additions-only diff confirmed).

## Self-Check: PASSED

- Created files exist: `CalendarViewModelLinkedTests.cs`, `LinkedBoardDeepLinkTests.cs`; commits `5bf8f498`, `e4d947e0`, `da2dbec2` are in git log.
