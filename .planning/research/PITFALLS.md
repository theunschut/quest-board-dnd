# Pitfalls Research

**Domain:** Adding multi-tenancy (EF Core Global Query Filters + junction table + SuperAdmin role + namespace rename) to a live ASP.NET Core 10 MVC application with a production SQL Server database and 191 active tests
**Researched:** 2026-06-29
**Confidence:** MEDIUM (codebase-grounded; web sources LOW confidence, cross-validated against actual code)

---

## Critical Pitfalls

### Pitfall 1: Global Query Filter Evaluates to NULL and Silently Returns No Rows

**What goes wrong:**
`HasQueryFilter(e => e.GroupId == _currentGroupId)` where `_currentGroupId` is resolved at query time from an injected `ITenantContext`. If `ITenantContext.CurrentGroupId` is null (no active group set), EF Core translates the WHERE clause to `WHERE GroupId = NULL`, which matches nothing — every query returns an empty result set. The app does not crash; it just shows blank pages, empty quest boards, and zero shop items. The failure mode is invisible and may pass a smoke test.

**Why it happens:**
The `ITenantContext` is typically backed by a session cookie, claim, or HTTP header. It returns null during: (a) unauthenticated requests to public pages, (b) the SuperAdmin context where no specific group is active, (c) Hangfire job execution where HttpContext is null, (d) any startup seed code that runs before a request exists.

**How to avoid:**
- Design `ITenantContext` to have an explicit "unresolved" state distinct from "null" — throw `InvalidOperationException("No active group context")` for unresolved rather than returning null.
- Register a filter that returns all rows when no group is active, only for controllers marked with `[AllowAnonymous]` or routes that are intentionally cross-tenant (SuperAdmin area).
- In `QuestBoardContext.OnModelCreating`, use a named filter so SuperAdmin code can call `IgnoreQueryFilters("GroupFilter")` without disabling soft-delete or other filters.
- Verify public endpoints (home page, login page) do not trigger GroupId-filtered queries.

**Warning signs:**
- Integration tests that seed data return 0 results after adding filters.
- `HomeController` shows an empty quest list for unauthenticated visitors.
- `SeedShopDataAsync` in `Program.cs` fails silently because it inserts items but they vanish on retrieval (no active GroupId during startup scope).

**Phase to address:** Group entity + Global Filter introduction phase. Must be the first thing proven before any other multi-tenancy work.

---

### Pitfall 2: DailyReminderJob Queries Across All Groups After Global Filter Is Added

**What goes wrong:**
`DailyReminderJob.ExecuteAsync` creates a scope via `IServiceScopeFactory.CreateAsyncScope()` and calls `questRepository.GetFinalizedQuestsForDateAsync(tomorrow)`. After adding the global query filter, the `DbContext` inside that scope has **no active GroupId** (HttpContext is null in Hangfire). With the "return nothing on null" approach (Pitfall 1 fix), the job sends zero reminders for every group. With the "skip filter on null" approach, it returns quests across all groups — correct sweep behaviour, but the job then enqueues `SessionReminderJob` for those quests. `SessionReminderJob` uses a newly created scope that also has no active GroupId — so it queries `PlayerSignups` with no group scope, risking loading signups from the wrong group's data if a quest was somehow duplicated across groups.

**Why it happens:**
The codebase already uses `IServiceScopeFactory` in all jobs (a solved problem per PROJECT.md), but that pattern resolves scoped services — it does not set an HTTP context or active tenant. `DailyReminderJob` is legitimately a cross-group sweep, but `SessionReminderJob` is quest-specific and must be group-scoped after multi-tenancy lands.

**How to avoid:**
- `DailyReminderJob` must explicitly bypass the group filter using `IgnoreQueryFilters` or a dedicated SuperAdmin scope — it legitimately needs all groups' quests.
- `SessionReminderJob.ExecuteAsync` receives `questId` as a parameter. After multi-tenancy, add `groupId` as a second explicit parameter. The job resolves the tenant via `groupId`, not via HttpContext.
- The enqueue call in `DailyReminderJob` becomes: `Enqueue<SessionReminderJob>(j => j.ExecuteAsync(quest.Id, quest.GroupId, false, false, CancellationToken.None))`.
- `QuestFinalizedEmailJob`, `QuestDateChangedEmailJob`, and `ConfirmationEmailJob` all have the same pattern — each must receive `GroupId` explicitly when enqueued.

**Warning signs:**
- After adding global filters, no reminder emails are sent for any group (filter returns nothing in background context).
- Or conversely: a reminder email is sent to players from the wrong group because the quest's PlayerSignups loaded cross-group.
- The `ReminderLogEntity` dedup check uses `(questId, playerId)` — this remains correct because Identity assigns int Ids globally, not per-group.

**Phase to address:** Hangfire job adaptation phase — after Group entity and global filters land. Must update all four email jobs in one PR.

---

### Pitfall 3: Namespace Rename Breaks the Migration Build Before a Single Line of Multi-Tenancy Code Is Written

**What goes wrong:**
`QuestBoardContextModelSnapshot.cs` and every `.Designer.cs` migration file embeds the CLR type attribute `[DbContext(typeof(EuphoriaInn.Repository.Entities.QuestBoardContext))]` and string literals like `"EuphoriaInn.Repository.Entities.CharacterClassEntity"`. After renaming the assembly and all namespaces from `EuphoriaInn.*` to `QuestBoard.*`, the existing migration files reference types that no longer exist. The project fails to compile and `dotnet ef migrations add` cannot run.

**Why it happens:**
EF Core migrations are code, not metadata. The `.Designer.cs` files reference the concrete DbContext type by name via the `[DbContext]` attribute. The model snapshot stores entity CLR type names as string literals that EF uses to match model state between migrations. A namespace rename without updating these files breaks the entire migration chain at the C# layer.

**How to avoid:**
1. Rename namespaces first — just project and namespace renames, zero logic changes.
2. Do a global find-replace across all `Migrations/*.cs` and `Migrations/*.Designer.cs` files: `EuphoriaInn.Repository` → `QuestBoard.Repository`, `EuphoriaInn.Domain` → `QuestBoard.Domain`, etc.
3. Update the `[DbContext(typeof(...))]` attributes in all `.Designer.cs` files.
4. Run `dotnet build` from a clean checkout (no cached `obj/` or `bin/`) — must compile clean before running `dotnet ef`.
5. Run `dotnet ef migrations add NamespaceRename --project ../QuestBoard.Repository` to regenerate the snapshot with correct new type names. This migration will have an empty `Up()` method — correct.
6. Verify `dotnet ef database update` succeeds on the development SQL Server without rolling back existing data.
7. The `__EFMigrationsHistory` table stores `MigrationId` values by timestamp+name (e.g. `20260626190255_AddReminderLog`) — renaming namespaces does NOT change these IDs. Production data is safe; only the C# build is affected.

**Warning signs:**
- `CS0246: The type or namespace name 'EuphoriaInn' could not be found` in migration files after rename.
- `dotnet ef migrations add` reports it cannot find the DbContext type.
- `ModelSnapshot` fails to load with a type resolution exception at startup.

**Phase to address:** Namespace Rename phase — must be its own isolated phase, fully verified (build + `dotnet ef` + all 191 tests green) before any multi-tenancy changes begin.

---

### Pitfall 4: InMemory Test Factory Has No GroupId Context — All Integration Tests Fail After Filter Addition

**What goes wrong:**
`WebApplicationFactoryBase` uses `options.UseInMemoryDatabase(Database.DatabaseName)`. EF Core's InMemory provider **does** apply `HasQueryFilter`. The filter evaluates its expression against the injected `ITenantContext`. In tests, `TestAuthHandler` sets `UserId`, `UserName`, `Email`, and `Roles` claims, but does **not** set an active `GroupId`. The global filter evaluates to `e.GroupId == 0` (default int) or `e.GroupId == null`, returning zero rows for every query. All 139 existing integration tests that call `TestDataHelper.CreateTestQuestAsync` will create entities without a `GroupId` and get 0 results back — every assertion on content will fail.

**Why it happens:**
`TestDataHelper` creates entities by directly calling `context.Quests.Add(quest)` without setting `GroupId`. After the global filter is added, these entities are invisible to all subsequent reads in the same test because `GroupId` is 0 and no group with Id 0 is the active tenant.

**How to avoid:**
- `TestDataHelper` must be updated to accept a `groupId` parameter (or use a constant `TestGroupId = 1`).
- `WebApplicationFactoryBase.ConfigureTestServices` must register a test `ITenantContext` implementation that always returns `GroupId = 1`.
- `TestDataHelper.SeedRolesAsync` must also create the test group (Id = 1, Name = "TestGroup") and assign all test users to it.
- The `TestAuthHandler` authorization header format `userId:userName:email:roles` may need to extend to `userId:userName:email:roles:groupId` for tests that need to simulate switching groups.
- Tests that intentionally test cross-group SuperAdmin behaviour need a factory override with `IgnoreQueryFilters` or a SuperAdmin `ITenantContext`.

**Warning signs:**
- After adding `HasQueryFilter`, running `dotnet test` shows 100+ failures, all due to empty result sets or 404s for entities that were just created.
- `content.Should().Contain("Adventure Quest")` fails immediately in `QuestControllerIntegrationTests_Comprehensive`.

**Phase to address:** Test infrastructure update — must happen **in the same commit** as the global filter addition. Do not merge a PR that breaks 139 integration tests.

---

### Pitfall 5: Locking Out the Only Admin After Removing Self-Registration

**What goes wrong:**
Self-registration is removed. The only way to create accounts becomes Admin-only user creation. If the production deployment runs before at least one Admin account exists with a known password and correct group membership, all admin functions become inaccessible. Recovery requires directly manipulating SQL Server (`AspNetUserRoles` table) — an operational risk that must be pre-planned.

**Why it happens:**
The removal of the Register controller/view is a code change applied on startup. The data migration that seeds the EuphoriaInn group and assigns existing users to it may not simultaneously guarantee Admin roles are correctly preserved. If the migration seed accidentally fails to assign the existing Admin user to the new group structure, the user exists in `AspNetUsers` but their `AdminHandler` check (`userService.IsInRoleAsync`) fails because the group-aware role lookup returns nothing.

**How to avoid:**
- Before removing the Register endpoint, verify via the production DB that at least one user has the `Admin` role in `AspNetUserRoles`.
- The data migration that seeds `GROUP=EuphoriaInn` must preserve all existing `AspNetUserRoles` entries untouched — add a test migration assertion.
- Add a startup guard: if no Admin user exists and self-registration is disabled, log a `CRITICAL` error and refuse to start (fail fast rather than silently lock out).
- The `SuperAdmin` role must be bootstrapped from environment variables (e.g. `SUPERADMIN_EMAIL`, `SUPERADMIN_PASSWORD`) — separate from the group Admin system.
- Test the lockout scenario explicitly: run the migration against a copy of the production DB snapshot and verify admin login works before deploying.

**Warning signs:**
- Production deploy completes but `/Admin` returns 403 for all users.
- `AdminHandler.HandleRequirementAsync` calls `userService.IsInRoleAsync(context.User, "Admin")` — if role claims are missing from the cookie (stale login session), this returns false even for real admins.

**Phase to address:** Self-registration removal + data migration phase. These two must be planned together; the data migration must run and be verified before the registration endpoint is deleted.

---

### Pitfall 6: Applying a Global Query Filter to UserEntity Breaks ASP.NET Identity's Internal User Lookups

**What goes wrong:**
Adding `HasQueryFilter(u => u.UserGroups.Any(g => g.GroupId == _currentGroupId))` to `UserEntity` in `QuestBoardContext.OnModelCreating` will break ASP.NET Core Identity's internal `UserStore` queries. `FindByEmailAsync`, `FindByNameAsync`, and `FindByIdAsync` all query the `Users` DbSet directly. With a group filter active, these calls return null for any user who is not in the currently active group — including SuperAdmins who have no specific active group set. Login fails silently.

**Why it happens:**
The ASP.NET Core Identity team has explicitly stated they do not support multi-tenancy. `UserStore<UserEntity>` queries the `Users` DbSet and does not call `IgnoreQueryFilters` — it has no awareness of custom filters. Any filter on `UserEntity` is applied to Identity's own internal lookups.

**How to avoid:**
- **Do NOT put a global query filter on `UserEntity` itself.** Apply group filters only on content entities: `QuestEntity`, `ShopItemEntity`, `CharacterEntity`, `DungeonMasterProfileEntity`, `TradeItemEntity`, `UserTransactionEntity`, `PlayerSignupEntity`, `ReminderLogEntity`.
- The junction table `UserGroupEntity` (user-to-group membership) is a separate entity used only for membership queries — it does not filter the user itself.
- The existing `AdminHandler` calls `userService.IsInRoleAsync(context.User, "Admin")` and `DungeonMasterHandler` calls `userService.IsInRoleAsync(context.User, "DungeonMaster")` — these are claim-based and must remain unchanged.
- Group-scoped authorization uses a new policy (`GroupMemberOnly`) that checks the GroupId claim, separate from role-based policies.

**Warning signs:**
- Login returns "invalid username or password" for all users after adding group filter.
- `UserManager.FindByEmailAsync` returns null for valid users.
- Password reset, email confirmation, and change-email flows all fail.

**Phase to address:** Group entity definition phase. Document "no global filter on UserEntity" as an architecture constraint before any code is written.

---

### Pitfall 7: TestAuthHandler Missing GroupId Claim Causes Silent 403 for All Authenticated Tests

**What goes wrong:**
`TestAuthHandler.HandleAuthenticateAsync` parses `userId:userName:email:roles` from the Authorization header. After adding a `GroupMemberHandler` that checks `context.User.FindFirst("GroupId")`, tests using `CreateAuthenticatedDMClientAsync` will have no `GroupId` claim. The handler fails silently — the requirement goes unmet, and the controller returns 403. All DM and player integration tests fail with 403, which looks identical to a real authorization regression.

**Why it happens:**
`AuthenticationHelper` was built for a single-tenant world. Adding a new `IAuthorizationHandler` to the pipeline without updating `TestAuthHandler` to emit the required claims causes silent 403 failures that are indistinguishable from actual policy regressions.

**How to avoid:**
- When adding any new `IAuthorizationHandler`, update `TestAuthHandler` in the same PR to emit the required claims.
- Extend `CreateAuthenticatedClientAsync` with an optional `groupId` parameter defaulting to `1` (test group).
- The existing `DungeonMasterOnly` and `AdminOnly` policies must NOT gain a GroupId check — they remain role-only. Only new policies (`GroupMemberOnly`, `GroupAdminOnly`) are group-aware.
- Add a dedicated test for `GroupMemberHandler` that verifies it succeeds with a valid GroupId claim and fails without one.

**Warning signs:**
- After adding `GroupMemberHandler`, all existing DM and admin integration tests return 403.
- `QuestController` `Create_Get_WhenAuthenticatedAsDM_ShouldReturnCreateForm` fails with 403 despite the DM role being correctly set.

**Phase to address:** Authorization handler update phase, co-deployed with any new GroupId-bearing claim addition to the pipeline.

---

### Pitfall 8: SeedShopDataAsync Creates Orphaned Shop Items Without GroupId

**What goes wrong:**
`Program.cs` calls `SeedShopDataAsync` which calls `shopSeedService.SeedBasicEquipmentAsync(adminUser.Id)`. After adding `GroupId` as a required non-nullable column on `ShopItemEntity` and a global query filter, this startup call either: (a) fails with a SQL Server constraint violation because `GroupId` is 0/not set, or (b) creates items with `GroupId = 0` that are invisible to all groups (no group has Id 0). The shop is empty on first boot after the migration.

**Why it happens:**
The shop seed was written pre-multi-tenancy. It creates items attributed to a user, not a group. After adding group scoping, the seed method signature must change.

**How to avoid:**
- Update `SeedShopDataAsync` to accept and pass the EuphoriaInn group's `GroupId` when creating seed items.
- Make the seed idempotent by checking `context.ShopItems.IgnoreQueryFilters().Any(i => i.GroupId == groupId && i.Name == "...")` before inserting.
- Consider seeding shop items per-group during group creation rather than at application startup — cleaner for a multi-group world.

**Warning signs:**
- Application starts without error but shop is empty for all users after migration.
- `ShopItems` table contains rows with `GroupId = 0` or null that are never displayed.

**Phase to address:** Data migration + seeding phase — update `SeedShopDataAsync` in the same migration that creates the Groups table.

---

## Technical Debt Patterns

| Shortcut | Immediate Benefit | Long-term Cost | When Acceptable |
|----------|-------------------|----------------|-----------------|
| Skip filter on null GroupId (return all rows) | Simplifies startup/seed code; background jobs work without explicit groupId | Cross-tenant data leakage if null GroupId reaches a user-facing query | Never for user-facing queries; only for explicitly SuperAdmin-scoped operations with named filter bypass |
| Global filter on UserEntity | Simpler model (user sees only their group) | Breaks Identity's internal UserStore queries (login, password reset, confirm email) | Never |
| Hardcode GroupId = 1 in test infrastructure | Faster test migration from single-tenant | Tests never verify cross-group isolation | Acceptable for MVP phase; must add cross-group isolation tests before shipping multi-group |
| Namespace rename mixed with multi-tenancy feature PR | Fewer PRs | Impossible to bisect — is the failure a rename bug or a filter bug? | Never — isolate the rename PR completely |
| Reuse `ReminderLogEntity` (questId, playerId) unique index without adding groupId | No migration change needed | Correct — Identity assigns player Ids globally; no per-group Id collision | Acceptable permanently |

---

## Integration Gotchas

| Integration | Common Mistake | Correct Approach |
|-------------|----------------|------------------|
| EF Core + InMemory (test factory) | Adding HasQueryFilter without updating test ITenantContext registration | Register `TestTenantContext : ITenantContext` returning `GroupId = 1` in `ConfigureTestServices` |
| Hangfire + GlobalQueryFilter | Relying on the filter to scope DailyReminderJob across groups | DailyReminderJob calls `IgnoreQueryFilters` (cross-group sweep); SessionReminderJob receives explicit `groupId` parameter |
| EF Core named filters (EF10) | Using `IgnoreQueryFilters()` with no args disables ALL filters | Use `HasQueryFilter("GroupFilter", ...)` (EF10) and disable by name to preserve other named filters |
| EF Core required navigation + filter | Inner join on a filtered required navigation silently drops parent rows | Make navigations to filtered entities optional (LEFT JOIN) or add matching filter to parent entity |
| ASP.NET Identity + filter | Filtering UserEntity by GroupId breaks UserManager.FindByEmailAsync | Never apply HasQueryFilter to UserEntity; scope only content entities |
| AutoMapper + GroupId | EntityProfile maps Entity → DomainModel without GroupId | Add GroupId to both EntityProfile (Entity→Domain) and exclude from ViewModelProfile if not surfaced in UI |

---

## Security Mistakes

| Mistake | Risk | Prevention |
|---------|------|------------|
| Using IgnoreQueryFilters() in a controller action for "admin view" without SuperAdmin policy | Any user who bypasses the policy check sees all groups' data | IgnoreQueryFilters only in SuperAdmin-routed actions protected by `[Authorize("SuperAdminOnly")]` |
| Using FromSqlRaw / ExecuteSqlRaw without manual GroupId WHERE clause | Full cross-tenant data exposure — global filters do not apply to non-composable raw SQL | Audit all raw SQL usages before adding global filters; add an integration test that proves group isolation |
| Session-based GroupId cookie with no server-side validation | User changes their active GroupId cookie to access another group | Validate GroupId server-side: confirm the authenticated user is a member of the claimed group on every request via middleware or ActionFilter |
| Forgetting to add GroupId to future entities | New entity type silently unscoped — exposes data across groups | Document GroupId as a required field in CONVENTIONS.md; add an integration test that asserts every DbSet is filtered |

---

## "Looks Done But Isn't" Checklist

- [ ] **Global filter on GroupId:** Verify the filter is also correct for related entities loaded via `.Include()` — PlayerSignup.Quest, Character.Owner, UserTransaction.ShopItem each carry their own GroupId or are only accessible via a parent that already carries the filter.
- [ ] **Namespace rename:** Run `dotnet build` from a clean checkout (no cached `obj/` or `bin/`) to confirm all Designer.cs files compile. A dirty build masks missing type references because cached assemblies are used.
- [ ] **Hangfire jobs:** After adding a `groupId` parameter to `SessionReminderJob.ExecuteAsync`, previously queued Hangfire jobs stored in SQL Server will fail deserialization (their serialized argument list lacks the new parameter). Plan a queue drain window before deploying.
- [ ] **Admin lockout:** Verify admin login works on a DB snapshot copy before production deploy. Do not assume seeding preserved role assignments.
- [ ] **Test suite count:** After every multi-tenancy PR, confirm `dotnet test` shows 191+ passing (not a silent all-failures-as-skipped situation).
- [ ] **Self-registration removed:** Confirm the `/Account/Register` GET and POST both redirect to login — not just the nav link removed. Automated tools can still POST directly to the endpoint.
- [ ] **Group picker persistence:** Whatever mechanism persists the active GroupId (cookie, claim, session) must survive a page reload and a process restart on the LXC host. Test with explicit session expiry.
- [ ] **ReminderLog dedup integrity:** Confirm the unique index `(QuestId, PlayerId)` in `ReminderLogEntity` correctly deduplicates across a multi-group deployment — verify that player int Ids remain globally unique (they do, since Identity assigns Ids globally).

---

## Recovery Strategies

| Pitfall | Recovery Cost | Recovery Steps |
|---------|---------------|----------------|
| Global filter returning null/empty (blank pages in production) | MEDIUM | Deploy hotfix: set default GroupId to the first group if null; restore data visibility immediately without a DB change |
| Hangfire jobs failing post-deploy due to argument schema change | LOW | Delete failed jobs from Hangfire dashboard at `/hangfire`; re-enqueue with correct signature via Admin UI or temporary script |
| Namespace rename breaks migration build | MEDIUM | Revert the rename PR; do a targeted find-replace on the Migrations/ directory only in a new branch; re-verify clean build before merging |
| Admin lockout in production | HIGH | DB access required: `INSERT INTO AspNetUserRoles (UserId, RoleId) VALUES (...)` directly against SQL Server at /opt/questboard/; test this procedure against a DB copy before deploying |
| 191 integration tests fail after filter addition | LOW | Roll back the filter PR; update test infrastructure (ITenantContext stub, TestDataHelper groupId) in a separate branch; re-merge with tests green |
| UserEntity filter breaks login | CRITICAL | Revert the HasQueryFilter on UserEntity immediately — this is a login-breaking production incident; a deploy is required within minutes |

---

## Pitfall-to-Phase Mapping

| Pitfall | Prevention Phase | Verification |
|---------|------------------|--------------|
| P1 — Null GroupId returns empty rows | Group entity + filter introduction | `dotnet test` passes with seeded group; home page shows quests without crash |
| P2 — DailyReminderJob cross-group sweep | Hangfire job adaptation (after filters land) | Integration test: DailyReminderJob with two groups only queues reminders for the correct group's quests |
| P3 — Namespace rename breaks migration build | Namespace rename phase (isolated, first) | `dotnet build` clean from `obj/`-free checkout; `dotnet ef migrations add NamespaceRename` succeeds; all 191 tests pass |
| P4 — Test factory has no GroupId context | Test infrastructure update (same PR as global filter) | All 191 tests pass after update; new cross-tenant isolation test added |
| P5 — Admin lockout after registration removal | Self-registration removal + data migration phase | Pre-deploy checklist: Admin role confirmed in DB; staging deploy + admin login verified |
| P6 — UserEntity filter breaks login | Group entity definition phase (architecture decision doc) | UserEntity has no HasQueryFilter; login smoke test passes in staging |
| P7 — TestAuthHandler missing GroupId claim | Authorization handler update phase | New GroupMemberHandler test passes; all 139 existing integration tests remain green |
| P8 — SeedShopDataAsync creates orphaned data | Data migration + seeding phase | ShopItems in DB have correct GroupId; shop page shows items after fresh deploy |

---

## Sources

- [EF Core Global Query Filters — Microsoft Learn](https://learn.microsoft.com/en-us/ef/core/querying/filters)
- [Implementing Secure Multi-Tenancy with EF Global Query Filters — Medium](https://medium.com/@assiljanbeih/implementing-secure-multi-tenancy-with-eflobal-query-filters-net-9502ac290fb2)
- [Allow ignoring Global Query Filters for specific Include navigations — GitHub Issue #37296](https://github.com/dotnet/efcore/issues/37296)
- [Warning: Role has global query filter and is required end of relationship — GitHub Issue #26185](https://github.com/dotnet/efcore/issues/26185)
- [Hangfire + HttpContext null in background jobs — GitHub Issue #2004](https://github.com/HangfireIO/Hangfire/issues/2004)
- [How to Access HttpContext and Services with Hangfire — Wrapt Dev Blog](https://wrapt.dev/blog/hangfire-job-context)
- [EF Core Migration Files — Learn EF Core](https://www.learnentityframeworkcore.com/migrations/migration-files)
- [Migrations do not fully qualify types — GitHub Issue #25933](https://github.com/dotnet/efcore/issues/25933)
- [ASP.NET Identity and multi-tenancy best practices — DEV Community](https://dev.to/luqman_bolajoko/implementing-aspnet-identity-for-a-multi-tenant-application-best-practices-4an6)
- [DbContext Lifetime, Configuration, and Initialization — Microsoft Learn](https://learn.microsoft.com/en-us/ef/core/dbcontext-configuration/)
- Codebase analysis: `QuestBoardContext.cs`, `WebApplicationFactoryBase.cs`, `AuthenticationHelper.cs`, `TestDataHelper.cs`, `DailyReminderJob.cs`, `SessionReminderJob.cs`, `AdminHandler.cs`, `Program.cs`

---
*Pitfalls research for: Multi-tenancy addition to ASP.NET Core 10 MVC + EF Core + Hangfire (D&D Quest Board v5.0)*
*Researched: 2026-06-29*
