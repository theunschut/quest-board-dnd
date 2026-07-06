---
phase: 54-fix-mobile-signup-for-finalized-quests-inconsistent-with-des
plan: 01
subsystem: api
tags: [aspnet-core-mvc, ef-core, waitlist, quest-signup]

# Dependency graph
requires:
  - phase: 44-post-finalization-voting-waitlist-auto-promotion
    provides: "WaitlistOrdering.OrderWaitlist extension and QuestService.PromoteNextWaitlistedPlayerIfSeatFreedAsync auto-promotion pipeline, reused unchanged"
provides:
  - "JoinFinalizedQuest waitlists a full-quest Player join (IsSelected = false) instead of hard-rejecting it"
  - "Integration test coverage for the capacity-branch decision (full/space-available Player, always-seated AssistantDM/Spectator)"
  - "Unit test coverage confirming a JoinFinalizedQuest-shaped new joiner orders correctly in GetTopWaitlistedCandidateAsync"
affects: [54-02]

# Tech tracking
tech-stack:
  added: []
  patterns: ["role-and-capacity boolean threaded into IsSelected instead of a hard reject + redirect"]

key-files:
  created:
    - QuestBoard.IntegrationTests/Controllers/QuestJoinFinalizedQuestTests.cs
  modified:
    - QuestBoard.Service/Controllers/QuestBoard/QuestController.cs
    - QuestBoard.UnitTests/Repository/PlayerSignupRepositoryTests.cs

key-decisions:
  - "Kept the existing recompute-then-decide shape in JoinFinalizedQuest; only changed the decision's outcome from ModelState error + redirect to a boolean feeding IsSelected"
  - "No call added to PromoteNextWaitlistedPlayerIfSeatFreedAsync — a new join never frees a seat"

patterns-established:
  - "Capacity-gated role assignment: `role != SignupRole.Player || <space check>` short-circuits non-Player roles to always-seated"

requirements-completed: [D-03, D-04, D-05]

# Metrics
duration: 25min
completed: 2026-07-06
---

# Phase 54 Plan 01: JoinFinalizedQuest waitlist-instead-of-reject Summary

**A full-quest Player join now creates a waitlisted `PlayerSignup` (`IsSelected = false`) instead of being hard-rejected, via one boolean threaded into the existing signup-creation shape of `QuestController.JoinFinalizedQuest`.**

## Performance

- **Duration:** ~25 min
- **Tasks:** 2/2 completed
- **Files modified:** 3 (1 controller, 2 test files — 1 new)

## Accomplishments
- `JoinFinalizedQuest`'s capacity branch no longer returns `ModelState.AddModelError("This quest is full...")` + redirect for a full-quest Player join; it now creates the signup waitlisted, reusing Phase 44's existing waitlist ordering/auto-promotion machinery unchanged
- AssistantDM/Spectator joins remain always-seated regardless of Player-slot fullness (unchanged behavior, explicitly regression-tested)
- New-joiner-shaped waitlisted signups (fresh `SignupTime`, `LastVoteChangeTime == null`) participate correctly in `GetTopWaitlistedCandidateAsync`'s existing tiebreak ordering — no special-casing needed
- Since `JoinFinalizedQuest` is the one shared controller action both desktop and mobile POST to, this fix applies identically to both platforms

## Task Commits

Each task was committed atomically:

1. **Task 1: Wave 0 — write failing tests for JoinFinalizedQuest capacity-branch behavior and new-joiner waitlist ordering** - `845ec7f` (test)
2. **Task 2: Change JoinFinalizedQuest capacity branch to waitlist a full Player join and make Task 1 tests GREEN** - `7ba2e5b` (feat)

_TDD cycle: RED confirmed (`JoinFinalizedQuest_Post_WhenQuestFullAndRoleIsPlayer_CreatesWaitlistedSignup` failed against unmodified controller code — 1 of 4 integration tests failed, the other 3 already passed since AssistantDM/Spectator/space-available behavior was unaffected), then GREEN (all 4 integration tests + 14 unit tests passed after Task 2)._

## Files Created/Modified
- `QuestBoard.IntegrationTests/Controllers/QuestJoinFinalizedQuestTests.cs` - New file: 4 integration tests covering full-quest Player waitlisting, space-available Player seating, and always-seated AssistantDM/Spectator regression guards
- `QuestBoard.UnitTests/Repository/PlayerSignupRepositoryTests.cs` - Added `GetTopWaitlistedCandidateAsync_NewJoinerFromJoinFinalizedQuest_OrdersCorrectlyAmongExistingWaitlist` confirming ordering correctness for a new-joiner-shaped waitlisted signup
- `QuestBoard.Service/Controllers/QuestBoard/QuestController.cs` - `JoinFinalizedQuest`: replaced the capacity hard-reject block with an `isPlayerRoleWithSpace` boolean, threaded into the signup's `IsSelected` assignment

## Decisions Made
- Preserved the exact recompute-then-decide shape (`quest.PlayerSignups.Where(ps => ps.IsSelected && ps.Role == SignupRole.Player).Count()`) already in place — only the outcome of the decision changed, not the server-side trust boundary (T-54-01 mitigation)
- Did not call `PromoteNextWaitlistedPlayerIfSeatFreedAsync` from this action — a new join never frees a seat, so no promotion signal applies here

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

None.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- Backend half of Phase 54 complete: `JoinFinalizedQuest` now waitlists full-quest Player joins for both desktop and mobile callers
- Plan 02 (mobile "Join This Quest" card UI + copy update) can proceed independently against disjoint files (`Details.Mobile.cshtml`, `Details.cshtml` copy-only edit) — no dependency on this plan's controller change beyond it already being correct
- `AntiForgeryTokenCoverageTests` confirmed green — no antiforgery regression from the capacity-branch edit

---
*Phase: 54-fix-mobile-signup-for-finalized-quests-inconsistent-with-des*
*Completed: 2026-07-06*
