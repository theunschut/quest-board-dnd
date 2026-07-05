---
phase: 49-fix-guild-members-page-missing-group-tenant-filtering
plan: 03
subsystem: shop
tags: [efcore, multi-tenancy, query-filter, regression-test, user-transaction]

# Dependency graph
requires: [49-01]
provides:
  - ShopService.ReturnOrSellItemAsync now looks up the original transaction via the Include-protected GetTransactionWithDetailsAsync
  - UserTransactionRepositoryTests.cs regression coverage proving cross-group exclusion
affects: []

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Regression test asserting an Include-driven inner join, not a dedicated GroupId column, protects a shared-only-via-navigation entity"

key-files:
  created:
    - QuestBoard.UnitTests/Repository/UserTransactionRepositoryTests.cs
  modified:
    - QuestBoard.Domain/Services/ShopService.cs

key-decisions:
  - "GetTransactionWithDetailsAsync was already declared on IUserTransactionRepository (added by a prior phase) — no interface change needed, only the one call-site swap in ShopService.ReturnOrSellItemAsync"
  - "No GroupId column or extra filter added to UserTransaction; the existing Include-driven inner-join protection is documented and enforced by test instead, per the plan's explicit instruction"

patterns-established:
  - "Pattern: for an entity that has no GroupId of its own but is protected only because every read Includes a filtered required-FK navigation, add a regression test asserting the cross-group row is excluded — this converts an incidental protection into an enforced invariant that fails loudly if the Include is ever dropped"

requirements-completed: []

# Metrics
duration: 8min
completed: 2026-07-05
status: complete
---

# Phase 49 Plan 03: UserTransaction Include-Protection Hardening Summary

**Closed the one unguarded UserTransaction lookup (ShopService.ReturnOrSellItemAsync's base GetByIdAsync) by switching it to the Include-protected GetTransactionWithDetailsAsync, and added a regression test proving the Include-driven inner join genuinely excludes cross-group transactions.**

## Performance

- **Duration:** 8 min
- **Started:** 2026-07-05T18:45:00Z
- **Completed:** 2026-07-05T18:53:00Z
- **Tasks:** 2
- **Files modified:** 2 (1 created, 1 modified)

## Accomplishments
- `ShopService.ReturnOrSellItemAsync`'s original-transaction lookup now uses `transactionRepository.GetTransactionWithDetailsAsync(transactionId, token)` instead of the unguarded base `GetByIdAsync(transactionId, token)` — a cross-group original transaction now resolves to `null` (correctly rejected by the existing ownership/null guard) instead of silently succeeding
- `GetTransactionWithDetailsAsync` was already present on `IUserTransactionRepository` (declared with an XML doc comment), so no interface change was required — confirmed before making the swap
- New `QuestBoard.UnitTests/Repository/UserTransactionRepositoryTests.cs` seeds two groups, one `ShopItem` per group, and one `Purchase` transaction against each (same buyer), then proves under `ActiveGroupId = 1`:
  - the transaction referencing the group-2 `ShopItem` is excluded from `GetTransactionsByUserAsync`
  - the transaction referencing the group-1 `ShopItem` is returned
- This converts the previously-incidental "Include folds the filter into an inner join" protection into an enforced regression: a future refactor that drops `.Include(t => t.ShopItem)` from `UserTransactionRepository`'s methods will now fail this test suite loudly instead of silently reopening the leak

## Task Commits

Each task was committed atomically:

1. **Task 1: Switch ReturnOrSellItemAsync to the Include-protected GetTransactionWithDetailsAsync** - `61cbe29` (fix)
2. **Task 2: Create UserTransactionRepositoryTests proving cross-group exclusion** - `88a33ed` (test)

## Files Created/Modified
- `QuestBoard.Domain/Services/ShopService.cs` - `ReturnOrSellItemAsync`'s original-transaction lookup switched from `GetByIdAsync` to `GetTransactionWithDetailsAsync`; no other logic changed
- `QuestBoard.UnitTests/Repository/UserTransactionRepositoryTests.cs` - New: 2 tests (cross-group exclusion, same-group inclusion) using a mutable `IActiveGroupContext` test double mirroring `PlayerSignupRepositoryTests.cs`'s InMemory harness pattern

## Decisions Made
- Confirmed `GetTransactionWithDetailsAsync` was already declared on `IUserTransactionRepository` before making any change — the plan's "confirm, add if missing" instruction resolved to a no-op here, so the diff is a single line in `ShopService.cs`.
- Built the test's mutable `IActiveGroupContext` test double (`MutableTestGroupContext`) rather than reusing `PlayerSignupRepositoryTests`'s fixed `TestActiveGroupContext` (which always returns `null`), since this test needs one seeded database queried under two different active-group values in the same test method — seed with `ActiveGroupId = null` (sees all rows while writing), then flip to `1` before the read-side assertion.

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered
- The `Write` tool created `UserTransactionRepositoryTests.cs` with LF-only line endings instead of CRLF (project convention per CLAUDE.md). Converted to CRLF via a small script before running tests; verified 0 LF-only lines remaining. No functional impact, caught before commit. (Same class of issue noted in plan 49-01's SUMMARY — appears to be a `Write`-tool-wide behavior on this platform, not specific to this file.)

## User Setup Required

None - no external service configuration, no new migration, no schema change.

## Next Phase Readiness

- `dotnet build` succeeds for the full solution
- `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~UserTransactionRepositoryTests"` — 2/2 passed
- `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~Shop"` (sanity/regression check) — 6/6 passed, no regression in refund/return logic
- Grep of `ShopService.cs` for `D-1`/`Phase 49` — zero matches, no tracking IDs leaked into source
- No blockers for sibling phase-49 plans or the phase's final integration/merge step

---
*Phase: 49-fix-guild-members-page-missing-group-tenant-filtering*
*Completed: 2026-07-05*
