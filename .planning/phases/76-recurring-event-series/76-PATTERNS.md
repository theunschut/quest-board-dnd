# Phase 76: Recurring Event Series - Pattern Map

**Mapped:** 2026-08-28
**Files analyzed:** 24 (new + modified)
**Analogs found:** 24 / 24 (all have at least a role-match; several have exact-match precedent)

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|---|---|---|---|---|
| `QuestBoard.Domain/Services/EventSeriesDateGenerator.cs` (NEW) | service (pure/domain) | transform | No direct analog — pure algorithm class is new to this codebase | no analog (see below) |
| `QuestBoard.Domain/Services/EventSeriesService.cs` (NEW) | service (orchestration) | CRUD + batch | `QuestBoard.Domain/Services/EventService.cs` | exact (same layer, same DI shape) |
| `QuestBoard.Domain/Models/EventSeries.cs` (NEW) | model | transform | `QuestBoard.Domain/Models/Event.cs` (or QuestBoard equivalent) | exact |
| `QuestBoard.Domain/Interfaces/IEventSeriesService.cs` (NEW) | service (interface) | CRUD | `QuestBoard.Domain/Interfaces/IEventService.cs` | exact |
| `QuestBoard.Domain/Interfaces/IEventSeriesRepository.cs` (NEW, or fold into `IEventRepository`) | service (interface) | CRUD | `QuestBoard.Domain/Interfaces/IEventRepository.cs` | exact |
| `QuestBoard.Repository/Entities/EventSeriesEntity.cs` (MODIFY — add Title/Description/StartTime/EndDate) | model (entity) | CRUD | itself (extend in place) | exact |
| `QuestBoard.Repository/Entities/EventEntity.cs` (MODIFY — add cancelled marker) | model (entity) | CRUD | itself (extend in place) | exact |
| `QuestBoard.Repository/EventSeriesRepository.cs` (NEW, or extend `EventRepository.cs`) | service (repository) | CRUD | `QuestBoard.Repository/EventRepository.cs` | exact |
| `QuestBoard.Repository/EventRepository.cs` (MODIFY — narrow scope-sweep + cancel + slot-index queries) | service (repository) | CRUD | itself + `PlayerSignupRepository.ChangeVoteAsync` | exact |
| `QuestBoard.Repository/Migrations/{ts}_AddSeriesRecurrence.cs` (NEW) | migration | batch | `QuestBoard.Repository/Migrations/20260826134133_AddCalendarEventsFeature.cs` | exact |
| `QuestBoard.Repository/Entities/QuestBoardContext.cs` (MODIFY — filtered unique index, EndDate column, cancelled marker mapping) | config (EF model config) | CRUD | itself (extend `OnModelCreating`) | exact |
| `QuestBoard.Repository/Automapper/EntityProfile.cs` (MODIFY) | config (mapper) | transform | itself (extend Event/EventSeries maps) | exact |
| `QuestBoard.Service/Automapper/ViewModelProfile.cs` (MODIFY) | config (mapper) | transform | itself (extend EventViewModel/new SeriesViewModel maps) | exact |
| `QuestBoard.Service/Controllers/Events/EventsController.cs` (MODIFY — Cancel action, scope-aware Edit, PreviewSeries) | controller | request-response | itself (extend in place) | exact |
| `QuestBoard.Service/Controllers/Events/SeriesController.cs` (NEW, or fold into `EventsController`) | controller | request-response | `QuestBoard.Service/Controllers/Events/EventsController.cs` | exact |
| `QuestBoard.Service/Jobs/RecurringOccurrenceTopUpJob.cs` (NEW) | service (background job) | batch/event-driven | `QuestBoard.Service/Jobs/DailyReminderJob.cs` | exact |
| `QuestBoard.Service/Jobs/HangfireJobHelper.cs` (no change expected, reused as-is) | utility | request-response | itself | exact (reused, not modified) |
| `QuestBoard.Service/Program.cs` (MODIFY — register new recurring job) | config | batch | itself, lines ~349-359 | exact |
| `QuestBoard.Service/Views/Events/Create.cshtml` (MODIFY — repeats toggle, mask strip, preview) | component (view) | request-response | itself + `Views/ShopManagement/Create.cshtml` (checkbox-reveal idiom) + `Views/Quest/Details.cshtml` (fetch-POST idiom) | exact (extend) / role-match (borrowed idioms) |
| `QuestBoard.Service/Views/Events/Edit.cshtml` (MODIFY — scope-prompt modal, collision notice) | component (view) | request-response | itself | exact |
| `QuestBoard.Service/Views/Events/Details.cshtml` (MODIFY — cancelled banner, Cancel vs Delete, series link) | component (view) | request-response | itself | exact |
| `QuestBoard.Service/Views/Series/Details.cshtml` (NEW) | component (view) | request-response | `QuestBoard.Service/Views/Events/Details.cshtml` | exact (structural sibling) |
| `QuestBoard.Service/Views/Shared/_Calendar.cshtml` (MODIFY — cancelled chip modifier only) | component (view/partial) | request-response | itself | exact |
| `QuestBoard.Service/Views/Calendar/Index.cshtml` (MODIFY — horizon banner, Legend row) | component (view) | request-response | itself | exact |
| `QuestBoard.Service/Views/Calendar/Index.Mobile.cshtml` (MODIFY — cancelled agenda entry) | component (view) | request-response | itself | exact |
| `QuestBoard.UnitTests/Services/EventSeriesDateGeneratorTests.cs` (NEW) | test (unit) | transform | none — first pure-generator test in the repo; closest shape is any plain xUnit fact class with no DB | role-match |
| `QuestBoard.UnitTests/Repository/EventSeriesMaterializationTests.cs` (NEW) | test (unit/InMemory) | CRUD | `QuestBoard.UnitTests/Repository/EventSignupRepositoryTests.cs` | exact |
| `QuestBoard.UnitTests/Services/RecurringOccurrenceTopUpJobTests.cs` (NEW) | test (unit) | event-driven | `QuestBoard.UnitTests/Services/DailyReminderJobTests.cs` | exact |
| `QuestBoard.IntegrationTests/Tests/EventSeriesTenantIsolationTests.cs` (NEW) | test (integration) | request-response | `QuestBoard.IntegrationTests/Tests/EventTenantIsolationTests.cs` | exact |

## Pattern Assignments

### `QuestBoard.Domain/Services/EventSeriesDateGenerator.cs` (pure domain service, transform)

**No analog exists in this codebase** — this is the first pure, no-DI, no-I/O algorithm class. Do not force it into a repository/service DI shape. Follow the shape RESEARCH.md already pinned (Pattern 1), which is itself the concrete spec to build from — treat that code block as the load-bearing source, not merely illustrative:

```csharp
namespace QuestBoard.Domain.Services;

public static class EventSeriesDateGenerator
{
    public static IEnumerable<(int SlotIndex, DateOnly Date, bool Fires)> GenerateSlots(
        DateOnly anchorDate,
        int intervalWeeks,
        IReadOnlyList<bool> cycleMask,
        DateOnly? endDate,
        int maxSlots)
    {
        for (var slot = 0; slot < maxSlots; slot++)
        {
            var date = anchorDate.AddDays(slot * intervalWeeks * 7);
            if (endDate.HasValue && date > endDate.Value) yield break;
            yield return (slot, date, cycleMask[slot % cycleMask.Count]);
        }
    }
}
```
Take `today` as a caller-supplied value wherever "today" matters downstream (in the orchestration layer, not here) — this method has no I/O and no `DateTime.Now` call, so it stays trivially unit-testable.

---

### `QuestBoard.Domain/Services/EventSeriesService.cs` (orchestration service, CRUD + batch)

**Analog:** `QuestBoard.Domain/Interfaces/IEventService.cs` / `IEventRepository.cs` (`AddWithCampaignFanOutAsync`, `GetSeriesGroupIdAsync` already exist and must be reused, not reimplemented)

**Reusable fan-out (do not reimplement)** — `QuestBoard.Repository/EventRepository.cs:48-71`:
```csharp
public async Task AddWithCampaignFanOutAsync(Event newEvent, IEnumerable<int> memberIds, CancellationToken token = default)
{
    var entity = Mapper.Map<EventEntity>(newEvent);
    foreach (var memberId in memberIds.Distinct())
    {
        entity.Signups.Add(new EventSignupEntity { UserId = memberId, Availability = (int)VoteType.Yes });
        // Availability defaults to Yes; UpdatedAt stays unset -- the "automatic pass" marker
        // the generator's fan-out must preserve (D-29).
    }
    await DbSet.AddAsync(entity, token);
    await DbContext.SaveChangesAsync(token);
    newEvent.Id = entity.Id;
}
```
**Hazard:** never stamp `UpdatedAt` on the signup rows this method creates (D-29) — the generator's materializer must call this method as-is rather than building its own `EventSignupEntity` and setting a timestamp.

**Idempotency / materialization shape** — build per RESEARCH.md Pattern 2 (already vetted against D-18/D-19/D-20/D-23/D-25): load every existing `SeriesSlotIndex` for the series with **no date predicate** (`repository.GetSlotIndexesForSeriesAsync`), subtract from generator candidates, keep only today-or-later dates, call `AddWithCampaignFanOutAsync` (Campaign boards) or `AddAsync` (One-Shot) per new occurrence, one at a time so a mid-run retry finds prior writes already present.

---

### `QuestBoard.Repository/EventRepository.cs` (MODIFY — narrow scope-sweep write)

**Analog:** `QuestBoard.Repository/PlayerSignupRepository.cs:43-` (`ChangeVoteAsync`) — the narrow-scalar-update shape required for the D-09 "this and all future events" sweep and the D-14 cancel write, because `EventEntity` now carries a `Signups` navigation collection that makes `BaseRepository.UpdateAsync`'s `Mapper.Map(model, entity)` unsafe:

```csharp
public async Task<bool> ChangeVoteAsync(int playerSignupId, int proposedDateId, VoteType vote, CancellationToken cancellationToken = default)
{
    var entity = await DbSet
        .Include(ps => ps.DateVotes)
        .FirstOrDefaultAsync(ps => ps.Id == playerSignupId, cancellationToken);
    if (entity == null) throw new ArgumentException("Player signup not found", nameof(playerSignupId));
    // ... targeted field mutation, single SaveChangesAsync ...
}
```
Apply this shape for: (1) the D-09 template sweep across N future, untouched occurrences (load once, loop-mutate, one `SaveChangesAsync`, skip rows separately moved/edited/cancelled), and (2) the D-14 cancel write (load the one `EventEntity`, set the cancelled marker, save — never go through `BaseRepository.UpdateAsync`).

**Existing methods to extend, not duplicate** — `QuestBoard.Repository/EventRepository.cs:12-45`: `GetEventsForCalendarAsync` (no manual `GroupId` filter needed — query filter enforces it), `GetEventWithDetailsAsync` (cross-board id returns null via filter, not an exception), `GetSeriesGroupIdAsync` (already the D-21/second-layer check for series-board matching, reuse for D-12/D-13 as well).

---

### `QuestBoard.Repository/Entities/EventSeriesEntity.cs` (MODIFY) / `EventEntity.cs` (MODIFY)

**Analog:** themselves, as shipped — extend in place, do not create parallel entities.

Shipped `EventSeriesEntity` (add `Title`, `Description`, `StartTime`, `EndDate`; **must rewrite the stale class comment**, `QuestBoard.Repository/Entities/EventSeriesEntity.cs:6-8`, "No code reads or writes it yet" — no longer true):
```csharp
[Table("EventSeries")]
public class EventSeriesEntity : IEntity
{
    [Key] public int Id { get; set; }
    public DateOnly AnchorDate { get; set; }
    public int IntervalWeeks { get; set; }
    [Range(0, 6)] public int WeekDay { get; set; }
    [StringLength(200)] public string CycleMask { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int GroupId { get; set; }
    public virtual GroupEntity Group { get; set; } = null!;
}
```
Shipped `EventEntity` (add cancelled marker — bool `IsCancelled` or nullable `CancelledAt`, per CONTEXT.md's open naming discretion) — note the existing `Signups` navigation comment at `EventEntity.cs:42-44` explaining exactly why the generic `UpdateAsync` path must not be used once this collection is populated.

---

### `QuestBoard.Repository/Migrations/{ts}_AddSeriesRecurrence.cs` (NEW migration)

**Analog:** `QuestBoard.Repository/Migrations/20260826134133_AddCalendarEventsFeature.cs`

Confirms the exact gap this migration must close — `IX_Events_SeriesId` is a **plain, non-unique** index today:
```csharp
migrationBuilder.CreateIndex(
    name: "IX_Events_SeriesId",
    table: "Events",
    column: "SeriesId");   // NOT unique
```
`FK_Events_EventSeries_SeriesId` (line 55-59) declares no `onDelete`, which EF Core defaults to `NO ACTION` for an optional relationship — D-12's Delete/Detach outcomes must be written as deliberate multi-step operations, not a migration-level cascade change.

**Filtered unique index to add** (`QuestBoardContext.OnModelCreating`, alongside the existing `EventEntity` index block at `QuestBoardContext.cs:308-309`):
```csharp
modelBuilder.Entity<EventEntity>()
    .HasIndex(e => new { e.SeriesId, e.SeriesSlotIndex })
    .IsUnique()
    .HasFilter("[SeriesId] IS NOT NULL")
    .HasDatabaseName("IX_Events_SeriesId_SeriesSlotIndex");
```

---

### `QuestBoard.Service/Jobs/RecurringOccurrenceTopUpJob.cs` (NEW)

**Analog:** `QuestBoard.Service/Jobs/DailyReminderJob.cs` (full file, 46 lines) + `QuestBoard.Service/Jobs/HangfireJobHelper.cs` (full file, 35 lines)

**Imports/DI pattern** (`DailyReminderJob.cs:1-11`):
```csharp
using QuestBoard.Domain.Interfaces;
using Hangfire;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace QuestBoard.Service.Jobs;

public class DailyReminderJob(
    IServiceScopeFactory scopeFactory,
    IBackgroundJobClient backgroundJobClient,
    ILogger<DailyReminderJob> logger)
```

**Core pattern to follow exactly, with one deliberate deviation** (`DailyReminderJob.cs:13-44`):
```csharp
public async Task ExecuteAsync(CancellationToken cancellationToken = default)
{
    var tomorrow = DateTime.Today.AddDays(1);
    await HangfireJobHelper.RunInScopeAsync(scopeFactory, groupId: null, async sp =>
    {
        var questRepository = sp.GetRequiredService<IQuestRepository>();
        var quests = await questRepository.GetQuestsForTomorrowAllGroupsAsync(tomorrow, cancellationToken);
        // ^ IgnoreQueryFilters() internally -- fine because read-only.
        ...
    });
}
```
**Deviation the new job MUST make (D-28):** this job writes, so it cannot call `RunInScopeAsync` once with `groupId: null` and `IgnoreQueryFilters()`. It must enumerate real group ids via `GroupRepository.GetAllWithMemberCountAsync()` (no query filter on `GroupEntity` — it is the tenant boundary itself) in one scope call, then call `RunInScopeAsync` **once per group** with a real, non-null `groupId`, per RESEARCH.md Pattern 3.

**`HangfireJobHelper.RunInScopeAsync` (reuse verbatim, no changes needed)** — full source:
```csharp
internal static class HangfireJobHelper
{
    internal static async Task RunInScopeAsync(
        IServiceScopeFactory scopeFactory,
        int? groupId,
        Func<IServiceProvider, Task> action)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        if (groupId is not null)
        {
            var groupContext = scope.ServiceProvider.GetRequiredService<ActiveGroupContextService>();
            groupContext.SetGroupId(groupId);
        }
        await action(scope.ServiceProvider);
    }
}
```

**Registration pattern** — `Program.cs` around line 355 (exact text not re-quoted here; RESEARCH.md confirms the shape `RecurringJob.AddOrUpdate<DailyReminderJob>("daily-session-reminders", job => job.ExecuteAsync(CancellationToken.None), "0 9 * * *")`). Register the new job as a sibling with a distinct id and off-peak cron string, placed after `ConfigureDatabase()` so migrations have run first.

**Stale doc-comment hazard to fix in the same diff** — `QuestBoard.Service/Services/ActiveGroupContextService.cs:16-24`:
```csharp
/// Returns the overridden group ID (set by Hangfire jobs via SetGroupId),
/// or reads from Session for normal HTTP requests.
/// Returns null when no override is set and HttpContext is absent — null means "see all".
public int? ActiveGroupId => ...
```
This comment is false — every `HasQueryFilter` in `QuestBoardContext.cs` is fail-closed (confirmed lines 339-455: `activeGroupContext.ActiveGroupId != null && e.GroupId == activeGroupContext.ActiveGroupId`, for every filtered entity including `EventEntity`/`EventSeriesEntity`/`EventSignupEntity` at lines 428-445). A null `ActiveGroupId` returns **zero rows**, never all rows. Correct the comment as part of this phase's diff.

---

### `QuestBoard.UnitTests/Services/RecurringOccurrenceTopUpJobTests.cs` (NEW)

**Analog:** `QuestBoard.UnitTests/Services/DailyReminderJobTests.cs` (full file, 85 lines) — the mocked-scope-factory shape:
```csharp
var serviceProvider = Substitute.For<IServiceProvider>();
serviceProvider.GetService(typeof(IQuestRepository)).Returns(_questRepository);

var scope = Substitute.For<IServiceScope>();
scope.ServiceProvider.Returns(serviceProvider);

_scopeFactory = Substitute.For<IServiceScopeFactory>();
_scopeFactory.CreateAsyncScope().Returns(new AsyncServiceScope(scope));
```
**Deviation the new test must make:** because the job now calls `RunInScopeAsync` once per group (not once with `groupId: null`), the test must assert `SetGroupId` (or the equivalent group-context call) was invoked once per seeded group id — not merely that the scope factory was called once. This is exactly the assertion RESEARCH.md's Wave-0 gap list calls for ("must assert per-group `SetGroupId` calls, not a single cross-group call").

---

### `QuestBoard.UnitTests/Repository/EventSeriesMaterializationTests.cs` (NEW)

**Analog:** `QuestBoard.UnitTests/Repository/EventSignupRepositoryTests.cs` (full file, 302 lines) — the InMemory-DB + `IActiveGroupContext` test-double shape:
```csharp
private static QuestBoardContext CreateContext(string databaseName, IActiveGroupContext activeGroupContext)
{
    var options = new DbContextOptionsBuilder<QuestBoardContext>()
        .UseInMemoryDatabase(databaseName)
        .Options;
    return new QuestBoardContext(options, activeGroupContext);
}

private sealed class MutableTestGroupContext : IActiveGroupContext
{
    public int? ActiveGroupId { get; set; }
}
```
Use `MutableTestGroupContext` (settable `ActiveGroupId`) exactly as `EventSignupRepositoryTests` does for its cross-group rejection test (`SetAvailabilityAsync_EventNotVisibleThroughActiveBoard_ThrowsAndWritesNoRow`, lines 132-157) as the template for this phase's idempotency tests: run the generator/materializer twice and assert row count is unchanged; cancel-then-run; move-then-run; move-outside-runway-then-run (D-20's specific named risk).

---

### `QuestBoard.IntegrationTests/Tests/EventSeriesTenantIsolationTests.cs` (NEW)

**Analog:** `QuestBoard.IntegrationTests/Tests/EventTenantIsolationTests.cs` (full file, 333 lines) — mirror its exact shape:
- Seeding helper pattern (`SeedOtherBoardEventAsync`, lines 50-69) — seed on an unfiltered context (`ActiveGroupId = null` on the seeding side) since the read filter offers no protection on an insert.
- `factory.TestGroupContext.ActiveGroupId = 1;` before every act, reset in `DisposeAsync` (lines 25-29).
- The desktop + mobile dual-render check (`GroupFilter_HidesEventFromOtherGroupOnDesktopCalendar` / `...OnMobileAgenda`, lines 71-167) — extend to series occurrences and the new Series Details page.
- The cross-board-reference rejection test (`Edit_Post_EventPointingAtAnotherBoardSchedule_ReturnsBadRequest`, lines 277-332) is the direct template for testing that a series action (Edit/Cancel/End/Delete/Detach) rejects a cross-board `SeriesId`, using the same double-check precedent already implemented at `EventsController.cs:199-208` (`SeriesIsOnActiveBoardAsync`).
- The posted-GroupId-ignored test (`Create_Post_PostedGroupIdIsIgnored_ServerStampsActiveBoard`, lines 248-275) is the template for confirming the series-create path also stamps `GroupId` from `activeGroupContext`, never from the form.

---

### `QuestBoard.Service/Controllers/Events/EventsController.cs` (MODIFY)

**Analog:** itself, as shipped (315 lines, read in full).

**Existing server-side re-resolution precedent to extend for D-16 (Cancel replaces Delete)** — `EventsController.cs:195-208`:
```csharp
// The read filter already hides another board's schedule, and this explicit comparison is
// a deliberate second layer so a weakened filter still cannot let an event be saved against
// another board's schedule.
private async Task<bool> SeriesIsOnActiveBoardAsync(int seriesId, CancellationToken token)
{
    if (activeGroupContext.ActiveGroupId is not { } groupId) return false;
    var seriesGroupId = await eventService.GetSeriesGroupIdAsync(seriesId, token);
    return seriesGroupId.HasValue && seriesGroupId.Value == groupId;
}
```
Follow this exact shape for the new Cancel action's own server-side check: re-resolve `existingEvent.SeriesId` on the POST itself before allowing Cancel, and — mirroring the `QuestController.cs:762` precedent restated in RESEARCH.md ("never trust the client-rendered button visibility") — reject a Delete POST against a series occurrence rather than merely hiding the Delete button in the view.

**Existing board-type re-resolution precedent (same pattern, for reference)** — `QuestController.cs:760-766`:
```csharp
// Close/Reopen only makes sense for campaign-board quests; never trust the
// client-rendered button visibility to enforce this server-side.
var boardType = await GetActiveBoardTypeAsync();
if (boardType != BoardType.Campaign)
{
    return BadRequest("Close is only supported for campaign quests.");
}
```

**Existing Edit action to extend for D-09 scope**  — `EventsController.cs:118-165` — add a `scope` parameter (or dedicated view-model field) bound from the hidden form field the D-09 modal sets; branch between "only this event" (existing single-entity update path, unchanged) and "this and future" (call the new narrow sweep repository method described above).

**Existing Create action to extend for D-06/D-08** — `EventsController.cs:53-100` — wrap series-row-creation + first full generation pass in one transaction (D-25); reuse the existing Campaign-vs-OneShot branch (`boardTypeResolver.GetBoardTypeAsync`) that already decides `AddWithCampaignFanOutAsync` vs `AddAsync`, applying it per generated occurrence rather than once.

---

### `QuestBoard.Service/Views/Events/Create.cshtml` (MODIFY)

**Analog:** itself (60 lines, full file read) + two borrowed idioms per UI-SPEC:

Existing form shape to extend (`Create.cshtml:16-46`) — Title/Date/StartTime/Description fields already present; the repeats toggle and revealed section insert after the existing Description block, before the `<hr/>` button row.

**Checkbox-reveals-a-section idiom** (`Views/ShopManagement/Create.cshtml:117-123`):
```html
<div class="form-check mb-3">
    <input class="form-check-input" type="checkbox" id="enableAvailabilityWindow" onchange="toggleAvailabilityWindow()">
    <label class="form-check-label" for="enableAvailabilityWindow">
        Set specific availability dates for this item
    </label>
    <div class="form-text">Check this to set when the item should be available in the shop. Leave unchecked for permanent availability.</div>
</div>
```
UI-SPEC deviation: because the revealed content (cadence + mask + preview) is structurally larger than two `disabled` inputs, use a JS-toggled `d-none` class on a wrapper `<div>` rather than `disabled` attributes on always-rendered inputs.

**fetch-POST idiom for the live preview** — `Views/Events/Details.cshtml:207-227` (`setAvailability`, this same view's own script block) is the closest already-in-this-view example of the FormData+antiforgery+fetch shape (`Views/Quest/Details.cshtml:944-985` is the canonical source RESEARCH.md cites, same shape):
```javascript
function setAvailability(eventId, availability) {
    const formData = new FormData();
    formData.append('__RequestVerificationToken', '@tokens.RequestToken');
    formData.append('availability', availability);
    fetch(`/Events/SetAvailability/${eventId}`, { method: "POST", body: formData })
        .then(res => { if (res.ok) { location.reload(); } else { res.text().then(text => alert(...)); } })
        .catch(err => alert("An error occurred..."));
}
```
**Deviation for the preview fetch specifically:** do not `location.reload()` on success or `alert()` on failure — it is read-only and non-blocking (per UI-SPEC); replace the preview list's contents on success, show inline error copy on failure, debounce 400ms, guard against out-of-order responses with a request-token comparison.

---

### `QuestBoard.Service/Views/Events/Details.cshtml` (MODIFY) / `Views/Series/Details.cshtml` (NEW)

**Analog:** `Views/Events/Details.cshtml` itself (230 lines, full file read) is both the file to modify and the structural template for the new Series Details page.

Existing `modern-card` / `Actions` card shape to extend for D-16 (Cancel vs Delete) and D-06 (series link) — `Details.cshtml:33-51`:
```html
@if (Model.CanManage)
{
    <div class="card modern-card">
        <div class="card-header modern-card-header"><h4 class="mb-0">Actions</h4></div>
        <div class="card-body modern-card-body">
            <a href="@Url.Action("Edit", new { id = Model.Id })" class="btn btn-warning w-100 mb-2">
                <i class="fas fa-pen me-2"></i>Edit Event
            </a>
            <form asp-action="Delete" method="post" onsubmit="return confirm('@deleteConfirmMessage');">
                <input type="hidden" name="id" value="@Model.Id" />
                <button type="submit" class="btn btn-danger w-100"><i class="fas fa-trash me-2"></i>Delete Event</button>
            </form>
        </div>
    </div>
}
```
For a series occurrence (`Model.SeriesId != null`), swap the Delete form for a Cancel form (`btn-outline-warning`, `fa-ban`, native `confirm()` per UI-SPEC D-16 copy) and add the "View Series Details" link above it. One-off events (`Model.SeriesId == null`) keep this block byte-for-byte unchanged.

**`modern-card` header pattern to open the new Series Details page** (per CLAUDE.md, and matching this same file's own header conventions):
```html
<div class="card-header modern-card-header">
    <h2 class="mb-0">
        <i class="fas fa-repeat text-purple me-2"></i>
        {Series.Title} — Recurring Series
    </h2>
</div>
```

---

### `QuestBoard.Service/Views/Shared/_Calendar.cshtml` (MODIFY — cancelled chip only)

**Analog:** itself, the existing guarded chip block:
```html
@* empty by default, so the callers of this partial that build their own *@
@* model and never populate events render nothing here without any flag *@
@* to set. *@
@if (day.EventsOnDay.Any())
{
    <div class="calendar-events">
        @foreach (var eventOnDay in day.EventsOnDay)
        {
            <div class="calendar-event" title="@eventOnDay.Event.Title - @eventOnDay.TimeLabel">
                <a href="@Url.Action("Details", "Events", new { id = eventOnDay.Event.Id })" class="quest-link">
                    <div class="calendar-event-title"><i class="fas fa-calendar-day me-1"></i>@eventOnDay.Event.Title</div>
                    <div class="calendar-event-time">@eventOnDay.TimeLabel</div>
                </a>
            </div>
        }
    </div>
}
```
Add only a conditional CSS modifier class (`cancelled`) on the existing `.calendar-event` div when `eventOnDay.Event.IsCancelled`. **Hard guardrail:** this partial has 6 call sites; only `Views/Calendar/Index.cshtml` is in scope. The other 5 (`Views/Quest/Details.cshtml:604,648,696`, `Views/Quest/Details.Mobile.cshtml:158,196`) never populate `EventsOnDay`, so the class can never be reached there — do not add a new `ViewBag` flag or any markup (e.g. the D-26 horizon banner) that those 5 sites would need to explicitly opt out of.

## Shared Patterns

### Server-side re-resolution over client-rendered visibility (D-16, D-09, D-24)
**Source:** `QuestBoard.Service/Controllers/Events/EventsController.cs:195-208` (`SeriesIsOnActiveBoardAsync`), `QuestBoard.Service/Controllers/QuestBoard/QuestController.cs:760-766`
**Apply to:** Cancel action (re-resolve `SeriesId` before allowing), Edit action (re-resolve scope + cross-board series reference), Delete action (reject outright for a series occurrence).

### Fail-closed tenant scoping via `HasQueryFilter` + explicit second-layer check
**Source:** `QuestBoard.Repository/Entities/QuestBoardContext.cs:428-445` (filters) + `EventsController.cs:199-208` (second layer)
**Apply to:** every new EventSeries read/write path — a null `ActiveGroupId` must return zero rows, and any cross-entity reference (event → series) needs its own explicit group-id comparison because the filter alone does not protect an insert.

### Narrow scalar-update repository methods, never `BaseRepository.UpdateAsync` once a navigation collection exists
**Source:** `QuestBoard.Repository/PlayerSignupRepository.cs:43-` (`ChangeVoteAsync`)
**Apply to:** the D-09 future-occurrence sweep and the D-14 cancel write on `EventRepository`, both because of `EventEntity.Signups` and because the sweep needs per-row skip-logic no generic update path offers.

### Hangfire per-group scope pattern, no `IgnoreQueryFilters()` for writes
**Source:** `QuestBoard.Service/Jobs/HangfireJobHelper.cs` (reused verbatim) + `QuestBoard.Service/Jobs/DailyReminderJob.cs` (pattern to extend, with the one-scope-per-group deviation)
**Apply to:** `RecurringOccurrenceTopUpJob`.

### `modern-card` / `modern-card-header` / `modern-card-body` view convention (per CLAUDE.md)
**Source:** `QuestBoard.Service/Views/Events/Details.cshtml` (every card block), `QuestBoard.Service/Views/Events/Create.cshtml`
**Apply to:** every new/modified view this phase touches, including the new Series Details page.

### Toast feedback via `TempData["Success"]`, no new plumbing
**Source:** `EventsController.cs:97,162,183` (`TempData["Success"] = "..."`)
**Apply to:** series created/ended/deleted/detached, occurrence cancelled/updated toasts (`_Toasts.cshtml` already renders these app-wide).

## No Analog Found

| File | Role | Data Flow | Reason |
|---|---|---|---|
| `QuestBoard.Domain/Services/EventSeriesDateGenerator.cs` | service (pure algorithm) | transform | This codebase has no prior pure, no-DI, no-I/O algorithm class — every existing "service" is DI-constructed and touches a repository. RESEARCH.md's own Pattern 1 code block is the closest thing to a spec and should be treated as load-bearing, not merely illustrative. |
| `QuestBoard.UnitTests/Services/EventSeriesDateGeneratorTests.cs` | test (pure unit) | transform | No existing test class in the repo tests a static, DB-free, DI-free method — every existing unit test either mocks a repository/service or spins up an InMemory `QuestBoardContext`. Use plain xUnit `[Fact]`/`[Theory]` with no fixture, no substitute, no context — the simplest test shape in the stack, just none of the existing files happen to demonstrate it. |

## Metadata

**Analog search scope:** `QuestBoard.Domain/`, `QuestBoard.Repository/`, `QuestBoard.Service/Controllers/`, `QuestBoard.Service/Views/`, `QuestBoard.Service/Jobs/`, `QuestBoard.UnitTests/`, `QuestBoard.IntegrationTests/`
**Files scanned:** ~20 direct reads (entities, migration, context, controller, job, helper, 3 test classes, 2 views, 1 repository) plus 76-CONTEXT.md / 76-RESEARCH.md / 76-UI-SPEC.md
**Pattern extraction date:** 2026-08-28
