<!-- refreshed: 2026-07-02 -->
# Architecture

**Analysis Date:** 2026-07-02

## System Overview

```text
┌─────────────────────────────────────────────────────────────────┐
│                     Service Layer (ASP.NET Core MVC)            │
│  Controllers | Views | ViewModels | Authorization | Middleware  │
│         QuestBoard.Service/                                      │
├──────────────┬──────────────────────┬───────────────────────────┤
│   QuestBoard │   Admin/Account/     │    Platform Area          │
│  Controllers │ Authorization        │    /platform/group/*      │
│  `Controllers/` │  `Authorization/` │  `Areas/Platform/`        │
└────────┬─────┴────────┬─────────────┴──────────┬────────────────┘
         │              │                        │
         └──────────────┼────────────────────────┘
                        │
         ┌──────────────▼──────────────┐
         │   Domain Layer (Business)   │
         │    QuestBoard.Domain/       │
         │                             │
         │  Interfaces | Services      │
         │  Models | Enums             │
         │  Extensions                 │
         └──────────────┬──────────────┘
                        │
         ┌──────────────▼────────────────────┐
         │   Repository Layer (Data Access)  │
         │     QuestBoard.Repository/        │
         │                                   │
         │  EF Core Context | Repositories  │
         │  Entities | Migrations           │
         │  AutoMapper (Entity ↔ Domain)    │
         └──────────────┬────────────────────┘
                        │
                        ▼
        ┌────────────────────────────────┐
        │  SQL Server Database (Local)   │
        │  AspNetSessionState table      │
        │  (distributed cache)           │
        └────────────────────────────────┘


Background Job Processing:
        ┌──────────────────────────────────┐
        │    Hangfire (SQL Server-backed)  │
        │    QuestBoard.Service/Jobs/      │
        └────────────┬─────────────────────┘
                     │
                     ▼
        Scoped DI Container
        → ActiveGroupContextService.SetGroupId(groupId)
        → Domain Services + Repositories
        → Email/Reminder Dispatch
```

## Component Responsibilities

| Component | Responsibility | File |
|-----------|----------------|------|
| HomeController | Entry point; redirects authenticated users to quest board | `QuestBoard.Service/Controllers/QuestBoard/HomeController.cs` |
| QuestController | Quest CRUD, finalization, player signup management | `QuestBoard.Service/Controllers/QuestBoard/QuestController.cs` |
| GroupPickerController | Group selection UI; stores ActiveGroupId in session | `QuestBoard.Service/Controllers/GroupPickerController.cs` |
| Platform/GroupController | SuperAdmin-only group CRUD and membership management | `QuestBoard.Service/Areas/Platform/Controllers/GroupController.cs` |
| AccountController | Login, password reset, email confirmation, user management | `QuestBoard.Service/Controllers/Admin/AccountController.cs` |
| AdminController | SuperAdmin dashboard, user creation/editing | `QuestBoard.Service/Controllers/Admin/AdminController.cs` |
| QuestService | Quest finalization, email dispatch coordination | `QuestBoard.Domain/Services/QuestService.cs` |
| GroupService | Group CRUD, membership queries (group-scoped) | `QuestBoard.Domain/Services/GroupService.cs` |
| QuestBoardContext | EF Core DbContext; applies global query filters for group isolation | `QuestBoard.Repository/Entities/QuestBoardContext.cs` |
| ActiveGroupContextService | Reads/writes active group from session; allows jobs to override | `QuestBoard.Service/Services/ActiveGroupContextService.cs` |
| GroupSessionMiddleware | Enforces group session; redirects to picker if missing | `QuestBoard.Service/Middleware/GroupSessionMiddleware.cs` |
| QuestFinalizedEmailJob | Hangfire job; sends finalized quest emails to players | `QuestBoard.Service/Jobs/QuestFinalizedEmailJob.cs` |
| DailyReminderJob | Scheduled (09:00 CET daily); sends session reminders | `QuestBoard.Service/Jobs/DailyReminderJob.cs` |

## Pattern Overview

**Overall:** Clean Architecture with strict 3-layer dependency flow (Service → Domain → Repository) plus multi-tenancy via Global Query Filters.

**Key Characteristics:**
- **Three-layer dependency**: Service layer depends on Domain; Domain depends on Repository; Repository never depends upward.
- **Multi-tenancy by group**: Authenticated, non-SuperAdmin users are scoped to one group at a time (via session). SuperAdmin has no active group (sees all).
- **Global query filters**: `QuestEntity` and `ShopItemEntity` carry `GroupId` and are automatically filtered by `IActiveGroupContext.ActiveGroupId` at the EF Core level.
- **Session-backed group context**: `ActiveGroupId` persists in SQL Server distributed cache (`AspNetSessionState` table) so group context survives app restarts.
- **Hangfire background jobs with group context**: Scoped DI pattern (HangfireJobHelper) sets `ActiveGroupContextService.SetGroupId(groupId)` before running job queries.
- **AutoMapper at two boundaries**: Entity ↔ DomainModel (Repository layer) and DomainModel ↔ ViewModel (Service layer) to decouple presentations.

## Layers

**Service Layer:**
- Purpose: MVC controllers, views, view models, authorization, middleware, background job definitions.
- Location: `QuestBoard.Service/`
- Contains: Controllers (organized by feature), Razor views, ViewModels, Authorization handlers, Middleware, Hangfire job classes.
- Depends on: Domain layer (IServices, domain models, enums), Repository layer (IdentityService, UserManager), Hangfire, ASP.NET Core Identity.
- Used by: HTTP clients; Hangfire scheduler.

**Domain Layer:**
- Purpose: Business logic, domain models, service interfaces, validation rules.
- Location: `QuestBoard.Domain/`
- Contains: Services (business logic implementations), Models (domain entities), Interfaces (service/repository contracts), Enums, Extensions.
- Depends on: Repository layer (only for IRepository interfaces and IActiveGroupContext); no direct code references to EF Core or SQL.
- Used by: Service layer, Repository layer (for mapper profiles).

**Repository Layer:**
- Purpose: Data access, EF Core migrations, entity mappings, AutoMapper to domain models.
- Location: `QuestBoard.Repository/`
- Contains: Repositories (EF-based implementations), Entities (EF data models), QuestBoardContext, Migrations, EntityProfile (AutoMapper).
- Depends on: Domain layer (for model interfaces, IActiveGroupContext injected into DbContext).
- Used by: Domain services via interface injection; service layer for identity/user manager access.

## Data Flow

### Primary Request Path (Authenticated, Group-Scoped)

1. **Authentication middleware** validates JWT/session claims; `User.Identity.IsAuthenticated == true` gates entry.
2. **GroupSessionMiddleware** (`QuestBoard.Service/Middleware/GroupSessionMiddleware.cs`) resolves `IActiveGroupContext` from session. If `ActiveGroupId == null` and user is not SuperAdmin, redirects to `/groups/pick`.
3. **Controller action** (e.g., `QuestController.Index`) receives authenticated request with `IActiveGroupContext` set to session's `ActiveGroupId`.
4. **Domain service** (e.g., `IQuestService.GetQuestsWithSignupsAsync`) uses `IQuestRepository`.
5. **Repository layer** queries via `QuestBoardContext`. Global query filter automatically applies `WHERE GroupId == activeGroupContext.ActiveGroupId` to `QuestEntity` DbSet.
6. **EF Core** translates filtered LINQ to SQL; executes against SQL Server.
7. **Entity → DomainModel** mapping via `EntityProfile` (AutoMapper).
8. **DomainModel → ViewModel** mapping via `ViewModelProfile` (AutoMapper).
9. **View rendered** with ViewModel data; browser receives HTML.

### Group Selection Flow

1. User logs in; no `ActiveGroupId` in session yet.
2. **GroupSessionMiddleware** detects null, redirects GET request to `/groups/pick`.
3. **GroupPickerController.Index** calls `IGroupService.GetGroupsForUserAsync(userId)` or `GetAllWithMemberCountAsync()` (SuperAdmin).
4. **GroupPickerController.SelectGroup** (POST) stores group ID: `HttpContext.Session.SetInt32(SessionKeys.ActiveGroupId, groupId)`.
5. Session middleware on next request reads the stored ID; request proceeds normally.

### Background Job Execution (Quest Finalization Email)

1. **QuestService.FinalizeQuestAsync** calls `IQuestEmailDispatcher.EnqueueFinalizedEmail(...)`.
2. **HangfireQuestEmailDispatcher** enqueues via `IBackgroundJobClient.Enqueue<QuestFinalizedEmailJob>(...)`.
3. **Hangfire background thread** dequeues job; instantiates `QuestFinalizedEmailJob`.
4. **QuestFinalizedEmailJob.ExecuteAsync** calls `HangfireJobHelper.RunInScopeAsync(scopeFactory, groupId, async sp => {...})`.
5. **HangfireJobHelper** creates DI scope, resolves `ActiveGroupContextService` (concrete type, not interface), calls `SetGroupId(groupId)`.
6. Job's lambda receives scoped provider; resolves `IQuestRepository`, `IEmailRenderService`, `IEmailService`.
7. **Repository queries** respect the overridden `ActiveGroupContextService._overriddenGroupId`; only see quests/items from `groupId`.
8. Email rendering and sending proceed; job completes.

**State Management:**
- **Session state**: `ActiveGroupId` stored in `AspNetSessionState` table (SQL Server distributed cache). Survives app restarts.
- **IActiveGroupContext**: Scoped per request/job; reads session in HTTP context, or uses override set by Hangfire jobs.
- **Group filter override**: `ActiveGroupContextService._groupIdOverridden` flag prevents jobs from seeing unrelated group data.

## Key Abstractions

**IActiveGroupContext:**
- Purpose: Provides the current request/job's group scoping level.
- Examples: `QuestBoard.Domain/Interfaces/IActiveGroupContext.cs`
- Pattern: Injected into `QuestBoardContext` (DbContext constructor); evaluated at query-filter time; allows jobs to override via concrete `ActiveGroupContextService.SetGroupId(groupId)`.

**IQuestEmailDispatcher / HangfireQuestEmailDispatcher:**
- Purpose: Abstract background job enqueuing; allows tests to swap with `NullQuestEmailDispatcher`.
- Examples: `QuestBoard.Domain/Interfaces/IQuestEmailDispatcher.cs`, `QuestBoard.Service/Services/HangfireQuestEmailDispatcher.cs`
- Pattern: Service layer calls `dispatcher.Enqueue(...)` (async coordination), Hangfire pulls job from queue.

**Global Query Filters (EF Core):**
- Purpose: Enforce group isolation at the query level, not in application code.
- Examples: `QuestBoardContext.OnModelCreating` lines 244–252.
- Pattern: `modelBuilder.Entity<QuestEntity>().HasQueryFilter(e => activeGroupContext.ActiveGroupId == null || e.GroupId == activeGroupContext.ActiveGroupId)`.

**AutoMapper Profiles:**
- Purpose: Decouple entity/model/viewmodel representations.
- Examples: `EntityProfile` (Entity ↔ DomainModel), `ViewModelProfile` (DomainModel ↔ ViewModel).
- Pattern: Two boundary maps; models in the middle are never serialized directly to HTTP responses.

## Entry Points

**HTTP Request (Authenticated):**
- Location: `QuestBoard.Service/Controllers/QuestBoard/HomeController.cs`
- Triggers: Browser navigation to `/` or `/quests`; user is either anonymous or authenticated.
- Responsibilities: Redirects anonymous users to login; authenticated users to quest board index.

**Group Picker (Session Missing):**
- Location: `QuestBoard.Service/Controllers/GroupPickerController.cs`
- Triggers: `GroupSessionMiddleware` detects `ActiveGroupId == null` and user is not SuperAdmin.
- Responsibilities: Displays available groups; POST handler stores selected group in session.

**SuperAdmin Platform (Group Management):**
- Location: `QuestBoard.Service/Areas/Platform/Controllers/GroupController.cs`
- Triggers: SuperAdmin navigates to `/platform/group/index`.
- Responsibilities: Create, rename, delete groups; manage group members.

**Hangfire Recurring Jobs:**
- Location: `QuestBoard.Service/Program.cs` lines 339–342 (DailyReminderJob registration).
- Triggers: Cron schedule (09:00 daily for session reminders); ad-hoc via `IBackgroundJobClient.Enqueue` for quest finalization emails.
- Responsibilities: Background email dispatch, retry logic with exponential backoff (5 attempts: 1/2/4/8/16 seconds).

## Architectural Constraints

- **Threading:** Single-threaded ASP.NET Core request loop (Kestrel); Hangfire background threads pool-based (2 workers configured in Program.cs line 245).
- **Global state:** `ActiveGroupContextService` carries instance-level `_overriddenGroupId` flag; scoped per job execution, not global.
- **Circular imports:** None observed. Dependencies flow strictly downward: Service → Domain → Repository.
- **No UserEntity query filter:** EF Core Identity requires unrestricted user queries (login, token generation, password reset all fail if UserEntity is filtered). Chart note: UserEntity intentionally excluded from global filters (QuestBoardContext line 254).
- **Session persistence:** Hangfire jobs cannot access `HttpContext.Session` (no HttpContext in background threads). `ActiveGroupContextService.SetGroupId()` bridges this gap.
- **SuperAdmin group scoping:** SuperAdmin intentionally has `ActiveGroupId = null` (sees all data) to manage cross-tenant concerns. Check `GroupSessionMiddleware` line 65–66 and `GroupPickerController` line 19 for SuperAdmin bypass.

## Anti-Patterns

### Accessing ActiveGroupId Without Null Check

**What happens:** Code calls `activeGroupContext.ActiveGroupId` assuming it's never null, then passes it to a method expecting `int`.

**Why it's wrong:** SuperAdmin runs with `ActiveGroupId == null` by design. A NullReferenceException or silent "0" behavior exposes admin data incorrectly or crashes the request.

**Do this instead:** Use `activeGroupContext.RequireActiveGroupId()` (extension in `QuestBoard.Domain/Extensions/ActiveGroupContextExtensions.cs`) in user-scoped paths; guard SuperAdmin separately in admin paths.

### Querying UserEntity with Group Scoping

**What happens:** EF Core developer adds a query filter to `UserEntity` "for consistency" with `QuestEntity`/`ShopItemEntity`.

**Why it's wrong:** ASP.NET Core Identity internals query users during login, email confirmation, and password reset without passing group context. Silent query filter failures break authentication entirely.

**Do this instead:** Leave `UserEntity` unfiltered. Access control is enforced via role checks (`GroupRole` on `UserGroupEntity`), not via query filters.

### Referencing the Old EuphoriaInn Namespace

**What happens:** Old documentation or external scripts reference `EuphoriaInn.*` namespaces after the v5.0 multi-tenancy milestone.

**Why it's wrong:** All classes were renamed to `QuestBoard.*` in the namespace migration. Imports will fail; type reflection breaks.

**Do this instead:** Use `QuestBoard.Service`, `QuestBoard.Domain`, `QuestBoard.Repository` exclusively. The old `EuphoriaInn.*` directories may still exist in the repo (for legacy test fixtures), but all active code uses the new names.

## Error Handling

**Strategy:** Layered error responses with minimal end-user exposure.

**Patterns:**
- **Authorization failures** (403): `[Authorize]` attributes and policy-based handlers short-circuit before business logic runs.
- **Validation failures** (400): `ModelState.IsValid` checks in controllers; AutoMapper projections are conservative (nulls rather than exceptions).
- **Resource not found** (404): Repository returns `null` on missing ID; controller returns `NotFound()`.
- **Database failures** (500): Unhandled exceptions propagate to ASP.NET Core error handler middleware (`app.UseExceptionHandler("/Error")`).
- **Hangfire job failures** (logged, retried): AutomaticRetryAttribute with exponential backoff (5 attempts, 1/2/4/8/16 seconds). Failed job details visible in Hangfire dashboard (SuperAdmin only).
- **Rate limit violations** (429): `EnableRateLimiting` on actions; custom handler writes "Too many requests" response.

## Cross-Cutting Concerns

**Logging:** `ILogger<T>` injected into services/jobs; logged to console + configured providers (appsettings.json).

**Validation:** 
- **Input validation:** ViewModels use `[Required]`, `[StringLength]`, `[Range]` attributes; server-side model binding checks.
- **Business rule validation:** Domain services enforce invariants (e.g., cannot add user to group twice; check `GroupService.AddMemberAsync`).

**Authentication:** ASP.NET Core Identity + session cookies. SuperAdmin role checked via `User.IsInRole("SuperAdmin")` in middleware and controllers. GroupRole checked via `IUserService.GetEffectiveGroupRoleAsync(User, activeGroupContext.RequireActiveGroupId())`.

**Authorization:** 
- **"DungeonMasterOnly"** policy: `DungeonMasterRequirement` handler checks if user is DM (GroupRole.DungeonMaster or GroupRole.Admin) in active group, or SuperAdmin.
- **"AdminOnly"** policy: `AdminRequirement` handler checks if user is group admin (GroupRole.Admin) or SuperAdmin.
- **"SuperAdminOnly"** policy: `policy.RequireRole("SuperAdmin")` (built-in policy).

---

*Architecture analysis: 2026-07-02*
