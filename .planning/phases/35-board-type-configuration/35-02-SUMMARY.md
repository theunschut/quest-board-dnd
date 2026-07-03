---
phase: 35-board-type-configuration
plan: 02
subsystem: api
tags: [aspnet-core-mvc, viewmodel, data-annotations, model-binding, mass-assignment-protection]

# Dependency graph
requires:
  - phase: 35-01
    provides: "BoardType enum, settable BoardType on Group/GroupEntity, AutoMapper ForMember casts, repository projections, EF migration"
provides:
  - "GroupCreateViewModel.BoardType (nullable BoardType?, [Required]) forcing an explicit board-type choice on group creation"
  - "GroupEditViewModel.BoardType (non-nullable, display-only, no [Required]) for read-only rendering in the Edit view"
  - "GroupController.Create POST persists the selected BoardType onto the new Group"
  - "GroupController.Edit GET populates BoardType for display"
  - "GroupController.Edit POST provably never reads model.BoardType (D-06 mass-assignment mitigation via omission)"
affects: [35-03-views]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Nullable enum + [Required] on a Create ViewModel to force an explicit selection instead of silently binding to the CLR default enum value"
    - "Tamper protection via omission: Edit POST simply never assigns the protected field from the posted model, no [BindNever]/allowlist needed"

key-files:
  created: []
  modified:
    - QuestBoard.Service/ViewModels/PlatformViewModels/GroupCreateViewModel.cs
    - QuestBoard.Service/ViewModels/PlatformViewModels/GroupEditViewModel.cs
    - QuestBoard.Service/Areas/Platform/Controllers/GroupController.cs
    - QuestBoard.IntegrationTests/Controllers/GroupManagementIntegrationTests.cs

key-decisions:
  - "GroupCreateViewModel.BoardType is BoardType? (nullable), not plain BoardType, so an empty <select> submission fails [Required] instead of silently model-binding to OneShot (ordinal 0) — this is what satisfies D-03"
  - "GroupEditViewModel.BoardType has no [Required] and is never read in Edit POST — display-only, by design"
  - "Fixed 4 pre-existing integration tests (CreateGroup_WithValidName_ShouldRedirectOrReturn200, CreateGroup_AfterCreation_ShouldAppearInIndex, DeleteGroup_WhenEmpty_ShouldShowDeleteConfirmation, DeleteGroup_WhenEmpty_PostShouldRedirectToIndex, DeleteGroup_WhenHasMembers_ShouldRedirectToIndex) that POST to Create without a BoardType field — now required, so their group-creation setup step now needs BoardType=OneShot in the form payload"

patterns-established:
  - "Nullable-enum-plus-[Required] idiom for any future ViewModel field that must force an explicit user choice rather than accept a silent enum default"

requirements-completed: [BOARD-01, BOARD-02]

# Metrics
duration: 20min
completed: 2026-07-03
---

# Phase 35 Plan 02: BoardType ViewModel & Controller Wiring Summary

**GroupCreateViewModel gets a required nullable BoardType (forces explicit choice, D-03); GroupEditViewModel gets a display-only BoardType; GroupController's Create POST persists it, Edit GET populates it, and Edit POST provably never reads it (D-06 tamper mitigation via omission) — 3 of 4 Plan-01 integration facts flip from red to green, the 4th remains red pending Plan 03's view work by design.**

## Performance

- **Duration:** 20 min
- **Started:** 2026-07-03T11:40:00Z
- **Completed:** 2026-07-03T12:00:00Z
- **Tasks:** 2
- **Files modified:** 4 (0 created, 4 modified)

## Accomplishments
- `GroupCreateViewModel.BoardType` added as `BoardType?` with `[Required(ErrorMessage = "Board type is required.")]` — the nullable type is what prevents an unselected dropdown from silently binding to `OneShot` (ordinal 0)
- `GroupEditViewModel.BoardType` added as a plain non-nullable, non-required, display-only field
- `GroupController.Create` POST now constructs `new Group { Name = model.Name, BoardType = model.BoardType!.Value }`
- `GroupController.Edit` GET now populates `BoardType = group.BoardType` on the view model
- `GroupController.Edit` POST body verified unchanged: `group.Name = model.Name;` remains the sole assignment statement, with zero occurrences of `model.BoardType` anywhere in the method — the D-06 mass-assignment mitigation continues to hold by construction
- Three of the four Plan-01 integration facts flipped from red to green: `CreateGroup_WithBoardType_ShouldPersistSelection`, `EditGroup_PostingChangedBoardType_ShouldBeSilentlyIgnored` (already green from Plan 01, stays green), `GroupsIndex_SeededGroup_ShouldDefaultToOneShot` (already green, stays green)

## Task Commits

Each task was committed atomically:

1. **Task 1: Add BoardType to the Create and Edit ViewModels** - `841cf69` (feat)
2. **Task 2: Wire GroupController Create POST (persist), Edit GET (populate), Edit POST (ignore)** - `645b615` (feat)

## Files Created/Modified
- `QuestBoard.Service/ViewModels/PlatformViewModels/GroupCreateViewModel.cs` - Added `using QuestBoard.Domain.Enums;` and `public BoardType? BoardType { get; set; }` with `[Required(ErrorMessage = "Board type is required.")]` and `[Display(Name = "Board Type")]`
- `QuestBoard.Service/ViewModels/PlatformViewModels/GroupEditViewModel.cs` - Added `using QuestBoard.Domain.Enums;` and `public BoardType BoardType { get; set; }` with `[Display(Name = "Board Type")]`, no `[Required]`
- `QuestBoard.Service/Areas/Platform/Controllers/GroupController.cs` - Create POST now sets `BoardType = model.BoardType!.Value` on the new `Group`; Edit GET now sets `BoardType = group.BoardType` on the view model; Edit POST body left untouched (no `model.BoardType` reference)
- `QuestBoard.IntegrationTests/Controllers/GroupManagementIntegrationTests.cs` - Added `["BoardType"] = ((int)BoardType.OneShot).ToString()` to the form payloads of 4 pre-existing tests whose Create-POST setup step broke once `BoardType` became `[Required]`

## Decisions Made
- `BoardType!.Value` in Create POST is safe because it only executes after `ModelState.IsValid` passes, and `[Required]` on the nullable `BoardType?` guarantees a non-null value at that point
- No `[BindNever]` or `[Bind(...)]` allowlist added to Edit POST — matches the codebase's existing "only copy the field you intend to allow" convention (the same pattern already used for `Name`)

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Fixed 4 pre-existing integration tests broken by making BoardType required on Create**
- **Found during:** Task 2 verification (`dotnet test QuestBoard.IntegrationTests --filter "FullyQualifiedName~GroupManagementIntegrationTests"`)
- **Issue:** `CreateGroup_WithValidName_ShouldRedirectOrReturn200`, `CreateGroup_AfterCreation_ShouldAppearInIndex`, `DeleteGroup_WhenEmpty_ShouldShowDeleteConfirmation`, `DeleteGroup_WhenEmpty_PostShouldRedirectToIndex`, and `DeleteGroup_WhenHasMembers_ShouldRedirectToIndex` all POST to `/platform/Group/Create` with only a `Name` field, using group creation purely as test setup for their actual assertions (index listing, delete confirmation, delete flows). Once `GroupCreateViewModel.BoardType` became `[Required]`, these POSTs started failing validation (re-rendering the form instead of creating the group), so the group was never created and every downstream assertion in those tests failed (either directly, or via a null `FirstOrDefault` lookup).
- **Fix:** Added `["BoardType"] = ((int)BoardType.OneShot).ToString()` to each of the 5 affected form-data dictionaries so group creation succeeds as those tests originally intended. This is a direct, necessary consequence of Task 2's own change (adding `[Required]` to `BoardType`) — not a new feature, and no assertion was weakened.
- **Files modified:** `QuestBoard.IntegrationTests/Controllers/GroupManagementIntegrationTests.cs`
- **Verification:** All 5 tests pass after the fix; full `GroupManagementIntegrationTests` run went from 5 failed/9 passed to 1 failed/13 passed (the 1 remaining failure is the expected Plan-03-scoped gap, see Issues Encountered)
- **Committed in:** `645b615` (Task 2 commit)

---

**Total deviations:** 1 auto-fixed (Rule 1 — bug fix, test-only, directly caused by this plan's own change)
**Impact on plan:** Necessary to keep the existing test suite green after making `BoardType` a required Create field. No scope creep — no production code touched beyond what Task 2 specified.

## Issues Encountered

**`CreateGroup_WithoutBoardType_ShouldFailValidation` remains red, by design, pending Plan 03.** This test asserts the rendered response body contains the literal string "Board type is required." after POSTing Create without a `BoardType` field. ASP.NET Core's `asp-validation-summary="ModelOnly"` (used on `Create.cshtml`) only surfaces model-level errors (those added via `ModelState.AddModelError(string.Empty, ...)`) — it does NOT surface property-keyed errors like the one `[Required]` adds for `BoardType`, unless the view also has a corresponding `asp-validation-for="BoardType"` span (the way `Name`'s error is surfaced via its own `asp-validation-for="Name"` span). The Create view has no `BoardType` form control yet — that is explicitly Plan 03's scope per this plan's own objective ("The views still need the actual form controls (Plan 03) for a human to exercise the flow"). I verified this is not a bug in my Task 2 changes: the controller correctly returns `ModelState.IsValid == false` and re-renders the form with a 200 (confirmed via manual test run), but the error text genuinely cannot appear in the HTML until Plan 03 adds the `<select asp-for="BoardType">` and its validation span. This matches Plan 01's own scaffold note that this fact was expected to stay red until "Plans 02 and 03" (plural) wire the controller AND views — Task 2's acceptance-criteria bullet claiming this specific fact goes green after Task 2 alone appears to be an overstatement in the plan text; I did not weaken the test or fabricate a workaround (e.g., adding a bare unbound validation span) to force a false-green, since that would misrepresent Plan 03's actual scope. This will go green automatically once Plan 03 adds the view control — no further action needed here.

**Unrelated pre-existing test flake observed (out of scope, not touched):** `AdminControllerIntegrationTests.SendConfirmationEmail_Post_WhenUserUnconfirmed_ShouldRedirectToUsersWithSuccess` failed with a 429 (rate-limited) when run as part of the full `QuestBoard.IntegrationTests` suite, but passes cleanly in isolation. This is unrelated to `GroupController`/`BoardType` and outside this plan's file scope — not fixed, noted here per the deviation rules' scope boundary.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- `GroupCreateViewModel`/`GroupEditViewModel`/`GroupController` now expose the full server-side `BoardType` contract for Plan 03's Razor views to bind against: Create needs a `<select asp-for="BoardType">` populated from the enum plus an `asp-validation-for="BoardType"` span (this is what will flip `CreateGroup_WithoutBoardType_ShouldFailValidation` green); Edit needs a disabled `<select asp-for="BoardType">` (or equivalent read-only rendering) using the populated display value.
- All integration test facts needed by Plan 03 are in place and green except the one view-dependent fact documented above, which Plan 03's own work will close.
- No blockers identified for Plan 03.

---
*Phase: 35-board-type-configuration*
*Completed: 2026-07-03*
