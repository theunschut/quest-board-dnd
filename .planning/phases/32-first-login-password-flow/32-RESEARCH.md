# Phase 32: First-Login Password Flow - Research

**Researched:** 2026-07-01
**Domain:** ASP.NET Core 10 Identity (passwordless account creation, token-based password reset), ASP.NET Core rate limiting middleware
**Confidence:** HIGH

## Summary

This phase replaces admin-set passwords with a passwordless account-creation flow (`UserManager.CreateAsync(user)` with no password overload) plus a combined "Welcome — set your password and confirm your email" link, and adds a self-service Forgot Password flow that shares the same `SetPassword` landing action. All four of CONTEXT.md's flagged uncertainties (D-02 Identity behavior, rate-limiting syntax, token-provider scoping, and token round-trip compatibility) were verified directly against ASP.NET Core's actual source code (dotnet/aspnetcore `main` branch, which tracks .NET 10) and current Microsoft Learn documentation dated 2025-11-26 for aspnetcore-10.0. All four are confirmed to work exactly as CONTEXT.md hoped, with no surprises.

One material discrepancy was found during codebase verification: CONTEXT.md's D-08 and code_context describe a "stripped-down auth layout... no main nav" that `Login.cshtml` supposedly already uses. This layout does not exist. `Login.cshtml` and `Login.Mobile.cshtml` use the **standard** `_Layout.cshtml` / `_Layout.Mobile.cshtml` via `_ViewStart.cshtml` — the same layout as authenticated pages, which conditionally hides only nav *menu items* (not the nav bar itself) for unauthenticated users. `_Layout.GroupPicker.cshtml` is a genuinely stripped-down layout, but it renders a Logout button and assumes an authenticated user — wrong fit for an anonymous ForgotPassword/SetPassword page. The planner must decide explicitly: reuse the standard `_Layout.cshtml` (as Login.cshtml actually does today, zero new layout work) or design a new minimal layout. This is called out as an assumption requiring a locked decision before planning proceeds on this specific point.

A second gap surfaced: `IIdentityService` has no method for directly setting `EmailConfirmed = true` outside of the token-verified `ConfirmEmailAsync` path. D-09 requires `SetPassword` to mark `EmailConfirmed = true` on success as a **side effect** of a password-reset token verification, not an email-confirmation token verification — these are different token purposes/providers in Identity and cannot be conflated. The clean fix (verified against source): `IdentityUser<TKey>.EmailConfirmed` is a `public virtual bool { get; set; }` property directly on the entity — no need to go through `IUserEmilStore` casting. A new `IIdentityService` method can simply set `entity.EmailConfirmed = true` and call `userManager.UpdateAsync(entity)` after the existing `ResetPasswordAsync` call succeeds.

**Primary recommendation:** Implement `SetPassword` as: decode token → call `identityService.ResetPasswordAsync(userId, token, newPassword)` (existing, reused as-is) → on success, add a new `IIdentityService.ConfirmEmailDirectlyAsync(int userId)` (or fold into a new combined method) that sets `EmailConfirmed = true` via direct property write + `UpdateAsync` → redirect to Login with success TempData. Use the exact `Microsoft.AspNetCore.RateLimiting` `AddFixedWindowLimiter` + `[EnableRateLimiting("policy")]` pattern from the Verified Code Examples section for D-12. Confirm the auth-layout question with the user/planner before writing `ForgotPassword.cshtml`/`SetPassword.cshtml`.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Passwordless user creation | API / Backend (`IIdentityService`/`IdentityService`) | — | Identity's `UserManager.CreateAsync(user)` is a Repository-layer concern; Domain/Service layers just call through |
| Password-reset token generation/validation | API / Backend (`IIdentityService`) | — | Wraps `UserManager.GeneratePasswordResetTokenAsync`/`ResetPasswordAsync`; token crypto handled entirely by Identity's DataProtection stack |
| Welcome / Forgot-password email dispatch | API / Backend (Hangfire job, Service layer) | — | Existing `IServiceScopeFactory` + `CreateAsyncScope()` job pattern; must not be constructor-injected (locked v4.0 architectural decision) |
| Email template rendering | API / Backend (Razor components rendered server-side via `HtmlRenderer`) | — | `IEmailRenderService` renders `.razor` files server-side in job context, not via MVC view engine (locked v4.0 decision) |
| ForgotPassword / SetPassword forms | Frontend Server (SSR, `AccountController`) | Browser (basic form validation via unobtrusive jQuery validation, already wired via `_Layout.cshtml`) | Anonymous MVC actions rendering Razor views — standard SSR pattern already used by `Login`/`ConfirmEmail` |
| Rate limiting on ForgotPassword POST | API / Backend (ASP.NET Core middleware, `Program.cs`) | — | `Microsoft.AspNetCore.RateLimiting` operates at the ASP.NET Core middleware/endpoint layer, before the controller action runs |
| Admin "Resend welcome email" button | Frontend Server (SSR, `AdminController`/`Views/Admin/Users.cshtml`) | — | Existing row-action + TempData pattern, no new architectural surface |

## User Constraints (from CONTEXT.md)

### Locked Decisions

- **D-01:** Admin-created accounts are created via ASP.NET Identity's passwordless `UserManager.CreateAsync(user)` overload (no `PasswordHash` set at all) — not a generated throwaway password. `IIdentityService.CreateUserAsync` (or a new method) must stop requiring a `password` argument for this path. `CreateUserViewModel.Password` field is removed entirely; `Views/Admin/CreateUser.cshtml` and `CreateUser.Mobile.cshtml` lose the password input.
- **D-02 (verified — see Common Pitfalls / Code Examples below):** `UserManager.CheckPasswordAsync` / `SignInManager.PasswordSignInAsync` correctly reject sign-in attempts against a passwordless account (no `PasswordHash`) rather than erroring — confirmed against actual ASP.NET Core 10 Identity source.
- **D-03:** One combined link does both jobs: clicking a "Set your password" link and successfully submitting a new password sets `PasswordHash` AND marks `EmailConfirmed = true` in the same action. No separate "confirm email" step for admin-created users.
- **D-04:** New dedicated `Welcome.razor` email template (in `QuestBoard.Service/Components/Emails/`) + new `WelcomeEmailJob` (in `QuestBoard.Service/Jobs/`), following the existing Cinzel/wax-seal visual style (see `ConfirmEmail.razor` for the pattern to follow, then retire it).
- **D-05:** The old `ConfirmEmail.razor` + `ConfirmationEmailJob` are retired (deleted) — confirmed they have no other callers (verified: only `AdminController.CreateUser` and `AdminController.SendConfirmationEmail` reference them). The separate email-address-change flow (`ChangeEmailConfirm.razor` + `ChangeEmailConfirmationJob`, triggered from `AccountController.Edit` / `AdminController.EditUser`) is untouched.
- **D-06:** `AdminController.CreateUser` no longer calls `identityService.GenerateEmailConfirmationAsync` + enqueues `ConfirmationEmailJob`. Instead it generates a password-reset token (see D-11) and enqueues `WelcomeEmailJob` with a link to the shared `SetPassword` action (see D-09).
- **D-07:** The existing "Send Confirmation Email" button on `Views/Admin/Users.cshtml` (shown when `EmailConfirmed == false`) is repurposed to send the new combined Welcome email instead of the old confirm-only one — same trigger condition, same button, new job underneath. Effectively becomes the "Resend welcome email" action (see D-14).
- **Deferred (explicitly out of scope):** A "password changed" notification email for `AccountController.ChangePassword` or `AdminController.ResetPassword` — neither sends an email today; adding one is a separate future idea, not part of this phase.
- **D-08:** New `GET/POST /Account/ForgotPassword` action pair in `AccountController` — a dedicated view with just an email field ("Send reset link" button), using **[NEEDS RESOLUTION — see Assumptions Log A1]** the same stripped-down auth layout as `Login.cshtml` (no main nav). A "Forgot password?" link is added to `Login.cshtml` and `Login.Mobile.cshtml` pointing to this new action.
- **D-09:** A shared `GET/POST /Account/SetPassword?userId=X&token=Y` action pair is the landing page for BOTH the welcome-email link and the forgot-password-email link. Same mechanics for both: decode token, call `identityService.ResetPasswordAsync(userId, token, newPassword)` (already exists on `IIdentityService`/`IUserService` — no interface change needed here), on success mark `EmailConfirmed = true` and redirect to Login with a success TempData message.
- **D-10:** New `ForgotPassword.razor` email template (separate from `Welcome.razor`, same visual style) sent by a new job when a user requests a reset via `/Account/ForgotPassword`.
- **D-11:** `ForgotPassword` POST always shows the same generic response regardless of whether the submitted email matches an account: *"If that email is registered, a reset link has been sent."* No email is sent for non-existent accounts, but the UI gives no indication either way — prevents email enumeration.
- **D-12:** Basic rate limiting on the `ForgotPassword` POST action using ASP.NET Core's built-in `Microsoft.AspNetCore.RateLimiting` middleware (ships with .NET, no new package) — fixed-window policy, 3 requests per 15 minutes per client IP, applied via `[EnableRateLimiting("...")]` on the action or a scoped policy in `Program.cs`.
- **D-13:** Extend `DataProtectionTokenProviderOptions.TokenLifespan` (configured in `Program.cs` alongside the existing Identity setup) to **7 days**. This is the shared "Default" token provider used by password-reset, email-confirmation, and change-email tokens alike — extending it uniformly is acceptable since none of those flows currently need a shorter window. **(Verified — see Common Pitfalls below: this is not currently configured at all in `Program.cs`, so this is a net-new configuration block, not an edit to an existing one.)**
- **D-14:** Admin gets a "Resend welcome email" button on `Views/Admin/Users.cshtml`, shown for any account with `EmailConfirmed == false` (i.e., hasn't completed first login yet) — same row-action + TempData feedback pattern as the existing Promote/Demote/ResetPassword buttons (see D-07 — this is the same button, repurposed).

### Claude's Discretion

- Exact route/action naming beyond what's specified above (follows existing `ConfirmEmail`/`ConfirmEmailChange` naming convention in `AccountController`).
- Whether `IIdentityService.CreateUserAsync` changes signature in place or a new method is added alongside it — planner's call based on how many other callers exist (verified: `IUserService.CreateAsync` in `UserService.cs` is the only caller of `IIdentityService.CreateUserAsync`, and `AdminController.CreateUser` is the only caller of `IUserService.CreateAsync` — see Integration Points below).
- Whether the rate-limiting policy is a named policy in `Program.cs`'s `AddRateLimiter(...)` or an inline partitioner — implementation detail.
- Exact copy/wording and layout details of `Welcome.razor` and `ForgotPassword.razor` (follow the established Cinzel/wax-seal visual style from `ConfirmEmail.razor` / `ChangeEmailConfirm.razor`).
- Whether the forgot-password confirmation message renders inline on the same page or via a redirect to a small confirmation view.

### Deferred Ideas (OUT OF SCOPE)

- **"Password changed" notification email** — for `AccountController.ChangePassword` (self-service) and/or `AdminController.ResetPassword` (admin-triggered). Neither currently sends any email; deferred to a future phase once `Welcome.razor` establishes the pattern.

## Phase Requirements

> v5.0 `REQUIREMENTS.md` does not currently list a forgot-password requirement. Per CONTEXT.md's canonical_refs, the planner must add one. No requirement IDs were assigned to this phase before research; the table below proposes IDs for the planner to adopt or rename.

| ID (proposed) | Description | Research Support |
|----|-------------|------------------|
| PWFLOW-01 | Admin-created accounts have no password at creation (`UserManager.CreateAsync(user)`, no-password overload); `CreateUserViewModel.Password` removed | Verified via ASP.NET Core source: `CreateCoreAsync` never hashes/validates a password when called via this overload — see Code Examples |
| PWFLOW-02 | New user receives one combined "Welcome — set your password" email that sets `PasswordHash` and `EmailConfirmed = true` in a single action (`SetPassword`) | Verified: `GeneratePasswordResetTokenAsync`/`ResetPasswordAsync` share purpose string `"ResetPassword"` and provider — safe token round-trip; `EmailConfirmed` is a directly settable property |
| PWFLOW-03 | Passwordless accounts cannot sign in until `SetPassword` is completed — `PasswordSignInAsync` must fail gracefully (not throw) against a null `PasswordHash` | Verified against `VerifyPasswordAsync` source: explicit `if (hash == null) return PasswordVerificationResult.Failed;` |
| PWFLOW-04 (new requirement) | Self-service "Forgot password?" flow: `GET/POST /Account/ForgotPassword`, enumeration-safe generic response, rate-limited 3 req/15 min per IP | Verified current `Microsoft.AspNetCore.RateLimiting` API (aspnetcore-10.0 docs, 2025-11-26) — see Code Examples |
| PWFLOW-05 | Admin "Resend welcome email" button replaces "Send Confirmation Email" on `Views/Admin/Users.cshtml`, same `EmailConfirmed == false` trigger condition | Verified: existing button/condition already present at `Users.cshtml:142-151`, only the underlying job/label changes |
| PWFLOW-06 | `DataProtectionTokenProviderOptions.TokenLifespan` set to 7 days, uniformly affecting password-reset, email-confirmation, and change-email tokens (2FA/Authenticator tokens unaffected — feature not implemented in this codebase) | Verified: `TokenOptions` defaults `PasswordResetTokenProvider`, `EmailConfirmationTokenProvider`, `ChangeEmailTokenProvider` all to `DefaultProvider`; no override exists in current `Program.cs` |

The planner should reconcile these proposed IDs with `.planning/ROADMAP.md` §Phase 32 and add the accepted set to `.planning/REQUIREMENTS.md` under a new "Password Flow" or "Auth" section, then update the Traceability table.

## Standard Stack

### Core

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Microsoft.AspNetCore.Identity (built into shared framework) | 10.0 (matches `dotnet --version` 10.0.301 confirmed in this repo) | User creation, password hashing, token generation/validation | Already the app's identity system; no new package |
| Microsoft.AspNetCore.Identity.UI | 10.0.9 [VERIFIED: csproj] | Pulled in transitively; not directly used for UI in this app (custom views) | Already referenced in `QuestBoard.Service.csproj` |
| Microsoft.AspNetCore.RateLimiting (built into shared framework, `Microsoft.AspNetCore.App`) | 10.0 (ships with .NET 10 runtime) | Fixed-window rate limiting on `ForgotPassword` POST | No NuGet package needed — part of the ASP.NET Core shared framework since .NET 7; confirmed current for .NET 10 via Microsoft Learn (aspnetcore-10.0 moniker, updated 2025-11-26) |

**No new NuGet packages are required for this phase.** Both `Microsoft.AspNetCore.RateLimiting` and the additional Identity APIs used (`UserManager.CreateAsync(user)`, `GeneratePasswordResetTokenAsync`) are part of the already-referenced ASP.NET Core shared framework / `Microsoft.AspNetCore.Identity.UI` package.

### Supporting

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| Hangfire.AspNetCore / Hangfire.SqlServer | 1.8.23 [VERIFIED: csproj] | Background job execution for `WelcomeEmailJob` / `ForgotPasswordEmailJob` | Already used for all 4 existing email jobs; new jobs follow identical `IServiceScopeFactory` pattern |

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Built-in `Microsoft.AspNetCore.RateLimiting` | AspNetCoreRateLimit (third-party NuGet package) | Third-party package adds a dependency and its own config surface for functionality the .NET 10 shared framework already provides natively; D-12 explicitly locks in "no new package" |
| Direct `EmailConfirmed` property write | `IUserEmailStore<TUser>.SetEmailConfirmedAsync` via store cast | Both work; direct property write is simpler since `UserEntity : IdentityUser<int>` exposes `EmailConfirmed` as a public settable property — no casting needed, matches this codebase's existing convention of manipulating entity properties directly before `UpdateAsync`/`SaveChangesAsync` |

## Package Legitimacy Audit

No external packages are installed in this phase — `Microsoft.AspNetCore.RateLimiting` ships as part of the `Microsoft.AspNetCore.App` shared framework (already referenced implicitly via the ASP.NET Core SDK) and requires no `PackageReference` addition. `Microsoft.AspNetCore.Identity.UI` 10.0.9 is already present in `QuestBoard.Service.csproj`.

**Packages removed due to slopcheck [SLOP] verdict:** none (no packages evaluated — none proposed for install)
**Packages flagged as suspicious [SUS]:** none

*Package Legitimacy Gate skipped: this phase requires zero new package installs. If the planner discovers a need for a package during implementation (e.g., a phone/SMS token provider), re-run this gate at that time.*

## Architecture Patterns

### System Architecture Diagram

```
                    ┌─────────────────────────────────────────┐
                    │           AdminController                 │
                    │  CreateUser (POST)                         │
                    │    1. userService.CreateAsync(email, name) │  ← D-01: no password arg
                    │    2. SetGroupRoleAsync(...)                │
                    │    3. identityService.GenPwdResetToken()    │  ← D-06/D-11 (new method)
                    │    4. jobClient.Enqueue<WelcomeEmailJob>    │
                    └───────────────────┬─────────────────────┘
                                        │ enqueue
                                        ▼
                    ┌─────────────────────────────────────────┐
                    │            Hangfire Job Queue              │
                    │  WelcomeEmailJob.ExecuteAsync(...)          │
                    │    IServiceScopeFactory.CreateAsyncScope()  │
                    │    → IEmailRenderService.RenderAsync<Welcome>│
                    │    → IEmailService.SendAsync(...)           │
                    └───────────────────┬─────────────────────┘
                                        │ SMTP/Resend
                                        ▼
                              [ New user's inbox ]
                                        │ clicks "Set your password"
                                        ▼
                    ┌─────────────────────────────────────────┐
                    │           AccountController                │
                    │  SetPassword (GET) — render form            │
                    │  SetPassword (POST)                          │
                    │    1. decode token (Base64Url)               │
                    │    2. identityService.ResetPasswordAsync(...)│  ← existing, reused
                    │    3. on success: mark EmailConfirmed = true │  ← NEW IIdentityService method
                    │    4. redirect to Login w/ success TempData  │
                    └─────────────────────────────────────────┘

        ── Parallel self-service path (D-08/D-09/D-10/D-11/D-12) ──

                    ┌─────────────────────────────────────────┐
                    │           AccountController                │
                    │  ForgotPassword (GET) — render email form    │
                    │  ForgotPassword (POST)  [EnableRateLimiting] │  ← D-12: 3 req/15min per IP
                    │    1. look up user by email (no leak)        │
                    │    2. IF found: gen token, enqueue job        │
                    │    3. ALWAYS: same generic TempData message   │  ← D-11: enumeration-safe
                    └───────────────────┬─────────────────────┘
                                        │ enqueue (only if user found)
                                        ▼
                    ┌─────────────────────────────────────────┐
                    │            Hangfire Job Queue              │
                    │  ForgotPasswordEmailJob.ExecuteAsync(...)   │
                    │    → IEmailRenderService.RenderAsync<ForgotPassword>│
                    │    → IEmailService.SendAsync(...)           │
                    └───────────────────┬─────────────────────┘
                                        │ SMTP/Resend
                                        ▼
                          [ existing user's inbox ]
                                        │ clicks "Reset password"
                                        ▼
                          (same SetPassword action as above)
```

### Recommended Project Structure

No new folders — all new files land in existing conventional locations:
```
QuestBoard.Service/
├── Controllers/Admin/
│   ├── AccountController.cs      # + ForgotPassword (GET/POST), SetPassword (GET/POST)
│   └── AdminController.cs        # CreateUser + SendConfirmationEmail modified (D-06/D-07)
├── ViewModels/AccountViewModels/
│   ├── ForgotPasswordViewModel.cs   # new: Email field only
│   └── SetPasswordViewModel.cs      # new: UserId, Token, NewPassword, ConfirmPassword
├── Views/Account/
│   ├── ForgotPassword.cshtml + .Mobile.cshtml   # new
│   └── SetPassword.cshtml + .Mobile.cshtml      # new
├── Jobs/
│   ├── WelcomeEmailJob.cs            # new
│   └── ForgotPasswordEmailJob.cs     # new (naming at planner's discretion)
└── Components/Emails/
    ├── Welcome.razor                 # new
    └── ForgotPassword.razor          # new
```

### Pattern 1: Passwordless account creation

**What:** Create an Identity user with no password set, deferring password creation to a later self-service step.
**When to use:** Admin-created accounts (D-01).
**Example:**
```csharp
// Source: verified against dotnet/aspnetcore main branch, src/Identity/Extensions.Core/src/UserManager.cs
// CreateAsync(TUser user) — the no-password overload — never touches PasswordHash:
public virtual async Task<IdentityResult> CreateAsync(TUser user)
{
    var result = await CreateCoreAsync(user).ConfigureAwait(false);
    return result;
}
// CreateCoreAsync calls ValidateUserAsync(user) only — no password validators run,
// no PasswordHasher invoked. Resulting entity has PasswordHash == null in the DB.

// IdentityService.cs — new/modified method:
public async Task<IdentityResult> CreateUserAsync(string email, string name)
{
    var entity = new UserEntity { UserName = email, Email = email, Name = name };
    var result = await userManager.CreateAsync(entity); // no-password overload
    if (result.Succeeded)
        await userManager.AddToRoleAsync(entity, "Player");
    return result;
}
```

### Pattern 2: Shared password-reset token for two flows

**What:** One token-generation primitive (`GeneratePasswordResetTokenAsync`) and one consumption primitive (`ResetPasswordAsync`) serve both the Welcome flow and the Forgot Password flow, because both purposes are semantically "set/reset this account's password."
**When to use:** `SetPassword` GET/POST action, called from both email link types (D-09).
**Example:**
```csharp
// Source: verified against dotnet/aspnetcore main branch, UserManager.cs
public virtual Task<string> GeneratePasswordResetTokenAsync(TUser user)
    => GenerateUserTokenAsync(user, Options.Tokens.PasswordResetTokenProvider, ResetPasswordTokenPurpose);

public virtual async Task<IdentityResult> ResetPasswordAsync(TUser user, string token, string newPassword)
{
    if (!await VerifyUserTokenAsync(user, Options.Tokens.PasswordResetTokenProvider, ResetPasswordTokenPurpose, token))
        return IdentityResult.Failed(ErrorDescriber.InvalidToken());
    // ... sets new password hash, updates security stamp
}
// Both use purpose string "ResetPassword" and Options.Tokens.PasswordResetTokenProvider
// (defaults to "Default" = DataProtectorTokenProvider<TUser>) — tokens are interchangeable
// between generation call sites (AdminController.CreateUser and AccountController.ForgotPassword)
// as long as they both call the SAME GeneratePasswordResetTokenAsync method.
```

### Pattern 3: Enumeration-safe generic response (D-11)

**What:** `ForgotPassword` POST always returns the same message and status regardless of whether the email exists.
**When to use:** Any self-service account-recovery endpoint.
**Example:**
```csharp
[HttpPost]
[ValidateAntiForgeryToken]
[EnableRateLimiting("forgot-password")]
public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
{
    if (ModelState.IsValid)
    {
        var userId = await identityService.GetIdByEmailAsync(model.Email);
        if (userId.HasValue)
        {
            var rawToken = await identityService.GeneratePasswordResetTokenAsync(userId.Value); // new method
            var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(rawToken));
            var callbackUrl = Url.Action(nameof(SetPassword), "Account",
                new { userId = userId.Value, token = encodedToken }, Request.Scheme);
            jobClient.Enqueue<ForgotPasswordEmailJob>(j => j.ExecuteAsync(model.Email, callbackUrl!, CancellationToken.None));
        }
        // Same message whether or not userId.HasValue — no branch on the response.
        TempData["Success"] = "If that email is registered, a reset link has been sent.";
        return RedirectToAction(nameof(ForgotPassword));
    }
    return View(model);
}
```

### Pattern 4: Rate limiting a single POST action (D-12)

**What:** Fixed-window limiter scoped to one action, partitioned by client IP.
**When to use:** `ForgotPassword` POST only — not a global limiter.
**Example:**
```csharp
// Source: Microsoft Learn, "Rate limiting middleware in ASP.NET Core", aspnetcore-10.0 moniker,
// updated 2025-11-26 — https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit?view=aspnetcore-10.0

// Program.cs — registration (add near other builder.Services.Add* calls):
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("forgot-password", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 3,
                Window = TimeSpan.FromMinutes(15),
                QueueLimit = 0,               // no queueing — reject immediately over limit
                AutoReplenishment = true
            }));

    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await context.HttpContext.Response.WriteAsync(
            "Too many requests. Please try again later.", cancellationToken);
    };
});

// Program.cs — middleware pipeline (must be added AFTER app.UseRouting(),
// BEFORE app.UseAuthorization() is fine either order relative to auth, but
// MUST come after UseRouting when using endpoint-specific [EnableRateLimiting]):
app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseMiddleware<GroupSessionMiddleware>();
app.UseRateLimiter();          // <-- NEW: add here, after UseRouting, order relative
                                //     to UseAuthentication/UseAuthorization is flexible
app.UseAuthorization();

// AccountController.cs — action attribute:
[HttpPost]
[ValidateAntiForgeryToken]
[EnableRateLimiting("forgot-password")]
public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model) { ... }
```

**Middleware ordering note (verified against this app's actual `Program.cs`):** `UseRateLimiter()` must be called after `UseRouting()` for endpoint-specific (`[EnableRateLimiting]`) policies to resolve correctly. This app already calls `app.UseRouting()` at line 172, well before the route-mapping calls. Insert `app.UseRateLimiter()` anywhere after line 172 and before the app finishes configuring the pipeline (e.g., alongside the existing `UseSession()`/`UseAuthentication()`/`UseAuthorization()` block at lines 174-177) — exact position relative to auth/session middleware does not matter for rate limiting to function, only that it's after `UseRouting()`.

### Anti-Patterns to Avoid

- **Generating separate tokens for Welcome vs. Forgot Password with different purposes:** Both must call `GeneratePasswordResetTokenAsync` (purpose `"ResetPassword"`) so the single `SetPassword` action can verify either with `ResetPasswordAsync`. Do not use `GenerateEmailConfirmationTokenAsync` for the Welcome link — its purpose string (`"ConfirmEmail"`) is incompatible with `ResetPasswordAsync`'s verification (`"ResetPassword"`), and would fail token validation.
- **Global rate limiter for this use case:** D-12 wants a scoped, single-action limiter (3/15min), not an app-wide `GlobalLimiter`. Using `options.GlobalLimiter` would also throttle unrelated endpoints unless carefully partitioned by path, adding needless complexity.
- **Casting to `IUserEmailStore` to set `EmailConfirmed`:** Unnecessary indirection in this codebase — `UserEntity : IdentityUser<int>` already exposes `EmailConfirmed` as a public settable property. Direct property write + `UpdateAsync` is simpler and matches existing codebase conventions (e.g., `ChangeEmailAsync`'s `SetUserNameAsync` call is the only store-adjacent call already in use, and even that goes through `UserManager`, not raw store casting).

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|--------------|-----|
| Password-reset token generation/expiry | Custom GUID + DB expiry column | `UserManager.GeneratePasswordResetTokenAsync` / `ResetPasswordAsync` (already used by `AdminResetPasswordAsync`) | Handles cryptographic signing via ASP.NET Core Data Protection, security-stamp invalidation (so a token becomes invalid if the password already changed), and purpose-string separation automatically |
| Rate limiting | Custom `IMemoryCache`-based counter | `Microsoft.AspNetCore.RateLimiting` fixed-window limiter | Ships in the framework, handles partitioning, queueing, and rejection responses correctly out of the box; a hand-rolled cache counter would need its own thread-safety and window-reset logic |
| Enumeration-safe response | Custom timing-attack mitigation (e.g., artificial delay) | Simple "always return the same message, only conditionally send email" (D-11's approach) | Simpler and sufficient for this app's threat model — no need for constant-time response padding given this isn't a high-value target requiring defense against timing side-channels |

**Key insight:** ASP.NET Core Identity's token infrastructure already solves "generate a time-limited, tamper-proof, single-purpose token tied to a user and a security stamp" — the entire feature request in this phase (Welcome link + Forgot Password link) is just two different *triggers* for the exact same underlying primitive that's already wired into this codebase via `AdminResetPasswordAsync`.

## Common Pitfalls

### Pitfall 1: Assuming a "stripped-down auth layout" already exists for Login

**What goes wrong:** CONTEXT.md's D-08 and code_context both assert that `Login.cshtml` "uses a distinct layout from the main app chrome (no nav)" and that `ForgotPassword.cshtml`/`SetPassword.cshtml` should "use the same one."
**Why it happens:** This claim does not match the actual codebase. `QuestBoard.Service/Views/_ViewStart.cshtml` sets `Layout = "_Layout"` (or `_Layout.Mobile` for mobile) for **every** view under `Views/`, including `Views/Account/Login.cshtml` — there is no per-controller or per-view override in any Account view. `_Layout.cshtml` always renders the full navbar; it only conditionally hides nav *menu items* based on `User.Identity.IsAuthenticated`, not the navbar itself.
**How to avoid:** The planner must explicitly decide: (a) reuse `_Layout.cshtml` as-is for `ForgotPassword`/`SetPassword` (zero new layout work, matches what `Login.cshtml` actually does today), or (b) create a genuinely new minimal auth layout and retrofit `Login.cshtml` to use it too (larger scope than this phase implies). `_Layout.GroupPicker.cshtml` is NOT a drop-in fit — it assumes an authenticated user (renders a Logout button) and would need modification for anonymous pages.
**Warning signs:** If a plan says "Login.cshtml already has AuthLayout, just reuse it" — that premise is false; verify against `_ViewStart.cshtml` before writing tasks.

### Pitfall 2: Using the wrong token-generation method for the Welcome link

**What goes wrong:** Since the old flow used `GenerateEmailConfirmationAsync` (email-confirmation purpose), it's tempting to keep using it for the Welcome email and separately set the password. But `ResetPasswordAsync` (which `SetPassword` must call to actually set the password) only accepts tokens generated with `Options.Tokens.PasswordResetTokenProvider` and purpose `"ResetPassword"` — an email-confirmation token will fail `VerifyUserTokenAsync` inside `ResetPasswordAsync` with `IdentityResult.Failed(ErrorDescriber.InvalidToken())`.
**Why it happens:** The two purposes look interchangeable superficially (both are "click a link to prove you own this email"), but Identity's token verification is purpose-string-scoped by design, and mixing purposes silently fails token validation at consumption time, not generation time — the bug wouldn't surface until a real user clicks the link.
**How to avoid:** `AdminController.CreateUser` (D-06) and `AccountController.ForgotPassword` (D-08) must BOTH call the same new `GeneratePasswordResetTokenAsync`-wrapping method on `IIdentityService`, never `GenerateEmailConfirmationAsync`.
**Warning signs:** A plan task that calls `identityService.GenerateEmailConfirmationAsync` anywhere in the CreateUser or ForgotPassword flow is a bug — that method should only remain (if at all) for hypothetical future use, since D-05 retires the `ConfirmEmail`/`ConfirmationEmailJob` flow that was its only consumer.

### Pitfall 3: `TokenLifespan` is not currently configured — this is a net-new config block, not an edit

**What goes wrong:** CONTEXT.md's D-13 landmine warns to "confirm in Program.cs before assuming it's isolated to password-reset tokens" — this research confirms there is currently **no** `DataProtectionTokenProviderOptions` configuration at all in `Program.cs` (verified by reading the full file). The Identity setup block (lines 41-59) configures `Password`, `User`, and `Lockout` options only, then calls `.AddDefaultTokenProviders()` with no further token options configured — meaning `TokenLifespan` is currently at its **framework default of 1 day** [CITED: learn.microsoft.com DataProtectionTokenProviderOptions.TokenLifespan docs].
**Why it happens:** It's easy to assume "extend the existing config" when actually this phase must ADD a new `builder.Services.Configure<DataProtectionTokenProviderOptions>(o => o.TokenLifespan = TimeSpan.FromDays(7));` call — there's nothing to "extend."
**How to avoid:** Add the `Configure<DataProtectionTokenProviderOptions>` call as a new statement in `Program.cs`, positioned after `.AddDefaultTokenProviders()` (order doesn't strictly matter since it's a separate `Configure<T>` call, but grouping it near the Identity block aids readability).
**Warning signs:** A plan that says "modify the existing `TokenLifespan` value" is describing something that doesn't exist yet — it should say "add `TokenLifespan` configuration."

### Pitfall 4: No public `UserManager.SetEmailConfirmedAsync` — plan the primitive explicitly

**What goes wrong:** D-09 requires `SetPassword` to "mark `EmailConfirmed = true`" after a successful password reset. There is no `UserManager.SetEmailConfirmedAsync(user, true)` convenience method in ASP.NET Core Identity — only `IUserEmailStore<TUser>.SetEmailConfirmedAsync` exists on the store interface (protected access via `GetEmailStore()` inside `UserManager`, not directly callable from outside).
**Why it happens:** Assuming Identity has a symmetric "un-confirm/confirm without a token" helper because `ConfirmEmailAsync` (token-based) exists — it doesn't have a non-token equivalent as a public `UserManager` method.
**How to avoid:** Since `UserEntity : IdentityUser<int>` and `EmailConfirmed` is `public virtual bool { get; set; }` directly on `IdentityUser<TKey>` [VERIFIED: dotnet/aspnetcore source], the simplest correct approach is: fetch the entity via `userManager.FindByIdAsync`, set `entity.EmailConfirmed = true`, call `await userManager.UpdateAsync(entity)`. Add this as a new `IIdentityService` method (e.g., `ConfirmEmailDirectlyAsync(int userId)` or fold the flag-set into a combined `SetPasswordAsync(int userId, string token, string newPassword)` method that does both steps atomically).
**Warning signs:** A plan task that tries to call `identityService.ConfirmEmailAsync(userId, token)` using the password-reset token inside `SetPassword` — this will fail because `ConfirmEmailAsync` internally verifies against purpose `"ConfirmEmail"`, not `"ResetPassword"`; the token types are not interchangeable (same root cause as Pitfall 2, applied to the read side instead of the write side).

### Pitfall 5: `ChangePasswordViewModel`/`ResetPasswordViewModel` MinimumLength doesn't match Identity's `RequiredLength`

**What goes wrong:** Existing `ChangePasswordViewModel.NewPassword` and `AdminViewModels/ResetPasswordViewModel.NewPassword` both use `[StringLength(100, MinimumLength = 6)]`, but `Program.cs`'s Identity configuration sets `options.Password.RequiredLength = 8`. A new `SetPasswordViewModel` copied from these existing patterns would inherit the same mismatch — a user could pass client-side/DataAnnotation validation with a 6-7 character password, then fail server-side Identity validation with a confusing generic error.
**Why it happens:** Pre-existing inconsistency in the codebase (not introduced by this phase), but new code in this phase risks propagating it via copy-paste.
**How to avoid:** When creating `SetPasswordViewModel`, use `MinimumLength = 8` to match `Program.cs`'s actual `RequiredLength`, or better, don't duplicate the length constraint client-side at all and rely on the `IdentityResult.Errors` returned by `ResetPasswordAsync` (already the pattern in `AccountController.ChangePassword`'s error-handling `foreach` loop).
**Warning signs:** A `SetPasswordViewModel` with `MinimumLength = 6` — flag this in code review as it does not match the configured Identity policy, even though it's copying an existing (also wrong) pattern.

### Pitfall 6: `EmailPreviewController` needs updating for the new/deleted templates

**What goes wrong:** `QuestBoard.Service/Controllers/Admin/EmailPreviewController.cs` has one preview action per email template, including `ConfirmEmail()` (which renders the template D-05 deletes) — if left as-is after `ConfirmEmail.razor` is deleted, this preview action will fail to compile (missing type reference).
**Why it happens:** Not mentioned in CONTEXT.md's Key Files to Modify list — it's an easy miss since it's a dev-only admin tool, not user-facing.
**How to avoid:** Add `EmailPreviewController.cs` to the plan's file-modification list: remove the `ConfirmEmail()` preview action (and its `Index()` menu link), add `Welcome()` and `ForgotPassword()` preview actions following the exact pattern of the existing `ConfirmEmail()`/`ChangeEmailConfirm()` actions.
**Warning signs:** `dotnet build` failing with a missing-type error referencing `Components.Emails.ConfirmEmail` after deleting the `.razor` file — check `EmailPreviewController.cs` first.

## Code Examples

### D-02 Verification: Passwordless sign-in fails gracefully (not an exception)

```csharp
// Source: dotnet/aspnetcore main branch, src/Identity/Extensions.Core/src/UserManager.cs
// VerifyPasswordAsync — the innermost check invoked by CheckPasswordAsync:
protected virtual async Task<PasswordVerificationResult> VerifyPasswordAsync(
    IUserPasswordStore<TUser> store, TUser user, string password)
{
    var hash = await store.GetPasswordHashAsync(user, CancellationToken).ConfigureAwait(false);
    if (hash == null)
    {
        return PasswordVerificationResult.Failed;   // <-- graceful, no throw, no hasher call
    }
    var result = PasswordHasher.VerifyHashedPassword(user, hash, password);
    return result;
}

// Source: dotnet/aspnetcore main branch, src/Identity/Core/src/SignInManager.cs
// PasswordSignInAsync(string userName, ...) — the overload IdentityService.PasswordSignInAsync
// in THIS codebase calls, passing `email` as `userName` (works because UserName == Email at creation):
public virtual async Task<SignInResult> PasswordSignInAsync(string userName, string password,
    bool isPersistent, bool lockoutOnFailure)
{
    var user = await UserManager.FindByNameAsync(userName);
    if (user == null)
        return SignInResult.Failed;
    return await PasswordSignInAsync(user, password, isPersistent, lockoutOnFailure);
    // internally: CheckPasswordSignInAsync -> UserManager.CheckPasswordAsync -> VerifyPasswordAsync
    // (shown above) -> returns false for null hash -> SignInResult.Failed, never throws
}
```

### Existing `AdminResetPasswordAsync` — the primitive to extract for the new shared method

```csharp
// Source: QuestBoard.Repository/IdentityService.cs (already in this codebase)
// This is the closest existing analog to the new method the planner must add —
// it already demonstrates GeneratePasswordResetTokenAsync + ResetPasswordAsync used together:
public async Task<IdentityResult> AdminResetPasswordAsync(ClaimsPrincipal adminUser, int targetUserId, string newPassword)
{
    var adminEntity = await userManager.GetUserAsync(adminUser);
    if (adminEntity == null || !await userManager.IsInRoleAsync(adminEntity, "Admin"))
        return IdentityResult.Failed(new IdentityError { Description = "Admin user not found or not authorized." });

    var entity = await userManager.FindByIdAsync(targetUserId.ToString());
    if (entity == null)
        return IdentityResult.Failed(new IdentityError { Description = "User not found." });

    var resetToken = await userManager.GeneratePasswordResetTokenAsync(entity);
    return await userManager.ResetPasswordAsync(entity, resetToken, newPassword);
}
// The NEW method the planner needs is simpler (no admin-role check, and it needs to
// EXPOSE the raw token for email-link construction rather than consuming it immediately):
public async Task<string?> GeneratePasswordResetTokenForUserAsync(int userId)
{
    var entity = await userManager.FindByIdAsync(userId.ToString());
    if (entity == null) return null;
    return await userManager.GeneratePasswordResetTokenAsync(entity);
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|---------------|--------|
| Admin sets a throwaway password at account creation | Passwordless creation + self-service Welcome link | This phase (v5.0, Phase 32) | Removes password field from `CreateUserViewModel`/`CreateUser.cshtml`; admin no longer sees/handles any password value |
| Separate "confirm email" step for new accounts | Combined "set password + confirm email" in one click | This phase | `ConfirmEmail.razor`/`ConfirmationEmailJob` retired; `Welcome.razor`/`WelcomeEmailJob` replace them |
| Third-party rate-limiting packages (pre-.NET 7) | Built-in `Microsoft.AspNetCore.RateLimiting` middleware | .NET 7+ (2022), confirmed current for .NET 10 (docs updated 2025-11-26) | No new package needed for D-12's rate limiting requirement |

**Deprecated/outdated:** None specific to this phase — the codebase is already on the current ASP.NET Core 10 APIs; no migration from an older Identity or rate-limiting pattern is needed.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|----------------|
| A1 | CONTEXT.md's premise that `Login.cshtml` uses a "stripped-down auth layout (no main nav)" is **incorrect** as written — verified `Login.cshtml` uses the standard `_Layout.cshtml` via `_ViewStart.cshtml`, with no per-view layout override anywhere in `Views/Account/`. This is not an `[ASSUMED]` claim from training data — it is a direct, verified codebase finding that contradicts CONTEXT.md. Flagged here because it changes what "using the same layout as Login" means for `ForgotPassword.cshtml`/`SetPassword.cshtml`: the planner must pick between (a) full `_Layout.cshtml` (matches literal current Login.cshtml behavior) or (b) a new minimal layout (matches CONTEXT.md's apparent intent). | User Constraints D-08, Summary, Pitfall 1 | If the planner assumes a dedicated auth layout exists and references it, tasks will fail immediately (file not found) or silently produce a full-nav ForgotPassword/SetPassword page inconsistent with the design intent behind D-08's wording |
| A2 | The proposed `IIdentityService` method names (`GeneratePasswordResetTokenForUserAsync`, `ConfirmEmailDirectlyAsync`) are illustrative suggestions, not locked — CONTEXT.md's "Claude's Discretion" explicitly leaves exact signatures to the planner | Code Examples, Pitfall 4 | Low risk — naming only, no functional ambiguity; planner should still ensure the underlying token-purpose and property-write mechanics match what's verified here |
| A3 | Proposed requirement IDs (PWFLOW-01 through PWFLOW-06) in the Phase Requirements section are suggestions for the planner to add to `REQUIREMENTS.md` — not pre-existing, not locked by any prior decision | Phase Requirements | Low risk — IDs are a bookkeeping convenience; the underlying decisions (D-01 through D-14) are what's actually locked |

**A1 requires user/planner confirmation before writing `ForgotPassword.cshtml`/`SetPassword.cshtml` tasks** — this is the one item in this research that should be explicitly resolved (either by the planner making a call and stating it, or by a quick check back with the user) rather than silently assumed either way.

## Open Questions (RESOLVED)

1. **Should `ForgotPassword`/`SetPassword` use the full `_Layout.cshtml` (matching what `Login.cshtml` actually does) or a new minimal layout?**
   - **RESOLVED (32-03-PLAN.md):** Reuse the existing `_Layout.cshtml` — no new minimal auth layout is built in this phase. This matches what `Login.cshtml` actually does today (Assumption A1) and adds zero new layout work. A dedicated stripped-down auth layout is deferred as a separate future scope item if the D-08 "no main nav" intent is later confirmed with the user.
   - What we know: No dedicated stripped-down auth layout currently exists; `_Layout.GroupPicker.cshtml` is the closest analog but assumes an authenticated user.
   - What's unclear: Whether CONTEXT.md's D-08 wording ("no main nav") reflects a genuine design intent that just hasn't been built yet, or a misremembering of what Login.cshtml already does.
   - Recommendation: Default to reusing `_Layout.cshtml` (zero new layout work, consistent with current `Login.cshtml` behavior) unless the user explicitly wants a new minimal layout built as part of this phase — that would be a larger, separate scope addition worth calling out plainly in the plan.

2. **Exact placement/naming of the new `IIdentityService` methods for token-issuance-without-immediate-consumption and direct email-confirmation.**
   - **RESOLVED (32-01-PLAN.md):** Two granular methods were chosen (not a single combined method): `GeneratePasswordResetTokenForUserAsync(int userId)` (raw ResetPassword-purpose token by userId) and `ConfirmEmailDirectlyAsync(int userId)` (direct `EmailConfirmed = true` property write + `UpdateAsync`). The controller (Plan 03 SetPassword) calls `ResetPasswordAsync` then `ConfirmEmailDirectlyAsync` in sequence. Both underlying Identity primitives are the verified-safe ones from the Code Examples / Pitfall 4 sections.
   - What we know: The underlying Identity primitives (`GeneratePasswordResetTokenAsync`, direct `EmailConfirmed` property write) are verified and safe.
   - What's unclear: Whether to add two granular methods or one combined method (e.g., a single `SetPasswordAsync(userId, token, newPassword)` on `IIdentityService` that both resets the password AND sets `EmailConfirmed = true`, versus keeping `ResetPasswordAsync` untouched and adding a separate `ConfirmEmailDirectlyAsync` called right after from the controller).
   - Recommendation: Planner's discretion per CONTEXT.md — a combined method reduces controller-side sequencing risk (can't forget to call the second step) and is recommended, but either approach is functionally sound given the verified primitives.

## Environment Availability

Skipped — this phase has no external service/tool dependencies beyond what's already running in this Windows dev environment (SQL Server, .NET 10 SDK, both confirmed present: `dotnet --version` → `10.0.301`). No new packages, no new external services, no new CLI tools.

## Validation Architecture

### Test Framework

| Property | Value |
|----------|-------|
| Framework | xUnit v3, with `Microsoft.AspNetCore.Mvc.Testing` (`WebApplicationFactory`) for integration tests, `NSubstitute` + `FluentAssertions` for unit tests |
| Config file | No dedicated xunit config found beyond project `.csproj` settings; `GlobalUsings.cs` per project |
| Quick run command | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~IdentityService\|FullyQualifiedName~WelcomeEmailJob\|FullyQualifiedName~ForgotPasswordEmailJob"` |
| Full suite command | `dotnet test` (currently 55 unit + 181 integration tests green per Phase 31 SUMMARY) |

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|--------------------|--------------|
| PWFLOW-01 | Passwordless `CreateUserAsync` leaves `PasswordHash` null; no password validators run | unit (IdentityService) | `dotnet test --filter FullyQualifiedName~IdentityServiceTests` | ❌ Wave 0 — no `IdentityServiceTests.cs` file currently exists in `QuestBoard.UnitTests`; needs creation, or cover via integration test against real `UserManager` |
| PWFLOW-02 | `SetPassword` sets both `PasswordHash` and `EmailConfirmed = true` in one action | integration (AccountController) | `dotnet test --filter FullyQualifiedName~AccountControllerIntegrationTests` | ❌ Wave 0 — `SetPassword` action doesn't exist yet; add test cases to existing `AccountControllerIntegrationTests.cs` |
| PWFLOW-03 | Passwordless account cannot sign in (`PasswordSignInAsync` returns `Failed`, no exception) | integration (AccountController.Login against a passwordless-seeded user) | `dotnet test --filter FullyQualifiedName~AccountControllerIntegrationTests` | ❌ Wave 0 — needs a new test seeding a user via the no-password path then attempting login |
| PWFLOW-04 | `ForgotPassword` POST is enumeration-safe and rate-limited (3/15min per IP) | integration (AccountController) | `dotnet test --filter FullyQualifiedName~AccountControllerIntegrationTests` | ❌ Wave 0 — new action + new tests; rate-limit test needs 4 rapid requests from the same simulated IP to assert the 4th returns 429 |
| PWFLOW-05 | "Resend welcome email" button visible only when `EmailConfirmed == false`; enqueues `WelcomeEmailJob` | integration (AdminController) | `dotnet test --filter FullyQualifiedName~AdminControllerIntegrationTests` | ✅ existing `SendConfirmationEmail` tests in `AdminControllerIntegrationTests.cs` can be adapted (verify file exists — confirmed at `QuestBoard.IntegrationTests/Controllers/AdminControllerIntegrationTests.cs`) |
| PWFLOW-06 | `TokenLifespan` configured to 7 days | manual/unit (Program.cs config, or a smoke test asserting `IOptions<DataProtectionTokenProviderOptions>.Value.TokenLifespan == TimeSpan.FromDays(7)`) | `dotnet test --filter FullyQualifiedName~ProgramConfigurationTests` (if such a test class is created) | ❌ Wave 0 — no existing test asserts token lifespan; low priority, could be a simple options-resolution unit test in the WebApplicationFactory-based test host |

### Sampling Rate

- **Per task commit:** `dotnet test QuestBoard.UnitTests` (fast, seconds) plus targeted integration test filter for the controller under change
- **Per wave merge:** `dotnet test` (full suite — currently 55 unit + 181 integration tests, expect growth from new SetPassword/ForgotPassword/WelcomeEmailJob/ForgotPasswordEmailJob coverage)
- **Phase gate:** Full suite green before `/gsd:verify-work`

### Wave 0 Gaps

- [ ] `QuestBoard.UnitTests/Services/WelcomeEmailJobTests.cs` — mirror `ConfirmationEmailJobTests.cs` exactly (same scope-factory mocking pattern), covering REQ PWFLOW-02
- [ ] `QuestBoard.UnitTests/Services/ForgotPasswordEmailJobTests.cs` — same pattern, covering PWFLOW-04
- [ ] New test cases in `QuestBoard.IntegrationTests/Controllers/AccountControllerIntegrationTests.cs` for `ForgotPassword` GET/POST and `SetPassword` GET/POST, covering PWFLOW-02/03/04
- [ ] Adapt existing `SendConfirmationEmail`-related tests in `AdminControllerIntegrationTests.cs` (if present) to the renamed/repurposed `WelcomeEmailJob` path, covering PWFLOW-05
- [ ] Delete `QuestBoard.UnitTests/Services/ConfirmationEmailJobTests.cs` per CONTEXT.md's Files to Delete list — replace with `WelcomeEmailJobTests.cs`
- [ ] No dedicated rate-limiter test framework in place — a targeted integration test issuing 4 rapid POSTs to `/Account/ForgotPassword` from the test client and asserting the 4th gets HTTP 429 is the recommended approach (uses the same `WebApplicationFactoryBase`/`CreateNonRedirectingClient()` pattern already in `AccountControllerIntegrationTests.cs`)

## Security Domain

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|----------------|---------|-------------------|
| V2 Authentication | yes | ASP.NET Core Identity `UserManager`/`SignInManager` — password hashing (PBKDF2 via default `PasswordHasher<TUser>`), lockout (`MaxFailedAccessAttempts = 5`, already configured) |
| V3 Session Management | yes (pre-existing, not modified by this phase) | ASP.NET Core cookie authentication, already configured via `AddIdentity` |
| V4 Access Control | yes | `[Authorize]`/policy-based authorization on `AdminController` (`AdminOnly`); `ForgotPassword`/`SetPassword`/Welcome-link consumption are intentionally anonymous (`[AllowAnonymous]` implicit — no `[Authorize]` on `AccountController` class level) |
| V5 Input Validation | yes | DataAnnotations on new `ForgotPasswordViewModel`/`SetPasswordViewModel` (`[Required]`, `[EmailAddress]`, `[DataType(DataType.Password)]`, `[Compare]`) — match existing `ChangePasswordViewModel`/`ResetPasswordViewModel` pattern (see Pitfall 5 for the length-mismatch caveat) |
| V6 Cryptography | yes — never hand-roll | ASP.NET Core Identity's `DataProtectorTokenProvider` (AES-based via Data Protection API) for password-reset tokens; `PasswordHasher<TUser>` (PBKDF2/Argon2-class) for password hashing — both already in place, this phase only adjusts `TokenLifespan`, not the crypto primitives themselves |

### Known Threat Patterns for ASP.NET Core Identity + Rate Limiting

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|-----------------------|
| Email enumeration via ForgotPassword response differences | Information Disclosure | D-11's generic response ("If that email is registered...") regardless of match — already the locked design |
| Brute-force / spam of ForgotPassword to flood a victim's inbox | Denial of Service (against the victim's inbox, and resource exhaustion against the email-sending service) | D-12's rate limiting (3 req/15min per IP) — mitigates but does not eliminate (a distributed attacker with many IPs could still flood); acceptable per this app's threat model (internal D&D group tool, not a public high-value target) |
| IP-based rate-limit partition bypassed by IP spoofing / reverse proxy | Denial of Service (rate-limit bypass) | Documented risk in Microsoft's own guidance: "Creating partitions on client IP addresses makes the app vulnerable to Denial of Service Attacks that employ IP Source Address Spoofing" [CITED: learn.microsoft.com rate-limit.md]. This app is deployed behind a reverse proxy per `CLAUDE.md`/deployment notes — `httpContext.Connection.RemoteIpAddress` may reflect the proxy's IP rather than the real client unless `UseForwardedHeaders` is configured. **Not currently verified whether `ForwardedHeadersMiddleware` is configured in this app's `Program.cs`** — if absent, all requests through the reverse proxy would share one partition key (the proxy's IP), making the rate limit either too strict (one shared bucket for all real users) or ineffective depending on deployment topology. Flagged as an item the planner should verify/address, not a hard blocker for a 3-req/15-min internal-tool policy. |
| Stale/long-lived password-reset tokens increasing exposure window (D-13's 7-day TokenLifespan) | Elevation of Privilege (if a token is intercepted/leaked) | Identity's token includes the user's security stamp — if the password or email changes in the meantime, the stamp changes and invalidates any outstanding token automatically. A 7-day window is longer than the ASP.NET Core default (1 day) but is an explicit, accepted tradeoff per D-13 given this is a low-traffic internal tool where users may not check email promptly. |

**Reverse-proxy IP note:** Verify whether `builder.Services.Configure<ForwardedHeadersOptions>` + `app.UseForwardedHeaders()` is already configured elsewhere in this app (not found in the `Program.cs` sections read during this research — grep for "ForwardedHeaders" during planning to confirm before relying on `RemoteIpAddress` for the rate-limit partition key).

## Sources

### Primary (HIGH confidence)

- `dotnet/aspnetcore` GitHub repository, `main` branch (tracks .NET 10 development) — `src/Identity/Extensions.Core/src/UserManager.cs`: `CheckPasswordAsync`, `CheckPasswordCoreAsync`, `VerifyPasswordAsync`, `CreateAsync(TUser user)`, `GeneratePasswordResetTokenAsync`, `ResetPasswordAsync`, `TokenOptions.cs` defaults
- `dotnet/aspnetcore` GitHub repository, `main` branch — `src/Identity/Core/src/SignInManager.cs`: `PasswordSignInAsync` (both overloads), `CheckPasswordSignInAsync`
- `dotnet/aspnetcore` GitHub repository — `src/Identity/Extensions.Stores/src/IdentityUser.cs`: `EmailConfirmed` property declaration (`public virtual bool { get; set; }`)
- `dotnet/aspnetcore` GitHub repository — `src/Identity/Extensions.Core/src/IUserEmailStore.cs`: `SetEmailConfirmedAsync` interface method
- Microsoft Learn, "Rate limiting middleware in ASP.NET Core," `aspnetcore-10.0` moniker, last updated 2025-11-26 — `https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit?view=aspnetcore-10.0` — `AddFixedWindowLimiter`, `[EnableRateLimiting]`, `PartitionedRateLimiter` by IP, `OnRejected`, middleware ordering (`UseRateLimiter()` after `UseRouting()`), and the explicit IP-spoofing caveat for IP-based partitioning
- This codebase, directly read: `QuestBoard.Service/Program.cs` (full Identity/pipeline config), `QuestBoard.Repository/IdentityService.cs`, `QuestBoard.Domain/Interfaces/IIdentityService.cs`, `QuestBoard.Domain/Services/UserService.cs`, `QuestBoard.Domain/Interfaces/IUserService.cs`, `QuestBoard.Service/Controllers/Admin/AccountController.cs`, `QuestBoard.Service/Controllers/Admin/AdminController.cs`, `QuestBoard.Service/Views/_ViewStart.cshtml`, `QuestBoard.Service/Views/Account/Login.cshtml` + `.Mobile.cshtml`, `QuestBoard.Service/Views/Shared/_Layout.cshtml` + `_Layout.GroupPicker.cshtml`, `QuestBoard.Service/Jobs/ConfirmationEmailJob.cs` + `ChangeEmailConfirmationJob.cs`, `QuestBoard.Service/Components/Emails/ConfirmEmail.razor`, `QuestBoard.Service/Controllers/Admin/EmailPreviewController.cs`, `QuestBoard.Repository/Entities/UserEntity.cs`, `QuestBoard.Service/ViewModels/*/CreateUserViewModel.cs`/`ChangePasswordViewModel.cs`/`ResetPasswordViewModel.cs`, `QuestBoard.UnitTests/Services/ConfirmationEmailJobTests.cs`, `QuestBoard.IntegrationTests/Controllers/AccountControllerIntegrationTests.cs`, `QuestBoard.Service/QuestBoard.Service.csproj` (confirmed `Microsoft.AspNetCore.Identity.UI` 10.0.9 reference), `.planning/codebase/CONVENTIONS.md`

### Secondary (MEDIUM confidence)

- WebSearch results cross-referencing `TokenOptions` default provider assignments (`PasswordResetTokenProvider`, `EmailConfirmationTokenProvider`, `ChangeEmailTokenProvider` all default to `DefaultProvider`) — corroborated by direct source read of `TokenOptions.cs`
- WebSearch results on `AddDefaultTokenProviders` registering `DataProtectorTokenProvider` under `TokenOptions.DefaultProvider`, `EmailTokenProvider`/`PhoneNumberTokenProvider`/`AuthenticatorTokenProvider` under their respective default names — consistent with the `TokenOptions.cs` defaults found directly

### Tertiary (LOW confidence)

- None — every claim material to a planning decision was cross-verified against direct source reads or current official Microsoft documentation.

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — no new packages; both APIs used (`Microsoft.AspNetCore.RateLimiting`, extended `UserManager`/`SignInManager` methods) are part of the already-referenced ASP.NET Core 10 shared framework, confirmed via `dotnet --version` (10.0.301) and `QuestBoard.Service.csproj`
- Architecture: HIGH — all patterns (Hangfire job scoping, token generation/consumption, enumeration-safe response) either directly mirror existing codebase code (`AdminResetPasswordAsync`, `ConfirmationEmailJob`) or are drawn from current official Microsoft documentation
- Pitfalls: HIGH — all six pitfalls are grounded in either direct source verification (D-02, D-13's token provider scoping, `EmailConfirmed` property access) or direct codebase reads (Pitfall 1's layout discrepancy, Pitfall 5's length mismatch, Pitfall 6's `EmailPreviewController` gap) — none are speculative

**Research date:** 2026-07-01
**Valid until:** 2026-07-31 (30 days — ASP.NET Core 10 Identity APIs are stable/LTS-track; the one fast-moving element, `Microsoft.AspNetCore.RateLimiting` docs, was last updated 2025-11-26 and is unlikely to change materially within 30 days)
