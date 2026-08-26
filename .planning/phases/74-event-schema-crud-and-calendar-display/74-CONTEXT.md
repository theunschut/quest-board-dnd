# Phase 74: Event Schema, CRUD, and Calendar Display - Context

**Gathered:** 2026-08-26
**Status:** Ready for planning

<domain>
## Phase Boundary

A DM can create, edit, and delete a one-off, informational event on their board — title, optional Markdown description, date, optional start time — reachable from a "Create Event" entry in the same navbar category as "Create Quest". The event renders on both the desktop calendar page and the mobile calendar page, visually distinct from a quest at a glance, and is visible to every member of that board and to nobody outside it.

This phase also **owns the storage convention and tenant scoping for the whole Calendar Events feature** (Phases 74–77). One purely additive migration creates all three tables up front, following the `AddContactsFeature` precedent.

Not in this phase: player availability signups (Phase 75), recurring series generation (Phase 76), the availability overview page (Phase 77), any change to the 5 `Views/Quest/Details(.Mobile).cshtml` call sites of `_Calendar.cshtml`, and anything that touches quest creation.

</domain>

<decisions>
## Implementation Decisions

### Storage shape

- **D-01: `DateOnly` for the occurrence date, `TimeOnly?` for the optional start time.** *Explicitly confirmed at the ROADMAP's request.* Both map natively to SQL Server `date`/`time` in EF Core 10. This deliberately does **not** follow `Quest.FinalizedDate`'s naive-local `DateTime` convention — that convention is only half-observed today, and `DateOnly` makes the DST bug class structurally impossible rather than merely avoided by discipline.

  **Accepted cost:** `CalendarViewModel` is `DateTime` end to end (`GetCalendarDays()`, `CalendarDay.Date`, `QuestOnDay`). There is therefore a conversion seam at the view-model boundary, and it is the one place a date bug can live. Keep it to a single well-named conversion point — do not scatter `.ToDateTime(...)` calls through the views.

- **D-02: All three tables land in this one migration** — Events, EventSeries, EventSignups — as ordered `CreateTable` calls with no backfill, matching `20260706193921_AddContactsFeature`. Phases 75 and 76 then become pure code changes with no schema work. Two of the three tables ship with no code touching them for a while; that is the knowing trade for locking the storage convention before any occurrence data exists.

- **D-03: `EventEntity` carries a nullable series FK from day one**, so a one-off event and a Phase-76 materialized occurrence are the same entity. (Locked in ROADMAP; D-02 is what makes it possible without a later schema change.)

- **D-04: Tenant scoping — `GroupId` column on `EventEntity` and `EventSeriesEntity`; `EventSignupEntity` scoped through its required `Event` navigation.**
  - `EventSeriesEntity` cannot be scoped through `Event` — the FK points the other way and is nullable — so a series must carry its own `GroupId`. This follows the `QuestEntity` / `ShopItemEntity` / `CharacterEntity` / `ContactEntity` shape.
  - `EventSignupEntity` follows the `PlayerSignupEntity` shape (`.HasQueryFilter(x => activeGroupContext.ActiveGroupId != null && x.Event.GroupId == activeGroupContext.ActiveGroupId)`).
  - Every filter must be **fail-closed** — a null `ActiveGroupId` returns zero rows, never every group's rows merged. Mirror the existing comment block in `QuestBoardContext.cs:271–283` exactly, including the "do not capture `ActiveGroupId` into a local var" rule.

  **Consequence that must be actively handled:** two `GroupId` columns means an event could be written pointing at another board's series. `HasQueryFilter` constrains **reads only**. An explicit board check on write is required — see D-21.

- **D-05: No author column. `CreatedAt` only.** An event is board-level information, not one person's item; recording an author would imply an ownership that does not exist. This directly determines D-11 (any DM on the board can edit any event) and is deliberate, not an oversight. There is no `CreatedByUserId`, no `DungeonMasterId`.

- **D-06: Description is Markdown, unbounded — matching `Quest.Description`, not `Contact.Description`'s `[StringLength(2000)]` plain text.** Reuses `_MarkdownEditor.cshtml` for authoring and the established render path for display. PROJECT.md records `IMarkdownService.ExtractPlainText()` as the single mechanism for every plain-text teaser surface (established Phase 66 D-06, reused Phase 70) — use it anywhere an event description appears as a tooltip or preview, so raw `**`/`#` never leaks. A second text convention was rejected: PROJECT.md blames exactly that drift class for four recorded bugs.

- **D-07: No `EventType` field, no relation to `Quest`.** (Locked in ROADMAP. Meaning comes from the board's immutable `BoardType`.)

### Desktop calendar rendering

- **D-08: Events render in their own block *above* the quest list inside a day cell**, with their own CSS class — not as a third variant interleaved into `.quest-events`. Position carries the "different kind of thing" signal before colour does, and it keeps the event cap independent of the quests' existing `Take(3)`.

  **Accepted cost:** a day holding both gets taller. The grid row height must cope — check this, don't assume it.

- **D-09: The 5 out-of-scope call sites are protected structurally, not by a flag.** `_Calendar.cshtml` has exactly 6 call sites: `Views/Calendar/Index.cshtml:32` (in scope) and `Views/Quest/Details.cshtml:604,648,696` + `Views/Quest/Details.Mobile.cshtml:158,196` (must stay untouched). All 5 build a local `calendarMonth` `CalendarViewModel` inside the view. Adding an **events collection that defaults to empty** on `CalendarViewModel` means those 5 sites render zero events with no flag, no branch, and nothing to forget — a future 7th call site inherits the safe default automatically.

  Rejected: gating on the existing `ViewBag.IsDetailsPage`. That flag defaults to `false`, so a call site that forgets to set it *shows* events — the failure mode points the wrong way.

  Rejected: a duplicate `_CalendarWithEvents.cshtml`. Two near-identical 14K partials is the duplication class PROJECT.md blames for four recorded drift bugs.

  **This is an acceptance criterion, not a code-review note** (ROADMAP says so explicitly): a test must assert that a Quest Details page with a same-day event on the same board renders no event markup.

- **D-10: The event chip is clickable and opens an event details view.** This gives the Markdown description somewhere to render — a `title=` tooltip is plain text and cannot — and gives Phases 75/77 an obvious surface to hang availability on. The view is readable by every member of the board, matching the phase goal that everyone sees the event.

- **D-11: Edit and Delete live on the event details view, DM-gated, and nowhere else.** One surface, one authorization check, and the read path everyone uses is the same one DMs act from. Because there is no author column (D-05), **any DM on the board can edit or delete any event on that board** — this is the intended rule, not a gap.

  Rejected: inline pencil/trash on the calendar chip — that would put DM-only controls inside `_Calendar.cshtml`, the partial shared with the 5 out-of-scope Quest Details sites, directly courting the drift risk the ROADMAP names.

  Rejected: a dedicated Events index page — a second view and a second render surface that no EVENT requirement asks for.

- **D-12: The Legend card gains an Event row, and its hint is updated.** `Views/Calendar/Index.cshtml` hardcodes 4 legend rows (Proposed, Finalized, No building key, and a "Click quests for details" hint). Add a swatch row matching the event chip, and change the hint to cover events as well as quests — it becomes factually wrong once event chips are clickable (D-10). The legend is the only place the calendar explains its own colour coding, so leaving it stale makes EVENT-03's "distinguishable at a glance" half-true.

### Mobile agenda

- **D-13: Widen the filter *and* rewrite the empty state — both halves are required.** `Views/Calendar/Index.Mobile.cshtml:9` filters `.Where(d => !d.IsEmpty && d.QuestsOnDay.Any())`, so a day with only an event is invisible. The filter becomes has-quests **or** has-events. The empty state (currently "No Quests This Month" / "No adventures are planned for {month}") becomes month-neutral. Widening the filter alone would leave an events-only month showing "No Quests This Month" above a list of events — worse than the current state, and a live failure of EVENT-04's intent.

- **D-14: An event with no start time renders as "All day"** — on mobile, where every agenda entry prints `HH:mm` in a right-hand slot, and on desktop for consistency. An empty slot reads as a rendering bug and is indistinguishable from a data-loading failure.

- **D-15: Within a day section, events come first, then quests** — mirroring the desktop day cell (D-08) so both platforms share one mental model. This is the explicit Phase 72 lesson (splitting platforms risks behavioural divergence); the mobile agenda has no legend, so grouping is the only signal available. Rejected: strict chronological mixing, which diverges from desktop and gives "all day" events no natural position.

- **D-16: A mobile event entry taps through to the same event details view as desktop (D-10)**, styled with its own agenda-entry CSS class alongside the existing `agenda-quest-finalized` / `agenda-quest-proposed` variants. Mobile markup must be verified with a **real mobile User-Agent**, not devtools emulation — PROJECT.md records a live case (`_Layout.Platform.Mobile.cshtml`) of mobile markup that was never selected.

### CRUD behaviour

- **D-17: Delete uses a native `confirm()` dialog**, following the Phase 72 D-07 idiom and `revokeSignup()` in `Quest/Details.cshtml`. Deleting an informational event is genuinely low-stakes at this point — the DM can recreate it in seconds. *Flagged for Phase 75:* once availability signups hang off an event, a delete destroys other people's answers and `confirm()` understates that. Revisit there, not here.

- **D-18: Hard delete, not soft delete.** No cancelled/deleted flag. Phase 76's "cancelled occurrence" concept is that phase's problem — adding the column now means every read path in 74 and 75 has to filter on a state nothing sets.

- **D-19: No date restriction — an event can be created or edited onto a past date.** An event is a record, not a booking: backfilling last month's session and correcting a typo on an event that already happened are both legitimate. A future-only rule would also collide with Phase 76's moved/edited occurrences. **Accepted cost:** no guard against a fat-fingered year.

- **D-20: After Create, Edit, and Delete, redirect to the calendar at the *event's* month**, using the existing `CalendarController.Index(year, month)` route — not the current month. A January event created in August must not dump the DM on August; that reads as a silent failure. All three actions set `TempData["Success"]`, which `_Toasts.cshtml` picks up automatically in every layout with no view changes (Phase 72 D-14).

### Tenant scoping and testing (locked by ROADMAP — restated because D-04 raises the stakes)

- **D-21: Defence in both layers.** `HasQueryFilter` for reads (D-04) **plus** an explicit board check on write. `HasQueryFilter` constrains reads only; a mis-scoped `GroupId` on an insert leaks across boards with no schema-level safety net. This app has shipped two real cross-tenant leaks (Phases 49/55) and Phase 72 (D-13) found a third live gap during discussion. With two `GroupId` columns (D-04), the write path must also reject an event whose series belongs to a different board.

- **D-22: A dedicated two-group integration test is not optional.** `WebApplicationFactoryBase.TestGroupContext` is a shared singleton `MutableGroupContext` defaulting to `ActiveGroupId = 1`, so the standard integration test is **structurally blind** to the multi-group bug class. Follow the `QuestBoard.IntegrationTests/Tests/TenantIsolationTests.cs` precedent: seed Group 2 via `factory.Database.CreateContext()` (which uses `ActiveGroupId = null` and sees everything), flip `factory.TestGroupContext.ActiveGroupId = 1`, assert absence — and reset to `1` in `DisposeAsync` so state does not bleed into later test classes.

- **D-23: Quest creation must be provably unaffected** (EVENT-05) — no validation, no warning, no blocking, regardless of what events exist on the chosen date. This needs a test that asserts it, not an absence of code that would have caused it.

- **D-24: The migration must not break boot.** Migrations auto-apply on startup via `context.Database.Migrate()`. Purely additive, ordered `CreateTable` calls, no backfill — verify the app starts against a database at the pre-74 migration.

### Claude's Discretion

Not discussed — planner decides:
- Exact chip colour / CSS class names for the event block, and whether events get their own per-day cap (quests use `Take(3)` with no "and N more" affordance).
- Whether the event details view needs a separate `.Mobile` variant (`Quest/Details` has one; Shop item details shares `_ShopItemDetailsContent.cshtml`) — and whether Create/Edit forms do.
- Controller / service / repository / domain-model naming and file placement, and the Entity ↔ DomainModel ↔ ViewModel AutoMapper profile entries.
- Whether the events collection on `CalendarViewModel` is raw domain models or a `QuestOnDay`-style wrapper, and where the `DateOnly` → `DateTime` conversion seam (D-01) sits.
- `Title` max length, and `OnDelete` behaviour on the `GroupId` and series FKs.
- Whether the mobile agenda's month-neutral empty-state copy is "Nothing This Month" or another wording.
- Toast message wording for create / edit / delete.
- Index strategy on `Events` (a `(GroupId, Date)` index is the obvious candidate given the monthly calendar query, cf. `AddQuestFinalizedDateIndex`).
- Test structure beyond the two named must-haves (D-22 cross-group, D-23 quest-creation-unaffected) and the D-09 Details-page assertion.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase scope and requirements
- `.planning/ROADMAP.md` — Phase 74 entry: goal, 5 success criteria, scope notes, "Decisions locked before planning", and the 5 named risks this phase must actively avoid. Also read the Phase 75/76/77 entries — this phase's schema (D-02) creates their tables, so their scope notes constrain the column set.
- `.planning/REQUIREMENTS.md` — EVENT-01 … EVENT-06 in full (lines 30–37), plus the EVTAVAIL / EVTRECUR / EVTVIEW blocks that the tables created here must eventually serve.
- `.planning/PROJECT.md` — Key Decisions table (the drift/duplication bug history behind D-06 and D-09) and the Known issues section.

### Project conventions
- `CLAUDE.md` — EF packages belong only in `QuestBoard.Repository`; the `modern-card` / `modern-card-header` / `modern-card-body` view pattern with `<hr>` before the button section and `d-flex justify-content-between` button layout; the **no GSD references in source comments** rule (applies to every comment written this phase); migrations auto-apply on startup.
- `.planning/codebase/ARCHITECTURE.md` — Service → Domain → Repository one-way dependency, and the two AutoMapper boundaries.
- `.planning/codebase/CONVENTIONS.md` — naming and AutoMapper patterns.
- `.planning/codebase/TESTING.md` — integration vs unit test placement.

### Prior phase decisions this phase inherits
- `.planning/phases/72-change-character-on-an-existing-signup/72-CONTEXT.md` — D-07 (`confirm()` idiom, → D-17), D-13 (defence in both layers for group scoping, → D-21), D-14 (`TempData["Success"]` + `_Toasts.cshtml`, → D-20), D-16 (plain-language comments, no requirement IDs). Also the desktop-and-mobile-in-one-phase rationale.

### Code the phase must read before changing
- `QuestBoard.Repository/Entities/QuestBoardContext.cs:270–345` — the global query filter block, its fail-closed shape, and the "do not capture `ActiveGroupId` into a local var" warning. D-04's three filters go here.
- `QuestBoard.Repository/Migrations/20260706193921_AddContactsFeature.cs` — the multi-table additive migration precedent named by the ROADMAP.
- `QuestBoard.Repository/Entities/ContactEntity.cs` — the `GroupId` + `[ForeignKey]` nav entity shape.
- `QuestBoard.Service/Views/Shared/_Calendar.cshtml` — the shared partial (14.3K, 6 call sites). The day-cell block around `day.QuestsOnDay.Take(3)` is where D-08 lands.
- `QuestBoard.Service/Views/Calendar/Index.cshtml` — in-scope desktop page; partial call at line 32, hardcoded Legend card below it (D-12).
- `QuestBoard.Service/Views/Calendar/Index.Mobile.cshtml` — hand-rolled agenda; the filter at line 9 and the empty state (D-13), the `HH:mm` slot (D-14).
- `QuestBoard.Service/ViewModels/CalendarViewModels/` — `CalendarViewModel.cs`, `CalendarDay.cs`, `QuestOnDay.cs`. D-09's empty-by-default collection goes here.
- `QuestBoard.Service/Controllers/QuestBoard/CalendarController.cs` — currently single-source (`questService.GetQuestsForCalendarAsync`); needs the events read added.
- `QuestBoard.Service/Views/Shared/_Layout.cshtml:91–117` and `_Layout.Mobile.cshtml:76–98` — the DM navbar sections. "Create Event" goes next to "Create Quest", **not** gated on `BoardType` (Create Quest isn't).
- `QuestBoard.IntegrationTests/Tests/TenantIsolationTests.cs` — the two-group test precedent for D-22.
- `QuestBoard.IntegrationTests/WebApplicationFactoryBase.cs` and `Helpers/MutableGroupContext.cs` — why the default harness is blind to the multi-group bug class.
- `QuestBoard.Service/Views/Shared/_MarkdownEditor.cshtml` — the authoring widget reused by D-06.

### Do not touch
- `QuestBoard.Service/Views/Quest/Details.cshtml:604, 648, 696`
- `QuestBoard.Service/Views/Quest/Details.Mobile.cshtml:158, 196`

  These 5 `_Calendar.cshtml` call sites render the per-quest date-picker/voting widget. D-09 makes them safe structurally; they must not be edited.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `_MarkdownEditor.cshtml` + `IMarkdownService` (incl. `ExtractPlainText()`) — description authoring and any plain-text preview surface (D-06).
- `_Toasts.cshtml` — already wired into all 5 layouts including the Platform Area pair; `TempData["Success"]` needs no view changes (D-20).
- `ContactEntity` + `AddContactsFeature` migration — the entity shape and multi-table additive migration template (D-02, D-04).
- `TenantIsolationTests.cs` — a working two-group isolation test to copy structurally (D-22).
- `CalendarController.Index(year, month)` — the existing route D-20 redirects to.
- Bootstrap 5.3 + Popper, already loaded on both layouts.

### Established Patterns
- **Fail-closed group filters.** Every group-scoped entity in `QuestBoardContext` uses `activeGroupContext.ActiveGroupId != null && ...`. Entities without their own `GroupId` filter through a required navigation. `CharacterEntity` carries a comment explicitly warning against "fixing" it to match `Quest`/`ShopItem` — read the comments before adding filters.
- **Monday-first calendar grid.** `CalendarViewModel.GetCalendarDays()` pads with `IsEmpty` days at both ends using `((int)dayOfWeek + 6) % 7`. Events must slot into this existing day list, not a parallel one.
- **The desktop calendar model is built per-view on Details pages** but from the controller on `Calendar/Index` — which is exactly what makes D-09's default-empty approach work.
- **`.Mobile.cshtml` view resolution** is real platform switching, not CSS. Mobile markup must be verified with a real mobile User-Agent (D-16).

### Integration Points
- `QuestBoardContext.OnModelCreating` — three new `DbSet`s, three query filters, FK configuration.
- `CalendarController.Index` — second data source alongside quests.
- `CalendarViewModel` — new default-empty events collection (D-09), consumed by `_Calendar.cshtml` and `Index.Mobile.cshtml`.
- `_Layout.cshtml` / `_Layout.Mobile.cshtml` DM navbar — "Create Event" entry (EVENT-06).
- New controller + service + repository following the strict Service → Domain → Repository direction, with both AutoMapper profiles extended.

</code_context>

<specifics>
## Specific Ideas

- The ROADMAP's own framing that this phase "owns the storage convention and tenant scoping for the whole feature" was treated as the organising principle of the storage discussion — hence all three tables now (D-02) rather than the smallest schema that satisfies EVENT-01…06.
- "An event is a record, not a booking" — the reasoning behind allowing past dates (D-19).
- "An event is board-level information, not one person's item" — the reasoning behind dropping the author column (D-05), which is what makes any-DM-can-edit (D-11) the natural rule rather than a permissions shortcut.

</specifics>

<deferred>
## Deferred Ideas

- **Stronger delete confirmation once signups exist.** `confirm()` is right for an event nobody has answered on; it understates a delete that destroys other people's availability. Revisit in Phase 75 (D-17).
- **An Events index / management page.** Rejected here as a second render surface no EVENT requirement asks for. If managing events three months out via calendar navigation proves annoying in practice, this is the shape it would take.
- **A "and N more" affordance on crowded calendar day cells.** Quests are silently capped at `Take(3)` today with no overflow indicator. Pre-existing, out of scope, but events make the cell busier.
- **Guarding against a fat-fingered year on event dates** (accepted cost of D-19).
- **Soft delete / cancelled state** — belongs with Phase 76's cancelled-occurrence concept (D-18).

</deferred>

---

*Phase: 74-Event Schema, CRUD, and Calendar Display*
*Context gathered: 2026-08-26*
