---
phase: quick-260714-b0w
plan: 01
subsystem: quests
tags: [ef-core, razor, linq, waitlist, voting]

requires: []
provides:
  - Waitlist auto-promotion filtered to Yes/Maybe voters for the finalized date
  - Regression tests covering promotion eligibility (all-No, non-voter, No-skipped-Maybe-promoted)
  - Waitlist section on Quest Manage page (desktop + mobile) mirroring Quest Details
affects: [quest-signup, quest-manage, quest-details]

tech-stack:
  added: []
  patterns:
    - "Promotion eligibility filter mirrors seat-fill rule (Yes/Maybe grant a seat, No/none never does)"

key-files:
  created: []
  modified:
    - QuestBoard.Repository/PlayerSignupRepository.cs
    - QuestBoard.UnitTests/Repository/PlayerSignupRepositoryTests.cs
    - QuestBoard.Service/Views/Quest/Manage.cshtml
    - QuestBoard.Service/Views/Quest/Manage.Mobile.cshtml

key-decisions:
  - "Fixed promotion eligibility with a single .Where() filter in GetTopWaitlistedCandidateAsync rather than changing VoteType enum ordering or the display waitlist ordering, keeping the fix minimal and focused on the actual defect."
  - "Added a Waitlist section to Quest Manage (not in original symptom report but requested by user) so DMs can see who voted No while managing a finalized quest, matching the existing Details page pattern."

patterns-established:
  - "Waitlist display markup is duplicated per-view (Details/Details.Mobile/Manage/Manage.Mobile) rather than extracted to a partial, consistent with existing codebase convention."

requirements-completed: [quick-260714-b0w]

coverage:
  - id: D1
    description: "Waitlist auto-promotion only selects a player who voted Yes or Maybe for the finalized date; No voters and non-voters are never promoted."
    requirement: "quick-260714-b0w"
    verification:
      - kind: unit
        ref: "QuestBoard.UnitTests/Repository/PlayerSignupRepositoryTests.cs#GetTopWaitlistedCandidateAsync_AllNoVoters_ReturnsNull"
        status: pass
      - kind: unit
        ref: "QuestBoard.UnitTests/Repository/PlayerSignupRepositoryTests.cs#GetTopWaitlistedCandidateAsync_NonVoterForFinalizedDate_ReturnsNull"
        status: pass
      - kind: unit
        ref: "QuestBoard.UnitTests/Repository/PlayerSignupRepositoryTests.cs#GetTopWaitlistedCandidateAsync_SkipsNoVoter_PromotesMaybeVoter"
        status: pass
    human_judgment: false
  - id: D2
    description: "Existing vote-priority ordering (Yes outranks Maybe, tiebreak by LastVoteChangeTime/SignupTime) is preserved unchanged for eligible candidates."
    verification:
      - kind: unit
        ref: "QuestBoard.UnitTests/Repository/PlayerSignupRepositoryTests.cs#GetTopWaitlistedCandidateAsync_OrdersByVotePriorityThenTimestamp"
        status: pass
      - kind: unit
        ref: "QuestBoard.UnitTests/Repository/PlayerSignupRepositoryTests.cs#GetTopWaitlistedCandidateAsync_SameVote_OrdersByLastVoteChangeTimeFallingBackToSignupTime"
        status: pass
    human_judgment: false
  - id: D3
    description: "Quest Manage page (desktop and mobile) shows a Waitlist section for finalized quests, listing non-selected Player signups with their vote (Yes/Maybe/No/No Vote)."
    requirement: "quick-260714-b0w"
    verification: []
    human_judgment: true
    rationale: "Razor view rendering of a new UI section requires visual/manual confirmation in a browser against real quest data; not covered by unit tests."

duration: 4min
completed: 2026-07-14
status: complete
---

# Quick Task 260714-b0w: Waitlist No-Voter Promotion Bug Summary

**Fixed a one-line eligibility gap in waitlist auto-promotion that let No/non-voters fill freed seats, and added a Waitlist section to the DM-facing Quest Manage page (desktop + mobile) mirroring the existing Quest Details pattern.**

## Performance

- **Duration:** ~4 min
- **Started:** 2026-07-14T06:51:28Z
- **Completed:** 2026-07-14T06:55:48Z
- **Tasks:** 3
- **Files modified:** 4

## Accomplishments
- `GetTopWaitlistedCandidateAsync` now filters promotion candidates to Yes/Maybe voters for the finalized date before ranking, so a No voter or non-voter can no longer be auto-promoted into a freed seat (mirrors the existing seat-fill rule in `QuestService.ChangeVoteAsync`).
- Three new regression tests cover: an all-No waitlist returning null, a non-voter for the finalized date returning null, and a No voter being skipped in favor of a Maybe voter — all existing ordering tests remain green (308/308 total unit tests pass).
- Quest Manage (desktop `Manage.cshtml` and mobile `Manage.Mobile.cshtml`) now render a Waitlist table/card-list for finalized quests, showing each non-selected Player's name, character, and vote badge (including red "No"), giving DMs the same visibility that already existed on Quest Details.

## Task Commits

Each task was committed atomically:

1. **Task 1: Exclude No/non-voters from waitlist auto-promotion** - `050cd0b` (fix)
2. **Task 2: Add regression tests for promotion eligibility and run the suite** - `87c23dc` (test)
3. **Task 3: Add Waitlist section to Quest Manage page (desktop + mobile)** - `1cd476a` (feat)

**Plan metadata:** committed separately by the orchestrator after this summary.

## Files Created/Modified
- `QuestBoard.Repository/PlayerSignupRepository.cs` - Added a `.Where()` clause to `GetTopWaitlistedCandidateAsync` excluding candidates whose vote for the finalized date is not Yes or Maybe, with a plain-language comment explaining the rule.
- `QuestBoard.UnitTests/Repository/PlayerSignupRepositoryTests.cs` - Added `GetTopWaitlistedCandidateAsync_AllNoVoters_ReturnsNull`, `GetTopWaitlistedCandidateAsync_NonVoterForFinalizedDate_ReturnsNull`, `GetTopWaitlistedCandidateAsync_SkipsNoVoter_PromotesMaybeVoter`.
- `QuestBoard.Service/Views/Quest/Manage.cshtml` - Added `@using QuestBoard.Domain.Extensions` and a Waitlist table (desktop) rendered after Selected Participants for finalized quests.
- `QuestBoard.Service/Views/Quest/Manage.Mobile.cshtml` - Added `@using QuestBoard.Domain.Extensions` and an equivalent Waitlist card-list (mobile) matching the existing Selected Participants card style.

## Decisions Made
- Filtered candidates in-memory using the existing `VoteForProposedDate` helper rather than pushing the filter into the DB-side `.Where` predicate, to keep the change minimal and localized to the LINQ chain the plan specified.
- Preserved per-view markup duplication (no shared partial) for the new Waitlist section, consistent with how `Details.cshtml`/`Details.Mobile.cshtml`/`Manage.cshtml`/`Manage.Mobile.cshtml` already duplicate similar tables.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Fixed Razor `@{ }` code-block syntax error in both Manage views**
- **Found during:** Task 3 (Add Waitlist section to Quest Manage page)
- **Issue:** The plan's markup blocks used `@{ ... }` to declare `waitlistFinalizedProposedDateManage`/`waitlistPlayersManage`, but that insertion point in both `Manage.cshtml` and `Manage.Mobile.cshtml` is already inside an enclosing C# code block (`@if (Model.IsFinalized) { ... }`), where Razor requires plain `{ }` for a nested statement block — using `@{` there raises `RZ1010: Unexpected "{" after "@" character`.
- **Fix:** Removed the `@{`/`}` wrapper around the two variable declarations in both files, leaving them as plain C# statements inside the existing enclosing block (the subsequent `@if (waitlistPlayersManage.Any())` still needs its `@` since it's markup-transitioning).
- **Files modified:** QuestBoard.Service/Views/Quest/Manage.cshtml, QuestBoard.Service/Views/Quest/Manage.Mobile.cshtml
- **Verification:** `dotnet build QuestBoard.Service/QuestBoard.Service.csproj` succeeds with 0 errors after the fix (was previously RZ1010 x2).
- **Committed in:** 1cd476a (Task 3 commit)

---

**Total deviations:** 1 auto-fixed (1 bug)
**Impact on plan:** The fix was a pure Razor syntax correction with no change to the markup's visible output, ordering logic, or filter behavior specified in the plan. No scope creep.

## Issues Encountered
None beyond the Razor syntax deviation documented above.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- The waitlist promotion fix and new Manage waitlist view are both self-contained; no follow-up work is required.
- Per the plan's `<notes>`, any quest that already has a wrongly-promoted No voter (from before this fix) will not self-heal automatically — the DM can correct it manually via Remove Participant or Open Quest + re-finalize. No data migration was included, as intended.
- Manual sanity checks (finalize a quest with an all-No waitlist, free a seat, confirm no promotion; open Manage on a finalized quest with a waitlist to confirm the new section renders) are recommended before considering this fully verified in a live environment, per the plan's `<verification>` section.

---
*Phase: quick-260714-b0w*
*Completed: 2026-07-14*

## Self-Check: PASSED

All 4 modified source files and this SUMMARY.md exist on disk; all 3 task commits (050cd0b, 87c23dc, 1cd476a) verified present in `git log --oneline --all`.
