---
phase: 31-unauthenticated-landing-redirect
plan: 02
subsystem: ui
tags: [aspnet-core-mvc, razor, mvc-routing, navigation]

# Dependency graph
requires:
  - phase: 31-unauthenticated-landing-redirect
    provides: plan 01's auth-lockdown changes (base branch includes these)
provides:
  - Public landing page at / requiring no authentication and loading no group-scoped data
  - Authenticated quest board at /quests (QuestController.Index) with the previously-home quest-signup logic intact
  - Full in-app reference sweep pointing quest-board navigation at /quests while preserving logout/access-denied/platform-exit links to /
affects: [31-03, 31-04]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Conditional navbar brand link (authenticated → Quest/Index, anonymous → Home/Index) via @if (User.Identity?.IsAuthenticated == true)"
    - "Action-level [Authorize] on QuestController.Index (not class-level) to avoid changing auth behavior of existing Details GET action"

key-files:
  created:
    - QuestBoard.Service/Views/Quest/Index.cshtml
    - QuestBoard.Service/Views/Quest/Index.Mobile.cshtml
  modified:
    - QuestBoard.Service/Controllers/QuestBoard/HomeController.cs
    - QuestBoard.Service/Controllers/QuestBoard/QuestController.cs
    - QuestBoard.Service/Views/Home/Index.cshtml
    - QuestBoard.Service/Views/Home/Index.Mobile.cshtml
    - QuestBoard.Service/Views/Shared/_Layout.cshtml
    - QuestBoard.Service/Views/Shared/_Layout.Mobile.cshtml
    - QuestBoard.Service/Views/Quest/Create.cshtml
    - QuestBoard.Service/Views/Quest/Create.Mobile.cshtml
    - QuestBoard.Service/Views/Account/Profile.cshtml
    - QuestBoard.Service/Views/Account/Profile.Mobile.cshtml
    - QuestBoard.Service/Controllers/GroupPickerController.cs

key-decisions:
  - "QuestController.Index carries action-level [Authorize] only (no class-level [Authorize] added to QuestController) — preserves the existing anonymous-allowed behavior of QuestController.Details"
  - "HomeController.Index remains intentionally public with zero service dependencies — the constructor no longer injects IQuestService/IUserService"

patterns-established:
  - "Conditional navbar brand link (authenticated → Quest/Index, anonymous → Home/Index)"

requirements-completed: [UX-01]

# Metrics
duration: 25min
completed: 2026-07-01
---

# Phase 31 Plan 02: Split home route — public landing at / and quest board at /quests Summary

**Migrated the quest board from HomeController.Index to a new QuestController.Index at /quests, and reduced HomeController.Index to a dependency-free public landing page with a Log In button.**

## Performance

- **Duration:** ~25 min
- **Completed:** 2026-07-01T06:21:19Z
- **Tasks:** 3 completed
- **Files modified:** 13 (2 new, 11 modified)

## Accomplishments
- New `QuestController.Index` action at `/quests`, decorated `[HttpGet] [Route("quests")] [Authorize]` (action-level only), with the quest-signup-status logic migrated verbatim from the old `HomeController.Index`
- `HomeController.Index` reduced to `[HttpGet] public IActionResult Index() => View();` — no service dependencies, no group-scoped data, intentionally public (no `[Authorize]`)
- Quest board views (`Views/Home/Index.cshtml`, `Index.Mobile.cshtml`) migrated to `Views/Quest/Index.cshtml`, `Index.Mobile.cshtml` — functionally verbatim (mobile view byte-identical; desktop view differs only in two trailing-whitespace characters on blank comment lines)
- New public landing views created in `Views/Home/` — centered modern-card (desktop) / centered stack (mobile) with app name, tagline, and a Log In button targeting `Account/Login`
- Full in-app reference sweep: navbar brand (both layouts), Quest Create Cancel buttons (both variants), Account Profile back-to-board buttons (both variants), and `GroupPickerController.RedirectToLocal` fallback all now target `/quests`; the four intentional public-landing links (`AccountController.Logout`, `AccountController.RedirectToLocal`, `AccessDenied.cshtml`, Platform layout exit link) were verified unchanged

## Task Commits

Each task was committed atomically:

1. **Task 1: Add QuestController.Index at /quests and simplify HomeController.Index to a public landing** - `0705286` (feat)
2. **Task 2: Migrate quest board views to Views/Quest and create new landing-page views in Views/Home** - `3963ec9` (feat)
3. **Task 3: Sweep all in-app references — point quest-board links to /quests, keep landing links at /** - `7b0ac54` (feat)

**Plan metadata:** committed alongside this SUMMARY.md (worktree mode — see final commit)

## Files Created/Modified
- `QuestBoard.Service/Controllers/QuestBoard/QuestController.cs` - Added `Index` action serving `/quests`; `Create` POST redirect changed to `RedirectToAction("Index")`
- `QuestBoard.Service/Controllers/QuestBoard/HomeController.cs` - Replaced entirely: dependency-free `Index() => View()`, no `[Authorize]`
- `QuestBoard.Service/Views/Quest/Index.cshtml` - NEW: migrated quest board view (poster cards), desktop
- `QuestBoard.Service/Views/Quest/Index.Mobile.cshtml` - NEW: migrated quest board view, mobile (byte-identical to prior Home/Index.Mobile.cshtml)
- `QuestBoard.Service/Views/Home/Index.cshtml` - Replaced: public landing page (modern-card, app name, tagline, Log In button)
- `QuestBoard.Service/Views/Home/Index.Mobile.cshtml` - Replaced: public landing page, mobile
- `QuestBoard.Service/Views/Shared/_Layout.cshtml` - Navbar brand now conditional (Quest/Index when authenticated, Home/Index otherwise)
- `QuestBoard.Service/Views/Shared/_Layout.Mobile.cshtml` - Same conditional navbar brand pattern, mobile
- `QuestBoard.Service/Views/Quest/Create.cshtml` - Cancel button now targets `Url.Action("Index", "Quest")`
- `QuestBoard.Service/Views/Quest/Create.Mobile.cshtml` - Same Cancel button change, mobile
- `QuestBoard.Service/Views/Account/Profile.cshtml` - Back-to-board button now targets `Quest/Index`
- `QuestBoard.Service/Views/Account/Profile.Mobile.cshtml` - Same back-to-board button change, mobile
- `QuestBoard.Service/Controllers/GroupPickerController.cs` - `RedirectToLocal` fallback now targets `Quest/Index` (D-07)

## Decisions Made
- Action-level `[Authorize]` on `QuestController.Index` only (no class-level attribute added to `QuestController`) — a class-level `[Authorize]` would have changed the auth behavior of the existing anonymous-allowed `Details` GET action, which was out of scope for this plan (per RESEARCH.md Open Question 2)
- Kept the `if (userEntity != null)` null-guard from the old `HomeController.Index` body even though the outer `IsAuthenticated` guard was removed — the action-level `[Authorize]` attribute already guarantees authentication, but the null-guard remains a defensive check against a null user lookup

## Deviations from Plan

None - plan executed exactly as written. The migrated `Views/Quest/Index.cshtml` differs from the original `Views/Home/Index.cshtml` only in two trailing-whitespace characters on otherwise-blank comment lines (introduced by the file-write tool trimming trailing whitespace) — no functional or rendering difference; `Views/Quest/Index.Mobile.cshtml` is byte-identical to its source.

## Issues Encountered
- Fresh worktree required a `dotnet restore` before the first `dotnet build --no-restore` would succeed (NETSDK1004: missing `project.assets.json`). This is expected worktree setup, not a plan deviation — resolved with `dotnet restore` at the solution root before proceeding.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- `/` now serves a public landing page; `/quests` serves the authenticated quest board with the previously-home logic intact
- Every quest-board navigation reference in-app resolves to `/quests`; logout/access-denied/platform-exit links still resolve to `/`
- The solution builds with zero errors
- Behavioral/integration-test verification (`GET /` → 200 landing page, `GET /quests` → board for authenticated user) is deferred to plan 31-04 per the plan's `<verification>` section — this plan covers the code changes only, not the automated test suite

---
*Phase: 31-unauthenticated-landing-redirect*
*Completed: 2026-07-01*

## Self-Check: PASSED

All 13 modified/created source files and the SUMMARY.md verified present on disk. All 3 task commits (`0705286`, `3963ec9`, `7b0ac54`) verified present in git log.
