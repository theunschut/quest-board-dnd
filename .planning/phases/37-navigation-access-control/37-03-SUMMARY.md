---
phase: 37-navigation-access-control
plan: 03
subsystem: ui
tags: [aspnet-razor, dependency-injection, navigation, authorization]

# Dependency graph
requires:
  - phase: 37-01
    provides: IActiveGroupContext (BoardType lookup, later moved to IBoardTypeResolver by this plan's fix)
  - phase: 37-02
    provides: SuperAdminOnly gate on AdminController.EmailStats
provides:
  - OneShot allowlist nav gating in both _Layout.cshtml and _Layout.Mobile.cshtml
  - D-04 fix (Calendar hidden for anonymous visitors) in both layouts
  - SuperAdmin-only Email Stats nav link in both layouts
  - IBoardTypeResolver — new interface/service split out of IActiveGroupContext to keep BoardType lookups DI-cycle-safe
affects: [any future service that needs the active group's BoardType outside a controller action — must depend on IBoardTypeResolver, not add data-lookup dependencies to ActiveGroupContextService]

# Tech tracking
tech-stack:
  added: []
  patterns: ["Split a thin, structurally-load-bearing interface (IActiveGroupContext, which QuestBoardContext's constructor depends on) away from any richer capability that itself needs repository/DbContext access, to avoid circular DI graphs hidden behind factory-based registrations"]

key-files:
  created:
    - QuestBoard.Domain/Interfaces/IBoardTypeResolver.cs
    - QuestBoard.Service/Services/BoardTypeResolver.cs
  modified:
    - QuestBoard.Service/Views/Shared/_Layout.cshtml
    - QuestBoard.Service/Views/Shared/_Layout.Mobile.cshtml
    - QuestBoard.Domain/Interfaces/IActiveGroupContext.cs
    - QuestBoard.Service/Services/ActiveGroupContextService.cs
    - QuestBoard.Service/Program.cs
    - QuestBoard.IntegrationTests/Helpers/MutableGroupContext.cs
    - QuestBoard.IntegrationTests/WebApplicationFactoryBase.cs
    - QuestBoard.UnitTests/Services/SessionReminderJobTests.cs

key-decisions:
  - "IBoardTypeResolver introduced as a new interface separate from IActiveGroupContext, specifically to keep QuestBoardContext's dependency graph acyclic — discovered necessary only after the human-verify checkpoint revealed the app couldn't start"
  - "ActiveGroupContextService reverted to depending on only IHttpContextAccessor (its pre-Plan-37-01 shape); the BoardType lookup (which needs IGroupService) now lives on the new BoardTypeResolver service instead"
  - "MutableGroupContext (test double) implements both IActiveGroupContext and IBoardTypeResolver so existing test code (`factory.TestGroupContext.BoardType = ...`) needed zero changes"

patterns-established:
  - "When a service is a constructor dependency of a DbContext (directly or transitively), never let its implementation depend on anything that itself needs that DbContext — even indirectly through a repository/service layer. Split into a separate interface if the richer capability needs data access."

requirements-completed: [NAV-01, NAV-02, NAV-03, NAV-04, NAV-05, NAV-06, ACCESS-01]

# Metrics
duration: 45min
completed: 2026-07-03
---

# Phase 37 Plan 03: Nav Gating + Checkpoint Summary

**OneShot allowlist nav gating shipped in both desktop and mobile layouts (LayoutNavigationTests 16/16 green), plus a circular DI dependency fix (new IBoardTypeResolver service) discovered and resolved during the human-verify checkpoint that had left the app unable to start.**

## Performance

- **Duration:** 45 min (implementation ~15 min + checkpoint diagnosis/fix ~30 min)
- **Started:** 2026-07-03T19:50:00Z
- **Completed:** 2026-07-03T20:28:00Z
- **Tasks:** 3/3 (2 auto/TDD + 1 human-verify checkpoint)
- **Files modified:** 10 (2 for nav gating, 8 for the DI fix)

## Accomplishments
- `_Layout.cshtml` and `_Layout.Mobile.cshtml` both gate Calendar, Shop, Manage Shop, Edit My Profile, and Players behind a confirmed-OneShot allowlist (`activeBoardType == BoardType.OneShot`), never a Campaign blocklist
- Calendar additionally hidden for anonymous (logged-out) visitors in both layouts (D-04)
- Guild Members and Quest Log remain visible regardless of board type (D-05, unchanged)
- Email Stats nav link gated to `User.IsInRole("SuperAdmin")` in both layouts
- All 16 `LayoutNavigationTests` (added RED by Plan 37-01) now pass — desktop and mobile, OneShot/Campaign/anonymous
- Human-verify checkpoint approved by the user across all six nav-visibility checks (desktop + mobile, OneShot/Campaign/anonymous/Admin/SuperAdmin)
- **Found and fixed a circular DI dependency** that surfaced only at the checkpoint: the app couldn't start at all under the merged Wave 1 + Task 1/2 code

## Task Commits

Each task was committed atomically:

1. **Task 1: Gate _Layout.cshtml (desktop)** - `f7a31fa` (feat)
2. **Task 2: Gate _Layout.Mobile.cshtml** - `c886f6b` (feat)
3. **Task 3: Human-verify checkpoint** - approved by user; no code changes of its own
4. **Deviation fix: break circular DI dependency** - `9f83d28` (fix)

## Files Created/Modified
- `QuestBoard.Service/Views/Shared/_Layout.cshtml` - OneShot allowlist gating, D-04 Calendar wrap, SuperAdmin Email Stats link
- `QuestBoard.Service/Views/Shared/_Layout.Mobile.cshtml` - Same gating, mirrored for mobile offcanvas nav
- `QuestBoard.Domain/Interfaces/IBoardTypeResolver.cs` *(new)* - `GetBoardTypeAsync` moved here from `IActiveGroupContext`
- `QuestBoard.Service/Services/BoardTypeResolver.cs` *(new)* - Implementation depending on `IActiveGroupContext` + `IGroupService`
- `QuestBoard.Domain/Interfaces/IActiveGroupContext.cs` - Reverted to just `ActiveGroupId` (pre-Plan-37-01 shape)
- `QuestBoard.Service/Services/ActiveGroupContextService.cs` - Reverted to depending on only `IHttpContextAccessor`
- `QuestBoard.Service/Program.cs` - Registered `IBoardTypeResolver` → `BoardTypeResolver`
- `QuestBoard.IntegrationTests/Helpers/MutableGroupContext.cs` - Now implements both `IActiveGroupContext` and `IBoardTypeResolver`
- `QuestBoard.IntegrationTests/WebApplicationFactoryBase.cs` - Registers `IBoardTypeResolver` singleton alongside `IActiveGroupContext`
- `QuestBoard.UnitTests/Services/SessionReminderJobTests.cs` - Reverted `ActiveGroupContextService` constructor call to single-argument form

## Decisions Made
- Kept the allowlist polarity (`== BoardType.OneShot`) strictly, never introducing a `!= BoardType.Campaign` blocklist form, per D-01 and the plan's explicit acceptance criteria
- Computed `activeBoardType` exactly once per layout render (not per gated item) to avoid redundant `GetBoardTypeAsync` calls
- Chose to split `IBoardTypeResolver` off `IActiveGroupContext` (rather than, e.g., having `BoardTypeResolver` query the database directly, bypassing `IGroupService`) to keep reusing the existing repository/service layer and its tenant-scoping conventions, while still breaking the cycle

## Deviations from Plan

### Auto-fixed Issues

**1. [Blocking — discovered at Task 3 checkpoint] Circular DI dependency prevented the app from starting**
- **Found during:** Task 3 (human-verify checkpoint) — the user reported the app wasn't starting in Visual Studio
- **Issue:** Plan 37-01 had added `IGroupService` as a constructor dependency of `ActiveGroupContextService` (the concrete `IActiveGroupContext` implementation). `QuestBoardContext`'s own constructor depends on `IActiveGroupContext`, and `IGroupService`'s repository chain depends back on `QuestBoardContext` — a genuine cycle: `QuestBoardContext → IActiveGroupContext → ActiveGroupContextService → IGroupService → GroupRepository → QuestBoardContext`. Because `IActiveGroupContext` is registered via a factory delegate (`sp => sp.GetRequiredService<ActiveGroupContextService>()`), .NET's build-time cycle detector never saw it — the app just recursed silently at runtime (via `SeedShopDataAsync` resolving `IGroupService` at startup) and never reached "Now listening"
- **Fix:** Introduced `IBoardTypeResolver` as a new, separate interface/service for the BoardType lookup (depends on `IActiveGroupContext` + `IGroupService`), and reverted `ActiveGroupContextService` back to depending on only `IHttpContextAccessor`. `_Layout.cshtml`/`_Layout.Mobile.cshtml` now `@inject IBoardTypeResolver` instead of calling `GetBoardTypeAsync()` on `IActiveGroupContext`. Test double `MutableGroupContext` implements both interfaces so no test code needed to change
- **Files modified:** `IActiveGroupContext.cs`, `IBoardTypeResolver.cs` (new), `ActiveGroupContextService.cs`, `BoardTypeResolver.cs` (new), `Program.cs`, `MutableGroupContext.cs`, `WebApplicationFactoryBase.cs`, `_Layout.cshtml`, `_Layout.Mobile.cshtml`, `SessionReminderJobTests.cs`
- **Verification:** `dotnet build` clean (0 errors/warnings); full `dotnet test` run 383/383 passing (123 unit + 260 integration, including all 16 `LayoutNavigationTests`); app confirmed starting and serving pages (anonymous landing + login) via live browser check; user approved the full checkpoint after retrying in Visual Studio
- **Committed in:** `9f83d28`

---

**Total deviations:** 1 auto-fixed (1 blocking)
**Impact on plan:** Essential fix — the phase could not have shipped without it. No scope creep; the fix is scoped exactly to the DI graph the plan's own change had broken.

## Issues Encountered

The human-verify checkpoint caught a real production-blocking bug that automated per-plan verification (build + targeted test filters, run separately for 37-01 and 37-02) had not surfaced, because neither plan's own test run exercised the app's actual startup path (`SeedShopDataAsync` in `Program.cs`) with both changes combined. This is exactly the scenario `checkpoint:human-verify` gates exist to catch.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- Phase 37 goal fully met: campaign groups show only relevant nav items, Email Stats is SuperAdmin-only with real server-side enforcement, and the app starts and runs correctly
- Any future service needing the active group's BoardType outside a controller action should depend on `IBoardTypeResolver`, not extend `ActiveGroupContextService`
- No blockers for phase completion

---
*Phase: 37-navigation-access-control*
*Completed: 2026-07-03*

## Self-Check: PASSED

- FOUND: QuestBoard.Service/Views/Shared/_Layout.cshtml
- FOUND: QuestBoard.Service/Views/Shared/_Layout.Mobile.cshtml
- FOUND: QuestBoard.Domain/Interfaces/IBoardTypeResolver.cs
- FOUND: QuestBoard.Service/Services/BoardTypeResolver.cs
- FOUND: commit f7a31fa (Task 1)
- FOUND: commit c886f6b (Task 2)
- FOUND: commit 9f83d28 (deviation fix)
- FOUND: dotnet test — 383/383 passing
