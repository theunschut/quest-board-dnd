---
phase: 91-linked-boards-with-a-shared-calendar
plan: 03
subsystem: platform
tags: [aspnet-mvc, razor, authorization, linked-boards, super-admin]
status: complete

requires:
  - phase: 91-linked-boards-with-a-shared-calendar
    provides: LinkedBoardSets table, nullable Groups.LinkedBoardSetId, ILinkedBoardSetRepository (plan 01)
provides:
  - "ILinkedBoardSetService / LinkedBoardSetService: same-type and one-set-per-board rules enforced server-side"
  - "LinkedBoardSetResult enum and LinkedBoardSetOutcome record"
  - "SuperAdminOnly LinkedBoardsController (Index GET; Create, Rename, AddBoard, RemoveBoard, Delete POST)"
  - "Desktop /platform/LinkedBoards page"
  - "LinkedBoardSetServiceTests and LinkedBoardsPlatformIntegrationTests"
affects: [91-04, 91-05, linked board reads, mobile Linked Boards page, Group Index entry button]

actuals:
  tokens: 17000
  tasks: 2
  commits: 4

tech-stack:
  added: []
  patterns:
    - "Service returns a LinkedBoardSetOutcome record; the controller maps it to a TempData toast and always redirects to Index"
    - "Confirm prompts built as a C# string, serialized with Json.Serialize into a JavaScript literal, then HTML-encoded by Razor when the finished handler string is written to the attribute"

key-files:
  created:
    - QuestBoard.Domain/Enums/LinkedBoardSetResult.cs
    - QuestBoard.Domain/Models/LinkedBoardSetOutcome.cs
    - QuestBoard.Domain/Interfaces/ILinkedBoardSetService.cs
    - QuestBoard.Domain/Services/LinkedBoardSetService.cs
    - QuestBoard.Service/Areas/Platform/Controllers/LinkedBoardsController.cs
    - QuestBoard.Service/ViewModels/PlatformViewModels/LinkedBoardsIndexViewModel.cs
    - QuestBoard.Service/ViewModels/PlatformViewModels/LinkedBoardSetCreateViewModel.cs
    - QuestBoard.Service/Areas/Platform/Views/LinkedBoards/Index.cshtml
    - QuestBoard.UnitTests/Services/LinkedBoardSetServiceTests.cs
    - QuestBoard.IntegrationTests/Controllers/LinkedBoardsPlatformIntegrationTests.cs
  modified:
    - QuestBoard.Domain/Extensions/ServiceExtensions.cs

key-decisions:
  - "The confirm handler is assembled as a plain string (return confirm(<Json.Serialize output>);) and written with a normal Razor attribute expression. Writing the IHtmlContent from Json.Serialize straight into the attribute would not be re-encoded, and its outer double quotes would end the attribute early; writing the finished string lets Razor HTML-encode it."
  - "Rename validates the name before looking the set up, so an invalid name never costs a read; NotFound and NotInSet share the single 'That set or board no longer exists.' toast"
  - "Mobile Index.Mobile.cshtml and the Group Index entry button are out of this plan's files and left to a later plan; the desktop view serves mobile until then"

patterns-established:
  - "Platform POST pages redirect to Index with Success/Error toasts built from a service outcome record"

requirements-completed: []

duration: 30min
completed: 2026-10-16
---

# Phase 91 Plan 03: Linked Boards Platform page Summary

A super admin can now create, rename, add to, remove from and delete linked sets on `/platform/LinkedBoards`. The service refuses a board of the wrong type or a board that already has a set even when the form is posted directly, and every confirm prompt that names a board or set is JavaScript-escaped.

## Accomplishments

- `LinkedBoardSetService` holds the rules: trimmed names (empty, whitespace or over 100 characters is `InvalidName`), `Enum.IsDefined` board type, a case-insensitive `NameExistsAsync` pre-check plus a null/false repository result for the unique-index race (both become `DuplicateName`), `TypeMismatch` against the set's stored type (so an empty set refuses a wrong-type board), `AlreadyLinked`, and `NotInSet` on remove.
- `LinkedBoardsController` carries `[Authorize(Policy = "SuperAdminOnly")]` at class level; all five POSTs have `[ValidateAntiForgeryToken]` and redirect to Index with the exact toast copy from the UI contract. An invalid Create model redirects with the name toast, or with "Choose the board type for the set." when only the type is wrong.
- Desktop `Index.cshtml`: create card, one card per set in name order with type and Active/Inactive badges, inactive/empty notes, rename form, board table with Remove Board, add-board picker limited to same-type unlinked boards (disabled with the "No unlinked ... boards are available." note when none), Delete Set, and the empty-page state.
- 27 integration facts (deny matrix for Admin/DungeonMaster/Player on GET and three POSTs, direct-post type-mismatch and already-linked refusals, board delete and Group Edit rename keep sets intact, picker filtering, empty state, name ordering, confirm escaping) and 26 service unit tests.

## Task Commits

1. Task 1 (tracer): create a set and add two boards end to end - `35407b33`
2. Task 2 RED: failing tests for rename, remove, delete - `4995d45a`
3. Task 2 GREEN: service rename, remove, delete - `315b8cad`
4. Task 2: controller POSTs, view controls, integration suite - `7a693f6f`

## Verification

- Tracer verify (`LinkedBoardsPlatformIntegrationTests`, `CrossBoardIgnoreQueryFiltersSeamTests`, `FeedRevisionWriteSeamTests`) passed before any expansion work.
- `LinkedBoardSetServiceTests` + `LinkedBoardSetRepositoryTests`: 37 passed.
- `LinkedBoardSet` + `Architecture` unit filter: 109 passed.
- `LinkedBoardsPlatformIntegrationTests`: 27 passed. `AntiForgeryTokenCoverageTests`, `GroupManagementIntegrationTests`, `PlatformAreaIntegrationTests`, `PlatformAreaLayoutTests`: 38 passed, none edited.
- Controller has 1 `SuperAdminOnly`, 5 `[HttpPost]`, 5 `[ValidateAntiForgeryToken]`; view has 2 `Json.Serialize`; no planning IDs in added source.
- Full-solution `dotnet test` was not run in this worktree (the plan defers it to after the wave).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Confirm handler written as an encoded string, not inline Json.Serialize output**
- **Found during:** Task 2 (view)
- **Issue:** The plan says to emit `return confirm(@Json.Serialize(message))` inside the attribute. `Json.Serialize` returns `IHtmlContent`, which Razor writes unencoded, so the JSON's outer double quotes would close the `onsubmit="..."` attribute.
- **Fix:** Build `var handler = $"return confirm({Json.Serialize(message)});"` in a Razor code block and write `onsubmit="@handler"`, so Razor HTML-encodes the whole handler. The integration test HTML-decodes each `onsubmit` and asserts it is one `confirm("...")` call with `O'Brien` and `"Q"`, and that no raw `O'Brien` appears anywhere in the page.
- **Files modified:** QuestBoard.Service/Areas/Platform/Views/LinkedBoards/Index.cshtml, QuestBoard.IntegrationTests/Controllers/LinkedBoardsPlatformIntegrationTests.cs
- **Commit:** 7a693f6f

**2. [Process] RED commit carried transient stubs**
- The Task 2 RED commit declared `RenameAsync`, `RemoveBoardAsync` and `DeleteAsync` on the interface with `NotImplementedException` bodies so the tests compile and fail at runtime (13 failed, 13 passed); the GREEN commit replaced them.

**3. [Process] Tracer feedback gate**
- Applied in its autonomous form as instructed: the tracer `<verify>` ran green after the commit, then expansion continued with no human checkpoint.

**4. [Plan wording] O'Brien assertion**
- The plan asks the test to "assert the HTML contains `O'Brien`" with the apostrophe escaped. The page encodes the name everywhere, so the test asserts the stronger property above instead of a literal substring.

## Issues Encountered

None.

## Known Stubs

None.

## Threat Flags

None. T-91-01 to T-91-05 are mitigated as planned: class-level policy with the role deny matrix, server-side type and one-set checks tested with hand-posted ids, existence and `NotInSet` re-checks, antiforgery on every POST, and JavaScript-escaped confirm prompts. T-91-07 is accepted (last write wins).

## Self-Check: PASSED

- All ten created files exist; `ServiceExtensions.cs` registers `ILinkedBoardSetService`.
- Commits `35407b33`, `4995d45a`, `315b8cad`, `7a693f6f` present in git log.
