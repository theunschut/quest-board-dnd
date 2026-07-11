# Phase 72: Platform Settings + Token Contract - Pattern Map

**Mapped:** 2026-07-11 (regenerated — supersedes prior version built around the rejected `IntegrationSettingEntity` fixed-column singleton design)
**Files analyzed:** 17
**Analogs found:** 15 / 17

**Regeneration note:** The prior PATTERNS.md mapped everything to a single `IntegrationSettingEntity`/`IntegrationsController` singleton-row design. That design was rejected in `72-CONTEXT.md` D-07 (2026-07-11) in favor of a generic key-value `PlatformSettingEntity { Id, Key, Value, GroupId (int?) }` with cascade lookup, plus a **second** Group Admin-only settings page (D-09). This file replaces the prior mapping entirely. The HMAC/token-contract sections were not settings-storage-dependent and carry forward conceptually, but no code lands for them this phase (design doc only) so they are out of scope for pattern-mapping (no files to classify).

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|---|---|---|---|---|
| `QuestBoard.Repository/Entities/PlatformSettingEntity.cs` | model (EF entity) | CRUD | `QuestBoard.Repository/Entities/GroupEntity.cs` | role-match (shape only — no key-value precedent exists) |
| `QuestBoard.Repository/Entities/QuestBoardContext.cs` (MODIFIED — unique index config) | config | CRUD | `QuestBoardContext.cs` `OnModelCreating` (existing unique-index/query-filter registrations) | role-match |
| `QuestBoard.Repository/Migrations/<timestamp>_AddPlatformSettings.cs` | migration | batch | any recent migration under `QuestBoard.Repository/Migrations/` | exact (mechanical, EF-generated) |
| `QuestBoard.Domain/Interfaces/IPlatformSettingRepository.cs` | service (interface) | CRUD | `QuestBoard.Domain/Interfaces/IGroupRepository.cs` (custom methods alongside `IBaseRepository<T>`) | role-match |
| `QuestBoard.Repository/PlatformSettingRepository.cs` | service (repository impl) | CRUD | `QuestBoard.Repository/GroupRepository.cs` | role-match — but cascade-lookup method (`GetCascadeAsync`/`GetForScopeAsync`) is genuinely new, no direct precedent |
| `QuestBoard.Domain/Interfaces/IPlatformSettingService.cs` | service (interface) | CRUD | `QuestBoard.Domain/Interfaces/IGroupService.cs` | role-match |
| `QuestBoard.Domain/Services/PlatformSettingService.cs` | service | CRUD | `QuestBoard.Domain/Services/GroupService.cs` | role-match — narrow, custom methods pattern (not full `IBaseService<T>`), same shape as `GroupService`'s bespoke `AddMemberAsync`/`HasMembersAsync` additions |
| `QuestBoard.Domain/Models/PlatformSetting.cs` (or a `PlatformSettingScope`/`OmphalosSettings` DTO) | model (domain) | CRUD | `QuestBoard.Domain/Models/Group.cs` | role-match |
| `QuestBoard.Domain/Automapper/EntityProfile.cs` (MODIFIED) | config | transform | existing `CreateMap<GroupEntity, Group>()` entries | exact |
| `QuestBoard.Repository/Extensions/ServiceExtensions.cs` (MODIFIED) | config | — | `services.AddScoped<IGroupRepository, GroupRepository>();` | exact |
| `QuestBoard.Domain/Extensions/ServiceExtensions.cs` (MODIFIED) | config | — | `services.AddScoped<IGroupService, GroupService>();` | exact |
| `QuestBoard.Service/Areas/Platform/Controllers/IntegrationsController.cs` | controller | request-response | `QuestBoard.Service/Areas/Platform/Controllers/UsersController.cs` | exact (lean `SuperAdminOnly` Platform-area controller, closest scope per CONTEXT.md's own `code_context`) |
| `QuestBoard.Service/Areas/Platform/Views/Integrations/Index.cshtml` + `Index.Mobile.cshtml` | component (Razor view) | request-response | `QuestBoard.Service/Areas/Platform/Views/Group/Index.cshtml` + `Index.Mobile.cshtml` | exact (modern-card, header-button nav wiring) |
| `QuestBoard.Service/ViewModels/PlatformViewModels/IntegrationSettingsViewModel.cs` | model (ViewModel) | request-response | existing `PlatformUserViewModel` (`ViewModels/PlatformViewModels/`) | role-match |
| `QuestBoard.Service/Controllers/Admin/AdminController.cs` (MODIFIED — new action) **or** new sibling `QuestBoard.Service/Controllers/Admin/AdminIntegrationsController.cs` | controller | request-response | `QuestBoard.Service/Controllers/Admin/AdminController.cs` (`Users` action + `PromoteToAdmin`/`DemoteFromAdmin` actions) | exact — same controller, same `[Authorize(Policy = "AdminOnly")]` + `IActiveGroupContext.ActiveGroupId` gating already used for group-scoped write actions |
| `QuestBoard.Service/Views/Admin/Integrations.cshtml` + `Integrations.Mobile.cshtml` | component (Razor view) | request-response | `QuestBoard.Service/Views/Admin/EditUser.cshtml` (masked/checkbox field patterns) + `Group/Index.cshtml` (modern-card shell) | role-match (composite: card shell from Platform, form-field patterns from `EditUser.cshtml`) |
| `QuestBoard.Service/wwwroot/css/platform-integrations.mobile.css` | config (CSS) | — | `wwwroot/css/platform-users.mobile.css` | exact (naming + wrapper-class convention, per UI-SPEC) |
| `QuestBoard.Service/wwwroot/css/admin-integrations.mobile.css` | config (CSS) | — | `wwwroot/css/admin-users.mobile.css` | exact (per UI-SPEC) |

## Pattern Assignments

### `QuestBoard.Repository/Entities/PlatformSettingEntity.cs` (model, CRUD)

**Analog:** `QuestBoard.Repository/Entities/GroupEntity.cs` (full file, 22 lines — read in one pass)

**Shape to copy** (`GroupEntity.cs:1-22`):
```csharp
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuestBoard.Repository.Entities;

[Table("Groups")]
public class GroupEntity : IEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;
    // ...
}
```

**Apply as** (per D-07/D-08 — this is new shape, no direct precedent, composed from the above + a nullable FK):
```csharp
[Table("PlatformSettings")]
public class PlatformSettingEntity : IEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required, StringLength(100)]
    public string Key { get; set; } = string.Empty;

    [StringLength(500)]
    public string Value { get; set; } = string.Empty;

    // null = instance-wide default row; non-null = this group's override (D-08)
    public int? GroupId { get; set; }
    public virtual GroupEntity? Group { get; set; }
}
```

**Unique constraint** — `(Key, GroupId)` must be unique per D-10 ("only `(Key, GroupId)` needs to be unique"). No existing filtered-unique-index precedent in this codebase was found for a nullable-column composite unique index; this is new `OnModelCreating` configuration (planner/implementer discretion per CONTEXT.md, e.g. `HasIndex(x => new { x.Key, x.GroupId }).IsUnique()` — EF Core/SQL Server treats multiple `NULL` GroupId rows as distinct by default under a plain unique index, which is actually the *wrong* behavior here since only one `GroupId = null` row should exist per `Key`; a filtered unique index or app-level check is required — flagged in CONTEXT.md as left to research/planning).

**No `HasQueryFilter`** — per the superseded RESEARCH.md's still-valid observation, `GroupEntity`/`UserEntity` carry no tenant query filter (they define the tenant boundary). `PlatformSettingEntity` must also not be filtered, since `GroupId = null` rows must remain visible regardless of `ActiveGroupContext.ActiveGroupId` (this is explicitly called out in `72-CONTEXT.md`'s `code_context`).

---

### `QuestBoard.Repository/PlatformSettingRepository.cs` (service/repository, CRUD)

**Analog:** `QuestBoard.Repository/GroupRepository.cs` (full file, 103 lines — read in one pass)

**Imports + class shape pattern** (`GroupRepository.cs:1-12`):
```csharp
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.Repository.Entities;

namespace QuestBoard.Repository;

internal class GroupRepository(QuestBoardContext dbContext, IMapper mapper)
    : BaseRepository<Group, GroupEntity>(dbContext, mapper), IGroupRepository
{
```
Apply identically: `internal class PlatformSettingRepository(QuestBoardContext dbContext, IMapper mapper) : BaseRepository<PlatformSetting, PlatformSettingEntity>(dbContext, mapper), IPlatformSettingRepository`.

**Custom-method-alongside-base pattern** (`GroupRepository.cs:44-46`, narrow single-purpose method):
```csharp
/// <inheritdoc/>
public async Task<bool> HasMembersAsync(int groupId, CancellationToken token = default)
    => await DbContext.UserGroups.AnyAsync(ug => ug.GroupId == groupId, token);
```

**Cascade lookup — new logic, no precedent.** Follow this shape (single query, prefer group row, fall back to null-group row), keeping it a plain LINQ query against `DbContext.Set<PlatformSettingEntity>()` rather than two round-trips where avoidable:
```csharp
/// <inheritdoc/>
public async Task<string?> GetCascadeValueAsync(string key, int? groupId, CancellationToken token = default)
{
    if (groupId is int gid)
    {
        var groupRow = await DbContext.Set<PlatformSettingEntity>()
            .FirstOrDefaultAsync(s => s.Key == key && s.GroupId == gid, token);
        if (groupRow != null) return groupRow.Value;
    }
    var defaultRow = await DbContext.Set<PlatformSettingEntity>()
        .FirstOrDefaultAsync(s => s.Key == key && s.GroupId == null, token);
    return defaultRow?.Value;
}
```
This is the "application-level fallback query, not a DB query filter" mechanism CONTEXT.md's `code_context` explicitly flags — do not attempt to model this as a `HasQueryFilter`.

**Existence/upsert pattern** — no direct precedent for upsert either; closest is `GroupRepository.AddMemberAsync` (`GroupRepository.cs:49-75`)'s "check existence, insert, catch `DbUpdateException` from the unique index race" shape, adapted for update-or-insert instead of insert-only. The "find, if-exists mutate-or-remove" shape from `RemoveMemberAsync` is the right template for both the `SaveAsync` upsert and the "Clear Override" delete action:
```csharp
public async Task RemoveMemberAsync(int groupId, int userId, CancellationToken token = default)
{
    var ug = await DbContext.UserGroups
        .FirstOrDefaultAsync(ug => ug.UserId == userId && ug.GroupId == groupId, token);
    if (ug == null) return;
    DbContext.UserGroups.Remove(ug);
    await DbContext.SaveChangesAsync(token);
}
```

---

### `QuestBoard.Domain/Services/PlatformSettingService.cs` (service, CRUD)

**Analog:** `QuestBoard.Domain/Services/GroupService.cs` (full file, 45 lines — read in one pass)

**Pattern — narrow service, NOT full `IBaseService<T>`** (per the superseded RESEARCH.md's Pattern 1, which remains valid guidance even though the entity shape changed — a key-value settings row is still not a genuine multi-row "collection" from the caller's perspective; it's resolved by `(key, scope)`, not by `Id`):
```csharp
internal class GroupService(IGroupRepository repository, IMapper mapper)
    : BaseService<Group>(repository, mapper), IGroupService
{
    /// <inheritdoc/>
    public async Task<bool> HasMembersAsync(int groupId, CancellationToken token = default)
        => await repository.HasMembersAsync(groupId, token);
```
Apply the same thin pass-through shape for `PlatformSettingService` methods like `GetForScopeAsync(int? groupId, ...)` (returns the resolved `OmphalosUrl`/`SharedSecret`/`IsEnabled` trio for a scope, applying the cascade for each of the three keys) and `SaveAsync(int? groupId, string url, string? newSecret, bool isEnabled, ...)` (blank-preserve guard per SETT-04, applied per-scope).

**Blank-preserve guard** — adapt from the codebase's only current precedent (`ContactsController.cs:238`, file-upload flavor) rather than `AdminController.EditUser`'s email-change logic (CONTEXT.md explicitly rules the latter out as the wrong analog):
```csharp
// ContactsController.cs:238 pattern — "otherwise remains unchanged"
var newSecret = string.IsNullOrWhiteSpace(model.SharedSecret) ? null : model.SharedSecret;
```
Apply this exact null-means-unchanged semantic in the service's `SaveAsync`, on both the instance-wide and group-override paths independently (per the D-03/D-09 interaction note in CONTEXT.md).

---

### `QuestBoard.Service/Areas/Platform/Controllers/IntegrationsController.cs` (controller, request-response)

**Analog:** `QuestBoard.Service/Areas/Platform/Controllers/UsersController.cs` (full file, 56 lines — read in one pass; confirmed by CONTEXT.md's own `code_context` as the closer-scoped template over `GroupController.cs`)

**Imports + class-level auth pattern** (`UsersController.cs:1-10`):
```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Service.ViewModels.PlatformViewModels;

namespace QuestBoard.Service.Areas.Platform.Controllers;

[Area("Platform")]
[Authorize(Policy = "SuperAdminOnly")]
public class UsersController(IUserService userService, IIdentityService identityService) : Controller
{
```
Apply identically for `IntegrationsController(IPlatformSettingService settingService) : Controller`, same `[Area("Platform")] [Authorize(Policy = "SuperAdminOnly")]`.

**GET + mutating-POST pattern** (`UsersController.cs:12-27` for GET shape; `29-44` for a `[ValidateAntiForgeryToken]` mutating POST with `TempData` success/error messaging):
```csharp
[HttpGet]
public async Task<IActionResult> Index()
{
    var users = await userService.GetAllAsync();
    var viewModels = new List<PlatformUserViewModel>();
    // ... build view model, never expose raw sensitive data
    return View(viewModels);
}

[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> Disable(int userId)
{
    // ... guard, call service, set TempData["Success"]/["Error"], redirect
    var result = await identityService.DisableUserAsync(userId);
    TempData[result.Succeeded ? "Success" : "Error"] =
        result.Succeeded ? "Account disabled." : "Failed to disable account. The user may no longer exist.";
    return RedirectToAction(nameof(Index));
}
```
Apply for `Index()` (GET — resolve current settings for `groupId: null`, populate `HasSecretConfigured`, never the raw secret), `Index(IntegrationSettingsViewModel, POST)` (Save Settings), and `GenerateSecret()` (POST, persists immediately per RESEARCH.md Pitfall 3, redirects with the plaintext value in `TempData` for one render).

---

### `QuestBoard.Service/Controllers/Admin/AdminController.cs` — new `Integrations` action(s) (controller, request-response)

**Analog:** `QuestBoard.Service/Controllers/Admin/AdminController.cs` itself — the `Users` action (GET) and `PromoteToAdmin`/`DemoteFromAdmin` (mutating POST) actions (`AdminController.cs:23-79`, read in one pass)

**Class-level auth is already the exact D-09 gate** (`AdminController.cs:20-21`):
```csharp
[Authorize(Policy = "AdminOnly")]
public class AdminController(IUserService userService, IQuestService questService, IGroupService groupService,
    IIdentityService identityService, IBackgroundJobClient jobClient, IOptions<EmailSettings> emailOptions,
    IMemoryCache cache, IActiveGroupContext activeGroupContext, ILogger<AdminController> logger,
    PartitionedRateLimiter<int> emailResendLimiter, ResendStatsClient resendStatsClient,
    IBoardTypeResolver boardTypeResolver) : Controller
```
**This is the key finding for D-09/SETT-09/SETT-10**: `"AdminOnly"` is NOT a simple `RequireRole` — it is backed by `AdminHandler.cs` (`QuestBoard.Service/Authorization/AdminHandler.cs`, full file, 37 lines), which already does exactly the check CONTEXT.md's `code_context` asked to confirm:
```csharp
// AdminHandler.cs:12-36
protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, AdminRequirement requirement)
{
    // Step 1: SuperAdmin bypass — reads claims directly, no DB call
    if (context.User.IsInRole("SuperAdmin")) { context.Succeed(requirement); return; }

    // Step 2: Null group guard
    if (activeGroupContext.ActiveGroupId is not { } groupId) { context.Fail(); return; }

    // Step 3: Group role check
    var role = await userService.GetGroupRoleAsync(context.User, groupId);
    if (role == GroupRole.Admin) context.Succeed(requirement);
    else context.Fail();
}
```
`[Authorize(Policy = "AdminOnly")]` on `AdminController` already enforces **exactly** "`GroupRole.Admin` for the active group, or SuperAdmin" — no new authorization plumbing is needed for the group-override page. This satisfies SETT-09's "Group Admin only, not DungeonMaster" requirement verbatim (the DM role never reaches `context.Succeed`).

**GET action pattern** (`AdminController.cs:23-53`, `Users()`):
```csharp
[HttpGet]
public async Task<IActionResult> Users()
{
    var groupId = activeGroupContext.ActiveGroupId;
    if (groupId == null) return RedirectToAction("Index", "GroupPicker");
    var allUsers = await userService.GetAllGroupMembersAsync(groupId.Value);
    // ... build view model
    return View(sortedUsers);
}
```
Apply for the new `Integrations()` GET action — resolve `groupId = activeGroupContext.ActiveGroupId`, redirect to `GroupPicker` if null (same null-group guard), call `IPlatformSettingService.GetForScopeAsync(groupId.Value, ...)`, build the three-state cascade view model (Override Active / Inherited / Not Configured per UI-SPEC).

**Mutating POST pattern with active-group re-derivation** (`AdminController.cs:55-79`, `PromoteToAdmin`/`DemoteFromAdmin`):
```csharp
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> PromoteToAdmin(int userId)
{
    var groupId = activeGroupContext.ActiveGroupId;
    if (groupId == null) return RedirectToAction(nameof(Users));
    // Guard against a crafted POST for a user outside the active group
    var targetRole = await userService.GetGroupRoleByIdAsync(userId, groupId.Value);
    if (targetRole == null) return RedirectToAction(nameof(Users));
    await userService.SetGroupRoleAsync(userId, groupId.Value, GroupRole.Admin);
    return RedirectToAction(nameof(Users));
}
```
Apply identically for `Integrations(IntegrationSettingsViewModel, POST)` (Save Override — always re-derive `groupId` from `activeGroupContext`, never trust a posted group ID, matching the "guard against a crafted POST" comment style), `GenerateSecret()` (POST, group-scoped), and `ClearOverride()` (POST, deletes the group's override rows).

**Controller-split decision (CONTEXT.md discretion note):** `AdminController.cs` is already large (13 constructor dependencies). Per CONTEXT.md's explicit discretion note referencing the `UsersController`-split-from-`GroupController` precedent, planner should weigh a new sibling `QuestBoard.Service/Controllers/Admin/AdminIntegrationsController.cs` (same `[Authorize(Policy = "AdminOnly")]`, same `IActiveGroupContext` DI) over adding actions directly to `AdminController`. Either way, the authorization/GET/POST patterns above apply unchanged — only the file location and route prefix differ.

---

### `QuestBoard.Service/ViewModels/PlatformViewModels/IntegrationSettingsViewModel.cs` (model, request-response)

**Analog:** `PlatformUserViewModel` (referenced by `UsersController.cs:20-24`) — small, flat, wraps a domain concept plus one or two UI-only derived booleans (`IsDisabled`).

**Pattern:** expose `bool HasSecretConfigured` (never the raw secret) for GET rendering, matching the "never populate the masked field with the real value" rule in RESEARCH.md Pattern 3 / UI-SPEC. For the group-override ViewModel variant, add the three-state cascade fields (`bool HasOverride`, `bool InstanceDefaultConfigured`, `bool InstanceDefaultEnabled`) needed to render the UI-SPEC's cascade banner — no direct precedent for a tri-state banner view model in this codebase; keep it flat (bools), not an enum, to match every other ViewModel's style (grep of `PlatformViewModels/`/`AdminViewModels/` shows no enum-typed view-state fields, only bools/derived flags).

---

### `QuestBoard.Service/Areas/Platform/Views/Integrations/Index.cshtml` + `.Mobile.cshtml` (component, request-response)

**Analog:** `QuestBoard.Service/Areas/Platform/Views/Group/Index.cshtml` (first 40 lines read; modern-card shell + header-button pattern) + `QuestBoard.Service/Views/Admin/EditUser.cshtml` (masked input / plain-checkbox field patterns, per RESEARCH.md Pattern 5 and UI-SPEC's `HasKey`-checkbox reference at `EditUser.cshtml:40-47`)

**Card shell + header-button nav pattern** (`Group/Index.cshtml:1-19`):
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
        <div class="d-flex gap-2">
            <a asp-controller="Group" asp-action="Create" asp-area="Platform" class="btn btn-success">
                <i class="fas fa-plus me-2"></i>Create Group
            </a>
            <a asp-controller="Users" asp-action="Index" asp-area="Platform" class="btn btn-secondary">
                <i class="fas fa-users-cog me-2"></i>Manage Users
            </a>
        </div>
    </div>
    <div class="card-body modern-card-body">
        ...
```
Apply this exact shell for `Integrations/Index.cshtml`'s outer card (`<h2>` = "Integrations", `fa-plug text-danger` per UI-SPEC), and add the third header button to this same `Group/Index.cshtml` file (`<a asp-controller="Integrations" asp-action="Index" asp-area="Platform" class="btn btn-secondary"><i class="fas fa-plug me-2"></i>Integrations</a>`) — MODIFY, not new file, matching CONTEXT.md/RESEARCH.md Pattern 4's "header button on the source page" nav mechanism (confirmed: no shared Platform nav list exists).

**Checkbox toggle pattern** (per UI-SPEC/RESEARCH.md Pattern 5, `EditUser.cshtml:40-47`, plain `form-check`, never `form-switch`):
```html
<div class="form-check">
    <input asp-for="IsEnabled" class="form-check-input" type="checkbox" />
    <label asp-for="IsEnabled" class="form-check-label">
        <i class="fas fa-toggle-on text-success me-2"></i>
        Integration Enabled
    </label>
</div>
```

---

### `QuestBoard.Service/Views/Admin/Integrations.cshtml` + `.Mobile.cshtml` (component, request-response)

**Analog:** Same modern-card shell as above, but living in `Views/Admin/` (not `Areas/Platform/Views/`) since this page hangs off `AdminController` (or its sibling), not the Platform area. Structurally identical card/form pattern; only the outer namespace/folder and nav entry point (Admin navbar dropdown item, not a header button — see UI-SPEC's Nav entry point row) differ. No existing `Views/Admin/*.cshtml` currently renders a three-state status banner — this is new UI composition (plain `text-muted` paragraph + badge, per UI-SPEC's explicit "no `.alert` precedent" note), not copied from an existing analog; compose it from the badge conventions already used in `Users/Index.cshtml`'s `Confirmed`/`Unconfirmed` `bg-success`/`bg-secondary` badges (cited directly in UI-SPEC).

---

## Shared Patterns

### Authorization — two distinct, both existing
**Source A (instance-wide page):** `"SuperAdminOnly"` policy, `policy.RequireRole("SuperAdmin")` (`Program.cs:87-88`) — apply to `IntegrationsController` class-level `[Authorize]`, identical to `UsersController.cs:9`/`GroupController.cs`.

**Source B (group-override page):** `"AdminOnly"` policy, backed by `AdminHandler.cs` (group-scoped `GroupRole.Admin` check with SuperAdmin bypass and null-active-group guard) — apply to wherever the group-override action lands, identical to `AdminController.cs:20`. This is the exact mechanism SETT-09/SETT-10 need; no new authorization code required.

### Blank-preserves-existing-value guard (SETT-04)
**Source:** `ContactsController.cs:238` ("Otherwise, the [x] remains unchanged" — file-upload flavor, adapted to `string?`)
**Apply to:** Both `IntegrationsController`'s and the group-override controller's Save actions, and the service-layer `SaveAsync` method.
```csharp
var newSecret = string.IsNullOrWhiteSpace(model.SharedSecret) ? null : model.SharedSecret;
```
**Do NOT copy from** `AdminController.EditUser` (`AdminController.cs:224`) — that method handles email-change confirmation logic, not a masked-secret blank-preserve guard; CONTEXT.md explicitly flags this as the wrong analog despite superficial similarity.

### CSRF protection
**Source:** `[ValidateAntiForgeryToken]` on every mutating POST across `UsersController.cs`/`AdminController.cs` (established convention, e.g. `UsersController.cs:30`, `AdminController.cs:56/69`)
**Apply to:** All new mutating actions — Save Settings, Generate Secret (both pages), Save Override, Clear Override.

### TempData success/error messaging
**Source:** `UsersController.cs:41-43` (`TempData[result.Succeeded ? "Success" : "Error"] = ...`)
**Apply to:** All Save/Generate/Clear actions across both pages — short, past-tense copy per UI-SPEC's Copywriting Contract (e.g. `"Integration settings saved."`, `"Group override cleared. This group now uses the instance-wide default."`).

### Server-side secret generation
**Source:** No existing codebase call site (new BCL usage) — `RandomNumberGenerator.GetString`/`GetHexString` (`System.Security.Cryptography`, BCL, .NET 8+), confirmed zero new packages needed.
**Apply to:** `GenerateSecret()` POST actions on both controllers, persisting immediately via the service's `SaveAsync` (per RESEARCH.md Pitfall 3 — do not defer to a separate Save click).

### Modern-card UI shell
**Source:** `site.css:1045-1334` (`.modern-card`/`.modern-card-header`/`.modern-card-body`), used verbatim across every Platform/Admin view.
**Apply to:** Both new pages' outer card markup — zero new CSS component classes needed per UI-SPEC.

### Mobile parity (mandatory, same task)
**Source:** RESEARCH.md Pattern 4 citing Phase 43/54 follow-up-fix history; UI-SPEC's explicit "no follow-up mobile fix" mandate.
**Apply to:** `Integrations/Index.cshtml` + `Index.Mobile.cshtml` must ship together; `Views/Admin/Integrations.cshtml` + `Integrations.Mobile.cshtml` must ship together; `Group/Index.cshtml` + `Index.Mobile.cshtml` edits (header button) must ship together.

## No Analog Found

| File | Role | Data Flow | Reason |
|---|---|---|---|
| `PlatformSettingRepository.GetCascadeValueAsync`/equivalent (cascade lookup method) | service (repository method) | transform/CRUD hybrid | No prior "group override falls back to instance-wide default" query exists anywhere in this codebase; closest conceptual (not mechanical) analog is the tenant `HasQueryFilter` registration in `QuestBoardContext.cs:280-373`, but the mechanism is intentionally different (app-level fallback, not a DB filter) — confirmed explicitly in `72-CONTEXT.md`'s `code_context` |
| `(Key, GroupId)` nullable-column composite unique constraint | migration/config | — | No existing filtered-unique-index precedent in this codebase (every existing unique index, e.g. `UserGroups (UserId, GroupId)`, is on non-nullable columns); left to planning per CONTEXT.md's explicit discretion note |
| Cascade status banner (three-state: Override Active / Inherited / Not Configured) | component (Razor partial/section) | request-response | New UI composition with no existing precedent in this codebase (UI-SPEC explicitly notes "no `.alert` precedent inside `modern-card` bodies for informational banners" — composed from existing badge conventions instead, not copied wholesale) |

## Metadata

**Analog search scope:** `QuestBoard.Repository/`, `QuestBoard.Domain/`, `QuestBoard.Service/Areas/Platform/`, `QuestBoard.Service/Controllers/Admin/`, `QuestBoard.Service/Authorization/`, `QuestBoard.Service/Views/Admin/`, `QuestBoard.Service/Program.cs`
**Files scanned:** `GroupEntity.cs`, `GroupRepository.cs`, `GroupService.cs`, `BaseRepository.cs`, `UsersController.cs`, `GroupController.cs` (referenced, not re-read), `AdminController.cs` (lines 1-80), `AdminHandler.cs`, `AdminRequirement.cs` (path only), `Program.cs` (lines 78-93), `Group/Index.cshtml` (lines 1-40), `ServiceExtensions.cs` (both layers, grep only)
**Pattern extraction date:** 2026-07-11
