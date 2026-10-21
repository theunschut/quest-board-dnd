---
phase: 93-per-board-page-selection-and-home-page
plan: 14
subsystem: testing
tags: [board-pages, policy, routes, automapper, xunit, characterisation-tests]
requires:
  - phase: 93-01
    provides: BoardPage enum, BoardPagePolicy, BoardPageRoutes, EntityProfile Group mapping
provides:
  - Regression tests for the page rules, route table, toast sentences and storage contract
affects: [93-10]
tech-stack:
  added: []
  patterns: [exhaustive mask loop invariant tests, InMemory repository round-trip with real EntityProfile]
key-files:
  created:
    - QuestBoard.UnitTests/Services/BoardPagePolicyTests.cs
    - QuestBoard.UnitTests/Services/BoardPageRoutesTests.cs
    - QuestBoard.UnitTests/Repository/BoardPageSettingsMappingTests.cs
  modified:
    - QuestBoard.UnitTests/Services/EntityProfileEnumCastTests.cs
key-decisions:
  - "Characterisation tests passed against the plan 01 rules unchanged; no production code was modified"
  - "No assertion on IsSwitchable(Quests); the quest-list switch plan owns it"
requirements-completed: [D-01, D-05, D-09, D-10, D-18, D-19]
status: complete
duration: 10 min
completed: 2026-10-21
commits: 2
plan_head_before: ea940b94de16c837b9aa72134023388d4f4b72c2
actuals:
  tokens: 9500
  tasks: 2
  commits: 2
coverage:
  - deliverable: "Board type ceiling holds for every mask on a Campaign board"
    human_judgment: false
    verification:
      - kind: test
        ref: "QuestBoard.UnitTests/Services/BoardPagePolicyTests.cs#IsAvailable_CampaignBoard_NeverOffersRefusedPagesForAnyStoredMask"
        status: pass
  - deliverable: "Quest Log follows Quests without changing its stored bit"
    human_judgment: false
    verification:
      - kind: test
        ref: "QuestBoard.UnitTests/Services/BoardPagePolicyTests.cs#IsAvailable_QuestLog_ComesBackWhenQuestsIsSwitchedBackOnWithItsOwnBitUntouched"
        status: pass
  - deliverable: "EffectiveHome is always null or an available candidate across 2 x 512 x 9 combinations"
    human_judgment: false
    verification:
      - kind: test
        ref: "QuestBoard.UnitTests/Services/BoardPagePolicyTests.cs#EffectiveHome_ForEveryBoardTypeMaskAndStoredHome_IsNullOrAnAvailableCandidate"
        status: pass
  - deliverable: "Route table, both HomeRoute fallbacks and the nine toast sentences are exact"
    human_judgment: false
    verification:
      - kind: test
        ref: "QuestBoard.UnitTests/Services/BoardPageRoutesTests.cs"
        status: pass
  - deliverable: "Settings storage contract including the protected link column"
    human_judgment: false
    verification:
      - kind: test
        ref: "QuestBoard.UnitTests/Repository/BoardPageSettingsMappingTests.cs#UpdateAsync_WritesBothSettingsAndLeavesTheLinkedBoardSetAtItsSeededValue"
        status: pass
---

# Phase 93 Plan 14: Page Rules and Storage Contract Tests Summary

**Exhaustive unit tests pin the board page policy (type ceiling over all 512 masks, Quest Log dependency, home fallback invariant), the route and toast table, and the Group settings storage mapping that must never overwrite a board's link.**

## Performance

- **Duration:** about 10 min
- **Tasks:** 2 of 2
- **Files:** 3 created, 1 modified (all tests)

## Accomplishments

- `BoardPagePolicyTests`: type matrix, Campaign ceiling over every mask 0..511, Quest Log dependency and restoration, the home fallback chain, the EffectiveHome invariant over both board types, every mask and every stored home (never the DM profile, always an available candidate), labels, lock reasons and `BoardPageSet` round-trips.
- `BoardPageRoutesTests`: the eight candidate routes, the DM profile throw, `HomeRoute` for null availability, no available candidate (Account/Profile), stored Calendar and a switched-off stored home, and the nine exact toast sentences.
- `EntityProfileEnumCastTests.BoardPage_CastRoundTrips` covers all nine values.
- `BoardPageSettingsMappingTests`: entity to Group and back, defaults map to empty set and the quest list, and `GroupRepository.UpdateAsync` writes both ints while `LinkedBoardSetId` stays at its seeded value.

## Task Commits

1. Task 1: page rules, route table and toast sentences - `363ef3c5` (test)
2. Task 2: storage contract - `34172cb3` (test)

## Deviations from Plan

None - plan executed exactly as written. Every characterisation test passed against the plan 01 rules on the first run, so `BoardPagePolicy.cs` and `BoardPageRoutes.cs` were left unmodified. There was no RED phase because these tests describe behaviour that already exists.

## Verification

- `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~BoardPage|FullyQualifiedName~EntityProfileEnumCast"`: 136 passed, 0 failed.
- No added source line carries a planning reference (grep count 0 before each commit).
- Full `dotnet test` is left to the orchestrator after the wave.

## Known Stubs

None.

## Threat Flags

None. The tests mitigate T-93-47 and T-93-48 as planned.

## Self-Check: PASSED

- Created files exist: BoardPagePolicyTests.cs, BoardPageRoutesTests.cs, BoardPageSettingsMappingTests.cs
- Commits `363ef3c5` and `34172cb3` exist on branch `worktree-agent-p93-14-1792582949`
