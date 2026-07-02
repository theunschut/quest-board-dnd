---
phase: 27-group-schema-foundation
fixed_at: 2026-06-30T10:30:00Z
review_path: .planning/phases/27-group-schema-foundation/27-REVIEW.md
iteration: 1
findings_in_scope: 3
fixed: 3
skipped: 0
status: all_fixed
---

# Phase 27: Code Review Fix Report

**Fixed at:** 2026-06-30T10:30:00Z
**Source review:** .planning/phases/27-group-schema-foundation/27-REVIEW.md
**Iteration:** 1

**Summary:**
- Findings in scope: 3 (WR-01, WR-02, WR-03)
- Fixed: 3
- Skipped: 0

## Fixed Issues

### WR-01: `GroupId` missing from `Quest` and `ShopItem` domain models

**Files modified:** `QuestBoard.Domain/Models/QuestBoard/Quest.cs`, `QuestBoard.Domain/Models/Shop/ShopItem.cs`
**Commit:** c1c481c
**Applied fix:** Added `public int GroupId { get; set; }` to `Quest` (after `DungeonMasterId`) and to `ShopItem` (before `CreatedByDmId`). AutoMapper now maps `GroupId` correctly in both entity-to-model and model-to-entity directions, preventing the silent zero-overwrite of the FK column on quest/shop-item update paths.

### WR-02: `UserGroupEntity.GroupRole` stored as `int` — no range validation

**Files modified:** `QuestBoard.Repository/Entities/UserGroupEntity.cs`
**Commit:** be32f31
**Applied fix:** Added `[Range(0, 2, ErrorMessage = "GroupRole must be a valid GroupRole enum value (0=Player, 1=DungeonMaster, 2=Admin).")]` attribute on the `GroupRole` property, matching the defined `GroupRole` enum values. Prevents out-of-range integers from silently passing through to the authorization logic planned for Phase 29.

### WR-03: `TestDataHelper.ClearDatabaseAsync` does not re-seed the EuphoriaInn group row

**Files modified:** `QuestBoard.IntegrationTests/Helpers/TestDataHelper.cs`
**Commit:** e737e8f
**Applied fix:** Added a `SeedDefaultGroupAsync` static method that inserts `GroupEntity { Id = 1, Name = "EuphoriaInn" }` if not already present, and called it from `ClearDatabaseAsync` after `SeedRolesAsync`. The existence guard (`!context.Groups.Any(g => g.Id == 1)`) makes the helper idempotent. This ensures tests that call `CreateTestQuestAsync` or `CreateShopItemAsync` after `ClearDatabaseAsync` will not fail with an FK constraint violation on `GroupId = 1`.

---

_Fixed: 2026-06-30T10:30:00Z_
_Fixer: Claude (gsd-code-fixer)_
_Iteration: 1_
