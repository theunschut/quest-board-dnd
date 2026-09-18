---
phase: 85-one-shot-quests-in-the-calendar-feed
verified: 2026-09-18T17:26:43Z
status: passed
score: 18/18 must-haves verified
behavior_unverified: 0
overrides_applied: 0
---

# Phase 85: One-Shot Quests in the Calendar Feed Verification Report

**Phase Goal:** The same subscription also carries the quest sessions the reader is actually part of — from their one-shot boards only — so a phone calendar shows the night they are playing, not just the board's informational events.

**Verified:** 2026-09-18T17:26:43Z
**Status:** passed
**Re-verification:** No — initial verification

## Goal Achievement

### Observable Truths

All 18 QUESTFEED requirements were checked against the actual shipped code (`QuestBoard.Repository/QuestRepository.cs`, `QuestBoard.Domain/Services/CalendarSubscriptionService.cs`, `QuestBoard.Domain/Services/CalendarFeedWriter.cs`, `QuestBoard.Domain/Models/CalendarFeedOptions.cs`, `QuestBoard.Domain/Enums/CalendarFeedSource.cs`) and against a fresh, independent run of every named test — not against SUMMARY.md's claims.

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | QUESTFEED-01: same address carries quests, no new endpoint/page/control | ✓ VERIFIED | `CalendarSubscriptionService.GetFeedAsync` (unchanged signature/route) grew a quest branch inline; no new controller action, route, or Profile control exists anywhere in `QuestBoard.Service`. Confirmed via source read and `git diff --stat` history (only test/domain/repository files touched). |
| 2 | QUESTFEED-02: confirmed-seat quest on one-shot board reaches the feed | ✓ VERIFIED | `QuestRepository.GetFeedQuestsForUserAsync` line ~292: `q.PlayerSignups.Any(ps => ps.PlayerId == userId && ps.IsSelected)`. Test `Feed_ServesASeatedReadersFinalizedOneShotQuest_ToAnonymousCaller` passes (re-run independently: 19/19 green in this file). |
| 3 | QUESTFEED-03: DM-owned quest with no signup row reaches the feed | ✓ VERIFIED | Same query, `\|\| q.DungeonMasterId == userId`. Test `Feed_ServesAFinalizedOneShotQuestTheReaderRunsAsDungeonMaster_WithNoSignupRowAtAll` seeds zero signup rows for the reader and asserts presence — re-run, passes. |
| 4 | QUESTFEED-04: both-routes quest emits exactly one entry/one identifier | ✓ VERIFIED | Query rooted at `Quests` (not `Union`), structurally visits each quest once. Test `Feed_QuestWhereReaderIsBothDungeonMasterAndHoldsAConfirmedSeat_EmitsExactlyOneEntryWithOneIdentifier` asserts three separate counts (event-opener, identifier-occurrence, title-occurrence) all equal to 1 — re-run, passes. |
| 5 | QUESTFEED-05: waitlisted signup excluded, promotion reaches next fetch | ✓ VERIFIED | Predicate requires `ps.IsSelected`. Test `Feed_WaitlistedSignup_StaysOutUntilTheSameRowIsPromotedToAConfirmedSeat` performs two fetches in one body (absent, then flip flag, present) — re-run, passes. This is a genuine before/after transition test, not two independent snapshots. |
| 6 | QUESTFEED-06: all three seat kinds (Player/Spectator/AssistantDM) reach feed identically | ✓ VERIFIED | No `SignupRole` clause in the predicate. Test `Feed_AllThreeSignupRoles_ReachTheFeedIdenticallyWhenTheSeatIsConfirmed` seeds one quest per role in one fetch, asserts 3 event openers and derives seat-kind values from the `SignupRole` enum rather than literals — re-run, passes. |
| 7 | QUESTFEED-07: `DungeonMasterSession`-flagged quest still reaches a seated reader | ✓ VERIFIED | No `DungeonMasterSession` clause in the predicate (confirmed by source read of `QuestRepository.cs`). Test `Feed_QuestFlaggedAsADungeonMasterSession_StillReachesAReaderHoldingAConfirmedSeat` — re-run, passes. |
| 8 | QUESTFEED-08: timed entry, configurable hours, never all-day | ✓ VERIFIED | `CalendarFeedEntry.Duration = TimeSpan.FromHours(options.QuestDurationHours)` set in the service; `StartTime` always populated from `FinalizedDate`, so the writer's `if (entry.StartTime.HasValue)` branch is always taken for quests (confirmed by source read of `CalendarFeedWriter.Write` and `CalendarSubscriptionService.GetFeedAsync`). Writer facts in `CalendarFeedWriterTests` (7 new facts) re-run green as part of the 59/59 unit-test run below. |
| 9 | QUESTFEED-09: session length configurable, refuses to start below 1h | ✓ VERIFIED | `CalendarFeedOptions.IsValid()`: `... && QuestDurationHours >= 1` (source-read confirmed). `CalendarFeedOptionsValidationTests` (4 new facts, including a wiring/refuse-to-start fact) re-run green. |
| 10 | QUESTFEED-10: every quest entry `TRANSP:TRANSPARENT` | ✓ VERIFIED | `AppendTimedEvent` emits `TRANSP:TRANSPARENT` unconditionally for every entry regardless of source (source read of `CalendarFeedWriter.cs` line 81) — one rule, no per-source branch. |
| 11 | QUESTFEED-11: title is `[Board] Title`, no quest/DM marker | ✓ VERIFIED | `BuildSummary`: `"[" + entry.BoardName + "] " + entry.Title` with no source-conditional prefix/suffix beyond the availability gate (source read). No-marker theory in `CalendarFeedWriterTests` parameterized over every `VoteType` value, re-run green. |
| 12 | QUESTFEED-12: never a parenthesised availability suffix on a quest | ✓ VERIFIED | `BuildSummary` gates the suffix switch on `entry.Source == CalendarFeedSource.Event` *before* reading `Availability` (source read, line 121) — closes the `VoteType.No`-default landmine explicitly called out in the plan and confirmed correct by the independent code reviewer (85-REVIEW.md, no Critical findings). |
| 13 | QUESTFEED-13: every disappearance route (un-finalize, delete, seat loss, date moved out of window) removes the quest at the next fetch with no cancellation marker | ✓ VERIFIED | Predicate requires `IsFinalized && FinalizedDate != null` and the window bound; the writer never emits a `STATUS` property anywhere (confirmed absent from `CalendarFeedWriter.cs`). Four transition-style facts (`Feed_UnfinalizedQuest...`, `Feed_DeletedQuest...`, `Feed_QuestWhoseReaderSignupRowIsDeleted...`, `Feed_RescheduledQuest_UpdatesInPlaceWithinTheWindowAndDisappearsOutsideIt`) each fetch twice/thrice in one body and assert the transition — re-run, all pass. The reschedule fact additionally asserts UID byte-identity across the move. |
| 14 | QUESTFEED-14: quests share the event window, no second pair of knobs | ✓ VERIFIED | `windowStart`/`windowEnd` computed once from `options.MonthsBack`/`MonthsAhead` and passed to both the event and quest repository calls (source read, `CalendarSubscriptionService.cs` lines 78-122); no `QuestMonthsBack`-style property exists anywhere in `CalendarFeedOptions.cs`. Two window-bound facts re-run green, deriving dates from the running host's options rather than literals. |
| 15 | QUESTFEED-15: campaign-board quest excluded, that board's events still appear | ✓ VERIFIED | `oneShotGroupIds` filters to `BoardType.OneShot` only and is used *only* for the quest query; `memberGroupIds` (the full membership set) still feeds the event query unchanged (source read, lines 67-86). `Feed_BoardTypeNarrowsQuestsButNotEvents_WithinTheSameFetch` and `Feed_CampaignQuestTheReaderRunsAsDungeonMaster_StaysOutAlongsideAQualifyingOneShotQuest` (proving the DM route too) both re-run green, asserting the campaign quest's absence *and* the campaign event's presence in one fetch. |
| 16 | QUESTFEED-16: non-member board's quest excluded; leaving a board removes its quests; a foreign row surviving the query is dropped and logged as an error | ✓ VERIFIED | `oneShotGroupIds` is derived solely from the fresh per-request `memberships` read (source read). Integration facts `Feed_ConfirmedSeatOnABoardTheReaderNeverJoined_StaysOutAlongsideAQualifyingQuest` and `Feed_LeavingABoard_RemovesItsQuestFromTheVeryNextFetch_WithNoErrorLoggedOnAHealthyFetch` re-run green. The drop-and-log branch itself is unreachable by any real (filtered) repository, so it is separately proven by a dedicated unit suite `CalendarSubscriptionQuestRecheckTests` (4 facts, driving the service directly with a misbehaving fake repository) — re-run, 59/59 unit tests pass including this suite. This is a case where behavior-dependent code is proven by a real, purpose-built test rather than left unverified. |
| 17 | QUESTFEED-17: quest and event sharing a numeric id produce distinct UIDs | ✓ VERIFIED | `BuildUid`: `$"questboard-{source...}-{sourceId}"` — namespaced by enum member name (source read, unchanged from Phase 84, `CalendarFeedSource.Quest` added additively). Writer fact asserting two distinct identifier lines for a colliding numeric id, re-run green. |
| 18 | QUESTFEED-18: merged document ordered by date/time regardless of source; empty-quest fetch byte-identical to the pre-phase event-only document | ✓ VERIFIED | `allEntries = entries.Concat(questEntries).OrderBy(Date).ThenBy(StartTime).ThenBy(Source).ThenBy(SourceId)` (source read, lines 162-167). `Feed_MergedDocument_OrdersEventsAndQuestsByDateThenStartTimeRegardlessOfSource` (exact-sequence assertion, scrambled seed order) and `Feed_EventsOnlyDocument_IsByteIdenticalWhenNoQuestQualifies` (body + ETag byte-equality across two fetches) both re-run green. |

**Score:** 18/18 truths verified (0 present, behavior-unverified)

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `QuestBoard.Domain/Enums/CalendarFeedSource.cs` | `Quest` member added | ✓ VERIFIED | Present, `BuildUid` untouched as instructed |
| `QuestBoard.Domain/Models/CalendarFeedEntry.cs` | `Duration` property | ✓ VERIFIED | Confirmed via `CalendarFeedWriter.cs` consuming `entry.Duration` |
| `QuestBoard.Domain/Models/CalendarFeedOptions.cs` | `QuestDurationHours`, validation | ✓ VERIFIED | Present at line 30, validated at line 34 |
| `QuestBoard.Domain/Interfaces/IQuestRepository.cs` / `QuestBoard.Repository/QuestRepository.cs` | `GetFeedQuestsForUserAsync` | ✓ VERIFIED | Implemented exactly as specified — single `Where`, `IgnoreQueryFilters()`, `AsNoTracking()` |
| `QuestBoard.Domain/Services/CalendarSubscriptionService.cs` | quest branch composition | ✓ VERIFIED | `oneShotGroupIds` derivation, unconditional call, second-layer re-check with `LogError`, projection, merge/order all present |
| `QuestBoard.Domain/Services/CalendarFeedWriter.cs` | source-aware duration + summary gate | ✓ VERIFIED | Both changes present exactly as specified |
| `QuestBoard.IntegrationTests/Tests/CalendarSubscriptionQuestFeedTests.cs` | 19 facts | ✓ VERIFIED | File exists, 19 `[Fact]` methods counted, all 19 pass on independent re-run |
| `QuestBoard.UnitTests/Services/CalendarSubscriptionQuestRecheckTests.cs` | 4 facts | ✓ VERIFIED | File exists, 4 facts counted, all pass on independent re-run |

### Key Link Verification

| From | To | Via | Status | Details |
|------|-----|-----|--------|---------|
| `CalendarSubscriptionService.GetFeedAsync` | `QuestRepository.GetFeedQuestsForUserAsync` | direct call with `oneShotGroupIds`, window bounds | ✓ WIRED | Confirmed by source read and by the passing tracer/expansion facts exercising the live HTTP path end to end |
| `QuestRepository.GetFeedQuestsForUserAsync` | board-id set | `oneShotGroupIds.Contains(q.GroupId)` inside the single `Where` | ✓ WIRED | The board-type and membership predicates cannot be satisfied independently — confirmed by source read and by the tenant-isolation/board-type facts |
| `CalendarFeedWriter.AppendTimedEvent` | `entry.Duration` | `start.Add(entry.Duration)` | ✓ WIRED | Confirmed by source read; duration facts (4h/2h) pass |
| `CalendarFeedWriter.BuildSummary` | `entry.Source` | gate before `Availability` switch | ✓ WIRED | Confirmed by source read; no-marker theory over every `VoteType` value passes |

### Data-Flow Trace (Level 4)

| Artifact | Data Variable | Source | Produces Real Data | Status |
|----------|---------------|--------|---------------------|--------|
| Quest VEVENT | `questEntries` | `questRepository.GetFeedQuestsForUserAsync` → real EF Core query against `DbContext.Quests` | Yes — a real, parameterized query, not a static/mock return | ✓ FLOWING |
| `oneShotGroupIds` | board-type-filtered set | `groupService.GetGroupsForUserAsync` (fresh per-request read) | Yes | ✓ FLOWING |

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| Quest tracer + all 19 quest-feed facts | `dotnet test QuestBoard.IntegrationTests --filter CalendarSubscriptionQuestFeedTests` | 19/19 passed | ✓ PASS |
| Recheck drop-and-log branch | `dotnet test QuestBoard.UnitTests --filter CalendarSubscriptionQuestRecheckTests` (included in a combined filter run) | 4/4 passed (part of 59/59) | ✓ PASS |
| Writer + options unit suites | `dotnet test QuestBoard.UnitTests --filter "CalendarFeedWriterTests\|CalendarFeedOptionsValidationTests\|CalendarSubscriptionQuestRecheckTests"` | 59/59 passed | ✓ PASS |
| Full unit suite | `dotnet test QuestBoard.UnitTests` | 525/525 passed | ✓ PASS |
| Full integration suite | `dotnet test QuestBoard.IntegrationTests` | 838/839 passed (1 pre-existing, documented, Linux-only failure — see below) | ✓ PASS (documented exception) |
| Phase 84 regression: `CalendarSubscriptionFeedTests` | `dotnet test QuestBoard.IntegrationTests --filter CalendarSubscriptionFeedTests` | 29/29 passed | ✓ PASS |
| Phase 84 regression: `ProfileCalendarSubscriptionTests` | `dotnet test QuestBoard.IntegrationTests --filter ProfileCalendarSubscriptionTests` | 28/28 passed | ✓ PASS |
| Forbidden-claim copy guard (incl. 5 new session-length cases) | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~NeitherLayout_MakesAForbiddenClaim"` | 19/19 passed | ✓ PASS |
| Build | `dotnet build` | 0 errors, 22 pre-existing NuGet version-constraint warnings unrelated to this phase | ✓ PASS |
| Planning-ID leak scan | `grep -rnE '\b(D-[0-9]{2}\|QUESTFEED-[0-9]{2}\|CALFEED-[0-9]{2}\|Phase [0-9]{2}\|85-0[0-9])\b' QuestBoard.Domain QuestBoard.Repository QuestBoard.Service QuestBoard.IntegrationTests QuestBoard.UnitTests` | no match | ✓ PASS |

All numbers above were independently reproduced in this verification session, not copied from SUMMARY.md.

### Known, Pre-Existing, Documented Exception (not a phase-85 defect)

`CalendarSubscriptionStaticGuardTests.NoPlanningOrTrackingReference_ReachedTheSourceTree` fails on this Linux host with `DirectoryNotFoundException` because its `ResolveRepoFile` helper collides with the extension-less `QuestBoard.Service` apphost binary that only exists on Linux builds. This is a Phase-84-era bug (commit `78aa5286`), explicitly out of Phase 85's Task 1 scope (the plan instructs not to touch `ResolveRepoFile`), and is recorded in `.planning/WINDOWS.md` and `85-06-SUMMARY.md`'s Known Open Items. Confirmed by independent re-run: this is the *only* failure in the full 839-test integration suite, and every fact this phase added (including the 5 new forbidden-claim cases in the same file) passes when the filter is scoped away from this one pre-existing test.

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|-------------|--------------|--------|----------|
| QUESTFEED-01 through QUESTFEED-18 | 85-01 (minted) / 85-02 through 85-06 (implemented) | See Observable Truths table above | ✓ SATISFIED (all 18) | Cross-referenced against `.planning/REQUIREMENTS.md` (all 18 checked `[x]` and `Complete` in the Traceability table), `.planning/ROADMAP.md` Phase 85 block (18/18 named, 6/6 plans checked), and `85-VALIDATION.md`'s per-task map (every row green). No orphaned requirements — REQUIREMENTS.md maps exactly 18 QUESTFEED ids to Phase 85 and every plan's `requirements:` frontmatter collectively claims all 18. |

No orphaned requirements found: `grep -E "Phase 85"` against REQUIREMENTS.md's traceability table yields exactly the 18 QUESTFEED rows, and every one is claimed by at least one plan's frontmatter (85-01: all 18 as minting; 85-02: 01,02,08,09,10,11,12,17,18; 85-03: 03,04,05,06,07; 85-04: 13,14; 85-05: 15,16,18; 85-06: 08,09 as the copy-guard closure).

### Anti-Patterns Found

None blocking. Two pre-existing Warning-level findings from the independent code review (`85-REVIEW.md`, 0 Critical / 2 Warning / 2 Info) are noted for completeness but do not block goal achievement:

- **WR-01** (`CalendarSubscriptionService.cs:126-134`): the second-layer re-check validates board scope only, not the seat-or-DM predicate itself — a future regression to the repository's seat clause would not be caught by the re-check. This is an inherited shape (the event branch has the identical limitation) and is advisory, not a defect in what shipped.
- **WR-02** (`CalendarSubscriptionService.cs:71-76`): `oneShotGroupIds`'s board-type signal shares its source with the thing it defends, so a `BoardType`-mapping bug upstream would propagate through both the query and the re-check identically. Also advisory, no code change required per the reviewer.
- Both are documented, reasoned, low-likelihood observations about defense-in-depth completeness rather than functional gaps in this phase's shipped behavior. No TBD/FIXME/XXX markers were found in any file this phase touched.

### Human Verification Required

None required to close this phase. Two gaps are deliberately left open by design and are not treated as verification failures per the phase's own explicit scope (confirmed recorded in `85-VALIDATION.md`'s Manual-Only Verifications table, `85-06-SUMMARY.md`'s "What This Phase Does Not Claim," and the ROADMAP.md Phase 85 block's "Gaps this phase does not claim to have closed"):

1. **Relational SQL translation of the quest predicate** — every integration fact runs on the EF Core in-memory provider; a runnable manual compensating check is recorded in `85-VALIDATION.md` (run the app against real SQL Server, fetch a seated user's feed, confirm no client-evaluation exception and the quest appears). Third consecutive phase (82, 84, 85) to defer this, with the reason (relational test infrastructure is larger than this phase) stated each time.
2. **Real-device calendar-client behavior** (refresh latency, in-place update rendering, disappearance behavior) — inherited from Phase 84, still unobserved by design; no claim about it appears anywhere in this phase's shipped code, comments, or ledgers.

Both are correctly recorded as open, not silently dropped, and neither is described anywhere as closed, resolved, or verified — consistent with the phase's own explicit prohibition against doing so.

### Gaps Summary

No gaps found. All 18 QUESTFEED requirements are backed by real, independently-reproduced passing tests and by source code that matches every locked decision (D-01 through D-10) in `85-CONTEXT.md`. The two deliberately-deferred items (relational translation, real-device check) are correctly recorded as open in all three ledgers and are not verification failures — they are named, bounded, reasoned exceptions consistent with the escalation pattern this phase and its two predecessors have followed. The one test failure observed (`NoPlanningOrTrackingReference_ReachedTheSourceTree`) is a pre-existing, Linux-only, Phase-84-era environment quirk, unrelated to any Phase 85 code, and is already tracked in `.planning/WINDOWS.md`.

The phase goal — "the same subscription also carries the quest sessions the reader is actually part of, from their one-shot boards only" — is achieved and independently verified against the live code path, not merely claimed.

---

_Verified: 2026-09-18T17:26:43Z_
_Verifier: Claude (gsd-verifier)_
