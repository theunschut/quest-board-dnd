# Phase 77: Availability Overview Page - Context

**Gathered:** 2026-08-29
**Status:** Ready for planning

<domain>
## Phase Boundary

One **read-only** page, scoped to the active board, showing the next N upcoming events as a grid of **events (rows) against members (columns)**, where each cell carries that member's availability, an untouched Campaign default is visually distinct from an answer a person actually gave, and each event row carries an availability count. Reachable by every board member. Nothing from another board ever appears, proven by a dedicated two-group integration test.

The data this page needs already exists. Phase 74 shipped the schema, Phase 75 shipped `EventSignup.HasAnswered` and the Campaign fan-out, Phase 76 shipped recurring occurrences and the cancelled tombstone. This phase is a **new read path plus a new view** — a query, a view model, a controller action, two nav changes, and two Razor views. It writes nothing.

Not in this phase: any write to availability (the existing `Events/Details` surface keeps that job — Phase 75 D-01), any change to the calendar's own rendering, quests in any form, and the cross-board personal agenda (now **Phase 82**, added to the roadmap on 2026-08-29 — see `<deferred>`).

</domain>

<decisions>
## Implementation Decisions

### The cell — telling a real answer from an untouched default (EVTVIEW-02)

- **D-01: An untouched Campaign default renders as a muted Yes chip; a confirmed answer renders solid.** Not a neutral mark. The distinction is deliberately one of emphasis rather than of meaning, because on a Campaign board the stored Yes is not a lie — that member *will* be counted as available if nobody chases them. The cell must say both things: "counted as available" and "nobody confirmed it".

  Rejected: a neutral dash claiming no vote at all (loses the fact that they are counted); a Yes chip with a `?` badge (busiest option, in the densest region of the page).

- **D-02: The muted state must carry a second signal that survives greyscale.** Fill weight alone is a difference of degree and is the exact thing a colour-blind or low-contrast viewer misses — which would defeat EVTVIEW-02 entirely for that viewer. Add a shape or text cue alongside the fill: a dotted border, an italic label, a small icon. Exact choice is the planner's, but *something non-colour* is mandatory, not advisory.

  A page legend is welcome in addition, but is **not** a substitute — a legend only helps the reader who looks at it.

- **D-03: Campaign-untouched and One-Shot-no-row must look different from each other.** They mean genuinely different things: the Campaign default stores Yes and is counted; a One-Shot member with no row stores nothing and is counted nowhere. Flattening them into one "hasn't answered" look would erase a real difference on the page whose whole job is to stop one availability state being mistaken for another.

  So the cell vocabulary has **four** states, not three: confirmed Yes / Maybe / No, muted-Yes (Campaign default), and empty (no row).

- **D-04: `HasAnswered` is the only input to this distinction.** `EventSignup.HasAnswered => UpdatedAt != null` already ships. Phase 75 D-11 is explicit that no consumer — this phase named directly — reads the raw timestamp for this purpose. Do not re-derive it, do not branch on board type to compute it: it is uniform across both board types by construction (Phase 75 D-10).

### The count (EVTVIEW-03)

- **D-05: The headline figure is total Yes including untouched defaults.** The big, glanceable number answers "who is expected at this session", which is what a DM planning a date actually needs — and on a Campaign board the default genuinely *is* the plan until someone changes it.

- **D-06: The confirmed portion is shown alongside it as secondary detail.** The headline is the figure that can mislead, so it never stands alone. Both facts are on the row: how many are counted, and how much of that anyone vouched for. This is what keeps D-05 from reintroducing the "yes by default read as a real answer" risk one line above the cells that just fixed it.

- **D-07: Maybe is counted and shown separately, never folded into Yes and never omitted.** A session with 2 Yes and 4 Maybe is not a dead date, and it is not a healthy one either — collapsing them would destroy the distinction the three-value vote exists to record. No is not counted anywhere; it is visible in its cell.

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

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase scope and requirements
- `.planning/ROADMAP.md` — the **Phase 77** entry: goal, 4 success criteria, both scope notes (the DM-only question, now answered by D-16; and the N+1 constraint), and the single named risk (the tenant-scoping trap on an aggregating page). Also the new **Phase 82** entry, which owns the deferred cross-board agenda.
- `.planning/REQUIREMENTS.md:72–75` — EVTVIEW-01 … EVTVIEW-04 in full.
- `.planning/phases/75-event-availability-signups/75-CONTEXT.md` — **the direct dependency.** D-01 (Details is the single availability surface → D-23), D-02 (roster visible to all members → D-16), D-03 (One-Shot roster shows only rows → D-13), **D-10/D-11 (`HasAnswered` is the untouched-vs-real mechanism; never read the raw timestamp → D-04)**, D-15/D-19 (Campaign fan-out is atomic, so membership implies rows → D-14), D-17 (`Date >= today`, date-only → D-11), D-28/D-29 (defence in both layers; the two-group test is not optional → D-26/D-27), D-30 (narrow scalar updates — relevant only if the planner adds a navigation collection).
- `.planning/phases/76-recurring-event-series/76-CONTEXT.md` — read the **"Consequence for Phase 77"** block at the end of `<deferred>`: cancelled occurrences keep their signup rows and must be excluded here (→ D-12). Also D-14 (the cancelled tombstone), D-114 (the global-runway-as-configurable-code-default precedent for D-09's N), and D-126 (per-group `SetGroupId()` iteration — **not** used by this phase, but the mechanism Phase 82 will need).
- `.planning/phases/74-event-schema-crud-and-calendar-display/74-CONTEXT.md` — D-01 (`DateOnly`/`TimeOnly?` and why → D-11), D-19 (past-dated events are allowed, so the lower bound is load-bearing), D-21 (the tenant-scoping shape).

### Project conventions
- `CLAUDE.md` — the `modern-card` / `modern-card-header` / `modern-card-body` view pattern (mandatory for the new views); EF packages only in `QuestBoard.Repository`; **no GSD references in source comments** — applies to every comment written this phase; migrations auto-apply on startup.
- `.planning/codebase/CONVENTIONS.md` — naming, AutoMapper patterns, the UI/UX card pattern, async conventions.
- `.planning/codebase/ARCHITECTURE.md` — Service → Domain → Repository one-way dependency and the two AutoMapper boundaries.
- `.planning/codebase/TESTING.md` — integration vs unit test placement.

### Code the phase must read before changing
- `QuestBoard.Domain/Models/EventSignup.cs` — `HasAnswered`, `Availability`, `UserName`. The comment above `HasAnswered` states the D-04 rule; do not contradict it.
- `QuestBoard.Domain/Models/Event.cs` — `Date` (`DateOnly`), `StartTime` (`TimeOnly?`), `CancelledAt` / `IsCancelled` (get-only, so it can never be mapped or bound).
- `QuestBoard.Domain/Interfaces/IEventRepository.cs` — the full set of shipped reads, and the XML docs stating how group scoping is enforced (by the query filter, not by a parameter). `GetEventsForCalendarAsync` fetches *all* events with no date predicate; D-09/D-10/D-11 need a new, bounded read rather than a filter over that one.
- `QuestBoard.Domain/Interfaces/IEventSignupRepository.cs` and `QuestBoard.Repository/EventSignupRepository.cs:60` — `GetRosterForEventAsync`, the single-query eager-include shape D-28 generalises from.
- `QuestBoard.Repository/Entities/QuestBoardContext.cs` — the fail-closed global query filter block, including the "do not capture `ActiveGroupId` into a local var" warning. This is the D-26 mechanism.
- `QuestBoard.Repository/GroupRepository.cs:144` — `GetEventSignupsForMemberIgnoringActiveBoardAsync`. Read it to understand why it is **not** a precedent for this phase (private, group-pinned, cleanup-only).
- `QuestBoard.Service/Controllers/Events/EventsController.cs` — `IsDmTierAsync`, `GetEffectiveRoleAsync`, `EventIsOnActiveBoard`, and the SuperAdmin-with-no-active-group handling to mirror.
- `QuestBoard.Service/Controllers/QuestBoard/CalendarController.cs` — how the events surface is assembled today, including the `boardTypeResolver` gate and the DM-tier conditional read.
- `QuestBoard.Service/Views/Calendar/Index.cshtml` and `Index.Mobile.cshtml` — the desktop/mobile split precedent for D-17, and where D-21's cross-link lands.
- `QuestBoard.Service/Views/Shared/_Layout.cshtml:162–176` (nav block and the board-type gate) and `:179` (the existing dropdown pattern D-19 copies).
- `QuestBoard.Service/Views/Shared/_Layout.Mobile.cshtml:141–152` — the flat offcanvas nav block D-20 extends. Confirmed to contain no dropdown anywhere.
- `QuestBoard.Service/Views/Quest/Manage.cshtml:159–213` — the nearest existing thing to this page: per-date Yes/Maybe/No counting with a "recommended date" marker. Useful for D-05…D-08's format, and it is DM-gated, which is the precedent D-16 deliberately does *not* follow.
- `QuestBoard.IntegrationTests/Tests/EventAvailabilityTenantIsolationTests.cs` — the closest sibling to the D-27 test.
- `QuestBoard.IntegrationTests/WebApplicationFactoryBase.cs` and `QuestBoard.IntegrationTests/Helpers/MutableGroupContext.cs` — why the default harness is blind without D-27.
- `QuestBoard.IntegrationTests/Controllers/LayoutNavigationTests.cs:54–110` — the four Calendar-link cases D-19/D-20 must keep green, and the shape new nav cases should follow.

### Do not touch
- `QuestBoard.Service/Views/Shared/_Calendar.cshtml` — Phase 74 D-09 protects five `Quest/Details(.Mobile).cshtml` call sites through this partial. This phase has no reason to go near it.
- `QuestBoard.Service/Views/Events/Details.cshtml` — D-23 keeps the write path exactly where it is.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- **`EventSignup.HasAnswered`** — ships, tested, and is the sole input to D-01/D-04. `EventSignupRepositoryTests.AutomaticPassRow_ReadsAsNotAnswered_UntilSetAvailabilityAsyncTouchesIt` is the test proving the automatic fan-out leaves it null.
- **`EventSignupRepository.GetRosterForEventAsync`** — one query, `.Include(es => es.User)`, ordered alphabetically in SQL. The template for D-28's aggregate.
- **`Event.IsCancelled`** — get-only, already on the domain model. D-12's filter needs nothing new.
- **`VoteType { No = 0, Maybe = 1, Yes = 2 }`** — reused as-is for the cell vocabulary.
- **`IBoardTypeResolver`** — server-side board-type resolution, needed by D-13/D-14's branch and already used by both `EventsController` and `CalendarController`.
- **`EventsController.IsDmTierAsync` / `GetEffectiveRoleAsync`** — exist, but D-16 means this page needs neither. Listed so nobody adds a gate out of habit.
- **The `_Layout.cshtml` user-menu dropdown** (`:179`) — the exact Bootstrap idiom D-19 copies for the Calendar dropdown.
- **`Quest/Manage.cshtml`'s per-date vote counting** — closest existing precedent for D-05…D-08, including a "recommended date" highlight for the best-attended option.

### Established Patterns
- **Mobile views are selected by user agent, not by breakpoint.** `Calendar/Index.Mobile.cshtml` exists; `Events/Details.cshtml` deliberately has none. D-17 opts into the split. **Devtools viewport emulation will not exercise the mobile view — verification requires a real mobile user agent.**
- **Group scoping is a fail-closed global query filter, enforced at the context, never by a method parameter.** Every `IEventRepository` XML doc says so explicitly. D-26 depends on this staying true.
- **The nav board-type gate is on a *resolved board type*, not a role** — `activeBoardType is BoardType.OneShot or BoardType.Campaign`, with a comment explaining that an unresolved type is deliberately excluded rather than guessed at. Both layouts carry it. D-22 reuses it unchanged.
- **The mobile layout has no dropdown anywhere** — verified, zero occurrences in `_Layout.Mobile.cshtml`. This is what makes D-20 a flat list rather than a mirror of D-19.
- **`LayoutNavigationTests` asserts on strings, not markup structure** — `html.Should().Contain("Calendar")`. Restructuring the nav entry does not break it.
- **`EventsController` has no `Index` action; `CalendarController` owns `Index`.** Neither is an obvious home for this page — hence the discretion note.
- **Campaign boards guarantee a signup row per member** (Phase 75 D-15/D-19, atomic — a failed fan-out rolls back the join). D-14 relies on this and must not re-verify it with a membership query.

### Integration Points
- **New bounded read** on `IEventRepository`/`IEventSignupRepository` — next N non-cancelled events dated today-or-later, with their signup rows and member names, in one round trip (D-09…D-12, D-28).
- **New controller action + view model** — placement is discretionary; the SuperAdmin-with-no-active-group handling must mirror `EventsController`'s.
- **Two new Razor views** — desktop grid and `.Mobile.cshtml` cards, both on the `modern-card` pattern.
- **`_Layout.cshtml`** — Calendar nav entry becomes a dropdown (D-19).
- **`_Layout.Mobile.cshtml`** — second flat entry beside Calendar (D-20).
- **`Calendar/Index.cshtml` and `Index.Mobile.cshtml`** — a link across to the overview (D-21). This is a link only; nothing about the calendar's own rendering changes.
- **Both AutoMapper profiles** extended for whatever the new view model needs.

</code_context>

<specifics>
## Specific Ideas

- **"The headline is the figure that can mislead, so it never stands alone."** The reasoning behind D-05 + D-06 as a pair. A planner tempted to simplify by dropping the confirmed count should understand that the pair is the decision — D-05 alone reintroduces exactly the risk EVTVIEW-02 exists to remove, one line above the cells that just fixed it.
- **The muted Yes is not a hedge, it is the honest answer.** D-01 was chosen over a neutral mark specifically because on a Campaign board the stored Yes *is* what will happen. The cell has to carry "counted as available" and "nobody confirmed it" simultaneously, which is why D-02's second signal is mandatory rather than a nicety.
- **Four cell states, not three.** D-03 is easy to lose during implementation. Confirmed Yes, Maybe, No, muted-Yes, and empty are five renderings the planner must lay out together — see D-15.
- **Events are rows because the row is the click target.** D-25, D-24, and D-17 are one decision viewed three ways. Transposing the grid silently breaks the other two.
- **On the cross-board idea:** the reason it is Phase 82 and not a flag on this page is that EVTVIEW-04 and its two-group test would have to both prove and disprove the same property depending on a toggle. That is a requirement conflict, not a size problem.

</specifics>

<deferred>
## Deferred Ideas

- **A personal cross-board event agenda** — every upcoming event the logged-in user is expected at, across all boards they belong to, with the board named on each row. Raised by the operator during this discussion. **Promoted to a real roadmap phase (Phase 82) on 2026-08-29** rather than left as a note, at the operator's request. The full analysis — why it cannot be a toggle on this page, why it cannot sit behind the Calendar nav gate, which two safe cross-group mechanisms already exist, and the two questions its own discuss pass must settle — lives in the Phase 82 entry in `.planning/ROADMAP.md`. It is quests-excluded and events-only.
- **Making cells writable** — considered and declined (D-23). Would make Phase 75 D-01's "single availability surface" false and put the ownership rule in a second place. D-24's click-through gives the action path without the write surface. If it is ever wanted, the `SetAvailability` endpoint is already ownership-checked and would be the thing to reuse.
- **An adjustable / configurable window control on the page** — declined under D-09. It asks the reader an implementation question instead of answering one. The value of N being configurable in code is a separate matter and is left to the planner.
- **DM-only extras on a shared page** (e.g. the confirmed/total split shown only to DM-tier) — declined under D-16. Two renderings of one page to build and test, for no requirement.
- **A per-board denominator on the count** ("4 of 6 members") — not decided either way; folded into D-08's format discretion. On a One-Shot board under D-13 there is no meaningful denominator, which is the wrinkle to think about before adding one.

</deferred>

---

*Phase: 77-Availability Overview Page*
*Context gathered: 2026-08-29*
