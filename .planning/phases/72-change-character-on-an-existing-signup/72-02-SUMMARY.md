---
phase: 72-change-character-on-an-existing-signup
plan: 02
subsystem: ui
tags: [razor, bootstrap-modal, javascript, extension-method, unit-tests]

requires: []
provides:
  - "CharacterDisplayExtensions.ToSelectLabel — single-source character option label"
  - "_CharacterSelectModal.cshtml — shared add/change/clear character modal partial"
  - "Trigger data-attribute contract (data-quest-id, data-current-character-id, data-current-character-label)"
affects: [72-03-desktop-wiring, 72-04-mobile-wiring]

tech-stack:
  added: []
  patterns:
    - "show.bs.modal + event.relatedTarget priming for a single modal instance serving many trigger sites"
    - "Inject-if-missing option pattern to guarantee a select's initial value always has a matching option"
    - "Disable-then-submit to post an absent field without tripping client-side required validation"

key-files:
  created:
    - QuestBoard.Service/Extensions/CharacterDisplayExtensions.cs
    - QuestBoard.UnitTests/Extensions/CharacterDisplayExtensionsTests.cs
    - QuestBoard.Service/Views/Shared/_CharacterSelectModal.cshtml
  modified: []

key-decisions:
  - "ToSelectLabel appends the status enum's own .ToString() rather than a hand-written map, so a future CharacterStatus value can't silently render as a number"
  - "Remove-character injects a stand-in <option> when the current character isn't already in the list, rather than assigning select.value with no matching option (which would silently fall back to the placeholder)"
  - "Remove submits the form directly (bypassing Save/constraint validation) after disabling the select, so the field posts absent and binds to null server-side"

patterns-established:
  - "One partial, one modal instance per page, primed per-invocation from trigger data-* attributes — the pattern plans 03/04 wire desktop and mobile triggers into"

requirements-completed: [SIGNCHAR-01, SIGNCHAR-02, SIGNCHAR-03, SIGNCHAR-04]

coverage:
  - id: D1
    description: "Single unit-tested ToSelectLabel() extension produces every character option label (Active byte-for-byte unchanged, non-Active suffixed with status name)"
    requirement: SIGNCHAR-04
    verification:
      - kind: unit
        ref: "QuestBoard.UnitTests/Extensions/CharacterDisplayExtensionsTests.cs#ToSelectLabel_ForActiveCharacter_ReturnsNameLevelAndClassesWithNoStatusSuffix"
        status: pass
      - kind: unit
        ref: "QuestBoard.UnitTests/Extensions/CharacterDisplayExtensionsTests.cs#ToSelectLabel_ForRetiredCharacter_AppendsTheStatusName"
        status: pass
      - kind: unit
        ref: "QuestBoard.UnitTests/Extensions/CharacterDisplayExtensionsTests.cs#ToSelectLabel_ForDeadCharacter_AppendsTheStatusName"
        status: pass
      - kind: unit
        ref: "QuestBoard.UnitTests/Extensions/CharacterDisplayExtensionsTests.cs#ToSelectLabel_WithMultipleClasses_JoinsThemWithCommaSpace"
        status: pass
      - kind: unit
        ref: "QuestBoard.UnitTests/Extensions/CharacterDisplayExtensionsTests.cs#ToSelectLabel_WithNoClasses_RendersEmptyParentheses"
        status: pass
    human_judgment: false
  - id: D2
    description: "Shared, model-less _CharacterSelectModal.cshtml partial exists with the fixed trigger data-attribute contract and destructive-action-isolated footer layout, ready for desktop/mobile wiring"
    requirement: SIGNCHAR-01
    verification:
      - kind: unit
        ref: "dotnet build (Razor compilation)"
        status: pass
    human_judgment: true
    rationale: "Visual layout (Remove far left, Cancel/Save grouped right, modal styling) and the modal-priming/remove-submit behavior in the browser require human/browser verification; not exercised by any automated test in this plan since the partial isn't yet rendered by a host view (plans 03/04 wire the triggers)."

duration: 25min
completed: 2026-08-25
status: complete
---

# Phase 72 Plan 02: Shared Character-Select Modal Summary

**Extracted the existing add-character modal into a shared, self-priming `_CharacterSelectModal.cshtml` partial that serves add/change/clear from one instance per page, backed by a single unit-tested `ToSelectLabel()` extension for every character option label.**

## Performance

- **Duration:** 25 min
- **Started:** 2026-08-25T13:02:00Z (approx.)
- **Completed:** 2026-08-25T13:26:16Z
- **Tasks:** 3
- **Files modified:** 3 (all new)

## Accomplishments
- `CharacterDisplayExtensions.ToSelectLabel(this Character)` centralizes the character-picker option text, preserving today's exact Active-character format byte-for-byte and appending a status suffix (e.g. `(Retired)`) for anything else, backed by 5 unit tests asserting exact strings
- `_CharacterSelectModal.cshtml` — a model-less shared partial holding the modal shell (mirroring the `ShopManagement` deny-modal shape), the option list driven entirely by `ToSelectLabel()`, and a footer with Remove isolated far left and Cancel/Save grouped right
- Self-priming `show.bs.modal` script reads `data-quest-id`/`data-current-character-id`/`data-current-character-label` from the trigger, resets state left by a prior invocation (re-enables the select, restores `required`, removes any previously injected option), and injects a stand-in `<option>` when the signup's current character isn't already in the list so the dropdown always opens showing the actual signup character
- Remove-character click handler: native `confirm()` guard, clears `required`, empties and disables the select (excluding it from submission), then calls `form.submit()` directly to bypass constraint validation
- Full test suite (313 unit + 399 integration, 712 total) passes with zero regressions — the new partial isn't yet rendered by any view, so nothing existing could have broken

## Task Commits

Each task was committed atomically:

1. **Task 1: Add the single-source character option label and unit-test it** - `707ae11` (feat)
2. **Task 2: Create the shared character-select modal partial** - `d26cbf1` (feat)
3. **Task 3: Add the self-priming and remove-character script to the partial** - `24dc8d7` (feat)

_Note: worktree mode — this is the only commit made for this plan's work beyond the SUMMARY commit; the orchestrator applies the final `docs` metadata commit after merge._

## Files Created/Modified
- `QuestBoard.Service/Extensions/CharacterDisplayExtensions.cs` - `ToSelectLabel(this Character)` extension, public so `QuestBoard.UnitTests` (no `InternalsVisibleTo` grant) can test it directly
- `QuestBoard.UnitTests/Extensions/CharacterDisplayExtensionsTests.cs` - 5 tests pinning exact label strings for Active/Retired/Dead, multi-class join, and no-classes cases
- `QuestBoard.Service/Views/Shared/_CharacterSelectModal.cshtml` - the shared modal partial (markup + priming/remove script)

## Decisions Made
- Followed the plan's discretion decisions verbatim (D-01 through D-06 in the plan's `<discretion_decisions>` block): partial renders modal-only (triggers stay in host views), per-invocation `show.bs.modal` priming with no AJAX/global state, disable-then-submit for null-posting, `enum.ToString()` for the status suffix, no added ordering (repository already orders correctly), and the source block's informational banner deliberately dropped since the field label already states the task.
- No deviations beyond the plan's own explicit discretion — implementation followed the action blocks and acceptance criteria as written.

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

None.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- `_CharacterSelectModal.cshtml` and its fixed trigger data-attribute contract (`data-quest-id`, `data-current-character-id`, `data-current-character-label`) are ready for plans 03 (desktop) and 04 (mobile) to wire trigger buttons into.
- `ToSelectLabel()` is ready to replace the inline option-text building at the remaining signup-time selects (`Details.cshtml:333`, `:419`, `Details.Mobile.cshtml:295`) that plans 03/04 touch.
- No blockers. The partial is not yet rendered by any host view — that wiring, plus the server-side `UpdateSignupCharacter` changes (D-10/D-13/D-14/D-15), the pencil/`+` triggers, and `ViewBag.UserCharacters` widening (D-12) are out of this plan's scope per the ROADMAP's stated internal order (this plan must land before 03/04 can consume it).

---
*Phase: 72-change-character-on-an-existing-signup*
*Completed: 2026-08-25*
