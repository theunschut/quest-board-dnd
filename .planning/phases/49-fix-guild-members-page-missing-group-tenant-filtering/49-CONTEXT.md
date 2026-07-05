# Phase 49: Fix Guild Members page missing group/tenant filtering - Context

**Gathered:** 2026-07-05
**Status:** Ready for planning

<domain>
## Phase Boundary

Two controllers have confirmed cross-group ("tenant") leaks; both are in scope for this phase:

1. **`GuildMembersController`** (nav-labeled "Guild Members", backing the character/guild-member directory) currently has **no group/tenant scoping at all** — `Index` lists every character in the entire database regardless of which group the viewer is in, and `Details`/`GetProfilePicture` can be reached for any character ID with no ownership or group check. Fix makes the character list and character detail/picture endpoints respect the viewer's active group, matching the group-scoping already correctly applied to the sibling "Players" page (`PlayersController` / `UserRepository.GetAllDungeonMasters`/`GetAllPlayers`).

2. **`DungeonMasterController`** (`/DungeonMaster/Profile/{id}` and related actions) has the same class of bug, discovered while confirming the Quest History list was already correctly filtered (it is — see D-05): `Profile`, `EditProfile` (GET **and** POST), and `GetDMProfilePicture` never check that the target user ID is a member of the viewer's active group. `EditProfile`'s POST path is the more severe of the two controllers' issues — it lets an Admin of one group **overwrite** the bio/profile picture of a DM in an unrelated group, not just view it.

</domain>

<decisions>
## Implementation Decisions

### Fix scope
- **D-01:** Fix both the Index list AND the direct-URL leak on `Details`/`GetProfilePicture` in the same phase — they're the same root cause (missing group filter) on the same controller, not separate bugs. Today, any authenticated user can view or fetch the profile picture of any character by ID, regardless of group, by editing the URL directly (`Details` and `GetProfilePicture` have zero ownership/group check — unlike `Edit`/`Delete`, which already check `character.OwnerId != currentUser.Id`).

### Group-scoping mechanism
- **D-02:** Filter characters by "owner is a member of the active group," reusing the exact pattern already established in `UserRepository.GetAllDungeonMasters`/`GetAllPlayers` (`QuestBoard.Repository/UserRepository.cs:20-48`): inject `IActiveGroupContext` and filter via `DbContext.UserGroups.Any(ug => ug.UserId == <ownerId> && ug.GroupId == activeGroupId)`. `CharacterEntity` has no `GroupId` column of its own (only `OwnerId` — see `QuestBoard.Repository/Entities/CharacterEntity.cs`); characters are not per-group entities like `QuestEntity`/`ShopItemEntity` (which get automatic EF Core global query filters per `.planning/codebase/ARCHITECTURE.md`). Do not add a `GroupId` column or migration — reuse the existing many-to-many `UserGroups` join, same as Users.

### SuperAdmin with no active group
- **D-03:** When `IActiveGroupContext.ActiveGroupId` is null (only reachable by SuperAdmin — `GroupSessionMiddleware` enforces a non-null active group for everyone else before requests reach this controller), Guild Members Index returns an empty list. This matches `UserRepository.GetAllDungeonMasters`/`GetAllPlayers`'s existing `if (groupId == null) return [];` behavior — no cross-group "superview" for this page.

### Cross-group direct-link response
- **D-04:** When `Details(id)` or `GetProfilePicture(id)` is requested for a character outside the viewer's active group, return **404 Not Found** (hide existence entirely), not 403 Forbidden. This follows the multi-tenant-isolation convention of not confirming another tenant's data exists, rather than the ownership-check `Forbid()` pattern already used by this controller's `Edit`/`Delete` actions (which is a same-tenant "you can view it but not edit it" distinction, not applicable here).

### Claude's Discretion
- Exact implementation shape for the group check on `Details`/`GetProfilePicture` (e.g., whether to add a new repository method that joins through `Owner`/`UserGroups`, or fetch-then-check-then-404 in the controller/service layer). Follow whichever is more consistent with `ICharacterRepository`/`ICharacterService`'s existing method shapes (`QuestBoard.Domain/Interfaces/ICharacterRepository.cs`, `QuestBoard.Domain/Interfaces/ICharacterService.cs`).
- Whether "MyCharacters" (the current user's own characters) needs any special-case handling — it doesn't: once the underlying list is scoped to "owners who are members of the active group," `MyCharacters`/`OtherCharacters` in `GuildMembersController.Index` (`QuestBoard.Service/Controllers/Characters/GuildMembersController.cs:29-42`) split from that already-filtered list unchanged, since a user viewing their active group is by definition a member of it.

### Investigated and ruled out — DungeonMasterProfile Quest History
- **D-05:** The user asked whether the "Quest History" list on `DungeonMasterController.Profile` (`QuestBoard.Service/Controllers/DungeonMaster/DungeonMasterController.cs:20-46`) also needs group filtering. Investigated and confirmed **no change needed**: `GetQuestsByDungeonMasterAsync` (`QuestBoard.Repository/QuestRepository.cs:239-247`) queries `DbContext.Quests` with no `.IgnoreQueryFilters()`, so it already inherits the automatic global query filter on `QuestEntity` (`QuestBoard.Repository/Entities/QuestBoardContext.cs:244-247`: `ActiveGroupId == null || e.GroupId == ActiveGroupId`). For every non-SuperAdmin viewer, Quest History is already correctly scoped to the active group. For SuperAdmin (`ActiveGroupId == null`), the filter's "null = see all" behavior shows quests across every group that DM has run — matching the app's existing intentional design for SuperAdmin elsewhere (e.g. `/quests`, per `.planning/codebase/CONCERNS.md`), not an oversight. User confirmed this is a non-issue once the filtering was explained.

### DungeonMasterController fix scope
- **D-06:** While confirming D-05, found a real (separate) leak on the same page: `Profile`/`GetDMProfilePicture` never check that the viewed `id` belongs to a member of the viewer's active group (read leak — bio/name/profile picture visible for any user ID, any group). Worse, `EditProfile` (GET+POST) only checks "is the current user the profile owner, or an Admin in *their own* active group" — it never checks the *target* user is in that group, so an Admin in Group A can submit the edit form and overwrite the bio/profile picture of a DM in unrelated Group Z (a write-path cross-tenant vulnerability, not just disclosure). User confirmed: fix all three (`Profile`, `EditProfile` GET+POST, `GetDMProfilePicture`) in this same phase, not just the read-only pair.

### DungeonMasterController group-scoping mechanism
- **D-07:** Use the existing `IUserService.GetGroupRoleByIdAsync(int userId, int groupId)` (`QuestBoard.Domain/Interfaces/IUserService.cs:79`, "Returns the given user's group role in the specified group, or null if they are not a member.") to check whether the target `id` is a member of `activeGroupContext.ActiveGroupId`. No new repository plumbing needed — this primitive already exists and already returns null for non-members. Apply the check identically in `Profile`, both `EditProfile` overloads, and `GetDMProfilePicture`.

### DungeonMasterController — SuperAdmin with no active group
- **D-08:** When `ActiveGroupId` is null (SuperAdmin only — same reasoning as D-03), treat every DM-profile-related action as inaccessible (`NotFound()`) rather than allowing a cross-group superview. Consistent with D-03's Guild Members convention. Do not call `GetGroupRoleByIdAsync` with a null group id — short-circuit to `NotFound()` before that call when `ActiveGroupId` is null.

### DungeonMasterController — cross-group response code
- **D-09:** `Profile`, `EditProfile` (GET+POST), and `GetDMProfilePicture` all return **404 Not Found** (not 403 Forbidden) when the target user is outside the viewer's active group — same convention as D-04. This is distinct from `EditProfile`'s existing same-tenant `Forbid()` (a same-group non-owner, non-Admin editor) — that check is unaffected and stays as-is; the new group-membership check runs first/separately and 404s before the existing Forbid-based ownership check would even be reached for an out-of-group target.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### The bug (controller with zero group scoping)
- `QuestBoard.Service/Controllers/Characters/GuildMembersController.cs` — `Index` (lines 17-45) calls `characterService.GetAllCharactersWithDetailsAsync()` with no group filter; `Details` (lines 47-61) and `GetProfilePicture` (lines 292-302) have no ownership/group check at all (unlike `Edit`/`Delete`, which check `OwnerId`).

### The established fix pattern (sibling page that already does this correctly)
- `QuestBoard.Service/Controllers/QuestBoard/PlayersController.cs` — `Index` (lines 9-23), the "Players" nav page, correctly scoped.
- `QuestBoard.Repository/UserRepository.cs` — `GetAllDungeonMasters` (lines 20-33) and `GetAllPlayers` (lines 36-48): the exact `IActiveGroupContext.ActiveGroupId` + `DbContext.UserGroups.Any(...)` pattern to replicate for characters, including the `if (groupId == null) return [];` SuperAdmin behavior (D-03). `GetAllGroupMembers` (lines 51-59) is the "any role, just membership" variant — closest analog to what Characters needs (character visibility isn't role-gated the way DM/Player lists are).
- `QuestBoard.Domain/Interfaces/IActiveGroupContext.cs` — the `ActiveGroupId` (nullable int) contract to inject into `CharacterRepository`.
- `QuestBoard.Domain/Extensions/ActiveGroupContextExtensions.cs` — `RequireActiveGroupId()` fail-fast helper; explicitly NOT for SuperAdmin/see-all paths (its own doc comment warns against this) — do not use it here given D-03.

### Data model (why there's no GroupId column to filter on directly)
- `QuestBoard.Repository/Entities/CharacterEntity.cs` — `OwnerId`/`Owner` (FK to `UserEntity`), no `GroupId`. Group membership must be resolved transitively via `Owner` → `UserGroups`.
- `.planning/codebase/ARCHITECTURE.md` (multi-tenancy section) — documents that only `QuestEntity`/`ShopItemEntity` carry a direct `GroupId` with automatic EF Core global query filters; `User`-adjacent entities (including, now, `Character`) are filtered manually via `IActiveGroupContext`, per the `UserRepository` pattern above.

### Code to modify
- `QuestBoard.Domain/Interfaces/ICharacterRepository.cs` / `ICharacterService.cs` — current signatures for `GetAllCharactersWithDetailsAsync`, `GetCharacterWithDetailsAsync`, `GetCharacterProfilePictureAsync` (none take a group/owner-scope parameter today).
- `QuestBoard.Repository/CharacterRepository.cs` — `GetAllCharactersWithDetailsAsync` (lines 12-23), `GetCharacterWithDetailsAsync` (lines 41-49), `GetCharacterProfilePictureAsync` (lines 62-68) — the three queries that need group scoping.
- `QuestBoard.Domain/Services/CharacterService.cs` — thin pass-through wrappers around the repository; likely needs matching signature updates.

### Middleware context (why this matters differently for SuperAdmin vs. everyone else)
- `QuestBoard.Service/Middleware/GroupSessionMiddleware.cs` — confirms SuperAdmin bypasses the "must have an active group" gate (lines 65-69, checked before the group-required redirect), so SuperAdmin is the only role that can reach `GuildMembersController`/`DungeonMasterController` with `ActiveGroupId == null`. Every other authenticated user always has a non-null `ActiveGroupId` here.

### The second bug (DungeonMasterController — read leak + write-path leak)
- `QuestBoard.Service/Controllers/DungeonMaster/DungeonMasterController.cs` — `Profile` (lines 19-46), `EditProfile` GET (lines 48-76) and POST (lines 78-119), `GetDMProfilePicture` (lines 121-134): none check that the target `id` belongs to a member of `activeGroupContext.ActiveGroupId`. `GetEffectiveRoleAsync` (lines 139-142, private helper) already shows the SuperAdmin-short-circuit pattern to mirror for D-08 (`User.IsInRole("SuperAdmin") ? GroupRole.Admin : ...` — but per D-08, DM-profile actions should 404 for SuperAdmin-with-no-group instead, not short-circuit to Admin).
- `QuestBoard.Domain/Interfaces/IUserService.cs:79` — `GetGroupRoleByIdAsync(int userId, int groupId)`: the existing "is this user a member of this group" primitive to reuse per D-07 (already implemented in `UserService.cs:84-87` → `repository.GetGroupRoleAsync(userId, groupId)`).
- `QuestBoard.Domain/Interfaces/IDungeonMasterProfileService.cs` / `QuestBoard.Domain/Services/DungeonMasterProfileService.cs` — `GetProfileByUserIdAsync`, `GetProfilePictureAsync`, `UpsertProfileAsync` — these operate purely on `userId`; the group check belongs in the controller (or a thin wrapper), not in these methods, since `DungeonMasterProfileEntity` has no group concept of its own (same shape as `CharacterEntity` — see D-02's data-model reasoning, which applies equally here).

No external ADRs/specs beyond the codebase map above — requirements are fully captured in the decisions section.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `IActiveGroupContext.ActiveGroupId` — already DI-registered and used by `UserRepository`; `CharacterRepository` just needs it added to its constructor the same way (`QuestBoard.Repository/UserRepository.cs:10` shows the constructor-injection shape: `UserRepository(QuestBoardContext dbContext, IMapper mapper, IActiveGroupContext activeGroupContext)`).
- `DbContext.UserGroups` — the many-to-many join table already used for exactly this kind of "is this user in this group" check; no new schema needed.

### Established Patterns
- Group-filtered read methods return an empty list (not throw, not null) when `ActiveGroupId` is null — see `GetAllDungeonMasters`/`GetAllPlayers`. Character list methods should follow the same convention (D-03); DungeonMasterController's single-record equivalent is `NotFound()` (D-08).
- Ownership checks in this same controller (`Edit`, `Delete`) use `Forbid()` for a same-tenant non-owner; that's a different scenario from cross-tenant access, which this phase treats as `NotFound()` per D-04 — don't conflate the two patterns. `DungeonMasterController.EditProfile` already has an analogous same-tenant `Forbid()` (lines 62-65, 92-96) for "not the owner and not Admin" — the new cross-group check (D-09) is additive, checked separately, and should 404 before that existing Forbid logic is reached for an out-of-group target.
- `GetEffectiveRoleAsync` (`DungeonMasterController.cs:139-142`) already demonstrates the "SuperAdmin short-circuits before calling `RequireActiveGroupId()`" pattern — reuse the same ordering (check role/null-group before touching group-scoped data) when adding D-07/D-08's checks.

### Integration Points
- `GetProfilePicture` (`GuildMembersController.cs:292-302`) is called from several other views' `<img>` tags via `Url.Action("GetProfilePicture", "GuildMembers", ...)` — `Views/Quest/_QuestCard.cshtml`, `Views/Quest/Manage.cshtml`, `Views/Quest/Details.cshtml`, `Views/QuestLog/Details.cshtml` (+ `.Mobile.cshtml` variants). All of these render pictures only for characters whose owners are already confirmed participants in the *current* group's quests, so adding a group check here should not break any of these legitimate call sites — verify this holds during planning/testing (e.g., a player who later leaves the group but has historical quest-log entries showing their character picture is an edge case worth a quick check, though out of scope to solve beyond "don't crash / graceful missing-image fallback already exists via `onerror` handlers in these views").
- `GetDMProfilePicture` (`DungeonMasterController.cs:121-134`) is only referenced from `Views/DungeonMaster/Profile.cshtml`/`.Mobile.cshtml` and `EditProfile.cshtml`/`.Mobile.cshtml` (unlike `GetProfilePicture`, it is not embedded in unrelated Quest views) — narrower blast radius to verify than the Characters equivalent.

</code_context>

<specifics>
## Specific Ideas

User's original report: "The Guild Members page is not group filtered" — confirmed via code inspection to be `GuildMembersController.Index`, which has no scoping whatsoever (returns literally every character in the system). Discussion expanded scope to also cover the `Details`/`GetProfilePicture` direct-URL leak on the same controller, since it's the identical root cause.

User then asked whether `DungeonMasterProfile`'s "Quest History" list needed the same fix. Investigation showed that specific list is already correctly filtered (D-05), but surfaced a real, separate leak on the same page (`Profile`/`EditProfile`/`GetDMProfilePicture` — D-06 through D-09), which the user chose to pull into this phase's scope given the phase's overall theme of closing tenant-filtering leaks.

</specifics>

<deferred>
## Deferred Ideas

None — both confirmed leaks (Characters, DungeonMasterController) are in scope for this phase. Quest History was investigated and ruled out as already correct (D-05).

</deferred>

---

*Phase: 49-fix-guild-members-page-missing-group-tenant-filtering*
*Context gathered: 2026-07-05*
