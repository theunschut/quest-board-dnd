---
phase: 30-group-ux-admin-user-creation
plan: 01
subsystem: auth
tags: [aspnet-core-mvc, session, group-picker, multi-tenancy, razor]

# Dependency graph
requires:
  - phase: 29-superadmin-management-area
    provides: IGroupService.GetAllWithMemberCountAsync, SessionKeys.ActiveGroupId, GroupWithMemberCount DTO, /platform area
provides:
  - GroupPickerController (Index GET + SelectGroup POST) — the post-login group-context entry point
  - IGroupService/IGroupRepository.GetGroupsForUserAsync(userId) — user-scoped group query
  - SessionKeys.ActiveGroupName session constant
  - GroupPickerViewModel, GroupPicker/Index.cshtml + Index.Mobile.cshtml
  - _Layout.GroupPicker.cshtml stripped-down pre-group-context layout
affects: [30-02-login-redirect-wiring, 30-03-admin-user-creation, 30-04-nav-group-switch, 30-05-tests]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Group-scoped query pattern: single LINQ .Select projection filtered by g.UserGroups.Any(ug => ug.UserId == userId), no per-row count round-trips (mirrors GetAllWithMemberCountAsync)"
    - "Inline RedirectToLocal helper using Url.IsLocalUrl(returnUrl) replicated per-controller rather than shared base, matching existing AccountController convention"
    - "Card-as-form pattern: each clickable card wraps a POST form with hidden fields + antiforgery token, onclick triggers form.submit() instead of a separate button"

key-files:
  created:
    - QuestBoard.Service/Controllers/GroupPickerController.cs
    - QuestBoard.Service/ViewModels/GroupPickerViewModels/GroupPickerViewModel.cs
    - QuestBoard.Service/Views/GroupPicker/Index.cshtml
    - QuestBoard.Service/Views/GroupPicker/Index.Mobile.cshtml
    - QuestBoard.Service/Views/Shared/_Layout.GroupPicker.cshtml
  modified:
    - QuestBoard.Domain/Interfaces/IGroupService.cs
    - QuestBoard.Domain/Interfaces/IGroupRepository.cs
    - QuestBoard.Domain/Services/GroupService.cs
    - QuestBoard.Repository/GroupRepository.cs
    - QuestBoard.Service/Constants/SessionKeys.cs

key-decisions:
  - "GroupPickerController uses [Authorize] only (no policy) — any authenticated user may view/select among their own groups; non-SuperAdmin loads are scoped via GetGroupsForUserAsync so cross-group enumeration is impossible (T-30-04)"
  - "RedirectToLocal helper replicated inline in GroupPickerController (not extracted to a shared base) to match the existing AccountController.RedirectToLocal convention in this codebase"

requirements-completed: [UX-01, UX-02, UX-03, UX-04]

# Metrics
duration: 25min
completed: 2026-06-30
status: complete
---

# Phase 30 Plan 01: Group Picker Foundation Summary

**GroupPickerController with auto-redirect/multi-group picker logic, user-scoped GetGroupsForUserAsync query, and desktop/mobile picker views with SuperAdmin and zero-group states**

## Performance

- **Duration:** 25 min
- **Tasks:** 4
- **Files modified:** 10 (5 created, 5 modified)

## Accomplishments
- `GroupPickerController.Index` auto-redirects single-group users straight through without showing a picker (UX-01), shows a card grid for multi-group users and SuperAdmin (UX-02/UX-03), and guards zero-group non-SuperAdmin users with a warning instead of a crash
- `SelectGroup` POST writes `ActiveGroupId` + `ActiveGroupName` to session and redirects through an `Url.IsLocalUrl` check (UX-04, open-redirect mitigation T-30-01)
- New `IGroupService/IGroupRepository.GetGroupsForUserAsync(userId)` returns only the requesting user's groups via a single LINQ projection (no N+1), closing off cross-group enumeration for non-SuperAdmins (T-30-04)
- Desktop and mobile picker views render group cards as one-click POST forms (CSRF-protected) with a SuperAdmin "Go to Platform" shortcut and a zero-groups alert state
- `SessionKeys.ActiveGroupName` is now available for the nav layout work in plan 30-04

## Task Commits

Each task was committed atomically:

1. **Task 1: Add GetGroupsForUserAsync to group service + repository + ActiveGroupName session key** - `7d4395c` (feat)
2. **Task 2: Create GroupPickerViewModel and _Layout.GroupPicker.cshtml** - `6060af1` (feat)
3. **Task 3: Create GroupPickerController with Index GET and SelectGroup POST** - `5492db8` (feat)
4. **Task 4: Create GroupPicker Index.cshtml and Index.Mobile.cshtml picker views** - `924b3b2` (feat)

## Files Created/Modified
- `QuestBoard.Domain/Interfaces/IGroupService.cs` - added `GetGroupsForUserAsync(userId)` contract
- `QuestBoard.Domain/Interfaces/IGroupRepository.cs` - mirrored repository contract
- `QuestBoard.Domain/Services/GroupService.cs` - one-line delegation to repository
- `QuestBoard.Repository/GroupRepository.cs` - LINQ projection filtered by `g.UserGroups.Any(ug => ug.UserId == userId)`
- `QuestBoard.Service/Constants/SessionKeys.cs` - added `ActiveGroupName` const
- `QuestBoard.Service/ViewModels/GroupPickerViewModels/GroupPickerViewModel.cs` - Groups, IsSuperAdmin, HasNoGroups, ReturnUrl
- `QuestBoard.Service/Views/Shared/_Layout.GroupPicker.cshtml` - stripped-down pre-group-context layout (brand + logout only)
- `QuestBoard.Service/Controllers/GroupPickerController.cs` - Index GET (auto-redirect/picker/zero-group guard) + SelectGroup POST
- `QuestBoard.Service/Views/GroupPicker/Index.cshtml` - desktop card grid picker
- `QuestBoard.Service/Views/GroupPicker/Index.Mobile.cshtml` - mobile single-column picker

## Decisions Made
- `GroupPickerController` has no `[Authorize(Policy = ...)]` — intentionally accessible to any authenticated user since it only ever shows/selects among the caller's own groups (SuperAdmin gets the full list via a separate, already-`SuperAdminOnly`-protected code path for `GetAllWithMemberCountAsync`)
- Kept `RedirectToLocal` logic inline as a private helper in `GroupPickerController` rather than introducing a shared base controller, consistent with how `AccountController` already does it

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

The plan's verification commands reference `QuestBoard.sln`, but the actual solution file in this repo is `QuestBoard.slnx` (a pre-existing naming difference unrelated to this plan). Used `dotnet build QuestBoard.slnx` / `dotnet test` against the actual file; both succeeded with 0 errors and all 8 `AdminController` integration tests still pass.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- `GroupPickerController` and views are fully built and compile, but nothing routes to `/groups/pick` yet — plan 30-02 must wire `AccountController.Login` POST to redirect there and remove self-registration
- `AdminController` still has three `?? 1` fallbacks (Users, GetAllPlayers, GetAllDungeonMasters) that plan 30-02/30-03 must remove once login enforces group selection
- `SessionKeys.ActiveGroupName` is set by both the auto-redirect and `SelectGroup` paths, ready for plan 30-04's nav "Switch Group" link
- No blockers for downstream plans 30-02 through 30-05

---
*Phase: 30-group-ux-admin-user-creation*
*Completed: 2026-06-30*

## Self-Check: PASSED

All 11 claimed files verified present on disk; all 5 commit hashes verified present in git log.
