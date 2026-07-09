---
phase: 66-quest-description-editor-rendering-proof-of-concept
plan: 06
subsystem: ui
tags: [razor, markdig, markdown, quest-views]

# Dependency graph
requires:
  - phase: 66-quest-description-editor-rendering-proof-of-concept (plan 01)
    provides: "IMarkdownService.ExtractPlainText(string?) and Html.Markdown() Razor HtmlHelper adapter"
  - phase: 66-quest-description-editor-rendering-proof-of-concept (plan 04)
    provides: "markdown-content.css typography scope consumed by Html.Markdown()'s .markdown-content wrapper"
provides:
  - "Quest Details (desktop + mobile) renders Description as formatted HTML via Html.Markdown()"
  - "Quest Manage (desktop + mobile) gains a new collapsed-by-default Description section rendering Html.Markdown(Model.Description)"
  - "Desktop board card (Index.cshtml) renders Description as Markdown-stripped plain text via IMarkdownService.ExtractPlainText, feeding the existing 130/250/500 poster-length calibration"
affects: [67-quest-rewards-recap-remaining-emails]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Read-side views consume Html.Markdown()/ExtractPlainText directly — no new server-side plumbing needed, matches 66-01's design intent"
    - "Board card (list context) always uses ExtractPlainText, never Html.Markdown — Details/Manage (detail context) always use Html.Markdown, never raw field access"

key-files:
  created: []
  modified:
    - QuestBoard.Service/Views/Quest/Details.cshtml
    - QuestBoard.Service/Views/Quest/Details.Mobile.cshtml
    - QuestBoard.Service/Views/Quest/Manage.cshtml
    - QuestBoard.Service/Views/Quest/Manage.Mobile.cshtml
    - QuestBoard.Service/Views/Quest/Index.cshtml

key-decisions:
  - "Manage's new collapsible Description section is a genuinely new sibling `card modern-card` block inserted immediately inside the existing outer card-body, not a card-within-a-card-body nesting — matches the plan's placement instruction literally"
  - "Details.Mobile.cshtml's Description block no longer applies the shared `quest-description-mobile` class (which carries the old pre-wrap CSS); the Rewards block on the same view keeps that class and its pre-wrap styling completely untouched, per D-06/the shared-class landmine callout in RESEARCH.md"
  - "Index.cshtml's SelectPosterByContent local function captures the razor-page-scoped @inject'd MarkdownService via closure — no parameter threading needed"

patterns-established: []

requirements-completed: [QUESTMD-01, EDITOR-04]

# Metrics
duration: ~4min
completed: 2026-07-09
status: complete
---

# Phase 66 Plan 06: Quest Description Editor Rendering Read-Views Summary

**Quest Description now renders as formatted HTML on Details (desktop + mobile) and a new collapsed-by-default Manage section, and as Markdown-stripped plain text on the board card — all via the `Html.Markdown()`/`ExtractPlainText` primitives built in 66-01, with zero changes to poster-selection thresholds**

## Performance

- **Duration:** ~4 min
- **Started:** 2026-07-09T17:21:00Z (approx, first commit 17:21:41)
- **Completed:** 2026-07-09T17:23:43Z
- **Tasks:** 3
- **Files modified:** 5

## Accomplishments
- Details.cshtml and Details.Mobile.cshtml render Quest Description via `@Html.Markdown(...)` instead of raw `pre-wrap` text; the Details.Mobile shared-class hazard (Description and Rewards both used `quest-description-mobile`) was resolved by dropping that class from the Description wrapper only — Rewards keeps it, and `quests.mobile.css` was not touched
- Quest Manage (desktop + mobile) gained a brand-new collapsed-by-default "Description" section (`fas fa-scroll` header icon, `btn-link text-warning fw-bold` collapse toggle, `fas fa-chevron-down` chevron) replicating `_QuestSection.cshtml`'s structure without including it as a partial — Description was previously entirely absent from Manage
- Index.cshtml's board card now shows `IMarkdownService.ExtractPlainText(quest.Description)` instead of the raw field, and the poster-length calculation (`SelectPosterByContent`) measures that same stripped string, so the 130/250/500 thresholds keep their existing calibration exactly as before
- `dotnet build QuestBoard.Service` succeeds with 0 warnings/errors after every task

## Task Commits

Each task was committed atomically:

1. **Task 1: Render Description as HTML on Details (desktop + mobile), preserving Rewards pre-wrap** - `71c14c79` (feat)
2. **Task 2: Add a collapsible Description section to Quest Manage (desktop + mobile)** - `6d9aa763` (feat)
3. **Task 3: Board card shows Markdown-stripped Description feeding poster selection (Index)** - `d1ec65e2` (feat)

_Note: This plan's SUMMARY/state commit is made separately by the orchestrator after all worktree agents in the wave complete._

## Files Created/Modified
- `QuestBoard.Service/Views/Quest/Details.cshtml` - Replaced the raw `pre-wrap` Description `<p>` with `@Html.Markdown(Model.Quest?.Description)`; added `@using QuestBoard.Service.Extensions`
- `QuestBoard.Service/Views/Quest/Details.Mobile.cshtml` - Description block now renders `@Html.Markdown(Model.Quest.Description)` without the shared `quest-description-mobile` wrapper class; Rewards block unchanged; added `@using QuestBoard.Service.Extensions`
- `QuestBoard.Service/Views/Quest/Manage.cshtml` - New collapsed-by-default `card modern-card` Description section inserted at the top of the existing card-body; added `@using QuestBoard.Service.Extensions`
- `QuestBoard.Service/Views/Quest/Manage.Mobile.cshtml` - Same new collapsible Description section, placed in the mobile content area with a distinct collapse target id (`questDescriptionCollapseMobile`); added `@using QuestBoard.Service.Extensions`
- `QuestBoard.Service/Views/Quest/Index.cshtml` - Added `@inject QuestBoard.Domain.Interfaces.IMarkdownService MarkdownService`; `SelectPosterByContent` and the description div both now call `MarkdownService.ExtractPlainText(quest.Description)` instead of reading `quest.Description` directly

## Decisions Made
- Manage's new Description section is inserted as a sibling `card modern-card` element immediately inside the existing outer `card-body`, not nested as a body-within-a-body — matches the plan's explicit placement instruction ("as a sibling block ... not nested as a card-within-a-card body")
- Used a distinct collapse target id per view (`questDescriptionCollapse` desktop, `questDescriptionCollapseMobile` mobile) even though desktop/mobile never render simultaneously, for defensive clarity
- `SelectPosterByContent` is a C# local function inside the Razor code block; it captures the `@inject`-resolved `MarkdownService` field via normal closure semantics, so no parameter threading was needed

## Deviations from Plan

None functionally — plan executed exactly as written. One documentation note:

The plan's Task 1 automated verify command includes `! grep -q "white-space: pre-wrap" QuestBoard.Service/Views/Quest/Details.cshtml`, which checks the *entire file* for any occurrence of that string. Details.cshtml's Rewards block (a separate, pre-existing element unrelated to Description) has always carried its own inline `style="white-space: pre-wrap;"` and — per this same plan's `must_haves.truths` ("Rewards' pre-wrap behavior is untouched") — is explicitly required to keep it. The literal grep as written in the plan would therefore fail against correct, intended code. This was verified directly: the only remaining `white-space: pre-wrap` occurrence in Details.cshtml is on the Rewards `<div class="quest-description-box">` line, not the Description element. Implementation follows the plan's stated intent (Description has no pre-wrap, Rewards is untouched) rather than the overly-broad literal grep. No code change was needed to resolve this — it is a plan-verification-script scoping issue, not a defect in the shipped views.

## Issues Encountered
None.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- All 5 locked `must_haves` artifacts exist and match their required `contains` patterns (`Html.Markdown` on Details.cshtml and Manage.cshtml, `ExtractPlainText` on Index.cshtml)
- `dotnet build QuestBoard.Service` succeeds with 0 warnings/errors after all 3 tasks
- Visual confirmation (rendered HTML actually looking correct, collapse behavior, board card appearance) is deferred to 66-07 per this plan's own `<verification>` block
- Phase 67 (Quest Rewards/Recap + remaining emails) can follow the same read-side wiring pattern established here (`Html.Markdown()` for detail views, `ExtractPlainText` for list/card contexts) when it migrates Rewards off its remaining `pre-wrap` styling

---
*Phase: 66-quest-description-editor-rendering-proof-of-concept*
*Completed: 2026-07-09*
