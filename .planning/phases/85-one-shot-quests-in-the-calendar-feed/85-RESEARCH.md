# Phase 85: One-Shot Quests in the Calendar Feed - Research

**Researched:** 2026-09-18
**Domain:** EF Core cross-tenant query composition (second source into an existing feed), RFC 5545 writer extension for a source-aware duration and summary rule
**Confidence:** HIGH — every claim about the shipped Phase 84 code, the entity shapes, and the tenant-safety pattern was read directly from the files this session, with line numbers and verbatim quotes. The one area held to MEDIUM is the EF Core SQL-translation behaviour of the recommended `Any()`/`||` query shape: it follows an already-shipped, production-exercised precedent in this exact codebase, but this session did not execute it against SQL Server, and the project's own test harness (confirmed by direct inspection) cannot verify relational translation either.

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**Which quests reach the feed**
- **D-01**: A session the reader is running as DM reaches their feed, via a second predicate on `DungeonMasterId`. The quest read is a union of two branches — signup rows for the owner, plus quests where `DungeonMasterId` is the owner. **Structural consequence, load-bearing:** the two branches can both match the same quest if a DM also holds a signup row on it; `UID` is derived from the quest id alone, so an un-deduplicated union emits two VEVENTs sharing one `UID`. A distinct-by-quest-id step is mandatory, not an optimisation.
- **D-02**: Confirmed seats only — `IsSelected == true`. A waitlisted signup never reaches the feed.
- **D-03**: All three signup roles count — Player, Spectator and AssistantDM. No `SignupRole` branch in the predicate.
- **D-04**: A quest flagged `DungeonMasterSession` is not filtered out. No `DungeonMasterSession` clause in the query.

**How a session occupies the calendar**
- **D-05**: A quest is a fixed four-hour block starting at `FinalizedDate`, with the duration configurable on `CalendarFeedOptions`. A quest is always a timed entry, never all-day. The four hours is invented and must not be presented in the application UI as though the board knows the duration.
- **D-06**: Every quest entry is `TRANSP:TRANSPARENT`, matching events. One transparency rule for the whole feed; no per-source branch.

**What each entry says**
- **D-07**: `[Board] Title`, unchanged from 84 D-09. No marker identifying an entry as a quest.
- **D-08**: A session the reader is DMing reads identically to one they are playing. No `(DM)` suffix.
- Inherited unchanged from Phase 84 and not reopened: no `DESCRIPTION` (84 D-10), no `URL` or link of any kind (84 D-11), no `VALARM` (84 D-04), floating local time with no `TZID` (84 D-01).

**When a session leaves the feed**
- **D-09**: A quest that stops qualifying simply disappears. No `STATUS:CANCELLED`. `Close` is campaign-only and rejects with `BadRequest` on a one-shot board, so `IsClosed` is unreachable for every quest this phase can emit — no `IsClosed` clause is needed and adding one would be dead code.
- **D-10**: Quests use the same rolling window as events — the existing `CalendarFeedOptions.MonthsBack` / `MonthsAhead`.

### Claude's Discretion
- Where the one-shot narrowing happens. `GroupRepository.GetGroupsForUserAsync` already returns `BoardType` per board and is already called on this path. The one-shot group-id set is derivable from data the feed path holds in hand — no extra read, and `IBoardTypeResolver` must not be touched.
- Query shape and where it lives — a new method on the quest or player-signup repository, and how the two D-01 branches are expressed (a union, two queries, or one `||`). Mandatory regardless of shape: membership scoping with the pinned set, the `IgnoreQueryFilters()` comment discipline, and the second-layer in-memory re-check with `LogError`.
- Membership and board type are two independent predicates and both must apply.
- Deduplication of the D-01 union by quest id, before entries reach the writer.
- Extending `CalendarFeedSource` with a `Quest` member. `BuildUid` needs no change.
- `CalendarFeedEntry`'s shape for a source with no vote. `Availability` is a bare `VoteType` today and `BuildSummary` branches on it unconditionally. Making it nullable, defaulting it, or branching on `Source` are all open — but a quest must never pick up a `(maybe)` or `(declined)` suffix by accident.
- The window comparison's time basis. The feed computes `today` from `timeProvider.GetUtcNow().UtcDateTime`, events compare against a naive `DateOnly`, and `FinalizedDate` is a `DateTime` stored in server local time (standing known issue). Decide the comparison explicitly; do not silently mix the two.
- The new duration option's validation in `CalendarFeedOptions.IsValid()`.
- Minting the requirement family. Phase 85's `Requirements:` is `TBD`; Phase 84 minted `CALFEED-01`–`17` into `REQUIREMENTS.md` and `ROADMAP.md` in its own first plan and this phase should follow that precedent.

### Deferred Ideas (OUT OF SCOPE)
- Waitlisted sessions in the feed, with a marker.
- A `(DM)` suffix on sessions the reader runs.
- A separate, longer history window for sessions.
- Marking quest entries busy (`TRANSP:OPAQUE`).
- A cross-board quest list as a page in the application. This phase adds quests to a feed, not a screen.
- Campaign-board quests in the feed.
- The real-device subscription check (inherited from Phase 84, still open).
</user_constraints>

<phase_requirements>
## Phase Requirements

No requirement IDs exist yet — the ROADMAP lists `Requirements: TBD` for Phase 85. Phase 84 minted its `CALFEED-01`–`17` family in its own first plan (matching the precedent Phases 80–82 used), and 85-CONTEXT.md's own Claude's Discretion section directs this phase to follow it. This research recommends the planner mint a **new, phase-scoped prefix** rather than continuing the `CALFEED-*` numbering — every other topically-related-but-distinct phase in this project's history (EVENT → EVTAVAIL → EVTRECUR → EVTVIEW → EVTAGENDA → EVTNAME) minted its own prefix even when directly extending a prior phase's surface, so `CALFEED-*` staying event-only and a new prefix (e.g. `QUESTFEED-*`) covering the quest half is the pattern-consistent choice. This is a recommendation, not a locked decision — the planner has final say.

At minimum, the minted family should cover: the D-01 union/dedup query and its two branches (signup-owner, DM-owner), D-02/D-03 (confirmed-seat, all-roles), D-04 (`DungeonMasterSession` not filtered), D-05 (four-hour configurable block, timed-only), D-06 (`TRANSP:TRANSPARENT` parity), D-07/D-08 (no marker, no DM suffix), D-09 (silent disappearance, no `IsClosed` clause), D-10 (shared window), the `CalendarFeedSource.Quest` UID namespacing, and the second-layer tenant/board-type re-check.
</phase_requirements>

## Summary

Phase 84 shipped a small, composable feed pipeline: `CalendarSubscriptionService.GetFeedAsync` reads fresh membership, computes a `DateOnly` window, calls one repository method rooted at the source table, re-checks every row against the pinned membership set with `LogError` on any mismatch, projects to `CalendarFeedEntry`, and hands the list to `CalendarFeedWriter`. Every one of Phase 85's ten decisions plugs into this pipeline as an addition, not a rewrite: a second repository read, a second projection branch, one writer change (duration must become configurable rather than hard-coded to one hour), and one new enum member. `BuildUid` already namespaces by `CalendarFeedSource.ToString()`, so adding `Quest` to the enum is free — no writer change needed for UID collision safety, confirmed by reading `CalendarFeedWriter.BuildUid` directly.

The one place this phase's design genuinely diverges from Phase 84's shape is D-01's "union of two branches." CONTEXT.md frames this as a literal union requiring a mandatory distinct-by-id step, but the codebase already contains a proven, production-exercised alternative that satisfies the same requirement with no union and no distinct step at all: `GroupRepository.GetGroupsForUserAsync` (`GroupRepository.cs:32`) roots its query at `Groups` and filters with `g.UserGroups.Any(ug => ug.UserId == userId)` — a single query, one row per group, no duplicates possible because the query is rooted at the entity being returned. The exact same shape applies here: root at `Quests`, filter with `q.PlayerSignups.Any(ps => ps.PlayerId == userId && ps.IsSelected) || q.DungeonMasterId == userId`. Because the query root is `Quests`, each quest row is visited at most once — the "duplicate VEVENT sharing one UID" failure mode CONTEXT.md warns about cannot occur structurally, which satisfies the requirement's intent (no duplicate UID reaches the writer) without needing a literal `Union`/`Concat`+`DistinctBy` step. CONTEXT.md's own Claude's Discretion section explicitly leaves "a union, two queries, or one `||`" open, so this is a legitimate resolution, not a departure from a locked decision — but the research below documents both shapes since a reviewer may reasonably ask for the literal union framing, and the risk profile differs (see Pitfall 1).

The second area needing explicit design is the time model. `CalendarFeedEntry` was built for a source whose date and start time are two separate nullable-independent fields (`Event.Date: DateOnly`, `Event.StartTime: TimeOnly?`) drawn from a `DateOnly`-windowed query. `QuestEntity.FinalizedDate` is a single `DateTime?` and is documented in `PROJECT.md` as stored in **server local time**, while the feed's window bounds are computed from `timeProvider.GetUtcNow().UtcDateTime`. These two facts must be reconciled explicitly (see Common Pitfalls) rather than silently mixed, exactly as CONTEXT.md's Claude's Discretion section flags.

**Primary recommendation:** add a new `Task<IList<Quest>> GetFeedQuestsForUserAsync(int userId, IReadOnlyCollection<int> oneShotGroupIds, DateTime windowStart, DateTime windowEnd, CancellationToken token)` on `IQuestRepository`, rooted at `DbContext.Quests.IgnoreQueryFilters()` with the `Any()`/`||` predicate above — this is naturally deduplicated, translates through the same EF Core mechanism the codebase already proves in production (`GroupRepository.GetGroupsForUserAsync`), and needs no client-side distinct step. Derive `oneShotGroupIds` from the same `memberships` list `CalendarSubscriptionService.GetFeedAsync` already reads, filtered to `BoardType.OneShot` — this single set encodes both the membership predicate and the board-type predicate at once, because it can only ever be a subset of the fresh per-user membership read. Give `CalendarFeedEntry` a `TimeSpan Duration` property defaulting to `TimeSpan.FromHours(1)` (so every existing Phase 84 call site and unit test keeps working unmodified), have the quest projection set it from a new `CalendarFeedOptions.QuestDurationHours` (default 4), and have `CalendarFeedWriter.AppendTimedEvent` add `entry.Duration` instead of a hard-coded hour. Make `BuildSummary` branch on `entry.Source == CalendarFeedSource.Event` before consulting `Availability` at all, rather than relying on every future quest-entry construction site to remember to set `Availability` to a value whose suffix is empty — `VoteType.No` is the enum's `0` default, so an entry built without explicitly setting `Availability` silently renders `(declined)` today, which is exactly the accident CONTEXT.md's Claude's Discretion section warns against.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Quest feed read (D-01 union/predicate) | API / Backend | Database / Storage | `IgnoreQueryFilters()` + pinned one-shot-membership predicate lives in the Repository layer; enforced a second time in the Domain service's re-check, exactly Phase 84's split |
| One-shot board narrowing | API / Backend | — | Derived from the same per-request `GetGroupsForUserAsync` read the event branch already performs; no new read, no `IBoardTypeResolver` |
| Duration/summary source-awareness in the writer | API / Backend | — | Pure formatting logic over already-loaded entries; no view, no client code |
| Feed composition ordering (events + quests merged) | API / Backend | — | `CalendarSubscriptionService.GetFeedAsync` concatenates both entry lists and re-sorts before handing to the writer |

## Standard Stack

No new package is required for this phase. It extends an existing hand-rolled writer and an existing repository/service pair; it introduces no new I/O, no new external dependency, and no new UI surface (85-CONTEXT.md: "No new read surface in the application UI. This phase adds quests to a feed, not a page.").

## Package Legitimacy Audit

Not applicable — this phase adds no external package. `dotnet list package` was not re-run since no dependency changes.

## Architecture Patterns

### System Architecture Diagram

```
CalendarSubscriptionService.GetFeedAsync(feedToken)   [unchanged entry point, Phase 84]
        │
        ├─ 1. Token lookup, revoke/not-found short-circuit          (unchanged)
        │
        ├─ 2. memberships = groupService.GetGroupsForUserAsync(subscription.UserId)   (unchanged read,
        │      already returns BoardType per board -- GroupWithMemberCount.BoardType)
        │      memberGroupIds  = memberships.Select(m => m.Id)                         (unchanged)
        │      oneShotGroupIds = memberships.Where(m => m.BoardType == BoardType.OneShot)
        │                                    .Select(m => m.Id)                        (NEW -- Phase 85)
        │      boardNamesById  = memberships.ToDictionary(...)                         (unchanged, reused
        │                                                                                for quest board names too)
        │
        ├─ 3a. Event branch (unchanged): eventSignupRepository.GetFeedRowsForUserAsync(...)
        │
        ├─ 3b. Quest branch (NEW): questRepository.GetFeedQuestsForUserAsync(
        │        subscription.UserId, oneShotGroupIds, windowStartDateTime, windowEndDateTime, token)
        │      -- rooted at Quests, IgnoreQueryFilters(), predicate:
        │           oneShotGroupIds.Contains(q.GroupId)
        │        && q.IsFinalized && q.FinalizedDate != null
        │        && q.FinalizedDate >= windowStart && q.FinalizedDate <= windowEnd
        │        && (q.PlayerSignups.Any(ps => ps.PlayerId == userId && ps.IsSelected)
        │            || q.DungeonMasterId == userId)
        │      -- naturally one row per quest; no Union, no client-side Distinct needed
        │        (see Pattern 1 for the literal-union alternative and its extra step)
        │
        ├─ 4a. Event second-layer re-check (unchanged): memberGroupIds.Contains(row.Event.GroupId), LogError on mismatch
        │
        ├─ 4b. Quest second-layer re-check (NEW): oneShotGroupIds.Contains(quest.GroupId), LogError on mismatch
        │      -- oneShotGroupIds already encodes BOTH membership and board-type, because it is derived
        │         from the same fresh per-user membership read as memberGroupIds, filtered further
        │
        ├─ 5. Project both branches to CalendarFeedEntry, concatenate, and impose a defined order
        │      (Date, then StartTime, then Source, then SourceId -- events and quests interleave by
        │      date rather than appearing as two undifferentiated blocks)
        │
        └─ 6. writer.Write(entries, "D&D Quest Board")   -- AppendTimedEvent now adds entry.Duration
               instead of a hard-coded AddHours(1); AppendAllDayEvent is unreachable for Source == Quest
               (FinalizedDate always carries a real time, so entry.StartTime is always non-null for a
               quest entry -- the null-StartTime branch stays event-only, matching D-05's own framing)
```

### Recommended Project Structure
```
QuestBoard.Domain/
├── Enums/CalendarFeedSource.cs           # add Quest member -- BuildUid needs no change
├── Models/CalendarFeedEntry.cs           # add Duration (TimeSpan, default 1h)
├── Models/CalendarFeedOptions.cs         # add QuestDurationHours (default 4) + IsValid() clause
├── Interfaces/IQuestRepository.cs        # add GetFeedQuestsForUserAsync(...)
└── Services/CalendarSubscriptionService.cs   # add quest branch, oneShotGroupIds derivation,
                                               # quest re-check, merge + order, quest projection

QuestBoard.Repository/
└── QuestRepository.cs                    # implement GetFeedQuestsForUserAsync

QuestBoard.Domain/Services/CalendarFeedWriter.cs   # AppendTimedEvent: start.AddHours(1) -> start.Add(entry.Duration)
                                                     # BuildSummary: branch on entry.Source before Availability switch
```

### Pattern 1: `Any()`/`||` single-query predicate vs. literal Union + Distinct

**What:** Two ways to express D-01's "signup-owner OR DM-owner" rule.

**Recommended — single query, `Any()`/`||`, rooted at `Quests`:**
```csharp
// QuestBoard.Repository/QuestRepository.cs -- new method
public async Task<IList<Quest>> GetFeedQuestsForUserAsync(
    int userId, IReadOnlyCollection<int> oneShotGroupIds,
    DateTime windowStart, DateTime windowEnd, CancellationToken token = default)
{
    // Rooted at Quests, so each quest is visited at most once -- no duplicate UID can reach the
    // writer regardless of whether the DM also holds a selected signup on their own quest.
    // Precedent for Any()-in-Where translating and running in production against SQL Server:
    // GroupRepository.cs:32, Where(g => g.UserGroups.Any(ug => ug.UserId == userId)).
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
`[VERIFIED: QuestBoard.Repository/GroupRepository.cs:29-42]` for the `Any()`-in-`Where` precedent (`Where(g => g.UserGroups.Any(ug => ug.UserId == userId))`, line 32). `[VERIFIED: QuestBoard.Repository/Entities/QuestEntity.cs:9-62]` for `DungeonMasterId` (int, line 23), `FinalizedDate` (`DateTime?`, line 27), `IsFinalized` (bool, line 29), `PlayerSignups` (`ICollection<PlayerSignupEntity>`, line 62), `GroupId` (int, line 55). `[VERIFIED: QuestBoard.Repository/Entities/PlayerSignupEntity.cs:9-42]` for `PlayerId` (int, line 20), `IsSelected` (bool, line 17), `QuestId` (int, line 29).

**Alternative — literal union with mandatory distinct, if a reviewer wants the CONTEXT.md framing verbatim:**
```csharp
var byDm = DbContext.Quests.IgnoreQueryFilters()
    .Where(q => oneShotGroupIds.Contains(q.GroupId) && q.IsFinalized && q.FinalizedDate != null
        && q.FinalizedDate.Value >= windowStart && q.FinalizedDate.Value <= windowEnd
        && q.DungeonMasterId == userId);

var bySignup = DbContext.PlayerSignups.IgnoreQueryFilters()
    .Where(ps => ps.PlayerId == userId && ps.IsSelected
        && oneShotGroupIds.Contains(ps.Quest.GroupId) && ps.Quest.IsFinalized && ps.Quest.FinalizedDate != null
        && ps.Quest.FinalizedDate.Value >= windowStart && ps.Quest.FinalizedDate.Value <= windowEnd)
    .Select(ps => ps.Quest);

var entities = await byDm.Union(bySignup).ToListAsync(token);  // EF Core translates a same-shape Union
                                                                 // to SQL UNION when both sides return
                                                                 // the same entity type -- untested this
                                                                 // session against this exact shape
// A client-side .DistinctBy(q => q.Id) is still required if either branch is materialized separately
// (e.g. .ToListAsync() twice then .Concat()) rather than composed as one IQueryable Union.
```
This shape needs the explicit distinct step CONTEXT.md names as mandatory **only if** the two branches are materialized and concatenated in memory. If composed as a single `IQueryable<QuestEntity>.Union(...)` before `ToListAsync`, SQL `UNION` (not `UNION ALL`) itself already deduplicates by row identity server-side — but this project has no existing `Union()` call anywhere in the codebase to confirm this exact translation against SQL Server (`grep -rn "\.Union("` across `QuestBoard.Repository` and `QuestBoard.Domain` returned zero hits this session), so treat this path as requiring its own translation verification (see Common Pitfalls, Pitfall 1) if chosen over the recommended `Any()`/`||` shape.

**When to use which:** the `Any()`/`||` shape is recommended because it has an in-repo, production-proven translation precedent and needs no distinct step at all. Choose the union shape only if a reviewer specifically wants the query to visibly mirror CONTEXT.md's "two branches" framing; if so, budget time to verify its SQL translation before trusting it (Pitfall 1).

### Pattern 2: One-shot narrowing as a byproduct of the existing membership read (no new read, no `IBoardTypeResolver`)

**What:** `oneShotGroupIds` is computed in `CalendarSubscriptionService.GetFeedAsync` from the same `memberships` list already fetched for `memberGroupIds`/`boardNamesById` — no second database round trip.
```csharp
var memberships = await groupService.GetGroupsForUserAsync(subscription.UserId, token);
var memberGroupIds = memberships.Select(m => m.Id).ToList();
var oneShotGroupIds = memberships.Where(m => m.BoardType == BoardType.OneShot).Select(m => m.Id).ToList();
var boardNamesById = memberships.ToDictionary(m => m.Id, m => m.Name);
```
`[VERIFIED: QuestBoard.Domain/Services/CalendarSubscriptionService.cs:66-68]` for the existing `memberships`/`memberGroupIds`/`boardNamesById` lines this extends. `[VERIFIED: QuestBoard.Repository/GroupRepository.cs:29-42]` for `GetGroupsForUserAsync` already selecting `BoardType = (BoardType)g.BoardType` into `GroupWithMemberCount` (line 39). `[VERIFIED: QuestBoard.Domain/Models/GroupWithMemberCount.cs]`, quoted in full: `public class GroupWithMemberCount { public int Id { get; set; } public string Name { get; set; } = string.Empty; public DateTime CreatedAt { get; set; } public int MemberCount { get; set; } public BoardType BoardType { get; set; } }`.

**Why this resolves the "two independent predicates" risk correctly:** `oneShotGroupIds` is filtered from `memberships`, which is itself the fresh per-request membership read. It can never contain a board the caller is not a member of, and it can never contain a Campaign board. Passing this single set as the `Contains(...)` predicate therefore enforces both the membership rule and the board-type rule at once, by construction — there is no way to satisfy one without the other because both are derived from the same source list in the same line. The risk CONTEXT.md names (collapsing the two filters, or trusting one to cover the other) would only materialize if `oneShotGroupIds` were instead built from an independent query (e.g. "all boards where BoardType == OneShot", unscoped by membership) — that shape must not be used.

**When to use:** every place this phase touches board-type scoping. Do not resolve `IBoardTypeResolver` anywhere on this path — it answers for the *active* group and returns null by construction on a request with no session (85-CONTEXT.md's own framing, confirmed by this phase's design: the feed path never has an active group).

### Pattern 3: Second-layer re-check extended for the quest branch

**What:** After the quest query returns, re-filter in memory against `oneShotGroupIds` and `LogError` on any mismatch — the same pattern as the event branch, applied to the new set.
```csharp
var checkedQuests = fetchedQuests.Where(q => oneShotGroupIds.Contains(q.GroupId)).ToList();
if (checkedQuests.Count != fetchedQuests.Count)
{
    logger.LogError(
        "Calendar feed dropped {DroppedCount} of {FetchedCount} quest row(s) falling outside the " +
        "subscription owner's one-shot board set. The query is built from the same set, so this " +
        "indicates a lost or mistranslated board-type or membership predicate.",
        fetchedQuests.Count - checkedQuests.Count, fetchedQuests.Count);
}
```
`[VERIFIED: QuestBoard.Domain/Services/CalendarSubscriptionService.cs:84-91]` — this is the existing event re-check, quoted: `var checkedRows = fetched.Where(row => memberGroupIds.Contains(row.Event.GroupId)).ToList(); if (checkedRows.Count != fetched.Count) { logger.LogError(...) }`. The quest re-check reproduces this exact shape with `oneShotGroupIds` in place of `memberGroupIds` and `q.GroupId` in place of `row.Event.GroupId`.

### Pattern 4: Source-aware duration without a per-source writer branch

**What:** Give `CalendarFeedEntry` a `Duration` property with a default that preserves every existing Phase 84 behaviour unmodified.
```csharp
// QuestBoard.Domain/Models/CalendarFeedEntry.cs -- add:
public TimeSpan Duration { get; set; } = TimeSpan.FromHours(1);   // Event's existing fixed one-hour
                                                                    // block (84 D-02), now explicit
                                                                    // rather than hard-coded in the writer

// QuestBoard.Domain/Services/CalendarFeedWriter.cs -- AppendTimedEvent, change:
//   var end = start.AddHours(1);
// to:
var end = start.Add(entry.Duration);
```
This means the *existing* Event projection in `CalendarSubscriptionService.GetFeedAsync` needs no change at all (the default already matches D-02's one-hour block), and only the *new* quest projection needs to set `Duration = TimeSpan.FromHours(options.QuestDurationHours)` explicitly. `[VERIFIED: QuestBoard.Domain/Services/CalendarFeedWriter.cs:68-82]`, quoted: `var start = entry.Date.ToDateTime(entry.StartTime!.Value); var end = start.AddHours(1);` (lines 70-71) is the exact line this pattern changes. `[VERIFIED: QuestBoard.UnitTests/Services/CalendarFeedWriterTests.cs:14-36]` — the existing `MakeEntry` test helper constructs `CalendarFeedEntry` without ever setting a duration-like field, confirming a property-level default is the change that avoids touching every existing golden-byte assertion.

### Pattern 5: `BuildSummary` branches on `Source`, not on `Availability` alone

**What:** Guard the vote-suffix logic behind an explicit source check rather than relying on every quest-entry construction site remembering to set `Availability` to a value whose switch case is empty.
```csharp
// QuestBoard.Domain/Services/CalendarFeedWriter.cs -- BuildSummary, change the suffix computation to:
var suffix = entry.Source == CalendarFeedSource.Event
    ? entry.Availability switch
    {
        VoteType.Maybe => " (maybe)",
        VoteType.No => " (declined)",
        _ => string.Empty,
    }
    : string.Empty;
```
This is a defensive, not merely stylistic, change: `[VERIFIED: QuestBoard.Domain/Enums/VoteType.cs]`, quoted in full: `public enum VoteType { No, Maybe, Yes }` — `No` is the enum's `0` default. A `CalendarFeedEntry` constructed without explicitly setting `Availability` (C#'s default-value behaviour for a value-typed property with no initializer) silently carries `VoteType.No`, which the *current* unconditional switch renders as `" (declined)"`. Branching on `Source` first removes this landmine entirely rather than depending on every present and future call site remembering to set `Availability = VoteType.Yes` for a quest.

### Anti-Patterns to Avoid
- **Building the quest query as a variant of `GetQuestsWithSignupsForRoleAsync` or any other existing `QuestRepository` method.** Every existing method is scoped to the *active* group via the ambient query filter; none of them accepts a cross-board membership set, and none of them expresses the DM-OR-signup union. `[VERIFIED: QuestBoard.Repository/QuestRepository.cs:1-115]` — no existing method takes an `IReadOnlyCollection<int>` group-id parameter or bypasses the filter.
- **Deriving `oneShotGroupIds` from a query that is not already scoped to `memberships`.** A separate "all one-shot boards" read, unfiltered by the caller's own membership, would satisfy the board-type predicate while silently dropping the membership predicate — exactly Risk 2 from the phase description.
- **Reaching for `IBoardTypeResolver` anywhere in this feed's read path.** It resolves the *active* group's type and returns null by construction on this session-less, no-active-group request.
- **Relying on `VoteType`'s default value to keep the suffix empty for a quest entry.** See Pattern 5 — `VoteType.No` is `0`, not a safe default.
- **Widening `AppendTimedEvent`'s hard-coded one-hour block silently, without a `Duration` field or equivalent explicit knob**, which would make Event and Quest entries indistinguishable in the writer and defeat D-05's configurability requirement.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Cross-board "OR" membership test across two navigation paths | A manual two-list-then-merge-in-C# dedup routine | EF Core's `Any()`-in-`Where` composed with `||`, exactly as `GroupRepository.GetGroupsForUserAsync` already does in production | Already proven to translate and run correctly in this codebase against the real database; a hand-rolled in-memory merge reintroduces the exact distinct-by-id bookkeeping CONTEXT.md is warning about, for no benefit |
| Quest-entry duration math | A per-source `if` inside the writer's timed-event branch | A `Duration` property on `CalendarFeedEntry` with a source-appropriate default | Keeps the writer source-agnostic (it already is, for everything except this one hard-coded hour) and matches the existing pattern where the service layer supplies fully-formed entries and the writer only formats them |

**Key insight:** this phase's entire risk surface is compositional, not novel — every mechanism it needs (pinned-membership `IgnoreQueryFilters()` bypass, second-layer re-check, `Any()`-in-`Where` translation, `TimeSpan`-based duration) already exists somewhere in this codebase and has already been proven correct there. The work is wiring, not invention.

## Common Pitfalls

### Pitfall 1: Trusting an untested `Union()` translation over the proven `Any()`/`||` shape
**What goes wrong:** A plan implements D-01 as a literal `IQueryable.Union()` of two separately-rooted queries (one at `Quests`, one at `PlayerSignups`) because CONTEXT.md's prose describes "a union of two branches," and assumes EF Core 10 translates it to a single `SELECT ... UNION SELECT ...` that deduplicates server-side.
**Why it happens:** the CONTEXT.md language is accurate about the *requirement* (rows from two logically distinct conditions, deduplicated by quest id) but is not itself a mandate for the SQL `UNION` operator specifically — Claude's Discretion explicitly leaves "a union, two queries, or one `||`" open. `grep -rn "\.Union("` across `QuestBoard.Repository` and `QuestBoard.Domain` returned zero hits this session — there is no in-repo precedent proving this project's EF Core/SQL Server combination translates a `Union()` of two entity-typed queries correctly, and this session did not execute one against the real database to confirm it independently.
**How to avoid:** prefer the `Any()`/`||` single-query shape (Pattern 1), which has a direct, already-production-proven precedent (`GroupRepository.cs:32`). If a reviewer specifically wants the literal union shape, budget a step to call `.ToQueryString()` on the composed `IQueryable` during development (this does not require a live database connection for a relational provider, though it does require the EF Core SQL Server provider rather than the InMemory provider the test harness uses) and manually inspect the generated SQL for a single `UNION`/`UNION ALL` with the expected `WHERE` clauses, rather than trusting it compiles and assuming correctness.
**Warning signs:** an `InvalidOperationException` at runtime naming "could not be translated" (EF Core's standard client-evaluation-fallback error), or — more dangerously — no exception at all but a silently wrong row count, since `UNION ALL` (which some EF Core versions have been reported to emit for `Concat` rather than `Union`) does *not* deduplicate.

### Pitfall 2: The test harness cannot verify either query shape's relational translation
**What goes wrong:** A plan ships full green tests and treats that as proof the query works against the production SQL Server database.
**Why it happens:** `[VERIFIED: QuestBoard.IntegrationTests/Tests/CalendarSubscriptionFeedTests.cs:9-27]` — this exact test class's own doc comment, read directly this session, states: *"the shared harness backs every suite with the EF Core InMemory provider, which evaluates every predicate as ordinary LINQ-to-Objects... the board containment test (memberGroupIds.Contains(...)) over an empty id collection is exercised only as List.Contains, never as its SQL translation."* `[VERIFIED: QuestBoard.IntegrationTests/Tests/AgendaTenantIsolationTests.cs:17-27]` states the same caveat for Phase 82's cross-board query, and a repo-wide search this session (`grep -rln "SqlServer\|UseSqlServer\|Relational" QuestBoard.IntegrationTests/ QuestBoard.UnitTests/`) found **zero** relational-provider test files anywhere in the codebase — this gap has been carried unaddressed since at least Phase 82 and Phase 84, both of which explicitly named it and neither of which closed it.
**How to avoid:** this phase's `Any()`/`||` predicate is simpler than Phase 84's `.Contains()`-over-a-collection pattern and follows an already-shipped precedent, which lowers but does not eliminate this risk. Do not treat green InMemory-provider integration tests as proof of SQL Server translation correctness. At minimum, note this explicitly in the plan's verification section as an inherited, not newly introduced, gap — consistent with how Phase 84 documented rather than silently accepted it.
**Warning signs:** none observable pre-deployment under the current harness — this is a structural blind spot, not a symptom to watch for.

### Pitfall 3: Mixing a UTC-computed window against a server-local-time `FinalizedDate`
**What goes wrong:** The quest branch reuses `windowStart`/`windowEnd` (currently `DateOnly`, computed as `DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime)`, see `CalendarSubscriptionService.cs:71-73`) directly against `FinalizedDate` (a `DateTime?` stored in server local time per the project's own documented known issue) without reconciling the two time bases.
**Why it happens:** `[VERIFIED: .planning/PROJECT.md:168]`, quoted: `"FinalizedDate stored as server local time (CET/CEST) — reminder job uses DateTime.Today.AddDays(1) which is correct for LXC host timezone but should be reviewed if deployment timezone changes"`. `[VERIFIED: QuestBoard.Domain/Services/CalendarSubscriptionService.cs:71-73]`, quoted: `var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime); var windowStart = today.AddMonths(-options.MonthsBack); var windowEnd = today.AddMonths(options.MonthsAhead);`. `[VERIFIED: QuestBoard.Domain/Services/QuestService.cs:182-183]` shows this exact class of mismatch already exists elsewhere in the codebase, quoted: `q.FinalizedDate.HasValue && q.FinalizedDate.Value.Date <= DateTime.UtcNow.AddDays(-1).Date` — comparing a server-local-time value against a UTC-now-derived bound is pre-existing, unaddressed tech debt in this project, not a hypothetical.
**How to avoid:** at `MonthsBack`/`MonthsAhead` granularity (a window measured in months, not hours), the CET/CEST-vs-UTC offset (1-2 hours) cannot move a `FinalizedDate` across a month boundary except within a few hours of the boundary itself — the practical impact is negligible, but it must be a **stated, deliberate** choice in the plan (convert `windowStart`/`windowEnd` to `DateTime` via `windowStart.ToDateTime(TimeOnly.MinValue)` / `windowEnd.ToDateTime(TimeOnly.MaxValue)`, both still UTC-derived) rather than silently inherited. Do not attempt to "fix" the server-local-time storage as part of this phase — that is out of scope and is a standing, separately-tracked known issue.
**Warning signs:** a UAT report of a quest appearing or disappearing from the feed exactly at a month-boundary window edge, near local midnight.

### Pitfall 4: `FinalizedDate` is one `DateTime`; `CalendarFeedEntry` wants a `DateOnly` + `TimeOnly?` pair
**What goes wrong:** The quest-to-entry projection passes `FinalizedDate` somewhere `CalendarFeedEntry.Date` (`DateOnly`) or `StartTime` (`TimeOnly?`) is expected without an explicit split, causing a compile error or, worse, an accidental truncation.
**Why it happens:** `[VERIFIED: QuestBoard.Repository/Entities/QuestEntity.cs:27]`, quoted: `public DateTime? FinalizedDate { get; set; }` — a single field carrying both date and time, unlike `EventEntity`'s already-split `Date`/`StartTime` pair.
**How to avoid:** project explicitly: `Date = DateOnly.FromDateTime(quest.FinalizedDate!.Value)`, `StartTime = TimeOnly.FromDateTime(quest.FinalizedDate.Value)`. Because the query predicate already guarantees `FinalizedDate != null` for every row that reaches this point (D-01's window filter), the `!.Value` is safe there, but the projection code should still assert or filter defensively rather than trust the query shape never changes underneath it.
**Warning signs:** a null-reference exception in the projection step, or (if a nullable-forgiving `!` is used incorrectly elsewhere) a quest silently rendering at midnight instead of its real finalized time.

### Pitfall 5: Forgetting `IgnoreQueryFilters()` on the `Quests`/`PlayerSignups` read
**What goes wrong:** The new query omits `IgnoreQueryFilters()`, and every row silently vanishes because `QuestEntity`'s and `PlayerSignupEntity`'s own ambient filters fail closed on a null `ActiveGroupId` (which this endpoint always has, by construction).
**Why it happens:** `[VERIFIED: QuestBoard.Repository/Entities/QuestBoardContext.cs:393-396]`, quoted: `modelBuilder.Entity<QuestEntity>().HasQueryFilter(e => activeGroupContext.ActiveGroupId != null && e.GroupId == activeGroupContext.ActiveGroupId);`. `[VERIFIED: QuestBoard.Repository/Entities/QuestBoardContext.cs:425-428]`, quoted: `modelBuilder.Entity<PlayerSignupEntity>().HasQueryFilter(ps => activeGroupContext.ActiveGroupId != null && ps.Quest.GroupId == activeGroupContext.ActiveGroupId);` — both entities in the D-01 predicate carry a fail-closed filter, and `PlayerSignupEntity`'s filter reaches through its `Quest` navigation, so it is filtered even when queried only implicitly via `q.PlayerSignups.Any(...)`.
**How to avoid:** `IgnoreQueryFilters()` on the root `DbContext.Quests` query call disables the filter for the whole query tree, including the `PlayerSignups` navigation reached inside `Any()` — this mirrors the note already recorded for the Phase 82 agenda query ("`IgnoreQueryFilters()` disables the filter for the whole query, including the `Signups` and `User` includes... it is safe because every included row hangs off an `Event` whose `GroupId` is pinned to the membership set" — the same reasoning applies here with `Quest.GroupId` pinned via `oneShotGroupIds`).
**Warning signs:** the feed silently omits every quest for every subscriber — indistinguishable from "no quests qualify" without a failing test that seeds a genuinely-qualifying quest and asserts it appears.

### Pitfall 6: `CalendarFeedSource.Quest` must exist before the query/projection code compiles against it
**What goes wrong:** implementation order accidentally writes the projection/writer changes before adding the enum member, or adds the enum member but forgets `BuildUid`'s reliance on `source.ToString().ToLowerInvariant()` already being enum-name-driven (it is, and needs no change — but a plan that "adds a Quest case to BuildUid" is doing unnecessary, riskier work).
**Why it happens:** `[VERIFIED: QuestBoard.Domain/Services/CalendarFeedWriter.cs:126-133]`, quoted: `return $"questboard-{source.ToString().ToLowerInvariant()}-{sourceId}";` — this line already generalizes over any enum member; adding `Quest` to `CalendarFeedSource` alone yields `questboard-quest-{id}` with zero further writer change, exactly as 85-CONTEXT.md's Claude's Discretion section states ("`BuildUid` needs no change").
**How to avoid:** add the enum member first; do not touch `BuildUid`.
**Warning signs:** a code review comment proposing a `switch` inside `BuildUid` — unnecessary and a sign the existing generalization was missed.

## Code Examples

### `CalendarFeedOptions` extension (D-05's configurable duration)
```csharp
// QuestBoard.Domain/Models/CalendarFeedOptions.cs -- add:
public int QuestDurationHours { get; set; } = 4;

// IsValid() -- extend the existing refuse-to-start guard:
public bool IsValid() => MonthsBack >= 0 && MonthsAhead >= 1 && LastFetchedThrottleMinutes >= 1
    && RetentionDays >= 1 && QuestDurationHours >= 1;
```
`[VERIFIED: QuestBoard.Domain/Models/CalendarFeedOptions.cs]` — full file read this session; quoted existing `IsValid()`: `public bool IsValid() => MonthsBack >= 0 && MonthsAhead >= 1 && LastFetchedThrottleMinutes >= 1 && RetentionDays >= 1;` (line 29), the exact line this extends.

### Quest projection in `CalendarSubscriptionService.GetFeedAsync`
```csharp
var quests = await questRepository.GetFeedQuestsForUserAsync(
    subscription.UserId, oneShotGroupIds, windowStart.ToDateTime(TimeOnly.MinValue), windowEnd.ToDateTime(TimeOnly.MaxValue), token);

var checkedQuests = quests.Where(q => oneShotGroupIds.Contains(q.GroupId)).ToList();
if (checkedQuests.Count != quests.Count)
{
    logger.LogError(
        "Calendar feed dropped {DroppedCount} of {FetchedCount} quest row(s) falling outside the subscription owner's one-shot board set.",
        quests.Count - checkedQuests.Count, quests.Count);
}

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
        // Availability left at its default -- BuildSummary now branches on Source before consulting
        // it at all (Pattern 5), so no suffix can leak regardless of this value.
    })
    .ToList();

var entries = entries.Concat(questEntries)
    .OrderBy(e => e.Date)
    .ThenBy(e => e.StartTime ?? TimeOnly.MaxValue)
    .ThenBy(e => e.Source)
    .ThenBy(e => e.SourceId)
    .ToList();
```
This is this session's synthesis grounded in the verified existing event-branch code (`CalendarSubscriptionService.cs:78-105`) and the verified entity/model shapes cited throughout — not itself a verified file (no such code exists yet).

## State of the Art

Not applicable — this phase extends an internal pipeline shipped four days prior in the same milestone; there is no external ecosystem or "current best practice" dimension beyond what Phase 84's own RESEARCH.md already established (RFC 5545 mechanics, client refresh behaviour) and which this phase explicitly inherits without re-verifying.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | The `Any()`/`||` predicate shape (Pattern 1) translates to a single, correct SQL query against SQL Server, mirroring `GroupRepository.GetGroupsForUserAsync`'s already-proven translation | Architecture Patterns, Pattern 1 | If EF Core 10 translates the combined `Any()` + `||` + `IgnoreQueryFilters()` shape differently than the simpler `Any()`-only precedent, the query could throw a client-evaluation exception at runtime or (worse) silently return an incorrect row set. Mitigated by recommending a `.ToQueryString()` sanity check during implementation and by the query's structural inability to produce duplicate rows even under partial mistranslation of the `Any()` clause (the `||` and `IgnoreQueryFilters()` pieces are independently precedented elsewhere in this codebase) |
| A2 | Recommending a new requirement-ID prefix (e.g. `QUESTFEED-*`) rather than continuing `CALFEED-*` numbering is the pattern-consistent choice | Phase Requirements | Low risk either way — this is explicitly left to the planner's discretion by 85-CONTEXT.md itself; if the planner disagrees, no rework beyond renumbering a table is needed |
| A3 | A 1-2 hour UTC-vs-server-local-time discrepancy in the window boundary (Pitfall 3) has negligible practical impact given a multi-month window | Common Pitfalls, Pitfall 3 | If wrong (e.g. a deployment moves to a timezone with a much larger UTC offset), a quest could appear or disappear from the feed a day early/late at the window edge — cosmetic, self-correcting at the next poll, not a data-safety issue |

## Open Questions

1. **Exact requirement-ID prefix.**
   - What we know: `Requirements: TBD` in the ROADMAP; Phase 84 minted its own family as its first plan.
   - What's unclear: whether the planner extends `CALFEED-*` or mints a new prefix.
   - Recommendation: mint a new prefix (see Phase Requirements section) — but this is genuinely the planner's call, not locked.

2. **Union vs. `Any()`/`||` query shape — final choice.**
   - What we know: both satisfy D-01's stated requirement; the `Any()`/`||` shape has a direct in-repo production precedent, the literal union does not.
   - What's unclear: whether a code reviewer will prefer the query to visibly mirror CONTEXT.md's "two branches" prose.
   - Recommendation: ship the `Any()`/`||` shape; if review pushback favors the literal union, budget the `.ToQueryString()` verification step called out in Pitfall 1 before merging it.

3. **Whether to add a lightweight relational-translation check given the harness's documented blind spot (Pitfall 2).**
   - What we know: no relational test infrastructure exists anywhere in this codebase today; this gap was already named and left open by both Phase 82 and Phase 84.
   - What's unclear: whether this phase is the right one to finally close it, versus continuing to document and defer it.
   - Recommendation: document it as an inherited gap in the plan's verification section (matching Phase 84's own treatment) rather than silently deferring it a third time with no record — but do not treat closing the gap as in-scope for this phase's goal unless the operator says otherwise.

## Environment Availability

Not applicable — this phase introduces no new external dependency, service, or tool. The .NET 10 SDK, SQL Server, and Docker availability already confirmed in Phase 84's research are unchanged.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit v3 (`xunit.v3`) + FluentAssertions, unchanged from Phase 84 `[VERIFIED: QuestBoard.UnitTests/Services/CalendarFeedWriterTests.cs and QuestBoard.IntegrationTests/Tests/CalendarSubscriptionFeedTests.cs — both read directly this session, same stack as cited in 84-RESEARCH.md]` |
| Config file | none dedicated — standard `dotnet test` per-project convention |
| Quick run command | `dotnet test QuestBoard.UnitTests --filter CalendarFeedWriterTests` |
| Full suite command | `dotnet test` |

### Phase Requirements → Test Map
No REQ-IDs exist yet (see `<phase_requirements>`); the table below maps CONTEXT.md decisions to the test shape they need.

| Decision | Behavior | Test Type | Automated Command | File Exists? |
|----------|----------|-----------|-------------------|-------------|
| D-01 (both branches) | A DM-only quest (no signup row) and a signup-only quest (not DM) both appear; a quest where the reader is both DM and holds a selected signup appears exactly once (one VEVENT, one UID) | unit (writer) + integration (query) | `dotnet test QuestBoard.IntegrationTests --filter CalendarSubscriptionQuestFeedTests` | ❌ Wave 0 |
| D-02/D-03 | A waitlisted (`IsSelected == false`) signup never appears; Spectator and AssistantDM signups appear exactly like Player | integration | same file | ❌ Wave 0 |
| D-04 | A `DungeonMasterSession` quest still appears for a seated player | integration | same file | ❌ Wave 0 |
| D-05 | Timed entry, `DTEND` exactly `QuestDurationHours` after `DTSTART`; never an all-day entry | unit (writer, golden-byte) | `dotnet test QuestBoard.UnitTests --filter CalendarFeedWriterTests` | ❌ Wave 0 (extend existing file) |
| D-06/D-07/D-08 | `TRANSP:TRANSPARENT`; `SUMMARY` is `[Board] Title` with no marker, no `(DM)` suffix, and never a `(maybe)`/`(declined)` suffix regardless of `Availability`'s value | unit (writer) | same command | ❌ Wave 0 (extend existing file) |
| D-09 | Un-finalizing, deleting, moving the finalized date, or losing the seat removes the quest from the feed at the next fetch; a closed quest never reaches the query in the first place (campaign-only `Close`) | integration | `dotnet test QuestBoard.IntegrationTests --filter CalendarSubscriptionQuestFeedTests` | ❌ Wave 0 |
| D-10 | A quest just inside/outside the shared `MonthsBack`/`MonthsAhead` window is included/excluded identically to an event | integration | same file | ❌ Wave 0 |
| Board-type narrowing (Claude's Discretion) | A finalized, signed-up quest on a **Campaign** board never appears, even though the reader is a member | integration, two-board-type | same file | ❌ Wave 0 |
| Tenant isolation (second-layer re-check) | A quest on a board the reader is not a member of never appears; a board the reader left disappears on the next fetch | integration, two-group | same file | ❌ Wave 0 |
| UID collision (Pitfall 6) | An event and a quest sharing the same integer id produce two distinct, non-colliding UIDs | unit (writer) | `dotnet test QuestBoard.UnitTests --filter CalendarFeedWriterTests` | ❌ Wave 0 (extend existing file) |

### Sampling Rate
- **Per task commit:** `dotnet test QuestBoard.UnitTests` (the writer's duration/summary/UID changes are pure-function and should dominate this suite for this phase)
- **Per wave merge:** `dotnet test` (full solution, catches the new tenant/board-type isolation suite)
- **Phase gate:** Full suite green before `/gsd-verify-work`. No new real-device check is required — this phase inherits Phase 84's deferred real-device gap (85-CONTEXT.md, Inherited assumptions) and must not claim it closes that gap.

### Wave 0 Gaps
- [ ] `QuestBoard.IntegrationTests/Tests/CalendarSubscriptionQuestFeedTests.cs` — new file, mirroring `CalendarSubscriptionFeedTests.cs`'s structure and its documented InMemory-provider caveat (copy the caveat doc-comment; it applies identically here per Pitfall 2), covering D-01 dedup, D-02/D-03/D-04 predicates, D-09 disappearance cases, D-10 window edges, board-type narrowing, and tenant isolation
- [ ] `QuestBoard.UnitTests/Services/CalendarFeedWriterTests.cs` — extend (not replace) with Quest-source cases: configurable duration, no-suffix-ever guarantee (including an explicit `VoteType.No`-with-`Source.Quest` case proving Pitfall 5's fix), UID namespacing against a colliding numeric id
- [ ] No framework install needed — xUnit v3/FluentAssertions are already solution-wide dependencies

## Security Domain

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-------------------|
| V4 Access Control | Yes — the primary new risk surface | Two independent predicates (membership, board type) combined via `oneShotGroupIds`, enforced at query time and re-checked in memory (Pattern 2/3); inherits Phase 84's `IgnoreQueryFilters()` + pinned-set discipline |
| V5 Input Validation | No new surface | This phase adds no new user-facing input; `feedToken` validation is unchanged from Phase 84 |
| V2/V3/V6 | No change | Unchanged from Phase 84 — this phase adds no new authentication, session, or cryptographic surface |

### Known Threat Patterns for this stack

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|----------------------|
| Cross-tenant quest leak via a dropped or collapsed board-type/membership predicate | Elevation of Privilege / Information Disclosure | `oneShotGroupIds` derived solely from the fresh per-request `memberships` read (Pattern 2), never from an independent board-type query; second-layer re-check with `LogError` (Pattern 3) — the same two prior real incidents this codebase has had (Phases 49/55) are the reason this defense-in-depth shape is mandatory here too |
| A quest a DM only sees via the `DungeonMasterId` branch bypassing the one-shot-board narrowing | Elevation of Privilege | The `oneShotGroupIds.Contains(q.GroupId)` predicate applies before the `Any()`/`||` OR-branch, not after — both branches are inside the same `Where`, so neither can independently escape the board-type scoping. This must be verified in the query's exact clause ordering; a plan that applies the OR-branch as a `.Where().Concat(otherQuery)` at the C# level rather than inside one predicate would reintroduce exactly this risk |

## Sources

### Primary (HIGH confidence — read directly from the repository this session)
- `QuestBoard.Domain/Services/CalendarSubscriptionService.cs` (full file)
- `QuestBoard.Domain/Services/CalendarFeedWriter.cs` (full file)
- `QuestBoard.Domain/Models/CalendarFeedEntry.cs`, `CalendarFeedOptions.cs`, `CalendarSubscription.cs`, `GroupWithMemberCount.cs` (full files)
- `QuestBoard.Domain/Enums/CalendarFeedSource.cs`, `VoteType.cs`, `SignupRole.cs`, `BoardType.cs` (full files)
- `QuestBoard.Repository/EventSignupRepository.cs`, `GroupRepository.cs`, `QuestRepository.cs` (lines 1-120), `PlayerSignupRepository.cs` (partial)
- `QuestBoard.Domain/Interfaces/IEventSignupRepository.cs`, `IQuestRepository.cs`, `IPlayerSignupRepository.cs`, `IGroupService.cs`
- `QuestBoard.Repository/Entities/QuestEntity.cs`, `PlayerSignupEntity.cs` (full files), `QuestBoardContext.cs` (lines 380-429, query filter block)
- `QuestBoard.Repository/Entities/QuestBoardContext.cs` grep confirming `QuestEntity`/`PlayerSignupEntity` filter locations
- `QuestBoard.Service/Controllers/QuestBoard/QuestController.cs` (lines 95-115, 245-265, 295-315, 470-495, 745-770)
- `QuestBoard.Service/Controllers/CalendarFeedController.cs` (full file)
- `QuestBoard.IntegrationTests/Tests/CalendarSubscriptionFeedTests.cs` (lines 1-60), `AgendaTenantIsolationTests.cs` (lines 1-40)
- `QuestBoard.UnitTests/Services/CalendarFeedWriterTests.cs` (lines 1-60)
- `.planning/phases/84-calendar-feed-foundation-and-event-subscription/84-CONTEXT.md`, `84-RESEARCH.md`, `84-PATTERNS.md` (full files)
- `.planning/phases/82-personal-cross-board-event-agenda/82-CONTEXT.md` (decisions D-01 through D-18, lines 1-330)
- `.planning/phases/85-one-shot-quests-in-the-calendar-feed/85-CONTEXT.md` (full file)
- `.planning/PROJECT.md` (line 168, `FinalizedDate` known issue)
- `.planning/REQUIREMENTS.md`, `.planning/STATE.md` (full files)
- `QuestBoard.Repository/QuestBoard.Repository.csproj` — EF Core version (`10.0.9`)
- `grep -rn "\.Union(\|\.Concat(\|\.DistinctBy(\|GroupBy(" QuestBoard.Repository/*.cs QuestBoard.Domain/**/*.cs` and `grep -rln "SqlServer\|UseSqlServer\|Relational" QuestBoard.IntegrationTests/ QuestBoard.UnitTests/` — both run directly this session, confirming zero `Union()` precedent and zero relational-provider test infrastructure

### Secondary (MEDIUM confidence)
- None — every substantive claim in this document was either read directly from the repository this session or is this session's own synthesis clearly marked as unverified (the literal-union translation path, Pattern 1's alternative).

### Tertiary (LOW confidence)
- None carried forward from Phase 84 — this phase makes no new client-behaviour (Apple/Google/Outlook) claims; it inherits Phase 84's existing LOW-confidence client-behaviour findings unchanged and does not re-litigate them.

## Metadata

**Confidence breakdown:**
- Standard stack: N/A — no new dependency
- Architecture: HIGH — every existing-code claim was read directly this session with line numbers and verbatim quotes; the one recommended-but-unverified piece (Any()/|| SQL translation) is clearly flagged as MEDIUM and distinguished from the verified precedent it's based on
- Pitfalls: HIGH for the codebase-grounded pitfalls (query filters, entity shapes, enum defaults, time-basis mismatch — all read directly); MEDIUM for the SQL-translation risk specifically, since no relational test exists in this codebase to close it

**Research date:** 2026-09-18
**Valid until:** 30 days, or immediately upon any change to `CalendarSubscriptionService.cs`, `CalendarFeedWriter.cs`, `QuestEntity.cs`, or `QuestBoardContext.cs`'s query filter block, whichever comes first — this document is grounded in the exact shipped bytes of those files as of this session.
