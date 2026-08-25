---
phase: 72-change-character-on-an-existing-signup
plan: 04
subsystem: ui
tags: [razor, mobile, bootstrap-modal, integration-tests]

requires:
  - phase: 72-change-character-on-an-existing-signup
    provides: "UpdateSignupCharacter server contract, ViewBag.UserCharacters widening (72-01)"
  - phase: 72-change-character-on-an-existing-signup
    provides: "_CharacterSelectModal.cshtml shared partial and ToSelectLabel() (72-02)"
provides:
  - "Details.Mobile.cshtml inline change/add triggers on both participant and waitlist rows"
  - "Details.Mobile.cshtml renders the shared _CharacterSelectModal partial exactly once"
  - "Mobile join-quest picker routed through ToSelectLabel(), closing the sixth and final reader of the character list"
  - "QuestDetailsMobileCharacterControlTests — 7-case real-User-Agent integration proof that the mobile view is genuinely selected and serves the trigger contract"
affects: []

tech-stack:
  added: []
  patterns:
    - "Inline subordinate trigger inside a small element (p-0/border-0/lh-1/align-baseline/fa-xs) to add a control without growing the line box — the mobile-specific styling constraint that keeps participant-row height unchanged"

key-files:
  created:
    - QuestBoard.IntegrationTests/Mobile/QuestDetailsMobileCharacterControlTests.cs
  modified:
    - QuestBoard.Service/Views/Quest/Details.Mobile.cshtml

key-decisions:
  - "Followed the plan's exact trigger markup and class list verbatim (p-0/border-0/lh-1/align-baseline/fa-xs) rather than reusing desktop's btn-sm btn-primary shape, since the plan documents this as load-bearing for row-height stability"
  - "Test file mirrors MobileViewsTests.cs conventions exactly: HttpRequestMessage + TryAddWithoutValidation for User-Agent, Authorization header copied off an authenticated client, TestContext.Current.CancellationToken on every async call"
  - "Modal-instance-count test uses a regex on the literal `id=\"characterSelectModal\"` attribute rather than a substring containment check, since `data-bs-target=\"#characterSelectModal\"` also contains that substring and would inflate a naive count"

patterns-established: []

requirements-completed: [SIGNCHAR-01, SIGNCHAR-02, SIGNCHAR-03, SIGNCHAR-04]

coverage:
  - id: D1
    description: "Both mobile row types (participant and waitlist) carry an inline pencil/plus change control on the viewer's own row, subordinate to the character-name text, with no third column and no row-height change"
    requirement: SIGNCHAR-02
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Mobile/QuestDetailsMobileCharacterControlTests.cs#MobileDetails_MobileUserAgent_WhenOwnSignupHasCharacter_RendersTriggerCarryingThatCharacterId"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Mobile/QuestDetailsMobileCharacterControlTests.cs#MobileDetails_MobileUserAgent_WhenOwnSignupHasNoCharacter_RendersAddTriggerWithEmptyCurrentCharacterId"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Mobile/QuestDetailsMobileCharacterControlTests.cs#MobileDetails_MobileUserAgent_ForAWaitlistedSignupWithCharacter_RendersTheTrigger"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Mobile/QuestDetailsMobileCharacterControlTests.cs#MobileDetails_MobileUserAgent_ForAnotherPlayersRow_RendersNoTriggerForThatRow"
        status: pass
    human_judgment: true
    rationale: "Row-height stability, baseline alignment, and the confirm/toast UX are visual/browser properties this plan's automated tests cannot assert on raw HTML; they remain UAT items as the plan's own verification block states."
  - id: D2
    description: "The mobile page renders one shared _CharacterSelectModal instance regardless of participant/waitlist row count, using the same trigger attribute vocabulary as desktop"
    requirement: SIGNCHAR-01
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Mobile/QuestDetailsMobileCharacterControlTests.cs#MobileDetails_MobileUserAgent_RendersTheSharedModalExactlyOnce"
        status: pass
    human_judgment: false
  - id: D3
    description: "A Retired/Dead character owned by the caller is a valid trigger target on mobile and its label carries the status suffix, matching the desktop pickers"
    requirement: SIGNCHAR-04
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Mobile/QuestDetailsMobileCharacterControlTests.cs#MobileDetails_MobileUserAgent_WhenSignupHoldsARetiredCharacter_RendersTheTriggerCarryingThatCharacterId"
        status: pass
    human_judgment: false
  - id: D4
    description: "The mobile view is genuinely selected and served for a real mobile User-Agent, and a desktop User-Agent does not receive it — the regression this repo has hit before"
    requirement: SIGNCHAR-03
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Mobile/QuestDetailsMobileCharacterControlTests.cs#MobileDetails_MobileUserAgentSelectsTheMobileView_AndDesktopUserAgentDoesNot"
        status: pass
    human_judgment: false

duration: 20min
completed: 2026-08-25
status: complete
---

# Phase 72 Plan 04: Mobile Character Change Wiring Summary

**Wired `Details.Mobile.cshtml`'s participant and waitlist rows to the shared `_CharacterSelectModal` partial via subordinate pencil/plus triggers, routed the join-quest picker through `ToSelectLabel()`, and pinned the whole contract with 7 real-User-Agent integration tests proving the mobile view is actually served.**

## Performance

- **Duration:** 20 min (approx)
- **Started:** 2026-08-25T13:24:00Z (approx.)
- **Completed:** 2026-08-25T13:44:13Z
- **Tasks:** 3
- **Files modified:** 2 (1 new, 1 modified)

## Accomplishments
- Both the participant row and the waitlist row on the mobile Details page now render an inline change/add trigger next to the character name, guarded by `isCurrentUser` alone for the filled state and by `isCurrentUser` plus a non-empty character list for the empty state — the exact `p-0 border-0 align-baseline lh-1 ms-2` class list keeps the small-element line box height unchanged
- `Details.Mobile.cshtml` renders `_CharacterSelectModal` exactly once, at the end of the body markup, sharing the same `data-quest-id`/`data-current-character-id`/`data-current-character-label` trigger contract the desktop page uses (verified: `grep -oE 'data-[a-z-]*'` yields exactly those five attribute names, no second dialect)
- The mobile join-quest picker (`finalizedQuestCharacterMobile`) now builds option text via `character.ToSelectLabel()`, deleting the inline class-list-building local and closing the sixth and final reader of the single-writer character list
- New `QuestDetailsMobileCharacterControlTests` (7 cases, all real `HttpRequestMessage` + explicit User-Agent header, no bare `GetAsync`): the guard test proving mobile vs. desktop view selection, filled/empty trigger states, Retired-character labeling, waitlist-row parity, exactly-one-modal-instance, and isolation from another player's row
- Full suite green after every task: 313 unit tests, 419 integration tests (412 pre-existing + 7 new)

## Task Commits

Each task was committed atomically:

1. **Task 1: Add the inline change trigger to both mobile row types and render the shared modal once** - `b9e487e` (feat)
2. **Task 2: Route the mobile signup-time picker through the shared label** - `7b46f82` (feat)
3. **Task 3: Prove the mobile markup actually renders, with real User-Agent integration tests** - `5d438ab` (test)

_Note: worktree mode — these are the task commits for this plan's work; the orchestrator applies the final `docs` metadata commit after merge._

## Files Created/Modified
- `QuestBoard.Service/Views/Quest/Details.Mobile.cshtml` - Inline change/add triggers on participant and waitlist rows, single shared-modal render call, join-quest picker routed through `ToSelectLabel()`
- `QuestBoard.IntegrationTests/Mobile/QuestDetailsMobileCharacterControlTests.cs` - 7-case integration test class proving the mobile trigger contract renders through the real platform-selection path

## Decisions Made
- Used the plan's exact trigger markup and class list verbatim rather than desktop's `btn btn-sm btn-primary` shape — the plan documents `p-0`/`border-0`/`lh-1`/`align-baseline`/`fa-xs` as load-bearing for keeping the participant-row height unchanged inside the small element
- Test file follows `MobileViewsTests.cs` conventions exactly (User-Agent constants, `HttpRequestMessage` + `TryAddWithoutValidation`, Authorization header copied off `AuthenticationHelper.CreateAuthenticatedClientWithUserAsync`, `TestContext.Current.CancellationToken` on every async call) so it reads as one family of mobile tests rather than a second style
- The exactly-one-modal-instance test counts the literal `id="characterSelectModal"` attribute via regex rather than a containment check, since the trigger's `data-bs-target="#characterSelectModal"` also contains that substring and would otherwise inflate the count
- Because plan 03 (desktop wiring) runs in the same wave and had not yet landed in this worktree at execution time, the trigger markup was built directly from this plan's own explicit action block and the frozen `_CharacterSelectModal.cshtml` contract from plan 02, rather than by copying live desktop markup — the plan's `<ordering_note>` establishes the contract is already frozen at the partial, so both host views wire against it independently

## Deviations from Plan

None - plan executed exactly as written. All acceptance criteria (grep-based markup checks, `dotnet build`, filtered and full `dotnet test` runs) passed on first attempt for each task.

## Issues Encountered

None.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- All six readers of `ViewBag.UserCharacters` on the Details page (desktop signup-time, desktop finalized-quest join, mobile join, plus the participant/waitlist trigger labels on both platforms) are now on `ToSelectLabel()`, so no picker or trigger label can describe the same character differently.
- Both platforms wire triggers against the same frozen `_CharacterSelectModal.cshtml` contract (`data-quest-id`, `data-current-character-id`, `data-current-character-label`, `#characterSelectModal`) — no mobile-only attribute, modal, or script was introduced.
- Manual UAT items remain, as documented in the plan's `<verification>` block: before/after row-height screenshot comparison, confirm-dialog-before-Remove behavior, success-toast visibility after scroll, and verification via a real mobile User-Agent rather than browser device-toolbar emulation.
- No blockers or concerns carried forward. This plan does not touch `Details.cshtml` or plan 03's test file, per the wave's disjoint-file-set requirement.

---
*Phase: 72-change-character-on-an-existing-signup*
*Completed: 2026-08-25*
