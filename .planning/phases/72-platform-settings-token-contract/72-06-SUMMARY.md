---
phase: 72-platform-settings-token-contract
plan: 06
subsystem: testing
tags: [xunit, integration-tests, authorization, aspnet-core-authorization]

# Dependency graph
requires:
  - phase: 72-platform-settings-token-contract (72-04)
    provides: "IntegrationsController — SuperAdmin-only /platform/Integrations page for the instance-wide default"
  - phase: 72-platform-settings-token-contract (72-05)
    provides: "AdminIntegrationsController — group-scoped Omphalos override CRUD"
provides:
  - "IntegrationsAreaIntegrationTests — SuperAdmin/Admin/Player/unauthenticated matrix for /platform/Integrations/Index"
  - "AdminIntegrationsAuthorizationTests — Group Admin/DungeonMaster/Player/SuperAdmin/unauthenticated matrix for /AdminIntegrations/Index"
  - "AddPlatformSettings migration proven to apply cleanly against the live integration-test database (transitively, via every authenticated test in both classes)"
affects: []

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Authorization matrix tests modeled verbatim on PlatformAreaIntegrationTests/AdminHandlerIntegrationTests shape — no new test infrastructure"

key-files:
  created:
    - QuestBoard.IntegrationTests/Controllers/IntegrationsAreaIntegrationTests.cs
    - QuestBoard.IntegrationTests/Controllers/AdminIntegrationsAuthorizationTests.cs
  modified:
    - QuestBoard.Service/Views/AdminIntegrations/Index.cshtml (moved from Views/Admin/Integrations.cshtml)
    - QuestBoard.Service/Views/AdminIntegrations/Index.Mobile.cshtml (moved from Views/Admin/Integrations.Mobile.cshtml)

key-decisions:
  - "Fixed a Rule 1 bug found while verifying Task 2: AdminIntegrationsController's View() calls resolve via the default MVC convention (Views/{Controller}/{Action}.cshtml), which for a sibling controller named AdminIntegrationsController is Views/AdminIntegrations/Index.cshtml — but 72-05 shipped the views at Views/Admin/Integrations.cshtml, causing every request (including a legitimate Group Admin) to 404. Fixed by moving the view files to the conventional location rather than hardcoding an explicit ~/Views/... path in the controller, so the existing MobileViewLocationExpander (which operates on MVC's location-format search, not explicit paths) continues to resolve the .Mobile variant correctly."

requirements-completed: [SETT-01, SETT-07, SETT-09, SETT-10]

coverage:
  - id: D1
    description: "A SuperAdmin can reach the instance-wide Integrations page (/platform/Integrations/Index); an Admin, a Player, and an unauthenticated user cannot"
    requirement: SETT-07
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/IntegrationsAreaIntegrationTests.cs#Integrations_WhenSuperAdmin_ShouldReturn200"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/IntegrationsAreaIntegrationTests.cs#Integrations_WhenAdmin_ShouldDeny"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/IntegrationsAreaIntegrationTests.cs#Integrations_WhenNotSuperAdmin_ShouldDeny"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/IntegrationsAreaIntegrationTests.cs#Integrations_WhenNotAuthenticated_ShouldRedirect"
        status: pass
    human_judgment: false
  - id: D2
    description: "A group Admin can reach the group-override page (/AdminIntegrations/Index); a DungeonMaster and a Player cannot; SuperAdmin bypasses; an unauthenticated user is redirected"
    requirement: SETT-10
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/AdminIntegrationsAuthorizationTests.cs#GroupIntegrations_WhenGroupAdmin_ShouldReturn200"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/AdminIntegrationsAuthorizationTests.cs#GroupIntegrations_WhenDungeonMaster_ShouldDeny"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/AdminIntegrationsAuthorizationTests.cs#GroupIntegrations_WhenPlayer_ShouldDeny"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/AdminIntegrationsAuthorizationTests.cs#GroupIntegrations_WhenSuperAdmin_ShouldReturn200"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/AdminIntegrationsAuthorizationTests.cs#GroupIntegrations_WhenNotAuthenticated_ShouldRedirect"
        status: pass
    human_judgment: false
  - id: D3
    description: "The AddPlatformSettings migration applies cleanly against the live SQL Server integration-test database"
    requirement: SETT-01
    verification:
      - kind: integration
        ref: "dotnet test QuestBoard.IntegrationTests (all 9 new tests run against the real WebApplicationFactoryBase test host, which applies every pending migration on startup)"
        status: pass
    human_judgment: false

# Metrics
duration: 22min
completed: 2026-07-11
status: complete
---

# Phase 72 Plan 06: Authorization Matrix Tests for Both Settings Pages Summary

**Two new integration-test classes (9 tests total) proving SuperAdminOnly on `/platform/Integrations` and group-scoped AdminOnly-with-DM-excluded on `/AdminIntegrations`, run against a live SQL Server test database that transitively proves the `AddPlatformSettings` migration applies cleanly — and a Rule 1 fix for a 404 bug in the group-override page's view resolution discovered while writing Task 2's tests.**

## Performance

- **Duration:** 22 min
- **Started:** 2026-07-11T16:52:41+02:00
- **Completed:** 2026-07-11T17:00:35+02:00
- **Tasks:** 2
- **Files modified:** 4 (2 created, 2 moved/fixed)

## Accomplishments
- `IntegrationsAreaIntegrationTests` (4 tests) — SuperAdmin gets 200 on `/platform/Integrations/Index`; a group-scoped Admin (not SuperAdmin), a Player, and an unauthenticated client are all denied, proving the instance-wide page's `SuperAdminOnly` boundary end-to-end.
- `AdminIntegrationsAuthorizationTests` (5 tests) — a group Admin gets 200 on `/AdminIntegrations/Index`; a DungeonMaster and a Player are denied (proving SETT-10's explicit DM exclusion); SuperAdmin bypasses (consistent with `AdminHandler`); unauthenticated is redirected.
- Every authenticated test in both classes runs against the real ASP.NET Core authorization pipeline and a real SQL Server test database that applies all pending EF Core migrations on startup — transitively proving `AddPlatformSettings` applies cleanly, with no separate migration-smoke test needed.
- Found and fixed a genuine 404 bug in `AdminIntegrationsController`: its `View()` calls resolved via MVC's default convention to `Views/AdminIntegrations/Index.cshtml`, but 72-05 shipped the actual view files at `Views/Admin/Integrations.cshtml`. Every request to the group-override page — including a legitimate Group Admin's — was 404ing before this fix.

## Task Commits

Each task was committed atomically:

1. **Task 1: Instance-wide Integrations authorization matrix** - `87e2760f` (test)
2. **Task 2: Group-override authorization matrix + view-path bugfix** - `cc602c7b` (test)

## Files Created/Modified
- `QuestBoard.IntegrationTests/Controllers/IntegrationsAreaIntegrationTests.cs` - 4-test SuperAdminOnly matrix for `/platform/Integrations/Index`
- `QuestBoard.IntegrationTests/Controllers/AdminIntegrationsAuthorizationTests.cs` - 5-test group-scoped AdminOnly (DM-excluded) matrix for `/AdminIntegrations/Index`
- `QuestBoard.Service/Views/AdminIntegrations/Index.cshtml` - moved from `Views/Admin/Integrations.cshtml` to the MVC-conventional location for this sibling controller
- `QuestBoard.Service/Views/AdminIntegrations/Index.Mobile.cshtml` - moved from `Views/Admin/Integrations.Mobile.cshtml` alongside the desktop view

## Decisions Made
- Fixed the view-resolution bug by **moving the view files** to the conventional `Views/AdminIntegrations/Index.cshtml` path rather than hardcoding an explicit `~/Views/Admin/Integrations.cshtml` path string in the controller's `View()` calls. An explicit app-relative path bypasses ASP.NET Core's `IViewLocationExpander` pipeline (`FindView`'s location-format search), which is what `MobileViewLocationExpander` hooks into to swap in the `.Mobile.cshtml` variant — hardcoding the path would have silently broken mobile view selection for this page. Moving the files preserves the existing, working mobile-detection mechanism with zero controller changes beyond what 72-05 already wrote.
- No changes needed to `AdminIntegrationsController.cs` itself — after the file move, its existing `return View(model)` / `return View("Index", model)` calls resolve correctly via the standard convention.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Fixed 404 on the group-override Integrations page (`AdminIntegrationsController` view resolution)**
- **Found during:** Task 2 (writing `AdminIntegrationsAuthorizationTests`) — the `GroupIntegrations_WhenGroupAdmin_ShouldReturn200` test failed with an unhandled `InvalidOperationException: The view 'Index' was not found`, surfaced as a 404 through the app's exception handler.
- **Issue:** `AdminIntegrationsController`'s `Index` GET/`Save`-invalid-model actions call `View(model)`/`View("Index", model)` with no explicit path, so MVC's default convention searches `Views/AdminIntegrations/Index.cshtml`. 72-05 shipped the actual view files at `Views/Admin/Integrations.cshtml` (matching the plan/UI-SPEC's literal `<files>` listing, written when the page was still expected to be an `AdminController` action rather than the sibling controller it became). The mismatch meant the group-override page 404'd for every caller, including a legitimate Group Admin — this is exactly the kind of regression T-72-06-01 in this plan's threat register exists to catch.
- **Fix:** Moved `Views/Admin/Integrations.cshtml` → `Views/AdminIntegrations/Index.cshtml` and `Views/Admin/Integrations.Mobile.cshtml` → `Views/AdminIntegrations/Index.Mobile.cshtml` (content unchanged — both views reference actions via `asp-action`, which resolves relative to the current controller regardless of the view's folder). No controller code changes required.
- **Files modified:** `QuestBoard.Service/Views/AdminIntegrations/Index.cshtml`, `QuestBoard.Service/Views/AdminIntegrations/Index.Mobile.cshtml` (moved, not edited)
- **Verification:** `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~AdminIntegrationsAuthorizationTests"` — all 5 tests pass (previously 2 failed with the 404). Full solution `dotnet build` (0 warnings/errors) and full `dotnet test` (311 unit + 405 integration, all pass) confirm no regression to the desktop/mobile view split or to `IntegrationsController`'s unrelated `/platform/Integrations` page.
- **Committed in:** `cc602c7b` (Task 2 commit)

---

**Total deviations:** 1 auto-fixed (1 Rule 1 bug)
**Impact on plan:** The fix was necessary for the group-override page to function at all — without it, SETT-09/SETT-10 would have been unreachable in production despite passing `dotnet build`. No scope creep: the fix is a pure file relocation, no new behavior, no architectural change.

## Issues Encountered

The Rule 1 bug above (`AdminIntegrationsController` view-path mismatch) was the only issue. It was not caught by 72-05's own verification because that plan's `<verify>` gates (grep-based content checks + `dotnet build`) never exercised the controller's actual view-resolution at runtime — only this plan's live-request integration tests could surface it. This is precisely the class of regression this plan's tests were designed to guard against (see threat T-72-06-01).

## User Setup Required

None - no external service configuration, no new packages. Both test classes reuse existing test infrastructure (`WebApplicationFactoryBase`, `AuthenticationHelper`, `TestDataHelper`) verbatim.

## Next Phase Readiness

- Both settings pages' authorization boundaries (SETT-01, SETT-07, SETT-09, SETT-10) are now proven end-to-end by tests that fail the build on any future regression — the standing guard called for in this plan's threat register.
- The `AddPlatformSettings` migration is proven to apply cleanly in the shared test host (SETT-08, proven transitively).
- Phase 72 is now fully executed (6/6 plans). Phase 73 (Quest Board token generation) can proceed — it will resolve settings by a quest's `GroupId` via the group-override → instance-default cascade this phase's `IPlatformSettingService` (72-03) already implements, and both settings surfaces are now confirmed reachable by their intended roles.
- No blockers.

---
*Phase: 72-platform-settings-token-contract*
*Completed: 2026-07-11*

## Self-Check: PASSED

All 4 created/moved files verified present on disk (`IntegrationsAreaIntegrationTests.cs`, `AdminIntegrationsAuthorizationTests.cs`, `Views/AdminIntegrations/Index.cshtml`, `Views/AdminIntegrations/Index.Mobile.cshtml`); both task commit hashes (`87e2760f`, `cc602c7b`) verified present in git log.
