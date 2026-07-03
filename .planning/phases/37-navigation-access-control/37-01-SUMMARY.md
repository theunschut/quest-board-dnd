---
phase: 37-navigation-access-control
plan: 01
subsystem: auth
tags: [group-context, nav-visibility, board-type, integration-tests, tdd]

# Dependency graph
requires:
  - phase: 35-board-type-configuration
    provides: "BoardType enum on GroupEntity/Group domain model"
  - phase: 36-campaign-quest-posting-closing
    provides: "Per-action ViewBag.BoardType threading precedent (QuestController.GetActiveBoardTypeAsync)"
provides:
  - "IActiveGroupContext.GetBoardTypeAsync(CancellationToken) — async, nullable BoardType accessor"
  - "ActiveGroupContextService implementation backed by IGroupService.GetByIdAsync, null-preserving (no OneShot fallback)"
  - "MutableGroupContext.BoardType settable test double property (default OneShot)"
  - "LayoutNavigationTests RED scaffold covering NAV-01..06, OneShot regression, and D-04 anonymous Calendar link"
affects: [37-02-nav-layout-gating, 37-03-access-denied-and-emailstats-lockdown]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "IActiveGroupContext extended with an async member; both concrete implementations (ActiveGroupContextService, MutableGroupContext) updated in lockstep since the interface has no default implementation"
    - "Null-preserving group-context lookups: GetBoardTypeAsync returns null for 'no active group', distinct from the per-action OneShot-defaulting precedent in QuestController"

key-files:
  created:
    - QuestBoard.IntegrationTests/Controllers/LayoutNavigationTests.cs
  modified:
    - QuestBoard.Domain/Interfaces/IActiveGroupContext.cs
    - QuestBoard.Service/Services/ActiveGroupContextService.cs
    - QuestBoard.IntegrationTests/Helpers/MutableGroupContext.cs
    - QuestBoard.UnitTests/Services/SessionReminderJobTests.cs

key-decisions:
  - "GetBoardTypeAsync returns null (not BoardType.OneShot) when no active group — deliberate divergence from QuestController.GetActiveBoardTypeAsync so the nav allowlist's '== BoardType.OneShot' check naturally hides gated items for every indeterminate state (D-03)"
  - "MutableGroupContext.BoardType defaults to OneShot so every pre-existing nav-visible integration test stays green without modification"
  - "Nav assertions in LayoutNavigationTests key on stable icon CSS classes (fa-store, fa-users me-) or link text (Calendar, Manage Shop, Edit My Profile, Guild Members) rather than generated hrefs, since asp-controller/asp-action tag helpers don't render literal href attributes in a form the test can predict without duplicating routing logic"

patterns-established:
  - "Dual-implementation interface extension: when adding a member to IActiveGroupContext, both ActiveGroupContextService (production) and MutableGroupContext (test double) must be updated together or the solution fails to compile"

requirements-completed: [NAV-01, NAV-02, NAV-03, NAV-04, NAV-05, NAV-06]

# Metrics
duration: 25min
completed: 2026-07-03
---

# Phase 37 Plan 01: Group-Context BoardType Seam Summary

**Extended `IActiveGroupContext` with an async, null-preserving `GetBoardTypeAsync` accessor backed by `IGroupService`, gave the integration-test double a settable `BoardType`, and stood up a 16-case RED `LayoutNavigationTests` scaffold (12 failing, 4 passing regression guards) that Plan 02's layout gating will turn green.**

## Performance

- **Duration:** ~25 min
- **Completed:** 2026-07-03T17:38:34Z
- **Tasks:** 3
- **Files modified:** 4 (1 created, 3 modified — includes 1 unplanned fix)

## Accomplishments
- `IActiveGroupContext` now exposes `Task<BoardType?> GetBoardTypeAsync(CancellationToken)`, implemented in `ActiveGroupContextService` via a single `IGroupService.GetByIdAsync` lookup that returns `null` (never `OneShot`) when no group is active — the seam any server-rendered view (starting with `_Layout.cshtml`/`_Layout.Mobile.cshtml` in Plan 02) can use regardless of which controller rendered the page.
- `MutableGroupContext` gained a settable `BoardType` property (default `OneShot`) so integration tests can flip board type per-test via `factory.TestGroupContext.BoardType = BoardType.Campaign;`, mirroring the existing `ActiveGroupId` mutation pattern.
- `LayoutNavigationTests` stood up as a 16-case (8 `[Theory]` × 2 user agents) RED scaffold: 12 cases correctly fail today (nav gating not yet implemented) and 4 pass today as regression guards (Guild Members stays visible under Campaign; the full OneShot nav is unaffected) — proving the scaffold isn't accidentally already green and that current behavior is untouched by this plan.

## Task Commits

Each task was committed atomically:

1. **Task 1: Extend IActiveGroupContext + ActiveGroupContextService with GetBoardTypeAsync** - `8119b75` (feat)
2. **Task 2: Add settable BoardType to MutableGroupContext test double** - `58d0a46` (feat)
3. **Task 3: Create failing LayoutNavigationTests scaffold (RED) for NAV-01..06 + D-04** - `167e430` (test)

**Plan metadata:** committed together with this SUMMARY.md (see final commit below).

## Files Created/Modified
- `QuestBoard.Domain/Interfaces/IActiveGroupContext.cs` - added `Task<BoardType?> GetBoardTypeAsync(CancellationToken)` member
- `QuestBoard.Service/Services/ActiveGroupContextService.cs` - added `IGroupService` constructor dependency and `GetBoardTypeAsync` implementation (null-preserving)
- `QuestBoard.IntegrationTests/Helpers/MutableGroupContext.cs` - added settable `BoardType` property (default OneShot) and `GetBoardTypeAsync` wrapper
- `QuestBoard.IntegrationTests/Controllers/LayoutNavigationTests.cs` - new RED test scaffold for NAV-01..06, OneShot regression, D-04 anonymous
- `QuestBoard.UnitTests/Services/SessionReminderJobTests.cs` - updated direct `ActiveGroupContextService` construction to pass a substituted `IGroupService` (Rule 3 fix, see Deviations)

## Decisions Made
- `GetBoardTypeAsync` returns `null` rather than defaulting to `OneShot` for "no active group" — per D-03, this lets the nav's `== BoardType.OneShot` allowlist check naturally hide gated items in every indeterminate state (anonymous, no group selected, unknown group) without a separate null-handling branch. This is the deliberate divergence from `QuestController.GetActiveBoardTypeAsync`'s existing OneShot-defaulting precedent, called out explicitly in the plan.
- Nav-visibility assertions in `LayoutNavigationTests` key on stable icon CSS classes (`fa-store`, `fa-users me-`) or unambiguous link text (`Calendar`, `Manage Shop`, `Edit My Profile`, `Guild Members`) rather than literal `href="/Shop"`-style strings — the layouts use `asp-controller`/`asp-action` tag helpers, which don't guarantee a predictable literal href substring without duplicating ASP.NET Core's routing/tag-helper logic in the test.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Fixed QuestBoard.UnitTests build break from ActiveGroupContextService's new constructor dependency**
- **Found during:** Task 3 (running the full test suite after the RED scaffold to check for regressions)
- **Issue:** `SessionReminderJobTests.cs` directly constructs `new ActiveGroupContextService(httpContextAccessor)` to build a mock `IServiceScopeFactory` chain for `SessionReminderJob`. Task 1 added a required second constructor parameter (`IGroupService groupService`), which broke this call site — `QuestBoard.UnitTests` failed to compile (`CS7036: no argument given for required parameter 'groupService'`).
- **Fix:** Added `var groupService = Substitute.For<IGroupService>();` and passed it into the constructor call, matching the existing NSubstitute mocking style already used throughout the test file.
- **Files modified:** `QuestBoard.UnitTests/Services/SessionReminderJobTests.cs`
- **Verification:** `dotnet build QuestBoard.UnitTests` exits 0; `dotnet test QuestBoard.UnitTests` — 123/123 passed.
- **Committed in:** `167e430` (bundled with Task 3's commit since it was discovered while verifying Task 3's test run)

---

**Total deviations:** 1 auto-fixed (1 blocking)
**Impact on plan:** Necessary to keep the full solution compiling after Task 1's interface/constructor change; no scope creep — the fix touches only the one call site broken by this plan's own change, not unrelated code.

## Issues Encountered

- Initial `LayoutNavigationTests` draft asserted on literal `href="/Shop"`/`>Players<` substrings, which don't appear in the rendered HTML (the layouts use Razor tag helpers with icon markup before the link text, e.g. `<i class="fas fa-store me-1"></i>Shop`, never `>Shop<`). Corrected the assertions to key on the icon's CSS class (`fa-store`, `fa-users me-`) before re-running — this was fixed during Task 3 itself (before commit), not tracked as a separate deviation since it's normal test-writing iteration within the same task, not a discovered pre-existing bug.
- This worktree's branch (`worktree-agent-a6cdf613c55e9d593`) had been created from a `main` commit that predated Phase 35-37's planning docs and code (Phases 35/36 and the Phase 37 plans were entirely absent). Confirmed via `git log` that the worktree branch was a strict ancestor of `milestone/v6-board-types` with zero unique commits, then fast-forward merged (`git merge milestone/v6-board-types --ff-only`) to bring in the missing history before any plan work began. No conflicts, no destructive operations — a clean fast-forward.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- Plan 02 can now wire `GetBoardTypeAsync` into `_Layout.cshtml`/`_Layout.Mobile.cshtml`'s `@if` gating for Calendar, Shop, Manage Shop, Edit My Profile, and Players, using the allowlist pattern (`== BoardType.OneShot`) per D-01/D-03.
- `LayoutNavigationTests` is ready to serve as Plan 02's turn-green target — 12 of its 16 cases should flip from FAIL to PASS once the gating lands; the 4 currently-passing cases (NAV-03 Guild Members regression guard, OneShot-full-nav regression) must remain green throughout Plan 02 as a no-regression check.
- No blockers. `dotnet build` (whole solution) and `dotnet test QuestBoard.UnitTests` are both clean; `dotnet test QuestBoard.IntegrationTests` shows exactly the 12 expected RED failures with no unrelated regressions (245/257 passing, all 12 failures in `LayoutNavigationTests`).

---
*Phase: 37-navigation-access-control*
*Completed: 2026-07-03*
