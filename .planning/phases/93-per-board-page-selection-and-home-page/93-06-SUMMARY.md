---
phase: 93-per-board-page-selection-and-home-page
plan: 06
subsystem: platform-ui
tags: [platform, board-settings, razor, mobile-twin, tdd]

requires:
  - phase: 93-01
    provides: BoardPagePolicy, BoardPageSet, Group.DisabledPages and Group.HomePage
  - phase: 93-04
    provides: IBoardPageSettingsService.EvaluateAsync, the save-time rules
provides:
  - Pages section (switch per switchable page, home page select) on the Platform board Edit page, desktop and mobile twin
  - Edit POST that saves name, switches and home through the settings rules and turns refusals into form errors
affects: [93-10]

actuals:
  tokens: 26000
  tasks: 2
  commits: 4
plan_head_before: 7c452094929a9d1e64472f83d498955cba48d94e

tech-stack:
  added: []
  patterns:
    - "Rows post only what is switchable: unlocked rows carry a hidden page name, the checkbox and a hidden false under a gap-free running index; locked rows are disabled with no name"
    - "Evaluate-then-save in the controller: every refusal re-renders the form from the board as stored, nothing is written"

key-files:
  created:
    - QuestBoard.Service/ViewModels/PlatformViewModels/BoardPageRowViewModel.cs
    - QuestBoard.IntegrationTests/Controllers/BoardPagePlatformTests.cs
    - QuestBoard.UnitTests/Controllers/GroupControllerEditConcurrencyTests.cs
  modified:
    - QuestBoard.Service/ViewModels/PlatformViewModels/GroupEditViewModel.cs
    - QuestBoard.Service/Areas/Platform/Controllers/GroupController.cs
    - QuestBoard.Service/Areas/Platform/Views/Group/Edit.cshtml
    - QuestBoard.Service/Areas/Platform/Views/Group/Edit.Mobile.cshtml
    - QuestBoard.Service/wwwroot/css/platform-group.mobile.css

key-decisions:
  - "Refusals render as a role=alert block of div.text-danger lines with a circle-exclamation icon (UI contract) instead of the bare ModelOnly validation summary; every error sentence is unchanged"
  - "The home select lists every candidate the board type allows, including pages currently off, so Calendar can be switched on and made home in one save"
  - "Success toast text is now 'Board settings saved.'"

patterns-established:
  - "PopulatePageSection rebuilds board type, rows, lock reasons and home choices on every render, so no display value is read from the post"

requirements-completed: [D-01, D-08, D-09, D-10, D-12, D-13, D-14, D-15, D-17, D-18]

coverage:
  - id: P1
    description: "A POST carrying only Id and Name leaves DisabledPages and HomePage as stored"
    requirement: "D-14"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/BoardPagePlatformTests.cs#Edit_PostingOnlyIdAndName_LeavesSettingsAsStored"
        status: pass
    human_judgment: false
  - id: P2
    description: "A posted switch and home page are saved on that board only and the redirect shows the success toast"
    requirement: "D-14"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/BoardPagePlatformTests.cs#Edit_PostingSwitchAndHome_SavesBothAndShowsTheSuccessToast"
        status: pass
    human_judgment: false
  - id: P3
    description: "Campaign boards ignore posted switches for Shop, Players and DM profile, and render those rows disabled with the exact reason; the home select never offers Shop or Players there"
    requirement: "D-15"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/BoardPagePlatformTests.cs#Edit_CampaignBoard_PostedSwitchesForTypeForbiddenPagesAreIgnored"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/BoardPagePlatformTests.cs#EditForm_CampaignBoard_LocksTheShopPlayersAndDmProfileRows"
        status: pass
    human_judgment: false
  - id: P4
    description: "Switching off the stored home, Events with upcoming events, or Events with running series is refused as a form error with nothing saved, and a refusal on a Campaign board re-renders the real board type"
    requirement: "D-13"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/BoardPagePlatformTests.cs#Edit_SwitchingOffTheStoredHome_IsRefusedAndNothingIsSaved"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/BoardPagePlatformTests.cs#Edit_RefusalOnACampaignBoard_RerendersTheRealBoardType"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/BoardPagePlatformTests.cs#Edit_SwitchingEventsOff_WithAnUpcomingEvent_IsRefusedAndNothingIsSaved"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/BoardPagePlatformTests.cs#Edit_SwitchingEventsOff_WithARunningSeries_IsRefusedAndNothingIsSaved"
        status: pass
    human_judgment: false
  - id: P5
    description: "The Events counts use the edited board, not the super admin's active board"
    requirement: "D-08"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/BoardPagePlatformTests.cs#Edit_EventsRefusal_CountsTheEditedBoardNotTheActiveBoard"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/BoardPagePlatformTests.cs#Edit_EditingABoardWithNoEventsOfItsOwn_IgnoresTheActiveBoardsEvents"
        status: pass
    human_judgment: false
  - id: P6
    description: "Switching Shop off and back on keeps the shop items and the Shop page lists them again; creating a board leaves DisabledPages and HomePage at 0"
    requirement: "D-09"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/BoardPagePlatformTests.cs#Edit_SwitchingShopOffAndOn_KeepsTheShopItemsAndListsThemAgain"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/BoardPagePlatformTests.cs#Create_NewBoard_StartsWithEveryPageOnAndTheQuestListAsHome"
        status: pass
    human_judgment: false
  - id: P7
    description: "The Pages section renders on both twins with eight labelled rows, Quest Log locked while Quests is off, no DM profile home option, and the rendered field names round-trip through a POST"
    requirement: "D-14"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/BoardPagePlatformTests.cs#EditForm_OneShotBoard_ShowsAPagesSectionWithEverySwitchAndAHomeSelect"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/BoardPagePlatformTests.cs#EditForm_QuestListOff_LocksTheQuestLogRow"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/BoardPagePlatformTests.cs#EditForm_NeverOffersTheDmProfileAsAHome"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/BoardPagePlatformTests.cs#EditForm_RenderedFieldNames_RoundTripThroughAPost"
        status: pass
    human_judgment: false
  - id: P8
    description: "A concurrent link change surfaces as a form error carrying the real board type, and the board's LinkedBoardSetId survives a save"
    requirement: "D-18"
    verification:
      - kind: unit
        ref: "QuestBoard.UnitTests/Controllers/GroupControllerEditConcurrencyTests.cs#Edit_WhenTheBoardIsChangedConcurrently_ReturnsTheFormWithAnErrorInsteadOfFailing"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/BoardPagePlatformTests.cs#Edit_BoardInALinkedSet_KeepsItsLinkedBoardSetId"
        status: pass
    human_judgment: false
  - id: P9
    description: "Visual fit of the switch rows on the glass card and the 44px tap target on a real phone"
    requirement: "D-14"
    verification: []
    human_judgment: true
    rationale: "Markup and CSS follow the approved contract and the rendered field names are tested, but no test asserts how the rows look or feel on a device"

duration: 25min
completed: 2026-10-21
status: complete
---

# Phase 93 Plan 06: Pages section on the Platform Edit page Summary

**A super admin switches a board's pages off and on and picks its home page on the board's own Edit page in one Save, through the same settings rules the server enforces, with refusals shown as form errors and unposted settings never touched.**

## Performance

- **Duration:** about 25 min
- **Tasks:** 2
- **Files modified:** 8 (3 created, 5 modified)

## Accomplishments
- `GroupController.Edit` POST builds a switch request from only the posted rows (undefined page values dropped, first row per page wins), asks `IBoardPageSettingsService.EvaluateAsync`, and saves only when the outcome has no errors. An Id-and-Name-only post changes nothing but the name.
- Every re-render (GET, refusal, concurrency error, duplicate name) goes through one `PopulatePageSection`, which recomputes the board's real type, row labels, lock reasons and home choices from the stored board and the displayed switches, so nothing display-related is read from the post.
- `DbUpdateConcurrencyException` from the link-column concurrency token becomes the form error "Someone else changed this board while you were editing. Reload the page and try again." instead of a 500.
- Both Edit twins carry the Pages section: one `form-switch` row per switchable page, locked rows disabled with no `name` and the reason text, a home select with no blank option, and the refusal block. The mobile twin gets a scoped `.platform-group-pages-mobile` block in `platform-group.mobile.css`.

## Task Commits

1. **Task 1 RED:** failing integration and unit tests for the Edit POST - `b1da8265` (test)
2. **Task 1 GREEN:** Edit GET/POST through the settings rules - `ae455f97` (feat)
3. **Task 2 RED:** failing render tests for both twins - `2d6d3d0e` (test)
4. **Task 2 GREEN:** Pages section on both twins plus mobile CSS - `c5f2d656` (feat)

## Files Created/Modified
- `QuestBoard.Service/ViewModels/PlatformViewModels/BoardPageRowViewModel.cs` - one switch row; `Page` and `Enabled` bound, the rest `[BindNever]`
- `QuestBoard.Service/ViewModels/PlatformViewModels/GroupEditViewModel.cs` - `Pages`, `HomePage` (nullable, absent means unchanged), `HomeChoices` (`[BindNever]`)
- `QuestBoard.Service/Areas/Platform/Controllers/GroupController.cs` - settings service dependency, Edit actions, `PopulatePageSection`
- `QuestBoard.Service/Areas/Platform/Views/Group/Edit.cshtml`, `Edit.Mobile.cshtml` - Pages section and refusal block
- `QuestBoard.Service/wwwroot/css/platform-group.mobile.css` - scoped rules for the mobile Pages section
- `QuestBoard.IntegrationTests/Controllers/BoardPagePlatformTests.cs` - 26 facts, including both user agents for the render checks
- `QuestBoard.UnitTests/Controllers/GroupControllerEditConcurrencyTests.cs` - the concurrency fact

## Decisions Made
- Followed the approved UI contract over the plan where they differ: refusals render as `div.text-danger` lines with a `fa-circle-exclamation` icon inside a `role="alert"` wrapper rather than the bare `asp-validation-summary="ModelOnly"` list, so they read as errors on the parchment card. Every error sentence and assertion is as specified. The field-level Name validation span is unchanged.
- Also took the contract's accessibility discretion: each switch has `id="page-{Page}"` with a matching label `for`, `role="switch"`, and a locked row's checkbox points at its reason through `aria-describedby`.
- Home select lists all candidates the type allows (including pages currently off); the settings service refuses an unavailable home with the "can't be the home page" error.

## Deviations from Plan

None - plan executed exactly as written, apart from the contract-driven refusal markup recorded above.

Test-wording note (not a code change): the plan's behaviour list says one series answers "End each series first." The settings service from the previous plan uses a singular sentence for one series ("End the series first.") and the plural only for two or more. The integration tests assert the singular for one series and the plural for two, matching the service.

## TDD Gate Compliance

Both tasks followed RED then GREEN. Task 1 RED: 11 of 14 integration facts failed on the planned behaviour (the other three, Id-and-Name-only, Campaign ignore and Create defaults, hold trivially before the change because the old action never touched settings) and the unit fact failed with the unhandled `DbUpdateConcurrencyException`. Task 2 RED: 10 of 24 integration facts failed on missing markup. `test(93-06)` precedes `feat(93-06)` for each task. No refactor commit was needed.

## Issues Encountered
- Full `dotnet test`: one failure, `MigratorCliTests.Status_WhenServerIsUnreachable_ReturnsCannotConnectWithoutLeakingConnectionDetails`, the known intermittent Migrator test already logged as pre-existing. Unit tests 1269 passed; integration tests 1435 passed, 11 skipped.

## Known Stubs

None.

## Threat Flags

None. No new action or route; the existing `SuperAdminOnly` policy and `[ValidateAntiForgeryToken]` are unchanged, and `AntiForgeryTokenCoverageTests` pass.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- Plan 10 flips `BoardPagePolicy.IsSwitchable` for the quest list; the Quest list row then appears here with no change, because the row list reads the policy.
- Visual check on a real phone of the mobile switch rows is the one thing left to a human (coverage item P9).

## Self-Check: PASSED

- All eight key files exist on disk; commits `b1da8265`, `ae455f97`, `2d6d3d0e` and `c5f2d656` exist; `git rev-list --count 7c452094..HEAD` was 4 before this summary.
- `dotnet build` succeeds; `BoardPagePlatform` (26), `GroupManagement`, `LinkedBoardsPlatform`, `AntiForgeryTokenCoverage`, `MobileCss` and `AmbientClockSeam` tests pass.
- No added source line carries a planning reference (grep count 0).

---
*Phase: 93-per-board-page-selection-and-home-page*
*Completed: 2026-10-21*
