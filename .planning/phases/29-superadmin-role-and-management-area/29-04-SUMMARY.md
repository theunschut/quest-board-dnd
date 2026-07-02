---
phase: 29-superadmin-role-and-management-area
plan: "04"
subsystem: platform-ui
tags: [superadmin, platform-area, mvc-area, group-management, razor-views, authorization]
dependency_graph:
  requires:
    - Phase 29-01 (SuperAdminOnly policy registered in Program.cs)
    - Phase 29-03 (IGroupService/IGroupRepository with all five MGMT operations)
  provides:
    - GroupController with [Area("Platform")] + [Authorize(Policy = "SuperAdminOnly")]
    - Five Razor views: Index, Create, Edit, Delete, Members (modern-card pattern)
    - _Layout.Platform.cshtml (minimal: logo, username, logout, back link)
    - _ViewImports.cshtml (unique @namespace for area)
    - _ViewStart.cshtml (Layout = "_Layout.Platform")
    - Five PlatformViewModels (GroupList, GroupCreate, GroupEdit, GroupMembers, AddMember)
    - Platform area route in Program.cs at /platform/{controller=Group}/{action=Index}/{id?}
    - UserGroup.User navigation property (added for Members view display)
    - EntityProfile mapping for UserGroup.User (UserGroupEntity -> UserGroup)
  affects:
    - Phase 29-05 (integration tests will test /platform/Group endpoints)
    - Phase 30 (Group UX — navigation link from quest board to /platform)
tech_stack:
  added: []
  patterns:
    - MVC Area with [Area("Platform")] controller attribute + area route in Program.cs
    - SuperAdminOnly policy on entire controller class (not per-action)
    - DbUpdateException catch for unique constraint violations (group name) in Create/Edit
    - asp-antiforgery="true" on inline Razor form tags (Remove Member)
    - asp-items="Html.GetEnumSelectList<GroupRole>()" for enum select dropdowns
    - UserGroup.User? navigation property mapped via AutoMapper for member display
key_files:
  created:
    - QuestBoard.Service/Areas/Platform/Controllers/GroupController.cs
    - QuestBoard.Service/Areas/Platform/Views/_ViewImports.cshtml
    - QuestBoard.Service/Areas/Platform/Views/_ViewStart.cshtml
    - QuestBoard.Service/Areas/Platform/Views/Shared/_Layout.Platform.cshtml
    - QuestBoard.Service/Areas/Platform/Views/Group/Index.cshtml
    - QuestBoard.Service/Areas/Platform/Views/Group/Create.cshtml
    - QuestBoard.Service/Areas/Platform/Views/Group/Edit.cshtml
    - QuestBoard.Service/Areas/Platform/Views/Group/Delete.cshtml
    - QuestBoard.Service/Areas/Platform/Views/Group/Members.cshtml
    - QuestBoard.Service/ViewModels/PlatformViewModels/GroupListViewModel.cs
    - QuestBoard.Service/ViewModels/PlatformViewModels/GroupCreateViewModel.cs
    - QuestBoard.Service/ViewModels/PlatformViewModels/GroupEditViewModel.cs
    - QuestBoard.Service/ViewModels/PlatformViewModels/GroupMembersViewModel.cs
    - QuestBoard.Service/ViewModels/PlatformViewModels/AddMemberViewModel.cs
  modified:
    - QuestBoard.Service/Program.cs
    - QuestBoard.Domain/Models/UserGroup.cs
    - QuestBoard.Repository/Automapper/EntityProfile.cs
decisions:
  - "GroupController [Area(\"Platform\")] + [Authorize(Policy = \"SuperAdminOnly\")] on the class (not per-action) — T-29-04-01 mitigation"
  - "[ValidateAntiForgeryToken] on all five POST actions — T-29-04-02 mitigation"
  - "DbUpdateException caught in Create and Edit POST actions for unique name constraint — T-29-04-03 mitigation"
  - "DeleteConfirmed checks HasMembersAsync server-side regardless of UI guard — T-29-04-05 mitigation"
  - "UserGroup.User? navigation property added to domain model + EntityProfile mapping — required for Members view to display Name/Email per member row"
  - "_Layout.Platform.cshtml includes only site.css (no calendar.css, quests.css, shop.css, guild-members.css, dm-profile.css) — platform area does not use those stylesheets"
metrics:
  duration: "5 minutes"
  completed_date: "2026-06-30"
  tasks_completed: 2
  files_modified: 17
---

# Phase 29 Plan 04: Platform MVC Area — Group Management UI Summary

Platform /platform MVC area scaffolded with GroupController, five Razor views (modern-card pattern), dedicated _Layout.Platform.cshtml, area _ViewImports/_ViewStart, all PlatformViewModels, and area route registration — delivering the full SuperAdmin group management interface (MGMT-01 through MGMT-06).

## Performance

- **Duration:** 5 min
- **Started:** 2026-06-30T13:32:16Z
- **Completed:** 2026-06-30T13:37:31Z
- **Tasks:** 2
- **Files modified:** 17

## Accomplishments

- GroupController: all five MGMT actions (Index, Create, Edit, Delete, Members) plus AddMember and RemoveMember POST actions — all protected by [Area("Platform")] + [Authorize(Policy = "SuperAdminOnly")] on the class
- All POST actions have [ValidateAntiForgeryToken]; Create and Edit POST catch DbUpdateException for unique group name constraint
- DeleteConfirmed has server-side HasMembersAsync guard (cannot delete non-empty group even via direct POST)
- _Layout.Platform.cshtml: minimal nav (logo, logged-in username, logout, back to quest board) — no quest board nav links
- _ViewImports.cshtml: uses @namespace QuestBoard.Service.Areas.Platform.Views (distinct from root Pages namespace)
- _ViewStart.cshtml: Layout = "_Layout.Platform"
- Platform area route added before default route in Program.cs
- Five modern-card Razor views match UI-SPEC.md and Copywriting Contract exactly
- Members view shows user Name/Email/Role badge and Remove button per member row; Add Member form with user selector and role picker
- 197/197 tests pass after both tasks

## Task Commits

1. **Task 1: PlatformViewModels, GroupController, area layout and route** - `15bc076` (feat)
2. **Task 2: Five Group management Razor views** - `b0eb225` (feat)

## Files Created/Modified

- `QuestBoard.Service/Areas/Platform/Controllers/GroupController.cs` — area controller with all actions and CSRF/auth guards
- `QuestBoard.Service/Areas/Platform/Views/_ViewImports.cshtml` — area-scoped tag helpers, @namespace, @using directives
- `QuestBoard.Service/Areas/Platform/Views/_ViewStart.cshtml` — Layout = "_Layout.Platform"
- `QuestBoard.Service/Areas/Platform/Views/Shared/_Layout.Platform.cshtml` — minimal platform layout
- `QuestBoard.Service/Areas/Platform/Views/Group/Index.cshtml` — groups table with Members/Edit/Delete actions
- `QuestBoard.Service/Areas/Platform/Views/Group/Create.cshtml` — create group form
- `QuestBoard.Service/Areas/Platform/Views/Group/Edit.cshtml` — rename group form
- `QuestBoard.Service/Areas/Platform/Views/Group/Delete.cshtml` — delete confirmation page
- `QuestBoard.Service/Areas/Platform/Views/Group/Members.cshtml` — member list + add/remove forms
- `QuestBoard.Service/ViewModels/PlatformViewModels/GroupListViewModel.cs` — IList<GroupWithMemberCount>
- `QuestBoard.Service/ViewModels/PlatformViewModels/GroupCreateViewModel.cs` — Name with [Required][StringLength(100)]
- `QuestBoard.Service/ViewModels/PlatformViewModels/GroupEditViewModel.cs` — Id + Name with same annotations
- `QuestBoard.Service/ViewModels/PlatformViewModels/GroupMembersViewModel.cs` — Group, Members, AvailableUsers, AddMember
- `QuestBoard.Service/ViewModels/PlatformViewModels/AddMemberViewModel.cs` — UserId + Role
- `QuestBoard.Service/Program.cs` — platform area route added before default route
- `QuestBoard.Domain/Models/UserGroup.cs` — added User? navigation property
- `QuestBoard.Repository/Automapper/EntityProfile.cs` — added User mapping for UserGroupEntity -> UserGroup

## Decisions Made

- UserGroup.User? navigation property added to domain model and mapped in EntityProfile — the plan's interface spec referenced this property but it wasn't in the actual domain model; GetMembersAsync uses .Include(ug => ug.User) so the data was available but not surfaced through AutoMapper
- _Layout.Platform.cshtml links only site.css — the page-specific CSS files (calendar.css, quests.css, etc.) are not needed in the platform area
- Members view renders "All users are already members of this group." when AvailableUsers is empty — prevents empty select submission

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 - Missing Critical Functionality] Added User? navigation property to UserGroup domain model**
- **Found during:** Task 1 (reviewing GroupMembersViewModel spec against actual domain model)
- **Issue:** The plan's interface section specified `public User? User` on UserGroup and `GroupMembersViewModel.Members` as `IList<UserGroup>`. However, `UserGroup` domain model had no `User` property. `GetMembersAsync` does `.Include(ug => ug.User)` in EF but AutoMapper dropped the navigation. The Members view requires displaying Name and Email per member row.
- **Fix:** Added `public User? User { get; set; }` to `QuestBoard.Domain/Models/UserGroup.cs`; added `.ForMember(dest => dest.User, opt => opt.MapFrom(src => src.User))` to `EntityProfile` UserGroupEntity→UserGroup mapping; added `.ForMember(dest => dest.User, opt => opt.Ignore())` to the reverse direction.
- **Files modified:** `QuestBoard.Domain/Models/UserGroup.cs`, `QuestBoard.Repository/Automapper/EntityProfile.cs`
- **Commit:** 15bc076

## Known Stubs

None. All data is fully wired:
- GroupController calls real IGroupService and IUserService
- All five views render live data from the database
- Members view shows actual user Name/Email from the mapped User navigation property

## Threat Surface Scan

New network endpoints introduced at /platform/*:
- GET /platform/Group/Index
- GET /platform/Group/Create, POST /platform/Group/Create
- GET /platform/Group/Edit/{id}, POST /platform/Group/Edit/{id}
- GET /platform/Group/Delete/{id}, POST /platform/Group/Delete (ActionName)
- GET /platform/Group/Members/{id}
- POST /platform/Group/AddMember/{id}
- POST /platform/Group/RemoveMember/{id}

All endpoints are covered by the plan's threat model:
- T-29-04-01: [Authorize(Policy = "SuperAdminOnly")] on controller class — implemented
- T-29-04-02: [ValidateAntiForgeryToken] on all POST actions — implemented
- T-29-04-03: ViewModel binding limits bound properties; DbUpdateException for unique name — implemented
- T-29-04-04: groupId from route parameter (server-controlled); ModelState validated — implemented
- T-29-04-05: HasMembersAsync server-side check in DeleteConfirmed — implemented
- T-29-04-06: @User.Identity?.Name in layout — acceptable disclosure of own username

No unplanned threat surface added.

## Self-Check: PASSED

Files verified to exist:
- QuestBoard.Service/Areas/Platform/Controllers/GroupController.cs: FOUND
- QuestBoard.Service/Areas/Platform/Views/_ViewImports.cshtml: FOUND
- QuestBoard.Service/Areas/Platform/Views/_ViewStart.cshtml: FOUND
- QuestBoard.Service/Areas/Platform/Views/Shared/_Layout.Platform.cshtml: FOUND
- QuestBoard.Service/Areas/Platform/Views/Group/Index.cshtml: FOUND
- QuestBoard.Service/Areas/Platform/Views/Group/Create.cshtml: FOUND
- QuestBoard.Service/Areas/Platform/Views/Group/Edit.cshtml: FOUND
- QuestBoard.Service/Areas/Platform/Views/Group/Delete.cshtml: FOUND
- QuestBoard.Service/Areas/Platform/Views/Group/Members.cshtml: FOUND

Commits verified in git log:
- 15bc076: feat(29-04): add PlatformViewModels, GroupController, area layout and route — FOUND
- b0eb225: feat(29-04): add five Group management Razor views for /platform area — FOUND

Build: dotnet build exits 0 (0 errors, 10 pre-existing warnings)
Tests: 197/197 pass (55 unit + 142 integration)
