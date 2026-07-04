---
phase: 38-group-scoped-user-list
reviewed: 2026-07-03T00:00:00Z
depth: standard
files_reviewed: 6
files_reviewed_list:
  - QuestBoard.Repository/UserRepository.cs
  - QuestBoard.Domain/Interfaces/IUserRepository.cs
  - QuestBoard.Domain/Interfaces/IUserService.cs
  - QuestBoard.Domain/Services/UserService.cs
  - QuestBoard.Service/Controllers/Admin/AdminController.cs
  - QuestBoard.IntegrationTests/Controllers/AdminControllerIntegrationTests.cs
findings:
  critical: 1
  warning: 2
  info: 2
  total: 5
status: issues_found
---

# Phase 38: Code Review Report

**Reviewed:** 2026-07-03
**Depth:** standard
**Files Reviewed:** 6
**Status:** issues_found

## Summary

This phase closes a cross-tenant PII leak on `AdminController.Users()` (read path) and hardens the four role-change POST actions (`PromoteToAdmin`/`DemoteFromAdmin`/`PromoteToDM`/`DemoteToPlayer`) with a membership guard (write path). The new `GetAllGroupMembers`/`GetAllGroupMembersAsync` layering mirrors the existing `GetAllDungeonMasters`/`GetAllPlayers` pattern cleanly, the guard logic in the four POST actions is correctly placed before the mutating call, and the new regression test (`Users_WhenAdmin_DoesNotShowUsersFromOtherGroups`) proves the read-path fix with both a positive and negative assertion.

However, the fix is incomplete relative to the actual attack surface of this controller. Four other actions in the *same controller* — `EditUser` (GET/POST), `ResetPassword` (GET/POST), `DeleteUser`, and `SendConfirmationEmail` — still resolve their target user via the ungrouped `userService.GetByIdAsync(userId)`/`RemoveAsync(user)`, with no check that the target belongs to the admin's active group. A group Admin in Group A can still edit, delete, reset the password of, or trigger a welcome/confirmation email for any user in Group B simply by supplying their raw id — the same class of cross-tenant authorization bypass this phase's own threat model (T-38-02) explicitly identified and fixed for the four role-change actions, just left open on five sibling actions in the identical controller. This was a scoping decision made in the phase's CONTEXT.md (D-04's "extended scope" is limited to the four role-change actions), but from a pure code-review lens it is a live, exploitable, unfixed vulnerability sitting right next to the ones that were fixed, and should be called out even though it predates this phase.

Additionally, no test was added to prove the new write-path guards (`PromoteToAdmin`/`DemoteFromAdmin`/`PromoteToDM`/`DemoteToPlayer`) actually reject an out-of-group `userId` — only the read-path got a regression test, despite the phase's own threat model classifying the write-path bypass (T-38-02) as "mitigate" and the codebase convention (cited in this same phase's CONTEXT.md) of a dedicated regression test per security fix.

## Critical Issues

### CR-01: EditUser, ResetPassword, DeleteUser, and SendConfirmationEmail are not group-scoped — cross-tenant authorization bypass

**File:** `QuestBoard.Service/Controllers/Admin/AdminController.cs:162, 185, 239, 260, 286, 309`
**Issue:** `EditUser` (GET line 162, POST line 185), `ResetPassword` (GET line 239, POST line 260), `DeleteUser` (line 286), and `SendConfirmationEmail` (line 309) all resolve the target user via the plain `userService.GetByIdAsync(userId)` / `GetByIdAsync(id)`, which — per `BaseRepository.GetByIdAsync` — does a raw `DbSet.FindAsync([id])` with no group filter at all (`UserEntity` intentionally has no EF Core global query filter, per this phase's own CONTEXT.md). None of these five actions verify the target `userId` is a member of `activeGroupContext.ActiveGroupId` before acting.

This means an authenticated group Admin whose active group is Group 1 can:
- **DeleteUser**: permanently delete a user belonging only to Group 2 by POSTing their id.
- **ResetPassword**: reset the password of a user in Group 2, effectively taking over their account.
- **EditUser**: rename or change the email address of a user in Group 2 (triggering a confirmation email to an attacker-controlled address for that victim's account).
- **SendConfirmationEmail**: trigger an unsolicited welcome/password-reset email flow for a Group 2 user.

This is the identical vulnerability class (T-38-02: "Elevation of Privilege / Tampering" via unguarded `userId`) that this same phase explicitly fixed for `PromoteToAdmin`/`DemoteFromAdmin`/`PromoteToDM`/`DemoteToPlayer`, using `GetGroupRoleByIdAsync(userId, groupId.Value) == null` as the guard. The same guard pattern was not applied to these five actions in the same controller, even though `Users()` (the page that links to all of them) is now correctly scoped, meaning the UI no longer *offers* these actions for out-of-group users — but nothing stops a crafted request from invoking them directly, exactly as called out for the four actions that were fixed.

**Fix:** Apply the same membership guard used in `PromoteToAdmin` etc. to all five actions. Example for `DeleteUser`:
```csharp
[HttpDelete]
[ValidateAntiForgeryToken]
public async Task<IActionResult> DeleteUser(int id)
{
    var groupId = activeGroupContext.ActiveGroupId;
    if (groupId == null) return NotFound();

    var targetRole = await userService.GetGroupRoleByIdAsync(id, groupId.Value);
    if (targetRole == null) return NotFound();

    var user = await userService.GetByIdAsync(id);
    if (user == null) return NotFound();

    await userService.RemoveAsync(user);
    return Ok();
}
```
Apply the analogous guard (redirect to `Users()` for GET/POST actions, matching the existing `groupId == null` redirect style) to `EditUser` (both verbs), `ResetPassword` (both verbs), and `SendConfirmationEmail`.

## Warnings

### WR-01: No regression test proves the four write-path guards actually reject out-of-group targets

**File:** `QuestBoard.IntegrationTests/Controllers/AdminControllerIntegrationTests.cs`
**Issue:** This phase added a membership guard to `PromoteToAdmin`, `DemoteFromAdmin`, `PromoteToDM`, and `DemoteToPlayer` (AdminController.cs lines 62-63, 75-76, 88-89, 101-102), and the phase's own threat model (`38-01-PLAN.md`, T-38-02) classifies this as a "mitigate" for an Elevation of Privilege/Tampering threat. However, no test in this file exercises any of those four actions at all — a grep for `PromoteToAdmin|DemoteFromAdmin|PromoteToDM|DemoteToPlayer` in the test file returns zero matches. Only the read-path (`Users_WhenAdmin_DoesNotShowUsersFromOtherGroups`) has a regression test. If a future change accidentally removed or broke the `targetRole == null` guard in any of the four POST actions, no test would catch it.
**Fix:** Add at least one test (can be parameterized/theory across the four actions) asserting that a crafted POST with a `userId` belonging only to a different group leaves the target's role/group membership unchanged and does not create a new `UserGroups` row — mirroring the codebase's stated convention (cited in this phase's own CONTEXT.md) of a dedicated regression test per security-relevant fix.

### WR-02: `GetAllGroupMembers` returns raw `User` objects but callers immediately re-query per-user role — inconsistent with the "why a dedicated method" framing

**File:** `QuestBoard.Repository/UserRepository.cs:51-59`, `QuestBoard.Service/Controllers/Admin/AdminController.cs:29-44`
**Issue:** `GetAllGroupMembers` was added specifically to return all members of a group (any role) in one query. `AdminController.Users()` then loops over every returned user and issues a second query per user (`GetGroupRoleByIdAsync`) to determine their role for display. This N+1 pattern already existed before this phase (flagged as out-of-scope "Claude's Discretion" in CONTEXT.md), so it is not a new defect introduced by this diff and performance is explicitly out of v1 review scope — noting only because the new `GetAllGroupMembers` method, if later reused elsewhere, does not by itself avoid this pattern; a future `(User, GroupRole)`-pair-returning method (mentioned as a discretionary improvement in CONTEXT.md) would remove the need for the per-row round-trip entirely. Not a blocker for this review; recorded for visibility since it sits directly beside the new code.
**Fix:** No action required for this phase. Consider the `(User, GroupRole)` pair-returning method mentioned in CONTEXT.md's "Claude's Discretion" section in a follow-up phase.

## Info

### IN-01: `SetGroupRoleAsync`'s return value is discarded in all four write-path call sites

**File:** `QuestBoard.Service/Controllers/Admin/AdminController.cs:64, 77, 90, 103`
**Issue:** `userService.SetGroupRoleAsync(userId, groupId.Value, GroupRole.X)` returns `int?` (the `UserGroups` row Id per `IUserRepository.SetGroupRoleAsync`'s doc comment), but the result is never checked in any of the four call sites. Since the membership guard above it already confirmed the target has an existing `UserGroups` row, the method will always take the "update" branch and always return a non-null Id, so this isn't currently reachable as a bug — but the discarded return makes the method signature's `int?` nullability pointless at these call sites and could mask a future regression if the guard logic changes.
**Fix:** No functional change required; optional readability improvement would be to use `_ = await userService.SetGroupRoleAsync(...)` to make the intentional discard explicit, or add a comment noting the guard above guarantees the row exists.

### IN-02: `GetEffectiveGroupRoleAsync`'s SuperAdmin bypass is not exercised by the new `GetAllGroupMembers` scoping

**File:** `QuestBoard.Domain/Services/UserService.cs:61-67`, `QuestBoard.Service/Controllers/Admin/AdminController.cs:26-29`
**Issue:** `Users()` uses `activeGroupContext.ActiveGroupId` directly and `GetAllGroupMembersAsync`/`GetGroupRoleByIdAsync` (the raw, non-SuperAdmin-aware lookups), which is correct per D-02 (uniform scoping, no SuperAdmin exception, deliberate and documented). This is not a bug — flagging only because a reader unfamiliar with D-02 could mistake the absence of a SuperAdmin branch here for an oversight given `GetEffectiveGroupRoleAsync` exists elsewhere in the same file and is used for authorization decisions. A one-line comment at the top of `Users()` referencing the intentional trade-off (in plain language, no phase/decision IDs per CLAUDE.md) would preempt that confusion for future maintainers.
**Fix:** Optional: add a plain-language comment such as `// SuperAdmin sees only the active group's members here too — switch groups via the group picker to view another group.` above the `groupId == null` guard in `Users()`.

---

_Reviewed: 2026-07-03_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
