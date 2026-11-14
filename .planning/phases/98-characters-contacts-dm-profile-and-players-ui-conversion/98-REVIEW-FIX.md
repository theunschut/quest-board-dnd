---
phase: 98-characters-contacts-dm-profile-and-players-ui-conversion
fixed_at: 2026-11-14T16:00:00Z
review_path: .planning/phases/98-characters-contacts-dm-profile-and-players-ui-conversion/98-REVIEW.md
iteration: 1
findings_in_scope: 2
fixed: 1
skipped: 1
status: partial
---

# Phase 98: Code Review Fix Report

**Fixed at:** 2026-11-14
**Source review:** .planning/phases/98-characters-contacts-dm-profile-and-players-ui-conversion/98-REVIEW.md
**Iteration:** 1

**Summary:**
- Findings in scope: 2 (Critical 0, Warning 2; Info out of scope for `critical_warning`)
- Fixed: 1
- Skipped: 1 (no_change_needed, operator decision)

## Fixed Issues

### WR-01: A rejected or cleared second file pick leaves the previous crop in the hidden input and uploads it

**Files modified:** `QuestBoard.Service/wwwroot/js/image-crop.js`, `QuestBoard.IntegrationTests/Tests/CharactersConversionGuardTests.cs`
**Commit:** 3aa6d4fd
**Applied fix:** Added `discardPendingCrop(input)` to `image-crop.js`. It looks up the hidden output for the input's configured output name, then calls the existing `resetCropState()`. The change handler now calls it on every exit that empties or refuses the pick: a cancelled or cleared chooser, an oversized file, a disallowed type and a decode failure. The GIF branch uses the same helper instead of its own inline lookup. Because the hidden output is resolved first, the clear also works when the crop dialog was never opened in the page view.

Guard added to `CharactersConversionGuardTests` (the repo has no browser-driven test, so this is a static scan of the module):
- `ImageCropModule_DiscardsThePendingCropWhenAPickIsRefusedOrCleared` requires every `!file` block and every block that resets `input.value = ''` to call `discardPendingCrop(input)`. It also requires at least four such blocks and the helper's definition.
- `RejectingBlockScanner_FlagsAnExitThatKeepsTheCrop` gives the scanner a known-bad and a known-good sample, so the guard cannot pass on a dead regex.

Verification: `node --check` on the script passed. `CharactersConversionGuardTests` ran 29 passed, 0 failed. This is a behavioural JS change that no browser test exercises, so a quick manual check in a browser is worthwhile: pick a photo, then pick an oversized or non-image file, and confirm the form no longer posts the first crop. Status: fixed, requires human verification.

## Skipped Issues

### WR-02: Phone Contacts page has no h1 when the board has no categories

**File:** `QuestBoard.Service/Views/Contacts/Index.Mobile.cshtml:242-247`
**Reason:** no_change_needed, operator decision. The operator decided this exact item at the Gate 2 review as `d1: as-is` (`~/qb-ui-review/phase-98/gate2/answers.json`, reply line `... d1:as-is ...`). The view is deliberately left unchanged.
**Original issue:** The title card's `<h1>` renders only inside `@if (Model.HasCategories)`, so a phone board with no categories starts heading navigation at an h2.

## Verification environment

Fixes were edited, committed and tested in the isolated review-fix worktree (`.claude/worktrees/rf-98-*`), not the main checkout. The worktree was fast-forwarded into `milestone/v9-rolling-improvements` afterwards. See the suite counts reported by the orchestrator hand-off.

---

_Fixed: 2026-11-14_
_Fixer: Claude (gsd-code-fixer)_
_Iteration: 1_

## Suite counts (after the fix)

- Unit: 1416 passed, 0 failed.
- Integration: 2302 passed, 0 failed, 16 skipped (SQL-backed migrator suite).
