---
phase: 35-board-type-configuration
fixed_at: 2026-07-03T12:15:00Z
review_path: .planning/phases/35-board-type-configuration/35-REVIEW.md
iteration: 1
findings_in_scope: 2
fixed: 2
skipped: 0
status: all_fixed
---

# Phase 35: Code Review Fix Report

**Fixed at:** 2026-07-03T12:15:00Z
**Source review:** .planning/phases/35-board-type-configuration/35-REVIEW.md
**Iteration:** 1

**Summary:**
- Findings in scope: 2 (WR-01, WR-02 — Critical: 0, Warning: 2 in scope; 3 Info findings excluded per fix_scope: critical_warning)
- Fixed: 2
- Skipped: 0

## Fixed Issues

### WR-01: BoardType immutability protection relies entirely on controller discipline, not binding

**Files modified:** `QuestBoard.Service/ViewModels/PlatformViewModels/GroupEditViewModel.cs`
**Commit:** a456a01
**Applied fix:** Added `[BindNever]` to `GroupEditViewModel.BoardType`, plus a plain-language comment explaining that the controller assigns this property for GET-display purposes only and it must never be populated by the model binder from a POST body. Verified via `GroupController.cs` that `BoardType` is only ever set manually in the GET action (`return View(new GroupEditViewModel { ..., BoardType = group.BoardType })`), never read from `model` in the POST action — so `[BindNever]` only closes the binding hole without affecting the existing display path (`asp-for="BoardType"` on a disabled `<select>` in `Edit.cshtml` / `Edit.Mobile.cshtml` reads the property directly, not through binding). This makes the immutability guarantee independent of future refactors to the POST handler, matching the fix suggested in REVIEW.md.

### WR-02: Create accepts undefined BoardType enum values with no validation

**Files modified:** `QuestBoard.Service/ViewModels/PlatformViewModels/GroupCreateViewModel.cs`
**Commit:** eb3d664
**Applied fix:** Added `[EnumDataType(typeof(BoardType), ErrorMessage = "Select a valid board type.")]` to `GroupCreateViewModel.BoardType`, alongside the existing `[Required]`. This rejects out-of-range integers (e.g. `BoardType=99`) at model-validation time before they can reach `GroupController.Create`, AutoMapper, or the `Groups.BoardType` int column, closing the gap where undefined values were previously persisted and then silently misread as "One-Shot" by the Index views. The optional DB-level CHECK constraint mentioned in REVIEW.md as defense-in-depth was not applied in this pass — it would require a new EF Core migration and is a larger, separately-reviewable change; left as a follow-up.

## Verification

- `dotnet build` (solution-wide, 6 projects): 0 errors, 0 warnings.
- `dotnet test` (full suite): 118/118 unit tests passed; 234/235 integration tests passed. The one integration failure (`AdminControllerIntegrationTests.SendConfirmationEmail_Post_WhenUserUnconfirmed_ShouldRedirectToUsersWithSuccess`, a 429 Too Many Requests rate-limit assertion) is unrelated to `BoardType` (confirmed no references to `BoardType` in that test file) and passes when run in isolation, indicating pre-existing test-run flakiness (shared rate-limiter state across the parallel suite) rather than a regression introduced by these fixes.

---

_Fixed: 2026-07-03T12:15:00Z_
_Fixer: Claude (gsd-code-fixer)_
_Iteration: 1_
