# Phase 74: Event Schema, CRUD, and Calendar Display - Research

**Researched:** 2026-08-26
**Domain:** ASP.NET Core 10 MVC / EF Core 10 additive schema + CRUD + Razor calendar rendering
**Confidence:** HIGH

## Summary

This phase is not a new-technology problem — it is a "follow the `Contact` precedent exactly, twice" problem. `ContactEntity` + `ContactRepository` + `ContactService` + `ContactsController` + `20260706193921_AddContactsFeature` is a complete, working, in-repo template for everything D-01 through D-24 ask for: a `GroupId`-scoped entity, a fail-closed `HasQueryFilter`, a multi-table additive migration with ordered `CreateTable` calls, `BaseRepository`/`BaseService` CRUD, and a DM-gated controller. `EventEntity` and `EventSeriesEntity` (GroupId-scoped) and `EventSignupEntity` (scoped through `Event`) map directly onto that shape. `TenantIsolationTests.cs` is an equally complete template for the D-22 two-group test.

The one genuinely new piece of engineering is the calendar rendering integration, and it has one concrete landmine already found in the codebase: `.calendar-body { grid-auto-rows: 120px }` combined with `.calendar-day { overflow: hidden }` in `calendar.css`. Today `.quest-events` is the only thing inside that fixed-height cell. Stacking an events block above it per D-08 without touching the row-height/overflow rule will silently clip whichever content doesn't fit — this needs an explicit CSS decision (e.g. `grid-auto-rows: minmax(120px, auto)`), not just "add a div."

The `DateOnly`/`TimeOnly` decision (D-01) is technically sound and low-risk: EF Core 8+ (this project runs 10.0.9) natively maps `DateOnly` → SQL Server `date` and `TimeOnly` → SQL Server `time`, no third-party package needed, confirmed by Microsoft's own EF8 breaking-changes documentation. The InMemory provider used by the integration test suite has supported both types since EF Core 6.

**Primary recommendation:** Build `EventEntity`/`EventSeriesEntity`/`EventSignupEntity` and their repository/service/controller stack as structural clones of the `Contact` stack (schema, query filters, CRUD flow, AutoMapper profile shape), reuse `_MarkdownEditor.cshtml` + `IMarkdownService` untouched, and treat the `_Calendar.cshtml` day-cell height as a CSS problem to solve explicitly rather than an emergent side effect.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Event CRUD (create/edit/delete) | API / Backend (MVC controller + Domain service) | Database / Storage | Standard Service → Domain → Repository write path, mirrors `ContactsController` |
| Tenant scoping on read | Database / Storage (`HasQueryFilter`) | API / Backend | EF Core global query filter is the single source of truth for reads; matches every other `GroupId`-scoped entity |
| Tenant scoping on write | API / Backend (controller/service) | Database / Storage | `HasQueryFilter` does not constrain inserts — D-21 requires an explicit board check in the write path, same as `ContactsController.Create` tagging `contact.GroupId = activeGroupContext.RequireActiveGroupId()` |
| Desktop calendar rendering | Frontend Server (Razor partial `_Calendar.cshtml`) | Browser (CSS) | Server-rendered day grid; CSS governs the visual distinction and row-height coping (D-08) |
| Mobile agenda rendering | Frontend Server (Razor view `Index.Mobile.cshtml`) | Browser (CSS) | Real platform-switched view (`.Mobile.cshtml` resolution), not a responsive CSS variant |
| Markdown authoring/rendering | Frontend Server (`_MarkdownEditor.cshtml` + `IMarkdownService`) | — | Reused untouched per D-06; no new rendering pipeline |
| DateOnly/TimeOnly ↔ DateTime conversion | API / Backend (controller or a dedicated mapper, per Claude's Discretion) | — | Single well-named seam per D-01's accepted cost — must not scatter into views |
| Navbar entry visibility | Frontend Server (Razor `@if` + `AuthorizationService`) | — | Matches "Create Quest" exactly — no `BoardType` gate |

## User Constraints

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**Storage shape**
- D-01: `DateOnly` for the occurrence date, `TimeOnly?` for the optional start time. `CalendarViewModel` stays `DateTime` end to end — one well-named conversion seam, not scattered `.ToDateTime(...)` calls.
- D-02: All three tables (Events, EventSeries, EventSignups) land in this one migration as ordered `CreateTable` calls, no backfill, matching `20260706193921_AddContactsFeature`.
- D-03: `EventEntity` carries a nullable series FK from day one, so a one-off event and a Phase-76 materialized occurrence are the same entity.
- D-04: `GroupId` on `EventEntity` and `EventSeriesEntity`; `EventSignupEntity` scoped through its required `Event` navigation. Every filter fail-closed. Mirror `QuestBoardContext.cs:270–283` comment block exactly, including "do not capture `ActiveGroupId` into a local var."
- D-05: No author column. `CreatedAt` only. Any DM on the board can edit/delete any event (→ D-11).
- D-06: Description is Markdown, unbounded — matches `Quest.Description`, not `Contact.Description`'s `[StringLength(2000)]`. Reuse `_MarkdownEditor.cshtml` and `IMarkdownService.ExtractPlainText()` for any plain-text teaser.
- D-07: No `EventType` field, no relation to `Quest`. Meaning comes from the board's immutable `BoardType`.

**Desktop calendar rendering**
- D-08: Events render in their own block above the quest list inside a day cell, own CSS class, not interleaved into `.quest-events`. Grid row height must cope — check this, don't assume it.
- D-09: The 5 out-of-scope call sites (`Views/Quest/Details.cshtml:604,648,696`, `Views/Quest/Details.Mobile.cshtml:158,196`) are protected structurally via an events collection that defaults to empty on `CalendarViewModel` — no flag, no branch. This is an acceptance criterion: a test must assert a Quest Details page with a same-day event renders no event markup.
- D-10: The event chip is clickable and opens an event details view (readable by every board member).
- D-11: Edit and Delete live on the event details view, DM-gated, nowhere else. No inline pencil/trash on the calendar chip. No dedicated Events index page.
- D-12: The Legend card gains an Event row; the "Click quests for details" hint is updated to cover events too.

**Mobile agenda**
- D-13: Widen the `Index.Mobile.cshtml:9` filter to has-quests-or-has-events, AND rewrite the empty state to be month-neutral. Both halves required.
- D-14: An event with no start time renders as "All day" on both mobile and desktop.
- D-15: Within a day section, events come first, then quests (mirrors desktop D-08).
- D-16: A mobile event entry taps through to the same event details view as desktop, styled with its own agenda-entry CSS class. Mobile markup must be verified with a real mobile User-Agent, not devtools emulation.

**CRUD behaviour**
- D-17: Delete uses a native `confirm()` dialog (Phase 72 D-07 idiom). Flagged for revisit in Phase 75 once signups exist.
- D-18: Hard delete, not soft delete. No cancelled/deleted flag.
- D-19: No date restriction — an event can be created or edited onto a past date. Accepted cost: no guard against a fat-fingered year.
- D-20: After Create, Edit, and Delete, redirect to the calendar at the event's month via `CalendarController.Index(year, month)`. All three actions set `TempData["Success"]`, picked up automatically by `_Toasts.cshtml`.

**Tenant scoping and testing**
- D-21: Defence in both layers — `HasQueryFilter` for reads plus an explicit board check on write. With two `GroupId` columns (D-04), the write path must reject an event whose series belongs to a different board.
- D-22: A dedicated two-group integration test is not optional. Follow `TenantIsolationTests.cs`: seed Group 2 via `factory.Database.CreateContext()`, flip `factory.TestGroupContext.ActiveGroupId = 1`, assert absence, reset to `1` in `DisposeAsync`.
- D-23: Quest creation must be provably unaffected (EVENT-05) — needs a test that asserts it.
- D-24: The migration must not break boot. Purely additive, ordered `CreateTable` calls, no backfill — verify the app starts against a database at the pre-74 migration.

### Claude's Discretion

Not discussed — planner decides:
- Exact chip colour / CSS class names for the event block, and whether events get their own per-day cap (quests use `Take(3)` with no "and N more" affordance).
- Whether the event details view needs a separate `.Mobile` variant, and whether Create/Edit forms do.
- Controller / service / repository / domain-model naming and file placement, and the Entity ↔ DomainModel ↔ ViewModel AutoMapper profile entries.
- Whether the events collection on `CalendarViewModel` is raw domain models or a `QuestOnDay`-style wrapper, and where the `DateOnly` → `DateTime` conversion seam (D-01) sits.
- `Title` max length, and `OnDelete` behaviour on the `GroupId` and series FKs.
- Whether the mobile agenda's month-neutral empty-state copy is "Nothing This Month" or another wording.
- Toast message wording for create / edit / delete.
- Index strategy on `Events` (a `(GroupId, Date)` index is the obvious candidate given the monthly calendar query, cf. `AddQuestFinalizedDateIndex`).
- Test structure beyond the two named must-haves (D-22 cross-group, D-23 quest-creation-unaffected) and the D-09 Details-page assertion.

### Deferred Ideas (OUT OF SCOPE)

- Stronger delete confirmation once signups exist — revisit in Phase 75 (D-17).
- An Events index / management page — rejected here as a second render surface no EVENT requirement asks for.
- A "and N more" affordance on crowded calendar day cells — pre-existing gap, out of scope, but events make the cell busier.
- Guarding against a fat-fingered year on event dates (accepted cost of D-19).
- Soft delete / cancelled state — belongs with Phase 76's cancelled-occurrence concept (D-18).
- Player availability signups (Phase 75), recurring series generation (Phase 76), the availability overview page (Phase 77).
- Any change to the 5 `Views/Quest/Details(.Mobile).cshtml` call sites of `_Calendar.cshtml`.
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| EVENT-01 | A DM can create an event with title, optional description, date, optional start time | `Contact` Create flow (`ContactsController.Create`, `ContactViewModel`) is the direct template; `_MarkdownEditor.cshtml` supplies the description field |
| EVENT-02 | A DM can edit and delete events on their own board; events scoped to that board, never visible to another | `ContactsController.Edit`/`Delete` DM-gated pattern; `QuestBoardContext` fail-closed `HasQueryFilter` pattern (D-04); write-side `RequireActiveGroupId()` pattern (D-21) |
| EVENT-03 | Events appear on desktop calendar, visually distinguishable from quests at a glance | `_Calendar.cshtml` day-cell structure, `.quest-event`/`.legend-item` CSS classes to extend per D-08/D-12; row-height pitfall documented below |
| EVENT-04 | Events appear on mobile calendar, which today only lists days with quests | `Index.Mobile.cshtml` filter/empty-state rewrite (D-13); `agenda-quest-*` CSS classes to extend per D-16 |
| EVENT-05 | Events never appear on quest board main page, never block/constrain quest creation | `QuestController.Create` has zero coupling point to touch — a test (D-23) proves the negative rather than code proving it by omission |
| EVENT-06 | "Create Event" sits in same navbar category as "Create Quest", available to all DM roles | `_Layout.cshtml:88-118` / `_Layout.Mobile.cshtml:75-99` DM dropdown, ungated by `BoardType` (matches "Create Quest") |
</phase_requirements>

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Entity Framework Core (SqlServer + relational) | 10.0.9 [VERIFIED: STACK.md + codebase] | ORM, migrations, `date`/`time` column mapping for `DateOnly`/`TimeOnly` | Already the project's ORM; EF Core 8+ natively maps `DateOnly`→`date`, `TimeOnly`→`time` with zero extra package [CITED: learn.microsoft.com/ef/core/what-is-new/ef-core-8.0/breaking-changes] |
| Microsoft.EntityFrameworkCore.InMemory | 10.0.9 [VERIFIED: STACK.md] | Integration test database provider | Already used by `WebApplicationFactoryBase`/`TestDatabase`; supports `DateOnly`/`TimeOnly` (native since EF Core 6) |
| AutoMapper | (project-pinned, unchanged) [ASSUMED — not re-verified this session, no version bump needed] | Entity ↔ DomainModel ↔ ViewModel mapping | Two-boundary convention already established; Event mappings are new profile entries, not a new dependency |
| Markdig (via `IMarkdownService`) | (project-pinned, unchanged) | Markdown → sanitized HTML rendering for event description | Already wraps this; D-06 reuses it, introduces no new package |

**No new NuGet packages are required for this phase.** Every capability (schema, CRUD, Markdown, calendar view) is served by libraries already in the three csproj files.

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| xUnit v3 / FluentAssertions / NSubstitute | (project-pinned) | Integration/unit tests for D-22/D-23/D-09 assertions | Standard for every new test file in this repo |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| `DateOnly`/`TimeOnly` (locked D-01) | `DateTime` matching `Quest.FinalizedDate` | Rejected by the user explicitly — reproduces the half-observed naive-local convention and its DST bug class |
| Server-side `EventEntity` clone of `Contact` | A generic "calendar item" polymorphic table shared with `Quest` | Rejected implicitly by D-07 ("No relation to Quest") and by the ROADMAP's explicit no-`EventType` stance — would reintroduce the exact discriminator ambiguity the decisions rule out |

**Installation:** None. No `dotnet add package` commands needed for this phase.

**Version verification:** EF Core and InMemory provider versions confirmed via `.planning/codebase/STACK.md` (10.0.9, both), which reflects the actually-restored packages in this repo — no drift expected since this phase adds no new package references.

## Package Legitimacy Audit

**Not applicable.** This phase installs zero new external packages — it only adds new entities, a migration, controllers/services/repositories, views, and CSS using libraries already present in the solution (EF Core, AutoMapper, Markdig via the existing `IMarkdownService`, xUnit/FluentAssertions/NSubstitute). No `npm view` / `pip index versions` / `cargo search` step applies.

## Architecture Patterns

### System Architecture Diagram

```
[Browser: Desktop /Calendar/Index]         [Browser: Mobile /Calendar/Index (platform-switched view)]
        |                                              |
        v                                              v
CalendarController.Index(year, month)  <---------------+
        |
        |-- questService.GetQuestsForCalendarAsync()  (existing, unchanged)
        |-- eventService.GetEventsForCalendarAsync()  (new, mirrors the above: fetch-all,
        |                                               filter-by-month happens in the view model,
        |                                               same convention as Quests today)
        v
CalendarViewModel { Quests, Events }   <-- new Events collection, DEFAULT EMPTY (D-09)
        |
        |-- GetCalendarDays() produces List<CalendarDay>, each day now also carrying
        |   List<EventOnDay> alongside List<QuestOnDay>
        v
_Calendar.cshtml (SHARED partial, 6 call sites)
        |
        |-- day.EventsOnDay.Any() --> render events block ABOVE .quest-events (D-08)
        |-- day.QuestsOnDay.Take(3) --> unchanged quest rendering
        |
        +--> [5 out-of-scope Quest/Details(.Mobile) call sites: Events collection is
              always empty there because those views build their own local
              CalendarViewModel and never populate Events -- renders nothing, no branch]

Index.Mobile.cshtml (NOT via _Calendar.cshtml -- hand-rolled agenda loop)
        |
        |-- agendaDays = GetCalendarDays().Where(d => quests-or-events)  (D-13, widened filter)
        |-- per day: events first (D-15), then quests
        v
    Agenda entries --> click --> EventsController.Details(id)  (same target as desktop chip, D-10/D-16)

[DM navbar: "Create Event"] --> EventsController.Create (GET/POST, DM-gated)
        |
        v
    EventsController.Create POST
        |-- event.GroupId = activeGroupContext.RequireActiveGroupId()   (D-21 write-side scoping)
        |-- eventService.AddAsync(event)
        v
    Redirect to CalendarController.Index(event's year, event's month)   (D-20)
```

### Recommended Project Structure
```
QuestBoard.Repository/
├── Entities/
│   ├── EventEntity.cs            # GroupId, Title, Description, Date (DateOnly), StartTime (TimeOnly?), SeriesId?, CreatedAt
│   ├── EventSeriesEntity.cs      # GroupId, cadence/anchor fields owned by Phase 76 but table created now (D-02)
│   └── EventSignupEntity.cs      # EventId (required), scoped through Event (D-04); fields owned by Phase 75
├── EventRepository.cs            # BaseRepository<Event, EventEntity> + GetEventsForCalendarAsync, GetEventWithDetailsAsync
└── Migrations/
    └── <timestamp>_AddCalendarEventsFeature.cs   # 3 ordered CreateTable calls, no backfill (D-02)

QuestBoard.Domain/
├── Interfaces/
│   ├── IEventRepository.cs
│   └── IEventService.cs
├── Models/
│   └── Event.cs                  # domain model mirroring Contact.cs shape
└── Services/
    └── EventService.cs           # BaseService<Event> + calendar-read passthrough

QuestBoard.Service/
├── Controllers/Events/
│   └── EventsController.cs       # Create/Edit/Delete (DM-gated), Details (all members)
├── ViewModels/EventViewModels/
│   └── EventViewModel.cs         # Title, Description (Markdown, unbounded), Date, StartTime?
├── ViewModels/CalendarViewModels/
│   ├── CalendarViewModel.cs      # + List<Event> Events (default empty, D-09) + EventsOnDay logic
│   └── EventOnDay.cs             # new, mirrors QuestOnDay's per-day wrapper shape
└── Views/
    ├── Events/
    │   ├── Create.cshtml (+ .Mobile if discretion says split)
    │   ├── Edit.cshtml
    │   └── Details.cshtml        # DM-gated Edit/Delete buttons live here only (D-11)
    ├── Shared/_Calendar.cshtml   # extended: events block above .quest-events (D-08)
    └── Calendar/
        ├── Index.cshtml          # Legend gains an Event row (D-12)
        └── Index.Mobile.cshtml   # widened filter + month-neutral empty state (D-13)
```

### Pattern 1: GroupId-scoped entity with fail-closed query filter (D-04)
**What:** An entity carries its own `GroupId` column, mapped with a `[ForeignKey]` navigation to `GroupEntity`, and a `HasQueryFilter` in `QuestBoardContext.OnModelCreating` that returns zero rows when `ActiveGroupId` is null.
**When to use:** `EventEntity` and `EventSeriesEntity` — both need their own `GroupId` per D-04's rationale (the series FK on `Event` is nullable and points the wrong way for `Event` to be scoped through it).
**Example:**
```csharp
// Source: QuestBoard.Repository/Entities/QuestBoardContext.cs:270-345 (existing pattern)
// ContactEntity deliberately does NOT offer a SuperAdmin cross-group view like Quest/ShopItem
// do above -- same "per-group roster" shape as CharacterEntity. An empty Contact list when no
// group is selected is the intended behavior here, not an oversight.
modelBuilder.Entity<ContactEntity>()
    .HasQueryFilter(e =>
        activeGroupContext.ActiveGroupId != null &&
        e.GroupId == activeGroupContext.ActiveGroupId);
```
Apply the identical shape to `EventEntity` and `EventSeriesEntity`. For `EventSignupEntity` (no own `GroupId`), mirror `PlayerSignupEntity`'s through-navigation filter:
```csharp
// Source: QuestBoardContext.cs:301-305 (existing pattern) — apply the same shape via ps.Event.GroupId
modelBuilder.Entity<PlayerSignupEntity>()
    .HasQueryFilter(ps =>
        activeGroupContext.ActiveGroupId != null &&
        ps.Quest.GroupId == activeGroupContext.ActiveGroupId);
```

### Pattern 2: Explicit write-side board check (D-21)
**What:** `HasQueryFilter` never runs on `Add`. The controller must stamp `GroupId` from the active group context, not trust a client-supplied value.
**When to use:** Every Create action, and any Edit path that could re-associate an event with a different series.
**Example:**
```csharp
// Source: QuestBoard.Service/Controllers/Contacts/ContactsController.cs (existing pattern)
// Tag the contact to the active group so the group-scoped roster query filter
// applies (ContactEntity is scoped by a global query filter on GroupId).
contact.GroupId = activeGroupContext.RequireActiveGroupId();
```
For the series-FK case D-21 calls out specifically (an event pointing at another board's series), add an explicit equality check before save: `if (series != null && series.GroupId != activeGroupContext.RequireActiveGroupId()) return BadRequest();` — there is no existing precedent for this exact cross-FK check in the codebase (Contacts has no analogous second FK), so this is new code, not a copy.

### Pattern 3: Multi-table additive migration, ordered `CreateTable`, no backfill (D-02)
**What:** One migration file creates all new tables in dependency order (parent tables before FK-dependent children), with indexes added at the end.
**Example:**
```csharp
// Source: QuestBoard.Repository/Migrations/20260706193921_AddContactsFeature.cs (existing pattern)
migrationBuilder.CreateTable(
    name: "Contacts",
    columns: table => new { /* ... */ Id = table.Column<int>(...).Annotation("SqlServer:Identity", "1, 1") /* ... */ },
    constraints: table =>
    {
        table.PrimaryKey("PK_Contacts", x => x.Id);
        table.ForeignKey(name: "FK_Contacts_Groups_GroupId", column: x => x.GroupId,
            principalTable: "Groups", principalColumn: "Id");
    });
// ... ContactImages depends on Contacts.Id (FK), created after Contacts
// ... ContactNotes depends on Contacts.Id (FK), created after Contacts
// ... indexes created last, after all three tables exist
```
For this phase: create `EventSeries` first (no FK dependency on `Events`), then `Events` (FK to `EventSeries`, nullable per D-03), then `EventSignups` (FK to `Events`, required).

### Pattern 4: `DateOnly`/`TimeOnly` entity properties (D-01)
**What:** EF Core 8+ maps these types natively — no `UseDateOnlyTimeOnly()` call, no third-party package.
**Example:**
```csharp
// New pattern for this codebase — no direct precedent yet, but standard EF Core 8+/10 usage
public DateOnly Date { get; set; }
public TimeOnly? StartTime { get; set; }
```
Migration output for these columns will read `type: "date"` and `type: "time"` respectively (verify by inspecting the generated migration file after `dotnet ef migrations add`, per D-24's boot-safety requirement).

### Anti-Patterns to Avoid
- **A local `var groupId = activeGroupContext.ActiveGroupId` captured before the `HasQueryFilter` lambda:** The existing `QuestBoardContext.cs` comment block is explicit that this captures the value once at model-build time (always null), not per-request. Reference `activeGroupContext` directly inside the lambda.
- **Trusting a posted `GroupId` or `EventSeriesId` on Create/Edit:** Both must be derived/verified server-side (D-21); a hidden form field is not a security boundary.
- **A duplicate `_CalendarWithEvents.cshtml`:** Explicitly rejected by D-09 — extend the one shared partial with a default-empty collection instead.
- **Interleaving events into `.quest-events`:** Explicitly rejected by D-08 — separate block, separate CSS class.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Markdown → HTML rendering + sanitization | A second Markdown pipeline for events | `IMarkdownService.RenderToHtml()` / `.ExtractPlainText()` | D-06 explicitly requires this; PROJECT.md blames a second text convention for four recorded drift bugs |
| Tenant scoping | Manual `.Where(x => x.GroupId == someGroupId)` sprinkled through repository methods | EF Core global `HasQueryFilter` (reads) + `RequireActiveGroupId()` (writes) | Matches every existing group-scoped entity; a hand-rolled per-query filter is exactly the kind of gap that caused Phases 49/55's real leaks |
| Monday-first calendar grid math | A parallel day-list builder for events | `CalendarViewModel.GetCalendarDays()`'s existing padding logic — events slot into the same `List<CalendarDay>`, not a second structure | One canonical day list keeps desktop and mobile from diverging (Phase 72's explicit lesson, restated as D-15's rationale) |
| Toast/flash messaging | A bespoke event-specific success banner | `TempData["Success"]` + the existing `_Toasts.cshtml` | Already wired into every layout; zero new view code required (D-20, established Phase 72 D-14) |

**Key insight:** Nothing in this phase requires new infrastructure. The entire implementation risk is in *disciplined reuse* of five already-proven patterns (query filter, write-side group stamp, additive migration, Markdown editor, toast) plus one genuinely new CSS layout decision (event-block-above-quest-list row height).

## Common Pitfalls

### Pitfall 1: Fixed-height calendar day cells clip the new events block
**What goes wrong:** `.calendar-body { grid-auto-rows: 120px }` and `.calendar-day { overflow: hidden }` (both in `QuestBoard.Service/wwwroot/css/calendar.css`) mean the day cell has a hard-coded height today. Currently only `.quest-events` fills that space. Stacking a new events block above it per D-08 without adjusting the row-height rule will silently clip content — either the events, the quests, or both — with no visible error.
**Why it happens:** The grid was designed for one child element (`.quest-events`); D-08 adds a second sibling.
**How to avoid:** Change `.calendar-body`'s `grid-auto-rows` to something that can grow (e.g. `minmax(120px, auto)`) or cap total visible items per cell explicitly (events + `Take(3)` quests) and verify against a day with both. D-08's own text calls this out as an "accepted cost... check this, don't assume it" — treat it as a required verification step, not an afterthought.
**Warning signs:** A day cell with 1 event + 3 quests renders visually truncated in a screenshot/manual check; `.quest-event` items get clipped at the bottom of the cell.

### Pitfall 2: `.calendar-day` has `overflow: hidden` — a details-page call site could hide an event chip mid-render if the collection isn't actually empty
**What goes wrong:** D-09's safety net (default-empty `Events` collection) only works if every one of the 5 out-of-scope call sites genuinely never populates it. If a future refactor of `Quest/Details.cshtml` starts constructing its local `CalendarViewModel` via a shared factory method that *does* populate events, the structural protection silently breaks.
**Why it happens:** The 5 call sites build `CalendarViewModel` inline today (per CONTEXT.md's canonical refs) — there's no single choke point enforcing "Details pages never get events."
**How to avoid:** The D-09 acceptance-criterion test (assert no event markup renders on a Quest Details page with a same-day event) is the actual safety net — write it as a first-class test, not a manual check, precisely because the structural protection is a convention, not a compiler-enforced constraint.
**Warning signs:** A Quest Details page starts showing an unrelated event chip after an unrelated refactor.

### Pitfall 3: `EventSignupEntity`'s only scoping path is `Event.GroupId` — but this phase creates the table with no code reading/writing it yet
**What goes wrong:** D-02 deliberately creates `EventSignups` now for Phase 75 to fill in later. If the `HasQueryFilter` for `EventSignupEntity` isn't added in this same migration/model-change (even though nothing writes to the table yet), Phase 75 inherits an unscoped table and has to remember to add tenant scoping retroactively — exactly the kind of "half-observed convention" D-01 was written to avoid for dates.
**Why it happens:** It's tempting to defer the query filter to "whichever phase actually uses the table."
**How to avoid:** Add all three `HasQueryFilter` entries (Event, EventSeries, EventSignup-via-Event) in this phase's `QuestBoardContext` changes, even though `EventSignupEntity` gets zero reads/writes until Phase 75. This is explicit in D-04's text ("Every filter must be fail-closed") and the phase's own framing ("owns the storage convention and tenant scoping for the whole feature").
**Warning signs:** Phase 75 discovers `EventSignupEntity` has no query filter and has to retrofit it under time pressure, with occurrence data already in the table.

### Pitfall 4: `DateOnly`/`TimeOnly` migration column types must be verified, not assumed
**What goes wrong:** Older guidance (EF Core 6/7, `ErikEJ.EntityFrameworkCore.SqlServer.DateOnlyTimeOnly`) suggests a third-party package is needed. On EF Core 10 this package must NOT be added — it is unnecessary and could conflict with the now-native mapping.
**How to avoid:** After `dotnet ef migrations add`, open the generated migration file and confirm the `Date` and `StartTime` columns read `type: "date"` and `type: "time"` respectively with no extra package reference added to any `.csproj`. This directly serves D-24 (migration must not break boot) — an incorrect or conflicting type mapping is exactly the kind of thing that would only surface at `context.Database.Migrate()` time on startup.

## Code Examples

### DM-gated CRUD controller shape (verified pattern from this codebase)
```csharp
// Source: QuestBoard.Service/Controllers/Contacts/ContactsController.cs (existing, working code)
[HttpPost]
[Authorize(Policy = "DungeonMasterOnly")]
[ValidateAntiForgeryToken]
public async Task<IActionResult> Create(ContactViewModel viewModel, CancellationToken token = default)
{
    var currentUser = await userService.GetUserAsync(User);
    if (currentUser.Id == 0) { return Challenge(); }
    if (!ModelState.IsValid) { return View(viewModel); }

    var contact = mapper.Map<Contact>(viewModel);
    contact.GroupId = activeGroupContext.RequireActiveGroupId();
    contact.CreatedByUserId = currentUser.Id;

    await contactService.AddAsync(contact, croppedImageData, token);
    return RedirectToAction(nameof(Index));
}
```
Adapt directly for `EventsController.Create` (no image handling needed, no `CreatedByUserId` per D-05, redirect target is `CalendarController.Index(year, month)` per D-20 instead of `Index`).

### Two-group cross-tenant integration test shape (verified pattern from this codebase)
```csharp
// Source: QuestBoard.IntegrationTests/Tests/TenantIsolationTests.cs (existing, working code)
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
        // ... seed Group 2 via factory.Database.CreateContext() (ActiveGroupId = null, sees all)
        factory.TestGroupContext.ActiveGroupId = 1;
        // ... assert Group 2's data does not appear in a Group-1-scoped response
    }
}
```
Copy this shape for `EventTenantIsolationTests` (or add cases to the existing file): seed an event with `GroupId = 2`, assert it never appears when `ActiveGroupId = 1`.

### Real mobile User-Agent test shape (verified pattern from this codebase — required by D-16)
```csharp
// Source: QuestBoard.IntegrationTests/Mobile/MobileViewsTests.cs (existing, working code)
private const string MobileUserAgent =
    "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1";

private async Task<(HttpResponseMessage Response, string Html)> GetWithUserAgentAsync(string url, string userAgent)
{
    var request = new HttpRequestMessage(HttpMethod.Get, url);
    request.Headers.TryAddWithoutValidation("User-Agent", userAgent);
    var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
    var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
    return (response, html);
}
```
Use this exact helper (or the existing one) to verify the mobile agenda actually renders the new event entries and the widened filter/empty state — devtools emulation alone does not exercise the `.Mobile.cshtml` view-selection path.

### DateOnly/TimeOnly entity property declaration (EF Core 10 native mapping)
```csharp
// New pattern for this codebase — standard EF Core 8+/10 usage, no package needed
[Table("Events")]
public class EventEntity : IEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    [StringLength(200)] // matches QuestEntity.Title's convention; final length is Claude's Discretion
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; } // unbounded, no [StringLength] — matches QuestEntity.Description (D-06)

    public DateOnly Date { get; set; }
    public TimeOnly? StartTime { get; set; }

    public int? SeriesId { get; set; } // nullable series FK (D-03)

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow; // no CreatedByUserId (D-05)

    public int GroupId { get; set; }
    [ForeignKey(nameof(GroupId))]
    public virtual GroupEntity Group { get; set; } = null!;
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|---------------|--------|
| `DateTime`/`TimeSpan` scaffolding for SQL `date`/`time` columns, requiring `ErikEJ.EntityFrameworkCore.SqlServer.DateOnlyTimeOnly` for EF Core 6/7 to use `DateOnly`/`TimeOnly` | Native `DateOnly` → `date`, `TimeOnly` → `time` mapping, no package | EF Core 8.0 [CITED: learn.microsoft.com/ef/core/what-is-new/ef-core-8.0/breaking-changes] | This project is on EF Core 10.0.9 — the native path applies directly; do not add the older community package |

**Deprecated/outdated:** The `ErikEJ.EntityFrameworkCore.SqlServer.DateOnlyTimeOnly` NuGet package (targeted EF Core 6/7) is obsolete for this project's EF Core 10 and must not be added.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | AutoMapper's currently-pinned version needs no bump for the new Event/EventSeries/EventSignup profile entries | Standard Stack | Low — AutoMapper's `CreateMap` API is stable across the versions likely in use; worst case is a straightforward version-mismatch compile error caught immediately at build time |

**If this table is empty:** N/A — one low-risk assumption logged above; everything else in this research was verified directly against the codebase (grep/read) or cited from Microsoft's own EF Core documentation.

## Open Questions

1. **Should `EventsController.GetEventsForCalendarAsync` fetch all events (like `QuestRepository.GetQuestsForCalendarAsync` fetches all quests, unfiltered by date) or scope to a date range?**
   - What we know: The existing Quest calendar read fetches every quest for the active group with no date-range `.Where()` — `CalendarViewModel.GetCalendarDays()` does the month-filtering client-side in memory.
   - What's unclear: Whether that pattern remains fine at event-table scale once Phase 76 starts materializing recurring occurrences (potentially hundreds of rows per board).
   - Recommendation: Match the existing convention for Phase 74 (fetch-all is simplest and consistent) — flag date-range scoping as a Phase 76 concern once occurrence volume is real, not a Phase 74 concern.

2. **Exact redirect-to-month mechanics for D-20 when an event's `Date` is a `DateOnly` but `CalendarController.Index(int? year, int? month)` takes ints.**
   - What we know: `CalendarController.Index` already accepts nullable `year`/`month` ints; `DateOnly` exposes `.Year`/`.Month` directly, so `RedirectToAction("Index", "Calendar", new { year = event.Date.Year, month = event.Date.Month })` requires no new conversion helper.
   - What's unclear: Nothing substantial — flagged only so the planner doesn't invent unnecessary conversion code for this one call site.
   - Recommendation: Use `DateOnly.Year`/`.Month` directly at the controller boundary; this is the D-01 "single well-named conversion seam" in miniature, not a new pattern.

## Environment Availability

Skipped — this phase has no external tool/service dependencies beyond the already-configured SQL Server / EF Core stack. All work is code, migration, and Razor view changes within the existing solution.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit v3.2.2 + FluentAssertions v8.10.0 + NSubstitute v5.3.0 [CITED: .planning/codebase/TESTING.md] |
| Config file | `QuestBoard.IntegrationTests/xunit.runner.json` (serial execution) |
| Quick run command | `dotnet test --filter "FullyQualifiedName~Event"` |
| Full suite command | `dotnet test` |

### Phase Requirements → Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| EVENT-01 | DM creates event with title/description/date/optional start time | integration | `dotnet test --filter "FullyQualifiedName~EventsControllerIntegrationTests.Create"` | ❌ Wave 0 |
| EVENT-02 | DM edits/deletes own-board events; scoped, never cross-board | integration | `dotnet test --filter "FullyQualifiedName~EventTenantIsolationTests"` | ❌ Wave 0 |
| EVENT-03 | Desktop calendar renders events distinct from quests | integration (HTML assertion) | `dotnet test --filter "FullyQualifiedName~CalendarControllerIntegrationTests"` | ✅ (extend existing file) |
| EVENT-04 | Mobile calendar renders event-only days | integration (real mobile UA) | `dotnet test --filter "FullyQualifiedName~MobileViewsTests"` | ✅ (extend existing file) |
| EVENT-05 | Quest creation provably unaffected by events | integration (negative assertion) | `dotnet test --filter "FullyQualifiedName~QuestControllerIntegrationTests_Comprehensive"` or a new focused test | ✅ (extend) or ❌ Wave 0 |
| EVENT-06 | "Create Event" navbar entry, DM roles only | integration | `dotnet test --filter "FullyQualifiedName~LayoutNavigationTests"` | ✅ (extend existing file) |
| D-09 | Quest Details page renders zero event markup even with a same-day event | integration | new test in `QuestDetailsCharacterControlTests.cs`-style file or a new `EventCalendarPartialTests.cs` | ❌ Wave 0 |

### Sampling Rate
- **Per task commit:** `dotnet test --filter "FullyQualifiedName~Event"`
- **Per wave merge:** `dotnet test`
- **Phase gate:** Full suite green before `/gsd-verify-work`

### Wave 0 Gaps
- [ ] `QuestBoard.IntegrationTests/Controllers/EventsControllerIntegrationTests.cs` — covers EVENT-01, EVENT-02
- [ ] `QuestBoard.IntegrationTests/Tests/EventTenantIsolationTests.cs` (or extend `TenantIsolationTests.cs`) — covers EVENT-02's cross-board clause, D-22
- [ ] A D-09 structural-protection test asserting no event markup renders on `Quest/Details` with a same-day event — likely lands in `CalendarControllerIntegrationTests.cs` or a new file alongside it
- [ ] EVENT-05's "quest creation provably unaffected" negative-assertion test — likely a new focused test rather than reusing the large `_Comprehensive` file
- [ ] Framework install: none — xUnit/FluentAssertions/NSubstitute/InMemory provider all already referenced

## Security Domain

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | no | Unchanged — `[Authorize]` on controller, existing Identity pipeline |
| V3 Session Management | no | Unchanged |
| V4 Access Control | yes | `[Authorize(Policy = "DungeonMasterOnly")]` on Create/Edit/Delete (matches `ContactsController`); `Details` open to all authenticated board members (D-10) |
| V5 Input Validation | yes | `[Required]`/`[StringLength]` DataAnnotations on `EventViewModel` (Title required, bounded; Description unbounded Markdown per D-06); server-side `ModelState.IsValid` check before any write |
| V6 Cryptography | no | Not applicable — no secrets, tokens, or cryptographic material in this phase |

### Known Threat Patterns for this stack

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Cross-tenant data leakage on read (Group A sees Group B's events) | Information Disclosure | EF Core `HasQueryFilter`, fail-closed on null `ActiveGroupId` (D-04) — this app has shipped two real leaks of exactly this shape (Phases 49/55), so this is not theoretical |
| Cross-tenant data leakage on write (event inserted with wrong `GroupId`, or pointing at another board's series) | Tampering / Elevation of Privilege | Explicit `activeGroupContext.RequireActiveGroupId()` stamp on Create; explicit series-`GroupId` equality check before save (D-21) — `HasQueryFilter` provides zero protection here |
| Stored XSS via event description Markdown | Tampering | `IMarkdownService.RenderToHtml()` sanitizes before render — same mechanism already trusted for `Quest.Description`; do not build a second rendering path (D-06) |
| CSRF on Create/Edit/Delete POSTs | Tampering | `[ValidateAntiForgeryToken]` on every POST action, matching `ContactsController` exactly |
| Authorization bypass on Delete/Edit (a Player calling the DM-only endpoint directly) | Elevation of Privilege | `[Authorize(Policy = "DungeonMasterOnly")]` is the actual boundary (not a client-side `CanManage` flag) — mirrors the explicit comment in `ContactsController.IsDmTierAsync()` that the policy attribute, not the display flag, is the security boundary |

## Sources

### Primary (HIGH confidence)
- `QuestBoard.Repository/Entities/QuestBoardContext.cs` (full file read) — global query filter shapes, fail-closed comment block, all existing `GroupId`-scoped entity patterns
- `QuestBoard.Repository/Entities/ContactEntity.cs`, `QuestBoard.Repository/ContactRepository.cs`, `QuestBoard.Domain/Services/ContactService.cs`, `QuestBoard.Service/Controllers/Contacts/ContactsController.cs` — the direct structural template for this phase
- `QuestBoard.Repository/Migrations/20260706193921_AddContactsFeature.cs` — multi-table additive migration precedent
- `QuestBoard.IntegrationTests/Tests/TenantIsolationTests.cs`, `QuestBoard.IntegrationTests/WebApplicationFactoryBase.cs`, `QuestBoard.IntegrationTests/Helpers/MutableGroupContext.cs` — D-22 test template and its blind-spot rationale
- `QuestBoard.Service/Views/Shared/_Calendar.cshtml`, `QuestBoard.Service/Views/Calendar/Index.cshtml`, `QuestBoard.Service/Views/Calendar/Index.Mobile.cshtml`, `QuestBoard.Service/ViewModels/CalendarViewModels/{CalendarViewModel,CalendarDay,QuestOnDay}.cs` — calendar rendering integration points
- `QuestBoard.Service/wwwroot/css/calendar.css`, `calendar.mobile.css` — the row-height pitfall (Pitfall 1) and CSS class conventions to extend
- `QuestBoard.IntegrationTests/Mobile/MobileViewsTests.cs` — real mobile User-Agent test pattern for D-16
- `QuestBoard.Domain/Interfaces/IMarkdownService.cs`, `QuestBoard.Service/Views/Shared/_MarkdownEditor.cshtml` — Markdown reuse contract for D-06
- `.planning/codebase/STACK.md`, `.planning/codebase/TESTING.md` — EF Core 10.0.9 / InMemory 10.0.9 / xUnit v3 version confirmation

### Secondary (MEDIUM confidence)
- [Breaking changes in EF Core 8.0 (EF8) - Microsoft Learn](https://learn.microsoft.com/en-us/ef/core/what-is-new/ef-core-8.0/breaking-changes) — confirms native `DateOnly`→`date`/`TimeOnly`→`time` mapping starting EF Core 8, no package needed for this project's EF Core 10

### Tertiary (LOW confidence)
- None — every claim in this research was either verified directly against the codebase or cited from official Microsoft documentation.

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - zero new packages; every library already in use and version-confirmed against STACK.md
- Architecture: HIGH - direct structural precedent (`Contact` stack) exists in the same repo for every decision in CONTEXT.md
- Pitfalls: HIGH - the row-height/overflow issue (Pitfall 1) was found by directly reading `calendar.css`, not inferred

**Research date:** 2026-08-26
**Valid until:** 30 days (stable internal stack, no fast-moving external dependency)
