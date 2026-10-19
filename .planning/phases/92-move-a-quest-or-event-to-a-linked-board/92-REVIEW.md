---
phase: 92-move-a-quest-or-event-to-a-linked-board
reviewed: 2026-10-19T00:00:00Z
depth: standard
files_reviewed: 78
files_reviewed_list:
  - QuestBoard.Domain/Enums/MoveBlockReason.cs
  - QuestBoard.Domain/Enums/MoveStatus.cs
  - QuestBoard.Domain/Enums/QuestMovedAudience.cs
  - QuestBoard.Domain/Extensions/MoveEligibility.cs
  - QuestBoard.Domain/Extensions/MovePreviewStamp.cs
  - QuestBoard.Domain/Extensions/ServiceExtensions.cs
  - QuestBoard.Domain/Extensions/WaitlistOrdering.cs
  - QuestBoard.Domain/Interfaces/IEventMoveRepository.cs
  - QuestBoard.Domain/Interfaces/IEventMoveService.cs
  - QuestBoard.Domain/Interfaces/IIdentityService.cs
  - QuestBoard.Domain/Interfaces/IMoveControlService.cs
  - QuestBoard.Domain/Interfaces/IQuestEmailDispatcher.cs
  - QuestBoard.Domain/Interfaces/IQuestMoveRepository.cs
  - QuestBoard.Domain/Interfaces/IQuestMoveService.cs
  - QuestBoard.Domain/Models/BoardMove/EventMovePlan.cs
  - QuestBoard.Domain/Models/BoardMove/EventMovePreview.cs
  - QuestBoard.Domain/Models/BoardMove/MoveControlState.cs
  - QuestBoard.Domain/Models/BoardMove/MovePerson.cs
  - QuestBoard.Domain/Models/BoardMove/MovePreview.cs
  - QuestBoard.Domain/Models/BoardMove/QuestMovePlan.cs
  - QuestBoard.Domain/Models/BoardMove/QuestMovePreview.cs
  - QuestBoard.Domain/Services/EventMoveService.cs
  - QuestBoard.Domain/Services/MoveControlService.cs
  - QuestBoard.Domain/Services/QuestMoveService.cs
  - QuestBoard.Repository/EventMoveRepository.cs
  - QuestBoard.Repository/Extensions/ServiceExtensions.cs
  - QuestBoard.Repository/IdentityService.cs
  - QuestBoard.Repository/QuestMoveRepository.cs
  - QuestBoard.Service/Components/Emails/QuestMoved.razor
  - QuestBoard.Service/Components/Emails/WaitlistPromoted.razor
  - QuestBoard.Service/Constants/TempDataKeys.cs
  - QuestBoard.Service/Controllers/Admin/EmailPreviewController.cs
  - QuestBoard.Service/Controllers/Events/EventsController.cs
  - QuestBoard.Service/Controllers/MoveController.cs
  - QuestBoard.Service/Controllers/QuestBoard/QuestController.cs
  - QuestBoard.Service/Helpers/MoveCopy.cs
  - QuestBoard.Service/Jobs/QuestMovedEmailJob.cs
  - QuestBoard.Service/Jobs/QuestWaitlistPromotedEmailJob.cs
  - QuestBoard.Service/Services/HangfireQuestEmailDispatcher.cs
  - QuestBoard.Service/Services/NullQuestEmailDispatcher.cs
  - QuestBoard.Service/ViewModels/MoveViewModels/MovePageViewModel.cs
  - QuestBoard.Service/Views/Events/Details.cshtml
  - QuestBoard.Service/Views/Move/Index.cshtml
  - QuestBoard.Service/Views/Move/Index.Mobile.cshtml
  - QuestBoard.Service/Views/Quest/Details.cshtml
  - QuestBoard.Service/Views/Quest/Details.Mobile.cshtml
  - QuestBoard.Service/Views/Shared/_Layout.cshtml
  - QuestBoard.Service/Views/Shared/_Toasts.cshtml
  - QuestBoard.Service/wwwroot/css/mobile.css
  - QuestBoard.Service/wwwroot/css/move-entry.css
  - QuestBoard.Service/wwwroot/css/move-entry.mobile.css
  - QuestBoard.IntegrationTests/Emails/QuestMovedRenderTests.cs
  - QuestBoard.IntegrationTests/Helpers/BoardMoveSeed.cs
  - QuestBoard.IntegrationTests/Helpers/MoveWebApplicationFactory.cs
  - QuestBoard.IntegrationTests/Tests/BoardMoveApplyTests.cs
  - QuestBoard.IntegrationTests/Tests/BoardMoveAuthorizationTests.cs
  - QuestBoard.IntegrationTests/Tests/BoardMoveControlTests.cs
  - QuestBoard.IntegrationTests/Tests/BoardMoveEndToEndTests.cs
  - QuestBoard.IntegrationTests/Tests/BoardMoveEventTests.cs
  - QuestBoard.IntegrationTests/Tests/BoardMoveMailEnqueueTests.cs
  - QuestBoard.IntegrationTests/Tests/BoardMovePageTests.cs
  - QuestBoard.IntegrationTests/Tests/BoardMoveQuestMailFlowTests.cs
  - QuestBoard.IntegrationTests/Tests/BoardMoveQuestPeopleTests.cs
  - QuestBoard.IntegrationTests/Tests/BoardMoveStaticGuardTests.cs
  - QuestBoard.IntegrationTests/Tests/BoardMoveTargetTests.cs
  - QuestBoard.IntegrationTests/Tests/BoardMoveViewsTests.cs
  - QuestBoard.UnitTests/Architecture/AmbientClockSeamTests.cs
  - QuestBoard.UnitTests/Architecture/BoardMoveRepositorySingleSaveTests.cs
  - QuestBoard.UnitTests/Extensions/BoardMoveEligibilityTests.cs
  - QuestBoard.UnitTests/Repository/BoardMoveEventRepositoryTests.cs
  - QuestBoard.UnitTests/Repository/BoardMoveQuestRepositoryTests.cs
  - QuestBoard.UnitTests/Repository/BoardMoveWaitlistParityTests.cs
  - QuestBoard.UnitTests/Services/BoardMoveControlServiceTests.cs
  - QuestBoard.UnitTests/Services/BoardMoveEventServiceTests.cs
  - QuestBoard.UnitTests/Services/BoardMoveMailJobTests.cs
  - QuestBoard.UnitTests/Services/BoardMoveQuestMailTests.cs
  - QuestBoard.UnitTests/Services/BoardMoveQuestPlanTests.cs
  - QuestBoard.UnitTests/Services/BoardMoveQuestPreviewTests.cs
findings:
  critical: 0
  warning: 2
  info: 7
  total: 9
status: issues_found
---

# Phase 92: Code Review Report

**Reviewed:** 2026-10-19
**Depth:** standard
**Files Reviewed:** 78
**Status:** issues_found

## Summary

The cross-board write path was read end to end: `MoveController`, `QuestMoveService` and `QuestMoveRepository`, `EventMoveService` and `EventMoveRepository`, `MoveControlService`, the mail dispatcher and jobs, the Move views (both layouts), the toast partial and the Details entry points. The production code was checked against the locked decisions in the phase context. The 199 unit tests matching `BoardMove|AmbientClockSeam` were run and pass. The integration tests were read but not run.

No blocker was found. These points held up:

- **Authorization.** `[Authorize(Policy = "SuperAdminOnly")]` is on the whole controller, so GET and POST are both covered. Anti-forgery is on both POSTs. The Details buttons key on `User.IsInRole("SuperAdmin")` rather than `CanManage`.
- **Target rule.** The set is read fresh on every preview and every confirm. `GetLinkedBoardsForViewerAsync` excludes the source board, so a posted source id or an out-of-set id ends in `TargetNotLinked`.
- **Query filters.** No `IgnoreQueryFilters`, `ExecuteUpdate`, raw SQL or hand-set entity state in any new production file.
- **Single save.** Each repository saves once. Every read happens before `GroupId` changes. A failed save clears the tracker.
- **Clock.** No ambient clock reads. `MoveEligibility` takes `boardClock.Today`, and the email sample date is fixed.
- **Redirects and XSS.** The toast link is built from `Url.Action` and re-checked with `Url.IsLocalUrl`. All titles and board names go through Razor or Blazor encoding. There is no `Html.Raw` in the Move views or the toast.
- **Mail.** Each enqueue is isolated by try/catch and only runs after a committed move. The actor is never mailed. Recipients are de-duplicated and the confirmed-address gate is applied. Mail jobs run under the target board id.
- **Waitlist.** `SelectPromotions` matches `GetTopWaitlistedCandidateAsync` (parity test passes). The new `QuestWaitlistPromotedEmailJob.ExecuteAsync` keeps its signature, so jobs already queued in Hangfire storage still bind.

Two warnings and some smaller items follow.

## Warnings

### WR-01: A repeated confirm after a successful move returns a bare 404

**File:** `QuestBoard.Service/Controllers/MoveController.cs:54-55` (also `:109-110`), caused by `QuestBoard.Domain/Services/QuestMoveService.cs:168-172` and `EventMoveService.cs:88-92`
**Issue:** The quest or event is looked up through the source board's query filter. Once the first POST commits, the entry is no longer on the source board. A second POST, from a double click or a resubmit after the browser's back button, takes the `NotFound` branch and renders a 404 page. The move itself succeeded and the success toast sits in TempData, but the user's last response can be the 404. The same happens on a GET of the Move page after the move. The confirm form has no submit-disable guard, so this is easy to hit on the slow path where the save, the Hangfire enqueues and the redirect take a moment.
**Fix:** Add one-shot submit protection to the confirm form, in both `Move/Index.cshtml` and `Move/Index.Mobile.cshtml`:

```html
<form method="post" ... onsubmit="this.querySelector('button[type=submit]').disabled = true;">
```

Also consider turning `NotFound` on the POST into a redirect to the board's list with an info toast ("That quest is no longer on this board"), instead of a 404 for an action the super admin just performed.

### WR-02: The repository trusts the plan's classification and only compares id sets

**File:** `QuestBoard.Repository/QuestMoveRepository.cs:34-37, 61-78`; `QuestBoard.Repository/EventMoveRepository.cs:27-32`
**Issue:** `ApplyAsync` re-reads the signups and rejects the plan only when the set of signup ids differs from the plan. It then applies the plan as written:

- A promoted id gets `IsSelected = true` without checking it is still unselected, still a Player, and still voted Yes or Maybe.
- A dropped id is deleted without checking it is still a non-member of the target.
- A signup inserted between the repository's read and `SaveChangesAsync` is not detected. `PlayerSignups` has no concurrency token, so it ends up on the target board even though its player was never reconciled against the target membership.

The window is small (one request), but this is the only place that can enforce "nothing from the preview is trusted". The page-level stamp covers only ids, not selection, role or votes. A DM who selects or revokes a signup between page render and confirm, with no waitlist involved, leaves the stamp unchanged while the page's "Selected" marks and seat count are stale.
**Fix:** Cheapest option: extend the stamp input and the repository's staleness check to include per-signup `(Id, IsSelected, Role)` for dropped and promoted signups. In `ApplyAsync`, compare those with the tracked rows and return `Changed` on any difference. If a stronger guarantee is wanted, run the read and the save in one serializable transaction.

## Info

### IN-01: `MoveStatus.Ready` is the enum's zero value and the default of every new preview

**File:** `QuestBoard.Domain/Enums/MoveStatus.cs:8`, `QuestBoard.Domain/Models/BoardMove/MovePreview.cs:11`
**Issue:** Any `MovePreview` whose status is not explicitly assigned reads as "Ready". Today every path in `BuildPreviewAsync` assigns one. A later early return that forgets to would produce a "ready" preview with `Plan == null`. The `Plan is not { }` guard in `MoveAsync` and the controller's default branch only partly mask that.
**Fix:** Put a neutral value first (for example `None = 0`, or start `Ready` at 1), or give `MovePreview.Status` no default.

### IN-02: The acting user can silently resolve to Id 0

**File:** `QuestBoard.Service/Controllers/MoveController.cs:21, 45, 76, 100`
**Issue:** `IUserService.GetUserAsync` returns `new User()` (Id 0) when the principal cannot be resolved. The controller passes `actor.Id` straight into the services. The service would then run with actor 0. The "never mail the actor" exclusion would match nobody, and the log line would name user 0. `EventsController.Details` guards with `currentUser.Id != 0`; this controller does not.
**Fix:** `if (actor.Id == 0) return Forbid();` at the top of each action, or a small private helper.

### IN-03: `BoardMoveStaticGuardTests` silently skips files that no longer exist

**File:** `QuestBoard.IntegrationTests/Tests/BoardMoveStaticGuardTests.cs:101-108, 110-119`
**Issue:** `PresentFiles()` filters the list with `File.Exists`, and the sanity test pins only three files. A renamed or moved feature file drops out of the planning-reference scan with no failure. The scan also covers only production files, not the 27 test files, which CLAUDE.md's comment rule applies to as well.
**Fix:** Assert that every entry in `FeatureSourceFiles` exists, and drop the "can name a file before it exists" leniency now that the feature has merged. Consider scanning the `BoardMove*` test files too.

### IN-04: Hard-coded 2031 dates make integration tests time bombs

**File:** `QuestBoard.IntegrationTests/Tests/BoardMoveControlTests.cs:36`, `BoardMoveEventTests.cs:32`, `BoardMoveViewsTests.cs:31`, `BoardMoveQuestPeopleTests.cs:12` (finalized quest dated 2031-03-10)
**Issue:** These tests run against the real board clock and treat 2031 dates as "upcoming". They will start failing once the real date passes them (finalized quest from March 2031, events from May 2031). `BoardMoveEndToEndTests.cs:38-39` uses `DateTime.Today` instead, an ambient clock read in the opposite direction.
**Fix:** Derive the dates from the factory's `IBoardClock` (`clock.Today.AddDays(n)`), or fix the clock for these fixtures with a fake.

### IN-05: The email subject is built twice

**File:** `QuestBoard.Service/Jobs/QuestMovedEmailJob.cs:46-48`, `QuestBoard.Service/Components/Emails/QuestMoved.razor` (`Subject`)
**Issue:** The job computes the SMTP subject and the template computes its own `Subject` for the HTML head with the same two strings. If one is edited, the inbox subject and the document title drift apart.
**Fix:** Expose the subject from one place, for example a static `QuestMoved.SubjectFor(audience, boardName, questTitle)` that both call.

### IN-06: Preview model carries an unused property and a possibly empty board name

**File:** `QuestBoard.Domain/Models/BoardMove/QuestMovePreview.cs` (`DungeonMasterId`), `QuestMoveService.cs:174` and `EventMoveService.cs:94`
**Issue:** `QuestMovePreview.DungeonMasterId` is set but never read. `SourceBoardName` falls back to an empty string when the board lookup returns null, which renders "On  " and "and  will no longer show it" in the Move views.
**Fix:** Remove the unused property. Fall back to a phrase such as "this board" in the view when the name is empty.

### IN-07: Event start time rendered with a culture-sensitive custom format; service build methods are long and near-duplicates

**File:** `QuestBoard.Service/ViewModels/MoveViewModels/MovePageViewModel.cs:109`; `QuestMoveService.cs:160-286`, `EventMoveService.cs:80-179`
**Issue:**
1. `start.ToString("HH:mm")` uses the current culture's time separator (it is `.` in some cultures). This wall-clock value is formatted by hand instead of through the project's time helper.
2. `BuildPreviewAsync` in the quest service is about 125 lines. The block from the board lookup through the target resolution (set read, `ChooseTarget`, `TargetNotLinked`) is copied word for word between the quest and event services. A fix to one will be missed in the other.

**Fix:** Format with `CultureInfo.InvariantCulture` or a `Html.WallClock` time variant. Extract the shared target resolution into one helper that returns either a resolved `LinkedBoard` or a terminal `MoveStatus`.

---

_Reviewed: 2026-10-19_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
