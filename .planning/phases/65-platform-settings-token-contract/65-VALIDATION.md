---
phase: 65
slug: platform-settings-token-contract
status: approved
nyquist_compliant: true
wave_0_complete: true
created: 2026-07-08
---

# Phase 65 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit v3 (`TestContext.Current.CancellationToken` pattern), NSubstitute for mocking, `Microsoft.AspNetCore.Mvc.Testing`/`WebApplicationFactoryBase` for integration tests |
| **Config file** | `QuestBoard.UnitTests/QuestBoard.UnitTests.csproj`, `QuestBoard.IntegrationTests/QuestBoard.IntegrationTests.csproj` |
| **Quick run command** | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~IntegrationSetting"` |
| **Full suite command** | `dotnet test` |
| **Estimated runtime** | ~10-20 seconds (quick, scoped filter), full suite varies |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~IntegrationSetting"`
- **After every plan wave:** Run `dotnet test` (full suite)
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** 60 seconds

---

## Per-Task Verification Map

Five plans / nine tasks. Waves: 65-01 and 65-05 in Wave 1; 65-02 and 65-03 in Wave 2; 65-04 in Wave 3.

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 65-01-01 | 01 | 1 | SETT-06 | T-65-tenant | `IntegrationSettingEntity` is a singleton (`Id=1`, `ValueGeneratedNever`) with NO tenant query filter — instance-wide row | build | `dotnet build QuestBoard.Repository QuestBoard.Domain` | ✅ source | ⬜ pending |
| 65-01-02 | 01 | 1 | SETT-08 | — | Generated migration creates `IntegrationSettings` with a non-identity `Id` (no `SqlServer:Identity` annotation) | build/migration | `dotnet build QuestBoard.Repository` | ✅ migration | ⬜ pending |
| 65-02-01 | 02 | 2 | SETT-04, SETT-05 | T-65-05 | RED scaffold pinning blank/whitespace-preserve + enabled-flag + bootstrap behavior before implementation exists | unit (RED) | `MISSING —` Wave 0 RED scaffold; goes GREEN under 65-02-02's `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~IntegrationSettingService"` | ❌ W0 — new file, modeled on `GroupServiceTests.cs` | ⬜ pending |
| 65-02-02 | 02 | 2 | SETT-04, SETT-05, SETT-06 | T-65-05 | Blank/whitespace `newSecret` preserves existing `OmphalosSharedSecret`; `IsEnabled`/`OmphalosUrl` round-trip; singleton bootstrap-on-first-access | unit | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~IntegrationSettingService"` | ⬜ green after 65-02-01 scaffold | ⬜ pending |
| 65-03-01 | 03 | 2 | SETT-01, SETT-02, SETT-03, SETT-04, SETT-07 | T-65-01/02/03/05/06 | Controller-level `SuperAdminOnly`; both POSTs `[ValidateAntiForgeryToken]`; GET never pre-fills the real secret; blank→null guard; CSPRNG GenerateSecret persists immediately | build | `dotnet build QuestBoard.Service` | ✅ source | ⬜ pending |
| 65-03-02 | 03 | 2 | SETT-02, SETT-03, SETT-05 | T-65-03 | Views mask the secret (`type="password"`), render a configured/not-configured indicator, and never print the stored secret value | build | `dotnet build QuestBoard.Service` | ✅ source | ⬜ pending |
| 65-04-01 | 04 | 3 | SETT-01 | T-65-nav | Nav button lives only on the SuperAdminOnly `Group/Index` page (desktop + mobile); no shared nav item | build | `dotnet build QuestBoard.Service` | ✅ source | ⬜ pending |
| 65-04-02 | 04 | 3 | SETT-01, SETT-07 | T-65-02 | SuperAdmin GET → 200 end-to-end render; Admin/Player/unauthenticated denied before render (authorization matrix) | integration | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~Integrations"` | ❌ W0 — new file, modeled on `PlatformAreaIntegrationTests.cs` | ⬜ pending |
| 65-05-01 | 05 | 1 | TOKEN-02 | T-65-C1/C2/C3 | Written byte-exact HMAC contract (decode→verify→parse, `FixedTimeEquals`, single-use nonce, 300s TTL) documented before Phases 66/67 implement | doc/grep | `test -f .planning/TOKEN-CONTRACT.md && grep -q "FixedTimeEquals" .planning/TOKEN-CONTRACT.md` | ❌ W0 — new top-level doc, no code | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

*Sampling continuity: no run of three consecutive tasks lacks an automated verify. Every task either carries a runnable `<automated>` command or is a Wave 0 RED scaffold (65-02-01) that its same-plan/same-wave GREEN task (65-02-02) turns into a passing command.*

---

## Wave 0 Requirements

- [x] `QuestBoard.UnitTests/Services/IntegrationSettingServiceTests.cs` — created RED by **65-02 Task 1**, made GREEN by **65-02 Task 2**; covers SETT-04, SETT-05 (blank/whitespace-preserve guard, enabled-flag persistence, bootstrap), modeled directly on `GroupServiceTests.cs`'s NSubstitute-based pattern.
- [x] `QuestBoard.IntegrationTests/Controllers/IntegrationsAreaIntegrationTests.cs` — created by **65-04 Task 2**; covers SETT-01 (SuperAdmin 200 end-to-end render) and SETT-07 (authorization matrix: SuperAdmin/Admin/Player/unauthenticated), modeled directly on `PlatformAreaIntegrationTests.cs`'s existing four-case shape (same `AuthenticationHelper` calls, same assertions, retargeted URL).
- [x] `.planning/TOKEN-CONTRACT.md` — created by **65-05 Task 1**; no test framework involved; existence + content grep (`FixedTimeEquals`, `nonce`, `Base64Url`, `expiry`) is the only "test" for TOKEN-02's written-contract deliverable.

*No new test framework install needed — xUnit v3/NSubstitute/`WebApplicationFactoryBase` are already fully set up and proven for this exact `Areas/Platform` + `SuperAdminOnly` shape.*

---

## Manual-Only Verifications

*None — all phase behaviors have automated verification. The one non-code deliverable (the written token contract) is verified by file existence + content grep, not a manual step.*

---

## Validation Sign-Off

- [x] All tasks have `<automated>` verify or Wave 0 dependencies
- [x] Sampling continuity: no 3 consecutive tasks without automated verify
- [x] Wave 0 covers all MISSING references
- [x] No watch-mode flags
- [x] Feedback latency < 60s
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** approved (2026-07-08)
