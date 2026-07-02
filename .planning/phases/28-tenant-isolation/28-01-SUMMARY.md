---
phase: 28-tenant-isolation
plan: "01"
subsystem: tenant-isolation
tags:
  - ef-core
  - global-query-filter
  - multi-tenancy
  - integration-tests
dependency_graph:
  requires:
    - "Phase 27 Group Schema Foundation (GroupId FKs on QuestEntity, ShopItemEntity)"
  provides:
    - "IActiveGroupContext interface (Domain layer)"
    - "ActiveGroupContextService (Session-backed, Hangfire-compatible)"
    - "HasQueryFilter on QuestEntity + ShopItemEntity"
    - "MutableGroupContext test stub (GroupId=1 default)"
  affects:
    - "QuestBoardContext constructor (new parameter)"
    - "TestDatabase.CreateContext() (new parameter)"
    - "WebApplicationFactoryBase (new property + DI override)"
    - "Program.cs (new scoped registrations)"
tech_stack:
  added:
    - "IActiveGroupContext (Domain interface)"
    - "ActiveGroupContextService (IHttpContextAccessor + session read)"
    - "SessionKeys static constants class"
    - "MutableGroupContext (settable test stub)"
    - "EF Core HasQueryFilter on QuestEntity, ShopItemEntity"
  patterns:
    - "Primary constructor injection (C# 12) for QuestBoardContext and ActiveGroupContextService"
    - "Lambda closure over service instance (not captured value) in HasQueryFilter"
    - "Dual-registration: AddScoped<ActiveGroupContextService>() + AddScoped<IActiveGroupContext>(sp => ...)"
    - "ConfigureTestServices singleton override pattern for IActiveGroupContext"
key_files:
  created:
    - "QuestBoard.Domain/Interfaces/IActiveGroupContext.cs"
    - "QuestBoard.Service/Constants/SessionKeys.cs"
    - "QuestBoard.Service/Services/ActiveGroupContextService.cs"
    - "QuestBoard.IntegrationTests/Helpers/MutableGroupContext.cs"
  modified:
    - "QuestBoard.Repository/Entities/QuestBoardContext.cs"
    - "QuestBoard.IntegrationTests/Helpers/TestDatabase.cs"
    - "QuestBoard.IntegrationTests/WebApplicationFactoryBase.cs"
    - "QuestBoard.Service/Program.cs"
    - "QuestBoard.IntegrationTests/Controllers/ShopControllerIntegrationTests.cs"
decisions:
  - "IActiveGroupContext placed in Domain (not Service) so Repository can consume it without upward dependency"
  - "HasQueryFilter uses lambda closure over service instance, never captured local var (prevents bake-at-startup bug)"
  - "UserEntity excluded from HasQueryFilter — ASP.NET Core Identity breaks silently if filter is applied"
  - "TestDatabase.CreateContext passes null ActiveGroupId (see-all) for seeding outside DI scope"
  - "TestGroupContext registered as singleton — test code mutates it directly before requests"
metrics:
  duration: "4 minutes"
  completed_date: "2026-06-30"
  tasks: 2
  files_created: 4
  files_modified: 5
---

# Phase 28 Plan 01: IActiveGroupContext Infrastructure Summary

**One-liner:** EF Core global query filters on QuestEntity/ShopItemEntity backed by session-aware IActiveGroupContext with a settable test stub defaulting to GroupId=1.

## What Was Built

Plan 01 delivers the compile-interdependent foundation for tenant isolation in one wave:

1. **IActiveGroupContext** (`QuestBoard.Domain/Interfaces/`) — single read-only `int? ActiveGroupId` property; null means "see all" (Phase 28 intentional temporary state).

2. **SessionKeys** (`QuestBoard.Service/Constants/`) — static constant `"ActiveGroupId"` string for session lookups; single definition, referenced by `ActiveGroupContextService`.

3. **ActiveGroupContextService** (`QuestBoard.Service/Services/`) — implements `IActiveGroupContext`, reads from `IHttpContextAccessor.HttpContext?.Session?.GetInt32(...)` using `?.` null-conditional throughout (safe for Hangfire background context). Exposes `SetGroupId(int?)` on the concrete class for job use (D-09).

4. **MutableGroupContext** (`QuestBoard.IntegrationTests/Helpers/`) — settable `int? ActiveGroupId { get; set; } = 1`; registered as singleton in test factory.

5. **QuestBoardContext** modified — second primary constructor parameter `IActiveGroupContext activeGroupContext`; `HasQueryFilter` appended in `OnModelCreating` for `QuestEntity` and `ShopItemEntity` with lambda closing over the service instance.

6. **TestDatabase.CreateContext()** — passes `new MutableGroupContext { ActiveGroupId = null }` (see-all behavior for seeding outside DI).

7. **WebApplicationFactoryBase** — new `TestGroupContext` property + `services.AddSingleton<IActiveGroupContext>(TestGroupContext)` in `ConfigureTestServices`.

8. **Program.cs** — `AddHttpContextAccessor()`, `AddScoped<ActiveGroupContextService>()`, `AddScoped<IActiveGroupContext>(sp => sp.GetRequiredService<ActiveGroupContextService>())` added outside the Testing environment guard (DI registration needed in both environments; test factory overrides with singleton).

## Verification

- `dotnet build` on full solution: 0 errors
- `dotnet test`: 194 tests pass (55 unit + 139 integration)

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] ShopControllerIntegrationTests inline seeder missing GroupId**
- **Found during:** Task 2 — first test run after HasQueryFilter was wired
- **Issue:** `SeedPublishedShopItemsAsync` in `ShopControllerIntegrationTests.cs` added `ShopItemEntity` records without setting `GroupId`. After the filter was applied, these items were excluded by the `e.GroupId == activeGroupContext.ActiveGroupId` predicate (ActiveGroupId=1, GroupId=0), causing 3 tests to find 0 items instead of 12/15.
- **Fix:** Added `GroupId = 1` to the inline entity initializer in `SeedPublishedShopItemsAsync`.
- **Files modified:** `QuestBoard.IntegrationTests/Controllers/ShopControllerIntegrationTests.cs`
- **Commit:** eb86d75

## Known Stubs

None — all data paths are wired. `ActiveGroupId = null` in TestDatabase is intentional (see-all for seeding), documented in code comments.

## Threat Surface Scan

No new network endpoints, auth paths, or file access patterns introduced. Changes are internal to EF Core model configuration and DI registration. Threat T-28-02 (direct object reference) is mitigated by the `HasQueryFilter` — queries filtering by a non-matching GroupId return null/empty, naturally producing 404 from controllers. T-28-03 (lambda value-capture bug) is mitigated by the lambda closure pattern and the CRITICAL comment in `QuestBoardContext.OnModelCreating`.

## Self-Check

Files created/modified:
- [x] `QuestBoard.Domain/Interfaces/IActiveGroupContext.cs` — exists
- [x] `QuestBoard.Service/Constants/SessionKeys.cs` — exists
- [x] `QuestBoard.Service/Services/ActiveGroupContextService.cs` — exists
- [x] `QuestBoard.IntegrationTests/Helpers/MutableGroupContext.cs` — exists
- [x] `QuestBoard.Repository/Entities/QuestBoardContext.cs` — modified
- [x] `QuestBoard.IntegrationTests/Helpers/TestDatabase.cs` — modified
- [x] `QuestBoard.IntegrationTests/WebApplicationFactoryBase.cs` — modified
- [x] `QuestBoard.Service/Program.cs` — modified

Commits:
- [x] 33d794a — Task 1 (four new files)
- [x] eb86d75 — Task 2 (four modified files + Rule 1 fix)

## Self-Check: PASSED
