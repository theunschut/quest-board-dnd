# Phase 75: Event Availability Signups - Research

**Researched:** 2026-08-27
**Domain:** ASP.NET Core 10 MVC / EF Core 10 — internal codebase architecture (no new external libraries)
**Confidence:** HIGH

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

30 decisions (D-01…D-30) are locked in `75-CONTEXT.md`. Full text is in that file; the load-bearing ones this research expands on:

- **D-01/D-02/D-06/D-07:** `Views/Events/Details.cshtml` is the only availability surface — three Yes/Maybe/No buttons following the `changeVote()` idiom, a named roster visible to every board member, and a `revokeSignup()`-style Withdraw action that deletes the row (One-Shot only).
- **D-08/D-09:** Withdraw is server-side-gated to One-Shot boards only (`QuestController.Close`/`Reopen` precedent, never trust client markup). Every write takes the acting user from `User`, never the request body.
- **D-10/D-11/D-12:** Every human-initiated write (including the row's creation) stamps `UpdatedAt`; auto-signup passes never do. Surface this as `HasAnswered` on the domain model. Rewrite the stale entity comment.
- **D-14–D-19:** Campaign fan-out writes a row for every member (any role) at event-create time, in the same unit of work as the event, regardless of date. Joining-member backfill boundary is `Date >= today`. Hook at `GroupService.AddMemberAsync` (a verified single chokepoint). Membership + backfill must be atomic — both or neither.
- **D-20–D-24:** Leaving deletes every signup a member holds on that board, past and future. Hook at `GroupService.RemoveMemberAsync` (also a verified single chokepoint). The Platform Remove Member control gains a confirmation.
- **D-25–D-27:** The event-delete `confirm()` gains a count of all signup rows (not just `HasAnswered` ones) that will be destroyed. No DB work needed for delete — cascade already ships.
- **D-28/D-29:** Defence in both layers — the query filter constrains reads only; every write must independently verify board ownership and actor identity. A dedicated two-group integration test is mandatory (EVTAVAIL-05), following `TenantIsolationTests.cs`.
- **D-30:** Signup writes use narrow scalar-update repository methods mirroring `PlayerSignupRepository.ChangeVoteAsync` — never the generic `BaseRepository.UpdateAsync`.

### Claude's Discretion

- Whether a past event still accepts new/changed answers (either is defensible; pick one and test it).
- Roster ordering and empty-state copy on a One-Shot board with no answers.
- Notification behaviour on an availability change (default: none).
- Inline vs Hangfire job for the join-time backfill (research below: inline is correct — see Pitfall 1).
- Domain/repository/service/controller naming, file placement, AutoMapper profile entries.
- Whether the roster is inline in `Events/Details.cshtml` or a partial.
- Whether `EventEntity` gains a `Signups` navigation collection, and the load shape (research below has a concrete recommendation).
- Test structure beyond the mandated two-group isolation test and the D-08 board-type-enforcement test.
- `FK_EventSignups_AspNetUsers_UserId` has no cascade — a non-issue today since the app never hard-deletes user accounts.

### Deferred Ideas (OUT OF SCOPE)

- An idempotent "sync availability" repair pass — aimed at Phase 76, where the materialization job needs it; D-19's atomicity removes the failure this phase would need it for.
- Automatically purging past events — considered and declined; past events stay exactly as they are.
- Distinguishing an untouched default from a real answer in the UI — Phase 77 (EVTVIEW-02); this phase only records the data (D-10/D-11).
- A per-event availability count on the details page or calendar — Phase 77 (EVTVIEW-03).
- Marking availability on the calendar chip or mobile agenda — rejected by D-05; `_Calendar.cshtml`, `Calendar/Index.cshtml`, `Calendar/Index.Mobile.cshtml` are untouched this phase.
- Guarding against a remove-and-re-add losing deliberate answers — accepted cost of D-20/D-22.

</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| EVTAVAIL-01 | One-Shot board: optional signup, Yes/Maybe/No, no row until created | `EventSignupRepository.SetAvailabilityAsync` (create-on-first-click) + `WithdrawAsync`; see Code Examples |
| EVTAVAIL-02 | Campaign board: every member auto-signed-up Yes from event creation; opt-out flips to No | Create-time fan-out in `EventsController.Create`, single unit of work with `eventService.AddAsync`; see Architecture Patterns |
| EVTAVAIL-03 | A player changes only their own availability, any time | Acting user always taken from `User` claims (D-09); server-side ownership check in every write method |
| EVTAVAIL-04 | Join backfills events `Date >= today`; leave deletes every signup on that board, past+future | `GroupService.AddMemberAsync`/`RemoveMemberAsync` hooks — see Pitfall 1 (atomicity) and Pitfall 2 (query-filter scoping) |
| EVTAVAIL-05 | Cross-board isolation proven by a two-group integration test | `TenantIsolationTests.cs` recipe reproduced in Code Examples; D-28 defence-in-both-layers |

</phase_requirements>

## Summary

This phase is pure code on top of a schema Phase 74 already shipped. Nothing here needs a new NuGet package, a new external service, or a new architectural layer — it is entirely about wiring `EventSignupEntity` up through Repository → Domain → Service correctly, and about getting two specific, easy-to-get-wrong pieces of EF Core plumbing right: **atomicity of a two-entity write inside a method that already has its own `SaveChangesAsync`/`DbUpdateException` handling**, and **the interaction between the group-scoped query filter and code paths that run outside the ambient `ActiveGroupId`**.

Both of those pieces already have a precedent baked into this codebase, and both precedents point away from the "obvious" EF Core answer. For atomicity, the codebase already hit this exact problem in Phase 45 (`CharacterRepository`/`ContactRepository`/`DungeonMasterProfileRepository`) and *rejected* `Database.BeginTransactionAsync()` — not on style grounds, but because it throws `InvalidOperationException` against the InMemory provider every unit and integration test in this solution runs on. The fix that shipped instead was: stage every entity mutation on the *same* `DbContext`, then call `SaveChangesAsync()` exactly once. That is the pattern `GroupService.AddMemberAsync`/`RemoveMemberAsync` need for D-19's atomicity requirement, and it is compatible with the InMemory provider used by every test in this repo.

For the query-filter interaction, the concrete danger is this: `GroupService.AddMemberAsync`/`RemoveMemberAsync` are called from `Areas/Platform/Controllers/GroupController.cs`, which operates on an explicit `id` route parameter that is **not** synchronized to the caller's `ActiveGroupId` in any way (verified — the controller never touches `IActiveGroupContext` or `Session` at all). `IBoardTypeResolver` and every `HasQueryFilter` in `QuestBoardContext` are bound to `ActiveGroupId`, not to a parameter. A backfill or cleanup query written the "normal" way — reading `Events`/`EventSignups` through the ambient filter — will silently operate on the wrong board, or on nothing, whenever a Platform admin adds/removes a member on a board that is not their own current selection. The fix is to never resolve board type via `IBoardTypeResolver` inside these two hooks, and to give the repository layer explicit-`groupId` read methods that deliberately bypass the ambient filter (`IgnoreQueryFilters()` re-paired with a manual `Where(GroupId == groupId)`) for exactly this narrow purpose.

**Primary recommendation:** Extend `GroupRepository.AddMemberAsync`/`RemoveMemberAsync` (not `GroupService`) to be the place both the membership write and the signup fan-out/cleanup writes are staged and saved together in one `SaveChangesAsync()` call, resolve BoardType via `groupService.GetByIdAsync(groupId)` (never `IBoardTypeResolver`), and add two new narrowly-scoped, explicit-`groupId` repository read methods (`IEventRepository.GetFutureEventIdsForGroupAsync`, a signup-cleanup equivalent) that use `IgnoreQueryFilters()` deliberately and locally rather than relying on ambient `ActiveGroupId`.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Record/change a player's own availability | API / Backend (`EventsController`) | Database (`EventSignups` unique index) | Controller enforces D-08/D-09 ownership + board-type rules server-side; DB enforces one-row-per-(event,user) |
| Campaign create-time fan-out | API / Backend (`EventsController.Create`) | Database (Repository layer) | Must happen in the same unit of work as the event insert (D-15); controller already resolves the active board |
| Campaign join/leave backfill & cleanup | Domain (`GroupService`) | Database (`GroupRepository`) | D-18/D-23 lock the Domain-layer chokepoints; the actual atomic multi-entity write belongs in the Repository layer that owns the shared `DbContext` |
| Roster display | API / Backend (read) + Browser (render) | — | Server loads via `.Include(es => es.User)`, no client-side aggregation needed |
| Cross-board isolation | Database (query filter) + API (explicit checks) | — | D-28: filter constrains reads only; every write independently re-verifies |

## Standard Stack

No new packages. This phase is entirely additive C# on the existing EF Core 10 / ASP.NET Core 10 MVC stack already in use (`Microsoft.EntityFrameworkCore.SqlServer`, `AutoMapper`, xUnit v3.2.2 / FluentAssertions / NSubstitute for tests — all already installed and used by `PlayerSignupRepository`, `GroupRepository`, `TenantIsolationTests.cs`).

**Installation:** none required.

## Package Legitimacy Audit

Not applicable — this phase installs no external packages. `Package Legitimacy Gate` skipped per the protocol's own scope (no packages to check).

## Architecture Patterns

### System Architecture Diagram

```
 One-Shot flow (opt-in)                    Campaign flow (opt-out)
 ───────────────────────                   ────────────────────────
 Player clicks Yes/Maybe/No  ─┐             DM creates event         ─┐
 on Events/Details.cshtml     │             (EventsController.Create) │
        │ fetch POST          │                    │                 │
        ▼                     │                    ▼                 │
 EventsController              │       eventService.AddAsync(event)   │
   .SetAvailability(id, vote)  │                    │                 │
        │                      │                    ▼ (same request,
        │  resolve User from   │       userRepository                 │  same DbContext)
        │  claims (D-09)       │         .GetAllGroupMembers(groupId) │
        ▼                      │                    │                 │
 EventSignupRepository         │                    ▼                 │
   .SetAvailabilityAsync       │       eventSignupRepository           │
   (eventId, userId, vote)     │         .AddFanOutAsync(eventId,      │
        │  create-or-update,   │           memberIds, Yes, no stamp)   │
        │  stamps UpdatedAt    │                    │                 │
        ▼                      │                    ▼                 │
     SaveChangesAsync()   ◄────┘             SaveChangesAsync()  ◄─────┘


 Join a Campaign board                      Leave a board
 ──────────────────────                     ─────────────
 GroupController(Platform)                  GroupController(Platform)
   .AddMember(id, ...)                        .RemoveMember(id, userId)
        │  id = explicit groupId,                    │  id = explicit groupId,
        │  NOT ActiveGroupId                          │  NOT ActiveGroupId
        ▼                                             ▼
 GroupService.AddMemberAsync(groupId,...)     GroupService.RemoveMemberAsync(groupId, userId)
        │                                             │
        ▼                                             ▼
 GroupRepository.AddMemberAsync                GroupRepository.RemoveMemberAsync
   1. exists-check (unchanged)                    1. delete UserGroups row (unchanged)
   2. groupService.GetByIdAsync(groupId)          2. eventRepo.GetEventSignupIdsForMember
      → BoardType (NOT IBoardTypeResolver)            OnGroupAsync(groupId, userId)
   3. if Campaign: eventRepo                          (IgnoreQueryFilters + explicit
      .GetFutureEventIdsForGroupAsync(                 Where(GroupId == groupId))
      groupId, today) (IgnoreQueryFilters)          3. remove those EventSignup rows
   4. stage UserGroupEntity.Add +                   4. ONE SaveChangesAsync() — same
      EventSignupEntity.AddRange (no stamp)             DbContext, atomic with the
   5. ONE SaveChangesAsync() — atomic,                  membership delete
      same try/catch race handling as today
```

### Recommended Project Structure

No new folders. New files slot into the existing per-layer structure exactly like `Event`/`EventRepository`/`EventService` did in Phase 74:

```
QuestBoard.Domain/
├── Models/EventSignup.cs                  # new domain model (UserId, EventId, Availability, HasAnswered)
├── Interfaces/IEventSignupRepository.cs   # new
├── Interfaces/IEventSignupService.cs      # new
├── Services/EventSignupService.cs         # new
├── Interfaces/IEventRepository.cs         # extended: GetFutureEventIdsForGroupAsync, roster helpers
├── Interfaces/IGroupRepository.cs         # AddMemberAsync/RemoveMemberAsync signatures unchanged externally
QuestBoard.Repository/
├── EventSignupRepository.cs               # new — narrow scalar-update methods (D-30)
├── EventRepository.cs                     # extended — explicit-groupId future-event query
├── GroupRepository.cs                     # extended — atomic fan-out/cleanup inside existing methods
├── Automapper/EntityProfile.cs            # new EventSignup <-> EventSignupEntity map
QuestBoard.Service/
├── Controllers/Events/EventsController.cs # new SetAvailability / Withdraw actions; Create gains fan-out call
├── ViewModels/EventViewModels/EventSignupViewModel.cs  # new — roster row + button state
├── Views/Events/Details.cshtml            # buttons + roster + Withdraw
├── Areas/Platform/Controllers/GroupController.cs  # RemoveMember confirmation copy (D-24)
├── Automapper/ViewModelProfile.cs         # EventSignup -> EventSignupViewModel map
```

### Pattern 1: Single-`SaveChangesAsync` atomicity (verified precedent — do not use `BeginTransactionAsync`)

**What:** When two entity mutations must succeed or fail together, stage both on the same `DbContext` and call `SaveChangesAsync()` once, rather than wrapping two independent calls in `Database.BeginTransactionAsync()`.

**When to use:** D-19 (membership + backfill), D-20/D-23 (membership delete + signup cleanup).

**Why not `BeginTransactionAsync`:** `.planning/milestones/v7.0-phases/45-dual-image-storage-backend/45-REVIEW-FIX.md` records that this exact fix was attempted with `BeginTransactionAsync` and reverted, because `QuestBoardContext` in every unit test (and this project's InMemory-backed integration tests, per `.planning/codebase/TESTING.md`) uses EF Core's InMemory provider, which throws `InvalidOperationException` on `BeginTransactionAsync`. The shipped fix (`CharacterRepository.UpdateWithProfileImageAsync`, commit `1a9d931`) instead combines the mutations into one tracked graph and one `SaveChangesAsync()` call — this works identically on InMemory and SQL Server with no transaction-API dependency.

**Example — the shipped Phase 45 precedent** (`QuestBoard.Repository/CharacterRepository.cs:196-251`):
```csharp
// Source: QuestBoard.Repository/CharacterRepository.cs (verified in this session)
var entity = await DbContext.Characters
    .Include(c => c.Classes)
    .Include(c => c.ProfileImage)
    .FirstOrDefaultAsync(c => c.Id == model.Id, token);
if (entity == null) return;

Mapper.Map(model, entity);
// ... reconcile navigations ...
ApplyProfileImage(entity, originalImageData, croppedImageData);

await DbContext.SaveChangesAsync(token);   // ONE call, both mutations committed together
```

**Applied to D-19** — recommended shape for `GroupRepository.AddMemberAsync`:
```csharp
// Recommended shape — mirrors the Phase 45 precedent and preserves the existing race handling.
public async Task AddMemberAsync(int groupId, int userId, GroupRole groupRole, CancellationToken token = default)
{
    var exists = await DbContext.UserGroups
        .AnyAsync(ug => ug.UserId == userId && ug.GroupId == groupId, token);
    if (exists)
        throw new InvalidOperationException("User is already a member of this group.");

    DbContext.UserGroups.Add(new UserGroupEntity { UserId = userId, GroupId = groupId, GroupRole = (int)groupRole });

    // BoardType resolved from the explicit groupId, never from IBoardTypeResolver/ActiveGroupId —
    // see Pitfall 2. GroupEntity carries no query filter, so this is safe regardless of the
    // caller's own active board.
    var group = await DbContext.Groups.FirstOrDefaultAsync(g => g.Id == groupId, token);
    if (group?.BoardType == (int)BoardType.Campaign)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        // Explicit groupId bypasses the ambient ActiveGroupId filter deliberately — see Pitfall 2.
        var futureEventIds = await DbContext.Events
            .IgnoreQueryFilters()
            .Where(e => e.GroupId == groupId && e.Date >= today)
            .Select(e => e.Id)
            .ToListAsync(token);

        foreach (var eventId in futureEventIds)
        {
            // No UpdatedAt stamp — this is an automatic backfill, not a human answer (D-10/D-13).
            DbContext.EventSignups.Add(new EventSignupEntity
            {
                EventId = eventId,
                UserId = userId,
                Availability = (int)VoteType.Yes
            });
        }
    }

    try
    {
        await DbContext.SaveChangesAsync(token); // membership + backfill committed together
    }
    catch (DbUpdateException)
    {
        throw new InvalidOperationException("User is already a member of this group.");
    }
}
```
This keeps the existing race-handling `catch` intact. Because the pre-check (`AnyAsync`) still runs first, the only realistic trigger for the `DbUpdateException` remains the same concurrent-membership race it handles today — the signup rows use a fresh `(EventId, UserId)` pair that cannot already exist for a user who was not previously a member, so they do not introduce a second failure mode into that catch block.

### Pattern 2: Explicit-`groupId` reads bypass the ambient filter deliberately — never `IBoardTypeResolver` inside `GroupService`

**What:** `IBoardTypeResolver.GetBoardTypeAsync()` and every `HasQueryFilter` in `QuestBoardContext` resolve against `IActiveGroupContext.ActiveGroupId` — the caller's *currently selected* board, read from ASP.NET Core Session (or a Hangfire-job override via `ActiveGroupContextService.SetGroupId`, a Service-layer-only method deliberately excluded from `IActiveGroupContext`). `GroupService.AddMemberAsync`/`RemoveMemberAsync` are called with an explicit `groupId` parameter that is **not** the caller's active group.

**Verified:** `QuestBoard.Service/Areas/Platform/Controllers/GroupController.cs` — `AddMember(int id, ...)` and `RemoveMember(int id, int userId, ...)` never reference `IActiveGroupContext` or `Session` anywhere in the file; `id` is resolved independently via `groupService.GetByIdAsync(id)`. A SuperAdmin managing Group 7 from the Platform area may have `ActiveGroupId == null` or `== 3` — either way, `IBoardTypeResolver.GetBoardTypeAsync()` would return the wrong board's type (or `null`), and any read against `Events`/`EventSignups` through the normal filtered `DbSet` would silently return zero rows for the *actual* target group.

**When to use:** Any read inside `GroupService.AddMemberAsync`/`RemoveMemberAsync` (or a repository method they call) that needs data scoped to the `groupId` parameter — the join backfill's future-event list (D-17) and the leave cleanup's existing-signup list (D-20).

**Contrast — `IBoardTypeResolver` IS safe** inside `EventsController` (D-08's withdraw guard, D-15's create-time fan-out): those actions already resolved/validated `ActiveGroupId` earlier in the same request (the `Create` POST redirects to `GroupPicker` if `ActiveGroupId` is null; `Details`/withdraw only ever operate on an event the query filter already scoped to the active board). The distinction is *whose* groupId is in play — a controller acting on the caller's own selected board vs. a Domain-service hook acting on an explicit, independent parameter.

**Precedent for the mechanism (`IgnoreQueryFilters`) — with an important caveat:** `.planning/ROADMAP.md`'s Phase 76 entry explicitly warns: *"The job runs outside `GroupSessionMiddleware`, so it must call `SetGroupId()` per group and iterate — never `IgnoreQueryFilters()`."* That guidance is about a **Hangfire background job** iterating over *every* group and making *many* repository calls per iteration — `SetGroupId()` (mutating the shared `ActiveGroupContextService` for the rest of that scope) is the safer choice there because a single missed `IgnoreQueryFilters()` call among many would silently leak. It is not reachable from Domain-layer code anyway (`SetGroupId` is on the concrete Service-layer `ActiveGroupContextService`, not on `IActiveGroupContext`, specifically to keep it out of Domain). This phase's situation is the opposite shape: one tightly-scoped repository method, one manual `Where(GroupId == groupId)` immediately paired with `IgnoreQueryFilters()`, no ambient state mutated, and no iteration to get wrong. Flagged as an **Open Question** below since it is a genuine judgment call the plan/plan-checker should confirm explicitly rather than inherit silently.

### Pattern 3: Narrow scalar-update repository methods (D-30)

**What:** `PlayerSignupRepository.ChangeVoteAsync` (`QuestBoard.Repository/PlayerSignupRepository.cs:43`) never calls `BaseRepository.UpdateAsync`/`Mapper.Map` — it loads the tracked entity, mutates scalar fields directly, and calls `SaveChangesAsync()` itself. `EventSignupRepository` should follow the same shape.

**Recommended method set** (`IEventSignupRepository`):

```csharp
// Create-or-update, stamps UpdatedAt — used by both the One-Shot first-click-creates-the-row
// case and every subsequent change on either board type (D-06, D-10).
Task SetAvailabilityAsync(int eventId, int userId, VoteType availability, CancellationToken token = default);

// Deletes the row — One-Shot only, enforced by the controller (D-07/D-08), not by this method.
Task<bool> WithdrawAsync(int eventId, int userId, CancellationToken token = default);

// Bulk insert, Availability = Yes, UpdatedAt left null — the create-time Campaign fan-out (D-15/D-16).
Task AddFanOutForEventAsync(int eventId, IEnumerable<int> userIds, CancellationToken token = default);

// Roster read for Events/Details.cshtml — no N+1 (single Include).
Task<IList<EventSignup>> GetRosterForEventAsync(int eventId, CancellationToken token = default);

// D-25/D-26 delete-confirmation count — ALL rows, not just HasAnswered ones.
Task<int> CountForEventAsync(int eventId, CancellationToken token = default);
```

The join-time backfill (D-17/D-18/D-19) and leave-time cleanup (D-20/D-23) are recommended to live in `GroupRepository` itself (see Pattern 1) rather than as calls into `IEventSignupRepository`, specifically so both entity mutations land in one `SaveChangesAsync()` call — see Pitfall 1.

### Pattern 4: Roster read without an `Event.Signups` domain-model hazard

**What/Why:** D-30's note flags that adding `EventEntity.Signups` "becomes load-bearing rather than precautionary" for the AutoMapper hazard. Verified detail that refines this: the hazard (`PlayerSignupRepository`/`CharacterRepository`'s reason for a custom `UpdateAsync` override) triggers specifically when **both** the entity *and* the domain model expose the same collection property, because AutoMapper's default `Mapper.Map(model, entity)` replaces the destination collection with fresh instances from the source. `CreateMap<Event, EventEntity>` (`EntityProfile.cs:143`) currently has no `Signups` member on either side.

**Recommendation:** Add `virtual ICollection<EventSignupEntity> Signups { get; set; } = []` to `EventEntity` only if convenient for EF's own relationship configuration — but do **not** add a matching `Signups` property to the `Event` domain model. With no source-side member, `CreateMap<Event, EventEntity>` never touches `entity.Signups`, so `EventsController.Edit`'s existing `eventService.UpdateAsync(existingEvent, token)` → `BaseRepository<Event, EventEntity>.UpdateAsync` → `Mapper.Map(model, entity)` path stays exactly as safe as it is today, with **no** custom `EventRepository.UpdateAsync` override needed. The roster itself is read through `IEventSignupRepository.GetRosterForEventAsync` (Pattern 3), which queries `EventSignups` directly:

```csharp
// Recommended — QuestBoard.Repository/EventSignupRepository.cs
public async Task<IList<EventSignup>> GetRosterForEventAsync(int eventId, CancellationToken token = default)
{
    // The ambient EventSignupEntity filter (es.Event.GroupId == ActiveGroupId) is correct here:
    // this always runs from EventsController.Details, inside a normal request where ActiveGroupId
    // already matches the event's board (the event itself was fetched through the same filter).
    var entities = await DbContext.EventSignups
        .Include(es => es.User)
        .Where(es => es.EventId == eventId)
        .ToListAsync(token);
    return Mapper.Map<IList<EventSignup>>(entities);
}
```

### Anti-Patterns to Avoid

- **Resolving BoardType via `IBoardTypeResolver` inside `GroupService.AddMemberAsync`/`RemoveMemberAsync`.** It reads `ActiveGroupId`, not the `groupId` parameter — wrong board, or `null`, whenever the two diverge (Platform admin flow). Use `groupService`/`DbContext.Groups.FirstOrDefaultAsync(g => g.Id == groupId)` instead — `GroupEntity` carries no query filter, so this is always correct regardless of the caller's own active board.
- **`Database.BeginTransactionAsync()` for D-19/D-20 atomicity.** Throws `InvalidOperationException` against the InMemory provider every test in this solution runs on (verified — `.planning/milestones/v7.0-phases/45-dual-image-storage-backend/45-REVIEW-FIX.md`). Use single-`SaveChangesAsync` staging instead (Pattern 1).
- **Adding `Signups` to the `Event` domain model "for convenience."** Immediately reopens the AutoMapper navigation-clobber hazard on every `EventsController.Edit` save (Pattern 4) for no benefit — the roster read never needs to go through `Event`.
- **A blanket `.IgnoreQueryFilters()` anywhere it isn't immediately paired with an explicit, hand-written `Where(GroupId == <explicit param>)` on the same query.** This is the one narrow, deliberate exception to the ROADMAP's "never `IgnoreQueryFilters()`" guidance (Pattern 2) — it must never be used as a shortcut to "just see everything."
- **Counting only `HasAnswered` rows in the D-25/D-26 delete-confirmation dialog.** D-26 explicitly requires counting *all* signup rows, including untouched Campaign defaults — this is a deliberate decision, not a bug to "fix."

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Atomic two-entity write with test-suite compatibility | A custom transaction wrapper or manual rollback logic | Single shared `DbContext`, one `SaveChangesAsync()` call | EF Core already wraps everything staged before one `SaveChangesAsync()` in an implicit DB transaction on relational providers, and this shape works identically against the InMemory provider every test uses — no new abstraction needed |
| Cross-board-scoped reads from a chokepoint that doesn't have the ambient group context | A parallel "admin view" DbContext or a second connection string | `IgnoreQueryFilters()` + an explicit `Where(GroupId == param)`, scoped to one query | The filter infrastructure already exists; bypass it narrowly rather than duplicating it |
| Three-state Yes/Maybe/No/unanswered availability | A new enum | The existing `VoteType { No, Maybe, Yes }` plus row-absence for "unanswered" | Exactly what `PlayerDateVoteEntity` already does; `EventSignupEntity.Availability` is already typed against it |

**Key insight:** everything this phase needs — atomic multi-entity writes, group-scoped reads from an ungrouped context, a tri-state vote — already has a shipped precedent somewhere in this codebase. The research risk here was never "what EF Core feature to reach for," it was "which of two existing, textually-similar-looking precedents (`BeginTransactionAsync` vs. single-`SaveChangesAsync`; `SetGroupId()` vs. `IgnoreQueryFilters()`) actually applies to this call site" — both answered above with verified evidence.

## Common Pitfalls

### Pitfall 1: `BeginTransactionAsync` looks like the "correct" fix for D-19 and breaks every test that touches it

**What goes wrong:** A natural first read of D-19's atomicity requirement is to wrap `GroupRepository.AddMemberAsync`'s existing `SaveChangesAsync()` call in `await DbContext.Database.BeginTransactionAsync(token)`. This compiles, looks correct, and works against a real SQL Server database.

**Why it happens:** It is the textbook EF Core answer to "make two writes atomic," and nothing about `QuestBoardContext` visibly signals that the test suite runs against a provider that doesn't support it.

**How to avoid:** Use the single-`SaveChangesAsync` pattern (Pattern 1/Architecture Patterns above) — verified as this exact codebase's own resolution to this exact problem in Phase 45.

**Warning signs:** `dotnet test QuestBoard.UnitTests` or `QuestBoard.IntegrationTests` throwing `InvalidOperationException` with a message about transactions not being supported by the in-memory store, on any test that exercises `AddMemberAsync`/`RemoveMemberAsync`.

### Pitfall 2: The join/leave backfill silently no-ops (or worse, touches the wrong board) when the acting admin's active board differs from the target group

**What goes wrong:** A repository/service method written the "normal" way — `eventService.GetEventsForCalendarAsync()`, or any query against `DbContext.Events`/`DbContext.EventSignups` without an explicit `IgnoreQueryFilters()` — silently returns rows for whatever `ActiveGroupId` happens to be, which for a Platform-area `AddMember`/`RemoveMember` call is unrelated to the `groupId` the admin is actually operating on. The result: a new Campaign member gets zero backfilled signups (if `ActiveGroupId` is null or a different group), or — if the admin happens to have a different group active whose events coincidentally exist — data written against the wrong board entirely.

**Why it happens:** `Areas/Platform/Controllers/GroupController.cs` never sets `Session`/`IActiveGroupContext` at all — verified by grep, zero matches for `ActiveGroupId` or `Session` in that file. It was never designed to operate within a "current board" mental model; it manages arbitrary groups by explicit `id`.

**How to avoid:** Every read inside `GroupService.AddMemberAsync`/`RemoveMemberAsync` (or a repository method they call) that needs group-scoped data must take that group as an explicit parameter and query with `IgnoreQueryFilters()` + a manually re-added `Where(GroupId == groupId)` (Pattern 2). Never rely on `IBoardTypeResolver` or the ambient `HasQueryFilter` inside these two hooks.

**Warning signs:** A Platform-admin-driven integration test that adds a member to a Campaign group *while the test's `TestGroupContext.ActiveGroupId` is set to a different group (or 999)* still produces zero or wrong-board signup rows. This is exactly the kind of test D-19's "atomic" promise implies should exist and pass regardless of the caller's own active-group state — recommend adding one.

### Pitfall 3: `MutableGroupContext.BoardType` is a hardcoded test flag, not a database read — seeding it does not exercise the real backfill logic

**What goes wrong:** `QuestBoard.IntegrationTests/Helpers/MutableGroupContext.cs` implements `IBoardTypeResolver` itself, with a plain settable `BoardType` property defaulting to `OneShot`, completely decoupled from any `GroupEntity` row in the test database. A test that does `factory.TestGroupContext.BoardType = BoardType.Campaign;` and then asserts backfill behaviour is testing the *stub*, not the recommended `groupService.GetByIdAsync(groupId)` read path this research recommends for `GroupService.AddMemberAsync` (Pattern 1/2).

**Why it happens:** `MutableGroupContext` was built for nav-visibility and controller-level board-type-gating tests (D-08 style), where the code under test genuinely does call `IBoardTypeResolver`. The join/leave backfill deliberately does **not** call `IBoardTypeResolver` (Pitfall 2), so this stub has no effect on it.

**How to avoid:** Tests for D-17/D-19's Campaign-vs-One-Shot backfill behaviour must seed a real `GroupEntity` row with the desired `BoardType` via `factory.Database.CreateContext()` (which sees everything, `ActiveGroupId = null`) — e.g. `ctx.Groups.Add(new GroupEntity { Id = 2, BoardType = (int)BoardType.Campaign, ... })` — not by setting `factory.TestGroupContext.BoardType`.

**Warning signs:** A backfill test that sets `TestGroupContext.BoardType` and passes even after the production code path is changed to stop calling `IBoardTypeResolver` — a sign the test was never exercising the real logic.

### Pitfall 4: `DbUpdateException` from the combined `SaveChangesAsync()` call gets mis-attributed to the membership race

**What goes wrong:** `GroupRepository.AddMemberAsync`'s existing `catch (DbUpdateException)` block assumes any failure means "the user is already a member" (the concurrent-add race it was written for). Once the same `SaveChangesAsync()` call also persists the signup fan-out, a *different* failure — e.g. a stale/deleted event id sneaking into the future-events query result between read and write — would be caught by the same block and reported as a misleading "already a member" error.

**Why it happens:** The pre-check (`AnyAsync` for existing membership) and the actual `SaveChangesAsync()` are two separate round-trips; combining more writes into the second one widens the surface the `catch` block silently reinterprets.

**How to avoid:** This is a genuinely rare edge case (an event would have to be deleted in the few-millisecond window between the `SELECT` and the `INSERT`), and is not worth a bespoke `SqlException`-number inspection for a 17-user app. Flagged so the plan does not assume the existing `catch` block's assumption ("any `DbUpdateException` here means duplicate membership") still holds unconditionally — a comment update noting the narrowed scope is the minimum needed; the plan may add finer-grained error handling if it judges the risk worth it.

## Code Examples

### The two-group isolation test recipe (D-29 / EVTAVAIL-05)

```csharp
// Source: QuestBoard.IntegrationTests/Tests/TenantIsolationTests.cs (verified structural recipe)
public class EventAvailabilityTenantIsolationTests(WebApplicationFactoryBase factory)
    : IClassFixture<WebApplicationFactoryBase>, IAsyncLifetime
{
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public ValueTask DisposeAsync()
    {
        factory.TestGroupContext.ActiveGroupId = 1;
        factory.TestGroupContext.BoardType = BoardType.OneShot; // reset the board-type stub too
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task CannotReadOrWriteAvailability_ForEventOnAnotherBoard()
    {
        await TestDataHelper.ClearDatabaseAsync(factory.Services);
        var user = await AuthenticationHelper.CreateTestUserAsync(
            factory.Services, "isoplayer1", "isoplayer1@example.com");

        // Seed Group 2 + an event on it, and a signup, via the unfiltered seeding context.
        await using var ctx = factory.Database.CreateContext(); // ActiveGroupId = null (sees all)
        ctx.Groups.Add(new GroupEntity { Id = 2, Name = "OtherGroup", CreatedAt = DateTime.UtcNow, BoardType = (int)BoardType.OneShot });
        var otherEvent = new EventEntity { Title = "Group2 Session", Date = DateOnly.FromDateTime(DateTime.Today), GroupId = 2, CreatedAt = DateTime.UtcNow };
        ctx.Events.Add(otherEvent);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act as Group 1 (a member of a different board).
        factory.TestGroupContext.ActiveGroupId = 1;
        var (client, _) = await AuthenticationHelper.CreateAuthenticatedClientWithUserAsync(
            factory, "isoviewer1", "isoviewer1@example.com");

        // Read: Details for a Group-2 event must 404, not leak the event.
        var readResponse = await client.GetAsync($"/Events/Details/{otherEvent.Id}", TestContext.Current.CancellationToken);
        readResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Write: setting availability on a Group-2 event must be refused, not silently accepted.
        var writeResponse = await client.PostAsync(
            $"/Events/SetAvailability/{otherEvent.Id}",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["availability"] = "Yes" }),
            TestContext.Current.CancellationToken);
        writeResponse.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.BadRequest, HttpStatusCode.Forbidden);
    }
}
```

### Join-time backfill boundary (D-17) — `DateOnly`, no time-of-day comparison

```csharp
// Today's event is included — DateOnly.FromDateTime(DateTime.Today) with no time component,
// matching Phase 74 D-01's structural elimination of the naive-DateTime DST bug class.
var today = DateOnly.FromDateTime(DateTime.Today);
var futureEventIds = await DbContext.Events
    .IgnoreQueryFilters()
    .Where(e => e.GroupId == groupId && e.Date >= today)
    .Select(e => e.Id)
    .ToListAsync(token);
```

## State of the Art

Not applicable — this phase does not touch any external library or framework version. All patterns above are internal-codebase precedents, not upstream ecosystem changes.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | The recommended repository method names (`SetAvailabilityAsync`, `AddFanOutForEventAsync`, `GetFutureEventIdsForGroupAsync`, etc.) are illustrative, not mandated — CONTEXT.md leaves exact naming to the planner's discretion | Architecture Patterns / Code Examples | None — explicitly a suggestion, not a locked decision |
| A2 | `AdminController.DeleteUser`'s call into `GroupService.RemoveMemberAsync` runs within a request where `ActiveGroupId` matches the target group (unlike the Platform `GroupController` path) — based on `AdminController`'s `[Authorize(Policy = "AdminOnly")]` + `IActiveGroupContext` dependency, not independently traced end-to-end in this session | Pitfall 2 | Low — the recommended fix (always use the explicit `groupId` parameter, never ambient `ActiveGroupId`) is correct regardless of whether this particular caller happens to already match; the assumption only affects how *urgent* the fix feels, not whether it's needed |

**If this table is empty:** N/A — see above.

## Open Questions

1. **Is `IgnoreQueryFilters()` inside `GroupRepository`/`EventRepository` an acceptable deviation from the ROADMAP's Phase-76-scoped "never `IgnoreQueryFilters()`" guidance?**
   - What we know: that guidance is written specifically about a Hangfire background job iterating across every group with many repository calls per iteration, where `SetGroupId()` is both reachable (Service-layer job) and safer (one mutation covers many calls). `GroupService.AddMemberAsync`/`RemoveMemberAsync` are Domain-layer (per D-18/D-23, `SetGroupId` is unreachable there by design) and need exactly one narrowly-scoped query each.
   - What's unclear: whether a future reviewer, seeing `IgnoreQueryFilters()` anywhere in the codebase, will assume it's the exact anti-pattern the ROADMAP warns about without reading this distinction.
   - Recommendation: the plan should include a comment at each `IgnoreQueryFilters()` call site (as shown in Code Examples) explaining specifically why it's paired with a manual `Where` and why `SetGroupId()` doesn't apply here (Domain-layer caller, no access to the Service-layer mutator). Flag for `gsd-plan-review-convergence`/code review to confirm agreement rather than treating this research's recommendation as unilaterally final.

2. **Past-event answer mutability (explicit Claude's Discretion item).**
   - What we know: Phase 74 D-19 allows past-dated events to exist and be created/edited with no guard.
   - What's unclear: whether `SetAvailabilityAsync`/`WithdrawAsync` should reject a write against a past `Event.Date`.
   - Recommendation: allow it (matches D-16's "the fan-out runs regardless of date" symmetry, and a DM correcting the record of "who actually showed up" after the fact is a legitimate use of a past event per D-19's own "an event is a record, not a booking" reasoning) — but this is explicitly left to the planner per CONTEXT.md, and the plan must state which behaviour was chosen and add a test for it.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit v3.2.2 + FluentAssertions v8.10.0 (verified — `.planning/codebase/TESTING.md`) |
| Config file | `QuestBoard.IntegrationTests/xunit.runner.json` (serial execution) |
| Quick run command | `dotnet test --filter "FullyQualifiedName~EventSignup"` |
| Full suite command | `dotnet test` |

### Phase Requirements → Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| EVTAVAIL-01 | One-Shot: no row until created; Yes/Maybe/No recordable | integration | `dotnet test --filter "FullyQualifiedName~EventsControllerIntegrationTests"` | ❌ Wave 0 |
| EVTAVAIL-02 | Campaign: every member auto-Yes at create; opt-out flips to No, never deletes | integration | same filter | ❌ Wave 0 |
| EVTAVAIL-03 | A player changes only their own answer | integration | same filter | ❌ Wave 0 |
| EVTAVAIL-04 | Join backfills `Date >= today`; leave deletes all (past+future) | integration + unit | `dotnet test --filter "FullyQualifiedName~GroupServiceTests|FullyQualifiedName~GroupMembershipEventBackfill"` | ❌ Wave 0 (extends existing `GroupServiceTests.cs`) |
| EVTAVAIL-05 | Cross-board isolation, two distinct groups | integration | `dotnet test --filter "FullyQualifiedName~EventAvailabilityTenantIsolationTests"` | ❌ Wave 0 (new class, follows `TenantIsolationTests.cs`) |

### Sampling Rate
- **Per task commit:** `dotnet test --filter "FullyQualifiedName~Event"`
- **Per wave merge:** `dotnet test`
- **Phase gate:** Full suite green before `/gsd-verify-work`

### Wave 0 Gaps
- [ ] `QuestBoard.IntegrationTests/Tests/EventAvailabilityTenantIsolationTests.cs` — covers EVTAVAIL-05, following the `TenantIsolationTests.cs` structural recipe above
- [ ] `QuestBoard.IntegrationTests/Controllers/EventsControllerIntegrationTests.cs` (new, or extend an existing Events test class if Phase 74 added one) — covers EVTAVAIL-01/02/03
- [ ] Extend `QuestBoard.UnitTests/Services/GroupServiceTests.cs` — covers the atomicity and board-type-scoping halves of EVTAVAIL-04 at the unit level (mocked `IGroupRepository`), with the integration test covering the real DB behaviour
- [ ] A dedicated D-08 test: a Campaign-board Withdraw attempt returns `BadRequest`/`Forbid`, never a successful delete

## Security Domain

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-------------------|
| V2 Authentication | No | Reuses existing ASP.NET Core Identity/cookie auth — no change this phase |
| V3 Session Management | No | No new session state |
| V4 Access Control | Yes | Acting user always taken from `User` claims, never the request body (D-09); board membership independently re-verified on every write (D-28), not just inferred from the read-side query filter |
| V5 Input Validation | Yes | `VoteType` cast from a bounded set (`[Range(0,2)]` on the entity, enum parse on the controller boundary) — no free-text availability value ever reaches the database |
| V6 Cryptography | No | Not applicable to this phase |

### Known Threat Patterns for this stack

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|----------------------|
| IDOR — a player passing another user's id to change their availability | Elevation of Privilege | `SetAvailabilityAsync`/`WithdrawAsync` always take `userId` from `User` claims server-side (D-09), never from a form field or route parameter |
| Cross-tenant write via a mis-scoped insert (the class of bug this app has shipped twice before — Phases 49/55, and a third live gap found at Phase 72 discussion) | Elevation of Privilege / Information Disclosure | D-28's defence-in-both-layers: the query filter (read-only) plus an explicit board-ownership check on every write; the `IgnoreQueryFilters()` uses in `GroupRepository`/`EventRepository` are the highest-risk lines in this phase for exactly this reason and must each carry a matching explicit `Where(GroupId == ...)` |
| Ambient-context confusion — code assuming `ActiveGroupId` matches an explicit `groupId` parameter (Pitfall 2) | Tampering / Information Disclosure | Never resolve board type or group-scoped data via `IBoardTypeResolver`/the ambient query filter inside `GroupService.AddMemberAsync`/`RemoveMemberAsync`; always use the explicit parameter |

## Sources

### Primary (HIGH confidence — verified directly against this codebase in this session)
- `QuestBoard.Repository/Entities/EventSignupEntity.cs`, `EventEntity.cs` — shipped schema
- `QuestBoard.Repository/Migrations/20260826134133_AddCalendarEventsFeature.cs` — verified FK/index/cascade shape
- `QuestBoard.Repository/Entities/QuestBoardContext.cs:420-441` — the three Event/EventSeries/EventSignup query filters, exact predicates
- `QuestBoard.Repository/PlayerSignupRepository.cs`, `CharacterRepository.cs` — narrow scalar-update and combined-save precedents
- `QuestBoard.Repository/GroupRepository.cs` (`AddMemberAsync`/`RemoveMemberAsync`), `QuestBoard.Domain/Services/GroupService.cs`, `QuestBoard.Domain/Services/UserService.cs:178` — chokepoint verification
- `QuestBoard.Domain/Interfaces/IBoardTypeResolver.cs`, `QuestBoard.Service/Services/BoardTypeResolver.cs`, `QuestBoard.Service/Services/ActiveGroupContextService.cs`, `QuestBoard.Service/Program.cs:217-233` — ActiveGroupId/BoardType resolution mechanics and the dual-registration/`SetGroupId` boundary
- `QuestBoard.Service/Areas/Platform/Controllers/GroupController.cs` — verified zero references to `ActiveGroupId`/`Session`
- `QuestBoard.Service/Controllers/Events/EventsController.cs` — existing D-15-relevant SuperAdmin-no-active-group handling and `SeriesIsOnActiveBoardAsync` precedent
- `QuestBoard.IntegrationTests/Tests/TenantIsolationTests.cs`, `WebApplicationFactoryBase.cs`, `Helpers/MutableGroupContext.cs` — two-group test recipe, `BoardType` stub behaviour
- `.planning/milestones/v7.0-phases/45-dual-image-storage-backend/45-REVIEW-FIX.md` — verified `BeginTransactionAsync` rejection and the single-`SaveChangesAsync` fix that shipped instead
- `.planning/ROADMAP.md` (Phase 76 entry) — verified `IgnoreQueryFilters()`/`SetGroupId()` guidance
- `.planning/codebase/TESTING.md` — verified InMemory provider usage across unit and integration tests

### Secondary (MEDIUM confidence)
- `.planning/research/events/ARCHITECTURE.md`, `SUMMARY.md` — pre-milestone exploratory research for the whole Calendar Events feature; useful directional confirmation (e.g. that `EventSignupRepository` needs a `ChangeVoteAsync`-style method) but predates the actual shipped schema and uses different field names (`PlayerId`/`VoteChangeTime` vs. the shipped `UserId`/`UpdatedAt`) — treated as background context, not a source of truth for this phase's concrete recommendations

### Tertiary (LOW confidence)
- None — no web search was performed or needed; `brave_search`/`exa_search`/`firecrawl` are all disabled in `.planning/config.json` and this phase's domain is entirely internal-codebase architecture

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — no new dependencies, nothing to verify against a registry
- Architecture (atomicity, query-filter scoping): HIGH — both grounded in verified, shipped precedent within this exact codebase (Phase 45 fix, Phase 76 ROADMAP guidance, direct grep confirmation of `GroupController`'s lack of `ActiveGroupId` awareness)
- Pitfalls: HIGH — each one traced to a specific, cited file and verified behaviour, not inferred

**Research date:** 2026-08-27
**Valid until:** No expiry driver — this is internal-codebase research, not tracking an external library version; re-verify only if `GroupRepository`, `QuestBoardContext`'s filter block, or `ActiveGroupContextService` change before this phase is planned/executed
