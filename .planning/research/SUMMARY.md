# Project Research Summary

**Project:** D&D Quest Board v5.0 Multi-Tenancy
**Domain:** Adding multi-group tenancy to an existing ASP.NET Core 10 MVC + EF Core + SQL Server application
**Researched:** 2026-06-29
**Confidence:** MEDIUM

## Executive Summary

The v5.0 milestone transforms the Quest Board from a single-tenant EuphoriaInn app into a generic multi-group platform. The recommended approach is a shared-database, shared-schema multi-tenancy model using EF Core 10 Global Query Filters. Every required capability is achievable with packages already in the project (EF Core 10.0.9, ASP.NET Core Identity, ASP.NET Core Areas); the only new wiring is a single AddHttpContextAccessor() call and an optional dev-time rename tool. The milestone naturally splits into two workstreams: a pure namespace rename (EuphoriaInn to QuestBoard) that touches ~200 files but changes zero behavior, followed by the multi-tenancy schema and logic work.

The key architectural decision is that IActiveGroupContext must be defined in the Domain layer, not the Service layer, because the Repository-layer QuestBoardContext must consume it and Repository already depends on Domain. The implementation (ActiveGroupContextService) lives in the Service layer and reads from ASP.NET Core Session (already configured with a 24-hour idle timeout). This dependency inversion is the central seam of the entire feature and must be established before any other multi-tenancy work begins.

The highest risks are operational rather than architectural: the global query filter silently returns empty result sets if the active group ID is null (blank pages, not crashes); the integration test suite (191 tests) will break wholesale the moment HasQueryFilter is added without a matching stub IActiveGroupContext registered in the test factory; and removing self-registration without verifying admin role integrity in production could lock out all administrators. All three risks are preventable with disciplined phase ordering and a pre-deploy DB snapshot check.

## Key Findings

### Recommended Stack

No new NuGet packages are required. Every multi-tenancy capability uses technology already present: EF Core 10.0.9 (named HasQueryFilter, migrations, junction table modeling), ASP.NET Core Identity (new SuperAdmin role, policy handler), ASP.NET Core Session (active-group persistence), and ASP.NET Core Areas (SuperAdmin management route). Named filters in EF Core 10 (HasQueryFilter(GroupFilter, ...)) allow selective bypass (IgnoreQueryFilters([GroupFilter])) without disabling any future soft-delete or other named filters.

The only tooling addition is ModernRonin.ProjectRenamer as an optional global CLI tool for the namespace rename. Its .slnx support is unverified, so a manual find-and-replace fallback must be planned. Third-party multi-tenancy frameworks (Finbuckle.MultiTenant, SaasKit, Abp.io) are explicitly ruled out as they target connection-string-per-tenant or database-per-tenant scenarios and cannot be added to an existing app without significant rework.

**Core technologies:**
- EF Core 10.0.9 HasQueryFilter: per-group data isolation, already present, named filters available
- IHttpContextAccessor / ASP.NET Core Session: active-group context resolution, already configured with 24h idle timeout
- ASP.NET Core Identity RoleManager: SuperAdmin role seeding, same pattern as existing Admin/DungeonMaster roles
- ASP.NET Core Areas: SuperAdmin management route isolation, zero packages, built-in MVC feature
- ModernRonin.ProjectRenamer (global tool only): namespace rename automation, LOW confidence on .slnx support

### Expected Features

**Must have (table stakes, all required for v5.0):**
- Group entity (GroupEntity: Id, Name, Slug, IsActive, CreatedAt): foundation for all other features
- GroupId FK on all content entities (Quest, ShopItem, Character, UserTransaction, TradeItem) + EF Core Global Query Filters: the isolation guarantee
- UserGroupEntity junction table (UserId, GroupId, IsAdmin) + seed all existing users into EuphoriaInn group: membership model
- IActiveGroupContext scoped service reading from Session: the per-request group resolver
- Group picker page (redirect after login if user is in 2+ groups) + group switcher in navbar: active-group UX
- SuperAdmin role + SuperAdminOnly policy + MVC Area at /groups for platform management: system administration
- Admin-only user creation in AdminController + self-registration removed from AccountController: closed-group security
- Data migration seeding GroupId = 1 for all existing rows: backward compatibility

**Should have (differentiators, defer to v5.x):**
- Per-group role scoping (role stored in UserGroupEntity, not global Identity roles): only needed when a user is DM in one group and Player in another
- Group invite link (time-limited signup URL): only needed when admin-creates-password is reported as inconvenient
- Group branding (description, avatar on GroupEntity): only meaningful once a second group exists

**Defer (confirmed anti-features for this scale):**
- Separate database per group: over-engineering for 1-3 groups at 17 users
- JWT-based active tenant claims: unnecessary for a cookie-auth MVC app
- Schema-per-group isolation: EF Core migrations do not support this pattern cleanly
- Real-time group presence: requires SignalR not in the stack
- Self-service group creation by non-SuperAdmin: SuperAdmin must be the gatekeeper

### Architecture Approach

The existing three-layer clean architecture (Service to Domain to Repository, strict one-way dependency) is extended with new components at each layer. IActiveGroupContext must be defined in Domain because QuestBoardContext in Repository must consume it and Repository already depends on Domain. The implementation lives in Service and reads from IHttpContextAccessor / Session. QuestBoardContext captures the resolved group ID into a readonly field at construction time (not a lazy property call in the HasQueryFilter lambda), which is the officially documented EF Core multitenancy pattern and avoids stale-scope issues. The SuperAdmin bypass is handled by a conditional in OnModelCreating (if (!_isSuperAdmin)) rather than scattered IgnoreQueryFilters() calls throughout repositories.

**Major components:**
1. IActiveGroupContext (Domain/Interfaces): exposes int? ActiveGroupId and bool IsSuperAdmin; null = cross-group / SuperAdmin view
2. ActiveGroupContextService (Service): reads Session ActiveGroupId key; returns null when HttpContext is absent (Hangfire job context)
3. QuestBoardContext (Repository, modified): accepts IActiveGroupContext in constructor; applies HasQueryFilter(GroupFilter, ...) on 5 entity types; skips filters entirely for SuperAdmin
4. GroupEntity + UserGroupEntity (Repository/Entities, new): group table and junction table
5. GroupsManagementController (Service/Areas/Groups, new): [Authorize(SuperAdminOnly)] MVC Area controller at /groups
6. GroupService / IGroupService (Domain, new): group CRUD and member management

### Critical Pitfalls

1. **Global filter returns empty rows when GroupId is null**: if ActiveGroupId is null for a user-facing request, WHERE GroupId = NULL matches nothing and pages appear blank rather than crashing. Prevention: throw InvalidOperationException for unresolved group context on user-facing requests; use null-means-SuperAdmin only in explicitly SuperAdmin-scoped operations with named filter bypass.

2. **Integration tests break wholesale after adding HasQueryFilter**: the InMemory provider applies query filters; TestDataHelper creates entities without GroupId; all 191 tests fail with empty result sets. Prevention: register a stub IActiveGroupContext (GroupId=1, IsSuperAdmin=false) in WebApplicationFactoryBase.ConfigureTestServices in the same PR that introduces the filter; update TestDataHelper to set GroupId = 1 on all created entities.

3. **Namespace rename breaks migration build**: .Designer.cs migration files embed CLR type attributes and string literals referencing old namespaces. After rename the project fails to compile and dotnet ef cannot run. Prevention: isolate the rename as its own phase; global find-replace all Migrations files; run dotnet ef migrations add NamespaceRename to regenerate the model snapshot; verify clean build before merging.

4. **Hangfire jobs lose group context and send wrong-group reminders**: DailyReminderJob runs without HttpContext; SessionReminderJob must receive groupId as an explicit parameter, not rely on session. Prevention: update all four email jobs to accept explicit groupId parameters; update DailyReminderJob to pass quest.GroupId when enqueuing.

5. **Admin lockout after removing self-registration**: removing the Register endpoint without verifying admin role integrity in the production DB can lock out all administrators. Prevention: verify Admin role exists in AspNetUserRoles before deploying; test admin login against a DB snapshot; add a startup guard that logs CRITICAL and refuses to start if no Admin user exists and self-registration is disabled.

6. **Applying HasQueryFilter to UserEntity breaks ASP.NET Identity**: UserManager.FindByEmailAsync and related methods query the Users DbSet without calling IgnoreQueryFilters; a group filter on UserEntity silently breaks login, password reset, and email confirmation. Prevention: apply HasQueryFilter only on content entities; never on UserEntity.
## Implications for Roadmap

Based on combined research, the dependency graph and pitfall-to-phase mapping strongly suggest a 5-phase structure.

### Phase 1: Namespace Rename (EuphoriaInn to QuestBoard)

**Rationale:** Every subsequent PR writes code in the new namespace. Mixing rename with schema changes produces an unreadable diff and makes partial rollback impossible. Architecture research identifies this as anti-pattern 5. The rename has zero behavior change and all 191 tests must pass before merge.

**Delivers:** Clean codebase with consistent QuestBoard.* namespaces; updated .slnx, .csproj files, migration Designer files, Dockerfile labels, CI references.

**Avoids:** Pitfall 3 (migration build broken by namespace mismatch in .Designer.cs files).

**Research flag:** Standard pattern, no deeper research needed. Verify ModernRonin.ProjectRenamer .slnx support before committing; have a manual grep fallback ready.

### Phase 2: Group Entity + Schema Foundation

**Rationale:** Every other multi-tenancy feature depends on GroupEntity existing. GroupId FK columns must exist before HasQueryFilter can reference them. The data migration must land here so production data integrity is established before filters are applied.

**Delivers:** GroupEntity and UserGroupEntity tables; GroupId FK on 5 content entities; EuphoriaInn group seeded; all existing users seeded into that group; IGroupRepository and IUserGroupRepository; composite indexes on GroupId columns.

**Addresses:** Group entity and migration, UserGroupEntity junction table, data migration seeding GroupId=1, existing data migrated into default group.

**Avoids:** Pitfall 8 (SeedShopDataAsync creates orphaned shop items without GroupId); Pitfall 6 (never add HasQueryFilter to UserEntity, document as architecture constraint here).

**Research flag:** Standard EF Core migration pattern, no deeper research needed.

### Phase 3: IActiveGroupContext + Global Query Filters + Hangfire Adaptation

**Rationale:** Filters can only be added after GroupId FK columns exist (Phase 2). Test infrastructure must be updated in the same PR as the filter addition. Hangfire job adaptation (all four email jobs gaining explicit groupId parameters) must also land in this phase to prevent cross-group reminder emails.

**Delivers:** IActiveGroupContext interface (Domain); ActiveGroupContextService (Service, reads Session); modified QuestBoardContext constructor with HasQueryFilter on 5 entity types; stub IActiveGroupContext in test factory; updated TestDataHelper with GroupId = 1; IGroupService / GroupService; updated Hangfire jobs with explicit groupId parameters.

**Addresses:** EF Core Global Query Filters, ITenantService scoped reads session, per-group data isolation.

**Avoids:** Pitfall 1 (null GroupId returns empty rows, use IsSuperAdmin conditional); Pitfall 4 (test factory has no GroupId context, fix in same PR); Pitfall 2 (Hangfire cross-group sweep).

**Research flag:** Highest-complexity phase. Consider --research-phase during planning for the Hangfire job adaptation, specifically how to handle already-queued jobs when ExecuteAsync gains a new groupId parameter (deserialization failure for in-flight jobs).

### Phase 4: SuperAdmin Role + Groups MVC Area

**Rationale:** The SuperAdmin role seed must exist before the area controller can be authorized. The area route must be registered before the default route (anti-pattern 4 from architecture research). This phase is self-contained and does not break any existing functionality.

**Delivers:** SuperAdmin Identity role seeded; SuperAdminOnly authorization policy; GroupsManagementController with Area and Authorize attributes; area route in Program.cs before default route; area _ViewImports.cshtml; group management views (list, create, members).

**Addresses:** SuperAdmin role and policy, dedicated management area for SuperAdmin, group-scoped admin panel.

**Avoids:** Area route registered after default route (404 for all area controllers); missing _ViewImports.cshtml in area (broken tag helpers).

**Research flag:** Standard ASP.NET Core Areas pattern, no deeper research needed.

### Phase 5: Active Group Picker + Group Switcher + Admin User Creation

**Rationale:** This phase depends on all previous phases: filters must work (Phase 3), groups must exist (Phase 2), SuperAdmin must be able to create groups (Phase 4). Removing self-registration belongs here because it requires admin user creation to be fully functional first.

**Delivers:** Group picker page (GET /Groups/Pick, redirected to after login if user is in 2+ groups); group switcher navbar dropdown (only rendered if membership count > 1); POST /Groups/Switch/{id} with server-side membership validation; self-registration removed from AccountController; admin user creation form in AdminController.

**Addresses:** Group picker at login, group switcher in navigation, admin-only user creation, remove self-registration.

**Avoids:** Pitfall 5 (admin lockout after registration removal); Pitfall 7 (TestAuthHandler missing GroupId claim, update in same PR as any new GroupId-bearing authorization handler).

**Research flag:** Group picker session persistence across LXC process restart needs validation during planning and should be an explicit UAT criterion.

### Phase Ordering Rationale

- Rename first: zero behavior change, fully verifiable, eliminates namespace ambiguity from all subsequent diffs.
- Schema before filters: HasQueryFilter references FK columns that must exist in entities at compile time.
- Filters before UI: group picker writing to Session has no effect if the DbContext does not read it; all data would appear unscoped.
- SuperAdmin area before group picker: SuperAdmin must be able to create groups before the picker has real groups to switch between.
- Group picker last: depends on all previous infrastructure; admin lockout risk isolated to the final phase with full pre-deploy checklist.

### Research Flags

Needs deeper research during planning:
- **Phase 3 (Hangfire job adaptation):** How to handle already-queued Hangfire jobs when SessionReminderJob.ExecuteAsync gains a new groupId parameter. Deserialization will fail for in-flight jobs. Plan a queue drain window or backward-compatible default parameter before planning this phase.

Standard patterns (skip --research-phase):
- **Phase 1 (Namespace Rename):** Mechanical find-and-replace; EF Core migration namespace behavior is well-documented.
- **Phase 2 (Group Entity Schema):** Standard EF Core migration + seed pattern.
- **Phase 4 (SuperAdmin Area):** Standard ASP.NET Core Areas; well-documented.
- **Phase 5 (Group Picker):** Standard session read/write pattern; existing session infrastructure reused.

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| Stack | MEDIUM | EF Core and ASP.NET Core from official Microsoft docs; rename tooling (.slnx support) is LOW, manual fallback required |
| Features | MEDIUM | EF Core multi-tenancy from official docs; UX patterns from industry observation; some implementation details from LOW-confidence single sources |
| Architecture | MEDIUM | Layer placement grounded in actual codebase analysis; EF Core DbContext injection pattern from official multitenancy docs |
| Pitfalls | MEDIUM | Cross-validated against actual codebase files (QuestBoardContext.cs, WebApplicationFactoryBase.cs, DailyReminderJob.cs, TestDataHelper.cs); high confidence these are real project-specific risks |

**Overall confidence:** MEDIUM

### Gaps to Address

- **ModernRonin.ProjectRenamer + .slnx compatibility:** The tool was built for .sln format; .slnx support is unverified. Plan a manual find-and-replace fallback before starting Phase 1.
- **Hangfire in-flight job argument compatibility:** When SessionReminderJob.ExecuteAsync gains a new groupId parameter, any jobs queued against the old signature will fail deserialization on pickup. Determine whether a queue drain window or a backward-compatible default parameter is the right mitigation before Phase 3 planning.
- **EF Core named filters availability in 10.0.9 GA:** HasQueryFilter with a name argument is documented as EF Core 10+. Verify this API shipped in 10.0.9 GA (not only preview). If unavailable, fall back to the combined-lambda IgnoreQueryFilters() approach.
- **Group picker persistence across LXC restart:** Session is in-memory by default; if the host restarts the active group selection is lost. Determine whether a distributed session store (backed by the existing SQL Server connection) or a UserPreference DB column is the right mitigation.

## Sources

### Primary (MEDIUM confidence, official Microsoft docs)
- https://learn.microsoft.com/en-us/ef/core/querying/filters: EF Core Global Query Filters, named filters, bypass patterns
- https://learn.microsoft.com/en-us/ef/core/miscellaneous/multitenancy: DbContext injection pattern for multi-tenancy
- https://learn.microsoft.com/en-us/ef/core/modeling/relationships/many-to-many: junction table without explicit entity
- https://learn.microsoft.com/en-us/aspnet/core/mvc/controllers/areas: Areas folder structure, route registration, ViewImports caveat
- https://learn.microsoft.com/en-us/aspnet/core/fundamentals/http-context: IHttpContextAccessor registration

### Secondary (MEDIUM confidence, community, multi-source agreement)
- https://codewithmukesh.com/blog/global-query-filters-efcore/: selective IgnoreQueryFilters by name
- https://www.thereformedprogrammer.net/building-asp-net-core-and-ef-core-multi-tenant-apps-part2-administration/: SuperAdmin vs tenant Admin hierarchy
- https://www.milanjovanovic.tech/blog/multi-tenant-applications-with-ef-core: architecture patterns

### Tertiary (LOW confidence, single source or community blog)
- https://github.com/ModernRonin/ProjectRenamer: rename tooling, .slnx support unverified
- https://antondevtips.com/blog/how-to-implement-multitenancy-in-asp-net-core-with-ef-core: ITenantProvider patterns
- https://andyp.dev/posts/disable-user-registrations-in-asp-net-core-3-identity: self-registration removal approach
- https://dev.to/luqman_bolajoko/implementing-aspnet-identity-for-a-multi-tenant-application-best-practices-4an6: never apply HasQueryFilter to UserEntity

---
*Research completed: 2026-06-29*
*Ready for roadmap: yes*
