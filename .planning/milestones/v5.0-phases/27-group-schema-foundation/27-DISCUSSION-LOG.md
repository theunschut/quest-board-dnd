# Phase 27: Group Schema Foundation - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-06-29
**Phase:** 27-group-schema-foundation
**Areas discussed:** GroupId FK nullability, Migration structure, UserEntity navigation property, Seeding edge cases, Delete behavior, Group name uniqueness

---

## GroupId FK Nullability

| Option | Description | Selected |
|--------|-------------|----------|
| Non-nullable from day 1 | GroupId is `int` (not `int?`). Phase 27 also updates test seed helpers to set GroupId=1. Tests keep passing, schema is correct immediately. | ✓ |
| Nullable now, enforce in Phase 28 | GroupId is `int?` in Phase 27. Existing tests unchanged. Phase 28 flips to non-nullable. | |

**User's choice:** Non-nullable from day 1
**Notes:** User agreed with the recommendation without hesitation.

---

## Migration Structure

| Option | Description | Selected |
|--------|-------------|----------|
| One atomic migration | All 8 steps (schema + seeding + cleanup) in a single migration class. Atomic rollback. | ✓ |
| Two migrations: schema then data | Schema first, data+cleanup second. Leaves DB in broken state between migrations. | |

**User's choice:** One atomic migration

**Follow-up: Multi-role seeding edge case**

| Option | Description | Selected |
|--------|-------------|----------|
| Admin > DungeonMaster > Player | Highest role wins when user has multiple AspNetUserRoles entries. | ✓ |
| You decide | Claude picks priority. | |

**Notes:** User initially asked whether the junction table PK prevents multiple roles per user per group (it does — unique on (UserId, GroupId)). After clarification that the question was specifically about the one-time seeding migration, confirmed Admin > DM > Player priority.

---

## UserEntity Navigation Property

| Option | Description | Selected |
|--------|-------------|----------|
| Yes, add now | `public virtual ICollection<UserGroupEntity> UserGroups { get; set; } = [];` on UserEntity. | ✓ |
| No, query through DbSet | `_context.UserGroups.Where(ug => ug.UserId == id)`. Keeps UserEntity minimal. | |

**User's choice:** Yes, add the navigation property
**Notes:** User agreed with recommendation. Consistent with existing Quests/Signups nav collections.

---

## Seeding Edge Cases (additional area)

| Option | Description | Selected |
|--------|-------------|----------|
| Default to Player | Users with no AspNetUserRoles entry get GroupRole.Player. No silent data loss. | ✓ |
| Skip them | Don't add to UserGroups. Risk of invisible users after Phase 28. | |

**User's choice:** Default to Player

---

## Delete Behavior on UserGroups

| Option | Description | Selected |
|--------|-------------|----------|
| Both cascade-delete | UserEntity deleted → UserGroups cascade. GroupEntity deleted → UserGroups cascade. | ✓ |
| NoAction on both | Match QuestEntity/ShopItemEntity pattern. Requires manual cleanup before any delete. | |

**User's choice:** Both cascade-delete
**Notes:** Safe because Phase 29 only allows deleting empty groups — cascade is a no-op in the normal path.

---

## Group Name Uniqueness

| Option | Description | Selected |
|--------|-------------|----------|
| Unique index | Database-level uniqueness on GroupEntity.Name. | ✓ |
| No constraint | Application validates only. | |

**User's choice:** Unique index

---

## Claude's Discretion

- GroupRole enum values: `Player = 0`, `DungeonMaster = 1`, `Admin = 2` — 0-based, highest role numerically is Admin
- Migration name: `AddGroupSchema`
- FK on Quest→Group and ShopItem→Group uses NoAction (not Cascade) — groups should never be deleted while they have content; matches existing cross-entity FK pattern

## Deferred Ideas

None — discussion stayed within phase scope.
