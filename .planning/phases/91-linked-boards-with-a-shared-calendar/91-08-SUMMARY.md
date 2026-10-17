---
phase: 91-linked-boards-with-a-shared-calendar
plan: 08
subsystem: calendar
tags: [linked-boards, calendar, mobile, razor, css, agenda]
status: complete

requires:
  - phase: 91-linked-boards-with-a-shared-calendar
    provides: "91-05 CalendarViewModel.LinkedBoards, CalendarDay.Linked, LinkedDayEntries, LinkedEventOnDay, LinkedQuestOnDay"
provides:
  - "Phone agenda (Calendar/Index.Mobile.cshtml) lists linked boards' events and quests after the board's own, including days only linked boards use"
  - "Also showing line under the month bar naming the linked boards"
  - "agenda-linked-* markup and calendar.mobile.css rules (note, entry, open/static/cancelled/proposed/finalized, title, time, board)"
affects: [91-10]

tech-stack:
  added: []
  patterns:
    - "Linked mobile rows are a separate typed block with their own class family, never reusing agenda-quest-entry or agenda-event-entry"
    - "Row body is a Razor templated delegate shared by the member and non-member branches so the two cannot drift apart"

key-files:
  created:
    - QuestBoard.IntegrationTests/Tests/LinkedBoardMobileCalendarTests.cs
  modified:
    - QuestBoard.Service/Views/Calendar/Index.Mobile.cshtml
    - QuestBoard.Service/wwwroot/css/calendar.mobile.css

key-decisions:
  - "Member rows keep the own-row onclick plus inner a.row-nav-link pattern (keeps the focusable link RowNavigationAccessibilityTests expects); non-member rows have neither, so no link can 404"
  - "Onclick is emitted in separate if/else branches rather than a Razor attribute expression, so the apostrophes around the URL are not entity-encoded and the markup matches the own rows"

patterns-established:
  - "Mobile linked rows show the board name (and DM name for quests) as text on a second line"

requirements-completed: []

duration: 25min
completed: 2026-10-16
actuals:
  tokens: 9500
  tasks: 2
  commits: 3
---

# Phase 91 Plan 08: Linked boards on the phone agenda Summary

**The /calendar phone agenda now shows the other boards in the active board's linked set after the board's own entries, including days only a linked board uses, with an "Also showing: ..." line under the month bar; rows link only for members and cancelled linked events are struck through with spoken text.**

## Accomplishments

- `agendaDays` includes `d.Linked.HasEntries`, so a linked-only day gets its own `agenda-day-section` with the standard day label; a month with no entries keeps "Nothing This Month".
- `div.agenda-linked-note` (fa-link, comma-separated board names, no set name) renders directly under the month bar only when the board has linked boards.
- Within a day the order is own events, own quests, linked events, linked quests.
- Linked rows: line 1 title (plus calendar-day icon for events) and time; line 2 board name, plus DM name for quests. Long text wraps (`overflow-wrap: anywhere`).
- Member rows carry `--open`, a row onclick to the ordinary Details URL and an inner `a.row-nav-link`; everyone else (including a SuperAdmin with no membership) gets `--static` with no onclick, anchor or tab stop. URLs come from `Url.Action` with an integer id only.
- Cancelled linked events get `--cancelled` plus a visually-hidden " Cancelled."; quests carry visually-hidden "Proposed." or "Finalized.".
- DM-only sessions the viewer may not see produce no row and no day section (service already removes them; proven end to end on mobile).
- `calendar.mobile.css` gains the commented linked block with no media query.

## Task Commits

1. Task 1 (tracer): linked rows, linked-only days, Also showing line, base CSS and the first facts - `4f3be29b`
2. Task 2 RED: failing member, non-member, cancelled, status facts - `8016a678`
3. Task 2 GREEN: member/static rows, cancelled and status text, state CSS - `5a73cfef`

## Verification

- Tracer gate (autonomous form): after the Task 1 commit, `LinkedBoardMobileCalendarTests` (7), `CalendarBoardTypeScopeTests` and `MobileViewsTests` passed (65 total), then execution continued.
- Task 2 RED: 5 of 14 facts failed (member navigation, non-member plain row, SuperAdmin plain row, cancelled text, status text); the DM-only facts passed by construction because the service already filters.
- Final run of `LinkedBoardMobileCalendarTests`, `RowNavigationAccessibilityTests`, `CalendarBoardTypeScopeTests`, `MobileViewsTests`: 78 passed, 0 failed.
- Acceptance greps: `@media` count in calendar.mobile.css is 0 (unchanged); `Html.Raw` count in Index.Mobile.cshtml is 0 (unchanged from the base commit).
- Not proven by InMemory: SQL translation of the linked reads (covered upstream by the local SQL Server checkpoint in the final plan). Visual rendering on a real phone (one linked board with no separator, four or more wrapping) is the backstop truth and was not browser-verified here.

## Deviations from Plan

None - plan executed exactly as written.

## Known Stubs

None.

## Threat Flags

None beyond the plan's threat model (T-91-28, T-91-29, T-91-30 mitigated as planned: Razor encoding, integer-id onclick, no link or handler for non-members).

## Self-Check: PASSED

- LinkedBoardMobileCalendarTests.cs, Index.Mobile.cshtml and calendar.mobile.css exist; commits `4f3be29b`, `8016a678`, `5a73cfef` are in git log.
