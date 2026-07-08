---
phase: 40-platform-members-page-redesign
plan: 02
subsystem: platform-group-management
tags: [aspnet-core-mvc, controller, hangfire, aspnet-identity]
dependency-graph:
  requires:
    - phase: 40-01
      provides: "IUserService.GetAvailableUsersAsync, GroupMembersViewModel.SearchQuery/CreateMember, CreateMemberViewModel"
  provides:
    - "GroupController.Members(id, search) — DB-filtered available-users query, echoes search term"
    - "GroupController.AddMember(id, model, search) — top-level UserId/Role binding, preserves search on redirect"
    - "GroupController.CreateMember(id, model) — route-scoped user create/add reusing Phase 39's CreateOrAddToGroupAsync"
  affects:
    - "Areas/Platform/Views/Group/Members.cshtml / Members.Mobile.cshtml (Plan 03 wires the search input, per-row Add form, and Create New User modal against these actions)"
tech-stack:
  added: []
  patterns:
    - "Route-only groupId sourcing — CreateMember takes id as a plain route parameter; GroupController never injects IActiveGroupContext"
    - "TempData + RedirectToAction(..., new { id }) in place of RedirectWithSuccess/RedirectWithWarning, since those helpers cannot carry route values"
key-files:
  created: []
  modified:
    - QuestBoard.Service/Areas/Platform/Controllers/GroupController.cs
    - QuestBoard.IntegrationTests/Controllers/GroupManagementIntegrationTests.cs
decisions:
  - "RedirectWithSuccess/RedirectWithWarning (Extensions/ControllerExtensions.cs) cannot carry the id route value needed to redirect back to the correct group's Members page, so CreateMember uses direct TempData assignment + RedirectToAction(nameof(Members), new { id }) instead of those helpers, while keeping the exact flash-message strings from AdminController.CreateUser"
  - "AddMember dropped [Bind(Prefix = \"AddMember\")] since the per-row Add form now posts UserId/Role as top-level fields, not nested under an AddMember. prefix"
metrics:
  duration: 20min
  completed: 2026-07-04
status: complete
---

# Phase 40 Plan 02: Wire GroupController to Search-Filtered Query and CreateMember Summary

Rewrote `GroupController.Members`/`AddMember` to use the Plan-01 DB-side search query and preserve the search term across the Add redirect, and added a new `CreateMember` POST action that reuses Phase 39's collision-aware `CreateOrAddToGroupAsync` with `groupId` sourced strictly from the route — `GroupController` never injects `IActiveGroupContext`.

## What Was Built

**Task 1 — Rewrite Members GET (search) and adapt AddMember (preserve search):**
- `Members(int id, string? search)` — replaced the in-memory `GetAllAsync().Where(...)` filter with `userService.GetAvailableUsersAsync(id, search)`; the ViewModel now sets `SearchQuery = search`
- `AddMember(int id, AddMemberViewModel model, string? search)` — dropped `[Bind(Prefix = "AddMember")]` so the per-row form binds `UserId`/`Role` as top-level fields; every `RedirectToAction(nameof(Members), ...)` now includes `search` so the filter survives an Add (D-04)

**Task 2 — Add CreateMember POST action (route-scoped groupId, Phase-39 outcomes):**
- Constructor extended to `(IGroupService groupService, IUserService userService, IIdentityService identityService, IBackgroundJobClient jobClient, ILogger<GroupController> logger)` — no `IActiveGroupContext`
- `CreateMember(int id, CreateMemberViewModel model)` calls `userService.CreateOrAddToGroupAsync(model.Email, model.Name, id, model.GroupRole)` and switches on the four Phase-39 outcomes (`NewAccountCreated`, `AddedToGroup`, `AddedToGroupStrandedAccount`, `AlreadyMember`, `Failed`), copying `AdminController.CreateUser`'s email-job enqueues and flash strings verbatim
- Invalid `ModelState` and the `Failed` outcome re-render the `Members` view with a freshly fetched `Group`/`Members`/`AvailableUsers` plus the posted `CreateMember` model, so the create-user modal (Plan 03) can redisplay validation errors
- Because `RedirectWithSuccess`/`RedirectWithWarning` only accept `(action, message)` with no route values, `CreateMember` sets `TempData["Success"]`/`TempData["Warning"]` directly and returns `RedirectToAction(nameof(Members), new { id })` so the redirect lands on the correct group

**Task 3 — Integration tests:**
- `MembersPage_WithSearch_ShouldReturnOnlyMatchingNonMembersAndEchoTerm` — matching search returns the seeded non-member; non-matching search excludes them
- `AddMember_WithSearch_ShouldPreserveSearchOnRedirect` — POST `AddMember` with a `search` field asserts the `Location` header contains `search={term}` and the `UserGroups` row was created
- `CreateMember_NewAccount_ShouldCreateUserAndAddToGroup` — new email creates a user and a group-1 membership row
- `CreateMember_AlreadyMemberEmail_ShouldNotDuplicateMembership` — posting an existing member's email leaves exactly one membership row
- `CreateMember_PostedToSecondGroup_ShouldScopeMembershipToRouteGroupId` — creates a second group, posts `CreateMember/{group2Id}`, and asserts the new membership is scoped to `group2Id` (not group 1) — proving `groupId` is route-sourced
- Updated `AddMember_ValidUserAndGroup_ShouldAddUserGroupsRow` to post top-level `UserId`/`Role` fields (dropped the `AddMember.` prefix), matching Task 1's binding change

## Verification

- `dotnet build QuestBoard.slnx` — Build succeeded, 0 errors (note: the plan's verification commands reference `QuestBoard.sln`; the actual solution file in this repo is `QuestBoard.slnx`, the same pre-existing convention noted in the 40-01 Summary)
- `dotnet test QuestBoard.IntegrationTests --filter FullyQualifiedName~GroupManagementIntegrationTests` — 23/23 passed (5 new tests + 1 updated pre-existing test)
- `grep -c "IActiveGroupContext\|activeGroupContext" GroupController.cs` — 0 matches, confirming the phase's hard constraint (D-06) holds
- `grep "GetAllAsync()" GroupController.cs` — 0 matches, confirming no leftover in-memory filtering

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Adjusted the search round-trip test's echo assertion to not depend on Plan 03's view markup**
- **Found during:** Task 3, `MembersPage_WithSearch_ShouldReturnOnlyMatchingNonMembersAndEchoTerm`
- **Issue:** The plan's acceptance criteria described asserting the search term is "echoed into the search input `value`" in the rendered HTML. `Members.cshtml` (Plan 03's scope) doesn't render a search input yet — asserting on `value="..."` markup that doesn't exist would fail for a reason unrelated to this plan's controller changes.
- **Fix:** Kept the assertion on the controller-level behavior that this plan actually delivers: the search term filters the DB query (`GetAvailableUsersAsync(id, search)`) and `GroupMembersViewModel.SearchQuery` is populated, verified via the filtered result set (matching name appears, non-matching search excludes it). Left an inline comment noting the visible search-input rendering is Plan 03's responsibility.
- **Files modified:** `QuestBoard.IntegrationTests/Controllers/GroupManagementIntegrationTests.cs`
- **Verification:** Test passes; still exercises the exact controller-level contract (MEMBERS-02) this plan owns.
- **Committed in:** 6beedd3 (Task 3 commit)

---

**Total deviations:** 1 auto-fixed (1 bug — test assertion scoped incorrectly against a future plan's view work)
**Impact on plan:** No scope creep; the fix narrowed an over-eager test assertion to match this plan's actual deliverable (controller wiring), deferring view-level verification to Plan 03 where the search input markup will exist.

## Requirements Addressed

- MEMBERS-02: satisfied — `Members` GET filters via `GetAvailableUsersAsync(id, search)`, no in-memory `GetAllAsync` filtering remains, and the term is echoed onto `GroupMembersViewModel.SearchQuery`.
- MEMBERS-03: satisfied — `CreateMember` creates/adds a user scoped to the route group with the four Phase-39 outcomes, verbatim flash strings, and email jobs.

## Self-Check: PASSED

- FOUND: QuestBoard.Service/Areas/Platform/Controllers/GroupController.cs (contains Members(int id, string? search), AddMember(int id, AddMemberViewModel model, string? search), CreateMember(int id, CreateMemberViewModel model))
- FOUND: QuestBoard.IntegrationTests/Controllers/GroupManagementIntegrationTests.cs (5 new tests + 1 updated test present, all passing)
- FOUND commit c41259f: feat(40-02): rewrite Members GET to use search-filtered query, preserve search on AddMember redirect
- FOUND commit 7a5cb6e: feat(40-02): add CreateMember POST action with route-scoped groupId
- FOUND commit 6beedd3: test(40-02): add integration coverage for search round-trip, search-preserving Add, and CreateMember
