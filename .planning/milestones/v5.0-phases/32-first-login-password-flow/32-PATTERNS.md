# Phase 32: First-Login Password Flow - Pattern Map

**Mapped:** 2026-07-01
**Files analyzed:** 21 (new + modified; deletions excluded from analog search)
**Analogs found:** 21 / 21

**Layout note (resolved):** `ForgotPassword.cshtml`/`SetPassword.cshtml` (+ `.Mobile.cshtml`) use the standard `_Layout.cshtml`/`_Layout.Mobile.cshtml` exactly as `Login.cshtml` does today — selected automatically via `Views/_ViewStart.cshtml`. No `Layout = "..."` override is needed in the new `.cshtml` files; do not reference `_Layout.GroupPicker.cshtml` (that one assumes an authenticated user and renders a Logout button — wrong fit).

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|---|---|---|---|---|
| `QuestBoard.Domain/Interfaces/IIdentityService.cs` (modify) | service (interface) | CRUD | itself — extend in place | exact |
| `QuestBoard.Repository/IdentityService.cs` (modify) | service | CRUD | `AdminResetPasswordAsync` method within itself | exact |
| `QuestBoard.Domain/Interfaces/IUserService.cs` (modify) | service (interface) | CRUD | itself — extend in place | exact |
| `QuestBoard.Domain/Services/UserService.cs` (modify) | service | CRUD | `ResetPasswordAsync` method within itself | exact |
| `QuestBoard.Service/Controllers/Admin/AccountController.cs` (modify — add ForgotPassword, SetPassword) | controller | request-response | `ConfirmEmail`/`ConfirmEmailChange` actions within itself | exact |
| `QuestBoard.Service/Controllers/Admin/AdminController.cs` (modify — CreateUser, SendConfirmationEmail) | controller | request-response | `SendConfirmationEmail`/`CreateUser` within itself | exact |
| `QuestBoard.Service/ViewModels/AdminViewModels/CreateUserViewModel.cs` (modify — remove Password) | model (view model) | transform | itself | exact |
| `QuestBoard.Service/Views/Admin/CreateUser.cshtml` + `.Mobile.cshtml` (modify — remove password field) | component (Razor view) | request-response | itself | exact |
| `QuestBoard.Service/Views/Admin/Users.cshtml` (modify — button relabel) | component (Razor view) | request-response | itself (row-action buttons) | exact |
| `QuestBoard.Service/Views/Account/Login.cshtml` + `.Mobile.cshtml` (modify — add link) | component (Razor view) | request-response | itself | exact |
| `QuestBoard.Service/Program.cs` (modify — TokenLifespan + RateLimiter) | config | event-driven/config | itself (Identity block, pipeline block) | exact |
| `QuestBoard.Service/Controllers/Admin/EmailPreviewController.cs` (modify — swap ConfirmEmail for Welcome/ForgotPassword previews) | controller | request-response | `ChangeEmailConfirm()`/`ConfirmEmail()` actions within itself | exact |
| `QuestBoard.Service/Jobs/WelcomeEmailJob.cs` (new) | service (background job) | event-driven | `QuestBoard.Service/Jobs/ConfirmationEmailJob.cs` | exact |
| `QuestBoard.Service/Jobs/ForgotPasswordEmailJob.cs` (new) | service (background job) | event-driven | `QuestBoard.Service/Jobs/ConfirmationEmailJob.cs` | exact |
| `QuestBoard.Service/Components/Emails/Welcome.razor` (new) | component (email template) | transform | `QuestBoard.Service/Components/Emails/ConfirmEmail.razor` | exact |
| `QuestBoard.Service/Components/Emails/ForgotPassword.razor` (new) | component (email template) | transform | `QuestBoard.Service/Components/Emails/ChangeEmailConfirm.razor` | exact |
| `QuestBoard.Service/Views/Account/ForgotPassword.cshtml` + `.Mobile.cshtml` (new) | component (Razor view) | request-response | `QuestBoard.Service/Views/Account/Login.cshtml` + `.Mobile.cshtml` | exact |
| `QuestBoard.Service/Views/Account/SetPassword.cshtml` + `.Mobile.cshtml` (new) | component (Razor view) | request-response | `QuestBoard.Service/Views/Account/ChangePassword.cshtml` (form shape) + `Login.cshtml` (anonymous card layout) | role-match |
| `QuestBoard.Service/ViewModels/AccountViewModels/ForgotPasswordViewModel.cs` (new) | model (view model) | transform | `QuestBoard.Service/ViewModels/AccountViewModels/LoginViewModel.cs` (Email field) | exact |
| `QuestBoard.Service/ViewModels/AccountViewModels/SetPasswordViewModel.cs` (new) | model (view model) | transform | `QuestBoard.Service/ViewModels/AdminViewModels/ResetPasswordViewModel.cs` | exact |
| `QuestBoard.UnitTests/Services/WelcomeEmailJobTests.cs` (new, replaces `ConfirmationEmailJobTests.cs`) | test | event-driven | `QuestBoard.UnitTests/Services/ConfirmationEmailJobTests.cs` | exact |
| `QuestBoard.UnitTests/Services/ForgotPasswordEmailJobTests.cs` (new) | test | event-driven | `QuestBoard.UnitTests/Services/ConfirmationEmailJobTests.cs` | exact |
| `QuestBoard.IntegrationTests/Controllers/AccountControllerIntegrationTests.cs` (modify — add ForgotPassword/SetPassword cases) | test | request-response | itself (`Login_Get_ShouldReturnSuccessStatusCode`, `Logout_Post_ShouldRedirectToHome` for anti-forgery pattern) | exact |

## Pattern Assignments

### `QuestBoard.Domain/Interfaces/IIdentityService.cs` + `QuestBoard.Repository/IdentityService.cs` (service, CRUD)

**Analog:** `AdminResetPasswordAsync` (same file, `QuestBoard.Repository/IdentityService.cs:98-110`) — this is the exact primitive to extract/generalize.

**Existing method to extend/replace — `CreateUserAsync`** (`QuestBoard.Repository/IdentityService.cs:33-51`):
```csharp
public async Task<IdentityResult> CreateUserAsync(string email, string name, string password)
{
    var entity = new UserEntity
    {
        UserName = email,
        Email = email,
        Name = name
    };
    var result = await userManager.CreateAsync(entity, password);

    if (result.Succeeded)
    {
        await userManager.AddToRoleAsync(entity, "Player");
        // Do not sign in until email is confirmed — the admin must send a confirmation
        // link first (via AdminController.SendConfirmationEmail).
    }

    return result;
}
```
Per D-01, change the `userManager.CreateAsync(entity, password)` call to the no-password overload `userManager.CreateAsync(entity)`, and drop the `password` parameter from the signature (or add a new overload — planner's discretion per CONTEXT.md; note research confirms `IUserService.CreateAsync` in `UserService.cs` is the ONLY caller of `IIdentityService.CreateUserAsync`, and `AdminController.CreateUser` is the only caller of `IUserService.CreateAsync`).

**Token-issuance-for-email-link pattern to copy** (`QuestBoard.Repository/IdentityService.cs:98-110`, `AdminResetPasswordAsync`):
```csharp
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
```
New method needed (per RESEARCH.md Code Examples) — same `FindByIdAsync` null-guard shape, but EXPOSES the raw token instead of consuming it immediately:
```csharp
public async Task<string?> GeneratePasswordResetTokenForUserAsync(int userId)
{
    var entity = await userManager.FindByIdAsync(userId.ToString());
    if (entity == null) return null;
    return await userManager.GeneratePasswordResetTokenAsync(entity);
}
```
Mirror this exact null-guard style as `GenerateChangeEmailTokenAsync` (`IdentityService.cs:133-138`) and `GenerateEmailConfirmationAsync` (`IdentityService.cs:118-123`) already do — same `if (entity == null) return null;` idiom.

**Reused as-is (no change needed) — `ResetPasswordAsync`** (`QuestBoard.Repository/IdentityService.cs:90-96`):
```csharp
public async Task<IdentityResult> ResetPasswordAsync(int userId, string token, string newPassword)
{
    var entity = await userManager.FindByIdAsync(userId.ToString());
    if (entity == null)
        return IdentityResult.Failed(new IdentityError { Description = "User not found." });
    return await userManager.ResetPasswordAsync(entity, token, newPassword);
}
```
`SetPassword` POST calls this directly via `IUserService.ResetPasswordAsync(User user, string token, string newPassword)` (`UserService.cs:88-91`).

**New method for D-09's "mark EmailConfirmed=true" side effect** — no existing analog exists (Pitfall 4 in RESEARCH.md confirms no `UserManager.SetEmailConfirmedAsync`). Follow the same file's `ConfirmEmailAsync` shape (`IdentityService.cs:125-131`) for the null-guard, but write the property directly instead of calling a token-verifying method:
```csharp
// Existing analog shape (ConfirmEmailAsync, token-based — for null-guard style only):
public async Task<IdentityResult> ConfirmEmailAsync(int userId, string token)
{
    var entity = await userManager.FindByIdAsync(userId.ToString());
    if (entity == null)
        return IdentityResult.Failed(new IdentityError { Description = "User not found." });
    return await userManager.ConfirmEmailAsync(entity, token);
}
// New (no token verification — EmailConfirmed is a public settable property on IdentityUser<int>):
public async Task<IdentityResult> ConfirmEmailDirectlyAsync(int userId)
{
    var entity = await userManager.FindByIdAsync(userId.ToString());
    if (entity == null)
        return IdentityResult.Failed(new IdentityError { Description = "User not found." });
    entity.EmailConfirmed = true;
    return await userManager.UpdateAsync(entity);
}
```

**Interface additions** — append to `QuestBoard.Domain/Interfaces/IIdentityService.cs:10-30` alongside existing signatures, same one-line style:
```csharp
Task<IdentityResult> ResetPasswordAsync(int userId, string token, string newPassword);
Task<IdentityResult> AdminResetPasswordAsync(ClaimsPrincipal adminUser, int targetUserId, string newPassword);
Task<string?> GenerateEmailConfirmationAsync(int userId);
Task<IdentityResult> ConfirmEmailAsync(int userId, string token);
```

---

### `QuestBoard.Domain/Services/UserService.cs` + `IUserService.cs` (service, CRUD)

**Analog:** `ResetPasswordAsync` overloads within the same file (`UserService.cs:88-96`) — thin pass-through wrappers to `IIdentityService`.

```csharp
public async Task<IdentityResult> ResetPasswordAsync(User user, string token, string newPassword)
{
    return await identityService.ResetPasswordAsync(user.Id, token, newPassword);
}

public async Task<IdentityResult> ResetPasswordAsync(ClaimsPrincipal adminUser, User user, string newPassword)
{
    return await identityService.AdminResetPasswordAsync(adminUser, user.Id, newPassword);
}
```
Any new `IIdentityService` method (token issuance, direct email-confirm) needs a matching one-line pass-through added to `UserService.cs` + `IUserService.cs`, following this exact wrapper shape. `CreateAsync` (`UserService.cs:27-30`) is the one to modify for D-01 (drop `password` arg):
```csharp
public async Task<IdentityResult> CreateAsync(string email, string name, string password)
{
    return await identityService.CreateUserAsync(email, name, password);
}
```

---

### `QuestBoard.Service/Controllers/Admin/AccountController.cs` — new `ForgotPassword` + `SetPassword` actions (controller, request-response)

**Analog:** `ConfirmEmail` / `ConfirmEmailChange` actions within the same file (`AccountController.cs:23-53`, `170-182`).

**Imports already present (reuse as-is)** (`AccountController.cs:1-10`):
```csharp
using QuestBoard.Domain.Interfaces;
using QuestBoard.Service.Controllers.QuestBoard;
using QuestBoard.Service.Jobs;
using QuestBoard.Service.ViewModels.AccountViewModels;
using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using System.Text;
```
Add `using System.Threading.RateLimiting;` and `using Microsoft.AspNetCore.RateLimiting;` for `[EnableRateLimiting]` on `ForgotPassword` POST.

**Anonymous callback pattern to copy** (`AccountController.cs:23-53`, `ConfirmEmail`):
```csharp
[HttpGet]
public async Task<IActionResult> ConfirmEmail(int userId, string token)
{
    if (userId <= 0 || string.IsNullOrEmpty(token))
    {
        TempData["Error"] = "Email confirmation failed. The link may be expired or invalid. Contact an administrator.";
        return RedirectToAction(nameof(Login));
    }

    try
    {
        var decodedToken = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(token));
        var result = await identityService.ConfirmEmailAsync(userId, decodedToken);

        if (result.Succeeded)
        {
            TempData["Success"] = "Email confirmed — you can now log in.";
        }
        else
        {
            TempData["Error"] = "Email confirmation failed. The link may be expired or invalid. Contact an administrator.";
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "ConfirmEmail failed for userId {UserId}", userId);
        TempData["Error"] = "Email confirmation failed. The link may be expired or invalid. Contact an administrator.";
    }

    return RedirectToAction(nameof(Login));
}
```
`SetPassword` follows this shape for token decode + try/catch + TempData + redirect-to-Login, but must render a GET form first (model-bound `userId`/`token` hidden fields) since it needs the new-password input before it can call `ResetPasswordAsync` — model it on `AdminController.ResetPassword` GET/POST pair (below) rather than a pure redirect-only action.

**Simple GET-renders-form / POST-validates pattern to copy** — `ChangePassword` (`AccountController.cs:184-213`):
```csharp
[HttpGet]
[Authorize]
public IActionResult ChangePassword()
{
    return View();
}

[HttpPost]
[ValidateAntiForgeryToken]
[Authorize]
public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
{
    if (ModelState.IsValid)
    {
        var result = await userService.ChangePasswordAsync(User, model.CurrentPassword, model.NewPassword);

        if (result.Succeeded)
        {
            TempData["SuccessMessage"] = "Password changed successfully!";
            return RedirectToAction(nameof(Profile));
        }

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }
    }

    return View(model);
}
```
`SetPassword` mirrors this exactly EXCEPT: no `[Authorize]` (anonymous callback action), the GET must accept and pass through `userId`/`token` route/query params into the view model, and on success it must ALSO call the new direct-email-confirm method (D-09) before redirecting to Login — not to `Profile`.

**Enumeration-safe `ForgotPassword` POST — use RESEARCH.md's verified Pattern 3 directly** (this is a new pattern, no direct codebase analog exists for "always succeed" responses — closest structural analog is the try/catch + generic-message style of `ConfirmEmail` above):
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
            var rawToken = await identityService.GeneratePasswordResetTokenForUserAsync(userId.Value);
            var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(rawToken!));
            var callbackUrl = Url.Action(nameof(SetPassword), "Account",
                new { userId = userId.Value, token = encodedToken }, Request.Scheme);
            jobClient.Enqueue<ForgotPasswordEmailJob>(j => j.ExecuteAsync(model.Email, callbackUrl!, CancellationToken.None));
        }
        TempData["Success"] = "If that email is registered, a reset link has been sent.";
        return RedirectToAction(nameof(ForgotPassword));
    }
    return View(model);
}
```
Note `identityService.GetIdByEmailAsync` already exists (`IdentityService.cs:112-116`) — reuse as-is, same pattern `AdminController.CreateUser` already uses at line 117.

---

### `QuestBoard.Service/Controllers/Admin/AdminController.cs` — `CreateUser` + `SendConfirmationEmail` (controller, request-response)

**Analog:** itself — `CreateUser` POST (`AdminController.cs:101-141`) and `SendConfirmationEmail` (`AdminController.cs:267-290`).

**Current `CreateUser` POST to modify** (`AdminController.cs:101-141`):
```csharp
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> CreateUser(CreateUserViewModel model)
{
    if (!ModelState.IsValid)
    {
        return View(model);
    }

    var groupId = activeGroupContext.ActiveGroupId;
    if (groupId == null) return RedirectToAction("Index", "GroupPicker");

    var result = await userService.CreateAsync(model.Email, model.Name, model.Password);

    if (result.Succeeded)
    {
        var userId = await identityService.GetIdByEmailAsync(model.Email);
        if (userId.HasValue)
        {
            await userService.SetGroupRoleAsync(userId.Value, groupId.Value, model.GroupRole);

            var rawToken = await identityService.GenerateEmailConfirmationAsync(userId.Value);
            if (rawToken != null)
            {
                var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(rawToken));
                var callbackUrl = Url.Action("ConfirmEmail", "Account", new { userId = userId.Value, token = encodedToken }, Request.Scheme);
                jobClient.Enqueue<ConfirmationEmailJob>(j => j.ExecuteAsync(model.Email, model.Name, callbackUrl!, CancellationToken.None));
            }
        }

        TempData["Success"] = $"Account created for {model.Name}. A confirmation email has been sent.";
        return RedirectToAction(nameof(Users));
    }

    foreach (var error in result.Errors)
    {
        ModelState.AddModelError(string.Empty, error.Description);
    }

    return View(model);
}
```
Per D-06: replace `userService.CreateAsync(model.Email, model.Name, model.Password)` with the passwordless call; replace `identityService.GenerateEmailConfirmationAsync` + `ConfirmationEmailJob` block with `identityService.GeneratePasswordResetTokenForUserAsync` + `Url.Action(nameof(AccountController.SetPassword), "Account", ...)` + `jobClient.Enqueue<WelcomeEmailJob>(...)`. Keep the `SetGroupRoleAsync` call and the overall try/success/error shape identical.

**Current `SendConfirmationEmail` to retarget** (`AdminController.cs:267-290`):
```csharp
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> SendConfirmationEmail(int userId)
{
    var user = await userService.GetByIdAsync(userId);
    if (user == null)
    {
        return RedirectToAction(nameof(Users));
    }

    var rawToken = await identityService.GenerateEmailConfirmationAsync(userId);
    if (rawToken == null || string.IsNullOrEmpty(user.Email))
    {
        TempData["Error"] = $"Failed to send confirmation email to {user.Name}. Please try again.";
        return RedirectToAction(nameof(Users));
    }

    var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(rawToken));
    var callbackUrl = Url.Action("ConfirmEmail", "Account", new { userId, token = encodedToken }, Request.Scheme);

    jobClient.Enqueue<ConfirmationEmailJob>(j => j.ExecuteAsync(user.Email!, user.Name, callbackUrl!, CancellationToken.None));
    TempData["Success"] = $"Confirmation email queued for {user.Name}.";
    return RedirectToAction(nameof(Users));
}
```
Same null-guard/error/success shape; swap the token-generation call and job/callback target the same way as `CreateUser` above (D-07/D-14 — action name can stay `SendConfirmationEmail` or be renamed at planner's discretion, but the button/route wiring in `Users.cshtml` must match whichever is chosen).

---

### `QuestBoard.Service/ViewModels/AdminViewModels/CreateUserViewModel.cs` (model, transform)

**Analog:** itself. Remove the `Password` property block:
```csharp
[Required]
[StringLength(100, MinimumLength = 8)]
[DataType(DataType.Password)]
[Display(Name = "Password")]
public string Password { get; set; } = string.Empty;
```
Leave `Email`, `Name`, `GroupRole` untouched (`CreateUserViewModel.cs:1-26`).

---

### `QuestBoard.Service/Views/Admin/CreateUser.cshtml` + `.Mobile.cshtml` (component, request-response)

**Analog:** itself. Remove this block from both files (desktop at `CreateUser.cshtml:37-41`, mobile at `CreateUser.Mobile.cshtml:35-39`):
```html
<div class="mb-3">
    <label asp-for="Password" class="form-label"></label>
    <input asp-for="Password" class="form-control" type="password" />
    <span asp-validation-for="Password" class="text-danger"></span>
</div>
```
Keep the surrounding `modern-card`/`modern-card-header`/`modern-card-body` structure, the `<hr>` before buttons, and the `d-flex justify-content-between` button row (desktop) / `d-grid gap-2` (mobile) exactly as-is — this already matches CLAUDE.md's UI/UX guidelines.

---

### `QuestBoard.Service/Views/Admin/Users.cshtml` (component, request-response) — button relabel (D-07/D-14)

**Analog:** itself, lines 141-151 (the existing conditional button block):
```html
@* Send Confirmation Email button - only for unconfirmed users with an email address *@
@if (!userModel.EmailConfirmed && !string.IsNullOrEmpty(userModel.User.Email))
{
    <form asp-action="SendConfirmationEmail" method="post" class="d-inline me-2">
        <input type="hidden" name="userId" value="@userModel.User.Id" />
        <button type="submit" class="btn btn-sm btn-info">
            <i class="fas fa-envelope me-1"></i>
            Send Confirmation Email
        </button>
    </form>
}
```
Keep the `EmailConfirmed == false` condition and the `d-inline me-2` form wrapper unchanged (D-14); only change the button label text/icon (e.g., "Resend Welcome Email") and, if the action is renamed, the `asp-action` value. Same file's `PromoteToAdmin` block (lines 97-103) shows the identical row-action form idiom used elsewhere on this page — consistent styling reference.

---

### `QuestBoard.Service/Views/Account/Login.cshtml` + `.Mobile.cshtml` (component, request-response) — add "Forgot password?" link (D-08)

**Analog:** itself. Insert a link near the `RememberMe` checkbox / above the submit button. Desktop structure (`Login.cshtml:51-61`):
```html
<div class="mb-3 form-check">
    <input asp-for="RememberMe" class="form-check-input" />
    <label asp-for="RememberMe" class="form-check-label"></label>
</div>

<div class="d-grid gap-2">
    <button type="submit" class="btn btn-warning btn-lg">
        <i class="fas fa-sign-in-alt me-2"></i>
        Log in
    </button>
</div>
```
Add `<a asp-action="ForgotPassword">Forgot password?</a>` between the checkbox div and the submit button div (exact placement/styling at planner's discretion — match existing Bootstrap link conventions used elsewhere, e.g. `text-decoration-none` links seen in other cards). Mobile mirrors the same insertion point in `Login.Mobile.cshtml:27-36`.

---

### `QuestBoard.Service/Program.cs` (config)

**TokenLifespan — net-new config block** (per RESEARCH.md Pitfall 3, nothing to "extend" — this doesn't exist yet). Insert after `.AddDefaultTokenProviders();` (`Program.cs:59`):
```csharp
builder.Services.Configure<DataProtectionTokenProviderOptions>(options =>
{
    options.TokenLifespan = TimeSpan.FromDays(7);
});
```
Follow the same `builder.Services.Configure<T>(options => { ... })` idiom already used for Kestrel at the top of the file (`Program.cs:24-27`):
```csharp
builder.Services.Configure<KestrelServerOptions>(options =>
{
    options.Limits.MaxRequestBodySize = 10 * 1024 * 1024; // 10 MB (slightly higher than validation to allow for form overhead)
});
```

**Rate limiter registration and middleware** — use RESEARCH.md's verified Code Example (Pattern 4) directly; insert `builder.Services.AddRateLimiter(...)` near the `AddAuthorizationBuilder()` block (`Program.cs:61-70`), and `app.UseRateLimiter()` in the pipeline block after `app.UseRouting();` (`Program.cs:172`), alongside the existing:
```csharp
app.UseRouting();

app.UseSession();
app.UseAuthentication();
app.UseMiddleware<GroupSessionMiddleware>();
app.UseAuthorization();
```
**Note:** verify whether `ForwardedHeadersOptions`/`UseForwardedHeaders()` exists elsewhere in `Program.cs` before relying on `RemoteIpAddress` for the rate-limit partition key (RESEARCH.md flags this as unverified — grep the full file during planning, not confirmed absent, only "not found in the sections read during research").

---

### `QuestBoard.Service/Controllers/Admin/EmailPreviewController.cs` (controller, request-response)

**Analog:** itself — `ConfirmEmail()` (lines 70-81, to be replaced) and `ChangeEmailConfirm()` (lines 83-94, structural pattern to copy for both new preview actions).

```csharp
[HttpGet]
public async Task<IActionResult> ConfirmEmail()
{
    var appUrl = $"{Request.Scheme}://{Request.Host}";
    var html = await emailRenderService.RenderAsync<Components.Emails.ConfirmEmail>(new()
    {
        [nameof(Components.Emails.ConfirmEmail.UserName)] = "Arannis",
        [nameof(Components.Emails.ConfirmEmail.CallbackUrl)] = $"{appUrl}/Account/ConfirmEmail?userId=preview&token=preview-token",
        [nameof(Components.Emails.ConfirmEmail.AppUrl)] = appUrl,
    });
    return Content(html, "text/html");
}
```
Replace with `Welcome()` (pointing `CallbackUrl` at `/Account/SetPassword?userId=preview&token=preview-token`) and add `ForgotPassword()` (same shape, `ForgotPassword.razor` params). Also update the `Index()` menu HTML (lines 14-34) — remove the `ConfirmEmail` `<li>` link, add `Welcome` and `ForgotPassword` links, following the exact `<li><a href="{{appUrl}}/EmailPreview/X">Label</a></li>` string-interpolation idiom already used for all five existing entries.

---

### `QuestBoard.Service/Jobs/WelcomeEmailJob.cs` + `ForgotPasswordEmailJob.cs` (service/background job, event-driven)

**Analog:** `QuestBoard.Service/Jobs/ConfirmationEmailJob.cs` (entire file, 31 lines) — copy verbatim and adapt template/subject.

```csharp
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.Service.Components.Emails;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace QuestBoard.Service.Jobs;

public class ConfirmationEmailJob(
    IServiceScopeFactory scopeFactory,
    ILogger<ConfirmationEmailJob> logger)
{
    public async Task ExecuteAsync(string toEmail, string userName, string callbackUrl, CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var renderService = scope.ServiceProvider.GetRequiredService<IEmailRenderService>();
        var emailService  = scope.ServiceProvider.GetRequiredService<IEmailService>();
        var emailSettings = scope.ServiceProvider.GetRequiredService<IOptions<EmailSettings>>().Value;

        var html = await renderService.RenderAsync<ConfirmEmail>(new Dictionary<string, object?>
        {
            { nameof(ConfirmEmail.UserName),    userName },
            { nameof(ConfirmEmail.CallbackUrl), callbackUrl },
            { nameof(ConfirmEmail.AppUrl),      emailSettings.AppUrl }
        });

        await emailService.SendAsync(toEmail, "Confirm your D&D Quest Board account", html);
    }
}
```
`WelcomeEmailJob` — rename type/logger references to `Welcome`, subject to something like `"Welcome to the D&D Quest Board — set your password"`. `ForgotPasswordEmailJob` — same shape, `toEmail`/`callbackUrl` params only (no `userName` needed since D-11 requires no user-identifying content risk — planner's call whether to include name; `ForgotPassword.razor` D-10 template is separate from `Welcome.razor` so parameter list can differ), subject like `"Reset your D&D Quest Board password"`.

**IServiceScopeFactory constraint (shared/cross-cutting):** both new jobs MUST use `IServiceScopeFactory.CreateAsyncScope()` — constructor-injecting `IEmailService`/`DbContext` directly into a Hangfire job class is a locked architectural violation (confirmed still applies per CONTEXT.md code_context and RESEARCH.md Architectural Responsibility Map).

---

### `QuestBoard.Service/Components/Emails/Welcome.razor` (component, transform)

**Analog:** `QuestBoard.Service/Components/Emails/ConfirmEmail.razor` (entire file, 82 lines) — same Cinzel/wax-seal visual style, same `_EmailLayout` wrapper, same three `[Parameter, EditorRequired]` properties (`UserName`, `CallbackUrl`, `AppUrl`).

Key structural pieces to reuse verbatim:
```razor
@using QuestBoard.Service.Components.Emails

<_EmailLayout Subject="..." PreviewText="...">
    <table width="600" ... style="background-image:url('@(AppUrl)/images/Blanks/Blanks%20w%20Shadow/PosterN.png');...">
        ...
        <img src="@(AppUrl)/images/Wax%20Seals/Crown%20Seal.png" width="80" alt="Wax Seal" style="display:block;" />
        <a href="@CallbackUrl" style="background-color:#FFD700;...">Button Text</a>
        ...
    </table>
</_EmailLayout>

@code {
    [Parameter, EditorRequired] public string UserName { get; set; } = string.Empty;
    [Parameter, EditorRequired] public string CallbackUrl { get; set; } = string.Empty;
    [Parameter, EditorRequired] public string AppUrl { get; set; } = string.Empty;
}
```
Change: Subject/PreviewText copy, title text ("Hail, @UserName!" → something like "Welcome, @UserName!"), body copy (mention setting a password AND confirming email in one click per D-03), CTA button text (e.g., "Set My Password"), and optionally the poster image variant (`ChangeEmailConfirm.razor` uses `Poster2.png` with a taller `840px` height — pick whichever poster/height fits the (likely longer) Welcome copy).

---

### `QuestBoard.Service/Components/Emails/ForgotPassword.razor` (component, transform)

**Analog:** `QuestBoard.Service/Components/Emails/ChangeEmailConfirm.razor` (entire file, 82 lines) — distinct template per D-10/Specific Ideas (separate `.razor` file, not shared/parameterized with `Welcome.razor`).

Same structural skeleton as `Welcome.razor` above; differentiate copy: title ("A Reset Has Been Requested" style, mirroring `ChangeEmailConfirm.razor`'s "A New Seal Has Been Requested"), body text about password reset (not email confirmation), CTA button text (e.g., "Reset My Password"), and a security-conscious "if you did not request this" disclaimer line — `ChangeEmailConfirm.razor:38-40` already has this exact pattern:
```html
<p style="font-size:13px;font-family:Georgia,serif;color:#5a4030;line-height:1.5;font-style:italic;text-shadow:1px 1px 3px rgba(255,255,255,0.8);margin:16px 0 0 0;">
    If you did not request this change, you may discard this scroll — your current address remains active.
</p>
```
Adapt wording to "If you did not request a password reset, you may safely ignore this email."

---

### `QuestBoard.Service/Views/Account/ForgotPassword.cshtml` + `.Mobile.cshtml` (component, request-response)

**Analog:** `QuestBoard.Service/Views/Account/Login.cshtml` (desktop, lines 1-66) + `Login.Mobile.cshtml` (mobile, lines 1-39) — same anonymous single-field-form card shape. Uses standard `_Layout.cshtml` automatically via `_ViewStart.cshtml` (no override needed — see Layout note at top of this document).

**Desktop structure to copy** (`Login.cshtml:1-21` header/card shell, `36-49` form field pattern):
```html
@model ForgotPasswordViewModel
@{
    ViewData["Title"] = "Forgot Password";
}

<div class="row justify-content-center">
    <div class="col-md-6 col-lg-4">
        <div class="card modern-card">
            <div class="card-header modern-card-header">
                <h2 class="mb-0">
                    <i class="fas fa-key me-2"></i>
                    @ViewData["Title"]
                </h2>
            </div>
            <div class="card-body modern-card-body">
                @* TempData Success/Error alert blocks — copy verbatim from Login.cshtml:18-34 *@
                <form asp-action="ForgotPassword" method="post">
                    <div asp-validation-summary="All" class="text-danger"></div>

                    <div class="mb-3">
                        <label asp-for="Email" class="form-label"></label>
                        <input asp-for="Email" class="form-control" />
                        <span asp-validation-for="Email" class="text-danger"></span>
                    </div>

                    <div class="d-grid gap-2">
                        <button type="submit" class="btn btn-warning btn-lg">
                            <i class="fas fa-paper-plane me-2"></i>
                            Send reset link
                        </button>
                    </div>
                </form>
            </div>
        </div>
    </div>
</div>
```
**TempData alert blocks to copy verbatim** (`Login.cshtml:18-34`):
```html
@if (TempData["Success"] != null)
{
    <div class="alert alert-success alert-dismissible fade show" role="alert">
        <i class="fas fa-check-circle me-2"></i>
        @TempData["Success"]
        <button type="button" class="btn-close" data-bs-dismiss="alert" aria-label="Close"></button>
    </div>
}

@if (TempData["Error"] != null)
{
    <div class="alert alert-danger alert-dismissible fade show" role="alert">
        <i class="fas fa-exclamation-triangle me-2"></i>
        @TempData["Error"]
        <button type="button" class="btn-close" data-bs-dismiss="alert" aria-label="Close"></button>
    </div>
}
```
This is exactly where the D-11 generic message ("If that email is registered, a reset link has been sent.") renders — POST redirects back to the GET action with `TempData["Success"]` set, matching this codebase's existing TempData-then-redirect idiom used everywhere else (`AdminController` row actions, `ConfirmEmail`, etc).

**Mobile structure to copy** (`Login.Mobile.cshtml`, entire file):
```html
@model ForgotPasswordViewModel

@section Styles {
    <link href="~/css/account.mobile.css" asp-append-version="true" rel="stylesheet" />
}

<div class="account-card-mobile mb-3">
    <h5 class="mb-3"><i class="fas fa-key text-warning me-2"></i>Forgot Password</h5>

    <div asp-validation-summary="All" class="text-danger mb-3"></div>

    <form asp-action="ForgotPassword" method="post">
        <div class="mb-3">
            <label asp-for="Email" class="form-label"></label>
            <input asp-for="Email" class="form-control" />
            <span asp-validation-for="Email" class="text-danger"></span>
        </div>

        <div class="d-grid gap-2 mt-3">
            <button type="submit" class="btn btn-warning btn-lg">
                <i class="fas fa-paper-plane me-2"></i>Send reset link
            </button>
        </div>
    </form>
</div>
```

---

### `QuestBoard.Service/Views/Account/SetPassword.cshtml` + `.Mobile.cshtml` (component, request-response)

**Analog:** `QuestBoard.Service/Views/Account/ChangePassword.cshtml` (desktop, lines 1-57) for the password-field/`Compare` shape, combined with `Login.cshtml`'s anonymous-card structure (no `[Authorize]`, no nav-dependent content) and hidden fields for `UserId`/`Token`.

**Desktop structure to copy** (`ChangePassword.cshtml:1-56`, adapted — drop `CurrentPassword` since this is a token-authorized reset, not a self-service change, add hidden `UserId`/`Token`):
```html
@model SetPasswordViewModel
@{
    ViewData["Title"] = "Set Your Password";
}

<div class="row justify-content-center">
    <div class="col-md-6">
        <div class="card modern-card">
            <div class="card-header modern-card-header">
                <h2 class="mb-0">
                    <i class="fas fa-key text-primary me-2"></i>
                    @ViewData["Title"]
                </h2>
            </div>
            <div class="card-body modern-card-body">
                <form asp-action="SetPassword" method="post">
                    <div asp-validation-summary="All" class="text-danger mb-3"></div>

                    <input type="hidden" asp-for="UserId" />
                    <input type="hidden" asp-for="Token" />

                    <div class="mb-3">
                        <label asp-for="NewPassword" class="form-label"></label>
                        <input asp-for="NewPassword" class="form-control" />
                        <span asp-validation-for="NewPassword" class="text-danger"></span>
                    </div>

                    <div class="mb-3">
                        <label asp-for="ConfirmPassword" class="form-label"></label>
                        <input asp-for="ConfirmPassword" class="form-control" />
                        <span asp-validation-for="ConfirmPassword" class="text-danger"></span>
                    </div>

                    <hr>

                    <div class="d-grid gap-2">
                        <button type="submit" class="btn btn-primary">
                            <i class="fas fa-save me-2"></i>
                            Set Password
                        </button>
                    </div>
                </form>
            </div>
        </div>
    </div>
</div>
```
Note: unlike `ChangePassword.cshtml`'s two-button `d-flex justify-content-between` (Cancel left / Submit right — CLAUDE.md's stated button convention for pages with a natural "cancel" destination), `SetPassword` is an anonymous single-purpose landing page with no natural cancel target, so a single centered submit button (`d-grid gap-2`, mirroring `Login.cshtml`'s single-button pattern) is the better fit — planner's discretion, but do not force a fabricated Cancel link.

**Mobile structure** — same adaptation pattern applied to `ChangePassword.Mobile.cshtml`'s shape (not read in full here but structurally identical to `CreateUser.Mobile.cshtml`'s `admin-form-card-mobile`/`account-card-mobile` wrapper + `@Html.AntiForgeryToken()` + `d-grid gap-2` idiom already shown above).

---

### `QuestBoard.Service/ViewModels/AccountViewModels/ForgotPasswordViewModel.cs` (model, transform)

**Analog:** `QuestBoard.Service/ViewModels/AccountViewModels/LoginViewModel.cs` (`Email` field only, lines 1-9):
```csharp
using System.ComponentModel.DataAnnotations;

namespace QuestBoard.Service.ViewModels.AccountViewModels;

public class ForgotPasswordViewModel
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
}
```

---

### `QuestBoard.Service/ViewModels/AccountViewModels/SetPasswordViewModel.cs` (model, transform)

**Analog:** `QuestBoard.Service/ViewModels/AdminViewModels/ResetPasswordViewModel.cs` (entire file, 21 lines) — same `UserId`/password/`Compare` shape, but `Token` (string) replaces `UserName` (string, display-only).

```csharp
using System.ComponentModel.DataAnnotations;

namespace QuestBoard.Service.ViewModels.AccountViewModels;

public class SetPasswordViewModel
{
    public int UserId { get; set; }

    public string Token { get; set; } = string.Empty;

    [Required]
    [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 8)]
    [DataType(DataType.Password)]
    [Display(Name = "New Password")]
    public string NewPassword { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    [Display(Name = "Confirm New Password")]
    [Compare("NewPassword", ErrorMessage = "The new password and confirmation password do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
```
**IMPORTANT (RESEARCH.md Pitfall 5):** use `MinimumLength = 8`, NOT `MinimumLength = 6` — the existing `ChangePasswordViewModel`/`ResetPasswordViewModel` analogs both use 6, which does NOT match `Program.cs`'s actual `options.Password.RequiredLength = 8` (`Program.cs:48`). Do not blindly copy the `MinimumLength = 6` from the two existing view models being used as structural analogs here — that value is a pre-existing bug, not a pattern to propagate.

---

### `QuestBoard.UnitTests/Services/WelcomeEmailJobTests.cs` + `ForgotPasswordEmailJobTests.cs` (test, event-driven)

**Analog:** `QuestBoard.UnitTests/Services/ConfirmationEmailJobTests.cs` (entire file, 89 lines) — copy verbatim, retarget generic type parameters and expected subject string.

```csharp
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.Service.Components.Emails;
using QuestBoard.Service.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace QuestBoard.UnitTests.Services;

public class ConfirmationEmailJobTests
{
    private readonly IEmailRenderService _renderService;
    private readonly IEmailService _emailService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ConfirmationEmailJob _sut;

    public ConfirmationEmailJobTests()
    {
        _renderService = Substitute.For<IEmailRenderService>();
        _emailService  = Substitute.For<IEmailService>();

        var emailOptions = Substitute.For<IOptions<EmailSettings>>();
        emailOptions.Value.Returns(new EmailSettings { AppUrl = "https://example.com" });

        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(IEmailRenderService)).Returns(_renderService);
        serviceProvider.GetService(typeof(IEmailService)).Returns(_emailService);
        serviceProvider.GetService(typeof(IOptions<EmailSettings>)).Returns(emailOptions);

        var scope = Substitute.For<IServiceScope>();
        scope.ServiceProvider.Returns(serviceProvider);

        _scopeFactory = Substitute.For<IServiceScopeFactory>();
        _scopeFactory.CreateAsyncScope().Returns(new AsyncServiceScope(scope));

        var logger = Substitute.For<ILogger<ConfirmationEmailJob>>();
        _sut = new ConfirmationEmailJob(_scopeFactory, logger);
    }

    [Fact]
    public async Task ExecuteAsync_CallsRenderAsync_WithCorrectParameters() { /* ... same shape, see analog ... */ }

    [Fact]
    public async Task ExecuteAsync_CallsSendAsync_WithRenderedHtml() { /* ... same shape, see analog ... */ }
}
```
For `WelcomeEmailJobTests`: replace `ConfirmationEmailJob` → `WelcomeEmailJob`, `ConfirmEmail` → `Welcome` throughout, expected subject → the new Welcome subject string. For `ForgotPasswordEmailJobTests`: replace with `ForgotPasswordEmailJob`/`ForgotPassword`, and adjust the constructor test setup if the job's `ExecuteAsync` signature drops the `userName` parameter (per the D-10 template decision above).

**Files to delete per CONTEXT.md:** `QuestBoard.UnitTests/Services/ConfirmationEmailJobTests.cs` itself is being replaced — confirm the new `WelcomeEmailJobTests.cs` supersedes it before deleting.

---

### `QuestBoard.IntegrationTests/Controllers/AccountControllerIntegrationTests.cs` (test, request-response)

**Analog:** itself — `Login_Get_ShouldReturnSuccessStatusCode` (lines 11-21) for simple GET assertions, `Logout_Post_ShouldRedirectToHome` (lines 84-109) for the anti-forgery-token POST pattern.

**Simple GET test pattern:**
```csharp
[Fact]
public async Task Login_Get_ShouldReturnSuccessStatusCode()
{
    var response = await _client.GetAsync("/Account/Login", TestContext.Current.CancellationToken);

    response.StatusCode.Should().Be(HttpStatusCode.OK);
    var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
    content.Should().Contain("Login");
}
```
Use this shape for `ForgotPassword_Get_ShouldReturnSuccessStatusCode` and a `SetPassword_Get_WithValidToken_ShouldReturnSuccessStatusCode` variant.

**Anti-forgery POST pattern:**
```csharp
[Fact]
public async Task Logout_Post_ShouldRedirectToHome()
{
    var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(factory);

    var getResponse = await client.GetAsync("/Account/Profile", TestContext.Current.CancellationToken);
    var (token, cookieValue) = await AntiForgeryHelper.ExtractAntiForgeryTokenAsync(getResponse);

    if (!string.IsNullOrEmpty(cookieValue))
    {
        client.DefaultRequestHeaders.Add("Cookie", $".AspNetCore.Antiforgery={cookieValue}");
    }

    var formContent = AntiForgeryHelper.CreateFormContentWithAntiForgeryToken([], token);

    var response = await client.PostAsync("/Account/Logout", formContent, TestContext.Current.CancellationToken);

    response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.OK);
}
```
Use this shape for `ForgotPassword_Post_*` (enumeration-safety cases: existing email vs. non-existent email both return the same TempData message/redirect) and `SetPassword_Post_*` (valid token sets password + `EmailConfirmed`; invalid/expired token fails gracefully). For the rate-limit test (PWFLOW-04), issue 4 rapid POSTs to `/Account/ForgotPassword` from `_client` and assert the 4th returns `HttpStatusCode.TooManyRequests` (429) — no existing analog for this specific assertion exists in the codebase; it is new but uses the same `_client`/`FormUrlEncodedContent` primitives already established in `Register_Post_WithValidData_ShouldReturnNotFound` (lines 35-54) for constructing form-encoded POST bodies.

## Shared Patterns

### IServiceScopeFactory job scoping (locked architectural decision)
**Source:** `QuestBoard.Service/Jobs/ConfirmationEmailJob.cs:14-19`, `QuestBoard.Service/Jobs/ChangeEmailConfirmationJob.cs:14-19`
**Apply to:** `WelcomeEmailJob.cs`, `ForgotPasswordEmailJob.cs`
```csharp
await using var scope = scopeFactory.CreateAsyncScope();
var renderService = scope.ServiceProvider.GetRequiredService<IEmailRenderService>();
var emailService  = scope.ServiceProvider.GetRequiredService<IEmailService>();
var emailSettings = scope.ServiceProvider.GetRequiredService<IOptions<EmailSettings>>().Value;
```
Never constructor-inject `IEmailService`/scoped services directly into a Hangfire job class.

### Token encode/decode (WebEncoders Base64Url)
**Source:** `QuestBoard.Service/Controllers/Admin/AccountController.cs:34` (decode), `:151-154` (encode), `AdminController.cs:125` (encode)
**Apply to:** `AccountController.ForgotPassword` (encode), `AccountController.SetPassword` (decode)
```csharp
// Encode (before building callback URL):
var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(rawToken));
var callbackUrl = Url.Action(nameof(SetPassword), "Account",
    new { userId = userId.Value, token = encodedToken }, Request.Scheme);
// Decode (inside the receiving action):
var decodedToken = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(token));
```

### TempData Success/Error + RedirectToAction (row-actions and auth flows alike)
**Source:** `AdminController.cs` row actions (e.g. `ResetPassword` POST, lines 224-251), `AccountController.ConfirmEmail`/`ConfirmEmailChange`
**Apply to:** `ForgotPassword` POST, `SetPassword` POST, `SendConfirmationEmail`/Welcome-resend action
```csharp
TempData["Success"] = "...";
return RedirectToAction(nameof(Users)); // or nameof(Login), nameof(ForgotPassword), etc.
```

### Alert rendering (Bootstrap dismissible alerts keyed on TempData)
**Source:** `QuestBoard.Service/Views/Account/Login.cshtml:18-34`
**Apply to:** `ForgotPassword.cshtml`, `SetPassword.cshtml` (and their `.Mobile.cshtml` counterparts, using the mobile card wrapper instead of `modern-card`)
```html
@if (TempData["Success"] != null)
{
    <div class="alert alert-success alert-dismissible fade show" role="alert">
        <i class="fas fa-check-circle me-2"></i>
        @TempData["Success"]
        <button type="button" class="btn-close" data-bs-dismiss="alert" aria-label="Close"></button>
    </div>
}
```

### Standard `_Layout.cshtml` for all Account views, including new anonymous ones
**Source:** `QuestBoard.Service/Views/_ViewStart.cshtml` (applies globally, no per-view override in `Views/Account/`)
**Apply to:** `ForgotPassword.cshtml`, `SetPassword.cshtml` — no `Layout = "..."` statement needed in these new files; the existing `_ViewStart.cshtml` mobile/desktop switch handles it automatically, exactly as it already does for `Login.cshtml`.

### IdentityResult error-to-ModelState loop
**Source:** `AccountController.ChangePassword` POST (lines 206-209), `AdminController.CreateUser`/`ResetPassword` POST
**Apply to:** `SetPassword` POST, modified `CreateUser` POST
```csharp
foreach (var error in result.Errors)
{
    ModelState.AddModelError(string.Empty, error.Description);
}
```

## No Analog Found

None — every file in CONTEXT.md's Key Files / New Files / Files to Delete lists has at least a role-match or exact analog in the existing codebase. This phase is almost entirely a recombination of already-established patterns (Identity token flows, Hangfire email jobs, Razor email templates, row-action admin buttons).

## Metadata

**Analog search scope:** `QuestBoard.Service/Controllers/Admin/`, `QuestBoard.Service/Views/Account/`, `QuestBoard.Service/Views/Admin/`, `QuestBoard.Service/Jobs/`, `QuestBoard.Service/Components/Emails/`, `QuestBoard.Service/ViewModels/AccountViewModels/`, `QuestBoard.Service/ViewModels/AdminViewModels/`, `QuestBoard.Domain/Interfaces/`, `QuestBoard.Domain/Services/`, `QuestBoard.Repository/`, `QuestBoard.Service/Program.cs`, `QuestBoard.UnitTests/Services/`, `QuestBoard.IntegrationTests/Controllers/`
**Files scanned:** 24 read in full (IdentityService.cs, IIdentityService.cs, UserService.cs, IUserService.cs, AccountController.cs, AdminController.cs, ConfirmationEmailJob.cs, ChangeEmailConfirmationJob.cs, ConfirmEmail.razor, ChangeEmailConfirm.razor, _EmailLayout.razor, Login.cshtml, Login.Mobile.cshtml, ChangePassword.cshtml, ChangePasswordViewModel.cs, LoginViewModel.cs, CreateUser.cshtml, CreateUser.Mobile.cshtml, CreateUserViewModel.cs, ResetPasswordViewModel.cs, Users.cshtml (partial), EmailPreviewController.cs, Program.cs (partial), ConfirmationEmailJobTests.cs, AccountControllerIntegrationTests.cs, _ViewStart.cshtml)
**Pattern extraction date:** 2026-07-01
