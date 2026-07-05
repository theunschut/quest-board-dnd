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

**User's choice:** Nothing further — confirmed as a non-issue once the existing global query filter was explained.
**Notes:** The adjacent profile-by-ID leak (bio/name/profile picture, not quest data) was surfaced but not selected for this phase's scope — recorded as a Deferred Idea in CONTEXT.md rather than silently dropped.

## Deferred Ideas

- `DungeonMasterController.Profile`/`GetDMProfilePicture` have no group check on which user ID can be viewed (bio/name/profile picture only — Quest History itself is unaffected, see above). Same class of leak as the Characters Details/GetProfilePicture fix in this phase, but not selected for this phase's scope.
