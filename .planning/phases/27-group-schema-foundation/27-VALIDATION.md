---
phase: 27
slug: group-schema-foundation
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-06-29
---

# Phase 27 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit (QuestBoard.IntegrationTests) |
| **Config file** | `QuestBoard.IntegrationTests/QuestBoard.IntegrationTests.csproj` |
| **Quick run command** | `dotnet test QuestBoard.IntegrationTests` |
| **Full suite command** | `dotnet build && dotnet test` |
| **Estimated runtime** | ~60 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test QuestBoard.IntegrationTests`
- **After every plan wave:** Run `dotnet build && dotnet test`
- **Before `/gsd-verify-work`:** Full suite must be green
- **Max feedback latency:** 60 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 27-01-01 | 01 | 1 | GROUP-01 | — | N/A | build | `dotnet build` | ✅ | ⬜ pending |
| 27-01-02 | 01 | 1 | GROUP-02 | — | N/A | build | `dotnet build` | ✅ | ⬜ pending |
| 27-01-03 | 01 | 1 | GROUP-03 | — | N/A | build | `dotnet build` | ✅ | ⬜ pending |
| 27-02-01 | 02 | 2 | GROUP-04 | — | N/A | integration | `dotnet test QuestBoard.IntegrationTests` | ✅ | ⬜ pending |
| 27-02-02 | 02 | 2 | GROUP-05 | — | N/A | integration | `dotnet test QuestBoard.IntegrationTests` | ✅ | ⬜ pending |
| 27-02-03 | 02 | 2 | GROUP-06 | — | N/A | integration | `dotnet test QuestBoard.IntegrationTests` | ✅ | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

Existing infrastructure covers all phase requirements (xUnit + integration test project already set up).

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Migration applies cleanly on production schema | GROUP-06 | Requires live SQL Server; integration tests use InMemory | Run `dotnet run` and verify startup migration completes without error |
| AspNetUserRoles contains no Player/DM/Admin entries after migration | GROUP-05 | Requires running migration against real DB | Check `AspNetUserRoles` table after migration via SQL query |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 60s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
