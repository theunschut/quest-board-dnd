---
phase: 82-personal-cross-board-event-agenda
reviewed: 2026-08-29T00:00:00Z
depth: standard
files_reviewed: 35
files_reviewed_list:
  - QuestBoard.Domain/Extensions/ServiceExtensions.cs
  - QuestBoard.Domain/Interfaces/IEventRepository.cs
  - QuestBoard.Domain/Interfaces/IEventService.cs
  - QuestBoard.Domain/Models/AgendaOptions.cs
  - QuestBoard.Domain/Models/AgendaRosterEntry.cs
  - QuestBoard.Domain/Models/AgendaRow.cs
  - QuestBoard.Domain/Models/CrossBoardAgenda.cs
  - QuestBoard.Domain/Services/EventService.cs
  - QuestBoard.IntegrationTests/Controllers/LayoutNavigationTests.cs
  - QuestBoard.IntegrationTests/Tests/AgendaControllerIntegrationTests.cs
  - QuestBoard.IntegrationTests/Tests/AgendaMobileRenderTests.cs
  - QuestBoard.IntegrationTests/Tests/AgendaTenantIsolationTests.cs
  - QuestBoard.Repository/EventRepository.cs
  - QuestBoard.Service/Automapper/ViewModelProfile.cs
  - QuestBoard.Service/Constants/SessionKeys.cs
  - QuestBoard.Service/Controllers/AgendaController.cs
  - QuestBoard.Service/Controllers/Events/EventsController.cs
  - QuestBoard.Service/Middleware/GroupSessionMiddleware.cs
  - QuestBoard.Service/ViewModels/AgendaViewModels/AgendaBoardOptionViewModel.cs
  - QuestBoard.Service/ViewModels/AgendaViewModels/AgendaEmptyState.cs
  - QuestBoard.Service/ViewModels/AgendaViewModels/AgendaRosterEntryViewModel.cs
  - QuestBoard.Service/ViewModels/AgendaViewModels/AgendaRowViewModel.cs
  - QuestBoard.Service/ViewModels/AgendaViewModels/AgendaViewModel.cs
  - QuestBoard.Service/Views/Agenda/Index.Mobile.cshtml
  - QuestBoard.Service/Views/Agenda/Index.cshtml
  - QuestBoard.Service/Views/Calendar/Index.Mobile.cshtml
  - QuestBoard.Service/Views/Calendar/Index.cshtml
  - QuestBoard.Service/Views/Events/Details.cshtml
  - QuestBoard.Service/Views/Events/Index.Mobile.cshtml
  - QuestBoard.Service/Views/Events/Index.cshtml
  - QuestBoard.Service/Views/Shared/_Layout.Mobile.cshtml
  - QuestBoard.Service/Views/Shared/_Layout.cshtml
  - QuestBoard.Service/wwwroot/css/agenda.css
  - QuestBoard.Service/wwwroot/css/agenda.mobile.css
  - QuestBoard.UnitTests/Services/CrossBoardAgendaTests.cs
findings:
  critical: 1
  warning: 8
  info: 8
  total: 17
status: issues_found
---

# Phase 82: Code Review Report

**Reviewed:** 2026-08-29
**Depth:** standard
**Files Reviewed:** 35
**Status:** issues_found

## Summary

The tenant-isolation core of this phase holds up under adversarial reading. The
`IgnoreQueryFilters()` bypass in `EventRepository.GetUpcomingAcrossGroupsWithSignupsAsync`
pins `memberGroupIds.Contains(e.GroupId)` in the same expression as the bypass; the includes
hang off already-pinned events; the ordering is fully deterministic (`Date`, `StartTime`,
`Id`) so the cross-board `Take` window cannot truncate unstably; the empty membership set
yields zero rows rather than everything; the controller intersects the session/query filter
against a freshly-read membership set *before* the query, so the filter can only narrow; and
the middleware exemption uses `StartsWithSegments`, so `/AgendaSomethingElse` does not match.
No XSS was found — every user-supplied value (board names, event titles, member names) goes
through Razor's default encoder, the modal's script uses `textContent`, and the `returnUrl`
round-trip is re-validated by `Url.IsLocalUrl` in `GroupPickerController`. The solution
builds clean. No GSD tracking references leaked into source; line endings are CRLF throughout.

What did not hold up is the **filter-persistence contract**. The one Critical finding is a
functional defect in the headline feature: the "Show More" paging link cannot distinguish
"no explicit filter" from "every board explicitly selected", so a single paging click
permanently writes the current board set into session — and any board joined afterwards is
then silently missing from the page whose entire purpose is to show every board's events.

Secondary concerns cluster around defence-in-depth that is documented rather than enforced:
the cross-board service API accepts a caller-supplied group set with only an XML comment
protecting it, the second-layer re-check drops foreign rows without emitting any signal, and
the whole isolation suite runs on the EF Core InMemory provider, so the riskiest line in the
phase is never translated to SQL.

## Critical Issues

### CR-01: "Show More" silently converts an implicit "all boards" view into a permanent session filter

**File:** `QuestBoard.Service/Controllers/AgendaController.cs:59-96,151`, `QuestBoard.Service/Views/Agenda/Index.cshtml:159`, `QuestBoard.Service/Views/Agenda/Index.Mobile.cshtml:173`

**Issue:**
`SelectedBoardIds` (`AgendaController.cs:151`) is always the joined *effective* board set, even
when the viewer never chose a filter — in the no-session, no-query case `requestedIds` is set
to `memberGroupIds` (line 63), so `effectiveGroupIds` is every board and `SelectedBoardIds`
is `"1,2"`.

Both paging links embed that value unconditionally:

```cshtml
<a href="@Url.Action("Index", new { take = Model.NextTake, boards = Model.SelectedBoardIds })">
```

On the next request `boardsProvided` is `true` and `rawBoards` is `"1,2"` (not `"all"`), so
control reaches line 89-96 and **writes `"1,2"` into `SessionKeys.AgendaBoardFilter`** — an
explicit filter the viewer never asked for.

The consequence is a silent data-omission bug in the primary feature. Sequence:

1. Viewer is on boards {1, 2}, has more than `DefaultTake` (5) upcoming events, clicks "Show More" once. Session now pins `"1,2"`.
2. Viewer is later added to board 3.
3. Every subsequent `/Agenda` request takes the `stored != null` branch (line 62-68) → `requestedIds = [1,2]` → board 3's events **never appear**.

The only signal is the collapsed dropdown's "2 of 3" badge; the one-click **"Show All Boards"**
reset (`Index.cshtml:73`) renders *only* in the `AllBoardsFiltered` empty state, so it is not
available in this partially-filtered state. A user therefore misses sessions on a board they
were just added to — precisely the failure this page exists to prevent. Note this directly
contradicts the stated contract on `AgendaViewModel.cs:16-18` ("carried into the paging link
so growing the window never silently resets the filter"): the code does not reset a filter, it
silently *creates* one.

The three integration tests around this (`Agenda_ShowMoreLink_CarriesEnlargedWindowAndCurrentSelection`,
`Agenda_FilterSelection_PersistsAcrossRequestsWithNoFilterParameter`) all start from an
explicit `?boards=` request, so none of them exercises the implicit-all → paging path.

**Fix:** Track whether an explicit selection is actually in effect and emit the reset sentinel
when it is not, so paging cannot manufacture a filter.

```csharp
// AgendaController.Index
var boardsProvided = Request.Query.TryGetValue("boards", out var rawBoardsValues);
var rawBoards = rawBoardsValues.ToString();
var isReset = boardsProvided && string.Equals(rawBoards, "all", StringComparison.OrdinalIgnoreCase);

string? stored = null;
List<int> requestedIds;
if (!boardsProvided)
{
    stored = HttpContext.Session.GetString(SessionKeys.AgendaBoardFilter);
    requestedIds = stored == null ? memberGroupIds
                 : stored == "none" ? []
                 : ParseBoardIds(stored);
}
else if (isReset) { HttpContext.Session.Remove(SessionKeys.AgendaBoardFilter); requestedIds = memberGroupIds; }
else { requestedIds = ParseBoardIds(rawBoards); }

// An explicit selection exists only when this request carried a real one, or session held one.
var hasExplicitSelection = (boardsProvided && !isReset) || stored != null;

// ... unchanged intersect + session write ...

// "all" round-trips through the reset branch above, which is a no-op when nothing is stored.
SelectedBoardIds = hasExplicitSelection ? string.Join(',', effectiveGroupIds) : "all",
```

Add a regression test: request `/Agenda` with no query and no session, follow the rendered
"Show More" href, then assert `SessionKeys.AgendaBoardFilter` is still absent (or, at the HTTP
level: seed a third board *after* following the link and assert its event appears on a plain
`/Agenda` request).

## Warnings

### WR-01: The cross-board API takes the membership set as a parameter, so the tenant bypass is protected by documentation rather than by code

**File:** `QuestBoard.Domain/Interfaces/IEventService.cs:61`, `QuestBoard.Domain/Services/EventService.cs:77-94`, `QuestBoard.Domain/Interfaces/IEventRepository.cs:91`

**Issue:** `GetCrossBoardAgendaAsync(IReadOnlyCollection<int> memberGroupIds, int currentUserId, …)`
is a public interface method that reaches a query which deliberately disables the global tenant
filter. Nothing in the type system or the call path requires `memberGroupIds` to be the caller's
own memberships — the only enforcement is the XML comment "which the caller reads fresh per
request from the viewer's own memberships". Today there is exactly one caller
(`AgendaController.cs:98`) and it is correct, so there is no live leak. But the app has shipped
two cross-tenant leaks before, and this is now the easiest possible shape to get wrong: a second
caller passing an unvalidated id list gets a full cross-tenant read with no error.

The service already receives `currentUserId`, so it has everything it needs to enforce this itself.

**Fix:** Inject `IGroupService` into `EventService` and intersect inside the domain layer, so the
parameter becomes a genuine hint that cannot widen:

```csharp
public async Task<CrossBoardAgenda> GetCrossBoardAgendaAsync(
    IReadOnlyCollection<int> requestedGroupIds, int currentUserId, int take, CancellationToken token = default)
{
    // Authorisation lives here, not in the caller: the requested set can only narrow the
    // viewer's own memberships, never add to them.
    var memberships = await groupService.GetGroupsForUserAsync(currentUserId, token);
    var memberGroupIds = requestedGroupIds.Intersect(memberships.Select(m => m.Id)).Distinct().ToList();
    ...
}
```

If the extra membership read is unwanted, at minimum rename the parameter to
`viewerMemberGroupIds` and mark the repository method `internal` so it cannot be reached from
outside the Repository assembly's own service registration.

### WR-02: The second-layer membership re-check drops foreign rows silently, so the regression it exists to catch would never be noticed

**File:** `QuestBoard.Domain/Services/EventService.cs:88-94`

**Issue:**

```csharp
var checkedRows = fetched.Where(row => memberGroupIds.Contains(row.Event.GroupId)).ToList();
```

This check exists to catch a dropped predicate in the `IgnoreQueryFilters()` query. If that
regression ever happens, the page renders normally — one fewer row, no exception, no log entry,
no metric. A live cross-tenant leak in the data layer would sit in production indefinitely,
visible only as an occasionally short agenda. That is not fail-closed for the *operator*, only
for the viewer. It is also inconsistent with how the rest of this codebase treats "cannot happen"
tenant states: `GroupSessionMiddleware.cs:107` deliberately chose a loud 409 over a silent
redirect for exactly this reason.

**Fix:** Treat a foreign row as an invariant violation, not as data to filter:

```csharp
var foreign = fetched.Where(row => !memberGroupIds.Contains(row.Event.GroupId)).ToList();
if (foreign.Count > 0)
{
    // The query pins GroupId to this same set, so a row outside it means the predicate was
    // lost. Fail the request rather than rendering a silently shortened agenda.
    throw new InvalidOperationException(
        $"Cross-board agenda returned {foreign.Count} row(s) outside the caller's board set.");
}
var checkedRows = fetched.ToList();
```

Update `CrossBoardAgendaTests.CrossBoardAgenda_RowOutsideMembershipSet_IsDroppedBeforeReachingTheCaller`
and `..._IsExcludedBeforeHasMoreIsComputed` to assert the throw. If throwing is judged too harsh
for a read-only page, inject `ILogger<EventService>` and log at Error — but do not leave it with
no signal at all.

### WR-03: The tenant-isolation suite runs on the EF Core InMemory provider, so the phase's riskiest line is never translated to SQL

**File:** `QuestBoard.IntegrationTests/Tests/AgendaTenantIsolationTests.cs` (whole file), `QuestBoard.IntegrationTests/WebApplicationFactoryBase.cs:61`, `QuestBoard.IntegrationTests/Helpers/TestDatabase.cs:13`

**Issue:** `AgendaTenantIsolationTests` is a well-constructed suite — the two-joined-boards
positive fact at lines 188-201 genuinely distinguishes "correctly scoped to two boards" from
"collapsed to one", which is the right thing to guard. But the harness is
`options.UseInMemoryDatabase(...)`, which evaluates every predicate as plain LINQ-to-Objects. That
means the three properties this phase's `EventRepository.cs:171-181` actually depends on are
never exercised against a relational provider:

- `memberGroupIds.Contains(e.GroupId)` with an **empty** collection is proven only as C# `List.Contains`, not as the SQL Server `OPENJSON`/`IN` translation the research flagged as an open question. (SQL Server does behave correctly here, but the tests are not what establishes that.)
- `.Take(take)` composed **before** `.Include(...).ThenInclude(...)` never has to survive query translation; a translation failure would throw in production while the suite stays green.
- `IgnoreQueryFilters()` interacting with the `EventSignupEntity` filter (`QuestBoardContext.cs:463-465`, which navigates `es.Event.GroupId`) generates no join at all in memory.

The provider choice is pre-existing and out of this phase's scope to change globally, but this
phase introduced the application's first user-facing filter bypass and is relying on this suite
as its safety net.

**Fix:** Add one relational smoke test for this query specifically — SQLite in-memory is enough
to prove the expression translates and the empty-`Contains` case returns zero rows:

```csharp
var connection = new SqliteConnection("DataSource=:memory:");
connection.Open();
var options = new DbContextOptionsBuilder<QuestBoardContext>().UseSqlite(connection).Options;
// seed two boards + events, then:
var rows = await repository.GetUpcomingAcrossGroupsWithSignupsAsync([], today, 10, default);
rows.Should().BeEmpty();
```

At minimum, note the provider limitation in the suite's class-level doc comment (lines 8-18),
which currently reads as though these facts prove end-to-end isolation.

### WR-04: Outline buttons violate the project's stated UI convention

**File:** `QuestBoard.Service/Views/Agenda/Index.cshtml:34,73`, `QuestBoard.Service/Views/Agenda/Index.Mobile.cshtml:33,75,122`

**Issue:** `CLAUDE.md` under "UI/UX Design Guidelines" states: *"Use filled colored buttons (not
outline)"*. Five new controls use `btn-outline-secondary`:

- `Index.cshtml:34` — "Filter Boards" dropdown toggle
- `Index.cshtml:73` — "Show All Boards" reset
- `Index.Mobile.cshtml:33` — "Filter Boards" collapse toggle
- `Index.Mobile.cshtml:75` — "Show All Boards" reset
- `Index.Mobile.cshtml:122` — "Show Roster" toggle

The sibling links added to pre-existing views in this same phase (`Events/Index.cshtml`,
`Calendar/Index.cshtml`) correctly use filled `btn-secondary`, so the new page is inconsistent
with both the convention and its own phase.

**Fix:** Replace `btn-outline-secondary` with `btn-secondary` on all five, per the convention and
matching the `My Agenda` entry-point buttons this phase added elsewhere.

### WR-05: ~110 lines duplicated verbatim between the desktop and mobile agenda views, including the board-switch modal and its script

**File:** `QuestBoard.Service/Views/Agenda/Index.cshtml:43-55,172-236`, `QuestBoard.Service/Views/Agenda/Index.Mobile.cshtml:43-55,182-246`

**Issue:** Three blocks are byte-identical across the two views:

- the board-filter `<form>` (13 lines)
- the entire `#switchBoardModal` including the antiforgery token and the `groupId`/`returnUrl` hidden fields (~48 lines)
- the `DOMContentLoaded` script that populates those hidden fields from `data-*` attributes (~15 lines)

Unlike the CSS pair — where the duplication is forced by the mobile layout's separate stylesheet
set and is explicitly justified in `agenda.mobile.css:5-11` — nothing forces this: Razor partials
render identically under both layouts, and this phase already uses one
(`~/Views/Events/_AvailabilityCell.cshtml`) for exactly this reason. The duplicated block is the
security-relevant part of the page (it posts a board switch), so a future fix applied to one copy
and not the other is a real drift risk.

**Fix:** Extract `~/Views/Agenda/_BoardFilterForm.cshtml` and `~/Views/Agenda/_SwitchBoardModal.cshtml`
(the latter taking `Model.ActiveBoardName`), render them from both views, and move the script into
`wwwroot/js/agenda.js` linked from both `@section Scripts` blocks. This also removes the two inline
`<script>` blocks flagged in IN-05.

### WR-06: Applying the board filter silently discards the current window size

**File:** `QuestBoard.Service/Views/Agenda/Index.cshtml:43-55`, `QuestBoard.Service/Views/Agenda/Index.Mobile.cshtml:43-55`

**Issue:** The filter form is `<form asp-action="Index" method="get">` with only `boards` inputs.
A GET form submission replaces the entire query string, so a reader who has paged out to
`take=15` and then narrows the filter is thrown back to `DefaultTake` (5) with no indication why.
The reverse direction was carefully handled (the paging link carries `boards`); this direction
was not.

**Fix:** Carry the current window through the form:

```cshtml
<form asp-action="Index" method="get">
    <input type="hidden" name="take" value="@Model.Take" />
    <input type="hidden" name="boards" value="" />
    ...
```

`Model.Take` is already the server-clamped value, so this cannot be used to widen the window.

### WR-07: The middleware exemption is controller-wide, not action-wide, and the class doc does not say so

**File:** `QuestBoard.Service/Middleware/GroupSessionMiddleware.cs:55-69,92`

**Issue:** `ExemptPathPrefixes` entry `$"/{ControllerNameOf<AgendaController>()}"` is matched with
`Path.StartsWithSegments(prefix)` (line 92). The segment match correctly rejects
`/AgendaSomethingElse` and is case-insensitive by default, so the narrow-scoping requirement is
met for *paths*. What it does not scope is *actions*: every current and future action on
`AgendaController`, including POST/PUT/DELETE, is exempt from both the null-`ActiveGroupId` gate
(the 409 branch at line 107) and the 5-minute membership revalidation (lines 116-149).

Today `Index` is the only action and it is a read scoped by a fresh membership read, so there is
no live hole. But the class doc at lines 15-42 characterises exemptions as "the genuine
group-agnostic workflows … that must never be gated on having an active group", and the inline
comment at lines 62-68 justifies the exemption purely in terms of *this one read*. Someone adding
a POST to `AgendaController` later gets a silent bypass of both guards with no warning anywhere.

**Fix:** Extend the inline comment at lines 62-68 to state the scope explicitly and set the rule
for future actions, e.g.:

```csharp
// ... Skipping the periodic membership revalidation below on this path costs nothing,
// because the page re-reads the viewer's memberships from the database on every single
// request. NOTE: this exemption covers every action on AgendaController, not just Index.
// Any action added here must therefore derive its own scope from a fresh membership read
// and must not assume a non-null ActiveGroupId -- if that ever stops being true, split this
// entry into an explicit "/Agenda/Index" path instead of exempting the controller.
```

Alternatively narrow the entry to `"/Agenda/Index"` now — but note the bare `/Agenda` route
(default-action) must then be added as a second entry, or the nav links break.

### WR-08: The filter sentinels `"all"` and `"none"` are undeclared magic strings spread across four files

**File:** `QuestBoard.Service/Controllers/AgendaController.cs:64,69,89,95`, `QuestBoard.Service/Views/Agenda/Index.cshtml:73`, `QuestBoard.Service/Views/Agenda/Index.Mobile.cshtml:75`, `QuestBoard.Service/Constants/SessionKeys.cs:12-20`

**Issue:** Two protocol values carry real behaviour and exist only as repeated literals:

- `"all"` — the reset sentinel, compared twice in the controller (lines 69, 89) and produced by `asp-route-boards="all"` in both views.
- `"none"` — the empty-selection session marker, written at line 95 and read at line 64. Its meaning is documented in `SessionKeys.cs:17` but the literal itself lives in the controller.

A typo in any one of these fails *silently* rather than loudly: an unrecognised value falls
through `ParseBoardIds`, which discards non-numeric tokens via `RemoveEmptyEntries` + `TryParse`,
producing an empty list — so `?boards=al` renders a plausible-looking "All Boards Filtered Out"
page and persists `"none"` into session. Note also that the `"all"` comparison at line 69 is
`OrdinalIgnoreCase` while the `"none"` comparison at line 64 is `==` (ordinal, case-sensitive);
the asymmetry is unexplained and only works because line 95 is the sole writer.

**Fix:** Declare both next to the session key they belong to and reference them everywhere:

```csharp
// SessionKeys.cs
public const string AgendaBoardFilter = "AgendaBoardFilter";
/// <summary>Querystring sentinel that clears the remembered selection.</summary>
public const string AgendaBoardFilterResetSentinel = "all";
/// <summary>Stored value meaning the viewer deselected every board.</summary>
public const string AgendaBoardFilterNoneSentinel = "none";
```

and in the views: `asp-route-boards="@SessionKeys.AgendaBoardFilterResetSentinel"`.

## Info

### IN-01: The `boards` action parameter is declared but never read

**File:** `QuestBoard.Service/Controllers/AgendaController.cs:28,56`
**Issue:** `Index(int? take = null, string? boards = null, …)` binds `boards`, but the method reads
`Request.Query["boards"]` instead (line 56) and never touches the bound value. The comment at lines
46-55 explains why the raw read is necessary and says the parameter "documents the querystring key's
shape for callers", but a reader scanning the signature will reasonably assume `boards` is the value
in play. The compiler will not warn.
**Fix:** Drop the parameter and fold its documentation into the existing comment, or rename it to
something that cannot be mistaken for live state (e.g. `_boardsQueryKeyDoc`) — dropping it is cleaner.

### IN-02: `AgendaViewModel.CurrentUserId` is set but read by neither view

**File:** `QuestBoard.Service/ViewModels/AgendaViewModels/AgendaViewModel.cs:28`, `QuestBoard.Service/Controllers/AgendaController.cs:155`
**Issue:** The controller populates `CurrentUserId`, but neither `Index.cshtml` nor
`Index.Mobile.cshtml` references it — `AgendaRosterEntryViewModel.IsViewer` already carries the
"this is you" signal, which is what both views actually use. Dead property, copied from
`EventOverviewViewModel` where it *is* consumed.
**Fix:** Remove the property and line 155.

### IN-03: `.agenda-list` is a class name with no rule anywhere

**File:** `QuestBoard.Service/Views/Agenda/Index.cshtml:80`, `QuestBoard.Service/wwwroot/css/agenda.css`
**Issue:** `<div class="agenda-list">` has no matching selector in `agenda.css`, `agenda.mobile.css`,
or any other stylesheet. It reads as a styling hook that was never written.
**Fix:** Drop the class, or add the rule it was meant to carry.

### IN-04: The query key is a bare literal disconnected from the tag helpers that produce it

**File:** `QuestBoard.Service/Controllers/AgendaController.cs:56`
**Issue:** `Request.Query.TryGetValue("boards", …)` uses a string literal, while the views produce
the same key through `name="boards"`, `asp-route-boards`, and `new { boards = … }`. Nothing ties the
five occurrences together; renaming any one silently breaks the filter with no compile error and no
test failure outside the integration suite.
**Fix:** `private const string BoardsQueryKey = "boards";` on the controller and reference it from
the raw read (the views cannot use it, but a single named constant at least makes the producer/consumer
pair greppable).

### IN-05: Two inline `onclick` handlers in the mobile view are dead by their own admission and block a future CSP

**File:** `QuestBoard.Service/Views/Agenda/Index.Mobile.cshtml:124,133`
**Issue:** `onclick="event.stopPropagation();"` appears on the roster toggle and the `.collapse`
container. The comment at lines 127-132 states plainly that "This card has no ambient click handler
of its own today", so both are defensive no-ops. They are also inline event handlers, which would
have to be removed before the app could adopt a `script-src 'self'` CSP (the app currently sends no
`Content-Security-Policy` header at all, so nothing is broken today).
**Fix:** Keep the guard if the team wants it, but move it into the extracted `agenda.js` from WR-05
as a delegated listener rather than two inline attributes.

### IN-06: Page-size arithmetic can overflow because `AgendaOptions` is bounded only from below

**File:** `QuestBoard.Domain/Models/AgendaOptions.cs:21`, `QuestBoard.Service/Controllers/AgendaController.cs:154`, `QuestBoard.Domain/Services/EventService.cs:86`
**Issue:** `IsValid()` checks `>= 1` and `DefaultTake <= MaxTake` but sets no ceiling. With a
pathological configured `MaxTake`/`PageIncrement` near `int.MaxValue`, `effectiveTake + options.PageIncrement`
(controller line 154) and `take + 1` (service line 86) both overflow to negative, and a negative
`Take(...)` is a runtime failure rather than a clamped page. Config-driven and unlikely, but the
option class is presented as validated-on-start, which currently overstates what it guarantees.
**Fix:** `public bool IsValid() => DefaultTake >= 1 && MaxTake >= 1 && MaxTake <= 500 && PageIncrement >= 1 && PageIncrement <= MaxTake && DefaultTake <= MaxTake;`
and update the `.Validate(...)` message on `ServiceExtensions.cs:33` to match.

### IN-07: The unresolvable-user path is guarded in the sibling controller but not here

**File:** `QuestBoard.Service/Controllers/AgendaController.cs:33`, `QuestBoard.Domain/Services/UserService.cs:84-89`, `QuestBoard.Service/Controllers/Events/EventsController.cs:47`
**Issue:** `GetUserAsync` returns `new User()` with `Id == 0` when the principal cannot be resolved.
`EventsController` guards for that explicitly (`currentUser.Id != 0 && await IsDmTierAsync()`);
`AgendaController` does not. It happens to fail closed — user 0 holds no membership rows, so the page
renders "No Boards Yet" — but that safety is incidental rather than stated, and `currentUser.Id` is
also passed into `GetCrossBoardAgendaAsync` as the viewer identity where `0` would silently match no
signup row.
**Fix:** Either add an explicit early return, or note in the existing comment block (lines 21-26) that
an unresolvable principal degrades to the zero-membership case by construction.

### IN-08: No test guards the `.collapse` container's `stopPropagation`, which the implementation calls out as deliberate

**File:** `QuestBoard.IntegrationTests/Tests/AgendaMobileRenderTests.cs:127-148`, `QuestBoard.Service/Views/Agenda/Index.Mobile.cshtml:133`
**Issue:** The markup correctly carries the guard on **both** the toggle and the `.collapse`
container — the sibling-page bug this was written to avoid is genuinely not present. But
`Agenda_MobileRoster_CollapsedByDefaultAndContainsMemberNames` asserts only `id="roster-{eventId}"`
and `agenda-roster-toggle`; nothing pins the container guard, so a future edit can drop it and stay
green despite the "keep it" instruction at `Index.Mobile.cshtml:127-132`.
**Fix:** Add `html.Should().Contain($"id=\"roster-{eventId}\" onclick=\"event.stopPropagation();\"");`
to that fact — or, if WR-05's `agenda.js` extraction lands, assert the delegated selector instead.

---

_Reviewed: 2026-08-29_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
