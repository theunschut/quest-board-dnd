# Phase 77: Availability Overview Page - Research

**Researched:** 2026-08-29
**Domain:** ASP.NET Core 10 MVC + EF Core 10 aggregating read-path (query shape, tenant isolation), read-only Razor view
**Confidence:** HIGH

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

### The cell — telling a real answer from an untouched default (EVTVIEW-02)

- **D-01: An untouched Campaign default renders as a *muted* Yes chip; a confirmed answer renders solid.** Not a neutral mark. The distinction is deliberately one of emphasis rather than of meaning, because on a Campaign board the stored Yes is not a lie — that member *will* be counted as available if nobody chases them. The cell must say both things: "counted as available" and "nobody confirmed it".

  Rejected: a neutral dash claiming no vote at all (loses the fact that they are counted); a Yes chip with a `?` badge (busiest option, in the densest region of the page).

- **D-02: The muted state must carry a second signal that survives greyscale.** Fill weight alone is a difference of degree and is the exact thing a colour-blind or low-contrast viewer misses — which would defeat EVTVIEW-02 entirely for that viewer. Add a shape or text cue alongside the fill: a dotted border, an italic label, a small icon. Exact choice is the planner's, but *something non-colour* is mandatory, not advisory.

  A page legend is welcome in addition, but is **not** a substitute — a legend only helps the reader who looks at it.

- **D-03: Campaign-untouched and One-Shot-no-row must look different from each other.** They mean genuinely different things: the Campaign default stores Yes and is counted; a One-Shot member with no row stores nothing and is counted nowhere. Flattening them into one "hasn't answered" look would erase a real difference on the page whose whole job is to stop one availability state being mistaken for another.

  So the cell vocabulary has **four** states, not three: confirmed Yes / Maybe / No, muted-Yes (Campaign default), and empty (no row).

- **D-04: `HasAnswered` is the only input to this distinction.** `EventSignup.HasAnswered => UpdatedAt != null` already ships. Phase 75 D-11 is explicit that no consumer — this phase named directly — reads the raw timestamp for this purpose. Do not re-derive it, do not branch on board type to compute it: it is uniform across both board types by construction (Phase 75 D-10).

### The count (EVTVIEW-03)

- **D-05: The headline figure is total Yes *including* untouched defaults.** The big, glanceable number answers "who is expected at this session", which is what a DM planning a date actually needs — and on a Campaign board the default genuinely *is* the plan until someone changes it.

- **D-06: The confirmed portion is shown alongside it as secondary detail.** The headline is the figure that can mislead, so it never stands alone. Both facts are on the row: how many are counted, and how much of that anyone vouched for. This is what keeps D-05 from reintroducing the "yes by default read as a real answer" risk one line above the cells that just fixed it.

- **D-07: Maybe is counted and shown *separately*, never folded into Yes and never omitted.** A session with 2 Yes and 4 Maybe is not a dead date, and it is not a healthy one either — collapsing them would destroy the distinction the three-value vote exists to record. No is not counted anywhere; it is visible in its cell.

- **D-08: Format is the planner's call, but all three figures must be readable in one glance.** That is up to four numbers per row (total yes, confirmed, maybe — plus board size if the planner wants a denominator). If a compact format cannot carry them without becoming noise, the *format* gives way, not D-05/D-06/D-07.

### The window — what "upcoming" means

- **D-09: A fixed count of events — the next N — not a date window and not everything.** Phase 76 maintains a runway of 20 live future occurrences *per series*, so a Campaign board running two series already holds 40+ future events and that number grows with every series added. A date window would let page width be set by data the page does not control; a fixed N bounds it.

  **Accepted cost, stated:** N events means different amounts of real time on different boards — a few weeks on a weekly series, most of a year on a monthly one.

- **D-10: Paging / "show more" to reach beyond the first N.** Nothing in the runway is unreachable from this page. Chosen over "N is the page" deliberately: a DM planning a session that sits past the window is a real case, and sending them to the calendar to do it defeats the point of an overview.

  **Note for the planner:** this is a second query shape on the page the ROADMAP already flags for N+1 risk across events × members × signups. Design the paging into the aggregating query, don't bolt it on.

- **D-11: The lower bound is `Date >= today`, date-only, with no time-of-day comparison.** Today's event stays on the page all day, including after its start time. This matches Phase 75 D-17 exactly and is the reason Phase 74 D-01 chose `DateOnly` — it makes the time-of-day boundary bug structurally impossible. Use `DateOnly.FromDateTime(DateTime.Today)`.

  Rejected: dropping an event once `StartTime` passes. It reintroduces the comparison `DateOnly` was chosen to avoid, and `StartTime` is nullable — an all-day event has nothing to compare, so it would need its own second rule.

- **D-12 (inherited, locked upstream): cancelled occurrences are excluded.** Phase 76 D-14 keeps a cancelled occurrence's signup rows alive, so a naive join renders a cancelled date as a session everyone agreed to. `Event.IsCancelled` is get-only on the domain model. This is not a preference — it is the consequence Phase 76's context explicitly hands to this phase, and it applies to both EVTVIEW-01's grid and EVTVIEW-03's counts.

### The member axis

- **D-13: On a One-Shot board, only members who hold a signup row appear.** No membership query, no "hasn't answered yet" group. This follows Phase 75 D-03 exactly rather than diverging from it, and keeps the page to a single query family.

  **Accepted cost, inherited from D-03 and now spread across the whole page:** on a One-Shot board you cannot tell "nobody else has looked" from "this is a small board", at a glance, across every upcoming event at once.

- **D-14: On a Campaign board the axis is every member, by construction rather than by query.** Phase 75 D-15/D-19 make the fan-out atomic with event creation and with joining, so "is a member" implies "has rows". The page does not need to verify this and must not query membership to double-check it.

- **D-15: The empty cell — a member on the axis who has no row for one particular event — is the planner's call.** It arises even under D-13: a One-Shot member who answered event A but not event B is on the axis with a hole at B. Genuinely blank or an explicit "not answered" mark are both fine; it must simply be distinguishable from the muted Yes (D-03). Judge it with all four cell states side by side.

### Visibility

- **D-16: Every board member can reach the page. No DM gate, no split rendering.** This settles the open question the ROADMAP flags for this phase, in the direction Phase 75 D-02 anticipated. The same data is already visible one event at a time on each `Events/Details` page, so gating the aggregate would make the same fact public per-event and restricted in aggregate — a rule that is hard to explain and easy to get wrong later.

  Rejected: DM-only (diverges from D-02 for identical data); all-members-with-DM-extras (two renderings of one page to build and test, for no requirement).

  **Accepted cost, stated:** aggregating makes patterns legible that the per-event views did not — who always says no, who never answers.

### Mobile

- **D-17: Mobile is a separate view rendering per-event cards, not the grid.** A real `.Mobile.cshtml`, selected by user agent the way `Calendar/Index.Mobile.cshtml` is. The matrix does not survive a phone screen, and the app's card idiom is native there.

  **Note:** mobile views in this app are **user-agent selected**, not breakpoint-driven. Browser devtools emulation will never exercise this view — verification needs a real mobile user agent.

- **D-18: Each card leads with the counts; the per-member breakdown sits behind a tap.** Scrolling the mobile list is then a scan of availability figures, which is what keeps EVTVIEW-03 ("obvious at a glance") working on a small screen. Rejected: full member list always visible (a card as tall as the board is large, and the counts stop being glanceable); counts-only with names only on the event page (mobile would then show availability per player nowhere on this page).

  **Accepted cost, stated:** EVTVIEW-01's "grid of events against players" is one tap away on mobile rather than on screen.

### Entry point and navigation

- **D-19: On desktop, the existing Calendar nav entry becomes a dropdown holding both Calendar and this page.** Not a new sibling top-level entry. `_Layout.cshtml` already has the dropdown pattern (the user menu, `_Layout.cshtml:179`), so this costs no new idiom there.

- **D-20: On mobile, two flat sibling entries instead — no dropdown.** `_Layout.Mobile.cshtml` contains **zero** `dropdown` occurrences; it is a flat offcanvas list. Introducing Bootstrap dropdown behaviour inside an offcanvas that has its own dismiss handling would be a first-of-its-kind interaction in that layout, on touch, for no gain — the offcanvas is already a vertical list.

- **D-21: The calendar page also links across to the overview.** Both entry points, deliberately: the nav for discovery, the calendar link because that is where someone already thinking about dates is standing.

- **D-22: Both nav changes sit under the existing resolved-board-type gate**, unchanged: `activeBoardType is BoardType.OneShot or BoardType.Campaign` (`_Layout.cshtml:168`, `_Layout.Mobile.cshtml:144`). Do not add a role condition — D-16 makes this an all-members page.

  **Verified, not assumed:** `LayoutNavigationTests` asserts on the *string* `"Calendar"` via `Contain`/`NotContain`, not on markup structure, so restructuring the entry into a dropdown keeps all four existing cases green. New cases are still needed for the new entry itself. Note that Phase 76 plan `76-14` fought a navigation regression in this exact block — treat it with care.

### Interaction

- **D-23: The page is read-only. It exposes no write path.** Availability is changed where Phase 75 D-01 put it: the event's Details page. This keeps that decision true, keeps the ownership rule (Phase 75 D-09) in one place, and keeps this phase's only hard problem the aggregating query.

- **D-24: The whole event row is a click target through to that event's Details page**, where the existing three-way vote control already lives. So the page is read-only without being a dead end. On mobile (D-17) the card is the equivalent target.

- **D-25: Events are rows, members are columns.** The row is then a natural full-width click target for D-24, and the unit of the page matches the mobile cards exactly — same unit, same order, both surfaces, no mental transposition when switching devices.

  **Accepted cost, stated:** page width now scales with board size rather than with the paged event count. A large board pushes the member columns wide. If that becomes the binding constraint in practice, revisit D-25 — but do not silently transpose, because D-24 and D-17 both depend on the event being the row.

### Tenant scoping (EVTVIEW-04) — non-negotiable

- **D-26: No `IgnoreQueryFilters()` on this page, in any form.** The ROADMAP names this as the phase's single active risk: *"This page joins across members and signups, which is exactly where `IgnoreQueryFilters()` gets reached for. It must not be."* The `EventSignupEntity` query filter scopes reads through its required `Event` navigation and is fail-closed. That is the mechanism; use it.

  The one existing `IgnoreQueryFilters()` in this area — `GroupRepository.GetEventSignupsForMemberIgnoringActiveBoardAsync` — is `private`, pins `es.Event.GroupId == groupId` in its predicate, and exists for the leave-board cleanup where the acting admin's active board differs from the board being left. It is **not** a precedent for a read path.

- **D-27: A dedicated two-group integration test is mandatory, not optional.** `WebApplicationFactoryBase.TestGroupContext` is a shared singleton `MutableGroupContext` defaulting to `ActiveGroupId = 1`, so the standard integration test is *structurally blind* to this bug class. Follow `QuestBoard.IntegrationTests/Tests/EventAvailabilityTenantIsolationTests.cs` — the Phase 75 sibling of this exact test, closest in shape to what this phase needs. Assert that neither another board's **events** nor its **members** appear, and reset `ActiveGroupId` to `1` in `DisposeAsync`.

- **D-28: The query must avoid an N+1 across events × members × signups.** ROADMAP scope note. `EventSignupRepository.GetRosterForEventAsync` is the single-query eager-include shape to generalise from — one round trip, `.Include(es => es.User)`, ordered in SQL so the view does not re-sort.

### Claude's Discretion

Not discussed — planner decides:

- **The value of N** and whether it is a code constant or configurable. Phase 76 D-114's global runway of 20 (a code default overridable through configuration) is the nearest precedent.
- The exact non-colour cue for D-02, the exact empty-cell treatment for D-15, and the precise count format for D-08 — judged together, with all four cell states side by side.
- The page's name, icon, route, and controller placement. `EventsController` has no `Index` today, and `CalendarController` owns `Index` — either is defensible.
- Whether the page carries a legend, and its copy.
- Empty-state copy when a board has no upcoming events at all.
- Whether the viewer's own column is highlighted or pinned.
- Column ordering on the member axis. `GetRosterForEventAsync` orders alphabetically by name in SQL; matching that costs nothing and keeps column order stable across rows — which it **must** be, or the grid is meaningless.
- Whether paging is a query-string page index or a "show more" that grows the set, and whether the page states how many events sit beyond the window.
- Whether cancelled occurrences get any acknowledgement (e.g. a count of hidden cancelled sessions) or simply vanish. D-12 locks the exclusion; it does not forbid saying so.
- Domain model / repository / service / controller naming, file placement, and the two AutoMapper profile entries.
- Test structure beyond the mandated D-27 isolation test.
- How the SuperAdmin-with-no-active-group case behaves. `EventsController` already carries handling for it (see `EventsControllerIntegrationTests.Details_Get_SuperAdminWithNoActiveGroup_DoesNotThrow`) — mirror whatever it does rather than inventing a third behaviour.

### Deferred Ideas (OUT OF SCOPE)

- **A personal cross-board event agenda** — every upcoming event the logged-in user is expected at, across all boards they belong to, with the board named on each row. Raised by the operator during this discussion. **Promoted to a real roadmap phase (Phase 82) on 2026-08-29** rather than left as a note, at the operator's request. The full analysis — why it cannot be a toggle on this page, why it cannot sit behind the Calendar nav gate, which two safe cross-group mechanisms already exist, and the two questions its own discuss pass must settle — lives in the Phase 82 entry in `.planning/ROADMAP.md`. It is quests-excluded and events-only.
- **Making cells writable** — considered and declined (D-23). Would make Phase 75 D-01's "single availability surface" false and put the ownership rule in a second place. D-24's click-through gives the action path without the write surface. If it is ever wanted, the `SetAvailability` endpoint is already ownership-checked and would be the thing to reuse.
- **An adjustable / configurable window control on the page** — declined under D-09. It asks the reader an implementation question instead of answering one. The value of N being configurable in code is a separate matter and is left to the planner.
- **DM-only extras on a shared page** (e.g. the confirmed/total split shown only to DM-tier) — declined under D-16. Two renderings of one page to build and test, for no requirement.
- **A per-board denominator on the count** ("4 of 6 members") — not decided either way; folded into D-08's format discretion. On a One-Shot board under D-13 there is no meaningful denominator, which is the wrinkle to think about before adding one.

**UI-SPEC.md is also locked** (see `.planning/phases/77-availability-overview-page/77-UI-SPEC.md`) — the exact cell markup (5-state vocabulary), count-summary block markup, desktop grid layout with sticky columns, mobile card layout with collapse-toggle, legend, empty state, "Show More Events" paging control, and nav dropdown/flat-list markup are all specified verbatim there and are not re-litigated by this research. This RESEARCH.md focuses on the parts UI-SPEC.md does not cover: the query shape, the tenant-scoping mechanism, the domain/repository/service/controller wiring, and the test strategy.
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| EVTVIEW-01 | A new page shows upcoming events for the current board as a grid of events against players, with each player's availability | Standard Stack + Architecture Patterns sections define the single bounded aggregate query (`Event` + `Signups.User`, `Date >= today`, non-cancelled, `Take(N)`) and the view model shape that drives the grid/card rendering already specified in UI-SPEC.md |
| EVTVIEW-02 | The overview visually distinguishes an untouched default from an answer the player actually gave | `EventSignup.HasAnswered` is confirmed as the sole, already-shipped input (Code Examples); no new domain logic needed — the cell-state mapping is a pure view-model projection |
| EVTVIEW-03 | The overview shows a per-event availability count, so a poorly-attended date is obvious at a glance | Count aggregation is computed in the view-model mapping layer (in-memory over the already-fetched `Signups` collection, not a second query) — see Architecture Patterns |
| EVTVIEW-04 | The overview never displays events or members from another board | Don't-Hand-Roll + Common Pitfalls sections cover the fail-closed `EventSignupEntity`/`EventEntity` query filters and why `IgnoreQueryFilters()` must never appear on this path; Code Examples includes the `EventAvailabilityTenantIsolationTests.cs` pattern to extend |
</phase_requirements>

## Summary

This phase adds exactly one new bounded read path and two new Razor views on top of infrastructure that Phases 74–76 already fully shipped. There is no new package, no new migration, and no new write path. The entire technical risk is concentrated in one query: fetch the next N non-cancelled events on the active board dated today-or-later, together with every signup row (and the signing member's name) for each, in a single round trip that never calls `IgnoreQueryFilters()`.

The codebase already contains the exact template to generalize from: `EventSignupRepository.GetRosterForEventAsync` (`QuestBoard.Repository/EventSignupRepository.cs:60`) does the single-event version of this join today — one query, `.Include(es => es.User)`, SQL-side ordering, and an explicit comment that the ambient `EventSignupEntity` query filter (scoped through `es.Event.GroupId`) is the correct and sufficient tenant boundary. The new aggregate query is the same shape widened from one event to N events, reached through `EventEntity.Signups` (a real one-to-many navigation, confirmed present) rather than a second `EventSignups` query — `DbContext.Events.Where(...).OrderBy(...).Take(n).Include(e => e.Signups).ThenInclude(s => s.User)`. Because this is a single collection navigation (not two independent ones), it does not trigger the `MultipleCollectionIncludeWarning`/cross-join blowup that `QuestRepository` works around elsewhere with `AsSplitQuery()` — that pattern is documented here as the thing to reach for only if a second independent collection is ever added to this query, not as something this query itself needs.

Both the count aggregation (EVTVIEW-03) and the four/five-state cell classification (EVTVIEW-02) are pure in-memory projections over the one fetched `Signups` collection per event — no second query, no N+1, computed once in the domain-to-view-model mapping layer. `EventSignup.HasAnswered => UpdatedAt != null` is the only signal needed and already ships correctly for both board types by construction (Phase 75 D-10/D-11). Tenant isolation rides entirely on the existing fail-closed `HasQueryFilter` on `EventEntity` (`e.GroupId == activeGroupContext.ActiveGroupId`) and `EventSignupEntity` (`es.Event.GroupId == activeGroupContext.ActiveGroupId`) in `QuestBoardContext.cs` — the phase's only hard rule is to never bypass it, and to prove that with a dedicated two-group integration test modeled directly on `EventAvailabilityTenantIsolationTests.cs`.

**Primary recommendation:** Add one new repository method (`IEventRepository.GetUpcomingWithSignupsAsync(int take, DateOnly today, CancellationToken)` or equivalent) that runs the single `Where` + `OrderBy` + `Take` + `Include(e => e.Signups).ThenInclude(s => s.User)` + `AsNoTracking()` query, do all EVTVIEW-02/03 aggregation in the domain/view-model mapping layer over the already-loaded `Signups` collection, and copy `EventAvailabilityTenantIsolationTests.cs`'s two-group seeding pattern verbatim for the new mandatory isolation test.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Bounded aggregate query (events × signups × members) | Repository (`QuestBoard.Repository`) | — | Single EF Core query is the only place that can satisfy the N+1 constraint (D-28); must live behind `IEventRepository`, the existing seam |
| Tenant scoping enforcement | Repository / EF Core global query filter (`QuestBoardContext`) | — | Already enforced ambiently at the `DbContext` model-build level; the new query must ride it, never bypass it (D-26) |
| Cell-state classification (5-state vocabulary) & count aggregation | Domain (`QuestBoard.Domain.Services` / mapping) or Service→ViewModel mapping | Service (`QuestBoard.Service.Automapper`) | Pure computation over already-loaded data; belongs wherever the domain model → view model translation already happens for `EventSignup`/`Event`, per the two-boundary AutoMapper convention |
| Paging (`take`/`?take=`) | Service (`EventsController`) | Repository (query parameter) | Controller owns the query-string contract (UI-SPEC section 7); repository just accepts a bounded `take` int |
| View rendering (grid, cards, legend, empty state) | Service (`QuestBoard.Service/Views/Events`) | — | Already fully specified in `77-UI-SPEC.md`; no research needed here |
| Nav entry / cross-link | Service (`_Layout.cshtml`, `_Layout.Mobile.cshtml`, `Calendar/Index(.Mobile).cshtml`) | — | Pure Razor/markup change under the existing board-type gate |
| Two-group tenant isolation test | Test (`QuestBoard.IntegrationTests/Tests`) | — | Must exercise the real HTTP pipeline through the real query filter, not a repository unit test with mocks |

## Standard Stack

No new packages. This phase is pure composition of already-referenced libraries.

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Microsoft.EntityFrameworkCore / .SqlServer | 10.0.9 `[VERIFIED: csproj]` | The aggregate query (`Include`/`ThenInclude`/`Take`/`AsNoTracking`) | Already the project's only ORM; version confirmed via `QuestBoard.Repository/QuestBoard.Repository.csproj:10-12` |
| ASP.NET Core MVC | .NET 10 `[VERIFIED: project files / CLAUDE.md]` | Controller action, Razor views | Existing stack, no change |
| AutoMapper | already referenced `[VERIFIED: codebase]` | `Event`↔`EventEntity` and `EventSignup`↔`EventSignupEntity` (Repository boundary); `Event`↔`EventViewModel` and `EventSignup`↔`EventSignupViewModel` (Service boundary) — both already exist and need only new members or a new view model class | Confirmed at `QuestBoard.Repository/Automapper/EntityProfile.cs:138-165` and `QuestBoard.Service/Automapper/ViewModelProfile.cs:104-154` |
| xUnit v3.2.2 + FluentAssertions 8.10.0 | `[VERIFIED: csproj + TESTING.md]` | The mandatory D-27 isolation test and any unit tests | Existing test stack, `TestContext.Current.CancellationToken` xUnit v3 idiom already used throughout |
| Bootstrap 5.3.0 + FontAwesome | `[VERIFIED: 77-UI-SPEC.md]` | All view markup (badges, dropdown, collapse, sticky table) | Already loaded on both layouts; zero new front-end dependency |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| `Microsoft.Extensions.Options` (`IOptions<T>`) | already referenced | If N (the page size / `take` default) becomes a configurable code default rather than a literal constant | Mirrors the exact `EventSeriesOptions` pattern already shipped (`QuestBoard.Domain/Extensions/ServiceExtensions.cs:14-17`) — recommended over a hardcoded literal so a future deployment can tune it without a code change |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Single `Include(e => e.Signups).ThenInclude(s => s.User)` query | `AsSplitQuery()` with two separate `Include`s | Not needed here — this query has exactly one collection navigation (`Signups`), so there is no multi-collection cross-join to split. `AsSplitQuery()` is the right tool only if a second independent collection (e.g. a future `Event.Comments`) is added later; using it here would add an unnecessary second round trip |
| Repository-level aggregation of counts | Aggregate counts in SQL via `.Select(new {...Count...})` projection | Rejected: `HasAnswered`/vote-count logic is domain logic (D-04 forbids re-deriving `HasAnswered`, keeps board-type-agnostic logic in one place), and the same `Signups` collection is already fully materialized for the grid — computing counts from it in C# costs nothing extra and keeps the classification logic testable as plain C# rather than as SQL translation |
| A new `IEventOverviewRepository` | Add methods to existing `IEventRepository` | Either is defensible per CONTEXT's discretion note; existing precedent (`IEventRepository`, `IEventSeriesRepository`) favors one repository per aggregate root, so extending `IEventRepository` (aggregate root = `Event`) is the more consistent choice, not a hard requirement |

**Installation:** None — no new packages.

**Version verification:** `QuestBoard.Repository.csproj:10-12` confirms `Microsoft.EntityFrameworkCore` / `.SqlServer` / `.Design` all pinned to `10.0.9`. `QuestBoard.IntegrationTests.csproj:13,17,21` confirms `FluentAssertions 8.10.0`, `xunit.v3 3.2.2`. No registry lookup needed — nothing is being added.

## Package Legitimacy Audit

Not applicable — this phase installs no new packages (confirmed by reading `QuestBoard.Repository.csproj` and `QuestBoard.Service.csproj`; every library used is already referenced). No `npm view`/`pip index`/`cargo search` verification needed. The planner should not add a `checkpoint:human-verify` task for dependencies on this phase.

## Architecture Patterns

### System Architecture Diagram

```
Browser (desktop or mobile UA)
        │  GET /Events?take=20   (or GET /Calendar/Index → click "Availability Overview")
        ▼
EventsController.Index(int take, CancellationToken)
        │
        │  1. resolve board type / active group (existing IBoardTypeResolver, IActiveGroupContext)
        │  2. IsDmTierAsync/GetEffectiveRoleAsync are NOT called — D-16 gates on auth only
        ▼
IEventService (or IEventOverviewService) . GetUpcomingOverviewAsync(take, token)
        │
        ▼
IEventRepository . GetUpcomingWithSignupsAsync(today, take, token)
        │
        │   DbContext.Events                              ← EventEntity query filter fires here
        │       .Where(e => e.Date >= today && e.CancelledAt == null)
        │       .OrderBy(e => e.Date).ThenBy(e => e.StartTime)
        │       .Take(take)
        │       .Include(e => e.Signups)                   ← EventSignupEntity query filter fires here too
        │           .ThenInclude(s => s.User)
        │       .AsNoTracking()
        │       .ToListAsync(token)                         ONE round trip
        ▼
IList<Event>  (domain models, each with its Signups navigation populated)
        │
        │  AutoMapper: EventEntity→Event, EventSignupEntity→EventSignup (Repository boundary, existing)
        ▼
Domain/mapping layer: per event, classify each member's cell (5-state) and compute
  Yes-including-defaults / confirmed / maybe counts — pure C#, no further query
        │
        │  AutoMapper: Event→EventOverviewRowViewModel, EventSignup→cell state (Service boundary, new members)
        ▼
EventsController returns View(viewModel)
        │
        ▼
Events/Index.cshtml (desktop grid)  or  Events/Index.Mobile.cshtml (card list, UA-selected)
```

### Recommended Project Structure
```
QuestBoard.Domain/
├── Interfaces/
│   └── IEventRepository.cs          # + GetUpcomingWithSignupsAsync (or new overview method)
├── Models/
│   └── (Event, EventSignup unchanged — HasAnswered already correct)
QuestBoard.Repository/
├── EventRepository.cs               # + implementation of the new bounded query
QuestBoard.Service/
├── Controllers/Events/
│   └── EventsController.cs          # + Index action (no existing Index today)
├── ViewModels/EventViewModels/
│   └── (new) EventOverviewViewModel.cs, EventOverviewRowViewModel.cs, MemberCellViewModel.cs
├── Views/Events/
│   ├── (new) Index.cshtml           # desktop grid — markup fully specified in 77-UI-SPEC.md
│   └── (new) Index.Mobile.cshtml    # mobile cards — markup fully specified in 77-UI-SPEC.md
├── Views/Shared/
│   ├── _Layout.cshtml               # nav dropdown edit (D-19)
│   └── _Layout.Mobile.cshtml        # nav flat-entry edit (D-20)
├── Views/Calendar/
│   ├── Index.cshtml                 # cross-link edit (D-21)
│   └── Index.Mobile.cshtml          # cross-link edit (D-21)
├── Automapper/
│   └── ViewModelProfile.cs          # + Event→EventOverviewRowViewModel, EventSignup→cell mapping
QuestBoard.IntegrationTests/Tests/
└── (new) EventsOverviewTenantIsolationTests.cs   # the mandatory D-27 two-group test
```

### Pattern 1: Single-query bounded aggregate with one collection navigation
**What:** Fetch N parent rows with their full child collection eagerly loaded, in one SQL round trip, using `Take` + a single `Include(...).ThenInclude(...)` chain (no `AsSplitQuery()` needed because there is exactly one collection navigation being included).
**When to use:** Any read that needs "N parents, each with all of one child collection" — exactly this phase's shape (N events, each with all its signups).
**Example:**
```csharp
// Source: generalizes QuestBoard.Repository/EventSignupRepository.cs:60-73 (GetRosterForEventAsync)
// and QuestBoard.Repository/EventRepository.cs:13-25 (GetEventsForCalendarAsync) —
// both already-shipped single-query patterns in this exact repository.
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

### Pattern 2: In-memory aggregation over an already-loaded collection (no second query)
**What:** Compute per-row counts (EVTVIEW-03) and per-cell classification (EVTVIEW-02) from the `Signups` collection that Pattern 1 already loaded — plain LINQ-to-objects, not a second database round trip.
**When to use:** Any derived figure that can be computed from data already in memory. Never re-query for a count that the aggregate fetch already has the rows for.
**Example:**
```csharp
// Illustrative shape for the mapping/service layer -- not existing code, based on the
// already-shipped EventSignup.HasAnswered signal (QuestBoard.Domain/Models/EventSignup.cs:25)
// and the VoteType enum {No=0, Maybe=1, Yes=2} reused as-is per CONTEXT.
var yesCount = eventSignups.Count(s => s.Availability == VoteType.Yes);       // D-05: includes untouched defaults
var confirmedCount = eventSignups.Count(s => s.Availability == VoteType.Yes && s.HasAnswered); // D-06
var maybeCount = eventSignups.Count(s => s.Availability == VoteType.Maybe);   // D-07, never folded into Yes

// Per-member cell state (5 states, D-01/D-02/D-03):
//   member has no row at all              -> Empty
//   row exists, HasAnswered == false, Yes -> MutedYesDefault   (Campaign auto-signup, D-04)
//   row exists, HasAnswered == true        -> ConfirmedYes / ConfirmedMaybe / ConfirmedNo by Availability
```

### Pattern 3: Options-bound code default for a tunable constant
**What:** Bind a small options class to configuration with a code default, so N can be tuned per deployment without a code change.
**When to use:** If the planner chooses a configurable N over a literal constant (CONTEXT leaves this to discretion).
**Example:**
```csharp
// Source: QuestBoard.Domain/Extensions/ServiceExtensions.cs:14-17 — the exact precedent
// ("A code default (runway 20, preview 10) keeps the feature working on a deployment with
// no matching configuration section") already shipped for EventSeriesOptions.
services.AddOptions<EventSeriesOptions>().BindConfiguration(EventSeriesOptions.SectionName);
```

### Anti-Patterns to Avoid
- **`IgnoreQueryFilters()` anywhere on this read path:** The ROADMAP names this as the phase's single named risk. The only existing usage in the codebase (`GroupRepository.GetEventSignupsForMemberIgnoringActiveBoardAsync`) is private, pins the group id explicitly in its own predicate, and exists for a completely different purpose (leave-board cleanup). It is not a template for a read path and must not be copied.
- **Re-querying membership on a Campaign board to build the member axis:** Phase 75 D-15/D-19 guarantee every Campaign member already has a signup row per event by construction. Querying `UserGroups`/membership to "double check" this is unnecessary work and a second query family the CONTEXT explicitly forbids (D-14).
- **Re-deriving `HasAnswered` from `UpdatedAt` timestamp comparisons or board-type branches:** D-04 is explicit — `HasAnswered` is already correct and uniform across both board types. Recomputing it (e.g. comparing `UpdatedAt` to `CreatedAt`) risks subtly diverging from the shipped definition.
- **Two independent `Include`s that would multiply row count:** Not a risk in the recommended single-collection query above, but if a future addition needs a second independent collection off `Event` (unlikely in this phase), reach for `AsSplitQuery()` immediately rather than accepting a cross-join — this is exactly the situation `QuestRepository.cs:89-105` and `:322-354` already solved and documented in-line.
- **Transposing the grid (members as rows, events as columns) for width reasons:** D-25 explicitly forbids this without revisiting the decision — D-24 (row = click target) and D-17 (mobile card = event) both depend on the event being the row.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Tenant isolation on a joined read | A manual `GroupId` filter/parameter threaded through the new repository method | The existing ambient `HasQueryFilter` on `EventEntity` and `EventSignupEntity` in `QuestBoardContext.cs:448-465` | It is already fail-closed (`ActiveGroupId != null && ...`), already tested, and is the one mechanism every other `IEventRepository`/`IEventSignupRepository` method relies on. A hand-rolled parallel filter is exactly the kind of thing that can drift out of sync with the model-level one |
| "Has this member confirmed?" logic | A new computed property or a raw `UpdatedAt != null` check re-typed at the call site | `EventSignup.HasAnswered` (already exists) | Phase 75 D-11 states no consumer should re-derive this; using the existing property is both less code and the only way to stay consistent if the definition ever needs to change |
| Cross-board leak proof | Trusting HTTP status codes alone, or trusting the query filter without a test | A dedicated two-group integration test seeded through the *unfiltered* seeding `DbContext` (bypassing the request-pipeline's active-group context entirely), following `EventAvailabilityTenantIsolationTests.cs` | `WebApplicationFactoryBase.TestGroupContext` defaults every test to `ActiveGroupId = 1`, so an ordinary test is structurally blind to a leak — it would need to already be looking at the wrong board to notice one |

**Key insight:** Every piece of infrastructure this phase needs — the fail-closed query filter, the single-query eager-include shape, the `HasAnswered` signal, the SuperAdmin-no-active-group handling, the options-bound configurable constant — was already built and proven in Phases 74–76. This phase's job is composition, not invention; any new abstraction beyond "one repository method, one view-model mapping, two views, two nav edits, one isolation test" should be treated with suspicion.

## Common Pitfalls

### Pitfall 1: `Take()` + collection `Include` producing unexpected row counts
**What goes wrong:** Combining `.Take(n)` on the outer query with `.Include()` of a one-to-many collection can, in older EF Core versions or with non-deterministic ordering, apply the limit to the joined row set rather than to the number of distinct parent entities, silently truncating events with many signups before events with few.
**Why it happens:** EF Core needs a stable `ORDER BY` to correctly window/paginate the outer entity before materializing the collection split (EF Core 5+ handles this correctly via row-numbering in a single query, but only when the ordering is deterministic).
**How to avoid:** Always pair `.Take(n)` with a fully deterministic `OrderBy(...).ThenBy(...)` on columns that produce a stable sort (date, then start time, then a tiebreaker like `Id` if two events can share the exact same date+time) — mirroring `EventRepository.GetEventsForCalendarAsync`'s existing `OrderBy(e => e.Date).ThenBy(e => e.StartTime)`.
**Warning signs:** An event with many signups appears to "use up" more of the `take` budget than an event with none; the returned event count is not exactly `min(take, available)`.

### Pitfall 2: Forgetting `EventEntity.IsCancelled` exclusion in the new query (re-litigating D-12)
**What goes wrong:** A naive `Where(e => e.Date >= today)` alone will include cancelled occurrences, whose signup rows Phase 76 deliberately keeps alive — rendering a cancelled date as a session everyone agreed to attend.
**Why it happens:** `IsCancelled` is a derived get-only property (`CancelledAt != null`), not a stored column, so it's easy to forget it needs a `Where` clause on the underlying `CancelledAt` field rather than being naturally excluded.
**How to avoid:** Always include `e.CancelledAt == null` in the same `Where` predicate as the date filter (as shown in Pattern 1 above).
**Warning signs:** A cancelled session that should be invisible still shows up in the grid/cards with a full attendance count.

### Pitfall 3: Computing counts with a second query instead of over the already-loaded collection
**What goes wrong:** A tempting but wasteful pattern is to call something like `eventSignupRepository.GetRosterForEventAsync(eventId)` per row to get counts, reintroducing the exact N+1 the ROADMAP calls out.
**Why it happens:** `GetRosterForEventAsync` already exists and "just works" for a single event, so it's an easy reach when building the per-row count without realizing the aggregate query already fetched the same data.
**How to avoid:** Compute all counts and cell states from the `Signups` collection already loaded by the Pattern 1 query — never call a per-event repository method inside a loop over the fetched events.
**Warning signs:** SQL Profiler / EF logging shows one query per event instead of exactly one query for the whole page.

### Pitfall 4: Treating a resolved `null` board type as "show nothing gracefully" instead of matching existing SuperAdmin/no-active-group handling
**What goes wrong:** A SuperAdmin with no active group, or a request where `IBoardTypeResolver.GetBoardTypeAsync()` returns `null`, could throw (e.g. from `RequireActiveGroupId()`) if the controller assumes an active group always exists.
**Why it happens:** Most of this app's controllers are written assuming a normal board member with an active group; SuperAdmin is a genuine edge case with no active group by design.
**How to avoid:** Mirror `EventsController`'s existing pattern exactly — `EventsControllerIntegrationTests.Details_Get_SuperAdminWithNoActiveGroup_DoesNotThrow` proves the app must not throw (`InternalServerError`) in this state; the new `Index` action should short-circuit the same way (e.g., an empty/none result rather than calling `RequireActiveGroupId()` unconditionally).
**Warning signs:** A 500 error when a SuperAdmin with no active group visits the new page.

## Code Examples

### The existing single-query template to generalize (verified in codebase)
```csharp
// Source: QuestBoard.Repository/EventSignupRepository.cs:60-73
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

### The fail-closed tenant filter this phase must ride, never bypass
```csharp
// Source: QuestBoard.Repository/Entities/QuestBoardContext.cs:448-465
modelBuilder.Entity<EventEntity>()
    .HasQueryFilter(e =>
        activeGroupContext.ActiveGroupId != null &&
        e.GroupId == activeGroupContext.ActiveGroupId);

modelBuilder.Entity<EventSignupEntity>()
    .HasQueryFilter(es =>
        activeGroupContext.ActiveGroupId != null &&
        es.Event.GroupId == activeGroupContext.ActiveGroupId);
```

### The two-group isolation test pattern to extend (D-27)
```csharp
// Source: QuestBoard.IntegrationTests/Tests/EventAvailabilityTenantIsolationTests.cs
// (Roster_ForGroupOneEvent_ContainsOnlyGroupOneMembers, lines 187-212) — the closest sibling.
// The new overview test should follow the same shape: seed a genuine second board through the
// *unfiltered* seeding DbContext (never through the request pipeline, which is pinned to
// ActiveGroupId = 1 by WebApplicationFactoryBase.TestGroupContext), seed a same-named member on
// each board so a leak is visible rather than coincidentally distinguishable, hit the new
// overview endpoint as an authenticated group-1 member, and assert the response contains
// neither the other board's event title nor its member's name. Reset
// factory.TestGroupContext.ActiveGroupId = 1 in DisposeAsync.
[Fact]
public async Task Roster_ForGroupOneEvent_ContainsOnlyGroupOneMembers()
{
    await TestDataHelper.ClearDatabaseAsync(factory.Services);

    var groupOneEventId = await SeedGroupOneEventAsync("Group One Roster Session", DateOnly.FromDateTime(DateTime.Today));
    var (groupOneClient, groupOneUser) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
        factory, "isoavail_roster_member", "isoavail_roster_member@example.com", name: "Shared Display Name");

    var otherEventId = await SeedOtherBoardEventAsync("Group Two Roster Session", DateOnly.FromDateTime(DateTime.Today));
    await SeedSignupAsync(otherEventId, groupId: 2, name: "Shared Display Name");

    factory.TestGroupContext.ActiveGroupId = 1;
    await groupOneClient.PostAsync($"/Events/SetAvailability/{groupOneEventId}",
        new FormUrlEncodedContent(new Dictionary<string, string> { ["availability"] = "Yes" }),
        TestContext.Current.CancellationToken);

    using var scope = factory.Services.CreateScope();
    var signupService = scope.ServiceProvider.GetRequiredService<IEventSignupService>();
    var roster = await signupService.GetRosterForEventAsync(groupOneEventId, TestContext.Current.CancellationToken);

    roster.Should().ContainSingle();
    roster[0].UserId.Should().Be(groupOneUser.Id);
}
```

### The multi-collection split-query precedent (not needed by this phase's single-collection query, kept for reference)
```csharp
// Source: QuestBoard.Repository/QuestRepository.cs:89-105
// Two independent collection Includes (ProposedDates and PlayerSignups) in a single
// query force EF to cross-join both collections, multiplying row count combinatorially
// and triggering the MultipleCollectionIncludeWarning. AsSplitQuery() issues one query
// per collection instead, avoiding the row-count blowup without changing the loaded shape.
var entity = await DbContext.Quests
    .AsSplitQuery()
    .Include(q => q.ProposedDates)
        .ThenInclude(pd => pd.PlayerVotes)
    .Include(q => q.PlayerSignups)
        .ThenInclude(ps => ps.Player)
    .FirstOrDefaultAsync(q => q.Id == id, cancellationToken: token);
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|---------------|--------|
| N/A — no prior overview page existed | This is the first aggregate/cross-cutting read on the Event/EventSignup schema | Phase 77 | Establishes the precedent for how future aggregate reads on this schema should be built (single query, in-memory aggregation, no `IgnoreQueryFilters()`) |

**Deprecated/outdated:** None — this phase builds entirely on infrastructure shipped in the last three phases (74–76), all still current.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | The new repository method belongs on `IEventRepository` rather than a new dedicated interface | Standard Stack / Alternatives Considered | Low — CONTEXT explicitly leaves this to planner discretion; either choice compiles and tests the same way |
| A2 | N's default value (exact integer) is not specified anywhere in CONTEXT or this research | Standard Stack | Low-medium — the planner must pick a concrete default (e.g. 10 or 20); wrong choice only affects page density, not correctness, and D-10's paging control mitigates it either way |

**If this table is empty:** N/A — two low-risk assumptions logged above, both explicitly left to planner discretion by CONTEXT.md itself, not areas where this research asserted unverified fact.

## Open Questions

1. **Exact controller/action home: `EventsController.Index()` vs. a dedicated new controller**
   - What we know: `EventsController` has no `Index` action today; `CalendarController` owns its own `Index`. UI-SPEC.md's navigation markup (section 8) already assumes `asp-controller="Events" asp-action="Index"`.
   - What's unclear: Whether `EventsController` is the best long-term home given it currently only holds single-event CRUD-ish actions (`Details`, `Create`, series management), or whether a page that aggregates across all events deserves a peer.
   - Recommendation: Follow UI-SPEC.md's already-chosen route (`EventsController.Index()`) — it is the more-referenced option across both CONTEXT.md and UI-SPEC.md and requires no further validation.

2. **Whether `IEventSignupService`/`IEventService` needs a new method or whether the controller calls `IEventRepository` through `IEventService` directly**
   - What we know: The existing service layer (`EventService`, `EventSignupService`) is a thin pass-through over the repository layer (see `EventSignupService.GetRosterForEventAsync` at `QuestBoard.Domain/Services/EventSignupService.cs:23-26`, which is a one-line delegation).
   - What's unclear: Whether the count/cell-state aggregation logic (Pattern 2) belongs inside `EventService`/a new domain service method, or in the AutoMapper mapping profile itself.
   - Recommendation: Put the aggregation in the domain service layer (alongside the repository call) rather than in the AutoMapper profile, since AutoMapper profiles in this codebase (per `EntityProfile.cs`/`ViewModelProfile.cs`) are used for straightforward property mapping, not multi-property derived aggregation — keeps aggregation logic unit-testable in isolation from mapping configuration.

## Environment Availability

Skipped — this phase has no new external dependencies (no new package, no new external service, no new CLI tool). Everything needed (.NET 10 SDK, EF Core 10.0.9, SQL Server via Docker) is already the project's baseline and unchanged by this phase.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit v3.2.2 + FluentAssertions 8.10.0 (already configured) |
| Config file | `QuestBoard.IntegrationTests/xunit.runner.json` (serial execution: `parallelizeAssembly: false`) |
| Quick run command | `dotnet test --filter "FullyQualifiedName~EventsOverview" --no-build` |
| Full suite command | `dotnet test` |

### Phase Requirements → Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| EVTVIEW-01 | Grid/card page shows upcoming events × members with availability | integration (HTTP GET, content assertions) | `dotnet test --filter "FullyQualifiedName~EventsOverviewControllerIntegrationTests"` | ❌ Wave 0 — new file |
| EVTVIEW-02 | Untouched-default cell renders distinctly from a confirmed answer | unit (view-model/mapping classification) + integration (markup assertion for the muted-Yes CSS class / `avail-cell-yes-muted`) | `dotnet test --filter "FullyQualifiedName~EventOverviewMapping"` | ❌ Wave 0 — new file(s) |
| EVTVIEW-03 | Per-event count shown (headline + confirmed + maybe) | unit (aggregation logic against a constructed `Signups` list covering all 5 cell states) | `dotnet test --filter "FullyQualifiedName~EventOverviewCounts"` | ❌ Wave 0 — new file |
| EVTVIEW-04 | Never shows another board's events/members | integration (two-group isolation, D-27 mandatory) | `dotnet test --filter "FullyQualifiedName~EventsOverviewTenantIsolationTests"` | ❌ Wave 0 — new file, follow `EventAvailabilityTenantIsolationTests.cs` |

### Sampling Rate
- **Per task commit:** `dotnet test --filter "FullyQualifiedName~EventsOverview" --no-build`
- **Per wave merge:** `dotnet test`
- **Phase gate:** Full suite green before `/gsd-verify-work`

### Wave 0 Gaps
- [ ] `QuestBoard.IntegrationTests/Tests/EventsOverviewTenantIsolationTests.cs` — covers EVTVIEW-04, modeled on `EventAvailabilityTenantIsolationTests.cs`
- [ ] `QuestBoard.IntegrationTests/Controllers/EventsControllerIntegrationTests.cs` (extend existing file) — covers EVTVIEW-01/02/03 happy-path + SuperAdmin-no-active-group + empty-state
- [ ] `QuestBoard.UnitTests/...` (new file for the aggregation/mapping logic) — covers EVTVIEW-02/03 in isolation from HTTP, using constructed `EventSignup` lists to cover all 5 cell states and the Yes/confirmed/maybe count math
- [ ] `QuestBoard.IntegrationTests/Controllers/LayoutNavigationTests.cs` (extend existing file) — new cases for the "Availability Overview" nav entry on both layouts (existing 4 Calendar-string cases stay green per D-22's verified note)
- [ ] No framework install needed — xUnit v3 + FluentAssertions already configured project-wide

## Security Domain

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | no | Page requires `[Authorize]` only (existing scheme); no new auth surface |
| V3 Session Management | no | No session state introduced; paging is stateless query-string (`?take=N`), matching UI-SPEC section 7's explicit rationale |
| V4 Access Control | **yes** | Tenant isolation via EF Core global query filter on `EventEntity`/`EventSignupEntity` (`QuestBoardContext.cs:448-465`) — never `IgnoreQueryFilters()`. No role-based gate is applicable/needed since D-16 makes this an all-authenticated-members page |
| V5 Input Validation | **yes** | The `take`/paging query parameter must be bounds-checked server-side (e.g., clamp to a sane max, reject negative/zero) before being passed to `.Take()`, mirroring `CalendarController.Index`'s existing `year`/`month` range validation (`CalendarController.cs:28-38`) as the in-codebase precedent for validating a query-string int |
| V6 Cryptography | no | No cryptographic operation in this phase |

### Known Threat Patterns for this stack

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Cross-tenant data disclosure via a joined/aggregating query (the ROADMAP's named risk) | Information Disclosure | Fail-closed EF Core global query filter, enforced ambiently at the `DbContext` level; proven by a dedicated two-group integration test (D-27) rather than assumed from the filter's presence alone |
| Unbounded `take` parameter causing excessive query cost | Denial of Service (resource exhaustion) | Server-side clamp on the `take` query parameter to a reasonable maximum (e.g., a small multiple of the code-default N), independent of whatever the client sends |
| SuperAdmin-with-no-active-group triggering an unhandled exception | (Reliability, not directly STRIDE) | Mirror `EventsController`'s existing short-circuit pattern rather than calling `RequireActiveGroupId()` unconditionally (Pitfall 4) |

## Sources

### Primary (HIGH confidence)
- `QuestBoard.Repository/EventSignupRepository.cs:60-73` — `GetRosterForEventAsync`, the single-query template generalized in Pattern 1
- `QuestBoard.Repository/EventRepository.cs:13-70` — `GetEventsForCalendarAsync`, `AddWithCampaignFanOutAsync`, confirming `EventEntity.Signups` navigation and existing scoping comments
- `QuestBoard.Repository/Entities/QuestBoardContext.cs:444-465` — the exact `HasQueryFilter` definitions for `EventEntity` and `EventSignupEntity`
- `QuestBoard.Domain/Models/EventSignup.cs`, `QuestBoard.Domain/Models/Event.cs` — `HasAnswered`, `IsCancelled`, `Date`/`StartTime` shapes
- `QuestBoard.Domain/Interfaces/IEventRepository.cs`, `IEventSignupRepository.cs` — full existing method inventory and XML-doc scoping conventions
- `QuestBoard.IntegrationTests/Tests/EventAvailabilityTenantIsolationTests.cs` — the direct sibling test pattern for D-27
- `QuestBoard.Repository/QuestRepository.cs:89-105, 322-354` — the `AsSplitQuery()` precedent and why it does not apply to this phase's single-collection query
- `QuestBoard.Service/Controllers/Events/EventsController.cs`, `QuestBoard.Service/Controllers/QuestBoard/CalendarController.cs` — `IsDmTierAsync`, `GetEffectiveRoleAsync`, SuperAdmin short-circuit pattern, existing `Index` assembly shape
- `QuestBoard.Repository/QuestBoard.Repository.csproj`, `QuestBoard.IntegrationTests/QuestBoard.IntegrationTests.csproj` — verified package versions (EF Core 10.0.9, xUnit v3.2.2, FluentAssertions 8.10.0)
- `QuestBoard.Domain/Extensions/ServiceExtensions.cs:14-17` — `EventSeriesOptions` code-default-with-configuration-override precedent
- `.planning/codebase/TESTING.md` — project-wide test conventions
- `.planning/phases/77-availability-overview-page/77-CONTEXT.md`, `77-UI-SPEC.md` — locked decisions and UI contract (copied verbatim above)

### Secondary (MEDIUM confidence)
- None used — every claim in this research is grounded directly in the codebase or in CONTEXT.md/UI-SPEC.md, not in external documentation, since this phase introduces no new external technology.

### Tertiary (LOW confidence)
- None.

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — no new packages; every version claim verified directly against `.csproj` files in this session
- Architecture: HIGH — the recommended query shape is a direct generalization of an already-shipped, already-tested method in the same repository class
- Pitfalls: HIGH — every pitfall traces to a specific, already-solved precedent elsewhere in this codebase (EF Core Take+Include ordering, `IsCancelled` exclusion pattern from Phase 76, `AsSplitQuery()` precedent, SuperAdmin no-active-group handling)

**Research date:** 2026-08-29
**Valid until:** No expiry pressure — this research is grounded in the current state of this specific codebase, not in external library churn. Re-verify only if `IEventRepository`/`QuestBoardContext`/`EventSignup` change materially before this phase is planned.
