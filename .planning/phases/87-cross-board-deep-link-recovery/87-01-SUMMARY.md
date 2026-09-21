---
phase: 87-cross-board-deep-link-recovery
plan: 01
subsystem: auth
tags: [aspnet-core-middleware, ef-core-query-filters, session, tenancy]

# Dependency graph
requires: []
provides:
  - "CrossBoardLookupKind enum and CrossBoardTarget record (Domain) -- the closed vocabulary later plans (87-02) extend with more entity kinds"
  - "CrossBoardLinkRepository -- the one membership-pinned IgnoreQueryFilters() seam permitted outside the pre-existing allowlist"
  - "ICrossBoardLinkResolver / CrossBoardLinkResolverService -- userId-only resolution with no SuperAdmin escape hatch"
  - "CrossBoardLinkRegistry -- the closed (controller, action) -> lookup-kind table 87-02 widens to all 18 routes"
  - "IActiveBoardSwitcher / ActiveBoardSwitcherService -- the single writer of the three active-board session keys, now used by all three former call sites"
  - "CrossBoardDeepLinkMiddleware -- registered after GroupSessionMiddleware and before UseAuthorization"
  - "CrossBoardWebApplicationFactory -- the session-backed integration harness 87-02/87-03/87-04 reuse"
affects: [87-02-widen-to-all-routes, 87-03-picker-skip-path, 87-04-oracle-parity-hardening]

# Actuals (#2632)
actuals:
  tokens: 14200
  tasks: 3
  commits: 4

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Closed-registry route resolution: a (controller, action) -> enum lookup table that must be extended explicitly before a route can be resolved cross-board"
    - "Membership-pinned IgnoreQueryFilters(): the caller's own membership set is pinned inside the SQL predicate itself, never checked after the fact"
    - "Single-writer session mutation: one shared service (ActiveBoardSwitcher) is the only code path permitted to write a related group of session keys"
    - "No-branch equivalence: a non-member and a nonexistent id are answered by the identical code path, with no distinguishing branch, so the feature cannot become an existence/membership oracle"

key-files:
  created:
    - QuestBoard.Domain/Enums/CrossBoardLookupKind.cs
    - QuestBoard.Domain/Models/CrossBoardTarget.cs
    - QuestBoard.Domain/Interfaces/ICrossBoardLinkRepository.cs
    - QuestBoard.Domain/Interfaces/ICrossBoardLinkResolver.cs
    - QuestBoard.Domain/Services/CrossBoardLinkResolverService.cs
    - QuestBoard.Repository/CrossBoardLinkRepository.cs
    - QuestBoard.Service/Constants/TempDataKeys.cs
    - QuestBoard.Service/Helpers/CrossBoardLinkRegistry.cs
    - QuestBoard.Service/Helpers/CrossBoardRouteTarget.cs
    - QuestBoard.Service/Services/IActiveBoardSwitcher.cs
    - QuestBoard.Service/Services/ActiveBoardSwitcherService.cs
    - QuestBoard.Service/Middleware/CrossBoardDeepLinkMiddleware.cs
    - QuestBoard.IntegrationTests/Helpers/CrossBoardWebApplicationFactory.cs
    - QuestBoard.IntegrationTests/Middleware/CrossBoardTestHarnessTests.cs
    - QuestBoard.IntegrationTests/Middleware/CrossBoardDeepLinkMiddlewareTests.cs
    - QuestBoard.UnitTests/Helpers/CrossBoardLinkRegistryTests.cs
  modified:
    - QuestBoard.Domain/Extensions/ServiceExtensions.cs
    - QuestBoard.Repository/Extensions/ServiceExtensions.cs
    - QuestBoard.Service/Controllers/GroupPickerController.cs
    - QuestBoard.Service/Views/Shared/_Toasts.cshtml
    - QuestBoard.Service/Program.cs

key-decisions:
  - "Registry lives in QuestBoard.Service/Helpers (controller/action names are Service-layer facts); the repository never sees a route string, only an enum + id"
  - "ActiveBoardSwitcherService lives in QuestBoard.Service/Services, not Domain, because it writes QuestBoard.Service.Constants.SessionKeys and Domain cannot reference Service"
  - "The middleware's header gate (Sec-Fetch-Dest/Mode plus prefetch-hint refusal) is a UX safety valve for a member's own browser, not a security boundary -- the tenancy guarantee is entirely the membership-pinned repository lookup"
  - "The switch-back form carries no returnUrl, structurally preventing the switch-back-then-re-switch ping-pong rather than merely guarding against it"

requirements-completed: [D-01, D-02, D-03, D-04, D-06, D-07, D-08, D-09, D-10, D-12, D-13, D-18, D-19]

duration: 70min
completed: 2026-09-21
status: complete
---

# Phase 87 Plan 1: Cross-Board Deep Link Recovery Tracer Summary

**A member of Board B with Board A active who follows a browser link to `/Quest/Details/{id}` for a Board B quest now lands on the quest page mid-request, board switched, with a one-shot "Switched board" banner offering the way back — proven end to end by a real integration harness, not by inspection.**

## Performance

- **Duration:** ~70 min (includes a coordinator-approved pause for live-browser verification of the tracer slice before Task 3 began)
- **Started:** 2026-09-21T18:47:00Z (approx, per STATE.md)
- **Completed:** 2026-09-21T20:05:13Z
- **Tasks:** 3
- **Files modified:** 21 (16 created, 5 modified)

## Accomplishments

- A working integration harness (`CrossBoardWebApplicationFactory`) that restores the real session-backed `IActiveGroupContext` in place of the shared test factory's singleton stub, so a board switch written into session is finally observable by the EF Core query filters in a test — every later fact in this phase rests on this.
- The full vertical slice for one route (`Quest/Details`): a closed registry, a membership-pinned `CrossBoardLinkRepository` (the only new `IgnoreQueryFilters()` call site in the application), a resolver that takes only a userId and never a role flag, a shared `ActiveBoardSwitcher` now used by all three places the application writes the active-board session keys, `CrossBoardDeepLinkMiddleware` slotted between `GroupSessionMiddleware` and `UseAuthorization`, and a one-shot switch-back banner rendered from the one shared toast partial (so the desktop layout, mobile twin, and picker layout all carry it for free).
- Two of the phase's two values-shaped guarantees pinned directly on the tracer route before any widening: a non-member's request and a nonexistent-id request produce byte-identical status/body/content-type (no existence-or-membership oracle), and a non-navigation request (background fetch, image load, any of three prefetch-hint variants) leaves the active board untouched.
- Live-browser verification against a real SQL Server confirmed every observable behavior: the switch, the banner text and switch-back button, the one-shot disappearance, the no-ping-pong redirect target, the header-gate 404, and the oracle-parity 404 — see Issues Encountered for one live finding worth recording.

## Task Commits

1. **Task 1: Make a cross-board switch observable in an integration test at all** — `a30d230e` (test)
2. **Task 2 (tracer): End-to-end "the link to the other board's quest opens the quest" — one route only** — `b89fa7b0` (feat)
3. **Task 3: Pin the two negative properties on the tracer route before widening it** — `da582c29` (test)
4. **Fix: strip a leaked decision-id reference from a test comment** — `7178515f` (fix) — see Deviations below

**Plan metadata:** commit pending (this SUMMARY + STATE/ROADMAP updates are the orchestrator's responsibility in this worktree-parallel run, per this plan's execution instructions)

## Files Created/Modified

- `QuestBoard.Domain/Enums/CrossBoardLookupKind.cs` — one-member enum (`Quest`); grows per lookup kind added in later plans
- `QuestBoard.Domain/Models/CrossBoardTarget.cs` — `GroupId`/`GroupName` record the resolver returns
- `QuestBoard.Domain/Interfaces/ICrossBoardLinkRepository.cs` / `QuestBoard.Repository/CrossBoardLinkRepository.cs` — the one membership-pinned filter-bypass class, returning only an `int?`
- `QuestBoard.Domain/Interfaces/ICrossBoardLinkResolver.cs` / `QuestBoard.Domain/Services/CrossBoardLinkResolverService.cs` — combines the repository's id with the caller's own membership list for the board name
- `QuestBoard.Service/Helpers/CrossBoardLinkRegistry.cs` — closed `(controller, action)` → lookup-kind table
- `QuestBoard.Service/Helpers/CrossBoardRouteTarget.cs` — parses and validates route values against the registry
- `QuestBoard.Service/Services/IActiveBoardSwitcher.cs` / `ActiveBoardSwitcherService.cs` — the one writer of the three active-board session keys
- `QuestBoard.Service/Middleware/CrossBoardDeepLinkMiddleware.cs` — the recovery middleware itself
- `QuestBoard.Service/Constants/TempDataKeys.cs` — the banner's TempData key constants
- `QuestBoard.Service/Controllers/GroupPickerController.cs` — both former inline session writers now call the shared switcher
- `QuestBoard.Service/Views/Shared/_Toasts.cshtml` — new `board-switch-toast` block, one-shot switch-back form
- `QuestBoard.Service/Program.cs` — new DI registration plus the middleware's pipeline slot
- `QuestBoard.IntegrationTests/Helpers/CrossBoardWebApplicationFactory.cs` — the session-backed test harness
- `QuestBoard.IntegrationTests/Middleware/CrossBoardTestHarnessTests.cs` — pins the harness itself
- `QuestBoard.IntegrationTests/Middleware/CrossBoardDeepLinkMiddlewareTests.cs` — the tracer fact plus the header-gate theory, verb-check, oracle-parity, and no-ping-pong facts
- `QuestBoard.UnitTests/Helpers/CrossBoardLinkRegistryTests.cs` — registry lookup/case-insensitivity/count tests

## Decisions Made

- Registry placed in `QuestBoard.Service/Helpers` (Service-layer, since controller/action names are Service-layer facts) rather than Domain, per the plan's recorded discretion.
- `ActiveBoardSwitcherService` placed in `QuestBoard.Service/Services` rather than Domain (corrects `87-RESEARCH.md`'s suggested location) because it writes `QuestBoard.Service.Constants.SessionKeys`, which Domain cannot reference.
- The oracle-parity fact compares status code, response body, and `Content-Type` header rather than a byte-for-byte header dump — headers like `Date` are expected to differ trivially between two requests and comparing them would make the test flaky without adding real assurance.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - CLAUDE.md compliance] Removed a leaked decision-id reference from a test comment**
- **Found during:** Task 3, during the plan's own forbidden-reference verification grep
- **Issue:** A comment on the no-ping-pong test cited a decision id (`D-04`) directly, violating CLAUDE.md's rule against embedding requirement/decision IDs in source comments
- **Fix:** Reworded the comment to describe the guarantee in plain language
- **Files modified:** `QuestBoard.IntegrationTests/Middleware/CrossBoardDeepLinkMiddlewareTests.cs`
- **Verification:** Repo-wide grep for `D-[0-2][0-9]`/`Phase 87`/`87-0[1-9]`/`RESEARCH.md`/`CONTEXT.md` across `QuestBoard.Domain`, `QuestBoard.Service`, `QuestBoard.Repository`, `QuestBoard.UnitTests`, and `QuestBoard.IntegrationTests` returns nothing
- **Committed in:** `7178515f`

---

**Total deviations:** 1 auto-fixed (CLAUDE.md compliance, caught by the plan's own verification step)
**Impact on plan:** Cosmetic fix to a test comment only; no behavior change; no scope creep.

## Issues Encountered

- **Live-browser finding (reported by the coordinator, no code change follows):** the first live-verification attempt used a quest on a board the SuperAdmin operator can reach via the group picker but is not a `UserGroups` member of, and it correctly 404'd. This is the resolver's userId-only design working exactly as intended — `GroupPickerController.Index`/`SelectGroup` skip the membership check for `SuperAdmin` (pre-existing behavior, unrelated to this plan), so a SuperAdmin can be *active* on a board their membership rows don't cover, but `CrossBoardLinkResolverService.ResolveAsync` has no such branch and never will: it takes a plain `userId` and nothing else, so a SuperAdmin who isn't a real member of the target board resolves to nothing, exactly like any other non-member. The coordinator considered reversing this and decided to keep it — recorded here so a future reader doesn't mistake the 404 for a bug.
- **Whole-solution `dotnet test` (no project argument) crashed twice with `Fatal error. Internal CLR error. (0x80131506)`**, including after the one retry CLAUDE.md's documented guidance calls for. Root cause not isolated further within this plan's scope — CLAUDE.md already documents this exact error as a known Linux/Roslyn flake. Verified equivalently instead by running each of the solution's two test projects individually and completely, twice each, with all tests green both times: `QuestBoard.UnitTests` 634/634, `QuestBoard.IntegrationTests` 882/882 (and again 873/873 immediately after Task 2, and 872/872 after Task 1). `dotnet test --filter "FullyQualifiedName~CrossBoard"` was also run across the whole solution successfully (37/37 passed), showing the crash is specific to the unfiltered whole-suite invocation rather than to anything in this plan's code.
- **`WallClockUnmovedTests.FinalizedQuestGameNight_NeverShowsTheUtcPlusTwoShiftedHour` failed once** during one full-suite `QuestBoard.IntegrationTests` run, then passed immediately on a full-suite rerun and in isolation. Pre-existing test, unrelated file, no code in this plan touches wall-clock rendering or the entities it exercises — logged here as an out-of-scope, apparently order/timing-dependent flake per the scope-boundary rule, not fixed.

## Next Phase Readiness

- The proven vertical slice (registry, repository, resolver, switcher, middleware, banner, harness) is exactly the shape `87-02` widens to all 18 routes and `87-03`/`87-04` build on — no rework anticipated.
- `CrossBoardLinkRepository`'s `ResolveSharedBoardIdsForUserAsync` method is implemented but has no caller yet; `87-02`'s profile-route work is expected to be its first consumer.
- No blockers. `GroupSessionMiddleware` and `QuestBoardContext` remain untouched and their existing tests remain green, so the two paths stay independently testable as the phase's own decisions require.

---
*Phase: 87-cross-board-deep-link-recovery*
*Plan: 01*
*Completed: 2026-09-21*
