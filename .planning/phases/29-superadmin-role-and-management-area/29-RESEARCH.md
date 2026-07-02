# Phase 29: SuperAdmin Role & Management Area - Research

**Researched:** 2026-06-30
**Domain:** ASP.NET Core 10 MVC — Authorization Handlers, Identity Roles, EF Core, MVC Areas
**Confidence:** HIGH

---

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**Auth Handlers**
- D-01: AdminHandler and DungeonMasterHandler each gain a second constructor parameter `IActiveGroupContext`. Both are already Scoped; no registration change needed.
- D-02: SuperAdmin bypass: each handler checks `context.User.IsInRole("SuperAdmin")` first. If true → `context.Succeed(requirement)` immediately and return.
- D-03: Null group guard: if `activeGroupContext.ActiveGroupId` is null after the SuperAdmin check → `context.Fail()`. Regular users without an active group session cannot pass any auth check.
- D-04: Group role check: call `await userService.GetGroupRoleAsync(context.User, activeGroupId.Value)`. AdminHandler: succeed if result is `GroupRole.Admin`. DungeonMasterHandler: succeed if result is `GroupRole.Admin` or `GroupRole.DungeonMaster`.
- D-05: `IActiveGroupContext` is NOT changed. It stays `int? ActiveGroupId { get; }` — no `IsSuperAdmin` property. The Phase 28 deferred item (add `IsSuperAdmin`) is explicitly cancelled. `HasQueryFilter` predicate stays `context.ActiveGroupId == null || e.GroupId == context.ActiveGroupId`.

**IUserService Extensions**
- D-06: Add `Task<GroupRole?> GetGroupRoleAsync(ClaimsPrincipal user, int groupId)` to `IUserService` and implement in `UserService`. Queries `UserGroups` where `UserId == userId && GroupId == groupId`. Returns null if no membership row found.
- D-07: Update `GetAllPlayersAsync` to query `UserGroups.GroupRole == GroupRole.Player` for `IActiveGroupContext.ActiveGroupId`.
- D-08: Update `GetAllDungeonMastersAsync` to query `UserGroups.GroupRole` is DungeonMaster or Admin for the active group.

**AdminController Promote/Demote Fix**
- D-09: PromoteToAdmin, DemoteFromAdmin, PromoteToDM, DemoteToPlayer update `UserGroups.GroupRole` for `IActiveGroupContext.ActiveGroupId`. AdminController already has IActiveGroupContext injected — no new constructor parameter.

**SuperAdmin Identity Role Seeding**
- D-10: SuperAdmin role created via EF Core migration `InsertData` into `AspNetRoles`: `Id = 4, Name = "SuperAdmin", NormalizedName = "SUPERADMIN"`. Consistent with ConvertIsDungeonMasterToRoles pattern.
- D-11: First SuperAdmin user assignment is a manual post-deploy step (SQL INSERT into AspNetUserRoles). No startup automation.

**/platform MVC Area**
- D-12: New MVC Area named `Platform`, routed at `/platform`. Directory structure: `QuestBoard.Service/Areas/Platform/Controllers/` and `QuestBoard.Service/Areas/Platform/Views/`. Protected by `SuperAdminOnly` authorization policy (`[Authorize(Policy = "SuperAdminOnly")]` on the area controller).
- D-13: Dedicated `_Layout.Platform.cshtml` in `Areas/Platform/Views/Shared/`. Logo, logged-in user name, logout button, "Back to quest board" link. No quest board navigation.
- D-14: Visual style: modern-card pattern (mandatory per CLAUDE.md). Tables for group and member lists. Standard form-based create/edit/delete (no modals required).
- D-15: Pages in scope: Groups index, Create group, Edit group, Delete group (only if 0 members), Group detail / members (list members, add existing user with GroupRole picker, remove user).

### Claude's Discretion

- Exact `SuperAdminOnly` policy registration in `Program.cs`
- `GroupController` vs. `PlatformController` naming inside the Area
- Whether group detail and member management live on one controller or two
- Exact column layout and button placement in the platform views (follow CLAUDE.md card pattern)

### Deferred Ideas (OUT OF SCOPE)

- Platform visual polish (charts, badges, animations)
- SuperAdmin link in the main quest board nav — Phase 30
- `IsSuperAdmin` on `IActiveGroupContext` — explicitly cancelled
- Group admin user creation and role promotion/demotion within a group — Phase 30 (MGMT-07, MGMT-08)
</user_constraints>

---

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| AUTH-01 | SuperAdmin role added to AspNetRoles and seedable at startup | EF migration InsertData pattern confirmed in ConvertIsDungeonMasterToRoles — D-10 |
| AUTH-02 | AdminHandler updated to check UserGroups.GroupRole == Admin for active group | Current AdminHandler reads AspNetUserRoles via IsInRoleAsync — must be rewritten per D-01–D-04 |
| AUTH-03 | DungeonMasterHandler updated to check UserGroups.GroupRole is DM or Admin for active group | Same rewrite pattern as AdminHandler — D-01–D-04 |
| AUTH-04 | Both handlers grant access when user holds SuperAdmin Identity role, regardless of active group | ClaimsPrincipal.IsInRole("SuperAdmin") check — D-02 |
| AUTH-05 | SuperAdminOnly authorization policy exists, used to protect management area | Standard AddPolicy in AddAuthorizationBuilder — confirmed in Program.cs |
| MGMT-01 | Dedicated MVC Area for SuperAdmin group management at /platform | First Area in the project — MVC Area mechanics confirmed |
| MGMT-02 | SuperAdmin can view a list of all groups with member counts | Requires GroupRepository.GetAllWithMemberCountAsync — new method |
| MGMT-03 | SuperAdmin can create a new group (name required) | GroupEntity.Name has unique DB index — must catch DbUpdateException |
| MGMT-04 | SuperAdmin can edit a group's name or delete an empty group | Delete must check UserGroups.Count == 0 before removing |
| MGMT-05 | SuperAdmin can add any existing user to any group and assign GroupRole | UserGroups has unique index on (UserId, GroupId) — must handle duplicate membership |
| MGMT-06 | SuperAdmin can remove a user from a group | Delete UserGroups row by Id or (UserId, GroupId) composite |
</phase_requirements>

---

## Summary

Phase 29 adds the SuperAdmin Identity role, fixes authorization handlers to read per-group roles from `UserGroups.GroupRole` instead of the now-empty `AspNetUserRoles`, and creates the first MVC Area in the project (`/platform`) for SuperAdmin group management.

The codebase is well-prepared. `GroupEntity`, `UserGroupEntity`, `IActiveGroupContext`, and `MutableGroupContext` are all already implemented from Phases 27–28. The three remaining work streams are: (1) rewrite the two auth handlers — both currently call `userService.IsInRoleAsync` which reads from `AspNetUserRoles`, now empty for Player/DM/Admin after Phase 27; (2) add `GetGroupRoleAsync`, fix `GetAllPlayersAsync` and `GetAllDungeonMastersAsync` in `IUserService`/`UserService` and `IUserRepository`/`UserRepository` to read from `UserGroups`; and (3) scaffold the Platform MVC Area with a `GroupController` and five views wired to a new `IGroupService`/`GroupRepository`.

There are no new DB schema changes in this phase — a single migration adds only the SuperAdmin row to `AspNetRoles` (Id=4). All other changes are code-only.

**Primary recommendation:** Layer the work as three independent tracks that can be planned in separate waves: auth-handler rewrite (Wave 1 foundation), IUserService/repository fixes (Wave 1 companion), and MVC Area + GroupRepository (Wave 2).

---

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| SuperAdmin role seeding (AspNetRoles) | Database / EF Migration | — | Row insertion into Identity tables — migration responsibility |
| Authorization handler logic (AdminHandler, DungeonMasterHandler) | Service Layer | Domain (IUserService) | Handlers live in QuestBoard.Service/Authorization; depend on Domain service interface |
| GetGroupRoleAsync / GetAllPlayers / GetAllDMs | Domain Service (UserService) | Repository (UserRepository) | Business logic in Domain; DB query in Repository |
| AdminController promote/demote fix | Service Layer (Controller) | Domain (via IUserService) | Controller delegates to service; no new layers needed |
| SuperAdminOnly policy registration | Service Layer (Program.cs) | — | Authorization policy configuration in DI setup |
| GroupRepository CRUD | Repository | — | EF Core DbSet access — Repository responsibility |
| IGroupService interface | Domain | — | Service interface belongs in Domain layer per architecture |
| GroupService business logic | Domain | Repository | Domain Service delegates to Repository |
| Platform Area controller | Service Layer (Area) | Domain (IGroupService) | MVC controller in Service project; depends on Domain interface |
| Platform Area views | Service Layer (Views) | — | Razor views in Service project under Areas/ |

---

## Standard Stack

### Core (all verified in codebase — no new libraries needed)

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Microsoft.AspNetCore.Authorization | Ships with .NET 10 | `IAuthorizationHandler`, `AuthorizationHandler<T>`, `AddAuthorizationBuilder()` | Built-in framework |
| Microsoft.AspNetCore.Identity | Ships with .NET 10 | `UserManager<T>`, `IdentityRole<int>`, `ClaimsPrincipal.IsInRole` | Built-in Identity framework |
| Microsoft.EntityFrameworkCore | Ships with .NET 10 | LINQ queries on `UserGroups` DbSet | Already in Repository project |
| AutoMapper | Existing dependency | GroupEntity ↔ Group mapping (already exists) | Already wired in EntityProfile |

[VERIFIED: codebase grep — no new NuGet packages needed for this phase]

**No new NuGet packages required.** All capabilities come from the existing stack.

---

## Architecture Patterns

### System Architecture Diagram

```
HTTP Request
     │
     ▼
[ASP.NET Core Middleware]
     │
     ├── /platform/* ──► [Platform Area: GroupController]
     │                          │ [Authorize(Policy="SuperAdminOnly")]
     │                          │
     │                    [IGroupService] ──► [GroupRepository] ──► [QuestBoardContext]
     │                          │                                        │ Groups DbSet
     │                          │                                        │ UserGroups DbSet
     │                          └────────────────────────────────────────┘
     │
     └── /Admin/* ────► [AdminController]
     │                          │ [Authorize(Policy="AdminOnly")]
     │                          │
     │                    [IUserService] ──► [IUserRepository] ──► [QuestBoardContext]
     │                                                                  │ UserGroups DbSet
     │                                                                  │ (NO AspNetUserRoles)
     │
AuthorizationMiddleware
     │
     ├── AdminOnly Policy ──► [AdminHandler]
     │                              │
     │                   1. IsInRole("SuperAdmin")? → Succeed
     │                   2. ActiveGroupId == null? → Fail
     │                   3. GetGroupRoleAsync → Admin? → Succeed
     │
     └── DungeonMasterOnly Policy ──► [DungeonMasterHandler]
                                            │
                                 1. IsInRole("SuperAdmin")? → Succeed
                                 2. ActiveGroupId == null? → Fail
                                 3. GetGroupRoleAsync → Admin|DM? → Succeed
```

### Recommended Project Structure (additions only)

```
QuestBoard.Service/
├── Areas/
│   └── Platform/
│       ├── Controllers/
│       │   └── GroupController.cs          # [Area("Platform")] [Authorize(Policy="SuperAdminOnly")]
│       └── Views/
│           ├── Group/
│           │   ├── Index.cshtml            # MGMT-02: list all groups with member counts
│           │   ├── Create.cshtml           # MGMT-03: create group form
│           │   ├── Edit.cshtml             # MGMT-04: rename group form
│           │   ├── Delete.cshtml           # MGMT-04: confirm delete (guard: 0 members)
│           │   └── Members.cshtml          # MGMT-05/06: member list + add/remove
│           ├── Shared/
│           │   └── _Layout.Platform.cshtml # minimal platform layout (D-13)
│           ├── _ViewImports.cshtml         # REQUIRED — tag helpers + @using for Area views
│           └── _ViewStart.cshtml           # sets Layout = "_Layout.Platform"
└── ViewModels/
    └── PlatformViewModels/
        ├── GroupListViewModel.cs
        ├── GroupMembersViewModel.cs
        └── AddMemberViewModel.cs

QuestBoard.Domain/
├── Interfaces/
│   └── IGroupService.cs                    # new — GetAllWithMemberCountAsync, Create, Rename, Delete, AddMember, RemoveMember
└── Models/
    └── GroupWithMemberCount.cs             # or extend Group.cs — new DTO

QuestBoard.Repository/
├── Interfaces/
│   └── IGroupRepository.cs                 # new (in Domain/Interfaces per existing pattern)
└── GroupRepository.cs                      # new — implements IGroupRepository
```

Note: `IGroupRepository` follows the same pattern as `IUserRepository` — defined in `QuestBoard.Domain/Interfaces/`, implemented in `QuestBoard.Repository/`.

### Pattern 1: MVC Area Registration

**What:** Areas are scoped route namespaces with their own Controllers/Views directories.
**When to use:** Feature isolation where a distinct URL prefix, layout, and authorization scope is needed.

```csharp
// Program.cs — add area route BEFORE the default route
app.MapControllerRoute(
    name: "platform",
    pattern: "platform/{controller=Group}/{action=Index}/{id?}",
    defaults: new { area = "Platform" },
    constraints: new { area = "Platform" });

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
```

Area controller attribute (REQUIRED alongside `[Authorize]`):

```csharp
// Source: ASP.NET Core 10 MVC Areas documentation
[Area("Platform")]
[Authorize(Policy = "SuperAdminOnly")]
public class GroupController(IGroupService groupService, IUserService userService) : Controller
{
    // ...
}
```

[VERIFIED: codebase — Program.cs currently has only the default route; area route must be added before it]

### Pattern 2: Auth Handler with IActiveGroupContext

**What:** Two-parameter primary constructor handler; SuperAdmin bypass first, then null guard, then group role lookup.

```csharp
// Source: verified AdminHandler.cs + D-01 through D-04 decisions
public class AdminHandler(
    IUserService userService,
    IActiveGroupContext activeGroupContext)
    : AuthorizationHandler<AdminRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        AdminRequirement requirement)
    {
        // Step 1: SuperAdmin bypass (D-02)
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

[VERIFIED: existing handler pattern in codebase; D-01–D-04 from CONTEXT.md]

### Pattern 3: GetGroupRoleAsync Implementation

**What:** New method on IUserService / UserService / IUserRepository / UserRepository chain.
**Layer chain:** IUserService → UserService → IUserRepository → UserRepository → QuestBoardContext.UserGroups

```csharp
// Domain/Services/UserService.cs (new method)
public async Task<GroupRole?> GetGroupRoleAsync(ClaimsPrincipal user, int groupId)
{
    var userId = await identityService.GetUserIdAsync(user);
    if (userId == null) return null;
    return await repository.GetGroupRoleAsync(userId.Value, groupId);
}
```

```csharp
// Repository/UserRepository.cs (new method)
public async Task<GroupRole?> GetGroupRoleAsync(int userId, int groupId)
{
    var ug = await DbContext.UserGroups
        .FirstOrDefaultAsync(ug => ug.UserId == userId && ug.GroupId == groupId);
    if (ug == null) return null;
    return (GroupRole)ug.GroupRole;
}
```

[VERIFIED: QuestBoardContext.UserGroups DbSet confirmed, UserGroupEntity.GroupRole is int, GroupRole enum confirmed in codebase]

### Pattern 4: GetAllPlayersAsync / GetAllDungeonMastersAsync Fix

**What:** Replace AspNetUserRoles JOIN with UserGroups JOIN in UserRepository.

```csharp
// Repository/UserRepository.cs — replaces current AspNetUserRoles query
public async Task<IList<User>> GetAllPlayers(CancellationToken token = default)
{
    // UserGroups filter requires activeGroupContext — inject into repository
    // OR pass groupId as parameter (see Anti-Patterns below for injection approach)
    var entities = await DbSet
        .Where(u => DbContext.UserGroups
            .Any(ug => ug.UserId == u.Id
                    && ug.GroupId == /* activeGroupId */
                    && ug.GroupRole == (int)GroupRole.Player))
        .ToListAsync(token);
    return Mapper.Map<IList<User>>(entities);
}
```

**Note on activeGroupId source in Repository:** `UserRepository` currently does NOT receive `IActiveGroupContext`. Two options:
- Inject `IActiveGroupContext` into `UserRepository` constructor (consistent with how `QuestBoardContext` receives it)
- Add `int groupId` parameter to `GetAllPlayers` / `GetAllDungeonMasters` method signatures (interface change propagates up)

[ASSUMED] — The cleaner approach is injecting `IActiveGroupContext` into `UserRepository` since it is already in the Domain layer and available to Repository. Parameter approach requires interface changes up to the controller. However, the planner should confirm which approach fits the existing pattern — `QuestBoardContext` already takes `IActiveGroupContext` as a constructor parameter, so repository injection is precedented.

### Pattern 5: Migration for SuperAdmin Role

**What:** Single `InsertData` step — no table creation.

```csharp
// Migration: AddSuperAdminRole.cs
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

[VERIFIED: ConvertIsDungeonMasterToRoles migration — exact InsertData format confirmed; roles 1=Player, 2=DungeonMaster, 3=Admin, 4=SuperAdmin is safe]

### Pattern 6: GroupRepository with Member Count

**What:** GroupRepository extends BaseRepository; needs non-standard query for member counts.

```csharp
public class GroupRepository(QuestBoardContext dbContext, IMapper mapper)
    : BaseRepository<Group, GroupEntity>(dbContext, mapper), IGroupRepository
{
    public async Task<IList<GroupWithMemberCount>> GetAllWithMemberCountAsync(CancellationToken token = default)
    {
        return await DbContext.Groups
            .Select(g => new GroupWithMemberCount
            {
                Id = g.Id,
                Name = g.Name,
                CreatedAt = g.CreatedAt,
                MemberCount = g.UserGroups.Count
            })
            .ToListAsync(token);
    }

    public async Task<bool> HasMembersAsync(int groupId, CancellationToken token = default)
    {
        return await DbContext.UserGroups.AnyAsync(ug => ug.GroupId == groupId, token);
    }
}
```

[VERIFIED: GroupEntity.UserGroups nav property confirmed in codebase; BaseRepository<Group, GroupEntity> pattern confirmed]

Note: `GroupRepository` does NOT use `HasQueryFilter` — Groups table has no filter. However, the inherited `GetAllAsync()` from `BaseRepository` works correctly since `Groups` DbSet is unfiltered.

### Pattern 7: SuperAdminOnly Policy Registration

**What:** Standard `AddPolicy` call in the existing `AddAuthorizationBuilder()` chain.

```csharp
// Program.cs — extend existing AddAuthorizationBuilder chain
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("DungeonMasterOnly", policy =>
        policy.Requirements.Add(new DungeonMasterRequirement()))
    .AddPolicy("AdminOnly", policy =>
        policy.Requirements.Add(new AdminRequirement()))
    .AddPolicy("SuperAdminOnly", policy =>
        policy.RequireRole("SuperAdmin"));
```

[VERIFIED: Program.cs AddAuthorizationBuilder confirmed; RequireRole is built-in, no custom handler needed for SuperAdminOnly]

### Pattern 8: Area _ViewImports.cshtml

**What:** Required file — without it, Area views do not get tag helpers or `@using` directives.

```razor
@* QuestBoard.Service/Areas/Platform/Views/_ViewImports.cshtml *@
@using Microsoft.AspNetCore.Authorization
@using QuestBoard.Domain.Enums
@using QuestBoard.Domain.Models
@using QuestBoard.Service
@using QuestBoard.Service.ViewModels.PlatformViewModels
@namespace QuestBoard.Service.Areas.Platform.Views
@addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers
@inject IAuthorizationService AuthorizationService
```

[VERIFIED: existing Views/_ViewImports.cshtml — pattern confirmed; namespace must differ from root _ViewImports]

### Anti-Patterns to Avoid

- **Hand-rolling group CRUD with raw SQL:** Use EF Core DbSet — migration already created Groups + UserGroups tables with proper constraints.
- **Putting IGroupRepository interface in QuestBoard.Repository.Interfaces:** The Domain-layer interfaces (`IUserRepository`, `IQuestRepository`, etc.) all live in `QuestBoard.Domain/Interfaces/`. `IGroupRepository` belongs there too.
- **Skipping `[Area("Platform")]` attribute on GroupController:** Without it, routing ignores the Area convention and /platform/ requests will 404.
- **Using `AddScoped<IActiveGroupContext, ActiveGroupContextService>()` alone:** Program.cs uses the dual-registration pattern (D-09 from Phase 28). Do not change this. Adding new registrations (GroupRepository, IGroupService) follows the same AddScoped pattern in the extension methods.
- **Checking `!context.User.Identity?.IsAuthenticated == true`:** The existing handlers contain this expression — it has a C# operator precedence bug (evaluates as `(!(bool?)) == true` which is always `false == true`). The Phase 29 rewrite drops this entire authentication check (ASP.NET Core's middleware already enforces authentication before handlers run). Do NOT copy this pattern into new handler code.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| SuperAdmin policy enforcement | Custom auth filter | `policy.RequireRole("SuperAdmin")` in AddAuthorizationBuilder | Built-in ASP.NET Core Identity role claim check — single line |
| Duplicate group name error handling | Try/catch + raw SQL check | Catch `DbUpdateException` on SaveChangesAsync — check inner exception for unique constraint | EF Core surfaces constraint violations; check-then-act is a TOCTOU race |
| Duplicate UserGroup membership | Check before insert | Catch `DbUpdateException` or check existence first | UserGroups has unique index on (UserId, GroupId); EF throws on duplicate |
| Member count per group | N+1 per-group query | `.Select(g => new { g, MemberCount = g.UserGroups.Count })` in single LINQ query | EF Core generates one SQL with COUNT subquery |
| Anti-forgery on Area forms | Custom CSRF | `[ValidateAntiForgeryToken]` + `asp-antiforgery="true"` on forms | Already wired in test infrastructure; standard MVC pattern |

**Key insight:** SuperAdmin access control, duplicate constraint handling, and related-count queries all have first-class EF Core / ASP.NET Core solutions that handle edge cases the hand-rolled versions miss.

---

## Common Pitfalls

### Pitfall 1: Missing [Area("Platform")] attribute
**What goes wrong:** GroupController is discovered by `AddControllersWithViews()` but the Area route does not map to it. `/platform/Group/Index` returns 404.
**Why it happens:** MVC Areas require BOTH a matching area route in `Program.cs` AND `[Area("AreaName")]` on the controller. Missing either breaks routing.
**How to avoid:** Always add both. Area route must come before the default route in `Program.cs`.
**Warning signs:** `/platform/Group/Index` returns 404 after wiring; the controller appears discovered in build output but is unreachable.

### Pitfall 2: Missing _ViewImports.cshtml in Area Views folder
**What goes wrong:** Area views cannot use tag helpers (`asp-action`, `asp-controller`, `asp-for`). Razor compilation may succeed but HTML is rendered without tag helper processing — forms have no action attributes.
**Why it happens:** The root `Views/_ViewImports.cshtml` does not cascade into Areas. Each Area's `Views/` folder needs its own `_ViewImports.cshtml`.
**How to avoid:** Create `Areas/Platform/Views/_ViewImports.cshtml` in Wave 0. Use a different `@namespace` from the root imports (e.g., `QuestBoard.Service.Areas.Platform.Views`).
**Warning signs:** `asp-action` links render as raw HTML attributes without resolved URLs; tag helper intellisense absent in IDE.

### Pitfall 3: GroupEntity unique index on Name
**What goes wrong:** Creating a group with a name that already exists throws `DbUpdateException` (SQL Server unique constraint violation) instead of returning a user-friendly validation error.
**Why it happens:** `GroupEntity.Name` has a DB-level unique index (confirmed in QuestBoardContext.OnModelCreating). EF throws on `SaveChangesAsync`.
**How to avoid:** In `GroupController.Create` [HttpPost], wrap `await groupService.CreateAsync(...)` in try/catch `DbUpdateException`. Check `ex.InnerException?.Message` for "unique" / "duplicate" and add `ModelState.AddModelError`. Alternatively, check for existence before insert (but this is a TOCTOU race on concurrent requests).
**Warning signs:** Unhandled exception page on duplicate group name submission.

### Pitfall 4: UserGroups unique index on (UserId, GroupId)
**What goes wrong:** Adding a user to a group they are already in throws `DbUpdateException`.
**Why it happens:** `UserGroups` has a unique composite index on `(UserId, GroupId)`.
**How to avoid:** In the "add member" action, check `UserGroups.AnyAsync(ug => ug.UserId == userId && ug.GroupId == groupId)` before inserting. If exists, show error; don't try to insert.
**Warning signs:** Unhandled exception when "Add Member" is submitted for an existing member.

### Pitfall 5: AdminHandler / DungeonMasterHandler registration — NO change needed
**What goes wrong:** A planner assumes DI registration must change because the constructor changed (now two parameters).
**Why it happens:** ASP.NET Core DI resolves constructor parameters automatically for registered types. Both `IUserService` (already Scoped) and `IActiveGroupContext` (already Scoped via dual pattern) are resolvable. Registration line stays `services.AddScoped<IAuthorizationHandler, AdminHandler>()`.
**How to avoid:** Do not touch DI registration lines for the handlers. Only the constructor and `HandleRequirementAsync` body change.
**Warning signs:** Over-engineering — registering handlers with factory methods or explicit parameter passing.

### Pitfall 6: GetAllPlayersAsync returns empty for SuperAdmin
**What goes wrong:** When SuperAdmin has no active group (null ActiveGroupId), the group-filtered query for players/DMs returns nothing.
**Why it happens:** After the fix, GetAllPlayers queries by group. SuperAdmin's ActiveGroupId is null (sees all at the EF filter level), but the explicit group filter in GetAllPlayers still requires a groupId.
**How to avoid:** GetAllPlayersAsync and GetAllDungeonMastersAsync must handle the null case. When `ActiveGroupId` is null (SuperAdmin context), return all users or return empty — decide per the phase scope. Since SuperAdmin does not use `/players` or DM listing pages (those are group-contextual), returning empty is acceptable. The key is that these methods do not throw on null. Alternatively, guard with `if (activeGroupId == null) return [];` at the top.
**Warning signs:** NullReferenceException or "Value cannot be null" in player/DM listing when SuperAdmin is logged in.

### Pitfall 7: IsSuperAdmin deferred — query filter remains "null = see all"
**What goes wrong:** After Phase 29, SuperAdmin navigates to the main quest board and sees ALL groups' quests (null = see all in the HasQueryFilter). This may appear wrong but is intentional until Phase 30.
**Why it happens:** D-05 explicitly cancels adding `IsSuperAdmin` to `IActiveGroupContext`. The filter stays `context.ActiveGroupId == null || e.GroupId == context.ActiveGroupId`. SuperAdmin's session never sets a group, so null = see all.
**How to avoid:** Do not add `IsSuperAdmin` to `IActiveGroupContext`. Do not modify `HasQueryFilter` predicates. Phase 30 handles group picker enforcement.
**Warning signs:** Temptation to "fix" the "bug" of SuperAdmin seeing all groups' quests — this is correct Phase 29 behavior.

### Pitfall 8: AdminController.Users shows wrong role info after handlers are fixed
**What goes wrong:** `AdminController.Users` calls `userService.GetRolesAsync(user)` which reads from `AspNetUserRoles` (now empty for Player/DM/Admin). The view's role badges (`IsAdmin`, `IsDungeonMaster`, `IsPlayer`) will all be false.
**Why it happens:** `GetRolesAsync` uses Identity's `GetRolesAsync` which reads `AspNetUserRoles`. After Phase 27 cleared Player/DM/Admin rows, this returns empty.
**How to avoid:** The D-07/D-08 fixes update `GetAllPlayersAsync` and `GetAllDungeonMastersAsync` to use UserGroups. But the `Users` page renders per-user role badges by checking `roles.Contains("Admin")` etc. from `GetRolesAsync`. This method also needs updating, OR the `UserManagementViewModel` role flags must be populated via UserGroups query instead of `GetRolesAsync`. This is a pre-existing breakage from Phase 27 that Phase 29 must fix. The `AdminController.Users` action must be updated to populate `IsAdmin`/`IsDungeonMaster`/`IsPlayer` from `UserGroups` for the active group rather than from `AspNetUserRoles`.
**Warning signs:** Admin > User Management page shows all users as having no role (no badges visible).

---

## Code Examples

### Current AdminHandler (to be replaced)

```csharp
// Source: QuestBoard.Service/Authorization/AdminHandler.cs [VERIFIED]
public class AdminHandler(IUserService userService) : AuthorizationHandler<AdminRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        AdminRequirement requirement)
    {
        // BUG: !context.User.Identity?.IsAuthenticated == true
        //      This is always false due to operator precedence; auth check is ineffective
        if (!context.User.Identity?.IsAuthenticated == true)
        {
            context.Fail();
            return;
        }

        var isAdmin = await userService.IsInRoleAsync(context.User, "Admin");
        // AspNetUserRoles is now empty for "Admin" (Phase 27 cleared it)
        // isAdmin is always false → context.Fail() for all users

        if (isAdmin) context.Succeed(requirement);
        else context.Fail();
    }
}
```

### Updated AdminHandler (target state)

```csharp
// Source: D-01 through D-04, QuestBoard.Service/Authorization/AdminHandler.cs [CITED: 29-CONTEXT.md]
public class AdminHandler(
    IUserService userService,
    IActiveGroupContext activeGroupContext)
    : AuthorizationHandler<AdminRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        AdminRequirement requirement)
    {
        // Step 1: SuperAdmin bypass (D-02) — ClaimsPrincipal.IsInRole reads claims directly, no async needed
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

### AdminController Promote/Demote Fix (target state)

```csharp
// Source: D-09, current AdminController.cs [CITED: 29-CONTEXT.md]
// Before: await userService.AddToRoleAsync(user, "Admin"); — reads AspNetUserRoles (empty)
// After: modify UserGroups.GroupRole for active group

[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> PromoteToAdmin(int userId)
{
    var groupId = activeGroupContext.ActiveGroupId;
    if (groupId == null) return RedirectToAction(nameof(Users));

    // userService.SetGroupRoleAsync — new method needed, or direct UserGroups manipulation
    await userService.SetGroupRoleAsync(userId, groupId.Value, GroupRole.Admin);
    return RedirectToAction(nameof(Users));
}
```

Note: `SetGroupRoleAsync` is not in `IUserService` today. Either add it as a new method or have `AdminController` depend on `IGroupService` for member management. Recommend adding `SetGroupRoleAsync(int userId, int groupId, GroupRole role)` to `IUserService` since `AdminController` already has that service injected.

[ASSUMED] — The exact new method names for promote/demote (whether via IUserService or IGroupService) are Claude's discretion per CONTEXT.md.

---

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| `AspNetUserRoles` for Player/DM/Admin | `UserGroups.GroupRole` per group | Phase 27 migration | Auth handlers, role queries, promote/demote all need updating |
| `context.User.Identity?.IsAuthenticated` check in handlers | Middleware handles auth before handlers; redundant check removed | Phase 29 rewrite | Handler code is simpler |
| `IsInRoleAsync` in handlers | Direct `ClaimsPrincipal.IsInRole("SuperAdmin")` (sync) + async GroupRole lookup | Phase 29 | More efficient — IsInRole reads claims, not DB |

**Deprecated/outdated in Phase 29 context:**
- `userService.IsInRoleAsync(context.User, "Admin")` in handlers — returns false always (AspNetUserRoles empty); replaced by UserGroups query.
- `userService.AddToRoleAsync / RemoveFromRoleAsync` in `AdminController` promote/demote — manipulates AspNetUserRoles (empty); replaced by UserGroups row update.
- `repository.GetAllDungeonMasters` and `repository.GetAllPlayers` — JOIN on AspNetUserRoles (empty); replaced by UserGroups JOIN.

---

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | Injecting `IActiveGroupContext` into `UserRepository` is the preferred approach for GetAllPlayersAsync/GetAllDungeonMastersAsync (vs. parameter on method signature) | Pattern 4 | If parameter approach chosen, IUserRepository and IUserService method signatures change — more interface changes but no hidden dependency |
| A2 | `AdminController.Users` page role badges should be updated to read from UserGroups (treating this as part of the D-07/D-08 fix scope) | Pitfall 8 | If out of scope, Users page remains broken — visible to any Admin user |
| A3 | `SetGroupRoleAsync(int userId, int groupId, GroupRole role)` is added to `IUserService` for use by `AdminController` promote/demote | Pattern — AdminController fix | If IGroupService is used instead, AdminController DI changes; minor but affects plan task structure |
| A4 | The `GroupWithMemberCount` type is a new simple DTO (not an AutoMapper-mapped entity) since it is a projection, not a full entity | Architecture Patterns | If AutoMapper is used instead, EntityProfile mapping must be added |

---

## Open Questions (RESOLVED)

1. **UserRepository IActiveGroupContext injection approach**
   - What we know: `QuestBoardContext` already receives `IActiveGroupContext` as a constructor parameter. `UserRepository` currently does not.
   - What's unclear: Should `UserRepository` receive `IActiveGroupContext` to know which group to query for GetAllPlayers/GetAllDMs, or should the method signatures change to accept `int groupId`?
   - Recommendation: Inject `IActiveGroupContext` into `UserRepository`. It is already Scoped and available. Adding a parameter would require interface signature changes that cascade up through `IUserRepository → IUserService → UserService → all callers`. Injection is lower-impact.

2. **AdminController.Users role badge fix scope**
   - What we know: `GetRolesAsync` reads `AspNetUserRoles` (empty); the Users page shows no role badges.
   - What's unclear: Is fixing this explicitly in scope for Phase 29? It is not listed as a decision in CONTEXT.md.
   - Recommendation: Include it — the Users page is the primary Admin UI surface and is currently broken. The fix follows naturally from D-07/D-08 (same root cause). Add to the wave covering IUserService fixes.

3. **IGroupService vs. GroupService scope**
   - What we know: No IGroupService or GroupService exists today.
   - What's unclear: How much business logic lives in GroupService vs. GroupRepository?
   - Recommendation: GroupService is thin — it validates (name not blank, group has zero members before delete, user not already a member) and delegates to GroupRepository. Most logic is guard clauses and single-entity operations.

---

## Environment Availability

All dependencies are already present. This phase is code + one migration — no new tools or external services required.

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| dotnet ef | Migration generation | Confirmed (used in Phases 26-28) | .NET 10 SDK | — |
| SQL Server | Migration application | Confirmed running (dev host) | Existing dev instance | — |
| AutoMapper | Group/UserGroup entity mapping | Already configured | Existing version | — |

---

## Validation Architecture

### Test Framework

| Property | Value |
|----------|-------|
| Framework | xUnit (QuestBoard.IntegrationTests) |
| Config file | `xunit.runner.json` |
| Quick run command | `dotnet test QuestBoard.IntegrationTests --filter "Category=Fast"` (or specific class) |
| Full suite command | `dotnet test` |

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| AUTH-01 | SuperAdmin role exists in AspNetRoles after migration | migration smoke | manual verify via SQL / seeded in TestDataHelper | ❌ Wave 0 |
| AUTH-02 | AdminHandler approves user with GroupRole.Admin for active group | integration | `dotnet test --filter "FullyQualifiedName~AdminHandlerTests"` | ❌ Wave 0 |
| AUTH-03 | DungeonMasterHandler approves DM and Admin GroupRoles; denies Player | integration | `dotnet test --filter "FullyQualifiedName~DungeonMasterHandlerTests"` | ❌ Wave 0 |
| AUTH-04 | SuperAdmin user bypasses both handlers and accesses AdminOnly pages | integration | `dotnet test --filter "FullyQualifiedName~SuperAdminAuthTests"` | ❌ Wave 0 |
| AUTH-05 | Non-SuperAdmin user receives 403 on /platform/* routes | integration | `dotnet test --filter "FullyQualifiedName~PlatformAreaAuthTests"` | ❌ Wave 0 |
| MGMT-01 | GET /platform/Group/Index returns 200 for SuperAdmin | integration | `dotnet test --filter "FullyQualifiedName~PlatformAreaAuthTests"` | ❌ Wave 0 |
| MGMT-02 | Groups index page lists all groups with correct member counts | integration | `dotnet test --filter "FullyQualifiedName~GroupManagementTests"` | ❌ Wave 0 |
| MGMT-03 | Create group: valid name succeeds; duplicate name shows validation error | integration | `dotnet test --filter "FullyQualifiedName~GroupManagementTests"` | ❌ Wave 0 |
| MGMT-04 | Delete group: empty group succeeds; non-empty group shows error | integration | `dotnet test --filter "FullyQualifiedName~GroupManagementTests"` | ❌ Wave 0 |
| MGMT-05 | Add member: valid user+group+role adds UserGroups row; duplicate shows error | integration | `dotnet test --filter "FullyQualifiedName~GroupManagementTests"` | ❌ Wave 0 |
| MGMT-06 | Remove member: UserGroups row is deleted | integration | `dotnet test --filter "FullyQualifiedName~GroupManagementTests"` | ❌ Wave 0 |

### Sampling Rate

- **Per task commit:** `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~AdminHandler OR FullyQualifiedName~DungeonMasterHandler"` (auth handler tests)
- **Per wave merge:** `dotnet test` (full 197-test suite)
- **Phase gate:** Full suite green before `/gsd-verify-work`

### Wave 0 Gaps

- [ ] `Controllers/AdminHandlerIntegrationTests.cs` — covers AUTH-02, AUTH-03, AUTH-04 — auth handler Group Role behavior
- [ ] `Controllers/PlatformAreaIntegrationTests.cs` — covers AUTH-05, MGMT-01 — area route access control
- [ ] `Controllers/GroupManagementIntegrationTests.cs` — covers MGMT-02 through MGMT-06
- [ ] Update `TestDataHelper.SeedRolesAsync` to include "SuperAdmin" role
- [ ] Update `AuthenticationHelper` to add `CreateAuthenticatedSuperAdminClientAsync` helper

---

## Security Domain

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | no | Identity handles login; no changes |
| V3 Session Management | no | Session already configured; no changes |
| V4 Access Control | yes | `[Authorize(Policy="SuperAdminOnly")]` on Area controller; `[ValidateAntiForgeryToken]` on all POST/DELETE actions |
| V5 Input Validation | yes | Model annotations on Group name (Required, StringLength 100); ModelState validation in controller |
| V6 Cryptography | no | No new cryptographic operations |

### Known Threat Patterns for this stack

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| IDOR — non-SuperAdmin accessing /platform/* | Elevation of Privilege | `[Authorize(Policy="SuperAdminOnly")]` on controller class; no action-level override |
| CSRF on group create/delete/member actions | Tampering | `[ValidateAntiForgeryToken]` on all POST actions; anti-forgery cookie configured in Program.cs |
| Group name injection (XSS via group name display) | Tampering | Razor automatic HTML encoding; no `@Html.Raw` usage |
| Mass assignment on GroupEntity create | Tampering | ViewModel binding (not entity binding); only Name field accepted from form |
| Privilege escalation via promote/demote | Elevation of Privilege | AdminController already protected by `[Authorize(Policy="AdminOnly")]`; promote/demote reads active group from server-side IActiveGroupContext, not from form input |

---

## Sources

### Primary (HIGH confidence)

- [VERIFIED: codebase] `QuestBoard.Service/Authorization/AdminHandler.cs` — current handler signature, IsInRoleAsync call
- [VERIFIED: codebase] `QuestBoard.Service/Authorization/DungeonMasterHandler.cs` — current handler, two IsInRoleAsync calls
- [VERIFIED: codebase] `QuestBoard.Service/Program.cs` — DI registration, AddAuthorizationBuilder, area route absence confirmed
- [VERIFIED: codebase] `QuestBoard.Domain/Interfaces/IUserService.cs` — current interface (no GetGroupRoleAsync, no groupId params)
- [VERIFIED: codebase] `QuestBoard.Domain/Services/UserService.cs` — implementation chain confirmed
- [VERIFIED: codebase] `QuestBoard.Repository/UserRepository.cs` — current AspNetUserRoles JOIN in GetAllDungeonMasters/GetAllPlayers
- [VERIFIED: codebase] `QuestBoard.Repository/Entities/QuestBoardContext.cs` — UserGroups DbSet, HasQueryFilter predicates, GroupEntity unique index
- [VERIFIED: codebase] `QuestBoard.Repository/Entities/UserGroupEntity.cs` — schema, GroupRole as int, FKs
- [VERIFIED: codebase] `QuestBoard.Repository/Entities/GroupEntity.cs` — Name StringLength(100), UserGroups nav property
- [VERIFIED: codebase] `QuestBoard.Repository/Migrations/20260630055221_AddGroupSchema.cs` — migration SQL patterns, role ID mapping
- [VERIFIED: codebase] `QuestBoard.Repository/Migrations/20250704211037_ConvertIsDungeonMasterToRoles.cs` — InsertData pattern for AspNetRoles
- [VERIFIED: codebase] `QuestBoard.Repository/Automapper/EntityProfile.cs` — GroupEntity↔Group and UserGroupEntity↔UserGroup mappings exist
- [VERIFIED: codebase] `QuestBoard.IntegrationTests/WebApplicationFactoryBase.cs` — MutableGroupContext, TestAuthHandler, NoOpBackgroundJobClient
- [VERIFIED: codebase] `QuestBoard.IntegrationTests/Helpers/AuthenticationHelper.cs` — TestAuthHandler role injection via "Test userId:userName:email:roles" header format
- [VERIFIED: codebase] `QuestBoard.Repository/Extensions/ServiceExtensions.cs` — where GroupRepository registration goes
- [VERIFIED: codebase] `QuestBoard.Domain/Extensions/ServiceExtensions.cs` — where IGroupService registration goes
- [VERIFIED: codebase] `QuestBoard.Service/Views/Admin/Users.cshtml` — modern-card pattern reference
- [VERIFIED: codebase] `QuestBoard.Service/Views/_ViewImports.cshtml` — root imports pattern for Area _ViewImports
- [CITED: 29-CONTEXT.md] All decisions D-01 through D-15

### Secondary (MEDIUM confidence)

- [CITED: ASP.NET Core 10 docs pattern] `app.MapControllerRoute` with area defaults — confirmed by existing Program.cs route registration structure
- [CITED: 28-CONTEXT.md] IActiveGroupContext interface design, dual registration pattern (D-09), MutableGroupContext test stub

### Tertiary (LOW confidence)

- None

---

## Metadata

**Confidence breakdown:**
- Standard Stack: HIGH — all libraries already in codebase; no new dependencies
- Architecture: HIGH — patterns confirmed from prior-phase code; handler rewrite is straightforward
- Auth Handler Rewrite: HIGH — current code read, target pattern from CONTEXT.md decisions
- IUserService/UserRepository Fixes: HIGH — exact query changes derivable from current AspNetUserRoles JOIN pattern
- MVC Area Mechanics: HIGH — standard ASP.NET Core; no ambiguity
- GroupRepository/GroupService: HIGH — BaseRepository pattern confirmed; member count query is standard LINQ projection
- Pitfalls: HIGH — all verified against actual migration files, entity config, and existing test patterns

**Research date:** 2026-06-30
**Valid until:** 2026-07-30 (stable .NET 10 stack)

---

## Key Facts for Planner Summary

1. **No IGroupService or IGroupRepository exists yet** — both must be created from scratch. IGroupRepository interface goes in `QuestBoard.Domain/Interfaces/` (not `QuestBoard.Repository/Interfaces/`). Registration goes in `QuestBoard.Repository/Extensions/ServiceExtensions.cs` (repository) and `QuestBoard.Domain/Extensions/ServiceExtensions.cs` (service).

2. **AdminHandler has a pre-existing auth check bug** — `!context.User.Identity?.IsAuthenticated == true` never executes the fail branch. The Phase 29 rewrite drops this entire check (redundant with middleware).

3. **AdminController does NOT currently inject IActiveGroupContext** — the CONTEXT.md says "already depends on IActiveGroupContext from Phase 28" but the actual file shows the constructor is `(IUserService userService, IQuestService questService, IIdentityService identityService, IBackgroundJobClient jobClient, IHttpClientFactory httpClientFactory, IOptions<EmailSettings> emailOptions, IMemoryCache cache)`. IActiveGroupContext is NOT there. The planner must add it.

4. **TestAuthHandler already supports role injection** — the test format `"Test userId:userName:email:role1,role2"` supports multiple roles including "SuperAdmin". `CreateAuthenticatedSuperAdminClientAsync` helper is needed in `AuthenticationHelper` but the infrastructure already handles it.

5. **TestDataHelper.SeedRolesAsync seeds only Admin/DungeonMaster/Player** — "SuperAdmin" must be added to the seeded roles for integration tests that test SuperAdmin authorization.

6. **`GetAllPlayersAsync` and `GetAllDungeonMastersAsync` currently query AspNetUserRoles** — which is empty after Phase 27. The `/players` and DM-listing pages have been returning empty results since Phase 27. Phase 29 fixes this.

7. **The `AdminController.Users` page is also broken** — it calls `GetRolesAsync` which reads AspNetUserRoles (empty). Role badges all show blank. This must be fixed alongside D-07/D-08.

8. **`GroupEntity.Name` has a DB-layer unique index** — `GroupController.Create` and `GroupController.Edit` must handle `DbUpdateException` for duplicate names.
