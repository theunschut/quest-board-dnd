---
phase: 28-tenant-isolation
plan: "03"
subsystem: tenant-isolation
tags:
  - integration-tests
  - ef-core
  - global-query-filter
  - multi-tenancy
  - tdd
dependency_graph:
  requires:
    - "Phase 28-01: IActiveGroupContext + HasQueryFilter + MutableGroupContext test stub"
    - "Phase 28-02: Hangfire groupId threading + GetQuestsForTomorrowAllGroupsAsync"
  provides:
    - "Cross-group tenant isolation integration tests (TENANT-03)"
    - "TDD proof that HasQueryFilter correctly excludes other-group quests in EF InMemory"
    - "Human-verified running application: quest list, shop, Send Reminder — no NullReferenceException"
  affects:
    - "Phase 29 — can now add IsSuperAdmin to tighten null semantics (D-05 deferred)"
tech_stack:
  added: []
  patterns:
    - "IClassFixture<WebApplicationFactoryBase> with primary constructor injection (xUnit v3)"
    - "TestDataHelper.ClearDatabaseAsync before each test (clean slate with roles + Group 1)"
    - "factory.Database.CreateContext() for seeding (ActiveGroupId=null, bypasses filter)"
    - "factory.TestGroupContext.ActiveGroupId mutation to scope HTTP requests to target group"
key_files:
  created:
    - "QuestBoard.IntegrationTests/Tests/TenantIsolationTests.cs"
  modified: []
decisions:
  - "Test 3 (null=see-all) queries via factory.Database.CreateContext() rather than HTTP — unambiguous proof of D-05 predicate without routing through stub"
  - "TestDataHelper.ClearDatabaseAsync chosen over factory.ResetDatabase() — seeds roles and Group 1 FK dependency, preventing SaveChanges FK constraint failures"
metrics:
  duration: "41 minutes (includes checkpoint wait)"
  completed_date: "2026-06-30"
  tasks: 1
  files_created: 1
  files_modified: 0
---

# Phase 28 Plan 03: Cross-Group Isolation Integration Tests Summary

**One-liner:** Three xUnit integration tests proving EF Core HasQueryFilter correctly hides Group-2 quests from Group-1 context and exposes all quests when ActiveGroupId is null.

## What Was Built

A single test file `QuestBoard.IntegrationTests/Tests/TenantIsolationTests.cs` with three test methods that exercise the `HasQueryFilter` predicate end-to-end:

1. **`GroupFilter_HidesQuestFromOtherGroup`** — Seeds Group 2 + a quest in Group 2. Sets `factory.TestGroupContext.ActiveGroupId = 1`. GETs the quest list page (`/`). Asserts the Group-2 quest title does NOT appear in the response body. Proves the filter excludes cross-group data.

2. **`GroupFilter_ShowsQuestFromSameGroup`** — Seeds a quest in Group 1. Sets `factory.TestGroupContext.ActiveGroupId = 1`. GETs the quest list page. Asserts the Group-1 quest title IS present in the response body. Proves the filter passes same-group data.

3. **`GroupFilter_NullGroupIdShowsAllGroups`** — Seeds quests in both Group 1 and Group 2. Queries via `factory.Database.CreateContext()` (which uses `MutableGroupContext { ActiveGroupId = null }`). Asserts both quest titles appear in the DbContext query result. Proves the null = see-all predicate (D-05) works correctly.

## Verification

- `dotnet test --filter TenantIsolationTests`: 3/3 pass
- `dotnet test QuestBoard.slnx --no-build`: **197 tests pass** (55 unit + 142 integration), 0 failures
  - Previous baseline: 194 tests (55 unit + 139 integration)
  - Net addition: +3 tests from this plan
- Human checkpoint approved: quest list loads, shop loads, Send Reminder present on manage page, no `NullReferenceException` in logs

## Final Phase Verification Checklist

- [x] `dotnet test --no-build` exits 0 — 197 tests pass (194 + 3 new)
- [x] `QuestBoardContext.cs` — `HasQueryFilter` on QuestEntity and ShopItemEntity, NOT on UserEntity
- [x] `QuestFinalizedEmailJob.cs` — `SetGroupId(groupId)` before `GetRequiredService<IQuestRepository>()`
- [x] `SessionReminderJob.cs` — `SetGroupId(groupId)` before `GetRequiredService<IQuestRepository>()`
- [x] `DailyReminderJob.cs` — `GetQuestsForTomorrowAllGroupsAsync` and `quest.GroupId`
- [x] `QuestRepository.cs` — `IgnoreQueryFilters()` in `GetQuestsForTomorrowAllGroupsAsync`
- [x] `WebApplicationFactoryBase.cs` — `AddSingleton<IActiveGroupContext>(TestGroupContext)`
- [x] `QuestBoard.Domain/Interfaces/IActiveGroupContext.cs` — exists with `int? ActiveGroupId { get; }`
- [x] Human checkpoint: quest list and shop load, no NullReferenceException in logs

## Phase 28 Success Criteria — Final Status

| # | Criterion | Status |
|---|-----------|--------|
| 1 | IActiveGroupContext in Domain layer — TENANT-01 done | Complete (28-01) |
| 2 | ActiveGroupContextService in Service layer, reads Session, SetGroupId method — TENANT-02 done | Complete (28-01) |
| 3 | HasQueryFilter on Quest and ShopItem; UserEntity excluded; cross-group isolation test green — TENANT-03 done | Complete (28-01 + 28-03) |
| 4 | All Hangfire jobs scope correctly (SetGroupId) or sweep correctly (IgnoreQueryFilters) — TENANT-04 done | Complete (28-02) |
| 5 | MutableGroupContext stub GroupId=1 default; TestDatabase compiles; all tests pass — TENANT-05 done | Complete (28-01 + 28-03) |
| 6 | Human checkpoint approved — application runs without errors | Complete (28-03) |

**Phase 28 is COMPLETE.**

## Deviations from Plan

### Auto-fixed Issues

None — plan executed exactly as written.

### Notes on TDD Classification

This plan was marked `tdd="true"`. The test file was written and run immediately after creation. However, since the `HasQueryFilter` infrastructure already existed from Plan 28-01, the tests passed GREEN on first run without a RED phase. The filter was already implemented — this plan adds the observable TDD verification artifact rather than driving new implementation. Noted as a TDD gate compliance observation: the RED gate does not formally apply here because the implementation predates this plan.

## Known Stubs

None — all data paths are wired. The `null = see all` semantics on `ActiveGroupId` are intentional Phase 28 behavior (D-05), documented in both the context and test code comments.

## Threat Surface Scan

No new network endpoints, auth paths, or file access patterns introduced. Test file is integration-test-only; not deployed. Threat mitigations from plan:

- **T-28-10** (TENANT-03 unverifiable without isolation tests): mitigated — three explicit tests cover hide-other-group, show-same-group, null-see-all.
- **T-28-11** (mutable singleton state bleeds between tests): mitigated — `TestDataHelper.ClearDatabaseAsync()` at the start of each test + explicit `factory.TestGroupContext.ActiveGroupId` set; clean slate per test.

## Self-Check

Files created:
- [x] `QuestBoard.IntegrationTests/Tests/TenantIsolationTests.cs` — exists, 136 lines, 3 test methods

Commits:
- [x] 10321b4 — `test(28-03): add cross-group tenant isolation integration tests`

Test results:
- [x] 3 TenantIsolationTests pass
- [x] 197 total pass, 0 fail

## Self-Check: PASSED
