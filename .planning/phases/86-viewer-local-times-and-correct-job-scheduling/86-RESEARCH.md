# Phase 86: Viewer-Local Times and Correct Job Scheduling - Research

**Researched:** 2026-09-20
**Domain:** ASP.NET Core 10 MVC server-side time handling, Hangfire recurring job scheduling, client-side JS date formatting
**Confidence:** HIGH

## Summary

This phase has two mechanically linked halves — a display fix and a scheduling fix — and both turn on the same classification work: deciding which `DateTime` on each entity is a real UTC instant and which is naive wall-clock the DM typed directly. That classification is already locked in 86-CONTEXT.md and this research largely **confirms it against the actual view code, line by line**, with two material corrections the planner needs (see Open Questions): `SignupTime` *is* rendered in several desktop views despite CONTEXT.md's claim that it "needs nothing," and two render sites (`Admin/EmailStats.cshtml`'s `AsOf`, `Areas/Platform/Views/Group`'s `CreatedAt`) are real instants CONTEXT.md's classification table never named.

The scheduling half is now precisely scoped by verified evidence: Hangfire 1.8.23's `TimeZoneInfo` overloads on `RecurringJob.AddOrUpdate` are `[Obsolete]` — the current, non-obsolete way to pin a cron job to a zone is the `RecurringJobOptions { TimeZone = ... }` overload, confirmed against the actual v1.8.23 source. The container's base image (`mcr.microsoft.com/dotnet/aspnet:10.0`, no `-alpine` suffix) is Debian-based, and Debian-based .NET images ship `tzdata`, so `TimeZoneInfo.FindSystemTimeZoneById("Europe/Amsterdam")` should resolve without any Dockerfile change — meaning D-04's UTC-fallback path is a genuine safety net rather than something that will fire on every boot.

The rendering half's idiomatic seam already exists in this codebase: an `internal static class HtmlHelperExtensions` in `QuestBoard.Service/Extensions/HtmlHelperExtensions.cs` with one method (`Html.Markdown(...)`) that resolves a service from `RequestServices` and returns `IHtmlContent`. No `TagHelper` exists anywhere in the solution — the time-rendering seam should follow the `Html.Markdown` shape, not introduce a new pattern. `site.js` is the one shared, un-bundled script loaded identically by both layouts (`_Layout.cshtml:256`, `_Layout.Mobile.cshtml:217`) and is the natural home for the `Intl.DateTimeFormat` pass.

**Primary recommendation:** Add one `IHtmlHelper` extension (e.g. `Html.LocalTime(DateTime utcInstant, string boardZoneFormat)`) that emits `<time datetime="...Z" title="...">{server-rendered board-zone value}</time>`, one vanilla-JS pass in `site.js` that walks `time[datetime]` elements on `DOMContentLoaded` and rewrites their text via `Intl.DateTimeFormat` against the viewer's resolved zone, one new `TimeZoneOptions`-shaped class following the `CalendarFeedOptions` idiom exactly, and pin all three Hangfire registrations plus the ambient `DateTime.Today`/`Now` reads in `EventSeriesService`, `GroupRepository`, `CalendarController`, `EventsController`, and `SeriesController` to that same resolved `TimeZoneInfo` through one injectable seam.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Viewer-zone time display | Browser / Client | Frontend Server (SSR) | `Intl.DateTimeFormat` runs in the browser (D-05); the server only provides the machine-readable instant and a board-zone fallback string for first paint / no-JS |
| Board-zone resolution & validation | API / Backend | — | A single `IOptions<T>`-bound `TimeZoneInfo`, resolved once at startup, consumed by both the cron registrations and the ambient service reads (D-03, D-07) |
| Recurring job scheduling | API / Backend | — | Hangfire's `RecurringJobOptions.TimeZone` is a backend-only concern; no client or DB tier involved |
| Degraded-zone visibility | API / Backend | — | ASP.NET Core `IHealthCheck` registered against `/health`, already polled by `docker-compose.yml`'s container healthcheck (D-04) |
| Wall-clock game-night values (never converted) | Database / Storage | — | `EventEntity.Date`/`StartTime`, `ProposedDateEntity.Date`, `QuestEntity.FinalizedDate`, `ShopItemEntity.AvailableFrom/Until` pass through every tier unmodified; the type carries no timezone, by design (Out of Scope: `DateTimeOffset` — see v9 REQUIREMENTS.md) |
| Calendar feed serialization | API / Backend | — | `CalendarSubscriptionService.GetFeedAsync` (Domain) splits `FinalizedDate` into floating `DateOnly`/`TimeOnly`; `CalendarFeedWriter` (Domain) serializes it with no zone designator. Both must stay provably untouched by this phase |

## User Constraints (from CONTEXT.md)

<user_constraints>

### Locked Decisions

- **The classification table** (86-CONTEXT.md `<decisions>`): `CreatedAt` (9 named entities), `UpdatedAt` (ContactNote, EventSignup), `CancelledAt`, `RevokedAt`, `LastFetchedAt`, `SentAt` (ReminderLog), `SignupTime`, `LastVoteChangeTime`, `TransactionDate`, `ListedDate`, `DeniedAt`, `ClosedDate` are real instants and convert. `QuestEntity.FinalizedDate`, `QuestEntity.FinalizedEmailSentForDate`, `ProposedDateEntity.Date`, `EventEntity.Date`+`StartTime`, `ShopItemEntity.AvailableFrom`/`AvailableUntil` are wall-clock and never convert.
- **D-01:** The mixed-zone page is accepted as-is; game-night rendering is out of scope entirely. No zone label on wall-clock values.
- **D-02:** A converted instant carries the source instant in a `title` attribute.
- **D-03:** The sweep timezone is configurable with a `Europe/Amsterdam` code default, following the `CalendarFeedOptions` idiom. All three Hangfire registrations and `DailyReminderJob`'s own "today" read the same zone.
- **D-04:** An unresolvable zone falls back to UTC with a logged warning; the application still boots. A named health check reports `Degraded` (still HTTP 200) when the fallback is active.
- **D-05:** The server renders the instant in the configured board zone; the browser then swaps it to the viewer's zone. A no-JS reader gets board time.
- **D-06:** Every rendered instant converts, including date-only renders — the rule is a property of the value, not the format string.
- **D-07:** Every ambient clock read compared against a board-local date moves onto one board clock: `EventSeriesService` (7 sites), `CalendarController`, `EventsController`, `SeriesController`, `GroupRepository`, `DailyReminderJob`. `EmailPreviewController`'s five `DateTime.Today` uses are cosmetic sample data and need no clock.
- Relative rendering ("2 hours ago") is declined.
- Emails need no change — all four transactional templates render only a wall-clock quest date with `"dddd, MMMM d"`, no time component.

### Claude's Discretion

- The shape of the board-clock seam (a `TimeProvider` wrapper, a dedicated interface, an options-backed service) — the constraint is one seam serving both the cron registrations and the ambient reads so they cannot diverge.
- The shape of the client-side formatting pass (script location, element marking, `Intl` options), subject to D-05's first-paint behaviour and D-02's `title` attribute.
- Which existing options class carries the zone, or whether a new one is minted.

### Deferred Ideas (OUT OF SCOPE)

- Labelling game-night times with an explicit board-zone marker — revisit if a player relocates.
- Relative rendering ("2 hours ago").
- Expressing the instant-versus-wall-clock distinction in the type system (distinct types or `DateTimeOffset`) — its own future phase.
- Per-user timezone preference — not wanted; the browser's resolved zone is the whole mechanism.

</user_constraints>

## Standard Stack

### Core

| Component | Version | Purpose | Why Standard |
|-----------|---------|---------|---------------|
| `Hangfire.Core` / `Hangfire.AspNetCore` / `Hangfire.SqlServer` | 1.8.23 (pinned) `[VERIFIED: QuestBoard.Service/QuestBoard.Service.csproj]` — `<PackageReference Include="Hangfire.AspNetCore" Version="1.8.23" />` | Recurring sweep scheduling | Already the app's job runner; this phase only changes the timezone argument passed to existing registrations |
| `System.TimeZoneInfo` (BCL) | .NET 10 (pinned; `dotnet --version` on this host reports `10.0.112` `[VERIFIED: local shell]`) | Board zone resolution (`TimeZoneInfo.FindSystemTimeZoneById`) | No IANA parsing library needed — the BCL resolves IANA ids (`Europe/Amsterdam`) natively on Linux via the OS's `tzdata` since .NET Core 2.0+ |
| `Intl.DateTimeFormat` (browser native) | N/A — ships with every evergreen browser | Client-side conversion to the viewer's resolved zone | Zero-dependency; `Intl.DateTimeFormat().resolvedOptions().timeZone` is the standard way to read the browser's own zone with no cookie or server round-trip |
| `Microsoft.Extensions.Diagnostics.HealthChecks.IHealthCheck` (BCL, part of `Microsoft.AspNetCore.App`) | Framework-shipped | D-04's Degraded check | `builder.Services.AddHealthChecks()` is already registered bare at `Program.cs:41` `[VERIFIED: QuestBoard.Service/Program.cs:41]` — `builder.Services.AddHealthChecks();` — adding `.AddCheck(...)` needs no new package |

**This phase installs zero new NuGet or npm packages.** Every mechanism above is either already a project dependency (Hangfire) or ships in the BCL/browser. See Package Legitimacy Audit below.

### Supporting

| Component | Purpose | When to Use |
|-----------|---------|-------------|
| `IHtmlHelper` extension method (existing pattern) | Emit the `<time>` element with server-rendered fallback text and `title` attribute | Every Razor view that currently calls `.ToString(...)` on a real-instant property |
| `RecurringJobOptions` (Hangfire, non-obsolete) | Pass a `TimeZoneInfo` to `RecurringJob.AddOrUpdate` | All three registrations in `Program.cs` |

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| `RecurringJobOptions { TimeZone = ... }` | The `TimeZoneInfo` positional-parameter overloads | Those overloads are `[Obsolete]` in Hangfire 1.8.23 `[VERIFIED: HangfireIO/Hangfire GitHub source, tag v1.8.23]` — using them compiles (obsolete is a warning, not an error; no `TreatWarningsAsErrors` found in `QuestBoard.Service.csproj` `[VERIFIED: QuestBoard.Service/QuestBoard.Service.csproj]`) but is the wrong idiom going forward |
| `IHtmlHelper` extension (`Html.LocalTime(...)`) | A `TagHelper` (`<time asp-utc="...">`) | No `TagHelper` exists anywhere in this solution (`find . -iname "*TagHelper*.cs"` returned nothing `[VERIFIED: repo search]`); `Html.Markdown` is the only precedent and it is an `IHtmlHelper` extension, not a TagHelper — matching the existing idiom costs less review friction than introducing a new one |
| A shipped NuGet timezone-conversion helper (e.g. `TimeZoneConverter`, `NodaTime`) | Raw `TimeZoneInfo.ConvertTimeFromUtc` | The BCL's own IANA-id resolution (`FindSystemTimeZoneById("Europe/Amsterdam")`) works natively on Linux/.NET Core; no Windows-name mapping is needed since the container never runs on Windows and the ID is IANA-shaped already |

**Installation:** None — no `npm install` or `dotnet add package` needed for this phase.

**Version verification:** `Hangfire.AspNetCore`/`Hangfire.SqlServer` confirmed pinned to `1.8.23` in the checked-in `.csproj` (`[VERIFIED: QuestBoard.Service/QuestBoard.Service.csproj]`); no registry lookup needed since no new package is added. `dotnet --version` on this development host reports `10.0.112` (`[VERIFIED: local shell]`), consistent with the `net10.0` TFM already in use.

## Package Legitimacy Audit

**N/A — this phase adds no new NuGet or npm packages.** Every mechanism (Hangfire's `RecurringJobOptions`, `System.TimeZoneInfo`, ASP.NET Core's `IHealthCheck`, the browser's `Intl.DateTimeFormat`) is already a project dependency or ships in the BCL/browser runtime. The existing test suite already avoids adding a testing-time-provider package for the same reason — `QuestBoard.UnitTests/Services/EventsOverviewAggregationTests.cs:22` hand-rolls a `FixedTimeProvider : TimeProvider` with the comment `[VERIFIED: QuestBoard.UnitTests/Services/EventsOverviewAggregationTests.cs:20-21]` — "Hand-written rather than a testing-time-provider package, so the fixed clock costs no new dependency: this phase's package legitimacy position is that it installs nothing." The planner should follow the same pattern for any new fixture needed here.

## Architecture Patterns

### System Architecture Diagram

```
Startup (Program.cs)
  │
  ├─► services.AddOptions<TimeZoneOptions>()      (new — CalendarFeedOptions idiom)
  │     .BindConfiguration(...)
  │     .Validate(o => o.IsValid())
  │     .ValidateOnStart()
  │
  ├─► BoardClock seam resolves TimeZoneInfo         (new — shared by cron + ambient reads)
  │     ├─ FindSystemTimeZoneById(configured id) succeeds → BoardZone = resolved
  │     └─ throws TimeZoneNotFoundException          → BoardZone = Utc, log warning,
  │                                                     flip a shared "degraded" flag
  │
  ├─► AddHealthChecks().AddCheck("board-timezone", …) reads the shared degraded flag
  │     → GET /health returns Degraded (still 200) when the fallback is active
  │     (docker-compose.yml's `curl -f http://localhost:8080/health` already polls this)
  │
  └─► RecurringJob.AddOrUpdate<T>(id, expr, cron, new RecurringJobOptions { TimeZone = BoardZone })
        × 3 registrations (daily-session-reminders, recurring-occurrence-top-up,
          calendar-subscription-retention)

Ambient "today" reads (ordinary request path, not startup)
  EventSeriesService / GroupRepository / CalendarController / EventsController /
  SeriesController / DailyReminderJob
      DateTime.Today  ──replaced by──►  BoardClock.Today (DateOnly, board-zone)
      DateTime.Now    ──replaced by──►  BoardClock.Now   (DateTime, board-zone)

Request → Razor View render (server, per HTTP request)
  Model.CreatedAt (UTC DateTime)
      │
      ▼
  Html.LocalTime(Model.CreatedAt, format)   ← new IHtmlHelper extension
      │  resolves BoardClock from RequestServices (same pattern as Html.Markdown)
      ▼
  <time datetime="2026-09-20T09:45:00Z"
        title="2026-09-20T09:45:00Z"
        data-fmt="MMM d, yyyy h:mm tt">Sep 20, 2026 11:45 AM</time>
      (text content = board-zone formatted string — the D-05 first-paint value)
      │
      ▼  ships to browser
Browser (site.js, DOMContentLoaded)
  document.querySelectorAll('time[datetime]').forEach(el => {
      el.textContent = new Intl.DateTimeFormat(undefined, { …options… })
                          .format(new Date(el.getAttribute('datetime')));
  });
      → viewer's Intl-resolved zone replaces the board-zone text with no flash
        for a board-zone reader (values are identical) and a swap for anyone else.
        No-JS reader keeps the server-rendered board-zone value (D-05).

CalendarSubscriptionService.GetFeedAsync (Domain) — MUST NOT be touched by any of the above
  q.FinalizedDate (wall-clock, board-local by construction)
      → Date = DateOnly.FromDateTime(q.FinalizedDate.Value)
      → StartTime = TimeOnly.FromDateTime(q.FinalizedDate.Value)
      → CalendarFeedWriter.FormatBasicDateTime(...) writes "DTSTART:" with no Z, no TZID
```

### Recommended Project Structure

No new folders. Touched/added files:

```
QuestBoard.Domain/
├── Models/
│   └── TimeZoneOptions.cs                  # new — CalendarFeedOptions-shaped options class
├── Interfaces/
│   └── IBoardClock.cs                      # new — the one seam D-03/D-07 both consume
├── Services/
│   └── BoardClock.cs                       # new — wraps TimeProvider + resolved TimeZoneInfo
│   └── EventSeriesService.cs               # touched — DateTime.Today → IBoardClock
QuestBoard.Repository/
│   └── GroupRepository.cs                  # touched — DateTime.Today → IBoardClock
QuestBoard.Service/
├── Extensions/
│   └── HtmlHelperExtensions.cs             # touched — add Html.LocalTime(...)
├── HealthChecks/
│   └── BoardTimeZoneHealthCheck.cs         # new — D-04's Degraded check
├── Jobs/
│   └── DailyReminderJob.cs                 # touched — DateTime.Today → IBoardClock, fix comment
├── Controllers/
│   ├── QuestBoard/CalendarController.cs    # touched — DateTime.Now → IBoardClock
│   ├── Events/EventsController.cs          # touched — DateTime.Today → IBoardClock
│   └── Events/SeriesController.cs          # touched — DateTime.Today → IBoardClock
├── Program.cs                              # touched — 3 RecurringJob registrations + health check
├── Views/**/*.cshtml + *.Mobile.cshtml     # touched — every real-instant render site (see inventory)
└── wwwroot/js/site.js                      # touched — Intl.DateTimeFormat pass
```

### Pattern 1: `IHtmlHelper` extension for real-instant rendering

**What:** A single extension method that takes the UTC `DateTime`, formats it server-side in the board zone, and emits a `<time>` element carrying the raw instant.
**When to use:** Every render site in the "real instant" column of the classification table.
**Example — the existing precedent this should match:**
```csharp
// Source: QuestBoard.Service/Extensions/HtmlHelperExtensions.cs (verified this session)
internal static IHtmlContent Markdown(this IHtmlHelper html, string? markdown)
{
    var service = html.ViewContext.HttpContext.RequestServices.GetRequiredService<IMarkdownService>();
    var rendered = service.RenderToHtml(markdown, MarkdownRenderTarget.Web);
    return new HtmlString($"<div class=\"markdown-content\">{rendered}</div>");
}
```
A `LocalTime` extension should resolve `IBoardClock` the same way (`RequestServices.GetRequiredService<IBoardClock>()`), format with the board `TimeZoneInfo`, and build the element with `TagBuilder` (not raw string interpolation) so the ISO-8601 `datetime` attribute and the formatted display text cannot break attribute quoting.

### Pattern 2: Non-obsolete Hangfire timezone registration

**What:** Pin a `RecurringJob.AddOrUpdate<T>` call to a specific zone via the current (non-`[Obsolete]`) API.
**When to use:** All three registrations in `Program.cs:372,380,389`.
**Example — confirmed against Hangfire v1.8.23 source (`RecurringJob.cs`, `RecurringJobOptions.cs`):**
```csharp
// RecurringJobOptions.TimeZone : TimeZoneInfo, default TimeZoneInfo.Utc — verified via
// raw.githubusercontent.com/HangfireIO/Hangfire/v1.8.23/src/Hangfire.Core/RecurringJobOptions.cs
RecurringJob.AddOrUpdate<DailyReminderJob>(
    "daily-session-reminders",
    job => job.ExecuteAsync(CancellationToken.None),
    "0 9 * * *",
    new RecurringJobOptions { TimeZone = boardClock.TimeZone });
```
The overload `AddOrUpdate<T>(string, Expression<Action<T>>, string, TimeZoneInfo, string queue)` still compiles but is marked `[Obsolete]` in this exact version — do not use it even though it is shorter.

### Pattern 3: Options class following `CalendarFeedOptions`

**What:** Code-default, config-overridable options class with a `SectionName` constant and `IsValid()`.
**Example — verified in full this session:**
```csharp
// Source: QuestBoard.Domain/Models/CalendarFeedOptions.cs (read in full this session)
public class CalendarFeedOptions
{
    public const string SectionName = "CalendarFeed";
    public int MonthsBack { get; set; } = 3;
    // ...
    public bool IsValid() => MonthsBack >= 0 && MonthsAhead >= 1 && LastFetchedThrottleMinutes >= 1
        && RetentionDays >= 1 && QuestDurationHours >= 1;
}
```
Registration idiom, verified in full:
```csharp
// Source: QuestBoard.Domain/Extensions/ServiceExtensions.cs:36-39 (read in full this session)
services.AddOptions<CalendarFeedOptions>()
    .BindConfiguration(CalendarFeedOptions.SectionName)
    .Validate(o => o.IsValid(), "CalendarFeed MonthsBack must be at least 0, and MonthsAhead, "
        + "LastFetchedThrottleMinutes, RetentionDays and QuestDurationHours must each be at least 1.")
    .ValidateOnStart();
```
A `TimeZoneOptions` class (name at planner's discretion) should follow this exactly, with `IsValid()` deliberately *not* attempting `FindSystemTimeZoneById` (that call can throw; `IsValid()` here should only check the string is non-empty — the actual resolution-with-fallback happens in the `IBoardClock` seam per D-04, not at `ValidateOnStart()` which would `throw` and defeat D-04's "still boots" requirement).

### Anti-Patterns to Avoid

- **Calling `TimeZoneInfo.FindSystemTimeZoneById` inside `.ValidateOnStart()`:** `ValidateOnStart()` throws on failure and aborts startup — the opposite of D-04's "an unresolvable zone falls back to UTC... the application still boots." Resolution-with-fallback must happen in the `IBoardClock` seam's own construction path, not in options validation.
- **Using the `TimeZoneInfo`-overload of `RecurringJob.AddOrUpdate`:** works, but is `[Obsolete]` in the pinned Hangfire version.
- **Setting the container `TZ` environment variable:** explicitly rejected by CONTEXT.md — it would move `DateTime.Today` to local while any *unfixed* ambient call site stayed on a UTC assumption, and it does nothing for Hangfire's own `TimeZoneInfo.Utc` default.
- **Converting via `DateTime.ToLocalTime()`:** relies on the *process's* local zone (which is UTC in the container and irrelevant even if it weren't) — never the right call for either the board zone or the viewer zone. Always resolve a named `TimeZoneInfo` (board) or leave the value untouched for the browser to convert (viewer).

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Cron-to-zone alignment | A fixed cron-hour offset (e.g. shift `0 9 * * *` to `0 7 * * *`) | `RecurringJobOptions.TimeZone` | Explicitly rejected in CONTEXT.md — a fixed offset cannot track DST and is wrong for half the year |
| IANA timezone → offset math | Manual UTC-offset arithmetic (`AddHours(2)`) | `TimeZoneInfo.ConvertTimeFromUtc` / `Intl.DateTimeFormat` | DST transitions, historical rule changes, and non-integer offsets (some zones use 30/45-minute offsets) are exactly the class of bug a hand-rolled offset reintroduces |
| Viewer zone detection | Server-side `Accept-Language` or IP-geolocation guessing | Browser's own `Intl.DateTimeFormat().resolvedOptions().timeZone` | Explicitly the locked mechanism (D-05's whole premise) — no stored preference, no header parsing, no error-prone heuristic |
| Windows-timezone-name mapping | A lookup table for `"Central European Standard Time"` ↔ `"Europe/Amsterdam"` | The IANA id directly (`Europe/Amsterdam`) | The container never runs on Windows (per CLAUDE.md, Windows is the *dev* host only; the shipped container is Linux/Debian), so no Windows-name compatibility shim is needed |

**Key insight:** every piece of machinery this phase needs — zone resolution, DST-aware conversion, viewer zone detection, cron scheduling with a zone — already exists in the BCL, Hangfire, or the browser. The entire risk surface is the *classification* (which `DateTime` means what) and *wiring* (one seam feeding both the cron and the ambient reads), not missing tooling.

## Common Pitfalls

### Pitfall 1: The `FinalizedDate ?? ClosedDate` coalesce mixes both categories through one format string

**What goes wrong:** `QuestLog/Details.cshtml:37`, `QuestLog/Details.Mobile.cshtml:38`, `QuestLog/Index.cshtml:23`, and `QuestLog/Index.Mobile.cshtml:50` all render `(quest.FinalizedDate ?? quest.ClosedDate)` through a single `.ToString(...)` call. `FinalizedDate` is wall-clock (never convert); `ClosedDate` is a real instant assigned `DateTime.UtcNow` at `QuestBoard.Repository/QuestRepository.cs:170` (`[VERIFIED: QuestBoard.Repository/QuestRepository.cs:170]` — `entity.ClosedDate = DateTime.UtcNow;`).
**Why it happens:** The coalesce reads naturally as "whichever date applies" without the type distinguishing the two meanings.
**How to avoid:** Split into two branches — render `FinalizedDate` untouched when present, and only pass through `Html.LocalTime(...)` for the `ClosedDate` fallback branch.
**Warning signs:** Any `??` between a wall-clock property and a real-instant property feeding one format call.

### Pitfall 2: `DateTime.UtcNow - purchase.TransactionDate` arithmetic must stay in UTC

**What goes wrong:** `Shop/Index.cshtml:76` computes `DateTime.UtcNow - purchase.TransactionDate`. `TransactionDate` is a real instant and is correctly UTC today. If the *rendering* of `TransactionDate` at lines 105/151 is changed to a converted local `DateTime` and that converted value is accidentally reused for the subtraction, the span becomes wrong by the board's UTC offset.
**Why it happens:** Rendering and arithmetic on the same property are easy to conflate once a "converted" local variable exists in scope.
**How to avoid:** Keep the raw UTC `TransactionDate` for the subtraction; only pass the value through `Html.LocalTime(...)` at the render call site, never assign the converted result back over the original variable.
**Warning signs:** Any arithmetic (`-`, `.CompareTo`, `<`, `>`) on a property that also has a display call site nearby.

### Pitfall 3: Four `datetime-local` form round-trip values must never be touched

**What goes wrong:** `Quest/CreateFollowUp.cshtml:74`, `CreateFollowUp.Mobile.cshtml:79`, `Quest/Edit.cshtml:97`, `Edit.Mobile.cshtml:101` render `ProposedDates` as `ToString("yyyy-MM-ddTHH:mm")` into `datetime-local` inputs and hidden fields. These are wall-clock *and* form values — the browser's `datetime-local` input has no timezone semantics at all; converting these would corrupt the round-trip (a submitted value would land on a shifted date/time).
**Why it happens:** A blanket "convert every rendered instant" sweep (D-06's own wording) could catch these if the classification isn't checked property-by-property rather than format-string-by-format-string.
**How to avoid:** These are `ProposedDateEntity.Date` — already on the never-convert side of the classification table. Confirm the property, not just the presence of a `.ToString(...)` call, before touching a site.
**Warning signs:** `type="datetime-local"` on the input/hidden field consuming the formatted string.

### Pitfall 4: `Series/Details.cshtml` calls `DateTime.Today` directly in the view, outside D-07's named list

**What goes wrong:** `QuestBoard.Service/Views/Series/Details.cshtml:12` (`var todayLabel = DateTime.Today.ToString("MMMM d, yyyy");`) and `:17` (`var today = DateOnly.FromDateTime(DateTime.Today);`) read the ambient clock directly in Razor, not through `SeriesController`. D-07 names `SeriesController` as one of the six files whose clock reads must move onto the board zone, but `SeriesController.cs` itself only has one `DateTime.Today` use (line 68, inside `EndAsync`) — the *view* has two more, independently. `[VERIFIED: QuestBoard.Service/Views/Series/Details.cshtml:12,17]` — quoted above verbatim.
**Why it happens:** D-07's file list was written from the controller/service layer; a Razor view calling `DateTime.Today` directly is a different call site the same audit would need to catch separately.
**How to avoid:** Treat this as a seventh D-07 site: the "Today" divider logic and the confirmation-message wording both need the board-zone `today`, not the container's raw UTC `DateTime.Today`. Passing the resolved `DateOnly` down from `SeriesController` into the view model (rather than the view calling the clock itself) is the more testable fix.
**Warning signs:** `grep DateTime.Today` limited to `.cs` files under `Services`/`Controllers`/`Repository` will miss this; the planner's own verification pass must also grep `.cshtml`.

### Pitfall 5: Hangfire is not registered in the `Testing` environment — the cron registrations cannot be integration-tested as written

**What goes wrong:** `Program.cs`'s three `RecurringJob.AddOrUpdate` calls sit inside `if (!app.Environment.IsEnvironment("Testing"))` (`[VERIFIED: QuestBoard.Service/Program.cs:366-390]`), and `WebApplicationFactoryBase.ConfigureWebHost` (`QuestBoard.IntegrationTests/WebApplicationFactoryBase.cs`) sets `builder.UseEnvironment("Testing")` and registers a spy `IBackgroundJobClient` instead of real Hangfire. A test that spins up the factory and inspects the registered cron/timezone will find nothing registered at all.
**Why it happens:** Hangfire needs a live SQL Server storage backend to register jobs against; the Testing environment deliberately skips it.
**How to avoid:** Test the timezone-resolution seam (`IBoardClock`/options `IsValid()`/fallback behaviour) as a pure unit test with `NSubstitute`/hand-rolled fakes, matching `EventsOverviewAggregationTests`'s `FixedTimeProvider` pattern. Do not attempt to assert the live cron registration through the integration test factory — extract the `TimeZoneInfo` selection into a testable method/seam instead, and assert *that* directly.
**Warning signs:** A plan task that says "integration test verifies the reminder job runs at 09:00 board time" — Hangfire's own execution cannot be observed this way in this test suite as it exists today.

### Pitfall 6: The Group/Contact/Quest `CreatedAt` render sites are asymmetric across layouts by design, not by omission

**What goes wrong:** `Quest/Manage.cshtml:754` and `Quest/Details.cshtml:844` render `CreatedAt`; their mobile twins (`Manage.Mobile.cshtml`, `Details.Mobile.cshtml`) do **not** render it at all — confirmed by an empty grep result on both files this session. `Shop/Index.Mobile.cshtml` and `ShopManagement/Index.Mobile.cshtml` similarly never render `TransactionDate`/`DeniedAt` even though their desktop twins do. `Admin/EmailStats.cshtml` has no mobile twin file at all.
**Why it happens:** These are pre-existing content differences between the layouts, not something this phase introduces.
**How to avoid:** Convert the render sites that exist; do **not** add a new `CreatedAt`/`TransactionDate`/`DeniedAt` render to a mobile view solely to "match" its desktop twin — that would be scope creep beyond a display-and-scheduling fix, and 86-CONTEXT.md's mobile-twin warning is about *shipping a converted value on one layout and leaving the other layout's existing (unconverted) render of the same value behind* — not about achieving new content parity.
**Warning signs:** A task that adds a `Created:` line to a mobile view where none existed before.

## Code Examples

### Board clock seam (discretionary shape, following the `TimeProvider` precedent)

```csharp
// Existing precedent for the injection style — verified this session:
// QuestBoard.Domain/Extensions/ServiceExtensions.cs:16-17
// services.TryAddSingleton(TimeProvider.System);
// "The system clock is registered here so the domain reads time through an injectable
//  seam rather than a static call."
public interface IBoardClock
{
    TimeZoneInfo TimeZone { get; }
    bool IsDegraded { get; }        // true when the configured zone could not be resolved
    DateOnly Today { get; }         // DateOnly.FromDateTime(ConvertFromUtc(utcNow))
    DateTime Now { get; }           // board-zone wall-clock "now"
}
```

### Existing `FixedTimeProvider` test double to mirror for `IBoardClock` fakes

```csharp
// Source: QuestBoard.UnitTests/Services/EventsOverviewAggregationTests.cs:20-25 (verified this session)
// Hand-written rather than a testing-time-provider package, so the fixed clock costs no
// new dependency: this phase's package legitimacy position is that it installs nothing.
private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
```

### The two protected call sites the calendar feed writer must never gain a conversion at

```csharp
// Source: QuestBoard.Domain/Services/CalendarSubscriptionService.cs:142-146 (verified this session)
// FinalizedDate is a single DateTime carrying both date and time, unlike an
// event's already-split pair, so it is split explicitly here. The query
// guarantees FinalizedDate != null for every row that reaches this point.
Date = DateOnly.FromDateTime(q.FinalizedDate!.Value),
StartTime = TimeOnly.FromDateTime(q.FinalizedDate.Value),
```
```csharp
// Source: QuestBoard.Domain/Services/CalendarFeedWriter.cs:78,96,149 (verified this session)
// DTSTART/DTEND for a timed entry are floating local time -- no zone designator --
AppendFoldedLine(builder, "DTSTART:" + FormatBasicDateTime(start));
AppendFoldedLine(builder, "DTSTART;VALUE=DATE:" + FormatBasicDate(entry.Date));
```
Neither of these two files should appear in this phase's diff at all — a code-review check for `CalendarFeedWriter.cs`/`CalendarSubscriptionService.cs` unchanged (or changed only for unrelated reasons) is a cheap regression gate.

## Date-Render Call Site Inventory

**Verified this session via `grep -n` against the actual view files** (not from training knowledge). Grouped by classification; every `.Mobile.cshtml` twin's status is stated explicitly rather than assumed.

### Real instants — must convert (desktop / mobile twin status noted)

| # | File:Line | Property | Format string | Mobile twin renders same property? |
|---|-----------|----------|----------------|--------------------------------------|
| 1 | `Account/Profile.cshtml:108` | `subscription.CreatedAt` | `"MMM d, yyyy"` | Yes — `Profile.Mobile.cshtml:68` |
| 2 | `Account/Profile.cshtml:111` | `subscription.LastFetchedAt` | `"MMM d, yyyy h:mm tt"` | Yes — `Profile.Mobile.cshtml:71` |
| 3 | `QuestLog/Details.cshtml:38` | `Model.Quest.CreatedAt` | `"MMMM dd, yyyy"` | Yes — `Details.Mobile.cshtml:39` |
| 4 | `Contacts/Details.cshtml:150` | `note.CreatedAt` | `"MMM d, yyyy h:mm tt"` | Yes — `Details.Mobile.cshtml:135` |
| 5 | `Quest/Manage.cshtml:754` | `Model.CreatedAt` | `"MMM dd, yyyy"` | **No** — `Manage.Mobile.cshtml` does not render `CreatedAt` at all (verified empty grep) |
| 6 | `Quest/_QuestCard.cshtml:87` | `Model.CreatedAt` | `"MMM dd, yyyy"` | N/A — one shared partial used by both layouts via `_QuestSection.cshtml:26` |
| 7 | `Quest/Details.cshtml:844` | `Model.Quest?.CreatedAt` | `"MMM dd, yyyy"` | **No** — `Details.Mobile.cshtml` does not render `CreatedAt` at all (verified empty grep) |
| 8 | `Shop/Index.cshtml:105` | `purchase.TransactionDate` | `"MMM dd"` | **No** — `Shop/Index.Mobile.cshtml` does not render `TransactionDate` (verified empty grep) |
| 9 | `Shop/Index.cshtml:151` | `purchase.TransactionDate` | `"MMM dd"` (second occurrence, different tab in the same view) | same as above |
| 10 | `ShopManagement/Index.cshtml:148` | `item.DeniedAt` | `"MMM dd, yyyy"` | **No** — `ShopManagement/Index.Mobile.cshtml` does not render `DeniedAt` (verified empty grep) |
| 11 | `Areas/Platform/Views/Group/Index.cshtml:51` | `item.CreatedAt` | `"yyyy-MM-dd"` | Yes — `Index.Mobile.cshtml:42` |
| 12 | `Admin/EmailStats.cshtml:63` | `Model.AsOf` (`= DateTime.UtcNow`, `[VERIFIED: QuestBoard.Service/Controllers/Admin/AdminController.cs:491]`) | `"g"` | N/A — no mobile twin file exists for `EmailStats` at all (verified via directory listing) |
| 13 | `Quest/Manage.cshtml:283` | `player.SignupTime` | `"MMM dd, h:mm tt"` | **No** — `Manage.Mobile.cshtml` uses `SignupTime` only as an `OrderBy` key (see Open Questions — contradicts CONTEXT.md) |
| 14 | `Quest/Manage.cshtml:326` | `assistant.SignupTime` | `"MMM dd, h:mm tt"` | same as above |
| 15 | `Quest/Manage.cshtml:362` | `spectator.SignupTime` | `"MMM dd, h:mm tt"` | same as above |
| 16 | `Quest/Manage.cshtml:515` | `participant.SignupTime` | `"MMM dd, h:mm tt"` | same as above |
| 17 | `Quest/Details.cshtml:886` | `signup.SignupTime` (Players group) | `"MMM dd"` | **No** — `Details.Mobile.cshtml` uses `SignupTime` only as an `OrderBy` key |
| 18 | `Quest/Details.cshtml:907` | `signup.SignupTime` (AssistantDM group) | `"MMM dd"` | same as above |
| 19 | `Quest/Details.cshtml:928` | `signup.SignupTime` (Spectator group) | `"MMM dd"` | same as above |

`ContactNote.UpdatedAt` (`Contacts/Details.cshtml:151`, `Details.Mobile.cshtml:136`) is checked only as `!= null` to append the literal text `" (edited)"` — never rendered as a date, needs no conversion. `RevokedAt`, `ListedDate`, `CancelledAt` have **no render site anywhere in the view tree** today (verified empty greps) — the classification still matters if a future view adds one, but no existing task is needed for them. `ShopItemEntity.CreatedAt` exists (`[VERIFIED: QuestBoard.Repository/Entities/ShopItemEntity.cs:43]` — `public DateTime CreatedAt { get; set; } = DateTime.UtcNow;`) but has zero read/render sites anywhere in the codebase — a tenth `CreatedAt` entity beyond CONTEXT.md's "9 entities" count, dead for display purposes.

### Wall-clock — never convert (confirmed correctly unconverted today)

| File:Line | Property | Note |
|-----------|----------|------|
| `QuestLog/Details.cshtml:37`, `Details.Mobile.cshtml:38`, `Index.cshtml:23`, `Index.Mobile.cshtml:50` | `quest.FinalizedDate ?? quest.ClosedDate` | **Landmine** — mixes a wall-clock and a real-instant property through one format call (see Common Pitfalls #1) |
| `Quest/Details.cshtml:66,77,442`, `Details.Mobile.cshtml:371` | `Model.Quest?.FinalizedDate` | `"dddd, MMMM dd, yyyy 'at' h:mm tt"` — game night, out of scope per D-01 |
| `Quest/Index.cshtml:135`, `Index.Mobile.cshtml:109` | `quest.FinalizedDate.Value` | same format |
| `Quest/_QuestCard.cshtml:48` | `Model.FinalizedDate` | same |
| `Quest/Manage.cshtml:99,179,789`, `Manage.Mobile.cshtml:91,264` | `finalizedDate.Date` / `date.Date` (a `ProposedDate`) | wall-clock |
| `Shared/_Calendar.cshtml:62,70`, `_Calendar.Mobile.cshtml:26,27`, `Calendar/Index.Mobile.cshtml:89,112` | `questOnDay.ProposedDate.Date` / `day.Date` | the calendar grid itself — the canonical "game night," explicitly out of scope |
| `Events/Index.cshtml:66,69`, `Index.Mobile.cshtml:80,83`, `Agenda/Index.cshtml:106,109`, `Agenda/Index.Mobile.cshtml:107,110` | `row.Date` / `row.StartTime` | `EventEntity.Date`/`StartTime` — wall-clock |
| `Events/Details.cshtml:41` | `Model.Date` | wall-clock |
| `Series/Details.cshtml:113` | `occurrence.Date` | wall-clock (occurrence = generated `Event`) |
| `Shared/_ShopItemDetailsContent.cshtml:77,83,89` | `Model.AvailableFrom`/`AvailableUntil` | wall-clock, bound from form fields |
| `ShopManagement/Create.cshtml`, `Create.Mobile.cshtml`, `Edit.cshtml`, `Edit.Mobile.cshtml` (`datetime-local` inputs) | `AvailableFrom`/`AvailableUntil` | form round-trip, never touch |
| `Quest/CreateFollowUp.cshtml:74`, `CreateFollowUp.Mobile.cshtml:79`, `Edit.cshtml:97`, `Edit.Mobile.cshtml:101` | `ProposedDates` `"yyyy-MM-ddTHH:mm"` | **Landmine** — form round-trip, never touch (Common Pitfalls #3) |

**Total distinct format strings observed across all render sites (real-instant + wall-clock): 19**, matching the phase description's count exactly (`ToString("MMM dd, yyyy")` ×10, `"dddd, MMMM dd, yyyy 'at' h:mm tt"` ×8, `"HH:mm"` ×8, `"MMM dd"` ×6, `"MMM dd, h:mm tt"` ×5, `"yyyy-MM-ddTHH:mm"` ×4, `"dddd, MMMM d, yyyy"` ×4, `"ddd, MMM d"` ×4, `"MMM d, yyyy h:mm tt"` ×4, `"N0"` ×3, `"MMMM dd, yyyy"` ×3, `"MMM dd, yyyy 'at' h:mm tt"` ×3, `"dddd, MMMM d"` ×2, `"dddd, MMM dd 'at' h:mm tt"` ×2, `"MMMM dd, yyyy 'at' h:mm tt"` ×2, `"MMM d, yyyy"` ×2, `"yyyy-MM-dd"` ×1, `"g"` ×1, `"MMMM d, yyyy"` ×1 — `"N0"` is a currency/number format unrelated to dates and can be ignored).

## Job Scheduling Call Sites

### The three Hangfire registrations (`Program.cs`, verified this session)

| Registration | Line | Cron | Current effective UTC time | Board-local time the comment claims |
|---|---|---|---|---|
| `daily-session-reminders` | `Program.cs:372` | `"0 9 * * *"` | 09:00 UTC | comment says "09:00 server local time (CET/CEST)" — false; user-visible (reminder emails) |
| `recurring-occurrence-top-up` | `Program.cs:380` | `"0 3 * * *"` | 03:00 UTC | comment says "03:00 server local time" — false; not user-visible, only needs to stay off-peak/distinct |
| `calendar-subscription-retention` | `Program.cs:389` | `"0 4 * * *"` | 04:00 UTC | comment says "04:00 server local time" — false; not user-visible |

All three registrations are inside `if (!app.Environment.IsEnvironment("Testing"))` (`Program.cs:366`) — see Common Pitfalls #5 for the testing implication.

### Ambient `DateTime.Today`/`.Now` call sites that must move onto the board clock (D-07)

| File:Line | Call | In scope per D-07? |
|---|---|---|
| `QuestBoard.Domain/Services/EventSeriesService.cs:21,58,90,172,185,192,229` (7 sites, confirmed by line-by-line read) | `DateOnly.FromDateTime(DateTime.Today)` | Yes — named |
| `QuestBoard.Repository/GroupRepository.cs:74` | `DateOnly.FromDateTime(DateTime.Today)` (campaign auto-signup sweep) | Yes — named |
| `QuestBoard.Service/Controllers/QuestBoard/CalendarController.cs:24` | `DateTime.Now` (calendar month default) | Yes — named |
| `QuestBoard.Service/Controllers/Events/EventsController.cs:95` | `DateOnly.FromDateTime(DateTime.Today)` (Create form default) | Yes — named |
| `QuestBoard.Service/Controllers/Events/SeriesController.cs:68` | `DateOnly.FromDateTime(DateTime.Today)` (`EndAsync`) | Yes — named |
| `QuestBoard.Service/Jobs/DailyReminderJob.cs:18` | `DateTime.Today.AddDays(1)` | Yes — named |
| `QuestBoard.Service/Views/Series/Details.cshtml:12,17` | `DateTime.Today` (view-level, not controller) | **Not named in D-07's file list — see Common Pitfalls #4** |
| `QuestBoard.Service/Controllers/Admin/EmailPreviewController.cs:70,88,89,157,175` (5 sites) | `DateTime.Today.AddDays(N)` | **Explicitly excluded** — CONTEXT.md: "cosmetic; they need no clock" (admin preview sample data) |
| `RecurringOccurrenceTopUpJob.cs` | none directly — calls `IEventSeriesService.GetActiveSeriesForActiveGroupAsync()` (`[VERIFIED: QuestBoard.Service/Jobs/RecurringOccurrenceTopUpJob.cs:36]`) | Fixed transitively once `EventSeriesService` is fixed — no separate change needed in the job itself |

### The exact non-obsolete Hangfire API (verified against v1.8.23 source)

`RecurringJob.AddOrUpdate<T>(string recurringJobId, Expression<Action<T>> methodCall, string cronExpression)` has two non-obsolete siblings: the 3-arg form above (implicit `TimeZoneInfo.Utc`) and `AddOrUpdate<T>(string recurringJobId, Expression<Action<T>> methodCall, string cronExpression, RecurringJobOptions options)`. `RecurringJobOptions.TimeZone` is `TimeZoneInfo`, defaulting to `TimeZoneInfo.Utc`. All `TimeZoneInfo`-positional-parameter overloads (both generic and non-generic, across `Expression<Action>`, `Expression<Action<T>>`, and `Task`-returning variants) carry `[Obsolete]` in this exact pinned version. `[VERIFIED: raw.githubusercontent.com/HangfireIO/Hangfire/v1.8.23/src/Hangfire.Core/RecurringJob.cs and RecurringJobOptions.cs, fetched this session]`

## Common Pitfalls (continued — see numbered list above for #1-#6)

### Pitfall 7: The container's IANA tzdata resolution is `[CITED]`, not independently proven on the actual production container

**What goes wrong:** D-04's whole fallback design assumes `TimeZoneInfo.FindSystemTimeZoneById("Europe/Amsterdam")` *can* fail (hence the UTC fallback + health check). Research (WebSearch, cross-referenced against community GitHub issues/blog posts, not official Microsoft documentation) indicates Debian-based `mcr.microsoft.com/dotnet/aspnet` images (which this project's `Dockerfile` uses — `FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base`, no `-alpine` suffix, `[VERIFIED: Dockerfile]`) ship `tzdata` by default, unlike Alpine-based tags. This means the fallback path likely never fires in the actual deployed container — but this is `[CITED]` from third-party sources (Steve Gordon's blog, `dotnet/dotnet-docker` GitHub issues), not verified by running the actual production image.
**Why it matters:** If tzdata resolution "just works," D-04's operator-chosen availability-over-strictness decision becomes a belt-and-suspenders safety net for a hypothetical future base-image change (e.g. a switch to Alpine for size) rather than a live risk today. This doesn't change the plan (D-04's mitigation is mandatory regardless), but it does mean the plan should include a real verification step — running `TimeZoneInfo.FindSystemTimeZoneById("Europe/Amsterdam")` inside the actual built container image — rather than assuming success.
**How to avoid:** Add a Wave-0 or early-task manual check: `docker run --rm <built-image> dotnet-exec-snippet` (or simply observe the health check reporting Healthy, not Degraded, on first deploy) to confirm zone resolution succeeds in the real image, not just in this Linux dev host (which already has `tzdata` installed system-wide — confirmed via `/usr/share/zoneinfo/Europe/Amsterdam` existing and `timedatectl` reporting `Europe/Amsterdam (CEST, +0200)` on this dev machine — that dev-host fact says nothing about the container's own filesystem).
**Warning signs:** A plan that treats the fallback path as untestable/unreachable and skips writing a unit test that forces an invalid zone id to exercise it.

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|-------------------|---------------|--------|
| `RecurringJob.AddOrUpdate(..., TimeZoneInfo, queue)` | `RecurringJob.AddOrUpdate(..., RecurringJobOptions)` | Hangfire 1.8.0 (per the blog title `hangfire.io/blog/2023/04/28/hangfire-1.8.0.html` returned by search; not fetched directly this session — `[CITED]`) | The `TimeZoneInfo`-positional overloads still compile in 1.8.23 but are marked `[Obsolete]`; new code should use `RecurringJobOptions` |

**Deprecated/outdated:** The comments at `Program.cs:371,381,386` and `DailyReminderJob.cs:16-17` asserting "server local time (CET/CEST)" are factually wrong for the shipped deployment (no `TZ` set, no `/etc/localtime` mount, Hangfire defaults to UTC) and must be corrected as part of this phase, per CLAUDE.md's rule against writing comments that go stale — these should describe the *actual* mechanism (a configured board zone) rather than an assumption about the container.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | Debian-based `mcr.microsoft.com/dotnet/aspnet:10.0` ships `tzdata` sufficient for `TimeZoneInfo.FindSystemTimeZoneById("Europe/Amsterdam")` to succeed without a Dockerfile change | Pitfall 7, Summary | If wrong, D-04's UTC fallback fires on every boot in production and the Degraded health check becomes permanently active — still correct behaviour per D-04, but a support annoyance the team should know about immediately rather than discover later. Verify against the actual built image in an early task. |
| A2 | Hangfire 1.8.0 is the version that deprecated the `TimeZoneInfo` positional overload in favour of `RecurringJobOptions` | State of the Art | Low risk — the version number is from a WebSearch title, not independently confirmed by reading the changelog; the `[Obsolete]` attribute itself was directly confirmed against v1.8.23 source, so the *current* API surface is solid regardless of exactly which version introduced the change |

**Two claims from 86-CONTEXT.md were found to be incorrect against the actual code and are corrected in Open Questions below, not listed here as this research's own assumptions** — they are contradictions in the upstream decision document, not assumptions this research introduced.

## Open Questions

1. **CONTEXT.md states "`SignupTime` needs nothing... appears in the views only as an `OrderBy` key, never rendered" — this is contradicted by the code.**
   - What we know: `Quest/Manage.cshtml:283,326,362,515` render `player.SignupTime.ToString("MMM dd, h:mm tt")` (and the assistant/spectator/participant equivalents), and `Quest/Details.cshtml:886,907,928` render `signup.SignupTime.ToString("MMM dd")` — six render sites total, all on desktop only (their `.Mobile.cshtml` twins use `SignupTime` solely as an `OrderBy` key, confirmed by grep). `[VERIFIED: QuestBoard.Service/Views/Quest/Manage.cshtml:283,326,362,515 and Details.cshtml:886,907,928]`
   - What's unclear: Whether the discuss-phase session simply missed these desktop-only sites, or deliberately scoped `SignupTime` out for a reason not recorded in CONTEXT.md/the discussion log.
   - Recommendation: The planner should treat `SignupTime` as a real instant requiring conversion at these six sites (it is unambiguously in the "real instants" classification table already — CONTEXT.md lists `SignupTime` there), and flag this correction explicitly in the plan so the discrepancy is visible rather than silently resolved either way.

2. **`Admin/EmailStats.cshtml`'s `Model.AsOf` and `Areas/Platform/Views/Group`'s `CreatedAt` are real instants not named in CONTEXT.md's classification table or ROADMAP.md's scope notes.**
   - What we know: `AsOf = DateTime.UtcNow` (`AdminController.cs:491`) rendered raw via `.ToString("g")` at `EmailStats.cshtml:63`; `GroupEntity.CreatedAt` rendered raw at `Areas/Platform/Views/Group/Index.cshtml:51` and `Index.Mobile.cshtml:42`. Both are admin/SuperAdmin-only surfaces.
   - What's unclear: Whether these were omitted from CONTEXT.md because the discuss-phase session scoped to player-facing surfaces only, or simply not found during that session's audit.
   - Recommendation: Per D-06 ("the rule is a property of the value, not the format string" — and by extension, not the page's audience), these should convert too. Low risk either way since they are low-traffic admin pages, but the plan should make an explicit decision rather than silently including or excluding them.

3. **`Series/Details.cshtml`'s two `DateTime.Today` calls are a view-level ambient read D-07's file list doesn't name.**
   - What we know: Confirmed by direct read — see Common Pitfalls #4.
   - What's unclear: Whether fixing this is required for D-07's stated goal ("no genuine off-by-one" for date comparisons) since this specific read only affects a confirmation-dialog wording and a "Today" divider position, not a stored value or an email.
   - Recommendation: Include it — the same midnight-to-02:00 window bug D-07 exists to fix applies here identically (the "Today" divider would sit one row off from where `SeriesController`'s own board-corrected `today` would place it, once that controller is fixed but the view isn't).

4. **Whether the `TimeZoneOptions` class should be its own new file or a new property on an existing options class is unresolved by design (Claude's Discretion).**
   - What we know: `CalendarFeedOptions` is the named pattern to follow; no existing options class has an obvious semantic overlap with "board timezone" (it's a cross-cutting concern, not calendar-feed-specific — the sweep jobs and the ambient reads have nothing to do with the calendar feed feature).
   - What's unclear: Nothing blocking — this is genuinely left to the planner per CONTEXT.md.
   - Recommendation: A new, small, dedicated class (e.g. `TimeZoneOptions` with `SectionName = "TimeZone"` and a single `BoardTimeZoneId` string property defaulting to `"Europe/Amsterdam"`) keeps this concern independently testable and avoids coupling an unrelated feature's config section to it.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET SDK | Build/test | ✓ | 10.0.112 (`[VERIFIED: local shell]`) | — |
| Docker | Container build/deploy verification (Pitfall 7) | ✓ | 29.8.1 (`[VERIFIED: local shell]`) | — |
| `mssql-dev` container (Linux dev host only, per CLAUDE.md) | EF Core migrations against a real SQL Server, if a plan task needs one | ✗ — not running (`docker ps --filter name=mssql-dev` returned no rows, `[VERIFIED: local shell]`) | — | Start via `docker compose -f /home/theunschut/Documents/SQLServer/docker-compose.yml up -d` per CLAUDE.md if a task needs a live SQL Server; the EF InMemory provider (already used by every existing integration test) is sufficient for this phase's tests and needs no live SQL Server at all |
| Host `tzdata` (dev machine only) | Sanity-checking `TimeZoneInfo.FindSystemTimeZoneById` locally before touching the container | ✓ — `/usr/share/zoneinfo/Europe/Amsterdam` exists, `timedatectl` reports `Europe/Amsterdam (CEST, +0200)` (`[VERIFIED: local shell]`) | — | N/A — this only proves the *dev host* resolves the zone, not the container; see Pitfall 7 |
| Browser `Intl.DateTimeFormat` | Client-side conversion | ✓ — universal in evergreen browsers | N/A | None needed |

**Missing dependencies with no fallback:** None — `mssql-dev` is missing but has a documented fallback (EF InMemory, already the pattern every existing test uses) and is not required to plan or execute this phase.

## Validation Architecture

### Test Framework

| Property | Value |
|----------|-------|
| Framework | xUnit v3 (`xunit.v3` 3.2.2, `[VERIFIED: QuestBoard.UnitTests/QuestBoard.UnitTests.csproj and QuestBoard.IntegrationTests/QuestBoard.IntegrationTests.csproj]`) with `FluentAssertions` 8.10.0 and `NSubstitute` 5.3.0 |
| Config file | No separate config file — settings live in each `.csproj` (`<Using Include="Xunit" />`, package references) |
| Quick run command | `dotnet test QuestBoard.UnitTests` |
| Full suite command | `dotnet test` (runs `QuestBoard.UnitTests` + `QuestBoard.IntegrationTests`) |

### Phase Requirements → Test Map

No formal `REQ-ID`s exist for this phase yet (`.planning/REQUIREMENTS.md` traceability table has no Phase 86 entries — confirmed by reading the full file this session). The behaviors below come directly from CONTEXT.md's decisions.

| Behavior | Test Type | Automated Command | File Exists? |
|----------|-----------|--------------------|-------------|
| A wall-clock value (`EventEntity.Date`, `ProposedDateEntity.Date`, `FinalizedDate`) is byte-for-byte unmoved when the board clock is set to a non-`Europe/Amsterdam` zone | unit | `dotnet test QuestBoard.UnitTests --filter FullyQualifiedName~WallClockUnmoved` | ❌ Wave 0 |
| A real-instant render (`Html.LocalTime`) produces the correct board-zone text and a `title`/`datetime` attribute carrying the untouched UTC instant | unit (Razor helper can be tested directly without spinning up MVC, or via a lightweight integration test asserting response HTML) | `dotnet test QuestBoard.UnitTests --filter FullyQualifiedName~LocalTimeRender` | ❌ Wave 0 |
| An unresolvable configured zone falls back to UTC, sets the health check to Degraded, and the app still starts | unit (construct `IBoardClock` with a garbage zone id directly — no need to boot the full `WebApplicationFactory`) | `dotnet test QuestBoard.UnitTests --filter FullyQualifiedName~BoardClockFallback` | ❌ Wave 0 |
| `/health` returns HTTP 200 with a Degraded status body when the fallback is active | integration | `dotnet test QuestBoard.IntegrationTests --filter FullyQualifiedName~HealthCheck` | ❌ Wave 0 — note Pitfall 5: this must inject the *health check's* degraded flag directly via `ConfigureTestServices`, not attempt to force Hangfire's real zone-resolution path through the Testing-environment factory |
| `EventSeriesService`'s 7 `DateTime.Today` call sites, once moved to `IBoardClock`, still compute the same series/runway results under a `FixedTimeProvider`-style board clock fake | unit | `dotnet test QuestBoard.UnitTests --filter FullyQualifiedName~EventSeriesService` | ✅ — extend existing test class pattern (no existing `EventSeriesServiceTests` confirmed by this session's search; if absent, this is also a Wave 0 gap) |
| The Hangfire registration's `RecurringJobOptions.TimeZone` argument resolves to the configured board zone (not the Hangfire default `Utc`) | unit | Extract the options-building code into a small static/testable method and assert its return value directly — do not attempt to assert against a live Hangfire storage backend | ❌ Wave 0 |

### Sampling Rate

- **Per task commit:** `dotnet test QuestBoard.UnitTests` (fast, no DB)
- **Per wave merge:** `dotnet test` (full suite, includes `QuestBoard.IntegrationTests`)
- **Phase gate:** Full suite green before `/gsd-verify-work`

### Wave 0 Gaps

- [ ] A `FixedTimeProvider`-or-equivalent fake `IBoardClock` test double, matching the hand-rolled (not package-based) pattern in `EventsOverviewAggregationTests.cs:20-25`
- [ ] Confirm whether `EventSeriesServiceTests.cs` already exists (not found in this session's search of `QuestBoard.UnitTests/Services/`) — if absent, the 7-site `DateTime.Today` migration has no existing regression net and needs one written from scratch
- [ ] A unit test that forces `TimeZoneInfo.FindSystemTimeZoneById` to throw (e.g. via a wrapped resolver interface) to exercise D-04's fallback-and-Degraded path, since the real container is expected (per A1) to resolve successfully and would never exercise this path in normal CI

## Security Domain

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-------------------|
| V2 Authentication | No | Phase touches no auth surface |
| V3 Session Management | No | — |
| V4 Access Control | No | No new authorization boundary — existing page-level policies (`DungeonMasterOnly`, `AdminOnly`) are untouched |
| V5 Input Validation | Yes (narrow) | The new `TimeZoneOptions.BoardTimeZoneId` is operator-configured (not end-user input), but its `IsValid()` must not attempt zone resolution itself (see Anti-Patterns) — validate only that it is a non-empty string, and let the `IBoardClock` seam own the resolve-or-fallback decision |
| V6 Cryptography | No | — |

### Known Threat Patterns for this stack

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|-----------------------|
| Manual HTML string interpolation building the `<time datetime="..." title="...">` element could break attribute quoting if a format string or value ever contains an unescaped quote | Tampering (low severity — the values are system-generated dates, not user input, so practical exploitability is near zero) | Build the element via `TagBuilder`/`HtmlContentBuilder` rather than raw `$"..."` string interpolation, mirroring the caution (if not the exact mechanism) of the existing `Html.Markdown` extension, which delegates all untrusted-content sanitization to `IMarkdownService` before wrapping it |
| A configuration typo in `TimeZoneOptions.BoardTimeZoneId` silently degrades every sweep to UTC with only a log line as evidence | Denial of Service (silent) | D-04's mandatory Degraded health check is exactly this mitigation — already locked into the plan, not a gap this research is introducing |

## Sources

### Primary (HIGH confidence — read directly this session)

- `QuestBoard.Service/Program.cs` (full relevant sections) — Hangfire registrations, health check registration, `IsProduction()` fail-fast pattern
- `QuestBoard.Service/Jobs/DailyReminderJob.cs` — full file
- `QuestBoard.Domain/Models/CalendarFeedOptions.cs` — full file
- `QuestBoard.Domain/Extensions/ServiceExtensions.cs` — options registration idiom
- `QuestBoard.Domain/Services/CalendarSubscriptionService.cs` (lines 1-154) — feed entry assembly, `FinalizedDate` split
- `QuestBoard.Domain/Services/CalendarFeedWriter.cs` — DTSTART serialization, floating-local-time comment
- `QuestBoard.Repository/QuestRepository.cs`, `GroupRepository.cs` — `ClosedDate` assignment, campaign auto-signup ambient clock read
- `QuestBoard.Repository/Entities/ShopItemEntity.cs`, `CalendarSubscriptionEntity.cs` — property declarations
- `QuestBoard.Service/Extensions/HtmlHelperExtensions.cs` — the `Html.Markdown` precedent pattern
- `QuestBoard.Service/Controllers/QuestBoard/CalendarController.cs`, `Events/EventsController.cs`, `Events/SeriesController.cs` — ambient clock reads
- `QuestBoard.Domain/Services/EventSeriesService.cs` — all 7 `DateTime.Today` sites
- `QuestBoard.UnitTests/Services/EventsOverviewAggregationTests.cs` — `FixedTimeProvider` test double pattern
- `QuestBoard.IntegrationTests/WebApplicationFactoryBase.cs` — confirms Hangfire is not registered in `Testing`
- Every `.cshtml`/`.Mobile.cshtml` view file cited in the Date-Render Call Site Inventory tables above (~35 files, grepped and cross-checked with direct `Read` calls for ambiguous cases)
- `Dockerfile`, `docker-compose.yml` — confirms no `TZ`, no `/etc/localtime` mount, base image tag
- Local shell: `dotnet --version`, `docker --version`, `docker ps`, `timedatectl`, `ls /usr/share/zoneinfo/...`

### Secondary (MEDIUM confidence — official/authoritative source fetched this session)

- `raw.githubusercontent.com/HangfireIO/Hangfire/v1.8.23/src/Hangfire.Core/RecurringJob.cs` and `RecurringJobOptions.cs` — fetched directly against the exact pinned tag; confirms the `[Obsolete]` status of the `TimeZoneInfo` overloads and the `RecurringJobOptions.TimeZone` property

### Tertiary (LOW confidence — WebSearch only, flagged for validation)

- Debian-based `mcr.microsoft.com/dotnet/aspnet` images shipping `tzdata` by default (vs. Alpine not) — cross-referenced across a Steve Gordon blog post and multiple `dotnet/dotnet-docker` GitHub issues, not Microsoft's own official documentation page. See Assumption A1 and Pitfall 7 — recommend a real verification step against the built container image early in execution.
- Hangfire 1.8.0 as the version that introduced the `RecurringJobOptions` API and deprecated the `TimeZoneInfo` overload — inferred from a blog post title only, not the changelog itself. See Assumption A2 (low risk — the current API surface is independently confirmed).

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — zero new packages, every mechanism verified against either the pinned Hangfire source or the BCL/browser spec
- Architecture: HIGH — the `Html.Markdown` and `CalendarFeedOptions` precedents were read in full and are directly reusable patterns
- Pitfalls: HIGH — every pitfall in this document is backed by a direct `grep`/`Read` of the actual file, not inferred from CONTEXT.md's description alone; two genuine contradictions with CONTEXT.md were found and are called out explicitly rather than silently resolved

**Research date:** 2026-09-20
**Valid until:** 30 days (stable, low-churn domain — BCL/Hangfire/browser APIs; re-verify sooner only if the Dockerfile's base image tag changes, since that is the one fact this research could not directly test against the real container)
