# Phase 32: First-Login Password Flow - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-07-01
**Phase:** 32-first-login-password-flow
**Areas discussed:** Password creation mechanism, Merge confirm-email + set-password, Scope: welcome-only vs. also forgot-password, Token expiry + resend

---

## Password Creation Mechanism

| Option | Description | Selected |
|--------|-------------|----------|
| Random throwaway password | `CreateUserAsync` keeps its signature, fed a generated value never shown/used | |
| Identity's passwordless `CreateAsync(user)` | No `PasswordHash` at all until the reset-token flow sets one; changes `IIdentityService.CreateUserAsync` contract | ✓ |

**User's choice:** Identity's passwordless `CreateAsync(user)` overload.
**Notes:** Cleaner semantically — no dummy secret ever exists on the account record. Flagged as a landmine to verify `PasswordSignInAsync`/`CheckPasswordAsync` degrade gracefully against a null `PasswordHash`.

---

## Merge confirm-email + set-password

| Option | Description | Selected |
|--------|-------------|----------|
| One combined link | Single "Set your password" email; clicking + setting password also confirms email | ✓ |
| Two separate steps | Keep existing confirm-email link independent from a new set-password link | |

**User's choice:** One combined link — replaces the `ConfirmationEmailJob`/`ConfirmEmail.razor` flow for new users entirely.

**Follow-up — "Send Confirmation Email" button repurposing:**

| Option | Description | Selected |
|--------|-------------|----------|
| Repurpose existing button to send combined welcome email | Same button, same `EmailConfirmed==false` condition, new job underneath | ✓ |
| Keep both flows side by side | Separate buttons for passwordless vs. has-password-but-unconfirmed accounts | |

**User's choice (via free text):** Raised a concern that `ConfirmEmail.razor` might be shared with the email-change flow, and asked for a dedicated new Razor template ("welcome.razor") rather than reusing/renaming the existing one. Claude verified via reference search: `ConfirmEmail.razor`/`ConfirmationEmailJob` are used ONLY by `AdminController.CreateUser` and `SendConfirmationEmail` — the email-change flow already uses a separate, untouched template (`ChangeEmailConfirm.razor`/`ChangeEmailConfirmationJob`). Recommendation: new dedicated `Welcome.razor` + `WelcomeEmailJob`, retire the old confirm-only pair. User confirmed this approach.

**Follow-up — "password changed" notification email (new idea raised by user):**

| Option | Description | Selected |
|--------|-------------|----------|
| Out of scope — defer | Neither ChangePassword nor AdminController.ResetPassword send email today; separate future idea | ✓ |
| Add it now | New `PasswordChanged.razor` + job for both flows | |

**User's choice:** Deferred, once Welcome.razor was agreed as the recommended path forward.

---

## Scope: welcome-only vs. also forgot-password

| Option | Description | Selected |
|--------|-------------|----------|
| Welcome-only | Matches ROADMAP title exactly; existing users still go through admin-triggered `ResetPassword` | |
| Also add self-service forgot-password | Reuses the same token plumbing; expands scope beyond the roadmap title | ✓ |

**User's choice:** Also add self-service forgot-password. Explicit, deliberate scope expansion (not scope creep) — same underlying token mechanism being built anyway.

**Follow-up — email enumeration:**

| Option | Description | Selected |
|--------|-------------|----------|
| Generic message always | "If that email is registered..." regardless of match | ✓ |
| Explicit error if not found | Friendlier for small trusted group, weaker security practice | |

**User's choice:** Generic message always.

**Follow-up — rate limiting:**

| Option | Description | Selected |
|--------|-------------|----------|
| No rate limiting | Matches current low-risk posture, 17-member group | |
| Add basic rate limiting | Prevent inbox-bombing via repeated reset requests | ✓ |

**User's choice:** Add basic rate limiting.

**Follow-up — rate limit mechanism:**

| Option | Description | Selected |
|--------|-------------|----------|
| ASP.NET Core built-in rate limiter, 3/15min per IP | Framework-native, no new package | ✓ |
| Simple IMemoryCache throttle, 3/15min per email | Keyed by target email instead of IP | |

**User's choice:** ASP.NET Core built-in rate limiter, 3 requests / 15 minutes per client IP.

**Follow-up — view/flow design (user asked Claude to explain, via free text):**
Claude proposed: dedicated `ForgotPassword.cshtml` (email-only, auth layout, link added to Login page) + a shared `SetPassword.cshtml` landing page consumed by both the welcome-email link and the forgot-password-email link (same underlying mechanics, different email templates). User confirmed this matched their intent.

---

## Token Expiry + Resend

| Option | Description | Selected |
|--------|-------------|----------|
| Extend to 7 days | `DataProtectionTokenProviderOptions.TokenLifespan = 7 days` | ✓ |
| Keep default (1 day) | No config change; rely on resend button if expired | |

**User's choice:** Extend to 7 days.

**Follow-up — resend button:**

| Option | Description | Selected |
|--------|-------------|----------|
| Yes, add resend button | Shown for `EmailConfirmed==false` accounts, mirrors existing row-action pattern | ✓ |
| No resend button | Admin re-runs CreateUser or resets another way | |

**User's choice:** Yes, add a resend button (same button as the repurposed "Send Confirmation Email" action from the Merge area — becomes "Resend welcome email").

---

## Claude's Discretion

- Exact route/action naming beyond what's specified (follows `ConfirmEmail`/`ConfirmEmailChange` convention).
- Whether `IIdentityService.CreateUserAsync` changes signature in place vs. a new method is added.
- Whether the rate-limiting policy is a named policy or inline partitioner in `Program.cs`.
- Exact copy/wording and layout of `Welcome.razor` and `ForgotPassword.razor` (follow established Cinzel/wax-seal style).
- Whether the forgot-password confirmation renders inline or via redirect to a confirmation view.

## Deferred Ideas

- **"Password changed" notification email** for `ChangePassword`/`AdminController.ResetPassword` — explicitly deferred to a future phase, once `Welcome.razor` establishes the pattern.
