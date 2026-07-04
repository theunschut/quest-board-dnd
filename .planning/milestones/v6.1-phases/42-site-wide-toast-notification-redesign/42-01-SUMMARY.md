---
phase: 42-site-wide-toast-notification-redesign
plan: 01
subsystem: ui
tags: [razor, bootstrap-toast, mvc, tempdata, controller-extensions]

# Dependency graph
requires: []
provides:
  - "Shared `_Toasts.cshtml` partial rendering Success/Error/Warning/Info/GoldReceived from TempData"
  - "All 5 layouts wired with `<partial name=\"_Toasts\" />`"
  - "`RedirectWithInfo` controller extension helper"
  - "Standardized `Success`/`Error`/`Info` TempData keys in AccountController"
  - "Consolidated toast-init logic in site.js's single DOMContentLoaded listener"
affects: [42-02, 42-03, 42-04, 42-05]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Single shared Razor partial for all TempData-driven flash/toast rendering, replacing per-view local markup"
    - "One site-wide DOMContentLoaded listener for toast bulk-init instead of per-view inline scripts"

key-files:
  created:
    - QuestBoard.Service/Views/Shared/_Toasts.cshtml
  modified:
    - QuestBoard.Service/Views/Shared/_Layout.cshtml
    - QuestBoard.Service/Views/Shared/_Layout.Mobile.cshtml
    - QuestBoard.Service/Views/Shared/_Layout.GroupPicker.cshtml
    - QuestBoard.Service/Areas/Platform/Views/Shared/_Layout.Platform.cshtml
    - QuestBoard.Service/Areas/Platform/Views/Shared/_Layout.Platform.Mobile.cshtml
    - QuestBoard.Service/Extensions/ControllerExtensions.cs
    - QuestBoard.Service/Controllers/Admin/AccountController.cs
    - QuestBoard.Service/wwwroot/js/site.js

key-decisions:
  - "Wired the shared partial into all 5 layouts (not the 3 named in CONTEXT.md), including the Platform-area pair, per RESEARCH Discrepancy #1 — those views resolve to _Layout.Platform.cshtml, not the root layouts"
  - "Standardized AccountController's outlier TempData keys (SuccessMessage/ErrorMessage/InfoMessage) to the standard Success/Error/Info scheme so _Toasts.cshtml reads one consistent key set with no dual-scheme branching"

patterns-established:
  - "Toast rendering centralized in one partial read from every layout; no per-view local toast/alert markup going forward (removed in Wave 2)"
  - "Toast bulk-init lives in a single site-wide site.js DOMContentLoaded listener; no per-view inline init scripts"

requirements-completed: []

# Metrics
duration: 25min
completed: 2026-07-04
status: complete
---

# Phase 42 Plan 01: Shared Toast Partial Foundation Summary

**Built a single shared `_Toasts.cshtml` partial (4 generic types + bespoke GoldReceived) wired into all 5 layouts, added a `RedirectWithInfo` controller helper, and standardized AccountController's TempData keys — the foundation Wave 2 migration plans depend on.**

## Performance

- **Duration:** 25 min
- **Started:** 2026-07-04T14:40:00Z (approx, per session state)
- **Completed:** 2026-07-04T15:05:03Z
- **Tasks:** 5
- **Files modified:** 9 (1 created, 8 modified)

## Accomplishments
- Shared `_Toasts.cshtml` partial renders Success (auto-hide 5000ms), Error (sticky), Warning (sticky), Info (auto-hide 5000ms, new type), and GoldReceived (auto-hide 6000ms) toasts from TempData — solid colored header bars, correct FontAwesome icon per type, no `@Html.Raw`.
- All 5 layouts (`_Layout.cshtml`, `_Layout.Mobile.cshtml`, `_Layout.GroupPicker.cshtml`, `_Layout.Platform.cshtml`, `_Layout.Platform.Mobile.cshtml`) now render `<partial name="_Toasts" />` immediately after `@RenderBody()`, including the Platform-area pair the CONTEXT.md literal text initially omitted.
- `RedirectWithInfo` extension method added to `ControllerExtensions.cs`, mirroring the existing `RedirectWithSuccess`/`RedirectWithError`/`RedirectWithWarning` one-line pattern.
- `AccountController.cs`'s three outlier TempData keys (`SuccessMessage`/`ErrorMessage`/`InfoMessage`) renamed to the standard `Success`/`Error`/`Info` scheme — message strings and redirect targets unchanged.
- Toast-init JS consolidated into `site.js`'s single existing `DOMContentLoaded` listener (bulk `querySelectorAll('.toast')` → `new bootstrap.Toast(el).show()`); no second listener registered.
- `dotnet build` succeeds with 0 warnings, 0 errors.

## Task Commits

Each task was committed atomically:

1. **Task 1: Create shared _Toasts.cshtml partial with all 4 generic types plus GoldReceived** - `9cb5dc5` (feat)
2. **Task 2: Wire the shared partial into all 5 layouts** - `9de0f87` (feat)
3. **Task 3: Add RedirectWithInfo helper to ControllerExtensions.cs** - `84470ae` (feat)
4. **Task 4: Standardize AccountController TempData keys to Success/Error/Info** - `3ea45f4` (fix)
5. **Task 5: Consolidate toast-init into the existing site.js DOMContentLoaded listener** - `54d1358` (feat)

_No plan-metadata commit in this run — orchestrator handles STATE.md/ROADMAP.md centrally after all worktree agents in the wave complete (worktree isolation mode)._

## Files Created/Modified
- `QuestBoard.Service/Views/Shared/_Toasts.cshtml` - New shared partial; all 4 generic toast types + bespoke GoldReceived block
- `QuestBoard.Service/Views/Shared/_Layout.cshtml` - Added `<partial name="_Toasts" />` after `@RenderBody()`
- `QuestBoard.Service/Views/Shared/_Layout.Mobile.cshtml` - Added `<partial name="_Toasts" />` after `@RenderBody()`
- `QuestBoard.Service/Views/Shared/_Layout.GroupPicker.cshtml` - Added `<partial name="_Toasts" />` after `@RenderBody()`
- `QuestBoard.Service/Areas/Platform/Views/Shared/_Layout.Platform.cshtml` - Added `<partial name="_Toasts" />` after `@RenderBody()`
- `QuestBoard.Service/Areas/Platform/Views/Shared/_Layout.Platform.Mobile.cshtml` - Added `<partial name="_Toasts" />` after `@RenderBody()` (dead-code layout, wired for future-proofing per RESEARCH)
- `QuestBoard.Service/Extensions/ControllerExtensions.cs` - Added `RedirectWithInfo` extension method
- `QuestBoard.Service/Controllers/Admin/AccountController.cs` - Renamed 3 outlier TempData key writes to standard scheme
- `QuestBoard.Service/wwwroot/js/site.js` - Appended toast bulk-init to the existing `DOMContentLoaded` listener

## Decisions Made
- Wired all 5 layouts (not the 3 CONTEXT.md's literal text named), including the Platform-area pair `_Layout.Platform.cshtml`/`_Layout.Platform.Mobile.cshtml` — per RESEARCH's Critical Discrepancy #1, Platform-area views resolve to a separate layout pair via `Areas/Platform/Views/_ViewStart.cshtml`, not the root layouts. Omitting them would leave those views with no toast container once Wave 2 removes their local alert markup.
- Standardized AccountController's TempData keys to the app-wide `Success`/`Error`/`Info` scheme (Claude's Discretion lean, confirmed in UI-SPEC) rather than adding dual-scheme branching logic to `_Toasts.cshtml`.

## Deviations from Plan

None - plan executed exactly as written. All 5 tasks matched their planned scope; no Rule 1-4 auto-fixes were needed.

## Issues Encountered

None.

**Intentional new-visible behavior (not a regression — expected per Task 4 and RESEARCH Pitfall 2):** As a direct consequence of the TempData key standardization in Task 4, two previously-silent AccountController code paths will now visibly render for the first time:
1. `ConfirmEmailChange`'s Error path — an expired/invalid email-change confirmation link now shows an Error toast on the Login page (previously wrote `TempData["ErrorMessage"]`, which `Login` never read).
2. `Edit`'s email-change confirmation path — requesting an email change from Profile's Edit form now shows an Info toast on the Profile page (previously wrote `TempData["InfoMessage"]`, which `Profile` never read).

Both paths were silently dropping their messages before this phase due to a key mismatch between the writer and the reader. This phase fixes that as a side effect of standardizing on one key scheme — it is the previously-broken path becoming visible, not scope creep.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- The shared `_Toasts.cshtml` partial, all 5 layout wirings, `RedirectWithInfo`, and standardized TempData keys are all in place and building cleanly (`dotnet build`: 0 warnings, 0 errors).
- Wave 2 plans (42-02 through 42-05) can now safely remove per-view local alert-banner/toast markup and rely on the shared partial — the enabling foundation this plan was scoped to deliver is complete.
- Note for Wave 2 planners/executors: until the Wave 2 migration plans run, some views may transiently show BOTH the old local alert/toast markup AND the new shared-partial toast for the same flash message — this is expected mid-migration per the plan's own verification notes and resolves once Plans 02-05 remove the local markup.
- Manual/UAT verification of visual rendering (per-layout smoke test, autohide timing, Info/Warning toast appearance) was not performed in this automated execution run — this project has no automated test suite (confirmed in RESEARCH), so a human-verify pass across the 5 layouts is recommended before/at the phase's final UAT gate, consistent with this milestone's established pattern.

---
*Phase: 42-site-wide-toast-notification-redesign*
*Completed: 2026-07-04*

## Self-Check: PASSED

- FOUND: QuestBoard.Service/Views/Shared/_Toasts.cshtml
- FOUND: .planning/phases/42-site-wide-toast-notification-redesign/42-01-SUMMARY.md
- FOUND: 9cb5dc5 (Task 1 commit)
- FOUND: 9de0f87 (Task 2 commit)
- FOUND: 84470ae (Task 3 commit)
- FOUND: 3ea45f4 (Task 4 commit)
- FOUND: 54d1358 (Task 5 commit)
- FOUND: b82e5db (Summary commit)
