---
phase: 30-group-ux-admin-user-creation
plan: 04
subsystem: ui-navigation
tags: [aspnet-core-mvc, razor, session, navigation, multi-tenancy]

# Dependency graph
requires:
  - phase: 30-group-ux-admin-user-creation
    plan: "30-01"
    provides: SessionKeys.ActiveGroupName, GroupPickerController
provides:
  - Desktop nav group-switch dropdown item (_Layout.cshtml)
  - Mobile nav group-switch nav item (_Layout.Mobile.cshtml)
affects: [30-05-tests]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "IHttpContextAccessor injected directly into a layout view to read session state at render time (mirrors pattern already specified in 30-PATTERNS.md)"
    - "Inline Razor code block computes activeGroupName once, with string.IsNullOrEmpty ternary fallback to a literal label"

key-files:
  created: []
  modified:
    - QuestBoard.Service/Views/Shared/_Layout.cshtml
    - QuestBoard.Service/Views/Shared/_Layout.Mobile.cshtml

key-decisions:
  - "Used SessionKeys.ActiveGroupName constant reference (not a literal string) in both layouts, importing QuestBoard.Service.Constants — keeps the read in sync with the writer in GroupPickerController from plan 30-01"

requirements-completed: [UX-05]

# Metrics
duration: 10min
completed: 2026-06-30
status: complete
---

# Phase 30 Plan 04: Nav Group Switch Summary

**Group-switch dropdown/nav item added to both desktop and mobile layouts, reading ActiveGroupName from session and linking to GroupPickerController/Index with a "Switch Group" fallback label**

## Performance

- **Duration:** 10 min
- **Tasks:** 2
- **Files modified:** 2

## Accomplishments
- `_Layout.cshtml` now injects `IHttpContextAccessor`, reads `SessionKeys.ActiveGroupName` from session, and renders a dropdown item (between Profile and Logout) showing the active group name with a `fa-arrows-rotate` icon, linking to `GroupPickerController.Index`
- `_Layout.Mobile.cshtml` mirrors the same pattern in the offcanvas nav, between the Profile nav-link and Logout
- Both layouts fall back to the literal label "Switch Group" when no active group name is present in session (e.g. SuperAdmin who hasn't picked a group, or a fresh session)
- Closes out UX-05 — the active group is now visible in both desktop and mobile navigation, with a one-click path back to the group picker (D-15)

## Task Commits

Each task was committed atomically:

1. **Task 1: Add group-switch item to desktop _Layout.cshtml user dropdown** - `676614f` (feat)
2. **Task 2: Add group-switch item to mobile _Layout.Mobile.cshtml nav** - `dd4ab04` (feat)

## Files Created/Modified
- `QuestBoard.Service/Views/Shared/_Layout.cshtml` - added `@using QuestBoard.Service.Constants` + `@inject IHttpContextAccessor`; added group-switch `<li>` dropdown item between Profile and the divider preceding Logout
- `QuestBoard.Service/Views/Shared/_Layout.Mobile.cshtml` - added same inject/using; added group-switch `<li class="nav-item">` between the Profile nav-link and Logout form in the offcanvas authenticated branch

## Decisions Made
- Followed 30-PATTERNS.md exactly: constant reference via `SessionKeys.ActiveGroupName` rather than a hardcoded `"ActiveGroupName"` string literal, so the session key stays in sync with the writer (`GroupPickerController.SelectGroup`/`Index` from plan 30-01)
- Kept the existing divider/Logout `<li>` structure untouched in both layouts — new item inserted purely additively

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

None specific to this plan. As documented in the plan's `<known_issue>` block, `dotnet test QuestBoard.IntegrationTests` shows 5 pre-existing failures unrelated to this plan's changes:
- 4 Register-related tests (`AccountControllerIntegrationTests.Register_*`, `MobileViewsTests.MobileAccountRegister_*`) — failing by design since plan 30-02 removed public self-registration; to be fixed in plan 30-05
- 1 `GroupManagementIntegrationTests.AddMember_ValidUserAndGroup_ShouldAddUserGroupsRow` — pre-existing failure from Phase 29's Platform GroupController, documented in `deferred-items.md`

All 159 other tests pass, confirming the layout changes are additive and introduce no new failures. The plan's verification command referenced `QuestBoard.sln`; the actual solution file is `QuestBoard.slnx` (same pre-existing naming difference noted in plan 30-01's summary) — used `QuestBoard.slnx` for both build and test runs.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- UX-05 is complete; all four UX requirements from this phase (UX-01 through UX-05) are now implemented across plans 30-01 and 30-04
- Plan 30-05 (tests) can now write/update integration tests asserting the nav group-switch item renders correctly and fix the 4 Register-related test failures left over from plan 30-02
- No blockers for plan 30-05

---
*Phase: 30-group-ux-admin-user-creation*
*Completed: 2026-06-30*

## Self-Check: PASSED

Both modified files verified present on disk; both commit hashes (676614f, dd4ab04) verified present in git log.
