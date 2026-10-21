---
phase: 93-per-board-page-selection-and-home-page
plan: 07
subsystem: ui
tags: [razor, aspnet-mvc, board-settings, signup, characters]

requires:
  - phase: 93-01
    provides: IBoardPageResolver, BoardPage enum, BoardPageSeed
  - phase: 93-02
    provides: BoardPage gate on QuestController and Characters gate on UpdateSignupCharacter
  - phase: 93-03
    provides: BoardPageAvailabilityExtensions.Serves and the BoardPages view injection
provides:
  - QuestController refuses any character on a signup while Characters is not served
  - Quest Details (both layouts) hides every character picker, trigger, modal and join script when Characters is off
  - Players page (both layouts) lists DM names as plain text when the DM profile is off
affects: [93-12]

actuals:
  tokens: 7000
  tasks: 2
  commits: 4
plan_head_before: 7c452094929a9d1e64472f83d498955cba48d94e

tech-stack:
  added: []
  patterns:
    - "Hidden controls render nothing: the wrapping condition covers the element, its label and the inline script that wires it"
    - "A server-side refusal through the existing validation path backs every hidden picker, so a stale form shows a message instead of saving something else"

key-files:
  created:
    - QuestBoard.IntegrationTests/Controllers/BoardPageCharacterLinksTests.cs
  modified:
    - QuestBoard.Service/Controllers/QuestBoard/QuestController.cs
    - QuestBoard.Service/Views/Quest/Details.cshtml
    - QuestBoard.Service/Views/Quest/Details.Mobile.cshtml
    - QuestBoard.Service/Views/Players/Index.cshtml
    - QuestBoard.Service/Views/Players/Index.Mobile.cshtml

key-decisions:
  - "Serves, not Allows: the pickers and the DM link had no board-type check, so they keep rendering in the unresolved state"
  - "A crafted character id is refused through the existing 'Invalid character selection.' redirect, not silently dropped"
  - "Both the add and the change character buttons are hidden, not only the add buttons the plan named"

patterns-established:
  - "Render tests read only the page body after the main element so menu links cannot pass for page content"

requirements-completed: [D-07, D-09]

coverage:
  - id: D1
    description: "With Characters off, a crafted CharacterId on the open signup or the finalized join is refused with 'Invalid character selection.' and no signup carries that character"
    requirement: "D-07"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/BoardPageCharacterLinksTests.cs#CharactersOff_OpenSignupPostWithOwnCharacter_IsRefusedAndNoSignupCarriesIt"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/BoardPageCharacterLinksTests.cs#CharactersOff_FinalizedJoinWithOwnCharacter_IsRefusedAndNoSignupCarriesIt"
        status: pass
    human_judgment: false
  - id: D2
    description: "With Characters off, signing up or joining without a character still works, the change-character POST is refused leaving the signup untouched, and with Characters on the same POST attaches the character"
    requirement: "D-07"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/BoardPageCharacterLinksTests.cs#CharactersOff_OpenSignupPostWithoutCharacter_CreatesSignupWithNoCharacter"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/BoardPageCharacterLinksTests.cs#CharactersOff_UpdateSignupCharacter_IsRefusedAndTheSignupKeepsItsCharacter"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/BoardPageCharacterLinksTests.cs#CharactersOn_OpenSignupPostWithOwnCharacter_AttachesIt"
        status: pass
    human_judgment: false
  - id: D3
    description: "With Characters off, Quest Details on desktop and mobile renders no character select, add/change button, modal or finalized-join script; existing character names and desktop portraits still render"
    requirement: "D-07"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/BoardPageCharacterLinksTests.cs#CharactersOff_FinalizedJoin_OffersNoCharacterChoiceAndEmitsNoScriptForIt"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/BoardPageCharacterLinksTests.cs#CharactersOff_ExistingSignupWithCharacter_KeepsNameAndPortraitButOffersNoChange"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/BoardPageCharacterLinksTests.cs#CharactersOff_OpenQuest_OffersNoCharacterSelectOrModal"
        status: pass
    human_judgment: false
  - id: D4
    description: "With Characters on every picker, trigger, script and modal renders as before on both layouts"
    requirement: "D-07"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/BoardPageCharacterLinksTests.cs#CharactersOn_FinalizedJoin_OffersTheSelectItsScriptAndTheModal"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/BoardPageCharacterLinksTests.cs#CharactersOn_ExistingSignupWithCharacter_OffersTheChangeButtonAndModal"
        status: pass
    human_judgment: false
  - id: D5
    description: "With the DM profile off the Players page shows DM names as plain text (no link, onclick or chevron) on both layouts; with it on the links are present"
    requirement: "D-09"
    verification:
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/BoardPageCharacterLinksTests.cs#DungeonMasterProfileOff_PlayersPage_ListsDungeonMasterNamesAsPlainText"
        status: pass
      - kind: integration
        ref: "QuestBoard.IntegrationTests/Controllers/BoardPageCharacterLinksTests.cs#DungeonMasterProfileOn_PlayersPage_LinksDungeonMasterNamesToTheirProfile"
        status: pass
    human_judgment: false

duration: 35min
completed: 2026-10-21
status: complete
---

# Phase 93 Plan 07: Hidden character pickers and plain-text DM names Summary

**A board without Characters offers no character choice anywhere on Quest Details (both layouts) and the server refuses any character id posted to a signup, while a board without the DM profile lists DM names on the Players page as plain text.**

## Performance

- **Duration:** about 35 min
- **Completed:** 2026-10-21
- **Tasks:** 2
- **Files modified:** 6 (1 created, 5 modified)

## Accomplishments
- `QuestController` gains an `IBoardPageResolver` dependency; `ResolveCharacterAssignmentAsync` returns `NotAssignable` while Characters is not served, so the open signup, the finalized join and the change-character path all refuse through "Invalid character selection."
- Quest Details desktop and mobile: signup select, finalized-join select with its label, wrapper and inline script, every add and change character button, and the `_CharacterSelectModal` partial are not rendered with Characters off. The mobile hidden `characterId` inputs stay and post empty. Character names, portraits and "No character" text stay.
- Players desktop: DM name is plain text in the cell. Players mobile: the DM row renders as `players-row no-link` with `players-name`, no `onclick`, no anchor, no chevron.
- `BoardPageCharacterLinksTests`: 25 cases (server refusals, render facts as theories over a desktop and a mobile user agent, positive controls with the pages on).

## Task Commits

1. **Task 1: The server refuses a character on a signup while Characters is off**
   - RED: `9fd92670` (test) - the two crafted-POST tests failed on the missing "Invalid character selection." message
   - GREEN: `2997569d` (feat)
2. **Task 2: Quest Details hides the character pickers and Players shows DM names as text**
   - RED: `ea8f85e4` (test) - 10 of the 25 cases failed (every "off" render case); the "on" controls and Task 1 cases passed
   - GREEN: `0d6ef6ef` (feat)

**Plan metadata:** the docs commit carrying this file.

## Files Created/Modified
- `QuestBoard.Service/Controllers/QuestBoard/QuestController.cs` - resolver dependency and the Characters check
- `QuestBoard.Service/Views/Quest/Details.cshtml` and `Details.Mobile.cshtml` - `charactersOn` gates pickers, triggers, script and modal
- `QuestBoard.Service/Views/Players/Index.cshtml` and `Index.Mobile.cshtml` - DM name as plain text when the DM profile is off
- `QuestBoard.IntegrationTests/Controllers/BoardPageCharacterLinksTests.cs` - server and render proofs

## Decisions Made
Followed the plan's recorded choices (Serves not Allows; refusal through the existing path; portrait endpoints untouched).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 - Missing critical] The change-character buttons for an existing character were also hidden**
- **Found during:** Task 2
- **Issue:** The plan named only the three `isCurrentUser && ownedCharacters.Any()` guards (the "Add character" buttons). Each row also carries a "Change character" button, guarded by a bare `isCurrentUser`, that opens the same modal. Leaving those would keep a trigger for a modal that is no longer rendered, contradicting "no add or change character button".
- **Fix:** all three change buttons per layout now require `charactersOn` as well.
- **Files modified:** `QuestBoard.Service/Views/Quest/Details.cshtml`, `Details.Mobile.cshtml`
- **Commit:** `0d6ef6ef`

**2. [Plan accuracy] Mobile Quest Details has no open-signup character select and no portrait image**
- **Found during:** Task 2
- **Issue:** The plan's behaviour list expects the open-signup select and the portrait `img` on both layouts. The mobile twin has neither (it never rendered them; the twins are not content-equivalent).
- **Fix:** none needed in the view. The tests assert the open-signup select is absent on both layouts (trivially true on mobile) and the portrait `img` on desktop only; character names are asserted on both.

---

**Total deviations:** 1 auto-fixed (1 Rule 2), 1 plan-accuracy note
**Impact on plan:** none on scope.

## TDD Gate Compliance

Both tasks ran a genuine RED before GREEN. For Task 2 the four view edits were first made, then reverted with a path-scoped `git checkout --` of the files this plan modified so the new tests could run against the original views (10 failures on the intended assertions), the RED commit was made, and the view edits were re-applied by hand.

## Issues Encountered
- A first attempt to save the view edits as a patch through `git diff` captured the token-compressed diff output, so the patch was unusable and the view edits were redone by hand. No code was lost.
- The full integration filter (`Quest`, 1445 tests) passed: 1434 passed, 11 skipped, 0 failed. `AmbientClockSeamTests` (65) passed.

## Known Stubs

None.

## Threat Flags

None. The change removes character-carrying inputs and refuses crafted ones; it adds no endpoint or trust boundary.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- Plan 12 adds the `[BoardPage]` gate to the Players, Characters and DungeonMaster controllers; this plan's views already follow the same switches.
- Portrait endpoints are left to plan 12's exemption; no image markup changed here.

## Self-Check: PASSED

- `BoardPageCharacterLinksTests.cs` exists; the four edited views and the controller carry the changes.
- Commits `9fd92670`, `2997569d`, `ea8f85e4` and `0d6ef6ef` exist on the branch; `git rev-list --count 7c452094..HEAD` is 4 before this docs commit.
- Acceptance criteria re-run: `BoardPageCharacterLinks` 25/25; the existing Quest, Players, MobileViews, CharacterSelect and BoardMoveControl tests passed unchanged.
- No added source line carries a planning reference (grep count 0).

---
*Phase: 93-per-board-page-selection-and-home-page*
*Completed: 2026-10-21*
