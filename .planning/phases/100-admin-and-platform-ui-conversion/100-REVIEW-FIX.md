---
phase: 100-admin-and-platform-ui-conversion
fixed_at: 2026-11-19T09:39:08Z
review_path: .planning/phases/100-admin-and-platform-ui-conversion/100-REVIEW.md
iteration: 1
findings_in_scope: 11
fixed: 6
skipped: 5
status: partial
---

# Phase 100: Code Review Fix Report

**Fixed at:** 2026-11-19T09:39:08Z
**Source review:** .planning/phases/100-admin-and-platform-ui-conversion/100-REVIEW.md
**Iteration:** 1

**Summary:**
- Findings in scope: 11 (4 warnings, 7 info; the orchestrator set the scope per finding)
- Fixed: 6 (WR-01, WR-02, WR-03, IN-01, IN-04, IN-06)
- Skipped: 5 (WR-04, IN-02, IN-03, IN-05, IN-07), each on the orchestrator's instruction

Work was done in the main checkout on `milestone/v9-rolling-improvements`, as the orchestrator instructed: no worktree, no branch switch, no stash and no reset. Every fix is one commit with explicit paths. For each behavioural fix, the new facts were run once against the pre-fix source (the changed files swapped in temporarily) and failed, then passed with the fix.

## Fixed Issues

### WR-01: G-23 is only partly fixed. EditUser, ResetPassword and SendConfirmationEmail still answer a stale or out-of-board id with a silent redirect

**Files modified:** `QuestBoard.Service/Controllers/Admin/AdminController.cs`, `QuestBoard.IntegrationTests/Controllers/AdminRoleGuardTests.cs`
**Commit:** 97f68a17
**Applied fix:** All ten silent `RedirectToAction(nameof(Users))` answers now return `this.RedirectWithError(nameof(Users), AdminActionMessages.MemberStale)`. These are the `targetRole == null` and `user == null` answers in EditUser GET and POST, ResetPassword GET and POST, and SendConfirmationEmail. This is the same answer the role actions and DeleteUser give, which completes the G-23 normalisation. New facts, 10 cases:
- `AccountAction_ForSomeoneWhoIsNotOnTheBoard_AnswersTheStaleToast_AndChangesNothing` covers the five entry points for an account that is on another board only. It checks the Error toast, the stale sentence, and that the name, address, key flag, password hash and memberships are unchanged.
- `AccountAction_ForAnAccountThatNoLongerExists_AnswersTheStaleSentence` covers the five entry points for an id that no account holds.

All 10 failed before the fix.

### WR-02: Create User by email still mails reach-refused accounts and gets around the per-recipient resend budget

**Files modified:**
- `QuestBoard.Service/ViewModels/AdminViewModels/AdminActionMessages.cs`
- `QuestBoard.Service/Controllers/Admin/AdminController.cs`
- `QuestBoard.Service/Areas/Platform/Controllers/GroupController.cs`
- `QuestBoard.Service/Program.cs`
- `QuestBoard.UnitTests/Controllers/GroupControllerBoardDeletionTests.cs`
- `QuestBoard.UnitTests/Controllers/GroupControllerBoardNameTests.cs`
- `QuestBoard.UnitTests/Controllers/GroupControllerEditConcurrencyTests.cs`
- `QuestBoard.IntegrationTests/Controllers/AddedMemberMailBudgetTests.cs` (new)
- `QuestBoard.IntegrationTests/Helpers/UnlimitedMailBudgetWebApplicationFactory.cs` (new)
- `QuestBoard.IntegrationTests/Controllers/GroupManagementIntegrationTests.cs`
- `QuestBoard.IntegrationTests/Controllers/AdminControllerIntegrationTests.cs`
- `QuestBoard.IntegrationTests/Tests/AdminPlatformConversionGuardTests.cs`

**Commit:** d5103df7
**Operator decision:** option (a), relayed by the orchestrator. The add checks the existing per-recipient limit (3 per hour, the limiter that Resend Welcome Email and the EditUser email change use) before it mails anyone. With the budget spent, the account is still added, no email is sent, and the Success toast says so. The add is never refused for this reason.

**Applied fix:**
- **Controllers.** Six existing-account branches take a permit from that singleton before any mail work: `AddedToGroup` and `AddedToGroupStrandedAccount` in Admin `CreateUser`, platform `AddMember` and platform `CreateMember`. The permit is taken through a small `TryTakeAddedMemberMailPermit` helper in each controller. If the permit is refused, the person keeps the membership and the toast is `AdminActionMessages.AddedToGroupEmailNotSent`: "{name} has been added to the group as {role}. No email was sent, because too many emails were sent to this account recently." That trailing sentence is the new constant `EmailSkippedRateLimited`, worded after `EmailRateLimited`.
- **What stays outside the limiter.** The welcome email for a brand-new account stays under the per-administrator Create User limit only.
- **Wiring.** `GroupController` takes the limiter as a new constructor dependency, and the three unit-test constructions pass a no-limit limiter. The Program.cs limiter comment now names the add paths.

**Facts:** the new `AddedMemberMailBudgetTests` has 20 cases.
- Under budget, for all three paths, with both confirmed and unconfirmed accounts: the person is added, exactly one mail is queued, and the toast says it was sent.
- Over budget, for the same six cases: the person is added with the role, no mail is queued, the toast is Success and not Error, and it carries the not-sent sentence.
- One fact shows that an add spends the last permit of the same budget the resend button uses.
- The role-word facts from IN-04 now also live here (see below).

All 7 budget facts failed before the fix.

**Test isolation (unexpected; this is why the facts moved):** the per-recipient limiter is a process-wide singleton that never resets, and `TestDataHelper.ClearDatabaseAsync` restarts the in-memory ids. Once adds spend budget, every class that clears its database and then adds an existing account reuses the same target id (usually 2). `GroupManagementIntegrationTests.AddMember_ExistingConfirmedUser_ShouldEnqueueGroupMembershipAddedEmailJob` and `AddMember_ExistingStrandedUser_ShouldEnqueueWelcomeEmailJob` therefore failed on the full run.
- `AddedMemberMailBudgetTests` has its own fixture and never clears its database.
- The IN-04 role-word facts moved into it from `AdminControllerIntegrationTests` and `GroupManagementIntegrationTests`.
- `GroupManagementIntegrationTests` now uses `UnlimitedMailBudgetWebApplicationFactory`. That host replaces only the unkeyed per-recipient limiter with a no-limit one and leaves the keyed Create User limiter alone. The class is not about the mail budget, and every one of its existing facts is unchanged.

**Not applied (outside the relayed decision):** the review's sketch for WR-02 also skipped the mail when the account is outside reach (a SuperAdmin account, or an Admin of another board). The operator decision as relayed covers the per-recipient limit only, so that part was not built. A reach-refused account is still mailed when it is added, now within its 3-an-hour budget. The G-02 residual (O-1 accept) is unchanged.

### WR-03: The role guard's write is an upsert, so a race with a removal re-adds the removed member, outside the membership chokepoint

**Files modified:**
- `QuestBoard.Domain/Interfaces/IUserRepository.cs`
- `QuestBoard.Repository/UserRepository.cs`
- `QuestBoard.Domain/Services/UserService.cs`
- `QuestBoard.Domain/Interfaces/IUserService.cs`
- `QuestBoard.UnitTests/Services/UserServiceTests.cs`
- `QuestBoard.IntegrationTests/Controllers/AdminRoleGuardTests.cs`

**Commits:** ba8de95e, a4854d60 (follow-up)
**Status:** fixed: requires human verification (concurrency logic)
**Applied fix:** There is a new `IUserRepository.UpdateGroupRoleAsync(userId, groupId, role, token)`. It is a tracked update of an existing `UserGroups` row that never creates one, and it returns `false` in two cases:
- No row exists.
- The row is deleted between the read and the save. The save's `DbUpdateConcurrencyException` is caught in the Repository layer.

The first commit also detached the stale entry through `Entry(...).State`. `FeedRevisionWriteSeamTests.NoProductionSource_WritesRowsPastTheChangeTracker` bans that shape in all production source, and the full unit run caught it. The follow-up commit a4854d60 removes the detach rather than weakening the guard. It is not needed: a refused role change answers NotAMember and that request makes no further save.

`UserService.ChangeMemberRoleAsync` now writes through it and maps `false` to `MemberChangeOutcome.NotAMember`, so the controller answers the stale toast. `SetGroupRoleAsync` (still used by `CreateOrAddToGroupAsync`) and `GroupService.AddMemberAsync` are unchanged. The `UserGroups` entity is not a feed table, so the Events/Quests change-tracker rule does not apply, and the write stays on the change tracker anyway.

**Facts:**
- The unit facts now assert the update-only call.
- New unit fact `ChangeMemberRoleAsync_WhenTheMembershipIsRemovedBeforeTheWrite_ReturnsNotAMemberAndNeverReAdds`.
- New integration fact `RoleChange_ThatLosesARaceWithARemoval_DoesNotPutThePersonBackOnTheBoard`. One scope reads the role, another scope removes the member through `IGroupService`, then the first scope's update returns `false` and no membership is re-created.
- Pin `UpdateGroupRole_ForAnExistingMember_StillChangesTheRole`.

These facts use the new method, so they cannot be run against the old upsert.

### IN-01: The QuestRepository.RemoveAsync comment says the delete is all-or-nothing, but its only caller has already committed the signup deletes

**Files modified:** `QuestBoard.Repository/QuestRepository.cs`
**Commit:** 3ccc51b6
**Applied fix:** Comment only. It now says that everything in this method is saved together with the quest in one save, and that the caller has already removed the signups in their own saves, so a refusal here leaves them deleted. There is no behaviour change, in line with WR-04's accepted residual.

### IN-04: Success toasts print the raw enum name ("DungeonMaster") that the display mapping exists to replace

**Files modified:** `QuestBoard.Service/ViewModels/AdminViewModels/AdminActionMessages.cs`, `QuestBoard.Service/Controllers/Admin/AdminController.cs`, `QuestBoard.Service/Areas/Platform/Controllers/GroupController.cs` (facts first added to `AdminControllerIntegrationTests.cs` and `GroupManagementIntegrationTests.cs`, then moved to `AddedMemberMailBudgetTests.cs` in d5103df7)
**Commit:** fe8b16a8
**Applied fix:** The six "added to the group as …" Success toasts now come from `AdminActionMessages.AddedToGroup(name, role)`, which takes the word from `AdminPlatformDisplay.GroupRoleName`. There were two in Admin CreateUser and four in platform AddMember/CreateMember.
- No existing fact pinned the toast text, so new facts pin the word ("Player", "Dungeon Master", "Admin") for Create User by address, Add Member and Create New User.
- The three Dungeon Master cases failed before the fix. Player and Admin match the enum name either way.

The role argument passed to `GroupMembershipAddedEmailJob` was left as the enum name. See the note under "Observations".

### IN-06: The Program.cs comment still says Create User is exempt from limiting

**Files modified:** `QuestBoard.Service/Program.cs`
**Commit:** a85b9176
**Applied fix:** Comment only. The comment now says that Create User's welcome email is exempt from the per-target limiter and has its own per-administrator limit below. WR-02 (d5103df7) then extended the same comment to name the existing-account add paths that the per-target limiter now covers.

## Skipped Issues

### WR-04: Signups are deleted in separate saves before the quest's own save, so a refused final save leaves them gone under "Nothing was changed"

**File:** `QuestBoard.Domain/Services/QuestService.cs:110-125`
**Reason:** skipped: accepted residual (Gate 2 `D100-questservice-removeasync`, recorded in 100-SECURITY.md). The review's verdict and the orchestrator both say to leave `QuestService.RemoveAsync` and the `DeleteQuest` catch as they are. Only the related comment was corrected (IN-01).
**Original issue:** Each signup is deleted with its own `SaveChangesAsync` before the quest's final save. If that final save throws, the toast says "Nothing was changed" while the signups and their date votes are already gone.

### IN-02: The per-administrator Create User budget is also spent on adds that send nothing or fail

**File:** `QuestBoard.Service/Controllers/Admin/AdminController.cs:120-127`
**Reason:** skipped on the orchestrator's instruction: left for a later decision and not fixed in this pass. The impact is low.
**Original issue:** The permit is taken before `CreateOrAddToGroupAsync`, so an `AlreadyMember` answer or an Identity-refused `Failed` answer still spends one of the 20 permits.

### IN-03: The unguarded IUserService.SetGroupRoleAsync stays on the public service interface

**File:** `QuestBoard.Domain/Interfaces/IUserService.cs:131`
**Reason:** skipped on the orchestrator's instruction: left for a later decision and not fixed in this pass. Note that integration tests call `IUserService.SetGroupRoleAsync` to seed memberships (for example in `AdminControllerRateLimitIntegrationTests`), so removing it from the interface means more than a one-line change.
**Original issue:** After this phase nothing in the Service layer calls it, and leaving it public offers a one-line way around the self and last-Admin guards.

### IN-05: A failed Create New User from the dialog drops Identity's reason without logging it

**File:** `QuestBoard.Service/Areas/Platform/Controllers/GroupController.cs` (CreateMember `Failed` branch)
**Reason:** skipped on the orchestrator's instruction: left for a later decision and not fixed in this pass.
**Original issue:** `result.Errors` is discarded and not logged, so the operator cannot tell why a create keeps failing.

### IN-07: The Service layer catches EF Core's DbUpdateException directly

**File:** `QuestBoard.Service/Controllers/Admin/AdminController.cs:13`
**Reason:** skipped on the orchestrator's instruction. The review marks the fix optional, and this follows existing precedent (`GroupController`, `ContactCategoryManagementController`). No EF package is added to the Service project.
**Original issue:** `DeleteQuest` depends on `Microsoft.EntityFrameworkCore.DbUpdateException`, which is a persistence type.

## Observations

- **Membership email still names the role by its enum name.** `GroupMembershipAddedEmailJob` still receives `GroupRole.ToString()`, and `Components/Emails/AddedToGroup.razor` prints it in prose ("as a <strong>DungeonMaster</strong>"). IN-04's review text allowed changing it if the template expects display text, which it does. The orchestrator scoped IN-04 to the toasts, so the email is unchanged. It is a one-line follow-up in each controller if wanted.
- **New write not yet in the security register.** `UserRepository.UpdateGroupRoleAsync` is a new tracked write on `UserGroups`. It is not in the "Writes added by this phase" table in 100-SECURITY.md, which was not edited here.

## Verification

All gates ran in the **main checkout** (`/mnt/Data/repos/quest-board-dnd`, branch `milestone/v9-rolling-improvements`). No worktree was used, so the numbers reproduce from this tree.

All three runs were at HEAD a4854d60:
- `dotnet build`: 0 errors and 73 warnings (the existing analyzer warnings, the same count as before the fixes)
- `dotnet test QuestBoard.UnitTests`: 1637 passed, 0 failed
- `dotnet test QuestBoard.IntegrationTests`: 3070 passed, 0 failed, 17 skipped
  - The 17 skips are the SQL-gated facts, the same set as at the phase close.
  - At the phase close the run was 3036 passed. The new facts are 10 for WR-01, 2 for WR-03, and 20 in `AddedMemberMailBudgetTests`; the IN-04 facts moved there.

One earlier full unit run failed `FeedRevisionWriteSeamTests.NoProductionSource_WritesRowsPastTheChangeTracker` on the WR-03 detach. It was fixed in a4854d60, as described under WR-03. The SQL-backed suites were not run, since no fix touched the quest-delete or migration paths.

---

_Fixed: 2026-11-19T09:39:08Z_
_Fixer: Claude (gsd-code-fixer)_
_Iteration: 1_
