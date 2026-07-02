# Stack Research

**Domain:** ASP.NET Core 10 MVC — v5.0 Multi-Tenancy additions to existing app
**Researched:** 2026-06-29
**Confidence:** MEDIUM (EF Core / ASP.NET Core from official Microsoft docs; rename tooling LOW from community sources)

---

## Summary: No New NuGet Packages Required

Every multi-tenancy feature in v5.0 is achievable with packages already present in the project. The only "addition" is one `Program.cs` line (`AddHttpContextAccessor()`) and an optional dev-time CLI tool for the rename. No `<PackageReference>` changes to any `.csproj`.

---

## Current Package Baseline (v4.0 — verified from csproj files)

| Package | Version | Project |
|---------|---------|---------|
| Microsoft.EntityFrameworkCore | 10.0.9 | Repository |
| Microsoft.EntityFrameworkCore.SqlServer | 10.0.9 | Repository |
| Microsoft.EntityFrameworkCore.Design | 10.0.9 | Repository |
| Microsoft.AspNetCore.Identity.EntityFrameworkCore | 10.0.9 | Repository |
| Microsoft.AspNetCore.Identity.UI | 10.0.9 | Service |
| Microsoft.AspNetCore.Identity | 2.3.11 | Domain |
| Microsoft.AspNetCore.App (FrameworkReference) | 10.0 | Domain |
| AutoMapper | 16.1.1 | Domain |
| Hangfire.AspNetCore + Hangfire.SqlServer | 1.8.23 | Service |

---

## Feature-by-Feature Stack Analysis

### 1. Namespace/Project Rename (EuphoriaInn → QuestBoard)

**What changes:** 5 folder names, 5 `.csproj` filenames, the `.slnx` project paths, all `namespace` declarations, all `using` statements, AutoMapper profile references, `appsettings` keys referencing the old name, Dockerfile/deploy script paths.

**Scale:** ~200 files touched in v4.0; namespace replace will touch nearly all of them.

**Tooling options:**

| Approach | Mechanism | Effort | Risk |
|----------|-----------|--------|------|
| `ModernRonin.ProjectRenamer` (recommended) | `dotnet tool install -g ModernRonin.ProjectRenamer` then `renamer --old EuphoriaInn --new QuestBoard` | Lowest | Must verify `.slnx` format support (tool was built for `.sln`; manual `.slnx` fix may be needed) |
| VS 2022 built-in | Right-click → Rename project, then right-click → Sync Namespaces | Low for namespaces; manual folder + `.slnx` path fix still needed | Sync Namespaces does not update cross-project `using` statements |
| Manual find-and-replace | IDE global search-replace `EuphoriaInn` → `QuestBoard` | High | Easy to miss occurrences in `.cshtml`, `.csproj`, `.slnx`, and config files |

**Recommendation:** Use `ModernRonin.ProjectRenamer` for the mechanical rename. Follow up with a manual grep for any remaining `EuphoriaInn` occurrences — particularly in `.cshtml` files, `appsettings.json` keys, and the `.slnx` file.

**csproj changes:** The .NET SDK infers `RootNamespace` from the `.csproj` filename; renaming the files is sufficient. Only add an explicit `<RootNamespace>` if the tool does not rename the `.csproj` file itself.

**EF Core migrations:** The migration history table (`__EFMigrationsHistory`) stores string migration names, not assembly-qualified types. The existing migrations remain valid after rename. The `dotnet ef` command paths (`--project`, `--startup-project`) must be updated to the new names.

**Integration tests:** `WebApplicationFactory<Program>` resolves `Program` from the renamed assembly automatically — no code change needed there, but the `ProjectReference` path in the test `.csproj` files must be updated.

**No new NuGet packages.**

---

### 2. EF Core Global Query Filters (Group Isolation)

**EF Core version in use:** 10.0.9 — supports named filters (new in EF Core 10).

**Pattern:** Inject a scoped `IGroupContextService` into `QuestBoardContext` via constructor, capture the active group ID as a private field, then apply `HasQueryFilter` in `OnModelCreating` for every group-scoped entity.

```csharp
// QuestBoardContext constructor
private readonly int? _activeGroupId;

public QuestBoardContext(
    DbContextOptions<QuestBoardContext> options,
    IGroupContextService groupContext)
    : base(options)
{
    _activeGroupId = groupContext.ActiveGroupId; // null = SuperAdmin / job scope
}

// OnModelCreating — EF Core 10 named filter
modelBuilder.Entity<QuestEntity>()
    .HasQueryFilter("GroupFilter", q => _activeGroupId == null || q.GroupId == _activeGroupId);
```

**Why named filters matter here:** EF Core 10's named `HasQueryFilter` enables `IgnoreQueryFilters(["GroupFilter"])` to selectively bypass only the group filter without also bypassing any soft-delete or other filters added later. This is critical for SuperAdmin queries that must see all groups.

**Navigation chain caveat (critical):** Any required navigation from a group-scoped entity to a non-group-scoped entity (e.g. `QuestEntity → UserEntity`) uses an INNER JOIN and will cause parent rows to be silently dropped. Review all `.IsRequired()` configurations — set to `.IsRequired(false)` where the join target is not group-scoped, or add a matching group filter to both sides of the relationship.

**Hangfire jobs:** Jobs run without an HTTP context. The `IGroupContextService` must handle `null` `HttpContext` gracefully by returning `null`, which makes `_activeGroupId == null` true and bypasses the group filter — giving jobs full visibility across all groups. This is the correct behavior for system-level jobs like the daily reminder sweep.

**No new NuGet packages.** Uses `Microsoft.EntityFrameworkCore 10.0.9` already present.

---

### 3. Many-to-Many User↔Group Membership

**Pattern:** Add navigation collections to both entities; EF Core infers the shadow join table automatically. Use `UsingEntity` to name the table explicitly.

```csharp
// UserEntity — add:
public ICollection<GroupEntity> Groups { get; set; } = [];

// GroupEntity — new entity:
public ICollection<UserEntity> Members { get; set; } = [];

// OnModelCreating
modelBuilder.Entity<UserEntity>()
    .HasMany(u => u.Groups)
    .WithMany(g => g.Members)
    .UsingEntity("UserGroupMemberships");
```

**No explicit join entity needed** for basic membership. If a "group-admin" role per user per group is added later, an explicit `UserGroupMembershipEntity` with a payload column would be introduced — out of scope for v5.0.

**No new NuGet packages.**

---

### 4. Active-Group Context Service (IGroupContextService)

**Mechanism:** A scoped service reads the active group ID from ASP.NET Core Session and exposes it for injection into `QuestBoardContext` and repositories.

```csharp
// Program.cs — two new lines
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IGroupContextService, SessionGroupContextService>();
```

`IHttpContextAccessor` is part of `Microsoft.AspNetCore.App` (the shared framework FrameworkReference already in `EuphoriaInn.Domain.csproj`). Session middleware is already registered (`AddSession` / `UseSession` already in `Program.cs`).

**Persistence mechanism:** Session (`IHttpContextAccessor.HttpContext.Session.GetInt32("ActiveGroupId")`). Session is server-side, tamper-proof, and already configured with a 24-hour idle timeout — ideal for "remember my selected group." The planner may also consider a per-user DB preference column as a fallback for cross-device persistence, but that is a design decision, not a stack decision.

**Null safety:** When `HttpContext` is null (Hangfire job context), `ActiveGroupId` returns `null` and the group filter is skipped. This is correct and intentional — jobs must see all groups.

**No new NuGet packages.** `AddHttpContextAccessor()` is a built-in framework method.

---

### 5. SuperAdmin Role + Authorization Policy

**Pattern:** Identical to the existing `AdminHandler` / `DungeonMasterHandler` pattern already in the codebase.

```csharp
// New files in EuphoriaInn.Service/Authorization/
public class SuperAdminRequirement : IAuthorizationRequirement { }

public class SuperAdminHandler : AuthorizationHandler<SuperAdminRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, SuperAdminRequirement requirement)
    {
        if (context.User.IsInRole("SuperAdmin"))
            context.Succeed(requirement);
        return Task.CompletedTask;
    }
}

// In Program.cs — add to existing AddAuthorizationBuilder chain
builder.Services.AddScoped<IAuthorizationHandler, SuperAdminHandler>();
// In .AddAuthorizationBuilder():
.AddPolicy("SuperAdminOnly", p => p.Requirements.Add(new SuperAdminRequirement()))
```

**Role seeding:** Add "SuperAdmin" via `RoleManager<IdentityRole<int>>` in the startup seed alongside the existing Admin/DungeonMaster/Player roles.

**No new NuGet packages.**

---

### 6. Admin-Only User Creation (Remove Self-Registration)

**Approach:** Remove or gate the public Register endpoint. Two options:

- **Preferred:** Add `[Authorize(Policy = "AdminOnly")]` to the existing Register action, which turns registration into an admin-guarded flow.
- **Alternative:** Remove the Register action and view entirely; admins create accounts via a new dedicated admin user-management page.

`Microsoft.AspNetCore.Identity.UI` is already present; no scaffold changes needed — the existing Account controller handles this.

**No new NuGet packages.**

---

### 7. SuperAdmin Area Routing (/groups or /superadmin)

**Pattern:** ASP.NET Core Areas — built-in MVC feature, zero packages.

**Folder structure** (inside `EuphoriaInn.Service/`):
```
Areas/
  SuperAdmin/
    Controllers/
      GroupsController.cs     ← [Area("SuperAdmin")] [Authorize(Policy="SuperAdminOnly")]
    Views/
      Groups/
        Index.cshtml
      Shared/
        _ViewImports.cshtml   ← copy tag helper imports here
```

**Program.cs routing** — must be registered BEFORE the default route:
```csharp
app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Groups}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
```

**_ViewImports caveat:** The existing `/Views/_ViewImports.cshtml` is NOT auto-applied to Area views. Add a `_ViewImports.cshtml` in `Areas/SuperAdmin/Views/` with the same `@using` and tag helper directives, or move the root-level `_ViewImports.cshtml` to the application root folder (one level above `Views/`).

**MobileViewLocationExpander caveat:** The existing `MobileViewLocationExpander` only populates `ViewLocationFormats`. If mobile views are ever needed in the SuperAdmin area, `AreaViewLocationFormats` must be extended separately. Not a concern for v5.0 since the SuperAdmin area is desktop-only management UI.

**No new NuGet packages.**

---

## What NOT to Add

| Avoid | Why | Use Instead |
|-------|-----|-------------|
| `Finbuckle.MultiTenant` | Designed for connection-string-per-tenant or database-per-tenant scenarios; adds significant abstraction overhead; overkill for single-database group isolation | EF Core `HasQueryFilter` + scoped `IGroupContextService` |
| `Abp.io` / Orchard Core | Full application platforms; cannot be bolted onto an existing app | Native EF Core + ASP.NET Core patterns |
| `SaasKit` | Last release 2018; no .NET 10 support | Custom scoped service backed by `IHttpContextAccessor` |
| `ModernRonin.ProjectRenamer` as `<PackageReference>` | Dev-time rename tool only; not a runtime dependency | Install as a global dotnet tool: `dotnet tool install -g ModernRonin.ProjectRenamer` |
| A second schema or database per group | This app has 17 members in one group becoming N groups; shared-schema single-database is the correct tier | EF Core global query filters on `GroupId` column |

---

## Installation Summary

**No `<PackageReference>` changes to any `.csproj`.**

**One optional dev-time CLI tool (global install, not added to project):**
```bash
dotnet tool install -g ModernRonin.ProjectRenamer
```

**One addition to `Program.cs`:**
```csharp
builder.Services.AddHttpContextAccessor(); // required for IGroupContextService
```

---

## Compatibility Notes

| Concern | Detail |
|---------|--------|
| Named `HasQueryFilter` | Requires EF Core 10.0+. Project is on 10.0.9 — confirmed available. |
| `IHttpContextAccessor` | Stable since .NET Core 1.x; `AddHttpContextAccessor()` is the standard registration. |
| `AddAuthorizationBuilder()` | Available since .NET 7; already used in the project. |
| ASP.NET Core Areas | Available since ASP.NET Core 1.0; stable across all versions. |
| `ModernRonin.ProjectRenamer` | Built for `.sln` format; `.slnx` support is unconfirmed — verify before use or fall back to manual `.slnx` edit. |
| EF Core migrations after rename | Migration history is unaffected; `dotnet ef` command `--project` paths must be updated. |
| Hangfire jobs + group context | `IGroupContextService` must return `null` when `HttpContext` is absent; group filter then evaluates `_activeGroupId == null` as true and passes all rows through. |

---

## Sources

- [EF Core Global Query Filters — Microsoft Learn](https://learn.microsoft.com/en-us/ef/core/querying/filters) — MEDIUM confidence (official docs, updated 2026-06-24)
- [EF Core Multi-tenancy — Microsoft Learn](https://learn.microsoft.com/en-us/ef/core/miscellaneous/multitenancy) — MEDIUM confidence (official docs, updated 2026-06-24)
- [EF Core Many-to-Many Relationships — Microsoft Learn](https://learn.microsoft.com/en-us/ef/core/modeling/relationships/many-to-many) — MEDIUM confidence (official docs)
- [ASP.NET Core Areas — Microsoft Learn](https://learn.microsoft.com/en-us/aspnet/core/mvc/controllers/areas) — MEDIUM confidence (official docs, updated 2025-08-28)
- [Access HttpContext in ASP.NET Core — Microsoft Learn](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/http-context) — MEDIUM confidence (official docs)
- [Renaming Projects in .NET — Tudor Wolff / Medium](https://medium.com/@tudor.wolff/renaming-projects-in-net-ccfb43979f46) — LOW confidence (community blog)
- [ModernRonin.ProjectRenamer GitHub](https://github.com/ModernRonin/ProjectRenamer) — LOW confidence (community tool; `.slnx` compatibility unverified)

---

*Stack research for: D&D Quest Board v5.0 Multi-Tenancy*
*Researched: 2026-06-29*
