---
phase: 27-group-schema-foundation
verified: 2026-06-30T08:00:00Z
status: human_needed
score: 10/12 must-haves verified
overrides_applied: 0
human_verification:
  - test: "Confirm GROUP-04/05/06 data seeding on live SQL Server"
    expected: "Groups table has exactly one row (Id=1, Name='EuphoriaInn'); every AspNetUsers row has a matching UserGroups row with correct GroupRole; AspNetUserRoles contains no Player/DM/Admin rows; all Quests and ShopItems have GroupId=1"
    why_human: "Migration raw SQL (IDENTITY_INSERT, LEFT JOIN seeding, AspNetUserRoles DELETE) runs only on SQL Server — the InMemory integration tests cannot exercise this path. Plan 03 documented a human-verify checkpoint that was completed and approved, but this verifier cannot re-run SQL Server spot-checks programmatically."
  - test: "Confirm REQUIREMENTS.md traceability is updated for GROUP-04/05/06"
    expected: "GROUP-04, GROUP-05, GROUP-06 checkboxes marked [x] and traceability table shows 'Complete' (currently shows 'Pending')"
    why_human: "REQUIREMENTS.md still shows GROUP-04/05/06 as unchecked and 'Pending' despite all three being implemented and human-verified in Plan 03. This is a documentation update only — no code change required."
---

# Phase 27: Group Schema Foundation Verification Report

**Phase Goal:** Establish the EF Core multi-group data model — GroupEntity, UserGroupEntity, GroupRole enum, GroupId FK on shared resources, AddGroupSchema migration with seeding — so that Phase 28 tenant isolation and Phase 29 auth can build on a verified foundation.
**Verified:** 2026-06-30T08:00:00Z
**Status:** human_needed
**Re-verification:** No — initial verification

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | GroupRole enum exists with Player=0, DungeonMaster=1, Admin=2 | VERIFIED | `QuestBoard.Domain/Enums/GroupRole.cs` — exact values confirmed |
| 2 | GroupEntity exists with Id, Name, CreatedAt, UserGroups nav, [Table("Groups")] | VERIFIED | `QuestBoard.Repository/Entities/GroupEntity.cs` — all fields present |
| 3 | UserGroupEntity exists with UserId, GroupId, GroupRole(int), FK navs, [Table("UserGroups")] | VERIFIED | `QuestBoard.Repository/Entities/UserGroupEntity.cs` — int GroupRole confirmed (not enum) |
| 4 | QuestEntity and ShopItemEntity carry non-nullable int GroupId FK + Group nav | VERIFIED | Lines 49-52 in QuestEntity.cs; lines 54-57 in ShopItemEntity.cs |
| 5 | UserEntity has UserGroups navigation collection (no scalar column added) | VERIFIED | `UserEntity.cs` line 18 — ICollection<UserGroupEntity> only |
| 6 | QuestBoardContext registers Groups/UserGroups DbSets with correct indexes and 4 FK delete behaviors | VERIFIED | Lines 33-35 (DbSets); lines 192-227 (OnModelCreating: Name unique, (UserId,GroupId) unique, Quest/ShopItem NoAction, UserGroup→User/Group Cascade) |
| 7 | EntityProfile maps GroupRole int↔enum at AutoMapper boundary | VERIFIED | Lines 124-128 in EntityProfile.cs — (GroupRole)src.GroupRole and (int)src.GroupRole |
| 8 | AddGroupSchema migration exists and is the latest migration | VERIFIED | `QuestBoard.Repository/Migrations/20260630055221_AddGroupSchema.cs` — class AddGroupSchema |
| 9 | Migration Up() covers all 8 FK-safe steps in correct order | VERIFIED | Steps 1-10 in Up(): CreateTable Groups, CreateTable UserGroups, AddColumn Quests.GroupId(default:0), AddColumn ShopItems.GroupId(default:0), IDENTITY_INSERT EuphoriaInn, UPDATE Quests/ShopItems GroupId=1, AddForeignKey Quests NoAction, AddForeignKey ShopItems NoAction, INSERT UserGroups LEFT JOIN + MAX(CASE), DELETE AspNetUserRoles Player/DM/Admin |
| 10 | TestDataHelper.CreateTestQuestAsync and CreateShopItemAsync set GroupId=1 | VERIFIED | Lines 31 and 108 in TestDataHelper.cs |
| 11 | Deployment constraint documented above the AddGroupSchema class declaration | VERIFIED | Lines 8-17 in AddGroupSchema.cs — mentions DungeonMasterHandler, AdminHandler, Phase 29, co-deploy rule |
| 12 | Migration applied cleanly on live SQL Server with all 6 data spot-checks passing (GROUP-04/05/06) | HUMAN NEEDED | Human-verify checkpoint in Plan 03 was completed and approved per 27-03-SUMMARY.md, but this verifier cannot re-confirm SQL Server state programmatically |

**Score:** 11/12 truths verified (11 programmatic + 1 human-needed)

Note: Truth 12 is marked human_needed, not failed — Plan 03 Task 2 was a blocking human checkpoint that the summary records as approved. The requirement is to confirm the live state rather than re-run verification.

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `QuestBoard.Domain/Enums/GroupRole.cs` | GroupRole enum Player=0, DM=1, Admin=2 | VERIFIED | All three values present |
| `QuestBoard.Repository/Entities/GroupEntity.cs` | [Table("Groups")], IEntity, Name/CreatedAt/UserGroups | VERIFIED | All fields and attributes present |
| `QuestBoard.Repository/Entities/UserGroupEntity.cs` | [Table("UserGroups")], int GroupRole, FK navs | VERIFIED | GroupRole stored as int; two ForeignKey nav props |
| `QuestBoard.Domain/Models/Group.cs` | IModel, Id/Name/CreatedAt | VERIFIED | All properties present |
| `QuestBoard.Domain/Models/UserGroup.cs` | IModel, GroupRole enum property | VERIFIED | `GroupRole GroupRole` (enum type, not int) |
| `QuestBoard.Repository/Migrations/20260630055221_AddGroupSchema.cs` | AddGroupSchema migration class | VERIFIED | Full Up()/Down() with all seeding steps |
| `QuestBoard.IntegrationTests/Helpers/TestDataHelper.cs` | GroupId=1 in Quest and ShopItem factories | VERIFIED | Both factory methods updated |
| `QuestBoard.Repository/Migrations/QuestBoardContextModelSnapshot.cs` | Reflects GroupEntity, UserGroupEntity, GroupId | VERIFIED | 17 hits for GroupEntity/UserGroupEntity/GroupId |

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|-----|--------|---------|
| UserGroupEntity | UserEntity | ForeignKey(UserId); UserEntity.UserGroups collects | VERIFIED | `ICollection<UserGroupEntity> UserGroups` on UserEntity; `[ForeignKey(nameof(UserId))]` on UserGroupEntity |
| QuestBoardContext | UserGroupEntity | DbSet<UserGroupEntity> + HasIndex(UserId,GroupId).IsUnique() | VERIFIED | DbSet line 35; HasIndex lines 197-199 |
| QuestBoardContext | GroupEntity | DbSet<GroupEntity> + HasIndex(Name).IsUnique() | VERIFIED | DbSet line 33; HasIndex lines 192-194 |
| EntityProfile | GroupRole enum | (GroupRole)src.GroupRole and (int)src.GroupRole in CreateMap | VERIFIED | Lines 124-128 in EntityProfile.cs |
| AddGroupSchema.Up() | Groups table | IDENTITY_INSERT + INSERT EuphoriaInn Id=1 | VERIFIED | Lines 113-117 in migration |
| AddGroupSchema.Up() | UserGroups seeding | LEFT JOIN AspNetUsers + MAX(CASE) + DELETE AspNetUserRoles | VERIFIED | Lines 150-173 in migration |
| TestDataHelper | QuestEntity.GroupId | CreateTestQuestAsync sets GroupId=1 | VERIFIED | Line 31 in TestDataHelper.cs |
| TestDataHelper | ShopItemEntity.GroupId | CreateShopItemAsync sets GroupId=1 | VERIFIED | Line 108 in TestDataHelper.cs |

### Data-Flow Trace (Level 4)

Not applicable — this phase produces schema/model artifacts and a data migration, not components that render dynamic data.

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| Repository project builds (model layer complete) | dotnet build | Commit a88fe75 + 1444c9e indicate green build; no errors in code scan | PASS (documented) |
| Full test suite (194 tests) passes with GroupId on entities | dotnet test | Plan 02 checkpoint recorded 194 passed, 0 failed | PASS (documented) |
| Migration applies on dev SQL Server with no errors | dotnet run / dotnet ef database update | Plan 03 Task 2 human checkpoint approved | PASS (human-approved) |

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|------------|-------------|--------|----------|
| GROUP-01 | 27-01 | GroupEntity table with Id, Name, CreatedAt | SATISFIED | GroupEntity.cs fully implemented; DbSet registered; unique Name index in QuestBoardContext |
| GROUP-02 | 27-01 | UserGroups junction with UserId, GroupId, GroupRole | SATISFIED | UserGroupEntity.cs with int GroupRole; (UserId,GroupId) unique index; cascade FK delete behaviors |
| GROUP-03 | 27-01 | GroupId FK on QuestEntity and ShopItemEntity | SATISFIED | Non-nullable int GroupId + Group nav on both entities; NoAction delete behavior |
| GROUP-04 | 27-02/27-03 | Data migration seeds EuphoriaInn as GroupId=1 | SATISFIED (human-verified) | Migration IDENTITY_INSERT + UPDATE Quests/ShopItems SET GroupId=1; Plan 03 checkpoint approved |
| GROUP-05 | 27-02/27-03 | All users assigned to EuphoriaInn with current role | SATISFIED (human-verified) | Migration LEFT JOIN + MAX(CASE) INSERT UserGroups; Plan 03 checkpoint confirmed all users have row |
| GROUP-06 | 27-02/27-03 | AspNetUserRoles Player/DM/Admin rows removed | SATISFIED (human-verified) | Migration DELETE WHERE r.Name IN ('Player','DungeonMaster','Admin'); Plan 03 checkpoint confirmed 0 rows |

**Note on REQUIREMENTS.md:** GROUP-04, GROUP-05, and GROUP-06 are marked unchecked (`[ ]`) and show "Pending" in the REQUIREMENTS.md traceability table, despite all three being implemented and human-verified. This is a documentation tracking gap only — the implementation is complete.

**Orphaned requirements:** None. All 6 requirement IDs (GROUP-01 through GROUP-06) appear in plan frontmatter and are accounted for.

### Anti-Patterns Found

No blockers found. All five new files and all five modified files were scanned for TODO/FIXME/placeholder/empty return patterns.

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| — | — | None found | — | — |

The migration contains "best-effort" language in Down() comments (lines 181-184) — this is intentional documentation of a known limitation (multi-role round-trip), not a code stub.

### Human Verification Required

#### 1. Confirm GROUP-04/05/06 live SQL Server state

**Test:** Connect to the local dev SQL Server and run the 6 spot-checks from Plan 03 Task 2:
1. `SELECT Id, Name FROM Groups;` — expect single row: `1, EuphoriaInn`
2. `SELECT COUNT(*) FROM Quests WHERE GroupId <> 1;` — expect 0
3. `SELECT COUNT(*) FROM ShopItems WHERE GroupId <> 1;` — expect 0
4. `SELECT COUNT(*) FROM AspNetUsers u LEFT JOIN UserGroups ug ON ug.UserId = u.Id WHERE ug.Id IS NULL;` — expect 0
5. `SELECT UserId, GroupRole FROM UserGroups ORDER BY UserId;` — confirm each user's GroupRole matches their prior Identity role
6. `SELECT COUNT(*) FROM AspNetUserRoles ur JOIN AspNetRoles r ON r.Id = ur.RoleId WHERE r.Name IN ('Player','DungeonMaster','Admin');` — expect 0

**Expected:** All six checks return the specified values.
**Why human:** The migration seeding SQL runs only on real SQL Server (IDENTITY_INSERT, raw INSERT/UPDATE/DELETE). The Plan 03 summary records these checks as passing, but the verifier cannot confirm live database state programmatically.

#### 2. Update REQUIREMENTS.md traceability for GROUP-04/05/06

**Test:** Open `REQUIREMENTS.md` and verify GROUP-04, GROUP-05, GROUP-06 are marked `[x]` and their traceability rows show "Complete".
**Expected:** Three checkboxes marked complete; traceability table updated.
**Why human:** This is a documentation update. The implementation is verified — only the tracking document needs updating.

### Gaps Summary

No code gaps found. All model artifacts exist, are substantive, and are wired correctly. The migration implements all 8 required FK-safe steps. TestDataHelper is updated. The deployment constraint is documented.

The two human_needed items are:
1. A re-confirmation of the live SQL Server state (Plan 03 checkpoint was already approved — this is confirmatory)
2. A REQUIREMENTS.md documentation update (GROUP-04/05/06 checkboxes not yet marked complete)

Neither blocks Phase 28 from planning or execution at the code level.

---

_Verified: 2026-06-30T08:00:00Z_
_Verifier: Claude (gsd-verifier)_
