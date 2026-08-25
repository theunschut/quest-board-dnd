# Stack Research: Calendar Events (v9.0)

**Domain:** Recurring calendar events feature (EventSeries + Event occurrences) for an existing ASP.NET Core 10 MVC / EF Core 10 / Hangfire 1.8 app
**Researched:** 2026-08-25
**Confidence:** MEDIUM-HIGH (versions verified against nuget.org and Microsoft Learn directly; Hangfire job pattern verified against this codebase's own source, not just docs)

## Recommended Stack

### Core Technologies

No new core technologies are required. This feature is built entirely on the app's existing stack:

| Technology | Version | Purpose | Why Recommended |
|------------|---------|---------|-----------------|
| EF Core 10 (SQL Server provider) | already pinned | Persist `EventSeries`/`Event` entities, native `DateOnly`/`TimeOnly` column mapping | No new package needed — `DateOnly`→`date` and `TimeOnly`→`time` mapping has been native since EF Core 8 and is unchanged in EF Core 10 |
| Hangfire 1.8.x (`RecurringJob.AddOrUpdate`) | 1.8.24 latest in the 1.8 line (published 2026-07-16) — project already pins 1.8, a routine in-range bump is safe, no forced upgrade needed | Rolling-window top-up job that materializes `Event` rows ~12 months ahead | Already the app's only background-job mechanism; a second scheduler would violate the no-framework-changes constraint |
| Custom cycle-mask generator (plain C#, no package) | n/a | Computes occurrence dates from `EventSeries` (cadence + mask + anchor) | See "RRULE-expressibility" below — the decided rule shape is not a single-RRULE pattern, so a ~40-line generator is both simpler and more correct than adopting a spec built for something else |

### Supporting Libraries

None needed. Do not add a recurrence/iCalendar package — see "What NOT to Use."

### Development Tools

| Tool | Purpose | Notes |
|------|---------|-------|
| xUnit (existing) | Unit-test the occurrence generator in isolation | The generator is pure date math (no DB, no HTTP) — write it as a `static` method or small stateless service so it's trivially unit-testable with table-driven test cases (anchor, cadence, mask, N occurrences expected) |

## Installation

```bash
# No new NuGet packages required for this feature.
# EventSeries/Event entities, the occurrence generator, and the Hangfire top-up job
# are all plain C# added to the existing three-layer solution.
```

---

## 1. Recurrence representation: custom cycle-mask model, not RRULE/Ical.Net

**Verdict: build the custom cycle-mask model. Do not adopt RFC 5545 RRULE or `Ical.Net` for the generation engine.**

### Is the decided rule expressible in a single RRULE?

No — and this is answerable concretely from the RFC 5545 spec text, not a hand-wave:

- `INTERVAL=2` on a `WEEKLY` rule with `BYDAY=SA` gives you "every 2 weeks on Saturday" — that part of the requirement maps cleanly.
- `BYSETPOS` selects the Nth match **within one interval/period of the rule** (e.g. "the last weekday of this month," "the 2nd Tuesday of this month"). It does not operate across periods — RRULE has no concept of "keep periods 1 and 2, drop periods 3 and 4, repeat," because `BYSETPOS`'s scope is always a single expansion period, not a rolling window over multiple periods.
- There is no RRULE part (`INTERVAL`, `BYDAY`, `BYSETPOS`, `BYWEEKNO`, or any combination) that expresses "generate the biweekly-Saturday sequence, then apply a repeating on/on/off/off mask over the resulting occurrences." That is a second-order filter over a first-order recurrence, and RFC 5545 doesn't model second-order filters.
- The only ways to get this out of RRULE/`Ical.Net` would be: (a) generate the full biweekly occurrence set programmatically and post-filter every 3rd/4th item — at which point the RRULE engine is contributing nothing except an occurrence generator that a 10-line loop replaces — or (b) hand-maintain two separate `RRULE`s with `INTERVAL=8` (4-week cadence) offset by 0 and 2 weeks respectively to approximate a 2-on/2-off mask, which stops generalizing the moment the mask isn't exactly 2-on/2-off (e.g. a 3-on/1-off or asymmetric mask) and silently breaks the "mirrored mask on a second board, same anchor" requirement, since keeping two independently-configured `RRULE` strings anchor-synchronized is itself extra bookkeeping RRULE doesn't help with.
- The "mirrored mask on a second board" requirement (`[on,on,off,off]` vs `[off,off,on,on]`, same cadence/anchor) is trivial in the cycle-mask model — same anchor, same cadence, an inverted mask array — but has no first-class RRULE representation at all.

Conclusion: RRULE was designed for calendar-interop scenarios (Outlook/Google Calendar/CalDAV) where recurrence rules must be exchanged with other calendar systems. This project has no such interop requirement — events aren't exported to `.ics`, synced to Google Calendar, or consumed by another calendaring client. Adopting RRULE here would mean bending the operator's already-decided cadence+mask+anchor model into RRULE's vocabulary, then post-filtering anyway, for a spec compliance benefit nobody consumes. A custom generator that mirrors the decided model 1:1 is more readable, easier to unit test (table-driven: given anchor/cadence/mask, assert the next N dates), and has zero mapping/translation layer between "what the operator specified" and "what the code computes."

### If a library were warranted anyway — Ical.Net verified specs

For completeness (e.g. if a future milestone adds `.ics` export/import), `Ical.Net` is the standard .NET choice:

| Attribute | Value | Source |
|---|---|---|
| Latest version | 5.2.3 | nuget.org/packages/Ical.Net (fetched directly) |
| Published | 2026-06-23 | nuget.org |
| License | MIT | nuget.org |
| Total downloads | ~35.1M (336K on current version) | nuget.org — actively used, healthy adoption signal |
| Target frameworks | .NET 6/8/9/10, netstandard | actively maintained against current .NET |

Not recommended for this phase — it solves a problem (RFC 5545 compliance/interop) this feature doesn't have, and it cannot express the mask requirement any more directly than hand-rolled code can.

### Recommended generator shape

```csharp
// Domain layer — pure function, no EF/DB dependency, trivially unit-testable
public static class EventOccurrenceGenerator
{
    public static IEnumerable<DateOnly> GenerateDates(
        DateOnly anchor,
        int intervalWeeks,      // cadence: every N weeks
        DayOfWeek weekday,      // "on a given weekday"
        IReadOnlyList<bool> cycleMask, // e.g. [true, true, false, false]
        DateOnly windowStart,
        DateOnly windowEnd)
    {
        var cadenceIndex = 0;
        for (var date = anchor; date <= windowEnd; date = date.AddDays(intervalWeeks * 7))
        {
            if (date >= windowStart && cycleMask[cadenceIndex % cycleMask.Count])
            {
                yield return date;
            }
            cadenceIndex++;
        }
    }
}
```

This same method backs both the live "next ~10 dates" preview on the series setup screen (call it with a small `windowEnd`, e.g. anchor + 2 years, `.Take(10)`) and the Hangfire top-up job (call it with `windowEnd = today + 12 months`) — one code path, no drift between preview and materialization.

---

## 2. Date/time storage: `DateOnly` for dates, `TimeOnly?` for optional start time, IANA-aware `TimeZoneInfo` only if you outgrow "server local time"

### Recommendation per field

| Field | Type | SQL Server column | Rationale |
|---|---|---|---|
| `Event.OccurrenceDate` (materialized occurrence) | `DateOnly` | `date` | An occurrence is a calendar day, not a timestamp — `DateOnly` says so at the type level and is EF Core 10-native (no conversions, no ambiguity about "which timezone is this date in") |
| `Event.StartTime` (optional) | `TimeOnly?` | `time` (nullable) | A wall-clock time of day ("starts at 19:00"), decoupled from any date/timezone concern — matches "optional start time" exactly; `TimeOnly` avoids the classic bug of stuffing a time-of-day into a `DateTime` with a throwaway date component |
| `EventSeries.AnchorDate` | `DateOnly` | `date` | Same reasoning as occurrence date — the anchor is "Saturday, Sep 5," not a timestamp |

Do **not** use `DateTimeOffset` for any of these three fields. `DateTimeOffset` is for instants that must be unambiguous across timezones (e.g. "this webhook fired at this exact moment") — it is the wrong type for "this event happens on this calendar date," because it forces you to pick and bake in a UTC offset for a date that, semantically, has none. Introducing `DateTimeOffset` here would also be inconsistent with the existing `FinalizedDate : DateTime` (server-local) column the reminder job already reads with `DateTime.Today.AddDays(1)` — mixing two timezone-representation strategies across sibling scheduling features (Quest dates vs Event dates) is a worse outcome than picking one deliberate approach for the new feature and leaving the existing tech debt exactly as documented.

**EF Core 10 mapping confirmed:** `DateOnly` → SQL Server `date`, `TimeOnly` → SQL Server `time`, `DateTime` → `datetime2`, `DateTimeOffset` → `datetimeoffset`. This has been EF Core's native SQL Server provider behavior since EF Core 8 (no `EFCore.SqlServer.DateOnlyTimeOnly` community package needed — that package only exists for EF Core 6/7) and is unchanged in EF Core 10.

### DST / 12-month rolling window — what actually matters here

The cadence math in the generator above (`date.AddDays(intervalWeeks * 7)`) operates entirely on `DateOnly`, which has no concept of time-of-day or timezone — `AddDays` on a calendar date is immune to DST by construction. **DST only becomes a real concern the moment you convert an `Event`'s date + optional `StartTime` into an actual instant** (e.g. to schedule an email reminder N hours before the event, the way `SessionReminderJob` already does for quests). That conversion is the one place `TimeZoneInfo` matters:

```csharp
var localDateTime = occurrenceDate.ToDateTime(startTime ?? TimeOnly.MinValue);
var tz = TimeZoneInfo.FindSystemTimeZoneById("Europe/Amsterdam"); // IANA id — see below
var utcInstant = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(localDateTime, DateTimeKind.Unspecified), tz);
```

Given this app's existing accepted pattern — `FinalizedDate` stored server-local, reminder job uses `DateTime.Today` and is explicitly documented as "correct for now, review if deployment timezone changes" — the pragmatic, consistent choice for v9.0 is: **store `Event` dates/times as plain server-local values (`DateOnly`/`TimeOnly`, no offset), exactly like `Quest.FinalizedDate` today.** Do not introduce `TimeZoneInfo`-based conversion into this feature unless a reminder/notification job is added for Events in this same milestone — if it is, resolve the timezone once via config (see below) rather than hardcoding it, since that's a small amount of extra work that removes the exact fragility already called out as tech debt for the Quest reminder job.

**If/when a timezone-aware conversion is needed** (e.g. an Event reminder job), confirmed current behavior for this Linux LXC host:

- `TimeZoneInfo.FindSystemTimeZoneById` on Linux/macOS resolves ids via the ICU library and natively supports **IANA ids** (`"Europe/Amsterdam"`), not Windows ids (`"Central European Standard Time"` is not guaranteed to resolve on Linux). This is unchanged and confirmed current in the .NET 10 docs (Microsoft Learn, `net-10.0` moniker, updated 2026-08-03).
- Since .NET 6, Windows *also* accepts IANA ids, so using IANA ids everywhere (including any dev-machine Windows testing) is the portable choice — this project's CLAUDE.md already flags the dev/prod OS split (Windows dev, Linux prod), so an IANA id is the only choice that works unmodified in both environments.
- Id lookup is case-insensitive on all platforms.
- Since .NET 8, `FindSystemTimeZoneById` returns a cached `TimeZoneInfo` instance — safe to call per-request/per-job without a manual cache.
- Concretely: put the IANA id in config (e.g. `appsettings.json` → `"TimeZone": "Europe/Amsterdam"`), resolve it once via `TimeZoneInfo.FindSystemTimeZoneById`, and use it for any Event-instant conversion — this is strictly better than the current hardcoded assumption in `DailyReminderJob`, without having to fix that job's existing debt in this milestone.

---

## 3. Hangfire recurring top-up job: reuse this codebase's exact existing pattern

Confirmed by reading the app's own `Program.cs` and `Jobs/` folder (not just docs) — the app already has 7 job classes and one registration convention. **The Event materialization job should be the 8th, built identically:**

### Registration (`Program.cs`, inside the existing migration-guarded block)

```csharp
if (!app.Environment.IsEnvironment("Testing"))
{
    app.Services.ConfigureDatabase();

    RecurringJob.AddOrUpdate<DailyReminderJob>(
        "daily-session-reminders",
        job => job.ExecuteAsync(CancellationToken.None),
        "0 9 * * *");

    // New: top up materialized Event occurrences on a rolling 12-month window
    RecurringJob.AddOrUpdate<EventOccurrenceTopUpJob>(
        "event-occurrence-topup",
        job => job.ExecuteAsync(CancellationToken.None),
        "0 3 * * *"); // once daily is enough for a 12-month rolling window
}
```

Hangfire.Core/AspNetCore/NetCore latest is **1.8.24** (published 2026-07-16) — stays within the 1.8.x line already pinned in this project; a routine patch bump, no breaking changes affecting `RecurringJob.AddOrUpdate`.

### Job class shape (constructor injection, no Hangfire-specific DI magic)

```csharp
public class EventOccurrenceTopUpJob(
    IServiceScopeFactory scopeFactory,
    ILogger<EventOccurrenceTopUpJob> logger)
{
    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        await HangfireJobHelper.RunInScopeAsync(scopeFactory, groupId: null, async sp =>
        {
            var seriesRepository = sp.GetRequiredService<IEventSeriesRepository>();
            var activeSeries = await seriesRepository.GetActiveSeriesAsync(cancellationToken);

            foreach (var series in activeSeries)
            {
                // materialize/upsert — see idempotency below
            }
        });
    }
}
```

This follows the same constructor-injection + `IServiceScopeFactory`-via-`HangfireJobHelper.RunInScopeAsync` pattern every existing job (`DailyReminderJob`, `SessionReminderJob`, `WelcomeEmailJob`, etc.) already uses — scoped services (`DbContext`, repositories) are never constructor-injected directly into a job class, because Hangfire jobs are resolved outside a request scope. `groupId: null` is correct here since a cross-group sweep is exactly what `DailyReminderJob` already does for the identical reason (materialization needs to run for every group's series, not one tenant).

### Idempotency (safe to run twice without duplicating occurrences)

Two complementary mechanisms, both consistent with existing codebase precedent (`ReminderLog` dedup table for the email reminder job):

1. **Unique constraint at the DB level** — `(EventSeriesId, OccurrenceDate)` unique index on `Event`. This is the hard guarantee: even if the job races itself (e.g. a slow run overlaps the next day's trigger), a duplicate insert throws rather than silently creating a second row for the same date.
2. **Query-before-insert / high-watermark check in the job** — for each active series, compute the already-materialized max `OccurrenceDate`, generate only dates after that point up to the rolling window's end (`today + 12 months`), and skip series that are already topped up. This makes the common case (nothing new to do) a cheap no-op rather than relying on the unique-index catch as the primary mechanism.

Do not reach for a third-party dedup package (e.g. `Hangfire.Idempotent`) — that solves "don't enqueue the same job twice," which isn't the actual risk here (Hangfire's own recurring-job scheduler already guarantees single execution per trigger). The real risk is "don't insert the same `Event` row twice across job runs," which is a domain-data problem solved by the unique index + watermark check above, not a job-scheduling problem.

### Avoiding a silent failure

The app already has two layers of this covered app-wide, and this job should rely on them rather than inventing new infrastructure:

- **Global automatic retry** — `Program.cs` already registers `GlobalJobFilters.Filters.Add(new AutomaticRetryAttribute { Attempts = 5, DelaysInSeconds = [1, 2, 4, 8, 16] })` for every job in the app, this one included by default. A single transient failure (e.g. a DB blip) self-heals without any new code.
- **SuperAdmin-visible dashboard** — `/hangfire` is already restricted to SuperAdmin (`IDashboardAuthorizationFilter`) and shows failed/retried jobs. A persistently-failing top-up job is visible there after all 5 retries are exhausted.
- **Structured logging** — follow the existing convention (`ILogger<T>` injected, `LogInformation` for the "nothing to do" case, and — new for this job — `LogWarning`/`LogError` if a series fails to materialize, including the series id) so the failure is diagnosable from logs even between dashboard checks.

This app has no automated alerting today (no email-on-job-failure, no external monitoring) for its existing 7 jobs — introducing one just for this feature would be scope creep beyond what the rest of the job infrastructure does. If silent 12-month-window staleness becomes a real operational risk after shipping, that's a candidate for a small follow-up (e.g. a `LastSuccessfulRun` timestamp surfaced on an admin page) applied to all jobs uniformly, not a bespoke mechanism for Events alone.

---

## 4. What NOT to add

| Avoid | Why | Use instead |
|---|---|---|
| `Ical.Net` or any RFC 5545 RRULE library | Cannot express the decided cycle-mask model in a single rule (see §1); would add a dependency, a translation layer, and a parsing/serialization surface for zero benefit since there's no `.ics` interop requirement | The ~40-line custom `EventOccurrenceGenerator` above |
| An external scheduler (Quartz.NET, Azure Functions Timer, cron on the host, etc.) | Violates the no-framework-changes constraint; Hangfire is already the app's one job runner with SQL Server storage, dashboard, and retry policy already wired up | The existing `RecurringJob.AddOrUpdate` convention, identical to the 7 jobs already in `Jobs/` |
| A client-side JS calendar library (FullCalendar, tui-calendar, etc.) | The existing `_Calendar.cshtml`/`_Calendar.Mobile.cshtml` partial + `CalendarViewModel.GetCalendarDays()` is a working, deeply-integrated month-grid renderer — it's not a generic calendar widget, it's fused with this app's own vote-button/signup/finalized-quest rendering logic (see below). Swapping to a JS library would mean re-implementing all of that server-rendered interaction client-side in JS, a much larger and riskier change than the Events feature itself calls for | Extend `_Calendar.cshtml` with an `EventsOnDay` list rendered alongside `QuestsOnDay`, following the exact same per-day loop structure |
| A full "calendaring" subsystem/package (e.g. a generic .NET scheduling/calendar NuGet package beyond Ical.Net) | No such package would understand this app's cadence+mask+anchor model any better than the custom generator; general-purpose calendar packages target RRULE-shaped recurrence, which this feature explicitly isn't | Custom generator + plain EF entities |
| `DateTimeOffset` for occurrence dates/anchor | Wrong semantic type for "a calendar date," forces a spurious UTC-offset decision on data that doesn't have one, and would introduce a second timezone-representation strategy alongside the existing server-local `Quest.FinalizedDate` | `DateOnly` (dates) / `TimeOnly?` (optional time) |
| Hangfire.Idempotent or similar dedup-attribute packages | Solves "don't double-enqueue," which isn't this job's actual risk (Hangfire's scheduler already guarantees one trigger execution); the real risk (duplicate `Event` rows across runs) is a domain-data problem | DB unique index `(EventSeriesId, OccurrenceDate)` + watermark check in job logic |

### Does the hand-rolled calendar stay?

**Yes — confirmed by reading `_Calendar.cshtml` directly, not just asserted.** The partial isn't a generic month grid; it's tightly fused with quest-specific rendering: vote-button radio groups bound to `DateVotes[i].Vote` for the signup form, a details-page/main-page dual mode (`isDetailsPage`), key-availability warning icons, finalized/proposed status badges, and per-user vote-indicator icons — all server-rendered from `CalendarViewModel.GetCalendarDays()`. Adding Events means adding a parallel `EventsOnDay` collection to `CalendarDay` and a corresponding rendering block in the same `@foreach` loop the Quest rendering already uses (informational-only per the spec — no vote-button wiring needed for Events themselves, since Event RSVP via `VoteType` is a separate signup UI, not calendar-cell inline voting like Quests). This is a natural, additive extension of the existing pattern, not a rewrite. Replacing it with a JS library would throw away this proven, tested, deeply-integrated rendering logic for no stated benefit — there is no requirement here (drag-and-drop editing, external calendar sync, etc.) that a hand-rolled server-rendered grid can't satisfy.

## Version Compatibility

| Package | Compatible With | Notes |
|---|---|---|
| Hangfire.Core/AspNetCore/NetCore 1.8.24 | .NET 10, SQL Server storage (already configured) | In-range patch bump from whatever 1.8.x is currently pinned; verify via `dotnet list package` before bumping, no code changes expected |
| EF Core 10 SQL Server provider | `DateOnly`/`TimeOnly` native support | No additional package; do not add `EFCore.SqlServer.DateOnlyTimeOnly` (that's an EF Core 6/7-only shim, unneeded and likely incompatible packaging assumptions against EF Core 10) |

## Sources

- [NuGet Gallery — Ical.Net](https://www.nuget.org/packages/Ical.Net) — version 5.2.3, MIT license, publish date, download counts (fetched directly) — MEDIUM confidence
- [RFC 5545 §3.3.10 Recurrence Rule (icalendar.org)](https://icalendar.org/iCalendar-RFC-5545/3-3-10-recurrence-rule.html) — BYSETPOS/INTERVAL semantics — MEDIUM confidence
- [Microsoft Learn — TimeZoneInfo.FindSystemTimeZoneById (net-10.0)](https://learn.microsoft.com/en-us/dotnet/api/system.timezoneinfo.findsystemtimezonebyid?view=net-10.0) — IANA vs Windows id behavior on Linux, cached-instance behavior since .NET 8 — MEDIUM confidence, official docs
- Web search: EF Core DateOnly/TimeOnly → SQL Server `date`/`time` mapping since EF Core 8 — MEDIUM confidence, cross-checked across multiple independent sources (Microsoft breaking-changes doc, ErikEJ blog, code-maze)
- Web search: Hangfire.Core/AspNetCore/NetCore latest version 1.8.24, published 2026-07-16 — MEDIUM confidence
- `C:\Repos\quest-board\QuestBoard.Service\Program.cs` (lines ~260, ~355) — `AutomaticRetryAttribute` global filter, `RecurringJob.AddOrUpdate` registration convention — HIGH confidence, read directly from source
- `C:\Repos\quest-board\QuestBoard.Service\Jobs\DailyReminderJob.cs`, `HangfireJobHelper.cs` — existing job constructor-injection and scoped-service pattern — HIGH confidence, read directly from source
- `C:\Repos\quest-board\QuestBoard.Service\Views\Shared\_Calendar.cshtml` — confirms hand-rolled calendar is deeply fused with quest vote/signup rendering, not a generic grid — HIGH confidence, read directly from source
- `.planning/PROJECT.md` (Known issues / tech debt, Constraints) — existing `FinalizedDate` server-local storage decision and its documented fragility — HIGH confidence, project's own source of truth

---
*Stack research for: Calendar Events feature (v9.0 milestone)*
*Researched: 2026-08-25*
