---
phase: 58-rename-the-guild-members-feature-to-characters-everywhere-co
plan: 02
subsystem: ui
tags: [css, static-assets, rename]

# Dependency graph
requires: []
provides:
  - "characters.css (renamed from guild-members.css) with .characters-page scoping class"
  - "characters.mobile.css (renamed from guild-members.mobile.css) with all character-* mobile classes"
  - "Corrected header comments in character-detail.mobile.css and character-form.mobile.css"
affects: [58-03, 58-04]

# Tech tracking
tech-stack:
  added: []
  patterns: []

key-files:
  created:
    - QuestBoard.Service/wwwroot/css/characters.css
    - QuestBoard.Service/wwwroot/css/characters.mobile.css
  modified:
    - QuestBoard.Service/wwwroot/css/character-detail.mobile.css
    - QuestBoard.Service/wwwroot/css/character-form.mobile.css

key-decisions:
  - "Whole-file substring replacement (guild-members-page -> characters-page, guild-section/guild-member/guild-empty-state -> character-section/character-member/character-empty-state) covered every class token without over-matching any existing character-* selector"

patterns-established: []

requirements-completed: []

# Metrics
duration: 6min
completed: 2026-07-06
status: complete
---

# Phase 58 Plan 02: Rename guild-members CSS files to characters Summary

**Renamed guild-members.css and guild-members.mobile.css to characters.css/characters.mobile.css with every guild-* class token converted to character-*; corrected two stale header-comment paths in sibling CSS files.**

## Performance

- **Duration:** 6 min
- **Started:** 2026-07-06T17:35:00Z
- **Completed:** 2026-07-06T17:41:58Z
- **Tasks:** 3 completed
- **Files modified:** 4 (2 renamed, 2 comment-only edits)

## Accomplishments
- `guild-members.css` renamed to `characters.css`; `.guild-members-page` scoping class renamed to `.characters-page` (40 occurrences); stale "GUILD MEMBERS" header comment corrected to "CHARACTERS"
- `guild-members.mobile.css` renamed to `characters.mobile.css`; all `guild-section-*`, `guild-member-*`, and `guild-empty-state` classes renamed to `character-*` equivalents; header comment updated to reference the new filename and `Characters/Index.Mobile.cshtml`
- `character-detail.mobile.css` and `character-form.mobile.css` header comments corrected to reference `Characters/...` view paths instead of stale `GuildMembers/...` paths (no CSS rules touched)

## Task Commits

Each task was committed atomically:

1. **Task 1: Rename guild-members.css to characters.css and rename the .guild-members-page scoping class** - `0037458` (refactor)
2. **Task 2: Rename guild-members.mobile.css to characters.mobile.css and rename all guild-* mobile classes** - `251da98` (refactor)
3. **Task 3: Correct stale "GuildMembers/..." header-comment paths in two sibling CSS files** - `96168af` (docs)

_Note: no plan-metadata commit in this plan — worktree mode; orchestrator handles STATE.md/ROADMAP.md after wave merge._

## Files Created/Modified
- `QuestBoard.Service/wwwroot/css/characters.css` - Desktop character-card styles, renamed from guild-members.css, `.characters-page` scoping class
- `QuestBoard.Service/wwwroot/css/characters.mobile.css` - Mobile character-list-row styles, renamed from guild-members.mobile.css, all classes renamed to character-*
- `QuestBoard.Service/wwwroot/css/character-detail.mobile.css` - Header comment corrected (no rule changes)
- `QuestBoard.Service/wwwroot/css/character-form.mobile.css` - Header comment corrected (no rule changes)

## Decisions Made
None beyond the plan's own guidance — followed the plan's substring-replacement approach exactly as specified (`guild-members-page` -> `characters-page`; `guild-section`/`guild-member`/`guild-empty-state` -> `character-section`/`character-member`/`character-empty-state`).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Sed substring replacement collateral-damaged the mobile file's header comment filename**
- **Found during:** Task 2 (renaming guild-members.mobile.css)
- **Issue:** The plan's recommended whole-file substring replacement of `guild-member` -> `character-member` also matched inside the original filename text embedded in the header comment (`guild-members.mobile.css` -> `character-members.mobile.css`), producing an incorrect filename reference distinct from the actual renamed file (`characters.mobile.css`).
- **Fix:** Corrected the header comment line to read `characters.mobile.css` (matching the actual renamed file), immediately after the substring replacement and before running verification.
- **Files modified:** QuestBoard.Service/wwwroot/css/characters.mobile.css
- **Verification:** `grep -ci guild` returns 0; header comment now reads `/* characters.mobile.css — loaded exclusively by Characters/Index.Mobile.cshtml ... */`
- **Committed in:** 251da98 (part of Task 2 commit)

---

**Total deviations:** 1 auto-fixed (1 bug)
**Impact on plan:** Cosmetic correction to a comment string introduced by the plan's own suggested sed approach; no functional or class-name impact. No scope creep.

## Issues Encountered
None.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

`characters.css` and `characters.mobile.css` exist with all `character-*` classes and zero "guild" text anywhere under `wwwroot/css/`. Old `guild-members*.css` files are fully removed. Plans 03 (mobile `<link>` in Index.Mobile.cshtml) and 04 (desktop `<link>` in _Layout.cshtml) can now safely point at the new filenames and class names — no view currently references either the old or new names, so no intermediate broken state exists. No blockers.

---
*Phase: 58-rename-the-guild-members-feature-to-characters-everywhere-co*
*Completed: 2026-07-06*
