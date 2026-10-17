# Phase 91: Linked Boards with a Shared Calendar - Pattern Map

**Mapped:** 2026-10-16
**Files analyzed:** 33 (new + modified)
**Analogs found:** 33 / 33 (new Platform page, set repo/service and linked reads all have close precedent; no file lacks an analog)

Design decisions (nullable `LinkedBoardSetId` on `Groups`, set stores `BoardType`, reads inside the already-allowlisted `QuestRepository.cs` / `EventRepository.cs`, separate linked collections on the view model) come from 91-RESEARCH.md and are not re-argued here. CONTEXT.md D-10/D-14/D-15/D-19/D-20 override the body of RESEARCH.md where they differ: cancelled linked events ARE included (carry cancellation state, no `CancelledAt == null` predicate), no board-name line on desktop chips, and the linked quest projection must carry `DungeonMasterSession` with the viewer's per-board role. Excerpts below are from the real codebase.

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match |
|---|---|---|---|---|
| `QuestBoard.Repository/Entities/LinkedBoardSetEntity.cs` (new) | model (entity) | CRUD | `Entities/GroupEntity.cs`, `CalendarSubscriptionEntity.cs` | exact |
| `QuestBoard.Repository/Entities/GroupEntity.cs` (mod) | model | CRUD | itself (add nullable FK + nav) | exact |
| `QuestBoard.Repository/Entities/QuestBoardContext.cs` (mod) | config | - | CalendarSubscription config block (lines 60, 619-624), GroupEntity block (~295-310) | exact |
| `QuestBoard.Repository/Migrations/<ts>_AddLinkedBoardSets*.cs` + snapshot | migration | batch | `20260918080027_AddCalendarSubscriptions.cs` (CreateTable), `20260703113120_AddBoardTypeToGroup.cs` (AddColumn on Groups) | exact |
| `QuestBoard.Repository/LinkedBoardSetRepository.cs` (new) | repository | CRUD | `GroupRepository.cs` | exact |
| `QuestBoard.Repository/QuestRepository.cs` (mod: `GetLinkedQuestsForCalendarAsync`) | repository | request-response, cross-board read | `GetFeedQuestsForUserAsync` (lines 277-300) + `GetQuestsWithSignupsForRoleAsync` (line 70, DM-only rule) | role-match |
| `QuestBoard.Repository/QuestRepository.cs` (mod: DM-only filter on `GetQuestsForCalendarAsync`) | repository | request-response | `GetQuestsWithSignupsForRoleAsync` (lines 70-79) | exact |
| `QuestBoard.Repository/EventRepository.cs` (mod: `GetLinkedEventsForCalendarAsync`) | repository | request-response, cross-board read | `GetUpcomingAcrossGroupsWithSignupsAsync` (lines 160-185) | exact |
| `QuestBoard.Repository/Automapper/EntityProfile.cs` (mod) | config | transform | `CreateMap<Group, GroupEntity>` (lines 210-215) | exact |
| `QuestBoard.Repository/Extensions/ServiceExtensions.cs` (mod) | config | - | line 29 `AddScoped<IGroupRepository, GroupRepository>()` | exact |
| `QuestBoard.Domain/Models/LinkedBoardSet.cs`, `LinkedBoard.cs`, `LinkedQuestEntry.cs`, `LinkedEventEntry.cs`, `LinkedCalendarData.cs` (new) | model | transform | `Models/Group.cs`, `GroupWithMemberCount` | role-match |
| `QuestBoard.Domain/Interfaces/ILinkedBoardSetRepository.cs`, `ILinkedBoardSetService.cs`, `ILinkedCalendarService.cs` (new); `IQuestRepository`/`IEventRepository` (mod) | interface | - | `IGroupRepository` / `IGroupService` | exact |
| `QuestBoard.Domain/Services/LinkedBoardSetService.cs` (new) | service | CRUD | `Services/GroupService.cs` | exact |
| `QuestBoard.Domain/Services/LinkedCalendarService.cs` (new) | service | request-response, cross-board read + re-check | `Services/EventService.cs` lines 85-125 (agenda re-check) | exact |
| `QuestBoard.Domain/Extensions/ServiceExtensions.cs` (mod) | config | - | line 57 `AddScoped<IGroupService, GroupService>()` | exact |
| `QuestBoard.Service/Areas/Platform/Controllers/LinkedBoardsController.cs` (new) | controller | CRUD | `Areas/Platform/Controllers/GroupController.cs` | exact |
| `QuestBoard.Service/ViewModels/PlatformViewModels/LinkedBoards*ViewModel.cs` (new) | view model | request-response | `GroupCreateViewModel.cs`, `GroupListViewModel` | exact |
| `QuestBoard.Service/Areas/Platform/Views/LinkedBoards/Index.cshtml` + `Index.Mobile.cshtml` (new) | view | request-response | `Platform/Views/Group/Index.cshtml` + `Index.Mobile.cshtml` | exact |
| `Platform/Views/Group/Index.cshtml` + `Index.Mobile.cshtml` (mod: entry button) | view | - | header button group lines 12-19 | exact |
| `QuestBoard.Service/Automapper/ViewModelProfile.cs` (mod, only if domain models are mapped) | config | transform | existing Group maps | role-match |
| `QuestBoard.Service/Controllers/QuestBoard/CalendarController.cs` (mod) | controller | request-response | itself, lines 19-75 | exact |
| `QuestBoard.Service/Controllers/QuestBoard/QuestController.cs` `Details` (mod, ~lines 310-385) | controller | request-response | itself | exact |
| `QuestBoard.Service/ViewModels/CalendarViewModels/CalendarViewModel.cs`, `CalendarDay.cs` (mod); `LinkedQuestOnDay.cs`, `LinkedEventOnDay.cs` (new) | view model | transform | `CalendarViewModel.cs`, `CalendarDay.cs`, `EventOnDay.cs` | exact |
| `QuestBoard.Service/Views/Shared/_Calendar.cshtml` (mod) + new `_LinkedDayEntries.cshtml` | view | request-response | `_Calendar.cshtml` day cell (`day-cell-items`) | exact |
| `QuestBoard.Service/Views/Shared/_Calendar.Mobile.cshtml` (mod) | view | request-response | itself (`calendar-date-entry-mobile`) | exact |
| `QuestBoard.Service/Views/Calendar/Index.cshtml` (mod: Legend) and `Index.Mobile.cshtml` (mod) | view | request-response | Legend card line ~70; agenda line 9, loops 94/105 | exact |
| `wwwroot/css/calendar.css`, `calendar.mobile.css`, `quests.mobile.css`, platform mobile css (mod) | config (CSS) | - | existing `.legend-item.*`, `.quest-event`, `platform-group.mobile.css` | role-match |
| Tests (see section at end) | test | - | listed there | exact |

## Pattern Assignments

### `LinkedBoardSetEntity.cs` + `GroupEntity.cs` + `QuestBoardContext.cs` (entity/config, CRUD)

**Analog:** `GroupEntity` (no query filter, `[Table]`, `IEntity`) and the `CalendarSubscriptionEntity` configuration.

Entity shape (from RESEARCH, matches `GroupEntity.cs:6-24` conventions):
```csharp
[Table("LinkedBoardSets")]
public class LinkedBoardSetEntity : IEntity
{
    [Key][DatabaseGenerated(DatabaseGeneratedOption.Identity)] public int Id { get; set; }
    [Required][StringLength(100)] public string Name { get; set; } = string.Empty;
    public int BoardType { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public virtual ICollection<GroupEntity> Boards { get; set; } = [];
}
// GroupEntity: public int? LinkedBoardSetId { get; set; }  + [ForeignKey] virtual LinkedBoardSetEntity? LinkedBoardSet
```
Context config, copy shape of `QuestBoardContext.cs:60` (DbSet) and `:619-624`:
```csharp
public DbSet<CalendarSubscriptionEntity> CalendarSubscriptions { get; set; }
...
modelBuilder.Entity<CalendarSubscriptionEntity>()
    .HasIndex(cs => cs.Token)
    .IsUnique();
```
Add: `DbSet<LinkedBoardSetEntity> LinkedBoardSets`; `HasIndex(s => s.Name).IsUnique()`; `Entity<GroupEntity>().HasOne(g => g.LinkedBoardSet).WithMany(s => s.Boards).HasForeignKey(g => g.LinkedBoardSetId).OnDelete(DeleteBehavior.SetNull)`. Do NOT add a `HasQueryFilter` to either table (Groups and UserGroups are unfiltered by design; the set repo then needs no filter bypass). Pitfall: the model snapshot must match exactly (`MigrationPreflightTests.ShippedModel_HasNoPendingModelChanges`).

### Migration `<ts>_AddLinkedBoardSets` (migration, batch)

**Analogs:** `20260918080027_AddCalendarSubscriptions.cs` (CreateTable + index) and `20260703113120_AddBoardTypeToGroup.cs` (AddColumn to Groups). Newest id is `20260930161610_AddFeedEntryRevisions`; the new timestamp must sort after it. Generate with `dotnet ef migrations add AddLinkedBoardSets --project ../QuestBoard.Repository` from `QuestBoard.Service/` (never hand-write the Designer/snapshot). Expected generated shape:
```csharp
using System;
using Microsoft.EntityFrameworkCore.Migrations;
#nullable disable
namespace QuestBoard.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddLinkedBoardSets : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LinkedBoardSets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ...
                },
                constraints: table => { table.PrimaryKey("PK_LinkedBoardSets", x => x.Id); });
            migrationBuilder.AddColumn<int>(name: "LinkedBoardSetId", table: "Groups", type: "int", nullable: true);
            // CreateIndex (unique Name), CreateIndex(Groups.LinkedBoardSetId), AddForeignKey ... onDelete: ReferentialAction.SetNull
        }
        // symmetrical Down
    }
}
```
UTF-8 BOM, `#nullable disable`, `/// <inheritdoc />`. No `ALTER DATABASE|FULLTEXT|MEMORY_OPTIMIZED` (`ShippedMigrations_NoneNeedToRunOutsideATransaction`).

### `LinkedBoardSetRepository.cs` (repository, CRUD)

**Analog:** `QuestBoard.Repository/GroupRepository.cs`

**Imports + shape** (lines 1-10):
```csharp
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.Repository.Entities;

namespace QuestBoard.Repository;

internal class GroupRepository(QuestBoardContext dbContext, IMapper mapper, IBoardClock boardClock)
    : BaseRepository<Group, GroupEntity>(dbContext, mapper), IGroupRepository
```
**Unfiltered projection read** (lines 13-26), the template for set listing with board counts and for "other boards in my set":
```csharp
return await DbContext.Groups
    .Select(g => new GroupWithMemberCount { Id = g.Id, Name = g.Name, CreatedAt = g.CreatedAt,
        MemberCount = g.UserGroups.Count, BoardType = (BoardType)g.BoardType })
    .ToListAsync(token);
```
**Rule to carry over** (lines 62-70 comment): "GroupEntity carries no query filter, so this read is correct regardless of which board the caller has selected" -- do NOT add a filter bypass to this file. Writes: load tracked entities and assign `board.LinkedBoardSetId = setId` then `SaveChangesAsync`; `.Update(`, `.Attach(`, `.Entry(`, `ExecuteUpdate`, `.State =` are banned in all production code by `FeedRevisionWriteSeamTests`. `DeleteSetAsync` must `Include(s => s.Boards)` tracked before `Remove` so InMemory also un-links. Linked-board lookup snippet: RESEARCH "Linked-board lookup" (Code Examples).

### `QuestRepository.GetLinkedQuestsForCalendarAsync` (repository, cross-board read)

**Analog:** `QuestRepository.GetFeedQuestsForUserAsync` (lines 277-300) for the pinned bypass shape; comment style to follow:
```csharp
// Rooted at Quests ... The filter bypass below is immediately re-narrowed by the caller-supplied
// board-id set, which is strictly narrower than the ambient filter for any single board ...
var entities = await DbContext.Quests
    .IgnoreQueryFilters()
    .Where(q => oneShotGroupIds.Contains(q.GroupId) && ...)
    .AsNoTracking()
    .ToListAsync(token);
```
Use a flat `.Select(q => new LinkedQuestEntry { QuestId, GroupId, Title, DungeonMasterName = q.DungeonMaster.Name, DungeonMasterSession = q.DungeonMasterSession, IsFinalized, FinalizedDate, ProposedDates = ... })`. Never `Include`, never `ProjectForCalendar` (it eagerly loads PlayerSignups/Votes/Players, lines 352-363). Do not copy `GetQuestsForTomorrowAllGroupsAsync` (lines 267-274, bare bypass with no group predicate). `ProposedDateEntity`'s own filter is `pd.Quest.GroupId == ActiveGroupId`, so the bypass must cover the whole query (a test must assert the entry lands on its proposed date). Only this file and `EventRepository.cs` may contain the token `IgnoreQueryFilters` outside a comment (`CrossBoardIgnoreQueryFiltersSeamTests`), so no edit to the allowlist.

**DM-only rule analog** (`QuestRepository.cs:70-79`, `GetQuestsWithSignupsForRoleAsync`):
```csharp
.Where(q => (!q.IsFinalized || (q.IsFinalized && q.FinalizedDate > oneDayAgo)) &&
            (!q.DungeonMasterSession || isAdminOrDm) && !q.IsClosed)
```
D-20 for the own calendar: change `GetQuestsForCalendarAsync` (lines 49-54, `ProjectForCalendar(DbContext.Quests).ToListAsync`) to accept `bool isAdminOrDm` and add `.Where(q => !q.DungeonMasterSession || isAdminOrDm)` (mirror just that clause; do not copy the `!q.IsClosed`/date clauses, the calendar keeps all). Update `IQuestRepository`, `IQuestService`/`QuestService` and callers (`CalendarController`, `QuestController.Details`). `IsDmTierAsync()` in `CalendarController` already computes the viewer's DM tier (SuperAdmin counts as Admin as on the quest board today; verify its role logic in the controller). For linked quests the role is per owning board: `LinkedCalendarService` must obtain the viewer's role on each linked board fresh (from `IGroupService`/`UserGroups`, same read that gives membership) and filter `DungeonMasterSession` rows after the query by that map, SuperAdmin treated as Admin.

### `EventRepository.GetLinkedEventsForCalendarAsync` (repository, cross-board read)

**Analog:** `EventRepository.GetUpcomingAcrossGroupsWithSignupsAsync` lines 160-185:
```csharp
// Scope is re-imposed immediately by memberGroupIds ... Stepping outside the ambient filter here
// disables it for the whole query ... every included row hangs off an event whose GroupId is
// already pinned to memberGroupIds.
var entities = await DbContext.Events
    .IgnoreQueryFilters()
    .Where(e => memberGroupIds.Contains(e.GroupId) && e.Date >= today && e.CancelledAt == null)
    .OrderBy(e => e.Date).ThenBy(e => e.StartTime).ThenBy(e => e.Id)
    ...
    .AsNoTracking()
    .ToListAsync(token);
```
Differences for this phase: drop `CancelledAt == null` (cancelled linked events show struck through) and project `IsCancelled = e.CancelledAt != null`; no `Include(Signups)`; flat `Select` to `LinkedEventEntry { EventId, GroupId, Title, Date, StartTime, IsCancelled }`; window by month on `e.Date`.

### `LinkedCalendarService.cs` (service, cross-board read with fail-closed re-check)

**Analog:** `QuestBoard.Domain/Services/EventService.cs` lines ~85-125 (logger-injected, `internal` class, re-check then `LogError`):
```csharp
var fetched = await repository.GetUpcomingAcrossGroupsWithSignupsAsync(memberGroupIds, today, take + 1, token);
// Second-layer re-check ... reads the same memberGroupIds list the query itself was built from, so it
// catches a dropped predicate or a bad translation of the containment test -- it does not catch a
// wrong membership set, because both checks trust the same input.
var checkedRows = fetched.Where(row => memberGroupIds.Contains(row.Event.GroupId)).ToList();
if (checkedRows.Count != fetched.Count)
{
    logger.LogError("Cross-board agenda dropped {DroppedCount} of {FetchedCount} row(s) falling outside the caller's board set. " + "...",
        fetched.Count - checkedRows.Count, fetched.Count);
}
```
Copy this for quests and events; use `ILogger<LinkedCalendarService>`. Return `LinkedCalendarData.Empty` when `activeGroupId` is null (do NOT use `RequireActiveGroupId()`, which throws; `Details` is reachable without an active board) or when the set has fewer than two boards, with NO cross-board read (prove with `DidNotReceive`). Membership/role: `IGroupService.GetGroupsForUserAsync(userId)` (fresh `UserGroups`, same source `CrossBoardLinkResolverService` uses). Skip the quest read when the set `BoardType == Campaign`. Keep the literal token `IgnoreQueryFilters` out of this file entirely, including string literals.

### `LinkedBoardSetService.cs` (service, CRUD)

**Analog:** `QuestBoard.Domain/Services/GroupService.cs` (internal class, primary-constructor injecting the repository, thin pass-through with `/// <inheritdoc/>`):
```csharp
internal class GroupService(IGroupRepository repository, IMapper mapper)
    : BaseService<Group>(repository, mapper), IGroupService
{
    /// <inheritdoc/>
    public async Task<bool> HasMembersAsync(int groupId, CancellationToken token = default)
        => await repository.HasMembersAsync(groupId, token);
```
Add rule enforcement here (not only in the picker): result enum (`Added`, `TypeMismatch`, `AlreadyLinked`, `NotFound`), `board.BoardType == set.BoardType`, `board.LinkedBoardSetId == null`, name pre-check (`AnyAsync`) plus unique index, since InMemory does not enforce unique indexes.

### DI registration (config)

`QuestBoard.Repository/Extensions/ServiceExtensions.cs:29`: `services.AddScoped<IGroupRepository, GroupRepository>();` and `QuestBoard.Domain/Extensions/ServiceExtensions.cs:57`: `services.AddScoped<IGroupService, GroupService>();`. Add the three new pairs next to them.

### AutoMapper at both boundaries

**Entity boundary** (`EntityProfile.cs:210-215`):
```csharp
CreateMap<GroupEntity, Group>()
    .ForMember(dest => dest.BoardType, opt => opt.MapFrom(src => (BoardType)src.BoardType));
CreateMap<Group, GroupEntity>()
    .ForMember(dest => dest.BoardType, opt => opt.MapFrom(src => (int)src.BoardType));
```
Add `.ForMember(d => d.LinkedBoardSetId, o => o.Ignore()).ForMember(d => d.LinkedBoardSet, o => o.Ignore())` to `Group -> GroupEntity` so a rename through `BaseRepository.UpdateAsync` (`Mapper.Map(model, trackedEntity)`) cannot null the link; add `CreateMap<LinkedBoardSetEntity, LinkedBoardSet>` (BoardType int<->enum like Group) only if the set repo returns mapped models. The linked-read repos project to domain models directly with `Select`, so they need no map. The ViewModel boundary (`ViewModelProfile.cs`) only needs entries if platform view models are mapped; GroupController builds view models by hand (`new GroupEditViewModel { Id = group.Id ... }`), so copy that and add no map.

### `LinkedBoardsController.cs` (controller, CRUD)

**Analog:** `QuestBoard.Service/Areas/Platform/Controllers/GroupController.cs`

**Auth/header** (lines 15-23):
```csharp
[Area("Platform")]
[Authorize(Policy = "SuperAdminOnly")]
public class GroupController(IGroupService groupService, ..., ILogger<GroupController> logger) : Controller
```
**Core pattern** (lines 25-60): GET index builds a view model from a service call; POST `[HttpPost][ValidateAntiForgeryToken]`, `if (!ModelState.IsValid) return View(model);`, try/catch for duplicate name, `TempData["Success"|"Error"|"Warning"]`, `RedirectToAction(nameof(Index))`; unknown id -> `RedirectToAction(nameof(Index))`:
```csharp
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> Create(GroupCreateViewModel model)
{
    if (!ModelState.IsValid) return View(model);
    try { await groupService.AddAsync(...); TempData["Success"] = "Group created successfully."; return RedirectToAction(nameof(Index)); }
    catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("unique", StringComparison.OrdinalIgnoreCase) == true || ...)
    { ModelState.AddModelError(nameof(model.Name), "A group with that name already exists..."); return View(model); }
}
```
Route is `platform/{controller=Group}/{action=Index}/{id?}` (Program.cs:367-369), so the URL is `/platform/LinkedBoards/...`; `/platform` is exempt from `GroupSessionMiddleware`. Every POST needs `[ValidateAntiForgeryToken]` (`AntiForgeryTokenCoverageTests` sweeps by reflection). Copy copy-wording from the UI-SPEC ("Set" terminology, not "Group"; players never see set names). Board deletion (D-03): `GroupController.DeleteConfirmed` (lines 107-122) needs no change since the nullable FK column makes it automatic; add a test.

**Validation view model analog** (`GroupCreateViewModel.cs`):
```csharp
[Required(ErrorMessage = "Group name is required.")]
[StringLength(100, ErrorMessage = "Group name cannot exceed 100 characters.")]
public string Name { get; set; } = string.Empty;
[Required] [EnumDataType(typeof(BoardType), ErrorMessage = "Select a valid board type.")]
public BoardType? BoardType { get; set; }
```
Never bind board type on rename (`[BindNever]` precedent in `GroupEditViewModel`).

### Linked Boards views (view, request-response)

**Analog:** `Areas/Platform/Views/Group/Index.cshtml` and `Index.Mobile.cshtml`.
```html
<div class="card modern-card">
    <div class="card-header modern-card-header d-flex justify-content-between align-items-center">
        <h2 class="mb-0"><i class="fas fa-layer-group text-danger me-2"></i>Group Management</h2>
        <div class="d-flex gap-2">
            <a asp-controller="Group" asp-action="Create" asp-area="Platform" class="btn btn-success"><i class="fas fa-plus me-2"></i>Create Group</a>
            <a asp-controller="Users" asp-action="Index" asp-area="Platform" class="btn btn-secondary"><i class="fas fa-users-cog me-2"></i>Manage Users</a>
```
Then `card-body modern-card-body`, `table-responsive`, `badge bg-info text-dark` (Campaign) / `badge bg-primary` (One-Shot). Mobile twin: `@section Styles { <link href="~/css/platform-group.mobile.css" ...> }`, `platform-group-card-mobile`, `group-card-mobile`, `btn-sm`, inline destructive posts with `onsubmit="return confirm('...')"` (see `Group/Members.Mobile.cshtml`). No `@media` in mobile CSS. Follow 91-UI-SPEC section 1 for exact classes and copy; follow `.claude/ui-guidelines.md` (cancel left, primary right, `<hr>` before buttons). Entry point: add a button to the header `d-flex gap-2` group in both `Group/Index.cshtml` (lines 12-19) and `Group/Index.Mobile.cshtml`.

### `CalendarController.Index` (controller, request-response)

**Analog:** itself, `CalendarController.cs` lines 19-75. Imports: `QuestBoard.Domain.Interfaces`, `QuestBoard.Service.ViewModels.CalendarViewModels`. Add `ILinkedCalendarService` to the primary constructor. Core pattern to extend:
```csharp
var activeBoardType = await boardTypeResolver.GetBoardTypeAsync(token);
var includeQuests = activeBoardType != BoardType.Campaign;
IList<Quest> allQuests = includeQuests ? await questService.GetQuestsForCalendarAsync(token) : [];
var allEvents = await eventService.GetEventsForCalendarAsync(token);
var calendarModel = new CalendarViewModel { Year = selectedYear, Month = selectedMonth, Quests = [.. allQuests], Events = [.. allEvents], CanManage = await IsDmTierAsync() };
```
Add the linked read after these, window from `new DateTime(selectedYear, selectedMonth, 1)` to `+1 month` (never `DateTime.Now/Today/UtcNow`: the controller is on `AmbientClockSeamTests`; `boardClock.Now` is the allowed source), then set `LinkedQuests`, `LinkedEvents`, `LinkedBoards`. Pass the viewer id from the same user lookup the controller already uses.

### `QuestController.Details` (controller)

**Analog:** itself, lines ~310-385: `allQuests = await questService.GetQuestsForCalendarAsync(token)` (351), one `CalendarViewModel { Year, Month, Quests = allQuests.ToList() }` per month with a proposed date (353-363), `ViewBag.CalendarMonths`, `ViewBag.CurrentQuestId`, `ViewBag.IsDetailsPage = true`. Do a single linked read spanning first day of earliest month to the first day after the latest month, then distribute onto each month model; skip when there are no months. Must keep `ViewBag.BoardToday` and not introduce ambient clock reads (guarded file). Own events stay off Details.

### `CalendarViewModel`, `CalendarDay`, `LinkedQuestOnDay`, `LinkedEventOnDay` (view model, transform)

**Analogs:** `CalendarViewModel.cs`, `CalendarDay.cs`, `EventOnDay.cs`.

**Default-empty convention** (carry over, `CalendarViewModel.cs:11-17`):
```csharp
// Defaults to empty on purpose. The shared calendar partial is rendered from several places that
// build their own instance of this model and never set this collection ...
public List<Event> Events { get; set; } = new();
```
`CalendarDay.cs`: `public List<EventOnDay> EventsOnDay { get; set; } = [];` -> add `LinkedEventsOnDay`, `LinkedQuestsOnDay` the same way. In `GetCalendarDays()` (lines 51-60) build them next to `GetQuestsForDate(date)`/`GetEventsForDate(date)`, mirroring the `GetQuestsForDate` finalized rule (finalized only on its date and only if a proposed date exists that day; open quests one entry per proposed date) and `GetEventsForDate`'s sort (`StartTime ?? TimeOnly.MinValue`); `GetEventsForDate` is the single DateOnly<->DateTime conversion point, reuse it for linked events. Time text via a `TimeLabel` property modelled on:
```csharp
public string TimeLabel => Event.StartTime.HasValue ? Event.StartTime.Value.ToString("HH:mm") : "All day";
```
Linked types expose no vote/signup/player members (reflection guard test). The mobile agenda filter must include linked collections: `Index.Mobile.cshtml:9` currently `Model.GetCalendarDays().Where(d => !d.IsEmpty && (d.QuestsOnDay.Any() || d.EventsOnDay.Any()))`.

### `_Calendar.cshtml` + `_LinkedDayEntries.cshtml` (view, desktop)

**Analog:** `_Calendar.cshtml`. Day cell is `<div class="day-cell-items">` (~line 35) containing the `calendar-events` block (~42) then the `quest-events` block (~58); vote markup is inside `@if (isDetailsPage && questOnDay.Quest.Id == currentQuestId)` (~102). New slot: after the `quest-events` block, still inside `day-cell-items`, a `<div class="linked-day-entries">` rendered through a separate form-free partial so the vote branch is unreachable. Class names must not contain `calendar-event`/`calendar-events` (`EventCalendarPartialTests` negative assertions) and must not reuse `.quest-event`. Member entry: single `<a class="linked-entry ..." href="@Url.Action("Details","Quest",new{id})">` (or `Events`); non-member: non-interactive element, no `href`, no `onclick`, no `data-quest-id`. Count badge `linked-day-count` outside `day-cell-items`. Exact markup/CSS: 91-UI-SPEC section 2. Use `Html.WallClock`/server-side `TimeLabel`, never `DateTime.ToString` in views. Three Details call sites (`Details.cshtml` ~605, 649, 697) inherit this automatically.

### `_Calendar.Mobile.cshtml` (view, mobile Details)

**Analog:** itself: `calendar-date-entry-mobile` > `calendar-date-label-mobile` > vote rows, gated by `isDetailsPage && questOnDay.Quest.Id == currentQuestId`. Insert `div.also-that-day` lines directly after the date-label div, text/anchor only (the list sits inside the vote `<form>`). `Html.PartialAsync("_Calendar", ...)` resolves to the `.Mobile` twin via `MobileViewLocationExpander`.

### `Calendar/Index.cshtml` Legend and `Calendar/Index.Mobile.cshtml`

Legend row pattern: `<div class="d-flex align-items-center mb-2"><div class="legend-item proposed me-2"></div><small>Proposed</small></div>` (swatch classes `proposed|finalized|event|cancelled` in `calendar.css` ~450-473); add conditional block when `Model.LinkedBoards.Any()` after the Cancelled row. Mobile: events loop ~94, quests loop ~105, rows are `onclick="window.location.href=..."` plus inner `<a class="row-nav-link">` (copy the pair for member rows only); "Also showing:" line right after `div.calendar-month-nav` (lines 20-33).

### CSS

Add `.linked-*` rules to `wwwroot/css/calendar.css`, `calendar.mobile.css`, `quests.mobile.css` (also-that-day); mobile files carry a "No @media queries" rule and `MobileCssTests` guard integrity; keep 44px touch targets on tappable rows. Platform mobile page may link `platform-group.mobile.css` (generic classes) or a new `platform-linked-boards.mobile.css`.

## Shared Patterns

### Cross-board read confinement (all linked reads)
**Source:** `QuestBoard.UnitTests/Architecture/CrossBoardIgnoreQueryFiltersSeamTests.cs:16-23` (closed list: `EventSignupRepository`, `EventRepository`, `GroupRepository`, `QuestRepository`, `CrossBoardLinkRepository`). Equality assertion; the substring `IgnoreQueryFilters` outside comments fails the build in any other production `.cs`. Do not add the new reads to `CrossBoardLinkRepository.cs` (its header promises it never selects entity data).

### No feed-revision writes
**Source:** `FeedRevisionWriteSeamTests` (bans `.Update(`, `.UpdateRange(`, `.Attach(`, `.AttachRange(`, `.Entry(`, `.TrackGraph(`, `ExecuteUpdate`, `ExecuteSql`, `.State =` in all production code outside `Migrations/`, `AllowedWriteSites = []`). `CalendarFeedWriter.cs`, `CalendarSubscriptionService.cs`, `FeedRevisionStamper.cs` must show an empty `git diff`.

### Ambient clock
**Source:** `AmbientClockSeamTests` (guards `CalendarController`, `QuestController`, `QuestRepository`, `GroupRepository`, `Quest/Details*.cshtml`). Use `IBoardClock` or the selected Year/Month; Details views must keep `ViewBag.BoardToday`.

### Authorization
**Source:** `GroupController.cs:15-16` `[Area("Platform")] [Authorize(Policy = "SuperAdminOnly")]` plus `[ValidateAntiForgeryToken]` on every POST. Calendar surfaces: `[Authorize]` on `CalendarController`; `QuestController.Details` has no class-level guard, so the linked service must tolerate a null active board.

### Membership (fresh per request, no SuperAdmin branch)
**Source:** `IGroupService.GetGroupsForUserAsync(userId)` -> `GroupRepository.GetGroupsForUserAsync` (`g.UserGroups.Any(ug => ug.UserId == userId)`). Same source as `CrossBoardLinkResolverService`, so link-rendered and link-works cannot disagree. Deep link behaviour needs no new code: `CrossBoardLinkRegistry` already maps `QuestController.Details` -> Quest and `EventsController.Details` -> Event, so `RegisteredRoutes_HasExactly18Entries` stays green.

### Error / logging
**Source:** `EventService.cs` re-check block (LogError with counts, comment saying plainly both layers read the same set).

### Comments
CLAUDE.md: no phase numbers, decision IDs (`D-10`), or planning doc names in source comments or string literals; explain the why in plain language.

## No Analog Found

None. Closest-to-novel items and their nearest precedent: the per-board role map for D-20 (no existing per-board role lookup used for filtering; compose from `GroupService.GetGroupsForUserAsync` plus the group role on `UserGroups`, extending `GroupWithMemberCount` or a new narrow read in `GroupRepository` which is already unfiltered and allowlisted); the "Also showing" / "Also that day" markup (follow 91-UI-SPEC; closest existing markup is `calendar-date-label-mobile` and `row-nav-link`).

## Test Analogs

| New test | Analog |
|---|---|
| `QuestBoard.UnitTests/Repository/LinkedBoardSetRepositoryTests.cs`, `LinkedCalendarReadTests.cs` | `QuestBoard.UnitTests/Repository/GroupRepositoryTests.cs`, `QuestBoardContextFilterTests.cs` (InMemory, private `TestActiveGroupContext`) |
| `QuestBoard.UnitTests/Services/LinkedCalendarServiceTests.cs` | `QuestBoard.UnitTests/Services/CrossBoardAgendaTests.cs` (NSubstitute repo + recording logger; empty set -> `DidNotReceive`; foreign row dropped + logged) |
| `QuestBoard.UnitTests/ViewModels/CalendarViewModelLinkedTests.cs` | existing CalendarViewModel tests (same finalized/proposed matrix for parity) |
| `QuestBoard.IntegrationTests/Tests/LinkedBoardCalendarTenantIsolationTests.cs`, `LinkedBoardQuestDetailsTests.cs` | `Tests/AgendaTenantIsolationTests.cs` (primary-ctor `WebApplicationFactoryBase`, `IAsyncLifetime`, `SeedBoardAsync`/`SeedEventAsync`/`SeedMembershipAsync`; `DisposeAsync` resets `factory.TestGroupContext.ActiveGroupId = 1; BoardType = BoardType.OneShot;`), `Controllers/CalendarBoardTypeScopeTests.cs` (quest + proposed date seeding, mobile UA), `Controllers/EventCalendarPartialTests.cs` |
| `QuestBoard.IntegrationTests/Controllers/LinkedBoardsPlatformIntegrationTests.cs` | `Controllers/GroupManagementIntegrationTests.cs` (`AuthenticationHelper.CreateAuthenticatedSuperAdminClientAsync(_factory)`, `TestDataHelper.ClearDatabaseAsync`, form posts) and `PlatformAreaIntegrationTests.cs` (deny matrix) |
| `QuestBoard.IntegrationTests/Middleware/LinkedBoardDeepLinkTests.cs` | `Middleware/CrossBoardDeepLinkMiddlewareTests.cs` (`CrossBoardWebApplicationFactory`, `NavigationGet` with `Sec-Fetch-Dest: document` / `Sec-Fetch-Mode: navigate`) |
| `QuestBoard.IntegrationTests/Tests/LinkedBoardStaticGuardTests.cs` | `Tests/CalendarSubscriptionStaticGuardTests.cs` (repo-root walk, reads views from disk) |

Existing guards that must stay green: `CrossBoardIgnoreQueryFiltersSeamTests`, `AmbientClockSeamTests`, `FeedRevisionWriteSeamTests`, `MigrationPreflightTests`, `AntiForgeryTokenCoverageTests`, `CrossBoardLinkRegistryTests`, `EventCalendarPartialTests`, `MobileCssTests`, `RowNavigationAccessibilityTests`.

## Metadata

**Analog search scope:** QuestBoard.Repository, QuestBoard.Domain, QuestBoard.Service (controllers, areas/Platform, view models, shared views), QuestBoard.UnitTests, QuestBoard.IntegrationTests
**Files read this session:** CONTEXT.md, RESEARCH.md (full), `GroupRepository.cs`, `GroupController.cs`, `QuestRepository.cs` (targeted), `EventRepository.cs` (targeted), `EventService.cs` (targeted), `EntityProfile.cs` (targeted), `CalendarController.cs`, `CalendarViewModel.cs`, `CalendarDay.cs`, `EventOnDay.cs`, `GroupService.cs`, `GroupCreateViewModel.cs`, two migrations, Group/Index.cshtml
**Pattern extraction date:** 2026-10-16
