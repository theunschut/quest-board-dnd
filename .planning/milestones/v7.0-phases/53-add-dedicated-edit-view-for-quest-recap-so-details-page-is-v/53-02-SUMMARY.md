---
phase: 53-add-dedicated-edit-view-for-quest-recap-so-details-page-is-v
plan: 02
subsystem: ui
tags: [aspnet-core-mvc, razor, modern-card, quest-log]

# Dependency graph
requires:
  - phase: 53-01
    provides: "EditRecapViewModel + QuestLogController.EditRecap GET/POST action pair with two-layer DM/Admin authorization"
provides:
  - "Dedicated EditRecap page (desktop modern-card + mobile quest-edit-card-mobile) with Save Recap / Cancel per D-03"
  - "Read-only Session Recap display on Details/Details.Mobile for all users, no inline edit form"
  - "Dynamic Add Recap / Edit Recap entry-point button on Details gated by ViewBag.CanEditRecap (D-01/D-02)"
affects: []

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "New page-scoped edit views mirror an existing modern-card analog (Quest/Edit) verbatim for header/button-row conventions, deviating only where the UI-SPEC explicitly calls out (fa-book not fa-edit, no tips sidebar, no btn-warning)"
    - "View-only DM/Admin affordances (edit buttons) are defense-in-depth only — the authoritative check stays server-side on the action being linked to"

key-files:
  created:
    - QuestBoard.Service/Views/QuestLog/EditRecap.cshtml
    - QuestBoard.Service/Views/QuestLog/EditRecap.Mobile.cshtml
  modified:
    - QuestBoard.Service/Views/QuestLog/Details.cshtml
    - QuestBoard.Service/Views/QuestLog/Details.Mobile.cshtml
    - QuestBoard.IntegrationTests/Controllers/QuestLogControllerIntegrationTests.cs

key-decisions:
  - "Session Recap read-only display renders unconditionally (not gated behind !CanEditRecap) so DM/Admin also sees the clean read-only view, with the edit affordance surfaced only as a separate button below it"

patterns-established:
  - "Recap edit affordance moved off the Details page entirely onto its own dedicated page, following the same modern-card (desktop) / quest-edit-card-mobile (mobile) shape as Quest/Edit, rather than an inline conditional form embedded in a detail/show page"

requirements-completed: []

# Metrics
duration: ~20min
completed: 2026-07-06
---

# Phase 53 Plan 02: Dedicated EditRecap View Summary

**Split the Session Recap UI onto its own `EditRecap` page (desktop modern-card + mobile quest-edit-card-mobile) with Save/Cancel, leaving Details read-only for everyone with a dynamic Add/Edit entry-point button — human-verified on desktop and mobile.**

## Performance

- **Duration:** ~20 min
- **Started:** 2026-07-06T09:35:00Z
- **Completed:** 2026-07-06T09:55:00Z
- **Tasks:** 3 completed (2 auto + 1 checkpoint)
- **Files modified:** 5 (2 created, 3 modified)

## Accomplishments
- `EditRecap.cshtml` created: modern-card, `fa-book` header, single `col-lg-8 col-md-7` column (no tips sidebar), `rows="10"` textarea, Cancel (secondary, left) / Save Recap (primary, right) per D-03
- `EditRecap.Mobile.cshtml` created: `quest-edit-card-mobile`, reuses `quest-edit.mobile.css`, `rows="6"` textarea, `d-flex gap-2` + `flex-fill` button row
- `Details.cshtml` / `Details.Mobile.cshtml` Session Recap block now renders read-only unconditionally for all users (`recap-display-box` or "No recap has been written for this quest yet."), with the old inline `UpdateRecap` form removed entirely
- DM/Admin sees a dynamic "Add Recap" (`fa-plus`, empty recap) / "Edit Recap" (`fa-edit`, existing recap) button under the recap content on both Details views, linking to the new `EditRecap` page
- Human verification checkpoint (desktop + mobile, read-only display, Add/Edit button, Save persists, Cancel discards) — **approved** by the developer

## Task Commits

Each task was committed atomically:

1. **Task 1: Create the desktop and mobile EditRecap views** - `38a8ae2` (feat)
2. **Task 2: Convert Details recap sections to read-only + entry-point button** - `6b0df63` (feat)
   - **Deviation fix (Rule 1 - Bug):** `e81469c` (fix) — stale test assertion, see below
3. **Task 3: Human verification of the read-only Details + dedicated EditRecap flow** - checkpoint, resolved via developer response: **"approved"**

**Plan metadata:** committed alongside this SUMMARY (worktree mode — orchestrator finalizes shared-file updates after merge)

## Files Created/Modified
- `QuestBoard.Service/Views/QuestLog/EditRecap.cshtml` - Desktop dedicated recap edit form (modern-card)
- `QuestBoard.Service/Views/QuestLog/EditRecap.Mobile.cshtml` - Mobile dedicated recap edit form (quest-edit-card-mobile)
- `QuestBoard.Service/Views/QuestLog/Details.cshtml` - Read-only recap display + conditional Add/Edit entry-point button
- `QuestBoard.Service/Views/QuestLog/Details.Mobile.cshtml` - Read-only recap display + conditional Add/Edit entry-point button (mobile)
- `QuestBoard.IntegrationTests/Controllers/QuestLogControllerIntegrationTests.cs` - Updated `Details_NonOwnerAdmin_SeesRecapEditMarker` to assert on the `/QuestLog/EditRecap/{id}` link instead of the removed inline "Save Recap" text

## Decisions Made
- Read-only recap display on Details is rendered unconditionally (not gated behind `!CanEditRecap`), so the DM/Admin also gets the clean read-only presentation, with editing surfaced purely via the separate entry-point button — matching the plan's explicit intent that Details becomes view-only for everyone.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Fixed stale Details recap-edit-marker test assertion**
- **Found during:** Task 2 (Convert Details recap sections to read-only + entry-point button)
- **Issue:** `Details_NonOwnerAdmin_SeesRecapEditMarker` asserted the Details page response body contained "Save Recap" text — true only while the inline recap edit form lived on Details. After Task 2 removed that inline form, the assertion no longer matched anything the Details page renders, since the edit affordance is now a link to the dedicated `EditRecap` page.
- **Fix:** Updated the test to assert on the `/QuestLog/EditRecap/{id}` link instead, preserving the original regression coverage (a non-owner Admin correctly sees the edit entry point) against the new UI shape.
- **Files modified:** `QuestBoard.IntegrationTests/Controllers/QuestLogControllerIntegrationTests.cs`
- **Verification:** Test asserts against the new link markup; consistent with the acceptance criteria in Task 2 (`Url.Action("EditRecap", "QuestLog"` present on Details).
- **Committed in:** `e81469c`

---

**Total deviations:** 1 auto-fixed (1 bug fix)
**Impact on plan:** Necessary correctness fix — the test was asserting on UI markup the plan itself removed. No scope creep.

## Issues Encountered

None beyond the deviation above.

## Human Verification

**Checkpoint (Task 3):** Developer ran the app and manually verified, on both desktop and mobile:
1. Session Recap renders read-only on Details (no inline textarea)
2. Dynamic Add Recap (+ icon) / Edit Recap (pencil icon) button appears for DM/Admin
3. Button navigates to the modern-card EditRecap page, textarea prefilled, Save Recap (primary, right) / Cancel (secondary, left)
4. Save persists and returns to Details with updated text shown read-only
5. Cancel returns to Details without saving

**Outcome:** Developer responded **"approved"** — all steps confirmed working on both desktop and mobile.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

Phase 53 (dedicated EditRecap view) is complete: Details is read-only for everyone, the DM/Admin has a clear Add/Edit entry point, and the dedicated EditRecap page handles Save/Cancel per D-03. No blockers for future phases.

---
*Phase: 53-add-dedicated-edit-view-for-quest-recap-so-details-page-is-v*
*Completed: 2026-07-06*

## Self-Check: PASSED

All created/modified files and commit hashes (`38a8ae2`, `6b0df63`, `e81469c`) verified present in the worktree and git history.
