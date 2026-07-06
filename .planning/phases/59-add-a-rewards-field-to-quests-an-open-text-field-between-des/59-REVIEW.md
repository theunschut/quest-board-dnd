---
phase: 59-add-a-rewards-field-to-quests-an-open-text-field-between-des
reviewed: 2026-07-06T00:00:00Z
depth: standard
files_reviewed: 20
files_reviewed_list:
  - QuestBoard.Repository/Entities/QuestEntity.cs
  - QuestBoard.Domain/Models/QuestBoard/Quest.cs
  - QuestBoard.Service/ViewModels/QuestViewModels/QuestViewModel.cs
  - QuestBoard.Service/ViewModels/QuestViewModels/FollowUpQuestViewModel.cs
  - QuestBoard.Domain/Interfaces/IQuestService.cs
  - QuestBoard.Domain/Interfaces/IQuestRepository.cs
  - QuestBoard.Domain/Services/QuestService.cs
  - QuestBoard.Repository/QuestRepository.cs
  - QuestBoard.Service/Controllers/QuestBoard/QuestController.cs
  - QuestBoard.Repository/Migrations/QuestBoardContextModelSnapshot.cs
  - QuestBoard.Repository/Migrations/20260706194635_AddRewardsToQuest.cs
  - QuestBoard.Repository/Migrations/20260706194635_AddRewardsToQuest.Designer.cs
  - QuestBoard.UnitTests/Services/QuestServiceTests.cs
  - QuestBoard.UnitTests/Services/EmailConfirmationJobGuardTests.cs
  - QuestBoard.Service/Views/Quest/Create.cshtml
  - QuestBoard.Service/Views/Quest/Create.Mobile.cshtml
  - QuestBoard.Service/Views/Quest/Edit.cshtml
  - QuestBoard.Service/Views/Quest/Edit.Mobile.cshtml
  - QuestBoard.Service/Views/Quest/CreateFollowUp.cshtml
  - QuestBoard.Service/Views/Quest/Details.cshtml
  - QuestBoard.Service/Views/Quest/Details.Mobile.cshtml
  - QuestBoard.Service/Views/QuestLog/Details.cshtml
findings:
  critical: 0
  warning: 2
  info: 3
  total: 5
status: issues_found
---

# Phase 59: Code Review Report

**Reviewed:** 2026-07-06
**Depth:** standard
**Files Reviewed:** 20 (+2 test files)
**Status:** issues_found

## Summary

This phase adds a nullable, unbounded `Rewards` text field to `Quest` end-to-end: entity → domain model → both view models → repository/service update paths → migration → six Razor views. The plumbing is mostly correct: `Rewards` maps by AutoMapper convention (no explicit ignore/override needed since property names match on both `CreateMap<QuestViewModel, Quest>()`/`CreateMap<Quest, QuestViewModel>()`), the migration is a simple additive nullable column matching the entity, and the `UpdateQuestPropertiesWithNotificationsAsync` chain forwards the value faithfully from controller → service → repository → `SaveChangesAsync`.

The one real correctness gap is in `CreateFollowUp` (GET): every other pre-fillable field (Title, Description, ChallengeRating, TotalPlayerCount, DungeonMasterId) is copied from the original quest into the follow-up form, but `Rewards` is left out, so the DM sees a blank Rewards box despite the "pre-filled from the original quest" banner promising otherwise. There's also a leftover dead method in `QuestRepository` (`UpdateProposedDatesIntelligently`) that appears unrelated to this phase but sits directly next to the method this phase's tests exercise (`UpdateProposedDatesWithNotificationTracking`) and should be cleaned up while this area is being touched. No security issues found — Rewards is rendered through standard Razor `@` interpolation (auto-encoded) in every view, so no XSS exposure, and there is no length/DB mismatch since both the view models and entity leave the field unbounded (`nvarchar(max)`), matching `Description`'s existing precedent.

Test coverage for the new field is thin: exactly one test (`UpdateQuestPropertiesWithNotificationsAsync_WithRewards_ForwardsExactRewardsValueToRepository`) exercises `Rewards`, and it only checks the edit/update path. There is no test for `CreateFollowUpQuestWithDetailsAsync` forwarding `Rewards`, and no model-level test for `Quest.Rewards` at all (`QuestModelTests.cs` was not updated).

## Warnings

### WR-01: CreateFollowUp GET does not pre-fill Rewards from the original quest

**File:** `QuestBoard.Service/Controllers/QuestBoard/QuestController.cs:907-918`
**Issue:** The `CreateFollowUp` GET action builds the `FollowUpQuestViewModel` by copying `Title`, `Description`, `ChallengeRating`, `TotalPlayerCount`, and `DungeonMasterId` from the original quest, but omits `Rewards`. The view's info banner ("This form is pre-filled from the original quest...") and the fact every sibling field is copied strongly implies this was meant to carry over too — as written, a DM creating a follow-up quest for, e.g., a multi-part campaign arc loses the previous rewards text and has to retype it (or it silently stays blank if they don't notice). This is inconsistent with the rest of the pre-fill behavior added in this same view model.
**Fix:**
```csharp
var viewModel = new FollowUpQuestViewModel
{
    OriginalQuestId = original.Id,
    Title = $"{original.Title} - Part 2",
    Description = original.Description,
    Rewards = original.Rewards,
    ChallengeRating = original.ChallengeRating,
    TotalPlayerCount = original.TotalPlayerCount,
    DungeonMasterId = original.DungeonMasterId,
    DungeonMasterSession = false,
    ProposedDates = [],   // always empty
};
```
If the omission is intentional (e.g., rewards shouldn't roll over because a new part implies new loot), update the info banner copy in `CreateFollowUp.cshtml:23-26` to clarify that Rewards is excluded from the pre-fill, so the behavior isn't just a silent surprise.

### WR-02: Dead method `UpdateProposedDatesIntelligently` left in QuestRepository

**File:** `QuestBoard.Repository/QuestRepository.cs:279-309`
**Issue:** `UpdateProposedDatesIntelligently` is never called anywhere in the codebase — it was superseded by `UpdateProposedDatesWithNotificationTracking` (lines 311-351), which is nearly identical but additionally tracks affected players for the date-changed email. The two methods duplicate the same date-matching/add/remove logic, and the older one is confusing dead weight for anyone reading this file, especially since this phase's changes sit in the same class and one might reasonably assume both are still in use.
**Fix:** Remove `UpdateProposedDatesIntelligently` (and the private helper `IsSameDateTime` usage there is shared, so keep `IsSameDateTime`). If the dead method is truly unrelated to this phase's scope, flag it for removal in a follow-up cleanup rather than leaving it as-is:
```csharp
// Delete lines 279-309 (UpdateProposedDatesIntelligently) — UpdateProposedDatesWithNotificationTracking
// is a strict superset of its behavior and is the only method still called.
```

## Info

### IN-01: No test coverage for Rewards on the follow-up-quest creation path

**File:** `QuestBoard.UnitTests/Services/QuestServiceTests.cs`
**Issue:** Only `UpdateQuestPropertiesWithNotificationsAsync_WithRewards_ForwardsExactRewardsValueToRepository` exercises the new `Rewards` parameter, and it only covers the quest-edit path. `CreateFollowUpQuestWithDetailsAsync` also takes a `rewards` parameter and forwards it via the same `UpdateQuestPropertiesWithNotificationsAsync` call, but no test asserts that. `QuestModelTests.cs` (domain model tests) also has no assertion that `Quest.Rewards` round-trips.
**Fix:** Add a test similar to the existing one, but calling `CreateFollowUpQuestWithDetailsAsync` and asserting the repository receives the exact rewards string, plus a trivial `Quest_ShouldSetProperties`-style assertion for `Rewards` in `QuestModelTests.cs`.

### IN-02: Inconsistent whitespace preservation between Description and Rewards in QuestLog details

**File:** `QuestBoard.Service/Views/QuestLog/Details.cshtml:44-60`
**Issue:** The `Rewards` box uses `style="white-space: pre-wrap;"` (line 59) but the `Description` box directly above it does not (lines 47-49). Both are free-text fields a DM might format with line breaks, so a DM who writes a multi-line description will see it collapsed to one line while multi-line rewards render as intended — a visual inconsistency introduced by only adding `pre-wrap` to the new field instead of matching the existing one.
**Fix:** Add `style="white-space: pre-wrap;"` to the Description box in this view for consistency (or drop it from Rewards if Description's collapsed rendering is intentional):
```html
<div class="quest-description-box" style="white-space: pre-wrap;">
    @Model.Quest.Description
</div>
```

### IN-03: TotalPlayerCount has no [Range] validation on QuestViewModel despite the same field being range-validated on FollowUpQuestViewModel

**File:** `QuestBoard.Service/ViewModels/QuestViewModels/QuestViewModel.cs:23`
**Issue:** Not introduced by this phase, but noticed while reviewing the file: `QuestViewModel.TotalPlayerCount` has no `[Required]`/`[Range]` attribute, while the newer `FollowUpQuestViewModel.TotalPlayerCount` (also touched by this phase for the `Rewards` field) has `[Required][Range(1, 20, ...)]`. A `Create`/`Edit` POST could submit a negative or zero player count for a one-shot quest with no server-side validation error, only silently producing a quest with an unusable capacity (e.g., `TotalPlayerCount = -1` breaks the `selectedCount < quest.TotalPlayerCount` capacity checks in `QuestService.ChangeVoteAsync` and `QuestController.JoinFinalizedQuest`, potentially blocking all joins).
**Fix:** Align the two view models:
```csharp
[Required]
[Range(1, 20, ErrorMessage = "Player count must be between 1 and 20.")]
public int TotalPlayerCount { get; set; } = 6;
```

---

_Reviewed: 2026-07-06_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
