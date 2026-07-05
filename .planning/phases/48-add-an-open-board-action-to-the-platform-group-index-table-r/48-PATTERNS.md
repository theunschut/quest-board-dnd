# Phase 48: Add an Open Board action to the /platform group index table - Pattern Map

**Mapped:** 2026-07-04
**Files analyzed:** 2 (both existing views to modify; no new files)
**Analogs found:** 2 / 2

This is a small, purely presentational change. No new controller, service, or entity code is introduced — `GroupPickerController.SelectGroup` is reused verbatim (D-01). The only work is inserting a per-row `<form>` button into two existing Razor views.

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|-----------------|----------------|
| `QuestBoard.Service/Areas/Platform/Views/Group/Index.cshtml` | component (Razor view, desktop table) | request-response (form POST) | `QuestBoard.Service/Views/GroupPicker/Index.cshtml` | exact (per-item POST form pattern) |
| `QuestBoard.Service/Areas/Platform/Views/Group/Index.Mobile.cshtml` | component (Razor view, mobile card list) | request-response (form POST) | `QuestBoard.Service/Views/GroupPicker/Index.cshtml` (form pattern) + `Index.cshtml` (desktop, for sibling button styling) | exact |

No controller or service files are created or modified. `GroupPickerController.SelectGroup` (lines 41-51) is the reused target — read-only reference, not touched.

## Pattern Assignments

### `QuestBoard.Service/Areas/Platform/Views/Group/Index.cshtml` (component, request-response)

**Analog A — form-submit pattern:** `QuestBoard.Service/Views/GroupPicker/Index.cshtml` (lines 40-52)

**Analog B — sibling action-button styling in the same table:** same file, Actions column (lines 52-65)

**Form pattern to copy** (from GroupPicker/Index.cshtml, lines 40-43):
```cshtml
<form asp-action="SelectGroup" method="post">
    @Html.AntiForgeryToken()
    <input type="hidden" name="groupId" value="@group.Id" />
    <input type="hidden" name="returnUrl" value="@Model.ReturnUrl" />
```

Important adaptation per D-01/integration-points in CONTEXT.md: `GroupPickerController` is in the default area, not `Platform`, so the form tag helper must explicitly avoid inheriting the ambient `Platform` area. Use:
```cshtml
<form asp-controller="GroupPicker" asp-action="SelectGroup" asp-area="" method="post" class="d-inline">
    @Html.AntiForgeryToken()
    <input type="hidden" name="groupId" value="@item.Id" />
    <button type="submit" class="btn btn-sm btn-primary me-2">
        <i class="fas fa-door-open me-1"></i>Open Board
    </button>
</form>
```
(No `returnUrl` input needed — leaving it unset falls through to `SelectGroup`'s default `RedirectToAction("Index", "Quest")`, per D-01.)

**Sibling button pattern to match** (Index.cshtml, lines 53-58 — existing `Members`/`Edit` anchors, for visual/markup consistency of classes and icon spacing):
```cshtml
<a asp-controller="Group" asp-action="Members" asp-area="Platform" asp-route-id="@item.Id" class="btn btn-sm btn-info me-2">
    <i class="fas fa-users me-1"></i>Members
</a>
<a asp-controller="Group" asp-action="Edit" asp-area="Platform" asp-route-id="@item.Id" class="btn btn-sm btn-warning me-2">
    <i class="fas fa-edit me-1"></i>Edit
</a>
```

**Insertion point:** immediately before the `Members` anchor at line 53, inside the same `<td class="text-end">` cell (line 52). The `Open Board` form/button goes first (leftmost), per D-01 phase boundary.

**Conditional-gating pattern (NOT applied to Open Board):** the existing `Delete` button (lines 59-64) is gated with `@if (item.MemberCount == 0)`. Per CONTEXT.md discretion note, `Open Board` should NOT copy this gate — always show it regardless of member count.

---

### `QuestBoard.Service/Areas/Platform/Views/Group/Index.Mobile.cshtml` (component, request-response)

**Analog:** same two sources as desktop — `GroupPicker/Index.cshtml` for the form, and the desktop `Index.cshtml`/this file's own sibling buttons (lines 45-56) for mobile button styling.

**Sibling button pattern to match** (Index.Mobile.cshtml, lines 45-50):
```cshtml
<a asp-controller="Group" asp-action="Members" asp-area="Platform" asp-route-id="@item.Id" class="btn btn-sm btn-info">
    <i class="fas fa-users me-1"></i>Members
</a>
<a asp-controller="Group" asp-action="Edit" asp-area="Platform" asp-route-id="@item.Id" class="btn btn-sm btn-warning">
    <i class="fas fa-edit me-1"></i>Edit
</a>
```
Note: mobile buttons omit the `me-2` (spacing handled by the parent's `d-flex gap-2` at line 44), unlike the desktop table cell which uses `me-2` per button. Match this convention — use no trailing margin class on the mobile button, rely on the existing `gap-2` wrapper.

**Form to insert** (same structure as desktop, mobile-styled):
```cshtml
<form asp-controller="GroupPicker" asp-action="SelectGroup" asp-area="" method="post" class="d-inline">
    @Html.AntiForgeryToken()
    <input type="hidden" name="groupId" value="@item.Id" />
    <button type="submit" class="btn btn-sm btn-primary">
        <i class="fas fa-door-open me-1"></i>Open Board
    </button>
</form>
```

**Insertion point:** immediately before the `Members` anchor at line 45, inside the `<div class="d-flex flex-wrap gap-2 mt-2">` wrapper (line 44).

---

## Shared Patterns

### Reused backend action (no new code)
**Source:** `QuestBoard.Service/Controllers/GroupPickerController.cs`, lines 41-51
```csharp
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> SelectGroup(int groupId, string? returnUrl = null)
{
    var group = await groupService.GetByIdAsync(groupId);
    if (group == null) return NotFound();

    HttpContext.Session.SetInt32(SessionKeys.ActiveGroupId, group.Id);
    HttpContext.Session.SetString(SessionKeys.ActiveGroupName, group.Name);
    return RedirectToLocal(returnUrl);
}
```
**Apply to:** both views' new form — this is the exact POST target, unmodified. `RedirectToLocal` falls back to `RedirectToAction("Index", "Quest")` when `returnUrl` is null/non-local, which is the desired behavior here.

### Cross-area form routing
**Pattern:** when a Razor view rendered inside `Areas/Platform/Views/...` needs to POST to a controller in the default (non-area) namespace, the `<form>` tag helper must set `asp-area=""` explicitly (ASP.NET Core area routing otherwise inherits the ambient `Platform` area from the current route values and 404s).
**Apply to:** both `Index.cshtml` and `Index.Mobile.cshtml` new forms.

### Icon/button convention (CLAUDE.md UI/UX guidelines)
**Apply to:** both new buttons — filled color button (`btn-primary`, not outline), FontAwesome icon with `me-1`/`me-2` spacing matching sibling buttons in the same view, per D-recommendation `fa-door-open`.

## No Analog Found

None — both target files have a direct structural analog (`GroupPicker/Index.cshtml` for the form; the file's own sibling buttons for styling).

## Metadata

**Analog search scope:** `QuestBoard.Service/Views/GroupPicker/`, `QuestBoard.Service/Areas/Platform/Views/Group/`, `QuestBoard.Service/Controllers/GroupPickerController.cs`
**Files scanned:** 4 (all read in full; all ≤ 100 lines, single-pass reads, no re-reads needed)
**Pattern extraction date:** 2026-07-04
