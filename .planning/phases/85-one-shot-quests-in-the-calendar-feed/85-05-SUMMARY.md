---
phase: 85-one-shot-quests-in-the-calendar-feed
plan: 05
subsystem: api
tags: [integration-tests, unit-tests, ef-core, calendar-feed, quest-board, security]

requires:
  - phase: 85-one-shot-quests-in-the-calendar-feed
    plan: 04
    provides: "GetFeedQuestsForUserAsync on QuestRepository, the disappearance-route facts, and CalendarSubscriptionQuestFeedTests with MutateQuestAsync/DeleteQuestAsync/DeleteSignupAsync as the mutation helpers this plan extends"
provides:
  - "Six behavioural facts on CalendarSubscriptionQuestFeedTests pinning board-type narrowing (both routes into the feed), tenant isolation against a non-member board, the leave-a-board removal with a no-error-on-healthy-fetch guarantee, merged-document ordering across sources, and the events-only byte-identical guarantee"
  - "New CalendarSubscriptionQuestRecheckTests unit suite driving CalendarSubscriptionService directly with a misbehaving fake quest repository, reaching the second-layer re-check's drop-and-log branch the real, filtered repository can never trigger"
  - "SeedEventAsync, SeedEventSignupAsync and RemoveMembershipAsync added to CalendarSubscriptionQuestFeedTests as the event-seeding and membership-removal halves of the seeding helper set"
affects: []

actuals:
  tokens: 8100
  tasks: 3
  commits: 3

tech-stack:
  added: []
  patterns:
    - "Two-board-per-fact structure for every membership/board-type fact: a positive outcome on one board and an opposite outcome on a second board in the same fetch, so absence alone can never be mistaken for correct scoping"
    - "Driving an internal domain service directly with NSubstitute fakes for every dependency except the real writer, to reach a re-check branch the real, filtered repository structurally cannot trigger -- following CrossBoardAgendaTests' established convention for this codebase's other cross-board re-check"
    - "Parsing SUMMARY line text in document order (rather than asserting containment or index comparisons) to assert an exact interleaved sequence across two entry sources"

key-files:
  created:
    - QuestBoard.UnitTests/Services/CalendarSubscriptionQuestRecheckTests.cs
  modified:
    - QuestBoard.IntegrationTests/Tests/CalendarSubscriptionQuestFeedTests.cs

key-decisions:
  - "Simulated the 'unscoped all-one-shot-boards' mutation (acceptance criterion for Task 1) as Enumerable.Range(1, 1000) rather than a literal query, since CalendarSubscriptionService has no method to query all one-shot boards independent of membership -- this represents the same failure mode (a board-id set that is not derived from the caller's own membership read) without inventing a second production code path"
  - "Fact 2 in Task 1 reused the same two-board setup as fact 1 but routed the campaign quest's inclusion attempt through Dungeon Master ownership instead of a signup row, per the plan's explicit framing that this is deliberately the same scenario proven through the second way into the feed"
  - "The non-qualifying quest in Task 3's byte-identical fact was made non-qualifying by omitting any signup row and using a different Dungeon Master, rather than seeding-then-mutating to un-finalized -- simpler and exercises the same 'reaches the query, gets filtered' path the plan's 'or one the reader holds no seat on' alternative names"

patterns-established: []

requirements-completed: [QUESTFEED-15, QUESTFEED-16, QUESTFEED-18]

coverage:
  - id: D1
    description: "A finalized quest the reader holds a confirmed seat on, on a campaign board the reader belongs to, never appears in the feed, while that same board's event does appear in the same fetch"
    requirement: "QUESTFEED-15"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Tests/CalendarSubscriptionQuestFeedTests.cs#Feed_BoardTypeNarrowsQuestsButNotEvents_WithinTheSameFetch"
        status: pass
    human_judgment: false
  - id: D2
    description: "A campaign quest the reader runs as Dungeon Master, with no signup row, still stays out -- the second way into the feed is proven closed too"
    requirement: "QUESTFEED-15"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Tests/CalendarSubscriptionQuestFeedTests.cs#Feed_CampaignQuestTheReaderRunsAsDungeonMaster_StaysOutAlongsideAQualifyingOneShotQuest"
        status: pass
    human_judgment: false
  - id: D3
    description: "A confirmed seat row seeded on a board the reader never joined never reaches the feed, even alongside a genuinely qualifying quest on a board the reader belongs to"
    requirement: "QUESTFEED-16"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Tests/CalendarSubscriptionQuestFeedTests.cs#Feed_ConfirmedSeatOnABoardTheReaderNeverJoined_StaysOutAlongsideAQualifyingQuest"
        status: pass
    human_judgment: false
  - id: D4
    description: "A board the reader leaves removes its quest at the very next fetch, and a healthy, correctly scoped fetch writes no error record"
    requirement: "QUESTFEED-16"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Tests/CalendarSubscriptionQuestFeedTests.cs#Feed_LeavingABoard_RemovesItsQuestFromTheVeryNextFetch_WithNoErrorLoggedOnAHealthyFetch"
        status: pass
    human_judgment: false
  - id: D5
    description: "A quest row that survives the feed query's predicate but falls outside the reader's one-shot board set is dropped before the response and recorded as a single error carrying both the dropped and fetched counts; a healthy read logs nothing; and the board-id set crossing into the repository carries only the reader's own one-shot boards"
    requirement: "QUESTFEED-16"
    verification:
      - kind: unit
        ref: "QuestBoard.UnitTests/Services/CalendarSubscriptionQuestRecheckTests.cs (4 facts: DroppedRow_NeverReachesTheDocument, DroppedRow_IsRecordedAsASingleErrorCarryingBothCounts, HealthyRead_LogsNothing, TheArgumentsCrossingIntoTheRepository_CarryOnlyTheReadersOwnOneShotBoards)"
        status: pass
    human_judgment: false
  - id: D6
    description: "The combined document orders every entry by date and then start time regardless of source, with all-day entries first on their day and sources interleaved rather than grouped, and a fetch with no qualifying quest produces a document byte-identical (including entity tag) to the event-only document"
    requirement: "QUESTFEED-18"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Tests/CalendarSubscriptionQuestFeedTests.cs#Feed_MergedDocument_OrdersEventsAndQuestsByDateThenStartTimeRegardlessOfSource"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Tests/CalendarSubscriptionQuestFeedTests.cs#Feed_EventsOnlyDocument_IsByteIdenticalWhenNoQuestQualifies"
        status: pass
    human_judgment: false

duration: 40min
completed: 2026-09-18
status: complete
---

# Phase 85 Plan 05: Board Type, Tenant Isolation, the Drop-and-Log Branch, and Merged-Document Ordering Summary

**Six new integration facts and a new four-fact unit suite close out the phase's security argument: board-type and membership narrowing are each proven independently against a second board with an opposite outcome in the same fetch (including through the Dungeon Master route), the second-layer re-check's drop-and-log branch is reached by driving the domain service directly with a misbehaving repository, and the merged event+quest document is proven correctly ordered and byte-identical to the pre-phase event-only feed when no quest qualifies -- with three mutation checks run and reverted, each confirmed to turn its named fact red.**

## Performance

- **Duration:** ~40 min
- **Completed:** 2026-09-18T16:45:00Z
- **Tasks:** 3 (all `type="auto"`)
- **Files modified:** 1 modified, 1 created

## Accomplishments
- `Feed_BoardTypeNarrowsQuestsButNotEvents_WithinTheSameFetch` -- a campaign board's quest is excluded from the same fetch where its event is present, guarding specifically against events ever being retroactively restricted to one-shot boards
- `Feed_CampaignQuestTheReaderRunsAsDungeonMaster_StaysOutAlongsideAQualifyingOneShotQuest` -- the same exclusion proven through the Dungeon Master route, the second way into the feed that could escape scoping if the two conditions were ever composed as separate queries
- `Feed_ConfirmedSeatOnABoardTheReaderNeverJoined_StaysOutAlongsideAQualifyingQuest` -- a confirmed seat row seeded deliberately on a non-member board proves the board predicate itself rather than the seat predicate
- `Feed_LeavingABoard_RemovesItsQuestFromTheVeryNextFetch_WithNoErrorLoggedOnAHealthyFetch` -- membership is re-read on every fetch, and a normal, correctly scoped fetch produces no error record
- `CalendarSubscriptionQuestRecheckTests` (new file, 4 facts) -- drives `CalendarSubscriptionService.GetFeedAsync` directly with a fake quest repository that returns a row outside the one-shot set it was handed, proving the row is dropped from a real rendered document, recorded as a single error carrying both counts, that a healthy read logs nothing, and that the board-id set crossing into the repository contains only the reader's own one-shot board
- `Feed_MergedDocument_OrdersEventsAndQuestsByDateThenStartTimeRegardlessOfSource` -- seeds entries in scrambled order and asserts the exact interleaved title sequence, proving the document is one ordered list rather than events grouped ahead of quests
- `Feed_EventsOnlyDocument_IsByteIdenticalWhenNoQuestQualifies` -- a feed with no qualifying quest produces a body and entity tag byte-identical to the pre-phase event-only document
- `SeedEventAsync`, `SeedEventSignupAsync` and `RemoveMembershipAsync` added to the class's seeding helper set, following the shapes already established in `CalendarSubscriptionFeedTests`
- Three one-shot mutation checks run and reverted (see Verification below), each confirmed to turn its named fact(s) red for the reason claimed

## Task Commits

Each task was committed atomically:

1. **Task 1: Membership and board type as two independent rules, each proven against a second board in the same fetch** - `719d4af1` (test)
2. **Task 2: Reach the drop-and-log branch the real repository cannot trigger** - `e929a529` (test)
3. **Task 3: Merged-document ordering, and the guarantee that an events-only feed is unchanged** - `f184b856` (test)

**Plan metadata:** pending (this commit)

## Files Created/Modified
- `QuestBoard.IntegrationTests/Tests/CalendarSubscriptionQuestFeedTests.cs` - six new facts appended across three tasks; `SeedEventAsync`, `SeedEventSignupAsync` and `RemoveMembershipAsync` added as private seeding/mutation helpers
- `QuestBoard.UnitTests/Services/CalendarSubscriptionQuestRecheckTests.cs` - new file, four facts driving `CalendarSubscriptionService` directly with fakes for every dependency except the real `CalendarFeedWriter`

## Decisions Made
- Simulated the "unscoped all-one-shot-boards" mutation as `Enumerable.Range(1, 1000)` rather than inventing a second production query, since no such independent query exists in the codebase to swap in
- Routed Task 1's second board-type fact through Dungeon Master ownership rather than a signup row, per the plan's explicit "deliberately the same scenario from a different route in" framing
- Made Task 3's non-qualifying quest fail to qualify by omitting a signup row (rather than seeding-then-un-finalizing), the simpler of the plan's two named alternatives

## Deviations from Plan

None -- plan executed exactly as written across all three tasks.

## Issues Encountered

None. `dotnet test` (with `DOTNET_USE_POLLING_FILE_WATCHER=1`) reproduced exactly the one pre-existing, documented, out-of-scope failure noted in this session's environment context (`CalendarSubscriptionStaticGuardTests.NoPlanningOrTrackingReference_ReachedTheSourceTree`, owned by plan 85-06, caused by the Linux apphost binary colliding with the project-folder name its repo-root resolver walks up to find) -- 525/525 `QuestBoard.UnitTests` facts passed and 833/834 `QuestBoard.IntegrationTests` facts passed (19 of which are `CalendarSubscriptionQuestFeedTests`' own: the tracer's one, plan 85-03's six, plan 85-04's six, and this plan's six).

## Verification

- `dotnet test QuestBoard.IntegrationTests --filter CalendarSubscriptionQuestFeedTests` exits 0 with 19 facts
- `dotnet test QuestBoard.UnitTests --filter CalendarSubscriptionQuestRecheckTests` exits 0 with 4 facts
- `dotnet test` exits 0 for the whole solution except the one pre-existing, documented, out-of-scope failure noted above
- `git status --porcelain` names exactly two files across this plan's three commits, both test files, no production source file
- `grep -c 'Quest' QuestBoard.Domain/Services/CalendarSubscriptionService.cs` is unchanged from plan 85-02's value (15) -- confirmed via `git diff --stat` showing no output on that file after every mutation check was reverted
- `grep -rnE '\b(D-[0-9]{2}|QUESTFEED-[0-9]{2}|CALFEED-[0-9]{2}|Phase [0-9]{2}|85-0[0-9])\b'` returns no match in either `QuestBoard.IntegrationTests` or `QuestBoard.UnitTests`
- Three mutation checks were run once against `QuestBoard.Domain/Services/CalendarSubscriptionService.cs` and reverted, each confirmed to turn its named fact(s) red:
  1. Replacing `oneShotGroupIds` with `memberGroupIds` (the full membership set, dropping the `BoardType.OneShot` filter) -> both board-type facts failed (the campaign quest became present)
  2. Replacing `oneShotGroupIds` with `Enumerable.Range(1, 1000)` (an unscoped, membership-independent set) -> the non-member fact failed (the non-member board's quest became present)
  3. Removing the quest second-layer re-check entirely -> both `DroppedRow_NeverReachesTheDocument` and `DroppedRow_IsRecordedAsASingleErrorCarryingBothCounts` failed (the foreign row reached the document; no error was logged)
  4. Substituting `e.StartTime ?? TimeOnly.MaxValue` for `e.StartTime` in the merge ordering -> the ordering fact failed (the all-day entry sorted after the day's timed entries)
- The domain service file is confirmed byte-identical to plan 85-04's committed state after every mutation was reverted (`git diff --stat` reports no output)

**Inherited gaps, restated and still open after this plan (not re-argued, not narrowed):** the integration facts run on the EF Core in-memory provider and therefore prove this application's scoping logic rather than relational translation of the predicate -- the manual relational check recorded in `85-VALIDATION.md` remains the compensating control and should be run once before this milestone ships. The real-device subscription check stays open and unclaimed; nothing in this plan observes a client's poll-and-render behaviour.

## User Setup Required

None -- no external service configuration required.

## Next Phase Readiness

This plan closes out the phase's `<threat_model>` -- all three high-severity threats (`T-85-01`, `T-85-03`, `T-85-13`) now carry both a named integration fact and, for `T-85-01`, a unit fact reaching the branch integration tests structurally cannot reach. No production source file was touched by this plan; `QuestRepository.GetFeedQuestsForUserAsync` and `CalendarSubscriptionService.GetFeedAsync` remain exactly as plan 85-02 shipped them, now with their full remaining behavioural surface pinned by tests. Plan 85-06 (the pre-existing static-guard test fix, already scoped and out of this plan's reach) is the only remaining plan in this phase.

No blockers. The known environment-only test-runner limitation (inotify exhaustion under parallel `WebApplicationFactory` hosts without `DOTNET_USE_POLLING_FILE_WATCHER=1`, not observed this run) and the one pre-existing static-guard failure (owned by plan 85-06) remain orthogonal to this plan's code path.

## Self-Check: PASSED

- FOUND: `QuestBoard.IntegrationTests/Tests/CalendarSubscriptionQuestFeedTests.cs`
- FOUND: `QuestBoard.UnitTests/Services/CalendarSubscriptionQuestRecheckTests.cs`
- FOUND: `.planning/phases/85-one-shot-quests-in-the-calendar-feed/85-05-SUMMARY.md`
- FOUND commit `719d4af1` (Task 1)
- FOUND commit `e929a529` (Task 2)
- FOUND commit `f184b856` (Task 3)

---
*Phase: 85-one-shot-quests-in-the-calendar-feed*
*Completed: 2026-09-18*
