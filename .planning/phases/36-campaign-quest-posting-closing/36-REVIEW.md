---
phase: 36-campaign-quest-posting-closing
reviewed: 2026-07-03T16:52:48+02:00
depth: standard
files_reviewed: 26
files_reviewed_list:
  - QuestBoard.Domain/Interfaces/IQuestRepository.cs
  - QuestBoard.Domain/Interfaces/IQuestService.cs
  - QuestBoard.Domain/Models/QuestBoard/Quest.cs
  - QuestBoard.Domain/Services/QuestService.cs
  - QuestBoard.IntegrationTests/Controllers/QuestCloseTests.cs
  - QuestBoard.IntegrationTests/Helpers/TestDataHelper.cs
  - QuestBoard.Repository/Entities/QuestEntity.cs
  - QuestBoard.Repository/Migrations/20260703135517_AddQuestCloseFields.Designer.cs
  - QuestBoard.Repository/Migrations/20260703135517_AddQuestCloseFields.cs
  - QuestBoard.Repository/Migrations/QuestBoardContextModelSnapshot.cs
  - QuestBoard.Repository/QuestRepository.cs
  - QuestBoard.Service/Controllers/QuestBoard/QuestController.cs
  - QuestBoard.Service/Controllers/QuestBoard/QuestLogController.cs
  - QuestBoard.Service/ViewModels/QuestViewModels/QuestViewModel.cs
  - QuestBoard.Service/Views/Quest/Create.Mobile.cshtml
  - QuestBoard.Service/Views/Quest/Create.cshtml
  - QuestBoard.Service/Views/Quest/Details.Mobile.cshtml
  - QuestBoard.Service/Views/Quest/Details.cshtml
  - QuestBoard.Service/Views/Quest/Index.Mobile.cshtml
  - QuestBoard.Service/Views/Quest/Index.cshtml
  - QuestBoard.Service/Views/Quest/Manage.Mobile.cshtml
  - QuestBoard.Service/Views/Quest/Manage.cshtml
  - QuestBoard.Service/Views/QuestLog/Details.Mobile.cshtml
  - QuestBoard.Service/Views/QuestLog/Details.cshtml
  - QuestBoard.Service/Views/QuestLog/Index.Mobile.cshtml
  - QuestBoard.Service/Views/QuestLog/Index.cshtml
  - QuestBoard.UnitTests/Services/QuestServiceTests.cs
findings:
  critical: 3
  warning: 4
  info: 2
  total: 9
status: issues_found
---

# Phase 36: Code Review Report

**Reviewed:** 2026-07-03T16:52:48+02:00
**Depth:** standard
**Files Reviewed:** 26
**Status:** issues_found

## Summary

This phase adds `IsClosed`/`ClosedDate` fields and Close/Reopen actions for campaign-board quests, and threads `BoardType` awareness through the Quest and QuestLog controllers/views. The migration and entity/model wiring for the new fields is clean and consistent. However, the review surfaced a critical authorization gap: `Close`/`Reopen`/`Finalize` and — more seriously — `Edit` never validate the quest's board type server-side, even though `Create` explicitly sanitizes board-type-inappropriate fields. Because `Edit` accepts raw `DungeonMasterSession`/`ChallengeRating`/`TotalPlayerCount` from the posted form with no override, a DM can set `DungeonMasterSession=true` on a Campaign-board quest via a direct POST, which combines with a real filter gap in `GetCompletedQuestsAsync` (`|| q.IsClosed` bypasses the `!DungeonMasterSession` check) to leak DM-only campaign quests into the public Quest Log once closed. A pre-existing, unrelated-to-this-phase but still-live defect in `CreateFollowUpQuestAsync` (never sets `GroupId` on the new quest) is also flagged since the file is in scope and the bug is real and currently shipping.

## Critical Issues

### CR-01: Close/Reopen (and Finalize) never verify the quest's BoardType server-side

**File:** `QuestBoard.Service/Controllers/QuestBoard/QuestController.cs:694-754`
**Issue:** `Close(int id)` and `Reopen(int id)` are gated only by ownership/admin checks (`IsQuestOwner` / `GroupRole.Admin`). Neither the controller, `QuestService`, nor `QuestRepository` ever checks `BoardType` before performing the close/reopen. The UI only renders the Close/Reopen buttons `@if (boardType == BoardType.Campaign)` (`Manage.cshtml:538`), but that is purely a rendering guard — a DM can `POST /Quest/Close/{id}` (or `/Quest/Reopen/{id}`) for a one-shot quest they own directly (e.g. via curl/devtools) and it will succeed, setting `IsClosed=true` on a quest type the feature was never designed for. This then causes that quest to be picked up by `GetCompletedQuestsAsync`'s unconditional `|| q.IsClosed` branch (see WR-01) and appear in the Quest Log with `ClosedDate` populated but `FinalizedDate` unset, producing an inconsistent, never-tested state.
**Fix:** Validate board type server-side before performing the close/reopen, mirroring the pattern already used in `Create`:
```csharp
[HttpPost]
[ValidateAntiForgeryToken]
[Authorize(Policy = "DungeonMasterOnly")]
public async Task<IActionResult> Close(int id)
{
    var quest = await questService.GetQuestWithDetailsAsync(id);
    if (quest == null || quest.IsClosed) return NotFound();

    var boardType = await GetActiveBoardTypeAsync();
    if (boardType != BoardType.Campaign) return BadRequest("Close is only supported for campaign quests.");
    // ... existing authorization + CloseQuestAsync call
}
```
Apply the same guard to `Reopen`.

### CR-02: `Edit` POST accepts DungeonMasterSession/ChallengeRating/TotalPlayerCount unsanitized for Campaign quests

**File:** `QuestBoard.Service/Controllers/QuestBoard/QuestController.cs:188-247`
**Issue:** `Create` POST explicitly overrides `ProposedDates`, `ChallengeRating`, `TotalPlayerCount`, and `DungeonMasterSession` to fixed defaults when `boardType == BoardType.Campaign` (lines 103-111), because the Create view hides those fields for Campaign boards and the server must never trust client-side hiding. `Edit` POST has no equivalent branch: it passes `viewModel.Quest.DungeonMasterSession`, `viewModel.Quest.ChallengeRating`, and `viewModel.Quest.TotalPlayerCount` straight through to `UpdateQuestPropertiesWithNotificationsAsync` (lines 234-244) regardless of board type. Since `Edit.cshtml` (not itself in this phase's file list) was never updated to hide these fields for Campaign quests either, this is reachable through the normal Edit form as well as a crafted POST. Setting `DungeonMasterSession=true` on a Campaign quest is the trigger for the Quest Log leak described in WR-01.
**Fix:** Mirror the `Create` sanitization in `Edit` POST:
```csharp
var boardType = await GetActiveBoardTypeAsync(token);
if (boardType == BoardType.Campaign)
{
    viewModel.Quest.ChallengeRating = 1;
    viewModel.Quest.TotalPlayerCount = 0;
    viewModel.Quest.DungeonMasterSession = false;
    viewModel.Quest.ProposedDates = [];
}
```
placed before the `UpdateQuestPropertiesWithNotificationsAsync` call.

### CR-03: `CreateFollowUpQuestAsync` never sets `GroupId` on the new quest (pre-existing, still live)

**File:** `QuestBoard.Domain/Services/QuestService.cs:218-228`
**Issue:** The follow-up `Quest` object built in `CreateFollowUpQuestAsync` copies `Title`, `Description`, `ChallengeRating`, `TotalPlayerCount`, `DungeonMasterId`, `DungeonMasterSession`, `ProposedDates`, and `OriginalQuestId` from the original quest, but never sets `GroupId`. `Quest.GroupId` defaults to `0`. `QuestEntity.GroupId` has a required FK to `GroupEntity` with `OnDelete(DeleteBehavior.NoAction)` (`QuestBoardContext.cs`), and no `Group` with `Id == 0` exists in any real deployment (identity columns start at 1), so `repository.AddAsync(followUp, token)` will throw a `DbUpdateException` (FK violation) for essentially every call. This means `CreateFollowUp` (GET and POST), `CreateFollowUpQuestAsync`, and `CreateFollowUpQuestWithDetailsAsync` are broken end-to-end in the current codebase. `git blame` traces this to phase 34.2, not to this phase's commits, but the file is in scope for this review and the defect directly affects a currently-shipping code path (`Manage.cshtml:520-526` and `Manage.Mobile.cshtml:138-144` both link to it).
**Fix:**
```csharp
var followUp = new Quest
{
    Title = $"{original.Title} - Part 2",
    Description = original.Description,
    ChallengeRating = original.ChallengeRating,
    TotalPlayerCount = original.TotalPlayerCount,
    DungeonMasterId = original.DungeonMasterId,
    DungeonMasterSession = false,
    GroupId = original.GroupId,   // <-- must inherit the original quest's group
    ProposedDates = [],
    OriginalQuestId = original.Id,
};
```

## Warnings

### WR-01: `GetCompletedQuestsAsync` lets closed quests bypass the DM-only-session filter

**File:** `QuestBoard.Domain/Services/QuestService.cs:176-188`
**Issue:** The finalized-quest branch correctly excludes DM-only sessions (`&& !q.DungeonMasterSession`), but the closed-quest branch is OR'd in unconditionally: `... || q.IsClosed`. Today this is masked because `Create` always forces `DungeonMasterSession = false` for Campaign quests — but as shown in CR-02, `Edit` does not enforce the same invariant, so a DM-only campaign quest can exist and, once closed, will appear in the public Quest Log (`QuestLogController.Index`/`Details`) despite the comment on `QuestLogController.Details:42` explicitly stating "DM-only sessions are not shown in the quest log." The same gap exists in `QuestLogController.Details` (`isCompletedOneShot` also omits any DungeonMasterSession check for the `IsClosed` branch, though that's consistent with this service method).
**Fix:** Add the same guard to the closed branch:
```csharp
return quests
    .Where(q => (q.IsFinalized
                 && q.FinalizedDate.HasValue
                 && q.FinalizedDate.Value.Date <= DateTime.UtcNow.AddDays(-1).Date
                 && !q.DungeonMasterSession)
                || (q.IsClosed && !q.DungeonMasterSession))
    .OrderByDescending(q => q.IsClosed ? q.ClosedDate : q.FinalizedDate)
    .ToList();
```
This should be paired with fixing CR-02 so `DungeonMasterSession` can never actually become true for a Campaign quest in the first place.

### WR-02: `UpdateRecap`'s completed-quest check diverges from `Details`'s

**File:** `QuestBoard.Service/Controllers/QuestBoard/QuestLogController.cs:91-96`
**Issue:** `Details` GET computes `isCompletedOneShot` including `&& !quest.DungeonMasterSession` (line 46), matching the stated intent that DM-only sessions never appear in the Quest Log. `UpdateRecap` POST computes the same-named local with the exact same date logic but omits the `DungeonMasterSession` check (lines 91-92). This means a DM can successfully call `UpdateRecap` on a finalized DM-only-session quest that `Details` would otherwise 404 for everyone (including that DM), silently writing a recap that can never be viewed through the normal Quest Log UI. Low blast radius (self-write on an own quest, gated by `DungeonMasterOnly` + ownership check), but it is dead/inconsistent validation logic that will confuse the next person who has to reconcile the two methods.
**Fix:** Copy the same DungeonMasterSession exclusion into `UpdateRecap`'s `isCompletedOneShot` computation so both methods agree on what "completed" means.

### WR-03: Broad "unique" substring match in `QuestRepository.AddAsync` exception filter

**File:** `QuestBoard.Repository/QuestRepository.cs:19-30`
**Issue:** The `catch (DbUpdateException ex) when (... || ex.InnerException?.Message.Contains("unique") == true)` clause is intended to catch the `IX_Quests_OriginalQuestId` unique-index violation and re-throw it as a friendly `InvalidOperationException`. The fallback `.Contains("unique")` check is much broader than the specific index name and will also swallow/rewrite any other unique-constraint violation raised while inserting a `Quest` (e.g. if a future migration adds another unique index to the `Quests` table), replacing the real underlying error with the misleading message "A follow-up quest already exists for this quest." This will misdirect debugging effort if it ever fires for an unrelated constraint.
**Fix:** Narrow the fallback to check for the specific SQL Server unique-violation error number (2601/2627) combined with the known index/constraint name, or drop the generic `"unique"` fallback entirely and rely solely on the `IX_Quests_OriginalQuestId` substring match.

### WR-04: `IsQuestOwner`-style DM checks rely on Id, but Index/QuestLog Index views compare DM by Name for navigation

**File:** `QuestBoard.Service/Views/Quest/Index.cshtml:82`, `QuestBoard.Service/Views/Quest/Index.Mobile.cshtml:33`
**Issue:** Both Index views decide whether to route a card click to `Manage` (DM view) or `Details` (player view) using `ViewBag.CurrentUserName == quest.DungeonMaster?.Name` — a name comparison, not an Id comparison. Two different users with the same display `Name` (nothing in the codebase enforces uniqueness on `User.Name`) will incorrectly route to each other's Manage page navigation target (though the controller-side `Manage` action still independently re-checks `IsQuestOwner`/Admin and would `NotFound`/deny access, so this is a UX/navigation defect rather than an authorization bypass). This pattern is unrelated to `IsClosed`/`BoardType` but sits directly in files touched by this phase's `boardType` conditionals, so it's called out for awareness even though the underlying comparison predates this phase.
**Fix:** Pass `CurrentUserId` through instead and compare `currentUserId == quest.DungeonMaster?.Id`, consistent with the `IsQuestOwner` helper already used server-side.

## Info

### IN-01: Magic "one day" completion window duplicated across five files

**File:** `QuestBoard.Domain/Services/QuestService.cs:183`, `QuestBoard.Service/Controllers/QuestBoard/QuestLogController.cs:45,92`, `QuestBoard.Service/Views/Quest/Manage.cshtml:592`, `QuestBoard.Service/Views/Quest/Details.cshtml:645`, `QuestBoard.Service/Views/Quest/Index.Mobile.cshtml:57`
**Issue:** The `FinalizedDate.Value.Date <= DateTime.UtcNow.AddDays(-1).Date` expression (or its mirror `... > oneDayAgo` in the repository) is repeated verbatim across service, controller, and multiple Razor views with no shared constant or helper. Any future change to the "how many days until a quest is considered done" business rule requires hunting down every occurrence.
**Fix:** Extract a single `Quest.IsDone` computed property or a small extension method (e.g. `quest.IsCompletedOneShot()`) in the Domain layer and reuse it everywhere instead of re-deriving the date arithmetic per call site.

### IN-02: `QuestViewModel.TotalPlayerCount` has no `[Range]`/`[Required]` validation unlike `FollowUpQuestViewModel`

**File:** `QuestBoard.Service/ViewModels/QuestViewModels/QuestViewModel.cs:21`
**Issue:** `FollowUpQuestViewModel.TotalPlayerCount` (a sibling view model, not in this review's scope but instructive) carries `[Required][Range(1, 20, ...)]`, while `QuestViewModel.TotalPlayerCount` has no data annotations at all. For one-shot quest Create/Edit, this means a negative or zero `TotalPlayerCount` can be submitted and will pass `ModelState.IsValid`, only to produce a nonsensical "0 of -5 players" experience downstream in `Manage.cshtml`/`Details.cshtml` player-count displays. Not a regression introduced by this phase, but worth tightening while these view models are being touched for `BoardType` awareness.
**Fix:** Add `[Range(1, 20, ErrorMessage = "Player count must be between 1 and 20.")]` to `QuestViewModel.TotalPlayerCount` for parity with `FollowUpQuestViewModel`.

---

_Reviewed: 2026-07-03T16:52:48+02:00_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
