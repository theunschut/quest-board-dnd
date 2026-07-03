# Phase 35: Board Type Configuration - Research

**Researched:** 2026-07-03
**Domain:** ASP.NET Core 10 MVC — EF Core migration + AutoMapper enum mapping + Razor view convention replication (no new frameworks/libraries)
**Confidence:** HIGH

## Summary

This phase adds one net-new `int`-backed enum column (`BoardType`) to the existing `Groups` table and threads it through all four layers (Entity → Domain → ViewModel → View) using conventions the codebase already established for `GroupRole` and `SignupRole`. There is no framework research needed — this is a "replicate the existing pattern" phase, not a "discover the standard stack" phase. Every code location cited in `35-CONTEXT.md` was re-verified against the current working tree and matches exactly (file paths, line numbers, and code content are all still accurate — nothing has drifted).

Two nuances not fully captured in CONTEXT.md were found during verification and are the primary value-add of this research: (1) the current `Group ↔ GroupEntity` AutoMapper mapping is a bare `CreateMap<GroupEntity, Group>().ReverseMap()` with zero `ForMember` calls — adding an int↔enum field means this mapping can no longer be a bare `ReverseMap()` and must gain explicit `ForMember` calls on both directions, mirroring the adjacent `UserGroupEntity ↔ UserGroup` mapping already in the same file; and (2) `Areas/Platform/Views/Group/Index.cshtml` binds to `GroupWithMemberCount` (a projection model), not `Group` — so `BoardType` must also be added to `GroupWithMemberCount`, `IGroupRepository.GetAllWithMemberCountAsync`/`GetGroupsForUserAsync`'s EF projection `.Select(...)`, and `GroupRepository`'s two LINQ projections, or the badge column on Index will have nothing to bind to. `GroupPickerViewModel`/`GroupPickerController` also consume `GroupWithMemberCount` but only read `Id`/`Name` — adding `BoardType` there is additive and does not require any changes to that controller in this phase.

**Primary recommendation:** Add `BoardType` as `int`-backed on `GroupEntity`/`Group`/`GroupWithMemberCount` (enum defined in `QuestBoard.Domain/Enums/BoardType.cs` with `OneShot = 0, Campaign = 1`), migrate with `AddColumn<int>(..., defaultValue: 0)` mirroring `20260129155948_AddSignupRoleToPlayerSignup.cs` exactly, add explicit `ForMember` int↔enum conversions to `EntityProfile.cs`'s `Group` mapping (breaking its current bare `ReverseMap()`), and do NOT put `BoardType` on `GroupEditViewModel` at all — omission from that ViewModel's bindable surface is what makes D-06's silent-tamper-rejection work implicitly, requiring no defensive code in the controller.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| BoardType selection UI (Create form) | Frontend Server (SSR — Razor View) | API/Backend (ViewModel validation) | Dropdown rendering is a Razor concern; `[Required]`-style validation (if any) lives on the ViewModel |
| BoardType persistence | Database / Storage | API/Backend (Domain Service) | New column on `Groups` table; `GroupService.AddAsync` is the only write path |
| BoardType read-only display (Edit view, Index badge) | Frontend Server (SSR — Razor View) | API/Backend (read projection) | Badge/disabled-select rendering is view-layer; the value must reach the view via `GroupWithMemberCount`/`GroupEditViewModel` |
| BoardType tamper protection (Edit POST) | API/Backend (Controller) | — | Enforced by ViewModel shape (field absence), not by explicit runtime validation — matches existing `Edit` POST pattern of only copying `model.Name` |
| BoardType default for existing groups | Database / Storage | — | `defaultValue: 0` in the migration's `AddColumn`, no backfill SQL needed since `OneShot` is ordinal 0 |

## Standard Stack

No new packages, libraries, or frameworks are introduced by this phase. It exclusively reuses:

| Component | Version (confirmed in repo) | Purpose |
|-----------|------|---------|
| ASP.NET Core MVC | .NET 10 (net10.0, per `.csproj` TargetFramework) | Controllers/Views |
| EF Core | 10.0.9 (per `Microsoft.EntityFrameworkCore.InMemory` package ref in test project; matches production EF Core version) | Migration + entity mapping |
| AutoMapper | Already referenced in `QuestBoard.Repository`/`QuestBoard.Service` (existing `Profile` classes) | Entity↔Domain, Domain↔ViewModel mapping |
| xunit.v3 | 3.2.2 | Integration/unit tests |
| FluentAssertions | 8.10.0 | Test assertions |

**Installation:** None required — no new package references needed for this phase.

## Package Legitimacy Audit

Not applicable — this phase installs no external packages.

## Architecture Patterns

### System Architecture Diagram

```
[SuperAdmin Browser]
        |
        v
  Create.cshtml (GET/POST)  ---- <select asp-for="BoardType" asp-items="Html.GetEnumSelectList<BoardType>()">
        |
        v
  GroupController.Create(GroupCreateViewModel model)   [Service tier]
        |
        v
  new Group { Name = model.Name, BoardType = model.BoardType }
        |
        v
  GroupService.AddAsync(Group)   [Domain tier]
        |
        v
  AutoMapper: Group -> GroupEntity   (ForMember: BoardType = (int)src.BoardType)
        |
        v
  GroupRepository -> EF Core -> Groups.BoardType column   [Repository/Database tier]

---

  Edit.cshtml (GET) <---- GroupEditViewModel { Id, Name, BoardType }  <---- disabled <select>, value-only, non-bindable on POST
        |
        v (POST)
  GroupController.Edit(GroupEditViewModel model)
        |
        v
  group.Name = model.Name;   // BoardType intentionally never read from model — silent no-op by omission
        |
        v
  GroupService.UpdateAsync(group)   // group.BoardType unchanged, loaded from DB

---

  Index.cshtml <---- GroupListViewModel.Groups : IList<GroupWithMemberCount>  <---- badge column reads item.BoardType
        ^
        |
  GroupRepository.GetAllWithMemberCountAsync()  -->  .Select(g => new GroupWithMemberCount { ..., BoardType = (BoardType)g.BoardType })
```

### Recommended Project Structure

No new folders. Files touched (all pre-existing):
```
QuestBoard.Domain/
├── Enums/BoardType.cs                          # NEW enum file
├── Models/Group.cs                             # + BoardType property
├── Models/GroupWithMemberCount.cs               # + BoardType property
QuestBoard.Repository/
├── Entities/GroupEntity.cs                      # + BoardType int property
├── Automapper/EntityProfile.cs                  # Group mapping: bare ReverseMap() -> explicit ForMember int<->enum
├── GroupRepository.cs                           # + BoardType in both GroupWithMemberCount projections
├── Migrations/{timestamp}_AddBoardTypeToGroup.cs # NEW migration
QuestBoard.Service/
├── Areas/Platform/Controllers/GroupController.cs # Create POST sets BoardType; Edit POST unchanged (no BoardType read)
├── ViewModels/PlatformViewModels/GroupCreateViewModel.cs # + BoardType property (bindable)
├── ViewModels/PlatformViewModels/GroupEditViewModel.cs   # + BoardType property, DISPLAY-ONLY (see Pitfall below)
├── Areas/Platform/Views/Group/Create.cshtml (+ .Mobile.cshtml)  # + dropdown + permanence warning
├── Areas/Platform/Views/Group/Edit.cshtml (+ .Mobile.cshtml)    # + disabled dropdown
├── Areas/Platform/Views/Group/Index.cshtml (+ .Mobile.cshtml)  # + badge column
```

### Pattern 1: Int-backed enum on entity, real enum on domain model (established convention)
**What:** Entities store enums as `int` columns; `Domain.Models` classes expose the real C# enum type; AutoMapper bridges the two directions with explicit casts.
**When to use:** Every enum-valued field in this codebase (`GroupRole`, `SignupRole`, `ItemRarity`, `ItemType`, `ItemStatus`, `TransactionType`, `CharacterStatus`, `CharacterRole`, `DndClass`, `VoteType`) follows this pattern without exception. `BoardType` must follow it too for consistency.
**Example (existing code, `UserGroupEntity ↔ UserGroup`, confirmed at `QuestBoard.Repository/Automapper/EntityProfile.cs:124-130`):**
```csharp
// Source: QuestBoard.Repository/Automapper/EntityProfile.cs (verified in repo, current state)
CreateMap<UserGroupEntity, UserGroup>()
    .ForMember(dest => dest.GroupRole, opt => opt.MapFrom(src => (GroupRole)src.GroupRole))
    .ForMember(dest => dest.User, opt => opt.MapFrom(src => src.User));

CreateMap<UserGroup, UserGroupEntity>()
    .ForMember(dest => dest.GroupRole, opt => opt.MapFrom(src => (int)src.GroupRole))
    .ForMember(dest => dest.User, opt => opt.Ignore());
```
**Apply this exact shape to `BoardType`** in the `Group ↔ GroupEntity` mapping (see Pitfall 1 below — this mapping currently has no `ForMember` calls at all).

### Pattern 2: Enum dropdown with `Html.GetEnumSelectList<TEnum>()`
**What:** Razor `<select>` bound to an enum property via `asp-items="Html.GetEnumSelectList<TEnum>()"`.
**When to use:** Any enum selection UI in this app — established for `GroupRole` in `Members.cshtml`.
**Example (verified in repo, `Areas/Platform/Views/Group/Members.cshtml:115-118`):**
```html
<!-- Source: QuestBoard.Service/Areas/Platform/Views/Group/Members.cshtml -->
<select asp-for="AddMember.Role" class="form-select" asp-items="Html.GetEnumSelectList<GroupRole>()">
</select>
```
For the Create form's "no pre-selected default" requirement (D-03), precedent for a blank placeholder option exists one field above it in the same file (`AddMember.UserId`):
```html
<select asp-for="AddMember.UserId" class="form-select">
    <option value="">-- Select a user --</option>
    ...
</select>
```
Combine both idioms for `BoardType`: use `asp-items="Html.GetEnumSelectList<BoardType>()"` but prepend a blank `<option value="">-- Select Board Type --</option>` so the dropdown has no valid default (D-03). Because `BoardType` is a non-nullable enum on `GroupCreateViewModel`, model binding will bind an empty submission to the enum's default (`0` = `OneShot`) unless the ViewModel property is made nullable (`BoardType?`) with a `[Required]` attribute to force explicit selection — see Pitfall 3.

### Pattern 3: Manual if/else-if badge rendering (no shared badge helper)
**What:** Badges for enum values are rendered with `@if`/`else if` chains directly in the `.cshtml`, not through a shared Razor helper/partial/tag helper.
**When to use:** Any place an enum value needs a colored badge — established for `GroupRole` in `Members.cshtml`.
**Example (verified in repo, `Members.cshtml`):**
```html
@if (member.GroupRole == GroupRole.Admin)
{
    <span class="badge bg-danger"><i class="fas fa-shield-alt me-1"></i>Admin</span>
}
else if (member.GroupRole == GroupRole.DungeonMaster)
{
    <span class="badge bg-warning text-dark"><i class="fas fa-crown me-1"></i>DungeonMaster</span>
}
else
{
    <span class="badge bg-primary"><i class="fas fa-dice-d20 me-1"></i>Player</span>
}
```
Apply the same shape for `BoardType` (two values only — `OneShot`/`Campaign` — so a single `if`/`else` suffices, no `else if` chain needed).

### Pattern 4: EF migration for adding an int-backed enum column with default value
**What:** `migrationBuilder.AddColumn<int>(name, table, type: "int", nullable: false, defaultValue: N)`.
**When to use:** Adding any new int-backed enum column to an existing table with existing rows.
**Example (verified in repo, `20260129155948_AddSignupRoleToPlayerSignup.cs`, full file content):**
```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    migrationBuilder.AddColumn<int>(
        name: "SignupRole",
        table: "PlayerSignups",
        type: "int",
        nullable: false,
        defaultValue: 0);
}

protected override void Down(MigrationBuilder migrationBuilder)
{
    migrationBuilder.DropColumn(
        name: "SignupRole",
        table: "PlayerSignups");
}
```
This is a direct template for the new migration — no data backfill SQL is needed (unlike `20260630055221_AddGroupSchema.cs`, which needed a `Sql()` UPDATE because its default value of `0` was a placeholder, not a real business value). Here, `defaultValue: 0` IS the correct business default (`OneShot`), so `Up()`/`Down()` are a straight column add/drop — no `Sql()` calls required.

### Anti-Patterns to Avoid
- **Adding `BoardType` as a bindable property on `GroupEditViewModel` that the controller reads from `model`:** This would require an explicit "ignore this field" guard in the controller, which is more fragile than simply never wiring the field into the POST assignment. Follow D-06 literally: `GroupEditViewModel` may have a `BoardType` property (needed to *display* the value on GET), but the `Edit` POST action body must never reference `model.BoardType` — the existing `group.Name = model.Name;` is the only assignment line, and it should stay the only assignment line.
- **Using a shared badge partial/tag helper "for cleanliness":** The codebase has no such abstraction anywhere (`GroupRole` badges are hand-rolled per view). Introducing one now is out of scope and inconsistent — replicate the copy-paste if/else pattern instead.
- **Bare `ReverseMap()` for the `Group` entity mapping after adding `BoardType`:** `CreateMap<GroupEntity, Group>().ReverseMap()` will compile and appear to work by default (AutoMapper maps `int BoardType` on entity to `BoardType BoardType` on domain model via its built-in enum-underlying-type convention if property names match) — but this contradicts the codebase's own established convention of explicit `ForMember` int↔enum casts used everywhere else (`UserGroupEntity`, `ShopItemEntity`, `PlayerSignupEntity`, `CharacterEntity`, `CharacterClassEntity`, `UserTransactionEntity`, `PlayerDateVoteEntity`). For consistency and to avoid relying on AutoMapper's implicit enum-conversion convention (which differs subtly from explicit casts if the enum's underlying values ever diverge from ordinal order), add explicit `ForMember` calls even though a bare `ReverseMap()` would technically function. **Note:** this is a style/consistency recommendation, not a strict technical requirement — flag to planner as MEDIUM confidence since AutoMapper's own docs confirm implicit int↔enum mapping works when the underlying type matches.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Enum dropdown rendering | Custom `<option>` loop | `Html.GetEnumSelectList<TEnum>()` | Already the established convention; handles display names automatically |
| Int↔enum entity mapping | Manual mapping code in controller/service | AutoMapper `ForMember` | Consistent with every other enum in the codebase; keeps mapping logic in one place (`EntityProfile.cs`) |
| Field-level "lock after creation" protection | Custom attribute, FluentValidation rule, or runtime guard clause | Simply omit the field from the POST handler's entity-copy logic | This is literally how the existing `Edit` action already protects every field except `Name` — no new abstraction needed |

**Key insight:** This phase is 100% "extend existing patterns," not "solve a new problem." Any solution introducing a new abstraction (validation attribute, badge helper, mapping utility) is scope creep relative to what four other enums already do in this exact codebase.

## Runtime State Inventory

Not applicable — this is a greenfield additive phase (new column, new enum, no rename/refactor/migration of existing meaning). Skipping this section per format instructions.

## Common Pitfalls

### Pitfall 1: Group entity mapping currently has zero `ForMember` calls — don't assume the existing `ReverseMap()` "just works" without verification
**What goes wrong:** Planner/implementer adds `BoardType` to `GroupEntity` and `Group`, assumes the existing `CreateMap<GroupEntity, Group>().ReverseMap()` handles it (AutoMapper conventionally can map `int` <-> enum by matching property name and underlying type), and skips updating `EntityProfile.cs` entirely.
**Why it happens:** Every other enum mapping in the file has explicit `ForMember` casts, creating an expectation that "this is required," when technically for AutoMapper's default conventions, a same-named `int` source property maps to an enum destination property automatically in many AutoMapper versions — but this is inconsistent with this codebase's explicit style and could behave unexpectedly if `IMapper` configuration validation (`AssertConfigurationIsValid()`) is run in tests.
**How to avoid:** Check whether any test calls `mapper.ConfigurationProvider.AssertConfigurationIsValid()` (search found none directly, but confirm during planning) — regardless, add explicit `ForMember` calls to match the established style for consistency and readability, even if AutoMapper would technically resolve it via convention.
**Warning signs:** AutoMapper throws `AutoMapperMappingException` at runtime only when the mapping is actually exercised (lazy validation) if a a convention mismatch exists — a missed `ForMember` may not surface until integration tests run `POST /Group/Create`.

### Pitfall 2: `Index.cshtml`'s badge column has no data source unless `GroupWithMemberCount` and its two producing queries are updated too
**What goes wrong:** Implementer adds `BoardType` to `GroupEntity`/`Group` only, adds the dropdown to Create/Edit views, but the Index badge column shows nothing (or fails to compile) because `Model.Groups` is `IList<GroupWithMemberCount>`, which doesn't have `BoardType` unless it's added there too, and `GroupRepository.GetAllWithMemberCountAsync()`/`GetGroupsForUserAsync()`'s `.Select(...)` projections don't populate it.
**Why it happens:** `GroupWithMemberCount` (`QuestBoard.Domain/Models/GroupWithMemberCount.cs`) is a separate, hand-maintained projection model from `Group` — it's easy to update `Group` and forget its sibling.
**How to avoid:** Add `BoardType` to `GroupWithMemberCount` and to both LINQ projections in `GroupRepository.cs` (`GetAllWithMemberCountAsync` and `GetGroupsForUserAsync`) in the same task/commit as the `GroupEntity`/`Group` change.
**Warning signs:** Compile error in `Index.cshtml` (`'GroupWithMemberCount' does not contain a definition for 'BoardType'`) — this is a strong, fail-fast signal, not a silent bug, so it's low-risk if the planner is aware, but should be called out explicitly as a task step so it isn't discovered mid-implementation.

### Pitfall 3: D-03's "no pre-selected default" requires `BoardType?` (nullable) on `GroupCreateViewModel`, not a plain non-nullable `BoardType`
**What goes wrong:** If `GroupCreateViewModel.BoardType` is declared as a plain (non-nullable) `BoardType` enum, an empty/unselected `<select>` submission model-binds to the CLR default (`0` = `OneShot`) rather than failing validation — silently defeating D-03's "forces the SuperAdmin to actively choose" requirement, because the form will validate successfully even if the SuperAdmin never touched the dropdown.
**Why it happens:** ASP.NET Core model binding for enums treats an empty submitted value as "use default(TEnum)" unless the property is nullable and marked `[Required]`.
**How to avoid:** Declare `public BoardType? BoardType { get; set; }` on `GroupCreateViewModel` with a `[Required(ErrorMessage = "...")]` attribute, so an unselected dropdown produces a validation error instead of silently defaulting to `OneShot`. In the `Create` POST action, `model.BoardType` will be a non-null `BoardType` value by the time `ModelState.IsValid` is true, so `new Group { Name = model.Name, BoardType = model.BoardType!.Value }` (or `.GetValueOrDefault()`) is safe post-validation.
**Warning signs:** Manual test: submit the Create form without touching the Board Type dropdown — if the group is created as "One-Shot" without a validation error, this pitfall has occurred.

### Pitfall 4: EF Core migration timestamp must sort after `20260702081517_AddQuestFinalizedDateIndex` (the current latest migration)
**What goes wrong:** Running `dotnet ef migrations add` from a stale working directory or with system clock skew could generate a migration with an earlier timestamp than the existing latest, causing `dotnet ef database update` (or the app's auto-`Migrate()` on startup) to apply migrations out of order or fail model-snapshot comparison.
**Why it happens:** EF Core migration names are `yyyyMMddHHmmss_Name` and rely on the local system clock at generation time; today's date is 2026-07-03, and the most recent existing migration is dated 2026-07-02, so this is a low-risk pitfall in practice, but worth a sanity check.
**How to avoid:** Run `dotnet ef migrations add AddBoardTypeToGroup --project ../QuestBoard.Repository` from `QuestBoard.Service/` (per CLAUDE.md's documented command) immediately before implementation, and verify the generated file's timestamp prefix is numerically greater than `20260702081517`.
**Warning signs:** `dotnet ef migrations list` shows the new migration out of chronological order, or the `QuestBoardContextModelSnapshot.cs` diff looks unexpectedly large (a sign the tool picked up unrelated pending model changes).

### Pitfall 5: `GroupPickerViewModel`/`GroupPickerController` also consume `GroupWithMemberCount` — verify no unintended behavior change
**What goes wrong:** Adding `BoardType` to `GroupWithMemberCount` is additive and safe for `GroupPickerController`, since that controller/view only reads `.Id`/`.Name`/`.MemberCount` — but it's worth an explicit verification pass (or a quick integration test run) to confirm the `/groups/pick` flow still renders correctly after the projection model gains a field, since this phase's success criteria don't mention the picker at all and no behavior change there is intended.
**Why it happens:** Any shared projection model touched by multiple controllers carries a small risk of an unnoticed downstream effect, even when the change is purely additive.
**How to avoid:** After implementation, run the existing `GroupPickerControllerIntegrationTests.cs` suite to confirm no regression (should pass unmodified — no new test needed there, since this phase's scope explicitly excludes the picker).
**Warning signs:** None expected — flagging only for completeness per "check all downstream consumers" verification discipline.

## Code Examples

### New enum definition (follow `GroupRole.cs`'s exact shape)
```csharp
// Source: pattern from QuestBoard.Domain/Enums/GroupRole.cs (verified in repo)
namespace QuestBoard.Domain.Enums;

public enum BoardType
{
    OneShot = 0,
    Campaign = 1
}
```

### GroupCreateViewModel (nullable enum + Required, per Pitfall 3)
```csharp
using System.ComponentModel.DataAnnotations;
using QuestBoard.Domain.Enums;

namespace QuestBoard.Service.ViewModels.PlatformViewModels;

public class GroupCreateViewModel
{
    [Required(ErrorMessage = "Group name is required.")]
    [StringLength(100, ErrorMessage = "Group name cannot exceed 100 characters.")]
    [Display(Name = "Group Name")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Board type is required.")]
    [Display(Name = "Board Type")]
    public BoardType? BoardType { get; set; }
}
```

### GroupEditViewModel (display-only — property exists for GET rendering, never read on POST)
```csharp
using System.ComponentModel.DataAnnotations;
using QuestBoard.Domain.Enums;

namespace QuestBoard.Service.ViewModels.PlatformViewModels;

public class GroupEditViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Group name is required.")]
    [StringLength(100, ErrorMessage = "Group name cannot exceed 100 characters.")]
    [Display(Name = "Group Name")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "Board Type")]
    public BoardType BoardType { get; set; }
}
```
Controller's `Edit` GET populates `BoardType = group.BoardType` for display; the `Edit` POST action body must NOT add a line reading `model.BoardType` — leave `group.Name = model.Name;` as the only assignment, per D-06.

### Migration (mirrors AddSignupRoleToPlayerSignup exactly)
```csharp
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuestBoard.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddBoardTypeToGroup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BoardType",
                table: "Groups",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BoardType",
                table: "Groups");
        }
    }
}
```

## State of the Art

No external state-of-the-art shifts apply — this phase uses only internal, already-established conventions. No section applicable.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | Bare `ReverseMap()` for `Group` should be replaced with explicit `ForMember` casts for style consistency, even though AutoMapper's default convention would likely resolve `int BoardType` <-> `BoardType BoardType` automatically | Anti-Patterns, Pitfall 1 | Low — if planner chooses to leave `ReverseMap()` as-is and it works via AutoMapper convention, only a minor style inconsistency results, not a functional bug. Recommend the explicit-`ForMember` approach be validated by writing/running one test that exercises `IMapper.Map<GroupEntity>(group)` round-trip before finalizing. |
| A2 | `BoardType?` (nullable) + `[Required]` on `GroupCreateViewModel` is necessary to satisfy D-03 ("no pre-selected default... forces the SuperAdmin to actively choose") | Pitfall 3, Code Examples | Medium — if a plain non-nullable `BoardType` is used instead, the form will silently accept an unselected dropdown as `OneShot`, which technically violates D-03's stated intent even though it wouldn't cause a visible error. This is a model-binding behavior claim based on ASP.NET Core training knowledge, not verified via Context7/official docs in this session. |

**If this table is empty:** N/A — two assumptions logged above, both low-to-medium risk and both independently testable during implementation (write a test that submits the Create form with no BoardType selected and assert a validation error, per A2).

## Open Questions

1. **Should `BoardType` have a `[Display(Name = "Board Type")]` label matching "Board Type" (two words) exactly, or some other casing/spacing?**
   - What we know: `GroupRole`'s field label in `Members.cshtml` is `"Role"` (via `asp-for` auto-label from the property name, not an explicit `[Display]`); no strict convention for multi-word enum field labels was found elsewhere.
   - What's unclear: Whether `asp-for="BoardType"` without an explicit `[Display]` attribute would render as "Board Type" (ASP.NET Core's default label generation inserts spaces before capitals in PascalCase names) or literally "BoardType".
   - Recommendation: Add an explicit `[Display(Name = "Board Type")]` attribute for certainty rather than relying on ASP.NET Core's PascalCase-splitting label behavior — this guarantees the exact label text regardless of any global `DisplayNameFor` customization elsewhere in the app (none was found, but explicit is safer than implicit for a UI-visible label).

## Environment Availability

Skipped — this phase has no external dependencies beyond the existing .NET 10 SDK / EF Core tooling already confirmed present and in use by the rest of the codebase (evidenced by successful prior migrations in the repo history).

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xunit.v3 (3.2.2) + FluentAssertions (8.10.0) + Microsoft.AspNetCore.Mvc.Testing (10.0.9) |
| Config file | `QuestBoard.IntegrationTests/QuestBoard.IntegrationTests.csproj` (also `QuestBoard.UnitTests/QuestBoard.UnitTests.csproj` for unit-level tests) |
| Quick run command | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~GroupManagementIntegrationTests"` |
| Full suite command | `dotnet test` (from repo root, runs all test projects) |

### Phase Requirements → Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| BOARD-01 | SuperAdmin can select Board Type on Create form; selected value persists to the group | integration | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~CreateGroup"` | ❌ Wave 0 — needs new test asserting BoardType round-trips through Create POST |
| BOARD-01 | Create form validation rejects submission with no Board Type selected (Pitfall 3 / D-03) | integration | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~CreateGroup_WithoutBoardType"` | ❌ Wave 0 — new test |
| BOARD-02 | Edit form displays Board Type read-only; POSTing a changed value has no effect on the stored entity | integration | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~EditGroup_BoardTypeTamper"` | ❌ Wave 0 — new test |
| BOARD-02 (success criterion 4) | Existing groups (e.g., seeded `EuphoriaInn`) default to `OneShot` after migration | integration | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~GroupsIndex_ShouldShowSeededGroup"` (extend existing test, or add new assertion) | Existing test file present; needs a new assertion or new `[Fact]` for BoardType default |

### Sampling Rate
- **Per task commit:** `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~GroupManagementIntegrationTests"`
- **Per wave merge:** `dotnet test` (full suite)
- **Phase gate:** Full suite green before `/gsd:verify-work`

### Wave 0 Gaps
- [ ] `QuestBoard.IntegrationTests/Controllers/GroupManagementIntegrationTests.cs` — extend with new `[Fact]`s covering BOARD-01 (dropdown persists selection, validation rejects empty selection) and BOARD-02 (Edit POST tamper is a no-op, existing seeded group defaults to OneShot)
- [ ] No new test project or framework install needed — `WebApplicationFactoryBase`/`AuthenticationHelper`/`TestDataHelper` fixtures already exist and are directly reusable
- [ ] Consider one unit test in `QuestBoard.UnitTests` for `GroupService.AddAsync` if any BoardType-specific business logic is added there (none is currently planned per CONTEXT.md — only `Name` validation exists in `GroupService.AddAsync` today)

## Security Domain

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | No | Unchanged — `[Authorize(Policy = "SuperAdminOnly")]` on `GroupController` already gates all actions this phase touches |
| V3 Session Management | No | No change to session handling |
| V4 Access Control | Yes (already satisfied) | `GroupController` already carries `[Area("Platform")] [Authorize(Policy = "SuperAdminOnly")]` at class level — no new authorization logic needed since Create/Edit actions already require SuperAdmin |
| V5 Input Validation | Yes | `[Required]` + nullable `BoardType?` on `GroupCreateViewModel` (Pitfall 3); `[ValidateAntiForgeryToken]` already present on both `Create` and `Edit` POST actions |
| V6 Cryptography | No | Not applicable to this phase |

### Known Threat Patterns for ASP.NET Core MVC (this phase's stack)

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Mass-assignment / over-posting (tampering with a hidden/disabled form field to change a value the server shouldn't accept) | Tampering | This is exactly D-06's scenario. Standard ASP.NET Core mitigation: do not include the field as a bindable property that the controller reads from the posted model for the write path — i.e., `GroupEditViewModel.BoardType` may exist for *display*, but the `Edit` POST handler must never assign `group.BoardType = model.BoardType;`. This is the codebase's existing convention (only `Name` is copied) — no `[BindNever]` attribute or `[Bind(include:)]` allowlist is used elsewhere in this codebase, so introducing one now would be inconsistent; simply not writing the assignment line is sufficient and matches precedent. |
| CSRF on Create/Edit POST | Tampering/Spoofing | Already mitigated — both actions already carry `[ValidateAntiForgeryToken]` and the forms already emit anti-forgery tokens (standard `<form asp-action="...">` tag helper behavior in this codebase, confirmed no `asp-antiforgery="false"` override present on Create/Edit forms) |

## Sources

### Primary (HIGH confidence — verified directly against working tree in this session)
- `C:\Repos\quest-board\QuestBoard.Repository\Entities\GroupEntity.cs` — confirmed no `BoardType` exists yet
- `C:\Repos\quest-board\QuestBoard.Domain\Models\Group.cs` — confirmed no `BoardType` exists yet
- `C:\Repos\quest-board\QuestBoard.Domain\Models\GroupWithMemberCount.cs` — confirmed projection model needs parallel update
- `C:\Repos\quest-board\QuestBoard.Repository\GroupRepository.cs` — confirmed two `.Select(...)` projections need `BoardType`
- `C:\Repos\quest-board\QuestBoard.Repository\Automapper\EntityProfile.cs` — confirmed `Group` mapping is bare `ReverseMap()` (lines 120-121), confirmed `UserGroupEntity↔UserGroup` explicit `ForMember` pattern (lines 124-130) to replicate
- `C:\Repos\quest-board\QuestBoard.Service\Areas\Platform\Controllers\GroupController.cs` — confirmed exact Create/Edit action bodies match CONTEXT.md's line citations
- `C:\Repos\quest-board\QuestBoard.Service\ViewModels\PlatformViewModels\GroupCreateViewModel.cs` / `GroupEditViewModel.cs` — confirmed current shape (Name-only)
- `C:\Repos\quest-board\QuestBoard.Service\Areas\Platform\Views\Group\Members.cshtml` — confirmed enum-dropdown convention (line ~117) and badge if/else convention
- `C:\Repos\quest-board\QuestBoard.Service\Areas\Platform\Views\Group\{Create,Edit,Index}.cshtml` (+ `.Mobile.cshtml`) — confirmed current structure, confirmed no `Details.cshtml` exists
- `C:\Repos\quest-board\QuestBoard.Repository\Migrations\20260129155948_AddSignupRoleToPlayerSignup.cs` — confirmed exact migration template
- `C:\Repos\quest-board\QuestBoard.Repository\Migrations\20260630055221_AddGroupSchema.cs` — confirmed original `Groups` table shape (Id/Name/CreatedAt only, no other pending FK complexity)
- `C:\Repos\quest-board\QuestBoard.Repository\Entities\QuestBoardContext.cs` — confirmed `GroupEntity` has a unique index on `Name` only, no other fluent config relevant to `BoardType`
- `C:\Repos\quest-board\QuestBoard.Domain\Enums\GroupRole.cs` — confirmed enum declaration style/shape to replicate
- `C:\Repos\quest-board\QuestBoard.IntegrationTests\Controllers\GroupManagementIntegrationTests.cs` — confirmed existing test conventions (xunit.v3, `TestContext.Current.CancellationToken`, `WebApplicationFactoryBase`, `TestDataHelper.ClearDatabaseAsync`)
- `C:\Repos\quest-board\QuestBoard.IntegrationTests\Helpers\TestDataHelper.cs` — confirmed `SeedDefaultGroupAsync` constructs `GroupEntity` without setting `BoardType` (relies on default(int)=0)
- `C:\Repos\quest-board\QuestBoard.IntegrationTests\QuestBoard.IntegrationTests.csproj` — confirmed test framework versions (xunit.v3 3.2.2, FluentAssertions 8.10.0, Mvc.Testing 10.0.9)
- `C:\Repos\quest-board\QuestBoard.Service\Controllers\GroupPickerController.cs` / `GroupPickerViewModel.cs` — confirmed additive-safe, no changes needed
- `C:\Repos\quest-board\.planning\PROJECT.md` — confirmed `BoardType` enum decision (`OneShot`/`Campaign` order, switch-expression dispatch convention for later phases)
- Full-codebase grep for `BoardType` across `*.cs` and `*.cshtml` — confirmed zero existing occurrences, no conflict

### Secondary (MEDIUM confidence)
- ASP.NET Core enum model-binding behavior for non-nullable enums defaulting to `default(TEnum)` on empty submission (Pitfall 3 / Assumption A2) — based on general ASP.NET Core MVC model-binding knowledge, not verified against official docs or Context7 in this session (no Context7 MCP tool was invoked; standard training-knowledge behavior for `DefaultModelBinder` enum handling).

### Tertiary (LOW confidence)
- None — no unverified WebSearch-only claims were used in this research; the phase is entirely internal-codebase-driven with no external ecosystem research required.

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — no new stack; 100% reuse of already-verified in-repo conventions
- Architecture: HIGH — every code location and pattern cited was re-verified against the current working tree in this session
- Pitfalls: HIGH for Pitfalls 1, 2, 4, 5 (all directly observed via code reads); MEDIUM for Pitfall 3 (based on general ASP.NET Core model-binding knowledge, not verified via official docs this session — flagged in Assumptions Log)

**Research date:** 2026-07-03
**Valid until:** Indefinite for the architectural/pattern findings (internal codebase conventions don't go stale on a calendar). Re-verify code line numbers if more than ~2 phases of unrelated `GroupController`/`GroupEntity`/`EntityProfile.cs` changes land before this phase is planned/executed.
