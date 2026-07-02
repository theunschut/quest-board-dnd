# Phase 28: Tenant Isolation - Context

**Gathered:** 2026-06-30
**Status:** Ready for planning

<domain>
## Phase Boundary

Wire up `IActiveGroupContext` (Domain) + `ActiveGroupContextService` (Service) + EF Core Global Query Filters on `QuestEntity` and `ShopItemEntity` + Hangfire job adaptation + integration test stub. All 191 existing tests must pass with the filter in place.

This phase delivers **runtime isolation infrastructure only** — no UI, no group picker, no authorization handler changes. Those belong to Phases 29–30.

</domain>

<decisions>
## Implementation Decisions

### IActiveGroupContext Interface
- **D-01:** `IActiveGroupContext` defined in `QuestBoard.Domain/` with a single read-only property `int? ActiveGroupId { get; }`. Read-only in Phase 28 — no setter or mutation method. Phase 29 adds `bool IsSuperAdmin { get; }` (see Deferred section).
- **D-02:** `ActiveGroupContextService` in `QuestBoard.Service/` implements `IActiveGroupContext`. Reads `ActiveGroupId` from `IHttpContextAccessor.HttpContext?.Session?.GetInt32("ActiveGroupId")`. When `HttpContext` is null (Hangfire background context), the `?.` chain returns null naturally — graceful, no throw.

### EF Core Global Query Filter
- **D-03:** Filter predicate: `context.ActiveGroupId == null || e.GroupId == context.ActiveGroupId`. Applied in `QuestBoardContext.OnModelCreating` via `HasQueryFilter` on `QuestEntity` and `ShopItemEntity`. `UserEntity` does NOT receive a filter (breaks Identity).
- **D-04:** `IActiveGroupContext` is constructor-injected into `QuestBoardContext` as a scoped dependency. Both `QuestBoardContext` and `ActiveGroupContextService` are registered as Scoped in DI. Repository layer receives the interface (defined in Domain), not the concrete Service-layer implementation.
- **D-05:** **Null = see all (Phase 28 intentional temporary state).** When `ActiveGroupId` is null — no HTTP context (Hangfire), or session not yet set (user hasn't gone through group picker in Phase 30) — all quests/shop items pass the filter. This is the correct Phase 28 behavior because the group picker does not yet exist. Phase 29 tightens this (see Deferred).

### Hangfire Job Adaptation
- **D-06:** `QuestFinalizedEmailJob.ExecuteAsync` and `SessionReminderJob.ExecuteAsync` each receive an explicit `int groupId` parameter. Callers set the scoped `IActiveGroupContext` to that `groupId` before the repository query runs. This ensures the filter resolves to the correct group, not null.
- **D-07:** `ConfirmationEmailJob` and `QuestDateChangedEmailJob` require **no changes** — neither queries the `Quests` or `ShopItems` tables.
- **D-08:** The daily CRON sweep job that finds all tomorrow's quests (to enqueue per-quest `SessionReminderJob`s) calls a new dedicated repository method `GetQuestsForTomorrowAllGroupsAsync()`. This method internally calls `.IgnoreQueryFilters()` to perform a system-wide cross-group sweep. The method's name makes the cross-group intent explicit.
- **D-09:** How the per-job `groupId` is communicated to the scoped `QuestBoardContext`: the concrete `ActiveGroupContextService` class (not the interface) exposes a `void SetGroupId(int? groupId)` method. Jobs inject `ActiveGroupContextService` directly (not via the interface) and call `SetGroupId(groupId)` at the start of their scope before any repository call.

### Integration Test Stub
- **D-10:** `WebApplicationFactoryBase` gets a public `TestGroupContext` property of type `MutableGroupContext` (a simple settable class implementing `IActiveGroupContext`). Default `ActiveGroupId = 1`. Tests that need a different group set `factory.GroupContext.ActiveGroupId = 2` before the request.
- **D-11:** `MutableGroupContext` is registered as a singleton in `ConfigureTestServices`: `services.AddSingleton<IActiveGroupContext>(factory.GroupContext)`. The instance is controlled by the test, not recreated per request — tests reset it as needed.
- **D-12:** Note on test database: `WebApplicationFactoryBase` uses `UseInMemoryDatabase`, NOT SQLite (despite what ARCHITECTURE.md may indicate). The STACK.md reference to SQLite is outdated. This does not affect the stub approach but the planner should not create SQLite-specific test infrastructure.

### Claude's Discretion
- Session key name used to store `ActiveGroupId` in `ISession` — consistent string constant (e.g., `"ActiveGroupId"`) defined in one place, referenced by `ActiveGroupContextService` and Phase 30's group picker.
- DI registration lifetime for `ActiveGroupContextService` — Scoped (must match `QuestBoardContext` lifetime; not Singleton which would share state across requests).
- `MutableGroupContext` placement — either in `QuestBoard.IntegrationTests/` (test project only) or as an inner class in `WebApplicationFactoryBase`.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Requirements & Scope
- `.planning/REQUIREMENTS.md` §Tenant Isolation — TENANT-01 through TENANT-05: exact requirements and acceptance criteria for this phase
- `.planning/ROADMAP.md` §Phase 28 — phase goal, success criteria, dependency on Phase 27

### Prior Phase Decisions
- `.planning/phases/27-tenant-isolation/../27-group-schema-foundation/27-CONTEXT.md` — Group schema decisions (GroupEntity, UserGroupEntity, GroupId FKs) that Phase 28 builds on
- `.planning/STATE.md` §Key Architectural Decisions (v5.0) — locked design decisions including layer boundaries and IActiveGroupContext placement

### Architecture Constraints
- `.planning/codebase/ARCHITECTURE.md` — layer dependency direction (Service → Domain → Repository); IActiveGroupContext must be in Domain, not Service
- `.planning/codebase/STACK.md` — EF Core version, Hangfire setup

### Key Implementation Files
- `QuestBoard.Repository/Entities/QuestBoardContext.cs` — where `HasQueryFilter` calls must be added; `IActiveGroupContext` constructor injection goes here
- `QuestBoard.Service/Jobs/QuestFinalizedEmailJob.cs` — needs `groupId` parameter added (D-06, D-09)
- `QuestBoard.Service/Jobs/SessionReminderJob.cs` — needs `groupId` parameter added (D-06, D-09)
- `QuestBoard.Repository/Repositories/QuestRepository.cs` — needs `GetQuestsForTomorrowAllGroupsAsync()` with `IgnoreQueryFilters()` (D-08)
- `QuestBoard.IntegrationTests/WebApplicationFactoryBase.cs` — where `IActiveGroupContext` stub is registered (D-10, D-11)

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `QuestBoardContext.OnModelCreating` — already configures all Group FKs from Phase 27; `HasQueryFilter` calls are additive
- `IServiceScopeFactory` + `CreateAsyncScope()` pattern — already used in all four Hangfire jobs; `ActiveGroupContextService` injection follows the same pattern
- `IBackgroundJobClient` `NoOpBackgroundJobClient` stub — pattern for test stubs already established in `WebApplicationFactoryBase`

### Established Patterns
- **Scoped service injection into DbContext:** Constructor injection via primary constructor (C# 12 style, consistent with existing `QuestBoardContext(DbContextOptions<QuestBoardContext> options)`)
- **EF filter nullable comparison:** Use `context.ActiveGroupId == null || e.GroupId == context.ActiveGroupId` — EF Core translates this correctly to SQL `(@activeGroupId IS NULL OR GroupId = @activeGroupId)`
- **Job scope creation:** `await using var scope = scopeFactory.CreateAsyncScope(); var svc = scope.ServiceProvider.GetRequiredService<T>();` — established in all four existing jobs
- **Test service override:** `services.AddSingleton<T>(instance)` in `ConfigureTestServices` — used for `NoOpBackgroundJobClient`, same pattern for `IActiveGroupContext`

### Integration Points
- **CRON daily sweep → per-quest reminders:** The daily job finds tomorrow's quests via `GetQuestsForTomorrowAllGroupsAsync()` (new, cross-group) then enqueues `SessionReminderJob` per quest, passing each quest's `GroupId`
- **QuestFinalizedEmailJob callers:** Wherever the DM finalizes a quest and calls `EnqueueFinalizedEmail` — those callers have the DM's `ActiveGroupId` in session; pass it through to the job
- **`ActiveGroupContextService.SetGroupId` call site:** Inside `QuestFinalizedEmailJob.ExecuteAsync` and `SessionReminderJob.ExecuteAsync`, immediately after `CreateAsyncScope()` and before any repository call

### Known Landmines
- `HasQueryFilter` on `UserEntity` MUST NOT be added — it breaks ASP.NET Core Identity (login, password reset, email confirmation all fail silently)
- EF Core InMemory provider used in integration tests does NOT support all SQL behaviors, but `HasQueryFilter` IS honored in InMemory — the stub GroupId=1 filter will correctly exclude quests with GroupId=2 in cross-group isolation tests
- `IHttpContextAccessor.HttpContext` is null in Hangfire — `ActiveGroupContextService` must use `?.` null-conditional throughout, never `!`
- `ActiveGroupContextService` registered as Scoped means one instance per HTTP request. Hangfire jobs create their own scope via `IServiceScopeFactory` — they get a fresh `ActiveGroupContextService` instance per job execution, which is correct for D-09's `SetGroupId` approach

</code_context>

<specifics>
## Specific Ideas

- Session key constant: `public static class SessionKeys { public const string ActiveGroupId = "ActiveGroupId"; }` — defined in `QuestBoard.Service/` or `QuestBoard.Domain/Constants/`
- `GetQuestsForTomorrowAllGroupsAsync()` naming makes the cross-group intent explicit and greppable in code review

</specifics>

<deferred>
## Deferred Ideas

### Phase 29 MUST-DO — filter tightening (locked decision from Phase 28 discussion)
Phase 28's null = see all is an **intentional temporary state**. Phase 29 must:
1. Add `bool IsSuperAdmin { get; }` to `IActiveGroupContext`
2. Update `ActiveGroupContextService` to return `IsSuperAdmin = true` when the user holds the SuperAdmin Identity role
3. Update the `HasQueryFilter` predicate to: `context.IsSuperAdmin || context.ActiveGroupId == null || e.GroupId == context.ActiveGroupId`
4. After Phase 30 lands (group picker enforces group selection), null will only occur for SuperAdmin in production — the filter will be correct

This was explicitly discussed and agreed during Phase 28 context gathering. Phase 29 planner must pick this up.

### Reviewed Todos (not folded)
None surfaced during cross-reference check.

</deferred>

---

*Phase: 28-tenant-isolation*
*Context gathered: 2026-06-30*
