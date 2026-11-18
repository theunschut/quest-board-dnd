# Phase 100 deferred items

Out-of-scope discoveries made while executing the phase. Each row names where it was found, what it is and what was done.

## Found while executing

| Found in | Item | Status |
|----------|------|--------|
| 100-02 (fixture creation) | The Platform Group Members "Create New User" modal posts its fields as `CreateMember.Email`, `CreateMember.Name` and `CreateMember.GroupRole`, but the `CreateMember` action binds an unprefixed `CreateMemberViewModel`. A form submitted as built therefore always arrives with empty fields, fails validation and re-renders the Members page with the modal closed and no visible message. The integration tests post unprefixed names, so they do not see it. The fixture tool submits the same form with the `CreateMember.` prefix removed from its field names, which creates the account through the real endpoint. Found in `Areas/Platform/Views/Group/Members.cshtml` (modal inputs) against `GroupController.CreateMember`. | Not fixed in 100-02 (the plan changes no source). Needs an owner: bind the view-model with `[Bind(Prefix = "CreateMember")]` or post unprefixed names, and give the failed path a visible reopen or toast. Related to the plan that decides the failed-create behaviour. Update from 100-04: reproduced as a skipped fact (`GroupManagementIntegrationTests.CreateMember_PostedWithTheFieldNamesTheDialogUses_CreatesTheAccount`) and put to the operator as finding X-3 / O-16 in the security document, so it is in scope and no longer only deferred. |

## Server gaps no converted view reaches (D-04)

From the server-rules audit (plan 100-04). Severity and the reason each is out of scope; the full gap table is in `100-SECURITY.md`.

| Where | What was read or measured | Severity | Why it is out of scope |
|-------|---------------------------|----------|------------------------|
| Owner notification of an admin password reset and of an admin-initiated email change (`AdminController.cs:263-277`, `:328-332`; gap G-12) | Read: no mail reaches the account owner when a board Admin resets their password, and the old address is not told when an Admin starts an email change | Low | A notification is a new email template, which is the email phase's territory; no converted view shows or sends it. Deferred by recommendation, confirmed or changed by the operator at O-8 |
| Anonymous email-change confirmation that also renames the account (`AccountController.cs:298-318`, `IdentityService.cs:124-135`) | Read: `ConfirmEmailChange` is an anonymous token link; on success it changes the address and also sets the username to the new address, and the token only reaches the new address's owner | Low on its own (the token is bound to the new address and mailed to it). It was the second hop of the takeover chain only while a board Admin could start an email change for an elevated account, which the reach rule closes in scope | Reached from an email link, not from any of the 25 views; the Account area is not part of this conversion |
| `QuestController.Delete` (the Dungeon Master's own quest delete) | Read: it goes through the same `QuestService.RemoveAsync` as the Admin delete, so the reminder-row and follow-up cleanup in the audit's quest-delete gap (G-05) fixes it as well | Medium (same defect as the Admin delete, shared fix) | Not reachable from a converted view; no separate change is planned, it is covered by the shared service fix |
| Two SuperAdmins can disable each other and only SQL recovers (`UsersController.cs:33-60`; gap G-16) | Read, and an existing test pins that a peer SuperAdmin may be disabled | Info | Platform design choice, recorded; the Platform Users page is converted but this behaviour is not changed by this phase |
