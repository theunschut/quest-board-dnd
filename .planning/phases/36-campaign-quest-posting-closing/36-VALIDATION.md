---
phase: 36
slug: campaign-quest-posting-closing
status: draft
nyquist_compliant: false
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

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 36-0x-xx | TBD | 0 | — | — | N/A — Wave 0 test-infra extension | unit/integration | `dotnet test` | ❌ W0 | ⬜ pending |
| 36-0x-xx | TBD | 1 | CQUEST-01 | — | Campaign quest Create succeeds with no `ProposedDates`; `ChallengeRating`/`TotalPlayerCount`/`DungeonMasterSession` are not DM-selectable and default server-side | integration | `dotnet test --filter "FullyQualifiedName~QuestControllerIntegrationTests_Comprehensive"` (extend existing file) | ❌ W0 — new test case | ⬜ pending |
| 36-0x-xx | TBD | 1 | CQUEST-02 | — | Campaign quest Details/Manage render no signup or date-voting section — only quest content + Close/Reopen control | unit/integration | New Razor-rendering/ViewBag assertion (file TBD) | ❌ W0 | ⬜ pending |
| 36-0x-xx | TBD | 1 | CQUEST-03 | T-36-xx (broken access control — non-owner/non-admin closes quest) | Close hides quest from active board immediately; only owning DM or group Admin can close (`IsQuestOwner` `Id`-based check, mirrors `Finalize`) | unit | `QuestServiceTests.cs` — new `CloseQuestAsync_...` tests, mirroring `FinalizeQuestAsync_...` (lines 52-93) | ❌ W0 — extend `QuestServiceTests.cs` | ⬜ pending |
| 36-0x-xx | TBD | 1 | CQUEST-04 | T-36-xx (same access-control pattern as Close) | Reopen restores quest to active board immediately; same ownership check as Close | unit | Same file, new `ReopenQuestAsync_...` tests | ❌ W0 | ⬜ pending |
| 36-0x-xx | TBD | 1 | CQUEST-05 | — | Closed campaign quest appears in Quest Log immediately (no next-day wait); one-shot `FinalizedDate` next-day filter is unchanged | unit | `QuestServiceTests.cs` — new test on `GetCompletedQuestsAsync` covering the new `IsClosed` OR-branch | ❌ W0 | ⬜ pending |
| 36-0x-xx | TBD | 1 | CQUEST-06 | T-36-xx (CSRF on new Close/Reopen POST endpoints) | No email sent for post/close/reopen in a campaign group — `IQuestEmailDispatcher` mock receives zero calls after `CloseQuestAsync`/`ReopenQuestAsync`/`AddAsync` for a campaign quest; `[ValidateAntiForgeryToken]` present on both new POST actions | unit/integration | Mirrors `FinalizeQuestAsync_WhenQuestReFetchReturnsNull_SendsNoEmails` (line 56); new CSRF-token assertion on `Close`/`Reopen` integration tests | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

Task IDs are `TBD` — plans do not exist yet for this phase. The planner must fill in real Task IDs/Plan numbers and assign real `T-36-xx` threat IDs from its `<threat_model>` block when it authors PLAN.md.

---

## Wave 0 Requirements

- [ ] `QuestBoard.UnitTests/Services/QuestServiceTests.cs` — extend with `CloseQuestAsync`/`ReopenQuestAsync`/updated `GetCompletedQuestsAsync` test cases (file exists, needs new `[Fact]`/`[Theory]` methods)
- [ ] `QuestBoard.IntegrationTests/Controllers/` — new or extended test file for `Close`/`Reopen` action authorization + happy-path (mirror `QuestFinalizeTests.cs` structure)
- [ ] `QuestBoard.IntegrationTests/Helpers/TestDataHelper.cs` — `CreateTestQuestAsync` needs new optional parameters (`isClosed`, `closedDate`, `boardType`/`groupId` pointing at a Campaign-type test group) to support seeding campaign-quest test fixtures
- [ ] A test `GroupEntity` seed with `BoardType = Campaign` needs to exist in the integration test fixture setup (check `GroupManagementIntegrationTests.cs`/`TenantIsolationTests.cs` for existing group-seeding helpers to extend — none currently seed a Campaign-type group)
- No new framework install needed — xunit.v3/FluentAssertions/NSubstitute already present and used identically for this kind of service-method + controller-action testing throughout the codebase.

---

## Manual-Only Verifications

*All phase behaviors have automated verification.*

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 60s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
