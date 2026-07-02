---
phase: 30-group-ux-admin-user-creation
plan: 05
subsystem: testing
tags: [xunit, integration-tests, aspnet-core-mvc, group-picker, admin, multi-tenancy]

# Dependency graph
requires:
  - phase: 30-group-ux-admin-user-creation
    plan: "30-01"
    provides: "GroupPickerController (Index GET + SelectGroup POST)"
  - phase: 30-group-ux-admin-user-creation
    plan: "30-02"
    provides: "Register route removed; Login redirects to GroupPicker"
  - phase: 30-group-ux-admin-user-creation
    plan: "30-03"
    provides: "AdminController.CreateUser; ?? 1 fallbacks removed"
  - phase: 30-group-ux-admin-user-creation
    plan: "30-04"
    provides: "Nav group-switch item in desktop and mobile layouts"
provides:
  - "GroupPickerControllerIntegrationTests.cs — UX-01..UX-04 coverage"
  - "AccountControllerIntegrationTests.cs — Register GET/POST assert NotFound (REG-01)"
  - "AdminControllerIntegrationTests.cs — CreateUser auth-gating, form, and group-assignment tests (MGMT-07, REG-02, REG-03)"
  - "MobileViewsTests.cs — MobileAccountRegister updated to assert NotFound (REG-01)"
  - "GroupManagementIntegrationTests.cs — AddMember form field prefix corrected (MGMT-05 now correct)"
  - "UserRepository.SetGroupRoleAsync — upsert fix (insert when row absent)"
  - "Full test suite green: 226/226 (55 unit + 171 integration)"
affects: []

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Plain FormUrlEncodedContent without real anti-forgery token — TestAntiforgeryDecorator.ValidateRequestAsync always returns Task.CompletedTask in the Testing environment; established convention in GroupManagementIntegrationTests"
    - "[Bind(Prefix)] binding convention: action parameters with a prefix attribute require form fields to carry the prefix or model binds to type defaults; test form data must mirror the view's asp-for prefix"

key-files:
  created:
    - QuestBoard.IntegrationTests/Controllers/GroupPickerControllerIntegrationTests.cs
  modified:
    - QuestBoard.IntegrationTests/Controllers/AccountControllerIntegrationTests.cs
    - QuestBoard.IntegrationTests/Controllers/AdminControllerIntegrationTests.cs
    - QuestBoard.IntegrationTests/Mobile/MobileViewsTests.cs
    - QuestBoard.IntegrationTests/Controllers/GroupManagementIntegrationTests.cs
    - QuestBoard.Repository/UserRepository.cs
    - QuestBoard.Service/Views/GroupPicker/Index.cshtml
    - QuestBoard.Service/Views/GroupPicker/Index.Mobile.cshtml
    - QuestBoard.Service/Views/Admin/Users.cshtml
    - QuestBoard.Service/Views/Admin/Users.Mobile.cshtml

key-decisions:
  - "UX-04 (session persistence) assertability: the TestAuthHandler-based client (Authorization header) does not round-trip ASP.NET Core session cookies the way a browser would. SelectGroup_ShouldPersistActiveGroupInSession asserts the redirect response and the group DB record rather than a follow-up session read, with an explanatory comment noting the harness limitation"
  - "UserRepository.SetGroupRoleAsync changed from pure-update to upsert (insert when no row exists) — required for AdminController.CreateUser group-assignment path; pre-existing callers (PromoteToAdmin, DemoteFromAdmin, etc.) already seed a UserGroups row via AuthenticationHelper and are unaffected"

requirements-completed: [UX-01, UX-02, UX-03, UX-04, UX-05, MGMT-07, REG-01, REG-02, REG-03]

# Metrics
duration: 35min
completed: 2026-06-30
status: complete
---

# Phase 30 Plan 05: Tests + Full-Suite Green Gate Summary

**Integration test suite covering the full Phase 30 group-UX loop: GroupPicker (UX-01..UX-04), Register-is-gone (REG-01), and admin-CreateUser group assignment (MGMT-07, REG-02, REG-03); full suite green at 226/226**

## Performance

- **Duration:** 40 min (including verification)
- **Tasks completed:** 4 of 4 (Task 4 human-verify approved)
- **Files modified:** 10 (1 created, 9 modified)

## Accomplishments

- `GroupPickerControllerIntegrationTests.cs` (new): 5 integration tests covering the post-login group-context entry point — unauthenticated redirect, single-group auto-redirect (UX-01), multi-group picker page (UX-02), SuperAdmin Platform option (UX-03), and SelectGroup redirect/session write (UX-04)
- `AccountControllerIntegrationTests.cs`: replaced three Register tests (expected 200/redirect/create-user) with two tests asserting `HttpStatusCode.NotFound` for both GET and POST to `/Account/Register` (REG-01)
- `AdminControllerIntegrationTests.cs`: added `CreateUser_WhenNotAdmin_ShouldBeForbidden`, `CreateUser_Get_WhenAdmin_ShouldReturnForm`, and `CreateUser_Post_WhenAdmin_CreatesUserInActiveGroup` (MGMT-07, REG-02, REG-03); the CreateUser POST test verifies a `UserGroups` row with the chosen `GroupRole` is present in group 1 (the `MutableGroupContext` default active group) after the POST
- `MobileViewsTests.cs`: `MobileAccountRegister_MobileUserAgent_RendersGlassCardForm` renamed and updated to assert `HttpStatusCode.NotFound` — the Register route is gone; assertion updated to correct new behavior, not weakened
- `GroupManagementIntegrationTests.cs`: `AddMember_ValidUserAndGroup_ShouldAddUserGroupsRow` corrected to post `AddMember.UserId`/`AddMember.Role` instead of the unprefixed names — `[Bind(Prefix = "AddMember")]` on the action parameter requires form fields to carry that prefix
- `UserRepository.cs`: `SetGroupRoleAsync` fixed from pure-update (returned `null` and inserted nothing when no UserGroups row existed) to upsert (inserts a new `UserGroupEntity` when no row is found) — this is the bug that caused `CreateUser_Post_WhenAdmin_CreatesUserInActiveGroup` to fail, since brand-new users have no pre-existing group membership row

## Task Commits

Each task was committed atomically:

1. **Task 1: Create GroupPickerControllerIntegrationTests (UX-01..UX-04)** - `b4ed066` (test)
2. **Task 2: Update Register tests to assert 404 and add AdminController CreateUser tests** - `5447dd3` (test)
3. **Task 3: Full-suite green gate** - `06a3e12` (test)
4. **Task 4: Checkpoint — human-verify** — APPROVED by user during checkpoint review

## Files Created/Modified

- `QuestBoard.IntegrationTests/Controllers/GroupPickerControllerIntegrationTests.cs` - new file, 5 tests
- `QuestBoard.IntegrationTests/Controllers/AccountControllerIntegrationTests.cs` - 3 Register tests replaced by 2 NotFound assertions
- `QuestBoard.IntegrationTests/Controllers/AdminControllerIntegrationTests.cs` - 3 CreateUser tests added; updated usings
- `QuestBoard.IntegrationTests/Mobile/MobileViewsTests.cs` - mobile Register test updated to assert NotFound
- `QuestBoard.IntegrationTests/Controllers/GroupManagementIntegrationTests.cs` - AddMember form field names prefixed per Bind convention
- `QuestBoard.Repository/UserRepository.cs` - SetGroupRoleAsync: pure-update changed to upsert
- `QuestBoard.Service/Views/GroupPicker/Index.cshtml` - removed "Or select a group below to enter as a member" label (SuperAdmin branch)
- `QuestBoard.Service/Views/GroupPicker/Index.Mobile.cshtml` - same label removed (mobile)
- `QuestBoard.Service/Views/Admin/Users.cshtml` - added "Create User" button in card header
- `QuestBoard.Service/Views/Admin/Users.Mobile.cshtml` - added "Create User" button in mobile header

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] UserRepository.SetGroupRoleAsync: no-op on missing row**

- **Found during:** Task 2 — `CreateUser_Post_WhenAdmin_CreatesUserInActiveGroup` test failing
- **Issue:** `SetGroupRoleAsync` only updated an existing `UserGroups` row. When called for a brand-new user (no row yet), it returned `null` and silently wrote nothing to the database. This broke the `AdminController.CreateUser` path that calls `SetGroupRoleAsync` immediately after creating the user — the created user ended up with no group membership, defeating the entire purpose of REG-02.
- **Fix:** Changed the method to upsert: if no row exists, insert a new `UserGroupEntity`; if one exists, update its `GroupRole`. Existing callers that only update roles on pre-seeded memberships are unaffected.
- **Files modified:** `QuestBoard.Repository/UserRepository.cs`
- **Commit:** included in `5447dd3`

**2. [Rule 1 - Bug] GroupManagementIntegrationTests.AddMember: wrong form field names**

- **Found during:** Task 3 full-suite gate — `AddMember_ValidUserAndGroup_ShouldAddUserGroupsRow` failing (pre-existing, deferred in deferred-items.md)
- **Issue:** The test posted `UserId`/`Role` directly, but `GroupController.AddMember` uses `[Bind(Prefix = "AddMember")]`, so the model bound to defaults (UserId=0, Role=0/Player). The `groupService.AddMemberAsync` ran against userId=0 (not the new user), producing no useful membership row for the assertion. The test was verifying the wrong userId anyway.
- **Fix:** Changed form field names to `AddMember.UserId`/`AddMember.Role` to match the binding prefix.
- **Files modified:** `QuestBoard.IntegrationTests/Controllers/GroupManagementIntegrationTests.cs`
- **Commit:** included in `06a3e12`

**3. [Rule 1 - Test update] MobileViewsTests: Register test asserting 200 for a deleted route**

- **Found during:** Task 3 full-suite gate
- **Issue:** `MobileAccountRegister_MobileUserAgent_RendersGlassCardForm` expected the Register mobile view (200) — the route was deleted in plan 30-02. This is the fourth expected test failure that plan 30-02's SUMMARY.md documented as "to be fixed in plan 30-05."
- **Fix:** Renamed test to `MobileAccountRegister_MobileUserAgent_ShouldReturnNotFound` and updated assertion to `NotFound`. This reflects new correct behavior, does not weaken the assertion.
- **Files modified:** `QuestBoard.IntegrationTests/Mobile/MobileViewsTests.cs`
- **Commit:** included in `06a3e12`

## Issues Encountered

- The plan's verification commands reference `QuestBoard.sln` — the actual file is `QuestBoard.slnx` (pre-existing naming difference noted in 30-01 through 30-04 summaries). Used `QuestBoard.slnx` throughout.
- Session round-tripping is not testable with the TestAuthHandler+header-based client — `SelectGroup_ShouldPersistActiveGroupInSession` asserts the redirect and DB state rather than reading session back from a follow-up request; limitation documented in test comment.

## Known Stubs

None — all test assertions are functional and data-driven.

## Threat Flags

None — no new network endpoints, auth paths, or schema changes introduced by this plan (tests only, plus the UserRepository upsert fix which closes a data-loss path rather than opening a new trust boundary).

## User Setup Required

None — no external service configuration required.

## Verification Fixes (Post-Checkpoint)

During human verification the user made two UI polish improvements:

**1. GroupPicker views — removed redundant label**
- Removed "Or select a group below to enter as a member" `<p>` tag from both `Index.cshtml` and `Index.Mobile.cshtml` (inside the SuperAdmin branch, immediately after the "Go to Platform" button). The label was redundant — the card grid below already makes the intent clear.

**2. Admin/Users views — added "Create User" button**
- Added a "Create User" button to the card header in both `Users.cshtml` (desktop) and `Users.Mobile.cshtml` (mobile), linking to `asp-action="CreateUser"`. This makes the admin user creation flow discoverable from the Users list page without requiring navigation to a separate URL.

## Next Phase Readiness

- All Phase 30 plans (30-01 through 30-05) are complete
- Human verification approved
- Test suite green at 226/226
- Phase 30 group-UX loop is fully implemented, tested, and verified end-to-end; ready for milestone close

---
*Phase: 30-group-ux-admin-user-creation*
*Completed: 2026-06-30*

## Self-Check: PASSED

All 10 claimed files (9 modified, 1 created) verified present on disk.
All 3 task commit hashes (b4ed066, 5447dd3, 06a3e12) verified in git log.
Full test suite: 226 tests, 0 failures (verified by `dotnet test QuestBoard.slnx` output above).
