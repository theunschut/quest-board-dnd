---
phase: 31-unauthenticated-landing-redirect
plan: 04
subsystem: testing
tags: [xunit, integration-tests, aspnetcore, auth, middleware]

# Dependency graph
requires:
  - phase: 31-01
    provides: "[Authorize] lockdown on HomeController/CalendarController/QuestLogController/DungeonMasterController"
  - phase: 31-02
    provides: "Public landing page at HomeController.Index, quest board migrated to QuestController.Index at /quests, GroupPickerController fallback redirect to /quests"
  - phase: 31-03
    provides: "GroupSessionMiddleware and the real /groups/pick route"
provides:
  - Full integration-test coverage for the Phase 31 auth lockdown, landing/quest split, and session-recovery middleware
  - Human-verified sign-off on the landing page, /quests routing, and single-group session recovery
  - Fix for authenticated visitors seeing the logged-out landing page at /
affects: [group-session-recovery, integration-tests, home-controller]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "GetWithUserAgentAsync overload that attaches a Test-scheme Authorization header, used to route mobile-view-detection tests through authenticated requests after the auth lockdown"
    - "try/finally reset of the shared TestGroupContext singleton (ActiveGroupId) around each GroupSessionMiddleware test to avoid cross-test bleed"

key-files:
  created:
    - QuestBoard.IntegrationTests/Controllers/GroupSessionMiddlewareIntegrationTests.cs
  modified:
    - QuestBoard.IntegrationTests/Controllers/CalendarControllerIntegrationTests.cs
    - QuestBoard.IntegrationTests/Controllers/QuestLogControllerIntegrationTests.cs
    - QuestBoard.IntegrationTests/Controllers/HomeControllerIntegrationTests.cs
    - QuestBoard.IntegrationTests/Controllers/DungeonMasterControllerIntegrationTests.cs
    - QuestBoard.IntegrationTests/Controllers/QuestControllerIntegrationTests_Comprehensive.cs
    - QuestBoard.IntegrationTests/Mobile/MobileViewsTests.cs
    - QuestBoard.IntegrationTests/Tests/TenantIsolationTests.cs
    - QuestBoard.Service/Controllers/QuestBoard/HomeController.cs

key-decisions:
  - "Mobile-view and tenant-isolation stragglers (out of the plan's named three test classes) were fixed under Task 4's full-suite gate rather than deferred, since they broke for the identical root cause (unauthenticated requests to now-locked-down routes) already being fixed elsewhere in the same task"
  - "Authenticated visitors hitting / are redirected straight to /quests via RedirectToAction rather than rendering group-scoped data inline in HomeController, keeping the landing page free of any group lookup and relying on GroupSessionMiddleware (session-expiry) and the /quests EF Core query filter (SuperAdmin all-groups) to handle the edge cases"

requirements-completed: [UX-01, UX-04]

# Metrics
duration: 25min
completed: 2026-07-01
---

# Phase 31 Plan 04: Test Suite Green Gate & Human Verification Summary

**Full integration suite (55 unit + 181 integration tests) green after auth lockdown/landing-split/session-recovery changes, plus a human-verified fix for authenticated visitors landing on the logged-out home page**

## Performance

- **Duration:** 25 min (08:40:21 first task commit -> 09:05:11 last fix commit; checkpoint verification time not included)
- **Started:** 2026-07-01T08:40:21+02:00
- **Completed:** 2026-07-01T09:05:11+02:00
- **Tasks:** 5 (4 auto + 1 checkpoint)
- **Files modified:** 8 (1 created, 7 modified)

## Accomplishments
- Calendar, QuestLog, Home, and DungeonMaster integration tests updated to prove the Phase 31 auth lockdown (D-01/D-02/D-04): authenticated 200s, unauthenticated redirects, and landing-page-only assertions on `/`
- `/quests` route covered with both an authenticated-200 (rendering seeded quest content) and an unauthenticated-redirect test (D-05)
- New `GroupSessionMiddlewareIntegrationTests` proves the session-recovery middleware's four behaviors: no-group redirect to `/groups/pick`, SuperAdmin exemption, `/groups/pick` non-loop, and active-group pass-through (D-09/D-10/D-11)
- Full-suite green gate: found and fixed 13 additional stragglers beyond the three named test classes (`MobileViewsTests`, `TenantIsolationTests`) that were also hitting now-locked-down routes unauthenticated
- Human-verify checkpoint approved after all 6 manual checks passed, following one round-trip fix for a UX gap the checkpoint itself surfaced

## Task Commits

Each task was committed atomically:

1. **Task 1: Update Calendar/QuestLog/Home/DM tests for auth lockdown and landing split** - `b8da010` (test)
2. **Task 2: Add /quests authenticated-200 and unauthenticated-redirect coverage** - `e504996` (test)
3. **Task 3: Create GroupSessionMiddlewareIntegrationTests** - `abd6be3` (feat)
4. **Task 4: Full-suite green gate — fix mobile and tenant-isolation stragglers** - `2dfa228` (test)
5. **Task 5: Human-verify checkpoint** - approved; triggered fix `63ae9c4` (fix)

**Plan metadata:** committed alongside this SUMMARY (see final commit)

## Files Created/Modified
- `QuestBoard.IntegrationTests/Controllers/GroupSessionMiddlewareIntegrationTests.cs` - New: 4 tests covering the session-recovery middleware's redirect, SuperAdmin exemption, exempt-path non-loop, and pass-through behaviors, with try/finally reset of the shared `TestGroupContext` singleton
- `QuestBoard.IntegrationTests/Controllers/CalendarControllerIntegrationTests.cs` - 200-expecting tests switched to authenticated clients; new unauthenticated-redirect test added (D-01)
- `QuestBoard.IntegrationTests/Controllers/QuestLogControllerIntegrationTests.cs` - Same transformation as Calendar (D-01)
- `QuestBoard.IntegrationTests/Controllers/HomeControllerIntegrationTests.cs` - Quest-display tests replaced with landing-page assertions (Log In button present, no quest content even when quests are seeded) (D-04)
- `QuestBoard.IntegrationTests/Controllers/DungeonMasterControllerIntegrationTests.cs` - New unauthenticated-redirect test for Profile GET (D-02)
- `QuestBoard.IntegrationTests/Controllers/QuestControllerIntegrationTests_Comprehensive.cs` - New authenticated-200 and unauthenticated-redirect tests for `/quests` (D-05)
- `QuestBoard.IntegrationTests/Mobile/MobileViewsTests.cs` - Mobile-view-detection tests routed through authenticated requests (new `GetWithUserAgentAsync` overload with Test-scheme Authorization header) instead of unauthenticated `/`, `/Calendar`, `/QuestLog`
- `QuestBoard.IntegrationTests/Tests/TenantIsolationTests.cs` - Group-filter tests moved from the landing page (no longer renders quest content) to authenticated `/quests` requests
- `QuestBoard.Service/Controllers/QuestBoard/HomeController.cs` - `Index` now redirects an already-authenticated visitor straight to `/quests` instead of rendering the logged-out landing copy

## Decisions Made
- Fixed the 13 mobile/tenant-isolation stragglers within Task 4's scope rather than treating them as new out-of-plan work, since they shared the exact root cause (unauthenticated requests to routes locked down in 31-01/31-02) already being remediated in the same task
- `HomeController.Index`'s authenticated-redirect fix required no new group-lookup logic: `GroupSessionMiddleware` already intercepts any authenticated user with no active group before they reach `/`  (since `/` is not on its exempt-path list), and the EF Core global query filter already treats a null `ActiveGroupId` (SuperAdmin) as "see all groups" once redirected to `/quests` — confirmed by re-reading both code paths before committing the fix

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Fixed 13 additional test stragglers beyond the three named classes**
- **Found during:** Task 4 (full-suite green gate)
- **Issue:** `MobileViewsTests` and `TenantIsolationTests` were still issuing unauthenticated requests to `/`, `/Calendar`, and `/QuestLog` — all now locked down or repurposed as the public landing page by 31-01/31-02 — causing 12 mobile-detection tests and group-filter assertions to fail
- **Fix:** Added a `GetWithUserAgentAsync` overload attaching a Test-scheme Authorization header for mobile tests; moved tenant-isolation group-filter assertions to authenticated `/quests` requests
- **Files modified:** `QuestBoard.IntegrationTests/Mobile/MobileViewsTests.cs`, `QuestBoard.IntegrationTests/Tests/TenantIsolationTests.cs`
- **Verification:** `dotnet test` full suite green (55 unit + 181 integration, 0 failures)
- **Committed in:** `2dfa228` (Task 4 commit)

**2. [Rule 1 - Bug, found via checkpoint] Authenticated visitors saw the logged-out landing page at /**
- **Found during:** Task 5 (human-verify checkpoint) — the orchestrator applied this fix between checkpoint approval rounds, not the plan's task executor
- **Issue:** An already-authenticated user hitting `/` saw the public "Log In" landing copy instead of being routed to their quest board — misleading UX flagged by the human verifier during step 3 of the checkpoint
- **Fix:** `HomeController.Index` now redirects authenticated users straight to `/quests` via `RedirectToAction("Index", "Quest")`. Investigated and confirmed no additional group-lookup logic was needed: `GroupSessionMiddleware` already redirects any authenticated user with no active group to `/groups/pick` before reaching this action, and the EF Core query filter on `QuestEntity`/`ShopItemEntity` already treats a null `ActiveGroupId` (SuperAdmin) as "see all groups"
- **Files modified:** `QuestBoard.Service/Controllers/QuestBoard/HomeController.cs`
- **Verification:** User re-ran all 6 checkpoint verification steps after the fix and confirmed: "as far as i can tell, everything works." Full suite reconfirmed green post-fix (55 unit + 181 integration, 0 failures)
- **Committed in:** `63ae9c4` (fix)

---

**Total deviations:** 2 auto-fixed (both Rule 1 - bug fixes surfaced by the full-suite gate and the human-verify checkpoint respectively)
**Impact on plan:** Both fixes were necessary corrections directly caused by Phase 31's own changes (route lockdown and landing/board split) — no scope creep. Deviation 1 kept the gate honest (no `[AllowAnonymous]` added, no `[Authorize]` weakened). Deviation 2 closed a genuine UX regression the manual checkpoint exists to catch.

## Issues Encountered
A related but separate routing regression (`[Route("groups/pick")]` silently disabling `GroupPickerController.Index`'s conventional route) was found during the Wave 1→2 post-merge gate, before this plan started, and is documented as a Post-Merge Amendment in `31-03-SUMMARY.md` (commit `997d27f`) rather than here, since it originated from a 31-03 task, not a 31-04 task.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- The full integration suite (55 unit + 181 integration tests) is green with zero failures, covering every Phase 31 behavior: auth lockdown (D-01/D-02), landing/board split (D-04/D-05), fallback redirect (D-07), and session-recovery middleware (D-09/D-10/D-11)
- Human sign-off recorded for all 6 manual verification steps: public landing page with no quest content, post-login redirect to `/quests`, navbar brand link to `/quests`, single-group session recovery via session-cookie clear, and Calendar/QuestLog authenticated-vs-unauthenticated behavior
- Phase 31 is complete — no blockers for subsequent phases

---
*Phase: 31-unauthenticated-landing-redirect*
*Completed: 2026-07-01*

## Self-Check: PASSED

All created/modified files confirmed present:
- QuestBoard.IntegrationTests/Controllers/GroupSessionMiddlewareIntegrationTests.cs
- QuestBoard.IntegrationTests/Controllers/CalendarControllerIntegrationTests.cs
- QuestBoard.IntegrationTests/Controllers/QuestLogControllerIntegrationTests.cs
- QuestBoard.IntegrationTests/Controllers/HomeControllerIntegrationTests.cs
- QuestBoard.IntegrationTests/Controllers/DungeonMasterControllerIntegrationTests.cs
- QuestBoard.IntegrationTests/Controllers/QuestControllerIntegrationTests_Comprehensive.cs
- QuestBoard.IntegrationTests/Mobile/MobileViewsTests.cs
- QuestBoard.IntegrationTests/Tests/TenantIsolationTests.cs
- QuestBoard.Service/Controllers/QuestBoard/HomeController.cs

All commits confirmed in git log:
- b8da010 (Task 1)
- e504996 (Task 2)
- abd6be3 (Task 3)
- 2dfa228 (Task 4)
- 63ae9c4 (Task 5 checkpoint fix)

Full suite reconfirmed green at continuation time: 55 unit tests + 181 integration tests, 0 failures.
