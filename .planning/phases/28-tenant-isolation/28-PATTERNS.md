# Phase 28: Tenant Isolation - Pattern Map

**Mapped:** 2026-06-30
**Files analyzed:** 12 (new/modified)
**Analogs found:** 12 / 12

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|---|---|---|---|---|
| `QuestBoard.Domain/Interfaces/IActiveGroupContext.cs` | interface | request-response | `QuestBoard.Domain/Interfaces/IEmailRenderService.cs` | role-match (single-method read interface) |
| `QuestBoard.Service/Services/ActiveGroupContextService.cs` | service | request-response | `QuestBoard.Service/Services/HangfireReminderJobDispatcher.cs` | role-match (implements Domain interface, injected dep) |
| `QuestBoard.Service/Constants/SessionKeys.cs` | config/constant | — | inline `const string` pattern in existing jobs | structural |
| `QuestBoard.Repository/Entities/QuestBoardContext.cs` | config/model | CRUD | self (modify in place) | exact |
| `QuestBoard.Service/Jobs/QuestFinalizedEmailJob.cs` | job | event-driven | self (modify in place) | exact |
| `QuestBoard.Service/Jobs/SessionReminderJob.cs` | job | event-driven | self (modify in place) | exact |
| `QuestBoard.Service/Jobs/DailyReminderJob.cs` | job | event-driven | self (modify in place) | exact |
| `QuestBoard.Domain/Interfaces/IQuestRepository.cs` | interface | CRUD | self (add method to existing interface) | exact |
| `QuestBoard.Repository/QuestRepository.cs` | repository | CRUD | `GetFinalizedQuestsForDateAsync` in same file (lines 209-215) | exact |
| `QuestBoard.Domain/Interfaces/IQuestEmailDispatcher.cs` | interface | event-driven | self (add groupId param) | exact |
| `QuestBoard.Domain/Interfaces/IReminderJobDispatcher.cs` | interface | event-driven | self (add groupId param) | exact |
| `QuestBoard.Service/Services/HangfireQuestEmailDispatcher.cs` | service | event-driven | self (add groupId pass-through) | exact |
| `QuestBoard.Service/Services/HangfireReminderJobDispatcher.cs` | service | event-driven | self (add groupId pass-through) | exact |
| `QuestBoard.IntegrationTests/Helpers/MutableGroupContext.cs` | test helper | — | `NoOpBackgroundJobClient` in `WebApplicationFactoryBase.cs` (lines 114-118) | role-match (test stub implementing Domain interface) |
| `QuestBoard.IntegrationTests/WebApplicationFactoryBase.cs` | test config | — | self (lines 63-66 show `AddSingleton<IBackgroundJobClient>` pattern) | exact |
| `QuestBoard.IntegrationTests/Helpers/TestDatabase.cs` | test helper | CRUD | self (modify `CreateContext()` line 22) | exact |

---

## Pattern Assignments

### NEW: `QuestBoard.Domain/Interfaces/IActiveGroupContext.cs` (interface, request-response)

**Analog:** `QuestBoard.Domain/Interfaces/IEmailRenderService.cs`

**Imports pattern** (lines 1-7, IEmailRenderService.cs):
```csharp
namespace QuestBoard.Domain.Interfaces;

public interface IEmailRenderService
{
    Task<string> RenderAsync<TComponent>(Dictionary<string, object?> parameters)
        where TComponent : Microsoft.AspNetCore.Components.IComponent;
}
```

**Core pattern — single-purpose read-only interface:**
```csharp
// Copy structure: no using directives needed, file-scoped namespace, XML doc, one member
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

---

### NEW: `QuestBoard.Service/Services/ActiveGroupContextService.cs` (service, request-response)

**Analog:** `QuestBoard.Service/Services/HangfireReminderJobDispatcher.cs`

**Imports pattern** (lines 1-5, HangfireReminderJobDispatcher.cs):
```csharp
using QuestBoard.Domain.Interfaces;
using QuestBoard.Service.Jobs;
using Hangfire;

namespace QuestBoard.Service.Services;
```

**Core pattern** (full file, HangfireReminderJobDispatcher.cs):
```csharp
// Primary constructor with injected dependency, implements Domain interface
public class HangfireReminderJobDispatcher(IBackgroundJobClient jobClient) : IReminderJobDispatcher
{
    public void EnqueueSessionReminder(int questId, bool forceResend = false, bool useYesMaybeVoters = false)
    {
        jobClient.Enqueue<SessionReminderJob>(j => j.ExecuteAsync(questId, forceResend, useYesMaybeVoters, CancellationToken.None));
    }
}
```

**Applied to ActiveGroupContextService — differences from analog:**
- Adds `private int? _overriddenGroupId; private bool _groupIdOverridden;` fields for the SetGroupId override
- `IHttpContextAccessor` injected instead of `IBackgroundJobClient`
- `ActiveGroupId` property returns override-first, falls back to `_httpContextAccessor.HttpContext?.Session?.GetInt32(SessionKeys.ActiveGroupId)`
- Adds `public void SetGroupId(int? groupId)` mutation method (concrete class only — not on the interface)
- Import: `using Microsoft.AspNetCore.Http;` (for `IHttpContextAccessor`)

```csharp
using Microsoft.AspNetCore.Http;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Service.Constants;

namespace QuestBoard.Service.Services;

public class ActiveGroupContextService(IHttpContextAccessor httpContextAccessor) : IActiveGroupContext
{
    private int? _overriddenGroupId;
    private bool _groupIdOverridden;

    public int? ActiveGroupId =>
        _groupIdOverridden
            ? _overriddenGroupId
            : httpContextAccessor.HttpContext?.Session?.GetInt32(SessionKeys.ActiveGroupId);

    /// <summary>
    /// Called by Hangfire jobs to set the group context before any repository call.
    /// HTTP context is null in background threads; this provides groupId explicitly.
    /// </summary>
    public void SetGroupId(int? groupId)
    {
        _groupIdOverridden = true;
        _overriddenGroupId = groupId;
    }
}
```

---

### NEW: `QuestBoard.Service/Constants/SessionKeys.cs` (config)

**Analog:** No existing constants file — model on `namespace QuestBoard.Service.Constants` pattern used in rest of Service layer.

```csharp
namespace QuestBoard.Service.Constants;

public static class SessionKeys
{
    public const string ActiveGroupId = "ActiveGroupId";
}
```

---

### MODIFY: `QuestBoard.Repository/Entities/QuestBoardContext.cs` (repository context, CRUD)

**Analog:** Self — existing file at `QuestBoard.Repository/Entities/QuestBoardContext.cs`.

**Current primary constructor** (line 7):
```csharp
public class QuestBoardContext(DbContextOptions<QuestBoardContext> options) : IdentityDbContext<UserEntity, IdentityRole<int>, int>(options)
```

**New primary constructor — add IActiveGroupContext parameter:**
```csharp
// Add second constructor parameter; inject via DI alongside options
public class QuestBoardContext(
    DbContextOptions<QuestBoardContext> options,
    IActiveGroupContext activeGroupContext)
    : IdentityDbContext<UserEntity, IdentityRole<int>, int>(options)
```

**Add import:**
```csharp
using QuestBoard.Domain.Interfaces;
```

**HasQueryFilter addition in OnModelCreating — insert after existing Group FK config (after line 228, before closing brace):**

The existing Group FK section ends with `UserGroup → Group: Cascade` (lines 224-227). Append after that block:

```csharp
        // TENANT-03: Global query filters for group isolation
        // Null = see all (Phase 28 intentional; Phase 29 tightens with IsSuperAdmin check)
        // Lambda closes over activeGroupContext instance — re-evaluated per query, not at startup
        modelBuilder.Entity<QuestEntity>()
            .HasQueryFilter(e =>
                activeGroupContext.ActiveGroupId == null ||
                e.GroupId == activeGroupContext.ActiveGroupId);

        modelBuilder.Entity<ShopItemEntity>()
            .HasQueryFilter(e =>
                activeGroupContext.ActiveGroupId == null ||
                e.GroupId == activeGroupContext.ActiveGroupId);

        // UserEntity intentionally excluded — HasQueryFilter on UserEntity breaks ASP.NET Core Identity
```

**Critical: Do NOT capture value — always reference the service instance:**
```csharp
// WRONG — captures null at model-build time, filter never activates:
var id = activeGroupContext.ActiveGroupId;
modelBuilder.Entity<QuestEntity>().HasQueryFilter(e => e.GroupId == id);

// CORRECT — re-evaluates per query from live service instance:
modelBuilder.Entity<QuestEntity>().HasQueryFilter(e =>
    activeGroupContext.ActiveGroupId == null || e.GroupId == activeGroupContext.ActiveGroupId);
```

---

### MODIFY: `QuestBoard.Service/Jobs/QuestFinalizedEmailJob.cs` (job, event-driven)

**Analog:** Self — existing file `QuestBoard.Service/Jobs/QuestFinalizedEmailJob.cs`.

**Current ExecuteAsync signature** (lines 14-25):
```csharp
public async Task ExecuteAsync(
    int questId,
    DateTime finalizedDate,
    string[] recipientEmails,
    string[] playerNames,
    string questTitle,
    string dmName,
    string questDescription,
    int challengeRating,
    CancellationToken cancellationToken = default)
```

**New signature — add groupId as second parameter (D-06):**
```csharp
public async Task ExecuteAsync(
    int questId,
    int groupId,                  // NEW — D-06, D-09
    DateTime finalizedDate,
    string[] recipientEmails,
    // ... rest unchanged
    CancellationToken cancellationToken = default)
```

**Existing scope creation pattern** (lines 25-30) — D-09 SetGroupId added immediately after scope:
```csharp
await using var scope = scopeFactory.CreateAsyncScope();
// D-09: inject concrete type (not interface) to call SetGroupId
var groupContext = scope.ServiceProvider.GetRequiredService<ActiveGroupContextService>();
groupContext.SetGroupId(groupId);    // must be before any repository call

var questRepository = scope.ServiceProvider.GetRequiredService<IQuestRepository>();
// ... rest unchanged
```

**Import to add:**
```csharp
using QuestBoard.Service.Services;   // for ActiveGroupContextService
```

---

### MODIFY: `QuestBoard.Service/Jobs/SessionReminderJob.cs` (job, event-driven)

**Analog:** Self — existing file `QuestBoard.Service/Jobs/SessionReminderJob.cs`.

**Current ExecuteAsync signature** (line 15):
```csharp
public async Task ExecuteAsync(int questId, bool forceResend = false, bool useYesMaybeVoters = false, CancellationToken cancellationToken = default)
```

**New signature — add groupId before optional parameters (D-06):**
```csharp
public async Task ExecuteAsync(
    int questId,
    int groupId,                  // NEW — D-06, D-09
    bool forceResend = false,
    bool useYesMaybeVoters = false,
    CancellationToken cancellationToken = default)
```

**Existing scope creation** (lines 17-22) — same SetGroupId insertion pattern as QuestFinalizedEmailJob:
```csharp
await using var scope = scopeFactory.CreateAsyncScope();
// D-09: inject concrete type to call SetGroupId
var groupContext = scope.ServiceProvider.GetRequiredService<ActiveGroupContextService>();
groupContext.SetGroupId(groupId);    // must be before any repository call

var questRepository = scope.ServiceProvider.GetRequiredService<IQuestRepository>();
// ... rest unchanged from line 18 onward
```

**Import to add:**
```csharp
using QuestBoard.Service.Services;   // for ActiveGroupContextService
```

---

### MODIFY: `QuestBoard.Service/Jobs/DailyReminderJob.cs` (job, event-driven)

**Analog:** Self — existing file `QuestBoard.Service/Jobs/DailyReminderJob.cs`.

**Current repository call** (line 23):
```csharp
var quests = await questRepository.GetFinalizedQuestsForDateAsync(tomorrow, cancellationToken);
```

**Change to new cross-group method (D-08):**
```csharp
var quests = await questRepository.GetQuestsForTomorrowAllGroupsAsync(tomorrow, cancellationToken);
```

**Current enqueue call** (lines 35-37):
```csharp
backgroundJobClient.Enqueue<SessionReminderJob>(
    job => job.ExecuteAsync(quest.Id, false, false, CancellationToken.None));
```

**Updated enqueue call — add quest.GroupId (D-06, Pitfall 5):**
```csharp
backgroundJobClient.Enqueue<SessionReminderJob>(
    job => job.ExecuteAsync(quest.Id, quest.GroupId, false, false, CancellationToken.None));
```

Note: `quest.GroupId` must be accessible on the `Quest` domain model. Verify `Quest.cs` has `int GroupId { get; set; }` and `EntityProfile` maps `QuestEntity.GroupId` → `Quest.GroupId`. If missing, add the property and mapping as part of this wave.

---

### MODIFY: `QuestBoard.Domain/Interfaces/IQuestRepository.cs` (interface, CRUD)

**Analog:** Self — existing file. Closest method: `GetFinalizedQuestsForDateAsync` (line 36).

**Current method** (line 36):
```csharp
Task<IList<Quest>> GetFinalizedQuestsForDateAsync(DateTime date, CancellationToken token = default);
```

**Add new cross-group method after it (D-08):**
```csharp
    Task<IList<Quest>> GetFinalizedQuestsForDateAsync(DateTime date, CancellationToken token = default);

    /// <summary>
    /// Returns all finalized quests for tomorrow across ALL groups.
    /// Bypasses the group query filter — use only for system-wide sweep operations.
    /// </summary>
    Task<IList<Quest>> GetQuestsForTomorrowAllGroupsAsync(DateTime date, CancellationToken token = default);
```

---

### MODIFY: `QuestBoard.Repository/QuestRepository.cs` (repository, CRUD)

**Analog:** Self — existing `GetFinalizedQuestsForDateAsync` method (lines 209-215).

**Current method to model from** (lines 209-215):
```csharp
public async Task<IList<Quest>> GetFinalizedQuestsForDateAsync(DateTime date, CancellationToken token = default)
{
    var entities = await ProjectWithoutCharacterImages(DbContext.Quests)
        .Where(q => q.FinalizedDate.HasValue && q.FinalizedDate.Value.Date == date.Date)
        .ToListAsync(token);
    return Mapper.Map<IList<Quest>>(entities);
}
```

**New method — identical structure except `IgnoreQueryFilters()` added (D-08):**
```csharp
public async Task<IList<Quest>> GetQuestsForTomorrowAllGroupsAsync(DateTime date, CancellationToken token = default)
{
    // D-08: explicit cross-group intent — IgnoreQueryFilters bypasses HasQueryFilter on QuestEntity
    var entities = await ProjectWithoutCharacterImages(DbContext.Quests.IgnoreQueryFilters())
        .Where(q => q.FinalizedDate.HasValue && q.FinalizedDate.Value.Date == date.Date)
        .ToListAsync(token);
    return Mapper.Map<IList<Quest>>(entities);
}
```

Place this method directly after `GetFinalizedQuestsForDateAsync` (after line 215).

---

### MODIFY: `QuestBoard.Domain/Interfaces/IQuestEmailDispatcher.cs` (interface, event-driven)

**Analog:** Self — existing file, `EnqueueFinalizedEmail` method (lines 9-18).

**Add `int groupId` as second parameter to `EnqueueFinalizedEmail`:**
```csharp
void EnqueueFinalizedEmail(
    int questId,
    int groupId,                  // NEW — D-06
    DateTime finalizedDate,
    string[] recipientEmails,
    string[] playerNames,
    string questTitle,
    string dmName,
    string questDescription,
    int challengeRating);
```

`EnqueueDateChangedEmail` is unchanged (D-07).

---

### MODIFY: `QuestBoard.Domain/Interfaces/IReminderJobDispatcher.cs` (interface, event-driven)

**Analog:** Self — existing file (lines 8-10).

**Current method** (lines 8-10):
```csharp
void EnqueueSessionReminder(int questId, bool forceResend = false, bool useYesMaybeVoters = false);
```

**Add `int groupId` parameter (D-06):**
```csharp
void EnqueueSessionReminder(int questId, int groupId, bool forceResend = false, bool useYesMaybeVoters = false);
```

---

### MODIFY: `QuestBoard.Service/Services/HangfireQuestEmailDispatcher.cs` (service, event-driven)

**Analog:** Self — existing file, `EnqueueFinalizedEmail` (lines 13-26).

**Current Enqueue call** (lines 22-25):
```csharp
jobClient.Enqueue<QuestFinalizedEmailJob>(j => j.ExecuteAsync(
    questId, finalizedDate, recipientEmails, playerNames,
    questTitle, dmName, questDescription, challengeRating,
    CancellationToken.None));
```

**Updated — add groupId as second argument, matching new job signature:**
```csharp
public void EnqueueFinalizedEmail(
    int questId,
    int groupId,                  // NEW — pass through to job
    DateTime finalizedDate,
    // ... rest of params unchanged
    int challengeRating)
{
    jobClient.Enqueue<QuestFinalizedEmailJob>(j => j.ExecuteAsync(
        questId, groupId, finalizedDate, recipientEmails, playerNames,
        questTitle, dmName, questDescription, challengeRating,
        CancellationToken.None));
}
```

---

### MODIFY: `QuestBoard.Service/Services/HangfireReminderJobDispatcher.cs` (service, event-driven)

**Analog:** Self — existing file (lines 12-16).

**Current Enqueue call** (lines 13-16):
```csharp
public void EnqueueSessionReminder(int questId, bool forceResend = false, bool useYesMaybeVoters = false)
{
    jobClient.Enqueue<SessionReminderJob>(j => j.ExecuteAsync(questId, forceResend, useYesMaybeVoters, CancellationToken.None));
}
```

**Updated — add groupId parameter (D-06):**
```csharp
public void EnqueueSessionReminder(int questId, int groupId, bool forceResend = false, bool useYesMaybeVoters = false)
{
    jobClient.Enqueue<SessionReminderJob>(j => j.ExecuteAsync(questId, groupId, forceResend, useYesMaybeVoters, CancellationToken.None));
}
```

---

### NEW: `QuestBoard.IntegrationTests/Helpers/MutableGroupContext.cs` (test helper)

**Analog:** `NoOpBackgroundJobClient` in `QuestBoard.IntegrationTests/WebApplicationFactoryBase.cs` (lines 114-118) — a class defined in the test project that implements a Domain/Hangfire interface, registered as a singleton stub.

**Pattern from analog** (lines 111-118):
```csharp
/// <summary>
/// No-op Hangfire client used in integration tests (Hangfire itself is not configured in Testing env).
/// </summary>
public class NoOpBackgroundJobClient : IBackgroundJobClient
{
    public string Create(Job job, IState state) => "test-job-id";
    public bool ChangeState(string jobId, IState state, string? expectedStateName) => true;
}
```

**Applied to MutableGroupContext:**
```csharp
using QuestBoard.Domain.Interfaces;

namespace QuestBoard.IntegrationTests.Helpers;

/// <summary>
/// Settable implementation of IActiveGroupContext for integration tests.
/// Defaults to GroupId = 1 (EuphoriaInn seed group). Tests override as needed.
/// </summary>
public class MutableGroupContext : IActiveGroupContext
{
    public int? ActiveGroupId { get; set; } = 1;
}
```

---

### MODIFY: `QuestBoard.IntegrationTests/WebApplicationFactoryBase.cs` (test config)

**Analog:** Self — existing file, specifically the `NoOpBackgroundJobClient` singleton registration (line 66) and `Database` property pattern (lines 13-17).

**Existing singleton stub pattern** (lines 63-66):
```csharp
builder.ConfigureTestServices(services =>
{
    services.AddSingleton<IBackgroundJobClient>(new NoOpBackgroundJobClient());
```

**Add TestGroupContext property after Database property (line 13-17 area):**
```csharp
public TestDatabase Database { get; }
public MutableGroupContext TestGroupContext { get; } = new MutableGroupContext();   // NEW — D-10
```

**Add registration in ConfigureTestServices after existing AddSingleton (line 66):**
```csharp
services.AddSingleton<IBackgroundJobClient>(new NoOpBackgroundJobClient());
services.AddSingleton<IActiveGroupContext>(TestGroupContext);    // NEW — D-11
```

**Import to add:**
```csharp
using QuestBoard.Domain.Interfaces;
```

---

### FIX: `QuestBoard.IntegrationTests/Helpers/TestDatabase.cs` (test helper)

**Analog:** Self — existing file. The problem is line 22 `return new QuestBoardContext(_options)` which will fail to compile after the constructor gains a second `IActiveGroupContext` parameter.

**Current CreateContext** (lines 21-24):
```csharp
public QuestBoardContext CreateContext()
{
    return new QuestBoardContext(_options);
}
```

**Fixed CreateContext — pass MutableGroupContext with null = see all (Pitfall 1 from RESEARCH.md):**
```csharp
public QuestBoardContext CreateContext()
{
    // null = see all records (Phase 28 behavior); TestDatabase is used for seeding outside DI
    return new QuestBoardContext(_options, new MutableGroupContext { ActiveGroupId = null });
}
```

No other changes to this file.

---

## Shared Patterns

### Primary Constructor (C# 12 style)

**Source:** `QuestBoard.Repository/Entities/QuestBoardContext.cs` (line 7), `QuestBoard.Service/Jobs/QuestFinalizedEmailJob.cs` (lines 10-12)
**Apply to:** `ActiveGroupContextService`, `QuestBoardContext` (after modification)

```csharp
// All new classes and modified class headers use primary constructor syntax
public class MyClass(IDependency dep) : IInterface
{
    // No constructor body — dep is captured by primary constructor
}
```

### Hangfire IServiceScopeFactory.CreateAsyncScope Pattern

**Source:** `QuestBoard.Service/Jobs/QuestFinalizedEmailJob.cs` (lines 25-30), `QuestBoard.Service/Jobs/SessionReminderJob.cs` (lines 17-22)
**Apply to:** Both job modifications — the `SetGroupId` call slots in immediately after `CreateAsyncScope()` and before any `GetRequiredService<IQuestRepository>()` call.

```csharp
await using var scope = scopeFactory.CreateAsyncScope();
// SetGroupId FIRST — before any repository resolution
var groupContext = scope.ServiceProvider.GetRequiredService<ActiveGroupContextService>();
groupContext.SetGroupId(groupId);
// Then resolve repositories
var questRepository = scope.ServiceProvider.GetRequiredService<IQuestRepository>();
```

### Singleton Stub Registration in ConfigureTestServices

**Source:** `QuestBoard.IntegrationTests/WebApplicationFactoryBase.cs` (line 66)
**Apply to:** `IActiveGroupContext` registration for `MutableGroupContext`

```csharp
// Pattern: AddSingleton<Interface>(instance) where instance is a test-controlled property
services.AddSingleton<IBackgroundJobClient>(new NoOpBackgroundJobClient());   // existing
services.AddSingleton<IActiveGroupContext>(TestGroupContext);                  // new, same pattern
```

### DI Registration in Program.cs

**Source:** `QuestBoard.Service/Program.cs` (lines 99-100) — interface-to-implementation binding, Scoped lifetime
**Apply to:** `ActiveGroupContextService` registration

```csharp
// Existing pattern for Scoped service + interface binding:
builder.Services.AddScoped<IQuestEmailDispatcher, HangfireQuestEmailDispatcher>();
builder.Services.AddScoped<IReminderJobDispatcher, HangfireReminderJobDispatcher>();

// New registration — Scoped lifetime critical (must match QuestBoardContext scope):
builder.Services.AddScoped<ActiveGroupContextService>();
builder.Services.AddScoped<IActiveGroupContext>(sp =>
    sp.GetRequiredService<ActiveGroupContextService>());
```

The dual-registration ensures both `IActiveGroupContext` (used by `QuestBoardContext`) and `ActiveGroupContextService` (used by jobs calling `SetGroupId`) resolve to the same instance within a request scope.

---

## No Analog Found

All files have analogs or are self-modifications. No entries.

---

## Metadata

**Analog search scope:** `QuestBoard.Domain/Interfaces/`, `QuestBoard.Service/Services/`, `QuestBoard.Service/Jobs/`, `QuestBoard.Repository/`, `QuestBoard.IntegrationTests/Helpers/`
**Files scanned:** 17
**Pattern extraction date:** 2026-06-30

### Open Question for Planner (from RESEARCH.md)

Before implementing `DailyReminderJob` changes, verify that the `Quest` domain model (`QuestBoard.Domain/Models/QuestBoard/Quest.cs`) has `int GroupId { get; set; }` and that `EntityProfile.cs` maps `QuestEntity.GroupId` → `Quest.GroupId`. Phase 27 added `GroupId` to `QuestEntity` but the mapping may not have been added. If missing, add both as part of the first implementation wave.
