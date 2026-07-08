---
phase: 38-group-scoped-user-list
audited: 2026-07-04
asvs_level: 1
block_on: open
threats_total: 7
threats_closed: 7
threats_open: 0
---

# Phase 38: Security Audit — Group-Scoped User List

## Scope

Verifies every threat declared in `.planning/phases/38-group-scoped-user-list/38-01-PLAN.md`'s `<threat_model>` block, plus the CR-01 finding raised in `38-REVIEW.md` and closed per `38-REVIEW-FIX.md`. Evidence is grep/read verification against the actual implementation, plus a live `dotnet build` and `dotnet test` run — not documentation or intent.

## Threat Verification

| Threat ID | Category | Disposition | Verification Method | Evidence | Result |
|-----------|----------|-------------|----------------------|----------|--------|
| T-38-01 | Information Disclosure | mitigate | grep for mitigation pattern | `AdminController.cs:29` — `var allUsers = await userService.GetAllGroupMembersAsync(groupId.Value);` (no more `GetAllAsync()` in `Users()`). `UserRepository.cs:51-59` — `GetAllGroupMembers` filters `DbContext.UserGroups.Any(ug => ug.UserId == u.Id && ug.GroupId == groupId)` with no `GroupRole` filter. Full Repository→Domain→Service chain confirmed: `IUserRepository.cs`, `UserRepository.cs:51`, `IUserService.cs`, `UserService.cs:55-58`. | CLOSED |
| T-38-02 | Elevation of Privilege / Tampering | mitigate | grep for guard pattern before mutating call | All four actions (`PromoteToAdmin` :62-63, `DemoteFromAdmin` :75-76, `PromoteToDM` :88-89, `DemoteToPlayer` :101-102) contain `GetGroupRoleByIdAsync(userId, groupId.Value)` → `if (targetRole == null) return RedirectToAction(nameof(Users));` placed immediately before their respective `SetGroupRoleAsync` call. Confirmed by direct read of `AdminController.cs`. | CLOSED |
| T-38-03 | Information Disclosure (regression) | mitigate | test execution | `Users_WhenAdmin_DoesNotShowUsersFromOtherGroups` present at `AdminControllerIntegrationTests.cs:281`, asserts `content.Should().Contain(inGroupMarker)` (positive control) and `content.Should().NotContain(outOfGroupMarker)` (negative isolation assertion). Executed live: `dotnet test --filter AdminControllerIntegrationTests` → 24/24 passed, including this test. | CLOSED |
| T-38-SA | Information Disclosure (accepted trade-off) | accept | accepted-risk log entry | Logged below in Accepted Risks Log. `Users()` reads `activeGroupContext.ActiveGroupId` uniformly with no `IsInRole("SuperAdmin")` branch (confirmed absent in `AdminController.cs:23-53`) — matches the declared trade-off exactly. | CLOSED |
| T-38-SC | Tampering (supply chain) | accept | accepted-risk log entry + dependency diff check | Logged below in Accepted Risks Log. No `.csproj` files were modified by this phase (`38-01-SUMMARY.md` `files_modified` lists only `.cs` files); no new `<PackageReference>` entries introduced. | CLOSED |
| CR-01 | Elevation of Privilege / Tampering (code-review finding) | mitigate | grep for guard pattern before mutating/PII-exposing call in all 5 actions | Guard present and correctly ordered (before the target-user-touching call) in all five actions: `EditUser` GET `:165-166` (before `GetByIdAsync` at :168), `EditUser` POST `:194-195` (before `GetByIdAsync` at :197), `ResetPassword` GET `:254-255` (before `GetByIdAsync` at :257), `ResetPassword` POST `:281-282` (before `GetByIdAsync`/`ResetPasswordAsync` at :284/:290), `DeleteUser` `:314-315` (before `GetByIdAsync`/`RemoveAsync` at :317/:323), `SendConfirmationEmail` `:343-344` (before `GetByIdAsync` at :346 — guard placed after the pre-existing rate-limit lease at :333, as the fix report states, which does not touch or expose the target user so ordering is safe). `grep -c GetGroupRoleByIdAsync AdminController.cs` = 11 (1 Users() loop + 4 T-38-02 guards + 6 CR-01 guard sites across 5 actions incl. EditUser/ResetPassword's two verbs each). Companion regression test `RoleChangeActions_WhenTargetUserOutOfGroup_ShouldNotChangeMembership` (`:323`, Theory over all 4 role-change endpoints) also passes. | CLOSED |
| WR-01 (review-flagged gap, tracked under T-38-02's disposition) | Missing regression test for write-path guard | mitigate | test execution | `RoleChangeActions_WhenTargetUserOutOfGroup_ShouldNotChangeMembership` at `AdminControllerIntegrationTests.cs:323`, `[Theory]` over `PromoteToAdmin`/`DemoteFromAdmin`/`PromoteToDM`/`DemoteToPlayer`, asserts the target's `UserGroups` row is untouched (`GroupId == 2`, `GroupRole == Player`) after a crafted POST from a group-1 admin. Executed live as part of the 24/24 passing suite. | CLOSED |

## Live Verification Commands Run

```
dotnet build QuestBoard.slnx
  -> Build succeeded. 0 Warning(s), 0 Error(s).

dotnet test QuestBoard.IntegrationTests/QuestBoard.IntegrationTests.csproj --filter "FullyQualifiedName~AdminControllerIntegrationTests"
  -> Passed! - Failed: 0, Passed: 24, Skipped: 0, Total: 24, Duration: 5s
```

Both the T-38-03 read-path test and the WR-01 write-path theory test (4 cases) are included in this run and pass.

## Accepted Risks Log

### T-38-SA — SuperAdmin loses cross-group visibility on `/Admin/Users`
- **Category:** Information Disclosure (accepted trade-off, i.e. removal of an unintentional over-exposure, not introduction of new risk)
- **Disposition:** accept
- **Rationale:** Scoping `Users()` uniformly (no `SuperAdmin` exception) closes the T-38-01 cross-tenant leak for all roles including SuperAdmin. This means SuperAdmin can no longer see every platform user on this specific page — a capability that existed only as an unintentional side effect of the pre-fix bug, not a designed feature. User confirmed this trade-off explicitly during planning (`38-01-PLAN.md` T-38-SA row, `38-CONTEXT.md` D-02).
- **Compensating control:** SuperAdmin can still reach any group's member list by switching the active group via the group picker (`GroupPicker` controller, unaffected by this phase).
- **Residual risk:** Low. No cross-tenant data exposure; only a workflow-convenience reduction for one specific role, remediated by a dedicated Platform Members page planned for Phase 40.
- **Owner:** Confirmed by user during Phase 38 planning.

### T-38-SC — No supply-chain review needed for this phase
- **Category:** Tampering (supply chain)
- **Disposition:** accept
- **Rationale:** This phase's diff touches only existing first-party `.cs` files (`UserRepository.cs`, `IUserRepository.cs`, `IUserService.cs`, `UserService.cs`, `AdminController.cs`, `AdminControllerIntegrationTests.cs`). No `.csproj` changes, no new `<PackageReference>` entries, no npm/pip/cargo installs.
- **Compensating control:** N/A — no new dependency surface to compensate for.
- **Residual risk:** None introduced by this phase.
- **Owner:** N/A (no action needed; re-evaluate if a future phase touching this code adds a dependency).

## Unregistered Flags

None. `38-01-SUMMARY.md` contains no `## Threat Flags` section, so the executor did not self-report any new attack surface beyond the plan's threat model. The one gap that did surface post-implementation (CR-01, plus its companion missing-test WR-01) was caught by code review rather than self-flagged, and is fully accounted for above — both closed with evidence, not merely claimed by the fix report.

## Notes for Future Phases

- Phase 40 (Platform Members Page Redesign) is the designated remediation for the T-38-SA accepted trade-off — SuperAdmin's cross-group visibility should be restored there via a dedicated, explicitly-scoped page rather than by re-opening `Users()`.
- The `GetGroupRoleByIdAsync`-as-membership-check pattern (null return = "not a member") is now used in 11 call sites in `AdminController.cs`. Any new action added to this controller that resolves a `userId`/`id` route or form parameter into a specific user must apply the same guard before the first line that reads or mutates that user — this phase's own CR-01 finding demonstrates that omitting it on even a subset of actions in the same controller is a live, exploitable gap.
