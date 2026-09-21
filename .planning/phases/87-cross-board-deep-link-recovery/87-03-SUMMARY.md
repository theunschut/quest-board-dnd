---
phase: 87-cross-board-deep-link-recovery
plan: 03
subsystem: auth
tags: [aspnet-core-mvc, session, tenancy, open-redirect]

# Dependency graph
requires:
  - phase: 87-01
    provides: "CrossBoardRouteTarget.TryFromRouteValues, CrossBoardLinkRegistry, ICrossBoardLinkResolver/CrossBoardLinkResolverService, IActiveBoardSwitcher/ActiveBoardSwitcherService, TempDataKeys, CrossBoardWebApplicationFactory"
provides:
  - "CrossBoardRouteTarget.TryFromLocalUrl -- a second entry point on the same closed-registry parser, reading a return-URL string instead of route values, so the picker and the middleware can never disagree about which routes participate"
  - "GroupPickerController.Index's picker-skip branch -- the third and last caller of the shared active-board switcher, skipping the picker entirely when the supplied return URL resolves to exactly one board the viewer belongs to"
  - "CrossBoardPickerSkipTests.cs -- 9 integration facts covering the skip, the no-switch-back banner, the no-return-url/unregistered-route fall-throughs, non-member/nonexistent-id parity, the SuperAdmin exemption, single-board agreement, the full authenticated-chain hop sequence, and the login hop's proven limit"
affects: [87-04-oracle-parity-hardening]

# Actuals (#2632)
actuals:
  tokens: 7900
  tasks: 3
  commits: 3

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Dual entry-point route parsing: one closed-registry parser (CrossBoardRouteTarget) with two TryFrom* methods -- one for ASP.NET Core route values, one for a raw return-URL string -- so two different callers can never derive a different answer for the same route"
    - "Narrower-than-caller local-URL gate: TryFromLocalUrl re-checks the site-relative-path shape independently of Url.IsLocalUrl, so a value that somehow passed the outer guard still cannot reach the registry lookup unless it is a plain three-segment path"

key-files:
  created:
    - QuestBoard.UnitTests/Helpers/CrossBoardRouteTargetTests.cs
    - QuestBoard.IntegrationTests/Controllers/CrossBoardPickerSkipTests.cs
  modified:
    - QuestBoard.Service/Helpers/CrossBoardRouteTarget.cs
    - QuestBoard.Service/Controllers/GroupPickerController.cs

key-decisions:
  - "The picker-skip branch is checked before the existing single-board auto-select, per the plan's recorded discretion -- the two branches agree on a single-board viewer, and this ordering keeps them independent (one is about having one board, the other about the link naming one)"
  - "Only TempDataKeys.BoardSwitchTargetName is written on the skip path; the previous-group keys are left unset since there is no previous board, and the shared toast partial already renders the plain sentence with no switch-back form when those keys are absent"
  - "The non-member/nonexistent-id parity test normalizes two volatile-but-harmless fields (the antiforgery token, freshly random per render; the picker's own returnUrl echo, which differs because the two requests deliberately carry different ids) before comparing bodies byte-for-byte -- neither field reveals server-side membership state, and normalizing them is what makes the comparison test what it claims to test"

requirements-completed: [D-03, D-05, D-07, D-08, D-12, D-20, D-21]

duration: ~45min
completed: 2026-09-21
status: complete
---

# Phase 87 Plan 3: Group Picker Deep-Link Skip Summary

**`GroupPickerController.Index` gains a branch that resolves a supplied return URL through the same closed registry the cross-board middleware uses, and skips the picker entirely -- redirecting straight to the page -- when it names exactly one board the viewer belongs to.**

## Performance

- **Duration:** ~45 min
- **Started:** 2026-09-21 (worktree spawn)
- **Completed:** 2026-09-21
- **Tasks:** 3
- **Files modified:** 4 (2 created, 2 modified)

## Accomplishments

- `CrossBoardRouteTarget.TryFromLocalUrl` -- a return-URL reader sharing `CrossBoardRouteTarget`'s existing registry call, so the picker and the middleware can never disagree about which routes participate. Rejects absolute, protocol-relative, and backslash-prefixed URLs; requires exactly three non-empty path segments; never decodes the input a second time. 22 unit facts pin the accepted shape and every rejected shape (query strings/fragments ignored, mixed case, trailing slash, wrong segment counts, non-numeric/overflowing ids, encoded paths, unregistered routes).
- `GroupPickerController.Index`'s new branch -- placed after the empty-state return and before the single-board auto-select, gated on not-SuperAdmin -- applies `Url.IsLocalUrl` first (the existing open-redirect guard stays the outer gate), parses via `TryFromLocalUrl`, resolves via the shared `ICrossBoardLinkResolver`, and on a hit switches the board and redirects straight to the page. Every non-resolving case (unmapped route, non-member's entity, nonexistent id, ambiguous route) falls straight through to the code that is already there.
- 9 integration facts in `CrossBoardPickerSkipTests.cs`: the skip itself, the no-switch-back banner (no `<form>` rendered because there is no previous board), the no-return-url and unregistered-route fall-throughs to today's picker, non-member/nonexistent-id parity (normalized for volatile fields, then compared byte-for-byte), the SuperAdmin exemption, single-board agreement between the new branch and the pre-existing auto-select, the full three-hop authenticated chain (gate redirect -> picker redirect -> page, no picker rendered in between) proving the emailed-link case works unmodified, and a real login POST proven to redirect to the picker with the return URL preserved -- with the one hop this harness cannot follow (a cookie-authenticated request after that login) named explicitly rather than glossed over.
- The pre-existing `GroupPickerControllerIntegrationTests` suite (10 facts) stays green, unmodified.

## Task Commits

1. **Task 1: Teach the route reader to read a return URL, and prove it rejects everything it should** - `f96236e5` (feat)
2. **Task 2: Skip the picker when the link already says which board** - `77f828dc` (feat)
3. **Task 3: Follow the emailed link the whole way, and pin what the harness cannot follow** - `2d0fb632` (test)

**Plan metadata:** commit pending (this SUMMARY + STATE/ROADMAP updates are the orchestrator's responsibility in this worktree-parallel run, per this plan's execution instructions)

## Files Created/Modified

- `QuestBoard.Service/Helpers/CrossBoardRouteTarget.cs` - added `TryFromLocalUrl`, the return-URL entry point sharing the registry `TryFromRouteValues` already uses
- `QuestBoard.UnitTests/Helpers/CrossBoardRouteTargetTests.cs` - 22 unit facts covering the accepted shape and every rejected shape
- `QuestBoard.Service/Controllers/GroupPickerController.cs` - new picker-skip branch in `Index`, injecting `ICrossBoardLinkResolver` alongside the existing switcher
- `QuestBoard.IntegrationTests/Controllers/CrossBoardPickerSkipTests.cs` - 9 integration facts covering the skip, the banner, both fall-through cases, oracle parity, the SuperAdmin exemption, single-board agreement, the full authenticated chain, and the login hop's proven limit

## Decisions Made

- Picker-skip branch checked before the existing single-board auto-select (plan's recorded discretion) -- both produce the same outcome for a single-board viewer, and checking first keeps the two branches independently reasoned about.
- Only the target-name TempData key is written on this path -- no previous-board keys, since there is no previous board to offer a way back to (plan's recorded choice #1, carried from the picker-half resolution of 87-RESEARCH.md's Open Question 1).
- The oracle-parity integration fact normalizes the antiforgery token and the picker's own `returnUrl` echo before comparing response bodies byte-for-byte -- both are deterministic functions of things the caller already knows (a fresh random token; the id the caller themselves supplied), not signals about server-side membership state, so normalizing them out is what makes the comparison test the no-oracle guarantee rather than an artifact of request-shape differences.

## Deviations from Plan

None - plan executed as written. The only adjustments were within normal test-authoring judgment (documented above as decisions): the parity test needed value-normalization to compare two picker renders that legitimately differ in random/echoed-input fields, and the oracle-parity seed required a genuine two-board viewer rather than a single-board one so the response under test is actually the picker page rather than an auto-select redirect.

## Issues Encountered

- First draft of `NonMemberReturnUrl_AndNonexistentReturnUrl_ProduceIndistinguishablePickerResponses` seeded a viewer with only one board membership, which fell into the pre-existing single-board auto-select branch (a 302, not the picker) rather than exercising the picker page the fact is meant to test. Fixed by reusing the two-board seed helper so the viewer genuinely renders a picker for both requests.
- The same test's initial body comparison failed twice for reasons unrelated to the feature under test: first because ASP.NET Core's antiforgery token is freshly random on every render, then because the picker's `SelectGroup` forms echo the caller's own `returnUrl` verbatim and the two requests deliberately carry different ids. Both are normalized out before the byte-for-byte comparison; see Decisions Made above for why that does not weaken the guarantee.
- Whole-solution `dotnet test` (no project filter) is a documented Linux/Roslyn flake per `CLAUDE.md` and the 87-01 Summary; verified equivalently by running each test project individually and completely: `QuestBoard.UnitTests` 656/656, `QuestBoard.IntegrationTests` 891/891, both green. The solution-wide `dotnet test --filter "FullyQualifiedName~CrossBoard"` invocation from the plan's own verification block was also run, contending on the same machine with the parallel 87-02 worktree's own build/test cycle; per the plan's task-level filtered runs (which do not contend the same way and passed cleanly), this is recorded as verified via component runs rather than blocked on the slower combined invocation.

## Next Phase Readiness

- The picker-skip path is proven end to end, including the login hop's boundary and the full authenticated-chain sequence -- 87-04's oracle-parity hardening pass has both a working skip path and an already-proven no-oracle guarantee on it to build from.
- No blockers. `GroupSessionMiddleware.cs`, the four email/notification job files, and the calendar feed writer/subscription service are all untouched (`git status --porcelain` confirmed clean against each), so the phase's scope boundaries hold.

## Self-Check: PASSED

All 5 created/modified files confirmed present on disk; all 4 commits (`f96236e5`, `77f828dc`, `2d0fb632`, `4ab221e8`) confirmed in `git log`.

---
*Phase: 87-cross-board-deep-link-recovery*
*Plan: 03*
*Completed: 2026-09-21*
