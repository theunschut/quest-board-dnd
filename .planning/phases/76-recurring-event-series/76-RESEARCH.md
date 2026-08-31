# Phase 76: Recurring Event Series - Research

**Researched:** 2026-08-28
**Domain:** Custom cycle-mask date generation (Domain layer), EF Core idempotency constraints, Hangfire per-group background job iteration, fail-closed multi-tenant query filters (ASP.NET Core MVC + SQL Server, .NET 10)
**Confidence:** HIGH

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

D-01…D-30 are locked in `76-CONTEXT.md` and are treated as non-negotiable by this research. Full text is in that file; the load-bearing ones for planning are restated here verbatim in substance:

- **D-01**: A cycle-mask position is one cadence step, not one calendar week: `date(N) = AnchorDate + (N × IntervalWeeks) weeks`, `fires(N) = CycleMask[N mod CycleMask.Length] == 1`. `SeriesSlotIndex` counts every step including non-firing ones — this is what makes `(SeriesId, SeriesSlotIndex)` idempotency work under a moved occurrence.
- **D-02**: Weekly grid only. No daily, no monthly (deferred).
- **D-03**: Mask entered as a clickable toggle strip, no fixed length (`nvarchar(200)`, ~100-position schema ceiling, 24-position UI cap per UI-SPEC).
- **D-04**: `WeekDay` is derived from `AnchorDate` and written on save, never independently editable.
- **D-05**: The live preview is computed server-side by a debounced fetch, running the *same* Domain generator that later materializes occurrences. No JS reimplementation.
- **D-06**: Series setup is a "repeats" toggle on the existing Create Event form. Plus a series detail page reached from any occurrence's Details view.
- **D-07**: The cadence rule (anchor, interval, mask) is immutable after creation — stronger than and supersedes the roadmap's "rule edits are additive only" lock for cadence. Changing the rhythm means ending the series and creating a new one.
- **D-08**: `Title`, `Description`, `StartTime` are added to `EventSeriesEntity` as template fields in this phase's migration; the generator stamps them onto every occurrence.
- **D-09**: Saving an edit to one occurrence prompts for scope — "Only this event" (default) or "This and all future events." The latter updates the series template and rewrites future occurrences nobody has separately touched (moved/edited/cancelled ones are skipped). No past occurrence is ever rewritten by any scope.
- **D-10**: The series page carries no template edit form — template changes flow only through D-09's scope prompt.
- **D-11**: Ending a series sets a nullable `EndDate`. No slot fires past it. Past occurrences are always kept; the confirm offers to clear future ones.
- **D-12**: Removing a series offers Delete (removes series + all its occurrences, signups cascade via `FK_EventSignups_Events_EventId`) or Detach (drops only the rule, nulls both `SeriesId` and `SeriesSlotIndex` on every occurrence, leaves them as one-off events). `FK_Events_EventSeries_SeriesId` has no `onDelete` today — EF Core defaults an optional relationship to `NO ACTION`, so both outcomes must be written deliberately.
- **D-13**: The series-delete confirm counts sessions split past/future, plus real `HasAnswered` answers — a deliberate, narrow divergence from Phase 75 D-26 (which governs the *single-event* delete confirm unchanged).
- **D-14**: A cancelled occurrence is a tombstone — the row stays, gains a cancelled marker. Availability answers survive.
- **D-15**: Cancelled occurrences stay visible, struck through and muted, on desktop calendar, mobile agenda, and the details page (banner, availability buttons removed).
- **D-16**: Cancel replaces Delete on a series occurrence, enforced server-side by re-resolving `SeriesId` — not merely hidden in markup. One-off events keep Delete unchanged.
- **D-17**: Moving an occurrence onto a date another *live* sibling already holds is allowed, with a notice in the D-09 scope dialog. A cancelled sibling does not trigger the notice.
- **D-18 (hard constraint)**: The existence check is keyed on `(SeriesId, SeriesSlotIndex)`. Never on date.
- **D-19 (hard constraint)**: A filtered unique index on `(SeriesId, SeriesSlotIndex) WHERE SeriesId IS NOT NULL` is added in this phase's migration.
- **D-20 (hard constraint)**: The existence query loads the series' slot indexes with no date predicate — never scoped to the runway window, because a moved occurrence may sit outside it.
- **D-21**: The runway is measured in live (non-cancelled) future occurrences, not a date horizon.
- **D-22**: The runway is a global 20, a code default overridable through configuration. Not per-series.
- **D-23**: Only slots dated today or later are materialized. Past slots are computed (for numbering/phase) but never created.
- **D-24**: Generation is one Domain service method, called directly — by the controller synchronously on save, and by the recurring job on schedule. Not enqueued to Hangfire on save.
- **D-25**: Commit granularity differs by path: the job commits per-occurrence (with its campaign signups) for monotonic progress under retry; the controller wraps the series row + first generation pass in one transaction.
- **D-26**: A DM-visible horizon banner on the calendar page when any active series is below its runway.
- **D-27**: A dedicated daily recurring job at an off-peak hour, registered beside `daily-session-reminders`, following `HangfireJobHelper.RunInScopeAsync` with `IServiceScopeFactory`.
- **D-28**: The job iterates groups with `SetGroupId()` per group — never `IgnoreQueryFilters()`.
- **D-29**: The generator's campaign fan-out must not stamp `UpdatedAt`.
- **D-30**: Fan-out happens at occurrence-create time, in the same unit of work as the occurrence, for every member regardless of role, regardless of the occurrence's date. Campaign boards only.

### Claude's Discretion

- Whether `EventSeries` gets its own domain model / repository / service triple, or is served through the existing `IEventService`; and whether the series page is a new controller or actions on `EventsController`.
- Naming and type of the cancelled marker — a bool, or a nullable `CancelledAt` timestamp.
- Exact preview count (~10 per EVTRECUR-02, pinned to **exactly 10** by UI-SPEC) and past-anchor preview behaviour (pinned by UI-SPEC — see below).
- Toggle-strip styling and UI-level cap (pinned by UI-SPEC at 24 positions — see below).
- The off-peak hour for the recurring job, and its Hangfire job id.
- Wording of every confirm/banner/toast (pinned by UI-SPEC — see below).
- Whether the D-09 scope prompt is a native `confirm()` or a custom dialog (pinned by UI-SPEC: custom two-button Bootstrap modal).
- Index strategy beyond the mandated D-19 unique index.
- Where the `DateOnly` → `DateTime` conversion seam sits for any new calendar-facing view model.
- Whether a detached, cancelled occurrence keeps its cancelled marker or is un-cancelled.
- Test structure beyond the mandated two-group tenant isolation test and the idempotency tests EVTRECUR-07 implies (run twice; cancel-then-run; move-then-run; move-outside-runway-then-run; EVTRECUR-08 mirrored-mask interleaving).

**Note:** `76-UI-SPEC.md` (read alongside this research, already approved-pending) resolves most of the above discretion items — dialog types, exact copy, toggle-strip cap (24), preview count (10) and past-anchor copy, color/icon treatment for Cancel vs Delete, and the series-page layout. Treat UI-SPEC as binding for anything it pins; this research does not re-litigate those choices.

### Deferred Ideas (OUT OF SCOPE)

- Daily cadence (cadence-unit field + `AddDays`).
- Monthly cadence ("nth weekday of every N months") — the one shape RFC 5545 `BYSETPOS` handles natively; deliberately not pursued this phase.
- Per-series runway override.
- An advanced mask editor beyond the toggle strip.
- EVTRECUR-09 — regenerating untouched future occurrences on a rule edit (moot under D-07's immutable-cadence lock).
- Phase 77 consequence (not deferred for that phase, just not this one): the availability overview grid must exclude cancelled occurrences.

</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| EVTRECUR-01 | DM sets cadence (every N weeks on a weekday) + anchor date + repeating on/off cycle mask | D-01 arithmetic verified against `EventSeriesEntity` schema; see Architecture Patterns → Cycle-Mask Generator |
| EVTRECUR-02 | Live preview of next ~10 dates before saving | D-05 fetch idiom traced to `Views/Quest/Details.cshtml:966`; UI-SPEC pins exactly 10 and past-anchor copy |
| EVTRECUR-03 | Occurrences generated ahead of time on a rolling window, topped up automatically | D-21/D-22/D-27 traced to `HangfireJobHelper`/`DailyReminderJob`/`Program.cs` registration pattern |
| EVTRECUR-04 | DM cancels a single occurrence without affecting the rest | D-14/D-16 traced to `QuestController.cs:762` server-side re-resolution precedent |
| EVTRECUR-05 | DM moves a single occurrence to a different date without affecting the rest | D-09/D-17/D-20 — existence-check query design documented below |
| EVTRECUR-06 | DM edits a single occurrence's details without affecting the rest | D-09 scope-sweep design; `BaseRepository.UpdateAsync` narrow-write caveat documented below |
| EVTRECUR-07 | Re-running the generator never duplicates, resurrects, or overwrites | D-18/D-19/D-20 verified against shipped migration (`IX_Events_SeriesId` is **not** unique today) and `AutomaticRetryAttribute{Attempts=5}` at `Program.cs:260` |
| EVTRECUR-08 | Two boards with mirrored masks on the same cadence/anchor interleave without colliding | D-01's slot-index arithmetic verified to structurally guarantee this (shared date grid, disjoint firing positions) |

</phase_requirements>

## Summary

This phase adds no new libraries, no new NuGet packages, and no new external service. Everything it needs — Hangfire, EF Core, the fail-closed query-filter convention, the `modern-card` view pattern — already ships in this codebase from Phases 55/74/75. The work is entirely: (1) a pure, dependency-free Domain-layer date generator implementing the D-01 arithmetic; (2) an EF Core migration adding template fields, `EndDate`, a cancelled marker, and — critically — a filtered unique index that does not exist today; (3) a Hangfire job that must iterate real groups one at a time, because this is the first job in the codebase to *write* data across every board rather than read it with `IgnoreQueryFilters()`; and (4) view/controller work extending the Phase 74/75 event surfaces per `76-UI-SPEC.md`.

The single highest-risk fact this research confirms by reading the shipped schema rather than assuming it: **`IX_Events_SeriesId` is a plain, non-unique index today.** Nothing at the database level currently prevents two rows from claiming the same `(SeriesId, SeriesSlotIndex)` pair, and the app already registers a global `AutomaticRetryAttribute { Attempts = 5 }` (`Program.cs:260`) that re-runs a partially-failed Hangfire job from scratch. Without D-19's filtered unique index, a crash mid-generation is a guaranteed duplicate-occurrence bug, not a theoretical one. This research also confirms EF Core's SQL Server provider already auto-filters nullable-column unique indexes to `IS NOT NULL` by convention — the D-19 index should still be declared with an explicit `.HasFilter(...)` matching the locked wording, both for readability and because `SeriesSlotIndex` should never independently be null when `SeriesId` is set (defense in depth, not reliance on the convention).

The second confirmed risk: the stale doc comment on `ActiveGroupContextService` ("null means see all," `ActiveGroupContextService.cs:19`) is real and must be corrected in this phase's diff, because every `HasQueryFilter` in `QuestBoardContext.cs` (lines 330–455) is fail-closed — a null `ActiveGroupId` returns zero rows. The one cross-group precedent in the codebase, `QuestRepository.GetQuestsForTomorrowAllGroupsAsync`, uses `IgnoreQueryFilters()` for a **read-only** sweep; the recurring-occurrence job **writes** and must instead enumerate real group ids (via `GroupRepository`, whose backing `GroupEntity` carries no query filter at all — it *is* the tenant boundary) and call `SetGroupId()` before every per-group query, exactly as D-28 locks.

**Primary recommendation:** Build the generator as a pure, no-DI, no-DateTime.Now-reaching static-friendly Domain class that takes `(AnchorDate, IntervalWeeks, CycleMask, EndDate, todayOverride)` and returns candidate `(SlotIndex, Date)` pairs — unit-testable with zero database. Layer materialization (the idempotency check, the `AddWithCampaignFanOutAsync` write, the runway top-up) as a second Domain service method that consumes the generator's pure output. The controller's live-preview endpoint and the Hangfire job both call the *materialization* method's read-only preview path and full-write path respectively — never two implementations of the date math.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Cycle-mask date arithmetic (D-01) | Domain | — | Pure function, no I/O; must be identical between preview and materialization (D-05) |
| Occurrence idempotency check (D-18/D-19/D-20) | Domain (query orchestration) + Database (constraint) | Repository | The check is a Domain-level decision ("is this slot already handled?"); the unique index is the backstop when the check itself races under retry |
| Series template storage & occurrence stamping (D-08) | Repository (entity) | Domain (mapping) | `EventSeriesEntity` gains fields; `EventRepository`/generator read them once per generation pass |
| Live preview endpoint (D-05) | API/Backend (Service controller action) | Domain (shared generator) | Controller is a thin fetch target; all logic lives in the Domain method it calls |
| Per-occurrence campaign fan-out (D-29/D-30) | Repository | Domain (orchestration) | Reuses existing `AddWithCampaignFanOutAsync` — do not reimplement |
| Rolling-window top-up job (D-21/D-22/D-27) | API/Backend (Hangfire job in Service project) | Domain (generator + materialization call) | Job is I/O orchestration (DI scope, per-group iteration); it must not embed date logic itself |
| Tenant/group iteration safety (D-28) | API/Backend (job) + Database (query filter) | — | `SetGroupId()` per group before any repository call inside the loop |
| Cancel/Move/Edit single occurrence (D-14…D-17) | API/Backend (controller actions) | Browser (scope-prompt modal, D-09 UI) | Server re-resolves `SeriesId`/ownership on every write; client UI is convenience only |
| Horizon health signal (D-26) | Browser/Frontend (calendar page banner) | API/Backend (one count query) | DM-visible, degrades honestly even if the check itself has a bug — the DM is already looking at the page that would be running dry |
| Series lifecycle (End/Delete/Detach) (D-11/D-12/D-13) | API/Backend (controller) | Database (FK behavior — currently `NO ACTION`, must be handled in code) | The shipped FK does not cascade; both outcomes are explicit code paths |

## Standard Stack

No new libraries are introduced by this phase. Every dependency below already ships in the codebase.

### Core (existing, reused)

| Library | Version | Purpose | Why Standard (for this phase) |
|---------|---------|---------|--------------------------------|
| Hangfire.AspNetCore / Hangfire.SqlServer | 1.8.23 [VERIFIED: QuestBoard.Service.csproj] | Recurring job scheduling for the rolling-window top-up (D-27) | Already used for `daily-session-reminders`; this phase adds a sibling job, not a new scheduler |
| Microsoft.EntityFrameworkCore(.SqlServer) | matches `net10.0` target [VERIFIED: QuestBoard.Repository.csproj] | Migration for template fields, `EndDate`, cancelled marker, filtered unique index | Existing ORM; no alternative considered |
| AutoMapper | existing app-wide config [VERIFIED: EntityProfile.cs] | Entity↔domain-model mapping for the extended `Event`/`EventSeries` models | Existing convention; `Signups`/`Group`/`Series` already explicitly ignored on reverse maps — extend, don't replace |
| xunit.v3 / FluentAssertions / NSubstitute / EF Core InMemory | pinned in `QuestBoard.UnitTests.csproj` [VERIFIED] | Unit tests for the pure generator and for repository-level idempotency/tenant-isolation tests | Existing test stack; `QuestBoardContext` + `InMemoryDatabase` + a settable `IActiveGroupContext` is the established repository-test pattern (see `EventSignupRepositoryTests.cs`) |

### Explicitly rejected

| Instead of | Rejected alternative | Why |
|------------|----------------------|-----|
| Custom Domain-layer generator | An RRULE/iCalendar library (`Ical.Net`, etc.) | RFC 5545's `BYSETPOS` selects only within a single interval, not across periods, so "two on, two off" is not expressible; any library would still need post-filtering. Already excluded in `.planning/REQUIREMENTS.md` Out of Scope and reaffirmed by `76-ROADMAP.md` scope notes. [CITED: project REQUIREMENTS.md] |
| `nvarchar(200)` comma-delimited mask | JSON column / int bitmask / child table | No JSON precedent in this schema; a bitmask caps cycle length and is opaque; a child table is a join for something never queried independently. Locked in ROADMAP, restated in CONTEXT.md scope notes. |
| Cancelled tombstone row | Hard-delete + separate skipped-slots table, or a per-series watermark | Two sources of truth for the same question, or an unresolvable ambiguity between "cancelled" and "never created" (D-14). |

**Installation:** none — no `dotnet add package` needed for this phase.

## Package Legitimacy Audit

**Not applicable.** This phase installs zero external packages (NuGet, npm, or otherwise). All work uses libraries already present in the solution (see Standard Stack above). No `npm view` / `pip index versions` / `cargo search` verification is required, and there is nothing for the Package Legitimacy Gate to check.

**Packages removed due to [SLOP] verdict:** none — no packages were proposed.
**Packages flagged as suspicious [SUS]:** none.

## Architecture Patterns

### System Architecture Diagram

```
┌─────────────────────────── Browser ───────────────────────────┐
│  Create Event form (repeats toggle)      Series Details page   │
│    │ debounced fetch (400ms)               │ (read-only)        │
└────┼───────────────────────────────────────┼─────────────────-─┘
     │ POST /Events/PreviewSeries            │ GET
     ▼                                       ▼
┌─────────────────────── Service (Controllers) ──────────────────┐
│  EventsController                    SeriesController (or       │
│   .Create (POST, wraps in 1 txn)      actions on Events-         │
│   .Edit (scope-aware, D-09)           Controller — discretion)   │
│   .Cancel (replaces Delete, D-16)                                │
│   .PreviewSeries (read-only, calls same generator as below)      │
└───────────────┬─────────────────────────────────┬───────────────┘
                │                                  │
                ▼                                  ▼
┌─────────────────── Domain (pure + orchestration) ───────────────┐
│  EventSeriesDateGenerator (pure, D-01 arithmetic)                │
│    GenerateCandidates(anchor, intervalWeeks, mask, endDate,      │
│                       today) -> IEnumerable<(int Slot, DateOnly)>│
│         │ consumed by both preview and materialization           │
│         ▼                                                        │
│  EventSeriesMaterializer / IEventSeriesService (orchestration)   │
│    - loads existing SlotIndexes for series (D-20: no date filter)│
│    - filters candidates to slots not yet present (D-18)          │
│    - filters to today-or-later (D-23)                            │
│    - calls IEventRepository.AddWithCampaignFanOutAsync per slot  │
│      (D-29: no UpdatedAt stamp; D-30: every member, any date)    │
└───────────────┬───────────────────────────────────┬─────────────┘
                │                                    │
                ▼                                    ▼
┌────────────── Repository (EF Core / SQL Server) ─────────────────┐
│  EventEntity (+ IsCancelled/CancelledAt, D-14)                    │
│  EventSeriesEntity (+ Title/Description/StartTime D-08, EndDate  │
│                       D-11)                                       │
│  Unique filtered index: (SeriesId, SeriesSlotIndex)               │
│                          WHERE SeriesId IS NOT NULL (D-19)        │
└────────────────────────────────────────────────────────────────-─┘

┌────────────────── Hangfire (off-peak daily, D-27) ───────────────┐
│  RecurringOccurrenceTopUpJob.ExecuteAsync()                       │
│    groupIds = GroupRepository.GetAllWithMemberCountAsync()        │
│    foreach groupId:                                               │
│      HangfireJobHelper.RunInScopeAsync(scopeFactory, groupId, sp =>│
│        for each active (EndDate null-or-future) series in group:  │
│          call EventSeriesMaterializer to top up to runway (D-21/22)│
│          commit per occurrence (D-25 — monotonic under retry)     │
│      )                                                             │
│  -- NEVER IgnoreQueryFilters(); NEVER a single cross-group query   │
└────────────────────────────────────────────────────────────────-─┘
```

### Recommended Project Structure

```
QuestBoard.Domain/
├── Models/
│   ├── Event.cs                    # + SeriesSlot-adjacent fields already present; add cancelled marker
│   └── EventSeries.cs              # NEW — domain model counterpart to EventSeriesEntity (currently has none)
├── Interfaces/
│   ├── IEventSeriesService.cs      # NEW — cadence CRUD + GenerateAndMaterializeAsync + PreviewAsync
│   └── IEventSeriesRepository.cs   # NEW — or fold into IEventRepository (discretion)
├── Services/
│   ├── EventSeriesDateGenerator.cs # NEW — pure, static-friendly, zero DI, zero I/O
│   └── EventSeriesService.cs       # NEW — orchestration: idempotency, fan-out, runway top-up

QuestBoard.Repository/
├── Entities/
│   ├── EventSeriesEntity.cs        # + Title, Description, StartTime, EndDate
│   └── EventEntity.cs              # + IsCancelled (bool) or CancelledAt (DateTime?)
├── Migrations/
│   └── {timestamp}_AddSeriesRecurrence.cs   # template fields, EndDate, cancelled marker, filtered unique index
├── EventSeriesRepository.cs        # NEW (or extend EventRepository — discretion)

QuestBoard.Service/
├── Controllers/Events/
│   ├── EventsController.cs         # Cancel action, scope-aware Edit, PreviewSeries endpoint
│   └── SeriesController.cs         # NEW (or fold into EventsController — discretion): Details/End/Delete
├── Jobs/
│   └── RecurringOccurrenceTopUpJob.cs  # NEW — follows DailyReminderJob/HangfireJobHelper pattern
├── Views/Events/
│   ├── Create.cshtml               # repeats toggle + mask strip + preview panel (per UI-SPEC)
│   ├── Edit.cshtml                 # scope-prompt modal (D-09) for series occurrences
│   └── Details.cshtml              # cancelled banner, Cancel vs Delete, series link
├── Views/Series/ (or Views/Events/Series/)
│   └── Details.cshtml              # NEW series page (read-only rule, occurrence list, End/Delete)
├── Views/Shared/
│   └── _Calendar.cshtml            # cancelled chip modifier (guardrail: only Calendar/Index.cshtml call site)
├── Views/Calendar/
│   ├── Index.cshtml                # horizon banner (D-26), Legend row
│   └── Index.Mobile.cshtml         # cancelled agenda entry modifier

QuestBoard.UnitTests/
├── Services/
│   └── EventSeriesDateGeneratorTests.cs   # NEW — pure unit tests, no DB (mask correctness, mirrored masks)
├── Repository/
│   └── EventSeriesMaterializationTests.cs # NEW — InMemory DB, idempotency (double-run, cancel-then-run,
│                                             move-then-run, move-outside-runway-then-run)
├── Services/
│   └── RecurringOccurrenceTopUpJobTests.cs # NEW — mirrors DailyReminderJobTests.cs (mocked scope factory)

QuestBoard.IntegrationTests/Tests/
└── EventSeriesTenantIsolationTests.cs      # NEW — mirrors EventTenantIsolationTests.cs (two-group)
```

### Pattern 1: Pure date generator, consumed by two callers

**What:** A stateless method implementing exactly the D-01 arithmetic, taking `today` as a parameter (never reading `DateTime.Today` internally) so both the preview endpoint and the job can be tested deterministically.
**When to use:** Any time a candidate slot/date needs computing — preview, first-save materialization, and nightly top-up all call this same method.
**Example:**
```csharp
// QuestBoard.Domain/Services/EventSeriesDateGenerator.cs
// Source: derived directly from 76-CONTEXT.md D-01 (project-internal, not a library API)
namespace QuestBoard.Domain.Services;

public static class EventSeriesDateGenerator
{
    // Every slot has a date whether or not it fires; the mask decides which become events.
    // SeriesSlotIndex counts every step including non-firing ones -- this is what keeps a
    // moved occurrence's slot number stable even after the mask or a later slot changes.
    public static IEnumerable<(int SlotIndex, DateOnly Date, bool Fires)> GenerateSlots(
        DateOnly anchorDate,
        int intervalWeeks,
        IReadOnlyList<bool> cycleMask,
        DateOnly? endDate,
        int maxSlots)
    {
        for (var slot = 0; slot < maxSlots; slot++)
        {
            var date = anchorDate.AddDays(slot * intervalWeeks * 7);
            if (endDate.HasValue && date > endDate.Value)
            {
                yield break;
            }

            var fires = cycleMask[slot % cycleMask.Count];
            yield return (slot, date, fires);
        }
    }
}
```
Note: `maxSlots` is a caller-supplied bound (e.g. "enough slots to find N firing dates on/after today") — the generator itself has no concept of a runway count; that belongs in the materialization/orchestration layer, keeping this method trivially unit-testable.

### Pattern 2: Idempotent materialization with no date-scoped existence check

**What:** Load every existing `SeriesSlotIndex` for the series (no date filter — D-20), compute candidate firing slots via Pattern 1, subtract the existing set, keep only today-or-later dates (D-23), and materialize the remainder via the existing fan-out method.
**When to use:** Both the controller's first-save pass and the nightly top-up job call this.
**Example:**
```csharp
// QuestBoard.Domain/Services/EventSeriesService.cs (illustrative shape)
// Source: derived from 76-CONTEXT.md D-18/D-19/D-20/D-23/D-25/D-29/D-30
public async Task TopUpAsync(int seriesId, int runwayTarget, CancellationToken token)
{
    var series = await repository.GetSeriesAsync(seriesId, token);
    if (series.EndDate is { } end && DateOnly.FromDateTime(DateTime.Today) > end) return;

    // No date predicate here (D-20) -- a slot moved far outside any "window" must still
    // read as already-handled, or it gets recreated on the original date.
    var existingSlots = await repository.GetSlotIndexesForSeriesAsync(seriesId, token);

    var today = DateOnly.FromDateTime(DateTime.Today);
    var liveFutureCount = await repository.CountLiveFutureOccurrencesAsync(seriesId, today, token);

    var maxSlots = existingSlots.Count == 0 ? 0 : existingSlots.Max() + 1;
    while (liveFutureCount < runwayTarget)
    {
        var candidates = EventSeriesDateGenerator
            .GenerateSlots(series.AnchorDate, series.IntervalWeeks, series.CycleMask, series.EndDate, maxSlots + 200)
            .Where(c => c.Fires && !existingSlots.Contains(c.SlotIndex));

        var next = candidates.FirstOrDefault(c => c.Date >= today); // D-23
        if (next == default) break; // EndDate reached, or mask genuinely exhausted

        var newEvent = BuildEventFromTemplate(series, next.SlotIndex, next.Date);
        if (boardType == BoardType.Campaign)
        {
            await eventRepository.AddWithCampaignFanOutAsync(newEvent, memberIds, token); // D-29/D-30
        }
        else
        {
            await eventRepository.AddAsync(newEvent, token);
        }

        existingSlots.Add(next.SlotIndex);
        liveFutureCount++;
        maxSlots = next.SlotIndex + 1;

        // D-25: commit granularity — in the job path, each occurrence (with its fan-out) is its
        // own unit of work; a crash at N keeps N-1 and the retry adds the rest.
    }
}
```

### Pattern 3: Per-group Hangfire iteration (no `IgnoreQueryFilters`)

**What:** Enumerate real group ids via `GroupRepository` (whose `GroupEntity` carries no query filter — it is the tenant boundary itself, confirmed by `QuestBoardContext.OnModelCreating` having no `HasQueryFilter` for `GroupEntity`), then `SetGroupId()` per group before any series/event query.
**When to use:** `RecurringOccurrenceTopUpJob.ExecuteAsync`.
**Example:**
```csharp
// QuestBoard.Service/Jobs/RecurringOccurrenceTopUpJob.cs
// Source: pattern verified against QuestBoard.Service/Jobs/DailyReminderJob.cs and
// QuestBoard.Repository/GroupRepository.cs (GetAllWithMemberCountAsync has no query filter)
public async Task ExecuteAsync(CancellationToken token = default)
{
    // groupId: null here only to make the *first* scope call to enumerate groups -- this read
    // is on GroupEntity, which carries no HasQueryFilter, so it needs no override. Every
    // subsequent per-group operation below explicitly sets the group id before querying.
    var groups = await HangfireJobHelper.RunInScopeAsync(scopeFactory, groupId: null, async sp =>
    {
        var groupRepository = sp.GetRequiredService<IGroupRepository>();
        return await groupRepository.GetAllWithMemberCountAsync(token);
    });

    foreach (var group in groups)
    {
        await HangfireJobHelper.RunInScopeAsync(scopeFactory, group.Id, async sp =>
        {
            var seriesService = sp.GetRequiredService<IEventSeriesService>();
            var activeSeries = await seriesService.GetActiveSeriesForActiveGroupAsync(token);
            foreach (var series in activeSeries)
            {
                await seriesService.TopUpAsync(series.Id, runwayTarget: options.Value.RunwaySize, token);
            }
        });
    }
}
```
This mirrors `DailyReminderJob`'s shape exactly, with one structural difference: `DailyReminderJob` calls `RunInScopeAsync(scopeFactory, groupId: null, ...)` once and lets `IgnoreQueryFilters()` do the cross-group read inside `QuestRepository`. This job cannot do that because it writes — so it calls `RunInScopeAsync` once per group, each time with a real, non-null `groupId`.

### Pattern 4: Narrow scalar-update repository methods, not `BaseRepository.UpdateAsync`

**What:** `PlayerSignupRepository.ChangeVoteAsync` / `EventSignupRepository.SetAvailabilityAsync` load the tracked entity directly (`FirstOrDefaultAsync`, optionally with `.Include`), mutate specific fields, and `SaveChangesAsync` — rather than going through `BaseRepository.UpdateAsync`, which does `DbSet.FindAsync([model.Id])` + `Mapper.Map(model, entity)`.
**When to use:** The D-09 "this and all future events" template sweep (many rows, selective field overwrite, must skip rows that were separately moved/edited/cancelled) and the D-14 cancel write. `EventEntity` now carries a `Signups` navigation collection; `AutoMapper`'s `EntityProfile` already explicitly `.Ignore()`s `Signups`/`Group`/`Series` on the `Event → EventEntity` reverse map (confirmed at `EntityProfile.cs:143-146`), which is precisely why the *existing* `EventsController.Edit` action can safely call `eventService.UpdateAsync(existingEvent, token)` today (single-event, no navigation collision). A bulk sweep across N occurrences should still use dedicated, narrow repository methods rather than N calls into the generic update path, both for the skip-logic (D-09's "occurrences that were separately touched are skipped") and for round-trip efficiency.
**Example:** see `PlayerSignupRepository.ChangeVoteAsync` (`QuestBoard.Repository/PlayerSignupRepository.cs:43`) for the shape: `Include` what you need, mutate, single `SaveChangesAsync`.

### Anti-Patterns to Avoid

- **Reimplementing the date math in JavaScript for the live preview.** D-05 forbids this explicitly; PROJECT.md blames this duplication class for four recorded bugs. The preview endpoint must call the same Domain method the job/controller call.
- **Scoping the idempotency existence query to the runway date window.** D-20 names this exact optimization as the trap: a moved occurrence outside the window reads as "free" and regenerates.
- **`IgnoreQueryFilters()` anywhere in the job.** The one precedent (`GetQuestsForTomorrowAllGroupsAsync`) is read-only; this job writes and must use `SetGroupId()` per group instead (D-28).
- **Hard-deleting a cancelled occurrence.** Loses the idempotency answer ("was this slot ever handled?") and the availability history; D-14 requires a tombstone.
- **Enqueuing generation to Hangfire on save.** D-24 forbids this — `WorkerCount = 2` risks the DM landing on an empty calendar, a failure past 5 retries is invisible, and Hangfire is skipped entirely in the Testing environment (`Program.cs:266-272`), silently defeating integration tests of the "create with repeat" path.
- **Trusting client-rendered visibility for Cancel-vs-Delete.** D-16 requires the server to re-resolve `SeriesId` on the POST action itself, following the `QuestController.cs:762` precedent (board-type re-resolution, not trusting which button rendered).

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Recurrence rule expression | An RRULE parser/generator, even a minimal one | The custom D-01 pure generator | RFC 5545's `BYSETPOS` cannot express a cross-period on/off mask; a library still needs post-filtering, so it buys nothing and adds a dependency |
| Cross-group iteration for a background write | A raw SQL cross-group query, or `IgnoreQueryFilters()` + manual re-filtering | `GroupRepository.GetAllWithMemberCountAsync()` (unfiltered by design — `GroupEntity` has no query filter) + `SetGroupId()` per group | Matches the one locked convention in this codebase (D-28) and avoids re-deriving the "GroupEntity is the tenant boundary, not tenant-scoped" fact ad hoc |
| Per-occurrence campaign fan-out | A second signup-creation loop inside the generator | `IEventRepository.AddWithCampaignFanOutAsync` (already exists, already leaves `UpdatedAt` unset) | Already written for Phase 75 D-15; reimplementing risks silently stamping `UpdatedAt` and breaking Phase 77's `HasAnswered` distinction (D-29) |
| Bulk field update across many entities | Looping `BaseRepository.UpdateAsync` (which does one `FindAsync` + one `SaveChangesAsync` per call) | A dedicated narrow repository method that loads once, mutates in a loop, saves once | `BaseRepository.UpdateAsync`'s per-call `FindAsync`/`SaveChangesAsync` round trip does not compose across the D-09 "sweep all future untouched occurrences" operation, and offers no place to apply the skip-logic for separately-touched rows |

**Key insight:** every piece of infrastructure this phase needs — background job scaffolding, tenant isolation, campaign fan-out, narrow-write repository methods — was already built by Phases 55/74/75 specifically so this phase would not have to rebuild it. The actual net-new code is small: the pure generator, the idempotency/materialization orchestration around it, the migration, and the view/controller surface described in `76-UI-SPEC.md`.

## Common Pitfalls

### Pitfall 1: Believing "76 is pure code" (Phase 74 D-02's claim)

**What goes wrong:** Planning this phase as view/controller work only, discovering mid-implementation that `EventSeriesEntity` has no `Title`/`Description`/`StartTime`, no `EndDate`, and that `IX_Events_SeriesId` is not unique.
**Why it happens:** Phase 74 D-02 predicted this phase would need no schema work; that held for Phase 75 but not this one, and the gap was only found by reading the shipped schema (confirmed directly during this research — see `EventSeriesEntity.cs` and the `20260826134133_AddCalendarEventsFeature.cs` migration).
**How to avoid:** Plan the migration as Wave 0 work, before any view/controller task. It's a hard dependency for everything else.
**Warning signs:** Any task list for this phase that has no migration step.

### Pitfall 2: The stale `ActiveGroupContextService` doc comment

**What goes wrong:** A developer reads `ActiveGroupContextService.cs:19` ("Returns null when no override is set and HttpContext is absent — null means 'see all'") and assumes a null group id in a job context is safe or intentional.
**Why it happens:** The comment predates Phase 55's fail-closed filters and was never updated.
**How to avoid:** Fix the comment in this phase's diff (it directly touches this file's caller pattern via `HangfireJobHelper.RunInScopeAsync`). Every `HasQueryFilter` in `QuestBoardContext.cs` (confirmed lines 339-455) returns **zero** rows for a null `ActiveGroupId`, never all rows.
**Warning signs:** Any code comment or design note claiming a null group context is a "see everything" mode.

### Pitfall 3: Retry re-running a partially-failed generation from scratch

**What goes wrong:** The job creates occurrences 1-18 of a 20-occurrence top-up, then throws (e.g. a transient SQL timeout). Hangfire's global `AutomaticRetryAttribute { Attempts = 5 }` (`Program.cs:260`) re-runs `ExecuteAsync` from the top. Without D-18/D-19/D-20, occurrences 1-18 get recreated as duplicates.
**Why it happens:** The retry policy is registered globally and applies to every job, including this new one, with no per-job override in evidence anywhere in the codebase.
**How to avoid:** The idempotency check (existing-slot lookup with no date filter) must run before every single occurrence write, not once at the top of the method — so a mid-run crash and retry finds occurrences 1-18 already present and only creates 19-20.
**Warning signs:** A test that runs the generator once, asserts N occurrences, then does not also test running it a second time and asserting still-N.

### Pitfall 4: Scoping the top-up query to the runway window for "efficiency"

**What goes wrong:** A well-intentioned optimization — "only load occurrences within the next 20 slots' date range" — silently breaks EVTRECUR-07 the moment a DM drags an occurrence far outside that window (either far into the future beyond the runway, or backwards).
**Why it happens:** It looks like a reasonable index-friendly narrowing, and it works in every test that doesn't specifically move an occurrence a long distance.
**How to avoid:** D-20 is explicit and locked: load *all* slot indexes for the series, no date predicate. This is a small table per series (bounded by the runway plus whatever the DM has moved/cancelled), so the "optimization" buys nothing meaningful anyway.
**Warning signs:** Any `Where(e => e.Date >= someWindow)` clause feeding into the existence check.

### Pitfall 5: `FK_Events_EventSeries_SeriesId` has no cascade — deleting a series throws today

**What goes wrong:** Implementing D-12's "Delete" outcome as a naive `context.EventSeries.Remove(series); SaveChanges()` throws a foreign-key violation, because the shipped migration declares the FK with no `onDelete`, which EF Core defaults to `NO ACTION` for an optional relationship.
**Why it happens:** The FK was declared in Phase 74 before "delete a whole series" was a feature; nothing added a cascade because nothing needed one yet.
**How to avoid:** Both D-12 outcomes (Delete, Detach) must be written as deliberate multi-step operations: Delete removes the occurrences (and lets `FK_EventSignups_Events_EventId`'s existing cascade handle their signups) before or together with the series row; Detach nulls `SeriesId`/`SeriesSlotIndex` on every occurrence first. Do not rely on adding `onDelete: Cascade` to the migration as a substitute for Detach's semantics — Cascade would make *every* series removal behave like Delete, which is not what D-12 asks for.
**Warning signs:** A migration diff that changes `FK_Events_EventSeries_SeriesId` to `onDelete: Cascade` without a corresponding Detach code path that explicitly nulls the FK columns first.

### Pitfall 6: Forgetting a read surface when adding the cancelled marker

**What goes wrong:** D-14/D-15 require the cancelled state to render correctly on the desktop calendar chip, the mobile agenda entry, and the occurrence Details page. Missing one makes a cancelled session look live on that one surface — indistinguishable from a bug per D-15's own reasoning (echoing Phase 74 D-14).
**Why it happens:** Three separate view files (`_Calendar.cshtml`, `Index.Mobile.cshtml`, `Details.cshtml`) each independently render event data; there is no single "event row" component to update once.
**How to avoid:** Track all three as explicit, separately-verified tasks. `76-UI-SPEC.md` § "Calendar and agenda reads → cancelled filter" already names this as "the phase's most easily-forgotten surface."
**Warning signs:** A UAT pass that only checks the Details page for the cancelled banner.

### Pitfall 7: The five protected `_Calendar.cshtml` call sites

**What goes wrong:** Adding the D-15 cancelled-chip CSS class or the D-26 horizon banner directly inside `_Calendar.cshtml` (the shared partial) leaks that markup onto the 5 other call sites — the per-quest date-picker widget at `Views/Quest/Details.cshtml:604,648,696` and `Details.Mobile.cshtml:158,196` — which must render no series-specific markup at all (Phase 74 D-09).
**Why it happens:** The partial is genuinely shared; the natural place to "just add the class" is inside it.
**How to avoid:** The cancelled-chip CSS modifier is safe because those 5 call sites build their own `CalendarViewModel` and never populate `EventsOnDay` (confirmed: `_Calendar.cshtml`'s `@if (day.EventsOnDay.Any())` guard, empty-by-default). The D-26 horizon banner is **not** safe to place inside the partial at all — it must live in `Views/Calendar/Index.cshtml` only, per `76-UI-SPEC.md`'s explicit guardrail.
**Warning signs:** Any diff touching `_Calendar.cshtml` that adds a new `ViewBag` flag those 5 sites would need to opt out of, or that adds banner markup rather than a per-event CSS modifier.

## Code Examples

### The shipped schema this phase extends (verified by reading the files directly)

```csharp
// QuestBoard.Repository/Entities/EventSeriesEntity.cs — as shipped, before this phase's migration
// Source: direct file read, 2026-08-28
[Table("EventSeries")]
public class EventSeriesEntity : IEntity
{
    [Key] public int Id { get; set; }
    public DateOnly AnchorDate { get; set; }
    public int IntervalWeeks { get; set; }
    [Range(0, 6)] public int WeekDay { get; set; }         // 0 = Sunday, matches System.DayOfWeek
    [StringLength(200)] public string CycleMask { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int GroupId { get; set; }
    public virtual GroupEntity Group { get; set; } = null!;
    // No Title, Description, StartTime, EndDate -- D-08/D-11 add these.
}
```

```csharp
// QuestBoard.Repository/Migrations/20260826134133_AddCalendarEventsFeature.cs (excerpt) — confirms
// the non-unique index this phase must fix
migrationBuilder.CreateIndex(
    name: "IX_Events_SeriesId",
    table: "Events",
    column: "SeriesId");   // NOT unique -- D-19 adds a filtered unique index on (SeriesId, SeriesSlotIndex)
```

### The reusable fan-out method (do not reimplement)

```csharp
// QuestBoard.Repository/EventRepository.cs — as shipped
// Source: direct file read, 2026-08-28
public async Task AddWithCampaignFanOutAsync(Event newEvent, IEnumerable<int> memberIds, CancellationToken token = default)
{
    var entity = Mapper.Map<EventEntity>(newEvent);
    foreach (var memberId in memberIds.Distinct())
    {
        entity.Signups.Add(new EventSignupEntity { UserId = memberId, Availability = (int)VoteType.Yes });
        // Availability defaults to Yes; UpdatedAt is left at its default (null) -- this is the
        // "automatic pass, not a person" marker the generator's fan-out must preserve (D-29).
    }
    await DbSet.AddAsync(entity, token);
    await DbContext.SaveChangesAsync(token);
    newEvent.Id = entity.Id;
}
```

### The filtered unique index (EF Core Fluent API, verified against Microsoft's documented pattern)

```csharp
// In QuestBoardContext.OnModelCreating, alongside the existing EventEntity configuration
modelBuilder.Entity<EventEntity>()
    .HasIndex(e => new { e.SeriesId, e.SeriesSlotIndex })
    .IsUnique()
    .HasFilter("[SeriesId] IS NOT NULL")
    .HasDatabaseName("IX_Events_SeriesId_SeriesSlotIndex");
// Note: EF Core's SQL Server provider already applies an implicit "IS NOT NULL" filter to any
// nullable column participating in a unique index, by convention. The explicit HasFilter above
// is kept anyway to match D-19's exact locked wording and as defense in depth against a future
// EF Core convention change -- do not rely on the implicit behavior alone.
```
[CITED: learn.microsoft.com/en-us/ef/core/modeling/indexes — filtered unique index pattern confirmed via Microsoft Learn and community EF Core references, 2026-08-28]

### The per-group Hangfire iteration this job must follow (existing sibling job)

```csharp
// QuestBoard.Service/Jobs/DailyReminderJob.cs — as shipped
// Source: direct file read, 2026-08-28
public async Task ExecuteAsync(CancellationToken cancellationToken = default)
{
    var tomorrow = DateTime.Today.AddDays(1);
    await HangfireJobHelper.RunInScopeAsync(scopeFactory, groupId: null, async sp =>
    {
        var questRepository = sp.GetRequiredService<IQuestRepository>();
        var quests = await questRepository.GetQuestsForTomorrowAllGroupsAsync(tomorrow, cancellationToken);
        // ^ this repository method uses IgnoreQueryFilters() internally -- acceptable ONLY
        //   because it is read-only. The new recurring-occurrence job must NOT follow this
        //   shape; it needs SetGroupId() per group because it writes (D-28).
        ...
    });
}
```

```csharp
// QuestBoard.Service/Program.cs:355-358 — the registration pattern to follow, after ConfigureDatabase()
RecurringJob.AddOrUpdate<DailyReminderJob>(
    "daily-session-reminders",
    job => job.ExecuteAsync(CancellationToken.None),
    "0 9 * * *");
// D-27: register a sibling job at a distinct off-peak hour, separate id, same placement
// (after ConfigureDatabase so migrations have already run).
```

### Repository-level idempotency test pattern to follow

```csharp
// QuestBoard.UnitTests/Repository/EventSignupRepositoryTests.cs — the established shape
// Source: direct file read, 2026-08-28
private static QuestBoardContext CreateContext(string databaseName, IActiveGroupContext activeGroupContext)
{
    var options = new DbContextOptionsBuilder<QuestBoardContext>()
        .UseInMemoryDatabase(databaseName)
        .Options;
    return new QuestBoardContext(options, activeGroupContext);
}
// A MutableTestGroupContext (settable ActiveGroupId) plus this in-memory context is the
// established pattern for both idempotency tests (double-run, cancel-then-run, move-then-run)
// and cross-group isolation tests (seed group 2 with ActiveGroupId=null on the seeding
// context, then verify group 1's context cannot see it).
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|---------------|--------|
| N/A (no recurrence exists yet) | Slot-indexed cycle-mask generator, immutable cadence | This phase | First recurrence feature in the app; sets the idempotency-key convention (`(SeriesId, SlotIndex)`, never date) that any future recurrence-adjacent feature (e.g. Phase 77's grid, or a future EVTRECUR-09) must respect |
| `IX_Events_SeriesId` (non-unique) | Filtered unique `(SeriesId, SeriesSlotIndex)` | This phase's migration | Closes a real, currently-exploitable duplicate-row gap under Hangfire's existing 5-attempt retry policy |

**Deprecated/outdated:** The `EventSeriesEntity` class comment ("No code reads or writes it yet") stops being true this phase and must be rewritten as part of the diff — leaving it would be actively misleading to the next reader.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | EF Core's SQL Server provider auto-applies an `IS NOT NULL` filter to nullable-column unique indexes by convention (in addition to the explicit `HasFilter` this phase should still write) | Code Examples → filtered unique index | Low — the explicit `HasFilter` in the recommended snippet is correct and sufficient regardless of whether the implicit convention also applies; this is presented as helpful context, not a load-bearing claim the plan depends on |
| A2 | `GroupRepository.GetAllWithMemberCountAsync()` is a safe, adequate source for "all group ids" for the job's outer iteration, given `GroupEntity` carries no query filter | Architecture Patterns → Pattern 3 | Low-Medium — if the group count grows very large this becomes an O(groups) job; acceptable at this app's scale (a handful of boards), but the planner should confirm no pagination is silently applied to this existing method before relying on it for completeness |
| A3 | The Hangfire cron string format for an "off-peak hour" follows the same 5-field syntax already used for `daily-session-reminders` ("0 9 * * *") | Architecture Patterns → Pattern 3 / D-27 | Low — this is the exact syntax already proven working in this codebase; no new syntax is introduced |

**If this table is empty:** N/A — see entries above. All are low-risk; none require a locked user decision to be revisited, and each was cross-checked against a directly-read file or an authoritative external source rather than left as pure recollection.

## Open Questions

1. **Where does the series template live in the domain model — a new `EventSeries` domain model, or fields bolted onto `Event`?**
   - What we know: `EventSeriesEntity` currently has no domain-model counterpart at all (`IEventService`/`IEventRepository` only expose `GetSeriesGroupIdAsync`, a scalar lookup). D-08 requires `Title`/`Description`/`StartTime` on the entity as template fields.
   - What's unclear: Whether the planner introduces a full `EventSeries` domain model + repository + service triple (matching every other entity's shape in this codebase) or minimizes surface area by extending `IEventRepository`/`IEventService` directly, given `EventSeries` has exactly one consumer class of operations (create/end/delete/detach/preview/materialize).
   - Recommendation: Given the amount of series-specific orchestration this phase needs (generator, materializer, top-up, series page CRUD), a dedicated `IEventSeriesService`/`IEventSeriesRepository` pair matches the codebase's established one-service-per-entity convention better than overloading `IEventService`. Left as CONTEXT.md's own listed discretion item — this research does not override that.

2. **Exact shape of the "count live future occurrences" query for the D-21 runway check.**
   - What we know: The runway is measured in live (non-cancelled) future occurrences (D-21), and the horizon banner (D-26) needs the same or a closely related count per active series.
   - What's unclear: Whether one repository method serves both the top-up job's "do I need to generate more?" check and the calendar page's "should the banner show?" check, or whether they diverge (e.g. the banner might want to batch across all of a board's series in one query for page-load efficiency, while the job iterates series one at a time).
   - Recommendation: Design one `CountLiveFutureOccurrencesAsync(seriesId, today)` method for the job, and a second `GetSeriesRunningLowOnBoardAsync()` (returns the list of under-runway series titles) for the banner, since the banner's DM-facing copy needs series names, not just a boolean.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET SDK | Build/test the whole phase | ✓ | 10.0.400 [VERIFIED: `dotnet --version`] | — |
| Hangfire.AspNetCore / Hangfire.SqlServer | D-27 recurring job | ✓ | 1.8.23 [VERIFIED: QuestBoard.Service.csproj] | — |
| SQL Server (local, `localhost` per CLAUDE.md) | EF Core migration apply-on-startup | Assumed available per project convention — not re-probed this session (already required by every prior phase) | — | — |
| EF Core InMemory provider | Unit-level repository/idempotency tests | ✓ | pinned in `QuestBoard.UnitTests.csproj` [VERIFIED] | — |

**Missing dependencies with no fallback:** none identified.
**Missing dependencies with fallback:** none identified — everything this phase needs is already present and verified in-repo.

## Validation Architecture

### Test Framework

| Property | Value |
|----------|-------|
| Framework | xUnit v3 3.2.2 + FluentAssertions 8.10.0 + NSubstitute 5.3.0 + EF Core InMemory 10.0.9 [VERIFIED: QuestBoard.UnitTests.csproj] |
| Config file | none — convention-based project structure (`Repository/`, `Services/` folders) |
| Quick run command | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~EventSeries"` |
| Full suite command | `dotnet test` (runs `QuestBoard.UnitTests` and `QuestBoard.IntegrationTests` both, per `QuestBoard.slnx`) |

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| EVTRECUR-01 | Mask/cadence math produces correct dates for "two on, two off" and other patterns | unit | `dotnet test --filter EventSeriesDateGeneratorTests` | ❌ Wave 0 |
| EVTRECUR-02 | Preview endpoint returns ~10 dates using the same generator as materialization | unit + integration | `dotnet test --filter PreviewSeries` | ❌ Wave 0 |
| EVTRECUR-03 | Runway top-up creates occurrences up to the configured count, no more, no fewer | integration (InMemory DB) | `dotnet test --filter TopUpAsync` | ❌ Wave 0 |
| EVTRECUR-04 | Cancel tombstones the row; server rejects Delete on a series occurrence | integration | `dotnet test --filter Cancel` | ❌ Wave 0 |
| EVTRECUR-05 | Moving an occurrence does not get regenerated on the original date, even outside the runway window | integration | `dotnet test --filter MoveThenRun` | ❌ Wave 0 |
| EVTRECUR-06 | Editing one occurrence with "only this event" scope leaves siblings untouched; "this and future" sweeps untouched future rows only | integration | `dotnet test --filter EditScope` | ❌ Wave 0 |
| EVTRECUR-07 | Running the generator twice produces no duplicates; cancel-then-run does not resurrect; move-then-run does not duplicate | integration (InMemory DB, mirrors `EventSignupRepositoryTests` pattern) | `dotnet test --filter Idempotency` | ❌ Wave 0 |
| EVTRECUR-08 | Mirrored masks on two boards interleave with zero colliding dates | unit (pure generator, two mask instances) | `dotnet test --filter MirroredMask` | ❌ Wave 0 |
| (cross-cutting) | Two-group tenant isolation — series/occurrences from board B never visible/writable from board A's context | integration | `dotnet test --filter EventSeriesTenantIsolationTests` | ❌ Wave 0 |
| (cross-cutting) | Job iterates real groups, never `IgnoreQueryFilters()`, never touches another board's series | unit (mocked `IServiceScopeFactory`, mirrors `DailyReminderJobTests`) | `dotnet test --filter RecurringOccurrenceTopUpJobTests` | ❌ Wave 0 |

### Sampling Rate

- **Per task commit:** targeted `dotnet test --filter <area>` for the area just touched.
- **Per wave merge:** `dotnet test` (full suite — both `QuestBoard.UnitTests` and `QuestBoard.IntegrationTests`).
- **Phase gate:** Full suite green before `/gsd-verify-work`, plus a manual UAT pass covering all three cancelled-state read surfaces (Pitfall 6) and a real mobile User-Agent check on the agenda view (per `76-UI-SPEC.md`'s restated Phase 74 D-16 requirement).

### Wave 0 Gaps

- [ ] `QuestBoard.UnitTests/Services/EventSeriesDateGeneratorTests.cs` — pure mask/cadence math, mirrored-mask non-collision (EVTRECUR-08)
- [ ] `QuestBoard.UnitTests/Repository/EventSeriesMaterializationTests.cs` (or similar name) — idempotency: double-run, cancel-then-run, move-then-run, move-outside-runway-then-run
- [ ] `QuestBoard.UnitTests/Services/RecurringOccurrenceTopUpJobTests.cs` — mirrors `DailyReminderJobTests.cs`'s mocked-scope-factory pattern; must assert per-group `SetGroupId` calls, not a single cross-group call
- [ ] `QuestBoard.IntegrationTests/Tests/EventSeriesTenantIsolationTests.cs` — mirrors `EventTenantIsolationTests.cs`: series/occurrence visibility, create/edit/cancel rejection across boards
- [ ] No new test framework install needed — everything above builds on the already-configured xUnit v3 + InMemory EF Core stack

## Security Domain

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-------------------|
| V2 Authentication | No | Unchanged — existing ASP.NET Core Identity, no new auth surface |
| V3 Session Management | No | Unchanged |
| V4 Access Control | Yes | `[Authorize(Policy = "DungeonMasterOnly")]` on all series-mutating actions (Create-with-repeat, Edit, Cancel, End, Delete, Detach), matching the existing `EventsController` convention; server-side re-resolution of `SeriesId`/`GroupId` on every write (D-16's pattern), never trusting client-rendered button visibility |
| V5 Input Validation | Yes | Cycle mask length capped server-side independent of the UI's 24-position cap (schema ceiling ~100 via `nvarchar(200)`); `IntervalWeeks` must reject ≤0; anchor date, mask format validated on the same POST that writes the series row, not only client-side |
| V6 Cryptography | No | No new secrets, tokens, or crypto primitives introduced |

### Known Threat Patterns for this stack

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|----------------------|
| Cross-tenant series/occurrence access (posting a series id or event id belonging to another board) | Tampering / Information Disclosure | Fail-closed `HasQueryFilter` on `EventEntity`/`EventSeriesEntity` (already shipped) + explicit second-layer comparison in the controller (`SeriesIsOnActiveBoardAsync` pattern already shipped at `EventsController.cs:199`) — extend this same double-check to every new series action |
| Idempotency-key race under concurrent/retried writes producing duplicate rows | Tampering (data integrity) | D-19's filtered unique DB constraint as the backstop — the Domain-level existence check alone is not sufficient under Hangfire's 5-attempt automatic retry, which is exactly the check-then-insert race the index closes |
| Privilege bypass via markup-only hiding of the Delete button on a series occurrence | Elevation of Privilege | D-16 — server-side `SeriesId` re-resolution on the POST action itself, not merely which button the view rendered |
| Mass-count disclosure via delete-confirm dialog copy (D-13) revealing precise past/future/answer counts to a DM who might not otherwise know engagement levels | Information Disclosure (minor, in-tenant only) | Acceptable and intentional — the confirm is DM-only, same-board, and the count is the whole point of the confirm (making a destructive action's blast radius visible before committing) |

## Sources

### Primary (HIGH confidence — direct codebase reads, this session)

- `QuestBoard.Repository/Entities/EventSeriesEntity.cs`, `EventEntity.cs` — shipped schema
- `QuestBoard.Repository/Migrations/20260826134133_AddCalendarEventsFeature.cs` — confirms non-unique `IX_Events_SeriesId`, no `onDelete` on `FK_Events_EventSeries_SeriesId`
- `QuestBoard.Repository/Entities/QuestBoardContext.cs` (lines 330-455) — every `HasQueryFilter`, confirms fail-closed shape for `EventEntity`/`EventSeriesEntity`/`EventSignupEntity`
- `QuestBoard.Service/Services/ActiveGroupContextService.cs` — confirms the stale "null means see all" doc comment
- `QuestBoard.Service/Jobs/HangfireJobHelper.cs`, `DailyReminderJob.cs` — the pattern D-27/D-28 must follow
- `QuestBoard.Service/Program.cs` (lines 240-360) — `AutomaticRetryAttribute{Attempts=5}`, `WorkerCount=2`, Testing-environment Hangfire skip, `daily-session-reminders` registration
- `QuestBoard.Repository/QuestRepository.cs:265` — the `IgnoreQueryFilters()` precedent D-28 does not follow
- `QuestBoard.Service/Controllers/QuestBoard/QuestController.cs:740-778` — server-side re-resolution precedent (D-16)
- `QuestBoard.Domain/Interfaces/IEventService.cs`, `IEventRepository.cs`; `QuestBoard.Domain/Services/EventService.cs`; `QuestBoard.Repository/EventRepository.cs` — confirms `AddWithCampaignFanOutAsync`/`GetSeriesGroupIdAsync` already exist
- `QuestBoard.Service/Controllers/Events/EventsController.cs` — full current action set (Details/Create/Edit/Delete/SetAvailability/Withdraw)
- `QuestBoard.Repository/BaseRepository.cs`, `PlayerSignupRepository.cs`, `EventSignupRepository.cs` — narrow-scalar-update pattern vs. generic `UpdateAsync`
- `QuestBoard.Repository/GroupRepository.cs`, `IGroupRepository.cs` — confirms `GroupEntity` carries no query filter and `GetAllWithMemberCountAsync` is a safe cross-tenant enumeration source
- `QuestBoard.Repository/Automapper/EntityProfile.cs` (lines 138-158) — confirms `Signups`/`Group`/`Series` are explicitly ignored on the `Event → EventEntity` reverse map
- `QuestBoard.UnitTests/Repository/EventSignupRepositoryTests.cs`, `QuestBoard.UnitTests/Services/DailyReminderJobTests.cs` — established unit-test patterns to extend
- `QuestBoard.IntegrationTests/Tests/EventTenantIsolationTests.cs`, `QuestBoard.IntegrationTests/Helpers/MutableGroupContext.cs` — established two-group integration-test pattern to extend
- `.planning/config.json` — confirms `nyquist_validation: true`, no `security_enforcement: false` override, no external-tool config flags relevant to this phase
- `.planning/phases/76-recurring-event-series/76-CONTEXT.md`, `76-UI-SPEC.md`, `.planning/REQUIREMENTS.md`, `.planning/STATE.md` — phase scope, locked decisions, UI contract, requirement text

### Secondary (MEDIUM confidence)

- [Indexes - EF Core | Microsoft Learn](https://learn.microsoft.com/en-us/ef/core/modeling/indexes) — filtered unique index Fluent API syntax (`HasFilter`), and the SQL Server provider's implicit nullable-column filtering convention

### Tertiary (LOW confidence)

- None used as load-bearing claims. Community blog posts surfaced during the EF Core filtered-index search (e.g. `mousavi310.github.io`, `riptutorial.com`) corroborated the same syntax as Microsoft Learn but were not individually cited as the syntax is already confirmed by the primary source.

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — no new packages; every dependency version confirmed by direct `.csproj` read
- Architecture: HIGH — every pattern above is either read directly from shipped code or is a mechanical extension of a directly-read pattern (Hangfire job shape, repository narrow-update shape, integration-test tenant-isolation shape)
- Pitfalls: HIGH — each pitfall traces to a specific, directly-verified fact in the shipped schema, migration, or `Program.cs` (non-unique index, missing `onDelete`, global retry attribute, stale doc comment, protected `_Calendar.cshtml` call sites)

**Research date:** 2026-08-28
**Valid until:** 30 days (stable internal codebase, no external API surface; re-verify only if Phase 74/75 schema changes land before this phase executes)
