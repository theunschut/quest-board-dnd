---
phase: 85
slug: one-shot-quests-in-the-calendar-feed
# status lifecycle: draft (seeded by plan-phase) → validated (set by validate-phase §6)
# audit-milestone §5.5 distinguishes NOT-VALIDATED (draft) from PARTIAL (validated + nyquist_compliant: false) (#2117)
status: draft
nyquist_compliant: true
wave_0_complete: true
created: 2026-09-18
---

# Phase 85 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.
> Seeded from `85-RESEARCH.md` `## Validation Architecture`. The per-task map is filled once PLAN.md task IDs exist.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit v3 (`xunit.v3`) + FluentAssertions — already solution-wide, unchanged from Phase 84 |
| **Config file** | none dedicated — standard `dotnet test` per-project convention |
| **Quick run command** | `dotnet test QuestBoard.UnitTests --filter CalendarFeedWriterTests` |
| **Full suite command** | `dotnet test` |
| **Estimated runtime** | ~30s quick / full suite dominated by `QuestBoard.IntegrationTests` |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test QuestBoard.UnitTests` — the writer's duration/summary/UID changes are pure-function and dominate this suite for this phase
- **After every plan wave:** Run `dotnet test` (full solution — catches the new tenant/board-type isolation suite)
- **Before `/gsd-verify-work`:** Full suite must be green. **No new real-device check is required** — this phase inherits Phase 84's deferred real-device gap (`85-CONTEXT.md`, Inherited assumptions) and must not claim it closes that gap.
- **Max feedback latency:** 60 seconds

---

## Per-Task Verification Map

*Filled once PLAN.md task IDs exist. Seeded from the decision→test map in `85-RESEARCH.md`.*

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 85-02-T1 | 85-02 | 1 | QUESTFEED-01 / QUESTFEED-02 | T-85-03 | A seated reader's finalized one-shot quest reaches the live feed as a correct VEVENT, end to end through repository, service and writer | integration (end-to-end tracer) | `dotnet test QuestBoard.IntegrationTests --filter CalendarSubscriptionQuestFeedTests` | ✅ | ✅ green |
| 85-02-T2 | 85-02 | 1 | QUESTFEED-09 | — | The application refuses to start when the configured quest session length is below one hour | unit (options validation) | `dotnet test QuestBoard.UnitTests --filter CalendarFeedOptionsValidationTests` | ✅ | ✅ green |
| 85-02-T3 | 85-02 | 1 | QUESTFEED-08 / QUESTFEED-09 (D-05) | — | Timed entry; `DTEND` exactly `QuestDurationHours` after `DTSTART`; never an all-day entry | unit (writer, golden-byte) | `dotnet test QuestBoard.UnitTests --filter CalendarFeedWriterTests` | ✅ | ✅ green |
| 85-02-T3 | 85-02 | 1 | QUESTFEED-10 / QUESTFEED-11 / QUESTFEED-12 (D-06 / D-07 / D-08) | — | `TRANSP:TRANSPARENT`; `SUMMARY` is `[Board] Title` with no quest marker, no `(DM)` suffix, and **never** a `(maybe)`/`(declined)` suffix regardless of `Availability`'s value | unit (writer) | same command | ✅ | ✅ green |
| 85-02-T3 | 85-02 | 1 | QUESTFEED-17 (UID collision) | T-85-04 | An event and a quest sharing the same integer id produce two distinct, non-colliding UIDs | unit (writer) | `dotnet test QuestBoard.UnitTests --filter CalendarFeedWriterTests` | ✅ | ✅ green |
| 85-03-T1 | 85-03 | 2 | QUESTFEED-03 / QUESTFEED-04 (D-01, both branches) | — | A DM-only quest (no signup row) and a signup-only quest both appear; a quest where the reader is both DM **and** holds a selected signup appears exactly once (one VEVENT, one UID) | unit (writer) + integration (query) | `dotnet test QuestBoard.IntegrationTests --filter CalendarSubscriptionQuestFeedTests` | ✅ | ✅ green |
| 85-03-T2 | 85-03 | 2 | QUESTFEED-05 / QUESTFEED-06 (D-02 / D-03) | — | A waitlisted (`IsSelected == false`) signup never appears; Spectator and AssistantDM signups appear exactly like Player | integration | same file | ✅ | ✅ green |
| 85-03-T2 | 85-03 | 2 | QUESTFEED-07 (D-04) | — | A `DungeonMasterSession` quest still appears for a seated player | integration | same file | ✅ | ✅ green |
| 85-04-T1 | 85-04 | 3 | QUESTFEED-13 (D-09) | — | Un-finalizing, deleting, moving the finalized date, or losing the seat removes the quest at the next fetch; a closed quest never reaches the query | integration | `dotnet test QuestBoard.IntegrationTests --filter CalendarSubscriptionQuestFeedTests` | ✅ | ✅ green |
| 85-04-T2 | 85-04 | 3 | QUESTFEED-14 (D-10) | — | A quest just inside/outside the shared `MonthsBack`/`MonthsAhead` window is included/excluded identically to an event | integration | same file | ✅ | ✅ green |
| 85-05-T1 | 85-05 | 4 | QUESTFEED-15 (Board-type narrowing) | T-85-02 | A finalized, signed-up quest on a **Campaign** board never appears, even though the reader is a member | integration, two-board-type | same file | ✅ | ✅ green |
| 85-05-T1 | 85-05 | 4 | QUESTFEED-16 (Tenant isolation, second-layer re-check) | T-85-01 | A quest on a board the reader is not a member of never appears; a board the reader left disappears on the next fetch, and a surviving foreign row raises `LogError` | integration, two-group | same file | ✅ | ✅ green |
| 85-05-T2 | 85-05 | 4 | QUESTFEED-16 | T-85-01 | A quest row that survives the feed query's predicate but falls outside the reader's one-shot board set is dropped before the response and recorded as an error in the application log | unit (second-layer re-check) | `dotnet test QuestBoard.UnitTests --filter CalendarSubscriptionQuestRecheckTests` | ✅ | ✅ green |
| 85-05-T3 | 85-05 | 4 | QUESTFEED-18 | — | The combined document orders every entry by date and then start time regardless of source, and a fetch with no qualifying quest produces the same document the event-only feed produced before this phase | integration | `dotnet test QuestBoard.IntegrationTests --filter CalendarSubscriptionQuestFeedTests` | ✅ | ✅ green |
| 85-06-T1 | 85-06 | 5 | QUESTFEED-08 (copy guard, D-05) | T-85-15 | Application copy never presents the invented four-hour session length as a fact the board knows, enforced by the same forbidden-claim theory that already covers the equivalent event claim | integration (static guard, data-driven) | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~NeitherLayout_MakesAForbiddenClaim"` | ✅ | ✅ green |

*Status legend: ⬜=not yet run · ✅=green · ❌=red · ⚠️=flaky. Every row above reads ✅ green.*

---

## Wave 0 Requirements

- [x] `QuestBoard.IntegrationTests/Tests/CalendarSubscriptionQuestFeedTests.cs` — **new file created by 85-02**, mirroring `CalendarSubscriptionFeedTests.cs`'s structure and copying its documented InMemory-provider caveat doc-comment (it applies identically here). Covers D-01 dedup, D-02/D-03/D-04 predicates, D-09 disappearance cases, D-10 window edges, board-type narrowing, and tenant isolation. **Extended by 85-03, 85-04 and 85-05** as each plan's facts land. 19 facts total.
- [x] `QuestBoard.UnitTests/Services/CalendarFeedWriterTests.cs` — **extend, not replace, made by 85-02**, with Quest-source cases: configurable duration, the no-suffix-ever guarantee (including an explicit `VoteType.No`-with-`Source.Quest` case proving the enum-default landmine is fixed), and UID namespacing against a colliding numeric id. 7 new facts.
- [x] `QuestBoard.UnitTests` options-validation suite — **extension made by 85-02**, pinning the refuse-to-start guard for a quest session length configured below one hour. 4 new facts.
- [x] `QuestBoard.UnitTests/Services/CalendarSubscriptionQuestRecheckTests.cs` — **new file created by 85-05**, pinning the second-layer re-check's drop-and-log branch for a quest row that survives the query predicate but falls outside the reader's one-shot board set. 4 facts.
- [x] No framework install needed — xUnit v3 / FluentAssertions are already solution-wide dependencies.
- [x] `QuestBoard.IntegrationTests/Tests/CalendarSubscriptionStaticGuardTests.cs` — **extended by 85-06**, adding 5 forbidden-claim cases pinning the invented four-hour session length so no future copy presents it as known.

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Relational SQL translation of the quest predicate | D-01 | The integration suite runs on the EF Core **InMemory** provider, which cannot prove a LINQ shape translates to SQL Server. This is an inherited gap from Phases 82 and 84, and this is the **third consecutive phase** to defer it rather than close it — closing it means standing up relational test infrastructure (a real SQL Server test fixture), which is larger than any one phase and remains out of scope here. | Run the app against the real SQL Server (`dotnet run --project QuestBoard.Service`, connection string in `appsettings.json`), sign in as a user holding a confirmed seat or Dungeon Master role on a finalized, one-shot-board quest, fetch `GET /feeds/calendar/{feedToken}.ics` for that user's subscription, and confirm no `InvalidOperationException` about client-side evaluation and that the quest's VEVENT appears in the response body. The predicate under test is `QuestRepository.GetFeedQuestsForUserAsync`, invoked from `CalendarSubscriptionService.GetFeedAsync`. |
| Real calendar-client rendering | D-05, D-06, D-07 | Unit and markup tests cannot prove a phone renders the feed correctly. | **Inherited from Phase 84 and still not closed by this phase.** No output of this phase observes refresh latency, in-place update, or what a client does when an entry disappears between polls — and the silent-disappearance decision (D-09) rests on exactly that last, unobserved behaviour. See `85-CONTEXT.md`, Inherited assumptions, and Phase 84's own deferred real-device check. |

---

## Validation Sign-Off

- [x] All tasks have `<automated>` verify or Wave 0 dependencies
- [x] Sampling continuity: no 3 consecutive tasks without automated verify
- [x] Wave 0 covers all MISSING references
- [x] No watch-mode flags
- [x] Feedback latency < 60s
- [x] `nyquist_compliant: true` set in frontmatter

This phase added 39 automated facts across five test classes, each re-run individually below against the shipped code and each exiting 0:

- `CalendarSubscriptionQuestFeedTests` — 19 facts (the tracer plus 85-03's six, 85-04's six, and 85-05's six): `dotnet test QuestBoard.IntegrationTests --filter CalendarSubscriptionQuestFeedTests`
- `CalendarFeedOptionsValidationTests` — 4 new facts: `dotnet test QuestBoard.UnitTests --filter CalendarFeedOptionsValidationTests`
- `CalendarFeedWriterTests` — 7 new facts: `dotnet test QuestBoard.UnitTests --filter CalendarFeedWriterTests`
- `CalendarSubscriptionQuestRecheckTests` — 4 facts, new file: `dotnet test QuestBoard.UnitTests --filter CalendarSubscriptionQuestRecheckTests`
- `CalendarSubscriptionStaticGuardTests`'s forbidden-claim theory — 5 new cases (19 total, 14 pre-existing from Phase 84): `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~NeitherLayout_MakesAForbiddenClaim"`

Observed before any tick above was written: `dotnet test QuestBoard.UnitTests` — 525/525 passed. `dotnet test QuestBoard.IntegrationTests` — 838/839 passed, with exactly one pre-existing, documented, out-of-scope failure (`CalendarSubscriptionStaticGuardTests.NoPlanningOrTrackingReference_ReachedTheSourceTree`, a Linux-only apphost-name collision in a helper this phase's Task 1 was explicitly instructed not to touch — see `deferred-items.md` item 1). This failure predates this phase (introduced in Phase 84, commit `78aa5286`) and is orthogonal to every fact this phase added; every command listed above, and the full `QuestBoard.UnitTests` run, exits 0 on its own.

**Approval:** pending
