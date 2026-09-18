---
phase: 85
slug: one-shot-quests-in-the-calendar-feed
# status lifecycle: draft (seeded by plan-phase) → validated (set by validate-phase §6)
# audit-milestone §5.5 distinguishes NOT-VALIDATED (draft) from PARTIAL (validated + nyquist_compliant: false) (#2117)
status: draft
nyquist_compliant: false
wave_0_complete: false
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
| TBD | TBD | TBD | D-01 (both branches) | — | A DM-only quest (no signup row) and a signup-only quest both appear; a quest where the reader is both DM **and** holds a selected signup appears exactly once (one VEVENT, one UID) | unit (writer) + integration (query) | `dotnet test QuestBoard.IntegrationTests --filter CalendarSubscriptionQuestFeedTests` | ❌ W0 | ⬜ pending |
| TBD | TBD | TBD | D-02 / D-03 | — | A waitlisted (`IsSelected == false`) signup never appears; Spectator and AssistantDM signups appear exactly like Player | integration | same file | ❌ W0 | ⬜ pending |
| TBD | TBD | TBD | D-04 | — | A `DungeonMasterSession` quest still appears for a seated player | integration | same file | ❌ W0 | ⬜ pending |
| TBD | TBD | TBD | D-05 | — | Timed entry; `DTEND` exactly `QuestDurationHours` after `DTSTART`; never an all-day entry | unit (writer, golden-byte) | `dotnet test QuestBoard.UnitTests --filter CalendarFeedWriterTests` | ❌ W0 (extend) | ⬜ pending |
| TBD | TBD | TBD | D-06 / D-07 / D-08 | — | `TRANSP:TRANSPARENT`; `SUMMARY` is `[Board] Title` with no quest marker, no `(DM)` suffix, and **never** a `(maybe)`/`(declined)` suffix regardless of `Availability`'s value | unit (writer) | same command | ❌ W0 (extend) | ⬜ pending |
| TBD | TBD | TBD | D-09 | — | Un-finalizing, deleting, moving the finalized date, or losing the seat removes the quest at the next fetch; a closed quest never reaches the query | integration | `dotnet test QuestBoard.IntegrationTests --filter CalendarSubscriptionQuestFeedTests` | ❌ W0 | ⬜ pending |
| TBD | TBD | TBD | D-10 | — | A quest just inside/outside the shared `MonthsBack`/`MonthsAhead` window is included/excluded identically to an event | integration | same file | ❌ W0 | ⬜ pending |
| TBD | TBD | TBD | Board-type narrowing | T-85-BOARDTYPE | A finalized, signed-up quest on a **Campaign** board never appears, even though the reader is a member | integration, two-board-type | same file | ❌ W0 | ⬜ pending |
| TBD | TBD | TBD | Tenant isolation (second-layer re-check) | T-85-TENANT | A quest on a board the reader is not a member of never appears; a board the reader left disappears on the next fetch, and a surviving foreign row raises `LogError` | integration, two-group | same file | ❌ W0 | ⬜ pending |
| TBD | TBD | TBD | UID collision | T-85-UID | An event and a quest sharing the same integer id produce two distinct, non-colliding UIDs | unit (writer) | `dotnet test QuestBoard.UnitTests --filter CalendarFeedWriterTests` | ❌ W0 (extend) | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `QuestBoard.IntegrationTests/Tests/CalendarSubscriptionQuestFeedTests.cs` — **new file**, mirroring `CalendarSubscriptionFeedTests.cs`'s structure and copying its documented InMemory-provider caveat doc-comment (it applies identically here). Covers D-01 dedup, D-02/D-03/D-04 predicates, D-09 disappearance cases, D-10 window edges, board-type narrowing, and tenant isolation.
- [ ] `QuestBoard.UnitTests/Services/CalendarFeedWriterTests.cs` — **extend, not replace**, with Quest-source cases: configurable duration, the no-suffix-ever guarantee (including an explicit `VoteType.No`-with-`Source.Quest` case proving the enum-default landmine is fixed), and UID namespacing against a colliding numeric id.
- [ ] No framework install needed — xUnit v3 / FluentAssertions are already solution-wide dependencies.

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Relational SQL translation of the quest predicate | D-01 | The integration suite runs on the EF Core **InMemory** provider, which cannot prove a LINQ shape translates to SQL Server. This is an inherited gap from Phases 82 and 84, not new to this phase — it must be **stated**, not silently carried. | Run the app against the real SQL Server (`dotnet run --project QuestBoard.Service`), fetch the feed URL for a user seated on a one-shot quest, and confirm no `InvalidOperationException` about client evaluation and that the quest appears. |
| Real calendar-client rendering | D-05, D-06, D-07 | Unit and markup tests cannot prove a phone renders the feed correctly. | **Inherited from Phase 84 and still deferred** — this phase does not close it and must not claim to. See `85-CONTEXT.md`, Inherited assumptions. |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 60s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
