---
phase: 88-calendar-feed-times-anchored-to-the-board-timezone
plan: 06
subsystem: calendar-feed
tags: [icalendar, sequence, dtstamp, availability, integration-tests, gap-closure]

requires:
  - phase: 88
    plan: 05
    provides: "Store-owned FeedRevision and FeedRevisedAt on events, published as SEQUENCE and DTSTAMP"
provides:
  - "Every event write path (edit form, this-and-future sweep, cancel and restore) proven to reach a reader's feed as a newer revision through the real controllers"
  - "Proof that a save changing nothing the feed shows leaves the document byte-identical and answers 304"
  - "EventFeedRow.AnswerWrittenAt and the service rule that stamps an event entry with the later of the event revision time and the reader's answer time"
affects: [88-07, 88-08, 88-09, phase-88-verification]

gap_closure: true
gap_ids: [G-88-4]
plan_head_before: 73f26e48341572210100185015f6c9b087d8b17b

actuals:
  tokens: 9500
  tasks: 2
  commits: 2

tech-stack:
  added: []
  patterns:
    - "Feed facts read single lines out of an entry block and compare whole blocks only across two fetches, so a line added to every entry later cannot break them"
    - "Seeded rows carry one fixed past instant so a stamp comparison never depends on second-granularity timing"

key-files:
  created:
    - QuestBoard.IntegrationTests/Tests/CalendarFeedEventRevisionTests.cs
    - QuestBoard.UnitTests/Services/CalendarFeedRevisionInputTests.cs
  modified:
    - QuestBoard.Domain/Models/EventFeedRow.cs
    - QuestBoard.Repository/EventSignupRepository.cs
    - QuestBoard.Domain/Services/CalendarSubscriptionService.cs

key-decisions:
  - "A reader's own availability answer raises that reader's DTSTAMP and never the sequence number: the sequence is shared by every reader of the event and must stay one-way, and a per-reader counter cannot be made one-way because withdrawing deletes the answer row"
  - "An event entry's stamp is the later of the event's FeedRevisedAt and the reader's answer row time (UpdatedAt, or CreatedAt when no person has answered)"

requirements-completed: [CALTZ-12, CALTZ-14, CALTZ-15]

coverage:
  - id: D1
    description: "An event edited through the edit form goes out under a byte-identical UID with the sequence one higher and a DTSTAMP no earlier than the edit, for a title, a date and a start time change, and an untouched neighbour keeps a byte-identical entry"
    requirement: "CALTZ-12"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Tests/CalendarFeedEventRevisionTests.cs#Feed_EventEditedThroughTheEditForm_GoesOutWithAHigherSequenceAndALaterStamp"
        status: pass
    human_judgment: false
  - id: D2
    description: "A this-and-future edit raises the edited occurrence and every swept sibling by one with the new start time, while a sibling the sweep skips keeps a byte-identical entry"
    requirement: "CALTZ-12"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Tests/CalendarFeedEventRevisionTests.cs#Feed_ThisAndFutureEdit_RaisesEverySweptSiblingAndLeavesASkippedOneByteIdentical"
        status: pass
    human_judgment: false
  - id: D3
    description: "A cancelled event leaves the feed and a restored one returns under the same UID at sequence 3 with a later stamp"
    requirement: "CALTZ-12"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Tests/CalendarFeedEventRevisionTests.cs#Feed_RestoredEvent_ComesBackUnderTheSameUidWithAHigherSequence"
        status: pass
    human_judgment: false
  - id: D4
    description: "A description-only save leaves the reader's document byte-identical and a fetch presenting the earlier ETag gets 304; an edit that changes the feed invalidates the earlier ETag"
    requirement: "CALTZ-14"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Tests/CalendarFeedEventRevisionTests.cs#Feed_EventSaveThatChangesNothingTheFeedShows_LeavesTheDocumentByteIdenticalAndAnswers304"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Tests/CalendarFeedEventRevisionTests.cs#Feed_EventEditedThroughTheEditForm_InvalidatesTheEarlierEntityTag"
        status: pass
    human_judgment: false
  - id: D5
    description: "A reader's answer moves only that reader's stamp: the marker updates, the sequence stays, another reader's document is byte-identical and 304s on its earlier ETag, and withdrawing then answering again never moves the stamp or sequence backwards"
    requirement: "CALTZ-15"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Tests/CalendarFeedEventRevisionTests.cs#Feed_ReaderChangesTheirOwnAnswer_MovesOnlyThatReadersStampAndNeverTheSequence"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Tests/CalendarFeedEventRevisionTests.cs#Feed_ReaderWithdrawsAndAnswersAgain_KeepsTheSequenceAndNeverMovesTheStampBackwards"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Tests/CalendarFeedEventRevisionTests.cs#Feed_AnswerRowNobodyHasSet_StampsWithTheRowCreationTimeUntilThePersonAnswers"
        status: pass
    human_judgment: false
  - id: D6
    description: "CalendarSubscriptionService builds an event entry's LastRevisedAt as the later of FeedRevisedAt and AnswerWrittenAt and its Sequence from FeedRevision alone; quest entries are unaffected"
    requirement: "CALTZ-15"
    verification:
      - kind: unit
        ref: "QuestBoard.UnitTests/Services/CalendarFeedRevisionInputTests.cs"
        status: pass
    human_judgment: false

duration: 12min
completed: 2026-09-30
status: complete
---

# Phase 88 Plan 06: Event Revision Proof and Availability Stamp Summary

**Every event write path proven through the real controllers to reach the reader's feed as a newer revision, a no-change save proven byte-identical and 304-able, and a reader's own availability answer moving only that reader's DTSTAMP via the later of the event revision time and the answer row time**

## Performance

- **Duration:** 12 min
- **Started:** 2026-09-30T16:25:30Z
- **Completed:** 2026-09-30T16:38:00Z
- **Tasks:** 2
- **Files modified:** 5 (2 created, 3 modified)

## Accomplishments

- A Dungeon Master's edit (title, date or start time), a this-and-future sweep, and a cancel followed by a restore each arrive at the anonymous feed as the same UID with a higher sequence number and a stamp from the moment of the write. The restored entry comes back at `SEQUENCE:3` (cancel is revision 2, restore is revision 3).
- The sweep fact seeds four occurrences, makes one ineligible by giving it its own title, and shows the edited occurrence plus both eligible siblings at `SEQUENCE:2` with the new `DTSTART`, while the skipped sibling's whole entry is byte-identical to the previous fetch.
- A description-only edit through the form leaves the reader's whole document string-equal and the earlier ETag answers 304. An edit that changes what the feed shows makes the earlier ETag miss and returns the new document in full.
- `EventFeedRow.AnswerWrittenAt` is set from `UpdatedAt ?? CreatedAt` in `GetFeedRowsForUserAsync` (predicate, filter bypass and ordering untouched), and the service stamps each event entry with the later of `FeedRevisedAt` and `AnswerWrittenAt`, with the sequence still the event's `FeedRevision` alone.

## Availability decision and accepted cost

A reader's own answer changes the marker in `SUMMARY` (" (maybe)", " (declined)"), so it is a change the feed shows and needs a revision signal. It is carried by the reader's own `DTSTAMP`, not the sequence number: the sequence is stored on the event and published to every reader, so raising it on one vote would give every other member a new ETag and a higher number for a change they cannot see, and a per-reader counter would reset when the answer row is deleted on withdraw, letting a client see the same UID at a lower number and ignore later changes. The answer time is a real instant that only moves forward even when the row is deleted and made again, so the stamp is a safe per-reader carrier (RFC 5546 uses `DTSTAMP` as the tie-breaker at equal sequence numbers). Accepted cost: a client that orders updates only by sequence number may keep a stale marker until the event itself is next revised; re-submitting the same answer moves the stamp while the content stays the same, which is harmless.

## Task Commits

1. **Task 1: Every event write path reaches the reader's feed as a newer revision** - `3d069277` (test)
2. **Task 2: A reader's own availability answer moves only that reader's stamp** - `06de80d0` (feat)

**Plan metadata:** the docs commit that follows this summary.

## Test totals (Windows)

- `CalendarFeedEventRevisionTests`: 10 passed, 0 failed (Task 1 alone added 7: 3 theory cases and 4 facts; Task 2 added 3 facts)
- `CalendarFeedRevisionInputTests` (unit): 4 passed, 0 failed
- Calendar unit filter (`CalendarFeed|CalendarSubscriptionQuestRecheck|AmbientClockSeamTests`): 140 passed, 0 failed
- Integration filter (`CalendarFeedEventRevisionTests|CalendarSubscriptionFeedTests|CalendarSubscriptionQuestFeedTests|EventsControllerIntegrationTests`): 87 passed, 0 failed
- Full unit project: 781 passed, 0 failed
- Full integration project: 947 passed, 0 failed

## Files Created/Modified

- `QuestBoard.IntegrationTests/Tests/CalendarFeedEventRevisionTests.cs` - ten facts over the edit form, sweep, cancel/restore, no-change save, ETag, and availability
- `QuestBoard.UnitTests/Services/CalendarFeedRevisionInputTests.cs` - service revision inputs pinned with a capturing writer
- `QuestBoard.Domain/Models/EventFeedRow.cs` - `AnswerWrittenAt`
- `QuestBoard.Repository/EventSignupRepository.cs` - sets `AnswerWrittenAt` from the signup row
- `QuestBoard.Domain/Services/CalendarSubscriptionService.cs` - later-of rule for the event entry stamp, with a comment explaining the split

All five files in the plan's `files_modified` were changed; no file outside that list was touched and nothing was deleted.

## Decisions Made

See `key-decisions`. No new decisions beyond the plan's recorded choice.

## Deviations from Plan

None - plan executed exactly as written.

## TDD Gate Compliance

Task 2's RED was run locally before the implementation: with `AnswerWrittenAt` added to the row shape but the service not yet using it, `GetFeedAsync_AnswerWrittenAfterTheEventRevision_StampsTheEntryWithTheAnswerTime` failed on its assertion (expected the answer time 2026-09-13, found the event revision time 2026-09-10) and the other three unit facts passed. The plan makes each task a single commit, so the failing state was not committed on its own; there is no separate `test(88-06)` RED commit ahead of the `feat(88-06)` commit. Task 1's facts pass against the 88-05 behaviour by design (they prove it through the controllers).

## Issues Encountered

None. Build and every calendar test were green on the first run after the edits.

## Known Stubs

None.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- Ready for the merge with 88-07 and 88-08. New feed facts assert on SEQUENCE and DTSTAMP values and compare whole entry blocks only across two fetches, never against a literal, so the `LAST-MODIFIED` line 88-08 adds cannot break them. The event stamp is what 88-08 will publish as `LAST-MODIFIED` too, so a reader's answer will move that line with it.
- Gap G-88-4 and CALTZ-09 are left for verify-work to mark, as instructed.

## Self-Check: PASSED

- Created files verified on disk: `CalendarFeedEventRevisionTests.cs`, `CalendarFeedRevisionInputTests.cs`.
- Commits `3d069277` and `06de80d0` exist on `worktree-agent-p88-06-1790785708`.
- Acceptance checks: `AnswerWrittenAt = ` appears once in `EventSignupRepository.cs`; `AnswerWrittenAt` appears in `CalendarSubscriptionService.cs`; no planning identifiers appear in the touched source or test files.

---
*Phase: 88-calendar-feed-times-anchored-to-the-board-timezone*
*Completed: 2026-09-30*
