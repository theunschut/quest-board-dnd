# Phase 41: Safe User Removal & Account Disable - Pattern Map

**Mapped:** 2026-07-04
**Files analyzed:** 12
**Analogs found:** 12 / 12

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|--------------------|------|-----------|-----------------|----------------|
| `QuestBoard.Service/Controllers/Admin/AdminController.cs` (`DeleteUser`, modified) | controller | request-response (CRUD delete-membership) | `Areas/Platform/Controllers/GroupController.cs` (`RemoveMember`, lines 267-274) | exact |
| `QuestBoard.Domain/Interfaces/IIdentityService.cs` (3 new methods) | service (interface) | request-response | same file (existing method signatures) | exact |
| `QuestBoard.Repository/IdentityService.cs` (3 new methods) | service (impl) | CRUD (state mutation via `UserManager`) | same file, `ConfirmEmailDirectlyAsync` (lines 141-149) | exact |
| `QuestBoard.Service/Areas/Platform/Controllers/UsersController.cs` (new) | controller | request-response / CRUD | `Areas/Platform/Controllers/GroupController.cs` (whole file, esp. lines 17-32, 267-274) | exact |
| `QuestBoard.Service/ViewModels/PlatformViewModels/PlatformUserViewModel.cs` (new) | model (view model) | transform | `ViewModels/PlatformViewModels/GroupListViewModel.cs` | exact |
| `QuestBoard.Service/Areas/Platform/Views/Users/Index.cshtml` (new) | component (Razor view) | request-response | `Areas/Platform/Views/Group/Index.cshtml` | exact |
| `QuestBoard.Service/Areas/Platform/Views/Users/Index.Mobile.cshtml` (new) | component (Razor view) | request-response | `Views/Admin/Users.Mobile.cshtml` (mobile card pattern) + `Group/Index.cshtml` (header) | role-match |
| `QuestBoard.Service/Areas/Platform/Views/Group/Index.cshtml` (header button added) | component (Razor view) | request-response | same file's own header bar (lines 6-15) | exact |
| `QuestBoard.Service/Areas/Platform/Views/Group/Index.Mobile.cshtml` (header button added) | component (Razor view) | request-response | `Group/Index.cshtml` header bar | exact |
| `QuestBoard.Service/Views/Admin/Users.cshtml` (Delete button/JS renamed) | component (Razor view) | request-response | same file (lines 169-172, 190-206) | exact |
| `QuestBoard.Service/Views/Admin/Users.Mobile.cshtml` (Delete button/JS renamed) | component (Razor view) | request-response | `Views/Admin/Users.cshtml` (mirrors desktop) | exact |
| `QuestBoard.Service/Controllers/Admin/AccountController.cs` (`Login` POST, `IsLockedOut` branch) | controller | request-response | same file (lines 122-147) | exact |
| `QuestBoard.Service/Program.cs` (`Configure<SecurityStampValidatorOptions>`) | config | request-response (framework middleware config) | same file (lines 45-63, `AddIdentity` chain) | exact |

## Pattern Assignments

### `QuestBoard.Service/Controllers/Admin/AdminController.cs` — `DeleteUser` (controller, CRUD)

**Analog:** `Areas/Platform/Controllers/GroupController.cs` lines 267-274 (`RemoveMember`)

**Current code to replace** (`AdminController.cs` lines 339-358):
```csharp
[HttpDelete]
[ValidateAntiForgeryToken]
public async Task<IActionResult> DeleteUser(int id)
{
    var groupId = activeGroupContext.ActiveGroupId;
    if (groupId == null) return NotFound();

    // Guard against a crafted request for a user outside the active group
    var targetRole = await userService.GetGroupRoleByIdAsync(id, groupId.Value);
    if (targetRole == null) return NotFound();

    var user = await userService.GetByIdAsync(id);
    if (user == null)
    {
        return NotFound();
    }

    await userService.RemoveAsync(user);   // <-- REPLACE
    return Ok();
}
```

**Core pattern to copy** (`GroupController.cs` lines 267-274 — the identical primitive):
```csharp
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> RemoveMember(int id, int userId, string? search, string? memberSearch)
{
    await groupService.RemoveMemberAsync(id, userId);
    TempData["Success"] = "Member removed from the group.";
    return RedirectToAction(nameof(Members), new { id, search, memberSearch });
}
```

**Replacement line for `DeleteUser`:** swap `await userService.RemoveAsync(user);` for `await groupService.RemoveMemberAsync(groupId.Value, id);` — keep the existing `[HttpDelete]` signature, `return Ok();`, and the two group-scoping guard checks above it unchanged (D-01). `groupService` (`IGroupService`) is already a constructor parameter on `AdminController` (line 21) — no DI change needed.

---

### `QuestBoard.Domain/Interfaces/IIdentityService.cs` + `QuestBoard.Repository/IdentityService.cs` — 3 new methods (service, CRUD)

**Analog:** `IdentityService.cs` `ConfirmEmailDirectlyAsync` (lines 141-149) — thin `FindByIdAsync` + `UserManager` wrapper shape used uniformly across the file.

**Imports already present** (no new imports needed in either file):
```csharp
// IdentityService.cs
using QuestBoard.Domain.Interfaces;
using QuestBoard.Repository.Entities;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;
```

**Core pattern to copy exactly:**
```csharp
/// <inheritdoc/>
public async Task<IdentityResult> ConfirmEmailDirectlyAsync(int userId)
{
    var entity = await userManager.FindByIdAsync(userId.ToString());
    if (entity == null)
        return IdentityResult.Failed(new IdentityError { Description = "User not found." });

    entity.EmailConfirmed = true;
    return await userManager.UpdateAsync(entity);
}
```

**New methods, following the identical shape** (see RESEARCH.md Pattern 2 for full text — reproduced here as the load-bearing excerpt):
```csharp
public async Task<IdentityResult> DisableUserAsync(int userId)
{
    var entity = await userManager.FindByIdAsync(userId.ToString());
    if (entity == null)
        return IdentityResult.Failed(new IdentityError { Description = "User not found." });

    await userManager.SetLockoutEndDateAsync(entity, DateTimeOffset.MaxValue);
    await userManager.UpdateSecurityStampAsync(entity); // D-10 — invalidate active session
    return IdentityResult.Success;
    // Do NOT set entity.LockoutEnabled here — D-09 deliberately leaves it untouched.
}

public async Task<IdentityResult> EnableUserAsync(int userId)
{
    var entity = await userManager.FindByIdAsync(userId.ToString());
    if (entity == null)
        return IdentityResult.Failed(new IdentityError { Description = "User not found." });

    await userManager.SetLockoutEndDateAsync(entity, null);
    return IdentityResult.Success;
    // D-12: no SecurityStamp bump on enable.
}

public async Task<DateTimeOffset?> GetLockoutEndAsync(int userId)
{
    var entity = await userManager.FindByIdAsync(userId.ToString());
    return entity?.LockoutEnd;
}
```

**Interface additions** — mirror the existing XML-doc-per-method convention in `IIdentityService.cs` (each method has a `/// <summary>` block, see lines 12-107 for the exact style to match).

**Error handling pattern:** All existing `IdentityService` methods return `IdentityResult.Failed(new IdentityError { Description = "..." })` on a null-entity guard — no try/catch anywhere in this file; `IdentityResult` itself is the error channel. Follow this — do not introduce exception handling in the new methods.

---

### `QuestBoard.Service/Areas/Platform/Controllers/UsersController.cs` (new controller, request-response/CRUD)

**Analog:** `Areas/Platform/Controllers/GroupController.cs` (whole file structure; class-level attributes lines 17-24, `Index` action lines 27-32, `RemoveMember` mutating-action shape lines 267-274)

**Imports pattern** (`GroupController.cs` lines 1-13, trim to what's actually used):
```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Service.ViewModels.PlatformViewModels;

namespace QuestBoard.Service.Areas.Platform.Controllers;
```

**Class/attribute + constructor pattern** (`GroupController.cs` lines 17-24):
```csharp
[Area("Platform")]
[Authorize(Policy = "SuperAdminOnly")]
public class UsersController(IUserService userService, IIdentityService identityService) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var users = await userService.GetAllAsync();
        var viewModels = new List<PlatformUserViewModel>();
        foreach (var user in users)
        {
            var lockoutEnd = await identityService.GetLockoutEndAsync(user.Id);
            viewModels.Add(new PlatformUserViewModel
            {
                User = user,
                IsDisabled = lockoutEnd == DateTimeOffset.MaxValue
            });
        }
        return View(viewModels);
    }
}
```

**Mutating-action pattern to copy** (`GroupController.cs` `RemoveMember`, lines 267-274 — same `[HttpPost] + [ValidateAntiForgeryToken] + TempData["Success"] + RedirectToAction(nameof(Index))` shape):
```csharp
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> Disable(int userId)
{
    var currentUserId = await identityService.GetUserIdAsync(User); // prefer existing method over raw ClaimTypes lookup (RESEARCH.md Open Question 1)
    if (currentUserId == userId)
    {
        TempData["Error"] = "You cannot disable your own account.";
        return RedirectToAction(nameof(Index));
    }
    await identityService.DisableUserAsync(userId);
    TempData["Success"] = "Account disabled.";
    return RedirectToAction(nameof(Index));
}

[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> Enable(int userId)
{
    await identityService.EnableUserAsync(userId);
    TempData["Success"] = "Account re-enabled.";
    return RedirectToAction(nameof(Index));
}
```

**Auth pattern:** `[Authorize(Policy = "SuperAdminOnly")]` at class level — identical to `GroupController.cs` line 18, no per-action attribute needed.

---

### `QuestBoard.Service/ViewModels/PlatformViewModels/PlatformUserViewModel.cs` (new, model)

**Analog:** `ViewModels/PlatformViewModels/GroupListViewModel.cs` (plain POCO view model, `using QuestBoard.Domain.Models;` + one collection/property, no logic)
```csharp
using QuestBoard.Domain.Models;

namespace QuestBoard.Service.ViewModels.PlatformViewModels;

public class GroupListViewModel
{
    public IList<GroupWithMemberCount> Groups { get; set; } = [];
}
```
New shape to follow:
```csharp
using QuestBoard.Domain.Models;

namespace QuestBoard.Service.ViewModels.PlatformViewModels;

public class PlatformUserViewModel
{
    public required User User { get; set; }
    public bool IsDisabled { get; set; }
}
```

---

### `QuestBoard.Service/Areas/Platform/Views/Users/Index.cshtml` (new) + `Index.Mobile.cshtml` (new)

**Analog:** `Areas/Platform/Views/Group/Index.cshtml` (header bar, alert blocks, table scaffold) — copy the entire `modern-card` shell verbatim.

**Header + card shell to copy** (`Group/Index.cshtml` lines 1-34):
```html
@model GroupListViewModel
@{
    ViewData["Title"] = "Group Management";
}

<div class="card modern-card">
    <div class="card-header modern-card-header d-flex justify-content-between align-items-center">
        <h2 class="mb-0">
            <i class="fas fa-layer-group text-danger me-2"></i>
            Group Management
        </h2>
        <a asp-controller="Group" asp-action="Create" asp-area="Platform" class="btn btn-success">
            <i class="fas fa-plus me-2"></i>Create Group
        </a>
    </div>
    <div class="card-body modern-card-body">
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
For this new view, use `@model IEnumerable<PlatformUserViewModel>` (matching `Views/Admin/Users.cshtml`'s `@model IEnumerable<UserManagementViewModel>` pattern, line 4), header title `"User Accounts"` / `fas fa-users-cog text-danger me-2` (no create button in this header, per UI-SPEC), and the action-button convention block already specified verbatim in `41-UI-SPEC.md` lines 145-179 (Status badge + Disable/Enable form markup — copy directly from there, it's already codebase-conformant).

**Antiforgery token injection pattern** (`Views/Admin/Users.cshtml` lines 1-8 — needed if using JS `fetch()`/`confirm()` for Disable, though the UI-SPEC's recommended markup uses plain `<form method="post">` + inline `onclick="return confirm(...)"`, which needs no injected token block at all — just standard Razor `asp-antiforgery` via `<form>` tag helper, matching `Group/Index.cshtml`'s `Delete` form pattern, not `Users.cshtml`'s `fetch()` pattern).

**Empty-state pattern** (`Views/Admin/Users.cshtml` lines 180-186):
```html
<div class="text-center py-5">
    <i class="fas fa-users fa-3x text-muted mb-3"></i>
    <p class="text-muted">No users found.</p>
</div>
```

**Mobile variant analog:** No existing `Areas/Platform/Views/Group/Index.Mobile.cshtml` content was read in this pass, but `Views/Admin/Users.Mobile.cshtml` (sibling of `Users.cshtml`) is the correct per-user stacked-card analog referenced in UI-SPEC line 139 ("mirrors `Users.Mobile.cshtml`'s existing per-user card block"). Planner/executor should read that file directly when implementing — not duplicated here to avoid an unnecessary third full-file read at pattern-mapping time.

---

### `QuestBoard.Service/Areas/Platform/Views/Group/Index.cshtml` + `Index.Mobile.cshtml` — header button addition

**Analog:** the file's own existing header bar (self-pattern, lines 6-15) — add one sibling `<a>` tag, same `d-flex justify-content-between` row:
```html
<div class="card-header modern-card-header d-flex justify-content-between align-items-center">
    <h2 class="mb-0">
        <i class="fas fa-layer-group text-danger me-2"></i>
        Group Management
    </h2>
    <a asp-controller="Group" asp-action="Create" asp-area="Platform" class="btn btn-success">
        <i class="fas fa-plus me-2"></i>Create Group
    </a>
    <!-- NEW per D-06 -->
    <a asp-controller="Users" asp-action="Index" asp-area="Platform" class="btn btn-secondary">
        <i class="fas fa-users-cog me-2"></i>Manage Users
    </a>
</div>
```
Note: this pushes the header bar to 3 flex children instead of 2 — verify visually against UI-SPEC's `d-flex justify-content-between` contract (spacing/wrap behavior at narrow viewport widths is a `Manage Users`-button-specific check, not covered by any existing 3-button header in the codebase today).

---

### `QuestBoard.Service/Views/Admin/Users.cshtml` + `Users.Mobile.cshtml` — Delete button rename (D-02)

**Analog:** the file's own current Delete button + `deleteUser()` JS (lines 168-172, 190-206) — this is a targeted edit, not a new-analog search.

**Current code to change:**
```html
@* Delete User button - last *@
<button type="button" class="btn btn-danger btn-sm" onclick="deleteUser(@userModel.User.Id)">
    <i class="fas fa-trash"></i>
    Delete
</button>
```
```javascript
function deleteUser(id) {
    if (confirm("Are you sure you want to delete this user? This action cannot be undone.")) {
        fetch(`/Admin/DeleteUser/${id}`, {
            method: "DELETE",
            headers: {
                'RequestVerificationToken': '@tokens.RequestToken'
            }
        }).then(res => {
            if (res.ok) {
                location.reload();
            } else {
                alert("Delete failed.");
            }
        });
    }
}
```
**New copy (locked, D-02 + UI-SPEC):** button label `"Remove from Group"`, icon `fa-user-minus` (not `fa-trash`), confirm string exactly `"Are you sure you want to remove this user from the group? They will keep their account and any other group memberships."`. Keep the `fetch()` + `DELETE /Admin/DeleteUser/${id}` + antiforgery-token-header shape unchanged — only button/JS-string copy and icon change; the HTTP verb/route are untouched by D-01/D-02 since the controller action itself keeps `[HttpDelete]`.

---

### `QuestBoard.Service/Controllers/Admin/AccountController.cs` — `Login` POST, `IsLockedOut` branch (D-13)

**Analog:** the file's own current branch (lines 122-147) plus its own already-injected `identityService` (constructor line 16) and its own existing use of `identityService.GetIdByEmailAsync` (line 45, in `ForgotPassword`).

**Current code to change:**
```csharp
if (result.IsLockedOut)
{
    ModelState.AddModelError(string.Empty, "Account locked due to too many failed attempts. Try again in 15 minutes.");
    return View(model);
}
```
**Replacement pattern (per RESEARCH.md Pitfall 4, using the codebase's own established `GetIdByEmailAsync` call shape from line 45):**
```csharp
if (result.IsLockedOut)
{
    var userId = await identityService.GetIdByEmailAsync(model.Email);
    var lockoutEnd = userId.HasValue ? await identityService.GetLockoutEndAsync(userId.Value) : null;

    ModelState.AddModelError(string.Empty, lockoutEnd == DateTimeOffset.MaxValue
        ? "This account has been disabled. Contact an administrator."
        : "Account locked due to too many failed attempts. Try again in 15 minutes.");
    return View(model);
}
```
No constructor/DI change needed — `identityService` is already a parameter (line 16).

---

### `QuestBoard.Service/Program.cs` — `SecurityStampValidatorOptions` (D-11)

**Analog:** the file's own existing `AddIdentity<UserEntity, IdentityRole<int>>(...)` configuration block (lines 45-63) — add a new standalone `Configure<>` call immediately after `.AddDefaultTokenProviders();` (line 63).

**Existing block for placement reference:**
```csharp
builder.Services.AddIdentity<UserEntity, IdentityRole<int>>(options =>
{
    // ... (lines 46-57 existing password/user options, unchanged)
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Lockout.AllowedForNewUsers = true;
    // ...
})
// ...
.AddDefaultTokenProviders();
```
**New block to add directly after:**
```csharp
builder.Services.Configure<SecurityStampValidatorOptions>(options =>
{
    options.ValidationInterval = TimeSpan.FromMinutes(5);
});
```

## Shared Patterns

### `SuperAdminOnly` Platform-area gating
**Source:** `Areas/Platform/Controllers/GroupController.cs` lines 17-18
```csharp
[Area("Platform")]
[Authorize(Policy = "SuperAdminOnly")]
```
**Apply to:** `UsersController.cs` (new) class declaration — identical, no per-action `[Authorize]` needed.

### `[ValidateAntiForgeryToken]` on every mutating POST
**Source:** every `[HttpPost]` action in `GroupController.cs` (e.g. lines 38-39, 143-144, 267-268) and `AdminController.cs` (line 340)
**Apply to:** `UsersController.Disable`, `UsersController.Enable` — both new POST actions must carry `[ValidateAntiForgeryToken]`.

### TempData flash-message + `RedirectToAction(nameof(Index))` after mutation
**Source:** `GroupController.cs` `RemoveMember` (lines 267-274), `AddMember` (lines 143-163)
```csharp
TempData["Success"] = "Member removed from the group.";
return RedirectToAction(nameof(Members), new { id, search, memberSearch });
```
**Apply to:** `UsersController.Disable`/`Enable` — use `TempData["Success"]`/`TempData["Error"]` + `RedirectToAction(nameof(Index))` (no route params needed since `Index` takes none).

### `modern-card` / `modern-card-header` / `modern-card-body` Razor shell
**Source:** `Group/Index.cshtml` lines 6-16, `Views/Admin/Users.cshtml` lines 10-20 — identical shell in both.
**Apply to:** `Areas/Platform/Views/Users/Index.cshtml` (new) — copy the shell verbatim, changing only header icon/title and omitting the create-action button (per UI-SPEC layout notes).

### Thin `IIdentityService` wrapper method shape (`FindByIdAsync` → guard → `UserManager` call)
**Source:** `QuestBoard.Repository/IdentityService.cs`, every existing method (e.g. lines 11-16, 141-149, 152-156).
**Apply to:** all 3 new `IIdentityService`/`IdentityService` methods (`DisableUserAsync`, `EnableUserAsync`, `GetLockoutEndAsync`) — no try/catch, `IdentityResult.Failed(new IdentityError { Description = "..." })` for the not-found case.

## No Analog Found

None — every file in scope has a direct or near-direct analog already in the codebase; this phase is explicitly "wire existing primitives together" per RESEARCH.md's own framing.

## Metadata

**Analog search scope:** `QuestBoard.Service/Controllers/Admin/`, `QuestBoard.Service/Areas/Platform/Controllers/`, `QuestBoard.Service/Areas/Platform/Views/Group/`, `QuestBoard.Service/Views/Admin/`, `QuestBoard.Repository/IdentityService.cs`, `QuestBoard.Domain/Interfaces/IIdentityService.cs`, `QuestBoard.Service/ViewModels/PlatformViewModels/`, `QuestBoard.Service/Program.cs`
**Files scanned:** 9 read directly (full or targeted ranges) + CONTEXT.md/RESEARCH.md/UI-SPEC.md already contain verified line-cited excerpts for the remainder (`Group/Index.Mobile.cshtml`, `Users.Mobile.cshtml`, full `AdminController.cs`) reused rather than re-read
**Pattern extraction date:** 2026-07-04
