---
phase: 36-campaign-quest-posting-closing
plan: 01
subsystem: database
tags: [ef-core, automapper, migrations, integration-tests, campaign-mode]

# Dependency graph
requires:
  - phase: 35-board-type-configuration
    provides: "BoardType enum (OneShot/Campaign) on GroupEntity/Group, threaded through entity/domain/projection models"
provides:
  - "IsClosed (bool) + ClosedDate (DateTime?) fields on QuestEntity and Quest domain model"
  - "Reversible AddQuestCloseFields EF Core migration (additive columns on Quests)"
  - "TestDataHelper.CreateTestQuestAsync isClosed/closedDate/groupId optional params"
  - "TestDataHelper.SeedCampaignGroupAsync helper (default group id 2, BoardType.Campaign)"
affects: [36-02, 36-03, 36-04, 36-05]

# Tech tracking
tech-stack:
  added: []
  patterns: ["Additive parallel lifecycle field pair (IsX/XDate) mirroring existing IsFinalized/FinalizedDate shape, with implicit same-name AutoMapper mapping"]

key-files:
  created:
    - QuestBoard.Repository/Migrations/20260703135517_AddQuestCloseFields.cs
    - QuestBoard.Repository/Migrations/20260703135517_AddQuestCloseFields.Designer.cs
  modified:
    - QuestBoard.Repository/Entities/QuestEntity.cs
    - QuestBoard.Domain/Models/QuestBoard/Quest.cs
    - QuestBoard.Repository/Migrations/QuestBoardContextModelSnapshot.cs
    - QuestBoard.IntegrationTests/Helpers/TestDataHelper.cs

key-decisions:
  - "IsClosed/ClosedDate added as net-new fields, not a reuse of IsFinalized/FinalizedDate — keeps campaign quests structurally excluded from DailyReminderJob/QuestFinalizedEmailJob"
  - "No index added on (IsClosed, ClosedDate) yet — deferred per RESEARCH.md Open Question 1 guidance until the board filter query is shown to be slow"
  - "SeedCampaignGroupAsync defaults to group id 2 to avoid colliding with SeedDefaultGroupAsync's group id 1"

patterns-established:
  - "Campaign lifecycle fields mirror one-shot lifecycle fields exactly (unannotated, no [Required], implicit AutoMapper same-name mapping) — future lifecycle-adjacent fields should follow the same shape"

requirements-completed: [CQUEST-03, CQUEST-04, CQUEST-05, CQUEST-06]

# Metrics
duration: 15min
completed: 2026-07-03
---

# Phase 36 Plan 01: Quest Close Lifecycle Persistence Foundation Summary

**Additive IsClosed/ClosedDate fields on QuestEntity + Quest, a reversible EF Core migration, and TestDataHelper campaign-group/closed-quest seeding support**

## Performance

- **Duration:** ~15 min
- **Started:** 2026-07-03T13:42:00Z
- **Completed:** 2026-07-03T13:57:21Z
- **Tasks:** 2 completed
- **Files modified:** 6 (2 new migration files, 4 modified)

## Accomplishments
- `QuestEntity` and `Quest` domain model both carry `IsClosed`/`ClosedDate`, positioned right after `FinalizedEmailSentForDate`, mirroring the unannotated `IsFinalized`/`FinalizedDate` shape exactly
- Generated `AddQuestCloseFields` migration: additive `IsClosed` (bit, default false) and `ClosedDate` (datetime2, nullable) columns on `Quests`, with a clean `Down()` that drops both
- `TestDataHelper.CreateTestQuestAsync` extended with trailing optional `isClosed`/`closedDate`/`groupId` params (existing positional callers unaffected)
- New `TestDataHelper.SeedCampaignGroupAsync(services, groupId = 2)` helper seeds a `Campaign`-board-type group for integration tests

## Task Commits

Each task was committed atomically:

1. **Task 1: Add IsClosed/ClosedDate to entity + domain model, generate migration** - `3fb4e1c` (feat)
2. **Task 2: Extend TestDataHelper for campaign-group and closed-quest seeding** - `db2dcca` (feat)

## Files Created/Modified
- `QuestBoard.Repository/Entities/QuestEntity.cs` - Added `ClosedDate`/`IsClosed` properties
- `QuestBoard.Domain/Models/QuestBoard/Quest.cs` - Added `ClosedDate`/`IsClosed` properties
- `QuestBoard.Repository/Migrations/20260703135517_AddQuestCloseFields.cs` - Reversible migration adding both columns to `Quests`
- `QuestBoard.Repository/Migrations/20260703135517_AddQuestCloseFields.Designer.cs` - EF migration designer/snapshot metadata
- `QuestBoard.Repository/Migrations/QuestBoardContextModelSnapshot.cs` - Updated model snapshot
- `QuestBoard.IntegrationTests/Helpers/TestDataHelper.cs` - `CreateTestQuestAsync` gains `isClosed`/`closedDate`/`groupId` optional params; new `SeedCampaignGroupAsync` helper

## Decisions Made
- No explicit AutoMapper `ForMember` added for `IsClosed`/`ClosedDate` — confirmed same-name implicit mapping already works for `IsFinalized`/`FinalizedDate` with zero explicit mapping in `EntityProfile.cs`, so the new fields follow the same convention with no code changes needed there
- No new database index added in this plan — matches RESEARCH.md's "defer until actually slow" guidance for the `!IsClosed` board-filter scan

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

None. `dotnet ef migrations add` emitted the standard pre-existing global-query-filter warnings (from Phase 34.2's soft-delete/tenancy filters) unrelated to this change; migration output matched the plan's exact acceptance criteria on the first attempt.

## User Setup Required

None - no external service configuration required. The migration auto-applies on next app startup via `context.Database.Migrate()`.

## Next Phase Readiness

- `IsClosed`/`ClosedDate` are now available for Plan 02 (service methods `CloseQuestAsync`/`ReopenQuestAsync`), Plan 03 (controller actions), and Plan 04 (views)
- `TestDataHelper.SeedCampaignGroupAsync` + extended `CreateTestQuestAsync` give downstream plans everything needed to seed a campaign-type group and a closed campaign quest in integration tests
- No blockers identified

---
*Phase: 36-campaign-quest-posting-closing*
*Completed: 2026-07-03*
