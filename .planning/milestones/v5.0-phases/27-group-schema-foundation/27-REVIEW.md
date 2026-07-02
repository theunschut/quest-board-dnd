---
phase: 27-group-schema-foundation
reviewed: 2026-06-30T10:00:00Z
depth: standard
files_reviewed: 12
files_reviewed_list:
  - QuestBoard.Domain/Enums/GroupRole.cs
  - QuestBoard.Repository/Entities/GroupEntity.cs
  - QuestBoard.Repository/Entities/UserGroupEntity.cs
  - QuestBoard.Domain/Models/Group.cs
  - QuestBoard.Domain/Models/UserGroup.cs
  - QuestBoard.Repository/Entities/QuestEntity.cs
  - QuestBoard.Repository/Entities/ShopItemEntity.cs
  - QuestBoard.Repository/Entities/UserEntity.cs
  - QuestBoard.Repository/Entities/QuestBoardContext.cs
  - QuestBoard.Repository/Automapper/EntityProfile.cs
  - QuestBoard.Repository/Migrations/20260630055221_AddGroupSchema.cs
  - QuestBoard.IntegrationTests/Helpers/TestDataHelper.cs
findings:
  critical: 0
  warning: 3
  info: 3
  total: 6
status: issues_found
---

# Phase 27: Code Review Report

**Reviewed:** 2026-06-30T10:00:00Z
**Depth:** standard
**Files Reviewed:** 12
**Status:** issues_found

## Summary

Phase 27 introduces the group schema foundation: `GroupEntity`, `UserGroupEntity`, the `GroupRole` enum, domain models, AutoMapper mappings, a migration, and integration test helpers. The implementation is well-structured and the migration is carefully ordered to avoid the FK-before-data pitfall documented in research. The deployment constraint comment (Phases 27-29 co-deployment requirement) is correct and appropriate.

Three warnings and three info items were found. No critical issues (no security vulnerabilities, no data-loss paths, no crashes). The most significant finding is a domain model gap: `Quest` and `ShopItem` domain models are missing `GroupId`, which means AutoMapper silently drops the column on every round-trip through the domain layer. This is a latent correctness bug that will surface as soon as any code reads or writes quests/shop items through the repository interface.

---

## Warnings

### WR-01: `GroupId` missing from `Quest` and `ShopItem` domain models — AutoMapper silently loses the value on every round-trip

**File:** `QuestBoard.Domain/Models/QuestBoard/Quest.cs` (entire file) and `QuestBoard.Domain/Models/Shop/ShopItem.cs` (entire file)

**Issue:** `QuestEntity.GroupId` was added in this phase (line 49 of `QuestEntity.cs`), and `ShopItemEntity.GroupId` was added at line 54 of `ShopItemEntity.cs`. However, the corresponding domain models — `Quest` and `ShopItem` — have no `GroupId` property. AutoMapper's `CreateMap<QuestEntity, Quest>()` and `CreateMap<Quest, QuestEntity>()` mappings are configured with `ReverseMap`-style bidirectional conventions and no explicit ignore for `GroupId`. This means:

- `QuestEntity → Quest`: the `GroupId` value is silently discarded (no corresponding property on `Quest`).
- `Quest → QuestEntity`: `GroupId` is written as `0` (int default), overwriting the correct value in the database on any update path that maps through the domain layer.

The migration correctly seeds `GroupId = 1` for existing rows, but any quest update that goes through `mapper.Map<QuestEntity>(quest)` will reset `GroupId` to `0`, violating the FK constraint added in the migration (step 7). This will cause a runtime exception (`FK_Quests_Groups_GroupId` violation) or silently corrupt data depending on EF tracking state.

**Fix:** Add `GroupId` to both domain models:

```csharp
// QuestBoard.Domain/Models/QuestBoard/Quest.cs
public int GroupId { get; set; }

// QuestBoard.Domain/Models/Shop/ShopItem.cs
public int GroupId { get; set; }
```

If `GroupId` is intentionally excluded from the domain model for this phase (because group-scoped reads are not yet implemented), then it must be explicitly ignored in both mapping directions in `EntityProfile.cs` to prevent silent zeroing:

```csharp
// EntityProfile.cs — Quest mappings
CreateMap<Quest, QuestEntity>()
    .ForMember(dest => dest.GroupId, opt => opt.Ignore())   // add this
    .ForMember(dest => dest.OriginalQuest, opt => opt.Ignore())
    .ForMember(dest => dest.FollowUpQuest, opt => opt.Ignore());
```

---

### WR-02: `UserGroupEntity.GroupRole` stored as `int` — no range validation, invalid enum values accepted silently

**File:** `QuestBoard.Repository/Entities/UserGroupEntity.cs:20`

**Issue:** `GroupRole` is stored as `int` (matching the existing pattern in the codebase for enum columns). However, unlike most other enum columns (e.g., `PlayerSignupEntity.SignupRole`), there is no `[Range]` attribute or DB check constraint preventing out-of-range values. If code writes an arbitrary integer (e.g., `99`) via direct entity construction or a misconfigured mapping, the cast `(GroupRole)src.GroupRole` in `EntityProfile.cs` line 125 will produce an undefined enum value that silently passes through to authorization logic in Phase 29. Given that this field will drive authorization decisions (Phase 29 replaces Identity role claims with `UserGroups.GroupRole`), silent acceptance of invalid values is a correctness risk.

This is the existing convention for all enum columns in the repository, so adding validation here is not a breaking deviation — it's a hardening opportunity before the field carries auth semantics.

**Fix:** Add a range constraint matching the defined enum values:

```csharp
[Range(0, 2, ErrorMessage = "GroupRole must be a valid GroupRole enum value (0=Player, 1=DungeonMaster, 2=Admin).")]
public int GroupRole { get; set; }
```

Or enforce at the DB level in the `OnModelCreating` configuration with a check constraint (SQL Server supports these via `modelBuilder.Entity<UserGroupEntity>().ToTable(t => t.HasCheckConstraint("CK_UserGroups_GroupRole", "[GroupRole] BETWEEN 0 AND 2"))`).

---

### WR-03: `TestDataHelper` hardcodes `GroupId = 1` without ensuring the group row exists — tests may fail on clean databases

**File:** `QuestBoard.IntegrationTests/Helpers/TestDataHelper.cs:31` and `:108`

**Issue:** `CreateTestQuestAsync` (line 31) and `CreateShopItemAsync` (line 108) both hardcode `GroupId = 1`, assuming the `EuphoriaInn` seed row with `Id = 1` is present. However, `ClearDatabaseAsync` (line 164) calls `EnsureDeletedAsync` + `EnsureCreatedAsync`, which recreates the schema but does **not** re-run EF migrations — so the seed data inserted by migration step 5 (`INSERT INTO Groups (Id, Name, CreatedAt) VALUES (1, 'EuphoriaInn', ...)`) will not be present after a `ClearDatabaseAsync` call. Any test that calls `ClearDatabaseAsync` before `CreateTestQuestAsync` or `CreateShopItemAsync` will fail with an FK constraint violation on `FK_Quests_Groups_GroupId` or `FK_ShopItems_Groups_GroupId`.

**Fix:** Add a seed-group helper and call it from `ClearDatabaseAsync`, or create the group in each helper method if it does not already exist:

```csharp
public static async Task SeedDefaultGroupAsync(IServiceProvider services)
{
    using var scope = services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<QuestBoardContext>();
    if (!context.Groups.Any(g => g.Id == 1))
    {
        context.Groups.Add(new GroupEntity { Id = 1, Name = "EuphoriaInn", CreatedAt = DateTime.UtcNow });
        await context.SaveChangesAsync();
    }
}
```

Call `await SeedDefaultGroupAsync(services)` at the end of `ClearDatabaseAsync`, after `SeedRolesAsync`.

---

## Info

### IN-01: `Group` domain model does not expose `UserGroups` — potential friction for future group-membership queries

**File:** `QuestBoard.Domain/Models/Group.cs:1-8`

**Issue:** `GroupEntity` has a `ICollection<UserGroupEntity> UserGroups` navigation property (line 19), but the `Group` domain model has no corresponding collection. This is intentional for a foundation phase, but when Phase 28/29 code needs to query members of a group through the domain layer, there will be no way to access this data without adding the property. The AutoMapper `CreateMap<GroupEntity, Group>().ReverseMap()` at line 121 of `EntityProfile.cs` will silently ignore `UserGroups` on the entity-to-model direction.

**Fix:** No immediate action required. When Phase 28 adds group-membership queries, add `IList<UserGroup> Members { get; set; } = [];` to `Group.cs` and update the mapping.

---

### IN-02: `GroupEntity.CreatedAt` defaults via C# initializer, not `defaultValueSql` — value is set at object construction time, not at DB insertion time

**File:** `QuestBoard.Repository/Entities/GroupEntity.cs:17`

**Issue:** `CreatedAt` is initialized with `DateTime.UtcNow` in the C# property initializer. This is the existing pattern in the codebase (`QuestEntity.cs`, `ShopItemEntity.cs` both do the same), so it is consistent. However, it means the timestamp is set when the entity object is created in memory, not when EF commits it to the database. If an entity object is held in memory for a meaningful period before being saved, the `CreatedAt` value will be earlier than the actual insertion time. For a `CreatedAt` field this is low risk, but it is worth noting as a known pattern deviation from `defaultValueSql: "GETUTCDATE()"`.

**Fix:** No immediate action required (consistent with codebase convention). If precise DB-side timestamps are needed in a future phase, consider `defaultValueSql` in the model configuration.

---

### IN-03: Migration `Down()` re-inserts `AspNetUserRoles` from `UserGroups` but does not guard against duplicate rows

**File:** `QuestBoard.Repository/Migrations/20260630055221_AddGroupSchema.cs:189-200`

**Issue:** The `Down()` reversal at lines 189-200 re-inserts `AspNetUserRoles` rows from `UserGroups` using a plain `INSERT INTO AspNetUserRoles (UserId, RoleId) SELECT ...`. If any `AspNetUserRoles` rows for these users already exist at rollback time (e.g., from a partial migration or manual intervention), this insert will fail with a primary key violation. `AspNetUserRoles` has a composite PK on `(UserId, RoleId)`.

The migration comment at line 185 acknowledges this is "best-effort restoration", but a silent failure mode (unhandled exception during `Down()`) is worse than a documented limitation.

**Fix:** Use `INSERT INTO ... SELECT ... WHERE NOT EXISTS (...)` to make the rollback idempotent:

```sql
INSERT INTO AspNetUserRoles (UserId, RoleId)
SELECT ug.UserId,
    CASE ug.GroupRole WHEN 2 THEN 3 WHEN 1 THEN 2 ELSE 1 END
FROM UserGroups ug
WHERE ug.GroupId = 1
AND NOT EXISTS (
    SELECT 1 FROM AspNetUserRoles existing
    WHERE existing.UserId = ug.UserId
    AND existing.RoleId = CASE ug.GroupRole WHEN 2 THEN 3 WHEN 1 THEN 2 ELSE 1 END
)
```

---

_Reviewed: 2026-06-30T10:00:00Z_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
