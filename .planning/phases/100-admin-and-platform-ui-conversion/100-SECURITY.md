---
phase: 100
slug: admin-and-platform-ui-conversion
status: draft
# threats_open = count of OPEN threats at or above workflow.security_block_on severity (the blocking gate)
threats_open: 0
asvs_level: 1
block_on: high
created: 2026-11-18
---

# Phase 100 — Security

> Per-phase security contract for the Admin and Platform conversion: the server-rules audit (gap table), the verdicts, the live proofs on p100 fixtures, the operator's answers and the threat register.
> Draft written by plan 100-04, before any fix. Plan 100-05 puts this document and its questions in front of the operator; the fix plans (100-06 to 100-10) implement the recorded answers. Nothing in `QuestBoard.Service`, `QuestBoard.Domain` or `QuestBoard.Repository` changed in the plan that wrote this draft.

## Audit gap table

Severity: High (account takeover or data loss reachable by an authenticated Admin), Medium (wrong result or unhandled error on a normal path), Low (UX or hygiene), Info (recorded, no change proposed). Scope follows D-04: reachable from one of the converted views, or not. Evidence names the source line, the reproduction fact (committed skipped until its fix; every one was run unskipped once and failed for the stated reason, output saved under `~/qb-ui-review/phase-100/audit/red/`) and the live proof on p100 fixtures (`audit/live-proofs.json`). Source lines were re-read in this plan and match the research. "Read" means source reading with no run.

| ID | Action | Current behaviour | Evidence | Proposed rule | Scope | Severity | Status |
|----|--------|-------------------|----------|---------------|-------|----------|--------|
| G-01 | `ResetPassword` GET and POST, `EditUser` GET and POST | A board Admin opens and completes a password reset for, and edits the name and address of, an Admin of another board and a SuperAdmin account. Only membership of the active board is checked; the service overload checks only that the actor exists | `AdminController.cs:287-343` (ResetPassword), `:198-285` (EditUser), `UserService.cs:107-110`, `IdentityService.cs:67-79`. Facts: `AdminAccountReachTests.ResetPassword_Post_ForAnAdminOfAnotherBoard_IsRefusedWithAnErrorToast_AndThePasswordIsUnchanged`, `ResetPassword_Post_ForAPlatformAdministrator_IsRefusedWithAnErrorToast_AndThePasswordIsUnchanged`, `ResetPassword_Get_ForAnAccountWithMoreReach_RedirectsWithAnErrorToast` (2 cases), `EditUser_Post_ForAnAccountWithMoreReach_IsRefused_AndNameAndEmailAreUnchanged` (2), `EditUser_Get_ForAnAccountWithMoreReach_RedirectsWithAnErrorToast` (2). Pins that stay green: `ResetPassword_Post_ForAMemberOfTheSameBoard_StillSucceeds`, `EditUser_Post_ForAMemberOfTheSameBoard_StillSavesTheName` (Player, Dungeon Master, peer Admin), `ResetPassword_Post_BySuperAdmin_StillSucceedsForAnAdminOfAnotherBoard`, `EditUser_Post_BySuperAdmin_StillSavesTheNameOfAnotherPlatformAdministrator`. Live proof `reset-other-board-admin`: GET answered 200, the POST succeeded and the target signed in with the new password | Refuse when the actor is not a SuperAdmin and the target is a SuperAdmin or holds Admin on a board other than the active one; enforce in the controller (GET and POST) and inside the service overload | In scope (D-02) | High | decided (D-02) |
| G-02 | `CreateUser` POST | An email that already belongs to an account adds that account to the active board with the posted role and ignores the posted name, which turns any account into a "member of this board" and is the only precondition G-01 needs | `AdminController.cs:116-195`, `UserService.cs:137`. Fact `AdminAccountReachTests.CreateUser_WithAPlatformAdministratorsAddress_ThenResetPassword_IsRefused` (a SuperAdmin's address added, then reset, password changed). Not run live: it would add a platform account to a board | D-02 closes it for SuperAdmin and foreign-Admin accounts. An ordinary account that also sits on another board can still be added then reset; see O-1 | In scope | High before D-02, Medium after | residual: the elevated-account part is closed by D-02; an ordinary account that also sits on another board can still be added by email and then edited or reset (O-1 accept). Operator: 'approve all recommendations' |
| G-03 | `PromoteToAdmin`, `DemoteFromAdmin`, `PromoteToDM`, `DemoteToPlayer`, `DeleteUser` | No self check and no last-Admin check: an Admin demotes or removes themselves; the last Admin of a board can be demoted or removed; the action name is not checked against the current role (`PromoteToDM` on an Admin demotes, `DemoteToPlayer` on an Admin drops two levels) | `AdminController.cs:58-106`, `:344-363`, `UserRepository.cs:105-120`, `GroupService.cs:40-41`. Facts: `AdminRoleGuardTests.DemoteFromAdmin_OnTheActorThemselves_IsRefused_AndTheirRoleIsUnchanged`, `DemoteToPlayer_OnTheActorThemselves_...`, `PromoteToDM_OnTheActorThemselves_...`, `DeleteUser_OnTheActorThemselves_IsRefused_AndTheirMembershipStays`, `RoleAction_OnTheOnlyAdminOfTheBoard_IsRefused_AndTheRoleIsUnchanged` (DemoteFromAdmin, DemoteToPlayer, crafted PromoteToDM), `DeleteUser_OnTheOnlyAdminOfTheBoard_IsRefused_AndTheMembershipStays`. Pins: `DemoteFromAdmin_ByAnotherAdmin_StillDemotesAnAdminWhoIsNotTheLastOne`, `PromoteToDM_ByAnAdmin_StillPromotesAPlayer`, `PromoteToAdmin_ByAnAdmin_StillPromotesADungeonMaster`, `DeleteUser_ByAnAdmin_StillRemovesAPlayerFromTheBoard`. Live proofs `last-admin-demotion` (the SuperAdmin demoted the only Admin of P100 Audit Campaign, the board had no Admin, restored) and `self-demotion` (p100admin demoted themselves and was locked out of Admin/Users until the SuperAdmin restored the role) | The guard judges the resulting role, not the action name: refuse when the target is the actor, or when the target is Admin, the new role is not Admin and the board has exactly one Admin; same for removal. Guarded method on `IGroupService` | In scope (D-03) | Medium | decided (D-03) |
| G-04 | Same five actions, board scope | A target outside the active board answers a silent redirect to Users with no toast (`DeleteUser` answers 404). Board scope itself holds | `AdminController.cs:63-64`, `:76-77`, `:89-90`, `:102-103`, `:353`; existing `AdminControllerIntegrationTests.RoleChangeActions_WhenTargetUserOutOfGroup_ShouldNotChangeMembership`. Fact `AdminRoleGuardTests.RoleAction_ForSomeoneWhoIsNotOnTheBoard_AnswersAnErrorToast_AndAddsNoMembership` (4 actions). Live proof `stale-role-action`: redirect to Users, no toast, nothing changed | Keep the scope check; answer a stale page with an Error toast instead of silence | In scope | Low | approved |
| G-05 | `DeleteQuest` | Board-scoped (see verdict). Defects on the path: the quest's reminder rows are not removed (`ReminderLogs → Quest` is NoAction) and a follow-up quest on another board keeps pointing at the deleted quest. On SQL Server the first throws on delete (read; the in-memory database has no foreign keys) | `QuestService.cs:110-125`, `QuestBoardContext.cs:267-279`, `QuestRepository.cs:96-115`. Facts: `AdminQuestDeleteTests.DeleteQuest_RemovesTheReminderRowsOfTheQuest`, `DeleteQuest_ClearsTheLinkOfAFollowUpQuestOnAnotherBoard`. Pins: `DeleteQuest_ForAQuestOfAnotherBoard_Answers404_AndDeletesNothing`, `DeleteQuest_ForAQuestOfTheActiveBoard_StillRemovesTheQuestAndItsSignups`. Live proof `deletequest-cross-board`: 404 and the quest still listed on its own board | In `QuestService.RemoveAsync`, remove the quest's reminder rows through the change tracker in the same save and clear an other-board follow-up's link to the original; the controller turns any remaining database error into an Error toast | In scope | Medium | approved |
| G-06 | `SendConfirmationEmail` | The hourly resend budget (3 per target account) is taken before the board-membership check, so an Admin can spend any account's budget, including accounts on other boards, and that account's own Admin is refused for an hour | `AdminController.cs:367-382`, `Program.cs:171-187`. Fact `AdminControllerRateLimitIntegrationTests.SendConfirmationEmail_ResendsRefusedAsOutOfBoard_DoNotSpendTheTargetsBudget` (three out-of-board resends, then the own-board Admin got 429) | Take the lease after the scope, reach and already-confirmed checks | In scope | Low | approved |
| G-07 | `SendConfirmationEmail` shape | Not the takeover shape (the link goes to the account's own stored address); a nuisance shape. The reach rule is cheap and consistent (the row's button is disabled with the same reason) | `AdminController.cs:395-413`. Fact `AdminAccountReachTests.SendConfirmationEmail_ForAnAccountWithMoreReach_IsRefused_AndNoMailIsQueued` (2 cases: a welcome mail was queued for a SuperAdmin account and for an other-board Admin account) | Apply the D-02 reach rule for consistency; the operator confirms | In scope | Low | approved |
| G-08 | `EditUser` POST with an email change | Name and key flag are saved before the limiter is consulted; a refused change answers a bare-text 429 page after part of the edit was written. `SendConfirmationEmail` answers the same bare 429 | `AdminController.cs:250`, `:256-261`, `:372-376`. Fact `AdminControllerRateLimitIntegrationTests.EditUser_RefusedEmailChange_LeavesTheNameUnchanged` (the refused fourth save changed the name). Existing facts expect 429 (`AdminControllerRateLimitIntegrationTests.cs:70-95`, `:135-185`) | Acquire before any write; answer with a redirect and an Error toast instead of a bare page (O-4: the 429 status contract in the tests changes) | In scope | Low | approved (follows O-4 toast) |
| G-09 | `CreateUser` POST | The one-shot welcome email is deliberately exempt from the resend limiter, so an Admin can create accounts for arbitrary external addresses without limit and spend the relay's daily quota | `Program.cs:171-177`; pin `AdminControllerRateLimitIntegrationTests.CreateUser_RapidRequests_ShouldNotBeRateLimited`. Read | Per-actor limiter (for example 20 per hour) or accept (O-5) | In scope | Low | approved (follows O-5 limit 20 per 60 minutes) |
| G-10 | `CreateUser` / Platform `AddMember` toasts | The toast prints the real name of an existing account for any address (collision-aware by design, documented in `IUserService.cs:124-135`) | `AdminController.cs:159`, `:180`. Read | Accept | In scope | Info | residual: accepted as designed, the toast names the real owner of an existing address. Operator: 'approve all recommendations' |
| G-11 | `EditUser` | Name, address and key flag are global account fields edited from a board page; a board Admin changes how a plain member appears on every board. D-02 permits this for non-elevated accounts | `AdminController.cs:225-285`. Read | Record as residual | In scope | Info | residual: accepted, D-02 already limits it to non-elevated accounts. Operator: 'approve all recommendations' |
| G-12 | Reset and email change | No notification reaches the account owner on an admin password reset, and the old address is not told of an admin-initiated email change | `AdminController.cs:263-277`, `:328-332`. Read | A new email template is a new capability for the email phase: deferred (O-8). See deferred-items | Deferred | Low | residual: deferred to the email phase (O-8 defer); not fixed. Operator: 'approve all recommendations' |
| G-13 | Platform `RemoveMember` | No check that the board or the membership exists; the page says "Member removed from the group." even when nothing was removed | `GroupController.cs:501-508`, `GroupRepository.cs:114-118`. Fact `GroupManagementIntegrationTests.RemoveMember_ForSomeoneWhoIsNotAMember_DoesNotAnswerSuccess`. Live proof `platform-remove-non-member`: the Success toast was shown for a non-member of P100 Audit Lone and nothing changed | Success only if a row went; otherwise a Warning that the person was not a member | In scope | Low | approved |
| G-14 | Platform `RemoveMember` and the last Admin | A SuperAdmin may remove every member including the last Admin, which a board needs before it can be deleted (it requires zero members) | `GroupController.cs:216-221`, `:501-508`. Pin `GroupManagementIntegrationTests.RemoveMember_StillRemovesTheLastAdminOfABoard` | Exempt from the last-Admin rule (the guard lives on the Admin-page methods); confirm at O-2 | In scope | Info | approved (follows O-2 exempt) |
| G-15 | Platform `CreateMember` (dialog) | On an invalid model or a failed create the page re-renders with the validation summary inside the closed dialog, so the refusal is invisible | `GroupController.cs:400-412`, `:478-497`. Fact `GroupManagementIntegrationTests.CreateMember_WhenItFails_IsVisibleToTheViewer` (accepts either an Error toast on a redirect or a re-rendered page whose `#createMemberModal` carries a marker to open on load; the answer to O-12 picks one and the fix plan fixes the assertion) | Redirect with an Error toast, like Add Member (O-12) | In scope | Low | approved (follows O-12 toast) |
| G-16 | Platform `Disable` / `Enable` | Self-disable is refused; a peer SuperAdmin may be disabled by design; two SuperAdmins can lock each other out and only SQL recovers | `UsersController.cs:33-60`; `UsersControllerIntegrationTests` (peer allowed). Read | Accept; recorded | In scope | Info | residual: accepted as design. Operator: 'approve all recommendations' |
| G-17 | `EmailStats?force=true` | A GET that clears the five-minute cache and calls the mail provider; SuperAdmin only, no data written | `AdminController.cs:454-478`. Read | Stay a GET or become a POST (O-6) | In scope | Info | residual: stays a GET (O-6 get), accepted as is. Operator: 'approve all recommendations' |
| G-18 | Remove from Group confirm wording | The Admin page sentence says the person keeps "any other group memberships" but the removal also deletes their availability answers on this board; the Platform sentence says so | `Users.cshtml:165`, `Users.Mobile.cshtml:120`, `GroupRepository.cs:121-127`, `Members.cshtml:77`. Read | Gate 1 wording item: bring the Admin sentence in line (O-10) | In scope | Low | approved (Gate 1 item 8; O-10 gate1) |
| G-19 | ResetPassword form hint | The view model accepts 6 characters, Identity requires 8 with an upper-case letter, a lower-case letter and a digit, so 6 or 7 characters pass the form and fail on the server with Identity's message | `ResetPasswordViewModel.cs:12`, `Program.cs:50-55`. Read this plan | Align the attribute to the policy; Gate 1 copy item (O-10) | In scope | Low | approved (Gate 1 item 9; O-10 gate1) |
| G-20 | Undefined enum values (`GroupRole`, `BoardType`) | Refuted by the research probe: MVC rejects `GroupRole=7` and `-1`, so a crafted role never reaches the service | Research probe | None | — | Info | no gap |
| G-21 | Linked boards (5 actions) | Name, type, duplicate, mismatch, already linked, not in set and concurrency are all validated in the service with explicit outcomes and toasts; writes are tracked | `LinkedBoardSetService.cs`, `LinkedBoardSetRepository.cs`. Read | None | — | Info | no gap |
| G-22 | Anti-forgery | Every POST and DELETE in the four controllers carries the token check. The coverage sweep reflects over `HttpPostAttribute` only and the test factory validates nothing, so the two `[HttpDelete]` actions are guarded only by hand. Both live DELETE proofs sent the page's token | `AdminController.cs:344`, `:436`; `AntiForgeryTokenCoverageTests.cs:29-33`. Read | The POST change brings both under the sweep; keep it green | In scope | Info | no gap |
| G-23 | Crafted-request responses | An out-of-board id answers a silent redirect (role actions, EditUser, ResetPassword, SendConfirmationEmail) or 404 (`DeleteUser`, `DeleteQuest`); a missing token answers the framework's 400 | See G-04; live proofs `stale-role-action`, `deletequest-cross-board` | Normalise to a redirect with an Error toast on the Admin controller (G-04) | In scope | Low | approved |

### Findings added by this audit

Found while reproducing the gaps; they are reachable from converted views and are put to the operator as O-14 to O-16.

| ID | Action | Current behaviour | Evidence | Proposed rule | Scope | Severity | Status |
|----|--------|-------------------|----------|---------------|-------|----------|--------|
| X-1 | Users page "Demote to DM" (`DemoteFromAdmin`) | The button is labelled "Demote to DM" and the comment says one level down, but the action sets the role to Player. The Users page then shows the person as Player | `Users.cshtml:103-111`, `Users.Mobile.cshtml:77-81`, `AdminController.cs:71-80` (sets `GroupRole.Player`). Live proof `last-admin-demotion`: the role after the button was Player. Read and live; no fact written because the answer (change the action or the label) is the operator's | Make the action match the label (demote to Dungeon Master) or relabel it; recommended: match the action to the label | In scope | Medium | approved (O-14 dm) |
| X-2 | `ResetPassword` POST, success message | The success message is written under the temp-data key `SuccessMessage`, which no layout renders, so a successful reset shows no toast at all. The live proof recorded no toast on the Users page after a successful reset | `AdminController.cs:332`, `_Toasts.cshtml` (renders Success, Error, Warning, Info). Live proof `reset-other-board-admin` (`response.toasts` empty after success) | Use the Success key with the existing wording ("Password reset successfully for {name}!") | In scope | Low | approved (O-15 yes) |
| X-3 | Platform Members "Create New User" dialog | The dialog posts its fields as `CreateMember.Email`, `CreateMember.Name` and `CreateMember.GroupRole`, but the action binds an unprefixed model, so a form submitted as built always arrives empty, fails validation and re-renders with the dialog closed and no message. The existing tests post unprefixed names and never see it (found while creating the p100 fixtures; see deferred-items) | `Members.cshtml` (dialog inputs), `GroupController.cs:395-412`. Fact `GroupManagementIntegrationTests.CreateMember_PostedWithTheFieldNamesTheDialogUses_CreatesTheAccount` | Bind the model with the dialog's prefix (or post unprefixed names) and give the failure path a visible refusal together with G-15 | In scope | Medium | approved (O-16 yes) |

## DeleteQuest verdict

- `DeleteQuest` is limited to the active board: the controller finds the quest through the repository's `FindAsync`, and the quest entity has a fail-closed query filter on the active board, so another board's id returns nothing.
- Even if a row were found, `QuestService.RemoveAsync` re-reads the quest through a filtered query and returns without deleting when it gets nothing, so the delete cannot cross boards.
- Pinned, not fixed: `AdminQuestDeleteTests.DeleteQuest_ForAQuestOfAnotherBoard_Answers404_AndDeletesNothing` passes today (404; the quest, its signups and its reminder rows survive), and live proof `deletequest-cross-board` agrees (404 from `p100admin` with P100 Audit One-Shot active for a P100 Audit Campaign quest; the quest is still listed on its own board).
- No board-scoping rule is needed. What the delete needs is the cleanup in G-05, and a toast answer after it becomes a POST.
- The write stays on the change tracker (`Remove` then `SaveChanges`); the calendar feed revision guard is not at risk from a delete.

## Live proofs

All proofs ran on the working tree on port 8340 against p100 fixtures only, through the app's own forms or a crafted request carrying the page's own token. No real account, board, membership or set was touched, and the reused SuperAdmin and reused admin were never reset or edited. `fixtures.mjs --check` passed under both User-Agents after each state-changing proof.

| Proof id | Gap | What it showed | Restored |
|----------|-----|----------------|----------|
| `reset-other-board-admin` | G-01, X-2 | `p100admin` (Admin of P100 Audit One-Shot only) opened Reset Password for `p100otheradmin` (Admin of P100 Audit Campaign): GET 200; the POST succeeded, `p100otheradmin` signed in with the new password (stored in `credentials.json`, never printed), and the Users page showed no toast | A password cannot be restored, only replaced; the fixture account keeps working and its sessions were re-created on both ports |
| `last-admin-demotion` | G-03, X-1 | The SuperAdmin, with P100 Audit Campaign open, pressed "Demote to DM" for `p100otheradmin`, the only Admin: it succeeded with no toast, the role became Player (X-1) and the board had no Admin | Yes: Promote to DM then Promote to Admin through the page; the board's Admin is back |
| `self-demotion` | G-03 | `p100admin` pressed "Demote to DM" on their own row on P100 Audit One-Shot: it succeeded, the next Admin/Users request answered Access Denied, and the board had no Admin | Yes: the SuperAdmin restored the Admin role |
| `stale-role-action` | G-04, G-23 | `p100admin` posted Promote to Admin for an id no account holds: a redirect to Users, no toast, no change | Nothing changed |
| `platform-remove-non-member` | G-13 | The SuperAdmin posted RemoveMember on P100 Audit Lone for `p100player`, who is not a member of it: the Success toast "Member removed from the group." was shown and nothing changed (P100 Audit Lone still empty, `p100player` still on P100 Audit One-Shot) | Nothing changed |
| `deletequest-cross-board` | G-05 | `p100admin` with P100 Audit One-Shot open sent DELETE for a P100 Audit Campaign quest id with the page token: 404, and the quest is still listed on P100 Audit Campaign | Nothing changed |

Request, response and the state before and after of each proof are in `~/qb-ui-review/phase-100/audit/live-proofs.json`.

## Reproduction facts

Each was run unskipped once and failed on the first assertion that states the missing rule; output is saved under `~/qb-ui-review/phase-100/audit/red/<fact name>.txt` (theory cases carry the case in the file name). Each is committed with `Skip = "Reproduces an open gap; enabled by its fix"`. 21 skipped entries (theories count once) stand for 30 cases.

| Fact (class `AdminAccountReachTests` unless noted) | Gap | Saved failing run (file under `audit/red/`) |
|------|-----|-------------------|
| `ResetPassword_Post_ForAnAdminOfAnotherBoard_IsRefusedWithAnErrorToast_AndThePasswordIsUnchanged` | G-01 | `reset-other-board-admin.txt` (same run as the first file below) |
| `ResetPassword_Post_ForAPlatformAdministrator_IsRefusedWithAnErrorToast_AndThePasswordIsUnchanged` | G-01 | `ResetPassword_Post_ForAPlatformAdministrator_...txt` |
| `ResetPassword_Get_ForAnAccountWithMoreReach_RedirectsWithAnErrorToast` (2) | G-01 | `ResetPassword_Get_ForAnAccountWithMoreReach_..._platformAdministrator_True/False.txt` |
| `EditUser_Post_ForAnAccountWithMoreReach_IsRefused_AndNameAndEmailAreUnchanged` (2) | G-01 | `EditUser_Post_ForAnAccountWithMoreReach_..._platformAdministrator_True/False.txt` |
| `EditUser_Get_ForAnAccountWithMoreReach_RedirectsWithAnErrorToast` (2) | G-01 | `EditUser_Get_ForAnAccountWithMoreReach_..._platformAdministrator_True/False.txt` |
| `SendConfirmationEmail_ForAnAccountWithMoreReach_IsRefused_AndNoMailIsQueued` (2) | G-07 | `SendConfirmationEmail_ForAnAccountWithMoreReach_..._platformAdministrator_True/False.txt` |
| `CreateUser_WithAPlatformAdministratorsAddress_ThenResetPassword_IsRefused` | G-02 | `CreateUser_WithAPlatformAdministratorsAddress_ThenResetPassword_IsRefused.txt` |
| `AdminRoleGuardTests.DemoteFromAdmin_OnTheActorThemselves_IsRefused_AndTheirRoleIsUnchanged` | G-03 | `DemoteFromAdmin_OnTheActorThemselves_....txt` |
| `AdminRoleGuardTests.DemoteToPlayer_OnTheActorThemselves_IsRefused_AndTheirRoleIsUnchanged` | G-03 | `DemoteToPlayer_OnTheActorThemselves_....txt` |
| `AdminRoleGuardTests.PromoteToDM_OnTheActorThemselves_IsRefused_AndTheirRoleIsUnchanged` | G-03 | `PromoteToDM_OnTheActorThemselves_....txt` |
| `AdminRoleGuardTests.DeleteUser_OnTheActorThemselves_IsRefused_AndTheirMembershipStays` | G-03 | `DeleteUser_OnTheActorThemselves_....txt` |
| `AdminRoleGuardTests.RoleAction_OnTheOnlyAdminOfTheBoard_IsRefused_AndTheRoleIsUnchanged` (3) | G-03 | `RoleAction_OnTheOnlyAdminOfTheBoard_..._action_<name>.txt` |
| `AdminRoleGuardTests.DeleteUser_OnTheOnlyAdminOfTheBoard_IsRefused_AndTheMembershipStays` | G-03 | `DeleteUser_OnTheOnlyAdminOfTheBoard_....txt` |
| `AdminRoleGuardTests.RoleAction_ForSomeoneWhoIsNotOnTheBoard_AnswersAnErrorToast_AndAddsNoMembership` (4) | G-04, G-23 | `RoleAction_ForSomeoneWhoIsNotOnTheBoard_..._action_<name>.txt` |
| `AdminQuestDeleteTests.DeleteQuest_RemovesTheReminderRowsOfTheQuest` | G-05 | `DeleteQuest_RemovesTheReminderRowsOfTheQuest.txt` |
| `AdminQuestDeleteTests.DeleteQuest_ClearsTheLinkOfAFollowUpQuestOnAnotherBoard` | G-05 | `DeleteQuest_ClearsTheLinkOfAFollowUpQuestOnAnotherBoard.txt` |
| `AdminControllerRateLimitIntegrationTests.SendConfirmationEmail_ResendsRefusedAsOutOfBoard_DoNotSpendTheTargetsBudget` | G-06 | `SendConfirmationEmail_ResendsRefusedAsOutOfBoard_DoNotSpendTheTargetsBudget.txt` |
| `AdminControllerRateLimitIntegrationTests.EditUser_RefusedEmailChange_LeavesTheNameUnchanged` | G-08 | `EditUser_RefusedEmailChange_LeavesTheNameUnchanged.txt` |
| `GroupManagementIntegrationTests.RemoveMember_ForSomeoneWhoIsNotAMember_DoesNotAnswerSuccess` | G-13 | `RemoveMember_ForSomeoneWhoIsNotAMember_DoesNotAnswerSuccess.txt` |
| `GroupManagementIntegrationTests.CreateMember_WhenItFails_IsVisibleToTheViewer` | G-15 | `CreateMember_WhenItFails_IsVisibleToTheViewer.txt` |
| `GroupManagementIntegrationTests.CreateMember_PostedWithTheFieldNamesTheDialogUses_CreatesTheAccount` | X-3 | `CreateMember_PostedWithTheFieldNamesTheDialogUses_CreatesTheAccount.txt` |

Notes the fix plans need: the two `DeleteUser` facts and the two quest-delete pins use the DELETE verb because that is what the endpoints answer today; the fix that turns them into POST requests rewrites those assertions to a redirect and an Error or Success toast. The fixed-limit facts live in their own fixture because the app keeps the hourly mail budget per account id for the life of the process; the other reach facts use distinct id offsets for the same reason.

## Questions for the operator

D-02 and D-03 are decided and are not re-asked. The recommendation is the research's; the operator's answer goes in the next section.

| # | Question | Recommendation |
|---|----------|----------------|
| O-1 | G-02: an ordinary account (Player or Dungeon Master) on another board can still be added by email and then reset by this board's Admin. Accept as D-02 stands, or also refuse edit and reset for any account that holds any role on another board? | Accept as decided; the add emails the owner. Record the residual |
| O-2 | G-14: the last-Admin guard applies to the Admin page only, not to the SuperAdmin's Platform Members removal (needed to empty a board before deleting it). Confirm | Exempt |
| O-3 | Rule reading A1: a peer Admin of the same board is reachable; the actor themself is reachable for name and email; a self ResetPassword is allowed or sent to the account page (it skips the current-password check) | Peer and self reachable; self reset allowed |
| O-4 | G-08: replace the bare 429 page with an Error toast and move the limiter check before any write (the 429 status contract in the tests changes) | Yes |
| O-5 | G-09: add a per-actor limit on `CreateUser`, or accept | Add a generous limit (for example 20 per hour per actor) |
| O-6 | G-17: `EmailStats` Refresh stays a GET link, or becomes a POST | Stay GET (SuperAdmin only, idempotent) |
| O-7 | Accept the simultaneous mutual-demotion race (two Admins demoting each other at the same moment) | Accept |
| O-8 | G-12: owner notification on an admin reset and an admin email change is a new email, deferred to the email phase | Defer |
| O-9 | Wording of the D-02 refusal reason: generic ("only a platform administrator can change this account") or naming why? Naming discloses another board's role | Generic |
| O-10 | G-18 and G-19 copy: add the availability-answers sentence to the Admin Remove from Group confirm; align the reset-password minimum to 8 | Yes to both, as Gate 1 items |
| O-11 | G-05: approve deleting reminder rows and unlinking an other-board follow-up as part of quest delete | Yes |
| O-12 | G-15: reopen the dialog on errors, or redirect with a toast | Toast redirect (same as Add Member) |
| O-13 | The EmailStats test seam: a `ResendBaseUrl` setting (recommended) or an interface with a Development stub | Setting |
| O-14 | X-1: the "Demote to DM" button sets Player. Change the action to demote one level to Dungeon Master, or relabel the button "Demote to Player" | Match the action to the label (demote to Dungeon Master) |
| O-15 | X-2: show the existing "Password reset successfully for {name}!" as a Success toast after a reset | Yes |
| O-16 | X-3: fix the Create New User dialog binding together with G-15 (bind the dialog's prefix, redirect with a toast on failure) | Yes, in the plan that owns the Members page |

## Operator answers

Recorded verbatim in `audit-answers.json` (source `operator`; the answer was given in chat on 2026-11-18). Source of every answer below: operator in chat: "approve all recommendations", shown against the brief `~/qb-ui-review/phase-100/audit/checkpoint.html`, whose default resume line is every recommendation. Nothing was defaulted by the executor. The line the operator's phrase stands for:

> G-02 residual, G-04 approve, G-05 approve, G-06 approve, G-07 approve, G-08 approve, G-09 approve, G-10 residual, G-11 residual, G-12 residual, G-13 approve, G-14 approve, G-15 approve, G-16 residual, G-17 residual, G-18 approve, G-19 approve, G-23 approve; O-1 accept, O-2 exempt, O-3 peerAdmin reachable, selfEdit reachable, selfReset allowed, O-4 toast, O-5 limit 20 per 60 minutes, O-6 get, O-7 accept, O-8 defer, O-9 generic, O-10 removeSentence gate1, passwordHint gate1, O-11 yes, O-12 toast, O-13 dev-setting, O-14 dm, O-15 yes, O-16 yes; sentences as proposed

G-01 and G-03 were decided before the checkpoint (D-02, D-03) and were not re-asked; G-20 to G-22 are not gaps. Gate 1 item 6c (the role word, Admin or Administrator) was not answered at this checkpoint, so `wordingEarly.adminWord` is null, every sentence below stays role-free and item 6c stays a Gate 1 question.

| Question | Answer | Consequence |
|----------|--------|-------------|
| O-1 (add-by-email residual) | accept | The reach rule is exactly D-02; the ordinary-account remainder is the G-02 residual |
| O-2 (Platform removal and the last Admin) | exempt | The last-Admin guard lives on the Admin page only; the Platform removal stays unguarded and its pin stays |
| O-3 (reading of D-02) | peerAdmin reachable, selfEdit reachable, selfReset allowed | A peer Admin of the same board and the actor's own name and email are reachable; the actor may reset their own password through the Admin page |
| O-4 (rate-limit refusal) | toast | Limiter before any write; a refusal is a redirect and an Error toast (`toastRateLimit`); the existing 429 tests change; Gate 1 item 14 is answered |
| O-5 (Create User limit) | limit 20 per 60 minutes | A per-actor limiter on Create User with its own registration; `toastCreateLimit` applies; the unlimited-rapid-requests pin changes to allow 20 |
| O-6 (Email Stats Refresh verb) | get | No change; G-17 residual |
| O-7 (simultaneous mutual demotion) | accept | Recorded as a residual race; the self rule blocks the single-actor case and a SuperAdmin can restore an Admin from the Platform page |
| O-8 (owner notification) | defer | G-12 stays in deferred-items.md for the email phase |
| O-9 (reach wording) | generic | `reasonReach` and `toastReach` as proposed |
| O-10 (Remove from Group sentence, password hint) | removeSentence gate1, passwordHint gate1 | Gate 1 items 8 and 9 are asked there |
| O-11 (quest delete cleanup) | yes | Reminder rows and an other-board follow-up link are cleaned in the same save |
| O-12 (failed Create New User) | toast | Redirect with an Error toast like Add Member (`toastCreateMemberFailed`); Gate 1 item 10 is answered |
| O-13 (EmailStats capture seam) | dev-setting | `EmailSettings:ResendBaseUrl` honoured only when the host environment is Development, pinned by a fact |
| O-14 (X-1 Demote to DM) | dm | `DemoteFromAdmin` sets Dungeon Master |
| O-15 (X-2 reset success toast) | yes | The existing "Password reset successfully for {name}!" as a Success toast; no new sentence |
| O-16 (X-3 dialog binding) | yes | The Create New User dialog binding is fixed together with G-15 in the plan that owns the Members page |

### Approved sentences

All 15 approved verbatim ("sentences as proposed"); `{name}` is replaced by the person's name.

| Key | Text | Source |
|-----|------|--------|
| reasonSelf | This is your own account, so you cannot change your role or remove yourself here. | approved |
| reasonLastAdmin | This person is the only one who can manage this board. Give someone else that role first. | approved |
| reasonReach | Only a SuperAdmin can change this account. | approved |
| toastSelf | You cannot change your own role or remove yourself from the board. | approved |
| toastLastAdmin | A board must keep at least one member who can manage it. Give someone else that role first. | approved |
| toastReach | Only a SuperAdmin can change that account. | approved |
| toastStale | That person is not a member of this board any more. | approved |
| toastRemoved | {name} was removed from the group. | approved |
| toastQuestDeleted | Quest deleted. | approved |
| toastQuestMissing | That quest no longer exists on this board. | approved |
| toastQuestRefused | That quest could not be deleted. Nothing was changed. | approved |
| toastRateLimit | Too many emails were sent to this account recently. Try again in an hour. | approved (O-4 toast) |
| toastCreateLimit | You have created many accounts in a short time. Try again later. | approved (O-5 limit) |
| toastCreateMemberFailed | The account could not be created. Check the details and try again. | approved (O-12 toast) |
| toastPlatformNonMember | {name} was not a member of this group. | approved (G-13) |

### Which plan implements each rule

| Rule | Plan |
|------|------|
| G-01, G-02 (elevated part), G-06, G-07, G-08, G-09 | 100-06 and 100-07 (reach rule in 100-06; limiter order, refusal toast and Create User limit in 100-07) |
| X-2 (reset success toast) | 100-06 (the ResetPassword POST it rewrites); not named in that plan's text, see the SUMMARY |
| G-03 (role changes) and X-1 | 100-09 |
| G-03 (removal), G-04, G-13, G-14, G-23 | 100-10 |
| G-05 | 100-08 |
| G-15 and X-3 | 100-19 |
| G-17 | 100-20 (residual: stays a GET, no change) |
| G-18 and G-19 | Gate 1 and 100-14, 100-15 |
| G-10, G-11, G-12, G-16 | residual or info, not fixed; G-12 deferred to the email phase |

## Writes added by this phase

Each fix plan that adds a database write registers it here with method, entity, rows touched and why it stays on the change tracker; the cleanup plan checks the phase diff against exactly this list. No write was added by the audit plan.

| Plan | Method | Entity | Rows touched | Why it is tracked |
|------|--------|--------|--------------|-------------------|
| 100-08 | `QuestRepository.RemoveAsync` (override; removes the quest's reminder rows) | `ReminderLogEntity` (`ReminderLogs`) | every row whose `QuestId` is the quest being deleted; read with `IgnoreQueryFilters` selected by that id alone | `RemoveRange` on tracked entities, saved in the same `SaveChangesAsync` as the quest. Approved by the operator at O-11 (yes). No raw SQL, `ExecuteDelete`, `Attach` or hand-set state. |
| 100-08 | `QuestRepository.RemoveAsync` (override; unlinks a follow-up quest) | `QuestEntity` (`Quests`), column `OriginalQuestId` | each quest whose `OriginalQuestId` is the quest being deleted, usually on another board; read with `IgnoreQueryFilters` selected by that id alone | tracked property set to null, saved in the same `SaveChangesAsync` as the quest delete, so it goes through the change tracker and the feed revision guard. `OriginalQuestId` is not a field the calendar feed revision watches. Approved by the operator at O-11 (yes). |

## Threat register

(Empty: filled by the security audit.)
