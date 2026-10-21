---
phase: 93-per-board-page-selection-and-home-page
plan: 11
subsystem: testing
tags: [xunit, integration-tests, static-guard, dependency-injection, platform-form, razor]

requires:
  - phase: 93-per-board-page-selection-and-home-page
    provides: BoardPageResolver, BoardPageGateFilter, Platform Edit form, home redirect, menu gating, board-list badge (plans 01-10, 12-15)
provides:
  - BoardPageStaticGuardTests scanning 80 source files for planning references
  - ProductionResolverWebApplicationFactory restoring the real IBoardTypeResolver behind the page resolver
  - BoardPageDependencyInjectionTests proving the page resolver resolves and answers through the production board-type resolver
  - BoardPageEndToEndTests walking the operator's target through the Platform form on desktop and mobile
affects: [phase verification, ship gate]

actuals:
  tokens: 5400
  tasks: 2
  commits: 2
plan_head_before: 2c151822b378ccda636b52d7fc19c4cdf43e536e

tech-stack:
  added: []
  patterns:
    - "Static guard with fragment-built regexes, one theory row per source file, violations reported with line number and label"
    - "Factory subclass re-registers the production IBoardTypeResolver after the base callback so the last registration wins"
    - "End-to-end settings changes are posted from the parsed Edit form, not seeded, so form, rules, storage, resolver, gate, menus and home redirect agree"

key-files:
  created:
    - QuestBoard.IntegrationTests/Tests/BoardPageStaticGuardTests.cs
    - QuestBoard.IntegrationTests/Helpers/ProductionResolverWebApplicationFactory.cs
    - QuestBoard.IntegrationTests/Tests/BoardPageDependencyInjectionTests.cs
    - QuestBoard.IntegrationTests/Tests/BoardPageEndToEndTests.cs
  modified: []

key-decisions:
  - "Guard file list was built from git diff c35512f3..HEAD (non-test, non-migration), which added dm-profile.mobile.css to the plan's list; all 80 listed files exist"
  - "End-to-end walk builds its POST from the rendered Edit form's own hidden Page fields and checkbox/hidden-false pairs rather than hand-writing field names"

requirements-completed: [D-01, D-05, D-07, D-09, D-10, D-11, D-14, D-15, D-16, D-18, D-21]

coverage:
  - id: D1
    description: "No source file the phase touched carries a decision id, phase or plan id, or planning document name; a guard fails if one appears"
    requirement: "D-01"
    verification:
      - kind: unit
        ref: "QuestBoard.IntegrationTests/Tests/BoardPageStaticGuardTests.cs#FeatureSourceFile_CarriesNoPlanningOrTrackingReference"
        status: pass
    human_judgment: false
  - id: D2
    description: "The page resolver resolves and answers through the production IBoardTypeResolver, and GET / redirects an unconfigured board to /quests"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Tests/BoardPageDependencyInjectionTests.cs"
        status: pass
    human_judgment: false
  - id: D3
    description: "A super admin sets a board's home to Calendar and switches Shop and Contacts off via the Platform form; a player lands on /Calendar, sees the Quests item and calendar brand link on both layouts, loses Shop and Contacts from the menus and is refused them with the toast; switching back restores the board exactly"
    requirement: "D-14"
    verification:
      - kind: e2e
        ref: "QuestBoard.IntegrationTests/Tests/BoardPageEndToEndTests.cs#SuperAdmin_OpensABoardOnItsCalendarWithPagesTrimmed_ThenPutsItBackExactlyAsItWas"
        status: pass
    human_judgment: false
  - id: D4
    description: "Campaign board Edit shows Shop, Players and DM profile greyed with the lock reason, and only the configured board carries the Home badge"
    requirement: "D-16"
    verification:
      - kind: e2e
        ref: "QuestBoard.IntegrationTests/Tests/BoardPageEndToEndTests.cs#SuperAdmin_OpensABoardOnItsCalendarWithPagesTrimmed_ThenPutsItBackExactlyAsItWas"
        status: pass
    human_judgment: false
  - id: D5
    description: "How the trimmed pages and menus actually look on a real device and browser"
    verification: []
    human_judgment: true
    rationale: "The suite reads markup and wording on the InMemory harness; it cannot judge layout or touch behaviour"

duration: 18min
completed: 2026-10-21
status: complete
---

# Phase 93 Plan 11: Phase-closing proofs Summary

**Planning-reference guard over 80 phase source files, a production-DI proof for the page resolver, and an end-to-end Platform-form walk that opens a board on /Calendar with Shop and Contacts gone and restores it exactly, on desktop and mobile layouts**

## Performance

- **Duration:** 18 min
- **Started:** 2026-10-21T13:23:00Z
- **Completed:** 2026-10-21T13:41:00Z
- **Tasks:** 2
- **Files modified:** 4 (all new, all test code)

## Accomplishments
- `BoardPageStaticGuardTests` scans every non-test, non-migration source file changed since the phase base for decision ids, this phase's phase and plan ids, and planning document names. All patterns are built from fragments so the guard file never holds them. A sanity fact requires at least 50 of the listed files to exist (80 do).
- `ProductionResolverWebApplicationFactory` puts the real `BoardTypeResolver` behind `BoardPageResolver`; `BoardPageDependencyInjectionTests` resolves `IBoardPageResolver` in a fresh scope, checks its answer matches board 1's stored type, and walks an authenticated `GET /` to `/quests`. This closes the production-DI debt for the resolver seam.
- `BoardPageEndToEndTests` drives the operator's target only through the Platform Edit form (posted from the rendered fields), then checks the root redirect, both menus, brand link, Quests item, the Shop refusal toast, the Contacts refusal, the Campaign board's locked rows, the Home badge, and the exact restore (quest-list brand link, no Quests item, Shop item and contact listed again). Runs on a desktop and a mobile user agent.

## Task Commits

1. **Task 1: Guard and production DI proof** - `377b148b` (test)
2. **Task 2: Operator end-to-end walk** - `f4436252` (test)

**Plan metadata:** committed with this summary (docs: complete plan)

_TDD note: both tasks are proofs over behavior built by earlier plans, so the new tests were green on their first run. The guard was cross-checked with an independent grep of the same files for the same patterns (no matches), and the end-to-end walk did fail once on a wrong expectation of mine (Location `/platform`, not `/platform/Group`), which shows its assertions discriminate. There is no separate `feat` commit because no production code changed._

## Files Created/Modified
- `QuestBoard.IntegrationTests/Tests/BoardPageStaticGuardTests.cs` - per-file planning-reference scan with sanity fact
- `QuestBoard.IntegrationTests/Helpers/ProductionResolverWebApplicationFactory.cs` - factory registering the production board-type resolver
- `QuestBoard.IntegrationTests/Tests/BoardPageDependencyInjectionTests.cs` - resolver graph and root-redirect facts
- `QuestBoard.IntegrationTests/Tests/BoardPageEndToEndTests.cs` - the operator's walk, one theory over both user agents

## Decisions Made
- Built the guard's file list from the real diff, which surfaced `wwwroot/css/dm-profile.mobile.css` that the plan did not list.
- Post the Edit form from its own rendered hidden `Pages[n].Page` fields and checkbox pairs, so a rename of a form field breaks the walk instead of passing vacuously.

## Deviations from Plan

None - plan executed exactly as written. (The guard list gained one file the plan allowed for: "Add any further file a plan SUMMARY lists as changed".) No source file needed a planning reference removed; none were found.

## Issues Encountered
- The only failure in the full run is the known intermittent `MigratorBackupAndApplyCliTests.StateChangingCommand_WhenServerIsUnreachable...` already recorded in `deferred-items.md`; ignored per plan notes.
- One foreground `dotnet test | grep` call hung with no output (pipe held open after the run); re-running with output redirected to a file completed normally.

## Verification Results
- `dotnet test QuestBoard.IntegrationTests --filter "BoardPageStaticGuard|BoardPageDependencyInjection|BoardMoveStaticGuard|LinkedBoardStaticGuard"`: 157 passed.
- `dotnet test QuestBoard.IntegrationTests --filter BoardPageEndToEnd`: 2 passed (desktop, mobile).
- Full `dotnet test`: integration 1652 passed, 11 skipped (pre-existing SQL Server skips), 0 failed; unit 1296 passed, 1 failed (the known Migrator flake).
- `git diff --name-only c35512f3 HEAD` over `CalendarFeedWriter.cs`, `CalendarSubscriptionService.cs`, `FeedRevisionStamper.cs`: empty. `AmbientClockSeamTests`, `CrossBoardIgnoreQueryFiltersSeamTests` and `AntiForgeryTokenCoverageTests` were green in the full run.

## Known Stubs

None.

## Threat Flags

None - test code only, no new runtime surface.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- Every plan in phase 93 now has a summary; the phase is ready for verification.
- Real-device appearance of the trimmed pages remains a human judgment (D5).

## Self-Check: PASSED

All four created files exist; commits `377b148b` and `f4436252` are present on `milestone/v9-rolling-improvements`.

---
*Phase: 93-per-board-page-selection-and-home-page*
*Completed: 2026-10-21*
