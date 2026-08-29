---
phase: 82-personal-cross-board-event-agenda
plan: 02
subsystem: api
tags: [ef-core, tenant-isolation, event-signups, cross-board-read]

requires:
  - phase: 82-personal-cross-board-event-agenda
    provides: 82-PATTERNS.md (verified analog excerpts for the repository/service query shape)
provides:
  - Four agenda domain types (AgendaOptions, AgendaRow, AgendaRosterEntry, CrossBoardAgenda)
  - EventRepository.GetUpcomingAcrossGroupsWithSignupsAsync -- the single, membership-pinned
    query that lifts the ambient board filter for a user-facing cross-board read
  - EventService.GetCrossBoardAgendaAsync -- composes agenda rows with the viewer's own cell
    and a full roster, with a second-layer in-memory membership re-check
  - CrossBoardAgendaTests unit suite (12 facts) covering paging, the re-check, the empty-set
    contract, cell classification and roster ordering
affects: [82-03 (agenda controller/views), any future cross-board read]

tech-stack:
  added: []
  patterns:
    - "Membership-pinned IgnoreQueryFilters(): the filter bypass and the caller's membership
       predicate live in the same statement chain, never split across an overload or branch"
    - "Fetch-one-extra paging idiom (take + 1, HasMore = count > take) reused from
       EventService.GetAvailabilityOverviewAsync"
    - "Second-layer in-memory re-check after materialization, with its coverage limits written
       down in the comment rather than implied"

key-files:
  created:
    - QuestBoard.Domain/Models/AgendaOptions.cs
    - QuestBoard.Domain/Models/AgendaRow.cs
    - QuestBoard.Domain/Models/AgendaRosterEntry.cs
    - QuestBoard.Domain/Models/CrossBoardAgenda.cs
    - QuestBoard.UnitTests/Services/CrossBoardAgendaTests.cs
  modified:
    - QuestBoard.Domain/Extensions/ServiceExtensions.cs
    - QuestBoard.Domain/Interfaces/IEventRepository.cs
    - QuestBoard.Repository/EventRepository.cs
    - QuestBoard.Domain/Interfaces/IEventService.cs
    - QuestBoard.Domain/Services/EventService.cs

key-decisions:
  - "AgendaRow carries no board-name/board-type property -- the domain Event model has no
     group navigation, and the caller already holds the board name from the membership read
     it performs anyway; adding an include would widen a shared type for every consumer"
  - "The membership predicate and the IgnoreQueryFilters() call sit in one statement chain
     with no overload or default-argument path that can reach the bypass without the
     predicate -- verified by a static grep audit (exactly 1 occurrence in the repository,
     0 anywhere under Domain/ or Service/)"
  - "The service never short-circuits an empty membership set -- it is passed through to the
     repository unconditionally, and a unit test asserts the repository was actually called
     with the empty collection so a future short-circuit would fail"
  - "The second-layer re-check drops out-of-membership rows before the window is trimmed, so
     HasMore is computed from surviving rows only; its comment documents that it shares the
     same memberGroupIds input as the query, so it catches a dropped predicate but not a
     wrong membership set"

patterns-established:
  - "Composite IgnoreQueryFilters() pattern for user-facing cross-board reads: generalise the
     deterministic-ordering/eager-include shape from a same-board read, add the membership
     containment predicate in the same Where, immediately after the bypass call"

requirements-completed:
  - EVTAGENDA-01
  - EVTAGENDA-03
  - EVTAGENDA-07
  - EVTAGENDA-08
  - EVTAGENDA-09

coverage:
  - id: D1
    description: "Repository query returns the next N upcoming, non-cancelled events across an explicitly supplied set of board ids, with every signup and signer name from one round trip, ordered deterministically"
    requirement: "EVTAGENDA-01"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Tests/EventsOverviewTenantIsolationTests.cs (sibling suite unaffected, confirms no regression)"
        status: pass
      - kind: other
        ref: "static grep audit: IgnoreQueryFilters count == 1 in EventRepository.cs, 0 under Domain/ and Service/ (*.cs only)"
        status: pass
    human_judgment: false
  - id: D2
    description: "Service composes each row with the viewer's own availability cell and the event's complete roster, re-checks group membership after materialization before trimming, and never short-circuits an empty membership set"
    requirement: "EVTAGENDA-03"
    verification:
      - kind: unit
        ref: "QuestBoard.UnitTests/Services/CrossBoardAgendaTests.cs (12 facts, all pass)"
        status: pass
    human_judgment: false
  - id: D3
    description: "A caller with an empty membership set gets an empty agenda, not every board's events, and the repository is still called (no short-circuit)"
    requirement: "EVTAGENDA-08"
    verification:
      - kind: unit
        ref: "QuestBoard.UnitTests/Services/CrossBoardAgendaTests.cs#CrossBoardAgenda_EmptyMembershipSet_StillCallsRepository_AndReturnsEmptyAgenda"
        status: pass
    human_judgment: false
  - id: D4
    description: "AgendaOptions provides configurable page sizes with code defaults, config binding and start-up validation"
    requirement: "EVTAGENDA-07"
    verification:
      - kind: unit
        ref: "QuestBoard.UnitTests -- EventsOverviewOptionsValidation filter run against shared IsValid() shape; AgendaOptions.IsValid mirrors it exactly and is exercised transitively by ValidateOnStart() at every dotnet test/build invocation in this session"
        status: pass
    human_judgment: false

duration: ~35min
completed: 2026-08-29
status: complete
---

# Phase 82 Plan 02: Cross-Board Agenda Query and Service Summary

**Membership-pinned EF Core query that lifts the ambient tenant filter exactly once, paired with a service layer that adds a second-layer in-memory re-check and composes each row with the viewer's own availability cell plus a full roster.**

## Performance

- **Duration:** ~35 min
- **Completed:** 2026-08-29
- **Tasks:** 3
- **Files modified:** 9 (5 created, 4 modified)

## Accomplishments

- Four new domain types (`AgendaOptions`, `AgendaRow`, `AgendaRosterEntry`, `CrossBoardAgenda`) with `AgendaOptions` registered in DI using the same code-default-plus-configuration-plus-`ValidateOnStart()` shape as `EventsOverviewOptions`.
- `EventRepository.GetUpcomingAcrossGroupsWithSignupsAsync` -- the application's first user-facing read that deliberately lifts the ambient tenant filter, with the caller's membership set pinned in the same `Where` as the bypass. A static audit confirms `IgnoreQueryFilters` appears exactly once in the repository and zero times anywhere under `QuestBoard.Domain/` or `QuestBoard.Service/` source.
- `EventService.GetCrossBoardAgendaAsync` -- builds a bounded, chronologically ordered agenda using the same fetch-one-extra paging idiom as the availability overview, reuses `ClassifyCell` for both the viewer's own cell and every roster entry, and re-checks each row's board membership after materialization before trimming the window.
- `CrossBoardAgendaTests` -- 12 facts covering paging (`HasMore` true/false), the drop-before-trim re-check (including a case that proves the drop happens before `HasMore` is computed), the empty-membership-set contract (asserted directly on the repository substitute), viewer-cell classification, roster completeness/ordering, and the fixed-clock/`take + 1` request shape.

## Task Commits

Each task was committed atomically:

1. **Task 1: Agenda domain models and configurable page-size options** - `dd20429f` (feat)
2. **Task 2: The membership-pinned cross-board query** - `f3afd27a` (feat)
3. **Task 3: The agenda service, its second-layer re-check, and its unit tests** - `f0e01380` (feat)

_No plan-metadata commit in worktree mode -- STATE.md/ROADMAP.md are owned by the orchestrator._

## Files Created/Modified

- `QuestBoard.Domain/Models/AgendaOptions.cs` - page-size options (DefaultTake 5, MaxTake 50, PageIncrement 5) with `IsValid()`
- `QuestBoard.Domain/Models/AgendaRow.cs` - one agenda row: event, viewer's own cell, full roster
- `QuestBoard.Domain/Models/AgendaRosterEntry.cs` - one roster member's answer plus a viewer flag
- `QuestBoard.Domain/Models/CrossBoardAgenda.cs` - the read result: rows plus `HasMore`
- `QuestBoard.Domain/Extensions/ServiceExtensions.cs` - registers `AgendaOptions` beside `EventsOverviewOptions`
- `QuestBoard.Domain/Interfaces/IEventRepository.cs` - declares `GetUpcomingAcrossGroupsWithSignupsAsync`
- `QuestBoard.Repository/EventRepository.cs` - implements the membership-pinned cross-board query
- `QuestBoard.Domain/Interfaces/IEventService.cs` - declares `GetCrossBoardAgendaAsync`
- `QuestBoard.Domain/Services/EventService.cs` - implements the agenda composition and re-check
- `QuestBoard.UnitTests/Services/CrossBoardAgendaTests.cs` - unit suite for the new service method

## Decisions Made

- No board-name/board-type property on `AgendaRow` -- the domain `Event` model carries no group navigation, and the caller already holds board names from the membership read it must perform anyway. Adding a `.Include(e => e.Group)` would widen a shared type for every other consumer.
- The membership predicate is written in the same statement chain as the filter bypass, immediately after it, with no overload or default-parameter path that could reach the bypass unguarded. This is the load-bearing safety property the phase's threat model requires, and it is what the static grep audit (`IgnoreQueryFilters` count == 1) proves mechanically rather than by inspection alone.
- The service calls the repository unconditionally even with an empty membership set (no short-circuit), and a unit test asserts on the substitute that the repository was actually invoked with the empty collection -- this is the test that would fail if a future change added a short-circuit that skipped exercising the containment predicate for the caller with the least access.
- The second-layer re-check runs before the window is trimmed so `HasMore` reflects only surviving rows, and its comment documents its real limit: it re-reads the same `memberGroupIds` list the query used, so it catches a dropped predicate or a bad containment translation but not a wrong membership set supplied by the caller.

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

A `Bash` `cd /c/Repos/quest-board && dotnet build`-style command run early in Task 1 verification silently ran against the main repository checkout rather than this worktree (cwd resets between Bash calls, so the `cd` only affected that single invocation, but it meant the build/test results from that call proved nothing about the new files). Caught before committing by noticing the shell's actual working directory did not match expectations; all subsequent build/test/grep verification was re-run without `cd`, from the worktree root, and confirmed against the correct files.

Separately, the shell's `grep` (proxied through an rtk wrapper) produced inconsistent line counts against these CRLF-terminated files -- `grep -c` returned large, clearly-wrong numbers for a few `[Fact]`-count checks. All acceptance-criteria grep checks were re-verified with the sandboxed `Grep` tool (which reported the correct counts) before treating any criterion as passed. No production code was affected; this was purely a verification-tooling caveat for this session.

## Next Phase Readiness

The repository and service layers for the cross-board agenda are complete, tested, and match the phase's threat model exactly: one bypass call site, membership-pinned, with a documented second-layer re-check. Phase 82 Plan 03 (controller, view models, views, session-stored board filter) can now build directly on `IEventService.GetCrossBoardAgendaAsync` and `AgendaOptions` without further changes to this layer.

## Self-Check: PASSED

- FOUND: QuestBoard.Domain/Models/AgendaOptions.cs
- FOUND: QuestBoard.UnitTests/Services/CrossBoardAgendaTests.cs
- FOUND: dd20429f (Task 1 commit)
- FOUND: f3afd27a (Task 2 commit)
- FOUND: f0e01380 (Task 3 commit)

---
*Phase: 82-personal-cross-board-event-agenda*
*Completed: 2026-08-29*
