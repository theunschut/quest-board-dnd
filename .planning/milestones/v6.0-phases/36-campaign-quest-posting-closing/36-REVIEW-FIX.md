---
phase: 36-campaign-quest-posting-closing
fixed_at: 2026-07-03T17:07:52+02:00
review_path: .planning/phases/36-campaign-quest-posting-closing/36-REVIEW.md
iteration: 1
findings_in_scope: 7
fixed: 7
skipped: 0
status: all_fixed
---

# Phase 36: Code Review Fix Report

**Fixed at:** 2026-07-03T17:07:52+02:00
**Source review:** .planning/phases/36-campaign-quest-posting-closing/36-REVIEW.md
**Iteration:** 1

**Summary:**
- Findings in scope: 7 (critical_warning scope: CR-01, CR-02, CR-03, WR-01, WR-02, WR-03, WR-04)
- Fixed: 7
- Skipped: 0

## Fixed Issues

### CR-01: Close/Reopen (and Finalize) never verify the quest's BoardType server-side

**Files modified:** `QuestBoard.Service/Controllers/QuestBoard/QuestController.cs`, `QuestBoard.IntegrationTests/Controllers/QuestCloseTests.cs`
**Commits:** `9f45ed4`, `8ad6799`
**Applied fix:** Added a server-side `GetActiveBoardTypeAsync()` check to both `Close(int id)` and `Reopen(int id)` that rejects the request with `400 BadRequest` unless the active board resolves to `BoardType.Campaign`, mirroring the pattern already used in `Create`. This closes the gap where a DM could `POST /Quest/Close/{id}` or `/Quest/Reopen/{id}` for a one-shot quest directly.

Applying this fix surfaced that the three existing `QuestCloseTests` (`Close_OwningDm_RedirectsToManage_AndClosesQuest`, `Reopen_OwningDm_RedirectsToManage_AndReopensQuest`, `Close_NonOwnerNonAdmin_IsForbidden_AndQuestRemainsOpen`) exercised the default group (Id=1), which resolves to `BoardType.OneShot` — so they started failing with the newly-correct `400 BadRequest` once the authorization gap was closed. These tests were updated (following the existing `Campaign_Create_WithNoProposedDates_Persists` pattern already in the same file) to seed a campaign-board group (Id=2), grant the relevant users `UserGroups` membership in it, and set `factory.TestGroupContext.ActiveGroupId = 2` for the duration of each test. All 5 tests in `QuestCloseTests` pass after this update. This test-fixture change is committed separately (`8ad6799`) but is part of completing CR-01 — the source fix is not correct/mergeable without it.

Finalize (`Finalize(int id)`) was not given the same explicit guard: it is only reachable when `SelectedDateId` resolves to one of `quest.ProposedDates`, and Campaign quests always have `ProposedDates = []` (enforced by `Create`'s and now `Edit`'s sanitization, CR-02), so Finalize is already structurally unreachable for Campaign quests. The review's Fix section only provided code for Close/Reopen; no additional Finalize-specific check was added.

### CR-02: `Edit` POST accepts DungeonMasterSession/ChallengeRating/TotalPlayerCount unsanitized for Campaign quests

**Files modified:** `QuestBoard.Service/Controllers/QuestBoard/QuestController.cs`
**Commit:** `7e24ee2`
**Applied fix:** Added the same board-type sanitization block used in `Create` to `Edit` POST, placed immediately before the `UpdateQuestPropertiesWithNotificationsAsync` call. When the active board resolves to `BoardType.Campaign`, `ChallengeRating`, `TotalPlayerCount`, `DungeonMasterSession`, and `ProposedDates` on the posted view model are overridden with fixed defaults, regardless of what the client submitted.

### CR-03: `CreateFollowUpQuestAsync` never sets `GroupId` on the new quest (pre-existing, still live)

**Files modified:** `QuestBoard.Domain/Services/QuestService.cs`
**Commit:** `9a40944`
**Applied fix:** Added `GroupId = original.GroupId` to the follow-up `Quest` object constructed in `CreateFollowUpQuestAsync`, so the new quest inherits the original quest's group instead of defaulting to `0` (which would violate the required FK to `GroupEntity` and throw `DbUpdateException` on every call).

### WR-01: `GetCompletedQuestsAsync` lets closed quests bypass the DM-only-session filter

**Files modified:** `QuestBoard.Domain/Services/QuestService.cs`
**Commit:** `042ef00`
**Applied fix:** Added `&& !q.DungeonMasterSession` to the `q.IsClosed` branch of the `Where` predicate, so DM-only-session campaign quests are excluded from the Quest Log once closed, matching the existing exclusion already applied to the finalized-quest branch. Verified against the existing `QuestServiceTests` (`GetCompletedQuestsAsync_IncludesClosedCampaignQuest_WithNoNextDayWait`, `GetCompletedQuestsAsync_PreservesOneShotNextDayWait`, `GetCompletedQuestsAsync_OrdersClosedAndFinalizedQuestsTogether_ClosedNotSortedAsNull`) — all pass unchanged since none of them exercise a closed+DM-session combination.

### WR-02: `UpdateRecap`'s completed-quest check diverges from `Details`'s

**Files modified:** `QuestBoard.Service/Controllers/QuestBoard/QuestLogController.cs`
**Commit:** `093d92d`
**Applied fix:** Added `&& !quest.DungeonMasterSession` to `UpdateRecap`'s `isCompletedOneShot` computation, matching `Details`'s equivalent check exactly (including the same explanatory comment), so both methods now agree on what counts as a "completed" quest.

### WR-03: Broad "unique" substring match in `QuestRepository.AddAsync` exception filter

**Files modified:** `QuestBoard.Repository/QuestRepository.cs`
**Commit:** `301bf6a`
**Applied fix:** Removed the generic `ex.InnerException?.Message.Contains("unique") == true` fallback from the `DbUpdateException` filter, leaving only the specific `IX_Quests_OriginalQuestId` substring match (per the review's second suggested option — "drop the generic `unique` fallback entirely"). No test in the codebase asserted on the broad fallback behavior, so this is a safe narrowing.

### WR-04: `IsQuestOwner`-style DM checks rely on Id, but Index/QuestLog Index views compare DM by Name for navigation

**Files modified:** `QuestBoard.Service/Views/Quest/Index.cshtml`, `QuestBoard.Service/Views/Quest/Index.Mobile.cshtml`
**Commit:** `d4ad1c3`
**Applied fix:** Replaced the `ViewBag.CurrentUserName == quest.DungeonMaster?.Name` comparison (used to route card clicks to `Manage` vs `Details`) with an Id-based comparison (`currentUserId.HasValue && currentUserId.Value == quest.DungeonMaster?.Id`) in both the desktop and mobile Index views, consistent with the `IsQuestOwner` helper already used server-side. In `Index.Mobile.cshtml`, the now-unused `currentUserName` local variable was removed since its only use site was the comparison being replaced.

## Skipped Issues

None — all in-scope findings were fixed.

## Notes

- **Out of scope (not fixed in this pass):** IN-01 (magic "one day" completion window duplicated across five files) and IN-02 (`QuestViewModel.TotalPlayerCount` missing `[Range]`/`[Required]` validation) are Info-severity and outside the `critical_warning` fix scope for this run.
- **Full solution build:** `dotnet build` succeeds with 0 errors, 0 warnings across all 6 projects after all fixes.
- **Full solution test run:** `dotnet test` reports 123/123 unit tests passing and 240/241 integration tests passing. The single failure, `AdminControllerIntegrationTests.SendConfirmationEmail_Post_WhenUserUnconfirmed_ShouldRedirectToUsersWithSuccess`, is a pre-existing, order-dependent flake unrelated to phase 36 or to any finding fixed here — it returns `429 TooManyRequests` only when run alongside other tests in its class (shared rate-limiter state), and passes reliably (17/17 and 1/1) when run in isolation, both on this fix branch and on the pre-fix `main` state. This file was last touched by an unrelated prior commit (`acd870d`) and is not part of this phase's file list.

---

_Fixed: 2026-07-03T17:07:52+02:00_
_Fixer: Claude (gsd-code-fixer)_
_Iteration: 1_
