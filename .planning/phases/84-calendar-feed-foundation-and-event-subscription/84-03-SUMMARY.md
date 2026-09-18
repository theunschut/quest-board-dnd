---
phase: 84-calendar-feed-foundation-and-event-subscription
plan: 03
subsystem: calendar-feed
tags: [rfc5545, unit-testing, tdd]

requires:
  - phase: 84-calendar-feed-foundation-and-event-subscription
    provides: "Plan 84-02's tracer-slice CalendarFeedWriter (timed VEVENT branch only) and its ICalendarFeedWriter/CalendarFeedEntry/CalendarFeedSource contracts, unchanged by this plan"
provides:
  - "A complete CalendarFeedWriter: all-day branch (DTSTART/DTEND;VALUE=DATE with exclusive next-day end), a single free-text escaper, a single 75-octet character-boundary-safe line folder, the board-prefixed/vote-suffixed SUMMARY, and the calendar-level headers (VERSION, PRODID, CALSCALE, X-WR-CALNAME, X-PUBLISHED-TTL, REFRESH-INTERVAL)"
  - "A 30-fact exact-byte unit suite (CalendarFeedWriterTests.cs) pinning every emitted property, including an enumerated, non-hard-coded invariant guard over BuildUid's stability, distinctness and no-configuration facts"
affects: [84-04, 84-05, 84-06, 84-07, 84-08, 85]

actuals:
  tokens: 7137
  tasks: 3
  commits: 5

tech-stack:
  added: []
  patterns:
    - "Character-boundary-safe UTF-8 line folding: back off the byte-count cut point while the byte at the boundary is a UTF-8 continuation byte, rather than cutting by fixed byte count alone"
    - "RFC 5545 TEXT escaping in a fixed four-step order (backslash, then semicolon, then comma, then newline) so later steps cannot double-escape backslashes the earlier steps introduced"
    - "Enumerated-not-hard-coded invariant test (Enum.GetValues<CalendarFeedSource>()) so a future added source automatically re-proves distinctness rather than needing a matching new test line"

key-files:
  created: []
  modified:
    - QuestBoard.Domain/Services/CalendarFeedWriter.cs
    - QuestBoard.UnitTests/Services/CalendarFeedWriterTests.cs

key-decisions:
  - "Vote-marker branches on Availability alone, never HasAnswered -- a campaign auto-created Yes row renders identically to a chosen Yes, an accepted cost recorded in the file's own comment rather than a third '(no answer)' marker (that option was proposed and dropped per 84-CONTEXT.md D-18)."
  - "Calendar-level headers (X-WR-CALNAME, X-PUBLISHED-TTL, REFRESH-INTERVAL) were implemented alongside task 1's AppendCalendarHeaders rather than deferred to task 2, since the header-emission code path and the branch-selection code path were touched in the same StringBuilder-assembly pass -- task 2's own header tests passed immediately (already-satisfied, not newly red), leaving only the vote-marker suffix as task 2's genuinely new RED fact."

requirements-completed: [CALFEED-09, CALFEED-10, CALFEED-11, CALFEED-12]

coverage:
  - id: D1
    description: "A timed entry emits DTSTART/DTEND exactly one hour apart with no zone marker, and a late start rolls the end's date forward rather than clamping at midnight"
    requirement: CALFEED-09
    verification:
      - kind: unit
        ref: "QuestBoard.UnitTests/Services/CalendarFeedWriterTests.cs#Write_TimedEntry_EmitsStartAndEndOneHourApart"
        status: pass
      - kind: unit
        ref: "QuestBoard.UnitTests/Services/CalendarFeedWriterTests.cs#Write_TimedEntryLateStart_RollsDateForwardForEnd"
        status: pass
    human_judgment: false
  - id: D2
    description: "A null-start-time entry emits a date-valued start and an exclusive next-day date-valued end, so a one-day all-day entry occupies exactly one day, for every date tested"
    requirement: CALFEED-09
    verification:
      - kind: unit
        ref: "QuestBoard.UnitTests/Services/CalendarFeedWriterTests.cs#Write_NullStartTimeEntry_EmitsDateValuedStartAndExclusiveNextDayEnd"
        status: pass
      - kind: unit
        ref: "QuestBoard.UnitTests/Services/CalendarFeedWriterTests.cs#Write_AllDayEntries_AlwaysSpanExactlyOneDay"
        status: pass
    human_judgment: false
  - id: D3
    description: "Every entry emits TRANSP:TRANSPARENT and SEQUENCE:0; the emitted DTSTAMP derives from the entry's own CreatedAt, so two Write calls on the same entry list separated by a real clock change are byte-identical"
    requirement: CALFEED-09
    verification:
      - kind: unit
        ref: "QuestBoard.UnitTests/Services/CalendarFeedWriterTests.cs#Write_AnyEntry_EmitsTransparent"
        status: pass
      - kind: unit
        ref: "QuestBoard.UnitTests/Services/CalendarFeedWriterTests.cs#Write_AnyEntry_EmitsSequenceZero"
        status: pass
      - kind: unit
        ref: "QuestBoard.UnitTests/Services/CalendarFeedWriterTests.cs#Write_Entry_DtstampMatchesCreatedAt"
        status: pass
      - kind: unit
        ref: "QuestBoard.UnitTests/Services/CalendarFeedWriterTests.cs#Write_SameEntryTwice_ProducesByteIdenticalOutputAcrossAClockChange"
        status: pass
    human_judgment: false
  - id: D4
    description: "A title containing a comma, semicolon, backslash or line break escapes and round-trips exactly through unescaping; escaping runs before folding so free text can never break a parser"
    requirement: CALFEED-11
    verification:
      - kind: unit
        ref: "QuestBoard.UnitTests/Services/CalendarFeedWriterTests.cs#Write_TitleWithSpecialCharacters_EscapesAndRoundTrips"
        status: pass
      - kind: unit
        ref: "QuestBoard.UnitTests/Services/CalendarFeedWriterTests.cs#Write_TitleWithLineBreak_EscapesToLiteralBackslashN"
        status: pass
    human_judgment: false
  - id: D5
    description: "No emitted content line exceeds 75 octets before its terminator; continuation lines carry exactly one leading space; a long or non-ASCII title round-trips losslessly through fold/unfold with no split multi-byte character"
    requirement: CALFEED-11
    verification:
      - kind: unit
        ref: "QuestBoard.UnitTests/Services/CalendarFeedWriterTests.cs#Write_LongTitle_FoldsAt75OctetsAndUnfoldsToOriginal"
        status: pass
      - kind: unit
        ref: "QuestBoard.UnitTests/Services/CalendarFeedWriterTests.cs#Write_MultiByteTitle_FoldsOnCharacterBoundaryAndRoundTrips"
        status: pass
    human_judgment: false
  - id: D6
    description: "No emitted line declares a timezone, timezone component, description, link, alarm component or cancellation status; the document is CRLF-terminated throughout and an empty entry list still emits a valid opener/headers/terminator with zero VEVENTs"
    requirement: CALFEED-10
    verification:
      - kind: unit
        ref: "QuestBoard.UnitTests/Services/CalendarFeedWriterTests.cs#Write_Document_NeverEmitsForbiddenProperties"
        status: pass
      - kind: unit
        ref: "QuestBoard.UnitTests/Services/CalendarFeedWriterTests.cs#Write_EmptyEntryList_EmitsValidDocumentWithNoEvents"
        status: pass
      - kind: unit
        ref: "QuestBoard.UnitTests/Services/CalendarFeedWriterTests.cs#Write_Document_EveryLineEndsWithCrlfAndTerminatesWithCalendarEnd"
        status: pass
    human_judgment: false
  - id: D7
    description: "The title is the board name in square brackets, a space, then the event title, with a maybe/declined suffix appended only for those two answers -- never for Yes, and never consulting HasAnswered"
    requirement: CALFEED-11
    verification:
      - kind: unit
        ref: "QuestBoard.UnitTests/Services/CalendarFeedWriterTests.cs#Write_YesAnswer_EmitsPlainTitleWithNoSuffix"
        status: pass
      - kind: unit
        ref: "QuestBoard.UnitTests/Services/CalendarFeedWriterTests.cs#Write_MaybeAnswer_AppendsMaybeSuffix"
        status: pass
      - kind: unit
        ref: "QuestBoard.UnitTests/Services/CalendarFeedWriterTests.cs#Write_NoAnswer_AppendsDeclinedSuffix"
        status: pass
      - kind: unit
        ref: "QuestBoard.UnitTests/Services/CalendarFeedWriterTests.cs#Write_Summary_OpensWithBoardNameBracket"
        status: pass
      - kind: unit
        ref: "QuestBoard.UnitTests/Services/CalendarFeedWriterTests.cs#Write_BoardNameWithComma_EscapesAndRoundTrips"
        status: pass
    human_judgment: false
  - id: D8
    description: "The document carries its calendar-level headers (VERSION, PRODID, CALSCALE, X-WR-CALNAME, X-PUBLISHED-TTL, REFRESH-INTERVAL) exactly once regardless of entry count, all preceding the first VEVENT, with no METHOD property"
    requirement: CALFEED-10
    verification:
      - kind: unit
        ref: "QuestBoard.UnitTests/Services/CalendarFeedWriterTests.cs#Write_Document_EmitsCalNameTtlAndRefreshInterval"
        status: pass
      - kind: unit
        ref: "QuestBoard.UnitTests/Services/CalendarFeedWriterTests.cs#Write_Document_EmitsVersionProdidCalscaleAndNoMethod"
        status: pass
      - kind: unit
        ref: "QuestBoard.UnitTests/Services/CalendarFeedWriterTests.cs#Write_MultipleEntries_CalendarHeadersAppearExactlyOnceBeforeFirstEvent"
        status: pass
    human_judgment: false
  - id: D9
    description: "The entry identifier's exact value, its stability across two renders, its distinctness per declared source (enumerated, not hard-coded), its anchored no-configuration pattern, and the emitter's single composition rule via BuildUid are all pinned"
    requirement: CALFEED-12
    verification:
      - kind: unit
        ref: "QuestBoard.UnitTests/Services/CalendarFeedWriterTests.cs#BuildUid_EventFortyTwo_ReturnsExactLiteral"
        status: pass
      - kind: unit
        ref: "QuestBoard.UnitTests/Services/CalendarFeedWriterTests.cs#Write_SameEntryTwice_UidLineIsIdenticalAcrossRenders"
        status: pass
      - kind: unit
        ref: "QuestBoard.UnitTests/Services/CalendarFeedWriterTests.cs#BuildUid_EveryDeclaredSource_YieldsDistinctIdentifierForSameNumericId"
        status: pass
      - kind: unit
        ref: "QuestBoard.UnitTests/Services/CalendarFeedWriterTests.cs#BuildUid_AnySourceAndId_MatchesAnchoredNamespacedPattern"
        status: pass
      - kind: unit
        ref: "QuestBoard.UnitTests/Services/CalendarFeedWriterTests.cs#Write_EmittedUidLine_EqualsBuildUidResultForSameSourceAndId"
        status: pass
    human_judgment: false

duration: 16min
completed: 2026-09-18
status: complete
---

# Phase 84 Plan 3: Calendar Feed Writer Widening -- All-Day Branch, Escaping, Folding, Vote Suffix Summary

**CalendarFeedWriter now emits a complete, mechanically correct RFC 5545 document: the all-day branch with an exclusive next-day end, a single four-step free-text escaper, a character-boundary-safe 75-octet line folder, the board-prefixed/vote-suffixed title, and the calendar-level headers -- pinned by 30 exact-byte unit facts including an enumerated invariant guard over the entry identifier.**

## Performance

- **Duration:** 16 min
- **Started:** 2026-09-18T08:09:52Z (previous plan's close-out commit)
- **Completed:** 2026-09-18T08:26:22Z
- **Tasks:** 3
- **Files modified:** 2 (1 production, 1 test)

## Accomplishments

- All-day branch emits `DTSTART;VALUE=DATE`/`DTEND;VALUE=DATE` with an exclusive next-day end, so a one-day entry occupies exactly one day rather than two
- A single `EscapeText` applied to every free-text value (title, calendar name) in the fixed order backslash -> semicolon -> comma -> newline, so escaping the backslash first never gets double-escaped by later steps
- A single `FoldLine`/`AppendFoldedLine` pair cuts every content line at 75 octets, backing off the cut point while it lands on a UTF-8 continuation byte, so a long or non-ASCII title round-trips losslessly
- `SUMMARY` now suffixes ` (maybe)`/` (declined)` based on `Availability` alone (never `HasAnswered`), so a campaign auto-created Yes row renders identically to a chosen Yes -- an accepted cost documented in the file
- Calendar-level headers (`VERSION`, `PRODID`, `CALSCALE`, `X-WR-CALNAME`, `X-PUBLISHED-TTL`, `REFRESH-INTERVAL`) emitted exactly once per document, before the first `VEVENT`, with no `METHOD` property
- A 30-fact exact-byte unit suite (`CalendarFeedWriterTests.cs`) pins every behavior above, including an entry-identifier invariant guard that enumerates `CalendarFeedSource` rather than hard-coding it, so a future added source automatically re-proves distinctness

## Task Commits

Each task followed its own RED (test) then GREEN (implementation) cycle, committed atomically:

1. **Task 1 RED: failing tests for all-day branch, escaping and folding** - `b5fd7830` (test)
2. **Task 1 GREEN: all-day branch, escaper and folder implementation** - `d0e2b03e` (feat)
3. **Task 2 RED: failing tests for the vote-marker title suffix** - `adf57287` (test)
4. **Task 2 GREEN: maybe/declined vote-marker suffix** - `ca9c3122` (feat)
5. **Task 3: entry-identifier invariant guard tests (no production change needed)** - `ebd25335` (test)

**Plan metadata:** committed as part of this SUMMARY.

_Task 2's calendar-header facts (`X-WR-CALNAME`, `X-PUBLISHED-TTL`, `REFRESH-INTERVAL`, `VERSION`/`PRODID`/`CALSCALE`/no-`METHOD`) were already satisfied by task 1's `AppendCalendarHeaders`, since the header-emission and branch-selection code paths were touched in the same pass -- only the vote-marker suffix was genuinely RED for task 2. Task 3 added tests only: `BuildUid` was already correct from the tracer, so no production commit was needed for that task._

## Files Created/Modified

- `QuestBoard.Domain/Services/CalendarFeedWriter.cs` - widened with `AppendAllDayEvent`, `AppendCalendarHeaders`, `EscapeText`, `FoldLine`/`AppendFoldedLine`, `FormatBasicDate`, renamed `FormatFloating` to `FormatBasicDateTime`, and the vote-suffix branch in `BuildSummary`
- `QuestBoard.UnitTests/Services/CalendarFeedWriterTests.cs` - new test class, 30 facts, exact-string/regex assertions in the `MarkdownServiceTests` style (no mocking, no snapshot framework)

## Decisions Made

See `key-decisions` in the frontmatter above. The most consequential: the vote-marker branches on `Availability` alone rather than `HasAnswered`, per 84-CONTEXT.md D-18's explicit acceptance of that cost.

## Deviations from Plan

None - plan executed exactly as written. Calendar-level headers landed one task earlier than the plan's task boundary suggested (both header-emission and branch-selection logic touch the same `StringBuilder` assembly pass in `Write`), but this is an implementation-ordering detail, not a scope or behavior deviation -- every behavior task 2 specified for headers was still verified by task 2's own tests, all of which passed.

## Issues Encountered

None.

## User Setup Required

None - no external service configuration required. No new dependency was added.

## Next Phase Readiness

- The writer's public surface (`Write`, `BuildUid`) is unchanged from the tracer, so `CalendarSubscriptionService.GetFeedAsync`'s call site required no change and the whole solution remains green (485 unit + 720 integration tests, 0 failures).
- Plans 84-04 through 84-08 (retention sweep, Profile UI, throttle, rate limiting) can proceed against this now-complete writer with no further widening expected in this area.
- Not covered by this plan and explicitly out of scope per the plan's own artifact list: the throttle logic, the Profile UI, and the retention sweep -- these are 84-04 through 84-08's remit.

## Self-Check: PASSED

- `QuestBoard.Domain/Services/CalendarFeedWriter.cs` - FOUND
- `QuestBoard.UnitTests/Services/CalendarFeedWriterTests.cs` - FOUND
- Commit `b5fd7830` - FOUND in `git log --oneline --all`
- Commit `d0e2b03e` - FOUND in `git log --oneline --all`
- Commit `adf57287` - FOUND in `git log --oneline --all`
- Commit `ca9c3122` - FOUND in `git log --oneline --all`
- Commit `ebd25335` - FOUND in `git log --oneline --all`
- `dotnet test QuestBoard.UnitTests --filter CalendarFeedWriterTests` - PASSED (30/30)
- `dotnet test QuestBoard.IntegrationTests --filter CalendarSubscriptionFeedTests` - PASSED (1/1)
- `dotnet test` (full solution) - PASSED (485 unit + 720 integration, 0 failures)
- Comment-stripped greps: no VALARM/VTIMEZONE/TZID/STATUS:CANCELLED, no DESCRIPTION/URL, no METHOD:, no IOptions/TimeProvider/DateTime.UtcNow/DateTime.Now, no AppendLine, no non-comment HasAnswered - all PASSED
- `file` reports CRLF line terminators for both touched files - PASSED

---
*Phase: 84-calendar-feed-foundation-and-event-subscription*
*Completed: 2026-09-18*
