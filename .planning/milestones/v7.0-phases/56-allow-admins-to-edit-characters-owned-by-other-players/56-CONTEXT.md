# Phase 56: Allow admins to edit characters owned by other players - Context

**Gathered:** 2026-07-06
**Status:** Ready for planning

<domain>
## Phase Boundary

Admins (and SuperAdmins) can act on characters owned by other players in `GuildMembersController` — Edit, Delete, and ToggleRetirement — without being blocked by the current owner-only check. Players retain full existing access to their own characters. No changes to character creation, guild membership management, or other unrelated controllers.

</domain>

<decisions>
## Implementation Decisions

### Scope of admin bypass
- **D-01:** The admin bypass applies to all three currently owner-gated actions in `GuildMembersController`: `Edit`, `Delete`, and `ToggleRetirement` — not just `Edit`. Rationale: these three actions share the same `character.OwnerId != currentUser.Id` gate today (confirmed in RESEARCH.md at `GuildMembersController.cs:155-159, 183-186, 263-267, 284-288`); leaving Delete/Retire owner-only while Edit is admin-accessible would be an inconsistent admin experience.

### Roles granted the bypass
- **D-02:** Only `Admin` and `SuperAdmin` get the bypass — mirror `DungeonMasterController.EditProfile`'s existing pattern exactly (via `IUserService.GetEffectiveGroupRoleAsync(User, groupId)`, built in Phase 34.3). `DungeonMaster` role is explicitly OUT of scope for this phase — do not extend the bypass to DMs.

### Cross-tenant safety
- **D-03:** No new tenant/group guard needs to be hand-rolled. `CharacterEntity`'s EF Core `HasQueryFilter` (hardened in Phase 49, and Phase 55's cross-tenant fix is fully shipped/merged) is fail-closed even for SuperAdmin with no active group — a cross-tenant character ID lookup returns `null`, which the controller already turns into `NotFound()`. Do not add a parallel `IsTargetInActiveGroupAsync`-style helper (that pattern exists only because `UserEntity` has no query filter — it doesn't apply here).

### View/ViewModel naming
- **D-04:** Add a `CanEdit` boolean to the relevant character ViewModel (mirroring `DMProfileViewModel.CanEdit`) to drive admin-vs-owner UI differences, rather than repurposing the existing `IsOwner` flag for this purpose.

### Claude's Discretion
- Whether Delete/ToggleRetirement need their own `Can*` ViewModel flags or can share `CanEdit`/derive from role checks directly in the view.
- Exact test method names and structure for the six new integration tests, so long as they cover: Admin success, SuperAdmin success, Player-denied regression, cross-tenant 404, owner-still-works regression, and Details-page content assertion (per RESEARCH.md recommendation, mirroring `AdminHandlerIntegrationTests.cs` style).

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Analog pattern (proven, shipped)
- `.planning/phases/56-allow-admins-to-edit-characters-owned-by-other-players/56-RESEARCH.md` — Full research findings: exact file/line locations of the current owner-only checks in `GuildMembersController`, the `DungeonMasterController.EditProfile` analog to mirror, `IUserService.GetEffectiveGroupRoleAsync` usage, and the fail-closed query filter behavior confirmation.

No external specs/ADRs beyond RESEARCH.md — this is an ad-hoc backlog phase (no REQUIREMENTS.md mapping), same pattern as Phases 47-51 and 55.

</canonical_refs>

<specifics>
## Specific Ideas

- User's own words: "I need the ability as an admin (and thus superadmin) to be able to edit a character from another player."
- Follow the `DungeonMasterController.EditProfile` pattern exactly — it's a proven, shipped analog solving the identical "ownership OR Admin/SuperAdmin" problem in this same codebase.

</specifics>

<deferred>
## Deferred Ideas

- Extending the bypass to `DungeonMaster` role — explicitly out of scope per D-02, not deferred to a specific future phase, just not part of this request.

</deferred>

---

*Phase: 56-allow-admins-to-edit-characters-owned-by-other-players*
*Context gathered: 2026-07-06*
