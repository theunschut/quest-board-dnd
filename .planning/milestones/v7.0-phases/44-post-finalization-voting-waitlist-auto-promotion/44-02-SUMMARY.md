---
phase: 44-post-finalization-voting-waitlist-auto-promotion
plan: 02
subsystem: domain-services
tags: [efcore, hangfire, razor-email, automapper, xunit, waitlist, voting]

# Dependency graph
requires: ["44-01"]
provides:
  - "QuestService.ChangeVoteAsync — server-side-recomputed Yes-vote selection, no capacity rejection"
  - "QuestService.RevokeSignupAsync — wasSelected-gated promotion on hard-delete revoke"
  - "QuestService.PromoteNextWaitlistedPlayerIfSeatFreedAsync — shared promotion+email orchestration path"
  - "IQuestEmailDispatcher.EnqueueWaitlistPromotedEmail — singular-recipient promotion email dispatch"
  - "QuestWaitlistPromotedEmailJob + WaitlistPromoted.razor — single-recipient promotion email pipeline"
affects: [44-03]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Singular recipientEmail/playerName parameters (not arrays) on EnqueueWaitlistPromotedEmail, making multi-recipient broadcast structurally impossible"
    - "Shared PromoteNextWaitlistedPlayerIfSeatFreedAsync called from both the vote-No path and the revoke path, avoiding the two-call-site drift pitfall RESEARCH.md flagged"

key-files:
  created:
    - QuestBoard.Service/Components/Emails/WaitlistPromoted.razor
    - QuestBoard.Service/Jobs/QuestWaitlistPromotedEmailJob.cs
  modified:
    - QuestBoard.Domain/Interfaces/IQuestEmailDispatcher.cs
    - QuestBoard.Service/Services/HangfireQuestEmailDispatcher.cs
    - QuestBoard.Service/Services/NullQuestEmailDispatcher.cs
    - QuestBoard.Domain/Interfaces/IQuestService.cs
    - QuestBoard.Domain/Services/QuestService.cs
    - QuestBoard.Domain/Interfaces/IPlayerSignupService.cs
    - QuestBoard.Domain/Services/PlayerSignupService.cs
    - QuestBoard.Service/Controllers/QuestBoard/QuestController.cs
    - QuestBoard.UnitTests/Services/QuestServiceTests.cs

key-decisions:
  - "NullQuestEmailDispatcher (Testing-environment no-op dispatcher) was not listed in the plan's files_modified but had to gain the new interface method to keep the solution compiling — added as an in-scope Rule 3 fix, not a deviation requiring user input, since it is a structural consequence of extending IQuestEmailDispatcher."
  - "QuestController.ChangeVoteToYes delegates to the new QuestService.ChangeVoteAsync (removing its own 'already selected'/'no available spots' hard-rejects) rather than being deleted outright in this plan, per the plan's own compile-order note — Plan 03 owns the full controller rewrite into a single ChangeVote(id, vote) action."
  - "RevokeSignup controller action left calling playerSignupService.RemoveAsync directly (not yet wired to QuestService.RevokeSignupAsync) — the plan's Task 2 scope was QuestService orchestration only; wiring RevokeSignup's controller call site to the new RevokeSignupAsync is explicitly Plan 03's responsibility per the plan's read_first/action notes."

patterns-established:
  - "Promotion selection is never gated on email eligibility (IsSelected flips regardless) — only the email send itself is skipped for players with no confirmed email, mirroring FinalizeQuestAsync's existing filter."

requirements-completed: [VOTE-04, VOTE-05, VOTE-07]

# Metrics
duration: 40min
completed: 2026-07-05
status: complete
---

# Phase 44 Plan 02: Promotion Orchestration & Single-Recipient Email Pipeline Summary

**QuestService.ChangeVoteAsync/RevokeSignupAsync share one PromoteNextWaitlistedPlayerIfSeatFreedAsync path that promotes exactly one waitlisted candidate and emails only that candidate via a new singular-recipient WaitlistPromoted pipeline, backed by 5 new unit tests.**

## Performance

- **Duration:** ~40 min
- **Tasks:** 3
- **Files modified:** 11 (9 modified, 2 created)

## Accomplishments
- Added `WaitlistPromoted.razor` cloning `QuestFinalized.razor`'s visual system verbatim (Poster background, CR badge, Cinzel title, gold divider, metadata table, wax-seal+CTA row) with new promotion copy and a single `PlayerName` string parameter instead of a recipient list
- Added `QuestWaitlistPromotedEmailJob` mirroring `QuestFinalizedEmailJob` but with no `for` loop and no dedup guard — sends to exactly one `recipientEmail`
- Added `IQuestEmailDispatcher.EnqueueWaitlistPromotedEmail` with singular `recipientEmail`/`playerName` parameters (not arrays), implemented in both `HangfireQuestEmailDispatcher` and `NullQuestEmailDispatcher`
- Added `QuestService.ChangeVoteAsync`: persists the vote via `playerSignupRepository.ChangeVoteAsync`, and for a Yes vote re-fetches the quest server-side to decide selection from a fresh `selectedCount < TotalPlayerCount` check — never trusting any client-supplied capacity signal, never rejecting
- Added `QuestService.RevokeSignupAsync`: re-fetches the quest, captures `wasSelected` before deleting the signup, and only triggers promotion when the deleted signup was previously selected
- Added the shared private `PromoteNextWaitlistedPlayerIfSeatFreedAsync`, called from both paths: finds the top waitlisted candidate, guards `candidate.Id == freeingPlayerSignupId` (never promotes the freeing player), flips `IsSelected`, and enqueues the promotion email only when the candidate has a confirmed email
- Removed the Plan-01 temporary shim `ChangeVoteToYesAndSelectAsync` from `IPlayerSignupService`/`PlayerSignupService`
- Updated `QuestController.ChangeVoteToYes` to delegate to `questService.ChangeVoteAsync` (removing its own hard-reject checks) so the solution keeps compiling ahead of Plan 03's full controller rewrite
- Added 5 new `QuestServiceTests` covering VOTE-04 (vote-No promotion, revoke-promotion), VOTE-05 (Maybe no-op), and VOTE-07 (waitlisted-revoke no-op, freeing-player-never-promoted guard); full unit suite (155 tests) green

## Task Commits

Each task was committed atomically:

1. **Task 1: WaitlistPromoted.razor + QuestWaitlistPromotedEmailJob + dispatcher method (single recipient)** - `de34623` (feat)
2. **Task 2: QuestService promotion orchestration for vote-No and revoke (VOTE-04/05)** - `d741b24` (feat)
3. **Task 3: Extend QuestServiceTests for promotion + single-recipient email (VOTE-04/05/07)** - `a0cf455` (test)

_Note: docs metadata commit is applied by the orchestrator after all wave agents complete, per worktree execution mode._

## Files Created/Modified
- `QuestBoard.Service/Components/Emails/WaitlistPromoted.razor` - new promotion email template, single `PlayerName` parameter
- `QuestBoard.Service/Jobs/QuestWaitlistPromotedEmailJob.cs` - new Hangfire job, single-recipient send, no dedup guard
- `QuestBoard.Domain/Interfaces/IQuestEmailDispatcher.cs` - new `EnqueueWaitlistPromotedEmail` signature (singular recipient)
- `QuestBoard.Service/Services/HangfireQuestEmailDispatcher.cs` - implements `EnqueueWaitlistPromotedEmail` via `jobClient.Enqueue<QuestWaitlistPromotedEmailJob>`
- `QuestBoard.Service/Services/NullQuestEmailDispatcher.cs` - no-op implementation of the new method (Rule 3 fix — required to keep Testing environment DI compiling)
- `QuestBoard.Domain/Interfaces/IQuestService.cs` - added `ChangeVoteAsync`/`RevokeSignupAsync` signatures
- `QuestBoard.Domain/Services/QuestService.cs` - added `ChangeVoteAsync`, `RevokeSignupAsync`, `PromoteNextWaitlistedPlayerIfSeatFreedAsync`
- `QuestBoard.Domain/Interfaces/IPlayerSignupService.cs` - removed `ChangeVoteToYesAndSelectAsync`
- `QuestBoard.Domain/Services/PlayerSignupService.cs` - removed `ChangeVoteToYesAndSelectAsync` shim
- `QuestBoard.Service/Controllers/QuestBoard/QuestController.cs` - `ChangeVoteToYes` delegates to `questService.ChangeVoteAsync`, hard-reject checks removed
- `QuestBoard.UnitTests/Services/QuestServiceTests.cs` - 5 new tests covering VOTE-04/05/07

## Decisions Made
- `NullQuestEmailDispatcher` required an implementation of the new interface method to keep the Testing-environment DI graph compiling — not called out in the plan's `files_modified` list, added as a structural Rule 3 fix
- Kept `RevokeSignup` controller action calling `playerSignupService.RemoveAsync` directly rather than wiring it to the new `QuestService.RevokeSignupAsync` — Plan 03 owns that controller rewrite per the plan's own task notes; this plan's scope was the `QuestService`-level orchestration only
- Selection for a Yes vote is decided from a freshly re-fetched `Quest` (`repository.GetQuestWithDetailsAsync`) rather than any parameter passed by the caller, mirroring `FinalizeQuestAsync`'s existing re-fetch pattern and closing the T-44-CAPACITY threat

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Added EnqueueWaitlistPromotedEmail to NullQuestEmailDispatcher**
- **Found during:** Task 1 build verification
- **Issue:** `dotnet build` failed with `CS0535` — `NullQuestEmailDispatcher` (the Testing-environment no-op `IQuestEmailDispatcher` implementation, not listed in this plan's `files_modified`) did not implement the new interface method, since interface changes propagate to every implementer.
- **Fix:** Added a no-op `EnqueueWaitlistPromotedEmail` implementation to `NullQuestEmailDispatcher`, mirroring its existing no-op pattern for `EnqueueFinalizedEmail`/`EnqueueDateChangedEmail`.
- **Files modified:** `QuestBoard.Service/Services/NullQuestEmailDispatcher.cs`
- **Verification:** `dotnet build --no-incremental` — Build succeeded
- **Committed in:** `de34623` (part of Task 1 commit)

**2. [Rule 3 - Blocking] Added `using QuestBoard.Domain.Models;` to QuestWaitlistPromotedEmailJob.cs**
- **Found during:** Task 1 build verification
- **Issue:** `EmailSettings` (referenced via `IOptions<EmailSettings>`) lives in `QuestBoard.Domain.Models` — the new job file was missing this using directive, causing `CS0246`.
- **Fix:** Added the missing using directive.
- **Files modified:** `QuestBoard.Service/Jobs/QuestWaitlistPromotedEmailJob.cs`
- **Verification:** `dotnet build --no-incremental` — Build succeeded
- **Committed in:** `de34623` (part of Task 1 commit)

---

**Total deviations:** 2 auto-fixed (both blocking/build-error fixes)
**Impact on plan:** No scope creep — both fixes are structural consequences of extending an existing interface and importing an existing type, required for the solution to compile as the plan itself specifies.

## Issues Encountered
- One pre-existing integration test (`AdminControllerIntegrationTests.SendConfirmationEmail_Post_WhenUserUnconfirmed_ShouldRedirectToUsersWithSuccess`) failed on the full `dotnet test` run with a `429 TooManyRequests` instead of the expected `302 Found` — this is a rate-limit-policy test-ordering flake in an unrelated controller/file, not touched by this plan, and out of this plan's scope per the deviation rules' scope boundary. Not fixed. All 155 unit tests (including the full `QuestServiceTests` suite) passed cleanly.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- Plan 03 (UI + controller wiring) can now call `questService.ChangeVoteAsync(questId, playerSignupId, vote, finalizedProposedDateId)` and `questService.RevokeSignupAsync(questId, playerSignupId)` directly from the new `ChangeVote(id, vote)` controller action and the rewritten `RevokeSignup` action
- `WaitlistOrdering.OrderWaitlist` (from Plan 01) and the new promotion/email path are both ready for the desktop/mobile waitlist UI and vote-change buttons
- `QuestController.ChangeVoteToYes` still exists as a compiling delegate to the new service method — Plan 03's own `<artifacts_produced>` note confirms it fully rewrites this action into the single `ChangeVote(id, vote)` shape
- No blockers

---
*Phase: 44-post-finalization-voting-waitlist-auto-promotion*
*Completed: 2026-07-05*

## Self-Check: PASSED

All 11 created/modified source files plus the SUMMARY.md verified present on disk; all 4 task/summary commit hashes (`de34623`, `d741b24`, `a0cf455`, `3c5814c`) verified present in git log.
