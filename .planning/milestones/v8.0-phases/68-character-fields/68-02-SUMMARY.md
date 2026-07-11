---
phase: 68-character-fields
plan: 02
subsystem: ui
tags: [markdown, razor, character-details, css]

# Dependency graph
requires:
  - phase: 65-markdown-rendering-foundation
    provides: IMarkdownService / Html.Markdown() extension helper with Web/Email sanitizer profiles
  - phase: 66-quest-description-editor
    provides: The Html.Markdown() read-side call pattern (per-view @using QuestBoard.Service.Extensions + direct call inside a non-<p> container) proven on Quest Details
provides:
  - Character Description and Backstory render as sanitized, formatted HTML on Character Details desktop and mobile
  - Mobile catch-all color rule extended to cover Markdown <li> elements
affects: [68-01 (Character write-side editor), 68-03 (verification checkpoint)]

# Tech tracking
tech-stack:
  added: []
  patterns: ["Read-side Html.Markdown() call replacing a pre-wrap <p> wrapper, mirroring the Quest Details/Manage precedent from Phase 66/67"]

key-files:
  created: []
  modified:
    - QuestBoard.Service/Views/Characters/Details.cshtml
    - QuestBoard.Service/Views/Characters/Details.Mobile.cshtml
    - QuestBoard.Service/wwwroot/css/character-detail.mobile.css

key-decisions:
  - "Dropped (not moved) the inline pre-wrap <p> wrappers on both desktop and mobile — Markdig's block-level output (headings, lists, blockquotes) cannot legally live inside a <p>, and the global .markdown-content { white-space: normal } rule from Phase 67 already governs paragraph spacing."
  - "Added no new desktop CSS — site.css's !important .modern-card heading-color rules already apply regardless of markdown-content nesting depth, confirmed by 68-UI-SPEC.md."

patterns-established: []

requirements-completed: [CHARMD-01, CHARMD-02]

coverage:
  - id: D1
    description: "Desktop Character Details renders Description and Backstory via Html.Markdown() inside the existing blank-field guards, with the required @using QuestBoard.Service.Extensions import and no leftover pre-wrap <p> wrapper"
    requirement: "CHARMD-01"
    verification:
      - kind: other
        ref: "dotnet build QuestBoard.Service/QuestBoard.Service.csproj -c Debug (0 errors, 0 warnings)"
        status: pass
      - kind: other
        ref: "grep -c '@using QuestBoard.Service.Extensions' Details.cshtml == 1; grep for form-control-plaintext pre-wrap returns 0 matches"
        status: pass
    human_judgment: false
  - id: D2
    description: "Mobile Character Details renders Description and Backstory via Html.Markdown() inside character-info-row, with the required @using import, and the mobile catch-all color rule gains an li selector so Markdown list items render full-opacity gold instead of muted"
    requirement: "CHARMD-02"
    verification:
      - kind: other
        ref: "dotnet build QuestBoard.Service/QuestBoard.Service.csproj -c Debug (0 errors, 0 warnings)"
        status: pass
      - kind: other
        ref: "grep -c '@using QuestBoard.Service.Extensions' Details.Mobile.cshtml == 1; grep -c '.character-detail-card li' character-detail.mobile.css == 1"
        status: pass
    human_judgment: true
    rationale: "Live formatted rendering, no-doubled-spacing visual confirmation, and mobile list-item color are explicitly deferred to 68-03's human-verify checkpoint per this plan's own <verification> section — build/grep checks prove the code is wired correctly but not the rendered visual result."

duration: 3min
completed: 2026-07-10
status: complete
---

# Phase 68 Plan 02: Character Details Markdown Rendering Summary

**Character Description and Backstory now render as sanitized Markdown HTML via `Html.Markdown()` on both desktop and mobile Character Details, replacing the old pre-wrap plain-text `<p>` wrappers, with mobile Markdown list items fixed to full-opacity gold.**

## Performance

- **Duration:** 3 min
- **Started:** 2026-07-10T11:16:49Z
- **Completed:** 2026-07-10T11:18:57Z
- **Tasks:** 2
- **Files modified:** 3

## Accomplishments
- Desktop `Details.cshtml` renders `Model.Description`/`Model.Backstory` via `@Html.Markdown(...)`, dropping the illegal pre-wrap `<p>` wrapper, with the required `@using QuestBoard.Service.Extensions` import added
- Mobile `Details.Mobile.cshtml` does the same inside `.character-info-row`, also with the required import
- `character-detail.mobile.css` catch-all color rule extended with an `li` selector so Markdown bullet/numbered list items render full-opacity gold instead of falling back to the muted `.character-info-value` color

## Task Commits

Each task was committed atomically:

1. **Task 1: Render Description + Backstory as HTML on desktop Details.cshtml** - `b4c1fed` (feat)
2. **Task 2: Render Description + Backstory as HTML on mobile Details.Mobile.cshtml + add li to the mobile catch-all** - `0b823a5` (feat)

_Note: worktree mode — plan metadata commit (SUMMARY.md) follows this commit; STATE.md/ROADMAP.md are updated centrally by the orchestrator after merge._

## Files Created/Modified
- `QuestBoard.Service/Views/Characters/Details.cshtml` - Added `@using QuestBoard.Service.Extensions`; replaced pre-wrap `<p>` wrappers for Description/Backstory with `@Html.Markdown(...)` calls
- `QuestBoard.Service/Views/Characters/Details.Mobile.cshtml` - Added `@using QuestBoard.Service.Extensions`; replaced `character-info-value` `<p>` wrappers for Description/Backstory with `@Html.Markdown(...)` calls
- `QuestBoard.Service/wwwroot/css/character-detail.mobile.css` - Added `.character-detail-card li` to the existing catch-all parchment color rule (same declaration as `p`/`a`/`span:not(.badge)`)

## Decisions Made
- Dropped the inline `style="white-space: pre-wrap;"` `<p>` wrappers entirely rather than relocating them — `.markdown-content { white-space: normal }` (Phase 67) already owns paragraph spacing, and Markdig's block-level HTML output cannot legally nest inside a `<p>`.
- Added zero new CSS on desktop — confirmed via 68-UI-SPEC.md that `site.css`'s `!important` `.modern-card` heading-color rules already win regardless of `.markdown-content` nesting depth, so no `.modern-card-body > .markdown-content` override was needed (and would have been dead CSS given Character Details' extra div-nesting level vs Quest Details).

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

None.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- Read side of CHARMD-01/CHARMD-02 complete and build-verified; ready for 68-03's live human-verify checkpoint (formatted rendering, no-doubled-spacing, mobile list-item color) and the `CharactersControllerIntegrationTests` gate mentioned in this plan's `<verification>` section.
- No blockers.

---
*Phase: 68-character-fields*
*Completed: 2026-07-10*

## Self-Check: PASSED

- FOUND: QuestBoard.Service/Views/Characters/Details.cshtml
- FOUND: QuestBoard.Service/Views/Characters/Details.Mobile.cshtml
- FOUND: QuestBoard.Service/wwwroot/css/character-detail.mobile.css
- FOUND: .planning/phases/68-character-fields/68-02-SUMMARY.md
- FOUND commit: b4c1fed (Task 1)
- FOUND commit: 0b823a5 (Task 2)
- FOUND commit: 56e2947 (docs: SUMMARY)
