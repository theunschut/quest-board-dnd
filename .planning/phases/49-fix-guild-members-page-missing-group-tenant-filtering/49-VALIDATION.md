---
phase: 49
slug: fix-guild-members-page-missing-group-tenant-filtering
status: final
nyquist_compliant: true
wave_0_complete: true
created: 2026-07-05
---

# Phase 49 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xunit.v3 3.2.2 + FluentAssertions 8.10.0 + NSubstitute 5.3.0 [VERIFIED: QuestBoard.UnitTests.csproj] |
| **Config file** | `QuestBoard.UnitTests/QuestBoard.UnitTests.csproj` |
| **Quick run command** | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~PlayerSignupRepositoryTests|FullyQualifiedName~CharacterRepositoryTests|FullyQualifiedName~UserTransactionRepositoryTests" && dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~DungeonMasterControllerIntegrationTests"` |
| **Full suite command** | `dotnet test` |
| **Estimated runtime** | ~30-60 seconds (quick filter) / full suite varies |

---

## Sampling Rate

- **After every task commit:** Run the relevant filtered command from the Per-Task Verification Map below
- **After every plan wave:** Run `dotnet test` (full suite — this project has `QuestBoard.UnitTests` and `QuestBoard.IntegrationTests`; confirm both run clean)
- **Before `/gsd-verify-work`:** Full suite must be green
- **Max feedback latency:** 60 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 49-01 Task 1 | 01 | 1 | D-02/D-03 | IDOR (Character list/detail) | `CharacterEntity` gets `GroupId` column + migration + no-null-escape-hatch `HasQueryFilter`; stale transitive-filter comments corrected for Character/UserTransaction/PlayerSignup | build | `cd QuestBoard.Service && dotnet build ../QuestBoard.Repository` | ✅ modifies existing files | ⬜ pending |
| 49-01 Task 2 | 01 | 1 | D-01/D-04 | IDOR (Character picture) | `GetCharacterProfilePictureAsync` rewritten to root through `DbContext.Characters`; `Create` POST stamps `GroupId`; `TestDataHelper` updated | build | `cd QuestBoard.Service && dotnet build` | ✅ modifies existing files | ⬜ pending |
| 49-01 Task 3 | 01 | 1 | D-01/D-02/D-03/D-04 | IDOR (Character list/detail/picture) | List/detail scoped to active group; SuperAdmin-with-no-group sees empty list; profile-picture 404 for cross-group ID | unit (repository) | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~CharacterRepositoryTests"` | ✅ new file created by this task | ⬜ pending |
| 49-02 Task 1 | 02 | 2 | D-06/D-07/D-08/D-09 | Confused deputy (DM profile view/edit/picture) | `Profile`/`EditProfile`(GET+POST)/`GetDMProfilePicture` all 404 for cross-group target; SuperAdmin-no-group also 404s | build | `cd QuestBoard.Service && dotnet build` | ✅ modifies existing file | ⬜ pending |
| 49-02 Task 2 | 02 | 2 | D-06/D-07/D-08/D-09/D-09a | Confused deputy (DM profile view/edit/picture) | Integration tests prove cross-group 404 and SuperAdmin-no-group 404 for all four actions | integration (controller) | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~DungeonMasterControllerIntegrationTests"` | ✅ existing file — extended | ⬜ pending |
| 49-03 Task 1 | 03 | 2 | D-11 | Fragile transitive filter (UserTransaction) | `ReturnOrSellItemAsync` uses `GetTransactionWithDetailsAsync` (Include-protected), not the unguarded base `GetByIdAsync` | build | `cd QuestBoard.Service && dotnet build` | ✅ modifies existing file | ⬜ pending |
| 49-03 Task 2 | 03 | 2 | D-10/D-11 | Fragile transitive filter (UserTransaction) | Cross-group `UserTransaction` excluded from `GetTransactionsByUserAsync`-style queries (Include-driven inner join regression test) | unit (repository) | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~UserTransactionRepositoryTests"` | ✅ new file created by this task | ⬜ pending |
| 49-04 Task 1 | 04 | 2 | D-12 | Fragile transitive filter (PlayerSignup) | New Quest-including PlayerSignup lookup added (repository + service) so callers can validate the parent Quest's group | build | `cd QuestBoard.Service && dotnet build` | ✅ modifies existing files | ⬜ pending |
| 49-04 Task 2 | 04 | 2 | D-12/D-13 | Confused deputy (RemovePlayerSignup) | `RemovePlayerSignup` checks target signup's parent Quest group membership; 404 (not 403) for cross-group | build | `cd QuestBoard.Service && dotnet build` | ✅ modifies existing file | ⬜ pending |
| 49-04 Task 3 | 04 | 2 | D-12/D-13 | Confused deputy + fragile transitive filter (PlayerSignup) | Cross-group `RemovePlayerSignup` blocked (404); other 3 unfiltered repository methods' current pre-validation-dependent safety regression-tested | unit (repository) | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~PlayerSignupRepositoryTests"` | ✅ existing file — extended | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky — statuses flip to ✅ as each task's commit lands during `/gsd-execute-phase 49`.*

*D-05 and D-09a are investigated-and-ruled-out decisions with no code change (confirmed in 49-CONTEXT.md and 49-RESEARCH.md) — they have no row here by design.*

---

## Wave 0 Requirements

- [x] `QuestBoard.UnitTests/Repository/CharacterRepositoryTests.cs` — new file, assigned to 49-01 Task 3; covers D-01/D-02/D-03/D-04 (list scoping, SuperAdmin-empty behavior, profile-picture cross-group 404)
- [x] `QuestBoard.UnitTests/Repository/UserTransactionRepositoryTests.cs` — new file, assigned to 49-03 Task 2; covers D-11's regression test (cross-group transaction excluded)
- [x] `DungeonMasterControllerIntegrationTests.cs` — already exists (contrary to this file's original research-time note); extended by 49-02 Task 2 to cover D-06 through D-09's four hardened actions
- [x] `QuestBoard.UnitTests/Repository/PlayerSignupRepositoryTests.cs` — existing file, extended by 49-04 Task 3 with cross-group regression coverage for D-12 (RemovePlayerSignup fix + the other 3 methods' documented safety) and D-13 (404 response)

All Wave 0 gaps are assigned to a specific plan task in the finalized PLAN.md files — no outstanding scaffolding work remains unassigned.

---

## Manual-Only Verifications

*None — all phase behaviors have automated verification via the unit/integration test map above.*

---

## Validation Sign-Off

- [x] All tasks have `<automated>` verify or Wave 0 dependencies
- [x] Sampling continuity: no 3 consecutive tasks without automated verify
- [x] Wave 0 covers all MISSING references
- [x] No watch-mode flags
- [x] Feedback latency < 60s
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** approved 2026-07-05 (via gsd-plan-checker VERIFICATION PASSED)
