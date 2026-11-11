---
phase: 102-board-deletion
plan: 06
subsystem: repository
tags: [board-deletion, preview, ef-core]
requires:
  - phase: 102-04
    provides: scoped BoardX(groupId) helpers, reach-in helpers, BoardDeletionTestGraph
provides:
  - GetDeletionPreviewAsync on IGroupRepository and IGroupService
  - BoardDeletionPreview, BoardDeletionItem, BoardUnlinkedFollowUp domain models
affects: [102-07]
tech-stack:
  added: []
  patterns: [preview projected from the same scoped queries as the delete]
key-files:
  created:
    - QuestBoard.Domain/Models/BoardDeletion/BoardDeletionPreview.cs
    - QuestBoard.Domain/Models/BoardDeletion/BoardDeletionItem.cs
    - QuestBoard.Domain/Models/BoardDeletion/BoardUnlinkedFollowUp.cs
    - QuestBoard.UnitTests/Repository/GroupRepositoryBoardDeletionPreviewTests.cs
  modified:
    - QuestBoard.Domain/Interfaces/IGroupRepository.cs
    - QuestBoard.Domain/Interfaces/IGroupService.cs
    - QuestBoard.Domain/Services/GroupService.cs
    - QuestBoard.Repository/GroupRepository.cs
key-decisions:
  - "Names are sorted in memory with StringComparer.OrdinalIgnoreCase so order is independent of database collation"
requirements-completed: [D-05, D-07]
status: complete
actuals:
  tokens: 14000
  tasks: 1
  commits: 1
---

# Phase 102 Plan 06: Deletion Preview Summary

**Board deletion preview built from the delete's own scoped helpers, so per-kind counts equal what the delete removes, with case-insensitively sorted names for named kinds and a separate report of rows on other boards that would only be unlinked.**

## Accomplishments
- `GroupRepository.GetDeletionPreviewAsync` returns all 15 kinds in enum order (empty kinds present with Count 0). Count-only kinds use `CountAsync`; named kinds project only the name column.
- Reach-ins reported separately: follow-up quests (title plus their own board's name, sorted by title), signups losing a character, events leaving a series.
- Service is a one-line passthrough; no new `IgnoreQueryFilters` sites, no saves, no image tables touched.
- 6 facts (7 cases) in `GroupRepositoryBoardDeletionPreviewTests`: parity with the delete, names only for named kinds and sorted with duplicates kept, empty kinds, reach-ins, active-board independence (other board, null), nothing saved.

## Task Commits
1. Task 1: Deletion preview - `8e7fc9e3`

## Verification
- Plan verify filter: 116 passed. Full unit suite: 1353 passed. Integration suite: 1816 passed, 13 skipped (SQL Server gated).

## Deviations from Plan
None - plan executed exactly as written.

## Known Stubs
None.

## Threat Flags
None.

## Self-Check: PASSED
