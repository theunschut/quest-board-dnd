---
phase: 55-fix-cross-tenant-quest-leak-on-quest-board-quests-from-anoth
plan: 01
subsystem: database
tags: [ef-core, query-filter, multi-tenancy, security]

# Dependency graph
requires:
  - phase: 49-fix-guild-members-page-missing-group-tenant-filtering
    provides: The fail-closed HasQueryFilter shape (CharacterEntity) this plan replicates across 7 more entities
provides:
  - Fail-closed HasQueryFilter for all 7 group-scoped entities (QuestEntity, ShopItemEntity, ProposedDateEntity, PlayerDateVoteEntity, PlayerSignupEntity, ReminderLogEntity, UserTransactionEntity)
  - Regression test file proving zero-rows-on-null-ActiveGroupId for all 7 entities
affects: [55-02 (GroupSessionMiddleware/GroupPickerController fixes — same phase, defense-in-depth layer above this one)]

# Tech tracking
tech-stack:
  added: []
  patterns: ["EF Core HasQueryFilter fail-closed shape (ActiveGroupId != null && ...) applied uniformly to every group-scoped entity, no null-passthrough escape hatch"]

key-files:
  created:
    - QuestBoard.UnitTests/Repository/QuestBoardContextFilterTests.cs
  modified:
    - QuestBoard.Repository/Entities/QuestBoardContext.cs
    - QuestBoard.UnitTests/Repository/PlayerSignupRepositoryTests.cs

key-decisions:
  - "All 7 group-scoped entity filters (5 named in CONTEXT.md + 2 discovered by RESEARCH.md — ReminderLogEntity, UserTransactionEntity) hardened uniformly, not just the entities directly involved in the reported symptom"
  - "PlayerSignupRepositoryTests' TestActiveGroupContext changed from ActiveGroupId => null to => 1, since that helper seeds and queries group-1 data through the same context instance and only worked previously because the fail-open filter let a null ActiveGroupId see every row"

patterns-established:
  - "Fail-closed group-scoped query filter: ActiveGroupId != null && <path>.GroupId == ActiveGroupId, no null-passthrough"

requirements-completed: []

# Metrics
duration: 25min
completed: 2026-07-06
---

# Phase 55 Plan 01: Fail-closed EF Core query filter hardening Summary

**Hardened all 7 group-scoped `HasQueryFilter` predicates in `QuestBoardContext.cs` (5 named + 2 discovered) from fail-open `ActiveGroupId == null || ...` to fail-closed `ActiveGroupId != null && ...`, closing the data-layer half of the cross-tenant quest leak.**

## Performance

- **Duration:** 25 min
- **Started:** 2026-07-06T07:50:00Z
- **Completed:** 2026-07-06T08:15:00Z
- **Tasks:** 2 completed
- **Files modified:** 3 (1 created, 2 modified)

## Accomplishments

- Wrote a new `QuestBoardContextFilterTests.cs` regression suite: 7 fail-closed assertions (one per group-scoped entity) plus 1 positive companion test, proving RED against the old fail-open filters before the fix
- Hardened all 7 `HasQueryFilter` predicates (`QuestEntity`, `ShopItemEntity`, `ProposedDateEntity`, `PlayerDateVoteEntity`, `PlayerSignupEntity`, `ReminderLogEntity`, `UserTransactionEntity`) to fail-closed, matching `CharacterEntity`'s existing Phase 49 shape
- Corrected the stale "Null = see all (SuperAdmin/seeding contexts intentionally bypass group scoping)" comment block to describe the new fail-closed intent
- Full unit test suite (177 tests) green after the fix, confirming no other test silently depended on the old fail-open shape (one test-fixture bug was found and fixed — see Deviations)

## Task Commits

Each task was committed atomically:

1. **Task 1: Write failing fail-closed filter regression tests (Wave 0)** - `8524d34` (test)
2. **Task 2: Harden all 7 group-scoped filters to fail-closed + correct comments** - `f7d270f` (feat)

_Note: Task 2's commit also includes the PlayerSignupRepositoryTests.cs fixture fix required for the plan's own verification step (full suite green) to pass — see Deviations below._

## Files Created/Modified

- `QuestBoard.UnitTests/Repository/QuestBoardContextFilterTests.cs` - New regression suite: 7 zero-rows-on-null-ActiveGroupId assertions + 1 positive companion test, using a local `MutableTestGroupContext` test double
- `QuestBoard.Repository/Entities/QuestBoardContext.cs` - All 7 group-scoped `HasQueryFilter` predicates flipped from `ActiveGroupId == null || ...` to `ActiveGroupId != null && ...`; explanatory comment block rewritten
- `QuestBoard.UnitTests/Repository/PlayerSignupRepositoryTests.cs` - `TestActiveGroupContext.ActiveGroupId` changed from `null` to `1` (see Deviations)

## Decisions Made

- Followed RESEARCH.md/PATTERNS.md's exact before/after transformation for all 7 entities, no deviation from the specified predicate shape or navigation paths.
- Did not touch the already-fail-closed `CharacterEntity`/`CharacterClassEntity`/`CharacterImageEntity` filters or their "no SuperAdmin escape hatch" comments, per the plan's explicit instruction — even though those comments are now slightly redundant (every entity lacks the escape hatch now), rewriting them was out of this plan's scope.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Fixed PlayerSignupRepositoryTests' TestActiveGroupContext relying on the fail-open filter**
- **Found during:** Task 2 verification (full unit suite run after hardening the filters)
- **Issue:** `PlayerSignupRepositoryTests.cs`'s single-arg `CreateContext(databaseName)` helper built a context with `TestActiveGroupContext` (`ActiveGroupId => null`), then seeded AND queried group-1 data through that *same* context instance in 9 tests (`ChangeVoteAsync_*`, `GetTopWaitlistedCandidateAsync_OrdersByVotePriorityThenTimestamp`, `GetTopWaitlistedCandidateAsync_SameVote_...`). This only worked because the old fail-open filter let a null `ActiveGroupId` see every group's rows. After hardening, these tests failed with "Player signup not found" / EF Core identity-conflict errors, since the null-context query now legitimately returns zero rows.
- **Fix:** Changed `TestActiveGroupContext.ActiveGroupId` from `null` to `1`, matching `SeedQuestAndUserAsync`'s own default `groupId = 1` parameter, so the seed-then-query-same-context pattern these 9 tests rely on continues to work under the new fail-closed filter.
- **Files modified:** `QuestBoard.UnitTests/Repository/PlayerSignupRepositoryTests.cs`
- **Verification:** Full unit suite (`dotnet test QuestBoard.UnitTests/QuestBoard.UnitTests.csproj`) went from 9 failures / 168 passed to 0 failures / 177 passed.
- **Committed in:** `f7d270f` (Task 2 commit)

---

**Total deviations:** 1 auto-fixed (Rule 1 - bug in test fixture, not production code)
**Impact on plan:** Necessary to satisfy the plan's own verification step ("full unit suite green — confirms no existing test relied on the fail-open behavior"). No scope creep — fix is confined to the one test fixture affected by this plan's filter change.

## Issues Encountered

None beyond the deviation above.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- Data-layer defense-in-depth (D-03) is complete and independently verified: a null `ActiveGroupId` now yields zero rows for all 7 group-scoped entities regardless of any middleware-layer bypass.
- `DailyReminderJob`'s cross-group sweep (`GetQuestsForTomorrowAllGroupsAsync`) uses `.IgnoreQueryFilters()` and is confirmed unaffected by this change (bypasses `HasQueryFilter` entirely).
- Plan 02 (middleware gate extension to SuperAdmin, `GroupPickerController.SelectGroup` membership check, periodic re-validation) is the remaining half of this phase's fix and is independent of this plan's changes — no blockers.

---
*Phase: 55-fix-cross-tenant-quest-leak-on-quest-board-quests-from-anoth*
*Completed: 2026-07-06*

## Self-Check: PASSED

- FOUND: QuestBoard.UnitTests/Repository/QuestBoardContextFilterTests.cs
- FOUND: QuestBoard.Repository/Entities/QuestBoardContext.cs
- FOUND: .planning/phases/55-fix-cross-tenant-quest-leak-on-quest-board-quests-from-anoth/55-01-SUMMARY.md
- FOUND commit: 8524d34 (test: add failing fail-closed filter regression tests)
- FOUND commit: f7d270f (feat: harden all 7 group-scoped query filters to fail-closed)
