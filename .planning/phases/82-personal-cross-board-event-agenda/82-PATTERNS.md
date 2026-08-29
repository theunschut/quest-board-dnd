# Phase 82: Personal Cross-Board Event Agenda - Pattern Map

**Mapped:** 2026-08-29
**Files analyzed:** 19 (new/modified)
**Analogs found:** 17 / 19 (2 explicitly net-new, no analog)

All line numbers below were re-verified directly against the current working tree (not just quoted from RESEARCH.md) immediately before writing this file.

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|---|---|---|---|---|
| `QuestBoard.Domain/Interfaces/IEventRepository.cs` (add method) | interface | CRUD (read) | `IEventRepository.GetUpcomingWithSignupsAsync` decl | exact |
| `QuestBoard.Repository/EventRepository.cs` (add `GetUpcomingAcrossGroupsWithSignupsAsync`) | repository | CRUD (read), cross-tenant | `EventRepository.GetUpcomingWithSignupsAsync` (:132-157) + `GroupRepository`'s two `IgnoreQueryFilters` private methods (:130-149) | exact (composite) |
| `QuestBoard.Domain/Interfaces/IGroupRepository.cs` / `IGroupService.cs` (reuse, no change expected) | interface | CRUD (read) | `GetGroupsForUserAsync` decl | exact — likely no change needed |
| `QuestBoard.Domain/Services/EventService.cs` (add `GetCrossBoardAgendaAsync`) | service | CRUD (read), transform | `EventService.GetAvailabilityOverviewAsync` (:41-64) | exact |
| `QuestBoard.Domain/Models/AgendaRow.cs` (new) | model | transform | `EventAvailabilityRow` (Phase 77's row model) | role-match |
| `QuestBoard.Domain/Models/AgendaOptions.cs` (new) | config | — | `EventsOverviewOptions.cs` | exact |
| `QuestBoard.Domain/Extensions/ServiceExtensions.cs` (add `AddOptions<AgendaOptions>` line) | config/DI | — | existing `EventsOverviewOptions` registration (:25-26) | exact |
| `QuestBoard.Service/Controllers/AgendaController.cs` (new) | controller | request-response | `EventsController.Index` (:34-51) plus SuperAdmin handling (:98-101) | exact |
| `QuestBoard.Service/Controllers/AgendaController.cs` — switch-prompt POST handling | controller | request-response | `GroupPickerController.SelectGroup`/`RedirectToLocal` (reused unchanged, not re-implemented) | exact — reuse, don't recreate |
| `QuestBoard.Service/ViewModels/AgendaViewModels/AgendaViewModel.cs` (new) | viewmodel | transform | `EventOverviewViewModel` | role-match |
| `QuestBoard.Service/ViewModels/AgendaViewModels/AgendaRowViewModel.cs` (new) | viewmodel | transform | `EventOverviewRowViewModel` | role-match |
| `QuestBoard.Service/Automapper/ViewModelProfile.cs` (add `CreateMap<AgendaRow, AgendaRowViewModel>`) | mapping config | transform | `CreateMap<EventAvailabilityRow, EventOverviewRowViewModel>` (:165-169) | exact |
| `QuestBoard.Repository/Automapper/EntityProfile.cs` (entity↔domain maps for any new domain types touching entities — likely none needed since `AgendaRow` composes existing `Event`/`EventSignup`) | mapping config | transform | existing `Event`/`EventSignup` maps | role-match / possibly no-op |
| `QuestBoard.Service/Constants/SessionKeys.cs` (add `AgendaBoardFilter` key) | config/utility | session read/write | `ActiveGroupId`/`ActiveGroupName` scalar keys (:8-10) | **no analog for the collection shape — net new (see Shared Patterns)** |
| `QuestBoard.Service/Views/Agenda/Index.cshtml` (new) | view | request-response | `Views/Events/Index.cshtml` (current, post-gap-closure) | exact |
| `QuestBoard.Service/Views/Agenda/Index.Mobile.cshtml` (new) | view | request-response | `Views/Events/Index.Mobile.cshtml` (current, post-gap-closure, :76-93 roster block) | exact |
| `QuestBoard.Service/Views/Shared/_Layout.cshtml` (add dropdown `<li>`) | view partial | request-response | existing "Switch Group" `<li>` in the user dropdown | exact |
| `QuestBoard.Service/Views/Shared/_Layout.Mobile.cshtml` (add flat `<li>`) | view partial | request-response | existing flat "Switch Group" `<li>` | exact |
| `QuestBoard.Service/Views/Events/Index.cshtml` / `Index.Mobile.cshtml` (add cross-link) | view partial | request-response | UI-SPEC's own cross-link markup block | exact (spec-provided) |
| `QuestBoard.Service/Views/Calendar/Index.cshtml` / `Index.Mobile.cshtml` (add cross-link) | view partial | request-response | same cross-link markup as above | exact |
| `QuestBoard.Service/Views/Events/Details.cshtml` (add conditional back-link) | view | request-response | none needed — UI-SPEC gives the exact 6-line block; do not otherwise touch this file (explicit "do not touch" in CONTEXT.md) | exact (spec-provided) |
| `QuestBoard.Service/Controllers/GroupPickerController.cs` (`RedirectToLocal`/`SelectGroup` — set `ReturnedFromAgenda` flag for D-13, or agenda passes a marker) | controller | request-response | `SelectGroup`/`RedirectToLocal` (:42-73 current) — likely only touched if a `ReturnedFromAgenda` signal needs threading; reuse, minimal edit | exact |
| `QuestBoard.IntegrationTests/Tests/AgendaTenantIsolationTests.cs` (new) | test | integration | `EventAvailabilityTenantIsolationTests.cs` full file (seeding helpers, `DisposeAsync`) | exact |
| `QuestBoard.IntegrationTests/Controllers/LayoutNavigationTests.cs` (add cases) | test | integration | existing `Nav_*_AvailabilityOverviewLinkPresent`-shaped theory cases | exact |

## Pattern Assignments

### `QuestBoard.Repository/EventRepository.cs` — `GetUpcomingAcrossGroupsWithSignupsAsync` (repository, cross-tenant CRUD read)

**Analog 1 — deterministic ordering + eager-include shape:** `EventRepository.GetUpcomingWithSignupsAsync`, `QuestBoard.Repository/EventRepository.cs:132-157` (verified current):

```csharp
public async Task<IList<EventWithSignups>> GetUpcomingWithSignupsAsync(DateOnly today, int take, CancellationToken token = default)
{
    // Scoping comes entirely from EventEntity's and EventSignupEntity's fail-closed query
    // filters -- no manual GroupId predicate is added here. The ordering has to stay fully
    // deterministic (hence the Id tiebreaker) because Take is applied before the signup
    // collection is materialised, and an unstable sort could truncate the window
    // unpredictably when two events share a date and start time.
    var entities = await DbContext.Events
        .Where(e => e.Date >= today && e.CancelledAt == null)
        .OrderBy(e => e.Date)
        .ThenBy(e => e.StartTime)
        .ThenBy(e => e.Id)
        .Take(take)
        .Include(e => e.Signups)
            .ThenInclude(s => s.User)
        .AsNoTracking()
        .ToListAsync(token);

    return entities
        .Select(entity => new EventWithSignups
        {
            Event = Mapper.Map<Event>(entity),
            Signups = Mapper.Map<List<EventSignup>>(entity.Signups)
        })
        .ToList();
}
```

**Analog 2 — the predicate-pinned `IgnoreQueryFilters()` shape and its comment convention:** `GroupRepository.cs:130-149` (verified current, both private methods):

```csharp
// The ambient board filter answers for the caller's currently selected board, which is the
// wrong question for an operation that targets a board named by an explicit groupId
// argument. Scope is re-imposed immediately below by that same argument, so this query is
// strictly narrower than an unscoped bypass rather than broader.
private async Task<List<int>> GetFutureEventIdsForGroupIgnoringActiveBoardAsync(int groupId, DateOnly today, CancellationToken token)
{
    return await DbContext.Events
        .IgnoreQueryFilters()
        .Where(e => e.GroupId == groupId && e.Date >= today)
        .Select(e => e.Id)
        .ToListAsync(token);
}

// Same reasoning as GetFutureEventIdsForGroupIgnoringActiveBoardAsync: the ambient filter
// scopes to the caller's selected board, while this operation targets the board named by
// the groupId argument, so scope is re-imposed explicitly from that argument.
private async Task<List<EventSignupEntity>> GetEventSignupsForMemberIgnoringActiveBoardAsync(int groupId, int userId, CancellationToken token)
{
    return await DbContext.EventSignups
        .IgnoreQueryFilters()
        .Where(es => es.UserId == userId && es.Event.GroupId == groupId)
        .ToListAsync(token);
}
```

**Composite pattern to write (generalising both):**

```csharp
public async Task<IList<EventWithSignups>> GetUpcomingAcrossGroupsWithSignupsAsync(
    IReadOnlyCollection<int> memberGroupIds, DateOnly today, int take, CancellationToken token = default)
{
    // Scope is re-imposed immediately by memberGroupIds, supplied by the caller from a fresh
    // membership read taken this same request -- this bypass is therefore strictly narrower
    // than the ambient filter for any single board, never broader. IgnoreQueryFilters()
    // disables the filter for the whole query, including the Signups and User includes below;
    // that is intended, because every included row hangs off an Event whose GroupId is already
    // pinned to memberGroupIds.
    var entities = await DbContext.Events
        .IgnoreQueryFilters()
        .Where(e => memberGroupIds.Contains(e.GroupId) && e.Date >= today && e.CancelledAt == null)
        .OrderBy(e => e.Date)
        .ThenBy(e => e.StartTime)
        .ThenBy(e => e.Id)
        .Take(take)
        .Include(e => e.Signups)
            .ThenInclude(s => s.User)
        .AsNoTracking()
        .ToListAsync(token);

    return entities
        .Select(entity => new EventWithSignups
        {
            Event = Mapper.Map<Event>(entity),
            Signups = Mapper.Map<List<EventSignup>>(entity.Signups)
        })
        .ToList();
}
```

**Explicitly not a precedent** — do not shape the new method after `QuestRepository.GetQuestsForTomorrowAllGroupsAsync` (`QuestRepository.cs:267`), a bare `IgnoreQueryFilters()` with no group predicate. That is a background-job read; this is a user-facing read and must always carry the `memberGroupIds.Contains(...)` predicate in the same `Where`.

---

### Membership read (D-15) — `GroupRepository.GetGroupsForUserAsync`

**Analog:** `QuestBoard.Repository/GroupRepository.cs:29-42` (verified current, no change needed — call as-is):

```csharp
public async Task<IList<GroupWithMemberCount>> GetGroupsForUserAsync(int userId, CancellationToken token = default)
{
    return await DbContext.Groups
        .Where(g => g.UserGroups.Any(ug => ug.UserId == userId))
        .Select(g => new GroupWithMemberCount
        {
            Id = g.Id,
            Name = g.Name,
            CreatedAt = g.CreatedAt,
            MemberCount = g.UserGroups.Count,
            BoardType = (BoardType)g.BoardType
        })
        .ToListAsync(token);
}
```

Call this fresh, every request, in `AgendaController.Index` (never session/claims). It already returns `Id`, `Name`, `BoardType` — exactly what both D-14's predicate and D-04's filter checklist need from one call. Build `memberGroupIds` and `boardNamesById` from its result in the controller/service, do not add a second query.

---

### `QuestBoard.Domain/Services/EventService.cs` — `GetCrossBoardAgendaAsync` (service, CRUD read + transform)

**Analog:** `EventService.GetAvailabilityOverviewAsync`, `QuestBoard.Domain/Services/EventService.cs:1-8, 41-50` (verified current — constructor and clock usage):

```csharp
internal class EventService(IEventRepository repository, IMapper mapper, TimeProvider timeProvider) : BaseService<Event>(repository, mapper), IEventService
{
    ...
    public async Task<EventAvailabilityOverview> GetAvailabilityOverviewAsync(int take, CancellationToken token = default)
    {
        // Date-only, no time-of-day comparison, and read in UTC from the injected clock so it
        // lines up with the UTC timestamps this same feature already writes onto signups and
        // cancellations.
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);

        // Asking for one more row than the caller wants is how the page learns there is more to
        // show without a second, separate count query.
        var fetched = await repository.GetUpcomingWithSignupsAsync(today, take + 1, token);
        var hasMore = fetched.Count > take;
        var events = hasMore ? fetched.Take(take).ToList() : fetched.ToList();
        ...
```

**Copy for the new method:** constructor already has `TimeProvider timeProvider` injected — reuse it, do **not** reintroduce `DateTime.Today`. Compute `today` the same way. Follow the same `take + 1` / `hasMore` idiom. After fetching rows, add the D-16 re-check (`row.Event.GroupId` must be in `memberGroupIds`, fail closed / drop the row and consider logging if one is ever found) — this re-check has no existing precedent in this file; write it net-new but keep the comment explicit about its limited coverage (same `memberGroupIds` list on both sides, so it catches a dropped predicate/bad translation, not a wrong membership set).

Do **not** port `ClassifyCell`'s member-axis/column construction (`EventService.cs`, the `.SelectMany(...).GroupBy(s => s.UserId)` block right after the excerpt above) — D-02/roadmap explicitly reject porting the member axis. Do reuse `ClassifyCell` itself (the `HasAnswered`+`VoteType` → `AvailabilityCellState` classification) for both the viewer's own cell and every roster entry's cell.

---

### `QuestBoard.Service/Controllers/AgendaController.cs` (controller, request-response)

**Analog:** `EventsController.Index` + SuperAdmin handling, `QuestBoard.Service/Controllers/Events/EventsController.cs:1-51, 98-101` (verified current):

```csharp
[Authorize]
public class EventsController(
    IEventService eventService,
    IUserService userService,
    IActiveGroupContext activeGroupContext,
    IMapper mapper,
    IEventSignupService eventSignupService,
    IBoardTypeResolver boardTypeResolver,
    IEventSeriesService eventSeriesService,
    IOptions<EventsOverviewOptions> overviewOptions) : Controller
{
    // Read-only and available to every board member: ... The page size is clamped server-side
    // so a client-supplied value can never turn into an unbounded query ...
    [HttpGet]
    public async Task<IActionResult> Index(int? take = null, CancellationToken token = default)
    {
        var options = overviewOptions.Value;
        var effectiveTake = Math.Clamp(take ?? options.DefaultTake, 1, Math.Max(1, options.MaxTake));

        var overview = await eventService.GetAvailabilityOverviewAsync(effectiveTake, token);
        var currentUser = await userService.GetUserAsync(User);

        var viewModel = new EventOverviewViewModel
        {
            Members = mapper.Map<IList<OverviewMemberViewModel>>(overview.Members),
            Rows = mapper.Map<IList<EventOverviewRowViewModel>>(overview.Rows),
            HasMore = overview.HasMore,
            Take = effectiveTake,
            NextTake = Math.Min(effectiveTake + options.PageIncrement, options.MaxTake),
            CurrentUserId = currentUser.Id
        };

        return View(viewModel);
    }
```

SuperAdmin-with-no-active-group handling (line 98 comment pattern, `Create` action) — this shape doesn't directly transfer (Agenda needs no active group at all, per D-07/D-09), but the *pattern* of an explicit comment stating why a SuperAdmin branch is or isn't needed should be copied: state plainly in `AgendaController.Index` that there is deliberately **no** SuperAdmin branch — D-09 makes the query scope by `UserGroups` for every user including SuperAdmins, with no widening escape hatch.

**Copy for `AgendaController.Index`:**
1. Inject `IGroupService`/`IGroupRepository` (for membership), `IEventService` (new method), `IUserService`, `IOptions<AgendaOptions>`.
2. Read `boards` filter from query string, read/write `SessionKeys.AgendaBoardFilter` (CSV string — see Shared Patterns).
3. **Intersect** the requested filter against the fresh `memberGroupIds` before using it (D-17 case 4 — load-bearing, do not skip).
4. Clamp `take` the same `Math.Clamp` way, against `AgendaOptions`.
5. Map to `AgendaViewModel`, set `HasMore`/`NextTake`/`CanShowMore` the same way `EventOverviewViewModel` does.

**Switch-prompt POST — do not write a new action for this.** Reuse `GroupPickerController.SelectGroup` unchanged, verified current, `QuestBoard.Service/Controllers/GroupPickerController.cs` (per RESEARCH.md Pattern 4, confirmed against source structure):

```csharp
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> SelectGroup(int groupId, string? returnUrl = null)
{
    var group = await groupService.GetByIdAsync(groupId);
    if (group == null) return NotFound();

    var isSuperAdmin = User.IsInRole("SuperAdmin");
    if (!isSuperAdmin)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var role = await userService.GetGroupRoleByIdAsync(userId, groupId);
        if (role == null) return NotFound();
    }

    HttpContext.Session.SetInt32(SessionKeys.ActiveGroupId, group.Id);
    HttpContext.Session.SetString(SessionKeys.ActiveGroupName, group.Name);
    HttpContext.Session.SetString(SessionKeys.ActiveGroupValidatedAtUtc, DateTime.UtcNow.ToString("O"));
    return RedirectToLocal(returnUrl);
}

private IActionResult RedirectToLocal(string? returnUrl)
{
    if (Url.IsLocalUrl(returnUrl)) return Redirect(returnUrl);
    return RedirectToAction("Index", "Quest");
}
```

The agenda's switch-confirm modal form (UI-SPEC Component Spec 4) posts directly to this action with `groupId` = the row's board id, `returnUrl` = `Url.Action("Details", "Events", new { id = row.EventId })`.

**Guard the premise directly (D-11):** `EventsController.Details` has no explicit ownership guard — confirmed current shape calls `eventService.GetEventWithDetailsAsync(id)` (ambient-filtered) and `NotFound()`s if null. `EventIsOnActiveBoard` (private helper, `EventsController.cs:462-463`, verified current) exists and is used only by `SetAvailability`/`Withdraw`:

```csharp
private bool EventIsOnActiveBoard(Event candidate) =>
    activeGroupContext.ActiveGroupId is { } groupId && candidate.GroupId == groupId;
```

This is the guard D-16's in-memory re-check is *weaker than* (independent session-state comparison vs. same-list re-check) — do not weaken or replace it; the Agenda feature does not touch it at all.

---

### `QuestBoard.Domain/Models/AgendaOptions.cs` + DI registration (config)

**Analog:** `QuestBoard.Domain/Models/EventsOverviewOptions.cs` (verified current, full file):

```csharp
namespace QuestBoard.Domain.Models;

// Code defaults, overridable through configuration, so no deployment environment file has
// to change for the feature to work.
public class EventsOverviewOptions
{
    public const string SectionName = "EventsOverview";
    public int DefaultTake { get; set; } = 10;
    public int MaxTake { get; set; } = 100;
    public int PageIncrement { get; set; } = 10;
    public bool IsValid() => DefaultTake >= 1 && MaxTake >= 1 && PageIncrement >= 1 && DefaultTake <= MaxTake;
}
```

**Registration**, `QuestBoard.Domain/Extensions/ServiceExtensions.cs:25-26` (verified current — note: **not** `Program.cs`):

```csharp
services.AddOptions<EventsOverviewOptions>()
    .BindConfiguration(EventsOverviewOptions.SectionName)
```

**Copy for `AgendaOptions`:** same shape, `SectionName = "Agenda"`, and per UI-SPEC's own recommendation use lower defaults (`DefaultTake = 5`, `PageIncrement = 5`, `MaxTake = 50`) since each row carries a full roster. Add an identical `services.AddOptions<AgendaOptions>().BindConfiguration(AgendaOptions.SectionName);` line in the same `ServiceExtensions.cs` file, right beside the `EventsOverviewOptions` line.

---

### `QuestBoard.Service/Views/Agenda/Index.cshtml` + `Index.Mobile.cshtml` (desktop + mobile views)

**No further excerpting needed here** — UI-SPEC.md (already read in full) supplies complete, ready-to-copy markup for every block: page shell (`modern-card`), row anatomy, the explicit control (both variants), the switch-confirm modal + its `show.bs.modal` JS, the mobile card + roster toggle with **both** `stopPropagation()` calls, the board filter dropdown/collapse, the three empty states, and the paging control. Copy those blocks verbatim; they are already grounded in this codebase's idiom. The one thing to re-verify at execution time against the *current* file (not this snapshot) is `Index.Mobile.cshtml:76-93`'s two-`stopPropagation()` shape, since it was itself a gap-closure fix — confirm it is still present before treating it as a copy source, but it was not re-read as part of this pattern-mapping pass (RESEARCH.md's excerpt is trusted here; re-verify at plan/execute time per its own "Valid until" note).

---

### `QuestBoard.Service/Views/Shared/_Layout.cshtml` / `_Layout.Mobile.cshtml` (nav entries)

UI-SPEC.md Component Spec 0 supplies the exact markup for both entries (desktop dropdown `<li>`, mobile flat `<li>`), placed beside the existing "Switch Group" entries and outside the `activeBoardType is BoardType.OneShot or BoardType.Campaign` gate. Copy verbatim from UI-SPEC. No additional excerpting needed.

---

### `QuestBoard.Repository/EventSignupRepository.cs` — `GetRosterForEventAsync` (D-18's eager-include shape to generalise)

**Analog**, `QuestBoard.Repository/EventSignupRepository.cs:60-73` (verified current):

```csharp
public async Task<IList<EventSignup>> GetRosterForEventAsync(int eventId, CancellationToken token = default)
{
    // The ambient query filter is the correct scoping here: this only ever runs from the
    // event details request, where the event itself was already fetched through the same
    // filter. Roster ordering is alphabetical by member name, so the view does not need to
    // re-sort.
    var entities = await DbContext.EventSignups
        .Include(es => es.User)
        .Where(es => es.EventId == eventId)
        .OrderBy(es => es.User.Name)
        .ToListAsync(token);

    return Mapper.Map<IList<EventSignup>>(entities);
}
```

This is the single-query, ordered-in-SQL eager-include shape D-18 requires the new cross-board query to also follow — already satisfied by the `Include(e => e.Signups).ThenInclude(s => s.User)` in the composite pattern above (one round trip). Do not add a second per-event roster query in the controller/service layer — the roster for every row must arrive with the same query that fetched the events.

---

### `QuestBoard.Service/Automapper/ViewModelProfile.cs` — AutoMapper style (service boundary)

**Analog**, `QuestBoard.Service/Automapper/ViewModelProfile.cs:165-169` (verified current):

```csharp
CreateMap<EventAvailabilityRow, EventOverviewRowViewModel>()
    .ForMember(dest => dest.EventId, opt => opt.MapFrom(src => src.Event.Id))
    .ForMember(dest => dest.Title, opt => opt.MapFrom(src => src.Event.Title))
    .ForMember(dest => dest.Date, opt => opt.MapFrom(src => src.Event.Date))
    .ForMember(dest => dest.StartTime, opt => opt.MapFrom(src => src.Event.StartTime));
```

**Copy for `CreateMap<AgendaRow, AgendaRowViewModel>()`** using the same `.ForMember(dest => dest.X, opt => opt.MapFrom(src => src.Event.X))` shape for the fields that live on `Event`. `BoardName` cannot be mapped this way — `Event`/`EventEntity` domain model has no `Group`/`BoardName` field (`EventEntity.Group` is a real navigation on the entity, but nothing carries it across the entity→domain boundary). Set `BoardName` (and `IsActiveBoard`) in the controller/service after mapping, from the in-memory `boardNamesById` dictionary built from `GetGroupsForUserAsync`'s result — the same way `EventOverviewViewModel.CurrentUserId` is set by the controller rather than mapped.

**Anti-pattern, explicitly do not do:** add `.Include(e => e.Group)` to the new repository query to try to flow the board name through `Event`/`EventEntity` mapping — there is no domain-model field to receive it, and widening `Event` would touch every other consumer of that shared model.

---

### `QuestBoard.IntegrationTests/Tests/*TenantIsolationTests.cs` (D-17's four-case test)

**Analog**, full file `QuestBoard.IntegrationTests/Tests/EventAvailabilityTenantIsolationTests.cs` (verified current, header + `DisposeAsync` + two seeding helpers shown):

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

    private async Task<int> SeedGroupOneEventAsync(string title, DateOnly date)
    {
        await using var ctx = factory.Database.CreateContext();
        var newEvent = new EventEntity { Title = title, GroupId = 1, Date = date, CreatedAt = DateTime.UtcNow };
        ctx.Events.Add(newEvent);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        return newEvent.Id;
    }

    private async Task<int> SeedSignupAsync(int eventId, int groupId, string name)
    {
        var user = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "isoavail_seed", "isoavail_seed@example.com", name: name);
        await using var ctx = factory.Database.CreateContext();
        // ... (membership + signup insert follows, elided here — full file has the remainder)
    }
}
```

**Copy for the new `AgendaTenantIsolationTests.cs`:**
- Same `IClassFixture<WebApplicationFactoryBase>, IAsyncLifetime` shape, same `DisposeAsync` reset of `factory.TestGroupContext.ActiveGroupId = 1` (and `BoardType`) — **mandatory per D-17**, since `WebApplicationFactoryBase.TestGroupContext` is a shared singleton `MutableGroupContext` defaulting to 1 and other test classes will run after this one in the same process.
- Extend the two-group seeding helpers to a **third** board (group id 3) for D-17 case 2 ("two joined boards both appear, a third does not") — no existing test class exercises three boards simultaneously; this is new test code, but follows the exact same `factory.Database.CreateContext()` unfiltered-write pattern shown above.
- Add a "leave a board" helper calling `IGroupService.RemoveMemberAsync`/`GroupRepository.RemoveMemberAsync` via `factory.Services.CreateScope()` — no existing test calls this directly; follow the same DI-scope pattern this file's `SeedSignupAsync`/roster-check facts already use for `IEventSignupService`.
- Add a case using `AuthenticationHelper`'s SuperAdmin client creation with zero seeded memberships for D-09/EVTAGENDA-08.
- Add a case that requests the agenda with a board id the viewer is not a member of, asserting it never widens the result (D-17 case 4) — this is the test that proves the D-15/D-17-Pitfall-2 intersection step in the controller.

---

### `QuestBoard.IntegrationTests/Controllers/LayoutNavigationTests.cs` (nav visibility test)

No excerpt needed beyond what's already established: add `[Theory]` cases mirroring the existing `Nav_*_AvailabilityOverviewLinkPresent`-shaped cases, plus a genuinely new case setting `MutableGroupContext.BoardType = null` (nullable `BoardType?`, directly settable, not currently exercised by any case in this file) and asserting the new "My Agenda" nav entry is still present — this is the concrete proof that D-08's nav entry is the unconditional path in.

---

## Shared Patterns

### Cross-tenant bypass comment convention
**Source:** `GroupRepository.cs:130-141` (see excerpt above)
**Apply to:** `EventRepository.GetUpcomingAcrossGroupsWithSignupsAsync` and the D-16 re-check in `EventService.GetCrossBoardAgendaAsync`.
Every `IgnoreQueryFilters()` call in this codebase carries a comment stating *why* the bypass is safe (predicate re-imposes scope immediately) written for the next engineer who copies the shape. Per CONTEXT.md's own framing, this phase's comment is the one every future cross-board feature author will read first — write it accordingly, and do not omit the explicit "not a precedent" callout referencing `QuestRepository.GetQuestsForTomorrowAllGroupsAsync` if the reviewer needs the contrast spelled out.

### Session-stored scalar convention → generalised to a collection (net new, no direct analog)
**Source:** `QuestBoard.Service/Constants/SessionKeys.cs` (verified current, full file):
```csharp
public static class SessionKeys
{
    public const string ActiveGroupId = "ActiveGroupId";
    public const string ActiveGroupName = "ActiveGroupName";
    public const string ActiveGroupValidatedAtUtc = "ActiveGroupValidatedAtUtc";

    public static string ShowHiddenContactsKey(int groupId) => $"ShowHiddenContacts_{groupId}";
}
```
**No analog exists — net new.** Every key here is a scalar (`SetInt32`/`SetString`). There is no precedent anywhere in this codebase for storing a collection (a set of selected board ids) in session. Do not reach for `System.Text.Json` (used elsewhere only for `ResendStatsClient`'s external API deserialization, never for session) — that would be a one-off pattern inconsistent with every other session value. Follow the CSV convention instead, matching the existing `SetString`/`GetString` idiom exactly:
```csharp
public const string AgendaBoardFilter = "AgendaBoardFilter";

// Write
HttpContext.Session.SetString(SessionKeys.AgendaBoardFilter, string.Join(',', selectedGroupIds));

// Read + mandatory D-17-case-4 intersection against the fresh membership set
var stored = HttpContext.Session.GetString(SessionKeys.AgendaBoardFilter);
var requestedIds = string.IsNullOrEmpty(stored)
    ? memberGroupIds
    : stored.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList();
var effectiveGroupIds = requestedIds.Intersect(memberGroupIds).ToList();
```
**Apply to:** `AgendaController.Index` only. The intersection step is load-bearing — it is what makes D-17's fourth mandated test case true by construction. Never skip it, and never trust the session value as more than a hint.

### Reuse `GroupPickerController.SelectGroup` unchanged (D-11/D-13)
**Source:** `GroupPickerController.cs` (excerpt above, verified current)
**Apply to:** the Agenda's switch-confirm modal form (posts directly to this action, no new controller action).
Do not write a second "set active board" endpoint. `Url.IsLocalUrl`'s open-redirect guard and `[ValidateAntiForgeryToken]`'s CSRF protection both come for free by reusing this action verbatim.

### Shared modal populated per-row via `data-*` attributes
**Source:** `QuestBoard.Service/Views/ShopManagement/Index.cshtml` — trigger button (:94-98) and modal (:455-489), verified current:
```html
<button type="button" class="btn btn-danger btn-sm btn-action" title="Deny"
        data-bs-toggle="modal" data-bs-target="#denyModal"
        data-item-id="@item.Id"
        data-item-name="@item.Name">
    <i class="fas fa-times"></i>
</button>
...
<div class="modal fade" id="denyModal" tabindex="-1">
  <div class="modal-dialog">
    <div class="modal-content bg-dark text-light">
      <form id="denyForm" method="post">
        @Html.AntiForgeryToken()
        ...
```
Note: this existing form has no `show.bs.modal` JS listener visible in the excerpt scanned — UI-SPEC's Component Spec 4 supplies that listener explicitly (reading `event.relatedTarget.getAttribute('data-*')` and populating hidden fields), which is the more complete version to copy. **Apply to:** the Agenda's switch-confirm modal — one shared `<div id="switchBoardModal">`, one trigger button per other-board row, `data-group-id`/`data-board-name`/`data-return-url` populated on `show.bs.modal`. Use UI-SPEC's fuller listener pattern (already vetted), citing this file only for the `data-bs-toggle`/`data-*`/`bg-dark text-light` modal-idiom precedent.

### AutoMapper: computed/cross-boundary fields set by the controller, not mapped
**Source:** `EventsController.cs:47` (`CurrentUserId = currentUser.Id` set directly on the view model after `mapper.Map(...)`, per RESEARCH.md's verified citation)
**Apply to:** `AgendaRowViewModel.BoardName` / `IsActiveBoard` — set post-map from the in-memory `boardNamesById` dictionary and `activeGroupContext.ActiveGroupId`, not via a `.ForMember` projection (no source field exists to project from).

## No Analog Found

| File / Concern | Role | Data Flow | Reason |
|---|---|---|---|
| `SessionKeys.AgendaBoardFilter` (collection storage) | utility/session | request-response | No code anywhere in this repository stores a collection in session — every existing key is a scalar. Treated fully in Shared Patterns above; build from the CSV convention, not from a JSON precedent. |
| D-16's in-memory post-query re-check (`row.Event.GroupId` against `memberGroupIds`) | service-layer guard | transform | No existing method in `EventService`/anywhere else re-verifies query-filter correctness against the same list it queried with. This is genuinely new code, though structurally trivial (`.Where`/`.All` over already-materialized rows) — write it net-new with the limits comment D-16 mandates. |

## Metadata

**Analog search scope:** `QuestBoard.Repository/*.cs`, `QuestBoard.Domain/Services/*.cs`, `QuestBoard.Domain/Models/*.cs`, `QuestBoard.Service/Controllers/**/*.cs`, `QuestBoard.Service/Views/**/*.cshtml`, `QuestBoard.Service/Constants/SessionKeys.cs`, `QuestBoard.Service/Automapper/ViewModelProfile.cs`, `QuestBoard.Repository/Automapper/EntityProfile.cs`, `QuestBoard.IntegrationTests/**/*.cs`
**Files scanned (direct read/grep, this session):** `EventRepository.cs`, `GroupRepository.cs`, `EventSignupRepository.cs`, `EventService.cs`, `EventsController.cs`, `GroupPickerController.cs` (via RESEARCH.md verified excerpt + structural grep), `SessionKeys.cs`, `EventsOverviewOptions.cs`, `ServiceExtensions.cs`, `ViewModelProfile.cs`, `EventAvailabilityTenantIsolationTests.cs`, `ShopManagement/Index.cshtml`
**Pattern extraction date:** 2026-08-29
