---
phase: 67-remaining-quest-fields-email-templates
plan: 03
subsystem: ui
tags: [markdown, easymde, razor, quest-log, recap, rewards]

# Dependency graph
requires:
  - phase: 66-quest-description-editor-rendering-proof-of-concept
    provides: "_MarkdownEditor.cshtml partial, _QuestFormScripts.cshtml, Html.Markdown() HtmlHelper extension, IMarkdownService"
provides:
  - "Recap edit forms (QuestLog/EditRecap desktop+mobile) wired to the shared Markdown editor with working toolbar/preview"
  - "QuestLog Details (desktop+mobile) rendering Rewards and Recap as formatted HTML via Html.Markdown()"
  - "QuestLog Details.Mobile.cshtml Rewards section (D-03 mobile parity backfill — never existed before)"
affects: ["67-05 (live human-verify checkpoint covering EditRecap's editor + CSS-leak fix)"]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Reused Phase 66's _MarkdownEditor.cshtml + _QuestFormScripts.cshtml + Html.Markdown() verbatim, no new infrastructure"

key-files:
  created: []
  modified:
    - QuestBoard.Service/Views/QuestLog/EditRecap.cshtml
    - QuestBoard.Service/Views/QuestLog/EditRecap.Mobile.cshtml
    - QuestBoard.Service/Views/QuestLog/Details.cshtml
    - QuestBoard.Service/Views/QuestLog/Details.Mobile.cshtml

key-decisions:
  - "FieldName kept lowercase 'recap' on both EditRecap forms to preserve the existing raw name=\"recap\" model-binding convention (not asp-for)"
  - "Mobile Details Rewards section (D-03) placed between Original Quest Description and Adventurers, reusing the desktop .quest-description-box class and fa-coins icon"

patterns-established:
  - "@using QuestBoard.Service.Extensions required in any view calling Html.Markdown() that doesn't already import it via a shared _ViewImports"

requirements-completed: [QUESTMD-02, QUESTMD-03]

# Metrics
duration: ~20min
completed: 2026-07-10
---

# Phase 67 Plan 03: Recap Editor Wiring + QuestLog Rewards/Recap Rendering Summary

**Wired the shared Markdown editor into both Recap edit forms and switched QuestLog Details (desktop + mobile) to render Rewards and Recap as formatted HTML, backfilling the mobile Rewards section that never existed (D-03).**

## Performance

- **Duration:** ~20 min
- **Completed:** 2026-07-10T08:11:15Z
- **Tasks:** 2/2 completed
- **Files modified:** 4

## Accomplishments
- `EditRecap.cshtml` / `EditRecap.Mobile.cshtml` now use `_MarkdownEditor` (toolbar + preview) instead of a plain textarea, with the previously-missing `@section Scripts { _QuestFormScripts }` block added so EasyMDE actually initializes
- `QuestLog/Details.cshtml` and `Details.Mobile.cshtml` render both Rewards and Recap via `Html.Markdown()` instead of raw text
- `Details.Mobile.cshtml` gained a brand-new Rewards section (D-03) mirroring desktop's structure — this view had no Rewards markup at all before this plan
- Out-of-scope "Original Quest Description" blocks on both Details views left untouched (still raw text, still pre-wrap) — no shared CSS class edited

## Task Commits

Each task was committed atomically:

1. **Task 1: Wire _MarkdownEditor into the Recap edit forms (desktop + mobile) and add the scripts include** - `23dcc9f` (feat)
2. **Task 2: Render Rewards + Recap via Html.Markdown() on QuestLog Details (desktop + mobile), including the D-03 mobile Rewards backfill** - `05d95ae` (feat)

## Files Created/Modified
- `QuestBoard.Service/Views/QuestLog/EditRecap.cshtml` - Recap textarea replaced with `_MarkdownEditor` partial (`FieldName="recap"`), `_QuestFormScripts` section added
- `QuestBoard.Service/Views/QuestLog/EditRecap.Mobile.cshtml` - same swap, mobile layout
- `QuestBoard.Service/Views/QuestLog/Details.cshtml` - Rewards and Recap now render via `Html.Markdown()`; added `@using QuestBoard.Service.Extensions`
- `QuestBoard.Service/Views/QuestLog/Details.Mobile.cshtml` - Recap now renders via `Html.Markdown()`; new Rewards section added (D-03); added `@using QuestBoard.Service.Extensions`

## Decisions Made
- Kept `FieldName = "recap"` lowercase in both EditRecap forms — `EditRecapViewModel.Recap` binds via the raw `name="recap"` convention, not `asp-for`; changing case would break POST model binding.
- Placed the new mobile Rewards section (D-03) between "Original Quest Description" and "Adventurers", matching desktop's Description → Rewards ordering, using the same `.quest-description-box` class and `fas fa-coins` icon desktop's Rewards block uses.
- Did not touch `markdown-content.css` or `quests.css` in this plan — the `.markdown-content { white-space: normal }` pre-wrap override these boxes rely on is owned by sibling plan 67-02 (same wave), per the plan's explicit scope note.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Added missing `@using QuestBoard.Service.Extensions` to both QuestLog Details views**
- **Found during:** Task 2 build verification
- **Issue:** `Html.Markdown()` is defined as an `internal static` extension method in `QuestBoard.Service.Extensions.HtmlHelperExtensions`. Neither `QuestLog/Details.cshtml` nor `Details.Mobile.cshtml` had that namespace imported (unlike `Quest/Details.cshtml`, which already does), so `dotnet build` failed with CS1061 (`Markdown` not found on `IHtmlHelper<...>`) on both files.
- **Fix:** Added `@using QuestBoard.Service.Extensions` alongside the existing `@using` lines in both files.
- **Files modified:** `QuestBoard.Service/Views/QuestLog/Details.cshtml`, `QuestBoard.Service/Views/QuestLog/Details.Mobile.cshtml`
- **Verification:** `dotnet build QuestBoard.Service/QuestBoard.Service.csproj -c Debug` — 0 errors, 0 warnings
- **Committed in:** `05d95ae` (Task 2 commit)

---

**Total deviations:** 1 auto-fixed (1 blocking build error)
**Impact on plan:** Necessary compile-time fix, no scope creep — same namespace `Quest/Details.cshtml` already imports for the identical helper.

## Issues Encountered
None beyond the deviation above.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- QUESTMD-03 (Recap read+write) and the QuestLog slice of QUESTMD-02 (Rewards) are complete, including the D-03 mobile backfill.
- This plan depends on sibling plan 67-02 (same wave) for the `.markdown-content { white-space: normal; }` CSS override that makes the newly-rendered Markdown content actually display with paragraph spacing instead of collapsing under the shared `.quest-description-box`/`.recap-display-box` pre-wrap rule. That CSS lands when 67-02's worktree merges — no action needed from this plan, per its own objective note.
- Live editor + rendering + text-color verification (EasyMDE nested in `.modern-card` on `EditRecap.cshtml`) is deferred to 67-05's human-verify checkpoint, as stated in this plan's own `<verification>` section.

---
*Phase: 67-remaining-quest-fields-email-templates*
*Completed: 2026-07-10*

## Self-Check: PASSED

All 4 modified source files and the SUMMARY.md itself confirmed present on disk; all 3 commits (`23dcc9f`, `05d95ae`, `637db18`) confirmed present in `git log --oneline --all`.
