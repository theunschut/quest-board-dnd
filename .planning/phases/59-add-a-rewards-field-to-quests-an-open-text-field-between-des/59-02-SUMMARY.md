---
phase: 59-add-a-rewards-field-to-quests-an-open-text-field-between-des
plan: 02
subsystem: ui
tags: [razor, cshtml, mvc, quest-board, forms]

# Dependency graph
requires:
  - phase: 59-01
    provides: "QuestViewModel.Rewards / Quest.Rewards domain model, EF migration"
provides:
  - "Rewards textarea on Create/Edit/CreateFollowUp forms (desktop + mobile)"
  - "Rewards boxed display callout on Quest Details (desktop + mobile) and QuestLog Details"
affects: [quest-views, quest-log-views]

# Tech tracking
tech-stack:
  added: []
  patterns: ["Reuse .quest-description-box/.quest-description-mobile with inline white-space: pre-wrap for new optional text fields"]

key-files:
  created: []
  modified:
    - QuestBoard.Service/Views/Quest/Create.cshtml
    - QuestBoard.Service/Views/Quest/Create.Mobile.cshtml
    - QuestBoard.Service/Views/Quest/Edit.cshtml
    - QuestBoard.Service/Views/Quest/Edit.Mobile.cshtml
    - QuestBoard.Service/Views/Quest/CreateFollowUp.cshtml
    - QuestBoard.Service/Views/Quest/Details.cshtml
    - QuestBoard.Service/Views/Quest/Details.Mobile.cshtml
    - QuestBoard.Service/Views/QuestLog/Details.cshtml

key-decisions:
  - "Rewards field placed before the @if (boardType != BoardType.Campaign) conditional on Create/Edit so it shows for both OneShot and Campaign boards"
  - "Display blocks reuse existing .quest-description-box / .quest-description-mobile classes with inline style=\"white-space: pre-wrap;\" rather than introducing new CSS"
  - "fa-coins icon locked for every Rewards heading/label across desktop and mobile, no substitution"

patterns-established:
  - "Optional text field mirrors Description field exactly (no required asterisk, same wrapper/label/textarea structure)"

requirements-completed: []

# Metrics
duration: 14min
completed: 2026-07-06
status: complete
---

# Phase 59 Plan 02: Rewards Field UI (Forms + Display) Summary

**Rewards textarea added to all 5 Quest form views and a hidden-when-empty boxed Rewards callout added to all 3 Quest/QuestLog display views, mirroring the existing Description field/box patterns exactly.**

## Performance

- **Duration:** ~14 min
- **Started:** 2026-07-06T19:XX:XXZ
- **Completed:** 2026-07-06
- **Tasks:** 2 of 3 (Task 3 is a blocking human-verify checkpoint — plan paused there)
- **Files modified:** 8

## Accomplishments
- Rewards textarea (optional, no required asterisk) added to Create.cshtml, Create.Mobile.cshtml, Edit.cshtml, Edit.Mobile.cshtml, and CreateFollowUp.cshtml, positioned between Description and Challenge Rating and before the Campaign board-type conditional (shows for both OneShot and Campaign boards)
- Rewards boxed display callout added to Quest/Details.cshtml, Quest/Details.Mobile.cshtml, and QuestLog/Details.cshtml, hidden entirely when Rewards is null/empty/whitespace, using the gold `fa-coins` icon and the existing `.quest-description-box`/`.quest-description-mobile` styling
- Follow-Up quest form's Rewards field left unfilled (no pre-fill logic added — binds to the empty-by-default `FollowUpQuestViewModel.Rewards`)
- Quest board list card (`_QuestCard.cshtml`) and `Quest/Index.cshtml` deliberately left untouched (confirmed via grep — zero Rewards references)
- Service project builds clean (0 errors, 0 warnings) after each task

## Task Commits

Each task was committed atomically:

1. **Task 1: Add the Rewards form field to the five form views** - `33a482a` (feat)
2. **Task 2: Add the Rewards display block to the three display views** - `9e9c1f9` (feat)
3. **Task 3: Human verification checkpoint** - not started, plan paused here (blocking checkpoint, autonomous: false)

## Files Created/Modified
- `QuestBoard.Service/Views/Quest/Create.cshtml` - Rewards textarea (`asp-for="Rewards"`) inserted after Description, before the Campaign conditional
- `QuestBoard.Service/Views/Quest/Create.Mobile.cshtml` - Same field, mobile label class parity (`dm-create-label`)
- `QuestBoard.Service/Views/Quest/Edit.cshtml` - Rewards textarea (`asp-for="Quest.Rewards"`) inserted after Description, before the Campaign conditional
- `QuestBoard.Service/Views/Quest/Edit.Mobile.cshtml` - Same field, mobile variant
- `QuestBoard.Service/Views/Quest/CreateFollowUp.cshtml` - Rewards textarea (`asp-for="Rewards"`) inserted between Description and Challenge Rating (no board-type conditional in this view); left blank, no pre-fill
- `QuestBoard.Service/Views/Quest/Details.cshtml` - Rewards display block (`<h5><i class="fas fa-coins text-warning me-2"></i>Rewards</h5>` + `.quest-description-box`) inserted after the Description `<p>`, guarded by `IsNullOrWhiteSpace`
- `QuestBoard.Service/Views/Quest/Details.Mobile.cshtml` - Rewards display block mirroring the existing Description conditional, same `.quest-description-mobile` class, `fa-coins` icon prefix
- `QuestBoard.Service/Views/QuestLog/Details.cshtml` - Read-only Rewards box inserted after "Original Quest Description", before the Adventurers section, guarded by `IsNullOrWhiteSpace`

## Decisions Made
- Rewards field/block placement mirrors the UI-SPEC's exact markup and the existing Description pattern in every file — no deviation from the design contract.
- Reused `.quest-description-box`/`.quest-description-mobile` with inline `white-space: pre-wrap` rather than editing `quests.css`, per plan instruction (no new CSS classes).

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

None.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

Tasks 1 and 2 are complete, committed, and build-verified. The plan is now paused at Task 3, a blocking `checkpoint:human-verify` task (`autonomous: false`). A fresh agent (or the orchestrator) must resume with the human verification steps documented in `59-02-PLAN.md` (run the app, verify the Rewards field/display across Create/Edit/Follow-Up/Details/QuestLog Details, desktop + mobile, per the 7-point checklist in the plan).

---
*Phase: 59-add-a-rewards-field-to-quests-an-open-text-field-between-des*
*Completed: 2026-07-06 (Tasks 1-2; Task 3 checkpoint pending)*
