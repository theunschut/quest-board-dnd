# Phase 82: Personal Cross-Board Event Agenda - Research

**Researched:** 2026-08-29
**Domain:** ASP.NET Core 10 MVC / EF Core cross-tenant read, session-scoped view filter, Razor desktop+mobile views
**Confidence:** HIGH (codebase-verified throughout; a small number of UI-discretion items are MEDIUM/LOW and flagged in the Assumptions Log)

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

D-01 through D-18 in `.planning/phases/82-personal-cross-board-event-agenda/82-CONTEXT.md` are locked and are **not** re-derived here. In summary, for quick reference while reading this document:

- **D-01:** Query starts from `Events`, not `EventSignups` — every upcoming event on every board the viewer belongs to, whether or not a signup row exists.
- **D-02:** Each row carries the full roster (title, date/time, board name, viewer's own availability, every member's availability) — not just the viewer's own answer or a count.
- **D-03:** A global next-N across all boards, chronological, not N per board.
- **D-04:** A board-selection filter, default all-ticked, applied **before** the take.
- **D-05:** Filter selection remembered in ASP.NET session (same mechanism as `ActiveGroupId`), not a persisted preference.
- **D-06:** Roster visible inline on desktop, behind a tap on mobile — mirrors Phase 77 D-17/D-18. Mobile is real `.Mobile.cshtml`, user-agent selected.
- **D-07:** The agenda supplements; `GroupPickerController.Index`'s redirect and the login path are untouched.
- **D-08:** Nav entry visible to every authenticated user, no board-count gate, sits beside Switch Group (outside the Calendar board-type gate).
- **D-09:** A SuperAdmin sees only boards they are actually a member of — no cross-group escape hatch, ever.
- **D-10:** Phase 77's overview and the Calendar page both cross-link to the agenda, in addition to the dropdown entry; the dropdown entry is the only unconditional path in.
- **D-11:** Acting on a row for a non-active-board event prompts ("This session is on *Board*. Switch to it?") via an antiforgery-protected post that reuses `GroupPickerController.SelectGroup`; same-active-board rows skip the prompt.
- **D-12:** The click target is an explicit control on the row, **not the whole row** — a deliberate divergence from Phase 77 D-24/D-25.
- **D-13:** `Details` carries a conditional back-link to the agenda when arrived from it, via `returnUrl` through `RedirectToLocal`. The active board is **not** switched back on return.
- **D-14:** One query, `IgnoreQueryFilters()` with `memberGroupIds.Contains(e.GroupId) && e.Date >= today && e.CancelledAt == null` pinned in the predicate immediately. Never per-board `SetGroupId()` iteration. Never a bare `IgnoreQueryFilters()` (the `QuestRepository.GetQuestsForTomorrowAllGroupsAsync` shape is explicitly not a precedent).
- **D-15:** The membership set (`memberGroupIds`) is read fresh from `UserGroups` on every request — never session, never claims.
- **D-16:** A second in-memory re-check of every materialized row's `GroupId` against the same `memberGroupIds` list, with its limits documented in the comment (it does not cover a wrong membership set, only a dropped predicate or bad translation).
- **D-17:** A mandatory four-case integration test: (1) non-member board fully absent, (2) two joined boards both appear and a third does not, (3) a left board disappears, (4) the board filter cannot widen the set. Reset `ActiveGroupId` to `1` in `DisposeAsync`.
- **D-18:** The query must not N+1 across events x signups x users x groups — one round trip, generalising `EventSignupRepository.GetRosterForEventAsync`'s shape.

### Claude's Discretion

- Page name, icon, route, controller home (`EventsController.Index` is taken by Phase 77).
- Whether the agenda gets its own take default rather than sharing `EventsOverviewOptions.DefaultTake = 10` (a `MaxTake` ceiling is still mandatory).
- How the board name is rendered on a row (plain text / badge / colour chip, with or without board type).
- Whether the list carries day/date group headers or each row is fully self-contained.
- Empty-state copy for three cases: no boards, boards with no upcoming events, every board filtered out.
- Whether the switch prompt is a modal, inline confirm, or interstitial page, and its copy.
- How the mobile card carries D-12's explicit control alongside D-06's roster disclosure without the two competing for the same tap.
- Whether cancelled occurrences get any acknowledgement (exclusion itself is mandatory).
- Whether the viewer's own roster entry is highlighted, and roster ordering (`GetRosterForEventAsync` orders alphabetically; matching costs nothing).
- Domain model / repository / service / controller naming, file placement, AutoMapper profile entries at both boundaries.
- Test structure beyond the four mandated D-17 cases.

### Deferred Ideas (OUT OF SCOPE)

- Landing a multi-board user on the agenda instead of the group picker (declined, D-07).
- The agenda as the group picker's replacement, folding switch+navigate into one click (declined, D-07/D-11).
- Answering availability inline on the agenda (declined, D-11 — would falsify Phase 75 D-01's single-availability-surface rule).
- A per-user persisted board filter preference across logout/devices (declined, D-05).
- Gating the nav entry on belonging to >1 board (declined, D-08).
- A SuperAdmin view of every board's events (declined, D-09).
- Per-board quotas or a per-board floor in the window (declined, D-03).
- Quests on the agenda (roadmap-excluded).
- Switching the active board back on return from `Details` (declined, D-13).

</user_constraints>

<phase_requirements>
## Phase Requirements

**Phase 82 has no requirement IDs yet** (`.planning/REQUIREMENTS.md` marks it `Requirements: TBD`; the `EVTVIEW-01..04` block belongs to Phase 77 and `EVTVIEW-04` is explicitly board-scoped). The existing ID families are `EVENT-*` (foundation), `EVTAVAIL-*` (availability/signups), `EVTRECUR-*` (recurrence), `EVTVIEW-*` (board-scoped overview). None of those families describes a cross-board read, so this phase needs its own prefix. Recommended: **`EVTAGENDA-*`**, minted by the planner (this list is a starting proposal, not a locked decision):

| Proposed ID | Description | Research Support |
|---|---|---|
| EVTAGENDA-01 | A member belonging to more than one board sees every upcoming event across all their boards in one place, one row per event, ordered chronologically and interleaved across boards, regardless of whether a signup row exists for it | D-01/D-03; `IEventRepository.GetUpcomingWithSignupsAsync` is the query shape to generalise (see Architecture Patterns) |
| EVTAGENDA-02 | Every row names the board the event belongs to | D-02/D-04; `GroupRepository.GetGroupsForUserAsync` already returns `Id`+`Name` for the viewer's boards — see the "board name" finding below |
| EVTAGENDA-03 | Each row carries the viewer's own availability and the event's complete roster (every member's availability on that event), matching what `Events/Details` already shows for that event | D-02; `EventSignupRepository.GetRosterForEventAsync` is the eager-include shape to generalise, `EventService.ClassifyCell`/`_AvailabilityCell.cshtml` are reused verbatim |
| EVTAGENDA-04 | A board-selection filter narrows the set, defaults to all boards ticked, is remembered for the session, and is applied before the next-N take, not after | D-04/D-05; no existing session-collection precedent — see Common Pitfalls #3 |
| EVTAGENDA-05 | The agenda is reachable from the user dropdown beside Switch Group, visible to every authenticated user regardless of board type or whether any board is currently active | D-08/D-10; exact `_Layout.cshtml`/`_Layout.Mobile.cshtml` insertion points identified below |
| EVTAGENDA-06 | Acting on a row for an event on a board that is not the viewer's active board prompts to switch before proceeding to that event's Details; a row already on the active board skips the prompt | D-11/D-12/D-13; `GroupPickerController.SelectGroup`/`RedirectToLocal` reused unchanged |
| EVTAGENDA-07 | A board the viewer has left never appears on their agenda on the very next request; membership is read fresh from `UserGroups` on every request, never from session or claims | D-15; `GroupRepository.RemoveMemberAsync`/`IGroupService.RemoveMemberAsync` is the "leave" call the D-17 test exercises |
| EVTAGENDA-08 | A SuperAdmin's agenda is scoped by their own `UserGroups` rows exactly like any other user — no unbounded cross-board read | D-09; deliberately does **not** mirror `GroupPickerController.Index`'s `GetAllWithMemberCountAsync` branch |
| EVTAGENDA-09 | The cross-board read never displays another board's events or members, never lets the board filter widen the set beyond the viewer's own memberships, and is proven by a mandatory four-case automated test | D-14/D-16/D-17/D-18; `EventAvailabilityTenantIsolationTests.cs` is the closest sibling test class (see Testing section) |

The planner should confirm the prefix and exact wording with the operator during plan-phase, and add these (or the planner's revision of them) to `.planning/REQUIREMENTS.md` under a new `### Personal Cross-Board Event Agenda` heading, following the existing `- [ ] **ID**: description` format.

</phase_requirements>

## Summary

This phase adds one read-only, read-heavy page: a new repository query starting from `Events` (not `EventSignups`), scoped by the viewer's own membership set rather than the ambient `ActiveGroupId`, feeding a view that lists every upcoming event across every board the viewer belongs to with the full roster inline. Almost everything this phase needs already exists in the codebase in a directly generalisable shape: `EventRepository.GetUpcomingWithSignupsAsync` is the query template, `GroupRepository.GetGroupsForUserAsync` already returns exactly the membership set (`Id`, `Name`, `BoardType`) that both D-15's security predicate and D-04's filter UI need from one call, `GroupPickerController.SelectGroup` is reusable unchanged for D-11/D-13, and `_AvailabilityCell.cshtml` is reusable unchanged for the roster. The one genuinely new mechanic is storing a filter selection (a set of ints) in ASP.NET session — nothing in this codebase does that today; every existing session value is a scalar (`SetInt32`/`SetString`).

The most important fact this research surfaced is **not** a gap in CONTEXT.md's decisions — those are internally consistent and well-reasoned — but a fact about the *current state of the codebase this phase will be built on top of*: **Phase 77 is not finished.** Its own `77-VERIFICATION.md` (still on disk, now superseded by later commits) recorded `status: gaps_found` with a blocking mobile-paging gap and several warnings, four gap-closure plans (`77-05` through `77-08`) have since executed and fixed the blocking gap and most of the warnings, but **two more plans (`77-09`, `77-10`) exist as `PLAN.md` files with no `SUMMARY.md`** — meaning they are queued, not yet executed, as of this research. `77-10` in particular retrofits real keyboard-reachable anchors onto every clickable row/card in the app, including `Events/Index.cshtml`'s own `onclick="window.location.href=...">` row pattern, because that pattern is inaccessible. Phase 82 should **not** copy that pattern (D-12's explicit-control-not-whole-row decision already steers away from it) and should build its own click target as a real anchor/button/form from the start, which happens to make it forward-compatible with whatever `77-10` lands as the app-wide convention. Everything else this research cites from Phase 77's shipped code (`GetUpcomingWithSignupsAsync`, the `TimeProvider`-injected clock, `CanShowMore`, the mobile roster `stopPropagation`) reflects the **current, already-hardened** state after gap closure, not the original `77-01..04` snapshot CONTEXT.md's canonical refs point at — file line numbers below are current as of this research and are called out explicitly where they moved.

**Primary recommendation:** Add one new method to `IEventRepository`/`EventRepository` (e.g. `GetUpcomingAcrossGroupsWithSignupsAsync(memberGroupIds, today, take)`) built by generalising `GetUpcomingWithSignupsAsync`'s exact shape with `IgnoreQueryFilters()` and the membership predicate pinned in immediately, get the membership set from a new `IGroupRepository`/`IGroupService` call that reuses `GetGroupsForUserAsync`'s existing shape, resolve the board name from that same in-memory list (do **not** add `.Include(e => e.Group)` — the domain `Event` model has no `Group` navigation to receive it), store the filter selection as a comma-separated string in session via `Session.SetString`/`GetString` (no JSON precedent exists in this codebase to imitate), and build the switch-prompt as a real POST to `GroupPickerController.SelectGroup` with `returnUrl` set to the event's `Details` URL.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|---|---|---|---|
| Cross-board event query + membership scoping | API / Backend (`EventRepository`, `GroupRepository`) | Database (composite index on `Events(GroupId, Date)` already exists and covers this read) | EF Core query filters and `IgnoreQueryFilters()` are backend-only mechanisms; the membership predicate must be computed and re-verified server-side per D-15/D-16 |
| Board-filter session state | Frontend Server / MVC (session-backed, SQL-Server-distributed-cache since Phase 33) | — | Session is server-side state in this app (cookie carries only the session id); no client-side storage is involved |
| Roster rendering (`_AvailabilityCell`) | Browser / Razor view | — | Pure presentation, already a shared partial with no board-awareness of its own |
| Switch-then-navigate flow | API / Backend (`GroupPickerController.SelectGroup`) | Browser (antiforgery-protected POST + client-side confirm/modal) | The board switch is a session mutation and must happen server-side under CSRF protection; the confirmation UI is presentation only |
| Nav entry visibility | Frontend Server / MVC (`_Layout.cshtml` server-rendered Razor) | — | Nav gating in this app is always server-rendered per-request, never client-side toggling |

No capability in this phase belongs on a CDN/static tier; there are no static assets beyond the existing `modern-card`/`events-overview` CSS this phase should extend, not duplicate.

## Live Codebase State — Read Before Planning

This section exists because CONTEXT.md's canonical refs cite Phase 77 file states that have since moved. Treat everything below as the authoritative current state (verified by direct file read during this research session, at commit `09dbf7d9` on `milestone/v9-rolling-improvements`).

### Phase 77 is mid-flight, not closed

- `77-01` through `77-08` have both a `PLAN.md` and a `SUMMARY.md` in `.planning/phases/77-availability-overview-page/` — executed.
- **`77-09-PLAN.md` and `77-10-PLAN.md` exist with no `SUMMARY.md`** — queued, not yet executed.
- `77-09` (wave 2, depends on `77-05`/`77-07`) hardens the mobile-paging test coverage that let the original blocking gap ship unnoticed, and marks all four `EVTVIEW-*` requirement IDs complete in `.planning/REQUIREMENTS.md`.
- `77-10` (wave 3, depends on `77-05`/`77-09`) retrofits real `<a>` anchors onto **every** clickable row/card app-wide (including `Events/Index.cshtml` and `Index.Mobile.cshtml`, plus nine other views) for keyboard/AT accessibility, and adds a shared CSS class in `modern-card.css` plus a new `RowNavigationAccessibilityTests.cs`. **Implication for Phase 82:** build the D-12 explicit control as a real focusable `<a>`/`<button>` from day one — this is both what D-12 already asks for and what the rest of the app is being retrofitted toward; don't add a bare `onclick` handler that a later phase has to fix again.
- `77-VERIFICATION.md` on disk still shows `status: gaps_found` with a **blocking** gap ("mobile availability overview has no paging control") — this has been fixed by `77-05` (confirmed: `Index.Mobile.cshtml` now reads `Model.CanShowMore`/`Model.NextTake` and renders the control at lines 96-103). Do not plan around the stale `HasMore`-without-ceiling-check behaviour the verification report describes; the current `EventOverviewViewModel.CanShowMore` (`EventOverviewViewModel.cs:26`) is `HasMore && NextTake > Take`, which is the corrected form and the one to imitate if Phase 82's own view model needs an equivalent guard.
- `EventService.GetAvailabilityOverviewAsync` (`QuestBoard.Domain/Services/EventService.cs:41`) now takes an injected `TimeProvider timeProvider` in its constructor and computes `today` as `DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime)` — **not** `DateTime.Today` as CONTEXT.md's citation of Phase 77's original shape implies. This was a gap-closure fix (server-local clock -> injected UTC clock) landed in `77-07`. **Phase 82's new cross-board query/service should follow this exact pattern** (inject `TimeProvider`, use `GetUtcNow().UtcDateTime`) both for consistency and because it is the more correct, more testable choice; there is no reason to reintroduce `DateTime.Today` in new code sitting right next to the method that just moved away from it.
- Also fixed since the verification snapshot: the mobile roster `<div class="collapse" id="roster-@row.EventId">` now carries its own `onclick="event.stopPropagation();"` (`Index.Mobile.cshtml:81`), closing the "tap inside the expanded roster navigates away" bug the verification report flagged as a warning. **Relevant precedent for D-06:** when Phase 82 builds its own mobile tap-to-reveal roster, put `stopPropagation()` on the collapse container itself, not only on the toggle button — the toggle-only version is exactly the shape that shipped broken here.

**Recommendation for the Phase 82 planner:** re-run `git log --oneline -20` and re-read `EventRepository.cs`/`EventService.cs`/`EventsController.cs`/`Index.Mobile.cshtml` immediately before writing plans, in case `77-09`/`77-10` have landed between this research and plan-phase. Nothing in this research depends on `77-09`/`77-10` landing (Phase 82 builds new files/methods rather than editing Phase 77's), but the accessibility convention `77-10` establishes should be followed proactively per the paragraph above.

### STATE.md is stale

`.planning/STATE.md`'s frontmatter says `current_phase: 77`, `status: executing`, `Plan: 1 of 10`, dated `2026-08-29T10:43:39Z`. The actual repository is 14 commits ahead of the snapshot the initial session context described, with 8 of Phase 77's 10 plans already executed and verified (with gap closure). This is a documentation-lag issue for the operator to reconcile, not something Phase 82's plans need to fix, but the planner should not trust STATE.md's phase-77 progress numbers when sequencing Phase 82 work.

### A stale worktree exists

`git worktree list` shows a **locked** worktree at `.claude/worktrees/agent-a04c703798130cbb6` still checked out at the current commit. If this is left over from a completed executor run it should be pruned (`git worktree remove` / `git worktree prune`) before Phase 82 execution starts, so a parallel execution wave does not collide with it. Not a blocker for planning.

## Standard Stack

No new external dependency is needed for this phase. Everything required is already in the project: ASP.NET Core 10 MVC, EF Core 10 (`Microsoft.EntityFrameworkCore.SqlServer`), AutoMapper, the existing session/distributed-cache setup (SQL-Server-backed since Phase 33), and `TimeProvider` (built into .NET, already used by `EventService`). No `npm view`/`pip`/`cargo` verification applies — this is not a JS/Python/Rust phase and no package.json/requirements.txt/Cargo.toml is touched.

### Alternatives Considered

| Instead of | Could use | Tradeoff |
|---|---|---|
| Comma-separated string in session for the filter | `System.Text.Json` serialize a `List<int>` into a session string | JSON is used elsewhere in this codebase only for external API responses (`ResendStatsClient`), never for session; a CSV string matches the codebase's existing `SetString`/`GetString`-only convention for session and needs no serializer dependency. Recommend CSV. |
| `IgnoreQueryFilters()` generalised from `GetUpcomingWithSignupsAsync` | Per-board `ActiveGroupContextService.SetGroupId()` iteration | Explicitly rejected by D-14 (scoped-service mutation inside one HTTP request is unsafe; see `GroupRepository.cs` and `ActiveGroupContextService.cs`) |

## Package Legitimacy Audit

**Not applicable.** This phase installs no new NuGet package, npm package, or any other external dependency. No package-legitimacy check was run because there is nothing to check.

## Architecture Patterns

### System Architecture Diagram

```
[Browser: GET /Agenda?boards=1,3]
        |
        v
[AgendaController.Index]
        |-- reads filter selection from Session (CSV string) -----------------+
        |-- calls IGroupService.GetGroupsForUserAsync(currentUser.Id)         |  (D-15: fresh every request)
        |        -> IList<GroupWithMemberCount> { Id, Name, BoardType }      |
        |-- intersects filter selection with membership set (D-17 case 4)    |
        |-- calls IEventService.GetCrossBoardAgendaAsync(memberGroupIds, take)
        v
[EventService.GetCrossBoardAgendaAsync]
        |-- today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime)
        v
[EventRepository.GetUpcomingAcrossGroupsWithSignupsAsync]
        |-- DbContext.Events.IgnoreQueryFilters()
        |     .Where(e => memberGroupIds.Contains(e.GroupId)
        |               && e.Date >= today && e.CancelledAt == null)
        |-- .OrderBy(Date).ThenBy(StartTime).ThenBy(Id)
        |-- .Take(take + 1)
        |-- .Include(e => e.Signups).ThenInclude(s => s.User)
        |-- .AsNoTracking()
        v
[EventService: build rows, D-16 re-check every row.GroupId is in memberGroupIds]
        |-- board name resolved from the membership list already fetched above
        |   (in-memory dictionary lookup by GroupId -> Name; no second query)
        v
[AgendaController: map to view model, HasMore/NextTake/CanShowMore per Phase 77 pattern]
        v
[Views/Agenda/Index.cshtml + Index.Mobile.cshtml]
        |-- each row: board name, date/time, viewer's own _AvailabilityCell, roster
        |-- explicit control (real <a>/<button>, D-12) ->
        v
[on active-board row: GET Events/Details/{id} directly]
[on other-board row: POST confirm -> GroupPickerController.SelectGroup(groupId, returnUrl=Events/Details/{id})]
        v
[GroupPickerController.SelectGroup: verify membership, set session ActiveGroupId, RedirectToLocal(returnUrl)]
        v
[Events/Details renders the switched-to event; conditional "back to agenda" link if returnUrl indicated agenda origin]
```

### Recommended Project Structure

```
QuestBoard.Domain/
├── Models/
│   ├── AgendaRow.cs              # new: Event + BoardName + roster, generalises EventWithSignups
│   └── AgendaOptions.cs          # new (discretion): DefaultTake/MaxTake/PageIncrement, mirrors EventsOverviewOptions
├── Interfaces/
│   ├── IEventRepository.cs       # add GetUpcomingAcrossGroupsWithSignupsAsync
│   └── IEventService.cs          # add GetCrossBoardAgendaAsync
QuestBoard.Repository/
│   └── EventRepository.cs        # add the new method, generalising GetUpcomingWithSignupsAsync
QuestBoard.Service/
├── Controllers/
│   └── AgendaController.cs       # new (discretion on name) — Index action, reads/writes session filter
├── ViewModels/AgendaViewModels/
│   ├── AgendaViewModel.cs        # new: Rows, HasMore/NextTake/CanShowMore, board filter checklist
│   └── AgendaRowViewModel.cs     # new: EventId, BoardId, BoardName, Title, Date, StartTime, MyCell, Roster
├── Views/Agenda/
│   ├── Index.cshtml              # desktop
│   └── Index.Mobile.cshtml       # mobile, user-agent selected
└── Automapper/ViewModelProfile.cs  # add CreateMap<AgendaRow, AgendaRowViewModel>() etc.
```

### Pattern 1: Membership-pinned `IgnoreQueryFilters()` (D-14)

**What:** Bypass the ambient `ActiveGroupId` filter only while supplying the caller's own membership set explicitly in the same predicate, so the query is strictly narrower than any single board's ambient filter, never broader.

**When to use:** Only for this phase's cross-board read. Every other read in the app should keep using the ambient filter as-is.

**Example (generalising `EventRepository.GetUpcomingWithSignupsAsync`, current file, `QuestBoard.Repository/EventRepository.cs:132-157`):**
```csharp
// Source: QuestBoard.Repository/EventRepository.cs (existing method, generalised per D-14/D-18)
public async Task<IList<EventWithSignups>> GetUpcomingAcrossGroupsWithSignupsAsync(
    IReadOnlyCollection<int> memberGroupIds, DateOnly today, int take, CancellationToken token = default)
{
    // Scope is re-imposed immediately by memberGroupIds, supplied by the caller from a fresh
    // UserGroups read (D-15) -- this bypass is therefore strictly narrower than the ambient
    // filter for any single board, never broader. IgnoreQueryFilters() disables the filter for
    // the whole query, including the Signups and User includes below; that is intended, because
    // every included row hangs off an Event whose GroupId is already pinned to memberGroupIds.
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
If `memberGroupIds` is empty (a SuperAdmin or a viewer with zero memberships, D-09), the `Contains` predicate on an empty collection returns zero rows for every `e.GroupId` — confirm this in a unit test rather than assuming it; EF Core's SQL translation of `List<int>.Contains` over an empty list is provider-dependent in exactly how it renders (`1=0` vs a parameterized `IN`), but the *result* — zero rows — should hold regardless of provider.

### Pattern 2: Board name without touching the `Event` domain model or entity mapping

**What:** `EventEntity.Group` (`QuestBoard.Repository/Entities/EventEntity.cs:44-45`) is a real navigation property, but the **domain** `Event` model (`QuestBoard.Domain/Models/Event.cs`) has no `Group`/`GroupName` field, and `CreateMap<EventEntity, Event>()` maps nothing onto one because it doesn't exist. Adding `.Include(e => e.Group)` to the new query and then trying to flow a board name through the existing `Event` domain model would require either widening `Event` (touching every other consumer of that shared model) or introducing a second EF include with no corresponding domain field to land in.

**Recommended instead:** `GroupRepository.GetGroupsForUserAsync(userId)` (`QuestBoard.Repository/GroupRepository.cs:29-42`) already returns `IList<GroupWithMemberCount>` with `Id` and `Name` for exactly the viewer's own boards — the same call D-15 needs anyway for the membership predicate. Build a `Dictionary<int, string>` from that list once per request and look up each row's board name by `Event.GroupId` in memory when assembling the view model. Zero extra queries, zero N+1 risk, and the `Event`/`EventEntity` mapping stays untouched.

```csharp
// Source: pattern derived from QuestBoard.Repository/GroupRepository.cs:29 (GetGroupsForUserAsync)
var memberships = await groupService.GetGroupsForUserAsync(currentUser.Id, token); // D-15: fresh every request
var memberGroupIds = memberships.Select(m => m.Id).ToList();
var boardNamesById = memberships.ToDictionary(m => m.Id, m => m.Name);
// ... after fetching rows:
var boardName = boardNamesById[row.Event.GroupId]; // safe: D-16's re-check already guarantees membership
```

### Pattern 3: Session-stored filter selection (D-05) — new territory

**What exists today:** `SessionKeys` (`QuestBoard.Service/Constants/SessionKeys.cs`) only ever stores scalars: `ActiveGroupId` (`SetInt32`/`GetInt32`), `ActiveGroupName`/`ActiveGroupValidatedAtUtc` (`SetString`/`GetString`), and a per-group boolean (`ShowHiddenContactsKey`, also `SetInt32`/`GetInt32` as a 0/1 flag). **No code anywhere in this repository stores a collection in session.** `System.Text.Json` is used elsewhere (`ResendStatsClient.cs`) but only for deserializing an external API response, never for session.

**Recommended pattern (new, but consistent with the codebase's existing session idiom):**
```csharp
// New session key, added to QuestBoard.Service/Constants/SessionKeys.cs
public const string AgendaBoardFilter = "AgendaBoardFilter";

// Write: comma-separated ids, matching the SetString/GetString convention already used for
// ActiveGroupName -- no JSON serializer dependency introduced for a value this simple.
HttpContext.Session.SetString(SessionKeys.AgendaBoardFilter, string.Join(',', selectedGroupIds));

// Read: split, parse, and -- critically for D-17 case 4 -- intersect with the FRESH membership
// set before using it as a query predicate. A value left over from a board the viewer has since
// left, or a value that was never validated, must never widen the query.
var stored = HttpContext.Session.GetString(SessionKeys.AgendaBoardFilter);
var requestedIds = string.IsNullOrEmpty(stored)
    ? memberGroupIds // default: all boards ticked
    : stored.Split(',', StringSplitOptions.RemoveEmptyEntries)
        .Select(int.Parse)
        .ToList();
var effectiveGroupIds = requestedIds.Intersect(memberGroupIds).ToList(); // D-17 case 4
```
The intersection step is load-bearing: it is what makes D-17's fourth mandated test case ("the board filter cannot widen the set") true by construction rather than by convention, and it means a stale/left-board id sitting in session from before a `RemoveMemberAsync` call is silently dropped rather than rejected with an error.

### Pattern 4: Reusing `GroupPickerController.SelectGroup` unchanged (D-11/D-13)

**Verified exactly as CONTEXT.md describes**, current file `QuestBoard.Service/Controllers/GroupPickerController.cs:42-73`:
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
The agenda's switch-prompt form should POST to this exact action with `groupId` set to the row's board id and `returnUrl` set to `Url.Action("Details", "Events", new { id = row.EventId })`. Because `Url.IsLocalUrl` only checks that the URL is local (not that it's an events URL), a plain `/Events/Details/123` return URL survives `RedirectToLocal` unchanged. **`Events/Details` has no `.Mobile.cshtml`** (confirmed: only `Details.cshtml` exists under `QuestBoard.Service/Views/Events/`), so D-13's conditional back-link only needs to touch one file.

### Pattern 5: `EventsController.Details`'s current no-guard shape (confirms D-11's premise)

**Verified**, current file `QuestBoard.Service/Controllers/Events/EventsController.cs:53-76`: `Details` calls `eventService.GetEventWithDetailsAsync(id)`, which returns `null` for another board's event because `EventEntity`'s query filter excludes it (`EventRepository.GetEventWithDetailsAsync`, `QuestBoard.Repository/EventRepository.cs:28-34`), and the controller `NotFound()`s. No explicit ownership guard exists in `Details` itself — confirming CONTEXT's D-11 premise exactly. `EventIsOnActiveBoard` (private helper, `EventsController.cs:460-461`) exists and is used by `SetAvailability`/`Withdraw`, **not** by `Details` — this is the guard D-16's own in-memory re-check is deliberately described as weaker than (D-16 compares against the same membership list the query already used; `EventIsOnActiveBoard` compares against independent session state).

### Pattern 6: Mobile tap-to-reveal roster (D-06) — copy the *current*, fixed shape

Current `QuestBoard.Service/Views/Events/Index.Mobile.cshtml:76-93` (after the `77-05`/`77-07` gap-closure fixes, verified by direct read):
```html
<!-- Source: QuestBoard.Service/Views/Events/Index.Mobile.cshtml (current, post-gap-closure) -->
<button type="button" class="btn btn-sm btn-outline-secondary mt-2 avail-expand-toggle"
        data-bs-toggle="collapse" data-bs-target="#roster-@row.EventId"
        onclick="event.stopPropagation();" aria-expanded="false">
    <i class="fas fa-chevron-down me-1"></i>Show players
</button>
<div class="collapse mt-2" id="roster-@row.EventId" onclick="event.stopPropagation();">
    <ul class="list-unstyled mb-0 small">
        @* ... roster rows, each rendering _AvailabilityCell.cshtml ... *@
    </ul>
</div>
```
Note the `onclick="event.stopPropagation();"` on **both** the toggle button and the collapse container div. An earlier version of this exact file (documented in `77-VERIFICATION.md`) had it only on the button, which let a tap inside the *expanded* roster bubble up to the card's own `onclick` and navigate away — copy the two-`stopPropagation()` version, not the one-`stopPropagation()` version, if working from an older reference or from memory of Phase 77's design docs.

**Caution:** the *card* itself in this file still uses `onclick="window.location.href=...">` for its own navigation (line 62), which is the pattern `77-10` (queued) is retrofitting into a real anchor app-wide for accessibility. Phase 82's own row/card should not copy this outer pattern — build D-12's explicit control as a real `<a>`/`<button>` from the start (see the Live Codebase State section above).

### Pattern 7: Nav entry placement (D-08)

Current `QuestBoard.Service/Views/Shared/_Layout.cshtml` (desktop), inside the authenticated branch, user-menu dropdown, immediately around the Switch Group entry:
```html
<!-- Source: QuestBoard.Service/Views/Shared/_Layout.cshtml:196-220 (current) -->
<li class="nav-item dropdown">
    <a class="nav-link dropdown-toggle" ...>@currentUser.Name</a>
    <ul class="dropdown-menu dropdown-menu-end text-wrap" ...>
        <li><a class="dropdown-item" asp-controller="Account" asp-action="Profile">Profile</a></li>
        <li><hr class="dropdown-divider"></li>
        <li>
            <a class="dropdown-item text-wrap" asp-controller="GroupPicker" asp-action="Index">
                @(string.IsNullOrEmpty(activeGroupName) ? "Switch Group" : activeGroupName)
            </a>
        </li>
        <!-- NEW: agenda entry goes here, beside Switch Group, OUTSIDE the
             `activeBoardType is BoardType.OneShot or BoardType.Campaign` gate
             that wraps the separate Calendar dropdown above this block (lines 169-188) -->
        <li><hr class="dropdown-divider"></li>
        <li><form asp-controller="Account" asp-action="Logout" ...>...</form></li>
    </ul>
</li>
```
Mobile equivalent (flat list, confirmed zero `dropdown` occurrences anywhere in `_Layout.Mobile.cshtml`): the Switch Group entry sits at `_Layout.Mobile.cshtml:169-173`; the new entry is a flat `<li class="nav-item">` sibling immediately beside it, following the exact same pattern the Calendar/Availability Overview pair already uses at lines 146-155 of that file. **Do not gate the new entry on `activeBoardType`** — the whole point of D-08/D-10 is that this entry is the one unconditional path in when no board type has resolved.

### Pattern 8: `EventsOverviewOptions`-style options registration (discretion item)

Current registration, `QuestBoard.Domain/Extensions/ServiceExtensions.cs:20` (not `Program.cs` — CONTEXT.md doesn't specify where, and this is the actual location):
```csharp
// Same code-default-plus-configuration shape as EventSeriesOptions above: a
// deployment with no matching configuration section still works.
services.AddOptions<EventsOverviewOptions>().BindConfiguration(EventsOverviewOptions.SectionName);
```
If the planner chooses a separate take default for the agenda (recommended — ten *full-roster* rows is heavier than ten grid rows), add a parallel `AgendaOptions` class and an identical `services.AddOptions<AgendaOptions>().BindConfiguration(AgendaOptions.SectionName);` line in the same file. Per the project's deployment model (server env file, `appsettings.json` shows dev-only defaults — see project memory), **do not** require an `appsettings.json` section for the feature to work; the `EventsOverviewOptions` pattern (code defaults, configuration is optional override) is exactly the shape that avoids a deployment-time server file edit, and D-"Claude's Discretion" already frames it this way.

### Pattern 9: AutoMapper style at both boundaries

Repository boundary (`QuestBoard.Repository/Automapper/EntityProfile.cs:138-146`) and Service boundary (`QuestBoard.Service/Automapper/ViewModelProfile.cs:156-169`) both use `CreateMap<Src, Dest>()` with `.ForMember(...)` only where property names diverge or a value is computed. The closest precedent to what Phase 82 needs is the Phase 77 pair:
```csharp
// Source: QuestBoard.Service/Automapper/ViewModelProfile.cs:165-169 (current)
CreateMap<EventAvailabilityRow, EventOverviewRowViewModel>()
    .ForMember(dest => dest.EventId, opt => opt.MapFrom(src => src.Event.Id))
    .ForMember(dest => dest.Title, opt => opt.MapFrom(src => src.Event.Title))
    .ForMember(dest => dest.Date, opt => opt.MapFrom(src => src.Event.Date))
    .ForMember(dest => dest.StartTime, opt => opt.MapFrom(src => src.Event.StartTime));
```
A new `CreateMap<AgendaRow, AgendaRowViewModel>()` following this exact shape is the natural analogue; `BoardName` cannot come from a `.ForMember(... src.Event....)` projection since (per Pattern 2 above) it isn't on `Event` — it should be set by the controller/service after mapping, from the in-memory `boardNamesById` lookup, exactly as `EventOverviewViewModel.CurrentUserId` is set by the controller rather than mapped (`EventsController.cs:47`).

### Anti-Patterns to Avoid

- **A bare `IgnoreQueryFilters()` with no predicate** — the exact shape of `QuestRepository.GetQuestsForTomorrowAllGroupsAsync` (`QuestRepository.cs:267`), explicitly named by D-14 as not a precedent. This app has shipped two real cross-tenant leaks this way (Phases 49/55) plus a third live gap found during Phase 72's discussion.
- **Per-board `ActiveGroupContextService.SetGroupId()` iteration inside one HTTP request** — unsafe because the service is scoped and mutating it rewrites the ambient board for everything downstream in the same request (`_Layout`, `IBoardTypeResolver`). Safe only in Hangfire jobs, which open a fresh DI scope per board via `HangfireJobHelper.RunInScopeAsync`.
- **Reading the membership set from session or claims** — explicitly rejected by D-15; both create a window where a left board's data is still reachable.
- **Copying the outer card's `onclick="window.location.href=...">` pattern** from Phase 77's current `Index.cshtml`/`Index.Mobile.cshtml` — inaccessible, and mid-retrofit (`77-10`) into real anchors app-wide.
- **Adding `.Include(e => e.Group)` to feed a board name through the `Event` domain model** — the model has no field to receive it; use the already-fetched membership list instead (Pattern 2).
- **JSON-serializing the filter selection into session** — no precedent in this codebase; a CSV string matches the existing `SetString`/`GetString`-only convention.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---|---|---|---|
| Membership-scoped cross-board query | A raw SQL query or a manually-joined LINQ query bypassing EF's filter machinery entirely | `IgnoreQueryFilters()` + explicit predicate, generalising `EventRepository.GetUpcomingWithSignupsAsync` | EF Core's filter machinery already handles the Signups/User include correctly once `IgnoreQueryFilters()` is applied at the root; a raw SQL query would have to re-implement the cancelled/date logic and lose the `Include`-based eager-load shape entirely |
| Board membership check | A new "is member of any of these groups" helper on `IUserService` | `IGroupService.GetGroupsForUserAsync` — already returns exactly this, already used by `GroupPickerController.Index` | One existing, tested call serves both D-15's predicate and D-04's filter UI; a second helper would be a second thing to keep correct |
| Antiforgery-protected board switch | A new controller action that sets `ActiveGroupId` | `GroupPickerController.SelectGroup` (unchanged) | It already does membership verification, antiforgery, and `returnUrl` handling; D-11/D-13 explicitly mandate reusing it rather than inventing a second way to set the active board |
| Cell rendering (Yes/Maybe/No/muted-Yes/empty) | A new partial or a re-derivation of the classification logic | `_AvailabilityCell.cshtml` + `EventService`'s (private, but pattern-copyable) `ClassifyCell` logic, keyed only on `HasAnswered` | Already ships, already tested, already the single source of the five-state vocabulary across both existing surfaces; a second implementation risks the two surfaces drifting apart on what a cell looks like |

**Key insight:** almost nothing in this phase is new mechanism — it's new *composition* of existing mechanisms (the ignore-filters-plus-predicate shape, the roster eager-include shape, the switch-group POST, the cell partial) applied to a wider input (a set of group ids instead of one). The one place with zero precedent is session-stored collection state (Pattern 3), and even that should follow the codebase's existing "primitive values only" session convention rather than reaching for a new serialization dependency.

## Common Pitfalls

### Pitfall 1: Treating `IgnoreQueryFilters()` as scoped to the root entity only

**What goes wrong:** Assuming the `Signups`/`User` includes still get filtered by their own `HasQueryFilter` even though the root `Events` query called `IgnoreQueryFilters()`, and adding a redundant manual filter on the included navigations "just in case" — which would then filter *out* legitimate cross-board rosters and silently break D-02.
**Why it happens:** It's counter-intuitive that ignoring filters on one entity type in a query also ignores them on entity types reached only through `Include`.
**How to avoid:** This is confirmed EF Core behavior (global query filters are applied to entity types reached via navigation properties the same as the root, and `IgnoreQueryFilters()` disables filters for every entity type touched by that query) — trust it, and write the comment CONTEXT.md's D-14 already asks for explaining why it's safe here specifically (every included row hangs off an `Event` whose `GroupId` is already pinned).
**Warning signs:** A roster that's mysteriously empty for events on a second board even though the membership predicate is correct.

### Pitfall 2: Forgetting the D-17-case-4 intersection when reading the session filter

**What goes wrong:** Reading the stored CSV filter and passing it straight into the query's predicate without intersecting it against the *current* membership set. A viewer who ticks boards A and B, then leaves B, then reloads the agenda, would have a stale `AgendaBoardFilter=1,2` sitting in session; if that's used un-intersected, and if (hypothetically) a future code path ever let a group id reach the predicate without the D-14 pin also applying membership, board B's data could resurface through the filter parameter alone.
**Why it happens:** Session data outlives the state it was computed from; nothing invalidates it when membership changes.
**How to avoid:** Always compute `effectiveGroupIds = requestedIds.Intersect(memberGroupIds)` — never trust the session value as anything more than a hint about which of the *current* memberships to show.
**Warning signs:** D-17's fourth test case (filter cannot widen the set) is the direct regression guard for this; it must actually seed a stale/foreign id in the filter and prove it's ignored, not merely prove the happy path filters correctly.

### Pitfall 3: No existing precedent for session collections — inventing an inconsistent one

**What goes wrong:** Reaching for `System.Text.Json` (used elsewhere for `ResendStatsClient`'s API deserialization) to serialize the filter selection, when nothing else in the session layer uses JSON, creating a one-off pattern a future reader has to learn just for this feature.
**Why it happens:** JSON is the obvious general-purpose answer, and it *is* present in the codebase, just for an unrelated purpose.
**How to avoid:** Use a comma-separated string via the existing `SetString`/`GetString` idiom (Pattern 3). It's simpler, has no serialization edge cases (no escaping needed for a list of ints), and matches every other session value in `SessionKeys.cs`.
**Warning signs:** A `using System.Text.Json` import appearing in a controller that otherwise has no JSON needs.

### Pitfall 4: Copying Phase 77's per-column highlight CSS bug

**What goes wrong:** `wwwroot/css/events-overview.css`'s `.avail-col-self` rule (meant to highlight the viewer's own column) has no `!important` and specificity (0,1,0), so it's unconditionally overridden by `modern-card.css`'s `.modern-card .table th`/`td` `!important` rules — the highlight silently never paints (flagged as a warning in `77-VERIFICATION.md`, not yet confirmed fixed by the gap-closure waves this research reviewed). Phase 82's own "highlight the viewer's own roster entry" discretion item (if taken) risks the identical bug if it reuses the same CSS approach without checking specificity against `modern-card.css`'s existing `!important` rules.
**Why it happens:** `modern-card.css`'s table styling was written generically and its `!important` rules were not anticipated to need an escape hatch for a later feature.
**How to avoid:** If a highlight is added, verify its selector specificity against `modern-card.css`'s rules directly (grep for `!important` in that file) before assuming a plain class selector will paint.
**Warning signs:** The class is present in the rendered HTML but no visual difference appears.

### Pitfall 5: Building the row's click target as `onclick` instead of a real anchor

**What goes wrong:** Copying Phase 77's current whole-card `onclick="window.location.href=...">` pattern for D-12's explicit control, producing a control that isn't keyboard-reachable — precisely the defect `77-10` (queued) is retrofitting out of the rest of the app.
**Why it happens:** It's the closest visible precedent in the sibling view, and D-12 doesn't spell out the HTML mechanism, only that the target must not be the whole row.
**How to avoid:** Implement D-12's control as a real `<a>` (same-board case, direct link to Details) or a real `<button type="submit">` inside a small `<form>` (other-board case, POSTs to the switch-prompt flow) — both are natively focusable and require no extra work to satisfy `77-10`'s eventual app-wide convention.
**Warning signs:** The control renders as a `<span>`/`<div>` with only an `onclick` attribute and no `href`/`tabindex`.

## Code Examples

See Architecture Patterns 1-9 above — each includes a verified, current-state code excerpt with its exact source file and line numbers rather than a hypothetical example, per this phase's emphasis on codebase-grounded reuse over invented patterns.

## State of the Art

Not generally applicable — this is an internal-conventions-driven phase in a single, actively-maintained codebase, not a public-ecosystem library choice. The one relevant "old approach / current approach" pair is internal to this project:

| Old Approach | Current Approach | When Changed | Impact |
|---|---|---|---|
| `DateOnly.FromDateTime(DateTime.Today)` (server-local clock) in `EventService.GetAvailabilityOverviewAsync` | `DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime)` (injected `TimeProvider`, UTC) | Phase 77 gap-closure plan `77-07` (landed after the `77-01..04` snapshot CONTEXT.md's canonical refs describe) | Phase 82's new cross-board query/service should use the same injected-`TimeProvider`/UTC pattern from the start, not the older server-local-clock shape |
| Toggle-button-only `stopPropagation()` on the mobile roster collapse | `stopPropagation()` on both the toggle button and the collapse container | Phase 77 gap-closure (landed after the original design) | Copy the two-`stopPropagation()` version for D-06's mobile disclosure |

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|---|---|---|
| A1 | `List<int>.Contains(...)` translates to a working, empty-set-safe SQL predicate against the EF Core InMemory provider used by this app's integration test suite, matching production SQL Server behaviour closely enough that a green integration test is trustworthy evidence | Pattern 1 | If the InMemory provider handles an empty `memberGroupIds` list differently from SQL Server (e.g. throws vs. returns zero rows), a passing D-17 test could mask a production-only bug; recommend the planner add an explicit unit test (not just integration) for the zero-membership case if repository-level unit tests are already part of this codebase's convention |
| A2 | `77-09`/`77-10` will not have landed and changed `Events/Index.cshtml`'s click pattern or `EventOverviewViewModel`'s shape by the time Phase 82 is planned/executed | Live Codebase State | Low — Phase 82 builds new files rather than editing Phase 77's, so this mostly affects which convention to imitate, not whether Phase 82's own code compiles; re-verify immediately before planning per the recommendation in that section |
| A3 | The recommended `EVTAGENDA-*` requirement ID prefix and the nine proposed requirement statements are an acceptable minting for this phase | Phase Requirements | Low — explicitly flagged as a starting proposal for the planner/operator to confirm, not a locked decision |

## Open Questions

1. **Will `77-09`/`77-10` land before or during Phase 82's own execution window?**
   - What we know: both exist as unexecuted `PLAN.md` files, sequenced as `wave: 2`/`wave: 3` gap-closure plans depending on already-executed plans.
   - What's unclear: whether the operator will run them before starting Phase 82, interleaved, or after.
   - Recommendation: build Phase 82's views with real anchors/buttons from the start (Pattern 5/Anti-Pattern) so the answer doesn't matter — Phase 82's own files are new, not edits to files `77-10` touches.

2. **Does the EF Core InMemory test provider handle `Contains` over an empty `List<int>` identically to SQL Server?**
   - What we know: both should return zero rows in principle.
   - What's unclear: not independently verified against this specific provider version (`Microsoft.EntityFrameworkCore.InMemory v10.0.9`) in this session.
   - Recommendation: the planner should have the executor add a direct unit or integration test for the zero-membership case (D-09's SuperAdmin-with-no-memberships path) rather than relying solely on inference from provider documentation.

## Validation Architecture

### Test Framework

| Property | Value |
|---|---|
| Framework | xUnit v3.2.2 (integration), same runner for unit tests |
| Config file | `QuestBoard.IntegrationTests/xunit.runner.json` (serial execution: `parallelizeAssembly: false`, `parallelizeTestCollections: false` — required because tests share one in-memory database per factory) |
| Quick run command | `dotnet test --filter "FullyQualifiedName~Agenda"` (adjust to whatever class name the planner picks) |
| Full suite command | `dotnet test` |

### Phase Requirements -> Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|---|---|---|---|---|
| EVTAGENDA-01/03 | Cross-board rows with roster, no-signup-row events included | integration | `dotnet test --filter "FullyQualifiedName~AgendaControllerIntegrationTests"` | Wave 0 (new file, no existing equivalent) |
| EVTAGENDA-04 | Board filter applied before take, session-remembered | integration | same as above, additional `[Fact]`s | Wave 0 |
| EVTAGENDA-05 | Nav entry visible unconditionally | integration | `dotnet test --filter "FullyQualifiedName~LayoutNavigationTests"` | Existing file, add new `[Theory]` cases following the `Nav_*_AvailabilityOverviewLinkPresent` shape — but note the **new** case needed where `BoardType` is null/unresolved (no existing test sets this; every current case sets `OneShot` or `Campaign` explicitly) |
| EVTAGENDA-06 | Switch-prompt POST, active-board skip | integration | new test class | Wave 0 |
| EVTAGENDA-07/09 | Left-board disappears, cannot widen via filter | integration | new class, generalising `EventAvailabilityTenantIsolationTests.cs`'s seeding helpers | Wave 0 (D-17 mandates this explicitly) |
| EVTAGENDA-08 | SuperAdmin scoped by own memberships | integration | new `[Fact]` using `CreateAuthenticatedSuperAdminClientAsync` with zero seeded memberships | Wave 0 |

### Sampling Rate

- **Per task commit:** targeted `dotnet test --filter` on the new test class(es)
- **Per wave merge:** `dotnet test QuestBoard.IntegrationTests` plus `dotnet test QuestBoard.UnitTests`
- **Phase gate:** full `dotnet test` green before `/gsd-verify-work`; also re-run `grep -c 'IgnoreQueryFilters' <new repository file>` and confirm exactly 1 (the one deliberate D-14 call), following the exact audit pattern `77-VERIFICATION.md` used against `EventRepository.cs`/`EventService.cs`/`EventsController.cs`

### Wave 0 Gaps

- [ ] A new integration test file generalising `EventAvailabilityTenantIsolationTests.cs`'s seeding helpers (`SeedOtherBoardEventAsync`, `SeedSignupAsync`) to a **third** board and a user with memberships in two of three — the harness supports this today via direct `factory.Database.CreateContext()` writes (no harness change needed), but no existing test class exercises three boards simultaneously.
- [ ] A "leave a board" integration helper/pattern — no existing test calls `IGroupService.RemoveMemberAsync`/`GroupRepository.RemoveMemberAsync` directly; it is reachable via `scope.ServiceProvider.GetRequiredService<IGroupService>()` inside a test, following the same DI-scope pattern `EventAvailabilityTenantIsolationTests.Roster_ForGroupOneEvent_ContainsOnlyGroupOneMembers` already uses for `IEventSignupService`.
- [ ] A `LayoutNavigationTests` case with `BoardType = null`/unresolved (not `OneShot`/`Campaign`) asserting the new nav entry is still present — no existing case in that file exercises an unresolved board type at all; the harness's `MutableGroupContext.BoardType` field is nullable (`BoardType?`) so this is directly settable, just not currently exercised.
- [ ] Framework install: none — xUnit/FluentAssertions/NSubstitute already referenced by both test projects.

## Security Domain

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---|---|---|
| V2 Authentication | No (new behaviour) | Existing `[Authorize]` on the controller, same as `EventsController` |
| V3 Session Management | Yes | Existing SQL-Server-backed distributed session (Phase 33); new session key follows the existing `SessionKeys` pattern, no new session mechanism introduced |
| V4 Access Control | **Yes — the load-bearing category for this entire phase** | Membership-pinned `IgnoreQueryFilters()` (D-14), fresh-per-request membership read (D-15), second in-memory re-check (D-16), mandatory four-case test (D-17) — this is the app's first user-facing read to deliberately bypass the ambient tenant filter, so every future cross-board feature will be reviewed against the precedent this phase sets |
| V5 Input Validation | Yes | The board-filter query-string/session value must be validated as a set of ints belonging to the viewer's own memberships (Pitfall 2's intersection step) before use in any predicate |
| V6 Cryptography | No | Not applicable — no new cryptographic operation in this phase |

### Known Threat Patterns for this stack

| Pattern | STRIDE | Standard Mitigation |
|---|---|---|
| Cross-tenant data disclosure via a widened or bypassed query filter (this app's own repeat history: Phases 49, 55, and a third gap found during Phase 72's discussion) | Information Disclosure | `IgnoreQueryFilters()` used only with an explicit, caller-supplied predicate that is itself derived from a fresh authorization read (D-14/D-15), never from client-supplied state alone |
| Stale authorization state surviving a privilege change (leaving a board) | Information Disclosure / Elevation of Privilege | Membership read fresh every request, never cached in session or claims (D-15); the filter's stored selection is always intersected against the fresh set, never trusted alone (Pitfall 2) |
| CSRF on the board-switch action | Tampering | Already covered — `GroupPickerController.SelectGroup` carries `[ValidateAntiForgeryToken]`; reused unchanged, not reimplemented |
| Open redirect via a crafted `returnUrl` | Tampering / Spoofing | Already covered — `RedirectToLocal`'s `Url.IsLocalUrl` guard is reused unchanged |

## Sources

### Primary (HIGH confidence — direct codebase read, current commit `09dbf7d9`)
- `QuestBoard.Repository/EventRepository.cs`, `QuestBoard.Repository/EventSignupRepository.cs`, `QuestBoard.Repository/GroupRepository.cs` — query shapes generalised in this research
- `QuestBoard.Repository/Entities/QuestBoardContext.cs` — full global query filter block
- `QuestBoard.Repository/Entities/EventEntity.cs`, `QuestBoard.Domain/Models/Event.cs` — confirmed `Group` navigation exists on the entity but not the domain model
- `QuestBoard.Service/Controllers/Events/EventsController.cs`, `QuestBoard.Service/Controllers/GroupPickerController.cs` — controller patterns reused
- `QuestBoard.Domain/Services/EventService.cs` — current `TimeProvider`-injected shape
- `QuestBoard.Service/Views/Events/Index.cshtml`, `Index.Mobile.cshtml`, `_AvailabilityCell.cshtml`, `_AvailabilityCounts.cshtml` — current, post-gap-closure state
- `QuestBoard.Service/Views/Shared/_Layout.cshtml`, `_Layout.Mobile.cshtml` — current nav structure and line numbers
- `QuestBoard.Service/Constants/SessionKeys.cs`, `QuestBoard.Service/Middleware/MobileDetectionMiddleware.cs`, `QuestBoard.Service/ViewExpanders/MobileViewLocationExpander.cs` — session and mobile-detection mechanisms
- `QuestBoard.IntegrationTests/Tests/EventAvailabilityTenantIsolationTests.cs`, `WebApplicationFactoryBase.cs`, `Helpers/MutableGroupContext.cs`, `Helpers/AuthenticationHelper.cs`, `Controllers/LayoutNavigationTests.cs` — test harness capabilities confirmed
- `.planning/phases/77-availability-overview-page/77-VERIFICATION.md`, `77-09-PLAN.md`, `77-10-PLAN.md`, and `git log`/`git worktree list` — Phase 77's actual, in-progress state
- `.planning/codebase/TESTING.md` — test conventions

### Secondary (MEDIUM confidence)
- [Global Query Filters - EF Core | Microsoft Learn](https://learn.microsoft.com/en-us/ef/core/querying/filters) and corroborating search results — confirms `IgnoreQueryFilters()` disables filters for every entity type reached by the query, including navigation-included ones (Pattern 1 / Pitfall 1)

### Tertiary (LOW confidence)
- None relied upon as load-bearing; all package-free, internal-conventions claims were verified directly against source rather than inferred.

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — no new dependency, entirely internal-conventions research
- Architecture: HIGH — every pattern cited traces to a specific, current-state file and line range verified in this session
- Pitfalls: HIGH — five of five pitfalls trace to an already-observed precedent in this exact codebase (either a Phase 77 gap-closure fix or an explicitly-rejected Phase 82 alternative in CONTEXT.md)
- Live-state currency: MEDIUM — accurate as of commit `09dbf7d9`; flagged explicitly that `77-09`/`77-10` may land before Phase 82 planning starts and should be re-checked

**Research date:** 2026-08-29
**Valid until:** Re-verify the Live Codebase State section immediately before planning if more than a few days elapse, or if `git log` shows new commits on `77-09`/`77-10`; the rest of this research (query shape, session pattern, AutoMapper style, security patterns) is stable against the project's established conventions and does not need re-verification absent a conventions change.
