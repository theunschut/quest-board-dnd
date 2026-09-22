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

The calendar feed emits floating local `DTSTART` with no `TZID`/`VTIMEZONE`. `CalendarFeedWriter.cs`
and `CalendarSubscriptionService.cs` are guarded by tests that pin this — treat changes there as
high-risk.
