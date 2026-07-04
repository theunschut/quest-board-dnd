---
phase: 38-group-scoped-user-list
verified: 2026-07-03T22:15:00Z
status: passed
score: 9/9 must-haves verified
behavior_unverified: 0
overrides_applied: 0
---

# Phase 38: Group-Scoped User List Verification Report

**Phase Goal:** Scope AdminController.Users() to the currently active group and harden the four role-change POST actions against out-of-group targets, closing a cross-tenant PII leak (USERS-01).
**Verified:** 2026-07-03T22:15:00Z
**Status:** passed
**Re-verification:** No — initial verification

**Scope note:** This verification covers both the original plan (38-01) scope and the code-review escalation (CR-01) that the user chose to fix immediately rather than defer — extending the same membership guard to `EditUser` (GET/POST), `ResetPassword` (GET/POST), `DeleteUser`, and `SendConfirmationEmail`. The practical phase goal ("closing this cross-tenant vulnerability class in AdminController") is verified against the full, current state of `AdminController.cs`, not just the 38-01-PLAN.md must_haves.

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | A group admin opening `/Admin/Users` sees only users who belong to the currently active group | VERIFIED | `AdminController.cs:29` — `Users()` calls `userService.GetAllGroupMembersAsync(groupId.Value)`, replacing the former unscoped `GetAllAsync()`. `UserRepository.cs:51-59` — `GetAllGroupMembers` filters on `DbContext.UserGroups.Any(ug => ug.UserId == u.Id && ug.GroupId == groupId)`, no role restriction, no cross-group leak path. |
| 2 | A group admin never sees a user from a different group on `/Admin/Users`, even when multiple groups share the platform | VERIFIED | Integration test `Users_WhenAdmin_DoesNotShowUsersFromOtherGroups` (`AdminControllerIntegrationTests.cs:281-312`) seeds an out-of-group user (group 2) and an in-group user (group 1), asserts the out-of-group marker is absent and the in-group marker is present. Ran the test directly: **24/24 passed** in the `AdminControllerIntegrationTests` class (includes this test). |
| 3 | Each listed user still shows their correct role within the active group (no regression from existing per-user role display) | VERIFIED | `AdminController.cs:32-44` — the `foreach` loop over `allUsers` calling `GetGroupRoleByIdAsync(user.Id, groupId.Value)` and populating `IsAdmin`/`IsDungeonMaster`/`IsPlayer` is unchanged from before the phase; sort logic (`OrderBy`/`ThenBy`) also unchanged. Confirmed by full `AdminControllerIntegrationTests` run passing (no regression in existing role-display-dependent tests). |
| 4 | A crafted POST to PromoteToAdmin/DemoteFromAdmin/PromoteToDM/DemoteToPlayer for a userId not in the active group silently redirects to Users() with no role change and no UserGroups row created | VERIFIED | `AdminController.cs:57-105` — all four actions call `GetGroupRoleByIdAsync(userId, groupId.Value)` and `if (targetRole == null) return RedirectToAction(nameof(Users));` before `SetGroupRoleAsync`. Proven by `[Theory] RoleChangeActions_WhenTargetUserOutOfGroup_ShouldNotChangeMembership` (`AdminControllerIntegrationTests.cs:318-365`), parameterized across all four endpoints, asserting the target's only `UserGroups` row is untouched (still `GroupId=2`, still `Player`) after the crafted POST. Test run confirmed passing (4/4 theory cases within the 24/24 total). |
| 5 | SuperAdmin viewing `/Admin/Users` sees only the active group's members (no cross-group exception) | VERIFIED | `AdminController.cs:24-53` — `Users()` has no SuperAdmin branch; it reads `activeGroupContext.ActiveGroupId` uniformly for all callers of the `[Authorize(Policy = "AdminOnly")]`-gated controller. Documented as accepted trade-off T-38-SA in `38-01-PLAN.md`'s threat model. |
| 6 | (CR-01 extension) EditUser, ResetPassword, DeleteUser, and SendConfirmationEmail reject a crafted request/POST for a userId not in the active group | VERIFIED | `AdminController.cs:160-377` — all five actions (`EditUser` GET line 160, `EditUser` POST line 185, `ResetPassword` GET line 249, `ResetPassword` POST line 274, `DeleteUser` line 306, `SendConfirmationEmail` line 327) now resolve `groupId`, call `GetGroupRoleByIdAsync(target, groupId.Value)`, and short-circuit (`RedirectToAction(nameof(Users))` or `NotFound()` for `DeleteUser`) before any `GetByIdAsync`/mutation. Guard placed before the target-resolving `GetByIdAsync` in every case; `SendConfirmationEmail`'s guard correctly sits after its pre-existing rate-limit lease acquisition (preserves ordering per REVIEW-FIX.md's stated approach). |
| 7 | The CR-01 fix does not regress existing AdminController integration tests | VERIFIED | Ran `dotnet test --filter "FullyQualifiedName~AdminControllerIntegrationTests"` directly: **24/24 passed** (0 failed), covering the pre-existing tests plus the two new/extended tests from Tasks 3 and WR-01. |
| 8 | The CR-01 fix's follow-up commit (462e94b) correctly repairs the test regression it introduced | VERIFIED | `git show 462e94b` shows the fix is in `QuestBoard.IntegrationTests/Mobile/MobileViewsTests.cs` (not `AdminControllerIntegrationTests.cs`) — `GetMobilePage_AdminEditUser` and `GetMobilePage_AdminResetPassword` now call `SetGroupRoleAsync(targetUser.Id, 1, GroupRole.Player)` to give the target user a group-1 membership row before hitting the now-guarded `EditUser`/`ResetPassword` GET actions. Ran `dotnet test --filter "FullyQualifiedName~MobileViewsTests"` directly: **44/44 passed** (0 failed). |
| 9 | Full solution builds clean with all changes applied | VERIFIED | Ran `dotnet build` directly at repo root: **Build succeeded, 0 Warning(s), 0 Error(s)** across all 5 projects (Domain, Repository, Service, IntegrationTests, UnitTests). |

**Score:** 9/9 truths verified (0 present, behavior-unverified)

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `QuestBoard.Repository/UserRepository.cs` | `GetAllGroupMembers(int groupId, ...)`, manual UserGroups join, no role filter | VERIFIED | Lines 51-59; body uses `DbContext.UserGroups.Any(ug => ug.UserId == u.Id && ug.GroupId == groupId)` — no `ug.GroupRole` token present in this method. |
| `QuestBoard.Domain/Interfaces/IUserRepository.cs` | Matching interface signature | VERIFIED | Line 28: `Task<IList<User>> GetAllGroupMembers(int groupId, CancellationToken token = default);` |
| `QuestBoard.Domain/Interfaces/IUserService.cs` | `GetAllGroupMembersAsync(int groupId, ...)` signature | VERIFIED | Line 49: `Task<IList<User>> GetAllGroupMembersAsync(int groupId, CancellationToken token = default);` |
| `QuestBoard.Domain/Services/UserService.cs` | Pass-through to repository | VERIFIED | Lines 55-58: body calls `repository.GetAllGroupMembers(groupId, token)`. |
| `QuestBoard.Service/Controllers/Admin/AdminController.cs` | `Users()` scoped read + guard on 4 role-change POSTs + guard on 5 CR-01 sibling actions | VERIFIED | `Users()` line 29 uses `GetAllGroupMembersAsync`; guards present at lines 62-63, 75-76, 88-89, 101-102 (role-change actions) and 165-166, 194-195, 254-255, 281-282, 314-315, 343-344 (CR-01 sibling actions). |
| `QuestBoard.IntegrationTests/Controllers/AdminControllerIntegrationTests.cs` | Cross-group-isolation test + write-path guard test | VERIFIED | `Users_WhenAdmin_DoesNotShowUsersFromOtherGroups` (line 281) and `RoleChangeActions_WhenTargetUserOutOfGroup_ShouldNotChangeMembership` (line 323, `[Theory]` across 4 endpoints). Both run and pass. |
| `QuestBoard.IntegrationTests/Mobile/MobileViewsTests.cs` | Fix for CR-01-induced regression | VERIFIED | `git show 462e94b` — two mobile admin tests updated to seed group-1 membership for their target users; both pass under direct test run. |

### Key Link Verification

| From | To | Via | Status | Details |
|------|-----|-----|--------|---------|
| `AdminController.Users()` | `IUserService.GetAllGroupMembersAsync` | direct call, groupId.Value | WIRED | `AdminController.cs:29` |
| `UserService.GetAllGroupMembersAsync` | `IUserRepository.GetAllGroupMembers` | pass-through call | WIRED | `UserService.cs:57` |
| `UserRepository.GetAllGroupMembers` | `QuestBoardContext.UserGroups` | manual join, no role filter | WIRED | `UserRepository.cs:54-57` |
| `AdminController` (4 role-change POSTs) | `IUserService.GetGroupRoleByIdAsync` | membership guard before `SetGroupRoleAsync` | WIRED | `AdminController.cs:62,75,88,101` — all precede the corresponding `SetGroupRoleAsync` call |
| `AdminController` (EditUser/ResetPassword/DeleteUser/SendConfirmationEmail) | `IUserService.GetGroupRoleByIdAsync` | membership guard before `GetByIdAsync`/mutation | WIRED | `AdminController.cs:165,194,254,281,314,343` — all precede the target-resolving `GetByIdAsync` call |

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| Full solution builds | `dotnet build` | Build succeeded, 0 errors, 0 warnings | PASS |
| Cross-group read isolation + write-path guard (single class run) | `dotnet test --filter "FullyQualifiedName~AdminControllerIntegrationTests"` | 24/24 passed | PASS |
| Mobile admin tests unaffected by CR-01 guard | `dotnet test --filter "FullyQualifiedName~MobileViewsTests"` | 44/44 passed | PASS |

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|------------|-------------|--------|----------|
| USERS-01 | 38-01-PLAN.md | Group admin's Users management page shows only members of the currently active group, not all platform users | SATISFIED | `AdminController.Users()` scoped read (truth 1-3), regression test passing (truth 2), REQUIREMENTS.md already marks USERS-01 "Complete". No orphaned requirement IDs found for Phase 38 in REQUIREMENTS.md — only USERS-01 is mapped and it is accounted for in 38-01-PLAN.md's frontmatter. |

### Anti-Patterns Found

None. Scanned all phase-modified files (`AdminController.cs`, `UserRepository.cs`, `IUserRepository.cs`, `IUserService.cs`, `UserService.cs`, `AdminControllerIntegrationTests.cs`, `MobileViewsTests.cs`) for `TBD|FIXME|XXX|TODO|HACK|PLACEHOLDER`, empty-return stubs, and GSD tracking references (`Phase 38`, `USERS-01`, `D-0*`, `WR-0*`, `CR-01`) introduced by this phase's commits. No matches. The two `D-01`/`D-09` references found in `AdminControllerIntegrationTests.cs` lines 223-224 pre-date this phase (git blame: commit `246a184a`, 2026-07-01, two days before Phase 38 started) and are out of scope for this phase's hygiene check.

### Human Verification Required

None. All truths are verified via direct code inspection, a direct (non-cached) `dotnet build`, and direct (non-cached) `dotnet test` runs of the relevant test classes — not SUMMARY.md claims.

### Gaps Summary

No gaps. The original plan's scope (38-01) and the escalated CR-01 fix (applied immediately by user choice rather than deferred) are both fully implemented, wired, tested, and passing. The follow-up regression fix (462e94b) for the CR-01-induced test break in `MobileViewsTests.cs` is also correctly applied and verified passing. Full solution builds clean; 24/24 AdminController integration tests pass; 44/44 Mobile view tests pass.

---

_Verified: 2026-07-03T22:15:00Z_
_Verifier: Claude (gsd-verifier)_
