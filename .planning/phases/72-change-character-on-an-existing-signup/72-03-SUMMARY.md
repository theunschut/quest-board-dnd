---
phase: 72-change-character-on-an-existing-signup
plan: 03
subsystem: ui
tags: [razor, bootstrap-modal, aspnet-core-mvc, integration-tests]

# Dependency graph
requires:
  - phase: 72-change-character-on-an-existing-signup/72-01
    provides: "UpdateSignupCharacter accepting any owned character, widened ViewBag.UserCharacters"
  - phase: 72-change-character-on-an-existing-signup/72-02
    provides: "_CharacterSelectModal.cshtml shared partial and CharacterDisplayExtensions.ToSelectLabel()"
provides:
  - "Desktop Details.cshtml renders the shared _CharacterSelectModal partial exactly once, replacing the old add-only modal"
  - "Change trigger (btn-primary, fa-edit) on the player's own row in both the finalized-participants and waitlist tables, guarded only by isCurrentUser"
  - "Empty-state add trigger retargeted to the shared modal with the same three data attributes"
  - "Both desktop signup-time character pickers (finalizedQuestCharacter, CharacterId) render options via ToSelectLabel()"
  - "QuestDetailsCharacterControlTests — 7-case integration test class pinning trigger identity, single-modal-instance, Retired labeling, and no cross-row leakage"
affects: [72-04-mobile-wiring]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Regex-extracted trigger tags in integration tests, rather than substring Contain checks, to bind data-current-character-id to the correct button regardless of attribute order"

key-files:
  created:
    - QuestBoard.IntegrationTests/Controllers/QuestDetailsCharacterControlTests.cs
  modified:
    - QuestBoard.Service/Views/Quest/Details.cshtml

key-decisions:
  - "Filled-state change trigger is guarded by isCurrentUser alone, deliberately with no check on the owned-character count — a character is already set, so there's always something to do (change it or clear it), and gating on count would lock a player out of clearing a Retired character"
  - "Empty-state add trigger keeps its existing isCurrentUser + non-empty-character-list condition unchanged, just retargeted to the shared modal"

patterns-established: []

requirements-completed: [SIGNCHAR-01, SIGNCHAR-03, SIGNCHAR-04]

coverage:
  - id: D1
    description: "Player's own row in both desktop tables (finalized-participants and waitlist) shows a change control immediately after the character name when a character is set, carrying that character's id"
    requirement: SIGNCHAR-01
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/QuestDetailsCharacterControlTests.cs#Details_Get_WhenOwnSignupHasCharacter_RendersTriggerCarryingThatCharacterId"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/QuestDetailsCharacterControlTests.cs#Details_Get_ForAWaitlistedSignupWithCharacter_RendersTheTrigger"
        status: pass
    human_judgment: false
  - id: D2
    description: "Change control renders even when the player's only character is Retired, with the status suffix visible on the trigger's label"
    requirement: SIGNCHAR-04
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/QuestDetailsCharacterControlTests.cs#Details_Get_WhenSignupHoldsARetiredCharacter_RendersTheTriggerCarryingThatCharacterId"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/QuestDetailsCharacterControlTests.cs#Details_Get_WhenSignupHasRetiredCharacterAndNoOtherCharacters_StillRendersTheChangeTrigger"
        status: pass
    human_judgment: false
  - id: D3
    description: "Empty-state add control still renders when no character is set and the player owns at least one character"
    requirement: SIGNCHAR-01
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/QuestDetailsCharacterControlTests.cs#Details_Get_WhenOwnSignupHasNoCharacter_RendersAddTriggerWithEmptyCurrentCharacterId"
        status: pass
    human_judgment: false
  - id: D4
    description: "The page renders exactly one shared modal instance, and no control leaks onto another player's row"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/QuestDetailsCharacterControlTests.cs#Details_Get_RendersTheSharedModalExactlyOnce"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/QuestDetailsCharacterControlTests.cs#Details_Get_ForAnotherPlayersRow_RendersNoTriggerForThatRow"
        status: pass
    human_judgment: false
  - id: D5
    description: "The two desktop signup-time pickers (join-quest panel, sign-up form) render every option through ToSelectLabel(), so Active labels stay byte-identical and non-Active characters show their status"
    requirement: SIGNCHAR-04
    verification:
      - kind: unit
        ref: "dotnet build (Razor compilation) + QuestBoard.IntegrationTests full-suite pass with no regressions"
        status: pass
    human_judgment: true
    rationale: "The visible rendered text for Active vs. Retired/Dead options in these two specific pickers is pinned by plan 02's ToSelectLabel unit tests and by full-suite regression passing, but no test in this plan renders the join-quest picker's actual option HTML for a mixed Active/Retired character set — that's a UAT item per the plan's own verification section."
  - id: D6
    description: "Opening the modal on a signup holding a Retired character shows that character pre-selected with its status suffix, the confirm-guarded Remove flow works, and success toasts appear after swap/clear"
    verification: []
    human_judgment: true
    rationale: "Explicitly called out in the plan's <verification> section as manual verification items that cannot honestly be asserted from markup alone (browser-rendered modal state, confirm() dialog, toast display) — belongs to UAT, not this executor's automated pass."

# Metrics
duration: 15min
completed: 2026-08-25
status: complete
---

# Phase 72 Plan 03: Desktop Character Control Wiring Summary

**Wired the shared character-select modal into the desktop Details.cshtml: a change trigger on the player's own row in both the finalized and waitlist tables, the empty-state add button retargeted to the same modal, both signup-time pickers routed through `ToSelectLabel()`, and a 7-case integration test class pinning the trigger contract.**

## Performance

- **Duration:** 15 min
- **Started:** 2026-08-25T15:30:00Z (approx.)
- **Completed:** 2026-08-25T15:44:55Z
- **Tasks:** 3
- **Files modified:** 2 (1 new, 1 modified)

## Accomplishments
- Deleted the old add-only `addCharacterModal`/`addCharacterForm` block from `Details.cshtml` and replaced it with a single `Html.RenderPartialAsync("_CharacterSelectModal")` call, so the desktop page now renders exactly one modal instance
- Added a `btn-sm btn-primary` change trigger (fa-edit icon) immediately after the character name in both the finalized-participants cell and the waitlist cell, guarded only by `isCurrentUser` — present whenever a character is set, including a sole Retired one
- Retargeted the existing green empty-state add button in both cells to `#characterSelectModal` with the three trigger data attributes (`data-quest-id`, empty `data-current-character-id`/`data-current-character-label`), keeping its own condition and styling unchanged
- Replaced the inline class-list/name/level string building in the `finalizedQuestCharacter` and `CharacterId` signup-time pickers with `@character.ToSelectLabel()`, deleting the now-unused local `classList` variables
- New `QuestDetailsCharacterControlTests` integration test class (7 test methods) pinning: own-signup trigger identity, empty-state add trigger, single modal instance, Retired-character label suffix, sole-Retired-character still showing the change trigger, waitlist row wiring, and no leakage of another player's character id onto the viewer's page
- Full test suite green: 313 unit tests, 419 integration tests (412 + 7 new), zero regressions

## Task Commits

Each task was committed atomically:

1. **Task 1: Replace the add-only modal with the shared partial and add the change trigger to both character cells** - `c936af3` (feat)
2. **Task 2: Route the two desktop signup-time pickers through the shared label** - `5c9d37e` (refactor)
3. **Task 3: Pin the desktop trigger markup with integration tests** - `3c54f2a` (test)

_Note: worktree mode — the orchestrator applies the final `docs` metadata commit after merge._

## Files Created/Modified
- `QuestBoard.Service/Views/Quest/Details.cshtml` - removed the add-only modal block, rendered the shared `_CharacterSelectModal` partial, added change triggers to both character cells, retargeted both empty-state add buttons, routed both signup-time pickers through `ToSelectLabel()`
- `QuestBoard.IntegrationTests/Controllers/QuestDetailsCharacterControlTests.cs` - new 7-case integration test class covering the desktop trigger contract

## Decisions Made
- Followed the plan's action blocks verbatim: filled-state trigger guarded by `isCurrentUser` alone (no character-count gate), empty-state trigger keeps its existing count-gated condition
- Test assertions extract trigger `<button>` tags via regex bound to `data-bs-target="#characterSelectModal"` rather than doing loose substring `Contain` checks on the raw HTML, so a `data-current-character-id` assertion is provably tied to the correct trigger rather than any attribute occurring anywhere on the page

## Deviations from Plan

None - plan executed exactly as written. All acceptance criteria and verification commands passed on first attempt for each task; no auto-fixes were required.

## Issues Encountered

None.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- The desktop Details page now fully exercises the shared modal/label contract established in plan 02, on top of the server contract from plan 01
- Plan 04 (mobile wiring) can follow the same pattern against `Details.Mobile.cshtml`'s `#finalizedQuestCharacterMobile` picker (read site 6 in the phase's read-site ledger), independently of this plan's changes
- Manual UAT items remain open per the plan's own `<verification>` section: visual placement/sizing of the change control in both tables, pre-selected Retired character with status suffix on modal open, confirm-guarded Remove, and success toasts after swap/clear — none of these are honestly assertable from markup alone
- No blockers or concerns carried forward

---
*Phase: 72-change-character-on-an-existing-signup*
*Completed: 2026-08-25*

## Self-Check: PASSED

- FOUND: QuestBoard.Service/Views/Quest/Details.cshtml
- FOUND: QuestBoard.IntegrationTests/Controllers/QuestDetailsCharacterControlTests.cs
- FOUND: c936af3 (Task 1 commit)
- FOUND: 5c9d37e (Task 2 commit)
- FOUND: 3c54f2a (Task 3 commit)
