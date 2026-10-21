---
phase: 93-per-board-page-selection-and-home-page
plan: 13
subsystem: ui
tags: [aspnet-mvc, redirect, board-settings, home-page, board-picker]

requires:
  - phase: 93-01
    provides: IBoardPageResolver, BoardPageRoutes.HomeRoute, BoardPageSeed test helper
provides:
  - "/ redirects a signed-in visitor to the active board's home page"
  - Board picker fallback (no return URL, and so after login) goes through Home/Index
  - Account Profile back link on both layouts targets Home/Index
  - BoardPageHomeTests covering every home candidate, fallbacks, per-board homes and return-URL precedence
affects: [93-14, 93-15]

actuals:
  tokens: 12000
  tasks: 2
  commits: 4
plan_head_before: ea940b94de16c837b9aa72134023388d4f4b72c2

tech-stack:
  added: []
  patterns:
    - "Entry points that mean 'home' either call BoardPageRoutes.HomeRoute or point at Home/Index, so they cannot disagree"

key-files:
  created:
    - QuestBoard.IntegrationTests/Controllers/BoardPageHomeTests.cs
  modified:
    - QuestBoard.Service/Controllers/QuestBoard/HomeController.cs
    - QuestBoard.Service/Controllers/GroupPickerController.cs
    - QuestBoard.Service/Views/Account/Profile.cshtml
    - QuestBoard.Service/Views/Account/Profile.Mobile.cshtml
    - QuestBoard.IntegrationTests/Middleware/CrossBoardDeepLinkMiddlewareTests.cs

key-decisions:
  - "HomeController.Index is the single place that resolves / ; the picker and Account link point at Home/Index and inherit its answer"
  - "Account back link keeps its 'Back to Quest Board' label; only its target moved"
  - "MoveController redirects stay on the list of the same kind (that list is on by construction)"
  - "Signed-out landing page and the board-less super admin picker redirect are unchanged"

patterns-established:
  - "Stored home that is switched off or disallowed for the board type falls back to the quest list via HomeRoute"

requirements-completed: [D-10, D-11, D-12]

coverage:
  - id: D1
    description: "GET / lands on the board's home page for all eight home candidates, the quest list by default, and the quest list when the stored home is off or disallowed"
    requirement: "D-10"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/BoardPageHomeTests.cs#Root_LandsOnTheStoredHomePage"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/BoardPageHomeTests.cs#Root_WhenTheStoredHomeIsSwitchedOff_FallsBackToTheQuestList"
        status: pass
    human_judgment: false
  - id: D2
    description: "Board picker and login land on / when no return URL is given, a local return URL still wins, an external one is ignored; the Account back link targets /"
    requirement: "D-11"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/BoardPageHomeTests.cs#PickingABoardWithNoReturnUrl_GoesToTheRoot"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/BoardPageHomeTests.cs#AccountProfile_BackLinkTargetsTheRoot"
        status: pass
    human_judgment: false
  - id: D3
    description: "Two boards in one linked set keep different home pages"
    requirement: "D-12"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/BoardPageHomeTests.cs#TwoBoardsInOneLinkedSet_KeepTheirOwnHomePages"
        status: pass
    human_judgment: false

duration: 25min
completed: 2026-10-21
status: complete
---

# Phase 93 Plan 13: Home entry points Summary

**`/`, the board picker (and so login) and the Account back link all land on the active board's chosen home page through the shared home route, per board, with an explicit local return URL still winning.**

## Performance

- **Tasks:** 2 of 2
- **Files:** 1 created, 5 modified

## Accomplishments

- `HomeController.Index` is async and resolves the board's availability, redirecting through `BoardPageRoutes.HomeRoute`. A signed-out visitor still gets the landing page; a board-less user is still sent to the picker by the group-session middleware before the controller runs.
- `GroupPickerController.RedirectToLocal` keeps the `Url.IsLocalUrl` check and now falls back to `Home/Index` instead of the quest list, so single-board auto-switch, `SelectGroup` and login all end on the home page.
- The Account Profile "Back to Quest Board" link targets `Home/Index` on both the desktop and mobile layout; label, classes and icon are unchanged.
- `BoardPageHomeTests` (19 cases): all eight candidates, default, switched-off home, campaign board with Shop home, board-less super admin, signed-out visitor, picker with no / local / external return URL, two linked boards with different homes, and the Account back link on a desktop and a mobile user agent.

## Task Commits

1. RED tests for both tasks - `8cddb0f2` (test)
2. Task 1: `/` lands on the board's home - `a7df7081` (feat)
3. Task 2: picker, login and Account back link - `ac246aa6` (feat)
4. Follow-on fix to an existing test - `7375d4f1` (test)

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Existing switch-back deep-link test assumed the picker lands on the quest list directly**
- **Found during:** full-suite run after Task 2
- **Issue:** `CrossBoardDeepLinkMiddlewareTests.SwitchBack_NeverReturnsToTheDeepLinkThatTriggeredTheSwitch` fetched the picker's redirect target and expected the quest list body. The target is now `/`, which itself redirects, so the body was empty.
- **Fix:** the test follows that one extra redirect hop before asserting the board's quest list. The behaviour the test protects (never returning to the triggering deep link) is unchanged.
- **Files modified:** QuestBoard.IntegrationTests/Middleware/CrossBoardDeepLinkMiddlewareTests.cs
- **Commit:** `7375d4f1`

**2. TDD ordering note:** the RED commit holds all tests for both tasks (one test file), so the Task 2 picker and Profile tests were already green when the RED run happened. The Root tests (the real behaviour change) were run and failed before the HomeController change.

**Total deviations:** 1 auto-fixed (Rule 1). **Impact:** none on scope.

## Issues Encountered

- `QuestBoard.UnitTests.Migrator.MigratorCliTests.Status_WhenServerIsUnreachable_ReturnsCannotConnectWithoutLeakingConnectionDetails` fails in this environment (expects exit code 4, gets 1). It exercises a SQL Server connection, touches nothing in this plan, and is out of scope.

## Verification

- `dotnet build` succeeds.
- Filtered runs: `BoardPageHome|HomeController|GroupSessionMiddleware|BoardPageGate` (41 passed) and `BoardPageHome|GroupPicker|GroupSessionMiddleware|CrossBoardPickerSkip|AccountController` (74 passed).
- Full `dotnet test`: integration suite green after the follow-on fix (the single failure was the deep-link test above); unit suite has only the unrelated migrator failure.
- No added source line carries a planning reference.

## Known Stubs

None.

## Threat Flags

None. The `returnUrl` open-redirect guard (`Url.IsLocalUrl`) is unchanged and tested with a local and an external value; the new fallback is a route name.

## Next Phase Readiness

Ready for 93-14 and 93-15. Home entry points all share `BoardPageRoutes.HomeRoute`.

## Self-Check: PASSED
