---
phase: 31
slug: unauthenticated-landing-redirect
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-06-30
---

# Phase 31 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit v3 + FluentAssertions + ASP.NET Core integration test host |
| **Config file** | `QuestBoard.IntegrationTests/QuestBoard.IntegrationTests.csproj` |
| **Quick run command** | `dotnet test QuestBoard.IntegrationTests --filter "HomeController|QuestController|Calendar|QuestLog|DungeonMaster|GroupPicker|GroupSession" --no-build` |
| **Full suite command** | `dotnet test` |
| **Estimated runtime** | ~60 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test QuestBoard.IntegrationTests --filter "HomeController|QuestController" --no-build`
- **After every plan wave:** Run `dotnet test`
- **Before `/gsd-verify-work`:** Full suite must be green
- **Max feedback latency:** ~60 seconds

---

## Per-Task Verification Map

| Task ID | Requirement | Behavior | Test Type | Automated Command | File Exists | Status |
|---------|-------------|----------|-----------|-------------------|-------------|--------|
| Auth lockdown | D-01 | `/Calendar` unauthenticated → 302 | integration | `dotnet test --filter "CalendarController"` | ✅ update | ⬜ pending |
| Auth lockdown | D-01 | `/QuestLog` unauthenticated → 302 | integration | `dotnet test --filter "QuestLogController"` | ✅ update | ⬜ pending |
| DM auth | D-02 | `/DungeonMaster/Profile/{id}` unauthenticated → 302 | integration | `dotnet test --filter "DungeonMasterController"` | ✅ check | ⬜ pending |
| Landing page | D-04 | `GET /` returns 200 for unauthenticated user | integration | `dotnet test --filter "HomeController"` | ✅ update | ⬜ pending |
| Landing page | D-04 | Landing page contains Login button, no quest cards | integration | `dotnet test --filter "HomeController"` | ✅ update | ⬜ pending |
| Quest route | D-05 | `GET /quests` returns 200 for authenticated user | integration | `dotnet test --filter "QuestController"` | ✅ add test | ⬜ pending |
| Quest route | D-05 | `GET /quests` returns 302 for unauthenticated user | integration | `dotnet test --filter "QuestController"` | ✅ add test | ⬜ pending |
| Picker redirect | D-07 | `POST /GroupPicker/SelectGroup` no returnUrl → `/quests` | integration | `dotnet test --filter "GroupPickerController"` | ✅ add assertion | ⬜ pending |
| Session middleware | D-09 | Authenticated + no session accessing `/quests` → 302 to group picker | integration | `dotnet test --filter "GroupSession"` | ❌ NEW FILE | ⬜ pending |
| Session middleware | D-09 | SuperAdmin + no session accessing `/quests` → NOT redirected | integration | `dotnet test --filter "GroupSession"` | ❌ NEW FILE | ⬜ pending |
| Picker loop guard | D-10 | `/GroupPicker/Index` with no session → 200 (not looped) | integration | `dotnet test --filter "GroupPicker"` | ✅ verify | ⬜ pending |

---

## Wave 0 Requirements

- [ ] `QuestBoard.IntegrationTests/Controllers/GroupSessionMiddlewareIntegrationTests.cs` — stubs for D-09 session-recovery middleware tests (new file, blocked on Wave 1 middleware implementation)

*Existing infrastructure (xUnit v3, WebApplicationFactoryBase, AuthenticationHelper) covers all other requirements.*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Landing page visual appearance | D-04 | CSS/layout cannot be asserted by integration tests | Browse to `http://localhost:5000/` as unauthenticated user; confirm app name, tagline, and "Log in" button are visible |
| Single-group session recovery flow | D-11 | Requires real session + auto-pick behavior | Log in as single-group user, clear session, navigate to `/quests`; confirm auto-redirect back to `/quests` without seeing picker |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 60s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
