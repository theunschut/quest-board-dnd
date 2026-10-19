---
phase: 92-move-a-quest-or-event-to-a-linked-board
plan: 01
subsystem: board-move
tags: [quest, linked-boards, move, super-admin, ef-core, feed-revision]
requires: []
provides:
  - MoveStatus and MoveBlockReason enums
  - BoardMove preview and plan contracts (MovePerson, MovePreview, QuestMovePreview, QuestMovePlan)
  - IQuestMoveService / QuestMoveService (tracer MoveAsync)
  - IQuestMoveRepository / QuestMoveRepository (single tracked save)
  - MoveController POST /Move/Quest (SuperAdminOnly)
  - BoardMoveSeed integration-test helper
affects: [92-02, 92-03, 92-04, 92-05, 92-06]
tech-stack:
  added: []
  patterns:
    - tracked load, validate before mutate, GroupId assigned last, one SaveChangesAsync, ChangeTracker.Clear on failure
    - preview object doubles as the move outcome via its Status
key-files:
  created:
    - QuestBoard.Domain/Enums/MoveStatus.cs
    - QuestBoard.Domain/Enums/MoveBlockReason.cs
    - QuestBoard.Domain/Models/BoardMove/MovePerson.cs
    - QuestBoard.Domain/Models/BoardMove/MovePreview.cs
    - QuestBoard.Domain/Models/BoardMove/QuestMovePreview.cs
    - QuestBoard.Domain/Models/BoardMove/QuestMovePlan.cs
    - QuestBoard.Domain/Interfaces/IQuestMoveService.cs
    - QuestBoard.Domain/Interfaces/IQuestMoveRepository.cs
    - QuestBoard.Domain/Services/QuestMoveService.cs
    - QuestBoard.Repository/QuestMoveRepository.cs
    - QuestBoard.Service/Controllers/MoveController.cs
    - QuestBoard.IntegrationTests/Helpers/BoardMoveSeed.cs
    - QuestBoard.IntegrationTests/Tests/BoardMoveApplyTests.cs
  modified:
    - QuestBoard.Domain/Extensions/ServiceExtensions.cs
    - QuestBoard.Repository/Extensions/ServiceExtensions.cs
decisions:
  - Refusals on POST redirect to the quest's Details with an Error toast; success redirects to the source Quest Board
  - The linked set is read fresh on every move, and the source board is never in it, so a posted source id is refused by the same check
status: complete
actuals:
  tokens: 14000
  tasks: 1
  commits: 1
---

# Phase 92 Plan 01: Quest move tracer Summary

A super admin's confirm POST now moves an open quest to the other board of its linked set in one tracked save, raising the feed revision by one through the existing stamper.

## What was built

- Contracts in `QuestBoard.Domain/Models/BoardMove` plus the `MoveStatus` and `MoveBlockReason` enums, as specified.
- `QuestMoveService.MoveAsync`: returns `NotFound` when there is no active board or the quest is not on it, reads the linked set fresh, returns `NoLinkedBoards` or `TargetNotLinked` without writing, builds the keep-everyone plan, applies it, logs one structured line (ids and counts only) on success, and maps any other exception to `Failed`.
- `QuestMoveRepository.ApplyAsync`: loads the tracked quest with signups and votes under the source board filter, checks the loaded signup ids against the plan before mutating, assigns `GroupId` last and saves once. A concurrency conflict clears the tracker and returns `Changed`. Any other failure clears the tracker and rethrows.
- `MoveController` is `[Authorize(Policy = "SuperAdminOnly")]` at class level. `ConfirmQuest` carries `[ValidateAntiForgeryToken]` and maps outcomes to the Success and Error toasts and redirects. No route was added to `CrossBoardLinkRegistry`.
- DI registrations in both `ServiceExtensions` files.
- `BoardMoveSeed` shared test helper and `BoardMoveApplyTests` (4 tests).

## Verification

- `dotnet build`: 0 errors.
- Integration tests (BoardMoveApply, AntiForgeryTokenCoverage, LinkedBoardStaticGuard): 29 passed.
- Unit tests (Architecture, CrossBoardLinkRegistry, FeedRevisionStamper, AmbientClock): 163 passed.
- No planning references in added source lines (grep count 0).
- `CalendarFeedWriter.cs`, `CalendarSubscriptionService.cs` and `FeedRevisionStamper.cs` untouched.

## Deviations from Plan

None - plan executed as written. One test-only adjustment: the second `BoardMoveApplyTests` fact follows the redirect first to consume the success toast, because the toast text names the quest and would otherwise defeat the "title no longer listed" assertion on `/quests`.

The tracer feedback gate had nothing to gate: this plan holds only the tracer task, and its `<verify>` passed end to end.

## Known Stubs

None.

## Threat Flags

None. The new endpoint is the one the plan's threat model covers.

## Self-Check: PASSED

All 15 files listed above exist and are committed in a41d1500.
