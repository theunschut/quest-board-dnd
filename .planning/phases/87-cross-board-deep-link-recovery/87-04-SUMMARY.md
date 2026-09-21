---
phase: 87-cross-board-deep-link-recovery
plan: 04
subsystem: auth
tags: [aspnet-core-mvc, ef-core-query-filters, session, tenancy, integration-testing]

# Dependency graph
requires:
  - phase: 87-01
    provides: "CrossBoardWebApplicationFactory harness, CrossBoardLinkRepository, ICrossBoardLinkResolver/CrossBoardLinkResolverService, CrossBoardDeepLinkMiddleware, TempDataKeys, the tracer route's paired-parity fact this plan generalises"
  - phase: 87-02
    provides: "CrossBoardLinkRegistry widened to all 18 routes and 8 lookup kinds, the entity-backed projections this plan's family theory exercises"
  - phase: 87-03
    provides: "GroupPickerController's picker-skip path, proven separately and not re-tested here"
provides:
  - "CrossBoardOracleParityTests -- the non-member/nonexistent-id paired-parity fact generalised from one route to all 8 lookup-kind families, plus a SuperAdmin, plus the no-half-switch and bare-404 structural facts"
  - "CrossBoardAuthorizationBoundaryTests -- both directions of the middleware's pre-authorization pipeline position pinned as a tested contract, plus proof that a cancelled event and a Draft shop item still resolve and switch even though the page itself declines to serve them"
affects: []

# Actuals (#2632)
actuals:
  tokens: 7300
  tasks: 2
  commits: 2

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Family-level parity theory: one paired non-member/nonexistent-id fact per lookup kind rather than per route, since routes sharing a kind share the exact same resolver code path"
    - "Header-set comparison with a named volatile-header exclusion list (Date, Request-Id, traceparent), stronger than the tracer's original status/body/Content-Type-only comparison"
    - "Pipeline-position-as-contract: an authorization-boundary fact that fails specifically if the middleware's Program.cs registration moves after UseAuthorization, rather than merely documenting the ordering in a comment"

key-files:
  created:
    - QuestBoard.IntegrationTests/Security/CrossBoardOracleParityTests.cs
    - QuestBoard.IntegrationTests/Security/CrossBoardAuthorizationBoundaryTests.cs
  modified: []

key-decisions:
  - "Family routes for the parity theory use each family's natural Details-style read route where one exists (Quest, Event, Character, Contact, ShopItem, EventSeries); ContactCategory and BoardMember have no registered Details route, so their one registered route (an Edit page, a Profile page) stands in for the family, per the plan's own recorded per-family (not per-route) scoping choice"
  - "The viewer in the parity seed is Admin on every board they belong to, isolating the parity fact to the resolver's own behavior -- no policy check in any of the eight routes under test can itself distinguish the non-member request from the nonexistent-id request, so the only thing that can differ is what CrossBoardLinkRepository does"
  - "The 'must now succeed' authorization-boundary fact makes the viewer the target quest's own Dungeon Master (not merely a DM-tier role on the target board), because QuestController.Edit carries an independent ownership check beyond the DungeonMasterOnly policy -- isolating the fact to the policy question the plan asks, rather than tangling it with a second, unrelated ownership question the action asks afterward"
  - "The two 'resolves but the page will not serve it' facts assert the board-switch banner's board name and the entity's own visibility marker in the same response the resolver produced, rather than a separate follow-up request -- proving both the switch and the page's own decision in one HTTP round trip, matching the tracer suite's existing style"

requirements-completed: [D-01, D-11, D-12, D-13, D-14, D-15, D-17, D-18, D-19, D-20, D-21]

duration: ~45min
completed: 2026-09-21
status: halted
---

# Phase 87 Plan 4: Full-Width Oracle Parity and Authorization Boundary Summary

**Both automated tasks land clean on the first test run -- 11 paired-parity facts across all 8 lookup-kind families (plus a SuperAdmin, a no-half-switch fact, and a bare-404 fact) and 4 authorization-boundary facts pinning the middleware's pre-authorization pipeline position in both directions -- with the plan's final task, a blocking human-verification checkpoint, deliberately left for the orchestrator to present.**

## Performance

- **Duration:** ~45 min
- **Started:** 2026-09-21 (worktree spawn)
- **Completed:** 2026-09-21 (Tasks 1-2; Task 3 pending)
- **Tasks:** 2 of 3 (Task 3 is a blocking human-verify checkpoint, not yet run)
- **Files modified:** 2 (2 created, 0 modified)

## Accomplishments

- `CrossBoardOracleParityTests.cs` -- 11 facts proving the non-member response and the nonexistent-id response are indistinguishable in status code, response body, and every header not itself a function of wall-clock time or per-request tracing plumbing, across all 8 lookup-kind families (Quest, Event, Character, Contact, ShopItem, EventSeries, ContactCategory, BoardMember), for a SuperAdmin on the Quest family, and two structural facts: a failed resolution never leaves the board half-switched, and the unresolvable case is still the bare framework 404 (empty body, no rendered error page).
- `CrossBoardAuthorizationBoundaryTests.cs` -- 4 facts proving the landed page's role check is judged against the board the request was just switched to: a Player-on-active/DungeonMaster-on-target viewer reaches the target board's quest edit page (and the board stays switched on a following request), while a DungeonMaster-on-active/Player-on-target viewer is refused that same page yet still ends up switched to the target board -- proving the resolver never consults roles. Two further facts show a cancelled event and a Draft-status shop item both resolve and switch the board even though each page's own rendering rules, not the resolver, are what withhold normal service.
- Both test classes passed on the first full run against the real solution: `dotnet test QuestBoard.IntegrationTests --filter FullyQualifiedName~CrossBoardOracleParity` (11/11), `--filter FullyQualifiedName~CrossBoardAuthorizationBoundary` (4/4), the full `QuestBoard.UnitTests` suite (692/692), the full `QuestBoard.IntegrationTests` suite (932/932), and the solution-wide `dotnet test --filter "FullyQualifiedName~CrossBoard"` (82 unit + 63 integration, all green).
- Task 3 -- the blocking human-verification checkpoint covering a real wrong-board link in a real browser, the signed-out emailed-link chain, and the banner on a real mobile user agent -- is deliberately not started. It requires a human, per this plan's `autonomous: false` frontmatter and the checkpoint's own `gate="blocking"` attribute.

## Task Commits

1. **Task 1: Prove the non-member response and the nonexistent response are the same response, family by family** - `2f15fb48` (test)
2. **Task 2: Prove the landed page is judged with the board it landed on** - `217bb864` (test)
3. **Task 3: Human verification -- a real link, a real browser, a real phone** - not started (blocking checkpoint, awaiting the orchestrator/human)

**Plan metadata:** commit pending (this SUMMARY is the orchestrator's responsibility to commit centrally in this worktree-parallel run, per this plan's execution instructions)

## Files Created/Modified

- `QuestBoard.IntegrationTests/Security/CrossBoardOracleParityTests.cs` - paired non-member/nonexistent-id parity theory across all 8 lookup-kind families, a SuperAdmin fact, a no-half-switch fact, and a bare-404 fact
- `QuestBoard.IntegrationTests/Security/CrossBoardAuthorizationBoundaryTests.cs` - both directions of the pre-authorization pipeline-position contract, plus the two resolves-but-page-refuses facts (cancelled event, Draft shop item)

## Decisions Made

- Per-family (not per-route) parity coverage, per the plan's own recorded scoping choice: eighteen routes share eight lookup kinds and the same three-line resolver code path per kind, so a pair per route would exercise the same lines eighteen times over.
- The parity seed's viewer is Admin on every board they belong to, so no authorization policy in any of the eight family routes can itself distinguish the two requests under test -- isolating the fact entirely to `CrossBoardLinkRepository`'s own behavior.
- The authorization-boundary "must now succeed" fact makes the viewer the actual owning Dungeon Master of the target quest, not merely DM-tier on the target board, because `QuestController.Edit` layers an independent ownership check on top of the `DungeonMasterOnly` policy; conflating the two would have made the fact fail for the wrong reason.
- The two "resolves but the page will not serve it" facts read the board-switch banner and the entity's own visibility marker off the same HTTP response the resolver produced, rather than issuing a second request, matching the style already established in the tracer suite.

## Deviations from Plan

None - plan executed exactly as written through Task 2. Both test files matched the plan's `<action>` specification on the first implementation, and every test passed on the first run with no seeding fixes required.

## Issues Encountered

None. `dotnet build` succeeded with 0 new warnings (only the two pre-existing `NU1608` package-constraint warnings). No CLR flake was hit on this run; both `QuestBoard.UnitTests` (692/692) and `QuestBoard.IntegrationTests` (932/932) ran clean as full suites, and the solution-wide `dotnet test --filter "FullyQualifiedName~CrossBoard"` invocation also completed cleanly (82 unit + 63 integration, all passed) with no contention from a parallel worktree this time.

## Next Phase Readiness

- Tasks 1 and 2 are complete, committed, and independently verified against the real solution. The phase's two governing properties -- no existence/membership oracle, and no authorization decision against the wrong board -- are now proven at the full 18-route, 8-family width the phase widened to.
- Task 3 is a blocking `checkpoint:human-verify` (`gate="blocking"`) and has not been run. Per this plan's execution instructions, the orchestrator must present this checkpoint to a human rather than the executor self-approving it. The three things that need a human eye, per the plan: (1) the signed-out emailed-link chain's picker-vs-direct-landing hop, which the integration harness structurally cannot follow because it authenticates through a test scheme rather than the real login cookie; (2) the switch banner on a real mobile user agent; (3) whether the automatic switch actually removes the friction the phase exists to fix.
- No blockers for a human to pick up Task 3. The application builds and all test suites pass; nothing in Tasks 1-2 touched `GroupSessionMiddleware`, `QuestBoardContext`, or any file outside the two new test classes listed above (`git status --porcelain` confirms no other production or test files were modified in this worktree).

## Self-Check: PASSED

Both created files confirmed present on disk; both commits (`2f15fb48`, `217bb864`) confirmed in `git log`.

---
*Phase: 87-cross-board-deep-link-recovery*
*Plan: 04*
*Completed: 2026-09-21 (Tasks 1-2; Task 3 awaiting human verification)*
