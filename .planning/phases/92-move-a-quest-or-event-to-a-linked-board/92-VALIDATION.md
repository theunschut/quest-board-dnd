---
phase: 92
slug: move-a-quest-or-event-to-a-linked-board
# status lifecycle: draft (seeded by plan-phase) → validated (set by validate-phase §6)
# audit-milestone §5.5 distinguishes NOT-VALIDATED (draft) from PARTIAL (validated + nyquist_compliant: false) (#2117)
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-10-18
---

# Phase 92 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution. Derived from
> `92-RESEARCH.md` § Validation Architecture.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xunit.v3 3.2.2 + FluentAssertions 8.10.0 + NSubstitute 5.3.0, .NET 10 |
| **Config file** | `QuestBoard.IntegrationTests/xunit.runner.json` (serial: `parallelizeAssembly: false`, `parallelizeTestCollections: false`); unit tests have none |
| **Quick run command** | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~BoardMove|FullyQualifiedName~Architecture"` |
| **Full suite command** | `dotnet test` (repo root; both test projects) |
| **Estimated runtime** | ~60 seconds quick, several minutes full |

---

## Sampling Rate

- **After every task commit:** Run the quick run command
- **After every plan wave:** Run `dotnet test`
- **Before `/gsd-verify-work`:** Full suite must be green
- **Max feedback latency:** 120 seconds

---

## Per-Task Verification Map

Filled in by the planner and kept current by the executor. Each D-21 proof and the D-20 single-save
proof must map to at least one task.

Task ids are `92-{plan}-{task}`. Every command runs from the repository root (`cd "$(git rev-parse --show-toplevel)"`); `U` = `dotnet test QuestBoard.UnitTests`, `I` = `dotnet test QuestBoard.IntegrationTests`.

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 92-01-01 | 01 | 1 | D-21.3 (quest), D-02, D-18, D-20 (tracer) | T-92-01, T-92-02, T-92-04, T-92-05, T-92-06 | Confirm POST moves the quest in one tracked save; FeedRevision +1; 404 on source, 200 on target; outside-set and self targets refused | integration (end to end) | `I --filter "FullyQualifiedName~BoardMoveApply"` | ❌ W0 | ⬜ pending |
| 92-02-01 | 02 | 1 | D-17 (templates), UI-SPEC email | T-92-14 | QuestMoved renders three audiences, wall-clock date, encoded title; WaitlistPromoted unchanged without MovedToBoardName | integration | `I --filter "FullyQualifiedName~QuestMovedRender"` | ❌ W0 | ⬜ pending |
| 92-02-02 | 02 | 1 | D-06 / D-17 (plumbing), D-21.7 / D-21.12 (harness) | T-92-10, T-92-12 | One job per recipient under the given board id; after-move waitlist entry point; capturing factory | unit + integration | `U --filter "FullyQualifiedName~BoardMoveMail" && I --filter "FullyQualifiedName~BoardMoveMail"` | ❌ W0 | ⬜ pending |
| 92-03-01 | 03 | 2 | D-21.2 (unit), D-21.8 / D-21.10 (rules), D-15 | T-92-02, T-92-08, T-92-09 | Eligibility rule for quests and events on the board clock; target rules; stale stamp → Changed with no write; closed, played and stale-stamp POSTs leave the quest on its board | unit + integration | `U --filter "FullyQualifiedName~BoardMoveEligibility\|FullyQualifiedName~BoardMoveQuestPreview" && I --filter "FullyQualifiedName~BoardMoveApply"` | ❌ W0 | ⬜ pending |
| 92-04-01 | 04 | 3 | D-14, D-13, UI-SPEC page frame | T-92-16 | Page states (one board, picker, tampered, blocked, unlinked) on desktop and mobile; encoded user text | integration | `I --filter "FullyQualifiedName~BoardMovePage"` | ❌ W0 | ⬜ pending |
| 92-04-02 | 04 | 3 | D-21.1, D-21.2, D-15 | T-92-15, T-92-02, T-92-08 | Admin/DM of both boards, Player and anonymous refused on Move GET and POST, nothing written; outside-set and self targets refused on direct POST; no set / set of one refused; stale preview re-shown with HTTP 200 | integration | `I --filter "FullyQualifiedName~BoardMoveAuthorization\|FullyQualifiedName~BoardMoveTarget"` | ❌ W0 | ⬜ pending |
| 92-05-01 | 05 | 3 | D-21.3 / D-21.4 / D-21.5 / D-21.9, D-07, D-20 | T-92-04, T-92-21 | Exactly one SavedChanges; kept rows intact with CharacterId null; dropped signups and votes gone; both chain links null; Changed leaves every row untouched | unit | `U --filter "FullyQualifiedName~BoardMoveQuestRepository"` | ❌ W0 | ⬜ pending |
| 92-05-02 | 05 | 3 | D-21.6, D-21.7 (selection) | T-92-19, T-92-23 | DM rule incl. SuperAdmin DM passes; waitlist selection; parity with `GetTopWaitlistedCandidateAsync` | unit | `U --filter "FullyQualifiedName~BoardMoveQuestPlan\|FullyQualifiedName~BoardMoveWaitlistParity"` | ❌ W0 | ⬜ pending |
| 92-05-03 | 05 | 3 | D-21.4 / D-21.5 / D-21.6 / D-21.9 (HTTP), D-21.7 (enqueue), D-21.12 (quest) | T-92-19, T-92-20, T-92-22, T-92-24 | People, DM refusal and chain proven over HTTP; promotion mail enqueued with the target board id; kept/DM/removed mails to confirmed addresses only, never the actor | unit + integration | `I --filter "FullyQualifiedName~BoardMoveQuestPeople"` and `U --filter "FullyQualifiedName~BoardMoveQuestMail" && I --filter "FullyQualifiedName~BoardMoveQuestMail"` | ❌ W0 | ⬜ pending |
| 92-06-01 | 06 | 4 | D-21.11, D-20 (event) | T-92-28 | Campaign Yes rows with UpdatedAt null; non-member rows dropped; kept rows byte-identical; one SavedChanges | unit | `U --filter "FullyQualifiedName~BoardMoveEventRepository"` | ❌ W0 | ⬜ pending |
| 92-06-02 | 06 | 4 | D-21.8 / D-21.10 (event service), D-08 | T-92-25, T-92-26 | Series occurrence and past event blocked; Campaign fill-in only on a Campaign target; no mail dependency | unit | `U --filter "FullyQualifiedName~BoardMoveEventService"` | ❌ W0 | ⬜ pending |
| 92-06-03 | 06 | 4 | D-21.1 / D-21.3 / D-21.8 / D-21.10 / D-21.11 / D-21.12 (event HTTP) | T-92-27 | Event GET/POST refused for Admin/DM; one-off and detached move; series and past refused; cancelled stays cancelled; no job enqueued | integration | `I --filter "FullyQualifiedName~BoardMoveEvent"` | ❌ W0 | ⬜ pending |
| 92-07-01 | 07 | 5 | D-05 / D-06 / D-10 / D-14 / D-18 (rows), UI-SPEC | T-92-29 | Quest consequence rows and names list on both layouts; zero-omitted rows | integration | `I --filter "FullyQualifiedName~BoardMoveViews"` | ❌ W0 | ⬜ pending |
| 92-07-02 | 07 | 5 | D-08 / D-12 (rows), D-21.6 (GET render), UI-SPEC hygiene | T-92-29 | Event rows, DM refusal alert, markup-hygiene source fact | integration | `I --filter "FullyQualifiedName~BoardMoveViews"` | ❌ W0 | ⬜ pending |
| 92-08-01 | 08 | 5 | D-13, D-14 (control state) | T-92-31 | Control state only on a linked board; eligibility from IBoardClock | unit | `U --filter "FullyQualifiedName~BoardMoveControlService"` | ❌ W0 | ⬜ pending |
| 92-08-02 | 08 | 5 | D-21.2 / D-13 (no control), D-01, D-16 (toast) | T-92-31, T-92-32, T-92-33 | No control on unlinked board, set of one or for a non-SuperAdmin; disabled with reason; Moved toast with local link | integration | `I --filter "FullyQualifiedName~BoardMoveControl"` | ❌ W0 | ⬜ pending |
| 92-09-01 | 09 | 6 | D-20 (static), ambient clock, no planning refs | T-92-35 | One `SaveChangesAsync(` per move repository; no bypass; new files on the clock guard; no planning references | unit + integration | `U --filter "FullyQualifiedName~Architecture" && I --filter "FullyQualifiedName~BoardMoveStaticGuard"` | ❌ W0 | ⬜ pending |
| 92-09-02 | 09 | 6 | D-18 / D-19, D-14 / D-16 (walk) | T-92-36, T-92-37 | End-to-end walk on desktop and mobile; one log line with ids and counts and no names or emails; feed SEQUENCE:2 for target subscriber, UID gone for source-only subscriber | integration + full suite | `I --filter "FullyQualifiedName~BoardMoveEndToEnd" && dotnet test` | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

Each test file is created by the task that first needs it (tests-first within the task), so no separate Wave 0 plan exists.

- [ ] `QuestBoard.IntegrationTests/Helpers/BoardMoveSeed.cs` (92-01-01; event helpers 92-06-03) — shared seeding for every board-move test class
- [ ] `QuestBoard.IntegrationTests/Tests/BoardMoveApplyTests.cs` (92-01-01) — the tracer's end-to-end proof
- [ ] `QuestBoard.UnitTests/Extensions/BoardMoveEligibilityTests.cs`, `QuestBoard.UnitTests/Services/BoardMoveQuestPreviewTests.cs` (92-03-01)
- [ ] `QuestBoard.IntegrationTests/Emails/QuestMovedRenderTests.cs` (92-02-01) — three audiences, wall-clock date text, WaitlistPromoted unchanged when the new parameter is null
- [ ] `QuestBoard.IntegrationTests/Helpers/MoveWebApplicationFactory.cs`, `QuestBoard.UnitTests/Services/BoardMoveMailJobTests.cs`, `QuestBoard.IntegrationTests/Tests/BoardMoveMailEnqueueTests.cs` (92-02-02)
- [ ] `QuestBoard.IntegrationTests/Tests/BoardMovePageTests.cs` (92-04-01), `BoardMoveAuthorizationTests.cs`, `BoardMoveTargetTests.cs` (92-04-02)
- [ ] `QuestBoard.UnitTests/Repository/BoardMoveQuestRepositoryTests.cs` (92-05-01) — single save, Changed untouched, D-21.3/4/5/9
- [ ] `QuestBoard.UnitTests/Services/BoardMoveQuestPlanTests.cs`, `QuestBoard.UnitTests/Repository/BoardMoveWaitlistParityTests.cs` (92-05-02)
- [ ] `QuestBoard.UnitTests/Services/BoardMoveQuestMailTests.cs`, `QuestBoard.IntegrationTests/Tests/BoardMoveQuestPeopleTests.cs`, `BoardMoveQuestMailFlowTests.cs` (92-05-03)
- [ ] `QuestBoard.UnitTests/Repository/BoardMoveEventRepositoryTests.cs` (92-06-01), `QuestBoard.UnitTests/Services/BoardMoveEventServiceTests.cs` (92-06-02), `QuestBoard.IntegrationTests/Tests/BoardMoveEventTests.cs` (92-06-03)
- [ ] `QuestBoard.IntegrationTests/Tests/BoardMoveViewsTests.cs` (92-07-01/02)
- [ ] `QuestBoard.UnitTests/Services/BoardMoveControlServiceTests.cs` (92-08-01), `QuestBoard.IntegrationTests/Tests/BoardMoveControlTests.cs` (92-08-02)
- [ ] `QuestBoard.UnitTests/Architecture/BoardMoveRepositorySingleSaveTests.cs`, `QuestBoard.IntegrationTests/Tests/BoardMoveStaticGuardTests.cs` (92-09-01) — exactly one `SaveChangesAsync(` per move repository, no bypass, no planning references
- [ ] `QuestBoard.IntegrationTests/Tests/BoardMoveEndToEndTests.cs` (92-09-02) — walk, log line, feed

No framework install needed.

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Move page at phone width with long board names | UI-SPEC backstop (E2, E5) | Visual layout | At 390px: select and preview heading wrap, confirm button wraps and stays ≥44px, no horizontal scroll |
| Quest Moved email with long title and board name | UI-SPEC backstop (E8) | Visual email render | Open the three Email Preview entries with long names; nothing overlaps the poster's bottom edge |
| Atomicity on real SQL Server | D-20 | InMemory has no transactions or unique indexes | Optional operator spot check against the local SQL Server at localhost:1433 (do not provision it) |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 120s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
