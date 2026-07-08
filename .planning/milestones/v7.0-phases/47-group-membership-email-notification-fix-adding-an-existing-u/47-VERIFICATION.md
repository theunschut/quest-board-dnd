---
phase: 47-group-membership-email-notification-fix-adding-an-existing-u
verified: 2026-07-05T00:00:00Z
status: passed
score: 5/5 must-haves verified
behavior_unverified: 0
overrides_applied: 0
---

# Phase 47: Group Membership Email Notification Fix Verification Report

**Phase Goal:** Fix `GroupController.AddMember` (Platform area, SuperAdmin-only) so adding an existing user to a group sends the same email notification as `CreateMember` and `AdminController.CreateUser` already do — by rerouting through the existing shared `UserService.CreateOrAddToGroupAsync` method instead of calling `groupService.AddMemberAsync` directly (per 47-CONTEXT.md decisions D-01 through D-06).
**Verified:** 2026-07-05
**Status:** passed
**Re-verification:** No — initial verification

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | Adding an existing confirmed user to a group via the Members-page available-users panel (`AddMember` POST) enqueues `GroupMembershipAddedEmailJob`. | VERIFIED | `GroupController.cs:167-178` (`AddedToGroup` arm calls `jobClient.Enqueue<GroupMembershipAddedEmailJob>`). Behaviorally proven: `AddMember_ExistingConfirmedUser_ShouldEnqueueGroupMembershipAddedEmailJob` (line 812) run directly — PASSED. |
| 2 | Adding an existing stranded/unconfirmed user to a group via `AddMember` enqueues `WelcomeEmailJob` with a `SetPassword` callback URL. | VERIFIED | `GroupController.cs:180-199` (`AddedToGroupStrandedAccount` arm builds `SetPassword` callback via `Url.Action` and calls `jobClient.Enqueue<WelcomeEmailJob>`). Behaviorally proven: `AddMember_ExistingStrandedUser_ShouldEnqueueWelcomeEmailJob` (line 848) run directly — PASSED. |
| 3 | Adding a user who is already a member via `AddMember` produces a warning toast and enqueues no email. | VERIFIED | `GroupController.cs:201-203` (`AlreadyMember` arm sets `TempData["Warning"]`, no job enqueue). Behaviorally proven: `AddMember_AlreadyMember_ShouldEnqueueNoEmail` (line 888) run directly — PASSED. |
| 4 | The `AddMember` success toast tells the user a notification email has been sent. | VERIFIED | `GroupController.cs:176,197`: `"{result.Name} has been added to the group as {model.Role}. A notification email has been sent."` — identical wording to `CreateMember`'s equivalent arms (lines 265, 285). |
| 5 | `AddMember` routes through the same `UserService.CreateOrAddToGroupAsync` shared method that `CreateMember` and `AdminController.CreateUser` already use. | VERIFIED | `GroupController.cs:163`: `userService.CreateOrAddToGroupAsync(user.Email!, user.Name, id, model.Role)`. Direct `groupService.AddMemberAsync(` call and `catch (InvalidOperationException)` block confirmed removed from `AddMember`'s body (grep scoped to the method returned no matches). |

**Score:** 5/5 truths verified (0 present, behavior-unverified)

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `QuestBoard.Service/Areas/Platform/Controllers/GroupController.cs` | `AddMember` rerouted through `CreateOrAddToGroupAsync` with outcome switch and email enqueues | VERIFIED | Lines 145-212 fully rewritten per plan: group load, user resolution, `CreateOrAddToGroupAsync` call, 4-arm switch (`AddedToGroup`, `AddedToGroupStrandedAccount`, `AlreadyMember`, `Failed`/`default`). Old `try/catch (InvalidOperationException)` and direct `groupService.AddMemberAsync` call confirmed absent. |
| `QuestBoard.IntegrationTests/WebApplicationFactoryBase.cs` | Capturing `IBackgroundJobClient` spy | VERIFIED | `CapturingBackgroundJobClient` class (line 131) with `ConcurrentBag<Job> EnqueuedJobs`, `Clear()` method, and `public CapturingBackgroundJobClient JobClient` factory property (line 17) registered via `services.AddSingleton<IBackgroundJobClient>(JobClient)` (line 71), replacing the prior `NoOpBackgroundJobClient` registration. `NoOpBackgroundJobClient` preserved in-file (line 121), unused. |
| `QuestBoard.IntegrationTests/Controllers/GroupManagementIntegrationTests.cs` | Regression tests asserting `AddMember` enqueues the correct email job per outcome | VERIFIED | Three new `[Fact]` tests present (lines 812, 848, 888) asserting `GroupMembershipAddedEmailJob`/`WelcomeEmailJob` counts per outcome via `_factory.JobClient.EnqueuedJobs`. |

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|----|--------|---------|
| `GroupController.cs` (`AddMember`) | `UserService.CreateOrAddToGroupAsync` | `userService.CreateOrAddToGroupAsync(user.Email!, user.Name, id, model.Role)` | WIRED | Line 163, only call site within `AddMember`. |
| `GroupController.cs` (`AddMember`) | `GroupMembershipAddedEmailJob` | `jobClient.Enqueue<GroupMembershipAddedEmailJob>` in the `AddedToGroup` arm | WIRED | Line 174, inside `AddMember`'s switch scope (verified via scoped grep of lines 145-212). |
| `GroupController.cs` (`AddMember`) | `WelcomeEmailJob` | `jobClient.Enqueue<WelcomeEmailJob>` in the `AddedToGroupStrandedAccount` arm | WIRED | Line 193, inside `AddMember`'s switch scope. |

### Behavioral Spot-Checks / Test Execution

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| Build succeeds with zero errors | `dotnet build QuestBoard.slnx -c Debug --nologo -v q` | Build succeeded, 0 Warning(s), 0 Error(s) | PASS |
| `AddMember`-scoped tests (3 pre-existing + 3 new) pass | `dotnet test --filter "FullyQualifiedName~GroupManagementIntegrationTests.AddMember"` | Passed: 6, Failed: 0, Total: 6 | PASS |
| Full `GroupManagementIntegrationTests` suite is green | `dotnet test --filter "FullyQualifiedName~GroupManagementIntegrationTests"` | Passed: 31, Failed: 0, Total: 31 | PASS (matches SUMMARY's claimed 31/31) |
| Commits referenced in SUMMARY exist | `git log` / `git show --stat` for `5032d04`, `3e4346a`, `bd46603` | All three commits found with matching content | PASS |

### Requirements Coverage

No REQ-IDs declared in PLAN frontmatter (`requirements: []`), no phase-47 mapping exists in `.planning/REQUIREMENTS.md` (grep returned no matches). This matches the phase's declared ad-hoc status (same precedent as Phase 48) — not treated as a gap or orphaned requirement.

### Anti-Patterns Found

None. Scoped grep of `AddMember`'s method body (lines 145-212) for `groupService.AddMemberAsync(`, `catch (InvalidOperationException)`, `model.GroupRole` (wrong field name), `TBD`, `FIXME`, `XXX`, `TODO`, `HACK`, `PLACEHOLDER`, and `D-0`/`Phase 47` planning references — zero matches. The file does contain one pre-existing `(D-04)` comment at line 266 of `GroupManagementIntegrationTests.cs`, but `git blame` confirms it predates this phase (commit `6beedd34`, phase 40/44 era) and decorates a different, untouched test method (`AddMember_WithSearch_ShouldPreserveSearchOnRedirect`) — correctly left alone per the phase's file-scope boundary and CLAUDE.md's "don't opportunistically edit out-of-scope code" convention. Not a blocker introduced by this phase.

### Human Verification Required

None. All truths are directly observable via source inspection and executed integration tests (no visual, real-time, or external-service-dependent behavior in scope).

### Gaps Summary

No gaps. All 5 must-have truths verified against actual code and passing tests (not just SUMMARY claims). All 3 required artifacts present, substantive, and wired. All 3 key links confirmed wired within `AddMember`'s method scope. Build is clean (0 errors/warnings). The full `GroupManagementIntegrationTests` suite (31/31) and the `AddMember`-scoped subset (6/6) were both re-run independently during verification and passed, corroborating the SUMMARY's test-run claims rather than merely trusting them. No REQ-ID gaps (none declared, none expected). No debt markers or planning-ID leakage introduced by this phase.

---

_Verified: 2026-07-05_
_Verifier: Claude (gsd-verifier)_
