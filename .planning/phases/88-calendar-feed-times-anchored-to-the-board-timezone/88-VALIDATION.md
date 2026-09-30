---
phase: "88"
slug: "calendar-feed-times-anchored-to-the-board-timezone"
# status lifecycle: draft (seeded by plan-phase) → validated (set by validate-phase §6)
# audit-milestone §5.5 distinguishes NOT-VALIDATED (draft) from PARTIAL (validated + nyquist_compliant: false) (#2117)
status: draft
nyquist_compliant: false
wave_0_complete: false
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
| **Quick run command** | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~CalendarFeed\|FullyQualifiedName~CalendarSubscriptionQuestRecheck\|FullyQualifiedName~AmbientClockSeamTests"` (103 tests at planning time; 129 once 88-02 and 88-03 land) |
| **Integration filter** | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~CalendarSubscriptionFeedTests\|FullyQualifiedName~CalendarSubscriptionQuestFeedTests\|FullyQualifiedName~BoardTimeZoneHealthCheckTests\|FullyQualifiedName~CalendarFeedBoardZoneHttpTests"` (51 tests at planning time; 55 once 88-03 lands) |
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
| 88-01-T1 | 88-01 | 1 | CALTZ-01 | — | N/A | unit | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~CalendarFeedWriterTests"` | rewrite existing | ⬜ pending |
| 88-02-T1, 88-02-T2, 88-04-T1 | 88-02, 88-04 | 2, 3 | CALTZ-02 | T-88-01 | N/A | unit | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~CalendarFeed"` | ❌ W0 (new facts) | ⬜ pending |
| 88-01-T1, 88-02-T2, 88-03-T2 | 88-01, 88-02, 88-03 | 1, 2 | CALTZ-03 | — | N/A | unit | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~CalendarFeed"` | ❌ W0 (new facts) | ⬜ pending |
| 88-02-T2, 88-03-T1 | 88-02, 88-03 | 2 | CALTZ-04 | T-88-02 | Declared zone never taken from the raw configured string | unit + integration | quick unit command; integration filter | ❌ W0 (new facts) | ⬜ pending |
| 88-02-T1, 88-02-T2, 88-03-T1 | 88-02, 88-03 | 2 | CALTZ-05 | T-88-02, T-88-06 | Declared zone equals resolved zone (UTC fallback declared as UTC) | unit + integration | quick unit command; integration filter | ❌ W0 (new facts) | ⬜ pending |
| 88-02-T3 | 88-02 | 2 | CALTZ-06 | T-88-05 | N/A | unit | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~CalendarFeedWriterTests"` | rewrite existing | ⬜ pending |
| 88-02-T2, 88-03-T1 | 88-02, 88-03 | 2 | CALTZ-07 | — | N/A | unit | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~CalendarFeedWriterTests"` | ❌ W0 (new facts) | ⬜ pending |
| 88-01-T1, 88-02-T1, 88-03-T1 | 88-01, 88-02, 88-03 | 1, 2 | CALTZ-08 | T-88-03 | N/A | unit + integration | quick unit command; integration filter | rewrite existing | ⬜ pending |
| 88-01-T1, 88-02-T1, 88-04-T2 | 88-01, 88-02, 88-04 | 1, 2, 3 | CALTZ-10 | — | N/A | unit | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~AmbientClockSeamTests"` plus writer tests | ✅ exists | ⬜ pending |
| 88-04-T2 | 88-04 | 3 | CALTZ-09 | — | N/A | manual | see Manual-Only Verifications | n/a | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

Existing infrastructure covers all phase requirements: the framework, `FakeBoardClock` (settable `TimeZone`, `IsDegraded`, `Today`, `Now`), the zone-variant host factory pattern from `WallClockUnmovedTests`, and the `Pacific/Auckland` id are already in use.

- [ ] A shared `Amsterdam` `TimeZoneInfo` field in the test classes that construct the writer (added alongside the first rewritten fact, not a separate step) — 88-01 Task 1.
- [x] Optional cross-platform proof: run the quick unit filter inside the `mcr.microsoft.com/dotnet/sdk:10.0` container against the repo so the exact-byte `VTIMEZONE` facts also execute on Linux — planned as 88-04 Task 1. Run 2026-09-30 on mcr.microsoft.com/dotnet/sdk:10.0 against `git archive HEAD` (no volume mount): Windows 129 passed, Linux 129 passed, 0 failed on both.

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| An entry Google already holds corrects itself | CALTZ-09 | Google fetches the feed from its own servers; no local or LAN address can reach it | Before deploy, note one entry on the friend's Google Calendar (e.g. quest 12039 on 2 Oct 2026, showing 20:00). After deploy and Google's next refresh, record whether it reads 18:00. If not, remove and re-add the subscription once (D-08) and record that; this is not a phase failure. |
| New entries show 18:00 on Google | CALTZ-09 | Same as above | After the correction, create or find a new entry and confirm it reads 18:00 on the friend's phone. |
| Apple Calendar does not regress | CALTZ-09 | Client rendering cannot be pinned by byte tests | On the operator's iPhone (Apple Calendar, direct subscription), confirm the same event and quest still read 18:00. |
| Live bytes match the contract | CALTZ-09 | Production-only data | Optionally `curl` the live feed and check `DTSTART;TZID=Europe/Amsterdam:20261002T180000` and `SEQUENCE:1` for quest 12039. |
| Refresh latency recorded, not promised | CALTZ-09 | Observed behaviour only | Record how long Google took to refresh. Do not put any latency figure into user-facing copy. |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 30s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
