# Architecture Research — Calendar Events (v9.0)

**Domain:** ASP.NET Core 10 MVC feature integration into an existing 3-layer app
**Researched:** 2026-08-25
**Confidence:** HIGH (all findings verified against source in this repo; no external ecosystem research needed — this is a codebase-integration design, not a technology survey)

This is **not** a greenfield ecosystem survey. Quest Board already has a fixed 3-layer architecture, an established AutoMapper/entity/migration convention, and 10+ precedent features (Contacts, Characters, Quest, Shop) built the exact same way. This document designs Calendar Events to slot into that convention, cites every touch point with a real file path, and does not propose changing any existing pattern.

---

## 1. Entity Design

### Entities

```
EventSeriesEntity          (new)  — the recurrence rule + anchor
  └─< EventEntity            (new)  — one calendar occurrence (standalone OR series-generated)
        └─< EventSignupEntity  (new)  — one player's vote for one occurrence
```

**`EventSeriesEntity`** (table `EventSeries`)
| Column | Type | Notes |
|---|---|---|
| `Id` | `int` identity | PK |
| `GroupId` | `int` | FK → `Groups`, `NoAction` (matches `QuestEntity`/`ContactEntity` pattern — avoids cascade cycles through `Groups`) |
| `Title` | `nvarchar(200)` | template title copied onto each materialized occurrence |
| `Description` | `nvarchar(max)` nullable | template description (Markdown, same pipeline as Quest/Contact free text — see `RENDER-01/02/03` from v8.0) |
| `AnchorDate` | `datetime2` | the first occurrence's date; also the phase reference for `IntervalWeeks` math |
| `IntervalWeeks` | `int` | base cadence — every N weeks on `AnchorDate`'s weekday |
| `CycleMaskCsv` | `nvarchar(200)` | **see storage decision below** |
| `CreatedByUserId` | `int` | FK → `AspNetUsers` |
| `CreatedAt` | `datetime2` | |
| `IsActive` | `bool` | soft "stop generating new occurrences" flag — DM can deactivate a series without deleting already-materialized rows |

**`EventEntity`** (table `Events`)
| Column | Type | Notes |
|---|---|---|
| `Id` | `int` identity | PK |
| `GroupId` | `int` | FK → `Groups`, `NoAction`. **Denormalized** even though `EventSeriesId` also carries `GroupId` — same reasoning as `QuestEntity.GroupId` (not derived through a nullable parent), and required anyway because standalone (non-series) events have no series parent at all |
| `EventSeriesId` | `int?` | FK → `EventSeries`, nullable. Null = one-off event created directly (no recurrence) |
| `SeriesSlotIndex` | `int?` | **the idempotency key** — see "Idempotent materialization" below. Null for standalone events |
| `Title` | `nvarchar(200)` | copied from series at materialization time, then independently editable per-occurrence |
| `Description` | `nvarchar(max)` nullable | same — copied then independently editable |
| `Date` | `datetime2` | the actual occurrence date/time (may differ from the series-computed date if moved) |
| `Status` | `int` | enum: `Scheduled = 0`, `Cancelled = 1` (kept as a row, not deleted, so a cancelled occurrence still renders — greyed out — instead of silently vanishing from the calendar and confusing anyone who remembers seeing it) |
| `CreatedByUserId` | `int` | FK → `AspNetUsers` |
| `CreatedAt` | `datetime2` | |

**`EventSignupEntity`** (table `EventSignups`)
| Column | Type | Notes |
|---|---|---|
| `Id` | `int` identity | PK |
| `EventId` | `int` | FK → `Events`, `Cascade` (deleting an event should delete its signups — events have no cross-entity history value the way Quest signups do) |
| `PlayerId` | `int` | FK → `AspNetUsers` |
| `Vote` | `int` | reuses **existing** `QuestBoard.Domain.Enums.VoteType` { No=0, Maybe=1, Yes=2 } — do not create a parallel enum |
| `VoteChangeTime` | `datetime2?` | mirrors `PlayerSignupEntity.LastVoteChangeTime`'s "null = never changed since creation" convention |
| Unique constraint | `(EventId, PlayerId)` | one signup row per player per occurrence — enforced via an EF `HasIndex(...).IsUnique()`, mirroring `UserGroupEntity`'s implicit one-row-per-(User,Group) shape |

**Why no `EventSignupEntity` per-series row:** a series is a *rule*, not a roster. Signups only ever exist against a concrete `EventEntity` occurrence, exactly the way `PlayerSignupEntity` only ever exists against a concrete `QuestEntity`, never against some quest "template." This keeps auto-signup, waitlist-style vote counting, and the overview grid all reading from one table.

### Tenant scoping (`HasQueryFilter`)

Follow the fail-closed shape (`ActiveGroupId != null && ...`) locked in by Phase 55 (`QuestBoard.Repository/Entities/QuestBoardContext.cs:280-373`) — **not** the older Quest/ShopItem shape with the SuperAdmin null-bypass, which that same phase found to be the actual cross-tenant leak vector. Every new entity needs one:

```csharp
// EventSeriesEntity carries its own GroupId directly — same shape as QuestEntity/ContactEntity.
modelBuilder.Entity<EventSeriesEntity>()
    .HasQueryFilter(e =>
        activeGroupContext.ActiveGroupId != null &&
        e.GroupId == activeGroupContext.ActiveGroupId);

// EventEntity also carries its own GroupId directly (denormalized, not derived through the
// nullable EventSeriesId) so standalone events are scoped identically to series-generated ones.
modelBuilder.Entity<EventEntity>()
    .HasQueryFilter(e =>
        activeGroupContext.ActiveGroupId != null &&
        e.GroupId == activeGroupContext.ActiveGroupId);

// EventSignupEntity carries no GroupId of its own — scoped through its required Event
// navigation, same shape as PlayerSignupEntity → Quest (QuestBoardContext.cs:308-315).
modelBuilder.Entity<EventSignupEntity>()
    .HasQueryFilter(es =>
        activeGroupContext.ActiveGroupId != null &&
        es.Event.GroupId == activeGroupContext.ActiveGroupId);
```

Add the three `DbSet<>` properties to `QuestBoardContext` alongside the existing ones (`QuestBoardContext.cs:~39`), and the three `OnDelete(DeleteBehavior.NoAction)` / `Cascade` relationship configurations alongside the `Quest → Group`, `Contact → Group` block (`QuestBoardContext.cs:230-255`).

### Cycle mask storage — argued decision

The operator's spec: a repeating on/off cycle mask (e.g. `[on, on, off, off]`) layered on top of the base N-week cadence — meaning "every 2 weeks" picks candidate dates, and the mask then decides which of those candidate dates actually fire (e.g. run on weeks 1 and 2 of a 4-week cycle, skip weeks 3 and 4).

Four options considered:

| Option | Verdict | Why |
|---|---|---|
| **Delimited string** (`"1,1,0,0"` in an `nvarchar` column) | **Chosen** | Cycle length is small (realistically 2–8 slots for a biweekly/monthly-ish D&D game cadence), read once per materialization run (not queried inside SQL — no need for SQL-side filtering), and trivially parsed/validated in C# with `.Split(',').Select(s => s == "1")`. No schema migration needed to change cycle length (unlike a child table with a fixed slot count), no serialization ceremony (unlike JSON), and it's human-readable in the DB for support/debugging — a DM asking "why didn't my event fire this week" is answerable by eyeballing one column. |
| Child table (`EventSeriesCycleSlotEntity` with `SeriesId`, `SlotIndex`, `IsOn`) | Rejected | Correct normalized-relational answer, but massive overkill for a value that is never joined against, never filtered on in SQL, and always read as one atomic unit (the whole mask, in order) every time it's used. Adds a 4th new table, a 4th `HasQueryFilter`, and N rows of storage for what is functionally one small array. This is the "child table" version of over-engineering a config value. |
| SQL Server `JSON`/`nvarchar(max)` JSON column (`"[true,true,false,false]"`) | Rejected | SQL Server 2022 (this project's container image, `docker-compose.yml`) has `JSON_VALUE`/`OPENJSON` but this project does **zero** JSON-column querying anywhere else in the schema — introducing it for one field breaks convention for no payoff, since (like the child table) the mask is never queried inside SQL, only read into C# and iterated. |
| `int` bitmask (`CycleMask = 0b0011`) | Rejected | Most compact, but unlike the well-known `GroupRole`/`VoteType`/`BoardType` int-backed enums in this codebase (which have a small **fixed** set of named values), a cycle mask's bit *count* varies per series (2 slots vs. 8 slots) — an `int` caps it at 32 slots with no natural validation of "this bit is actually meaningful for this series's configured length," and it's opaque in the DB (a support query sees `13`, not `1,1,0,1`). The delimited string gives the same compactness for realistic cycle lengths with none of that opacity. |

**Chosen: `CycleMaskCsv nvarchar(200)`**, comma-separated `1`/`0` tokens, parsed by a small `EventSeries.GetCycleMask() : IReadOnlyList<bool>` helper on the **domain model** (not the entity — keep EF Core out of `QuestBoard.Domain`, per the existing layer boundary). `nvarchar(200)` comfortably covers any realistic cycle length (200 chars ≈ 100 slots) without needing a length migration later.

Materialization algorithm (domain-layer, e.g. in `EventSeriesService` or a small `EventOccurrenceGenerator` helper):
1. Walk candidate dates: `AnchorDate`, `AnchorDate + IntervalWeeks*1`, `AnchorDate + IntervalWeeks*2`, … up to the rolling window (~12 months out).
2. For candidate index `i`, `cycleIndex = i % mask.Count`. If `mask[cycleIndex] == false`, skip — no occurrence generated for that slot at all (not a Cancelled row; it simply never existed).
3. If `mask[cycleIndex] == true`, materialize (or confirm-already-materialized) an `EventEntity` with `SeriesSlotIndex = i`.

### Idempotent materialization — `SeriesSlotIndex`

`SeriesSlotIndex` is the sequential candidate-date index described above (0, 1, 2, 3, …), computed once from `(CandidateDate − AnchorDate) / (IntervalWeeks * 7 days)`, **not** derived from `Date` at query time (since `Date` can be moved by a DM edit — see below).

The materialization job's idempotency check per series, per run:
```
existingSlots = Events.Where(e => e.EventSeriesId == seriesId).Select(e => e.SeriesSlotIndex).ToHashSet();
for each slotIndex in the on-mask candidates within the rolling window:
    if slotIndex not in existingSlots: create EventEntity with SeriesSlotIndex = slotIndex, Date = computed date
    else: skip — already materialized (whether still Scheduled, or Cancelled, or Moved)
```

This is the same "already exists → skip, never duplicate" idempotency shape as `ReminderLogEntity` (`QuestBoard.Repository/Entities/ReminderLogEntity.cs`), which the existing `DailyReminderJob`/`SessionReminderJob` pair uses to guard against a Hangfire retry re-sending an already-sent reminder.

**Why this survives edits:** a DM moving an occurrence to a new date changes `EventEntity.Date` but never `SeriesSlotIndex`. A DM editing the title/description changes those columns but never `SeriesSlotIndex`. A DM cancelling sets `Status = Cancelled` but never touches `SeriesSlotIndex` or deletes the row. So on every subsequent materialization run, that slot is still found in `existingSlots` and is correctly never regenerated or duplicated — the slot index, not the date, is the series' bookkeeping key.

**What happens when the series rule itself is edited** (interval, mask, or anchor changed by a DM): see §4.

---

## 2. Domain Models + AutoMapper

### `QuestBoard.Domain/Models/QuestBoard/` (new files, alongside `Quest.cs`, `PlayerSignup.cs`)

```csharp
// EventSeries.cs
public class EventSeries : IModel
{
    public int Id { get; set; }
    public int GroupId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime AnchorDate { get; set; }
    public int IntervalWeeks { get; set; } = 1;
    public string CycleMaskCsv { get; set; } = "1"; // default: fires every interval, no skip pattern
    public int CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
    public IList<Event> Occurrences { get; set; } = [];

    public IReadOnlyList<bool> GetCycleMask() =>
        CycleMaskCsv.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim() == "1").ToList();
}

// Event.cs
public class Event : IModel
{
    public int Id { get; set; }
    public int GroupId { get; set; }
    public int? EventSeriesId { get; set; }
    public int? SeriesSlotIndex { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime Date { get; set; }
    public EventStatus Status { get; set; } = EventStatus.Scheduled;
    public int CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public IList<EventSignup> Signups { get; set; } = [];
}

// EventSignup.cs
public class EventSignup : IModel
{
    public int Id { get; set; }
    public required User Player { get; set; }
    public required Event Event { get; set; }
    public VoteType Vote { get; set; } = VoteType.No;
    public DateTime? VoteChangeTime { get; set; }
}
```

New enum `QuestBoard.Domain/Enums/EventStatus.cs`: `Scheduled = 0, Cancelled = 1` (mirrors the `int`-backed enum convention used by `CharacterStatus`, `GroupRole`, `VoteType`, `BoardType`).

### Service interfaces (`QuestBoard.Domain/Interfaces/`)

- **`IEventSeriesService`** — `CreateAsync`, `UpdateRuleAsync(int seriesId, ...)` (interval/mask/anchor edit — triggers regeneration per §4), `DeactivateAsync`.
- **`IEventService`** — `GetUpcomingForGroupAsync` (overview page + calendar), `GetEventsForCalendarAsync(...)` (mirrors `IQuestService.GetQuestsForCalendarAsync`, `QuestBoard.Domain/Services/QuestService.cs:55-58`), `CreateStandaloneAsync`, `CancelOccurrenceAsync`, `MoveOccurrenceAsync(int eventId, DateTime newDate)`, `EditOccurrenceAsync` (title/description, occurrence-only), `SignUpAsync(int eventId, int userId, VoteType vote)`.
- **`IEventMaterializationService`** (or a plain internal domain service, no interface needed if only the Hangfire job calls it — precedent: `DailyReminderJob` calls `IQuestRepository` directly, no dedicated "materialization service" interface exists for reminders either) — `MaterializeUpcomingOccurrencesAsync(int seriesId, DateTime windowEnd)`.

Repository interfaces: `IEventRepository`, `IEventSeriesRepository`, `IEventSignupRepository` in `QuestBoard.Domain/Interfaces/`, implementations `EventRepository`, `EventSeriesRepository`, `EventSignupRepository` in `QuestBoard.Repository/` (top-level, matching `PlayerSignupRepository.cs`'s location, not nested).

### `EntityProfile` additions (`QuestBoard.Repository/Automapper/EntityProfile.cs`)

Following the exact pattern already used for `PlayerSignup ↔ PlayerSignupEntity` (lines 42-50) and `Quest ↔ QuestEntity`'s shallow-nav-avoidance (lines 14-30):

```csharp
CreateMap<EventSeries, EventSeriesEntity>();
CreateMap<EventSeriesEntity, EventSeries>();

CreateMap<Event, EventEntity>()
    .ForMember(dest => dest.Status, opt => opt.MapFrom(src => (int)src.Status));
CreateMap<EventEntity, Event>()
    .ForMember(dest => dest.Status, opt => opt.MapFrom(src => (EventStatus)src.Status));

CreateMap<EventSignup, EventSignupEntity>()
    .ForMember(dest => dest.Event, opt => opt.Ignore())
    .ForMember(dest => dest.EventId, opt => opt.MapFrom(src => src.Event.Id))
    .ForMember(dest => dest.Player, opt => opt.Ignore())
    .ForMember(dest => dest.PlayerId, opt => opt.MapFrom(src => src.Player.Id))
    .ForMember(dest => dest.Vote, opt => opt.MapFrom(src => (int)src.Vote));
CreateMap<EventSignupEntity, EventSignup>()
    .ForMember(dest => dest.Vote, opt => opt.MapFrom(src => (VoteType)src.Vote));
```

### Does `EventSignup` hit the `PlayerSignupRepository.UpdateAsync` trap?

**Yes — same trap, same fix required.** `PlayerSignupRepository.UpdateAsync` (`QuestBoard.Repository/PlayerSignupRepository.cs:110-131`) overrides `BaseRepository<TModel,TEntity>.UpdateAsync` (`QuestBoard.Repository/BaseRepository.cs:63-69`) because the generic version does `Mapper.Map(model, entity)` — AutoMapper's default object-to-object map on a **tracked** entity overwrites navigation collections (`DateVotes`) wholesale, which for an EF-tracked entity means delete-and-reinsert-everything rather than a scoped update, and worse, can silently null out or blow away related rows that werenren't part of the incoming model graph.

`EventEntity` has an analogous shape: it's typically loaded with `.Include(e => e.Signups)` for the overview-grid and vote-recording paths, and a naive `Mapper.Map(model, entity)` call on a "recompute the whole event's data" path would clobber `Signups` the same way `PlayerSignupEntity.DateVotes` gets clobbered. The concrete places this bites:

- **`EventRepository`** does **not** need a custom `UpdateAsync` override for simple field edits (title/description/date/status) *if* those updates are done via a dedicated repository method that only touches scalar columns — same shape as `PlayerSignupRepository.ChangeVoteAsync` (lines 32-77), which deliberately does **not** call the generic `UpdateAsync`/`Mapper.Map` at all for exactly this reason.
- **Recommendation:** give `EventRepository` explicit narrow methods — `MoveOccurrenceAsync(eventId, newDate)`, `EditOccurrenceAsync(eventId, title, description)`, `CancelOccurrenceAsync(eventId)` — each doing scalar-only `entity.X = value; SaveChangesAsync()`, and **never** call the generic `UpdateAsync` on an `EventEntity` that has `Signups` loaded. This sidesteps the trap entirely rather than needing a full override like `PlayerSignupRepository.UpdateAsync` did (that override exists specifically because `PlayerSignup.DateVotes` genuinely needs bulk replace-semantics on vote update; `Event`'s signups are updated one row at a time via `EventSignupRepository`, so the trap is avoidable by not going through generic `UpdateAsync` at all).
- **`EventSignupRepository`** needs its own `RecordVoteAsync(eventId, userId, VoteType vote)` doing a scalar `Vote`/`VoteChangeTime` update on the existing row (or insert if none exists) — mirroring `PlayerSignupRepository.ChangeVoteAsync`'s shape, never routed through generic `UpdateAsync`.

---

## 3. Calendar Integration — Blast Radius

### Verified current shape

`CalendarViewModel` (`QuestBoard.Service/ViewModels/CalendarViewModels/CalendarViewModel.cs`) holds `List<Quest> Quests` and a `GetCalendarDays()` method that builds `List<CalendarDay>`, each with `List<QuestOnDay> QuestsOnDay` (`CalendarDay.cs`). `_Calendar.cshtml` and `_Calendar.Mobile.cshtml` both iterate `Model.GetCalendarDays()` and render `day.QuestsOnDay`.

**Verified call sites of `_Calendar`/`_Calendar.Mobile` (confirmed by direct file read, not the operator's count alone):**

| # | File | Line(s) | Context | Has events context available? |
|---|---|---|---|---|
| 1 | `Views/Calendar/Index.cshtml` | 32 | Full-month desktop calendar, `ViewBag.IsDetailsPage = false` | Yes — this is exactly where "Calendar views" (the operator's scope) means events must appear |
| 2 | `Views/Quest/Details.cshtml` | 482 | quest-not-yet-signed-up vote flow | No — quest-scoped context only |
| 3 | `Views/Quest/Details.cshtml` | 526 | already-signed-up, non-update-mode | No |
| 4 | `Views/Quest/Details.cshtml` | 574 | update-vote-mode | No |
| 5 | `Views/Quest/Details.Mobile.cshtml` | 144 | update-vote mode | No |
| 6 | `Views/Quest/Details.Mobile.cshtml` | 182 | initial signup mode | No |

This matches the operator's "6 sites" exactly (1 + 3 + 2).

**One additional consumer the operator's count did not include, but that must not be missed:** `Views/Calendar/Index.Mobile.cshtml` (confirmed by direct read) does **not** call the `_Calendar` partial at all — it independently calls `Model.GetCalendarDays()` (line 9: `var agendaDays = Model.GetCalendarDays().Where(...)`) and hand-renders its own agenda-list markup (lines 45-66), iterating `day.QuestsOnDay` directly. This is a **7th touch point** and, since it is the mobile month/agenda view — i.e. exactly the "calendar views" surface events must appear on — it is **not optional scope**: skipping it would leave events visible on desktop calendar but invisible on mobile calendar, a direct regression against the feature's own requirement.

### Why the 5 Quest/Details call sites have no events context, and why that's fine

`QuestController.Details` (confirmed via grep, `QuestController.cs:344-360`) builds `monthsWithProposedDates` — a `List<CalendarViewModel>`, one per month that has a proposed date for *this specific quest* — purely to give the DateVotes calendar grid its month scaffolding. It's a **vote-widget calendar**, not a "here's what's happening this month" calendar. It never had all-events-in-month semantics even for Quests (it always used `allQuests` — every quest, not just this month's — so `_Calendar.cshtml`'s day-bucketing does the actual per-day filtering). Injecting Events into these 5 render passes would put irrelevant campaign-availability data into a per-quest date-voting UI with no product reason to be there, and — per the operator's own scope line — **"Never appears on the quest board main page; only on calendar views."** `Quest/Details` is not a calendar view; it's a quest page that happens to reuse the calendar-grid partial as a date-picker widget. Events must **not** flow into these 5 sites.

### Minimal-blast-radius design

**Do not touch `CalendarViewModel`'s existing `Quests`/`GetCalendarDays()` shape, and do not touch `CalendarDay.QuestsOnDay`.** Both must stay exactly as-is because the 5 Quest/Details call sites bind against them today with zero events awareness, and changing their shape would force every one of those 5 sites to reason about events they don't have and shouldn't render.

Instead, **add an optional, additive `Events` list to `CalendarViewModel`** and a parallel `EventsOnDay` list to `CalendarDay`, defaulting to empty:

```csharp
// CalendarViewModel.cs — additive only
public List<Event> Events { get; set; } = new();   // new — empty by default

// GetCalendarDays() — existing method, one line added inside the day-building loop:
days.Add(new CalendarDay
{
    Date = date,
    Day = day,
    QuestsOnDay = questsOnDay,
    EventsOnDay = GetEventsForDate(date)   // new — returns [] when Events is empty, i.e. every
                                            // existing caller that never sets Events gets [] for free
});

private List<Event> GetEventsForDate(DateTime date) =>
    Events.Where(e => e.Status != EventStatus.Cancelled && e.Date.Date == date.Date).ToList();
    // Cancelled occurrences are deliberately excluded from calendar rendering, not hidden via CSS —
    // greyed-out-but-visible was considered and rejected: a monthly view with dozens of skipped/
    // cancelled slots would be noisier than useful. (Overview page still lists cancellations — §6.)
```

```csharp
// CalendarDay.cs — additive only
public List<Event> EventsOnDay { get; set; } = [];   // new
```

Because both new properties default to empty collections, **the 5 Quest/Details call sites require zero code changes** — `CalendarController` and `QuestController.Details` simply never populate `Events`, so `EventsOnDay` is always `[]` for those render passes, and the partials render nothing extra for them.

**`_Calendar.cshtml`** (`QuestBoard.Service/Views/Shared/_Calendar.cshtml`) gains a new block inside the existing `@foreach (var day in Model.GetCalendarDays())` loop, alongside the existing `@if (day.QuestsOnDay.Any())` block (around line 31) — a parallel `@if (day.EventsOnDay.Any())` rendering a distinct, visually simpler event chip (title + time, no vote buttons/waitlist/key-warning machinery — events have no seats/keys/finalization state). Reuse the existing `.quest-event`/`.calendar-quest-title` CSS class *shapes* but under a new `.calendar-event`/`event-event` class name so events are stylistically distinguishable (a different accent color, e.g. purple/info vs. the existing gold/finalized-green) without touching any Quest CSS rule.

**`_Calendar.Mobile.cshtml`** (`QuestBoard.Service/Views/Shared/_Calendar.Mobile.cshtml`) — note this file's *current* content is entirely about the vote-radio-button widget for the quest-details date picker (it does not render a generic day-by-day quest list at all; that's what `Calendar/Index.Mobile.cshtml`'s own agenda code does, independently). Since Events never flow into `Quest/Details` (the only caller of `_Calendar.Mobile`), **`_Calendar.Mobile.cshtml` needs no changes at all** for Events. This is a deliberate, verified conclusion, not an oversight — its only 2 call sites are both in the "no events context" group above.

**`Calendar/Index.Mobile.cshtml`** (the 7th touch point) needs its own additive change: alongside `var agendaDays = Model.GetCalendarDays().Where(d => !d.IsEmpty && d.QuestsOnDay.Any())`, broaden the predicate to `.Where(d => !d.IsEmpty && (d.QuestsOnDay.Any() || d.EventsOnDay.Any()))`, and add an `@foreach (var eventOnDay in day.EventsOnDay)` block parallel to the existing quest-entry loop (lines 53-64), rendering an `.agenda-event-entry` card.

### Populating `Events` — controller changes

**`CalendarController.Index`** (`QuestBoard.Service/Controllers/QuestBoard/CalendarController.cs:11-43`) is the only controller that needs a new data source wired in, since it's the only genuine "calendar view" entry point (Quest/Details is out of scope per above):

```csharp
var allEvents = await eventService.GetEventsForCalendarAsync(token);   // new IEventService call
var calendarModel = new CalendarViewModel
{
    Year = selectedYear,
    Month = selectedMonth,
    Quests = [.. allQuests],
    Events = [.. allEvents]   // new
};
```

This requires `CalendarController`'s constructor to take an additional `IEventService eventService` parameter (constructor injection, matching the existing primary-constructor style: `CalendarController(IQuestService questService, IEventService eventService)`).

`GetEventsForCalendarAsync` on `IEventService`/`EventService` should mirror `IQuestService.GetQuestsForCalendarAsync` (`QuestBoard.Domain/Services/QuestService.cs:55-58`) exactly — return all non-cancelled `Event`s for the active group, no month filtering server-side (the existing `Quest` equivalent doesn't month-filter either; `GetCalendarDays()` does the day-bucketing client-side-in-C#, and Events should follow the identical shape for consistency).

### Exhaustive file list for this section

| File | Change |
|---|---|
| `QuestBoard.Service/ViewModels/CalendarViewModels/CalendarViewModel.cs` | Modified — add `Events` list, extend `GetCalendarDays()` |
| `QuestBoard.Service/ViewModels/CalendarViewModels/CalendarDay.cs` | Modified — add `EventsOnDay` |
| `QuestBoard.Service/Controllers/QuestBoard/CalendarController.cs` | Modified — inject `IEventService`, populate `Events` |
| `QuestBoard.Service/Views/Shared/_Calendar.cshtml` | Modified — render `EventsOnDay` block |
| `QuestBoard.Service/Views/Calendar/Index.Mobile.cshtml` | Modified — include events in agenda predicate + render loop |
| `QuestBoard.Service/Views/Shared/_Calendar.Mobile.cshtml` | **No change** (verified: only reached from Quest/Details, which never sets `Events`) |
| `QuestBoard.Service/Views/Quest/Details.cshtml` | **No change** (verified: 3 call sites, all vote-widget context, `Events` stays empty) |
| `QuestBoard.Service/Views/Quest/Details.Mobile.cshtml` | **No change** (verified: 2 call sites, same reasoning) |
| `QuestBoard.Service/Views/Calendar/Index.cshtml` | **No change needed** — `ViewBag.IsDetailsPage = false` already set; partial handles the rest |
| `QuestBoard.Service/wwwroot/css/calendar.css` (or equivalent) | Modified — new `.calendar-event` styling |

---

## 4. Hangfire Materialization Job

### Pattern precedent

Copy `DailyReminderJob` (`QuestBoard.Service/Jobs/DailyReminderJob.cs`) verbatim in shape: a plain constructor-injected class (not `[AutomaticRetry]`-decorated itself — that's applied globally per Phase 34.2's "global Hangfire `AutomaticRetryAttribute` retry policy," confirmed in the v9.0 shipped-state notes), calling `HangfireJobHelper.RunInScopeAsync` (`QuestBoard.Service/Jobs/HangfireJobHelper.cs`) to get a scoped `IServiceProvider`.

```csharp
// QuestBoard.Service/Jobs/EventMaterializationJob.cs — new
public class EventMaterializationJob(
    IServiceScopeFactory scopeFactory,
    ILogger<EventMaterializationJob> logger)
{
    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        await HangfireJobHelper.RunInScopeAsync(scopeFactory, groupId: null, async sp =>
        {
            var seriesRepository = sp.GetRequiredService<IEventSeriesRepository>();
            var eventRepository = sp.GetRequiredService<IEventRepository>();

            // Cross-group sweep — groupId: null leaves ActiveGroupContextService untouched,
            // same pattern DailyReminderJob uses via GetQuestsForTomorrowAllGroupsAsync.
            // A dedicated "all active series across all groups" repository method is required
            // (see IEventSeriesRepository.GetAllActiveSeriesAllGroupsAsync below) because the
            // normal HasQueryFilter on EventSeriesEntity would otherwise silently return zero
            // rows when ActiveGroupId is null.
            var activeSeries = await seriesRepository.GetAllActiveSeriesAllGroupsAsync(cancellationToken);

            foreach (var series in activeSeries)
            {
                await MaterializeSeriesAsync(series, eventRepository, cancellationToken);
                logger.LogInformation(
                    "EventMaterializationJob: materialized occurrences for series {SeriesId} up to rolling window.",
                    series.Id);
            }
        });
    }
}
```

**Critical detail carried over from `DailyReminderJob`'s own pattern:** `GetQuestsForTomorrowAllGroupsAsync` exists specifically because the normal `HasQueryFilter` returns nothing when `ActiveGroupId` is null (a cross-group sweep job never sets a group). The Events materialization job needs the identical shape: `IEventSeriesRepository` needs an explicit `GetAllActiveSeriesAllGroupsAsync` method that either uses `IgnoreQueryFilters()` or is scoped per-group in a loop (`HangfireJobHelper.RunInScopeAsync(scopeFactory, groupId: g.Id, ...)` once per group, mirroring how `QuestFinalizedEmailJob`/`SessionReminderJob` are invoked **per quest's `GroupId`** from `DailyReminderJob`'s own foreach at line 34-37, rather than one giant cross-group query). **Recommend the per-group-scope-loop shape** (not `IgnoreQueryFilters()`) since it's the exact pattern this codebase already uses everywhere else and avoids introducing a new bypass mechanism the Phase 55 security work deliberately closed off elsewhere.

### Registration (`Program.cs`, alongside the existing `RecurringJob.AddOrUpdate<DailyReminderJob>` block at lines 355-358)

```csharp
RecurringJob.AddOrUpdate<EventMaterializationJob>(
    "event-occurrence-materialization",
    job => job.ExecuteAsync(CancellationToken.None),
    "0 3 * * *");   // once daily, off-peak — no product reason to run more than daily; a
                     // 12-month rolling window has enormous slack, unlike the reminder job's
                     // tight next-day deadline
```

Placed in the same `if (!app.Environment.IsEnvironment("Testing"))` block, after `ConfigureDatabase()`, for the same reason documented at `Program.cs:353-354` — migrations must run before the job can query the new tables.

### Idempotency across runs

Already covered in §1 — `SeriesSlotIndex` is looked up per run and never regenerated once present. This job is safe to run more than once a day, safe to retry (global `AutomaticRetryAttribute`), and safe to run against a series with zero prior occurrences (cold start) or thousands (steady state, since only the "top up to +12 months" delta gets created each run).

### Series rule edited — what regenerates, what's preserved

When a DM edits an `EventSeries`'s `IntervalWeeks`, `CycleMaskCsv`, or `AnchorDate` via `IEventSeriesService.UpdateRuleAsync`:

- **Preserved unconditionally:** every existing `EventEntity` row where the DM has taken an explicit per-occurrence action — `Status == Cancelled`, or a `Date` that no longer matches what the *original* rule would have computed for that `SeriesSlotIndex` (i.e., it's been moved), or `Title`/`Description` diverged from the series template. These are **never** touched by a rule edit or by the recurring job, because the job only ever *adds* new slots it hasn't seen — it has no delete/update path for existing rows at all (§1's algorithm is strictly additive).
- **What actually changes on a rule edit:** only **future, never-yet-passed, untouched** occurrences are affected, and only by the *next materialization run*, not synchronously by the edit itself. Two supported approaches, pick one at plan time:
  1. **(Recommended, simplest, safest)** A rule edit does **not** retroactively touch any already-materialized `EventEntity` row, touched or not. It only changes what the *next* job run computes as candidate slots going forward from `DateTime.Now`. This means a DM changing the cadence from biweekly to weekly won't "fix" already-generated future biweekly occurrences that haven't happened yet — those stay as originally generated. This is the same "changes apply going forward, not retroactively" semantic every recurring-scheduler product (Google Calendar's "this and following events," GitHub Actions' cron) uses to avoid silently deleting/rewriting rows a user might already be relying on (a signed-up player's vote on a future occurrence that suddenly disappears is a worse UX than a stale cadence for a few more weeks).
  2. **(More aggressive, more complexity, not recommended for the first phase)** Delete-and-regenerate all *future, untouched* (`Status == Scheduled` AND `Date` still equals the rule-computed date for its slot AND no signups exist) occurrences, then re-run materialization. This requires a "has anyone signed up or touched this row" guard before any delete, which is exactly the kind of judgment call that should be scoped explicitly rather than defaulted into — flag as a Phase 76+ discussion point, not a Phase 74/75 default.

**Recommendation: implement option 1 only** for the initial phase split (§8). It requires zero extra logic beyond "the job only adds, never deletes/updates" — which is already what §1's core algorithm does. Option 2 is a real future enhancement, not a blocker for shipping.

---

## 5. Auto-Signup on Campaign Boards

**Where it happens: at materialization time**, not at first render and not lazily. Rationale:

| Option | Consequence |
|---|---|
| **At materialization time (recommended)** | The Hangfire job, immediately after creating a new `EventEntity`, queries the group's current Campaign-board membership (`IGroupService.GetMembersAsync(groupId)`, confirmed interface at `QuestBoard.Domain/Interfaces/IGroupService.cs:38`) and bulk-inserts one `EventSignupEntity` per member with `Vote = VoteType.Yes`. This makes the overview page (§6) and the calendar both trivially correct on first read — no "compute membership on the fly" logic needed anywhere else, and the N+1-avoidance query shape in §6 stays simple (it can `Include(e => e.Signups)` and just trust the rows are there). |
| At first render (lazy, on-demand insert) | Requires every read path (calendar, overview, quest... wait, event details) to first check "does this event have signups for every current member?" and backfill before rendering — duplicates the membership-diffing logic across every read path, and risks a race if two requests hit the same stale event simultaneously (double-insert unless also uniqued, which the schema already is — but it's needless complexity for zero benefit over doing it once at materialization). |
| Fully lazy (never backfilled, computed virtually) | Means `EventSignupEntity` rows don't reliably exist for every member — breaks the "auto-signup" requirement's spirit (a player should see themselves as Yes without having to do anything, including view the page) and makes the overview grid's query shape (§6) much harder, since "no row" would have to be interpreted as an implicit Yes for Campaign boards but an implicit "not signed up" for OneShot boards — a landmine for whoever reads that query next. |

### New member joins after occurrences already exist

Materialization only fires new rows for *new occurrences*; it does not re-scan existing occurrences for newly-added members. So a player added to a Campaign group **after** some occurrences already have their signup rows will be missing from those existing occurrences' rosters (no `EventSignupEntity` row at all for them), while automatically appearing (via the next `EventMaterializationJob` run) on any newly-materialized future occurrence.

**Recommendation:** extend the *existing* group-join flow (`IGroupService.AddMemberAsync`, `QuestBoard.Domain/Interfaces/IGroupService.cs:27`, called from `GroupController.AddMember`/`AdminController.CreateUser`/`UserService.CreateOrAddToGroupAsync` per the v9.0 shipped-state history) to also backfill `EventSignupEntity(Vote = Yes)` rows for every future (`Date >= DateTime.UtcNow`), non-cancelled `EventEntity` in that group, **but only if `BoardType == Campaign`** (checked via the same `IBoardTypeResolver` used elsewhere, per the v6.0 Phase 37 precedent). This is a small, additive, well-scoped change to one already-shared join path rather than a new mechanism — flag as its own plan-level task rather than folding it silently into the materialization job, since it's triggered by a completely different event (a group-membership write, not a Hangfire tick).

---

## 6. Overview Page

**New controller, not an action bolted onto `CalendarController`.** `CalendarController` is scoped to the "date-grid" rendering concern (`GetCalendarDays()`); the availability overview is a fundamentally different shape (rows = events, columns = members, cells = vote), matching this codebase's existing "one controller per distinct page-family" convention (`ContactsController`, `CharactersController`, `PlayersController` are all separate despite all being "who's in this group" variants). Precedent controller shape: `ContactsController` (`QuestBoard.Service/Controllers/Contacts/ContactsController.cs`) — `[Authorize]` at class level, `[Authorize(Policy = "DungeonMasterOnly")]` on the mutating actions only.

**New file:** `QuestBoard.Service/Controllers/Events/EventsController.cs` (or `QuestBoard.Service/Controllers/QuestBoard/EventsController.cs` if events stay under the `QuestBoard` controller-folder convention like `CalendarController`/`QuestController` — either is consistent with existing precedent; `Contacts` and `Characters` both got their own top-level folder when they became distinct enough features, so `Events/` matches that more recent precedent better).

Actions needed: `Index` (calendar-style Create/browse), `Create`/`CreateSeries` (DM-tier), `Details` (occurrence detail + own-vote widget, all authenticated users), `Move`/`Cancel`/`Edit` (DM-tier, per-occurrence), and a distinct **`Overview`** action for the operator's requested grid page.

### Authorization

**`[Authorize(Policy = "DungeonMasterOnly")]`** on event/series creation and occurrence mutation — matches the operator's decision ("Event creation available to all DM roles"), and this policy (`DungeonMasterOnly` = `GroupRole.DungeonMaster` or `GroupRole.Admin`, or SuperAdmin — confirmed in `.planning/codebase/ARCHITECTURE.md`'s Authorization section) is exactly "all DM roles."

**`[Authorize]`** only (any authenticated group member) on `Overview` and `Details`/vote-recording — any player needs to see the grid and cast their own vote, same as any player can vote on a Quest's proposed dates without being a DM.

### N+1-safe query shape for the overview grid

The naive version (`foreach event { foreach member { query signup } }`) is the anti-pattern. The correct shape, one round-trip:

```csharp
// EventRepository (or a dedicated overview repository method)
var upcomingEvents = await DbSet
    .Where(e => e.Status != (int)EventStatus.Cancelled && e.Date >= DateTime.UtcNow)
    .OrderBy(e => e.Date)
    .Include(e => e.Signups)
        .ThenInclude(s => s.Player)   // one JOIN, not N queries
    .ToListAsync(cancellationToken);

var members = await groupRepository.GetMembersAsync(groupId);   // one query, existing method
```

The controller/service then builds the grid in memory: `events x members`, looking up each cell's vote from the already-loaded `event.Signups` collection (an in-memory `Dictionary<(int eventId, int userId), VoteType>` lookup built once from the flattened signup list) — zero additional DB round-trips per cell. This mirrors the same "load once, cross-reference in memory" shape already used by `_Calendar.cshtml`'s `questOnDay.ProposedDate.PlayerVotes?.FirstOrDefault(v => v.PlayerSignup?.Player?.Id == currentUserId)` pattern (confirmed at `_Calendar.cshtml:74`), just generalized from "one column" (current user) to "every member column."

**Tenant scoping is free here** — `EventEntity`'s `HasQueryFilter` (§1) already restricts `upcomingEvents` to the active group; `groupRepository.GetMembersAsync(groupId)` is explicitly parameterized (not filter-derived), matching the existing `GetAvailableUsers`/`GetMembersAsync` precedent noted in the v9.0 shipped-state history (Phase 40).

---

## 7. Migrations

Four migrations, in this exact dependency order (each auto-applies on startup via `context.Database.Migrate()` — a bad one blocks boot for every subsequent request until fixed, so keep each one small and independently reversible):

| # | Migration | Creates | Depends on |
|---|---|---|---|
| 1 | `AddEventSeries` | `EventSeries` table (no FKs to anything new — only `GroupId → Groups`, `CreatedByUserId → AspNetUsers`) | Nothing new |
| 2 | `AddEvents` | `Events` table (`GroupId → Groups`, `EventSeriesId → EventSeries` nullable, `CreatedByUserId → AspNetUsers`) + `IX_Events_GroupId`, `IX_Events_EventSeriesId`, `IX_Events_Date` | #1 |
| 3 | `AddEventSignups` | `EventSignups` table (`EventId → Events` cascade, `PlayerId → AspNetUsers`) + unique index `IX_EventSignups_EventId_PlayerId` | #2 |
| 4 | (optional, only if not folded into #1) `AddCycleMaskToEventSeries` | Adds `CycleMaskCsv` column if the team wants to ship #1 without it first (unlikely — recommend just including it in #1 since the whole feature is additive and this isn't a backfill scenario) | #1 |

Realistically, **migrations 1-3 can be a single migration** (`AddCalendarEventsFeature`) exactly matching the `AddContactsFeature` precedent (`QuestBoard.Repository/Migrations/20260706193921_AddContactsFeature.cs`, verified above) — that one migration created `Contacts`, `ContactImages`, and `ContactNotes` (3 tables + all FKs + all indexes) in one atomic `Up()`. Follow that same shape: **one migration, three `CreateTable` calls, in dependency order within the same `Up()` method** (`EventSeries` → `Events` → `EventSignups`), named `yyyyMMddHHmmss_AddCalendarEventsFeature` per the existing timestamp-prefixed naming convention (confirmed via `QuestBoard.Repository/Migrations/` listing — most recent: `20260707111803_RenameImageColumnsAddCropped`).

Boot-safety note specific to this feature: since every new table and column is purely additive (no existing table altered, no non-nullable column added to an existing populated table), there is no backfill-value risk the way `AddGroupIdToCharacters` (`20260705183646`) had to handle (that one had to default existing rows to `GroupId = 1`). This migration can be pure `CreateTable` calls — the lowest-risk migration shape this codebase has.

---

## 8. Phase Split

The feature spans 4 architectural concerns that have real sequencing dependencies: schema → domain/CRUD → recurrence engine → calendar/overview UI. Numbered from **74** (following the stated 72: change signup character, 73: security alerts).

### Phase 74 — Event data model + standalone (non-recurring) CRUD

**Delivers:** `EventEntity`/`EventSignupEntity` (not `EventSeriesEntity` yet — defer recurrence), the one migration (§7, minus the `EventSeries` table and `EventSeriesId`/`SeriesSlotIndex` columns — those can be added in Phase 76 as a second small migration, or included now as unused nullable columns if the team prefers one migration total; **recommend deferring** to keep this phase's blast radius to exactly what it uses), `IEventService`/`IEventRepository`/`EventRepository` with narrow scalar-update methods (§2's anti-`UpdateAsync`-trap guidance), `EntityProfile` mappings, `EventsController` with Create/Details/Edit/Cancel actions (standalone events only — no series concept exposed in the UI yet), desktop + mobile views, `DungeonMasterOnly` on mutations, navbar entry (`_Layout.cshtml`, alongside "Create Quest" per the operator's spec — confirmed insertion point at `_Layout.cshtml:96-99`'s dropdown, plus `_Layout.Mobile.cshtml`'s equivalent).

**Why first:** every other phase depends on `EventEntity` existing and being correctly tenant-scoped and correctly CRUD-able. Doing standalone-only first means the DM auto-signup and calendar-rendering work (Phases 75-76) can be built and manually verified against real rows before recurrence complexity is layered on.

**Explicitly deferred:** recurrence, calendar-grid rendering, overview page, auto-signup.

### Phase 75 — Auto-signup + Campaign/One-Shot opt-in/opt-out behavior

**Delivers:** the Campaign-board auto-signup-on-create path (§5) wired into `EventService.CreateStandaloneAsync`, the One-Shot opt-in signup UI/vote-recording (`EventSignupRepository.RecordVoteAsync`, mirroring `PlayerSignupRepository.ChangeVoteAsync`'s shape per §2), and the group-join backfill extension to `IGroupService.AddMemberAsync`/`CreateOrAddToGroupAsync` (§5's "new member joins after occurrences exist" fix).

**Why second, not folded into 74:** it's a genuinely separate correctness concern (who gets a row and when) layered cleanly on top of "an event exists" — testable in isolation (given N existing events and a board type, does the right signup set exist), and it's the piece most likely to need a live human-verify checkpoint (the operator's whole spec hinges on "auto-signed-up with vote=Yes" being visibly correct, which is a UX judgment call, not just a unit test).

**Depends on:** Phase 74 (needs `EventEntity`/`EventSignupEntity` to exist).

### Phase 76 — Calendar integration (the CalendarViewModel/partial blast-radius work)

**Delivers:** exactly §3's file list — `CalendarViewModel.Events`/`CalendarDay.EventsOnDay`, `CalendarController` wiring, `_Calendar.cshtml` event-chip rendering, `Calendar/Index.Mobile.cshtml` agenda-list inclusion, new `.calendar-event` CSS. Verified: no change needed to `_Calendar.Mobile.cshtml`, `Quest/Details.cshtml`, or `Quest/Details.Mobile.cshtml`.

**Why third, not first:** the calendar rendering has nothing to render until Phase 74 produces real `Event` rows, and no reason to differentiate "auto-signed-up Yes" styling from "not yet voted" until Phase 75 exists — though the calendar chip itself doesn't strictly need per-user vote state (unlike Quest's vote-radio widget), so this phase is genuinely independent of 75's internals, just sequenced after both for a working end-to-end demo at each phase boundary.

**Depends on:** Phase 74 (needs `Event` rows and `IEventService.GetEventsForCalendarAsync`). Does not strictly depend on Phase 75, but sequencing after it means the calendar can be manually verified against a realistic signed-up dataset rather than an empty one.

### Phase 77 — Recurrence: EventSeries + materialization job

**Delivers:** `EventSeriesEntity` + migration (adds the table plus the `EventSeriesId`/`SeriesSlotIndex` columns on `Events` if deferred from Phase 74), `IEventSeriesService`/`EventSeriesRepository`, the cycle-mask domain logic (§1's `GetCycleMask()`/materialization algorithm), `EventMaterializationJob` + `HangfireJobHelper`-pattern scope handling + `Program.cs` `RecurringJob.AddOrUpdate` registration (§4), series-creation UI (extending `EventsController.Create` with a "make this recurring" sub-form), and per-occurrence Move/Cancel/Edit actions that must now also handle the `SeriesSlotIndex` idempotency contract (§1) correctly — i.e., confirm a moved/edited/cancelled occurrence is never regenerated.

**Why last:** recurrence is additive on top of everything else — a materialization job that creates standalone-shaped `EventEntity` rows (from Phase 74's schema) which auto-signup (Phase 75's logic) applies to and which the calendar (Phase 76) already knows how to render, with zero changes required to any of those three phases' code. This is also the highest-complexity, highest-risk phase (idempotency correctness, "what regenerates on rule edit" semantics per §4) — isolating it last means a bug here can't block the three simpler, more valuable pieces (standalone events, auto-signup, calendar visibility) from shipping and being useful on their own.

**Depends on:** Phase 74 (schema foundation), Phase 76 (so recurrence-generated occurrences can be immediately verified on the calendar, not just in a database table).

### Phase 78 — Availability overview page

**Delivers:** `EventsController.Overview` action (or new dedicated controller per §6's naming discussion), the N+1-safe grid query (§6), desktop + mobile grid views, `[Authorize]` (not DM-only — any member views it).

**Why last:** the overview page is the most "read-heavy aggregate view" of the whole feature — it wants recurring events (Phase 77) already materializing real future rows, auto-signup (Phase 75) already populating the Campaign-board Yes column, and the calendar (Phase 76) already proving the underlying data model renders correctly, so the grid has a realistic, exercised dataset to display and be verified against rather than a synthetic one. It has no other phase depending on it, so there's no cost to sequencing it last.

**Depends on:** Phase 74 (data), Phase 75 (auto-signup correctness is the whole point of the grid for Campaign boards), Phase 77 (recurring events are "upcoming events" too — a grid that only shows standalone events would misrepresent availability for any group using recurring sessions).

### Dependency graph

```
74 (schema + standalone CRUD)
 ├──> 75 (auto-signup + opt-in)
 │      └──> 78 (overview) ◄──────┐
 ├──> 76 (calendar integration)   │
 │      └──> 78 (overview) ◄──────┤
 └──> 77 (recurrence + materialization, sequenced after 76 for verifiability)
        └──> 78 (overview) ◄──────┘
```

Minimum viable sequence if scope needs trimming mid-milestone: **74 → 75 → 76** ships a fully usable non-recurring Events feature (create, auto-signup, calendar-visible) — the operator's recurrence and overview-grid asks (77, 78) are real but separable value-adds, not blocking dependencies of each other or of the core feature.

---

## Sources

All findings are internal-codebase verification, cited inline by file path/line above. No external ecosystem/library research was performed or needed — SQL Server storage trade-offs (§1) and the EF Core `HasQueryFilter`/AutoMapper-collection-overwrite pitfalls (§2) are argued from this repository's own established, already-battle-tested patterns (Phase 49/55 security hardening, `PlayerSignupRepository`'s existing `UpdateAsync` override, `ReminderLogEntity`'s idempotency shape), not from generic web research.

- `.planning/PROJECT.md` (v9.0 shipped-state history — Phases 27, 34.2, 34.3, 37, 39, 40, 44, 49, 55, 57)
- `.planning/codebase/ARCHITECTURE.md`
- `.planning/codebase/CONVENTIONS.md`
- `QuestBoard.Repository/Entities/QuestBoardContext.cs`
- `QuestBoard.Repository/PlayerSignupRepository.cs`
- `QuestBoard.Repository/BaseRepository.cs`
- `QuestBoard.Repository/Automapper/EntityProfile.cs`
- `QuestBoard.Repository/Migrations/20260706193921_AddContactsFeature.cs`
- `QuestBoard.Repository/Migrations/20260705183646_AddGroupIdToCharacters.cs`
- `QuestBoard.Service/Jobs/DailyReminderJob.cs`
- `QuestBoard.Service/Jobs/HangfireJobHelper.cs`
- `QuestBoard.Service/Program.cs`
- `QuestBoard.Service/Controllers/QuestBoard/CalendarController.cs`
- `QuestBoard.Service/Controllers/Contacts/ContactsController.cs`
- `QuestBoard.Service/ViewModels/CalendarViewModels/CalendarViewModel.cs`
- `QuestBoard.Service/ViewModels/CalendarViewModels/CalendarDay.cs`
- `QuestBoard.Service/Views/Shared/_Calendar.cshtml`
- `QuestBoard.Service/Views/Shared/_Calendar.Mobile.cshtml`
- `QuestBoard.Service/Views/Calendar/Index.cshtml`
- `QuestBoard.Service/Views/Calendar/Index.Mobile.cshtml`
- `QuestBoard.Service/Views/Quest/Details.cshtml`
- `QuestBoard.Service/Views/Quest/Details.Mobile.cshtml`
- `QuestBoard.Service/Views/Shared/_Layout.cshtml`
- `QuestBoard.Domain/Models/QuestBoard/Quest.cs`
- `QuestBoard.Domain/Models/QuestBoard/PlayerSignup.cs`
- `QuestBoard.Domain/Enums/VoteType.cs`, `GroupRole.cs`, `BoardType.cs`
- `QuestBoard.Domain/Interfaces/IGroupService.cs`
- `QuestBoard.Repository/Entities/PlayerSignupEntity.cs`

---
*Architecture research for: Calendar Events (v9.0)*
*Researched: 2026-08-25*
