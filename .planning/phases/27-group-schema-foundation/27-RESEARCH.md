# Phase 27: Group Schema Foundation - Research

**Researched:** 2026-06-29
**Domain:** EF Core migrations, ASP.NET Core Identity, .NET 10 clean architecture
**Confidence:** HIGH

---

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions
- **D-01:** `GroupId` is non-nullable (`int`) on both `QuestEntity` and `ShopItemEntity` from day 1. The migration sets all existing rows to GroupId=1 before creating the NOT NULL constraint. Phase 27 updates integration test seed helpers to set `GroupId = 1` on any Quest or ShopItem they create so all 191 tests continue to pass.
- **D-02:** One atomic migration covers all 8 steps: create `Groups` table, create `UserGroups` table, add `GroupId` FK column to `Quests`, add `GroupId` FK column to `ShopItems`, insert the EuphoriaInn group (GroupId=1), insert UserGroups rows for all existing users, update all existing Quests/ShopItems to GroupId=1, delete Player/DungeonMaster/Admin entries from `AspNetUserRoles`. Atomic = either all 8 steps apply or none.
- **D-03:** Multi-role edge case in seeding: if a user has multiple entries in `AspNetUserRoles`, the migration assigns the highest role: Admin > DungeonMaster > Player.
- **D-04:** Users with no `AspNetUserRoles` entry are seeded into `UserGroups` with `GroupRole = Player` (default).
- **D-05:** `GroupRole` enum (`Player = 0`, `DungeonMaster = 1`, `Admin = 2`) defined in `QuestBoard.Domain/Enums/`. `UserGroupEntity` stores it as `int`. `EntityProfile` maps `int GroupRole` ↔ `GroupRole GroupRole` in the domain model. Repository does NOT depend on the enum directly.
- **D-06:** `UserGroupEntity` has navigation properties to both `UserEntity` and `GroupEntity`. Composite primary key OR unique index on `(UserId, GroupId)`.
- **D-07:** `UserEntity` gets a `public virtual ICollection<UserGroupEntity> UserGroups { get; set; } = [];` navigation property.
- **D-08:** `GroupEntity.Name` has a unique index at the database level.
- **D-09:** `UserGroupEntity` FK to `UserEntity`: cascade-delete. FK to `GroupEntity`: cascade-delete.
- **D-10:** FK from `QuestEntity.GroupId` → `GroupEntity`: NoAction. FK from `ShopItemEntity.GroupId` → `GroupEntity`: NoAction.

### Claude's Discretion
None — discussion stayed within phase scope.

### Deferred Ideas (OUT OF SCOPE)
None — discussion stayed within phase scope.
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| GROUP-01 | `GroupEntity` table exists with `Id`, `Name`, `CreatedAt` columns | Entity class with IEntity, `CreateTable` in migration |
| GROUP-02 | `UserGroups` junction table exists with `UserId`, `GroupId`, `GroupRole` (enum: Player / DungeonMaster / Admin) | `UserGroupEntity` with nav props, unique index, `CreateTable` in migration |
| GROUP-03 | `GroupId` FK added to `QuestEntity` and `ShopItemEntity` | `AddColumn` with defaultValue=1, then `AlterColumn` to remove default; entity property + OnModelCreating config |
| GROUP-04 | Data migration seeds the `"EuphoriaInn"` group as `GroupId = 1` | `migrationBuilder.InsertData()` in Up() with explicit Id value |
| GROUP-05 | All existing users assigned to EuphoriaInn in `UserGroups`; `GroupRole` seeded from current `AspNetUserRoles` | Raw SQL `INSERT INTO UserGroups SELECT ...` with CTE for highest-role logic |
| GROUP-06 | `AspNetUserRoles` entries for Player / DungeonMaster / Admin removed after migration; only SuperAdmin remains | `migrationBuilder.Sql("DELETE FROM AspNetUserRoles WHERE RoleId IN (...)")` — role IDs from `AspNetRoles` |
</phase_requirements>

---

## Summary

Phase 27 is a pure database schema and data migration phase. It adds the multi-group foundation — `GroupEntity`, `UserGroupEntity`, `GroupId` FKs on shared-resource tables, and a single atomic migration that seeds all existing production data into the `EuphoriaInn` group — with zero runtime behavior change. The application's current functionality is entirely preserved because no authorization code, service layer, or UI is modified.

The project now runs on **.NET 10 / EF Core 10.0.9** (not .NET 8 as documented in STACK.md — the rename phase updated the runtime). Integration tests use EF Core **InMemory** provider via `WebApplicationFactoryBase`, not SQLite. The schema is built via `EnsureCreated()` from the current model state, so `GroupId` must be initialized to `1` in `TestDataHelper` factory methods — not just in the migration seed.

The highest-complexity part of the migration is the data seeding in Step 5–6: selecting the highest role per user from `AspNetUserRoles` (joined to `AspNetRoles`), inserting into `UserGroups`, then deleting the now-redundant Player/DungeonMaster/Admin rows. A SQL CTE handles the max-role logic cleanly. The `Down()` migration reverses all 8 steps in reverse order.

**Primary recommendation:** Write the migration as pure `MigrationBuilder` API calls (no raw SQL except for the data-seeding steps that read from Identity tables), follow the `NoAction` FK pattern for Quest/ShopItem → Group, and use `Cascade` for UserGroup → User/Group ownership FKs. Add `GroupId = 1` to both factory methods in `TestDataHelper` immediately.

---

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| GroupEntity / UserGroupEntity C# entity classes | Repository | — | EF entities live in QuestBoard.Repository.Entities per clean architecture |
| GroupRole enum definition | Domain | — | Enums live in QuestBoard.Domain.Enums per existing pattern (Role, SignupRole, etc.) |
| EntityProfile GroupRole mapping | Repository (Automapper) | — | EntityProfile is in QuestBoard.Repository.Automapper — maps int GroupRole ↔ GroupRole enum |
| QuestBoardContext DbSet + OnModelCreating | Repository | — | All EF configuration is in QuestBoardContext.cs |
| EF Core migration (AddGroupSchema) | Repository/Migrations | — | All migrations live in QuestBoard.Repository.Migrations |
| Domain model for Group/UserGroup | Domain | — | Group and UserGroup domain models belong in QuestBoard.Domain.Models per layer rules |
| GroupRepository | Repository | — | Extends BaseRepository<GroupEntity> — same pattern as all other repositories |
| Integration test factory updates | IntegrationTests | — | TestDataHelper.CreateTestQuestAsync / CreateShopItemAsync must set GroupId = 1 |

---

## Standard Stack

### Core (no new packages — all existing)

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Microsoft.EntityFrameworkCore | 10.0.9 | ORM, migrations | Already in project — [VERIFIED: csproj] |
| Microsoft.EntityFrameworkCore.SqlServer | 10.0.9 | SQL Server provider | Already in project — [VERIFIED: csproj] |
| Microsoft.AspNetCore.Identity.EntityFrameworkCore | 10.0.9 | Identity DbContext base | Already in project — [VERIFIED: csproj] |
| AutoMapper | 14.0.0 | Entity↔Domain enum mapping | Already in project — [ASSUMED: STACK.md; not reverified] |
| Microsoft.EntityFrameworkCore.InMemory | 10.0.9 | Integration test database | Already in project — [VERIFIED: csproj] |

**No new NuGet packages are required for this phase.**

### Package Legitimacy Audit

No new packages are installed in this phase. All dependencies are already present in the project. This section is satisfied by the existing packages listed above.

| Package | Registry | Verdict | Disposition |
|---------|----------|---------|-------------|
| Microsoft.EntityFrameworkCore | nuget | OK | Approved — existing dependency |
| Microsoft.EntityFrameworkCore.SqlServer | nuget | OK | Approved — existing dependency |
| Microsoft.EntityFrameworkCore.InMemory | nuget | OK | Approved — existing dependency |

**Packages removed due to SLOP verdict:** none
**Packages flagged as suspicious (SUS):** none

---

## Architecture Patterns

### System Architecture Diagram

```
EF Migration (AddGroupSchema)
        |
        v
SQL Server
  Groups (new)
  UserGroups (new)
  Quests.GroupId (FK added)
  ShopItems.GroupId (FK added)
  AspNetUserRoles (Player/DM/Admin rows DELETED)
        |
        v
QuestBoardContext.OnModelCreating
  +HasOne(q => q.Group).WithMany().HasForeignKey(q => q.GroupId).OnDelete(NoAction)
  +HasOne(si => si.Group).WithMany().HasForeignKey(si => si.GroupId).OnDelete(NoAction)
  +HasOne(ug => ug.User).WithMany(u => u.UserGroups).HasForeignKey(ug => ug.UserId).OnDelete(Cascade)
  +HasOne(ug => ug.Group).WithMany(g => g.UserGroups).HasForeignKey(ug => ug.GroupId).OnDelete(Cascade)
  +HasIndex(ug => new { ug.UserId, ug.GroupId }).IsUnique()
  +HasIndex(g => g.Name).IsUnique()
        |
        v
New Entity classes
  GroupEntity (Id, Name, CreatedAt)
  UserGroupEntity (Id, UserId, GroupId, GroupRole int)
        |
        v
New Domain objects
  GroupRole enum (Domain/Enums)
  Group domain model (Domain/Models)
  UserGroup domain model (Domain/Models)
        |
        v
EntityProfile
  + GroupRole int ↔ GroupRole enum mapping
```

### Recommended Project Structure

```
QuestBoard.Domain/
  Enums/
    GroupRole.cs          # NEW: Player=0, DungeonMaster=1, Admin=2
  Models/
    Group.cs              # NEW: Id, Name, CreatedAt domain model
    UserGroup.cs          # NEW: Id, UserId, GroupId, GroupRole domain model

QuestBoard.Repository/
  Entities/
    GroupEntity.cs        # NEW: IEntity, [Table("Groups")]
    UserGroupEntity.cs    # NEW: IEntity, [Table("UserGroups")]
  Automapper/
    EntityProfile.cs      # MODIFIED: add GroupRole int↔enum mapping
  Migrations/
    <timestamp>_AddGroupSchema.cs  # NEW: single atomic migration

QuestBoard.IntegrationTests/
  Helpers/
    TestDataHelper.cs     # MODIFIED: GroupId = 1 in CreateTestQuestAsync and CreateShopItemAsync
```

### Pattern 1: Non-Nullable FK Added to Existing Table (SQL Server safe approach)

**What:** Adding a non-nullable int FK to a table that already has rows. EF Core scaffolded migrations add the column with a defaultValue first, then remove the default.
**When to use:** Any time a NOT NULL column is added to an existing production table.

```csharp
// Source: EF Core migration pattern — verified in existing project migrations [ASSUMED: EF Core docs pattern]
// Step 1: Add column with default so existing rows get a valid value
migrationBuilder.AddColumn<int>(
    name: "GroupId",
    table: "Quests",
    type: "int",
    nullable: false,
    defaultValue: 0);  // Temporary default — overwritten in Step 5 (UpdateData)

// Step 2 (later in same migration): Update existing rows before the NOT NULL constraint is enforced
// For SQL Server, the column is already NOT NULL due to defaultValue=0.
// We then update all rows to GroupId=1 in the data seeding step.
migrationBuilder.Sql("UPDATE Quests SET GroupId = 1");
migrationBuilder.Sql("UPDATE ShopItems SET GroupId = 1");

// Note: No AlterColumn needed for SQL Server — defaultValue=0 creates the NOT NULL column.
// EF Core generates AddColumn with defaultValue which SQL Server enforces immediately.
// The defaultValue is NOT a DEFAULT constraint kept after migration — it's only for the initial add.
```

**Important SQL Server distinction:** When EF Core uses `defaultValue: 0` in `AddColumn`, SQL Server fills existing rows with 0 immediately. The column IS non-nullable from the moment it's created. We then UPDATE rows to GroupId=1 in the same migration before adding the FK constraint.

### Pattern 2: Junction Table with Unique Index (not composite PK)

**What:** A junction table using an auto-increment Id PK plus a unique index enforcing the one-row-per-user-per-group constraint. Follows the `PlayerDateVoteEntity` pattern already in the codebase.

```csharp
// Source: QuestBoard.Repository.Entities.PlayerDateVoteEntity [VERIFIED: codebase]
[Table("UserGroups")]
public class UserGroupEntity : IEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    public int UserId { get; set; }

    [Required]
    public int GroupId { get; set; }

    [Required]
    public int GroupRole { get; set; }  // Stored as int, mapped to GroupRole enum in EntityProfile

    [ForeignKey(nameof(UserId))]
    public virtual UserEntity User { get; set; } = null!;

    [ForeignKey(nameof(GroupId))]
    public virtual GroupEntity Group { get; set; } = null!;
}
```

OnModelCreating unique index (mirrors PlayerDateVoteEntity pattern):

```csharp
// Source: QuestBoardContext.OnModelCreating [VERIFIED: codebase]
modelBuilder.Entity<UserGroupEntity>()
    .HasIndex(ug => new { ug.UserId, ug.GroupId })
    .IsUnique();
```

### Pattern 3: Data Seeding in Migration with Raw SQL for Identity Tables

**What:** Reading from ASP.NET Identity tables (`AspNetUserRoles`, `AspNetRoles`) to populate new tables requires raw SQL because `migrationBuilder.InsertData()` cannot do joins.

```csharp
// Source: ConvertIsDungeonMasterToRoles migration pattern [VERIFIED: codebase]
// Step: Insert EuphoriaInn group with explicit Id = 1
migrationBuilder.InsertData(
    table: "Groups",
    columns: new[] { "Id", "Name", "CreatedAt" },
    values: new object[] { 1, "EuphoriaInn", DateTime.UtcNow });

// Step: Insert UserGroups rows — highest role per user
// Role IDs: Player=1, DungeonMaster=2, Admin=3 (as seeded by ConvertIsDungeonMasterToRoles migration)
// GroupRole mapping: Player=0, DungeonMaster=1, Admin=2 (new GroupRole enum)
migrationBuilder.Sql(@"
    INSERT INTO UserGroups (UserId, GroupId, GroupRole)
    SELECT
        u.Id,
        1,
        CASE
            WHEN MAX(CASE r.Name WHEN 'Admin' THEN 3 WHEN 'DungeonMaster' THEN 2 ELSE 1 END) = 3 THEN 2
            WHEN MAX(CASE r.Name WHEN 'Admin' THEN 3 WHEN 'DungeonMaster' THEN 2 ELSE 1 END) = 2 THEN 1
            ELSE 0
        END
    FROM AspNetUsers u
    LEFT JOIN AspNetUserRoles ur ON ur.UserId = u.Id
    LEFT JOIN AspNetRoles r ON r.Id = ur.RoleId AND r.Name IN ('Player', 'DungeonMaster', 'Admin')
    GROUP BY u.Id
");

// Step: Delete Player/DungeonMaster/Admin entries from AspNetUserRoles
migrationBuilder.Sql(@"
    DELETE ur FROM AspNetUserRoles ur
    INNER JOIN AspNetRoles r ON ur.RoleId = r.Id
    WHERE r.Name IN ('Player', 'DungeonMaster', 'Admin')
");
```

### Pattern 4: GroupEntity with Unique Name Index

```csharp
// Source: Pattern follows existing NoAction and HasIndex patterns [VERIFIED: codebase]
[Table("Groups")]
public class GroupEntity : IEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual ICollection<UserGroupEntity> UserGroups { get; set; } = [];
}
```

OnModelCreating:
```csharp
modelBuilder.Entity<GroupEntity>()
    .HasIndex(g => g.Name)
    .IsUnique();

// Quest → Group: NoAction (group must not cascade-delete quests)
modelBuilder.Entity<QuestEntity>()
    .HasOne(q => q.Group)
    .WithMany()
    .HasForeignKey(q => q.GroupId)
    .OnDelete(DeleteBehavior.NoAction);

// ShopItem → Group: NoAction
modelBuilder.Entity<ShopItemEntity>()
    .HasOne(si => si.Group)
    .WithMany()
    .HasForeignKey(si => si.GroupId)
    .OnDelete(DeleteBehavior.NoAction);

// UserGroup → User: Cascade
modelBuilder.Entity<UserGroupEntity>()
    .HasOne(ug => ug.User)
    .WithMany(u => u.UserGroups)
    .HasForeignKey(ug => ug.UserId)
    .OnDelete(DeleteBehavior.Cascade);

// UserGroup → Group: Cascade
modelBuilder.Entity<UserGroupEntity>()
    .HasOne(ug => ug.Group)
    .WithMany(g => g.UserGroups)
    .HasForeignKey(ug => ug.GroupId)
    .OnDelete(DeleteBehavior.Cascade);
```

### Pattern 5: GroupRole Enum (Domain Layer)

```csharp
// Source: Pattern follows QuestBoard.Domain.Enums.Role [VERIFIED: codebase]
namespace QuestBoard.Domain.Enums;

public enum GroupRole
{
    Player = 0,
    DungeonMaster = 1,
    Admin = 2
}
```

EntityProfile mapping addition:
```csharp
// Source: Pattern follows ShopItem Type/Rarity/Status mappings in EntityProfile.cs [VERIFIED: codebase]
CreateMap<UserGroupEntity, UserGroup>()
    .ForMember(dest => dest.GroupRole, opt => opt.MapFrom(src => (GroupRole)src.GroupRole));

CreateMap<UserGroup, UserGroupEntity>()
    .ForMember(dest => dest.GroupRole, opt => opt.MapFrom(src => (int)src.GroupRole));
```

### Anti-Patterns to Avoid

- **Adding FK constraint before filling the column:** SQL Server will reject adding an FK constraint if any rows have GroupId=0 and no corresponding group row. Always populate data before adding the FK constraint — the migration order within a single migration matters.
- **Using `migrationBuilder.InsertData()` for cross-table seeding:** `InsertData` is for simple static data. Use `migrationBuilder.Sql()` for any seeding that requires reading from other tables (like AspNetUserRoles).
- **Composite primary key on junction table:** The existing pattern (`PlayerDateVoteEntity`) uses an auto-increment Id PK + unique index, not composite PK. Composite PK would require a `HasKey(ug => new { ug.UserId, ug.GroupId })` call and removes the `[Key]` attribute — this diverges from the codebase pattern and makes the entity incompatible with `IEntity` (which requires `int Id`). Use unique index instead.
- **Adding a `[Table("UserGroups")]` entity without registering `DbSet` in QuestBoardContext:** EF Core will not discover the entity without a DbSet registration or explicit `modelBuilder.Entity<UserGroupEntity>()` call. Always add the DbSet to QuestBoardContext.
- **Forgetting to update TestDataHelper:** The InMemory provider builds schema from the EF model state via `EnsureCreated()`. `GroupId` is non-nullable, so any test that creates a QuestEntity or ShopItemEntity without setting GroupId will get a default of 0 — which has no corresponding row in Groups and can cause FK violations or silent incorrect behavior in future phases. Set `GroupId = 1` explicitly.
- **Not accounting for users with NULL AspNetUserRoles:** The LEFT JOIN in the seeding SQL ensures users with no role get GroupRole=0 (Player). An INNER JOIN would silently skip those users, violating D-04.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Highest-role-per-user selection | Custom C# loop with UserManager calls | Raw SQL in migration with GROUP BY + CASE | Migration runs at startup before DI is available; must be pure SQL |
| Unique user-per-group enforcement | Application-level uniqueness check | Database unique index on (UserId, GroupId) | Race conditions make app-level checks unreliable |
| Enum-to-int DB storage | Custom ValueConverter or string storage | Implicit int storage (no converter) | Established codebase pattern — matches ShopItem.Type/Rarity/Status |
| Non-nullable FK on existing table | Nullable column + app-level validation | `AddColumn` with `defaultValue: 0` + SQL UPDATE in migration | SQL Server requires all rows to have a value when column is added as NOT NULL |

---

## Common Pitfalls

### Pitfall 1: FK constraint added before data population

**What goes wrong:** Adding the FK constraint `REFERENCES Groups(Id)` while rows in `Quests` still have `GroupId=0` — no matching row in `Groups` — causes SQL Server to reject the migration with a FK violation error.

**Why it happens:** EF Core migration scaffolding would normally add the FK in `CreateTable` or `AddForeignKey`, but the migration author may not realize the column is populated AFTER the FK declaration.

**How to avoid:** In a single atomic migration, the ORDER of operations matters within `Up()`. Structure as:
1. CreateTable Groups
2. CreateTable UserGroups
3. AddColumn Quests.GroupId (defaultValue:0 fills existing rows with 0)
4. AddColumn ShopItems.GroupId (defaultValue:0 fills existing rows with 0)
5. InsertData: EuphoriaInn row (GroupId=1)
6. Sql UPDATE Quests/ShopItems SET GroupId=1
7. AddForeignKey Quests → Groups (AFTER rows have valid GroupId=1)
8. AddForeignKey ShopItems → Groups (AFTER rows have valid GroupId=1)
9. Sql INSERT INTO UserGroups (seeding from AspNetUserRoles)
10. Sql DELETE FROM AspNetUserRoles (Player/DM/Admin cleanup)

**Warning signs:** Migration apply fails with "The INSERT statement conflicted with the FOREIGN KEY constraint".

### Pitfall 2: Groups table Id auto-increment overriding explicit seed value

**What goes wrong:** `InsertData` with an explicit `Id = 1` on an IDENTITY column may fail if the identity seed hasn't been pre-set, or may succeed but then conflict with the next auto-generated Id.

**Why it happens:** SQL Server IDENTITY columns ignore explicit values unless `SET IDENTITY_INSERT Groups ON` is used.

**How to avoid:** Use `migrationBuilder.Sql()` with `SET IDENTITY_INSERT Groups ON / OFF` around the insert, OR use `InsertData` with `SqlServer:Identity` annotation turned off, OR accept that EF Core's `InsertData` in migrations automatically wraps with IDENTITY_INSERT for seed data when using `migrationBuilder.InsertData()`.

**EF Core 10 behavior:** `migrationBuilder.InsertData()` does NOT automatically handle IDENTITY_INSERT. Use raw SQL:
```sql
SET IDENTITY_INSERT Groups ON;
INSERT INTO Groups (Id, Name, CreatedAt) VALUES (1, 'EuphoriaInn', GETUTCDATE());
SET IDENTITY_INSERT Groups OFF;
```

**Warning signs:** Migration apply fails with "Cannot insert explicit value for identity column in table 'Groups' when IDENTITY_INSERT is set to OFF."

### Pitfall 3: InMemory provider ignores SQL in migrations — test failures

**What goes wrong:** Integration tests use InMemory EF Core provider. The schema is built via `EnsureCreated()` from the model state. SQL-based seeding in the migration NEVER runs during integration tests. Tests that call `CreateTestQuestAsync` without setting `GroupId` get `0`, which is technically valid for InMemory (no FK enforcement) but will produce wrong data in phases 28–30 that rely on GroupId filtering.

**Why it happens:** InMemory provider doesn't run migrations, doesn't enforce FKs, and doesn't execute raw SQL. The only thing that matters for the schema is the EF model at the time `EnsureCreated()` is called.

**How to avoid:** Set `GroupId = 1` in `TestDataHelper.CreateTestQuestAsync()` and `TestDataHelper.CreateShopItemAsync()` factory methods. Additionally verify `TestDataHelper.SeedRolesAsync` still seeds the roles that tests rely on — note that after Phase 27 the migration deletes Player/DM/Admin from `AspNetUserRoles` in production, but integration tests use InMemory and their `SeedRolesAsync` calls `RoleManager.CreateAsync` directly, so this is safe.

**Warning signs:** Tests pass but GroupId is 0 in test data; future tenant-isolation tests (Phase 28) get unexpected results.

### Pitfall 4: UserEntity IdentityUser<int> constraints

**What goes wrong:** Attempting to add columns or change the PK of `UserEntity` breaks Identity.

**Why it happens:** `UserEntity : IdentityUser<int>` maps to `AspNetUsers` which Identity manages. EF Core Identity conventions are strict.

**How to avoid:** Only add a **navigation property** to UserEntity — `public virtual ICollection<UserGroupEntity> UserGroups { get; set; } = [];`. This is safe. Navigation properties are not column additions; they only affect EF relationship tracking. Verified that this is explicitly called out in CONTEXT.md as safe.

**Warning signs:** EF migration scaffolding tries to add a column to AspNetUsers — if this happens, the entity change was more than a nav property.

### Pitfall 5: Down() migration must reverse all 8 steps

**What goes wrong:** An incomplete `Down()` leaves the schema in a half-migrated state, making rollback dangerous.

**How to avoid:** Reverse all 8 steps in `Down()` in reverse order:
1. Re-insert Player/DM/Admin entries back into AspNetUserRoles (from UserGroups data)
2. DELETE FROM UserGroups
3. DropForeignKey + DropColumn GroupId from ShopItems
4. DropForeignKey + DropColumn GroupId from Quests
5. DropTable UserGroups
6. DropTable Groups

Note: Full Down() fidelity requires re-inserting AspNetUserRoles from UserGroups data. If Down() fidelity is not required (no production rollback expected), a simplified Down() that only drops tables and columns is acceptable — document the trade-off explicitly.

---

## Code Examples

### Full Entity: GroupEntity

```csharp
// Source: Follows IEntity and [Table] pattern from QuestBoard.Repository.Entities [VERIFIED: codebase]
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuestBoard.Repository.Entities;

[Table("Groups")]
public class GroupEntity : IEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual ICollection<UserGroupEntity> UserGroups { get; set; } = [];
}
```

### Full Entity: UserGroupEntity

```csharp
// Source: Follows PlayerDateVoteEntity pattern [VERIFIED: codebase]
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuestBoard.Repository.Entities;

[Table("UserGroups")]
public class UserGroupEntity : IEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    public int UserId { get; set; }

    [Required]
    public int GroupId { get; set; }

    [Required]
    public int GroupRole { get; set; }

    [ForeignKey(nameof(UserId))]
    public virtual UserEntity User { get; set; } = null!;

    [ForeignKey(nameof(GroupId))]
    public virtual GroupEntity Group { get; set; } = null!;
}
```

### UserEntity navigation property addition

```csharp
// Source: Follows existing Quests/Signups nav pattern [VERIFIED: codebase]
// Add to UserEntity (no column change — safe with IdentityUser<int>):
public virtual ICollection<UserGroupEntity> UserGroups { get; set; } = [];
```

### QuestEntity GroupId property

```csharp
// Source: Follows DungeonMasterId FK pattern [VERIFIED: codebase]
public int GroupId { get; set; }

[ForeignKey(nameof(GroupId))]
public virtual GroupEntity Group { get; set; } = null!;
```

### ShopItemEntity GroupId property

```csharp
// Source: Follows CreatedByDmId FK pattern [VERIFIED: codebase]
public int GroupId { get; set; }

[ForeignKey(nameof(GroupId))]
public virtual GroupEntity Group { get; set; } = null!;
```

### TestDataHelper updates

```csharp
// CreateTestQuestAsync — add GroupId = 1 to the quest initializer
var quest = new QuestEntity
{
    Title = title,
    Description = description,
    ChallengeRating = challengeRating,
    DungeonMasterId = dungeonMasterId,
    IsFinalized = isFinalized,
    FinalizedDate = finalizedDate,
    DungeonMasterSession = dungeonMasterSession,
    TotalPlayerCount = 4,
    GroupId = 1,        // <-- ADD THIS
    CreatedAt = DateTime.UtcNow
};

// CreateShopItemAsync — add GroupId = 1 to the item initializer
var item = new ShopItemEntity
{
    Name = name,
    Description = "Test item description",
    Price = price,
    Quantity = quantity,
    Type = (int)type,
    Rarity = (int)rarity,
    Status = 1,
    CreatedByDmId = createdByDmId,
    GroupId = 1,        // <-- ADD THIS
    CreatedAt = DateTime.UtcNow
};
```

---

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Per-user roles in AspNetUserRoles | Per-group roles in UserGroups.GroupRole | Phase 27 (this phase) | AspNetUserRoles still exists for SuperAdmin only |
| No group concept | GroupEntity + UserGroups junction | Phase 27 (this phase) | Foundation for tenant isolation in Phase 28 |
| QuestEntity / ShopItemEntity unscoped | GroupId FK on both entities | Phase 27 (this phase) | Enables EF Global Query Filter in Phase 28 |

**Deprecated after this phase:**
- AspNetUserRoles entries for Player/DungeonMaster/Admin: removed by migration. Authorization handlers in Phases 29–30 must use UserGroups.GroupRole instead.

---

## Runtime State Inventory

This phase is a migration phase with data transformation. All five categories are answered explicitly.

| Category | Items Found | Action Required |
|----------|-------------|------------------|
| Stored data | AspNetUserRoles: rows for Player/DungeonMaster/Admin per user (3 roles × N users); Quests: N rows without GroupId; ShopItems: N rows without GroupId | Migration seeds UserGroups from AspNetUserRoles, then deletes Player/DM/Admin rows; updates Quests.GroupId and ShopItems.GroupId to 1 |
| Live service config | App runs on Linux at /opt/questboard/ with env overrides at /etc/questboard/.env — no group-related config exists yet | None — migration applied automatically on startup via context.Database.Migrate() |
| OS-registered state | No OS-level state references group or role names | None |
| Secrets/env vars | No env vars reference Player/DungeonMaster/Admin role names | None |
| Build artifacts | QuestBoard.Repository/Migrations/ — new migration file will be generated | Run `dotnet ef migrations add AddGroupSchema --project ../QuestBoard.Repository` from QuestBoard.Service/ directory |

---

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET 10 SDK | Build + migration | Yes | 10.0.301 | — |
| dotnet-ef global tool | Migration generation | Assumed present from prior phases | 10.x | Install: `dotnet tool install --global dotnet-ef` |
| SQL Server (localhost) | Migration apply in dev | Yes (per CLAUDE.md) | 2022 | — |

---

## Validation Architecture

### Test Framework

| Property | Value |
|----------|-------|
| Framework | xunit.v3 3.2.2 |
| Config file | `QuestBoard.IntegrationTests/xunit.runner.json` |
| Quick run command | `dotnet test QuestBoard.IntegrationTests --no-build` |
| Full suite command | `dotnet test` (from solution root) |

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| GROUP-01 | GroupEntity table exists with correct columns | Integration (InMemory schema) | `dotnet test` (existing tests build schema via EnsureCreated — new entity verified implicitly) | No schema-only test — inferred from build success |
| GROUP-02 | UserGroups table exists with (UserId, GroupId) unique | Integration (InMemory schema) | `dotnet test` — adding a UserGroupEntity and detecting duplicate would be Wave 0 gap | No existing test |
| GROUP-03 | QuestEntity and ShopItemEntity have GroupId | Integration — `CreateTestQuestAsync` / `CreateShopItemAsync` compile with GroupId=1 | `dotnet test` | Existing tests — MODIFIED |
| GROUP-04/05/06 | Data seeding correctness | Manual verify on dev SQL Server after migration | `dotnet run` + SQL query against local DB | No automated test — migration data seeding is not tested in InMemory |
| All | 191 existing tests pass | Integration | `dotnet test` | Yes |

### Sampling Rate
- **Per task commit:** `dotnet build` (catches compilation errors from model changes)
- **Per wave merge:** `dotnet test` (full 191-test suite)
- **Phase gate:** Full suite green + manual SQL Server migration apply verification

### Wave 0 Gaps
- No existing test explicitly verifies GroupEntity or UserGroupEntity schema
- Migration data seeding (GROUP-04/05/06) has no automated test — planner should add a Wave 0 smoke test that applies the migration against a dev SQL Server and verifies row counts

*(Full 191 existing integration tests cover GROUP-03 implicitly once TestDataHelper is updated)*

---

## Security Domain

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | No | No auth changes in this phase |
| V3 Session Management | No | No session changes in this phase |
| V4 Access Control | No | No authorization changes in this phase — GroupRole data exists but is not used for access decisions until Phase 29 |
| V5 Input Validation | No | No user input processed in this phase |
| V6 Cryptography | No | No cryptographic operations |

**Security note:** The migration deletes Player/DungeonMaster/Admin entries from `AspNetUserRoles`. No authorization handler reads `AspNetUserRoles` for these roles in the current codebase — `DungeonMasterHandler` and `AdminHandler` use `IsInRoleAsync()` which reads from Identity's cached claims. After Phase 27, those handlers will still work IF users were signed in before the migration (claims in cookie). New logins after Phase 27 will find no Player/DM/Admin roles and the existing handlers will fail. This is acceptable because Phases 28–29 replace the handlers — but it means Phase 27 MUST be deployed as an atomic part of the full Phase 27–29 release OR deployed when no users are signed in.

**Risk:** If Phase 27 is deployed alone to production without Phases 28–29, existing users' authorization will break on next login (DungeonMaster/Admin policies will fail since no Identity roles remain). **Document this in the plan as a deployment constraint.**

---

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | Role IDs in AspNetRoles are Player=1, DungeonMaster=2, Admin=3 (as seeded by ConvertIsDungeonMasterToRoles migration) | Pattern 3 SQL examples | If IDs differ, GroupRole seeding SQL assigns wrong roles — verify with SELECT * FROM AspNetRoles before deploying |
| A2 | No user in production has both Admin and DungeonMaster entries (D-03 safety case) | Pitfall section | Handled by MAX(CASE) logic — low risk |
| A3 | dotnet-ef global tool is installed on the development machine | Environment Availability | Will block migration generation — install if missing |
| A4 | STACK.md references to .NET 8 / EF Core 8 are outdated; project now runs .NET 10 / EF Core 10.0.9 | Standard Stack | Verified via csproj — confirmed correct |

---

## Open Questions (RESOLVED)

1. **Down() migration fidelity for AspNetUserRoles**
   - What we know: The Up() migration deletes Player/DM/Admin entries from AspNetUserRoles. To fully reverse, Down() must re-insert them — but the original entries are gone.
   - What's unclear: Is a fully reversible Down() required, or is a "drops tables/columns only" Down() acceptable?
   - Recommendation: Implement Down() that re-inserts AspNetUserRoles rows from UserGroups data for completeness. If deemed impractical, document explicitly in migration comments that Down() is a schema-only rollback and data is not restored.
   - **RESOLVED:** Plan 02 implements Down() that re-inserts AspNetUserRoles from UserGroups before dropping tables/columns. If re-insertion is impractical in practice, migration comment documents that Down() is schema-only rollback and data is not restored.

2. **Production deployment constraint (Phase 27 alone breaks auth)**
   - What we know: Deleting Player/DM/Admin from AspNetUserRoles means the existing DungeonMasterHandler/AdminHandler fail on new logins after Phase 27 is deployed without Phase 29.
   - What's unclear: Will Phase 27–29 be deployed as a batch, or will Phase 27 be deployed standalone?
   - Recommendation: Plan must include a deployment note: Phase 27 is safe ONLY if deployed with Phases 28–29 simultaneously, OR if deployed during a maintenance window where no users will log in until Phase 29 is also deployed.
   - **RESOLVED:** Phase 27 must deploy with Phases 28–29 or during a maintenance window. Documented in AddGroupSchema migration comment per Plan 03 Task 1, which includes a blocking deployment checklist checkpoint.

---

## Sources

### Primary (MEDIUM confidence — codebase verified)
- `QuestBoard.Repository/Entities/QuestBoardContext.cs` — OnModelCreating patterns for FK configuration, NoAction, HasIndex
- `QuestBoard.Repository/Entities/PlayerDateVoteEntity.cs` — junction table with unique index pattern
- `QuestBoard.Repository/Entities/QuestEntity.cs` — FK property + navigation property pattern
- `QuestBoard.Repository/Entities/ShopItemEntity.cs` — FK property + navigation property pattern
- `QuestBoard.Repository/Entities/UserEntity.cs` — IdentityUser<int> nav property pattern
- `QuestBoard.Repository/Automapper/EntityProfile.cs` — int↔enum mapping pattern
- `QuestBoard.Repository/Migrations/20250704211037_ConvertIsDungeonMasterToRoles.cs` — raw SQL in migration for Identity tables
- `QuestBoard.Repository/Migrations/20260626190255_AddReminderLog.cs` — CreateTable + FK migration pattern
- `QuestBoard.IntegrationTests/Helpers/TestDataHelper.cs` — factory methods requiring GroupId=1 update
- `QuestBoard.IntegrationTests/WebApplicationFactoryBase.cs` — InMemory EF Core provider (no migrations, EnsureCreated)
- `QuestBoard.Repository/QuestBoard.Repository.csproj` — confirmed EF Core 10.0.9

### Tertiary (LOW confidence — CONTEXT.md decisions)
- `.planning/phases/27-group-schema-foundation/27-CONTEXT.md` — all locked decisions D-01 through D-10

## Metadata

**Confidence breakdown:**
- Standard Stack: HIGH — all packages verified via csproj files
- Architecture: HIGH — verified against live codebase
- Migration patterns: HIGH — derived from existing migrations in the project
- Pitfalls: MEDIUM — based on SQL Server EF Core behavior knowledge + codebase patterns

**Research date:** 2026-06-29
**Valid until:** 2026-07-29 (stable stack — EF Core 10.x releases are infrequent)
