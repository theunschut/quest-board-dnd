---
phase: 67-remaining-quest-fields-email-templates
plan: 01
subsystem: ui
tags: [razor, markdown, easymde, quest-forms]

# Dependency graph
requires:
  - phase: 66-quest-description-editor-rendering-proof-of-concept
    provides: "_MarkdownEditor.cshtml partial, MarkdownEditorViewModel, _QuestFormScripts (EasyMDE CDN + markdown-editor.js), POST /markdown/preview endpoint"
provides:
  - "Quest Rewards field wired to the shared Markdown editor on all 6 quest write forms (Create/Edit/Follow-Up, desktop + mobile)"
  - "Follow-Up mobile form now has a Rewards field (previously missing entirely, Phase 59 asymmetry closed)"
affects: [67-02, 67-03, 67-04, 67-05]

# Tech tracking
tech-stack:
  added: []
  patterns: []

key-files:
  created: []
  modified:
    - QuestBoard.Service/Views/Quest/Create.cshtml
    - QuestBoard.Service/Views/Quest/Create.Mobile.cshtml
    - QuestBoard.Service/Views/Quest/Edit.cshtml
    - QuestBoard.Service/Views/Quest/Edit.Mobile.cshtml
    - QuestBoard.Service/Views/Quest/CreateFollowUp.cshtml
    - QuestBoard.Service/Views/Quest/CreateFollowUp.Mobile.cshtml

key-decisions:
  - "No deviations from plan — mechanical repeat of Phase 66's Description wiring pattern applied verbatim to Rewards"

patterns-established: []

requirements-completed: [QUESTMD-02]

# Metrics
duration: 10min
completed: 2026-07-10
---

# Phase 67 Plan 01: Rewards Write-Form Markdown Editor Wiring Summary

**Wired the shared `_MarkdownEditor` partial into Quest Rewards on all 6 quest write forms and backfilled the missing Follow-Up mobile Rewards field, closing the write-side gap for QUESTMD-02.**

## Performance

- **Duration:** ~10 min
- **Started:** 2026-07-10T10:05:00+02:00 (approx)
- **Completed:** 2026-07-10T10:10:00+02:00 (approx)
- **Tasks:** 2 completed
- **Files modified:** 6

## Accomplishments
- All 6 quest write forms (Create, Create.Mobile, Edit, Edit.Mobile, CreateFollowUp, CreateFollowUp.Mobile) render Rewards through `_MarkdownEditor`, giving users the same toolbar/preview/paragraph-hint experience on Rewards as they already have on Description
- `CreateFollowUp.Mobile.cshtml` gained a brand-new Rewards field (D-02 mobile-parity backfill) — it previously had no Rewards field at all, a Phase 59 asymmetry versus desktop
- Rewards remains optional everywhere (`Required = false`, no red asterisk), matching `Rewards`'s `string?` (no `[Required]`) shape on both `QuestViewModel` and `FollowUpQuestViewModel`

## Task Commits

Each task was committed atomically:

1. **Task 1: Wire _MarkdownEditor into Rewards on Create/Edit (desktop + mobile)** - `be39490` (feat)
2. **Task 2: Wire Rewards editor into Follow-Up desktop, and backfill the missing Follow-Up mobile Rewards field (D-02)** - `8d7af06` (feat)

**Plan metadata:** (this commit) `docs(67-01): complete plan`

## Files Created/Modified
- `QuestBoard.Service/Views/Quest/Create.cshtml` - Rewards `<textarea>` replaced with `_MarkdownEditor` partial call (`FieldName = "Rewards"`, `Value = Model.Rewards`)
- `QuestBoard.Service/Views/Quest/Create.Mobile.cshtml` - same swap, mobile form
- `QuestBoard.Service/Views/Quest/Edit.cshtml` - Rewards `<textarea>` replaced with `_MarkdownEditor` partial call (`FieldName = "Quest.Rewards"`, `Value = Model.Quest.Rewards`)
- `QuestBoard.Service/Views/Quest/Edit.Mobile.cshtml` - same swap, mobile form
- `QuestBoard.Service/Views/Quest/CreateFollowUp.cshtml` - Rewards `<textarea>` replaced with `_MarkdownEditor` partial call (`FieldName = "Rewards"`, `Value = Model.Rewards`)
- `QuestBoard.Service/Views/Quest/CreateFollowUp.Mobile.cshtml` - new `_MarkdownEditor` Rewards field added between the existing Description editor and the ChallengeRating block (field did not exist before this plan)

## Decisions Made
None beyond what was already locked in 67-CONTEXT.md/67-PATTERNS.md — this plan executed the mechanical Phase 66 pattern repeat exactly as specified, including the exact `FieldName`/`Value` binding paths per file and the `Required = false` requirement.

## Deviations from Plan

None - plan executed exactly as written. `@using QuestBoard.Service.ViewModels.Shared` was already present in all six files (confirmed by direct read before editing, per the plan's read_first instructions), so no addition was needed. All six files already included `_QuestFormScripts` exactly once before and after the edit (confirmed via grep count), so no scripts changes were made.

## Issues Encountered

The plan's `<context>` referenced `.planning/phases/67-remaining-quest-fields-email-templates/67-PATTERNS.md`, which did not exist inside this worktree (it exists only as an untracked file in the main repo checkout, created after this worktree branched). Read it directly from the main repo's absolute path (`C:\Repos\quest-board\.planning\phases\67-remaining-quest-fields-email-templates\67-PATTERNS.md`) for corroborating detail; PLAN.md itself already contained the exact per-file `FieldName`/`Value` values needed, so this did not block execution.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- The write-side of QUESTMD-02 is complete for Rewards across all 6 quest write forms; the Follow-Up mobile Rewards gap (D-02) is closed.
- Read-side rendering of Rewards (`Html.Markdown()` swaps on Details/Manage/QuestLog views) and the Recap field are handled by sibling plans 67-02 through 67-04; live toolbar/preview verification (including the 320px one-row toolbar check) is deferred to 67-05's human-verify checkpoint, per the plan's stated verification scope.
- `dotnet build QuestBoard.Service/QuestBoard.Service.csproj -c Debug` is green after both tasks (0 errors, 0 warnings).

---
*Phase: 67-remaining-quest-fields-email-templates*
*Completed: 2026-07-10*
