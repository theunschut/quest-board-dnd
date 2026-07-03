# Phase 35: Board Type Configuration - Pattern Map

**Mapped:** 2026-07-03
**Files analyzed:** 13 (2 new, 11 modified)
**Analogs found:** 13 / 13

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|--------------------|------|-----------|-----------------|----------------|
| `QuestBoard.Domain/Enums/BoardType.cs` (NEW) | model (enum) | — | `QuestBoard.Domain/Enums/GroupRole.cs` | exact |
| `QuestBoard.Domain/Models/Group.cs` | model | CRUD | itself (add property) — sibling pattern: `UserGroup.cs` int↔enum field | exact |
| `QuestBoard.Domain/Models/GroupWithMemberCount.cs` | model (projection) | CRUD (read) | itself (add property) | exact |
| `QuestBoard.Repository/Entities/GroupEntity.cs` | model (entity) | CRUD | itself (add int property) — sibling: `UserGroupEntity.GroupRole` (int) | exact |
| `QuestBoard.Repository/Automapper/EntityProfile.cs` (Group mapping section) | service (mapper config) | transform | `UserGroupEntity ↔ UserGroup` mapping, same file lines 124-130 | exact |
| `QuestBoard.Repository/GroupRepository.cs` (two `.Select(...)` projections) | service (repository) | CRUD | itself (add field to both existing projections) | exact |
| `QuestBoard.Repository/Migrations/{timestamp}_AddBoardTypeToGroup.cs` (NEW) | migration | batch | `20260129155948_AddSignupRoleToPlayerSignup.cs` | exact |
| `QuestBoard.Service/Areas/Platform/Controllers/GroupController.cs` (Create/Edit actions) | controller | request-response | itself (existing Create/Edit actions, same file) | exact |
| `QuestBoard.Service/ViewModels/PlatformViewModels/GroupCreateViewModel.cs` | model (viewmodel) | request-response | itself (add property) | exact |
| `QuestBoard.Service/ViewModels/PlatformViewModels/GroupEditViewModel.cs` | model (viewmodel) | request-response | itself (add display-only property) | exact |
| `Areas/Platform/Views/Group/Create.cshtml` + `.Mobile.cshtml` | component (Razor view) | request-response | `Areas/Platform/Views/Group/Members.cshtml` (enum dropdown + blank-option idiom) | role-match |
| `Areas/Platform/Views/Group/Edit.cshtml` + `.Mobile.cshtml` | component (Razor view) | request-response | `Create.cshtml` (own sibling, disabled variant) + `Members.cshtml` (dropdown idiom) | role-match |
| `Areas/Platform/Views/Group/Index.cshtml` + `.Mobile.cshtml` | component (Razor view) | CRUD (read/list) | `Members.cshtml` (badge if/else convention) + itself (existing table column) | exact |
| `QuestBoard.IntegrationTests/Controllers/GroupManagementIntegrationTests.cs` (new `[Fact]`s) | test | request-response | itself (existing `CreateGroup_*`/`GroupsIndex_*` facts, same file) | exact |

## Pattern Assignments

### `QuestBoard.Domain/Enums/BoardType.cs` (NEW enum)

**Analog:** `QuestBoard.Domain/Enums/GroupRole.cs` (full file, 9 lines)

```csharp
// Source: QuestBoard.Domain/Enums/GroupRole.cs (verified in repo, full content)
namespace QuestBoard.Domain.Enums;

public enum GroupRole
{
    Player = 0,
    DungeonMaster = 1,
    Admin = 2
}
```

**Apply as:**
```csharp
namespace QuestBoard.Domain.Enums;

public enum BoardType
{
    OneShot = 0,
    Campaign = 1
}
```

---

### `QuestBoard.Domain/Models/Group.cs` (model, CRUD)

**Analog:** current file itself (full content, 8 lines) — add one property.

```csharp
// Source: QuestBoard.Domain/Models/Group.cs (verified in repo, full current content)
namespace QuestBoard.Domain.Models;

public class Group : IModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
```

**Add:** `public BoardType BoardType { get; set; }` (requires `using QuestBoard.Domain.Enums;` import — not currently imported in this file).

---

### `QuestBoard.Domain/Models/GroupWithMemberCount.cs` (projection model, CRUD read)

**Analog:** current file itself (full content, 9 lines).

```csharp
// Source: QuestBoard.Domain/Models/GroupWithMemberCount.cs (verified in repo, full current content)
namespace QuestBoard.Domain.Models;

public class GroupWithMemberCount
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public int MemberCount { get; set; }
}
```

**Add:** `public BoardType BoardType { get; set; }` (same enum import needed). **Do not skip this file** — `Index.cshtml` binds to `GroupWithMemberCount`, not `Group` (see Shared Patterns > Projection Model Consistency below).

---

### `QuestBoard.Repository/Entities/GroupEntity.cs` (entity, CRUD)

**Analog:** current file itself (full content, 20 lines) — sibling `UserGroupEntity.GroupRole` shows the int-backed-enum-on-entity idiom.

```csharp
// Source: QuestBoard.Repository/Entities/GroupEntity.cs (verified in repo, full current content)
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

    public virtual ICollection<UserGroupEntity> UserGroups { get; set; } = [];
}
```

**Add:** `public int BoardType { get; set; }` (plain `int`, no `[Required]`/`[StringLength]` needed — matches how `UserGroupEntity.GroupRole` is declared as bare `int`). No fluent-API config needed in `QuestBoardContext.OnModelCreating` — confirmed the only fluent config touching `GroupEntity` is the unique index on `Name` (`QuestBoardContext.cs:200-202`); a new plain int column requires no additional fluent config.

---

### `QuestBoard.Repository/Automapper/EntityProfile.cs` (Group mapping section, lines 120-121)

**Analog:** `UserGroupEntity ↔ UserGroup` mapping, same file, lines 124-130 (exact int↔enum `ForMember` idiom already established).

**Current state to replace** (lines 120-121):
```csharp
// Group mapping
CreateMap<GroupEntity, Group>().ReverseMap();
```

**Analog pattern to replicate** (lines 124-130, verified current content):
```csharp
// UserGroup mapping with GroupRole int↔enum conversion
CreateMap<UserGroupEntity, UserGroup>()
    .ForMember(dest => dest.GroupRole, opt => opt.MapFrom(src => (GroupRole)src.GroupRole))
    .ForMember(dest => dest.User, opt => opt.MapFrom(src => src.User));

CreateMap<UserGroup, UserGroupEntity>()
    .ForMember(dest => dest.GroupRole, opt => opt.MapFrom(src => (int)src.GroupRole))
    .ForMember(dest => dest.User, opt => opt.Ignore());
```

**Apply as:**
```csharp
// Group mapping with BoardType int<->enum conversion
CreateMap<GroupEntity, Group>()
    .ForMember(dest => dest.BoardType, opt => opt.MapFrom(src => (BoardType)src.BoardType));

CreateMap<Group, GroupEntity>()
    .ForMember(dest => dest.BoardType, opt => opt.MapFrom(src => (int)src.BoardType));
```

Note: this breaks the existing bare `.ReverseMap()` into two explicit `CreateMap` calls (one per direction), matching the `UserGroup` shape exactly. `GroupWithMemberCount` is not built via AutoMapper (it's populated by hand in `GroupRepository`'s LINQ `.Select(...)` projections) — no separate `CreateMap` entry needed for it.

---

### `QuestBoard.Repository/GroupRepository.cs` (two `.Select(...)` projections, lines 14-40)

**Analog:** current file itself — both projections already share an identical shape; extend both identically.

**Current state** (verified full content, lines 14-40):
```csharp
// Source: QuestBoard.Repository/GroupRepository.cs (verified in repo, current content)
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

public async Task<IList<GroupWithMemberCount>> GetGroupsForUserAsync(int userId, CancellationToken token = default)
{
    return await DbContext.Groups
        .Where(g => g.UserGroups.Any(ug => ug.UserId == userId))
        .Select(g => new GroupWithMemberCount
        {
            Id = g.Id,
            Name = g.Name,
            CreatedAt = g.CreatedAt,
            MemberCount = g.UserGroups.Count
        })
        .ToListAsync(token);
}
```

**Apply:** add `BoardType = (BoardType)g.BoardType,` to both anonymous-init `GroupWithMemberCount` blocks (file already has `using QuestBoard.Domain.Enums;` at line 3 for `GroupRole`, so no new import needed).

**Critical — do not skip:** if only `GroupEntity`/`Group` are updated and these two projections are left alone, `Index.cshtml`'s new badge column will fail to compile (`GroupWithMemberCount` won't have `BoardType` populated). This was flagged as Pitfall 2 in RESEARCH.md.

---

### `QuestBoard.Repository/Migrations/{timestamp}_AddBoardTypeToGroup.cs` (NEW migration)

**Analog:** `20260129155948_AddSignupRoleToPlayerSignup.cs` (full file, verified current content, 29 lines) — direct template, zero deviation needed.

```csharp
// Source: QuestBoard.Repository/Migrations/20260129155948_AddSignupRoleToPlayerSignup.cs (verified in repo, full file)
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuestBoard.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddSignupRoleToPlayerSignup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SignupRole",
                table: "PlayerSignups",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SignupRole",
                table: "PlayerSignups");
        }
    }
}
```

**Apply as:** identical shape, `name: "BoardType"`, `table: "Groups"`, `defaultValue: 0` (0 = `OneShot`, satisfying the "existing groups default to One-Shot" requirement with no `Sql()` backfill needed). Generate via `dotnet ef migrations add AddBoardTypeToGroup --project ../QuestBoard.Repository` from `QuestBoard.Service/` — do not hand-write the migration class; let EF tooling generate it and then verify the `Up`/`Down` match this shape exactly. Confirm the generated timestamp prefix sorts after `20260702081517` (the current latest migration).

---

### `QuestBoard.Service/Areas/Platform/Controllers/GroupController.cs` (Create/Edit actions, lines 27-77)

**Analog:** itself — current Create/Edit action bodies (verified full current content).

**Create POST** (lines 27-45, current state):
```csharp
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> Create(GroupCreateViewModel model)
{
    if (!ModelState.IsValid) return View(model);
    try
    {
        await groupService.AddAsync(new Group { Name = model.Name });
        TempData["Success"] = "Group created successfully.";
        return RedirectToAction(nameof(Index));
    }
    catch (DbUpdateException ex) when (
        ex.InnerException?.Message.Contains("unique", StringComparison.OrdinalIgnoreCase) == true ||
        ex.InnerException?.Message.Contains("duplicate", StringComparison.OrdinalIgnoreCase) == true)
    {
        ModelState.AddModelError(nameof(model.Name), "A group with that name already exists. Please choose a different name.");
        return View(model);
    }
}
```
**Apply:** change the `new Group { Name = model.Name }` line to `new Group { Name = model.Name, BoardType = model.BoardType!.Value }` (given `GroupCreateViewModel.BoardType` is nullable `BoardType?` + `[Required]`, per Pitfall 3 — see ViewModel section below). No other structural change; error handling/try-catch shape is unchanged.

**Edit GET + POST** (lines 48-77, current state):
```csharp
[HttpGet]
public async Task<IActionResult> Edit(int id)
{
    var group = await groupService.GetByIdAsync(id);
    if (group == null) return RedirectToAction(nameof(Index));
    return View(new GroupEditViewModel { Id = group.Id, Name = group.Name });
}

[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> Edit(GroupEditViewModel model)
{
    if (!ModelState.IsValid) return View(model);
    var group = await groupService.GetByIdAsync(model.Id);
    if (group == null) return RedirectToAction(nameof(Index));
    try
    {
        group.Name = model.Name;
        await groupService.UpdateAsync(group);
        TempData["Success"] = "Group name updated.";
        return RedirectToAction(nameof(Index));
    }
    catch (DbUpdateException ex) when (...)
    {
        ModelState.AddModelError(nameof(model.Name), "A group with that name already exists. Please choose a different name.");
        return View(model);
    }
}
```
**Apply:**
- Edit GET: change `new GroupEditViewModel { Id = group.Id, Name = group.Name }` to `new GroupEditViewModel { Id = group.Id, Name = group.Name, BoardType = group.BoardType }` (populates the display-only field).
- Edit POST: **do not add any line reading `model.BoardType`**. `group.Name = model.Name;` must remain the *only* assignment statement in the try block — this omission is what implements D-06's silent-tamper-rejection with zero extra code, matching the existing "only `Name` is copied back" convention exactly (this is also the codebase's *only* precedent for field-level write protection — no `[BindNever]`/allowlist pattern exists anywhere else to imitate instead).

---

### `QuestBoard.Service/ViewModels/PlatformViewModels/GroupCreateViewModel.cs` (viewmodel, request-response)

**Analog:** current file itself (full content, 11 lines).

```csharp
// Source: QuestBoard.Service/ViewModels/PlatformViewModels/GroupCreateViewModel.cs (verified in repo, full current content)
using System.ComponentModel.DataAnnotations;

namespace QuestBoard.Service.ViewModels.PlatformViewModels;

public class GroupCreateViewModel
{
    [Required(ErrorMessage = "Group name is required.")]
    [StringLength(100, ErrorMessage = "Group name cannot exceed 100 characters.")]
    [Display(Name = "Group Name")]
    public string Name { get; set; } = string.Empty;
}
```

**Apply:** add (requires new `using QuestBoard.Domain.Enums;` import):
```csharp
[Required(ErrorMessage = "Board type is required.")]
[Display(Name = "Board Type")]
public BoardType? BoardType { get; set; }
```
**Must be nullable `BoardType?`**, not plain `BoardType` — a non-nullable enum silently model-binds an empty `<select>` submission to `default(BoardType)` (=`OneShot`, ordinal 0), which would defeat D-03's "no pre-selected default, forces active choice" requirement without any visible validation error. This is Pitfall 3 in RESEARCH.md (confidence: MEDIUM — based on standard ASP.NET Core model-binding behavior, not verified via official docs this session; test manually by submitting Create with no Board Type selected and confirming a validation error appears).

---

### `QuestBoard.Service/ViewModels/PlatformViewModels/GroupEditViewModel.cs` (viewmodel, display-only)

**Analog:** current file itself (full content, 13 lines).

```csharp
// Source: QuestBoard.Service/ViewModels/PlatformViewModels/GroupEditViewModel.cs (verified in repo, full current content)
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

**Apply:** add (requires `using QuestBoard.Domain.Enums;`):
```csharp
[Display(Name = "Board Type")]
public BoardType BoardType { get; set; }
```
Plain non-nullable `BoardType` here (not `BoardType?`) — no `[Required]` needed since this property is display-only and never validated on POST (the disabled `<select>` doesn't submit a value anyway; see D-06). The property exists purely so `Edit.cshtml`'s GET-rendered view has a value to display in the disabled dropdown.

---

### `Areas/Platform/Views/Group/Create.cshtml` + `.Mobile.cshtml` (component, request-response)

**Analog for dropdown + blank-option idiom:** `Areas/Platform/Views/Group/Members.cshtml` lines 104-120 (verified current content).

```html
<!-- Source: QuestBoard.Service/Areas/Platform/Views/Group/Members.cshtml (verified in repo) -->
<div class="mb-3">
    <label asp-for="AddMember.UserId" class="form-label">User</label>
    <select asp-for="AddMember.UserId" class="form-select">
        <option value="">-- Select a user --</option>
        @foreach (var u in Model.AvailableUsers)
        {
            <option value="@u.Id">@u.Name (@u.Email)</option>
        }
    </select>
    <span asp-validation-for="AddMember.UserId" class="text-danger"></span>
</div>

<div class="mb-3">
    <label asp-for="AddMember.Role" class="form-label">Role</label>
    <select asp-for="AddMember.Role" class="form-select" asp-items="Html.GetEnumSelectList<GroupRole>()">
    </select>
    <span asp-validation-for="AddMember.Role" class="text-danger"></span>
</div>
```

**Structural analog for the surrounding form:** `Create.cshtml` itself (full current content, verified, 39 lines):
```html
<!-- Source: QuestBoard.Service/Areas/Platform/Views/Group/Create.cshtml (verified in repo, full current content) -->
@model GroupCreateViewModel
@{
    ViewData["Title"] = "Create Group";
}

<div class="row justify-content-center">
    <div class="col-md-6">
        <div class="card modern-card">
            <div class="card-header modern-card-header">
                <h2 class="mb-0">
                    <i class="fas fa-plus-circle text-danger me-2"></i>
                    Create Group
                </h2>
            </div>
            <div class="card-body modern-card-body">
                <form asp-action="Create" method="post">
                    <div asp-validation-summary="ModelOnly" class="text-danger mb-3"></div>

                    <div class="mb-3">
                        <label asp-for="Name" class="form-label">Group Name</label>
                        <input asp-for="Name" class="form-control" />
                        <span asp-validation-for="Name" class="text-danger"></span>
                    </div>

                    <hr>

                    <div class="d-flex justify-content-between">
                        <a asp-action="Index" asp-area="Platform" class="btn btn-secondary">
                            <i class="fas fa-arrow-left me-2"></i>Back to Groups
                        </a>
                        <button type="submit" class="btn btn-success">
                            <i class="fas fa-save me-2"></i>Create Group
                        </button>
                    </div>
                </form>
            </div>
        </div>
    </div>
</div>
```

**Apply:** insert a new `div.mb-3` block for `BoardType` between the `Name` field and the `<hr>`, combining both idioms per D-01/D-02/D-03/D-04:
```html
<div class="mb-3">
    <label asp-for="BoardType" class="form-label">Board Type</label>
    <select asp-for="BoardType" class="form-select">
        <option value="">-- Select Board Type --</option>
        @foreach (var item in Html.GetEnumSelectList<BoardType>())
        {
            <option value="@item.Value">@item.Text</option>
        }
    </select>
    <span asp-validation-for="BoardType" class="text-danger"></span>
    <div class="form-text">This cannot be changed after creation.</div>
</div>
```
Note: because D-03 requires no pre-selected default, `asp-items="Html.GetEnumSelectList<BoardType>()"` cannot be used directly combined with a hardcoded blank `<option>` in the exact same shorthand as `Members.cshtml`'s `Role` field (that field has no blank option and defaults to ordinal 0 intentionally) — instead follow the `AddMember.UserId` blank-option idiom (manual `@foreach` loop) so the blank placeholder renders first. No helper text per D-02 (RESEARCH.md's `[Display(Name=...)]` recommendation covers the label; do not add a "what is One-Shot/Campaign" explanation). Add the permanence-warning `<div class="form-text">` per D-04.

`Create.Mobile.cshtml` — apply the identical `BoardType` block inside its `<form>`, mirroring how `Name` already appears identically in both `Create.cshtml`/`Create.Mobile.cshtml` (structurally the Mobile variant is the same form fields inside a `.platform-group-card-mobile` div instead of a Bootstrap card).

---

### `Areas/Platform/Views/Group/Edit.cshtml` + `.Mobile.cshtml` (component, request-response)

**Analog:** `Edit.cshtml` itself (full current content, verified, 41 lines) + the same `Members.cshtml` dropdown idiom, rendered `disabled`.

```html
<!-- Source: QuestBoard.Service/Areas/Platform/Views/Group/Edit.cshtml (verified in repo, full current content) -->
@model GroupEditViewModel
@{
    ViewData["Title"] = "Edit Group";
}
...
<form asp-action="Edit" method="post">
    <div asp-validation-summary="ModelOnly" class="text-danger mb-3"></div>

    <input asp-for="Id" type="hidden" />

    <div class="mb-3">
        <label asp-for="Name" class="form-label">Group Name</label>
        <input asp-for="Name" class="form-control" />
        <span asp-validation-for="Name" class="text-danger"></span>
    </div>

    <hr>
    ...
</form>
```

**Apply:** insert a disabled, visually-consistent `<select>` block after `Name`, before `<hr>`:
```html
<div class="mb-3">
    <label asp-for="BoardType" class="form-label">Board Type</label>
    <select asp-for="BoardType" class="form-select" asp-items="Html.GetEnumSelectList<BoardType>()" disabled>
    </select>
    <div class="form-text">This cannot be changed after creation.</div>
</div>
```
Because the `<select>` is `disabled`, the browser will not submit its value on POST regardless of tampering via normal form interaction — but D-06 is about devtools tampering (removing `disabled` client-side or crafting a raw POST), so the real protection is server-side: **`GroupEditViewModel.BoardType` must never be read in the controller's `Edit` POST body** (see Controller section above). The `disabled` attribute is a UX nicety, not the security boundary.

`Edit.Mobile.cshtml` — same block, inside its `.platform-group-card-mobile` form, mirroring how `Name`/`Id` already appear identically between `Edit.cshtml` and `Edit.Mobile.cshtml`.

---

### `Areas/Platform/Views/Group/Index.cshtml` + `.Mobile.cshtml` (component, CRUD read)

**Analog for badge if/else convention:** `Members.cshtml` lines 55-72 (verified current content).

```html
<!-- Source: QuestBoard.Service/Areas/Platform/Views/Group/Members.cshtml (verified in repo) -->
@if (member.GroupRole == GroupRole.Admin)
{
    <span class="badge bg-danger">
        <i class="fas fa-shield-alt me-1"></i>Admin
    </span>
}
else if (member.GroupRole == GroupRole.DungeonMaster)
{
    <span class="badge bg-warning text-dark">
        <i class="fas fa-crown me-1"></i>DungeonMaster
    </span>
}
else
{
    <span class="badge bg-primary">
        <i class="fas fa-dice-d20 me-1"></i>Player
    </span>
}
```

**Structural analog for the table:** `Index.cshtml` itself, lines 40-70 (verified current content).

```html
<!-- Source: QuestBoard.Service/Areas/Platform/Views/Group/Index.cshtml (verified in repo, current content) -->
<thead>
    <tr>
        <th scope="col">Name</th>
        <th scope="col">Members</th>
        <th scope="col">Created</th>
        <th scope="col">Actions</th>
    </tr>
</thead>
<tbody>
    @foreach (var item in Model.Groups)
    {
        <tr>
            <td>@item.Name</td>
            <td>@item.MemberCount</td>
            <td>@item.CreatedAt.ToString("yyyy-MM-dd")</td>
            <td class="text-end">
                ...
            </td>
        </tr>
    }
</tbody>
```

**Apply:** add a new `<th scope="col">Board Type</th>` column header between `Name` and `Members` (per D-07, planner's choice on ordering — this ordering matches CONTEXT.md's suggested placement), and a matching `<td>` per row using the two-value if/else badge idiom (only `OneShot`/`Campaign`, so a single `if`/`else` suffices — no `else if` chain needed):
```html
<td>
    @if (item.BoardType == BoardType.Campaign)
    {
        <span class="badge bg-info text-dark">
            <i class="fas fa-book me-1"></i>Campaign
        </span>
    }
    else
    {
        <span class="badge bg-secondary">
            <i class="fas fa-dice-d20 me-1"></i>One-Shot
        </span>
    }
</td>
```
(Exact badge colors/icons are implementer's discretion per CONTEXT.md — above is illustrative, following the existing `bg-{color}` + FontAwesome-icon-with-me-1 shape.)

`Index.Mobile.cshtml` — analog is its own current content (verified, lines 42-61): each group renders as a `div.group-card-mobile` with a `<small>` line showing `Members: @item.MemberCount &middot; Created: @item.CreatedAt...`. Add the `BoardType` badge either inline next to the group name (`<strong class="parchment-text">@item.Name</strong>`) or as an additional line — planner's call, following the existing terse mobile-card idiom rather than adding a full table.

---

### `QuestBoard.IntegrationTests/Controllers/GroupManagementIntegrationTests.cs` (new `[Fact]`s)

**Analog:** existing `CreateGroup_WithValidName_ShouldRedirectOrReturn200` and `GroupsIndex_ShouldShowSeededGroup` facts, same file, lines 36-46 and 49-62 (verified current content).

```csharp
// Source: QuestBoard.IntegrationTests/Controllers/GroupManagementIntegrationTests.cs (verified in repo, current content)
[Fact]
public async Task CreateGroup_WithValidName_ShouldRedirectOrReturn200()
{
    await TestDataHelper.ClearDatabaseAsync(_factory.Services);
    var (client, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(_factory);
    var uniqueName = "TestGroup_" + Guid.NewGuid().ToString("N")[..8];
    var formData = new Dictionary<string, string> { ["Name"] = uniqueName };

    var response = await client.PostAsync("/platform/Group/Create",
        new FormUrlEncodedContent(formData), TestContext.Current.CancellationToken);

    // Should redirect to Index after successful creation
    response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.OK);
}
```

**Apply as template for new facts:**
- `CreateGroup_WithBoardType_ShouldPersistSelection` — POST with `formData["BoardType"] = "1"` (Campaign), then assert via `dbContext.Groups.FirstOrDefault(...)BoardType == 1` (mirrors the `DeleteGroup_WhenEmpty_ShouldShowDeleteConfirmation` fact's pattern of reaching into `QuestBoardContext` via `_factory.Services.CreateScope()`, lines 94-98 of the same file).
- `CreateGroup_WithoutBoardType_ShouldFailValidation` — POST with `formData` containing only `["Name"]`, omitting `BoardType` entirely; assert `response.StatusCode` is `OK` (re-rendered form with validation error) rather than a redirect, and content contains the `[Required]` error message.
- `EditGroup_PostingChangedBoardType_ShouldBeSilentlyIgnored` — create a group with `BoardType=OneShot` (0), then POST `/platform/Group/Edit` with `formData["BoardType"] = "1"` alongside valid `Id`/`Name`, then re-query the DB and assert `BoardType` is still `0`.
- Extend `GroupsIndex_ShouldShowSeededGroup` (or add a new fact) asserting the seeded `EuphoriaInn` group defaults to `OneShot` (0) after migration — reach into `QuestBoardContext` the same way `DeleteGroup_WhenEmpty_ShouldShowDeleteConfirmation` does.

All new facts should use `await TestDataHelper.ClearDatabaseAsync(_factory.Services);` and `AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(_factory)` at the top, exactly like every existing fact in this file (this is a hard, uniform convention — every single fact in the file starts with these two lines).

---

## Shared Patterns

### Int-backed enum on entity/domain model (project-wide convention)
**Source:** `QuestBoard.Repository/Automapper/EntityProfile.cs:124-130` (`UserGroupEntity ↔ UserGroup`)
**Apply to:** `GroupEntity.BoardType` (int) / `Group.BoardType` (enum) / `EntityProfile.cs` Group mapping section
```csharp
CreateMap<UserGroupEntity, UserGroup>()
    .ForMember(dest => dest.GroupRole, opt => opt.MapFrom(src => (GroupRole)src.GroupRole))
    .ForMember(dest => dest.User, opt => opt.MapFrom(src => src.User));

CreateMap<UserGroup, UserGroupEntity>()
    .ForMember(dest => dest.GroupRole, opt => opt.MapFrom(src => (int)src.GroupRole))
    .ForMember(dest => dest.User, opt => opt.Ignore());
```
Every enum in this codebase (`GroupRole`, `SignupRole`, `ItemRarity`, `ItemType`, `ItemStatus`, `TransactionType`, `CharacterStatus`, `CharacterRole`, `DndClass`, `VoteType`) follows this shape without exception — `BoardType` must too.

### Projection Model Consistency (Group ↔ GroupWithMemberCount)
**Source:** `QuestBoard.Repository/GroupRepository.cs:14-40`
**Apply to:** `GroupWithMemberCount.cs`, both `.Select(...)` projections in `GroupRepository.cs`
Any field added to `Group`/`GroupEntity` must be manually mirrored into `GroupWithMemberCount` and both of its producing LINQ projections (`GetAllWithMemberCountAsync`, `GetGroupsForUserAsync`) — this is a hand-maintained sibling model, not auto-derived, and `Index.cshtml` binds to it exclusively (not to `Group`).

### Field-level "lock after creation" via omission
**Source:** `QuestBoard.Service/Areas/Platform/Controllers/GroupController.cs:56-77` (`Edit` POST action, current: `group.Name = model.Name;` is the only assignment)
**Apply to:** `GroupController.Edit` POST — do not add `model.BoardType` to any assignment
No `[BindNever]`, no allowlist `[Bind(include:)]`, no runtime guard clause exists anywhere in this codebase for "protect this field from being overwritten." The sole existing mechanism is: the controller action body simply never reads the field from the posted model. Follow this exactly — do not introduce a new abstraction.

### Manual if/else-if badge rendering (no shared badge helper)
**Source:** `Areas/Platform/Views/Group/Members.cshtml` (GroupRole badges, lines 55-72)
**Apply to:** `Index.cshtml` + `Index.Mobile.cshtml` BoardType badge column
No shared Razor partial/tag-helper/`IHtmlHelper` extension for badges exists anywhere in the app. Every enum badge is a hand-rolled `@if`/`else if`/`else` chain co-located in the `.cshtml` file that needs it. Do not introduce a shared helper "for cleanliness" — it would be scope creep and inconsistent with four other enum badges already in the codebase.

### Enum dropdown with blank placeholder option
**Source:** `Areas/Platform/Views/Group/Members.cshtml` lines 104-111 (`AddMember.UserId`, manual blank-option `<option>` loop) combined with lines 115-119 (`AddMember.Role`, `Html.GetEnumSelectList<TEnum>()` + `asp-items`)
**Apply to:** `Create.cshtml` + `Create.Mobile.cshtml` `BoardType` dropdown
Standard `asp-items="Html.GetEnumSelectList<TEnum>()"` renders every enum value but no blank option — combine with a manual `@foreach` over `Html.GetEnumSelectList<BoardType>()` prefixed by a hardcoded `<option value="">-- Select Board Type --</option>` to satisfy D-03 (no pre-selected default).

## No Analog Found

None — every file in this phase's scope has a close, verified in-repo analog. This phase is a pure "extend four existing conventions" phase with no novel abstractions required.

## Metadata

**Analog search scope:** `QuestBoard.Domain/Enums/`, `QuestBoard.Domain/Models/`, `QuestBoard.Repository/Entities/`, `QuestBoard.Repository/Automapper/`, `QuestBoard.Repository/Migrations/`, `QuestBoard.Repository/GroupRepository.cs`, `QuestBoard.Service/Areas/Platform/Controllers/`, `QuestBoard.Service/Areas/Platform/Views/Group/`, `QuestBoard.Service/ViewModels/PlatformViewModels/`, `QuestBoard.IntegrationTests/Controllers/`
**Files scanned:** 15 (all read in full — no file in scope exceeds ~165 lines, well under the 2,000-line large-file threshold)
**Pattern extraction date:** 2026-07-03
