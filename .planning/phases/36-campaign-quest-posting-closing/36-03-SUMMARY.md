---
phase: 36-campaign-quest-posting-closing
plan: 03
subsystem: api
tags: [mvc, controllers, authorization, csrf, campaign-mode, quest-lifecycle]

# Dependency graph
requires:
  - phase: 36-campaign-quest-posting-closing (Plan 01)
    provides: "IsClosed/ClosedDate fields on QuestEntity/Quest, migration, TestDataHelper campaign seeding"
  - phase: 36-campaign-quest-posting-closing (Plan 02)
    provides: "QuestService.CloseQuestAsync/ReopenQuestAsync, IsClosed-aware board filters and Quest Log OR-branch"
provides:
  - "QuestController.Close/Reopen POST actions (DungeonMasterOnly + IsQuestOwner-or-Admin + anti-forgery)"
  - "QuestController.GetActiveBoardTypeAsync helper, IGroupService injected"
  - "ViewBag.BoardType threaded through QuestController Index/Create/Details/Manage and QuestLogController Index/Details"
  - "QuestViewModel.ProposedDates relaxed (no [Required]/[MinLength], defaults to empty list)"
  - "QuestController.Create conditional validation: one-shot still requires a date, campaign silently defaults ProposedDates/ChallengeRating/TotalPlayerCount/DungeonMasterSession"
  - "QuestLogController.Details/UpdateRecap guards admit closed campaign quests"
  - "QuestCloseTests integration suite (Close/Reopen happy paths, non-owner-non-admin denial, campaign vs one-shot Create)"
affects: [36-04, 36-05]

# Tech tracking
tech-stack:
  added: []
  patterns: ["GetActiveBoardTypeAsync helper resolving BoardType server-side from the active group, defaulting to OneShot when no group is active (mirrors the existing SuperAdmin-has-no-active-group GetEffectiveRoleAsync convention)"]

key-files:
  created:
    - QuestBoard.IntegrationTests/Controllers/QuestCloseTests.cs
    - .planning/phases/36-campaign-quest-posting-closing/deferred-items.md
  modified:
    - QuestBoard.Service/Controllers/QuestBoard/QuestController.cs
    - QuestBoard.Service/Controllers/QuestBoard/QuestLogController.cs
    - QuestBoard.Service/ViewModels/QuestViewModels/QuestViewModel.cs

key-decisions:
  - "GetActiveBoardTypeAsync defaults to BoardType.OneShot when ActiveGroupId is null instead of calling RequireActiveGroupId() — SuperAdmin legitimately has no active group and must not crash Index/Details/Manage"
  - "QuestController.Create now sets quest.GroupId from the active group before persisting — pre-existing gap where every created quest defaulted to GroupId 0 and was invisible under the GroupId-scoped EF Core query filter; surfaced by writing the campaign-Create integration test, fixed as Rule 2 (missing critical multi-tenancy correctness)"
  - "CSRF rejection for Close/Reopen is verified via the existing reflection-based AntiForgeryTokenCoverageTests sweep, not a live HTTP round-trip — the integration test factory's TestAntiforgeryDecorator always passes validation, so a missing-token POST cannot be distinguished from a valid one at the HTTP layer in this test harness"
  - "Non-owner-non-admin Close assertion uses BeOneOf(Forbidden, Redirect, Unauthorized) matching the codebase's established authorization-regression-test convention — Forbid() resolves through the Identity.Application forbid scheme in tests, which redirects rather than returning a raw 403"

patterns-established:
  - "Any future QuestController/QuestLogController action needing per-group config must resolve it via a GetActiveBoardTypeAsync-shaped helper that null-guards ActiveGroupId rather than calling RequireActiveGroupId() directly, to stay SuperAdmin-safe"

requirements-completed: [CQUEST-01, CQUEST-03, CQUEST-04, CQUEST-05, CQUEST-06]

# Metrics
duration: 35min
completed: 2026-07-03
---

# Phase 36 Plan 03: Campaign Quest Controller Wiring Summary

**Close/Reopen controller actions mirroring Finalize/Open's authorization exactly, campaign-mode Create validation relaxation with server-authoritative field defaulting, ViewBag.BoardType threaded through every quest-render action, and a QuestCloseTests integration suite — plus two pre-existing bugs (missing Quest.GroupId on Create, SuperAdmin-crashing GetActiveBoardTypeAsync) caught and fixed while writing the tests**

## Performance

- **Duration:** ~35 min
- **Started:** 2026-07-03T14:10:00Z
- **Completed:** 2026-07-03T14:45:00Z
- **Tasks:** 3 completed
- **Files modified:** 4 (3 modified, 1 new test file), plus 1 new tracking doc

## Accomplishments
- `QuestController` gained `Close`/`Reopen` POST actions mirroring `Open`'s exact authorization shape (`[HttpPost][ValidateAntiForgeryToken][Authorize(Policy = "DungeonMasterOnly")]`, `IsQuestOwner`-or-Admin check, redirect to `Manage`) — reusing the Phase 34.3-fixed `User.Id`-based ownership check verbatim, no new comparison logic introduced
- `GetActiveBoardTypeAsync` helper added to both `QuestController` and `QuestLogController`, resolving `BoardType` server-side from the active group via `IGroupService` — never trusted from posted form data
- `ViewBag.BoardType` threaded through `QuestController.Index`/`Create` (GET)/`Details` (GET)/`Manage` (GET) and `QuestLogController.Index`/`Details`
- `QuestViewModel.ProposedDates` relaxed to an unannotated empty-list default; `QuestController.Create` POST now applies conditional validation — one-shot boards still require at least one date, campaign boards silently override `ProposedDates`/`ChallengeRating`/`TotalPlayerCount`/`DungeonMasterSession` to fixed server-side defaults regardless of what was posted
- `QuestLogController.Details`/`UpdateRecap` guards restructured to admit closed campaign quests (`IsClosed`) alongside the existing completed-one-shot path, without weakening the one-shot next-day-wait rule
- New `QuestCloseTests.cs` integration suite: Close/Reopen happy paths (redirect + persisted `IsClosed` state), non-owner-non-admin denial with the quest remaining open, campaign Create with no dates persisting, and one-shot Create with no dates re-rendering with a validation error
- Full test suite: 123/123 unit tests green, 240/241 integration tests green (the 1 failure is a pre-existing, unrelated rate-limit flake in `AdminControllerIntegrationTests`, documented in `deferred-items.md`)

## Task Commits

Each task was committed atomically:

1. **Task 1: Add Close/Reopen actions, GetActiveBoardTypeAsync helper, and BoardType ViewBag threading** - `ccb1c04` (feat)
2. **Task 2: Relax Create ProposedDates for campaign + fix Quest Log guards** - `115160d` (feat)
3. **Task 3: Integration tests for Close/Reopen authorization, CSRF, and campaign Create** - `aaf8c89` (test) — includes the two bug fixes surfaced while writing these tests (see Deviations)
4. **Deferred-items tracking doc** - `f340596` (docs)

## Files Created/Modified
- `QuestBoard.Service/Controllers/QuestBoard/QuestController.cs` - `IGroupService` injected; `GetActiveBoardTypeAsync` helper; `Close`/`Reopen` actions; `ViewBag.BoardType` in `Index`/`Create`/`Details`/`Manage`; `Create` POST conditional validation + campaign field defaults; `quest.GroupId` now set from the active group on create
- `QuestBoard.Service/Controllers/QuestBoard/QuestLogController.cs` - `IGroupService` injected; `GetActiveBoardTypeAsync` helper; `ViewBag.BoardType` in `Index`/`Details`; `Details`/`UpdateRecap` guards admit closed campaign quests
- `QuestBoard.Service/ViewModels/QuestViewModels/QuestViewModel.cs` - `ProposedDates` drops `[Required]`/`[MinLength]`, defaults to `[]`
- `QuestBoard.IntegrationTests/Controllers/QuestCloseTests.cs` - New integration suite (5 tests)
- `.planning/phases/36-campaign-quest-posting-closing/deferred-items.md` - New tracking doc for the pre-existing `AdminControllerIntegrationTests` flake

## Decisions Made
- `GetActiveBoardTypeAsync` null-guards `ActiveGroupId` and defaults to `BoardType.OneShot` rather than calling `RequireActiveGroupId()`, matching the codebase's established SuperAdmin-has-no-active-group convention used by `GetEffectiveRoleAsync`
- CSRF enforcement for `Close`/`Reopen` relies on the existing reflection-based `AntiForgeryTokenCoverageTests` sweep rather than a new live-HTTP missing-token test, because the integration test factory's `TestAntiforgeryDecorator` always returns `true` from `IsRequestValidAsync`/`ValidateRequestAsync` — a missing-token POST is structurally indistinguishable from a valid one in this harness. `[ValidateAntiForgeryToken]` is present on both new actions and covered by the existing sweep.
- Non-owner-non-admin `Close` test asserts `BeOneOf(Forbidden, Redirect, Unauthorized)` rather than a strict `Forbidden`, matching every other authorization-regression test in the codebase (`Forbid()` resolves through the `Identity.Application` forbid scheme in the test environment, which issues a redirect rather than a raw 403)

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 - Missing Critical] `QuestController.Create` never set `quest.GroupId`**
- **Found during:** Task 3 (writing the campaign Create integration test)
- **Issue:** Neither `QuestViewModel`, the AutoMapper `QuestViewModel → Quest` mapping, nor the `Create` action ever assigned `Quest.GroupId`. Every quest created via `Create` defaulted to `GroupId = 0`, which falls outside every real group's `GroupId`-scoped EF Core global query filter — the quest would be invisible on any board. This was a pre-existing gap (not introduced by this plan) that only surfaced because this plan's campaign-Create test needed to read the quest back through a group-filtered context to verify it persisted with no dates.
- **Fix:** `Create` POST now sets `quest.GroupId = activeGroupContext.RequireActiveGroupId();` immediately after the AutoMapper projection, before the quest is persisted.
- **Files modified:** `QuestBoard.Service/Controllers/QuestBoard/QuestController.cs`
- **Verification:** `Campaign_Create_WithNoProposedDates_Persists` and `OneShot_Create_WithNoProposedDates_ReRendersWithValidationError` both pass; full test suite shows no regressions.
- **Committed in:** `aaf8c89` (Task 3 commit)

**2. [Rule 1 - Bug] `GetActiveBoardTypeAsync` crashed `QuestController.Index` for a SuperAdmin with no active group**
- **Found during:** Task 3 (full-suite verification run after Tasks 1-2)
- **Issue:** The helper called `activeGroupContext.RequireActiveGroupId()` unconditionally, which throws `InvalidOperationException` when no group is active. `QuestController.Index` explicitly supports SuperAdmin browsing with no active group selected (documented in its own comment), so wiring the new `ViewBag.BoardType` line into `Index` reintroduced the exact SuperAdmin-crash class of bug that Phase 34.3 had already fixed for `GetEffectiveRoleAsync`. Caught by `GroupSessionMiddlewareIntegrationTests.SuperAdmin_NoActiveGroup_NotRedirectedByMiddleware` failing in the full-suite run.
- **Fix:** `GetActiveBoardTypeAsync` (both `QuestController` and `QuestLogController`) now checks `activeGroupContext.ActiveGroupId is not { } groupId` and returns `BoardType.OneShot` early instead of throwing, mirroring the existing `GetEffectiveRoleAsync` SuperAdmin short-circuit pattern.
- **Files modified:** `QuestBoard.Service/Controllers/QuestBoard/QuestController.cs`, `QuestBoard.Service/Controllers/QuestBoard/QuestLogController.cs`
- **Verification:** Full `dotnet test` run shows `GroupSessionMiddlewareIntegrationTests.SuperAdmin_NoActiveGroup_NotRedirectedByMiddleware` passing; 240/241 integration tests green afterward.
- **Committed in:** `aaf8c89` (Task 3 commit)

---

**Total deviations:** 2 auto-fixed (1 missing critical / multi-tenancy correctness, 1 bug)
**Impact on plan:** Both fixes were necessary for the plan's own acceptance criteria to be verifiable and for the SuperAdmin path (an existing, tested contract) to keep working. No scope creep beyond what this plan's own controller changes touched.

## Issues Encountered

- The plan's Task 3 spec called for an explicit "CSRF: POST without a valid anti-forgery token → rejected" integration test. Investigation of the existing test harness (`WebApplicationFactoryBase.ConfigureWebHost`) showed the integration test factory installs a `TestAntiforgeryDecorator` that always returns success from anti-forgery validation, specifically so that other tests don't need real token round-trips. A live HTTP test asserting rejection on a missing token would therefore always pass regardless of whether `[ValidateAntiForgeryToken]` is actually present — a false-positive test. The codebase already has a dedicated mechanism for this exact class of assertion (`AntiForgeryTokenCoverageTests`, a reflection sweep over every `[HttpPost]` action), and `Close`/`Reopen` are automatically covered by it since they carry `[HttpPost]` + `[ValidateAntiForgeryToken]`. Ran that existing sweep to confirm it still passes with the new actions included, rather than writing a test that could not detect the condition it claims to test.
- Both bugs documented above were caught by the mandated full-suite verification (`dotnet test`) after Task 3's integration tests initially passed in isolation — the campaign Create GroupId bug required the SeedCampaignGroupAsync-backed test to actually read the quest back, and the SuperAdmin crash only appeared once the unrelated `GroupSessionMiddlewareIntegrationTests` ran in the same suite as the new `ViewBag.BoardType` wiring in `Index`.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- `Close`/`Reopen` actions, `ViewBag.BoardType`, and the relaxed campaign `Create` flow are all available for Plan 04 (views) to consume
- `QuestLogController` guards and `ViewBag.BoardType` wiring are ready for Plan 04/05's Quest Log view work
- The `Quest.GroupId`-on-Create fix means every quest created going forward (one-shot and campaign) is correctly tenant-scoped — this was silently broken before and is now fixed for all boards, not just campaign
- One pre-existing, unrelated test flake remains open (see `deferred-items.md`) — does not block this plan or the phase
- No blockers identified for Plans 04/05

---
*Phase: 36-campaign-quest-posting-closing*
*Completed: 2026-07-03*

## Self-Check: PASSED

All claimed files found on disk (`QuestController.cs`, `QuestLogController.cs`, `QuestViewModel.cs`,
`QuestCloseTests.cs`, `deferred-items.md`, this summary); all four task commits (`ccb1c04`, `115160d`,
`aaf8c89`, `f340596`) verified present in git log.
