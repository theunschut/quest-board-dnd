---
phase: 63-allow-any-player-to-edit-quest-recaps-not-just-the-assigned-
plan: 01
subsystem: quest-log
tags: [aspnet-mvc, authorization, viewbag, integration-tests]

# Dependency graph
requires:
  - phase: 53-add-dedicated-edit-view-for-quest-recap-so-details-page-is-v
    provides: The EditRecap GET+POST actions and DM/Admin-only gate this phase loosens
provides:
  - Recap editing on completed quests open to any authenticated group member (not just DM/Admin)
  - New ViewBag.CanManageQuest flag (DM/Admin-only) that keeps the Manage Quest Quick-Actions link locked down, split out from the broadened CanEditRecap
  - Integration test coverage for both the broadened recap permission and the still-restricted Manage-link gating
affects: [quest-log, authorization]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Two-flag ViewBag split for a single view section that gates two different concerns (CanEditRecap vs CanManageQuest), mirroring QuestController's ViewBag.CanManage = isQuestDm || isAdmin shape"

key-files:
  created: []
  modified:
    - QuestBoard.Service/Controllers/QuestBoard/QuestLogController.cs
    - QuestBoard.Service/Views/QuestLog/Details.cshtml
    - QuestBoard.Service/Views/QuestLog/Details.Mobile.cshtml
    - QuestBoard.IntegrationTests/Controllers/QuestLogControllerIntegrationTests.cs

key-decisions:
  - "Recap editing opens to any authenticated member of the quest's group (D-01) — not narrowed to quest participants, per explicit user request."
  - "Manage Quest link kept DM/Admin-only via a new, separate ViewBag.CanManageQuest flag (D-02) to avoid a permission-escalation bug that a naive single-flag broadening would have introduced."
  - "No editor attribution added to Recap (D-03) and no notification sent when a non-DM player edits the recap (D-04) — both explicitly declined as out-of-scope."
  - "In-action isQuestDm/isAdmin Forbid() branches in EditRecap GET/POST fully deleted rather than left as dead code, per Claude's Discretion in the phase context."

patterns-established:
  - "When splitting a shared permission flag that gates two unrelated UI elements, always add a new named flag instead of broadening the existing one, to prevent accidental escalation of the narrower permission's surface."

requirements-completed: []

# Metrics
duration: ~35min (across sessions, including a checkpoint pause for human verification)
completed: 2026-07-07
status: complete
---

# Phase 63 Plan 01: Allow any player to edit quest recaps Summary

**Removed the DM/Admin-only gate on QuestLogController.EditRecap (GET+POST) so any authenticated group member can edit a completed quest's Session Recap, while splitting the shared ViewBag flag so the unrelated "Manage Quest" link stays DM/Admin-only on both desktop and mobile.**

## Performance

- **Duration:** ~35 min of active execution across sessions (paused at the human-verify checkpoint between session 1 and this continuation)
- **Completed:** 2026-07-07
- **Tasks:** 4 (3 automated + 1 human-verify checkpoint)
- **Files modified:** 4

## Accomplishments
- `EditRecap` GET and POST in `QuestLogController` no longer require `[Authorize(Policy = "DungeonMasterOnly")]` or pass an in-action `isQuestDm || isAdmin` ownership check — any authenticated user reaches the page and can persist a recap, while the completed-quest eligibility guard and unauthenticated-challenge guard are untouched.
- `Details` GET now publishes two distinct ViewBag flags: a broadened `CanEditRecap` (true for any resolved authenticated user) and a new `CanManageQuest` (`isQuestDm || isAdmin`, mirroring `QuestController.CanManage`) so the Manage Quest Quick-Actions link is NOT accidentally exposed to every player.
- Both `Details.cshtml` and `Details.Mobile.cshtml` updated identically in the same commit (mobile-parity rule): recap Edit/Add button gated on `CanEditRecap`, Manage Quest link gated on `CanManageQuest`.
- Integration tests flipped from asserting Player-denial to Player-allowed for both GET and POST, plus two new tests (`Details_Player_DoesNotSeeManageQuestLink`, `Details_NonOwnerAdmin_SeesManageQuestLink`) locking in the permission split. Existing DM/Admin regression tests untouched.
- Human verification completed against the real running app confirmed: a plain Player sees the Edit/Add Recap button (desktop + mobile) and can save a recap, but does not see the Manage Quest link; a DM/Admin sees both. User responded "approved."

## Task Commits

Each task was committed atomically:

1. **Task 1: Remove DM/Admin recap-edit gate and split the shared Details flag in QuestLogController** - `9b52d8c` (feat)
2. **Task 2: Split CanEditRecap / CanManageQuest gates in both Details views** - `9042efd` (feat)
3. **Task 3: Flip recap-permission integration tests and add Manage-link gating coverage** - `3f2eeca` (test)
4. **Task 4: Human verification** - checkpoint, resolved via user response "approved" (no code commit — verification-only task)

**Merge into main working tree:** `e892aee` (merge commit — see Deviations below)

_Note: Tasks 1-3 were executed in an isolated git worktree by a prior execution wave, then merged into `milestone/v7-backlog-cleanup` ahead of the human-verify checkpoint so verification could run against the real app._

## Files Created/Modified
- `QuestBoard.Service/Controllers/QuestBoard/QuestLogController.cs` - Removed `[Authorize(Policy = "DungeonMasterOnly")]` from EditRecap GET+POST and their in-action `Forbid()` ownership checks; added `ViewBag.CanManageQuest = isQuestDm || isAdmin` alongside a broadened `ViewBag.CanEditRecap = currentUser != null`.
- `QuestBoard.Service/Views/QuestLog/Details.cshtml` - Manage Quest link gate changed from `CanEditRecap` to `CanManageQuest`; recap Edit/Add button gate left on `CanEditRecap`.
- `QuestBoard.Service/Views/QuestLog/Details.Mobile.cshtml` - Same split applied identically for mobile parity.
- `QuestBoard.IntegrationTests/Controllers/QuestLogControllerIntegrationTests.cs` - Flipped `EditRecap_Player_IsForbidden` → `EditRecap_Player_ReturnsOk` and `EditRecap_Post_Player_IsForbidden` → `EditRecap_Post_Player_RedirectsToDetails`; added `Details_Player_DoesNotSeeManageQuestLink` and `Details_NonOwnerAdmin_SeesManageQuestLink`.

## Decisions Made
- D-01: Recap editing opens to any authenticated group member, not narrowed to quest participants (explicit user request, alternative rejected).
- D-02: Manage Quest link split into its own `CanManageQuest` flag to avoid silently exposing DM/Admin-only functionality to all players — this was the critical design constraint of the whole phase.
- D-03: No editor attribution added to the recap.
- D-04: No email/notification sent on non-DM recap edits, consistent with Phase 61 precedent and the project's constrained email budget.

## Deviations from Plan

### Process deviation (not a code defect)

**1. Mid-flight merge of tasks 1-3 ahead of the checkpoint**
- **Found during:** Between Task 3 and Task 4 (human-verify checkpoint)
- **What happened:** Tasks 1-3 were executed and committed (`9b52d8c`, `9042efd`, `3f2eeca`) in an isolated git worktree per the standard wave-execution pattern. Before the human-verify checkpoint could be resolved, the orchestrator merged the worktree's three commits into `milestone/v7-backlog-cleanup` via merge commit `e892aee`, specifically so the human verifier could exercise the fix against the actual running app (`dotnet run`) rather than a detached worktree checkout.
- **Why this is not a deviation from the plan's substance:** All code changes and their content are exactly as planned in Tasks 1-3 — nothing was altered by the merge. This is purely a process/sequencing note: the merge happened before rather than after the checkpoint resolved, to unblock human verification.
- **Verification after merge:** `dotnet build` succeeded with 0 warnings, 0 errors on the merged tree.
- **No files modified beyond the original three commits' scope.**

**Total deviations:** 1 process-level note (early merge for verification purposes). No code deviations — plan executed exactly as written for all 3 automated tasks.
**Impact on plan:** None on correctness or scope. The early merge only changed when verification could happen, not what was verified.

## Issues Encountered

The user's first verification attempt was against the pre-merge (old, unmerged) code by mistake, and reported both the Edit Recap button and the Manage Quest link were hidden for a Player — this was expected given they were running the old code path. After confirming the merge (`e892aee`) was in place and `dotnet build` was clean, the user re-verified and responded "approved," confirming: a plain Player sees Edit/Add Recap (desktop + mobile) but not Manage Quest; a DM/Admin sees both.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- `dotnet build` succeeds with 0 warnings, 0 errors.
- `dotnet test --filter "FullyQualifiedName~QuestLogControllerIntegrationTests"` passing 15/15 after the test flip (2 renamed/flipped, 2 new, rest untouched regression guards).
- No `DungeonMasterOnly` occurrences remain in `QuestLogController.cs`; `ViewBag.CanManageQuest` present in the controller and both Details views.
- Human verification confirmed the Player-can-edit-recap / Player-cannot-Manage / DM-Admin-sees-both behavior on both desktop and mobile.
- Phase 63 is fully complete; no blockers for Phase 64 (already in progress per STATE.md at time of this summary).

---
*Phase: 63-allow-any-player-to-edit-quest-recaps-not-just-the-assigned-*
*Completed: 2026-07-07*
