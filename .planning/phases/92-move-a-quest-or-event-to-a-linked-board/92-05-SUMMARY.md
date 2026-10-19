---
phase: 92-move-a-quest-or-event-to-a-linked-board
plan: 05
subsystem: board-move
tags: [quest, move, signups, waitlist, chain, mail, identity]
requires:
  - phase: 92-01
    provides: tracer quest move, QuestMovePlan, single tracked save
  - phase: 92-02
    provides: Quest Moved mail and after-move waitlist mail enqueue methods
  - phase: 92-03
    provides: preview and stamp contract
provides:
  - QuestMoveRepository.ApplyAsync full reconciliation in one save
  - WaitlistOrdering.SelectPromotions (in-memory waitlist selection)
  - IIdentityService.IsSuperAdminAsync(int userId)
  - QuestMoveService real plan (membership, DM rule, waitlist, chain) and after-save mail fan-out
affects: [92-04, 92-06, 92-07, 92-08, 92-09]
tech-stack:
  added: []
  patterns:
    - "Waitlist promotions chosen in memory before the save, pinned to the repository rule by a parity test"
    - "Mail enqueued only after the save returns Moved, each enqueue in its own try/catch"
key-files:
  created:
    - QuestBoard.UnitTests/Repository/BoardMoveQuestRepositoryTests.cs
    - QuestBoard.UnitTests/Repository/BoardMoveWaitlistParityTests.cs
    - QuestBoard.UnitTests/Services/BoardMoveQuestPlanTests.cs
    - QuestBoard.UnitTests/Services/BoardMoveQuestMailTests.cs
    - QuestBoard.IntegrationTests/Tests/BoardMoveQuestPeopleTests.cs
    - QuestBoard.IntegrationTests/Tests/BoardMoveQuestMailFlowTests.cs
  modified:
    - QuestBoard.Repository/QuestMoveRepository.cs
    - QuestBoard.Repository/IdentityService.cs
    - QuestBoard.Domain/Services/QuestMoveService.cs
    - QuestBoard.Domain/Extensions/WaitlistOrdering.cs
    - QuestBoard.Domain/Interfaces/IIdentityService.cs
    - QuestBoard.UnitTests/Services/BoardMoveQuestPreviewTests.cs
key-decisions:
  - "The DM passes when they are an Identity SuperAdmin (read by id) or hold DungeonMaster/Admin on the target; the check runs before membership so a refused preview carries no plan or stamp"
  - "The acting super admin is never mailed, the DM gets only the DungeonMaster mail, a promoted player gets only the after-move waitlist mail"
patterns-established:
  - "Repository validates signup ids, follow-up ids, original link flag and promoted-in-kept before any mutation, GroupId assigned last"
status: complete
actuals:
  tokens: 60000
  tasks: 3
  commits: 3
---

# Phase 92 Plan 05: People, DM rule, chain, waitlist and mail Summary

A moved quest now looks as if it had been created on the target board: target members keep their signups and votes with characters cleared, non-members of any role are removed with their votes, the waitlist moves up for every freed seat, chain links that would cross boards are cut at both ends, a DM who cannot run quests there blocks the move, and everyone affected is mailed once under the target board.

## What was built

- `QuestMoveRepository.ApplyAsync` loads the quest, its signups with votes, and its follow-ups before any change. It returns `Changed` with nothing touched when the signup ids, the follow-up ids, the original-link flag or the promoted-in-kept rule no longer match the plan. Otherwise it removes dropped signups and their votes explicitly, clears `CharacterId` on kept signups, selects promoted ones, nulls both ends of the chain, assigns `GroupId` last and saves once.
- `WaitlistOrdering.SelectPromotions` picks unselected Player-role signups with a Yes or Maybe vote on the finalized proposed date, in `OrderWaitlist` order, up to the freed seats.
- `IIdentityService.IsSuperAdminAsync` reads the SuperAdmin Identity role by user id.
- `QuestMoveService` builds the real plan: DM rule first (`DungeonMasterNotEligible` with `Target` and `DmName` and no plan or stamp), then membership from `GetAllGroupMembers(target)`, the dropped list ordered selected-first then by name, `CharactersCleared`, `SeatsOpened`, promotions, chain ids and titles, and the stamp over kept, dropped, promoted and unlinked sets. The log line now carries the real kept, dropped, promoted and links-cut counts.
- `EnqueueMoveMail` runs only after `ApplyAsync` returns `Moved` and reads only the pre-move snapshot. Order: DM, kept not-promoted players, removed players, promoted players. Each recipient needs a confirmed address, is not the actor, and is mailed once per user. Every enqueue carries the target board id and sits in its own try/catch.

## Verification

- Unit: `BoardMoveQuestRepositoryTests` (15), `BoardMoveQuestPlanTests`, `BoardMoveQuestPreviewTests`, `BoardMoveWaitlistParityTests`, `BoardMoveQuestMailTests` (12), plus the Architecture, WaitlistOrdering, UserService and FeedRevisionStamper filters pass. Full unit suite: 1022 passed.
- Integration: `BoardMoveQuestPeople`, `BoardMoveQuestMail`, `BoardMoveApply`, `BoardMoveMail` 20 passed. Full integration suite: 1137 passed, 11 skipped (pre-existing).
- No planning references in added source lines.
- `CalendarFeedWriter.cs`, `CalendarSubscriptionService.cs` and `FeedRevisionStamper.cs` untouched. No new write shape banned by `FeedRevisionWriteSeamTests` (only property assignment, `Remove` and `RemoveRange`).

## Deviations from Plan

### Auto-fixed Issues

None needing a rule. Two process notes:

1. The mail fan-out (`EnqueueMoveMail`) was written in the same edit session as the Task 2 service work, so it landed in the Task 2 commit (16e3b632) instead of the Task 3 commit. Task 3's commit holds the unit and HTTP proofs for it. No behaviour differs from the plan.
2. Tests and implementation were committed together per task rather than as separate RED and GREEN commits; tests were written before the checks were run.

The tracer feedback gate did not apply: this plan contains no tracer task.

## Known Stubs

None.

## Not established

- InMemory enforces no unique index, so the "no hidden link blocks a new follow-up" intent is proven by asserting the stored `OriginalQuestId` values are null, not by a database constraint.
- Mail jobs are recorded by the capturing client, not run; rendering and delivery are covered by plan 02.

## Threat Flags

None. The new reads and writes stay inside the existing SuperAdminOnly move endpoint. Threat register items T-92-19 to T-92-24 are covered by the tests above (DM refusal over HTTP, non-member removal, chain nulls, mail gates, parity test, enqueue failure test).

## Commits

- 3d97e824: quest move save reconciles signups, waitlist and chain links
- 16e3b632: real plan from membership, DM role, chain and waitlist, with mail after the save
- f1fc4faf: tests for people, DM rule, chain, waitlist and mail

## Self-Check: PASSED

All created files exist and the three commits are in `git log`.
