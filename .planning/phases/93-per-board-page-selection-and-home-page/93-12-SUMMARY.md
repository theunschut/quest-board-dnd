---
phase: 93-per-board-page-selection-and-home-page
plan: 12
subsystem: ui
tags: [aspnet-mvc, resource-filter, board-settings, reflection-tests, integration-tests]

requires:
  - phase: 93-01
    provides: BoardPageGateFilter with [BoardPage] and [BoardPageExempt], BoardPageRoutes, BoardPageSeed
  - phase: 93-02
    provides: BoardPageGateMatrixTests and the gate on the quest, calendar, event and move controllers
provides:
  - "[BoardPage] on ShopManagement, Players, Characters, Contacts and ContactCategoryManagement controllers (class level)"
  - "[BoardPage(DungeonMasterProfile)] on DM Profile and both EditProfile actions"
  - "[BoardPageExempt] on the four picture endpoints (Characters x2, DungeonMaster x2)"
  - BoardPageCoverageTests, the closed list that fails the build for any unclassified controller action
affects: [93-13, 93-14]

actuals:
  tokens: 4200
  tasks: 2
  commits: 4
plan_head_before: 7c452094929a9d1e64472f83d498955cba48d94e

tech-stack:
  added: []
  patterns:
    - "Closed-list reflection test: every public action is gated, exempt, or on an explicit always-on controller list"
    - "Exemptions are marked on the action with [BoardPageExempt] rather than listed test-side"
    - "A refused POST is proven by taking the antiforgery token while the page is on, switching it off, then posting and counting rows with query filters ignored"

key-files:
  created:
    - QuestBoard.IntegrationTests/Security/BoardPageCoverageTests.cs
  modified:
    - QuestBoard.Service/Controllers/Shop/ShopManagementController.cs
    - QuestBoard.Service/Controllers/QuestBoard/PlayersController.cs
    - QuestBoard.Service/Controllers/DungeonMaster/DungeonMasterController.cs
    - QuestBoard.Service/Controllers/Characters/CharactersController.cs
    - QuestBoard.Service/Controllers/Contacts/ContactsController.cs
    - QuestBoard.Service/Controllers/Contacts/ContactCategoryManagementController.cs
    - QuestBoard.IntegrationTests/Controllers/BoardPageGateMatrixTests.cs

key-decisions:
  - "MoveController needs no always-on entry: all four of its actions carry their own page, so the coverage test sees it as gated"
  - "The coverage test pins both the class-level and the action-level sets exactly, so adding a gate anywhere is a deliberate edit to the test as well"
  - "Shop item rows are counted with query filters ignored, so the 'writes nothing' proof cannot pass because the filter hid the row"

patterns-established:
  - "BoardPageCoverageTests.EveryAction_IsGatedExemptOrOnAnAlwaysOnController names the offending controller and action and says which marker to add"
  - "Route invariant: every home candidate's RouteFor controller carries the same class-level page and has the routed action"

requirements-completed: [D-01, D-02, D-05, D-06, D-07]

coverage:
  - id: D1
    description: "On a Campaign board GET /ShopManagement, /Players, /DungeonMaster/Profile/{id} and /DungeonMaster/EditProfile redirect to the quest list with the page's toast; on a OneShot board they answer 200"
    requirement: "D-01"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/BoardPageGateMatrixTests.cs#OneShotOnlyPages_OnCampaignBoard_RedirectToQuestListWithToast"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/BoardPageGateMatrixTests.cs#OneShotOnlyPages_OnOneShotBoard_AreServed"
        status: pass
    human_judgment: false
  - id: D2
    description: "With Players switched off a Player, DM, Admin and SuperAdmin are all redirected to the quest list; with the DM profile switched off Profile and EditProfile are refused"
    requirement: "D-06"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/BoardPageGateMatrixTests.cs#Players_Off_RedirectsEveryRoleToQuestListWithToast"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/BoardPageGateMatrixTests.cs#DungeonMasterProfile_Off_RedirectsToQuestListWithToast"
        status: pass
    human_judgment: false
  - id: D3
    description: "A POST to /ShopManagement/Create with Shop off redirects home and adds no shop item; the same POST with Shop on adds one"
    requirement: "D-05"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/BoardPageGateMatrixTests.cs#ShopManagementCreate_PostWithShopOff_RedirectsHomeAndWritesNothing"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/BoardPageGateMatrixTests.cs#ShopManagementCreate_PostWithShopOn_AddsTheItem"
        status: pass
    human_judgment: false
  - id: D4
    description: "Characters and Contacts (with category management) are refused when switched off and served with defaults"
    requirement: "D-02"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/BoardPageGateMatrixTests.cs#Characters_Off_RedirectsToQuestListWithToast"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/BoardPageGateMatrixTests.cs#Contacts_Off_RedirectsToQuestListWithToast"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/BoardPageGateMatrixTests.cs#CharactersAndContacts_WithDefaultSettings_AreServed"
        status: pass
    human_judgment: false
  - id: D5
    description: "The Characters and DM picture endpoints are served with their page switched off"
    requirement: "D-07"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/BoardPageGateMatrixTests.cs#CharacterPictures_WhenCharactersOff_AreStillServed"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/BoardPageGateMatrixTests.cs#DungeonMasterPictures_WhenProfilePageOff_AreStillServed"
        status: pass
    human_judgment: false
  - id: D6
    description: "Every controller action is gated, exempt or on the always-on list; the page map, action-level pages, exempt set and home-candidate routes are pinned"
    requirement: "D-02"
    verification:
      - kind: unit
        ref: "QuestBoard.IntegrationTests/Security/BoardPageCoverageTests.cs#EveryAction_IsGatedExemptOrOnAnAlwaysOnController"
        status: pass
      - kind: unit
        ref: "QuestBoard.IntegrationTests/Security/BoardPageCoverageTests.cs#EveryGatedController_CarriesExactlyItsPage"
        status: pass
      - kind: unit
        ref: "QuestBoard.IntegrationTests/Security/BoardPageCoverageTests.cs#ActionLevelPages_AreExactlyThePinnedOnes"
        status: pass
      - kind: unit
        ref: "QuestBoard.IntegrationTests/Security/BoardPageCoverageTests.cs#EveryHomeCandidate_RoutesToAControllerCarryingThatPage"
        status: pass
    human_judgment: false

duration: 25min
completed: 2026-10-21
status: complete
---

# Phase 93 Plan 12: Gate Shop management, Players, DM profile, Characters and Contacts, and close the list Summary

**The server gate now stands behind every remaining board page (Shop management, Players, DM profile, Characters, Contacts and contact categories) for every role and verb, the four portrait endpoints stay open, and a reflection test fails the build for any controller action that is not gated, exempt or on an explicit always-on list.**

## Performance

- **Duration:** about 25 min
- **Started:** 2026-10-21T12:25:00Z
- **Completed:** 2026-10-21T12:51:00Z
- **Tasks:** 2
- **Files modified:** 8 (6 controllers, 1 extended test file, 1 new test file)

## Accomplishments
- Class-level `[BoardPage]` on `ShopManagementController` (Shop), `PlayersController` (Players), `CharactersController` (Characters), `ContactsController` and `ContactCategoryManagementController` (Contacts); action-level `[BoardPage(DungeonMasterProfile)]` on `DungeonMasterController.Profile` and both `EditProfile` actions.
- `[BoardPageExempt]` on `CharactersController.GetProfilePicture`/`GetCroppedPicture` and `DungeonMasterController.GetDMProfilePicture`/`GetOriginalDMProfilePicture`, so portraits on quest pages keep loading. Contact images stay gated with Contacts.
- Closes the Campaign gap: `/Players`, `/ShopManagement` and the DM profile pages now redirect to the quest list with a toast on a Campaign board, and answer 200 on a OneShot board.
- `BoardPageGateMatrixTests` extended (the earlier 17 cases untouched) with Campaign closure, one-shot controls, the four-role Players refusal, a refused Shop POST that writes nothing (with a positive control), Characters and Contacts refusals, and picture facts.
- `BoardPageCoverageTests` (reflection only) enforces the closed list, pins the class-level and action-level page maps and the exempt set, and checks that each home candidate routes to a controller carrying its page.

## Task Commits

1. **Task 1 RED: failing gate tests for Shop management, Players and the DM profile** - `26e75ff0` (test)
2. **Task 1 GREEN: gate Shop management, Players and the DM profile** - `bc16c46d` (feat)
3. **Task 2 RED: failing coverage and gate tests for Characters and Contacts** - `54eec4bf` (test)
4. **Task 2 GREEN: gate Characters and Contacts, keep portraits open** - `4a9ae277` (feat)

**Plan metadata:** recorded in the docs commit that carries this file.

## Files Created/Modified
- `QuestBoard.Service/Controllers/Shop/ShopManagementController.cs`, `QuestBoard/PlayersController.cs`, `DungeonMaster/DungeonMasterController.cs`, `Characters/CharactersController.cs`, `Contacts/ContactsController.cs`, `Contacts/ContactCategoryManagementController.cs` - the attributes and the `QuestBoard.Service.Filters` using
- `QuestBoard.IntegrationTests/Controllers/BoardPageGateMatrixTests.cs` - HTTP proof for each newly gated page
- `QuestBoard.IntegrationTests/Security/BoardPageCoverageTests.cs` - the closed list

## Decisions Made
- `MoveController` is not on the always-on list: each of its actions carries its own page, so the coverage test treats it as gated.
- The coverage test pins both the set of class-level gated controllers and the set of action-level gated actions, so a new gate is a deliberate edit in two places.
- The Shop-off POST test takes its antiforgery token from the create form while Shop is still on, then switches the page off. Antiforgery validation runs as an authorization filter before the resource filter, so without a valid token the test would see a 400 and prove nothing about the gate.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Test fidelity] Shop item count ignores query filters**
- **Found during:** Task 1 RED run
- **Issue:** the positive control (POST with Shop on) counted 0 items through a plain scope context, because the board query filter has no active board there, so a "writes nothing" assertion could have passed for the wrong reason.
- **Fix:** `CountShopItemsAsync` uses `IgnoreQueryFilters()`.
- **Files modified:** `QuestBoard.IntegrationTests/Controllers/BoardPageGateMatrixTests.cs`
- **Verification:** the control now sees 1 row, the refusal fact sees 0.
- **Committed in:** `26e75ff0`

---

**Total deviations:** 1 auto-fixed (1 Rule 1, test-only)
**Impact on plan:** none on scope or production code.

## TDD Gate Compliance

Both tasks followed RED then GREEN with `test(93-12)` commits before the matching `feat(93-12)` commits. Task 1 RED failed on the planned behaviour (200 instead of the 302 for every Campaign and Players-off case, and the refused POST writing a row) while the one-shot controls, the Shop-on POST and the DM picture facts passed. Task 2 RED failed on exactly the Characters and Contacts violations in the closed list, the missing class-level pins and route invariant for those pages, and the four refusal facts. No refactor commit was needed.

## Issues Encountered
- Full `dotnet test` in the worktree: integration tests 1442 passed, 11 skipped, 0 failed. Unit tests 1268 passed and 1 failed, `MigratorCliTests.Status_WhenServerIsUnreachable_ReturnsCannotConnectWithoutLeakingConnectionDetails`, the known intermittent failure already logged in deferred-items.md; unrelated to this plan.

## Known Stubs

None.

## Threat Flags

None. The attributes only add refusals; redirect targets still come from `BoardPageRoutes`.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- Every board page named in the phase's gate scope is now refused by the server when switched off or disallowed by board type, and the closed list means later controllers must be classified or the build fails.
- Plans that touch these controllers' actions can rely on the gate being in place.

## Self-Check: PASSED

- `BoardPageCoverageTests.cs` exists on disk; the six controllers carry the attributes (`git diff --stat` 8 files, 449 insertions, 0 deletions).
- Commits `26e75ff0`, `bc16c46d`, `54eec4bf` and `4a9ae277` exist; `git rev-list --count 7c452094..HEAD` was 4.
- Task verification commands re-run: 82 tests (Task 1 filter) and 233 tests (Task 2 filter) passed with no failures; `dotnet build` clean.
- No added source line carries a planning reference (grep count 0 over the plan's diff).

---
*Phase: 93-per-board-page-selection-and-home-page*
*Completed: 2026-10-21*
