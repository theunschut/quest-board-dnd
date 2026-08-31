# Phase 77: Availability Overview Page - Pattern Map

**Mapped:** 2026-08-29
**Files analyzed:** 15 (new/modified)
**Analogs found:** 15 / 15

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|--------------------|------|-----------|-----------------|---------------|
| `QuestBoard.Domain/Interfaces/IEventRepository.cs` (+ method) | model/interface | CRUD (bounded read) | same file, `GetEventsForCalendarAsync` doc block | exact (same interface, extend) |
| `QuestBoard.Repository/EventRepository.cs` (+ method) | service/repository | CRUD (single-query aggregate) | `EventSignupRepository.cs:60` `GetRosterForEventAsync` + `EventRepository.cs:13` `GetEventsForCalendarAsync` | exact |
| `QuestBoard.Domain/Services/EventService.cs` (+ method, aggregation) | service | transform (in-memory aggregation) | `EventSignupService.GetRosterForEventAsync` (thin pass-through) — pattern for wiring; count/cell logic is new but the "service does aggregation, not AutoMapper" split is the analog decision | role-match |
| `QuestBoard.Service/Controllers/Events/EventsController.cs` (+ `Index` action) | controller | request-response | `CalendarController.Index(int? year, int? month)` for int query-string validation shape; `EventsController.Details`/`Create` in same file for SuperAdmin/no-active-group short-circuit (`GetEffectiveRoleAsync`, `IsDmTierAsync`) | exact |
| `QuestBoard.Service/ViewModels/EventViewModels/EventOverviewViewModel.cs` (new) | model (viewmodel) | transform | `EventViewModel` (same folder), `CalendarViewModel` | role-match |
| `QuestBoard.Service/ViewModels/EventViewModels/EventOverviewRowViewModel.cs` (new) | model (viewmodel) | transform | `EventSignupViewModel`, `SeriesOccurrenceViewModel` | role-match |
| `QuestBoard.Service/ViewModels/EventViewModels/MemberCellViewModel.cs` (new) | model (viewmodel) | transform | `EventSignupViewModel` | role-match |
| `QuestBoard.Repository/Automapper/EntityProfile.cs` (no change expected; verify) | config (mapping) | transform | `CreateMap<EventSignupEntity, EventSignup>()` block, lines 159-166 | exact (reference only — Repository boundary needs no new members) |
| `QuestBoard.Service/Automapper/ViewModelProfile.cs` (+ maps) | config (mapping) | transform | `CreateMap<Event, EventViewModel>()` block, lines 104-126 | exact |
| `QuestBoard.Service/Views/Events/Index.cshtml` (new) | component (Razor view) | request-response | `Views/Calendar/Index.cshtml` (card header, `modern-card` shell); `Views/Quest/Manage.cshtml:159-213` (per-row Yes/Maybe/No counting layout) | role-match / exact for count block |
| `QuestBoard.Service/Views/Events/Index.Mobile.cshtml` (new) | component (Razor view) | request-response | `Views/Calendar/Index.Mobile.cshtml` (UA-selected mobile card list) | exact |
| `QuestBoard.Service/Views/Shared/_Layout.cshtml` (nav dropdown edit) | component (partial/layout) | request-response | Same file, existing user-menu dropdown (`dropdown-toggle`/`dropdown-menu`, near line 177) and existing flat Calendar `<li>` (lines 162-176) | exact |
| `QuestBoard.Service/Views/Shared/_Layout.Mobile.cshtml` (flat entry edit) | component (partial/layout) | request-response | Same file, existing flat Calendar `<li>` (lines 141-152) | exact |
| `QuestBoard.Service/Views/Calendar/Index.cshtml` / `Index.Mobile.cshtml` (cross-link edit) | component (Razor view) | request-response | Same files, existing header nav-button row / month-nav row | exact |
| `QuestBoard.IntegrationTests/Tests/EventsOverviewTenantIsolationTests.cs` (new) | test | event-driven (HTTP request/response over seeded state) | `QuestBoard.IntegrationTests/Tests/EventAvailabilityTenantIsolationTests.cs` | exact |
| `QuestBoard.IntegrationTests/Controllers/LayoutNavigationTests.cs` (extend) | test | request-response | Same file, existing `Nav_CampaignDm_CalendarLinkPresent`-style theory tests | exact |

## Pattern Assignments

### `QuestBoard.Repository/EventRepository.cs` (repository, CRUD bounded aggregate read)

**Analog:** `QuestBoard.Repository/EventSignupRepository.cs:60-73` (`GetRosterForEventAsync`) generalized with `QuestBoard.Repository/EventRepository.cs:13-25` (`GetEventsForCalendarAsync`)

**Imports pattern** (`EventRepository.cs:1-6`):
```csharp
using AutoMapper;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.Repository.Entities;
using Microsoft.EntityFrameworkCore;
```

**Core single-query aggregate pattern to copy** (generalized from `EventSignupRepository.cs:60-73` + `EventRepository.cs:13-25`):
```csharp
public async Task<IList<Event>> GetUpcomingWithSignupsAsync(
    DateOnly today, int take, CancellationToken token = default)
{
    // Group scoping is enforced entirely by EventEntity's and EventSignupEntity's fail-closed
    // query filters here -- no manual GroupId .Where is needed or added, and no
    // IgnoreQueryFilters() appears anywhere in this method.
    var entities = await DbContext.Events
        .Where(e => e.Date >= today && e.CancelledAt == null)
        .OrderBy(e => e.Date)
        .ThenBy(e => e.StartTime)
        .Take(take)
        .Include(e => e.Signups)
            .ThenInclude(s => s.User)
        .AsNoTracking()
        .ToListAsync(token);

    return Mapper.Map<IList<Event>>(entities);
}
```
Key details to preserve from the analog: no manual `GroupId` filter (relies on the ambient `HasQueryFilter`), deterministic `OrderBy...ThenBy` before `Take` (see `EventRepository.cs`'s existing calendar read for the exact ordering columns), `AsNoTracking()` for a read-only path, and a same-shape XML doc comment on the interface stating "group scoping is enforced by the entity's query filter, not by a parameter" (copy the doc style from `IEventRepository.cs`'s `GetEventsForCalendarAsync`).

**Error handling:** None needed — this repository layer has no try/catch anywhere; EF exceptions propagate. No analog file in this repo wraps a read in try/catch at this layer.

---

### `QuestBoard.Domain/Interfaces/IEventRepository.cs` (interface, add one method)

**Analog:** same file — every existing method's XML doc style

**Doc-comment pattern to copy** (from `IEventRepository.cs`, `GetEventsForCalendarAsync` doc block):
```csharp
/// <summary>
/// Returns every event in the active group, ordered by date then start time. Group scoping
/// is enforced by the entity's query filter, not by a parameter on this method. ...
/// </summary>
Task<IList<Event>> GetEventsForCalendarAsync(CancellationToken token = default);
```
New method should follow the same voice: state what is fetched, state that scoping is filter-enforced (not parameter-enforced), and state the exclusion (`CancelledAt == null`) and lower bound (`Date >= today`) explicitly, mirroring how `GetOccurrencesForSeriesAsync`'s doc states "past and future, cancelled included" as the deliberate contrast case.

---

### `QuestBoard.Service/Controllers/Events/EventsController.cs` (controller, add `Index` action)

**Analog:** `CalendarController.Index` (int query-string validation) + same-file `EventsController.Details`/`Create` (SuperAdmin/no-active-group short-circuit)

**Imports pattern** (`EventsController.cs:1-9`):
```csharp
using AutoMapper;
using QuestBoard.Domain.Enums;
using QuestBoard.Domain.Extensions;
using QuestBoard.Domain.Interfaces;
using QuestBoard.Domain.Models;
using QuestBoard.Domain.Services;
using QuestBoard.Service.ViewModels.EventViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
```

**Query-string int validation pattern** (`CalendarController.cs:20-35`):
```csharp
[HttpGet]
public async Task<IActionResult> Index(int? year = null, int? month = null, CancellationToken token = default)
{
    var currentDate = DateTime.Now;
    var selectedYear = year ?? currentDate.Year;
    var selectedMonth = month ?? currentDate.Month;

    if (selectedMonth < 1 || selectedMonth > 12)
    {
        return BadRequest("Invalid month. Month must be between 1 and 12.");
    }
    if (selectedYear < 1900 || selectedYear > 2100)
    {
        return BadRequest("Invalid year. Year must be between 1900 and 2100.");
    }
    ...
```
Apply the same shape to `take`: default when absent, clamp/reject out-of-range rather than passing an unbounded value straight to `.Take()` (Security Domain V5 in RESEARCH.md flags this explicitly).

**Auth pattern:** `[Authorize]` class-level attribute only (`EventsController.cs:13`) — no `[Authorize(Policy = "DungeonMasterOnly")]` on the new `Index`, since D-16 makes this an all-members page. Do **not** call `IsDmTierAsync()`/`GetEffectiveRoleAsync()` for gating (they exist in this same file for `Details`/`Create` — listed in CONTEXT.md specifically so nobody copies them here out of habit).

**SuperAdmin / no-active-group short-circuit pattern** (`EventsController.cs:66-68`, mirrored at `:222`, and `GetEffectiveRoleAsync` at `:526-528`):
```csharp
// A SuperAdmin has no active group by design, so there is no board to stamp onto the
// new event. Send them to pick one rather than letting the write throw.
if (activeGroupContext.ActiveGroupId is not { } activeGroupId)
{
    return RedirectToAction("Index", "GroupPicker");
}
```
For a **read** action (unlike this write example) the correct mirror is the pattern proven by `EventsControllerIntegrationTests.Details_Get_SuperAdminWithNoActiveGroup_DoesNotThrow` — the new `Index` should short-circuit to an empty/none result rather than calling `RequireActiveGroupId()` unconditionally (see RESEARCH.md Pitfall 4). Read `EventsController.cs` around `GetEffectiveRoleAsync` (`:518-528`) for the exact `User.IsInRole("SuperAdmin")` check idiom:
```csharp
private async Task<GroupRole?> GetEffectiveRoleAsync() =>
    User.IsInRole("SuperAdmin")
        ? GroupRole.Admin
        : await userService.GetEffectiveGroupRoleAsync(User, activeGroupContext.Require...);
```

---

### `QuestBoard.Domain/Services/EventService.cs` (domain service, aggregation logic — EVTVIEW-02/03)

**Analog:** `EventSignupService.GetRosterForEventAsync` — a one-line pass-through to the repository; the *pattern* to copy is "thin service delegates to repository," with the new count/cell-state aggregation added as plain C# in this layer per RESEARCH.md's explicit recommendation (not in AutoMapper).

**Aggregation pattern** (illustrative, per RESEARCH.md Pattern 2, built from shipped signals):
```csharp
// EventSignup.HasAnswered (QuestBoard.Domain/Models/EventSignup.cs:25) and
// VoteType {No=0, Maybe=1, Yes=2} are the only inputs — do not re-derive HasAnswered.
var yesCount = eventSignups.Count(s => s.Availability == VoteType.Yes);                          // D-05
var confirmedCount = eventSignups.Count(s => s.Availability == VoteType.Yes && s.HasAnswered);   // D-06
var maybeCount = eventSignups.Count(s => s.Availability == VoteType.Maybe);                      // D-07

// Per-member cell state (5 states, D-01/D-02/D-03):
//   no row at all                          -> Empty
//   row exists, HasAnswered == false, Yes  -> MutedYesDefault
//   row exists, HasAnswered == true        -> ConfirmedYes / ConfirmedMaybe / ConfirmedNo
```

---

### `QuestBoard.Service/Automapper/ViewModelProfile.cs` (+ new maps)

**Analog:** `CreateMap<Event, EventViewModel>()` block, lines 104-126

**Pattern to copy:**
```csharp
CreateMap<Event, EventViewModel>()
    .ForMember(dest => dest.CanManage, opt => opt.Ignore())
    // Roster, IsOneShotBoard, HasOwnSignup and MyAvailability are all computed
    // server-side per request, exactly like CanManage above.
    ...

CreateMap<EventSignup, EventSignupViewModel>();
```
For the new `Event → EventOverviewRowViewModel` map: ignore every computed/aggregated member (counts, cell states) with `.ForMember(dest => dest.X, opt => opt.Ignore())`, matching how `CanManage`/`Roster`/`IsOneShotBoard` are ignored on the existing `EventViewModel` map and filled in the controller/service instead. No reverse map is needed — this page is read-only, matching the precedent set by `CreateMap<EventSignup, EventSignupViewModel>()` (no reverse) and `CreateMap<EventSeries, SeriesDetailsViewModel>()` (explicitly no reverse, "the ... page is read-only").

---

### `QuestBoard.Service/Views/Events/Index.cshtml` (desktop grid view)

**Analog:** `Views/Calendar/Index.cshtml` (card shell, header nav row) + `Views/Quest/Manage.cshtml:159-213` (Yes/Maybe/No counting block)

**Card shell pattern** (`Calendar/Index.cshtml:1-27`):
```html
@model CalendarViewModel
@{
    ViewData["Title"] = "Quest Calendar";
}
<div class="card modern-card">
    <div class="card-header modern-card-header d-flex justify-content-between align-items-center">
        <h2 class="mb-0">
            <i class="fas fa-calendar-alt text-info me-2"></i>
            @ViewData["Title"]
        </h2>
        <div class="calendar-navigation"> ... </div>
    </div>
    <div class="card-body modern-card-body p-0"> ... </div>
</div>
```
The exact `modern-card` / `modern-card-header` / `modern-card-body` triad is mandatory per `CLAUDE.md`; `77-UI-SPEC.md` section 3 already gives the full verbatim markup to use in place of the calendar's own table body — treat UI-SPEC.md as authoritative for this file's body content, this analog only for the header/card-shell idiom.

**Per-row Yes/Maybe/No counting pattern** (`Quest/Manage.cshtml:163-165, 189-208`):
```csharp
var yesVotes = date.PlayerVotes.Where(v => v.Vote == VoteType.Yes).ToList();
var maybeVotes = date.PlayerVotes.Where(v => v.Vote == VoteType.Maybe).ToList();
var noVotes = date.PlayerVotes.Where(v => v.Vote == VoteType.No).ToList();
```
```html
<small class="text-success"><strong>Yes (@yesVotes.Count):</strong></small>
```
This is the nearest existing precedent for "count + colored label" on a per-date/per-row basis; the new page's count-summary block (UI-SPEC section 2) is the same idea reshaped into the two-line Display/Label format D-08 requires — copy the "compute the three filtered lists once per row, reuse everywhere" structure, not the exact three-column layout (that part is superseded by UI-SPEC.md).

---

### `QuestBoard.Service/Views/Events/Index.Mobile.cshtml` (mobile card view)

**Analog:** `Views/Calendar/Index.Mobile.cshtml` — confirmed to exist as the UA-selected sibling of `Calendar/Index.cshtml`; follow its `.cshtml`/`.Mobile.cshtml` naming and the same `@model` type shared with the desktop view. Markup body is fully specified in `77-UI-SPEC.md` section 4 (collapse-toggle roster, `event.stopPropagation()` on the toggle).

---

## Shared Patterns

### Tenant scoping (D-26, EVTVIEW-04) — apply to the new repository method and nothing else needs to change
**Source:** `QuestBoard.Repository/Entities/QuestBoardContext.cs:448-465`
```csharp
modelBuilder.Entity<EventEntity>()
    .HasQueryFilter(e =>
        activeGroupContext.ActiveGroupId != null &&
        e.GroupId == activeGroupContext.ActiveGroupId);

modelBuilder.Entity<EventSignupEntity>()
    .HasQueryFilter(es =>
        activeGroupContext.ActiveGroupId != null &&
        es.Event.GroupId == activeGroupContext.ActiveGroupId);
```
**Apply to:** `EventRepository`'s new method — never add a manual `.Where(e => e.GroupId == ...)` alongside this, and never call `IgnoreQueryFilters()`. The one existing `IgnoreQueryFilters()` call in the codebase (`GroupRepository.GetEventSignupsForMemberIgnoringActiveBoardAsync`) is private, group-pinned, and is explicitly *not* a precedent (CONTEXT.md D-26).

### SuperAdmin / no-active-group handling
**Source:** `QuestBoard.Service/Controllers/Events/EventsController.cs:518-528` (`IsDmTierAsync`, `GetEffectiveRoleAsync`) and `:66-68` (redirect-on-write example)
**Apply to:** The new `Index` action — mirror the existing short-circuit rather than inventing a third behavior (CONTEXT.md discretion note; RESEARCH.md Pitfall 4).

### Query-string int validation
**Source:** `QuestBoard.Service/Controllers/QuestBoard/CalendarController.cs:20-35`
**Apply to:** The new `Index` action's `take` parameter — default, then explicit range check with `BadRequest(...)`/clamp before use, never pass the raw client value into `.Take()`.

### AutoMapper: ignore server-computed members
**Source:** `QuestBoard.Service/Automapper/ViewModelProfile.cs:104-126` (`CanManage`, `Roster`, etc. ignored on `Event → EventViewModel`)
**Apply to:** `Event → EventOverviewRowViewModel` map — ignore every count/cell-state member; fill them in the domain service, not the mapping profile (RESEARCH.md Open Question 2 recommendation).

### Nav dropdown / flat-list markup
**Source:** `QuestBoard.Service/Views/Shared/_Layout.cshtml` (existing flat Calendar `<li>` around line 162-176, existing user-menu `dropdown-toggle`/`dropdown-menu` idiom near line 179) and `_Layout.Mobile.cshtml:141-152` (flat `<li>`, zero dropdowns in the whole file)
**Apply to:** `_Layout.cshtml`'s Calendar entry (convert to dropdown per D-19, exact markup already given verbatim in `77-UI-SPEC.md` section 8) and `_Layout.Mobile.cshtml` (add a second flat sibling `<li>`, same gate, per D-20). Both keep the existing `activeBoardType is BoardType.OneShot or BoardType.Campaign` gate unchanged (D-22) — do not add a role condition.

### Two-group tenant isolation test
**Source:** `QuestBoard.IntegrationTests/Tests/EventAvailabilityTenantIsolationTests.cs`
```csharp
public class EventAvailabilityTenantIsolationTests(WebApplicationFactoryBase factory)
    : IClassFixture<WebApplicationFactoryBase>, IAsyncLifetime
{
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public ValueTask DisposeAsync()
    {
        factory.TestGroupContext.ActiveGroupId = 1;
        factory.TestGroupContext.BoardType = BoardType.OneShot;
        return ValueTask.CompletedTask;
    }

    private async Task<int> SeedOtherBoardEventAsync(string title, DateOnly date)
    {
        await using var ctx = factory.Database.CreateContext();
        if (!ctx.Groups.Any(g => g.Id == 2))
        {
            ctx.Groups.Add(new GroupEntity { Id = 2, Name = "OtherAvailabilityBoard", CreatedAt = DateTime.UtcNow });
        }
        var otherEvent = new EventEntity { Title = title, GroupId = 2, Date = date, CreatedAt = DateTime.UtcNow };
        ctx.Events.Add(otherEvent);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        return otherEvent.Id;
    }
    // ... SeedGroupOneEventAsync mirrors this against GroupId 1, through the same unfiltered
    // seeding DbContext (factory.Database.CreateContext()), never through the request pipeline.
}
```
**Apply to:** `QuestBoard.IntegrationTests/Tests/EventsOverviewTenantIsolationTests.cs` (new file) — copy this class shape verbatim: seed via the unfiltered `factory.Database.CreateContext()`, seed a same-named member on both boards so a leak is visible rather than coincidentally distinguishable, hit the new overview endpoint as an authenticated group-1 member, assert neither the other board's event title nor its member's name appears, reset `ActiveGroupId = 1` in `DisposeAsync`.

### Nav string-assertion test pattern
**Source:** `QuestBoard.IntegrationTests/Controllers/LayoutNavigationTests.cs:54-70` (`Nav_CampaignDm_CalendarLinkPresent`)
```csharp
[Theory]
[InlineData(DesktopUserAgent)]
[InlineData(MobileUserAgent)]
public async Task Nav_CampaignDm_CalendarLinkPresent(string userAgent)
{
    _factory.TestGroupContext.BoardType = BoardType.Campaign;
    var (authClient, _) = await AuthenticationHelper.CreateAuthenticatedDMClientAsync(
        _factory, "navcal_dm", "navcal_dm@test.com");
    var (response, html) = await GetWithUserAgentAsync("/quests", userAgent, authClient.DefaultRequestHeaders.Authorization);
    response.StatusCode.Should().Be(HttpStatusCode.OK);
    html.Should().Contain("Calendar");
}
```
**Apply to:** New theory cases in the same file asserting `html.Should().Contain("Availability Overview")` under the same board-type/auth combinations, alongside the existing (unchanged) `"Calendar"` assertions — per D-22's verified note that string assertions stay green through the dropdown restructuring.

## No Analog Found

None — every file in scope has a close existing analog in this codebase; the phase is composition over Phases 74-76 infrastructure, not new invention (see RESEARCH.md "Key insight").

## Metadata

**Analog search scope:** `QuestBoard.Repository/`, `QuestBoard.Domain/`, `QuestBoard.Service/Controllers`, `QuestBoard.Service/Views`, `QuestBoard.Service/Automapper`, `QuestBoard.IntegrationTests/`
**Files scanned:** `EventSignupRepository.cs`, `EventRepository.cs`, `IEventRepository.cs`, `QuestBoardContext.cs`, `EventsController.cs`, `CalendarController.cs`, `EventSignup.cs`, `Event.cs`, `EntityProfile.cs`, `ViewModelProfile.cs`, `Quest/Manage.cshtml`, `Calendar/Index.cshtml`, `_Layout.cshtml`, `_Layout.Mobile.cshtml`, `EventAvailabilityTenantIsolationTests.cs`, `LayoutNavigationTests.cs`
**Pattern extraction date:** 2026-08-29
