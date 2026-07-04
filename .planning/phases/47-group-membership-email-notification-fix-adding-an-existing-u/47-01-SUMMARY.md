---
phase: 47-group-membership-email-notification-fix-adding-an-existing-u
plan: 01
subsystem: email
tags: [aspnet-core-mvc, hangfire, integration-tests, group-membership, email-notifications]

# Dependency graph
requires:
  - phase: 39-existing-email-collision-handling
    provides: "UserService.CreateOrAddToGroupAsync shared method with CreateOrAddToGroupOutcome enum (NewAccountCreated/AddedToGroup/AddedToGroupStrandedAccount/AlreadyMember/Failed)"
  - phase: 40-platform-group-members-page-redesign
    provides: "AddMember action and AddMemberViewModel on the Members-page available-users panel"
provides:
  - "AddMember (Platform GroupController) rerouted through CreateOrAddToGroupAsync as its third caller"
  - "GroupMembershipAddedEmailJob enqueue for confirmed users added to a group via AddMember"
  - "WelcomeEmailJob enqueue (with SetPassword callback) for stranded/unconfirmed users added via AddMember"
  - "CapturingBackgroundJobClient test spy replacing NoOpBackgroundJobClient in the integration test factory"
affects: [group-management, email-notifications, integration-tests]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Capturing test double for IBackgroundJobClient (ConcurrentBag<Job>) instead of a discarding no-op stub, enabling enqueue-assertion tests"

key-files:
  created: []
  modified:
    - QuestBoard.Service/Areas/Platform/Controllers/GroupController.cs
    - QuestBoard.IntegrationTests/WebApplicationFactoryBase.cs
    - QuestBoard.IntegrationTests/Controllers/GroupManagementIntegrationTests.cs

key-decisions:
  - "AddMember reroutes through CreateOrAddToGroupAsync (email/name resolved via GetByIdAsync(model.UserId) first) rather than duplicating CreateMember's email-dispatch branching inline"
  - "Failed/default outcome arm deviates from CreateMember's View()-redisplay pattern: AddMember has no dedicated form partial, so it uses TempData[\"Error\"] + redirect instead"
  - "CapturingBackgroundJobClient preserves NoOpBackgroundJobClient in the same file (other test classes may still reference it) and only swaps the DI registration"

patterns-established:
  - "Any future 'add user to group' entry point should call CreateOrAddToGroupAsync rather than groupService.AddMemberAsync directly, to guarantee consistent email dispatch"

requirements-completed: []

# Metrics
duration: 6min
completed: 2026-07-04
status: complete
---

# Phase 47 Plan 01: Group Membership Email Notification Fix Summary

**AddMember now routes through the shared CreateOrAddToGroupAsync method, enqueuing GroupMembershipAddedEmailJob or WelcomeEmailJob per outcome, closing the silent no-email gap in the Platform Members page's "add existing user" flow.**

## Performance

- **Duration:** 6 min
- **Started:** 2026-07-04T22:15:47Z
- **Completed:** 2026-07-04T22:22:12Z
- **Tasks:** 2 completed
- **Files modified:** 3

## Accomplishments
- `GroupController.AddMember` (Platform area, SuperAdmin-only) now enqueues `GroupMembershipAddedEmailJob` when an existing confirmed user is added to a group, and `WelcomeEmailJob` (with a `SetPassword` callback) when the added user's account was never confirmed (stranded)
- `AddMember` is now the third caller of `UserService.CreateOrAddToGroupAsync`, alongside `CreateMember` and `AdminController.CreateUser` — no inline duplicate of the email-dispatch branching logic
- Added a `CapturingBackgroundJobClient` test spy that records enqueued Hangfire `Job` objects, replacing the discarding `NoOpBackgroundJobClient` in the integration test DI registration
- Three new regression tests lock in the per-outcome enqueue behavior for `AddMember`; all 31 `GroupManagementIntegrationTests` (28 pre-existing + 3 new) pass

## Task Commits

Each task was committed atomically:

1. **Task 1: Reroute AddMember through CreateOrAddToGroupAsync with email-dispatch outcome switch** - `5032d04` (feat)
2. **Task 2: Add a capturing job-client spy and regression tests asserting AddMember email enqueues** - `3e4346a` (test)

**Plan metadata:** committed separately by the orchestrator after wave completion (worktree mode — see `<parallel_execution>` note).

## Files Created/Modified
- `QuestBoard.Service/Areas/Platform/Controllers/GroupController.cs` - `AddMember` action rewritten to load the group, resolve the picked user, call `CreateOrAddToGroupAsync`, and switch on the outcome enum to enqueue the correct email job and set the correct toast message
- `QuestBoard.IntegrationTests/WebApplicationFactoryBase.cs` - Added `CapturingBackgroundJobClient : IBackgroundJobClient`, exposed as `WebApplicationFactoryBase.JobClient`; DI registration for `IBackgroundJobClient` now points to it instead of `NoOpBackgroundJobClient` (which remains in the file, unused by the current registration)
- `QuestBoard.IntegrationTests/Controllers/GroupManagementIntegrationTests.cs` - Added `AddMember_ExistingConfirmedUser_ShouldEnqueueGroupMembershipAddedEmailJob`, `AddMember_ExistingStrandedUser_ShouldEnqueueWelcomeEmailJob`, `AddMember_AlreadyMember_ShouldEnqueueNoEmail`

## Decisions Made
- Followed the plan's decisions (D-01 through D-06) verbatim: reroute through the shared service method, switch on the full outcome enum with `NewAccountCreated` folded into `default` (unreachable for `AddMember`), replace the `try/catch (InvalidOperationException)` with the `AlreadyMember` arm, and match `CreateMember`'s toast copy per outcome.
- The `Failed`/`default` arm deliberately diverges from `CreateMember`'s `View()`-redisplay pattern (no dedicated form partial exists for `AddMember`), using `TempData["Error"]` + redirect instead — this was the plan's own documented deviation from the analog, not a new one introduced during execution.

## Deviations from Plan

None - plan executed exactly as written. The one deliberate deviation from the `CreateMember` analog pattern (the `Failed`/`default` arm using `TempData` + redirect instead of `View()`-redisplay) was explicitly called out and pre-approved in the plan itself (Pattern Map + task `<action>` text), not discovered during execution.

## Issues Encountered

None. Build succeeded with zero warnings/errors on first attempt for both tasks; all six `AddMember_*` tests (three pre-existing plus three new) passed on first `dotnet test` run.

**Pre-existing out-of-scope note:** `GroupManagementIntegrationTests.cs` line 266 has a pre-existing comment referencing `(D-04)` from an earlier phase (40). This predates this plan's changes and is outside this plan's file-scope (a different test method, `AddMember_WithSearch_ShouldPreserveSearchOnRedirect`), so per the scope-boundary rule it was left untouched rather than opportunistically edited.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- Core fix complete and verified: all three "add user to group" entry points (`AddMember`, `CreateMember`, `AdminController.CreateUser`) now behave identically with respect to email dispatch.
- `dotnet build QuestBoard.slnx -c Debug` succeeds with zero errors/warnings; `dotnet test --filter "FullyQualifiedName~GroupManagementIntegrationTests"` is fully green (31/31).
- No blockers for this ad-hoc bug-fix phase closing out.

---
*Phase: 47-group-membership-email-notification-fix-adding-an-existing-u*
*Completed: 2026-07-04*
