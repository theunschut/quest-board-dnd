# Phase 29: SuperAdmin Role & Management Area - Context

**Gathered:** 2026-06-30
**Status:** Ready for planning

<domain>
## Phase Boundary

Add the SuperAdmin Identity role (migration-seeded), update `AdminHandler` and `DungeonMasterHandler` to read `UserGroups.GroupRole` for the active group (fixing the auth system broken by Phase 27 clearing `AspNetUserRoles`), extend `IUserService` with group-role queries, fix `AdminController` promote/demote to write to `UserGroups`, and create the `/platform` MVC Area with a functional admin panel for SuperAdmin group management (MGMT-01–06).

This phase does NOT include: group-picker UX, navigation between quest board and platform, self-registration removal, or group-admin user creation. Those belong to Phase 30.

</domain>

<decisions>
## Implementation Decisions

### Auth Handlers — Replacing Identity role checks with UserGroups

- **D-01:** `AdminHandler` and `DungeonMasterHandler` each gain a second constructor parameter `IActiveGroupContext` alongside the existing `IUserService`. Both are already registered as Scoped in DI; no registration change needed.

- **D-02:** SuperAdmin bypass: each handler checks `context.User.IsInRole("SuperAdmin")` first. If true → `context.Succeed(requirement)` immediately and return. This uses `ClaimsPrincipal` directly — no property on `IActiveGroupContext`.

- **D-03:** Null group guard: if `activeGroupContext.ActiveGroupId` is `null` after the SuperAdmin check → `context.Fail()`. Regular users without an active group session cannot pass any auth check. (After Phase 30's group-picker, null will only occur for SuperAdmin in production.)

- **D-04:** Group role check: call `await userService.GetGroupRoleAsync(context.User, activeGroupId.Value)`.
  - `AdminHandler`: succeed if result is `GroupRole.Admin`.
  - `DungeonMasterHandler`: succeed if result is `GroupRole.Admin` or `GroupRole.DungeonMaster`.

- **D-05:** `IActiveGroupContext` is NOT changed. It stays `int? ActiveGroupId { get; }` — no `IsSuperAdmin` property. The Phase 28 deferred item (add `IsSuperAdmin`) is explicitly cancelled. `HasQueryFilter` predicate stays `context.ActiveGroupId == null || e.GroupId == context.ActiveGroupId` (null = see all). This is safe because the full v5.0 milestone merges as one PR before production deployment — Phase 30's group-picker enforcement lands before any real user hits the null case.

### IUserService Extensions

- **D-06:** Add `Task<GroupRole?> GetGroupRoleAsync(ClaimsPrincipal user, int groupId)` to `IUserService` (Domain interface) and implement in `UserService` (Service layer). Queries `UserGroups` where `UserId == userId && GroupId == groupId`. Returns `null` if no membership row found.

- **D-07:** Update `GetAllPlayersAsync` to query `UserGroups.GroupRole == GroupRole.Player` for `IActiveGroupContext.ActiveGroupId` instead of reading from `AspNetUserRoles`. This fixes the broken `/players` page that returned empty after Phase 27 cleared Identity role assignments.

- **D-08:** Update `GetAllDungeonMastersAsync` to query `UserGroups.GroupRole` is `DungeonMaster` or `Admin` for the active group. Same fix for DM-listing views.

### AdminController Promote/Demote Fix

- **D-09:** `PromoteToAdmin`, `DemoteFromAdmin`, `PromoteToDM`, `DemoteToPlayer` currently call `userService.AddToRoleAsync / RemoveFromRoleAsync` (modifying `AspNetUserRoles`, which is now empty for these roles). Update them to modify `UserGroups.GroupRole` for `IActiveGroupContext.ActiveGroupId`. `AdminController` already depends on `IActiveGroupContext` from Phase 28 — no new constructor parameter needed.

### SuperAdmin Identity Role Seeding

- **D-10:** SuperAdmin role created via EF Core migration `InsertData` into `AspNetRoles`: `Id = 4, Name = "SuperAdmin", NormalizedName = "SUPERADMIN"`. Consistent with how Player/DM/Admin roles were seeded in the `ConvertIsDungeonMasterToRoles` migration.

- **D-11:** First SuperAdmin user assignment is a **manual post-deploy step** — document a one-time SQL INSERT into `AspNetUserRoles (UserId, RoleId)` in deployment docs (add to the Phase 27–29 co-deployment constraint section in `STATE.md`). No startup automation needed.

### /platform MVC Area

- **D-12:** New MVC Area named `Platform`, routed at `/platform`. First MVC Area in the project — directory structure: `QuestBoard.Service/Areas/Platform/Controllers/` and `QuestBoard.Service/Areas/Platform/Views/`. Protected by a `SuperAdminOnly` authorization policy (AUTH-05: `[Authorize(Policy = "SuperAdminOnly")]` on the area controller).

- **D-13:** Dedicated `_Layout.Platform.cshtml` in `Areas/Platform/Views/Shared/`. Clean layout: QuestBoard logo, logged-in user name, logout button, "Back to quest board" link. No quest board navigation (no quests/shop/character links). Distinct from the main `_Layout.cshtml`.

- **D-14:** Visual style: functional admin panel using the modern-card pattern (mandatory per CLAUDE.md). Tables for group and member lists. Standard form-based create/edit/delete (no modals required). Same approach as existing `Admin > Users` and `Admin > Quests` pages — no polish extras (charts, badges, animations) in this phase.

- **D-15:** Pages in scope (matching MGMT-01–06):
  - Groups index — list all groups with member counts
  - Create group — name input form
  - Edit group — rename form
  - Delete group — confirmation (only if group has zero members)
  - Group detail / members — list members, add existing user (with GroupRole picker), remove user

### Claude's Discretion

- Exact `SuperAdminOnly` policy registration in `Program.cs` (standard `AddAuthorization` + `AddPolicy`)
- `GroupController` vs. `PlatformController` naming inside the Area
- Whether group detail and member management live on one controller or two
- Exact column layout and button placement in the platform views (follow CLAUDE.md card pattern)

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Requirements & Scope
- `.planning/REQUIREMENTS.md` §Authorization (AUTH-01–AUTH-05) — SuperAdmin role, handler updates, SuperAdminOnly policy
- `.planning/REQUIREMENTS.md` §Management Area (MGMT-01–MGMT-06) — /platform area functional requirements
- `.planning/ROADMAP.md` §Phase 29 — phase goal, success criteria, dependency on Phase 28

### Prior Phase Decisions
- `.planning/phases/28-tenant-isolation/28-CONTEXT.md` — IActiveGroupContext interface design (D-01/D-02), HasQueryFilter predicate (D-03/D-05), DI registration patterns, MutableGroupContext test stub
- `.planning/STATE.md` §Key Architectural Decisions (v5.0) — locked: per-group roles in UserGroups.GroupRole; AspNetUserRoles for SuperAdmin only; /platform route; AdminHandler/DungeonMasterHandler bypass pattern

### Key Files to Modify
- `QuestBoard.Service/Authorization/AdminHandler.cs` — add IActiveGroupContext constructor param, replace IsInRoleAsync with D-02/D-03/D-04 logic
- `QuestBoard.Service/Authorization/DungeonMasterHandler.cs` — same pattern
- `QuestBoard.Domain/Interfaces/IUserService.cs` — add GetGroupRoleAsync (D-06), update GetAllPlayersAsync/GetAllDungeonMastersAsync signatures if needed
- `QuestBoard.Service/Controllers/Admin/AdminController.cs` — fix PromoteToAdmin, DemoteFromAdmin, PromoteToDM, DemoteToPlayer (D-09)
- `QuestBoard.Repository/Migrations/20260630055221_AddGroupSchema.cs` — reference for migration SQL patterns and role-seeding style

### Reference Views
- `QuestBoard.Service/Views/Admin/Users.cshtml` — table-based admin view pattern to follow for platform pages
- `QuestBoard.Service/Views/Shared/_Layout.cshtml` — reference for what to strip down into _Layout.Platform.cshtml

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `UserGroupEntity` and `GroupEntity` — exist from Phase 27; no new schema needed. `QuestBoardContext` already has their `DbSet`s configured.
- `IActiveGroupContext` — already registered as Scoped in DI and injected into `QuestBoardContext`. Handlers can take it as a constructor parameter with no registration change.
- `BaseRepository<T>` — `GroupRepository` can extend this for standard group CRUD.
- `AdminController` — already injects `IActiveGroupContext` (Phase 28 wired it); D-09 fix requires no new constructor parameter.

### Established Patterns
- **Auth handler pattern:** Primary constructor `(IDependency dep) : AuthorizationHandler<TRequirement>`. See existing `AdminHandler` and `DungeonMasterHandler`.
- **Service method on IUserService:** Scoped service with `UserManager<UserEntity>` injection. `GetGroupRoleAsync` follows the same pattern as existing `IsInRoleAsync`.
- **Role seeding via migration:** `migrationBuilder.InsertData(table: "AspNetRoles", columns: [...], values: [...])` — see `ConvertIsDungeonMasterToRoles` migration for exact format (including `ConcurrencyStamp = Guid.NewGuid().ToString()`).
- **MVC Area registration:** `Program.cs` must call `app.MapControllerRoute` with an area route, and `AddControllersWithViews` automatically discovers areas under `Areas/`.
- **modern-card views:** `<div class="card-header modern-card-header">` + `<i class="fas ...">` + `<hr>` before button section. See any existing admin view.

### Integration Points
- `Program.cs` — add `SuperAdminOnly` policy in `AddAuthorization`, add area route in `UseEndpoints`, register `GroupRepository` / `IGroupService` in DI
- `AdminHandler` / `DungeonMasterHandler` — must re-register in DI after constructor change (they are registered as `services.AddScoped<IAuthorizationHandler, AdminHandler>()` — check `Program.cs`)
- `UserService` — implements `IUserService`; `GetGroupRoleAsync`, `GetAllPlayersAsync`, `GetAllDungeonMastersAsync` all go here

### Known Landmines
- `AspNetUserRoles` contains NO Player/DM/Admin entries after Phase 27 — any Identity role check for those roles returns false. All IUserService role-query methods must move to UserGroups.
- MVC Areas require `[Area("Platform")]` attribute on the area controller AND a matching area route in `Program.cs`. Missing either causes 404s.
- `_ViewImports.cshtml` in the area's `Views/` folder needs to be created (or the area's views won't get tag helpers and `@using` directives).
- `GroupEntity.Name` has a unique DB index (Phase 27 D-08) — create group must handle `DbUpdateException` for duplicate names gracefully.

</code_context>

<specifics>
## Specific Ideas

- Handler logic order (per discussion): (1) SuperAdmin short-circuit via `context.User.IsInRole("SuperAdmin")`, (2) null group guard, (3) GroupRole lookup — in that exact order.
- `_Layout.Platform.cshtml` contents: logo, `@User.Identity.Name`, logout link, "← Back to quest board" link. Nothing else in the nav.
- The `/players` page fix (D-07) is part of this phase — mentioned explicitly because the broken empty state was noticed during Phase 28 human verification and attributed to the same root cause.

</specifics>

<deferred>
## Deferred Ideas

- Platform visual polish (member count charts, status badges, confirmation animations) — future milestone when the platform area is used regularly
- SuperAdmin link in the main quest board nav — Phase 30 handles navigation between quest board and platform via the group-picker
- `IsSuperAdmin` on `IActiveGroupContext` — explicitly decided NOT to add; cancelled from Phase 28 deferred
- Group admin user creation and role promotion/demotion within a group — Phase 30 (MGMT-07, MGMT-08)

</deferred>

---

*Phase: 29-superadmin-role-and-management-area*
*Context gathered: 2026-06-30*
