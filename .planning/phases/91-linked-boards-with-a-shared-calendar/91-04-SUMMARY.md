---
phase: 91-linked-boards-with-a-shared-calendar
plan: 04
subsystem: repository
tags: [ef-core, linked-boards, calendar, filter-bypass, read-model]
status: complete

requires:
  - phase: 91-linked-boards-with-a-shared-calendar
    provides: "91-01 linked set storage and ILinkedBoardSetRepository; 91-02 own-calendar DM-only clause"
provides:
  - "ILinkedBoardSetRepository.GetLinkedBoardsForViewerAsync: the active board's other set boards with the viewer's role on each"
  - "IEventRepository.GetLinkedEventsForCalendarAsync: windowed, id-pinned, flat event projection (cancelled kept and flagged)"
  - "IQuestRepository.GetLinkedQuestsForCalendarAsync: windowed, id-pinned, flat quest projection with in-window proposed dates"
  - "Domain models LinkedBoard, ViewerLinkedBoards, LinkedQuestEntry, LinkedEventEntry, LinkedCalendarData"
affects: [91-05, 91-08, 91-09, 91-10]

tech-stack:
  added: []
  patterns:
    - "One filter bypass per linked read, re-narrowed at once by caller-supplied ids and short-circuited on an empty set"
    - "Flat Select projection into dedicated read models, never ProjectForCalendar or Include"

key-files:
  created:
    - QuestBoard.Domain/Models/LinkedBoard.cs
    - QuestBoard.Domain/Models/ViewerLinkedBoards.cs
    - QuestBoard.Domain/Models/LinkedQuestEntry.cs
    - QuestBoard.Domain/Models/LinkedEventEntry.cs
    - QuestBoard.Domain/Models/LinkedCalendarData.cs
    - QuestBoard.UnitTests/Repository/LinkedCalendarReadTests.cs
  modified:
    - QuestBoard.Domain/Interfaces/ILinkedBoardSetRepository.cs
    - QuestBoard.Domain/Interfaces/IQuestRepository.cs
    - QuestBoard.Domain/Interfaces/IEventRepository.cs
    - QuestBoard.Repository/LinkedBoardSetRepository.cs
    - QuestBoard.Repository/QuestRepository.cs
    - QuestBoard.Repository/EventRepository.cs

key-decisions:
  - "The viewer's role per linked board is one projection over Groups with a correlated UserGroups lookup; Groups, UserGroups and LinkedBoardSets carry no query filter, so LinkedBoardSetRepository needs no bypass"
  - "Both linked reads live in the already allowlisted QuestRepository.cs and EventRepository.cs, so the cross-board seam test needed no edit"
  - "Quests are windowed by a single 'has a proposed date in [from, to)' predicate, which covers both open and finalized placement"
  - "BoardName and ViewerIsMember on the entry models are left for the overlay service to fill from the lookup; the projections never join Groups"

patterns-established:
  - "Fail-closed lookup: a missing set, or a board or set whose BoardType differs, yields no linked boards"

requirements-completed: []

duration: 20min
completed: 2026-10-16
actuals:
  tokens: 14000
  tasks: 2
  commits: 3
---

# Phase 91 Plan 04: Linked calendar data reads Summary

**A fresh per-viewer lookup of the other boards in the active board's set, plus one id-pinned, month-windowed, read-only projection each for their events and quests.**

## Accomplishments

- `GetLinkedBoardsForViewerAsync` returns the set's other boards ordered by name with the viewer's `GroupRole` (null for a non-member), and nothing for an unlinked board, a set of one, or any board-type mismatch (active board or linked board). No SuperAdmin branch.
- `GetLinkedEventsForCalendarAsync` returns `LinkedEventEntry` rows for `Date in [from, to)` with `IsCancelled = CancelledAt != null` and no cancellation predicate.
- `GetLinkedQuestsForCalendarAsync` returns flat `LinkedQuestEntry` rows (title, DM name, DM-only flag, finalized state and date, in-window proposed dates); no `Include`, no `ProjectForCalendar`, nothing about signups, votes or players.
- Both reads short-circuit an empty id set, use `AsNoTracking`, and write nothing, so no feed revision moves.
- `LinkedCalendarReadTests` has 16 facts, including one proving B's quest carries non-empty proposed dates while the context is active on A.

## Task Commits

1. Task 1 (tracer): linked board lookup and pinned event read - `bc8dcc19`
2. Task 2 RED: failing quest read facts - `02c417e5`
3. Task 2 GREEN: windowed linked quest projection - `55d08593`

## Verification

- `dotnet test QuestBoard.UnitTests --filter "LinkedCalendarReadTests|LinkedBoardSetRepositoryTests|Architecture"`: 99 passed (includes `CrossBoardIgnoreQueryFiltersSeamTests`, `FeedRevisionWriteSeamTests`, `AmbientClockSeamTests`).
- `dotnet test QuestBoard.IntegrationTests --filter "CalendarDungeonMasterSessionTests|CalendarBoardTypeScopeTests|TenantIsolation"`: 61 passed.
- No diff under `QuestBoard.UnitTests/Architecture/`; each of `QuestRepository.cs` and `EventRepository.cs` gained exactly one `IgnoreQueryFilters` code call; `LinkedBoardSetRepository.cs` has none.
- Not proven by InMemory: SQL translation of the `Any` window predicate and the `ToList()` inside the quest projection. This is the local SQL Server check already planned for the final plan; the recorded fallback is to drop the date predicates.

## Deviations from Plan

### Auto-fixed Issues

None - plan executed exactly as written.

### Process

- The Task 2 RED commit carried a `NotImplementedException` stub for the quest read so the facts compile and fail at runtime (same approach as plan 01). All six quest facts failed at RED; the 10 tracer facts stayed green.
- The tracer gate ran in its autonomous form: the tracer verify (93 tests) passed after its commit, then execution continued.

## Known Stubs

None. `LinkedCalendarData` is defined but not yet consumed; the overlay service in plan 05 consumes it. `BoardName` and `ViewerIsMember` on the entry models are intentionally unset by these reads and are filled by that service.

## Threat Flags

None beyond the plan's threat model. The two bypass reads are the new cross-board surface and are mitigated by id pinning, the empty-set short-circuit and the narrow projection (T-91-11, T-91-12, T-91-16, T-91-18, T-91-19).

## Self-Check: PASSED

- All six created files exist; commits `bc8dcc19`, `02c417e5`, `55d08593` are in git log.
