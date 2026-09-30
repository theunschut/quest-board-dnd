---
phase: 88-calendar-feed-times-anchored-to-the-board-timezone
plan: 09
subsystem: calendar-feed
tags: [icalendar, sequence, linux-container, sql-server, migration, architecture-guidance, gap-closure]

requires:
  - phase: 88
    plan: 05
    provides: "FeedRevision and FeedRevisedAt columns, FeedRevisionStamper and the AddFeedEntryRevisions migration"
  - phase: 88
    plan: 08
    provides: "LAST-MODIFIED equal to DTSTAMP and the rewritten byte pins; the Windows totals to compare against Linux"
provides:
  - "The rewritten SEQUENCE, DTSTAMP and LAST-MODIFIED byte pins proven on Linux with the same Total as Windows (187 and 187, zero failed)"
  - "The standing architecture guidance states the revision contract, the known limitations and the change-tracker rule, and names FeedRevisionStamper.cs as high-risk"
  - "88-VALIDATION.md rows for CALTZ-11 to CALTZ-16, measured totals, the Linux and local SQL Server evidence and the queued production re-test"
  - "The migration and its one-time bump proven on the real SQL Server provider"
affects: [phase-88-verification]

gap_closure: true
gap_ids: [G-88-4]
plan_head_before: 4455f4a35a52b8a357cc2bf5d9785e7ae966e9cf

actuals:
  tokens: 1949
  tasks: 3
  commits: 3

tech-stack:
  added: []
  patterns:
    - "Byte-pin proof on Linux: git archive HEAD fed on standard input to a --rm SDK container with no volume mount"
    - "Relational-provider proof of a migration's set-based SQL with read-only sqlcmd queries"

key-files:
  created: []
  modified:
    - .claude/architecture.md
    - .planning/phases/88-calendar-feed-times-anchored-to-the-board-timezone/88-VALIDATION.md

key-decisions:
  - "The architecture paragraph keeps its place and the zone, wall-clock and never-converted sentences untouched; only the constant-sequence sentence and the closing sentence were rewritten"
  - "The CALTZ-11 map row was written with a pending SQL Server mark in the Task 2 commit and turned green in the Task 3 commit, so no row claimed a proof that had not run yet"

patterns-established:
  - "Known limitations of the revision contract live in the standing architecture guidance and in the validation map, worded identically"

requirements-completed: [CALTZ-11, CALTZ-15, CALTZ-16]

coverage:
  - id: D1
    description: "The calendar and revision unit filter reports the same Total on Linux (mcr.microsoft.com/dotnet/sdk:10.0, committed tree, no volume mount) as on Windows, with zero failed"
    requirement: "CALTZ-15"
    verification:
      - kind: unit
        ref: "dotnet test QuestBoard.UnitTests --filter CalendarFeed|CalendarSubscriptionQuestRecheck|AmbientClockSeamTests|FeedRevision (Windows 187, Linux 187)"
        status: pass
    human_judgment: false
  - id: D2
    description: "The architecture guidance states the revision contract, the two known limitations and the change-tracker rule, keeps the wall-clock rule, and carries no planning identifiers"
    requirement: "CALTZ-16"
    verification:
      - kind: other
        ref: ".claude/architecture.md string checks (FeedRevision, FeedRevisedAt, LAST-MODIFIED, FeedRevisionStamper.cs, change tracker, high-risk present; carries SEQUENCE:1 and planning-id patterns absent)"
        status: pass
    human_judgment: true
    rationale: "Whether the wording reads clearly to the next contributor is a judgment; the required statements are present by string check"
  - id: D3
    description: "The full solution suite is green"
    requirement: "CALTZ-16"
    verification:
      - kind: integration
        ref: "dotnet test (787 unit, 951 integration, 0 failed)"
        status: pass
    human_judgment: false
  - id: D4
    description: "AddFeedEntryRevisions applied to the local SQL Server: every pre-existing event and quest reads revision 2 stamped after its creation, and the FeedRevision column default is 1"
    requirement: "CALTZ-11"
    verification:
      - kind: other
        ref: "read-only sqlcmd checks against localhost:1433 database QuestBoard (Events 43, Quests 62; mismatch counts 0 and 0)"
        status: pass
    human_judgment: false
  - id: D5
    description: "A rescheduled entry moves in Google and Apple Calendar after the v5.3.3 deploy (gap G-88-4, UAT test 4)"
    verification: []
    human_judgment: true
    rationale: "Google and Apple fetch from their own servers and phones, and whether a client applies a higher sequence number is client behaviour; queued for /gsd-verify-work 88"

duration: 8min
completed: 2026-09-30
status: complete
---

# Phase 88 Plan 09: Revision Contract Proven on Linux and SQL Server Summary

**The rewritten SEQUENCE, DTSTAMP and LAST-MODIFIED byte pins pass with an identical Total of 187 on Linux and Windows, the AddFeedEntryRevisions migration bumped all 43 events and 62 quests to revision 2 on the local SQL Server, and the architecture guidance now states the revision contract and its known limitations**

## Performance

- **Duration:** about 8 min
- **Started:** 2026-09-30T16:43:57Z
- **Completed:** 2026-09-30T16:52Z
- **Tasks:** 3
- **Files modified:** 2 (0 created, 2 modified)

## Accomplishments

- Linux proof: the committed tree (`git archive HEAD`, written to a temporary archive, fed on standard input to a `--rm` `mcr.microsoft.com/dotnet/sdk:10.0` container, no volume mount, archive deleted afterwards) ran the calendar and revision unit filter to `Passed! Failed: 0, Passed: 187, Total: 187`. The same filter on Windows reported the same 187. No Linux-only failure, so no production or test change was needed.
- Full suite on Windows: 787 unit and 951 integration, 0 failed. The integration filter for the calendar feed and both revision classes measured 69.
- `.claude/architecture.md` calendar-feed paragraph: the constant `SEQUENCE:1` sentence became the revision contract (stored `FeedRevision` on `Events` and `Quests`, raised by `QuestBoardContext` through `FeedRevisionStamper`; `DTSTAMP` and `LAST-MODIFIED` carry `FeedRevisedAt` as a labelled UTC instant; a reader's own answer moves only that reader's stamp; never down or below 1; an unedited entry byte-identical). It states both known limitations (board renames do not re-signal existing entries; session length or board zone changes reach clients with each entry's next revision) and names `CalendarFeedWriter.cs`, `CalendarSubscriptionService.cs` and `FeedRevisionStamper.cs` as guarded and high-risk. The Entity Framework section gained the change-tracker rule. The file keeps its CRLF line endings.
- `88-VALIDATION.md`: map rows for CALTZ-11 to CALTZ-16 with covering tasks, threat refs and commands; quick and integration filters updated with measured totals (187 and 69); the Linux and local SQL Server bullets under Wave 0; the Known limitations note; and a pending manual row for the production reschedule re-test tied to G-88-4 and UAT test 4.
- Local SQL Server: before, no `_AddFeedEntryRevisions` row (newest `20260918080027_AddCalendarSubscriptions`), Events 43, Quests 62. After applying with `dotnet ef database update` (forward only, app not started): newest `20260930161610_AddFeedEntryRevisions`, both columns present and non-null on both tables, `FeedRevision` default `((1))`, zero rows with revision other than 2 or a stamp at or before creation on either table, row counts unchanged, and the plan's automated check prints 0.

## Task Commits

1. **Task 1: The rewritten byte pins pass on Linux** - `8f15847c` (docs)
2. **Task 2: Architecture guidance, validation map, full suite** - `25d0938a` (docs)
3. **Task 3: Migration and one-time bump on the local SQL Server** - `d862392a` (docs)

**Plan metadata:** recorded in the docs commit that follows this summary.

## Files Created/Modified

- `.claude/architecture.md` - revision contract, known limitations, high-risk file list, change-tracker rule
- `.planning/phases/88-calendar-feed-times-anchored-to-the-board-timezone/88-VALIDATION.md` - map rows, totals, Linux and SQL Server evidence, pending manual row, known limitations

## Decisions Made

- Left every zone sentence, the wall-clock definition and the never-converted rule untouched; edited only the constant-sequence sentence and the closing sentence of the paragraph.
- Wrote the CALTZ-11 map row as pending in the Task 2 commit and turned it green in the Task 3 commit, after the SQL Server check had actually run.

## Deviations from Plan

None - plan executed exactly as written. The migration was applied from `QuestBoard.Service/` with `--project ../QuestBoard.Repository` as the operator approved, which is equivalent to the plan's root-relative form.

## Issues Encountered

- The first Bash call that combined several `grep -c` counts hung and was moved to the background; it was discarded and the line-ending and string checks were redone with `node`. No files were affected.
- The Live-bytes row in the Manual-Only table still says `SEQUENCE:1` for quest 12039. It is a historical CALTZ-09 check and the plan said to leave every other line untouched, so it was left as is; after the v5.3.3 deploy that entry reads `SEQUENCE:2` or higher.

## Known Stubs

None.

## Threat Flags

None. No file added a network endpoint, auth path or trust boundary. T-88-21 (only the one migration applied, forward only, every other query a read) and T-88-22 (the token column and all subscription addresses never read, printed or stored) were honoured.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- Gap G-88-4 and CALTZ-09 are not marked resolved or complete here, as instructed. The production re-test (deploy v5.3.3, then check the stale UAT test 4 entry and a rescheduled entry on Google Calendar and the iPhone, attributing fetches through the per-device "Last fetched" value) is queued for `/gsd-verify-work 88` in the human-check of Task 3 and in the pending Manual-Only row of 88-VALIDATION.md.
- No refresh latency figure is promised anywhere.

## Self-Check: PASSED

- `.claude/architecture.md` and `88-VALIDATION.md` exist and were modified; commits `8f15847c`, `25d0938a` and `d862392a` exist and `git rev-list --count` from the recorded base `4455f4a3` is 3.
- `grep 'carries `SEQUENCE:1`'` count in `.claude/architecture.md` is 0; all required strings are present; the planning-identifier pattern count is 0; `renaming a board does not re-signal existing calendar entries` appears in both files.
- Automated SQL check prints 0; Windows and Linux Totals are both 187.

---
*Phase: 88-calendar-feed-times-anchored-to-the-board-timezone*
*Completed: 2026-09-30*
