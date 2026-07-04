---
phase: 40-platform-members-page-redesign
plan: 01
subsystem: platform-group-management
tags: [ef-core, viewmodel, backend-seam]
dependency-graph:
  requires: []
  provides:
    - "IUserRepository.GetAvailableUsers(groupId, search, token)"
    - "IUserService.GetAvailableUsersAsync(groupId, search, token)"
    - "CreateMemberViewModel"
    - "GroupMembersViewModel.SearchQuery / GroupMembersViewModel.CreateMember"
  affects:
    - "GroupController (Plan 02 wires these into Members/CreateMember actions)"
    - "Members.cshtml / Members.Mobile.cshtml (Plan 03 consumes ViewModel shape)"
tech-stack:
  added: []
  patterns:
    - "Negated manual-join EF query mirroring GetAllGroupMembers (not-in-group via !DbContext.UserGroups.Any(...))"
    - "Service-layer one-line delegate pattern (GetXAsync -> repository.GetX)"
key-files:
  created:
    - QuestBoard.Service/ViewModels/PlatformViewModels/CreateMemberViewModel.cs
  modified:
    - QuestBoard.Domain/Interfaces/IUserRepository.cs
    - QuestBoard.Repository/UserRepository.cs
    - QuestBoard.Domain/Interfaces/IUserService.cs
    - QuestBoard.Domain/Services/UserService.cs
    - QuestBoard.Service/ViewModels/PlatformViewModels/GroupMembersViewModel.cs
    - QuestBoard.UnitTests/Services/UserServiceTests.cs
    - QuestBoard.IntegrationTests/Controllers/GroupManagementIntegrationTests.cs
decisions:
  - "groupId taken as a plain int parameter (never IActiveGroupContext) to prevent the session/route confusion bug class seen in Phase 30 and 34.3"
  - "Search predicate uses plain EF Core .Contains() with no explicit case-insensitivity handling — SQL Server's default collation is case-insensitive"
metrics:
  duration: 25min
  completed: 2026-07-04
status: complete
---

# Phase 40 Plan 01: Available-Users Query & Members ViewModel Reshape Summary

Added a DB-side "users not in this group, matching search" query (`GetAvailableUsers`/`GetAvailableUsersAsync`) at repository, service, and interface level, plus a new `CreateMemberViewModel` and reshaped `GroupMembersViewModel` for the upcoming Platform Members page redesign.

## What Was Built

**Task 1 — GetAvailableUsers query (repository + service + interfaces) with unit + integration coverage:**
- `IUserRepository.GetAvailableUsers(int groupId, string? search, CancellationToken)` — new interface method
- `UserRepository.GetAvailableUsers(...)` — negates the `GetAllGroupMembers` not-in-group predicate (`!DbContext.UserGroups.Any(ug => ug.UserId == u.Id && ug.GroupId == groupId)`), then conditionally applies `.Where(u => u.Name.Contains(search) || (u.Email != null && u.Email.Contains(search)))` only when `search` is non-blank
- `IUserService.GetAvailableUsersAsync(...)` and `UserService.GetAvailableUsersAsync(...)` — one-line delegate to the repository
- Two new NSubstitute unit tests in `UserServiceTests.cs`: delegation + passthrough (asserts `.Received(1)` with same groupId/search values, returns same list reference), and null-search passthrough (service never substitutes empty string)
- Four new integration tests in `GroupManagementIntegrationTests.cs` against group 1 (EuphoriaInn): not-in-group exclusion/inclusion, search-by-name, search-by-email, null/empty search returns unfiltered

**Task 2 — CreateMemberViewModel and GroupMembersViewModel reshape:**
- New `CreateMemberViewModel` (`QuestBoard.Service/ViewModels/PlatformViewModels/CreateMemberViewModel.cs`) mirroring `CreateUserViewModel` field-for-field: `Email` (`[Required][EmailAddress]`), `Name` (`[Required][StringLength(100)]`), `GroupRole` (defaults to `GroupRole.Player`)
- `GroupMembersViewModel` gained `SearchQuery` (string?) and `CreateMember` (CreateMemberViewModel, defaulted to `new()`); existing `AddMember`, `Group`, `Members`, `AvailableUsers` properties untouched for Plan 02/03 to consume

## Verification

- `dotnet build QuestBoard.slnx` — Build succeeded, 0 errors
- `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~UserServiceTests|FullyQualifiedName~UserRepositoryTests"` — 13/13 passed (2 new `GetAvailableUsersAsync` delegation tests included)
- `dotnet test QuestBoard.IntegrationTests --filter FullyQualifiedName~GroupManagementIntegrationTests` — 18/18 passed (4 new available-users tests included)
- Confirmed `GetAvailableUsers` method body in `UserRepository.cs` contains no reference to `activeGroupContext` (the 3 pre-existing occurrences in the file belong to `GetAllDungeonMasters`/`GetAllPlayers`, untouched by this plan)

## Deviations from Plan

None — plan executed exactly as written. Note: the plan file used `.sln` in verification commands; the actual solution file in this repo is `QuestBoard.slnx` (pre-existing project convention, not a deviation requiring a fix — same command semantics, correct extension used during execution).

## Requirements Addressed

- MEMBERS-02 (query layer): satisfied — group-scoped, search-filtered "not in this group" query exists at repository/service level with both unit (service-seam delegation) and integration (real EF filtering) coverage.
- MEMBERS-03 (form model): satisfied — `CreateMemberViewModel` exists and is nested on `GroupMembersViewModel` as `CreateMember`.

## Self-Check: PASSED

- FOUND: QuestBoard.Service/ViewModels/PlatformViewModels/CreateMemberViewModel.cs
- FOUND: QuestBoard.Domain/Interfaces/IUserRepository.cs (contains GetAvailableUsers)
- FOUND: QuestBoard.Repository/UserRepository.cs (contains GetAvailableUsers, no activeGroupContext in that method)
- FOUND: QuestBoard.Domain/Interfaces/IUserService.cs (contains GetAvailableUsersAsync)
- FOUND: QuestBoard.Domain/Services/UserService.cs (contains repository.GetAvailableUsers delegate)
- FOUND: QuestBoard.UnitTests/Services/UserServiceTests.cs (GetAvailableUsersAsync tests present, passing)
- FOUND: QuestBoard.IntegrationTests/Controllers/GroupManagementIntegrationTests.cs (4 new available-users tests present, passing)
- FOUND commit 2cadb46: feat(40-01): add GetAvailableUsers query with unit and integration coverage
- FOUND commit 6ee5fd0: feat(40-01): add CreateMemberViewModel and reshape GroupMembersViewModel
