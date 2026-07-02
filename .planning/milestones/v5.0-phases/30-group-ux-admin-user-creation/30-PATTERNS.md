# Phase 30: Group UX & Admin User Creation — Pattern Map

**Mapped:** 2026-06-30
**Files analyzed:** 13 (new/modified)
**Analogs found:** 13 / 13

---

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|---------------|
| `QuestBoard.Service/Controllers/GroupPickerController.cs` | controller | request-response | `QuestBoard.Service/Areas/Platform/Controllers/GroupController.cs` | role-match |
| `QuestBoard.Service/Views/GroupPicker/Index.cshtml` | view | request-response | `QuestBoard.Service/Areas/Platform/Views/Group/Index.cshtml` | role-match |
| `QuestBoard.Service/Views/GroupPicker/Index.Mobile.cshtml` | view | request-response | `QuestBoard.Service/Views/Account/Login.Mobile.cshtml` | role-match |
| `QuestBoard.Service/Views/Shared/_Layout.GroupPicker.cshtml` | layout | request-response | `QuestBoard.Service/Areas/Platform/Views/Shared/_Layout.Platform.cshtml` | exact |
| `QuestBoard.Service/Views/Admin/CreateUser.cshtml` | view | request-response | `QuestBoard.Service/Views/Admin/EditUser.cshtml` | exact |
| `QuestBoard.Service/Views/Admin/CreateUser.Mobile.cshtml` | view | request-response | `QuestBoard.Service/Views/Account/Login.Mobile.cshtml` | role-match |
| `QuestBoard.Service/ViewModels/AdminViewModels/CreateUserViewModel.cs` | model | request-response | `QuestBoard.Service/ViewModels/AccountViewModels/RegisterViewModel.cs` | exact |
| `QuestBoard.Service/ViewModels/GroupPickerViewModels/GroupPickerViewModel.cs` | model | request-response | `QuestBoard.Service/ViewModels/PlatformViewModels/GroupListViewModel.cs` | role-match |
| `QuestBoard.Service/Constants/SessionKeys.cs` | config | — | self (modify) | exact |
| `QuestBoard.Service/Controllers/Admin/AccountController.cs` | controller | request-response | self (modify) | exact |
| `QuestBoard.Service/Controllers/Admin/AdminController.cs` | controller | request-response | self (modify) | exact |
| `QuestBoard.Service/Views/Shared/_Layout.cshtml` | layout | request-response | self (modify) | exact |
| `QuestBoard.Service/Views/Shared/_Layout.Mobile.cshtml` | layout | request-response | self (modify) | exact |

---

## Pattern Assignments

### `GroupPickerController.cs` (controller, request-response)

**Analog:** `QuestBoard.Service/Areas/Platform/Controllers/GroupController.cs`

**Imports pattern** (lines 1–8 of GroupController.cs):
```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.Service.ViewModels.PlatformViewModels;

namespace QuestBoard.Service.Areas.Platform.Controllers;
```
For GroupPickerController, replace area namespace and ViewModel namespace:
```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Service.Constants;
using QuestBoard.Service.ViewModels.GroupPickerViewModels;

namespace QuestBoard.Service.Controllers;
```

**Auth pattern** (line 12 of GroupController.cs — adapt for non-policy):
```csharp
// GroupController uses [Authorize(Policy = "SuperAdminOnly")]
// GroupPickerController must use [Authorize] only — accessible to any authenticated user
[Authorize]
public class GroupPickerController(IGroupService groupService) : Controller
```

**Core pattern** — Index GET auto-redirect or show picker:
```csharp
[HttpGet]
public async Task<IActionResult> Index(string? returnUrl = null)
{
    var isSuperAdmin = User.IsInRole("SuperAdmin");

    IList<GroupWithMemberCount> groups;
    if (isSuperAdmin)
        groups = await groupService.GetAllWithMemberCountAsync();
    else
    {
        // Requires new GetGroupsForUserAsync(userId) — see Pitfall 5 in RESEARCH.md
        var userId = /* parse from User claims */;
        groups = await groupService.GetGroupsForUserAsync(userId);
    }

    if (!isSuperAdmin && groups.Count == 0)
        return View(new GroupPickerViewModel { Groups = [], IsSuperAdmin = false, HasNoGroups = true });

    if (!isSuperAdmin && groups.Count == 1)
    {
        HttpContext.Session.SetInt32(SessionKeys.ActiveGroupId, groups[0].Id);
        HttpContext.Session.SetString(SessionKeys.ActiveGroupName, groups[0].Name);
        return Redirect(Url.IsLocalUrl(returnUrl) ? returnUrl! : Url.Action("Index", "Home")!);
    }

    return View(new GroupPickerViewModel { Groups = groups, IsSuperAdmin = isSuperAdmin, ReturnUrl = returnUrl });
}
```

**SelectGroup POST pattern** — mirrors promote/demote pattern from AdminController.cs (lines 54–61):
```csharp
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> SelectGroup(int groupId, string? returnUrl = null)
{
    var group = await groupService.GetByIdAsync(groupId);
    if (group == null) return NotFound();

    HttpContext.Session.SetInt32(SessionKeys.ActiveGroupId, group.Id);
    HttpContext.Session.SetString(SessionKeys.ActiveGroupName, group.Name);
    return Redirect(Url.IsLocalUrl(returnUrl) ? returnUrl! : Url.Action("Index", "Home")!);
}
```

---

### `Views/GroupPicker/Index.cshtml` (view, request-response)

**Analog:** `QuestBoard.Service/Areas/Platform/Views/Group/Index.cshtml`

**Card shell pattern** (lines 6–13 of Group/Index.cshtml):
```cshtml
<div class="card modern-card">
    <div class="card-header modern-card-header">
        <h2 class="mb-0">
            <i class="fas fa-layer-group text-danger me-2"></i>
            Group Management
        </h2>
    </div>
    <div class="card-body modern-card-body">
```

**TempData alert pattern** (lines 14–31 of Group/Index.cshtml):
```cshtml
@if (TempData["Success"] != null)
{
    <div class="alert alert-success alert-dismissible fade show" role="alert">
        <i class="fas fa-check-circle me-2"></i>
        @TempData["Success"]
        <button type="button" class="btn-close" data-bs-dismiss="alert" aria-label="Close"></button>
    </div>
}
```

**Group card grid pattern** (new — no exact analog; use Bootstrap row/col-auto or CSS grid):
```cshtml
@* One form per group card — POST with anti-forgery token on card click *@
<div class="row row-cols-1 row-cols-md-2 row-cols-lg-3 g-3 mb-4">
    @foreach (var group in Model.Groups)
    {
        <div class="col">
            <form asp-action="SelectGroup" method="post">
                @Html.AntiForgeryToken()
                <input type="hidden" name="groupId" value="@group.Id" />
                <input type="hidden" name="returnUrl" value="@Model.ReturnUrl" />
                <button type="submit" class="btn w-100 p-0 border-0 text-start">
                    <div class="card modern-card h-100">
                        <div class="card-body modern-card-body">
                            <h5 class="card-title">@group.Name</h5>
                            <p class="card-text text-muted">
                                <i class="fas fa-users me-1"></i>@group.MemberCount member(s)
                            </p>
                        </div>
                    </div>
                </button>
            </form>
        </div>
    }
</div>
@if (Model.IsSuperAdmin)
{
    <hr>
    <a href="/platform" class="btn btn-secondary">
        <i class="fas fa-cog me-2"></i>Go to Platform
    </a>
}
@if (Model.HasNoGroups)
{
    <div class="alert alert-warning">
        <i class="fas fa-exclamation-triangle me-2"></i>
        Your account is not assigned to any group. Please contact your administrator.
    </div>
}
```

**Layout directive** — stripped-down picker layout (see _Layout.GroupPicker.cshtml below):
```cshtml
@{
    ViewData["Title"] = "Select Group";
    Layout = "~/Views/Shared/_Layout.GroupPicker.cshtml";
}
```

---

### `Views/GroupPicker/Index.Mobile.cshtml` (view, request-response)

**Analog:** `QuestBoard.Service/Views/Account/Login.Mobile.cshtml`

**Mobile view structure** (full Login.Mobile.cshtml — 45 lines):
```cshtml
@section Styles {
    <link href="~/css/account.mobile.css" asp-append-version="true" rel="stylesheet" />
}

<div class="account-card-mobile mb-3">
    <h5 class="mb-3"><i class="fas fa-sign-in-alt text-warning me-2"></i>Log in</h5>
    ...
</div>
```
For GroupPicker mobile: same `account-card-mobile` container, cards stack naturally on mobile (single column by default from Bootstrap `row-cols-1`). Share the same group card form pattern from Index.cshtml.

**Layout directive:**
```cshtml
@{
    Layout = "~/Views/Shared/_Layout.GroupPicker.cshtml";
}
```

---

### `Views/Shared/_Layout.GroupPicker.cshtml` (layout, request-response)

**Analog:** `QuestBoard.Service/Areas/Platform/Views/Shared/_Layout.Platform.cshtml` (lines 1–50)

**Full stripped-down layout pattern** (lines 1–50 of _Layout.Platform.cshtml):
```cshtml
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>@ViewData["Title"] - D&D Quest Board Platform</title>
    <link href="https://cdn.jsdelivr.net/npm/bootstrap@5.3.0/dist/css/bootstrap.min.css" rel="stylesheet">
    <link href="https://cdnjs.cloudflare.com/ajax/libs/font-awesome/6.4.0/css/all.min.css" rel="stylesheet">
    <link href="https://fonts.googleapis.com/css2?family=Cinzel:wght@400;500;600;700&display=swap" rel="stylesheet">
    <link rel="stylesheet" href="~/css/site.css" asp-append-version="true" />
</head>
<body class="d-flex flex-column min-vh-100">

<nav class="navbar navbar-expand-lg navbar-dark bg-dark py-2">
    <div class="container-fluid px-3">
        <a class="navbar-brand" asp-controller="Group" asp-action="Index" asp-area="Platform">
            <i class="fas fa-dice-d20"></i> D&D Quest Board
        </a>
        <ul class="navbar-nav ms-auto d-flex flex-row align-items-center gap-3">
            <li class="nav-item">
                <span class="navbar-text text-light">
                    <i class="fas fa-user me-1"></i>@User.Identity?.Name
                </span>
            </li>
            <li class="nav-item">
                <form asp-controller="Account" asp-action="Logout" asp-area="" method="post" class="d-inline">
                    <button type="submit" class="btn btn-outline-light btn-sm">
                        <i class="fas fa-sign-out-alt me-1"></i>Logout
                    </button>
                </form>
            </li>
        </ul>
    </div>
</nav>

<div class="container mt-3 flex-grow-1">
    <main role="main">
        @RenderBody()
    </main>
</div>

<script src="https://cdnjs.cloudflare.com/ajax/libs/jquery/3.6.0/jquery.min.js"></script>
<script src="https://cdn.jsdelivr.net/npm/bootstrap@5.3.0/dist/js/bootstrap.bundle.min.js"></script>
<script src="~/js/site.js" asp-append-version="true"></script>
@await RenderSectionAsync("Scripts", required: false)
```
For GroupPicker layout: copy the same structure. Change navbar brand link to `asp-controller="Home" asp-action="Index" asp-area=""`. Remove the "Back to quest board" link (user is not yet in a group). Omit all nav items — this is a pre-group-context page.

---

### `Views/Admin/CreateUser.cshtml` (view, request-response)

**Analog:** `QuestBoard.Service/Views/Admin/EditUser.cshtml` (lines 1–79)

**Full form pattern** (full EditUser.cshtml):
```cshtml
@model EditUserViewModel
@{
    ViewData["Title"] = "Edit User";
}

<div class="row justify-content-center">
    <div class="col-md-6">
        <div class="card modern-card">
            <div class="card-header modern-card-header">
                <h2 class="mb-0">
                    <i class="fas fa-user-edit text-warning me-2"></i>
                    @ViewData["Title"]
                </h2>
            </div>
            <div class="card-body modern-card-body">
                <form asp-action="EditUser" method="post">
                    <div asp-validation-summary="ModelOnly" class="text-danger mb-3"></div>
                    <input asp-for="Id" type="hidden" />
                    <div class="mb-3">
                        <label asp-for="Name" class="form-label"></label>
                        <input asp-for="Name" class="form-control" />
                        <span asp-validation-for="Name" class="text-danger"></span>
                    </div>
                    <div class="mb-3">
                        <label asp-for="Email" class="form-label"></label>
                        <input asp-for="Email" class="form-control" type="email" />
                        <span asp-validation-for="Email" class="text-danger"></span>
                    </div>
                    <hr>
                    <div class="d-flex justify-content-between">
                        <a asp-action="Users" class="btn btn-secondary">
                            <i class="fas fa-arrow-left me-2"></i>Back to Users
                        </a>
                        <button type="submit" class="btn btn-success">
                            <i class="fas fa-save me-2"></i>Save Changes
                        </button>
                    </div>
                </form>
            </div>
        </div>
    </div>
</div>
```
For CreateUser: change action to `asp-action="CreateUser"`, remove `Id` hidden field, add `Password` field (`type="password"`), add `GroupRole` select dropdown (`<select asp-for="GroupRole" asp-items="Html.GetEnumSelectList<GroupRole>()" class="form-select">`). Change icon to `fa-user-plus`, button text to "Create User", button color to `btn-warning`.

---

### `Views/Admin/CreateUser.Mobile.cshtml` (view, request-response)

**Analog:** `QuestBoard.Service/Views/Account/Login.Mobile.cshtml`

**Mobile form pattern** (full Login.Mobile.cshtml — 45 lines):
```cshtml
<div class="account-card-mobile mb-3">
    <h5 class="mb-3"><i class="fas fa-user-plus text-warning me-2"></i>Create User</h5>
    <div asp-validation-summary="ModelOnly" class="text-danger mb-3"></div>
    <form asp-action="CreateUser" method="post">
        @Html.AntiForgeryToken()
        <!-- same fields as CreateUser.cshtml in mobile-friendly layout -->
    </form>
</div>
```

---

### `ViewModels/AdminViewModels/CreateUserViewModel.cs` (model, request-response)

**Analog:** `QuestBoard.Service/ViewModels/AccountViewModels/RegisterViewModel.cs` (full file)

**ViewModel pattern** (full RegisterViewModel.cs):
```csharp
using System.ComponentModel.DataAnnotations;

namespace QuestBoard.Service.ViewModels.AccountViewModels;

public class RegisterViewModel
{
    [Required]
    [StringLength(100)]
    [Display(Name = "Name")]
    public string Name { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(100, ErrorMessage = "...", MinimumLength = 8)]
    [DataType(DataType.Password)]
    [Display(Name = "Password")]
    public string Password { get; set; } = string.Empty;
    ...
}
```
For CreateUserViewModel: same namespace root `QuestBoard.Service.ViewModels.AdminViewModels`. Drop `ConfirmPassword` and `IsDungeonMaster`. Add `GroupRole` property:
```csharp
using QuestBoard.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace QuestBoard.Service.ViewModels.AdminViewModels;

public class CreateUserViewModel
{
    [Required][EmailAddress][Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [Required][StringLength(100)][Display(Name = "Display Name")]
    public string Name { get; set; } = string.Empty;

    [Required][StringLength(100, MinimumLength = 8)][DataType(DataType.Password)][Display(Name = "Password")]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Group Role")]
    public GroupRole GroupRole { get; set; } = GroupRole.Player;
}
```

---

### `ViewModels/GroupPickerViewModels/GroupPickerViewModel.cs` (model, request-response)

**Analog:** `QuestBoard.Service/ViewModels/PlatformViewModels/GroupListViewModel.cs`

**Analog pattern** (read from GroupListViewModel — contains `IList<GroupWithMemberCount> Groups`):
```csharp
namespace QuestBoard.Service.ViewModels.GroupPickerViewModels;

public class GroupPickerViewModel
{
    public IList<GroupWithMemberCount> Groups { get; set; } = [];
    public bool IsSuperAdmin { get; set; }
    public bool HasNoGroups { get; set; }
    public string? ReturnUrl { get; set; }
}
```
Note: `GroupWithMemberCount` is in `QuestBoard.Domain.Models` — same using as `GroupListViewModel`.

---

### `Constants/SessionKeys.cs` (config, modify)

**Current file** (full, 9 lines):
```csharp
namespace QuestBoard.Service.Constants;

public static class SessionKeys
{
    public const string ActiveGroupId = "ActiveGroupId";
}
```
**Add** one constant:
```csharp
public const string ActiveGroupName = "ActiveGroupName";  // Phase 30: stored by SelectGroup POST
```

---

### `Controllers/Admin/AccountController.cs` (controller, modify)

**Login POST change** — replace `RedirectToLocal(returnUrl)` on success (line 67):
```csharp
// BEFORE (line 67):
return RedirectToLocal(returnUrl);

// AFTER (D-01):
return RedirectToAction("Index", "GroupPicker", new { returnUrl });
```

**Remove Register GET** (lines 82–87) and **Register POST** (lines 89–122) entirely.

**Pattern for return** on Register POST (lines 96–112 — mirrors CreateUser POST structure):
```csharp
var result = await userService.CreateAsync(model.Email, model.Name, model.Password);
if (result.Succeeded)
{
    var userId = await identityService.GetIdByEmailAsync(model.Email);
    if (userId.HasValue)
    {
        var rawToken = await identityService.GenerateEmailConfirmationAsync(userId.Value);
        if (rawToken != null)
        {
            var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(rawToken));
            var callbackUrl = Url.Action("ConfirmEmail", "Account",
                new { userId = userId.Value, token = encodedToken }, Request.Scheme);
            jobClient.Enqueue<ConfirmationEmailJob>(
                j => j.ExecuteAsync(model.Email, model.Name, callbackUrl!, CancellationToken.None));
        }
    }
    return RedirectToLocal(returnUrl);
}
```
This is the exact block that `AdminController.CreateUser` POST should copy (adapting `RedirectToLocal` → `RedirectToAction(nameof(Users))`).

---

### `Controllers/Admin/AdminController.cs` (controller, modify)

**`??  1` fallback removal** — `Users()` method (line 32):
```csharp
// BEFORE (line 32):
GroupRole? groupRole = await userService.GetGroupRoleByIdAsync(user.Id, groupId ?? 1);

// AFTER (D-17):
var groupId = activeGroupContext.ActiveGroupId;
if (groupId == null) return RedirectToAction("Index", "GroupPicker");
// then use groupId.Value throughout:
GroupRole? groupRole = await userService.GetGroupRoleByIdAsync(user.Id, groupId.Value);
```

**CreateUser GET/POST pattern** — copy from `EditUser` (lines 94–155):
```csharp
// EditUser GET — lines 94–112
[HttpGet]
public async Task<IActionResult> EditUser(int userId)
{
    var user = await userService.GetByIdAsync(userId);
    if (user == null) return RedirectToAction(nameof(Users));
    var model = new EditUserViewModel { Id = user.Id, Name = user.Name, Email = user.Email, HasKey = user.HasKey };
    return View(model);
}

// EditUser POST — lines 114–155
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> EditUser(EditUserViewModel model)
{
    if (ModelState.IsValid)
    {
        ...
        return RedirectToAction(nameof(Users));
    }
    return View(model);
}
```
For `CreateUser` GET: return `View(new CreateUserViewModel())`. No pre-population needed.
For `CreateUser` POST: call `userService.CreateAsync` then `identityService.GetIdByEmailAsync` then `userService.SetGroupRoleAsync` then enqueue `ConfirmationEmailJob`. Set `TempData["Success"]`. Return `RedirectToAction(nameof(Users))` on success.

**ConfirmationEmailJob enqueueing pattern** (lines 228–239 of AdminController.cs — `SendConfirmationEmail`):
```csharp
var rawToken = await identityService.GenerateEmailConfirmationAsync(userId);
var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(rawToken));
var callbackUrl = Url.Action("ConfirmEmail", "Account", new { userId, token = encodedToken }, Request.Scheme);
jobClient.Enqueue<ConfirmationEmailJob>(j => j.ExecuteAsync(user.Email!, user.Name, callbackUrl!, CancellationToken.None));
TempData["Success"] = $"Confirmation email queued for {user.Name}.";
return RedirectToAction(nameof(Users));
```

---

### `Views/Shared/_Layout.cshtml` (layout, modify)

**Inject line at top** — mirror the existing `@using` on line 1:
```cshtml
@using QuestBoard.Domain.Interfaces
@inject Microsoft.AspNetCore.Http.IHttpContextAccessor HttpContextAccessor
```

**User dropdown modification** (lines 131–145 — add between Profile and Logout):
```cshtml
<ul class="dropdown-menu">
    <li>
        <a class="dropdown-item" asp-controller="Account" asp-action="Profile">
            <i class="fas fa-user-cog me-2"></i>Profile
        </a>
    </li>
    @{
        var activeGroupName = HttpContextAccessor.HttpContext?.Session?.GetString("ActiveGroupName");
    }
    @if (!string.IsNullOrEmpty(activeGroupName))
    {
        <li><hr class="dropdown-divider"></li>
        <li>
            <a class="dropdown-item" asp-controller="GroupPicker" asp-action="Index">
                <i class="fas fa-arrows-rotate me-2"></i>@activeGroupName
            </a>
        </li>
    }
    <li><hr class="dropdown-divider"></li>
    <li>
        <form asp-controller="Account" asp-action="Logout" method="post" class="d-inline">
            <button type="submit" class="dropdown-item">
                <i class="fas fa-sign-out-alt me-2"></i>Logout
            </button>
        </form>
    </li>
</ul>
```

---

### `Views/Shared/_Layout.Mobile.cshtml` (layout, modify)

**User profile / auth section** (lines 107–132) — add group switch link between Profile and Logout:
```cshtml
@if (User.Identity?.IsAuthenticated == true)
{
    var currentUser = await UserService.GetUserAsync(User);
    var activeGroupName = HttpContextAccessor.HttpContext?.Session?.GetString("ActiveGroupName");

    <li class="nav-item">
        <a class="nav-link" asp-controller="Account" asp-action="Profile">
            <i class="fas fa-user-cog me-2"></i>@currentUser.Name
        </a>
    </li>
    @if (!string.IsNullOrEmpty(activeGroupName))
    {
        <li class="nav-item">
            <a class="nav-link" asp-controller="GroupPicker" asp-action="Index">
                <i class="fas fa-arrows-rotate me-2"></i>@activeGroupName
            </a>
        </li>
    }
    <li class="nav-item">
        <form asp-controller="Account" asp-action="Logout" method="post">
            <button type="submit" class="nav-link btn btn-link text-start w-100"
                    data-bs-dismiss="offcanvas">
                <i class="fas fa-sign-out-alt me-2"></i>Logout
            </button>
        </form>
    </li>
}
```
Same `@inject Microsoft.AspNetCore.Http.IHttpContextAccessor HttpContextAccessor` needed at top.

---

## Shared Patterns

### Anti-Forgery Token on POST Forms
**Source:** `QuestBoard.Service/Controllers/Admin/AdminController.cs` (all POST actions, e.g. line 54)
**Apply to:** `GroupPickerController.SelectGroup` POST, `AdminController.CreateUser` POST
```csharp
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> SelectGroup(...)
```
In views:
```cshtml
<form asp-action="SelectGroup" method="post">
    @Html.AntiForgeryToken()
    ...
</form>
```

### TempData Success/Error Alerts
**Source:** `QuestBoard.Service/Areas/Platform/Views/Group/Index.cshtml` (lines 14–31)
**Apply to:** `Views/GroupPicker/Index.cshtml`, `Views/Admin/CreateUser.cshtml`
```cshtml
@if (TempData["Success"] != null)
{
    <div class="alert alert-success alert-dismissible fade show" role="alert">
        <i class="fas fa-check-circle me-2"></i>@TempData["Success"]
        <button type="button" class="btn-close" data-bs-dismiss="alert" aria-label="Close"></button>
    </div>
}
```

### Error Propagation from Identity Result
**Source:** `QuestBoard.Service/Controllers/Admin/AdminController.cs` (lines 193–198)
**Apply to:** `AdminController.CreateUser` POST
```csharp
foreach (var error in result.Errors)
{
    ModelState.AddModelError(string.Empty, error.Description);
}
return View(model);
```

### Null ActiveGroupId Guard + Redirect
**Source:** `QuestBoard.Service/Controllers/Admin/AdminController.cs` (lines 58–60)
**Apply to:** `AdminController.Users()` after `?? 1` removal
```csharp
var groupId = activeGroupContext.ActiveGroupId;
if (groupId == null) return RedirectToAction(nameof(Users));
// After Phase 30: redirect to GroupPicker instead:
if (groupId == null) return RedirectToAction("Index", "GroupPicker");
```

### Modern Card Form Layout — Button Row
**Source:** `QuestBoard.Service/Views/Admin/EditUser.cshtml` (lines 64–73)
**Apply to:** `Views/Admin/CreateUser.cshtml`
```cshtml
<hr>
<div class="d-flex justify-content-between">
    <a asp-action="Users" class="btn btn-secondary">
        <i class="fas fa-arrow-left me-2"></i>Back to Users
    </a>
    <button type="submit" class="btn btn-warning">
        <i class="fas fa-user-plus me-2"></i>Create User
    </button>
</div>
```

### RedirectToLocal Safety Check
**Source:** `QuestBoard.Service/Controllers/Admin/AccountController.cs` (lines 257–265)
**Apply to:** `GroupPickerController.SelectGroup` POST (replicate inline)
```csharp
private IActionResult RedirectToLocal(string? returnUrl)
{
    if (Url.IsLocalUrl(returnUrl))
        return Redirect(returnUrl);
    else
        return RedirectToAction(nameof(HomeController.Index), "Home");
}
```
In GroupPickerController, use inline:
```csharp
return Redirect(Url.IsLocalUrl(returnUrl) ? returnUrl! : Url.Action("Index", "Home")!);
```

---

## Files to Delete

| File | Reason |
|------|--------|
| `QuestBoard.Service/Views/Account/Register.cshtml` | D-09: self-registration removed |
| `QuestBoard.Service/Views/Account/Register.Mobile.cshtml` | D-19: mobile variant also removed |
| Register GET action in `AccountController.cs` (lines 82–87) | D-09 |
| Register POST action in `AccountController.cs` (lines 89–122) | D-09 |

---

## No Analog Found

All files have close analogs. No entries in this section.

---

## Metadata

**Analog search scope:** `QuestBoard.Service/Controllers/`, `QuestBoard.Service/Views/`, `QuestBoard.Service/ViewModels/`, `QuestBoard.Service/Areas/Platform/`, `QuestBoard.Service/Constants/`
**Files scanned:** 12
**Pattern extraction date:** 2026-06-30
