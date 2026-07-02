# Assigning the SuperAdmin Role

`SuperAdmin` is a platform-wide role (separate from the per-group `Admin`/`DungeonMaster`/`Player`
roles) that grants access to `/platform/*` — cross-group management (create/delete groups, manage
members across all groups).

## Why this is a manual SQL step

The `AddSuperAdminRole` migration (`QuestBoard.Repository/Migrations/20260630132256_AddSuperAdminRole.cs`)
only inserts the **role definition** into `AspNetRoles` (`Id = 4`, `Name = "SuperAdmin"`). It does
**not** assign any user to it. This was a deliberate choice from Phase 29 (see `T-29-M-01` in
`29-02-PLAN.md`) — keeping role-creation and role-assignment as separate steps means no user
automatically becomes SuperAdmin just from running migrations.

## Assign a user

Run against the app's SQL Server database (find the target user's GUID via email first):

```sql
INSERT INTO AspNetUserRoles (UserId, RoleId)
VALUES (
    (SELECT Id FROM AspNetUsers WHERE Email = 'user@example.com'),
    4
);
```

Or if you already have the `UserId` GUID:

```sql
INSERT INTO AspNetUserRoles (UserId, RoleId)
VALUES ('<userId-guid>', 4);
```

## Verify

```sql
SELECT u.Email, r.Name
FROM AspNetUserRoles ur
JOIN AspNetUsers u ON u.Id = ur.UserId
JOIN AspNetRoles r ON r.Id = ur.RoleId
WHERE r.Name = 'SuperAdmin';
```

## Remove SuperAdmin from a user

```sql
DELETE FROM AspNetUserRoles
WHERE RoleId = 4
  AND UserId = (SELECT Id FROM AspNetUsers WHERE Email = 'user@example.com');
```

Note: the migration's `Down()` only deletes the `SuperAdmin` role row itself (`Id = 4`) — if a user
still has that role assigned, their `AspNetUserRoles` row is left orphaned (ASP.NET Identity has no
FK cascade from `AspNetRoles` to `AspNetUserRoles` here). Remove user assignments before rolling
back the migration if that matters to you.
