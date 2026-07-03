---
phase: 35
slug: board-type-configuration
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-07-03
---

# Phase 35 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xunit.v3 (3.2.2) + FluentAssertions (8.10.0) + Microsoft.AspNetCore.Mvc.Testing (10.0.9) |
| **Config file** | `QuestBoard.IntegrationTests/QuestBoard.IntegrationTests.csproj` (also `QuestBoard.UnitTests/QuestBoard.UnitTests.csproj` for unit-level tests) |
| **Quick run command** | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~GroupManagementIntegrationTests"` |
| **Full suite command** | `dotnet test` |
| **Estimated runtime** | ~60 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~GroupManagementIntegrationTests"`
- **After every plan wave:** Run `dotnet test` (full suite)
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** 60 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 35-01-xx | 01 | 0 | BOARD-01 | — | N/A | unit/integration | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~GroupManagementIntegrationTests"` | ❌ W0 | ⬜ pending |
| 35-0x-xx | TBD | 1 | BOARD-01 | — | SuperAdmin can select Board Type on Create form; selected value persists to the group | integration | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~CreateGroup"` | ❌ W0 — needs new test asserting BoardType round-trips through Create POST | ⬜ pending |
| 35-0x-xx | TBD | 1 | BOARD-01 | — | Create form validation rejects submission with no Board Type selected (Pitfall 3 / D-03) | integration | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~CreateGroup_WithoutBoardType"` | ❌ W0 — new test | ⬜ pending |
| 35-0x-xx | TBD | 1 | BOARD-02 | T-35-01 | Edit form displays Board Type read-only; POSTing a changed value has no effect on the stored entity (mass-assignment/overposting mitigation) | integration | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~EditGroup_BoardTypeTamper"` | ❌ W0 — new test | ⬜ pending |
| 35-0x-xx | TBD | 1 | BOARD-02 | — | Existing groups (e.g., seeded `EuphoriaInn`) default to `OneShot` after migration | integration | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~GroupsIndex_ShouldShowSeededGroup"` | Existing test file present — needs a new assertion or new `[Fact]` for BoardType default | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `QuestBoard.IntegrationTests/Controllers/GroupManagementIntegrationTests.cs` — extend with new `[Fact]`s covering BOARD-01 (dropdown persists selection, validation rejects empty selection) and BOARD-02 (Edit POST tamper is a no-op, existing seeded group defaults to OneShot)
- [ ] No new test project or framework install needed — `WebApplicationFactoryBase`/`AuthenticationHelper`/`TestDataHelper` fixtures already exist and are directly reusable
- [ ] Consider one unit test in `QuestBoard.UnitTests` for `GroupService.AddAsync` only if BoardType-specific business logic is added there (none currently planned per CONTEXT.md — only `Name` validation exists in `GroupService.AddAsync` today)

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
