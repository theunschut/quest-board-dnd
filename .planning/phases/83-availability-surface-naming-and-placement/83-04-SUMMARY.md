---
phase: 83-availability-surface-naming-and-placement
plan: 04
subsystem: testing
tags: [xunit, integration-tests, authorization]

# Dependency graph
requires:
  - phase: 83-availability-surface-naming-and-placement
    provides: "plans 01-03's rename, subtitle, cross-link gating and nav placement -- this plan's guard class fails by design if run before those land"
provides:
  - Automated proof a Player still gets a 200 and the rendered grid from GET /Events
  - StaleAvailabilityOverviewLabelGuardTests, the structural guard against a partial rename leaving two names for one page
affects: []

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Cross-surface guard class fetching multiple distinct pages (not a single controller) with a shared GetWithUserAgentAsync helper, authenticated as a Dungeon Master so every affected surface is observed by one role"

key-files:
  created:
    - QuestBoard.IntegrationTests/Controllers/StaleAvailabilityOverviewLabelGuardTests.cs
  modified:
    - QuestBoard.IntegrationTests/Controllers/EventsOverviewControllerIntegrationTests.cs

key-decisions:
  - "Task 3 (minting EVTNAME-01 through EVTNAME-07 in .planning/REQUIREMENTS.md and closing out the Phase 83 roadmap entry) was deliberately NOT executed in this worktree. A second, concurrently-running Claude session was actively editing .planning/ROADMAP.md on the parent checkout at the time this plan ran; editing those two shared ledgers from an isolated worktree whose copy was already stale would either conflict or silently overwrite the other session's work. Task 3 is deferred to the orchestrator, which will perform it against a fresh read of the main working tree immediately after merging this plan's two commits."
  - "Wrote the new StaleAvailabilityOverviewLabelGuardTests.cs file via the Write tool (which emits LF), then converted it to CRLF with a follow-up PowerShell pass before running any acceptance criteria or tests, matching the same post-write conversion plan 83-02 already used for its own new test file."

requirements-completed: [EVTNAME-01, EVTNAME-07]

coverage:
  - id: D1
    description: "An automated case proves a caller holding only the Player role gets a 200 from GET /Events with the grid actually rendered"
    requirement: "EVTNAME-07"
    verification:
      - kind: integration
        ref: "dotnet test --filter EventsOverviewControllerIntegrationTests (19/19 passed, including Index_PlayerWithoutDmRole_ReturnsOkAndRendersGrid)"
        status: pass
    human_judgment: false
  - id: D2
    description: "A dedicated guard class proves the retired page label appears in the rendered HTML of none of Board Availability, My Agenda and the Calendar, on both user agents, as a Dungeon Master"
    requirement: "EVTNAME-01"
    verification:
      - kind: integration
        ref: "dotnet test --filter StaleAvailabilityOverviewLabelGuardTests (6/6 passed); grep -rl 'Availability Overview' across QuestBoard.Service/Domain/Repository/IntegrationTests/UnitTests names only the new guard class file"
        status: pass
    human_judgment: false

duration: ~20min
completed: 2026-08-30
status: complete
---

# Phase 83 Plan 04: Player-Reachability Case and Cross-Surface Retired-Label Guard Summary

**Added the automated proof that a Player still gets a working, fully-rendered `/Events` page and a new dedicated test class that guards against a partial rename leaving two names for one page across Board Availability, My Agenda and the Calendar.**

## Performance

- **Duration:** ~20 min
- **Completed:** 2026-08-30
- **Tasks:** 2/2 (Task 3 deferred to the orchestrator — see below)
- **Files modified:** 2 (1 modified, 1 new)

## Accomplishments
- Added `Index_PlayerWithoutDmRole_ReturnsOkAndRendersGrid` to `EventsOverviewControllerIntegrationTests`, placed directly after `Index_PlayerOnCampaignBoard_ReturnsOk`, seeding one event, one member and one confirmed signup, authenticating explicitly as a Player, and requiring a 200 status plus the rendered grid, seeded event title and seeded member name. No existing case in the file was altered.
- Created `StaleAvailabilityOverviewLabelGuardTests`, a new xUnit class carrying three `[Theory]` methods (`BoardAvailabilityPage_DoesNotRenderRetiredLabel`, `MyAgendaPage_DoesNotRenderRetiredLabel`, `CalendarPage_DoesNotRenderRetiredLabel`) each run over `DesktopUserAgent` and `MobileUserAgent` — six executed cases — authenticated as a Dungeon Master, each requiring a 200, a positive page marker for the surface actually fetched, and the absence of the retired `"Availability Overview"` label.

## Task Commits

Each task was committed atomically:

1. **Task 1: Prove a Player still gets a working page at /Events** - `d632a948` (test)
2. **Task 2: Add the cross-surface guard class against a partial rename** - `faa632db` (test)

**Task 3 was NOT executed in this worktree — see "Deferred to Orchestrator" below.**

## Files Created/Modified
- `QuestBoard.IntegrationTests/Controllers/EventsOverviewControllerIntegrationTests.cs` — added one `[Fact]` case; every pre-existing case is byte-identical (`git diff` shows only additions, 25 insertions / 0 deletions)
- `QuestBoard.IntegrationTests/Controllers/StaleAvailabilityOverviewLabelGuardTests.cs` — new file, 99 lines, CRLF, three theories over two user agents

## Decisions Made
- No architectural decisions required. Followed the plan's markup and assertion shapes verbatim: the `GetWithUserAgentAsync` helper copied from `CalendarButtonStyleTests`'s shape, the `DesktopUserAgent`/`MobileUserAgent` constants copied verbatim from `LayoutNavigationTests`, and the `IClassFixture`/`IAsyncLifetime`/`DisposeAsync` reset shape copied from `RowNavigationAccessibilityTests`, per the Pattern Map's "No Analog Found" guidance.
- The guard class's positive-marker assertions use each page's own heading text (`"Board Availability"`, `"My Agenda"`, `"Calendar"`) rather than a structural marker, matching the plan's explicit instruction and avoiding any risk of the guard becoming a permanently-green no-op against a blank or error response.

## Deviations from Plan

None — plan executed exactly as written for Tasks 1 and 2. All acceptance-criteria greps matched their predicted counts on the first attempt:
- Task 1: `Index_PlayerWithoutDmRole_ReturnsOkAndRendersGrid` count 1, `roles: ["Player"]` count 2, `avail-grid` count 2 (at least 1 required), `Index_PlayerOnCampaignBoard_ReturnsOk` count 1 (untouched), diff shows 25 insertions / 0 deletions, zero GSD-reference matches.
- Task 2: `NotContain("Availability Overview")` count 3, `NotContain("Board Availability")` count 0, `[Theory]` count 3, `InlineData(` count 6, `CreateAuthenticatedDMClientAsync` count 3, `CreateAuthenticatedClientWithUserAsync` count 0, `"/quests"` count 0, `HttpStatusCode.OK` count 3, no HTML parser introduced, zero GSD-reference matches, CRLF confirmed (99 lines / 99 CR-terminated lines), no csproj change needed (project globs sources).

**Total deviations:** 0 auto-fixed.

## Deferred to Orchestrator

**Task 3 of this plan — minting `EVTNAME-01` through `EVTNAME-07` in `.planning/REQUIREMENTS.md` and closing out the Phase 83 entry in `.planning/ROADMAP.md` — was explicitly excluded from this worktree's scope per the orchestrator's instructions and was NOT executed here.**

Reason: a second Claude session was running concurrently against the parent (non-worktree) checkout and actively editing `.planning/ROADMAP.md` while this plan executed. This worktree's copy of `.planning/ROADMAP.md` and `.planning/REQUIREMENTS.md` was a point-in-time snapshot taken at worktree creation and would go stale the moment the concurrent session committed its own edits to those files. Performing Task 3 here risked either a silent merge conflict or overwriting the other session's in-flight requirement/roadmap work with a stale base. The orchestrator will perform Task 3 against a fresh read of the main working tree immediately after merging this plan's two commits (`d632a948`, `faa632db`), so the identifier minting and roadmap reconciliation land on top of whatever the concurrent session has already committed rather than beneath it.

Consistent with plans 83-01, 83-02 and 83-03's own summaries: `requirements mark-complete EVTNAME-01 EVTNAME-07` was not run in this worktree for the same underlying reason — `.planning/REQUIREMENTS.md` still has no `EVTNAME-*` section as of this plan's execution, and this worktree's copy of that file must not be the one that ends up merged back for those definitions.

## Issues Encountered

None. `dotnet build` exited 0 after both tasks. `dotnet test --filter "EventsOverviewControllerIntegrationTests|StaleAvailabilityOverviewLabelGuardTests"` reported 24/24 passing. `dotnet test --filter "LayoutNavigationTests|CalendarButtonStyleTests"` (the wave-1 classes this plan does not touch) reported 48/48 passing, confirming no regression. `grep -rl 'Availability Overview' QuestBoard.Service/ QuestBoard.Domain/ QuestBoard.Repository/ QuestBoard.IntegrationTests/ QuestBoard.UnitTests/` named only `StaleAvailabilityOverviewLabelGuardTests.cs`, matching the plan's stated end state exactly.

## User Setup Required

None — no external service configuration required.

## Next Phase Readiness

Both automated controls the plan set out to add now exist and are green: the player-reachability case that fails loudly if `EventsController.Index` ever gains an authorization policy, and the cross-surface guard that fails loudly if any future edit reintroduces the retired label on any of the three affected surfaces. The only remaining work to fully close Phase 83 is Task 3 (the requirement and roadmap ledger close-out), which the orchestrator will perform on the main working tree after merging this worktree's two commits.

---
*Phase: 83-availability-surface-naming-and-placement*
*Completed: 2026-08-30*

## Self-Check: PASSED

- FOUND: QuestBoard.IntegrationTests/Controllers/EventsOverviewControllerIntegrationTests.cs
- FOUND: QuestBoard.IntegrationTests/Controllers/StaleAvailabilityOverviewLabelGuardTests.cs
- FOUND: commit d632a948
- FOUND: commit faa632db
