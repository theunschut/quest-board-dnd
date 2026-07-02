# Phase 32: First-Login Password Flow - Context

**Gathered:** 2026-07-01
**Status:** Ready for planning

<domain>
## Phase Boundary

Admin-created users no longer receive an admin-chosen password. `AdminController.CreateUser` creates the account with no password at all; the new user gets a single "Welcome — set your password" email that both sets their password and confirms their email in one click. This phase ALSO adds a self-service "Forgot password?" flow for existing users (user-directed scope expansion beyond the original ROADMAP title — same token plumbing, reused end-to-end), replacing the current situation where a forgotten password requires an admin to manually reset it via `AdminController.ResetPassword`.

This phase does NOT include: a "password changed" notification email for `AccountController.ChangePassword` (self-service) or `AdminController.ResetPassword` (admin-triggered) — explicitly deferred. It does not touch the email-address-change flow (`ChangeEmailConfirm.razor` / `ChangeEmailConfirmationJob`), which is a separate, already-isolated flow.

</domain>

<decisions>
## Implementation Decisions

### Password Creation Mechanism

- **D-01:** Admin-created accounts are created via ASP.NET Identity's passwordless `UserManager.CreateAsync(user)` overload (no `PasswordHash` set at all) — not a generated throwaway password. `IIdentityService.CreateUserAsync` (or a new method) must stop requiring a `password` argument for this path. `CreateUserViewModel.Password` field is removed entirely; `Views/Admin/CreateUser.cshtml` and `CreateUser.Mobile.cshtml` lose the password input.
- **D-02 (verify at planning/research time):** Confirm `UserManager.CheckPasswordAsync` / `SignInManager.PasswordSignInAsync` correctly reject sign-in attempts against a passwordless account (no `PasswordHash`) rather than erroring — this is standard Identity behavior but must be verified against the actual ASP.NET Core 10 Identity implementation before relying on it.

### Welcome Email (replaces email-confirmation-only flow for new users)

- **D-03:** One combined link does both jobs: clicking a "Set your password" link and successfully submitting a new password sets `PasswordHash` AND marks `EmailConfirmed = true` in the same action. No separate "confirm email" step for admin-created users.
- **D-04:** New dedicated `Welcome.razor` email template (in `QuestBoard.Service/Components/Emails/`) + new `WelcomeEmailJob` (in `QuestBoard.Service/Jobs/`), following the existing Cinzel/wax-seal visual style (see `ConfirmEmail.razor` for the pattern to follow, then retire it).
- **D-05:** The old `ConfirmEmail.razor` + `ConfirmationEmailJob` are retired (deleted) — confirmed they have no other callers. The separate email-address-change flow (`ChangeEmailConfirm.razor` + `ChangeEmailConfirmationJob`, triggered from `AccountController.Edit` / `AdminController.EditUser`) is untouched and must not be confused with the retired confirm-email-on-creation flow.
- **D-06:** `AdminController.CreateUser` no longer calls `identityService.GenerateEmailConfirmationAsync` + enqueues `ConfirmationEmailJob`. Instead it generates a password-reset token (see D-11) and enqueues `WelcomeEmailJob` with a link to the shared `SetPassword` action (see D-09).
- **D-07:** The existing "Send Confirmation Email" button on `Views/Admin/Users.cshtml` (shown when `EmailConfirmed == false`) is repurposed to send the new combined Welcome email instead of the old confirm-only one — same trigger condition, same button, new job underneath. Effectively becomes the "Resend welcome email" action (see D-14).
- **Deferred (explicitly out of scope):** A "password changed" notification email for `AccountController.ChangePassword` or `AdminController.ResetPassword` — neither sends an email today; adding one is a separate future idea, not part of this phase.

### Self-Service Forgot Password (scope expansion, user-directed)

- **D-08:** New `GET/POST /Account/ForgotPassword` action pair in `AccountController` — a dedicated view with just an email field ("Send reset link" button), using the same stripped-down auth layout as `Login.cshtml` (no main nav). A "Forgot password?" link is added to `Login.cshtml` and `Login.Mobile.cshtml` pointing to this new action.
- **D-09:** A shared `GET/POST /Account/SetPassword?userId=X&token=Y` action pair is the landing page for BOTH the welcome-email link and the forgot-password-email link. Same mechanics for both: decode token, call `identityService.ResetPasswordAsync(userId, token, newPassword)` (already exists on `IIdentityService`/`IUserService` — no interface change needed here), on success mark `EmailConfirmed = true` and redirect to Login with a success TempData message.
- **D-10:** New `ForgotPassword.razor` email template (separate from `Welcome.razor`, same visual style) sent by a new job when a user requests a reset via `/Account/ForgotPassword`.
- **D-11:** `ForgotPassword` POST always shows the same generic response regardless of whether the submitted email matches an account: *"If that email is registered, a reset link has been sent."* No email is sent for non-existent accounts, but the UI gives no indication either way — prevents email enumeration.
- **D-12:** Basic rate limiting on the `ForgotPassword` POST action using ASP.NET Core's built-in `Microsoft.AspNetCore.RateLimiting` middleware (ships with .NET, no new package) — fixed-window policy, 3 requests per 15 minutes per client IP, applied via `[EnableRateLimiting("...")]` on the action or a scoped policy in `Program.cs`.

### Token Expiry & Resend

- **D-13:** Extend `DataProtectionTokenProviderOptions.TokenLifespan` (configured in `Program.cs` alongside the existing Identity setup) to **7 days**. This is the shared "Default" token provider used by password-reset, email-confirmation, and change-email tokens alike — extending it uniformly is acceptable since none of those flows currently need a shorter window.
- **D-14:** Admin gets a "Resend welcome email" button on `Views/Admin/Users.cshtml`, shown for any account with `EmailConfirmed == false` (i.e., hasn't completed first login yet) — same row-action + TempData feedback pattern as the existing Promote/Demote/ResetPassword buttons (see D-07 — this is the same button, repurposed).

### Claude's Discretion

- Exact route/action naming beyond what's specified above (follows existing `ConfirmEmail`/`ConfirmEmailChange` naming convention in `AccountController`).
- Whether `IIdentityService.CreateUserAsync` changes signature in place or a new method is added alongside it — planner's call based on how many other callers exist.
- Whether the rate-limiting policy is a named policy in `Program.cs`'s `AddRateLimiter(...)` or an inline partitioner — implementation detail.
- Exact copy/wording and layout details of `Welcome.razor` and `ForgotPassword.razor` (follow the established Cinzel/wax-seal visual style from `ConfirmEmail.razor` / `ChangeEmailConfirm.razor`).
- Whether the forgot-password confirmation message renders inline on the same page or via a redirect to a small confirmation view (mirrors existing `ResendConfirmationSuccess`-style patterns if any exist, else planner's discretion).

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Requirements & Scope
- `.planning/REQUIREMENTS.md` — v5.0 requirements do not currently list a forgot-password requirement; the planner should add one to REQUIREMENTS.md reflecting the D-08–D-12 scope expansion agreed in this discussion.
- `.planning/ROADMAP.md` §Phase 32 — phase goal is currently "[To be planned]"; the planner must write a goal statement covering both the welcome flow AND the forgot-password expansion.

### Prior Phase Decisions (locked — do not re-litigate)
- `.planning/phases/30-group-ux-admin-user-creation/30-CONTEXT.md` — D-11 (original CreateUser form fields including Password — the Password field is now removed per D-01), D-12 (ConfirmationEmailJob reuse for email confirmation — superseded by D-04/D-05/D-06 in this phase)
- `.planning/milestones/v4.0-phases/24-email-confirmation-flow/24-CONTEXT.md` — D-01 (`EmailConfirmed` on `User` domain model, already wired via AutoMapper), D-02/D-03 (`ConfirmEmail` callback pattern in `AccountController` — the pattern to follow for the new `SetPassword` action), token URL-encoding pattern (`WebEncoders.Base64UrlEncode`/`Decode`)

### Key Files to Modify
- `QuestBoard.Domain/Interfaces/IIdentityService.cs` — `CreateUserAsync` signature change (D-01); may need a new `GeneratePasswordResetTokenAsync(int userId)` method (currently only `AdminResetPasswordAsync` generates a reset token internally, without exposing the raw token for email-link construction)
- `QuestBoard.Repository/IdentityService.cs` — implementation of the above; `ResetPasswordAsync(int userId, string token, string newPassword)` already exists and can be reused as-is for `SetPassword` (D-09)
- `QuestBoard.Domain/Services/UserService.cs` / `QuestBoard.Domain/Interfaces/IUserService.cs` — mirror any `IIdentityService` signature changes
- `QuestBoard.Service/Controllers/Admin/AccountController.cs` — add `ForgotPassword` GET/POST (D-08), `SetPassword` GET/POST (D-09); add "Forgot password?" link wiring
- `QuestBoard.Service/Controllers/Admin/AdminController.cs` — `CreateUser` POST changes (D-06); `SendConfirmationEmail` action repurposed to send Welcome email (D-07/D-14)
- `QuestBoard.Service/ViewModels/AdminViewModels/CreateUserViewModel.cs` — remove `Password` property (D-01)
- `QuestBoard.Service/Views/Admin/CreateUser.cshtml` + `CreateUser.Mobile.cshtml` — remove password field
- `QuestBoard.Service/Views/Admin/Users.cshtml` — repurpose "Send Confirmation Email" button copy/label (D-07/D-14)
- `QuestBoard.Service/Views/Account/Login.cshtml` + `Login.Mobile.cshtml` — add "Forgot password?" link (D-08)
- `QuestBoard.Service/Program.cs` — `DataProtectionTokenProviderOptions.TokenLifespan` config (D-13); `AddRateLimiter` policy registration (D-12)

### New Files to Create
- `QuestBoard.Service/Jobs/WelcomeEmailJob.cs`
- `QuestBoard.Service/Components/Emails/Welcome.razor`
- `QuestBoard.Service/Jobs/ForgotPasswordEmailJob.cs` (naming at planner's discretion)
- `QuestBoard.Service/Components/Emails/ForgotPassword.razor`
- `QuestBoard.Service/Views/Account/ForgotPassword.cshtml` + `ForgotPassword.Mobile.cshtml`
- `QuestBoard.Service/Views/Account/SetPassword.cshtml` + `SetPassword.Mobile.cshtml`
- `QuestBoard.Service/ViewModels/AccountViewModels/ForgotPasswordViewModel.cs` (email field only)
- `QuestBoard.Service/ViewModels/AccountViewModels/SetPasswordViewModel.cs` (userId, token, new password, confirm password)

### Files to Delete
- `QuestBoard.Service/Jobs/ConfirmationEmailJob.cs`
- `QuestBoard.Service/Components/Emails/ConfirmEmail.razor`
- Existing `ConfirmationEmailJobTests.cs` (in `QuestBoard.UnitTests/Services/`) — replace with tests for the new `WelcomeEmailJob`

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `IIdentityService.ResetPasswordAsync(int userId, string token, string newPassword)` (`QuestBoard.Repository/IdentityService.cs:90`) — already wraps `UserManager.ResetPasswordAsync(entity, token, newPassword)`; this is the exact primitive the shared `SetPassword` action needs. No change required to this method.
- `IUserService.ResetPasswordAsync(User user, string token, string newPassword)` (`QuestBoard.Domain/Services/UserService.cs:88`) — Domain-layer wrapper already exists; `SetPassword` controller action can call this directly via `IUserService`.
- `WebEncoders.Base64UrlEncode` / `Base64UrlDecode` pattern — already used for the `ConfirmEmail` and `ConfirmEmailChange` tokens in `AccountController.cs`; reuse identically for `SetPassword` and `ForgotPassword` tokens.
- `IEmailRenderService` + `IEmailService.SendAsync` — existing rendering/sending infrastructure (see `ConfirmationEmailJob.cs` for the exact pattern: `IServiceScopeFactory.CreateAsyncScope()`, `RenderAsync<T>(Dictionary<string,object?>)`, then `SendAsync`).
- `_EmailLayout.razor` — shared email chrome (Subject, PreviewText, Cinzel font, wax-seal styling) used by all four existing email templates; `Welcome.razor` and `ForgotPassword.razor` should use it the same way.
- `EmailSettings.AppUrl` — already injected into email templates for building asset URLs (e.g., wax seal images); reuse in new templates.

### Established Patterns
- `IServiceScopeFactory` + `CreateAsyncScope()` in every Hangfire job — scoped services (DbContext, IEmailService) cannot be constructor-injected into jobs (locked v5.0/v4.0 architectural decision, still applies to `WelcomeEmailJob`/`ForgotPasswordEmailJob`).
- Admin row-action pattern: POST action → `TempData["Success"/"Error"]` → `RedirectToAction(nameof(Users))` (see `PromoteToAdmin`, `ResetPassword`, `SendConfirmationEmail` in `AdminController.cs`).
- Anonymous callback action pattern: `AccountController.ConfirmEmail` / `ConfirmEmailChange` — decode token, call identity service, set TempData, `RedirectToAction(nameof(Login))`. `SetPassword` follows the same shape but renders a form first (GET) before accepting the new password (POST), since it needs user input (the new password) rather than just consuming the token directly.
- Stripped-down auth layout — `Login.cshtml` uses a distinct layout from the main app chrome (no nav); `ForgotPassword.cshtml` and `SetPassword.cshtml` should use the same one.

### Integration Points
- `AdminController.CreateUser` POST (line ~101–141) — replace the `userService.CreateAsync(email, name, password)` call and the `GenerateEmailConfirmationAsync` + `ConfirmationEmailJob` enqueue with the new passwordless creation + `WelcomeEmailJob` enqueue.
- `AdminController.SendConfirmationEmail` (line ~269–290) — retarget to `WelcomeEmailJob`.
- `Views/Admin/Users.cshtml` — button label/condition update (still keyed on `EmailConfirmed == false`).
- `Program.cs` Identity configuration block — add `TokenLifespan` override; add `AddRateLimiter(...)` registration and apply the policy to the `ForgotPassword` POST action.

### Known Landmines
- `ConfirmationEmailJob` and `ConfirmEmail.razor` are used ONLY by `AdminController.CreateUser` and `AdminController.SendConfirmationEmail` (confirmed via reference search) — safe to delete outright, no other callers.
- `ChangeEmailConfirm.razor` + `ChangeEmailConfirmationJob` are a SEPARATE, already-isolated flow for email-address changes (triggered from `AccountController.Edit` and `AdminController.EditUser`) — do not modify, do not confuse with the retired confirm-email-on-creation flow.
- `DataProtectionTokenProviderOptions.TokenLifespan` is shared across ALL "Default"-provider token purposes (password reset, email confirmation, change-email) — extending it to 7 days (D-13) affects all three uniformly; there is no evidence any of them need a shorter window today, but the planner should confirm this in `Program.cs` before assuming it's isolated to password-reset tokens.
- No `RequireConfirmedEmail`/`RequireConfirmedAccount` is configured on Identity — sign-in is not currently gated on `EmailConfirmed`. This phase does not change that; `EmailConfirmed` remains informational/display-only outside of this flow's own logic.
- Passwordless account creation (D-01) must be verified against `UserManager.CreateAsync(TUser user)` — the no-password overload skips password validators entirely (expected/desired), but confirm `PasswordSignInAsync` / `CheckPasswordAsync` degrade gracefully (return failure, not throw) when `PasswordHash` is null.
- `GroupRole` assignment (`SetGroupRoleAsync`) and the default `"Player"` Identity-role assignment inside `CreateUserAsync` are unaffected by this phase — they still happen at creation time, before the user has set a password.

</code_context>

<specifics>
## Specific Ideas

- Welcome email and Forgot-password email must be visually distinct templates (`Welcome.razor` vs `ForgotPassword.razor`), not a shared/parameterized template — the user was explicit about wanting separate Razor files per email purpose, one per distinct trigger (Welcome, Email Changed, and — deferred — Password Changed), even though the underlying landing page (`SetPassword`) is shared.
- Enumeration-safe generic response text for `ForgotPassword`: *"If that email is registered, a reset link has been sent."*
- Rate limit: 3 requests / 15 minutes per client IP on the `ForgotPassword` POST, using .NET's built-in `Microsoft.AspNetCore.RateLimiting` (no new package).

</specifics>

<deferred>
## Deferred Ideas

- **"Password changed" notification email** — for `AccountController.ChangePassword` (self-service) and/or `AdminController.ResetPassword` (admin-triggered). Neither currently sends any email; user explicitly deferred this to a future phase once `Welcome.razor` establishes the pattern.

None else — the forgot-password addition was a deliberate scope expansion by the user, not scope creep; it stayed within the discussion.

</deferred>

---

*Phase: 32-first-login-password-flow*
*Context gathered: 2026-07-01*
