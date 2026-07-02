---
phase: 30-group-ux-admin-user-creation
verified: 2026-06-30T00:00:00Z
status: human_needed
score: 9/10
behavior_unverified: 1
overrides_applied: 0
human_verification:
  - test: "After login as a multi-group user, select a group, then navigate to the quest board and reload. Verify the selected group name persists in the nav dropdown across requests."
    expected: "The nav dropdown shows the selected group name — not 'Switch Group' — on every subsequent page load until logout or switch."
    why_human: "The integration test harness (TestAuthHandler + Authorization header) does not round-trip ASP.NET Core session cookies the way a browser does. The test asserts the redirect succeeds and the group DB record exists, but cannot read session back from a follow-up HTTP request. Only a real browser session confirms cross-request persistence."
  - test: "As an unauthenticated user, navigate to /GroupPicker/AccessDenied (trigger an access-denied page). Verify the 'Register as DM' button either does not appear or routes correctly."
    expected: "No broken link — the 'Register as DM' button in AccessDenied.cshtml should either be removed or redirect to a valid destination. Currently it links to /Account/Register which returns 404."
    why_human: "This is a user-visible broken link, not a test-assertable routing error. The link renders only for unauthenticated users on the access-denied page."
behavior_unverified_items:
  - truth: "The active group selection persists in ASP.NET Core Session across requests until session expires or user switches groups"
    test: "Log in as a player assigned to one group, complete SelectGroup POST, then make a follow-up GET to /Home/Index with the same session cookie."
    expected: "IActiveGroupContext.ActiveGroupId returns the selected group ID on the second request; the session key 'ActiveGroupId' is present in the cookie-backed session store."
    why_human: "Session persistence is a state transition across requests. The GroupPickerController writes to session (verified by code presence) and ActiveGroupContextService reads from it (verified by code presence and wiring). Whether the write actually persists across requests in a real browser session requires a cross-request live test, which the integration harness cannot perform (documented in test comment)."
---

# Phase 30: Group UX & Admin User Creation — Verification Report

**Phase Goal:** Users land in the right group context after login, can switch groups, see the active group in navigation, and group admins can create new users — self-registration is no longer publicly available.
**Verified:** 2026-06-30
**Status:** human_needed
**Re-verification:** No — initial verification

---

## Goal Achievement

### Observable Truths (ROADMAP.md Success Criteria)

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | A user in exactly one group is auto-placed in that group's context after login; a user in multiple groups sees a group-picker page | VERIFIED | `GroupPickerController.Index` at lines 24–36: `if (!isSuperAdmin && groups.Count == 1)` → writes session and redirects; `if` falls through to `View(...)` for multi-group. Integration test `Index_WhenSingleGroupUser_ShouldRedirectAwayFromPicker` passes. |
| 2 | SuperAdmin always sees the group-picker after login and can enter any group or navigate to the management area | VERIFIED | Controller line 21: `isSuperAdmin ? await groupService.GetAllWithMemberCountAsync()` loads all groups. View `Index.cshtml` lines 10–17: `@if (Model.IsSuperAdmin)` renders "Go to Platform" button linking to `/platform`. Integration test `Index_WhenSuperAdmin_ShouldReturnPickerWithPlatformOption` passes. |
| 3 | The active group name and a "Switch group" link are visible in the navigation bar; clicking "Switch group" returns to the group-picker | VERIFIED | `_Layout.cshtml` lines 141–146: reads `SessionKeys.ActiveGroupName`, displays group name or "Switch Group" fallback, links to `GroupPickerController.Index`. Same pattern mirrored in `_Layout.Mobile.cshtml` lines 112–121. Data flows from session via `IHttpContextAccessor`. |
| 4 | The active group selection persists in ASP.NET Core Session across requests until session expires | PRESENT_BEHAVIOR_UNVERIFIED | `GroupPickerController` lines 31–32 and 46–47 write `SessionKeys.ActiveGroupId` and `SessionKeys.ActiveGroupName`. `ActiveGroupContextService` reads `ActiveGroupId` from session via `httpContextAccessor.HttpContext?.Session?.GetInt32(SessionKeys.ActiveGroupId)`. Wiring is present and complete. Cross-request persistence cannot be confirmed without a real browser session (harness limitation documented in test). |
| 5 | A group admin can create a new user, assign a GroupRole, which triggers email confirmation; that user cannot self-register via the public registration page | VERIFIED | `AdminController.CreateUser` (GET/POST) at lines 96–141: validates model, reads `activeGroupContext.ActiveGroupId`, calls `userService.CreateAsync`, then `userService.SetGroupRoleAsync`, then enqueues `ConfirmationEmailJob`. `/Account/Register` GET and POST both return 404 (Register actions deleted; integration tests assert `HttpStatusCode.NotFound`). |
| 6 | A group admin can promote or demote users within their group between Player, DungeonMaster, and Admin roles | VERIFIED | `AdminController` contains `PromoteToAdmin`, `DemoteFromAdmin`, `PromoteToDM`, `DemoteToPlayer` POST actions (lines 55–93), each reading `activeGroupContext.ActiveGroupId` and calling `userService.SetGroupRoleAsync`. `SetGroupRoleAsync` is an upsert (fixed in plan 30-05). |

**Score:** 9/10 truths verified (1 present, behavior-unverified — see Human Verification)

---

### Per-Requirement Status

| Req | Description | Status | Evidence |
|-----|-------------|--------|----------|
| UX-01 | Single-group users auto-skip the picker | VERIFIED | `GroupPickerController.Index` lines 29–33; test `Index_WhenSingleGroupUser_ShouldRedirectAwayFromPicker` passes |
| UX-02 | Multi-group users see the card picker at /groups/pick | VERIFIED | `GroupPickerController.Index` lines 36–37; view `Index.cshtml` row-cols card grid; test `Index_WhenMultiGroupUser_ShouldReturnPickerPage` passes |
| UX-03 | SuperAdmin sees a "Go to Platform" button on the picker | VERIFIED | `Index.cshtml` lines 10–17 — `@if (Model.IsSuperAdmin)` block renders `/platform` button; test `Index_WhenSuperAdmin_ShouldReturnPickerWithPlatformOption` passes |
| UX-04 | After selecting a group, the active group name appears in the desktop nav dropdown | PRESENT_BEHAVIOR_UNVERIFIED | Session write present in controller; `_Layout.cshtml` reads and displays it. Cross-request session persistence unverifiable in test harness — see Human Verification. |
| UX-05 | A "Switch Group" link in the nav returns user to the picker | VERIFIED | `_Layout.cshtml` line 144–145 and `_Layout.Mobile.cshtml` lines 120–121: link to `asp-controller="GroupPicker" asp-action="Index"` with group name or "Switch Group" label |
| REG-01 | /Account/Register returns 404; login page has no "Create Account" link | VERIFIED* | Register GET/POST actions deleted from `AccountController`; `Register.cshtml` and `Register.Mobile.cshtml` deleted (commit 564628f); Login views contain no `asp-action="Register"` reference. Tests `Register_Get_ShouldReturnNotFound` and `Register_Post_WithValidData_ShouldReturnNotFound` pass. WARNING: `AccessDenied.cshtml` line 48 still contains `asp-action="Register"` linking to the now-404 route — see Warning below. |
| MGMT-07 | Admin can create a user at /Admin/CreateUser with email, name, role | VERIFIED | `AdminController.CreateUser` GET returns the form; POST creates user; `CreateUser.cshtml` contains Email/Name/Password/GroupRole fields. Test `CreateUser_Get_WhenAdmin_ShouldReturnForm` and `CreateUser_Post_WhenAdmin_CreatesUserInActiveGroup` pass. |
| MGMT-08 | Admin can promote/demote users within their active group | VERIFIED | `AdminController` PromoteToAdmin/DemoteFromAdmin/PromoteToDM/DemoteToPlayer actions all use `activeGroupContext.ActiveGroupId` and call `userService.SetGroupRoleAsync` |
| REG-02 | Newly created user is assigned to the admin's active group with chosen GroupRole | VERIFIED | `AdminController.CreateUser` POST reads `activeGroupContext.ActiveGroupId` (server-side only, not from form) and calls `SetGroupRoleAsync`; `UserRepository.SetGroupRoleAsync` is an upsert. Test asserts `UserGroups` row exists with correct `GroupRole`. |
| REG-03 | A confirmation email is queued on successful user creation | VERIFIED | `AdminController.CreateUser` lines 122–127: generates confirmation token, encodes it, enqueues `ConfirmationEmailJob`. |

---

### Required Artifacts

| Artifact | Status | Evidence |
|----------|--------|----------|
| `QuestBoard.Service/Controllers/GroupPickerController.cs` | VERIFIED | Exists, substantive (Index GET + SelectGroup POST), used via login redirect and nav link |
| `QuestBoard.Service/Views/GroupPicker/Index.cshtml` | VERIFIED | Exists, renders card grid + SuperAdmin block + zero-group alert, wired to controller via `asp-action="SelectGroup"` |
| `QuestBoard.Service/Views/GroupPicker/Index.Mobile.cshtml` | VERIFIED | Exists, same structure, single-column layout |
| `QuestBoard.Service/Views/Shared/_Layout.GroupPicker.cshtml` | VERIFIED | Exists, contains exactly one `@RenderBody()`, brand + logout only (no group-context nav) |
| `QuestBoard.Service/ViewModels/GroupPickerViewModels/GroupPickerViewModel.cs` | VERIFIED | Exists with Groups, IsSuperAdmin, HasNoGroups, ReturnUrl properties |
| `QuestBoard.Domain/Interfaces/IGroupService.cs` | VERIFIED | Declares `GetGroupsForUserAsync(int userId, CancellationToken token = default)` |
| `QuestBoard.Repository/GroupRepository.cs` | VERIFIED | Implements `GetGroupsForUserAsync` as single LINQ projection filtered by `g.UserGroups.Any(ug => ug.UserId == userId)` — no N+1 |
| `QuestBoard.Service/Constants/SessionKeys.cs` | VERIFIED | Contains both `ActiveGroupId` and `ActiveGroupName` constants |
| `QuestBoard.Service/Controllers/Admin/AccountController.cs` | VERIFIED | Register GET/POST actions absent; Login POST redirects to `GroupPicker/Index` (line 67) |
| `QuestBoard.Service/Controllers/Admin/AdminController.cs` | VERIFIED | CreateUser GET/POST present; Users() redirects to GroupPicker when ActiveGroupId is null |
| `QuestBoard.Service/ViewModels/AdminViewModels/CreateUserViewModel.cs` | VERIFIED | Email, Name, Password, GroupRole with Data Annotations |
| `QuestBoard.Service/Views/Admin/CreateUser.cshtml` | VERIFIED | Full create-user form with GroupRole select, modern-card layout |
| `QuestBoard.Service/Views/Admin/CreateUser.Mobile.cshtml` | VERIFIED | admin-form-card-mobile pattern, same fields |
| `QuestBoard.Service/Views/Account/Register.cshtml` | VERIFIED DELETED | File absent from filesystem; confirmed deleted in commit 564628f |
| `QuestBoard.Service/Views/Account/Register.Mobile.cshtml` | VERIFIED DELETED | File absent from filesystem |
| `QuestBoard.IntegrationTests/Controllers/GroupPickerControllerIntegrationTests.cs` | VERIFIED | 5 tests covering unauthenticated redirect, UX-01, UX-02, UX-03, UX-04 — all pass |
| `QuestBoard.IntegrationTests/Controllers/AccountControllerIntegrationTests.cs` | VERIFIED | Register GET/POST now assert NotFound; 6 tests pass |
| `QuestBoard.IntegrationTests/Controllers/AdminControllerIntegrationTests.cs` | VERIFIED | 11 tests pass, including 3 new CreateUser tests (auth-gating, form, group-assignment) |

---

### Key Link Verification

| From | To | Via | Status |
|------|----|-----|--------|
| `AccountController.Login POST` | `GroupPickerController.Index` | `RedirectToAction("Index", "GroupPicker", new { returnUrl })` | WIRED |
| `GroupPickerController.Index` | `IGroupService.GetGroupsForUserAsync` | `await groupService.GetGroupsForUserAsync(userId)` for non-SuperAdmin | WIRED |
| `GroupPickerController.SelectGroup` | `SessionKeys` | `HttpContext.Session.SetInt32(SessionKeys.ActiveGroupId, ...)` + `SetString(SessionKeys.ActiveGroupName, ...)` | WIRED |
| `_Layout.cshtml` | `SessionKeys.ActiveGroupName` | `HttpContextAccessor.HttpContext?.Session?.GetString(SessionKeys.ActiveGroupName)` | WIRED |
| `_Layout.Mobile.cshtml` | `SessionKeys.ActiveGroupName` | Same pattern — `HttpContextAccessor.HttpContext?.Session?.GetString(SessionKeys.ActiveGroupName)` | WIRED |
| `AdminController.CreateUser POST` | `activeGroupContext.ActiveGroupId` | `var groupId = activeGroupContext.ActiveGroupId` (server-side, never form) | WIRED |
| `AdminController.CreateUser POST` | `ConfirmationEmailJob` | `jobClient.Enqueue<ConfirmationEmailJob>(...)` | WIRED |
| `UserRepository.SetGroupRoleAsync` | upsert logic | Insert new `UserGroupEntity` when no row exists (fixed in plan 30-05) | WIRED |

---

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| Single-group user redirected (UX-01) | `dotnet test --filter "Index_WhenSingleGroupUser_ShouldRedirectAwayFromPicker"` | Passed | PASS |
| Multi-group user sees picker (UX-02) | `dotnet test --filter "Index_WhenMultiGroupUser_ShouldReturnPickerPage"` | Passed | PASS |
| SuperAdmin sees platform button (UX-03) | `dotnet test --filter "Index_WhenSuperAdmin_ShouldReturnPickerWithPlatformOption"` | Passed | PASS |
| Register GET returns 404 (REG-01) | `dotnet test --filter "Register_Get_ShouldReturnNotFound"` | Passed | PASS |
| Admin CreateUser assigns to active group (REG-02) | `dotnet test --filter "CreateUser_Post_WhenAdmin_CreatesUserInActiveGroup"` | Passed | PASS |
| Full suite green | `dotnet test QuestBoard.IntegrationTests` | 171/171 pass | PASS |
| Unit tests | `dotnet test QuestBoard.UnitTests` | 55/55 pass | PASS |
| Build | `dotnet build QuestBoard.slnx -c Debug` | 0 errors, 30 pre-existing warnings | PASS |

---

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| `QuestBoard.Service/Views/Shared/AccessDenied.cshtml` | 48 | `asp-action="Register"` link pointing to deleted route | WARNING | Broken user-facing link — unauthenticated users on the access-denied page see a "Register as DM" button that leads to a 404. This file was not modified in Phase 30 (last commit `a477ab9`, pre-Phase 30). |

No TBD, FIXME, or XXX markers found in any Phase 30 modified files.

---

### Human Verification Required

#### 1. Cross-Request Session Persistence (UX-04)

**Test:** Log in with a browser as a user assigned to multiple groups. Select a group on the picker page. Navigate to the quest board. Reload the page. Click through to another page (e.g. Shop).
**Expected:** The nav dropdown shows the selected group name — not "Switch Group" — on every page load after selection, until logout or clicking "Switch Group."
**Why human:** The test harness uses `TestAuthHandler` + `Authorization` header (not cookies), so it cannot round-trip ASP.NET Core session cookies across HTTP requests. The code is present and wired (`SetInt32`/`SetString` in controller → `GetInt32` in `ActiveGroupContextService`), but runtime behavior across requests in a real browser session must be observed directly.

#### 2. Broken "Register as DM" Link in AccessDenied Page (REG-01 related)

**Test:** While logged out, trigger an access-denied response (e.g. navigate to `/Admin/Users`). Observe the AccessDenied page.
**Expected:** Either (a) the "Register as DM" button is absent, or (b) it links to a valid destination (e.g. Login page).
**Why human:** `AccessDenied.cshtml` line 48 contains `asp-action="Register"` which resolves to `/Account/Register` — a route that now returns 404. This file was not modified in Phase 30 but the Register route it references was deleted in Phase 30 (plan 30-02). This is a pre-existing view that became broken by the Phase 30 deletion. REG-01 is satisfied (the route is gone) but the broken link is a UX defect that requires either removing the button or updating it to link to the Login page.

---

### Gaps Summary

No gaps are blocking goal achievement. All 9 verifiable truths are confirmed in the codebase. One truth (UX-04 session persistence) is present and wired but behavior-dependent on runtime session round-tripping, requiring human confirmation.

The AccessDenied.cshtml broken link is a WARNING — a pre-existing view made stale by the Phase 30 Register deletion. It does not prevent the phase goal from being achieved (REG-01 is met — the route is 404) but should be cleaned up before the milestone ships to avoid a confusing dead-end for unauthenticated users.

---

_Verified: 2026-06-30_
_Verifier: Claude (gsd-verifier)_
