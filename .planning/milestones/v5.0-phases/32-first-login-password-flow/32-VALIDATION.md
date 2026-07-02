---
phase: 32
slug: first-login-password-flow
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-07-01
---

# Phase 32 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit v3, `Microsoft.AspNetCore.Mvc.Testing` (`WebApplicationFactory`) for integration tests, `NSubstitute` + `FluentAssertions` for unit tests |
| **Config file** | none — per-project `.csproj` settings, `GlobalUsings.cs` per project |
| **Quick run command** | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~IdentityService\|FullyQualifiedName~WelcomeEmailJob\|FullyQualifiedName~ForgotPasswordEmailJob"` |
| **Full suite command** | `dotnet test` |
| **Estimated runtime** | ~60-90 seconds (55 unit + 181 integration tests currently, expect growth) |

---

## Sampling Rate

- **After every task commit:** `dotnet test QuestBoard.UnitTests` (fast) plus targeted integration test filter for the controller under change
- **After every plan wave:** `dotnet test` (full suite)
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** ~90 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| TBD | TBD | TBD | PWFLOW-01 | — | Passwordless `CreateUserAsync` leaves `PasswordHash` null; no password validators run | unit | `dotnet test --filter FullyQualifiedName~IdentityServiceTests` | ❌ W0 | ⬜ pending |
| TBD | TBD | TBD | PWFLOW-02 | — | `SetPassword` sets both `PasswordHash` and `EmailConfirmed = true` in one action | integration | `dotnet test --filter FullyQualifiedName~AccountControllerIntegrationTests` | ❌ W0 | ⬜ pending |
| TBD | TBD | TBD | PWFLOW-03 | — | Passwordless account cannot sign in (`PasswordSignInAsync` returns `Failed`, no exception) | integration | `dotnet test --filter FullyQualifiedName~AccountControllerIntegrationTests` | ❌ W0 | ⬜ pending |
| TBD | TBD | TBD | PWFLOW-04 | Email enumeration / DoS via inbox flood | `ForgotPassword` POST is enumeration-safe and rate-limited (3/15min per IP) | integration | `dotnet test --filter FullyQualifiedName~AccountControllerIntegrationTests` | ❌ W0 | ⬜ pending |
| TBD | TBD | TBD | PWFLOW-05 | — | "Resend welcome email" button visible only when `EmailConfirmed == false`; enqueues `WelcomeEmailJob` | integration | `dotnet test --filter FullyQualifiedName~AdminControllerIntegrationTests` | ✅ existing | ⬜ pending |
| TBD | TBD | TBD | PWFLOW-06 | Long-lived token exposure window | `TokenLifespan` configured to 7 days | unit | `dotnet test --filter FullyQualifiedName~ProgramConfigurationTests` (new) | ❌ W0 | ⬜ pending |

*Task ID / Plan / Wave columns to be filled in by the planner once PLAN.md files exist. Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `QuestBoard.UnitTests/Services/WelcomeEmailJobTests.cs` — mirror `ConfirmationEmailJobTests.cs` exactly (same scope-factory mocking pattern), covering PWFLOW-02
- [ ] `QuestBoard.UnitTests/Services/ForgotPasswordEmailJobTests.cs` — same pattern, covering PWFLOW-04
- [ ] New test cases in `QuestBoard.IntegrationTests/Controllers/AccountControllerIntegrationTests.cs` for `ForgotPassword` GET/POST and `SetPassword` GET/POST, covering PWFLOW-02/03/04
- [ ] Adapt existing `SendConfirmationEmail`-related tests in `AdminControllerIntegrationTests.cs` to the renamed/repurposed `WelcomeEmailJob` path, covering PWFLOW-05
- [ ] Delete `QuestBoard.UnitTests/Services/ConfirmationEmailJobTests.cs` per CONTEXT.md's Files to Delete list — replace with `WelcomeEmailJobTests.cs`
- [ ] No dedicated rate-limiter test framework in place — a targeted integration test issuing 4 rapid POSTs to `/Account/ForgotPassword` from the test client, asserting the 4th gets HTTP 429

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|--------------------|
| Welcome / Forgot Password email visual rendering (Cinzel/wax-seal style) | PWFLOW-02 / PWFLOW-04 | Email client rendering (fonts, images) not practically assertable in automated tests | Use `EmailPreviewController` preview actions to visually inspect `Welcome.razor` and `ForgotPassword.razor` in a browser before shipping |
| Reverse-proxy IP partitioning for rate limiter | PWFLOW-04 | Depends on deployment topology (whether `ForwardedHeadersMiddleware` is configured) — not verifiable from unit/integration tests alone | Grep `Program.cs` for `ForwardedHeaders` during planning; if absent, manually verify in the deployed environment that `RemoteIpAddress` reflects the real client, not the reverse proxy |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 90s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
