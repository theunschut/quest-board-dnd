# Phase 28: Tenant Isolation - Research

**Researched:** 2026-06-30
**Domain:** EF Core Global Query Filters + ASP.NET Core Scoped DI + Hangfire background jobs
**Confidence:** HIGH

---

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

- **D-01:** `IActiveGroupContext` in `QuestBoard.Domain/` — `int? ActiveGroupId { get; }` (read-only in Phase 28).
- **D-02:** `ActiveGroupContextService` in `QuestBoard.Service/` reads `ActiveGroupId` from `IHttpContextAccessor.HttpContext?.Session?.GetInt32("ActiveGroupId")`. Null when HttpContext is absent (Hangfire) — graceful via `?.` chain, no throw.
- **D-03:** Filter: `context.ActiveGroupId == null || e.GroupId == context.ActiveGroupId`. Applied in `OnModelCreating` via `HasQueryFilter` on `QuestEntity` and `ShopItemEntity`. `UserEntity` does NOT receive a filter.
- **D-04:** `IActiveGroupContext` constructor-injected into `QuestBoardContext`. Both `QuestBoardContext` and `ActiveGroupContextService` are Scoped. Repository layer receives the Domain interface, not the concrete Service implementation.
- **D-05:** Null = see all (Phase 28 intentional temporary state). No HTTP context or session not yet set → all quests/shop items pass the filter.
- **D-06:** `QuestFinalizedEmailJob.ExecuteAsync` and `SessionReminderJob.ExecuteAsync` each receive explicit `int groupId` parameter. Callers set scoped `IActiveGroupContext` to that `groupId` before repo query runs.
- **D-07:** `ConfirmationEmailJob` and `QuestDateChangedEmailJob` require no changes — neither queries Quests or ShopItems tables.
- **D-08:** Daily CRON sweep job calls new dedicated `GetQuestsForTomorrowAllGroupsAsync()` with `.IgnoreQueryFilters()` internally.
- **D-09:** Concrete `ActiveGroupContextService` class (not the interface) exposes `void SetGroupId(int? groupId)`. Jobs inject `ActiveGroupContextService` directly and call `SetGroupId(groupId)` before any repo call.
- **D-10:** `WebApplicationFactoryBase` gets a public `TestGroupContext` property of type `MutableGroupContext` implementing `IActiveGroupContext`. Default `ActiveGroupId = 1`.
- **D-11:** `MutableGroupContext` registered as Singleton in `ConfigureTestServices`: `services.AddSingleton<IActiveGroupContext>(factory.GroupContext)`.
- **D-12:** Integration tests use `UseInMemoryDatabase` — NOT SQLite. Do not create SQLite-specific test infrastructure.

### Claude's Discretion

- Session key constant name and location (e.g., `SessionKeys.ActiveGroupId = "ActiveGroupId"` in `QuestBoard.Service/` or `QuestBoard.Domain/Constants/`).
- DI registration lifetime for `ActiveGroupContextService` — Scoped (must match `QuestBoardContext`).
- `MutableGroupContext` placement — either in `QuestBoard.IntegrationTests/` or as an inner class in `WebApplicationFactoryBase`.

### Deferred Ideas (OUT OF SCOPE)

- Phase 29 filter tightening: Add `bool IsSuperAdmin { get; }` to `IActiveGroupContext`; update predicate to `context.IsSuperAdmin || context.ActiveGroupId == null || e.GroupId == context.ActiveGroupId`.
- Phase 29: Update `ActiveGroupContextService` to return `IsSuperAdmin = true` when user holds SuperAdmin Identity role.
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| TENANT-01 | `IActiveGroupContext` interface defined in Domain layer with `ActiveGroupId` property (`int?`) | D-01; constructor injection into `QuestBoardContext` is the standard EF Core pattern for runtime-varying filter values |
| TENANT-02 | `ActiveGroupContextService` in Service layer implements `IActiveGroupContext`; reads from ASP.NET Core Session; returns null when user holds SuperAdmin role (Phase 29 tightens this) | D-02, D-05; Session.GetInt32 + IHttpContextAccessor nullable chain |
| TENANT-03 | EF Core Global Query Filters on `QuestEntity` and `ShopItemEntity`; `UserEntity` excluded | D-03, D-04; `HasQueryFilter` in `OnModelCreating`; verified InMemory provider honors filters |
| TENANT-04 | All four Hangfire jobs bypass filter or receive explicit groupId; cross-group sweep uses `IgnoreQueryFilters()` | D-06, D-07, D-08, D-09; `SetGroupId` pattern on concrete class; `GetQuestsForTomorrowAllGroupsAsync` with `IgnoreQueryFilters()` |
| TENANT-05 | Integration test factory registers stub `IActiveGroupContext` returning GroupId = 1; all 191 tests pass | D-10, D-11, D-12; Singleton stub via `ConfigureTestServices`; `TestDatabase` constructor fix required |
</phase_requirements>

---

## Summary

Phase 28 wires up the multi-tenancy filtering infrastructure for the Quest Board. The core mechanism is an EF Core Global Query Filter on `QuestEntity` and `ShopItemEntity` that reads from a scoped `IActiveGroupContext` — returning all records when `ActiveGroupId` is null (the Phase 28 default), or filtering to the matching group otherwise.

The key implementation challenge is that `QuestBoardContext` is constructed by the EF Core internal DI container — which is a problem when injecting a scoped application service. The solution is to register `IActiveGroupContext` as a standard Scoped service alongside `QuestBoardContext`, and inject it into `QuestBoardContext` via constructor injection. EF Core's `AddDbContext` is aware of the application service provider and will resolve the scoped `IActiveGroupContext` from the same request scope as the `QuestBoardContext`, making them effectively co-scoped. This is the canonical EF Core multi-tenancy pattern documented by Microsoft.

Hangfire jobs already use `IServiceScopeFactory.CreateAsyncScope()` uniformly — this phase extends that pattern. For jobs that query the Quests table (`QuestFinalizedEmailJob`, `SessionReminderJob`), the concrete `ActiveGroupContextService` is resolved from the job's scope and `SetGroupId(groupId)` is called before any repository call. The daily CRON sweep bypasses the filter via `IgnoreQueryFilters()` on a new dedicated repository method. The test factory gets a singleton `MutableGroupContext` instance that defaults to `GroupId = 1`.

A critical landmine is the `TestDatabase` helper: it calls `new QuestBoardContext(_options)` directly. After Phase 28 adds `IActiveGroupContext` as a second constructor parameter, this bare `new` call will not compile. The fix is to either also pass a `new MutableGroupContext { ActiveGroupId = null }` instance, or change `TestDatabase` to use the DI-resolved context. This is the most likely test-breakage vector.

**Primary recommendation:** Implement in a single wave — Domain interface, Service implementation, DI registration, `QuestBoardContext` filter wiring, job adaptation, repository method, and test stub must all land together because a partial implementation produces compile errors or test failures in 4 of the 5 pieces.

---

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| `IActiveGroupContext` interface definition | Domain | — | `QuestBoardContext` lives in Repository which depends on Domain; interface must be in Domain to avoid circular dep |
| `ActiveGroupContextService` (reads Session) | Service | — | `IHttpContextAccessor` is ASP.NET Core — belongs in Service layer, not Domain |
| EF Core query filter application | Repository (`QuestBoardContext`) | — | `HasQueryFilter` is in `OnModelCreating`; `QuestBoardContext` owns it |
| Hangfire job groupId parameter flow | Service (Jobs) | Domain (IQuestEmailDispatcher) | Jobs live in Service; callers pass groupId through dispatcher interface |
| Cross-group repository sweep | Repository (`QuestRepository`) | — | `IgnoreQueryFilters()` is an EF Core repository concern |
| Integration test stub | IntegrationTests | — | `MutableGroupContext` is test infrastructure only |
| Session key constant | Service or Domain/Constants | — | Discretion; both are valid; needs single-definition rule |

---

## Standard Stack

### Core

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| `Microsoft.EntityFrameworkCore` | 10.0.9 [VERIFIED: project csproj] | ORM with `HasQueryFilter` + `IgnoreQueryFilters` | Already in use; `HasQueryFilter` is the canonical EF Core multi-tenancy mechanism |
| `Microsoft.EntityFrameworkCore.InMemory` | 10.0.9 [VERIFIED: project csproj] | In-memory test DB that honors query filters | Already in integration test project |
| `Microsoft.AspNetCore.Http` (built-in) | .NET 10 | `IHttpContextAccessor` for reading Session in `ActiveGroupContextService` | Built-in; already used in Service layer |

### Supporting

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| `ISession.GetInt32(key)` (built-in) | .NET 10 | Reading `ActiveGroupId` from ASP.NET Core Session | Used inside `ActiveGroupContextService` |
| `IServiceScopeFactory.CreateAsyncScope()` (built-in) | .NET 10 | Creating per-job DI scopes in Hangfire jobs | Already established pattern in all four existing jobs |

No new NuGet packages are required for this phase. All capabilities are in libraries already present.

---

## Architecture Patterns

### System Architecture Diagram

```
HTTP Request
    │
    ▼
[ActiveGroupContextService]──reads──▶ ISession("ActiveGroupId") ──▶ int? value
    │ implements
    ▼
[IActiveGroupContext]
    │ injected via constructor
    ▼
[QuestBoardContext.OnModelCreating]
    │ HasQueryFilter
    ▼
QuestEntity filter: ActiveGroupId == null OR e.GroupId == ActiveGroupId
ShopItemEntity filter: ActiveGroupId == null OR e.GroupId == ActiveGroupId
(UserEntity: NO filter)

─────────────────────────────────────────────────────────

Hangfire Background Thread (no HttpContext)
    │
    ▼
IServiceScopeFactory.CreateAsyncScope()  ← existing pattern
    │
    ├──▶ Resolve ActiveGroupContextService (concrete, from job scope)
    │        └── SetGroupId(groupId)  ← set before any repo call
    │
    └──▶ Resolve IQuestRepository
             └── DbContext.Quests  ← filter resolves with set groupId

─────────────────────────────────────────────────────────

Hangfire CRON Sweep (DailyReminderJob)
    │
    ▼
QuestRepository.GetQuestsForTomorrowAllGroupsAsync()
    │ .IgnoreQueryFilters()
    ▼
Returns ALL groups' finalized quests for tomorrow
    │
    ▼ (foreach quest)
Enqueue SessionReminderJob(questId, groupId: quest.GroupId)

─────────────────────────────────────────────────────────

Integration Tests
    │
    ▼
WebApplicationFactoryBase.TestGroupContext (MutableGroupContext)
    │ AddSingleton<IActiveGroupContext>(factory.GroupContext)
    ▼
All test requests see GroupId = 1 (default)
    │ tests can override
    ▼
factory.GroupContext.ActiveGroupId = 2  ← per-test override
```

### Recommended Project Structure

New files to create:

```
QuestBoard.Domain/
└── Interfaces/
    └── IActiveGroupContext.cs          # TENANT-01

QuestBoard.Service/
├── Services/
│   └── ActiveGroupContextService.cs   # TENANT-02 (SetGroupId + Session read)
└── Constants/                         # (or Domain/Constants/)
    └── SessionKeys.cs                 # Claude's discretion — single place for key constant

QuestBoard.Repository/
└── QuestRepository.cs                 # Add GetQuestsForTomorrowAllGroupsAsync()

QuestBoard.IntegrationTests/
└── Helpers/
    └── MutableGroupContext.cs         # (or inner class in WebApplicationFactoryBase)
```

Modified files:

```
QuestBoard.Repository/Entities/QuestBoardContext.cs   # inject IActiveGroupContext, add HasQueryFilter
QuestBoard.Repository/Extensions/ServiceExtensions.cs # no change needed (AddDbContext already Scoped)
QuestBoard.Domain/Extensions/ServiceExtensions.cs     # no change (ActiveGroupContextService is Service-layer)
QuestBoard.Service/Program.cs                         # register ActiveGroupContextService as Scoped
QuestBoard.Service/Jobs/QuestFinalizedEmailJob.cs     # add groupId param, SetGroupId
QuestBoard.Service/Jobs/SessionReminderJob.cs         # add groupId param, SetGroupId
QuestBoard.Service/Jobs/DailyReminderJob.cs           # pass quest.GroupId to enqueued SessionReminderJob
QuestBoard.Service/Services/HangfireReminderJobDispatcher.cs  # pass groupId through
QuestBoard.Domain/Interfaces/IQuestRepository.cs      # add GetQuestsForTomorrowAllGroupsAsync
QuestBoard.IntegrationTests/WebApplicationFactoryBase.cs  # add TestGroupContext, register singleton
QuestBoard.IntegrationTests/Helpers/TestDatabase.cs   # fix bare new QuestBoardContext() call
```

### Pattern 1: HasQueryFilter with Constructor-Injected Scoped Service

**What:** Inject `IActiveGroupContext` into `QuestBoardContext` via constructor. Reference the captured instance in the `HasQueryFilter` lambda. EF Core evaluates the lambda per-query against the current instance's `ActiveGroupId` value.

**When to use:** Whenever a query filter must vary at runtime based on request context.

**How it works:** `AddDbContext` registers `QuestBoardContext` as Scoped. At request scope creation, ASP.NET Core DI resolves both `ActiveGroupContextService` (Scoped) and `QuestBoardContext` (Scoped) — since they share the same scope, the same `ActiveGroupContextService` instance is injected into `QuestBoardContext`. The `HasQueryFilter` lambda closes over `this` (the context), so it reads `activeGroupContext.ActiveGroupId` at query execution time, not at model-building time.

**Example:**

```csharp
// Source: Microsoft EF Core docs - https://learn.microsoft.com/en-us/ef/core/querying/filters
// QuestBoard.Repository/Entities/QuestBoardContext.cs

public class QuestBoardContext(
    DbContextOptions<QuestBoardContext> options,
    IActiveGroupContext activeGroupContext)         // <-- injected
    : IdentityDbContext<UserEntity, IdentityRole<int>, int>(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Existing FK config...

        // TENANT-03: Group query filters
        // Null = see all (Phase 28 intentional; Phase 29 tightens with IsSuperAdmin check)
        modelBuilder.Entity<QuestEntity>()
            .HasQueryFilter(e =>
                activeGroupContext.ActiveGroupId == null ||
                e.GroupId == activeGroupContext.ActiveGroupId);

        modelBuilder.Entity<ShopItemEntity>()
            .HasQueryFilter(e =>
                activeGroupContext.ActiveGroupId == null ||
                e.GroupId == activeGroupContext.ActiveGroupId);

        // UserEntity intentionally excluded — breaks ASP.NET Core Identity
    }
}
```

**Why the lambda captures correctly:** The `activeGroupContext` variable is a constructor parameter captured in the class's scope. Because `QuestBoardContext` is Scoped, its constructor runs once per request. The lambda closure over `activeGroupContext` means EF Core re-reads `.ActiveGroupId` from the instance on every query within that scope, which is correct.

### Pattern 2: Hangfire Job SetGroupId Before Repository Call

**What:** Jobs inject the concrete `ActiveGroupContextService` (not the interface) to access `SetGroupId()`. This is called immediately after scope creation, before any repository call.

**When to use:** Any Hangfire job that queries `Quests` or `ShopItems` tables.

**Example:**

```csharp
// Source: Project pattern (consistent with existing IServiceScopeFactory usage in all 4 jobs)
// QuestBoard.Service/Jobs/QuestFinalizedEmailJob.cs

public async Task ExecuteAsync(
    int questId,
    int groupId,                  // <-- new parameter (D-06)
    DateTime finalizedDate,
    // ... other params
    CancellationToken cancellationToken = default)
{
    await using var scope = scopeFactory.CreateAsyncScope();

    // D-09: inject concrete service (not interface) to access SetGroupId
    var groupContext = scope.ServiceProvider.GetRequiredService<ActiveGroupContextService>();
    groupContext.SetGroupId(groupId);    // <-- must be before any repo call

    var questRepository = scope.ServiceProvider.GetRequiredService<IQuestRepository>();
    // Now DbContext filter resolves groupId correctly
    var quest = await questRepository.GetQuestWithDetailsAsync(questId, cancellationToken);
    // ...
}
```

### Pattern 3: Cross-Group Repository Method with IgnoreQueryFilters

**What:** The daily sweep needs all groups' quests. A dedicated method name makes cross-group intent explicit and greppable.

**When to use:** Any system-wide sweep operation that must see all tenants' data.

**Example:**

```csharp
// Source: EF Core docs - https://learn.microsoft.com/en-us/ef/core/querying/filters
// QuestBoard.Repository/QuestRepository.cs

public async Task<IList<Quest>> GetQuestsForTomorrowAllGroupsAsync(
    DateTime date,
    CancellationToken token = default)
{
    // D-08: explicit cross-group intent — IgnoreQueryFilters bypasses HasQueryFilter
    var entities = await ProjectWithoutCharacterImages(
        DbContext.Quests.IgnoreQueryFilters())
        .Where(q => q.FinalizedDate.HasValue && q.FinalizedDate.Value.Date == date.Date)
        .ToListAsync(token);
    return Mapper.Map<IList<Quest>>(entities);
}
```

The corresponding Domain interface `IQuestRepository` needs this method added.

### Pattern 4: Integration Test Singleton Stub

**What:** A mutable implementation of `IActiveGroupContext` registered as Singleton in `ConfigureTestServices`. Tests set its `ActiveGroupId` property directly.

**When to use:** Integration tests that need a non-null `ActiveGroupId` to avoid fetching all tenants' data.

**Example:**

```csharp
// QuestBoard.IntegrationTests/Helpers/MutableGroupContext.cs (or inner class)
public class MutableGroupContext : IActiveGroupContext
{
    public int? ActiveGroupId { get; set; } = 1;  // Default = Group 1 (EuphoriaInn)
}

// WebApplicationFactoryBase.cs
public MutableGroupContext TestGroupContext { get; } = new MutableGroupContext();

// In ConfigureTestServices:
services.AddSingleton<IActiveGroupContext>(TestGroupContext);
```

### Anti-Patterns to Avoid

- **Capturing `ActiveGroupId` value in OnModelCreating:** `var id = activeGroupContext.ActiveGroupId; modelBuilder.Entity<QuestEntity>().HasQueryFilter(e => e.GroupId == id);` — this captures the value at startup (null), not per-query. Always capture the service instance reference, not its property value.
- **Registering `ActiveGroupContextService` as Singleton:** Would share session state across all requests. Must be Scoped.
- **Injecting `IActiveGroupContext` as Transient:** Creates a different instance than the one injected into `QuestBoardContext` within the same scope. Must be Scoped (or Singleton for test stub).
- **Adding `HasQueryFilter` to `UserEntity`:** Breaks ASP.NET Core Identity — login, password reset, email confirmation all fail silently.
- **Using `IActiveGroupContext` interface in job (not concrete type):** The interface has no `SetGroupId` method. Jobs must inject `ActiveGroupContextService` (concrete) to call `SetGroupId`.
- **Calling `IgnoreQueryFilters()` without a dedicated method name:** Makes cross-group intent invisible in code review. Always wrap in a method with `AllGroups` in its name.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Runtime query filtering by tenant | Per-repository `.Where(e => e.GroupId == groupId)` on every query | `HasQueryFilter` in `OnModelCreating` | One place to change; impossible to forget on a new query; EF handles SQL translation |
| Bypassing tenant filter for system jobs | Manual if-else on groupId == null | `.IgnoreQueryFilters()` on specific queries | EF Core built-in; clear intent; works through navigation includes |
| Scoped service in background thread | Thread-local or static | `IServiceScopeFactory.CreateAsyncScope()` | Already established pattern in all 4 existing jobs; correct lifetime management |

**Key insight:** The entire point of `HasQueryFilter` is to make filtering automatic and invisible. Hand-rolling per-repository WHERE clauses defeats this — one missed call means a data leak.

---

## Common Pitfalls

### Pitfall 1: TestDatabase Direct `new QuestBoardContext()` Constructor Call

**What goes wrong:** `TestDatabase.cs` line 16 calls `new QuestBoardContext(_options)` with only `DbContextOptions`. After adding `IActiveGroupContext` as a second constructor parameter, this line will not compile.

**Why it happens:** `TestDatabase` predates the IActiveGroupContext injection. It creates a raw context instance outside DI.

**How to avoid:** Fix `TestDatabase.CreateContext()` to pass a `MutableGroupContext` instance:

```csharp
// In TestDatabase.cs
private readonly IActiveGroupContext _groupContext;

public TestDatabase(string databaseName, IActiveGroupContext? groupContext = null)
{
    _groupContext = groupContext ?? new MutableGroupContext();
    // ...
}

public QuestBoardContext CreateContext()
{
    return new QuestBoardContext(_options, _groupContext);
}
```

Or, change `TestDatabase` to use the same `MutableGroupContext { ActiveGroupId = null }` that means "see all".

**Warning signs:** `CS7036: There is no argument given that corresponds to the required parameter 'activeGroupContext'` compile error in `TestDatabase.cs`.

### Pitfall 2: Filter Lambda Captures Value Instead of Service Reference

**What goes wrong:** If `HasQueryFilter` is written as:

```csharp
var id = activeGroupContext.ActiveGroupId;
modelBuilder.Entity<QuestEntity>().HasQueryFilter(e => e.GroupId == id);
```

The filter is evaluated once at `OnModelCreating` time with `id = null`, then baked in. All queries forever see null (pass-all).

**Why it happens:** Value types and struct-like captures create copies at the point of capture. The lambda must close over the service instance, not the property.

**How to avoid:** Always reference the service instance in the lambda body: `e => activeGroupContext.ActiveGroupId == null || e.GroupId == activeGroupContext.ActiveGroupId`.

**Warning signs:** Query filter appears to do nothing — all records returned regardless of session value.

### Pitfall 3: IHttpContextAccessor Returns Null in Hangfire

**What goes wrong:** `ActiveGroupContextService` reads `_httpContextAccessor.HttpContext?.Session?.GetInt32(...)`. If the `?.` chain is not used and `HttpContext` is null-checked with `!`, this throws `NullReferenceException` in Hangfire background threads.

**Why it happens:** Hangfire executes jobs on background threads with no associated HTTP request; `IHttpContextAccessor.HttpContext` is null.

**How to avoid:** Use `?.` throughout. The null result is intentional (null = see all). Never use `!` or assert non-null on `HttpContext` inside `ActiveGroupContextService`.

**Warning signs:** `NullReferenceException` in Hangfire job logs; jobs fail during email sending.

### Pitfall 4: Singleton ActiveGroupContextService Shares State Across Requests

**What goes wrong:** If `ActiveGroupContextService` (which holds mutable state via `SetGroupId`) is registered as Singleton, all concurrent requests share the same instance. Request A sets `GroupId = 2`, then Request B's context sees Group 2's data.

**Why it happens:** Singleton lifetime means one instance per application. `SetGroupId` mutates it.

**How to avoid:** Register `ActiveGroupContextService` as `Scoped`. The test stub `MutableGroupContext` is Singleton by design because integration tests are single-threaded and reset it explicitly.

**Warning signs:** Cross-group data leakage in load tests; random wrong-group data under concurrent requests.

### Pitfall 5: DailyReminderJob Passes No GroupId to SessionReminderJob

**What goes wrong:** After Phase 28, `DailyReminderJob` enqueues `SessionReminderJob` without a `groupId` parameter. `SessionReminderJob` resolves the context with `ActiveGroupId = null` (Hangfire, no HTTP context). With the null = see all rule, `GetQuestWithDetailsAsync(questId)` succeeds — but only because null bypasses the filter. This is fragile: Phase 29 will tighten null to mean SuperAdmin only, and jobs will start failing.

**Why it happens:** `DailyReminderJob` calls `GetQuestsForTomorrowAllGroupsAsync()` (which now returns quests from all groups). Each returned quest has a `GroupId`. That `GroupId` must be forwarded to the enqueued `SessionReminderJob`.

**How to avoid:** In `DailyReminderJob`, change the enqueue call to pass `quest.GroupId`. The `IQuestModel` must expose `GroupId` (it already does as of Phase 27).

**Warning signs:** Phase 29 regression — `SessionReminderJob` suddenly finds no quest when `IsSuperAdmin = false` and `ActiveGroupId = null` is no longer "see all".

### Pitfall 6: HangfireQuestEmailDispatcher / HangfireReminderJobDispatcher Signature Mismatch

**What goes wrong:** `QuestFinalizedEmailJob.ExecuteAsync` and `SessionReminderJob.ExecuteAsync` gain a new `int groupId` parameter. Their dispatchers (`HangfireQuestEmailDispatcher`, `HangfireReminderJobDispatcher`) must also pass `groupId`. The Domain-layer interfaces `IQuestEmailDispatcher` and `IReminderJobDispatcher` must also expose `groupId`. Missing any one of these four places (two concrete dispatcher classes + two Domain interfaces) causes compile errors or missing-parameter runtime failures.

**How to avoid:** Update the full chain atomically — Domain interface → Service concrete dispatcher → job method signature.

---

## Code Examples

### IActiveGroupContext Interface (TENANT-01)

```csharp
// Source: D-01 — confirmed as standard EF Core multi-tenancy pattern
// QuestBoard.Domain/Interfaces/IActiveGroupContext.cs

namespace QuestBoard.Domain.Interfaces;

/// <summary>
/// Provides the active group ID for the current request or execution context.
/// Null means "see all records" (Phase 28 temporary state; Phase 29 adds IsSuperAdmin).
/// </summary>
public interface IActiveGroupContext
{
    int? ActiveGroupId { get; }
}
```

### ActiveGroupContextService (TENANT-02)

```csharp
// Source: D-02, D-09 — Session read + SetGroupId for job context
// QuestBoard.Service/Services/ActiveGroupContextService.cs

using Microsoft.AspNetCore.Http;
using QuestBoard.Domain.Interfaces;

namespace QuestBoard.Service.Services;

public class ActiveGroupContextService : IActiveGroupContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private int? _overriddenGroupId;
    private bool _groupIdOverridden;

    public ActiveGroupContextService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public int? ActiveGroupId =>
        _groupIdOverridden
            ? _overriddenGroupId
            : _httpContextAccessor.HttpContext?.Session?.GetInt32(SessionKeys.ActiveGroupId);

    /// <summary>
    /// Called by Hangfire jobs to set the group context before any repository call.
    /// The HTTP context is null in background threads; this provides the groupId explicitly.
    /// </summary>
    public void SetGroupId(int? groupId)
    {
        _groupIdOverridden = true;
        _overriddenGroupId = groupId;
    }
}
```

> Note: An alternative simpler design is to have `ActiveGroupId` read from either the override or Session, with SetGroupId simply storing an override. The exact field names are Claude's discretion.

### Session Key Constant (Claude's Discretion)

```csharp
// Source: D-specifics — single definition rule
// QuestBoard.Service/Constants/SessionKeys.cs (or QuestBoard.Domain/Constants/SessionKeys.cs)

namespace QuestBoard.Service.Constants;  // adjust namespace to chosen layer

public static class SessionKeys
{
    public const string ActiveGroupId = "ActiveGroupId";
}
```

### DI Registration in Program.cs

```csharp
// Source: D-04 — Scoped lifetime, Service layer registration
// In QuestBoard.Service/Program.cs, alongside AddRepositoryServices / AddDomainServices:

builder.Services.AddHttpContextAccessor();  // required for IHttpContextAccessor
builder.Services.AddScoped<ActiveGroupContextService>();
builder.Services.AddScoped<IActiveGroupContext>(sp =>
    sp.GetRequiredService<ActiveGroupContextService>());
```

> Alternative: Register `ActiveGroupContextService` as Scoped and use it to satisfy `IActiveGroupContext` by also registering the interface binding. The key is that the SAME instance is resolved for both `IActiveGroupContext` (used by QuestBoardContext) and `ActiveGroupContextService` (used by jobs calling `SetGroupId`).

### TestDatabase Fix

```csharp
// Source: D-12 + Pitfall 1 analysis
// QuestBoard.IntegrationTests/Helpers/TestDatabase.cs — MUST be updated

public class TestDatabase : IDisposable
{
    public string DatabaseName { get; }
    private readonly DbContextOptions<QuestBoardContext> _options;

    public TestDatabase(string databaseName)
    {
        DatabaseName = databaseName;
        _options = new DbContextOptionsBuilder<QuestBoardContext>()
            .UseInMemoryDatabase(DatabaseName)
            .EnableSensitiveDataLogging()
            .Options;

        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    public QuestBoardContext CreateContext()
    {
        // Pass MutableGroupContext with null = see all for raw context creation outside DI
        return new QuestBoardContext(_options, new MutableGroupContext { ActiveGroupId = null });
    }
    // ...
}
```

---

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| EF Core named query filters required workaround for multiple filters | EF Core 10 supports named `HasQueryFilter("key", predicate)` natively, enabling `IgnoreQueryFilters(["key"])` per-filter | EF Core 10 [VERIFIED: MS docs] | Phase 28 uses a single filter per entity — no need for named filters yet; Phase 29 may use named filters to add IsSuperAdmin filter independently |
| Single `HasQueryFilter` call overwrites previous | EF Core 10: multiple named filters compose with AND | EF Core 10 [VERIFIED: MS docs] | Phase 28 only needs one filter per entity — but Phase 29 planner should use named filters |

**EF Core 10 specifics for this phase:** [VERIFIED: MS docs — learn.microsoft.com/en-us/ef/core/querying/filters]

- `HasQueryFilter(predicate)` without a name: Still works in EF Core 10, overwrites any previous unnamed filter. Safe for Phase 28 because we're adding the first (and only) filter per entity.
- `IgnoreQueryFilters()` (no args): Disables ALL filters on the entity. Used in `GetQuestsForTomorrowAllGroupsAsync` — correct for D-08.
- `IgnoreQueryFilters(["FilterName"])` (selective, EF Core 10+): Disables specific named filter. Not needed in Phase 28 but important for Phase 29 planner.
- `HasQueryFilter` lambda captures: EF Core captures the entire lambda expression graph. Closures over service instance references (not value copies) re-evaluate per query — confirmed behavior.
- InMemory provider honors `HasQueryFilter`: [VERIFIED: EF Core source — NavigationExpandingExpressionVisitor applies filters to all entity query root expressions, including InMemory]. The test stub will correctly filter quests with GroupId != 1.

---

## Runtime State Inventory

Step 2.5 SKIPPED — this is a greenfield feature addition (new interfaces, new service, new filter wiring). No rename, refactor, or migration of existing runtime state is involved in this phase. Phase 27 already completed the data migration seeding `GroupId = 1` on all existing records.

---

## Environment Availability

Step 2.6: SKIPPED — this phase is purely code changes. No new external tools, services, CLIs, or runtimes are required beyond what is already in use.

---

## Validation Architecture

### Test Framework

| Property | Value |
|----------|-------|
| Framework | xunit.v3 3.2.2 + Microsoft.AspNetCore.Mvc.Testing 10.0.9 [VERIFIED: IntegrationTests.csproj] |
| Config file | `QuestBoard.IntegrationTests/xunit.runner.json` |
| Quick run command | `dotnet test QuestBoard.IntegrationTests/ --no-build` |
| Full suite command | `dotnet test --no-build` (all projects) |

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| TENANT-01 | `IActiveGroupContext` defined in Domain, accessible from Repository layer | unit (compile gate) | `dotnet build` | Wave 0: create interface |
| TENANT-02 | `ActiveGroupContextService` reads session; null when no HttpContext | unit | `dotnet build` + existing integration tests exercise session indirectly | Wave 0: create service |
| TENANT-03 | Quest/ShopItem queries are group-filtered; UserEntity queries not filtered | integration | Existing quest/shop tests pass with GroupId=1 stub (they seed GroupId=1 data) | Relies on D-10/D-11 stub being in place |
| TENANT-03 | Cross-group isolation: quest in Group 2 not visible to Group 1 session | integration | New test: `QuestRepository_GroupFilter_ExcludesOtherGroup` | Wave 0: create test file |
| TENANT-04 | Hangfire jobs execute without NullReferenceException from missing HttpContext | integration | Existing `QuestReminderTests.cs` and `QuestFinalizeTests.cs` pass | Existing — must still pass |
| TENANT-05 | All 201 tests pass (current count is 201 [VERIFIED: `dotnet test --list-tests`]) | full suite | `dotnet test --no-build` | All existing |

> Note: `dotnet test --list-tests` shows 201 test method names — the CONTEXT.md says "191" which may be an older count or a different counting method. The planner should use the current count of 201 as the gate.

### Sampling Rate

- **Per task commit:** `dotnet build` (compile gate, ~10 seconds)
- **Per wave merge:** `dotnet test --no-build` (full suite, ~60 seconds)
- **Phase gate:** Full suite green before `/gsd-verify-work`

### Wave 0 Gaps

The following items must exist before implementation tasks run:

- [ ] `QuestBoard.Domain/Interfaces/IActiveGroupContext.cs` — TENANT-01 (create in first task)
- [ ] `QuestBoard.IntegrationTests/Helpers/MutableGroupContext.cs` — TENANT-05 (or inner class)
- [ ] New integration test for cross-group filter isolation (TENANT-03 cross-group scenario)
- [ ] Fix `TestDatabase.CreateContext()` — compiles with new `QuestBoardContext` constructor

---

## Security Domain

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | no | Phase 29 covers auth handler changes |
| V3 Session Management | yes | ASP.NET Core built-in session with `HttpOnly`, `IsEssential` (already configured in Program.cs) |
| V4 Access Control | yes (data isolation) | `HasQueryFilter` provides automatic per-request group scoping |
| V5 Input Validation | no | No user-supplied input in this phase |
| V6 Cryptography | no | No crypto changes |

### Known Threat Patterns for EF Core Query Filters

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Session fixation/forgery → wrong GroupId in session | Elevation of Privilege | Session cookies are HttpOnly + IsEssential (already set); no change needed in Phase 28 |
| Direct object reference bypass — request for Quest by ID from another group | Information Disclosure | `HasQueryFilter` returns null for `GetQuestWithDetailsAsync(id)` when GroupId doesn't match — controller returns 404 naturally |
| Hangfire job reads wrong group's quests | Information Disclosure | D-09: `SetGroupId` before any repo call; D-08: `IgnoreQueryFilters` only on the cross-group method |
| Test stub registered as Singleton leaks between parallel test runs | Test isolation concern | xunit.v3 uses `IClassFixture<WebApplicationFactoryBase>` — each test class gets its own factory instance; Singleton is safe within a class |

---

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | `QuestBoard.Service/Program.cs` does not already register `IHttpContextAccessor` — `AddHttpContextAccessor()` is needed | DI Registration example | Low: if already registered, calling it twice is a no-op |
| A2 | `Quest` domain model (as returned by `IQuestRepository`) exposes `GroupId` property after Phase 27 mapping | Pitfall 5 | Medium: if `GroupId` is not mapped to the domain model, `DailyReminderJob` can't forward it; check EntityProfile |
| A3 | Current test count is 201 (from `dotnet test --list-tests`), not 191 as stated in CONTEXT.md | Validation Architecture | Low: the gate is "all tests pass," not the exact number |

---

## Open Questions

1. **Does the `Quest` domain model expose `GroupId`?**
   - What we know: `QuestEntity.GroupId` was added in Phase 27. `EntityProfile.cs` maps Entity ↔ DomainModel.
   - What's unclear: Whether `EntityProfile` was updated in Phase 27 to include the `GroupId` → `Quest.GroupId` mapping.
   - Recommendation: Planner should verify `Quest.cs` domain model has `int GroupId` property and `EntityProfile` maps it. If missing, add as part of Wave 1 task 1.

2. **Where does `ActiveGroupContextService` registration live?**
   - What we know: `AddDomainServices()` (Domain extension) and `AddRepositoryServices()` (Repository extension) are the existing patterns. Service-layer registrations like Hangfire dispatchers are inline in `Program.cs`.
   - What's unclear: Should `ActiveGroupContextService` get its own Service-layer extension method, or register inline in `Program.cs`?
   - Recommendation: Register inline in `Program.cs` alongside the existing dispatcher registrations (they share the same "Service-layer, not Domain or Repository" category).

3. **Session must be configured before `UseAuthentication` — is `UseSession()` already before `app.UseAuthentication()`?**
   - What we know: `Program.cs` shows `app.UseSession()` before `app.UseAuthentication()` — this is correct.
   - What's unclear: Nothing — this is verified. No middleware order change needed.

---

## Sources

### Primary (HIGH confidence)

- [VERIFIED: MS docs — learn.microsoft.com/en-us/ef/core/querying/filters] — `HasQueryFilter`, `IgnoreQueryFilters`, multi-tenancy constructor injection pattern, EF Core 10 named filters
- [VERIFIED: MS docs — learn.microsoft.com/en-us/ef/core/dbcontext-configuration/] — `AddDbContext` Scoped lifetime, constructor injection via DI, scoped service sharing within request scope
- [VERIFIED: project csproj] — EF Core 10.0.9, InMemory 10.0.9, .NET 10
- [VERIFIED: QuestBoardContext.cs] — Existing primary constructor pattern, OnModelCreating structure, existing FK config
- [VERIFIED: WebApplicationFactoryBase.cs] — Existing `ConfigureTestServices` pattern (NoOpBackgroundJobClient singleton), InMemory DB wiring
- [VERIFIED: TestDatabase.cs] — Direct `new QuestBoardContext(_options)` call confirmed; will break with two-parameter constructor
- [VERIFIED: DailyReminderJob.cs, SessionReminderJob.cs, QuestFinalizedEmailJob.cs] — Existing `IServiceScopeFactory.CreateAsyncScope()` pattern in all jobs
- [VERIFIED: Context7 /dotnet/efcore] — `GetApplicableQueryFilters` confirms `IgnoreQueryFilters` affects ALL entity query roots including InMemory

### Secondary (MEDIUM confidence)

- [CITED: 28-CONTEXT.md] — All D-0x decisions, confirmed locked

---

## Metadata

**Confidence breakdown:**

- Standard stack: HIGH — EF Core 10.0.9 verified from csproj; no new packages needed
- Architecture: HIGH — all key files read; injection pattern confirmed from official docs
- Pitfalls: HIGH — `TestDatabase` constructor break is verified from source; filter lambda capture verified from EF Core docs
- Hangfire pattern: HIGH — all 4 existing jobs read; `IServiceScopeFactory` + `CreateAsyncScope` confirmed

**Research date:** 2026-06-30
**Valid until:** 2026-07-30 (stable EF Core APIs; filter behavior unlikely to change in point releases)
