---
phase: 58-rename-the-guild-members-feature-to-characters-everywhere-co
plan: 04
subsystem: ui
tags: [razor, mvc, cshtml, navigation, url-action]

# Dependency graph
requires:
  - phase: 58-03
    provides: CharactersController (renamed from GuildMembersController) with GetProfilePicture/Index actions
provides:
  - All 6 Url.Action("GetProfilePicture", ...) cross-references across Quest/QuestLog views repointed from "GuildMembers" to "Characters"
  - Desktop + mobile nav roster link repointed to Characters controller with "Characters" visible text
  - Desktop layout's CSS <link> repointed from guild-members.css to characters.css
affects: [58-05]

# Tech tracking
tech-stack:
  added: []
  patterns: []

key-files:
  created: []
  modified:
    - QuestBoard.Service/Views/QuestLog/Details.cshtml
    - QuestBoard.Service/Views/QuestLog/Details.Mobile.cshtml
    - QuestBoard.Service/Views/Quest/Details.cshtml
    - QuestBoard.Service/Views/Quest/Manage.cshtml
    - QuestBoard.Service/Views/Quest/_QuestCard.cshtml
    - QuestBoard.Service/Views/Shared/_Layout.cshtml
    - QuestBoard.Service/Views/Shared/_Layout.Mobile.cshtml

key-decisions: []

patterns-established: []

requirements-completed: []

# Metrics
duration: 5min
completed: 2026-07-06
status: complete
---

# Phase 58 Plan 04: Repoint cross-controller references and nav links to Characters Summary

**All 6 `Url.Action("GetProfilePicture", ...)` cross-references and both desktop/mobile nav links now target the renamed `Characters` controller, with the desktop layout loading `characters.css`.**

## Performance

- **Duration:** 5 min
- **Tasks:** 2 completed
- **Files modified:** 7

## Accomplishments
- Repointed all 6 `@Url.Action("GetProfilePicture", "GuildMembers", ...)` cross-references (across `QuestLog/Details.cshtml`, `QuestLog/Details.Mobile.cshtml`, `Quest/Details.cshtml` x2, `Quest/Manage.cshtml`, and the shared partial `Quest/_QuestCard.cshtml`) to `"Characters"`, keeping character profile-picture thumbnails resolvable everywhere they render.
- Updated both desktop (`_Layout.cshtml`) and mobile (`_Layout.Mobile.cshtml`) nav roster links to `asp-controller="Characters"` with visible text "Characters" (icon unchanged), and the desktop layout's CSS `<link>` to `~/css/characters.css`.
- Confirmed zero remaining "guild" text in either layout and zero remaining `Url.Action(..., "GuildMembers", ...)` sites in the Service project.
- `dotnet build QuestBoard.Service` succeeds with 0 warnings / 0 errors against the Plan-03-provided `CharactersController` (including its `GetProfilePicture` action).

## Task Commits

Each task was committed atomically:

1. **Task 1: Repoint all 6 Url.Action("GetProfilePicture", "GuildMembers", ...) cross-references to "Characters"** - `aa59f01` (feat)
2. **Task 2: Update desktop + mobile nav links and the desktop CSS link in the layouts** - `6b9cec9` (feat)

_Note: Plan metadata commit is added separately after this summary._

## Files Created/Modified
- `QuestBoard.Service/Views/QuestLog/Details.cshtml` - Profile-picture `Url.Action` controller arg changed to "Characters"
- `QuestBoard.Service/Views/QuestLog/Details.Mobile.cshtml` - Profile-picture `Url.Action` controller arg changed to "Characters"
- `QuestBoard.Service/Views/Quest/Details.cshtml` - Both profile-picture `Url.Action` sites (participant + player) changed to "Characters"
- `QuestBoard.Service/Views/Quest/Manage.cshtml` - Profile-picture `Url.Action` controller arg changed to "Characters"
- `QuestBoard.Service/Views/Quest/_QuestCard.cshtml` - Profile-picture `Url.Action` controller arg changed to "Characters" (shared partial, RESEARCH.md Pitfall 3)
- `QuestBoard.Service/Views/Shared/_Layout.cshtml` - Nav link `asp-controller`/text changed to Characters; CSS link changed to `characters.css`
- `QuestBoard.Service/Views/Shared/_Layout.Mobile.cshtml` - Nav link `asp-controller`/text changed to Characters

## Decisions Made
None - followed plan as specified.

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered
None.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- Profile-picture thumbnails and nav links are fully repointed at the `Characters` controller; `dotnet build` passes.
- Plan 05 (integration tests) runs in the same wave against disjoint files and is unaffected by this plan's changes.
- Recommended local smoke test (per plan's `<verification>` section, not run here): open a Quest with signed-up characters (board + details) and the Quest Log to visually confirm avatar thumbnails render, and confirm the nav reads "Characters" and routes to `/Characters`.

---
*Phase: 58-rename-the-guild-members-feature-to-characters-everywhere-co*
*Completed: 2026-07-06*

## Self-Check: PASSED

All 7 modified files verified present on disk; all 3 commit hashes (`aa59f01`, `6b9cec9`, `cc54ddc`) verified present in git log.
