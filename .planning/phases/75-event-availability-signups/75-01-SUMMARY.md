---
phase: 75-event-availability-signups
plan: 01
subsystem: database
tags: [ef-core, automapper, event-signup, availability, quest-board]

# Dependency graph
requires:
  - phase: 74-event-schema-crud-and-calendar-display
    provides: EventEntity, EventSignupEntity (table + unique index + cascade delete), QuestBoardContext.EventSignups query filter
provides:
  - EventSignup domain model with a HasAnswered flag derived from UpdatedAt
  - IEventSignupRepository/EventSignupRepository with narrow scalar-update SetAvailabilityAsync, WithdrawAsync, GetRosterForEventAsync
  - IEventSignupService/EventSignupService thin pass-through, registered in DI
  - EventEntity.Signups navigation for a future single-transaction event+fan-out write
  - EntityProfile maps for EventSignup <-> EventSignupEntity
affects: [75-02, 75-03, 75-04, 75-05]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Narrow scalar-update repository methods (SetAvailabilityAsync/WithdrawAsync) instead of a generic in-place UpdateAsync, matching PlayerSignupRepository.ChangeVoteAsync"
    - "Data-tier existence probe (AnyAsync against the filtered DbSet) before an insert, since the ambient query filter only constrains reads"

key-files:
  created:
    - QuestBoard.Domain/Models/EventSignup.cs
    - QuestBoard.Domain/Interfaces/IEventSignupRepository.cs
    - QuestBoard.Domain/Interfaces/IEventSignupService.cs
    - QuestBoard.Domain/Services/EventSignupService.cs
    - QuestBoard.Repository/EventSignupRepository.cs
    - QuestBoard.UnitTests/Repository/EventSignupRepositoryTests.cs
  modified:
    - QuestBoard.Repository/Entities/EventSignupEntity.cs
    - QuestBoard.Repository/Entities/EventEntity.cs
    - QuestBoard.Repository/Automapper/EntityProfile.cs
    - QuestBoard.Repository/Extensions/ServiceExtensions.cs
    - QuestBoard.Domain/Extensions/ServiceExtensions.cs

key-decisions:
  - "HasAnswered is a computed property (UpdatedAt != null), never a raw timestamp read by consumers"
  - "The creating write stamps UpdatedAt too, so a first click and a later change both mean 'a person set this'"
  - "SetAvailabilityAsync probes the filtered Events set with AnyAsync before any insert, since the query filter protects reads only"
  - "No Signups collection was added to the Event domain model, so Mapper.Map(model, entity) can never clobber the navigation on EventsController.Edit"

patterns-established:
  - "Pattern: signup write paths take the caller's userId as a plain parameter and locate rows by (eventId, userId), never by a signup row id, closing an entire class of IDOR before it can exist"

requirements-completed: [EVTAVAIL-01, EVTAVAIL-02, EVTAVAIL-03]

coverage:
  - id: D1
    description: "EventSignup domain model exposes HasAnswered and Availability as VoteType"
    requirement: "EVTAVAIL-01"
    verification:
      - kind: unit
        ref: "QuestBoard.UnitTests/Repository/EventSignupRepositoryTests.cs#AutomaticPassRow_ReadsAsNotAnswered_UntilSetAvailabilityAsyncTouchesIt"
        status: pass
    human_judgment: false
  - id: D2
    description: "SetAvailabilityAsync creates or updates a narrow row, stamping UpdatedAt on both the creating write and later changes, leaving CreatedAt untouched on update"
    requirement: "EVTAVAIL-02"
    verification:
      - kind: unit
        ref: "QuestBoard.UnitTests/Repository/EventSignupRepositoryTests.cs#SetAvailabilityAsync_NoExistingRow_CreatesRowWithAvailabilityAndNonNullUpdatedAt"
        status: pass
      - kind: unit
        ref: "QuestBoard.UnitTests/Repository/EventSignupRepositoryTests.cs#SetAvailabilityAsync_ExistingRow_UpdatesAvailabilityAndAdvancesUpdatedAtWithoutDuplicating"
        status: pass
      - kind: unit
        ref: "QuestBoard.UnitTests/Repository/EventSignupRepositoryTests.cs#SetAvailabilityAsync_ExistingRow_LeavesCreatedAtUnchanged"
        status: pass
    human_judgment: false
  - id: D3
    description: "A signup insert against an event outside the active board is refused at the data tier and writes no row"
    requirement: "EVTAVAIL-03"
    verification:
      - kind: unit
        ref: "QuestBoard.UnitTests/Repository/EventSignupRepositoryTests.cs#SetAvailabilityAsync_EventNotVisibleThroughActiveBoard_ThrowsAndWritesNoRow"
        status: pass
    human_judgment: false
  - id: D4
    description: "WithdrawAsync removes the caller's row and returns whether one existed; GetRosterForEventAsync returns every signup on the requested event, name-ordered, scoped to that event only"
    verification:
      - kind: unit
        ref: "QuestBoard.UnitTests/Repository/EventSignupRepositoryTests.cs#WithdrawAsync_RowExists_RemovesItAndReturnsTrue"
        status: pass
      - kind: unit
        ref: "QuestBoard.UnitTests/Repository/EventSignupRepositoryTests.cs#GetRosterForEventAsync_ReturnsAllRowsWithUserNamePopulated_OrderedAlphabetically_RegardlessOfHasAnswered"
        status: pass
      - kind: unit
        ref: "QuestBoard.UnitTests/Repository/EventSignupRepositoryTests.cs#GetRosterForEventAsync_ReturnsRowsForRequestedEventOnly"
        status: pass
    human_judgment: false

duration: 12min
completed: 2026-08-27
status: complete
---

# Phase 75 Plan 01: Event Signup Data Tier Summary

**EventSignup domain model with a computed HasAnswered flag, a narrow scalar-update EventSignupRepository (SetAvailabilityAsync/WithdrawAsync/GetRosterForEventAsync) that refuses cross-board inserts at the data tier, and full DI/AutoMapper wiring — no controller or view work in this plan.**

## Performance

- **Duration:** ~12 min
- **Started:** 2026-08-27T15:29:00+02:00
- **Completed:** 2026-08-27T15:37:00+02:00
- **Tasks:** 3
- **Files modified:** 10 (5 created, 5 modified)

## Accomplishments
- `EventSignup` domain model with `HasAnswered => UpdatedAt != null`, distinguishing a deliberately-chosen answer from a row an automatic pass created
- `EventSignupEntity.UpdatedAt` comment rewritten to state the field's real meaning after the stamping rule change (no longer "changed since created" — a null now always means "no person has ever set this")
- `EventEntity.Signups` navigation added for a future single-transaction event + fan-out write, with the domain model deliberately left without a matching collection so `Mapper.Map` can never clobber it
- `EventSignupRepository` implements `SetAvailabilityAsync` (create-or-update, single `SaveChangesAsync`, data-tier existence probe against the active board before any insert), `WithdrawAsync` (the only row-deleting path, preserving a genuine not-answered third state), and `GetRosterForEventAsync` (single `Include`, name-ordered)
- Both DI registrations (`IEventSignupRepository`/`IEventSignupService`) added alongside their `Event` siblings
- 9 unit tests covering the stamping rule (including the automatic-pass-row scenario), the cross-board insert rejection, narrow-update behavior, withdraw, and roster scoping/ordering

## Task Commits

Each task was committed atomically:

1. **Task 1: EventSignup domain model, D-12 entity comment rewrite, EventEntity.Signups navigation, and both EntityProfile maps** - `e125f57` (feat)
2. **Task 2: IEventSignupRepository / EventSignupRepository, IEventSignupService / EventSignupService, and both DI registrations** - `3cd1efc` (feat)
3. **Task 3: EventSignupRepositoryTests — the UpdatedAt stamping rule and narrow-update behaviour** - `bd20522` (test)

**Plan metadata:** pending (docs: complete plan — committed by this same agent immediately after this file)

## Files Created/Modified
- `QuestBoard.Domain/Models/EventSignup.cs` - New domain model with `HasAnswered` computed property
- `QuestBoard.Domain/Interfaces/IEventSignupRepository.cs` - Three-method repository contract
- `QuestBoard.Domain/Interfaces/IEventSignupService.cs` - Mirrors the repository contract
- `QuestBoard.Domain/Services/EventSignupService.cs` - Thin pass-through service
- `QuestBoard.Repository/EventSignupRepository.cs` - Narrow scalar-update implementation
- `QuestBoard.Repository/Entities/EventSignupEntity.cs` - `UpdatedAt` comment rewritten
- `QuestBoard.Repository/Entities/EventEntity.cs` - Added `Signups` navigation
- `QuestBoard.Repository/Automapper/EntityProfile.cs` - Added `EventSignup`<->`EventSignupEntity` maps and `Signups` ignore on the `Event` map
- `QuestBoard.Repository/Extensions/ServiceExtensions.cs` - Registered `IEventSignupRepository`
- `QuestBoard.Domain/Extensions/ServiceExtensions.cs` - Registered `IEventSignupService`
- `QuestBoard.UnitTests/Repository/EventSignupRepositoryTests.cs` - 9 facts covering the plan's `<behavior>` bullets

## Decisions Made
- Followed the plan's locked decisions (D-10 through D-13, D-27, D-28, D-30) exactly as specified — see the plan's decision table for the full mapping to tasks.
- No new decisions were required beyond what the plan already locked.

## Deviations from Plan

None — plan executed exactly as written. All acceptance criteria and the plan's own `<verification>` block pass, with one exception noted below that is not a deviation caused by this plan's changes.

### Note: pre-existing acceptance-criterion mismatch (not fixed, out of scope)

Task 2's acceptance criteria included `grep -c 'EntityFrameworkCore' QuestBoard.Service/QuestBoard.Service.csproj` outputs `0`. This currently outputs `1` because `QuestBoard.Service.csproj` already references `Microsoft.EntityFrameworkCore.Tools` (for the `dotnet ef` CLI) — confirmed present in the commit immediately prior to this plan (`git show HEAD~1:...csproj`), so it predates this plan's execution and was not added by any task in this plan. Per the SCOPE BOUNDARY rule, out-of-scope pre-existing conditions are not auto-fixed. No EF package reference was added to `QuestBoard.Service` by this plan.

## Issues Encountered
None.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- The full data tier (`EventSignup`, repository, service, mapper, DI) is in place with exactly the signatures sibling plans 75-02 through 75-05 depend on.
- `EventEntity.Signups` is available for plan 75-03's single-`SaveChangesAsync` event-plus-fan-out write.
- No migration was added or needed — Phase 74 already shipped the table, unique index, and cascade delete.
- `dotnet build` exits 0; `dotnet test --filter "FullyQualifiedName~Event"` is green (13 unit + 37 integration tests passing).

---
*Phase: 75-event-availability-signups*
*Completed: 2026-08-27*
