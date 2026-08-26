---
phase: 72-change-character-on-an-existing-signup
plan: 01
subsystem: api
tags: [aspnet-core-mvc, ef-core, integration-tests, quest-signup]

# Dependency graph
requires: []
provides:
  - "UpdateSignupCharacter accepts any owned character (Active, Retired, or Dead) and rejects only on ownership/board mismatch"
  - "ViewBag.UserCharacters carries every status from its single writer in Details GET"
  - "TempData Success/Error toast feedback on the character-change save path"
  - "13-case integration test class pinning all server-side behaviours this phase changes"
affects: [72-02, 72-03, 72-04]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Widen-at-single-writer: ViewBag.UserCharacters populated once in Details GET, unfiltered, feeding all downstream read sites"
    - "Split failure-path idiom: reachable-without-tampering failures become TempData[Error] + redirect; failures unreachable through legitimate UI interaction stay hard BadRequest"

key-files:
  created:
    - QuestBoard.IntegrationTests/Controllers/QuestUpdateSignupCharacterTests.cs
  modified:
    - QuestBoard.Service/Controllers/QuestBoard/QuestController.cs
    - QuestBoard.IntegrationTests/Helpers/TestDataHelper.cs

key-decisions:
  - "Dropped the CharacterStatus.Active gate entirely from UpdateSignupCharacter — ownership and board scope are the only remaining gates, per D-10"
  - "Kept the explicit board-id comparison in UpdateSignupCharacter as defense-in-depth even though the entity's global query filter already rejects cross-board characters (research-corrected: no live hole existed)"
  - "No-signup path redirects with TempData[Error] instead of returning a raw 400 body; cross-board rejection stays a hard BadRequest so its regression test asserts on an actual rejection"

patterns-established:
  - "Cross-board integration tests seed the second board's rows via factory.Database.CreateContext() (ActiveGroupId=null, bypasses the read filter) rather than the DI-scoped context"

requirements-completed: [SIGNCHAR-01, SIGNCHAR-03, SIGNCHAR-04, SIGNCHAR-05, SIGNCHAR-06, SIGNCHAR-07]

coverage:
  - id: D1
    description: "A player can swap their signup to a different Active character they own, and the change persists"
    requirement: SIGNCHAR-01
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/QuestUpdateSignupCharacterTests.cs#UpdateSignupCharacter_Post_WithDifferentActiveCharacter_UpdatesSignupCharacterId"
        status: pass
    human_judgment: false
  - id: D2
    description: "Posting with no characterId field clears the signup's CharacterId to null"
    requirement: SIGNCHAR-03
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/QuestUpdateSignupCharacterTests.cs#UpdateSignupCharacter_Post_WithNoCharacterIdField_ClearsSignupCharacterIdToNull"
        status: pass
    human_judgment: false
  - id: D3
    description: "Retired and Dead characters owned by the caller are valid selections and persist, including resubmitting the current one"
    requirement: SIGNCHAR-04
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/QuestUpdateSignupCharacterTests.cs#UpdateSignupCharacter_Post_WithRetiredCharacter_AssignsIt"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/QuestUpdateSignupCharacterTests.cs#UpdateSignupCharacter_Post_WithDeadCharacter_AssignsIt"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/QuestUpdateSignupCharacterTests.cs#UpdateSignupCharacter_Post_ResubmittingTheCurrentRetiredCharacter_LeavesItAssigned"
        status: pass
    human_judgment: false
  - id: D4
    description: "The change succeeds on a finalized quest with no date-based cutoff"
    requirement: SIGNCHAR-05
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/QuestUpdateSignupCharacterTests.cs#UpdateSignupCharacter_Post_OnFinalizedQuest_UpdatesSignupCharacterId"
        status: pass
    human_judgment: false
  - id: D5
    description: "The change succeeds for a waitlisted signup and for all three signup roles (Player, Spectator, AssistantDM)"
    requirement: SIGNCHAR-06
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/QuestUpdateSignupCharacterTests.cs#UpdateSignupCharacter_Post_ForWaitlistedSignup_UpdatesSignupCharacterId"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/QuestUpdateSignupCharacterTests.cs#UpdateSignupCharacter_Post_ForEachSignupRole_UpdatesSignupCharacterId"
        status: pass
    human_judgment: false
  - id: D6
    description: "A character owned by a different user in the same board, and a character on a different board, are both rejected with 400 and leave the signup's character unchanged"
    requirement: SIGNCHAR-07
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/QuestUpdateSignupCharacterTests.cs#UpdateSignupCharacter_Post_WithAnotherUsersCharacterInSameBoard_ReturnsBadRequestAndLeavesCharacterUnchanged"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/QuestUpdateSignupCharacterTests.cs#UpdateSignupCharacter_Post_WithCharacterFromAnotherBoard_ReturnsBadRequestAndLeavesCharacterUnchanged"
        status: pass
    human_judgment: false
  - id: D7
    description: "A successful change or clear sets TempData[Success]; a caller with no signup redirects with TempData[Error] instead of a raw 400 body; ViewBag.UserCharacters widened at its single writer"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/QuestUpdateSignupCharacterTests.cs#UpdateSignupCharacter_Post_WhenCallerHasNoSignupOnTheQuest_RedirectsToDetailsWithoutTouchingAnySignup"
        status: pass
    human_judgment: false

# Metrics
duration: 12min
completed: 2026-08-25
status: complete
---

# Phase 72 Plan 01: Server-Side Character Change Contract Summary

**Widened `ViewBag.UserCharacters` at its single writer and reworked `UpdateSignupCharacter` to drop the Active-only gate, add explicit board-scope defense-in-depth, split its two failure paths (friendly redirect vs. hard rejection), and set TempData toast feedback — locked behind a new 13-case integration test class.**

## Performance

- **Duration:** 12 min
- **Started:** 2026-08-25T13:20:00Z (approx)
- **Completed:** 2026-08-25T13:29:14Z
- **Tasks:** 3
- **Files modified:** 3 (1 new, 2 modified)

## Accomplishments
- `TestDataHelper.CreatePlayerSignupAsync` gained a trailing `int? characterId` parameter, letting tests seed a signup that already holds a character
- New `QuestUpdateSignupCharacterTests` integration test class (13 test cases across 11 methods, one a 3-case Theory) covering every server-side behaviour this phase changes
- `QuestController.Details` GET now assigns `ViewBag.UserCharacters` without narrowing by status — one writer, six read sites, all statuses
- `QuestController.UpdateSignupCharacter` drops the `CharacterStatus.Active` requirement, keeps ownership as a gate, and adds an explicit `character.GroupId` vs. `activeGroupContext.ActiveGroupId` comparison as insurance against a future query-filter bypass
- The no-signup failure path now sets `TempData["Error"]` and redirects to `Details` instead of returning a raw 400 body; the cross-board/cross-owner failure path stays a hard `BadRequest`
- Successful swap/clear sets `TempData["Success"]` with distinct wording for each case
- Full test suite green after each task: 308 unit tests, 412 integration tests

## Task Commits

Each task was committed atomically:

1. **Task 1: Add the character-bearing signup fixture and the baseline UpdateSignupCharacter test class** - `c6952cd` (test)
2. **Task 2: Widen the character list at its single writer and rework UpdateSignupCharacter** - `580fddf` (feat)
3. **Task 3: Cover the newly changed server behaviours, including both halves of the isolation requirement** - `de70110` (test)

**Plan metadata:** (this commit, follows)

## Files Created/Modified
- `QuestBoard.IntegrationTests/Controllers/QuestUpdateSignupCharacterTests.cs` - New 13-case integration test class covering swap, clear, finalized quest, waitlist, all three signup roles, same-board cross-owner rejection, Retired/Dead assignment, no-op resubmit, no-signup redirect, and cross-board rejection
- `QuestBoard.IntegrationTests/Helpers/TestDataHelper.cs` - `CreatePlayerSignupAsync` gained a trailing optional `characterId` parameter
- `QuestBoard.Service/Controllers/QuestBoard/QuestController.cs` - `Details` GET character-list widening; `UpdateSignupCharacter` status-gate removal, explicit board check, split failure responses, TempData success feedback

## Decisions Made
- Followed the plan's `research_correction_notice`: the explicit board-id comparison in `UpdateSignupCharacter` is documented as defense-in-depth insurance (the entity's global `HasQueryFilter` already rejects cross-board characters), not as closing a live hole — matching the plan's framing and CLAUDE.md's Code Comments rule (no tracking IDs in source)
- Both required plain-language comments were written exactly as specified: one explaining the board check as insurance against a future `IgnoreQueryFilters` bypass, one explaining why the no-signup path gets a friendly redirect while the cross-board path stays a hard rejection

## Deviations from Plan

None - plan executed exactly as written. All acceptance criteria and verification commands from the plan passed on first attempt for each task; no auto-fixes were required.

## Issues Encountered
None.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- `UpdateSignupCharacter` and `ViewBag.UserCharacters` now present a known-good, fully-tested contract: inactive characters are assignable, clearing persists null, ownership/board violations are rejected, and TempData toast keys are set on both success and the no-signup failure path
- Plans 02-04 (desktop and mobile view wiring) can build the pencil-icon trigger, the shared `_CharacterSelectModal.cshtml` partial, and the Remove-character button against this server contract with no further server-side changes expected
- No blockers or concerns carried forward

---
*Phase: 72-change-character-on-an-existing-signup*
*Completed: 2026-08-25*

## Self-Check: PASSED

- FOUND: QuestBoard.IntegrationTests/Controllers/QuestUpdateSignupCharacterTests.cs
- FOUND: .planning/phases/72-change-character-on-an-existing-signup/72-01-SUMMARY.md
- FOUND commit: c6952cd (Task 1)
- FOUND commit: 580fddf (Task 2)
- FOUND commit: de70110 (Task 3)
