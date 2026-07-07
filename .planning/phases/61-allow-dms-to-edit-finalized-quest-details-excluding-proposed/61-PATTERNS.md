# Phase 61: Allow DMs to edit finalized quest details - Pattern Map

**Mapped:** 2026-07-07
**Files analyzed:** 6 (all pre-existing, no new files)
**Analogs found:** 6 / 6 (self-analogs — every touched file has its pattern already established elsewhere in the same file)

## File Classification

This phase modifies zero new files — every file already exists and each one supplies its own precedent nearby (same controller, same view, same repository method). "Analog" here means the nearest existing block within the same file/codebase area that already does the thing this phase needs to add.

| Modified File | Role | Data Flow | Closest Analog | Match Quality |
|---|---|---|---|---|
| `QuestBoard.Service/Controllers/QuestBoard/QuestController.cs` (`Edit` GET/POST) | controller | request-response | `Create` POST (same file, lines 84-136) — `ModelState.AddModelError` + BoardType-conditional sanitization | exact |
| `QuestBoard.Domain/Services/QuestService.cs` (`UpdateQuestPropertiesWithNotificationsAsync` call site) | service | CRUD | same method, existing `updateProposedDates` boolean parameter (lines 133-153) | exact — no code change needed in this file |
| `QuestBoard.Repository/QuestRepository.cs` (`UpdateQuestPropertiesWithNotificationsAsync` impl) | model/repository | CRUD | same method (lines 188-216) — already branches on `updateProposedDates && proposedDates != null` | exact — no code change needed |
| `QuestBoard.Service/Views/Quest/Edit.cshtml` | component (Razor view) | request-response | same file's `@if (boardType != BoardType.Campaign)` block (lines 42-103) | exact |
| `QuestBoard.Service/Views/Quest/Edit.Mobile.cshtml` | component (Razor view) | request-response | same file's `@if (boardType != BoardType.Campaign)` block (lines 56-105) — desktop's mobile twin | exact |
| `QuestBoard.Service/Views/Quest/Manage.cshtml` | component (Razor view) | request-response | same file's pre-finalize "No Proposed Dates" `Edit Quest` link (lines 108-111) + `Open`/`CreateFollowUp`/`SendReminder` button row (lines 500-519) | exact |
| `QuestBoard.Service/Views/Quest/Manage.Mobile.cshtml` | component (Razor view) | request-response | same file's `Open`/`CreateFollowUp` button row (lines 120-139) | exact |

**Not modified but referenced for validation pattern:**
- `QuestController.cs` line 441-442 (`JoinFinalizedQuest`) — the exact `IsSelected && ps.Role == SignupRole.Player` predicate D-01's guard needs, closer than `Finalize`'s `selectedPlayerIds.Contains` variant since Edit (like `JoinFinalizedQuest`) reasons over *existing* selections, not a posted list.

## Pattern Assignments

### `QuestController.cs` — `Edit` GET (controller, request-response)

**Analog:** same file, `Edit` GET itself (lines 140-185) — only the finalized-block removal changes; everything else stays.

**Block to remove** (lines 162-166):
```csharp
// Don't allow editing of finalized quests
if (quest.IsFinalized)
{
    return BadRequest("Cannot edit a finalized quest. Open the quest first to make changes.");
}
```

**IsFinalized plumbing precedent** — `EditQuestViewModel` construction (lines 177-184) already threads several derived booleans (`CanEditProposedDates`, `HasExistingSignups`) alongside `Quest`/`DungeonMasters`; `IsFinalized` should be added the same way:
```csharp
return View(new EditQuestViewModel
{
    Id = quest.Id,
    Quest = questViewModel,
    DungeonMasters = dms,
    CanEditProposedDates = canEditProposedDates,
    HasExistingSignups = hasExistingSignups
    // add: IsFinalized = quest.IsFinalized
});
```

---

### `QuestController.cs` — `Edit` POST (controller, request-response)

**Analog:** `Create` POST's proposed-dates-required check (lines 99-102) is the exact `ModelState.AddModelError` precedent for D-01:
```csharp
var boardType = await GetActiveBoardTypeAsync(token);
if (boardType == BoardType.OneShot && (viewModel.ProposedDates == null || viewModel.ProposedDates.Count == 0))
{
    ModelState.AddModelError(nameof(viewModel.ProposedDates), "At least one proposed date is required.");
}
```
D-01's Total Player Count guard mirrors this shape: compute the boolean condition, then `ModelState.AddModelError(nameof(viewModel.Quest.TotalPlayerCount), "...")` before the `if (!ModelState.IsValid)` check at line 235.

**Selected-player-count predicate** — use `JoinFinalizedQuest`'s existing predicate (line 441-442), not `Finalize`'s (which operates on a posted list, irrelevant here since Edit never touches signups):
```csharp
quest.PlayerSignups.Where(ps => ps.IsSelected && ps.Role == SignupRole.Player).Count() < quest.TotalPlayerCount
```
D-01 needs the count itself (not just the boolean), e.g.:
```csharp
var selectedPlayerCount = existingQuest.PlayerSignups.Count(ps => ps.IsSelected && ps.Role == SignupRole.Player);
if (existingQuest.IsFinalized && viewModel.Quest.TotalPlayerCount < selectedPlayerCount)
{
    ModelState.AddModelError(nameof(viewModel.Quest.TotalPlayerCount),
        $"Total Player Count cannot be less than the {selectedPlayerCount} players already selected for this quest.");
}
```

**Block to remove** (lines 217-221):
```csharp
// Don't allow editing of finalized quests
if (existingQuest.IsFinalized)
{
    return BadRequest("Cannot edit a finalized quest. Open the quest first to make changes.");
}
```

**Re-render-on-validation-failure path** (lines 235-241) — already re-hydrates `DungeonMasters`/`ViewBag.BoardType` before returning the view; `IsFinalized` needs the same treatment (set on `viewModel` before `return View(viewModel)`, since this is a full-page re-render, not a redirect):
```csharp
if (!ModelState.IsValid)
{
    var dms = await userService.GetAllDungeonMastersAsync(token);
    viewModel.DungeonMasters = dms;
    ViewBag.BoardType = boardType;
    return View(viewModel);
}
```

**Existing safe call, D-04's `updateProposedDates: false` swap** (lines 253-264) — the boolean literal `true` at line 261 becomes conditional on `existingQuest.IsFinalized`, and `viewModel.Quest.ProposedDates` at line 262 is only passed when not finalized:
```csharp
await questService.UpdateQuestPropertiesWithNotificationsAsync(
    id,
    viewModel.Quest.Title,
    viewModel.Quest.Description,
    viewModel.Quest.Rewards,
    viewModel.Quest.ChallengeRating,
    viewModel.Quest.TotalPlayerCount,
    viewModel.Quest.DungeonMasterSession,
    !existingQuest.IsFinalized,                                            // was: true
    existingQuest.IsFinalized ? null : viewModel.Quest.ProposedDates,       // was: viewModel.Quest.ProposedDates
    token
);
```

**Authorization pattern (unchanged, no modification needed)** — identical block appears in both GET (lines 155-160) and POST (lines 210-215), and in `Finalize`/`Open` (lines 653-656, 686-696):
```csharp
var role = await GetEffectiveRoleAsync();
if (!IsQuestOwner(currentUser, existingQuest.DungeonMaster) && role != GroupRole.Admin)
{
    return Forbid();
}
```

---

### `QuestService.cs` / `QuestRepository.cs` — `UpdateQuestPropertiesWithNotificationsAsync` (service/repository, CRUD)

**No code change required.** The repository impl (lines 188-216) already gates date mutation behind `updateProposedDates && proposedDates != null` (line 207); calling it with `false`/`null` from the finalized-quest Edit path is safe today with zero modification:
```csharp
if (updateProposedDates && proposedDates != null)
{
    affectedPlayerEntities = UpdateProposedDatesWithNotificationTracking(entity, proposedDates);
}
```
Confirms D-02 (no email) automatically: `dispatcher.EnqueueDateChangedEmail` in `QuestService.cs` (lines 155-171) only fires when `affectedPlayers.Count > 0`, which requires the date-update branch to have run — skipped entirely when `updateProposedDates: false`.

---

### `Edit.cshtml` (Razor view, request-response)

**Analog:** own `@if (boardType != BoardType.Campaign)` wrapper (lines 42-103) — the existing mechanism for hiding OneShot-only fields.

**Proposed Dates sub-block to condition** (lines 68-102) — add `&& !IsFinalized` (or `!Model.IsFinalized` depending on where the flag lands, per Claude's Discretion in CONTEXT.md) as an inner condition, leaving CR/TotalPlayerCount/DMSession (lines 44-66) unconditioned by `IsFinalized`:
```cshtml
@if (boardType != BoardType.Campaign)
{
    <div class="mb-3"> <!-- Challenge Rating --> </div>
    <div class="mb-3"> <!-- Total Player Count --> </div>
    <div class="mb-3"> <!-- DM Session checkbox --> </div>

    @if (!Model.IsFinalized)  // new condition, wraps only Proposed Dates
    {
        <div class="mb-3">
            <label class="form-label">Proposed Dates &amp; Times <span class="text-danger">*</span></label>
            @* ... unchanged existing markup, lines 70-101 ... *@
        </div>
    }
}
```

**Sidebar tips block** (lines 118-146) — currently unconditional list of date-editing tips (`Existing dates cannot be edited, only removed`, etc.). Per CONTEXT.md's Claude's Discretion, either add a finalized-state variant or wrap in the same `!Model.IsFinalized` (note: Edit.Mobile.cshtml has **no equivalent tips block at all** — confirmed by full-file read — so if a finalized-state message is added here, no mobile counterpart is needed for the tips card specifically, only for the Proposed Dates form section itself).

---

### `Edit.Mobile.cshtml` (Razor view, request-response)

**Analog:** own `@if (boardType != BoardType.Campaign)` wrapper (lines 56-105) — structurally identical to desktop's, condensed markup (no `form-text` help blocks in some places).

**Proposed Dates sub-block to condition** (lines 80-104) — same treatment as desktop, add `&& !Model.IsFinalized`:
```cshtml
@if (boardType != BoardType.Campaign)
{
    <div class="mb-3"> <!-- Challenge Rating --> </div>
    <div class="mb-3"> <!-- Total Player Count --> </div>
    <div class="mb-3"> <!-- DM Session checkbox --> </div>

    @if (!Model.IsFinalized)  // new condition, mirrors desktop exactly
    {
        <div class="mb-3">
            <label class="form-label">Proposed Dates &amp; Times <span class="text-danger">*</span></label>
            @* ... unchanged existing markup, lines 82-103 ... *@
        </div>
    }
}
```
No tips sidebar exists in this file (confirmed — file ends at line 124 with only the form + Scripts section), so no parallel sidebar change is needed here even if desktop's tips card changes.

---

### `Manage.cshtml` (Razor view, request-response)

**Analog A — button placement/pattern:** finalized-OneShot button row (lines 500-519), the exact insertion point per D-04:
```cshtml
<div class="d-flex justify-content-between align-items-center">
    <div class="d-flex gap-2">
        <form asp-action="Open" method="post" style="display: inline;">
            <input type="hidden" name="id" value="@Model.Id" />
            <button type="submit" class="btn btn-warning" onclick="return confirm('...');">Open Quest</button>
        </form>
        @if (Model.FollowUpQuest == null)
        {
            <a href="@Url.Action("CreateFollowUp", "Quest", new { id = Model.Id })" class="btn btn-primary">
                <i class="fas fa-scroll me-2"></i>Create Follow-Up Quest
            </a>
        }
        <form asp-action="SendReminder" asp-route-id="@Model.Id" method="post">
            @Html.AntiForgeryToken()
            <button type="submit" class="btn btn-info">
                <i class="fas fa-envelope me-1"></i>Send Reminder
            </button>
        </form>
        <!-- INSERT: new Edit Quest link here -->
    </div>
    <button type="button" class="btn btn-secondary" onclick="window.location.reload()">Refresh Data</button>
</div>
```

**Analog B — exact link markup to copy** (pre-finalize empty-state `Edit Quest` link, lines 108-111 — link-based, no confirm dialog, no antiforgery token needed since it's a GET navigation not a POST):
```cshtml
<a href="@Url.Action("Edit", "Quest", new { id = Model.Id })" class="btn btn-primary">
    <i class="fas fa-edit me-1"></i>Edit Quest
</a>
```
Combining A's insertion point with B's markup gives the new button for the finalized-OneShot row.

---

### `Manage.Mobile.cshtml` (Razor view, request-response)

**Analog A — button placement/pattern:** `Open`/`CreateFollowUp`/`Refresh` row (lines 120-139), `flex-wrap gap-2` container, `flex-fill` buttons:
```cshtml
<div class="d-flex flex-wrap gap-2 mt-3">
    <form asp-action="Open" method="post" class="flex-fill">
        <input type="hidden" name="id" value="@Model.Id" />
        <button type="submit" class="btn btn-warning w-100" onclick="return confirm('...');">
            <i class="fas fa-unlock me-1"></i>Open Quest
        </button>
    </form>
    @if (Model.FollowUpQuest == null)
    {
        <a href="@Url.Action("CreateFollowUp", "Quest", new { id = Model.Id })" class="btn btn-primary flex-fill">
            <i class="fas fa-scroll me-1"></i>Create Follow-Up
        </a>
    }
    <!-- INSERT: new Edit Quest link here, flex-fill to match siblings -->
    <button type="button" class="btn btn-outline-secondary w-100" onclick="window.location.reload()">
        <i class="fas fa-sync me-1"></i>Refresh Data
    </button>
</div>
```

**Analog B — exact link markup to copy** (pre-finalize empty-state `Edit Quest` link, lines 108-111, mobile file):
```cshtml
<a href="@Url.Action("Edit", "Quest", new { id = Model.Id })" class="btn btn-primary">
    <i class="fas fa-edit me-1"></i>Edit Quest
</a>
```
Note mobile's sibling buttons in this row use `flex-fill` (not present in B's copy) — new button should add `flex-fill` to match the row's sibling buttons, consistent with how `CreateFollowUp`'s mobile variant (line 131) adds `flex-fill` to the same desktop markup that lacks it (line 508-509 desktop has no `flex-fill`, its mobile counterpart at 130-131 does).

## Shared Patterns

### ModelState validation errors (controller-level)
**Source:** `QuestController.cs` `Create` POST, lines 99-102
**Apply to:** `Edit` POST's new D-01 Total Player Count guard
```csharp
ModelState.AddModelError(nameof(viewModel.ProposedDates), "At least one proposed date is required.");
```
This is the established precedent per CONTEXT.md D-04/Claude's Discretion: controller-level `ModelState.AddModelError`, not a service/repository-layer guard.

### Selected-player-count predicate (existing selections, not posted list)
**Source:** `QuestController.cs` `JoinFinalizedQuest`, line 441-442
**Apply to:** `Edit` POST's D-01 guard (reasoning over `existingQuest.PlayerSignups`, since Edit never receives a posted player list)
```csharp
quest.PlayerSignups.Where(ps => ps.IsSelected && ps.Role == SignupRole.Player).Count()
```
(`Finalize`'s line 663 variant — `selectedPlayerIds.Contains(ps.Id) && ps.Role == SignupRole.Player` — is a weaker match since it reasons over a form-posted list, which Edit does not have.)

### BoardType-conditional field visibility
**Source:** `Edit.cshtml` line 42 / `Edit.Mobile.cshtml` line 56 — `@if (boardType != BoardType.Campaign)`
**Apply to:** Both Edit views' Proposed Dates sub-block, nested one level deeper with the new `!IsFinalized` condition (narrower scope — only Proposed Dates, not the whole OneShot-fields block).

### Authorization (DM-owner-or-Admin), unchanged
**Source:** `QuestController.cs`, identical block in `Edit` GET (155-160), `Edit` POST (210-215), `Finalize` (656), `Open` (694)
```csharp
var role = await GetEffectiveRoleAsync();
if (!IsQuestOwner(currentUser, existingQuest.DungeonMaster) && role != GroupRole.Admin)
{
    return Forbid();
}
```
No changes needed — already correct for this phase.

### `updateProposedDates` boolean as the "skip dates" seam
**Source:** `QuestService.cs` lines 133-153, `QuestRepository.cs` lines 188-216
**Apply to:** `Edit` POST's call site — this parameter exists precisely so a caller can update quest properties without touching dates; Edit's finalized-quest path is the first caller to pass `false` conditionally instead of always `true`.

## No Analog Found

None — every touched file supplies its own precedent from a nearby block in the same file. No cross-codebase search for external analogs was needed since CONTEXT.md already pinpointed exact line ranges via direct code inspection.

## Metadata

**Analog search scope:** `QuestBoard.Service/Controllers/QuestBoard/QuestController.cs`, `QuestBoard.Domain/Services/QuestService.cs`, `QuestBoard.Repository/QuestRepository.cs`, `QuestBoard.Service/Views/Quest/Edit.cshtml`, `QuestBoard.Service/Views/Quest/Edit.Mobile.cshtml`, `QuestBoard.Service/Views/Quest/Manage.cshtml`, `QuestBoard.Service/Views/Quest/Manage.Mobile.cshtml`, `QuestBoard.Service/ViewModels/QuestViewModels/EditQuestViewModel.cs`, `QuestBoard.Service/ViewModels/QuestViewModels/QuestViewModel.cs`
**Files scanned:** 9 (all fully or targeted-range read; no file exceeded 2,000 lines)
**Pattern extraction date:** 2026-07-07
