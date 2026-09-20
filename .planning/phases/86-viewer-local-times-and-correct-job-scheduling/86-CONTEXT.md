# Phase 86: Viewer-Local Times and Correct Job Scheduling - Context

**Gathered:** 2026-09-20
**Status:** Ready for planning

<domain>
## Phase Boundary

Every **real instant** this application renders appears in the reader's own browser timezone instead of UTC, and the three nightly Hangfire sweeps fire at the hour their registration claims rather than two hours later. No **wall-clock** value — no game night, no proposed date, no event time — moves by a single minute.

This is a display-and-scheduling phase. No stored value changes meaning, no column is rewritten, no migration is added. The calendar feed writer is not touched at all.

</domain>

<decisions>
## Implementation Decisions

### The classification everything depends on

`DateTime` carries two incompatible meanings in this schema and the type does not distinguish them. Every task in this phase depends on getting each property on the correct side.

**Real instants — stored UTC, converted for display:**
`CreatedAt` (Quest, Event, Character, Contact, ContactNote, Group, EventSignup, EventSeries, CalendarSubscription), `UpdatedAt` (ContactNote, EventSignup), `CancelledAt`, `RevokedAt`, `LastFetchedAt`, `SentAt` (ReminderLog), `SignupTime`, `LastVoteChangeTime`, `TransactionDate`, `ListedDate`, `DeniedAt`, `ClosedDate`.

**Naive wall-clock — never converted, never touched:**
`QuestEntity.FinalizedDate`, `QuestEntity.FinalizedEmailSentForDate`, `ProposedDateEntity.Date`, `EventEntity.Date` + `EventEntity.StartTime`, `ShopItemEntity.AvailableFrom`, `ShopItemEntity.AvailableUntil`.

Two of these were open questions in ROADMAP.md and are resolved by evidence rather than by preference:

- **`ClosedDate` is a real instant.** Assigned `DateTime.UtcNow` at `QuestBoard.Repository/QuestRepository.cs:170`.
- **`FinalizedEmailSentForDate` is wall-clock and internal-only.** `SetFinalizedEmailSentForDateAsync` stores the quest's finalized date verbatim (`QuestRepository.cs:229-234`) and `QuestFinalizedEmailJob.cs:35` compares it as `.Date == finalizedDate.Date` — a send-once dedupe key, not a timestamp any reader sees. It must never be converted and should not gain a display site.
- **`ShopItem.AvailableFrom`/`AvailableUntil` are wall-clock.** Bound straight from `ShopItemViewModel` form fields (`ShopManagementController.cs:77-78, 140-141`), never from a clock.

### Rendering

- **D-01: The mixed-zone page is accepted as-is; game-night rendering is out of scope entirely.** After this phase a page can show a converted instant beside an unconverted board wall-clock time with nothing distinguishing them. For a reader in the board's own zone the two agree and the mix is invisible; for a reader abroad the game night is the one that reads wrong. This is the same cost Phase 84 D-01 already accepted for the calendar feed ("a reader whose phone is in another timezone sees the wrong hour"), so the board and the feed stay consistent with each other rather than one being more careful than the other. Leaving game-night rendering untouched also makes "no game night moved" structurally true rather than something tests must prove. **Rejected:** labelling game nights with an explicit board-zone marker (noise on the busiest surfaces, and scope the request did not ask for); labelling every rendered time (makes the board look more timezone-aware than the model actually is).
- **D-02: A converted instant carries the source instant in a `title` attribute.** One attribute, no visual weight, and it turns "is this UTC or local?" into a hover instead of a code read — the exact diagnosis this phase started from.
- **D-06: Every rendered instant converts, including date-only renders.** The rule is a property of the value, not of the format string. Most instant renders in the views are date-only (`"MMM dd, yyyy"`, `"yyyy-MM-dd"`), and a date-only render of a UTC instant still shows the previous day between 22:00 board time and midnight. Scoping the rule to format strings that contain a time would leave that window open and would silently re-introduce the bug at the next date-only render anyone adds. — **Reversibility:** costly — undoing it means revisiting every converted render site across both the desktop and `.Mobile` layouts.
- **Relative rendering ("2 hours ago") is declined** for this phase. It would add a second rendering mode to build, test and keep consistent across both layouts for no correctness gain.

### First paint and the no-JS reader

- **D-05: The server renders the instant in the configured board zone; the browser then swaps it to the viewer's zone.** For a reader in the board's own zone the server value and the browser value are identical, so there is no visible flash at all. A reader abroad sees board time replaced by their own. A reader without JavaScript gets board time — the same zone the game nights on the same page are already in, so the page stays internally consistent rather than mixing UTC with wall-clock. **Rejected:** rendering UTC with an explicit label (a UTC flash on every page load for every reader, and a no-JS reader is left doing the arithmetic this phase exists to stop); rendering an empty placeholder (no time at all without JS, a functional regression).

This decision is only available because D-03 gives the server a configured board zone to render in.

### Job scheduling

- **D-03: The sweep timezone is configurable with a `Europe/Amsterdam` code default.** Follows the `CalendarFeedOptions` idiom exactly — "code defaults, overridable through configuration, so no deployment environment file has to change". All three registrations read it, and `DailyReminderJob`'s own notion of "today" reads the same zone, so the cron that wakes the job and the clock the job computes with cannot drift apart. **Rejected:** a hardcoded `TimeZoneInfo` (breaks the codebase's own pattern for this kind of knob); fixing only the reminder sweep (leaves two jobs whose stated hour still differs from their actual hour). **Also rejected on correctness grounds:** shifting the cron hours to compensate (`0 7 * * *` to land at 09:00) — a fixed offset cannot track DST and would be an hour wrong for half the year.
- **D-04: An unresolvable zone falls back to UTC with a logged warning — the application still boots.** Operator's decision, against the recommendation to fail fast. **The mitigation is mandatory, not optional:** a named health check reports `Degraded` when the fallback is active, so the degraded state is visible from outside the container. `Degraded` still returns 200, so the existing docker-compose healthcheck will not restart-loop the container. The reason this mitigation is required is that a log line is exactly what nobody read while the current bug was live.
- **D-07: Every ambient clock read that is compared against a board-local date moves onto one board clock.** `EventSeriesService` (7 sites), `CalendarController`, `EventsController`, `SeriesController`, `GroupRepository`, `DailyReminderJob`. These currently read UTC and compare against `DateOnly` values that are board-local wall-clock, so between midnight and 02:00 board time they operate on yesterday — `GetActiveSeriesAsync(today)`, `GetSeriesBelowRunwayAsync(today)` and the campaign auto-signup sweep at `GroupRepository.cs:74` are all affected. — **Reversibility:** costly — undo touches every service that took the clock by injection, across six files.

`EmailPreviewController`'s five `DateTime.Today` uses generate sample data for the admin preview page and are cosmetic; they need no clock.

### Emails need no change

All four transactional templates (`QuestFinalized`, `QuestDateChanged`, `SessionReminder`, `WaitlistPromoted`) render only a **quest date**, formatted `"dddd, MMMM d"` — day name, month, day, with **no time component at all**. Every one is a wall-clock game night, which D-01 puts out of scope, and there is no clock value to convert or zone to label. The "do emails need an explicit zone label" question in ROADMAP.md is therefore moot rather than answered.

### Claude's Discretion

- The shape of the board-clock seam (a `TimeProvider` wrapper, a dedicated interface, an options-backed service) is the planner's call. The constraint is that one seam serves both the cron registrations and the ambient reads so they cannot diverge.
- The shape of the client-side formatting pass (where the script lives, how elements are marked, which `Intl` options are used) is the planner's call, subject to D-05's first-paint behaviour and D-02's `title` attribute.
- Which existing options class carries the zone, or whether a new one is minted.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Binding prior decisions

- `.planning/phases/84-calendar-feed-foundation-and-event-subscription/84-CONTEXT.md` — **authoritative for D-01 (floating local time, no `TZID`, no `VTIMEZONE`)**, the decision this phase must not disturb. Also D-10/D-11 (no `DESCRIPTION`, no `URL`), which is why the feed cannot carry a zone label even if it wanted one.
- `.planning/phases/85-one-shot-quests-in-the-calendar-feed/85-CONTEXT.md` — records that `FinalizedDate` is stored as server local time and flags the mixed time basis in the feed window comparison (§ open questions). Confirms `FinalizedDate` is the value the feed emits.
- `.planning/ROADMAP.md` § Phase 86 — the classification table, the locked client-side mechanism, and the decision to leave the container `TZ` unset.

### Facts this phase corrects

- `.planning/PROJECT.md:173` — the standing tech-debt entry asserting `FinalizedDate` "is correct for LXC host timezone". **This is false for the shipped deployment** and must be corrected as part of this phase. `docker-compose.yml` sets no `TZ` and mounts no `/etc/localtime`, and `Dockerfile` installs no tzdata over `mcr.microsoft.com/dotnet/aspnet:10.0`, so the container clock is UTC.
- `QuestBoard.Service/Jobs/DailyReminderJob.cs:16-17` — comment asserting "DateTime.Today is server local time on the LXC container (CET/CEST)". False; correct it rather than preserve it.
- `QuestBoard.Service/Program.cs:371, 381, 386` — three comments asserting the sweeps run at "server local time". False; Hangfire's `RecurringJob.AddOrUpdate` defaults to `TimeZoneInfo.Utc` and none of the three registrations passes one.

### Codebase conventions

- `.planning/codebase/CONVENTIONS.md` — naming, AutoMapper boundaries, view conventions.
- `CLAUDE.md` § Code Comments — no phase or requirement IDs in source comments. The corrected comments above must explain the *why* in plain language.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets

- **`CalendarFeedOptions`** (`QuestBoard.Domain/Models/CalendarFeedOptions.cs`) — the exact pattern D-03 follows: code defaults, configuration override, `SectionName` constant, and an `IsValid()` that refuses startup on an unserviceable value. A zone option should look like this.
- **`TimeProvider` injection** — already used by `CalendarSubscriptionService` (`timeProvider.GetUtcNow().UtcDateTime`), with `FixedTimeProvider` test doubles in `EventsOverviewAggregationTests` and `CrossBoardAgendaTests`. The seam idiom exists; D-07 extends it rather than inventing it.
- **`AddHealthChecks()` / `MapHealthChecks("/health")`** (`Program.cs:41, 363`) — registered but carries no custom checks. D-04's Degraded check is the first. `docker-compose.yml` already polls this endpoint.
- **`site.js`** (`QuestBoard.Service/wwwroot/js/site.js`) — vanilla JS, loaded unconditionally from both `_Layout.cshtml:256` and `_Layout.Mobile.cshtml:217`. No bundler, no module system; jQuery and Bootstrap load from CDN ahead of it.

### Established Patterns

- **Desktop/`.Mobile` view twins.** 53 `.Mobile.cshtml` views exist and 15 of them render dates. Shipping a change on one layout and not its twin is a recorded failure mode in this codebase (Phases 43, 54, 72, and called out again in Phase 84's scope notes). Every view touched here has a twin that must be touched too.
- **Roughly 71 date-render call sites across the views in 19 distinct format strings**, none of which currently convert.

### Integration Points

- **The three `RecurringJob.AddOrUpdate` registrations** — `Program.cs:372` (`daily-session-reminders`, `0 9 * * *`), `:380` (`recurring-occurrence-top-up`, `0 3 * * *`), `:389` (`calendar-subscription-retention`, `0 4 * * *`). Only the first has user-visible timing; the other two need only stay off-peak and distinct from one another.
- **`CalendarFeedWriter` must be provably untouched.** It emits `FinalizedDate` as floating local time per 84 D-01. Anything that converts that value shifts every subscriber's session by the offset — and because a calendar client refreshes on its own schedule, nobody would observe it for hours.

### Landmines found during this discussion

- **A coalesce that mixes both categories through one format string.** `QuestBoard.Service/Views/QuestLog/Details.cshtml:37` and `Details.Mobile.cshtml:38` render `(Model.Quest.FinalizedDate ?? Model.Quest.ClosedDate)` — wall-clock coalesced with a real instant. It must be split before either side can be handled correctly. Same shape, date-only, at `QuestLog/Index.cshtml:23` and `Index.Mobile.cshtml:50`.
- **Arithmetic that must stay in UTC.** `QuestBoard.Service/Views/Shop/Index.cshtml:76` computes `DateTime.UtcNow - purchase.TransactionDate`. Correct today because both sides are UTC; it breaks if `TransactionDate` is converted server-side. Only *rendering* converts — spans and comparisons stay in UTC.
- **Four `datetime-local` form round-trip values that must never be touched.** `Quest/CreateFollowUp.cshtml:74`, `CreateFollowUp.Mobile.cshtml:79`, `Quest/Edit.cshtml:97`, `Edit.Mobile.cshtml:101` render `ProposedDates` as `ToString("yyyy-MM-ddTHH:mm")` into `datetime-local` inputs and hidden fields. They are wall-clock *and* form values; a blanket sweep over view date renders would catch them and corrupt the round-trip.
- **`SignupTime` needs nothing.** It appears in the views only as an `OrderBy` key (`Quest/Details.cshtml:80-97`, `QuestLog/Details.cshtml:63`), never rendered. Ordering is zone-invariant.

</code_context>

<specifics>
## Specific Ideas

- The operator's reported symptom was the Calendar Subscription section's "Last fetched" timestamp reading two hours behind a Dutch wall clock (`Views/Account/Profile.cshtml:111` and `Profile.Mobile.cshtml:71`). That is the canonical example of a real instant rendered raw, and a reasonable first target for an end-to-end tracer.
- The operator explicitly chose availability over strictness on the zone-resolution failure (D-04), then accepted the Degraded health check as the compensating visibility. Both halves are the decision; implementing the fallback without the health check does not satisfy it.

</specifics>

<deferred>
## Deferred Ideas

- **Labelling game-night times with an explicit board-zone marker.** Rejected under D-01 while every reader shares the board's zone. Revisit if a player relocates — at that point it is a small, well-understood follow-up phase, and the need will be concrete rather than hypothetical.
- **Relative rendering ("2 hours ago") for recency-style timestamps.** Considered under D-06 and declined; it adds a second rendering mode for no correctness gain.
- **Expressing the instant-versus-wall-clock distinction in the type system** (distinct types, or `DateTimeOffset` for the instant side). This phase relies on a written classification and tests; making the distinction unmistakable in code is a schema-and-model change and belongs in its own phase.
- **Per-user timezone preference.** Not discussed and not wanted — the browser's resolved zone is the whole mechanism, with no stored preference to manage.

</deferred>

---

*Phase: 86-viewer-local-times-and-correct-job-scheduling*
*Context gathered: 2026-09-20*
