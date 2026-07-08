---
phase: 42-site-wide-toast-notification-redesign
plan: 05
subsystem: ui
tags: [razor, bootstrap-toast, mvc, tempdata, admin, quest-management]

# Dependency graph
requires: ["42-01"]
provides:
  - "Admin Users page with local alert banners removed"
  - "Quest Manage (desktop + mobile) with local alert banners removed"
affects: []

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Continues Plan 01's shared toast partial pattern — no per-view local TempData alert markup"

key-files:
  created: []
  modified:
    - QuestBoard.Service/Views/Admin/Users.cshtml
    - QuestBoard.Service/Views/Quest/Manage.cshtml
    - QuestBoard.Service/Views/Quest/Manage.Mobile.cshtml

key-decisions:
  - "In Manage.cshtml, removed only the alert-dismissible wrapper markup around the Success message, preserving the embedded 'Send again (bypasses duplicate check)' reminder form that was nested inside the same @if (TempData[\"Success\"] != null) block"

patterns-established: []

requirements-completed: []

# Metrics
duration: 8min
completed: 2026-07-04
status: complete
---

# Phase 42 Plan 05: Root-Layout Alert Banner Removal (Admin Users, Quest Manage) Summary

**Removed the last 3 root-layout views' local TempData `alert-dismissible` flash markup (Admin Users, Quest Manage desktop + mobile), completing the app-wide migration to the shared toast partial from Plan 01.**

## Performance

- **Duration:** 8 min
- **Tasks:** 2
- **Files modified:** 3

## Accomplishments
- `Admin/Users.cshtml`: removed the three local `@if (TempData["Success"|"Error"|"Warning"] != null)` blocks wrapping `alert alert-dismissible` divs. The `modern-card` structure, "Create User" button, users table, and per-row action forms are untouched.
- `Quest/Manage.cshtml`: removed the local `Error` alert block and the `Success` alert block's wrapper div, while preserving the `SendReminder` "Send again (bypasses duplicate check)" form that was nested inside the same `Success` conditional — that form still renders whenever `TempData["Success"]` is set, now alongside a toast instead of an inline banner.
- `Quest/Manage.Mobile.cshtml`: removed the local `Error` alert-dismissible block. No embedded form was nested in this file's block, so removal was a straightforward deletion.
- All quest-management content (quest cards, date-voting UI, finalize/close/reopen actions, player selection) and the unrelated `Access Denied` non-TempData alert (an authorization guard, not a flash message) were left unchanged.
- `dotnet build` succeeds with 0 warnings, 0 errors.

## Task Commits

Each task was committed atomically:

1. **Task 1: Remove alert banners from Admin Users page** - `f8c414c` (feat)
2. **Task 2: Remove alert banners from Quest Manage (desktop + mobile)** - `67cc334` (feat)

_No plan-metadata commit in this run — orchestrator handles STATE.md/ROADMAP.md centrally after all worktree agents in the wave complete (worktree isolation mode)._

## Files Created/Modified
- `QuestBoard.Service/Views/Admin/Users.cshtml` - Removed 3 local TempData alert-dismissible blocks (Success/Error/Warning)
- `QuestBoard.Service/Views/Quest/Manage.cshtml` - Removed local TempData Error/Success alert-dismissible blocks; preserved the embedded SendReminder "Send again" form
- `QuestBoard.Service/Views/Quest/Manage.Mobile.cshtml` - Removed local TempData Error alert-dismissible block

## Decisions Made
- Manage.cshtml's Success block contained a nested `<form asp-action="SendReminder">` that must still render when `TempData["Success"]` is set (it's the "resend reminder, bypassing the duplicate-send guard" action, not decorative alert content). Only the `<div class="alert ...">...</div>` wrapper and its icon/button markup were deleted; the `@if (TempData["Success"] != null) { ... }` conditional itself was kept so the form's visibility trigger is unchanged.

## Deviations from Plan

None - plan executed exactly as written. Both tasks matched their planned scope; no Rule 1-4 auto-fixes were needed.

## Issues Encountered

None.

## Stub Tracking

No stubs introduced — this plan only deletes markup, adds nothing.

## Threat Flags

None — this plan only removes existing local flash-banner markup; no new surface introduced. Per the plan's own threat register (T-42-05-01), the shared partial (Plan 01) already handles rendering safely via plain `@TempData[...]` interpolation.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- All 3 remaining root-layout views with local alert-banner markup (Admin Users, Quest Manage desktop + mobile) are now migrated. Combined with Plans 01-04, no view in the app should render a local TempData `alert-dismissible` flash banner going forward — the phase-wide final grep check (per 42-05-PLAN.md's `<verification>` section) can now be run across all of `QuestBoard.Service` once all Wave 2 plans have landed.
- Manual/UAT verification of visual rendering (quest finalize/close/reopen action, admin user action, confirming top-right toast rendering on desktop and mobile) was not performed in this automated execution run — recommended per `42-VALIDATION.md` before the phase's final UAT gate.

---
*Phase: 42-site-wide-toast-notification-redesign*
*Completed: 2026-07-04*
