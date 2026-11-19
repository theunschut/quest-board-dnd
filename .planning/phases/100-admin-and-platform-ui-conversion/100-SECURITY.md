---
phase: 100
slug: admin-and-platform-ui-conversion
status: verified
# threats_open = count of OPEN threats at or above workflow.security_block_on severity (the blocking gate)
threats_open: 0
asvs_level: 1
block_on: high
created: 2026-11-18
---

# Phase 100 — Security

> Per-phase security contract for the Admin and Platform conversion: the server-rules audit (gap table), the verdicts, the live proofs on p100 fixtures, the operator's answers and the threat register.
> Written by plan 100-04 before any fix, put to the operator by 100-05, implemented by 100-06 to 100-10 and 100-19, and closed by plan 100-22: every gap now carries its closing evidence (commit, enabled fact, live proof) or the operator's residual words in "Closing evidence" below. Status `verified`: the evidence is complete, and the phase security audit of 2026-11-19 filled the threat register below. One low threat (T-100-81) stays open below the block_on threshold, so `threats_open` is 0.

## Audit gap table

Severity: High (account takeover or data loss reachable by an authenticated Admin), Medium (wrong result or unhandled error on a normal path), Low (UX or hygiene), Info (recorded, no change proposed). Scope follows D-04: reachable from one of the converted views, or not. Evidence names the source line, the reproduction fact (committed skipped until its fix; every one was run unskipped once and failed for the stated reason, output saved under `~/qb-ui-review/phase-100/audit/red/`) and the live proof on p100 fixtures (`audit/live-proofs.json`). Source lines were re-read in this plan and match the research. "Read" means source reading with no run.

| ID | Action | Current behaviour | Evidence | Proposed rule | Scope | Severity | Status |
|----|--------|-------------------|----------|---------------|-------|----------|--------|
| G-01 | `ResetPassword` GET and POST, `EditUser` GET and POST | A board Admin opens and completes a password reset for, and edits the name and address of, an Admin of another board and a SuperAdmin account. Only membership of the active board is checked; the service overload checks only that the actor exists | `AdminController.cs:287-343` (ResetPassword), `:198-285` (EditUser), `UserService.cs:107-110`, `IdentityService.cs:67-79`. Facts: `AdminAccountReachTests.ResetPassword_Post_ForAnAdminOfAnotherBoard_IsRefusedWithAnErrorToast_AndThePasswordIsUnchanged`, `ResetPassword_Post_ForAPlatformAdministrator_IsRefusedWithAnErrorToast_AndThePasswordIsUnchanged`, `ResetPassword_Get_ForAnAccountWithMoreReach_RedirectsWithAnErrorToast` (2 cases), `EditUser_Post_ForAnAccountWithMoreReach_IsRefused_AndNameAndEmailAreUnchanged` (2), `EditUser_Get_ForAnAccountWithMoreReach_RedirectsWithAnErrorToast` (2). Pins that stay green: `ResetPassword_Post_ForAMemberOfTheSameBoard_StillSucceeds`, `EditUser_Post_ForAMemberOfTheSameBoard_StillSavesTheName` (Player, Dungeon Master, peer Admin), `ResetPassword_Post_BySuperAdmin_StillSucceedsForAnAdminOfAnotherBoard`, `EditUser_Post_BySuperAdmin_StillSavesTheNameOfAnotherPlatformAdministrator`. Live proof `reset-other-board-admin`: GET answered 200, the POST succeeded and the target signed in with the new password | Refuse when the actor is not a SuperAdmin and the target is a SuperAdmin or holds Admin on a board other than the active one; enforce in the controller (GET and POST) and inside the service overload | In scope (D-02) | High | closed (see Closing evidence) |
| G-02 | `CreateUser` POST | An email that already belongs to an account adds that account to the active board with the posted role and ignores the posted name, which turns any account into a "member of this board" and is the only precondition G-01 needs | `AdminController.cs:116-195`, `UserService.cs:137`. Fact `AdminAccountReachTests.CreateUser_WithAPlatformAdministratorsAddress_ThenResetPassword_IsRefused` (a SuperAdmin's address added, then reset, password changed). Not run live: it would add a platform account to a board | D-02 closes it for SuperAdmin and foreign-Admin accounts. An ordinary account that also sits on another board can still be added then reset; see O-1 | In scope | High before D-02, Medium after | residual: the elevated-account part is closed by D-02; an ordinary account that also sits on another board can still be added by email and then edited or reset (O-1 accept). Operator: 'approve all recommendations'; the elevated-account part is closed (see Closing evidence). Residual (WR-02 remainder): an account the reach rule refuses is still mailed when it is added by email, within the per-recipient budget of 3 per hour (see Closing evidence) |
| G-03 | `PromoteToAdmin`, `DemoteFromAdmin`, `PromoteToDM`, `DemoteToPlayer`, `DeleteUser` | No self check and no last-Admin check: an Admin demotes or removes themselves; the last Admin of a board can be demoted or removed; the action name is not checked against the current role (`PromoteToDM` on an Admin demotes, `DemoteToPlayer` on an Admin drops two levels) | `AdminController.cs:58-106`, `:344-363`, `UserRepository.cs:105-120`, `GroupService.cs:40-41`. Facts: `AdminRoleGuardTests.DemoteFromAdmin_OnTheActorThemselves_IsRefused_AndTheirRoleIsUnchanged`, `DemoteToPlayer_OnTheActorThemselves_...`, `PromoteToDM_OnTheActorThemselves_...`, `DeleteUser_OnTheActorThemselves_IsRefused_AndTheirMembershipStays`, `RoleAction_OnTheOnlyAdminOfTheBoard_IsRefused_AndTheRoleIsUnchanged` (DemoteFromAdmin, DemoteToPlayer, crafted PromoteToDM), `DeleteUser_OnTheOnlyAdminOfTheBoard_IsRefused_AndTheMembershipStays`. Pins: `DemoteFromAdmin_ByAnotherAdmin_StillDemotesAnAdminWhoIsNotTheLastOne`, `PromoteToDM_ByAnAdmin_StillPromotesAPlayer`, `PromoteToAdmin_ByAnAdmin_StillPromotesADungeonMaster`, `DeleteUser_ByAnAdmin_StillRemovesAPlayerFromTheBoard`. Live proofs `last-admin-demotion` (the SuperAdmin demoted the only Admin of P100 Audit Campaign, the board had no Admin, restored) and `self-demotion` (p100admin demoted themselves and was locked out of Admin/Users until the SuperAdmin restored the role) | The guard judges the resulting role, not the action name: refuse when the target is the actor, or when the target is Admin, the new role is not Admin and the board has exactly one Admin; same for removal. Guarded method on `IGroupService` | In scope (D-03) | Medium | closed (see Closing evidence) |
| G-04 | Same five actions, board scope | A target outside the active board answers a silent redirect to Users with no toast (`DeleteUser` answers 404). Board scope itself holds | `AdminController.cs:63-64`, `:76-77`, `:89-90`, `:102-103`, `:353`; existing `AdminControllerIntegrationTests.RoleChangeActions_WhenTargetUserOutOfGroup_ShouldNotChangeMembership`. Fact `AdminRoleGuardTests.RoleAction_ForSomeoneWhoIsNotOnTheBoard_AnswersAnErrorToast_AndAddsNoMembership` (4 actions). Live proof `stale-role-action`: redirect to Users, no toast, nothing changed | Keep the scope check; answer a stale page with an Error toast instead of silence | In scope | Low | closed (see Closing evidence) |
| G-05 | `DeleteQuest` | Board-scoped (see verdict). Defects on the path: the quest's reminder rows are not removed (`ReminderLogs → Quest` is NoAction) and a follow-up quest on another board keeps pointing at the deleted quest. On SQL Server the first throws on delete (read; the in-memory database has no foreign keys) | `QuestService.cs:110-125`, `QuestBoardContext.cs:267-279`, `QuestRepository.cs:96-115`. Facts: `AdminQuestDeleteTests.DeleteQuest_RemovesTheReminderRowsOfTheQuest`, `DeleteQuest_ClearsTheLinkOfAFollowUpQuestOnAnotherBoard`. Pins: `DeleteQuest_ForAQuestOfAnotherBoard_Answers404_AndDeletesNothing`, `DeleteQuest_ForAQuestOfTheActiveBoard_StillRemovesTheQuestAndItsSignups`. Live proof `deletequest-cross-board`: 404 and the quest still listed on its own board | In `QuestService.RemoveAsync`, remove the quest's reminder rows through the change tracker in the same save and clear an other-board follow-up's link to the original; the controller turns any remaining database error into an Error toast | In scope | Medium | closed (see Closing evidence) |
| G-06 | `SendConfirmationEmail` | The hourly resend budget (3 per target account) is taken before the board-membership check, so an Admin can spend any account's budget, including accounts on other boards, and that account's own Admin is refused for an hour | `AdminController.cs:367-382`, `Program.cs:171-187`. Fact `AdminControllerRateLimitIntegrationTests.SendConfirmationEmail_ResendsRefusedAsOutOfBoard_DoNotSpendTheTargetsBudget` (three out-of-board resends, then the own-board Admin got 429) | Take the lease after the scope, reach and already-confirmed checks | In scope | Low | closed (see Closing evidence) |
| G-07 | `SendConfirmationEmail` shape | Not the takeover shape (the link goes to the account's own stored address); a nuisance shape. The reach rule is cheap and consistent (the row's button is disabled with the same reason) | `AdminController.cs:395-413`. Fact `AdminAccountReachTests.SendConfirmationEmail_ForAnAccountWithMoreReach_IsRefused_AndNoMailIsQueued` (2 cases: a welcome mail was queued for a SuperAdmin account and for an other-board Admin account) | Apply the D-02 reach rule for consistency; the operator confirms | In scope | Low | closed (see Closing evidence) |
| G-08 | `EditUser` POST with an email change | Name and key flag are saved before the limiter is consulted; a refused change answers a bare-text 429 page after part of the edit was written. `SendConfirmationEmail` answers the same bare 429 | `AdminController.cs:250`, `:256-261`, `:372-376`. Fact `AdminControllerRateLimitIntegrationTests.EditUser_RefusedEmailChange_LeavesTheNameUnchanged` (the refused fourth save changed the name). Existing facts expect 429 (`AdminControllerRateLimitIntegrationTests.cs:70-95`, `:135-185`) | Acquire before any write; answer with a redirect and an Error toast instead of a bare page (O-4: the 429 status contract in the tests changes) | In scope | Low | closed (see Closing evidence) |
| G-09 | `CreateUser` POST | The one-shot welcome email is deliberately exempt from the resend limiter, so an Admin can create accounts for arbitrary external addresses without limit and spend the relay's daily quota | `Program.cs:171-177`; pin `AdminControllerRateLimitIntegrationTests.CreateUser_RapidRequests_ShouldNotBeRateLimited`. Read | Per-actor limiter (for example 20 per hour) or accept (O-5) | In scope | Low | closed (see Closing evidence) |
| G-10 | `CreateUser` / Platform `AddMember` toasts | The toast prints the real name of an existing account for any address (collision-aware by design, documented in `IUserService.cs:124-135`) | `AdminController.cs:159`, `:180`. Read | Accept | In scope | Info | residual: accepted as designed, the toast names the real owner of an existing address. Operator: 'approve all recommendations' |
| G-11 | `EditUser` | Name, address and key flag are global account fields edited from a board page; a board Admin changes how a plain member appears on every board. D-02 permits this for non-elevated accounts | `AdminController.cs:225-285`. Read | Record as residual | In scope | Info | residual: accepted, D-02 already limits it to non-elevated accounts. Operator: 'approve all recommendations' |
| G-12 | Reset and email change | No notification reaches the account owner on an admin password reset, and the old address is not told of an admin-initiated email change | `AdminController.cs:263-277`, `:328-332`. Read | A new email template is a new capability for the email phase: deferred (O-8). See deferred-items | Deferred | Low | residual: deferred to the email phase (O-8 defer); not fixed. Operator: 'approve all recommendations' |
| G-13 | Platform `RemoveMember` | No check that the board or the membership exists; the page says "Member removed from the group." even when nothing was removed | `GroupController.cs:501-508`, `GroupRepository.cs:114-118`. Fact `GroupManagementIntegrationTests.RemoveMember_ForSomeoneWhoIsNotAMember_DoesNotAnswerSuccess`. Live proof `platform-remove-non-member`: the Success toast was shown for a non-member of P100 Audit Lone and nothing changed | Success only if a row went; otherwise a Warning that the person was not a member | In scope | Low | closed (see Closing evidence) |
| G-14 | Platform `RemoveMember` and the last Admin | A SuperAdmin may remove every member including the last Admin, which a board needs before it can be deleted (it requires zero members) | `GroupController.cs:216-221`, `:501-508`. Pin `GroupManagementIntegrationTests.RemoveMember_StillRemovesTheLastAdminOfABoard` | Exempt from the last-Admin rule (the guard lives on the Admin-page methods); confirm at O-2 | In scope | Info | closed (see Closing evidence) |
| G-15 | Platform `CreateMember` (dialog) | On an invalid model or a failed create the page re-renders with the validation summary inside the closed dialog, so the refusal is invisible | `GroupController.cs:400-412`, `:478-497`. Fact `GroupManagementIntegrationTests.CreateMember_WhenItFails_IsVisibleToTheViewer` (accepts either an Error toast on a redirect or a re-rendered page whose `#createMemberModal` carries a marker to open on load; the answer to O-12 picks one and the fix plan fixes the assertion) | Redirect with an Error toast, like Add Member (O-12) | In scope | Low | closed (see Closing evidence) |
| G-16 | Platform `Disable` / `Enable` | Self-disable is refused; a peer SuperAdmin may be disabled by design; two SuperAdmins can lock each other out and only SQL recovers | `UsersController.cs:33-60`; `UsersControllerIntegrationTests` (peer allowed). Read | Accept; recorded | In scope | Info | residual: accepted as design. Operator: 'approve all recommendations' |
| G-17 | `EmailStats?force=true` | A GET that clears the five-minute cache and calls the mail provider; SuperAdmin only, no data written | `AdminController.cs:454-478`. Read | Stay a GET or become a POST (O-6) | In scope | Info | residual: stays a GET (O-6 get), accepted as is. Operator: 'approve all recommendations' |
| G-18 | Remove from Group confirm wording | The Admin page sentence says the person keeps "any other group memberships" but the removal also deletes their availability answers on this board; the Platform sentence says so | `Users.cshtml:165`, `Users.Mobile.cshtml:120`, `GroupRepository.cs:121-127`, `Members.cshtml:77`. Read | Gate 1 wording item: bring the Admin sentence in line (O-10) | In scope | Low | closed (see Closing evidence) |
| G-19 | ResetPassword form hint | The view model accepts 6 characters, Identity requires 8 with an upper-case letter, a lower-case letter and a digit, so 6 or 7 characters pass the form and fail on the server with Identity's message | `ResetPasswordViewModel.cs:12`, `Program.cs:50-55`. Read this plan | Align the attribute to the policy; Gate 1 copy item (O-10) | In scope | Low | closed (see Closing evidence) |
| G-20 | Undefined enum values (`GroupRole`, `BoardType`) | Refuted by the research probe: MVC rejects `GroupRole=7` and `-1`, so a crafted role never reaches the service | Research probe | None | — | Info | no gap |
| G-21 | Linked boards (5 actions) | Name, type, duplicate, mismatch, already linked, not in set and concurrency are all validated in the service with explicit outcomes and toasts; writes are tracked | `LinkedBoardSetService.cs`, `LinkedBoardSetRepository.cs`. Read | None | — | Info | no gap |
| G-22 | Anti-forgery | Every POST and DELETE in the four controllers carries the token check. The coverage sweep reflects over `HttpPostAttribute` only and the test factory validates nothing, so the two `[HttpDelete]` actions are guarded only by hand. Both live DELETE proofs sent the page's token | `AdminController.cs:344`, `:436`; `AntiForgeryTokenCoverageTests.cs:29-33`. Read | The POST change brings both under the sweep; keep it green | In scope | Info | no gap |
| G-23 | Crafted-request responses | An out-of-board id answers a silent redirect (role actions, EditUser, ResetPassword, SendConfirmationEmail) or 404 (`DeleteUser`, `DeleteQuest`); a missing token answers the framework's 400 | See G-04; live proofs `stale-role-action`, `deletequest-cross-board` | Normalise to a redirect with an Error toast on the Admin controller (G-04) | In scope | Low | closed (see Closing evidence) |

### Findings added by this audit

Found while reproducing the gaps; they are reachable from converted views and are put to the operator as O-14 to O-16.

| ID | Action | Current behaviour | Evidence | Proposed rule | Scope | Severity | Status |
|----|--------|-------------------|----------|---------------|-------|----------|--------|
| X-1 | Users page "Demote to DM" (`DemoteFromAdmin`) | The button is labelled "Demote to DM" and the comment says one level down, but the action sets the role to Player. The Users page then shows the person as Player | `Users.cshtml:103-111`, `Users.Mobile.cshtml:77-81`, `AdminController.cs:71-80` (sets `GroupRole.Player`). Live proof `last-admin-demotion`: the role after the button was Player. Read and live; no fact written because the answer (change the action or the label) is the operator's | Make the action match the label (demote to Dungeon Master) or relabel it; recommended: match the action to the label | In scope | Medium | closed (see Closing evidence) |
| X-2 | `ResetPassword` POST, success message | The success message is written under the temp-data key `SuccessMessage`, which no layout renders, so a successful reset shows no toast at all. The live proof recorded no toast on the Users page after a successful reset | `AdminController.cs:332`, `_Toasts.cshtml` (renders Success, Error, Warning, Info). Live proof `reset-other-board-admin` (`response.toasts` empty after success) | Use the Success key with the existing wording ("Password reset successfully for {name}!") | In scope | Low | closed (see Closing evidence) |
| X-3 | Platform Members "Create New User" dialog | The dialog posts its fields as `CreateMember.Email`, `CreateMember.Name` and `CreateMember.GroupRole`, but the action binds an unprefixed model, so a form submitted as built always arrives empty, fails validation and re-renders with the dialog closed and no message. The existing tests post unprefixed names and never see it (found while creating the p100 fixtures; see deferred-items) | `Members.cshtml` (dialog inputs), `GroupController.cs:395-412`. Fact `GroupManagementIntegrationTests.CreateMember_PostedWithTheFieldNamesTheDialogUses_CreatesTheAccount` | Bind the model with the dialog's prefix (or post unprefixed names) and give the failure path a visible refusal together with G-15 | In scope | Medium | closed (see Closing evidence) |

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

### Gate 1 answers that reach the Admin Users page (100-14)

- Item 6c answered "Admin": the role badge word comes from the shared display mapping through the `_RoleBadge` partial on both layouts. The three reason sentences stay role-free as approved, so no sentence on the Users page carries a mapped role word and `sentenceChanges` is empty.
- Item 8 answered yes: the Remove from Group confirm sentence on both layouts is the corrected one that names the deleted availability answers (`AdminActionMessages.RemoveMemberConfirm`).
- The refused controls are `type="button"` with `aria-disabled="true"` and `aria-describedby` pointing at one reason line per refusal kind per row; none sits inside a POST form and none carries a title. The flags come from the server (`IsSelf`, `IsLastAdmin`, `Reach` per row, one `GetAccountReachAsync` call and one Admin count per page); the crafted-request refusals of 100-06, 100-09 and 100-10 are unchanged.

### Which plan implements each rule

| Rule | Plan |
|------|------|
| G-01, G-02 (elevated part), G-06, G-07, G-08, G-09 | 100-06 and 100-07 (reach rule in 100-06; limiter order, refusal toast and Create User limit in 100-07) |
| X-2 (reset success toast) | 100-06 (the ResetPassword POST it rewrites); not named in that plan's text, see the SUMMARY |
| G-03 (role changes) and X-1 | 100-09 |
| G-03 (removal), G-04, G-13, G-14, G-23 | 100-10 |
| G-05 | closed (see Closing evidence) |
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
| 100-REVIEW-FIX (WR-03; ba8de95e, a4854d60) | `UserRepository.UpdateGroupRoleAsync` (new; the update-only role write behind `UserService.ChangeMemberRoleAsync`, which the four Admin role buttons reach; replaces the upsert `SetGroupRoleAsync` on that path, while `SetGroupRoleAsync` stays for `CreateOrAddToGroupAsync`) | `UserGroupEntity` (`UserGroups`), column `GroupRole` | at most one row: the membership whose `UserId` is the target and whose `GroupId` is the active board, read with `FirstOrDefaultAsync` (`UserGroups` has no board query filter). It never creates a row: a missing row answers `false`, and a row deleted between the read and the save raises `DbUpdateConcurrencyException`, which the Repository catches and answers `false`, mapped by the Domain to `MemberChangeOutcome.NotAMember` (stale Error toast) | tracked property set on the loaded entity, saved by `SaveChangesAsync`. No `Add`, raw SQL, `ExecuteUpdate`, `Attach` or hand-set state (the first commit's `Entry(...).State` detach was removed in a4854d60 because `FeedRevisionWriteSeamTests.NoProductionSource_WritesRowsPastTheChangeTracker` bans it). `UserGroups` is not a feed table, so the calendar feed revision is not involved. Pinned by `AdminRoleGuardTests.RoleChange_ThatLosesARaceWithARemoval_DoesNotPutThePersonBackOnTheBoard`, `RoleChange_WhoseRowIsRemovedDuringTheSave_AnswersFalse_AndDoesNotPutThePersonBack`, `UpdateGroupRole_ForAnExistingMember_StillChangesTheRole` and unit `ChangeMemberRoleAsync_WhenTheMembershipIsRemovedBeforeTheWrite_ReturnsNotAMemberAndNeverReAdds`. |

## Closing evidence (plan 100-22)

Every gap has its closing evidence here. A commit is a task commit of the plan that fixed it; an enabled fact is a test that was committed skipped as a reproduction in 100-04 and runs (and passes) in the suites listed under "Suites at the close"; a live proof is a record in `~/qb-ui-review/phase-100/after/final/refusals.json` (the final sweep, run on the working tree on p100 fixtures only, with a crafted post carrying the page token where the view now disables the control) or in `after/final/clicks.json`. A residual row quotes the operator's words ("approve all recommendations", recorded in `audit-answers.json`).

| Gap | Closing commits | Enabled fact (class `AdminAccountReachTests` unless named) | Live proof (final sweep) | Residual words |
|-----|-----------------|-----------------------------------------------------------|--------------------------|----------------|
| G-01 | b70055f4, 0deeac0f, c7a65a8c (100-06) | `ResetPassword_Post_ForAnAdminOfAnotherBoard_IsRefusedWithAnErrorToast_AndThePasswordIsUnchanged` (bfe03096), `ResetPassword_Post_ForAPlatformAdministrator_...`, `ResetPassword_Get_ForAnAccountWithMoreReach_RedirectsWithAnErrorToast` (2), `EditUser_Post_ForAnAccountWithMoreReach_IsRefused_AndNameAndEmailAreUnchanged` (2), `EditUser_Get_ForAnAccountWithMoreReach_RedirectsWithAnErrorToast` (2); pins stay green | `reach-refused-reset` (GET and crafted POST refused with the Error toast, the old password still signs in), `reach-refused-edit` (name unchanged), `reach-refused-superadmin` (GET refused), `reach-refused-ui` (Edit and Resend Welcome Email are disabled buttons with the visible reason on Admin/Users for the other-board Admin and the SuperAdmin account, both User-Agents) | none (decided D-02) |
| G-02 | 0deeac0f, c7a65a8c (100-06) | `CreateUser_WithAPlatformAdministratorsAddress_ThenResetPassword_IsRefused` | `reach-refused-reset` covers the refusal that follows the add; the add itself was never run live (it would add a platform account to a board) | residual: an ordinary account that also sits on another board can still be added by email and then edited or reset (O-1 accept). Operator: "approve all recommendations". Residual (WR-02 remainder): an account the reach rule refuses (a SuperAdmin, or an Admin of another board) is still mailed when a board Admin adds it by email. The mail stays within that account's per-recipient budget of 3 per hour; once the budget is spent, the add goes ahead and no mail is sent. Edit and reset stay refused. Source: the operator's option (a) for WR-02 (the per-recipient limit only, not a reach check on the add), relayed through the parent session; see 100-REVIEW-FIX.md |
| G-03 | c6aebb7e, 3c9b6977 (100-09, roles); be95b554 (100-10, removal) | `AdminRoleGuardTests.DemoteFromAdmin_OnTheActorThemselves_IsRefused_AndTheirRoleIsUnchanged`, `DemoteToPlayer_OnTheActorThemselves_...`, `PromoteToDM_OnTheActorThemselves_...`, `DeleteUser_OnTheActorThemselves_IsRefused_AndTheirMembershipStays`, `RoleAction_OnTheOnlyAdminOfTheBoard_IsRefused_AndTheRoleIsUnchanged` (3), `DeleteUser_OnTheOnlyAdminOfTheBoard_IsRefused_AndTheMembershipStays` | `guard-self-demotion` (Error toast, still Admin), `guard-self-removal` (Error toast, membership stays), `guard-last-admin` (the SuperAdmin on P100 Audit Campaign: Error toast, the only Admin stays); the refused controls with their reasons are in `guard-self-demotion.ui` and `guard-last-admin.ui` under both User-Agents | none (decided D-03) |
| G-04 | 7c87ba23 (100-10) | `AdminRoleGuardTests.RoleAction_ForSomeoneWhoIsNotOnTheBoard_AnswersAnErrorToast_AndAddsNoMembership` (4) | `guard-stale` (Error toast "not a member of this board", nothing changed) | none |
| G-05 | 6b06a7a5, ce2ba651, 669ca614 (100-08) | `AdminQuestDeleteTests.DeleteQuest_RemovesTheReminderRowsOfTheQuest`, `DeleteQuest_ClearsTheLinkOfAFollowUpQuestOnAnotherBoard`, pin `DeleteQuest_ForAQuestOfAnotherBoard_AnswersAnErrorToast_AndDeletesNothing`; SQL fact `QuestDeleteSqlServerTests` (real foreign keys, passed in the SQL run, `after/final/sql-suites.txt`) | `deletequest-cross-board-post` (the other board's quest: Error toast "That quest no longer exists on this board.", still listed on its own board); `clicks.json` `delete-quest` accepted once per User-Agent on throwaway `P100 Audit Throwaway` quests: one POST, quest gone, Success toast | residual: `QuestService.RemoveAsync` deletes signups in separate saves before the final tracked save, so a refused final save can leave the signups gone under the toast "Nothing was changed". Each deleted signup's date votes go with it by cascade, along with its selection and waitlist state. A related race: when two Admins delete the same quest at once, the second request's per-signup save affects no row and that Admin sees "That quest could not be deleted. Nothing was changed." although the quest is gone (wrong wording, no data loss). Race-only (100-REVIEW.md WR-04); no other board's data and no event data is touched. Older than the phase; accepted at Gate 2 as `D100-questservice-removeasync` (d3, operator) and listed in deferred-items |
| G-06 | 2407aae6 (100-07) | `AdminControllerRateLimitIntegrationTests.SendConfirmationEmail_ResendsRefusedAsOutOfBoard_DoNotSpendTheTargetsBudget` | not run live: the hourly budget is per account for the life of the app process | none |
| G-07 | 0deeac0f (100-06) | `SendConfirmationEmail_ForAnAccountWithMoreReach_IsRefused_AndNoMailIsQueued` (2) | `reach-refused-ui` (Resend Welcome Email is a disabled button with the reason on both layouts) | none |
| G-08 | 2407aae6 (100-07) | `AdminControllerRateLimitIntegrationTests.EditUser_RefusedEmailChange_LeavesTheNameUnchanged`, `EditUser_EmailChange_ExceedingRateLimit_IsRefusedWithAToast` | not run live: the limiter budget is per account for the life of the process | none |
| G-09 | 1fa815fc (100-07) | `AdminControllerRateLimitIntegrationTests.CreateUser_BeyondTwentyAnHourByOneAdministrator_IsRefusedWithAToast_AndOtherAdministratorsAreUnaffected`, `CreateUser_InvalidForm_DoesNotSpendTheCreateBudget` | not run live (20 per 60 minutes per actor) | none |
| G-10 | none | none | none | residual: accepted as designed, the toast names the real owner of an existing address. Operator: "approve all recommendations" |
| G-11 | none | none | none | residual: accepted, D-02 already limits it to non-elevated accounts. Operator: "approve all recommendations" |
| G-12 | none | none | none | residual: deferred to the email phase (O-8 defer); not fixed; listed in deferred-items. Operator: "approve all recommendations" |
| G-13 | 7c87ba23 (100-10) | `GroupManagementIntegrationTests.RemoveMember_ForSomeoneWhoIsNotAMember_AnswersAWarning_AndNoSuccess` | `platform-remove-non-member-after` (Warning "was not a member of this group.", nothing removed, no Success toast) | none |
| G-14 | f0a796b2 pin, unchanged | pin `GroupManagementIntegrationTests.RemoveMember_StillRemovesTheLastAdminOfABoard` stays green | `clicks.json` `remove-member` accepted once per User-Agent on the throwaway member of P100 Audit Lone (the unguarded Platform removal works) | none (O-2 exempt) |
| G-15 | 41d4bc89 (100-19) | `GroupManagementIntegrationTests.CreateMember_WhenItFails_AnswersAnErrorToast_AndCreatesNothing` | sweep row `members-modal-error` under both User-Agents: the empty dialog post answers the persistent Error toast "The account could not be created. Check the details and try again." and writes nothing | none |
| G-16 | none | none | none | residual: accepted as design. Operator: "approve all recommendations" |
| G-17 | none | none | the sweep row `admin-emailstats-stats` loads `EmailStats?force=true` as a GET against the stub | residual: stays a GET (O-6 get). Operator: "approve all recommendations" |
| G-18 | 0628dc53 (100-14) | `AdminPlatformRenderTests.AdminUsers_KeepsTheControlsInOneOrder_AndTheRemoveFormWithItsConfirmSentence`, `AdminControllerIntegrationTests.UsersPage_RendersRemoveFromGroupAsAConfirmedPostForm_OnBothLayouts` (the sentence is `AdminActionMessages.RemoveMemberConfirm`) | `clicks.json` `remove-from-group` under both User-Agents: the dialog text equals the server-rendered `data-confirm-message`, which is the corrected sentence naming the deleted availability answers | none (Gate 1 item 8 yes) |
| G-19 | 130fdddc (100-15) | `AdminPlatformRenderTests.AdminResetPassword_ShowsThePasswordRuleHint_AlignedToTheServerPolicy`, `AdminResetPassword_WithASevenCharacterPassword_ShowsTheLengthMessageWithTheHint` | sweep row `admin-resetpassword-short` (seven characters: the length message sits beside the rule hint, nothing written) under both User-Agents | none (Gate 1 item 9 yes) |
| G-20 | none | none | none | no gap (refuted by the research probe) |
| G-21 | none | none | none | no gap |
| G-22 | 6b06a7a5, ce2ba651 (100-08), 5dd99c1c (100-21) | `AreaControllers_UseNoDeleteVerb` (no `[HttpDelete]` left in the four controllers), the anti-forgery coverage sweep now covers every action | every crafted post of the final sweep carried the page token | no gap |
| G-23 | 7c87ba23, 6b06a7a5 (100-10, 100-08) | the G-04 facts and `DeleteQuest_ForAQuestOfAnotherBoard_AnswersAnErrorToast_AndDeletesNothing` | `guard-stale`, `deletequest-cross-board-post` | none |
| X-1 | c6aebb7e (100-09) | `DemoteFromAdmin` sets Dungeon Master; the role-guard pins `DemoteFromAdmin_ByAnotherAdmin_StillDemotesAnAdminWhoIsNotTheLastOne` | not run live: a demotion would change a fixture's role | none (O-14 dm) |
| X-2 | 0deeac0f (100-06) | `AdminAccountReachTests.ResetPassword_Post_Success_ShowsTheNamedSuccessToastOnTheUsersPage` | `after/final/allowed-paths.json` `reach-allowed-reset`: the Success toast names the account and the new password signs in | none (O-15 yes) |
| X-3 | 41d4bc89 (100-19) | `GroupManagementIntegrationTests.CreateMember_PostedWithTheFieldNamesTheDialogRenders_CreatesTheAccount` | `clicks.json` `setup-member`: a member was created through the real Create New User dialog on P100 Audit Lone (first run, `clicks-run1-note.txt`) | none (O-16 yes) |

### Suites at the close

Build 0 errors. Unit 1636 passed. Integration 3036 passed and 17 skipped (the 17 are the SQL-gated facts of `MigrationRunnerSqlServerTests`, `ShopItemSqlServerTests`, `BoardDeletionSqlServerTests` and `QuestDeleteSqlServerTests`). The SQL-backed suites were run once with `QUESTBOARD_MIGRATOR_TEST_REQUIRED=1` and the connection from `migrator.env` loaded into that command's environment (never printed): 17 passed, 0 skipped (`after/final/sql-suites.txt`).

## The rules as built

- **Reach rule (D-02; O-1, O-3, O-9).** `IUserService.GetAccountReachAsync(actor, groupId, ids)` answers, per account, `Reachable`, `SuperAdminAccount` or `AdministersAnotherBoard`. A SuperAdmin actor reaches everything. Anyone else reaches a peer Admin of the same board, ordinary members and themselves (own name and email reachable, own password reset allowed); an account that holds the SuperAdmin role, or Admin on a board other than the active one, is refused. It is enforced in the controller on `EditUser` and `ResetPassword` (GET and POST) and `SendConfirmationEmail`, and the Users page asks the same question once for all rows so the buttons are disabled with the approved generic reason ("Only a SuperAdmin can change this account.", O-9 generic). A refusal is a redirect to Users with the Error toast.
- **Role and removal guards (D-03; O-2, O-7, O-14).** `IUserService.ChangeMemberRoleAsync` judges the resulting role and answers `MemberChangeOutcome` (`Changed`, `NotAMember`, `Self`, `LastAdmin`): the actor never changes their own role, a board's only Admin is never demoted and never removed from the Admin page, a person who is not on the board gets the stale-page Error toast. `DemoteFromAdmin` sets Dungeon Master as its label says. `IUserService.TryRemoveMemberAsync` carries the same rules for Remove from Group. The Platform Members removal stays unguarded for the last Admin (O-2 exempt: a board is emptied before it is deleted) and warns for a non-member. Race of two Admins demoting each other at once: accepted (O-7).
- **Limiter order and refusal contract (O-4, O-5).** The resend and email-change limiters are taken after the scope, reach and already-confirmed checks and before any write; a refused request is a redirect with the Error toast (`AdminActionMessages.EmailRateLimited`), never a bare 429 page. Create User has its own per-administrator limiter (`AdminRateLimitKeys.CreateUser`, 20 per 60 minutes, `AdminActionMessages.CreateLimited`); an invalid form does not spend it.
- **Quest delete cleanup (O-11).** `QuestRepository.RemoveAsync` removes the quest's reminder rows and unlinks a follow-up quest on another board in the same tracked save as the quest; the controller turns a remaining database refusal into the Error toast. The write stays on the change tracker (no bulk, raw SQL, `Attach` or hand-set state; `writes-check.py`: four write lines, all in that method). The residual `QuestService.RemoveAsync` signup saves are listed in the table above.
- **EmailStats seam (O-13).** `EmailSettings:ResendBaseUrl` is honoured by `ResendBaseAddress.Resolve` only on a Development host, so a scratch stub can stand in for the provider in captures and `EmailStatsRenderTests`; a Production host ignores it. Refresh Email Stats stays a GET (O-6).
- **Destructive actions.** `DeleteUser` and `DeleteQuest` are `[HttpPost]` with anti-forgery, answer a redirect with a toast, and are submitted by ordinary forms carrying `data-confirm-message` (the `confirm-submit.js` module; no inline script, no `alert`, no `fetch`). `AreaControllers_UseNoDeleteVerb` keeps it so.

## DeleteQuest verdict (final)

Unchanged and confirmed live: Delete Quest is limited to the active board. The controller finds the quest through the repository's `FindAsync`; the quest entity's fail-closed query filter returns nothing for another board's id, and `QuestService.RemoveAsync` re-reads through a filtered query and returns without deleting when it gets nothing. The pin is `AdminQuestDeleteTests.DeleteQuest_ForAQuestOfAnotherBoard_AnswersAnErrorToast_AndDeletesNothing` (it was a 404 pin on the DELETE verb until 100-08 turned the endpoint into a POST with a toast), and the final sweep's `deletequest-cross-board-post` agrees: from `p100admin` with P100 Audit One-Shot open, the POST for a P100 Audit Campaign quest answers the Error toast "That quest no longer exists on this board." and the quest is still listed on its own board.

## Threat register

Filled by the phase security audit on 2026-11-19 at c7f7ae18 (ASVS level 1, block_on high). Repo paths are relative to the repository root; `~/qb-ui-review/phase-100` is the scratch root.

| Threat ID | Category | Component | Severity | Disposition | Status | Evidence |
|-----------|----------|-----------|----------|-------------|--------|----------|
| T-100-SC | Tampering | npm packages | high | mitigate | CLOSED | `tools/node_modules` is a symlink to `phase-94/tools/node_modules` (playwright, playwright-core only); `tools/package.json` pins 1.64.1; no lockfile and no local install |
| T-100-01 | Information disclosure | email to real addresses from the local app | high | mitigate | CLOSED | `tools/run-app.mjs:9-11,32-42` forces `SuppressSending=true` and SMTP 127.0.0.1:1025 after any `--env`, and refuses overrides; `:111-115` checks the listener's `/proc/<pid>/environ` |
| T-100-02 | Information disclosure | credentials.json, migrator.env, state/*.json | medium | mitigate | CLOSED | all mode 600 under the scratch root; none tracked by git; no credential assignment in phase docs; every address in the phase docs is `@example.invalid` |
| T-100-03 | Information disclosure | real member data in tracer captures | medium | mitigate | CLOSED | captures exist only under the scratch root; the phase diff adds no image or HTML to the repo |
| T-100-04 | Information disclosure | calendar feed addresses in capture records | high | mitigate | CLOSED | `/feeds/calendar/` is aborted in `sweep.mjs:152`, `before-capture-100.mjs:84`, `lookcheck-100.mjs:35`, `clicks-100-22.mjs:55`, `findings-extra-100.mjs:28`; query strings are stripped (`sweep.mjs:153,209-210`); grep of all scratch records: 0 feed addresses. `audit-proofs*.mjs` set no abort route but record no request URLs |
| T-100-05 | Denial of service | the operator's database | medium | mitigate | CLOSED | `run-app.mjs:5-7,85-88` never stops a process it did not start |
| T-100-06 | Tampering | fixture writes on a real-data database | high | mitigate | CLOSED | `tools/fixtures.mjs` uses app forms only, with no SQL client, and has a `--check` mode (`:221,333`); 100-02-SUMMARY:34 says the baseline board's member count is unchanged |
| T-100-07 | Information disclosure | welcome emails for p100 accounts | high | mitigate | CLOSED | run-app forces suppression; `fixtures.json` has 15 addresses, all `@example.invalid` |
| T-100-08 | Information disclosure | generated passwords | medium | mitigate | CLOSED | `credentials.json` mode 600; `fixtures.json` has 0 "password" hits |
| T-100-09 | Elevation of privilege | reused SuperAdmin gaining a membership | low | mitigate | CLOSED | 100-02-SUMMARY:51: operator said yes; added as Player to P100 Audit One-Shot only |
| T-100-10 | Tampering | sweep writing to the database | medium | mitigate | CLOSED | `sweep.mjs:132-136` answers every stopped POST with 204 |
| T-100-11 | Information disclosure | real-member BEFORE frames | medium | mitigate | CLOSED | `mask-real-rows.py` masks with flat blocks and re-checks them; `gate-hygiene.py:77-91` rejects unmasked frames; re-run passed |
| T-100-12 | Tampering | baseline app migrating the database | low | mitigate | CLOSED | no change under `QuestBoard.Repository/Migrations` since 34ede3a8; run-app refuses a port that another tree serves |
| T-100-13 | Elevation of privilege | board Admin taking over another board's Admin or a SuperAdmin (G-01, G-02) | high | mitigate | CLOSED | fixed by 100-06 (see T-100-25 to T-100-28); both gates answered; no tag at HEAD |
| T-100-14 | Tampering | live proofs changing real data | high | mitigate | CLOSED | SECURITY.md "Live proofs": p100 fixtures only, the reused accounts were never edited, `fixtures.mjs --check` ran after each proof; `audit/live-proofs.json` mode 600 |
| T-100-15 | Information disclosure | new password set by a live proof | medium | mitigate | CLOSED | kept in `credentials.json` (600) only; SECURITY.md proof row `reset-other-board-admin` |
| T-100-16 | Repudiation | audit evidence | low | mitigate | CLOSED | the gap table cites file:line, fact and proof id; 31 files in `audit/red/` |
| T-100-17 | Denial of service | board left with no Admin by the last-Admin proof | medium | mitigate | CLOSED | SECURITY.md "Live proofs" Restored column; 100-04-SUMMARY:69 |
| T-100-18 | Repudiation | the audit answers | medium | mitigate | CLOSED | `audit-answers.json` has source `operator` and the verbatim answerLine |
| T-100-19 | Elevation of privilege | accepted residual left open by mistake | high | mitigate | CLOSED | every residual (G-02, G-05/WR-04, G-10, G-11, G-12, G-16, G-17, O-7) carries the operator's words; this audit re-checked each against the code and found them consistent |
| T-100-20 | Information disclosure | real members in BEFORE frames beside the mocks | medium | mitigate | CLOSED | `mask-real-rows.py`; `mockups/v1/frames.json`; gate hygiene ok |
| T-100-21 | Tampering | a mock that cannot ship | low | mitigate | CLOSED | `mock-scan.py mockups/v1` re-run: 0 hits; lookcheck in 100-11-SUMMARY:66-67 |
| T-100-22 | Information disclosure | Gate 1 package content | medium | mitigate | CLOSED | `gate-hygiene.py gate1`: ok (111 JPEG, allowed entries only); the package is outside the repo |
| T-100-23 | Repudiation | Gate 1 answers | low | mitigate | CLOSED | `gate1/answers.json`: source operator, 22 items, each with its source |
| T-100-24 | Tampering | unapproved copy change or defaulted item | medium | mitigate | CLOSED | Gate 1 items each carry a source; the 15 approved sentences in `AdminActionMessages.cs` match verbatim |
| T-100-25 | Elevation of privilege | ResetPassword for a SuperAdmin or another board's Admin | high | mitigate | CLOSED | `AdminController.cs:315-316` (GET), `:345-346` (POST); `UserService.cs:107-116` re-checks before `AdminResetPasswordAsync`; `AdminAccountReachTests.cs:149,164,182`; unit `ResetPasswordAsync_ForATargetOutOfReach_FailsAndNeverCallsTheIdentityReset` |
| T-100-26 | Spoofing | EditUser email change, then anonymous confirmation | high | mitigate | CLOSED | `AdminController.cs:222-223,254-255` run before the lease, the write and the job; `AdminAccountReachTests.cs:215` asserts 0 `ChangeEmailConfirmationJob` |
| T-100-27 | Elevation of privilege | add by email, then reset | medium | mitigate | CLOSED | `CreateUser_WithAPlatformAdministratorsAddress_ThenResetPassword_IsRefused` (`:263`); the ordinary-account remainder is the G-02 residual (O-1 accept) |
| T-100-28 | Tampering | SuperAdmin test read from a membership row | high | mitigate | CLOSED | `UserService.cs:133-138` uses `GetSuperAdminIdsAsync` (`IdentityService.cs:181-188`, the Identity role); `UserServiceTests.cs:555` |
| T-100-29 | Denial of service | reach query per row | low | mitigate | CLOSED | one query in `UserRepository.cs:144-158` plus one `GetUsersInRoleAsync`; the Users page asks once (`AdminController.cs:38`); `UserServiceTests.cs:479,586` |
| T-100-30 | Information disclosure | refusal wording naming another board's role | low | mitigate | CLOSED | `AdminActionMessages.cs:12,74` match the approved toastReach and reasonReach (O-9 generic) |
| T-100-31 | Denial of service | resend budget burnt by out-of-board requests | low | mitigate | CLOSED | lease at `AdminController.cs:423`, after the checks at `:403-418`; `AdminControllerRateLimitIntegrationTests.cs:261` |
| T-100-32 | Tampering | half-saved edit when the email change is refused | low | mitigate | CLOSED | lease at `AdminController.cs:268-270`, before the writes at `:272-279`; fact at `:316` |
| T-100-33 | Denial of service | unlimited creates spending the relay quota | low | mitigate | CLOSED | `Program.cs:197-207` (20 per 60 minutes per actor, keyed); `AdminController.cs:122-125`; facts at `:368,400` |
| T-100-34 | Information disclosure | real Resend key in tests or config | medium | mitigate | CLOSED | `EmailStatsWebApplicationFactory.cs:25-32` uses a stub key and the canned handler; the appsettings*.json diff since 34ede3a8 is empty |
| T-100-35 | Spoofing | ResendBaseUrl pointed at a hostile host | low | mitigate | CLOSED | `ResendBaseAddress.cs:11-20` honours it in Development only; `Program.cs:244-251`; `EmailStatsRenderTests.cs:244,252,266-276`; guard `ResendClient_BaseAddressIsDecidedOnlyByTheDevelopmentGatedHelper` |
| T-100-36 | Tampering | CSRF on DeleteUser and DeleteQuest | medium | mitigate | CLOSED | `AdminController.cs:370-372,468-471` are `[HttpPost][ValidateAntiForgeryToken]`; `AntiForgeryTokenCoverageTests`; `AreaControllers_UseNoDeleteVerb` |
| T-100-37 | Tampering | cross-board quest id | medium | mitigate | CLOSED | `QuestBoardContext.cs:480-483` filter; `AdminController.cs:474-478`; `AdminQuestDeleteTests.cs:60` |
| T-100-38 | Denial of service | unhandled DbUpdateException on quest delete | medium | mitigate | CLOSED | `QuestRepository.cs:32-58` saves the cleanup together with the quest; `AdminController.cs:480-487` catches and answers with a toast; `QuestDeleteSqlServerTests.cs:114` (SQL run in SECURITY.md "Suites at the close") |
| T-100-39 | Tampering | writes to Quests outside the change tracker | high | mitigate | CLOSED | `QuestRepository.cs:47,54,56-57` use only tracked writes; the phase diff has no Execute*, FromSql, Attach or State=; feed files untouched; `FeedRevisionWriteSeamTests` passes |
| T-100-40 | Tampering | stored names in an inline script | low | mitigate | CLOSED | `ConvertedViews_CarryNoInlineStyleScriptOrHandler`; `wwwroot/js/confirm-submit.js` reads `dataset.confirmMessage` |
| T-100-41 | Denial of service | self-lockout or Admin-less board through role changes | medium | mitigate | CLOSED | `UserService.cs:179-187`; `AdminRoleGuardTests.cs:91-136` |
| T-100-42 | Tampering | action name used to skip the guard | medium | mitigate | CLOSED | the guard compares the current and new role (`UserService.cs:175-187`); crafted PromoteToDM case at `AdminRoleGuardTests.cs:123` |
| T-100-43 | Denial of service | simultaneous mutual demotion | low | accept | CLOSED (accepted) | SECURITY.md Operator answers O-7 "accept"; "The rules as built" |
| T-100-44 | Repudiation | removal toast claiming success | low | mitigate | CLOSED | `GroupController.cs:521-525` answers a Warning; `GroupManagementIntegrationTests.cs:988` |
| T-100-45 | Denial of service | board deletion blocked by a guard in the shared chokepoint | medium | mitigate | CLOSED | `GroupService.cs:44-45` has no guard; pin at `GroupManagementIntegrationTests.cs:967`; board-delete test changes are additive only |
| T-100-46 | Tampering | quest title or description in markup | low | mitigate | CLOSED | no `Html.Raw` in `Views/Admin/Quests*.cshtml` |
| T-100-47 | Repudiation | a status shown differently on the two layouts | low | mitigate | CLOSED | one `QuestStatusBadgeModel` and one `_QuestStatusBadge` partial, used by both Quests views |
| T-100-48 | Tampering | a shared class changing pages outside the area | low | mitigate | CLOSED | only `.control-reason` (`components.css:704`) and `.row-slip` (`:729`) added, inside `@layer components` (`:5`) |
| T-100-49 | Elevation of privilege | a refused control that still submits | medium | mitigate | CLOSED | `Users.cshtml:72-143`; `AdminPlatformRenderTests.cs:231-243` asserts no form ancestor |
| T-100-50 | Information disclosure | which board's role made an account unreachable | low | mitigate | CLOSED | generic `ReasonReach` with no board name; the inherent "out of reach" signal is accepted under O-9 |
| T-100-51 | Tampering | member names or emails in attributes | low | mitigate | CLOSED | reason ids are `reason-*-{numeric id}` (`Users.cshtml:38-40`, `Users.Mobile.cshtml:29-31`) |
| T-100-52 | Information disclosure | browser autofill into CreateUser | low | mitigate | CLOSED | `AdminPlatformRenderTests.cs:586-587,626-627,688` |
| T-100-53 | Tampering | client length rule weaker than the server policy | low | accept | CLOSED (mitigated) | `ResetPasswordViewModel.cs:12` min 8 matches `Program.cs:53-57`; Gate 1 item 9 yes |
| T-100-54 | Tampering | CSRF on the three account forms | low | mitigate | CLOSED | `ConvertedViews_EveryPostFormCarriesATokenSource` (`AdminPlatformConversionGuardTests.cs:609`) |
| T-100-55 | Tampering | CSRF on Open Board | low | mitigate | CLOSED | `AdminPlatformRenderTests.cs:836` |
| T-100-56 | Information disclosure | real board names in captures | medium | mitigate | CLOSED | kept local; gate hygiene ok |
| T-100-57 | Tampering | board-deletion pieces changed by the badge swap | low | mitigate | CLOSED | the `_BoardDeleteBody.cshtml` diff is the badge partial plus the approved Gate 2 e5 h3-to-h2 change only |
| T-100-58 | Tampering | a renamed field dropping a setting | low | mitigate | CLOSED | `AdminPlatformRenderTests.cs:1041,1174` |
| T-100-59 | Tampering | CSRF on Group Create and Edit | low | mitigate | CLOSED | `GroupController.cs:57-59,91-93`; post-form scanner |
| T-100-60 | Tampering | a name breaking out of an inline handler | medium | mitigate | CLOSED | `LinkedBoards/Index.cshtml:117,155`; hostile-name facts at `LinkedBoardsPlatformIntegrationTests.cs:533-574,760-765` |
| T-100-61 | Denial of service | a SuperAdmin disabling their own account | low | mitigate | CLOSED | `UsersController.cs:33-40` (unchanged); `AdminPlatformRenderTests.cs:1490` |
| T-100-62 | Information disclosure | real accounts and sets in captures | medium | mitigate | CLOSED | kept local; reports are structural |
| T-100-63 | Repudiation | a failed create shown as nothing happening | low | mitigate | CLOSED | `GroupController.cs:426-430,507-510`; `GroupManagementIntegrationTests.cs:1026` |
| T-100-64 | Tampering | markup built from strings in a reopen module | low | mitigate | CLOSED (no surface) | O-12 chose the toast, so no module was built and no JS changed; `AreaModules_BuildNoMarkupFromStrings` |
| T-100-65 | Tampering | CSRF on Remove Member and Add | low | mitigate | CLOSED | `GroupController.cs:324-326,417-419,514-516` |
| T-100-66 | Information disclosure | real members in captures | medium | mitigate | CLOSED | gate captures use p100 boards; hygiene ok |
| T-100-67 | Information disclosure | Resend key or delivery data in a capture | medium | mitigate | CLOSED | `tools/resend-stub.mjs:48` binds 127.0.0.1 and is not listening now; appsettings diff empty |
| T-100-68 | Tampering | CSRF on a POST refresh | low | mitigate | CLOSED (no surface) | O-6 chose GET: `AdminController.cs:494-496` stays GET under SuperAdminOnly; G-17 residual |
| T-100-69 | Denial of service | a layout fix changing every page | low | mitigate | CLOSED | no layout or shell file in the phase diff |
| T-100-70 | Tampering | a reproduction fact left skipped | medium | mitigate | CLOSED | `AdminPlatformConversionGuardTests.cs:782,790`; 0 skips found |
| T-100-71 | Tampering | a DELETE verb or bulk write slipping back in | medium | mitigate | CLOSED | `:813,821`; phase-diff scan is clean |
| T-100-72 | Repudiation | a deleted test hiding lost coverage | low | mitigate | CLOSED | `tests-before.txt`, `tests-after.txt`, `tests-renamed.md`; 100-21-SUMMARY "Test inventory" |
| T-100-73 | Tampering | click-throughs touching real rows | high | mitigate | CLOSED | `tools/clicks-100-22.mjs:2-15,31-33` use throwaway p100 rows; 100-22-SUMMARY |
| T-100-74 | Information disclosure | migrator connection string | medium | mitigate | CLOSED | `migrator.env` mode 600; its value is in 0 files under the scratch root or `.planning` |
| T-100-75 | Information disclosure | real members in the final captures | medium | mitigate | CLOSED | kept local; gate2 hygiene ok |
| T-100-76 | Repudiation | a gap closed without evidence | medium | mitigate | CLOSED | SECURITY.md "Closing evidence" covers every gap |
| T-100-77 | Information disclosure | Gate 2 package content | medium | mitigate | CLOSED | `gate-hygiene.py gate2` ok (289 JPEG) |
| T-100-78 | Tampering | an unreviewed release or image publication | high | mitigate | CLOSED | no tag at HEAD or containing phase commits; `docker-publish.yml` and `release.yml` run on tags only |
| T-100-79 | Repudiation | Gate 2 answers | low | mitigate | CLOSED | `gate2/answers.json`: source operator, versions v1 and v2 |
| T-100-80 | Denial of service | self removal or removal of the last Admin | medium | mitigate | CLOSED | `UserService.cs:196-214`; `AdminController.cs:386-393`; `AdminRoleGuardTests.cs:105,136,185,199` |
| T-100-81 | Tampering | a server fix shipping unapproved wording | low | mitigate | OPEN (non-blocking) | the 15 approved sentences match the code, but `AdminActionMessages.cs:34,40-41` (`EmailSkippedRateLimited` and `AddedToGroupEmailNotSent`, from WR-02 commit d5103df7) were added after Gate 2 (edf81f9f). They are not in the approved set and appear 0 times in gate2/index.html. The fallback text at `UserService.cs:112` (reachable only in a race) was also never shown |

### Open threats

| Threat ID | Severity | Blocking | What is missing | To close it |
|-----------|----------|----------|-----------------|-------------|
| T-100-81 | low | no (below block_on high; not counted in `threats_open`) | The WR-02 behaviour was approved by the operator (option (a), relayed through the parent session: the add is never refused, the mail is skipped when the recipient's budget is spent, and the Success toast says so). The wording of the two new sentences was not approved: `AdminActionMessages.EmailSkippedRateLimited` ("No email was sent, because too many emails were sent to this account recently.") and the toast `AdminActionMessages.AddedToGroupEmailNotSent` that ends with it ("{name} has been added to the group as {role}. No email was sent, because too many emails were sent to this account recently."). Neither is among the 15 approved sentences, and Gate 2 never showed them. | The operator approves the wording verbatim; then add both to "Approved sentences" with that source and mark T-100-81 closed. |

### Accepted risks

1. **T-100-43, simultaneous mutual demotion race.** Source: O-7 "accept" (`audit-answers.json`; "Operator answers" and "The rules as built" above).
2. **G-02.** An ordinary account that also sits on another board can be added by email, then edited or reset. Related to T-100-27. Source: O-1 accept, operator "approve all recommendations" (G-02 rows above).
3. **G-05 / WR-04.** Signups (and each signup's date votes) are deleted in separate saves before the final quest save. Source: Gate 2 d3 accept (`gate2/answers.json`, `D100-questservice-removeasync`), the G-05 row of "Closing evidence", and deferred-items #6.
4. **G-10.** The toast names the real owner of an existing address. Source: operator "approve all recommendations" (G-10 rows).
5. **G-11.** A board Admin can edit the global name, address and key flag of accounts that are not elevated. Source: operator "approve all recommendations" (G-11 rows).
6. **G-12.** Owner notification on an admin reset or email change is deferred. Source: O-8 defer; G-12 rows; `deferred-items.md`.
7. **G-14.** The Platform RemoveMember may remove the last Admin. Source: O-2 exempt.
8. **G-16.** Peer SuperAdmins can disable each other. Source: operator "approve all recommendations" (G-16 rows).
9. **G-17.** The EmailStats force refresh stays a GET (covers T-100-68). Source: O-6 get.
10. **T-100-50.** The generic reason still shows that an account is out of the acting Admin's reach. Source: the 100-14 plan threat model and O-9 generic.

## Security Audit 2026-11-19

| Threats found | Closed | Open |
|---------------|--------|------|
| 82 | 81 | 1 (low, below block_on high) |

ASVS level 1, block_on high, `threats_open: 0`. Verified at c7f7ae18 against the code, the scratch tooling and re-runs of the security-relevant tests and checks.
