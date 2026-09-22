# Phase 86: Viewer-Local Times and Correct Job Scheduling - Pattern Map

**Mapped:** 2026-09-20
**Files analyzed:** ~30 (1 helper, 1 options class, 1 seam interface+impl, 1 health check, 3 job registrations, 6 ambient-clock call sites, 22 view render sites across desktop/mobile twins, 1 script file, 1+ test files)
**Analogs found:** 8 / 8 core seams (view render sites share one analog pattern; two items — health check and `EventSeriesServiceTests` — have **no analog**, reported explicitly below)

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|---|---|---|---|---|
| `QuestBoard.Service/Extensions/HtmlHelperExtensions.cs` (add `Html.LocalTime`) | utility (Razor helper) | request-response | `Html.Markdown` in the same file | exact |
| `QuestBoard.Domain/Models/TimeZoneOptions.cs` (new) | config | request-response | `QuestBoard.Domain/Models/CalendarFeedOptions.cs` | exact |
| `QuestBoard.Domain/Interfaces/IBoardClock.cs` + `QuestBoard.Domain/Services/BoardClock.cs` (new) | service | event-driven / request-response (shared clock seam) | `TimeProvider.System` registration + `CalendarSubscriptionService`'s `timeProvider.GetUtcNow()` consumption | role-match (extends existing seam, no direct 1:1 analog) |
| `QuestBoard.Service/HealthChecks/BoardTimeZoneHealthCheck.cs` (new) | service (health check) | request-response | none found | no analog — see below |
| `QuestBoard.Service/Program.cs` (3 `RecurringJob.AddOrUpdate` edits + health check + options registration) | config | event-driven (cron) | itself (existing registrations being corrected) | exact (self-referential) |
| `QuestBoard.Domain/Services/EventSeriesService.cs` (7 sites), `QuestBoard.Repository/GroupRepository.cs`, `CalendarController.cs`, `EventsController.cs`, `SeriesController.cs`, `DailyReminderJob.cs`, `Series/Details.cshtml` | service/controller/job/view | CRUD (ambient clock read) | `CalendarSubscriptionService.cs:142-146` (`timeProvider.GetUtcNow()` consumption) | role-match |
| `QuestBoard.Service/wwwroot/js/site.js` (add `hydrateLocalTimes()`) | utility (client script) | transform | the file's own existing `DOMContentLoaded` block (datetime-local input init) | exact |
| ~22 `.cshtml`/`.Mobile.cshtml` render sites (see 86-UI-SPEC.md §3 table) | component (Razor view) | transform (format string → `Html.LocalTime` call) | `Account/Profile.cshtml:108,111` / `Profile.Mobile.cshtml:68,71` | exact |
| `QuestBoard.UnitTests/.../BoardClockTests.cs` (new) | test | transform | `EventsOverviewAggregationTests.cs`'s `FixedTimeProvider` | exact (pattern to mirror, no direct clock test exists) |
| `EventSeriesServiceTests` | test | CRUD | none — file does not exist | no analog — see below |

## Pattern Assignments

### `QuestBoard.Service/Extensions/HtmlHelperExtensions.cs` — add `Html.LocalTime`

**Analog:** `Html.Markdown` in the exact same file (full file, 25 lines — read in full, no re-read needed).

```csharp
// QuestBoard.Service/Extensions/HtmlHelperExtensions.cs (verbatim, full file)
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Rendering;
using QuestBoard.Domain.Interfaces;

namespace QuestBoard.Service.Extensions;

internal static class HtmlHelperExtensions
{
    internal static IHtmlContent Markdown(this IHtmlHelper html, string? markdown)
    {
        var service = html.ViewContext.HttpContext.RequestServices.GetRequiredService<IMarkdownService>();
        var rendered = service.RenderToHtml(markdown, MarkdownRenderTarget.Web);
        return new HtmlString($"<div class=\"markdown-content\">{rendered}</div>");
    }
}
```

**Pattern to copy exactly:**
- `internal static` extension method on `this IHtmlHelper html` — not a `TagHelper`. **Confirmed: no `TagHelper` exists anywhere in this solution** (`find . -iname "*TagHelper*.cs"` returns nothing). Do not introduce one.
- Service resolution: `html.ViewContext.HttpContext.RequestServices.GetRequiredService<T>()` — per-request resolution, no constructor injection into the static class. `Html.LocalTime` must resolve `IBoardClock` (or its `TimeZoneInfo`) the identical way: `RequestServices.GetRequiredService<IBoardClock>()`.
- Return type `IHtmlContent`. `Html.Markdown` returns raw `HtmlString` via string interpolation because its input is already-sanitized HTML; `Html.LocalTime` must NOT follow that interpolation shortcut — per 86-UI-SPEC.md §1, build the `<time>` element via `TagBuilder` (not raw string interpolation) so the ISO `datetime` and `title` attributes cannot break attribute quoting. This is the one place the new code deliberately diverges from the analog's exact mechanics while keeping its resolution/return shape.
- Signature shape to mirror: `internal static IHtmlContent LocalTime(this IHtmlHelper html, DateTime utcInstant, string style)` — non-nullable `DateTime`, per UI-SPEC's null contract (existing view-level `.HasValue`/`!= null` guards stay untouched, no null overload).

---

### `QuestBoard.Domain/Models/TimeZoneOptions.cs` (new)

**Analog:** `QuestBoard.Domain/Models/CalendarFeedOptions.cs` (full file, 35 lines).

```csharp
// QuestBoard.Domain/Models/CalendarFeedOptions.cs (verbatim, full file)
namespace QuestBoard.Domain.Models;

// Code defaults, overridable through configuration, so no deployment environment file has
// to change for the feature to work.
public class CalendarFeedOptions
{
    public const string SectionName = "CalendarFeed";
    public int MonthsBack { get; set; } = 3;
    public int MonthsAhead { get; set; } = 12;
    public int LastFetchedThrottleMinutes { get; set; } = 15;
    public int RetentionDays { get; set; } = 30;
    public int QuestDurationHours { get; set; } = 4;

    public bool IsValid() => MonthsBack >= 0 && MonthsAhead >= 1 && LastFetchedThrottleMinutes >= 1 && RetentionDays >= 1 && QuestDurationHours >= 1;
}
```

**Registration idiom** (`QuestBoard.Domain/Extensions/ServiceExtensions.cs:37-40`, verbatim):

```csharp
services.AddOptions<CalendarFeedOptions>()
    .BindConfiguration(CalendarFeedOptions.SectionName)
    .Validate(o => o.IsValid(), "CalendarFeed MonthsBack must be at least 0, and MonthsAhead, LastFetchedThrottleMinutes, RetentionDays and QuestDurationHours must each be at least 1.")
    .ValidateOnStart();
```

Other options in the same file (`EventSeriesOptions`, `EventsOverviewOptions`, `AgendaOptions`) follow the identical shape — this is the codebase's one established idiom for "code default, config override, validated at startup."

**Pattern to copy exactly:**
- `public const string SectionName = "..."` constant.
- Plain auto-properties with inline code defaults, each preceded by a comment explaining *why* that default (matches CLAUDE.md's comment style: plain-language why, no requirement IDs).
- `public bool IsValid()` returning a boolean expression, never throwing.
- **Deviation required by D-04 (do not copy this part of the idiom):** RESEARCH.md is explicit that `TimeZoneOptions.IsValid()` must **not** call `TimeZoneInfo.FindSystemTimeZoneById` — that check belongs in the `IBoardClock` seam's own construction (with fallback), not in `.ValidateOnStart()`, because `ValidateOnStart()` throws and would abort startup, defeating D-04's "still boots" requirement. `IsValid()` here should only check the configured id string is non-empty.
- Registration: same `.AddOptions<T>().BindConfiguration(T.SectionName).Validate(...).ValidateOnStart()` chain, added to `AddDomainServices` in `ServiceExtensions.cs` alongside the four existing option registrations (lines 18-40).

---

### `QuestBoard.Domain/Interfaces/IBoardClock.cs` + `BoardClock.cs` (new seam)

**Analog:** No direct 1:1 seam exists yet, but the injection idiom it must extend is `TimeProvider.System` registration + `CalendarSubscriptionService`'s consumption of it.

**Registration precedent** (`QuestBoard.Domain/Extensions/ServiceExtensions.cs:14-16`, verbatim):
```csharp
// The system clock is registered here so the domain reads time through an injectable
// seam rather than a static call.
services.TryAddSingleton(TimeProvider.System);
```

**Consumption precedent** (`QuestBoard.Domain/Services/CalendarSubscriptionService.cs:142-146`, verbatim per RESEARCH.md's verified excerpt):
```csharp
// FinalizedDate is a single DateTime carrying both date and time, unlike an
// event's already-split pair, so it is split explicitly here. The query
// guarantees FinalizedDate != null for every row that reaches this point.
Date = DateOnly.FromDateTime(q.FinalizedDate!.Value),
StartTime = TimeOnly.FromDateTime(q.FinalizedDate.Value),
```
(Note: the actual `timeProvider.GetUtcNow().UtcDateTime` call site itself sits elsewhere in the same service per RESEARCH.md's Reusable Assets note — this is the DI shape to follow: constructor-injected `TimeProvider`, called per-request, never a static `DateTime.UtcNow`.)

**Pattern to copy:**
- Register `IBoardClock`/`BoardClock` the same way as `TimeProvider.System` — `services.TryAddSingleton<IBoardClock, BoardClock>()` (or `AddSingleton` if no override use case exists) in `ServiceExtensions.cs`, near the existing `TimeProvider.System` line since `BoardClock` will itself take a constructor-injected `TimeProvider` plus the new `TimeZoneOptions`.
- Consumers (services, controllers, jobs) take `IBoardClock` by constructor injection exactly as `CalendarSubscriptionService` takes `TimeProvider` — never resolve it via `RequestServices` outside of the one Razor-helper exception (`Html.LocalTime`, which has no constructor to inject into, matching how `Html.Markdown` resolves `IMarkdownService`).
- Interface shape suggested by RESEARCH.md (discretionary, but this is the shape reviewed against DI conventions):
```csharp
public interface IBoardClock
{
    TimeZoneInfo TimeZone { get; }
    bool IsDegraded { get; }
    DateOnly Today { get; }
    DateTime Now { get; }
}
```

---

### `QuestBoard.Service/HealthChecks/BoardTimeZoneHealthCheck.cs` (new) — NO ANALOG

**No existing `IHealthCheck` implementation exists anywhere in the solution.** Confirmed: `grep -rn "class.*HealthCheck\|IHealthCheck" QuestBoard.Service` returns nothing, and `Program.cs:41` registers only a bare `builder.Services.AddHealthChecks();` with no `.AddCheck(...)` call, and `Program.cs:363` maps it bare: `app.MapHealthChecks("/health");`. This is the first custom health check the codebase will have.

**Task-shape implication:** the planner must design this from the ASP.NET Core framework contract directly (`Microsoft.Extensions.Diagnostics.HealthChecks.IHealthCheck.CheckHealthAsync`, registered via `.AddCheck<T>("board-timezone")` or the inline `.AddCheck("board-timezone", () => ...)` overload), not from a codebase precedent. It should read `IBoardClock.IsDegraded` and return `HealthCheckResult.Degraded(...)` when true, `HealthCheckResult.Healthy()` otherwise — never `Unhealthy`, since D-04 requires HTTP 200 to keep the docker-compose healthcheck from restart-looping.

---

### Hangfire recurring-job registrations — `Program.cs:370-393` (verbatim, to be corrected)

```csharp
// Only run migrations if not in testing environment
if (!app.Environment.IsEnvironment("Testing"))
{
    app.Services.ConfigureDatabase();

    // Register daily session reminder sweep — runs at 09:00 server local time (CET/CEST).
    // Placed after ConfigureDatabase to ensure migrations have run before the job can fire (RESEARCH.md Pitfall 4).
    RecurringJob.AddOrUpdate<DailyReminderJob>(
        "daily-session-reminders",
        job => job.ExecuteAsync(CancellationToken.None),
        "0 9 * * *");

    // Register nightly recurring-series top-up sweep — runs at 03:00 server local time, a
    // distinct off-peak hour from the reminder sweep above so neither job's failure can affect
    // the other. Daily rather than weekly so a failed run self-heals the next night.
    RecurringJob.AddOrUpdate<RecurringOccurrenceTopUpJob>(
        "recurring-occurrence-top-up",
        job => job.ExecuteAsync(CancellationToken.None),
        "0 3 * * *");

    // Register nightly calendar-subscription retention sweep — runs at 04:00 server local
    // time, a third distinct off-peak hour so no two sweeps can contend and one failing cannot
    // be mistaken for the other. Daily rather than weekly so a missed run self-heals the next
    // night; the sweep is idempotent because a purged row cannot be purged twice.
    RecurringJob.AddOrUpdate<CalendarSubscriptionRetentionJob>(
        "calendar-subscription-retention",
        job => job.ExecuteAsync(CancellationToken.None),
        "0 4 * * *");
}
```

**Note:** the comment on the note referencing "(RESEARCH.md Pitfall 4)" is itself a planning-artifact reference left in existing code — CLAUDE.md's "Code Comments" rule forbids this pattern; when this block is touched, that parenthetical should also be rewritten to explain the ordering constraint in plain language ("must run after migrations so the job's target tables already exist") without naming a planning document.

**Required correction pattern** (from RESEARCH.md, confirmed against Hangfire 1.8.23 source — the non-obsolete API):
```csharp
RecurringJob.AddOrUpdate<DailyReminderJob>(
    "daily-session-reminders",
    job => job.ExecuteAsync(CancellationToken.None),
    "0 9 * * *",
    new RecurringJobOptions { TimeZone = boardClock.TimeZone });
```
Do **not** use the `TimeZoneInfo`-positional overload — it is `[Obsolete]` in the pinned 1.8.23 version even though it still compiles. All three registrations, plus their three false "server local time" comments, must be corrected the same way (comments describe the actual mechanism — a configured board zone — in plain language, per CLAUDE.md).

---

### Ambient clock call sites (D-07) — analog for the injection swap

**Analog:** `CalendarSubscriptionService`'s existing `TimeProvider` constructor injection (see `IBoardClock` section above) — this is the only precedent in the codebase for "a service takes a clock seam by DI instead of calling `DateTime.Today`/`DateTime.Now` statically."

**Sites to convert** (verified by RESEARCH.md's line-by-line grep, listed here for the planner's direct reference — no further reading needed):
- `QuestBoard.Domain/Services/EventSeriesService.cs:21,58,90,172,185,192,229` (7 sites) — `DateOnly.FromDateTime(DateTime.Today)` → `boardClock.Today`
- `QuestBoard.Repository/GroupRepository.cs:74` — same
- `QuestBoard.Service/Controllers/QuestBoard/CalendarController.cs:24` — `DateTime.Now` → `boardClock.Now`
- `QuestBoard.Service/Controllers/Events/EventsController.cs:95` — same as EventSeriesService
- `QuestBoard.Service/Controllers/Events/SeriesController.cs:68` — same
- `QuestBoard.Service/Jobs/DailyReminderJob.cs:18` (and its false comment at lines 16-17, "DateTime.Today is server local time on the LXC container (CET/CEST)" — must be corrected, not preserved)
- `QuestBoard.Service/Views/Series/Details.cshtml:12,17` — a **seventh D-07 site not in CONTEXT.md's original list** (RESEARCH.md Pitfall 4 / UI-SPEC §5). Pattern: pass the resolved board-zone `DateOnly`/label down from `SeriesController` into the view model rather than having the view call the ambient clock itself — this is the more testable fix and matches the MVC separation already used elsewhere (controllers own clock reads, views consume view-model data).

**Pattern to copy:** constructor-inject `IBoardClock` the same way each of these classes likely already injects other domain services/repositories (standard constructor DI already used throughout `QuestBoard.Domain`/`QuestBoard.Service`) — no new DI mechanism, just a new constructor parameter replacing a static `DateTime.Today`/`DateTime.Now` call.

---

### Client-side hydration — `QuestBoard.Service/wwwroot/js/site.js`

**Analog:** the file's own existing single `DOMContentLoaded` listener (lines 273-288+), which already initializes `datetime-local` input defaults, and (per UI-SPEC) also initializes toasts and Bootstrap tooltips further down the same block.

```javascript
// QuestBoard.Service/wwwroot/js/site.js:273-288 (verbatim excerpt)
document.addEventListener('DOMContentLoaded', function() {
    // Handle datetime-local inputs
    const datetimeInputs = document.querySelectorAll('input[type="datetime-local"]');
    datetimeInputs.forEach(input => {
        // For edit pages, clean existing values to remove seconds/milliseconds
        if (input.value) {
            cleanDateTimeValue(input);
        } else {
            // For create pages, set default time
            setDefaultDateTime(input);
        }
    });

    // Add resize listener for masonry layout
    window.addEventListener('resize', handleResize);
    ...
```

**Pattern to copy exactly:**
- **One listener, not two.** `site.js` has exactly one bottom `DOMContentLoaded` registration. Per UI-SPEC's implementation seam notes, `hydrateLocalTimes()` must be a plain function **invoked from inside this existing block** (alongside the datetime-local init, resize listener, toast/tooltip init already there) — never a second `document.addEventListener('DOMContentLoaded', ...)` call.
- `querySelectorAll` + `forEach` is the established iteration idiom in this file (see the `datetimeInputs.forEach` above) — `hydrateLocalTimes()` should use `document.querySelectorAll('time.local-time[datetime]').forEach(el => { ... })` the same way, with a `try`/`catch` per iteration per UI-SPEC §6's failure contract.
- No module system, no bundler — plain function declarations in the same file, consistent with `cleanDateTimeValue`/`setDefaultDateTime`/`handleResize` being ordinary top-level functions referenced from the listener.

**Layout inclusion (confirmed identical in both layouts):**
```
QuestBoard.Service/Views/Shared/_Layout.cshtml:256:        <script src="~/js/site.js" asp-append-version="true"></script>
QuestBoard.Service/Views/Shared/_Layout.Mobile.cshtml:217: <script src="~/js/site.js" asp-append-version="true"></script>
```
Identical `<script>` tag in both — no separate mobile script to touch.

---

### Representative Razor render site + mobile twin — `Account/Profile.cshtml` / `Profile.Mobile.cshtml`

**Desktop** (`QuestBoard.Service/Views/Account/Profile.cshtml:104-113`, verbatim):
```cshtml
<td>
    <div>@subscription.Name</div>
    <div class="calendar-subscription-meta">
        Created @subscription.CreatedAt.ToString("MMM d, yyyy")
        @if (subscription.LastFetchedAt.HasValue)
        {
            @:&middot; Last fetched @subscription.LastFetchedAt.Value.ToString("MMM d, yyyy h:mm tt")
        }
        else
        {
            @:&middot; Never fetched yet
```

**Mobile twin** (`QuestBoard.Service/Views/Account/Profile.Mobile.cshtml:63-72`, verbatim):
```cshtml
<div class="calendar-subscription-row">
    <div class="account-field-value calendar-subscription-name">@subscription.Name</div>
    <div class="account-field-label calendar-subscription-meta">
        Created @subscription.CreatedAt.ToString("MMM d, yyyy")<br />
        @if (subscription.LastFetchedAt.HasValue)
        {
            @:Last fetched @subscription.LastFetchedAt.Value.ToString("MMM d, yyyy h:mm tt")
        }
        else
        {
            @:Never fetched yet
```

**Before/after edit pattern for the planner:**
- `@subscription.CreatedAt.ToString("MMM d, yyyy")` → `@Html.LocalTime(subscription.CreatedAt, "date")`
- `@subscription.LastFetchedAt.Value.ToString("MMM d, yyyy h:mm tt")` → `@Html.LocalTime(subscription.LastFetchedAt.Value, "date-time")`
- **The existing `subscription.LastFetchedAt.HasValue` null guard and the `else` "Never fetched yet" branch are untouched** — `Html.LocalTime` only ever replaces the `.ToString(...)` call inside the already-true branch, per the null contract in UI-SPEC §"Implementation seam notes."
- **Divergence between the two twins to note:** desktop wraps the meta text in inline `@:&middot;` HTML-entity separators inside one `<div>`; mobile uses a `<br />` line break and drops the `&middot;` separator entirely, rendering the same two facts (`Created`, `Last fetched`) as two stacked lines instead of one middot-joined line. The `Html.LocalTime` swap does not change this structural divergence — it only replaces the value expression inside each existing text position, on both layouts, in the same wave (per UI-SPEC §5's parity rule).

---

### Test doubles for time — `QuestBoard.UnitTests`

**Analog:** `QuestBoard.UnitTests/Services/EventsOverviewAggregationTests.cs:16-25` (verbatim, confirmed via direct read):
```csharp
public class EventsOverviewAggregationTests
{
    private static readonly DateTimeOffset DefaultClockInstant = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    // Hand-written rather than a testing-time-provider package, so the fixed clock costs no
    // new dependency: this phase's package legitimacy position is that it installs nothing.
    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
```

**Pattern to copy exactly:** a `private sealed class FixedXxx : TimeProvider` (or, for `IBoardClock` directly, a hand-rolled `private sealed class FixedBoardClock : IBoardClock` with settable/constructor-fixed `TimeZone`/`Today`/`Now`/`IsDegraded` properties) nested inside the test class, with the same one-line comment convention explaining why it's hand-rolled rather than a package (`NSubstitute` is already a project dependency and used elsewhere in the same file for `ILogger`, but a hand-rolled fake matches this specific "fixed clock" precedent exactly). No new test package needed — matches the codebase's own stated "package legitimacy" position for this exact kind of fixture.

**`EventSeriesServiceTests` — NO ANALOG, confirmed absent.** `find . -iname "EventSeriesServiceTests*"` returns nothing. This changes the task shape for the 7-site `EventSeriesService` clock migration: there is no existing test file to extend with a `FixedBoardClock` case per RESEARCH.md's Pitfall 5 concern (Hangfire/DI integration tests can't observe this either — see below). The planner should treat covering `EventSeriesService`'s 7 clock-dependent branches as new test-file creation, not test-file extension, and should model the new file directly on `EventsOverviewAggregationTests.cs`'s structure (fixed-clock fields, `NSubstitute`-backed repository mocks, `FluentAssertions` assertions) since no domain-service test file for this specific class exists to imitate more directly.

## Shared Patterns

### Options class idiom (config, code-default + override + validate-on-start)
**Source:** `QuestBoard.Domain/Models/CalendarFeedOptions.cs` + `ServiceExtensions.cs:37-40`
**Apply to:** `TimeZoneOptions` — see full excerpt above. Deviation: `IsValid()` must not call `TimeZoneInfo.FindSystemTimeZoneById` (that belongs in `BoardClock`'s own fallback-aware construction, per D-04).

### `IHtmlHelper` extension for cross-cutting Razor rendering
**Source:** `QuestBoard.Service/Extensions/HtmlHelperExtensions.cs` (`Html.Markdown`)
**Apply to:** `Html.LocalTime` — same file, same `internal static` extension shape, same `RequestServices.GetRequiredService<T>()` resolution. No `TagHelper` anywhere in the solution; do not introduce one.

### Clock-as-injectable-seam (never a static `DateTime.Today`/`.Now`/`.UtcNow` call in new/touched code)
**Source:** `services.TryAddSingleton(TimeProvider.System)` in `ServiceExtensions.cs:16`, consumed by `CalendarSubscriptionService`
**Apply to:** `IBoardClock`/`BoardClock` registration and every D-07 ambient-clock call site (`EventSeriesService`, `GroupRepository`, `CalendarController`, `EventsController`, `SeriesController`, `DailyReminderJob`, `Series/Details.cshtml` via `SeriesController`).

### Desktop/`.Mobile` twin parity
**Source:** structural convention across the whole view tree (53 `.Mobile.cshtml` files); concretely demonstrated by `Account/Profile.cshtml` / `Profile.Mobile.cshtml` above.
**Apply to:** every one of the ~22 render-site edits in 86-UI-SPEC.md §3 — a desktop edit and its twin's edit (where the twin renders the same property) ship in the same wave. The explicit non-regression carve-out in UI-SPEC §5 (six site pairs where the mobile twin never rendered the property at all) must NOT gain a new render site.

### Single shared `DOMContentLoaded` listener in `site.js`
**Source:** `QuestBoard.Service/wwwroot/js/site.js:273` (existing datetime-local/resize/toast/tooltip init block)
**Apply to:** `hydrateLocalTimes()` — invoked from inside this same listener, never a second listener registration.

## No Analog Found

| File | Role | Data Flow | Reason |
|---|---|---|---|
| `QuestBoard.Service/HealthChecks/BoardTimeZoneHealthCheck.cs` | service (health check) | request-response | No custom `IHealthCheck` implementation exists anywhere in the solution (confirmed by grep); `AddHealthChecks()` is registered bare with no `.AddCheck(...)`. This is the first of its kind — implement directly from the ASP.NET Core framework contract, not from a codebase precedent. |
| `EventSeriesServiceTests` (new) | test | CRUD | File does not exist (confirmed by `find`). RESEARCH.md could not locate it either. The 7-site `EventSeriesService` clock migration needs a new test file modeled on `EventsOverviewAggregationTests.cs`'s structure rather than an existing test file to extend. Also note: Hangfire itself is not registered in the `Testing` environment (`Program.cs` wraps all three `RecurringJob.AddOrUpdate` calls in `if (!app.Environment.IsEnvironment("Testing"))`), so the cron/timezone wiring cannot be integration-tested through `WebApplicationFactoryBase` — only the `IBoardClock`/options resolution-and-fallback logic can be unit-tested directly. |

## Metadata

**Analog search scope:** `QuestBoard.Service/Extensions/`, `QuestBoard.Domain/Models/`, `QuestBoard.Domain/Extensions/ServiceExtensions.cs`, `QuestBoard.Domain/Services/CalendarSubscriptionService.cs`, `QuestBoard.Service/Program.cs`, `QuestBoard.Service/wwwroot/js/site.js`, `QuestBoard.Service/Views/Shared/_Layout*.cshtml`, `QuestBoard.Service/Views/Account/Profile*.cshtml`, `QuestBoard.UnitTests/Services/EventsOverviewAggregationTests.cs`, solution-wide `grep`/`find` for `TagHelper`, `IHealthCheck`, `EventSeriesServiceTests`.
**Files scanned:** ~12 read directly this session, plus the full call-site inventory already verified line-by-line in 86-RESEARCH.md and 86-UI-SPEC.md (not re-read here to avoid duplicate ranges).
**Pattern extraction date:** 2026-09-20
