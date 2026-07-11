# Phase 72: Platform Settings + Token Contract - Pattern Map

**Mapped:** 2026-07-08
**Files analyzed:** 20 (18 code files + 1 written contract doc + Group nav-button edits counted as 2)
**Analogs found:** 17 / 18 code files (TOKEN-CONTRACT.md is a design doc with no code analog)

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|---|---|---|---|---|
| `QuestBoard.Repository/Entities/IntegrationSettingEntity.cs` | model (EF entity) | CRUD (singleton row) | `QuestBoard.Repository/Entities/DungeonMasterProfileEntity.cs` (singleton shape) + `GroupEntity.cs` (flat DataAnnotations shape) | role-match (composite) |
| `QuestBoard.Repository/IntegrationSettingRepository.cs` | service (repository) | CRUD | `QuestBoard.Repository/GroupRepository.cs` | role-match |
| `QuestBoard.Repository/Migrations/<timestamp>_AddIntegrationSettings.cs` | migration | batch (schema DDL) | `QuestBoard.Repository/Migrations/20260626190255_AddReminderLog.cs` | exact (standard EF-generated `CreateTable`) |
| `QuestBoard.Repository/Entities/QuestBoardContext.cs` (MODIFIED) | config (DbContext) | CRUD | same file, `DungeonMasterProfileEntity` `ValueGeneratedNever()` block + `GroupEntity`/`UserEntity` no-query-filter precedent | exact |
| `QuestBoard.Repository/Extensions/ServiceExtensions.cs` (MODIFIED) | config (DI registration) | — | same file, existing repository registrations | exact |
| `QuestBoard.Repository/Automapper/EntityProfile.cs` (MODIFIED) | transform (AutoMapper profile) | transform | same file, `GroupEntity ↔ Group` map | exact |
| `QuestBoard.Domain/Interfaces/IIntegrationSettingRepository.cs` | service (interface) | CRUD | `QuestBoard.Domain/Interfaces/IGroupRepository.cs` | role-match |
| `QuestBoard.Domain/Interfaces/IIntegrationSettingService.cs` | service (interface) | CRUD | `QuestBoard.Domain/Interfaces/IGroupService.cs` (deviating from `IBaseService<T>` — see Pattern 1) | role-match, deliberate deviation |
| `QuestBoard.Domain/Models/IntegrationSetting.cs` | model (domain) | CRUD | `QuestBoard.Domain/Models/Group.cs` | exact |
| `QuestBoard.Domain/Services/IntegrationSettingService.cs` | service | CRUD | `QuestBoard.Domain/Services/GroupService.cs` | role-match |
| `QuestBoard.Domain/Extensions/ServiceExtensions.cs` (MODIFIED) | config (DI registration) | — | same file, existing service registrations | exact |
| `QuestBoard.Service/Areas/Platform/Controllers/IntegrationsController.cs` | controller | request-response | `QuestBoard.Service/Areas/Platform/Controllers/UsersController.cs` (scope) + `GroupController.cs` (POST/blank-preserve/TempData shape) | exact (composite) |
| `QuestBoard.Service/ViewModels/PlatformViewModels/IntegrationSettingsViewModel.cs` | model (ViewModel) | request-response | `QuestBoard.Service/ViewModels/PlatformViewModels/GroupEditViewModel.cs` + `EditShopItemViewModel.cs` (`[Url]` field) | role-match |
| `QuestBoard.Service/Areas/Platform/Views/Integrations/Index.cshtml` | component (Razor view, desktop) | request-response | `QuestBoard.Service/Areas/Platform/Views/Group/Index.cshtml` + `Views/Admin/EditUser.cshtml` (checkbox) | role-match (composite) |
| `QuestBoard.Service/Areas/Platform/Views/Integrations/Index.Mobile.cshtml` | component (Razor view, mobile) | request-response | `QuestBoard.Service/Areas/Platform/Views/Group/Index.Mobile.cshtml` | exact |
| `QuestBoard.Service/Areas/Platform/Views/Group/Index.cshtml` (MODIFIED) | component (nav wiring) | request-response | same file, existing "Manage Users" header-button addition | exact |
| `QuestBoard.Service/Areas/Platform/Views/Group/Index.Mobile.cshtml` (MODIFIED) | component (nav wiring) | request-response | same file, existing "Manage Users" header-button addition | exact |
| `QuestBoard.UnitTests/Services/IntegrationSettingServiceTests.cs` | test | CRUD | `QuestBoard.UnitTests/Services/GroupServiceTests.cs` | role-match |
| `QuestBoard.IntegrationTests/Controllers/IntegrationsAreaIntegrationTests.cs` | test | request-response | `QuestBoard.IntegrationTests/Controllers/PlatformAreaIntegrationTests.cs` | exact |
| `.planning/TOKEN-CONTRACT.md` | config (design doc, no runtime code) | — | none — see No Analog Found | n/a |

## Pattern Assignments

### `QuestBoard.Repository/Entities/IntegrationSettingEntity.cs` (model, CRUD/singleton)

**Analogs:** `DungeonMasterProfileEntity.cs` (singleton-Id shape) + `GroupEntity.cs` (flat DataAnnotations shape)

**Singleton entity shape** (`QuestBoard.Repository/Entities/DungeonMasterProfileEntity.cs:1-17`):
```csharp
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuestBoard.Repository.Entities;

[Table("DungeonMasterProfiles")]
public class DungeonMasterProfileEntity : IEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]  // Id = UserId, NOT auto-generated
    public int Id { get; set; }

    [StringLength(2000)]
    public string? Bio { get; set; }

    public virtual DungeonMasterProfileImageEntity? ProfileImage { get; set; }
}
```
Note: `DungeonMasterProfileEntity` uses `[DatabaseGenerated(DatabaseGeneratedOption.None)]` on the attribute *and* `.ValueGeneratedNever()` in `OnModelCreating` (belt-and-suspenders) because its `Id` is a foreign key (`= UserId`). For `IntegrationSettingEntity`, RESEARCH.md's Pattern 2 recommends the same `ValueGeneratedNever()` approach in `OnModelCreating` with `Id = 1` as the default value, since there is no foreign key to derive `Id` from.

**Flat DataAnnotations column shape** (`QuestBoard.Repository/Entities/GroupEntity.cs:1-22`):
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

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int BoardType { get; set; }

    public virtual ICollection<UserGroupEntity> UserGroups { get; set; } = [];
}
```

**Target shape** (per RESEARCH.md Pattern 2 and REQUIREMENTS.md SETT-06 column names — use these exact names, not the older `OmphalosBaseUrl` naming from earlier milestone research, per Pitfall 4):
```csharp
[Table("IntegrationSettings")]
public class IntegrationSettingEntity : IEntity
{
    [Key]
    public int Id { get; set; } = 1;   // ValueGeneratedNever() in OnModelCreating — singleton row

    [Required, StringLength(500)]
    public string OmphalosUrl { get; set; } = string.Empty;

    [StringLength(200)]
    public string OmphalosSharedSecret { get; set; } = string.Empty;

    public bool IsEnabled { get; set; }
}
```

---

### `QuestBoard.Repository/Entities/QuestBoardContext.cs` (MODIFIED)

**Analog:** same file — `DungeonMasterProfileEntity`'s `ValueGeneratedNever()` registration and the `GroupEntity`/`UserEntity` no-query-filter precedent

**DbSet declarations** (`QuestBoard.Repository/Entities/QuestBoardContext.cs:13-43`, add alongside these):
```csharp
public DbSet<GroupEntity> Groups { get; set; }

public DbSet<UserGroupEntity> UserGroups { get; set; }
```

**`ValueGeneratedNever()` pattern** (`QuestBoardContext.cs:159-162`):
```csharp
// DungeonMasterProfile — Id = UserId (no auto-generation)
modelBuilder.Entity<DungeonMasterProfileEntity>()
    .Property(p => p.Id)
    .ValueGeneratedNever();
```
Apply the same call for `IntegrationSettingEntity`, with a seeded/default `Id = 1` on the entity class itself (no FK relationship needed, unlike `DungeonMasterProfileEntity`).

**No query filter — critical, do not add one** (`QuestBoardContext.cs:365-367`, and every `HasQueryFilter` call at lines 280-373 for contrast):
```csharp
// UserEntity intentionally excluded — HasQueryFilter on UserEntity breaks ASP.NET Core Identity
// (login, password reset, and email confirmation all fail silently)
```
`GroupEntity` and `UserEntity` are the only entities with **no** `HasQueryFilter(...)` call registered — every other entity (`QuestEntity`, `ShopItemEntity`, `CharacterEntity`, etc.) has one keyed on `activeGroupContext.ActiveGroupId`. `IntegrationSettingEntity` must join the unfiltered set (`Groups`/`UserEntity`'s set) — it is instance-wide, not tenant-scoped. Do not add a `HasQueryFilter` call for it.

---

### `QuestBoard.Repository/IntegrationSettingRepository.cs` (repository, CRUD)

**Analog:** `QuestBoard.Repository/GroupRepository.cs:1-12` (class declaration shape; the bespoke query methods below are not needed — `IntegrationSetting` only needs `Get`/`Save`, inherited from `BaseRepository`)

```csharp
using AutoMapper;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.Repository.Entities;

namespace QuestBoard.Repository;

internal class GroupRepository(QuestBoardContext dbContext, IMapper mapper)
    : BaseRepository<Group, GroupEntity>(dbContext, mapper), IGroupRepository
{
    // ... bespoke methods beyond the base CRUD ...
}
```
`IntegrationSettingRepository` should follow the same `internal class ... (QuestBoardContext dbContext, IMapper mapper) : BaseRepository<IntegrationSetting, IntegrationSettingEntity>(dbContext, mapper), IIntegrationSettingRepository` shape, but needs no bespoke query methods beyond what `BaseRepository` already provides (`GetByIdAsync(1)`, `AddAsync`, `UpdateAsync` are sufficient — see `BaseRepository.cs` excerpt below).

**Base CRUD mechanics it inherits** (`QuestBoard.Repository/BaseRepository.cs:1-70`, full file — reuse verbatim, do not reimplement):
```csharp
internal abstract class BaseRepository<TModel, TEntity>(QuestBoardContext dbContext, IMapper mapper)
    : IBaseRepository<TModel>
    where TModel : class, IModel
    where TEntity : class, IEntity
{
    protected QuestBoardContext DbContext { get; } = dbContext;
    protected DbSet<TEntity> DbSet { get; } = dbContext.Set<TEntity>();
    protected IMapper Mapper { get; } = mapper;

    public virtual async Task AddAsync(TModel model, CancellationToken token = default)
    {
        var entity = Mapper.Map<TEntity>(model);
        await DbSet.AddAsync(entity, token);
        await DbContext.SaveChangesAsync(token);
        model.Id = entity.Id;
    }

    public virtual async Task<TModel?> GetByIdAsync(int id, CancellationToken token = default)
    {
        var entity = await DbSet.FindAsync([id], cancellationToken: token);
        return entity == null ? null : Mapper.Map<TModel>(entity);
    }

    public virtual async Task UpdateAsync(TModel model, CancellationToken token = default)
    {
        var entity = await DbSet.FindAsync([model.Id]);
        if (entity == null) return;
        Mapper.Map(model, entity);
        await DbContext.SaveChangesAsync(token);
    }
    // ... ExistsAsync, GetAllAsync, RemoveAsync, SaveChangesAsync ...
}
```
Note: for a singleton row, `AddAsync`'s `model.Id = entity.Id` auto-propagation won't apply the same way since `Id` is hardcoded to `1`, not DB-generated — the service layer's `GetAsync()` should call `GetByIdAsync(1)` and, if null, `AddAsync` a default row with `Id = 1` already set (first-run bootstrap), per RESEARCH.md's "Don't Hand-Roll" table (`FindAsync(1)` returning null on first run).

---

### `QuestBoard.Domain/Interfaces/IIntegrationSettingRepository.cs` (interface)

**Analog:** `QuestBoard.Domain/Interfaces/IGroupRepository.cs:1-39`
```csharp
using QuestBoard.Domain.Models;

namespace QuestBoard.Domain.Interfaces;

public interface IGroupRepository : IBaseRepository<Group>
{
    // ... bespoke methods ...
}
```
`IIntegrationSettingRepository : IBaseRepository<IntegrationSetting>` needs no additional bespoke methods — `IBaseRepository<T>`'s `GetByIdAsync`/`AddAsync`/`UpdateAsync` (`QuestBoard.Domain/Interfaces/IBaseRepository.cs:1-39`, full file) already cover everything the singleton-row service needs.

---

### `QuestBoard.Domain/Interfaces/IIntegrationSettingService.cs` (interface, deliberate deviation from `IBaseService<T>`)

**Analog:** `QuestBoard.Domain/Interfaces/IGroupService.cs:1-39` (shape reference), but **do not** extend `IBaseService<T>` — see RESEARCH.md Pattern 1 / Anti-Patterns for why (`GetAllAsync()`/`ExistsAsync(int id)` are meaningless for a table with exactly one row).

**`IBaseService<T>` being deviated from** (`QuestBoard.Domain/Interfaces/IBaseService.cs`, full file, 33 lines):
```csharp
public interface IBaseService<T>
{
    Task AddAsync(T model, CancellationToken token = default);
    Task<bool> ExistsAsync(int id, CancellationToken token = default);
    Task<IList<T>> GetAllAsync(CancellationToken token = default);
    Task<T?> GetByIdAsync(int id, CancellationToken token = default);
    Task RemoveAsync(T model, CancellationToken token = default);
    Task UpdateAsync(T model, CancellationToken token = default);
}
```

**Target shape** (narrow, purpose-built — from RESEARCH.md Pattern 1):
```csharp
namespace QuestBoard.Domain.Interfaces;

public interface IIntegrationSettingService
{
    /// <summary>Returns the current settings row, creating a default (disabled, empty) row on first access.</summary>
    Task<IntegrationSetting> GetAsync(CancellationToken token = default);

    /// <summary>
    /// Persists the settings. If newSecret is null/whitespace, the existing OmphalosSharedSecret
    /// is preserved unchanged — callers must not pass an empty string to mean "clear it."
    /// </summary>
    Task SaveAsync(string omphalosUrl, string? newSecret, bool isEnabled, CancellationToken token = default);
}
```

---

### `QuestBoard.Domain/Models/IntegrationSetting.cs` (domain model)

**Analog:** `QuestBoard.Domain/Models/Group.cs`, full file:
```csharp
using QuestBoard.Domain.Enums;

namespace QuestBoard.Domain.Models;

public class Group : IModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public BoardType BoardType { get; set; }
}
```
`IntegrationSetting : IModel` should mirror this — flat public properties matching the entity's fields (`OmphalosUrl`, `OmphalosSharedSecret`, `IsEnabled`), no business logic in the model itself.

---

### `QuestBoard.Domain/Services/IntegrationSettingService.cs` (service, CRUD)

**Analog:** `QuestBoard.Domain/Services/GroupService.cs`, full file (44 lines):
```csharp
using AutoMapper;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;

namespace QuestBoard.Domain.Services;

internal class GroupService(IGroupRepository repository, IMapper mapper)
    : BaseService<Group>(repository, mapper), IGroupService
{
    /// <inheritdoc/>
    public async Task<bool> HasMembersAsync(int groupId, CancellationToken token = default)
        => await repository.HasMembersAsync(groupId, token);

    /// <inheritdoc/>
    public override async Task AddAsync(Group model, CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(model.Name))
            throw new ArgumentException("Group name is required.", nameof(model));
        model.CreatedAt = DateTime.UtcNow;
        await base.AddAsync(model, token);
    }
}
```
`IntegrationSettingService` differs structurally: it implements `IIntegrationSettingService` directly (not `BaseService<T>`, per Pattern 1's interface deviation) but still takes `IIntegrationSettingRepository repository` in its constructor and delegates to the repository's inherited `GetByIdAsync(1)`/`AddAsync`/`UpdateAsync` for the actual persistence — same delegation style as `GroupService`, just not inheriting `BaseService<Group>` since there's no matching `IBaseService<T>` to implement.

---

### `QuestBoard.Repository/Automapper/EntityProfile.cs` (MODIFIED)

**Analog:** same file — `GroupEntity ↔ Group` map (`EntityProfile.cs:148-153`):
```csharp
// Group mapping with BoardType int<->enum conversion
CreateMap<GroupEntity, Group>()
    .ForMember(dest => dest.BoardType, opt => opt.MapFrom(src => (BoardType)src.BoardType));

CreateMap<Group, GroupEntity>()
    .ForMember(dest => dest.BoardType, opt => opt.MapFrom(src => (int)src.BoardType));
```
`IntegrationSettingEntity ↔ IntegrationSetting` needs only a plain `CreateMap<IntegrationSettingEntity, IntegrationSetting>().ReverseMap();` — no enum conversion members, following the simpler `ReminderLog`/`ProposedDate` pattern instead (`EntityProfile.cs:53-57`):
```csharp
// ProposedDate mapping
CreateMap<ProposedDate, ProposedDateEntity>()
    .ReverseMap();

// ReminderLog mapping
CreateMap<ReminderLog, ReminderLogEntity>().ReverseMap();
```

---

### `QuestBoard.Repository/Extensions/ServiceExtensions.cs` (MODIFIED, repository DI)

**Analog:** same file, full file (45 lines) — add `services.AddScoped<IIntegrationSettingRepository, IntegrationSettingRepository>();` alongside:
```csharp
services.AddScoped<IUserRepository, UserRepository>();
...
services.AddScoped<IGroupRepository, GroupRepository>();
```

---

### `QuestBoard.Domain/Extensions/ServiceExtensions.cs` (MODIFIED, domain service DI)

**Analog:** same file, full file (28 lines) — add `services.AddScoped<IIntegrationSettingService, IntegrationSettingService>();` alongside:
```csharp
services.AddScoped<IGroupService, GroupService>();
services.AddScoped<IImageValidationService, ImageValidationService>();
```

---

### `QuestBoard.Repository/Migrations/<timestamp>_AddIntegrationSettings.cs` (migration)

**Analog:** `QuestBoard.Repository/Migrations/20260626190255_AddReminderLog.cs`, full file — standard EF-generated `CreateTable`:
```csharp
public partial class AddReminderLog : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ReminderLogs",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                QuestId = table.Column<int>(type: "int", nullable: false),
                PlayerId = table.Column<int>(type: "int", nullable: false),
                SentAt = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ReminderLogs", x => x.Id);
                // ... FKs ...
            });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "ReminderLogs");
    }
}
```
This file should be **generated**, not hand-written — run `dotnet ef migrations add AddIntegrationSettings --project ../QuestBoard.Repository` from `QuestBoard.Service/` per CLAUDE.md; EF Core will emit the `CreateTable` call automatically once `IntegrationSettingEntity`/`DbSet<IntegrationSettingEntity>`/`ValueGeneratedNever()` are in place. Do not use the raw-SQL `IF NOT EXISTS ... CREATE TABLE` style from `20260701163850_AddSessionStateTable.cs` — that pattern exists only because `AspNetSessionState` is outside EF's normal entity/DbSet management (ASP.NET Core distributed-session infrastructure); `IntegrationSettingEntity` is a normal EF-mapped entity and should get a normal generated migration.

---

### `QuestBoard.Service/Areas/Platform/Controllers/IntegrationsController.cs` (controller, request-response)

**Analogs:** `UsersController.cs` (scope/shape) + `GroupController.cs` (POST/TempData/error-handling shape)

**Scope reference, full file** (`QuestBoard.Service/Areas/Platform/Controllers/UsersController.cs`, 55 lines):
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
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var users = await userService.GetAllAsync();
        return View(viewModels);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Disable(int userId)
    {
        var result = await identityService.DisableUserAsync(userId);
        TempData[result.Succeeded ? "Success" : "Error"] = ...;
        return RedirectToAction(nameof(Index));
    }
}
```

**Imports pattern** (`GroupController.cs:1-15`):
```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.Service.ViewModels.PlatformViewModels;

namespace QuestBoard.Service.Areas.Platform.Controllers;

[Area("Platform")]
[Authorize(Policy = "SuperAdminOnly")]
public class GroupController(IGroupService groupService, ...) : Controller
```

**POST/save + TempData + blank-preserve shape to copy** (adapted from `GroupController.cs:38-56` `Create` action shape, per RESEARCH.md Pattern 3):
```csharp
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> Index(IntegrationSettingsViewModel model, CancellationToken token)
{
    if (!ModelState.IsValid) return View(model);

    // Blank secret field means "don't change it" — never overwrite the stored secret with
    // an empty string just because the field was left masked/blank on this edit.
    var newSecret = string.IsNullOrWhiteSpace(model.SharedSecret) ? null : model.SharedSecret;

    await integrationSettingService.SaveAsync(model.OmphalosUrl, newSecret, model.IsEnabled, token);
    TempData["Success"] = "Integration settings saved.";
    return RedirectToAction(nameof(Index));
}
```

**"Generate Secret" POST action** (new pattern, no direct analog — per RESEARCH.md Pitfall 3, persist immediately, don't defer to a separate Save):
```csharp
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> GenerateSecret(CancellationToken token)
{
    var newSecret = RandomNumberGenerator.GetString(SecretChars, length: 48);
    var current = await integrationSettingService.GetAsync(token);
    await integrationSettingService.SaveAsync(current.OmphalosUrl, newSecret, current.IsEnabled, token);
    TempData["GeneratedSecret"] = newSecret; // shown once, then gone
    return RedirectToAction(nameof(Index));
}
```

**Error-handling pattern (unique-constraint style, if applicable)** (`GroupController.cs:43-55`):
```csharp
catch (DbUpdateException ex) when (
    ex.InnerException?.Message.Contains("unique", StringComparison.OrdinalIgnoreCase) == true ||
    ex.InnerException?.Message.Contains("duplicate", StringComparison.OrdinalIgnoreCase) == true)
{
    ModelState.AddModelError(nameof(model.Name), "...");
    return View(model);
}
```
Not directly needed for `IntegrationsController` (no uniqueness constraint on a singleton row), but keep the same `try/catch` shape if any `DbUpdateException` handling is warranted.

---

### `QuestBoard.Service/ViewModels/PlatformViewModels/IntegrationSettingsViewModel.cs`

**Analogs:** `GroupEditViewModel.cs` (shape) + `EditShopItemViewModel.cs:36-39` (`[Url]` field)

**Shape reference, full file** (`GroupEditViewModel.cs`, 21 lines):
```csharp
using System.ComponentModel.DataAnnotations;

namespace QuestBoard.Service.ViewModels.PlatformViewModels;

public class GroupEditViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Group name is required.")]
    [StringLength(100, ErrorMessage = "Group name cannot exceed 100 characters.")]
    [Display(Name = "Group Name")]
    public string Name { get; set; } = string.Empty;
}
```

**`[Url]` DataAnnotation precedent** (`EditShopItemViewModel.cs:36-39`):
```csharp
[Display(Name = "D&D Beyond Reference URL")]
[StringLength(500)]
[Url]
public string? ReferenceUrl { get; set; }
```

**Target shape** — note `SharedSecret` must NOT be pre-populated on GET (RESEARCH.md Pattern 3); expose `HasSecretConfigured` for the "configured" indicator instead:
```csharp
public class IntegrationSettingsViewModel
{
    [Required, Url, StringLength(500)]
    [Display(Name = "Omphalos URL")]
    public string OmphalosUrl { get; set; } = string.Empty;

    [StringLength(200)]
    [DataType(DataType.Password)]
    [Display(Name = "Shared Secret")]
    public string? SharedSecret { get; set; } // never pre-filled on GET

    public bool HasSecretConfigured { get; set; } // GET-only, drives the "•••• configured" label

    [Display(Name = "Integration Enabled")]
    public bool IsEnabled { get; set; }
}
```

---

### `QuestBoard.Service/Areas/Platform/Views/Integrations/Index.cshtml` (desktop) / `Index.Mobile.cshtml` (mobile)

**Analogs:** `Areas/Platform/Views/Group/Index.cshtml` + `Index.Mobile.cshtml` (modern-card shell), `Views/Admin/EditUser.cshtml` (checkbox pattern)

**Modern-card shell** (`Group/Index.cshtml:1-20`):
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
    </div>
    <div class="card-body modern-card-body">
        ...
    </div>
</div>
```

**Checkbox pattern — plain `form-check`, never `form-switch`** (`Views/Admin/EditUser.cshtml:40-49`, confirmed the only boolean-toggle style in this codebase, per RESEARCH.md Pattern 5):
```html
<div class="mb-3">
    <div class="form-check">
        <input asp-for="HasKey" class="form-check-input" type="checkbox" />
        <label asp-for="HasKey" class="form-check-label">
            <i class="fas fa-key text-success me-2"></i>
            User has a building key (can close the building)
        </label>
    </div>
    <span asp-validation-for="HasKey" class="text-danger"></span>
</div>
```

**Form/button layout, `<hr>` + `d-flex justify-content-between`** (`Views/Admin/EditUser.cshtml:22-73`):
```html
<form asp-action="EditUser" method="post">
    <div asp-validation-summary="ModelOnly" class="text-danger mb-3"></div>
    <input asp-for="Id" type="hidden" />

    <div class="mb-3">
        <label asp-for="Name" class="form-label"></label>
        <input asp-for="Name" class="form-control" />
        <span asp-validation-for="Name" class="text-danger"></span>
    </div>

    <hr>

    <div class="d-flex justify-content-between">
        <a asp-action="..." class="btn btn-secondary">
            <i class="fas fa-arrow-left me-2"></i>Back
        </a>
        <button type="submit" class="btn btn-success">
            <i class="fas fa-save me-2"></i>Save Changes
        </button>
    </div>
</form>
```

Mobile view must ship in the same task as the desktop view (RESEARCH.md Pattern 4, PROJECT.md Phase 43/54 precedent) — `Group/Index.Mobile.cshtml` mirrors `Group/Index.cshtml` exactly, same card content, `platform-group-card-mobile`-style wrapper class instead of `card modern-card`, `btn-sm` on buttons.

---

### `QuestBoard.Service/Areas/Platform/Views/Group/Index.cshtml` / `Index.Mobile.cshtml` (MODIFIED — header button nav)

**Analog:** same files — the existing "Manage Users" button addition (already merged 2026-07-08)

**Desktop** (`Group/Index.cshtml:12-19`):
```html
<div class="d-flex gap-2">
    <a asp-controller="Group" asp-action="Create" asp-area="Platform" class="btn btn-success">
        <i class="fas fa-plus me-2"></i>Create Group
    </a>
    <a asp-controller="Users" asp-action="Index" asp-area="Platform" class="btn btn-secondary">
        <i class="fas fa-users-cog me-2"></i>Manage Users
    </a>
    <!-- ADD: -->
    <a asp-controller="Integrations" asp-action="Index" asp-area="Platform" class="btn btn-secondary">
        <i class="fas fa-plug me-2"></i>Integrations
    </a>
</div>
```

**Mobile** (`Group/Index.Mobile.cshtml:15-22`, same pattern with `btn-sm`):
```html
<div class="d-flex flex-wrap gap-2">
    <a asp-controller="Group" asp-action="Create" asp-area="Platform" class="btn btn-success btn-sm">
        <i class="fas fa-plus me-2"></i>Create Group
    </a>
    <a asp-controller="Users" asp-action="Index" asp-area="Platform" class="btn btn-secondary btn-sm">
        <i class="fas fa-users-cog me-2"></i>Manage Users
    </a>
    <!-- ADD: -->
    <a asp-controller="Integrations" asp-action="Index" asp-area="Platform" class="btn btn-secondary btn-sm">
        <i class="fas fa-plug me-2"></i>Integrations
    </a>
</div>
```
There is no shared Platform-area nav list (`_Layout.Platform.cshtml`/`_Layout.Platform.Mobile.cshtml` confirmed to have none) — this header-button wiring is the only cross-linking mechanism between Platform pages. Do not add a shared nav item.

---

### `QuestBoard.UnitTests/Services/IntegrationSettingServiceTests.cs`

**Analog:** `QuestBoard.UnitTests/Services/GroupServiceTests.cs`, full file (72 lines) — NSubstitute + xUnit v3 pattern:
```csharp
using AutoMapper;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.Domain.Services;
using NSubstitute;

namespace QuestBoard.UnitTests.Services;

public class GroupServiceTests
{
    private readonly IGroupRepository _repository;
    private readonly IMapper _mapper;
    private readonly GroupService _sut;

    public GroupServiceTests()
    {
        _repository = Substitute.For<IGroupRepository>();
        _mapper = Substitute.For<IMapper>();
        _sut = new GroupService(_repository, _mapper);
    }

    [Fact]
    public async Task GetMembersAsync_DelegatesToRepositoryAndReturnsSameList()
    {
        // Arrange
        var expectedList = new List<UserGroup> { new() { Id = 1, UserId = 1, GroupId = 1 } };
        _repository.GetMembersAsync(Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(expectedList);

        // Act
        var result = await _sut.GetMembersAsync(1, "term", TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeSameAs(expectedList);
        await _repository.Received(1).GetMembersAsync(1, "term", Arg.Any<CancellationToken>());
    }
}
```
For `IntegrationSettingServiceTests`, the key test cases (per SETT-04/SETT-05) are: `SaveAsync` with a non-blank secret overwrites `OmphalosSharedSecret`; `SaveAsync` with a null/whitespace secret preserves the existing value (assert the repository's `UpdateAsync`/underlying entity received the *old* secret, not an empty string); `IsEnabled` round-trips through `SaveAsync`/`GetAsync` unchanged.

---

### `QuestBoard.IntegrationTests/Controllers/IntegrationsAreaIntegrationTests.cs`

**Analog:** `QuestBoard.IntegrationTests/Controllers/PlatformAreaIntegrationTests.cs`, full file (74 lines) — copy this 4-test authorization matrix verbatim, retargeted at `/platform/Integrations/Index`:
```csharp
using System.Net;
using QuestBoard.IntegrationTests.Helpers;

namespace QuestBoard.IntegrationTests.Controllers;

public class PlatformAreaIntegrationTests : IClassFixture<WebApplicationFactoryBase>
{
    private readonly WebApplicationFactoryBase _factory;

    [Fact]
    public async Task PlatformIndex_WhenSuperAdmin_ShouldReturn200()
    {
        await TestDataHelper.ClearDatabaseAsync(_factory.Services);
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(_factory);
        var response = await client.GetAsync("/platform/Group/Index", TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PlatformIndex_WhenNotSuperAdmin_ShouldDeny()
    {
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, "regularuser", "regular@test.com", roles: ["Player"]);
        var response = await client.GetAsync("/platform/Group/Index", TestContext.Current.CancellationToken);
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.Redirect);
    }

    [Fact]
    public async Task PlatformIndex_WhenNotAuthenticated_ShouldRedirect()
    {
        var unauthClient = _factory.CreateNonRedirectingClient();
        var response = await unauthClient.GetAsync("/platform/Group/Index", TestContext.Current.CancellationToken);
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PlatformIndex_WhenAdmin_ShouldDeny()
    {
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            _factory, "adminuser", "admin@test.com", roles: ["Admin"]);
        var response = await client.GetAsync("/platform/Group/Index", TestContext.Current.CancellationToken);
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.Redirect);
    }
}
```
Retarget every `"/platform/Group/Index"` to `"/platform/Integrations/Index"` and rename the class/methods accordingly (`IntegrationsIndex_WhenSuperAdmin_ShouldReturn200`, etc.) — same `AuthenticationHelper` calls, same assertions, same 4-case shape (SuperAdmin/Player/unauthenticated/Admin).

## Shared Patterns

### Authorization — `SuperAdminOnly` policy
**Source:** `QuestBoard.Service/Areas/Platform/Controllers/{GroupController.cs:17-18,UsersController.cs:8-9}`
**Apply to:** `IntegrationsController.cs` (controller-level, not per-action)
```csharp
[Area("Platform")]
[Authorize(Policy = "SuperAdminOnly")]
public class IntegrationsController(IIntegrationSettingService integrationSettingService) : Controller
```
Registered in `Program.cs:88-89` as `policy.RequireRole("SuperAdmin")` — no new policy needed.

### CSRF protection
**Source:** every mutating action in `GroupController.cs`/`UsersController.cs` (`[HttpPost] [ValidateAntiForgeryToken]`)
**Apply to:** `IntegrationsController.Index(POST)` and `IntegrationsController.GenerateSecret(POST)`

### Blank-preserves-existing-value guard (SETT-04)
**Source:** adapted from `QuestBoard.Service/Controllers/Contacts/ContactsController.cs:238` ("Otherwise, the contact image remains unchanged" — file-upload guard, adapted here to a `string?` field)
**Apply to:** `IntegrationsController.Index(POST)` — `var newSecret = string.IsNullOrWhiteSpace(model.SharedSecret) ? null : model.SharedSecret;` then pass `null` through to the service, which must not overwrite the stored value when `null`.
**Do NOT copy from:** `AdminController.EditUser` (`QuestBoard.Service/Controllers/Admin/AdminController.cs:224` area) — that handles email-change token flow, not a masked-field blank-preserve; CONTEXT.md's `code_context` explicitly flags this as the wrong analog.

### WebEncoders Base64URL convention (for the token contract document, consumed by Phase 73/74, not this phase's runtime code)
**Source:** `QuestBoard.Service/Controllers/Admin/AdminController.cs:134,166,265,400` (4 call sites)
```csharp
var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(rawToken));
```
**Apply to:** the written `.planning/TOKEN-CONTRACT.md` document — name `Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlEncode`/`Base64UrlDecode` explicitly as the encoding API Phase 73 (signer) and Phase 74 (verifier) must both use, per D-02.

### Modern-card UI shell
**Source:** `Group/Index.cshtml:6-20`, `EditUser.cshtml:14-21` (`modern-card`/`modern-card-header`/`modern-card-body`, per root `CLAUDE.md`)
**Apply to:** `Integrations/Index.cshtml` (desktop)

### Singleton-row bootstrap ("does it exist yet")
**Source:** RESEARCH.md's "Don't Hand-Roll" table — `BaseRepository.GetByIdAsync(1)` (`BaseRepository.cs:44-48`) returning `null` on first run; the service layer's `GetAsync()` then creates and persists a default row.
**Apply to:** `IntegrationSettingService.GetAsync()` — no raw SQL `SELECT COUNT(*)` or bootstrap script needed.

### DI registration
**Source:** `QuestBoard.Repository/Extensions/ServiceExtensions.cs` (repository registrations), `QuestBoard.Domain/Extensions/ServiceExtensions.cs` (service registrations)
**Apply to:** register `IIntegrationSettingRepository`/`IntegrationSettingRepository` and `IIntegrationSettingService`/`IntegrationSettingService` as `AddScoped`, alongside the `Group`-equivalent lines in each file.

## No Analog Found

| File | Role | Data Flow | Reason |
|---|---|---|---|
| `.planning/TOKEN-CONTRACT.md` | config (design doc) | — | No prior written cross-repo contract document exists in this codebase's `.planning/` — this is a new artifact type (design spec, not code). Content requirements are fully specified in RESEARCH.md's "Token Contract Design" section (field list, types, signing/verification sequence) — the planner should treat that section as the source content to transcribe into the new file, not search for a codebase analog. |

## Metadata

**Analog search scope:** `QuestBoard.Repository/` (Entities, root, Automapper, Extensions, Migrations), `QuestBoard.Domain/` (Interfaces, Models, Services, Extensions), `QuestBoard.Service/` (Areas/Platform, Controllers/Admin, Controllers/Contacts, ViewModels, Views/Admin), `QuestBoard.UnitTests/Services/`, `QuestBoard.IntegrationTests/Controllers/`
**Files scanned:** ~24 (all direct file reads listed above; no files re-read)
**Pattern extraction date:** 2026-07-08
