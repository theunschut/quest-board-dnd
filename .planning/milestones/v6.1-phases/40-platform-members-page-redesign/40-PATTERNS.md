# Phase 40: Platform Members Page Redesign - Pattern Map

**Mapped:** 2026-07-04
**Files analyzed:** 9 (2 new, 7 modified)
**Analogs found:** 9 / 9

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|--------------------|------|-----------|-----------------|----------------|
| `QuestBoard.Domain/Interfaces/IUserRepository.cs` (+`GetAvailableUsers`) | model/interface | CRUD (read) | `QuestBoard.Domain/Interfaces/IUserRepository.cs` — existing `GetAllGroupMembers` | exact (same file, sibling method) |
| `QuestBoard.Repository/UserRepository.cs` (+`GetAvailableUsers`) | service (repository) | CRUD (read) | `QuestBoard.Repository/UserRepository.cs:51-59` (`GetAllGroupMembers`) | exact |
| `QuestBoard.Domain/Interfaces/IUserService.cs` (+`GetAvailableUsersAsync`) | service/interface | CRUD (read) | `IUserService.cs:49` (`GetAllGroupMembersAsync`) | exact |
| `QuestBoard.Domain/Services/UserService.cs` (+`GetAvailableUsersAsync`) | service | CRUD (read) | same file — existing delegate methods around `GetAllGroupMembersAsync` | exact |
| `Areas/Platform/Controllers/GroupController.cs` (`Members` GET, `AddMember` POST adapted, new `CreateMember` POST) | controller | request-response / CRUD | `Controllers/Admin/AdminController.cs:113-190` (`CreateUser`) for the new action; `GroupController.cs` itself for `Members`/`AddMember`/`RemoveMember` shape | exact |
| `ViewModels/PlatformViewModels/GroupMembersViewModel.cs` (+`SearchQuery`, +`CreateMember`) | model (ViewModel) | request-response | same file (current shape) | exact |
| `ViewModels/PlatformViewModels/AddMemberViewModel.cs` (repurposed as per-row binding) | model (ViewModel) | request-response | same file (current shape) | exact |
| `ViewModels/PlatformViewModels/CreateMemberViewModel.cs` (new) | model (ViewModel) | request-response | `ViewModels/AdminViewModels/CreateUserViewModel.cs` | exact |
| `Areas/Platform/Views/Group/Members.cshtml` (two-column redesign) | component (Razor view) | request-response | `Views/Shop/Index.cshtml:201-283` (filter row) + `Views/ShopManagement/Index.cshtml:415-483` (modal) + `Views/Admin/CreateUser.cshtml` (form fields) + itself (members table/RemoveMember form) | exact (composite) |
| `Areas/Platform/Views/Group/Members.Mobile.cshtml` (stacked redesign) | component (Razor view) | request-response | `Areas/Platform/Views/Group/Members.Mobile.cshtml` (current) + `Views/ShopManagement/Index.Mobile.cshtml` (modal reuse) | exact |

## Pattern Assignments

### `QuestBoard.Repository/UserRepository.cs` (+`GetAvailableUsers`) (repository, CRUD-read)

**Analog:** `QuestBoard.Repository/UserRepository.cs:51-59` (`GetAllGroupMembers`)

**Imports** (already present at top of file, lines 1-6):
```csharp
using AutoMapper;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.Repository.Entities;
using Microsoft.EntityFrameworkCore;
```

**Core pattern to negate + extend** (`UserRepository.cs:51-59`):
```csharp
/// <inheritdoc/>
public async Task<IList<User>> GetAllGroupMembers(int groupId, CancellationToken token = default)
{
    // Membership is any UserGroups row for the group, regardless of role.
    var entities = await DbSet
        .Where(u => DbContext.UserGroups
            .Any(ug => ug.UserId == u.Id && ug.GroupId == groupId))
        .ToListAsync(cancellationToken: token);
    return Mapper.Map<IList<User>>(entities);
}
```
New method negates the `Any(...)` predicate and adds an optional search filter (see RESEARCH.md Pattern 1 for the exact negated+search-filtered shape — already fully worked out there, do not deviate). Place it adjacent to `GetAllGroupMembers` in the same file, matching its XML-doc `/// <inheritdoc/>` convention (doc lives on the interface, not the implementation).

**Note:** `GetAllGroupMembers` does NOT take `IActiveGroupContext` — it takes `groupId` as a plain parameter, unlike `GetAllDungeonMasters`/`GetAllPlayers` above it in the same file (lines 20-48) which do use the injected `activeGroupContext.ActiveGroupId`. The new `GetAvailableUsers` method must follow the `GetAllGroupMembers` shape (plain `groupId` parameter), not the `activeGroupContext` shape — this is the exact class of mistake flagged as the phase's top risk (never source `groupId` from session context).

---

### `Areas/Platform/Controllers/GroupController.cs` — `Members` GET (controller, request-response)

**Analog:** itself, current implementation (`GroupController.cs:114-130`)

**Imports** (current, `GroupController.cs:1-8`):
```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.Service.ViewModels.PlatformViewModels;
```

**Auth pattern** (class-level, `GroupController.cs:11-13`, unchanged):
```csharp
[Area("Platform")]
[Authorize(Policy = "SuperAdminOnly")]
public class GroupController(IGroupService groupService, IUserService userService) : Controller
```
The new `CreateMember` action needs additional constructor dependencies — see below (Pitfall: do NOT add `IActiveGroupContext`).

**Current `Members` action to replace** (`GroupController.cs:114-130`):
```csharp
[HttpGet]
public async Task<IActionResult> Members(int id)
{
    var group = await groupService.GetByIdAsync(id);
    if (group == null) return RedirectToAction(nameof(Index));
    var members = await groupService.GetMembersAsync(id);
    var allUsers = await userService.GetAllAsync();
    var memberUserIds = members.Select(m => m.UserId).ToHashSet();
    var availableUsers = allUsers.Where(u => !memberUserIds.Contains(u.Id)).ToList();
    return View(new GroupMembersViewModel
    {
        Group = group,
        Members = members,
        AvailableUsers = availableUsers,
        AddMember = new AddMemberViewModel()
    });
}
```
Replace the `userService.GetAllAsync()` + in-memory `.Where(...)` block entirely with `await userService.GetAvailableUsersAsync(id, search)`. Add a `string? search` parameter to the action signature and populate `SearchQuery` on the ViewModel (mirrors `Views/Shop`'s controller-side `SearchQuery` binding — see `Views/Shop/Index.cshtml:276` `value="@Model.SearchQuery"`).

**Current `AddMember` action to adapt for D-04** (`GroupController.cs:132-152`):
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
Remove the `[Bind(Prefix = "AddMember")]` attribute (per-row form posts `UserId`/`Role` as top-level fields now, per Pitfall 4 in RESEARCH.md). Add a `string? search` parameter and echo it in every `RedirectToAction(nameof(Members), new { id, search })` call — this is D-04, the easiest thing to forget (RESEARCH.md Pitfall 1).

**New `CreateMember` action — mirror `AdminController.CreateUser` exactly, minus `IActiveGroupContext`:**

Source: `Controllers/Admin/AdminController.cs:113-190` (full outcome switch, verbatim flash-message text):
```csharp
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> CreateMember(int id, CreateMemberViewModel model)
{
    if (!ModelState.IsValid)
    {
        // re-render Members with modal errors + fresh available-users list (no search re-applied unless carried through model)
    }

    var result = await userService.CreateOrAddToGroupAsync(model.Email, model.Name, id, model.GroupRole);

    switch (result.Outcome)
    {
        case CreateOrAddToGroupOutcome.NewAccountCreated:
            {
                var rawToken = await identityService.GeneratePasswordResetTokenForUserAsync(result.UserId!.Value);
                if (rawToken != null)
                {
                    var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(rawToken));
                    var callbackUrl = Url.Action("SetPassword", "Account", new { userId = result.UserId.Value, token = encodedToken }, Request.Scheme);
                    if (callbackUrl == null)
                        logger.LogError("Failed to generate SetPassword callback URL for userId {UserId}", result.UserId.Value);
                    else
                        jobClient.Enqueue<WelcomeEmailJob>(j => j.ExecuteAsync(model.Email, model.Name, callbackUrl, true, CancellationToken.None));
                }
                return this.RedirectWithSuccess(nameof(Members), $"Account created for {model.Name}. A welcome email with a set-password link has been sent.");
            }
        case CreateOrAddToGroupOutcome.AddedToGroup:
            {
                var group = await groupService.GetByIdAsync(id);
                var loginUrl = Url.Action("Login", "Account", null, Request.Scheme);
                if (loginUrl == null || group == null)
                    logger.LogError("Failed to generate Login callback URL or resolve group for userId {UserId}", result.UserId);
                else
                    jobClient.Enqueue<GroupMembershipAddedEmailJob>(j => j.ExecuteAsync(result.Email, result.Name, group.Name, model.GroupRole.ToString(), loginUrl, CancellationToken.None));
                return this.RedirectWithSuccess(nameof(Members), $"{result.Name} has been added to the group as {model.GroupRole}. A notification email has been sent.");
            }
        case CreateOrAddToGroupOutcome.AddedToGroupStrandedAccount:
            {
                // identical WelcomeEmailJob resend + same "added to group" text — see AdminController.CreateUser:159-176
            }
        case CreateOrAddToGroupOutcome.AlreadyMember:
            return this.RedirectWithWarning(nameof(Members), $"{result.Name} is already a member of this group.");
        case CreateOrAddToGroupOutcome.Failed:
        default:
            foreach (var error in result.Errors) ModelState.AddModelError(string.Empty, error);
            // re-render Members with modal open + errors
    }
}
```

**IMPORTANT deviation from the AdminController analog:** `AdminController.CreateUser` sources `groupId` from `activeGroupContext.ActiveGroupId` (line 122). The new `CreateMember` action must use the `id` route parameter instead and must NOT inject `IActiveGroupContext` anywhere in `GroupController`'s constructor — this is the phase's hard constraint (D-06), directly copied from a pattern that is otherwise wrong for this controller.

**Constructor additions needed** (mirrors `AdminController.cs:21`, minus `activeGroupContext`, `emailOptions`, `cache`, rate-limiter, and Resend stats — those are `AdminController`-specific, not needed here):
```csharp
public class GroupController(
    IGroupService groupService,
    IUserService userService,
    IIdentityService identityService,
    IBackgroundJobClient jobClient,
    ILogger<GroupController> logger) : Controller
```

**Error handling pattern:** `RedirectWithSuccess`/`RedirectWithWarning` (`Extensions/ControllerExtensions.cs:24,36`) — note these helpers only take `(action, message)`, no route-value dictionary. If `id`/`search` need to be preserved on the redirect target after a `CreateMember` success/warning, either extend these helpers or use `RedirectToAction(nameof(Members), new { id })` directly instead of the helper (need to decide during planning — the helpers as they exist today do not support extra route values).

---

### `ViewModels/PlatformViewModels/CreateMemberViewModel.cs` (new ViewModel, request-response)

**Analog:** `ViewModels/AdminViewModels/CreateUserViewModel.cs` (verbatim field shape)

```csharp
using QuestBoard.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace QuestBoard.Service.ViewModels.PlatformViewModels;

public class CreateMemberViewModel
{
    [Required]
    [EmailAddress]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    [Display(Name = "Display Name")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "Group Role")]
    public GroupRole GroupRole { get; set; } = GroupRole.Player;
}
```
Nest as `GroupMembersViewModel.CreateMember` (mirrors the existing `AddMember` nesting on the same ViewModel — see below), per RESEARCH.md's Open Questions recommendation.

---

### `ViewModels/PlatformViewModels/GroupMembersViewModel.cs` (modified)

**Analog:** itself, current shape (`GroupMembersViewModel.cs`):
```csharp
public class GroupMembersViewModel
{
    public Group Group { get; set; } = null!;
    public IList<UserGroup> Members { get; set; } = [];
    public AddMemberViewModel AddMember { get; set; } = new();
    public IList<User> AvailableUsers { get; set; } = [];
}
```
Add `public string? SearchQuery { get; set; }` (mirrors `Views/Shop`'s `SearchQuery` on its list ViewModel) and `public CreateMemberViewModel CreateMember { get; set; } = new();`. Keep `AddMemberViewModel AddMember` — it's repurposed as the per-row Add binding target (its `UserId`+`Role` shape already matches, per RESEARCH.md Assumption A3), but drop the `[Bind(Prefix = "AddMember")]` usage in the controller since each row now posts top-level `UserId`/`Role` fields, not a nested `AddMember.*` prefix.

---

### `Areas/Platform/Views/Group/Members.cshtml` (view, request-response) — three composite patterns

**Pattern A — Search filter row (D-01).** Analog: `Views/Shop/Index.cshtml:205-283`:
```html
<form method="get" action="@Url.Action("Index", "Shop")" class="shop-filter-row mb-3" id="shop-filter-form">
    ...
</form>
<div class="shop-search mb-3">
    <div class="input-group justify-content-center" style="max-width: 700px; margin: 0 auto;">
        <input type="text" name="search" class="form-control"
               placeholder="Search items by name or description..."
               value="@Model.SearchQuery" autocomplete="off" form="shop-filter-form" />
        <button type="submit" class="btn filter-apply-btn" form="shop-filter-form">
            <i class="fas fa-search me-1"></i>Search
        </button>
    </div>
</div>
```
For Members.cshtml: a simpler single-field `<form method="get" asp-action="Members" asp-route-id="@Model.Group.Id">` containing just the search input + submit button (no rarity checkboxes/sort needed — D-02 says Name+Email only, no extra filters).

**Pattern B — Per-row Add form (D-03/D-04).** Analog: today's own `RemoveMember` form, `Members.cshtml:75-80`:
```html
<form asp-action="RemoveMember" asp-route-id="@Model.Group.Id" method="post" asp-antiforgery="true" class="d-inline">
    <input type="hidden" name="userId" value="@member.UserId" />
    <button type="submit" class="btn btn-sm btn-danger">
        <i class="fas fa-user-minus me-1"></i>Remove Member
    </button>
</form>
```
Extend this exact shape per RESEARCH.md Pattern 2 — add a `Role` select and route `search` through via `asp-route-search="@Model.SearchQuery"` so `AddMember`'s redirect can echo it back (D-04).

**Pattern C — Create New User modal (D-05/D-06).** Analog: `Views/ShopManagement/Index.cshtml:445-483` (`#denyModal`) for the modal shell, and `Views/Admin/CreateUser.cshtml:22-55` for the form fields inside it:
```html
<div class="modal fade" id="denyModal" tabindex="-1">
    <div class="modal-dialog">
        <div class="modal-content bg-dark text-light">
            <form id="denyForm" method="post">
                @Html.AntiForgeryToken()
                <div class="modal-header border-secondary"> ... </div>
                <div class="modal-body"> ... </div>
                <div class="modal-footer border-secondary">
                    <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">Cancel</button>
                    <button type="submit" class="btn btn-danger">...</button>
                </div>
            </form>
        </div>
    </div>
</div>
```
And the form-field shape from `CreateUser.cshtml:25-41`:
```html
<div class="mb-3">
    <label asp-for="Email" class="form-label"></label>
    <input asp-for="Email" class="form-control" type="email" />
    <span asp-validation-for="Email" class="text-danger"></span>
</div>
<div class="mb-3">
    <label asp-for="Name" class="form-label"></label>
    <input asp-for="Name" class="form-control" />
    <span asp-validation-for="Name" class="text-danger"></span>
</div>
<div class="mb-3">
    <label asp-for="GroupRole" class="form-label"></label>
    <select asp-for="GroupRole" asp-items="Html.GetEnumSelectList<GroupRole>()" class="form-select"></select>
    <span asp-validation-for="GroupRole" class="text-danger"></span>
</div>
```
**Important simplification vs. `denyModal`:** unlike `denyModal`, which needs `show.bs.modal` JS to rewrite the form `action` per-row (because the same modal serves many different rows), the Create New User modal is a single static target (`asp-action="CreateMember" asp-route-id="@Model.Group.Id"`) — no JS delegation/rewriting needed, just a plain Tag-Helper form inside the modal markup, triggered via a plain `data-bs-toggle="modal" data-bs-target="#createMemberModal"` button (mirrors `ShopManagement/Index.cshtml:24`'s `#bulkActionsModal` trigger button, which also needs no JS since it's a static target — though that one is currently an unimplemented placeholder, only its trigger-button markup is a valid precedent here).

---

### `Areas/Platform/Views/Group/Members.Mobile.cshtml` (view, request-response)

**Analog:** itself (current stacked structure, already single-column) — apply Pattern A/B/C above in sequence per D-08 (Members list → Search + non-member cards → Create New User trigger). Reuse the identical Create-New-User modal markup unchanged (D-09/Pattern 4 resolution) — confirmed precedent: `ShopManagement/Index.Mobile.cshtml` reuses `#denyModal`/`#denyForm` verbatim from the desktop view with no mobile-specific modal changes.

---

## Shared Patterns

### Antiforgery on all new/modified forms
**Source:** `Areas/Platform/Views/Group/Members.cshtml:75` (`asp-antiforgery="true"`) and `Views/ShopManagement/Index.cshtml:450` (`@Html.AntiForgeryToken()` inside a modal form)
**Apply to:** Every per-row Add form, the search GET form (GET forms don't need antiforgery), and the Create New User modal form.

### Flash messaging
**Source:** `Extensions/ControllerExtensions.cs:24,36` (`RedirectWithSuccess`, `RedirectWithWarning`) — used verbatim by `AdminController.CreateUser`. `GroupController` today uses raw `TempData["Success"]`/`TempData["Error"]` (see `Members.cshtml:14-30` render logic, which already handles `TempData["Success"]`/`["Error"]` but not `["Warning"]` — the view will need a `TempData["Warning"]` alert block added, matching the `alert-success`/`alert-danger` pattern with `alert-warning` styling).
**Apply to:** New `CreateMember` action (all four outcomes per D-07) and the adapted `AddMember`/`Members` actions if migrated to the same helpers for consistency.

### `groupId` sourcing — route only, never session
**Source:** This phase's own `GroupController.Members`/`AddMember`/`RemoveMember` (`GroupController.cs:114-161`) — all three already take `id` as a route/action parameter, never `IActiveGroupContext`.
**Apply to:** New `CreateMember` action and the new `GetAvailableUsers`/`GetAvailableUsersAsync` methods — must take `groupId` as a plain parameter (mirrors `GetAllGroupMembers`, NOT `GetAllDungeonMasters`/`GetAllPlayers` which use `activeGroupContext`).

### Manual-join "not in group" query shape
**Source:** `QuestBoard.Repository/UserRepository.cs:51-59` (`GetAllGroupMembers`) — no EF Core Global Query Filter exists on `UserEntity` (confirmed zero `HasQueryFilter` calls in `QuestBoardContext.cs`), so every group-scoped user query is an explicit manual join.
**Apply to:** New `GetAvailableUsers` repository method (negate the `Any(...)` + add `Contains` search predicate, per RESEARCH.md Pattern 1).

## No Analog Found

None — every file this phase touches has a direct, concrete in-codebase analog. This phase introduces no new architectural pattern; RESEARCH.md's own conclusion ("Every pattern this phase needs already exists somewhere in the codebase") is confirmed by this mapping pass.

## Metadata

**Analog search scope:** `QuestBoard.Service/Areas/Platform/`, `QuestBoard.Service/Controllers/Admin/`, `QuestBoard.Service/Views/Shop/`, `QuestBoard.Service/Views/ShopManagement/`, `QuestBoard.Service/Views/Admin/`, `QuestBoard.Service/ViewModels/`, `QuestBoard.Service/Extensions/`, `QuestBoard.Repository/`, `QuestBoard.Domain/Interfaces/`, `QuestBoard.Domain/Services/`
**Files scanned:** 12 read directly (GroupController.cs, UserRepository.cs, AdminController.cs, Members.cshtml, Members.Mobile.cshtml, Shop/Index.cshtml, ShopManagement/Index.cshtml, Admin/CreateUser.cshtml, ControllerExtensions.cs, GroupMembersViewModel.cs, AddMemberViewModel.cs, CreateUserViewModel.cs) + 1 grep (IUserService.cs)
**Pattern extraction date:** 2026-07-04
