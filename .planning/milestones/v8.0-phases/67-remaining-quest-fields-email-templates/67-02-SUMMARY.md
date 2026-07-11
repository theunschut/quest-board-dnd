---
phase: 67-remaining-quest-fields-email-templates
plan: 02
subsystem: ui
tags: [markdown, razor, quest-rewards, bootstrap-collapse]

# Dependency graph
requires:
  - phase: 66-quest-description-editor-rendering-proof-of-concept
    provides: Html.Markdown() HtmlHelper extension, .markdown-content CSS wrapper, Manage-page Description collapsible pattern
provides:
  - Quest Rewards renders as formatted HTML on Quest Details (desktop + mobile)
  - Collapsed-by-default, blank-guarded Rewards section on Quest Manage (desktop + mobile)
  - .markdown-content white-space: normal override, scoped so it doesn't affect out-of-scope plain-text Description blocks sharing the same parchment box classes
affects: [67-03, 67-04, 67-05]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Read-side Markdown swap: @Model.Quest.Rewards -> @Html.Markdown(Model.Quest.Rewards), no extra wrapper div (helper already wraps in .markdown-content)"
    - "Manage-page collapsible section pattern reused verbatim for a second field (Rewards), scoped id suffix _Collapse / _CollapseMobile, blank-guarded since the field is optional"

key-files:
  created: []
  modified:
    - QuestBoard.Service/Views/Quest/Details.cshtml
    - QuestBoard.Service/Views/Quest/Details.Mobile.cshtml
    - QuestBoard.Service/Views/Quest/Manage.cshtml
    - QuestBoard.Service/Views/Quest/Manage.Mobile.cshtml
    - QuestBoard.Service/wwwroot/css/markdown-content.css

key-decisions:
  - "Pre-wrap doubling fix scoped to .markdown-content (white-space: normal), not to the shared .quest-description-box/.recap-display-box classes in quests.css, so out-of-scope plain-text Description blocks on QuestLog/Details(.Mobile).cshtml keep their line-break preservation until a later phase migrates them"
  - "Manage-page Rewards card is entirely omitted (not shown collapsed-and-empty) when Model.Rewards is blank, matching Details/QuestLog's existing convention"

patterns-established:
  - "Second confirmed instance of the Html.Markdown() read-side swap + Manage-page collapsible pattern from Phase 66, now proven to generalize to a second, optional (non-[Required]) field"

requirements-completed: [QUESTMD-02]

# Metrics
duration: 25min
completed: 2026-07-10
---

# Phase 67 Plan 02: Rewards Rendering on Quest Details & Manage Summary

**Quest Rewards now renders as formatted Markdown HTML on Quest Details (desktop+mobile) and a new collapsed-by-default Manage-page section, with the parchment-box pre-wrap doubling fixed at the `.markdown-content` level only.**

## Performance

- **Duration:** 25 min
- **Started:** 2026-07-10T07:45:00Z
- **Completed:** 2026-07-10T08:10:02Z
- **Tasks:** 2
- **Files modified:** 5

## Accomplishments
- Quest Details (desktop + mobile) render Rewards via `Html.Markdown(Model.Quest.Rewards)` instead of raw text
- Removed the inline `style="white-space: pre-wrap;"` on Details.cshtml's Rewards box (no longer needed once content is real HTML)
- Added a `white-space: normal` override to `.markdown-content`, closing the doubled-spacing bug where the parchment box's inherited `pre-wrap` was preserving Markdig's own insignificant block-level newlines
- New collapsed-by-default Rewards section added to Quest Manage (desktop + mobile), mirroring the existing Description collapsible exactly except for icon (`fa-coins`), title, collapse-target id, and a blank-guard (Rewards is optional, Description is not)
- `quests.css` left completely untouched — confirmed via `git diff --name-only` and a direct grep showing both `.quest-description-box` and `.recap-display-box` still carry `white-space: pre-wrap` there, preserving line-break behavior for out-of-scope plain-text Description blocks elsewhere in the app

## Task Commits

Each task was committed atomically:

1. **Task 1: Render Rewards via Html.Markdown() on Quest Details (desktop + mobile) and add the pre-wrap override** - `2f726fd` (feat)
2. **Task 2: Add the collapsible Rewards section to Quest Manage (desktop + mobile) — D-01** - `50e2e63` (feat)

**Plan metadata:** (recorded by orchestrator after merge)

## Files Created/Modified
- `QuestBoard.Service/Views/Quest/Details.cshtml` - Rewards box now renders `@Html.Markdown(Model.Quest.Rewards)`, inline pre-wrap style removed
- `QuestBoard.Service/Views/Quest/Details.Mobile.cshtml` - Rewards mobile block now renders `@Html.Markdown(Model.Quest.Rewards)`
- `QuestBoard.Service/Views/Quest/Manage.cshtml` - new blank-guarded, collapsed-by-default Rewards card (`questRewardsCollapse`) added immediately after the Description collapsible
- `QuestBoard.Service/Views/Quest/Manage.Mobile.cshtml` - same addition for mobile (`questRewardsCollapseMobile`)
- `QuestBoard.Service/wwwroot/css/markdown-content.css` - `.markdown-content` gains `white-space: normal` plus an explanatory comment on why the override lives here and not on the shared parchment-box classes

## Decisions Made
- Followed the plan's explicit scoping instruction: fixed the pre-wrap doubling bug on `.markdown-content` only, never touching `quests.css`'s shared `.quest-description-box`/`.recap-display-box` rules, since those classes are also used by out-of-scope plain-text content (`QuestLog/Details.cshtml` Original Description, `QuestLog/Details.Mobile.cshtml` Original Description) that must keep line-break preservation until a later phase migrates it.
- Manage-page Rewards card wraps the entire `<div class="card modern-card mb-3">` in the blank-guard (not just the body), so the section is omitted outright rather than shown empty-and-collapsed — matches the plan's acceptance criteria and Phase 67's UI-SPEC Copywriting Contract.

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

None.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- Rewards read-side rendering is now consistent across Quest Details and Quest Manage (desktop + mobile); QuestLog's Rewards/Recap read views and the Follow-Up/QuestLog mobile parity gaps (D-02, D-03) are handled in sibling plans 67-03/67-04.
- No blockers for downstream plans in this phase.

## Self-Check: PASSED

All 5 modified files confirmed present on disk; both task commits (`2f726fd`, `50e2e63`) confirmed present in git log.

---
*Phase: 67-remaining-quest-fields-email-templates*
*Completed: 2026-07-10*
