---
phase: 42-site-wide-toast-notification-redesign
plan: 02
subsystem: ui
tags: [razor, bootstrap-toast, mvc, tempdata, shop]

# Dependency graph
requires: ["42-01"]
provides:
  - "Shop's 4 toast-bearing views (Index/Details, desktop + mobile) migrated onto the shared _Toasts.cshtml partial"
affects: []

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Removed the last 4 duplicate per-view toast-init scripts; Shop views now rely solely on site.js's single init"

key-files:
  created: []
  modified:
    - QuestBoard.Service/Views/Shop/Index.cshtml
    - QuestBoard.Service/Views/Shop/Index.Mobile.cshtml
    - QuestBoard.Service/Views/Shop/Details.cshtml
    - QuestBoard.Service/Views/Shop/Details.Mobile.cshtml

key-decisions:
  - "Removed the now-empty @section Scripts block entirely from Details.cshtml and Details.Mobile.cshtml (both files had no other scripts once the generic toast-init was removed), per plan's own guidance to remove an empty section"
  - "Index.cshtml and Index.Mobile.cshtml kept @section Scripts since both retain non-toast scripts (modal-loading handler, purchase-button stopPropagation handler, and in Index.cshtml the Mystical Merchant novelty toast functions)"

patterns-established: []

requirements-completed: []

# Metrics
duration: 8min
completed: 2026-07-04
status: complete
---

# Phase 42 Plan 02: Shop Toast Migration Summary

**Removed local Bootstrap toast markup and duplicate init scripts from all 4 Shop views (Index/Details, desktop + mobile), delegating Success/Error/GoldReceived rendering to the shared `_Toasts.cshtml` partial while leaving the Mystical Merchant novelty toast completely untouched.**

## Performance

- **Duration:** 8 min
- **Tasks:** 2
- **Files modified:** 4

## Accomplishments
- `Views/Shop/Index.cshtml` and `Index.Mobile.cshtml` no longer render a local `.toast-container` for Success/Error, and no longer run a generic per-view `querySelectorAll('.toast')` bulk-init — both now rely on the shared partial + site.js's single init.
- `Views/Shop/Details.cshtml` and `Details.Mobile.cshtml` no longer render a local `.toast-container` for Success/Error/GoldReceived — the bespoke "+X gp" GoldReceived toast (ported to `_Toasts.cshtml` in Plan 01) now lives only in the shared partial.
- The Mystical Merchant novelty toast (`showMerchantDialog`/`showMerchantToast`) in `Index.cshtml` is fully preserved, including its own dynamic `new bootstrap.Toast(newToast)` self-init — it appends into whichever `.toast-container` is present in the DOM, which after this migration is the shared partial's container rendered from the layout.
- Modal-loading (`show.bs.modal`) and purchase-button `stopPropagation` handlers in both Index views are untouched.
- `dotnet build` succeeds with 0 warnings, 0 errors.
- Repo-wide grep for `new bootstrap.Toast` returns exactly 2 sites: `site.js`'s consolidated init and `Index.cshtml`'s `showMerchantToast()`, confirming no duplicate init paths remain anywhere in Shop.

## Task Commits

Each task was committed atomically:

1. **Task 1: Remove local toast container + init from Shop Index (desktop + mobile), preserving the merchant toast** - `0c7abdd` (feat)
2. **Task 2: Remove local toast container + init from Shop Details (desktop + mobile), including the local GoldReceived block** - `15bb13a` (feat)

_No plan-metadata commit in this run — orchestrator handles STATE.md/ROADMAP.md centrally after all worktree agents in the wave complete (worktree isolation mode)._

## Files Created/Modified
- `QuestBoard.Service/Views/Shop/Index.cshtml` - Removed local Success/Error toast-container and the generic bulk-init script; merchant toast functions and other scripts (modal-loading, purchase-button stopPropagation) preserved
- `QuestBoard.Service/Views/Shop/Index.Mobile.cshtml` - Removed local Success/Error toast-container and the generic bulk-init script; modal-loading and purchase-button handlers preserved
- `QuestBoard.Service/Views/Shop/Details.cshtml` - Removed local Success/Error/GoldReceived toast-container and the now-empty `@section Scripts` block entirely
- `QuestBoard.Service/Views/Shop/Details.Mobile.cshtml` - Removed local Success/Error/GoldReceived toast-container and the now-empty `@section Scripts` block entirely

## Decisions Made
- Removed the empty `@section Scripts` block entirely from both Details views since no other scripts remained after the toast-init removal, per the plan's explicit guidance.
- Kept `@section Scripts` in both Index views since they retain non-toast scripts (modal-loading, purchase-button handler, and Index.cshtml's merchant toast functions).

## Deviations from Plan

None - plan executed exactly as written. Both tasks matched their planned scope; no Rule 1-4 auto-fixes were needed.

## Issues Encountered

None.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- Shop's 4 toast-bearing views are now fully migrated onto the shared partial; no local toast markup or duplicate init scripts remain in Shop.
- The site-wide toast unification (across Wave 2 plans 42-02 through 42-05) is one plan closer to complete — this plan's scope (Shop) is done.
- Manual/UAT verification of visual rendering (GoldReceived + Success toast appearing together after a sale; merchant dialog styling/behavior) was not performed in this automated execution run, consistent with 42-01's note that this project has no automated test suite — recommended before/at the phase's final UAT gate per `42-VALIDATION.md`.

---
*Phase: 42-site-wide-toast-notification-redesign*
*Completed: 2026-07-04*
