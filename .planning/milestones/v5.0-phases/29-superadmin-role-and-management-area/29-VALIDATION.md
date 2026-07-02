---
phase: 29
slug: superadmin-role-and-management-area
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-06-30
---

# Phase 29 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit (QuestBoard.IntegrationTests) |
| **Config file** | `xunit.runner.json` |
| **Quick run command** | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~AdminHandler OR FullyQualifiedName~DungeonMasterHandler"` |
| **Full suite command** | `dotnet test` |
| **Estimated runtime** | ~30 seconds (full suite) |

---

## Sampling Rate

- **After every task commit:** Run auth handler tests (quick run command above)
- **After every plan wave:** Run `dotnet test` (full 197-test suite)
- **Before `/gsd-verify-work`:** Full suite must be green
- **Max feedback latency:** ~30 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 29-??-01 | auth | 1 | AUTH-01 | — | SuperAdmin role seeded in AspNetRoles | migration smoke | `dotnet test --filter "FullyQualifiedName~TestDataHelper"` | ❌ W0 | ⬜ pending |
| 29-??-02 | auth | 1 | AUTH-02 | T-29-01 | AdminHandler approves Admin GroupRole; denies others | integration | `dotnet test --filter "FullyQualifiedName~AdminHandlerTests"` | ❌ W0 | ⬜ pending |
| 29-??-03 | auth | 1 | AUTH-03 | T-29-01 | DungeonMasterHandler approves DM and Admin; denies Player | integration | `dotnet test --filter "FullyQualifiedName~DungeonMasterHandlerTests"` | ❌ W0 | ⬜ pending |
| 29-??-04 | auth | 1 | AUTH-04 | T-29-02 | SuperAdmin bypasses AdminHandler and DungeonMasterHandler | integration | `dotnet test --filter "FullyQualifiedName~SuperAdminAuthTests"` | ❌ W0 | ⬜ pending |
| 29-??-05 | area | 2 | AUTH-05 | T-29-03 | Non-SuperAdmin receives 403 on /platform/* | integration | `dotnet test --filter "FullyQualifiedName~PlatformAreaAuthTests"` | ❌ W0 | ⬜ pending |
| 29-??-06 | area | 2 | MGMT-01 | T-29-03 | GET /platform/Group/Index returns 200 for SuperAdmin | integration | `dotnet test --filter "FullyQualifiedName~PlatformAreaAuthTests"` | ❌ W0 | ⬜ pending |
| 29-??-07 | area | 2 | MGMT-02 | — | Groups index lists all groups with member counts | integration | `dotnet test --filter "FullyQualifiedName~GroupManagementTests"` | ❌ W0 | ⬜ pending |
| 29-??-08 | area | 2 | MGMT-03 | T-29-04 | Create group: valid name succeeds; duplicate name → validation error | integration | `dotnet test --filter "FullyQualifiedName~GroupManagementTests"` | ❌ W0 | ⬜ pending |
| 29-??-09 | area | 2 | MGMT-04 | — | Delete group: empty succeeds; non-empty → error | integration | `dotnet test --filter "FullyQualifiedName~GroupManagementTests"` | ❌ W0 | ⬜ pending |
| 29-??-10 | area | 2 | MGMT-05 | T-29-05 | Add member: valid user+group+role adds UserGroups row | integration | `dotnet test --filter "FullyQualifiedName~GroupManagementTests"` | ❌ W0 | ⬜ pending |
| 29-??-11 | area | 2 | MGMT-06 | — | Remove member: UserGroups row deleted | integration | `dotnet test --filter "FullyQualifiedName~GroupManagementTests"` | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

All test files are new — none exist yet. Wave 0 must create:

- [ ] `QuestBoard.IntegrationTests/Controllers/AdminHandlerIntegrationTests.cs` — covers AUTH-02, AUTH-03, AUTH-04
- [ ] `QuestBoard.IntegrationTests/Controllers/PlatformAreaIntegrationTests.cs` — covers AUTH-05, MGMT-01
- [ ] `QuestBoard.IntegrationTests/Controllers/GroupManagementIntegrationTests.cs` — covers MGMT-02 through MGMT-06
- [ ] Update `TestDataHelper.SeedRolesAsync` to include "SuperAdmin" role seeding
- [ ] Update `AuthenticationHelper` to add `CreateAuthenticatedSuperAdminClientAsync` helper

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| SuperAdmin role exists in AspNetRoles after migration apply | AUTH-01 | Migration smoke; no automated post-migration DB query in test suite | Apply migration on dev SQL Server; query `SELECT * FROM AspNetRoles WHERE Name = 'SuperAdmin'` |
| /platform pages render correctly with modern-card styling | MGMT-01–06 | CSS/layout correctness is visual | Log in as SuperAdmin; navigate to /platform; verify modern-card pattern, table layouts, forms |

---

## Threat Map

| Threat ID | Pattern | ASVS | Mitigation |
|-----------|---------|------|-----------|
| T-29-01 | IDOR: non-SuperAdmin accessing AdminOnly/DMOnly pages | V4 | `IsInRole("SuperAdmin")` short-circuit + GroupRole check in handlers |
| T-29-02 | Privilege escalation via handler bypass | V4 | Null group guard + explicit GroupRole check before succeed |
| T-29-03 | IDOR: non-SuperAdmin accessing /platform/* | V4 | `[Authorize(Policy="SuperAdminOnly")]` on area controller class |
| T-29-04 | Mass assignment / duplicate group name | V5 | ViewModel binding; DB unique index; catch DbUpdateException |
| T-29-05 | CSRF on group/member mutations | V4 | `[ValidateAntiForgeryToken]` on all POST actions |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 30s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
