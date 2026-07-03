---
phase: 38-group-scoped-user-list
fixed_at: 2026-07-03T21:48:42Z
review_path: .planning/phases/38-group-scoped-user-list/38-REVIEW.md
iteration: 1
findings_in_scope: 3
fixed: 2
skipped: 1
status: partial
---

# Phase 38: Code Review Fix Report

**Fixed at:** 2026-07-03T21:48:42Z
**Source review:** .planning/phases/38-group-scoped-user-list/38-REVIEW.md
**Iteration:** 1

**Summary:**
- Findings in scope: 3 (1 Critical, 2 Warning — `fix_scope: critical_warning`, Info findings excluded)
- Fixed: 2
- Skipped: 1

## Fixed Issues

### CR-01: EditUser, ResetPassword, DeleteUser, and SendConfirmationEmail are not group-scoped — cross-tenant authorization bypass

**Files modified:** `QuestBoard.Service/Controllers/Admin/AdminController.cs`
**Commit:** `58515c6`
**Applied fix:** Added the same active-group membership guard used by `PromoteToAdmin`/`DemoteFromAdmin`/`PromoteToDM`/`DemoteToPlayer` to the five remaining actions that resolved their target user via the ungrouped `userService.GetByIdAsync`: `EditUser` (GET and POST), `ResetPassword` (GET and POST), `DeleteUser`, and `SendConfirmationEmail`. Each action now resolves `activeGroupContext.ActiveGroupId`, calls `userService.GetGroupRoleByIdAsync(targetId, groupId.Value)`, and redirects/404s before touching the target user if the guard returns null (i.e., the target is not a member of the admin's active group). `SendConfirmationEmail`'s guard is placed after the existing rate-limit lease acquisition to preserve that action's ordering. Verified with `dotnet build` (0 errors) and confirmed via the full `AdminControllerIntegrationTests` suite (24/24 passed after the companion WR-01 fix).

### WR-01: No regression test proves the four write-path guards actually reject out-of-group targets

**Files modified:** `QuestBoard.IntegrationTests/Controllers/AdminControllerIntegrationTests.cs`
**Commit:** `662aea8`
**Applied fix:** Added a `[Theory]` test `RoleChangeActions_WhenTargetUserOutOfGroup_ShouldNotChangeMembership` parameterized across `PromoteToAdmin`, `DemoteFromAdmin`, `PromoteToDM`, and `DemoteToPlayer`. It seeds a target user who is a member only of group 2, then POSTs a crafted request to each endpoint from a group-1 admin client, and asserts the target's `UserGroups` row is untouched (still `GroupId=2`, still `Player`) and no new group-1 membership row was created. While implementing this, running the pre-existing suite surfaced that `CreateUnconfirmedTargetUserAsync` (used by `ResetPassword_Post_WhenAdmin_SucceedsForTargetUser` and `EditUser_EmailChange_ExceedingRateLimit_ShouldReturn429`) created target users via the raw `userService.CreateAsync` with no group membership at all — which the new CR-01 guard now correctly rejects. Fixed the helper to also call `SetGroupRoleAsync(userId, 1, GroupRole.Player)`, mirroring the group-assignment step `AdminController.CreateUser` performs in production. All 24 tests in `AdminControllerIntegrationTests` pass after this fix (4/4 new theory cases, plus the 2 previously-regressed tests, plus all 18 other existing tests).

## Skipped Issues

### WR-02: `GetAllGroupMembers` returns raw `User` objects but callers immediately re-query per-user role — inconsistent with the "why a dedicated method" framing

**File:** `QuestBoard.Repository/UserRepository.cs:51-59`, `QuestBoard.Service/Controllers/Admin/AdminController.cs:29-44`
**Reason:** REVIEW.md's own Fix section states "No action required for this phase" — this is an out-of-scope N+1 performance note explicitly deferred to a future phase (a `(User, GroupRole)`-pair-returning method), not a defect introduced by this diff. No code change applied per the reviewer's own guidance.
**Original issue:** `Users()` loops over every group member and issues a second per-user query (`GetGroupRoleByIdAsync`) to resolve their role for display — pre-existing pattern, not new in this phase, and explicitly out of v1 review scope.

---

_Fixed: 2026-07-03T21:48:42Z_
_Fixer: Claude (gsd-code-fixer)_
_Iteration: 1_
