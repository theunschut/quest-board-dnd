# Phase 27: Group Schema Foundation - Context

**Gathered:** 2026-06-29
**Status:** Ready for planning

<domain>
## Phase Boundary

Add the multi-group database schema: `GroupEntity` and `UserGroups` junction table, `GroupId` FKs on `QuestEntity` and `ShopItemEntity`, and a single atomic EF Core migration that seeds all existing data into the EuphoriaInn group and removes legacy per-user Identity role entries.

This phase delivers **schema and data migration only** — no runtime tenant isolation, no authorization changes, no UI. Those belong to Phases 28–30.

</domain>

<decisions>
## Implementation Decisions

### GroupId FK Strategy
- **D-01:** `GroupId` is non-nullable (`int`, not `int?`) on both `QuestEntity` and `ShopItemEntity` from day 1. The migration sets all existing rows to GroupId=1 before creating the NOT NULL constraint, so production apply is safe. Phase 27 also updates integration test seed helpers to set `GroupId = 1` on any Quest or ShopItem they create, so all 191 tests continue to pass.

### Migration Structure
- **D-02:** One atomic migration covers all 8 steps: create `Groups` table, create `UserGroups` table, add `GroupId` FK column to `Quests`, add `GroupId` FK column to `ShopItems`, insert the EuphoriaInn group (GroupId=1), insert UserGroups rows for all existing users, update all existing Quests/ShopItems to GroupId=1, delete Player/DungeonMaster/Admin entries from `AspNetUserRoles`. Atomic = either all 8 steps apply or none; clean rollback.
- **D-03:** Multi-role edge case in seeding: if a user has multiple entries in `AspNetUserRoles` (e.g., both Admin and DungeonMaster), the migration assigns the highest role: Admin > DungeonMaster > Player. In practice data is clean; this just makes the migration safe.
- **D-04:** Users with no `AspNetUserRoles` entry (edge case) are seeded into `UserGroups` with `GroupRole = Player` (default). No user is left without a group membership — avoids silent data loss after Phase 28 query filters land.

### GroupRole Enum
- **D-05:** `GroupRole` enum (`Player = 0`, `DungeonMaster = 1`, `Admin = 2`) defined in `QuestBoard.Domain/Enums/`. `UserGroupEntity` stores it as `int` (matches ShopItemEntity.Type/Rarity/Status pattern). `EntityProfile` maps `int GroupRole` ↔ `GroupRole GroupRole` in the domain model. Repository does NOT depend on the enum directly.

### Entity Design
- **D-06:** `UserGroupEntity` has navigation properties to both `UserEntity` and `GroupEntity`. Composite primary key OR unique index on `(UserId, GroupId)` — one row per user per group, enforced at the database level.
- **D-07:** `UserEntity` gets a `public virtual ICollection<UserGroupEntity> UserGroups { get; set; } = [];` navigation property — consistent with existing Quests/Signups nav collections. Phases 29–30 can traverse `user.UserGroups` instead of raw DbSet queries.
- **D-08:** `GroupEntity.Name` has a unique index at the database level (one-liner in `OnModelCreating`). Prevents duplicate group names.

### Cascade / Delete Behavior
- **D-09:** `UserGroupEntity` FK to `UserEntity`: cascade-delete (user deleted → their group memberships removed). FK to `GroupEntity`: cascade-delete (group deleted → its UserGroups rows removed). Safe because Phase 29 only allows deleting empty groups — cascade is a no-op in the normal path, but prevents orphan rows if deletion is forced.
- **D-10:** FK from `QuestEntity.GroupId` → `GroupEntity`: NoAction (matching existing QuestEntity FK pattern). FK from `ShopItemEntity.GroupId` → `GroupEntity`: NoAction. Groups should never be deleted while they have content.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Requirements & Scope
- `.planning/REQUIREMENTS.md` §Group Schema — GROUP-01 through GROUP-06: exact requirements and acceptance criteria for this phase
- `.planning/ROADMAP.md` §Phase 27 — phase goal, success criteria, dependency on Phase 26

### Architecture Constraints
- `.planning/codebase/ARCHITECTURE.md` — layer dependency direction (Service → Domain → Repository); enum placement pattern; AutoMapper boundary rules
- `.planning/codebase/STACK.md` §Database — EF Core migration approach, SQLite integration test setup with `EnsureCreated()` (not migrations)

### Key Implementation Files
- `QuestBoard.Repository/Entities/QuestBoardContext.cs` — where new DbSets and OnModelCreating configuration must be added
- `QuestBoard.Repository/Entities/QuestEntity.cs` — entity receiving GroupId FK
- `QuestBoard.Repository/Entities/ShopItemEntity.cs` — entity receiving GroupId FK
- `QuestBoard.Repository/Entities/UserEntity.cs` — entity receiving UserGroups navigation property
- `QuestBoard.Domain/Automapper/EntityProfile.cs` — where GroupRole int↔enum mapping must be added
- `QuestBoard.Repository/Migrations/` — existing migration conventions to follow

### Locked Decisions (from prior phases)
- `.planning/STATE.md` §Key Architectural Decisions (v5.0) — locked: per-group roles in UserGroups.GroupRole; AspNetUserRoles used only for SuperAdmin; Global Query Filters scoped to QuestEntity/ShopItemEntity only (Phase 28)

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `IEntity` marker interface (`QuestBoard.Repository/Entities/IEntity.cs`) — `GroupEntity` and `UserGroupEntity` should implement it
- `BaseRepository<T>` — a `GroupRepository` can extend this for standard CRUD

### Established Patterns
- **Entity FK pattern:** Data annotations (`[ForeignKey(nameof(X))]`) + virtual navigation property, backed by `OnModelCreating` relationship config. Follow QuestEntity/ShopItemEntity as the reference.
- **Delete behavior:** Use `OnDelete(DeleteBehavior.NoAction)` for cross-entity FKs (Quest→Group, ShopItem→Group) to match existing pattern. Use `OnDelete(DeleteBehavior.Cascade)` for ownership FKs (UserGroup→User, UserGroup→Group).
- **Enum as int:** Stored as `int` in entity (no explicit conversion), defined as enum in Domain, mapped in `EntityProfile`. ShopItemEntity.Type/Rarity/Status are the reference pattern.
- **Composite keys or unique indexes:** See `PlayerDateVoteEntity` unique index `(PlayerSignupId, ProposedDateId)` — same pattern for `UserGroups (UserId, GroupId)`.
- **Migration seeding:** Use `migrationBuilder.InsertData()` / `migrationBuilder.UpdateData()` / `migrationBuilder.DeleteData()` for data seeding in Up(); reverse in Down(). See existing migration files for format.

### Integration Points
- **Integration tests:** Use SQLite + `EnsureCreated()` (NOT migrations). Adding non-nullable `GroupId` to `QuestEntity` and `ShopItemEntity` requires updating test entity factories/seed helpers to set `GroupId = 1`. Check `QuestBoard.IntegrationTests/` for all locations that create `QuestEntity` or `ShopItemEntity` instances.
- **QuestBoardContext.OnModelCreating:** All new relationships, unique indexes, and cascade behaviors must be configured here. Existing NoAction pattern for SQL Server cascade avoidance.
- **AspNetUserRoles seeding read:** The migration's Up() must `SELECT` from `AspNetUserRoles` joined to `AspNetRoles` (to get role names) to determine each user's GroupRole before inserting into UserGroups. Then DELETE the Player/DungeonMaster/Admin rows.

### Known Landmines
- `UserEntity : IdentityUser<int>` — Identity puts strong restrictions on changes to the user entity. Adding a nav property (`UserGroups`) is safe; adding columns is not (and we're not adding any).
- SQLite used in integration tests does not run EF migrations — schema is built from model state via `EnsureCreated()`. GroupId must be initialized to 1 in all test entity constructors/factories, not just migration seeds.
- `AspNetRoles` table uses int PKs — when joining to look up role names, join on `IdentityRole<int>.Id` (int), not string.

</code_context>

<specifics>
## Specific Ideas

- Migration name suggestion: `AddGroupSchema` — covers the scope clearly
- `GroupRole` enum values: `Player = 0`, `DungeonMaster = 1`, `Admin = 2` — matches existing DM/Admin conceptual hierarchy; 0-based matches EF Core default storage

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope.

</deferred>

---

*Phase: 27-Group-Schema-Foundation*
*Context gathered: 2026-06-29*
