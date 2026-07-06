---
phase: 56-allow-admins-to-edit-characters-owned-by-other-players
verified: 2026-07-06T19:00:00Z
status: passed
score: 11/11 must-haves verified
behavior_unverified: 0
overrides_applied: 0
---

# Phase 56: Allow Admins to Edit Characters Owned by Other Players Verification Report

**Phase Goal:** An Admin (per-group `GroupRole.Admin`) or SuperAdmin can Edit, Delete, and Retire/Reactivate a character owned by another player in their active group, while Players stay restricted to their own characters and cross-tenant access remains blocked — mirroring the shipped `DungeonMasterController.EditProfile` ownership-OR-admin pattern, with no schema change or new packages.
**Verified:** 2026-07-06T19:00:00Z
**Status:** passed
**Re-verification:** No — initial verification

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | An Admin can open the Edit page for a character owned by another player in the same group (HTTP 200, not 403) | VERIFIED | `Edit_AdminEditingAnotherPlayersCharacter_ShouldSucceed` passes (test run: 16/16 GuildMembers tests green). `GuildMembersController.cs:180` guards via `CanManageCharacterAsync`, returns 200 for admin. |
| 2 | A SuperAdmin (with an active group selected) can open the Edit page for another player's character (HTTP 200, not 403) | VERIFIED | `Edit_SuperAdminEditingAnotherPlayersCharacter_ShouldSucceed` passes. `GetEffectiveGroupRoleAsync` resolves SuperAdmin to `GroupRole.Admin` internally (no re-derived short-circuit in controller — grep confirmed zero `IsInRole("SuperAdmin")` matches). |
| 3 | An Admin can POST Delete and POST ToggleRetirement on another player's character in the same group (redirect, not 403) | VERIFIED | `Delete_AdminDeletingAnotherPlayersCharacter_ShouldSucceed` and `ToggleRetirement_AdminTogglingAnotherPlayersCharacter_ShouldSucceed` both pass; assertions check `Location` header excludes `AccessDenied` AND verify actual DB-state change (deleted row / flipped status) — distinguishing true success from a Forbid-redirect false positive. |
| 4 | A plain Player still cannot Edit, Delete, or ToggleRetirement another player's character (HTTP 403) | VERIFIED | `Edit_PlayerEditingAnotherPlayersCharacter_ShouldBeForbidden`, `Delete_PlayerDeletingAnotherPlayersCharacter_ShouldBeForbidden`, `ToggleRetirement_PlayerTogglingAnotherPlayersCharacter_ShouldBeForbidden` all pass (`BeOneOf(Forbidden, Redirect, Unauthorized)` — matches this test harness's documented cookie-scheme `Forbid()` behavior, consistent with sibling suites `DungeonMasterControllerIntegrationTests`/`QuestControllerAuthorizationRegressionTests`). |
| 5 | An Admin in Group A cannot reach Edit/Delete/ToggleRetirement for a character in Group B — the response is 404, not 403 | VERIFIED | `Edit_AdminEditingCharacterInDifferentGroup_ShouldReturnNotFound` and `Delete_AdminDeletingCharacterInDifferentGroup_ShouldReturnNotFound` pass, asserting `HttpStatusCode.NotFound`. No hand-rolled `character.GroupId ==` / `ActiveGroupId ==` guard found in controller (grep: zero matches) — relies solely on `CharacterEntity`'s existing fail-closed EF Core query filter (D-03 honored). |
| 6 | The character owner can still Edit their own character (regression preserved) | VERIFIED | `Edit_OwnerEditingOwnCharacter_ShouldSucceed` passes; `CanManageCharacterAsync` short-circuits true on `character.OwnerId == currentUser.Id` before any role lookup. |
| 7 | The Details page renders the Edit/Retire/Delete Actions card to an Admin viewing another player's character (CanEdit=true), and still renders it to the owner | VERIFIED | `Details_AdminViewingAnotherPlayersCharacter_ShowsEditButton` passes, asserting response body contains "Edit Character". `Details.cshtml:59` and `Details.Mobile.cshtml:97` both gate the Actions card on `@if (canEdit)` where `canEdit = Model.CanEdit`; card markup unchanged (diff confirmed as a 1-line gate-variable swap). |
| 8 | D-01: the Admin/SuperAdmin bypass applies to all three actions (Edit, Delete, ToggleRetirement), not just Edit | VERIFIED | `GuildMembersController.cs` — `CanManageCharacterAsync` guard applied identically at Edit(GET) line 180, Edit(POST) line 216, Delete line 307, ToggleRetirement line 336. All four call the same shared helper (post-WR-02 refactor). |
| 9 | D-02: only Admin and SuperAdmin get the bypass — GroupRole.DungeonMaster remains excluded | VERIFIED | `CanManageCharacterAsync` (line 374-383) checks `role == GroupRole.Admin` only; `GetEffectiveGroupRoleAsync` resolves SuperAdmin to Admin but leaves DungeonMaster/Player unresolved to Admin. No test or code path grants DM the bypass. |
| 10 | D-03: no hand-rolled cross-tenant guard is added — cross-group access relies entirely on CharacterEntity's existing fail-closed EF Core query filter | VERIFIED | Grep of `GuildMembersController.cs` for `character.GroupId ==` / `ActiveGroupId ==` manual comparisons: zero matches. Cross-group 404s are produced by `GetCharacterWithDetailsAsync` returning `null` via the query filter, then the existing `if (character == null) return NotFound();` check. |
| 11 | D-04: a new CanEdit boolean is added to CharacterViewModel, distinct from and not repurposing IsOwner | VERIFIED | `CharacterViewModel.cs:43,45` — both `public bool IsOwner { get; set; }` and `public bool CanEdit { get; set; }` present as independent properties. `IsOwner` assignments in controller now correctly compute `character.OwnerId == currentUser.Id` (no unconditional `= true`; grep confirmed zero matches). |

**Score:** 11/11 truths verified (0 present, behavior-unverified)

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `QuestBoard.Service/ViewModels/CharacterViewModels/CharacterViewModel.cs` | CanEdit boolean flag, distinct from IsOwner | VERIFIED | Lines 43/45: both properties present and independent. |
| `QuestBoard.Service/Controllers/Characters/GuildMembersController.cs` | Ownership-OR-Admin guard on Edit(GET/POST), Delete, ToggleRetirement; CanEdit computed in Details | VERIFIED | All four guard sites use shared `CanManageCharacterAsync` helper (post-review-fix refactor); Details computes `CanEdit = isOwner \|\| role == GroupRole.Admin` at line 75. |
| `QuestBoard.Service/Views/GuildMembers/Details.cshtml` | Actions card gated on CanEdit instead of IsOwner | VERIFIED | Line 6: `var canEdit = Model.CanEdit;`; line 59: `@if (canEdit)`. |
| `QuestBoard.Service/Views/GuildMembers/Details.Mobile.cshtml` | Owner Actions card gated on CanEdit instead of IsOwner | VERIFIED | Line 6: `var canEdit = Model.CanEdit;`; line 97: `@if (canEdit)`. |
| `QuestBoard.IntegrationTests/Controllers/GuildMembersControllerIntegrationTests.cs` | 11 authorization integration tests covering Edit/Delete/ToggleRetirement/cross-tenant/owner/Details | VERIFIED (exceeded) | 12 new tests present (11 planned + `Edit_AdminEditingAnotherPlayersCharacterSetAsMain_ShouldPersistChangesAndPromoteCorrectOwner`, added post-review to close CR-01). All pass. |

### Key Link Verification

| From | To | Via | Status | Details |
|------|-----|-----|--------|---------|
| `GuildMembersController.Edit/Delete/ToggleRetirement` | `IUserService.GetEffectiveGroupRoleAsync` | effective-role resolution for Admin bypass, via shared `CanManageCharacterAsync` helper | WIRED | Called once per guard invocation, consistent across all four action methods. |
| `GuildMembersController.Details` | `CharacterViewModel.CanEdit` | controller-computed admin-or-owner flag drives the view Actions card | WIRED | `viewModel.CanEdit = isOwner \|\| role == GroupRole.Admin;` set before `return View(viewModel);`. |
| `Details.cshtml`/`Details.Mobile.cshtml` Actions card | `CharacterViewModel.CanEdit` | Razor conditional | WIRED | `@if (canEdit)` in both views, `canEdit` sourced from `Model.CanEdit`. |

### Behavioral Spot-Checks / Test Execution

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| GuildMembers authorization suite (16 tests: 4 pre-existing Index + 12 new) | `dotnet test QuestBoard.IntegrationTests --filter FullyQualifiedName~GuildMembersControllerIntegrationTests` | 16/16 passed, 0 failed, 4s | PASS |
| Full solution regression | `dotnet test` (run once) | UnitTests: 183/183 passed; IntegrationTests: 330/330 passed | PASS |
| Build (Debug) | `dotnet build QuestBoard.IntegrationTests -c Debug` | 0 errors, 0 warnings | PASS |

Verifier ran these commands independently in this session (not sourced from SUMMARY/REVIEW-FIX claims) — the 183/330 counts match REVIEW-FIX.md's claim exactly, confirmed by direct re-execution, not trusted from the report.

### Requirements Coverage

No REQUIREMENTS.md mapping exists for Phase 56 (confirmed via grep — zero matches for "56" or "Phase 56" in `.planning/REQUIREMENTS.md`). This is expected: phase frontmatter states "ad-hoc backlog phase — no REQUIREMENTS.md mapping," consistent with Phases 47-51 and 55 per 56-CONTEXT.md. Source of truth is 56-CONTEXT.md decisions D-01 through D-04, all independently verified above (truths 8-11).

| Requirement | Source | Status | Evidence |
|---|---|---|---|
| D-01 | 56-CONTEXT.md | SATISFIED | Bypass applied uniformly to Edit/Delete/ToggleRetirement via shared helper. |
| D-02 | 56-CONTEXT.md | SATISFIED | Only `GroupRole.Admin` (incl. SuperAdmin resolution) passes the guard; DM excluded. |
| D-03 | 56-CONTEXT.md | SATISFIED | No hand-rolled cross-tenant guard; relies on existing query filter. |
| D-04 | 56-CONTEXT.md | SATISFIED | `CanEdit` added as independent property; `IsOwner` semantics preserved. |

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| — | — | None found | — | Grep scan of all 6 modified files for TBD/FIXME/XXX/TODO/HACK/PLACEHOLDER, phase/requirement/review-ID references (D-0x, CR-01, WR-0x, Phase 56, 56-0x), `IsInRole("SuperAdmin")`, manual cross-tenant comparisons, and unconditional `IsOwner = true` — all clean. |

### Code Review Fix Confirmation (CR-01 / WR-01 / WR-02)

The phase's code review (56-REVIEW.md) found 1 Critical + 2 Warnings. All three were independently confirmed present in the current codebase (not just claimed in 56-REVIEW-FIX.md):

- **CR-01** (critical — admin promoting another player's character to Main used the wrong owner id and silently dropped the rest of the edit): Confirmed fixed at `GuildMembersController.cs:275,282` — `UpdateAsync(existingCharacter, token)` now runs unconditionally before the Main-promotion branch, and `SetAsMainCharacterAsync(id, existingCharacter.OwnerId, token)` uses the target character's actual owner id, not `currentUser.Id`. A new regression test (`Edit_AdminEditingAnotherPlayersCharacterSetAsMain_ShouldPersistChangesAndPromoteCorrectOwner`) exercises exactly this scenario — asserts the target's fields persisted, target promoted to Main, AND the admin's own pre-existing Main character was left untouched. Test passes (verified in this session's test run).
- **WR-01** (Details' null checks on `currentUser` were dead code since `GetUserAsync` never returns null): Confirmed fixed at `GuildMembersController.cs:63,65-68` — checks now use `currentUser.Id != 0`.
- **WR-02** (duplicated owner-or-admin guard across four call sites): Confirmed fixed — `CanManageCharacterAsync` private helper (lines 374-383) now used at all four guard sites (Edit GET/POST, Delete, ToggleRetirement).

Commits `e2ebafb`, `387b164`, `2da1efa`, `ad9c9a2` all verified present in `git log` with matching diffs to the claims.

### Human Verification Required

None. All must-haves are verifiable via passing automated tests and direct code inspection; no visual, real-time, or external-service-dependent behavior in this phase's scope.

### Gaps Summary

No gaps. All 11 must-have truths (roadmap goal + PLAN frontmatter, deduplicated) are verified against the current codebase state, not just the SUMMARY/REVIEW-FIX narrative. The critical CR-01 bug found in code review was independently confirmed fixed — both by reading the corrected code (owner id now correctly threaded to `SetAsMainCharacterAsync`, edits persisted unconditionally) and by re-running the full test suite in this session (183 unit + 330 integration tests, 0 failures), which matches but does not merely trust the reported counts.

---

_Verified: 2026-07-06T19:00:00Z_
_Verifier: Claude (gsd-verifier)_
