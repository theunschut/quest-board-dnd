# Phase 63: Allow any player to edit quest recaps, not just the assigned DM or admin - Context

**Gathered:** 2026-07-07
**Status:** Ready for planning

<domain>
## Phase Boundary

Today, editing a completed quest's Session Recap is gated to that specific quest's assigned DM, or an Admin — enforced twice: `[Authorize(Policy = "DungeonMasterOnly")]` on `QuestLogController.EditRecap` (GET+POST), plus an in-action `isQuestDm || isAdmin` check that further restricts to *this quest's* DM specifically (a different quest's DM is still `Forbid()`'d). The `Details`/`Details.Mobile` views gate the "Edit/Add Recap" button behind the same `ViewBag.CanEditRecap` computed identically.

This phase removes both gates so **any authenticated member of the quest's group** can open and save the recap — not just this quest's DM or an Admin. No change to which quests are eligible for recap editing (the existing `isCompletedOneShot || quest.IsClosed` completed-quest guard on GET/POST stays exactly as-is), no change to the recap's underlying storage (`QuestService.UpdateQuestRecapAsync`, unchanged), and no `BoardType` conditional (recap already applies equally to OneShot and Campaign quests, per Phase 53).

**Not in scope:** anything about who can view the quest log / recap (already open to any authenticated user — `Details` has no `CanEditRecap`-style view gate today, only an edit gate); the "Session Recap Available" badge on the Quest Log Index (untouched, unrelated).

</domain>

<decisions>
## Implementation Decisions

### Editor scope
- **D-01:** Recap editing opens to **any authenticated member of the quest's group** — not restricted to players who actually signed up/participated in that specific quest. Explicitly rejected the "only quest participants" alternative (checking `quest.PlayerSignups` for the current user) as unnecessary extra scope — the ask is simply "stop gating this to the DM/Admin," not "gate it to a different, narrower set."
- This means the `[Authorize(Policy = "DungeonMasterOnly")]` attribute on both `EditRecap` GET and POST must be removed (or relaxed to the class-level `[Authorize]`, which already requires authentication), and the in-action `isQuestDm || isAdmin` → `Forbid()` check in both actions must be removed. The existing completed-quest guard (`isCompletedOneShot || quest.IsClosed` → `NotFound()`/`BadRequest()`) stays untouched — it's orthogonal to *who* can edit, only *which quests* are editable.
- Tenant/group scoping is unaffected — `GetQuestWithDetailsAsync` already scopes quests to the active group, so "any authenticated member" naturally means "any member of the quest's own group," consistent with how the quest is already visible to that user at all.

### Manage Quest link must NOT be broadened (critical — avoids a permission-escalation bug)
- **D-02:** `Details.cshtml`/`Details.Mobile.cshtml` currently reuse the **same** `ViewBag.CanEditRecap` flag for two unrelated things: (1) showing the Edit/Add Recap button (line 130/106), and (2) showing the "Manage Quest" Quick Actions link (line 158/129) — which opens the full Manage page (Open/Close quest, remove player signups, edit quest details, etc.). Simply broadening `CanEditRecap` without splitting these would silently expose Manage Quest to every player — confirmed with the user this must NOT happen.
- **Required implementation:** split into two separate ViewBag flags:
  - `CanEditRecap` — broadened per D-01 (any authenticated user with quest visibility).
  - A new flag (e.g. `CanManageQuest`) — **unchanged** DM/Admin-only logic (`isQuestDm || isAdmin`, where `isQuestDm` means this quest's specific assigned DM), used to gate the "Manage Quest" link exactly as `CanEditRecap` does today. Mirrors `QuestController`'s existing `ViewBag.CanManage = isQuestDm || isAdmin` pattern (`QuestController.cs:340-342`) — same shape, just a sibling flag on `QuestLogController`.
- Both `Details.cshtml` and `Details.Mobile.cshtml` need this same two-flag split — same file/commit, not a follow-up (this project's established mobile-parity rule, most recently reaffirmed in Phase 61's context).

### Attribution
- **D-03:** No "last edited by" tracking. `Recap` stays a plain `string` field — no new column, no migration, no display change beyond the edit permission itself. Rejected adding editor attribution as unnecessary scope for this phase.

### Notifications
- **D-04:** No email or in-app notification to the DM when a non-DM player edits the recap. Matches this project's existing precedent (Phase 61 D-02 made the same call for quest-detail edits) and its constrained email budget (Resend: 100/day, 3000/month). No new email job/template.

### Claude's Discretion
- Exact name of the new "Manage Quest" gating flag (`CanManageQuest` suggested above; any equivalent name is fine as long as it's clearly distinct from `CanEditRecap` in the view code).
- Whether to keep the in-action DM/Admin check code path in `EditRecap` GET/POST as fully deleted vs. left as unreachable dead branches — expectation is full removal (simplification), but this is an implementation-detail call with no user-observable difference.
- Whether the `[Authorize]` attribute is entirely removed from `EditRecap` GET/POST (falling back to the class-level `[Authorize]`) or explicitly restated — no behavior difference either way since the class already applies `[Authorize]`.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### The feature being changed
- `QuestBoard.Service/Controllers/QuestBoard/QuestLogController.cs` — `Details` GET (lines 32-75; `CanEditRecap` computed at line 63-66), `EditRecap` GET (lines 77-114; `[Authorize(Policy = "DungeonMasterOnly")]` at line 78, `isQuestDm`/`isAdmin`/`Forbid()` at lines 104-111), `EditRecap` POST (lines 116-156; same policy attribute at line 118, same check at lines 144-151). All four gates (2 attributes + 2 in-action checks + 1 ViewBag computation) need to change together for this phase to be internally consistent.
- `QuestBoard.Service/Views/QuestLog/Details.cshtml` (recap section ~lines 115-143; Manage Quest link at ~line 158) and `Details.Mobile.cshtml` (recap section ~line 106; Manage Quest link at ~line 129) — both need the `CanEditRecap`/`CanManageQuest` flag split (D-02).

### Precedent for the DM/Admin-only "manage" pattern being preserved
- `QuestBoard.Service/Controllers/QuestBoard/QuestController.cs:340-342` — `ViewBag.CanManage = isQuestDm || isAdmin` on the Quest's own Manage-adjacent action; the new `CanManageQuest`-equivalent flag on `QuestLogController` should mirror this shape exactly.
- `.planning/phases/53-add-dedicated-edit-view-for-quest-recap-so-details-page-is-v/53-CONTEXT.md` — introduced the current `EditRecap` GET+POST actions and their DM/Admin gate (D-04 in that phase's context: 403-not-404, mirroring the POST's tested `Forbid()` behavior). This phase directly loosens that gate; the 403-vs-404 precedent becomes moot once the authorization check itself is removed (no one is `Forbid()`'d anymore for lacking DM/Admin status — only unauthenticated users are challenged).

### Mobile parity requirement
- `.planning/phases/61-allow-dms-to-edit-finalized-quest-details-excluding-proposed/61-CONTEXT.md` — states this project's standing rule: every view change ships identically on desktop and mobile in the same plan/commit, citing two prior incidents (Phase 43, Phase 54) where a desktop-only fix had to be backfilled for mobile in its own phase. Applies directly here since both `Details.cshtml` and `Details.Mobile.cshtml` need the same flag-split change.

### Existing role model (for reference — no changes needed here)
- `QuestBoard.Domain/Enums/GroupRole.cs` — `Player = 0, DungeonMaster = 1, Admin = 2`. The `"DungeonMasterOnly"` authorization policy (CLAUDE.md) requires `DungeonMaster` or `Admin`; removing it from `EditRecap` falls back to the controller's class-level `[Authorize]`, which requires only authentication — any role, including `Player`, then passes.

No external ADRs/specs beyond the codebase references above — this originated as a user-reported UX complaint ("not all DMs want to write recaps, some players do") with no prior planning doc; all scope and behavior decisions above were derived from codebase inspection and confirmed with the user during this discussion.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `QuestService.UpdateQuestRecapAsync(int questId, string recap, CancellationToken token)` — already exists, already correct, unchanged by this phase.
- `QuestController.cs`'s `ViewBag.CanManage = isQuestDm || isAdmin` — direct template for the new `CanManageQuest`-equivalent flag this phase must add to `QuestLogController`.

### Established Patterns
- Authorization in this controller today is two-layered (action-level policy attribute + in-action ownership check) — this phase removes both layers for recap editing specifically, while a *sibling* flag (Manage Quest) keeps both layers intact. The two concerns must not be conflated in the refactored code.
- The completed-quest eligibility guard (`isCompletedOneShot && !quest.DungeonMasterSession`, `!isCompletedOneShot && !quest.IsClosed`) is duplicated across `Details`, `EditRecap` GET, and `EditRecap` POST — unrelated to *who* can edit, stays as-is in all three places.

### Integration Points
- No new controller, service, or entity. Changes are confined to `QuestLogController.cs` (remove 2 attributes, remove 2 in-action checks, add 1 new ViewBag flag) and the two `Details` views (split 1 ViewBag flag into 2).

</code_context>

<specifics>
## Specific Ideas

User's own framing: "Not all DM's want to do this. Some players do, so why not leave it open for all to edit the recap?" — the intent is purely to remove the DM/Admin restriction, not to introduce a new, narrower restriction (e.g. participants-only). No other verbatim phrasing requests.

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope. The "quest participants only" alternative and "last edited by" attribution were both considered and explicitly declined (see D-01, D-03), not deferred as future work.

</deferred>

---

*Phase: 63-allow-any-player-to-edit-quest-recaps-not-just-the-assigned-*
*Context gathered: 2026-07-07*
