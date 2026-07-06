# Phase 53: Add dedicated Edit view for Quest recap so Details page is view-only - Pattern Map

**Mapped:** 2026-07-06
**Files analyzed:** 6 (2 new views, 2 modified views, 1 modified controller, 1 new ViewModel)
**Analogs found:** 6 / 6

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|--------------------|------|-----------|-----------------|----------------|
| `QuestBoard.Service/Controllers/QuestBoard/QuestLogController.cs` (add `EditRecap` GET+POST) | controller | request-response (CRUD update) | `QuestController.cs` `Edit` GET+POST (lines 138-266) | exact — same controller family, same ownership-check shape, same "single string field update" flow as `UpdateRecap` already on this exact file |
| `QuestBoard.Service/Views/QuestLog/EditRecap.cshtml` (new) | component (Razor view) | request-response (form render) | `QuestBoard.Service/Views/Quest/Edit.cshtml` | exact — UI-SPEC.md gives verbatim markup mirroring this file's modern-card structure |
| `QuestBoard.Service/Views/QuestLog/EditRecap.Mobile.cshtml` (new) | component (Razor view) | request-response (form render) | `QuestBoard.Service/Views/Quest/Edit.Mobile.cshtml` | exact — UI-SPEC.md gives verbatim markup mirroring this file's `.quest-edit-card-mobile` structure |
| `QuestBoard.Service/Views/QuestLog/Details.cshtml` (modify Session Recap block) | component (Razor view) | request-response (read-only render) | itself, prior version (lines 97-136) | exact — in-place restructuring, not a cross-file analog |
| `QuestBoard.Service/Views/QuestLog/Details.Mobile.cshtml` (modify Session Recap block) | component (Razor view) | request-response (read-only render) | itself, prior version (lines 91-124) | exact — in-place restructuring |
| `QuestBoard.Service/ViewModels/QuestLogViewModels/EditRecapViewModel.cs` (new) | model (ViewModel) | CRUD (data-transfer for edit form) | `QuestBoard.Service/ViewModels/QuestViewModels/EditQuestViewModel.cs` | role-match — same "Id + editable field(s)" shape, flattened per UI-SPEC.md guidance (no nested nav-property graph needed) |

## Pattern Assignments

### `QuestBoard.Service/Controllers/QuestBoard/QuestLogController.cs` (controller, request-response)

**Analog:** `QuestBoard.Service/Controllers/QuestBoard/QuestController.cs` lines 138-266 (`Edit` GET+POST), combined with this same file's existing `UpdateRecap` POST (lines 77-117) which the new POST action must match exactly in service-call shape.

**Imports already present in QuestLogController.cs** (lines 1-6, no new imports needed beyond the ViewModel):
```csharp
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Extensions;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Service.ViewModels.QuestLogViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
```

**Auth pattern — two-layer check, copy verbatim shape from existing `UpdateRecap`** (`QuestLogController.cs` lines 77-113):
```csharp
[HttpPost]
[ValidateAntiForgeryToken]
[Authorize(Policy = "DungeonMasterOnly")]
public async Task<IActionResult> UpdateRecap(int id, string recap, CancellationToken token = default)
{
    var quest = await questService.GetQuestWithDetailsAsync(id, token);

    if (quest == null)
    {
        return NotFound();
    }

    // Verify this is a completed quest (DM-only sessions are not shown in the quest log),
    // admitting closed campaign quests even though they never set FinalizedDate.
    var isCompletedOneShot = quest.IsFinalized && quest.FinalizedDate.HasValue
        && quest.FinalizedDate.Value.Date <= DateTime.UtcNow.AddDays(-1).Date
        && !quest.DungeonMasterSession;
    if (!isCompletedOneShot && !quest.IsClosed)
    {
        return BadRequest("Cannot update recap for a quest that is not completed.");
    }

    var currentUser = await userService.GetUserAsync(User);
    if (currentUser == null)
    {
        return Challenge();
    }

    // Check if current user is the quest's DM or an admin
    var isQuestDm = currentUser.Id == quest.DungeonMaster?.Id;
    var isAdmin = await GetEffectiveRoleAsync() == GroupRole.Admin;

    if (!isQuestDm && !isAdmin)
    {
        return Forbid();
    }

    await questService.UpdateQuestRecapAsync(id, recap, token);

    return RedirectToAction("Details", new { id });
}
```
Per CONTEXT.md D-04, the new `EditRecap` **GET** action must replicate this exact `Forbid()` behavior (not the project's usual 404-for-cross-tenant convention) — copy the `isQuestDm || isAdmin` check and completed-quest guard into the GET action too (per CONTEXT.md's "Claude's Discretion" note recommending reuse of the `isCompletedOneShot`/`IsClosed` guard).

**GET-action shape to follow** (structurally, from `QuestController.cs` `Edit` GET, lines 138-185 — note this uses `IsQuestOwner` helper + Forbid, the closest GET-with-Forbid precedent in the codebase since `QuestLogController`'s own `Details` GET only *sets a ViewBag flag* rather than forbidding):
```csharp
[HttpGet]
[Authorize(Policy = "DungeonMasterOnly")]
public async Task<IActionResult> Edit(int id, CancellationToken token = default)
{
    var quest = await questService.GetQuestWithDetailsAsync(id, token);

    if (quest == null)
    {
        return NotFound();
    }

    var currentUser = await userService.GetUserAsync(User);
    if (currentUser == null)
    {
        return Challenge();
    }

    // Check if current user is the quest's DM
    var role = await GetEffectiveRoleAsync();
    if (!IsQuestOwner(currentUser, quest.DungeonMaster) && role != GroupRole.Admin)
    {
        return Forbid();
    }
    ...
    return View(new EditQuestViewModel { ... });
}
```
Note: `QuestLogController` does **not** have an `IsQuestOwner` static helper (that's `QuestController`-local, line 1043) — `QuestLogController`'s existing pattern is the inline `currentUser.Id == quest.DungeonMaster?.Id` comparison (see `UpdateRecap` above and `Details` GET line 64). **Reuse the inline comparison already established in this file**, not `QuestController`'s helper — do not import cross-controller.

**Existing helpers on this controller to reuse as-is** (`QuestLogController.cs` lines 119-139):
```csharp
private async Task<GroupRole?> GetEffectiveRoleAsync() =>
    User.IsInRole("SuperAdmin")
        ? GroupRole.Admin
        : await userService.GetEffectiveGroupRoleAsync(User, activeGroupContext.RequireActiveGroupId());

private async Task<BoardType> GetActiveBoardTypeAsync(CancellationToken token = default)
{
    if (activeGroupContext.ActiveGroupId is not { } groupId)
    {
        return BoardType.OneShot;
    }

    var group = await groupService.GetByIdAsync(groupId, token);
    return group?.BoardType ?? BoardType.OneShot;
}
```
The new GET action needs `ViewBag.BoardType` set the same way `Details` GET does (line 73) if the new view ever needs board-type context — UI-SPEC.md's `EditRecap.cshtml`/`.Mobile.cshtml` templates do **not** reference `ViewBag.BoardType`, so this is likely unnecessary for the new view, but keep in mind if validation/behavior needs it later.

**POST action — service call, copy exactly** (`QuestLogController.cs` line 114, `IQuestService`):
```csharp
await questService.UpdateQuestRecapAsync(id, recap, token);
return RedirectToAction("Details", new { id });
```

**Completed-quest guard to duplicate in the new GET action** (already duplicated between `Details` GET lines 44-50 and `UpdateRecap` POST lines 91-97 in this file — same three-line block, copy again):
```csharp
var isCompletedOneShot = quest.IsFinalized && quest.FinalizedDate.HasValue
    && quest.FinalizedDate.Value.Date <= DateTime.UtcNow.AddDays(-1).Date
    && !quest.DungeonMasterSession;
if (!isCompletedOneShot && !quest.IsClosed)
{
    return NotFound(); // or BadRequest, matching whichever the GET action should return — CONTEXT.md doesn't lock this explicitly, UpdateRecap POST uses BadRequest, Details GET uses NotFound
}
```

---

### `QuestBoard.Service/Views/QuestLog/EditRecap.cshtml` (component, request-response) — NEW FILE

**Analog:** `QuestBoard.Service/Views/Quest/Edit.cshtml`

**Full target markup already specified verbatim in UI-SPEC.md** ("View-by-View Contract" section 3) — copy directly, no further derivation needed. Key structural excerpts from the analog for context:

**Imports/model declaration pattern** (`Quest/Edit.cshtml` lines 1-8):
```razor
@using QuestBoard.Domain.Enums
@using QuestBoard.Domain.Interfaces
@using QuestBoard.Service.ViewModels.QuestViewModels
@model EditQuestViewModel
@{
    ViewData["Title"] = $"Edit Quest: {Model.Quest.Title}";
    var boardType = (BoardType)ViewBag.BoardType;
}
```
New file's equivalent (per UI-SPEC.md section 3, no `boardType` needed):
```razor
@using QuestBoard.Service.ViewModels.QuestLogViewModels
@model EditRecapViewModel
@{
    ViewData["Title"] = $"Edit Recap: {Model.Quest.Title}";
}
```

**Modern-card structure** (`Quest/Edit.cshtml` lines 10-20):
```html
<div class="row">
    <div class="col-lg-8 col-md-7">
        <div class="card modern-card">
            <div class="card-header modern-card-header">
                <h2 class="mb-0">
                    <i class="fas fa-edit me-2"></i>
                    @ViewData["Title"]
                </h2>
            </div>
            <div class="card-body modern-card-body">
                <div asp-validation-summary="All" class="text-danger mb-3"></div>
```
Icon differs per UI-SPEC.md: use `fa-book` (not `fa-edit`) for the EditRecap header — matches the Session Recap section icon on Details, deliberately avoiding reuse of `fa-edit` (reserved for the entry-point button per D-02).

**Form + hidden Id pattern** (`Quest/Edit.cshtml` lines 21-22):
```html
<form asp-action="Edit" asp-route-id="@Model.Id" method="post">
    <input type="hidden" asp-for="Id" />
```
New file: `asp-action="EditRecap"`, same `asp-route-id="@Model.Id"` + hidden `Id` pattern.

**Button row — `<hr>` + `d-flex justify-content-between`, Cancel left / primary right** — this is the one place the analog's own desktop button block (lines 99-106, single-row `me-2` layout with `btn-warning`) does **NOT** match the target; CONTEXT.md D-03 explicitly requires the two-button `d-flex justify-content-between` layout instead. Use `Quest/Edit.Mobile.cshtml`'s desktop-adjacent intent + UI-SPEC.md's literal markup:
```html
<hr />

<div class="d-flex justify-content-between">
    <a href="@Url.Action("Details", "QuestLog", new { id = Model.Id })" class="btn btn-secondary">
        <i class="fas fa-times me-2"></i>Cancel
    </a>
    <button type="submit" class="btn btn-primary">
        <i class="fas fa-save me-2"></i>Save Recap
    </button>
</div>
```
Note the color deviation from the analog: `Quest/Edit.cshtml`'s submit button is `btn-warning` ("Update Quest") — UI-SPEC.md's Color section explicitly locks Save Recap as `btn-primary`, not `btn-warning`. Do not copy the analog's button color, only its structural shape.

**No tips sidebar** — omit the analog's `col-lg-4 col-md-5` second column (`Quest/Edit.cshtml` lines 112-141) entirely; UI-SPEC.md explicitly excludes it for this simpler single-field form.

**No `@section Scripts` needed** — the analog's `_QuestFormScripts` partial (line 145) handles proposed-date add/remove JS, irrelevant to a recap textarea; omit this section entirely in the new view.

---

### `QuestBoard.Service/Views/QuestLog/EditRecap.Mobile.cshtml` (component, request-response) — NEW FILE

**Analog:** `QuestBoard.Service/Views/Quest/Edit.Mobile.cshtml`

**Styles section + container pattern** (`Quest/Edit.Mobile.cshtml` lines 10-14):
```razor
@section Styles {
    <link href="~/css/quest-edit.mobile.css" asp-append-version="true" rel="stylesheet" />
}

<div class="container-fluid px-2 mt-2">
```
Reuse verbatim — `quest-edit.mobile.css` already defines `.quest-edit-card-mobile` generically enough to apply to the new recap card (UI-SPEC.md confirms: "Do not create a `recap-edit.mobile.css`").

**Card header pattern** (`Quest/Edit.Mobile.cshtml` lines 25-31):
```html
<div class="quest-edit-card-mobile mb-3">

    <div class="mb-3">
        <h5 class="mb-0">
            <i class="fas fa-edit text-warning me-2"></i>Edit Quest: @Model.Quest.Title
        </h5>
    </div>
```
New file uses `fa-book` icon (not `fa-edit`/not `text-warning`) per UI-SPEC.md section 4 — `<i class="fas fa-book me-2"></i>Edit Recap: @Model.Quest.Title`.

**Mobile button row — `d-flex gap-2` + `flex-fill`, NOT `justify-content-between`** (`Quest/Edit.Mobile.cshtml` lines 101-108):
```html
<div class="d-flex gap-2 mt-3">
    <button type="submit" class="btn btn-warning flex-fill">
        <i class="fas fa-save me-2"></i>Update Quest
    </button>
    <a href="@Url.Action("Manage", "Quest", new { id = Model.Id })" class="btn btn-secondary flex-fill">
        <i class="fas fa-arrow-left me-2"></i>Back to Quest
    </a>
</div>
```
New file swaps order (Cancel left / Save right, per D-03), changes copy to "Cancel"/`fa-times` (not "Back to Quest"/`fa-arrow-left` — UI-SPEC.md explicitly forbids copying that label), and changes color to `btn-primary` for Save (not `btn-warning`):
```html
<div class="d-flex gap-2 mt-3">
    <a href="@Url.Action("Details", "QuestLog", new { id = Model.Id })" class="btn btn-secondary flex-fill">
        <i class="fas fa-times me-2"></i>Cancel
    </a>
    <button type="submit" class="btn btn-primary flex-fill">
        <i class="fas fa-save me-2"></i>Save Recap
    </button>
</div>
```

---

### `QuestBoard.Service/Views/QuestLog/Details.cshtml` (component, request-response) — MODIFY IN PLACE

**Analog:** itself — replace lines 97-136 (see UI-SPEC.md "View-by-View Contract" section 1 for exact replacement markup).

**Current inline form/display block being removed** (lines 97-136, shown above in full read) — the `@if ((bool)ViewBag.CanEditRecap) { <form>...</form> } else { ...read-only... }` branch. This entire conditional structure is replaced by: unconditional read-only display + conditional button underneath.

**Existing read-only sub-block to preserve verbatim** (currently lines 125-134, the `else` branch content):
```html
@if (!string.IsNullOrWhiteSpace(Model.Quest.Recap))
{
    <div class="recap-display-box">
        @Model.Quest.Recap
    </div>
}
else
{
    <p class="text-muted">No recap has been written for this quest yet.</p>
}
```
This exact block becomes unconditional (moves out of the `else`), and the new "Add Recap"/"Edit Recap" button (UI-SPEC.md section 1, lines 123-135) is appended below it inside the same `<div class="mb-4">`, gated on `ViewBag.CanEditRecap`.

**New button, using existing `Url.Action` + FontAwesome conventions already used elsewhere in this same file** (e.g. line 153's `Manage Quest` button pattern):
```html
@if ((bool)ViewBag.CanEditRecap)
{
    <a href="@Url.Action("EditRecap", "QuestLog", new { id = Model.Quest.Id })" class="btn btn-primary mt-3">
        @if (!string.IsNullOrWhiteSpace(Model.Quest.Recap))
        {
            <i class="fas fa-edit me-2"></i>@:Edit Recap
        }
        else
        {
            <i class="fas fa-plus me-2"></i>@:Add Recap
        }
    </a>
}
```

**No changes to file's `@model`, `@using`, or the surrounding Quest Information/Description/Adventurers sections** — only the Session Recap `<div class="mb-4">` block (lines 97-136) is touched.

---

### `QuestBoard.Service/Views/QuestLog/Details.Mobile.cshtml` (component, request-response) — MODIFY IN PLACE

**Analog:** itself — replace lines 91-124 (see UI-SPEC.md "View-by-View Contract" section 2 for exact replacement markup).

Same restructuring as desktop, mobile idiom differences:
- `<h6>` instead of `<h5>` for the section heading (matches this file's existing mobile heading convention, e.g. line 93's current `<h6><i class="fas fa-book me-2"></i>Session Recap</h6>`)
- `<p class="mb-0 text-muted">` for empty-state (matches this file's existing empty-state convention at line 121, and the "No adventurers participated" pattern at line 85)
- Button gets `w-100` (full-width) — matches this file's existing full-width button convention (`Back to Quest Log` at line 131, `Manage Quest` at line 136)

```html
@if ((bool)ViewBag.CanEditRecap)
{
    <a href="@Url.Action("EditRecap", "QuestLog", new { id = Model.Quest.Id })" class="btn btn-primary w-100 mt-3">
        @if (!string.IsNullOrWhiteSpace(Model.Quest.Recap))
        {
            <i class="fas fa-edit me-2"></i>@:Edit Recap
        }
        else
        {
            <i class="fas fa-plus me-2"></i>@:Add Recap
        }
    </a>
}
```

No CSS changes needed — `.recap-display-box` mobile override (`quest-log-detail.mobile.css` lines 59-72, already confirmed to exist and style the box correctly) applies unconditionally now, no new selectors required.

---

### `QuestBoard.Service/ViewModels/QuestLogViewModels/EditRecapViewModel.cs` (model) — NEW FILE

**Analog:** `QuestBoard.Service/ViewModels/QuestViewModels/EditQuestViewModel.cs`
```csharp
using QuestBoard.Domain.Models;

namespace QuestBoard.Service.ViewModels.QuestViewModels;

public class EditQuestViewModel
{
    public int Id { get; set; }
    public QuestViewModel Quest { get; set; } = new();
    public IList<User> DungeonMasters { get; set; } = [];
    public bool CanEditProposedDates { get; set; }
    public bool HasExistingSignups { get; set; }
}
```

Per UI-SPEC.md's "New ViewModel" section, the new ViewModel should be flattened (no nested full-quest-graph object) but the Razor templates reference `Model.Quest.Title` for the page heading — meaning a minimal nested reference (title-only, not the full `Quest` domain model) or a flattened `Title` field are both viable; UI-SPEC.md defers the exact shape to planning. Suggested shape consistent with the templates as literally written in UI-SPEC.md (which use `Model.Quest.Title` and `Model.Recap` and `Model.Id`):
```csharp
namespace QuestBoard.Service.ViewModels.QuestLogViewModels;

public class EditRecapViewModel
{
    public int Id { get; set; }
    public string? Recap { get; set; }
    public QuestSummaryViewModel Quest { get; set; } = new(); // or similar minimal type exposing only Title
}
```
Sibling model for reference — `QuestLogDetailsViewModel` (`QuestBoard.Service/ViewModels/QuestLogViewModels/QuestLogDetailsViewModel.cs`) uses the full `Quest` domain model directly (`Domain.Models.QuestBoard.Quest`) rather than a flattened DTO:
```csharp
using QuestBoard.Domain.Models.QuestBoard;

namespace QuestBoard.Service.ViewModels.QuestLogViewModels;

public class QuestLogDetailsViewModel
{
    public Quest Quest { get; set; } = new();
}
```
Simplest option matching UI-SPEC.md's literal templates exactly, reusing the full `Quest` domain model (already loaded via `GetQuestWithDetailsAsync` in the controller, no new mapping needed) is the lowest-friction choice — avoids introducing a new mapper profile entry for a one-field view.

## Shared Patterns

### Authorization (two-layer: policy + in-action ownership check)
**Source:** `QuestBoard.Service/Controllers/QuestBoard/QuestLogController.cs` lines 77-113 (`UpdateRecap`)
**Apply to:** New `EditRecap` GET and POST actions — both need `[Authorize(Policy = "DungeonMasterOnly")]` at the action level AND the in-action `isQuestDm || isAdmin` check that `Forbid()`s otherwise. Do not rely on the coarse policy alone.
```csharp
var isQuestDm = currentUser.Id == quest.DungeonMaster?.Id;
var isAdmin = await GetEffectiveRoleAsync() == GroupRole.Admin;

if (!isQuestDm && !isAdmin)
{
    return Forbid();
}
```

### Completed-quest guard
**Source:** `QuestBoard.Service/Controllers/QuestBoard/QuestLogController.cs` lines 44-50 / 91-97 (duplicated in both `Details` GET and `UpdateRecap` POST already)
**Apply to:** New `EditRecap` GET action (per CONTEXT.md's "Claude's Discretion," recommended for consistency)
```csharp
var isCompletedOneShot = quest.IsFinalized && quest.FinalizedDate.HasValue
    && quest.FinalizedDate.Value.Date <= DateTime.UtcNow.AddDays(-1).Date
    && !quest.DungeonMasterSession;
if (!isCompletedOneShot && !quest.IsClosed)
{
    return NotFound(); // matches Details GET; UpdateRecap POST uses BadRequest instead
}
```

### Modern-card view structure (CLAUDE.md-mandated)
**Source:** `QuestBoard.Service/Views/Quest/Edit.cshtml` lines 10-20, `CLAUDE.md` "UI/UX Design Guidelines"
**Apply to:** `EditRecap.cshtml` (desktop)
```html
<div class="card modern-card">
    <div class="card-header modern-card-header">
        <h2 class="mb-0">
            <i class="fas fa-icon-name me-2"></i>
            Page Title
        </h2>
    </div>
    <div class="card-body modern-card-body">
        ...
    </div>
</div>
```
Button layout: `<hr>` before button row, `d-flex justify-content-between` with secondary/Cancel left and primary/Save right — locked by CLAUDE.md and reinforced by CONTEXT.md D-03.

### Mobile card structure
**Source:** `QuestBoard.Service/Views/Quest/Edit.Mobile.cshtml`, reusing `quest-edit.mobile.css`
**Apply to:** `EditRecap.Mobile.cshtml`
```razor
@section Styles {
    <link href="~/css/quest-edit.mobile.css" asp-append-version="true" rel="stylesheet" />
}
<div class="container-fluid px-2 mt-2">
    <div class="quest-edit-card-mobile mb-3">
        ...
    </div>
</div>
```
Mobile button row idiom: `d-flex gap-2` + `flex-fill` on both buttons (not `justify-content-between` — that's desktop-only per every existing mobile edit view in this codebase).

### Recap read-only display box (unchanged, reused verbatim)
**Source:** `QuestBoard.Service/wwwroot/css/quests.css` lines 825-834 (`.recap-display-box`), `QuestBoard.Service/wwwroot/css/quest-log-detail.mobile.css` lines 59-72 (mobile override)
**Apply to:** `Details.cshtml`, `Details.Mobile.cshtml` — no CSS changes required, class already styles the box correctly for the now-unconditional read-only render.

## No Analog Found

None — all 6 files have strong or exact analogs already in the codebase (`Quest/Edit.cshtml` + `.Mobile.cshtml` for the two new views, `QuestController.Edit` GET+POST for the new controller actions, `EditQuestViewModel`/`QuestLogDetailsViewModel` for the new ViewModel, and the files' own prior versions for the two in-place Details view edits).

## Metadata

**Analog search scope:** `QuestBoard.Service/Controllers/QuestBoard/`, `QuestBoard.Service/Views/Quest/`, `QuestBoard.Service/Views/QuestLog/`, `QuestBoard.Service/ViewModels/`, `QuestBoard.Service/wwwroot/css/`, `QuestBoard.IntegrationTests/Controllers/QuestLogControllerIntegrationTests.cs`
**Files scanned:** 9 (QuestLogController.cs, QuestController.cs, Quest/Edit.cshtml, Quest/Edit.Mobile.cshtml, QuestLog/Details.cshtml, QuestLog/Details.Mobile.cshtml, EditQuestViewModel.cs, QuestLogDetailsViewModel.cs, QuestLogControllerIntegrationTests.cs)
**Pattern extraction date:** 2026-07-06
