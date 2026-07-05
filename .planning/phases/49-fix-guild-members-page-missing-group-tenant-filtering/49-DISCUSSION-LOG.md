# Phase 49: Fix Guild Members page missing group/tenant filtering - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-07-05
**Phase:** 49-fix-guild-members-page-missing-group-tenant-filtering
**Areas discussed:** Fix scope, SuperAdmin case, Response code

---

## Fix scope

| Option | Description | Selected |
|--------|-------------|----------|
| Fix list + close direct-URL leak | Same root cause (missing group filter) on the same controller — treat it as one bug. Details/GetProfilePicture get a group check too. | ✓ |
| Fix Index list only | Narrowly match what was reported. Leave Details/GetProfilePicture as-is; file the direct-URL leak as a separate follow-up bug. | |

**User's choice:** Fix list + close direct-URL leak (recommended option)
**Notes:** Discovered during code inspection — `Details(id)` and `GetProfilePicture(id)` have no ownership/group check at all, unlike `Edit`/`Delete` in the same controller. User agreed this is the same defect, not separate scope.

---

## SuperAdmin case

| Option | Description | Selected |
|--------|-------------|----------|
| Empty list, matches Players page | Consistent with the existing GetAllDungeonMasters/GetAllPlayers behavior when ActiveGroupId is null — "no guild selected" rather than a special superview. | ✓ |
| Show all characters across all groups | Give SuperAdmin a cross-group superview here, distinct from the regular empty-list-when-unscoped pattern. | |

**User's choice:** Empty list, matches Players page (recommended option)
**Notes:** SuperAdmin bypasses GroupSessionMiddleware's active-group requirement, so it's the only role that can reach this controller with `ActiveGroupId == null`.

---

## Response code

| Option | Description | Selected |
|--------|-------------|----------|
| 404 Not Found | Hides the character's existence entirely — the typical multi-tenant-isolation convention. | ✓ |
| 403 Forbidden | Matches the existing pattern in this same controller's Edit/Delete actions, which Forbid() non-owners. | |

**User's choice:** 404 Not Found (recommended option)
**Notes:** Only relevant given the "close the leak" scope decision above — applies to cross-group access on Details/GetProfilePicture.

---

## Claude's Discretion

- Exact implementation shape for the group check on Details/GetProfilePicture (repository join vs. fetch-then-check-then-404) — follow whichever is more consistent with existing `ICharacterRepository`/`ICharacterService` method shapes.
- Whether "MyCharacters" needs special-case handling — it doesn't, since it's derived from the already-filtered list.

---

## DungeonMasterProfile Quest History (raised mid-discussion)

User asked whether the "Quest History" list on the DungeonMasterProfile view should also be filtered on the active group ID, given this phase's theme of closing tenant-filtering leaks.

**Investigation:** `GetQuestsByDungeonMasterAsync` queries `DbContext.Quests` directly with no `.IgnoreQueryFilters()`, so it already inherits the automatic global EF Core query filter on `QuestEntity` (`ActiveGroupId == null || GroupId == ActiveGroupId`). For every non-SuperAdmin viewer, Quest History is already correctly scoped. SuperAdmin sees all groups' quests for that DM by the filter's own "null = see all" design — consistent with how the rest of the app treats SuperAdmin elsewhere (e.g. `/quests`).

| Option | Description | Selected |
|--------|-------------|----------|
| Lock down profile-by-ID access | GetByIdAsync/GetProfileByUserIdAsync/GetDMProfilePicture have no group check — add the same group-membership + 404 pattern as Characters. | |
| Also make SuperAdmin's Quest History empty when unscoped | Override the filter's "null = see all" default to match the Guild Members empty-list convention (D-03). | |
| Nothing further — Quest History itself is already correct | Confirms the code reading is right; no additional work needed on the Quest History list. | ✓ |

**User's choice:** Nothing further — confirmed as a non-issue once the existing global query filter was explained (Quest History specifically).
**Notes:** The adjacent profile-by-ID leak (bio/name/profile picture, not quest data) was surfaced during this investigation but initially not selected — see follow-up below where the user reconsidered after a clarifying summary.

---

## DungeonMasterController profile-by-ID leak (reconsidered)

The initial summary of the investigation above conflated two things ("Quest History is fine" vs. "the surrounding page isn't") in a way that read as contradictory. Clarified: Quest History is fine; the rest of the DM Profile page (name/bio/picture) is a separate, real leak. Further investigation while restating this also surfaced that `EditProfile` (GET+POST) never checks the *target* user is in the current Admin's active group — an Admin in Group A can overwrite the bio/profile picture of a DM in unrelated Group Z. This is a write-path issue, more severe than the read-only leak on `Profile`/`GetDMProfilePicture`.

**Scope question:**

| Option | Description | Selected |
|--------|-------------|----------|
| Fix all three, including EditProfile's write leak | Add the group-membership check to Profile, EditProfile (GET+POST), and GetDMProfilePicture. | ✓ |
| Read-only leaks only (Profile, GetDMProfilePicture) | Leave EditProfile as-is; track its write-path issue as a separate, higher-priority follow-up bug. | |

**User's choice:** Fix all three (recommended option).

**SuperAdmin-with-no-active-group question:**

| Option | Description | Selected |
|--------|-------------|----------|
| Treat as inaccessible — 404/empty | Consistent with the Guild Members convention (D-03) — without a selected group, treat the target as not found. | ✓ |
| Allow SuperAdmin to view/edit any DM profile | Let SuperAdmin bypass group scoping here too, as a platform-admin utility. | |

**User's choice:** Treat as inaccessible — 404/empty (recommended option).

**Notes:** Recorded as D-06 through D-09 in CONTEXT.md. Reuses the existing `IUserService.GetGroupRoleByIdAsync(userId, groupId)` primitive (returns null for non-members) — no new repository plumbing needed, simpler than the Characters fix.

---

## UserTransaction group-scoping (raised mid-discussion)

User asked whether `UserTransaction` needs its own `GroupId`, since it's linked to `ShopItem` (which has `GroupId` + an automatic global query filter) — reasoning this might already be automatically safe.

**Investigation (empirical, not just reasoning):** Wrote and ran two throwaway xUnit tests against an EF Core InMemory `QuestBoardContext` (deleted after verification, not committed):
1. `ctx.UserTransactions.Include(t => t.ShopItem).Where(t => t.UserId == X)` for a transaction whose ShopItem is in a different group than `ActiveGroupId` → 0 rows returned.
2. Identical query without `.Include(t => t.ShopItem)` → 1 row returned (the leaked row).

Conclusion: `UserTransactionEntity.ShopItemId` is a required FK, so `.Include(t => t.ShopItem)` becomes an inner join in EF Core's translation, and `ShopItemEntity`'s query filter gets folded into that join — dropping the parent row for out-of-group items. All 5 `UserTransactionRepository` methods already include `ShopItem`, so **this is not a live bug today**. But the protection is undocumented/incidental — nothing enforces "every query must Include ShopItem" — and one call site (`ShopService.ReturnOrSellItemAsync`'s use of the base `GetByIdAsync(transactionId)`) already doesn't have it (safe today only by the accident of a subsequent ShopItem re-fetch throwing first).

Also found during this check: the `QuestBoardContext.cs:257-259` comment claiming CharacterEntity is "reached only through an already-filtered navigation" is now stale/wrong once D-02 ships (Characters get an explicit repository-level filter instead).

| Option | Description | Selected |
|--------|-------------|----------|
| Add to Phase 49 scope | Make the group-scoping explicit/documented (comment fix + regression test) and fix the one unguarded GetByIdAsync(transactionId) call in ReturnOrSellItemAsync. | ✓ |
| Log as a separate backlog item | Nothing is broken today; track as hardening for a future phase. | |
| Leave as-is | Works correctly today; skip formalizing it. | |

**User's choice:** Add to Phase 49 scope (recommended option).
**Notes:** Recorded as D-10/D-11 in CONTEXT.md. `UserTransactionEntity` itself gets no new GroupId column or migration — this is a documentation + test + one-call-site fix for that entity specifically. (A migration *does* end up happening elsewhere in this phase — see below — but for `CharacterEntity`, not `UserTransactionEntity`.)

---

## Migration mechanism revision — GroupId=1 default, schema change for CharacterEntity (raised mid-discussion)

User volunteered the migration backfill value directly: "the default groupId to use is 1. All items in the database currently are for that group."

This prompted re-examining D-02's original plan (manual `UserGroups` join, no schema change) — since the user was clearly expecting an actual migration to exist, the plan was reopened.

**Verified against the original `20260630055221_AddGroupSchema.cs` migration:** its own comment confirms GroupId=1 is literally "EuphoriaInn," the original/only group before multi-tenancy — matching the user's statement exactly, and giving a concrete precedent for the migration structure (add column with temp default → `UPDATE ... SET GroupId = 1` → add FK constraint after backfill → add index).

| Option | Description | Selected |
|--------|-------------|----------|
| Add GroupId column to CharacterEntity | Switch D-02 to the Quest/ShopItem pattern: direct column + automatic global query filter, migration backfills to GroupId=1. | ✓ |
| Also add GroupId to DungeonMasterProfileEntity | Apply the same treatment to DM profiles. | ✓ (initially — see below) |
| No, different table | — | |

**Follow-up: DungeonMasterProfileEntity wrinkle.** Checked `DungeonMasterProfileEntity`'s actual structure before implementing the "also" option — found `Id = UserId` (not auto-generated), a strict one-row-per-user table, unlike `CharacterEntity` which has its own auto-generated `Id`. Adding `GroupId` there would silently make a DM's shared bio/photo group-specific: a DM in two groups would only see their profile from whichever group got backfilled, nothing from the other. Flagged this before proceeding.

| Option | Description | Selected |
|--------|-------------|----------|
| Keep DM Profile shared, no GroupId | Revert to original D-07 plan: profile stays one-per-user, access still gated by GetGroupRoleByIdAsync. No migration, no behavior change. | ✓ |
| Make DM Profile per-group | Restructure to one profile per (UserId, GroupId) — bigger change, changes primary key structure. | |

**User's choice:** Keep DM Profile shared, no GroupId (recommended option). Recorded as **D-09a**.

**Follow-up: SuperAdmin null-ActiveGroupId filter shape.** Adopting Quest/ShopItem's exact filter pattern would also adopt their "null = see all" SuperAdmin behavior, silently reversing D-03 (which chose "empty for SuperAdmin"). Flagged the conflict.

User asked a clarifying question first: "when I as a super admin log in and select a group, my activeGroupId is set just like a player... how is it ever null?" — verified via `ActiveGroupContextService.cs` (reads from `HttpContext.Session`) that it's only null in the narrow window right after login, before any group has been picked in that session (SuperAdmin bypasses the forced-redirect-to-picker that every other role goes through) — not a routine workflow.

| Option | Description | Selected |
|--------|-------------|----------|
| Empty for SuperAdmin in that window | Keep D-03: Character's filter omits the `ActiveGroupId == null ||` escape hatch Quest/ShopItem have. | ✓ |
| Match Quest/ShopItem: see all | Reverse D-03 for consistency with the rest of the app. | |

**User's choice:** Empty for SuperAdmin (recommended option, D-03 kept, reimplemented as part of the filter shape itself).

**Net effect on the phase:** D-02, D-03, and D-04 (Details/GetProfilePicture — the latter needed its own fix since `CharacterImageEntity` is a shared-PK sibling not covered by `CharacterEntity`'s filter, same lesson as D-10) were all revised in CONTEXT.md. New: D-09a (DM Profile stays unchanged, considered and rejected).

---

## PlayerSignupEntity claim — recorded as an Unknown, not a spawned task

User asked to record the still-unverified `PlayerSignupEntity` claim (same comment block, same "reached only through Quest navigation" assertion that turned out false for CharacterEntity) directly in CONTEXT.md as an open question, rather than as a separately spawned background session — so `/gsd-plan-phase 49`'s research step can pick it up naturally. The previously-spawned background task chip for this was withdrawn/dismissed accordingly. Recorded under a new `<unknowns>` section in CONTEXT.md, explicitly out of scope for Phase 49's own execution.

## Deferred Ideas

None — the DungeonMasterController leak and the UserTransaction hardening were both pulled into this phase's scope after discussion, not deferred. The PlayerSignupEntity question is recorded as an Unknown for research, not deferred.
