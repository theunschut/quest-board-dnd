# Phase 49: Fix Guild Members page missing group/tenant filtering - Research

**Researched:** 2026-07-05
**Domain:** ASP.NET Core MVC + EF Core 10 multi-tenant query-filter hardening (C#/.NET, SQL Server)
**Confidence:** HIGH

## Summary

This phase's design is already fully locked in `49-CONTEXT.md` (D-01 through D-11). Every file/line reference cited there was cross-checked against the current working tree during this research pass and found accurate with **zero drift** — the migration precedent (`20260630055221_AddGroupSchema.cs`), `QuestBoardContext.cs`'s filter block, `GuildMembersController.cs`, `DungeonMasterController.cs`, `CharacterRepository.cs`, `UserTransactionRepository.cs`, and `ShopService.ReturnOrSellItemAsync` all match CONTEXT.md's description line-for-line (modulo cosmetic line-number shifts of a few lines in a couple of files, not semantic differences). No section of the locked plan needs revision.

This research's primary job was resolving the one explicitly flagged unknown: whether `PlayerSignupEntity` is safe (as its stale code comment claims) or leaky (as `CharacterEntity`'s identical claim turned out to be). **It is leaky** — confirmed empirically with a throwaway InMemory-provider test (written, run, and deleted per the recommended protocol) against two call paths, both of which returned cross-group rows with zero group filtering. Unlike `UserTransactionEntity` (D-10, confirmed safe via the required-FK-`Include`-driven inner join), `PlayerSignupEntity`'s equivalent required FK (`QuestId`) does **not** protect it, because the two vulnerable call paths never `.Include(ps => ps.Quest)` — they query `PlayerSignups` directly via `FindAsync`/`.Where(...)` with no join back to the group-scoped parent.

**Primary recommendation:** Execute the locked D-01–D-11 plan as written (no redesign needed), AND expand this phase's scope by one item: fix the confirmed `PlayerSignupEntity` leak on its one truly unguarded, independently-exploitable path — `QuestController.RemovePlayerSignup` (Admin-only route, takes a raw signup ID with no group check on the *target* signup, mirroring the exact "check caller's role but not target's group" gap D-06 found in `DungeonMasterController`). The other three `PlayerSignupRepository` methods proven unfiltered in isolation (`ChangeVoteAsync`, `GetByIdWithDateVotesAsync`, `GetTopWaitlistedCandidateAsync` on the repository) are **not independently exploitable in their current call sites** because every controller path feeding them first re-derives the `playerSignupId`/`questId` from an already-filtered `quest.PlayerSignups` navigation before calling them — but this protection is as incidental/undocumented as `UserTransaction`'s was pre-D-11, and should get the same "harden, don't just leave as reasoning" treatment.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Character list/detail group scoping | API/Backend (EF Core global query filter) | Database (schema: `GroupId` column + FK + index) | Mirrors `QuestEntity`/`ShopItemEntity` — tenant isolation belongs at the data-access layer so every current and future query is automatically safe, not just the ones a developer remembers to filter |
| Character profile-picture group scoping | API/Backend (repository query rewrite) | — | `CharacterImageEntity` has no filter of its own; must be reached through the filtered `CharacterEntity` root |
| DM profile view/edit access control | API/Backend (controller-level membership check) | — | `DungeonMasterProfileEntity` deliberately has no `GroupId` (D-09a) — access control, not row-level filtering, is the correct tier here since the underlying data is intentionally shared across a DM's groups |
| UserTransaction group scoping | API/Backend (repository `Include`-driven join) | — | No schema change; hardening is documentation + regression test + closing the one unguarded call site, consistent with the existing incidental-but-correct mechanism |
| PlayerSignup group scoping (this research's finding) | API/Backend (controller-level re-derivation from filtered Quest, OR add repository-level check) | Database (no schema change — mirrors UserTransaction's non-schema approach) | Same tier as UserTransaction: `PlayerSignupEntity` already has a required FK (`QuestId`) to an already-filtered parent (`QuestEntity`); the fix is ensuring every access path either joins through that FK or independently validates group membership, not adding a new column |

## Standard Stack

No new packages required — this phase is a schema + query-filter + access-control hardening within the existing stack.

### Core (existing, no version changes)
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Microsoft.EntityFrameworkCore | 10.0.9 [VERIFIED: project files] | ORM, global query filters, migrations | Already in use for `QuestEntity`/`ShopItemEntity` filters — this phase extends the identical mechanism to `CharacterEntity` |
| Microsoft.EntityFrameworkCore.InMemory | 10.0.9 [VERIFIED: QuestBoard.UnitTests.csproj] | Test-double provider for repository unit tests | Already the established pattern in `PlayerSignupRepositoryTests.cs`; reuse for D-11.2's new regression test and any new PlayerSignup hardening test |
| xunit.v3 | 3.2.2 [VERIFIED: QuestBoard.UnitTests.csproj] | Test framework | Existing project standard |
| FluentAssertions | 8.10.0 [VERIFIED: QuestBoard.UnitTests.csproj] | Test assertions (`.Should()`) | Existing project standard |

**Installation:** None — no new packages.

## Package Legitimacy Audit

Not applicable — this phase installs no new external packages. All work uses libraries already present in the project (`Microsoft.EntityFrameworkCore`, `xunit.v3`, `FluentAssertions`, `NSubstitute`).

## Architecture Patterns

### System Architecture Diagram

```
                     ┌─────────────────────────────────────────────┐
                     │   HTTP Request (authenticated user session) │
                     └───────────────────┬───────────────────────┘
                                         │
                          ActiveGroupId read from HttpContext.Session
                       (set once per session by GroupPicker / GroupSessionMiddleware)
                                         │
                 ┌───────────────────────┼────────────────────────────┐
                 │                       │                            │
     GuildMembersController   DungeonMasterController      QuestController
     (Index/Details/          (Profile/EditProfile/        (RemovePlayerSignup —
      GetProfilePicture)       GetDMProfilePicture)          Admin-only, ID-only route)
                 │                       │                            │
                 │                       │  IUserService               │
                 │                       │  .GetGroupRoleByIdAsync(    │
                 │                       │    targetUserId, groupId)   │
                 │                       │  -- NEW membership check    │
                 │                       │  gating target, not caller │
                 │                       ▼                            │
                 │              404 if target not in                  │
                 │              viewer's active group                 │
                 │                                                    │
                 ▼                                                    ▼
     ICharacterService / ICharacterRepository          IPlayerSignupService /
                 │                                     IPlayerSignupRepository
                 │  DbContext.Characters                        │
                 │  (plain LINQ — no manual filter code)          │  DbContext.PlayerSignups
                 ▼                                                (direct root — UNFILTERED
     ┌─────────────────────────────┐                              today; QuestId FK exists
     │  EF Core OnModelCreating    │                              but is never joined for
     │  HasQueryFilter<Character>  │                              these specific methods)
     │  ActiveGroupId != null &&  │                                       │
     │  e.GroupId == ActiveGroupId│                                       ▼
     │  (NO null-escape-hatch —   │                          Quest-rooted callers (Change
     │   differs from Quest/      │                          Vote/RevokeSignup/UpdateSignup*)
     │   ShopItem's filter shape) │                          already re-derive the target
     └─────────────────────────────┘                          PlayerSignup.Id from an
                 │                                             already-filtered quest.PlayerSignups
                 ▼                                             navigation BEFORE calling into
        SQL Server (Characters                                 the unfiltered repository method
        table, GroupId column,                                        │
        FK → Groups, index)                                           ▼
                                                        RemovePlayerSignup is the ONE path
                                                        that calls GetByIdAsync(id) directly,
                                                        with no quest/group re-derivation —
                                                        the actual exploitable gap found here
```

### Recommended Project Structure

No new folders — all changes are in existing files:

```
QuestBoard.Repository/
├── Entities/
│   ├── CharacterEntity.cs         # add GroupId + Group navigation
│   └── QuestBoardContext.cs       # add CharacterEntity HasQueryFilter + FK config; correct stale comment
├── Migrations/
│   └── <new>_AddGroupIdToCharacters.cs   # mirror 20260630055221_AddGroupSchema.cs structure
├── CharacterRepository.cs         # rewrite GetCharacterProfilePictureAsync only
└── PlayerSignupRepository.cs      # candidate for D-06-style hardening (see Don't Hand-Roll / Pitfalls)

QuestBoard.Service/
├── Controllers/Characters/GuildMembersController.cs   # inject IActiveGroupContext; stamp GroupId on Create
├── Controllers/DungeonMaster/DungeonMasterController.cs  # add GetGroupRoleByIdAsync membership checks
└── Controllers/QuestBoard/QuestController.cs          # RemovePlayerSignup needs a group-membership check on the target signup

QuestBoard.UnitTests/
└── Repository/
    ├── PlayerSignupRepositoryTests.cs   # existing — add regression coverage here (see below)
    └── UserTransactionRepositoryTests.cs # NEW per D-11.2 (file doesn't exist yet — confirmed via search)
```

### Pattern 1: Schema-based tenant scoping via EF Core global query filter (Character — D-02/D-03)
**What:** Add a real `GroupId` int column + FK to `Groups(Id)` on `CharacterEntity`, then register a `HasQueryFilter` in `OnModelCreating` that automatically restricts every LINQ query against `DbContext.Characters` to the caller's active group.
**When to use:** Any entity that conceptually belongs to exactly one group (Character, like Quest/ShopItem) — not entities with a many-to-many group relationship (User, via UserGroups).
**Example (mirrors existing QuestEntity/ShopItemEntity pattern exactly):**
```csharp
// Source: QuestBoard.Repository/Entities/QuestBoardContext.cs:244-252 (existing Quest/ShopItem pattern)
modelBuilder.Entity<QuestEntity>()
    .HasQueryFilter(e =>
        activeGroupContext.ActiveGroupId == null ||
        e.GroupId == activeGroupContext.ActiveGroupId);

// NEW for CharacterEntity — deliberately NO "ActiveGroupId == null ||" escape hatch (D-03):
modelBuilder.Entity<CharacterEntity>()
    .HasQueryFilter(e =>
        activeGroupContext.ActiveGroupId != null &&
        e.GroupId == activeGroupContext.ActiveGroupId);
```
**Critical EF Core gotcha (verified against this project's own code comment):** the lambda must close over the `activeGroupContext` *instance*, never a captured local of `.ActiveGroupId`'s value — `QuestBoardContext.cs:242-243`'s existing comment already documents this ("Do NOT capture activeGroupContext.ActiveGroupId into a local var here... Always reference the service"). This is because `OnModelCreating` runs once at model-build time; capturing the value would freeze it at `null` forever.

### Pattern 2: Reaching a sibling entity through its filtered parent (CharacterImage — D-04)
**What:** When entity B (no filter of its own) has a required 1:1 shared-PK relationship to entity A (has a filter), query MUST be rooted at A, never at B directly — otherwise B's filter-less table bypasses A's protection entirely.
**When to use:** Any `[Key][ForeignKey]`-shared-primary-key sibling table (this project's convention for 1:1 image/detail tables — also applies to `DungeonMasterProfileImageEntity` → `DungeonMasterProfileEntity`, though that pair is out of scope per D-09a since the parent itself has no filter).
**Example:**
```csharp
// Source: pattern confirmed via QuestBoard.Repository/CharacterRepository.cs:62-68 (before/after)
// BEFORE (bypasses CharacterEntity's filter entirely):
return await DbContext.CharacterImages
    .Where(c => c.Id == id)
    .Select(c => c.ImageData)
    .FirstOrDefaultAsync(token);

// AFTER (rooted at the filtered parent — D-04's fix):
return await DbContext.Characters
    .Where(c => c.Id == id)
    .Select(c => c.ProfileImage != null ? c.ProfileImage.ImageData : null)
    .FirstOrDefaultAsync(token);
```

### Pattern 3: Access-control check for a shared (non-schema-scoped) entity (DungeonMasterProfile — D-07/D-08)
**What:** When the underlying data is intentionally NOT partitioned by group (a DM's bio/photo is shared across every group they belong to), tenant isolation must be enforced at the controller/access-control layer, not via a query filter.
**When to use:** Any entity where adding `GroupId` would change data-sharing semantics, not just add security (this is the exact reasoning D-09a used to reject giving `DungeonMasterProfileEntity` a `GroupId`).
**Example:**
```csharp
// Source: existing primitive at QuestBoard.Domain/Services/UserService.cs:84-87, confirmed unchanged
public async Task<GroupRole?> GetGroupRoleByIdAsync(int userId, int groupId)
{
    return await repository.GetGroupRoleAsync(userId, groupId);
}
// Returns null if userId is not a member of groupId — exactly the primitive D-07 calls for.
// Usage pattern to add in DungeonMasterController, before touching group-scoped data:
if (activeGroupContext.ActiveGroupId is not { } groupId) return NotFound(); // D-08
var targetRole = await userService.GetGroupRoleByIdAsync(targetUser.Id, groupId);
if (targetRole == null) return NotFound(); // D-09 — 404, not 403, for cross-group
```

### Pattern 4: Required-FK-Include-driven transitive filtering (UserTransaction — D-10/D-11, and the PlayerSignup finding below)
**What:** EF Core translates `.Include(child => child.RequiredParentNav)` into an INNER JOIN when the FK is non-nullable. If the parent has a `HasQueryFilter`, that filter gets folded into the join condition, silently dropping child rows whose parent is out-of-scope — even though the child itself has no filter.
**When to use:** This IS a valid protection mechanism, but only for the specific query that includes the navigation. It is NOT durable — any query against the same DbSet that omits the `.Include(...)` bypasses it completely, as this research empirically demonstrated for `PlayerSignupEntity`.
**Example — the exact mechanism, and its failure mode:**
```csharp
// SAFE — Include folds ShopItem's filter into an inner join, so a cross-group transaction
// vanishes from the result set entirely (verified in D-10's throwaway test):
DbContext.UserTransactions.Include(t => t.ShopItem).Where(t => t.UserId == userId)

// UNSAFE — identical entity, same required FK, but no Include means no join,
// means no filter is ever evaluated:
DbContext.UserTransactions.Where(t => t.UserId == userId)   // LEAKS

// This research confirmed PlayerSignupEntity's QuestId FK has the exact same shape as
// UserTransactionEntity's ShopItemId FK (both [Required], both non-nullable int) — but
// unlike UserTransactionRepository's 5 methods (all of which DO Include(t => t.ShopItem)),
// PlayerSignupRepository's methods that query DbSet directly NEVER Include(ps => ps.Quest):
//   BaseRepository<PlayerSignup, PlayerSignupEntity>.GetByIdAsync  -> DbSet.FindAsync([id])
//   PlayerSignupRepository.ChangeVoteAsync                        -> DbSet.Include(ps => ps.DateVotes)...
//   PlayerSignupRepository.GetTopWaitlistedCandidateAsync         -> DbSet.Include(ps => ps.DateVotes)...
// None of these three ever touch the Quest navigation, so QuestEntity's filter never applies.
```

### Anti-Patterns to Avoid
- **Manual join-based filtering for single-group-owned entities:** The original D-02 plan (before revision) proposed a manual `DbContext.UserGroups.Any(...)` join for Character, mirroring `UserRepository.GetAllDungeonMasters`. That pattern is correct for `User` (many-to-many via UserGroups) but wrong for `Character` (belongs to exactly one group) — the schema-based `HasQueryFilter` approach is strictly better here because it protects every current and future query automatically, not just the ones a developer remembers to join correctly.
- **Trusting a "reached only through an already-filtered navigation" code comment without verification:** This exact comment existed for both `CharacterEntity` (false — this whole phase exists because of it) and `PlayerSignupEntity` (also false, per this research's empirical test). Any future entity carrying this same comment pattern should be independently verified with a throwaway InMemory test before being trusted, not reasoned about statically.
- **Checking only the caller's group role, never the target resource's group:** This is the shared root cause across THREE of this phase's findings — `DungeonMasterController.EditProfile` (D-06, caller's Admin-in-own-group role checked, target DM's group never checked), and now also `QuestController.RemovePlayerSignup` (`AdminOnly` policy checks the caller is Admin in their own active group via `AdminHandler.cs`, but the target `PlayerSignupEntity`'s group is never checked). Any authorization check gating a mutation on someone else's resource must validate the TARGET's group membership, not just the caller's.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| "Is this user a member of this specific group?" check | A fresh `DbContext.UserGroups.Any(...)` inline query in a new controller | `IUserService.GetGroupRoleByIdAsync(userId, groupId)` (already exists, `UserService.cs:84-87`) | Already implemented, already tested via the existing `UserServiceTests.cs` patterns, returns null for non-members exactly as needed |
| Tenant-scoped querying for a single-group-owned entity | Bespoke `.Where(x => x.GroupId == activeGroupId)` clauses sprinkled through repository methods | EF Core `HasQueryFilter` in `OnModelCreating` | Applies automatically and transparently to every LINQ query against the DbSet — durable protection against future developers forgetting to filter, exactly the property this phase is trying to add for Character |
| Determining if a required-FK sibling is safe to query directly | Reasoning/inspection alone ("it's probably fine because...") | A throwaway InMemory-provider xUnit test seeding a real cross-group scenario | This exact approach caught two real, previously-undetected classes of bug in this same phase (Character's false "safe" comment, and now PlayerSignup's) — static reasoning about EF Core's query-translation behavior is unreliable enough that this project has now twice been wrong about it without empirical testing |

**Key insight:** In this codebase, every "is this safe?" claim about implicit/transitive tenant filtering that was NOT empirically tested has turned out to be wrong at least partially (Character: fully wrong; PlayerSignup: wrong for 4 of 5 relevant methods, though 3 are currently non-exploitable due to incidental caller-side re-derivation). The pattern that IS safe (UserTransaction) was only confirmed safe by writing and running a test, not by reading the code. Treat every remaining "reached only through navigation" comment in this codebase as an open question until independently verified the same way.

## Runtime State Inventory

> This phase adds a schema column (`CharacterEntity.GroupId`) via migration with a backfill — the rename/refactor inventory categories are relevant here even though this isn't a rename phase.

| Category | Items Found | Action Required |
|----------|-------------|------------------|
| Stored data | `Characters` table has zero rows with any group association today (no `GroupId` column exists yet) — migration backfills all existing rows to `GroupId = 1` per explicit user instruction ("all existing rows belong to group 1") | Migration `UPDATE Characters SET GroupId = 1` (data migration, not just a code edit) — confirmed this is the only group that existed pre-multi-tenancy, consistent with `20260630055221_AddGroupSchema.cs`'s identical backfill for Quests/ShopItems |
| Live service config | None — no external service config (n8n/Datadog-equivalent) references Character rows by group | None |
| OS-registered state | None — no OS-level task/service registration references Characters | None |
| Secrets/env vars | None — no secret or env var references this column | None |
| Build artifacts | None — this is a net-new column addition, not a rename; no stale build artifact risk | None |

**Nothing else found in any category** — verified by grepping for `Characters` table references across migrations, Docker/compose config, and `.planning/codebase/STACK.md`; no non-EF-managed system stores Character data.

## Common Pitfalls

### Pitfall 1: Adding the FK constraint before backfilling existing rows
**What goes wrong:** SQL Server rejects the `AddForeignKey` migration step if any existing row has a `GroupId` value (including the temporary default) that doesn't exist in `Groups(Id)`.
**Why it happens:** EF Core migrations execute in the order written; if `AddForeignKey` runs before the `UPDATE ... SET GroupId = 1` backfill Sql step, rows still holding the temporary `defaultValue` (commonly `0`, which has no matching `Groups.Id`) fail the constraint.
**How to avoid:** Follow `20260630055221_AddGroupSchema.cs`'s exact step order: (1) `AddColumn` with temporary `defaultValue`, (2) `UPDATE ... SET GroupId = 1`, (3) `AddForeignKey`, (4) `CreateIndex`. This project's own migration already documents this ordering rationale in its comments.
**Warning signs:** Migration fails at deploy time with an FK violation error naming the new constraint; if this happens the fix is to reorder migration steps, not to relax the constraint.

### Pitfall 2: Capturing `activeGroupContext.ActiveGroupId` into a local variable inside `OnModelCreating`
**What goes wrong:** The filter permanently behaves as if `ActiveGroupId` is always `null` (or whatever it was at app startup), for every request, for the lifetime of the process.
**Why it happens:** `OnModelCreating` runs once when the EF Core model is built (effectively at startup, cached thereafter). A lambda that captures `activeGroupContext.ActiveGroupId` (the *value*) rather than `activeGroupContext` (the *service instance*) bakes in that one-time value into the compiled query filter expression tree.
**How to avoid:** Reference the injected `activeGroupContext` service instance directly inside the lambda (as the existing Quest/ShopItem filters already correctly do) — never assign `.ActiveGroupId` to a local `var` first. `QuestBoardContext.cs:242-243` already has an explicit CRITICAL comment warning about this; follow the same pattern for the new Character filter.
**Warning signs:** Every character list appears empty (or, if the escape hatch were mistakenly copied from Quest's shape, shows all characters across all groups) regardless of which group the session has active — and behaves identically across every request in a running process, which is the tell that the value was frozen at build time rather than re-evaluated per query.

### Pitfall 3: Checking the caller's group role but not the target resource's group (the recurring root cause in this phase)
**What goes wrong:** An authorization check like `[Authorize(Policy = "AdminOnly")]` (or an equivalent inline `role == GroupRole.Admin` check) correctly verifies the CALLER is an Admin in their OWN active group, but the action then operates on a resource identified by a raw client-supplied ID with no check that the resource belongs to that same group.
**Why it happens:** Role-based policies and group-membership checks look similar and are easy to conflate; a developer adding `[Authorize(Policy = "AdminOnly")]` reasonably assumes this covers "may this Admin touch this specific record," when it only covers "is this user an Admin somewhere."
**How to avoid:** For any action that accepts a target-resource ID and mutates/reveals cross-group-sensitive data, add an explicit target-group-membership check (`GetGroupRoleByIdAsync` for user-owned resources; the target entity's own `GroupId` compared to `activeGroupContext.ActiveGroupId` for schema-scoped entities) in addition to the caller's role check.
**Warning signs:** grep for `[Authorize(Policy = "AdminOnly")]` or `[Authorize(Policy = "DungeonMasterOnly")]` combined with a controller action taking an `int id` route parameter that's used directly in a repository lookup with no subsequent group/ownership comparison — this exact shape was found in `DungeonMasterController.EditProfile` (D-06) and, per this research, also in `QuestController.RemovePlayerSignup`.

### Pitfall 4: Trusting an entity's required FK to imply it's always safely joined
**What goes wrong:** Seeing that `PlayerSignupEntity.QuestId` is `[Required]` (same shape as `UserTransactionEntity.ShopItemId`, which IS safely protected) leads to assuming PlayerSignup gets the same protection — but the FK's mere existence guarantees nothing; only an actual `.Include()` of the required navigation in that specific query path creates the protective inner join.
**Why it happens:** The protective mechanism (D-10) depends entirely on whether a given LINQ query happens to `.Include()` the filtered parent — this is a per-query property, not a per-entity property, so a required FK on the entity is necessary but not sufficient.
**How to avoid:** Audit every method that queries the DbSet directly (`FindAsync`, `.Where(...)` without `.Include(parentNav)`) individually. Do not generalize "this FK is required, so it's fine" from one entity to another without checking each call site.
**Warning signs:** A code comment describing a class of entities as "safe via transitive navigation" without per-method verification (exactly what `QuestBoardContext.cs:257-259` claimed about both `CharacterEntity` and `PlayerSignupEntity` — one was checked and found false, the other now also found false).

## Code Examples

### Empirical leak-verification test pattern (used in this research; recommended for D-11.2-style regression coverage)
```csharp
// Source: pattern derived from QuestBoard.UnitTests/Repository/PlayerSignupRepositoryTests.cs's
// existing InMemory-provider harness (TestActiveGroupContext, CreateContext, CreateMapper).
// This exact shape (seed two groups, seed a signup whose parent Quest is in the OTHER group,
// then assert whether an unfiltered lookup returns it) is what confirmed both the UserTransaction
// D-10 finding (no leak, when Include is present) and this research's PlayerSignup finding (leak,
// when Include is absent).
[Fact]
public async Task GetByIdAsync_ForSignupInDifferentGroupsQuest_ReturnsRowRegardlessOfActiveGroup()
{
    // Seed a Quest in Group 2 with a PlayerSignup attached.
    // Then query as a viewer whose ActiveGroupId is 1.
    var result = await repository.GetByIdAsync(1, token);
    // If result is non-null, the entity leaked across the group boundary.
    result.Should().NotBeNull();
}
```

### CharacterEntity migration shape to mirror exactly
```csharp
// Source: QuestBoard.Repository/Migrations/20260630055221_AddGroupSchema.cs (verified current, no drift)
migrationBuilder.AddColumn<int>(
    name: "GroupId",
    table: "Characters",
    type: "int",
    nullable: false,
    defaultValue: 0);

migrationBuilder.Sql("UPDATE Characters SET GroupId = 1");

migrationBuilder.AddForeignKey(
    name: "FK_Characters_Groups_GroupId",
    table: "Characters",
    column: "GroupId",
    principalTable: "Groups",
    principalColumn: "Id",
    onDelete: ReferentialAction.NoAction);

migrationBuilder.CreateIndex(
    name: "IX_Characters_GroupId",
    table: "Characters",
    column: "GroupId");
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|---------------|--------|
| Manual `DbContext.UserGroups.Any(...)` join per-query for tenant scoping (original D-02 plan) | Schema `GroupId` column + EF Core `HasQueryFilter` (D-02 revision, this phase) | Mid-discussion during `/gsd-discuss-phase 49` | Character queries become durably, automatically safe instead of depending on every future developer remembering to add the join |
| "Reached only through an already-filtered navigation" as an unverified code comment | Empirically-verified-or-explicit-filter as the only trusted claim | This phase (Character) + this research (PlayerSignup) | Two previously-trusted "safe" claims in this exact codebase turned out to be false or partially false; the project's own comment style is no longer sufficient evidence on its own |

**Deprecated/outdated:**
- The `QuestBoardContext.cs:257-259` comment claiming both `CharacterEntity` and `PlayerSignupEntity` are "intentionally unfiltered... reached only through an already-filtered navigation" — this needs correcting for BOTH entities (Character moves into the explicit `HasQueryFilter` block per D-02; PlayerSignup's comment needs to now describe the *actual*, more fragile mechanism — see Recommendation below).

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | `GetTopWaitlistedCandidateAsync`'s `questId` parameter is never independently attacker-controllable outside the already-validated quest object in current call sites (only 2 call sites exist, both internal to `QuestService`, both deriving `questId` from a controller-validated quest) | Summary / Pitfall 4 / Common Pitfalls | If a future call site passes a client-supplied `questId` directly to this method without first validating group membership on that quest, the same leak becomes independently exploitable — low risk today, given the currently-exhaustive caller audit, but worth flagging to the planner as a "don't introduce a new call site without a group check" constraint rather than treating it as permanently safe |
| A2 | The planner will treat the `RemovePlayerSignup` finding as an in-scope expansion of this phase (matching the phase's stated theme of closing tenant-filtering leaks, and matching how D-06's DungeonMasterController finding was pulled into scope after being discovered mid-discussion) rather than deferring it to a new phase | Summary / Recommendation | If deferred instead, this specific Admin-only cross-group deletion capability remains exploitable in production until a follow-up phase addresses it — CONTEXT.md's own `<unknowns>` section explicitly asked for a recommendation on this exact question |

**If this table is empty:** N/A — two assumptions logged above, both low-to-moderate risk, both flagged explicitly for planner attention.

## Open Questions

1. **Should `RemovePlayerSignup`'s fix be scoped to just this one route, or should `PlayerSignupRepository`'s underlying unfiltered methods (`GetByIdAsync` via base class, `ChangeVoteAsync`, `GetTopWaitlistedCandidateAsync`) get a durable fix (e.g., an explicit `.Include(ps => ps.Quest)` + inline group check, matching UserTransaction's `Include`-based pattern) even though their current callers already re-derive safely?**
   - What we know: `RemovePlayerSignup` is the only call site that is independently exploitable today. The other three methods' current callers all re-derive the ID from an already-filtered `quest.PlayerSignups` navigation first, so they are not independently exploitable in their PRESENT call sites.
   - What's unclear: Whether the planner should treat "not currently exploitable but incidentally so" the same way D-10/D-11 treated UserTransaction (document + regression-test + leave the incidental mechanism as-is) or whether PlayerSignup's case is different enough (no existing `.Include` at all, vs. UserTransaction's existing-and-consistently-applied `.Include`) to warrant adding an explicit check to all four methods rather than just the one exploitable route.
   - Recommendation: At minimum, fix `RemovePlayerSignup` (the one real, standalone vulnerability) by adding a `GetGroupRoleByIdAsync`-style check or an inline `quest.GroupId == activeGroupContext.ActiveGroupId` comparison after loading the signup's parent quest. For the other three methods, apply D-11's exact treatment: correct the stale `QuestBoardContext.cs` comment to accurately describe the mechanism (safe only when callers pre-validate via the Quest navigation, NOT safe as a general repository-level guarantee) and add one regression test proving the current callers' pattern holds, so a future refactor that removes the pre-validation step fails loudly. This mirrors D-11's own "document + test, don't add a second redundant filtering mechanism" preference.

2. **Does `RemovePlayerSignup` need a `NotFound()` (404) or `Forbid()` (403) response for a cross-group target signup?**
   - What we know: D-04 and D-09 both establish 404 as this phase's convention for cross-tenant existence-hiding, applied consistently to Character and DungeonMasterProfile.
   - What's unclear: Nothing, really — this should follow the same D-04/D-09 convention for consistency, but is called out explicitly since `RemovePlayerSignup` wasn't part of CONTEXT.md's original decision set.
   - Recommendation: 404, for consistency with every other cross-group-response decision already locked in this phase.

## Environment Availability

Skipped — this phase has no external tool/service dependencies beyond the existing SQL Server + EF Core stack, which is already confirmed operational (migrations auto-apply on startup per project convention, `dotnet build`/`dotnet test` already succeed in this environment as demonstrated by this research's own throwaway-test run).

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xunit.v3 3.2.2 + FluentAssertions 8.10.0 + NSubstitute 5.3.0 [VERIFIED: QuestBoard.UnitTests.csproj] |
| Config file | `QuestBoard.UnitTests/QuestBoard.UnitTests.csproj` |
| Quick run command | `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~PlayerSignupRepositoryTests\|FullyQualifiedName~CharacterRepositoryTests\|FullyQualifiedName~UserTransactionRepositoryTests\|FullyQualifiedName~DungeonMasterControllerTests"` |
| Full suite command | `dotnet test` |

### Phase Requirements → Test Map

> This is an ad-hoc bug-fix phase with no REQ-IDs; rows below map to CONTEXT.md decision IDs instead.

| Decision ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| D-01/D-02/D-03 | `GuildMembersController.Index`/`Details` scoped to active group; SuperAdmin-with-no-group sees empty list, not cross-group superview | unit (repository) + integration (controller) | `dotnet test --filter FullyQualifiedName~CharacterRepositoryTests` | ❌ Wave 0 — no `CharacterRepositoryTests.cs` exists yet |
| D-04 | `GetProfilePicture` returns 404 for a cross-group character ID | unit (repository) | `dotnet test --filter FullyQualifiedName~CharacterRepositoryTests` | ❌ Wave 0 — same new file as above |
| D-06/D-07/D-08/D-09 | `DungeonMasterController.Profile`/`EditProfile`(GET+POST)/`GetDMProfilePicture` all 404 for a cross-group target user; SuperAdmin-no-group also 404s | integration (controller) | `dotnet test --filter FullyQualifiedName~DungeonMasterControllerTests` (or equivalent `QuestBoard.IntegrationTests` project pattern) | ❌ Wave 0 — no existing controller-level test file found for this controller |
| D-10/D-11 | Cross-group `UserTransaction` excluded from `GetTransactionsByUserAsync`; `ReturnOrSellItemAsync` uses `GetTransactionWithDetailsAsync`, not the unguarded base `GetByIdAsync` | unit (repository) | `dotnet test --filter FullyQualifiedName~UserTransactionRepositoryTests` | ❌ Wave 0 — no `UserTransactionRepositoryTests.cs` exists yet (confirmed via search) |
| This research's `RemovePlayerSignup` finding | Cross-group `PlayerSignupEntity` deletion via `RemovePlayerSignup` is blocked (or repository-level fix applied and regression-tested) | unit (repository) + integration (controller, for the AdminOnly-policy path) | `dotnet test --filter FullyQualifiedName~PlayerSignupRepositoryTests` | ✅ existing file — extend with new test cases |

### Sampling Rate
- **Per task commit:** `dotnet test QuestBoard.UnitTests --filter "FullyQualifiedName~Character|FullyQualifiedName~DungeonMaster|FullyQualifiedName~UserTransaction|FullyQualifiedName~PlayerSignup"`
- **Per wave merge:** `dotnet test` (full suite — this project has `QuestBoard.UnitTests` and `QuestBoard.IntegrationTests`; confirm both run clean before phase gate)
- **Phase gate:** Full suite green before `/gsd-verify-work`

### Wave 0 Gaps
- [ ] `QuestBoard.UnitTests/Repository/CharacterRepositoryTests.cs` — new file; covers D-01/D-02/D-03/D-04 (list scoping, SuperAdmin-empty behavior, profile-picture cross-group 404 via the rewritten query)
- [ ] `QuestBoard.UnitTests/Repository/UserTransactionRepositoryTests.cs` — new file; covers D-11.2's regression test (cross-group transaction excluded from `GetTransactionsByUserAsync`)
- [ ] A `DungeonMasterController` integration/unit test file — none currently exists for this controller; covers D-06 through D-09's four hardened actions. Check whether `QuestBoard.IntegrationTests/Controllers/` is the right home (that project already hosts `AdminControllerIntegrationTests.cs`/`AdminHandlerIntegrationTests.cs`, suggesting controller-level auth tests live there, not in `QuestBoard.UnitTests`)
- [ ] Extend existing `QuestBoard.UnitTests/Repository/PlayerSignupRepositoryTests.cs` with cross-group regression coverage for whichever fix scope the planner chooses (see Open Question 1)

## Security Domain

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | No | Not touched by this phase — existing ASP.NET Core Identity untouched |
| V3 Session Management | No | `ActiveGroupId` session-read mechanism (`ActiveGroupContextService.cs`) is pre-existing and untouched by this phase |
| V4 Access Control | **Yes — this is the core of the phase** | EF Core `HasQueryFilter` for schema-scoped entities (Character); explicit `GetGroupRoleByIdAsync` membership checks for access-control-only entities (DungeonMasterProfile); target-resource group validation for admin-only mutation routes (RemovePlayerSignup) |
| V5 Input Validation | No new surface | Route `id` parameters are integers already model-bound by ASP.NET Core; no new validation library needed |
| V6 Cryptography | No | Not touched |

### Known Threat Patterns for this stack

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Insecure Direct Object Reference (IDOR) — any authenticated user can view/modify another tenant's resource by guessing/iterating IDs | Information Disclosure / Elevation of Privilege | EF Core global query filter (schema-scoped entities) or explicit group-membership check before every read/write keyed by a client-supplied ID (access-control-only entities) — this phase's entire scope is closing IDOR instances across three controllers |
| Confused deputy — an authorized action (Admin role, valid CSRF token) operating on an unauthorized target resource | Elevation of Privilege | Validate the TARGET resource's tenant/group membership, not just the caller's role — the recurring fix pattern in D-06, D-09, and this research's `RemovePlayerSignup` finding |
| Existence oracle via differentiated error responses (403 vs 404 leaking whether a resource exists in another tenant) | Information Disclosure | Return 404 uniformly for "doesn't exist" and "exists but not in your group" (D-04/D-09 convention), never 403, for cross-tenant resource access |

## Sources

### Primary (HIGH confidence)
- Direct codebase inspection (Read/Grep/RIP-equivalent tools) of every file/line reference cited in `49-CONTEXT.md` — all confirmed accurate against the current working tree with no semantic drift.
- Empirical throwaway xUnit test (written, executed via `dotnet test`, and deleted per protocol) against the actual `QuestBoardContext`/EF Core 10.0.9 InMemory provider — this is the authoritative evidence for the PlayerSignupEntity leak finding, not reasoning about EF Core's query translation behavior.

### Secondary (MEDIUM confidence)
- None used — this research relied entirely on direct codebase verification and empirical testing rather than external documentation, since the domain (this project's own multi-tenancy pattern) has no external authority beyond the codebase itself.

### Tertiary (LOW confidence)
- None.

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — no new packages; all versions confirmed directly from `.csproj` files
- Architecture: HIGH — every pattern mirrors an already-implemented, already-tested precedent in this exact codebase (Quest/ShopItem filters, UserTransaction Include-join mechanism)
- Pitfalls: HIGH — all four pitfalls are either directly documented in this codebase's own comments (Pitfall 1, 2) or empirically demonstrated in this research session (Pitfall 3, 4)
- PlayerSignupEntity leak finding: HIGH — confirmed via executed, passing throwaway test against the real `QuestBoardContext`, not static reasoning

**Research date:** 2026-07-05
**Valid until:** 30 days (stable internal codebase pattern, not a fast-moving external dependency)
