# Architecture Research

**Domain:** Multi-tenancy integration — existing 3-layer clean architecture (ASP.NET Core 10 MVC)
**Researched:** 2026-06-29
**Confidence:** MEDIUM (EF Core and ASP.NET Core patterns from official Microsoft docs; layer-placement reasoning from architectural analysis of existing codebase)

---

## Standard Architecture

### System Overview After Multi-Tenancy

```
┌──────────────────────────────────────────────────────────────────────┐
│  QuestBoard.Service (Presentation)                                    │
│  ┌─────────────────┐  ┌────────────────┐  ┌──────────────────────┐  │
│  │ Controllers/     │  │ Areas/Groups/  │  │ ActiveGroupContext   │  │
│  │ (existing)       │  │ Controllers/   │  │ Service (NEW)        │  │
│  │                  │  │ GroupsMgmt     │  │ implements           │  │
│  │                  │  │ Controller     │  │ IActiveGroupContext  │  │
│  └──────────────────┘  │ [SuperAdmin]   │  │ → reads session/    │  │
│                        └────────────────┘  │   claim, DB lookup  │  │
│                                            └──────────────────────┘  │
├──────────────────────────── depends on ──────────────────────────────┤
│  QuestBoard.Domain (Business Logic)                                   │
│  ┌──────────────────┐  ┌──────────────────┐  ┌───────────────────┐  │
│  │ Interfaces/       │  │ Services/         │  │ Models/           │  │
│  │ IActiveGroup      │  │ GroupService      │  │ Group             │  │
│  │ Context (NEW)     │  │ (new group CRUD)  │  │ UserGroup         │  │
│  │ IGroupService     │  │                   │  │ (new domain       │  │
│  │ (NEW)             │  │                   │  │  models)          │  │
│  └──────────────────┘  └──────────────────┘  └───────────────────┘  │
├──────────────────────────── depends on ──────────────────────────────┤
│  QuestBoard.Repository (Data)                                         │
│  ┌─────────────────────────────────────────────────────────────┐     │
│  │  QuestBoardContext (MODIFIED)                                │     │
│  │  + GroupEntity, UserGroupEntity DbSets                       │     │
│  │  + IActiveGroupContext injected via constructor              │     │
│  │  + HasQueryFilter on Quest, ShopItem, Character,             │     │
│  │    UserTransaction, TradeItem → e.GroupId == _groupId        │     │
│  └─────────────────────────────────────────────────────────────┘     │
│  ┌───────────────┐  ┌──────────────────┐  ┌──────────────────────┐  │
│  │ GroupRepository│  │ UserGroupRepo    │  │ (existing repos,     │  │
│  │ (NEW)          │  │ (NEW)            │  │  unchanged)          │  │
│  └───────────────┘  └──────────────────┘  └──────────────────────┘  │
└──────────────────────────────────────────────────────────────────────┘
```

### Component Responsibilities

| Component | Responsibility | Layer | New / Modified |
|-----------|---------------|-------|----------------|
| `IActiveGroupContext` | Contract: exposes `int? ActiveGroupId` and `bool IsSuperAdmin` | Domain/Interfaces | NEW |
| `ActiveGroupContextService` | Implementation: resolves active group from session/claim; caches in scope | Service | NEW |
| `GroupEntity` | EF entity: Id, Name, Slug, IsActive, CreatedAt | Repository/Entities | NEW |
| `UserGroupEntity` | Junction table: UserId (int), GroupId (int), IsAdmin (bool) | Repository/Entities | NEW |
| `QuestBoardContext` | Inject `IActiveGroupContext`; add `HasQueryFilter` on 5 entity types; add 2 new DbSets | Repository | MODIFIED |
| `Group` / `UserGroup` | Domain models for the group concept | Domain/Models | NEW |
| `IGroupService` | Group CRUD operations, member management | Domain/Interfaces | NEW |
| `GroupService` | Implementation of IGroupService | Domain/Services | NEW |
| `GroupsManagementController` | SuperAdmin-only group/user management (MVC Area) | Service/Areas | NEW |
| `SuperAdmin` role | Identity role for cross-group administration | Service/Program.cs | NEW |
| `GroupId` FK columns | Added to: QuestEntity, ShopItemEntity, CharacterEntity, UserTransactionEntity, TradeItemEntity | Repository/Entities | MODIFIED |

---

## Question-by-Question Answers

### (a) Where does IActiveGroupContext live — Domain or Service layer?

**Answer: Domain layer.** Specifically `QuestBoard.Domain/Interfaces/IActiveGroupContext.cs`.

**Rationale:** The Repository layer's `QuestBoardContext` must consume this interface to apply Global Query Filters. The Repository layer already depends on the Domain layer (it uses `IBaseRepository<T>` contracts defined there). If `IActiveGroupContext` were defined in the Service layer, Repository would need a compile-time reference to Service — violating the strict one-way dependency rule (Service → Domain → Repository) and creating a circular dependency.

The implementation (`ActiveGroupContextService`) lives in the Service layer and is injected via the DI container. This follows the same pattern the codebase already uses: `IIdentityService` is defined in Domain, implemented in Repository's `IdentityService.cs`. The same inversion applies here.

```
IActiveGroupContext (Domain/Interfaces)
    ↑ implements
ActiveGroupContextService (Service) — reads HttpContext, session, or User claims
    ↓ injected into
QuestBoardContext (Repository) — captures groupId at construction time
```

**Interface shape:**
```csharp
// QuestBoard.Domain/Interfaces/IActiveGroupContext.cs
public interface IActiveGroupContext
{
    int? ActiveGroupId { get; }  // null = SuperAdmin cross-group view
    bool IsSuperAdmin { get; }
}
```

**Registration:** `services.AddScoped<IActiveGroupContext, ActiveGroupContextService>()` in `QuestBoard.Service/Program.cs` — must be registered before `AddDbContext<QuestBoardContext>()` in the DI builder so the container can satisfy the constructor dependency.

---

### (b) How do EF Core Global Query Filters interact with the existing QuestBoardContext?

**Mechanism (from official EF Core documentation):** Inject `IActiveGroupContext` into the `QuestBoardContext` constructor, capture the resolved group ID into a `readonly` field at construction time, then reference that field in `HasQueryFilter` lambdas inside `OnModelCreating`.

```csharp
// QuestBoard.Repository/Entities/QuestBoardContext.cs  (modified)
public class QuestBoardContext(
    DbContextOptions<QuestBoardContext> options,
    IActiveGroupContext groupContext)
    : IdentityDbContext<UserEntity, IdentityRole<int>, int>(options)
{
    private readonly int? _groupId = groupContext.ActiveGroupId;
    private readonly bool _isSuperAdmin = groupContext.IsSuperAdmin;

    // ... existing DbSets unchanged ...

    public DbSet<GroupEntity> Groups { get; set; }         // NEW
    public DbSet<UserGroupEntity> UserGroups { get; set; } // NEW

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ... existing FK configuration unchanged ...

        // NEW: Global query filters — omitted entirely for SuperAdmin
        if (!_isSuperAdmin)
        {
            modelBuilder.Entity<QuestEntity>()
                .HasQueryFilter("GroupFilter", q => q.GroupId == _groupId);

            modelBuilder.Entity<ShopItemEntity>()
                .HasQueryFilter("GroupFilter", s => s.GroupId == _groupId);

            modelBuilder.Entity<CharacterEntity>()
                .HasQueryFilter("GroupFilter", c => c.GroupId == _groupId);

            modelBuilder.Entity<UserTransactionEntity>()
                .HasQueryFilter("GroupFilter", t => t.GroupId == _groupId);

            modelBuilder.Entity<TradeItemEntity>()
                .HasQueryFilter("GroupFilter", t => t.GroupId == _groupId);
        }
    }
}
```

**Key constraints and caveats from official EF Core docs:**

1. EF Core 10 supports named filters via `HasQueryFilter("name", lambda)`. Named filters can be individually disabled: `query.IgnoreQueryFilters(["GroupFilter"])`. This project targets ASP.NET Core 10 / EF Core 9+ — verify named filter availability; fallback is the combined lambda approach with a single `IgnoreQueryFilters()` call.

2. Calling `HasQueryFilter` twice on the same entity without naming silently overwrites the first filter. Since the codebase has no existing query filters, there is no conflict today, but adding soft-delete filters later requires named filters.

3. Required navigations (INNER JOIN) on filtered entities can silently suppress parent rows when `Include()` is used. The existing schema uses `NoAction` delete behavior on most navigation properties. The critical risk is loading `Quest.PlayerSignups.Include(ps => ps.Player)` — Player/User entities are not group-filtered, so this is safe. Any future `Include` on a group-filtered entity from an un-filtered root needs review.

4. `ReminderLogEntity`, `UserEntity`, `CharacterImageEntity`, `DungeonMasterProfileEntity` do NOT get group filters — these are system-wide or user-owned entities.

5. Filters can only be defined on the root entity of an inheritance hierarchy. None of the existing entities use table-per-hierarchy inheritance, so this is not a current concern.

**DbContext lifetime:** `QuestBoardContext` is already registered as Scoped (the `AddDbContext` default). This is correct — a new instance per request picks up a fresh `IActiveGroupContext` with the current user's active group for that request.

**SuperAdmin bypass:** The `if (!_isSuperAdmin)` conditional in `OnModelCreating` means SuperAdmin DbContext instances have no group filters registered at all. This gives full cross-group visibility without needing `IgnoreQueryFilters()` calls scattered throughout repositories — cleaner and less error-prone.

**EF Core model caching warning:** EF Core caches the compiled model per `DbContext` type. Because `_isSuperAdmin` and `_groupId` are instance fields captured by value, the model is rebuilt per `DbContext` instance (or EF re-evaluates the lambda per instance). This is the officially documented pattern (Microsoft EF Core multitenancy docs, `ContactContext` example). At small scale (17-50 users) this causes no performance concern. At scale, use `DbContext` pooling with caution — pooled contexts reuse the model snapshot; test this if pooling is added later.

---

### (c) Safest build order given the namespace rename must happen first

The namespace rename (`EuphoriaInn` → `QuestBoard`) must be Phase 1 because all subsequent code is written in the new namespace. Mixing rename and schema changes in one commit produces an unreadable diff and makes rollback of either change impossible independently.

**Critical finding on migration compatibility (MEDIUM confidence from official EF Core docs):** EF Core tracks applied migrations in `__EFMigrationsHistory` by `MigrationId` (timestamp + name) and `ProductVersion` — NOT by C# namespace or assembly-qualified name. This is a key difference from EF6 (which used a `ContextKey` derived from the namespace). Renaming C# namespaces in migration files and the model snapshot does NOT corrupt the history table. No SQL UPDATE against `__EFMigrationsHistory` is needed. The only required work is updating the `namespace` declarations in all `.cs` and `.Designer.cs` migration files and the `QuestBoardContextModelSnapshot.cs` file.

**Recommended build order:**

```
Phase 1: Namespace Rename (EuphoriaInn → QuestBoard)
  - Rename .csproj files: EuphoriaInn.* → QuestBoard.*
  - Update solution file (.slnx)
  - Find-and-replace all namespace declarations and using directives in .cs files
  - Find-and-replace model namespaces in all .cshtml files
  - Update namespace in all existing Migration .cs and .Designer.cs files
  - Update QuestBoardContextModelSnapshot.cs namespace
  - Update appsettings.json keys, Dockerfile labels, CI references
  - Rebuild + run all 191 tests — zero schema changes, zero behavior changes
  - Commit: "chore: rename EuphoriaInn → QuestBoard (namespaces only)"

Phase 2: Group Entity Schema
  - Add GroupEntity, UserGroupEntity to Repository/Entities/
  - Add IGroupRepository, IUserGroupRepository to Domain/Interfaces/
  - Implement GroupRepository, UserGroupRepository in Repository/
  - Add Group, UserGroup domain models to Domain/Models/
  - Add GroupId FK (int, nullable initially) to the 5 content entity types
  - Register new repositories in Repository/Extensions/ServiceExtensions.cs
  - Migration: AddGroupEntityAndJunctionTable
  - Migration: AddGroupIdToContentEntities
  - Migration: SeedExistingGroupAndMembers
    (INSERT EuphoriaInn group; UPDATE all content rows to GroupId=1;
     INSERT all existing users into UserGroups)
  - Migration: MakeGroupIdRequired (after seed verified)
  - Run all tests; verify FK constraints

Phase 3: IActiveGroupContext + Global Query Filters
  - Add IActiveGroupContext to Domain/Interfaces/
  - Implement ActiveGroupContextService in Service/
  - Modify QuestBoardContext constructor to accept IActiveGroupContext
  - Add HasQueryFilter calls to OnModelCreating for 5 entity types
  - Register ActiveGroupContextService as Scoped in Program.cs (before AddDbContext)
  - Add IGroupService, GroupService to Domain
  - Register GroupService in Domain/Extensions/ServiceExtensions.cs
  - Update integration test WebApplicationFactory: register a stub IActiveGroupContext
    that returns GroupId=1, IsSuperAdmin=false (covers all existing test scenarios)
  - Run all 191 tests; verify filter applies per group; verify SuperAdmin stub bypasses

Phase 4: SuperAdmin Role + Groups MVC Area
  - Seed SuperAdmin IdentityRole in Program.cs startup
  - Add "SuperAdminOnly" authorization policy in Program.cs
  - Create Areas/Groups/ folder structure in QuestBoard.Service/
  - Create GroupsManagementController with [Area("Groups")] [Authorize(Policy="SuperAdminOnly")]
  - Add area route in Program.cs before default route
  - Add _ViewImports.cshtml under Areas/Groups/Views/
  - Run tests; verify authorization rejects non-SuperAdmin; verify routes resolve

Phase 5: Active Group Picker (user-facing group switching)
  - Implement group-picker UI (mechanism TBD by planner: cookie, session, claim)
  - Implement full ActiveGroupContextService group resolution from chosen persistence
  - Add group membership validation (user must belong to the group they select)
  - Run tests; verify correct data scoping per group selection
```

**Why this order:**
- Rename first: every subsequent PR is clean — no mixed namespaces in git diff, no ambiguity about which system a file belongs to.
- Schema before filters: `HasQueryFilter` references `GroupId`, which must exist in entities before the context compiles.
- Filters before UI: the group picker depends on filters working correctly. Landing the picker before filters results in all queries returning unscoped data temporarily.
- SuperAdmin area in Phase 4, not earlier: the role must be seeded, and the seed is part of the schema/startup phase.
- Integration tests updated in Phase 3 (not Phase 2) because the test factory change (stub `IActiveGroupContext`) is only needed when the DbContext constructor changes.

---

### (d) SuperAdmin management area — MVC Area vs regular controller

**Answer: Use an MVC Area named "Groups".** Route prefix: `/groups/`.

**Rationale:** The existing `AdminController` (under `Controllers/Admin/`) handles per-group administration: user listing, quest management, email stats. Adding SuperAdmin functionality to the same folder would blur the distinction between group-scoped admin actions and system-wide actions. An MVC Area provides a separate routing namespace (`/groups/...`), a separate view folder (`Areas/Groups/Views/`), and a distinct C# namespace (`QuestBoard.Service.Areas.Groups.Controllers`). This makes the security boundary explicit at the routing, namespace, and folder level.

**Folder structure:**
```
QuestBoard.Service/
├── Areas/
│   └── Groups/
│       ├── Controllers/
│       │   └── GroupsManagementController.cs
│       │       [Area("Groups")]
│       │       [Authorize(Policy = "SuperAdminOnly")]
│       └── Views/
│           ├── _ViewImports.cshtml   (tag helpers, @using QuestBoard.Service.Areas.Groups)
│           └── GroupsManagement/
│               ├── Index.cshtml      (list all groups)
│               ├── Create.cshtml
│               ├── Edit.cshtml
│               └── Members.cshtml    (add/remove users in a group)
```

**Route registration in Program.cs (before default route):**
```csharp
// Add BEFORE the default route
app.MapControllerRoute(
    name: "GroupsArea",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
```

The `{area:exists}` constraint matches only controllers decorated with `[Area("Groups")]`. No ambiguity with existing controllers.

**Tag helper usage in views:**
```cshtml
<a asp-area="Groups" asp-controller="GroupsManagement" asp-action="Index">
    Manage Groups
</a>
```

**ViewImports caveat (from official docs):** The global `/Views/_ViewImports.cshtml` does NOT apply to area views. A `_ViewImports.cshtml` must be added under `Areas/Groups/Views/` to enable tag helpers and `@using` directives.

**Authorization:** A `"SuperAdminOnly"` policy is added in Program.cs alongside the existing `"DungeonMasterOnly"` and `"AdminOnly"` policies:
```csharp
options.AddPolicy("SuperAdminOnly", policy =>
    policy.RequireRole("SuperAdmin"));
```

---

## New vs Modified Components Summary

### New Components (Repository layer)

| File | Purpose |
|------|---------|
| `Entities/GroupEntity.cs` | Group table: Id, Name, Slug, IsActive, CreatedAt |
| `Entities/UserGroupEntity.cs` | Junction: UserId (int), GroupId (int), IsAdmin (bool) |
| `Interfaces/IGroupRepository.cs` | Group CRUD |
| `Interfaces/IUserGroupRepository.cs` | Member management |
| `GroupRepository.cs` | EF implementation |
| `UserGroupRepository.cs` | EF implementation |
| `Migrations/XXXXX_AddGroupEntityAndJunctionTable.cs` | Groups + UserGroups tables |
| `Migrations/XXXXX_AddGroupIdToContentEntities.cs` | FK columns on 5 entities |
| `Migrations/XXXXX_SeedExistingGroupAndMembers.cs` | Seed EuphoriaInn group + migrate all rows |
| `Migrations/XXXXX_MakeGroupIdRequired.cs` | Make GroupId non-nullable after seed |

### New Components (Domain layer)

| File | Purpose |
|------|---------|
| `Interfaces/IActiveGroupContext.cs` | Contract for resolving current group |
| `Interfaces/IGroupService.cs` | Group management operations |
| `Interfaces/IGroupRepository.cs` | (also listed above — Domain defines the interface) |
| `Models/Group.cs` | Domain model |
| `Models/UserGroup.cs` | Domain model |
| `Services/GroupService.cs` | Group CRUD + member queries |

### New Components (Service layer)

| File | Purpose |
|------|---------|
| `ActiveGroupContextService.cs` | Reads session/claim, resolves GroupId; implements IActiveGroupContext |
| `Areas/Groups/Controllers/GroupsManagementController.cs` | SuperAdmin-only group management |
| `Areas/Groups/Views/GroupsManagement/*.cshtml` | Group management views |
| `Areas/Groups/Views/_ViewImports.cshtml` | Area-scoped view imports |

### Modified Components

| File | Change |
|------|--------|
| `Entities/QuestBoardContext.cs` | Add IActiveGroupContext ctor param, 2 DbSets, query filters in OnModelCreating |
| `Entities/QuestEntity.cs` | Add `int GroupId` FK |
| `Entities/ShopItemEntity.cs` | Add `int GroupId` FK |
| `Entities/CharacterEntity.cs` | Add `int GroupId` FK |
| `Entities/UserTransactionEntity.cs` | Add `int GroupId` FK |
| `Entities/TradeItemEntity.cs` | Add `int GroupId` FK |
| `Repository/Extensions/ServiceExtensions.cs` | Register IGroupRepository, IUserGroupRepository |
| `Service/Program.cs` | Register IActiveGroupContext as Scoped, SuperAdminOnly policy, area route, SuperAdmin role seed |
| `Domain/Extensions/ServiceExtensions.cs` | Register IGroupService |
| `IntegrationTests/...WebApplicationFactory.cs` | Register stub IActiveGroupContext (GroupId=1, IsSuperAdmin=false) |

---

## Data Flow Changes

### Multi-Tenant Read Request (after Phase 3)

```
Browser → GET /Quest/Index
  ↓
ActiveGroupContextService.ActiveGroupId
  (resolves from session key or User claim: active-group-id)
  ↓ captured at DbContext construction
QuestBoardContext._groupId = 1
  ↓
QuestRepository.GetAllAsync()
  ↓ EF Core auto-applies filter:
SELECT * FROM Quests WHERE GroupId = 1
  ↓
QuestService maps Entity → DomainModel
  ↓
QuestController maps DomainModel → ViewModel
  ↓
Razor view rendered
```

### SuperAdmin Cross-Group View (after Phase 4)

```
Browser → GET /groups/GroupsManagement/Index
  ↓
[Authorize(Policy="SuperAdminOnly")] passes
  ↓
ActiveGroupContextService.IsSuperAdmin = true
  ↓ DbContext constructed with IsSuperAdmin=true
QuestBoardContext: OnModelCreating skips all HasQueryFilter calls
  ↓
GroupsManagementController queries Groups table — no tenant filter
  ↓
Returns all groups unscoped
```

### Group Seed (migration)

```
Migration: SeedExistingGroupAndMembers
  ├── INSERT INTO Groups VALUES ('EuphoriaInn', 'euphoriainn', 1, NOW())
  ├── UPDATE Quests SET GroupId = 1
  ├── UPDATE ShopItems SET GroupId = 1
  ├── UPDATE Characters SET GroupId = 1
  ├── UPDATE UserTransactions SET GroupId = 1
  ├── UPDATE TradeItems SET GroupId = 1
  └── INSERT INTO UserGroups (UserId, GroupId, IsAdmin)
      SELECT Id, 1, CASE WHEN Id IN (SELECT UserId FROM AspNetUserRoles WHERE RoleId = AdminRoleId) THEN 1 ELSE 0 END
      FROM AspNetUsers
```

---

## Scaling Considerations

This application serves a small trusted group (17 members initially). The following is informational only:

| Scale | Notes |
|-------|-------|
| 1-5 groups | Single-DB with query filters is fully appropriate |
| 5-50 groups | Add composite indexes: `(GroupId, <existing PK columns>)` on the 5 filtered tables |
| 50+ groups | Global query filters on every query add overhead; consider row-level security at the DB level or schema-per-tenant |

**Recommended index additions (Phase 2 migration):**
```csharp
modelBuilder.Entity<QuestEntity>().HasIndex(q => q.GroupId);
modelBuilder.Entity<ShopItemEntity>().HasIndex(s => s.GroupId);
modelBuilder.Entity<CharacterEntity>().HasIndex(c => c.GroupId);
modelBuilder.Entity<UserTransactionEntity>().HasIndex(t => t.GroupId);
modelBuilder.Entity<TradeItemEntity>().HasIndex(t => t.GroupId);
```

---

## Anti-Patterns

### Anti-Pattern 1: Putting IActiveGroupContext in the Service layer

**What people do:** Define the interface in Service since that is where the implementation lives.

**Why it is wrong:** `QuestBoardContext` (in Repository) must consume this interface. If it is in Service, Repository gets a compile-time dependency on Service — violating the one-way rule and creating a circular dependency that the compiler will reject.

**Do this instead:** Define the interface in Domain. Repository already depends on Domain; Service already depends on Domain. Both can see it without circular references.

### Anti-Pattern 2: Lazy evaluation of tenant ID in the HasQueryFilter lambda

**What people do:** `HasQueryFilter(q => q.GroupId == _groupContextService.ActiveGroupId)` — capturing the service reference and calling the property in the lambda.

**Why it is wrong:** EF Core captures the lambda's closure at model build time. If you capture a service reference rather than a resolved value, EF may use a stale or wrong scope depending on how model caching interacts with the lifetime. The official Microsoft pattern captures the value at construction time.

**Do this instead:** Capture the value into a `readonly` field at DbContext construction (`_groupId = groupContext.ActiveGroupId`) and reference `_groupId` in the lambda. EF Core creates a new DbContext instance per scope so the field is fresh per request.

### Anti-Pattern 3: Applying group filter to UserEntity / Identity tables

**What people do:** Add `GroupId` to `UserEntity` and filter it.

**Why it is wrong:** Users are cross-group (many-to-many via `UserGroupEntity`). Filtering `UserEntity` by `GroupId` breaks Identity's user lookup, login, and role resolution. SuperAdmin cannot manage all users.

**Do this instead:** Scope access to users through `UserGroupEntity`. Controllers needing group-member lists query `UserGroups.Where(ug => ug.GroupId == activeGroupId).Select(ug => ug.User)`.

### Anti-Pattern 4: Registering the area route after the default route

**What people do:** Add the area route at the end of `Program.cs` route configuration.

**Why it is wrong:** ASP.NET Core conventional routing is order-dependent. The default route `{controller=Home}/{action=Index}/{id?}` matches `/groups/GroupsManagement/Index` before the area route gets a chance. Area controllers will return 404.

**Do this instead:** Register the area route before the default route in `Program.cs`. The `{area:exists}` constraint ensures only area-decorated controllers match.

### Anti-Pattern 5: Combining namespace rename with schema changes in one commit

**What people do:** Save time by doing the rename and adding GroupId FK in the same PR.

**Why it is wrong:** A rename commit touches hundreds of files. A schema commit touches entities, migrations, and tests. Mixed together the git diff is unreadable, CI failures are hard to attribute, and partial rollback of either change is impractical.

**Do this instead:** The rename is a pure no-behavior-change commit with all 191 tests still green. Schema additions are separate, reviewable commits with separate EF Core migrations.

### Anti-Pattern 6: Making GroupId nullable permanently

**What people do:** Leave `GroupId` as `int?` (nullable) across all entities for flexibility.

**Why it is wrong:** A null `GroupId` creates an implicit "ungrouped" or "global" data state that is not a real group. Every query that filters by group must add `== null` handling. The SuperAdmin bypass via `IsSuperAdmin` flag handles cross-group access cleanly without nullable FKs.

**Do this instead:** Seed all existing rows to `GroupId = 1` (EuphoriaInn group) in a migration, then make `GroupId` non-nullable in the next migration. Enforce `NOT NULL` at the database level.

---

## Integration Points

### Internal Boundaries

| Boundary | Communication | Change Required |
|----------|---------------|-----------------|
| Service → Domain | `IGroupService`, `IActiveGroupContext` interfaces | Add new interfaces |
| Domain → Repository | `IGroupRepository`, `IUserGroupRepository` interfaces | Add new repository interfaces |
| Repository DbContext → Domain | `IActiveGroupContext` injected by DI container | Add constructor parameter to QuestBoardContext |
| Service DI setup | `Program.cs` `AddScoped` registrations | Register `ActiveGroupContextService` before `AddDbContext` |
| Integration tests | `TestWebApplicationFactory` | Register stub `IActiveGroupContext` returning fixed GroupId=1, IsSuperAdmin=false |

---

## Sources

- [EF Core Global Query Filters — Microsoft Learn](https://learn.microsoft.com/en-us/ef/core/querying/filters) (MEDIUM confidence, official docs, updated 2026-06-24)
- [EF Core Multi-tenancy — Microsoft Learn](https://learn.microsoft.com/en-us/ef/core/miscellaneous/multitenancy) (MEDIUM confidence, official docs, updated 2026-06-24)
- [Managing EF Core Migrations — Microsoft Learn](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/managing) (MEDIUM confidence, official docs)
- [ASP.NET Core MVC Areas — Microsoft Learn](https://learn.microsoft.com/en-us/aspnet/core/mvc/controllers/areas?view=aspnetcore-10.0) (MEDIUM confidence, official docs)
- [Multi-Tenant Applications with EF Core — Milan Jovanovic](https://www.milanjovanovic.tech/blog/multi-tenant-applications-with-ef-core) (LOW confidence, community article)
- [Implementing Multi-Tenancy with EF Global Query Filters — Medium](https://medium.com/@assiljanbeih/implementing-secure-multi-tenancy-with-eflobal-query-filters-net-9502ac290fb2) (LOW confidence, community article)

---
*Architecture research for: Multi-tenancy integration in 3-layer ASP.NET Core MVC app*
*Researched: 2026-06-29*
