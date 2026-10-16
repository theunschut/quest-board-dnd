---
phase: 91-linked-boards-with-a-shared-calendar
plan: 02
subsystem: calendar
tags: [aspnet-mvc, ef-core, authorization, calendar, dm-only]
status: complete

requires:
  - phase: 91-linked-boards-with-a-shared-calendar
    provides: none (wave 1, independent of plan 01)
provides:
  - "GetQuestsForCalendarAsync(bool isAdminOrDm, ...) applying the quest board's DM-only rule"
  - "/calendar and the Quest Details calendar hide DM-only sessions from players"
  - "CalendarDungeonMasterSessionTests own-board matrix"
affects: [91-04, 91-05, linked board reads, calendar overlay]

actuals:
  tokens: 9500
  tasks: 2
  commits: 2

tech-stack:
  added: []
  patterns:
    - "One board, one DM-only rule: !q.DungeonMasterSession || isAdminOrDm in every quest read"

key-files:
  created:
    - QuestBoard.IntegrationTests/Tests/CalendarDungeonMasterSessionTests.cs
  modified:
    - QuestBoard.Repository/QuestRepository.cs
    - QuestBoard.Domain/Interfaces/IQuestRepository.cs
    - QuestBoard.Domain/Interfaces/IQuestService.cs
    - QuestBoard.Domain/Services/QuestService.cs
    - QuestBoard.Service/Controllers/QuestBoard/CalendarController.cs
    - QuestBoard.Service/Controllers/QuestBoard/QuestController.cs

key-decisions:
  - "The DM tier comes from the controllers' existing IsDmTierAsync / GetEffectiveRoleAsync (SuperAdmin short-circuits to Admin, otherwise read fresh from UserGroups); no new role lookup"
  - "The Quest Details page always keeps its own quest on its calendar, so a DM-only quest opened by URL renders exactly as before (that route stays deferred)"
  - "Only the DM-only clause is mirrored on the calendar read; closed and past quests stay visible"

patterns-established:
  - "Calendar quest reads take the viewer's DM tier as a leading parameter; no parameterless overload exists"

requirements-completed: []

duration: 25min
completed: 2026-10-16
---

# Phase 91 Plan 02: DM-only sessions on the own-board calendars Summary

Players no longer see DM-only quests on `/calendar` (desktop and mobile) or on the Quest Details date-vote calendar. DungeonMaster, Admin and SuperAdmin still do. The rule is the quest board's own `!q.DungeonMasterSession || isAdminOrDm` clause, passed through repository, service and both callers.

## Accomplishments

- `GetQuestsForCalendarAsync(bool isAdminOrDm, CancellationToken)` replaces the parameterless method in `QuestRepository`, `IQuestRepository`, `IQuestService` and `QuestService`. XML summaries say DM-only sessions are included only when `isAdminOrDm` is true.
- `CalendarController.Index` computes the DM tier once and uses it for both the quest load and `CanManage`.
- `QuestController.Details` computes the viewer role once, passes the DM tier into the calendar read, and re-adds the page's own quest if the filter removed it.
- `CalendarDungeonMasterSessionTests` has 13 facts: Player hidden and DM, Admin and SuperAdmin visible on desktop and mobile `/calendar`, plus the Details facts below. Every fact asserts an ordinary quest's title is present so an error page cannot pass an absence check.

## Task Commits

1. Task 1 (tracer): hide DM-only sessions on the calendar - `30afd014`
2. Task 2: same rule on the Quest Details calendar, own quest kept - `b326e457`

Task 2 was test-first. The two own-quest facts (desktop `current-quest` chip, mobile `calendar-date-entry-mobile`) failed before the re-add and pass after it.

## Verification

- `CalendarDungeonMasterSessionTests`, `EventCalendarPartialTests`, `MobileViewsTests`: 69 passed.
- `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~Calendar|FullyQualifiedName~Quest"`: 965 passed, 11 skipped (pre-existing SQL Server migrator tests), 0 failed.
- `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~Architecture"`: 72 passed (includes `AmbientClockSeamTests`).
- `QuestBoard.Service/Views/Quest/` has no diff; no planning IDs in added source lines; no parameterless `GetQuestsForCalendarAsync` caller remains.

## Deviations from Plan

None - plan executed exactly as written. The tracer feedback gate ran in interactive mode, so execution paused after Task 1 for approval and resumed on the same branch.

## Known Stubs

None.

## Threat Flags

None. T-91-08 is mitigated by the query clause and the Player/DM/Admin/SuperAdmin matrix. T-91-10 is mitigated by the fresh role lookup. T-91-09 is accepted: only the page's own quest is re-added.

## Self-Check: PASSED

- FOUND: QuestBoard.IntegrationTests/Tests/CalendarDungeonMasterSessionTests.cs
- FOUND commits: 30afd014, b326e457
