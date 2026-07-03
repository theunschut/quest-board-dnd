---
phase: 37
slug: navigation-access-control
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-07-03
---

# Phase 37 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 3.2.2 + Microsoft.AspNetCore.Mvc.Testing 10.0.9 (WebApplicationFactory) |
| **Config file** | `QuestBoard.IntegrationTests/QuestBoard.IntegrationTests.csproj`; no separate test-runner config file |
| **Quick run command** | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~AdminControllerIntegrationTests|FullyQualifiedName~LayoutNavigationTests"` |
| **Full suite command** | `dotnet test` |
| **Estimated runtime** | ~60 seconds (quick), ~5 minutes (full) |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~AdminControllerIntegrationTests|FullyQualifiedName~LayoutNavigationTests"`
- **After every plan wave:** Run `dotnet test`
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** 60 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 37-01-01 | 01 | 0 | Test infra | — | `MutableGroupContext.BoardType` settable property added | unit | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~MutableGroupContext"` | ❌ W0 | ⬜ pending |
| 37-01-02 | 01 | 1 | NAV-01 | — | Calendar hidden in Campaign group nav (desktop + mobile) | integration | `dotnet test --filter "FullyQualifiedName~LayoutNavigationTests"` | ❌ W0 | ⬜ pending |
| 37-01-03 | 01 | 1 | NAV-02 | — | Shop hidden in Campaign group nav | integration | `dotnet test --filter "FullyQualifiedName~LayoutNavigationTests"` | ❌ W0 | ⬜ pending |
| 37-01-04 | 01 | 1 | NAV-03 | — | Guild Members remains visible regardless of board type (regression) | integration | `dotnet test --filter "FullyQualifiedName~LayoutNavigationTests"` | ❌ W0 | ⬜ pending |
| 37-01-05 | 01 | 1 | NAV-04 | — | "Manage Shop" hidden in Campaign group nav | integration | `dotnet test --filter "FullyQualifiedName~LayoutNavigationTests"` | ❌ W0 | ⬜ pending |
| 37-01-06 | 01 | 1 | NAV-05 | — | "Edit My Profile" hidden in Campaign group nav | integration | `dotnet test --filter "FullyQualifiedName~LayoutNavigationTests"` | ❌ W0 | ⬜ pending |
| 37-01-07 | 01 | 1 | NAV-06 | — | "Players" hidden in Campaign group nav | integration | `dotnet test --filter "FullyQualifiedName~LayoutNavigationTests"` | ❌ W0 | ⬜ pending |
| 37-01-08 | 01 | 1 | D-04 | — | Calendar hidden for anonymous visitors (both layouts) | integration | `dotnet test --filter "FullyQualifiedName~LayoutNavigationTests"` | ❌ W0 | ⬜ pending |
| 37-02-01 | 02 | 1 | ACCESS-01 | T-37-01 | Email Stats nav link hidden for Admin (non-SuperAdmin) | integration | `dotnet test --filter "FullyQualifiedName~LayoutNavigationTests"` | ❌ W0 | ⬜ pending |
| 37-02-02 | 02 | 1 | ACCESS-01 | T-37-01 | Direct `/Admin/EmailStats` GET rejected for Admin, allowed for SuperAdmin | integration | `dotnet test --filter "FullyQualifiedName~AdminControllerIntegrationTests.EmailStats"` | ✅ Partial (extend) | ⬜ pending |
| 37-02-03 | 02 | 1 | ACCESS-01 | T-37-02 | AccessDenied action returns 200, generalized copy, no crash (anonymous or authenticated) | integration | `dotnet test --filter "FullyQualifiedName~AccountControllerIntegrationTests"` | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `QuestBoard.IntegrationTests/Controllers/LayoutNavigationTests.cs` (or similar) — covers NAV-01..06, D-04, ACCESS-01's nav-link visibility. Must exercise both desktop and mobile user agents (reuse `MobileViewsTests.GetWithUserAgentAsync` helper) and both board types.
- [ ] `MutableGroupContext.BoardType` settable property — prerequisite test infrastructure change before any nav test can vary board type.
- [ ] `AdminControllerIntegrationTests.EmailStats_WhenAdminNotSuperAdmin_ShouldBeRejected` — closes the one gap in existing EmailStats coverage (current test only covers Player role, not Admin-vs-SuperAdmin).
- [ ] AccessDenied action test coverage — new/reused action, zero existing coverage.

*Framework install: none — xUnit/Mvc.Testing already fully configured.*

---

## Manual-Only Verifications

*None — all phase behaviors have automated verification via integration tests.*

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 60s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
