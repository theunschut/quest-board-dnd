---
phase: 39-shared-collision-aware-user-creation-email
plan: 02
subsystem: email
tags: [razor-components, hangfire, tempdata, bootstrap-alerts]

# Dependency graph
requires:
  - phase: 39-shared-collision-aware-user-creation-email (Plan 01)
    provides: Shared collision-aware CreateOrAddToGroupAsync method on UserService
provides:
  - AddedToGroup.razor email component (token-free, names group + role)
  - GroupMembershipAddedEmailJob Hangfire job
  - RedirectWithWarning controller-extension helper
  - alert-warning flash banner in Admin Users view
affects: [39-03 (controller integration wiring these into AdminController.CreateUser)]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Email components mirror Welcome.razor's exact visual shell (600px parchment table, Cinzel title, wax seal, gold CTA) for a consistent brand look across notification types"
    - "Hangfire jobs resolve scoped services (IEmailRenderService, IEmailService, IOptions<EmailSettings>) from a fresh IServiceScopeFactory scope rather than constructor injection"
    - "TempData flash-message helpers (RedirectWithSuccess/Error/Warning) all delegate to a single RedirectWithMessage(action, key, message) core"

key-files:
  created:
    - QuestBoard.Service/Components/Emails/AddedToGroup.razor
    - QuestBoard.Service/Jobs/GroupMembershipAddedEmailJob.cs
  modified:
    - QuestBoard.Service/Extensions/ControllerExtensions.cs
    - QuestBoard.Service/Views/Admin/Users.cshtml

key-decisions:
  - "AddedToGroup.razor's CTA links to a plain /Account/Login route with no token — the user already has a password from their original account, so no set-password/callback URL is needed"
  - "Subject line for the added-to-group email follows WelcomeEmailJob's direct-string convention: $\"You've been added to {groupName}\""
  - "Warning banner reuses the existing dismissible alert markup shape (icon + message + btn-close) rather than introducing a toast notification, consistent with the deferred site-wide toast conversion decision"

requirements-completed: [CREATE-02, CREATE-03]

# Metrics
duration: ~3min
completed: 2026-07-04
status: complete
---

# Phase 39 Plan 02: AddedToGroup Email & Warning Flash Summary

**New AddedToGroup.razor email component (Welcome.razor's visual shell, token-free Log In CTA) plus its GroupMembershipAddedEmailJob Hangfire dispatcher, and a RedirectWithWarning/alert-warning flash pair for the Admin Users page**

## Performance

- **Duration:** ~3 min
- **Completed:** 2026-07-04
- **Tasks:** 3
- **Files modified:** 4 (2 created, 2 modified)

## Accomplishments
- Built `AddedToGroup.razor`, visually identical to `Welcome.razor` (parchment table, Cinzel title, wax-seal image, gold CTA) but naming the group and role, with a token-free "Log In" CTA
- Built `GroupMembershipAddedEmailJob`, following the established `IServiceScopeFactory` Hangfire scope pattern to render and send the new email
- Added `RedirectWithWarning` as a third sibling to `RedirectWithSuccess`/`RedirectWithError`, mapping to `TempData["Warning"]`
- Added the matching `alert-warning` dismissible flash block to `Views/Admin/Users.cshtml`

## Task Commits

Each task was committed atomically:

1. **Task 1: Create the AddedToGroup email component** - `069296d` (feat)
2. **Task 2: Create the GroupMembershipAddedEmailJob Hangfire job** - `3e58ee2` (feat)
3. **Task 3: Add RedirectWithWarning helper and the Users.cshtml warning banner** - `d5d6015` (feat)

**Plan metadata:** (this commit, following SUMMARY/STATE/ROADMAP updates)

## Files Created/Modified
- `QuestBoard.Service/Components/Emails/AddedToGroup.razor` - New email component; same visual shell as Welcome, names GroupName/Role, token-free Log In CTA
- `QuestBoard.Service/Jobs/GroupMembershipAddedEmailJob.cs` - Hangfire job resolving scoped services per-execution and sending the AddedToGroup email
- `QuestBoard.Service/Extensions/ControllerExtensions.cs` - Added `RedirectWithWarning` extension method
- `QuestBoard.Service/Views/Admin/Users.cshtml` - Added `@if (TempData["Warning"] != null)` alert-warning dismissible block

## Decisions Made
- Kept `AppUrl` as a parameter on `AddedToGroup` (matching `Welcome`) since the background/seal image URLs are built from it, even though the component itself no longer needs a callback token.
- Used `fas fa-exclamation-circle` for the warning icon (distinct from the `fa-exclamation-triangle` already used for `Error`, avoiding visual duplication between the two flash types).

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

None. All three `dotnet build` verifications passed on first attempt (0 warnings, 0 errors). A pending, out-of-plan-scope `.planning/config.json` working-tree change (`workflow.use_worktrees: true`, unrelated to this plan) was present throughout; it was kept out of all three task commits by staging files individually rather than using `git add -A`, per the run's explicit instructions. It was later resolved by a separate, out-of-band commit (`5a9df62`) authored outside this plan's execution.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- All four artifacts (`AddedToGroup.razor`, `GroupMembershipAddedEmailJob`, `RedirectWithWarning`, `alert-warning` view block) are ready for Plan 03 to wire into `AdminController.CreateUser` and the new Platform create-user entry point.
- No blockers.

---
*Phase: 39-shared-collision-aware-user-creation-email*
*Completed: 2026-07-04*

## Self-Check: PASSED

All 4 created/modified files found on disk. All 3 task commits (`069296d`, `3e58ee2`, `d5d6015`) found in git history.
