---
phase: 31-unauthenticated-landing-redirect
plan: 03
subsystem: auth
tags: [middleware, aspnetcore, session, routing, redirect]

# Dependency graph
requires:
  - phase: 31-02
    provides: HomeController public landing page, QuestController.Index at /quests, GroupPickerController fallback redirect to /quests
  - phase: 28-tenant-isolation
    provides: IActiveGroupContext / ActiveGroupContextService reading SessionKeys.ActiveGroupId from ISession
  - phase: 30-group-ux-admin-user-creation
    provides: GroupPickerController.Index auto-pick logic for single-group users
provides:
  - GroupSessionMiddleware that redirects authenticated, non-SuperAdmin users with no ActiveGroupId to /groups/pick
  - Real /groups/pick route on GroupPickerController.Index (in addition to the conventional /GroupPicker/Index route)
  - Middleware registered in all environments (including Testing) between UseAuthentication and UseAuthorization
affects: [31-04, group-session-recovery, integration-tests]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Named middleware class resolving a scoped interface (IActiveGroupContext) via context.RequestServices.GetRequiredService — avoids reading HttpContext.Session directly so tests can substitute MutableGroupContext"
    - "Guard-order early-out pattern in middleware: anonymous check, then privileged-role check, then exempt-path check, then the actual gate — privileged-role check placed before the gate to prevent redirect loops"

key-files:
  created:
    - QuestBoard.Service/Middleware/GroupSessionMiddleware.cs
  modified:
    - QuestBoard.Service/Controllers/GroupPickerController.cs
    - QuestBoard.Service/Program.cs

key-decisions:
  - "Middleware resolves IActiveGroupContext from RequestServices rather than reading context.Session directly, keeping it consistent with ActiveGroupContextService and testable via MutableGroupContext in the Testing environment"
  - "SuperAdmin role check runs before the exempt-path check and before the group-null check, since SuperAdmin legitimately has a null ActiveGroupId by design"
  - "Both /groups/pick (new attribute route) and /GroupPicker (conventional route to the same controller) are exempt paths — omitting either would create a redirect loop depending on which URL form a request used"
  - "Redirect target is the hardcoded string literal \"/groups/pick\", never derived from request input — closes the open-redirect threat (T-31-06)"

patterns-established:
  - "Session-recovery guard: authenticated + non-privileged-role + non-exempt-path + null-group -> single hardcoded redirect, else fall through to next(context)"

requirements-completed: [UX-04]

# Metrics
duration: 2min
completed: 2026-07-01
---

# Phase 31 Plan 03: Session-Recovery Middleware Summary

**GroupSessionMiddleware redirects authenticated users with an expired/missing group session to /groups/pick, exempting SuperAdmin and picker/auth/platform/error paths, with the picker itself now reachable at a real /groups/pick route**

## Performance

- **Duration:** 2 min (build/restore overhead ~2 min of the 133s wall-clock window was one-time NuGet restore for the fresh worktree)
- **Started:** 2026-07-01T06:26:07Z
- **Completed:** 2026-07-01T06:28:20Z
- **Tasks:** 3
- **Files modified:** 3 (1 created, 2 modified)

## Accomplishments
- `/groups/pick` is now a real attribute route on `GroupPickerController.Index`, alongside the existing conventional `/GroupPicker/Index` route
- New `GroupSessionMiddleware` guards every authenticated request: SuperAdmin and exempt paths pass through; everyone else with a null `ActiveGroupId` is redirected to `/groups/pick` before reaching a group-scoped controller
- Middleware registered between `UseAuthentication` and `UseAuthorization` in all environments, including Testing, so the integration tests added in the next plan can assert its behavior

## Task Commits

Each task was committed atomically:

1. **Task 1: Add [Route("groups/pick")] to GroupPickerController.Index** - `7b8c766` (feat)
2. **Task 2: Create GroupSessionMiddleware** - `ac7fda4` (feat)
3. **Task 3: Register GroupSessionMiddleware in Program.cs** - `ef018a5` (feat)

**Plan metadata:** committed alongside this SUMMARY (see final commit)

## Files Created/Modified
- `QuestBoard.Service/Middleware/GroupSessionMiddleware.cs` - New middleware: authenticated + non-SuperAdmin + non-exempt-path + null-ActiveGroupId → redirect to `/groups/pick`; resolves `IActiveGroupContext` via `RequestServices.GetRequiredService`
- `QuestBoard.Service/Controllers/GroupPickerController.cs` - Added `[Route("groups/pick")]` to the `Index` GET action
- `QuestBoard.Service/Program.cs` - Registered `app.UseMiddleware<GroupSessionMiddleware>();` between `UseAuthentication()` and `UseAuthorization()`, outside the Testing-only guard block

## Decisions Made
- Resolved `IActiveGroupContext` from `context.RequestServices` rather than reading `HttpContext.Session` directly, matching the plan's testability requirement and keeping a single source of truth (`ActiveGroupContextService`) for how the active group is determined
- Kept both `/groups/pick` and `/GroupPicker` in the exempt-path list rather than relying on only one, since both resolve to the same controller/action and either could be hit directly

## Deviations from Plan

None - plan executed exactly as written.

*(One environment-setup step was required but is not a plan deviation: the freshly-created worktree had no `obj/project.assets.json`, so `dotnet restore` was run once before the first build. This is NuGet restore of already-declared project dependencies — not a new package install — and is excluded from Rule 3's package-install carve-out, which concerns adding new packages, not restoring existing references.)*

## Issues Encountered

None beyond the one-time restore noted above, which resolved on the first attempt.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- `GroupSessionMiddleware` is live in all environments (including Testing), ready for the integration tests in plan 31-04 to assert: 302 to `/groups/pick` for an authenticated non-SuperAdmin user with no active group; no redirect for SuperAdmin; no redirect when `/groups/pick` itself is requested; no redirect when an active group is already set.
- Per the isolated-execution instructions for this wave, the known Wave-1 test failures (Calendar/QuestLog/Home/Mobile/TenantIsolation, from the auth lockdown in 31-01) were left untouched — those are explicitly in scope for plan 31-04, not this plan.
- No blockers for plan 31-04.

---
*Phase: 31-unauthenticated-landing-redirect*
*Completed: 2026-07-01*

## Self-Check: PASSED

All created/modified files confirmed present:
- QuestBoard.Service/Middleware/GroupSessionMiddleware.cs
- QuestBoard.Service/Controllers/GroupPickerController.cs
- QuestBoard.Service/Program.cs

All commits confirmed in git log:
- 7b8c766 (Task 1)
- ac7fda4 (Task 2)
- ef018a5 (Task 3)
- d0af090 (docs: SUMMARY.md)

## Post-Merge Amendment (orchestrator)

The Wave 1→2 post-merge test gate caught a regression introduced by Task 1's
`[Route("groups/pick")]` addition: ASP.NET Core opts an action out of
conventional routing entirely once it carries any attribute route, so
`GroupPickerController.Index` stopped responding at the pre-existing
`/GroupPicker/Index` path — 404ing `RedirectToLocal` fallbacks, tag-helper
links, and `GroupPickerControllerIntegrationTests`. Fixed by the orchestrator
(not the plan's executor) by adding `[Route("[controller]/[action]")]`
alongside the vanity route, restoring both paths. Commit `997d27f`.
