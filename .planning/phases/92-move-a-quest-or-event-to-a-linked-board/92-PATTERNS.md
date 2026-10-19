# Phase 92: Move a Quest or Event to a Linked Board - Pattern Map

**Mapped:** 2026-10-18
**Files analyzed:** 27 new/modified
**Analogs found:** 25 / 27 (2 partial, see "No Analog Found")

Source inputs: 92-CONTEXT.md (D-01..D-21, incl. D-03 SuperAdmin-DM addendum), 92-RESEARCH.md (Patterns 1-8, first 506 lines read), 92-UI-SPEC.md.

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|---|---|---|---|---|
| `QuestBoard.Domain/Interfaces/IBoardMoveService.cs` | service interface | request-response | `QuestBoard.Domain/Interfaces/IEventSeriesService.cs` | role-match |
| `QuestBoard.Domain/Interfaces/IBoardMoveRepository.cs` | repository interface | CRUD (one unit of work) | `IEventSeriesRepository` (`DetachSessions`/`CreateWithOccurrencesAsync`) | role-match |
| `QuestBoard.Domain/Services/BoardMoveService.cs` | service (internal) | request-response + after-save event-driven mail | `QuestBoard.Domain/Services/EventSeriesService.cs` + `QuestService.cs` 300-345 | role-match |
| `QuestBoard.Domain/Extensions/MoveEligibility.cs` | utility (pure rule) | transform | `QuestBoard.Domain/Extensions/QuestExtensions.cs` (`HasBeenPlayed`) | exact |
| `QuestBoard.Domain/Models/*MovePlan/Preview*.cs`, `Enums/QuestMovedAudience.cs` | model | transform | existing `QuestBoard.Domain/Models` + `ViewerLinkedBoards` | role-match |
| `QuestBoard.Repository/BoardMoveRepository.cs` | repository (internal sealed) | CRUD, single `SaveChangesAsync` | `QuestBoard.Repository/EventSeriesRepository.cs` (`DetachSessions` 165-188) and `EventRepository.AddWithCampaignFanOutAsync` | exact |
| `QuestBoard.Domain/Interfaces/IIdentityService.cs` + `QuestBoard.Repository/IdentityService.cs` (modified: add by-id SuperAdmin check) | service | request-response | `IdentityService.ChangePasswordAsync(int userId, ...)` | partial (no by-id role check exists) |
| `QuestBoard.Domain/Interfaces/IQuestEmailDispatcher.cs` (modified) + `Service/Services/HangfireQuestEmailDispatcher.cs` + `Service/Services/NullQuestEmailDispatcher.cs` | dispatcher | event-driven | existing `EnqueueWaitlistPromotedEmail` in the same three files | exact |
| `QuestBoard.Service/Jobs/QuestMovedEmailJob.cs` (+ new method on `QuestWaitlistPromotedEmailJob.cs`) | job | event-driven | `QuestBoard.Service/Jobs/QuestWaitlistPromotedEmailJob.cs` | exact |
| `QuestBoard.Service/Components/Emails/QuestMoved.razor` | email template | transform | `Components/Emails/QuestDateChanged.razor` (+ `AddedToGroup.razor` note style) | exact |
| `QuestBoard.Service/Components/Emails/WaitlistPromoted.razor` (modified, `MovedToBoardName`) | email template | transform | itself | exact |
| `QuestBoard.Service/Controllers/Admin/EmailPreviewController.cs` (modified, 4 actions) | controller | request-response | `WaitlistPromoted()` action there | exact |
| `QuestBoard.Service/Controllers/MoveController.cs` | controller | request-response | `Areas/Platform/Controllers/LinkedBoardsController.cs` (SuperAdminOnly + toast) and `Controllers/Events/SeriesController.cs` (active-board load, POST) | role-match |
| `QuestBoard.Service/ViewModels/MoveViewModels/*.cs` | view model | transform | `ViewModels/PlatformViewModels/*` | role-match |
| `QuestBoard.Service/Views/Move/Index.cshtml` | view (desktop) | request-response | `Views/Quest/Details.cshtml` card layout, `Events/Details.cshtml` col-lg-4/8 | role-match |
| `QuestBoard.Service/Views/Move/Index.Mobile.cshtml` | view (mobile) | request-response | `Views/Quest/CreateFollowUp.Mobile.cshtml` | exact |
| `QuestBoard.Service/wwwroot/css/move-entry.css` | css | n/a | `wwwroot/css/modern-card.css`; linked in `_Layout.cshtml` 20-32 | role-match |
| `QuestBoard.Service/wwwroot/css/move-entry.mobile.css` | css | n/a | `wwwroot/css/quest-followup.mobile.css` (`quest-followup-card-mobile`) | exact |
| `Views/Shared/_Layout.cshtml` (modified, one `<link>`) | layout | n/a | the `<link>` lines 20-32 | exact |
| `Views/Shared/_Toasts.cshtml` (modified, one block) + `Constants/TempDataKeys.cs` (3 keys) | view/constants | n/a | board-switch toast block lines 78-105; keys lines 15-17 | exact |
| `Views/Quest/Details.cshtml`, `Details.Mobile.cshtml`, `Views/Events/Details.cshtml` (modified, control) | view | request-response | DM Controls card (Details.cshtml 750-764), mobile Manage Quest (Details.Mobile 438-445), Events Actions card (Events/Details 53-61) | exact |
| `QuestBoard.Domain/Extensions/ServiceExtensions.cs`, `QuestBoard.Repository/Extensions/ServiceExtensions.cs` (modified) | config (DI) | n/a | existing `AddScoped` lists | exact |
| `QuestBoard.IntegrationTests/Controllers/MoveControllerIntegrationTests.cs` (+ factory subclass with recording dispatcher) | test | request-response | `Controllers/LinkedBoardsPlatformIntegrationTests.cs` | exact |
| `QuestBoard.UnitTests/Services/BoardMoveServiceTests.cs`, repository tests | test | transform | `QuestBoard.UnitTests/Services/UserServiceTests.cs` (NSubstitute) | role-match |

## Pattern Assignments

### `QuestBoard.Repository/BoardMoveRepository.cs` (repository, one unit of work)

**Analog:** `QuestBoard.Repository/EventSeriesRepository.cs`

**Class shape / imports** (lines 1-18):
```csharp
using AutoMapper;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.Repository.Entities;
using Microsoft.EntityFrameworkCore;

namespace QuestBoard.Repository;

internal class EventSeriesRepository(QuestBoardContext dbContext, IMapper mapper, IEventRepository eventRepository)
    : BaseRepository<EventSeries, EventSeriesEntity>(dbContext, mapper), IEventSeriesRepository
```
BoardMove has no single entity; take `QuestBoardContext dbContext` (and `ILogger` if logging here) directly rather than `BaseRepository`. Use `internal sealed`.

**Tracked load, mutate, one save** (EventSeriesRepository.cs 165-188):
```csharp
var occurrences = await DbContext.Events
    .Where(e => e.SeriesId == seriesId)
    .ToListAsync(token);
foreach (var occurrence in occurrences)
{
    occurrence.SeriesId = null;
    occurrence.SeriesSlotIndex = null;
}
DbSet.Remove(series);
await DbContext.SaveChangesAsync(token);
```
Copy this loaded-tracked-mutate-single-save shape. Order for the move: Include signups and `DateVotes`, load follow-ups by `OriginalQuestId`, validate against the plan, `Remove` dropped signups plus `RemoveRange(signup.DateVotes)`, `CharacterId = null`, promoted `IsSelected = true`, cut links, `GroupId = target` LAST, one save. Banned shapes (FeedRevisionWriteSeamTests, empty allowlist): `ExecuteUpdate`, `.Update(`, `.UpdateRange(`, `.Attach(`, `.Entry(`, `.TrackGraph(`, `ExecuteSql`, `.State =`. No `IgnoreQueryFilters()` (UserGroups/Groups are unfiltered).

**Campaign auto-row shape to reproduce** (`QuestBoard.Repository/EventRepository.cs` 50-63):
```csharp
foreach (var memberId in memberIds.Distinct())
{
    entity.Signups.Add(new EventSignupEntity
    {
        UserId = memberId,
        Availability = (int)VoteType.Yes
    });
}
```
Add the same object (no `UpdatedAt`) to the tracked event's `Signups` for each target member with no row.

**Transactions:** not needed. If ever required, guard with `IsRelational()` (InMemory rejects them), as `CreateWithOccurrencesAsync` does.

---

### `QuestBoard.Domain/Services/BoardMoveService.cs` (service)

**Analog:** `QuestBoard.Domain/Services/EventSeriesService.cs` (constructor, `IBoardClock`, `userRepository.GetAllGroupMembers`) and `QuestService.cs` 300-345 (waitlist rule).

**Primary-constructor, internal class** (EventSeriesService.cs 8-16):
```csharp
internal class EventSeriesService(
    IEventSeriesRepository repository,
    IEventRepository eventRepository,
    IUserRepository userRepository,
    IBoardTypeResolver boardTypeResolver,
    IActiveGroupContext activeGroupContext,
    IOptions<EventSeriesOptions> options,
    IBoardClock boardClock) : IEventSeriesService
```
Inject `IBoardClock` (never `DateTime.Today`), `IUserRepository`, `ILinkedBoardSetRepository`, `IQuestRepository`/`IEventRepository` (reads), `IBoardMoveRepository`, `IQuestEmailDispatcher`, `IIdentityService`, `ILogger<BoardMoveService>`.

**Member read** (EventSeriesService.cs 73): `(await userRepository.GetAllGroupMembers(groupId, token)).Select(member => member.Id).ToList()`; returns every `UserGroups` member of any role, no filter bypass.

**Target set read (fresh per request, D-02):** `ILinkedBoardSetRepository.GetLinkedBoardsForViewerAsync(activeGroupId, viewerUserId)` (`QuestBoard.Repository/LinkedBoardSetRepository.cs` 244-262). It returns `ViewerLinkedBoards.Empty` for a board in no set, a set of one, or a type mismatch; "target is in `.Boards`" is the whole D-02 check, and `LinkedBoard.ViewerIsMember` answers the toast-link question.

**D-03 DM rule (raw role plus SuperAdmin addendum).** There is NO existing by-user-id check of the Identity role in production code:
- `IUserService.GetEffectiveGroupRoleAsync(ClaimsPrincipal, groupId)` (`UserService.cs` 61-67) does `if (user.IsInRole("SuperAdmin")) return GroupRole.Admin;`, but needs a principal.
- `IUserService.GetGroupRoleByIdAsync(int userId, int groupId)` (`UserService.cs` 78-81) and `IUserRepository.GetGroupRoleAsync(userId, groupId)` (`UserRepository.cs` 78-85) take a user id but return only the raw `UserGroups` role (null for a SuperAdmin who is not a member).
- `SuperAdmin` is a real Identity role (`AspNetUserRoles`); the only by-id `IsInRoleAsync` shape is in tests (`QuestBoard.IntegrationTests/Helpers/AuthenticationHelper.cs` 96-107, using `UserManager<UserEntity>.FindByIdAsync` then `AddToRoleAsync`). `IdentityService` already wraps `UserManager` and has by-id methods (`ChangePasswordAsync(int userId, ...)`, `UserRepository`-free).

So the planner must add one method. Copy the `IdentityService` by-id shape (`QuestBoard.Repository/IdentityService.cs` 20-26):
```csharp
public async Task<IdentityResult> ChangePasswordAsync(int userId, string oldPassword, string newPassword)
{
    var entity = await userManager.FindByIdAsync(userId.ToString());
    if (entity == null)
        return IdentityResult.Failed(new IdentityError { Description = "User not found." });
    return await userManager.ChangePasswordAsync(entity, oldPassword, newPassword);
}
```
New method (suggested): `Task<bool> IsSuperAdminAsync(int userId)` on `IIdentityService`, implemented as `var entity = await userManager.FindByIdAsync(userId.ToString()); return entity != null && await userManager.IsInRoleAsync(entity, "SuperAdmin");`. Then the DM rule is `isSuperAdmin || role is GroupRole.DungeonMaster or GroupRole.Admin` (GroupRole: Player=0, DungeonMaster=1, Admin=2). Mock `IIdentityService` with NSubstitute as `UserServiceTests.cs` does (line 15/23). Note `IIdentityService` is in `QuestBoard.Domain/Interfaces`, so the Domain service can use it directly.

**Waitlist rule applied in memory (D-06)**, mirror of `QuestService.cs` 300-345; selection part only, no repository writes:
```csharp
var finalizedProposedDate = quest.ProposedDates
    .FirstOrDefault(pd => quest.FinalizedDate.HasValue && pd.Date.Date == quest.FinalizedDate.Value.Date);
if (finalizedProposedDate == null) return;
...
candidate.IsSelected = true;
// Selection is never gated on email eligibility, only the email send is.
if (string.IsNullOrEmpty(candidate.Player.Email) || !candidate.Player.EmailConfirmed) return;
dispatcher.EnqueueWaitlistPromotedEmail(quest.Id, quest.GroupId, quest.FinalizedDate.Value,
    candidate.Player.Email!, candidate.Player.Name, quest.Title,
    quest.DungeonMaster?.Name ?? "Unknown DM", quest.Description, quest.ChallengeRating);
```
Do NOT call `PromoteNextWaitlistedPlayerIfSeatFreedAsync` (private, filtered reads, per-candidate saves). Use `WaitlistOrdering.OrderWaitlist(finalizedPd.Id)` on kept Player-role, unselected signups voting Yes/Maybe. When enqueueing for the promoted player pass the TARGET board id as `groupId`; enqueue only after the repository save returns, each in try/catch with log.

**Log line (D-19):** structured `logger.LogInformation("Moved {EntryType} {EntryId} from board {SourceGroupId} to {TargetGroupId} by {ActorUserId}: kept {Kept}, dropped {Dropped}, promoted {Promoted}, links cut {LinksCut}", ...)`. No planning IDs in the message or comments.

---

### `QuestBoard.Domain/Extensions/MoveEligibility.cs` (utility, pure)

**Analog:** `QuestBoard.Domain/Extensions/QuestExtensions.cs` `HasBeenPlayed(boardToday)` (research quotes `quest is { IsFinalized: true, FinalizedDate: { } d } && DateOnly.FromDateTime(d) <= boardToday.AddDays(-1)`). Take `DateOnly boardToday` as a parameter; never read an ambient clock. Check order: quest closed, then played; event series (`SeriesId != null`), then past (`Date < boardToday`). Cancelled events are eligible. `AmbientClockSeamTests` guards `QuestController.cs`, `EventsController.cs`, `QuestService.cs`, `QuestRepository.cs` and both Quest Details views, so keep the rule in this new file and pass `boardToday` from the controller via `IBoardClock`.

---

### `QuestBoard.Service/Controllers/MoveController.cs` (controller)

**Analog:** `QuestBoard.Service/Areas/Platform/Controllers/LinkedBoardsController.cs` (policy plus TempData), `Controllers/Events/SeriesController.cs` (active-board entry load, `[HttpPost][ValidateAntiForgeryToken]`).

**Auth + class shape** (LinkedBoardsController.cs 1-13):
```csharp
[Authorize(Policy = "SuperAdminOnly")]
public class LinkedBoardsController(ILinkedBoardSetService linkedBoardSetService) : Controller
```
Not an Area. Put `[Authorize(Policy = "SuperAdminOnly")]` at class level (policy is `RequireRole("SuperAdmin")`, `Program.cs` 90-91; failures go to `/Account/AccessDenied`). Never key on `CanManage`.

**POST shape** (LinkedBoardsController.cs 29-47):
```csharp
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> Create(LinkedBoardSetCreateViewModel model)
{
    ...
    TempData["Error"] = ...;
    return RedirectToAction(nameof(Index));
}
```
`AntiForgeryTokenCoverageTests` fails on any POST without `[ValidateAntiForgeryToken]`. Toasts use `TempData["Success"]` / `TempData["Error"]` plus the three new `TempDataKeys` for the Moved toast. Return 404 when the entry is not on the active board (loaded under the source filter). Do not declare any type starting with `CrossBoardLink` (`LinkedBoardStaticGuardTests` 251-253), and add NO route to `CrossBoardLinkRegistry` (pinned at 18).

SuperAdmin check in views/controllers: `User.IsInRole("SuperAdmin")` (e.g. `QuestController.cs` 48, `EventsController.cs` 571).

---

### Dispatcher and job (`IQuestEmailDispatcher`, Hangfire, Null, `QuestMovedEmailJob`)

**Analog:** `EnqueueWaitlistPromotedEmail` in `IQuestEmailDispatcher.cs` 36-56, `HangfireQuestEmailDispatcher.cs` 47-62, `NullQuestEmailDispatcher.cs` 40, and `QuestWaitlistPromotedEmailJob.cs`.

**Dispatcher** (HangfireQuestEmailDispatcher.cs 47-62):
```csharp
public void EnqueueWaitlistPromotedEmail(int questId, int groupId, DateTime finalizedDate,
    string recipientEmail, string playerName, string questTitle, string dmName,
    string questDescription, int challengeRating)
{
    jobClient.Enqueue<QuestWaitlistPromotedEmailJob>(j => j.ExecuteAsync(
        questId, groupId, finalizedDate, recipientEmail, playerName,
        questTitle, dmName, questDescription, challengeRating,
        CancellationToken.None));
}
```
Add NEW methods (`EnqueueQuestMovedEmail`, `EnqueueWaitlistPromotedAfterMoveEmail`); do not add optional params to existing ones (expression trees forbid optional args, and queued jobs must still bind). Implement all three: interface, Hangfire, Null (the `Testing` environment registers `Null`).

**Job** (QuestWaitlistPromotedEmailJob.cs 10-52): primary constructor `(IServiceScopeFactory scopeFactory, ILogger<...> logger)`, `HangfireJobHelper.RunInScopeAsync(scopeFactory, groupId, async sp => { render via IEmailRenderService.RenderAsync<T>(new Dictionary<string, object?>{ { nameof(T.Param), value } }); emailService.SendAsync(to, subject, html); logger.LogInformation(...) })`. Questurl is `$"{emailSettings.AppUrl}/Quest/Details/{questId}"`. One job per recipient (singular recipient, idempotent retries). Pass the target `groupId`. The new waitlist-after-move method on the existing job passes `{ nameof(WaitlistPromoted.MovedToBoardName), movedToBoardName }`.

---

### `QuestBoard.Service/Components/Emails/QuestMoved.razor` and `WaitlistPromoted.razor`

**Analog:** `Components/Emails/WaitlistPromoted.razor` lines 1-50 (poster table, 40/26/15 sizes, `_EmailLayout Subject= PreviewText=`, `@QuestDate:dddd, MMMM d` formatted in the template, CTA/divider rows) and `QuestDateChanged.razor` for the info table. Format `SessionDate` in the template with `ToString("dddd, MMMM d")`, never convert zones. Add `[Parameter] public string? MovedToBoardName { get; set; }` to `WaitlistPromoted.razor`; render the note paragraph only when non-null so existing output stays byte-identical. Render pipeline: the job above.

**EmailPreviewController** (`Controllers/Admin/EmailPreviewController.cs`): copy the `WaitlistPromoted()` action (line 168-175, `emailRenderService.RenderAsync<Components.Emails.WaitlistPromoted>(new() { [nameof(...QuestTitle)] = "The Tomb of Annihilation", ... })`) and add index links beside line 52. WARNING: the file must keep exactly five `DateTime.Today` occurrences (guard test); it already has them at lines 70, 88, 89, 157, 175. For the new actions use `DateTime.UtcNow`-free constants or a fixed sample date such as `new DateTime(2026, 1, 1)` plus `.AddDays`, NOT `DateTime.Today`, and check `AmbientClockSeamTests` for `DateTime.UtcNow`/`Now` bans on that path first.

---

### Views: `Move/Index.cshtml` and `Move/Index.Mobile.cshtml`

**Mobile analog (exact):** `Views/Quest/CreateFollowUp.Mobile.cshtml` lines 1-40:
```cshtml
@section Styles {
    <link href="~/css/quest-followup.mobile.css" asp-append-version="true" rel="stylesheet" />
}
<div class="container-fluid px-2 mt-2">
    <div class="quest-followup-card-mobile mb-3">
        <h5 class="mb-0"><i class="fas fa-scroll text-warning me-2"></i>Create Follow-Up Quest</h5>
```
Replace with `move-entry.mobile.css` and `move-card-mobile`; footer `d-flex gap-2` with Back (`btn btn-secondary flex-fill`) and confirm form (`flex-fill`).

**Desktop analog:** `Events/Details.cshtml` (`col-lg-4` / `col-lg-8` pairing, `card modern-card`, `card-header modern-card-header`); forms use `asp-action`/tag-helper antiforgery. Dates via `Html.WallClock`, never `.ToString()`. Read `.claude/ui-guidelines.md` before writing. Obey the UI-SPEC cascade trap: no `<span>`, `.text-muted`, `fw-bold`, bare `<strong>` inside `.modern-card`.

**CSS:** `move-entry.css` linked in `_Layout.cshtml` with the sheets at lines 20-32 (`<link rel="stylesheet" href="~/css/X.css" asp-append-version="true" />`); `move-entry.mobile.css` copies the `quest-followup-card-mobile` rule from `wwwroot/css/quest-followup.mobile.css`. No `@media`, no inline style.

---

### Details controls (3 view edits)

Desktop Quest (`Views/Quest/Details.cshtml` 750-764), current DM Controls card:
```cshtml
@if ((bool)ViewBag.CanManage)
{
    <div class="card modern-card mb-3">
        ...
        <a href="@Url.Action("Manage", "Quest", new { id = Model.Quest?.Id })" class="btn btn-primary w-100">
            <i class="fas fa-cog me-2"></i> Manage Quest
        </a>
```
Add the Move control below Manage with `mt-2`, gated on `User.IsInRole("SuperAdmin")` AND a linked-board flag supplied by the controller/ViewBag (not `CanManage`). Mobile Quest (`Details.Mobile.cshtml` 438-445): insert directly after the `Manage Quest` `<a ... class="btn btn-warning w-100 mb-2">`. Event (`Events/Details.cshtml` 53-61): last item in the Actions card. `AmbientClockSeamTests` requires both quest views to keep the token `ViewBag.BoardToday`; compute eligibility server-side (controller via `IBoardClock`) and pass the reason string.

---

### `_Toasts.cshtml` + `TempDataKeys.cs`

**Analog:** board-switch block (`Views/Shared/_Toasts.cshtml` 78-105) and keys (`Constants/TempDataKeys.cs` 15-17):
```csharp
public const string BoardSwitchTargetName = "BoardSwitchTargetName";
```
```cshtml
@if (TempData[TempDataKeys.BoardSwitchTargetName] != null)
{
    <div class="toast show board-switch-toast" role="alert" data-bs-autohide="false">
        <div class="toast-header bg-info text-white"> ... </div>
        <div class="toast-body"> ... <button type="submit" class="btn btn-sm btn-dark"> ...
```
New block: header `bg-success text-white`, `fa-check-circle`, title `Moved`; body sentence plus an `<a class="btn btn-sm btn-dark">Open it on {Board}</a>` only when the entry-URL key is set. Add three keys: message, entry URL, target board name. A filled `btn-dark` (not outline) is deliberate; see the comment in the existing block.

---

### Tests

**Analog:** `QuestBoard.IntegrationTests/Controllers/LinkedBoardsPlatformIntegrationTests.cs`
```csharp
public class LinkedBoardsPlatformIntegrationTests : IClassFixture<WebApplicationFactoryBase>
private static CancellationToken Token => TestContext.Current.CancellationToken;
await using var context = _factory.Database.CreateContext();
context.Groups.Add(new GroupEntity { Id = id, Name = name, BoardType = (int)boardType, CreatedAt = DateTime.UtcNow, LinkedBoardSetId = linkedBoardSetId });
...
await TestDataHelper.ClearDatabaseAsync(_factory.Services);
var (client, _) = await AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(_factory);
var response = await client.PostAsync(url, Form(("Key","Value")), Token);
response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found);
```
SuperAdmin test users are real Identity role rows (`AuthenticationHelper.cs` 96-107 via `UserManager.AddToRoleAsync`); a SuperAdmin DM in the D-03 addendum test is built the same way. Reset `ActiveGroupId` to 1 in `DisposeAsync` (shared `TestGroupContext` singleton). The base factory (`WebApplicationFactoryBase.cs` 43, 47, 70) uses env `Testing` which registers `NullQuestEmailDispatcher`; D-21 #7 and #12 need a factory subclass overriding `IQuestEmailDispatcher` through `ConfigureTestServices` (line 70 is the hook) with a recording stub. Chain asserts check stored `OriginalQuestId` null (InMemory has no unique index). Unit tests: NSubstitute substitutes as in `UserServiceTests.cs` (`Substitute.For<IIdentityService>()`), `FakeBoardClock` for the clock. Add a parity test between the in-memory waitlist selection and `GetTopWaitlistedCandidateAsync`.

## Shared Patterns

### Super admin gate
**Source:** `QuestBoard.Service/Program.cs` 90-91 (policy), `LinkedBoardsController.cs` 10 (`[Authorize(Policy = "SuperAdminOnly")]`)
**Apply to:** `MoveController` class, all GET and POST; Details controls additionally check `User.IsInRole("SuperAdmin")`.

### Fresh linked-set read
**Source:** `ILinkedBoardSetRepository.GetLinkedBoardsForViewerAsync(activeGroupId, viewerUserId)` (`LinkedBoardSetRepository.cs` 244-292)
**Apply to:** GET, POST, and Details control visibility (D-13: show nothing when `Empty`). Never from session or claims.

### Change-tracker-only writes
**Source:** `EventSeriesRepository.cs` 165-188; guard `QuestBoard.UnitTests/Architecture/FeedRevisionWriteSeamTests.cs`
**Apply to:** `BoardMoveRepository` only. `FeedRevisionStamper` raises `FeedRevision` from the tracked `GroupId` change; do not edit `FeedRevisionStamper.cs`, `CalendarFeedWriter.cs`, `CalendarSubscriptionService.cs`.

### Board clock
**Source:** `EventSeriesService.cs` (`boardClock.Today`); guard `AmbientClockSeamTests`
**Apply to:** `MoveEligibility` callers, controller, service. No `DateTime.Now/UtcNow/Today` in new production code on guarded paths.

### Hangfire group scope
**Source:** `HangfireJobHelper.RunInScopeAsync(scopeFactory, groupId, ...)`
**Apply to:** `QuestMovedEmailJob` and the waitlist-after-move method; always the TARGET board id.

### Guard-test constraints to design around
- `CrossBoardLinkRegistryTests`: exactly 18 routes, add none.
- `LinkedBoardStaticGuardTests`: no new type starting with `CrossBoardLink`.
- `CrossBoardIgnoreQueryFiltersSeamTests`: no new `IgnoreQueryFilters()` file (not needed).
- `EmailPreviewController`: exactly five `DateTime.Today`.
- `AntiForgeryTokenCoverageTests`: every POST needs `[ValidateAntiForgeryToken]`.
- CLAUDE.md: no GSD IDs (D-xx, phase numbers) in source comments or strings.

## No Analog Found

| File | Role | Data Flow | Reason |
|---|---|---|---|
| By-user-id SuperAdmin check (`IIdentityService.IsSuperAdminAsync` or similar) | service | request-response | Production code only checks the role on the current principal (`User.IsInRole`, `UserService.GetEffectiveGroupRoleAsync`). `GetGroupRoleByIdAsync`/`GetGroupRoleAsync(userId, groupId)` read only `UserGroups` and return null for a non-member SuperAdmin. Closest shape to copy: `IdentityService` by-id methods using `userManager.FindByIdAsync` (+ `IsInRoleAsync`); test seeding in `AuthenticationHelper.cs` 96-107. |
| Preview stamp (SHA-256 of consequence set) | utility | transform | Nothing equivalent exists; use BCL `SHA256.HashData` per RESEARCH Pattern 7. |

## Metadata

**Analog search scope:** QuestBoard.Domain (Services, Interfaces, Extensions), QuestBoard.Repository, QuestBoard.Service (Controllers, Areas/Platform, Jobs, Services, Components/Emails, Views, Constants), QuestBoard.IntegrationTests (Controllers, Helpers, factory), QuestBoard.UnitTests (Services).
**Files scanned:** about 30 read in part.
**Pattern extraction date:** 2026-10-18
**Caveat:** RESEARCH.md was read only to line 506 of 814 (Pitfalls 3+, Validation Architecture, Open Questions not read); the planner should read the remainder.
