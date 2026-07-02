---
phase: 32-first-login-password-flow
plan: 02
subsystem: email-delivery-and-config
tags: [hangfire-jobs, razor-email-templates, rate-limiting, identity-token-config]
dependency-graph:
  requires: []
  provides:
    - WelcomeEmailJob.ExecuteAsync(string toEmail, string userName, string callbackUrl, CancellationToken)
    - ForgotPasswordEmailJob.ExecuteAsync(string toEmail, string callbackUrl, CancellationToken)
    - "forgot-password rate-limit policy name (Program.cs AddRateLimiter)"
    - "7-day DataProtectionTokenProviderOptions.TokenLifespan"
  affects:
    - QuestBoard.Service/Controllers/Admin/EmailPreviewController.cs
tech-stack:
  added: []
  patterns:
    - "IServiceScopeFactory.CreateAsyncScope() in every Hangfire job (locked v4.0 decision) — followed for both new jobs"
    - "Microsoft.AspNetCore.RateLimiting fixed-window policy partitioned by RemoteIpAddress, no new package"
key-files:
  created:
    - QuestBoard.Service/Jobs/WelcomeEmailJob.cs
    - QuestBoard.Service/Jobs/ForgotPasswordEmailJob.cs
    - QuestBoard.Service/Components/Emails/Welcome.razor
    - QuestBoard.Service/Components/Emails/ForgotPassword.razor
    - QuestBoard.UnitTests/Services/WelcomeEmailJobTests.cs
    - QuestBoard.UnitTests/Services/ForgotPasswordEmailJobTests.cs
  modified:
    - QuestBoard.Service/Controllers/Admin/EmailPreviewController.cs
    - QuestBoard.Service/Program.cs
  deleted:
    - QuestBoard.Service/Jobs/ConfirmationEmailJob.cs
    - QuestBoard.Service/Components/Emails/ConfirmEmail.razor
    - QuestBoard.UnitTests/Services/ConfirmationEmailJobTests.cs
decisions: []
metrics:
  duration: "~35 minutes (including worktree branch recovery)"
  completed: 2026-07-01
---

# Phase 32 Plan 02: Email Jobs, Templates, and Rate-Limit Config Summary

Built `WelcomeEmailJob`/`ForgotPasswordEmailJob` + their distinct Razor templates, retired the old `ConfirmationEmailJob`/`ConfirmEmail.razor` flow, updated the admin email-preview controller, and added the two `Program.cs` configuration blocks (7-day token lifespan, forgot-password rate limiter) that Plans 03/04's controllers will enqueue/consume against.

## What Was Built

**Task 1 — Jobs, templates, tests, retirement, preview controller:**
- `WelcomeEmailJob.ExecuteAsync(string toEmail, string userName, string callbackUrl, CancellationToken cancellationToken = default)` — renders `Welcome.razor` via `RenderAsync<Welcome>` with `UserName`/`CallbackUrl`/`AppUrl` keys, sends with subject "Welcome to the D&D Quest Board — set your password". Uses `IServiceScopeFactory.CreateAsyncScope()`, never constructor-injects scoped services (locked v4.0 pattern).
- `ForgotPasswordEmailJob.ExecuteAsync(string toEmail, string callbackUrl, CancellationToken cancellationToken = default)` — **no `userName` parameter** (per D-11: reset email carries no user-identifying content). Renders `ForgotPassword.razor` via `RenderAsync<ForgotPassword>` with `CallbackUrl`/`AppUrl` keys only, sends with subject "Reset your D&D Quest Board password".
- `Welcome.razor` and `ForgotPassword.razor` — visually distinct templates (per D-04/D-10, not shared/parameterized), both wrap `<_EmailLayout>`, follow the existing Cinzel/wax-seal visual style. `Welcome.razor` declares `UserName`/`CallbackUrl`/`AppUrl`; `ForgotPassword.razor` declares only `CallbackUrl`/`AppUrl` (parameter list matches the job exactly — no `UserName`).
- `WelcomeEmailJobTests.cs` and `ForgotPasswordEmailJobTests.cs` mirror the `ConfirmationEmailJobTests` NSubstitute scope-mocking pattern; 4 tests total (2 per job), all green in isolation before the retirement deletions.
- Deleted `ConfirmationEmailJob.cs`, `ConfirmEmail.razor`, `ConfirmationEmailJobTests.cs` (D-05 — confirmed no callers besides `AdminController`, which Plan 04 rewrites).
- `EmailPreviewController.cs`: removed the `ConfirmEmail()` preview action and its `Index()` menu link; added `Welcome()` and `ForgotPassword()` preview actions following the exact `ChangeEmailConfirm()` shape (preview `CallbackUrl` points at `/Account/SetPassword?userId=preview&token=preview-token`).

**Task 2 — Program.cs configuration:**
- `builder.Services.Configure<DataProtectionTokenProviderOptions>(options => options.TokenLifespan = TimeSpan.FromDays(7));` — net-new block (D-13/PWFLOW-06; framework default was 1 day, nothing previously configured this option).
- `builder.Services.AddRateLimiter(...)` registering a named `"forgot-password"` fixed-window policy: `PermitLimit = 3`, `Window = TimeSpan.FromMinutes(15)`, `QueueLimit = 0`, `AutoReplenishment = true`, partitioned by `httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"`; `OnRejected` returns HTTP 429 with a "Too many requests" message (D-12/PWFLOW-04).
- `app.UseRateLimiter();` added after `app.UseRouting()`, alongside the existing `UseSession()`/`UseAuthentication()`/`UseAuthorization()` block.
- No `ForwardedHeaders` configuration added — confirmed absent per plan instructions; documented in code comments as a manual deploy-environment verification item (threat T-32-04).
- No new NuGet package required — both `Microsoft.AspNetCore.RateLimiting` and the extended Identity APIs ship in the ASP.NET Core 10 shared framework.

## Final Job Signatures (for Plans 03/04)

```csharp
public class WelcomeEmailJob(IServiceScopeFactory scopeFactory, ILogger<WelcomeEmailJob> logger)
{
    public async Task ExecuteAsync(string toEmail, string userName, string callbackUrl, CancellationToken cancellationToken = default)
}

public class ForgotPasswordEmailJob(IServiceScopeFactory scopeFactory, ILogger<ForgotPasswordEmailJob> logger)
{
    public async Task ExecuteAsync(string toEmail, string callbackUrl, CancellationToken cancellationToken = default)
}
```

Rate-limit policy name for `[EnableRateLimiting(...)]`: **`"forgot-password"`**.

## Verification Evidence

- `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~WelcomeEmailJob|FullyQualifiedName~ForgotPasswordEmailJob"` — **4/4 passed** (run immediately after Task 1's (A)-(C) sub-steps, before the (D) retirement deletions took effect on the shared `QuestBoard.Service` project — see Known Limitation below).
- `dotnet build QuestBoard.Service` after Task 2 — confirmed **zero new errors** introduced by the Program.cs changes; the only build errors present are the two pre-existing, plan-documented `AdminController.cs` `CS0246: ConfirmationEmailJob` references (Plan 04's scope — see below).
- `grep ConfirmEmail QuestBoard.Service/Controllers/Admin/EmailPreviewController.cs` — one match, `ConfirmEmailChange` (an unrelated URL literal belonging to the untouched `ChangeEmailConfirm` flow), zero matches for `Components.Emails.ConfirmEmail`.
- `grep DataProtectionTokenProviderOptions|forgot-password|UseRateLimiter Program.cs` — all three present at the expected locations.
- File-existence checks: `ConfirmationEmailJob.cs`, `ConfirmEmail.razor`, `ConfirmationEmailJobTests.cs` confirmed deleted; `WelcomeEmailJob.cs`, `ForgotPasswordEmailJob.cs`, `Welcome.razor`, `ForgotPassword.razor`, `WelcomeEmailJobTests.cs`, `ForgotPasswordEmailJobTests.cs` confirmed present.

### Known Limitation: filtered test run cannot execute after Task 1(D)'s deletions land

The plan's `<action>` text states the filtered `dotnet test` run "is unaffected because the two new job tests + their jobs + templates all compile independently of AdminController." This is not accurate for a multi-project solution: `QuestBoard.UnitTests` references `QuestBoard.Service`, so **the entire `QuestBoard.Service` project must build** for any test in `QuestBoard.UnitTests` to run — including the two new job tests. Since `AdminController.cs` (at `HEAD`, prior to any Plan 02 changes) already references the now-deleted `ConfirmationEmailJob` type (lines 127, 287 — pre-existing code, unrelated to Plan 01's signature change), deleting `ConfirmationEmailJob.cs` in Task 1(D) makes `QuestBoard.Service` fail to build, which transitively blocks `dotnet test` for the whole `QuestBoard.UnitTests` project until Plan 04 rewrites `AdminController.CreateUser`/`SendConfirmationEmail`.

This was verified explicitly:
1. Ran the full 4-test filter immediately after creating the jobs/templates/tests (before deletion) — **4/4 green**.
2. Deleted the retired files per D-05.
3. Re-ran the same filter — build failed with exactly the two documented `AdminController.cs` `CS0246` errors, no other errors.
4. Ran `dotnet build QuestBoard.Service` again after Task 2's Program.cs changes — same two errors only, confirming Task 2 introduced no new breakage.

This is the plan's own accepted transient state (explicitly called out for `dotnet build` in both tasks' acceptance criteria), just inaccurately described as not affecting the test filter. No action needed from this plan — Plan 04 resolves it by rewriting `AdminController.cs`. Flagging here so Plan 04's executor and the phase verifier are not surprised by a red build until that plan lands.

## Deviations from Plan

None requiring a fix — the "Known Limitation" above is a documentation-accuracy note about the plan's own verification claim, not a defect introduced by this plan's implementation. No Rule 1-4 auto-fixes were needed; both tasks were implemented as specified.

## Auth Gates

None encountered.

## Self-Check: PASSED

All created files verified present; all three retired files verified absent; all three commits (`8e4bb9a`, `2925514`, `1a30653`) verified present in `git log`.
