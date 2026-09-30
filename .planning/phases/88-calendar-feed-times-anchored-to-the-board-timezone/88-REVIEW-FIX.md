---
phase: 88-calendar-feed-times-anchored-to-the-board-timezone
fixed_at: 2026-09-30T00:00:00Z
review_path: .planning/phases/88-calendar-feed-times-anchored-to-the-board-timezone/88-REVIEW.md
iteration: 1
findings_in_scope: 2
fixed: 2
skipped: 0
status: all_fixed
---

# Phase 88: Code Review Fix Report

**Fixed at:** 2026-09-30
**Source review:** .planning/phases/88-calendar-feed-times-anchored-to-the-board-timezone/88-REVIEW.md
**Iteration:** 1

**Summary:**
- Findings in scope: 2
- Fixed: 2
- Skipped: 0

Info findings (IN-01 to IN-04) were out of scope for `critical_warning` and were not touched.

## Fixed Issues

### WR-01: Feed window "today" still derives from UTC, not from the injected board clock

**Files modified:** `QuestBoard.Domain/Services/CalendarSubscriptionService.cs`, `QuestBoard.UnitTests/Services/CalendarFeedBoardZoneGuardTests.cs`, `QuestBoard.UnitTests/Services/CalendarSubscriptionQuestRecheckTests.cs`, `QuestBoard.UnitTests/Architecture/AmbientClockSeamTests.cs`
**Commit:** b43a4f74
**Applied fix:** `GetFeedAsync` now uses `boardClock.Today`. `timeProvider` stays for the two real instants (`TouchLastFetchedAsync`, `RevokeAsync`). The stale comment that accepted the UTC/board-date skew was rewritten.
**Tests:**
- New unit fact `GetFeedAsync_WindowIsMeasuredFromTheBoardClocksToday_NotTheUtcDate` uses an instant of 2026-09-19 23:30Z, an Auckland clock and `Today` = 2026-09-20. It asserts the window bounds the repository receives. Run against the old code it failed with "Expected capturedStart to be 2026-06-20, but found 2026-06-19".
- `FakeBoardClock.Today` defaults to `default(DateOnly)`, so the affected fakes in the guard and recheck suites now set it. The default made `AddMonths(-3)` throw.
- New source guard `CalendarSubscriptionService_MeasuresTheFeedWindowFromTheBoardClocksToday` requires `boardClock.Today` in the service. It also bans any line that both builds a date with `FromDateTime(` and reads `GetUtcNow`. The check is line-scoped because the quest branch legitimately calls `DateOnly.FromDateTime(q.FinalizedDate...)` on a wall-clock value. The guard failed against the old code. No existing guard entry was weakened or removed.
**Status note:** fixed: requires human verification (logic change). The window bound now follows the board date. That is the intent, but it is worth a glance.

### WR-02: Writer's DTSTAMP relies on the host's local zone, and the new guard test does not cover it

**Files modified:** `QuestBoard.Domain/Services/CalendarFeedWriter.cs`, `QuestBoard.UnitTests/Services/CalendarFeedWriterTests.cs`, `QuestBoard.UnitTests/Architecture/AmbientClockSeamTests.cs`
**Commit:** e98ad91d
**Applied fix:** `FormatUtcStamp` now does `DateTime.SpecifyKind(value, DateTimeKind.Utc)` and formats the result, with no `ToUniversalTime()`.
**Read/write verification (not assumed):**
- Every writer of `CreatedAt` stores UTC. The entity defaults are `DateTime.UtcNow` in `EventEntity` and `QuestEntity`. `GroupService`, `EventSignupRepository` and `CalendarSubscriptionRepository` also assign `DateTime.UtcNow`.
- The repository, service and web layers have no `ValueConverter`, `HasConversion` or `DateTimeKind` handling in the persistence path. `HtmlHelperExtensions` already documents that EF returns `Unspecified` for UTC-stored values.
- So `SpecifyKind(Utc)` labels the value correctly, and `ToUniversalTime()` was shifting it by the host offset.
**Tests:**
- New unit fact `Write_EntryWithUnspecifiedKindCreatedAt_StampsTheStoredDigitsUnshifted`. It failed against the old code on this non-UTC host. It cannot fail on a host whose local zone is UTC.
- New guard `CalendarFeedSources_NeverConvertThroughTheHostsLocalZone` covers both feed source files. It bans `TimeZoneInfo.Local`, `ToUniversalTime(` and `ToLocalTime(`. It failed against the old writer and passes now. The existing `CalendarFeedSources_TakeTheZoneOnlyFromTheBoardClock` test is untouched.

## Contract check

The byte-pinned output did not change. The existing exact-byte tests for TZID, VTIMEZONE, X-WR-TIMEZONE and SEQUENCE:1 all pass unmodified.

## Verification

Gates ran in the main checkout on `milestone/v9-rolling-improvements`, not in a worktree. The orchestrator asked for the current branch in place. The worktree default was therefore not used, and there was no worktree, temp branch or recovery sentinel to clean up. Final `dotnet test` on the whole solution:

- Unit tests: 734 passed, 0 failed
- Integration tests: 937 passed, 0 failed

---

_Fixed: 2026-09-30_
_Fixer: Claude (gsd-code-fixer)_
_Iteration: 1_
