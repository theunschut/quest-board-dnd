---
phase: 40
slug: platform-members-page-redesign
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-07-04
---

# Phase 40 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit v3 (3.2.2) + NSubstitute 5.3.0 (unit) / Microsoft.AspNetCore.Mvc.Testing 10.0.9 + EFCore.InMemory 10.0.9 (integration) |
| **Config file** | `QuestBoard.UnitTests/QuestBoard.UnitTests.csproj`, `QuestBoard.IntegrationTests/QuestBoard.IntegrationTests.csproj` |
| **Quick run command** | `dotnet test QuestBoard.UnitTests --filter FullyQualifiedName~UserServiceTests\|FullyQualifiedName~UserRepositoryTests` |
| **Full suite command** | `dotnet test` (from repo root — runs both `QuestBoard.UnitTests` and `QuestBoard.IntegrationTests`) |
| **Estimated runtime** | ~60 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test QuestBoard.UnitTests --filter FullyQualifiedName~UserServiceTests|FullyQualifiedName~UserRepositoryTests`
- **After every plan wave:** Run `dotnet test` (full unit + integration suite)
- **Before `/gsd-verify-work`:** Full suite must be green
- **Max feedback latency:** 60 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 40-01-01 | 01 | 0 | MEMBERS-02 | — | `GetAvailableUsers` returns only non-members, filtered case-insensitively by Name/Email | unit | `dotnet test QuestBoard.UnitTests --filter FullyQualifiedName~UserRepositoryTests` | ❌ W0 | ⬜ pending |
| 40-0X-0X | TBD | 1 | MEMBERS-01 | — | Members page renders two-column layout, members left / available users right | integration | `dotnet test --filter FullyQualifiedName~GroupManagementIntegrationTests` | ✅ file / ❌ new case W0 | ⬜ pending |
| 40-0X-0X | TBD | 1 | MEMBERS-02 | — | Members GET `?search=` renders filtered results and echoes term into search box | integration | `dotnet test --filter FullyQualifiedName~GroupManagementIntegrationTests` | ✅ file / ❌ new case W0 | ⬜ pending |
| 40-0X-0X | TBD | 1 | MEMBERS-01/D-04 | — | Add-to-group redirect preserves the active `search` query string | integration | `dotnet test --filter FullyQualifiedName~GroupManagementIntegrationTests` | ✅ file / ❌ new case W0 | ⬜ pending |
| 40-0X-0X | TBD | 1 | MEMBERS-03 | T-40-01 | `CreateMember` POST scopes to route `groupId`, never `IActiveGroupContext` | integration | `dotnet test --filter FullyQualifiedName~GroupManagementIntegrationTests` | ✅ file / ❌ new case W0 | ⬜ pending |
| 40-0X-0X | TBD | 1 | MEMBERS-03/D-07 | — | `CreateMember` fires identical flash messages/outcomes as `AdminController.CreateUser` | unit + integration | `dotnet test --filter FullyQualifiedName~GroupManagementIntegrationTests` | ❌ new cases W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*
*Exact Task IDs/Plan IDs finalized once the planner assigns them — this map records the required coverage, not final numbering.*

---

## Wave 0 Requirements

- [ ] `QuestBoard.UnitTests/Repository/UserRepositoryTests.cs` (or extend `UserServiceTests.cs`) — stubs/cases for `GetAvailableUsers` search + not-in-group filtering (MEMBERS-02)
- [ ] New test cases inside existing `QuestBoard.IntegrationTests/Controllers/GroupManagementIntegrationTests.cs` — two-column Members render, search round-trip, per-row Add with search preserved, and `CreateMember`'s four outcomes (MEMBERS-01, MEMBERS-02, MEMBERS-03, D-04)

*No new test framework or fixture install needed — both test projects and their frameworks already exist and are wired into CI.*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Two-column responsive layout renders correctly and mobile stacking (D-08) looks right | MEMBERS-01 | Visual layout/CSS correctness is not meaningfully assertable via integration test | Load `/Platform/Group/Members/{id}` at desktop and mobile viewport widths; confirm two-column vs. stacked layout matches CONTEXT.md D-08 |
| Create New User modal opens/closes correctly and mirrors ShopManagement modal UX (D-05) | MEMBERS-03 | Modal open/close/animation behavior is a browser-rendered interaction, not asserted by MVC integration tests | Click "Create New User" button, confirm Bootstrap modal opens with Email/Name/GroupRole fields, submits, and closes on success |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 60s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
