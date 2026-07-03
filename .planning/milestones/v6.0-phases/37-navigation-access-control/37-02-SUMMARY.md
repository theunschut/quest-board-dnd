---
phase: 37-navigation-access-control
plan: 02
subsystem: auth
tags: [authorization, aspnet-identity, cookie-auth, integration-tests]

# Dependency graph
requires:
  - phase: 37-01
    provides: IActiveGroupContext.GetBoardTypeAsync (unrelated seam, no direct dependency but same phase/wave ordering)
provides:
  - SuperAdminOnly gate on AdminController.EmailStats (ANDed with class-level AdminOnly)
  - AccountController.AccessDenied GET action
  - App-wide ConfigureApplicationCookie.AccessDeniedPath wiring
  - Generalized (policy-agnostic) AccessDenied.cshtml copy
affects: [37-03 (nav-link hiding for EmailStats and BoardType), any future authorization-policy work]

# Tech tracking
tech-stack:
  added: []
  patterns: ["Method-level [Authorize(Policy=...)] ANDed with class-level policy for narrower-than-class authorization", "ConfigureApplicationCookie.AccessDeniedPath as the single app-wide 404-vs-AccessDenied switch"]

key-files:
  created: []
  modified:
    - QuestBoard.Service/Controllers/Admin/AdminController.cs
    - QuestBoard.Service/Controllers/Admin/AccountController.cs
    - QuestBoard.Service/Views/Shared/AccessDenied.cshtml
    - QuestBoard.Service/Program.cs
    - QuestBoard.IntegrationTests/Controllers/AdminControllerIntegrationTests.cs
    - QuestBoard.IntegrationTests/Controllers/AccountControllerIntegrationTests.cs

key-decisions:
  - "SuperAdminOnly attribute is described as ANDing with class-level AdminOnly, not overriding it — both must pass, which works because SuperAdmin already passes AdminOnly via AdminHandler's role bypass"
  - "AccessDenied action lives on AccountController (not a new controller) to reuse the /Account exemption already in GroupSessionMiddleware.ExemptPathPrefixes and avoid a redirect loop"
  - "AccessDenied.cshtml generalized to 'You Don't Have Permission' / fa-ban icon, keeping the modern-card structure and authenticated/unauthenticated branching intact"

patterns-established:
  - "Narrowing an action beyond its controller's class-level policy: add a method-level [Authorize(Policy=...)] above the action, keep the class-level attribute, and document it as an AND (not override) in code comments and SUMMARY"

requirements-completed: [ACCESS-01]

# Metrics
duration: 8min
completed: 2026-07-03
---

# Phase 37 Plan 02: EmailStats SuperAdmin Gate + AccessDenied Wiring Summary

**EmailStats locked to SuperAdmin via a method-level policy ANDed with the class-level AdminOnly gate, plus an app-wide ConfigureApplicationCookie.AccessDeniedPath wiring that turns every authorization-policy failure into a real AccessDenied page instead of a silent 404.**

## Performance

- **Duration:** 8 min
- **Started:** 2026-07-03T17:39:37Z
- **Completed:** 2026-07-03T17:45:07Z
- **Tasks:** 2 completed
- **Files modified:** 6

## Accomplishments
- `AdminController.EmailStats` now requires `SuperAdminOnly` in addition to the class-level `AdminOnly` policy — an Admin who is not also a SuperAdmin is rejected server-side on a direct URL hit
- `AccountController.AccessDenied` GET action wired, reusing the existing orphaned `AccessDenied.cshtml` view
- `ConfigureApplicationCookie.AccessDeniedPath = "/Account/AccessDenied"` added app-wide in `Program.cs` — this was previously unconfigured, so every policy failure across the whole app (not just EmailStats) silently 404'd
- `AccessDenied.cshtml` copy generalized off hardcoded "Dungeon Master Access Required" wording to policy-agnostic "You Don't Have Permission" copy, keeping the `modern-card` structure and authenticated/unauthenticated branches unchanged
- Integration tests added proving: Admin-not-SuperAdmin rejected from EmailStats, SuperAdmin succeeds (200), and `/Account/AccessDenied` serves generalized copy with no "Dungeon Master" wording

## Task Commits

Each task was committed atomically:

1. **Task 1: Gate EmailStats with SuperAdminOnly and wire AccessDenied action + view + cookie** - `395998e` (feat)
2. **Task 2: Integration tests — Admin-vs-SuperAdmin EmailStats gate + AccessDenied page** - `26fcf7e` (test)

_Note: Task 2 was tagged `tdd="true"` in the plan; because the SuperAdminOnly gate and AccessDenied wiring were already implemented in Task 1 (same plan, sequential), the tests were written and run directly against the completed implementation rather than as a separate RED phase against not-yet-written code — all 5 targeted tests (2 new EmailStats + 1 new AccessDenied + 2 pre-existing EmailStats regression checks) passed on first run._

## Files Created/Modified
- `QuestBoard.Service/Controllers/Admin/AdminController.cs` - Added `[Authorize(Policy = "SuperAdminOnly")]` on `EmailStats`, ANDed with the existing class-level `AdminOnly`
- `QuestBoard.Service/Controllers/Admin/AccountController.cs` - Added `AccessDenied` GET action (`[HttpGet]`, `[AllowAnonymous]`)
- `QuestBoard.Service/Views/Shared/AccessDenied.cshtml` - Generalized copy (heading, icon, both info/warning branches) off Dungeon-Master-specific wording
- `QuestBoard.Service/Program.cs` - Added `builder.Services.ConfigureApplicationCookie(options => options.AccessDeniedPath = "/Account/AccessDenied")`
- `QuestBoard.IntegrationTests/Controllers/AdminControllerIntegrationTests.cs` - Added `EmailStats_WhenAdminNotSuperAdmin_ShouldBeRejected` and `EmailStats_WhenSuperAdmin_ShouldSucceed`
- `QuestBoard.IntegrationTests/Controllers/AccountControllerIntegrationTests.cs` - Added `AccessDenied_Get_ShouldReturnSuccessWithGeneralizedCopy`

## Decisions Made
- Documented the new attribute as ANDing with (not overriding) the class-level `AdminOnly` policy, in both the code comment and here, per the plan's explicit instruction — this matters because a future reader could otherwise assume the method-level attribute replaces the class-level one
- Kept `AccessDenied` on `AccountController` rather than introducing a new controller, since `/Account` is already exempted in `GroupSessionMiddleware.ExemptPathPrefixes`, avoiding a redirect-loop risk
- Chose `fa-ban` (already-established denial icon pattern elsewhere in the app per plan guidance) over keeping `fa-crown`, since `fa-crown` was specifically tied to the now-removed DM-specific framing

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

None. `dotnet build QuestBoard.Service` and the targeted `dotnet test` filter both passed on first attempt.

**Note on full-suite regression run:** Running the full `QuestBoard.IntegrationTests` suite (not just the targeted filter) shows 12 pre-existing failures, all in `LayoutNavigationTests` (added by Plan 37-01 as an intentionally-RED scaffold — see commit `167e430`: "RED as expected: 12 assertion failures pending Plan 02's layout gating"). Per 37-02-PLAN.md's own objective, this plan is explicitly "deliberately layout-free" — nav-link hiding for EmailStats and BoardType is Plan 03's scope (Wave 2), not this plan's. These 12 failures are out of this plan's scope per the scope-boundary rule and are expected to turn green only after Plan 03 executes. No action taken; not a regression introduced by this plan (all 12 failures are in a test file this plan did not touch, and existed before Task 1/2 commits).

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- `SuperAdminOnly` gate on EmailStats and the app-wide `AccessDenied` wiring are both in place and tested — Plan 03 can now point its EmailStats nav-link visibility check at the same `SuperAdminOnly`-equivalent condition without needing further server-side changes here.
- The 12 pre-existing `LayoutNavigationTests` RED failures remain open for Plan 03 to close (out of this plan's scope).
- No blockers for Plan 03.

---
*Phase: 37-navigation-access-control*
*Completed: 2026-07-03*

## Self-Check: PASSED

- FOUND: QuestBoard.Service/Controllers/Admin/AdminController.cs
- FOUND: QuestBoard.Service/Controllers/Admin/AccountController.cs
- FOUND: QuestBoard.Service/Views/Shared/AccessDenied.cshtml
- FOUND: QuestBoard.Service/Program.cs
- FOUND: QuestBoard.IntegrationTests/Controllers/AdminControllerIntegrationTests.cs
- FOUND: QuestBoard.IntegrationTests/Controllers/AccountControllerIntegrationTests.cs
- FOUND: commit 395998e (Task 1)
- FOUND: commit 26fcf7e (Task 2)
