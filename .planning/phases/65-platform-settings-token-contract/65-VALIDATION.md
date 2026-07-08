---
phase: 65
slug: platform-settings-token-contract
status: draft
nyquist_compliant: false
wave_0_complete: false
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

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 65-01-01 | 01 | 1 | SETT-06, SETT-08 | — | `IntegrationSettingEntity` migration applies cleanly; singleton row (`Id=1`, `ValueGeneratedNever()`) is not tenant-query-filtered | build/migration | `dotnet ef migrations add AddIntegrationSettings --project ../QuestBoard.Repository` then covered implicitly by any integration test hitting the new controller against the test DB | ❌ W0 | ⬜ pending |
| 65-01-02 | 01 | 1 | SETT-04, SETT-05 | — | Blank `newSecret` preserves existing `OmphalosSharedSecret`; non-blank overwrites; `IsEnabled` persists correctly | unit | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~IntegrationSettingServiceTests"` | ❌ W0 — new file, modeled on `GroupServiceTests.cs` | ⬜ pending |
| 65-02-01 | 02 | 2 | SETT-01, SETT-02, SETT-03 | — | SuperAdmin can navigate to `/platform/Integrations`; page renders URL input + `type="password"` secret input | integration | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~Integrations"` | ❌ W0 — new file, modeled on `PlatformAreaIntegrationTests.cs` | ⬜ pending |
| 65-02-02 | 02 | 2 | SETT-07 | V4 Access Control | Non-SuperAdmin (Admin, DungeonMaster, Player, unauthenticated) cannot reach the settings page — 403/redirect before controller construction | integration | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~Integrations"` (same file, authorization-matrix cases mirroring `PlatformAreaIntegrationTests.cs`'s existing 4-case shape) | ❌ W0 | ⬜ pending |
| 65-02-03 | 02 | 2 | SETT-04 (UI half) | Information Disclosure | GET never populates `SharedSecret` with the real stored value; renders a "configured" indicator instead | integration | Covered by the same `IntegrationsAreaIntegrationTests.cs` — assert rendered HTML never contains the raw secret value | ❌ W0 | ⬜ pending |
| 65-03-01 | 03 | 2/3 | TOKEN-02 (written contract only) | — | `.planning/TOKEN-CONTRACT.md` exists, documents the byte-exact sign/verify sequence (Pitfall 1) and the full field list/types (nonce, userId, questId, questTitle, questDate, expiry) | doc/grep | `test -f .planning/TOKEN-CONTRACT.md` and `grep -q "FixedTimeEquals" .planning/TOKEN-CONTRACT.md` | ❌ W0 — new top-level doc, no code | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `QuestBoard.UnitTests/Services/IntegrationSettingServiceTests.cs` — covers SETT-04, SETT-05 (blank-preserve guard, enabled-flag persistence), modeled directly on `GroupServiceTests.cs`'s NSubstitute-based pattern
- [ ] `QuestBoard.IntegrationTests/Controllers/IntegrationsAreaIntegrationTests.cs` — covers SETT-01, SETT-02, SETT-03, SETT-04 (GET never leaks secret), SETT-07 (authorization matrix: SuperAdmin/Admin/DungeonMaster/Player/unauthenticated), modeled directly on `PlatformAreaIntegrationTests.cs`'s existing shape (same `AuthenticationHelper` calls, same assertions, retargeted URL)
- [ ] `.planning/TOKEN-CONTRACT.md` — no test framework involved; existence + content grep is the only "test" for TOKEN-02's written-contract deliverable

*No new test framework install needed — xUnit v3/NSubstitute/`WebApplicationFactoryBase` are already fully set up and proven for this exact `Areas/Platform` + `SuperAdminOnly` shape.*

---

## Manual-Only Verifications

*None — all phase behaviors have automated verification. The one non-code deliverable (the written token contract) is verified by file existence + content grep, not a manual step.*

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 60s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
