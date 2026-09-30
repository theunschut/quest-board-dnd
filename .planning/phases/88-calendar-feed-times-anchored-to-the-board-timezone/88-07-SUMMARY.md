---
phase: 88-calendar-feed-times-anchored-to-the-board-timezone
plan: 07
subsystem: calendar-feed
tags: [icalendar, sequence, dtstamp, etag, integration-tests, quest-controller, gap-closure]

requires:
  - phase: 88
    plan: 05
    provides: "Store-owned per-entry revision (FeedRevision, FeedRevisedAt) raised on every save that changes a feed-visible quest field"
provides:
  - "End-to-end proof, through the real QuestController and the anonymous feed, that Open then Finalize (another date and the same date) and a title edit each reach the reader as a strictly newer revision under the same UID"
  - "End-to-end proof that a description-only quest save leaves the whole document byte-identical and an earlier ETag answers 304"
affects: [88-09, phase-88-verification]

gap_closure: true
gap_ids: [G-88-4]
plan_head_before: 73f26e48341572210100185015f6c9b087d8b17b

actuals:
  tokens: 4200
  tasks: 1
  commits: 1

tech-stack:
  added: []
  patterns:
    - "Feed facts read single lines out of the entry's own block (UID, SEQUENCE, DTSTART, DTSTAMP, SUMMARY) and compare whole documents only across fetches, so extra writer properties cannot break them"
    - "Quest seeded through the seeding context with a fixed past CreatedAt, so the first stamp is a known value later revisions must move past"

key-files:
  created:
    - QuestBoard.IntegrationTests/Tests/CalendarFeedQuestRevisionTests.cs
  modified: []

key-decisions:
  - "The board is the default seeded board (group 1, one-shot), because the authenticated Dungeon Master helper enrols its user there with the Dungeon Master role, which the Open, Finalize and Edit actions need"
  - "The Dungeon Master is also the subscription owner, so the facts exercise the reader-runs-it route with no signup row"
  - "The no-change fact reads the saved quest back through the seeding context with a query-filter bypass, in test code only, to prove the edit really landed before asserting the document did not move"

patterns-established:
  - "A revision fact captures the UTC time truncated to whole seconds before the write, then asserts DTSTAMP is on or after it and after the first fetch's stamp"

requirements-completed: [CALTZ-12, CALTZ-15]

coverage:
  - id: D1
    description: "A finalized one-shot quest that the Dungeon Master reopens disappears from the next fetch, and finalizing it again at a different proposed date brings it back under a byte-identical UID with SEQUENCE:3, the new DTSTART and a DTSTAMP no earlier than the finalize"
    requirement: "CALTZ-12"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Tests/CalendarFeedQuestRevisionTests.cs#Feed_QuestReopenedAndFinalizedAtAnotherDate_ComesBackUnderTheSameUidWithAHigherSequence"
        status: pass
    human_judgment: false
  - id: D2
    description: "Reopening and finalizing again at the same proposed date still returns the entry with SEQUENCE:3, an unchanged DTSTART and a later DTSTAMP"
    requirement: "CALTZ-12"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Tests/CalendarFeedQuestRevisionTests.cs#Feed_QuestReopenedAndFinalizedAtTheSameDate_StillComesBackWithAHigherSequence"
        status: pass
    human_judgment: false
  - id: D3
    description: "Retitling a finalized quest through the edit form returns the entry with the new title, SEQUENCE:2 and a DTSTAMP no earlier than the edit"
    requirement: "CALTZ-15"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Tests/CalendarFeedQuestRevisionTests.cs#Feed_QuestRetitledThroughTheEditForm_GoesOutWithAHigherSequenceAndALaterStamp"
        status: pass
    human_judgment: false
  - id: D4
    description: "A quest edit that changes only the description leaves the reader's whole document and ETag unchanged, and presenting the earlier ETag gets 304 Not Modified with an empty body"
    requirement: "CALTZ-15"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Tests/CalendarFeedQuestRevisionTests.cs#Feed_QuestSaveThatChangesNothingTheFeedShows_LeavesTheDocumentByteIdenticalAndAnswers304"
        status: pass
    human_judgment: false
  - id: D5
    description: "The plan is tests only: no production file changed and the IgnoreQueryFilters allowlist test passes unchanged"
    verification:
      - kind: unit
        ref: "QuestBoard.UnitTests CrossBoardIgnoreQueryFiltersSeamTests (7 passed)"
        status: pass
    human_judgment: false

duration: 15min
completed: 2026-09-30
status: complete
---

# Phase 88 Plan 07: Quest Write Paths Reach the Feed Summary

**Four integration facts drive the real Open, Finalize and Edit quest actions as the quest's Dungeon Master and read the anonymous feed, proving reopen-then-refinalize and retitle each arrive as a higher SEQUENCE with a later DTSTAMP under the same UID, and a description-only save transfers nothing new (identical document, 304 on the old ETag)**

## Performance

- **Duration:** 15 min
- **Tasks:** 1
- **Files modified:** 1 (1 created, no production files)

## Accomplishments

- Reopen then finalize at another proposed date: the entry is absent after Open, then returns under the byte-identical UID line with `SEQUENCE:3`, the new `DTSTART` and a `DTSTAMP` at or after the instant captured before the finalize post and after the seeded first stamp.
- Reopen then finalize at the same proposed date: the entry returns with `SEQUENCE:3`, the unchanged `DTSTART` and a later `DTSTAMP`.
- Retitle through the edit form (antiforgery GET, then the `Quest.*` fields): `SUMMARY` carries the new title, `SEQUENCE:2`, a `DTSTAMP` at or after the edit, same UID.
- Description-only edit: the saved row is read back to confirm the edit landed, the document and ETag are unchanged, and an `If-None-Match` fetch with the earlier tag answers `304 Not Modified` with an empty body.
- Mutation check: with the stamper's feed-change test temporarily forced false (reverted, never committed), three of the four facts failed, so the facts bite; the no-change fact correctly stays green in that state.

## Task Commits

1. **Task 1: Every quest write path reaches the reader's feed as a newer revision, and a no-change save stays byte-identical** - `23b1aedd` (test)

**Plan metadata:** recorded in the docs commit that follows this summary.

_Note: the task is marked `tdd="true"` but the behaviour it pins was built in the preceding plan, so there is no failing-test-first commit; the mutation check above stands in for the RED evidence._

## Test totals (Windows)

- New `CalendarFeedQuestRevisionTests`: 4 passed, 0 failed
- Existing quest controller facts (`QuestFinalizedEditTests`, `QuestControllerAuthorizationRegressionTests`, `QuestFinalizeTests`): 21 passed, 0 failed
- `CrossBoardIgnoreQueryFiltersSeamTests`: 7 passed, 0 failed, allowlist and its count untouched
- Not run here: the full unit and integration projects (this worktree holds one new test file only; siblings change other calendar files in parallel)

## Files Created/Modified

- `QuestBoard.IntegrationTests/Tests/CalendarFeedQuestRevisionTests.cs` - the four facts, with a shared arrange step, post helpers for Open, Finalize and Edit, and single-line accessors over an entry's own block

## Decisions Made

See `key-decisions`. No assertion pins a whole VEVENT block against a literal and none checks for the absence of a `LAST-MODIFIED` line, so the parallel writer change and the availability-driven stamp change cannot break these facts when merged.

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

The first run of the no-change fact failed reading the saved quest back, because the seeding context carries no active board and its tenant filter matched nothing. The read-back now bypasses the filter, in test code only (as the neighbouring quest feed facts already do); no production file and no allowlist changed.

## Known Stubs

None.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- Ready for 88-09. Every write path the operator named (finalize including Open then Finalize again, and quest title edits) now has a controller-to-feed proof.
- Gap G-88-4 and CALTZ-09 are left for verify-work to mark, as instructed. A board rename remains out of scope and unchanged.

## Self-Check: PASSED

- Created file verified on disk: `QuestBoard.IntegrationTests/Tests/CalendarFeedQuestRevisionTests.cs`.
- Commit `23b1aedd` exists on `worktree-agent-p88-07-1790785708`; `git diff --name-only 23b1aedd^..23b1aedd` names only the new test file.
- `/Quest/Open/` and `/Quest/Finalize/` each appear in the test file; no planning identifiers appear in the test source.

---
*Phase: 88-calendar-feed-times-anchored-to-the-board-timezone*
*Completed: 2026-09-30*
