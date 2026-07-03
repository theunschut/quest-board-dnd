---
phase: 35-board-type-configuration
plan: 01
subsystem: database
tags: [ef-core, automapper, entity-framework, migration, integration-tests, xunit]

# Dependency graph
requires: []
provides:
  - "BoardType enum (OneShot=0, Campaign=1) in QuestBoard.Domain.Enums"
  - "BoardType property on Group, GroupWithMemberCount (enum) and GroupEntity (int)"
  - "Explicit int<->enum AutoMapper ForMember maps for Group<->GroupEntity"
  - "BoardType populated in both GroupRepository projections (GetAllWithMemberCountAsync, GetGroupsForUserAsync)"
  - "EF Core migration AddBoardTypeToGroup adding Groups.BoardType int NOT NULL DEFAULT 0"
  - "Four integration test scaffolds establishing the phase's Nyquist gate (2 red, 2 green)"
affects: [35-02-viewmodel-controller, 35-03-views]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Int-backed enum on entity, explicit AutoMapper ForMember cast (mirrors GroupRole/UserGroupEntity idiom)"
    - "EF Core migration generated via `dotnet ef migrations add` from QuestBoard.Service/, not hand-written"

key-files:
  created:
    - QuestBoard.Domain/Enums/BoardType.cs
    - QuestBoard.Repository/Migrations/20260703113120_AddBoardTypeToGroup.cs
    - QuestBoard.Repository/Migrations/20260703113120_AddBoardTypeToGroup.Designer.cs
  modified:
    - QuestBoard.Domain/Models/Group.cs
    - QuestBoard.Domain/Models/GroupWithMemberCount.cs
    - QuestBoard.Repository/Entities/GroupEntity.cs
    - QuestBoard.Repository/Automapper/EntityProfile.cs
    - QuestBoard.Repository/GroupRepository.cs
    - QuestBoard.Repository/Migrations/QuestBoardContextModelSnapshot.cs
    - QuestBoard.IntegrationTests/Controllers/GroupManagementIntegrationTests.cs

key-decisions:
  - "Generated migration timestamp 20260703113120 sorts after the prior latest (20260702081517) as required"
  - "EditGroup_PostingChangedBoardType_ShouldBeSilentlyIgnored passes green in this plan (not red) because the tamper-protection is implemented by omission — GroupEditViewModel has no BoardType property yet, so nothing can leak through the Edit POST regardless of Plan 02/03 status; this is the intended steady-state behavior already holding true, not a fail-fast violation"

patterns-established:
  - "BoardType follows the exact GroupRole/UserGroupEntity int<->enum convention with no deviation"

requirements-completed: [BOARD-01, BOARD-02]

# Metrics
duration: 15min
completed: 2026-07-03
---

# Phase 35 Plan 01: BoardType Data Layer & Test Scaffolds Summary

**BoardType enum threaded through entity/domain/projection models, explicit AutoMapper int<->enum casts, EF Core migration `AddBoardTypeToGroup` (defaultValue 0), and four integration test scaffolds forming the phase's Nyquist gate (2 red pending Plan 02, 2 already green).**

## Performance

- **Duration:** 15 min
- **Started:** 2026-07-03T11:26:00Z
- **Completed:** 2026-07-03T11:33:58Z
- **Tasks:** 3
- **Files modified:** 10 (3 created, 7 modified)

## Accomplishments
- `BoardType` enum (`OneShot = 0`, `Campaign = 1`) established as the foundation for the whole v6.0 milestone
- Entity (int), domain model (enum), and list-projection model (enum) all carry `BoardType`, wired through explicit AutoMapper `ForMember` casts matching the existing `GroupRole`/`UserGroupEntity` convention
- Both `GroupRepository` projections (`GetAllWithMemberCountAsync`, `GetGroupsForUserAsync`) populate `BoardType` — avoids the RESEARCH.md Pitfall 2 compile failure that would otherwise block Plan 03's Index badge column
- EF Core migration `AddBoardTypeToGroup` adds `Groups.BoardType int NOT NULL DEFAULT 0`, defaulting all existing groups to One-Shot with zero backfill SQL
- Four new integration test facts scaffolded as the phase's feedback gate for Plans 02/03

## Task Commits

Each task was committed atomically:

1. **Task 1: Add BoardType enum and thread it through entity, domain, and projection models** - `0efad42` (feat)
2. **Task 2: Wire AutoMapper explicit ForMember, repository projections, and generate the migration** - `7e1ab39` (feat)
3. **Task 3: Scaffold failing integration tests for BOARD-01 and BOARD-02 behaviors** - `f6d5f5c` (test)

_Note: Task 3 is `tdd="true"` but this plan only scaffolds the RED-phase assertions; the corresponding GREEN implementation lands in Plans 02/03, so no `feat` commit follows in this plan._

## Files Created/Modified
- `QuestBoard.Domain/Enums/BoardType.cs` - New enum, mirrors `GroupRole.cs` exactly
- `QuestBoard.Domain/Models/Group.cs` - Added `BoardType BoardType { get; set; }` + `using QuestBoard.Domain.Enums;`
- `QuestBoard.Domain/Models/GroupWithMemberCount.cs` - Added `BoardType BoardType { get; set; }` + enum import
- `QuestBoard.Repository/Entities/GroupEntity.cs` - Added bare `public int BoardType { get; set; }`, no attributes
- `QuestBoard.Repository/Automapper/EntityProfile.cs` - Replaced bare `CreateMap<GroupEntity, Group>().ReverseMap()` with two explicit `ForMember` int<->enum casts
- `QuestBoard.Repository/GroupRepository.cs` - Added `BoardType = (BoardType)g.BoardType,` to both `GroupWithMemberCount` projections
- `QuestBoard.Repository/Migrations/20260703113120_AddBoardTypeToGroup.cs` - New migration, `AddColumn<int>`/`DropColumn`, `defaultValue: 0`, no `Sql()` calls
- `QuestBoard.Repository/Migrations/20260703113120_AddBoardTypeToGroup.Designer.cs` - EF-generated designer file
- `QuestBoard.Repository/Migrations/QuestBoardContextModelSnapshot.cs` - EF-regenerated snapshot reflecting the new column
- `QuestBoard.IntegrationTests/Controllers/GroupManagementIntegrationTests.cs` - Four new `[Fact]`s: `CreateGroup_WithBoardType_ShouldPersistSelection`, `CreateGroup_WithoutBoardType_ShouldFailValidation`, `EditGroup_PostingChangedBoardType_ShouldBeSilentlyIgnored`, `GroupsIndex_SeededGroup_ShouldDefaultToOneShot`

## Decisions Made
- Migration generated via `dotnet ef migrations add AddBoardTypeToGroup --project ../QuestBoard.Repository` from `QuestBoard.Service/`, not hand-written; verified `Up`/`Down` match the `AddSignupRoleToPlayerSignup` template exactly (single `AddColumn<int>`/`DropColumn`, no `Sql()` backfill)
- Generated migration filename: `20260703113120_AddBoardTypeToGroup.cs` — timestamp prefix `20260703113120` sorts numerically after the prior latest `20260702081517`, as required
- No other AutoMapper consumer needed updating — `GroupWithMemberCount` is hand-populated via LINQ `.Select(...)` in `GroupRepository`, not built via AutoMapper, so it required no separate `CreateMap` entry (confirmed per 35-PATTERNS.md)

## Deviations from Plan

None — plan executed exactly as written. One clarification worth recording (not a deviation, since it matches the plan's own design intent):

**Test result nuance on `EditGroup_PostingChangedBoardType_ShouldBeSilentlyIgnored`:** The plan's action note states "these tests are EXPECTED TO FAIL after this plan... Only `GroupsIndex_SeededGroup_ShouldDefaultToOneShot` should pass." In practice, `EditGroup_PostingChangedBoardType_ShouldBeSilentlyIgnored` also passes today. This was verified deliberately (not skipped past per the TDD fail-fast rule) by reading the current `GroupEditViewModel`/`GroupController.Edit` — `GroupEditViewModel` has no `BoardType` property yet and `Edit` POST only ever assigns `group.Name = model.Name;`, so a posted `BoardType` field has nothing to bind to and cannot possibly change the stored value, regardless of Plan 02/03 status. The assertion (`stored BoardType unchanged after POSTing a different value`) is already true by construction, not because the D-06 tamper-defense is fully wired end-to-end. This is the intended final steady-state (tamper-protection via omission, per D-06/35-CONTEXT.md) already holding, so it is correctly green rather than a test that "isn't testing what you think." No fix applied; the assertion was not weakened.

## Issues Encountered

**`dotnet build QuestBoard.Domain QuestBoard.Repository` (as written in Task 1's `<verify>`) fails** with `MSB1008: Only one project can be specified` — `dotnet build` accepts only a single project/solution argument, not a space-separated list. Worked around by running `dotnet build QuestBoard.Domain` and `dotnet build QuestBoard.Repository` as two separate invocations (both passed), then confirming the full solution with a plain `dotnet build`. No code change required; this is a verification-command issue, not a plan defect requiring escalation.

## User Setup Required

None - no external service configuration required. The migration auto-applies on next `dotnet run`/startup per existing project convention.

## Next Phase Readiness

- `BoardType` contract (enum + entity/domain/projection properties) is fully available for Plan 02 (`GroupCreateViewModel`/`GroupEditViewModel`/`GroupController`) and Plan 03 (Razor views) to consume directly, per the interfaces block in 35-01-PLAN.md
- Test gate in place: `CreateGroup_WithBoardType_ShouldPersistSelection` and `CreateGroup_WithoutBoardType_ShouldFailValidation` are red and will turn green once Plan 02 wires the ViewModel/controller; `EditGroup_PostingChangedBoardType_ShouldBeSilentlyIgnored` and `GroupsIndex_SeededGroup_ShouldDefaultToOneShot` are green and must remain green through Plans 02/03
- No blockers identified for Plan 02

---
*Phase: 35-board-type-configuration*
*Completed: 2026-07-03*

## Self-Check: PASSED

All created files and commit hashes verified present:
- FOUND: QuestBoard.Domain/Enums/BoardType.cs
- FOUND: QuestBoard.Repository/Migrations/20260703113120_AddBoardTypeToGroup.cs
- FOUND: QuestBoard.Repository/Migrations/20260703113120_AddBoardTypeToGroup.Designer.cs
- FOUND: .planning/phases/35-board-type-configuration/35-01-SUMMARY.md
- FOUND commit: 0efad42 (Task 1)
- FOUND commit: 7e1ab39 (Task 2)
- FOUND commit: f6d5f5c (Task 3)
