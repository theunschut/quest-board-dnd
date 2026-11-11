---
phase: 102-board-deletion
plan: 05
subsystem: platform-board-management
tags: [board-delete, ef-core, model-pin, sql-server, tests]
requires: [102-01]
provides:
  - Model-metadata pin of every foreign key the board delete depends on
  - Typed-name and status-mapping facts for GroupService.DeleteBoardAsync
  - SQL Server-gated real delete and rollback test in a scratch database
affects: [102-04, 102-10]
tech-stack:
  added: []
  patterns:
    - model-metadata assertions over context.Model for delete-rule drift
    - ScratchDatabase plus MigrateAsync plus DI-resolved internal repository
key-files:
  created:
    - QuestBoard.UnitTests/Repository/BoardOwnedForeignKeysTests.cs
    - QuestBoard.UnitTests/Services/GroupServiceBoardDeletionTests.cs
    - QuestBoard.IntegrationTests/Tests/BoardDeletionSqlServerTests.cs
  modified: []
decisions:
  - The seven non-cascade foreign keys among board-owned tables are pinned as "Dependent.Property -> Principal" strings so a failure names the exact key
  - The gated test reads the contact-tag join table by column name (ContactsId) via a parameterized SqlQuery, since it has no mapped CLR type
status: complete
actuals:
  tokens: 15000
  tasks: 2
  commits: 2
---

# Phase 102 Plan 05: Delete safety nets Summary

Three test files, no production change: a model pin that fails a unit test (not a production delete) when a new table references a board, the typed-name rules proven at the service, and a SQL Server-gated test that runs the real delete and a real rollback against the production migration schema.

## What was built

- `BoardOwnedForeignKeysTests` (4 facts, read `context.Model` only):
  - `EveryForeignKeyIntoGroups_IsHandledByTheBoardDelete`: exactly UserGroup Cascade plus Character, ContactCategory, Contact, ContactTag, Event, EventSeries, Quest, ShopItem all NoAction.
  - `NonCascadeForeignKeysAmongBoardOwnedTables_AreTheHandledSet`: PlayerSignup to Quest, ReminderLog to Quest, Quest.OriginalQuestId, PlayerSignup to Character, Event to EventSeries, UserTransaction to ShopItem, UserTransaction.OriginalTransactionId.
  - `NoTableOutsideTheBoardReachesIntoBoardOwnedRows`: principal set is the 18 board-owned types, `GroupEntity` deliberately excluded (its keys are owned by the first fact); the shared-type join `ContactEntityContactTagEntity` is the one allowed outsider and both its keys must be Cascade.
  - `ImageNoteClassSignupAndVoteForeignKeys_Cascade`: image, note, class, event signup, proposed date, date vote and tag-join keys all Cascade (comment explains why the delete never loads image blobs and why InMemory leaves orphan image rows).
- `GroupServiceBoardDeletionTests` (5 facts/theory cases, 9 test cases): NotFound, exact name, trimmed name, Theory over "iron table", "Iron  Table", "Iron Table.", "" and null giving NameMismatch with `DidNotReceive().DeleteBoardAsync`, and HasMembers passthrough.
- `BoardDeletionSqlServerTests` (2 facts): each starts with `MigratorSqlServer.RequireServerConnectionString()`, creates a `ScratchDatabase`, builds a provider with `AddRepositoryServices`, `AddAutoMapper(EntityProfile)` and a private fixed `IBoardClock`, runs `MigrateAsync`, seeds with database-generated ids (every board-owned kind on board A, a smaller graph on board B, the three cross-board reach-ins, a linked set holding both). Fact 1 deletes board A through `IGroupRepository` and checks zero rows per table for A (including CharacterImages, ContactImages and the `ContactContactTags` join rows), B unchanged, reach-ins kept with links cleared, linked set kept. Fact 2 installs an AFTER DELETE trigger on `Groups` that throws, expects the delete to throw, and checks both boards' per-table counts are unchanged.

## Verification

- `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~BoardOwnedForeignKeys|FullyQualifiedName~GroupServiceBoardDeletion"`: 13 passed, 0 failed.
- `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~BoardDeletionSqlServer"`: 2 skipped, 0 failed (no `QUESTBOARD_MIGRATOR_TEST_CONNECTION`; `QUESTBOARD_MIGRATOR_TEST_REQUIRED` not set). No package reference changed.

## Facts that depend on 102-04 or on a real server

- The model-pin facts and the service facts depend only on 102-01 and the current model; they pass now.
- `DeletingAFullyPopulatedBoard_SucceedsAndLeavesTheOtherBoardIntact` can only pass once 102-04's full delete is merged (current code removes only the quest family, so a populated board would hit a foreign-key error). It is written against the 102-04 contract: reach-ins unlinked (`OriginalQuestId`, `CharacterId`, `SeriesId` plus `SeriesSlotIndex` set to null), linked set and other board's `LinkedBoardSetId` kept.
- Both gated facts have only been compiled, never executed: SQL Server at localhost:1433 is down and the seed graph (identity users, unique indexes, filtered `SeriesId`/`SeriesSlotIndex` index, self-referencing quest and transaction chains) is unverified against a real server. Plan 102-10 runs them for real; adjust the seed there if a column or unique-index requirement was missed.
- In the rollback fact, SQL Server may reject the DELETE itself because the table has a trigger and EF uses an OUTPUT clause; either error fails the save at the Groups delete, after the earlier deletes ran, which is all the rollback check needs. A circular-dependency error from EF on the self-referencing chains would be the documented fallback case for 102-10.

## Deviations from Plan

None - plan executed exactly as written. The pinned behaviours were confirmed against `QuestBoardContext.cs` first (all non-membership Group foreign keys are NoAction).

## Known Stubs

None.

## Threat Flags

None. The gated test only creates and drops its own Guid-named scratch database and never reads the application connection string (T-102-15). The model pin covers T-102-16.

## Self-Check: PASSED

- Created files exist: BoardOwnedForeignKeysTests.cs, GroupServiceBoardDeletionTests.cs, BoardDeletionSqlServerTests.cs.
- Commits exist: 689ac9a2 (unit tests), 9638f096 (gated SQL Server test).
- No planning IDs in the test sources; STATE.md and ROADMAP.md untouched.
