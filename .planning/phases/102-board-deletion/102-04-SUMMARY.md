---
phase: 102-board-deletion
plan: 04
subsystem: platform-board-management
tags: [board-delete, ef-core, change-tracker, fk-integrity, platform]
requires: [102-01]
provides:
  - BoardContentKind (15 kinds, confirmation-page order)
  - BoardDeletionResult.RemovedCounts
  - Full GroupRepository.DeleteBoardAsync covering every board-owned table
  - Scoped helpers reusable by the preview (BoardEvents ... BoardTransactions, FollowUpsOnOtherBoards, OtherBoardSignupsUsingBoardCharacters, OtherBoardEventsInBoardSeries)
  - BoardDeletionTestGraph seed graph and model-driven FK-integrity sweep
affects: [102-05, 102-06]
tech-stack:
  added: []
  patterns:
    - explicit-id IgnoreQueryFilters helpers re-scoped by groupId
    - cross-board reach-ins cleared as tracked property writes, rows kept
    - FK sweep over the EF model reading columns through EF.Property (covers shadow FKs)
key-files:
  created:
    - QuestBoard.Domain/Enums/BoardContentKind.cs
    - QuestBoard.UnitTests/Repository/BoardDeletionTestGraph.cs
    - QuestBoard.UnitTests/Repository/GroupRepositoryBoardDeletionTests.cs
  modified:
    - QuestBoard.Domain/Models/BoardDeletion/BoardDeletionResult.cs
    - QuestBoard.Repository/GroupRepository.cs
decisions:
  - Image rows (character, contact, DM) and the contact-to-tag join rows are never loaded; the database cascade removes them
  - Character classes are removed explicitly but not counted as their own kind
  - The FK sweep reads columns with EF.Property so the shadow foreign key PlayerSignupEntity.UserEntityId is covered
status: complete
actuals:
  tokens: 28000
  tasks: 2
  commits: 2
---

# Phase 102 Plan 04: Full board delete and FK-integrity sweep Summary

A board delete now removes every board-owned table (quests and their dates, votes, signups and reminder logs; events, signups and series; characters and classes; contacts, notes, categories and tags; shop items and every chained transaction), unlinks the three cross-board reach-ins while keeping those rows, reports per-kind removed counts, and is proven on InMemory by a model-driven foreign-key sweep.

## What was built

- `BoardContentKind` (15 members, ending in `Transactions`) and `BoardDeletionResult.RemovedCounts` (empty for refused results).
- `GroupRepository`: scoped helpers for each remaining table and the three reach-in helpers, each `IgnoreQueryFilters()` immediately re-scoped by the explicit board id. `BoardTransactions` also takes rows chained through `OriginalTransactionId` to this board's shop items.
- `DeleteBoardAsync`: before staging removals, loads the reach-ins and writes `OriginalQuestId = null`, `CharacterId = null`, `SeriesId = null` and `SeriesSlotIndex = null` (tracked writes, rows stay). Then stages every remaining list with its count; no image navigation or image row is loaded. Still one `SaveChangesAsync` with the clear-and-rethrow catch.
- `BoardDeletionTestGraph`: `Store`, `CreateRepository`, `SeedBoardsAndUsersAsync`, `SeedQuestContentAsync`, `SeedFullBoardAsync`, `SeedSurvivorsAsync`, `SeedReachInsAsync`, `CountBoardRowsAsync`, `CountSurvivorsAsync`, `SnapshotBoardAsync` and `AssertNoDanglingNonCascadeForeignKeys`. The sweep skips shared-type and owned entities and Cascade/SetNull keys, and fails naming the dependent type, property, value and delete behaviour.
- `GroupRepositoryBoardDeletionTests`: 11 test methods (12 cases with the theory): removed counts, HasMembers refusal with unchanged counts, NotFound, exactly one save, failed save clears tracker and rethrows, sweep fires on a dangling reference, full removal, another board unchanged (active board = other and = none), survivors untouched, reach-ins unlinked and kept, linked board set kept.

## Verification

- `dotnet test QuestBoard.UnitTests --filter "GroupRepositoryBoardDeletion|Architecture"`: 104 passed.
- Wave gate: `dotnet test QuestBoard.UnitTests`: 1333 passed; `dotnet test QuestBoard.IntegrationTests`: 1816 passed, 11 skipped (SQL Server-gated, server is down).
- No migration added. No SQL Server used.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] FK sweep could not read a shadow foreign key**
- **Found during:** Task 1
- **Issue:** The planned approach reads key values through `IProperty.PropertyInfo`, but `PlayerSignupEntity.UserEntityId` (a shadow FK created by `UserEntity.Signups`) has no CLR property, so the sweep failed loudly on every fact.
- **Fix:** The sweep projects each column with `EF.Property<T>` through a generic loader, which covers mapped and shadow keys alike. Composite keys still fail loudly.
- **Files modified:** QuestBoard.UnitTests/Repository/BoardDeletionTestGraph.cs
- **Commit:** 3a84d612

**2. [Rule 1 - Bug] Test assertion compared a 15-kind dictionary to a 5-kind seed**
- **Issue:** The failed-save fact compared the full per-kind dictionary to the quest-only seed counts; it now checks the seeded kinds are all still present.
- **Commit:** 3a84d612

## Known Stubs

None.

## Self-Check: PASSED

- Files exist: BoardContentKind.cs, BoardDeletionTestGraph.cs, GroupRepositoryBoardDeletionTests.cs, 102-04-SUMMARY.md
- Commits exist: 3a84d612, 0d735a26
