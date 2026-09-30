---
phase: 88-calendar-feed-times-anchored-to-the-board-timezone
reviewed: 2026-09-30T00:00:00Z
depth: standard
files_reviewed: 11
files_reviewed_list:
  - .claude/architecture.md
  - QuestBoard.Domain/Interfaces/ICalendarFeedWriter.cs
  - QuestBoard.Domain/Services/CalendarFeedWriter.cs
  - QuestBoard.Domain/Services/CalendarSubscriptionService.cs
  - QuestBoard.IntegrationTests/Tests/CalendarFeedBoardZoneHttpTests.cs
  - QuestBoard.IntegrationTests/Tests/CalendarSubscriptionFeedTests.cs
  - QuestBoard.IntegrationTests/Tests/CalendarSubscriptionQuestFeedTests.cs
  - QuestBoard.UnitTests/Architecture/AmbientClockSeamTests.cs
  - QuestBoard.UnitTests/Services/CalendarFeedBoardZoneGuardTests.cs
  - QuestBoard.UnitTests/Services/CalendarFeedWriterTests.cs
  - QuestBoard.UnitTests/Services/CalendarSubscriptionQuestRecheckTests.cs
findings:
  critical: 0
  warning: 2
  info: 4
  total: 6
status: issues_found
---

# Phase 88: Code Review Report

**Reviewed:** 2026-09-30
**Depth:** standard
**Files Reviewed:** 11
**Status:** issues_found

## Summary

The zoned-feed change is correct on the core contract. Stored wall-clock digits are written unchanged. The TZID, the VTIMEZONE and X-WR-TIMEZONE come from one derived string. The zone reaches the writer only through `IBoardClock.TimeZone`. The VTIMEZONE onset arithmetic is right: the onset is written in the local time of the offset in force just before it. The bisect search, the leading observance, the fold logic and the all-day branch all behave as documented.

I ran the affected suites on this Windows host: 129 unit tests and 52 integration tests, all passing. I found no planning or tracking IDs in the source comments or string literals, so the CLAUDE.md comment rule holds.

There are no blockers. The two warnings concern ambient time reads that this phase's own architectural rule and guard tests were meant to exclude. The info items are comment drift and edge-case assumptions in the time-zone block generator.

## Warnings

### WR-01: Feed window "today" still derives from UTC, not from the injected board clock

**File:** `QuestBoard.Domain/Services/CalendarSubscriptionService.cs:79`
**Issue:** This phase injects `IBoardClock` into the service, but `var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);` is still a server-side "what day is it" read that bypasses the seam. `.claude/architecture.md` says these reads go through `IBoardClock` and never through ambient UTC. `IBoardClock.Today` exists for this purpose.

The phase also added the service to `BoardClockConsumerPaths` in `AmbientClockSeamTests`. That positive check is satisfied by the string "IBoardClock" appearing at all, and here it appears only because of the `.TimeZone` read. The test now reports the service as seam-compliant while the one date computation in it is not. The line 115 comment accepts the UTC/board-date skew as negligible, but the rule leaves no room for that exception.

The skew is real. Between local midnight and UTC midnight, `today` is a day off from the board date. Events on the edge of the `MonthsBack`/`MonthsAhead` bound can flip in or out of the feed.

**Fix:**
```csharp
var today = boardClock.Today;
```
Keep `timeProvider` for the `TouchLastFetchedAsync` and `RevokeAsync` instants, which are real instants. Update the affected unit and integration fakes. `FakeBoardClock.Today` defaults to `default(DateOnly)`, so tests that rely on the window need to set it.

### WR-02: Writer's DTSTAMP relies on the host's local zone, and the new guard test does not cover it

**File:** `QuestBoard.Domain/Services/CalendarFeedWriter.cs:283-284`, guard at `QuestBoard.UnitTests/Architecture/AmbientClockSeamTests.cs:298-329`
**Issue:** `value.ToUniversalTime()` converts through `TimeZoneInfo.Local` when `value.Kind` is `Unspecified` or `Local`. `CreatedAt` comes through EF and AutoMapper, and I found no UTC value converter or `DateTimeKind` handling in `QuestBoard.Repository`. SQL Server `datetime2` values therefore come back as `Unspecified`.

The result is host-dependent DTSTAMP output: identical on a UTC container, shifted by the host offset on a Windows or non-UTC host. The `Unspecified` handling is an inference; no test exercises it. The writer's header comment says the feed "derives from the entry's own CreatedAt rather than any ambient clock". That is true for the clock but not for the zone.

The new `CalendarFeedSources_TakeTheZoneOnlyFromTheBoardClock` test bans only `TimeZoneOptions`, `BoardTimeZoneId` and `FindSystemTimeZoneById`. It would pass code using `TimeZoneInfo.Local` or `ToUniversalTime()`, which is the same class of leak the test exists to stop. The DTSTAMP behaviour predates this phase. The phase now relies on DTSTAMP stability across hosts, because `SEQUENCE:1` is constant and revisions tie-break on DTSTAMP.

**Fix:** Treat the stored value as UTC explicitly:
```csharp
private static string FormatUtcStamp(DateTime value) =>
    DateTime.SpecifyKind(value, DateTimeKind.Utc)
        .ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
```
Add a test with an `Unspecified` `CreatedAt` that asserts the digits are unchanged. Add `TimeZoneInfo.Local` and `ToUniversalTime(` to the banned shapes in the guard test.

## Info

### IN-01: Writer header comment still describes a "five-field VEVENT"

**File:** `QuestBoard.Domain/Services/CalendarFeedWriter.cs:9-13`
**Issue:** Timed entries now emit seven properties (UID, DTSTAMP, DTSTART, DTEND, SUMMARY, TRANSP, SEQUENCE). The comment also says "no recurrence rule". That remains true, but the "smaller surface than a library" justification now understates the generated VTIMEZONE logic in this class.
**Fix:** Reword to match the current surface, for example "a small fixed set of VEVENT properties plus a generated time-zone block".

### IN-02: Time-zone block generation assumes at most one offset change per 24-hour step and whole-minute alignment

**File:** `QuestBoard.Domain/Services/CalendarFeedWriter.cs:123-167`
**Issue:** Two assumptions are unstated.
- Two transitions inside one one-day step would leave `zone.GetUtcOffset(next) == previous`, and the block would silently drop both. No real zone does this today.
- The bisect probes are anchored at `windowStart`, which carries the seconds of the earliest `StartTime`. A `StartTime` such as 19:00:30 makes every probe land on `:30`. The onset is then up to 59 seconds late and is written with non-zero seconds, for example `T030030`, contradicting the comment "onsets never carry fractional seconds". Current inputs come from HH:mm pickers, so this is latent.

**Fix:** Truncate `windowStart` and `windowEnd` to the minute before probing, for example `new DateTime(ticks - ticks % TimeSpan.TicksPerMinute, DateTimeKind.Utc)`. Document the one-change-per-day assumption at the loop.

### IN-03: `SEQUENCE:1` is a one-time migration lever, and the contract does not say so

**File:** `QuestBoard.Domain/Services/CalendarFeedWriter.cs:214-219`, `:237`
**Issue:** The constant fixes the old-to-new migration, where floating-time events must be replaced by zoned ones. It does not signal later edits to a client that compares SEQUENCE and then DTSTAMP. DTSTAMP is `CreatedAt`, so it is also constant. A client that applies updates only on a higher revision will ignore a rescheduled event, and the integration test at `CalendarSubscriptionQuestFeedTests` pins this as intended.

If the target clients (Apple Calendar and Google in particular) replace subscribed feeds wholesale, this is fine. That is an assumption to record, and a future contract change cannot reuse the same trick without a code change.
**Fix:** State in `architecture.md` that revision numbers are constant by design, and record that the real-phone check covers a rescheduled entry. Otherwise no change.

### IN-04: Timed and all-day event emission duplicate the property lines

**File:** `QuestBoard.Domain/Services/CalendarFeedWriter.cs:201-239`
**Issue:** `AppendTimedEvent` and `AppendAllDayEvent` repeat UID, DTSTAMP, SUMMARY, TRANSP and SEQUENCE. Those lines must stay in the same order, and the `SEQUENCE` invariant is now written in two places. A future edit to one branch, such as a revision bump, can miss the other. Neither method uses instance state, so both could be `static`.
**Fix:** Extract a shared `AppendEvent(builder, entry, startLine, endLine)` helper and make both branches static.

---

_Reviewed: 2026-09-30_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
