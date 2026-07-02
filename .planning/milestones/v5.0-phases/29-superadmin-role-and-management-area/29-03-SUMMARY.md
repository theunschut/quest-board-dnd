---
phase: 29-superadmin-role-and-management-area
plan: "03"
subsystem: database
tags: [group-management, efcore, repository-pattern, di, domain-service]

requires:
  - phase: 29-01
    provides: SuperAdminOnly policy registered in Program.cs; IUserService extended with GetGroupRoleAsync/SetGroupRoleAsync

provides:
  - IGroupRepository interface (Domain layer) with GetAllWithMemberCountAsync, HasMembersAsync, AddMemberAsync, RemoveMemberAsync, GetMembersAsync
  - IGroupService interface (Domain layer) mirroring IGroupRepository methods plus base CRUD
  - GroupWithMemberCount DTO for member-count projection queries
  - GroupService thin delegation service with AddAsync override (name guard + CreatedAt stamp)
  - GroupRepository EF Core implementation with single-query member count via UserGroups.Count projection
  - DI registrations in both ServiceExtensions files

affects:
  - Phase 29-04 (Platform MVC Area GroupController depends on IGroupService)
  - Phase 29-05 (integration tests for group management operations)

tech-stack:
  added: []
  patterns:
    - GroupRepository extends BaseRepository<Group, GroupEntity>; thin delegation pattern for GroupService matching CharacterService
    - UserGroups.Count projection in single EF Core LINQ query (no N+1)
    - AnyAsync existence check before AddMemberAsync insert (application-level duplicate guard; DB unique index is final guard)
    - Silent no-op in RemoveMemberAsync when membership row is absent (idempotent delete)
    - DbUpdateException from unique constraint violations bubbles to controller (GroupService.AddAsync does not catch it)

key-files:
  created:
    - QuestBoard.Domain/Interfaces/IGroupService.cs
    - QuestBoard.Domain/Interfaces/IGroupRepository.cs
    - QuestBoard.Domain/Models/GroupWithMemberCount.cs
    - QuestBoard.Domain/Services/GroupService.cs
    - QuestBoard.Repository/GroupRepository.cs
  modified:
    - QuestBoard.Repository/Extensions/ServiceExtensions.cs
    - QuestBoard.Domain/Extensions/ServiceExtensions.cs

key-decisions:
  - "IGroupRepository interface lives in QuestBoard.Domain/Interfaces/ (not in QuestBoard.Repository) — consistent with IUserRepository pattern; Domain must not reference Repository"
  - "GroupWithMemberCount is a plain DTO (not AutoMapper-mapped) — it is a LINQ projection, not a full entity round-trip; no EntityProfile mapping needed"
  - "GroupService.AddAsync overrides base to enforce non-blank name and stamp CreatedAt; DbUpdateException for unique name violation bubbles to controller"
  - "AddMemberAsync uses AnyAsync existence check before insert — belt-and-suspenders with the DB unique composite index on (UserId, GroupId)"
  - "RemoveMemberAsync is idempotent — returns silently when membership row is not found; no exception thrown"

patterns-established:
  - "Pattern: GroupRepository extends BaseRepository<Group, GroupEntity> with additional DbContext.UserGroups queries (same constructor as UserRepository)"
  - "Pattern: GroupService thin delegation with optional override of base methods for domain guards"

requirements-completed: [MGMT-02, MGMT-03, MGMT-04, MGMT-05, MGMT-06]

duration: 7min
completed: 2026-06-30
---

# Phase 29 Plan 03: Group Service Layer Summary

**IGroupService/IGroupRepository interfaces, GroupWithMemberCount DTO, GroupService thin delegation service, and GroupRepository EF Core implementation — the data backbone for the /platform area**

## Performance

- **Duration:** 7 min
- **Started:** 2026-06-30T13:30:00Z
- **Completed:** 2026-06-30T13:37:00Z
- **Tasks:** 2
- **Files modified:** 7

## Accomplishments

- IGroupRepository and IGroupService interfaces defined in Domain layer with five MGMT-02–06 operations
- GroupRepository implements all interface methods using QuestBoardContext.Groups and QuestBoardContext.UserGroups DbSets; member count delivered via single-query UserGroups.Count projection (no N+1)
- GroupService thin delegation service overrides AddAsync to enforce non-blank name and auto-stamp CreatedAt; all other methods are one-line delegates to repository
- Both services registered in DI via their respective ServiceExtensions files
- 197/197 tests pass after both tasks

## Task Commits

1. **Task 1: IGroupService, IGroupRepository interfaces, GroupWithMemberCount DTO** - `5fe61a0` (feat)
2. **Task 2: GroupService, GroupRepository, DI registrations** - `43ea5b8` (feat)

## Files Created/Modified

- `QuestBoard.Domain/Interfaces/IGroupService.cs` - Service interface extending IBaseService<Group> with five MGMT methods
- `QuestBoard.Domain/Interfaces/IGroupRepository.cs` - Repository interface extending IBaseRepository<Group> with same five methods
- `QuestBoard.Domain/Models/GroupWithMemberCount.cs` - Flat DTO for LINQ projection with MemberCount property
- `QuestBoard.Domain/Services/GroupService.cs` - Thin delegation service; AddAsync override guards name and stamps CreatedAt
- `QuestBoard.Repository/GroupRepository.cs` - EF Core implementation; GetAllWithMemberCountAsync uses UserGroups.Count; AddMemberAsync guards against duplicate with AnyAsync; RemoveMemberAsync is idempotent
- `QuestBoard.Repository/Extensions/ServiceExtensions.cs` - Added IGroupRepository → GroupRepository registration
- `QuestBoard.Domain/Extensions/ServiceExtensions.cs` - Added IGroupService → GroupService registration

## Decisions Made

- IGroupRepository lives in Domain/Interfaces (not Repository) — same pattern as IUserRepository; prevents Repository → Domain dependency inversion
- GroupWithMemberCount is a plain DTO, not AutoMapper-mapped — it is projected directly in LINQ, not a round-trip from a stored entity
- DbUpdateException for unique name constraint violation is not caught in GroupService.AddAsync — it bubbles to the controller (plan 29-04) which will catch it and show a user-friendly message

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

None. Build and tests passed on first attempt.

## Known Stubs

None. All methods are fully implemented with real EF Core queries.

## Threat Surface Scan

No new network endpoints, auth paths, or schema changes. GroupRepository operates on existing Groups and UserGroups DbSets with no new tables or migrations. All threat mitigations from the plan's threat model are applied:
- T-29-GS-01: AnyAsync existence check before AddMemberAsync insert — implemented
- T-29-GS-02: GroupController (plan 29-04) bears the SuperAdminOnly authorization; GroupService trusts the controller layer — acknowledged
- T-29-GS-03: DbUpdateException for unique name constraint bubbles to controller — implemented
- T-29-GS-04: Single LINQ query with COUNT subquery for member counts — implemented (no N+1)

## Next Phase Readiness

- IGroupService is ready for injection into GroupController (plan 29-04)
- GetAllWithMemberCountAsync, AddMemberAsync, RemoveMemberAsync, HasMembersAsync all implemented and registered
- No blockers for plan 29-04

---
*Phase: 29-superadmin-role-and-management-area*
*Completed: 2026-06-30*
