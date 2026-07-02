---
phase: 30-group-ux-admin-user-creation
plan: 02
subsystem: auth
tags: [aspnet-core-mvc, identity, razor, group-picker]

# Dependency graph
requires:
  - phase: 30-group-ux-admin-user-creation
    provides: "30-01 GroupPickerController (Index GET accepting returnUrl) — the redirect target wired in this plan"
provides:
  - "AccountController.Login POST redirects to GroupPicker/Index with returnUrl instead of RedirectToLocal — every successful login now runs the group-context lifecycle (D-01)"
  - "Public self-registration surface fully removed: Register GET/POST actions deleted, Register.cshtml + Register.Mobile.cshtml deleted, Create Account links removed from Login views (REG-01)"
affects: [30-04-nav-group-switch, 30-05-tests]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Login success branch routes through GroupPickerController.Index rather than RedirectToLocal directly; GroupPicker's own RedirectToLocal (with Url.IsLocalUrl check) is the only place open-redirect validation now happens for the post-login returnUrl"

key-files:
  created: []
  modified:
    - QuestBoard.Service/Controllers/Admin/AccountController.cs
    - QuestBoard.Service/Views/Account/Login.cshtml
    - QuestBoard.Service/Views/Account/Login.Mobile.cshtml
  deleted:
    - QuestBoard.Service/Views/Account/Register.cshtml
    - QuestBoard.Service/Views/Account/Register.Mobile.cshtml

key-decisions:
  - "RegisterViewModel left in place (unused but harmless) per plan scope — removing it was explicitly out of scope for this plan"
  - "ConfirmationEmailJob enqueue logic removed from AccountController.Register entirely (not relocated here) — it already exists in AdminController.CreateUser from plan 30-03"

requirements-completed: [UX-01, UX-02, UX-03, REG-01]

# Metrics
duration: 15min
completed: 2026-06-30
status: complete
---

# Phase 30 Plan 02: Login → GroupPicker Wiring & Registration Removal Summary

**AccountController.Login POST now redirects through GroupPickerController.Index instead of RedirectToLocal, and the entire public self-registration surface (actions + views + links) is deleted**

## Performance

- **Duration:** 15 min
- **Tasks:** 2
- **Files modified:** 6 (4 modified, 2 deleted)

## Accomplishments
- `AccountController.Login` POST success branch (`result.Succeeded`) now returns `RedirectToAction("Index", "GroupPicker", new { returnUrl })`, routing every successful login through the group-context picker built in plan 30-01 (D-01, UX-01/UX-02/UX-03)
- `Register` GET and POST actions deleted from `AccountController` along with their `ConfirmationEmailJob` enqueue logic (that logic now lives solely in `AdminController.CreateUser` from plan 30-03)
- `RedirectToLocal` private helper retained — still used by `Logout`, `ConfirmEmailChange`, and others
- `Views/Account/Register.cshtml` and `Register.Mobile.cshtml` deleted entirely
- "Don't have an account? / Create Account" block removed from both `Login.cshtml` and `Login.Mobile.cshtml` — no view under `Views/Account/` references `asp-action="Register"` anymore

## Task Commits

Each task was committed atomically:

1. **Task 1: Redirect Login POST to GroupPicker and remove Register GET/POST actions** - `f588934` (feat)
2. **Task 2: Delete Register views and remove Create Account links from Login views** - `564628f` (feat)

## Files Created/Modified
- `QuestBoard.Service/Controllers/Admin/AccountController.cs` - Login POST success branch redirects to `GroupPicker/Index`; `Register` GET/POST actions deleted
- `QuestBoard.Service/Views/Account/Login.cshtml` - removed "Create Account" block and trailing `<hr>`
- `QuestBoard.Service/Views/Account/Login.Mobile.cshtml` - removed "Create Account" link and `<hr>`
- `QuestBoard.Service/Views/Account/Register.cshtml` - deleted
- `QuestBoard.Service/Views/Account/Register.Mobile.cshtml` - deleted

## Decisions Made
- Left `RegisterViewModel` in place (unused but harmless) — removing it was explicitly out of scope per the plan's artifact table
- Did not relocate the `ConfirmationEmailJob` enqueue logic from the deleted `Register` POST — it already exists in `AdminController.CreateUser` (plan 30-03), so deleting it here (rather than moving it) is correct and avoids duplication

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

A full `dotnet test QuestBoard.slnx` run after both tasks shows 5 failing integration tests, all expected:
- `AccountControllerIntegrationTests.Register_Post_WithMismatchedPasswords_ShouldReturnError`, `Register_Post_WithValidData_ShouldCreateUser`, `Register_Get_ShouldReturnSuccessStatusCode` — fail because `/Account/Register` now correctly 404s; the plan's own `<verification>` section calls this out explicitly ("integration tests that assert `/Account/Register` returns 200/Redirect will FAIL — they are updated in plan 30-05... do not 'fix' by restoring Register")
- `MobileViewsTests.MobileAccountRegister_MobileUserAgent_RendersGlassCardForm` — same root cause, same expected-failure category
- `GroupManagementIntegrationTests.AddMember_ValidUserAndGroup_ShouldAddUserGroupsRow` — the pre-existing, unrelated failure documented in `deferred-items.md` from plan 30-03 (Platform area `GroupController.AddMember`, untouched by this plan)

No new unexpected failures. `dotnet build QuestBoard.slnx -c Debug` succeeded with 0 errors after each task.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- Every successful login now lands on `/groups/pick` (or auto-redirects through it for single-group users) — the full login → group-context chain from plans 30-01 and 30-02 is wired end-to-end
- Public self-registration is fully closed off (REG-01): no route, no view, no link
- The 4 Register-related integration test failures are expected and tracked for plan 30-05 to update/remove
- The 1 pre-existing `GroupManagementIntegrationTests` failure remains tracked in `deferred-items.md`, unaffected by this plan
- No blockers for plan 30-04 (nav group switch) or 30-05 (tests)

---
*Phase: 30-group-ux-admin-user-creation*
*Completed: 2026-06-30*

## Self-Check: PASSED

All 4 claimed files (AccountController.cs, Login.cshtml, Login.Mobile.cshtml, SUMMARY.md) verified present on disk; both Register view files confirmed deleted; both commit hashes (f588934, 564628f) verified present in git log.
