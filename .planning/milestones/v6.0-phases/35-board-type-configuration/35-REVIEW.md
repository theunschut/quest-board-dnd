---
phase: 35-board-type-configuration
reviewed: 2026-07-03T14:04:45Z
depth: standard
files_reviewed: 20
files_reviewed_list:
  - QuestBoard.Domain/Enums/BoardType.cs
  - QuestBoard.Domain/Models/Group.cs
  - QuestBoard.Domain/Models/GroupWithMemberCount.cs
  - QuestBoard.IntegrationTests/Controllers/GroupManagementIntegrationTests.cs
  - QuestBoard.Repository/Automapper/EntityProfile.cs
  - QuestBoard.Repository/Entities/GroupEntity.cs
  - QuestBoard.Repository/GroupRepository.cs
  - QuestBoard.Repository/Migrations/20260703113120_AddBoardTypeToGroup.Designer.cs
  - QuestBoard.Repository/Migrations/20260703113120_AddBoardTypeToGroup.cs
  - QuestBoard.Repository/Migrations/QuestBoardContextModelSnapshot.cs
  - QuestBoard.Service/Areas/Platform/Controllers/GroupController.cs
  - QuestBoard.Service/Areas/Platform/Views/Group/Create.Mobile.cshtml
  - QuestBoard.Service/Areas/Platform/Views/Group/Create.cshtml
  - QuestBoard.Service/Areas/Platform/Views/Group/Edit.Mobile.cshtml
  - QuestBoard.Service/Areas/Platform/Views/Group/Edit.cshtml
  - QuestBoard.Service/Areas/Platform/Views/Group/Index.Mobile.cshtml
  - QuestBoard.Service/Areas/Platform/Views/Group/Index.cshtml
  - QuestBoard.Service/ViewModels/PlatformViewModels/GroupCreateViewModel.cs
  - QuestBoard.Service/ViewModels/PlatformViewModels/GroupEditViewModel.cs
  - QuestBoard.Service/wwwroot/css/site.css
findings:
  critical: 0
  warning: 2
  info: 3
  total: 5
status: issues_found
---

# Phase 35: Code Review Report

**Reviewed:** 2026-07-03T14:04:45Z
**Depth:** standard
**Files Reviewed:** 20
**Status:** issues_found

## Summary

This phase adds a `BoardType` (OneShot/Campaign) enum to `Group`, set once at creation and intended to be immutable afterward. I traced the full write path for both Create and Edit to specifically verify the mass-assignment mitigation the phase brief called out.

**The mitigation holds.** `GroupEditViewModel.BoardType` has no `[BindNever]` and model binding will happily populate it from a posted `BoardType` field (confirmed by the `EditGroup_PostingChangedBoardType_ShouldBeSilentlyIgnored` test, which posts a raw `BoardType` field through a antiforgery-stubbed test client). However, `GroupController.Edit(GroupEditViewModel model)` never reads `model.BoardType`: it loads the existing `Group` domain object via `groupService.GetByIdAsync(model.Id)` (which carries the DB's current `BoardType`), overwrites only `group.Name = model.Name`, and passes that same `group` object to `UpdateAsync`. `BaseRepository.UpdateAsync` then does `Mapper.Map(model, entity)` where `model` is the domain `Group` (not the view model) — so the `BoardType` written back to the tracked `GroupEntity` is always the value that was already in the database, never anything derived from the POST body. This is correct and matches the intended design (field-by-field assignment on a DB-loaded domain object, not blanket re-binding of the posted view model onto the entity).

That said, the mitigation is **entirely convention-based** with no compiler or model-binding enforcement — see WR-01. I also found a real, unvalidated-input gap on the Create path where an out-of-range `BoardType` integer can be persisted as a meaningless value — see WR-02. Info-level items cover minor duplication and inconsistent UX messaging.

## Warnings

### WR-01: BoardType immutability protection relies entirely on controller discipline, not binding

**File:** `QuestBoard.Service/ViewModels/PlatformViewModels/GroupEditViewModel.cs:16`
**Issue:** `GroupEditViewModel.BoardType` is bindable from the POST body (no `[BindNever]`/`[ValidateNever]`, and the property is not `init`-only or otherwise binder-proof). The immutability guarantee for `BoardType` on Edit exists solely because `GroupController.Edit(GroupEditViewModel model)` (POST) happens to only ever copy `model.Name` onto the DB-loaded `Group` before calling `UpdateAsync`. Nothing prevents a future edit to that action — e.g. someone "cleaning up" the handler to `groupService.UpdateAsync(mapper.Map<Group>(model))` or adding `group.BoardType = model.BoardType` alongside the `Name` assignment for a "let's also let admins fix typos in BoardType" feature — from silently reintroducing the mass-assignment hole this phase was built to avoid. There is no compile-time or binding-time signal that `BoardType` must never flow from `model` into the persisted entity on this action.
**Fix:** Add `[BindNever]` (or `[ModelBinder(BinderType = typeof(Microsoft.AspNetCore.Mvc.ModelBinding.Binders.??))]`/remove the setter and use a constructor) to `GroupEditViewModel.BoardType` so the view model can still carry the value for display (as data, assigned by the controller in the GET action) without ever being *populated by* the model binder on POST:
```csharp
[Display(Name = "Board Type")]
[BindNever]
public BoardType BoardType { get; set; }
```
This makes the immutability guarantee independent of how the POST handler is refactored in the future, and is consistent with `[Bind(Prefix = "AddMember")]` already showing this codebase is comfortable using binding attributes to constrain what a POST can influence.

### WR-02: Create accepts undefined BoardType enum values with no validation

**File:** `QuestBoard.Service/ViewModels/PlatformViewModels/GroupCreateViewModel.cs:13-15`
**Issue:** `BoardType` only carries `[Required]`. Nothing constrains the posted integer to a defined `BoardType` member (`0` or `1`). A POST of `BoardType=99` passes `ModelState.IsValid` (the value is present and parses as an `int`), flows through `model.BoardType!.Value` in `GroupController.Create`, gets cast `(int)src.BoardType` by AutoMapper, and is persisted verbatim into the `Groups.BoardType` int column — there is no CHECK constraint at the DB layer either (see `AddBoardTypeToGroup.cs:13-18`, plain `int` column). Once persisted, `GroupRepository`'s projections cast it back with `(BoardType)g.BoardType`, which never throws for undefined values in C#; the Index views' `item.BoardType == BoardType.Campaign` check then silently falls into the `else` branch and renders it as "One-Shot," masking the corruption instead of surfacing it.
**Fix:** Add `[EnumDataType(typeof(BoardType))]` to reject undefined values at the model-validation layer:
```csharp
[Required(ErrorMessage = "Board type is required.")]
[EnumDataType(typeof(BoardType), ErrorMessage = "Select a valid board type.")]
[Display(Name = "Board Type")]
public BoardType? BoardType { get; set; }
```
Consider also a DB-level CHECK constraint (`BoardType IN (0, 1)`) via a follow-up migration for defense in depth, since the enum is a closed, rarely-changing set.

## Info

### IN-01: Duplicated CSS comment/rule for disabled-field styling

**File:** `QuestBoard.Service/wwwroot/css/site.css:480-486, 1148-1156`
**Issue:** The same comment (`/* Disabled fields must read as non-interactive, not just be non-interactive */`) and near-identical rule sets for `:disabled` form controls are defined twice — once globally and once scoped to `.modern-card`. This likely happened because the two rule blocks were added to satisfy different visual contexts (global forms vs. modern-card forms), but the duplication makes future updates (e.g. changing the disabled color) easy to apply in only one place and forget the other.
**Fix:** Consolidate into a single rule set, or at minimum cross-reference the two blocks with a comment noting the other location, e.g. `/* See also .modern-card override further below */`.

### IN-02: Edit view gives no explanation for why Board Type is disabled

**File:** `QuestBoard.Service/Areas/Platform/Views/Group/Edit.cshtml:27-30`, `QuestBoard.Service/Areas/Platform/Views/Group/Edit.Mobile.cshtml:28-31`
**Issue:** The Create views include helper text ("This cannot be changed after creation.") next to the Board Type selector, but the Edit views render the same disabled selector with no explanatory text at all. An admin looking at the Edit form has no in-context indication of *why* the field is grayed out, which is a minor discoverability gap given the Create view clearly anticipated needing to explain this.
**Fix:** Add matching helper text under the disabled select in both Edit views:
```html
<small class="form-text text-muted">Board type cannot be changed after creation.</small>
```

### IN-03: Disabled `<select>` has no fallback if JS/CSS strips the `disabled` attribute client-side

**File:** `QuestBoard.Service/Areas/Platform/Views/Group/Edit.cshtml:29`, `QuestBoard.Service/Areas/Platform/Views/Group/Edit.Mobile.cshtml:30`
**Issue:** The only client-side protection against changing `BoardType` on Edit is the HTML `disabled` attribute on the `<select>`. This is not a security boundary (browsers omit `disabled` fields from form submission, so this is fine as-is and WR-01/server-side field omission is the real guard), but it's worth noting for future maintainers that the `disabled` attribute serves purely a UX purpose here — the actual enforcement is server-side (WR-01 above). No fix required; documenting so a future contributor doesn't mistake the `disabled` attribute itself for the security boundary and, e.g., "fix" a UX complaint by switching to `readonly` plus enabling the field for some other reason without realizing the server-side guard is what actually matters.
**Fix:** Optional: add a short code comment above the disabled select noting that immutability is enforced server-side in `GroupController.Edit`, not by this attribute, to reduce the risk of a future misguided "fix."

---

_Reviewed: 2026-07-03T14:04:45Z_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
