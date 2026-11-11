---
phase: 102-board-deletion
plan: 08
subsystem: platform-ui
tags: [razor, css, es-module, typed-name-gate, static-guard]
requires:
  - phase: 102-07
    provides: board delete confirmation page and shared body partial
  - phase: 102-02
    provides: ActiveBoardSessionExtensions (scanned by the guard)
  - phase: 102-03
    provides: platform-users-boards.mobile.css (guarded here)
provides:
  - desktop and phone pages-layer sheets for the Delete page
  - typed-name gate module (exact trimmed ordinal match unlocks Delete board)
  - static guard and rendered disabled-state check for the new files
affects: [102-09, UAT]
tech-stack:
  added: []
  patterns: [per-modifier custom property for a row stripe, module registered after site.js to win the pageshow race]
key-files:
  created:
    - QuestBoard.Service/wwwroot/css/platform-group-delete.css
    - QuestBoard.Service/wwwroot/css/platform-group-delete.mobile.css
    - QuestBoard.Service/wwwroot/js/platform-board-delete.js
    - QuestBoard.IntegrationTests/Tests/BoardDeletionStaticGuardTests.cs
    - QuestBoard.IntegrationTests/Controllers/BoardDeletePageGateRenderTests.cs
  modified:
    - QuestBoard.Service/Areas/Platform/Views/Group/Delete.cshtml
    - QuestBoard.Service/Areas/Platform/Views/Group/Delete.Mobile.cshtml
    - QuestBoard.Service/Areas/Platform/Views/Group/_BoardDeleteBody.cshtml
key-decisions:
  - "Legacy platform-group.mobile.css left untouched; the new sheets already meet the converted definition so the later UI conversion phase only verifies the Delete page"
patterns-established:
  - "Server-rendered disabled submit plus a module gate; server stays authoritative"
requirements-completed: [D-08, D-10]
status: complete
actuals:
  tokens: 28000
  tasks: 2
  commits: 2
duration: 20min
completed: 2026-11-11
---

# Phase 102 Plan 08: Delete page sheets and typed-name gate Summary

**Tokens-only pages-layer sheets for both Delete layouts plus an ES module that unlocks the server-disabled Delete board button only on an exact trimmed, case-sensitive board-name match.**

## Performance

- 2 tasks, 2 commits, no deviations
- Verification: `dotnet test QuestBoard.IntegrationTests --filter "BoardDeletionStaticGuard|BoardDeletePageGateRender|PlatformBoardDelete|UiFoundation|MobileCssTests|BoardPageStaticGuardTests"` passed 201/201 (189 before the new tests, 12 new cases).

## Accomplishments

- `platform-group-delete.css` and `platform-group-delete.mobile.css`: everything inside `@layer pages`, tokens only, no priority flag, no colour literal, no media query in the phone sheet. Row stripe colour is a per-modifier custom property (danger, info, neutral text tokens). Rows, hints, name lists, help text and the status line use `overflow-wrap: anywhere` and `min-width: 0`, so long names never force horizontal scroll. The phone sheet sizes each `summary` at `--qb-hit-min` with `display: flex; align-items: center`.
- Both twins load their own sheet through `@section Styles` and the gate module through `@section Scripts` as `type="module"`. The partial's headings carry `board-delete-heading` and the action row has `gap-2`.
- `platform-board-delete.js`: the button is rendered `disabled` by the server; `sync()` enables it only when `input.value.trim()` equals the data-attribute name. The status line ("Name matches. You can delete the board now.") is written only when the matched state changes, built with `createElement`/`createTextNode` and cleared with `replaceChildren()`. The form `submit` handler blocks a disabled submit and remembers a real one; a `pageshow` listener (registered after `site.js`) clears the submitted flag on a persisted restore and re-syncs.
- `BoardDeletionStaticGuardTests` (10 tests): no inline code, no unencoded HTML, twins link their own sheet and the module (phone twin never links the legacy group phone sheet), pages-layer/priority/colour rules over three sheets, no media query in phone sheets, no markup sinks in the module, `pageshow` and `trim()` present, no planning references across views, partial, sheets, module and all new C# files, and a scanner self-test.
- `BoardDeletePageGateRenderTests`: Theory over desktop and phone User-Agent asserting the rendered button carries `data-board-delete-submit disabled` and the module script is loaded (the held-out check behind the no-JS backstop).

## Task Commits

1. Task 1: sheets and Styles wiring - `51ef0a9d`
2. Task 2: gate module, disabled button, guard and render test - `a1ed6440`

## Deviations from Plan

None - plan executed as written. The guard's C# file list uses the actual locations of `BoardDeleteStatus.cs` and `BoardContentKind.cs` (`QuestBoard.Domain/Enums/`).

## Known Stubs

None.

## Threat Flags

None. T-102-22 (server stays authoritative), T-102-23 (no HTML-string sinks, enforced by `GateModule_WritesNoMarkupFromStrings`) and T-102-32 (`DeleteViews_CarryNoInlineStyleScriptOrHandler`) are mitigated as planned.

## Manual verification still owed (backstops)

- bfcache: after a back-forward restore the Delete button state matches the input's current value (UAT by hand).
- no-JS: the button never unlocks without JavaScript (covered by the rendered disabled-state test; unlock path is browser-only).

## Self-Check: PASSED

All five created files exist and commits `51ef0a9d` and `a1ed6440` are in the branch history.
