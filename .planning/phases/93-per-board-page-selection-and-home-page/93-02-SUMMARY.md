---
phase: 93-per-board-page-selection-and-home-page
plan: 02
subsystem: ui
tags: [aspnet-mvc, resource-filter, board-settings, integration-tests]

requires:
  - phase: 93-01
    provides: BoardPageGateFilter with [BoardPage] and [BoardPageExempt], BoardPagePolicy, BoardPageSeed
provides:
  - "[BoardPage] on the Calendar, Events, Series, Quest and Quest Log controllers (class level)"
  - "[BoardPage] on the admin quest list and quest delete, the four Move actions, and Characters on UpdateSignupCharacter"
  - BoardPageGateMatrixTests, the HTTP proof of the gate per page, ready for extension by the later gating plan
affects: [93-07, 93-08, 93-09, 93-12]

actuals:
  tokens: 14000
  tasks: 2
  commits: 4
plan_head_before: ea940b94de16c837b9aa72134023388d4f4b72c2

tech-stack:
  added: []
  patterns:
    - "Class-level [BoardPage] for a page's controller, action-level [BoardPage] where a controller serves several pages"
    - "An action may carry several [BoardPage] attributes and the filter requires every one"

key-files:
  created:
    - QuestBoard.IntegrationTests/Controllers/BoardPageGateMatrixTests.cs
  modified:
    - QuestBoard.Service/Controllers/QuestBoard/CalendarController.cs
    - QuestBoard.Service/Controllers/Events/EventsController.cs
    - QuestBoard.Service/Controllers/Events/SeriesController.cs
    - QuestBoard.Service/Controllers/QuestBoard/QuestController.cs
    - QuestBoard.Service/Controllers/QuestBoard/QuestLogController.cs
    - QuestBoard.Service/Controllers/Admin/AdminController.cs
    - QuestBoard.Service/Controllers/MoveController.cs

key-decisions:
  - "No action body changed: the attributes alone give the refusal, so the redirect-after-save and character-refusal work stays with the later plans that own it"
  - "Move tests and the admin delete test go beyond the plan's listed behaviours so every attribute added here has an HTTP fact behind it"

patterns-established:
  - "BoardPageGateMatrixTests.AssertRefusedAsync: assert the redirect path, then follow it with the same client and assert the toast"
  - "Tests read signups and quests through a scope from the factory services, because a context from factory.Database has no active board and the query filters hide every row"

requirements-completed: [D-02, D-05, D-07, D-19]

coverage:
  - id: D1
    description: "With Calendar off, GET /Calendar redirects to the quest list with the calendar toast; with defaults it answers 200"
    requirement: "D-02"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/BoardPageGateMatrixTests.cs#Calendar_Off_RedirectsToQuestListWithToast"
        status: pass
    human_judgment: false
  - id: D2
    description: "With Events off, a DM's /Events and /Events/Create and /Series/Details/{id} redirect with the Events toast; with defaults a DM's /Events answers 200"
    requirement: "D-02"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/BoardPageGateMatrixTests.cs#Events_Off_RedirectsDungeonMasterToQuestListWithToast"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/BoardPageGateMatrixTests.cs#Series_Off_RedirectsToQuestListWithToast"
        status: pass
    human_judgment: false
  - id: D3
    description: "With Quests off and home stored on the quest list, /quests, the admin quest list and quest delete redirect to /Calendar with the quest list toast"
    requirement: "D-05"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/BoardPageGateMatrixTests.cs#QuestList_Off_RedirectsToEffectiveHomeWithToast"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/BoardPageGateMatrixTests.cs#AdminQuests_WhenQuestListOff_RedirectsAdminToEffectiveHome"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/BoardPageGateMatrixTests.cs#AdminDeleteQuest_WhenQuestListOff_IsRefusedAndTheQuestStays"
        status: pass
    human_judgment: false
  - id: D4
    description: "The Quest Log is refused whenever the quest list is off, whatever its own switch says, and on its own switch otherwise"
    requirement: "D-19"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/BoardPageGateMatrixTests.cs#QuestLog_WhenQuestListOff_RedirectsWhateverItsOwnSwitchSays"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/BoardPageGateMatrixTests.cs#QuestLog_Off_RedirectsToQuestListWithToast"
        status: pass
    human_judgment: false
  - id: D5
    description: "UpdateSignupCharacter needs both Quests and Characters: with Characters off it redirects home and the signup's character is unchanged; with defaults it still changes it"
    requirement: "D-07"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/BoardPageGateMatrixTests.cs#UpdateSignupCharacter_WhenCharactersOff_RedirectsHomeAndKeepsTheCharacter"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/BoardPageGateMatrixTests.cs#UpdateSignupCharacter_WithDefaultSettings_ChangesTheCharacter"
        status: pass
    human_judgment: false
  - id: D6
    description: "The quest move and event move pages are refused for a super admin when Quests or Events is off"
    requirement: "D-02"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/BoardPageGateMatrixTests.cs#Move_WhenItsPageIsOff_IsRefusedForSuperAdmin"
        status: pass
    human_judgment: false
  - id: D7
    description: "With default settings the quest list, Quest Log, Calendar and Events overview answer exactly as before"
    requirement: "D-02"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/BoardPageGateMatrixTests.cs#QuestPages_WithDefaultSettings_AreServed"
        status: pass
    human_judgment: false

duration: 20min
completed: 2026-10-21
status: complete
---

# Phase 93 Plan 02: Gate Calendar, Events, Series, Quest, Quest Log, admin quests and Move Summary

**Calendar, Events, Series, the quest list, Quest Log, the admin quest list and both move flows now refuse a typed or crafted request with a toast and a redirect to the board's home whenever their page is switched off, with `UpdateSignupCharacter` also requiring Characters.**

## Performance

- **Duration:** about 20 min
- **Started:** 2026-10-21T11:40:00Z
- **Completed:** 2026-10-21T12:02:00Z
- **Tasks:** 2
- **Files modified:** 8 (7 controllers, 1 new test file)

## Accomplishments
- `[BoardPage]` at class level on `CalendarController` (Calendar), `EventsController` and `SeriesController` (Events), `QuestController` (Quests) and `QuestLogController` (Quest Log).
- Action-level attributes where a controller serves more than one page: `AdminController.Quests` and `DeleteQuest` (Quests), `MoveController.Quest`/`ConfirmQuest` (Quests) and `Event`/`ConfirmEvent` (Events), and `QuestController.UpdateSignupCharacter` additionally Characters.
- `BoardPageGateMatrixTests` with 17 cases: every refusal asserts the redirect path and, by following it with the same client, the exact toast; every refused page has a positive control or default-settings counterpart.
- No action body changed.

## Task Commits

1. **Task 1 RED: failing gate tests for Calendar, Events and Series** - `606b4cbf` (test)
2. **Task 1 GREEN: Calendar, Events and Series gated** - `4d98495b` (feat)
3. **Task 2 RED: failing gate tests for quest pages, Quest Log, admin quests and moves** - `424d4fe4` (test)
4. **Task 2 GREEN: quest pages, Quest Log, admin quest list and moves gated** - `ae41bf7b` (feat)

**Plan metadata:** recorded in the docs commit that carries this file.

## Files Created/Modified
- `QuestBoard.IntegrationTests/Controllers/BoardPageGateMatrixTests.cs` - the matrix of gate facts (created here, extended by the later gating plan)
- `QuestBoard.Service/Controllers/QuestBoard/CalendarController.cs`, `Events/EventsController.cs`, `Events/SeriesController.cs`, `QuestBoard/QuestController.cs`, `QuestBoard/QuestLogController.cs`, `Admin/AdminController.cs`, `MoveController.cs` - the `[BoardPage]` attributes and the `QuestBoard.Service.Filters` using

## Decisions Made
Followed the plan's recorded choices. Added facts for the Move actions and admin delete beyond the plan's behaviour list so each attribute added here is proven over HTTP.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Test fidelity] Signup and quest reads go through a service scope**
- **Found during:** Task 2 RED run
- **Issue:** the first draft read the signup through `factory.Database.CreateContext()`, which has no active board, so the board query filter hid every row and the default-settings control threw "Sequence contains no elements".
- **Fix:** read through `factory.Services.CreateScope()` and `QuestBoardContext`, as the existing character tests do.
- **Files modified:** `QuestBoard.IntegrationTests/Controllers/BoardPageGateMatrixTests.cs`
- **Verification:** the control passes, the refusal fact asserts the unchanged character.
- **Committed in:** `424d4fe4`

---

**Total deviations:** 1 auto-fixed (1 Rule 1, test-only)
**Impact on plan:** none on scope or production code.

## TDD Gate Compliance

Both tasks followed RED then GREEN with `test(93-02)` commits before the matching `feat(93-02)` commits. Each RED run failed on assertions for the planned behaviour (a 200 instead of the 302, or the wrong landing path), with the positive controls passing; no refactor commit was needed.

## Issues Encountered
- Full `dotnet test` in the worktree: 1350 integration tests passed (11 skipped). Unit tests: 1106 passed and 1 failed, `MigratorBackupAndApplyCliTests.StateChangingCommand_WhenServerIsUnreachable...(command: "backup")`, the intermittent failure already logged by the previous plan on a clean checkout; unrelated to this plan.

## Known Stubs

None.

## Threat Flags

None. The attributes only add refusals; the redirect target still comes from `BoardPageRoutes`.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- Plans 07, 08 and 09 can now edit Quest, Calendar, Events, Series and Move controllers knowing the gate is in place.
- The later gating plan can extend `BoardPageGateMatrixTests` and add the closed-list coverage test once every attribute exists.

## Self-Check: PASSED

- `BoardPageGateMatrixTests.cs` exists on disk; all seven modified controllers carry the attributes (`git diff --stat` 13 + 6 insertions).
- Commits `606b4cbf`, `4d98495b`, `424d4fe4` and `ae41bf7b` exist; `git rev-list --count ea940b94..HEAD` was 4.
- Task verification commands re-run: 315 tests (Task 1 filter) and 371 tests (Task 2 filter) passed with no failures.
- No added source line carries a planning reference (grep count 0 before each commit).

---
*Phase: 93-per-board-page-selection-and-home-page*
*Completed: 2026-10-21*
