---
phase: 55-fix-cross-tenant-quest-leak-on-quest-board-quests-from-anoth
plan: 04
subsystem: auth
tags: [middleware, session, aspnet-core, tenant-isolation, group-membership]

# Dependency graph
requires:
  - phase: 55-02
    provides: GroupSessionMiddleware reordered (exempt-first, no SuperAdmin bypass, null-ActiveGroupId gate)
  - phase: 55-03
    provides: GroupPickerController with IUserService injected and SelectGroup membership check
provides:
  - Interval-gated (5-minute) re-validation of a session's ActiveGroupId membership inside GroupSessionMiddleware
  - ActiveGroupValidatedAtUtc session key stamped at both GroupPickerController write-sites
  - Fix for a latent integration-test-infrastructure gap that let ~60 tests pass without real group membership
affects: [55-fix-cross-tenant-quest-leak (phase closure), any future integration test using CreateAuthenticatedClientWithUserAsync]

# Tech tracking
tech-stack:
  added: []
  patterns: ["Session-timestamp-gated re-validation (mirrors SecurityStampValidatorOptions.ValidationInterval concept, independent implementation since ActiveGroupId lives in Session, not claims)"]

key-files:
  created:
    - QuestBoard.UnitTests/Middleware/GroupSessionMiddlewareRevalidationTests.cs
  modified:
    - QuestBoard.Service/Constants/SessionKeys.cs
    - QuestBoard.Service/Controllers/GroupPickerController.cs
    - QuestBoard.Service/Middleware/GroupSessionMiddleware.cs
    - QuestBoard.IntegrationTests/Helpers/AuthenticationHelper.cs
    - QuestBoard.IntegrationTests/Tests/TenantIsolationTests.cs

key-decisions:
  - "5-minute re-validation interval reuses the app's existing SecurityStampValidatorOptions.ValidationInterval bound for consistency, implemented independently since ActiveGroupId lives in Session, not the auth cookie's claims"
  - "A removed member is gated out via the exact same GET/HEAD-redirect vs POST-409 branch the null-ActiveGroupId gate already uses, not a new response shape"
  - "Unparseable or absent ActiveGroupValidatedAtUtc forces re-validation, never skips it — fail toward re-checking"
  - "CreateAuthenticatedClientWithUserAsync now seeds a group-1 UserGroups row whenever roles is left at its default (null); an explicit empty array (roles: []) still opts out, preserving the one existing zero-membership test's intent"

patterns-established:
  - "Membership re-validation via GetGroupRoleByIdAsync inside middleware, resolved through context.RequestServices (scoped service in singleton-style middleware), same resolution style as IActiveGroupContext"

requirements-completed: []

# Metrics
duration: 32min
completed: 2026-07-06
---

# Phase 55 Plan 04: Interval-Gated Group Membership Re-Validation Summary

**GroupSessionMiddleware now re-checks a session's ActiveGroupId membership every 5 minutes via GetGroupRoleByIdAsync, gating out removed members with the same redirect/409 pattern as the null-group case, closing the time-axis gap where a removed member kept board access until their session happened to re-select a group.**

## Performance

- **Duration:** 32 min
- **Started:** 2026-07-06T08:01:00Z
- **Completed:** 2026-07-06T08:33:00Z
- **Tasks:** 3 completed
- **Files modified:** 6 (3 planned production/test files + 2 deviation fixes in test infrastructure, plus 1 new test file)

## Accomplishments
- `ActiveGroupValidatedAtUtc` session key stamped at both `GroupPickerController` write-sites (`Index`'s single-group auto-select branch and `SelectGroup`)
- `GroupSessionMiddleware.InvokeAsync` gained an interval-gated re-validation block: stale/missing timestamp triggers a `GetGroupRoleByIdAsync` check; a no-longer-member is cleared from session and redirected (GET/HEAD) or 409'd (POST/PUT/PATCH/DELETE); a still-valid member gets `ActiveGroupValidatedAtUtc` restamped; within the 5-minute window no DB round-trip occurs; SuperAdmin is excluded entirely
- New `GroupSessionMiddlewareRevalidationTests.cs` unit test suite (5 `[Fact]`s) drives `InvokeAsync` directly against a hand-rolled in-memory `ISession` double, since the integration harness cannot round-trip session cookies or simulate elapsed wall-clock time
- Full regression sweep: 182 unit tests + 307 integration tests all green (up from 62 integration failures surfaced mid-implementation — see Deviations)

## Task Commits

Each task was committed atomically:

1. **Task 1: Add validated-at session key + stamp at both write-sites** - `b892e63` (feat)
2. **Task 2: Write failing middleware re-validation unit tests (RED)** - `5424a7e` (test)
3. **Task 3: Implement interval-gated re-validation block (GREEN)** - `f23d309` (feat)
4. **Deviation fix: seed UserGroups membership in test auth helper** - `11b774e` (fix)

_TDD-shaped plan: test (RED) → feat (GREEN), same as the tdd_execution convention even though the plan itself is `type: execute`, not `type: tdd`._

## Files Created/Modified
- `QuestBoard.Service/Constants/SessionKeys.cs` - Added `ActiveGroupValidatedAtUtc` constant
- `QuestBoard.Service/Controllers/GroupPickerController.cs` - Stamps the timestamp at both `ActiveGroupId` write-sites
- `QuestBoard.Service/Middleware/GroupSessionMiddleware.cs` - New interval-gated re-validation block, `MembershipRevalidationInterval` field, updated class doc comment (step 4/5)
- `QuestBoard.UnitTests/Middleware/GroupSessionMiddlewareRevalidationTests.cs` - New: 5 tests (removed+stale GET, removed+stale POST, still-member restamp, fresh-no-DB-call, SuperAdmin-excluded), backed by hand-rolled `FakeSession`/`FakeActiveGroupContext` test doubles
- `QuestBoard.IntegrationTests/Helpers/AuthenticationHelper.cs` - `CreateAuthenticatedClientWithUserAsync` now seeds a group-1 `UserGroups` row by default (deviation fix)
- `QuestBoard.IntegrationTests/Tests/TenantIsolationTests.cs` - `GroupFilter_NonExistentGroup_ReturnsEmpty` switched to a SuperAdmin client (deviation fix)

## Decisions Made
- Reused the concept (not the code path) of Phase 41's `SecurityStampValidatorOptions.ValidationInterval` — 5-minute bounded staleness — since `ActiveGroupId` lives in `HttpContext.Session`, not in the auth cookie's claims, so it needs an independent session-timestamp check
- Round-trip ("O") `DateTime` formatting with `DateTimeStyles.RoundtripKind` parsing, matching the interfaces block's exact recommendation
- Fail-toward-re-checking: a null or unparseable `ActiveGroupValidatedAtUtc` always triggers re-validation, never skips it (mitigates T-55-11 tampering threat)

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Test-infrastructure gap: `CreateAuthenticatedClientWithUserAsync` never seeded `UserGroups` membership when called without a `roles` argument**
- **Found during:** Task 3, running the full regression suite after implementing the re-validation block
- **Issue:** The new interval-gated membership check correctly calls `GetGroupRoleByIdAsync` and gates out non-members. ~60 existing integration tests authenticate via `CreateAuthenticatedClientWithUserAsync(factory, ...)` with no `roles` argument, which historically skipped the `UserGroups`-seeding block entirely. The shared `MutableGroupContext` test double defaults `ActiveGroupId = 1` regardless of real membership, so these tests were only ever passing because the pre-Plan-04 middleware never checked membership at all — a latent gap the new correctness fix immediately surfaced as 62 test failures (302 redirects instead of 200 OK).
- **Fix:** `CreateAuthenticatedClientWithUserAsync` now seeds a group-1 `UserGroups` row (defaulting to `GroupRole.Player`, or `Admin`/`DungeonMaster` if those roles were requested) whenever `roles` is left at its default (`null`). An explicit empty array (`roles: []`) still opts out entirely, preserving the one existing test (`GroupPickerControllerIntegrationTests.Index_WhenUserHasNoGroupMemberships_ShouldReturnFriendlyEmptyState`) that deliberately relies on zero memberships.
- **Files modified:** `QuestBoard.IntegrationTests/Helpers/AuthenticationHelper.cs`
- **Verification:** Full suite re-run: 182 unit + 307 integration tests, all green.
- **Committed in:** `11b774e`

**2. [Rule 1 - Bug] `TenantIsolationTests.GroupFilter_NonExistentGroup_ReturnsEmpty` conflated query-filter testing with membership-gating**
- **Found during:** Task 3, same regression pass
- **Issue:** This test intentionally points `TestGroupContext.ActiveGroupId` at group `999` (non-existent) to prove the EF Core query filter returns zero rows, not every row. With the new membership re-validation active, a non-SuperAdmin authenticated user with no membership in group 999 is now (correctly) redirected to `/groups/pick` before the request ever reaches the query filter, breaking the test's original assertion path.
- **Fix:** Switched this specific test's client to `CreateAuthenticatedSuperAdminClientAsync`, since SuperAdmin is exempt from membership re-validation by design (D-06) — this keeps the test exercising only the EF query filter's non-existent-group behavior, matching its stated docstring intent ("Proves that the EF Core HasQueryFilter correctly scopes quests").
- **Files modified:** `QuestBoard.IntegrationTests/Tests/TenantIsolationTests.cs`
- **Verification:** `TenantIsolationTests` — all 5 tests green.
- **Committed in:** `11b774e`

---

**Total deviations:** 2 auto-fixed (both Rule 1 — pre-existing test-infrastructure bugs surfaced by the new correctness check, not introduced by it)
**Impact on plan:** Both fixes were necessary to keep the existing test suite green without weakening the new re-validation logic. No scope creep — no production code outside the plan's stated files was touched; all changes were confined to test infrastructure the plan's own regression-check step (`dotnet test` full suite) is responsible for keeping green.

## Issues Encountered
None beyond the deviations documented above.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- D-06 (the last of Phase 55's residual gaps) is closed: a user removed from their active group mid-session is gated out within 5 minutes, matching the phase's stated success criteria
- Full suite green (182 unit + 307 integration tests) — no regressions carried forward
- No blockers for phase closure

---
*Phase: 55-fix-cross-tenant-quest-leak-on-quest-board-quests-from-anoth*
*Completed: 2026-07-06*
