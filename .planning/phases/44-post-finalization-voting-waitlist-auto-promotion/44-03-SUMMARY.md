---
phase: 44-post-finalization-voting-waitlist-auto-promotion
plan: 03
subsystem: ui
tags: [aspnet-mvc, razor, csharp, waitlist, voting]

# Dependency graph
requires:
  - phase: 44-02
    provides: "QuestService.ChangeVoteAsync/RevokeSignupAsync, promotion orchestration, singular-recipient promotion email pipeline"
provides:
  - "QuestController.ChangeVote(id, vote) HTTP endpoint replacing ChangeVoteToYes"
  - "RevokeSignup wired to questService.RevokeSignupAsync (promotion-aware)"
  - "Desktop Details.cshtml waitlist rendered via the shared OrderWaitlist extension, with Vote Yes/Maybe/No UI"
  - "Mobile Details.Mobile.cshtml waitlist section + Vote Yes/Maybe/No UI (new mobile parity)"
affects: []

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "changeVote(questId, vote) JS function (FormData + antiforgery token + numeric vote) shared verbatim between desktop and mobile, replacing changeVoteToYes"
    - "Waitlist ordering resolved once per view via OrderWaitlist(finalizedProposedDateId), with an explicit null-guarded fallback to the old OrderBy(SignupTime) only when the finalized proposed date can't be resolved"

key-files:
  created: []
  modified:
    - QuestBoard.Service/Controllers/QuestBoard/QuestController.cs
    - QuestBoard.Service/Views/Quest/Details.cshtml
    - QuestBoard.Service/Views/Quest/Details.Mobile.cshtml
    - QuestBoard.Domain/Services/QuestService.cs
    - QuestBoard.UnitTests/Services/QuestServiceTests.cs

key-decisions:
  - "Kept a defensive OrderBy(SignupTime)-only fallback for the rare case where the finalized proposed date can't be resolved (mirrors the controller's own 'Could not find the finalized date information' guard) — this is not a second live ordering path for the normal case, just a null-safety branch."

patterns-established:
  - "Vote Yes/Maybe/No buttons always render in a d-flex justify-content-between row with Revoke on the left, matching D-02 on both desktop and mobile."

requirements-completed: []  # Task 4 (human-verify checkpoint) not yet resolved — see below.

# Metrics
duration: (in progress — checkpoint pending)
completed: (pending)
status: in_progress
---

# Phase 44 Plan 03: Post-Finalization Vote UI + Controller Wiring Summary

**QuestController.ChangeVote(id, vote) replaces ChangeVoteToYes with capacity-free voting; desktop and mobile both render the waitlist via the shared OrderWaitlist extension with Vote Yes/Maybe/No buttons alongside Revoke — Task 4's human-verify checkpoint is next.**

## Performance

- **Started:** 2026-07-04T22:43:00Z (approx, worktree spawn)
- **Tasks:** 3 of 4 complete (Task 4 is a blocking human-verify checkpoint, not yet reached/resolved)
- **Files modified:** 3

## Accomplishments
- Replaced `QuestController.ChangeVoteToYes(int id)` with `ChangeVote(int id, VoteType vote)`: validates the vote enum with `Enum.IsDefined`, resolves only the caller's own signup (never a client-supplied signup id), and never rejects on capacity — delegates entirely to `questService.ChangeVoteAsync`
- Wired `RevokeSignup` to `questService.RevokeSignupAsync` so a revoke by a previously-selected player now triggers auto-promotion of the top waitlisted candidate
- Rewrote the desktop `Details.cshtml` waitlist to compute its order via the shared `WaitlistOrdering.OrderWaitlist` extension (vote priority, then last-vote-change time) instead of a plain `SignupTime` sort
- Replaced the desktop waitlist's single "Join Quest" action with three Vote Yes/Maybe/No buttons for the current user's own row, and added the same three buttons to the shared Revoke row (Revoke left, votes right) for both the selected-participant and waitlist cases
- Added a brand-new mobile waitlist section to `Details.Mobile.cshtml` built on the existing `participant-list-mobile`/`participant-row` stacked-card idiom (no `<table>`), consuming the identical `OrderWaitlist` ordering as desktop
- Added Vote Yes/Maybe/No buttons alongside Revoke on mobile (Revoke left, votes right), matching desktop layout and behavior
- Added a `changeVote(questId, vote)` JS function to both views (FormData + antiforgery token + numeric vote, POST `/Quest/ChangeVote/{id}`), removing the old `changeVoteToYes` function and its single-purpose endpoint call
- Full unit test suite (155 tests) green; the one integration test failure present (`AdminControllerIntegrationTests.SendConfirmationEmail_Post_WhenUserUnconfirmed_ShouldRedirectToUsersWithSuccess`, a pre-existing rate-limit-policy test-ordering flake also seen in Plan 02) is unrelated to any file this plan touches

## Task Commits

Each task was committed atomically:

1. **Task 1: Replace ChangeVoteToYes with ChangeVote(id, vote); wire RevokeSignup to promotion** - `96e2b8a` (feat)
2. **Task 2: Desktop Details.cshtml — centralized ordering + 3-button vote UI (D-01/D-02)** - `d31b631` (feat)
3. **Task 3: Mobile Details.Mobile.cshtml — new waitlist section + 3-button vote UI (D-05)** - `5344801` (feat)

_Task 4 (human-verify checkpoint) has not been reached/resolved yet — the plan is paused pending human verification. Plan metadata / docs commit is applied by the orchestrator after all wave agents complete, per worktree execution mode._

## Files Created/Modified
- `QuestBoard.Service/Controllers/QuestBoard/QuestController.cs` - `ChangeVote(int id, VoteType vote)` replaces `ChangeVoteToYes`; `RevokeSignup` now calls `questService.RevokeSignupAsync`
- `QuestBoard.Service/Views/Quest/Details.cshtml` - waitlist ordered via `OrderWaitlist`; Vote Yes/Maybe/No buttons added to the waitlist action cell and the shared Revoke row; `changeVote` JS replaces `changeVoteToYes`
- `QuestBoard.Service/Views/Quest/Details.Mobile.cshtml` - new waitlist section (stacked-card idiom) + Vote Yes/Maybe/No buttons alongside Revoke; `changeVote` JS added

## Decisions Made
- Kept a defensive `OrderBy(SignupTime)`-only fallback in both views for the rare null case where the finalized proposed date can't be resolved from `Quest.ProposedDates` — this branch mirrors the controller's own existing "Could not find the finalized date information" guard and is not a second live ordering path under normal operation (the `if (Model.Quest?.IsFinalized == true)` block guarantees `FinalizedDate` is set by the time this code runs).
- Applied the three vote buttons to both the selected-participants Revoke row and the waitlist action-cell row on desktop, matching D-02's instruction that both sections get vote buttons.

## Deviations from Plan

None - plan executed exactly as written for Tasks 1-3.

## Issues Encountered
- One pre-existing integration test (`AdminControllerIntegrationTests.SendConfirmationEmail_Post_WhenUserUnconfirmed_ShouldRedirectToUsersWithSuccess`) failed with `429 TooManyRequests` instead of `302 Found` on the full `dotnet test` run — a rate-limit-policy test-ordering flake in an unrelated controller, already documented as out-of-scope in Plan 02's SUMMARY.md. Not fixed here either; not caused by this plan's changes. All 155 unit tests passed cleanly.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- Task 4 (blocking human-verify checkpoint) is next: the human must run the app against a finalized, fully-seated One-Shot quest and exercise the Vote Yes/Maybe/No + waitlist + promotion flow on both desktop and mobile, then confirm the promotion email reaches only the promoted player.
- Per this project's own standing note in PROJECT.md, real-device/worktree checkpoints must be tested only after this plan's worktree branch is merged back into the working branch — the running dev server otherwise sees stale (pre-fix) code.
- No blockers for Tasks 1-3; all acceptance criteria for those tasks were verified against source before committing.

---
*Phase: 44-post-finalization-voting-waitlist-auto-promotion*
*Completed: (pending — Task 4 checkpoint outstanding)*

## Self-Check: PASSED

All 3 modified files verified present on disk with the expected content (`ChangeVote`, `OrderWaitlist`, `changeVote(` JS in each view as applicable); all 3 task commit hashes (`96e2b8a`, `d31b631`, `5344801`) verified present in `git log`.

## Behavior Extension — Round 2 of Task 4 Checkpoint (Maybe Can Also Fill an Open Seat)

During live testing of the Task 4 human-verify checkpoint, the tester reported: voting Maybe from a Yes-selected position kept them selected (expected), but voting Maybe while waitlisted (previously voted No) also kept them waitlisted, when they expected to move up to the selected table.

The orchestrator confirmed the original design intent with the user directly: the shipped behavior (only a Yes vote can fill an open seat; Maybe never promotes) was a deliberate symmetric read of VOTE-05's "Maybe never promotes" language, extended from "a selected player's Maybe never bumps someone else" to "Maybe never grants a seat at all." The user's explicit answer: **"Maybe can also fill an open seat"** — a waitlisted player voting Maybe should be auto-selected immediately if a seat is free, exactly like a Yes vote does today. Only a No vote (or no free seat) leaves them waitlisted. This is a genuine behavior extension beyond the original plan text, confirmed by the user mid-checkpoint — not a misreading to walk back.

**What changed:**
- `QuestBoard.Domain/Services/QuestService.cs` — `ChangeVoteAsync`'s seat-granting branch condition changed from `if (vote == VoteType.Yes)` to `if (vote == VoteType.Yes || vote == VoteType.Maybe)`. The fresh server-side re-fetch, `selectedCount < quest.TotalPlayerCount` capacity check, and "never trust client-supplied capacity" guard are all unchanged — only the vote value that can trigger the branch was widened. A `VoteType.No` vote still never enters this branch.
- **Explicitly unchanged:** the separate rule that a currently-*selected* player voting Maybe keeps their seat with no promotion of anyone else (VOTE-05's original scenario) — covered by the pre-existing `ChangeVoteAsync_SelectedPlayerVotesMaybe_DoesNotPromote` test, which still passes unmodified. That test exercises a different code path (the `seatFreed` / promotion branch driven by the repository's own vote-persistence return value), which this fix does not touch.
- `QuestBoard.UnitTests/Services/QuestServiceTests.cs` — added `ChangeVoteAsync_WaitlistedPlayerVotesMaybe_SelectsWhenSeatAvailable` (asserts a waitlisted player's Maybe vote selects them when a seat is free, mirroring the same repository-mock/assertion shape as the existing selection tests) and `ChangeVoteAsync_WaitlistedPlayerVotesNo_StaysWaitlistedEvenWithSeatAvailable` (asserts a No vote from a waitlisted player never triggers the select-if-room branch, even with capacity available).

**Verification:**
- `dotnet build --no-incremental` — Build succeeded, 0 warnings, 0 errors
- `dotnet test --filter FullyQualifiedName~QuestServiceTests` — 16/16 passed, including the two new tests and the unchanged `ChangeVoteAsync_SelectedPlayerVotesMaybe_DoesNotPromote`
- `dotnet test --no-build` (full suite) — 291/292 passed; the only failure is the same pre-existing `AdminControllerIntegrationTests.SendConfirmationEmail_Post_WhenUserUnconfirmed_ShouldRedirectToUsersWithSuccess` rate-limit-policy flake already documented in Plan 02's and this plan's original SUMMARY sections, unrelated to this change

**Task 4 checkpoint status:** Still outstanding — this fix must go through another round of human verification before the checkpoint can be marked resolved. Do not treat this SUMMARY update as closing Task 4.

**Commit:** `d26cf73` — `fix(44-03): let a Maybe vote also fill an open seat, not just Yes`
