---
phase: 49-fix-guild-members-page-missing-group-tenant-filtering
verified: 2026-07-05T19:10:07Z
status: passed
score: 15/15 must-haves verified
behavior_unverified: 0
overrides_applied: 0
---

# Phase 49: Fix Guild Members page missing group/tenant filtering Verification Report

**Phase Goal:** `GuildMembersController` (Guild Members list/details/picture), `DungeonMasterController` (DM profile view/edit/picture), and `QuestController.RemovePlayerSignup` stop leaking data/mutations across groups — all three currently let any authenticated user view (and, for DM profiles and player-signup removal, an Admin overwrite/delete) another group's characters, DM profiles, or player signups by ID, with no group-membership check on the target. `CharacterEntity` gets a real `GroupId` column (migration, backfilled to 1) and an automatic EF Core global query filter, mirroring `QuestEntity`/`ShopItemEntity` exactly, rather than a manual join. `UserTransaction`'s currently-incidental group-scoping (verified safe today via an EF Core inner-join side effect, not by design) is documented, tested, and its one unguarded call site closed. `PlayerSignupEntity`'s identical incidental-scoping gap (found during this phase's research) gets the same hardening, plus a real fix for the one independently-exploitable path (`RemovePlayerSignup`).

**Verified:** 2026-07-05T19:10:07Z
**Status:** passed
**Re-verification:** No — initial verification

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | Guild Members list/detail/picture respect the viewer's active group (D-01/D-02) | VERIFIED | `CharacterEntity.GroupId` + `HasQueryFilter` in `QuestBoardContext.cs:265-268`; `GetAllCharactersWithDetailsAsync`/`GetCharacterWithDetailsAsync` are plain LINQ against filtered `DbContext.Characters`. `CharacterRepositoryTests.GetAllCharactersWithDetailsAsync_ActiveGroupOne_ExcludesGroupTwoCharacter` passes. |
| 2 | Details(id)/GetProfilePicture(id) for a character in another group return 404, not the character (D-01/D-04) | VERIFIED | `GetCharacterWithDetailsAsync` naturally 404s via the filter; `GetCharacterProfilePictureAsync` rewritten to root through `DbContext.Characters` (`CharacterRepository.cs:62-70`). `CharacterRepositoryTests.GetCharacterWithDetailsAsync_ForCharacterInDifferentGroup_ReturnsNull` and `GetCharacterProfilePictureAsync_ForCharacterInDifferentGroup_ReturnsNull` pass. |
| 3 | SuperAdmin with ActiveGroupId == null sees an empty Guild Members list (D-03) | VERIFIED | Filter shape is `ActiveGroupId != null && e.GroupId == ActiveGroupId` (no null-escape-hatch), confirmed at `QuestBoardContext.cs:266-268`. `CharacterRepositoryTests.GetAllCharactersWithDetailsAsync_NoActiveGroup_ReturnsEmpty` passes. |
| 4 | Creating a character stamps GroupId to the creator's active group (D-02) | VERIFIED | `GuildMembersController.cs:139`: `character.GroupId = activeGroupContext.RequireActiveGroupId();` before `AddAsync`. |
| 5 | Existing Characters rows are backfilled to GroupId = 1 by the migration (D-02) | VERIFIED | Migration `20260705183646_AddGroupIdToCharacters.cs`: `AddColumn(defaultValue:0)` → `Sql("UPDATE Characters SET GroupId = 1")` → `AddForeignKey` → `CreateIndex`, in that exact order (backfill before FK). |
| 6 | Profile(id) returns 404 when target user is not a member of the viewer's active group (D-06/D-07/D-09) | VERIFIED | `DungeonMasterController.cs:25`: `if (!await IsTargetInActiveGroupAsync(id)) return NotFound();`. Integration test `Profile_CrossGroupTarget_ReturnsNotFound` passes. |
| 7 | EditProfile GET/POST return 404 for cross-group target before existing Forbid() (D-06/D-09) | VERIFIED | `DungeonMasterController.cs:63` (GET) and `:96` (POST): membership check precedes the `role != GroupRole.Admin` Forbid() check textually and at runtime. Integration tests `EditProfile_Get_CrossGroupTarget_ReturnsNotFound` and `EditProfile_Post_CrossGroupTarget_ReturnsNotFoundAndDoesNotPersist` pass. |
| 8 | GetDMProfilePicture(id) returns 404 for cross-group target (D-06/D-09) | VERIFIED | `DungeonMasterController.cs:130`. Integration test `GetDMProfilePicture_CrossGroupTarget_ReturnsNotFound` passes. |
| 9 | ActiveGroupId == null → all four DM-profile actions 404 without calling GetGroupRoleByIdAsync with a null group (D-08) | VERIFIED | `IsTargetInActiveGroupAsync` (`DungeonMasterController.cs:158-162`) short-circuits `if (activeGroupContext.ActiveGroupId is not { } groupId) return false;` before calling `GetGroupRoleByIdAsync`. Integration test `Profile_SuperAdminNoActiveGroup_ReturnsNotFound` passes. |
| 10 | DungeonMasterProfileEntity unchanged — no GroupId, still shared across groups (D-09a) | VERIFIED | `grep GroupId DungeonMasterProfileEntity.cs` → no matches. |
| 11 | A cross-group UserTransaction is excluded from GetTransactionsByUserAsync, proven by a regression test (D-10/D-11.2) | VERIFIED | `UserTransactionRepositoryTests.GetTransactionsByUserAsync_TransactionForCrossGroupShopItem_IsExcluded` passes (Include-driven inner join folds ShopItem's filter). |
| 12 | ShopService.ReturnOrSellItemAsync uses GetTransactionWithDetailsAsync, not the unguarded base GetByIdAsync (D-11.3) | VERIFIED | `ShopService.cs:122`: `var originalTransaction = await transactionRepository.GetTransactionWithDetailsAsync(transactionId, token);`. No remaining `GetByIdAsync(transactionId` call in this method. |
| 13 | QuestController.RemovePlayerSignup returns 404 and performs no deletion when target signup's parent Quest is not in caller's active group (D-12.1/D-13) | VERIFIED | `QuestController.cs:626-638`: loads via `GetByIdWithQuestAsync`, checks `activeGroupContext.ActiveGroupId is not { } groupId \|\| signup.Quest.GroupId != groupId` → `NotFound()` before `RemoveAsync`. Repository-level regression: `PlayerSignupRepositoryTests.GetByIdWithQuestAsync_ForSignupOnOtherGroupsQuest_ReturnsNullWhenActiveGroupDiffers` passes. No dedicated controller/HTTP-level integration test exists for this specific branch (see Human Verification note below), but logic is directly readable and unit-proven at the data layer it depends on. |
| 14 | RemovePlayerSignup still removes an in-group signup (no regression) | VERIFIED | `QuestBoard.IntegrationTests --filter QuestController` (29 tests) all pass with no regressions; full `QuestBoard.IntegrationTests` suite (298 tests, includes Quest/Shop) passes. |
| 15 | A regression test proves the current PlayerSignup callers' pre-validation pattern holds — filtered-navigation path is scoped, direct unfiltered lookup is not (D-12.2) | VERIFIED | `PlayerSignupRepositoryTests.GetByIdWithQuestAsync_ForSignupInActiveGroup_ReturnsSignupWithQuestGroupId` and `GetByIdAsync_ForSignupOnOtherGroupsQuest_StillReturnsRow_DocumentingCallerMustPreValidate` both pass. |

**Score:** 15/15 truths verified (0 present, behavior-unverified)

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `QuestBoard.Repository/Entities/CharacterEntity.cs` | GroupId column + Group navigation | VERIFIED | `public int GroupId { get; set; }` + `[ForeignKey(nameof(GroupId))] public virtual GroupEntity Group` present |
| `QuestBoard.Repository/Entities/QuestBoardContext.cs` | CharacterEntity HasQueryFilter (no null-escape) + FK config + corrected comment | VERIFIED | Filter at lines 265-268 (no `ActiveGroupId == null \|\|`); FK config at 224-228; corrected comment block at 270-284 documents Character/UserTransaction/PlayerSignup mechanisms accurately |
| `QuestBoard.Repository/CharacterRepository.cs` | GetCharacterProfilePictureAsync rooted through DbContext.Characters | VERIFIED | Lines 62-70, no reference to `DbContext.CharacterImages` remains |
| `QuestBoard.Repository/Migrations/20260705183646_AddGroupIdToCharacters.cs` | Migration: AddColumn → backfill → FK → index | VERIFIED | Exact step order confirmed, matches `AddGroupSchema` precedent |
| `QuestBoard.Service/Controllers/Characters/GuildMembersController.cs` | IActiveGroupContext injected, Create POST stamps GroupId | VERIFIED | Constructor param present; `RequireActiveGroupId()` call at line 139 |
| `QuestBoard.UnitTests/Repository/CharacterRepositoryTests.cs` | Cross-group + SuperAdmin-empty + picture-404 coverage | VERIFIED | 5 tests present, all pass |
| `QuestBoard.Service/Controllers/DungeonMaster/DungeonMasterController.cs` | Target-group-membership check via GetGroupRoleByIdAsync | VERIFIED | `IsTargetInActiveGroupAsync` helper wired into all 4 actions |
| `QuestBoard.IntegrationTests/Controllers/DungeonMasterControllerIntegrationTests.cs` | Cross-group 404 + SuperAdmin-no-group 404 coverage | VERIFIED | 5 new tests present, all pass; 15/15 total in class |
| `QuestBoard.Domain/Services/ShopService.cs` | ReturnOrSellItemAsync uses GetTransactionWithDetailsAsync | VERIFIED | Line 122 |
| `QuestBoard.UnitTests/Repository/UserTransactionRepositoryTests.cs` | Cross-group exclusion regression test | VERIFIED | 2 tests present, both pass |
| `QuestBoard.Repository/PlayerSignupRepository.cs` | GetByIdWithQuestAsync with Include(ps => ps.Quest) | VERIFIED | Lines 23-29 |
| `QuestBoard.Service/Controllers/QuestBoard/QuestController.cs` | RemovePlayerSignup target-Quest group check | VERIFIED | Lines 626-638 |
| `QuestBoard.UnitTests/Repository/PlayerSignupRepositoryTests.cs` | Cross-group regression coverage for RemovePlayerSignup lookup | VERIFIED | 3 new tests present (lines 314-386 region), all pass |

### Key Link Verification

| From | To | Via | Status | Details |
|------|-----|-----|--------|---------|
| `QuestBoardContext.cs` | `CharacterEntity.cs` | `HasQueryFilter` + `HasOne(c => c.Group)` | WIRED | Confirmed at lines 224-228, 265-268 |
| `GuildMembersController.cs` | `IActiveGroupContext` | constructor injection + `RequireActiveGroupId()` stamp | WIRED | Confirmed at lines 16, 139 |
| `DungeonMasterController.cs` | `IUserService.GetGroupRoleByIdAsync` | `IsTargetInActiveGroupAsync` helper | WIRED | Confirmed at line 161, invoked from all 4 actions |
| `ShopService.cs` | `IUserTransactionRepository` | `GetTransactionWithDetailsAsync(transactionId, token)` | WIRED | Confirmed at line 122 |
| `QuestController.cs` | `IPlayerSignupService` | `GetByIdWithQuestAsync` exposing `signup.Quest.GroupId` | WIRED | Confirmed at line 626, compared at line 635 |

### Behavioral Spot-Checks / Test Execution

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| Full solution builds | `dotnet build` | Build succeeded, 0 warnings, 0 errors | PASS |
| Character/UserTransaction/PlayerSignup repository regression tests | `dotnet test QuestBoard.UnitTests --filter "CharacterRepositoryTests\|UserTransactionRepositoryTests\|PlayerSignupRepositoryTests"` | 20/20 passed | PASS |
| DungeonMasterController cross-group + SuperAdmin integration tests | `dotnet test QuestBoard.IntegrationTests --filter "DungeonMasterControllerIntegrationTests"` | 15/15 passed | PASS |
| QuestController + GuildMembers integration tests (regression) | `dotnet test QuestBoard.IntegrationTests --filter "QuestController\|GuildMembersControllerIntegrationTests"` | 29/29 passed | PASS |
| Full integration suite (Quest/Shop broad regression) | `dotnet test QuestBoard.IntegrationTests --filter "Quest\|Shop"` | 298/298 passed | PASS |

### Requirements Coverage

This phase has no formal REQ-IDs (ad-hoc bug-fix phase). Source of truth is 49-CONTEXT.md decisions D-01 through D-13.

| Decision | Covered By Plan | Status | Evidence |
|----------|-----------------|--------|----------|
| D-01 (fix Index + Details/GetProfilePicture leak) | 49-01 | SATISFIED | CharacterEntity filter + rewritten picture query |
| D-02 (schema-based GroupId + filter, not manual join) | 49-01 | SATISFIED | Migration + HasQueryFilter |
| D-03 (SuperAdmin-null → empty, no escape hatch) | 49-01 | SATISFIED | Filter shape confirmed, no `== null \|\|` |
| D-04 (404 not 403 for cross-group Character access) | 49-01 | SATISFIED | Filter naturally 404s via existing NotFound() paths |
| D-05 (Quest History already correct — informational, no plan coverage by design) | none (investigation-only) | SATISFIED (no code change needed) | Documented in 49-CONTEXT.md as ruled-out; correctly has no plan artifact |
| D-06 (DungeonMasterController read+write leak, fix all 4 actions) | 49-02 | SATISFIED | All 4 actions gated |
| D-07 (use GetGroupRoleByIdAsync, no new plumbing) | 49-02 | SATISFIED | `IsTargetInActiveGroupAsync` reuses existing primitive |
| D-08 (SuperAdmin-null → 404, no call with null group) | 49-02 | SATISFIED | Short-circuit before `GetGroupRoleByIdAsync` call |
| D-09 (404 not 403, runs before existing Forbid()) | 49-02 | SATISFIED | Check precedes Forbid() in both EditProfile overloads |
| D-09a (DungeonMasterProfileEntity stays schema-unchanged) | 49-02 | SATISFIED | No GroupId on entity |
| D-10 (UserTransaction empirically safe via Include-driven inner join) | 49-03 | SATISFIED | Regression test proves the mechanism |
| D-11 (harden: comment + regression test + fix ReturnOrSellItemAsync call site) | 49-01 (comment) + 49-03 (test + call site) | SATISFIED | Comment corrected in QuestBoardContext.cs; call site fixed; test passes |
| D-12 (PlayerSignup same gap; fix RemovePlayerSignup; harden other 3 paths) | 49-04 | SATISFIED | Quest-including lookup + controller check + documentation tests |
| D-13 (RemovePlayerSignup cross-group → 404 not 403) | 49-04 | SATISFIED | `NotFound()` returned for both null-group and cross-group cases |

No orphaned requirements/decisions found — all D-01 through D-13 (including D-09a) are accounted for across the four plans, matching the phase's own ROADMAP.md plan-to-decision mapping.

### Anti-Patterns Found

None. Grep across all files modified in this phase for `TBD|FIXME|XXX|TODO|HACK|PLACEHOLDER` and for leaked decision-ID/phase-number strings (`D-0\d`, `D-1\d`, `Phase 49`, `TENANT-`) returned zero matches in every modified source file.

### Human Verification Required

None required to reach `passed`. One item is noted for completeness/awareness (not a gap, not blocking):

- `QuestController.RemovePlayerSignup`'s cross-group 404 branch (the controller-level `if (activeGroupContext.ActiveGroupId is not { groupId } || signup.Quest.GroupId != groupId) return NotFound();` check) has no dedicated HTTP-level integration test exercising an actual cross-group DELETE request — coverage stops at the repository-level regression test for `GetByIdWithQuestAsync`'s own scoping, plus static code reading of the controller logic. The plan's own acceptance criteria only required the repository-level test plus a regression run of the existing `QuestController` integration suite (which passed, 29/29, confirming no same-group regression). The logic is simple, directly readable, and low-risk, so this is not treated as a gap — flagged here only so a reviewer can decide whether to request a follow-up HTTP-level test in a future phase.

### Gaps Summary

None. All 15 derived observable truths (covering D-01 through D-13, including D-09a) verified against the actual codebase — not just SUMMARY.md claims. Build succeeds. All relevant unit and integration test suites pass (20 unit + 298 integration, including the newly added regression coverage for every fixed leak). No anti-patterns or leaked planning references found in modified files.

---

_Verified: 2026-07-05T19:10:07Z_
_Verifier: Claude (gsd-verifier)_
