---
phase: 28-tenant-isolation
fixed_at: 2026-06-30T13:00:00Z
review_path: .planning/phases/28-tenant-isolation/28-REVIEW.md
iteration: 1
findings_in_scope: 3
fixed: 2
skipped: 1
status: partial
---

# Phase 28: Code Review Fix Report

**Fixed at:** 2026-06-30T13:00:00Z
**Source review:** .planning/phases/28-tenant-isolation/28-REVIEW.md
**Iteration:** 1

**Summary:**
- Findings in scope: 3 (WR-01, WR-02, WR-03)
- Fixed: 2
- Skipped: 1

## Fixed Issues

### WR-01: Singleton `MutableGroupContext` captures a `QuestBoardContext` that is registered as Scoped — test state bleed risk

**Files modified:** `QuestBoard.IntegrationTests/Tests/TenantIsolationTests.cs`
**Commit:** fb76f45
**Applied fix:** Added `IAsyncLifetime` to `TenantIsolationTests`. The class declaration now reads `: IClassFixture<WebApplicationFactoryBase>, IAsyncLifetime`. Added `InitializeAsync() => Task.CompletedTask` and `DisposeAsync()` that resets `factory.TestGroupContext.ActiveGroupId = 1`. This ensures the singleton is always restored to the default group after the test class lifecycle ends, preventing state bleed into subsequently-executed test classes.

---

### WR-03: `ActiveGroupContextService` is registered twice in the same DI scope — `IActiveGroupContext` may resolve a different instance than `ActiveGroupContextService`

**Files modified:** `QuestBoard.Service/Program.cs`
**Commit:** c0b929d
**Applied fix:** Added a multi-line explanatory comment block immediately before the two `AddScoped` registrations on lines 108-110. The comment documents the D-09 dual registration pattern from STATE.md: (1) why `AddScoped<ActiveGroupContextService>()` is needed so Hangfire jobs can resolve the concrete type and call `SetGroupId(groupId)` — a method absent from the `IActiveGroupContext` interface; (2) why `AddScoped<IActiveGroupContext>(factory)` delegates to the same scoped instance, guaranteeing that `SetGroupId` mutations are visible to `QuestBoardContext` within the same scope; and (3) a maintenance warning not to collapse both into a single `AddScoped<IActiveGroupContext, ActiveGroupContextService>()` call, which would break concrete-type resolution in Hangfire jobs.

---

## Skipped Issues

### WR-02: `QuestDateChangedEmailJob` does not call `SetGroupId` — query filter uses the default (null = see all)

**File:** `QuestBoard.Service/Jobs/QuestDateChangedEmailJob.cs:24`
**Reason:** false positive — no repository calls in job body; data is pre-resolved at enqueue time. The job receives all required data (recipient emails, player names, quest title, DM name, old date, new date) as constructor parameters. It makes zero `IQuestRepository` calls. Its only service dependencies are `IEmailRenderService` and `IEmailService`, neither of which is scoped to a group. Adding a defensive `SetGroupId` call would introduce an unnecessary `int groupId` parameter threading through `IQuestEmailDispatcher` and `HangfireQuestEmailDispatcher` for no functional benefit. The sibling jobs (`QuestFinalizedEmailJob`, `SessionReminderJob`) call `SetGroupId` because they make repository queries at job-execution time; `QuestDateChangedEmailJob` does not.
**Original issue:** `QuestDateChangedEmailJob.ExecuteAsync` does not resolve `ActiveGroupContextService` or call `SetGroupId`, unlike the sibling Hangfire jobs.

---

_Fixed: 2026-06-30T13:00:00Z_
_Fixer: Claude (gsd-code-fixer)_
_Iteration: 1_
