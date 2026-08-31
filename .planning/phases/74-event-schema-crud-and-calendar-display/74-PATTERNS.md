# Phase 74: Event Schema, CRUD, and Calendar Display - Pattern Map

**Mapped:** 2026-08-26
**Files analyzed:** 24
**Analogs found:** 22 / 24

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|---|---|---|---|---|
| `QuestBoard.Repository/Entities/EventEntity.cs` | model (entity) | CRUD | `QuestBoard.Repository/Entities/ContactEntity.cs` | exact |
| `QuestBoard.Repository/Entities/EventSeriesEntity.cs` | model (entity) | CRUD | `QuestBoard.Repository/Entities/ContactEntity.cs` | role-match (simpler, no nav collections yet) |
| `QuestBoard.Repository/Entities/EventSignupEntity.cs` | model (entity) | CRUD | `QuestBoard.Repository/Entities/PlayerSignupEntity.cs` (through-nav scoping) | role-match |
| `QuestBoard.Repository/Migrations/<ts>_AddCalendarEventsFeature.cs` | migration | batch | `QuestBoard.Repository/Migrations/20260706193921_AddContactsFeature.cs` | exact |
| `QuestBoard.Domain/Models/Event.cs` | model (domain) | CRUD | `QuestBoard.Domain/Models/Contact.cs` (not read directly; shape inferred from `ContactEntity`/`ContactService` usage) | role-match |
| `QuestBoard.Domain/Interfaces/IEventRepository.cs` / `IEventService.cs` | interface | CRUD | `IContactRepository` / `IContactService` (not read; signatures inferred from `ContactRepository`/`ContactService` bodies) | role-match |
| `QuestBoard.Repository/EventRepository.cs` | service (repository) | CRUD | `QuestBoard.Repository/ContactRepository.cs` | exact |
| `QuestBoard.Domain/Services/EventService.cs` | service | CRUD | `QuestBoard.Domain/Services/ContactService.cs` | exact (minus image-handling branches) |
| `QuestBoard.Service/Controllers/Events/EventsController.cs` | controller | request-response | `QuestBoard.Service/Controllers/Contacts/ContactsController.cs` | exact |
| `QuestBoard.Service/ViewModels/EventViewModels/EventViewModel.cs` | model (viewmodel) | request-response | `QuestBoard.Service/ViewModels/ContactViewModels/ContactViewModel.cs` (not read; inferred from mapper.Map calls in `ContactsController`) | role-match |
| `QuestBoard.Service/Views/Events/Create.cshtml` | component (Razor view) | request-response | `QuestBoard.Service/Views/Contacts/Create.cshtml` | exact (per UI-SPEC explicit instruction) |
| `QuestBoard.Service/Views/Events/Edit.cshtml` | component (Razor view) | request-response | `QuestBoard.Service/Views/Contacts/Edit.cshtml` | exact |
| `QuestBoard.Service/Views/Events/Details.cshtml` | component (Razor view) | request-response | `QuestBoard.Service/Views/Contacts/Details.cshtml` | exact (per UI-SPEC explicit instruction) |
| `QuestBoard.Repository/Entities/QuestBoardContext.cs` (modified: 3 new `HasQueryFilter` + `DbSet`s) | config | CRUD | Existing `QuestEntity`/`ShopItemEntity` (own-GroupId) and `PlayerSignupEntity` (through-nav) filter blocks, same file | exact |
| `QuestBoard.Service/ViewModels/CalendarViewModels/CalendarViewModel.cs` (modified) | model (viewmodel) | transform | same file, existing `Quests`/`GetCalendarDays()` shape | exact |
| `QuestBoard.Service/ViewModels/CalendarViewModels/CalendarDay.cs` (modified) | model (viewmodel) | transform | same file, existing `QuestsOnDay` property | exact |
| `QuestBoard.Service/ViewModels/CalendarViewModels/EventOnDay.cs` | model (viewmodel) | transform | `QuestBoard.Service/ViewModels/CalendarViewModels/QuestOnDay.cs` | exact |
| `QuestBoard.Service/Controllers/QuestBoard/CalendarController.cs` (modified) | controller | request-response | same file, existing `questService.GetQuestsForCalendarAsync()` call | exact |
| `QuestBoard.Service/Views/Shared/_Calendar.cshtml` (modified) | component (Razor partial) | transform | same file, existing `.quest-events`/`quest-event` day-cell block | exact |
| `QuestBoard.Service/Views/Calendar/Index.cshtml` (modified: Legend row) | component (Razor view) | transform | same file, existing Legend card rows | exact |
| `QuestBoard.Service/Views/Calendar/Index.Mobile.cshtml` (modified) | component (Razor view, hand-rolled agenda) | transform | same file, existing filter/empty-state/agenda-entry loop | exact |
| `QuestBoard.Service/wwwroot/css/calendar.css` (modified) | config (CSS) | — | same file, `.calendar-body`/`.quest-event`/`.legend-item` rules | exact |
| `QuestBoard.Service/wwwroot/css/calendar.mobile.css` (modified) | config (CSS) | — | same file, `.agenda-quest-entry`/`.agenda-quest-title` rules | exact |
| `QuestBoard.Service/Views/Shared/_Layout.cshtml` (modified) | component (Razor partial, navbar) | request-response | same file, DM dropdown "Create Quest" `<li>` | exact |
| `QuestBoard.Service/Views/Shared/_Layout.Mobile.cshtml` (modified) | component (Razor partial, navbar) | request-response | mobile mirror of `_Layout.cshtml` DM dropdown | role-match (not read directly; same shape expected) |
| `QuestBoard.Domain/Automapper/EntityProfile.cs` (modified) | config (AutoMapper) | transform | existing `CreateMap<ContactEntity, Contact>()` entries | exact |
| `QuestBoard.Service/Automapper/ViewModelProfile.cs` (modified) | config (AutoMapper) | transform | existing `CreateMap<Contact, ContactViewModel>()` entries | exact |
| `QuestBoard.IntegrationTests/Controllers/EventsControllerIntegrationTests.cs` | test | request-response | Existing `ContactsController`-style integration tests (not read; convention inferred from `TenantIsolationTests.cs` harness usage) | role-match |
| `QuestBoard.IntegrationTests/Tests/EventTenantIsolationTests.cs` (or extend `TenantIsolationTests.cs`) | test | CRUD | `QuestBoard.IntegrationTests/Tests/TenantIsolationTests.cs` | exact |
| `QuestBoard.IntegrationTests/Mobile/MobileViewsTests.cs` (extended) | test | request-response | same file, real-UA helper pattern | exact |

## Pattern Assignments

### `QuestBoard.Repository/Entities/EventEntity.cs` (model, CRUD)

**Analog:** `QuestBoard.Repository/Entities/ContactEntity.cs` (full file, 45 lines)

**Full pattern to clone:**
```csharp
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuestBoard.Repository.Entities;

[Table("Contacts")]
public class ContactEntity : IEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Description { get; set; }
    // ... more fields ...

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int GroupId { get; set; }

    [ForeignKey(nameof(GroupId))]
    public virtual GroupEntity Group { get; set; } = null!;
}
```

**Deviations required by CONTEXT.md decisions (do not copy these fields verbatim):**
- No `CreatedByUserId`/`CreatedByUser` nav (D-05 — no author column).
- `Description` has **no** `[StringLength(2000)]` — unbounded, matching `QuestEntity.Description` instead (D-06).
- `Date` is `DateOnly`, `StartTime` is `TimeOnly?` (D-01) — new-to-codebase EF Core 10 native mapping, per RESEARCH.md Code Examples section:
```csharp
public DateOnly Date { get; set; }
public TimeOnly? StartTime { get; set; }
```
- Add nullable `SeriesId`/`Series` nav (D-03) — no direct `Contact` analog for a nullable self-referencing-style FK; model it the same way `QuestEntity`/other entities declare a nullable FK + `[ForeignKey]` nav pair.

---

### `QuestBoard.Repository/EventRepository.cs` (service/repository, CRUD)

**Analog:** `QuestBoard.Repository/ContactRepository.cs` (213 lines, read in full)

**Class declaration + base-class pattern** (lines 1-9):
```csharp
using AutoMapper;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.Repository.Entities;
using Microsoft.EntityFrameworkCore;

namespace QuestBoard.Repository;

internal class ContactRepository(QuestBoardContext dbContext, IMapper mapper) : BaseRepository<Contact, ContactEntity>(dbContext, mapper), IContactRepository
```
`EventRepository` follows this exact shape: `internal class EventRepository(QuestBoardContext dbContext, IMapper mapper) : BaseRepository<Event, EventEntity>(dbContext, mapper), IEventRepository`.

**"With details" read pattern** (lines 11-21) — group scoping relies entirely on the entity's query filter, no manual `.Where(GroupId ==)`:
```csharp
public async Task<IList<Contact>> GetAllContactsWithDetailsAsync(CancellationToken token = default)
{
    // Group scoping is enforced entirely by ContactEntity's fail-closed query filter here --
    // no manual GroupId .Where is needed or added.
    var entities = await DbContext.Contacts
        .Include(c => c.CreatedByUser)
        .OrderBy(c => c.Name)
        .ToListAsync(token);

    var contacts = Mapper.Map<IList<Contact>>(entities);
    return contacts;
}
```
For `EventRepository.GetEventsForCalendarAsync()` — mirror `QuestRepository.GetQuestsForCalendarAsync()`'s fetch-all convention (per RESEARCH.md Open Question 1), not a date-range query.

**Update pattern restoring tracked navigations** (lines 81-110) — copy this shape if `Event` has any nav that AutoMapper's `Map(model, entity)` could null out (e.g. `Group`, `Series`):
```csharp
public override async Task UpdateAsync(Contact model, CancellationToken token = default)
{
    var entity = await DbContext.Contacts.FirstOrDefaultAsync(c => c.Id == model.Id, token);
    if (entity == null) return;

    var trackedGroup = entity.Group;
    Mapper.Map(model, entity);
    entity.Group = trackedGroup;

    await DbContext.SaveChangesAsync(token);
}
```

---

### `QuestBoard.Domain/Services/EventService.cs` (service, CRUD)

**Analog:** `QuestBoard.Domain/Services/ContactService.cs` (116 lines, read in full)

**Minimal pass-through shape to copy** (lines 1-19):
```csharp
using AutoMapper;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;

namespace QuestBoard.Domain.Services;

internal class ContactService(IContactRepository repository, IMapper mapper) : BaseService<Contact>(repository, mapper), IContactService
{
    public async Task<IList<Contact>> GetAllContactsWithDetailsAsync(CancellationToken token = default)
    {
        return await repository.GetAllContactsWithDetailsAsync(token);
    }
    // ... more thin pass-throughs ...
}
```
`EventService` needs none of `ContactService`'s image-handling complexity (`UpdateAsync` overloads, `AddAsync(model, croppedImageData)`, `GetContactOriginalImageAsync`) — Events have no images. Use the plain `BaseService<Event>` CRUD (`AddAsync`, `UpdateAsync`, `RemoveAsync`) directly plus one calendar-read passthrough method.

---

### `QuestBoard.Service/Controllers/Events/EventsController.cs` (controller, request-response)

**Analog:** `QuestBoard.Service/Controllers/Contacts/ContactsController.cs` (457 lines, read in full)

**Class + DI shape** (lines 13-20):
```csharp
[Authorize]
public class ContactsController(
    IContactService contactService,
    IUserService userService,
    IActiveGroupContext activeGroupContext,
    IImageValidationService imageValidationService,
    IMapper mapper) : Controller
```
`EventsController` drops `IImageValidationService` (no images).

**Create POST pattern — write-side group stamp (D-21)** (lines 88-150, condensed to the load-bearing lines):
```csharp
[HttpPost]
[Authorize(Policy = "DungeonMasterOnly")]
[ValidateAntiForgeryToken]
public async Task<IActionResult> Create(ContactViewModel viewModel, CancellationToken token = default)
{
    var currentUser = await userService.GetUserAsync(User);
    if (currentUser.Id == 0) { return Challenge(); }
    if (!ModelState.IsValid) { return View(viewModel); }

    var contact = mapper.Map<Contact>(viewModel);

    // Tag the contact to the active group so the group-scoped roster query filter
    // applies (ContactEntity is scoped by a global query filter on GroupId).
    contact.GroupId = activeGroupContext.RequireActiveGroupId();

    await contactService.AddAsync(contact, croppedImageData, token);
    return RedirectToAction(nameof(Index));
}
```
For `EventsController.Create`: no `CreatedByUserId` stamp (D-05), and the redirect target is `RedirectToAction("Index", "Calendar", new { year = event.Date.Year, month = event.Date.Month })` per D-20, not `Index` on the same controller.

**Edit GET/POST pattern** (lines 152-255) — same `IsDmTierAsync`-gated fetch-and-check-null shape; for `EventsController.Edit`, add the D-21 series-FK cross-board check (no existing precedent — new code):
```csharp
if (series != null && series.GroupId != activeGroupContext.RequireActiveGroupId())
{
    return BadRequest();
}
```

**Delete POST pattern** (lines 257-271):
```csharp
[HttpPost]
[Authorize(Policy = "DungeonMasterOnly")]
[ValidateAntiForgeryToken]
public async Task<IActionResult> Delete(int id, CancellationToken token = default)
{
    var contact = await contactService.GetContactWithDetailsAsync(id, token);
    if (contact == null) { return NotFound(); }

    await contactService.RemoveAsync(contact, token);
    return RedirectToAction(nameof(Index));
}
```
`EventsController.Delete` redirects to the calendar at the event's month (D-20) instead — capture `event.Date.Year`/`.Month` **before** calling `RemoveAsync`.

**DM-tier authorization helper pattern** (lines 423-431) — this is a **display-only** `CanManage` flag, not the security boundary:
```csharp
// The DungeonMasterOnly policy attribute is the security boundary for
// Create/Edit/Delete. This helper is used only to compute a display-only flag
// (CanManage) for views — it deliberately resolves the same way
// GetEffectiveGroupRoleAsync does, but never gates an action.
private async Task<bool> IsDmTierAsync()
{
    var role = await userService.GetEffectiveGroupRoleAsync(User, activeGroupContext.RequireActiveGroupId());
    return role == GroupRole.Admin || role == GroupRole.DungeonMaster;
}
```
For `EventsController.Details`, use this to compute `viewModel.CanManage` (D-11: any DM, not owner-restricted — Contacts' `IsVisibleTo` owner logic does NOT apply to Events, since D-05 has no author/creator concept at all).

---

### `QuestBoard.Repository/Migrations/<ts>_AddCalendarEventsFeature.cs` (migration, batch)

**Analog:** `QuestBoard.Repository/Migrations/20260706193921_AddContactsFeature.cs` (full `Up()` read, lines 1-115)

**Ordered multi-table `CreateTable` pattern:**
```csharp
migrationBuilder.CreateTable(
    name: "Contacts",
    columns: table => new
    {
        Id = table.Column<int>(type: "int", nullable: false)
            .Annotation("SqlServer:Identity", "1, 1"),
        Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
        Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
        CreatedByUserId = table.Column<int>(type: "int", nullable: false),
        CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
        GroupId = table.Column<int>(type: "int", nullable: false)
    },
    constraints: table =>
    {
        table.PrimaryKey("PK_Contacts", x => x.Id);
        table.ForeignKey(
            name: "FK_Contacts_Groups_GroupId",
            column: x => x.GroupId,
            principalTable: "Groups",
            principalColumn: "Id");
    });

// ContactNotes created AFTER Contacts because it FKs to Contacts.Id
migrationBuilder.CreateTable(
    name: "ContactNotes",
    columns: table => new { /* ... */ ContactId = table.Column<int>(type: "int", nullable: false) /* ... */ },
    constraints: table =>
    {
        table.PrimaryKey("PK_ContactNotes", x => x.Id);
        table.ForeignKey(
            name: "FK_ContactNotes_Contacts_ContactId",
            column: x => x.ContactId,
            principalTable: "Contacts",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);
    });

// Indexes created LAST, after all tables exist
migrationBuilder.CreateIndex(name: "IX_Contacts_GroupId", table: "Contacts", column: "GroupId");
```
**Ordering for this phase (per D-02/RESEARCH.md Pattern 3):** create `EventSeries` first (no dependency on `Events`), then `Events` (nullable FK to `EventSeries`), then `EventSignups` (required FK to `Events`), indexes last. A `(GroupId, Date)` composite index on `Events` is the RESEARCH-recommended default (cf. `AddQuestFinalizedDateIndex` precedent, not read directly this pass — same `CreateIndex` shape as above, just with two columns).

**Column type note:** `Date`/`StartTime` columns must read `type: "date"` / `type: "time"` respectively in the generated migration — verify after running `dotnet ef migrations add` (D-24).

---

### `QuestBoard.Repository/Entities/QuestBoardContext.cs` (config, tenant scoping)

**Analog:** same file, lines 270-320 (existing filter block, read directly)

**Own-GroupId fail-closed filter pattern** (for `EventEntity`, `EventSeriesEntity`):
```csharp
// Lambda closes over activeGroupContext instance — re-evaluated per query, not at startup
// CRITICAL: Do NOT capture activeGroupContext.ActiveGroupId into a local var here.
//           That captures the value once (null at model-build time). Always reference the service.
modelBuilder.Entity<QuestEntity>()
    .HasQueryFilter(e =>
        activeGroupContext.ActiveGroupId != null &&
        e.GroupId == activeGroupContext.ActiveGroupId);
```

**Through-navigation fail-closed filter pattern** (for `EventSignupEntity`, scoped via required `Event` nav):
```csharp
// PlayerSignupEntity carries no GroupId of its own — scoped through its required Quest
// navigation. This also makes every PlayerSignupRepository method (including the base
// GetByIdAsync/FindAsync path) automatically group-scoped.
modelBuilder.Entity<PlayerSignupEntity>()
    .HasQueryFilter(ps =>
        activeGroupContext.ActiveGroupId != null &&
        ps.Quest.GroupId == activeGroupContext.ActiveGroupId);
```
Apply verbatim as `es.Event.GroupId == activeGroupContext.ActiveGroupId` for `EventSignupEntity` — required per Pitfall 3 in RESEARCH.md, even though nothing reads/writes this table yet.

---

### `QuestBoard.Service/ViewModels/CalendarViewModels/{CalendarViewModel,CalendarDay,QuestOnDay}.cs` (viewmodel, transform)

**Analogs:** all three files, read in full.

`CalendarViewModel.cs` (55 lines shown) — existing `Quests`/`GetCalendarDays()` shape:
```csharp
public class CalendarViewModel
{
    public int Year { get; set; }
    public int Month { get; set; }
    public List<Quest> Quests { get; set; } = new();

    public List<CalendarDay> GetCalendarDays()
    {
        var days = new List<CalendarDay>();
        var firstDayOfWeek = ((int)FirstDayOfWeek + 6) % 7;
        for (int i = 0; i < firstDayOfWeek; i++) { days.Add(new CalendarDay { IsEmpty = true }); }
        for (int day = 1; day <= DaysInMonth; day++)
        {
            var date = new DateTime(Year, Month, day);
            var questsOnDay = GetQuestsForDate(date);
            days.Add(new CalendarDay { Date = date, Day = day, QuestsOnDay = questsOnDay });
        }
        // ... trailing empty days ...
        return days;
    }
}
```
Add `public List<Event> Events { get; set; } = new();` (default empty — D-09 structural protection) and an `EventsOnDay` step inside `GetCalendarDays()` mirroring `GetQuestsForDate`. This is the single well-named `DateOnly → DateTime` conversion seam (D-01) — compare `Event.Date.ToDateTime(...)` against the loop's `DateTime date` once here, not scattered elsewhere.

`CalendarDay.cs` (full file, 8 lines):
```csharp
public class CalendarDay
{
    public DateTime Date { get; set; }
    public int Day { get; set; }
    public bool IsEmpty { get; set; }
    public List<QuestOnDay> QuestsOnDay { get; set; } = [];
}
```
Add `public List<EventOnDay> EventsOnDay { get; set; } = [];` alongside.

`QuestOnDay.cs` (full file, 8 lines) — direct template for the new `EventOnDay.cs`:
```csharp
public class QuestOnDay
{
    public Quest Quest { get; set; } = null!;
    public ProposedDate ProposedDate { get; set; } = null!;
    public bool IsFinalized { get; set; }
}
```
`EventOnDay` needs only `public Event Event { get; set; } = null!;` plus whatever display-time fields (e.g. a precomputed "All day" flag) the view needs — simpler than `QuestOnDay` since there's no finalized/proposed distinction (D-07: no `EventType`).

---

### `QuestBoard.Service/Controllers/QuestBoard/CalendarController.cs` (controller, request-response)

**Analog:** same file, full 42 lines read.

```csharp
[Authorize]
public class CalendarController(IQuestService questService) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(int? year = null, int? month = null, CancellationToken token = default)
    {
        var currentDate = DateTime.Now;
        var selectedYear = year ?? currentDate.Year;
        var selectedMonth = month ?? currentDate.Month;
        if (selectedMonth < 1 || selectedMonth > 12) { return BadRequest("Invalid month..."); }
        if (selectedYear < 1900 || selectedYear > 2100) { return BadRequest("Invalid year..."); }

        var allQuests = await questService.GetQuestsForCalendarAsync(token);
        var calendarModel = new CalendarViewModel { Year = selectedYear, Month = selectedMonth, Quests = [.. allQuests] };
        return View(calendarModel);
    }
}
```
Add `IEventService eventService` to the constructor and a second fetch: `var allEvents = await eventService.GetEventsForCalendarAsync(token); calendarModel.Events = [.. allEvents];` — same fetch-all convention, no date-range filtering this phase (RESEARCH.md Open Question 1).

---

### `QuestBoard.Service/Views/Shared/_Calendar.cshtml` (Razor partial, transform)

**Analog:** same file, day-cell block read (lines 1-80 shown; `.quest-events`/`.quest-event` block at lines 32-46).

```html
<div class="calendar-day @(day.IsEmpty ? "empty" : "") @(day.Date.Date == DateTime.Today ? "today" : "")">
    @if (!day.IsEmpty)
    {
        <div class="day-number">@day.Day</div>

        @if (day.QuestsOnDay.Any())
        {
            <div class="quest-events">
                @foreach (var questOnDay in day.QuestsOnDay.Take(3))
                {
                    <div class="quest-event @(questOnDay.IsFinalized ? "finalized" : "proposed") ...">
                        <a href="@Url.Action("Details", "Quest", new { id = questOnDay.Quest.Id })" class="quest-link">
                            <div class="calendar-quest-title">@questOnDay.Quest.Title</div>
                            <div class="quest-time">@questOnDay.ProposedDate.Date.ToString("HH:mm")</div>
                        </a>
                    </div>
                }
            </div>
        }
    }
</div>
```
Per D-08/D-09/UI-SPEC Component Contract: insert a **new sibling block** `.calendar-events` immediately after `.day-number`, **before** `.quest-events`:
```html
@if (day.EventsOnDay.Any())
{
    <div class="calendar-events">
        @foreach (var eventOnDay in day.EventsOnDay.Take(3))
        {
            <div class="calendar-event">
                <a href="@Url.Action("Details", "Events", new { id = eventOnDay.Event.Id })" class="quest-link">
                    <div class="calendar-event-title"><i class="fas fa-calendar-day"></i> @eventOnDay.Event.Title</div>
                    <div class="calendar-event-time">@(eventOnDay.Event.StartTime.HasValue ? eventOnDay.Event.StartTime.Value.ToString("HH:mm") : "All day")</div>
                </a>
            </div>
        }
    </div>
}
```
Because the 5 out-of-scope call sites never populate `day.EventsOnDay` (their locally-built `CalendarViewModel` leaves `Events` at its default-empty list), this block renders nothing there with no branch/flag — this is D-09's structural protection and must be verified by a test, not just code review.

---

### `QuestBoard.Service/wwwroot/css/calendar.css` (config, row-height fix — Pitfall 1)

**Analog:** same file, `.calendar-body` rule (grep-located at line ~38: `grid-auto-rows: 120px`).

```css
.calendar-body {
    grid-auto-rows: 120px;   /* BEFORE */
}
```
Change to (per UI-SPEC Component Contract, mandatory not optional):
```css
.calendar-body {
    grid-auto-rows: minmax(120px, auto);
}
```
Note lines ~406/414 already show `minmax(120px, auto)` elsewhere in the file (likely a `.details-page` variant) — confirm the base `.calendar-body` rule (not just the details-page override) gets the same treatment, and verify a day with 1 event + 3 quests renders uncropped (`.calendar-day { overflow: hidden }` still applies and will clip if the row can't grow).

New chip classes per UI-SPEC: `.calendar-events`, `.calendar-event` (border-left `#6f42c1`, `rgba(255,255,255,0.9)` background, `padding: 2px 4px`), `.calendar-event-title` (10px/600), `.calendar-event-time` (8px/400 italic) — mirror `.quest-events`/`.quest-event`/`.calendar-quest-title`/`.quest-time`'s existing rule shapes exactly, just with the new class names and the purple accent color.

---

### `QuestBoard.Service/Views/Calendar/Index.Mobile.cshtml` (Razor view, hand-rolled agenda)

**Analog:** same file, lines 1-30 read (filter at line 9).

```csharp
var agendaDays = Model.GetCalendarDays().Where(d => !d.IsEmpty && d.QuestsOnDay.Any()).ToList();
```
Per D-13/UI-SPEC, widen to:
```csharp
var agendaDays = Model.GetCalendarDays().Where(d => !d.IsEmpty && (d.QuestsOnDay.Any() || d.EventsOnDay.Any())).ToList();
```
Empty-state copy (not yet read directly — locate the "No Quests This Month"/"No adventures are planned" strings further down the file) becomes "Nothing This Month" / "No quests or events are planned for {month}. Check another month." per UI-SPEC Copywriting Contract. Per-day render order inside each `agenda-day-section`: `EventsOnDay` first, then `QuestsOnDay` (D-15).

---

### `QuestBoard.IntegrationTests/Tests/TenantIsolationTests.cs` (test, D-22 template)

**Analog:** full class pattern, lines 1-70+ read.

```csharp
public class TenantIsolationTests(WebApplicationFactoryBase factory)
    : IClassFixture<WebApplicationFactoryBase>, IAsyncLifetime
{
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync()
    {
        factory.TestGroupContext.ActiveGroupId = 1;
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task GroupFilter_HidesQuestFromOtherGroup()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var dm = await AuthenticationHelper.CreateTestUserAsync(factory.Services, "isolationdm1", "isolationdm1@example.com");

        await using var ctx = factory.Database.CreateContext(); // ActiveGroupId = null (sees all for seeding)
        ctx.Groups.Add(new GroupEntity { Id = 2, Name = "OtherGroup", CreatedAt = DateTime.UtcNow });
        ctx.Quests.Add(new QuestEntity { Title = "GroupTwoQuest", GroupId = 2, /* ... */ });
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);

        factory.TestGroupContext.ActiveGroupId = 1;
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(factory, "isolationviewer1", "isolationviewer1@example.com");
        var response = await client.GetAsync("/quests", TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().NotContain("GroupTwoQuest");
    }
}
```
Copy this exact structure for `EventTenantIsolationTests` (or add cases to this file): seed an `EventEntity` with `GroupId = 2` via `ctx.Set<EventEntity>().Add(...)`, flip `factory.TestGroupContext.ActiveGroupId = 1`, assert the event's title never appears in the `/calendar` (or wherever `CalendarController.Index` is routed) response body. Also add a positive "same group shows" case mirroring `GroupFilter_ShowsQuestFromSameGroup`, and reset `ActiveGroupId = 1` in `DisposeAsync` exactly as shown.

---

## Shared Patterns

### Fail-closed tenant query filter
**Source:** `QuestBoard.Repository/Entities/QuestBoardContext.cs:270-320`
**Apply to:** `EventEntity`, `EventSeriesEntity` (own-`GroupId` shape), `EventSignupEntity` (through-`Event` nav shape)
```csharp
// CRITICAL: Do NOT capture activeGroupContext.ActiveGroupId into a local var here.
modelBuilder.Entity<QuestEntity>()
    .HasQueryFilter(e =>
        activeGroupContext.ActiveGroupId != null &&
        e.GroupId == activeGroupContext.ActiveGroupId);
```

### Write-side group stamp (defense in depth, D-21)
**Source:** `QuestBoard.Service/Controllers/Contacts/ContactsController.cs:141-143`
**Apply to:** `EventsController.Create` (and any Edit path that reassigns `SeriesId`)
```csharp
contact.GroupId = activeGroupContext.RequireActiveGroupId();
```

### DM-gated CRUD controller shape
**Source:** `QuestBoard.Service/Controllers/Contacts/ContactsController.cs` (whole file)
**Apply to:** `EventsController` — `[Authorize]` class-level, `[Authorize(Policy = "DungeonMasterOnly")]` + `[ValidateAntiForgeryToken]` on Create/Edit/Delete POSTs, plain `[Authorize]` GET-only on `Details`.

### `TempData["Success"]` + `_Toasts.cshtml` (D-20)
**Source:** established Phase 72 D-14, reused here — no `_Toasts.cshtml` code change needed, just set `TempData["Success"] = "Event created successfully."` etc. before each redirect in `EventsController`.

### `IMarkdownService` reuse (D-06)
**Source:** `QuestBoard.Domain/Interfaces/IMarkdownService.cs`, `QuestBoard.Service/Views/Shared/_MarkdownEditor.cshtml` (not read this pass — reuse untouched per explicit decision, no new rendering pipeline). Apply to `Events/Create.cshtml`/`Edit.cshtml` (authoring) and `Events/Details.cshtml` (`@Html.Markdown(...)` render call, matching whatever helper `Quest/Details.cshtml` already uses for `Quest.Description`).

## No Analog Found

| File | Role | Data Flow | Reason |
|---|---|---|---|
| `QuestBoard.Repository/Entities/EventSeriesEntity.cs` cadence/anchor fields | model | CRUD | Owned by Phase 76 — table created now per D-02 but no field shape precedent exists yet in this codebase; planner should keep this entity minimal (Id, GroupId, CreatedAt placeholder) for Phase 74 |
| `QuestBoard.Repository/Entities/EventSignupEntity.cs` field shape | model | CRUD | Owned by Phase 75 — only the FK-to-Event + GroupId-via-nav scoping is this phase's concern; no existing signup-shape entity to structurally clone beyond `PlayerSignupEntity`'s scoping pattern (already captured above) |
| D-21 series-cross-board write check | controller logic | request-response | RESEARCH.md explicitly states "no existing precedent for this exact cross-FK check in the codebase" — new code, not a copy |

## Metadata

**Analog search scope:** `QuestBoard.Repository/`, `QuestBoard.Domain/`, `QuestBoard.Service/Controllers`, `QuestBoard.Service/Views/{Contacts,Calendar,Shared}`, `QuestBoard.Service/wwwroot/css`, `QuestBoard.IntegrationTests/Tests`
**Files scanned:** 12 read in full/targeted sections (`ContactEntity.cs`, `ContactRepository.cs`, `ContactService.cs`, `ContactsController.cs`, `AddContactsFeature.cs` migration, `QuestBoardContext.cs` filter block, `CalendarViewModel.cs`/`CalendarDay.cs`/`QuestOnDay.cs`, `_Calendar.cshtml`, `calendar.css` grep, `CalendarController.cs`, `Index.Mobile.cshtml` head, `_Layout.cshtml` DM dropdown, `TenantIsolationTests.cs`) plus CONTEXT.md/RESEARCH.md/UI-SPEC.md in full.
**Pattern extraction date:** 2026-08-26
