---
phase: 28-tenant-isolation
reviewed: 2026-06-30T12:00:00Z
depth: standard
files_reviewed: 22
files_reviewed_list:
  - QuestBoard.Domain/Interfaces/IActiveGroupContext.cs
  - QuestBoard.Service/Constants/SessionKeys.cs
  - QuestBoard.Service/Services/ActiveGroupContextService.cs
  - QuestBoard.IntegrationTests/Helpers/MutableGroupContext.cs
  - QuestBoard.Repository/Entities/QuestBoardContext.cs
  - QuestBoard.IntegrationTests/Helpers/TestDatabase.cs
  - QuestBoard.IntegrationTests/WebApplicationFactoryBase.cs
  - QuestBoard.Service/Program.cs
  - QuestBoard.Domain/Interfaces/IQuestRepository.cs
  - QuestBoard.Domain/Interfaces/IQuestEmailDispatcher.cs
  - QuestBoard.Domain/Interfaces/IReminderJobDispatcher.cs
  - QuestBoard.Repository/QuestRepository.cs
  - QuestBoard.Domain/Services/QuestService.cs
  - QuestBoard.Service/Services/HangfireQuestEmailDispatcher.cs
  - QuestBoard.Service/Services/HangfireReminderJobDispatcher.cs
  - QuestBoard.Service/Services/NullQuestEmailDispatcher.cs
  - QuestBoard.Service/Services/NullReminderJobDispatcher.cs
  - QuestBoard.Service/Jobs/QuestFinalizedEmailJob.cs
  - QuestBoard.Service/Jobs/SessionReminderJob.cs
  - QuestBoard.Service/Jobs/DailyReminderJob.cs
  - QuestBoard.Service/Controllers/QuestBoard/QuestController.cs
  - QuestBoard.IntegrationTests/Tests/TenantIsolationTests.cs
findings:
  critical: 0
  warning: 3
  info: 3
  total: 6
status: issues_found
---

# Phase 28: Code Review Report

**Reviewed:** 2026-06-30T12:00:00Z
**Depth:** standard
**Files Reviewed:** 22
**Status:** issues_found

## Summary

Phase 28 introduces EF Core `HasQueryFilter`-based group isolation across `QuestEntity` and `ShopItemEntity`. The overall architecture is sound: the lambda closure pattern is correct (it closes over the `activeGroupContext` service instance, not a snapshot value), `UserEntity` is correctly excluded, and the `IActiveGroupContext` registration is scoped to match `QuestBoardContext`'s lifetime. The Hangfire job wiring is thorough — `SetGroupId` is called before repository resolution in both `QuestFinalizedEmailJob` and `SessionReminderJob`, and `DailyReminderJob` correctly uses `GetQuestsForTomorrowAllGroupsAsync` with `IgnoreQueryFilters()` for the cross-group sweep.

Three warning-level issues and three info-level issues are identified below. No critical security vulnerabilities were found. The most important finding is a **singleton-captures-scoped-service** problem in the test factory (WR-01) that can cause non-deterministic test failures when the xUnit test runner reuses the factory across test classes.

---

## Warnings

### WR-01: Singleton `MutableGroupContext` captures a `QuestBoardContext` that is registered as Scoped — test state bleed risk

**File:** `QuestBoard.IntegrationTests/WebApplicationFactoryBase.cs:69`

**Issue:** `IActiveGroupContext` is registered as `Singleton` (the `TestGroupContext` instance) in the test factory, while `QuestBoardContext` is registered as `Scoped`. This is correct from a DI lifetime standpoint — the scoped `QuestBoardContext` resolves a fresh `IActiveGroupContext` from the scope on each request, so there is no captured-scoped-in-singleton problem in production paths.

However, `TestGroupContext.ActiveGroupId` is a plain `{ get; set; }` property mutated directly by test code (e.g., `factory.TestGroupContext.ActiveGroupId = 1` on lines 40 and 76 of `TenantIsolationTests.cs`). Because `WebApplicationFactoryBase` is shared across **all three** tests in `TenantIsolationTests` via `IClassFixture<WebApplicationFactoryBase>`, any test that forgets to reset `ActiveGroupId` leaves dirty state for subsequent tests. The third test (`GroupFilter_NullGroupIdShowsAllGroups`) relies on the HTTP stack not being called but does mutate the singleton via the seeding path — yet the tests that do call `CreateClient()` both assign `ActiveGroupId = 1` without ever resetting to the original default (also 1). In the current test set this is harmless, but there is no teardown guard: if a future test sets `ActiveGroupId = 2` and throws before the reset, the next test in the fixture sees stale state.

**Fix:** Add a `ResetGroupContext()` helper method to `WebApplicationFactoryBase` and call it in an `IAsyncLifetime.DisposeAsync` or via a per-test `finally` block, or — safer — drive the active group entirely through the HTTP session rather than mutating the singleton:

```csharp
// Option A: reset in each test's finally (simple)
public async Task GroupFilter_HidesQuestFromOtherGroup()
{
    try
    {
        factory.TestGroupContext.ActiveGroupId = 1;
        // ... test body ...
    }
    finally
    {
        factory.TestGroupContext.ActiveGroupId = 1; // restore default
    }
}

// Option B: add a thread-safe reset method to WebApplicationFactoryBase
public void ResetGroupContext() => TestGroupContext.ActiveGroupId = 1;
```

---

### WR-02: `QuestDateChangedEmailJob` does not call `SetGroupId` — query filter uses the default (null = see all) for its `IQuestRepository` resolution

**File:** `QuestBoard.Service/Jobs/QuestDateChangedEmailJob.cs:24`

**Issue:** `QuestDateChangedEmailJob.ExecuteAsync` creates a DI scope and resolves `IEmailRenderService`, `IEmailService`, and `IOptions<EmailSettings>` — but it does **not** resolve `IQuestRepository` or any other repository that touches filtered data. At present the job only sends pre-rendered email content (all data passed in via parameters), so there is no immediate security impact: no repository query is made.

The risk is latent: if a future developer adds a repository call to this job (e.g., to re-fetch the quest for a dedup guard, mirroring the pattern in `QuestFinalizedEmailJob`) without noticing the missing `SetGroupId` call, the group filter will be absent and the job may either see all groups' data or see no data at all (if `ActiveGroupId` defaults to null = see all). The inconsistency with the sibling jobs (`QuestFinalizedEmailJob` and `SessionReminderJob`) is a code smell that increases this risk.

**Fix:** Add a defensive `SetGroupId` call even though the current job body doesn't use a repository, and add a `// NOTE: no repository calls in this job — SetGroupId retained for consistency and future-safety` comment. Alternatively, pass `groupId` into the interface/dispatcher signature to enforce the pattern at the call site:

```csharp
// In QuestDateChangedEmailJob.ExecuteAsync, add groupId parameter and wire it through:
public async Task ExecuteAsync(
    int questId,
    int groupId,              // add this
    string[] recipientEmails,
    ...
)
{
    await using var scope = scopeFactory.CreateAsyncScope();
    // Defensive: ensures filter is active if a repo call is ever added
    var groupContext = scope.ServiceProvider.GetRequiredService<ActiveGroupContextService>();
    groupContext.SetGroupId(groupId);
    // ... rest of job unchanged
}
```

Also thread `groupId` through `IQuestEmailDispatcher.EnqueueDateChangedEmail` and `HangfireQuestEmailDispatcher`.

---

### WR-03: `ActiveGroupContextService` is registered twice in the same DI scope — `IActiveGroupContext` may resolve a different instance than `ActiveGroupContextService`

**File:** `QuestBoard.Service/Program.cs:98-100`

**Issue:** The registration pattern is:

```csharp
builder.Services.AddScoped<ActiveGroupContextService>();
builder.Services.AddScoped<IActiveGroupContext>(sp =>
    sp.GetRequiredService<ActiveGroupContextService>());
```

Resolving `IActiveGroupContext` from the DI container calls the factory which calls `GetRequiredService<ActiveGroupContextService>()`. Because `ActiveGroupContextService` is registered as a named scoped service, `GetRequiredService` will return the **same** instance within the scope — this is correct and the pattern is safe.

The concern is that `QuestFinalizedEmailJob` and `SessionReminderJob` resolve `ActiveGroupContextService` by concrete type (not interface) to call `SetGroupId`, and then `IQuestRepository` is resolved subsequently (which eventually reaches `QuestBoardContext`, which was given `IActiveGroupContext` via constructor injection at scope creation time). Because `QuestBoardContext` receives `IActiveGroupContext` — resolved via the factory above — it gets the **same** `ActiveGroupContextService` instance. So `SetGroupId` correctly mutates the instance that the `DbContext` holds a reference to.

However, this is only correct because the DI container guarantees scoped-lifetime singleton-within-scope semantics. The code has no comment explaining why the concrete-type resolution is safe here and why it does not create a second independent instance. This is a future maintenance trap: if someone changes the registration (e.g., uses `AddScoped<IActiveGroupContext, ActiveGroupContextService>()` without the factory, or removes the concrete-type registration), the `GetRequiredService<ActiveGroupContextService>()` in the jobs will throw `InvalidOperationException` at runtime, not at startup.

**Fix:** Add an explanatory comment and consider adding a guard assertion in development:

```csharp
// Register the concrete type so Hangfire jobs can call SetGroupId (interface has no SetGroupId).
// IActiveGroupContext delegates to the same scoped instance via the factory below.
// IMPORTANT: both registrations MUST resolve the same instance within a scope.
builder.Services.AddScoped<ActiveGroupContextService>();
builder.Services.AddScoped<IActiveGroupContext>(sp =>
    sp.GetRequiredService<ActiveGroupContextService>());
```

---

## Info

### IN-01: `TestDatabase.Reset()` swallows all exceptions silently — a broken reset is invisible

**File:** `QuestBoard.IntegrationTests/Helpers/TestDatabase.cs:32-38`

**Issue:** The `Reset()` method wraps `EnsureDeleted` + `EnsureCreated` in a bare `catch { // Ignore errors }` block. If the in-memory database fails to reset (e.g., due to a concurrent disposal or a schema mismatch that a future migration introduces), tests will silently continue against a partially-reset or stale database state. Test failures will be non-deterministic and hard to diagnose.

**Fix:** Log the error at minimum, or rethrow after logging:

```csharp
public void Reset()
{
    using var context = CreateContext();
    context.Database.EnsureDeleted();
    context.Database.EnsureCreated();
}
```

The `try/catch` was presumably added to handle a disposal race. If that race is real, narrow the catch to the specific exception type.

---

### IN-02: `activeGroupContext.ActiveGroupId ?? 1` in `QuestController.SendReminder` is a documented-but-undiscoverable fallback

**File:** `QuestBoard.Service/Controllers/QuestBoard/QuestController.cs:677`

**Issue:** The fallback `?? 1` (GroupId 1 = EuphoriaInn, the only production group in Phase 28) is documented in the phase context and is intentional. However, `ActiveGroupId` can only be null in two real scenarios: (1) no session cookie is set for the authenticated user, or (2) the Hangfire execution path (which has no `HttpContext`). Neither scenario should apply to an authenticated DM making an HTTP POST request — the session should always contain `ActiveGroupId` for authenticated users.

If the session key is absent for a legitimate authenticated request (e.g., a user who authenticated before the session key was introduced), the fallback silently assigns Group 1, which may be wrong for a future second group. This should either assert non-null in production or redirect the user to re-set their group context.

**Fix:** Add a defensive check or log a warning when the fallback fires:

```csharp
var groupId = activeGroupContext.ActiveGroupId;
if (groupId == null)
{
    // Phase 28 safe fallback — Phase 29 should enforce group selection before this point
    logger.LogWarning("SendReminder: ActiveGroupId is null for authenticated DM {UserId}; defaulting to group 1.", currentUser.Id);
    groupId = 1;
}
reminderJobDispatcher.EnqueueSessionReminder(id, groupId.Value, forceResend, useYesMaybeVoters: true);
```

---

### IN-03: `TenantIsolationTests` has no test for `ActiveGroupId = null` path through the HTTP stack — only through the direct `DbContext` path

**File:** `QuestBoard.IntegrationTests/Tests/TenantIsolationTests.cs:90-135`

**Issue:** `GroupFilter_NullGroupIdShowsAllGroups` verifies the `null = see all` predicate by querying `DbContext.Quests.ToList()` directly via `TestDatabase.CreateContext()` — bypassing the HTTP stack and DI pipeline entirely. There is no integration test that sets `factory.TestGroupContext.ActiveGroupId = null` and then makes an HTTP request to verify the "see all" behaviour flows correctly through the full web application stack (middleware, controller, repository). In Phase 29 when `null` semantics change (IsSuperAdmin check), this gap means there is no HTTP-level regression baseline.

**Fix:** Add a fourth test that sets `ActiveGroupId = null`, calls a controller endpoint (e.g., `GET /`), and asserts that quests from both groups appear in the response body.

---

_Reviewed: 2026-06-30T12:00:00Z_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
