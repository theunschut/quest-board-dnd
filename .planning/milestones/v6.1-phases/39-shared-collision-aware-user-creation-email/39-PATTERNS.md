# Phase 39: Shared Collision-Aware User Creation & Email - Pattern Map

**Mapped:** 2026-07-04
**Files analyzed:** 8 (2 new, 6 modified) + 1 existing helper class extended
**Analogs found:** 8 / 8

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|--------------------|------|-----------|-----------------|----------------|
| `QuestBoard.Domain/Interfaces/IUserService.cs` (add `CreateOrAddToGroupAsync`) | service (interface) | CRUD | same file, existing `SetGroupRoleAsync`/`CreateAsync` members | exact (same interface) |
| `QuestBoard.Domain/Services/UserService.cs` (add `CreateOrAddToGroupAsync`) | service | CRUD + event-driven (email trigger signal) | `GroupService.AddMemberAsync` (`QuestBoard.Domain/Services/GroupService.cs:23-25`) + `UserService.CreateAsync` (same file, lines 30-34) | exact |
| `QuestBoard.Service/Controllers/Admin/AdminController.cs` (`CreateUser` POST, refactor) | controller | request-response | same file's own current `CreateUser` action (`AdminController.cs:113-157`) and `SendConfirmationEmail` (`AdminController.cs:329-377`) | exact (self-analog, refactor in place) |
| `QuestBoard.Service/Extensions/ControllerExtensions.cs` (add `RedirectWithWarning`) | utility | request-response | same file's `RedirectWithSuccess`/`RedirectWithError` (`ControllerExtensions.cs:24-31`) | exact |
| `QuestBoard.Service/Components/Emails/AddedToGroup.razor` (new) | component (email template) | transform (data → HTML) | `QuestBoard.Service/Components/Emails/Welcome.razor` (whole file) | exact |
| `QuestBoard.Service/Jobs/GroupMembershipAddedEmailJob.cs` (new) | service (background job) | event-driven | `QuestBoard.Service/Jobs/WelcomeEmailJob.cs` (whole file) | exact |
| `QuestBoard.Domain/Interfaces/IGroupRepository.cs` / `GroupRepository.cs` (no change expected — reused as-is) | repository | CRUD | `AddMemberAsync` (`QuestBoard.Repository/GroupRepository.cs:49-64`) | exact (reuse, no modification) |
| `QuestBoard.Service/Views/Admin/Users.cshtml` (flash rendering, verify `alert-warning` block added) | view (partial flash render) | request-response | same file's existing `Success`/`Error` TempData blocks (`Users.cshtml:21-34`) | exact |

## Pattern Assignments

### `QuestBoard.Domain/Services/UserService.cs` — new `CreateOrAddToGroupAsync` (service, CRUD)

**Primary analogs:** `GroupService.AddMemberAsync` (throw-on-collision) + `UserService.CreateAsync` (brand-new path) + `IIdentityService.GetIdByEmailAsync`/`HasPasswordAsync` (collision detection)

**Existence-check-then-throw pattern to mirror** (`QuestBoard.Repository/GroupRepository.cs:49-64`):
```csharp
public async Task AddMemberAsync(int groupId, int userId, GroupRole groupRole, CancellationToken token = default)
{
    // Check existence first — UserGroups has unique composite index on (UserId, GroupId)
    var exists = await DbContext.UserGroups
        .AnyAsync(ug => ug.UserId == userId && ug.GroupId == groupId, token);
    if (exists)
        throw new InvalidOperationException("User is already a member of this group.");

    DbContext.UserGroups.Add(new UserGroupEntity
    {
        UserId = userId,
        GroupId = groupId,
        GroupRole = (int)groupRole
    });
    await DbContext.SaveChangesAsync(token);
}
```
`IGroupService.AddMemberAsync` (`QuestBoard.Domain/Interfaces/IGroupService.cs:23-27`) already documents this throw contract — the new shared method should call `groupService.AddMemberAsync(...)` (not `userService.SetGroupRoleAsync`, which is upsert-only and gives no "already exists" signal) and catch `InvalidOperationException` internally, or let it bubble and have the caller catch it (see Controller section below for which approach the caller expects).

**Collision-detection pattern to copy** (`AdminController.cs:129-146`, adapt into the Domain method):
```csharp
var userId = await identityService.GetIdByEmailAsync(model.Email);
if (userId.HasValue)
{
    await userService.SetGroupRoleAsync(userId.Value, groupId.Value, model.GroupRole);

    var rawToken = await identityService.GeneratePasswordResetTokenForUserAsync(userId.Value);
    if (rawToken != null)
    {
        var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(rawToken));
        var callbackUrl = Url.Action("SetPassword", "Account", new { userId = userId.Value, token = encodedToken }, Request.Scheme);
        // ... enqueue WelcomeEmailJob
    }
}
```
Note: the Domain-layer method cannot call `Url.Action` (MVC-only) — the shared method should return a result/enum indicating which email variant the controller must send, and the controller builds the callback URL and enqueues the job (see Integration Points below). This mirrors how `AdminController.CreateUser` already owns the `Url.Action` + `jobClient.Enqueue` calls today, just fed by the new Domain method's outcome instead of inline logic.

**Stranded-account check** — read `EmailConfirmed` off the domain `User` model (`QuestBoard.Domain/Models/User.cs:20`) after loading the colliding user, mirroring the `hasExistingPassword` check already used in `SendConfirmationEmail`:
```csharp
// AdminController.cs:374-375
var hasExistingPassword = await userService.HasPasswordAsync(userId);
jobClient.Enqueue<WelcomeEmailJob>(j => j.ExecuteAsync(user.Email!, user.Name, callbackUrl, !hasExistingPassword, CancellationToken.None));
```
Use `EmailConfirmed == false` (per D-01) as the stranded-account signal instead of `HasPasswordAsync`, but the resend-token-then-enqueue shape is identical to `SendConfirmationEmail` (`AdminController.cs:329-377`) in full:
```csharp
var rawToken = await identityService.GeneratePasswordResetTokenForUserAsync(userId);
if (rawToken == null || string.IsNullOrEmpty(user.Email))
{
    return this.RedirectWithError(nameof(Users), $"Failed to send confirmation email to {user.Name}. Please try again.");
}

var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(rawToken));
var callbackUrl = Url.Action("SetPassword", "Account", new { userId, token = encodedToken }, Request.Scheme);
// ...
jobClient.Enqueue<WelcomeEmailJob>(j => j.ExecuteAsync(user.Email!, user.Name, callbackUrl, !hasExistingPassword, CancellationToken.None));
```

---

### `QuestBoard.Service/Controllers/Admin/AdminController.cs` — `CreateUser` POST refactor (controller, request-response)

**Analog:** the controller's own current implementation, plus the exception-catch shape from `GroupController.AddMember`

**Current shape being replaced** (`AdminController.cs:113-157`, full method) — the new version replaces the inline `CreateAsync` + `SetGroupRoleAsync` + token/URL/enqueue block with one call to the new Domain method, then branches on its result for the three flash-message outcomes (D-09).

**"Already a member" catch pattern to mirror** (`QuestBoard.Service/Areas/Platform/Controllers/GroupController.cs:132-152`):
```csharp
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> AddMember(int id, [Bind(Prefix = "AddMember")] AddMemberViewModel model)
{
    if (!ModelState.IsValid)
    {
        TempData["Error"] = "Invalid form submission.";
        return RedirectToAction(nameof(Members), new { id });
    }
    try
    {
        await groupService.AddMemberAsync(id, model.UserId, model.Role);
        var user = await userService.GetByIdAsync(model.UserId);
        TempData["Success"] = $"{user?.Name ?? "User"} added to the group as {model.Role}.";
    }
    catch (InvalidOperationException)
    {
        TempData["Error"] = "This user is already a member of the group.";
    }
    return RedirectToAction(nameof(Members), new { id });
}
```
Adapt this catch-`InvalidOperationException` shape for CREATE-03 ("already a member"), but use `this.RedirectWithWarning(...)` (new helper, D-08) instead of raw `TempData["Error"]`, per D-09's third bullet.

**Existing `TryReturnInvalidModel` guard** (already used at top of `CreateUser`, `AdminController.cs:117-120`) — keep unchanged:
```csharp
if (this.TryReturnInvalidModel(model, out var invalidModelResult))
{
    return invalidModelResult!;
}
```

**Three-outcome flash messages to implement verbatim (D-09):**
```csharp
// New account
return this.RedirectWithSuccess(nameof(Users), $"Account created for {model.Name}. A welcome email with a set-password link has been sent.");

// Collision-add
return this.RedirectWithSuccess(nameof(Users), $"{model.Name} has been added to the group as {model.GroupRole}. A notification email has been sent.");

// Already a member (CREATE-03)
return this.RedirectWithWarning(nameof(Users), $"{model.Name} is already a member of this group.");
```

---

### `QuestBoard.Service/Extensions/ControllerExtensions.cs` — add `RedirectWithWarning` (utility, request-response)

**Analog:** same file, `RedirectWithError` (lines 30-31), directly adjacent to `RedirectWithSuccess`

**Exact pattern to copy** (`ControllerExtensions.cs:24-31`):
```csharp
/// <summary>
/// Sets TempData["Success"] and redirects to the given action.
/// </summary>
internal static IActionResult RedirectWithSuccess(this Controller controller, string action, string message)
    => controller.RedirectWithMessage(action, "Success", message);

/// <summary>
/// Sets TempData["Error"] and redirects to the given action.
/// </summary>
internal static IActionResult RedirectWithError(this Controller controller, string action, string message)
    => controller.RedirectWithMessage(action, "Error", message);
```
New sibling to add immediately after `RedirectWithError`:
```csharp
/// <summary>
/// Sets TempData["Warning"] and redirects to the given action.
/// </summary>
internal static IActionResult RedirectWithWarning(this Controller controller, string action, string message)
    => controller.RedirectWithMessage(action, "Warning", message);
```

**View-side rendering to mirror** — `Views/Admin/Users.cshtml` renders `Success`/`Error` TempData as Bootstrap alerts (`Users.cshtml:21-34`):
```cshtml
@if (TempData["Success"] != null)
{
    <div class="alert alert-success alert-dismissible fade show" role="alert">
        ...
        @TempData["Success"]
    </div>
}
@if (TempData["Error"] != null)
{
    <div class="alert alert-danger alert-dismissible fade show" role="alert">
        ...
        @TempData["Error"]
```
Add an equivalent `@if (TempData["Warning"] != null)` block using Bootstrap's `alert-warning` class, following the exact same dismissible-alert markup shape. Note: this flash rendering is duplicated per-view (Admin/Users.cshtml, Account/Profile.cshtml, Shop/Index.cshtml, etc. — 12 files use `TempData[...]` per the grep scan) rather than centralized in `_Layout.cshtml`; only `Views/Admin/Users.cshtml` needs the new `Warning` block for this phase since that's the only view this feature touches.

---

### `QuestBoard.Service/Components/Emails/AddedToGroup.razor` (new) (component, transform)

**Analog:** `QuestBoard.Service/Components/Emails/Welcome.razor` (whole file, 93 lines) — copy structure exactly per D-04

**Full structural pattern to copy** (`Welcome.razor:1-93`):
- Same `@using QuestBoard.Service.Components.Emails` + `<_EmailLayout Subject="..." PreviewText="...">` wrapper (lines 1-3)
- Same 600px table, parchment background image, Cinzel serif title block (lines 4-29)
- Same wax-seal image + CTA button row structure (lines 64-79) — for `AddedToGroup.razor`, per D-05 the CTA is a plain link to `/Account/Login` with **no token parameter**, unlike Welcome's `@CallbackUrl` which carries the SetPassword token
- Same `@code` block parameter declaration style (lines 87-92):
```csharp
@code {
    [Parameter, EditorRequired] public string UserName { get; set; } = string.Empty;
    [Parameter, EditorRequired] public string CallbackUrl { get; set; } = string.Empty;
    [Parameter, EditorRequired] public string AppUrl { get; set; } = string.Empty;
    [Parameter, EditorRequired] public bool IsNewAccount { get; set; } = true;
}
```
For `AddedToGroup.razor`, replace `CallbackUrl`/`IsNewAccount` params with `GroupName` and `Role` (per D-06: "You've been added to {GroupName} as a {Role}.") and a `LoginUrl` (plain, no token) parameter for the CTA link.

**Shell dependency** — `_EmailLayout.razor` (whole file, unchanged, reused as-is):
```razor
<_EmailLayout Subject="@Subject" PreviewText="...">
    @ChildContent
</_EmailLayout>
```
`_EmailLayout.razor` takes `Subject` and optional `PreviewText` params (`_EmailLayout.razor:28-29`) — no changes needed to this file.

---

### `QuestBoard.Service/Jobs/GroupMembershipAddedEmailJob.cs` (new) (service, event-driven)

**Analog:** `QuestBoard.Service/Jobs/WelcomeEmailJob.cs` (whole file, 33 lines) — copy structure exactly

**Full structural pattern to copy** (`WelcomeEmailJob.cs:1-32`):
```csharp
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.Service.Components.Emails;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace QuestBoard.Service.Jobs;

public class WelcomeEmailJob(
    IServiceScopeFactory scopeFactory,
    ILogger<WelcomeEmailJob> logger)
{
    public async Task ExecuteAsync(string toEmail, string userName, string callbackUrl, bool isNewAccount, CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var renderService = scope.ServiceProvider.GetRequiredService<IEmailRenderService>();
        var emailService  = scope.ServiceProvider.GetRequiredService<IEmailService>();
        var emailSettings = scope.ServiceProvider.GetRequiredService<IOptions<EmailSettings>>().Value;

        var html = await renderService.RenderAsync<Welcome>(new Dictionary<string, object?>
        {
            { nameof(Welcome.UserName),      userName },
            { nameof(Welcome.CallbackUrl),   callbackUrl },
            { nameof(Welcome.AppUrl),        emailSettings.AppUrl },
            { nameof(Welcome.IsNewAccount),  isNewAccount }
        });

        await emailService.SendAsync(toEmail, "Welcome to the D&D Quest Board — set your password", html);
        logger.LogInformation("WelcomeEmailJob: sent welcome email.");
    }
}
```
For `GroupMembershipAddedEmailJob`, take `toEmail`, `userName`, `groupName`, `role`, `loginUrl` parameters, render `AddedToGroup` instead of `Welcome`, and follow the established per-email subject-line style (per Claude's Discretion note in CONTEXT.md) — e.g. `"You've been added to {groupName}"` — matching `WelcomeEmailJob`'s pattern of a static/formatted subject string passed directly to `emailService.SendAsync`.

**Constructor/DI convention (critical, established since Phase 20):** Hangfire jobs always take `IServiceScopeFactory`, never constructor-inject scoped services directly — copy this exactly, do not inject `IEmailRenderService`/`IEmailService` directly into the job's primary constructor.

---

## Shared Patterns

### TempData flash-message helpers (controller-wide)
**Source:** `QuestBoard.Service/Extensions/ControllerExtensions.cs:9-48` (whole file)
**Apply to:** `AdminController.CreateUser` (all three outcome branches)
```csharp
internal static IActionResult RedirectWithMessage(this Controller controller, string action, string tempDataKey, string message)
{
    controller.TempData[tempDataKey] = message;
    return controller.RedirectToAction(action);
}
internal static IActionResult RedirectWithSuccess(this Controller controller, string action, string message)
    => controller.RedirectWithMessage(action, "Success", message);
internal static IActionResult RedirectWithError(this Controller controller, string action, string message)
    => controller.RedirectWithMessage(action, "Error", message);
```

### Existence-check-then-throw for "already exists" detection
**Source:** `QuestBoard.Repository/GroupRepository.cs:49-64` (`AddMemberAsync`)
**Apply to:** the new `IUserService.CreateOrAddToGroupAsync` Domain method (call `IGroupService.AddMemberAsync`, catch `InvalidOperationException` to detect CREATE-03)

### Hangfire job scope pattern
**Source:** `QuestBoard.Service/Jobs/WelcomeEmailJob.cs:10-19`
**Apply to:** `GroupMembershipAddedEmailJob` — always `IServiceScopeFactory` + `CreateAsyncScope()`, never direct scoped-service injection

### Email visual template shell
**Source:** `QuestBoard.Service/Components/Emails/_EmailLayout.razor` (whole file, unchanged) + `Welcome.razor` (whole file, structural copy target)
**Apply to:** `AddedToGroup.razor` — same wax-seal/Cinzel/parchment look (D-04), no simplified style

### Resend-token-then-enqueue (stranded account)
**Source:** `QuestBoard.Service/Controllers/Admin/AdminController.cs:329-377` (`SendConfirmationEmail`)
**Apply to:** the stranded-account branch inside the new collision-handling flow (D-01) — fresh `GeneratePasswordResetTokenForUserAsync`, `Url.Action("SetPassword", "Account", ...)`, re-enqueue `WelcomeEmailJob`

## No Analog Found

None — every file in scope has a strong existing analog in the codebase (this phase is explicitly building a shared/consolidated version of patterns that already exist in at least two places: `AdminController.CreateUser` and `GroupController.AddMember`).

## Metadata

**Analog search scope:** `QuestBoard.Domain/Services/`, `QuestBoard.Domain/Interfaces/`, `QuestBoard.Repository/GroupRepository.cs`, `QuestBoard.Service/Controllers/Admin/`, `QuestBoard.Service/Areas/Platform/Controllers/`, `QuestBoard.Service/Extensions/`, `QuestBoard.Service/Components/Emails/`, `QuestBoard.Service/Jobs/`, `QuestBoard.Service/Views/Admin/Users.cshtml`
**Files scanned:** 13
**Pattern extraction date:** 2026-07-04
