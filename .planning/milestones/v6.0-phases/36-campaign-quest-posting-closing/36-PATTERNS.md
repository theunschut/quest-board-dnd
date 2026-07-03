# Phase 36: Campaign Quest Posting & Closing - Pattern Map

**Mapped:** 2026-07-03
**Files analyzed:** 20 (new/modified)
**Analogs found:** 20 / 20 (all files have an in-repo precedent — no new patterns invented by this phase)

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|---------------|
| `QuestBoard.Repository/Entities/QuestEntity.cs` (+`IsClosed`, `+ClosedDate`) | model (EF entity) | CRUD | Same file's existing `IsFinalized`/`FinalizedDate` fields | exact (additive, same file) |
| `QuestBoard.Domain/Models/QuestBoard/Quest.cs` (+`IsClosed`, `+ClosedDate`) | model (domain) | CRUD | Same file's existing `IsFinalized`/`FinalizedDate` fields | exact |
| `QuestBoard.Repository/Migrations/{ts}_AddQuestCloseFields.cs` | migration | batch (schema DDL) | `20260703113120_AddBoardTypeToGroup.cs` (column+default) and `20260702081517_AddQuestFinalizedDateIndex.cs` (index) | exact |
| `QuestBoard.Domain/Interfaces/IQuestRepository.cs` (+2 signatures) | interface | CRUD | Existing `OpenQuestAsync` signature/doc comment | exact |
| `QuestBoard.Repository/QuestRepository.cs` (+`CloseQuestAsync`, `+ReopenQuestAsync`) | service (repository impl) | CRUD | `OpenQuestAsync` (lines 141-157) | exact |
| `QuestBoard.Repository/QuestRepository.cs` (`GetQuestsWithSignupsAsync`/`GetQuestsWithSignupsForRoleAsync` edits) | service (repository impl) | CRUD (filter/query) | Same methods, existing `IsFinalized`/`FinalizedDate` OR-predicate | exact |
| `QuestBoard.Domain/Interfaces/IQuestService.cs` (+2 signatures) | interface | CRUD | Existing `OpenQuestAsync` signature/doc comment | exact |
| `QuestBoard.Domain/Services/QuestService.cs` (+`CloseQuestAsync`, `+ReopenQuestAsync`) | service (domain) | CRUD | `OpenQuestAsync` passthrough (lines 91-94) | exact |
| `QuestBoard.Domain/Services/QuestService.cs` (`GetCompletedQuestsAsync` edit) | service (domain) | CRUD (filter/query) | Same method, existing AND-chain predicate (lines 164-175) | exact |
| `QuestBoard.Service/Controllers/QuestBoard/QuestController.cs` (+`Close`, `+Reopen` actions) | controller | request-response | `Open` action (lines 637-666), `Finalize` action (lines 609-631) | exact |
| `QuestBoard.Service/Controllers/QuestBoard/QuestController.cs` (`Create` POST edit — conditional validation) | controller | request-response | `CreateFollowUp` POST's manual `ModelState.AddModelError` guard style (lines 816+) | role-match |
| `QuestBoard.Service/Controllers/QuestBoard/QuestController.cs` (+`GetActiveBoardTypeAsync` helper) | controller (private helper) | request-response | `GetEffectiveRoleAsync` helper pattern | exact |
| `QuestBoard.Service/Controllers/QuestBoard/QuestLogController.cs` (`Details`/`UpdateRecap` guard edits) | controller | request-response | Same methods, existing `IsFinalized`-based guard (lines 41, 83) | exact |
| `QuestBoard.Service/ViewModels/QuestViewModels/QuestViewModel.cs` (relax `ProposedDates`, +`BoardType`) | model (view model) | CRUD | Same file's existing `[Required, MinLength(1)]` attribute usage | exact |
| `QuestBoard.Service/Views/Quest/Index.cshtml` (BoardType-conditional card) | component (Razor view) | request-response (SSR) | Same file's existing wax-seal/CR-badge/signup-count block (lines 82-137) | exact |
| `QuestBoard.Service/Views/Quest/Index.Mobile.cshtml` (status badge logic) | component (Razor view) | request-response (SSR) | Same file's `IsFinalized`-based status badge (lines 39-85) | exact |
| `QuestBoard.Service/Views/Quest/Manage.cshtml` (+Close/Reopen buttons, CR badge removal) | component (Razor view) | request-response (SSR) | Same file's `Open`/`Finalize` button forms (lines 349-361, 508-528) | exact |
| `QuestBoard.Service/Views/Quest/Manage.Mobile.cshtml` (mirror of Manage.cshtml edits) | component (Razor view) | request-response (SSR) | `Manage.cshtml`'s equivalent sections | exact |
| `QuestBoard.Service/Views/Quest/Details.cshtml` (+`.Mobile`, CR badge removal, no signup UI) | component (Razor view) | request-response (SSR) | `Manage.cshtml`'s CR badge header block (lines 24-29) | role-match |
| `QuestBoard.Service/Views/Quest/Create.cshtml` (conditional field visibility) | component (Razor view) | request-response (SSR) | Same file's existing form field blocks (lines 32-78) | exact |
| `QuestBoard.Service/Views/QuestLog/Index.cshtml` (CR badge + Adventurers line removal) | component (Razor view) | request-response (SSR) | Same file's existing card markup (lines 27-30, 45-48) | exact |
| `QuestBoard.Service/Views/QuestLog/Details.cshtml` (guard/display parity) | component (Razor view) | request-response (SSR) | `QuestLog/Index.cshtml` card simplification + `QuestLogController.Details` guard | role-match |
| `QuestBoard.UnitTests/Services/QuestServiceTests.cs` (+new `[Fact]`s) | test | request-response (unit) | Existing `FinalizeQuestAsync_...` tests (lines 55-71) | exact |
| `QuestBoard.IntegrationTests/Controllers/QuestFinalizeTests.cs` or new `QuestCloseTests.cs` | test | request-response (integration) | `QuestFinalizeTests.cs` (whole file, 61 lines) | exact |
| `QuestBoard.IntegrationTests/Helpers/TestDataHelper.cs` (`CreateTestQuestAsync` params) | utility (test helper) | CRUD (seed data) | Same method (lines 8-39) | exact |

## Pattern Assignments

### `QuestBoard.Repository/Entities/QuestEntity.cs` (model, CRUD)

**Analog:** same file, existing `IsFinalized`/`FinalizedDate` fields (lines 27-31)

**Fields to add** (immediately after `FinalizedEmailSentForDate`, line 31):
```csharp
public DateTime? FinalizedDate { get; set; }

public bool IsFinalized { get; set; }

public DateTime? FinalizedEmailSentForDate { get; set; }

// ADD:
public DateTime? ClosedDate { get; set; }

public bool IsClosed { get; set; }
```
No `[Required]` attribute needed — mirrors `FinalizedDate`'s nullable, unannotated style exactly. `IsClosed` needs no attribute either, matching `IsFinalized`.

---

### `QuestBoard.Domain/Models/QuestBoard/Quest.cs` (model, CRUD)

**Analog:** same file, lines 26-28

Add `IsClosed`/`ClosedDate` in the same relative position as the entity (after `FinalizedEmailSentForDate`, line 30):
```csharp
public DateTime? FinalizedDate { get; set; }

public bool IsFinalized { get; set; }

public DateTime? FinalizedEmailSentForDate { get; set; }

// ADD:
public DateTime? ClosedDate { get; set; }

public bool IsClosed { get; set; }
```
AutoMapper's default same-name mapping (used for `IsFinalized`/`FinalizedDate` today, confirmed by absence of any `ForMember` for those fields in `EntityProfile.cs`) applies automatically — no new `ForMember` needed in `QuestBoard.Repository/Automapper/EntityProfile.cs`.

---

### `QuestBoard.Repository/Migrations/{timestamp}_AddQuestCloseFields.cs` (migration, batch)

**Analog 1 (column+default):** `QuestBoard.Repository/Migrations/20260703113120_AddBoardTypeToGroup.cs`
```csharp
// Source: 20260703113120_AddBoardTypeToGroup.cs:11-19 — column-add pattern to mirror
protected override void Up(MigrationBuilder migrationBuilder)
{
    migrationBuilder.AddColumn<int>(
        name: "BoardType",
        table: "Groups",
        type: "int",
        nullable: false,
        defaultValue: 0);
}
```
Apply the same shape twice — once for `IsClosed` (`type: "bit"`, `nullable: false`, `defaultValue: false`) and once for `ClosedDate` (`type: "datetime2"`, `nullable: true`, no `defaultValue`) — on table `"Quests"`.

**Analog 2 (index):** `QuestBoard.Repository/Migrations/20260702081517_AddQuestFinalizedDateIndex.cs`
```csharp
// Source: 20260702081517_AddQuestFinalizedDateIndex.cs:11-17 — index pattern, only if planner decides one is warranted
migrationBuilder.CreateIndex(
    name: "IX_Quests_IsFinalized_FinalizedDate",
    table: "Quests",
    columns: new[] { "IsFinalized", "FinalizedDate" });
```
If added, name it `IX_Quests_IsClosed_ClosedDate`, columns `{ "IsClosed", "ClosedDate" }`. Per Pitfall 5: `Up()` must order `AddColumn → AddColumn → CreateIndex`; `Down()` must reverse-order `DropIndex → DropColumn → DropColumn` (drop the index before dropping either column it references).

Migration must live in `QuestBoard.Repository` only (never add EF packages to `QuestBoard.Service`), generated via:
```
dotnet ef migrations add AddQuestCloseFields --project ../QuestBoard.Repository
```
(run from `QuestBoard.Service/`, per CLAUDE.md).

---

### `QuestBoard.Repository/QuestRepository.cs` — `CloseQuestAsync`/`ReopenQuestAsync` (service, CRUD)

**Analog:** `OpenQuestAsync` (lines 141-157)

**Core pattern to mirror** (note: no `.Include(q => q.PlayerSignups)` needed — campaign quests never have signups):
```csharp
// Source: QuestBoard.Repository/QuestRepository.cs:141-157 — OpenQuestAsync, the shape to copy
public async Task OpenQuestAsync(int questId, CancellationToken token = default)
{
    var entity = await DbContext.Quests
        .Include(q => q.PlayerSignups)
        .FirstOrDefaultAsync(q => q.Id == questId, cancellationToken: token);
    if (entity == null) return;

    entity.IsFinalized = false;
    entity.FinalizedDate = null;

    foreach (var playerSignup in entity.PlayerSignups)
    {
        playerSignup.IsSelected = false;
    }

    await DbContext.SaveChangesAsync(token);
}
```
New methods (add after `OpenQuestAsync`, line 157):
```csharp
/// <inheritdoc/>
public async Task CloseQuestAsync(int questId, CancellationToken token = default)
{
    var entity = await DbContext.Quests
        .FirstOrDefaultAsync(q => q.Id == questId, cancellationToken: token);
    if (entity == null) return;

    entity.IsClosed = true;
    entity.ClosedDate = DateTime.UtcNow;

    await DbContext.SaveChangesAsync(token);
}

/// <inheritdoc/>
public async Task ReopenQuestAsync(int questId, CancellationToken token = default)
{
    var entity = await DbContext.Quests
        .FirstOrDefaultAsync(q => q.Id == questId, cancellationToken: token);
    if (entity == null) return;

    entity.IsClosed = false;
    entity.ClosedDate = null;

    await DbContext.SaveChangesAsync(token);
}
```

**Query-filter pattern to extend** (`GetQuestsWithSignupsAsync`, lines 58-66; `GetQuestsWithSignupsForRoleAsync`, lines 69-78):
```csharp
// Source: QuestBoard.Repository/QuestRepository.cs:58-66 — existing one-shot-only predicate
public async Task<IList<Quest>> GetQuestsWithSignupsAsync(CancellationToken token = default)
{
    var oneDayAgo = DateTime.UtcNow.AddDays(-1);
    var entities = await ProjectWithoutCharacterImages(DbContext.Quests)
        .Where(q => !q.IsFinalized || (q.IsFinalized && q.FinalizedDate > oneDayAgo))
        .OrderByDescending(q => q.CreatedAt)
        .ToListAsync(cancellationToken: token);
    return Mapper.Map<IList<Quest>>(entities);
}
```
Extend the `.Where()` predicate with an OR-branch for `!q.IsClosed`, e.g.:
```csharp
.Where(q => (!q.IsFinalized || (q.IsFinalized && q.FinalizedDate > oneDayAgo)) && !q.IsClosed)
```
(A campaign quest never sets `IsFinalized`, so the left clause is always true for it — `!q.IsClosed` is the only meaningful gate. Do this as an AND onto the whole existing clause, not nested inside it — see Pitfall 3 for why OR/AND placement matters, though this particular filter is AND-shaped because "closed" should exclude regardless of one-shot state, unlike `GetCompletedQuestsAsync` which is OR-shaped for the opposite reason — being *included*.)

---

### `QuestBoard.Domain/Services/QuestService.cs` — `CloseQuestAsync`/`ReopenQuestAsync` (service, CRUD)

**Analog:** `OpenQuestAsync` (lines 91-94) — thin passthrough, no email dispatch

```csharp
// Source: QuestBoard.Domain/Services/QuestService.cs:90-94 — OpenQuestAsync passthrough, the shape to copy
/// <inheritdoc/>
public async Task OpenQuestAsync(int questId, CancellationToken token = default)
{
    await repository.OpenQuestAsync(questId, token);
}
```
New methods:
```csharp
/// <inheritdoc/>
public async Task CloseQuestAsync(int questId, CancellationToken token = default)
{
    await repository.CloseQuestAsync(questId, token);
}

/// <inheritdoc/>
public async Task ReopenQuestAsync(int questId, CancellationToken token = default)
{
    await repository.ReopenQuestAsync(questId, token);
}
```
**Critical:** unlike `FinalizeQuestAsync` (lines 17-46), these must NOT call `dispatcher.EnqueueFinalizedEmail` or any `IQuestEmailDispatcher` method — structural email suppression for CQUEST-06 depends on this.

**`GetCompletedQuestsAsync` OR-branch edit** (lines 164-175):
```csharp
// Source: QuestBoard.Domain/Services/QuestService.cs:164-175 — existing AND-chain predicate
public async Task<IList<Quest>> GetCompletedQuestsAsync(CancellationToken token = default)
{
    var quests = await repository.GetQuestsWithDetailsAsync(token);

    return quests
        .Where(q => q.IsFinalized
                    && q.FinalizedDate.HasValue
                    && q.FinalizedDate.Value.Date <= DateTime.UtcNow.AddDays(-1).Date
                    && !q.DungeonMasterSession)
        .OrderByDescending(q => q.FinalizedDate)
        .ToList();
}
```
Change to an **OR**, not an extended AND (Pitfall 3):
```csharp
.Where(q => (q.IsFinalized
             && q.FinalizedDate.HasValue
             && q.FinalizedDate.Value.Date <= DateTime.UtcNow.AddDays(-1).Date
             && !q.DungeonMasterSession)
            || q.IsClosed)
.OrderByDescending(q => q.IsClosed ? q.ClosedDate : q.FinalizedDate)
```
(Ordering also needs to key off `ClosedDate` for campaign entries — plain `OrderByDescending(q => q.FinalizedDate)` would sort every campaign quest as `null`/oldest.)

---

### `QuestBoard.Service/Controllers/QuestBoard/QuestController.cs` — `Close`/`Reopen` actions (controller, request-response)

**Analog:** `Open` action (lines 637-666), authorization/guard shape from `Finalize` (lines 609-631)

```csharp
// Source: QuestBoard.Service/Controllers/QuestBoard/QuestController.cs:637-666 — Open action, the exact shape to copy
[HttpPost]
[ValidateAntiForgeryToken]
[Authorize(Policy = "DungeonMasterOnly")]
public async Task<IActionResult> Open(int id)
{
    var quest = await questService.GetQuestWithDetailsAsync(id);

    if (quest == null || !quest.IsFinalized)
    {
        return NotFound();
    }

    var currentUser = await userService.GetUserAsync(User);
    if (currentUser == null)
    {
        return Challenge();
    }

    // Verify DM authorization
    var role = await GetEffectiveRoleAsync();
    if (!IsQuestOwner(currentUser, quest.DungeonMaster) && role != GroupRole.Admin)
    {
        return Forbid();
    }

    // Open the quest using the specialized service method
    await questService.OpenQuestAsync(id);

    return RedirectToAction("Manage", new { id });
}
```
New actions (D-01/D-02/D-03 — identical shape, stays on `Manage`, no confirm dialog):
```csharp
[HttpPost]
[ValidateAntiForgeryToken]
[Authorize(Policy = "DungeonMasterOnly")]
public async Task<IActionResult> Close(int id)
{
    var quest = await questService.GetQuestWithDetailsAsync(id);
    if (quest == null || quest.IsClosed) return NotFound();

    var currentUser = await userService.GetUserAsync(User);
    if (currentUser == null) return Challenge();

    var role = await GetEffectiveRoleAsync();
    if (!IsQuestOwner(currentUser, quest.DungeonMaster) && role != GroupRole.Admin) return Forbid();

    await questService.CloseQuestAsync(id);
    return RedirectToAction("Manage", new { id });
}

[HttpPost]
[ValidateAntiForgeryToken]
[Authorize(Policy = "DungeonMasterOnly")]
public async Task<IActionResult> Reopen(int id)
{
    var quest = await questService.GetQuestWithDetailsAsync(id);
    if (quest == null || !quest.IsClosed) return NotFound();

    var currentUser = await userService.GetUserAsync(User);
    if (currentUser == null) return Challenge();

    var role = await GetEffectiveRoleAsync();
    if (!IsQuestOwner(currentUser, quest.DungeonMaster) && role != GroupRole.Admin) return Forbid();

    await questService.ReopenQuestAsync(id);
    return RedirectToAction("Manage", new { id });
}
```
Note: `IsQuestOwner` is the Phase 34.3-fixed `User.Id`-based ownership check — do not reintroduce a name-based comparison (Security Domain finding in RESEARCH.md).

**`Create` POST conditional validation (imports pattern + guard-clause style):**
```csharp
// Source: QuestBoard.Service/Controllers/QuestBoard/QuestController.cs:1-12 — import block convention
using AutoMapper;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Extensions;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.Domain.Models.QuestBoard;
using QuestBoard.Service.ViewModels.CalendarViewModels;
using QuestBoard.Service.ViewModels.QuestViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
```
Existing `Create` POST (lines 78-111) currently does `if (!ModelState.IsValid) return View(viewModel);` unconditionally. Per RESEARCH.md's resolved approach: do NOT add `[Required]`-conditional attributes to `QuestViewModel`; instead relax `ProposedDates` to a plain `[]`-default field (drop `[Required, MinLength(1)]`) and add a hand-written guard before `ModelState.IsValid`, mirroring `CreateFollowUp` POST's manual `ModelState.AddModelError` style — read `BoardType` server-side (never trust a posted value) via the new `GetActiveBoardTypeAsync()` helper (mirrors `GetEffectiveRoleAsync`'s shape), then branch:
```csharp
var boardType = await GetActiveBoardTypeAsync(token);
if (boardType == BoardType.OneShot && (viewModel.ProposedDates == null || viewModel.ProposedDates.Count == 0))
{
    ModelState.AddModelError(nameof(viewModel.ProposedDates), "At least one proposed date is required.");
}
if (boardType == BoardType.Campaign)
{
    viewModel.ProposedDates = [];
    viewModel.ChallengeRating = 1;
    viewModel.TotalPlayerCount = 0;
    viewModel.DungeonMasterSession = false;
}
```
(Exact default values per Assumption A3 — confirm with planner/user if stricter defaults desired.)

**`GetActiveBoardTypeAsync` helper (new private method, colocated with `GetEffectiveRoleAsync`):**
```csharp
// New helper — mirrors the existing GetEffectiveRoleAsync private-helper convention
private async Task<BoardType> GetActiveBoardTypeAsync(CancellationToken token = default)
{
    var groupId = activeGroupContext.RequireActiveGroupId();
    var group = await groupService.GetByIdAsync(groupId, token);
    return group?.BoardType ?? BoardType.OneShot;
}
```
Requires injecting `IGroupService groupService` into the constructor (currently not present — constructor at lines 14-22).

---

### `QuestBoard.Service/Controllers/QuestBoard/QuestLogController.cs` — guard edits (controller, request-response)

**Analog:** same file, existing guards (lines 41, 83)

```csharp
// Source: QuestBoard.Service/Controllers/QuestBoard/QuestLogController.cs:40-44 — Details guard, current form
// Verify this is a completed quest (DM-only sessions are not shown in the quest log)
if (!quest.IsFinalized || !quest.FinalizedDate.HasValue || quest.FinalizedDate.Value.Date > DateTime.UtcNow.AddDays(-1).Date || quest.DungeonMasterSession)
{
    return NotFound();
}
```
Per Pitfall 2, change to an explicit OR-branch admitting closed campaign quests:
```csharp
var isCompletedOneShot = quest.IsFinalized && quest.FinalizedDate.HasValue
    && quest.FinalizedDate.Value.Date <= DateTime.UtcNow.AddDays(-1).Date
    && !quest.DungeonMasterSession;
if (!isCompletedOneShot && !quest.IsClosed)
{
    return NotFound();
}
```
Same restructuring applies to `UpdateRecap`'s guard (line 83, currently `BadRequest` instead of `NotFound`) — keep the `BadRequest` response type for `UpdateRecap`, just update the condition the same way.

---

### `QuestBoard.Service/ViewModels/QuestViewModels/QuestViewModel.cs` (model, CRUD)

**Analog:** same file, current state
```csharp
// Source: QuestBoard.Service/ViewModels/QuestViewModels/QuestViewModel.cs:25-27 — current attribute to relax
[Required]
[MinLength(1, ErrorMessage = "At least one proposed date is required.")]
public IList<DateTime> ProposedDates { get; set; } = [DateTime.Today.AddDays(1).AddHours(18)];
```
Change to (drop both attributes, default to empty list so campaign posts cleanly and the controller-level guard from above does the OneShot-only enforcement):
```csharp
public IList<DateTime> ProposedDates { get; set; } = [];
```
Do **not** add a bindable `BoardType` property to this ViewModel (Security Domain finding — never trust a posted `BoardType`; always resolve server-side via `GetActiveBoardTypeAsync()`).

---

### `QuestBoard.Service/Views/Quest/Index.cshtml` (component, SSR)

**Analog:** same file, current wax-seal/CR-badge/signup block (lines 82-137)

```html
<!-- Source: QuestBoard.Service/Views/Quest/Index.cshtml:82-137 — current markup -->
<!-- Wax Seal (Finalized Quest) -->
<div class="wax-seal" style="bottom: @(sealBottom)%; left: @(sealLeft)%;">
    <img src="/images/Wax Seals/@waxSeal" alt="@(quest.IsFinalized ? "Finalized" : "Open") Quest"
         class="seal-image @(quest.IsFinalized ? "finalized-seal" : "open-seal")" />
</div>
...
<!-- Challenge Rating -->
<div class="challenge-rating">
    <span class="cr-badge">
        <i class="fas fa-dice-d20 cr-icon" title="Challenge Rating"></i>
        CR @quest.ChallengeRating
    </span>
</div>
...
@if (quest.IsFinalized && quest.FinalizedDate.HasValue)
{
    <div class="quest-date">...</div>
    <div class="quest-players"><strong>Selected Adventurers:</strong> @quest.PlayerSignups.Where(ps => ps.IsSelected).Count()</div>
}
else
{
    <div class="quest-signups"><strong>Adventurers signed up:</strong> @quest.PlayerSignups.Count()</div>
}
```
Per UI-SPEC's "Key Design Decision" section (resolves D-06), apply exactly this contract:
```html
<img src="/images/Wax Seals/@waxSeal"
     alt="@((quest.BoardType == BoardType.Campaign ? (quest.IsClosed ? "Closed" : "Open") : (quest.IsFinalized ? "Finalized" : "Open"))) Quest"
     class="seal-image @((quest.BoardType == BoardType.Campaign ? quest.IsClosed : quest.IsFinalized) ? "finalized-seal" : "open-seal")" />
```
CR badge block (lines 97-103): wrap in `@if (quest.BoardType != BoardType.Campaign) { ... }` — full removal, not `display:none` (D-04).
Signup-count block (lines 122-137): wrap the whole `@if/else` in an outer `@if (quest.BoardType != BoardType.Campaign) { ... }` (D-05) — no substitute content, let `.quest-description` (`flex-grow:1`) fill the space; no CSS changes needed per UI-SPEC.
Requires `quest.BoardType` to be available on the view model passed to `Index` — per RESEARCH.md Pattern 3, thread via `ViewBag.BoardType` (populated once in `QuestController.Index` from `GetActiveBoardTypeAsync()`), not by adding `BoardType` onto the `Quest` domain model. Adjust the Razor conditions above to read `(BoardType)ViewBag.BoardType` instead of `quest.BoardType` if a per-quest field is not added.

---

### `QuestBoard.Service/Views/Quest/Manage.cshtml` (component, SSR)

**Analog:** same file, `Open`/`Finalize` button forms (lines 349-361, 508-528)

```html
<!-- Source: QuestBoard.Service/Views/Quest/Manage.cshtml:508-528 — Open Quest button, the layout/placement pattern to copy -->
<div class="d-flex justify-content-between align-items-center">
    <div class="d-flex gap-2">
        <form asp-action="Open" method="post" style="display: inline;">
            <input type="hidden" name="id" value="@Model.Id" />
            <button type="submit" class="btn btn-warning" onclick="return confirm('...');">Open Quest</button>
        </form>
        ...
    </div>
    <button type="button" class="btn btn-secondary" onclick="window.location.reload()">Refresh Data</button>
</div>
```
Per UI-SPEC Copywriting Contract and Color section: Close button uses `btn-secondary` (no confirm), Reopen uses `btn-warning` (no confirm — differs from `Open`'s `confirm()` since Reopen has no destructive side effect):
```html
<form asp-action="Close" method="post" style="display: inline;">
    <input type="hidden" name="id" value="@Model.Id" />
    @Html.AntiForgeryToken()
    <button type="submit" class="btn btn-secondary">Close Quest</button>
</form>
```
```html
<form asp-action="Reopen" method="post" style="display: inline;">
    <input type="hidden" name="id" value="@Model.Id" />
    @Html.AntiForgeryToken()
    <button type="submit" class="btn btn-warning">Reopen Quest</button>
</form>
```
Placement: same `.d-flex.justify-content-between` row, left-grouped with other primary/utility actions, `Refresh Data` alone on the right — per UI-SPEC's explicit note that existing Finalize/Open layout precedent overrides the generic CLAUDE.md left/right convention here.

CR badge in card header (lines 26-29):
```html
<!-- Source: QuestBoard.Service/Views/Quest/Manage.cshtml:24-30 -->
<div class="card-header modern-card-header d-flex justify-content-between align-items-center">
    <h2 class="mb-0">@ViewData["Title"]</h2>
    <span class="badge cr-badge fs-6">
        <i class="fas fa-dice-d20 me-1"></i>
        CR @Model.ChallengeRating
    </span>
</div>
```
Wrap the `<span class="badge cr-badge...">` in `@if ((BoardType)ViewBag.BoardType != BoardType.Campaign)` (D-04).

---

### `QuestBoard.Service/Views/QuestLog/Index.cshtml` (component, SSR)

**Analog:** same file, current card markup (lines 27-30, 45-48)

```html
<!-- Source: QuestBoard.Service/Views/QuestLog/Index.cshtml:27-30 — CR badge to remove for campaign -->
<span class="cr-badge">
    <i class="fas fa-dice-d20 me-1"></i>
    CR @quest.ChallengeRating
</span>
```
```html
<!-- Source: QuestBoard.Service/Views/QuestLog/Index.cshtml:45-48 — Adventurers meta-item to remove for campaign -->
<div class="meta-item">
    <i class="fas fa-users me-2"></i>
    <span><strong>Adventurers:</strong> @selectedPlayers.Count</span>
</div>
```
Per D-07: wrap both in a `BoardType` check (same source as `Index.cshtml`/`Manage.cshtml` — group's `BoardType`, since `Quest` has no own `BoardType` field; thread via each quest's `GroupId` or a per-item `ViewBag`/lookup, since Quest Log lists multiple quests — but per the "Note" in CONTEXT.md, a single group's Quest Log is homogeneous, so a single `ViewBag.BoardType` populated once in `QuestLogController.Index` is sufficient, matching Pattern 3's "single lookup per action" reasoning).

---

## Shared Patterns

### Authorization (DungeonMasterOnly + ownership-or-admin)
**Source:** `QuestBoard.Service/Controllers/QuestBoard/QuestController.cs:637-660` (`Open` action)
**Apply to:** `Close`, `Reopen` actions
```csharp
[Authorize(Policy = "DungeonMasterOnly")]
...
var role = await GetEffectiveRoleAsync();
if (!IsQuestOwner(currentUser, quest.DungeonMaster) && role != GroupRole.Admin) return Forbid();
```
Never reintroduce name-based ownership comparison — `IsQuestOwner` is already `User.Id`-based (Phase 34.3 fix).

### CSRF protection on mutating POSTs
**Source:** every existing `[HttpPost]` action in `QuestController`/`QuestLogController`
**Apply to:** `Close`, `Reopen` actions and their Razor `<form>` tags
```csharp
[ValidateAntiForgeryToken]
```
```html
@Html.AntiForgeryToken()
```

### Tenant isolation (implicit, no new code needed)
**Source:** `QuestEntity`'s `HasQueryFilter` (`e.GroupId == activeGroupContext.ActiveGroupId`), confirmed via `GetQuestWithDetailsAsync`
**Apply to:** `Close`/`Reopen` — call the same filtered `GetQuestWithDetailsAsync(id)` (not an `IgnoreQueryFilters()` variant) so cross-group id-guessing 404s before authorization is even checked, exactly like `Open`/`Finalize` today.

### BoardType-conditional dispatch
**Source:** `QuestBoard.Domain/Services/ShopService.cs:50-62` (`CalculateItemPriceAsync`, `ItemRarity switch` — the locked project convention per PROJECT.md)
**Apply to:** Any new `BoardType`-branching logic in `QuestController`/views
```csharp
// Source: QuestBoard.Domain/Services/ShopService.cs:50-62 — switch-expression convention to follow
return Task.FromResult(rarity switch
{
    ItemRarity.Common => 100m,
    ItemRarity.Uncommon => 500m,
    ...
    _ => 100m
});
```
Use `switch` expressions, not `if/else` chains, wherever `BoardType` drives more than a single boolean branch.

### ViewBag threading for view-only, non-domain data
**Source:** `QuestController.Index` (`ViewBag.CurrentUserName`, `ViewBag.CurrentUserId`, lines 59-60); `Manage.cshtml`'s existing `ViewBag.IsAuthorized`/`ViewBag.IsAdmin`
**Apply to:** `ViewBag.BoardType`, populated once per action via the new `GetActiveBoardTypeAsync()` helper, in `QuestController.Index`/`Details`/`Manage`/`Create` and `QuestLogController.Index`/`Details`.

### Migration column-add + optional index (two-step, reversible)
**Source:** `20260703113120_AddBoardTypeToGroup.cs` + `20260702081517_AddQuestFinalizedDateIndex.cs`
**Apply to:** `AddQuestCloseFields` migration — `Up()`: AddColumn(s) → CreateIndex; `Down()`: DropIndex → DropColumn(s) (reverse order, per Pitfall 5).

## No Analog Found

None — every file this phase touches has a direct, exact-match precedent already in the codebase (the one-shot `Finalize`/`Open` lifecycle, the `BoardType`/index migrations, and the existing `QuestServiceTests`/`QuestFinalizeTests`/`TestDataHelper` test scaffolding). This phase is explicitly an "audit and extend" task per RESEARCH.md, not a novel-pattern task.

## Metadata

**Analog search scope:** `QuestBoard.Repository/` (Entities, Migrations, QuestRepository.cs, Automapper), `QuestBoard.Domain/` (Models, Interfaces, Services, Enums), `QuestBoard.Service/` (Controllers/QuestBoard, ViewModels/QuestViewModels, Views/Quest, Views/QuestLog), `QuestBoard.UnitTests/Services`, `QuestBoard.IntegrationTests/Controllers`, `QuestBoard.IntegrationTests/Helpers`
**Files scanned:** ~25 (all read directly, no broad globbing needed — RESEARCH.md and CONTEXT.md already pinpointed exact file/line locations)
**Pattern extraction date:** 2026-07-03
