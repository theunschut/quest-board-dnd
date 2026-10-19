---
phase: 92-move-a-quest-or-event-to-a-linked-board
fixed_at: 2026-10-19T00:00:00Z
review_path: .planning/phases/92-move-a-quest-or-event-to-a-linked-board/92-REVIEW.md
iteration: 1
findings_in_scope: 2
fixed: 2
skipped: 0
status: all_fixed
---

# Phase 92: Code Review Fix Report

**Fixed at:** 2026-10-19
**Source review:** .planning/phases/92-move-a-quest-or-event-to-a-linked-board/92-REVIEW.md
**Iteration:** 1

**Summary:**
- Findings in scope: 2 (0 critical, 2 warning; Info items IN-01..IN-07 out of scope)
- Fixed: 2
- Skipped: 0

Both fixes were made directly in the main checkout on `milestone/v9-rolling-improvements`
(no worktree, as instructed). Nothing was pushed.

## Fixed Issues

### WR-01: A repeated confirm after a successful move returns a bare 404

Extended by the browser UAT gap (test 4): Back to `/Move/Quest/{id}` after a move also returned a bare 404.

**Files modified:** `QuestBoard.Service/Controllers/MoveController.cs`, `QuestBoard.Service/Views/Move/Index.cshtml`, `QuestBoard.Service/Views/Move/Index.Mobile.cshtml`, `QuestBoard.Service/wwwroot/js/site.js`, `QuestBoard.IntegrationTests/Tests/BoardMoveApplyTests.cs`, `QuestBoard.IntegrationTests/Tests/BoardMovePageTests.cs`, `QuestBoard.IntegrationTests/Tests/BoardMoveEventTests.cs`
**Commit:** b44d2379
**Applied fix:**
- All four actions (`Quest`, `ConfirmQuest`, `Event`, `ConfirmEvent`) now turn `MoveStatus.NotFound` into a redirect to the active board's Quest Board (`Quest/Index`) or Events list (`Events/Index`) with `TempData["Info"]` ("That quest/event is no longer on this board."). The reply is identical for an id that never existed, an id on a board the viewer cannot see, and an entry just moved away, so it does not reveal which one it was.
- Submit lock: both confirm forms (desktop and `.Mobile`) carry `data-disable-on-submit`. `site.js` gained `disableSubmitButtonsOnSubmit()` (wired in the existing `DOMContentLoaded` handler, no inline script, so the CSP is respected) plus a `pageshow` handler that re-enables the buttons when a page is restored from the back-forward cache (otherwise Back would show a permanently disabled button).
- Tests: the two tests that asserted the old 404 were replaced (`AQuestThatDoesNotExist_...` and `EventOnAnotherBoard_...`), and new tests pin redirect + toast for GET and POST, quest and event: a repeated confirm after a move (the entry stays moved, feed revision stays 2), GET after a move, an unknown id versus an id on another board giving the same reply, and the confirm form opting in to the lock with no inline `onsubmit` on both layouts. The `Quest/Details` 404 assertion in `BoardMoveApplyTests` is a different controller and was left unchanged.

### WR-02: The repository trusts the plan's classification and only compares id sets

**Files modified:** `QuestBoard.Domain/Models/BoardMove/QuestMovePlan.cs`, `QuestBoard.Domain/Models/BoardMove/EventMovePlan.cs`, `QuestBoard.Domain/Extensions/MovePreviewStamp.cs`, `QuestBoard.Domain/Services/QuestMoveService.cs`, `QuestBoard.Domain/Services/EventMoveService.cs`, `QuestBoard.Repository/QuestMoveRepository.cs`, `QuestBoard.Repository/EventMoveRepository.cs`, `QuestBoard.UnitTests/Repository/BoardMoveQuestRepositoryTests.cs`, `QuestBoard.UnitTests/Repository/BoardMoveEventRepositoryTests.cs`, `QuestBoard.UnitTests/Services/BoardMoveQuestPreviewTests.cs`, `QuestBoard.UnitTests/Services/BoardMoveEventServiceTests.cs`
**Commit:** 06751a48
**Status:** fixed: requires human verification (staleness logic is a semantic check, not covered by syntax verification alone)
**Applied fix:**
- `QuestMovePlan` now carries `SignupBasis` (per signup: id, player id, `IsSelected`, `Role`, vote on the finalized date) and `FinalizedProposedDateId`. `EventMovePlan` carries `SignupBasis` (row id, user id). Both records live in the existing plan files.
- `QuestMoveRepository.ApplyAsync` returns `Changed`, touching nothing, when any loaded signup differs from its basis (owner, seat, role, vote), when the basis does not cover exactly the loaded signups, when a promoted signup is no longer unselected, a Player and voted Yes or Maybe, and when target-board membership no longer matches the plan (a kept player who left, or a dropped player who joined).
- `EventMoveRepository.ApplyAsync` returns `Changed` on any row whose owner differs from the basis, any kept or dropped row whose target membership flipped, or a campaign add for someone who is not a target member. The check reads `UserGroups` (no filter bypass).
- The page stamp folds in the quest signup state (`MovePreviewStamp.Compute` gained an optional order-insensitive `signupStates` argument), so a DM selecting or revoking a seat between page render and confirm now yields the "things changed" page instead of silently stale marks.
- Constraints held: still exactly one `SaveChangesAsync` per repository, no `IgnoreQueryFilters` / `ExecuteUpdate` / raw SQL / hand-set state, no ambient clock. `BoardMoveRepositorySingleSaveTests`, `FeedRevisionWriteSeamTests`, `AmbientClockSeamTests` and `BoardMoveStaticGuardTests` pass.
- Tests added: 7 quest repository cases (waitlisted selected, kept seat revoked, dropped selected, role switched, vote changed, dropped player joined target, kept player left target) plus promotion with no finalized date, promotion of an already-selected signup and a plan missing a basis entry; 5 event repository cases (dropped joined target, kept left target, add for a non-member, row owned by someone else, missing basis); service tests for the stamp changing on a seat change and for the plan carrying its basis. Existing repository tests were updated to seed target-board memberships and build the new plan shape.
- Known limit (unchanged, documented rather than fixed): a signup inserted after the repository's read but before `SaveChangesAsync` cannot be detected without a serializable transaction, which the EF Core InMemory provider used by the tests does not support. The window is one request and the extra reads now shrink it further.

## Verification

Gates ran in the main checkout (no worktree), against the EF Core InMemory provider; no SQL Server was started or touched.

- `dotnet build`: 0 errors.
- `dotnet test QuestBoard.UnitTests`: 1103 passed, 0 failed (baseline 1084, +19).
- `dotnet test QuestBoard.IntegrationTests`: 1294 passed, 0 failed, 11 skipped (baseline 1286, +8).
- Note: a single solution-level `dotnet test` invocation hit the transient `Fatal error. Internal CLR error. (0x80131506)` twice (initial run and one re-run). The projects were then built once and each test project run on its own with `--no-build`, which passed as above. No GC environment variables were set.
- Not verified: the submit lock was not exercised in a real browser; it is covered by markup tests and by `node -c` on `site.js`.

---

_Fixed: 2026-10-19_
_Fixer: Claude (gsd-code-fixer)_
_Iteration: 1_
