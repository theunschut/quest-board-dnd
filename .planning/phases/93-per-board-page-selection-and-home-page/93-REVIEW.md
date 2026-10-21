---
phase: 93-per-board-page-selection-and-home-page
reviewed: 2026-10-21T00:00:00Z
depth: standard
files_reviewed: 110
files_reviewed_list:
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
  - QuestBoard.IntegrationTests/Controllers/BoardPageBoardListTests.cs
  - QuestBoard.IntegrationTests/Controllers/BoardPageCharacterLinksTests.cs
  - QuestBoard.IntegrationTests/Controllers/BoardPageEventsCalendarTests.cs
  - QuestBoard.IntegrationTests/Controllers/BoardPageGateMatrixTests.cs
  - QuestBoard.IntegrationTests/Controllers/BoardPageGateTests.cs
  - QuestBoard.IntegrationTests/Controllers/BoardPageHomeTests.cs
  - QuestBoard.IntegrationTests/Controllers/BoardPageNavigationTests.cs
  - QuestBoard.IntegrationTests/Controllers/BoardPagePlatformTests.cs
  - QuestBoard.IntegrationTests/Controllers/BoardPageQuestsOffTests.cs
  - QuestBoard.IntegrationTests/Helpers/BoardPageSeed.cs
  - QuestBoard.IntegrationTests/Helpers/ProductionResolverWebApplicationFactory.cs
  - QuestBoard.IntegrationTests/Middleware/CrossBoardDeepLinkMiddlewareTests.cs
  - QuestBoard.IntegrationTests/Security/BoardPageCoverageTests.cs
  - QuestBoard.IntegrationTests/Tests/BoardPageDependencyInjectionTests.cs
  - QuestBoard.IntegrationTests/Tests/BoardPageEndToEndTests.cs
  - QuestBoard.IntegrationTests/Tests/BoardPageLinkedCalendarTests.cs
  - QuestBoard.IntegrationTests/Tests/BoardPageMoveTests.cs
  - QuestBoard.IntegrationTests/Tests/BoardPageStaticGuardTests.cs
  - QuestBoard.Repository/Automapper/EntityProfile.cs
  - QuestBoard.Repository/Entities/GroupEntity.cs
  - QuestBoard.Repository/EventRepository.cs
  - QuestBoard.Repository/GroupRepository.cs
  - QuestBoard.Repository/LinkedBoardSetRepository.cs
  - QuestBoard.Repository/Migrations/20261021112418_AddBoardPageSettings.Designer.cs
  - QuestBoard.Repository/Migrations/20261021112418_AddBoardPageSettings.cs
  - QuestBoard.Repository/Migrations/QuestBoardContextModelSnapshot.cs
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
  - QuestBoard.UnitTests/Architecture/AmbientClockSeamTests.cs
  - QuestBoard.UnitTests/Controllers/GroupControllerEditConcurrencyTests.cs
  - QuestBoard.UnitTests/Repository/BoardPageCountQueryTests.cs
  - QuestBoard.UnitTests/Repository/BoardPageSettingsMappingTests.cs
  - QuestBoard.UnitTests/Repository/GroupRepositoryTests.cs
  - QuestBoard.UnitTests/Repository/LinkedCalendarReadTests.cs
  - QuestBoard.UnitTests/Services/BoardMoveTargetPageOffTests.cs
  - QuestBoard.UnitTests/Services/BoardPageAvailabilityExtensionsTests.cs
  - QuestBoard.UnitTests/Services/BoardPagePolicyTests.cs
  - QuestBoard.UnitTests/Services/BoardPageRoutesTests.cs
  - QuestBoard.UnitTests/Services/BoardPageSettingsServiceTests.cs
  - QuestBoard.UnitTests/Services/EntityProfileEnumCastTests.cs
  - QuestBoard.UnitTests/Services/LinkedCalendarServiceTests.cs
findings:
  critical: 0
  warning: 2
  info: 5
  total: 7
status: issues_found
---

# Phase 93: Code Review Report

**Reviewed:** 2026-10-21
**Depth:** standard
**Files Reviewed:** 110
**Status:** issues_found

## Summary

The page-selection feature is largely sound. I traced the gate end to end and found no way
around it:

- `BoardPageGateFilter` is a global resource filter. It reads `BoardPageAttribute` from endpoint
  metadata, so it covers every HTTP verb. It runs after the authorization middleware and the
  antiforgery filter, so anonymous and unauthorised callers keep their existing responses.
- `BoardPageCoverageTests` pins that every public action on every controller is gated, exempt, or on a
  short always-on list. That closes the "forgot a new action" hole.
- Cross-board deep links switch the active board before the gate runs, and the resolver memo is keyed on
  the active board id, so the gate always judges the board the action will actually use.
- A crafted Platform form cannot widen a board's pages:
  - `ApplySwitches` re-checks `AllowedOn` and `Enum.IsDefined`.
  - Locked rows are never posted, and their state is recomputed server-side.
  - `Label`, `Locked` and `BoardType` are `[BindNever]` or recomputed.
  - The home page is validated against candidates and availability.
- The character-assignment gate is centralised in `ResolveCharacterAssignmentAsync`, so every write path
  that touches `CharacterId` is covered, not just the picker action.
- The move services refuse a target board whose Events or Quests page is off before any write.
- No planning or tracking references were found in the changed source. The migration adds two non-null
  `int` columns with default 0, and its `Down` is symmetric.

I found no BLOCKER-class defects. The two warnings are about the Platform save path under concurrent
super-admin edits. The info items are a docstring that overstates what its predicate does, dead or
fragile helper code, and a test that hard-codes this phase's identifiers.

## Warnings

### WR-01: Platform Edit can silently overwrite a concurrent page change, and its "live content" guards are check-then-act

**File:** `QuestBoard.Service/Areas/Platform/Controllers/GroupController.cs:78-118`, `QuestBoard.Domain/Services/BoardPageSettingsService.cs:12-33`, `QuestBoard.Repository/Entities/QuestBoardContext.cs:321`
**Issue:**
- The form posts a value for every unlocked page row. `Edit` loads the board with `FindAsync`, applies all posted rows over the stored mask, and writes the result.
- The only concurrency token on `Groups` is `LinkedBoardSetId`. `DisabledPages` and `HomePage` have none, and a token read from the same `FindAsync` could not detect a stale form anyway.
- Super admin A opens the form. Super admin B switches Shop off and saves. A then saves, for example, a rename with Shop still ticked, and Shop is silently switched back on. The "Someone else changed this board" message cannot fire for this case, because only `LinkedBoardSetId` is a token. The new `DbUpdateConcurrencyException` catch and its unit test only cover the link column.
- The same gap affects the Quests-off and Events-off guards. `EvaluateQuestsOffAsync` and `EvaluateEventsOffAsync` count live rows, and the save happens afterwards. A DM can create a quest or event between the count and the save, leaving live content behind a page the gate now refuses. The window is small, but the guard is the only protection.
**Fix:** Post the mask the form was rendered from, as a hidden field (or a version token). In `Edit`, compare it to the freshly loaded `group.DisabledPages` and `HomePage`. If it differs, re-render with the concurrency message instead of applying the rows. Or make `DisabledPages` an EF concurrency token, load the entity inside the same unit of work as the count, and let a concurrent write raise `DbUpdateConcurrencyException`. For the check-then-act window, run the count and the save in one serializable transaction, or accept the window and say so in the guard's comment.

### WR-02: Docstring says the live-quest count uses "the same rule the quest board uses", but the list uses a different cutoff

**File:** `QuestBoard.Repository/QuestRepository.cs:424-440` (compare `QuestRepository.cs:61-66` and `QuestBoard.Domain/Extensions/QuestExtensions.cs:22-25`)
**Issue:**
- `CountLiveForBoardAsync` documents itself as the rule the quest board uses to list a quest. It compares `FinalizedDate >= todayStart`, which is the date-based `HasBeenPlayed` rule.
- The board's own list queries (`GetQuestsWithSignupsAsync` and `...ForRoleAsync`) use `FinalizedDate > boardClock.Now.AddDays(-1)`, a rolling 24 hours.
- A quest finalized for 19:00 yesterday is still listed today until 19:00, but is not counted live. Quests can therefore be switched off while a quest is still visible on the quest list.
- The inaccuracy matters beyond the comment: the guard's stated contract is "no quest the board still shows is stranded".
**Fix:** Either reuse one shared predicate in both places, or correct the comment to say the count follows the played-quest rule and not the list cutoff. Decide deliberately whether the 24-hour tail should block the switch, and add a repository test at the boundary (finalized yesterday evening, board clock this morning).

## Info

### IN-01: `BoardPagePolicy.IsSwitchable` is a permanently-true hook

**File:** `QuestBoard.Domain/Extensions/BoardPagePolicy.cs:55`, used at `BoardPageSettingsService.cs:60-63` and `GroupController.cs:132`
**Issue:** It returns `true` for every page, yet two call sites branch on it, and `ApplySwitches` special-cases `QuestLog` separately. This is dead abstraction that suggests a rule that does not exist.
**Fix:** Delete it and its two uses, or give it a real rule and a test that fails if it returns true for everything.

### IN-02: `BoardPageSet` shifts wrap for undefined page values

**File:** `QuestBoard.Domain/Enums/BoardPage.cs:27-31`
**Issue:**
- C# masks shift counts to 5 bits, so `(BoardPage)33` aliases `Calendar` in `Contains`, `With` and `Without`.
- `BoardPageSettingsService` guards with `Enum.IsDefined`. `BoardPagePolicy.IsAvailable` and `BoardPageAvailability.IsAvailable` are public and unguarded, and `[BoardPage(...)]` accepts any value, so a typo'd cast would silently gate on the wrong bit.
**Fix:** Throw `ArgumentOutOfRangeException` when `(int)page` is outside `0..31`, or check `Enum.IsDefined` once inside `BoardPageSet`.

### IN-03: Planning-reference guard test hard-codes this phase's identifiers and a stale-prone file list

**File:** `QuestBoard.IntegrationTests/Tests/BoardPageStaticGuardTests.cs:35-45, 52-130`
**Issue:**
- The patterns build "Phase 93", `93-01..15` and `D-01..21` out of string fragments so the file does not contain the forbidden literals. That still ties the test to one phase's numbering.
- It protects only the listed files, and a missing file is silently skipped. Only the `>= 50` count guards against silent shrinkage.
- The project rule is general: no requirement IDs, phase or plan numbers, or planning document names in source. The test would not catch a later phase's references in these files.
**Fix:** Scan every `*.cs` and `*.cshtml` under the three source projects with generic patterns (any phase number, `\b[A-Z]{2,}-\d{2}\b`, `*-REVIEW.md`, `*-PLAN.md`). Fail if a listed file is missing, instead of skipping it.

### IN-04: Platform board list shows the stored home page, not the effective one

**File:** `QuestBoard.Service/Areas/Platform/Views/Group/Index.cshtml:53-56`, `Index.Mobile.cshtml:44-47`, `QuestBoard.Repository/GroupRepository.cs:20-41`
**Issue:** The "Home: X" badge reads the stored `HomePage`. If X is unavailable (switched off, or a stored home that no longer applies), the board actually opens elsewhere, but the list still claims X. The list projection does not carry `DisabledPages`, so the view cannot compute the effective home.
**Fix:** Project `DisabledPages` and `BoardType` into `GroupWithMemberCount` and show `BoardPagePolicy.EffectiveHome(...)`. Mark the badge when the stored home is being overridden.

### IN-05: When a board has no available home page, the account page's "Back to Quest Board" button links to itself

**File:** `QuestBoard.Service/Views/Account/Profile.cshtml` (button), `QuestBoard.Service/Helpers/BoardPageRoutes.cs:34-42`
**Issue:**
- With every home candidate off, `HomeRoute` returns `Account/Profile`. The button now points to `Home/Index`, which redirects straight back to `Account/Profile`.
- It is a no-op link on the one board state where the account page is the only page left. There is no redirect loop, because the gate never gates Account.
**Fix:** Hide the button when `BoardPages.GetAsync()` yields no `EffectiveHome`, or rename it for that state.

---

_Reviewed: 2026-10-21_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
