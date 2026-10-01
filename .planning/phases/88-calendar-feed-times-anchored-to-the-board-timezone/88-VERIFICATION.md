---
phase: 88-calendar-feed-times-anchored-to-the-board-timezone
verified: 2026-09-30T18:00:00Z
status: passed
score: 15/16 requirements verified (CALTZ-09 open; reschedule re-test pending)
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
  - ".planning/phases/88-calendar-feed-times-anchored-to-the-board-timezone/88-05-PLAN.md"
  - ".planning/phases/88-calendar-feed-times-anchored-to-the-board-timezone/88-05-SUMMARY.md"
  - ".planning/phases/88-calendar-feed-times-anchored-to-the-board-timezone/88-06-PLAN.md"
  - ".planning/phases/88-calendar-feed-times-anchored-to-the-board-timezone/88-06-SUMMARY.md"
  - ".planning/phases/88-calendar-feed-times-anchored-to-the-board-timezone/88-07-PLAN.md"
  - ".planning/phases/88-calendar-feed-times-anchored-to-the-board-timezone/88-07-SUMMARY.md"
  - ".planning/phases/88-calendar-feed-times-anchored-to-the-board-timezone/88-08-PLAN.md"
  - ".planning/phases/88-calendar-feed-times-anchored-to-the-board-timezone/88-08-SUMMARY.md"
  - ".planning/phases/88-calendar-feed-times-anchored-to-the-board-timezone/88-09-PLAN.md"
  - ".planning/phases/88-calendar-feed-times-anchored-to-the-board-timezone/88-09-SUMMARY.md"
  - "QuestBoard.Domain/Interfaces/ICalendarFeedWriter.cs"
  - "QuestBoard.Domain/Models/CalendarFeedEntry.cs"
  - "QuestBoard.Domain/Models/Event.cs"
  - "QuestBoard.Domain/Models/EventFeedRow.cs"
  - "QuestBoard.Domain/Models/QuestBoard/Quest.cs"
  - "QuestBoard.Domain/Services/CalendarFeedWriter.cs"
  - "QuestBoard.Domain/Services/CalendarSubscriptionService.cs"
  - "QuestBoard.IntegrationTests/Tests/CalendarFeedBoardZoneHttpTests.cs"
  - "QuestBoard.IntegrationTests/Tests/CalendarFeedEventRevisionTests.cs"
  - "QuestBoard.IntegrationTests/Tests/CalendarFeedQuestRevisionTests.cs"
  - "QuestBoard.IntegrationTests/Tests/CalendarSubscriptionFeedTests.cs"
  - "QuestBoard.IntegrationTests/Tests/CalendarSubscriptionQuestFeedTests.cs"
  - "QuestBoard.Repository/Automapper/EntityProfile.cs"
  - "QuestBoard.Repository/Entities/EventEntity.cs"
  - "QuestBoard.Repository/Entities/QuestEntity.cs"
  - "QuestBoard.Repository/Entities/QuestBoardContext.cs"
  - "QuestBoard.Repository/EventSignupRepository.cs"
  - "QuestBoard.Repository/FeedRevisionStamper.cs"
  - "QuestBoard.Repository/Migrations/20260930161610_AddFeedEntryRevisions.cs"
  - "QuestBoard.Repository/Migrations/QuestBoardContextModelSnapshot.cs"
  - "QuestBoard.UnitTests/Architecture/AmbientClockSeamTests.cs"
  - "QuestBoard.UnitTests/Architecture/FeedRevisionWriteSeamTests.cs"
  - "QuestBoard.UnitTests/Repository/FeedRevisionStamperTests.cs"
  - "QuestBoard.UnitTests/Services/CalendarFeedBoardZoneGuardTests.cs"
  - "QuestBoard.UnitTests/Services/CalendarFeedRevisionInputTests.cs"
  - "QuestBoard.UnitTests/Services/CalendarFeedWriterTests.cs"
  - "QuestBoard.UnitTests/Services/CalendarSubscriptionQuestRecheckTests.cs"

covered_digest: "v1:sha256:f7314bc506e1baab72d6c85688949eb2d5988e3f26bc42ec59c0311e9e3bf010"
behavior_unverified: 0
overrides_applied: 0
re_verification:
  previous_status: human_needed
  previous_score: 9/10
  gaps_closed:
    - "G-88-4: a rescheduled entry a subscribed calendar already holds now carries a higher SEQUENCE and a later DTSTAMP / LAST-MODIFIED under the same UID (CALTZ-11..16)"
  gaps_remaining: []
  regressions: []
human_verification:

  - test: "Production reschedule re-test (UAT test 4 / gap G-88-4), after the v5.3.3 deploy: on a phone that already holds a game night from the subscription (Google Calendar and, separately, Apple Calendar), change that game night's time or title on the board, then let the calendar fetch the feed"
    expected: "The already-held entry moves to the new time or title in place, with no duplicate. The one-time migration bump means entries held before the deploy should also repair on the first fetch after it (SEQUENCE 2 and a later stamp). Record app, OS and date of each entry checked; promise no refresh latency. If Google keeps the old time, the accepted resolution is a new subscription address."
    why_human: "Whether a third-party client replaces a held entry on a higher SEQUENCE and later DTSTAMP is client behaviour; no byte test can show it. The first UAT run failed exactly here. This is CALTZ-09's open half and the only evidence that can close G-88-4 end to end."
  - test: "Optional two-tab edit against the local SQL Server: open the same event's edit form in two browser tabs, save a title change in one, then save a date change in the other"
    expected: "Both saves succeed, no error page, and the row ends at its starting revision plus 2 (check FeedRevision in the Events table). The feed shows the later SEQUENCE."
    why_human: "The concurrency-token predicate and the in-context retry were proven on the EF InMemory provider only (88-REVIEW-FIX.md says so). The SQL Server UPDATE ... WHERE FeedRevision = @original path, including transaction rollback before the retry, has never been run against a real database."
  - test: "Apple Calendar (and Google) on the same phones, after the reschedule check above: confirm nothing else regressed (entries still read 18:00 for 18:00 board times)"
    expected: "Times unchanged. UAT already confirmed this for Google (tests 1, 2) and Apple with a London override (test 3) on v5.3.2; this is a cheap regression glance on v5.3.3."
    why_human: "Client rendering."
---

# Phase 88: Calendar Feed Times Anchored to the Board Timezone Verification Report

**Phase Goal:** A game night set for 18:00 on the board shows as 18:00 in every subscriber's phone calendar, whichever calendar app they use, instead of 19:00 on one phone and 20:00 on another. After UAT, gap closure G-88-4 adds: a rescheduled entry that a subscribed calendar already holds must move to its new time, because the feed now carries a per-entry revision.
**Verified:** 2026-09-30
**Status:** human_needed
**Re-verification:** Yes. The previous report (human_needed, 9/10) covered plans 88-01..88-04. This one covers all nine plans, adds CALTZ-11..16, and folds in the UAT result and both code-review rounds.

## Verdict

Everything that can be proved in the codebase is proved. The zone work (CALTZ-01..08, 10) still holds, unchanged except where the revision contract deliberately superseded the constant-SEQUENCE clauses. The gap closure is implemented and wired end to end. The store raises a per-entry revision on every save that changes what the feed shows, the writer publishes it as SEQUENCE and as DTSTAMP and LAST-MODIFIED, and each write path is proven through the real controllers. No failed truth, no blocker, no unresolved debt marker.

The phase cannot be `passed`, for one reason only: the defect UAT found (test 4) was observed on a real phone, and no test can stand in for the phone. CALTZ-09 stays Pending in REQUIREMENTS.md and is correct to. The zone half of CALTZ-09 has real-device evidence (UAT tests 1, 2, 3, 5, 6 passed). The reschedule half needs the production re-test after v5.3.3.

## Goal Achievement

### Observable Truths

ROADMAP.md carries no separate success-criteria list for this phase. The sixteen CALTZ requirements plus the goal are the contract, along with each plan's `must_haves`.

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| CALTZ-01 | Timed entries declare the board zone on DTSTART and DTEND, stored digits unchanged, no trailing Z | VERIFIED | `CalendarFeedWriter.AppendTimedEvent` writes `DTSTART;TZID=<tzid>:` / `DTEND;TZID=<tzid>:` via `FormatBasicDateTime` (no `Z`). Whole-block byte pin in `CalendarFeedWriterTests`; live HTTP facts; all green. Production feed inspected in UAT test 6: 41 timed entries, all TZID, none with `Z`. |
| CALTZ-02 | One VTIMEZONE after the headers, before the first VEVENT, fixed-date observances, no RRULE, same bytes on Windows and Linux | VERIFIED | `AppendTimeZone` probes `GetUtcOffset` (day step plus minute bisect), leading observance at 19700101, no RRULE. Linux container run equals Windows (Total 187 on both, per 88-09; earlier 129 on both per 88-04). UAT test 6 cross-checked 82 production DTSTART/DTEND lines against ICU rules. |
| CALTZ-03 | One calendar-level zone header, same id as entries and block, all from the resolved zone | VERIFIED | `Write` derives `tzid` once and passes it to header, block and lines; service passes `boardClock.TimeZone`. Source-scan seam fact in `AmbientClockSeamTests` bans the configured-string shapes. |
| CALTZ-04 | Non-default and Windows-style zone ids declared under an IANA name with their own changes | VERIFIED | `ResolveTzid` via `TryConvertWindowsIdToIanaId`; Auckland and Windows-id unit and live HTTP facts pass. |
| CALTZ-05 | Unresolvable zone: UTC fallback declared through the same path, no zoneless branch | VERIFIED | Single `Write(..., TimeZoneInfo boardZone)` signature; UTC unit fact and live `Definitely/NotAZone` fact pass. |
| CALTZ-06 | (Superseded in part by CALTZ-11/13) Every entry carries SEQUENCE, never 0; UID unchanged | VERIFIED | `BuildSequenceLine` writes `Math.Max(1, entry.Sequence)` on both branches; UID builder untouched. A never-edited entry still goes out as SEQUENCE:1. The constant-1 and creation-time-stamp clauses were intentionally replaced, and REQUIREMENTS.md carries the supersession note. |
| CALTZ-07 | All-day entry stays date-valued with no zone; no timed entry means no VTIMEZONE | VERIFIED | `AppendAllDayEvent` writes `DTSTART;VALUE=DATE:`; `AppendTimeZone` returns early with no timed entries; unit and live facts. |
| CALTZ-08 | The feed never converts a stored wall-clock value | VERIFIED | Writer formats `entry.Date.ToDateTime(entry.StartTime)` only; service copies `Date`/`StartTime` directly; guard fact pins digits 180000 across Amsterdam, Auckland and UTC fallback. |
| CALTZ-09 | Production check: 18:00 in Google (existing then new entry) and Apple | NEEDS HUMAN | Zone half: real-device evidence exists. UAT tests 1, 2, 3 passed on v5.3.2 (Google showed the board's times after a new subscription address; Apple with a London override read 18:00 as 17:00, proving the zoned entries are read). Reschedule half: UAT test 4 failed, the fix is now in code, and the re-test on v5.3.3 has not been done. REQUIREMENTS.md correctly keeps it `[ ]` / Pending. See Human Verification. |
| CALTZ-10 | Floating-contract statements rewritten, guard tests rewritten | VERIFIED | `CalendarFeedFloatingTimeGuardTests.cs` is gone (git diff shows 170 lines deleted); `CalendarFeedBoardZoneGuardTests.cs` exists; CALFEED-10 carries its supersession note; `.claude/architecture.md` describes the zoned contract and keeps the high-risk warning, wall-clock definition and never-convert rule. |
| CALTZ-11 | Stored revision and last-revised time on every event and quest, added by one schema change that also raises every existing row once | VERIFIED | `EventEntity`/`QuestEntity` carry `FeedRevision` (default 1) and `FeedRevisedAt`. Migration `20260930161610_AddFeedEntryRevisions` adds both columns to both tables, then `UPDATE ... SET FeedRevision = FeedRevision + 1, FeedRevisedAt = SYSUTCDATETIME()` on each. `dotnet ef migrations has-pending-model-changes`, run by me: "No changes have been made to the model". **Independently queried the local SQL Server (read-only):** Events 43 rows, FeedRevision min 2 / max 2; Quests 62 rows, min 2 / max 2; 0 rows with `FeedRevisedAt <= CreatedAt`; history table's last migration is `AddFeedEntryRevisions`. Matches the operator's "bumps every existing row once" decision. |
| CALTZ-12 | A save raises the revision by exactly one, never backwards, when and only when it changes what the feed shows; every write path; no caller can overwrite it; known limitations recorded | VERIFIED | `FeedRevisionStamper.StampChangedRow`: restores stored original values first (so a caller-written value is discarded), compares the feed field lists (event: Title, Date, StartTime, CancelledAt, GroupId; quest: Title, FinalizedDate, IsFinalized, GroupId), bumps to `original + 1`, stamp is `max(utcNow, stored)`, and clears `IsModified` on a no-change save. `QuestBoardContext` overrides both `SaveChanges(bool)` and `SaveChangesAsync(bool, ct)`. Both AutoMapper entity maps ignore the two columns. `StampNewRow` starts new rows at 1 with the stamp equal to `CreatedAt`. **Write-path coverage:** my own grep of `QuestBoard.Repository`, `QuestBoard.Domain`, `QuestBoard.Service` for `ExecuteUpdate`, `ExecuteDelete`, `ExecuteSql`, `FromSql`, `.Attach(`, `.Update(`, `.UpdateRange(`, `.Entry(`, `.State =`, `TrackGraph` (excluding migrations and obj) returns nothing; `FeedRevisionWriteSeamTests` pins the same, including the state-setting shape after the WR-02 fix. **Behaviour:** event edit (title, date, start time), this-and-future sweep with a skipped sibling, cancel then restore, quest reopen then finalize at another and at the same date, quest retitle, and no-feed-change saves are each exercised through the real controllers and the anonymous feed in `CalendarFeedEventRevisionTests` (8 facts) and `CalendarFeedQuestRevisionTests` (4 facts); the stamper has field-by-field unit proof. Board rename and session-length/zone changes are documented limitations in CALTZ-12's text and in `.claude/architecture.md`, per operator decision. |
| CALTZ-13 | SEQUENCE is the stored revision, floored at 1, never lower than published; DTSTAMP and LAST-MODIFIED carry the last-revised time labelled as UTC, not converted | VERIFIED | `CalendarFeedWriter`: `BuildSequenceLine` uses `Math.Max(1, entry.Sequence)`; one `stamp = FormatUtcStamp(entry.LastRevisedAt)` string feeds both `DTSTAMP:` and `LAST-MODIFIED:` on the timed and all-day branch; `FormatUtcStamp` uses `SpecifyKind(Utc)`, never `ToUniversalTime`. The service sets `Sequence = row.Event.FeedRevision` / `q.FeedRevision` and never the creation time. "Never lower than published" is enforced by the store: concurrency token plus rebase-and-retry means the bump is always stored + 1 (see WR-01 below). Whole-block pins at sequence 4 revised 2026-09-25 08:15:30Z; distinct per-entry sequences (3, 7, 4) in one document; guard facts would catch a regression to CreatedAt. |
| CALTZ-14 | A reader's own availability change moves only that reader's stamp and last-modified, never the shared sequence or another reader's document | VERIFIED | `EventSignupRepository.GetFeedRowsForUserAsync` sets `AnswerWrittenAt = UpdatedAt ?? CreatedAt`; the service uses `Sequence = row.Event.FeedRevision` (event alone) and `LastRevisedAt = LaterOf(FeedRevisedAt, AnswerWrittenAt)`. Integration facts: answer change, never-answered row, and withdraw-then-answer-again (stamp and sequence never go backwards), each asserting the other reader's document is byte-identical and 304s on their earlier ETag. Service-level capturing-writer pins in `CalendarFeedRevisionInputTests`. Matches the operator decision. |
| CALTZ-15 | Unchanged entry is byte-identical between fetches, ETag stable, 304; exact-byte pins pass on Linux as well as Windows | VERIFIED | Stamp comes from stored data only (no clock); `Write_SameEntryTwice_ProducesByteIdenticalOutputAcrossAClockChange` intact; integration facts assert a no-feed-change edit leaves the document byte-identical and a presented ETag gets 304. Linux: 88-09 recorded the `mcr.microsoft.com/dotnet/sdk:10.0` run at Total 187 equal to Windows. I did not re-run Linux (no container in this pass); I re-ran the Windows subsets (below), and the byte pins have no platform-dependent input. |
| CALTZ-16 | Every statement describing SEQUENCE as a constant or the stamp as creation time rewritten; the tests that pinned them rewritten, not deleted | VERIFIED | `.claude/architecture.md` calendar paragraph now states SEQUENCE = stored `FeedRevision` raised by `FeedRevisionStamper`, DTSTAMP/LAST-MODIFIED = `FeedRevisedAt`, never down or below 1, byte-identical when unchanged, the two known limitations, and names `FeedRevisionStamper.cs` among the high-risk files. The Entity Framework section was also extended (88-REVIEW-FIX). REQUIREMENTS.md CALTZ-06 carries its supersession note. Writer comments describe the revision contract. The old constant-SEQUENCE/creation-time pins in `CalendarFeedWriterTests`, `CalendarFeedBoardZoneGuardTests`, `CalendarSubscriptionQuestFeedTests` and `CalendarSubscriptionFeedTests` were rewritten (diffs show modifications, not deletions). One stale comment remains (IN-01, below). |

**Score:** 15/16 requirements verified in the codebase; CALTZ-09 is a human-verification item and does not count as verified. `behavior_unverified` is 0: every behavior-dependent invariant (bump exactly +1, never backwards, no-change leaves columns untouched, caller cannot overwrite, concurrent and stale saves, write-path coverage, per-reader stamp) has a passing behavioural test. The one qualification is that the concurrency invariant is proven on the InMemory provider only; see the second human-verification item and WR-01 below.

### Plan-level must-haves (88-05..88-09), spot-checked against code

| Plan | Must-have | Status | Evidence |
|------|-----------|--------|----------|
| 88-05 | Migration adds both columns to both tables and bumps every existing row once; no model drift | VERIFIED | Migration source read; `has-pending-model-changes` clean; local SQL Server rows all at revision 2. |
| 88-05 | Both save overloads run the stamper | VERIFIED | `QuestBoardContext.SaveChanges(bool)` and `SaveChangesAsync(bool, ct)` both call `FeedRevisionStamper.Apply(ChangeTracker, clock.GetUtcNow().UtcDateTime)`; a seam test pins both overrides. |
| 88-05 | No production write bypasses the change tracker | VERIFIED | My grep: zero hits; guard test with empty allowlist passes. |
| 88-05 | New row at revision 1 with stamp = CreatedAt | VERIFIED | `StampNewRow`. |
| 88-06 | Event write paths (edit, this-and-future, cancel/restore, no-change) and per-reader answer proven via HTTP | VERIFIED | 8 facts in `CalendarFeedEventRevisionTests`, all passing in the calendar integration run. |
| 88-07 | Quest write paths proven via HTTP; tests only, allowlist untouched | VERIFIED | 4 facts in `CalendarFeedQuestRevisionTests`; no production `.cs` file in that plan's scope, and `IgnoreQueryFilters` allowlist untouched. |
| 88-08 | LAST-MODIFIED directly after DTSTAMP, same value; whole blocks pinned; service hands stored revision | VERIFIED | Writer source above; `Write_TimedEntry_EmitsTheExactEventBlock` etc. pass. |
| 88-09 | Linux run equals Windows; full suite green; architecture paragraph rewritten; migration applied locally; validation recorded | VERIFIED (Linux and full suite not re-run by me, see below) | Architecture paragraph read; local SQL Server independently queried; `88-VALIDATION.md` carries CALTZ-11..16 rows, the Linux Total 187, local-SQL-Server result and a pending production-reschedule row. |

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `QuestBoard.Repository/FeedRevisionStamper.cs` | Store-side revision rules | VERIFIED | 204 lines, fully implemented; field lists, recovery path, never-backwards rule. |
| `QuestBoard.Repository/Entities/QuestBoardContext.cs` | Save overrides running the stamper; concurrency token | VERIFIED | Both overrides, bounded 5-retry rebase loop, `IsConcurrencyToken()` on both `FeedRevision` properties. |
| `QuestBoard.Repository/Migrations/20260930161610_AddFeedEntryRevisions*.cs` + snapshot | Columns and one-time bump | VERIFIED | Present; snapshot matches the model. |
| `QuestBoard.Repository/Entities/EventEntity.cs`, `QuestEntity.cs` | `FeedRevision` (default 1), `FeedRevisedAt` | VERIFIED | Present. |
| `QuestBoard.Repository/Automapper/EntityProfile.cs` | Mapper ignores both columns | VERIFIED | Lines 33, 169 (`opt.Ignore()`), plus the paired `FeedRevisedAt`. |
| `QuestBoard.Domain/Models/CalendarFeedEntry.cs` | `Sequence`, `LastRevisedAt` | VERIFIED | Present and consumed. |
| `QuestBoard.Domain/Models/EventFeedRow.cs` | `AnswerWrittenAt` | VERIFIED | Line 19, set in the repository. |
| `QuestBoard.Domain/Services/CalendarFeedWriter.cs` | Zoned lines, VTIMEZONE, revision SEQUENCE/DTSTAMP/LAST-MODIFIED | VERIFIED | ~400 lines, no stubs. |
| `QuestBoard.Domain/Services/CalendarSubscriptionService.cs` | Hands stored revision, `boardClock.Today` window, `boardClock.TimeZone` | VERIFIED | Lines 79 (`boardClock.Today`), 113-120, 146-148, 169. |
| Test files listed under `covered_files` | Proofs | VERIFIED | All present; see spot-checks. |
| `.claude/architecture.md` | Revision contract paragraph | VERIFIED | Lines 46-64 region. |

### Key Link Verification

| From | To | Via | Status |
|------|----|-----|--------|
| `QuestBoardContext.SaveChanges*` | `FeedRevisionStamper.Apply` | both overrides call it before the base save | WIRED |
| `QuestBoardContext.SaveChanges*` | `FeedRevisionStamper.TryAdoptStoredRevision[Async]` | catch `DbUpdateConcurrencyException`, bounded retry | WIRED |
| `CalendarSubscriptionService` | `CalendarFeedEntry.Sequence/LastRevisedAt` | `row.Event.FeedRevision`, `LaterOf(FeedRevisedAt, AnswerWrittenAt)`, `q.FeedRevision`, `q.FeedRevisedAt` | WIRED |
| `EventSignupRepository.GetFeedRowsForUserAsync` | `EventFeedRow.AnswerWrittenAt` | `UpdatedAt ?? CreatedAt` | WIRED |
| `CalendarFeedWriter` | DTSTAMP and LAST-MODIFIED | one `stamp` string, both branches | WIRED |
| `CalendarSubscriptionService` | `IBoardClock` and writer | `boardClock.Today`, `writer.Write(allEntries, "D&D Quest Board", boardClock.TimeZone)` | WIRED |
| `AutoMapper` (DomainModel -> Entity) | revision columns | ignored, plus the stamper restores originals anyway | WIRED (defence in depth) |

### Data-Flow Trace (Level 4)

| Artifact | Data | Source | Real data | Status |
|----------|------|--------|-----------|--------|
| Feed SEQUENCE | `FeedRevision` | Events/Quests columns, set by stamper and migration | Yes: local DB shows 2 on every row; integration facts observe 1 -> 2 -> 3 through real controllers | FLOWING |
| Feed DTSTAMP/LAST-MODIFIED | `FeedRevisedAt` / reader `UpdatedAt` | Same columns / signup row | Yes | FLOWING |
| Feed times and zone | `entry.Date`/`StartTime`, `IBoardClock.TimeZone` | EF rows and singleton clock | Yes (UAT 5 and 6 on local and production) | FLOWING |

### Behavioral Spot-Checks (run by me)

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| Calendar, revision and seam unit tests, Windows | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~Calendar\|FullyQualifiedName~AmbientClockSeam\|FullyQualifiedName~FeedRevision"` | 216 passed, 0 failed | PASS |
| Calendar integration tests, Windows | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~Calendar"` | 190 passed, 0 failed | PASS |
| No EF model drift | `dotnet ef migrations has-pending-model-changes` | "No changes have been made to the model since the last migration." | PASS |
| Migration applied, all rows bumped once (local SQL Server, read-only query) | `sqlcmd ... SELECT COUNT, MIN/MAX(FeedRevision), count(FeedRevisedAt <= CreatedAt)` | Events 43 rows at 2; Quests 62 rows at 2; 0 anomalies | PASS |
| No banned write shapes in production source | grep over Repository/Domain/Service | zero hits | PASS |
| No planning IDs or debt markers in the phase `.cs` diff | grep of added lines in `0d081643..HEAD` for CALTZ, G-88, Phase N, plan numbers, D-xx, WR-xx, planning doc names, TBD/FIXME/XXX/TODO/HACK | zero hits | PASS |
| Full suite (805 unit, 951 integration) | not re-run by me | taken from the orchestrator and `88-REVIEW-FIX.md`; I ran the calendar-relevant subsets only | NOT INDEPENDENTLY RUN |
| Linux byte pins (Total 187) | not re-run by me | taken from 88-09's recorded container run, which matches the earlier independent Linux run for 88-04 | NOT INDEPENDENTLY RUN |

### Probe Execution

No probes declared by any plan and none found under `scripts/*/tests/probe-*.sh`. SKIPPED.

### Requirements Coverage

All sixteen IDs appear in at least one PLAN's `requirements` frontmatter and in REQUIREMENTS.md (definitions at lines 143-158, traceability rows 393-408). No orphans.

| Requirement | Source Plans | Status | Evidence |
|-------------|--------------|--------|----------|
| CALTZ-01 | 88-01 | SATISFIED | above |
| CALTZ-02 | 88-01, 88-02, 88-04 | SATISFIED | above, plus Linux |
| CALTZ-03 | 88-01, 88-02, 88-03 | SATISFIED | above |
| CALTZ-04 | 88-02, 88-03 | SATISFIED | above |
| CALTZ-05 | 88-02, 88-03 | SATISFIED | above |
| CALTZ-06 | 88-02 (superseded in part by 88-05, 88-08) | SATISFIED | above |
| CALTZ-07 | 88-02, 88-03 | SATISFIED | above |
| CALTZ-08 | 88-01, 88-02, 88-03 | SATISFIED | above |
| CALTZ-09 | 88-04 | NEEDS HUMAN | Not satisfied until the production re-test. The 88-04 SUMMARY lists CALTZ-09 under `requirements-completed`; that remains inaccurate (its own coverage entry is `human_judgment`). REQUIREMENTS.md is correct. |
| CALTZ-10 | 88-01, 88-02, 88-04 | SATISFIED | above |
| CALTZ-11 | 88-05, 88-09 | SATISFIED | above, with independent DB evidence |
| CALTZ-12 | 88-05, 88-06, 88-07 | SATISFIED | above |
| CALTZ-13 | 88-05, 88-08 | SATISFIED | above |
| CALTZ-14 | 88-06 | SATISFIED | above |
| CALTZ-15 | 88-06, 88-07, 88-08, 88-09 | SATISFIED | above |
| CALTZ-16 | 88-05, 88-08, 88-09 | SATISFIED | above |

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| `QuestBoard.Domain/Services/CalendarFeedWriter.cs` | 9-13 | Header comment still says "five-field VEVENT"; entries now carry eight properties | Info (review IN-01, carried from round 1) | Comment drift only. |
| `QuestBoard.Domain/Services/CalendarSubscriptionService.cs` | ~180-183 | Tautological ETag comment (review IN-02) | Info | None. |
| `QuestBoard.Repository/EventSignupRepository.cs` | 27-28, 40-41 | Re-posting the same availability still rewrites `UpdatedAt` via ambient `DateTime.UtcNow` (review IN-03) | Info | Moves that one reader's DTSTAMP and ETag with no visible change; breaches byte-identical-when-unchanged only for that reader's own click. Does not touch the shared SEQUENCE. |
| `QuestBoard.Domain/Models/CalendarFeedEntry.cs` | 36 | `LastRevisedAt` defaults to year 1 silently (review IN-05) | Info | Unreachable through the current service. |
| `.claude/architecture.md` | 46-64 | Client-dependent availability limitation (review IN-04: a SEQUENCE-only client may keep a stale "(maybe)"/"(declined)" suffix) not listed among known limitations | Info | Documentation completeness. The operator explicitly chose DTSTAMP-only signalling for answers. |
| Phase `.cs` diff | - | Planning IDs, debt markers | none found | The CLAUDE.md comment rule holds. |

Nothing here is a blocker. All five Info items are carried from `88-REVIEW.md` and were out of scope for `88-REVIEW-FIX.md`.

### Code-review findings, judged against the goal

- **Round 1 WR-01 / WR-02** (feed window from UTC `Today`; DTSTAMP via `ToUniversalTime`): closed. `boardClock.Today` is used at `CalendarSubscriptionService.cs:79`-area, and `FormatUtcStamp` now labels with `SpecifyKind`, matching the ban in the seam test.
- **Round 2 WR-01 (concurrent or stale saves could lose a bump or lower SEQUENCE):** fixed in 2dc50af0. `FeedRevision` is a concurrency token and the save overrides adopt the stored revision and retry (max 5). I read the mechanism and it is sound: the retry re-enters `Apply`, which recomputes from the adopted original; only Event/Quest updates or deletes are treated as recoverable; a deleted row still throws. Four tests that fail on the old code cover the lost bump (event, quest, sync overload) and the backwards write. Caveat, recorded as a human-verification item: proven on InMemory, not against the SQL Server UPDATE predicate. Risk is narrow (DM edit racing a sweep) and errs toward extra upward bumps, never lower ones.
- **Round 2 WR-02 (write-seam guard missed hand-set entity state):** fixed in 4c722f40. `FeedRevisionWriteSeamTests` now bans `.Entry(`, `.TrackGraph(` and any `.State =` assignment by regex and handles `//` inside string literals; the detector self-check proves each shape fires. My independent grep agrees that no production source uses any of them.

### Human Verification Required

See the frontmatter `human_verification`. Summary:

1. **Production reschedule re-test of UAT test 4 (gap G-88-4 and CALTZ-09's open half), after the v5.3.3 deploy.** Reschedule or retitle an entry that Google Calendar and Apple Calendar already hold and confirm it moves in place. Because the migration raised every existing row once, entries held from before the deploy should also repair on the first fetch. This is what actually closes G-88-4. If it fails again, the next suspect is the client's own update rule, not the feed's bytes.
2. **Optional two-tab edit on the local SQL Server** to exercise the concurrency-token path on the real provider.
3. **Regression glance** at Google and Apple times on v5.3.3.

Record which app and OS each phone runs and the date of each checked entry. Do not promise a refresh latency anywhere. Earlier UAT showed Google decides for itself when to re-fetch and caches per subscription address, so a new subscription address is the accepted fallback.

### Gaps Summary

No gaps. G-88-4's root cause (no revision signal after first publication: constant SEQUENCE, DTSTAMP = CreatedAt, no LAST-MODIFIED, no revision column) is addressed at every artifact the gap listed: the columns and migration, the store-side stamper on every save, the service passing the stored revision, the writer emitting SEQUENCE from it and DTSTAMP plus LAST-MODIFIED from one stamp string, the reschedule/retitle paths proven through the real controllers, and the tests that enforced the defect rewritten to enforce the contract. The operator decisions are honoured: board rename is documented as a known limitation (and no new `IgnoreQueryFilters` call site was added); the migration bumps every existing row once (verified on the local database); a reader's answer moves only that reader's DTSTAMP and LAST-MODIFIED, never the shared SEQUENCE.

What is not established, and cannot be from the code, is that Google and Apple actually apply the higher SEQUENCE to an entry they already hold. Until the production re-test is run, the statement "a rescheduled entry moves on a real phone" is supported by every automated proof available but not directly observed. That is a human-verification item, not a gap.

---

_Verified: 2026-09-30_
_Verifier: Claude (gsd-verifier)_

## Human Verification Outcome (2026-10-01)

All human-verification items are complete. `88-UAT.md` is `status: complete` with 7/7 passed and 0 issues.

- **Production reschedule re-test (gap G-88-4, CALTZ-09's open half):** passed on v5.3.3.
  - Apple Calendar (test 7, 2026-09-30): an entry the iPhone already held updated after a real change. Production and the iPhone's own address both served it at `SEQUENCE:3`, with `LAST-MODIFIED`.
  - Google Calendar (test 4, operator-confirmed 2026-10-01): a rescheduled entry Google already held moved.
- **Two-tab concurrent edit (optional):** dropped from UAT by operator decision. The SQL Server concurrency path remains a residual note in `88-SECURITY.md`.
- **CALTZ-09:** marked complete in REQUIREMENTS.md. That is the only change to a covered file since this report was written, so `covered_digest` was recomputed with `verification.fingerprint` over the same 47 covered files.

Status moved from `human_needed` to `passed`.
