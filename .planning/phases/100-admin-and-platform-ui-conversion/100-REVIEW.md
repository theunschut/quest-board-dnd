---
phase: 100-admin-and-platform-ui-conversion
reviewed: 2026-11-19T09:19:50Z
depth: standard
files_reviewed: 89
files_reviewed_list:
  - QuestBoard.Domain/Enums/AccountReach.cs
  - QuestBoard.Domain/Enums/MemberChangeOutcome.cs
  - QuestBoard.Domain/Interfaces/IGroupRepository.cs
  - QuestBoard.Domain/Interfaces/IGroupService.cs
  - QuestBoard.Domain/Interfaces/IIdentityService.cs
  - QuestBoard.Domain/Interfaces/IUserRepository.cs
  - QuestBoard.Domain/Interfaces/IUserService.cs
  - QuestBoard.Domain/Models/EmailSettings.cs
  - QuestBoard.Domain/Services/GroupService.cs
  - QuestBoard.Domain/Services/UserService.cs
  - QuestBoard.IntegrationTests/Controllers/AdminAccountReachTests.cs
  - QuestBoard.IntegrationTests/Controllers/AdminControllerIntegrationTests.cs
  - QuestBoard.IntegrationTests/Controllers/AdminControllerRateLimitIntegrationTests.cs
  - QuestBoard.IntegrationTests/Controllers/AdminQuestDeleteTests.cs
  - QuestBoard.IntegrationTests/Controllers/AdminRoleGuardTests.cs
  - QuestBoard.IntegrationTests/Controllers/BoardPageBoardListTests.cs
  - QuestBoard.IntegrationTests/Controllers/BoardPageGateMatrixTests.cs
  - QuestBoard.IntegrationTests/Controllers/GroupManagementIntegrationTests.cs
  - QuestBoard.IntegrationTests/Controllers/LinkedBoardsPlatformIntegrationTests.cs
  - QuestBoard.IntegrationTests/Controllers/PlatformBoardDeleteTests.cs
  - QuestBoard.IntegrationTests/Controllers/QuestStatusBoardClockTests.cs
  - QuestBoard.IntegrationTests/Controllers/UsersControllerIntegrationTests.cs
  - QuestBoard.IntegrationTests/Helpers/CannedResendHandler.cs
  - QuestBoard.IntegrationTests/Helpers/EmailStatsWebApplicationFactory.cs
  - QuestBoard.IntegrationTests/Mobile/MobileViewsTests.cs
  - QuestBoard.IntegrationTests/Tests/AdminPlatformConversionGuardTests.cs
  - QuestBoard.IntegrationTests/Tests/AdminPlatformDisplayTests.cs
  - QuestBoard.IntegrationTests/Tests/AdminPlatformRenderTests.cs
  - QuestBoard.IntegrationTests/Tests/BoardPageEndToEndTests.cs
  - QuestBoard.IntegrationTests/Tests/BoardPageStaticGuardTests.cs
  - QuestBoard.IntegrationTests/Tests/EmailStatsRenderTests.cs
  - QuestBoard.IntegrationTests/Tests/LinkedBoardStaticGuardTests.cs
  - QuestBoard.IntegrationTests/Tests/QuestDeleteSqlServerTests.cs
  - QuestBoard.IntegrationTests/Tests/UiFoundationLayerGuardTests.cs
  - QuestBoard.Repository/GroupRepository.cs
  - QuestBoard.Repository/IdentityService.cs
  - QuestBoard.Repository/QuestRepository.cs
  - QuestBoard.Repository/UserRepository.cs
  - QuestBoard.Service/Areas/Platform/Controllers/GroupController.cs
  - QuestBoard.Service/Areas/Platform/Views/Group/Create.Mobile.cshtml
  - QuestBoard.Service/Areas/Platform/Views/Group/Create.cshtml
  - QuestBoard.Service/Areas/Platform/Views/Group/Delete.cshtml
  - QuestBoard.Service/Areas/Platform/Views/Group/Edit.Mobile.cshtml
  - QuestBoard.Service/Areas/Platform/Views/Group/Edit.cshtml
  - QuestBoard.Service/Areas/Platform/Views/Group/Index.Mobile.cshtml
  - QuestBoard.Service/Areas/Platform/Views/Group/Index.cshtml
  - QuestBoard.Service/Areas/Platform/Views/Group/Members.Mobile.cshtml
  - QuestBoard.Service/Areas/Platform/Views/Group/Members.cshtml
  - QuestBoard.Service/Areas/Platform/Views/Group/_BoardDeleteBody.cshtml
  - QuestBoard.Service/Areas/Platform/Views/LinkedBoards/Index.Mobile.cshtml
  - QuestBoard.Service/Areas/Platform/Views/LinkedBoards/Index.cshtml
  - QuestBoard.Service/Areas/Platform/Views/Users/Index.Mobile.cshtml
  - QuestBoard.Service/Areas/Platform/Views/Users/Index.cshtml
  - QuestBoard.Service/Controllers/Admin/AdminController.cs
  - QuestBoard.Service/Program.cs
  - QuestBoard.Service/Services/AdminRateLimitKeys.cs
  - QuestBoard.Service/Services/ResendBaseAddress.cs
  - QuestBoard.Service/ViewModels/AdminViewModels/AdminActionMessages.cs
  - QuestBoard.Service/ViewModels/AdminViewModels/AdminPlatformDisplay.cs
  - QuestBoard.Service/ViewModels/AdminViewModels/QuestStatusBadgeModel.cs
  - QuestBoard.Service/ViewModels/AdminViewModels/ResetPasswordViewModel.cs
  - QuestBoard.Service/ViewModels/AdminViewModels/UserManagementViewModel.cs
  - QuestBoard.Service/Views/Admin/CreateUser.Mobile.cshtml
  - QuestBoard.Service/Views/Admin/CreateUser.cshtml
  - QuestBoard.Service/Views/Admin/EditUser.Mobile.cshtml
  - QuestBoard.Service/Views/Admin/EditUser.cshtml
  - QuestBoard.Service/Views/Admin/EmailStats.cshtml
  - QuestBoard.Service/Views/Admin/Quests.Mobile.cshtml
  - QuestBoard.Service/Views/Admin/Quests.cshtml
  - QuestBoard.Service/Views/Admin/ResetPassword.Mobile.cshtml
  - QuestBoard.Service/Views/Admin/ResetPassword.cshtml
  - QuestBoard.Service/Views/Admin/Users.Mobile.cshtml
  - QuestBoard.Service/Views/Admin/Users.cshtml
  - QuestBoard.Service/Views/Shared/_BoardTypeBadge.cshtml
  - QuestBoard.Service/Views/Shared/_QuestStatusBadge.cshtml
  - QuestBoard.Service/Views/Shared/_RoleBadge.cshtml
  - QuestBoard.Service/wwwroot/css/admin-form.mobile.css
  - QuestBoard.Service/wwwroot/css/admin-pages.css
  - QuestBoard.Service/wwwroot/css/admin-pages.mobile.css
  - QuestBoard.Service/wwwroot/css/admin-quests.mobile.css
  - QuestBoard.Service/wwwroot/css/admin-users.mobile.css
  - QuestBoard.Service/wwwroot/css/components.css
  - QuestBoard.Service/wwwroot/css/platform-group.mobile.css
  - QuestBoard.Service/wwwroot/css/platform-linked-boards.mobile.css
  - QuestBoard.Service/wwwroot/css/platform-pages.css
  - QuestBoard.Service/wwwroot/css/platform-pages.mobile.css
  - QuestBoard.Service/wwwroot/css/platform-users.mobile.css
  - QuestBoard.UnitTests/Services/GroupServiceTests.cs
  - QuestBoard.UnitTests/Services/UserServiceTests.cs
findings:
  critical: 0
  warning: 4
  info: 7
  total: 11
status: issues_found
---

# Phase 100: Code Review Report

**Reviewed:** 2026-11-19T09:19:50Z
**Depth:** standard
**Files Reviewed:** 89 (six of the CSS paths are deletions; the review confirmed nothing still references them)
**Status:** issues_found

## Narrative Findings (AI reviewer)

## Summary

The review covered the server-side changes most closely: the reach rule (`GetAccountReachAsync` and the controller gate), the self and last-Admin guards (`ChangeMemberRoleAsync`, `TryRemoveMemberAsync`), the Platform `RemoveMember` and `CreateMember` paths, both rate limiters in `Program.cs`, the quest-delete cleanup in `QuestRepository.RemoveAsync`, `AdminActionMessages`, and the conversion of every destructive action to an ordinary anti-forgery POST form behind `confirm-submit.js`. The views and CSS got a lighter pass.

What holds:
- The reach rule is enforced in the controller before any read or write on EditUser, ResetPassword (GET and POST) and SendConfirmationEmail, and again inside `UserService.ResetPasswordAsync`.
- `GetUserIdsAdministeringOtherGroupsAsync` reads `UserGroups`, which has no board query filter, so the "Admin of another board" check sees every board.
- The guards judge the role the person ends up with, so a crafted action name cannot get past them.
- Both limiters are now taken after the scope, reach and already-confirmed checks and before any write. The keyed `CreateUser` limiter cannot be resolved in place of the unkeyed per-target one.
- `ResendBaseAddress` honours the override only on a Development host and only for http or https.
- The quest-delete cleanup stays on the change tracker. `FindAsync`, `RemoveRange` and a tracked property set to null are saved together with the quest. `OriginalQuestId` is not a feed field.
- Every in-scope view that carries `data-confirm-message` loads `confirm-submit.js`. Each such form is a `method="post"` with `asp-action`, so the form tag helper adds the token.
- The CreateMember dialog posts the unprefixed `Email`, `Name` and `GroupRole` that the unprefixed `CreateMemberViewModel` binds, so the field-name mismatch is fixed.
- No planning IDs appear in source or CSS, `IBoardClock` is used for the board day, no EF package was added, and `appsettings.json` is untouched.

Four problems remain:
1. The stale-page normalisation that G-23 claims is closed was done for only some of the actions.
2. Create User's add-by-email path still mails reach-refused accounts and gets around the per-recipient resend budget.
3. A race lets the role guard re-add a member who was just removed, through the upsert.
4. The `QuestService.RemoveAsync` residual. It is race-only, as recorded. Its true scope is slightly wider than the wording in SECURITY.md, but it is not worse in kind.

## Warnings

### WR-01: G-23 is only partly fixed. EditUser, ResetPassword and SendConfirmationEmail still answer a stale or out-of-board id with a silent redirect

**File:** `QuestBoard.Service/Controllers/Admin/AdminController.cs:205`, `:213`, `:237`, `:245`, `:298`, `:306`, `:328`, `:336`, `:389`, `:397`
**Issue:** G-23's approved rule was "normalise to a redirect with an Error toast on the Admin controller", and SECURITY.md marks it closed. The fix only reached the four role actions, DeleteUser and DeleteQuest. EditUser (GET and POST), ResetPassword (GET and POST) and SendConfirmationEmail still return a bare `RedirectToAction(nameof(Users))` with no toast in two cases:
- the target is not a member of the active board (`targetRole == null`), or
- the account is gone (`user == null`).

This is reachable in normal use. An Admin who keeps the Users page open while another Admin removes a member, then presses Edit, Resend Welcome Email or Reset Password on that row, lands back on Users with no explanation, which is the exact G-04/G-23 symptom. The closing evidence lists only the G-04 role-action facts and the DeleteQuest pin, so no test covers these five entry points.
**Fix:** Use the same answer the role actions use:
```csharp
var targetRole = await userService.GetGroupRoleByIdAsync(userId, groupId.Value);
if (targetRole == null) return this.RedirectWithError(nameof(Users), AdminActionMessages.MemberStale);
...
var user = await userService.GetByIdAsync(userId);
if (user == null) return this.RedirectWithError(nameof(Users), AdminActionMessages.MemberStale);
```
Apply this to all five actions. Then add an `AdminRoleGuardTests`-style theory over EditUser GET/POST, ResetPassword GET/POST and SendConfirmationEmail that asserts the Error toast.

### WR-02: Create User by email still mails reach-refused accounts and gets around the per-recipient resend budget

**File:** `QuestBoard.Service/Controllers/Admin/AdminController.cs:149-182` (with `UserService.cs:256-285`)
**Issue:** G-07 refuses Resend Welcome Email for a SuperAdmin account or an Admin of another board. G-06 and G-08 keep every mail to one recipient within 3 per hour (`email-resend:{userId}`). Create User's existing-account branch checks neither rule:
- `AddedToGroupStrandedAccount` (the target's email is unconfirmed) generates a password-reset token and enqueues the same `WelcomeEmailJob` with a SetPassword link that `SendConfirmationEmail` sends. It does this for any account, including the reach-refused ones.
- `AddedToGroup` enqueues `GroupMembershipAddedEmailJob`.

A board Admin can repeat Create User (by email), then Remove from Group, on the same address. Each cycle mails the target. The only cap is the per-actor 20 per hour limit, and that budget belongs to the actor, not the recipient. So one inbox, including a platform administrator's, can receive about 20 mails an hour from each Admin, against the recipient budget of 3 that this phase set out to enforce. The link goes to the owner's own address, so this is not a takeover. It is the nuisance and relay-quota shape that G-06, G-07 and G-09 were meant to close.
**Fix:** In the two existing-account branches, take the per-target permit before enqueueing. Skip the mail (but keep the membership) when the account is outside reach or the permit is refused:
```csharp
case CreateOrAddToGroupOutcome.AddedToGroupStrandedAccount:
case CreateOrAddToGroupOutcome.AddedToGroup:
    var reachable = await IsWithinReachAsync(groupId.Value, result.UserId!.Value);
    using (var mailLease = reachable ? emailResendLimiter.AttemptAcquire(result.UserId.Value) : null)
    {
        if (reachable && mailLease!.IsAcquired) { /* enqueue as today */ }
        else { /* success toast without "an email has been sent" */ }
    }
```
An alternative is to refuse the add for reach-refused accounts outright, which closes G-02's elevated part at the add as well. That is an operator decision.

### WR-03: The role guard's write is an upsert, so a race with a removal re-adds the removed member, outside the membership chokepoint

**File:** `QuestBoard.Domain/Services/UserService.cs:175-190`, `QuestBoard.Repository/UserRepository.cs:105-120`
**Issue:** `ChangeMemberRoleAsync` first reads the role (`NotAMember` if there is no row) and later calls `repository.SetGroupRoleAsync`. `SetGroupRoleAsync` re-queries and, when no row is found, adds a new `UserGroupEntity`. Suppose Admin A presses Remove from Group (or the SuperAdmin removes the member on the Platform page) while Admin B presses Promote to Admin on the same row. The membership can be deleted between B's check and B's write. B's request then re-creates it with the new role, possibly Admin, and answers as if it were an ordinary change.

The re-add also bypasses `GroupService.AddMemberAsync`, which that file documents as "the single chokepoint every membership addition funnels through". So whatever that chokepoint does for a new member is skipped. O-7 accepted only the mutual-demotion race. A removed person silently regaining access, possibly as Admin, is a different and worse outcome. It is race-only, so this is a Warning, not a Blocker.
**Fix:** Give the guard an update-only write that cannot create a row, and map a missing row to `NotAMember`:
```csharp
// IUserRepository
Task<bool> UpdateGroupRoleAsync(int userId, int groupId, GroupRole role, CancellationToken token = default);

// UserRepository: tracked update only
var ug = await DbContext.UserGroups.FirstOrDefaultAsync(x => x.UserId == userId && x.GroupId == groupId, token);
if (ug == null) return false;
ug.GroupRole = (int)role;
await DbContext.SaveChangesAsync(token);
return true;

// UserService.ChangeMemberRoleAsync
return await repository.UpdateGroupRoleAsync(targetUserId, groupId, newRole, token)
    ? MemberChangeOutcome.Changed
    : MemberChangeOutcome.NotAMember;
```

### WR-04: Signups are deleted in separate saves before the quest's own save, so a refused final save leaves them gone under "Nothing was changed" (accepted residual, no fix)

**File:** `QuestBoard.Domain/Services/QuestService.cs:110-125`, `QuestBoard.Service/Controllers/Admin/AdminController.cs:465-474`, `QuestBoard.Service/ViewModels/AdminViewModels/AdminActionMessages.cs:78`
**Issue:** `QuestService.RemoveAsync` removes each `PlayerSignup` through `playerSignupRepository.RemoveAsync`. That is `BaseRepository.RemoveAsync`, which calls `SaveChangesAsync` once per signup. Only after that does `QuestRepository.RemoveAsync` save the reminder rows, the follow-up unlink and the quest delete together. If the final save throws `DbUpdateException`, the controller shows `QuestRefused` ("That quest could not be deleted. Nothing was changed.") while the signups are already deleted.

Re-assessment against the Gate 2 record (`D100-questservice-removeasync`, d3):
- **Reachability: race-only, as recorded.** The final save has four possible blockers:
  - `ReminderLogs` (NoAction): now removed in the same save.
  - `Quests.OriginalQuestId` (ClientSetNull): now unlinked in the same save.
  - `PlayerSignups` (NoAction): already deleted.
  - `ProposedDates`: cascade.

  So nothing refuses the final save in normal use. The remaining triggers are concurrent writes in the gap: a player signing up (new `PlayerSignups` row, NoAction FK) or the reminder job inserting a `ReminderLogs` row. `QuestEntity.FeedRevision` is a concurrency token, but a concurrent title or date edit is handled by `FeedRevisionStamper.TryAdoptStoredRevision` (Deleted Quest entries are recoverable), so it does not trigger this path.
- **Scope: slightly wider than the wording, not worse in kind.** Each signup's `PlayerDateVotes` go with it by cascade, along with the signup's selection and waitlist state, so "signups gone" also means "date votes gone". No other board's data and no event data is touched.
- **Change-tracker rule and calendar feed: not broken.** Every delete is a tracked `Remove` with no bulk, raw SQL or `Attach`. The quest row is either deleted or unchanged, so its `FeedRevision` never goes backwards. A player's personal feed loses the entry only because their signup is really gone, which is a true change, and it gets a new ETag.
- **Related symptom from the same split.** Two Admins delete the same quest at once. The second request's per-signup `SaveChanges` affects 0 rows, raises `DbUpdateConcurrencyException`, and is caught as `DbUpdateException`. That Admin sees "could not be deleted. Nothing was changed." although the quest is gone. It is wrong wording, not data loss, and the same race class.

Verdict: **accepted residual, no fix.** The fixer must leave `QuestService.RemoveAsync` and the `DeleteQuest` catch as they are. If the residual is ever lifted, the shape of the fix is to delete the signups through the tracked graph already loaded by `GetQuestWithManageDetailsAsync` and save once inside `QuestRepository.RemoveAsync`. IN-01 is a comment-only correction and does not touch this behaviour.

## Info

### IN-01: The QuestRepository.RemoveAsync comment says the delete is all-or-nothing, but its only caller has already committed the signup deletes

**File:** `QuestBoard.Repository/QuestRepository.cs:37-41`
**Issue:** The comment says "Everything is a tracked change saved together with the quest, so the delete either happens whole or not at all." Within this method that is true. Across the delete it is false, because `QuestService.RemoveAsync` has already committed the signup and vote deletions (WR-04). A future reader will trust the comment and miss the residual.
**Fix:** Comment only. Reword it to: "...saved together with the quest in one save. The quest's signups are removed by the caller beforehand in their own saves, so a refusal here leaves them deleted."

### IN-02: The per-administrator Create User budget is also spent on adds that send nothing or fail

**File:** `QuestBoard.Service/Controllers/Admin/AdminController.cs:120-127`, `:184-194`
**Issue:** The permit is taken before `CreateOrAddToGroupAsync`. An `AlreadyMember` answer (no account created, no mail) and an Identity-refused `Failed` answer (the form re-renders) each still use one of the 20 permits. The comment "spent only by a request that passed validation" is literally true, but it hides that a non-creating outcome spends budget. The impact is low.
**Fix:** Take the permit after an `AlreadyMember` pre-check, or accept this and reword the comment to "spent by every request that passed form validation, whatever the outcome".

### IN-03: The unguarded IUserService.SetGroupRoleAsync stays on the public service interface

**File:** `QuestBoard.Domain/Interfaces/IUserService.cs:131`, `QuestBoard.Domain/Services/UserService.cs:167-170`
**Issue:** After this phase, nothing in the Service layer calls `SetGroupRoleAsync`. Its only caller is `CreateOrAddToGroupAsync` inside `UserService`. Leaving it public offers a one-line way around the self and last-Admin guards for the next controller that needs a role change.
**Fix:** Remove it from `IUserService` and call `repository.SetGroupRoleAsync` directly inside `CreateOrAddToGroupAsync`. Alternatively, mark it in its XML doc as an unguarded primitive for account creation only.

### IN-04: Success toasts print the raw enum name ("DungeonMaster") that the display mapping exists to replace

**File:** `QuestBoard.Service/Controllers/Admin/AdminController.cs:160`, `:181`; `QuestBoard.Service/Areas/Platform/Controllers/GroupController.cs:354`, `:377`, `:442`, `:464`
**Issue:** `$"... added to the group as {model.GroupRole}"` renders `DungeonMaster`, while every badge and select on the same pages now shows "Dungeon Master" through `AdminPlatformDisplay.GroupRoleName`. The same raw name is passed into `GroupMembershipAddedEmailJob` (`AdminController.cs:158`).
**Fix:** Use `AdminPlatformDisplay.GroupRoleName(model.GroupRole)` in the toasts. Change the email argument only if the email template expects display text.

### IN-05: A failed Create New User from the dialog drops Identity's reason without logging it

**File:** `QuestBoard.Service/Areas/Platform/Controllers/GroupController.cs:472-475`
**Issue:** The `Failed` branch now shows only the generic `CreateMemberFailed` toast, as approved at O-12. `result.Errors`, such as an invalid address or an Identity username clash, is discarded and not logged anywhere, so the operator cannot tell why a create keeps failing.
**Fix:** Keep the approved toast and add `logger.LogWarning("Create member failed for board {GroupId}: {Errors}", id, string.Join("; ", result.Errors));`. Do not log the email address.

### IN-06: The Program.cs comment still says Create User is exempt from limiting

**File:** `QuestBoard.Service/Program.cs:179`
**Issue:** "CreateUser's one-shot automated welcome email is explicitly exempt." Create User now has its own per-administrator limiter, registered a few lines below. The sentence is only true of the per-target limiter and reads as if Create User were unlimited.
**Fix:** Change it to: "CreateUser's welcome email is exempt from this per-target limiter; it has its own per-administrator limit below."

### IN-07: The Service layer catches EF Core's DbUpdateException directly

**File:** `QuestBoard.Service/Controllers/Admin/AdminController.cs:13`, `:465-472`
**Issue:** `DeleteQuest` depends on `Microsoft.EntityFrameworkCore.DbUpdateException`, which is a Repository-layer type, so the Service layer depends on persistence details. `GroupController` and `ContactCategoryManagementController` already do the same, so this follows existing precedent rather than adding a new kind of violation. No EF package was added to the Service project.
**Fix (optional):** Have `IQuestService` return an outcome (for example `Deleted`, `Missing`, `Refused`) and translate the database refusal in the Domain or Repository layer. Leave it if the precedent is accepted.

---

_Reviewed: 2026-11-19T09:19:50Z_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
