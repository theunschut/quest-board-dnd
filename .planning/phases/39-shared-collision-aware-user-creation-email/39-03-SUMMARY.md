---
phase: 39-shared-collision-aware-user-creation-email
plan: 03
subsystem: auth
tags: [aspnet-identity, hangfire, email, admin-controller, collision-handling]

# Dependency graph
requires:
  - phase: 39-01
    provides: "UserService.CreateOrAddToGroupAsync + CreateOrAddToGroupResult/CreateOrAddToGroupOutcome shared collision-aware creation method"
  - phase: 39-02
    provides: "GroupMembershipAddedEmailJob, AddedToGroup.razor email component, RedirectWithWarning controller extension"
provides:
  - "AdminController.CreateUser (POST) fully driven by the shared CreateOrAddToGroupAsync method — no more inline CreateAsync + SetGroupRoleAsync"
  - "All four observable outcomes wired end-to-end: new account, silent collision-add, stranded-account resend, already-member warning"
  - "The exact call shape (groupId as a plain parameter, not session-derived) that Phase 40's platform entry point will reuse"
affects: [40-platform-members-page-redesign]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Controller action branches on a Domain-layer outcome enum (CreateOrAddToGroupOutcome) rather than catching exceptions or checking booleans, keeping all five paths explicit and exhaustive (switch with default)"
    - "Stranded-account resend mirrors SendConfirmationEmail's fresh-token generation exactly, reusing WelcomeEmailJob rather than adding a third email template"

key-files:
  created: []
  modified:
    - QuestBoard.Service/Controllers/Admin/AdminController.cs

key-decisions:
  - "IGroupService injected directly into AdminController's primary constructor (already registered in DI) solely to resolve the group Name for the AddedToGroup email body"
  - "AddedToGroupStrandedAccount branch returns the identical success flash string as AddedToGroup so the admin sees no distinction between which email template actually fired"

patterns-established:
  - "Silent privilege-grant paths (collision auto-add) are threat-modeled explicitly (STRIDE) and gated behind a blocking human-verify checkpoint rather than shipped on green tests alone"

requirements-completed: [CREATE-01, CREATE-02, CREATE-03, CREATE-04]

# Metrics
duration: 8min
completed: 2026-07-04
status: complete
---

# Phase 39 Plan 03: Wire CreateUser POST onto the shared collision-aware method Summary

**AdminController.CreateUser now branches on CreateOrAddToGroupAsync's five-outcome result to drive new-account creation, silent collision auto-add with a token-free notification email, stranded-account SetPassword resend, and an already-member warning flash — closing CREATE-01 through CREATE-04.**

## Performance

- **Duration:** 8 min (Task 1 + Task 2 automated work; Task 3 was a human-verify pause, elapsed wall-clock excluded from this figure per GSD convention)
- **Started:** 2026-07-04T01:13:01+02:00 (prior plan's metadata commit)
- **Completed:** 2026-07-04T01:15:57+02:00 (Task 1/2 commit)
- **Tasks:** 3 (2 automated + 1 human-verify checkpoint)
- **Files modified:** 1

## Accomplishments
- `CreateUser` POST fully driven by the shared `CreateOrAddToGroupAsync` method — the exact call shape (`groupId` as a plain parameter) Phase 40's platform entry point will reuse
- All three collision outcomes fire the exact D-09 flash copy and correct email (new account welcome, token-free AddedToGroup notification, already-member warning)
- Stranded-account path (D-01) resends Welcome/SetPassword with a fresh token instead of AddedToGroup, using the identical admin-facing flash as the normal add (D-03)
- Silent privilege-grant path passed human security verification: no Identity-role grant, no silent password mutation, no cross-group add

## Task Commits

Each task was committed atomically:

1. **Task 1: Refactor CreateUser POST onto CreateOrAddToGroupAsync with three-outcome flash and email dispatch** - `86c580d` (feat)
2. **Task 2: Full-solution build and regression test** - verification only, no file changes (128/128 unit tests, 265/265 integration tests passed against the Task 1 commit)
3. **Task 3: Human-verify the four collision outcomes and security-sensitive paths** - checkpoint approved by user, no commit (pure verification gate)

**Plan metadata:** (this commit)

## Files Created/Modified
- `QuestBoard.Service/Controllers/Admin/AdminController.cs` - `CreateUser` POST replaced inline `CreateAsync`/`SetGroupRoleAsync` with `userService.CreateOrAddToGroupAsync(...)`; added a `switch` over `CreateOrAddToGroupOutcome` covering `NewAccountCreated`, `AddedToGroup`, `AddedToGroupStrandedAccount`, `AlreadyMember`, and `Failed`; injected `IGroupService groupService` into the primary constructor to resolve the group name for the `GroupMembershipAddedEmailJob` payload

## Decisions Made
- `IGroupService` was injected directly into `AdminController`'s existing primary constructor (already registered in DI) rather than resolving the group name via a new abstraction — matches the plan's explicit instruction and avoids adding a seam only this one call site needs
- The `AddedToGroupStrandedAccount` branch mirrors `SendConfirmationEmail`'s fresh-token generation and null-callbackUrl guard verbatim, and deliberately returns the same success flash text as `AddedToGroup` so the admin-facing UX is outcome-agnostic even though two different email templates fire under the hood

## Deviations from Plan

None - plan executed exactly as written. Task 1's implementation matched every acceptance criterion in the plan (five-outcome switch, `IGroupService` injection, verbatim D-09 flash strings, no GSD ID references in source, `dotnet build` exit 0). Task 2's full-solution build and both test suites were already green from the prior session's verification and required no fixes.

## Issues Encountered

None. Two commits landed on top of this plan's work between Task 2 completing and Task 3's approval (`2ef606f` — an EmailPreviewController addition, and `6b608f3` — a local-dev `EmailSettings.SuppressSending` flag added so the user could exercise the full email-dispatch flow without live SMTP sends during manual verification). Neither commit touches `AdminController.cs` or any file in this plan's `files_modified` scope; verified via `git show --stat 86c580d` that the Task 1 commit remains intact and unaffected, and via `git status --short` that the working tree is clean.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

Phase 39 is now fully complete (all 3 plans done). `CreateOrAddToGroupAsync` is proven end-to-end in `AdminController.CreateUser` with the exact `groupId`-as-plain-parameter call shape Phase 40's new platform create-user entry point needs to reuse — Phase 40 must source `groupId` strictly from the route (never from `IActiveGroupContext`, per the STATE.md risk flag) when it wires its own call to this same shared method. No blockers.

---
*Phase: 39-shared-collision-aware-user-creation-email*
*Completed: 2026-07-04*
