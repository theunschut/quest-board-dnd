---
phase: 88-calendar-feed-times-anchored-to-the-board-timezone
plan: 05
subsystem: calendar-feed
tags: [icalendar, sequence, dtstamp, ef-core, migration, savechanges, change-tracker, gap-closure]

requires:
  - phase: 88
    plan: 02
    provides: "SEQUENCE:1 floor and the writer's UTC-labelled DTSTAMP"
  - phase: 88
    plan: 03
    provides: "Ambient-clock seam guard that this plan extends"
provides:
  - "A store-owned per-entry revision (FeedRevision, FeedRevisedAt) on Events and Quests, raised in the store on every save that changes what the feed shows"
  - "AddFeedEntryRevisions migration that also raises every existing row once"
  - "A feed writer that publishes SEQUENCE from the stored revision (floored at 1) and DTSTAMP from the last-revised time on both entry branches"
  - "Field-by-field proof of the revision rules and a guard forbidding writes that bypass the change tracker"
affects: [88-06, 88-07, 88-08, 88-09, phase-88-verification]

gap_closure: true
gap_ids: [G-88-4]
plan_head_before: 6908b7d4421bbe1246b2baa6761ef803836abf98

actuals:
  tokens: 11000
  tasks: 2
  commits: 2

tech-stack:
  added: []
  patterns:
    - "Store-side revision hook inside the DbContext's own SaveChanges(bool) and SaveChangesAsync(bool, ct) overrides, so every construction path (DI, the integration factory's re-registration, tests that build the context directly) gets the rule with no registration to forget"
    - "Original-versus-current comparison through the change tracker after DetectChanges, so mapping a domain model over a tracked entity is seen"
    - "Closed-allowlist source guard with an empty allowlist plus a positive fact that keeps it from passing vacuously"

key-files:
  created:
    - QuestBoard.Repository/FeedRevisionStamper.cs
    - QuestBoard.Repository/Migrations/20260930161610_AddFeedEntryRevisions.cs
    - QuestBoard.Repository/Migrations/20260930161610_AddFeedEntryRevisions.Designer.cs
    - QuestBoard.UnitTests/Repository/FeedRevisionStamperTests.cs
    - QuestBoard.UnitTests/Architecture/FeedRevisionWriteSeamTests.cs
  modified:
    - QuestBoard.Repository/Entities/EventEntity.cs
    - QuestBoard.Repository/Entities/QuestEntity.cs
    - QuestBoard.Repository/Entities/QuestBoardContext.cs
    - QuestBoard.Repository/Automapper/EntityProfile.cs
    - QuestBoard.Repository/Migrations/QuestBoardContextModelSnapshot.cs
    - QuestBoard.Domain/Models/Event.cs
    - QuestBoard.Domain/Models/QuestBoard/Quest.cs
    - QuestBoard.Domain/Models/CalendarFeedEntry.cs
    - QuestBoard.Domain/Services/CalendarSubscriptionService.cs
    - QuestBoard.Domain/Services/CalendarFeedWriter.cs
    - QuestBoard.IntegrationTests/Tests/CalendarSubscriptionQuestFeedTests.cs
    - QuestBoard.UnitTests/Services/CalendarFeedWriterTests.cs
    - QuestBoard.UnitTests/Services/CalendarFeedBoardZoneGuardTests.cs
    - QuestBoard.UnitTests/Architecture/AmbientClockSeamTests.cs

key-decisions:
  - "The revision hook lives in QuestBoardContext's own save overrides rather than a registered SaveChangesInterceptor, because the integration factory re-registers the context without interceptors and about twenty unit tests construct the context directly"
  - "Column names FeedRevision (int) and FeedRevisedAt (UTC DateTime) on both entities; the map from domain to entity ignores both, and the stamper puts back the stored values before deciding a bump"
  - "Event feed-visible fields: Title, Date, StartTime, CancelledAt, GroupId. Quest feed-visible fields: Title, FinalizedDate, IsFinalized, GroupId"
  - "FeedRevisedAt never moves backwards: under clock skew the stored stamp stays and the revision still rises"
  - "The migration is the revision of every existing row: FeedRevision goes 1 to 2 and FeedRevisedAt becomes SYSUTCDATETIME() for all Events and Quests"

patterns-established:
  - "A row's revision is a store concern: callers and mappers can neither set nor lower it"
  - "The writer never publishes a sequence below 1 and never derives a stamp from a clock"

requirements-completed: [CALTZ-11, CALTZ-12, CALTZ-13, CALTZ-16]

coverage:
  - id: D1
    description: "A rescheduled quest goes out under a byte-identical UID with SEQUENCE:2 in place of SEQUENCE:1 and a DTSTAMP no earlier than the move and later than the first fetch's"
    requirement: "CALTZ-11"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Tests/CalendarSubscriptionQuestFeedTests.cs#Feed_RescheduledQuest_UpdatesInPlaceWithinTheWindowAndDisappearsOutsideIt"
        status: pass
    human_judgment: false
  - id: D2
    description: "The AddFeedEntryRevisions migration adds FeedRevision (default 1) and FeedRevisedAt to Events and Quests and raises every existing row once; the model has no drift"
    requirement: "CALTZ-12"
    verification:
      - kind: other
        ref: "dotnet ef migrations has-pending-model-changes; dotnet ef migrations script AddCalendarSubscriptions AddFeedEntryRevisions"
        status: pass
    human_judgment: true
    rationale: "The generated SQL was inspected and the model matches, but the bump was not executed against SQL Server here; the local SQL Server run is the next plan's job"
  - id: D3
    description: "A save raises the revision by exactly one only when a feed-visible field changed value, the stamp never goes backwards, callers cannot set the stored values, and both save overloads apply the rules"
    requirement: "CALTZ-13"
    verification:
      - kind: unit
        ref: "QuestBoard.UnitTests/Repository/FeedRevisionStamperTests.cs (38 tests, every feed-visible and non-feed field, store ownership, clock skew, sync and async save, repository write paths)"
        status: pass
    human_judgment: false
  - id: D4
    description: "The writer publishes SEQUENCE from the entry's revision (floored at 1) and DTSTAMP from the last-revised time with the stored UTC digits unshifted, on the timed and the all-day branch; whole-block pins unchanged"
    requirement: "CALTZ-16"
    verification:
      - kind: unit
        ref: "QuestBoard.UnitTests/Services/CalendarFeedWriterTests.cs#Write_Entry_DtstampMatchesTheLastRevisionTime"
        status: pass
      - kind: unit
        ref: "QuestBoard.UnitTests/Services/CalendarFeedWriterTests.cs#Write_EntryWithUnspecifiedKindRevisionTime_StampsTheStoredDigitsUnshifted"
        status: pass
    human_judgment: false
  - id: D5
    description: "No production source outside the migrations writes rows past the change tracker, pinned by a guard with an empty allowlist; the ambient-clock guard covers the context and the stamper"
    requirement: "CALTZ-13"
    verification:
      - kind: unit
        ref: "QuestBoard.UnitTests/Architecture/FeedRevisionWriteSeamTests.cs"
        status: pass
      - kind: unit
        ref: "QuestBoard.UnitTests/Architecture/AmbientClockSeamTests.cs"
        status: pass
    human_judgment: false

duration: 12min
completed: 2026-09-30
status: complete
---

# Phase 88 Plan 05: Stored Feed Revision Summary

**Store-owned per-entry revision on Events and Quests, raised inside QuestBoardContext's save overrides only when a feed-visible field changes, published as SEQUENCE and DTSTAMP so a rescheduled quest goes out under the same UID with a higher number and a later stamp**

## Performance

- **Duration:** 12 min
- **Started:** 2026-09-30T16:13:08Z
- **Completed:** 2026-09-30T16:25:00Z
- **Tasks:** 2
- **Files modified:** 19 (5 created, 14 modified)

## Accomplishments

- A quest whose finalized date moves now reaches a subscriber under a byte-identical UID with `SEQUENCE:2` (was `SEQUENCE:1` on both fetches) and a `DTSTAMP` from the moment of the move. This is the signal a revision-comparing calendar client needs before it replaces the copy it holds.
- The revision is raised in the store by `FeedRevisionStamper`, called from both `SaveChanges(bool)` and `SaveChangesAsync(bool, ct)` on `QuestBoardContext`. It compares each feed-visible property's original and current value after `DetectChanges`, so a description-only edit, an unchanged re-save and an email-sent bookkeeping write leave the entry byte-identical.
- `AddFeedEntryRevisions` adds the columns and raises every existing row once, which repairs the entry from UAT test 4.
- A guard test fails the build if `ExecuteUpdate`, `ExecuteSql`, `.Attach(`, `.AttachRange(`, `.Update(` or `.UpdateRange(` appears in production source outside the migrations.

## Migration and field lists

- **Migration file:** `QuestBoard.Repository/Migrations/20260930161610_AddFeedEntryRevisions.cs` (with `.Designer.cs`, and the model snapshot regenerated).
- **Event feed-visible fields:** `Title`, `Date`, `StartTime`, `CancelledAt`, `GroupId`.
- **Quest feed-visible fields:** `Title`, `FinalizedDate`, `IsFinalized`, `GroupId`.
- **Generated SQL checked:** `ALTER TABLE [Events] ADD [FeedRevision] int NOT NULL DEFAULT 1` and the same for `[Quests]`, then `UPDATE [Events] SET [FeedRevision] = [FeedRevision] + 1, [FeedRevisedAt] = SYSUTCDATETIME()` and the `[Quests]` equivalent.

## Task Commits

1. **Task 1: End to end tracer** - `f1e43ca6` (feat) - columns, stamper, context overrides, migration, entry revision inputs, writer, rewritten reschedule fact
2. **Task 2: Revision rules proven, write seam guarded** - `446a7e20` (test) - 38 stamper tests, 3 seam tests, ambient-clock guard extension

**Plan metadata:** recorded in the docs commit that follows this summary.

## Test totals (Windows)

- Calendar unit filter (`CalendarFeed|CalendarSubscriptionQuestRecheck|AmbientClockSeamTests|FeedRevision`): 177 passed, 0 failed
- Calendar integration filter (`CalendarSubscriptionFeedTests|CalendarSubscriptionQuestFeedTests|CalendarFeedBoardZoneHttpTests`): 52 passed, 0 failed
- Full unit project: 777 passed, 0 failed
- Full integration project: 937 passed, 0 failed
- `dotnet ef migrations has-pending-model-changes`: no drift

## Files Created/Modified

- `QuestBoard.Repository/FeedRevisionStamper.cs` - the store-side revision rules and the two feed-visible field lists
- `QuestBoard.Repository/Entities/QuestBoardContext.cs` - optional `TimeProvider` constructor parameter and the two save overrides
- `QuestBoard.Repository/Entities/EventEntity.cs`, `QuestEntity.cs` - `FeedRevision`, `FeedRevisedAt`
- `QuestBoard.Repository/Automapper/EntityProfile.cs` - domain-to-entity maps ignore both columns
- `QuestBoard.Repository/Migrations/20260930161610_AddFeedEntryRevisions*.cs`, `QuestBoardContextModelSnapshot.cs` - the migration
- `QuestBoard.Domain/Models/CalendarFeedEntry.cs` - `Sequence` and `LastRevisedAt` replace the creation-time input
- `QuestBoard.Domain/Services/CalendarSubscriptionService.cs` - entries take the stored revision; ETag comment reworded
- `QuestBoard.Domain/Services/CalendarFeedWriter.cs` - `DTSTAMP` from `LastRevisedAt`, `SEQUENCE` through one floored helper, on both branches
- Domain `Event` and `Quest` - read-only carriers of the two values
- Tests as listed in the frontmatter

## Decisions Made

See `key-decisions`. In short: hook in the context not an interceptor, `FeedRevision`/`FeedRevisedAt` names, the two field lists above, a stamp that never goes backwards, and the migration counting as every existing row's first revision.

## Deviations from Plan

None - plan executed exactly as written.

## TDD Gate Compliance

Task 2 is marked `tdd="true"`, but the plan places the implementation in Task 1 (the tracer) and the tests in Task 2, so no failing-test commit precedes the implementation and there is no `test(...)` RED commit ahead of a `feat(...)` commit. This is the plan's designed order, not a skipped gate. To confirm the new tests bite, three defects were injected into `FeedRevisionStamper` in the working tree and reverted without committing: dropping `GroupId` from the event list failed the `GroupId` event case, and removing the never-backwards guard failed the clock-skew fact. A third injection, removing the two lines that put the stored values back, failed nothing, because setting `IsModified = false` on a property already reverts it to its original value; those lines are kept as the explicit statement of the rule.

## Issues Encountered

None. The build, model snapshot and every calendar test were green on the first run after the edits.

## Known Stubs

None.

## User Setup Required

None - no external service configuration required. The migration is applied on startup; do not run `dotnet ef database update` before the local SQL Server proof in the last plan of this phase.

## Next Phase Readiness

- Ready for 88-06. The rescheduled quest and the stamper's field rules are pinned; 88-06 (a reader's availability answer) and 88-08 (`LAST-MODIFIED` and rewritten whole-block pins) build on `Sequence` and `LastRevisedAt`.
- Gap G-88-4 and CALTZ-09 are left for verify-work to mark, as instructed.
- Known limitations recorded in the plan and unchanged here: renaming a board, the quest session length and the board zone reach clients only with each entry's next real revision.

## Self-Check: PASSED

- Created files verified on disk: `FeedRevisionStamper.cs`, both migration files, `FeedRevisionStamperTests.cs`, `FeedRevisionWriteSeamTests.cs`.
- Commits `f1e43ca6` and `446a7e20` exist on `milestone/v9-rolling-improvements`.
- Acceptance checks: `FeedRevisionStamper.Apply` appears twice in `QuestBoardContext.cs`; `entry.LastRevisedAt` appears twice in `CalendarFeedWriter.cs`; no `CreatedAt` property remains on `CalendarFeedEntry`; no planning identifiers appear in the new source or the migration.

---
*Phase: 88-calendar-feed-times-anchored-to-the-board-timezone*
*Completed: 2026-09-30*
