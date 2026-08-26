# Pitfalls Research — Calendar Events (v9.0 Events milestone)

**Domain:** Adding a recurring-events subsystem (EventSeries → materialized EventOccurrence rows, availability signup, auto-signup, availability overview) to a mature ASP.NET Core 10 MVC app with an existing, documented history of DST/local-time fragility, Hangfire footguns, and two real cross-tenant security leaks.
**Researched:** 2026-08-25
**Confidence:** HIGH — every pitfall below is grounded in a real file, a real prior incident recorded in `.planning/PROJECT.md`, or code read directly from this repository (not generic ASP.NET/Hangfire advice).

**Proposed phase breakdown used for the mapping table below** (provisional roles, actual phase numbers ≥74 to be assigned by the roadmap):
- **Phase A** — EventSeries/EventOccurrence schema + materialization job (the engine)
- **Phase B** — Occurrence lifecycle: cancel / move / edit a single occurrence; series-rule edits
- **Phase C** — Signup/voting UI + auto-signup logic (Campaign opt-out vs One-Shot opt-in)
- **Phase D** — Calendar view integration (desktop + mobile, all 6 existing `_Calendar.cshtml` call sites)
- **Phase E** — Availability overview page (events × members × signups)

---

## Critical Pitfalls

### Pitfall 1: DST wall-clock drift in recurrence generation, and the storage convention that must be copied (not invented)

**What goes wrong:**
"Every 2 weeks on Saturday at 19:00" silently becomes 18:00 or 20:00 local time for occurrences that fall after a DST boundary the series crosses, if the materialization job computes each cycle's start time by converting the anchor to a fixed instant (UTC) and adding a constant duration (`TimeSpan.FromDays(14)` / `14 * cycleIndex` applied to a UTC-kind value) rather than doing calendar arithmetic on the naive local wall-clock value. A 12-month rolling window guarantees every series crosses at least one, usually two, DST transitions (last Sunday of March and October in CET/CEST), so this is not an edge case for this feature — it is the common case for every recurring series that survives more than ~6 months.

A second, independent failure mode: even if wall-clock time is preserved correctly, a **materialized occurrence row and a calendar cell computed separately can disagree** about which day an occurrence belongs to if one path treats the stored value as local/Unspecified and another treats it as UTC or ISO-8601-with-implicit-browser-timezone. `CalendarViewModel.GetCalendarDays()`/`_Calendar.cshtml` today bucket everything via `.Date` comparisons against `new DateTime(Year, Month, day)` (Kind=Unspecified) and `DateTime.Today` (server local) — entirely self-consistent *only* because every value in that path is local/Unspecified. Any Events code path that starts treating `EventOccurrence.StartDate` as UTC (e.g., an API/JSON endpoint later consumed by a JS `Date()` that assumes an offset-less ISO string is browser-local) would place the same physical row on a different calendar day than the server-rendered `_Calendar.cshtml` view does, with zero errors thrown.

**Why it happens:**
This project already has exactly this problem, half-solved, in its existing `FinalizedDate` field. Per `.planning/PROJECT.md`'s "Known issues" section: *"`FinalizedDate` stored as server local time (CET/CEST) — reminder job uses `DateTime.Today.AddDays(1)` which is correct for LXC host timezone but should be reviewed if deployment timezone changes."* But that consistency is not universal: `QuestLogController` (3 call sites), several Views (`Quest/Details.cshtml`, `Quest/Index.Mobile.cshtml`, `_QuestCard.cshtml`, `Admin/Quests.cshtml`, `Admin/Quests.Mobile.cshtml`, `Quest/Manage.cshtml`), and `QuestService.cs:183` all compare `FinalizedDate.Value.Date` against **`DateTime.UtcNow.AddDays(-1).Date`** — a local-time value compared against a UTC value. This "works" today only because those comparisons are day-granularity with a generous 1-day offset that absorbs the ~1-2 hour CET/CEST skew without flipping the wrong day in practice. It is fragile, not correct, and Events must not copy the UTC-comparison half of this pattern — only the local-storage half.

**How to avoid:**
1. Store `EventOccurrence.StartDate` (and `EventSeries.AnchorDate`) exactly the way `Quest.FinalizedDate` is stored today: a plain `DateTime` (Kind=Unspecified), representing server-local wall-clock time. Do not introduce `DateTimeOffset` or UTC storage for this feature alone — that would create a second, incompatible time convention living alongside `FinalizedDate` in the same calendar UI, which is worse than either convention consistently applied.
2. Compute every recurrence cycle via **local calendar-date arithmetic on the naive `DateTime`** — `anchor.AddDays(14 * cycleIndex)` (or the cadence's exact weekday/interval math) — never by converting to UTC, adding a `TimeSpan`, and converting back.
3. Every comparison the materialization job, calendar view, or overview page makes against "now"/"today" must use `DateTime.Now`/`DateTime.Today` (server local), matching `CalendarController.Index`'s existing `DateTime.Now` and `_Calendar.cshtml`'s `DateTime.Today` — never mix in `DateTime.UtcNow` the way the `FinalizedDate`-comparison call sites listed above do. If this feature's code ever needs to touch a `FinalizedDate`-adjacent comparison, do not copy the `UtcNow` pattern from those call sites; flag it as pre-existing debt instead.
4. Because this app has no client-side timezone picker anywhere and its ~17 members are a single trusted group (one deployment TZ), do not add `DateTimeOffset`/UTC-normalization machinery "for correctness" — it would introduce a second time-storage convention this codebase does not otherwise have, for a scale problem that does not exist here. Consistency with `FinalizedDate` is more valuable than theoretical multi-timezone correctness.

**Warning signs:**
- Any Events code that calls `.ToUniversalTime()`, `.ToLocalTime()`, `DateTime.SpecifyKind(..., DateTimeKind.Utc)`, or constructs `DateTimeOffset` for occurrence dates.
- Any Events arithmetic expressed as `TimeSpan.FromDays(N)`/`.AddHours()` applied to an already-UTC-converted value, rather than `.AddDays(N)` on the naive local `DateTime`.
- A materialized occurrence whose `.Hour`/`.Minute` doesn't match its series' anchor time for cycles that fall after late March or late October.

**How to verify prevention worked (the specific check):**
A unit test that anchors a series at a fixed local time (e.g., 19:00) on a date before the last Sunday of March, generates 26+ weekly/bi-weekly cycles spanning both the March and October transitions of a real year, and asserts every materialized occurrence's `TimeOfDay` equals the anchor's `TimeOfDay` exactly. This is the single test that catches a regression here; it does not exist today because this project has never had a recurrence-generation code path before.

**Phase to address:** Phase A (materialization job) — this must be locked down before any occurrence rows exist, since occurrences created with the wrong convention cannot be safely bulk-corrected once players have voted on them (see Pitfall 3).

---

### Pitfall 2: Container timezone is not pinned anywhere — "every 2 weeks on Saturday" is only as stable as the host's `/etc/localtime`

**What goes wrong:**
No file in this repository sets `TZ`, calls `TimeZoneInfo.FindSystemTimeZoneById`, or otherwise pins the application's notion of "local time" (confirmed by search — zero hits for `TimeZoneInfo`/`TZ=` anywhere in `QuestBoard.Service`, `QuestBoard.Domain`, or deployment docs). `DateTime.Now`/`DateTime.Today` on the Linux LXC host resolve entirely from the OS's system timezone configuration, which nothing in this codebase owns or asserts. If that ever changes — an LXC template rebuild, a host migration, a minimal-image default of `TZ=UTC`, or simply a misconfigured systemd unit — every local-time convention in the app shifts uniformly and silently: `DailyReminderJob`'s "tomorrow" cutoff, `_Calendar.cshtml`'s "today" highlight, and (once this feature ships) every Events anchor/cadence computation. Occurrences materialized *before* such a change and occurrences materialized *after* it would silently disagree about what "19:00 Saturday" means in real-world terms, with no error anywhere.

**Why it happens:** This project's deployment model is "direct `dotnet run` on a Linux LXC host" (`/opt/questboard/`, env overrides at `/etc/questboard/.env`) — there is no Docker base-image `ENV TZ=...` layer to inherit correctness from, unlike a containerized deployment where a Dockerfile typically pins this. The host's timezone has simply never needed to be an explicit, owned piece of app configuration before, because nothing in the app previously depended on wall-clock arithmetic spanning more than a day or two.

**How to avoid:** Pin the timezone explicitly as deployment configuration, not implicit host state — e.g., set `TZ=Europe/Amsterdam` (or whichever zone the LXC host is actually configured for; verify against the live host, don't assume) in `/etc/questboard/.env` or the systemd unit's `Environment=` directive, and log `TimeZoneInfo.Local.Id` (or `TimeZoneInfo.Local.StandardName`) at application startup so a future host migration that silently changes the OS timezone is visible in the startup log rather than invisible.

**Warning signs:** No startup log line stating which timezone the app believes it's running in; deploy runbooks that don't mention verifying host TZ.

**How to verify prevention worked:** A startup log assertion/line printing `TimeZoneInfo.Local.Id`, checked against the expected value as part of any future server-setup doc, plus a deploy-time manual check (`timedatectl` on the host) recorded the same way Phase 71 recorded its deferred Outlook-rendering verification — as an explicit, structured "verify on deploy" item, not silently assumed.

**Phase to address:** Phase A — add the startup log line alongside the materialization job's own logging (Pitfall 4's recommended log line is a natural place to also emit `TimeZoneInfo.Local.Id`).

---

### Pitfall 3: Hangfire recurring job silently stops — the calendar quietly runs dry at the 12-month horizon with no error anyone sees

**What goes wrong:** This project's only precedent for a maintenance-style Hangfire job is `DailyReminderJob` — a same-day, fire-and-forget job whose failure is noticed within 24 hours (someone doesn't get a reminder email). The Events materialization job is structurally different: it exists to **maintain a rolling buffer**, and a silent failure (an unhandled exception exhausting the global `AutomaticRetryAttribute` retry budget established in Phase 34.2, a bad deploy that broke the job's DI graph, or simply the recurring-job registration silently failing to re-register after a schema change) produces no user-visible symptom for weeks or months — until a DM scrolls the calendar far enough forward to notice occurrences just stop. By the time that's noticed, root-causing "what changed weeks ago" is much harder than it would be if the gap were surfaced immediately.

The Hangfire dashboard (`/hangfire`) is restricted to SuperAdmin only (Phase 29 decision) and has no link in mobile nav (a pre-existing, documented gap) — nobody is passively looking at it. There is no email-on-job-failure or external alerting anywhere in this stack.

**Why it happens:** Every existing job in this codebase (`DailyReminderJob`, `SessionReminderJob`, `QuestFinalizedEmailJob`, `QuestWaitlistPromotedEmailJob`) is a "does something today, done" job. Nothing in the existing job set requires the concept of "is the buffer still healthy," so there's no existing pattern to copy for self-monitoring — a naive implementation will mechanically copy `DailyReminderJob`'s "log and return" shape, which is silent-by-default for exactly the failure mode that matters most here.

**How to avoid:**
1. Log a structured summary every run, mirroring `DailyReminderJob`'s existing `logger.LogInformation` convention but with the numbers that actually matter for a rolling-buffer job: groups processed, occurrences created, and the resulting horizon date per series (or the minimum horizon across all series).
2. Add an active, surfaced health check rather than relying on passive log-reading: on Calendar/Index or the new overview page load (cheap, since it's already querying series/occurrences), compute "does every active EventSeries have a materialized horizon ≥ today + 11 months" and, if not, log a WARNING and/or show a SuperAdmin/DM-visible banner. This makes a stalled job something a DM notices in-app within a day, not months later.
3. Confirm the job registration itself follows the established `RecurringJob.AddOrUpdate<TJob>("job-id", ..., cron)` pattern placed **after** `app.Services.ConfigureDatabase()` inside the `!IsEnvironment("Testing")` guard (`Program.cs`, same placement as `DailyReminderJob`'s registration) — registering before migrations run is this project's own documented Pitfall 4 from the original session-reminders research.

**Warning signs:** No log line distinguishing "ran and did nothing because nothing was due" from "ran and materialized N occurrences" from "did not run at all"; no way to answer "when did this last successfully run" without opening `/hangfire` directly.

**How to verify prevention worked:** A manual `/hangfire` dashboard check after the job's first few scheduled runs in production (this project's established fallback for gaps automated tests can't cover, per the Phase 37 "live `dotnet run` smoke test" precedent) plus the in-app horizon health check described above, which is the durable, permanent guard (the dashboard check is not).

**Phase to address:** Phase A.

---

### Pitfall 4: Idempotency — the identity scheme must survive move/cancel/edit, or the job resurrects, duplicates, or overwrites occurrences on every re-run

**What goes wrong:** Two compounding facts make this near-certain if not designed explicitly: (1) Hangfire's `[AutomaticRetryAttribute]` is already globally enabled in this app (Phase 34.2) — if the materialization job throws partway through group 7 of 12, the automatic retry re-runs the **entire job from the start**, not just the failed portion; (2) the job re-runs on a schedule regardless, and each run must decide "which cycles of each series already have a row" — get that decision wrong and the job either creates a second row for an already-existing (possibly cancelled or moved) occurrence, or silently skips cycles it shouldn't.

The specific trap: if the job's "does this occurrence already exist" check is keyed on **date** (`WHERE SeriesId = X AND Date = Y`), it will fail exactly for the occurrences the feature spec calls out as needing independent lifecycle — a DM who **moves** an occurrence to a different date vacates its original date slot; the next job run sees that original date-slot as "missing" and re-materializes a duplicate occurrence at the old date, resurrecting something the DM explicitly moved away from. The same date-keyed check also cannot distinguish "cancelled" from "never created" unless cancellation is itself a first-class row state rather than a deletion.

**Why it happens:** Date-keyed existence checks are the obvious, natural-seeming approach for anyone not already thinking about the move/cancel/edit lifecycle — and this project has no prior "materialize N rows from a recurrence rule, idempotently, across retries" code to draw a pattern from.

**How to avoid:**
1. Give each occurrence a stable identity independent of its date: a `(EventSeriesId, CycleIndex)` pair (an integer counting cycles from the series anchor, respecting the on/off cycle mask), never `(EventSeriesId, Date)`. `CycleIndex` never changes even if a DM moves the occurrence's date.
2. Give `EventOccurrence` a `Status` (e.g., `Scheduled` / `Cancelled` / `Moved` / `EditedIndependently`) rather than deleting cancelled rows. The materialization job's entire top-up logic becomes: *for cycle indices 0..N (within the rolling window) that have no `EventOccurrence` row at all for this series, create one; never touch, delete, or re-date a cycle index that already has a row, regardless of its `Status`.* This single rule mechanically satisfies "don't resurrect cancelled," "don't duplicate moved," and "don't overwrite edited" at once, and makes the job trivially safe to re-run and safe to retry mid-batch (partial completion just means fewer cycle indices got a row this pass; the next run fills in the rest without touching what already exists).
3. Series-rule edits (cadence/cycle-mask/anchor changed by the DM after occurrences already exist and votes have been cast) must apply **only to cycle indices beyond the edit point that have no row yet** — already-materialized rows are immutable snapshots of "what was scheduled," never silently regenerated or re-dated by a later rule change. This directly mirrors this project's own Phase 61 precedent (`Quest.FinalizedDate`/`ProposedDates`/roster are explicitly never touched by a later "Edit finalized quest" action, verified end-to-end into the repository, not just asserted by a test) — the same shape of problem: a later structural edit must not silently invalidate state users have already acted on.

**Warning signs:** Existence checks written as `.Any(o => o.SeriesId == id && o.Date == candidateDate)`; a cancelled occurrence reappearing after the next scheduled job run; a moved occurrence's original slot getting re-created.

**How to verify prevention worked:** A unit test seeding a series with cycle index 5 cancelled and cycle index 8 moved to a different date, running the materialization job twice, and asserting exactly one row exists at each of those cycle indices with `Status` unchanged from the seeded value. A second test asserting a job execution that throws after processing group 3 of 5 (simulating a mid-batch failure + Hangfire's automatic retry) produces no duplicate rows for groups 1-3 on the retry.

**Phase to address:** Phase A (identity/status scheme) and Phase B (move/cancel/edit actions and the series-rule-edit boundary rule).

---

### Pitfall 5: Auto-signup blast radius — "Yes by default" must never be mistaken for "Yes, confirmed," and a re-run must never silently overwrite a real vote

**What goes wrong:** Campaign boards auto-sign-up every current member to every occurrence with `VoteType.Yes`, opt-out. Over a 12-month rolling window with weekly cadence, one series alone creates up to ~52 occurrence rows × every member — for a 15-20 person group running 2-3 concurrent series, that's on the order of 2,000+ signup rows created without a single person taking an action. Several distinct traps compound inside this one mechanism:

1. **Re-run resets a real "No."** If the materialization job's per-occurrence work includes "ensure every current member has a signup row," and it runs that logic again on a later pass (rather than only at the moment an occurrence row is first created), it can silently flip a player's deliberate "No, I can't make training that week" back to the auto-default "Yes" — the single most damaging version of this bug for a small, close-knit trusted group, because it destroys trust in the tool rather than just producing a wrong number.
2. **Auto-Yes read as a real answer.** `VoteType.Yes` from auto-signup and `VoteType.Yes` from a player who deliberately confirmed look byte-for-byte identical in the schema today (`VoteType { No, Maybe, Yes }` has no "never reviewed" state). A DM reading the new availability overview page 4 months out sees "12/12 Yes" with no way to know whether anyone has actually looked at that date.
3. **Membership drift.** A member who joins mid-window: does materialization back-fill signup rows for the ~12 months of *already-existing* future occurrences, or only for occurrences created after they joined? Left undefined, a newly-joined member is silently invisible on the overview page for up to a year. A member who leaves: `UserEntity` is deliberately **excluded** from `HasQueryFilter` (`QuestBoardContext.cs` comment: "breaks ASP.NET Core Identity"), so nothing in the schema automatically hides a departed member's stale rows — a naive overview-page query joining Occurrence → Signup → User with no explicit current-membership check will happily render a former member's leftover "Yes" as if they were still expected to show up.

**Why it happens:** VoteType was designed for One-Shot boards' explicit, one-time date voting — a fundamentally different shape (a handful of proposed dates, voted once, per quest) than "auto-populate a year of rows per member, keep them correct as membership and per-occurrence intent both drift over time."

**How to avoid:**
1. Apply the auto-Yes default **only at the moment a signup row is first created** for a given (Occurrence, Member) pair — never re-run or re-assert it on subsequent job passes. This falls directly out of Pitfall 4's `CycleIndex`-keyed idempotency design: if a signup row already exists for that pair, the job must never touch its `Vote` value again, full stop.
2. Add a field distinguishing "system-defaulted, never reviewed" from "player explicitly confirmed," reusing this project's own Phase 44 precedent rather than inventing a new mechanism — Phase 44 added `LastVoteChangeTime` to `PlayerSignup` for exactly this "when did a human actually touch this" purpose. Surface it as a visually distinct state on the overview grid (e.g., grey/auto icon vs. green/confirmed icon), not just a boolean buried in a tooltip.
3. Define membership-join/leave behavior explicitly rather than let it fall out of implementation accident: decide (and test) whether a new member gets back-filled signup rows for the existing rolling window at join time, and add an explicit current-membership check to every query that joins Occurrence/Signup through to `UserEntity` on the overview page — the same "verify the TARGET resource's group/membership, not just the caller's role" lesson PROJECT.md records from Phase 49, applied here to "current membership" instead of "group."
4. Treat the row-growth number as a real design input, not a hypothetical: with 2-3 concurrent weekly series and ~17 members, expect low-thousands of signup rows within the first year. This is fine for SQL Server at this scale, but it means the overview page (Pitfall 8) cannot afford to load the full 12-month window per render.

**Warning signs:** A player reports "I said no to this and it's showing yes again"; the overview page has no visual difference between an auto-default and a confirmed vote; a departed member still appears on the overview grid.

**How to verify prevention worked:** A unit test asserting the materialization job's second run on an occurrence a player already voted `No` on leaves that vote unchanged; a regression test for a member removed from the group no longer appearing on the overview page query even though their historical signup rows still exist in the table.

**Phase to address:** Phase A (one-time-only default, tied to Pitfall 4's identity scheme) and Phase C (membership-join/leave behavior, the auto-vs-confirmed UI distinction).

---

### Pitfall 6: The Hangfire materialization job runs entirely outside `GroupSessionMiddleware` and the HTTP pipeline — this is the single most likely place a leak or a silent-zero-rows bug enters this feature

**What goes wrong — the mechanism, in depth:**

`GroupSessionMiddleware` — the component that gates every group-scoped HTTP request on having a valid, re-validated `ActiveGroupId` — only runs on the HTTP request pipeline (`InvokeAsync(HttpContext context)`). Hangfire jobs execute on background threads with **no `HttpContext` at all**, so this middleware never runs for them, by construction, for every job this app has ever had and will ever have. The only thing standing in for it is `ActiveGroupContextService`, whose `ActiveGroupId` getter is:

```csharp
public int? ActiveGroupId =>
    _groupIdOverridden
        ? _overriddenGroupId
        : httpContextAccessor.HttpContext?.Session?.GetInt32(SessionKeys.ActiveGroupId);
```

Absent an explicit `SetGroupId()` call (made via `HangfireJobHelper.RunInScopeAsync(scopeFactory, groupId, ...)`), `ActiveGroupId` is `null` inside a job. Because every `HasQueryFilter` in `QuestBoardContext.OnModelCreating` was hardened to be **fail-closed** in Phase 55 (`activeGroupContext.ActiveGroupId != null && e.GroupId == activeGroupContext.ActiveGroupId`), a `null` `ActiveGroupId` today means **every group-scoped EF Core query returns zero rows** — not "all groups merged together." This directly **contradicts** `ActiveGroupContextService.cs`'s own XML doc comment, which still reads: *"Returns null when no override is set and HttpContext is absent — null means 'see all'."* That comment is stale relative to the real, shipped Phase 55 behavior, and is exactly the class of drift PROJECT.md's own Key Decisions table warns about generically ("'Reached only through an already-filtered navigation' code comments must be empirically verified, not trusted" — Phase 49) — here it's not a navigation-path assumption but a *directly misleading doc comment on the exact seam this feature's job depends on most*.

Two distinct, concrete bug shapes fall out of this:

1. **Silent zero-row reads → phantom duplicates or a job that "does nothing" for every group.** The only existing template for a cross-group Hangfire job (`DailyReminderJob`) deliberately calls `HangfireJobHelper.RunInScopeAsync(scopeFactory, groupId: null, ...)` and reads via a **bespoke** repository method, `GetQuestsForTomorrowAllGroupsAsync`, which explicitly calls `.IgnoreQueryFilters()` with the comment *"Explicit cross-group intent — IgnoreQueryFilters bypasses HasQueryFilter on QuestEntity."* A new-code author copying `groupId: null` from that job **without also copying the `IgnoreQueryFilters()` read** will get zero rows back from any *normal* filtered repository call — indistinguishable, from inside the job, from "no groups have any series." Combined with Pitfall 4's idempotency design, if this happens on the *existence check* (not the group-enumeration step), the job will believe no occurrences exist yet for every group and duplicate every already-materialized future occurrence, silently, on every run.
2. **Unfiltered writes — the real leak vector.** EF Core query filters apply to **reads only**; `Add`/`SaveChanges` are never filtered. So even a job that correctly enumerates groups via `IgnoreQueryFilters()` and correctly calls `SetGroupId(group.Id)` before each group's *read* pass has **zero schema-level protection on the write path** — if the code constructing a new `EventOccurrenceEntity` derives `GroupId` from anywhere other than the exact group being iterated right now (a stale captured variable, an outer-scope series lookup that was itself read cross-group and got mismatched to the wrong iteration), the row is written into the **wrong group** with no filter ever firing to catch it. That occurrence then becomes visible on the wrong group's calendar and overview page — a real, silent cross-tenant leak, and structurally the *opposite* failure mode from (1): reads fail closed, writes don't fail at all.
3. **The overview page must never "optimize" its way around the filter.** The new availability overview page (events × members × signups) is a normal HTTP-context page, so `GroupSessionMiddleware` + the fail-closed filters apply automatically — *as long as every query goes through the standard EF Core `DbSet`/repository path*. Given this page is explicitly flagged (Pitfall 8) as a cartesian-explosion risk, there is a real temptation to hand-roll a more "efficient" query using `IgnoreQueryFilters()` plus a manually-appended `.Where(e => e.GroupId == someGroupId)` for performance. If `someGroupId` is sourced from anything other than the same trusted, middleware-validated `IActiveGroupContext.ActiveGroupId` every other group-scoped page uses (e.g., a bindable route/query parameter), this reintroduces exactly the IDOR class of bug Phase 55 already fixed once (`GroupPickerController.SelectGroup` — "validated only that a posted `groupId` existed, never that the caller was a member").

**Why it happens:** This project's only two prior maintenance-style jobs (`DailyReminderJob`, `SessionReminderJob`) each solved this problem correctly but *differently* — one via explicit `IgnoreQueryFilters()` cross-group reads, one via per-quest `SetGroupId(groupId)` — and a new author has no single canonical pattern to copy; the two existing examples look superficially similar (`groupId: null` vs. an explicit int) but mean opposite things depending on whether the repository method underneath is itself filter-aware or filter-bypassing.

**How to avoid:**
1. The materialization job must enumerate groups/series via an explicit, clearly-commented `IgnoreQueryFilters()` cross-group repository method (matching `GetQuestsForTomorrowAllGroupsAsync`'s exact comment style: *"Explicit cross-group intent"*), then for **each** group in that loop, call `groupContext.SetGroupId(group.Id)` before any further read *within that iteration* (idempotency existence checks included) — never rely on the ambient ActiveGroupId being anything but explicitly set per iteration.
2. Every write (`Add(new EventOccurrenceEntity { ... })`) must set `GroupId` from the loop variable of the *current* iteration, never from a captured outer value or a value threaded through more than one level of indirection — treat this as a mandatory code-review checklist item (see below), since nothing in the schema will catch a wrong value here.
3. The overview page must reuse the same filtered repository methods every other group-scoped page uses; `IgnoreQueryFilters()` must never appear in this page's code path under any performance justification, and it must never accept a client-supplied `groupId` parameter.
4. File a follow-up to correct `ActiveGroupContextService.cs`'s stale "null means 'see all'" doc comment to match the actual Phase 55 fail-closed behavior — leaving it as-is is a live trap for the *next* person who reads it while writing Hangfire job code, not just a cosmetic issue.

**Warning signs:** A materialization job that runs without error but produces zero occurrences for any group; occurrences appearing on a group's calendar that no series in that group could have produced; any new repository method called from this job that isn't traceable to either "explicitly filtered after `SetGroupId`" or "explicitly `IgnoreQueryFilters()` with a cross-group comment."

**How to verify prevention worked (the specific checks):**
1. A new `EventBoardContextFilterTests`-style unit test (mirroring the existing `QuestBoardContextFilterTests.cs`) using the InMemory provider, seeding two groups' occurrences, asserting a query with `ActiveGroupId = null` returns **zero** rows and a query with `ActiveGroupId = 1` never returns Group 2's rows.
2. An integration test that runs the materialization job end-to-end against a database seeded with **2+ distinct GroupIds** and asserts, after the run, that every created occurrence's `GroupId` matches the group its series belongs to. This test must **not** use `WebApplicationFactoryBase`'s default `MutableGroupContext` (which defaults to a single `ActiveGroupId = 1`, see Pitfall 9) — it is the one test in this entire feature that specifically must exercise multi-group behavior, since that's precisely the scenario the standing test-suite gap doesn't cover today.
3. A code-review checklist line item specifically for this job: trace every repository call it makes to confirm it is either (a) reached only after an explicit `SetGroupId(group.Id)` scoped to the current loop iteration, or (b) an explicitly-commented `IgnoreQueryFilters()` cross-group method that manually stamps `GroupId` on every write from a value provably originating in the same iteration.

**Phase to address:** Phase A — this is foundational to the materialization job's own design, not something that can be bolted on after. Re-verify explicitly in Phase E (overview page) for the "no `IgnoreQueryFilters()` on this page" rule.

---

### Pitfall 7: Extending the shared, 6-call-site `_Calendar.cshtml` partial for Events repeats this project's own documented near-duplicate-view-drift problem

**What goes wrong:** `_Calendar.cshtml` is rendered from exactly 6 call sites (confirmed by search): `Calendar/Index.cshtml` (1×, the full-board monthly calendar), and `Quest/Details.cshtml` (3×) + `Quest/Details.Mobile.cshtml` (2×), where it serves an entirely different purpose — a small embedded "pick a proposed date" mini-calendar widget inside a quest's signup form, driven by `CalendarViewModel.Quests` built from that one quest's own proposed dates. `CalendarViewModel.GetQuestsForDate` and the partial's Razor markup are entirely `Quest`/`VoteType`/`PlayerSignup`-shaped today, with zero concept of Events.

Extending `CalendarViewModel`/`_Calendar.cshtml` to also carry an `EventsOnDay` collection touches all 6 call sites at once, 5 of which have **no events/series/signup data loaded at all** and no legitimate reason to render Events (a per-quest date-picker widget showing unrelated Events is confusing UX, and would add an events-x-signups fetch to every quest-details page load — 3× per desktop page load). This is the exact shape of bug this project has hit repeatedly and documented explicitly: `Characters/Edit.cshtml` missing a guard its 3 near-duplicate siblings have, `Characters/Create.cshtml`'s dead `isEdit` branch, `BoardType` lookup implemented 3 times with near-identical logic, `.quest-description-mobile` duplicating another class under a different name. A change intended for one call site (`Calendar/Index`) silently reaches 5 others built for an unrelated purpose.

**Why it happens:** `_Calendar.cshtml` is this app's only existing "render a month grid" partial, so it's the obvious thing to reach for — but it was purpose-built as a Quest-date-voting widget first and a full board view second, and nothing in its current shape signals "5 of my 6 callers don't want what you're about to add."

**How to avoid:** Do not extend the shared `_Calendar.cshtml`/`CalendarViewModel` to carry Events at all. Give the full-board `Calendar/Index` view its own Events rendering — either a second partial rendered alongside the existing one, or a distinct `_CalendarWithEvents.cshtml` used only by `Calendar/Index` — leaving the 5 `Quest/Details`(`.Mobile`) mini-calendar call sites byte-for-byte untouched, mirroring this project's own precedent for purely-additive, zero-behavior-change scoping (e.g., Phase 58's rename phase, Phase 59's Rewards field explicitly excluded from paths where it didn't belong). If sharing code IS chosen instead, gate the Events branch behind an explicit `ViewBag.IncludeEvents` flag defaulting to `false`, and run the same kind of repo-wide, independently-derived verification sweep this project used to close its comment-tag cleanup (Phase 34: *"each verifier ran a fresh, independently-derived grep pattern"*) confirming the new branch is provably inert everywhere it's not wanted.

**Warning signs:** A diff touching `_Calendar.cshtml` that doesn't also show a deliberate `ViewBag`/flag check at each of the 5 `Quest/Details` call sites; a quest-details page slowing down or showing unrelated Events after this feature ships.

**How to verify prevention worked:** Manually load all 3 desktop and 2 mobile `Quest/Details` mini-calendar widgets after the change and confirm zero visual or behavioral difference from before; if a shared flag was used, grep-verify every one of the 5 non-`Calendar/Index` call sites explicitly passes `false`/omits the flag.

**Phase to address:** Phase D.

---

### Pitfall 8: New mobile event markup could land in a layout that never renders

**What goes wrong:** This app has a documented, live instance of exactly this bug: `Areas/Platform/Views/Shared/_Layout.Platform.Mobile.cshtml` is dead code, because the Platform area's `_ViewStart.cshtml` unconditionally selects the desktop `_Layout.Platform` — unlike the root `_ViewStart.cshtml`, which correctly branches on `IsMobile`. Two CSS file header comments (`platform-group.mobile.css`, `platform-users.mobile.css`) still incorrectly claim otherwise; this was only discovered by accident during Phase 42 research, not by any test or by the view simply failing to render (it renders fine — just inside the wrong chrome). This app's mobile view selection is **User-Agent-based, not viewport-based** (a separate, previously-noted gap: devtools viewport emulation does not exercise it).

If any part of the Events feature is placed under `Areas/Platform/` (plausible only if a DM-tier event-management screen is modeled as a Platform-area feature by mistake — it should not be, since DM-tier features like Contacts (Phase 57) and Characters are ordinary per-group controllers, not Platform-area ones), a new `.Mobile.cshtml` view for it would silently never render on mobile, exactly like the existing dead layout.

**Why it happens:** The Platform area's `_ViewStart.cshtml` divergence from the root app's is easy to miss because both areas otherwise look symmetric (both have a desktop/mobile layout pair); nothing fails loudly.

**How to avoid:** Place all Events controllers/views under the root `Views/`/ordinary MVC routing (matching `QuestController`/`CalendarController`/`ContactsController`'s precedent), never under `Areas/Platform/`. After adding any `.Mobile.cshtml` view for this feature, verify it renders under a **real mobile User-Agent** (a spoofed UA string or, per this project's own standing requirement for mobile UI phases, a real device over LAN) — not devtools viewport emulation — and confirm the `.Mobile.cshtml` markup, not desktop chrome, is what's actually served.

**Warning signs:** Any new Events file path under `Areas/Platform/`; a mobile UI review done only via devtools viewport resize rather than an actual mobile User-Agent/device.

**How to verify prevention worked:** A real-UA (or real-device, matching Phase 43's iPhone-over-LAN precedent) check of every new Events mobile view, checked off explicitly, not assumed from desktop-emulation testing.

**Phase to address:** Phase D (calendar view) and Phase C/E (any DM-facing event-management or overview mobile views).

---

### Pitfall 9: Cartesian-explosion / N+1 risk on the availability overview page, and the eager-image-loading trap this codebase just finished fixing everywhere else

**What goes wrong:** The overview page's natural shape — events × members × signups, with signups potentially joined through to `Character` for display — is structurally the same multi-collection-`Include` shape that already forced `QuestRepository.GetQuestWithDetailsAsync` onto `.AsSplitQuery()` to avoid EF Core's `MultipleCollectionIncludeWarning`/cartesian row explosion. A materialized 12-month rolling window makes the raw row count much larger than anything Quest has ever produced: one weekly series alone can carry ~52 real occurrence rows per year; joined against ~15-20 members and their signups for 2-3 concurrent series, a single unbounded "load everything" query is on the order of several thousand joined rows for one page render if it isn't bounded by a near-term date window.

Separately, and independently: this project just spent an entire phase (Phase 62, "Stop eagerly loading image bytes in list/entity queries") removing exactly this trap from six other list/detail read paths (`CharacterRepository`, `ContactRepository`, `DungeonMasterProfileRepository`) by replacing `.Include(x => x.ProfileImage)` (which pulls raw `byte[]` image data into every row of a list query) with a lightweight `HasProfilePicture` boolean projection. Any new overview-page repository method that joins Occurrence → Signup → Character/User "for display" and reaches for `.Include(s => s.Character).ThenInclude(c => c.ProfileImage)` reintroduces the exact class of bug this codebase just finished eliminating everywhere else, in a brand-new code path with the highest row-multiplication factor in the app.

**Why it happens:** The overview page is new-code, greenfield territory — it has no existing precedent to inherit correctness from by copy-paste, only ones to actively remember and apply (`AsSplitQuery`/`AsNoTracking` from `QuestRepository`, the boolean-projection pattern from Phase 62).

**How to avoid:**
1. Bound the overview page's query to a near-term window server-side (e.g., next 8-12 weeks, with pagination/lazy-load for anything further out) — never load the full 12-month materialized set and filter in memory.
2. Follow `QuestRepository.ProjectWithoutCharacterImages`'s established pattern (`.AsNoTracking().AsSplitQuery()`) for any multi-collection `Include` this page's repository method needs.
3. Project a `HasProfilePicture`-style boolean instead of `.Include`-ing character/DM-profile image bytes, exactly mirroring Phase 62's fix, from the very first version of this repository method — don't ship the eager-load version and fix it in a follow-up phase the way the other 6 call sites had to be.

**Warning signs:** A repository method backing the overview page with an unguarded multi-collection `.Include()` and no `.AsSplitQuery()`; the overview page rendering `byte[]` image data through a `.Include(...ProfileImage)` chain; page load time scaling visibly with the number of active series rather than the near-term window size.

**How to verify prevention worked:** `dotnet ef` / SQL Server Profiler check confirming the overview page issues split queries (not one cartesian join) and never selects an image `byte[]` column; a load test seeding 3 series × 12 months × 20 members confirming render time is bounded by the near-term window, not the full materialized set.

**Phase to address:** Phase E.

---

### Pitfall 10: The integration-test harness is structurally blind to exactly the two things this feature depends on most — multi-group execution and real Hangfire semantics

**What goes wrong:** `WebApplicationFactoryBase`'s `MutableGroupContext` test double defaults `ActiveGroupId = 1` and `BoardType = OneShot`, and the Testing environment registers `NullQuestEmailDispatcher`/`NullReminderJobDispatcher` because Hangfire itself is never registered in Testing at all (`Program.cs`'s `else` branch). PROJECT.md already documents the general shape of this gap: *"Integration tests always override `IActiveGroupContext`/`IBoardTypeResolver` with a test double... so no automated test exercises `Program.cs`'s real production DI graph end-to-end"* — and that a real regression class (the Phase 37 circular-DI bug) *"wouldn't be caught by the current suite"* and was only caught by a manual `dotnet run` smoke test.

For this feature specifically, that gap lines up exactly with its two highest-risk mechanisms: (1) Pitfall 6's multi-group tenant-scoping bug can only be exercised by a test that explicitly seeds 2+ `GroupId`s and asserts cross-group isolation — the default single-group harness will pass green regardless of whether the job's multi-group logic is correct or badly broken; (2) Hangfire-specific behaviors (its distributed lock preventing concurrent execution of the same recurring-job id, `[AutomaticRetryAttribute]`'s re-run-from-scratch semantics on exception) cannot be exercised at all through the standard test harness, since Hangfire isn't running in Testing — the materialization job class itself can still be unit/integration tested by constructing it directly and calling `ExecuteAsync()` (the same way `DailyReminderJobTests.cs` already mocks its way around Hangfire today), but that approach, by construction, will never catch a bug specific to Hangfire's own scheduling/locking/retry mechanics.

**Why it happens:** The test infrastructure was built for a single-tenant-per-test-run mental model (matching how every other feature in this app has been tested so far, including Quest/Character/Contact, none of which have ever needed a Hangfire job that reasons about *multiple* groups in one execution).

**How to avoid:**
1. Do not trust a green run of the standard integration suite as evidence this feature's tenant-scoping is correct — it structurally cannot detect the bug class most likely to occur here (Pitfall 6). Write the dedicated multi-group materialization test called out in Pitfall 6 explicitly; do not consider that pitfall closed without it.
2. Accept, explicitly and in writing (not silently), that Hangfire-specific retry/lock/concurrency behavior is a known, permanent gap in this feature's automated coverage — plan for a manual `/hangfire` dashboard + live `dotnet run` smoke test after initial deploy (mirroring the Phase 37 precedent), and re-run it after any change to the job's registration or retry configuration.
3. Where the job class is unit-tested directly (bypassing Hangfire), explicitly test the *idempotency* behavior described in Pitfall 4 by simulating a retry (call `ExecuteAsync()` twice, or call it once, throw partway through via a test double, then call it again) rather than only testing a single successful run — a single-run-only test suite would look complete while never actually exercising the retry-safety property this feature depends on most.

**Warning signs:** A materialization-job test suite where every test uses the default single-group `MutableGroupContext`; no test that simulates a second `ExecuteAsync()` call against state left by the first.

**How to verify prevention worked:** Presence of the specific multi-group integration test (Pitfall 6) and the retry-simulation unit test (Pitfall 4) in the actual test suite, reviewed as an explicit code-review checklist item rather than inferred from overall test-count/green-CI alone.

**Phase to address:** Phase A (test design, alongside the job itself) — flag explicitly during Phase A's plan/discuss step, since it shapes how the job's tests must be written from day one, not bolted on after.

---

### Pitfall 11: Several new tables under `context.Database.Migrate()` risk taking down the entire quest board on a bad migration, not just the new feature

**What goes wrong:** Migrations auto-apply on every startup via a single blocking `context.Database.Migrate()` call in `ServiceExtensions.ConfigureDatabase()`, which runs before the web server starts accepting requests. This feature ships at minimum 3 new interrelated tables (EventSeries, EventOccurrence, EventSignup, plausibly more) with several FKs to existing tables (`Groups`, `Users`, `Characters`) in a single migration set — more interrelated new schema in one feature than this project has shipped in one migration set before. If any one migration in the set has a SQL error (a bad cascade path, a NOT NULL column referencing a table not yet created within the same batch, a naming collision), `Migrate()` throws during startup and the **entire application fails to start** — not just the Events feature, but the Core Value flow ("DMs post quests and players sign up") this project's own PROJECT.md names as the thing that must never break.

**Why it happens:** This project's standing convention (`dotnet ef migrations add`, scaffolded, auto-applied, no manual `database update` step) has served it well for single- or few-table changes, but has never been stress-tested against a several-table, heavily-interrelated new feature shipped as one migration set.

**How to avoid:**
1. Split into small, additive, independently-appliable migrations per table where feasible, matching this project's own established preference for minimal, reviewable EF-scaffolded diffs (Phase 45's rename migration is the explicit precedent: *"EF Core 10 scaffolded the rename cleanly... zero `DropColumn`, no hand-editing needed"*).
2. Dry-run every migration against a real copy of the dev database before merge — this is not new advice invented for this feature; it is this project's own explicit prior recommendation, verbatim, from Phase 45's research: *"the hand-edited migration should still be manually dry-run verified... since auto-apply-on-startup does not substitute for pre-merge verification against a populated database."*
3. Design and test each new FK's delete behavior explicitly rather than accepting EF Core scaffold defaults — deleting an `EventSeries` should plausibly cascade to its `EventOccurrence` rows, but must never cascade into `UserEntity` (mirroring the exact `NoAction`-FK caution PROJECT.md records from Phase 41's `SAFE-01` work, where hard user-deletion was deliberately avoided specifically because of `DbUpdateException` risk from `NoAction` FKs elsewhere in the schema).

**Warning signs:** A migration set generated without individually reviewing each `Up()`/`Down()` pair; no dry-run against a populated database before merge; cascade-delete behavior left at EF Core scaffold defaults without an explicit decision recorded.

**How to verify prevention worked:** Each migration in the set applied successfully, in order, against a full copy of the real dev database (not just the InMemory test provider, which never runs migration SQL at all) before the phase is considered done; an explicit note of the chosen delete behavior for every new FK.

**Phase to address:** Phase A (schema migrations land here; every later phase's migrations should be additive on top, not restructuring).

---

## Technical Debt Patterns

| Shortcut | Immediate Benefit | Long-term Cost | When Acceptable |
|----------|-------------------|-----------------|------------------|
| Copying `DailyReminderJob`'s `groupId: null` pattern without also copying its bespoke `IgnoreQueryFilters()` read method | Less code to write initially | Silent zero-row reads or a real cross-group write leak (Pitfall 6) | Never — always pair `groupId: null` with an explicitly-commented cross-group repository method |
| Keying occurrence existence checks on `(SeriesId, Date)` instead of `(SeriesId, CycleIndex)` | Simpler mental model, matches how a human describes "the Saturday event" | Resurrects moved occurrences, can't distinguish cancelled-vs-never-created (Pitfall 4) | Never |
| Re-running the auto-signup default on every job pass instead of only at row creation | Simpler job logic, no "has this already been decided" check needed | Silently overwrites a player's real vote (Pitfall 5) | Never |
| Reusing `_Calendar.cshtml` for Events by adding a conditional branch instead of a separate partial | Less new Razor to write | Reaches 5 unrelated call sites, repeats this project's own documented view-drift problem (Pitfall 7) | Only if gated behind an explicit `ViewBag` flag defaulting off, with a repo-wide grep-verified sweep confirming inertness at the other 5 sites |
| Loading the full 12-month materialized window on the overview page and filtering in memory | Simpler query, works fine in dev with a handful of seeded rows | Cartesian-explosion page load once real data accumulates (Pitfall 9) | Never in production-shaped code; acceptable only as a throwaway first draft explicitly marked for the near-term-window fix before merge |

## Integration Gotchas

| Integration | Common Mistake | Correct Approach |
|-------------|------------------|-------------------|
| Hangfire recurring job (materialization) | Registering it before `app.Services.ConfigureDatabase()`, causing it to fire before the new Events tables' migrations have applied | Register `RecurringJob.AddOrUpdate<EventMaterializationJob>(...)` after `ConfigureDatabase()`, inside the `!IsEnvironment("Testing")` guard — identical placement to `DailyReminderJob`'s existing registration |
| `IActiveGroupContext` inside a Hangfire job | Assuming a `null` `ActiveGroupId` means "see all groups" (the stale doc comment in `ActiveGroupContextService.cs`) | It means zero rows post-Phase-55 (fail-closed). Use `IgnoreQueryFilters()` explicitly for cross-group reads, `SetGroupId(group.Id)` explicitly per iteration for group-scoped reads/writes |
| EF Core `HasQueryFilter` on new Event entities | Assuming the filter protects writes the way it protects reads | Filters apply to reads only — every `Add()` needs its `GroupId` traced back to the correct loop iteration by hand |
| `VoteType` reuse for event signups | Assuming `Yes` always means "player confirmed" | Auto-signup-default `Yes` and player-confirmed `Yes` need a distinguishing field (reuse `LastVoteChangeTime`'s precedent from Phase 44) |

## Performance Traps

| Trap | Symptoms | Prevention | When It Breaks |
|------|----------|------------|-----------------|
| Unbounded 12-month occurrence×member×signup join on the overview page | Page load time growing with total materialized rows, not with what's actually displayed | Bound the query to a near-term window server-side; `.AsNoTracking().AsSplitQuery()` per `QuestRepository`'s established pattern | Noticeable once 2-3 concurrent weekly series accumulate a few months of history — i.e., within the first season of use, not hypothetically far out |
| Eager-loading Character/profile image bytes through Occurrence→Signup→Character joins | Overview page payload size scaling with member count × occurrence count × image size | Project `HasProfilePicture`-style booleans, exactly matching Phase 62's fix for every other list view | Immediately, the first time the overview page is used with real profile photos uploaded |
| Materialization job doing one DB round-trip per occurrence per member instead of batched inserts | Job execution time growing linearly with series count × window length × member count, risking the retry/duplicate interaction in Pitfall 4 if it runs long enough to hit Hangfire's lock/timeout window | Batch-insert occurrences and signups per group/series in as few `SaveChangesAsync()` calls as reasonable | Once several series × 12 months × double-digit membership is real data, not seed data |

## Security Mistakes

| Mistake | Risk | Prevention |
|---------|------|------------|
| Materialization job writes `EventOccurrence.GroupId` from a stale/mis-scoped variable instead of the current loop iteration's group | Silent cross-tenant data leak — an occurrence physically stamped into the wrong group, visible to that group's members with no filter ever firing (writes aren't filtered) | Code-review checklist item tracing every write's `GroupId` source to the current iteration; the multi-group integration test from Pitfall 6 |
| Overview page reaches for `IgnoreQueryFilters()` "for performance" and re-derives group scoping from a client-supplied parameter | Reintroduces the exact IDOR class Phase 55 already fixed once (`GroupPickerController.SelectGroup`) | Never use `IgnoreQueryFilters()` on the overview page; always source group scope from the middleware-validated `IActiveGroupContext`, never from a route/query parameter |
| A departed member's stale auto-signup rows still resolving on the overview page (since `UserEntity` has no `HasQueryFilter`) | A former member's leftover "Yes" silently misread by a DM as a still-expected attendee | Explicit current-membership check on every query joining Occurrence/Signup through to `UserEntity`, mirroring Phase 49's "verify the target resource, not just the caller" lesson |

## UX Pitfalls

| Pitfall | User Impact | Better Approach |
|---------|-------------|-------------------|
| Auto-signup `Yes` rendered identically to a player-confirmed `Yes` | DM reads "12/12 Yes" on a date nobody has actually looked at, makes a scheduling decision on false confidence | Distinct visual state (grey/auto vs. green/confirmed) on the overview grid, backed by a reviewed-vs-never-touched field |
| A moved/cancelled occurrence silently reappearing after a job re-run | Players lose trust in the calendar's accuracy — "I cancelled that and it came back" | `CycleIndex`-keyed idempotency (Pitfall 4) makes this structurally impossible, not just unlikely |
| Events surfacing inside the per-quest mini-calendar widget on `Quest/Details` | Confusing, unrelated content injected into a focused date-voting UI | Keep Events entirely out of the 5 `Quest/Details`(`.Mobile`) call sites (Pitfall 7) |

## "Looks Done But Isn't" Checklist

- [ ] **Recurrence generation:** Often missing DST-boundary coverage — verify with a unit test spanning both the March and October transitions of a real year, asserting wall-clock time is preserved (Pitfall 1)
- [ ] **Materialization job idempotency:** Often "works" on a single clean run but never tested against a simulated retry or a second execution against already-materialized state — verify with a double-`ExecuteAsync()` test and a mid-batch-throw-then-retry test (Pitfall 4, 10)
- [ ] **Multi-group correctness:** Often "works" in every test because the default test harness (`MutableGroupContext`) only ever exercises a single group — verify with a dedicated 2+-group integration test (Pitfall 6, 10)
- [ ] **Auto-signup one-time-only:** Often re-applies the Yes default on every job pass because the "already decided" check was written for occurrence existence, not per-member signup existence — verify with a test asserting a second job run never touches an existing signup's `Vote` (Pitfall 5)
- [ ] **Mobile Events views:** Often verified only via devtools viewport emulation — verify with a real mobile User-Agent or real device, per this app's own standing UA-based (not viewport-based) mobile-selection mechanism (Pitfall 8)
- [ ] **Overview page query shape:** Often looks fine with seed data, hides an unbounded join until real data accumulates — verify with a load test seeding several months of real-shaped data, not just a handful of rows (Pitfall 9)
- [ ] **Migration set dry-run:** Often only verified via the InMemory test provider, which never executes migration SQL at all — verify by applying every migration against a real copy of the populated dev database (Pitfall 11)

## Recovery Strategies

| Pitfall | Recovery Cost | Recovery Steps |
|---------|-----------------|------------------|
| DST wall-clock drift already shipped to production occurrences | MEDIUM | Write a one-time backfill script correcting affected occurrences' `TimeOfDay` to match their series anchor, scoped to `Status = Scheduled` rows only (never touch `Cancelled`/`Moved`/`EditedIndependently` — those are user-owned state); notify affected DMs before running it |
| Duplicate occurrences created by a non-idempotent materialization pass | LOW–MEDIUM | Identify duplicates by `(SeriesId, CycleIndex)` (not date, which may have been independently edited on one of the pair), keep the earliest-created row per pair, delete the rest **only if no signups/votes exist on the duplicate** — otherwise flag for manual DM review rather than auto-deleting voted-on data |
| A real cross-tenant leak from a mis-scoped write (Pitfall 6) already shipped | HIGH | Same remediation shape as this project's own Phase 55 incident: identify every affected row via a targeted query, correct or delete them, add the missing regression test, and treat it with the same "fail-closed defense-in-depth" response Phase 55 used — layer the fix on top of, not instead of, the existing filter |
| Overview page performance degrading in production after real data accumulates | LOW | Retrofit the near-term-window bound and `AsSplitQuery()`/projection fixes (Pitfall 9) — purely additive, no data migration needed |

## Pitfall-to-Phase Mapping

| Pitfall | Prevention Phase | Verification |
|---------|-------------------|----------------|
| 1. DST wall-clock drift | Phase A | Unit test spanning both real-year DST transitions, asserting `TimeOfDay` preserved across 26+ cycles |
| 2. Unpinned container timezone | Phase A | Startup log line for `TimeZoneInfo.Local.Id`; deploy-time host TZ check recorded explicitly, not assumed |
| 3. Silent Hangfire job stoppage / horizon depletion | Phase A | Structured per-run log summary + in-app horizon health check surfaced on Calendar/overview load |
| 4. Idempotency (resurrect/duplicate/overwrite) | Phase A (identity/status scheme), Phase B (move/cancel/edit + series-rule-edit boundary) | Cancel-then-rerun and mid-batch-retry unit tests; series-rule-edit test asserting already-materialized rows untouched |
| 5. Auto-signup blast radius | Phase A (one-time-only default), Phase C (membership drift, auto-vs-confirmed UI) | Second-run test asserting existing votes untouched; membership-leave query test |
| 6. Hangfire job outside HTTP/tenant context | Phase A (job design), Phase E (overview page's "no `IgnoreQueryFilters()`" rule) | Dedicated 2+-group integration test; `EventBoardContextFilterTests` fail-closed unit test; code-review GroupId-tracing checklist |
| 7. Shared `_Calendar.cshtml` 6-call-site drift | Phase D | Manual check of all 5 non-`Calendar/Index` call sites showing zero behavioral change; grep-verified flag inertness if shared code is used |
| 8. Mobile view-selection dead-code trap | Phase D, Phase C/E | Real mobile User-Agent/device check of every new `.Mobile.cshtml` view, not devtools emulation |
| 9. Overview page N+1 / cartesian explosion / eager image loads | Phase E | Split-query + near-term-window verification under a realistic multi-month, multi-member load test |
| 10. Test-harness blind spots (multi-group, real Hangfire) | Phase A | Presence of the multi-group integration test and retry-simulation unit test, checked explicitly in code review, not inferred from green CI |
| 11. Migration safety for several new interrelated tables | Phase A | Every migration dry-run applied against a populated dev database copy, in order, before merge; explicit FK delete-behavior decisions recorded |

## Sources

- `.planning/PROJECT.md` — "Known issues / tech debt," "Constraints," "Key Decisions" sections (full read); specifically the `FinalizedDate`/DST tech-debt note, the Phase 55 cross-tenant leak and fail-closed filter decision, the Phase 49 target-resource-verification lesson, the Phase 37 DI-graph test-gap note, the Phase 62 eager-image-loading fix, the Phase 42/Platform-mobile-dead-layout note, and the Phase 45 migration-dry-run recommendation
- `QuestBoard.Repository/Entities/QuestBoardContext.cs` — exact `HasQueryFilter` predicates for every tenant-scoped entity, confirmed fail-closed
- `QuestBoard.Service/Services/ActiveGroupContextService.cs` — confirmed stale "null means 'see all'" doc comment contradicting the real Phase 55 behavior
- `QuestBoard.Service/Middleware/GroupSessionMiddleware.cs` — confirmed HTTP-pipeline-only scope, never reached by Hangfire jobs
- `QuestBoard.Service/Jobs/DailyReminderJob.cs`, `SessionReminderJob.cs`, `HangfireJobHelper.cs` — this project's only existing precedents for cross-group vs. per-group job execution
- `QuestBoard.Repository/QuestRepository.cs` — `GetQuestsForTomorrowAllGroupsAsync`'s `IgnoreQueryFilters()` pattern; `ProjectWithoutCharacterImages`'s `AsNoTracking()/AsSplitQuery()` pattern
- `QuestBoard.Service/ViewModels/CalendarViewModels/CalendarViewModel.cs`, `QuestBoard.Service/Views/Shared/_Calendar.cshtml` — confirmed Quest-only shape and exact 6 call sites (`Calendar/Index.cshtml`, `Quest/Details.cshtml` ×3, `Quest/Details.Mobile.cshtml` ×2)
- `QuestBoard.IntegrationTests/Helpers/MutableGroupContext.cs` — confirmed single-group (`ActiveGroupId = 1`), single-BoardType test-harness default
- Repo-wide search for `DateTime.Now`/`DateTime.UtcNow`/`DateTime.Today`/`TimeZoneInfo` across `QuestBoard.Service`/`QuestBoard.Domain` — confirmed no timezone pinning anywhere, and confirmed the existing local/UTC comparison mismatch on `FinalizedDate` call sites in `QuestLogController`, `QuestService.cs`, and 5 Razor views

---
*Pitfalls research for: Calendar Events feature, v9.0 milestone*
*Researched: 2026-08-25*
