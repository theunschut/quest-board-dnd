---
phase: 93-per-board-page-selection-and-home-page
plan: 03
subsystem: ui
tags: [razor, layout, navigation, aspnet-mvc, board-settings]

requires:
  - phase: 93-01
    provides: BoardPage enum, BoardPagePolicy, IBoardPageResolver, BoardPageRoutes, BoardPageSeed
provides:
  - BoardPageAvailabilityExtensions.Allows and .Serves (null-safe view and controller helpers)
  - IBoardPageResolver injected into every view as BoardPages, QuestBoard.Service.Helpers imported
  - Desktop and mobile layouts gated by the active board's availability, brand link to the board's home, conditional Quests entry
  - My Agenda's Board Availability button (both twins) following the Events switch
affects: [93-07, 93-08, 93-10, 93-13]

actuals:
  tokens: 30000
  tasks: 2
  commits: 4
plan_head_before: ea940b94de16c837b9aa72134023388d4f4b72c2

tech-stack:
  added: []
  patterns:
    - "Allows (needs a resolved board) versus Serves (unresolved passes) keeps each call site's pre-existing behaviour for the unresolved state"
    - "Layouts read the resolver once into a local named pages and derive the brand route from BoardPageRoutes.HomeRoute"
    - "A dropdown whose items are all hidden is not rendered at all"

key-files:
  created:
    - QuestBoard.Service/Extensions/BoardPageAvailabilityExtensions.cs
    - QuestBoard.UnitTests/Services/BoardPageAvailabilityExtensionsTests.cs
    - QuestBoard.IntegrationTests/Controllers/BoardPageNavigationTests.cs
  modified:
    - QuestBoard.Service/Views/_ViewImports.cshtml
    - QuestBoard.Service/Views/Shared/_Layout.cshtml
    - QuestBoard.Service/Views/Shared/_Layout.Mobile.cshtml
    - QuestBoard.Service/Views/Agenda/Index.cshtml
    - QuestBoard.Service/Views/Agenda/Index.Mobile.cshtml

key-decisions:
  - "Items that were board-type gated use Allows and items with no check use Serves, so the unresolved state renders exactly as before"
  - "The Quests entry carries id nav-quests (desktop) and nav-quests-mobile (mobile) and is first in the main menu"
  - "The injected resolver is named BoardPages because Pages collides with the QuestBoard.Service.Pages namespace in _ViewImports"
  - "Both layouts no longer inject IBoardTypeResolver; the resolver's availability carries the board type"

patterns-established:
  - "Navigation tests look only at the markup before the main element so page-body links cannot pass for menu entries"
  - "Test fixture InitializeAsync clears the store so seeding page settings always finds the default board"

requirements-completed: [D-07, D-11, D-18, D-21]

coverage:
  - id: D1
    description: "Allows is false and Serves is true when no board resolved; a switched-off page and the Campaign ceiling are neither allowed nor served"
    requirement: "D-18"
    verification:
      - kind: unit
        ref: "QuestBoard.UnitTests/Services/BoardPageAvailabilityExtensionsTests.cs"
        status: pass
    human_judgment: false
  - id: D2
    description: "Switched-off pages' menu items are not rendered on either layout, and the DM dropdown disappears when all its items are hidden"
    requirement: "D-07"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/BoardPageNavigationTests.cs#SwitchedOffPagesWithCalendarHome_AreHiddenQuestsEntryAppearsAndBrandGoesToCalendar"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/BoardPageNavigationTests.cs#DungeonMasterDropdown_WhenEveryItemIsHidden_IsNotRendered"
        status: pass
    human_judgment: false
  - id: D3
    description: "My Agenda's Board Availability button follows the Events switch on both user agents and still shows with no resolved board type"
    requirement: "D-07"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/BoardPageNavigationTests.cs#Agenda_WithEventsOff_HidesBoardAvailabilityButton"
        status: pass
    human_judgment: false
  - id: D4
    description: "Unconfigured boards and the unresolved state render the menus they rendered before; existing LayoutNavigationTests pass unchanged"
    requirement: "D-18"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/BoardPageNavigationTests.cs#Unconfigured_OneShotPlayer_SeesEveryPageAndBrandLinksToQuestList"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/LayoutNavigationTests.cs"
        status: pass
    human_judgment: false
  - id: D5
    description: "The authenticated brand link points at the board's home route (/quests unconfigured, /Calendar when home is Calendar); the signed-out brand is unchanged"
    requirement: "D-11"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/BoardPageNavigationTests.cs#Anonymous_BrandLinksToHome"
        status: pass
    human_judgment: false
  - id: D6
    description: "A Quests menu entry appears on both layouts only when the board's effective home is not the quest list and Quests is available"
    requirement: "D-21"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/BoardPageNavigationTests.cs#SwitchedOffPagesWithCalendarHome_AreHiddenQuestsEntryAppearsAndBrandGoesToCalendar"
        status: pass
    human_judgment: false

duration: 25min
completed: 2026-10-21
status: complete
---

# Phase 93 Plan 03: Navigation follows the board's pages Summary

**Both layouts and My Agenda now ask a null-safe `Allows`/`Serves` pair on the injected board resolver which pages to show, the authenticated brand link goes to the board's home route, and a Quests entry appears when the home is elsewhere, with unconfigured boards and the unresolved state rendering exactly as before.**

## Performance

- **Duration:** about 25 min
- **Completed:** 2026-10-21
- **Tasks:** 2
- **Files modified:** 9 (3 created, 5 modified view or helper files, plus tests counted above)

## Accomplishments
- `BoardPageAvailabilityExtensions.Allows` (resolved and offered) and `.Serves` (unresolved passes, mirroring the gate filter), pinned by 22 unit cases.
- `_ViewImports` injects `IBoardPageResolver` as `BoardPages` and imports `QuestBoard.Service.Helpers`.
- Desktop and mobile layouts: Quest Management, Create Quest, Create Event, Board Availability, Manage Shop, Edit My Profile, Shop, Quest Log, Characters, Contacts, Players and Calendar each follow their page; the desktop DM dropdown is skipped when none of its five items would render.
- Brand link built from `BoardPageRoutes.HomeRoute`; "Quests" item (`nav-quests` / `nav-quests-mobile`) rendered only when the effective home is another page and Quests is available.
- My Agenda's Board Availability button (desktop and mobile twins) follows the Events switch; a DM with no resolved board still sees it.
- `BoardPageNavigationTests` covers every behaviour on both user agents (24 cases).

## Task Commits

1. **Task 1: Open the resolver to every view with null-safe helpers**
   - RED: `ca17be2d` (test) - failing assertions against a stub that always answered false
   - GREEN: `39c8c49e` (feat)
2. **Task 2: Layouts and My Agenda show only the active board's pages**
   - `ca5ee6fd` (test) and `4ce8a095` (feat)

**Plan metadata:** the docs commit carrying this file.

## Files Created/Modified
- `QuestBoard.Service/Extensions/BoardPageAvailabilityExtensions.cs` - Allows and Serves helpers
- `QuestBoard.Service/Views/_ViewImports.cshtml` - `BoardPages` injection and Helpers using
- `QuestBoard.Service/Views/Shared/_Layout.cshtml` and `_Layout.Mobile.cshtml` - gated menus, brand link, Quests entry
- `QuestBoard.Service/Views/Agenda/Index.cshtml` and `Index.Mobile.cshtml` - Board Availability button follows Events
- `QuestBoard.UnitTests/Services/BoardPageAvailabilityExtensionsTests.cs` - helper semantics
- `QuestBoard.IntegrationTests/Controllers/BoardPageNavigationTests.cs` - menu, brand and Agenda button proofs

## Decisions Made
Followed the plan's recorded choices. The only addition: the layouts drop their `IBoardTypeResolver` injection since `pages` now carries the board type.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Navigation tests needed a fresh store per test**
- **Found during:** Task 2
- **Issue:** `BoardPageSeed.SetAsync` calls `Single` on group 1; without a cleared store the first run threw "Sequence contains no elements".
- **Fix:** the test class clears and re-seeds the default board in `InitializeAsync`, as `BoardPageGateTests` does per test.
- **Files modified:** `QuestBoard.IntegrationTests/Controllers/BoardPageNavigationTests.cs`
- **Committed in:** `ca5ee6fd`

---

**Total deviations:** 1 auto-fixed (1 Rule 3)
**Impact on plan:** none on scope.

## TDD Gate Compliance

Task 1 followed RED then GREEN (`ca17be2d` then `39c8c49e`; the RED failures were assertion failures on the target tests).

Task 2 does not show a RED run. The view edits were already in the working tree when the navigation tests were written, and reverting them to demonstrate a failing run was refused by the auto-mode permission gate (a checkout of the modified view files would have discarded uncommitted work), so no workaround was attempted. As a substitute the tests were mutation-checked: temporarily forcing the mobile Contacts condition to true made `SwitchedOffPagesWithCalendarHome_AreHiddenQuestsEntryAppearsAndBrandGoesToCalendar` fail on the mobile user agent, and the change was then reverted. The `test(93-03)` commit precedes the `feat(93-03)` commit in history, but the tests were first run against the finished views.

## Issues Encountered
- `QuestBoard.UnitTests.Migrator.MigratorCliTests.Status_WhenServerIsUnreachable_ReturnsCannotConnectWithoutLeakingConnectionDetails` failed once in the full `dotnet test` run and passed when the `MigratorCliTests` group ran alone (4/4). It is the same intermittent Migrator-group flake already logged in `deferred-items.md` by the previous plan; it touches no code in this plan. Everything else in the full run was green (1128 unit, 1357 integration, 11 skipped).
- Files needing CRLF (the new C# files) were converted after writing, so no line-ending churn appears in the diff.

## Known Stubs

None.

## Threat Flags

None. Menus remain presentation only; the server gate refuses the same pages from the same resolver.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- Plans 07, 08 and 10 can use `pages.Allows` and `pages.Serves` in views and controllers against the `BoardPages` injection.
- The Calendar page's own Board Availability button is still plan 08's.

## Self-Check: PASSED

- Created files exist on disk (extension, unit tests, navigation tests).
- Commits `ca17be2d`, `39c8c49e`, `ca5ee6fd` and `4ce8a095` exist on the branch; `git rev-list --count ea940b94..HEAD` is 4 before this docs commit.
- Acceptance criteria re-run: `BoardPageAvailabilityExtensions` 22/22, `BoardPageNavigation` 24/24, plus the LayoutNavigation, MobileViews, MobileCss, CalendarButtonStyle, Agenda and StaleAvailabilityOverviewLabelGuard filter 188/188 in one run.
- No added source line carries a planning reference (grep count 0).

---
*Phase: 93-per-board-page-selection-and-home-page*
*Completed: 2026-10-21*
