---
phase: 85-one-shot-quests-in-the-calendar-feed
plan: 04
subsystem: api
tags: [integration-tests, ef-core, calendar-feed, quest-board, rfc5545]

requires:
  - phase: 85-one-shot-quests-in-the-calendar-feed
    plan: 03
    provides: "GetFeedQuestsForUserAsync on QuestRepository, the seat-or-Dungeon-Master disjunction, and CalendarSubscriptionQuestFeedTests with SeedQuestAsync/SeedPlayerSignupAsync as the seeding helpers this plan extends"
provides:
  - "Six behavioural facts on CalendarSubscriptionQuestFeedTests pinning every disappearance route (un-finalize, delete, seat withdrawn, date moved outside the window), the in-place update of a rescheduled quest under a byte-identical identifier, and both bounds of the shared rolling window read from the running host's own configuration"
  - "MutateQuestAsync, DeleteQuestAsync and DeleteSignupAsync added to CalendarSubscriptionQuestFeedTests as the mutation-half of the seeding helper set, reusable by the remaining phase 85 plan"
affects: [85-05-one-shot-quests-in-the-calendar-feed]

actuals:
  tokens: 5300
  tasks: 2
  commits: 1

tech-stack:
  added: []
  patterns:
    - "Two-fetch transition facts (seed, fetch, assert present, mutate, fetch, assert absent) against one subscription in one test body, rather than two separately-seeded end states, so a predicate that lost the clause under test cannot pass by accident against a feed that never updates"
    - "Identifier byte-equality captured from a first fetch and compared against a second, rather than re-derived from the entity id, to prove a reschedule updates an existing calendar entry in place instead of accumulating a duplicate"

key-files:
  modified:
    - QuestBoard.IntegrationTests/Tests/CalendarSubscriptionQuestFeedTests.cs

key-decisions:
  - "Split the un-finalize and delete cases into two separate facts rather than one, since each needs its own board/quest pair to keep the before/after transition isolated and unambiguous"
  - "The un-finalize fact's mutation (clear IsFinalized and FinalizedDate together) was cross-checked against QuestRepository.OpenQuestAsync, the actual production code path for sending a quest back to voting -- confirming the seeded mutation matches real application behavior rather than an invented shape"
  - "Window-fact board name shortened to 'Quest Feed Window Board' (from an initially longer name) after the first test run revealed RFC 5545 line folding splitting a plain Contain() assertion's expected text across two physical lines -- same class of issue documented in plan 85-03's summary, fixed the same way"
  - "The three required mutation checks (finalized-state clause, seat operand, window clause) were run directly against the shipped QuestRepository.cs predicate, each confirmed red, then reverted via `git checkout --` on that single file; the finalized-state-clause check removes the whole `q.IsFinalized && q.FinalizedDate != null` conjunct rather than only the boolean flag, since the un-finalize fact clears both fields together and either sub-clause alone still excludes the row defensively"

patterns-established: []

requirements-completed: [QUESTFEED-13, QUESTFEED-14]

coverage:
  - id: D1
    description: "Un-finalizing a quest removes it from the very next fetch, with no cancellation status property anywhere in the document"
    requirement: "QUESTFEED-13"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Tests/CalendarSubscriptionQuestFeedTests.cs#Feed_UnfinalizedQuest_DisappearsFromTheVeryNextFetch"
        status: pass
    human_judgment: false
  - id: D2
    description: "Deleting a quest removes it from the very next fetch"
    requirement: "QUESTFEED-13"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Tests/CalendarSubscriptionQuestFeedTests.cs#Feed_DeletedQuest_DisappearsFromTheVeryNextFetch"
        status: pass
    human_judgment: false
  - id: D3
    description: "Losing the confirmed seat (the signup row deleted outright) removes the quest from the very next fetch"
    requirement: "QUESTFEED-13"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Tests/CalendarSubscriptionQuestFeedTests.cs#Feed_QuestWhoseReaderSignupRowIsDeleted_DisappearsFromTheVeryNextFetch"
        status: pass
    human_judgment: false
  - id: D4
    description: "Moving a finalized date inside the window updates the entry in place under its unchanged, byte-identical identifier; moving it outside the window removes the entry"
    requirement: "QUESTFEED-13"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Tests/CalendarSubscriptionQuestFeedTests.cs#Feed_RescheduledQuest_UpdatesInPlaceWithinTheWindowAndDisappearsOutsideIt"
        status: pass
    human_judgment: false
  - id: D5
    description: "A quest finalized just inside either bound of the shared rolling window appears, and one just outside either bound does not, with the bounds read from the running host's configuration"
    requirement: "QUESTFEED-14"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Tests/CalendarSubscriptionQuestFeedTests.cs#Feed_QuestJustInsideTheBackwardWindowBound_AppearsWhileOneJustOutsideDoesNot"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Tests/CalendarSubscriptionQuestFeedTests.cs#Feed_QuestJustInsideTheForwardWindowBound_AppearsWhileOneJustOutsideDoesNot"
        status: pass
    human_judgment: false

duration: 25min
completed: 2026-09-18
status: complete
---

# Phase 85 Plan 04: Disappearance Routes and the Shared Rolling Window Summary

**Six new integration facts on the live anonymous calendar feed address prove every route by which a one-shot quest stops qualifying -- un-finalized, deleted, seat withdrawn, or rescheduled outside the window -- disappears silently from the very next fetch with no tombstone, that a reschedule within the window updates the entry in place under a byte-identical identifier, and that both bounds of the shared rolling window are honored using dates derived from the running host's own configuration.**

## Performance

- **Duration:** ~25 min
- **Completed:** 2026-09-18T16:25:29Z
- **Tasks:** 2 (both `type="auto"`)
- **Files modified:** 1

## Accomplishments
- `Feed_UnfinalizedQuest_DisappearsFromTheVeryNextFetch` -- clearing `IsFinalized`/`FinalizedDate` together (matching `QuestRepository.OpenQuestAsync`'s real un-finalize path) removes the quest at the next fetch with no `STATUS` property anywhere
- `Feed_DeletedQuest_DisappearsFromTheVeryNextFetch` -- deleting the quest row outright removes it at the next fetch
- `Feed_QuestWhoseReaderSignupRowIsDeleted_DisappearsFromTheVeryNextFetch` -- deleting the signup row (withdrawing from a quest), distinct from flipping the confirmed-seat flag on a surviving row, removes the quest at the next fetch
- `Feed_RescheduledQuest_UpdatesInPlaceWithinTheWindowAndDisappearsOutsideIt` -- a three-fetch fact: moving the finalized date within the window updates `DTSTART` while the `UID` line captured from the first fetch stays byte-identical to the second; moving it outside the window removes the entry entirely
- `Feed_QuestJustInsideTheBackwardWindowBound_AppearsWhileOneJustOutsideDoesNot` and `Feed_QuestJustInsideTheForwardWindowBound_AppearsWhileOneJustOutsideDoesNot` -- both bounds proven with dates derived from `CalendarFeedOptions.MonthsBack`/`MonthsAhead` read out of the running host, each placed a full day clear of the boundary; the forward fact additionally asserts via reflection that `CalendarFeedOptions` exposes exactly one backward and one forward bound, so no second pair of window knobs exists for quests
- `MutateQuestAsync`, `DeleteQuestAsync` and `DeleteSignupAsync` added as the mutation half of the class's seeding helper set
- Three one-shot mutation checks run and reverted against `QuestRepository.GetFeedQuestsForUserAsync` -- each confirmed the named fact(s) go red for the reason claimed (see Verification below)

## Task Commits

Both tasks were committed together as one commit, since Task 2's window facts and Task 1's date-move fact share the same "derive dates from configuration, place clear of the bound" reasoning and the file's helper additions serve both:

1. **Task 1 + Task 2: Disappearance routes and the shared rolling window** - `b03e53dc` (test)

**Plan metadata:** pending (this commit)

## Files Created/Modified
- `QuestBoard.IntegrationTests/Tests/CalendarSubscriptionQuestFeedTests.cs` - six new facts appended; `MutateQuestAsync`, `DeleteQuestAsync` and `DeleteSignupAsync` added as private mutation helpers

## Decisions Made
- Un-finalize and delete written as two separate facts (plan left this open) rather than combined into one, since each needs its own board/quest pair to keep its own before/after transition isolated
- The un-finalize mutation's shape (clear `IsFinalized` and `FinalizedDate` together) was verified against `QuestRepository.OpenQuestAsync` -- the actual production code that sends a quest back to voting -- confirming the fact matches real application behavior rather than an assumption
- Shortened one board name after RFC 5545 line folding broke a plain `Contain()` assertion (see Deviations)
- The finalized-state-clause mutation check removes the whole `q.IsFinalized && q.FinalizedDate != null` conjunct rather than only the boolean flag; since the un-finalize fact clears both fields together, removing either sub-clause alone leaves the other still excluding the row defensively and the fact would stay green. Removing the whole conjunct produces a `NullReferenceException` on the now-unguarded `.Value` dereference, which fails the fact for the reason the acceptance criterion describes -- the clause is proven load-bearing even though the failure mode is an exception rather than a clean assertion mismatch

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] The backward/forward window facts' board name triggered RFC 5545 line folding, breaking their own SUMMARY assertions**
- **Found during:** first run of the window facts (Task 2)
- **Issue:** `SUMMARY:[Quest Feed Backward Window Board] Quest Feed Backward Inside Session` is 77 octets, over RFC 5545's 75-octet fold limit (the writer's real, correct folding behavior splits the title mid-line). Three of the four seeded titles across both window facts exceeded the limit.
- **Fix:** Shortened the board name from `Quest Feed Backward Window Board`/`Quest Feed Forward Window Board` to `Quest Feed Window Board` for both facts, bringing every combined `SUMMARY` line to 67-69 octets.
- **Files modified:** `QuestBoard.IntegrationTests/Tests/CalendarSubscriptionQuestFeedTests.cs`
- **Commit:** `b03e53dc`

**2. [Rule 1 - Bug] Two in-progress helper comments referenced a plan/phase identifier, violating CLAUDE.md's "no planning IDs in source comments" rule**
- **Found during:** the plan's own acceptance-criteria grep check (`grep -rnE '...85-0[0-9]...' QuestBoard.IntegrationTests`)
- **Issue:** A draft comment on `DeleteSignupAsync` and its call site referenced "85-03's waitlist fact" by plan number.
- **Fix:** Reworded both comments to "the waitlist-promotion fact elsewhere in this file" -- same meaning, no planning identifier.
- **Files modified:** `QuestBoard.IntegrationTests/Tests/CalendarSubscriptionQuestFeedTests.cs`
- **Commit:** `b03e53dc`

---

**Total deviations:** 2 auto-fixed (both Rule 1, test-data/comment-only, no functional impact).
**Impact on plan:** None on scope or correctness.

## Issues Encountered

None beyond the two auto-fixed issues above. Full-solution `dotnet test` (with `DOTNET_USE_POLLING_FILE_WATCHER=1`) reproduced exactly the one pre-existing, documented, out-of-scope failure (`CalendarSubscriptionStaticGuardTests.NoPlanningOrTrackingReference_ReachedTheSourceTree`, owned by plan 85-06) -- 521/521 `QuestBoard.UnitTests` facts passed and 827/828 `QuestBoard.IntegrationTests` facts passed (13 of which are this file's, the tracer's one plus 85-03's six plus this plan's six).

## Verification

- `dotnet test QuestBoard.IntegrationTests --filter CalendarSubscriptionQuestFeedTests` exits 0 with 13 facts (the tracer's one, plan 85-03's six, and this plan's six)
- `dotnet test` exits 0 for the whole solution except the one pre-existing, documented, out-of-scope failure noted above
- `git status --porcelain` names exactly one file, and it is a test file
- `grep -rnE '\b(D-[0-9]{2}|QUESTFEED-[0-9]{2}|CALFEED-[0-9]{2}|Phase [0-9]{2}|85-0[0-9])\b' QuestBoard.IntegrationTests` returns no match
- Three mutation checks were run once against `QuestBoard.Repository/QuestRepository.cs` and reverted, each turning its named fact(s) red:
  1. Removing the entire finalized-state conjunct (`q.IsFinalized && q.FinalizedDate != null`) → `Feed_UnfinalizedQuest_DisappearsFromTheVeryNextFetch` failed (an `InvalidOperationException` on the now-unguarded `FinalizedDate.Value` dereference for the un-finalized row)
  2. Removing the seat operand (`q.PlayerSignups.Any(...)`), leaving only the Dungeon Master operand → `Feed_QuestWhoseReaderSignupRowIsDeleted_DisappearsFromTheVeryNextFetch` failed at its own first assertion (the before-fetch no longer showed the quest present, since the reader holds no `DungeonMasterId` match)
  3. Removing the window clause (`q.FinalizedDate.Value >= windowStart && q.FinalizedDate.Value <= windowEnd`) → `Feed_RescheduledQuest_UpdatesInPlaceWithinTheWindowAndDisappearsOutsideIt`, `Feed_QuestJustInsideTheBackwardWindowBound_AppearsWhileOneJustOutsideDoesNot` and `Feed_QuestJustInsideTheForwardWindowBound_AppearsWhileOneJustOutsideDoesNot` all failed (the third fetch's disappearance and both "outside" quests' absences turned into unexpected presences)
- The repository file (`QuestBoard.Repository/QuestRepository.cs`) is confirmed clean (`git diff --stat` reports no output) after every mutation was reverted -- the shipped code is byte-identical to what plan 85-02 committed

**Inherited gap restated, not closed:** every fact here runs on the EF Core in-memory provider, proving this application's scoping logic rather than relational translation. The full reasoning for deferring that gap a third time is in `85-02-PLAN.md`'s `<verification>` block; it is not re-argued or narrowed here. The real-device subscription check remains open and unclaimed.

## User Setup Required

None -- no external service configuration required.

## Next Phase Readiness

Plan 85-05 can extend this same test file (board-type narrowing, tenant isolation, ordering, and the second-layer re-check's drop-and-log branch) against the same `GetFeedQuestsForUserAsync` wiring without any further architectural change. `MutateQuestAsync`, `DeleteQuestAsync` and `DeleteSignupAsync` are now available alongside `SeedQuestAsync`/`SeedPlayerSignupAsync` for any fact that needs to change or remove a seeded row mid-test.

No blockers. The known environment-only test-runner limitation (inotify exhaustion under parallel `WebApplicationFactory` hosts without `DOTNET_USE_POLLING_FILE_WATCHER=1`) and the one pre-existing static-guard failure (owned by plan 85-06) remain orthogonal to this plan's code path.

## Self-Check: PASSED

- FOUND: `QuestBoard.IntegrationTests/Tests/CalendarSubscriptionQuestFeedTests.cs`
- FOUND: `.planning/phases/85-one-shot-quests-in-the-calendar-feed/85-04-SUMMARY.md`
- FOUND commit `b03e53dc` (Task 1 + Task 2)

---
*Phase: 85-one-shot-quests-in-the-calendar-feed*
*Completed: 2026-09-18*
