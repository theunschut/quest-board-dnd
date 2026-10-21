---
phase: 93-per-board-page-selection-and-home-page
plan: 15
subsystem: ui
tags: [razor, ef-core, platform, board-list, badges]

requires:
  - phase: 93-per-board-page-selection-and-home-page
    provides: GroupEntity.HomePage column, BoardPage enum, BoardPagePolicy.Label, BoardPageSeed
provides:
  - GroupWithMemberCount.HomePage filled by both GroupRepository projections
  - "Home: {Label}" badge on both Platform board-list twins
  - BoardPageBoardListTests proving the badge on desktop and mobile user agents
affects: [93-06 platform edit form, board picker readers of GroupWithMemberCount]

actuals:
  tokens: 6500
  tasks: 2
  commits: 4
plan_head_before: ea940b94de16c837b9aa72134023388d4f4b72c2

tech-stack:
  added: []
  patterns:
    - "Board-list row located per layout by splitting on <tr> / group-card-mobile and matching board name + Open Board"

key-files:
  created:
    - QuestBoard.IntegrationTests/Controllers/BoardPageBoardListTests.cs
  modified:
    - QuestBoard.Domain/Models/GroupWithMemberCount.cs
    - QuestBoard.Repository/GroupRepository.cs
    - QuestBoard.UnitTests/Repository/GroupRepositoryTests.cs
    - QuestBoard.Service/Areas/Platform/Views/Group/Index.cshtml
    - QuestBoard.Service/Areas/Platform/Views/Group/Index.Mobile.cshtml

key-decisions:
  - "Badge reads the stored home, not the effective home, so it shows what the super admin chose"

patterns-established:
  - "Both GroupRepository projections fill every GroupWithMemberCount field so no reader silently sees a default"

requirements-completed: [D-16, D-18]

coverage:
  - id: D1
    description: "GetAllWithMemberCountAsync and GetGroupsForUserAsync return each board's stored home page"
    requirement: "D-16"
    verification:
      - kind: unit
        ref: "QuestBoard.UnitTests/Repository/GroupRepositoryTests.cs#GetAllWithMemberCountAsync_BoardWithStoredHome_ReturnsThatHomeAndDefaultsToQuests"
        status: pass
      - kind: unit
        ref: "QuestBoard.UnitTests/Repository/GroupRepositoryTests.cs#GetGroupsForUserAsync_BoardWithStoredHome_ReturnsThatHomeAndDefaultsToQuests"
        status: pass
    human_judgment: false
  - id: D2
    description: "Platform board list shows 'Home: Calendar' on the configured board's row on desktop and mobile user agents"
    requirement: "D-16"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/BoardPageBoardListTests.cs#BoardList_BoardWithStoredHome_ShowsHomeBadgeOnItsRow"
        status: pass
    human_judgment: false
  - id: D3
    description: "A board on the default home renders its row with no 'Home:' text on either layout"
    requirement: "D-18"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/BoardPageBoardListTests.cs#BoardList_BoardOnDefaultHome_ShowsNoHomeBadgeOnItsRow"
        status: pass
    human_judgment: false

duration: 15min
completed: 2026-10-21
status: complete
---

# Phase 93 Plan 15: Board list Home badge Summary

**Platform board list marks every board whose stored home is not the quest list with a "Home: {Label}" badge on both layouts, fed by a HomePage field now filled in both GroupRepository projections**

## Performance

- **Duration:** ~15 min
- **Tasks:** 2
- **Files modified:** 6 (1 created)

## Accomplishments
- `GroupWithMemberCount.HomePage` added and projected by `GetAllWithMemberCountAsync` and `GetGroupsForUserAsync`.
- `Home: {Label}` badge (`badge bg-secondary ms-1` with a house icon) beside the board-type badge on `Index.cshtml` and `Index.Mobile.cshtml`, rendered only when the stored home is not `BoardPage.Quests`.
- Boards on the default home render exactly as before.

## Task Commits

1. **Task 1: board list reads each board's stored home** - `63094577` (test, RED), `08e20581` (feat, GREEN)
2. **Task 2: Home badge on both twins** - `0222ca5d` (test, RED), `def04f99` (feat, GREEN)

**Plan metadata:** committed separately (docs: complete plan)

## TDD Gate Compliance

Both tasks have a failing `test(93-15)` commit followed by a passing `feat(93-15)` commit. RED evidence: Task 1 failed on assertion "Expected HomePage to be Calendar but found Quests" for both projections; Task 2 failed on "to contain 'Home: Calendar'" for both user agents. The default-home facts were already green at RED, as expected, since they pin unchanged behaviour (D-18).

## Files Created/Modified
- `QuestBoard.Domain/Models/GroupWithMemberCount.cs` - `HomePage` property
- `QuestBoard.Repository/GroupRepository.cs` - `HomePage = (BoardPage)g.HomePage` in both projections; no clock read added
- `QuestBoard.UnitTests/Repository/GroupRepositoryTests.cs` - two facts, one per projection
- `QuestBoard.Service/Areas/Platform/Views/Group/Index.cshtml` / `Index.Mobile.cshtml` - badge plus `@using QuestBoard.Domain.Extensions`
- `QuestBoard.IntegrationTests/Controllers/BoardPageBoardListTests.cs` - badge theories over both user agents

## Decisions Made
- Badge shows the stored home rather than the effective home, as recorded in the plan.
- Test seeds board 1 with a Calendar home and board 2 (campaign test group) with the default, and scopes assertions to each board's own row.

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered
- Full `QuestBoard.UnitTests` run has one failure unrelated to this plan: `MigratorBackupAndApplyCliTests.StateChangingCommand_WhenServerIsUnreachable_ReturnsCannotConnectWithoutLeakingConnectionDetails(command: "apply")` returns exit code 1 instead of 4 (Migrator CLI, depends on SQL Server reachability on the host). Not touched by this plan; left as is. All `GroupRepository`, `AmbientClockSeam`, `BoardPageBoardList`, `GroupManagement`, `LinkedBoardsPlatform` and `MobileViews` tests pass.

## User Setup Required
None - no external service configuration required.

## Known Stubs
None.

## Threat Flags
None. The badge shows only a board's own home label on a page already restricted to super admins (T-93-46, accepted).

## Next Phase Readiness
The Edit form plan can rely on the board list already surfacing the stored home; no dependency on this plan's files beyond `GroupWithMemberCount.HomePage`.

## Self-Check: PASSED
- Files verified present; commits 63094577, 08e20581, 0222ca5d, def04f99 exist on the branch.

---
*Phase: 93-per-board-page-selection-and-home-page*
*Completed: 2026-10-21*
