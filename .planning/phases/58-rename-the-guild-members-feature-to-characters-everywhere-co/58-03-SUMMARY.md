---
phase: 58-rename-the-guild-members-feature-to-characters-everywhere-co
plan: 03
subsystem: ui
tags: [aspnet-core-mvc, razor, mvc-convention-routing, rename-refactor]

# Dependency graph
requires:
  - phase: 58-02
    provides: characters.css / characters.mobile.css renamed files and character-* class names
provides:
  - CharactersController serving /Characters/* (replaces GuildMembersController and /GuildMembers/*)
  - Views/Characters/ folder with all 8 roster views (replaces Views/GuildMembers/)
  - Guild-free Index/Details copy and CSS class references
affects: [58-04, 58-05, 58-06]

# Tech tracking
tech-stack:
  added: []
  patterns: [atomic controller-rename + view-folder-move to preserve MVC default view-location convention]

key-files:
  created: [QuestBoard.Service/Controllers/Characters/CharactersController.cs]
  modified:
    - QuestBoard.Service/Views/Characters/Index.cshtml
    - QuestBoard.Service/Views/Characters/Index.Mobile.cshtml
    - QuestBoard.Service/Views/Characters/Details.cshtml
    - QuestBoard.Service/Views/Characters/Details.Mobile.cshtml

key-decisions:
  - "Section heading renamed from 'Guild Roster' to 'Character Roster' to preserve the own-characters-vs-others distinction"
  - "Empty-state copy reworded without guild framing ('Create your first character to get started!' / 'No other adventurers have created characters yet!')"

patterns-established:
  - "Coupled MVC renames (controller class + view folder) must land in a single commit to avoid a broken intermediate view-resolution state"

requirements-completed: []

# Metrics
duration: 12min
completed: 2026-07-06
status: complete
---

# Phase 58 Plan 03: Atomic controller + views rename Summary

**Renamed `GuildMembersController` to `CharactersController` and moved all 8 views from `Views/GuildMembers/` to `Views/Characters/` in one atomic commit, then dropped remaining "guild" copy/classes from the Index and Details views.**

## Performance

- **Duration:** 12 min
- **Started:** 2026-07-06T17:40:00Z
- **Completed:** 2026-07-06T17:52:02Z
- **Tasks:** 3 completed
- **Files modified:** 9 (1 created, 1 deleted via rename, 8 views moved, 4 of which also content-edited)

## Accomplishments
- `CharactersController` now serves the full route surface `/Characters/*` (Index, Details, Create GET/POST, Edit GET/POST, Delete, ToggleRetirement, GetProfilePicture) with zero behavior change — convention-based routing meant only the class name needed to change.
- `Views/Characters/` holds all 8 roster views; `Views/GuildMembers/` no longer exists.
- Index and Details views (desktop + mobile) are guild-free: page title, headings, CSS classes, CSS link, and empty-state/back-link copy all updated to the Characters terminology established by Plan 02's renamed CSS.

## Task Commits

Each task was committed atomically:

1. **Task 1: ATOMIC — rename controller class + move all 8 views to Views/Characters/** - `1e3e4dd` (refactor)
2. **Task 2: Update Index.cshtml + Index.Mobile.cshtml** - `0cd9349` (feat)
3. **Task 3: Update Details.cshtml + Details.Mobile.cshtml** - `424577c` (fix)

_Note: Task 1's first commit attempt (`a5bc3a5`) omitted the working-tree deletion of the old `GuildMembersController.cs` file — a `git rm`/staging gap, not a content gap. This was caught immediately by the post-commit deletion check and folded into the same commit via `git commit --amend` before any other work landed on top, since the deletion is part of the same atomic Task 1 change (old controller file must not coexist with the new one). The amended commit hash `1e3e4dd` is the one that landed._

## Files Created/Modified
- `QuestBoard.Service/Controllers/Characters/CharactersController.cs` - Renamed from `GuildMembersController.cs`; class renamed, in-body comment reworded, all 7 action bodies unchanged
- `QuestBoard.Service/Views/Characters/Index.cshtml` - Page title/heading/section-heading/empty-state copy de-guilded; `.characters-page` class
- `QuestBoard.Service/Views/Characters/Index.Mobile.cshtml` - CSS `<link>` repointed to `characters.mobile.css`; all `guild-*` classes renamed to `character-*`; copy de-guilded
- `QuestBoard.Service/Views/Characters/Details.cshtml` - "Back to Guild Members" → "Back to Characters" (only change; classes already `character-*`)
- `QuestBoard.Service/Views/Characters/Details.Mobile.cshtml` - Same back-link text change (only change)
- `QuestBoard.Service/Views/Characters/Edit.cshtml`, `Edit.Mobile.cshtml`, `Create.cshtml`, `Create.Mobile.cshtml` - Moved only, zero content changes (per RESEARCH.md Pitfall 4, verified already guild-free)

## Decisions Made
- "Guild Roster" section heading renamed to "Character Roster" (Claude's discretion per CONTEXT.md D-01) — keeps the "My Characters" vs. "everyone else's characters" two-section distinction clear.
- Empty-state copy reworded to "Create your first character to get started!" and "No other adventurers have created characters yet!" — drops guild framing while keeping natural tone.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking/self-correction] Task 1's initial commit missed staging the old controller file's deletion**
- **Found during:** Task 1 (post-commit deletion check, per task_commit_protocol step 6)
- **Issue:** `git mv` was used for the 8 view files but the old `GuildMembersController.cs` was removed from disk via `rm` rather than `git rm`; the first commit attempt staged only the new `CharactersController.cs` addition and the view renames, leaving the deletion of `GuildMembersController.cs` unstaged in the working tree. This meant, immediately post-commit, both the old and new controller files existed in git history at that commit — a violation of Task 1's atomicity requirement (old controller file must not coexist with the new one).
- **Fix:** Staged the deletion (`git add QuestBoard.Service/Controllers/Characters/GuildMembersController.cs`) and folded it into the same Task 1 commit via `git commit --amend --no-edit`, since no other commit had yet landed on top and this is a same-task correction, not a rewrite of prior work.
- **Files modified:** `QuestBoard.Service/Controllers/Characters/GuildMembersController.cs` (deletion now included in the amended commit)
- **Verification:** `git show --stat HEAD` confirms the amended commit shows `GuildMembersController.cs => CharactersController.cs` as a single rename; `git status --short` shows a clean working tree; `dotnet build QuestBoard.Service` succeeds with 0 errors after the amendment.
- **Committed in:** `1e3e4dd` (final Task 1 commit, supersedes the interim `a5bc3a5`)

---

**Total deviations:** 1 auto-fixed (1 blocking/self-correction)
**Impact on plan:** No scope creep — this was a git-staging mechanics fix to satisfy the plan's own atomicity requirement, not a code change beyond what Task 1 specified.

## Issues Encountered
None beyond the deviation documented above.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- `CharactersController` and `Views/Characters/` are fully in place and guild-free for the Index/Details surfaces; `dotnet build QuestBoard.Service` is green.
- Remaining cross-references still pointing at the old `"GuildMembers"` controller-name string (e.g. `Url.Action("GetProfilePicture", "GuildMembers", ...)` in `QuestLog/Details.cshtml`, `Quest/Details.cshtml`, `Quest/Manage.cshtml`, `Quest/_QuestCard.cshtml`) and the nav links in `_Layout.cshtml`/`_Layout.Mobile.cshtml` are out of this plan's scope (RESEARCH.md Wave 4/5) — until those land, profile-picture links from Quest/QuestLog views to characters will 404, and nav will still say "Guild Members". This is expected: Plan 03 only covers Wave 3 (the atomic controller+views rename). Integration tests referencing `/GuildMembers/*` routes (Wave 6) will also still fail until their own plan updates the route strings — not a regression introduced by this plan, but a known pending gap for whichever plan covers cross-references/tests.

---
*Phase: 58-rename-the-guild-members-feature-to-characters-everywhere-co*
*Completed: 2026-07-06*

## Self-Check: PASSED

- FOUND: QuestBoard.Service/Controllers/Characters/CharactersController.cs
- FOUND: QuestBoard.Service/Views/Characters/Index.cshtml
- FOUND: QuestBoard.Service/Views/Characters/Index.Mobile.cshtml
- FOUND: QuestBoard.Service/Views/Characters/Details.cshtml
- FOUND: QuestBoard.Service/Views/Characters/Details.Mobile.cshtml
- CONFIRMED ABSENT: QuestBoard.Service/Controllers/Characters/GuildMembersController.cs
- CONFIRMED ABSENT: QuestBoard.Service/Views/GuildMembers/
- FOUND commit: 1e3e4dd (Task 1)
- FOUND commit: 0cd9349 (Task 2)
- FOUND commit: 424577c (Task 3)
