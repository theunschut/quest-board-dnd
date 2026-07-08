---
phase: 36
slug: campaign-quest-posting-closing
status: planned
nyquist_compliant: true
wave_0_complete: false
created: 2026-07-03
---

# Phase 36 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xunit.v3 (3.2.2) + FluentAssertions (8.10.0) + NSubstitute (5.3.0) |
| **Config file** | `QuestBoard.UnitTests/QuestBoard.UnitTests.csproj`, `QuestBoard.IntegrationTests/QuestBoard.IntegrationTests.csproj` |
| **Quick run command** | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~QuestServiceTests"` |
| **Full suite command** | `dotnet test` |
| **Estimated runtime** | ~60 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~QuestServiceTests"`
- **After every plan wave:** Run `dotnet test` (full suite — unit + integration)
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** 60 seconds

---

## Per-Task Verification Map

Wave 0 test scaffolding lives in Plan 01 (Task 2, TestDataHelper) and Plan 02 (Task 1, failing unit tests — RED). The RED→GREEN cycle is Plan 02 Task 1 → Task 2.

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 36-01-T2 | 36-01 | 1 | CQUEST-03/04/05/06 | T-36-01 | Test-infra extension: seed a campaign group + closed campaign quest (`TestDataHelper`); migration `Down()` reverses cleanly | integration (scaffold) | `dotnet build QuestBoard.IntegrationTests/QuestBoard.IntegrationTests.csproj` | ❌ W0 → created here | ⬜ pending |
| 36-02-T1 | 36-02 | 2 | CQUEST-03/04/05/06 | T-36-03 | RED: failing unit tests for Close/Reopen delegation, zero-email, Quest Log OR-branch | unit | `dotnet build QuestBoard.UnitTests/QuestBoard.UnitTests.csproj` (expected to fail — RED) | ❌ W0 → created here | ⬜ pending |
| 36-02-T2 | 36-02 | 2 | CQUEST-03 | T-36-04 | Close hides quest from active board immediately (`!IsClosed` AND on board filters) | unit | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~QuestServiceTests"` | ✅ (GREEN after T2) | ⬜ pending |
| 36-02-T2 | 36-02 | 2 | CQUEST-04 | T-36-04 | Reopen restores quest to active board immediately | unit | Same filter | ✅ | ⬜ pending |
| 36-02-T2 | 36-02 | 2 | CQUEST-05 | — | Closed campaign quest appears in Quest Log immediately (no next-day wait); one-shot `FinalizedDate` next-day filter unchanged | unit | Same filter (`GetCompletedQuestsAsync` OR-branch tests) | ✅ | ⬜ pending |
| 36-02-T2 | 36-02 | 2 | CQUEST-06 | T-36-03 | No email on close/reopen — `IQuestEmailDispatcher` mock receives zero calls; `CloseQuestAsync`/`ReopenQuestAsync` never reference `dispatcher` | unit | Same filter (`_dispatcher.DidNotReceiveWithAnyArgs()`) | ✅ | ⬜ pending |
| 36-03-T3 | 36-03 | 3 | CQUEST-01 | T-36-07 | Campaign quest Create succeeds with no `ProposedDates`; CR/TotalPlayerCount/DMSession default server-side; `BoardType` resolved server-side (never bound) | integration | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~QuestCloseTests"` | ❌ W0 → created here | ⬜ pending |
| 36-03-T3 | 36-03 | 3 | CQUEST-03 | T-36-05 | Only owning DM or group Admin can close (`IsQuestOwner` `Id`-based, mirrors `Finalize`); non-owner → 403 | integration | Same filter | ❌ W0 → created here | ⬜ pending |
| 36-03-T3 | 36-03 | 3 | CQUEST-04 | T-36-05 | Same ownership check as Close for Reopen; non-owner → 403 | integration | Same filter | ❌ W0 → created here | ⬜ pending |
| 36-03-T3 | 36-03 | 3 | CQUEST-06 | T-36-06 | `[ValidateAntiForgeryToken]` present on both Close/Reopen POST actions — missing-token POST rejected | integration | Same filter (CSRF assertion) | ❌ W0 → created here | ⬜ pending |
| 36-03-T2 | 36-03 | 3 | CQUEST-05 | — | Quest Log Details/UpdateRecap guards admit closed campaign quests (no 404/BadRequest) | integration (via full suite) | `dotnet test` | (covered by suite) | ⬜ pending |
| 36-04-T3 | 36-04 | 4 | CQUEST-01/02/03/04 | T-36-06 | Human-verify: campaign board/Manage/Details/Create render correctly (Open/Closed seal, no CR/signup/date-voting, Close/Reopen buttons, stripped Create); one-shot unregressed | human-check | manual (blocking checkpoint) + `dotnet test` regression | — | ⬜ pending |
| 36-05-T2 | 36-05 | 4 | CQUEST-05 | T-36-10 | Human-verify: campaign Quest Log entry (no CR/Adventurers, ClosedDate, immediate) + recap flow; one-shot unregressed | human-check | manual (blocking checkpoint) + `dotnet test` regression | — | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*
*Task ID format: `{plan}-T{taskNumber}` (e.g. `36-02-T1` = Plan 02, Task 1).*

---

## Wave 0 Requirements

Wave 0 scaffolding is absorbed into Plan 01 (Task 2) and Plan 02 (Task 1) rather than a standalone plan:

- [x] `QuestBoard.IntegrationTests/Helpers/TestDataHelper.cs` — `CreateTestQuestAsync` gains `isClosed`/`closedDate`/`groupId` optional params + new `SeedCampaignGroupAsync` (Plan 01, Task 2)
- [x] `QuestBoard.UnitTests/Services/QuestServiceTests.cs` — new `CloseQuestAsync`/`ReopenQuestAsync`/`GetCompletedQuestsAsync` failing tests authored first (Plan 02, Task 1 — RED)
- [x] `QuestBoard.IntegrationTests/Controllers/QuestCloseTests.cs` — new Close/Reopen authz + CSRF + campaign-create test file (Plan 03, Task 3)
- [x] A campaign-type `GroupEntity` seed (`SeedCampaignGroupAsync`, id 2) available to integration fixtures (Plan 01, Task 2)
- No new framework install needed — xunit.v3/FluentAssertions/NSubstitute already present.

---

## Manual-Only Verifications

The two human-verify checkpoints (Plan 04 Task 3, Plan 05 Task 2) confirm the presentational campaign rendering (CR-badge/signup-line removal, wax-seal relabel, Close/Reopen button placement, Quest Log card simplification) on desktop + mobile. Every underlying behavior has automated coverage; the checkpoints verify visual/interaction fidelity only, backed by a full-suite regression run.

---

## Validation Sign-Off

- [x] All tasks have `<automated>` verify or a human-verify checkpoint (both UI plans pair the checkpoint with a `dotnet test` regression run)
- [x] Sampling continuity: no 3 consecutive tasks without automated verify
- [x] Wave 0 covers all MISSING references (absorbed into Plans 01/02/03)
- [x] No watch-mode flags
- [x] Feedback latency < 60s
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** planned 2026-07-03
