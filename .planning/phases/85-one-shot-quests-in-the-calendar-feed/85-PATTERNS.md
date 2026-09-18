# Phase 85: One-Shot Quests in the Calendar Feed - Pattern Map

**Mapped:** 2026-09-18
**Files analyzed:** 8 (all modifications; no new entity/migration in this phase)
**Analogs found:** 8 / 8 — every file is an addition to code Phase 84 shipped four days prior in the same milestone; this phase's own shipped code is its own best analog for everything except the D-01 union/predicate shape and the source-aware writer branches, which reach one layer further back to Phase 82.

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|---|---|---|---|---|
| `QuestBoard.Repository/QuestRepository.cs` (new `GetFeedQuestsForUserAsync`) | service (repository), cross-tenant read | CRUD / request-response | `QuestBoard.Repository/GroupRepository.cs` `GetGroupsForUserAsync` (lines 29-42) for the `Any()`-in-`Where` shape; `QuestBoard.Domain/Services/EventService.cs` / `EventSignupRepository.GetFeedRowsForUserAsync` for the tenant-safety framing | exact (predicate shape) / role-match (safety shape) |
| `QuestBoard.Domain/Interfaces/IQuestRepository.cs` (new method) | service (interface) | CRUD | `QuestBoard.Domain/Interfaces/IEventSignupRepository.cs` (doc-comment discipline: caller supplies `userId` from the authenticated principal, never request input) | role-match |
| `QuestBoard.Domain/Services/CalendarSubscriptionService.cs` (quest branch: `oneShotGroupIds`, query call, re-check, projection, merge+order) | service | CRUD + event-driven read | itself, lines 55-108 (the existing event branch it extends — verbatim structural precedent, same file) | exact |
| `QuestBoard.Domain/Services/CalendarFeedWriter.cs` (`AppendTimedEvent` duration; `BuildSummary` source branch) | utility (pure formatting) | transform | itself, lines 68-82 (`AppendTimedEvent`) and 100-118 (`BuildSummary`) — same file, existing lines being generalized | exact |
| `QuestBoard.Domain/Models/CalendarFeedEntry.cs` (add `Duration`) | model (domain) | — | itself (existing file, additive property) | exact |
| `QuestBoard.Domain/Models/CalendarFeedOptions.cs` (add `QuestDurationHours` + `IsValid()` clause) | config | — | itself, existing `IsValid()` (existing file, additive) | exact |
| `QuestBoard.Domain/Enums/CalendarFeedSource.cs` (add `Quest` member) | model (enum) | — | itself (existing file, additive) | exact |
| `QuestBoard.IntegrationTests/Tests/CalendarSubscriptionQuestFeedTests.cs` (new) | test | request-response, tenant-isolation | `QuestBoard.IntegrationTests/Tests/CalendarSubscriptionFeedTests.cs` (structure + InMemory-provider caveat doc-comment) and `AgendaTenantIsolationTests.cs` (seeding helper set, two-group shape) | exact |
| `QuestBoard.UnitTests/Services/CalendarFeedWriterTests.cs` (extended) | test | transform (pure-function) | itself (existing file, extended with Quest-source cases) | exact |

## Pattern Assignments

### `QuestBoard.Repository/QuestRepository.cs` — new `GetFeedQuestsForUserAsync` (repository, cross-tenant read)

**Primary analog — the `Any()`-in-`Where` predicate shape:** `QuestBoard.Repository/GroupRepository.cs:29-40`, `GetGroupsForUserAsync` (verified this session):
```csharp
// QuestBoard.Repository/GroupRepository.cs:29-40
public async Task<IList<GroupWithMemberCount>> GetGroupsForUserAsync(int userId, CancellationToken token = default)
{
    return await DbContext.Groups
        .Where(g => g.UserGroups.Any(ug => ug.UserId == userId))
        .Select(g => new GroupWithMemberCount
        {
            Id = g.Id,
            Name = g.Name,
            CreatedAt = g.CreatedAt,
            MemberCount = g.UserGroups.Count,
            BoardType = (BoardType)g.BoardType
        })
        .ToListAsync(token);
}
```
This is the in-repo, production-proven precedent for translating `Any()`-in-`Where` against SQL Server. Reproduce the same shape, rooted at `DbContext.Quests` instead of `DbContext.Groups`, combined with `||` for the DM branch:
```csharp
public async Task<IList<Quest>> GetFeedQuestsForUserAsync(
    int userId, IReadOnlyCollection<int> oneShotGroupIds,
    DateTime windowStart, DateTime windowEnd, CancellationToken token = default)
{
    // Rooted at Quests, so each quest is visited at most once -- no duplicate UID can reach
    // the writer regardless of whether the DM also holds a selected signup on their own quest.
    var entities = await DbContext.Quests
        .IgnoreQueryFilters()
        .Where(q => oneShotGroupIds.Contains(q.GroupId)
            && q.IsFinalized && q.FinalizedDate != null
            && q.FinalizedDate.Value >= windowStart && q.FinalizedDate.Value <= windowEnd
            && (q.PlayerSignups.Any(ps => ps.PlayerId == userId && ps.IsSelected)
                || q.DungeonMasterId == userId))
        .AsNoTracking()
        .ToListAsync(token);

    return Mapper.Map<IList<Quest>>(entities);
}
```
Do not use `.Take(...)` and do not `.Include(...)` a signup roster — this phase's window is a date range, not a row count, and no roster join is needed (mirrors Phase 84's own trimming of the `EventSignups`-rooted method relative to the wider `GetUpcomingAcrossGroupsWithSignupsAsync` it was modeled on).

**Anti-pattern, explicit:** do not build this as a variant of any existing `QuestRepository` method — every existing method is scoped to the *active* group via the ambient query filter and none expresses the DM-OR-signup union. Do not resolve `IActiveGroupContext`/`IBoardTypeResolver` anywhere on this path.

**`IgnoreQueryFilters()` must cover both entities reached by the predicate.** `QuestEntity` and `PlayerSignupEntity` (reached implicitly through `q.PlayerSignups.Any(...)`) both carry fail-closed `HasQueryFilter` calls keyed on `ActiveGroupId`, which is always null on this session-less path:
```csharp
// QuestBoard.Repository/Entities/QuestBoardContext.cs:393-396 (Quest filter, fail-closed)
modelBuilder.Entity<QuestEntity>().HasQueryFilter(e => activeGroupContext.ActiveGroupId != null && e.GroupId == activeGroupContext.ActiveGroupId);
// QuestBoard.Repository/Entities/QuestBoardContext.cs:425-428 (PlayerSignup filter, reached via Quest navigation)
modelBuilder.Entity<PlayerSignupEntity>().HasQueryFilter(ps => activeGroupContext.ActiveGroupId != null && ps.Quest.GroupId == activeGroupContext.ActiveGroupId);
```
`IgnoreQueryFilters()` on the root `Quests` query disables the filter tree for the whole query, including the `PlayerSignups` navigation reached inside `Any()` — one call is sufficient, but it is mandatory or every row silently vanishes (indistinguishable from "no quests qualify").

### `QuestBoard.Domain/Interfaces/IQuestRepository.cs` (interface, doc-comment discipline)

**Analog:** `QuestBoard.Domain/Interfaces/IEventSignupRepository.cs:6-23` (Phase 84 pattern, reused unchanged):
```csharp
/// <summary>
/// ... The caller must supply <paramref name="userId"/> from the authenticated principal and
/// never from request input, ...
/// </summary>
```
Apply the same discipline to the new `GetFeedQuestsForUserAsync` doc comment: `userId` and `oneShotGroupIds` both come from server-side state (`subscription.UserId`, the fresh membership read), never from any request parameter.

### `QuestBoard.Domain/Services/CalendarSubscriptionService.cs` — quest branch (service, composition point)

**Analog: itself.** The existing event branch, verified this session (lines ~55-108):
```csharp
// Membership is read fresh from the database on every fetch and never taken from
// session or claims -- this request has neither. IActiveGroupContext must never be
// resolved anywhere on this path: it is null here, and every entity filtered through
// it returns zero rows silently rather than throwing.
var memberships = await groupService.GetGroupsForUserAsync(subscription.UserId, token);
var memberGroupIds = memberships.Select(m => m.Id).ToList();
var boardNamesById = memberships.ToDictionary(m => m.Id, m => m.Name);

var options = feedOptions.Value;
var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
var windowStart = today.AddMonths(-options.MonthsBack);
var windowEnd = today.AddMonths(options.MonthsAhead);

var fetched = await eventSignupRepository.GetFeedRowsForUserAsync(
    subscription.UserId, memberGroupIds, windowStart, windowEnd, token);

// Second-layer re-check, mirroring EventService.GetCrossBoardAgendaAsync. This is
// mandatory here specifically because a feed is read by a machine, so a leak has no
// reader to notice it.
var checkedRows = fetched.Where(row => memberGroupIds.Contains(row.Event.GroupId)).ToList();
if (checkedRows.Count != fetched.Count)
{
    logger.LogError(
        "Calendar feed dropped {DroppedCount} of {FetchedCount} row(s) falling outside the subscription owner's board set. The query is built from the same set, so this indicates a lost or mistranslated board predicate.",
        fetched.Count - checkedRows.Count,
        fetched.Count);
}

var entries = checkedRows
    .Select(row => new CalendarFeedEntry { Source = CalendarFeedSource.Event, ... })
    .ToList();

var body = writer.Write(entries, "D&D Quest Board");
```
**Add, immediately after `boardNamesById`:**
```csharp
var oneShotGroupIds = memberships.Where(m => m.BoardType == BoardType.OneShot).Select(m => m.Id).ToList();
```
This single line encodes both the membership predicate and the board-type predicate at once — it can only ever be a subset of `memberGroupIds`, so it cannot satisfy one without the other. Do not derive it from a separate, unfiltered "all one-shot boards" query.

**Quest read + second-layer re-check — reproduce the event branch's three parts verbatim, substituting the quest set:**
```csharp
var quests = await questRepository.GetFeedQuestsForUserAsync(
    subscription.UserId, oneShotGroupIds,
    windowStart.ToDateTime(TimeOnly.MinValue), windowEnd.ToDateTime(TimeOnly.MaxValue), token);

var checkedQuests = quests.Where(q => oneShotGroupIds.Contains(q.GroupId)).ToList();
if (checkedQuests.Count != quests.Count)
{
    logger.LogError(
        "Calendar feed dropped {DroppedCount} of {FetchedCount} quest row(s) falling outside the subscription owner's one-shot board set. The query is built from the same set, so this indicates a lost or mistranslated board-type or membership predicate.",
        quests.Count - checkedQuests.Count, quests.Count);
}
```

**Projection + merge — append to the event `entries` list, then impose a defined order (this phase's addition; Phase 84 had only one source so no ordering decision existed before now):**
```csharp
var questEntries = checkedQuests
    .Select(q => new CalendarFeedEntry
    {
        Source = CalendarFeedSource.Quest,
        SourceId = q.Id,
        BoardName = boardNamesById.TryGetValue(q.GroupId, out var boardName) ? boardName : string.Empty,
        Title = q.Title,
        Date = DateOnly.FromDateTime(q.FinalizedDate!.Value),
        StartTime = TimeOnly.FromDateTime(q.FinalizedDate.Value),
        Duration = TimeSpan.FromHours(options.QuestDurationHours),
        CreatedAt = q.CreatedAt
        // Availability left at its default -- BuildSummary now branches on Source before
        // consulting it at all, so no (maybe)/(declined) suffix can leak onto a quest row.
    })
    .ToList();

var allEntries = entries.Concat(questEntries)
    .OrderBy(e => e.Date)
    .ThenBy(e => e.StartTime ?? TimeOnly.MaxValue)
    .ThenBy(e => e.Source)
    .ThenBy(e => e.SourceId)
    .ToList();

var body = writer.Write(allEntries, "D&D Quest Board");
```

**Time-basis note (Pitfall 3 from RESEARCH.md, load-bearing for this file specifically):** the window is computed from `timeProvider.GetUtcNow().UtcDateTime` as a `DateOnly`, while `FinalizedDate` is a `DateTime?` stored in server local time (standing known issue, `PROJECT.md:168`). Convert the `DateOnly` bounds to `DateTime` via `windowStart.ToDateTime(TimeOnly.MinValue)` / `windowEnd.ToDateTime(TimeOnly.MaxValue)` as shown above — a stated, deliberate choice, not a silent mix. Do not attempt to fix the underlying server-local-time storage; out of scope.

### `QuestBoard.Domain/Services/CalendarFeedWriter.cs` — `AppendTimedEvent` duration, `BuildSummary` source branch (utility, pure formatting)

**Analog: itself.** Full relevant excerpt verified this session:
```csharp
// AppendTimedEvent, current (lines ~68-82)
private void AppendTimedEvent(StringBuilder builder, CalendarFeedEntry entry)
{
    var start = entry.Date.ToDateTime(entry.StartTime!.Value);
    var end = start.AddHours(1);
    ...
}
```
**Change** `var end = start.AddHours(1);` to `var end = start.Add(entry.Duration);`. `AppendAllDayEvent` is untouched and stays unreachable for `Source == Quest` (`FinalizedDate` always carries a real time, so `entry.StartTime` is always non-null for a quest entry).

```csharp
// BuildSummary, current (verified this session)
private static string BuildSummary(CalendarFeedEntry entry)
{
    var title = "[" + entry.BoardName + "] " + entry.Title;

    var suffix = entry.Availability switch
    {
        VoteType.Maybe => " (maybe)",
        VoteType.No => " (declined)",
        _ => string.Empty,
    };

    return EscapeText(title + suffix);
}
```
**Change** the suffix computation to branch on `Source` before consulting `Availability` at all — `VoteType.No` is the enum's `0` default (`public enum VoteType { No, Maybe, Yes }`), so a quest entry built without explicitly setting `Availability` would otherwise silently render `" (declined)"`:
```csharp
var suffix = entry.Source == CalendarFeedSource.Event
    ? entry.Availability switch
    {
        VoteType.Maybe => " (maybe)",
        VoteType.No => " (declined)",
        _ => string.Empty,
    }
    : string.Empty;
```

**`BuildUid` needs no change** — verified this session:
```csharp
public string BuildUid(CalendarFeedSource source, int sourceId) =>
    $"questboard-{source.ToString().ToLowerInvariant()}-{sourceId}";
```
Already generalizes over any enum member; adding `Quest` to `CalendarFeedSource` alone yields `questboard-quest-{id}` with zero further writer change. Do not add a `switch` case here.

### `QuestBoard.Domain/Models/CalendarFeedEntry.cs` (model, additive)

**Analog: itself** (existing file, additive property). Add:
```csharp
public TimeSpan Duration { get; set; } = TimeSpan.FromHours(1);   // Event's existing fixed
                                                                    // one-hour block, now
                                                                    // explicit rather than
                                                                    // hard-coded in the writer
```
The default preserves every existing Phase 84 call site and unit test unmodified — only the new quest projection sets it explicitly.

### `QuestBoard.Domain/Models/CalendarFeedOptions.cs` (config, additive)

**Analog: itself**, existing `IsValid()` (verified pattern, refuse-to-start guard):
```csharp
public bool IsValid() => MonthsBack >= 0 && MonthsAhead >= 1 && LastFetchedThrottleMinutes >= 1 && RetentionDays >= 1;
```
Add:
```csharp
public int QuestDurationHours { get; set; } = 4;

public bool IsValid() => MonthsBack >= 0 && MonthsAhead >= 1 && LastFetchedThrottleMinutes >= 1
    && RetentionDays >= 1 && QuestDurationHours >= 1;
```

### `QuestBoard.Domain/Enums/CalendarFeedSource.cs` (enum, additive)

**Analog: itself.** Add a `Quest` member. The enum's own comment (per 84-PATTERNS/85-RESEARCH) already names this phase as the reason it exists. No other file needs a matching enum change beyond the `switch` in `BuildSummary` above.

### Test files

**`QuestBoard.IntegrationTests/Tests/CalendarSubscriptionQuestFeedTests.cs` (new)**

**Analog:** `QuestBoard.IntegrationTests/Tests/CalendarSubscriptionFeedTests.cs` — mirror its structure verbatim, including its InMemory-provider caveat doc-comment (its own doc comment states the shared harness backs every suite with the EF Core InMemory provider, which evaluates every predicate as ordinary LINQ-to-Objects — copy this caveat; it applies identically to the new `Any()`/`||` quest query). Cover: D-01 dedup (DM-only quest, signup-only quest, and a quest where the reader is both — asserting exactly one VEVENT/UID), D-02/D-03/D-04 predicates, D-09 disappearance cases, D-10 window edges, board-type narrowing (a finalized signed-up quest on a Campaign board must never appear), and tenant isolation.

**Seeding-helper analog:** `QuestBoard.IntegrationTests/Tests/AgendaTenantIsolationTests.cs` — reuse its seeding helper set (`SeedBoardAsync`, `SeedMembershipAsync`, `SeedMemberAsync`, `RemoveAllMembershipsAsync`, `LeaveBoardAsync`) verbatim, adapted for quest seeding instead of event seeding, for the two-group tenant-isolation cases.

**`QuestBoard.UnitTests/Services/CalendarFeedWriterTests.cs` (extended, not replaced)**

**Analog: itself.** Add Quest-source cases to the existing `MakeEntry`-style test helper: configurable duration (`DTEND` exactly `QuestDurationHours` after `DTSTART`), the no-suffix-ever guarantee (including an explicit `VoteType.No`-with-`Source.Quest` case proving the `BuildSummary` fix), and UID namespacing against a colliding numeric id (an Event and a Quest sharing the same integer id must produce two distinct, non-colliding UIDs).

## Shared Patterns

### Tenant-safety: pinned predicate + second-layer re-check (third instance in this codebase, now fourth)
**Source:** `QuestBoard.Domain/Services/CalendarSubscriptionService.cs` (existing event branch) and `QuestBoard.Repository/GroupRepository.cs:29-40` (`Any()`-in-`Where` translation precedent)
**Apply to:** the new quest branch in `CalendarSubscriptionService.GetFeedAsync` and the new `QuestRepository.GetFeedQuestsForUserAsync` — fresh membership read → `IgnoreQueryFilters()` + pinned `oneShotGroupIds.Contains(...)` predicate → in-memory re-check → `LogError` on any mismatch. `oneShotGroupIds` must apply *inside* the same `Where` as the DM/signup `Any()`/`||` branch, not as a separate `.Where().Concat(...)` composed at the C# level — the latter would let the DM branch escape board-type scoping.

### Fail-closed query filters — never resolve `IActiveGroupContext`/`IBoardTypeResolver` in this feed's path
**Source:** `QuestBoard.Repository/Entities/QuestBoardContext.cs:393-396` (`QuestEntity` filter), `:425-428` (`PlayerSignupEntity` filter, reached via `Quest` navigation)
**Apply to:** the new repository method — `IgnoreQueryFilters()` is mandatory on the root `Quests` query or every quest row silently vanishes. `IBoardTypeResolver` answers for the *active* group and returns null by construction on this session-less request; `oneShotGroupIds` (derived from the fresh membership read) is the only legitimate source of board-type scoping here.

### `VoteType.No` is the enum default — never trust an unset `Availability` to render empty
**Source:** `QuestBoard.Domain/Services/CalendarFeedWriter.cs` `BuildSummary`, verified this session; `QuestBoard.Domain/Enums/VoteType.cs` (`public enum VoteType { No, Maybe, Yes }`)
**Apply to:** every quest-entry construction site — branch on `entry.Source == CalendarFeedSource.Event` before consulting `Availability` at all, rather than depending on the quest projection remembering to set a "safe" `Availability` value.

### `BuildUid`'s existing enum-name-driven format needs no per-source branch
**Source:** `QuestBoard.Domain/Services/CalendarFeedWriter.cs`, `BuildUid`
**Apply to:** adding `CalendarFeedSource.Quest` — add the enum member only; do not touch `BuildUid`.

## No Analog Found

None — every file in this phase's scope extends code Phase 84 shipped in the same milestone, and each has a same-file or same-pattern-family precedent.

## Metadata

**Analog search scope:** `QuestBoard.Repository/` (`QuestRepository.cs`, `GroupRepository.cs`, `QuestBoardContext.cs` query-filter block), `QuestBoard.Domain/` (`CalendarSubscriptionService.cs`, `CalendarFeedWriter.cs`, `Models/CalendarFeedEntry.cs`, `Models/CalendarFeedOptions.cs`, `Enums/CalendarFeedSource.cs`, `Enums/VoteType.cs`, `Interfaces/IQuestRepository.cs`, `Interfaces/IEventSignupRepository.cs`), `QuestBoard.IntegrationTests/Tests/` (`CalendarSubscriptionFeedTests.cs`, `AgendaTenantIsolationTests.cs`), `QuestBoard.UnitTests/Services/CalendarFeedWriterTests.cs`
**Files scanned:** 10 read directly this session (verbatim excerpts confirmed against shipped bytes), plus 85-RESEARCH.md's own already-verified excerpts reused where this session's direct reads overlapped
**Pattern extraction date:** 2026-09-18
