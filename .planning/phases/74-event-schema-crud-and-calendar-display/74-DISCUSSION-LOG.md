# Phase 74: Event Schema, CRUD, and Calendar Display - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-08-26
**Phase:** 74-Event Schema, CRUD, and Calendar Display
**Areas discussed:** Storage shape, Desktop calendar look, Mobile agenda, CRUD surface & who edits

---

## Storage shape

### Date types (ROADMAP explicitly asked for confirmation)

| Option | Description | Selected |
|--------|-------------|----------|
| Confirm `DateOnly`/`TimeOnly` | Native SQL Server `date`/`time` in EF Core 10; DST bug class structurally impossible. Cost: conversion seam at the `CalendarViewModel` boundary | ✓ |
| Match `Quest`'s `DateTime` convention | One date type codebase-wide, no seam. Cost: inherits the half-observed naive-local convention; reversal gets expensive once Phase 76 materializes occurrences | |
| `DateOnly`/`TimeOnly?` in domain, one `DateTime?` column | Cost: "optional start time" becomes indistinguishable from midnight | |

**User's choice:** Confirm `DateOnly`/`TimeOnly`
**Notes:** The ROADMAP's own reasoning was accepted as written. The conversion seam is a known, accepted cost — CONTEXT.md instructs the planner to keep it to one named point.

### Migration footprint

| Option | Description | Selected |
|--------|-------------|----------|
| All three tables now | Matches ROADMAP literally + `AddContactsFeature` precedent; 75/76 become pure code. Cost: two empty tables in production | ✓ |
| Events + EventSeries only | EventSeries is forced by the nullable FK; EventSignups isn't. Cost: a second migration in Phase 75 | |
| Events only, series FK later | Cost: contradicts a locked ROADMAP decision; turns Phase 76 into a schema change on a live table | |

**User's choice:** All three tables now
**Notes:** Anchored on the ROADMAP's framing that this phase "owns the storage convention for the whole feature."

### Tenant scoping shape

| Option | Description | Selected |
|--------|-------------|----------|
| `GroupId` on Event + Series; Signups via nav | Series can't scope through Event (FK points the other way and is nullable). Cost: two `GroupId` columns → an event could point at another board's series | ✓ |
| `GroupId` on all three | Every table self-describing. Cost: three places a wrong `GroupId` can be written; signup `GroupId` can drift from its event's | |
| `GroupId` on Series only; Event via nav | Cost: non-starter — a one-off event has a null series and would have no scoping at all | |

**User's choice:** `GroupId` on Event + Series; Signups via nav
**Notes:** The two-column consequence is why CONTEXT.md D-21 restates the explicit-board-check-on-write requirement rather than leaving it to the ROADMAP.

### Authorship / audit columns

| Option | Description | Selected |
|--------|-------------|----------|
| `CreatedByUserId` + `CreatedAt` | Follows `ContactEntity`; keeps creator-only editing available without a later migration | |
| `CreatedAt` only, no author | An event is board-level information, not one person's item. Cost: closes off creator-only editing; no "who put this here" trail | ✓ |
| Neither | Cost: no ordering fallback, no appearance timestamp | |

**User's choice:** `CreatedAt` only, no author
**Notes:** This is what makes "any DM on the board can edit any event" the natural rule rather than a permissions shortcut. Recorded as deliberate in CONTEXT.md D-05 so a future reviewer doesn't read it as an omission.

### Description field type

| Option | Description | Selected |
|--------|-------------|----------|
| Markdown, unbounded — match `Quest` | Reuses `_MarkdownEditor.cshtml` and `ExtractPlainText()`. Cost: Markdown pipeline for a field that may hold one sentence | ✓ |
| Plain text, `StringLength(2000)` — match `Contact` | Simplest field. Cost: a second text convention — the drift class PROJECT.md blames for four bugs | |
| Markdown capped at 2000 | Cost: diverges from `Quest` anyway, just less visibly | |

**User's choice:** Markdown, unbounded — match `Quest`
**Notes:** Raised by Claude as a follow-up after the initial four questions; the user chose to take it rather than defer to the planner.

---

## Desktop calendar look

### Day-cell layout

| Option | Description | Selected |
|--------|-------------|----------|
| Separate `.event-events` block above quests | Position signals "different kind of thing" before colour; independent caps. Cost: taller cells | ✓ |
| Third chip variant in the same list | Smallest diff. Cost: EVENT-03 rests entirely on colour; shared `Take(3)` can hide the event | |
| Compact badge on the day number | Events stay visually secondary. Cost: title not readable at a glance — arguably fails EVENT-03 | |

**User's choice:** Separate block above quests

### Protecting the 5 out-of-scope `_Calendar.cshtml` call sites

| Option | Description | Selected |
|--------|-------------|----------|
| Empty by default on the view model | The 5 Details sites construct `calendarMonth` without the property, so they render zero events with no flag and nothing to forget | ✓ |
| Gate on existing `ViewBag.IsDetailsPage` | Explicit and readable. Cost: flag defaults to `false`, so a forgotten call site *shows* events — failure mode points the wrong way | |
| Split into `_CalendarWithEvents.cshtml` | The 5 sites literally cannot change. Cost: two near-identical 14K partials — the duplication class PROJECT.md blames for four bugs | |

**User's choice:** Empty by default on the view model
**Notes:** Scouting confirmed the ROADMAP's "6 call sites" figure exactly — `Calendar/Index.cshtml:32`, `Quest/Details.cshtml:604,648,696`, `Quest/Details.Mobile.cshtml:158,196`.

### Chip clickability

| Option | Description | Selected |
|--------|-------------|----------|
| Not clickable, `title=` tooltip only | Cost: a Markdown description has nowhere to render (tooltips are plain text) | |
| Clickable — opens a details view | Gives the description a home and Phases 75/77 a surface. Cost: a view the requirements don't strictly ask for | ✓ |
| Clickable — inline popover/collapse | Bootstrap 5.3 already loaded. Cost: Markdown in a popover on a grid cell is fiddly; Phase 75 wants a real page anyway | |

**User's choice:** Clickable — opens a details view
**Notes:** This decision is what later made "Edit/Delete live on the details view" the obvious answer in the CRUD area.

### Legend card

| Option | Description | Selected |
|--------|-------------|----------|
| Add an Event row + update the hint | The hint ("Click quests for details") becomes factually wrong once chips are clickable | ✓ |
| Add an Event row, leave the hint | Cost: hint now wrong | |
| Leave the legend untouched | Cost: a new colour appears with nothing explaining it | |

**User's choice:** Add an Event row + update the hint

**Offered and declined:** a further question on the per-day event cap (quests use `Take(3)` with no overflow affordance) — routed to Claude's discretion.

---

## Mobile agenda

### Filter and empty state

| Option | Description | Selected |
|--------|-------------|----------|
| Widen filter + rewrite empty state | Both halves required — widening alone leaves an events-only month showing "No Quests This Month" above a list of events | ✓ |
| Widen the filter only | Satisfies EVENT-04 literally. Cost: empty state becomes a flat contradiction | |
| Separate Events section above the agenda | Mirrors desktop at page level. Cost: breaks day-by-day chronology | |

**User's choice:** Widen filter + rewrite empty state

### Time slot for an event with no start time

| Option | Description | Selected |
|--------|-------------|----------|
| "All day" | Names the actual state; no layout change. Cost: slight overclaim for an unset time | ✓ |
| Leave the slot empty | Cost: reads as a rendering bug, indistinguishable from a load failure | |
| Dash or icon | Cost: needs explaining, and mobile has no legend | |

**User's choice:** "All day" — applied on desktop too, for consistency

### Ordering within a day section

| Option | Description | Selected |
|--------|-------------|----------|
| Events first, then quests | Mirrors the desktop day cell — the Phase 72 one-mental-model lesson. Cost: an all-day event sits above a 20:00 quest | ✓ |
| Strict chronological, mixed | Truest to an agenda. Cost: diverges from desktop; all-day events need a tiebreak rule anyway | |
| Quests first, then events | Keeps the core loop primary. Cost: contradicts desktop on the same data | |

**User's choice:** Events first, then quests

**Offered and declined:** whether the event details view needs its own `.Mobile` variant — routed to Claude's discretion.

---

## CRUD surface & who edits

### Where Edit and Delete live

| Option | Description | Selected |
|--------|-------------|----------|
| On the event details view | One surface, one auth check, same read path everyone uses. Cost: no all-events list | ✓ |
| A dedicated Events index page | Mirrors `ShopManagement/Index`. Cost: a second view and render surface no requirement asks for | |
| Inline controls on the calendar cell | Fewest clicks. Cost: DM-only controls inside the partial shared with 5 out-of-scope sites | |

**User's choice:** On the event details view
**Notes:** Follows directly from the earlier decision to make the chip clickable. No author column means any DM on the board can edit any event.

### Delete confirmation

| Option | Description | Selected |
|--------|-------------|----------|
| Native `confirm()` | Phase 72 D-07 idiom; low-stakes today. Cost: understates a delete once Phase 75 attaches signups | ✓ |
| Bootstrap modal | Styled, can name the event. Cost: a second confirmation convention | |
| Soft delete | Nothing ever lost; gives Phase 76 its cancelled column early. Cost: a state model nothing in this phase sets | |

**User's choice:** Native `confirm()`
**Notes:** Flagged in CONTEXT.md for revisit in Phase 75, when a delete starts destroying other people's availability answers.

### Past dates

| Option | Description | Selected |
|--------|-------------|----------|
| Allowed — no date restriction | An event is a record, not a booking. Cost: no guard against a fat-fingered year | ✓ |
| Create blocked in the past, edit allowed | Cost: arbitrary asymmetry; blocks deliberate backfill | |
| Blocked entirely — future only | Cost: a past event can never be corrected; collides with Phase 76's moved occurrences | |

**User's choice:** Allowed — no date restriction

### Post-action destination

| Option | Description | Selected |
|--------|-------------|----------|
| Calendar at the event's month, toast on all three | Lands on the surface that proves the change; uses the existing `Index(year, month)` route. Cost: Edit loses the details view | ✓ |
| Details view after Create/Edit, calendar after Delete | Confirms the record field by field. Cost: two destinations; Create doesn't show calendar placement | |
| Always the current calendar month | Predictable. Cost: an event three months out lands you where nothing visibly happened | |

**User's choice:** Calendar at the event's month, toast on all three

---

## Claude's Discretion

Routed to the planner (see CONTEXT.md `### Claude's Discretion` for the full list):

- Event chip colour, CSS class names, and the per-day event cap
- Whether the event details view / Create / Edit forms need `.Mobile` variants
- Controller / service / repository / domain-model naming and AutoMapper profile entries
- Shape of the events collection on `CalendarViewModel` and where the `DateOnly` → `DateTime` seam sits
- `Title` max length and `OnDelete` behaviour on the `GroupId` and series FKs
- Mobile empty-state copy and toast wording
- Index strategy on `Events`
- Test structure beyond the three named must-haves

## Deferred Ideas

- Stronger delete confirmation once signups exist — Phase 75
- An Events index / management page — if calendar navigation proves annoying in practice
- An "and N more" overflow affordance on crowded day cells — pre-existing gap, `Take(3)` today
- Guarding against a fat-fingered year on event dates — accepted cost
- Soft delete / cancelled state — belongs with Phase 76's cancelled-occurrence concept

## Not Discussed — Already Locked

Carried in from the ROADMAP and prior phases without re-asking: no `EventType` field, no relation to `Quest`, quest creation provably unaffected, defence-in-both-layers group scoping with a dedicated two-group integration test, desktop and mobile in one phase, and the 5 `Quest/Details` calendar call sites staying untouched.
