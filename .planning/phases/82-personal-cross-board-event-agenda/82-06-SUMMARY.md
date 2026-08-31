---
phase: 82-personal-cross-board-event-agenda
plan: 06
subsystem: testing
tags: [xunit, fluentassertions, tenant-isolation, integration-tests, static-audit]

requires:
  - phase: 82-personal-cross-board-event-agenda
    provides: 82-02 (IEventService.GetCrossBoardAgendaAsync, the membership-pinned repository query)
  - phase: 82-personal-cross-board-event-agenda
    provides: 82-03 (AgendaController, the session filter, and AgendaControllerIntegrationTests)
provides:
  - AgendaTenantIsolationTests -- an 8-fact suite proving the four mandated isolation cases as
    distinct facts, including the two-joined-boards case that proves aggregation rather than
    mere absence
  - Seven filter-behaviour facts added to AgendaControllerIntegrationTests covering the
    filter-before-the-window ordering, session persistence, the reset sentinel, and window clamping
  - The phase-gate static audit, re-run against the fully merged tree and recorded below
affects: []

tech-stack:
  added: []
  patterns:
    - "Three-board seeding harness generalising the sibling two-board isolation suites, with a
       LeaveBoardAsync helper that calls the real IGroupService.RemoveMemberAsync and verifies
       the membership row's removal directly against the unfiltered seeding context"

key-files:
  created:
    - QuestBoard.IntegrationTests/Tests/AgendaTenantIsolationTests.cs
  modified:
    - QuestBoard.IntegrationTests/Tests/AgendaControllerIntegrationTests.cs

key-decisions:
  - "The two-joined-boards fact (Agenda_ForMemberOfTwoOfThreeBoards_ShowsBothJoinedBoardsAndNotTheThird)
     was kept as its own fact, never folded into the non-member-absence fact -- it is the only
     fact in the suite that would fail if the aggregation silently collapsed to a single board"
  - "LeaveBoardAsync verifies its own post-condition by re-reading UserGroups through the
     unfiltered seeding context after calling the real domain service, rather than trusting the
     service call's return value alone"
  - "The filter-before-the-window fact seeds board two with six events (one more than the
     default window of five) and a distinct board one event, then proves the filtered request
     surfaces an event that was absent from the prior unfiltered request -- this is the one
     fact that would fail if filtering were applied after the window instead of before it"
  - "Window clamp facts count events by a distinctive title prefix (e.g. 'Clamp Ceiling Session')
     rather than counting literal digits, so a two-digit event number (Session 10) cannot
     produce a false match against a one-digit sibling (Session 1)"

patterns-established: []

requirements-completed:
  - EVTAGENDA-04
  - EVTAGENDA-07
  - EVTAGENDA-08
  - EVTAGENDA-09

coverage:
  - id: D1
    description: "A non-member board contributes nothing to the agenda -- no event title, member name, or board name"
    requirement: "EVTAGENDA-09"
    verification:
      - kind: integration
        ref: "AgendaTenantIsolationTests.Agenda_ForMemberOfOneBoard_ShowsNothingFromANonMemberBoard"
        status: pass
    human_judgment: false
  - id: D2
    description: "A viewer in two of three boards sees both joined boards interleaved by date and never the third -- the fact that proves aggregation rather than absence"
    requirement: "EVTAGENDA-09"
    verification:
      - kind: integration
        ref: "AgendaTenantIsolationTests.Agenda_ForMemberOfTwoOfThreeBoards_ShowsBothJoinedBoardsAndNotTheThird"
        status: pass
      - kind: integration
        ref: "AgendaTenantIsolationTests.Agenda_ForMemberOfTwoBoards_InterleavesRowsByDate"
        status: pass
    human_judgment: false
  - id: D3
    description: "Leaving a board removes it from the agenda on the very next request, with the pre-leave state asserted so the fact cannot pass vacuously"
    requirement: "EVTAGENDA-07"
    verification:
      - kind: integration
        ref: "AgendaTenantIsolationTests.Agenda_AfterLeavingABoard_ShowsNothingFromItOnTheNextRequest"
        status: pass
    human_judgment: false
  - id: D4
    description: "The board filter can never widen the set, whether the foreign id arrives on the query string or a stale session value naming a board the viewer has since left"
    requirement: "EVTAGENDA-04"
    verification:
      - kind: integration
        ref: "AgendaTenantIsolationTests.Agenda_FilterNamingANonMemberBoard_DoesNotWidenTheSet"
        status: pass
      - kind: integration
        ref: "AgendaTenantIsolationTests.Agenda_StaleSessionFilterNamingALeftBoard_IsIgnored"
        status: pass
    human_judgment: false
  - id: D5
    description: "A SuperAdmin is scoped by their own membership rows exactly like anyone else -- no all-groups branch on this page"
    requirement: "EVTAGENDA-08"
    verification:
      - kind: integration
        ref: "AgendaTenantIsolationTests.Agenda_SuperAdminWithNoMemberships_SeesNoBoards"
        status: pass
      - kind: integration
        ref: "AgendaTenantIsolationTests.Agenda_SuperAdminMemberOfOneBoard_SeesOnlyThatBoard"
        status: pass
    human_judgment: false
  - id: D6
    description: "The filter narrows before the window is taken, is remembered across requests, is resettable via the reset sentinel, carries through paging, and clamps the window size at both ends"
    requirement: "EVTAGENDA-04"
    verification:
      - kind: integration
        ref: "AgendaControllerIntegrationTests.Agenda_FilteringToOneBoard_PullsFurtherEventsIntoTheWindow"
        status: pass
      - kind: integration
        ref: "AgendaControllerIntegrationTests.Agenda_FilterSelection_PersistsAcrossRequestsWithNoFilterParameter"
        status: pass
      - kind: integration
        ref: "AgendaControllerIntegrationTests.Agenda_ResetSentinel_ClearsStoredSelection_AndShowsEveryBoardAgain"
        status: pass
      - kind: integration
        ref: "AgendaControllerIntegrationTests.Agenda_DeselectingEveryBoard_PersistsAcrossNextPlainRequest"
        status: pass
      - kind: integration
        ref: "AgendaControllerIntegrationTests.Agenda_ShowMoreLink_CarriesEnlargedWindowAndCurrentSelection"
        status: pass
      - kind: integration
        ref: "AgendaControllerIntegrationTests.Agenda_WindowSizeAboveCeiling_IsClampedDown"
        status: pass
      - kind: integration
        ref: "AgendaControllerIntegrationTests.Agenda_WindowSizeOfZero_IsClampedUp"
        status: pass
    human_judgment: false
  - id: D7
    description: "Phase-gate static audit: exactly one filter bypass in the repository layer, zero anywhere in Domain/ or Service/"
    requirement: "EVTAGENDA-04"
    verification:
      - kind: other
        ref: "Sandboxed Grep: IgnoreQueryFilters count == 1 in QuestBoard.Repository/EventRepository.cs; 0 files under QuestBoard.Domain/ and QuestBoard.Service/"
        status: pass
    human_judgment: false

duration: ~55min
completed: 2026-08-29
status: complete
---

# Phase 82 Plan 06: Cross-Board Agenda Tenant Isolation Suite and Static Audit Summary

**An 8-fact three-board isolation suite proving the cross-board agenda is bounded by the viewer's own memberships from four directions -- including the two-joined-boards case that is the only fact that would fail if the aggregation silently collapsed to one board -- plus seven filter-behaviour facts and a phase-gate static audit confirming exactly one `IgnoreQueryFilters` call site, in the repository layer only.**

## Performance

- **Duration:** ~55 min
- **Completed:** 2026-08-29
- **Tasks:** 3
- **Files modified:** 2 (1 created, 1 modified)

## Accomplishments

- `AgendaTenantIsolationTests.cs` -- a new 8-fact suite generalising the sibling two-board isolation harnesses (`EventAvailabilityTenantIsolationTests`, `EventsOverviewTenantIsolationTests`) to three boards. Covers: a non-member board fully absent; a viewer in two of three boards seeing both joined boards interleaved by date with the third absent (kept as its own fact, deliberately not folded into the absence fact); a genuine date interleaving proof; a board disappearing from the agenda on the very next request after the viewer leaves it (with the pre-leave state asserted); a filter naming a non-member board never widening the result; a stale session filter naming a left board being silently ignored; and a SuperAdmin scoped by their own memberships exactly like anyone else, both with zero memberships and with exactly one.
- A `LeaveBoardAsync` test helper that calls the real `IGroupService.RemoveMemberAsync` (the same path production code uses) and independently verifies the membership row's removal by re-reading `UserGroups` through the unfiltered seeding context, so a silent no-op in the service could never make a dependent fact pass for the wrong reason.
- Seven new facts added to `AgendaControllerIntegrationTests.cs`: the load-bearing filter-before-the-window fact (filtering to one board pulls an event into the window that was absent from the prior unfiltered request); filter-selection session persistence across a plain request; the `boards=all` reset sentinel clearing the stored selection; the all-boards-filtered-out state surviving a later plain request; the Show More link carrying both the enlarged window and the current selection; and window-size clamping at both the ceiling (a `take=100000` request still renders exactly 50 rows) and the floor (`take=0` still renders exactly 1 row, with a working Show More link).
- The phase-gate static audit, re-run against the fully merged tree at the end of wave 4 rather than trusted from wave 1's report: `IgnoreQueryFilters` appears exactly once, in `QuestBoard.Repository/EventRepository.cs`, and zero times under `QuestBoard.Domain/` or `QuestBoard.Service/`.
- Full solution test run: 420 unit tests and 603 integration tests, all passing.

## Task Commits

Each task was committed atomically:

1. **Task 1: Three-board harness, non-member absence, and the two-joined-boards proof** - `3fa2cbb1` (test)
2. **Task 2: Leaving a board, the filter that cannot widen, and SuperAdmin scoping** - `8f607584` (test)
3. **Task 3: Filter behaviour facts and the phase-gate static audit** - `7e169780` (test)

_No plan-metadata commit in worktree mode -- STATE.md/ROADMAP.md are owned by the orchestrator._

## Files Created/Modified

- `QuestBoard.IntegrationTests/Tests/AgendaTenantIsolationTests.cs` - the three-board isolation suite (8 facts) plus the `LeaveBoardAsync` helper
- `QuestBoard.IntegrationTests/Tests/AgendaControllerIntegrationTests.cs` - seven added filter-behaviour and window-clamp facts (11 -> 18 total facts)

## Decisions Made

- The two-joined-boards fact is kept structurally separate from the non-member-absence fact, per the plan's explicit instruction: it is the only fact in the entire suite that would fail if the cross-board query silently collapsed to a single board's results, so folding it into an absence-only assertion would remove the one check that actually proves the feature works.
- `LeaveBoardAsync` sets `TestGroupContext.ActiveGroupId` to the board being left before calling `RemoveMemberAsync`, matching the plan's instruction, even though inspection of `QuestBoardContext`'s model-building confirmed `UserGroupEntity` carries no query filter at all (unlike `EventSignupEntity`, which does) -- the membership row is reachable regardless of the active board. The pinning is kept anyway since it is harmless and matches the plan's explicit shape; the real safety net is the post-condition assertion re-reading `UserGroups` through the unfiltered seeding context, which is what would actually catch a silent no-op.
- The filter-before-the-window fact seeds exactly one more event on the filtered board than the default window (six events against a `DefaultTake` of five) so the newly-surfaced event is deterministic and unambiguous, and asserts both that the sixth event stays excluded (window still bounded) and that the fifth event newly appears (window narrowed before being taken) -- proving the ordering without needing to inspect the query plan directly.
- Window-clamp facts assert on a shared title *prefix* (e.g. `"Clamp Ceiling Session"`) rather than a full title match, so counting occurrences of the prefix across the rendered body correctly counts every rendered row regardless of one- vs two-digit suffixes, without needing per-title exact-match plumbing for fifty-one seeded events.

## Deviations from Plan

None - plan executed exactly as written. All acceptance criteria and behaviors described in the plan's three tasks were implemented and verified on the first test run, with no auto-fixes needed.

## Issues Encountered

None. Per this project's known caveat, every acceptance-criteria grep count in this plan (fact counts, the `groupId: 3` seeding literal, `RemoveMemberAsync`/`CreateAuthenticatedSuperAdminClientAsync` presence, tracking-identifier absence, and all three static-audit counts) was verified with the sandboxed `Grep` tool rather than the shell's `rtk`-proxied `grep`, and line-ending consistency (CRLF, no mixed LF/CRLF lines) was independently verified byte-for-byte after every write.

## Known Stubs

None -- this plan adds no production code and no view markup; every added file is a test file exercising already-shipped behaviour.

## Threat Flags

None. All new surface in this plan is test code exercising existing production surface already covered by earlier plans' threat models; no new network endpoint, auth path, file access pattern, or schema change was introduced.

## Next Phase Readiness

This is the phase's final plan. The cross-board agenda is now proven bounded by the viewer's own memberships from all four mandated directions (non-member absence, two-joined-boards aggregation, leave-a-board, and the non-widening filter), the filter's narrow-before-window/session-persistence/reset/paging/clamp behaviour is fully covered, and the phase-gate static audit is recorded green against the fully merged tree. No further work is outstanding for this phase's proof obligation.

## Self-Check: PASSED

- FOUND: QuestBoard.IntegrationTests/Tests/AgendaTenantIsolationTests.cs
- FOUND: QuestBoard.IntegrationTests/Tests/AgendaControllerIntegrationTests.cs (extended)
- FOUND: .planning/phases/82-personal-cross-board-event-agenda/82-06-SUMMARY.md
- FOUND: 3fa2cbb1 (Task 1 commit)
- FOUND: 8f607584 (Task 2 commit)
- FOUND: 7e169780 (Task 3 commit)

---
*Phase: 82-personal-cross-board-event-agenda*
*Completed: 2026-08-29*
