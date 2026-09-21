---
phase: 87-cross-board-deep-link-recovery
plan: 02
subsystem: auth
tags: [aspnet-core-mvc, ef-core-query-filters, architecture-test, tenancy]

# Dependency graph
requires:
  - phase: 87-01
    provides: "CrossBoardLookupKind enum, CrossBoardLinkRepository, ICrossBoardLinkResolver/CrossBoardLinkResolverService, CrossBoardLinkRegistry (1 route), CrossBoardDeepLinkMiddleware, CrossBoardWebApplicationFactory harness"
provides:
  - "CrossBoardIgnoreQueryFiltersSeamTests -- a closed 5-file allowlist architecture test confining IgnoreQueryFilters() to production files, mirroring AmbientClockSeamTests but inverted (confinement, not just non-regression)"
  - "CrossBoardLookupKind widened to 8 members (Quest, Event, Character, Contact, ShopItem, EventSeries, ContactCategory, BoardMember)"
  - "CrossBoardLinkRepository widened to 7 membership-pinned board-id projections, one per entity-backed kind"
  - "CrossBoardLinkResolverService's BoardMember branch -- the profile ambiguity rule (resolves only when the viewer and target share exactly one board)"
  - "CrossBoardLinkRegistry widened from 1 to 18 routes -- the phase's full read + edit/manage route list"
  - "CrossBoardRouteCoverageTests -- 26 real-HTTP facts proving all 18 routes, the 6 excluded image routes, and the shop modal variant"
affects: [87-03-picker-skip-path, 87-04-oracle-parity-hardening]

# Actuals (#2632)
actuals:
  tokens: 12300
  tasks: 3
  commits: 3

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Inverted allowlist architecture test: AmbientClockSeamTests asks 'do these files still avoid X'; CrossBoardIgnoreQueryFiltersSeamTests asks 'is X confined to only these files, scanning the whole tree' -- a stronger containment guarantee for an escape-hatch call rather than a banned one"
    - "Explicit throw-on-wrong-method switch arm: BoardMember's switch arm in ResolveBoardIdAsync throws InvalidOperationException rather than silently answering, because its answer shape (a set) differs from every other kind's (a single id) -- a caller reaching for the wrong method fails loudly instead of getting a plausible-looking wrong answer"
    - "nameof-derived registry keys scale linearly: adding entity-backed routes to an already-proven registry is one dictionary line each, no new mechanism"

key-files:
  created:
    - QuestBoard.UnitTests/Architecture/CrossBoardIgnoreQueryFiltersSeamTests.cs
    - QuestBoard.IntegrationTests/Middleware/CrossBoardRouteCoverageTests.cs
  modified:
    - QuestBoard.Domain/Enums/CrossBoardLookupKind.cs
    - QuestBoard.Domain/Interfaces/ICrossBoardLinkResolver.cs
    - QuestBoard.Domain/Services/CrossBoardLinkResolverService.cs
    - QuestBoard.Repository/CrossBoardLinkRepository.cs
    - QuestBoard.Service/Helpers/CrossBoardLinkRegistry.cs
    - QuestBoard.UnitTests/Helpers/CrossBoardLinkRegistryTests.cs

key-decisions:
  - "The allowlist architecture test scans QuestBoard.Repository/Domain/Service only (production source) -- QuestBoard.UnitTests and QuestBoard.IntegrationTests are excluded because seeding/asserting against another board's rows via IgnoreQueryFilters() is the ordinary correct way to write a test here, per the plan's recorded scoping decision"
  - "BoardMember's 'already the active board' case is left entirely to the existing middleware guard (which already no-ops when the resolved target equals the current active board) rather than duplicating that check inside the resolver -- the resolver only owns the ambiguity rule (exactly one shared board)"
  - "Test seeding for the 16-route coverage theory reuses TestDataHelper's groupId-aware helpers (Quest, Character, Contact, ContactCategory) and seeds Event/EventSeries/ShopItem directly through the context, following AgendaTenantIsolationTests' established pattern for entities with no groupId-aware helper"

requirements-completed: [D-09, D-10, D-11, D-12, D-16, D-17, D-18]

duration: 35min
completed: 2026-09-21
status: complete
---

# Phase 87 Plan 2: Widen to All Routes Summary

**Widened the cross-board deep-link resolver from one proven route to all 18 board-scoped GET routes named by the phase, behind a new 5-file allowlist architecture test that fails the build if `IgnoreQueryFilters()` appears anywhere else in production code -- landed before any of the seven new projections it protects.**

## Performance

- **Duration:** ~35 min
- **Started:** 2026-09-21T20:00:00Z (approx)
- **Completed:** 2026-09-21T20:33:47Z
- **Tasks:** 3
- **Files modified:** 8 (2 created, 6 modified)

## Accomplishments

- `CrossBoardIgnoreQueryFiltersSeamTests` -- a closed, 5-file allowlist (the 4 pre-existing `IgnoreQueryFilters()` call sites plus `CrossBoardLinkRepository`) that scans every production `.cs` file under `QuestBoard.Repository`, `QuestBoard.Domain` and `QuestBoard.Service` with comments stripped, and fails naming any file outside the allowlist. Verified behaviorally: a temporary out-of-allowlist call site fails the confinement fact by name; a comment merely mentioning the method name does not.
- `CrossBoardLookupKind` widened to 8 members and `CrossBoardLinkRepository` widened to 7 membership-pinned, scalar (`int?` GroupId only) board-id projections -- one per entity-backed kind (Event, Character, Contact, ShopItem, EventSeries, ContactCategory), each shaped identically to the quest projection from 87-01. `BoardMember` gets an explicit switch arm that throws rather than silently answering, since its answer shape (a set of shared boards) differs from every other kind's (a single id).
- `CrossBoardLinkResolverService` branches on `BoardMember` to apply the profile ambiguity rule: resolves only when the viewer and target share exactly one board. The "already active" case is left to the existing middleware no-op guard rather than duplicated here.
- `CrossBoardLinkRegistry` widened from 1 to 18 `nameof`-derived entries covering the full read (8) and edit/manage (10) route list. The six image subresources and the shop modal variant are deliberately absent -- excluded for free by the navigation gate rather than by a second list.
- `CrossBoardRouteCoverageTests` -- 26 real-HTTP-round-trip facts against the `CrossBoardWebApplicationFactory` harness: all 16 entity-backed routes resolve across boards (read routes assert 200 + body content; edit/manage routes assert not-404), the 6 image routes and the shop modal variant provably leave the active board unchanged, and the profile ambiguity rule holds across the unambiguous-share, two-shared-boards, and no-id cases.

## Task Commits

1. **Task 1: Close the filter-bypass seam with an allowlist architecture test, before widening anything** - `2e0ae5b7` (test)
2. **Task 2: Add the remaining id-to-board projections and the profile ambiguity rule** - `df72c660` (feat)
3. **Task 3: Open the registry to all 18 routes and prove each one end to end** - `a87db17f` (test)

**Plan metadata:** commit pending (this SUMMARY is the orchestrator's responsibility to commit centrally in this worktree-parallel run, per this plan's execution instructions)

## Files Created/Modified

- `QuestBoard.UnitTests/Architecture/CrossBoardIgnoreQueryFiltersSeamTests.cs` -- the 5-file allowlist confinement test, mirroring `AmbientClockSeamTests`' structure but inverted in direction
- `QuestBoard.Domain/Enums/CrossBoardLookupKind.cs` -- widened from 1 to 8 members
- `QuestBoard.Domain/Interfaces/ICrossBoardLinkResolver.cs` -- doc comment clarifying the resolver answers only "which board", never visibility/authorization
- `QuestBoard.Domain/Services/CrossBoardLinkResolverService.cs` -- `BoardMember` branch and the ambiguity rule
- `QuestBoard.Repository/CrossBoardLinkRepository.cs` -- 6 new scalar projections plus the `BoardMember` throw-on-wrong-method arm
- `QuestBoard.Service/Helpers/CrossBoardLinkRegistry.cs` -- widened from 1 to 18 entries, with the image-route exclusion documented as deliberate
- `QuestBoard.UnitTests/Helpers/CrossBoardLinkRegistryTests.cs` -- extended to assert the full 18-entry table, each route's kind, image-route non-resolution, and ordinary non-participating routes
- `QuestBoard.IntegrationTests/Middleware/CrossBoardRouteCoverageTests.cs` -- 26 end-to-end facts covering the full route set

## Decisions Made

- The allowlist architecture test scans production source only (`QuestBoard.Repository`, `QuestBoard.Domain`, `QuestBoard.Service`); the two test projects are excluded per the plan's own recorded scoping decision, since seeding/asserting against another board's rows via `IgnoreQueryFilters()` is the ordinary correct pattern in tests here.
- `BoardMember`'s "target already in the active board" case is left entirely to the existing middleware no-op guard (which already treats a same-board resolution as nothing-to-do) rather than re-implementing that check inside the resolver -- keeps the resolver's only new responsibility the ambiguity rule itself.
- Test seeding for `Event`, `EventSeries` and `ShopItem` (which have no `groupId`-aware `TestDataHelper` method) goes directly through `factory.Database.CreateContext()`, following the established `AgendaTenantIsolationTests` pattern rather than adding new helper overloads.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Seeded contact needed `isRevealed: true` for `Contacts/Details` to resolve**
- **Found during:** Task 3, first `CrossBoardRouteCoverageTests` run
- **Issue:** `ContactsController.Details`'s own `IsVisibleTo` gate hides an unrevealed contact from a viewer who did not create it, independent of the cross-board resolution being proven -- the seeded contact returned 404 even though the board had correctly switched.
- **Fix:** Set `isRevealed: true` on the seeded contact in `CrossBoardRouteCoverageTests`.
- **Files modified:** `QuestBoard.IntegrationTests/Middleware/CrossBoardRouteCoverageTests.cs`
- **Verification:** `Contacts/Details` theory row passes; full `QuestBoard.IntegrationTests` suite green.
- **Committed in:** `a87db17f` (Task 3 commit)

**2. [Rule 1 - Bug] Seeded EventSeries needed a valid `CycleMask`**
- **Found during:** Task 3, first `CrossBoardRouteCoverageTests` run
- **Issue:** `EventSeriesEntity.CycleMask` defaults to an empty string; `SeriesController.Details` calls `EventSeriesDateGenerator.ParseMask`, which throws `ArgumentException` ("A cycle must have at least one position") on an empty mask -- unrelated to the cross-board resolution being proven, but blocking the test's own render assertion.
- **Fix:** Set `CycleMask = "1"` (one firing position) on the seeded series.
- **Files modified:** `QuestBoard.IntegrationTests/Middleware/CrossBoardRouteCoverageTests.cs`
- **Verification:** `Series/Details` theory row passes; full `QuestBoard.IntegrationTests` suite green.
- **Committed in:** `a87db17f` (Task 3 commit)

---

**Total deviations:** 2 auto-fixed (both Rule 1 -- test-seeding bugs found while proving the coverage theory, not production defects)
**Impact on plan:** Both fixes are to the new test file's own seed data; no production code changed as a result. No scope creep.

## Issues Encountered

None beyond the two auto-fixed seeding bugs above. `dotnet build` (0 new warnings beyond the pre-existing `NU1608`/`xUnit1051` ones) and the whole-solution `dotnet test` both ran clean on the first attempt with no CLR flake this time -- `QuestBoard.UnitTests` 670/670, `QuestBoard.IntegrationTests` 908/908.

## Next Phase Readiness

- All 18 routes named by the phase now resolve across boards, each proven by a real HTTP round trip. The registry, repository, and resolver are exactly the shape 87-03 (picker-skip path) and 87-04 (oracle-parity hardening) build on.
- The allowlist architecture test (`CrossBoardIgnoreQueryFiltersSeamTests`) is now load-bearing for the rest of the phase: any future call site anywhere in production code outside the 5-file list fails the build immediately.
- No blockers. `AmbientClockSeamTests` was read and mirrored but never edited (`git status --porcelain` on it returns nothing), keeping the two seams independently testable.

---
*Phase: 87-cross-board-deep-link-recovery*
*Plan: 02*
*Completed: 2026-09-21*
