---
phase: 91-linked-boards-with-a-shared-calendar
reviewed: 2026-10-16T00:00:00Z
depth: standard
files_reviewed: 66
files_reviewed_list:
  - QuestBoard.Domain/Enums/LinkedBoardSetResult.cs
  - QuestBoard.Domain/Extensions/ServiceExtensions.cs
  - QuestBoard.Domain/Interfaces/IEventRepository.cs
  - QuestBoard.Domain/Interfaces/ILinkedBoardSetRepository.cs
  - QuestBoard.Domain/Interfaces/ILinkedBoardSetService.cs
  - QuestBoard.Domain/Interfaces/ILinkedCalendarService.cs
  - QuestBoard.Domain/Interfaces/IQuestRepository.cs
  - QuestBoard.Domain/Interfaces/IQuestService.cs
  - QuestBoard.Domain/Models/LinkedBoard.cs
  - QuestBoard.Domain/Models/LinkedBoardCandidate.cs
  - QuestBoard.Domain/Models/LinkedBoardSet.cs
  - QuestBoard.Domain/Models/LinkedBoardSetBoard.cs
  - QuestBoard.Domain/Models/LinkedBoardSetOutcome.cs
  - QuestBoard.Domain/Models/LinkedCalendarData.cs
  - QuestBoard.Domain/Models/LinkedEventEntry.cs
  - QuestBoard.Domain/Models/LinkedQuestEntry.cs
  - QuestBoard.Domain/Models/ViewerLinkedBoards.cs
  - QuestBoard.Domain/Services/LinkedBoardSetService.cs
  - QuestBoard.Domain/Services/LinkedCalendarService.cs
  - QuestBoard.Domain/Services/QuestService.cs
  - QuestBoard.IntegrationTests/Controllers/LinkedBoardsPlatformIntegrationTests.cs
  - QuestBoard.IntegrationTests/Middleware/LinkedBoardDeepLinkTests.cs
  - QuestBoard.IntegrationTests/Tests/CalendarDungeonMasterSessionTests.cs
  - QuestBoard.IntegrationTests/Tests/LinkedBoardCalendarTenantIsolationTests.cs
  - QuestBoard.IntegrationTests/Tests/LinkedBoardEndToEndTests.cs
  - QuestBoard.IntegrationTests/Tests/LinkedBoardMobileCalendarTests.cs
  - QuestBoard.IntegrationTests/Tests/LinkedBoardQuestDetailsTests.cs
  - QuestBoard.IntegrationTests/Tests/LinkedBoardStaticGuardTests.cs
  - QuestBoard.Repository/Automapper/EntityProfile.cs
  - QuestBoard.Repository/Entities/GroupEntity.cs
  - QuestBoard.Repository/Entities/LinkedBoardSetEntity.cs
  - QuestBoard.Repository/Entities/QuestBoardContext.cs
  - QuestBoard.Repository/EventRepository.cs
  - QuestBoard.Repository/Extensions/ServiceExtensions.cs
  - QuestBoard.Repository/LinkedBoardSetRepository.cs
  - QuestBoard.Repository/Migrations/20261016061911_AddLinkedBoardSets.cs
  - QuestBoard.Repository/QuestRepository.cs
  - QuestBoard.Service/Areas/Platform/Controllers/LinkedBoardsController.cs
  - QuestBoard.Service/Areas/Platform/Views/Group/Index.Mobile.cshtml
  - QuestBoard.Service/Areas/Platform/Views/Group/Index.cshtml
  - QuestBoard.Service/Areas/Platform/Views/LinkedBoards/Index.Mobile.cshtml
  - QuestBoard.Service/Areas/Platform/Views/LinkedBoards/Index.cshtml
  - QuestBoard.Service/Controllers/QuestBoard/CalendarController.cs
  - QuestBoard.Service/Controllers/QuestBoard/QuestController.cs
  - QuestBoard.Service/ViewModels/CalendarViewModels/CalendarDay.cs
  - QuestBoard.Service/ViewModels/CalendarViewModels/CalendarViewModel.cs
  - QuestBoard.Service/ViewModels/CalendarViewModels/LinkedDayEntries.cs
  - QuestBoard.Service/ViewModels/CalendarViewModels/LinkedEventOnDay.cs
  - QuestBoard.Service/ViewModels/CalendarViewModels/LinkedQuestOnDay.cs
  - QuestBoard.Service/ViewModels/PlatformViewModels/LinkedBoardSetCreateViewModel.cs
  - QuestBoard.Service/ViewModels/PlatformViewModels/LinkedBoardsIndexViewModel.cs
  - QuestBoard.Service/Views/Calendar/Index.Mobile.cshtml
  - QuestBoard.Service/Views/Calendar/Index.cshtml
  - QuestBoard.Service/Views/Shared/_Calendar.Mobile.cshtml
  - QuestBoard.Service/Views/Shared/_Calendar.cshtml
  - QuestBoard.Service/Views/Shared/_LinkedAlsoThatDay.cshtml
  - QuestBoard.Service/Views/Shared/_LinkedDayEntries.cshtml
  - QuestBoard.Service/wwwroot/css/calendar.css
  - QuestBoard.Service/wwwroot/css/calendar.mobile.css
  - QuestBoard.Service/wwwroot/css/platform-linked-boards.mobile.css
  - QuestBoard.Service/wwwroot/css/quests.mobile.css
  - QuestBoard.UnitTests/Repository/LinkedBoardSetRepositoryTests.cs
  - QuestBoard.UnitTests/Repository/LinkedCalendarReadTests.cs
  - QuestBoard.UnitTests/Services/LinkedBoardSetServiceTests.cs
  - QuestBoard.UnitTests/Services/LinkedCalendarServiceTests.cs
  - QuestBoard.UnitTests/ViewModels/CalendarViewModelLinkedTests.cs
findings:
  critical: 0
  warning: 2
  info: 4
  total: 6
resolved:
  warning: 2
  info: 4
status: issues_found
---

# Phase 91: Code Review Report

**Reviewed:** 2026-10-16
**Depth:** standard
**Files Reviewed:** 66
**Status:** issues_found

## Summary

Linked board sets, the linked calendar read path, the SuperAdmin Platform screens and the desktop and mobile views were reviewed against the diff from `f8ce15b8`. No blocker was found. I looked at each of the areas called out in the brief.

- **Tenant isolation.** Both `IgnoreQueryFilters()` reads (`EventRepository.GetLinkedEventsForCalendarAsync`, `QuestRepository.GetLinkedQuestsForCalendarAsync`) are re-pinned in the same expression to `linkedGroupIds`.
  - The ids come from a fresh server-side lookup, `GetLinkedBoardsForViewerAsync`, not from request input.
  - An empty id list short-circuits to an empty result rather than widening.
  - The projections are flat and select no signups, votes or players.
  - The active board is excluded (`g.Id != activeGroupId`).
  - The set type is checked against the board type.
  - Campaign sets never fetch quests.
- **DM-only sessions.** The rule is applied per owning board, with the viewer's role on that board, and SuperAdmin counts as Admin. The own-board calendar and the Quest Details calendar now apply the same rule, and `GetQuestsForCalendarAsync` has no other callers.
- **Authorization and CSRF.** The controller carries `[Authorize(Policy = "SuperAdminOnly")]` and every POST has `[ValidateAntiForgeryToken]`. The form tag helper emits the token.
- **XSS.**
  - Every user-controlled value (set, board and quest/event names) goes through Razor encoding.
  - The `confirm()` handlers serialize to a JS string literal through `Json.Serialize` (the default escaping encoder) and are then attribute-encoded.
  - There is no `Html.Raw`, and no `UnsafeRelaxedJsonEscaping` is configured.
  - TempData toasts are rendered encoded.
- **Board-clock rules.** Linked quest times are shown as stored with no timezone conversion, and the window is built from the requested year and month rather than an ambient clock.
- **Schema.** The migration, its snapshot and the `SetNull` FK agree. The `Group` to `GroupEntity` AutoMapper mapping now ignores the link columns, so a board rename cannot clear a link.

The remaining findings are concurrency and error-handling weaknesses in the SuperAdmin-only write path and small consistency items. None is a data leak.

## Warnings

### WR-01: Any DbUpdateException is reported as a duplicate set name, and the rename recovery can itself throw

**File:** `QuestBoard.Repository/LinkedBoardSetRepository.cs:69-75` and `143-153`
**Issue:** `CreateAsync` and `RenameAsync` catch every `DbUpdateException` and treat it as the unique-name index rejecting a concurrent duplicate.
- A timeout, a deadlock, a dropped connection or a truncation error wrapped in `DbUpdateException` is therefore shown to the SuperAdmin as "A linked set named X already exists", and the real failure is swallowed.
- In `RenameAsync` the recovery query uses `FirstAsync`. If the set was deleted between the failed save and the re-read, that throws `InvalidOperationException` and the request returns a 500 instead of the intended `false`.
- The unique-index catch paths cannot be exercised by the EF InMemory provider, which does not enforce unique indexes. They are untested.

**Fix:** Narrow the catch to the SQL Server unique-violation numbers, and make the recovery tolerant.
```csharp
catch (DbUpdateException ex) when (ex.InnerException is Microsoft.Data.SqlClient.SqlException { Number: 2601 or 2627 })
{
    set.Name = await dbContext.LinkedBoardSets.AsNoTracking()
        .Where(s => s.Id == setId).Select(s => s.Name).FirstOrDefaultAsync(token) ?? set.Name;
    return false;
}
```
If the Repository layer should not reference the SqlClient type, re-run `NameExistsAsync` inside the catch and rethrow when no duplicate is found.

**Resolution (2026-10-17, commit a191b32c):** Fixed with the provider-agnostic variant. After a refused save, `CreateAsync` and `RenameAsync` ask `NameExistsAsync` whether another set now holds the name. Only then is it reported as a duplicate; any other failure is rethrown. `RenameAsync` keeps the stored name from its own read and restores it without a query, so the recovery cannot throw. It now returns `LinkedBoardSetResult`, and a set deleted mid-rename reports `NotFound` instead of `DuplicateName`. Repository facts drive each path with an interceptor: a name taken just before the save, a refused save with the name free (throws), and a set deleted just before the rename.

### WR-02: AddBoard and RemoveBoard are check-then-write with an unconditional overwrite

**File:** `QuestBoard.Repository/LinkedBoardSetRepository.cs:81-89`, used by `QuestBoard.Domain/Services/LinkedBoardSetService.cs:46-72` and `101-121`
**Issue:** The service checks type match, "already in a set" and membership against a read-only snapshot. `AssignBoardAsync` then sets `LinkedBoardSetId` unconditionally on a freshly loaded row.
- Two SuperAdmins adding the same unlinked board to two different sets concurrently both pass `AlreadyLinked`. The last write silently moves the board and the first admin is told it was added. This breaks the stated "refused when the board is already in a set" guarantee.
- If a set is deleted between `GetByIdAsync` and the assignment, the FK rejects the write. That `DbUpdateException` is not caught in this method and surfaces as a 500, whereas the service's own contract is to return `NotFound`.

**Fix:** Make the assignment conditional on the state the service checked, and map the FK failure to `false`.
```csharp
public async Task<bool> AssignBoardAsync(int groupId, int? expectedCurrentSetId, int? newSetId, CancellationToken token = default)
{
    var affected = await dbContext.Groups
        .Where(g => g.Id == groupId && g.LinkedBoardSetId == expectedCurrentSetId)
        .ExecuteUpdateAsync(s => s.SetProperty(g => g.LinkedBoardSetId, newSetId), token);
    return affected == 1;
}
```
`Groups` is not covered by the Events/Quests change-tracker rule, so `ExecuteUpdate` is permitted here. Wrap the call in a try/catch for the FK violation.

**Resolution (2026-10-17, commit a191b32c):** Fixed, but not with `ExecuteUpdate`. `FeedRevisionWriteSeamTests` bans `ExecuteUpdate` in all production source, not only for Events and Quests, so the premise above was wrong. The conditional write goes through the change tracker instead.
- `Groups.LinkedBoardSetId` is now a concurrency token, the same pattern as `FeedRevision`. An add writes only if the board is still in no set, and a remove only if it is still in that set.
- `AssignBoardAsync` is replaced by `AddBoardAsync` and `RemoveBoardAsync`, which return `LinkedBoardSetResult`. A lost race reports `AlreadyLinked` or `NotInSet` and leaves the board where the other admin put it. A set deleted before the write reports `NotFound` instead of a 500.
- `DeleteAsync` relies on the foreign key's `ON DELETE SET NULL` on relational providers instead of un-linking each board, so a concurrent move cannot fail the delete.
- No migration was needed: `dotnet ef migrations has-pending-model-changes` reports none.
- Repository facts reproduce each race through two contexts over one store. Removing the token makes the add and remove race facts fail.
- Residual: an ordinary board edit now fails on save if a super admin links or unlinks that same board in the milliseconds between the edit's read and its write. No data is lost; the edit can be resubmitted.

## Info

### IN-01: The "single place" date-type conversion rule is now broken by a second site

**File:** `QuestBoard.Domain/Services/LinkedCalendarService.cs:45`
**Issue:** The comment on `CalendarViewModel.ToStoredDate` states "No other file may convert between them", but `LinkedCalendarService` calls `DateOnly.FromDateTime` twice for the event window. The behaviour is correct, because the bounds are midnight-aligned month starts, but the invariant is now fiction and a future non-aligned caller would silently truncate.
**Fix:** Either accept `DateOnly` bounds in `ILinkedCalendarService`, or relax the comment so it reflects that the window conversion lives in the service.

### IN-02: Linked time labels depend on the current culture

**File:** `QuestBoard.Service/ViewModels/CalendarViewModels/LinkedEventOnDay.cs:14` and `LinkedQuestOnDay.cs:15`
**Issue:** `ToString("HH:mm")` renders the `:` as the current culture's time separator. The same pattern already exists in the own-entry views, but the new view-model properties are a chance to avoid it.
**Fix:** `ToString("HH:mm", CultureInfo.InvariantCulture)`.

### IN-03: Dead or redundant fields in the repository and controller layer

**File:** `QuestBoard.Repository/LinkedBoardSetRepository.cs:92-106` and `QuestBoard.Service/Areas/Platform/Controllers/LinkedBoardsController.cs`
**Issue:**
- `GetUnlinkedBoardsAsync` filters on `LinkedBoardSetId == null` and then projects `LinkedBoardSetId`, which is always null.
- `LinkedBoardSetService.AddBoardAsync` and `RemoveBoardAsync` load the set with all its boards through `GetByIdAsync` only to read its name and type.
- The Platform actions do not take or forward a `CancellationToken`, unlike `CalendarController.Index`.

**Fix:** Drop the redundant projection. Optionally add a lighter set lookup, and accept a `CancellationToken` in the actions.

### IN-04: Linked-calendar tests cannot see SQL translation or unique-index behaviour

**File:** `QuestBoard.UnitTests/Repository/LinkedCalendarReadTests.cs`, `QuestBoard.UnitTests/Repository/LinkedBoardSetRepositoryTests.cs`
**Issue:** All linked-board repository tests run on EF InMemory. The `IgnoreQueryFilters().Where(ids.Contains(...) && ProposedDates.Any(...))` queries, the nested `ProposedDates ... .ToList()` projection, and the duplicate-name catch paths are never translated or enforced against SQL Server. Reading the queries, they should translate under EF Core 10, but nothing proves it.
**Fix:** Add one smoke run of the two linked read queries and the duplicate-name path against a real SQL Server (an operator verification step is enough, given the project's no-provisioning rule).

---

_Reviewed: 2026-10-16_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_

## Info Resolution (2026-10-17)

- **IN-01:** The `CalendarViewModel.ToStoredDate` comment now scopes its claim to placing entries on grid days. It also names the one other conversion, the linked read's midnight-aligned month window in `LinkedCalendarService`. The interface was left unchanged, because accepting `DateOnly` bounds would have pushed the conversion into two controllers.
- **IN-02:** `LinkedEventOnDay` and `LinkedQuestOnDay` format `HH:mm` with `CultureInfo.InvariantCulture`.
- **IN-03:**
  - `GetUnlinkedBoardsAsync` no longer projects the always-null `LinkedBoardSetId`.
  - The Platform actions still take no `CancellationToken`, matching every other Platform controller (`GroupController` and others).
  - The lighter set lookup was not added: it is optional, and the set read is one indexed row.
- **IN-04:** Closed by evidence rather than code. The migration and the two `IgnoreQueryFilters` reads ran against local SQL Server on 2026-10-16 and 2026-10-17 with no translation errors, as recorded in `91-10-SUMMARY.md`. The duplicate-name and race paths are now driven by interceptor-based repository facts.

