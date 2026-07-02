---
phase: 28
slug: tenant-isolation
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-06-30
---

# Phase 28 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xunit.v3 3.2.2 + Microsoft.AspNetCore.Mvc.Testing 10.0.9 |
| **Config file** | `QuestBoard.IntegrationTests/xunit.runner.json` |
| **Quick run command** | `dotnet test QuestBoard.IntegrationTests/ --no-build` |
| **Full suite command** | `dotnet test --no-build` |
| **Estimated runtime** | ~60 seconds (full suite) |

---

## Sampling Rate

- **After every task commit:** `dotnet build` (compile gate, ~10 seconds)
- **After every plan wave:** `dotnet test --no-build` (full suite, ~60 seconds)
- **Before `/gsd-verify-work`:** Full suite must be green
- **Max feedback latency:** 60 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 28-01-01 | 01 | 1 | TENANT-01 | — | IActiveGroupContext in Domain, no circular dep | compile gate | `dotnet build` | ❌ W0 | ⬜ pending |
| 28-01-02 | 01 | 1 | TENANT-02 | T-session | ActiveGroupContextService null-safe; no throw when HttpContext null | compile gate | `dotnet build` | ❌ W0 | ⬜ pending |
| 28-01-03 | 01 | 1 | TENANT-03 | T-direct-ref | HasQueryFilter on Quest/ShopItem; UserEntity unfiltered | integration | `dotnet test --no-build` | ❌ W0 | ⬜ pending |
| 28-01-04 | 01 | 1 | TENANT-05 | — | Stub IActiveGroupContext GroupId=1; all existing tests pass | integration | `dotnet test --no-build` | ❌ W0 | ⬜ pending |
| 28-02-01 | 02 | 2 | TENANT-04 | T-hangfire | Jobs use SetGroupId before repo call; cross-group sweep IgnoreQueryFilters | integration | `dotnet test --no-build` | ✅ existing | ⬜ pending |
| 28-02-02 | 02 | 2 | TENANT-03 | T-direct-ref | Cross-group isolation: quest in Group 2 not visible to Group 1 session | integration | `dotnet test --no-build` | ❌ W0 | ⬜ pending |
| 28-03-01 | 03 | 3 | TENANT-05 | — | All 201 existing tests pass | full suite | `dotnet test --no-build` | ✅ existing | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `QuestBoard.Domain/Interfaces/IActiveGroupContext.cs` — stub interface for TENANT-01 compile gate
- [ ] `QuestBoard.Service/Services/ActiveGroupContextService.cs` — stub service for TENANT-02 compile gate
- [ ] `QuestBoard.IntegrationTests/Helpers/MutableGroupContext.cs` — test stub for TENANT-05
- [ ] Fix `TestDatabase.CreateContext()` — update direct `new QuestBoardContext(options)` call to pass stub `IActiveGroupContext`
- [ ] New integration test method `QuestRepository_GroupFilter_ExcludesOtherGroup` — cross-group isolation test (TENANT-03)

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Null = see all pass-through when no session (pre-Phase 30) | TENANT-03 | No HTTP session in Phase 28 test scenarios that mimic "not yet on group picker" | Verify quests load on fresh login before Phase 30 lands |

---

## Threat Summary

| Pattern | STRIDE | Mitigation in Phase 28 |
|---------|--------|------------------------|
| Session fixation → wrong GroupId in session | Elevation of Privilege | Session cookies HttpOnly + IsEssential already set in Program.cs; no change needed |
| Direct object reference bypass — quest by ID from another group | Information Disclosure | HasQueryFilter returns null → controller returns 404 naturally |
| Hangfire job reads wrong group's quests | Information Disclosure | D-09: SetGroupId before any repo call; D-08: IgnoreQueryFilters only on cross-group method |
| Test stub Singleton leaks between parallel test runs | Test isolation | xunit.v3 IClassFixture — each test class gets own factory; Singleton safe within class |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 60s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
