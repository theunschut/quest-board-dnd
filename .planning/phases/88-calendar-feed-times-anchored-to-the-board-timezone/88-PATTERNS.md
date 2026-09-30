# Phase 88: Calendar Feed Times Anchored to the Board Timezone - Pattern Map

**Mapped:** 2026-09-30
**Files analyzed:** 13 (0 new source files, all modifications; 1 optional extraction)
**Analogs found:** 13 / 13

All paths below were confirmed git-tracked. Note two path corrections against RESEARCH.md: the integration tests `WallClockUnmovedTests.cs` and `BoardTimeZoneHealthCheckTests.cs` live in `QuestBoard.IntegrationTests/Controllers/`, not `Tests/`.

## File Classification

| File | Role | Data Flow | Closest Analog | Match |
|------|------|-----------|----------------|-------|
| `QuestBoard.Domain/Services/CalendarFeedWriter.cs` | service (pure text writer) | transform | itself (`AppendCalendarHeaders`, `AppendFoldedLine`) | exact |
| `QuestBoard.Domain/Interfaces/ICalendarFeedWriter.cs` | interface | transform | itself (line 12 signature) | exact |
| `QuestBoard.Domain/Services/CalendarSubscriptionService.cs` | service | request-response | `QuestBoard.Domain/Services/QuestService.cs` (primary-ctor `IBoardClock`) | exact |
| optional `QuestBoard.Domain/Services/VTimeZoneBuilder.cs` (NEW, only if builder exceeds ~60 lines) | utility | transform | `CalendarFeedWriter` private statics | role-match |
| `QuestBoard.UnitTests/Services/CalendarFeedWriterTests.cs` | test | transform | itself | exact |
| `QuestBoard.UnitTests/Services/CalendarFeedFloatingTimeGuardTests.cs` | test | transform | itself + `Helpers/FakeBoardClock.cs` | exact |
| `QuestBoard.UnitTests/Services/CalendarSubscriptionQuestRecheckTests.cs` | test | request-response | itself (ctor gains `new FakeBoardClock()`) | exact |
| `QuestBoard.IntegrationTests/Tests/CalendarSubscriptionFeedTests.cs` | test | request-response | itself | exact |
| `QuestBoard.IntegrationTests/Tests/CalendarSubscriptionQuestFeedTests.cs` | test | request-response | itself | exact |
| new zone-variant integration facts (Auckland, `Definitely/NotAZone`) | test | request-response | `Controllers/WallClockUnmovedTests.cs`, `Controllers/BoardTimeZoneHealthCheckTests.cs` | exact |
| `.claude/architecture.md` (lines 46-47) | config/doc | n/a | own "Time and the board clock" section | exact |
| `.planning/REQUIREMENTS.md` | doc | n/a | `QUESTFEED-*` block (lines 120-139) + traceability rows (356+) | exact |
| `.planning/ROADMAP.md` | doc | n/a | `QUESTFEED-*` rows (line 752+) and CALFEED rows (735+) | exact |

## Pattern Assignments

### `CalendarFeedWriter.cs` (transform)

**Current code to edit** (verified current this session):
- Class comment lines 9-12 ("five-field VEVENT with no timezone") is stale; rewrite.
- `Write` line 18-21: signature and `AppendCalendarHeaders(builder, calendarName)`; the new `AppendTimeZone` call goes between line 21 and the `foreach` on line 23.
- `AppendCalendarHeaders` lines 44-64; `X-WR-CALNAME` at line 50 (insert `X-WR-TIMEZONE` directly after).
- Timed branch comment lines 66-69 stale; `DTSTART:`/`DTEND:` at 78-79; `SEQUENCE:0` at line 82 and line 100 (all-day). `FormatBasicDateTime` comment lines 149-150 stale.

**Line emission pattern to copy for every new VTIMEZONE line** (lines 173-176):
```csharp
private static void AppendFoldedLine(StringBuilder builder, string content)
{
    builder.Append(FoldLine(content)).Append(LineBreak);
}
```
Block openers in the existing code use `builder.Append("BEGIN:VEVENT").Append(LineBreak);` (line 75); RESEARCH's `AppendFoldedLine(builder, "BEGIN:VTIMEZONE")` is equivalent for short lines.

**Escaping** (161-168): route `TZID:` and `X-WR-TIMEZONE:` values through `EscapeText`. For the `;TZID=` parameter on `DTSTART`/`DTEND`, use the raw tzid (quote only if it contains `;`, `:` or `,`).

**Header pattern** (line 50):
```csharp
AppendFoldedLine(builder, "X-WR-CALNAME:" + EscapeText(calendarName));
```
Add `AppendFoldedLine(builder, "X-WR-TIMEZONE:" + EscapeText(tzid));` after it.

**Timed lines** (78-79) become `"DTSTART;TZID=" + tzid + ":" + FormatBasicDateTime(start)`; `start` stays `entry.Date.ToDateTime(entry.StartTime!.Value)` (Unspecified kind, no conversion). All-day lines 96-97 keep `VALUE=DATE` with no TZID; only `SEQUENCE` changes.

**Full VTIMEZONE builder, ResolveTzid, FormatUtcOffset, and probing algorithm:** copy from 88-RESEARCH.md "Target writer shape" (prototype hash-verified on Windows and Linux). No `DateTime.Now/UtcNow/Today` may appear (AmbientClockSeamTests guard). No GSD ids in comments.

### `ICalendarFeedWriter.cs`

Line 12 currently: `string Write(IReadOnlyList<CalendarFeedEntry> entries, string calendarName);`. Add `TimeZoneInfo boardZone` (per-document). Update the `<inheritdoc/>` XML doc on the interface to say the zone is the resolved board zone.

### `CalendarSubscriptionService.cs` (gains IBoardClock)

**Ctor today** (lines 12-20): parameters in order `subscriptionRepository, eventSignupRepository, questRepository, groupService, writer, timeProvider, feedOptions, logger`. Insert `IBoardClock boardClock` after `writer` (matches RESEARCH and the tests' positional constructor call).

**Analog for taking IBoardClock via primary ctor** (`QuestService.cs` lines 8-15; `EventSeriesService.cs` line 16 identical style):
```csharp
internal class QuestService(
    IQuestRepository repository,
    ...
    IMapper mapper,
    IBoardClock boardClock) : BaseService<Quest>(repository, mapper), IQuestService
```
`IBoardClock` is a singleton (`ServiceExtensions.cs:73`, `services.TryAddSingleton<IBoardClock, BoardClock>();`), so injecting into the scoped service is safe. `using QuestBoard.Domain.Interfaces;` is already present. Call: `writer.Write(allEntries, "D&D Quest Board", boardClock.TimeZone)`. Keep `TimeProvider` for the window. Reword the comment near lines 114-119 (no planning ids); do not change the code below it (lines ~145-146 split `FinalizedDate` unchanged).

### Unit tests: `CalendarFeedWriterTests.cs`

Existing style (lines 104-107): exact `body.Should().Contain("DTSTART:...")` plus `NotContain`. Rewrite per RESEARCH Code Map (lines 104-107, 117-118, 163-171 rename to `EmitsSequenceOne`, 268-281 drop only the TZID/VTIMEZONE NotContains, 483-516, 576). 33 `Writer.Write(...)` call sites: add one shared `private static readonly TimeZoneInfo AmsterdamZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Amsterdam");` field (the file already uses static `Writer`) to keep the diff small. Helper `AssertNoPhysicalLineExceeds75Octets` (line ~64) should be applied to a zone document too.

### `CalendarFeedFloatingTimeGuardTests.cs` (rewrite, keep file)

**Reuse the file's own shapes**: `MakeFinalizedQuestEntry` (21-34), `SilentLogger` (76-86, hand-rolled because the service is internal), and the substitute-writer capture at lines 145-148:
```csharp
writer.Write(Arg.Do<IReadOnlyList<CalendarFeedEntry>>(entries => capturedEntries = entries), Arg.Any<string>())
    .Returns("BEGIN:VCALENDAR\r\nEND:VCALENDAR\r\n");
```
Extend with a third matcher: `Arg.Do<TimeZoneInfo>(z => capturedZone = z)`. Ctor call at lines 150-158 gains `nonDefaultBoardClock` after `writer`. Existing non-Amsterdam zone construction already at line 58 and 99 (`"Pacific/Auckland"`); pass this clock into the service (currently built at 97-101, "held in scope" and unused).

**FakeBoardClock** (`QuestBoard.UnitTests/Helpers/FakeBoardClock.cs` lines 9-18): settable `TimeZone` (default `TimeZoneInfo.Utc`), `IsDegraded`, `Today`, `Now`. Use `new FakeBoardClock { TimeZone = TimeZoneInfo.Utc, IsDegraded = true }` for the UTC-fallback fact. Rewrite class comment (11-16) and comments at 43-44, 54-57, 91-96, 165-166, which are all stale. Rewrite fact names (`...EmitsExactFloatingDtstart...`, `...UnaffectedByANonDefaultTimeZone...`).

Add per CONTEXT specifics: one entry 2 Oct 18:00 and one 30 Oct (Amsterdam) asserting the VTIMEZONE has a STANDARD observance `DTSTART:20261025T030000 / TZOFFSETFROM:+0200 / TZOFFSETTO:+0100` and both `DTSTART;TZID=Europe/Amsterdam:` lines keep `180000` digits. Expected bytes are in RESEARCH "Expected bytes".

### `CalendarSubscriptionQuestRecheckTests.cs`

Ctor at ~113-121 with a real `CalendarFeedWriter`: add `new FakeBoardClock()` after the writer argument (needs `using QuestBoard.UnitTests.Helpers;` if not present). No assertion edits.

### Integration tests: feed tests and new zone-variant facts

**Zone-variant factory to copy verbatim** (`Controllers/WallClockUnmovedTests.cs` lines 22-34):
```csharp
private WebApplicationFactory<Program> CreateZoneVariantFactory(string zoneId)
{
    return factory.WithWebHostBuilder(builder =>
    {
        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TimeZone:BoardTimeZoneId"] = zoneId
            });
        });
    });
}
```
Class shape: `public class X(WebApplicationFactoryBase factory) : IClassFixture<WebApplicationFactoryBase>`, usings `Microsoft.AspNetCore.Hosting`, `Microsoft.Extensions.Configuration`, `QuestBoard.IntegrationTests.Helpers`.

**Degraded variant** (`Controllers/BoardTimeZoneHealthCheckTests.cs` ~lines 27-40): same body with `["TimeZone:BoardTimeZoneId"] = "Definitely/NotAZone"` (named `CreateDegradedZoneFactory`). Use it to assert the feed declares `TZID:UTC`, a `+0000` observance, and `X-WR-TIMEZONE:UTC`.

Existing feed assertion edits: `Tests/CalendarSubscriptionFeedTests.cs` lines 190-191; `Tests/CalendarSubscriptionQuestFeedTests.cs` lines 258, 259, 264, 675. For those two feed tests, reuse each file's own helpers to create the subscription and fetch the feed; the new zone facts fit in a new class or these files, planner's call. Because the feed tests likely use the default factory, the default expectation is `DTSTART;TZID=Europe/Amsterdam:`.

### Docs: requirements minting (`CALTZ`)

**Analog:** Phase 85 QUESTFEED. Three edit sites:
1. `.planning/REQUIREMENTS.md`: a new section after the QUESTFEED block (lines ~120-139) with the same bullet form `- [ ] **CALTZ-01**: <sentence>` (sentences from RESEARCH "Phase Requirements"; QUESTFEED uses `[x]` once complete, start with `[ ]`). Section header style: `### Calendar Feed — One-Shot Quest Sessions`. Add traceability rows near the end of the existing table (QUESTFEED rows at 356+): `| CALTZ-01 | Phase 88 | Pending |`. Amend `CALFEED-10` (line 111, "floating local time with no timezone declared") to note it is superseded by CALTZ-01.
2. `.planning/ROADMAP.md`: replace line 1070 (`**Requirements**: TBD — settle in the discuss pass.`) with the family IDs; add a traceability table at ~735-752 style: `| CALTZ-01 | Phase 88 |`. Check line 1099 `** TBD` for a second placeholder.
3. The first plan owns this work (CONTEXT).

Note memory: planning docs have mixed LF/CRLF line endings, so never anchor edits on `\n`.

### `.claude/architecture.md` lines 46-47

Rewrite only the feed sentence ("The calendar feed emits floating local `DTSTART` with no `TZID`/`VTIMEZONE`...") to: timed entries carry `TZID` of the resolved board zone with a generated VTIMEZONE, wall-clock digits unchanged; keep "treat changes there as high-risk". Keep line 37's wall-clock vocabulary.

## Shared Patterns

### Single-source zone
`IBoardClock.TimeZone` only (never `IOptions<TimeZoneOptions>`). `BoardClock` falls back to `TimeZoneInfo.Utc` (`BoardClock.cs:27`).

### Constructor argument churn
Two unit test files construct `CalendarSubscriptionService` positionally (Guard tests lines 150-158; Recheck tests ~113-121); both need `IBoardClock` after `writer`. Grep `new CalendarSubscriptionService(` before finalizing in case others exist.

### AmbientClockSeamTests
Adding `IBoardClock` to the service does not affect it (per RESEARCH, neither file is in its guarded lists), but the new code must not contain `DateTime.Now/UtcNow/Today`; the builder uses only `GetUtcOffset` and entry-derived values.

### Comments
No requirement IDs, phase numbers or planning doc names in source (CLAUDE.md). Explain the why in plain language.

### Test assertion style
FluentAssertions plain-string `Contain("...\r\n")`, no snapshot framework, xunit.v3 `[Fact]`.

## No Analog Found

| File | Role | Reason |
|------|------|--------|
| VTIMEZONE offset-probing builder | utility | No existing code probes offset transitions; use RESEARCH "Target writer shape" (prototype-verified on Windows and Linux). |

## Metadata

**Analog search scope:** `QuestBoard.Domain/Services`, `QuestBoard.Domain/Interfaces`, `QuestBoard.UnitTests/{Services,Helpers,Architecture}`, `QuestBoard.IntegrationTests/Controllers`, `.planning/{REQUIREMENTS,ROADMAP}.md`
**Pattern extraction date:** 2026-09-30
