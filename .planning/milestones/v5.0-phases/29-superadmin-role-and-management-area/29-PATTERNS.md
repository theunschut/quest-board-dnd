# Phase 29: SuperAdmin Role & Management Area - Pattern Map

**Mapped:** 2026-06-30
**Files analyzed:** 23 new/modified files
**Analogs found:** 23 / 23

---

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|---------------|
| `QuestBoard.Service/Authorization/AdminHandler.cs` | middleware | request-response | self (rewrite) | self |
| `QuestBoard.Service/Authorization/DungeonMasterHandler.cs` | middleware | request-response | `AdminHandler.cs` | exact |
| `QuestBoard.Domain/Interfaces/IUserService.cs` | service interface | CRUD | self (extend) | self |
| `QuestBoard.Domain/Interfaces/IUserRepository.cs` | repository interface | CRUD | self (extend) | self |
| `QuestBoard.Domain/Services/UserService.cs` | service | CRUD | self (extend) | self |
| `QuestBoard.Repository/UserRepository.cs` | repository | CRUD | self (extend) | self |
| `QuestBoard.Service/Controllers/Admin/AdminController.cs` | controller | CRUD | self (extend) | self |
| `QuestBoard.Service/Program.cs` | config | request-response | self (extend) | self |
| `QuestBoard.Repository/Extensions/ServiceExtensions.cs` | config | CRUD | self (extend) | self |
| `QuestBoard.Domain/Extensions/ServiceExtensions.cs` | config | CRUD | self (extend) | self |
| `QuestBoard.Repository/Migrations/AddSuperAdminRole.cs` | migration | batch | `ConvertIsDungeonMasterToRoles.cs` | exact |
| `QuestBoard.Domain/Interfaces/IGroupService.cs` | service interface | CRUD | `IQuestService.cs` | role-match |
| `QuestBoard.Domain/Interfaces/IGroupRepository.cs` | repository interface | CRUD | `IUserRepository.cs` | exact |
| `QuestBoard.Domain/Services/GroupService.cs` | service | CRUD | `CharacterService.cs` | exact |
| `QuestBoard.Repository/GroupRepository.cs` | repository | CRUD | `UserRepository.cs` | exact |
| `QuestBoard.Service/Areas/Platform/Controllers/GroupController.cs` | controller | CRUD | `AdminController.cs` | role-match |
| `QuestBoard.Service/Areas/Platform/Views/Group/Index.cshtml` | view | request-response | `Views/Admin/Users.cshtml` | exact |
| `QuestBoard.Service/Areas/Platform/Views/Group/Create.cshtml` | view | request-response | `Views/Admin/EditUser.cshtml` | exact |
| `QuestBoard.Service/Areas/Platform/Views/Group/Edit.cshtml` | view | request-response | `Views/Admin/EditUser.cshtml` | exact |
| `QuestBoard.Service/Areas/Platform/Views/Group/Delete.cshtml` | view | request-response | `Views/Admin/EditUser.cshtml` | role-match |
| `QuestBoard.Service/Areas/Platform/Views/Group/Members.cshtml` | view | request-response | `Views/Admin/Users.cshtml` | role-match |
| `QuestBoard.Service/Areas/Platform/Views/Shared/_Layout.Platform.cshtml` | view | request-response | `Views/Shared/_Layout.cshtml` | role-match |
| `QuestBoard.Service/Areas/Platform/Views/_ViewImports.cshtml` | config | request-response | `Views/_ViewImports.cshtml` | exact |
| `QuestBoard.Service/Areas/Platform/Views/_ViewStart.cshtml` | config | request-response | `Views/_ViewStart.cshtml` | exact |
| `QuestBoard.Service/ViewModels/PlatformViewModels/GroupListViewModel.cs` | model | CRUD | `ViewModels/AdminViewModels/UserManagementViewModel.cs` | role-match |
| `QuestBoard.Service/ViewModels/PlatformViewModels/GroupMembersViewModel.cs` | model | CRUD | `ViewModels/AdminViewModels/UserManagementViewModel.cs` | role-match |
| `QuestBoard.Service/ViewModels/PlatformViewModels/AddMemberViewModel.cs` | model | CRUD | `ViewModels/AdminViewModels/EditUserViewModel.cs` | role-match |
| `QuestBoard.Domain/Models/GroupWithMemberCount.cs` | model | CRUD | `QuestBoard.Domain/Models/Group.cs` | role-match |
| `QuestBoard.IntegrationTests/Controllers/AdminHandlerIntegrationTests.cs` | test | request-response | `AdminControllerIntegrationTests.cs` | exact |
| `QuestBoard.IntegrationTests/Controllers/PlatformAreaIntegrationTests.cs` | test | request-response | `AdminControllerIntegrationTests.cs` | exact |
| `QuestBoard.IntegrationTests/Controllers/GroupManagementIntegrationTests.cs` | test | CRUD | `AdminControllerIntegrationTests.cs` | role-match |
| `QuestBoard.IntegrationTests/Helpers/TestDataHelper.cs` | test | CRUD | self (extend) | self |
| `QuestBoard.IntegrationTests/Helpers/AuthenticationHelper.cs` | test | request-response | self (extend) | self |

---

## Pattern Assignments

### `QuestBoard.Service/Authorization/AdminHandler.cs` (middleware, request-response)

**Analog:** self — full rewrite based on D-01 through D-04

**Imports pattern** (lines 1-3):
```csharp
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
```

**Current (broken) pattern to REPLACE** (lines 1-29):
```csharp
// CURRENT — reads AspNetUserRoles (now empty) — REPLACE ENTIRELY
public class AdminHandler(IUserService userService) : AuthorizationHandler<AdminRequirement>
{
    protected override async Task HandleRequirementAsync(...)
    {
        if (!context.User.Identity?.IsAuthenticated == true)  // BUG: always false, never triggers Fail
        { context.Fail(); return; }
        var isAdmin = await userService.IsInRoleAsync(context.User, "Admin");  // always false (AspNetUserRoles empty)
        if (isAdmin) context.Succeed(requirement); else context.Fail();
    }
}
```

**Target pattern** (D-01 through D-04 from 29-CONTEXT.md):
```csharp
// TARGET — two-param constructor, three-step logic: SuperAdmin bypass → null guard → GroupRole check
public class AdminHandler(
    IUserService userService,
    IActiveGroupContext activeGroupContext)
    : AuthorizationHandler<AdminRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        AdminRequirement requirement)
    {
        // Step 1: SuperAdmin bypass (D-02) — reads claims directly, no DB call
        if (context.User.IsInRole("SuperAdmin"))
        {
            context.Succeed(requirement);
            return;
        }

        // Step 2: Null group guard (D-03)
        if (activeGroupContext.ActiveGroupId is not { } groupId)
        {
            context.Fail();
            return;
        }

        // Step 3: Group role check (D-04)
        var role = await userService.GetGroupRoleAsync(context.User, groupId);
        if (role == GroupRole.Admin)
            context.Succeed(requirement);
        else
            context.Fail();
    }
}
```

**DI registration note:** `services.AddScoped<IAuthorizationHandler, AdminHandler>()` in Program.cs does NOT change — ASP.NET Core DI resolves both constructor params automatically.

---

### `QuestBoard.Service/Authorization/DungeonMasterHandler.cs` (middleware, request-response)

**Analog:** `AdminHandler.cs` — identical three-step pattern, different role check at step 3

**Current pattern to REPLACE** (lines 1-30 of `DungeonMasterHandler.cs`):
```csharp
// CURRENT — reads AspNetUserRoles (empty); two separate IsInRoleAsync calls
public class DungeonMasterHandler(IUserService userService) : AuthorizationHandler<DungeonMasterRequirement>
```

**Target pattern** — copy AdminHandler structure exactly, change step 3 only:
```csharp
public class DungeonMasterHandler(
    IUserService userService,
    IActiveGroupContext activeGroupContext)
    : AuthorizationHandler<DungeonMasterRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        DungeonMasterRequirement requirement)
    {
        if (context.User.IsInRole("SuperAdmin"))
        { context.Succeed(requirement); return; }

        if (activeGroupContext.ActiveGroupId is not { } groupId)
        { context.Fail(); return; }

        // Step 3 differs: DM or Admin both satisfy DungeonMasterRequirement
        var role = await userService.GetGroupRoleAsync(context.User, groupId);
        if (role == GroupRole.Admin || role == GroupRole.DungeonMaster)
            context.Succeed(requirement);
        else
            context.Fail();
    }
}
```

---

### `QuestBoard.Domain/Interfaces/IUserService.cs` (service interface, CRUD)

**Analog:** self — add two new method signatures

**New method to ADD** (after existing `IsInRoleAsync` signatures, D-06):
```csharp
Task<GroupRole?> GetGroupRoleAsync(ClaimsPrincipal user, int groupId);

Task<int?> SetGroupRoleAsync(int userId, int groupId, GroupRole role);
```

**Existing method signatures that stay unchanged** (lines 9-39 of `IUserService.cs`):
```csharp
// GetAllPlayersAsync and GetAllDungeonMastersAsync signatures do NOT change (D-07/D-08 fix is in implementation)
Task<IList<User>> GetAllDungeonMastersAsync(CancellationToken token = default);
Task<IList<User>> GetAllPlayersAsync(CancellationToken token = default);
```

---

### `QuestBoard.Domain/Interfaces/IUserRepository.cs` (repository interface, CRUD)

**Analog:** self — add two new method signatures matching new service methods

**New methods to ADD** (after line 10 of `IUserRepository.cs`):
```csharp
Task<GroupRole?> GetGroupRoleAsync(int userId, int groupId);
Task<int?> SetGroupRoleAsync(int userId, int groupId, GroupRole role);
```

---

### `QuestBoard.Domain/Services/UserService.cs` (service, CRUD)

**Analog:** self — add `GetGroupRoleAsync` and `SetGroupRoleAsync` following existing delegation pattern

**Existing delegation pattern** (lines 53-66 of `UserService.cs`):
```csharp
// All methods delegate to identityService or repository — copy this pattern
public async Task<bool> IsInRoleAsync(ClaimsPrincipal user, string role)
{
    return await identityService.IsInRoleAsync(user, role);
}
```

**New methods to ADD** — follow the `GetUserAsync` pattern for userId resolution (lines 53-56):
```csharp
public async Task<GroupRole?> GetGroupRoleAsync(ClaimsPrincipal user, int groupId)
{
    var userId = await identityService.GetUserIdAsync(user);
    if (userId == null) return null;
    return await repository.GetGroupRoleAsync(userId.Value, groupId);
}

public async Task<int?> SetGroupRoleAsync(int userId, int groupId, GroupRole role)
{
    return await repository.SetGroupRoleAsync(userId, groupId, role);
}
```

**Fix for `GetAllPlayersAsync` / `GetAllDungeonMastersAsync`** (lines 37-43 — delegate to updated repository methods, signatures unchanged):
```csharp
// No change needed in UserService — just delegates to repository.GetAllPlayers(token)
// The fix is in UserRepository where the query changes from AspNetUserRoles to UserGroups
```

---

### `QuestBoard.Repository/UserRepository.cs` (repository, CRUD)

**Analog:** self — replace `DbContext.UserRoles` JOIN with `DbContext.UserGroups` JOIN

**Current broken pattern** (lines 16-35 of `UserRepository.cs`):
```csharp
// CURRENT — reads DbContext.UserRoles (AspNetUserRoles, empty after Phase 27) — REPLACE
public async Task<IList<User>> GetAllDungeonMasters(CancellationToken token = default)
{
    var entities = await DbSet
        .Where(u => DbContext.UserRoles
            .Any(ur => ur.UserId == u.Id &&
                      DbContext.Roles.Any(r => r.Id == ur.RoleId &&
                                            (r.Name == "DungeonMaster" || r.Name == "Admin"))))
        .ToListAsync(cancellationToken: token);
    return Mapper.Map<IList<User>>(entities);
}
```

**Target pattern** — UserGroups JOIN using IActiveGroupContext (inject in constructor alongside existing params):
```csharp
// NEW constructor adds IActiveGroupContext (already scoped, consistent with QuestBoardContext)
internal class UserRepository(QuestBoardContext dbContext, IMapper mapper, IActiveGroupContext activeGroupContext)
    : BaseRepository<User, UserEntity>(dbContext, mapper), IUserRepository

public async Task<IList<User>> GetAllDungeonMasters(CancellationToken token = default)
{
    var groupId = activeGroupContext.ActiveGroupId;
    if (groupId == null) return [];  // Pitfall 6: SuperAdmin null guard
    var entities = await DbSet
        .Where(u => DbContext.UserGroups
            .Any(ug => ug.UserId == u.Id
                    && ug.GroupId == groupId.Value
                    && (ug.GroupRole == (int)GroupRole.DungeonMaster || ug.GroupRole == (int)GroupRole.Admin)))
        .ToListAsync(cancellationToken: token);
    return Mapper.Map<IList<User>>(entities);
}

public async Task<IList<User>> GetAllPlayers(CancellationToken token = default)
{
    var groupId = activeGroupContext.ActiveGroupId;
    if (groupId == null) return [];
    var entities = await DbSet
        .Where(u => DbContext.UserGroups
            .Any(ug => ug.UserId == u.Id
                    && ug.GroupId == groupId.Value
                    && ug.GroupRole == (int)GroupRole.Player))
        .ToListAsync(cancellationToken: token);
    return Mapper.Map<IList<User>>(entities);
}

public async Task<GroupRole?> GetGroupRoleAsync(int userId, int groupId)
{
    var ug = await DbContext.UserGroups
        .FirstOrDefaultAsync(ug => ug.UserId == userId && ug.GroupId == groupId);
    if (ug == null) return null;
    return (GroupRole)ug.GroupRole;
}

public async Task<int?> SetGroupRoleAsync(int userId, int groupId, GroupRole role)
{
    var ug = await DbContext.UserGroups
        .FirstOrDefaultAsync(ug => ug.UserId == userId && ug.GroupId == groupId);
    if (ug == null) return null;
    ug.GroupRole = (int)role;
    await DbContext.SaveChangesAsync();
    return ug.Id;
}
```

---

### `QuestBoard.Service/Controllers/Admin/AdminController.cs` (controller, CRUD)

**Analog:** self — add `IActiveGroupContext` constructor param (CONTEXT.md Key Fact #3: it is NOT currently injected); fix promote/demote; fix Users role badges

**Constructor change** — add `IActiveGroupContext activeGroupContext` to existing primary constructor (line 19):
```csharp
// CURRENT (line 19):
public class AdminController(IUserService userService, IQuestService questService, IIdentityService identityService,
    IBackgroundJobClient jobClient, IHttpClientFactory httpClientFactory, IOptions<EmailSettings> emailOptions,
    IMemoryCache cache) : Controller

// TARGET — add IActiveGroupContext:
public class AdminController(IUserService userService, IQuestService questService, IIdentityService identityService,
    IBackgroundJobClient jobClient, IHttpClientFactory httpClientFactory, IOptions<EmailSettings> emailOptions,
    IMemoryCache cache, IActiveGroupContext activeGroupContext) : Controller
```

**Promote/demote fix pattern** (D-09) — replace `AddToRoleAsync`/`RemoveFromRoleAsync` calls (lines 53-115):
```csharp
// BEFORE (lines 62-64):
await userService.RemoveFromRoleAsync(user, "Player");
await userService.RemoveFromRoleAsync(user, "DungeonMaster");
await userService.AddToRoleAsync(user, "Admin");

// AFTER — use SetGroupRoleAsync (D-09):
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> PromoteToAdmin(int userId)
{
    var groupId = activeGroupContext.ActiveGroupId;
    if (groupId == null) return RedirectToAction(nameof(Users));
    await userService.SetGroupRoleAsync(userId, groupId.Value, GroupRole.Admin);
    return RedirectToAction(nameof(Users));
}

// Same pattern for DemoteFromAdmin (→ GroupRole.DungeonMaster),
// PromoteToDM (→ GroupRole.DungeonMaster), DemoteToPlayer (→ GroupRole.Player)
```

**Users action fix** (lines 22-48) — replace `GetRolesAsync` with UserGroups query for role badges:
```csharp
// AFTER — populate role flags from GetGroupRoleAsync instead of GetRolesAsync
foreach (var user in allUsers)
{
    var groupId = activeGroupContext.ActiveGroupId;
    GroupRole? groupRole = groupId.HasValue
        ? await userService.GetGroupRoleAsync(user, groupId.Value)  // need overload accepting User or int
        : null;
    userViewModels.Add(new UserManagementViewModel
    {
        User = user,
        IsAdmin = groupRole == GroupRole.Admin,
        IsDungeonMaster = groupRole == GroupRole.DungeonMaster,
        IsPlayer = groupRole == GroupRole.Player,
        EmailConfirmed = user.EmailConfirmed
    });
}
```

Note: `GetGroupRoleAsync` takes `ClaimsPrincipal` currently (D-06). A second overload `GetGroupRoleAsync(int userId, int groupId)` may be cleaner for the Users action — planner decides based on what's already being added to IUserRepository.

---

### `QuestBoard.Service/Program.cs` (config, request-response)

**Analog:** self — add SuperAdminOnly policy and area route

**SuperAdminOnly policy** — extend existing `AddAuthorizationBuilder()` chain (lines 64-68):
```csharp
// CURRENT (lines 64-68):
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("DungeonMasterOnly", policy =>
        policy.Requirements.Add(new DungeonMasterRequirement()))
    .AddPolicy("AdminOnly", policy =>
        policy.Requirements.Add(new AdminRequirement()));

// TARGET — add SuperAdminOnly:
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("DungeonMasterOnly", policy =>
        policy.Requirements.Add(new DungeonMasterRequirement()))
    .AddPolicy("AdminOnly", policy =>
        policy.Requirements.Add(new AdminRequirement()))
    .AddPolicy("SuperAdminOnly", policy =>
        policy.RequireRole("SuperAdmin"));  // built-in — no custom handler needed
```

**Area route** — add BEFORE the default route (line 204):
```csharp
// Add BEFORE existing default route:
app.MapControllerRoute(
    name: "platform",
    pattern: "platform/{controller=Group}/{action=Index}/{id?}",
    defaults: new { area = "Platform" },
    constraints: new { area = "Platform" });

// EXISTING (line 204):
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
```

---

### `QuestBoard.Repository/Extensions/ServiceExtensions.cs` (config, CRUD)

**Analog:** self — add `IGroupRepository` registration following existing `AddScoped` pattern (lines 18-26):
```csharp
// Existing pattern:
services.AddScoped<IUserRepository, UserRepository>();

// Add after existing repository registrations:
services.AddScoped<IGroupRepository, GroupRepository>();
```

---

### `QuestBoard.Domain/Extensions/ServiceExtensions.cs` (config, CRUD)

**Analog:** self — add `IGroupService` registration following existing pattern (lines 15-22):
```csharp
// Existing pattern:
services.AddScoped<IUserService, UserService>();

// Add after existing service registrations:
services.AddScoped<IGroupService, GroupService>();
```

---

### `QuestBoard.Repository/Migrations/AddSuperAdminRole.cs` (migration, batch)

**Analog:** `QuestBoard.Repository/Migrations/20250704211037_ConvertIsDungeonMasterToRoles.cs`

**Exact InsertData pattern** (lines 14-22 of `ConvertIsDungeonMasterToRoles.cs`):
```csharp
migrationBuilder.InsertData(
    table: "AspNetRoles",
    columns: new[] { "Id", "Name", "NormalizedName", "ConcurrencyStamp" },
    values: new object[,]
    {
        { 1, "Player", "PLAYER", Guid.NewGuid().ToString() },
        { 2, "DungeonMaster", "DUNGEONMASTER", Guid.NewGuid().ToString() },
        { 3, "Admin", "ADMIN", Guid.NewGuid().ToString() }
    });
```

**Target migration** (D-10) — single row, same format:
```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    migrationBuilder.InsertData(
        table: "AspNetRoles",
        columns: new[] { "Id", "Name", "NormalizedName", "ConcurrencyStamp" },
        values: new object[] { 4, "SuperAdmin", "SUPERADMIN", Guid.NewGuid().ToString() });
}

protected override void Down(MigrationBuilder migrationBuilder)
{
    migrationBuilder.DeleteData(
        table: "AspNetRoles",
        keyColumn: "Id",
        keyValue: 4);
}
```

---

### `QuestBoard.Domain/Interfaces/IGroupService.cs` (service interface, CRUD)

**Analog:** `QuestBoard.Domain/Interfaces/IQuestService.cs` (service interface with extra methods beyond IBaseService)

**Interface pattern** from `IQuestService.cs` (lines 1-10):
```csharp
using QuestBoard.Domain.Models;

namespace QuestBoard.Domain.Interfaces;

public interface IGroupService : IBaseService<Group>
{
    // Beyond base CRUD — specific group management methods:
    Task<IList<GroupWithMemberCount>> GetAllWithMemberCountAsync(CancellationToken token = default);
    Task<bool> HasMembersAsync(int groupId, CancellationToken token = default);
    Task AddMemberAsync(int groupId, int userId, GroupRole role, CancellationToken token = default);
    Task RemoveMemberAsync(int groupId, int userId, CancellationToken token = default);
    Task<IList<UserGroup>> GetMembersAsync(int groupId, CancellationToken token = default);
}
```

---

### `QuestBoard.Domain/Interfaces/IGroupRepository.cs` (repository interface, CRUD)

**Analog:** `QuestBoard.Domain/Interfaces/IUserRepository.cs` (lines 1-12) — exact pattern

**Pattern from `IUserRepository.cs`:**
```csharp
using QuestBoard.Domain.Models;

namespace QuestBoard.Domain.Interfaces;

public interface IGroupRepository : IBaseRepository<Group>
{
    Task<IList<GroupWithMemberCount>> GetAllWithMemberCountAsync(CancellationToken token = default);
    Task<bool> HasMembersAsync(int groupId, CancellationToken token = default);
    Task AddMemberAsync(int groupId, int userId, GroupRole groupRole, CancellationToken token = default);
    Task RemoveMemberAsync(int groupId, int userId, CancellationToken token = default);
    Task<IList<UserGroup>> GetMembersAsync(int groupId, CancellationToken token = default);
}
```

---

### `QuestBoard.Domain/Services/GroupService.cs` (service, CRUD)

**Analog:** `QuestBoard.Domain/Services/CharacterService.cs` — thin delegation service

**Pattern from `CharacterService.cs`** (lines 1-10):
```csharp
using AutoMapper;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;

namespace QuestBoard.Domain.Services;

internal class CharacterService(ICharacterRepository repository, IMapper mapper)
    : BaseService<Character>(repository, mapper), ICharacterService
```

**Target pattern:**
```csharp
internal class GroupService(IGroupRepository repository, IMapper mapper)
    : BaseService<Group>(repository, mapper), IGroupService
{
    public async Task<IList<GroupWithMemberCount>> GetAllWithMemberCountAsync(CancellationToken token = default)
        => await repository.GetAllWithMemberCountAsync(token);

    public async Task<bool> HasMembersAsync(int groupId, CancellationToken token = default)
        => await repository.HasMembersAsync(groupId, token);

    public async Task AddMemberAsync(int groupId, int userId, GroupRole role, CancellationToken token = default)
        => await repository.AddMemberAsync(groupId, userId, role, token);

    public async Task RemoveMemberAsync(int groupId, int userId, CancellationToken token = default)
        => await repository.RemoveMemberAsync(groupId, userId, token);

    public async Task<IList<UserGroup>> GetMembersAsync(int groupId, CancellationToken token = default)
        => await repository.GetMembersAsync(groupId, token);

    // Validation example from CharacterService lines 51-56 — guard clauses before delegation:
    public override async Task AddAsync(Group model, CancellationToken token = default)
    {
        // Guard: name required (already enforced by [Required] annotation but belt+suspenders)
        if (string.IsNullOrWhiteSpace(model.Name))
            throw new ArgumentException("Group name is required.", nameof(model));
        await base.AddAsync(model, token);
        // DbUpdateException for unique name violation bubbles up to controller
    }
}
```

---

### `QuestBoard.Repository/GroupRepository.cs` (repository, CRUD)

**Analog:** `QuestBoard.Repository/UserRepository.cs` — BaseRepository extension with extra queries

**Pattern from `UserRepository.cs`** (lines 1-9):
```csharp
using AutoMapper;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.Repository.Entities;
using Microsoft.EntityFrameworkCore;

namespace QuestBoard.Repository;

internal class GroupRepository(QuestBoardContext dbContext, IMapper mapper)
    : BaseRepository<Group, GroupEntity>(dbContext, mapper), IGroupRepository
```

**Member count query** (from RESEARCH.md Pattern 6 — verified GroupEntity.UserGroups nav property):
```csharp
public async Task<IList<GroupWithMemberCount>> GetAllWithMemberCountAsync(CancellationToken token = default)
{
    return await DbContext.Groups
        .Select(g => new GroupWithMemberCount
        {
            Id = g.Id,
            Name = g.Name,
            CreatedAt = g.CreatedAt,
            MemberCount = g.UserGroups.Count  // GroupEntity.UserGroups nav property confirmed
        })
        .ToListAsync(token);
}

public async Task<bool> HasMembersAsync(int groupId, CancellationToken token = default)
    => await DbContext.UserGroups.AnyAsync(ug => ug.GroupId == groupId, token);

public async Task AddMemberAsync(int groupId, int userId, GroupRole groupRole, CancellationToken token = default)
{
    // Check existence first (Pitfall 4: unique index on UserId+GroupId)
    var exists = await DbContext.UserGroups
        .AnyAsync(ug => ug.UserId == userId && ug.GroupId == groupId, token);
    if (exists) throw new InvalidOperationException("User is already a member of this group.");
    DbContext.UserGroups.Add(new UserGroupEntity
    {
        UserId = userId,
        GroupId = groupId,
        GroupRole = (int)groupRole
    });
    await DbContext.SaveChangesAsync(token);
}

public async Task RemoveMemberAsync(int groupId, int userId, CancellationToken token = default)
{
    var ug = await DbContext.UserGroups
        .FirstOrDefaultAsync(ug => ug.UserId == userId && ug.GroupId == groupId, token);
    if (ug == null) return;
    DbContext.UserGroups.Remove(ug);
    await DbContext.SaveChangesAsync(token);
}

public async Task<IList<UserGroup>> GetMembersAsync(int groupId, CancellationToken token = default)
{
    var entities = await DbContext.UserGroups
        .Include(ug => ug.User)
        .Where(ug => ug.GroupId == groupId)
        .ToListAsync(token);
    return Mapper.Map<IList<UserGroup>>(entities);
}
```

---

### `QuestBoard.Service/Areas/Platform/Controllers/GroupController.cs` (controller, CRUD)

**Analog:** `QuestBoard.Service/Controllers/Admin/AdminController.cs`

**Controller structure pattern** (lines 18-19 of `AdminController.cs`):
```csharp
// AdminController class declaration — copy structure, change attributes and services:
[Authorize(Policy = "AdminOnly")]
public class AdminController(IUserService userService, ...) : Controller
```

**Target pattern:**
```csharp
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.Domain.Enums;
using QuestBoard.Service.ViewModels.PlatformViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace QuestBoard.Service.Areas.Platform.Controllers;

[Area("Platform")]                              // REQUIRED — without this, area routing 404s
[Authorize(Policy = "SuperAdminOnly")]
public class GroupController(IGroupService groupService, IUserService userService) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var groups = await groupService.GetAllWithMemberCountAsync();
        return View(new GroupListViewModel { Groups = groups });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(GroupCreateViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        try
        {
            await groupService.AddAsync(new Group { Name = model.Name });
            return RedirectToAction(nameof(Index));
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("unique", StringComparison.OrdinalIgnoreCase) == true
                                         || ex.InnerException?.Message.Contains("duplicate", StringComparison.OrdinalIgnoreCase) == true)
        {
            ModelState.AddModelError(nameof(model.Name), "A group with this name already exists.");
            return View(model);
        }
    }
    // ... Edit, Delete, Members actions follow same GET/POST pattern
}
```

**DELETE action pattern** (lines 280-290 of `AdminController.cs` — HTTP DELETE + AJAX):
```csharp
// AdminController uses [HttpDelete] + fetch() for DeleteUser
// For group delete: use form POST with confirmation view (D-14 says "no modals") — simpler
[HttpGet]
public async Task<IActionResult> Delete(int id)
{
    var group = await groupService.GetByIdAsync(id);
    if (group == null) return RedirectToAction(nameof(Index));
    var hasMembers = await groupService.HasMembersAsync(id);
    if (hasMembers)
    {
        TempData["Error"] = "Cannot delete a group that has members.";
        return RedirectToAction(nameof(Index));
    }
    return View(group);
}

[HttpPost, ActionName("Delete")]
[ValidateAntiForgeryToken]
public async Task<IActionResult> DeleteConfirmed(int id)
{
    var group = await groupService.GetByIdAsync(id);
    if (group != null) await groupService.RemoveAsync(group);
    return RedirectToAction(nameof(Index));
}
```

---

### Platform Views (Index, Create, Edit, Delete, Members)

**Analog:** `QuestBoard.Service/Views/Admin/Users.cshtml` (table/list), `QuestBoard.Service/Views/Admin/EditUser.cshtml` (form)

**modern-card structure** (lines 10-17 of `Users.cshtml`):
```html
<div class="card modern-card">
    <div class="card-header modern-card-header">
        <h2 class="mb-0">
            <i class="fas fa-icon-name text-color me-2"></i>
            Page Title
        </h2>
    </div>
    <div class="card-body modern-card-body">
```

**Table pattern** (lines 38-46 of `Users.cshtml`):
```html
<div class="table-responsive">
    <table class="table table-striped table-hover">
        <thead>
            <tr>
                <th scope="col">Column1</th>
                <th scope="col">Column2</th>
                <th scope="col">Actions</th>
            </tr>
        </thead>
        <tbody>
            @foreach (var item in Model.Items)
            {
                <tr>...</tr>
            }
        </tbody>
    </table>
</div>
```

**Empty state pattern** (lines 169-173 of `Users.cshtml`):
```html
<div class="text-center py-5">
    <i class="fas fa-layer-group fa-3x text-muted mb-3"></i>
    <p class="text-muted">No groups found.</p>
</div>
```

**Form pattern with button layout** (lines 64-74 of `EditUser.cshtml`):
```html
<form asp-action="Create" method="post">
    <div asp-validation-summary="ModelOnly" class="text-danger mb-3"></div>
    <div class="mb-3">
        <label asp-for="Name" class="form-label"></label>
        <input asp-for="Name" class="form-control" />
        <span asp-validation-for="Name" class="text-danger"></span>
    </div>
    <hr>
    <div class="d-flex justify-content-between">
        <a asp-action="Index" class="btn btn-secondary">
            <i class="fas fa-arrow-left me-2"></i>Cancel
        </a>
        <button type="submit" class="btn btn-success">
            <i class="fas fa-save me-2"></i>Save
        </button>
    </div>
</form>
```

**TempData success/error alerts** (lines 18-34 of `Users.cshtml`):
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

---

### `QuestBoard.Service/Areas/Platform/Views/Shared/_Layout.Platform.cshtml` (view, request-response)

**Analog:** `QuestBoard.Service/Views/Shared/_Layout.cshtml` — stripped-down version

**Keep from `_Layout.cshtml`** (lines 1-17, head section):
```html
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>@ViewData["Title"] - D&D Quest Board Platform</title>
    <link href="https://cdn.jsdelivr.net/npm/bootstrap@5.3.0/dist/css/bootstrap.min.css" rel="stylesheet">
    <link href="https://cdnjs.cloudflare.com/ajax/libs/font-awesome/6.4.0/css/all.min.css" rel="stylesheet">
    <link rel="stylesheet" href="~/css/site.css" asp-append-version="true" />
</head>
```

**Strip from `_Layout.cshtml`** — remove: `<ul class="navbar-nav me-auto">` quest board nav links (Shop, QuestLog, GuildMembers, Players, Admin dropdown, DM dropdown)

**Replace nav body** with minimal platform nav (D-13):
```html
<nav class="navbar navbar-expand-lg navbar-dark bg-dark py-2">
    <div class="container-fluid px-3">
        <a class="navbar-brand" asp-controller="Group" asp-action="Index" asp-area="Platform">
            <i class="fas fa-dice-d20"></i> D&D Quest Board
        </a>
        <ul class="navbar-nav ms-auto">
            <li class="nav-item">
                <span class="navbar-text text-light me-3">
                    <i class="fas fa-user me-1"></i>@User.Identity?.Name
                </span>
            </li>
            <li class="nav-item">
                <a class="nav-link" asp-controller="Home" asp-action="Index" asp-area="">
                    <i class="fas fa-arrow-left me-1"></i>Back to quest board
                </a>
            </li>
            <li class="nav-item">
                <form asp-controller="Account" asp-action="Logout" asp-area="" method="post" class="d-inline">
                    <button type="submit" class="btn btn-link nav-link">
                        <i class="fas fa-sign-out-alt me-1"></i>Logout
                    </button>
                </form>
            </li>
        </ul>
    </div>
</nav>
```

**Keep from `_Layout.cshtml`** (lines 158-177, body/scripts):
```html
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

---

### `QuestBoard.Service/Areas/Platform/Views/_ViewImports.cshtml` (config, request-response)

**Analog:** `QuestBoard.Service/Views/_ViewImports.cshtml` (lines 1-15) — exact pattern, different namespace

**Pattern from root `_ViewImports.cshtml`:**
```razor
@using Microsoft.AspNetCore.Authorization
@using QuestBoard.Domain.Enums
@using QuestBoard.Domain.Interfaces
@using QuestBoard.Domain.Models
@using QuestBoard.Service
@using QuestBoard.Service.ViewModels.PlatformViewModels

@namespace QuestBoard.Service.Areas.Platform.Views   // DIFFERENT from root: QuestBoard.Service.Pages
@addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers
@inject IAuthorizationService AuthorizationService
```

**Critical difference:** `@namespace` MUST differ from root (use `QuestBoard.Service.Areas.Platform.Views` not `QuestBoard.Service.Pages`). This is the most common pitfall (Pitfall 2).

---

### `QuestBoard.Service/Areas/Platform/Views/_ViewStart.cshtml` (config, request-response)

**Analog:** `QuestBoard.Service/Views/_ViewStart.cshtml` (lines 1-7)

**Pattern from root `_ViewStart.cshtml`:**
```razor
@using QuestBoard.Domain.Interfaces
@{
    Layout = "_Layout";  // root uses conditional mobile layout
}
```

**Target** — always use platform layout (no mobile variants in platform area):
```razor
@{
    Layout = "_Layout.Platform";
}
```

---

### Integration Test Files

**Analog:** `QuestBoard.IntegrationTests/Controllers/AdminControllerIntegrationTests.cs`

**Test class structure** (lines 1-16 of `AdminControllerIntegrationTests.cs`):
```csharp
using QuestBoard.IntegrationTests.Helpers;
using System.Net;

namespace QuestBoard.IntegrationTests.Controllers;

public class AdminHandlerIntegrationTests : IClassFixture<WebApplicationFactoryBase>
{
    private readonly WebApplicationFactoryBase _factory;
    private readonly HttpClient _client;

    public AdminHandlerIntegrationTests(WebApplicationFactoryBase factory)
    {
        _factory = factory;
        _client = factory.CreateNonRedirectingClient();  // non-redirecting for auth tests
    }
```

**Auth test pattern** (lines 19-26 of `AdminControllerIntegrationTests.cs`):
```csharp
[Fact]
public async Task AdminPage_WhenNotAuthenticated_ShouldRedirectToLogin()
{
    var response = await _client.GetAsync("/Admin/Users", TestContext.Current.CancellationToken);
    response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.Unauthorized);
}

[Fact]
public async Task AdminPage_WhenNotAdmin_ShouldReturnForbidden()
{
    var (regularClient, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
        _factory, "regularuser", "regular@example.com", roles: ["Player"]);
    var response = await regularClient.GetAsync("/Admin/Users", TestContext.Current.CancellationToken);
    response.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.Redirect);
}
```

**SuperAdmin test pattern** — uses existing TestAuthHandler format `"Test userId:userName:email:SuperAdmin"`:
```csharp
public static async Task<(HttpClient client, UserEntity user)> CreateAuthenticatedSuperAdminClientAsync(
    WebApplicationFactory<Program> factory,
    string userName = "superadmin",
    string email = "superadmin@example.com",
    string name = "Super Admin User")
{
    return await CreateAuthenticatedClientWithUserAsync(factory, userName, email, "SuperAdmin123!", name, ["SuperAdmin"]);
}
```

**TestDataHelper.SeedRolesAsync fix** (lines 191-192 of `TestDataHelper.cs`) — add "SuperAdmin":
```csharp
// CURRENT:
string[] roleNames = ["Admin", "DungeonMaster", "Player"];
// TARGET:
string[] roleNames = ["Admin", "DungeonMaster", "Player", "SuperAdmin"];
```

---

## Shared Patterns

### Authorization (all controllers)
**Source:** `QuestBoard.Service/Program.cs` lines 62-68
**Apply to:** `GroupController` (SuperAdminOnly), existing AdminHandler/DungeonMasterHandler registrations unchanged
```csharp
builder.Services.AddScoped<IAuthorizationHandler, DungeonMasterHandler>();
builder.Services.AddScoped<IAuthorizationHandler, AdminHandler>();
```

### Primary Constructor DI Pattern
**Source:** All existing services/repositories use C# 12 primary constructor syntax
**Apply to:** All new files (`GroupService`, `GroupRepository`, `GroupController`)
```csharp
// Primary constructor — not classic constructor with `this.field = field` assignments
internal class GroupService(IGroupRepository repository, IMapper mapper)
    : BaseService<Group>(repository, mapper), IGroupService
```

### ValidateAntiForgeryToken on all POSTs
**Source:** `AdminController.cs` lines 51, 70, 86, 100, 199, 241
**Apply to:** All POST/DELETE actions in `GroupController`
```csharp
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> Create(...)
```

### TempData for flash messages
**Source:** `AdminController.cs` lines 169, 261; `Users.cshtml` lines 18-34
**Apply to:** All GroupController POST actions that redirect after success/failure
```csharp
TempData["Success"] = "Group created successfully.";
TempData["Error"] = "Cannot delete a group that has members.";
```

### DbUpdateException for unique constraint violations
**Source:** RESEARCH.md Pitfall 3 — `GroupEntity.Name` has DB-level unique index
**Apply to:** `GroupController.Create` and `GroupController.Edit` POST actions
```csharp
catch (DbUpdateException ex) when (
    ex.InnerException?.Message.Contains("unique", StringComparison.OrdinalIgnoreCase) == true ||
    ex.InnerException?.Message.Contains("duplicate", StringComparison.OrdinalIgnoreCase) == true)
{
    ModelState.AddModelError(nameof(model.Name), "A group with this name already exists.");
    return View(model);
}
```

### asp-area tag helper for cross-area links
**Source:** RESEARCH.md Pattern 1 — Area routing requires area attribute
**Apply to:** `_Layout.Platform.cshtml` links back to main quest board
```html
<!-- Links FROM platform area back to main area use asp-area="" (empty string = default area) -->
<a asp-controller="Home" asp-action="Index" asp-area="">Back to quest board</a>
```

---

## No Analog Found

All files have close analogs. No entries in this section.

---

## Metadata

**Analog search scope:** Full repository — `QuestBoard.Service/`, `QuestBoard.Domain/`, `QuestBoard.Repository/`, `QuestBoard.IntegrationTests/`
**Files scanned:** ~25 source files read directly
**Pattern extraction date:** 2026-06-30

**Key cross-cutting facts for planner:**
1. `AdminController` does NOT currently inject `IActiveGroupContext` — this is a confirmed fact (line 19 of `AdminController.cs`) contradicting CONTEXT.md
2. `IUserRepository.GetAllPlayers` / `GetAllDungeonMasters` currently JOIN on `DbContext.UserRoles` (AspNetUserRoles, empty) — must switch to `DbContext.UserGroups`
3. `TestDataHelper.SeedRolesAsync` must add "SuperAdmin" to the role list (line 191)
4. `AuthenticationHelper` needs `CreateAuthenticatedSuperAdminClientAsync` — existing `CreateAuthenticatedClientWithUserAsync` already supports any roles array
5. `_ViewStart.cshtml` in the Area must use `"_Layout.Platform"` (not `"_Layout"`)
6. Area `_ViewImports.cshtml` namespace must be `QuestBoard.Service.Areas.Platform.Views` (not `QuestBoard.Service.Pages`)
