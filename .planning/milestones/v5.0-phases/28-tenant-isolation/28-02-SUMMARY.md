---
phase: 28-tenant-isolation
plan: "02"
subsystem: tenant-isolation
tags:
  - hangfire
  - multi-tenancy
  - ef-core
  - background-jobs
  - group-isolation
dependency_graph:
  requires:
    - phase: "28-01"
      provides: "IActiveGroupContext, ActiveGroupContextService.SetGroupId(), HasQueryFilter on QuestEntity/ShopItemEntity"
  provides:
    - "GetQuestsForTomorrowAllGroupsAsync with IgnoreQueryFilters (D-08)"
    - "QuestFinalizedEmailJob.ExecuteAsync with groupId param + SetGroupId before repo call (D-09)"
    - "SessionReminderJob.ExecuteAsync with groupId param + SetGroupId before repo call (D-09)"
    - "DailyReminderJob cross-group sweep calling GetQuestsForTomorrowAllGroupsAsync + forwarding quest.GroupId"
    - "Full dispatcher chain: IQuestEmailDispatcher, IReminderJobDispatcher, Hangfire + Null concretes"
    - "QuestController passes IActiveGroupContext.ActiveGroupId to EnqueueSessionReminder"
  affects:
    - "Phase 29 SuperAdmin — will add IsSuperAdmin to IActiveGroupContext and tighten null semantics"
    - "Phase 30 Group UX — will enforce group selection before DM can access manage page"
tech_stack:
  added: []
  patterns:
    - "D-09: Jobs inject ActiveGroupContextService (concrete) via GetRequiredService to call SetGroupId before any repository call"
    - "D-08: GetQuestsForTomorrowAllGroupsAsync uses IgnoreQueryFilters for explicit cross-group system-wide sweep"
    - "groupId threaded through the full dispatcher chain: Domain interface → Hangfire concrete → Null concrete → job signature"
key_files:
  created: []
  modified:
    - "QuestBoard.Domain/Interfaces/IQuestRepository.cs"
    - "QuestBoard.Domain/Interfaces/IQuestEmailDispatcher.cs"
    - "QuestBoard.Domain/Interfaces/IReminderJobDispatcher.cs"
    - "QuestBoard.Repository/QuestRepository.cs"
    - "QuestBoard.Domain/Services/QuestService.cs"
    - "QuestBoard.Service/Services/HangfireQuestEmailDispatcher.cs"
    - "QuestBoard.Service/Services/HangfireReminderJobDispatcher.cs"
    - "QuestBoard.Service/Services/NullQuestEmailDispatcher.cs"
    - "QuestBoard.Service/Services/NullReminderJobDispatcher.cs"
    - "QuestBoard.Service/Jobs/QuestFinalizedEmailJob.cs"
    - "QuestBoard.Service/Jobs/SessionReminderJob.cs"
    - "QuestBoard.Service/Jobs/DailyReminderJob.cs"
    - "QuestBoard.Service/Controllers/QuestBoard/QuestController.cs"
    - "QuestBoard.UnitTests/Services/QuestServiceTests.cs"
    - "QuestBoard.UnitTests/Services/EmailConfirmationJobGuardTests.cs"
    - "QuestBoard.UnitTests/Services/SessionReminderJobTests.cs"
    - "QuestBoard.UnitTests/Services/DailyReminderJobTests.cs"
key_decisions:
  - "QuestController uses activeGroupContext.ActiveGroupId ?? 1 as safe fallback for Phase 28 — null means no session yet (single-group era); GroupId=1 (EuphoriaInn) is correct for existing deployment"
  - "ActiveGroupContextService registered as Scoped ensures SetGroupId in job scope does not bleed into unrelated request scopes"
  - "IgnoreQueryFilters restricted to one method (GetQuestsForTomorrowAllGroupsAsync) — method name makes cross-group intent explicit and greppable"
requirements-completed:
  - TENANT-04
duration: 6min
completed: "2026-06-30"
---

# Phase 28 Plan 02: Hangfire GroupId Threading Summary

**groupId threaded through the full Hangfire dispatcher chain with SetGroupId called before every repository access, plus a cross-group daily sweep via GetQuestsForTomorrowAllGroupsAsync with IgnoreQueryFilters — TENANT-04 complete.**

## Performance

- **Duration:** 6 min
- **Started:** 2026-06-30T07:12:40Z
- **Completed:** 2026-06-30T07:18:44Z
- **Tasks:** 2
- **Files modified:** 17

## Accomplishments

- Full dispatcher chain updated atomically — IQuestEmailDispatcher, IReminderJobDispatcher, both Hangfire and Null concretes, plus both job signatures all carry `int groupId` as a required parameter; compile gate prevents any caller from omitting it
- Both jobs call `GetRequiredService<ActiveGroupContextService>().SetGroupId(groupId)` immediately after `CreateAsyncScope()` and before any `GetRequiredService<IQuestRepository>()` call — D-09 ordering enforced
- DailyReminderJob switched from `GetFinalizedQuestsForDateAsync` to `GetQuestsForTomorrowAllGroupsAsync`, which uses `IgnoreQueryFilters()` for system-wide cross-group sweep; each enqueued `SessionReminderJob` receives `quest.GroupId` — D-08 done, Pitfall 5 from RESEARCH.md eliminated
- QuestController injects `IActiveGroupContext` and passes `ActiveGroupId ?? 1` to `EnqueueSessionReminder` — DM-triggered manual reminder correctly scoped to active group
- 194 tests pass (55 unit + 139 integration)

## Task Commits

Each task was committed atomically:

1. **Task 1: Update domain interfaces, QuestRepository, and QuestService caller** - `a848a11` (feat)
2. **Task 2: Update all dispatcher concretes, job signatures, and QuestController caller** - `202c4de` (feat)

**Plan metadata:** (docs commit — see below)

## Files Created/Modified

- `QuestBoard.Domain/Interfaces/IQuestRepository.cs` — added `GetQuestsForTomorrowAllGroupsAsync` method with doc comment
- `QuestBoard.Domain/Interfaces/IQuestEmailDispatcher.cs` — added `int groupId` as second parameter to `EnqueueFinalizedEmail`
- `QuestBoard.Domain/Interfaces/IReminderJobDispatcher.cs` — added `int groupId` as second parameter to `EnqueueSessionReminder`
- `QuestBoard.Repository/QuestRepository.cs` — implemented `GetQuestsForTomorrowAllGroupsAsync` with `IgnoreQueryFilters()`
- `QuestBoard.Domain/Services/QuestService.cs` — passes `quest.GroupId` to `EnqueueFinalizedEmail`
- `QuestBoard.Service/Services/HangfireQuestEmailDispatcher.cs` — `int groupId` parameter + forwarded to job Enqueue call
- `QuestBoard.Service/Services/HangfireReminderJobDispatcher.cs` — `int groupId` parameter + forwarded to job Enqueue call
- `QuestBoard.Service/Services/NullQuestEmailDispatcher.cs` — `int groupId` parameter (no-op body unchanged)
- `QuestBoard.Service/Services/NullReminderJobDispatcher.cs` — `int groupId` parameter (no-op body unchanged)
- `QuestBoard.Service/Jobs/QuestFinalizedEmailJob.cs` — `int groupId` param + `ActiveGroupContextService.SetGroupId(groupId)` before repo call
- `QuestBoard.Service/Jobs/SessionReminderJob.cs` — `int groupId` param (before optional bools) + `SetGroupId(groupId)` before repo call
- `QuestBoard.Service/Jobs/DailyReminderJob.cs` — calls `GetQuestsForTomorrowAllGroupsAsync` + passes `quest.GroupId` to enqueued jobs
- `QuestBoard.Service/Controllers/QuestBoard/QuestController.cs` — injects `IActiveGroupContext`; passes `ActiveGroupId ?? 1` to `EnqueueSessionReminder`
- `QuestBoard.UnitTests/Services/QuestServiceTests.cs` — updated `EnqueueFinalizedEmail` assertions with `Arg.Any<int>()` groupId argument
- `QuestBoard.UnitTests/Services/EmailConfirmationJobGuardTests.cs` — updated all three `EnqueueFinalizedEmail` assertions with groupId argument
- `QuestBoard.UnitTests/Services/SessionReminderJobTests.cs` — updated for new `groupId` param + wired `ActiveGroupContextService` into service provider stub
- `QuestBoard.UnitTests/Services/DailyReminderJobTests.cs` — updated to stub `GetQuestsForTomorrowAllGroupsAsync` (not the old method); added `GroupId` to quest helper

## Decisions Made

- `QuestController` uses `activeGroupContext.ActiveGroupId ?? 1` as safe fallback — in Phase 28, null means session not yet set (no group picker); GroupId=1 (EuphoriaInn) is the correct single-group-era default. Phase 30 will enforce group selection before DM can access the manage page.
- Jobs resolve `ActiveGroupContextService` via `GetRequiredService<ActiveGroupContextService>()` (concrete type, not interface) because `SetGroupId` is on the concrete class only — this is the D-09 pattern.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] SessionReminderJobTests did not list in plan — updated for new groupId signature**
- **Found during:** Task 2 — build after updating `SessionReminderJob.ExecuteAsync`
- **Issue:** `SessionReminderJobTests.cs` directly calls `_sut.ExecuteAsync(questId: 1, forceResend: false)` — missing the new required `int groupId` parameter (CS7036). Additionally, the test's `IServiceProvider` stub did not provide `ActiveGroupContextService`, which the job now resolves via `GetRequiredService<ActiveGroupContextService>()`.
- **Fix:** Added `using QuestBoard.Service.Services` + `using Microsoft.AspNetCore.Http`; registered `ActiveGroupContextService` instance in service provider stub; added `groupId: 1` to all `ExecuteAsync` call sites (7 calls).
- **Files modified:** `QuestBoard.UnitTests/Services/SessionReminderJobTests.cs`
- **Verification:** All 7 calls compile; tests pass.
- **Committed in:** `202c4de` (Task 2 commit)

**2. [Rule 1 - Bug] DailyReminderJobTests did not list in plan — updated for renamed method and new groupId forwarding**
- **Found during:** Task 2 — test run after completing all dispatcher + job changes
- **Issue:** `DailyReminderJobTests` stubs `GetFinalizedQuestsForDateAsync` but the job now calls `GetQuestsForTomorrowAllGroupsAsync`, causing NSubstitute to return default empty list; `_backgroundJobClient.Received(2).Create(...)` assertion fails because no quests are returned. Additionally, `MakeQuest` helper didn't set `GroupId`, which would cause Hangfire enqueue calls with `quest.GroupId = 0` instead of intended value.
- **Fix:** Updated both `GetFinalizedQuestsForDateAsync` stubs to `GetQuestsForTomorrowAllGroupsAsync`; added `GroupId = groupId` (default 1) to `MakeQuest` helper.
- **Files modified:** `QuestBoard.UnitTests/Services/DailyReminderJobTests.cs`
- **Verification:** `ExecuteAsync_WhenTwoQuestsForTomorrow_EnqueuesTwoJobs` passes; all tests pass.
- **Committed in:** `202c4de` (Task 2 commit)

---

**Total deviations:** 2 auto-fixed (both Rule 1 — test files not listed in plan that reference changed signatures)
**Impact on plan:** Both auto-fixes were necessary to maintain a compiling and fully passing test suite. No scope creep.

## Issues Encountered

None — all changes proceeded as planned. The two auto-fixes were discovered immediately via compiler errors and one test failure, and were resolved inline.

## Known Stubs

None — all data paths are wired. `ActiveGroupContextService.SetGroupId(groupId)` is called with the concrete group ID at every Hangfire job execution point.

## Threat Surface Scan

No new network endpoints, auth paths, or file access patterns introduced. All changes are internal to existing Hangfire job bodies and dispatcher interfaces. Threat mitigations from the plan's threat register:

- **T-28-06** (wrong group's quests in job): mitigated — `SetGroupId(groupId)` called before any repository access in both jobs.
- **T-28-07** (IgnoreQueryFilters misuse): accepted — single call site inside `GetQuestsForTomorrowAllGroupsAsync`, method name makes intent explicit.
- **T-28-08** (DailyReminderJob missing groupId): mitigated — `quest.GroupId` forwarded to each enqueued `SessionReminderJob`.
- **T-28-09** (interface signature mismatch): mitigated — full chain updated atomically; build gate enforces matching signatures.

## Self-Check

Files modified exist:
- [x] `QuestBoard.Domain/Interfaces/IQuestRepository.cs` — GetQuestsForTomorrowAllGroupsAsync present
- [x] `QuestBoard.Domain/Interfaces/IQuestEmailDispatcher.cs` — int groupId second param present
- [x] `QuestBoard.Domain/Interfaces/IReminderJobDispatcher.cs` — int groupId second param present
- [x] `QuestBoard.Repository/QuestRepository.cs` — GetQuestsForTomorrowAllGroupsAsync with IgnoreQueryFilters present
- [x] `QuestBoard.Domain/Services/QuestService.cs` — quest.GroupId passed to EnqueueFinalizedEmail
- [x] `QuestBoard.Service/Services/HangfireQuestEmailDispatcher.cs` — int groupId present
- [x] `QuestBoard.Service/Services/HangfireReminderJobDispatcher.cs` — int groupId present
- [x] `QuestBoard.Service/Services/NullQuestEmailDispatcher.cs` — int groupId present
- [x] `QuestBoard.Service/Services/NullReminderJobDispatcher.cs` — int groupId present
- [x] `QuestBoard.Service/Jobs/QuestFinalizedEmailJob.cs` — groupId param + SetGroupId before repo call
- [x] `QuestBoard.Service/Jobs/SessionReminderJob.cs` — groupId param + SetGroupId before repo call
- [x] `QuestBoard.Service/Jobs/DailyReminderJob.cs` — GetQuestsForTomorrowAllGroupsAsync + quest.GroupId
- [x] `QuestBoard.Service/Controllers/QuestBoard/QuestController.cs` — IActiveGroupContext injected; groupId passed

Commits:
- [x] a848a11 — Task 1 (domain interfaces + repository + QuestService + unit test assertions)
- [x] 202c4de — Task 2 (all dispatcher concretes + job signatures + QuestController + auto-fix tests)

Test results:
- [x] 55 unit tests pass
- [x] 139 integration tests pass
- [x] 194 total pass, 0 fail

## Self-Check: PASSED
