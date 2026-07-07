# Phase 61: Allow DMs to edit finalized quest details (excluding proposed and selected dates) - Context

**Gathered:** 2026-07-07
**Status:** Ready for planning

<domain>
## Phase Boundary

Today, `QuestController.Edit` (GET and POST) hard-blocks editing once `quest.IsFinalized` is true — both actions return `BadRequest("Cannot edit a finalized quest. Open the quest first to make changes.")`. The only way to change a finalized OneShot quest's details today is `Open` (`QuestRepository.OpenQuestAsync`), which un-finalizes the quest **and** resets every signup's `IsSelected = false`, wiping the player roster.

This phase removes that block for the non-date fields: a DM (or Admin) can edit Title, Description, Rewards, Challenge Rating, Total Player Count, and Dungeon Master Session Only on a finalized quest without going through Open — so the already-locked-in date and player selections survive untouched. `ProposedDates` and `FinalizedDate` ("the proposed and selected dates" from the phase title) stay off-limits: the Proposed Dates section is hidden from the Edit form entirely when the quest is finalized, and the update call skips `ProposedDates` for finalized quests.

Scoped to OneShot-board quests only — Campaign-board quests never use `IsFinalized`/`Finalize` (no proposed dates, no per-quest signup capacity; they use `Close`/`Reopen` instead) and are already always editable via the existing Campaign section on Manage, unchanged by this phase.

**Not in scope:** changing who is selected (`PlayerSignup.IsSelected`) — that stays governed by `Finalize`, `ChangeVoteAsync`'s auto-promotion, `RevokeSignupAsync`, and the Admin-only `RemovePlayerSignup` action, none of which this phase touches. The existing `Open` action/button is untouched and remains available for DMs who genuinely want to reset dates and selections.

</domain>

<decisions>
## Implementation Decisions

### Total Player Count edge case
- **D-01:** If a DM lowers Total Player Count below the number of players currently selected (`quest.PlayerSignups.Count(ps => ps.IsSelected && ps.Role == SignupRole.Player)`), reject with a validation error (e.g. "Total Player Count cannot be less than the N players already selected for this quest.") rather than allowing an over-capacity state or auto-removing anyone. The DM must remove a player first (existing Admin-only `RemovePlayerSignup` action on Manage) before lowering the count. This check only applies when `quest.IsFinalized` — before finalization no one is selected yet, so the scenario can't occur on the pre-finalize Edit path and that path's behavior is unchanged.

### Notifications
- **D-02:** No email notification when a DM edits a finalized quest's Title/Description/Rewards/Challenge Rating/Total Player Count/DM-session flag. Matches how these same fields already behave on non-finalized quests (no email today). Only `ProposedDates` changes trigger the existing date-changed email (`UpdateQuestPropertiesWithNotificationsAsync`'s `updateProposedDates` branch) — irrelevant here since finalized-quest edits never touch dates. Do not build a new "quest details updated" email/job this phase.

### Edit window
- **D-03:** Editing works on any finalized quest regardless of how long ago the session happened — no cutoff at the "Done" status (`IsFinalized && FinalizedDate <= yesterday`, shown as a dark "Done" badge on Manage). Same latitude as Phase 53's Recap editing, which also has no time cutoff. `IsFinalized` alone is the only gate that matters; `IsClosed`/`Done` state is irrelevant to whether Edit is reachable.

### Entry point & form reuse
- **D-04:** Reuse the existing `EditQuestViewModel` / `Quest/Edit.cshtml` (+ `.Mobile.cshtml`) and the existing `QuestController.Edit` GET/POST actions — no new controller action, no new view. Changes needed:
  - Remove the `if (quest.IsFinalized) return BadRequest(...)` block from both `Edit` GET and POST (`QuestController.cs`).
  - In the view, wrap the "Proposed Dates & Times" block (`Edit.cshtml:68-102`, `Edit.Mobile.cshtml`'s equivalent block) so it only renders `@if (boardType != BoardType.Campaign && !quest.IsFinalized)` — needs `IsFinalized` passed to the view (via `EditQuestViewModel` or `ViewBag`, since `QuestViewModel` itself has no `IsFinalized` property today).
  - In the POST action, when `existingQuest.IsFinalized`, call `UpdateQuestPropertiesWithNotificationsAsync` with `updateProposedDates: false` (omit `viewModel.Quest.ProposedDates` entirely) instead of today's hardcoded `true`.
  - Add an "Edit Quest" button on the finalized-OneShot branch of Manage, next to the existing "Open Quest" button: desktop `Manage.cshtml` around line 500-512 (inside the `<div class="d-flex gap-2">` alongside `Open`/`CreateFollowUp`/`SendReminder`), mobile `Manage.Mobile.cshtml` around line 120-139 (same `d-flex flex-wrap gap-2` group).

### Claude's Discretion
- Exact wording of the Total Player Count validation error message (D-01) — just needs to state the current selected-player count and that it can't be lowered below it.
- Whether the "Quest Editing Tips" sidebar tip list (`Edit.cshtml:118-146` — "Existing dates cannot be edited, only removed" etc.) needs a finalized-quest variant explaining dates aren't shown at all in this state, vs. just naturally not rendering the dates-specific tips when Proposed Dates isn't shown — planner's call on the cleanest way to keep the sidebar accurate.
- Whether `IsFinalized` reaches the view via a new `EditQuestViewModel.IsFinalized` property or via `ViewBag` (mirroring how `CanEditProposedDates`/`HasExistingSignups` already do it) — either is fine, just needs to be set in both `Edit` GET and re-render-on-validation-failure paths in POST.
- Where exactly the Total Player Count validation (D-01) lives — `ModelState.AddModelError` in the controller (consistent with existing `Create`'s proposed-dates-required check) vs. a guard inside `QuestService`/`QuestRepository`. Controller-level `ModelState` is the existing precedent for this kind of form-level validation in `Edit`/`Create`.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

No external ADRs/specs — this is an ad-hoc backlog phase (no REQUIREMENTS.md mapping), same pattern as Phases 47-60. All scope is captured in the decisions above and the file-level pointers below.

### The block to remove
- `QuestBoard.Service/Controllers/QuestBoard/QuestController.cs` — `Edit` GET (the `if (quest.IsFinalized) return BadRequest(...)` block) and `Edit` POST (the matching block on `existingQuest.IsFinalized`) — both to be removed/relaxed per D-04.

### Fields/model stack (already exist, no new properties needed except IsFinalized plumbing)
- `QuestBoard.Service/ViewModels/QuestViewModels/QuestViewModel.cs` — `Title`, `Description`, `Rewards`, `ChallengeRating`, `TotalPlayerCount`, `DungeonMasterSession`, `ProposedDates` — the exact field set this phase's Edit form already carries; no new fields needed.
- `QuestBoard.Service/ViewModels/QuestViewModels/EditQuestViewModel.cs` — `Id`, `Quest`, `DungeonMasters`, `CanEditProposedDates`, `HasExistingSignups` — candidate location for the `IsFinalized` flag (Claude's Discretion).
- `QuestBoard.Repository/Entities/QuestEntity.cs` — `IsFinalized`, `FinalizedDate`, `TotalPlayerCount` — the fields governing D-01/D-03's guards.

### Service/repository — update call and player-count guard
- `QuestBoard.Domain/Services/QuestService.cs:126-146` — `UpdateQuestPropertiesWithNotificationsAsync(questId, title, description, rewards, challengeRating, totalPlayerCount, dungeonMasterSession, updateProposedDates, proposedDates, token)` — call with `updateProposedDates: false` for finalized quests per D-04.
- `QuestBoard.Repository/QuestRepository.cs` — `UpdateQuestPropertiesWithNotificationsAsync` (repository impl) — only touches `entity.ProposedDates` when `updateProposedDates && proposedDates != null`; already safe to call with `false` for finalized-quest edits, no repository change needed for that part.
- `QuestBoard.Service/Controllers/QuestBoard/QuestController.cs` — `Finalize` action (~line 646) — reference for how `quest.PlayerSignups.Where(ps => selectedPlayerIds.Contains(ps.Id) && ps.Role == SignupRole.Player).Count()` computes the player-role selected count; D-01's validation needs the equivalent `quest.PlayerSignups.Count(ps => ps.IsSelected && ps.Role == SignupRole.Player)` against the *existing* selection (not a posted list) since Edit doesn't touch signups.

### Views — hide Proposed Dates when finalized
- `QuestBoard.Service/Views/Quest/Edit.cshtml:42-103` — the `@if (boardType != BoardType.Campaign)` block wrapping Challenge Rating/Total Player Count/DM Session/Proposed Dates; only the inner Proposed Dates sub-block (lines 68-102) needs the added `&& !IsFinalized` condition — CR/TotalPlayerCount/DMSession stay visible and editable per this phase's goal.
- `QuestBoard.Service/Views/Quest/Edit.Mobile.cshtml` — same structural mirror, its own Proposed Dates block (grep-confirmed around the `Model.Quest.ProposedDates` references).
- `QuestBoard.Service/Views/Quest/Edit.cshtml:118-146` — "Quest Editing Tips" sidebar — currently all tips are about dates; see Claude's Discretion for finalized-state handling.

### Manage page — new Edit Quest entry point for finalized quests
- `QuestBoard.Service/Views/Quest/Manage.cshtml:500-521` — finalized-OneShot button row (`Open Quest` / `Create Follow-Up Quest` / `Send Reminder`) — add `Edit Quest` here per D-04.
- `QuestBoard.Service/Views/Quest/Manage.Mobile.cshtml:120-139` — same row, mobile variant.
- `QuestBoard.Service/Views/Quest/Manage.cshtml:100-116` / `Manage.Mobile.cshtml:144-158` — the pre-finalize "No Proposed Dates" empty-state `Edit Quest` link — unaffected, already works today, listed only for contrast (this is the *unfinalized* Edit entry point, not the one this phase adds).

### Untouched — confirm no regressions
- `QuestBoard.Repository/QuestRepository.cs` — `OpenQuestAsync` (un-finalizes + resets `IsSelected` on every signup) and `FinalizeQuestAsync` (sets `IsFinalized`/`FinalizedDate`, computes initial `IsSelected`) — both stay exactly as-is; this phase adds an alternative lighter-weight edit path, it does not change what `Open`/`Finalize` do.
- `QuestBoard.Service/Controllers/QuestBoard/QuestController.cs` — `RemovePlayerSignup` (Admin-only) — the existing mechanism a DM/Admin already has to free a seat, referenced by D-01's error message as "what to do instead."

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `EditQuestViewModel` / `Quest/Edit.cshtml` + `.Mobile.cshtml` / `QuestController.Edit` GET+POST — the entire existing Edit feature is reused as-is per D-04; this phase only relaxes its finalized-quest guard and conditions one sub-section on `IsFinalized`.
- `ModelState.AddModelError` pattern already used in `Create` POST for the proposed-dates-required check — the same pattern applies to D-01's Total Player Count guard.

### Established Patterns
- BoardType-conditional field visibility (`@if (boardType != BoardType.Campaign)`) is the existing mechanism for hiding OneShot-only fields on Campaign quests; this phase adds a second, narrower condition (`&& !IsFinalized`) inside that block for the Proposed Dates sub-section specifically, not the whole block.
- `UpdateQuestPropertiesWithNotificationsAsync`'s `updateProposedDates` boolean already exists precisely to let a caller update quest properties without touching dates — Edit's finalized-quest path is simply the first caller to pass `false` instead of always `true`.
- Authorization for Edit is already correct for this phase's needs: `[Authorize(Policy = "DungeonMasterOnly")]` + in-action `IsQuestOwner(currentUser, quest.DungeonMaster) || role == GroupRole.Admin` check — identical to `Finalize`/`Open`/`Close` — no authorization changes needed.

### Integration Points
- Two controller actions (`Edit` GET, `Edit` POST) — remove/relax the `IsFinalized` block, add D-01's validation, pass `IsFinalized` to the view, call `UpdateQuestPropertiesWithNotificationsAsync` with `updateProposedDates: false` for finalized quests.
- Two views (`Edit.cshtml`, `Edit.Mobile.cshtml`) — condition the Proposed Dates sub-section on `!IsFinalized`.
- Two views (`Manage.cshtml`, `Manage.Mobile.cshtml`) — add the new `Edit Quest` button to the finalized-OneShot button row.
- No new migration, no new ViewModel class, no new service/repository method — every touched file already exists.

</code_context>

<specifics>
## Specific Ideas

User's own words: "I want the ability for a DM to edit their quest details when a quest is finalized. Pretty much everything except for the proposed and selected dates." — confirmed via discussion to mean: `ProposedDates` (the candidate-date list) and `FinalizedDate` (the date chosen among them) are excluded; every other quest field (Title, Description, Rewards, Challenge Rating, Total Player Count, DM Session flag) is editable, reusing the existing Edit form/action rather than building something new.

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope.

### Reviewed Todos (not folded)
None — `gsd-sdk query todo.match-phase 61` returned zero matches.

</deferred>

---

*Phase: 61-allow-dms-to-edit-finalized-quest-details-excluding-proposed*
*Context gathered: 2026-07-07*
