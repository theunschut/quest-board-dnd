---
phase: 56-allow-admins-to-edit-characters-owned-by-other-players
fixed_at: 2026-07-06T16:40:00Z
review_path: .planning/phases/56-allow-admins-to-edit-characters-owned-by-other-players/56-REVIEW.md
iteration: 1
findings_in_scope: 3
fixed: 3
skipped: 0
status: all_fixed
---

# Phase 56: Code Review Fix Report

**Fixed at:** 2026-07-06T16:40:00Z
**Source review:** .planning/phases/56-allow-admins-to-edit-characters-owned-by-other-players/56-REVIEW.md
**Iteration:** 1

**Summary:**
- Findings in scope: 3 (CR-01, WR-01, WR-02 — critical_warning scope; IN-01/IN-02 out of scope)
- Fixed: 3
- Skipped: 0

## Fixed Issues

### CR-01: Admin editing another player's character to Main corrupts the admin's own roster and silently drops the edit

**Files modified:** `QuestBoard.Service/Controllers/Characters/GuildMembersController.cs`, `QuestBoard.IntegrationTests/Controllers/GuildMembersControllerIntegrationTests.cs`
**Commits:** `e2ebafb`, `387b164`
**Applied fix:** In the `Edit` POST action, `characterService.UpdateAsync(existingCharacter, token)` is now called unconditionally, persisting all edited fields (Name, Level, Status, SheetLink, Description, Backstory, Classes, ProfilePicture) before the Main-promotion branch runs. When `viewModel.Role == CharacterRole.Main`, `SetAsMainCharacterAsync` is now called with `existingCharacter.OwnerId` (the target character's actual owner) instead of `currentUser.Id` (the acting admin's id), so an admin promoting another player's character to Main no longer demotes their own roster and no longer silently drops the rest of the edit.

Also added an integration test, `Edit_AdminEditingAnotherPlayersCharacterSetAsMain_ShouldPersistChangesAndPromoteCorrectOwner`, which performs an admin `POST /GuildMembers/Edit` on another player's character with `Role=Main` and a full set of changed fields, and asserts: (a) all edited fields were persisted on the target character, (b) the target character was promoted to `Main`, and (c) the admin's own pre-existing `Main` character was left untouched (not demoted).

**Note:** the underlying `Edit` logic change touches a Main-promotion code path shared by every caller (not just admins), so this is flagged for human verification per the logic-fix policy — see verification note below.

### WR-01: `Details` computes `CanEdit` without covering the same `IsOwner` semantics used elsewhere for null-safety consistency

**Files modified:** `QuestBoard.Service/Controllers/Characters/GuildMembersController.cs`
**Commit:** `2da1efa`
**Applied fix:** In the `Details` action, replaced the always-true `currentUser != null` checks with `currentUser.Id != 0`, matching the fact that `GetUserAsync` never returns `null` (an unresolvable identity comes back as `new User()` with `Id == 0`). This makes an unresolved identity distinguishable from a character legitimately having `OwnerId == 0`. Scoped narrowly to `Details` (the location cited in the finding) rather than expanded to the other `currentUser == null` checks elsewhere in the controller, per the review's note that broader cleanup is out of scope for this phase.

### WR-02: Duplicated owner-or-admin guard block repeated four times with no shared helper

**Files modified:** `QuestBoard.Service/Controllers/Characters/GuildMembersController.cs`
**Commit:** `ad9c9a2`
**Applied fix:** Extracted a private `CanManageCharacterAsync(User currentUser, Character character)` helper implementing the owner-or-admin check, and replaced all four duplicated guard blocks (`Edit` GET, `Edit` POST, `Delete`, `ToggleRetirement`) with `if (!await CanManageCharacterAsync(currentUser, character)) return Forbid();`.

## Verification

- `dotnet build QuestBoard.Service` — succeeded after each fix (0 errors, 0 warnings).
- `dotnet test QuestBoard.IntegrationTests --filter FullyQualifiedName~GuildMembersControllerIntegrationTests` — 16/16 passed after each fix.
- Full `dotnet test` (all projects) — 183/183 unit tests passed, 330/330 integration tests passed. No regressions.

**Requires human verification:** CR-01's fix changes control flow in a shared code path (`UpdateAsync` now always runs before the conditional `SetAsMainCharacterAsync` call, rather than the two being mutually exclusive). This is a logic change beyond a pure one-line correction, so please manually confirm the new ordering is correct for the self-edit case too (owner editing their own character and setting Role=Main) in addition to the admin case exercised by the new test, even though the full suite passed and the new integration test explicitly covers the admin path.

## Skipped Issues

None — all in-scope findings were fixed.

---

_Fixed: 2026-07-06T16:40:00Z_
_Fixer: Claude (gsd-code-fixer)_
_Iteration: 1_
