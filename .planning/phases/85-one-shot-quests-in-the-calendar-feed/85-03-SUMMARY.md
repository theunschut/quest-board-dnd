---
phase: 85-one-shot-quests-in-the-calendar-feed
plan: 03
subsystem: api
tags: [integration-tests, ef-core, calendar-feed, quest-board]

requires:
  - phase: 85-one-shot-quests-in-the-calendar-feed
    plan: 02
    provides: "GetFeedQuestsForUserAsync on QuestRepository, the Quests-rooted seat-or-Dungeon-Master disjunction, and CalendarSubscriptionQuestFeedTests as the tracer fact + seeding helpers this plan extends"
provides:
  - "Six behavioural facts on CalendarSubscriptionQuestFeedTests pinning both routes into the feed (signup seat, Dungeon Master ownership), the single-entry guarantee when a quest satisfies both routes at once, the negative control that the query does not degenerate to board scope, the waitlist-to-confirmed-seat transition, seat-kind parity across Player/Spectator/AssistantDM, and the DungeonMasterSession listing-hide-is-not-an-access-control rule"
  - "SeedQuestAsync and SeedPlayerSignupAsync extended with optional dungeonMasterSession, isSelected and role parameters, reusable by the remaining phase 85 plans"
affects: [85-04-one-shot-quests-in-the-calendar-feed, 85-05-one-shot-quests-in-the-calendar-feed]

actuals:
  tokens: 4000
  tasks: 2
  commits: 1

tech-stack:
  added: []
  patterns:
    - "Count assertions (event-opener count, identifier-occurrence count, title-occurrence count) rather than containment assertions wherever a duplicate emission is the risk being guarded against -- a duplicate is invisible to Contain() but not to a count"
    - "One-shot mutation checks run directly against the shipped repository predicate (edit, run the single named fact, confirm red, revert) as the acceptance-criteria-mandated proof that each fact actually guards the behavior it claims to, rather than trusting the fact's intent"

key-files:
  modified:
    - QuestBoard.IntegrationTests/Tests/CalendarSubscriptionQuestFeedTests.cs

key-decisions:
  - "Committed both tasks' facts in a single commit rather than two, because both tasks extend the same two seeding helpers (SeedQuestAsync, SeedPlayerSignupAsync) with the same parameter additions -- the two tasks' hunks in this file are not independently separable without re-authoring the helper extensions twice"
  - "Shortened the Assistant Dungeon Master seat-kind fact's quest title from the plan's illustrative 'Quest Feed Assistant Dungeon Master Seat Session' to 'Quest Feed Assistant DM Seat Session' after the first test run folded the SUMMARY line at RFC 5545's 75-octet limit, splitting the title across a folded continuation line and breaking a plain Contain() assertion -- this is Rule 1 (bug fix): the original title was long enough that the writer's real, correct RFC 5545 folding behavior broke a test assertion that wasn't accounting for it"

patterns-established: []

requirements-completed: [QUESTFEED-03, QUESTFEED-04, QUESTFEED-05, QUESTFEED-06, QUESTFEED-07]

coverage:
  - id: D1
    description: "A finalized quest the reader owns as Dungeon Master, with no signup row of their own on it, appears in their feed"
    requirement: "QUESTFEED-03"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Tests/CalendarSubscriptionQuestFeedTests.cs#Feed_ServesAFinalizedOneShotQuestTheReaderRunsAsDungeonMaster_WithNoSignupRowAtAll"
        status: pass
    human_judgment: false
  - id: D2
    description: "A finalized quest where the reader is both the Dungeon Master and the holder of a confirmed seat appears exactly once, with exactly one identifier"
    requirement: "QUESTFEED-04"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Tests/CalendarSubscriptionQuestFeedTests.cs#Feed_QuestWhereReaderIsBothDungeonMasterAndHoldsAConfirmedSeat_EmitsExactlyOneEntryWithOneIdentifier"
        status: pass
    human_judgment: false
  - id: D3
    description: "A finalized quest on the reader's own board that they hold no seat on stays out, even alongside a quest they are genuinely seated on"
    requirement: "QUESTFEED-03, QUESTFEED-04 (negative control)"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Tests/CalendarSubscriptionQuestFeedTests.cs#Feed_FinalizedQuestOnTheReadersOwnBoardWithNoSeat_StaysOutAlongsideAQuestTheyAreSeatedOn"
        status: pass
    human_judgment: false
  - id: D4
    description: "A waitlisted signup never reaches the feed, and its promotion off the waitlist reaches the very next fetch"
    requirement: "QUESTFEED-05"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Tests/CalendarSubscriptionQuestFeedTests.cs#Feed_WaitlistedSignup_StaysOutUntilTheSameRowIsPromotedToAConfirmedSeat"
        status: pass
    human_judgment: false
  - id: D5
    description: "A Spectator seat and an Assistant Dungeon Master seat each put a quest in the feed exactly as a Player seat does"
    requirement: "QUESTFEED-06"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Tests/CalendarSubscriptionQuestFeedTests.cs#Feed_AllThreeSignupRoles_ReachTheFeedIdenticallyWhenTheSeatIsConfirmed"
        status: pass
    human_judgment: false
  - id: D6
    description: "A quest flagged as a Dungeon Master session appears for a reader holding a confirmed seat on it"
    requirement: "QUESTFEED-07"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Tests/CalendarSubscriptionQuestFeedTests.cs#Feed_QuestFlaggedAsADungeonMasterSession_StillReachesAReaderHoldingAConfirmedSeat"
        status: pass
    human_judgment: false

duration: 40min
completed: 2026-09-18
status: complete
---

# Phase 85 Plan 03: Both Routes, the Single-Entry Guarantee, and the Seat-Is-the-Authority Rules Summary

**Six new integration facts on the live anonymous calendar feed address pin the Dungeon Master route into the feed, the single-entry guarantee when a quest satisfies both routes at once, a negative control against board-scope leakage, the waitlist-to-confirmed-seat transition, seat-kind parity across all three signup roles, and the DungeonMasterSession listing-hide-is-not-an-access-control rule — each one backed by a one-shot mutation check against the shipped repository predicate proving it actually guards what it claims to.**

## Performance

- **Duration:** ~40 min
- **Completed:** 2026-09-18T16:08:14Z
- **Tasks:** 2 (both `type="auto"`)
- **Files modified:** 1

## Accomplishments
- `Feed_ServesAFinalizedOneShotQuestTheReaderRunsAsDungeonMaster_WithNoSignupRowAtAll` — the Dungeon Master route into the feed, with zero signup rows seeded for the reader
- `Feed_QuestWhereReaderIsBothDungeonMasterAndHoldsAConfirmedSeat_EmitsExactlyOneEntryWithOneIdentifier` — the single-entry guarantee, asserted as three separate counts (event openers, identifier occurrences, title-line occurrences) rather than a containment check, since a duplicate is invisible to `Contain()`
- `Feed_FinalizedQuestOnTheReadersOwnBoardWithNoSeat_StaysOutAlongsideAQuestTheyAreSeatedOn` — the negative control proving the query doesn't degenerate to "every finalized quest on a board I belong to," using two quests in one fetch so a board-scoped predicate fails on the count as well as the absence assertion
- `Feed_WaitlistedSignup_StaysOutUntilTheSameRowIsPromotedToAConfirmedSeat` — a single fact proving both the waitlisted-row exclusion and its promotion transition, by flipping the same row's confirmed-seat flag mid-fact and re-fetching
- `Feed_AllThreeSignupRoles_ReachTheFeedIdenticallyWhenTheSeatIsConfirmed` — Player, Spectator and AssistantDM seats all reaching the feed identically, with the seat-kind values read from the `SignupRole` enum rather than integer literals
- `Feed_QuestFlaggedAsADungeonMasterSession_StillReachesAReaderHoldingAConfirmedSeat` — the accepted-cost rule that the listing-hide flag is not an access control
- `SeedQuestAsync` and `SeedPlayerSignupAsync` extended with optional `dungeonMasterSession`, `isSelected` and `role` parameters (all defaulting to the tracer's original behavior), avoided adding parallel helpers per the plan's artifact instructions
- Six one-shot mutation checks run and reverted against `QuestRepository.GetFeedQuestsForUserAsync` — each one confirmed the named fact goes red for the reason claimed (see Verification below)

## Task Commits

Both tasks were committed together, since they extend the same two seeding helpers with the same parameter additions and the file's diff is not independently separable along the task boundary without re-authoring the helper extensions twice:

1. **Task 1 + Task 2: Both routes into the feed, the single-entry guarantee, and the seat-is-the-authority rules** - `675f3ff0` (test)

**Plan metadata:** pending (this commit)

## Files Created/Modified
- `QuestBoard.IntegrationTests/Tests/CalendarSubscriptionQuestFeedTests.cs` - six new facts appended; `SeedQuestAsync` gained an optional `dungeonMasterSession` parameter; `SeedPlayerSignupAsync` gained optional `isSelected` and `role` parameters; a new `CountOccurrences` helper added alongside the existing `CountVEvents`

## Decisions Made
- Extended the tracer's two seeding helpers with optional parameters rather than adding parallel helpers, exactly as the plan's `<artifacts_this_phase_produces>` section directed
- Committed both tasks as one commit given the shared-helper-extension coupling described above
- Shortened one quest title after discovering RFC 5545 line folding broke a containment assertion (see Deviations)

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] The Assistant Dungeon Master seat-kind fact's quest title triggered RFC 5545 line folding, breaking its own assertion**
- **Found during:** Task 2 verification (first test run of the seat-kind fact)
- **Issue:** `SUMMARY:[Quest Feed Seat Kind Board] Quest Feed Assistant Dungeon Master Seat Session` is 85 octets, over RFC 5545's 75-octet fold limit. The real, correct writer behavior folds the line mid-title with a continuation space, which broke the fact's plain `body.Should().Contain("Quest Feed Assistant Dungeon Master Seat Session")` assertion — the title text itself was split across two physical lines in the wire format.
- **Fix:** Shortened the quest title to `Quest Feed Assistant DM Seat Session` (73 octets with its `SUMMARY:[Board]` prefix), which fits under the fold limit and matches how a real DM-Assistant-role quest title would typically be written in practice.
- **Files modified:** `QuestBoard.IntegrationTests/Tests/CalendarSubscriptionQuestFeedTests.cs`
- **Commit:** `675f3ff0`

Or: no other deviations — every other fact and mutation check ran exactly as the plan specified.

### Auth gates

None — no authentication or external service configuration touched by this plan.

---

**Total deviations:** 1 auto-fixed (Rule 1, test-data-only, no functional impact).
**Impact on plan:** None on scope or correctness — the underlying application behavior (RFC 5545 line folding) is unchanged and correct; only the test's own title string needed to be shorter than the fold limit.

## Issues Encountered

None beyond the RFC 5545 folding discovery above, which was resolved inline. Full-solution `dotnet test` reproduced exactly the one pre-existing, out-of-scope failure documented in this session's environment note (`CalendarSubscriptionStaticGuardTests.NoPlanningOrTrackingReference_ReachedTheSourceTree`, owned by plan 85-06, caused by the Linux apphost binary colliding with the project-folder name its repo-root resolver walks up to find) — 821 of 822 `QuestBoard.IntegrationTests` facts passed, plus all 521 `QuestBoard.UnitTests` facts. No inotify exhaustion occurred this run.

## Verification

- `dotnet test QuestBoard.IntegrationTests --filter CalendarSubscriptionQuestFeedTests` exits 0 with exactly 7 facts (the tracer's one plus these six)
- `dotnet test` exits 0 for the whole solution except the one pre-existing, documented, out-of-scope failure noted above
- `git status --porcelain` names exactly one file, and it is a test file
- `grep -rnE '\b(D-[0-9]{2}|QUESTFEED-[0-9]{2}|CALFEED-[0-9]{2}|Phase [0-9]{2}|85-0[0-9])\b' QuestBoard.IntegrationTests` returns no match
- All six mutation checks were run once against `QuestBoard.Repository/QuestRepository.cs` and reverted, each turning its named fact red:
  1. Removing the Dungeon Master operand (`|| q.DungeonMasterId == userId`) → `Feed_ServesAFinalizedOneShotQuestTheReaderRunsAsDungeonMaster_WithNoSignupRowAtAll` failed (quest absent)
  2. Removing the seat operand (`q.PlayerSignups.Any(...)`) → `Feed_FinalizedQuestOnTheReadersOwnBoardWithNoSeat_StaysOutAlongsideAQuestTheyAreSeatedOn` failed (the seated quest's positive half went absent)
  3. Re-shaping the query into two separately materialized lists (`bySeat`, `byDm`) concatenated in memory (`bySeat.Concat(byDm).ToList()`) → `Feed_QuestWhereReaderIsBothDungeonMasterAndHoldsAConfirmedSeat_EmitsExactlyOneEntryWithOneIdentifier` failed (event-opener count went from 1 to 2)
  4. Adding a seat-kind clause (`&& ps.SignupRole == 0`) → `Feed_AllThreeSignupRoles_ReachTheFeedIdenticallyWhenTheSeatIsConfirmed` failed (Spectator quest went absent)
  5. Removing the confirmed-seat clause (`&& ps.IsSelected`) → `Feed_WaitlistedSignup_StaysOutUntilTheSameRowIsPromotedToAConfirmedSeat` failed (the pre-promotion absence assertion became a false presence)
  6. Adding a `DungeonMasterSession` clause (`&& !q.DungeonMasterSession`) → `Feed_QuestFlaggedAsADungeonMasterSession_StillReachesAReaderHoldingAConfirmedSeat` failed (quest went absent)
- The repository file (`QuestBoard.Repository/QuestRepository.cs`) is confirmed clean (`git diff --stat` reports no output) after every mutation was reverted — the shipped code is byte-identical to what plan 85-02 committed

**Inherited gap restated, not closed:** every fact in this plan runs on the EF Core in-memory provider and therefore proves this application's scoping logic rather than relational translation of the predicate. The full reasoning for deferring that gap a third time is in `85-02-PLAN.md`'s `<verification>` block; it is not re-argued here and it is not narrowed by these facts. The real-device subscription check likewise stays open and unclaimed.

## User Setup Required

None — no external service configuration required.

## Next Phase Readiness

Plans 85-04 and 85-05 can extend this same test file (window edges, disappearance cases, board-type narrowing, tenant isolation) against the same `GetFeedQuestsForUserAsync` wiring without any further architectural change. `SeedQuestAsync` and `SeedPlayerSignupAsync` are now shaped to accept the parameters those plans' facts are likely to need (`dungeonMasterSession`, `isSelected`, `role`), so those plans should extend rather than duplicate them.

No blockers. The known environment-only test-runner limitation (inotify exhaustion under parallel `WebApplicationFactory` hosts, not observed this run) and the one pre-existing static-guard failure (owned by plan 85-06) remain orthogonal to this plan's code path.

## Self-Check: PASSED

- FOUND: `QuestBoard.IntegrationTests/Tests/CalendarSubscriptionQuestFeedTests.cs`
- FOUND: `.planning/phases/85-one-shot-quests-in-the-calendar-feed/85-03-SUMMARY.md`
- FOUND commit `675f3ff0` (Task 1 + Task 2)

---
*Phase: 85-one-shot-quests-in-the-calendar-feed*
*Completed: 2026-09-18*
