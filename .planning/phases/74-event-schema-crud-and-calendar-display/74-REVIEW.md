---
phase: 74-event-schema-crud-and-calendar-display
reviewed: 2026-08-27T06:42:08Z
depth: standard
files_reviewed: 38
files_reviewed_list:
  - QuestBoard.Repository/Entities/EventEntity.cs
  - QuestBoard.Repository/Entities/EventSeriesEntity.cs
  - QuestBoard.Repository/Entities/EventSignupEntity.cs
  - QuestBoard.Repository/Entities/QuestBoardContext.cs
  - QuestBoard.Repository/EventRepository.cs
  - QuestBoard.Repository/Automapper/EntityProfile.cs
  - QuestBoard.Repository/Extensions/ServiceExtensions.cs
  - QuestBoard.Repository/Migrations/20260826134133_AddCalendarEventsFeature.cs
  - QuestBoard.Repository/Migrations/20260826134133_AddCalendarEventsFeature.Designer.cs
  - QuestBoard.Repository/Migrations/QuestBoardContextModelSnapshot.cs
  - QuestBoard.Domain/Models/Event.cs
  - QuestBoard.Domain/Interfaces/IEventRepository.cs
  - QuestBoard.Domain/Interfaces/IEventService.cs
  - QuestBoard.Domain/Services/EventService.cs
  - QuestBoard.Domain/Extensions/ServiceExtensions.cs
  - QuestBoard.Service/Controllers/Events/EventsController.cs
  - QuestBoard.Service/Controllers/QuestBoard/CalendarController.cs
  - QuestBoard.Service/ViewModels/EventViewModels/EventViewModel.cs
  - QuestBoard.Service/ViewModels/CalendarViewModels/EventOnDay.cs
  - QuestBoard.Service/ViewModels/CalendarViewModels/CalendarDay.cs
  - QuestBoard.Service/ViewModels/CalendarViewModels/CalendarViewModel.cs
  - QuestBoard.Service/Automapper/ViewModelProfile.cs
  - QuestBoard.Service/Views/Events/Create.cshtml
  - QuestBoard.Service/Views/Events/Edit.cshtml
  - QuestBoard.Service/Views/Events/Details.cshtml
  - QuestBoard.Service/Views/Shared/_Calendar.cshtml
  - QuestBoard.Service/Views/Shared/_Layout.cshtml
  - QuestBoard.Service/Views/Shared/_Layout.Mobile.cshtml
  - QuestBoard.Service/Views/Calendar/Index.cshtml
  - QuestBoard.Service/Views/Calendar/Index.Mobile.cshtml
  - QuestBoard.Service/wwwroot/css/calendar.css
  - QuestBoard.Service/wwwroot/css/calendar.mobile.css
  - QuestBoard.IntegrationTests/Tests/EventTenantIsolationTests.cs
  - QuestBoard.IntegrationTests/Controllers/EventsControllerIntegrationTests.cs
  - QuestBoard.IntegrationTests/Controllers/EventCalendarPartialTests.cs
  - QuestBoard.IntegrationTests/Controllers/CalendarControllerIntegrationTests.cs
  - QuestBoard.IntegrationTests/Controllers/LayoutNavigationTests.cs
  - QuestBoard.IntegrationTests/Mobile/MobileViewsTests.cs
findings:
  critical: 1
  warning: 2
  info: 1
  total: 4
status: resolved
---

# Phase 74: Code Review Report

**Reviewed:** 2026-08-27T06:42:08Z
**Depth:** standard
**Files Reviewed:** 38
**Status:** resolved (CR-01 severity corrected to defense-in-depth; all findings addressed)

## Summary

This phase adds three EF entities (`EventEntity`, `EventSeriesEntity`, `EventSignupEntity`), a
domain/repository/service layer, a DM-gated CRUD controller, three Razor views, desktop/mobile
calendar rendering, and a tenant-isolation test suite for board-level calendar events.

The tenant-isolation work is genuinely solid: every `HasQueryFilter` in `QuestBoardContext.cs`
follows the existing fail-closed pattern (`ActiveGroupId != null && ...`), `EventRepository`
never calls `IgnoreQueryFilters`, the write path stamps `GroupId` from
`activeGroupContext.RequireActiveGroupId()` rather than trusting the posted form, AutoMapper
explicitly `.Ignore()`s `GroupId`/`SeriesId`/`SeriesSlotIndex`/`CreatedAt` on the
`EventViewModel → Event` map (closing off a mass-assignment path even if a client posts those
field names), and the cross-board schedule-reference check in `EventsController.Edit` runs
before the entity mutation and before `UpdateAsync` is called. XSS surface is clean — every
event field is rendered through standard Razor interpolation or the shared sanitized
`Html.Markdown()` helper; there is no `Html.Raw` anywhere in the reviewed views. The integration
test suite for tenant isolation (`EventTenantIsolationTests.cs`) is unusually rigorous: every
negative assertion ("board 2's event is invisible") is paired with a positive assertion that
proves the page actually rendered real content, which is exactly the discipline needed to avoid
a vacuously-passing isolation test.

The one confirmed defect is a crash, not a leak: `EventsController.Create` (POST) calls
`activeGroupContext.RequireActiveGroupId()` unconditionally, but the `DungeonMasterOnly` policy
that gates this action lets `SuperAdmin` through via an explicit bypass in `DungeonMasterHandler`,
and `SuperAdmin` has `ActiveGroupId == null` by design. The method's own doc comment says not to
call it on a SuperAdmin-reachable path; this call site does exactly that. `Edit`/`Delete`/
`Details` are not at risk of the same crash because they all resolve the event through the
fail-closed query filter first, which already returns `NotFound` for `SuperAdmin` before any
`RequireActiveGroupId()` call is reached — but `Create` has no such guard in front of it.

Two of the reviewed test files also carry stale scaffold-era header comments claiming the suite
is "expected to fail (404)" until a controller that has since been built and shipped; these are
misleading to a future reader and should be removed.

## Critical Issues

> **SEVERITY CORRECTED POST-REVIEW — CR-01 was not reachable.**
> This review analysed the controller in isolation and missed an upstream guard.
> `GroupSessionMiddleware` is registered in `Program.cs` after `UseAuthentication()` and
> before `UseAuthorization()`, and short-circuits any authenticated request whose
> `ActiveGroupId` is null: 302 to `/groups/pick` for GET/HEAD, 409 Conflict for
> POST/PUT/PATCH/DELETE. Its exempt-path list is only `/groups/pick`, `/GroupPicker`,
> `/Account`, `/platform` and `/Error` — Events, Contacts and Characters are all gated.
> No 500 was reachable through the HTTP pipeline, so the true severity is
> **defense-in-depth, not an active defect**.
>
> The fix was applied anyway, by explicit developer decision: all 11 unguarded call sites
> across `ContactsController`, `CharactersController` and `EventsController` now either
> short-circuit SuperAdmin to `GroupRole.Admin` (role lookups) or redirect to the group
> picker / fail closed (write-stamps and scoped reads), matching the Phase 34.3 pattern.
> Six SuperAdmin regression tests were added; suite went 779 → 785, all passing.
>
> Lesson for future reviews: a controller-level reachability claim is not established until
> the middleware pipeline above it has been checked.

### CR-01: SuperAdmin crashes with an unhandled exception when creating an event

**File:** `QuestBoard.Service/Controllers/Events/EventsController.cs:63`

**Issue:** `EventsController.Create` (POST) is gated by `[Authorize(Policy = "DungeonMasterOnly")]`.
`DungeonMasterHandler` explicitly bypasses this policy for `SuperAdmin`:

```csharp
// DungeonMasterHandler.cs
if (context.User.IsInRole("SuperAdmin"))
{
    context.Succeed(requirement);
    return;
}
```

`SuperAdmin` has `ActiveGroupId == null` by design (documented in
`.planning/codebase/CONCERNS.md` under "IActiveGroupContext null handling in SuperAdmin
context", and in the doc comment on `RequireActiveGroupId()` itself: *"Do NOT use this on the
SuperAdmin/see-all/seeding paths, where a null ActiveGroupId is intentional... calling it there
would incorrectly turn valid 'see all' behavior into an error."*)

Yet `Create` calls it unconditionally, with nothing upstream to short-circuit or guard it:

```csharp
[HttpPost]
[Authorize(Policy = "DungeonMasterOnly")]
[ValidateAntiForgeryToken]
public async Task<IActionResult> Create(EventViewModel viewModel, CancellationToken token = default)
{
    ...
    var newEvent = mapper.Map<Event>(viewModel);
    newEvent.GroupId = activeGroupContext.RequireActiveGroupId(); // throws InvalidOperationException for SuperAdmin
    await eventService.AddAsync(newEvent, token);
    ...
}
```

`RequireActiveGroupId()` throws `InvalidOperationException("Active group context is not
initialized. This request requires a selected group.")`, which is unhandled here and surfaces as
a 500 to any `SuperAdmin` who submits the Create Event form. This is exactly the bug class the
project already paid down once in Phase 34.3 (see `CONCERNS.md`: *"SuperAdmin null-dereference on
RequireActiveGroupId()"*), which prescribes checking `User.IsInRole("SuperAdmin")` before calling
it, or using `GetEffectiveGroupRoleAsync` instead where it applies. `QuestController`,
`QuestLogController`, and `DungeonMasterController` all apply that guard at their equivalent call
sites; this new controller does not.

Note `Edit`/`Delete`/`Details` are not exposed to the same crash: each of those first calls
`GetEventWithDetailsAsync(id)`, which is filtered by the same fail-closed query filter, so for a
`SuperAdmin` (`ActiveGroupId == null`) the lookup always returns `null` and the action returns
`NotFound()` before any `RequireActiveGroupId()` call downstream is reached. `Create` has no
such prior lookup to protect it.

**Fix:** Mirror the pattern already used elsewhere in the codebase (e.g.
`QuestController.cs:46-48`):

```csharp
if (currentUser.Id == 0)
{
    return Challenge();
}

if (!ModelState.IsValid)
{
    return View(viewModel);
}

if (User.IsInRole("SuperAdmin"))
{
    // SuperAdmin has no active group by design; creating a board-scoped event
    // requires picking a board first.
    return BadRequest("Select an active board before creating an event.");
}

var newEvent = mapper.Map<Event>(viewModel);
newEvent.GroupId = activeGroupContext.RequireActiveGroupId();
```

Add an integration test that authenticates as `SuperAdmin` and posts to `/Events/Create`,
asserting it does not 500 — the existing `EventsControllerIntegrationTests.cs` has no such case
today (see WR-02).

## Warnings

### WR-01: Stale "expected to 404" scaffold comments left in two shipped test files

**File:** `QuestBoard.IntegrationTests/Controllers/EventsControllerIntegrationTests.cs:6-9`
**File:** `QuestBoard.IntegrationTests/Controllers/EventCalendarPartialTests.cs:7-13`

**Issue:** Both files carry a header comment written for an earlier wave of this phase, before
`EventsController` existed:

```csharp
// This is an intentionally-failing scaffold: it targets the Events routes as plain string
// literals so the test project keeps compiling before the controller behind those routes
// exists. Every fact below is expected to return 404 (route not found) until that controller
// lands — that is the deliberate starting state for this suite, not a bug in the tests.
```

The controller has since landed and every fact in these files now asserts real 200/302/400
outcomes, not 404. The comment is now factually wrong and will mislead the next person who reads
this file into thinking these tests are still in a "red" scaffold state, or into not noticing a
future regression that actually does turn these into 404s.

**Fix:** Delete both stale header blocks (or replace with a short note describing what the suite
actually verifies now, without the "expected to fail" framing).

### WR-02: No SuperAdmin coverage in the Events controller test suite

**File:** `QuestBoard.IntegrationTests/Controllers/EventsControllerIntegrationTests.cs`

**Issue:** The suite covers `Player` (blocked) and `DungeonMaster` (allowed) for every write
action, but never exercises `SuperAdmin`, which is the one role that takes a structurally
different path through `EventsController` (policy bypass in `DungeonMasterHandler`, combined
with `ActiveGroupId == null`). This gap is why CR-01 shipped undetected — the project's own
`CONCERNS.md` "Safe modification" checklist for this exact fragility explicitly calls for "an
integration test that invokes the action as SuperAdmin and verifies no null-dereference occurs"
whenever a new controller action reaches this pattern.

**Fix:** Add a `[Fact]` that authenticates as `SuperAdmin` (with no active group), POSTs to
`/Events/Create`, and asserts a non-500 response — this will fail today against CR-01 and pass
once it is fixed.

## Info

### IN-01: Time-label formatting logic duplicated between two view models

**File:** `QuestBoard.Service/ViewModels/EventViewModels/EventViewModel.cs:31`
**File:** `QuestBoard.Service/ViewModels/CalendarViewModels/EventOnDay.cs:14`

**Issue:** The identical "all day vs HH:mm" formatting rule is implemented twice:

```csharp
// EventViewModel.cs
public string TimeLabel => StartTime.HasValue ? StartTime.Value.ToString("HH:mm") : "All day";

// EventOnDay.cs
public string TimeLabel => Event.StartTime.HasValue ? Event.StartTime.Value.ToString("HH:mm") : "All day";
```

Both comments explicitly frame this as "the single place" the wording is decided, but there are
in fact two copies; a future change to the wording (e.g. localizing "All day") has to remember to
touch both.

**Fix:** Move the formatting onto `Event` itself (e.g. `Event.TimeLabel` computed property) or a
small extension method (`TimeOnly?.ToTimeLabel()`), and have both view models delegate to it.

---

_Reviewed: 2026-08-27T06:42:08Z_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
