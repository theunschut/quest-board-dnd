---
phase: 44-post-finalization-voting-waitlist-auto-promotion
fixed_at: 2026-07-04T23:39:14Z
review_path: .planning/phases/44-post-finalization-voting-waitlist-auto-promotion/44-REVIEW.md
iteration: 1
findings_in_scope: 5
fixed: 5
skipped: 0
status: all_fixed
---

# Phase 44: Code Review Fix Report

**Fixed at:** 2026-07-04T23:39:14Z
**Source review:** .planning/phases/44-post-finalization-voting-waitlist-auto-promotion/44-REVIEW.md
**Iteration:** 1

**Summary:**
- Findings in scope: 5 (2 critical, 3 warning; fix_scope=critical_warning, Info findings IN-01/IN-02 excluded)
- Fixed: 5
- Skipped: 0

## Fixed Issues

### CR-01: Revoking a selected non-Player signup incorrectly promotes a waitlisted Player

**Files modified:** `QuestBoard.Domain/Services/QuestService.cs`
**Commit:** c1e0fe4
**Applied fix:** `RevokeSignupAsync` now scopes the "seat freed" check to `signup.IsSelected && signup.Role == SignupRole.Player` (`wasSelectedPlayer`) instead of `signup.IsSelected` alone, matching the review's suggested fix. A revoked selected `AssistantDM`/`Spectator` signup no longer triggers `PromoteNextWaitlistedPlayerIfSeatFreedAsync`.

### CR-02: A No vote from a selected non-Player signup incorrectly promotes a waitlisted Player

**Files modified:** `QuestBoard.Repository/PlayerSignupRepository.cs`
**Commit:** 2bbaa43
**Applied fix:** `ChangeVoteAsync` now computes `wasSelectedPlayer = wasSelected && entity.SignupRole == (int)SignupRole.Player` and returns `wasSelectedPlayer && vote == VoteType.No` as the seat-freed signal. `IsSelected` is still cleared unconditionally for any previously-selected role voting No (preserving existing non-Player unselect behavior, as the review's fix note explicitly allowed) — only the promotion signal returned to the caller is now Player-scoped.

### WR-01: Stale XML doc contract on `IQuestService.ChangeVoteAsync` contradicts implementation

**Files modified:** `QuestBoard.Domain/Interfaces/IQuestService.cs`
**Commit:** fde88f9
**Applied fix:** Replaced the doc comment with the review's suggested text: a Yes or Maybe vote can select a waitlisted player into an open seat when a fresh server-side seat count shows room; a selected player voting No frees their seat and triggers promotion; a No vote never grants or changes selection.

### WR-02: `GetTopWaitlistedCandidateAsync` vote-priority sort duplicated verbatim in Domain and Repository

**Files modified:** `QuestBoard.Domain/Extensions/WaitlistOrdering.cs`, `QuestBoard.Repository/PlayerSignupRepository.cs`
**Commit:** 7087ca6
**Applied fix:** Extracted a single value-based `WaitlistOrdering.VotePriority(int? vote)` helper operating on a plain nullable int (rather than mapping repository entities to domain models, which would have required additional `Include`s and risked losing required-navigation-property invariants). `PlayerSignupRepository.GetTopWaitlistedCandidateAsync` now calls this shared helper via a small `VoteForProposedDate` entity accessor instead of maintaining its own duplicate `VotePriority` implementation. Both layers now apply the identical vote-priority rule from one place.

### WR-03: No test coverage for non-Player roles through the promotion-trigger paths

**Files modified:** `QuestBoard.UnitTests/Services/QuestServiceTests.cs`, `QuestBoard.UnitTests/Repository/PlayerSignupRepositoryTests.cs`
**Commit:** 7aa3541
**Applied fix:** Added `RevokeSignupAsync_WhenRevokedSignupWasSelectedAssistantDM_DoesNotPromote` and `ChangeVoteAsync_SelectedSpectatorVotesNo_DoesNotPromote` in `QuestServiceTests.cs` (asserting `DidNotReceive().UpdateAsync(...)` / `DidNotReceive().EnqueueWaitlistPromotedEmail(...)`), plus two repository-level tests exercising the real fixed `PlayerSignupRepository.ChangeVoteAsync` against an `AssistantDM` and a `Spectator` signup (`ChangeVoteAsync_SelectedAssistantDMVotesNo_SetsIsSelectedFalseButReturnsFalse`, `ChangeVoteAsync_SelectedSpectatorVotesNo_SetsIsSelectedFalseButReturnsFalse`), confirming `IsSelected` still clears but the seat-freed signal returns `false`.

## Skipped Issues

None — all in-scope findings were fixed.

## Verification

- `dotnet build` (full solution): succeeded, 0 warnings, 0 errors, after each fix and after the final test-coverage commit.
- `dotnet test` (full solution, run from the fix worktree root after all five commits): **QuestBoard.UnitTests** 161/161 passed; **QuestBoard.IntegrationTests** 292/292 passed. No regressions.
- Info-tier findings IN-01 (`.Date.Date` lookup inconsistency in `Details.cshtml`) and IN-02 (stale `IPlayerSignupRepository.ChangeVoteAsync` doc re: role scoping) were left untouched — out of scope for `fix_scope: critical_warning`.

---

_Fixed: 2026-07-04T23:39:14Z_
_Fixer: Claude (gsd-code-fixer)_
_Iteration: 1_
