# Phase 84: Calendar Feed Foundation and Event Subscription - Context

**Gathered:** 2026-09-17
**Status:** Ready for planning

<domain>
## Phase Boundary

A personal, token-authenticated `text/calendar` feed that a phone's calendar app subscribes to once and then refreshes on its own, plus the surface on the Profile page that gets the address onto the phone.

Events only. Quests are Phase 85 and must not be pulled forward. One-way publication only — no CalDAV, nothing a reader does in their calendar app ever writes back to the board.

</domain>

<decisions>
## Implementation Decisions

### Which events reach the feed

This area was not on the original list. The operator raised it at the wrap-up gate, and it changed the phase's central query, so it is recorded first.

- **D-17: The feed is scoped to events the viewer holds an `EventSignup` row on — it deliberately diverges from My Agenda.** Phase 82 D-01 scoped the agenda by *board membership*, explicitly rejecting row-scoping. This feed does the opposite.

  The two rules are identical on a campaign board, because joining backfills a `Yes` row on every future event (Phase 75 D-15/D-19) — the operator identified this themselves. They diverge only on a one-shot board, where no row exists until the viewer answers (Phase 75 D-03). Put narrowly — "one-shot event posted, you have not answered, does it reach your phone?" — the operator chose no.

  **Accepted cost, stated at the time:** the calendar can never warn you about a one-shot session you have not yet noticed. That was the exact case Phase 82 D-01 existed to protect, and it is knowingly given up here.

  **Structural consequence, load-bearing:** the query starts from `EventSignups` filtered to the viewer, not from `Events`. Phase 82 D-01 required the opposite shape and its note that "Phase 77's `GetUpcomingWithSignupsAsync` cannot be widened into this" applies again in the other direction. This is a *third* query shape, not a variant of either existing one. It still carries Phase 82's membership scoping, the `IgnoreQueryFilters()` + pinned-set pattern, and D-16's second in-memory re-check — a signup row is not on its own proof the viewer may see the event.

  — **Reversibility:** reversible — output scoping only, no stored data. Changing it later changes what appears on subscribers' phones at the next poll, with no migration.

- **D-18: Every event with a row appears regardless of the vote; the answer is carried as a title suffix, and only for `(maybe)` and `(declined)`.** A plain title means either "answered yes" or "never answered".

  The operator proposed marking the vote rather than filtering declines, and asked for advice. Advice given and accepted in part:
  - **Suffix, not prefix.** The title already opens with `[Board]` (D-09); a second prefix can consume a narrow phone day view's entire visible width before the event name begins. A suffix is what truncation should sacrifice first.
  - **A campaign auto-row must not be marked "Yes".** It is `Yes` with `HasAnswered == false` — marking it as an answer asserts something the viewer never said. `HasAnswered` exists to keep those apart.

  The operator took the suffix and dropped the proposed `(no answer)` marker. **Accepted cost:** a never-answered campaign event is therefore indistinguishable from an accepted one, and reads implicitly as accepted.

### Time, duration and all-day entries

- **D-01: Floating local time — no `TZID`, no `VTIMEZONE`.** `DTSTART:20260920T190000` with no zone; the phone renders it in whatever zone the phone is in. Matches the codebase's naive model exactly — `grep TimeZoneInfo` across the solution returns nothing, and an event is a `DateOnly` plus a nullable `TimeOnly`.

  Rejected: `TZID=Europe/Amsterdam` with a real DST-carrying `VTIMEZONE` (correct anywhere, but becomes this application's first written-down timezone); and conversion to UTC (needs a source zone anyway, so the same decision one step removed, with DST errors surfacing as every event being an hour wrong for part of the year).

  **Accepted cost:** a reader whose phone is in another timezone sees the wrong hour.

- **D-02: A timed event is a fixed one-hour block. An event with a null `StartTime` is a true all-day entry.** Timed: `DTSTART`/`DTEND` an hour apart. All-day: `VALUE=DATE`, with the usual exclusive next-day `DTEND`.

  One hour renders reliably as a visible block on every client without overclaiming. Events are informational board entries, so an evening-length default would swallow a whole evening for a one-line notice. Rejected: a zero-length pin (honest, since the schema genuinely does not know, but renders as an easy-to-miss sliver and clients handle a missing `DTEND` inconsistently).

  **The hour is invented.** Nothing in the schema supports it; it must not be presented in the UI as though the board knows how long an event runs.

- **D-03: `TRANSP:TRANSPARENT` on every entry.** Entries appear on the calendar but never mark the reader busy. The one-hour block is invented (D-02) and an all-day entry blacking out a whole day would be plainly wrong, so marking busy would publish a fiction to anyone checking availability.

- **D-04: No `VALARM`.** Both iOS and Google let a reader set a default alert on a subscribed calendar, so the choice belongs to them once rather than being published to everyone. Research should confirm how each target client treats alarms in subscribed feeds before anyone relies on their absence *or* presence.

### Token lifecycle and revocation

- **D-05: A dedicated token table — many named, independently revocable subscriptions per user.** Rejected: a single random column on `UserEntity` (smaller, but one URL per person, so rotating kills every device at once); and a Data-Protection-signed payload with no storage, which would make Phase 78's *unshipped* D-03 key-ring persistence a hard prerequisite of this phase and still needs stored state to revoke at all.

  — **Reversibility:** one-way — a new table and its migration.

- **D-06: The token is stored as-is and its full address stays visible on Profile, re-copyable at any time.** Serving the feed is a direct indexed lookup.

  The question was first asked in jargon the operator could not act on and was re-put in plain language: the random string in the address *is* the password, there is no login, and the choice is whether the page keeps showing it.

  **Accepted cost, stated:** anyone with database access, or a database dump, can read every live subscription address. Judged acceptable because the payload is event titles, dates and board names. Rejected: show-once with only a hash stored (a stolen dump yields nothing, but no re-copying, no second device, and no way to verify the address on a phone is the expected one).

  — **Reversibility:** costly — switching to hashed storage invalidates every existing subscription, so every member re-subscribes on every device.

- **D-07: Each row shows its name, its address, a created date and a last-fetched timestamp, and supports rename and delete.** Last-fetched is the only way to tell a live subscription from a dead one before revoking it, and the only way an unexpected fetch would ever be noticed. The fetch-time write must be throttled so a hammered address cannot become a write storm.

- **D-08: A revoked subscription answers `410 Gone`; an address that never existed answers `404 Not Found`.**

  **Derived consequence, surfaced to the operator at the time and accepted:** telling the two apart means a revoked subscription cannot actually be deleted. The row survives as a tombstone — a `RevokedAt` timestamp — and there is no hard `DELETE` on this table. "Delete" in the UI (D-07) means revoke.

  **Accepted cost:** the differing answers confirm to anyone probing that a given address was once real. Minor for high-entropy random strings, but it is information the `404`-for-both option would not have given away.

  — **Reversibility:** one-way — tombstone retention is a schema and data-lifecycle commitment, not a response-code choice.

### What each entry says

- **D-09: The title is `[Board] Title`.** The board name is prefixed so it survives the truncation a cramped phone day view applies. Follows Phase 82 D-02 and Phase 83 D-03/D-04, which made "say which board" the naming principle for both availability surfaces. Rejected: board in the description (invisible exactly where the reader glances) and board in `LOCATION` (visible, but a board is not a place and a client may try to map it).

- **D-10: No `DESCRIPTION` at all.** Event descriptions are unbounded Markdown and a calendar entry wants plain text.

  **This retires work the ROADMAP assumed.** `IMarkdownService` has only `Web` and `Email` targets; no plain-text path is now needed, and the ROADMAP's "Markdown leaking into `DESCRIPTION`" risk no longer applies to this phase.

- **D-11: No `URL` and no link of any kind.** Proposed by the operator rather than chosen from the options offered, on the reasoning that everyone already knows the site's address and a link opening a board other than the one the reader is logged in to would clash.

  Confirmed against the code: `EventsController.Details` fetches through the board-scoped read (`EventsController.cs:64`), so a deep link to an event on a non-active board returns `NotFound()`. Phase 82 D-11 solved this for the agenda with a prompt-then-switch **POST**; a calendar link is a plain GET and cannot reuse it.

  **Consequence:** the cross-board deep-link problem is not solved in this phase — it is removed from it. An entry is a board-prefixed title, a date and a time, and nothing else. The feed is deliberately not a second read surface for the board.

- **D-12: A cancelled event is dropped from the feed entirely, not marked `STATUS:CANCELLED`.** This reuses the existing predicate verbatim — `EventRepository.cs:173` already filters `CancelledAt == null`.

  **Accepted cost, stated:** a cancellation reaches the subscriber only as a silent disappearance, and a reader is unlikely to notice an event they are no longer looking for.

### Feed window and the subscribe surface

- **D-13: A rolling date window — a few months of history, roughly a year ahead — recomputed on every fetch.** The exact numbers are the planner's to fix and should be configurable in the manner of `AgendaOptions`.

  A row count is the wrong shape here: a phone renders a month at a time, so the question is which dates, not how many rows. Rejected: upcoming-only (the calendar could never answer "when did we last play?") and no window at all (the file grows forever and every device re-downloads all of it several times a day).

  **Structural consequence:** a date range with no `Take`, and **no roster join** — no `Include(Signups).ThenInclude(User)`, because no roster ever reaches an entry (D-10, D-11). Combined with D-17 this is a distinctly simpler read than the agenda's, not a widening of it.

- **D-14: Copy button, a `webcal://` link, and a QR code.** The QR is the one that actually solves the stated problem — the address is created on a desktop and needed on a phone.

  **Adds a dependency.** No current package can generate a QR code. This is a real addition to a dependency list the project has kept deliberately short.

- **D-15: A section on the existing Profile page, on both `Profile.cshtml` and `Profile.Mobile.cshtml`.** Not its own page.

  **Accepted cost:** Profile today is a plain card with a name row and an email row; a subscription table with per-row rename and delete controls plus a QR code will dominate it. Both layouts ship together — shipping one without the other is a recorded failure mode here (Phases 43, 54, 72).

- **D-16: Nothing is minted until the reader presses Add.** The section shows an explainer and an Add control; no subscription exists until asked for. A subscription address is a password with no expiry, and minting one for every member on a page load they did not make for this reason is the wrong default.

### Claude's Discretion

- **Absolute base URL comes from `EmailSettings:AppUrl`**, following Phase 78 D-07's reasoning. Taken as builder's discretion and flagged to the operator, who did not object. Request-derived URLs are wrong in both scheme and host until the `XForwardedProto`/`XForwardedHost` fix lands — `Program.cs:103` sets only `XForwardedFor` — and that fix belongs to Phase 78, which has not shipped. `AppUrl` is already proven correct in production by working email links.
- The all-day `VALUE=DATE` mapping and its exclusive next-day `DTEND` (a classic off-by-one that renders a one-day event across two days).
- `UID` scheme. Must be stable across polls or a phone accumulates a duplicate per refresh, and must be namespaced by source — a quest and an event can share an integer id, and Phase 85 adds the second source.
- Rate limiting on the anonymous endpoint. `Program.cs` already has named policies (`forgot-password`, `set-password`) to pattern from.

### Open for research — not decided

- **What the calendar is called on the reader's phone** (`X-WR-CALNAME`). Non-standard but broadly honoured; without it the subscription is named after its URL.
- **How fast a change reaches a device.** Whether to publish `X-PUBLISHED-TTL` / `REFRESH-INTERVAL` and what to do with `ETag` / conditional requests. Google in particular chooses its own polling interval, so these may be advisory at best — confirm before promising any refresh latency in the UI copy.
- **The feed route's exemption from `GroupSessionMiddleware`.** Anonymous callers already pass through untouched (`GroupSessionMiddleware.cs:101`), so calendar apps are unaffected. But an *authenticated* reader opening the address in a browser with no active board would be redirected to the board picker. `/Account` is already exempt (`GroupSessionMiddleware.cs:63`), which may or may not cover where this route ends up living.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### This phase and its neighbours
- `.planning/ROADMAP.md` § Phase 84 — goal, scope notes, risks. Note that D-10 and D-12 retire two of its six named risks, and D-11 removes the deep-link problem rather than solving it.
- `.planning/ROADMAP.md` § Phase 85 — the quest half. Must not be pulled forward.
- `.planning/ROADMAP.md` § Research Flags — Phase 84 is flagged for a research step on client behaviour.

### Decisions this phase inherits or deliberately breaks with
- `.planning/phases/82-personal-cross-board-event-agenda/82-CONTEXT.md` — D-01 (board-membership scoping, **which D-17 deliberately departs from**), D-02 (board named on every row), D-11 (prompt-then-switch POST), D-14/D-15/D-16 (the tenant-safety query pattern this feed must still follow), D-17 (the tenant-isolation test shape), D-18 (no N+1).
- `.planning/phases/78-link-preview-foundation-and-quest-cards/78-CONTEXT.md` — D-03 (Data Protection key-ring persistence, **unshipped**, which D-05 avoids depending on), D-05/D-06 (forwarded-headers state), D-07 (`EmailSettings:AppUrl` as canonical base URL, followed here), D-08 (dedicated anonymous route pattern).
- `.planning/phases/83-availability-surface-naming-and-placement/83-CONTEXT.md` — D-03/D-04 (the "say whose and which board" naming principle behind D-09).

### Project-level
- `.planning/PROJECT.md` § Constraints, § Key Decisions.
- `.planning/codebase/CONVENTIONS.md` — naming, async, migration, and the modern-card UI pattern D-15's section must follow.
- `CLAUDE.md` — CRLF for source files, EF packages confined to `QuestBoard.Repository`, no planning IDs in source comments.

### Code the phase builds on
- `QuestBoard.Repository/EventRepository.cs:160` — the cross-board read and its `IgnoreQueryFilters()` + pinned-set comment. The model for the new query's safety, not its shape.
- `QuestBoard.Domain/Services/EventService.cs:82` — membership-scoped service read with the second-layer re-check and `LogError`.
- `QuestBoard.Service/Middleware/GroupSessionMiddleware.cs:59` — the exempt-path list.
- `QuestBoard.Service/Program.cs:101` — forwarded headers; `:118` — rate-limiter policies.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `EventRepository.GetUpcomingAcrossGroupsWithSignupsAsync` — not reusable as-is (wrong shape under D-13 and D-17), but its `IgnoreQueryFilters()` + pinned-membership comment block is the safety pattern the new query must reproduce.
- `EventService.GetCrossBoardAgendaAsync` — the second-layer re-check and `LogError` on a surviving foreign row. Mandatory here: a feed is read by a machine, so a leak has no reader to notice it.
- `Program.cs` rate-limiter policies (`forgot-password`, `set-password`) — the pattern for a policy on the anonymous feed endpoint.
- `Views/Account/Profile.cshtml` + `.Mobile` — the host for D-15.
- `AgendaOptions` — the pattern for D-13's configurable window bounds.

### Established Patterns
- Three-layer Service → Domain → Repository with AutoMapper at both boundaries; EF packages only in `QuestBoard.Repository`.
- Anonymous requests pass `GroupSessionMiddleware` untouched; authenticated ones with no active board are redirected unless the path is exempt.
- Migrations auto-apply on startup, so D-05's table and D-08's tombstone column need no deploy step.
- Tenant safety is defence in depth: a scoped query, a pinned predicate, and an independent in-memory re-check that fails closed and logs.

### Integration Points
- New anonymous controller serving `text/calendar`; no view, no layout, so `MobileDetectionMiddleware` is irrelevant to it.
- New entity, repository, migration for the subscription table (`RevokedAt` tombstone per D-08, last-fetched per D-07).
- New query starting from `EventSignups` (D-17) with a date range (D-13) and no roster include.
- `Profile` GET and new POST actions for add, rename and revoke, on both layouts.
- One new package for QR generation (D-14).

</code_context>

<specifics>
## Specific Ideas

- The operator's framing throughout: this is a "when am I busy" surface, not a second view of the board. D-10 and D-11 both follow from it, and it is the reason the ICS writer ended up very small — a `UID`, a start, an end, a title and a transparency flag.
- On links: *"people know the url of the website, but being member of multiple boards will clash if you're logged in to board 1, but the link opens something on board 2?"*
- On the vote marker: the operator proposed showing the answer in the title rather than filtering declines out, and accepted the suffix form while declining the `(no answer)` marker.

</specifics>

<deferred>
## Deferred Ideas

- **Quests in the feed — Phase 85.** Rules given by the operator during this discussion and to be recorded in the Phase 85 roadmap entry: one-shot boards only; only quests the viewer is signed up for; and only once the quest is **finalized** — never while date voting is still running.
- **A per-board feed URL.** Not raised as a want; the single personal feed follows the Phase 82 precedent. Would need a different token-to-scope model.
- **Marking a never-answered campaign event.** D-18 dropped `(no answer)`; revisit if unanswered campaign events reading as accepted turns out to mislead.
- **Renaming `EmailSettings:AppUrl`.** Already noted as a naming smell in Phase 78 D-07 and deferred there. A calendar feature reading a key under `EmailSettings` makes it smell more, but renaming touches every email template and the server's env file.

</deferred>

---

*Phase: 84-calendar-feed-foundation-and-event-subscription*
*Context gathered: 2026-09-17*
