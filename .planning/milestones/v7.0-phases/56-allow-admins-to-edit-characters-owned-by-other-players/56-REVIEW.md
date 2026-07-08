---
phase: 56-allow-admins-to-edit-characters-owned-by-other-players
reviewed: 2026-07-06T16:24:40Z
depth: standard
files_reviewed: 6
files_reviewed_list:
  - QuestBoard.Service/Controllers/Characters/GuildMembersController.cs
  - QuestBoard.Service/ViewModels/CharacterViewModels/CharacterViewModel.cs
  - QuestBoard.Service/Views/GuildMembers/Details.cshtml
  - QuestBoard.Service/Views/GuildMembers/Details.Mobile.cshtml
  - QuestBoard.IntegrationTests/Controllers/GuildMembersControllerIntegrationTests.cs
  - QuestBoard.Repository/CharacterRepository.cs
findings:
  critical: 1
  warning: 2
  info: 2
  total: 5
status: issues_found
---

# Phase 56: Code Review Report

**Reviewed:** 2026-07-06T16:24:40Z
**Depth:** standard
**Files Reviewed:** 6
**Status:** issues_found

## Summary

Reviewed the widening of `GuildMembersController`'s owner-only authorization guard to owner-OR-admin on `Edit`/`Delete`/`ToggleRetirement`, the new `CanEdit` flag on `CharacterViewModel`, its use in the `Details` views, and the `CharacterRepository.UpdateAsync` navigation-reconciliation fix.

The authorization guard logic itself is correct: `GetEffectiveGroupRoleAsync` folds the global `SuperAdmin` role into `GroupRole.Admin` server-side, `CharacterEntity` carries a group-scoped global query filter so an admin in group A can never reach a character in group B (confirmed by the `Edit_AdminEditingCharacterInDifferentGroup_ShouldReturnNotFound` / `Delete_AdminDeletingCharacterInDifferentGroup_ShouldReturnNotFound` tests), and every mutating action (Edit GET/POST, Delete, ToggleRetirement) applies the same `character.OwnerId != currentUser.Id && role != GroupRole.Admin` guard consistently. The `CharacterRepository.UpdateAsync` reconciliation logic correctly scopes "tracked" classes to the target character only, so a tampered class `Id` belonging to a different character cannot be adopted (it falls through to the insert-as-new branch instead).

However, tracing the full `Edit` POST call chain surfaced a critical, newly-reachable data-corruption bug: when an Admin edits another player's character and sets its `Role` to `Main` (a plain, unrestricted dropdown in the Edit form), `SetAsMainCharacterAsync` is called with the **admin's own** user id instead of the target character's owner id. This silently demotes all of the admin's own characters to `Backup` and drops the entire edit for the target character, since neither code path ends up calling `UpdateAsync` on `existingCharacter` for that branch. This is a direct, previously-unreachable consequence of this phase's authorization widening (self-edit was never exposed to this bug because `currentUser.Id` and the owner id always matched).

## Critical Issues

### CR-01: Admin editing another player's character to Main corrupts the admin's own roster and silently drops the edit

**File:** `QuestBoard.Service/Controllers/Characters/GuildMembersController.cs:273-280`
**Issue:**
```csharp
// If setting as main, update all user's characters
if (viewModel.Role == CharacterRole.Main && existingCharacter.Role != CharacterRole.Main)
{
    await characterService.SetAsMainCharacterAsync(id, currentUser.Id, token);
}
else
{
    await characterService.UpdateAsync(existingCharacter, token);
}
```
`SetAsMainCharacterAsync(characterId, userId)` (`QuestBoard.Domain/Services/CharacterService.cs:35-54`) loads `GetCharactersByOwnerIdAsync(userId)`, demotes every one of that user's characters to `Backup`, then looks for `characterId` among that same set to promote it to `Main`.

When an Admin (not the owner) edits `existingCharacter` and sets `viewModel.Role = CharacterRole.Main`, this call is made as `SetAsMainCharacterAsync(id, currentUser.Id, token)` — `currentUser.Id` is the **admin's** id, not `existingCharacter.OwnerId`. Consequences:
1. Every character actually owned by the admin gets silently demoted to `Backup` (unrelated data corruption, no relation to the character being edited).
2. `userCharacters.FirstOrDefault(c => c.Id == characterId)` returns `null` because the target character belongs to a different owner and is not in `userCharacters` — so the target character is never promoted to Main.
3. Worse, none of `existingCharacter`'s other pending scalar changes (Name, Level, Status, SheetLink, Description, Backstory, Classes, ProfilePicture — all assigned onto `existingCharacter` earlier in this action) are persisted at all, because this branch never calls `characterService.UpdateAsync(existingCharacter, ...)`. The admin's entire edit is silently lost with a 302 redirect implying success.

This is reachable through the ordinary Edit form — `Role` is a plain `<select>` with no ownership-based restriction (`Views/GuildMembers/Edit.cshtml:60-61`, `Edit.Mobile.cshtml:54-55`) — and is not covered by any existing test (all "admin editing" tests in `GuildMembersControllerIntegrationTests.cs` only exercise the GET action or a POST branch that doesn't touch `Role`).

**Fix:** Pass the character's actual owner id, not the acting user's id:
```csharp
if (viewModel.Role == CharacterRole.Main && existingCharacter.Role != CharacterRole.Main)
{
    await characterService.SetAsMainCharacterAsync(id, existingCharacter.OwnerId, token);
}
else
{
    await characterService.UpdateAsync(existingCharacter, token);
}
```
Additionally, note that `SetAsMainCharacterAsync` (as invoked) never applies the other scalar/Classes/ProfilePicture changes made to `existingCharacter` — even after fixing the owner id, promoting to Main will skip persisting the rest of the edited fields. Consider persisting `existingCharacter`'s other fields via `UpdateAsync` in both branches (or having `SetAsMainCharacterAsync` accept the already-mutated character and update it directly) so a "promote to Main + edit other fields in the same submit" case doesn't silently drop the other field changes for *any* caller, not just the admin path.

## Warnings

### WR-01: `Details` computes `CanEdit` without covering the same `IsOwner` semantics used elsewhere for null-safety consistency

**File:** `QuestBoard.Service/Controllers/Characters/GuildMembersController.cs:58-72`
**Issue:** `GetUserAsync` (`QuestBoard.Domain/Services/UserService.cs:96-100`) never returns `null` — on a missing user it returns `new User()` with `Id` defaulting to `0`. The `currentUser != null` checks throughout `GuildMembersController` (`Details`, `Edit`, `Delete`, `ToggleRetirement`) are therefore always `true` in practice, and the real protection against an unresolvable identity is the accidental equality `character.OwnerId == 0`, which would incorrectly evaluate to `true` if a character ever legitimately has `OwnerId == 0` (e.g., a seeded/orphaned row). This is a latent inconsistency in how "current user is valid" is checked (pre-dates this phase, but this phase added a second, parallel `role` computation on top of the same shaky `currentUser != null` guard in `Details`), and could mask an unresolved-identity bug as ordinary owner/non-owner logic instead of failing loudly.
**Fix:** Prefer a check against `currentUser.Id != 0` (or have `GetUserAsync` return a nullable `User?` and update call sites) so a genuinely-unresolvable identity is distinguishable from `OwnerId == 0`. Out of scope to fix broadly in this phase, but worth flagging since `Details`'s new `role` computation now depends on the same guard.

### WR-02: Duplicated owner-or-admin guard block repeated four times with no shared helper

**File:** `QuestBoard.Service/Controllers/Characters/GuildMembersController.cs:177-181, 214-218, 304-308, 334-338`
**Issue:** The exact same three lines (`GetEffectiveGroupRoleAsync` call + `character.OwnerId != currentUser.Id && role != GroupRole.Admin` + `Forbid()`) are duplicated across `Edit` GET, `Edit` POST, `Delete`, and `ToggleRetirement`. This is a correctness risk going forward — a future change to the authorization rule (e.g., adding a `DungeonMaster` bypass, or changing the semantics for a specific action) requires remembering to update all four call sites identically. It already required copy-pasting a 4-line comment four times.
**Fix:** Extract a small private helper, e.g.:
```csharp
private async Task<bool> CanManageCharacterAsync(User currentUser, Character character)
{
    if (character.OwnerId == currentUser.Id) return true;
    var role = await userService.GetEffectiveGroupRoleAsync(User, activeGroupContext.RequireActiveGroupId());
    return role == GroupRole.Admin;
}
```
and replace each guard with `if (!await CanManageCharacterAsync(currentUser, character)) return Forbid();`. Reduces the chance that a future edit to one call site silently diverges from the others.

## Info

### IN-01: `Details` GET always resolves `GetEffectiveGroupRoleAsync` even for characters the user already owns

**File:** `QuestBoard.Service/Controllers/Characters/GuildMembersController.cs:61-65`
**Issue:** When `isOwner` is already `true`, the extra `GetEffectiveGroupRoleAsync` round-trip to resolve `role` for the `CanEdit` computation is unnecessary (short-circuiting `isOwner || role == GroupRole.Admin` would skip it). Not a correctness bug — purely a minor, avoidable extra async/DB call on every page view for a character's own owner. Performance is out of scope per review policy, noting only because it's a one-line, no-risk simplification alongside the guard-extraction suggested in WR-02.
**Fix:**
```csharp
GroupRole? role = isOwner ? null : await userService.GetEffectiveGroupRoleAsync(User, activeGroupContext.RequireActiveGroupId());
```

### IN-02: No integration test exercises the Admin-edits-and-sets-Role-to-Main path

**File:** `QuestBoard.IntegrationTests/Controllers/GuildMembersControllerIntegrationTests.cs`
**Issue:** The test suite added by this phase (`Edit_AdminEditingAnotherPlayersCharacter_ShouldSucceed`, `Delete_AdminDeletingAnotherPlayersCharacter_ShouldSucceed`, `ToggleRetirement_AdminTogglingAnotherPlayersCharacter_ShouldSucceed`, etc.) exercises the GET Edit page, Delete, and ToggleRetirement thoroughly, but no test performs an actual admin `POST /GuildMembers/Edit` with changed field values (particularly `Role = Main`), which is exactly the path that hides CR-01. `Edit_AdminEditingAnotherPlayersCharacter_ShouldSucceed` only asserts the GET returns `200 OK` — it never submits the form.
**Fix:** Add a POST-based test, e.g. `Edit_AdminEditingAnotherPlayersCharacterSetAsMain_ShouldPersistChangesAndPromoteCorrectOwner`, that submits the edit form as an admin, sets `Role = Main`, and asserts (a) the target character's fields were actually persisted, and (b) the target character (not one of the admin's own) is the one promoted to Main.

---

_Reviewed: 2026-07-06T16:24:40Z_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
