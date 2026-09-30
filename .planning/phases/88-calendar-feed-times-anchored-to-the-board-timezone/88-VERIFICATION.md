---
phase: 88-calendar-feed-times-anchored-to-the-board-timezone
verified: 2026-09-30T12:30:00Z
status: human_needed
score: 9/10 must-haves verified
covered_files:
  - ".claude/architecture.md"
  - ".planning/REQUIREMENTS.md"
  - ".planning/phases/88-calendar-feed-times-anchored-to-the-board-timezone/88-01-PLAN.md"
  - ".planning/phases/88-calendar-feed-times-anchored-to-the-board-timezone/88-01-SUMMARY.md"
  - ".planning/phases/88-calendar-feed-times-anchored-to-the-board-timezone/88-02-PLAN.md"
  - ".planning/phases/88-calendar-feed-times-anchored-to-the-board-timezone/88-02-SUMMARY.md"
  - ".planning/phases/88-calendar-feed-times-anchored-to-the-board-timezone/88-03-PLAN.md"
  - ".planning/phases/88-calendar-feed-times-anchored-to-the-board-timezone/88-03-SUMMARY.md"
  - ".planning/phases/88-calendar-feed-times-anchored-to-the-board-timezone/88-04-PLAN.md"
  - ".planning/phases/88-calendar-feed-times-anchored-to-the-board-timezone/88-04-SUMMARY.md"
  - "QuestBoard.Domain/Interfaces/ICalendarFeedWriter.cs"
  - "QuestBoard.Domain/Services/CalendarFeedWriter.cs"
  - "QuestBoard.Domain/Services/CalendarSubscriptionService.cs"
  - "QuestBoard.IntegrationTests/Tests/CalendarFeedBoardZoneHttpTests.cs"
  - "QuestBoard.UnitTests/Architecture/AmbientClockSeamTests.cs"
  - "QuestBoard.UnitTests/Services/CalendarFeedBoardZoneGuardTests.cs"
  - "QuestBoard.UnitTests/Services/CalendarFeedWriterTests.cs"
covered_digest: "v1:sha256:9be12a8fba94d0921ba61060f45a3618eae5f9f330f80ad1ae78e653f49880e9"
behavior_unverified: 0
overrides_applied: 0
human_verification:
  - test: "Google Calendar, already-subscribed phone: open the subscribed calendar and look at a game night that Google held BEFORE this fix (the entry that read 19:00 or 20:00)"
    expected: "After Google's next refresh the entry reads 18:00 (the stored board time). If Google keeps the old time, remove and re-add the subscription once; that is the accepted resolution. Record the observation only; promise no refresh latency."
    why_human: "Google fetches the feed from its own servers and decides for itself whether SEQUENCE:1 (was 0) makes it replace an entry it already holds. No byte test can show a third-party client's replacement behaviour."
  - test: "Google Calendar: create a new game night at 18:00 on production, wait for Google to fetch the feed"
    expected: "The new entry reads 18:00 on the subscribed phone."
    why_human: "How Google renders a TZID=Europe/Amsterdam entry with a generated VTIMEZONE is client behaviour."
  - test: "Apple Calendar on an iPhone: check the same event and quest"
    expected: "Still reads 18:00 (no regression from the previous floating form)."
    why_human: "Same reason; iOS behaviour cannot be pinned by byte tests."
  - test: "Optional but recommended (review IN-03): reschedule an entry that both apps already hold and confirm it moves"
    expected: "The changed time shows. SEQUENCE is a constant 1 and DTSTAMP is the constant CreatedAt, so a client that applies updates only on a higher revision could ignore later reschedules. This is the one design risk the byte tests cannot rule out."
    why_human: "Client update semantics."
---

# Phase 88: Calendar Feed Times Anchored to the Board Timezone Verification Report

**Phase Goal:** A game night set for 18:00 on the board shows as 18:00 in every subscriber's phone calendar, whichever calendar app they use, instead of 19:00 on one phone and 20:00 on another.
**Verified:** 2026-09-30
**Status:** human_needed
**Re-verification:** No, initial verification

## Verdict

The code side of the phase is achieved and independently verified. Every timed entry now declares the board's zone, carries the stored digits unchanged, and is backed by a generated VTIMEZONE and X-WR-TIMEZONE derived from the one zone the board clock resolved. The end goal, "reads 18:00 on a real phone in Google and Apple Calendar", is CALTZ-09. It is a manual production check that has not been performed, so the phase cannot be `passed`. There are no failed truths and no blockers.

## Goal Achievement

### Observable Truths (mapped to the requirement family, which is the phase's success contract)

ROADMAP.md carries no separate success-criteria list for Phase 88; the ten CALTZ requirements plus the goal are the contract.

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| CALTZ-01 | Timed entries declare the board zone on DTSTART and DTEND, stored digits unchanged, no trailing Z | VERIFIED | `CalendarFeedWriter.AppendTimedEvent` (lines ~201-222) writes `DTSTART;TZID=<tzid>:` and `DTEND;TZID=<tzid>:` via `FormatBasicDateTime` (no `Z`). Pinned by `Write_TimedEntry_EmitsTheExactEventBlock`, midnight-crossing and duration facts, and the live HTTP facts; all green. |
| CALTZ-02 | Exactly one VTIMEZONE after the headers, before the first VEVENT; offset in effect before the earliest entry plus every change to the latest end; fixed-date observances, no RRULE; same bytes on Windows and Linux | VERIFIED | `AppendTimeZone` probes `TimeZoneInfo.GetUtcOffset` (day step plus minute bisect), leading observance at 19700101, window padded a day past the latest end, no RRULE or TZNAME emitted. Pinned by the spring/autumn/gap/overlap/summer-only/window-tail facts. Independent Linux run (see below): 129 passed, same as Windows. |
| CALTZ-03 | One calendar-level zone header, same id as entries and block, all from the resolved zone, never the configured string | VERIFIED | `Write` derives `tzid` once and passes it to `AppendCalendarHeaders`, `AppendTimeZone` and the timed lines. Service passes `boardClock.TimeZone` (`CalendarSubscriptionService.cs:169`). `AmbientClockSeamTests.CalendarFeedSources_TakeTheZoneOnlyFromTheBoardClock` bans `TimeZoneOptions`, `BoardTimeZoneId`, `FindSystemTimeZoneById` in both files and `IBoardClock` in the writer. Grep confirms none present. |
| CALTZ-04 | Non-default and Windows-style zone ids are declared under an IANA name with their own offset changes | VERIFIED | `ResolveTzid` uses `TimeZoneInfo.TryConvertWindowsIdToIanaId`. Unit facts for `W. Europe Standard Time` (Berlin) and Pacific/Auckland; live HTTP facts in `CalendarFeedBoardZoneHttpTests` for Auckland and the Windows id. |
| CALTZ-05 | Unresolvable configured zone: clock falls back to UTC, feed declares UTC through the same path, no zoneless branch | VERIFIED | No zoneless overload exists (interface has a single `Write` with `TimeZoneInfo boardZone`). UTC unit fact (single +0000 STANDARD, no `-0000`, no `Etc/UTC`); live fact with `Definitely/NotAZone` asserts the body never names it. |
| CALTZ-06 | Every entry, timed and all-day, carries SEQUENCE:1, never 0; UID and DTSTAMP unchanged | VERIFIED | `SEQUENCE:1` on both branches; `BuildUid` and `FormatUtcStamp(CreatedAt)` untouched. Facts: `Write_AnyEntry_EmitsSequenceOne`, empty-document fact, exact VEVENT block pins, quest-reschedule fetch fact. |
| CALTZ-07 | All-day entry stays date-valued with no zone; feed with no timed entry has no VTIMEZONE | VERIFIED | `AppendAllDayEvent` writes `DTSTART;VALUE=DATE:`; `AppendTimeZone` returns early when there are no timed entries. Facts: all-day-only, empty-list and mixed-document tests, plus live HTTP fact. |
| CALTZ-08 | Feed never converts a stored wall-clock value | VERIFIED | The writer only formats `entry.Date.ToDateTime(entry.StartTime)`. Service builds entries with `Date`/`StartTime` copied directly. Guard fact pins the digits 180000 across zones (default, Auckland, UTC fallback) end to end. |
| CALTZ-09 | Production check: 18:00 in Google (existing then new entry) and Apple | NEEDS HUMAN | Not performed. See Human Verification. REQUIREMENTS.md correctly keeps it `[ ]` / Pending. |
| CALTZ-10 | Floating-contract statements rewritten, not deleted; floating guard tests rewritten to pin the zoned contract | VERIFIED | `.claude/architecture.md` lines 46-54 rewritten to the zoned contract, keeps the high-risk warning, wall-clock definition and never-convert rule unchanged. `CalendarFeedFloatingTimeGuardTests.cs` no longer exists; `CalendarFeedBoardZoneGuardTests.cs` exists. CALFEED-10 carries a supersession note. Writer comments describe the zoned contract. (Minor comment drift in IN-01, see below.) |

**Score:** 9/10 requirements verified; CALTZ-09 is a human-verification item and does not count as verified. `behavior_unverified` is 0: the behavior-dependent invariants (block contents at clock changes, DST gap/overlap, sequence stability, zone hand-off identity, degraded-clock path) each have passing behavioral tests.

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `QuestBoard.Domain/Interfaces/ICalendarFeedWriter.cs` | `Write(..., TimeZoneInfo boardZone)` | VERIFIED | Present, single zoned signature. |
| `QuestBoard.Domain/Services/CalendarFeedWriter.cs` | Zoned lines, X-WR-TIMEZONE, VTIMEZONE, SEQUENCE:1 | VERIFIED | Substantive (about 370 lines), fully implemented, no stubs. |
| `QuestBoard.Domain/Services/CalendarSubscriptionService.cs` | Hands `boardClock.TimeZone` to the writer | VERIFIED | Line 169; `IBoardClock` injected at line 18. |
| `QuestBoard.UnitTests/Services/CalendarFeedBoardZoneGuardTests.cs` | Zoned guard (renamed) | VERIFIED | Exists; old floating guard file removed. |
| `QuestBoard.UnitTests/Services/CalendarFeedWriterTests.cs` | Byte pins | VERIFIED | Includes all pins named in 88-02 must_haves. |
| `QuestBoard.IntegrationTests/Tests/CalendarFeedBoardZoneHttpTests.cs` | Live-feed proof for four zones | VERIFIED | Exists; passes. |
| `QuestBoard.UnitTests/Architecture/AmbientClockSeamTests.cs` | Additive seam guard | VERIFIED | Diff vs pre-phase is purely additive (two guarded/consumer entries, one new fact); no existing entry removed or reworded. |
| `.claude/architecture.md` | Zoned contract paragraph | VERIFIED | Contains `IBoardClock.TimeZone`, `CalendarFeedWriter.cs`, high-risk warning. |
| `.planning/phases/88-*/88-VALIDATION.md` | Linux evidence, nyquist_compliant true | VERIFIED | `nyquist_compliant: true`; Linux row recorded. |

### Key Link Verification

| From | To | Via | Status |
|------|----|-----|--------|
| `CalendarSubscriptionService` | `IBoardClock` | primary-ctor param, `boardClock.TimeZone` | WIRED |
| `CalendarSubscriptionService` | `CalendarFeedWriter` | `writer.Write(allEntries, "D&D Quest Board", boardClock.TimeZone)` (line 169) | WIRED |
| `CalendarFeedWriter.Write` | header / block / timed lines | one `ResolveTzid(boardZone)` result | WIRED |
| Guard tests | Service/writer | `BeSameAs` on the captured zone argument; source-scan seam fact | WIRED |

### Data-Flow Trace (Level 4)

| Artifact | Data | Source | Real data | Status |
|----------|------|--------|-----------|--------|
| Feed body | `entry.Date` / `StartTime` | EF event and quest rows via `GetFeedRowsForUserAsync` / `GetFeedQuestsForUserAsync` | Yes (integration tests fetch seeded rows over HTTP and see `190000`) | FLOWING |
| Feed body | zone | `IBoardClock.TimeZone` (singleton resolved from configuration) | Yes (variant hosts prove Amsterdam, Auckland, UTC fallback, Windows id) | FLOWING |

### Behavioral Spot-Checks (run by the verifier, not taken from SUMMARY)

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| Calendar unit tests plus seam guard, Windows | `dotnet test QuestBoard.UnitTests --filter "Calendar\|AmbientClockSeam"` | 140 passed, 0 failed | PASS |
| Calendar integration tests, Windows | `dotnet test QuestBoard.IntegrationTests --filter Calendar` | 176 passed, 0 failed | PASS |
| Exact-byte VTIMEZONE pins on Linux | `git archive HEAD` streamed into `mcr.microsoft.com/dotnet/sdk:10.0`, filter `CalendarFeed\|CalendarSubscriptionQuestRecheck\|AmbientClockSeamTests` | 129 passed, 0 failed (matches the SUMMARY's Windows figure and count) | PASS |

The full-suite figure (729 unit + 937 integration) is the orchestrator's and the SUMMARY's; I re-ran the calendar-relevant subsets rather than the whole suite.

### Probe Execution

No probes declared by the phase; none found under `scripts/*/tests/probe-*.sh` relevant to it. SKIPPED.

### Requirements Coverage

| Requirement | Source Plans | Status | Evidence |
|-------------|--------------|--------|----------|
| CALTZ-01 | 88-01 | SATISFIED | above |
| CALTZ-02 | 88-01, 88-02, 88-04 | SATISFIED | above, plus Linux run |
| CALTZ-03 | 88-01, 88-02, 88-03 | SATISFIED | above |
| CALTZ-04 | 88-02, 88-03 | SATISFIED | above |
| CALTZ-05 | 88-02, 88-03 | SATISFIED | above |
| CALTZ-06 | 88-02 | SATISFIED | above |
| CALTZ-07 | 88-02, 88-03 | SATISFIED | above |
| CALTZ-08 | 88-01, 88-02, 88-03 | SATISFIED | above |
| CALTZ-09 | 88-04 | NEEDS HUMAN | Manual production check not performed. The 88-04 SUMMARY lists it under `requirements-completed`; that is inaccurate, and the SUMMARY's own D4 coverage entry has empty verification with `human_judgment: true`. REQUIREMENTS.md rightly keeps it Pending. |
| CALTZ-10 | 88-01, 88-02, 88-04 | SATISFIED | above |

All ten IDs appear in at least one PLAN's `requirements` frontmatter and in REQUIREMENTS.md (lines 143-152 and traceability rows 387-396). No orphaned requirements.

### Anti-Patterns Found

| File | Pattern | Severity | Impact |
|------|---------|----------|--------|
| Phase `.cs` diff (0d081643..HEAD) | Planning IDs (CALTZ/D-xx/Phase/plan numbers) or planning doc names in source | none found | The CLAUDE.md comment rule holds. |
| Same | TBD/FIXME/XXX/TODO markers | none found | |
| `CalendarFeedWriter.cs:9-13` | Header comment still says "five-field VEVENT" (seven properties now) | Info (IN-01) | Comment drift only. |

### Code Review Warnings, judged against the goal

- **WR-01 (feed window "today" from `TimeProvider` UTC, `CalendarSubscriptionService.cs:79`): does not undermine the phase goal or any must_have.** It bounds which entries are in the MonthsBack/MonthsAhead window; it never touches a rendered time, zone or digit. The behaviour is pre-existing, and the code already carries a comment accepting the skew. It does conflict with the standing architecture rule ("server-side what-day-is-it reads go through `IBoardClock`"), and the new seam guard cannot see it, since it only bans configured-zone shapes and checks that the string `IBoardClock` appears. Recommend a small follow-up (`boardClock.Today` plus fixture updates), not a phase 88 gap. Severity: WARNING, non-blocking.
- **WR-02 (DTSTAMP via `ToUniversalTime()` on `Unspecified` `CreatedAt`): does not undermine the goal.** DTSTAMP is not a displayed time and this code predates the phase. Production is a UTC LXC/container, where `Unspecified` treated as Local equals UTC, so output is stable. The residual risk is host-dependent DTSTAMP on a non-UTC host, which matters only because the phase leans on constant DTSTAMP and SEQUENCE for revision ordering (see IN-03). Recommend the `SpecifyKind` fix plus a guard for `TimeZoneInfo.Local` / `ToUniversalTime(`. Severity: WARNING, non-blocking.
- IN-02 (block generation assumes at most one offset change per day and minute-aligned probes) and IN-03 (SEQUENCE is a one-time lever) are latent; IN-03 is folded into the fourth human-verification item.

### Human Verification Required

See frontmatter `human_verification`. Summary:

1. **Google Calendar, entry held before the fix.** Confirm it corrects to 18:00 after a refresh, or re-subscribe once.
2. **Google Calendar, new entry.** Confirm 18:00.
3. **Apple Calendar on iPhone.** Confirm 18:00 unchanged from what it showed before.
4. **Rescheduled entry (recommended).** Confirm a time change on an already-held entry propagates, given constant SEQUENCE and DTSTAMP.

Record which app and OS each phone runs and the date of each checked entry. Do not promise a refresh latency anywhere.

### Gaps Summary

No gaps. Nothing failed. The only unresolved item is CALTZ-09, which by design (D-09) is provable only on production with real phones. Until it is performed the phase goal, "reads 18:00 on every phone, whichever app", is supported by every automated proof available but not directly observed.

---

_Verified: 2026-09-30_
_Verifier: Claude (gsd-verifier)_
