# Phase 49: Fix Guild Members page missing group/tenant filtering - Context

**Gathered:** 2026-07-05
**Status:** Ready for planning

<domain>
## Phase Boundary

`GuildMembersController` (nav-labeled "Guild Members", backing the character/guild-member directory) currently has **no group/tenant scoping at all** — `Index` lists every character in the entire database regardless of which group the viewer is in, and `Details`/`GetProfilePicture` can be reached for any character ID with no ownership or group check. This phase makes the character list and character detail/picture endpoints respect the viewer's active group, matching the group-scoping already correctly applied to the sibling "Players" page (`PlayersController` / `UserRepository.GetAllDungeonMasters`/`GetAllPlayers`).

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
- `QuestBoard.Service/Middleware/GroupSessionMiddleware.cs` — confirms SuperAdmin bypasses the "must have an active group" gate (lines 65-69, checked before the group-required redirect), so SuperAdmin is the only role that can reach `GuildMembersController` with `ActiveGroupId == null`. Every other authenticated user always has a non-null `ActiveGroupId` here.

No external ADRs/specs beyond the codebase map above — requirements are fully captured in the decisions section.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `IActiveGroupContext.ActiveGroupId` — already DI-registered and used by `UserRepository`; `CharacterRepository` just needs it added to its constructor the same way (`QuestBoard.Repository/UserRepository.cs:10` shows the constructor-injection shape: `UserRepository(QuestBoardContext dbContext, IMapper mapper, IActiveGroupContext activeGroupContext)`).
- `DbContext.UserGroups` — the many-to-many join table already used for exactly this kind of "is this user in this group" check; no new schema needed.

### Established Patterns
- Group-filtered read methods return an empty list (not throw, not null) when `ActiveGroupId` is null — see `GetAllDungeonMasters`/`GetAllPlayers`. Character list methods should follow the same convention (D-03).
- Ownership checks in this same controller (`Edit`, `Delete`) use `Forbid()` for a same-tenant non-owner; that's a different scenario from cross-tenant access, which this phase treats as `NotFound()` per D-04 — don't conflate the two patterns.

### Integration Points
- `GetProfilePicture` (`GuildMembersController.cs:292-302`) is called from several other views' `<img>` tags via `Url.Action("GetProfilePicture", "GuildMembers", ...)` — `Views/Quest/_QuestCard.cshtml`, `Views/Quest/Manage.cshtml`, `Views/Quest/Details.cshtml`, `Views/QuestLog/Details.cshtml` (+ `.Mobile.cshtml` variants). All of these render pictures only for characters whose owners are already confirmed participants in the *current* group's quests, so adding a group check here should not break any of these legitimate call sites — verify this holds during planning/testing (e.g., a player who later leaves the group but has historical quest-log entries showing their character picture is an edge case worth a quick check, though out of scope to solve beyond "don't crash / graceful missing-image fallback already exists via `onerror` handlers in these views").

</code_context>

<specifics>
## Specific Ideas

User's original report: "The Guild Members page is not group filtered" — confirmed via code inspection to be `GuildMembersController.Index`, which has no scoping whatsoever (returns literally every character in the system). Discussion expanded scope to also cover the `Details`/`GetProfilePicture` direct-URL leak on the same controller, since it's the identical root cause.

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope (this controller's missing group filtering).

</deferred>

---

*Phase: 49-fix-guild-members-page-missing-group-tenant-filtering*
*Context gathered: 2026-07-05*
