---
phase: 93-per-board-page-selection-and-home-page
fixed_at: 2026-10-21T00:00:00Z
review_path: .planning/phases/93-per-board-page-selection-and-home-page/93-REVIEW.md
iteration: 1
findings_in_scope: 2
fixed: 2
skipped: 0
status: all_fixed
---

# Phase 93: Code Review Fix Report

**Fixed at:** 2026-10-21
**Source review:** .planning/phases/93-per-board-page-selection-and-home-page/93-REVIEW.md
**Iteration:** 1

**Summary:**
- Findings in scope: 2
- Fixed: 2
- Skipped: 0

Verification ran in the isolated review-fix worktree, not the main checkout: the full `dotnet test`
(unit 1300 passed, integration 1652 passed, 11 skipped, 0 failed) was run there after both commits.
The worktree is fast-forwarded into the milestone branch and removed, so the numbers are
reproducible by running `dotnet test` in the main checkout.

## Fixed Issues

### WR-01: Platform Edit can silently overwrite a concurrent page change, and its "live content" guards are check-then-act

**Files modified:** `QuestBoard.Service/Areas/Platform/Controllers/GroupController.cs`, `QuestBoard.Service/ViewModels/PlatformViewModels/GroupEditViewModel.cs`, `QuestBoard.Service/Areas/Platform/Views/Group/Edit.cshtml`, `QuestBoard.Service/Areas/Platform/Views/Group/Edit.Mobile.cshtml`, `QuestBoard.Domain/Services/BoardPageSettingsService.cs`, `QuestBoard.UnitTests/Controllers/GroupControllerEditConcurrencyTests.cs`
**Commit:** 1ce594cd
**Status:** fixed: requires human verification (concurrency logic; covered by unit tests, but the two-admin scenario was not exercised against a real database)
**Applied fix:**
- Stale-form overwrite (closed): the edit form now carries two hidden fields, `RenderedDisabledPages` (the mask) and `RenderedHomePage`, stamped from the board as stored on every render. `Edit` POST compares them with the freshly loaded board before doing anything else. On a mismatch it saves nothing, runs no counts, adds the existing "Someone else changed this board while you were editing. Reload the page and try again." error, and re-renders the form from the stored settings (the posted home page selection is cleared so the stored one shows). The existing `DbUpdateConcurrencyException` path reuses the same message through a shared constant and also stamps the stored state. A form that does not carry the fields (an old client posting only Id and Name) skips the check, which matches the existing "unposted keeps stored value" contract.
- Chosen approach and why: a posted-version comparison rather than an EF concurrency token. It needs no schema change or migration, and a token on `DisabledPages` read from the same load could not detect a stale form anyway (the review's own observation).
- Check-then-act window (accepted residual, documented): the live-quest, upcoming-event and active-series counts run before the save, and a DM creating content in that gap leaves it behind the switched-off page. I did not wrap the counts and the save in a serializable transaction: the counts live in the quest and event repositories and the save in the group repository, each on its own unit of work, so a transaction would mean new cross-repository plumbing for a window of milliseconds on a super-admin-only action. The content is only hidden and returns when the page is switched back on. The reasoning is recorded as a comment in `BoardPageSettingsService.EvaluateAsync`.
- Tests added: stale mask refused without save or evaluation and showing the stored settings; stale home page refused without save; matching tokens still save.

### WR-02: Docstring says the live-quest count uses "the same rule the quest board uses", but the list uses a different cutoff

**Files modified:** `QuestBoard.Domain/Interfaces/IQuestRepository.cs`, `QuestBoard.Domain/Services/BoardPageSettingsService.cs`, `QuestBoard.Repository/QuestRepository.cs`, `QuestBoard.UnitTests/Repository/BoardPageCountQueryTests.cs`, `QuestBoard.UnitTests/Services/BoardPageSettingsServiceTests.cs`
**Commit:** d75a83e5
**Status:** fixed: requires human verification (the rule for what blocks the Quests switch changed)
**Applied fix:** I aligned the count with the list rather than correcting the comment, because the guard's contract is that no quest the board still shows is stranded. `CountLiveForBoardAsync` now takes the board's current time (`DateTime boardNow`, supplied from `IBoardClock.Now` by the service, so no ambient clock read was added and `AmbientClockSeamTests` is untouched) and counts a quest as live when it is not closed and is either not finalized, finalized with no date (kept from before, counted as live), or finalized later than `boardNow - 1 day`, which is the cutoff the two list queries use. The interface docstring and the repository comment now describe that rule. Tests: the boundary fixture gained a quest finalized the evening before (now counted) and one finalized the morning before, earlier than the board's time of day (not counted); the old has-been-played comparison test was replaced by one that reads the quest list as the board itself and asserts the count equals the number of listed quests, so the two cannot drift again.

---

_Fixed: 2026-10-21_
_Fixer: Claude (gsd-code-fixer)_
_Iteration: 1_
