---
phase: 30
slug: group-ux-admin-user-creation
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-06-30
---

# Phase 30 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit (ASP.NET Core integration tests) |
| **Config file** | `QuestBoard.Tests/QuestBoard.Tests.csproj` |
| **Quick run command** | `dotnet test --filter "Category=Unit"` |
| **Full suite command** | `dotnet test` |
| **Estimated runtime** | ~30 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet build`
- **After every plan wave:** Run `dotnet test`
- **Before `/gsd-verify-work`:** Full suite must be green
- **Max feedback latency:** 60 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 30-01-01 | 01 | 1 | UX-01 | — | Group picker shown only to multi-group users | integration | `dotnet test` | ✅ | ⬜ pending |
| 30-01-02 | 01 | 1 | UX-02 | — | SuperAdmin always sees picker | integration | `dotnet test` | ✅ | ⬜ pending |
| 30-01-03 | 01 | 1 | UX-03 | — | Session persists active group | integration | `dotnet test` | ✅ | ⬜ pending |
| 30-01-04 | 01 | 1 | UX-04 | — | Nav shows group name + switch link | manual | — | — | ⬜ pending |
| 30-01-05 | 01 | 1 | UX-05 | — | Switch group returns to picker | manual | — | — | ⬜ pending |
| 30-02-01 | 02 | 1 | MGMT-07 | — | Admin can create user in their group | integration | `dotnet test` | ✅ | ⬜ pending |
| 30-02-02 | 02 | 1 | MGMT-08 | — | Admin can change user role within group | integration | `dotnet test` | ✅ | ⬜ pending |
| 30-02-03 | 02 | 1 | REG-01 | — | Public registration returns 404 | integration | `dotnet test` | ✅ | ⬜ pending |
| 30-02-04 | 02 | 1 | REG-02 | — | Email confirmation triggered on admin-created user | manual | — | — | ⬜ pending |
| 30-02-05 | 02 | 1 | REG-03 | — | Admin-created user lands in correct group | integration | `dotnet test` | ✅ | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

Existing infrastructure covers all phase requirements. xUnit integration tests already exist in `QuestBoard.Tests/` — no new test framework setup needed.

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Nav shows active group name + Switch group link | UX-04 | Requires visual browser verification | Login as group member, verify nav bar shows group name and "Switch group" link |
| Clicking "Switch group" returns to picker | UX-05 | Requires browser navigation flow | Click "Switch group" in nav, verify redirect to GroupPicker/Index |
| Email confirmation received after admin creates user | REG-02 | Requires email delivery | Admin creates user, check that confirmation email is delivered to inbox |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 60s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
