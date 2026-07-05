---
phase: 50-fix-quest-edit-page-show-edit-button-for-campaign-quests-and
plan: 03
subsystem: ui
tags: [aspnet-core-mvc, razor, viewbag, board-type]

# Dependency graph
requires:
  - phase: 50-01
    provides: Wave-0 failing integration tests (QuestCampaignUiParityTests) defining the Edit-page acceptance criteria
provides:
  - Edit GET/POST controller actions set ViewBag.BoardType (server-resolved, never trusted from request data)
  - Edit.cshtml and Edit.Mobile.cshtml hide Challenge Rating, Total Player Count, DM-Session checkbox, and Proposed Dates for Campaign quests
  - Invalid Edit POST submissions for Campaign quests return 200 instead of throwing InvalidCastException
affects: [quest-edit, campaign-boards]

# Tech tracking
tech-stack:
  added: []
  patterns: ["@if (boardType != BoardType.Campaign) view-level field gating, mirrored from Create.cshtml/Create.Mobile.cshtml"]

key-files:
  created: []
  modified:
    - QuestBoard.Service/Controllers/QuestBoard/QuestController.cs
    - QuestBoard.Service/Views/Quest/Edit.cshtml
    - QuestBoard.Service/Views/Quest/Edit.Mobile.cshtml

key-decisions:
  - "Edit POST's boardType resolution was moved to run once, before the ModelState check, and reused for both the validation-failure ViewBag assignment and the existing Campaign sanitization block — avoids a duplicate GetActiveBoardTypeAsync call."
  - "Mobile's top-of-page HasExistingSignups banner deliberately left ungated (not wrapped in the boardType conditional), unlike the desktop banner which is nested inside the Proposed Dates block and gets hidden with it."

patterns-established:
  - "Edit views mirror Create's boardType-gating pattern exactly: `var boardType = (BoardType)ViewBag.BoardType;` at file top, single @if wrapping all OneShot-only fields."

requirements-completed: [D-04, D-05]

# Metrics
duration: 25min
completed: 2026-07-05
status: complete
---

# Phase 50 Plan 03: Edit Page Campaign Field Visibility Summary

**Edit.cshtml/Edit.Mobile.cshtml now hide Challenge Rating, Total Player Count, DM-Session checkbox, and Proposed Dates for Campaign quests via ViewBag.BoardType, mirroring Create; Edit POST's validation-failure path was fixed to set ViewBag.BoardType before returning, closing an InvalidCastException risk this same change would otherwise introduce.**

## Performance

- **Duration:** ~25 min
- **Started:** 2026-07-05T19:33:59Z (phase execution start)
- **Completed:** 2026-07-05T19:59:29Z
- **Tasks:** 2
- **Files modified:** 3

## Accomplishments
- `QuestController.Edit` GET now sets `ViewBag.BoardType` from `GetActiveBoardTypeAsync`, matching the pattern already used by `Create` GET.
- `QuestController.Edit` POST resolves `boardType` once, before the `ModelState.IsValid` check, and sets `ViewBag.BoardType` on the validation-failure `return View(viewModel)` path — this is the Pitfall-3 corollary fix, since the view will unconditionally cast `ViewBag.BoardType` once Task 2 lands.
- `Edit.cshtml` and `Edit.Mobile.cshtml` both wrap Challenge Rating, Total Player Count, the DM-Session checkbox, and the entire Proposed Dates block (including the nested `HasExistingSignups` alert on desktop) in `@if (boardType != BoardType.Campaign)`, achieving full field-visibility parity with Create for Campaign quests.
- Desktop "Quest Editing Tips" sidebar left untouched (D-06).

## Task Commits

Each task was committed atomically:

1. **Task 1: Set ViewBag.BoardType in Edit GET and on the Edit POST validation-failure path (D-05 + Pitfall 3)** - `87da4f9` (feat)
2. **Task 2: Wrap the four OneShot-only fields in @if (boardType != BoardType.Campaign) on both Edit views (D-04)** - `75ab47d` (feat)

**Plan metadata:** committed separately per worktree protocol (SUMMARY.md commit below)

## Files Created/Modified
- `QuestBoard.Service/Controllers/QuestBoard/QuestController.cs` - Edit GET sets `ViewBag.BoardType`; Edit POST resolves `boardType` before the ModelState check and reuses it for the validation-failure ViewBag assignment and the existing Campaign sanitization block.
- `QuestBoard.Service/Views/Quest/Edit.cshtml` - Added `var boardType = (BoardType)ViewBag.BoardType;` and wrapped the four OneShot-only field blocks (Challenge Rating, Total Player Count, DM-Session checkbox, Proposed Dates) in `@if (boardType != BoardType.Campaign)`.
- `QuestBoard.Service/Views/Quest/Edit.Mobile.cshtml` - Same declaration and wrapping; top-of-page `HasExistingSignups` banner left outside the conditional (see Decisions).

## Decisions Made

- **Single `boardType` resolution in Edit POST:** The plan required moving the existing `GetActiveBoardTypeAsync` call earlier (before the ModelState check) rather than adding a second call, so there is exactly one resolution reused by both the ViewBag assignment (validation-failure path) and the Campaign sanitization block (success path). Verified via `grep -c GetActiveBoardTypeAsync` scoped to the Edit POST method — exactly one call.
- **Mobile `HasExistingSignups` banner left ungated (Pitfall 2):** Unlike the desktop view, where the banner is nested inside the Proposed Dates `mb-3` block and is naturally hidden along with it, the mobile banner sits near the top of the page, structurally separate from the form fields. Gating it on `boardType != BoardType.Campaign` was deliberately NOT done: `HasExistingSignups` reflects any player signup on the quest, not just date-related activity, so hiding it for Campaign quests risked suppressing a legitimately relevant "players have signed up" warning about non-date changes (e.g. title/description edits). This is an intentional divergence between the desktop and mobile implementations, driven by their different DOM structure, not an oversight.

## Deviations from Plan

None - plan executed exactly as written. Both tasks landed as specified; no bugs, missing functionality, or blocking issues were discovered during implementation.

## Issues Encountered

None specific to this plan's files. The full `dotnet test` run showed 4 pre-existing failures unrelated to this plan's scope:
- 3 `QuestCampaignUiParityTests.CampaignManage_*` failures (Manage page Edit/Delete link rendering) — these test the `/Quest/Manage` view, which is `files_modified` scope for plan 50-02, not this plan (50-03, `Edit`/`Edit.Mobile` only). Confirmed via `git log` that the wave-0 tests (commit `d24e8df`) were authored across all three of phase 50's plans at once; these particular assertions are outside this plan's three files.
- 1 `AdminControllerIntegrationTests.SendConfirmationEmail_Post_WhenUserUnconfirmed_ShouldRedirectToUsersWithSuccess` failure — a rate-limiting test flake (`429 TooManyRequests` instead of expected `302`), unrelated to quest editing.

All tests scoped to this plan pass: `QuestCampaignUiParityTests` filtered to `CampaignEdit_*` and `OneShotEdit_*` (4/4 passed), plus the specific Pitfall-3 guard `CampaignEdit_InvalidModelState_Returns200_DoesNotThrow` (1/1 passed).

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- Edit page now has full field-visibility parity with Create for Campaign vs OneShot quests.
- The 3 pre-existing `CampaignManage_*` failures are expected to be resolved by plan 50-02 (Manage page), not this plan — no action needed here.
- No blockers for phase completion once 50-02's Manage-page work lands.

---
*Phase: 50-fix-quest-edit-page-show-edit-button-for-campaign-quests-and*
*Completed: 2026-07-05*
