---
phase: 49
slug: fix-guild-members-page-missing-group-tenant-filtering
status: draft
nyquist_compliant: false
wave_0_complete: false
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
| **Quick run command** | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~PlayerSignupRepositoryTests|FullyQualifiedName~CharacterRepositoryTests|FullyQualifiedName~UserTransactionRepositoryTests|FullyQualifiedName~DungeonMasterControllerTests"` |
| **Full suite command** | `dotnet test` |
| **Estimated runtime** | ~30-60 seconds (quick filter) / full suite varies |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~Character|FullyQualifiedName~DungeonMaster|FullyQualifiedName~UserTransaction|FullyQualifiedName~PlayerSignup"`
- **After every plan wave:** Run `dotnet test` (full suite — this project has `QuestBoard.UnitTests` and `QuestBoard.IntegrationTests`; confirm both run clean)
- **Before `/gsd-verify-work`:** Full suite must be green
- **Max feedback latency:** 60 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 49-xx-xx | TBD | TBD | D-01/D-02/D-03 | IDOR (Character list/detail) | `GuildMembersController.Index`/`Details` scoped to active group; SuperAdmin-with-no-group sees empty list, not cross-group superview | unit (repository) + integration (controller) | `dotnet test --filter FullyQualifiedName~CharacterRepositoryTests` | ❌ W0 | ⬜ pending |
| 49-xx-xx | TBD | TBD | D-04 | IDOR (Character picture) | `GetProfilePicture` returns 404 for a cross-group character ID | unit (repository) | `dotnet test --filter FullyQualifiedName~CharacterRepositoryTests` | ❌ W0 (same file as above) | ⬜ pending |
| 49-xx-xx | TBD | TBD | D-06/D-07/D-08/D-09 | Confused deputy (DM profile view/edit/picture) | `DungeonMasterController.Profile`/`EditProfile`(GET+POST)/`GetDMProfilePicture` all 404 for a cross-group target user; SuperAdmin-no-group also 404s | integration (controller) | `dotnet test --filter FullyQualifiedName~DungeonMasterControllerTests` | ❌ W0 — no existing controller-level test file for this controller | ⬜ pending |
| 49-xx-xx | TBD | TBD | D-10/D-11 | Fragile transitive filter (UserTransaction) | Cross-group `UserTransaction` excluded from `GetTransactionsByUserAsync`; `ReturnOrSellItemAsync` uses `GetTransactionWithDetailsAsync`, not the unguarded base `GetByIdAsync` | unit (repository) | `dotnet test --filter FullyQualifiedName~UserTransactionRepositoryTests` | ❌ W0 — no `UserTransactionRepositoryTests.cs` exists yet | ⬜ pending |
| 49-xx-xx | TBD | TBD | D-12 | Confused deputy (RemovePlayerSignup) + fragile transitive filter (PlayerSignup repo methods) | Cross-group `PlayerSignupEntity` removal via `RemovePlayerSignup` is blocked; other 3 unfiltered repository methods documented + regression-tested for their current pre-validation-dependent safety | unit (repository) + integration (controller, `AdminOnly` policy path) | `dotnet test --filter FullyQualifiedName~PlayerSignupRepositoryTests` | ✅ existing file — extend | ⬜ pending |
| 49-xx-xx | TBD | TBD | D-13 | Existence oracle | `RemovePlayerSignup` returns 404 (not 403) for a cross-group target signup | integration (controller) | `dotnet test --filter FullyQualifiedName~PlayerSignupRepositoryTests` (or Quest controller integration test) | ❌ W0 — new test case | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

*Task IDs are TBD until the planner assigns plan/wave numbers — the planner must fill these in per the actual PLAN.md task breakdown.*

---

## Wave 0 Requirements

- [ ] `QuestBoard.UnitTests/Repository/CharacterRepositoryTests.cs` — new file; covers D-01/D-02/D-03/D-04 (list scoping, SuperAdmin-empty behavior, profile-picture cross-group 404 via the rewritten query)
- [ ] `QuestBoard.UnitTests/Repository/UserTransactionRepositoryTests.cs` — new file; covers D-11.2's regression test (cross-group transaction excluded from `GetTransactionsByUserAsync`)
- [ ] A `DungeonMasterController` integration/unit test file — none currently exists for this controller; covers D-06 through D-09's four hardened actions. Check whether `QuestBoard.IntegrationTests/Controllers/` is the right home (that project already hosts `AdminControllerIntegrationTests.cs`/`AdminHandlerIntegrationTests.cs`, suggesting controller-level auth tests live there)
- [ ] Extend existing `QuestBoard.UnitTests/Repository/PlayerSignupRepositoryTests.cs` with cross-group regression coverage for D-12 (RemovePlayerSignup fix + the other 3 methods' documented safety) and D-13 (404 response)

---

## Manual-Only Verifications

*None — all phase behaviors have automated verification via the unit/integration test map above.*

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 60s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
