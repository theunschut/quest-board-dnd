---
phase: 100-admin-and-platform-ui-conversion
plan: 09
subsystem: role-change-guard
tags: [authorization, domain-guard, last-admin, self-change, toasts, reproduction-facts]
requires: ["100-08", "100-05"]
provides:
  - "IUserService.ChangeMemberRoleAsync returns a MemberChangeOutcome (Changed, NotAMember, Self, LastAdmin) judged in the Domain on the target's current role, the new role and the board's Admin count"
  - "IGroupRepository / IGroupService CountMembersWithRoleAsync, one CountAsync over UserGroups"
  - "The four role buttons (PromoteToAdmin, DemoteFromAdmin, PromoteToDM, DemoteToPlayer) share one controller helper that maps Self and LastAdmin to the approved Error toasts"
  - "Demote to DM now sets Dungeon Master, matching its label"
affects: [100-10]
tech-stack:
  added: []
  patterns: ["guard judged on the resulting role, never on the action name", "outcome enum returned from a Domain service and mapped to a toast in the controller"]
key-files:
  created:
    - QuestBoard.Domain/Enums/MemberChangeOutcome.cs
  modified:
    - QuestBoard.Domain/Interfaces/IUserService.cs
    - QuestBoard.Domain/Services/UserService.cs
    - QuestBoard.Domain/Interfaces/IGroupService.cs
    - QuestBoard.Domain/Services/GroupService.cs
    - QuestBoard.Domain/Interfaces/IGroupRepository.cs
    - QuestBoard.Repository/GroupRepository.cs
    - QuestBoard.Service/Controllers/Admin/AdminController.cs
    - QuestBoard.Service/ViewModels/AdminViewModels/AdminActionMessages.cs
    - QuestBoard.UnitTests/Services/UserServiceTests.cs
    - QuestBoard.UnitTests/Services/GroupServiceTests.cs
    - QuestBoard.IntegrationTests/Controllers/AdminRoleGuardTests.cs
    - QuestBoard.IntegrationTests/Tests/AdminPlatformConversionGuardTests.cs
decisions:
  - "O-7 accept: the simultaneous mutual demotion of two Admins is accepted as a residual race. The self rule blocks the single-actor case, the last-Admin rule blocks emptying a board one request at a time, and a SuperAdmin can restore an Admin from the Platform page. Recorded for 100-SECURITY.md (already carries the O-7 row)."
  - "O-14 dm (X-1): DemoteFromAdmin sets Dungeon Master, not Player. DemoteToPlayer still sets Player."
  - "A person asking to be given the role they already hold is not refused as Self; only a change to a different role is. Staying an Admin is never a LastAdmin case."
  - "NotAMember keeps today's silent redirect for all four actions; the stale-page toast is 100-10's."
metrics:
  duration: "about 45 minutes"
status: complete
actuals:
  tokens: 14000
  tasks: 3
  commits: 3
---

# Phase 100 Plan 09: Role-change guard Summary

The four role buttons on the Admin Users page can no longer demote the acting Admin or leave a board without an Admin, whichever button a crafted request names, and the Domain makes that decision.

## What was built

**Domain.** `MemberChangeOutcome` (`Changed`, `NotAMember`, `Self`, `LastAdmin`). `UserService.ChangeMemberRoleAsync(groupId, actingUserId, targetUserId, newRole)` reads the target's current role (null gives `NotAMember`), refuses `Self` when the actor is the target and the role would differ, refuses `LastAdmin` when the target is an Admin who would stop being one and the board has at most one Admin, otherwise writes the role through the existing `SetGroupRoleAsync` repository call and gives `Changed`. The Admin count is `CountMembersWithRoleAsync`, passed through `GroupService` from one `CountAsync` in `GroupRepository` (the analog of `HasMembersAsync`). The Service project gained no EF package and no DbContext use.

**Controller.** `PromoteToAdmin`, `DemoteFromAdmin`, `PromoteToDM` and `DemoteToPlayer` are one-line actions into a private `ChangeRoleAsync(userId, newRole)`. It resolves the acting id from the `NameIdentifier` claim (a SuperAdmin who is not a member never equals a member's id, so is never "self"), calls the guard, and answers `Self` and `LastAdmin` with an Error toast via `RedirectWithError`. `Changed` and `NotAMember` redirect to Users as before. The two sentences are the approved `toastSelf` and `toastLastAdmin`, added to `AdminActionMessages` as `SelfRefused` and `LastAdminRefused`; neither names the Admin or Administrator role (`role-noun-check.py` prints ok).

**X-1 / O-14.** `DemoteFromAdmin` now asks for `GroupRole.DungeonMaster` (it set Player before); the pin fact now asserts Dungeon Master.

## Tests

- Unit: `UserServiceTests` cover every branch (self for each role pair, last Admin for both target roles with a different acting id, another Admin remaining, non-Admin targets without a count, staying Admin, not a member, a non-member actor never self); `GroupServiceTests` pins the count passthrough. 64 facts in the filtered UserService/GroupService run.
- Integration, enabled in `AdminRoleGuardTests` (were skipped as reproductions): self demotion through `DemoteFromAdmin`, `DemoteToPlayer` and `PromoteToDM`; the SuperAdmin on the only Admin through all three demoting actions (the crafted `PromoteToDM` case included). New pins: another Admin demotes a non-last Admin through `DemoteToPlayer` (Player) and the SuperAdmin still demotes a Dungeon Master.
- Still skipped in `AdminRoleGuardTests`, exactly the removal and stale-page facts, for 100-10: `DeleteUser_OnTheActorThemselves_IsRefused_AndTheirMembershipStays`, `DeleteUser_OnTheOnlyAdminOfTheBoard_IsRefused_AndTheMembershipStays`, `RoleAction_ForSomeoneWhoIsNotOnTheBoard_AnswersAnErrorToast_AndAddsNoMembership` (the theory over all four actions).
- `AdminPlatformConversionGuardTests.PhaseSourceFiles` gained the new and edited source files (`MemberChangeOutcome.cs`, `IGroupService.cs`, `GroupService.cs`, `IGroupRepository.cs`, `GroupRepository.cs`, `GroupServiceTests.cs`).
- Board-deletion facts (`PlatformBoardDeleteTests`, `BoardDeletion*`) pass unchanged; the three `BoardDeletionSqlServerTests` stay skipped without the SQL environment, as before.

## Verification results

- `dotnet test QuestBoard.UnitTests`: 1628 passed, 0 failed, 0 skipped (was 1613).
- `dotnet test QuestBoard.IntegrationTests`: 2838 passed, 0 failed, 23 skipped (was 2830 + 27 skipped; four facts enabled, four new facts).
- Task 1 and Task 2 filtered runs: green (unit 64; integration 29 passed + 6 skipped, then 67 passed + 6 skipped).
- `role-noun-check.py`: ok.
- `GroupService.cs` diff since the plan start is `4 insertions(+)`, the count passthrough only; `RemoveMemberAsync` is byte-identical.
- No view, sheet or script change in the repository (`git diff --stat` over `QuestBoard.Service/Views`, `Areas/Platform/Views`, `wwwroot` prints nothing).
- Live check on fixtures (port 8340, `after/100-09/live.json`): `guard-self-demotion` refused with the Error toast "You cannot change your own role or remove yourself from the board." and p100admin still Admin; `guard-last-admin` (the SuperAdmin demoting p100otheradmin, the only Admin of P100 Audit Campaign) refused with "A board must keep at least one member who can manage it. Give someone else that role first." and still Admin. Both `refused` and `unchanged` are true. Nothing needed restoring; `fixtures.mjs --check` passed under both User-Agents. The 8340 app was stopped afterwards.

## Deviations from Plan

**1. [Rule 3 - Blocking] The live proofs `guard-self-demotion` and `guard-last-admin` did not exist in `audit-proofs.mjs`.** The plan named them as existing. Added both to `~/qb-ui-review/phase-100/tools/audit-proofs.mjs` (scratch toolset, outside the repository) modelled on `self-demotion` and `last-admin-demotion`; each records `refused` and `unchanged` and restores the role through the real Promote buttons only if the guard failed.

**2. Task boundary.** The Task 1 commit already routes all four role actions through the shared controller helper, because the four actions are one code path; Task 2's commit therefore holds the remaining enabled facts, the new pins and the guard-list append rather than the controller change. The TDD red step was not run as a separate failing build: the new unit facts call an API that did not exist, so the red state is a compile failure; tests and implementation went into the same Task 1 commit.

## Notes for the next plan (100-10)

- `ChangeMemberRoleAsync` already returns `NotAMember`; 100-10 maps it to `AdminActionMessages.MemberStale` (the stale toast already exists) for the four role actions and un-skips the stale-page theory.
- The removal guard goes in `AdminController.DeleteUser`, not in `GroupService.RemoveMemberAsync` or the repository (board deletion relies on that chokepoint). `CountMembersWithRoleAsync` and `SelfRefused` / `LastAdminRefused` are reusable. `MemberChangeOutcome` already has `Self`, `LastAdmin`, `NotAMember`.
- `AdminActionMessages` sentences `SelfRefused` and `LastAdminRefused` read "change your own role or remove yourself" and "that role", so they already fit the removal refusal.
- Gate 1 item 6c is still unanswered; no sentence names the role.

## Known Stubs

None.

## Threat Flags

None. T-100-41 and T-100-42 are mitigated by the Domain guard (judged on the resulting role and the Admin count, never the action name) and the enabled facts plus the live check; T-100-43 (simultaneous mutual demotion) is accepted per O-7.

## Self-Check: PASSED

- Created and edited files exist, including `QuestBoard.Domain/Enums/MemberChangeOutcome.cs`; `grep -c CountMembersWithRoleAsync QuestBoard.Repository/GroupRepository.cs` prints 1 and `grep -c ChangeMemberRoleAsync QuestBoard.Service/Controllers/Admin/AdminController.cs` prints 1.
- Commits `c6aebb7e` (feat, Task 1) and `3c9b6977` (test, Task 2) exist on `milestone/v9-rolling-improvements`.
