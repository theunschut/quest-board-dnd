---
phase: 72
slug: platform-settings-token-contract
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-07-08
revised: 2026-07-11
---

# Phase 72 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.
>
> **Reset 2026-07-11:** The previous version of this file (status: approved, 2026-07-08) mapped tasks against the old fixed-column `IntegrationSettingEntity` design and the 5 plans built for it. That design was superseded by CONTEXT.md D-07–D-10 (generic key-value `PlatformSettingEntity` + a second group-override page) and the phase is being replanned from scratch. The Per-Task Verification Map below is reset to skeleton state — it needs repopulating against the new plan/task IDs once the planner produces them.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit v3 (`TestContext.Current.CancellationToken` pattern), NSubstitute for mocking, `Microsoft.AspNetCore.Mvc.Testing`/`WebApplicationFactoryBase` for integration tests |
| **Config file** | `QuestBoard.UnitTests/QuestBoard.UnitTests.csproj`, `QuestBoard.IntegrationTests/QuestBoard.IntegrationTests.csproj` |
| **Quick run command** | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~PlatformSetting"` |
| **Full suite command** | `dotnet test` |
| **Estimated runtime** | ~10-20 seconds (quick, scoped filter), full suite varies |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~PlatformSetting"`
- **After every plan wave:** Run `dotnet test` (full suite)
- **Before `/gsd-verify-work`:** Full suite must be green
- **Max feedback latency:** 60 seconds

---

## Per-Task Verification Map

*Reset pending replan — to be filled in once Phase 72's plans are regenerated against the D-07–D-10 key-value/cascade/two-page design (new `PlatformSettingEntity`, group-override controller/page, SETT-09/SETT-10).*

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| {N}-01-01 | 01 | 1 | REQ-{XX} | T-{N}-01 / — | {expected secure behavior or "N/A"} | unit | `{command}` | ✅ / ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `{tests/test_file.cs}` — stubs for the new `PlatformSettingService`/`PlatformSettingRepository` cascade lookup
- [ ] `{tests/conftest}` — shared fixtures
- [ ] `{framework install}` — none expected; xUnit v3/NSubstitute/`WebApplicationFactoryBase` are already fully set up

*If none: "Existing infrastructure covers all phase requirements."*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| {behavior} | REQ-{XX} | {reason} | {steps} |

*If none: "All phase behaviors have automated verification."*

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 60s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
