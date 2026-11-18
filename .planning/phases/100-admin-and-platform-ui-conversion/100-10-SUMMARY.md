---
phase: 100-admin-and-platform-ui-conversion
plan: 10
subsystem: member-removal-guard
tags: [authorization, domain-guard, last-admin, self-removal, stale-page, toasts]
requires: ["100-09", "100-05"]
provides:
  - "IUserService.TryRemoveMemberAsync returns a MemberChangeOutcome (Changed, NotAMember, Self, LastAdmin) and calls the unguarded IGroupService.RemoveMemberAsync only for Changed"
  - "Admin DeleteUser (Remove from Group) goes through the guarded removal and maps each outcome to an approved toast"
  - "The four role actions answer the stale-page Error toast for someone who is not on the board"
  - "Platform RemoveMember answers a Warning for a non-member and keeps the unguarded removal for members"
affects: [100-14]
tech-stack:
  added: []
  patterns: ["guard wraps the shared chokepoint instead of living in it", "outcome enum from a Domain service mapped to a toast in the controller"]
key-files:
  created: []
  modified:
    - QuestBoard.Domain/Interfaces/IUserService.cs
    - QuestBoard.Domain/Services/UserService.cs
    - QuestBoard.Service/Controllers/Admin/AdminController.cs
    - QuestBoard.Service/Areas/Platform/Controllers/GroupController.cs
    - QuestBoard.Service/ViewModels/AdminViewModels/AdminActionMessages.cs
    - QuestBoard.UnitTests/Services/UserServiceTests.cs
    - QuestBoard.IntegrationTests/Controllers/AdminRoleGuardTests.cs
    - QuestBoard.IntegrationTests/Controllers/AdminControllerIntegrationTests.cs
    - QuestBoard.IntegrationTests/Controllers/GroupManagementIntegrationTests.cs
    - QuestBoard.IntegrationTests/Tests/AdminPlatformConversionGuardTests.cs
decisions:
  - "O-2 exempt (G-14 approve): Platform RemoveMember still removes the last Admin of a board, because a board can only be deleted once it has no members. The guard is in TryRemoveMemberAsync (Admin pages), not in GroupService.RemoveMemberAsync or the repository; both are unchanged since the plan start."
  - "G-04 / G-23 approve: a role action or Remove from Group for an id that is not a member of the active board answers the approved stale-page Error toast; nothing is changed."
  - "G-13 approve: Platform RemoveMember of a non-member answers the approved Warning sentence and removes nothing; a member is removed with today's success toast."
  - "Self removal is refused for every role, not only Admin: a platform administrator who is a Player on the board also cannot remove themselves from the Admin page."
  - "No sentence names the Admin or Administrator role (item 6c is still a Gate 1 question); role-noun-check.py prints ok."
metrics:
  duration: "about 40 minutes"
status: complete
actuals:
  tokens: 15000
  tasks: 3
  commits: 3
---

# Phase 100 Plan 10: Removal guard and stale-page answers Summary

Remove from Group on the Admin pages can no longer drop the acting Admin or the board's only Admin, stale requests for a non-member answer a toast instead of silence, and the Platform page stops claiming it removed someone who was never a member.

## What was built

**Domain.** `UserService.TryRemoveMemberAsync(groupId, actingUserId, targetUserId)` reads the target's role (null gives `NotAMember`), refuses `Self` when actor and target are the same account, refuses `LastAdmin` when the target is an Admin and the board has at most one Admin (the same `CountMembersWithRoleAsync` query the role guard uses), and otherwise calls `IGroupService.RemoveMemberAsync` and returns `Changed`. `GroupService.RemoveMemberAsync` and `GroupRepository.RemoveMemberAsync` are untouched, so board deletion and the Platform page keep their unguarded chokepoint (verified by `git diff` against the plan start: no change).

**Admin controller.** `DeleteUser` resolves the acting id from the `NameIdentifier` claim, calls the guarded removal and maps `NotAMember` to `MemberStale`, `Self` to `SelfRefused`, `LastAdmin` to `LastAdminRefused` (all Error toasts) and `Changed` to the Success toast `{name} was removed from the group.`. The shared role-action helper now maps `NotAMember` to `MemberStale` too, so the four role buttons no longer redirect silently for a person who left the board.

**Platform controller.** `RemoveMember` checks membership first. For a non-member it sets `TempData["Warning"]` to `AdminActionMessages.NotAMemberOfGroup(name)` ("{name} was not a member of this group.", name falls back to "That person" for an id no account holds), removes nothing and keeps the search terms on the redirect. For a member it calls the unguarded removal as before (O-2 exempt).

## Facts

- Unit: six new `TryRemoveMemberAsync` facts (self for each role, last Admin whoever acts, another Admin remaining, non-Admin targets without counting Admins, not a member). `UserService` filtered run: 49 green.
- Integration, enabled: `DeleteUser_OnTheActorThemselves_IsRefused_AndTheirMembershipStays`, `DeleteUser_OnTheOnlyAdminOfTheBoard_IsRefused_AndTheMembershipStays`, `RoleAction_ForSomeoneWhoIsNotOnTheBoard_AnswersAnErrorToast_AndAddsNoMembership` (4 actions), `RemoveMember_ForSomeoneWhoIsNotAMember_AnswersAWarning_AndNoSuccess` (renamed, also pins the Warning sentence).
- Integration, added: DeleteUser for a non-member, the three approved sentences pinned by text (self, keep-a-manager, stale), and a removal of an Admin who is not the last one still works.
- Rewritten: the out-of-group role-change theory now expects the stale Error toast (same intent: the target's only membership row is untouched). Renames are logged in `tests-renamed.md`.
- Pins that pass unchanged: `RemoveMember_StillRemovesTheLastAdminOfABoard` (Platform), the board-deletion facts, `DeleteUser_Post_RemovesGroupMembershipOnly...`.
- `AdminRoleGuardTests` has no skipped fact left. `GroupManagementIntegrationTests` keeps 2 skipped facts, both for 100-19: the failed Create New User visibility fact and the `CreateMember.` prefix binding fact (the plan said 1; it predates the prefix fact being committed as a second reproduction).
- `AdminPlatformConversionGuardTests.PhaseSourceFiles` gained the Platform `GroupController.cs` and `AdminControllerIntegrationTests.cs` (append only); no planning reference found.

## Verification

- `role-noun-check.py`: ok (no string names the Admin or Administrator role).
- No view, sheet or script change (`git diff --stat` against the plan start for Views, Platform Views, wwwroot is empty).
- Live on fixtures (port 8340, `after/100-10/live.json`): `guard-self-removal` p100admin pressing Remove on their own row on P100 Audit One-Shot: Error toast with the self sentence, still an Admin, member list unchanged. `guard-stale` Promote to Admin for an id no account holds: Error toast "That person is not a member of this board any more.", roles unchanged. `platform-remove-non-member-after` the SuperAdmin removing p100player from P100 Audit Lone: Warning "... was not a member of this group.", no success toast, Lone still empty, p100player still on One-Shot. `fixtures.mjs --check`: passed under both User-Agents. The proof tool `audit-proofs.mjs` gained the three proof ids (scratch tools only).
- Suites: unit 1636 passed, 0 failed; integration 2850 passed, 19 skipped, 0 failed.

## Deviations from Plan

None in the source. Two tool-side facts: the three proof ids named in the plan did not exist in `audit-proofs.mjs`, so they were added there (scratch root, not the repository); and the plan's expected count of 1 skipped fact in `GroupManagementIntegrationTests` is 2 for the reason above.

## Residuals and notes for Gate 1 (100-11/12) and 100-14

- Self removal is refused for every role, so the Remove from Group control on the viewer's own row is always refused; 100-14 should disable it with the approved `reasonSelf` sentence, and the Remove control of the board's only Admin with `reasonLastAdmin` (both role-free).
- No sentence names the Admin or Administrator role; item 6c is still unanswered.
- Platform RemoveMember may still remove the last Admin (O-2 exempt); recorded, not guarded.
- The QuestService signup-before-final-save residual from 100-08 is still open for review.
- The posted Platform success toast stays "Member removed from the group." (the approved Admin sentence with the person's name applies to the Admin page only).

## Known Stubs

None.

## Threat Flags

None. T-100-44 (success claimed for a non-removal), T-100-45 (guard kept out of the shared chokepoint) and T-100-80 (self and last-Admin removal) are mitigated by the changes and facts above.

## Commits

- be95b554 feat(100-10): guarded member removal refuses self removal and the last Admin in the Domain
- 7c87ba23 feat(100-10): stale-page toasts for role actions, Platform non-member removal answers a Warning
