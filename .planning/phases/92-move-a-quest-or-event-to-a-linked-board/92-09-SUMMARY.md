---
phase: 92-move-a-quest-or-event-to-a-linked-board
plan: 09
subsystem: board-move
tags: [move, guards, architecture-tests, end-to-end, calendar-feed, audit-log]
requires:
  - phase: 92-05
    provides: QuestMoveService and QuestMoveRepository
  - phase: 92-06
    provides: EventMoveService and EventMoveRepository
  - phase: 92-07
    provides: Move views and CSS
  - phase: 92-08
    provides: Move control and the Moved toast
provides:
  - Nine new files under the ambient-clock guard and three new board-clock consumers
  - MoveEligibility_TakesTheBoardLocalDateRatherThanResolvingOne
  - BoardMoveRepositorySingleSaveTests
  - BoardMoveStaticGuardTests
  - BoardMoveEndToEndTests
affects: []
tech-stack:
  added: []
  patterns:
    - "Source-scan guards pin a rule that behavioural tests cannot see: one save per move, no filter bypass, no planning reference in the feature's files"
    - "End-to-end walks read the confirm form's own hidden fields rather than recomputing a stamp"
key-files:
  created:
    - QuestBoard.UnitTests/Architecture/BoardMoveRepositorySingleSaveTests.cs
    - QuestBoard.IntegrationTests/Tests/BoardMoveStaticGuardTests.cs
    - QuestBoard.IntegrationTests/Tests/BoardMoveEndToEndTests.cs
  modified:
    - QuestBoard.UnitTests/Architecture/AmbientClockSeamTests.cs
key-decisions:
  - "The feed fact seeds the session a few days ahead of today rather than on a fixed 2031 date, because the calendar feed only carries entries inside its window; the walks and audit facts use the same date for one shared scenario"
  - "The audit PII fact scans every captured record that mentions Move in its text or category, not just the audit line, so the controller's and the mail path's records are covered too"
patterns-established:
  - "A new guard test file for a feature rather than growing the previous feature's guard"
status: complete
actuals:
  tokens: 25000
  tasks: 2
  commits: 2
---

# Phase 92 Plan 09: Guards and end-to-end proof Summary

The board move is now locked in: a later edit cannot add a second save, an ambient clock read, a Move route in the deep-link registry or a planning reference to the move code, and the whole flow is proven over HTTP on desktop and mobile with the full suite green.

## What was built

**Task 1 - guards** (commit bcc5cdbb)
- `AmbientClockSeamTests`: nine paths added to the guarded list (MoveEligibility, EventMoveService, MoveControlService, QuestMoveService, EventMoveRepository, QuestMoveRepository, MoveController, both Move views), the three move services added as board-clock consumers, and a new fact pinning `MoveEligibility` to take `DateOnly boardToday` and never mention `IBoardClock`. No existing entry or exemption changed; every added file passed without touching production code.
- `BoardMoveRepositorySingleSaveTests`: both move repositories contain exactly one `SaveChangesAsync(` and no `IgnoreQueryFilters`; the three move services contain no save at all; `CrossBoardLinkRegistry.cs` does not mention `MoveController`.
- `BoardMoveStaticGuardTests`: a closed list of the feature's source files (51 paths, missing ones skipped), scanned per file for a decision id, a phase 92 reference, a plan id and a planning document name. Every literal is built from fragments so the test file does not hold them whole. Nothing in the existing source tripped it.

**Task 2 - end to end** (commit 1aa04216), `BoardMoveEndToEndTests`
- Quest walk (desktop and mobile theory): Details, control href `/Move/Quest/{id}`, Move page and preview, confirm with the page's own id, target and stamp, redirect to `/quests`, "Moved" toast with " 1 signup was removed." and "Open it on Board Bravo" linking `/Quest/Details/{id}`, then the quest renders on the target board. The mobile case also asserts the mobile Move page is the one served.
- Event walk: Events Details, Move page, confirm, redirect to the Events list, toast link `/Events/Details/{id}`, event renders on the target board.
- Audit facts: exactly one record "Moved quest {id} from board 1 to board 2 by user {actor}" carrying "kept 2, dropped 1, promoted 1, links cut 0"; the event twin carries "kept 1, dropped 1, added 0". No Move-related record contains a seeded name or email address.
- Feed fact: before the move the kept member's and the dropped player's feeds carry the quest UID at `SEQUENCE:1` under the source board's name; after it the kept member's feed carries the same UID at `SEQUENCE:2` with `[Board Bravo]` in the SUMMARY and no trace of the old board name, and the dropped player's feed no longer carries the UID. No feed source file was touched.

Full suite: 1084 unit tests and 1286 integration tests pass (11 pre-existing skips), including every existing calendar feed test.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug in plan assumption] Source board is named "EuphoriaInn", not "Board Alpha"**
- **Found during:** Task 2 (first run of the feed fact)
- **Issue:** The shared harness seeds board 1 as "EuphoriaInn"; the plan's "Board Alpha" is only the label other test classes use for it and never appears in a rendered feed entry.
- **Fix:** The feed assertions read `[EuphoriaInn]` before the move and assert it is gone afterwards. The scenario helper does not rename board 1, since other suites share the harness.
- **Files modified:** QuestBoard.IntegrationTests/Tests/BoardMoveEndToEndTests.cs
- **Commit:** 1aa04216

**2. [Rule 1 - Plan assumption] Session date is relative to today, not 2031-03-10**
- **Found during:** Task 2 (design)
- **Issue:** The calendar feed only carries entries inside a months-back and months-ahead window, so a 2031 quest could never appear in a feed and the plan's feed fact could not hold.
- **Fix:** The shared scenario dates the session five days ahead of today. Eligibility is unaffected because the quest stays in the future.
- **Commit:** 1aa04216

Otherwise the plan was executed as written. Both K1 and K2 (and the dropped player D1) are also given a membership on the source board in the scenario, because the feed only shows a reader quests on boards they belong to.

## Known Stubs

None.

## Threat Flags

None. Tests only; no production surface was added.

## Self-Check: PASSED

- FOUND: QuestBoard.UnitTests/Architecture/BoardMoveRepositorySingleSaveTests.cs
- FOUND: QuestBoard.IntegrationTests/Tests/BoardMoveStaticGuardTests.cs
- FOUND: QuestBoard.IntegrationTests/Tests/BoardMoveEndToEndTests.cs
- FOUND: commit bcc5cdbb (guards), commit 1aa04216 (end to end)
