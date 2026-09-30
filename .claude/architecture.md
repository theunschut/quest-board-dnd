# Architecture

Three-layer clean architecture: **Service → Domain → Repository** (strict one-way dependency).

- `QuestBoard.Service` — MVC controllers, Razor views, ViewModels, authorization handlers
- `QuestBoard.Domain` — business logic, domain models, service interfaces
- `QuestBoard.Repository` — EF Core entities, repositories, `QuestBoardContext`, migrations

AutoMapper runs at two boundaries:

- Entity ↔ DomainModel: `QuestBoard.Repository/Automapper/EntityProfile.cs`
- DomainModel ↔ ViewModel: `QuestBoard.Service/Automapper/ViewModelProfile.cs`

Authorization policies: `"DungeonMasterOnly"` (DungeonMaster or Admin role), `"AdminOnly"` (Admin role only).

## Entity Framework

**IMPORTANT**: EF packages belong only in `QuestBoard.Repository` — never add them to the Service project.

Migrations are **auto-applied on startup** via `context.Database.Migrate()` — no manual `database update` needed in dev.

Writes to `Events` and `Quests` must go through the change tracker — no bulk update, raw SQL or attaching a detached entity — or the calendar feed revision does not rise, and a guard test fails.

```bash
# Add/remove migrations (run from QuestBoard.Service/)
dotnet ef migrations add MigrationName --project ../QuestBoard.Repository
dotnet ef migrations remove --project ../QuestBoard.Repository
```

## Time and the board clock

The application distinguishes two kinds of date value, and confusing them is the most damaging
mistake available in this codebase:

- A **real instant** — a UTC-stored point in time (`CreatedAt`, `ClosedDate`, a signup timestamp).
  It *should* follow the viewer. Render via `Html.LocalTime`, which emits
  `<time class="local-time" datetime="…Z" title="… UTC">`; `site.js` re-formats it into the
  viewer's own timezone on load.
- A **wall-clock value** — a floating local time with no UTC instant (a finalized game night, a
  proposed session date, an event's `Date` + `StartTime`). It must **never** be timezone-converted;
  doing so moves when real people show up. Render via `Html.WallClock`, which deliberately takes no
  timezone parameter and so cannot convert.

Server-side "what day is it" reads go through `IBoardClock` (`QuestBoard.Domain`), never
`DateTime.Now`/`UtcNow`/`Today` — the container clock is UTC and would misjudge board-local dates.
`AmbientClockSeamTests` holds a closed list of permitted ambient reads; do not weaken it.

The calendar feed declares the board's zone on every timed entry: `DTSTART;TZID=<zone>` and
`DTEND;TZID=<zone>` carry the stored wall-clock digits unchanged, because declaring which zone an
hour belongs to is not converting it. A generated `VTIMEZONE` lists that zone's offsets across the
span the entries cover, found by asking the zone for its offset at a moment so the block comes out
identical on Windows and Linux, and `X-WR-TIMEZONE` names the same zone. All three come from
`IBoardClock.TimeZone`, never from the configured id string, so a clock that fell back to UTC
declares UTC. All-day entries stay date-valued and carry no zone. Each entry's
`SEQUENCE` is its stored revision, `FeedRevision` on `Events` and `Quests`. `QuestBoardContext`
raises it through `FeedRevisionStamper` on any save that changes what the feed shows: an event's
title, date, start time, cancellation or board, or a quest's title, finalized date, finalized state
or board. `DTSTAMP` and `LAST-MODIFIED` carry the matching `FeedRevisedAt`, a real UTC instant that
is labelled and never converted; a reader's own availability answer moves only that reader's stamp.
The sequence must never go down or below 1, and an entry nobody changed must stay byte-identical
between fetches so the ETag and 304 hold. Known limitations:
renaming a board does not re-signal existing calendar entries, so each entry picks up the new board
name on its next real revision, and a change to the configured quest session length or board zone
is configuration rather than a stored row, so it likewise reaches clients only with each entry's
next revision. `CalendarFeedWriter.cs`,
`CalendarSubscriptionService.cs` and `FeedRevisionStamper.cs` are guarded by tests that pin this —
treat changes there as high-risk.
