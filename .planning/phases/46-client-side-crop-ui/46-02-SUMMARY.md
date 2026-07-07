---
phase: 46-client-side-crop-ui
plan: 02
subsystem: ui
tags: [css, aspect-ratio, characters, contacts, responsive]

# Dependency graph
requires:
  - phase: 45-dual-image-storage-backend
    provides: dual-image storage backend (OriginalImageData/CroppedImageData columns) that this UI work displays against
provides:
  - Square (1:1) list-card image boxes for Characters and Contacts index pages
  - Removal of fixed-height mobile overrides that were re-cropping square images
affects: [46-client-side-crop-ui remaining plans, especially the human-verify checkpoint in Plan 07]

# Tech tracking
tech-stack:
  added: []
  patterns: ["CSS aspect-ratio for responsive square boxes, replacing fixed-height + object-fit:cover"]

key-files:
  created: []
  modified:
    - QuestBoard.Service/wwwroot/css/characters.css
    - QuestBoard.Service/wwwroot/css/contacts.css

key-decisions:
  - "Used aspect-ratio: 1 / 1 plus height: auto on the base rule instead of a fixed pixel height, so the square scales at any grid column width without a mobile-specific override"

patterns-established:
  - "Square list-card image containers: aspect-ratio: 1 / 1; height: auto; on the base rule, no @media height override needed"

requirements-completed: [IMAGE-04]

# Metrics
duration: 5min
completed: 2026-07-07
---

# Phase 46 Plan 02: Convert List-Card Image Containers to Square Boxes Summary

**Characters and Contacts index list-card image boxes changed from fixed-height (200px desktop / 180px mobile) landscape boxes to responsive 1:1 aspect-ratio squares, so square-cropped photos display without `object-fit: cover` re-cropping them top/bottom.**

## Performance

- **Duration:** 5 min
- **Started:** 2026-07-07T14:01:00Z
- **Completed:** 2026-07-07T14:06:36Z
- **Tasks:** 1 completed
- **Files modified:** 2

## Accomplishments
- `.characters-page .character-image` now uses `aspect-ratio: 1 / 1; height: auto;` instead of `height: 200px`
- `.contacts-page .contact-image` now uses `aspect-ratio: 1 / 1; height: auto;` instead of `height: 200px`
- Removed the `@media (max-width: 768px)` `height: 180px` override from both files — the aspect-ratio rule already sizes the box responsively at any column width
- DM profile CSS (`dm-profile.css` / `dm-profile.mobile.css`) intentionally left untouched — its circle box is already square

## Task Commits

Each task was committed atomically:

1. **Task 1: Convert .character-image and .contact-image to a 1:1 square box and remove the mobile height overrides** - `af8f389` (feat)

**Plan metadata:** (this commit, docs: complete plan)

## Files Created/Modified
- `QuestBoard.Service/wwwroot/css/characters.css` - `.characters-page .character-image` rule converted to aspect-ratio square; mobile `height: 180px` override line removed
- `QuestBoard.Service/wwwroot/css/contacts.css` - `.contacts-page .contact-image` rule converted to aspect-ratio square; mobile `height: 180px` override line removed

## Decisions Made
None beyond what the plan specified — followed the plan's exact before/after CSS shape from 46-UI-SPEC.md and 46-PATTERNS.md.

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

None.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- Square list-card boxes are in place for both Characters and Contacts index pages, ready for the crop-UI plans in this phase to produce square-cropped images that will display correctly without re-cropping.
- Visual confirmation of the square list-card rendering is deferred to the human-verify checkpoint in Plan 07, per this plan's own verification section — not gated here.
- No blockers for subsequent plans in Phase 46.

---
*Phase: 46-client-side-crop-ui*
*Completed: 2026-07-07*

## Self-Check: PASSED

- FOUND: QuestBoard.Service/wwwroot/css/characters.css
- FOUND: QuestBoard.Service/wwwroot/css/contacts.css
- FOUND: .planning/phases/46-client-side-crop-ui/46-02-SUMMARY.md
- FOUND commit: af8f389 (feat(46-02): convert character/contact list-card images to 1:1 square boxes)
- FOUND commit: 0881149 (docs(46-02): complete convert-list-card-images-to-square plan)
