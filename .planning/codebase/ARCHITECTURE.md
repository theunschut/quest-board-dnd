<!-- refreshed: 2026-07-01 -->
# Architecture

**Analysis Date:** 2026-07-01

## System Overview

```text
┌─────────────────────────────────────────────────────────────────┐
│                    HTTP Request / Response                       │
├──────────────────┬──────────────────┬──────────────────┬─────────┤
│   Controllers    │    Middleware    │  Authorization   │  Jobs   │
│ `Controllers/`   │ `Middleware/`    │ `Authorization/` │`Jobs/`  │
├──────────────────┴──────────────────┴──────────────────┴─────────┤
│                    QuestBoard.Service Layer                      │
│ ViewModels, Razor Views, AutoMapper (ViewModel ↔ DomainModel)  │
│ Email Rendering, Background Job Dispatchers, Group Session      │
└────────────────────────┬─────────────────────────────────────────┘
                         │
         ┌───────────────┴───────────────┐
         ▼                               ▼
┌──────────────────────────┐   ┌──────────────────────────┐
│  QuestBoard.Domain Layer │   │  Cross-Cutting: Hangfire │
│  Business Services       │   │  Background Job Runner   │
│  Domain Models & Enums   │   │  SQL Server Job Queue    │
│ `Services/`, `Models/`   │   │ (separate execution ctx) │
│ `Interfaces/`            │   │                          │
└──────────────┬───────────┘   └──────────────────────────┘
               │
               ▼
┌──────────────────────────────────────────────────────────┐
│         QuestBoard.Repository Layer                       │
│  Entity Framework Core, DbContext, EF Migrations         │
│  Repositories, Entity Mapping (Entity ↔ DomainModel)    │
│  `Entities/`, `Migrations/`, `BaseRepository.cs`        │
├──────────────────────────────────────────────────────────┤
│                SQL Server Database                        │
│  Tables: Quests, Users, PlayerSignups, ShopItems, etc.  │
└──────────────────────────────────────────────────────────┘
```

## Component Responsibilities

| Component | Responsibility | File |
|-----------|----------------|------|
| **QuestController** | Quest listing, creation, finalization, date changes | `QuestBoard.Service/Controllers/QuestBoard/QuestController.cs` |
| **AccountController** | User login, registration, password reset, email confirmation | `QuestBoard.Service/Controllers/Admin/AccountController.cs` |
| **AdminController** | User management, role assignments, email stats | `QuestBoard.Service/Controllers/Admin/AdminController.cs` |
| **ShopController** | Player shop browsing, item purchase, gold transactions | `QuestBoard.Service/Controllers/Shop/ShopController.cs` |
| **ShopManagementController** | DM/Admin item creation, editing, deletion | `QuestBoard.Service/Controllers/Shop/ShopManagementController.cs` |
| **GroupSessionMiddleware** | Session group context validation, redirect to group picker if expired | `QuestBoard.Service/Middleware/GroupSessionMiddleware.cs` |
| **QuestService** | Quest business logic: finalize, retrieve with filtered data, email dispatch | `QuestBoard.Domain/Services/QuestService.cs` |
| **PlayerSignupService** | Player signup registration, vote casting, signup withdrawal | `QuestBoard.Domain/Services/PlayerSignupService.cs` |
| **QuestRepository** | Quest EF Core queries, get with details/signup, state mutations | `QuestBoard.Repository/QuestRepository.cs` |
| **BaseRepository** | Generic CRUD: Add, Get, Update, Remove, AutoMapper mapping | `QuestBoard.Repository/BaseRepository.cs` |
| **QuestBoardContext** | EF Core DbContext, schema modeling, OnModelCreating config, multi-group filtering | `QuestBoard.Repository/Entities/QuestBoardContext.cs` |
| **ActiveGroupContextService** | Reads session group ID for HTTP requests; overridden by Hangfire jobs | `QuestBoard.Service/Services/ActiveGroupContextService.cs` |
| **HangfireQuestEmailDispatcher** | Enqueues quest email jobs (finalized, date changed) to SQL Server queue | `QuestBoard.Service/Services/HangfireQuestEmailDispatcher.cs` |
| **QuestFinalizedEmailJob** | Hangfire background job — renders email template, calls email service | `QuestBoard.Service/Jobs/QuestFinalizedEmailJob.cs` |

## Pattern Overview

**Overall:** Three-layer clean architecture with strict one-way dependency: **Service → Domain → Repository**.

**Key Characteristics:**
- **Unidirectional dependency flow** — Service depends on Domain; Domain depends on Repository; Repository never references Service
- **Domain as contract layer** — Interfaces in Domain define boundaries; Service and Repository implement them
- **AutoMapper at two boundaries** — Entity ↔ DomainModel (Repository layer) and DomainModel ↔ ViewModel (Service layer)
- **Multi-tenancy via group context** — ActiveGroupContext scoped to request (via session) or job execution (via override); DB queries filtered by GroupId
- **Background jobs via Hangfire** — Email and reminder jobs enqueued as SQL Server durable jobs, executed asynchronously with dedicated thread pool

## Layers

**Service Layer (QuestBoard.Service):**
- Purpose: HTTP request handling, authorization, view model transformation, background job dispatch
- Location: `QuestBoard.Service/`
- Contains: MVC Controllers, Razor Views, ViewModels, Authorization handlers, Middleware, Jobs, AutoMapper ViewModelProfile, Services (Hangfire dispatcher, email renderer, group context)
- Depends on: Domain (service interfaces, enums, domain models), ASP.NET Core framework, Hangfire client
- Used by: Browser clients via HTTP/MVC routing

**Domain Layer (QuestBoard.Domain):**
- Purpose: Business logic, domain models, service/repository contracts, cross-cutting enums
- Location: `QuestBoard.Domain/`
- Contains: Service implementations (business rules, email dispatch coordination), Domain models (Quest, PlayerSignup, User, etc.), Service/Repository interfaces, Enums (Role, SignupRole, CharacterStatus, etc.), Extensions
- Depends on: Repository (repository interfaces), AutoMapper
- Used by: Service layer (calls services), Repository layer (implements repositories)

**Repository Layer (QuestBoard.Repository):**
- Purpose: Data access, EF Core entity modeling, database schema definition, AutoMapper EntityProfile
- Location: `QuestBoard.Repository/`
- Contains: EF Core entities (QuestEntity, UserEntity, etc.), QuestBoardContext (DbContext), Repository implementations (QuestRepository, CharacterRepository, etc.), EF Core Migrations, AutoMapper EntityProfile
- Depends on: Domain (domain models, interfaces), EF Core, SQL Server
- Used by: Domain layer (called by service implementations)

## Data Flow

### Primary Request Path: Quest Creation (POST /Quest/Create)

1. **Controller Entry** (`QuestController.Create(POST)` — `QuestBoard.Service/Controllers/QuestBoard/QuestController.cs:76`)
   - Extracts QuestViewModel from request body
   - Resolves injected `IQuestService` (Domain service interface)
   - Validates ViewModel state

2. **Service Processing** (`QuestService.AddAsync()` — `QuestBoard.Domain/Services/QuestService.cs`)
   - Inherits from `BaseService<Quest>` which wraps repository calls
   - Calls `IQuestRepository.AddAsync(domainModel)` to persist

3. **AutoMapper Transformation** (Domain → Entity)
   - `EntityProfile.cs` maps Quest (domain model) → QuestEntity (EF entity)
   - OriginalQuest/FollowUpQuest mapped shallowly to avoid AutoMapper recursion
   - Controller property mapping: ProposedDates list → individual DateTime objects

4. **Repository Persistence** (`QuestRepository.AddAsync()` — `QuestBoard.Repository/QuestRepository.cs:18`)
   - Maps domain Quest to QuestEntity via AutoMapper
   - Attaches entity to DbSet<QuestEntity>
   - Calls SaveChangesAsync() to flush to SQL Server
   - Propagates DB-generated Id back to domain model for immediate use

5. **View Response**
   - Controller redirects to `QuestController.Index(GET)`
   - Repository query fetches updated quests with signups (projected via `ProjectWithoutCharacterImages()`)
   - AutoMapper maps QuestEntity collection back to Quest domain models
   - ViewModelProfile maps Quest → QuestViewModel for display
   - Razor view renders quest list

### Background Job Path: Quest Finalized Email (Hangfire)

1. **Job Enqueue** (occurs in Service layer during quest finalization)
   - `QuestService.FinalizeQuestAsync()` calls `IQuestEmailDispatcher.EnqueueFinalizedEmail(...questId, groupId, ...)`
   - Concrete implementation: `HangfireQuestEmailDispatcher.EnqueueFinalizedEmail()` → `IBackgroundJobClient.Enqueue<QuestFinalizedEmailJob>()`
   - Job is serialized to SQL Server Hangfire job table with questId, groupId, recipient emails

2. **Hangfire Background Execution** (separate thread, no HTTP context)
   - Hangfire worker thread deserializes job and calls `QuestFinalizedEmailJob.ExecuteAsync(questId, groupId, ...)`
   - Service scope created; `ActiveGroupContextService.SetGroupId(groupId)` called to establish context (D-09)
   - Scope-injected `IQuestRepository` queries for quest; repository filter applies GROUP BY clause to respect multi-tenancy
   - Email renderer resolves `IEmailRenderService` to render Razor email template (QuestFinalized component)
   - `IEmailService` sends via Resend API

3. **Result Tracking**
   - `repository.SetFinalizedEmailSentForDateAsync()` records date email was sent to prevent duplicate sends

### Group Context Flow (Multi-Tenancy)

1. **HTTP Request** — `GroupSessionMiddleware` resolves `IActiveGroupContext` from DI container
   - Context reads `Session[SessionKeys.ActiveGroupId]` (int? or null)
   - If null and not exempt path → redirect to group picker (`/groups/pick`)

2. **Domain/Repository Queries** — Queries automatically filtered by `GroupId`
   - QuestBoardContext receives injected `IActiveGroupContext` in constructor
   - OnModelCreating or per-query uses `activeGroupContext.ActiveGroupId` to generate WHERE clauses
   - Example: `Quests.Where(q => q.GroupId == activeGroupContext.ActiveGroupId)`

3. **Hangfire Job Execution** — No HTTP context (Session unavailable)
   - Job receives explicit `groupId` parameter
   - `ActiveGroupContextService.SetGroupId(groupId)` sets override before any repository resolution
   - Same group filtering applies

**State Management:**
- HTTP request state: Session (user, group context), HttpContext
- Background job state: Hangfire SQL Server job queue, domain model snapshots passed to job
- Transient state within request: Scoped DI services (QuestBoardContext, repositories, services)
- Persistent state: SQL Server (all entities, Hangfire job queue, migrations)

## Key Abstractions

**IBaseRepository<T> / IBaseService<T>:**
- Purpose: Define standard CRUD contract and implementation pattern
- Examples: `IQuestRepository : IBaseRepository<Quest>`, `QuestService : BaseService<Quest>`
- Pattern: Generic base + specialized repo/service for complex queries (GetQuestsWithDetailsAsync, FinalizeQuestAsync)

**IQuestEmailDispatcher:**
- Purpose: Decouple Domain service from Service-layer job infrastructure (Hangfire)
- Examples: `HangfireQuestEmailDispatcher` (production), `NullQuestEmailDispatcher` (testing)
- Pattern: Domain QuestService calls dispatcher.EnqueueFinalizedEmail(); implementation decides if async (Hangfire) or no-op

**IActiveGroupContext:**
- Purpose: Inject current group context into QuestBoardContext so queries filter by group; works across HTTP and Hangfire
- Implementation: `ActiveGroupContextService` reads session for HTTP, accepts override for jobs
- Pattern: Scoped DI service shared between QuestBoardContext and Hangfire jobs via SetGroupId()

**IEntity / IModel:**
- Purpose: Marker interfaces to bind generic repository CRUD to concrete types
- Usage: All EF entities implement IEntity; all domain models implement IModel

## Entry Points

**HTTP Entry Point (MVC Routing):**
- Location: `QuestBoard.Service/Program.cs:281-283` (default route)
- Triggers: Browser requests to `/Quest`, `/Shop`, `/Account`, etc.
- Responsibilities: Route to controller → invoke action → resolve dependencies → call domain services

**Area Entry Point (Platform/Group Management):**
- Location: `QuestBoard.Service/Program.cs:276-279` (area route)
- Pattern: `/platform/{controller}/{action}/{id}` 
- Responsibilities: Group CRUD, tenant isolation setup

**Health Check Entry Point:**
- Location: `QuestBoard.Service/Program.cs:285` (/health)
- Responsibilities: Liveness probe for container orchestration

**Hangfire Recurring Job Entry Point:**
- Location: `QuestBoard.Service/Program.cs:297-300` (DailyReminderJob, runs at 09:00 server time)
- Triggers: Cron schedule; Hangfire scheduler invokes job via SQL Server
- Responsibilities: Sweep for sessions due reminders, enqueue email jobs

**Database Entry Point:**
- Location: `QuestBoard.Service/Program.cs:290` (ConfigureDatabase)
- Triggers: App startup (non-Testing environments)
- Responsibilities: Apply EF Core migrations auto (context.Database.Migrate()), seed shop data

## Architectural Constraints

- **Threading:** Single-threaded per HTTP request (ASP.NET Core async/await); Hangfire runs 2 background worker threads (Program.cs:202)
- **Global state:** `ActiveGroupContextService` scoped per request/job (not truly global); DI container is thread-safe singleton; no other module-level singletons beyond DI and logging factories
- **Circular imports:** BaseService/BaseRepository use generics to avoid hard references; Domain interfaces prevent Service→Repository direct calls
- **EF Tracking:** Repositories use AutoMapper to map tracked entities to domain models; domain models are untracked DTOs, mutations require re-query before update
- **Multi-tenancy filter:** GroupId is always a required FK on quest/signup/shop entities; queries MUST filter by ActiveGroupId or behave as "null = see all" (SuperAdmin bypass)
- **Authorization:** Done via ASP.NET Core [Authorize] attributes and `GroupSessionMiddleware`; no domain-layer authorization logic
- **Dependency Injection:** Services registered in Program.cs:145-211; Scoped lifetime for QuestBoardContext, repositories, services; Hangfire background jobs use `CreateAsyncScope()` to resolve fresh instances

## Anti-Patterns

### AutoMapper Circular Recursion on Related Entities

**What happens:** Quest → QuestEntity maps OriginalQuest/FollowUpQuest recursively, causing infinite loop if AutoMapper isn't configured to stop.

**Why it's wrong:** Infinite recursion exhausts stack; circular data fetches bloat response payloads; unclear what depth to fetch.

**Do this instead:** Map related entities shallowly (Id + Title only) in EntityProfile.cs. If full details needed, fetch separately and compose in service layer.
- Example: `QuestBoard.Repository/Automapper/EntityProfile.cs:18-26`

### Missing Group Context in Background Jobs

**What happens:** Hangfire jobs enqueue without groupId; background execution has no group context; queries return all groups instead of tenant-isolated data.

**Why it's wrong:** Multi-tenancy isolation violated; emails sent to wrong groups; data leakage across tenants.

**Do this instead:** Always pass groupId to job enqueue method; job constructor receives explicit groupId; call `ActiveGroupContextService.SetGroupId(groupId)` before any repository resolution. This is centralized in `HangfireJobHelper.RunInScopeAsync`, which every job now calls instead of setting up its own scope inline.
- Example: `QuestBoard.Service/Jobs/QuestFinalizedEmailJob.cs:15-16` (groupId parameter), `QuestBoard.Service/Jobs/HangfireJobHelper.cs` (SetGroupId call, centralized)

### Repository Mutable State Without Re-Query

**What happens:** Service updates a domain model, calls repository.UpdateAsync(); downstream code reads cached domain model that has stale related data.

**Why it's wrong:** Observer of domain model sees inconsistent state; downstream mutations based on stale data cause silent bugs.

**Do this instead:** After update, re-fetch from repository to load fresh related entities.
- Example: `QuestBoard.Domain/Services/QuestService.cs:20-21` (FinalizeQuestAsync re-fetches post-save)

### Service-Layer Repository Direct References

**What happens:** Service injects `QuestRepository` (concrete class) instead of `IQuestRepository` (interface).

**Why it's wrong:** Breaks layering; Service layer couples to Repository implementation details; testing becomes harder (can't swap mock).

**Do this instead:** Always inject interfaces defined in Domain (IQuestRepository, IQuestService, etc.); repository classes are internal to Repository project.
- Example: Controllers inject `IQuestService` (`QuestBoard.Service/Controllers/QuestBoard/QuestController.cs:17`)

## Deferred Hardening / Scaling Notes

### Tenant-isolation defense-in-depth (deferred)

The EF Core Global Query Filter on `QuestEntity` and `ShopItemEntity` provides working cross-tenant protection today — a quest or shop item belonging to another group is not returned to a controller under normal navigation, because every query is transparently scoped by `ActiveGroupId`.

Per-controller `Forbid()` defense-in-depth checks (comparing a loaded entity's `GroupId` against the active group before returning it) are a deferred hardening idea for a future phase. It is not implemented now because the footprint of adding and testing this check across every controller action rivals the size of a full controller refactor, and the query filter already closes the practical risk. If a future phase enforces Group Picker selection at every entry point without gaps, this becomes pure defense-in-depth rather than the primary safety net; until then it remains a documented, accepted residual risk.

### Hangfire fan-out is intentional

`DailyReminderJob` correctly fetches all groups' due quests once (`GetQuestsForTomorrowAllGroupsAsync`), then enqueues one small `SessionReminderJob` per quest. This fan-out pattern gives:
- Parallel worker processing — multiple small jobs can run concurrently across Hangfire's worker pool
- Isolated per-job retry — one quest's email failure does not block or retry every other quest's reminder
- Horizontal scaling — additional Hangfire server instances increase throughput without code changes

A monolithic `SessionReminderBatchJob` that processed all quests in one job would lose all three properties. Batching is a monitored future scaling path — worth revisiting only if the Hangfire queue depth grows into the thousands — not a current problem at this app's scale.

### Dependencies at risk

All Identity-adjacent emails (password reset, welcome/confirmation) route through Hangfire jobs that call `IEmailService` directly — ASP.NET Core Identity's built-in email sender is not used anywhere in this codebase. This avoids the risk of Identity silently falling back to an unconfigured default sender.

The Resend SMTP relay is a single point of failure: there is no secondary relay configured, and `SmtpClient` throws immediately on connection failure rather than retrying internally. The practical mitigation is Hangfire's own job retry (bounded via `AutomaticRetryAttribute`), not a fallback relay — if Resend is down for longer than the retry window allows, jobs land in the Failed Jobs queue for manual admin retry from the Hangfire dashboard.

## Error Handling

**Strategy:** Mix of exception propagation and data validation.

**Patterns:**
- **Validation errors:** [Authorize] attributes + ModelState in controllers; return View with ModelState.AddModelError()
- **Business rule violations:** Domain services throw InvalidOperationException (e.g., duplicate follow-up quest, no eligible signups)
- **Database errors:** EF Core DbUpdateException caught in repository specializations (e.g., QuestRepository.AddAsync checks for duplicate OriginalQuestId constraint)
- **Authentication failures:** [Authorize] triggers Challenge(); GroupSessionMiddleware returns 409 Conflict if group context missing for non-idempotent requests
- **Rate limiting:** RateLimiter middleware (Program.cs:105-135) returns 429 TooManyRequests for /forgot-password and /set-password endpoints

## Cross-Cutting Concerns

**Logging:** ILogger<T> injected via DI; used in Program.cs for migrations, repositories for deduplication logic (QuestFinalizedEmailJob.cs:43)

**Validation:** 
- [Required], [StringLength] annotations on domain models
- ModelState checks in controllers
- Repository-level uniqueness constraints (PlayerDateVote index on (PlayerSignupId, ProposedDateId) — EntityProfile.cs:85-87)

**Authentication:** 
- ASP.NET Core Identity (UserEntity, UserManager, SignInManager)
- [Authorize] controller attributes
- Role-based policies: "DungeonMasterOnly", "AdminOnly", "SuperAdminOnly"

**Authorization:**
- DungeonMasterHandler / AdminHandler (Program.cs:74-75) — check User.IsInRole()
- GroupSessionMiddleware (Middleware/GroupSessionMiddleware.cs) — enforce group context presence
- AdminDashboardAuthFilter (Authorization/AdminDashboardAuthFilter.cs) — Hangfire dashboard access check

**Email Dispatch:**
- IQuestEmailDispatcher interface (Domain contract)
- HangfireQuestEmailDispatcher implementation (Service layer, calls Hangfire)
- Background jobs render Razor components, call IEmailService (Resend API)

---

*Architecture analysis: 2026-07-01*
