---
phase: "88"
slug: "calendar-feed-times-anchored-to-the-board-timezone"
# status lifecycle: draft (seeded by plan-phase) → validated (set by validate-phase §6)
# audit-milestone §5.5 distinguishes NOT-VALIDATED (draft) from PARTIAL (validated + nyquist_compliant: false) (#2117)
status: validated
nyquist_compliant: true
wave_0_complete: true
created: "2026-09-30"
---

# Phase 88 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xunit.v3 3.2.2 on `net10.0`, FluentAssertions 8.10.0, NSubstitute 5.3.0 (unit) |
| **Config file** | none beyond the test csproj files |
| **Quick run command** | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~CalendarFeed\|FullyQualifiedName~CalendarSubscriptionQuestRecheck\|FullyQualifiedName~AmbientClockSeamTests\|FullyQualifiedName~FeedRevision"` (103 tests at planning time; 129 once 88-02 and 88-03 land; 187 once the revision contract lands, measured 2026-09-30 on Windows and Linux) |
| **Integration filter** | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~CalendarSubscriptionFeedTests\|FullyQualifiedName~CalendarSubscriptionQuestFeedTests\|FullyQualifiedName~BoardTimeZoneHealthCheckTests\|FullyQualifiedName~CalendarFeedBoardZoneHttpTests\|FullyQualifiedName~CalendarFeedEventRevisionTests\|FullyQualifiedName~CalendarFeedQuestRevisionTests"` (51 tests at planning time; 55 once 88-03 lands; 69 once the revision contract lands, measured 2026-09-30 on Windows) |
| **Full suite command** | `dotnet test` |
| **Estimated runtime** | ~19 s quick (including build), ~6 s integration filter, full suite longer |

If `dotnet build` fails on locked files, ask the user to stop the Visual Studio debugger (Shift+F5).

---

## Sampling Rate

- **After every task commit:** Run the quick unit command above.
- **After every plan wave:** Run the quick unit command plus the integration filter.
- **Before `/gsd-verify-work`:** `dotnet test` (full suite) must be green, then the manual production checks below.
- **Max feedback latency:** 30 seconds

---

## Per-Task Verification Map

Rows are keyed by requirement (minted into REQUIREMENTS.md by 88-01 Task 2). Task ids read `88-<plan>-T<n>`, where `<n>` is the task position in that plan. The new integration class `CalendarFeedBoardZoneHttpTests` (88-03) joins the integration filter as `|FullyQualifiedName~CalendarFeedBoardZoneHttpTests`; the renamed guard `CalendarFeedBoardZoneGuardTests` (88-02) is already matched by `FullyQualifiedName~CalendarFeed`.

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 88-01-T1 | 88-01 | 1 | CALTZ-01 | — | N/A | unit | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~CalendarFeedWriterTests"` | rewrite existing | ✅ green |
| 88-02-T1, 88-02-T2, 88-04-T1 | 88-02, 88-04 | 2, 3 | CALTZ-02 | T-88-01 | N/A | unit | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~CalendarFeed"` | ❌ W0 (new facts) | ✅ green |
| 88-01-T1, 88-02-T2, 88-03-T2 | 88-01, 88-02, 88-03 | 1, 2 | CALTZ-03 | — | N/A | unit | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~CalendarFeed"` | ❌ W0 (new facts) | ✅ green |
| 88-02-T2, 88-03-T1 | 88-02, 88-03 | 2 | CALTZ-04 | T-88-02 | Declared zone never taken from the raw configured string | unit + integration | quick unit command; integration filter | ❌ W0 (new facts) | ✅ green |
| 88-02-T1, 88-02-T2, 88-03-T1 | 88-02, 88-03 | 2 | CALTZ-05 | T-88-02, T-88-06 | Declared zone equals resolved zone (UTC fallback declared as UTC) | unit + integration | quick unit command; integration filter | ❌ W0 (new facts) | ✅ green |
| 88-02-T3 | 88-02 | 2 | CALTZ-06 | T-88-05 | N/A | unit | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~CalendarFeedWriterTests"` | rewrite existing | ✅ green |
| 88-02-T2, 88-03-T1 | 88-02, 88-03 | 2 | CALTZ-07 | — | N/A | unit | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~CalendarFeedWriterTests"` | ❌ W0 (new facts) | ✅ green |
| 88-01-T1, 88-02-T1, 88-03-T1 | 88-01, 88-02, 88-03 | 1, 2 | CALTZ-08 | T-88-03 | N/A | unit + integration | quick unit command; integration filter | rewrite existing | ✅ green |
| 88-01-T1, 88-02-T1, 88-04-T2 | 88-01, 88-02, 88-04 | 1, 2, 3 | CALTZ-10 | — | N/A | unit | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~AmbientClockSeamTests"` plus writer tests | ✅ exists | ✅ green |
| 88-04-T2 | 88-04 | 3 | CALTZ-09 | — | N/A | manual | see Manual-Only Verifications; results in 88-UAT.md | n/a | ✅ green (manual, 2026-09-30) |
| 88-05-T1, 88-05-T2, 88-09-T3 | 88-05, 88-09 | 1, 3 | CALTZ-11 | T-88-10, T-88-13 | The schema change only increments: every existing row reads revision 2 with a stamp after its creation, and the column default is 1 | unit + integration + SQL Server | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~FeedRevisionStamperTests"`; integration filter; the read-only `sqlcmd` check in 88-09 Task 3 | ✅ exists | ✅ green (InMemory tests and the local SQL Server check, 2026-09-30) |
| 88-05-T1, 88-05-T2, 88-06-T1, 88-07-T1 | 88-05, 88-06, 88-07 | 1, 2 | CALTZ-12 | T-88-09, T-88-11, T-88-17 | No posted or mapped value reaches the revision columns; no write path bypasses the change tracker | unit + integration | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~FeedRevisionStamperTests\|FullyQualifiedName~FeedRevisionWriteSeamTests"`; integration filter (`CalendarFeedEventRevisionTests`, `CalendarFeedQuestRevisionTests`) | ✅ exists | ✅ green |
| 88-05-T1, 88-05-T2, 88-08-T1 | 88-05, 88-08 | 1, 2 | CALTZ-13 | T-88-10, T-88-19 | The sequence number never goes down or below 1; DTSTAMP and LAST-MODIFIED come from one computed string | unit + integration | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~CalendarFeedWriterTests"`; integration filter | ✅ exists | ✅ green |
| 88-06-T2 | 88-06 | 2 | CALTZ-14 | T-88-14, T-88-15, T-88-16 | One reader's answer time never reaches another reader's document; the stamp only moves forward and the sequence number stays event-scoped | unit + integration | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~CalendarFeedRevisionInputTests"`; integration filter (`CalendarFeedEventRevisionTests`) | ✅ exists | ✅ green |
| 88-06-T1, 88-07-T1, 88-08-T1, 88-09-T1 | 88-06, 88-07, 88-08, 88-09 | 2, 3 | CALTZ-15 | T-88-08, T-88-20 | An unchanged entry is byte-identical between fetches (ETag and 304 hold); the rewritten byte pins pass on Linux as well as Windows | unit + integration | quick unit command on Windows and inside `mcr.microsoft.com/dotnet/sdk:10.0` (Total 187 on both); integration filter | ✅ exists | ✅ green |
| 88-05-T2, 88-08-T1, 88-08-T2, 88-09-T2 | 88-05, 88-08, 88-09 | 1, 2, 3 | CALTZ-16 | T-88-20 | No test or statement still pins a constant sequence number or a creation-time stamp; the architecture guidance states the revision contract | unit + integration + review | quick unit command; `dotnet test` (full suite); `.claude/architecture.md` paragraph review | ✅ exists | ✅ green |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

Existing infrastructure covers all phase requirements: the framework, `FakeBoardClock` (settable `TimeZone`, `IsDegraded`, `Today`, `Now`), the zone-variant host factory pattern from `WallClockUnmovedTests`, and the `Pacific/Auckland` id are already in use.

- [x] A shared `Amsterdam` `TimeZoneInfo` field in the test classes that construct the writer (added alongside the first rewritten fact, not a separate step) — 88-01 Task 1. Present as `AmsterdamZone` in `CalendarFeedWriterTests` and `CalendarFeedBoardZoneGuardTests`.
- [x] Optional cross-platform proof: run the quick unit filter inside the `mcr.microsoft.com/dotnet/sdk:10.0` container against the repo so the exact-byte `VTIMEZONE` facts also execute on Linux — planned as 88-04 Task 1. Run 2026-09-30 on mcr.microsoft.com/dotnet/sdk:10.0 against `git archive HEAD` (no volume mount): Windows 129 passed, Linux 129 passed, 0 failed on both.
- [x] Gap run of the same cross-platform proof, after the revision contract landed (88-05 to 88-08). Run 2026-09-30 on mcr.microsoft.com/dotnet/sdk:10.0 against `git archive HEAD` (no volume mount, archive fed on standard input and deleted afterwards), filter `FullyQualifiedName~CalendarFeed|FullyQualifiedName~CalendarSubscriptionQuestRecheck|FullyQualifiedName~AmbientClockSeamTests|FullyQualifiedName~FeedRevision`: Windows Total 187 passed, Linux Total 187 passed, 0 failed on both. The rewritten SEQUENCE, DTSTAMP and LAST-MODIFIED byte pins therefore also hold on the platform production runs.
- [x] Local SQL Server check (the InMemory tests never run the migration's SQL). 2026-09-30, localhost:1433, database `QuestBoard`, Windows authentication, read-only `sqlcmd` queries apart from the migration. Before: `__EFMigrationsHistory` held no `_AddFeedEntryRevisions` row (newest was `20260918080027_AddCalendarSubscriptions`); Events 43 rows, Quests 62 rows. This task applied the migration with `dotnet ef database update` (forward only, application not started). After: newest MigrationId `20260930161610_AddFeedEntryRevisions`; `FeedRevision` and `FeedRevisedAt` present on both tables, non-null, `FeedRevision` column default `((1))`; `Events WHERE FeedRevision <> 2 OR FeedRevisedAt <= CreatedAt` = 0 and the same query on `Quests` = 0, so every pre-existing row was bumped exactly once and stamped after its creation; Events 43 and Quests 62 rows unchanged; `FeedRevision < 1 OR FeedRevisedAt < CreatedAt` over both tables = 0. No subscription token column was read.

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| An entry Google already holds corrects itself | CALTZ-09 | Google fetches the feed from its own servers; no local or LAN address can reach it | Before deploy, note one entry on the friend's Google Calendar (e.g. quest 12039 on 2 Oct 2026, showing 20:00). After deploy and Google's next refresh, record whether it reads 18:00. If not, remove and re-add the subscription once (D-08) and record that; this is not a phase failure. |
| New entries show 18:00 on Google | CALTZ-09 | Same as above | After the correction, create or find a new entry and confirm it reads 18:00 on the friend's phone. |
| Apple Calendar does not regress | CALTZ-09 | Client rendering cannot be pinned by byte tests | On the operator's iPhone (Apple Calendar, direct subscription), confirm the same event and quest still read 18:00. |
| Live bytes match the contract | CALTZ-09 | Production-only data | Optionally `curl` the live feed and check `DTSTART;TZID=Europe/Amsterdam:20261002T180000` and `SEQUENCE:1` for quest 12039. |
| Refresh latency recorded, not promised | CALTZ-09 | Observed behaviour only | Record how long Google took to refresh. Do not put any latency figure into user-facing copy. |
| A rescheduled entry moves in Google and Apple Calendar after the v5.3.3 deploy (gap G-88-4, 88-UAT.md test 4) | CALTZ-09 | Google and Apple fetch the feed from their own servers and phones, which no localhost address can reach; whether a client applies a higher sequence number is client behaviour byte tests cannot show; which client made a fetch is only visible on the per-device subscription row | **✅ Passed 2026-09-30 on v5.3.3 (88-UAT.md test 7).** Apple Calendar on the operator's iPhone (direct "Subscribed" calendar) updated an entry it already held ("GameNight: Blood on the Clocktower") after it was changed in production. Production and the iPhone's own address both served every entry at `SEQUENCE:2` or higher, with `LAST-MODIFIED`. Google was not re-checked separately; its behaviour is recorded in tests 1–2, where a new subscription address was needed for entries it already held. No refresh latency was measured or promised. Original instruction: after deploying v5.3.3, record whether the entry that stayed stale in test 4 now shows its current time (the one-time bump sends every entry out at `SEQUENCE:2`). Reschedule an entry both Google Calendar and Apple Calendar hold; on the Profile page confirm "Last fetched" advanced on a subscription row only the iPhone uses, then check Apple Calendar; after Google's next refresh check Google Calendar. Record the app, the phone's OS and the date for each entry checked, and any refresh delay as an observation only. Never paste a subscription address into a log, issue or chat. |

**Known limitations** (stated in `.claude/architecture.md` too): renaming a board does not re-signal existing calendar entries; each entry picks up the new board name on its next real revision. A change to the configured quest session length or board zone likewise reaches clients only with each entry's next revision. No refresh latency is promised anywhere.

---

## Validation Sign-Off

- [x] All tasks have `<automated>` verify or Wave 0 dependencies
- [x] Sampling continuity: no 3 consecutive tasks without automated verify
- [x] Wave 0 covers all MISSING references
- [x] No watch-mode flags
- [x] Feedback latency < 30s
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** validated 2026-09-30

---

## Validation Audit 2026-09-30

| Metric | Count |
|--------|-------|
| Gaps found | 0 |
| Resolved | 0 |
| Escalated | 0 |

All nine automated requirements (CALTZ-01 to CALTZ-08, CALTZ-10) are COVERED by the tests in the Per-Task Map. The full suite is green at the audited HEAD (734 unit, 937 integration, 0 failed), including the five tests added with the code-review fixes: the board-clock feed window and the host-zone-free DTSTAMP, with their source guards. The Linux container run matched Windows at 129/129. No gaps were found, so no Nyquist auditor was spawned.

CALTZ-09 is manual by nature and was verified on 2026-09-30 after the v5.3.2 deploy (88-UAT.md tests 1–3, 6):
- Production endpoint: serves the zoned document, and all 82 timed lines agree with ICU's Europe/Amsterdam rules.
- Google Calendar: shows the board's times. For entries it already held, Google needed a new subscription address; re-adding the same address was not enough, because Google caches each subscribed calendar by its address.
- Apple Calendar: with Time Zone Override set to London, an 18:00 game night reads 17:00, so the iPhone reads the zoned entries.
- Refresh latency: not measured. Google was not left long enough to show whether an existing subscription corrects itself, so no latency figure exists to put in user-facing copy.

The optional reschedule check (88-UAT.md test 4) is still open. It is not a CALTZ-09 criterion.
