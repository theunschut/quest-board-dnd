# Phase 82: Personal Cross-Board Event Agenda - Context

**Gathered:** 2026-08-29
**Status:** Ready for planning

<domain>
## Phase Boundary

One **read-only** page listing, one row per event, every upcoming event across **all boards the logged-in viewer is a member of** — with the board named on every row, the viewer's own availability on it, and the event's full roster carried inline. Rows are ordered chronologically and interleaved across boards; a board-selection filter narrows the set. Reached from the user dropdown beside **Switch Group**, deliberately outside the Calendar nav's board-type gate, because a cross-board page has no active board type.

The page needs no active board. Its scoping comes entirely from the viewer's own memberships, read fresh at request time — never from a bare filter bypass, never from the existence of a signup row, and never from cached or claim-carried state.

This is **not** Phase 77's grid. Across boards there is no single membership set, so the events × members matrix does not generalise and the member column axis is not ported. A roster carried *inside* each row is a different thing and does generalise, because each event's roster is just that event's own board's members.

Not in this phase: quests in any form (roadmap-excluded); any write to availability from this page (the switch-then-Details path covers it); any change to Phase 77's board-scoped overview, which stays the board-scoped page it was built as; any change to the login or landing path.

</domain>

<decisions>
## Implementation Decisions

### What lands on the agenda

- **D-01: Every upcoming event on every board the viewer belongs to, whether or not a signup row exists.** Not "events I hold a row on", and not "events I have answered".

  This is the only rule that reads the same on both board types. Campaign boards are opt-out — joining backfills a `Yes` on every future event with `HasAnswered` false (Phase 75 D-15/D-19) — so a row exists on everything but almost nothing has been answered. One-Shot boards are opt-in — no row exists until you answer (Phase 75 D-03) — so scoping to rows would silently hide exactly the events a personal agenda exists to surface. Either row-based rule would make the page mean something different on each of the viewer's boards.

  **Structural consequence, load-bearing:** the query must start from `Events`, not from `EventSignups` as Phase 77's did, with the viewer's own signup left-joined. Phase 77's `GetUpcomingWithSignupsAsync` cannot be widened into this — it is a new query, not a variant.

- **D-02: The row carries the full event payload, including the complete roster** — as if the event were opened from its own board. Title, date/time, board name, the viewer's own availability, and every member's availability on that event.

  The operator's case is two campaigns that alternate sessions; the value of the page is seeing both parties' answers in one chronological list, which neither board can show on its own.

  **Accepted cost, stated:** this widens the read from "my rows" to "every member's rows on every board I belong to". It is **not** a privilege escalation — every one of those rosters is already visible to the viewer on that board's own `Events/Details` page — but it makes the cross-tenant read (D-14…D-18) the load-bearing part of this phase rather than a formality. Phase 77 D-16's accepted cost ("aggregating makes patterns legible that the per-event views did not") now applies *across boards*.

  Rejected: the viewer's own answer alone (narrowest read, but loses the reason the page was asked for); own answer plus aggregate counts, or plus a headcount (same widening of the read, less payoff than the full roster).

- **D-03: A global next-N across all boards, chronological — not N per board.** Ten events in total, interleaved by date.

  Confirmed deliberately after advice. Phase 77 D-09 bounded the page so its size was not set by data the page does not control; a per-board N reintroduces exactly that, with page length scaling by board count and "show more" becoming ambiguous about which board it extends.

  The failure mode of a global N is a cadence mismatch — a weekly board burying a monthly one below the fold. **D-04's filter is the release valve for it**, and that is why the per-board-floor option was rejected rather than merged: an explicit control the reader can see and act on beats a hidden quota that silently reshapes the list.

  **Accepted cost, stated:** a high-cadence board can dominate page one. Paging and the filter are the answers; a quota is not.

- **D-04: A board-selection filter on the page, defaulting to all boards ticked.** In scope as a view control over the page's own data set — the same category as Phase 77's paging, not a new capability.

  **It must apply before the take, not after.** Filtering changes *which* events fall inside the global next-N, so filtering after the window would return fewer than N rows from an already-narrowed set instead of pulling the next ones in.

- **D-05: The filter selection is remembered for the session**, stored the way `ActiveGroupId` already is — ASP.NET session, which since Phase 33 is backed by the SQL Server distributed cache, so it survives app restarts but not logout or a different device.

  No migration, no new table, and it reuses the exact mechanism the Switch Group control beside it already uses. Rejected: a per-user persisted preference (needs a migration and a defined behaviour when the viewer joins or leaves a board after saving); no memory at all (the operator explicitly asked to avoid re-ticking every visit).

- **D-06: The roster is visible inline on desktop and behind a tap on mobile.** Mirrors Phase 77 D-17/D-18 exactly — same split, same reasoning, no new idiom on either surface. Ten full rosters is a long desktop page but a scannable one; on a phone it is unusable.

  Mobile is a real `.Mobile.cshtml` selected by user agent, as Phase 77 D-17 established. **Devtools viewport emulation will never exercise it — verification needs a real mobile user agent.**

### Where it lives and what it replaces

- **D-07: The agenda supplements; it does not change where anyone lands.** The login path is untouched: `GroupPicker` then `Quest/Index`, exactly as today.

  This was the roadmap's first mandated question. Landing a multi-board user on the agenda was genuinely tempting — the picker is a forced interstitial that exists *only* for people with more than one board, and the agenda is the only surface in the app that needs no active board, so it is the only thing that could stand in front of it. It was declined because it edits `GroupPickerController`'s redirect, a path every single login goes through including single-board users, for a benefit this phase does not need in order to be complete. The roadmap's scope note points the same way.

  Rejected outright: making the agenda the group picker's replacement, where clicking a row both switches board and opens the event. That folds D-11's decision into the landing decision and couples the page to session board state.

- **D-08: The nav entry is visible to every authenticated user — no board-count gate.** Same as the Switch Group entry it sits beside, which renders for everyone today.

  A single-board viewer gets a chronological list of their events with rosters inline, which is genuinely different from Phase 77's grid rather than dead weight. Gating on a count would put a membership query in `_Layout.cshtml` on every page render for every user, or a new session value to keep in sync across every join and leave path — real cost for a cosmetic gain.

- **D-09: A SuperAdmin sees only the boards they are actually a member of.** One rule for everyone; the agenda is scoped by `UserGroups` rows, full stop.

  `GroupPicker` hands a SuperAdmin every group (`GetAllWithMemberCountAsync`), and mirroring that here would turn this page into an unbounded read over every event in the application — precisely the shape the roadmap names as this phase's highest risk. Keeping one rule means there is no privileged branch on the app's widest read for anyone to weaken later. A SuperAdmin with no memberships gets the empty state.

  Rejected: a SuperAdmin-only toggle to widen the set (both behaviours to build and test, with the wide one reachable by query string on the most sensitive read in the app).

- **D-10: Phase 77's board overview and the calendar both cross-link to the agenda**, in addition to the dropdown entry.

  **Note for the planner:** both of those surfaces sit behind the `activeBoardType is BoardType.OneShot or BoardType.Campaign` gate (`_Layout.cshtml:168`, `_Layout.Mobile.cshtml:144`), so those two links only exist once a board type has resolved. **The user-dropdown entry is therefore the unconditional path in, and is what keeps the page reachable when no board is active.** Do not let the cross-links become the only route.

### Crossing the board boundary

- **D-11: Acting on a row for an event on a non-active board prompts before switching.** "This session is on *Board*. Switch to it?" — an antiforgery-protected post that sets `ActiveGroupId` and redirects to that event's `Details`. Rows already on the viewer's active board skip the prompt and go straight through.

  The facts this settles: `EventsController.Details` has no explicit guard — it calls `GetEventWithDetailsAsync(id)`, the ambient filter returns null for another board's event, and it `NotFound()`s. So a plain link from the agenda to a non-active board's event is a 404 today.

  Because D-02 puts the full roster on the row, the *only* thing `Details` still offers that the row does not is changing the viewer's own answer. So this decision is "how do I reply to an event on a board I am not currently in", not "how do I see more".

  Rejected: silently switching (`ActiveGroupId` drives quests, shop, gold, characters and the whole nav — following a link would move the viewer's entire app context to the other campaign without asking, and setting session state as a side effect of a GET breaks back-button semantics); answering inline on the agenda (would make Phase 75 D-01's single-availability-surface rule false and require replacing `EventIsOnActiveBoard`, a guard that exists specifically to stop cross-board writes); non-clickable rows (a dead end for the one action the page makes the reader want).

  **Reuse `GroupPicker`'s existing `SelectGroup` path rather than inventing a second way to set the active board.**

- **D-12: The click target is an explicit control on the row, not the whole row.** Phase 77 D-24 made the whole row a target because the row was one compact grid line. A row that now carries a roster is a different object: a full-row target swallows text selection, and its consequence here is a board switch rather than a plain navigation — one stray click away is the wrong distance for that.

  This is a deliberate divergence from Phase 77 D-24/D-25, not an oversight. Recorded as such so a later reader does not "restore consistency".

- **D-13: `Details` carries a way back to the agenda when the viewer arrived from it.** `GroupPickerController.SelectGroup` already threads a `returnUrl` through `RedirectToLocal`, so the round trip needs no new mechanism.

  Without it, answering one event on the other campaign strands the viewer on that board with no signposted route back to the list they were working through. **The active board is deliberately *not* switched back on return** — switching twice per answer is state that is easy to get wrong and hard to notice when it is.

### The cross-tenant read and its proof

- **D-14: One query, `IgnoreQueryFilters()` with the viewer's membership set pinned in the predicate.** `memberGroupIds.Contains(e.GroupId) && e.Date >= today && e.CancelledAt == null`, ordered by date / start time / id, global take in SQL.

  This is `GroupRepository.GetEventSignupsForMemberIgnoringActiveBoardAsync`'s shape generalised from one group id to a set — the roadmap's rule exactly: *bypass the ambient filter only while supplying the group explicitly*. Scope is re-imposed by the argument immediately, so the query is strictly narrower than the ambient filter would be for any single board, not broader. One round trip, no request-scoped state mutated.

  Rejected: **per-board `SetGroupId` iteration.** `ActiveGroupContextService` is a *scoped* service. Hangfire gets away with per-group iteration because `HangfireJobHelper.RunInScopeAsync` opens a fresh DI scope per board; inside one HTTP request, mutating it rewrites the ambient board for everything downstream — including `_Layout`'s `activeGroupName` and `IBoardTypeResolver`. It also cannot express a global take: it would fetch N from each board and merge, which is one query per board and reintroduces the per-board-N shape D-03 rejected. Rejected also: per-board iteration in child DI scopes — structurally the safest reading of that mechanism, but it means opening N scopes and N DbContexts to render one page, a shape this app has never used inside a request.

  **Explicitly not a precedent to copy:** `QuestRepository.GetQuestsForTomorrowAllGroupsAsync` (`QuestRepository.cs:267`) is a **bare** `IgnoreQueryFilters()` with no group predicate at all. It is a background-job read for tomorrow's reminders. It is a third mechanism the roadmap did not count, and it is exactly the shape this page must not have.

  **Note for the planner:** `IgnoreQueryFilters()` disables the filter for the whole query, including the `Signups` and `User` includes. That is intended here — the rosters must come back — and it is safe because every included row hangs off an `Event` whose `GroupId` is pinned to the membership set. Say so in the comment, because it will look alarming otherwise.

- **D-15: The membership set is read fresh from `UserGroups` on every request.** Never from session, never from claims.

  This is what closes the roadmap's second named risk directly: *"Leaking board names or event titles from a board the viewer has left. Membership is the authorisation, and it is checked at read time — not inferred from the existence of a signup row."* Leave a board and its events, titles and name are gone on the next page load, not whenever a cache happens to expire. It costs one indexed lookup — the same one `GroupPicker` already does.

  Rejected: caching it in session beside `ActiveGroupId` (creates a window where a left board is still in the cached set, and every join and leave path in the app would have to remember to invalidate it); carrying it in claims (only refreshes when the cookie is reissued, so a stale set can outlive a leave by the full cookie lifetime).

  **This matters more under D-01 than it would have otherwise:** the query starts from `Events`, so a left board's event rows still exist and still match the date predicate. Only the membership check removes them. Phase 75 D-20's signup-row deletion on leave is cleanup, not access control, and does nothing for this page.

- **D-16: A second in-memory layer, with its limits written down.** After materialization, re-check every row's `GroupId` against the membership set and fail closed if one is not in it.

  Phase 75 D-28/D-29 established defence in both layers and this is cheap. But the code comment must state plainly what it does and does not cover: **both layers read the same `memberGroupIds` list**, so it catches a dropped predicate or a bad `Contains` translation — not a wrong membership set. This is weaker than `EventIsOnActiveBoard`, which compares against independent session state. A guard whose limits are recorded does not get mistaken later for one that covers more.

  Rejected: a second, separately-read membership set to make the layers genuinely independent — two membership queries per page load and two sources that can disagree, with no defined behaviour when they do.

- **D-17: The mandatory integration test proves four things, not one.** Follow `QuestBoard.IntegrationTests/Tests/EventAvailabilityTenantIsolationTests.cs`, and **reset `ActiveGroupId` to `1` in `DisposeAsync`** — `WebApplicationFactoryBase.TestGroupContext` is a shared singleton `MutableGroupContext` defaulting to 1, so the standard harness is structurally blind to this bug class.

  1. **A non-member board is fully absent.** Viewer belongs to A, not B: none of B's event titles, member names, or board name reach the page.
  2. **Two joined boards both appear, and a third does not.** Viewer belongs to A and B but not C: A's and B's events both render, interleaved by date; C's do not. **Without this case, a suite that only proves absence stays green if the aggregation silently collapses to a single board** — breaking the entire feature while proving perfect isolation.
  3. **A board you left disappears.** Viewer was a member of B, held signups there, then left: B's events and board name are gone. Named by the roadmap, and load-bearing under D-01/D-15 as explained above.
  4. **The board filter cannot widen the set.** Requesting the agenda with a board id the viewer is not a member of in the filter selection is ignored or rejected, never honoured. The filter narrows the viewer's own memberships; it is never an input that adds to them.

- **D-18: The query must not N+1 across events × signups × users × groups.** `EventSignupRepository.GetRosterForEventAsync` (`EventSignupRepository.cs:60`) is the single-query eager-include shape to generalise from — one round trip, ordered in SQL so the view does not re-sort. This page adds the group join for the board name; it must not become a per-row lookup.

### Claude's Discretion

Not discussed — planner decides:

- **The page's name, icon, route, and controller home.** `EventsController.Index` is taken by Phase 77's board-scoped overview, so this needs its own action or its own controller. Neither is obviously right.
- **Whether the agenda gets its own take default rather than sharing `EventsOverviewOptions.DefaultTake = 10`.** Ten *full-roster* rows is a much heavier page than ten grid rows — same options pattern, likely a separate value. A `MaxTake` ceiling is still required so a client-supplied page size cannot go unbounded, as Phase 77's `Math.Clamp` does.
- **How the board name is rendered on a row** — plain text, badge, per-board colour chip, with or without the board type. It must be on every row (roadmap requirement), but its treatment is open.
- **Whether the list carries day or date group headers**, or whether each row is fully self-contained.
- **Empty-state copy** for three distinct cases: the viewer belongs to no boards, belongs to boards with no upcoming events, and has filtered every board out. The third is recoverable and should say so.
- **Whether the switch prompt is a modal, an inline confirm, or a small interstitial page**, and its copy.
- **How the mobile card carries D-12's explicit control** alongside D-06's roster disclosure without the two competing for the same tap.
- **Whether cancelled occurrences get any acknowledgement** or simply vanish. Phase 76 D-14 keeps their signup rows alive, so the exclusion is mandatory (inherited from Phase 77 D-12); saying so is optional.
- **Whether the viewer's own entry in each roster is highlighted**, and roster ordering within a row. `GetRosterForEventAsync` orders alphabetically by name in SQL; matching that costs nothing.
- Domain model / repository / service / controller naming, file placement, and the AutoMapper profile entries at both boundaries.
- Test structure beyond the four mandated cases in D-17.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase scope and requirements
- `.planning/ROADMAP.md` — the **Phase 82** entry in full: goal, origin, all five scope notes, both named risks, and the two decisions it required a discuss pass to settle (D-01 and D-07 answer them). Also the **Phase 77** entry, whose vocabulary, window and date boundary this phase inherits.
- `.planning/REQUIREMENTS.md` — **Phase 82 has no requirement IDs yet** (`Requirements: TBD` in the roadmap). The `EVTVIEW-01`…`EVTVIEW-04` block covers Phase 77 only, and `EVTVIEW-04` is explicitly *board-scoped* — it is not this phase's rule. Requirements for this phase need minting during planning.
- `.planning/phases/77-availability-overview-page/77-CONTEXT.md` — **the direct dependency.** D-01…D-04 (the five-state cell vocabulary and `HasAnswered` as its only input), D-09/D-10 (next-N with paging), D-11 (`Date >= today`, date-only), D-12 (cancelled occurrences excluded), D-17/D-18 (the desktop/mobile split and the tap-to-reveal roster), D-23 (read-only), D-24/D-25 (whole-row target — **deliberately diverged from here, see D-12**), D-26/D-27 (the tenancy rule and the two-group test).
- `.planning/phases/75-event-availability-signups/75-CONTEXT.md` — D-01 (Details is the single availability surface), D-03 (One-Shot roster shows only rows), D-10/D-11 (`HasAnswered`; never read the raw timestamp), D-15/D-19 (the Campaign fan-out is atomic), **D-20 (signup rows deleted on leave — cleanup, NOT access control; D-15 above depends on understanding this)**, D-28/D-29 (defence in both layers).
- `.planning/phases/76-recurring-event-series/76-CONTEXT.md` — D-14 (the cancelled tombstone keeps signup rows alive), D-114 (the configurable-code-default precedent), D-126 (per-group `SetGroupId()` iteration — **evaluated and rejected here, see D-14**).
- `.planning/phases/74-event-schema-crud-and-calendar-display/74-CONTEXT.md` — D-01 (`DateOnly`/`TimeOnly?`), D-19 (past-dated events are allowed, so the lower bound is load-bearing).

### Project conventions
- `CLAUDE.md` — the `modern-card` / `modern-card-header` / `modern-card-body` view pattern (mandatory for the new views); EF packages only in `QuestBoard.Repository`; **no GSD references in source comments**; migrations auto-apply on startup.
- `.planning/codebase/CONVENTIONS.md` — naming, AutoMapper patterns, async conventions.
- `.planning/codebase/ARCHITECTURE.md` — Service → Domain → Repository one-way dependency and the two AutoMapper boundaries.
- `.planning/codebase/TESTING.md` — integration vs unit test placement.

### Code the phase must read before changing
- `QuestBoard.Repository/GroupRepository.cs:144` — `GetEventSignupsForMemberIgnoringActiveBoardAsync`, and its sibling at `:132`. **The shape D-14 generalises**, and the comments above them state the rule this phase follows.
- `QuestBoard.Repository/GroupRepository.cs:29` — `GetGroupsForUserAsync`. Already returns the viewer's boards with `Name` and `BoardType`, unfiltered (the Groups table is not tenant-filtered). The membership source for D-15, and already the board names D-04's filter needs.
- `QuestBoard.Repository/QuestRepository.cs:267` — `GetQuestsForTomorrowAllGroupsAsync`. Read it to understand why it is **not** a precedent: a bare `IgnoreQueryFilters()` with no group predicate, for a background job.
- `QuestBoard.Repository/EventRepository.cs:132` — `GetUpcomingWithSignupsAsync`. Phase 77's query, which relies entirely on the ambient filter and has no `GroupId` parameter to widen. Read it to see why D-01 needs a new query rather than a variant, and reuse its deterministic ordering (date, start time, **id tiebreaker**) — the tiebreaker matters more here, not less, because `Take` is applied across boards.
- `QuestBoard.Repository/EventSignupRepository.cs:60` — `GetRosterForEventAsync`, the single-query eager-include shape D-18 generalises from.
- `QuestBoard.Repository/Entities/QuestBoardContext.cs` — the fail-closed global query filter block, including the "do not capture `ActiveGroupId` into a local var" warning. This is what D-14 is deliberately stepping outside of, under an explicit predicate.
- `QuestBoard.Service/Services/ActiveGroupContextService.cs` — **scoped**, with `SetGroupId` mutating it for the rest of the scope. The reason D-14 rejected per-board iteration inside a request.
- `QuestBoard.Service/Jobs/HangfireJobHelper.cs` — `RunInScopeAsync`, the fresh-scope-per-board pattern that makes `SetGroupId` safe in a job and unsafe in a request.
- `QuestBoard.Service/Controllers/GroupPickerController.cs` — `SelectGroup` (membership-verified, antiforgery-protected) and `RedirectToLocal`'s `returnUrl` handling. **D-11 and D-13 both reuse this rather than adding a second way to set the active board.** Also the `groups.Count == 1` auto-select and the `Quest/Index` redirect that D-07 leaves untouched.
- `QuestBoard.Service/Controllers/Events/EventsController.cs` — `Index` (Phase 77's overview: the clamped take, `HasMore`, `NextTake` paging shape to mirror), `Details` (no explicit guard — the 404 D-11 solves), `EventIsOnActiveBoard` at `:460` (the second-layer precedent D-16 is weaker than, and the guard D-11 deliberately does not weaken), and the SuperAdmin-with-no-active-group handling.
- `QuestBoard.Domain/Services/EventService.cs:41` — `GetAvailabilityOverviewAsync`, including `ClassifyCell` and `BuildRow`. The cell classification is reusable for the viewer's own cell and for each roster entry; the member-axis construction is **not** reused (D-02, and the roadmap's "do not port the member axis").
- `QuestBoard.Service/Views/Events/_AvailabilityCell.cshtml` and `_AvailabilityCounts.cshtml` — the shared five-state partials. `_AvailabilityCell` is reused as-is; both surfaces must render through it so the pages cannot drift.
- `QuestBoard.Domain/Models/EventsOverviewOptions.cs` — `DefaultTake` / `MaxTake` / `PageIncrement`, and the code-default-overridable-by-configuration pattern the agenda's own take should follow.
- `QuestBoard.Domain/Models/Event.cs` — `Date` (`DateOnly`), `StartTime` (`TimeOnly?`), `CancelledAt` / `IsCancelled` (get-only), and **`GroupId`, already on the domain model** — the field D-14's predicate and D-16's re-check both key on.
- `QuestBoard.Service/Views/Shared/_Layout.cshtml:162–176` (the Calendar dropdown and its board-type gate — **which this page sits outside**) and `:204–210` (the Switch Group entry the new entry sits beside).
- `QuestBoard.Service/Views/Shared/_Layout.Mobile.cshtml:141–152` (the flat offcanvas nav block; **no dropdown exists anywhere in this layout**) and `:169–173` (the flat Switch Group entry).
- `QuestBoard.Service/Views/Events/Index.cshtml` and `Index.Mobile.cshtml` — Phase 77's desktop/mobile split, the `modern-card` treatment, and where D-10's cross-link lands.
- `QuestBoard.Service/Views/Calendar/Index.cshtml` and `Index.Mobile.cshtml` — the other D-10 cross-link site.
- `QuestBoard.IntegrationTests/Tests/EventAvailabilityTenantIsolationTests.cs` — the closest sibling to the D-17 test, including the `DisposeAsync` reset.
- `QuestBoard.IntegrationTests/WebApplicationFactoryBase.cs` and `QuestBoard.IntegrationTests/Helpers/MutableGroupContext.cs` — why the default harness is blind without D-17.
- `QuestBoard.IntegrationTests/Controllers/LayoutNavigationTests.cs` — the nav cases that must stay green, and the shape new cases for D-08/D-10 should follow.

### Do not touch
- `QuestBoard.Service/Views/Events/Details.cshtml` — the write path stays exactly where Phase 75 D-01 put it. D-13 adds a conditional back link and nothing else.
- `QuestBoard.Service/Controllers/GroupPickerController.cs` `Index` — D-07 leaves the landing path alone. `SelectGroup` is reused; `Index`'s redirect is not changed.
- `QuestBoard.Service/Views/Shared/_Calendar.cshtml` — Phase 74 D-09 protects five call sites through this partial. Nothing here needs it.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- **`_AvailabilityCell.cshtml` + `AvailabilityCellState`** — the shipped five-state cell vocabulary, already carrying three independent non-colour signals for the unconfirmed default. Reused verbatim for the viewer's own cell and every roster entry. Nothing about it needs to change.
- **`EventService.ClassifyCell`** — the `HasAnswered` + `VoteType` classification, already correct and already tested. Reuse rather than re-derive.
- **`GroupRepository.GetGroupsForUserAsync`** — returns the viewer's boards with `Name` and `BoardType` and needs no filter bypass. The membership source for D-15, and already the board names D-04's filter needs.
- **`GroupPickerController.SelectGroup`** — membership-verified, antiforgery-protected, `returnUrl`-aware. D-11 and D-13 reuse it directly.
- **`GroupRepository.GetEventSignupsForMemberIgnoringActiveBoardAsync`** — the exact predicate-pinned bypass shape D-14 generalises, with the reasoning already written in the comment above it.
- **`EventsController.Index`'s paging shape** — `Math.Clamp(take ?? DefaultTake, 1, MaxTake)`, fetch `take + 1` to learn `HasMore` without a second count query, `NextTake` capped at `MaxTake`. Generalises to the agenda unchanged.
- **`EventsOverviewOptions`** — the code-default-overridable-by-configuration pattern for the agenda's own take value.
- **`Event.GroupId`** — already on the domain model, so D-14's predicate and D-16's re-check need no entity or mapping change.

### Established Patterns
- **Group scoping is a fail-closed global query filter enforced at the context, never by a method parameter.** Every `IEventRepository` XML doc says so. D-14 steps outside it deliberately and under an explicit predicate — the first user-facing read in the app to do so, which is why D-16 and D-17 exist.
- **`ActiveGroupContextService` is scoped, and `SetGroupId` mutates it for the remainder of the scope.** Safe in Hangfire because `HangfireJobHelper` opens a fresh scope per board; unsafe inside an HTTP request, where `_Layout` and `IBoardTypeResolver` read it later.
- **Mobile views are selected by user agent, not by breakpoint.** D-06's mobile view will never be exercised by devtools emulation.
- **`_Layout.Mobile.cshtml` contains no dropdown anywhere** — the new mobile entry is a flat sibling beside Switch Group, as Phase 77 D-20 established for its own entry.
- **The Calendar nav gate is on a resolved board type, not a role.** This page sits outside it by design (D-10's note).
- **Session is SQL-Server-backed since Phase 33**, so D-05's filter memory survives an app restart.
- **Two real cross-tenant leaks have shipped in this codebase (Phases 49/55), and a third live gap was found during Phase 72's discussion.** This is the reason D-14…D-17 are specified this tightly rather than left to the planner.

### Integration Points
- **New cross-board read** on a repository — next N non-cancelled events dated today-or-later across the viewer's membership set, with every signup and user, plus the group name, in one round trip (D-01, D-03, D-14, D-18).
- **New membership-set read** — fresh per request (D-15).
- **New controller action + view model + AutoMapper profile entries at both boundaries** — placement discretionary; must mirror `EventsController`'s SuperAdmin-with-no-active-group handling.
- **Two new Razor views** — desktop list with inline rosters, `.Mobile.cshtml` cards with tap-to-reveal, both on the `modern-card` pattern, both rendering cells through `_AvailabilityCell.cshtml`.
- **`_Layout.cshtml`** — new entry in the user dropdown beside Switch Group (D-08).
- **`_Layout.Mobile.cshtml`** — flat sibling entry beside Switch Group (D-08).
- **`Events/Index(.Mobile).cshtml` and `Calendar/Index(.Mobile).cshtml`** — cross-links across to the agenda (D-10). Links only; no rendering changes.
- **`Events/Details(.cshtml)`** — a conditional back link when arrived from the agenda (D-13). Nothing else changes there.
- **Session** — one new key for the board filter selection (D-05), beside `ActiveGroupId`.

</code_context>

<specifics>
## Specific Ideas

- **"I'm playing in two boards. The two campaigns alternate in sessions so a total overview is really useful (including all signups)."** The operator's own framing, and the reason D-02 puts the full roster on the row rather than a count. A planner tempted to trim the row to a summary should understand that the roster *is* the feature — the counts version was offered and declined.
- **The board filter is the release valve, not a nice-to-have.** D-03 chose a global next-N over a per-board quota specifically because D-04's filter gives the reader a visible control for the same problem. Dropping or deferring the filter does not leave D-03 intact; it leaves a page a busy board can silently dominate with no recourse.
- **Both joined boards appearing is the assertion that proves the feature.** D-17's second case exists because a suite built only from Phase 77's isolation test would stay entirely green if this page collapsed to one board. Absence testing proves safety; only the two-board case proves the thing works.
- **"Membership is the authorisation, and it is checked at read time."** The roadmap's phrase, and the through-line of D-01, D-14, D-15 and D-16 as a set. D-01 makes the query start from `Events`, which means a left board's rows still exist and still match the date predicate — so D-15's fresh read is the *only* thing removing them. These four are one decision seen from four sides.
- **This page is where the app finally has a legitimate reason to bypass the ambient filter in a user-facing read.** Every previous bypass was a background job or a private cleanup. Whatever comment sits above D-14's query will be read by everyone who adds a cross-board feature after this one — write it for that reader.

</specifics>

<deferred>
## Deferred Ideas

- **Landing a multi-board user on the agenda instead of the group picker** — considered seriously and declined under D-07. The picker is a forced interstitial that exists only for multi-board users, and the agenda is the only surface needing no active board, so it is the natural candidate. Declined because it edits a path every login goes through, including single-board users, for value this phase does not need. If it is ever wanted, `GroupPickerController.Index`'s redirect is the single place to change.
- **The agenda as the group picker's replacement**, where clicking a row both switches board and opens the event. Declined under D-07/D-11 — it couples the page to session board state and folds the click-through decision into the landing decision.
- **Answering availability inline on the agenda** — declined under D-11. It would make Phase 75 D-01's single-availability-surface rule false and require replacing `EventIsOnActiveBoard` with a membership check on a guard that exists specifically to stop cross-board writes. If it is ever wanted, that guard swap is the whole design decision and deserves its own phase.
- **A per-user persisted board filter preference** (survives logout, follows you across devices) — declined under D-05 in favour of session storage. Needs a migration and a defined behaviour when the viewer joins or leaves a board after saving.
- **Gating the nav entry on belonging to more than one board** — declined under D-08. Would need a membership count in `_Layout.cshtml` on every render for every user, or a session value kept in sync across every join and leave path.
- **A SuperAdmin view of every board's events** — declined under D-09. Mirrors `GroupPicker`'s all-groups behaviour but turns the page into an unbounded read over every event in the application.
- **Per-board quotas or a per-board floor in the window** — declined under D-03; D-04's filter covers the same problem with a control the reader can see.
- **Quests on the agenda** — roadmap-excluded, not discussed. This is an events-only surface.
- **Switching the active board back when returning from `Details` to the agenda** — declined under D-13. Two board switches per answer is state that is easy to get wrong and hard to notice when it is.

</deferred>

---

*Phase: 82-Personal Cross-Board Event Agenda*
*Context gathered: 2026-08-29*
