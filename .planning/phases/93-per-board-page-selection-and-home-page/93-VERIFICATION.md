---
phase: 93-per-board-page-selection-and-home-page
verified: 2026-10-21T14:02:31Z
status: human_needed
score: 7/7 must-haves verified
covered_files:
  - .planning/phases/93-per-board-page-selection-and-home-page/93-01-PLAN.md
  - .planning/phases/93-per-board-page-selection-and-home-page/93-01-SUMMARY.md
  - .planning/phases/93-per-board-page-selection-and-home-page/93-02-PLAN.md
  - .planning/phases/93-per-board-page-selection-and-home-page/93-02-SUMMARY.md
  - .planning/phases/93-per-board-page-selection-and-home-page/93-03-PLAN.md
  - .planning/phases/93-per-board-page-selection-and-home-page/93-03-SUMMARY.md
  - .planning/phases/93-per-board-page-selection-and-home-page/93-04-PLAN.md
  - .planning/phases/93-per-board-page-selection-and-home-page/93-04-SUMMARY.md
  - .planning/phases/93-per-board-page-selection-and-home-page/93-05-PLAN.md
  - .planning/phases/93-per-board-page-selection-and-home-page/93-05-SUMMARY.md
  - .planning/phases/93-per-board-page-selection-and-home-page/93-06-PLAN.md
  - .planning/phases/93-per-board-page-selection-and-home-page/93-06-SUMMARY.md
  - .planning/phases/93-per-board-page-selection-and-home-page/93-07-PLAN.md
  - .planning/phases/93-per-board-page-selection-and-home-page/93-07-SUMMARY.md
  - .planning/phases/93-per-board-page-selection-and-home-page/93-08-PLAN.md
  - .planning/phases/93-per-board-page-selection-and-home-page/93-08-SUMMARY.md
  - .planning/phases/93-per-board-page-selection-and-home-page/93-09-PLAN.md
  - .planning/phases/93-per-board-page-selection-and-home-page/93-09-SUMMARY.md
  - .planning/phases/93-per-board-page-selection-and-home-page/93-10-PLAN.md
  - .planning/phases/93-per-board-page-selection-and-home-page/93-10-SUMMARY.md
  - .planning/phases/93-per-board-page-selection-and-home-page/93-11-PLAN.md
  - .planning/phases/93-per-board-page-selection-and-home-page/93-11-SUMMARY.md
  - .planning/phases/93-per-board-page-selection-and-home-page/93-12-PLAN.md
  - .planning/phases/93-per-board-page-selection-and-home-page/93-12-SUMMARY.md
  - .planning/phases/93-per-board-page-selection-and-home-page/93-13-PLAN.md
  - .planning/phases/93-per-board-page-selection-and-home-page/93-13-SUMMARY.md
  - .planning/phases/93-per-board-page-selection-and-home-page/93-14-PLAN.md
  - .planning/phases/93-per-board-page-selection-and-home-page/93-14-SUMMARY.md
  - .planning/phases/93-per-board-page-selection-and-home-page/93-15-PLAN.md
  - .planning/phases/93-per-board-page-selection-and-home-page/93-15-SUMMARY.md
  - .planning/phases/93-per-board-page-selection-and-home-page/93-CONTEXT.md
  - QuestBoard.Domain/Enums/BoardPage.cs
  - QuestBoard.Domain/Enums/MoveStatus.cs
  - QuestBoard.Domain/Extensions/BoardPagePolicy.cs
  - QuestBoard.Domain/Extensions/ServiceExtensions.cs
  - QuestBoard.Domain/Interfaces/IBoardPageResolver.cs
  - QuestBoard.Domain/Interfaces/IBoardPageSettingsService.cs
  - QuestBoard.Domain/Interfaces/IEventRepository.cs
  - QuestBoard.Domain/Interfaces/IQuestRepository.cs
  - QuestBoard.Domain/Models/Group.cs
  - QuestBoard.Domain/Models/GroupWithMemberCount.cs
  - QuestBoard.Domain/Models/LinkedBoard.cs
  - QuestBoard.Domain/Models/LinkedEventEntry.cs
  - QuestBoard.Domain/Models/LinkedQuestEntry.cs
  - QuestBoard.Domain/Services/BoardPageSettingsService.cs
  - QuestBoard.Domain/Services/EventMoveService.cs
  - QuestBoard.Domain/Services/LinkedCalendarService.cs
  - QuestBoard.Domain/Services/MoveControlService.cs
  - QuestBoard.Domain/Services/QuestMoveService.cs
  - QuestBoard.Repository/Automapper/EntityProfile.cs
  - QuestBoard.Repository/Entities/GroupEntity.cs
  - QuestBoard.Repository/EventRepository.cs
  - QuestBoard.Repository/GroupRepository.cs
  - QuestBoard.Repository/LinkedBoardSetRepository.cs
  - QuestBoard.Repository/QuestRepository.cs
  - QuestBoard.Service/Areas/Platform/Controllers/GroupController.cs
  - QuestBoard.Service/Areas/Platform/Views/Group/Edit.Mobile.cshtml
  - QuestBoard.Service/Areas/Platform/Views/Group/Edit.cshtml
  - QuestBoard.Service/Areas/Platform/Views/Group/Index.Mobile.cshtml
  - QuestBoard.Service/Areas/Platform/Views/Group/Index.cshtml
  - QuestBoard.Service/Controllers/Admin/AdminController.cs
  - QuestBoard.Service/Controllers/Characters/CharactersController.cs
  - QuestBoard.Service/Controllers/Contacts/ContactCategoryManagementController.cs
  - QuestBoard.Service/Controllers/Contacts/ContactsController.cs
  - QuestBoard.Service/Controllers/DungeonMaster/DungeonMasterController.cs
  - QuestBoard.Service/Controllers/Events/EventsController.cs
  - QuestBoard.Service/Controllers/Events/SeriesController.cs
  - QuestBoard.Service/Controllers/GroupPickerController.cs
  - QuestBoard.Service/Controllers/MoveController.cs
  - QuestBoard.Service/Controllers/QuestBoard/CalendarController.cs
  - QuestBoard.Service/Controllers/QuestBoard/HomeController.cs
  - QuestBoard.Service/Controllers/QuestBoard/PlayersController.cs
  - QuestBoard.Service/Controllers/QuestBoard/QuestController.cs
  - QuestBoard.Service/Controllers/QuestBoard/QuestLogController.cs
  - QuestBoard.Service/Controllers/Shop/ShopController.cs
  - QuestBoard.Service/Controllers/Shop/ShopManagementController.cs
  - QuestBoard.Service/Extensions/BoardPageAvailabilityExtensions.cs
  - QuestBoard.Service/Filters/BoardPageGateFilter.cs
  - QuestBoard.Service/Helpers/BoardPageRoutes.cs
  - QuestBoard.Service/Program.cs
  - QuestBoard.Service/Services/BoardPageResolver.cs
  - QuestBoard.Service/ViewModels/MoveViewModels/MovePageViewModel.cs
  - QuestBoard.Service/ViewModels/PlatformViewModels/BoardPageRowViewModel.cs
  - QuestBoard.Service/ViewModels/PlatformViewModels/GroupEditViewModel.cs
  - QuestBoard.Service/Views/Account/Profile.Mobile.cshtml
  - QuestBoard.Service/Views/Account/Profile.cshtml
  - QuestBoard.Service/Views/Agenda/Index.Mobile.cshtml
  - QuestBoard.Service/Views/Agenda/Index.cshtml
  - QuestBoard.Service/Views/Calendar/Index.Mobile.cshtml
  - QuestBoard.Service/Views/Calendar/Index.cshtml
  - QuestBoard.Service/Views/DungeonMaster/Profile.Mobile.cshtml
  - QuestBoard.Service/Views/DungeonMaster/Profile.cshtml
  - QuestBoard.Service/Views/Events/Create.cshtml
  - QuestBoard.Service/Views/Events/Edit.cshtml
  - QuestBoard.Service/Views/Move/Index.Mobile.cshtml
  - QuestBoard.Service/Views/Move/Index.cshtml
  - QuestBoard.Service/Views/Players/Index.Mobile.cshtml
  - QuestBoard.Service/Views/Players/Index.cshtml
  - QuestBoard.Service/Views/Quest/Details.Mobile.cshtml
  - QuestBoard.Service/Views/Quest/Details.cshtml
  - QuestBoard.Service/Views/Shared/_Layout.Mobile.cshtml
  - QuestBoard.Service/Views/Shared/_Layout.cshtml
  - QuestBoard.Service/Views/Shared/_LinkedAlsoThatDay.cshtml
  - QuestBoard.Service/Views/Shared/_LinkedDayEntries.cshtml
  - QuestBoard.Service/Views/_ViewImports.cshtml
  - QuestBoard.Service/wwwroot/css/dm-profile.mobile.css
  - QuestBoard.Service/wwwroot/css/platform-group.mobile.css
covered_digest: "v1:sha256:4879cd10656fc6cdf883c5f170709b8b8ec3f810f897ff62c0a9cce7f02ae0a6"
behavior_unverified: 0
overrides_applied: 0
human_verification:
  - test: "Open a board's Platform Edit page on a real phone (real mobile user agent, not devtools emulation). Flip several Pages switches, change the Home page, Save, and trigger a refusal (for example switch off the board's current home page)."
    expected: "Switch rows are comfortably tappable, locked rows show their reason legibly, the refusal message is readable on the form, nothing overflows at roughly 390px wide."
    why_human: "Mobile views are chosen by user agent and the Platform mobile views render inside the desktop Platform layout; automated tests assert markup, not touch feel or layout."
  - test: "On a phone-width real device, open the Move page for a quest or event on a board that is linked to a board with a very long name whose Quests or Events page is off."
    expected: "The picker option for the board with the page off is disabled, its suffix is readable, and the long name does not break the layout at 390px."
    why_human: "Text wrapping and truncation of long board names plus a suffix inside a native select cannot be judged from markup."
  - test: "Switch off several pages on a board (for example Shop, Contacts, Events) with Calendar as home, then browse it on a real phone and on desktop."
    expected: "Both menus show no dead entries, the Quests menu item appears, and pages that lost links (Calendar, My Agenda, Players, Quest Details) look intentional, with no empty gaps."
    why_human: "Real-device visual appearance of trimmed pages and layout gaps is a judgment call."
---

# Phase 93: Per-Board Page Selection and Home Page Verification Report

**Phase Goal:** In the Platform area, a super admin can choose which pages each board offers (Shop, Contacts, Players, Characters, Quest Log, Events, Calendar and more). A switched-off page disappears from both menus, and the server refuses it on that board when its URL is typed. A super admin can also set which page a board opens on. The default stays the quest list, and at least one board can open on `/calendar`. Boards nobody configures behave exactly as they do today.
**Verified:** 2026-10-21T14:02:31Z
**Status:** human_needed
**Re-verification:** No, initial verification

## Goal Achievement

The goal is achieved in the codebase. No truth failed. Status is `human_needed` only because the executors flagged three real-device/visual checks that cannot be proven by InMemory integration tests, and the decision tree routes any non-empty human-verification list to `human_needed`.

ROADMAP lists no numbered Success Criteria for this phase, so the must-haves below are derived from the phase goal sentence by sentence (Option C), plus the plan frontmatter truths and the D-01..D-21 decisions in 93-CONTEXT.md.

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | A super admin can choose which pages a board offers, in Platform, with one Save | VERIFIED | `Areas/Platform/Controllers/GroupController.cs` Edit GET/POST binds `Pages[]` rows and `HomePage`, calls `IBoardPageSettingsService.EvaluateAsync`, writes `DisabledPages`/`HomePage` through `groupService.UpdateAsync`. Edit.cshtml and Edit.Mobile.cshtml render a Pages section (switch rows, locked rows with reasons, Home select). Tests: `Edit_PostingSwitchAndHome_SavesBothAndShowsTheSuccessToast`, `EditForm_*` (both user agents), `Edit_SwitchingShopOffAndOn_KeepsTheShopItemsAndListsThemAgain` |
| 2 | A switched-off page disappears from both menus | VERIFIED | `_Layout.cshtml` and `_Layout.Mobile.cshtml` gate every item through `BoardPages.GetAsync()` plus `Allows`/`Serves`. The DM dropdown disappears when empty. Tests: `SwitchedOffPagesWithCalendarHome_AreHiddenQuestsEntryAppearsAndBrandGoesToCalendar` (desktop and mobile UA), `DungeonMaster_WithEventsShopAndProfileOff_*`, `DungeonMasterDropdown_WhenEveryItemIsHidden_IsNotRendered`, `Admin_WithQuestsOff_DoesNotSeeQuestManagement` |
| 3 | The server refuses a switched-off page when its URL is typed (every verb, every role, including super admin) | VERIFIED | `BoardPageGateFilter` is a global resource filter (`Program.cs:38`) reading `[BoardPage]` endpoint metadata. It redirects to the effective home with an `Info` toast, which `_Toasts.cshtml` renders. Attributes sit on every page controller (Shop, ShopManagement, Characters, Contacts, ContactCategoryManagement, Players, Calendar, QuestLog, Quest, Events, Series, DungeonMaster.Profile/EditProfile x2, Admin Quests/DeleteQuest, Move x4, `UpdateSignupCharacter` also needs Characters). Only the four picture endpoints carry `[BoardPageExempt]`. `BoardPageCoverageTests` (reflection) pins that every public action is gated, exempt or on the always-on list. Tests: `Shop_OffWithCalendarHome_RedirectsToCalendarWithToast`, `Shop_Off_IsRefusedForSuperAdminToo`, `Players_Off_RedirectsEveryRoleToQuestListWithToast`, `ShopManagementCreate_PostWithShopOff_RedirectsHomeAndWritesNothing`, `Move_WhenItsPageIsOff_IsRefusedForSuperAdmin` |
| 4 | A super admin can set the board's home page. Default stays the quest list, and a board can open on `/calendar` | VERIFIED | `HomeController.Index`, both layouts' brand link, `GroupPickerController.RedirectToLocal` (falls back to `Home/Index`) and the gate redirect all use `BoardPageRoutes.HomeRoute(availability)`, backed by `BoardPagePolicy.EffectiveHome`. Return URLs still win. Tests: `Root_LandsOnTheStoredHomePage(BoardPage.Calendar, ...)`, `Root_WithDefaultSettings_LandsOnTheQuestList`, `PickingABoardWithNoReturnUrl_GoesToTheRoot`, `PickingABoardWithALocalReturnUrl_GoesThereInsteadOfHome`, `TwoBoardsInOneLinkedSet_KeepTheirOwnHomePages`, `SuperAdmin_OpensABoardOnItsCalendarWithPagesTrimmed_ThenPutsItBackExactlyAsItWas` (desktop and mobile UA: `/` goes to `/Calendar`, brand link `/Calendar`, `/Shop` goes to `/Calendar` with the toast, restore returns to `/quests`) |
| 5 | Boards nobody configures behave exactly as today | VERIFIED | Migration `20261021112418_AddBoardPageSettings` adds two non-null int columns with default 0 (nothing off, home = Quests), `Down` is symmetric. Layout diff keeps today's items for unconfigured boards (`Quests` menu item only when home is not Quests). Tests: `Unconfigured_OneShotPlayer_SeesEveryPageAndBrandLinksToQuestList`, `Root_ThroughTheProductionBoardTypeResolver_RedirectsAnUnconfiguredBoardToTheQuestList`, `*_WithDefaultSettings_*` families, `Shop_WithNoResolvedBoardType_AnswersAsItDoesWithShopOn`, `Create_NewBoard_StartsWithEveryPageOnAndTheQuestListAsHome`. `CalendarFeedWriter.cs`, `CalendarSubscriptionService.cs`, `FeedRevisionStamper.cs` are untouched (no diff since the pre-phase commit). |
| 6 | Board type is a hard ceiling (D-01): Shop, Players and DM profile can never be on for a Campaign board, enforced on save and on serve | VERIFIED | `BoardPagePolicy.AllowedOn` is consulted by `IsAvailable` (serve) and by `BoardPageSettingsService.ApplySwitches/CanChange` (save, ignoring posted values for locked rows). Closes the old gap where `/shop` and `/players` opened on Campaign boards. Tests: `OneShotOnlyPages_OnCampaignBoard_RedirectToQuestListWithToast`, `Shop_OnCampaignBoardWithNothingStored_RedirectsToQuestListWithToast`, `Edit_CampaignBoard_PostedSwitchesForTypeForbiddenPagesAreIgnored`, `EditForm_CampaignBoard_LocksTheShopPlayersAndDmProfileRows`, `Root_OnACampaignBoardStoringShopAsHome_FallsBackToTheQuestList` |
| 7 | Switching off cannot strand content or the home page (D-08, D-13, D-19, D-20) | VERIFIED | `BoardPageSettingsService` refuses Events off with upcoming events or running series, Quests off with live quests, and any off-switch that removes the current home, all with counts in the message and nothing saved. Quest Log is coupled to Quests in `BoardPagePolicy.IsAvailable` and `LockReason` ("Needs the quest list") without changing the stored bit. Move services refuse a target with the kind off (`QuestMoveService`, `EventMoveService`, `MoveStatus.TargetPageOff`). Tests: `Edit_SwitchingEventsOff_WithAnUpcomingEvent_*`, `Edit_SwitchingQuestsOff_*`, `Edit_SwitchingOffTheStoredHome_IsRefusedAndNothingIsSaved`, `QuestsOffAndOn_KeepsTheStoredQuestLogSettingAsItWas`, `QuestConfirm_TargetWithQuestsOff_IsRefusedAndTheQuestStaysPut`, `EventConfirm_TargetWithEventsOff_*` |

**Score:** 7/7 truths verified (0 present-but-behavior-unverified)

### Decision Coverage (D-01..D-21 from 93-CONTEXT.md)

REQUIREMENTS.md has no entries for this phase (ROADMAP says "Requirements: TBD"); the plans' `requirements:` ids are CONTEXT decision ids. The union across the 15 plans covers D-01 through D-21 with none missing and none orphaned.

| Decision | Status | Evidence |
|----------|--------|----------|
| D-01 type is a hard limit | SATISFIED | Truth 6 |
| D-02 every page has a switch, subpages follow their page | SATISFIED | `BoardPagePolicy.Catalogue` lists 9 pages; Shop/ShopManagement, Contacts/ContactCategoryManagement, Events/Series, DM Profile/EditProfile gated; `BoardPageCoverageTests.EveryGatedController_CarriesExactlyItsPage` |
| D-03 quest list switchable (research verdict) | SATISFIED | Quests row exists on the form (`EditForm_ShowsAQuestListSwitchRow`) |
| D-04 Quests off means gone entirely | SATISFIED | `QuestController`, `AdminController.Quests/DeleteQuest`, `MoveController.Quest/ConfirmQuest` gated; Calendar skips own quests; `QuestsOff_*` tests |
| D-05 redirect home with toast | SATISFIED | Truth 3 |
| D-06 off means off for everyone | SATISFIED | `Shop_Off_IsRefusedForSuperAdminToo`, `Players_Off_*` for every role |
| D-07 links disappear, data stays, picture endpoints open | SATISFIED | Players DM names become plain text, Characters picker/change control hidden with Characters off, Events buttons hidden on Calendar/Agenda, Quest history titles plain text, linked-calendar entries inert; `CharacterPictures_WhenCharactersOff_AreStillServed`, `DungeonMasterPictures_WhenProfilePageOff_AreStillServed`, `CharactersOff_ExistingSignupWithCharacter_KeepsNameAndPortraitButOffersNoChange`. Share-link/link-card controls are N/A until Phases 78/79 exist; the forward constraint was written into ROADMAP for those phases |
| D-08 refuse Events/Quests off with upcoming content | SATISFIED | Truth 7. Guarded feed files untouched. See WR-02 for a 24h edge |
| D-09 hide, never delete | SATISFIED | `Edit_SwitchingShopOffAndOn_KeepsTheShopItemsAndListsThemAgain`, `SwitchingQuestsBackOn_BringsEveryQuestBackUnchanged`; no delete path in the settings service |
| D-10 any all-member page can be home, DM-only cannot | SATISFIED | `HomeCandidates` excludes DM profile; `EditForm_NeverOffersTheDmProfileAsAHome`; `Root_LandsOnTheStoredHomePage` theory |
| D-11 home applies everywhere | SATISFIED with one documented deviation | `/`, brand links, GroupPicker fallback, gate redirect all use the home route. `MoveController` redirects were deliberately left pointing at the same-kind list (on by construction because the Move action is gated by that same page). Decided in research Open Question 3 and plan 93-13; the literal D-11 text names a MoveController fallback, so this is flagged as a warning below |
| D-12 per board | SATISFIED | Columns on `Groups`; `TwoBoardsInOneLinkedSet_KeepTheirOwnHomePages` |
| D-13 refuse switching off the current home | SATISFIED | `EvaluateHome`; `Edit_SwitchingOffTheStoredHome_IsRefusedAndNothingIsSaved` |
| D-14 Pages section on Edit, both layouts | SATISFIED | Edit.cshtml and Edit.Mobile.cshtml; `EditForm_*(userAgent)` |
| D-15 greyed-out locked rows with reason | SATISFIED | `LockReason`; `EditForm_CampaignBoard_LocksTheShopPlayersAndDmProfileRows` |
| D-16 "Home: X" badge only on non-default boards | SATISFIED | Index.cshtml/Index.Mobile.cshtml; `BoardList_*` tests. See IN-04 below (shows stored, not effective home) |
| D-17 Create unchanged | SATISFIED | `GroupController.Create` untouched; `Create_NewBoard_StartsWithEveryPageOnAndTheQuestListAsHome` |
| D-18 existing boards unchanged | SATISFIED | Truth 5 |
| D-19 Quest Log follows Quests | SATISFIED | Truth 7 |
| D-20 live quest = not closed and not played | SATISFIED | `QuestRepository.CountLiveForBoardAsync` implements the played-quest rule; `Edit_SwitchingQuestsOff_WithAnOpenQuestWhoseDatesAllPassed_IsRefusedAndNothingIsSaved` |
| D-21 "Quests" menu item only when home is not Quests | SATISFIED | `_Layout.cshtml:165` and the Mobile twin; end-to-end test asserts presence with Calendar home and absence on default |

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `QuestBoard.Domain/Enums/BoardPage.cs` | Page keys plus bit-set | VERIFIED | Present, stable numeric contract documented |
| `QuestBoard.Domain/Extensions/BoardPagePolicy.cs` | Single rule set | VERIFIED | Used by resolver, settings service, controller, views, routes |
| `QuestBoard.Service/Filters/BoardPageGateFilter.cs` | Server refusal | VERIFIED | Registered globally, covers all verbs |
| `QuestBoard.Service/Services/BoardPageResolver.cs` | Fresh per-request read, no fourth board-type lookup | VERIFIED | Delegates to `IBoardTypeResolver`, memo keyed on active board id |
| `QuestBoard.Service/Helpers/BoardPageRoutes.cs` | Home route and toast text | VERIFIED | Names only, never request data, so no open redirect |
| `QuestBoard.Domain/Services/BoardPageSettingsService.cs` | Save-time rules | VERIFIED | Type ceiling, home, Events/Quests guards |
| `QuestBoard.Repository/Migrations/20261021112418_AddBoardPageSettings*` | Columns with default 0 | VERIFIED | Snapshot updated |
| Platform Edit/Index views (desktop and mobile) | Pages section, badge | VERIFIED | Tests per user agent |
| Both layouts | Menu gating, brand, Quests item | VERIFIED | |

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|-----|--------|---------|
| `Program.cs` | `BoardPageGateFilter` | `options.Filters.Add<BoardPageGateFilter>()` | WIRED | line 38 |
| `Program.cs` | `BoardPageResolver` | `AddScoped<IBoardPageResolver,...>` | WIRED | line 262; `BoardPageDependencyInjectionTests` proves the production graph |
| `GroupController.Edit` | `BoardPageSettingsService` | `EvaluateAsync` | WIRED | Errors become `ModelState` errors, nothing saved on refusal |
| Layouts, HomeController, gate | `BoardPageRoutes.HomeRoute` | direct calls | WIRED | One source for home |
| Platform form rows | server rules | posted rows re-checked via `AllowedOn`/`Enum.IsDefined`; Label/Locked/BoardType are `[BindNever]` or recomputed | WIRED | |

### Data-Flow Trace (Level 4)

| Artifact | Data Variable | Source | Produces Real Data | Status |
|----------|---------------|--------|--------------------|--------|
| Layouts, gate | `BoardPageAvailability` | `GroupService.GetByIdAsync` then `GroupEntity.DisabledPages/HomePage` columns | Yes (EF read each request, no cache) | FLOWING |
| Platform Edit form | `Model.Pages`, `HomeChoices` | `BoardPagePolicy` over stored `Group` | Yes | FLOWING |
| Platform board list badge | `item.HomePage` | `GroupRepository` projection of `HomePage` | Yes | FLOWING |

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| Page-selection integration suite and cross-board deep-link tests | `dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~BoardPage\|FullyQualifiedName~CrossBoardDeepLink"` | 334 passed, 0 failed | PASS |
| Page-selection unit tests (policy, settings service, routes, concurrency, repository counts, move target, linked calendar, ambient clock seam, enum cast, group repository) | `dotnet test QuestBoard.UnitTests --filter "...BoardPage\|GroupControllerEditConcurrency\|BoardMoveTargetPageOff\|LinkedCalendar\|AmbientClockSeam\|EntityProfileEnumCast\|GroupRepository"` | 337 passed, 0 failed | PASS |
| Full-suite result reported by orchestrator | not re-run by verifier (orchestrator ran it once at HEAD) | integration 1652 passed / 11 skipped / 0 failed; unit 1296 passed / 1 failed (`MigratorBackupAndApplyCliTests...WhenServerIsUnreachable`, pre-existing, passes in isolation, logged in `deferred-items.md`) | Accepted as unrelated to this phase |

### Probe Execution

Step 7c: SKIPPED. No probe scripts are declared by the plans and no `scripts/*/tests/probe-*.sh` apply to this phase.

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|-------------|-------------|--------|----------|
| D-01..D-21 | plans 93-01..93-15 | Decisions in 93-CONTEXT.md (REQUIREMENTS.md holds no IDs for this phase) | All SATISFIED | See Decision Coverage. No orphaned IDs |

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| `Views/DungeonMaster/Profile.Mobile.cshtml` | 80, 95 | `Date TBD` | Info | User-facing fallback text that exists before this phase; not a debt marker. No `TBD/FIXME/XXX/TODO/HACK` markers and no planning or tracking references (D-xx, Phase NN, plan ids, `*.md` planning names) found in any `.cs/.cshtml/.css/.js` under the three source projects |

### Review Findings Weighed (93-REVIEW.md)

| Finding | Severity | Does it undermine a must-have? |
|---------|----------|--------------------------------|
| WR-01: Platform Edit can silently overwrite a concurrent page change, and the live-content guards are check-then-act | WARNING | No. It needs two super admins editing the same board at once, or a DM creating content inside the sub-second gap between count and save. No success criterion or decision depends on cross-admin serialisation, and a quest or event created after the page is off is refused by the gate. Worth a follow-up (post the rendered mask as a hidden version field, or make the columns a concurrency token), not a blocker. |
| WR-02: live-quest count uses the "played" cutoff, but the quest list uses a rolling 24 hours | WARNING | No. D-20 says "not closed and not played" and the code implements exactly the played-quest rule (`QuestExtensions.HasBeenPlayed`). The only effect is that a quest finalized for last evening can still appear on the list for up to 24h while Quests is switched off; its pages then redirect, which is not a dead end. The docstring over-claims, and a boundary test is missing. Follow-up, not a blocker. |
| IN-01..IN-05 | INFO | No. Dead `IsSwitchable` hook, unguarded bit shifts for undefined page values, a phase-specific static guard test, board-list badge shows the stored not the effective home, account-page "Back" button self-links when a board has no available home. Mention for cleanup. |

### Warnings (non-blocking)

1. **D-11 literal deviation (MoveController).** D-11 lists "the MoveController fallback" among places that should use the home page. The implementation (plan 93-13, research Open Question 3, resolved) keeps its redirects pointing at the same-kind list. This is safe, because the list is switched on whenever the Move action is reachable, and it satisfies the stated aim that nothing sends someone to a switched-off page. If the operator wants literal coverage, only `MoveController.NoLongerOnThisBoard` would need to point at Home.
2. **WR-01** and **WR-02** above, carried as warnings.

### Human Verification Required

### 1. Platform Edit switch rows on a real phone

**Test:** Open a board's Platform Edit page on a real phone (real mobile user agent, not devtools). Flip several Pages switches, change the Home page, Save, and provoke a refusal (switch off the current home page).
**Expected:** Rows are comfortably tappable, locked-row reasons are legible, the refusal message is readable on the form, nothing overflows at about 390px.
**Why human:** Mobile views are selected by user agent and Platform mobile views render inside the desktop Platform layout; tests assert markup only.

### 2. Move picker with a long board name plus suffix at 390px

**Test:** On a real phone, open the Move page for a quest or event whose linked set includes a board with a very long name whose Quests or Events page is off.
**Expected:** That option is disabled, its suffix reads clearly, the layout does not break.
**Why human:** Wrapping/truncation inside a native select cannot be judged from markup.

### 3. Real-device appearance of trimmed pages

**Test:** Switch off Shop, Contacts and Events on a board with Calendar as home, then browse it on a real phone and on desktop.
**Expected:** Menus carry no dead entries, a Quests item appears, and pages that lost links (Calendar, My Agenda, Players, Quest Details) look intentional with no awkward gaps.
**Why human:** Visual quality judgment.

### Gaps Summary

No gaps. Every derived must-have and every decision D-01..D-21 is satisfied in the code and backed by passing tests, including an end-to-end walk (Platform form to `/` opening on `/Calendar`, trimmed menus, refused URLs with toast, and exact restore) on both desktop and mobile user agents. The guarded calendar feed files are untouched. Remaining items are the three real-device checks above, plus the non-blocking warnings (WR-01, WR-02, D-11 Move fallback wording).

---

_Verified: 2026-10-21T14:02:31Z_
_Verifier: Claude (gsd-verifier)_
