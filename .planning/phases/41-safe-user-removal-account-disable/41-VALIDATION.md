---
phase: 41
slug: safe-user-removal-account-disable
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-07-04
---

# Phase 41 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit v3 + `Microsoft.AspNetCore.Mvc.Testing` `WebApplicationFactory` (integration) |
| **Config file** | `QuestBoard.IntegrationTests/QuestBoard.IntegrationTests.csproj` (existing, no changes needed) |
| **Quick run command** | `dotnet test --filter FullyQualifiedName~AdminControllerIntegrationTests\|FullyQualifiedName~UsersControllerIntegrationTests\|FullyQualifiedName~AccountControllerIntegrationTests` |
| **Full suite command** | `dotnet test` |
| **Estimated runtime** | ~60 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test --filter FullyQualifiedName~{TestClass}` (targeted to the class the task touched)
- **After every plan wave:** Run `dotnet test` (full suite)
- **Before `/gsd-verify-work`:** Full suite must be green
- **Max feedback latency:** 60 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 41-0X-0X | TBD | 0 | SAFE-01 | — | New `AdminControllerIntegrationTests.DeleteUser_Post_*` seeds a user with quest/shop/transaction/reminder history, asserts 200 OK + group membership removed + account/characters/other-group-memberships intact | integration | `dotnet test --filter FullyQualifiedName~AdminControllerIntegrationTests` | ❌ W0 | ⬜ pending |
| 41-0X-0X | TBD | 0 | SAFE-02/D-07/D-08 | T-41-01 | New `UsersControllerIntegrationTests` covers Disable action: sets `LockoutEnd`, no data deleted, self-disable blocked (D-07), peer-SuperAdmin disable allowed (D-08) | integration | `dotnet test --filter FullyQualifiedName~UsersControllerIntegrationTests` | ❌ W0 | ⬜ pending |
| 41-0X-0X | TBD | 0 | SAFE-03/D-12 | — | Same test file covers Enable action: `LockoutEnd` cleared, disabled user can sign in again | integration | `dotnet test --filter FullyQualifiedName~UsersControllerIntegrationTests` | ❌ W0 | ⬜ pending |
| 41-0X-0X | TBD | 0 | SAFE-04/D-13 | — | New `AccountControllerIntegrationTests` cases: `LockoutEnd == MaxValue` shows disabled copy; ordinary 5-failed-attempt lockout still shows 15-minute copy | integration | `dotnet test --filter FullyQualifiedName~AccountControllerIntegrationTests` | ❌ W0 | ⬜ pending |
| 41-0X-0X | TBD | 1 | SAFE-02 | T-41-02 | `Disable`/`Enable` POST actions carry `[ValidateAntiForgeryToken]` (CSRF hardening, matches every other mutating action in this codebase) | integration | `dotnet test --filter FullyQualifiedName~UsersControllerIntegrationTests` | ❌ new case W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*
*Exact Task IDs/Plan IDs finalized once the planner assigns them — this map records the required coverage, not final numbering.*

---

## Wave 0 Requirements

- [ ] `QuestBoard.IntegrationTests/Controllers/AdminControllerIntegrationTests.cs` — add `DeleteUser_Post_*` tests covering SAFE-01 (group-only removal + no-throw with quest/shop/transaction/reminder history)
- [ ] `QuestBoard.IntegrationTests/Controllers/UsersControllerIntegrationTests.cs` (new file, Platform area) — covers SAFE-02/SAFE-03 (Disable/Enable actions, self-disable guard D-07, peer-SuperAdmin-allowed D-08, CSRF token presence)
- [ ] `QuestBoard.IntegrationTests/Controllers/AccountControllerIntegrationTests.cs` — add `Login_Post_DisabledAccount_ShowsDisabledMessage` and confirm/add a test for the ordinary 15-minute-lockout message wording (SAFE-04)
- [ ] Reuse existing `AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync` (`QuestBoard.IntegrationTests/Helpers/AuthenticationHelper.cs:164-171`) for all new SuperAdmin-authenticated test clients — no new test helper needed
- [ ] Reuse existing `factory.Services.CreateScope()` + `UserManager<UserEntity>` resolution pattern (`AccountControllerIntegrationTests.cs:508-513`) for asserting `LockoutEnd`/`SecurityStamp` state directly against the DB in new tests

*No new test framework or fixture install needed — existing integration test project and helpers already cover this phase's needs.*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| New Platform Users page (`Index.cshtml` + `Index.Mobile.cshtml`) renders modern-card pattern correctly at desktop and mobile widths | SAFE-02 | Visual layout/CSS correctness is not meaningfully assertable via integration test | Load `/Platform/Users` at desktop and mobile viewport widths; confirm modern-card pattern, Disable/Enable button styling per CLAUDE.md UI/UX guidelines |
| Self-disable guard is visibly hidden/disabled in the UI for the current SuperAdmin's own row | SAFE-02/D-07 | Confirming the control is absent/disabled in rendered HTML is covered by integration test, but the *visual* affordance (e.g. greyed-out button, tooltip) benefits from a manual look | Sign in as SuperAdmin, open `/Platform/Users`, confirm own row shows no active Disable control |
| "Remove from Group" confirm-dialog copy reads correctly and matches D-02's intent | SAFE-01/D-02 | JS `confirm()` dialog text is not asserted by MVC integration tests | Click "Remove from Group" on `/Admin/Users`, confirm dialog text matches CONTEXT.md D-02 copy |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 60s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
