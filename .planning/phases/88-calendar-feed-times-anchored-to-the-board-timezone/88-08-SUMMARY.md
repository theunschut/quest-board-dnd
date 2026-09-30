---
phase: 88-calendar-feed-times-anchored-to-the-board-timezone
plan: 08
subsystem: calendar-feed
tags: [icalendar, last-modified, dtstamp, sequence, byte-pins, gap-closure]

requires:
  - phase: 88
    plan: 05
    provides: "CalendarFeedEntry.Sequence and LastRevisedAt published as SEQUENCE and DTSTAMP by the writer"
provides:
  - "LAST-MODIFIED equal to DTSTAMP, on the line directly after it, on the timed and the all-day VEVENT branch"
  - "The writer's revision contract pinned byte for byte at a non-default sequence and revision time"
  - "A zone guard whose service facts fail if the entry handed to the writer carries the creation time or a constant sequence number"
  - "Live reschedule and event tracer facts that see LAST-MODIFIED equal to DTSTAMP on real fetches"
affects: [88-09, phase-88-verification]

gap_closure: true
gap_ids: [G-88-4]
plan_head_before: 73f26e48341572210100185015f6c9b087d8b17b

actuals:
  tokens: 9000
  tasks: 2
  commits: 2

tech-stack:
  added: []
  patterns:
    - "One computed stamp string feeds both DTSTAMP and LAST-MODIFIED so the two cannot disagree"
    - "Whole-block byte pins use a non-default sequence number and revision time so a fallback to either default fails"

key-files:
  created: []
  modified:
    - QuestBoard.Domain/Services/CalendarFeedWriter.cs
    - QuestBoard.UnitTests/Services/CalendarFeedWriterTests.cs
    - QuestBoard.UnitTests/Services/CalendarFeedBoardZoneGuardTests.cs
    - QuestBoard.IntegrationTests/Tests/CalendarSubscriptionQuestFeedTests.cs
    - QuestBoard.IntegrationTests/Tests/CalendarSubscriptionFeedTests.cs

key-decisions:
  - "LAST-MODIFIED is emitted, equal to DTSTAMP, directly after it, on both branches (recorded choice in the plan)"
  - "The stamp is computed inline once per branch rather than through a shared helper, so each branch visibly writes both lines"

patterns-established:
  - "The writer stays the single place that turns an entry's Sequence and LastRevisedAt into SEQUENCE, DTSTAMP and LAST-MODIFIED"

requirements-completed: [CALTZ-13, CALTZ-15, CALTZ-16]

coverage:
  - id: D1
    description: "Every VEVENT, timed and all-day, carries LAST-MODIFIED directly after DTSTAMP with the same UTC value; both whole blocks are pinned byte for byte at sequence 4 revised 2026-09-25 08:15:30 UTC"
    requirement: "CALTZ-16"
    verification:
      - kind: unit
        ref: "QuestBoard.UnitTests/Services/CalendarFeedWriterTests.cs#Write_TimedEntry_EmitsTheExactEventBlock"
        status: pass
      - kind: unit
        ref: "QuestBoard.UnitTests/Services/CalendarFeedWriterTests.cs#Write_AllDayEntry_EmitsTheExactEventBlock"
        status: pass
      - kind: unit
        ref: "QuestBoard.UnitTests/Services/CalendarFeedWriterTests.cs#Write_EveryEntry_CarriesALastModifiedLineEqualToItsStamp"
        status: pass
    human_judgment: false
  - id: D2
    description: "Each entry writes its own revision as SEQUENCE, a never-edited entry writes 1, and a revision of 0 or below is written as 1"
    requirement: "CALTZ-13"
    verification:
      - kind: unit
        ref: "QuestBoard.UnitTests/Services/CalendarFeedWriterTests.cs#Write_EachEntry_EmitsItsOwnRevisionAsTheSequenceNumber"
        status: pass
      - kind: unit
        ref: "QuestBoard.UnitTests/Services/CalendarFeedWriterTests.cs#Write_NeverEditedEntry_StartsAtSequenceOne"
        status: pass
      - kind: unit
        ref: "QuestBoard.UnitTests/Services/CalendarFeedWriterTests.cs#Write_RevisionBelowOne_IsWrittenAsSequenceOne"
        status: pass
    human_judgment: false
  - id: D3
    description: "The same entry rendered before and after a revision keeps its UID, raises SEQUENCE, moves DTSTAMP and LAST-MODIFIED later, and differs in no other line than the revision lines and the moved times"
    requirement: "CALTZ-15"
    verification:
      - kind: unit
        ref: "QuestBoard.UnitTests/Services/CalendarFeedWriterTests.cs#Write_SameEntryBeforeAndAfterARevision_DiffersOnlyInItsRevisionLinesAndMovedTimes"
        status: pass
    human_judgment: false
  - id: D4
    description: "The zone guard proves the service hands the writer the quest's stored revision and last-revised time, never its creation time, and the real-writer fact finds SEQUENCE:4 with DTSTAMP and LAST-MODIFIED at the revision time"
    requirement: "CALTZ-16"
    verification:
      - kind: unit
        ref: "QuestBoard.UnitTests/Services/CalendarFeedBoardZoneGuardTests.cs#GetFeedAsync_RealWriterUnderADegradedClock_DeclaresUtcThroughTheSamePath"
        status: pass
      - kind: unit
        ref: "QuestBoard.UnitTests/Services/CalendarFeedBoardZoneGuardTests.cs#GetFeedAsync_NonDefaultBoardClock_HandsTheWriterThatZoneAndAnUnshiftedEntry"
        status: pass
    human_judgment: false
  - id: D5
    description: "The live reschedule fact and the live event tracer fact find a LAST-MODIFIED line equal to the entry's DTSTAMP"
    requirement: "CALTZ-16"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Tests/CalendarSubscriptionQuestFeedTests.cs#Feed_RescheduledQuest_UpdatesInPlaceWithinTheWindowAndDisappearsOutsideIt"
        status: pass
    human_judgment: false

duration: 15min
completed: 2026-09-30
status: complete
---

# Phase 88 Plan 08: Feed Writer Revision Contract Pinned Summary

**LAST-MODIFIED equal to DTSTAMP on every VEVENT from one computed stamp string, with the writer's sequence, stamp and whole-block pins rewritten byte for byte and the zone guard rebuilt so a fall back to the creation time or a constant sequence number fails the build**

## Performance

- **Duration:** about 15 min
- **Completed:** 2026-09-30
- **Tasks:** 2
- **Files modified:** 5 (0 created, 5 modified)

## Accomplishments

- Both `AppendTimedEvent` and `AppendAllDayEvent` compute the stamp once and write `DTSTAMP:` then `LAST-MODIFIED:` from that string, so the two lines cannot disagree. Every other line, the order and the folding path are unchanged.
- The two whole-block pins are now the exact ten-line blocks at sequence 4 revised 2026-09-25 08:15:30 UTC, still whole-block `Contain(string.Join("\r\n", ...) + "\r\n")` assertions.
- New writer facts: each entry's own sequence (3 and 7 in one document), never-edited entry at 1, a theory over 0 and -5 that floors to 1, LAST-MODIFIED equal to DTSTAMP on a mixed document, a before-and-after-a-revision render whose differing lines are only DTSTAMP, LAST-MODIFIED, SEQUENCE, DTSTART and DTEND, and an empty document with no LAST-MODIFIED line.
- The zone guard's quest fixture carries revision 4 and an unspecified-kind revision time of 2026-09-18 10:11:12, distinct from its creation time of 2026-09-01. Both capturing-writer facts assert the entry's `Sequence` and `LastRevisedAt`; the real-writer fact finds `SEQUENCE:4`, `DTSTAMP:20260918T101112Z`, `LAST-MODIFIED:20260918T101112Z` and no `DTSTAMP:20260901T000000Z`. The October clock-change fact builds revisions 2 and 5 and ties each `SEQUENCE` to its own UID block. Every zone assertion is untouched.
- The live reschedule fact (both fetches) and the live event tracer fact find LAST-MODIFIED equal to DTSTAMP.
- `Write_SameEntryTwice_ProducesByteIdenticalOutputAcrossAClockChange` is untouched and passes.

## Test totals (Windows) for 88-09 to compare against Linux

- `CalendarFeedWriterTests` alone: Total 66 (was 60 before this plan, +6)
- Calendar unit filter as the plan states it (`CalendarFeed|CalendarSubscriptionQuestRecheck|AmbientClockSeamTests`): **142 passed, 0 failed**
- Calendar unit filter as 88-05 measured it (adds `FeedRevision`): **183 passed, 0 failed** (was 177)
- Calendar integration filter (`CalendarSubscriptionFeedTests|CalendarSubscriptionQuestFeedTests|CalendarFeedBoardZoneHttpTests`): **52 passed, 0 failed**
- Full unit project: 783 passed, 0 failed (was 777)

## Task Commits

1. **Task 1: LAST-MODIFIED and the writer's revision pins rewritten byte for byte** - `f91d5d6c` (feat)
2. **Task 2: Zone guard and live facts pin the revision, never the creation time** - `a5542ac5` (test)

**Plan metadata:** recorded in the docs commit that follows this summary.

## Files Created/Modified

- `QuestBoard.Domain/Services/CalendarFeedWriter.cs` - LAST-MODIFIED written after DTSTAMP from the same stamp string, both branches
- `QuestBoard.UnitTests/Services/CalendarFeedWriterTests.cs` - `MakeEntry` takes a sequence; rewritten sequence, whole-block and stamp facts; six new facts
- `QuestBoard.UnitTests/Services/CalendarFeedBoardZoneGuardTests.cs` - revision-bearing fixture, revision assertions, per-UID sequence check, extended class comment
- `QuestBoard.IntegrationTests/Tests/CalendarSubscriptionQuestFeedTests.cs` - LAST-MODIFIED equals DTSTAMP on both reschedule fetches
- `QuestBoard.IntegrationTests/Tests/CalendarSubscriptionFeedTests.cs` - LAST-MODIFIED equals DTSTAMP on the event tracer fact

## Decisions Made

- Emit LAST-MODIFIED equal to DTSTAMP directly after it, as the plan recorded.
- Computed the stamp inline in each branch instead of a shared helper. A helper would have left a single `LAST-MODIFIED:` line in the writer, and the plan's acceptance check expects it in both branches.

## Deviations from Plan

None - plan executed exactly as written. The empty-document LAST-MODIFIED check was added as a sibling fact (the plan allowed either), which is what brings the writer fact count up by six as the acceptance criterion requires.

## Issues Encountered

A combined shell command that ran several `grep -c` checks followed by the commit hung in the background (the shell hook wrapping grep stalled). The commit had not run; I killed nothing that mattered, re-ran the acceptance counts through the search tool and committed in a separate call. No files were affected.

## Known Stubs

None.

## Threat Flags

None. The change adds one property line that duplicates an existing value; no new endpoint, auth path or trust boundary.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- Ready for 88-09. The Windows totals above are the numbers to compare against the Linux run.
- Gap G-88-4 and CALTZ-09 are not marked resolved or complete here, as instructed; the orchestrator and verify-work own that.
- 88-06 changes how the reader's availability answer moves DTSTAMP through the entry's revised-at input; the writer already publishes whatever `LastRevisedAt` it is handed, so no writer change is needed for it.

## Self-Check: PASSED

- Modified files verified present; commits `f91d5d6c` and `a5542ac5` exist on `worktree-agent-p88-08-1790785708` and `git rev-list --count` from the recorded base is 2.
- `LAST-MODIFIED:` appears on 2 lines in the writer; `SEQUENCE:4` appears in both whole-block pins; `Write_SameEntryTwice_ProducesByteIdenticalOutputAcrossAClockChange` appears once; `FeedRevisedAt` and `SEQUENCE:4` appear in the guard file; `LAST-MODIFIED` appears in both integration files.
- No planning identifiers added to source comments or string literals; all five edited files keep CRLF line endings.

---
*Phase: 88-calendar-feed-times-anchored-to-the-board-timezone*
*Completed: 2026-09-30*
