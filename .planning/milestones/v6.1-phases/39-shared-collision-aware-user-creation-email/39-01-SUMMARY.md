---
phase: 39-shared-collision-aware-user-creation-email
plan: 01
subsystem: auth
tags: [ef-core, identity, nsubstitute, xunit, domain-service, group-membership]

# Dependency graph
requires: []
provides:
  - "IUserService.CreateOrAddToGroupAsync — shared collision-aware creation/add-to-group method"
  - "CreateOrAddToGroupResult / CreateOrAddToGroupOutcome — result type with four resolvable outcomes"
  - "UserService now composes IGroupService (new constructor parameter)"
affects: [39-02, 39-03, 40]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Throw-on-collision membership add (IGroupService.AddMemberAsync) instead of upsert (SetGroupRoleAsync) for detecting already-member state"
    - "Existing-account collision loads real Name/Email/EmailConfirmed via GetByIdAsync rather than trusting caller-submitted values"

key-files:
  created:
    - QuestBoard.Domain/Models/CreateOrAddToGroupResult.cs
  modified:
    - QuestBoard.Domain/Interfaces/IUserService.cs
    - QuestBoard.Domain/Services/UserService.cs
    - QuestBoard.UnitTests/Services/UserServiceTests.cs

key-decisions:
  - "UserService takes IGroupService as a new primary-constructor parameter, composing the sibling Domain interface rather than a raw repository — mirrors existing IIdentityService composition and keeps the Service→Domain→Repository dependency direction intact"
  - "AddMemberAsync (throw-on-collision) used instead of SetGroupRoleAsync (upsert) specifically so the already-member outcome is detectable — an intentional divergence from AdminController.CreateUser's current inline approach"

patterns-established:
  - "CreateOrAddToGroupResult is the shared contract Plan 02/03 (email/controller wiring) and Phase 40 (platform entry point) build on"

requirements-completed: [CREATE-01, CREATE-03]

# Metrics
duration: 12min
completed: 2026-07-03
status: complete
---

# Phase 39 Plan 1: Shared Collision-Aware User Creation Summary

**Domain-layer `IUserService.CreateOrAddToGroupAsync` resolving four collision outcomes (new account, added-to-group, added-to-group-stranded-account, already-member) via throw-on-collision membership add, fully unit-tested with NSubstitute.**

## Performance

- **Duration:** 12 min
- **Started:** 2026-07-03T22:53:00Z
- **Completed:** 2026-07-03T23:05:20Z
- **Tasks:** 3
- **Files modified:** 4 (1 created, 3 modified)

## Accomplishments
- New `CreateOrAddToGroupResult`/`CreateOrAddToGroupOutcome` model carrying the resolved outcome, userId, email, name, and error descriptions for the failed-create path
- `IUserService.CreateOrAddToGroupAsync` implemented in `UserService`, composing the new `IGroupService` dependency to detect already-member collisions via `AddMemberAsync`'s throw-on-collision contract
- Full unit coverage of all four outcomes plus a regression assertion that no collision branch ever creates a new account or mutates the existing account's name

## Task Commits

Each task was committed atomically:

1. **Task 1: Add CreateOrAddToGroupResult model and outcome enum** - `e521bbe` (feat)
2. **Task 2: Add CreateOrAddToGroupAsync to IUserService and UserService** - `0fc1090` (feat)
3. **Task 3: Unit-test all four collision outcomes** - `b027358` (test)

**Plan metadata:** pending (docs: complete plan)

## Files Created/Modified
- `QuestBoard.Domain/Models/CreateOrAddToGroupResult.cs` - New result record + 5-member outcome enum
- `QuestBoard.Domain/Interfaces/IUserService.cs` - New `CreateOrAddToGroupAsync` interface member with XML-doc
- `QuestBoard.Domain/Services/UserService.cs` - New `IGroupService` constructor dependency + `CreateOrAddToGroupAsync` implementation
- `QuestBoard.UnitTests/Services/UserServiceTests.cs` - `IGroupService` substitute added to fixture; 5 new tests covering all outcomes

## Decisions Made
- Constructor composition: `UserService` now depends on `IGroupService` directly (sibling Domain interface), not a raw repository — consistent with how it already composes `IIdentityService`. No DI wiring change needed since `IGroupService` was already registered via `AddScoped`.
- Used `groupService.AddMemberAsync` (throw-on-collision) rather than `SetGroupRoleAsync` (upsert) on the existing-user branch, per the plan's explicit instruction — this is the mechanism that makes `AlreadyMember` detectable at all.
- Existing-user branches always reload `Name`/`Email`/`EmailConfirmed` via `GetByIdAsync` rather than trusting the submitted form values, satisfying the "never mutate existing account's Name" requirement (D-07 in phase context, not referenced in code).

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

Initial test-file compile errors were resolved during Task 3 itself (not deviations from the shipped plan, just implementation fixes while writing the tests):
- Missing `using Microsoft.AspNetCore.Identity;` for `IdentityResult.Success` in the new-account test — added.
- NSubstitute's `.Throws()`/`.ThrowsAsync()` extension methods require `using NSubstitute.ExceptionExtensions;`, not exposed by the base `NSubstitute` namespace — added the missing using directive so the already-member test could stub `IGroupService.AddMemberAsync` to throw `InvalidOperationException`.

Both were resolved before the task's verification step ran; final `dotnet test` run was clean (10/10 passing, 0 failures).

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- `IUserService.CreateOrAddToGroupAsync` is ready for Plan 02 (email job/template) and Plan 03 (AdminController.CreateUser refactor + flash messages) to consume.
- The Domain method deliberately does not build callback URLs or enqueue email jobs — those remain MVC-only concerns for Plan 03's controller wiring, as scoped by this plan's objective.
- No blockers. Full solution (`QuestBoard.slnx`) builds clean; targeted `UserServiceTests` filter passes 10/10.

---
*Phase: 39-shared-collision-aware-user-creation-email*
*Completed: 2026-07-03*

## Self-Check: PASSED

All created/modified files verified present on disk; all four task/summary commit hashes (e521bbe, 0fc1090, b027358, 48fd200) verified present in git log.
