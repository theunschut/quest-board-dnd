# Phase 60: Stop creating AspNetUserRoles entries for new users; role assignment has moved to UserGroups - Context

**Gathered:** 2026-07-06
**Status:** Ready for planning

<domain>
## Phase Boundary

New user creation must stop writing to `AspNetUserRoles`. Role assignment is now exclusively `UserGroups.GroupRole` (0=Player, 1=DungeonMaster, 2=Admin) — a per-group role model introduced in Phase 27 (v5.0). The one legitimate remaining use of `AspNetUserRoles` is the system-wide `SuperAdmin` Identity role, which is untouched by this phase.

This phase fixes the write-time bug (still creating stale rows) and removes the now-fully-dead read/write API surface for the retired per-group-via-Identity-role model. It does not touch existing historical bad data, and does not touch the `SuperAdmin` role mechanism.

</domain>

<decisions>
## Implementation Decisions

### Root cause (confirmed via code read, not guesswork)
- **D-01:** `IdentityService.CreateUserAsync` (`QuestBoard.Repository/IdentityService.cs:37-55`) calls `userManager.AddToRoleAsync(entity, "Player")` after creating every new Identity user. This is the only production code path that writes a non-SuperAdmin row to `AspNetUserRoles` today. It must be removed. Nothing in the Service layer reads `IsInRole("Player"/"DungeonMaster"/"Admin")` — confirmed via full-solution grep — so this write has been pure dead weight since Phase 27/34.3 migrated all inline role checks to `GetEffectiveGroupRoleAsync`/`UserGroups.GroupRole`.
- Role assignment for real (both new-account and existing-account) flows already happens correctly via `UserService.CreateOrAddToGroupAsync` → `SetGroupRoleAsync(userId, groupId, role)`, which writes `UserGroups.GroupRole`. That path is correct and must not change.

### Cleanup scope
- **D-02:** Remove the now-fully-dead role API surface entirely, not just the one `CreateUserAsync` call. Confirmed zero callers anywhere in `QuestBoard.Service` (controllers) or `QuestBoard.IntegrationTests` for:
  - `IUserService.AddToRoleAsync(User, string)` / `IIdentityService.AddToRoleAsync(int, string)`
  - `IUserService.RemoveFromRoleAsync(User, string)` / `IIdentityService.RemoveFromRoleAsync(int, string)`
  - `IUserService.IsInRoleAsync(User, string)` / `IIdentityService.IsInRoleAsync(int, string)` — **note:** do NOT remove `IIdentityService.IsInRoleAsync(ClaimsPrincipal, string)` if it's still wired to anything using `SuperAdmin`; verify at research/plan time whether this overload has any caller before deleting it specifically (the `User`/`int`-keyed overloads are confirmed dead; the `ClaimsPrincipal` overload needs its own caller check since it wasn't part of this discussion's grep scope).
  - `IUserService.GetRolesAsync(User)` / `IIdentityService.GetRolesAsync(int)`
  - Delete both the interface members and their implementations (`UserService.cs`, `IdentityService.cs`, `IUserService.cs`, `IIdentityService.cs`).
  - Do not remove `AddToRoleAsync`/role-checking machinery related to `SuperAdmin` — that role is real, system-wide, and unrelated to this per-group-role cleanup.

### Existing data
- **D-03:** Leave existing stale `AspNetUserRoles` rows alone. Do **not** write a data-cleanup migration. **Correction from initial discussion:** the 2025-era "Player"/"DungeonMaster"/"Admin" rows were NOT left uncleaned — `20260630055221_AddGroupSchema` (Phase 27, Step 9-10) already backfilled `UserGroups` from `AspNetUserRoles` for every user that existed at the time, then explicitly `DELETE`d all Player/DungeonMaster/Admin rows from `AspNetUserRoles` (see migration comment: "UserGroups now holds per-group roles; AspNetUserRoles is reserved for SuperAdmin only"). So the actual stale data today is much narrower than originally described: only the "Player" rows re-added by every user created between Phase 27 shipping (2026-06-30) and this fix landing — roughly a week's worth of accounts, not a multi-year backlog. The decision stands unchanged (no cleanup migration this phase, low volume, harmless/unread), just the rationale is corrected here so research/planning don't cite the wrong migration or overstate the cleanup's scope.

### Test helper alignment
- **D-04:** `QuestBoard.IntegrationTests/Helpers/AuthenticationHelper.cs` should be aligned to match the production fix:
  - `CreateTestUserAsync` (lines 42-47): remove the "Assign default Player role" block (`roleManager.RoleExistsAsync("Player")` / `CreateAsync` / `userManager.AddToRoleAsync(user, "Player")`).
  - `CreateAuthenticatedClientWithUserAsync` (lines 98-117): the `roles != null && roles.Length > 0` loop calls `userManager.AddToRoleAsync(userFromDb, role)` for each requested role (`"SuperAdmin"`, `"Admin"`, `"DungeonMaster"`). **Critical constraint: the `SuperAdmin` case must keep writing to `AspNetUserRoles`** — `User.IsInRole("SuperAdmin")` is a real, live production check and the Test auth scheme's role claims are sourced from `AspNetUserRoles` via `GetRolesAsync` at the end of this method (lines 153-160), round-tripped through the `Test` auth header. Only stop this loop from creating `AspNetUserRoles` rows for `"Admin"`/`"DungeonMaster"`/`"Player"` (the retired per-group roles) — `SuperAdmin` seeding must be preserved exactly as-is.
  - The parallel `UserGroups` seeding block (lines 128-150, mapping `roles` → `GroupRole` enum) already exists and is correct — no change needed there.
- **Known risk to verify during planning/execution, not a vision decision:** `CreateAuthenticatedClientAsync` (lines 52-86, a different/simpler helper than `CreateAuthenticatedClientWithUserAsync`) has `roles = (await userManager.GetRolesAsync(userFromDb)).ToArray(); roles ??= ["Player"];` — this fallback only triggers on `null`, not on an empty array. Once the Player-role seed is removed from `CreateTestUserAsync`, `GetRolesAsync` will return `[]` (not null), so this method's default behavior silently changes from "Player role claim present" to "zero role claims." This is used across ~215 call sites in the integration test suite (mostly via the sibling `CreateAuthenticatedClientWithUserAsync`, but `CreateAuthenticatedClientAsync` itself has its own callers too — check via grep at plan time). Since no production authorization logic checks `IsInRole("Player")`, this is expected to be safe, but the full test suite (`dotnet test`) must be run and any failures traced back to this specific fallback before considering the phase done.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Root cause / files to change
- `QuestBoard.Repository/IdentityService.cs` — `CreateUserAsync` (lines 37-55) has the bug; also implements the dead `AddToRoleAsync`/`RemoveFromRoleAsync`/`IsInRoleAsync`/`GetRolesAsync` methods slated for removal (D-02)
- `QuestBoard.Domain/Services/UserService.cs` — thin pass-through wrappers for the same dead methods (lines 13-16, 90-93, 104-113, 119-122)
- `QuestBoard.Domain/Interfaces/IUserService.cs` — interface declarations to remove
- `QuestBoard.Domain/Interfaces/IIdentityService.cs` — interface declarations to remove
- `QuestBoard.IntegrationTests/Helpers/AuthenticationHelper.cs` — test helper alignment (D-04), lines 42-47 and 98-117

### Correct pattern already in place (do not modify, use as reference)
- `QuestBoard.Domain/Services/UserService.cs` — `CreateOrAddToGroupAsync` (lines 161-232) and `SetGroupRoleAsync` (lines 155-158): the correct, already-working role-assignment path via `UserGroups.GroupRole`
- `QuestBoard.Repository/Entities/UserGroupEntity.cs` — the `GroupRole` int column (0=Player, 1=DungeonMaster, 2=Admin) that is the sole source of truth for per-group roles
- `QuestBoard.Service/Controllers/Admin/AdminController.cs` (lines 57-103) — `PromoteToAdmin`/`DemoteFromAdmin`/`PromoteToDM`/`DemoteToPlayer` — reference for how role changes correctly go through `SetGroupRoleAsync` only

### Historical/background context
- `.planning/codebase/CONCERNS.md` (search "Group role authorization regression") — documents Phase 27's original move of per-group roles out of `AspNetUserRoles` into `UserGroups.GroupRole`, and Phase 34.3's fix of the read-side inline `IsInRole` checks. This phase closes the one remaining write-side gap that 34.3 didn't touch.
- `QuestBoard.Repository/Migrations/20260630055221_AddGroupSchema.cs` (Step 9-10, and its class-level deployment-constraint comment) — **the actually-relevant migration for "existing data" (D-03).** Backfills `UserGroups` from `AspNetUserRoles` for every pre-existing user, then `DELETE`s all Player/DungeonMaster/Admin rows from `AspNetUserRoles`. This is why there is no multi-year backlog of stale rows — only ones created since this migration shipped (2026-06-30) via the still-buggy `CreateUserAsync` write path this phase fixes.
- `QuestBoard.Repository/Migrations/20250704211037_ConvertIsDungeonMasterToRoles.cs` — the original 2025 migration that first seeded the `Player`/`DungeonMaster`/`Admin` `AspNetRoles` *definitions* (the `AspNetRoles` catalog rows, id 1/2/3) and assigned initial `AspNetUserRoles` rows from the old `IsDungeonMaster` boolean. The role *definitions* it created still exist (unused, D-02/D-03 leave them); the *assignment* rows it created were later deleted by `AddGroupSchema` above, not left uncleaned.
- PROJECT.md Key Decisions table (search "Group role authorization regression fix") — v5.0 Phase 34.3 entry, confirms the read-side migration and the exact mechanism (`GetEffectiveGroupRoleAsync`) this phase's write-side fix must stay consistent with.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `UserService.SetGroupRoleAsync` / `GetGroupRoleByIdAsync` — already the correct, sole mechanism for reading/writing per-group roles; no new code needed here, just removal of the dead parallel path.

### Established Patterns
- SuperAdmin is checked via `User.IsInRole("SuperAdmin")` directly (a real ASP.NET Core Identity role, deliberately kept separate from the per-group `GroupRole` model per the PROJECT.md decision "SuperAdminOnly policy uses RequireRole('SuperAdmin') — no custom handler"). This phase must not disturb that.
- Per-group role reads always go through `GetEffectiveGroupRoleAsync(User, groupId)` (SuperAdmin short-circuit built in) or `GetGroupRoleByIdAsync(userId, groupId)` for a specific target user — never `IsInRole` for Player/DungeonMaster/Admin.

### Integration Points
- `IdentityService.CreateUserAsync` is called only via `UserService.CreateAsync`, which is called only from `UserService.CreateOrAddToGroupAsync` (the new-account branch). No other callers exist — the fix is fully contained to this one call chain.

</code_context>

<specifics>
## Specific Ideas

No specific UI/UX requirements — this is a pure backend correctness fix with no user-facing behavior change. The "specific idea" driving this phase is the user's own diagnosis: "a new user still gets an AspNetUserRoles entry. This has been moved to UserGroups and should not use AspNetUserRoles anymore" — confirmed accurate via code trace (D-01).

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope. The existing-data cleanup (purging stale `AspNetUserRoles` rows) and removing the `AspNetRoles` "Player"/"DungeonMaster"/"Admin" role definitions themselves were both explicitly considered and declined (D-03) rather than deferred to a future phase — no action item was created for them.

</deferred>

---

*Phase: 60-stop-creating-aspnetuserroles-entries-for-new-users-role-ass*
*Context gathered: 2026-07-06*
