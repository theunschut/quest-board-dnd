---
phase: 46-client-side-crop-ui
plan: 04
subsystem: views
tags: [crop, image-display, razor-views, mvc]

# Dependency graph
requires:
  - phase: 46-client-side-crop-ui (plan 03)
    provides: GetCroppedPicture (Characters), GetCroppedContactImage (Contacts) read actions; GetDMProfilePicture repointed in place
provides:
  - Every read-only avatar/thumbnail outside Character Details and Contact Details now serves the cropped image endpoint
affects: []

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Repoint-only view edits: change only the Url.Action action-name string literal, leaving route object/alt/class/style/onerror attributes untouched"

key-files:
  created: []
  modified:
    - QuestBoard.Service/Views/Characters/Index.cshtml
    - QuestBoard.Service/Views/Characters/Index.Mobile.cshtml
    - QuestBoard.Service/Views/Contacts/Index.cshtml
    - QuestBoard.Service/Views/Contacts/Index.Mobile.cshtml
    - QuestBoard.Service/Views/Quest/Details.cshtml
    - QuestBoard.Service/Views/Quest/Manage.cshtml
    - QuestBoard.Service/Views/Quest/_QuestCard.cshtml
    - QuestBoard.Service/Views/QuestLog/Details.cshtml
    - QuestBoard.Service/Views/QuestLog/Details.Mobile.cshtml

key-decisions: []

patterns-established: []

requirements-completed: [IMAGE-04]

# Metrics
duration: 12min
completed: 2026-07-07
---

# Phase 46 Plan 04: Repoint Read-Only Avatars to Cropped Endpoints Summary

**Nine read-only views (11 `src` occurrences) repointed from the original-read image actions to Plan 03's new cropped-read actions, leaving Character Details and Contact Details as the only two pages still serving the original image per D-03.**

## Performance

- **Duration:** 12 min
- **Started:** 2026-07-07T16:05:00Z
- **Completed:** 2026-07-07T16:17:54Z
- **Tasks:** 2
- **Files modified:** 9

## Accomplishments
- `Characters/Index.cshtml` and `Index.Mobile.cshtml` (2 occurrences each) now call `GetCroppedPicture` instead of `GetProfilePicture`.
- `Contacts/Index.cshtml` and `Index.Mobile.cshtml` now call `GetCroppedContactImage` instead of `GetContactImage`.
- `Quest/Details.cshtml` (both the selected-participants table and the waitlist table), `Quest/Manage.cshtml`, `Quest/_QuestCard.cshtml`, `QuestLog/Details.cshtml`, and `QuestLog/Details.Mobile.cshtml` all repointed their cross-controller `Url.Action("GetProfilePicture", "Characters", ...)` calls to `"GetCroppedPicture"`, keeping the `"Characters"` controller argument and route object untouched.
- Verified via `grep` that no `GetProfilePicture` reference remains anywhere under `Views/Quest/` or `Views/QuestLog/`, and none remains in the four repointed Character/Contact index views.
- Verified the four Details views (`Characters/Details.cshtml`, `Details.Mobile.cshtml`, `Contacts/Details.cshtml`, `Details.Mobile.cshtml`) were left untouched and still call the original-read actions (`GetProfilePicture`/`GetContactImage`), preserving D-03's cropped-vs-original display rule.
- `dotnet build QuestBoard.Service` succeeds with 0 warnings/0 errors, confirming all Razor view changes compile cleanly.

## Task Commits

Each task was committed atomically:

1. **Task 1: Repoint Characters/Contacts index thumbnails to the cropped-read actions** - `6a8e7ea` (feat)
2. **Task 2: Repoint Quest and QuestLog participant avatars to GetCroppedPicture** - `1381e88` (feat)

## Files Created/Modified
- `QuestBoard.Service/Views/Characters/Index.cshtml` - Both `GetProfilePicture` occurrences (My Characters + Character Roster sections) repointed to `GetCroppedPicture`
- `QuestBoard.Service/Views/Characters/Index.Mobile.cshtml` - Both `GetProfilePicture` occurrences repointed to `GetCroppedPicture`
- `QuestBoard.Service/Views/Contacts/Index.cshtml` - `GetContactImage` repointed to `GetCroppedContactImage`
- `QuestBoard.Service/Views/Contacts/Index.Mobile.cshtml` - `GetContactImage` repointed to `GetCroppedContactImage`
- `QuestBoard.Service/Views/Quest/Details.cshtml` - Both selected-participant and waitlist-participant avatar `Url.Action` calls repointed to `GetCroppedPicture`
- `QuestBoard.Service/Views/Quest/Manage.cshtml` - Participant roster avatar repointed to `GetCroppedPicture`
- `QuestBoard.Service/Views/Quest/_QuestCard.cshtml` - Inline quest-board card avatar repointed to `GetCroppedPicture`
- `QuestBoard.Service/Views/QuestLog/Details.cshtml` - Recap participant avatar repointed to `GetCroppedPicture`
- `QuestBoard.Service/Views/QuestLog/Details.Mobile.cshtml` - Recap participant avatar repointed to `GetCroppedPicture`

## Decisions Made
None beyond the plan's own explicit action-name-only repoint instruction — no discretion was required.

## Deviations from Plan

None - plan executed exactly as written. All 11 occurrences across 9 files matched the plan's exact-line inventory; no unexpected occurrences were found and no Details view needed touching.

## Issues Encountered
None.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- Every read-only avatar/thumbnail outside Character Details and Contact Details now serves the cropped image endpoint, per D-03.
- Character Details and Contact Details remain untouched and still serve the original image.
- `dotnet build QuestBoard.Service` succeeds (Razor compiles), confirming no markup regressions.
- No blockers for subsequent plans in this phase (Plan 06's upload-view preview repoints and the crop-modal UI itself remain out of scope for this plan, as documented in the plan's `<interfaces>` section).

---
*Phase: 46-client-side-crop-ui*
*Completed: 2026-07-07*

## Self-Check: PASSED

All 9 claimed modified files found on disk; both task commit hashes (`6a8e7ea`, `1381e88`) found in git history; `dotnet build QuestBoard.Service` verified 0 errors/0 warnings.
