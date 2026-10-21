---
phase: 93-per-board-page-selection-and-home-page
plan: 10
subsystem: board-pages
tags: [board-pages, quests, ef-core, razor, xunit, integration-tests]
requires:
  - phase: 93-01
    provides: BoardPagePolicy, BoardPageAvailability, page gate
  - phase: 93-04
    provides: BoardPageSettingsService and the count-query pattern
  - phase: 93-08
    provides: CalendarController reads IBoardPageResolver
  - phase: 93-14
    provides: BoardPagePolicyTests, left IsSwitchable(Quests) unasserted
provides:
  - The quest list is switchable like every other page
  - IQuestRepository.CountLiveForBoardAsync and the live-quest refusal in the settings service
  - A board's own calendar loads no quests while the quest list is off
  - DM profile quest history as plain text on both layouts while the quest list is off
  - BoardPageQuestsOffTests, an end-to-end proof of a board with quests off
affects: [93-11, 93-12]
actuals:
  tokens: 12000
  tasks: 2
  commits: 4
tech-stack:
  added: []
  patterns:
    - "Cross-board scalar count behind IgnoreQueryFilters with an explicit group id and the board date passed in"
    - "SQL-translatable predicate pinned to the shared in-memory rule by a parity test at the day boundary"
key-files:
  created:
    - QuestBoard.IntegrationTests/Controllers/BoardPageQuestsOffTests.cs
  modified:
    - QuestBoard.Domain/Extensions/BoardPagePolicy.cs
    - QuestBoard.Domain/Interfaces/IQuestRepository.cs
    - QuestBoard.Domain/Services/BoardPageSettingsService.cs
    - QuestBoard.Repository/QuestRepository.cs
    - QuestBoard.Service/Controllers/QuestBoard/CalendarController.cs
    - QuestBoard.Service/Views/DungeonMaster/Profile.cshtml
    - QuestBoard.Service/Views/DungeonMaster/Profile.Mobile.cshtml
    - QuestBoard.Service/wwwroot/css/dm-profile.mobile.css
    - QuestBoard.UnitTests/Services/BoardPagePolicyTests.cs
    - QuestBoard.UnitTests/Services/BoardPageSettingsServiceTests.cs
    - QuestBoard.UnitTests/Repository/BoardPageCountQueryTests.cs
    - QuestBoard.IntegrationTests/Controllers/BoardPagePlatformTests.cs
key-decisions:
  - "IsSwitchable returns true for every page; the Platform row, the settings switch and every other consumer follow with no further change"
  - "A quest is live while it is not closed and either not finalized or finalized for today or later; an open quest whose dates all passed counts"
  - "The live-quest count only runs on the Quests on-to-off transition, so an unrelated save on a board that already has quests off never queries"
  - "The guarded feed-file check stays in the verify command rather than a test that shells out to git"
requirements-completed: [D-03, D-04, D-05, D-07, D-08, D-09, D-13, D-19, D-20, D-21]
plan_head_before: 49c26a8d036c62a8eb12dfd6f2872e866d6ced48
commits: 4
status: complete
duration: 12 min
completed: 2026-10-21
---

# Phase 93 Plan 10: Quests Off Summary

**The quest list becomes switchable, refused while any quest is live (not closed, not yet played) or while it is the home page, and a board with quests off loads no quests on its calendar and links none in DM quest history.**

## Performance

- **Duration:** 12 min
- **Started:** 2026-10-21T13:02:00Z
- **Completed:** 2026-10-21T13:14:00Z
- **Tasks:** 2 (each as RED then GREEN)
- **Files modified:** 13 (1 created)

## Accomplishments

- `BoardPagePolicy.IsSwitchable` is true for every page. That one change put the "Quest list" row on both Platform Edit twins and made the settings service apply a Quests switch.
- `IQuestRepository.CountLiveForBoardAsync(groupId, boardToday)` counts `!IsClosed && (!IsFinalized || FinalizedDate == null || FinalizedDate >= start of boardToday)` behind `IgnoreQueryFilters()` with the explicit group id, reading no clock. A parity test maps the seeded entities and checks the count against `!IsClosed && !HasBeenPlayed(today)` at the 23:00 / 00:00 boundary.
- `BoardPageSettingsService` refuses Quests on-to-off with "Quests can't be switched off while this board has 1 live quest. Close or delete them first." (singular and plural). The existing home refusal covers "Quest list is this board's home page; pick another home page first." and the existing Quest Log step keeps its stored bit while Quests is off.
- `CalendarController.Index` loads no quests when the board does not serve the quest list (`includeQuests = type != Campaign && pages.Serves(Quests)`); the `pages` read moved above that line.
- Both DM profile twins render quest history titles as plain text with the quest list off; the mobile row drops its `onclick` and anchor and gains a `dm-quest-history-item--static` modifier (no pointer, no pressed tint).
- `BoardPageQuestsOffTests` (40 cases) covers the Platform form, refusals, every quest route for every role, menus, the own calendar, DM profile, a linked board, switching back on, and the Quest Log bit surviving an off/on round trip.

## Task Commits

1. **Task 1 RED:** `bbdf72ab` (test) - failing policy, count and settings tests with compile stubs
2. **Task 1 GREEN:** `bbedd46a` (feat) - switchable quest list, live-quest count and refusal
3. **Task 2 RED:** `d7757d89` (test) - end-to-end quests-off tests; own-calendar and DM-history cases failed
4. **Task 2 GREEN:** `ad5ac85a` (feat) - calendar condition and both DM profile twins

**Plan metadata:** the docs commit following this file.

## Files Created/Modified

- `QuestBoard.Domain/Extensions/BoardPagePolicy.cs` - every page switchable
- `QuestBoard.Domain/Interfaces/IQuestRepository.cs` - `CountLiveForBoardAsync`
- `QuestBoard.Repository/QuestRepository.cs` - the live count
- `QuestBoard.Domain/Services/BoardPageSettingsService.cs` - `IQuestRepository` dependency, live-quest refusal
- `QuestBoard.Service/Controllers/QuestBoard/CalendarController.cs` - quests condition
- `QuestBoard.Service/Views/DungeonMaster/Profile.cshtml`, `Profile.Mobile.cshtml`, `wwwroot/css/dm-profile.mobile.css` - plain-text history
- `QuestBoard.UnitTests/...` - policy, count query and settings service facts
- `QuestBoard.IntegrationTests/Controllers/BoardPageQuestsOffTests.cs` - end-to-end proof

## TDD Gate Compliance

Both tasks followed RED then GREEN with separate commits. Task 1's RED needed compile stubs (interface member, a repository method returning 0, the new constructor parameter) so the tests could run; they then failed on assertions (12 failures). Task 2's RED failed on exactly the two behaviours the code change owns (own calendar shows the quest, DM history links it) on both user agents, 4 failures; the remaining facts already passed because earlier plans route every other surface through the policy, and Task 1's flip made the Platform row appear. No REFACTOR commit was needed.

## Decisions Made

- The one-line policy flip carries the Platform row, the settings switch and the gate coverage; this plan adds only the quest-specific pieces (recorded choice 1).
- The repository predicate is written once for SQL and pinned to `HasBeenPlayed` by a parity test rather than shared as an expression (recorded choice 2).
- The guarded-feed-files check is left to the verify command: it needs the `c35512f3` base commit and a git process, which a unit-style integration test should not depend on.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Platform tests assumed eight switch rows**
- **Found during:** Task 2 (running the existing Platform form tests after the policy flip)
- **Issue:** `EditForm_OneShotBoard_ShowsAPagesSectionWithEverySwitchAndAHomeSelect` and `EditForm_RenderedFieldNames_RoundTripThroughAPost` counted 8 rows; the Quest list row makes it 9.
- **Fix:** Counts updated to 9 and "Quest list" added to the expected labels.
- **Files modified:** `QuestBoard.IntegrationTests/Controllers/BoardPagePlatformTests.cs`
- **Committed in:** `d7757d89`

**2. [Rule 1 - Bug] Wrong expected live count in my own first test draft**
- **Found during:** Task 1 GREEN
- **Issue:** The boundary seed has four live quests on board 1 (open, open with past dates, finalized at midnight today, finalized in the future), and the test said three.
- **Fix:** Expected value corrected to 4; the parity test confirmed the repository was right.
- **Files modified:** `QuestBoard.UnitTests/Repository/BoardPageCountQueryTests.cs`
- **Committed in:** `bbedd46a`

---

**Total deviations:** 2 auto-fixed (2 Rule 1). **Impact on plan:** none on behaviour; both were test-side counts.

## Issues Encountered

None. Full `dotnet test` is green: 1297 unit tests, 1571 integration tests (11 skipped, pre-existing). The three guarded feed files show no change since `c35512f3`, and the added lines carry no planning references.

## Known Stubs

None.

## Threat Flags

None. The live count is a scalar scoped by an explicit group id inside `QuestRepository.cs`, already on the cross-board allowlist, so the seam lists are unchanged.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

Every page, the quest list included, is now switchable. The invariant tests from the policy plan cover masks with Quests off, and the gate coverage tests stay green. Plan 11's static-guard and end-to-end tests can assume Quests is switchable.

## Self-Check: PASSED

---
*Phase: 93-per-board-page-selection-and-home-page*
*Completed: 2026-10-21*
