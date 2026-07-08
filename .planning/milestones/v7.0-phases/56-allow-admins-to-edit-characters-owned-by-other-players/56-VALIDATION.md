---
phase: 56
slug: allow-admins-to-edit-characters-owned-by-other-players
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-07-06
---

# Phase 56 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit v3 (3.2.2), `Microsoft.AspNetCore.Mvc.Testing` 10.0.9 |
| **Config file** | `QuestBoard.IntegrationTests/xunit.runner.json` |
| **Quick run command** | `dotnet test QuestBoard.IntegrationTests --filter FullyQualifiedName~GuildMembersControllerIntegrationTests` |
| **Full suite command** | `dotnet test` |
| **Estimated runtime** | ~60 seconds (quick), full suite varies |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test QuestBoard.IntegrationTests --filter FullyQualifiedName~GuildMembersControllerIntegrationTests`
- **After every plan wave:** Run `dotnet test`
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** 120 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 56-01-01 | 01 | 1 | D-02 | V4 Access Control | Admin can Edit another player's character in same group | integration | `dotnet test --filter Edit_AdminEditingAnotherPlayersCharacter_ShouldSucceed` | ❌ W0 | ⬜ pending |
| 56-01-02 | 01 | 1 | D-02 | V4 Access Control | SuperAdmin can Edit another player's character (active group selected) | integration | `dotnet test --filter Edit_SuperAdminEditingAnotherPlayersCharacter_ShouldSucceed` | ❌ W0 | ⬜ pending |
| 56-01-03 | 01 | 1 | D-02 | V4 Access Control | Player still cannot Edit another player's character (regression guard) | integration | `dotnet test --filter Edit_PlayerEditingAnotherPlayersCharacter_ShouldBeForbidden` | ❌ W0 | ⬜ pending |
| 56-01-04 | 01 | 1 | D-03 | V4 Access Control (IDOR) | Admin in Group A cannot reach a Group B character's Edit page — 404 not 403 | integration | `dotnet test --filter Edit_AdminEditingCharacterInDifferentGroup_ShouldReturnNotFound` | ❌ W0 | ⬜ pending |
| 56-01-05 | 01 | 1 | — | — | Owner can still Edit their own character (regression guard) | integration | `dotnet test --filter Edit_OwnerEditingOwnCharacter_ShouldSucceed` | ❌ W0 | ⬜ pending |
| 56-01-06 | 01 | 1 | D-04 | — | Details page shows Edit button (CanEdit=true) to Admin viewing another's character | integration | `dotnet test --filter Details_AdminViewingAnotherPlayersCharacter_ShowsEditButton` | ❌ W0 | ⬜ pending |
| 56-02-01 | 02 | 1 | D-01 | V4 Access Control | Admin can Delete another player's character (same group) | integration | `dotnet test --filter Delete_AdminDeletingAnotherPlayersCharacter_ShouldSucceed` | ❌ W0 | ⬜ pending |
| 56-02-02 | 02 | 1 | D-01 | V4 Access Control | Player still cannot Delete another player's character (regression guard) | integration | `dotnet test --filter Delete_PlayerDeletingAnotherPlayersCharacter_ShouldBeForbidden` | ❌ W0 | ⬜ pending |
| 56-02-03 | 02 | 1 | D-01 | V4 Access Control | Admin can ToggleRetirement on another player's character (same group) | integration | `dotnet test --filter ToggleRetirement_AdminTogglingAnotherPlayersCharacter_ShouldSucceed` | ❌ W0 | ⬜ pending |
| 56-02-04 | 02 | 1 | D-01 | V4 Access Control | Player still cannot ToggleRetirement another player's character (regression guard) | integration | `dotnet test --filter ToggleRetirement_PlayerTogglingAnotherPlayersCharacter_ShouldBeForbidden` | ❌ W0 | ⬜ pending |
| 56-02-05 | 02 | 1 | D-03 | V4 Access Control (IDOR) | Admin in Group A cannot Delete/ToggleRetirement a Group B character — 404 | integration | `dotnet test --filter Delete_AdminDeletingCharacterInDifferentGroup_ShouldReturnNotFound` | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `QuestBoard.IntegrationTests/Controllers/GuildMembersControllerIntegrationTests.cs` — 11 new test methods (zero authorization tests exist today for Edit/Delete/ToggleRetirement — Pitfall 4 in RESEARCH.md)
- [ ] No new fixtures or framework install needed — `AuthenticationHelper`, `TestDataHelper`, `WebApplicationFactoryBase` already support every scenario (confirmed in RESEARCH.md Code Examples)

---

## Manual-Only Verifications

*None — all phase behaviors have automated (integration test) verification per RESEARCH.md's Phase Requirements → Test Map.*

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 120s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
