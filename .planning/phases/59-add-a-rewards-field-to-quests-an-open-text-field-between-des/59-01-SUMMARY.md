---
phase: 59-add-a-rewards-field-to-quests-an-open-text-field-between-des
plan: 01
subsystem: quests-backend
tags: [ef-core, migration, quest-service, quest-repository, unit-tests]
status: complete
dependency-graph:
  requires: []
  provides:
    - "QuestEntity.Rewards"
    - "Quest.Rewards"
    - "QuestViewModel.Rewards"
    - "FollowUpQuestViewModel.Rewards"
    - "AddRewardsToQuest EF Core migration"
    - "rewards parameter on UpdateQuestPropertiesWithNotificationsAsync / CreateFollowUpQuestWithDetailsAsync"
  affects:
    - "QuestBoard.Service/Views/Quest/*.cshtml (Plan 59-02 view wiring)"
tech-stack:
  added: []
  patterns:
    - "Nullable, unattributed string property mirroring Recap's shape (no [Required]/[StringLength])"
    - "Explicit-parameter service/repository method threading (not AutoMapper passthrough)"
key-files:
  created:
    - QuestBoard.Repository/Migrations/20260706194635_AddRewardsToQuest.cs
    - QuestBoard.Repository/Migrations/20260706194635_AddRewardsToQuest.Designer.cs
  modified:
    - QuestBoard.Repository/Entities/QuestEntity.cs
    - QuestBoard.Domain/Models/QuestBoard/Quest.cs
    - QuestBoard.Service/ViewModels/QuestViewModels/QuestViewModel.cs
    - QuestBoard.Service/ViewModels/QuestViewModels/FollowUpQuestViewModel.cs
    - QuestBoard.Domain/Interfaces/IQuestService.cs
    - QuestBoard.Domain/Interfaces/IQuestRepository.cs
    - QuestBoard.Domain/Services/QuestService.cs
    - QuestBoard.Repository/QuestRepository.cs
    - QuestBoard.Service/Controllers/QuestBoard/QuestController.cs
    - QuestBoard.Repository/Migrations/QuestBoardContextModelSnapshot.cs
    - QuestBoard.UnitTests/Services/QuestServiceTests.cs
    - QuestBoard.UnitTests/Services/EmailConfirmationJobGuardTests.cs
decisions:
  - "Rewards kept off both Campaign-sanitization allowlists (Create/Edit) so it survives untouched for Campaign quests"
  - "CreateFollowUp GET pre-fill object left unchanged — Rewards is never copied from the original quest, DM fills it fresh on the follow-up form"
metrics:
  duration_minutes: 7
  completed: 2026-07-06
  tasks_completed: 3
  files_changed: 14
---

# Phase 59 Plan 01: Rewards field backend plumbing Summary

Threaded a nullable `Rewards` string end-to-end through the Entity/Domain/ViewModel stack, the two explicit-parameter service/repository methods, a dedicated EF Core migration, and the affected unit tests — establishing the data layer that Plan 59-02's views will bind to.

## What Was Built

- **Task 1:** Added `public string? Rewards { get; set; }` to `QuestEntity`, `Quest` (domain), `QuestViewModel`, and `FollowUpQuestViewModel`, mirroring `Recap`'s exact nullable/unattributed shape (no `[Required]`, no `[StringLength]`). Placed between `Description` and `ChallengeRating` on both ViewModels to match form field order. No AutoMapper `ForMember` needed — convention-based mapping picks up the same-named property automatically on both `EntityProfile` and `ViewModelProfile`.
- **Task 2:** Added a `string? rewards` parameter (positioned immediately after `description`) to `IQuestService.UpdateQuestPropertiesWithNotificationsAsync`, `IQuestRepository.UpdateQuestPropertiesWithNotificationsAsync`, and `IQuestService.CreateFollowUpQuestWithDetailsAsync`, plus their `QuestService`/`QuestRepository` implementations. `QuestRepository` now assigns `entity.Rewards = rewards;` alongside `entity.Description`. `QuestController.Edit` POST passes `viewModel.Quest.Rewards`; `QuestController.CreateFollowUp` POST passes `viewModel.Rewards`. The Campaign-sanitization blocks in `Create`/`Edit` POST and the `CreateFollowUp` GET pre-fill object were deliberately left untouched.
- **Task 3:** Generated the `AddRewardsToQuest` EF Core migration (nullable `nvarchar(max)` column on `Quests`, mirroring `AddRecapToQuest` exactly) via `dotnet ef migrations add`. The auto-regenerated `QuestBoardContextModelSnapshot.cs` gained the corresponding `Rewards` property entry. Fixed the 4 existing NSubstitute call sites across `QuestServiceTests.cs` and `EmailConfirmationJobGuardTests.cs` broken by the new parameter, and added a new `[Fact]` (`UpdateQuestPropertiesWithNotificationsAsync_WithRewards_ForwardsExactRewardsValueToRepository`) proving the service forwards the exact rewards string to the repository call.

## Verification

- Full solution build (`dotnet build QuestBoard.slnx -c Debug`): 0 errors, 0 warnings.
- Filtered unit test run (`QuestServiceTests|EmailConfirmationJobGuardTests`): 23/23 passed.
- Full unit test suite: 184/184 passed.
- Grep confirmed `Rewards` does not appear inside either Campaign-sanitization `if (boardType == BoardType.Campaign)` block.
- Grep confirmed no GSD tracking IDs (`D-0x`, `Phase 59`, `59-01`, `WR-0x`) in any modified source file.

## Deviations from Plan

None — plan executed exactly as written. The single `dotnet ef migrations add` command produced the migration pair and snapshot update precisely as specified; no manual snapshot edits were needed.

## TDD Gate Compliance

Task 3 was marked `tdd="true"`, but its actual shape was "fix 4 broken existing tests + add 1 new forwarding test + generate a migration" rather than a pure new-behavior RED→GREEN cycle. The new forwarding test and its passing implementation (the `rewards` parameter already threaded in Task 2) landed together in a single commit, since the behavior it verifies was already implemented by the prior task — there was no separate RED (failing-test) commit for this specific test. All tests pass; no gap in coverage, just a deviation from the strict RED/GREEN/REFACTOR commit sequence for this one task.

## Self-Check: PASSED

- FOUND: QuestBoard.Repository/Migrations/20260706194635_AddRewardsToQuest.cs
- FOUND: QuestBoard.Repository/Migrations/20260706194635_AddRewardsToQuest.Designer.cs
- FOUND: commit 43fc26d (Task 1)
- FOUND: commit 0b032db (Task 2)
- FOUND: commit b2edcf3 (Task 3)
