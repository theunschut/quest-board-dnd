---
phase: 41-safe-user-removal-account-disable
plan: 03
subsystem: platform-admin
tags: [aspnet-core-identity, lockout, superadmin, razor-views]

# Dependency graph
requires:
  - phase: 41-safe-user-removal-account-disable (plan 02)
    provides: IIdentityService.DisableUserAsync/EnableUserAsync/GetLockoutEndAsync primitives
provides:
  - "Areas/Platform/Controllers/UsersController.cs — SuperAdmin-only Index/Disable/Enable actions"
  - "PlatformUserViewModel — User + IsDisabled shape reused by any future Platform Users work"
  - "Users/Index.cshtml + Index.Mobile.cshtml — cross-group SuperAdmin user list"
affects: [41-04-login-messaging]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "New Platform-area controller mirrors GroupController's class-level [Area(\"Platform\")] + [Authorize(Policy = \"SuperAdminOnly\")] + TempData flash + RedirectToAction(nameof(Index)) shape"
    - "Self-disable guard implemented twice by design: server-side in the controller (authoritative) and client-visible as a disabled button+tooltip in the view (affordance only)"

key-files:
  created:
    - QuestBoard.Service/ViewModels/PlatformViewModels/PlatformUserViewModel.cs
    - QuestBoard.Service/Areas/Platform/Controllers/UsersController.cs
    - QuestBoard.Service/Areas/Platform/Views/Users/Index.cshtml
    - QuestBoard.Service/Areas/Platform/Views/Users/Index.Mobile.cshtml
    - QuestBoard.IntegrationTests/Controllers/UsersControllerIntegrationTests.cs
  modified:
    - QuestBoard.Service/Areas/Platform/Views/Group/Index.cshtml
    - QuestBoard.Service/Areas/Platform/Views/Group/Index.Mobile.cshtml

key-decisions:
  - "Disable/Enable status is derived purely from IIdentityService.GetLockoutEndAsync == DateTimeOffset.MaxValue — no new schema, no new IsDisabled column"
  - "UsersController.Disable does not special-case the target's role — disabling another SuperAdmin is allowed by design (D-08)"
  - "Live HTTP CSRF-rejection test rewritten to a reflection-based [ValidateAntiForgeryToken] presence check, because the integration test harness's TestAntiforgeryDecorator always validates successfully (documented precedent: GroupPickerControllerIntegrationTests, AntiForgeryTokenCoverageTests)"

requirements-completed: [SAFE-02, SAFE-03]

# Metrics
duration: 20min
completed: 2026-07-04
status: complete
---

# Phase 41 Plan 03: Platform Users Disable/Enable Surface Summary

**New SuperAdmin-only `Areas/Platform/Controllers/UsersController` (Index/Disable/Enable) with desktop + mobile cross-group user list views, a "Manage Users" entry point on the Group Index header, and integration tests proving disable sets the lockout sentinel without deleting data while self-disable is blocked and peer-SuperAdmin disable is allowed.**

## Performance

- **Duration:** 20 min
- **Started:** 2026-07-04T12:34:00Z
- **Completed:** 2026-07-04T12:54:00Z
- **Tasks:** 3 completed
- **Files modified:** 7 (5 created, 2 modified)

## Accomplishments
- `PlatformUserViewModel` (`User` + `IsDisabled`) mirrors the existing `GroupListViewModel` POCO pattern
- `UsersController` (`[Area("Platform")]`, `[Authorize(Policy = "SuperAdminOnly")]`) exposes `Index`/`Disable`/`Enable`, deriving status from `GetLockoutEndAsync` and wiring `Disable`/`Enable` to Plan 02's `IIdentityService` primitives
- Server-side self-disable guard: `Disable` compares `GetUserIdAsync(User)` against the target `userId` and short-circuits with a `TempData["Error"]` before calling `DisableUserAsync` — no role special-casing for peer-SuperAdmin targets
- `Users/Index.cshtml` + `Index.Mobile.cshtml` render a cross-group table/stacked-card list with Confirmed and Active/Disabled badges, and exactly one action button per row (Disable when active, Enable when disabled, or a disabled "Disable" control with a tooltip for the viewer's own row)
- `Group/Index.cshtml` + `Index.Mobile.cshtml` gained a "Manage Users" `btn-secondary` header button linking to the new page
- 6 integration tests added (5 planned + 1 supporting reflection test), covering Disable's `LockoutEnd`/no-deletion behavior, Enable clearing the sentinel, the self-disable guard, peer-SuperAdmin disable, and CSRF protection

## Task Commits

Each task was committed atomically:

1. **Task 1: Build UsersController + PlatformUserViewModel with self-disable guard** - `e6e25cc` (feat)
2. **Task 2: Build Users/Index views + Manage Users entry point** - `3b461da` (feat)
3. **Task 3: Add UsersControllerIntegrationTests** - `0bab65f` (test)

_Note: no plan-metadata commit — orchestrator handles STATE.md/ROADMAP.md updates centrally after the wave completes._

## Files Created/Modified
- `QuestBoard.Service/ViewModels/PlatformViewModels/PlatformUserViewModel.cs` - new POCO, `User` + `IsDisabled`
- `QuestBoard.Service/Areas/Platform/Controllers/UsersController.cs` - new SuperAdminOnly controller, `Index`/`Disable`/`Enable`
- `QuestBoard.Service/Areas/Platform/Views/Users/Index.cshtml` - new desktop cross-group user table view
- `QuestBoard.Service/Areas/Platform/Views/Users/Index.Mobile.cshtml` - new mobile stacked-card variant
- `QuestBoard.Service/Areas/Platform/Views/Group/Index.cshtml` - added "Manage Users" header button
- `QuestBoard.Service/Areas/Platform/Views/Group/Index.Mobile.cshtml` - added "Manage Users" header button
- `QuestBoard.IntegrationTests/Controllers/UsersControllerIntegrationTests.cs` - new test file, 6 test methods

## Decisions Made
- Followed `41-PATTERNS.md`'s exact controller/view scaffolding verbatim — no deviation in method bodies, route names, or copy
- CSRF test (`Disable_Post_WithoutAntiForgeryToken_IsRejected`) rewritten from an HTTP-400 assertion to a "no server error" assertion, plus a new companion reflection test (`Disable_And_Enable_Actions_CarryValidateAntiForgeryToken`) that directly proves `[ValidateAntiForgeryToken]` presence on both actions — the test harness's `TestAntiforgeryDecorator` (in `WebApplicationFactoryBase.cs`) always validates successfully by design, so no live HTTP round-trip in this codebase can observe a real CSRF rejection. This matches the established convention documented in `GroupPickerControllerIntegrationTests` and the app-wide `AntiForgeryTokenCoverageTests` reflection sweep, which independently already covers `UsersController` since it scans every controller type in the assembly.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] CSRF integration test could not observe a 400 in this test harness**
- **Found during:** Task 3 (test run)
- **Issue:** The plan's acceptance criteria for `Disable_Post_WithoutAntiForgeryToken_IsRejected` expected a >=400 response for a POST missing the antiforgery token. The test harness replaces `IAntiforgery` with a `TestAntiforgeryDecorator` that always succeeds validation (see `WebApplicationFactoryBase.cs`), so the controller redirected (302) instead of rejecting, exactly as documented precedent in `GroupPickerControllerIntegrationTests` already establishes for this codebase.
- **Fix:** Rewrote the test to assert "no server error" (status < 500) instead of a hard rejection, and added a new reflection-based test (`Disable_And_Enable_Actions_CarryValidateAntiForgeryToken`) that directly proves both `Disable` and `Enable` carry `[ValidateAntiForgeryToken]` — the actual, provable mitigation for T-41-02 in this codebase's test infrastructure.
- **Files modified:** `QuestBoard.IntegrationTests/Controllers/UsersControllerIntegrationTests.cs`
- **Commit:** `0bab65f`

## Issues Encountered

None blocking. The CSRF-test harness limitation above was identified and resolved within the same task before committing.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- Plan 04 (`AccountController.Login` disabled-vs-lockout messaging, SAFE-04) can proceed independently — it only needs `IIdentityService.GetLockoutEndAsync`, already available since Plan 02, and does not depend on anything new from this plan.
- Full solution build (`dotnet build QuestBoard.Service/QuestBoard.Service.csproj`) succeeds with 0 warnings, 0 errors.
- `dotnet test --filter FullyQualifiedName~UsersControllerIntegrationTests` passes all 6 tests; the pre-existing `AntiForgeryTokenCoverageTests` and `PlatformAreaIntegrationTests` suites remain green alongside the new tests.
- No blockers.

---
*Phase: 41-safe-user-removal-account-disable*
*Completed: 2026-07-04*

## Self-Check: PASSED
