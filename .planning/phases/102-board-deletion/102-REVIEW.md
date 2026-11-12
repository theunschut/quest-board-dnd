---
phase: 102-board-deletion
reviewed: 2026-11-11T16:38:05Z
depth: standard
files_reviewed: 47
files_reviewed_list:
  - QuestBoard.Domain/Enums/BoardContentKind.cs
  - QuestBoard.Domain/Enums/BoardDeleteStatus.cs
  - QuestBoard.Domain/Interfaces/IGroupRepository.cs
  - QuestBoard.Domain/Interfaces/IGroupService.cs
  - QuestBoard.Domain/Interfaces/IUserRepository.cs
  - QuestBoard.Domain/Interfaces/IUserService.cs
  - QuestBoard.Domain/Models/BoardDeletion/BoardDeletionItem.cs
  - QuestBoard.Domain/Models/BoardDeletion/BoardDeletionPreview.cs
  - QuestBoard.Domain/Models/BoardDeletion/BoardDeletionResult.cs
  - QuestBoard.Domain/Models/BoardDeletion/BoardUnlinkedFollowUp.cs
  - QuestBoard.Domain/Services/GroupService.cs
  - QuestBoard.Domain/Services/UserService.cs
  - QuestBoard.IntegrationTests/Controllers/BoardDeletePageGateRenderTests.cs
  - QuestBoard.IntegrationTests/Controllers/GroupManagementIntegrationTests.cs
  - QuestBoard.IntegrationTests/Controllers/LinkedBoardsPlatformIntegrationTests.cs
  - QuestBoard.IntegrationTests/Controllers/PlatformBoardDeleteTests.cs
  - QuestBoard.IntegrationTests/Controllers/UsersControllerIntegrationTests.cs
  - QuestBoard.IntegrationTests/Tests/BoardDeletionSqlServerTests.cs
  - QuestBoard.IntegrationTests/Tests/BoardDeletionStaticGuardTests.cs
  - QuestBoard.IntegrationTests/Tests/TenantIsolationTests.cs
  - QuestBoard.Repository/GroupRepository.cs
  - QuestBoard.Repository/UserRepository.cs
  - QuestBoard.Service/Areas/Platform/Controllers/GroupController.cs
  - QuestBoard.Service/Areas/Platform/Controllers/UsersController.cs
  - QuestBoard.Service/Areas/Platform/Views/Group/Delete.Mobile.cshtml
  - QuestBoard.Service/Areas/Platform/Views/Group/Delete.cshtml
  - QuestBoard.Service/Areas/Platform/Views/Group/_BoardDeleteBody.cshtml
  - QuestBoard.Service/Areas/Platform/Views/Users/Index.Mobile.cshtml
  - QuestBoard.Service/Areas/Platform/Views/Users/Index.cshtml
  - QuestBoard.Service/Extensions/ActiveBoardSessionExtensions.cs
  - QuestBoard.Service/Middleware/GroupSessionMiddleware.cs
  - QuestBoard.Service/Services/IActiveBoardSwitcher.cs
  - QuestBoard.Service/ViewModels/PlatformViewModels/BoardDeletePageViewModel.cs
  - QuestBoard.Service/ViewModels/PlatformViewModels/BoardDeleteRowViewModel.cs
  - QuestBoard.Service/ViewModels/PlatformViewModels/PlatformUserViewModel.cs
  - QuestBoard.Service/wwwroot/css/platform-group-delete.css
  - QuestBoard.Service/wwwroot/css/platform-group-delete.mobile.css
  - QuestBoard.Service/wwwroot/css/platform-users-boards.mobile.css
  - QuestBoard.Service/wwwroot/js/platform-board-delete.js
  - QuestBoard.UnitTests/Controllers/GroupControllerBoardDeletionTests.cs
  - QuestBoard.UnitTests/Middleware/GroupSessionMiddlewareRevalidationTests.cs
  - QuestBoard.UnitTests/Repository/BoardDeletionTestGraph.cs
  - QuestBoard.UnitTests/Repository/BoardOwnedForeignKeysTests.cs
  - QuestBoard.UnitTests/Repository/GroupRepositoryBoardDeletionPreviewTests.cs
  - QuestBoard.UnitTests/Repository/GroupRepositoryBoardDeletionTests.cs
  - QuestBoard.UnitTests/Repository/UserRepositoryBoardNamesTests.cs
  - QuestBoard.UnitTests/Services/GroupServiceBoardDeletionTests.cs
findings:
  critical: 1
  warning: 4
  info: 5
  total: 10
status: issues_found
---

# Phase 102: Code Review Report

**Reviewed:** 2026-11-11T16:38:05Z
**Depth:** standard
**Files Reviewed:** 47
**Status:** issues_found

## Narrative Findings (AI reviewer)

## Summary

I reviewed the board delete (repository, service, controller), the confirmation page (partial, twins, gate
module, CSS), the session-clearing changes (middleware, extension), the Users board column and the new and
changed tests.

The core delete holds up:
- **Atomicity and the change tracker.** Every row is loaded tracked and removed in one `SaveChangesAsync`. That
  is one transaction, and EF orders the deletes, including the self-referencing `OriginalQuest` chain.
  Nothing uses `ExecuteUpdate`/`ExecuteDelete`, raw SQL or manual entry state.
- **Board scoping.** Every `IgnoreQueryFilters` helper is re-scoped by the explicit `groupId`.
- **Cross-board references.** Follow-ups, signups using one of the board's characters, and events in one of its
  series are unlinked, not deleted.
- **Safe failure.** A failed save clears the tracker, and the controller turns the exception into a friendly
  error.
- **Page safety.** Anti-forgery is present on the POST. All user-authored names render through Razor encoding
  or text nodes. The new views have no inline script or style.
- **No planning IDs.** The only planning-ID-shaped string is the deliberately split sample in
  `BoardDeletionStaticGuardTests`.

The defects found:
- **Name check.** The typed name is compared after trimming on one side only, and board names are never trimmed
  when saved. A board whose name starts or ends with whitespace cannot be deleted.
- **Members race.** The member recheck is not atomic with the delete.
- **Other sessions.** Sessions other than the deleting admin's can keep writing to a deleted board for up to
  five minutes.
- **Phone layout.** The phone-layout `<summary>` styling removes the disclosure marker.
- **Silent omission.** The page view model silently drops any content kind it has no label for.

## Critical Issues

### CR-01: A board whose name has leading or trailing whitespace can never be deleted

**File:** `QuestBoard.Domain/Services/GroupService.cs:55`, `QuestBoard.Service/wwwroot/js/platform-board-delete.js:41`, `QuestBoard.Service/Areas/Platform/Views/Group/_BoardDeleteBody.cshtml:100-101`
**Issue:** The server compares `confirmName?.Trim()` against the **untrimmed** stored `group.Name`. The gate module
does the same: `input.value.trim() === input.dataset.boardDeleteName`.

Board names are never trimmed when they are saved:
- `GroupController.Create` (line 52) and `Edit` (line 123) store `model.Name` as posted.
- `GroupCreateViewModel`/`GroupEditViewModel` only carry `[Required]`/`[StringLength]`.
- No trimming model binder exists.

So a board named `"Goblin Night "` (a trailing space from a paste is enough) never matches:
1. The admin types exactly what the page tells them to: *"Type "Goblin Night " exactly, including capitals and
   spaces"*.
2. The trailing space is stripped from the typed side only.
3. The button never unlocks, and a hand-crafted POST is refused with NameMismatch.

The page actively instructs the admin to include spaces it then discards. The only escape is to notice the
whitespace and rename the board first, and nothing on the page hints at that. This breaks the phase's core
promise for those boards.

The two trims also use different character sets. JS `trim()` strips U+FEFF and .NET `Trim()` does not, while
.NET strips U+0085 and JS does not. In those cases the button unlocks but the server refuses.

**Fix:** Apply the same normalisation to both sides, on both client and server, and stop new names from carrying
the whitespace:
```csharp
// GroupService.DeleteBoardAsync
if (!string.Equals(confirmName?.Trim(), group.Name.Trim(), StringComparison.Ordinal))
    return BoardDeletionResult.Refused(BoardDeleteStatus.NameMismatch, group.Name);
```
```js
// platform-board-delete.js
const isMatch = input.value.trim() === input.dataset.boardDeleteName.trim();
```
Also trim `model.Name` in `GroupController.Create`/`Edit` (or in `GroupService.AddAsync`/update) so new names
cannot carry edge whitespace. The route id already pins the board, so comparing trimmed names cannot pick the
wrong board.

## Warnings

### WR-01: The member recheck is not atomic with the delete, so a concurrently added member is silently erased

**File:** `QuestBoard.Repository/GroupRepository.cs:236-291`
**Issue:** `DeleteBoardAsync` checks `UserGroups.AnyAsync(...)` at line 236. It then runs about 17 further
round-trips to load and stage rows, and only then calls `SaveChangesAsync` at line 291. None of this runs inside
a transaction that holds a lock on the board's membership rows.

Suppose another super admin adds a member in that window, through `AddMemberAsync` from Platform Members or from
the Admin area:
1. The `DELETE FROM Groups` cascades `UserGroups` (Cascade from Group).
2. On a campaign board, the member's automatic `EventSignups` also disappear (Cascade from Event).
3. The delete reports success.
4. The member keeps a "you were added to board X" email (`GroupMembershipAddedEmailJob`) for a board that no
   longer exists.

This breaks the D-09 guarantee that a board gaining members is refused, and loses a membership with no trace.
The window is small, but the outcome is silent data loss on an irreversible operation.

A related case: a row that a background job adds in the same window makes the save fail on a foreign key, and the
admin sees "could not be deleted". One example is `RecurringOccurrenceTopUpJob` materialising an occurrence for a
series on this board. That is safe but contradicts D-09's "content added before confirm is simply deleted".

**Fix:** Run the recheck and the save in one serializable transaction. A concurrent membership insert for this
`GroupId` then blocks or deadlocks, and does not slip through. The app has no EF retrying execution strategy, so
an explicit transaction is allowed.
```csharp
await using var tx = await DbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
if (await DbContext.UserGroups.AnyAsync(ug => ug.GroupId == groupId, token))
    return BoardDeletionResult.Refused(BoardDeleteStatus.HasMembers, group.Name);
// ... stage as today ...
await DbContext.SaveChangesAsync(token);
await tx.CommitAsync(token);
```
(Alternatively, read the membership rows with `UPDLOCK, HOLDLOCK` through a narrowly allowlisted query.)

### WR-02: Other sessions that still name the deleted board can write to it for up to five minutes and get a 500

**File:** `QuestBoard.Service/Middleware/GroupSessionMiddleware.cs:127-167`, `QuestBoard.Service/Areas/Platform/Controllers/GroupController.cs:240,262-273`
**Issue:** `ForgetDeletedBoardInOwnSessionAsync` clears only the deleting admin's session. Every other session
whose active board is the deleted one is only caught on the next revalidation, and that happens only when
`ActiveGroupValidatedAtUtc` is older than five minutes. This covers a second super admin who picked the board a
minute earlier, or an ex-member validated recently.

Until then, the middleware passes their requests through:
- **Reads** show an empty board.
- **Writes** stamp `GroupId` = the deleted id and fail on the foreign key. That is an unhandled
  `DbUpdateException`, a 500. Writes include creating a quest, event, contact, shop item or character.

The middleware's own comment names this failure ("a session still naming it would let a write stamp a board that
no longer exists"). The fix only narrows it to the five-minute interval.

**Fix:** Do not rely on the interval for an existence fact that the delete can publish:
- **Option A:** always verify the board exists for non-GET/HEAD requests. That is one primary-key lookup on
  `Groups`, which carries no query filter.
- **Option B:** have the delete record the id in a process-wide "deleted boards" set, such as `IMemoryCache` with
  a lifetime of at least `MembershipRevalidationInterval`. The middleware checks that set before the interval
  test and calls `ClearActiveBoard()` and `GateToBoardPicker` on a hit.

### WR-03: The phone-layout `<summary>` rules remove the disclosure marker, so the name lists do not look expandable

**File:** `QuestBoard.Service/wwwroot/css/platform-group-delete.mobile.css:53-58`, `QuestBoard.Service/wwwroot/css/platform-users-boards.mobile.css:7-12`
**Issue:** `summary { display: flex; }` takes the element out of `display: list-item`. Chromium and Firefox only
render the `::marker` disclosure triangle for `list-item`, so the triangle disappears. On the phone confirmation
page, "Show quest titles" and the like look like plain text, and so does "3 boards" on Platform Users. Nothing
signals that they open.

On the delete page those lists are the D-07 mechanism for checking what will be destroyed. If the admin cannot
find them, the "impossible to delete the wrong board by accident" safeguard is weaker. The desktop sheet keeps the
default display and is fine.

**Fix:** Keep the native marker and size the tap target without flex, for example:
```css
.board-delete-names > summary {
  display: list-item;
  min-height: var(--qb-hit-min);
  padding-block: var(--qb-space-xs);
  cursor: pointer;
}
```
Alternatively, keep flex and add an explicit chevron (a Font Awesome icon in the markup, rotated with
`details[open] > summary`).

### WR-04: The confirmation page silently omits any content kind it has no label for

**File:** `QuestBoard.Service/ViewModels/PlatformViewModels/BoardDeletePageViewModel.cs:63`
**Issue:** `.Where(item => item.Count > 0 && Labels.ContainsKey(item.Kind))` drops a `BoardContentKind` that has
no entry in `Labels`. The repository's `DeleteBoardAsync` would still delete those rows.

So adding a new board-owned kind has one guaranteed failure mode:
- The repository and its tests are updated (the preview/graph tests pin `Enum.GetValues<BoardContentKind>()`).
- The label is forgotten.
- The irreversible delete removes rows the page never showed.

No test asserts that every enum value has a label. On the one page whose job is to show everything that will go,
this should fail loudly, not silently.

**Fix:** Remove the `ContainsKey` filter and index `Labels[item.Kind]` directly, so a missing label throws.
Also add a unit test:
```csharp
Enum.GetValues<BoardContentKind>().Should().OnlyContain(k => BoardDeletePageViewModel.HasLabel(k));
```
Or test the same thing by rendering a preview with every kind at count 1 and asserting one row per kind.

## Info

### IN-01: The transaction sweep can delete a row whose own shop item lives on another board

**File:** `QuestBoard.Repository/GroupRepository.cs:207-210`
**Issue:** The second clause, `t.OriginalTransaction.ShopItem.GroupId == groupId`, deletes a transaction whose
own `ShopItemId` belongs to another board. That conflicts with D-05: rows on other boards are unlinked, not
deleted. The preview also counts such a row as this board's content, not under "Kept, but unlinked". It handles
only one chain level.

`ShopService` always copies `ShopItemId` from the original, so the row is unreachable today. The clause is
defensive code that would do the wrong thing if it ever fired.
**Fix:** Either drop the second clause, or treat such rows as cross-board: set `OriginalTransactionId = null` and
list them in the unlinked section.

### IN-02: The heading level skips on the phone page

**File:** `QuestBoard.Service/Areas/Platform/Views/Group/Delete.Mobile.cshtml:12`, `_BoardDeleteBody.cshtml:55,67,72`
**Issue:** The phone twin's card title is an `h1`, and the shared partial's section headings are `h3`. The phone
outline jumps from h1 to h3. On desktop the title is h2, so h3 follows correctly.
**Fix:** Pass the section heading level into the partial, or use `h2` for the phone title as the desktop twin
does. The guidelines allow either.

### IN-03: The vacuous body assertion after a redirect

**File:** `QuestBoard.IntegrationTests/Tests/TenantIsolationTests.cs:198`
**Issue:** The test now asserts a 302 to the picker. The body of a 302 is empty, so
`body.Should().NotContain("OnlyGroupOneQuest")` can never fail. The real guarantee is the direct
`CountAsync` check below it.
**Fix:** Remove the body assertion, or follow the redirect and assert on the picker page.

### IN-04: No test pins anti-forgery on the board delete POST

**File:** `QuestBoard.IntegrationTests/Controllers/PlatformBoardDeleteTests.cs`
**Issue:** `DeleteConfirmed` carries `[ValidateAntiForgeryToken]`, which is correct. `UsersControllerIntegrationTests`
has a `..._WithoutAntiForgeryToken_IsRejected` test for its POSTs, but the most destructive POST in the app has no
equivalent. Removing the attribute would go unnoticed.
**Fix:** Add `Post_WithoutAntiForgeryToken_IsRejectedAndKeepsTheBoard`.

### IN-05: The name-mismatch re-render is outside the friendly-error envelope

**File:** `QuestBoard.Service/Areas/Platform/Controllers/GroupController.cs:247-258`
**Issue:** `RenderNameMismatchAsync` calls `GetByIdAsync` and `GetDeletionPreviewAsync` outside the try/catch that
wraps the delete, so a database failure there surfaces as a 500. Nothing has been changed at that point, so this
is minor, but it is the one remaining 500 path on the delete POST.
**Fix:** Wrap the re-render in the same try/catch, then redirect to `Delete` with the friendly error.

---

_Reviewed: 2026-11-11T16:38:05Z_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
