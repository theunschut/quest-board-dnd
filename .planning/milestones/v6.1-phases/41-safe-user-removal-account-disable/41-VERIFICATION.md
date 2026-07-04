---
phase: 41-safe-user-removal-account-disable
verified: 2026-07-04T00:00:00Z
status: passed
score: 5/5 must-haves verified
behavior_unverified: 0
overrides_applied: 0
---

# Phase 41: Safe User Removal & Account Disable Verification Report

**Phase Goal:** Removing a user from a group no longer risks deleting their account or breaking the database, and a SuperAdmin has a real way to deactivate a problem account without destroying data.
**Verified:** 2026-07-04
**Status:** passed
**Re-verification:** No — initial verification

## Goal Achievement

### Observable Truths (ROADMAP Success Criteria)

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | A group admin clicking "Delete" (now "Remove from Group") on the Users page removes the user from the active group only — account and other memberships untouched | VERIFIED | `AdminController.DeleteUser` (line 356) calls `groupService.RemoveMemberAsync(groupId.Value, id)` instead of `userService.RemoveAsync(user)`. `GroupRepository.RemoveMemberAsync` only deletes the `UserGroupEntity` row and calls `SaveChangesAsync` — never touches `UserEntity`. Test `DeleteUser_Post_RemovesGroupMembershipOnly_AccountAndOtherMembershipsIntact` passes (ran in isolation, GREEN). |
| 2 | A user who has DM'd quests, listed shop items, made gold transactions, or received reminders can still be removed from a group without an unhandled server error | VERIFIED | Since `RemoveMemberAsync` only deletes the `UserGroupEntity` join row, none of the `NoAction`/`Cascade` FKs on `UserEntity` (QuestEntity.DungeonMasterId, ShopItemEntity.CreatedByDmId, UserTransactionEntity.UserId, ReminderLogEntity.PlayerId, etc. — confirmed in `QuestBoardContext.OnModelCreating`) are ever triggered. Test `DeleteUser_Post_WithQuestShopTransactionReminderHistory_DoesNotThrow` passes (ran in isolation, GREEN), asserting 200 OK and all FK-history rows survive. |
| 3 | A SuperAdmin can disable a user account so it can no longer sign in, with no data deleted | VERIFIED | `UsersController.Disable` ([Area("Platform")][Authorize(Policy="SuperAdminOnly")]) calls `identityService.DisableUserAsync(userId)`, which sets `LockoutEnd = DateTimeOffset.MaxValue` and bumps the security stamp — no row deletion. Test `Disable_Post_SetsLockoutEnd_AccountNotDeleted` passes (ran in isolation, GREEN), confirming `LockoutEnd == MaxValue` and the account row still resolves via `UserManager.FindByIdAsync`. |
| 4 | A SuperAdmin can re-enable a previously disabled account, restoring login access | VERIFIED | `UsersController.Enable` calls `identityService.EnableUserAsync(userId)`, which sets `LockoutEnd = null`. Test `Enable_Post_ClearsLockoutEnd_LoginRestored` passes (ran in isolation, GREEN). |
| 5 | A disabled user attempting to log in sees a message that does not falsely imply a 15-minute temporary lockout | VERIFIED | `AccountController.Login`'s `IsLockedOut` branch resolves `lockoutEnd` via `GetIdByEmailAsync` + `GetLockoutEndAsync` and does an exact `== DateTimeOffset.MaxValue` comparison, showing "This account has been disabled. Contact an administrator." only for that case; ordinary lockouts keep "...Try again in 15 minutes." Tests `Login_Post_DisabledAccount_ShowsDisabledMessage` and `Login_Post_TemporaryLockout_ShowsFifteenMinuteMessage` both pass (ran in isolation, GREEN). |

**Score:** 5/5 truths verified (0 present, behavior-unverified)

### Additional Plan-Level Must-Haves

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 6 | Removal button reads "Remove from Group" with accurate confirm copy, not "Delete" | VERIFIED | `Users.cshtml` line 171 and `Users.Mobile.cshtml` line 102 both render "Remove from Group" with `fa-user-minus` icon (grep for `fa-trash` on the removal control returns 0 matches in both files); JS confirm string matches spec exactly. |
| 7 | Active-session revocation window bounded (~5 min, not 30-min default) | VERIFIED | `Program.cs` lines 67-70: `Configure<SecurityStampValidatorOptions>` sets `ValidationInterval = TimeSpan.FromMinutes(5)`, placed after the `AddIdentity(...).AddDefaultTokenProviders()` chain. |
| 8 | Disable/enable never touches `LockoutEnabled` (DB-only escape hatch preserved) | VERIFIED | `grep -c "LockoutEnabled" QuestBoard.Repository/IdentityService.cs` returns 0. |
| 9 | SuperAdmin cannot disable their own account (self-disable guard) | VERIFIED | `UsersController.Disable` compares `identityService.GetUserIdAsync(User)` to `userId` and short-circuits with `TempData["Error"]` before calling `DisableUserAsync`. Test `Disable_Post_SelfTarget_IsBlocked` passes; view renders a disabled button with tooltip for the current user's own row. |
| 10 | SuperAdmin can disable a peer SuperAdmin (no special-casing) | VERIFIED | `UsersController.Disable` has no role check on the target. Test `Disable_Post_PeerSuperAdmin_IsAllowed` passes. |

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `QuestBoard.Service/Controllers/Admin/AdminController.cs` | `DeleteUser` calls `groupService.RemoveMemberAsync` | VERIFIED | Line 356: `await groupService.RemoveMemberAsync(groupId.Value, id);`. Old `userService.RemoveAsync(user)` call removed. Guards (`groupId == null`, `GetGroupRoleByIdAsync` cross-group check) preserved. |
| `QuestBoard.Service/Views/Admin/Users.cshtml` | "Remove from Group" button + `fa-user-minus` | VERIFIED | Line 169-171. |
| `QuestBoard.Service/Views/Admin/Users.Mobile.cshtml` | Same, mobile | VERIFIED | Line 101-102. |
| `QuestBoard.IntegrationTests/Controllers/AdminControllerIntegrationTests.cs` | Two SAFE-01 tests | VERIFIED | Both present, both pass in isolation. |
| `QuestBoard.Domain/Interfaces/IIdentityService.cs` | `DisableUserAsync`/`EnableUserAsync`/`GetLockoutEndAsync` signatures | VERIFIED | Lines 108-121, each with XML doc. |
| `QuestBoard.Repository/IdentityService.cs` | Implementations | VERIFIED | Lines 184-214; `SetLockoutEndDateAsync` + `UpdateSecurityStampAsync` on disable, `SetLockoutEndDateAsync(entity, null)` on enable (no stamp bump), `GetLockoutEndAsync` returns `entity?.LockoutEnd`. |
| `QuestBoard.Service/Program.cs` | `SecurityStampValidatorOptions` config | VERIFIED | Lines 67-70. |
| `QuestBoard.Service/Areas/Platform/Controllers/UsersController.cs` | `SuperAdminOnly` Index/Disable/Enable | VERIFIED | Full file read; class-level `[Area("Platform")][Authorize(Policy = "SuperAdminOnly")]`. |
| `QuestBoard.Service/ViewModels/PlatformViewModels/PlatformUserViewModel.cs` | `User` + `IsDisabled` | VERIFIED | Used correctly by `UsersController.Index` and both views. |
| `QuestBoard.Service/Areas/Platform/Views/Users/Index.cshtml` | Cross-group table with status badge + actions | VERIFIED | Full file read; modern-card shell, Active/Disabled badges, single action per row, self-guard disabled control. |
| `QuestBoard.Service/Areas/Platform/Views/Users/Index.Mobile.cshtml` | Mobile stacked-card variant | VERIFIED | Full file read; identical logic to desktop. |
| `QuestBoard.Service/Areas/Platform/Views/Group/Index.cshtml` / `.Mobile.cshtml` | "Manage Users" entry point | VERIFIED | Both contain the link with `fa-users-cog`. |
| `QuestBoard.IntegrationTests/Controllers/UsersControllerIntegrationTests.cs` | Disable/Enable/self-guard/peer/CSRF tests | VERIFIED | 6 tests present (5 planned + 1 reflection-based CSRF), all pass in isolation. |
| `QuestBoard.Service/Controllers/Admin/AccountController.cs` | Login IsLockedOut branch distinguishes disabled vs. lockout | VERIFIED | Composes `GetIdByEmailAsync` + `GetLockoutEndAsync`, exact `== DateTimeOffset.MaxValue` comparison. |
| `QuestBoard.IntegrationTests/Controllers/AccountControllerIntegrationTests.cs` | Disabled-vs-lockout message tests | VERIFIED | Both tests present and pass in isolation. |

### Key Link Verification

| From | To | Via | Status | Details |
|------|-----|-----|--------|---------|
| `AdminController.DeleteUser` | `IGroupService.RemoveMemberAsync` | direct call | WIRED | Confirmed at line 356. |
| `Users.cshtml` removal button | `AdminController.DeleteUser` | `fetch DELETE /Admin/DeleteUser/{id}` | WIRED | Confirmed unchanged fetch shape with `RequestVerificationToken` header. |
| `IdentityService.DisableUserAsync/EnableUserAsync` | `UserManager` | `SetLockoutEndDateAsync` + `UpdateSecurityStampAsync` | WIRED | Confirmed in IdentityService.cs. |
| `Program.cs` | `SecurityStampValidatorOptions` | `Configure<T>` | WIRED | Confirmed. |
| `UsersController` | `IIdentityService` | `DisableUserAsync`/`EnableUserAsync`/`GetLockoutEndAsync` | WIRED | Confirmed at lines 19, 40, 50. |
| `Group/Index.cshtml` | `UsersController` | `asp-controller="Users" asp-action="Index" asp-area="Platform"` | WIRED | Confirmed "Manage Users" link. |
| `AccountController.Login` | `IIdentityService` | `GetIdByEmailAsync` + `GetLockoutEndAsync` | WIRED | Confirmed in Login's IsLockedOut branch. |

### Behavioral Spot-Checks (Tests Run in Isolation, Not Full Suite)

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| Build succeeds | `dotnet build` | 0 Warnings, 0 Errors | PASS |
| SAFE-01 group-only removal + FK no-throw | `dotnet test --filter FullyQualifiedName~AdminControllerIntegrationTests.DeleteUser` | 2/2 passed | PASS |
| SAFE-02/03 disable/enable/self-guard/peer/CSRF | `dotnet test --filter FullyQualifiedName~UsersControllerIntegrationTests` | 6/6 passed | PASS |
| SAFE-04 disabled vs. temporary lockout messaging | `dotnet test --filter FullyQualifiedName~AccountControllerIntegrationTests.Login_Post_DisabledAccount_ShowsDisabledMessage\|Login_Post_TemporaryLockout_ShowsFifteenMinuteMessage` | 2/2 passed | PASS |

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|-------------|-------------|--------|----------|
| SAFE-01 | 41-01 | Delete button removes from group only, account/other memberships intact | SATISFIED | `AdminController.DeleteUser` + `GroupRepository.RemoveMemberAsync`; 2 passing tests |
| SAFE-02 | 41-02, 41-03 | SuperAdmin can disable an account, no data deleted | SATISFIED | `IIdentityService.DisableUserAsync` + `UsersController.Disable`; 2 passing tests |
| SAFE-03 | 41-02, 41-03 | SuperAdmin can re-enable a disabled account | SATISFIED | `IIdentityService.EnableUserAsync` + `UsersController.Enable`; 1 passing test |
| SAFE-04 | 41-04 | Disabled user sees accurate message, not 15-minute lockout copy | SATISFIED | `AccountController.Login` IsLockedOut branch; 2 passing tests |

No orphaned requirements — all four IDs (SAFE-01 through SAFE-04) declared across plans match REQUIREMENTS.md's traceability table exactly.

### Anti-Patterns Found

None. Scanned all 10 modified/created files for `TBD|FIXME|XXX|TODO|HACK|PLACEHOLDER` — zero matches. No hardcoded empty-data stubs found; `fa-trash` fully removed from the removal control in both desktop and mobile Admin Users views.

### Human Verification Required

None. All must-haves are either directly code-verified (artifact/wiring checks) or behaviorally verified via passing integration tests exercising the exact state transitions (group-membership-only removal, LockoutEnd sentinel set/clear, self-disable short-circuit, peer-disable, and login message branching). No visual-only or real-time behaviors remain unverified — the UI-SPEC-driven button/badge markup was read directly and matches spec.

### Gaps Summary

No gaps. All 5 ROADMAP Success Criteria and all plan-level must-haves are verified against actual code (not SUMMARY claims), with targeted integration tests run in isolation and passing. The REQUIREMENTS.md checkboxes for SAFE-01 through SAFE-04 remain unchecked (`[ ]`) as of this verification — this is a documentation staleness note, not a code gap; the traceability table's phase-status column already correctly marks all four as mapped to Phase 41.

---

_Verified: 2026-07-04_
_Verifier: Claude (gsd-verifier)_
