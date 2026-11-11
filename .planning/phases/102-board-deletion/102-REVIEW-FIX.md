---
phase: 102-board-deletion
fixed_at: 2026-11-11T16:57:24Z
review_path: .planning/phases/102-board-deletion/102-REVIEW.md
iteration: 1
findings_in_scope: 5
fixed: 5
skipped: 0
status: all_fixed
---

# Phase 102: Code Review Fix Report

**Fixed at:** 2026-11-11T16:57:24Z
**Source review:** .planning/phases/102-board-deletion/102-REVIEW.md
**Iteration:** 1

**Summary:**
- Findings in scope: 5 (1 critical, 4 warnings; the 5 Info findings are out of scope for `critical_warning`)
- Fixed: 5
- Skipped: 0

**Where verification ran:** in the main checkout (`/mnt/Data/repos/quest-board-dnd`, branch
`milestone/v9-rolling-improvements`). The orchestrator said this agent was the only writer on that
checkout, so no worktree was created. The numbers below can be reproduced from that tree.

- `dotnet build`: succeeded. One transient `Internal CLR error (0x80131506)` on the first build; the
  re-run was clean.
- `dotnet test QuestBoard.UnitTests`: 1389 passed, 0 failed.
- `dotnet test QuestBoard.IntegrationTests`: 1841 passed, 14 skipped (the SQL Server-gated tests), 0 failed.
- Gated SQL Server run (`--filter FullyQualifiedName~SqlServer` with
  `QUESTBOARD_MIGRATOR_TEST_REQUIRED=1`): 14 passed, 0 failed.

## Fixed Issues

### CR-01: A board whose name has leading or trailing whitespace can never be deleted

**Files modified:** `QuestBoard.Domain/Extensions/BoardNameText.cs` (new), `QuestBoard.Domain/Services/GroupService.cs`, `QuestBoard.Domain/Interfaces/IGroupService.cs`, `QuestBoard.Service/wwwroot/js/platform-board-delete.js`, `QuestBoard.Service/Areas/Platform/Views/Group/_BoardDeleteBody.cshtml`, `QuestBoard.Service/ViewModels/PlatformViewModels/BoardDeletePageViewModel.cs`, `QuestBoard.Service/Areas/Platform/Controllers/GroupController.cs`, `QuestBoard.UnitTests/Services/GroupServiceBoardDeletionTests.cs`, `QuestBoard.UnitTests/Controllers/GroupControllerBoardNameTests.cs` (new), `QuestBoard.IntegrationTests/Tests/BoardDeletionStaticGuardTests.cs`, `QuestBoard.IntegrationTests/Controllers/PlatformBoardDeleteTests.cs`
**Commit:** fa180daf
**Applied fix:**
- **One whitespace definition.** The new `BoardNameText.EdgeWhitespace` is every Unicode White_Space
  character (what .NET `char.IsWhiteSpace` and `Trim()` strip) plus U+FEFF (which JavaScript `trim()`
  also strips). That is the union of what both sides treat as whitespace.
  - The server trims this set from both the typed name and the stored name, then compares them ordinal
    and case-sensitive.
  - The gate script no longer calls `trim()`. It carries the same set as `\uXXXX` escapes and trims both
    `input.value` and the data attribute with it.
- **Page text.** The page asks for `ConfirmationName`, the stored name with edge whitespace trimmed.
  This covers the help text, the mismatch line and the data attribute.
- **Saving names.** `GroupController.Create` and `Edit` save names trimmed with the same set. A name made
  only of U+FEFF is refused with "Group name is required.". No migration was written, so existing names
  are unchanged.
- **Tests.**
  - Service theories cover stored names and typed names that carry edge whitespace, including U+FEFF,
    U+0085 and U+00A0. A further test pins that case and inner spacing are still compared exactly.
  - A unit test pins the set to `char.IsWhiteSpace` plus U+FEFF.
  - A static guard checks that the script holds the server's set character for character and never
    calls `.trim(`.
  - Controller tests cover trimming on Create and Edit.
  - An end-to-end test deletes a board stored as `"Doomed Quest Table "` by typing the name without the
    trailing space.

### WR-01: The member recheck is not atomic with the delete, so a concurrently added member is silently erased

**Files modified:** `QuestBoard.Repository/GroupRepository.cs`, `QuestBoard.IntegrationTests/Tests/BoardDeletionSqlServerTests.cs`
**Commit:** e5a9a061
**Applied fix:**
- **The transaction.** `DeleteBoardAsync` keeps the early member check, which avoids loading the board
  for nothing. It then repeats the check inside a `Serializable` transaction that also wraps
  `SaveChangesAsync`, and commits after the save.
  - The transaction opens only when `Database.IsRelational()` is true, the same pattern
    `EventSeriesRepository` uses, so the InMemory unit tests run unchanged.
  - If the in-transaction check finds a member, the change tracker is cleared and the delete is refused
    with `HasMembers`. Disposing the transaction rolls it back.
- **Locking.** Only the check and the save run inside the transaction; the wide staging reads stay
  outside it.
  - The serializable range lock covers this board's membership range (the `IX_UserGroups_GroupId`
    seek) plus the rows the save touches by key. It never covers whole-table scans of other boards.
  - Writes stay on the change tracker. There is no raw SQL or `Execute*`.
- **Test.** A new gated SQL Server test uses a `DbCommandInterceptor`. Just before the first write of the
  save, it tries to insert a `UserGroups` row for the board on a second connection with
  `LOCK_TIMEOUT 3000`.
  - With the fix, the insert gets lock timeout 1222, the delete succeeds, and no membership row remains.
  - With the transaction temporarily disabled, the test fails as expected: the insert succeeded and was
    then removed by the cascade. The file was then restored.
- **Note for review:**
  - A concurrent `AddMemberAsync` that was held off now fails on the foreign key once the board is gone.
    That turns the silent loss into a visible error for the other admin, as the review proposed.
  - If the server picks the delete as a deadlock victim, the controller's existing catch shows the
    friendly "could not be deleted" message.

### WR-02: Other sessions that still name the deleted board can write to it for up to five minutes and get a 500

**Files modified:** `QuestBoard.Service/Middleware/GroupSessionMiddleware.cs`, `QuestBoard.UnitTests/Middleware/GroupSessionMiddlewareRevalidationTests.cs`
**Commit:** 507aedcc
**Applied fix:** This is a variant of option A and the least invasive change.
- **The change.** `needsRevalidation` is now also true for any request that is not GET or HEAD. A write
  always runs the existing check:
  - for members, `GetGroupRoleByIdAsync`, which returns null once the board is gone;
  - for super admins, the existing board existence lookup.
- **Effect.** A session naming a deleted board gets the existing 409 gate and has its active board
  cleared, instead of a foreign-key 500.
- **Cost.**
  - GET and HEAD keep the 5-minute interval, so page views cost nothing extra.
  - Each write costs one small indexed query.
  - As a side effect, a removed member's writes are also stopped at once rather than after the interval.
- **Code.** A small `IsWrite` helper is now shared with `GateToBoardPicker`, and the class doc was
  updated.
- **Tests.** New unit tests cover:
  - a super admin with a fresh timestamp and a deleted board, for POST, PUT and DELETE (409, session
    cleared);
  - a removed member with a fresh timestamp on POST (409);
  - a member with a fresh timestamp on POST (checked, then passed through);
  - HEAD with a fresh timestamp (no lookup).
- **Not covered.** Reads on a deleted board still show an empty board until the interval passes. That is
  not a 500, and the review only flagged writes as a failure.

### WR-03: The phone-layout `<summary>` rules remove the disclosure marker, so the name lists do not look expandable

**Files modified:** `QuestBoard.Service/wwwroot/css/platform-group-delete.mobile.css`, `QuestBoard.Service/wwwroot/css/platform-users-boards.mobile.css`, `QuestBoard.IntegrationTests/Tests/BoardDeletionStaticGuardTests.cs`
**Commit:** d8c21d48
**Applied fix:**
- **CSS.** In both phone sheets, `display: flex; align-items: center` on the summary became
  `display: list-item; align-content: center`. `min-height: var(--qb-hit-min)` and `cursor: pointer`
  are kept.
  - The native disclosure triangle comes back, and the tap target stays at least 44px.
  - Tokens only, inside `@layer pages`, with no colour literal, `@media` or `!important`.
- **Guard.** A new static guard, `NewSheets_KeepTheSummaryDisclosureMarker`, fails if any rule in the new
  sheets that targets a summary sets a display other than `list-item`. A known-bad sample was added to
  `Scanners_FireOnKnownBadSamples`.
- **Not checked by eye.** Nobody has checked the result under a phone User-Agent. On browsers older than
  roughly early 2024, `align-content` on a block container is ignored. The line then sits at the top of
  the 44px row, which still works.

### WR-04: The confirmation page silently omits any content kind it has no label for

**Files modified:** `QuestBoard.Service/ViewModels/PlatformViewModels/BoardDeletePageViewModel.cs`, `QuestBoard.UnitTests/ViewModels/BoardDeletePageViewModelTests.cs` (new)
**Commit:** 4d629323
**Applied fix:**
- **Behaviour.** The `Labels.ContainsKey` filter is gone. Labels are looked up through `LabelsFor`, which
  throws an `InvalidOperationException` naming the kind when there is no label. Kinds with a count of
  zero are still hidden.
- **Tests.** New unit tests check that:
  - a preview with every `BoardContentKind` value at count 1 renders exactly one row per kind;
  - a kind with no label throws;
  - zero-count kinds stay hidden.

---

_Fixed: 2026-11-11T16:57:24Z_
_Fixer: Claude (gsd-code-fixer)_
_Iteration: 1_
