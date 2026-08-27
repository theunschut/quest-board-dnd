---
phase: 75-event-availability-signups
plan: 02
subsystem: api
tags: [efcore, ef-query-filters, group-membership, event-signup, atomicity, razor]

# Dependency graph
requires:
  - phase: 75-event-availability-signups (plan 01, sibling wave)
    provides: EventSignup entity, IEventSignupRepository/Service, EventEntity.Signups navigation
provides:
  - "GroupRepository.AddMemberAsync backfills a Yes event signup for every future/today event when joining a campaign board, atomically with the membership row"
  - "GroupRepository.RemoveMemberAsync deletes every event signup a member holds on the board (past and future) atomically with the membership removal"
  - "GroupService.AddMemberAsync/RemoveMemberAsync documented as the sole membership chokepoints"
  - "Platform Members views confirm, with identical wording, that removing a member deletes their event availability"
affects: [event-availability-signups, group-management, platform-admin]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Single-DbContext staged mutations + one SaveChangesAsync for cross-table atomicity (EF InMemory has no BeginTransactionAsync) — same shape as CharacterRepository.UpdateWithProfileImageAsync"
    - "IgnoreQueryFilters() immediately followed by an explicit Where() re-imposing scope from the method's own argument, for operations that must act on a board other than the caller's active one"

key-files:
  created:
    - QuestBoard.UnitTests/Repository/GroupRepositoryTests.cs
  modified:
    - QuestBoard.Repository/GroupRepository.cs
    - QuestBoard.Domain/Services/GroupService.cs
    - QuestBoard.Service/Areas/Platform/Views/Group/Members.cshtml
    - QuestBoard.Service/Areas/Platform/Views/Group/Members.Mobile.cshtml
    - QuestBoard.UnitTests/Services/GroupServiceTests.cs

key-decisions:
  - "Board type is read directly from GroupEntity by the groupId argument, never through IBoardTypeResolver, because that service answers for the caller's currently selected board rather than the board named by an arbitrary route id."
  - "Leave cleanup has no date boundary and no answered-state branch: everything a member holds on a board is deleted, past and future, automatic and deliberate — accepted per D-20/D-21 (see 75-CONTEXT.md)."

requirements-completed: [EVTAVAIL-02, EVTAVAIL-04]

coverage:
  - id: D1
    description: "Joining a campaign board backfills a Yes signup for every event dated today or later, staged in the same SaveChangesAsync as the membership row"
    requirement: EVTAVAIL-04
    verification:
      - kind: unit
        ref: "QuestBoard.UnitTests/Repository/GroupRepositoryTests.cs#AddMemberAsync_CampaignBoardWithPastPresentAndFutureEvents_BackfillsTodayAndFutureOnly"
        status: pass
      - kind: unit
        ref: "QuestBoard.UnitTests/Repository/GroupRepositoryTests.cs#AddMemberAsync_CampaignBoard_DoesNotBackfillEventsOnAnotherBoard"
        status: pass
      - kind: unit
        ref: "QuestBoard.UnitTests/Repository/GroupRepositoryTests.cs#AddMemberAsync_BackfillIsUnaffectedByActingCallersSelectedBoard"
        status: pass
    human_judgment: false
  - id: D2
    description: "Joining a one-shot board creates zero signup rows"
    requirement: EVTAVAIL-02
    verification:
      - kind: unit
        ref: "QuestBoard.UnitTests/Repository/GroupRepositoryTests.cs#AddMemberAsync_OneShotBoardWithFutureEvents_CreatesNoSignupRows"
        status: pass
    human_judgment: false
  - id: D3
    description: "Joining an already-held membership throws InvalidOperationException and writes no signup rows (no half state)"
    requirement: EVTAVAIL-04
    verification:
      - kind: unit
        ref: "QuestBoard.UnitTests/Repository/GroupRepositoryTests.cs#AddMemberAsync_ExistingMember_ThrowsAndWritesNoSignupRows"
        status: pass
    human_judgment: false
  - id: D4
    description: "Leaving a board deletes every event signup that member holds on it, past and future, in the same SaveChangesAsync as the membership removal, without touching another board or another member's rows"
    requirement: EVTAVAIL-04
    verification:
      - kind: unit
        ref: "QuestBoard.UnitTests/Repository/GroupRepositoryTests.cs#RemoveMemberAsync_RemovesAllSignupsIncludingPastAndAnswered_AndRemovesMembership"
        status: pass
      - kind: unit
        ref: "QuestBoard.UnitTests/Repository/GroupRepositoryTests.cs#RemoveMemberAsync_LeavesSignupsOnAnotherBoardUntouched"
        status: pass
      - kind: unit
        ref: "QuestBoard.UnitTests/Repository/GroupRepositoryTests.cs#RemoveMemberAsync_LeavesOtherMembersSignupsOnSameBoardUntouched"
        status: pass
      - kind: unit
        ref: "QuestBoard.UnitTests/Repository/GroupRepositoryTests.cs#RemoveMemberAsync_NonMember_RemovesNothingAndThrowsNothing"
        status: pass
    human_judgment: false
  - id: D5
    description: "GroupService.AddMemberAsync/RemoveMemberAsync are documented as the sole membership chokepoints and remain pure pass-throughs"
    verification:
      - kind: unit
        ref: "QuestBoard.UnitTests/Services/GroupServiceTests.cs#AddMemberAsync_ForwardsArgumentsUnchangedToRepository"
        status: pass
      - kind: unit
        ref: "QuestBoard.UnitTests/Services/GroupServiceTests.cs#RemoveMemberAsync_ForwardsArgumentsUnchangedToRepository"
        status: pass
    human_judgment: false
  - id: D6
    description: "Both Platform Members views (desktop and mobile) confirm, with identical wording, that removing a member deletes their event availability before the request submits"
    requirement: EVTAVAIL-04
    verification: []
    human_judgment: true
    rationale: "Native browser confirm() dialog text is not assertable through the automated test harness — requires manual verification per 75-VALIDATION.md Manual-Only Verifications table."

# Metrics
duration: ~25min
completed: 2026-08-27
status: complete
---

# Phase 75 Plan 02: Membership-Synced Event Availability Summary

**Campaign-board joins backfill a Yes signup per future event and leaves erase every signup a member holds, both atomic with the membership write via single-DbContext staging**

## Performance

- **Duration:** ~25 min
- **Started:** 2026-08-27T13:20:00Z (approx.)
- **Completed:** 2026-08-27T13:46:18Z
- **Tasks:** 3
- **Files modified:** 6 (1 created, 5 modified)

## Accomplishments

- `GroupRepository.AddMemberAsync` now backfills a Yes event signup for every event dated today or later when the target board is a campaign board, staged on the same `DbContext` and committed by the one pre-existing `SaveChangesAsync` — a failed backfill cannot leave a member with no rows and no opt-in path.
- `GroupRepository.RemoveMemberAsync` now deletes every event signup that member holds on the board, past and future, automatic and deliberate, in the same save as the membership removal.
- Both new bypass queries (`GetFutureEventIdsForGroupIgnoringActiveBoardAsync`, `GetEventSignupsForMemberIgnoringActiveBoardAsync`) resolve board type and scope from the explicit `groupId`/`userId` arguments rather than the caller's currently selected board, closing the ambient-scoping gap the Platform group page (an arbitrary-board-by-route-id admin surface) would otherwise hit.
- `GroupService.AddMemberAsync`/`RemoveMemberAsync` are now documented via XML doc comments as the two membership chokepoints every caller (Platform group page, invite flow, admin user removal) funnels through.
- Both Platform Members views (`Members.cshtml`, `Members.Mobile.cshtml`) now confirm before submitting the Remove Member form, with identical wording naming what will be lost.
- 9 new `GroupRepositoryTests` facts and 2 new `GroupServiceTests` facts prove the backfill boundary, the cross-board/cross-member isolation, the join no-half-state guarantee, and the service pass-through — all seeding a real `GroupEntity.BoardType` row rather than relying on the resolver stub.

## Task Commits

Each task was committed atomically:

1. **Task 1: Atomic campaign backfill inside GroupRepository.AddMemberAsync** - `c63d3b9` (feat)
2. **Task 2: Leave cleanup in RemoveMemberAsync, GroupService chokepoint documentation, and the Platform remove confirmation** - `8bc982a` (feat)
3. **Task 3: GroupRepositoryTests for the backfill and cleanup, plus chokepoint pass-through facts in GroupServiceTests** - `ad471ea` (test)

## Files Created/Modified

- `QuestBoard.Repository/GroupRepository.cs` - `AddMemberAsync` backfill, `RemoveMemberAsync` cleanup, two new `IgnoreQueryFilters` helpers scoped by their explicit arguments
- `QuestBoard.Domain/Services/GroupService.cs` - chokepoint XML doc comments on `AddMemberAsync`/`RemoveMemberAsync`
- `QuestBoard.Service/Areas/Platform/Views/Group/Members.cshtml` - `onsubmit="return confirm(...)"` on the Remove Member form
- `QuestBoard.Service/Areas/Platform/Views/Group/Members.Mobile.cshtml` - identical confirmation text on the mobile Remove Member form
- `QuestBoard.UnitTests/Repository/GroupRepositoryTests.cs` - new file, 9 facts covering backfill and cleanup
- `QuestBoard.UnitTests/Services/GroupServiceTests.cs` - 2 new pass-through facts

## Decisions Made

- Board type is resolved by reading `GroupEntity` directly via the `groupId` argument, never through `IBoardTypeResolver` — that service answers for the caller's currently active board, which has no relationship to an arbitrary board managed by route id (the Platform group page never sets an active board at all).
- Atomicity is achieved by staging both mutations (membership + signups) on one `DbContext` and calling the single pre-existing `SaveChangesAsync`, not by `Database.BeginTransactionAsync` — the in-memory test provider throws on explicit transactions, and this mirrors the shipped precedent in `CharacterRepository.UpdateWithProfileImageAsync`.
- Leave cleanup has no date boundary and no answered-state branch, per D-20/D-21: everything a member holds on a board is deleted, past and future, automatic and deliberate. This is the only place a member leaving a board loses history (quest signups, date votes, characters, gold, transactions all survive) — an accepted, documented asymmetry, not something this plan "fixes."

## Deviations from Plan

None - plan executed exactly as written. Test assertions initially used `ActiveGroupId = null` contexts to read "unfiltered" cross-board data, which triggered the codebase's known fail-closed query-filter behavior (a null `ActiveGroupId` now excludes every row rather than including every row). This was corrected during Task 3's own verification loop — before any task was marked done or committed — by adding explicit `.IgnoreQueryFilters()` to the assertion queries, matching the harness convention already documented in `PlayerSignupRepositoryTests.cs`. Not logged as a plan deviation since it was a test-authoring correction caught by the task's own `<verify>` step, not a change to production behavior or plan scope.

## Issues Encountered

None beyond the test-authoring correction described above.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- `GroupService.AddMemberAsync`/`RemoveMemberAsync` are now the proven, documented, atomic chokepoints a later phase's availability grid can rely on: "is a member of a campaign board" now implies "has a row on every upcoming event."
- Manual verification still needed (per `75-VALIDATION.md`): confirm both Platform Members confirmation dialogs render with the intended wording in a real browser (native `confirm()` text is not assertable through the automated harness).
- No blockers for sibling/dependent plans in this phase (EventSignup entity, controllers, and tenant-isolation tests are owned by other plans in the same wave/phase).

---
*Phase: 75-event-availability-signups*
*Completed: 2026-08-27*
