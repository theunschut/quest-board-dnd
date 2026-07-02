---
phase: 26
slug: namespace-rename
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-06-29
---

# Phase 26 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit (dotnet test) |
| **Config file** | `EuphoriaInn.Tests/EuphoriaInn.Tests.csproj` (renamed to QuestBoard.Tests) |
| **Quick run command** | `dotnet build` |
| **Full suite command** | `dotnet test` |
| **Estimated runtime** | ~30 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet build`
- **After every plan wave:** Run `dotnet test`
- **Before `/gsd-verify-work`:** Full suite must be green
- **Max feedback latency:** 30 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 26-01-01 | 01 | 1 | RENAME-01 | — | N/A | build | `dotnet build` | ✅ | ⬜ pending |
| 26-01-02 | 01 | 1 | RENAME-01 | — | N/A | build | `dotnet build` | ✅ | ⬜ pending |
| 26-02-01 | 02 | 2 | RENAME-02 | — | N/A | build+test | `dotnet test` | ✅ | ⬜ pending |
| 26-03-01 | 03 | 2 | RENAME-03 | — | N/A | build+test | `dotnet test` | ✅ | ⬜ pending |
| 26-04-01 | 04 | 3 | RENAME-04 | — | N/A | build+test | `dotnet test` | ✅ | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

*Existing infrastructure covers all phase requirements — xUnit test suite (191 tests) is already in place. No new test stubs needed; this phase is a pure rename.*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Production systemd unit uses `QuestBoard.Service.dll` | RENAME-04 | Requires SSH to live server | SSH to server, run `cat /etc/systemd/system/questboard.service`, verify `ExecStart` points to `QuestBoard.Service.dll` |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 30s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
