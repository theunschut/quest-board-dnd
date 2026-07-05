# Phase 49: Fix Guild Members page missing group/tenant filtering - Context

**Gathered:** 2026-07-05
**Status:** Ready for planning

<domain>
## Phase Boundary

Three areas have confirmed or hardened cross-group ("tenant") leaks; all are in scope for this phase:

1. **`GuildMembersController`** (nav-labeled "Guild Members", backing the character/guild-member directory) currently has **no group/tenant scoping at all** — `Index` lists every character in the entire database regardless of which group the viewer is in, and `Details`/`GetProfilePicture` can be reached for any character ID with no ownership or group check. Fix makes the character list and character detail/picture endpoints respect the viewer's active group, matching the group-scoping already correctly applied to the sibling "Players" page (`PlayersController` / `UserRepository.GetAllDungeonMasters`/`GetAllPlayers`).

2. **`DungeonMasterController`** (`/DungeonMaster/Profile/{id}` and related actions) has the same class of bug, discovered while confirming the Quest History list was already correctly filtered (it is — see D-05): `Profile`, `EditProfile` (GET **and** POST), and `GetDMProfilePicture` never check that the target user ID is a member of the viewer's active group. `EditProfile`'s POST path is the more severe of the two controllers' issues — it lets an Admin of one group **overwrite** the bio/profile picture of a DM in an unrelated group, not just view it.

3. **`UserTransactionEntity`/`UserTransactionRepository`** — validated via empirical testing (see D-10) that this is *currently* safe, but only by an undocumented accident of EF Core's query translation, not by design. Harden it: document the real mechanism, close the one unguarded call site, and add a regression test so a future refactor can't silently reopen the leak.

</domain>

<decisions>
## Implementation Decisions

### Fix scope
- **D-01:** Fix both the Index list AND the direct-URL leak on `Details`/`GetProfilePicture` in the same phase — they're the same root cause (missing group filter) on the same controller, not separate bugs. Today, any authenticated user can view or fetch the profile picture of any character by ID, regardless of group, by editing the URL directly (`Details` and `GetProfilePicture` have zero ownership/group check — unlike `Edit`/`Delete`, which already check `character.OwnerId != currentUser.Id`).

### Group-scoping mechanism — REVISED: schema change, not a manual join
- **D-02 (superseded by this revision):** The original plan was to filter characters by "owner is a member of the active group" via a manual `DbContext.UserGroups.Any(...)` join, with no schema change. **User overrode this after the migration/backfill discussion below** — add a real `GroupId` column to `CharacterEntity` instead, mirroring `QuestEntity`/`ShopItemEntity` exactly:
  - **Migration:** follow the existing precedent in `QuestBoard.Repository/Migrations/20260630055221_AddGroupSchema.cs` (the original migration that added `GroupId` to `Quests`/`ShopItems`) step-for-step: add the column with a temporary `defaultValue`, backfill via `migrationBuilder.Sql("UPDATE Characters SET GroupId = 1")` (per the user: **all existing rows belong to group 1** — this is the only group that existed before multi-tenancy), then add the FK constraint to `Groups(Id)` and an index, in that order (FK added *after* backfill, matching the original migration's own comment about avoiding a constraint violation on rows that don't have a group yet).
  - **Global query filter:** add `modelBuilder.Entity<CharacterEntity>().HasQueryFilter(...)` in `QuestBoardContext.cs`, alongside the existing `QuestEntity`/`ShopItemEntity` filters — but with a **deliberately different null-handling shape**, see D-03 below.
  - **Creation-time stamp:** set `character.GroupId = activeGroupContext.RequireActiveGroupId();` in `GuildMembersController`'s `Create` POST action before calling `characterService.AddAsync(character, token)` — mirrors `QuestController.cs:122-124`'s exact existing pattern (`quest.GroupId = activeGroupContext.RequireActiveGroupId();`) for the identical reason (a DungeonMaster/Player creating a character is never in the null-active-group state — `GroupSessionMiddleware` guarantees it for non-SuperAdmin, and Character creation isn't a SuperAdmin action).
  - **Why this is better than the manual-join plan:** once the model-level filter exists, `GetAllCharactersWithDetailsAsync()` and `GetCharacterWithDetailsAsync(id)` (both already plain LINQ queries against `DbContext.Characters`, not `.Find()`) become automatically, transparently scoped — no bespoke `.Where`/join code needed in the repository at all. This also means Character now gets the same *durable* protection Quest/ShopItem have (every future query is automatically safe, not just the ones a developer remembers to filter) — directly addressing the exact fragility the UserTransaction investigation (D-10/D-11) surfaced for join-based/incidental approaches.

### SuperAdmin with no active group — REVISED filter shape
- **D-03 (kept, now implemented differently):** SuperAdmin with `ActiveGroupId == null` still sees an **empty** Guild Members list, not a cross-group superview — this decision itself is unchanged. But it's now enforced by the *filter's own shape*, not a manual `if (groupId == null) return [];` check: `CharacterEntity`'s filter must be `activeGroupContext.ActiveGroupId != null && e.GroupId == activeGroupContext.ActiveGroupId` — **no** `ActiveGroupId == null ||` escape hatch, unlike `QuestEntity`/`ShopItemEntity`'s filters (`QuestBoardContext.cs:244-252`), which deliberately use `== null ||` to mean "SuperAdmin sees all" for quests/shop items. This is an intentional, one-line divergence in filter shape from the existing pattern — **add a comment explaining why** (Character deliberately does not offer SuperAdmin a cross-group view, unlike Quest/ShopItem) so a future reader doesn't "fix" it to match the other two filters.
  - **Context on when this null case actually happens** (confirmed via `ActiveGroupContextService.cs:21-24`, which reads `ActiveGroupId` from `HttpContext.Session`): it is *not* a routine SuperAdmin workflow. Once a SuperAdmin selects a group via GroupPicker (same as anyone else), `ActiveGroupId` is set for the rest of that session and behaves like a regular user. It's null only in the narrow window right after login, before any group has been picked in that session — reachable only because `GroupSessionMiddleware` (`GroupSessionMiddleware.cs:65-69`) lets SuperAdmin bypass the forced-redirect-to-`/groups/pick` that every other role goes through.

### Cross-group direct-link response
- **D-04:** When `Details(id)` or `GetProfilePicture(id)` is requested for a character outside the viewer's active group, return **404 Not Found** (hide existence entirely), not 403 Forbidden. This follows the multi-tenant-isolation convention of not confirming another tenant's data exists, rather than the ownership-check `Forbid()` pattern already used by this controller's `Edit`/`Delete` actions (which is a same-tenant "you can view it but not edit it" distinction, not applicable here).
  - **`Details(id)` gets this for free** once D-02's filter exists: `GetCharacterWithDetailsAsync(id)` is already a plain `.FirstOrDefaultAsync(c => c.Id == id, ...)` query against `DbContext.Characters`, so the filter naturally makes it return `null` for an out-of-group id, and the controller's existing `if (character == null) return NotFound();` already handles the rest — no new code needed for `Details`.
  - **`GetProfilePicture(id)` does NOT get this for free — needs a rewrite.** `CharacterRepository.GetCharacterProfilePictureAsync(id)` currently queries `DbContext.CharacterImages` directly (`QuestBoard.Repository/CharacterRepository.cs:62-68`) — `CharacterImageEntity` is a *sibling* shared-primary-key entity (`[Key][ForeignKey(nameof(Character))] public int Id`, see `QuestBoard.Repository/Entities/CharacterImageEntity.cs`) with no `GroupId` of its own and no filter of its own. Querying it as the root bypasses `CharacterEntity`'s filter entirely — this is the *exact same class of gap* the D-10 UserTransaction investigation found (a required-FK sibling isn't protected unless the query is rooted at the filtered parent). **Fix:** rewrite the query to route through `DbContext.Characters` as the root, e.g. `DbContext.Characters.Where(c => c.Id == id).Select(c => c.ProfileImage != null ? c.ProfileImage.ImageData : null).FirstOrDefaultAsync(token)`, so the `CharacterEntity` filter actually applies.

### Claude's Discretion
- Exact EF Core migration mechanics (temporary default value, column nullability sequencing, index naming) — follow `20260630055221_AddGroupSchema.cs`'s existing structure precisely rather than inventing a new shape.
- Whether "MyCharacters" (the current user's own characters) needs any special-case handling — it doesn't: once the underlying list is scoped by the new filter, `MyCharacters`/`OtherCharacters` in `GuildMembersController.Index` (`QuestBoard.Service/Controllers/Characters/GuildMembersController.cs:29-42`) split from that already-filtered list unchanged, since a user viewing their active group is by definition a member of it.

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

### DungeonMasterProfile — explicitly stays schema-unchanged (considered and rejected)
- **D-09a:** When the Character fix above was revised to add a real `GroupId` column (D-02), the user asked whether `DungeonMasterProfileEntity` should get the same treatment. **Considered and explicitly rejected**: `DungeonMasterProfileEntity.Id` *is* `UserId` (`[Key][DatabaseGenerated(DatabaseGeneratedOption.None)]`, see `QuestBoard.Repository/Entities/DungeonMasterProfileEntity.cs:9-11`) — a strict one-row-per-user table today, unlike `CharacterEntity` which already has its own auto-generated `Id`. Adding `GroupId` here (backfilled to 1) would silently turn a DM's shared bio/photo into a group-specific one: a DM in two groups would see their profile only when viewing from whichever single group got backfilled, and nothing from any other group they're in — a real behavior regression, not just a security fix, for anyone in multiple groups. **Decision: keep `DungeonMasterProfileEntity` exactly as-is (no `GroupId`, no migration, still shared across all of a DM's groups).** D-06 through D-09 (the `GetGroupRoleByIdAsync`-based access check) remain the correct and complete mechanism for DungeonMasterController — they gate *who can view/edit* the profile without touching how the profile *data itself* is stored or shared.

### UserTransaction — validated, not a live bug, but hardening needed
- **D-10:** User asked whether `UserTransaction` needs its own `GroupId`, since it's linked to `ShopItem` (which has `GroupId` + an automatic EF Core global query filter). **Empirically verified with two throwaway InMemory-provider tests** (not just reasoned about — actually run against this project's real `QuestBoardContext`/EF Core 10.0.9):
  - `ctx.UserTransactions.Include(t => t.ShopItem).Where(t => t.UserId == X)` for a transaction whose `ShopItem` is in a different group than `ActiveGroupId` → **returns 0 rows** (protected).
  - The identical query **without** `.Include(t => t.ShopItem)` → **returns the row** (leaks).
  - Mechanism: `UserTransactionEntity.ShopItemId` is a required (non-nullable) FK, so EF Core translates `.Include(t => t.ShopItem)` into an **inner join**, and `ShopItemEntity`'s own `HasQueryFilter` gets folded into that join condition — which drops the parent `UserTransaction` row entirely, not just the navigation property. All five methods in `UserTransactionRepository` (`GetAllAsync`, `GetTransactionsByUserAsync`, `GetTransactionsByItemAsync`, `GetTransactionsByTypeAsync`, `GetTransactionWithDetailsAsync`) already include `.Include(t => t.ShopItem)`, so **all of them are currently correctly scoped** — no explicit `GroupId` column or `.Where` clause is needed for these five.
  - **Conclusion: no live bug today.** But this protection is incidental (nothing declares or enforces the invariant "every `UserTransaction` query must Include `ShopItem`"), unlike the deliberate, documented pattern for `CharacterEntity`/`PlayerSignupEntity` in `QuestBoardContext.cs:257-259` — and that comment is itself now stale for `CharacterEntity` regardless, since the D-02 revision below gives `CharacterEntity` its own real `HasQueryFilter` (added directly to the same filter block as `QuestEntity`/`ShopItemEntity`), not transitive-navigation protection. `UserTransactionEntity` isn't mentioned in that comment at all, and stays without its own filter — see D-11.

### UserTransaction — hardening scope
- **D-11:** Fix, in this phase:
  1. **Update the comment block in `QuestBoard.Repository/Entities/QuestBoardContext.cs:254-259`** to (a) remove `CharacterEntity` from the "reached only through an already-filtered navigation" claim — it moves up into the explicit `HasQueryFilter` block instead, alongside Quest/ShopItem, per D-02's revision — and (b) accurately document `UserTransactionEntity`'s real mechanism in its place (protected only via the required-FK-`Include`-driven inner join on `ShopItem`, not by any filter of its own — every query touching `UserTransactionEntity` must `Include(t => t.ShopItem)` or it silently leaks).
  2. **Add a regression test** (in `QuestBoard.UnitTests`, alongside the existing `PlayerSignupRepositoryTests.cs` InMemory-provider pattern) asserting a cross-group `UserTransaction` is excluded from `GetTransactionsByUserAsync` — so a future refactor that drops the `Include` or makes `ShopItemId` nullable fails the test loudly instead of silently reopening the leak.
  3. **Fix the one unguarded call site:** `ShopService.ReturnOrSellItemAsync` (`QuestBoard.Domain/Services/ShopService.cs:120-175`) calls the inherited base `transactionRepository.GetByIdAsync(transactionId, token)` (no `Include`, no group scoping at all) to look up the original purchase. It's safe today only because the very next block re-fetches the `ShopItem` by ID — which *is* properly filtered — and throws if it's gone (`"Original item no longer exists."`). Replace this call with the existing `transactionRepository.GetTransactionWithDetailsAsync(transactionId, token)` (already `Include`s `ShopItem`, already transitively protected), so this call site's safety no longer depends on the accident of what happens to run afterward.
- **Claude's Discretion:** Whether the corrected `QuestBoardContext.cs` comment additionally recommends an explicit `.Where` clause as a future-proofing measure, or documents the `Include`-required invariant as sufficient (the remaining transitive-navigation-reliant pattern in this codebase, now that `CharacterEntity` has moved off it per D-02) — lean toward documentation + test over introducing a second, redundant filtering mechanism, consistent with this project's existing style.

### PlayerSignup group scoping — found during research, pulled into scope
- **D-12:** `/gsd-plan-phase 49`'s research step resolved the `<unknowns>` question below empirically (throwaway InMemory-provider test, run via `dotnet test`, then deleted): `PlayerSignupEntity` has the same class of leak `CharacterEntity` had — the code comment at `QuestBoardContext.cs:257-259` claiming "reached only through an already-filtered navigation" is also false for `PlayerSignupEntity`, not just for the already-fixed `CharacterEntity`. Research further found a real, independently-exploitable vulnerability beyond the original question: `QuestController.RemovePlayerSignup` (`[Authorize(Policy = "AdminOnly")]`) checks the caller is an Admin in their own active group (via `AdminHandler.cs`), but never checks that the *target* `PlayerSignupEntity`'s parent Quest belongs to that same group — the identical "check caller's role, not target's group" root cause as D-06's `DungeonMasterController.EditProfile` finding. User confirmed (full fix, not deferred): fix both tiers in this phase:
  1. **Fix `QuestController.RemovePlayerSignup`** — add a group-membership check on the target signup's parent Quest (compare the parent `Quest.GroupId` to `activeGroupContext.ActiveGroupId`, or equivalent) before allowing removal, mirroring D-07's "check the target, not just the caller" pattern.
  2. **Harden the other 3 unfiltered `PlayerSignupRepository` paths** (`GetByIdAsync` via `BaseRepository<PlayerSignup, PlayerSignupEntity>`, `ChangeVoteAsync`, `GetTopWaitlistedCandidateAsync`) — not currently independently exploitable (every existing caller re-derives the target ID from an already-filtered `quest.PlayerSignups` navigation first), but as incidentally-safe as pre-D-11 `UserTransaction` was. Apply D-11's exact treatment: correct the stale `QuestBoardContext.cs:257-259` comment to accurately describe `PlayerSignupEntity`'s real mechanism (safe only when callers pre-validate via the Quest navigation — NOT a repository-level guarantee), and add a regression test proving the current callers' pre-validation pattern holds, so a future refactor that skips it fails loudly. Document + test, don't add a second redundant filtering mechanism — same preference as D-11.
- **D-13:** `RemovePlayerSignup`'s cross-group-target response is **404 Not Found**, not 403 — same convention as D-04/D-09, hiding cross-tenant existence rather than confirming it.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### The bug (controller with zero group scoping)
- `QuestBoard.Service/Controllers/Characters/GuildMembersController.cs` — `Index` (lines 17-45) calls `characterService.GetAllCharactersWithDetailsAsync()` with no group filter; `Details` (lines 47-61) and `GetProfilePicture` (lines 292-302) have no ownership/group check at all (unlike `Edit`/`Delete`, which check `OwnerId`).

### A related but NOT-reused pattern (Users are join-based; Character is now schema-based instead)
- `QuestBoard.Service/Controllers/QuestBoard/PlayersController.cs` / `QuestBoard.Repository/UserRepository.cs` (`GetAllDungeonMasters` lines 20-33, `GetAllPlayers` lines 36-48, `GetAllGroupMembers` lines 51-59) — this is the manual `DbContext.UserGroups.Any(...)` join pattern the *original* D-02 plan proposed reusing for Characters. **Superseded** — Character now gets its own `GroupId` column + `HasQueryFilter` instead (see D-02's revision), because `User` can't carry a direct `GroupId` (a user belongs to *many* groups via the `UserGroups` join table), whereas a `Character`, like a `Quest`, conceptually belongs to exactly one. Kept here only as context for why this alternative was considered and not chosen.
- `QuestBoard.Domain/Extensions/ActiveGroupContextExtensions.cs` — `RequireActiveGroupId()` fail-fast helper; used for the creation-time stamp per D-02 (mirrors `QuestController.cs:124`), NOT for the query filter itself (D-03's filter shape checks `ActiveGroupId != null` inline rather than throwing).

### The migration precedent to mirror exactly
- `QuestBoard.Repository/Migrations/20260630055221_AddGroupSchema.cs` — the original migration that added `GroupId` to `Quests`/`ShopItems`. Follow its exact structure for adding `GroupId` to `Characters`: add column with temporary default → `migrationBuilder.Sql("UPDATE Characters SET GroupId = 1")` (per the user, all existing rows belong to group 1) → add FK constraint to `Groups(Id)` *after* backfill → add index. This migration's own comments explain why the ordering matters (FK violation risk on unbackfilled rows).
- `QuestBoard.Repository/Entities/QuestBoardContext.cs:237-259` — where `CharacterEntity`'s new `HasQueryFilter` gets added (alongside `QuestEntity`/`ShopItemEntity`'s), and where the stale `CharacterEntity` mention in the "reached only through navigation" comment needs removing (also touched by D-11.1 for `UserTransactionEntity`).

### Code to modify (smaller than originally planned — filter does most of the work)
- `QuestBoard.Repository/Entities/CharacterEntity.cs` — add `public int GroupId { get; set; }` + FK navigation to `GroupEntity`, mirroring `QuestEntity`'s/`ShopItemEntity`'s existing `GroupId`/`Group` shape.
- `QuestBoard.Repository/Entities/QuestBoardContext.cs` — new `HasQueryFilter` for `CharacterEntity` (D-03's shape) + FK/relationship config (mirroring `QuestEntity`'s `HasOne(q => q.Group).WithMany().HasForeignKey(q => q.GroupId).OnDelete(DeleteBehavior.NoAction)` at `QuestBoardContext.cs:210-214`).
- One new migration (see precedent above).
- `QuestBoard.Repository/CharacterRepository.cs:62-68` — rewrite `GetCharacterProfilePictureAsync`'s query body only (signature unchanged) to route through `DbContext.Characters` instead of `DbContext.CharacterImages` directly, per D-04.
- `QuestBoard.Service/Controllers/Characters/GuildMembersController.cs` — `Create` POST action: add `character.GroupId = activeGroupContext.RequireActiveGroupId();` before `characterService.AddAsync(...)`. Requires injecting `IActiveGroupContext` into this controller (not currently a constructor dependency).
- **No changes needed** to `ICharacterRepository`/`ICharacterService`/`CharacterService.cs` signatures — `GetAllCharactersWithDetailsAsync()` and `GetCharacterWithDetailsAsync(id)` are already plain LINQ queries against `DbContext.Characters`, so they become transparently scoped once the model-level filter exists. This is the direct payoff of the schema-based approach over the originally-planned manual join.

### Middleware context (why this matters differently for SuperAdmin vs. everyone else)
- `QuestBoard.Service/Middleware/GroupSessionMiddleware.cs` — confirms SuperAdmin bypasses the "must have an active group" gate (lines 65-69, checked before the group-required redirect), so SuperAdmin is the only role that can reach `GuildMembersController`/`DungeonMasterController` with `ActiveGroupId == null`. Every other authenticated user always has a non-null `ActiveGroupId` here.

### The second bug (DungeonMasterController — read leak + write-path leak)
- `QuestBoard.Service/Controllers/DungeonMaster/DungeonMasterController.cs` — `Profile` (lines 19-46), `EditProfile` GET (lines 48-76) and POST (lines 78-119), `GetDMProfilePicture` (lines 121-134): none check that the target `id` belongs to a member of `activeGroupContext.ActiveGroupId`. `GetEffectiveRoleAsync` (lines 139-142, private helper) already shows the SuperAdmin-short-circuit pattern to mirror for D-08 (`User.IsInRole("SuperAdmin") ? GroupRole.Admin : ...` — but per D-08, DM-profile actions should 404 for SuperAdmin-with-no-group instead, not short-circuit to Admin).
- `QuestBoard.Domain/Interfaces/IUserService.cs:79` — `GetGroupRoleByIdAsync(int userId, int groupId)`: the existing "is this user a member of this group" primitive to reuse per D-07 (already implemented in `UserService.cs:84-87` → `repository.GetGroupRoleAsync(userId, groupId)`).
- `QuestBoard.Domain/Interfaces/IDungeonMasterProfileService.cs` / `QuestBoard.Domain/Services/DungeonMasterProfileService.cs` — `GetProfileByUserIdAsync`, `GetProfilePictureAsync`, `UpsertProfileAsync` — these operate purely on `userId`; the group check belongs in the controller (or a thin wrapper), not in these methods. `DungeonMasterProfileEntity` deliberately does **not** get a `GroupId` column (D-09a) — unlike `CharacterEntity`, which now does (D-02's revision) — so this stays access-control-only, not schema-level.

### The third area (UserTransaction — hardening, not a live bug)
- `QuestBoard.Repository/Entities/UserTransactionEntity.cs` — `ShopItemId`/`ShopItem` (required FK, no `GroupId` of its own).
- `QuestBoard.Repository/UserTransactionRepository.cs` — all 5 methods already `.Include(t => t.ShopItem)` (lines 14-16, 25-27, 37-39, 49-51, 61-64) — the mechanism D-10 validated as currently sufficient.
- `QuestBoard.Repository/Entities/QuestBoardContext.cs:254-259` — the comment to correct per D-11.1.
- `QuestBoard.Domain/Services/ShopService.cs:120-175` — `ReturnOrSellItemAsync`, the one call site to fix per D-11.3 (`transactionRepository.GetByIdAsync(transactionId, token)` → `GetTransactionWithDetailsAsync`).
- `QuestBoard.UnitTests/Repository/PlayerSignupRepositoryTests.cs` — existing InMemory-provider + `IActiveGroupContext` test pattern to mirror for D-11.2's new regression test (uses `UseInMemoryDatabase`, a test `IActiveGroupContext` implementation, and seeds `Groups`/`UserEntities`/`Quests` directly against `QuestBoardContext`).

### The fourth area (PlayerSignup — found during research, D-12/D-13)
- `QuestBoard.Service/Controllers/QuestBoard/QuestController.cs` — `RemovePlayerSignup` action, the confirmed new vulnerability: `[Authorize(Policy = "AdminOnly")]` checks caller's role only, never the target signup's parent Quest's group.
- `QuestBoard.Domain/.../AdminHandler.cs` (the `AdminOnly` policy handler) — confirms it validates the caller's own active-group Admin role, not any target resource's group.
- `QuestBoard.Repository/PlayerSignupRepository.cs` / `BaseRepository<PlayerSignup, PlayerSignupEntity>.GetByIdAsync` — the unfiltered repository paths (`GetByIdAsync` via base class, `ChangeVoteAsync`, `GetTopWaitlistedCandidateAsync`) that never `.Include(ps => ps.Quest)`, so `QuestEntity`'s filter never applies to them.
- `QuestBoard.Repository/Entities/QuestBoardContext.cs:257-259` — same stale comment block as D-11, also needs correcting for `PlayerSignupEntity` (not just `UserTransactionEntity`).
- `QuestBoard.UnitTests/Repository/PlayerSignupRepositoryTests.cs` — existing InMemory-provider test file to extend with the new regression coverage (D-12.2) and the `RemovePlayerSignup` fix's test (D-12.1).
- Full analysis and empirical evidence: `49-RESEARCH.md` (Summary, Pattern 4, Pitfall 3/4, Open Question 1).

No external ADRs/specs beyond the codebase map above — requirements are fully captured in the decisions section.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `IActiveGroupContext.ActiveGroupId` — already DI-registered; injected into `GuildMembersController` (new dependency) for the D-02 creation-time stamp. `CharacterRepository`/`CharacterService` do **not** need it injected — the model-level filter in `QuestBoardContext.cs` handles scoping automatically, the same way `QuestRepository`/`ShopRepository` never inject it either.
- `QuestEntity`'s `Group` navigation config (`QuestBoardContext.cs:210-214`, `HasOne(q => q.Group).WithMany().HasForeignKey(q => q.GroupId).OnDelete(DeleteBehavior.NoAction)`) — the exact relationship shape to replicate for `CharacterEntity`.

### Established Patterns
- SuperAdmin-empty-when-unscoped is now enforced by omitting the `ActiveGroupId == null ||` clause from Character's filter (D-03), a one-line divergence from Quest/ShopItem's filter shape — not a manual `if (groupId == null) return [];` guard in a repository method. DungeonMasterController's single-record equivalent is `NotFound()` (D-08), which *is* a manual guard, since `DungeonMasterProfileEntity` deliberately doesn't get a model-level filter (D-09a).
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

User then asked whether `UserTransaction` needs its own `GroupId`, reasoning that its link to the already-filtered `ShopItem` might make it automatically safe. Rather than answer from reasoning alone, this was verified empirically with throwaway EF Core InMemory-provider tests against the real `QuestBoardContext` — confirming the reasoning was directionally right (it is currently protected) but for a more specific and fragile reason than "automatic": a required-FK `Include`-driven inner join, not a query filter of its own (D-10). User chose to hardening this rather than leave it as an undocumented accident (D-11).

User then requested this migration/backfill note directly: **"the default groupId to use is 1, all items in the database currently are for that group"** — this became the backfill value for D-02's `CharacterEntity` migration (`migrationBuilder.Sql("UPDATE Characters SET GroupId = 1")`), and is confirmed consistent with the original `20260630055221_AddGroupSchema.cs` migration, whose own comment names GroupId=1 as "EuphoriaInn" — the same original/only group.

Discussing that migration also surfaced that D-02's original "manual join, no schema change" plan should instead mirror Quest/ShopItem's direct-`GroupId`-column + automatic-filter pattern — see D-02's revision above. This in turn required resolving two follow-on questions: whether `DungeonMasterProfileEntity` should get the same treatment (no — D-09a, it's a shared one-row-per-user table, not one-row-per-character) and how Character's filter should handle a null `ActiveGroupId` (empty, not see-all — D-03's revision, confirmed after tracing `ActiveGroupContextService.cs` to establish exactly when that null case is reachable).

</specifics>

<unknowns>
## Unknowns — Flag for Research

**RESOLVED by research (2026-07-05) — see D-12/D-13.** `PlayerSignupEntity`'s group-filtering claim in `QuestBoardContext.cs:257-259` was checked the same way D-10 checked UserTransaction (empirical InMemory-provider test, not reasoning) and found **false**, same as `CharacterEntity`'s identical claim. Research additionally found a real, independently-exploitable vulnerability (`QuestController.RemovePlayerSignup`) beyond the original question's scope. User confirmed a full fix (not deferral) — see D-12/D-13 in `<decisions>` and `49-RESEARCH.md` for full analysis and evidence. This is no longer an open unknown.

</unknowns>

<deferred>
## Deferred Ideas

None — all three confirmed leaks (Characters, DungeonMasterController, PlayerSignup/`RemovePlayerSignup`) are in scope for this phase. Quest History was investigated and ruled out as already correct (D-05). The PlayerSignupEntity question raised in `<unknowns>` was resolved by research and pulled into scope, not deferred — see D-12/D-13.

</deferred>

---

*Phase: 49-fix-guild-members-page-missing-group-tenant-filtering*
*Context gathered: 2026-07-05*
