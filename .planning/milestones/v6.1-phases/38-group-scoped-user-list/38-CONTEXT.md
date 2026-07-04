# Phase 38: Group-Scoped User List - Context

**Gathered:** 2026-07-03
**Status:** Ready for planning

<domain>
## Phase Boundary

`AdminController.Users()` currently calls `userService.GetAllAsync()` with no group filter, leaking every platform user into a group admin's Users management page. This phase scopes that read to the currently active group only, so a group admin (or SuperAdmin viewing the page) sees exclusively members of the active group — closing a cross-tenant PII leak. Per-user role display (Admin/DM/Player) must keep working exactly as it does today; this phase changes *which users appear*, not how each user's row is rendered.

Extended scope (locked during discussion, see Decisions): also close the matching write-path gap on the four role-change POST actions in the same controller, since leaving it open would make the read-path fix cosmetic.

</domain>

<decisions>
## Implementation Decisions

### Read-path scoping (USERS-01)
- **D-01:** Replace `userService.GetAllAsync()` in `AdminController.Users()` with a group-scoped query — mirror the exact manual-join pattern already used by `UserRepository.GetAllDungeonMasters`/`GetAllPlayers` (`DbSet.Where(u => DbContext.UserGroups.Any(ug => ug.UserId == u.Id && ug.GroupId == groupId.Value))`, no role filter — membership is any `UserGroups` row for that group). This is a manual join because `UserEntity` intentionally has no EF Core Global Query Filter (`QuestBoardContext.cs` — a query filter on `UserEntity` breaks ASP.NET Core Identity's login/password-reset/email-confirmation flows).
- **D-02:** Scoping applies uniformly — no SuperAdmin exception. SuperAdmin can pick any group via the group picker (even ones they don't belong to) and today sees all platform users on this page; after this fix they'll see only the active group's members here, same as a group Admin. This is a deliberate, accepted temporary trade-off: SuperAdmin loses the "see everyone" view on this specific page until Phase 40 ships the dedicated cross-group Platform Members page (next phase after 39). User confirmed this explicitly, aware of the PROJECT.md "no user-facing functionality removed" constraint — the removed behavior was an unintentional side effect of the bug being fixed, not a designed feature, so it doesn't trigger that constraint.
- **D-03:** Add a cross-group-isolation regression test (integration test asserting a group admin never sees a user from a different group) — follows this codebase's established convention of a dedicated regression test for every security-relevant fix (see PROJECT.md Key Decisions: CSRF regression test, secret-logging verification, etc.).

### Write-path hardening (found during code review, not in original USERS-01 wording)
- **D-04:** `PromoteToAdmin`, `DemoteFromAdmin`, `PromoteToDM`, and `DemoteToPlayer` (all in `AdminController.cs`) currently accept a raw `userId` with no server-side check that the target is a member of the active group — `UserRepository.SetGroupRoleAsync` silently creates a new `UserGroups` row for any userId submitted, meaning a crafted POST can currently grant/revoke a group role for a user outside the active group even after the read-path list is scoped (the UI would just stop rendering the button). **User chose to fix this in Phase 38**, not defer it — same controller, same page, same PR.
- **D-05:** Add a membership check before calling `SetGroupRoleAsync` in all four actions: if the target `userId` has no existing `UserGroups` row for the active `groupId`, **silently redirect to `Users()` with no role change and no error message** — mirrors the existing pattern in this controller (e.g., the `groupId == null` guard already just redirects with no banner). No user-facing message is needed since the UI never renders a Promote/Demote button for a non-member; this only guards against a directly-crafted request.

### Claude's Discretion
- Whether to add a brand-new repository method (e.g., `GetAllGroupMembers`) or reuse a union of the existing `GetAllDungeonMasters`/`GetAllPlayers` (since `GroupRole` has exactly 3 values: `Player`, `DungeonMaster`, `Admin`, and `SetGroupRoleAsync` always assigns one of them) — an implementation detail, not discussed with the user.
- Whether the membership-check helper for D-05 is a shared private method on `AdminController` or a new `IUserRepository`/`IUserService` method (e.g., `IsMemberOfGroupAsync`) — implementation detail.
- Whether to eliminate the existing per-user `GetGroupRoleByIdAsync` round-trip loop in `Users()` in favor of a single query returning `(User, GroupRole)` pairs — a pre-existing pattern, not required by this phase's success criteria, but worth considering if it falls out naturally from D-01's implementation.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Requirements & roadmap
- `.planning/REQUIREMENTS.md` — USERS-01 (the single requirement this phase satisfies)
- `.planning/ROADMAP.md` §"Phase 38: Group-Scoped User List" — goal, success criteria, dependency notes
- `.planning/STATE.md` §"Risk Flags for Planning" — original risk note calling out the manual-join requirement and regression test

No external specs/ADRs — requirements fully captured in decisions above.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `UserRepository.GetAllDungeonMasters` / `GetAllPlayers` (`QuestBoard.Repository/UserRepository.cs`) — exact manual-join pattern to mirror for D-01. Between them they already cover all 3 `GroupRole` values, so a union of both is a viable no-new-method implementation path.
- `AdminController`'s existing `groupId == null` guards (e.g. `Users()`, `PromoteToAdmin`, etc.) — the "just redirect, no error" pattern D-05 should follow.
- `QuestBoard.IntegrationTests/Controllers/AdminControllerIntegrationTests.cs` — existing integration test scaffolding/patterns (e.g. `ManageUsers_WhenNotAuthenticated_ShouldRedirectToLogin`) to extend for the D-03 regression test.

### Established Patterns
- `UserEntity` has **no** EF Core Global Query Filter by design (`QuestBoardContext.cs` line ~254) — group scoping for users must always be an explicit manual join, never assumed automatic like `QuestEntity`/`ShopItemEntity`.
- `GroupRole` enum (`QuestBoard.Domain/Enums/GroupRole.cs`): `Player = 0`, `DungeonMaster = 1`, `Admin = 2` — only 3 values, no "member with no role" state.
- `AdminHandler` (`QuestBoard.Service/Authorization/AdminHandler.cs`) already gives SuperAdmin a full bypass on the `AdminOnly` policy — confirms SuperAdmin can reach `AdminController.Users()` directly, and `GroupPickerController.Index` confirms SuperAdmin can pick any group regardless of their own membership.

### Integration Points
- `AdminController.cs` (`QuestBoard.Service/Controllers/Admin/`) — `Users()`, `PromoteToAdmin`, `DemoteFromAdmin`, `PromoteToDM`, `DemoteToPlayer` are the only actions touched by this phase.
- `IUserService`/`IUserRepository` — new or reused method(s) go here, following the existing `Domain → Repository` layering.

</code_context>

<specifics>
## Specific Ideas

No specific UI or copy requirements — this is a pure read/write-path data-scoping fix. The existing Users view and role-display logic are unchanged.

</specifics>

<deferred>
## Deferred Ideas

None raised during discussion — both selected gray areas stayed within this phase's extended scope.

</deferred>

---

*Phase: 38-Group-Scoped User List*
*Context gathered: 2026-07-03*
