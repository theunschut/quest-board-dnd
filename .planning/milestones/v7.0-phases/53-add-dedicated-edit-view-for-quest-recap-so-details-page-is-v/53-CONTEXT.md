# Phase 53: Add dedicated Edit view for Quest recap so Details page is view-only - Context

**Gathered:** 2026-07-06
**Status:** Ready for planning

<domain>
## Phase Boundary

`QuestLog/Details.cshtml` and `Details.Mobile.cshtml` currently embed the recap *edit form* inline: when `ViewBag.CanEditRecap` is true (the quest's DM or an Admin), the "Session Recap" section renders a raw `<textarea>` + Save button instead of the recap text; everyone else sees the read-only recap (or an empty-state message). This phase extracts that edit form into a new dedicated Edit page (its own GET+POST action pair + view, mirroring the existing `Quest/Edit.cshtml` full-page pattern), so `Details` becomes pure read-only display for **everyone**, DM/Admin included — with a link/button that takes an editor to the new edit page instead.

No backend/service changes: `QuestService.UpdateQuestRecapAsync` and the `Recap` field already exist and are unchanged. This is purely a controller-action + view restructuring task, applying equally to OneShot and Campaign quests (recap has no `BoardType` conditional today).

**Not in scope:** the "Session Recap Available" badge on the Quest Log list view (`Index.cshtml`) — already correct, checks `!string.IsNullOrWhiteSpace(quest.Recap)`, untouched by this phase.

</domain>

<decisions>
## Implementation Decisions

### Edit entry point
- **D-01:** The edit affordance lives inline in the Session Recap section itself — a button directly under the read-only recap text (or under the "No recap has been written yet" empty-state message) — not in the Quick Actions sidebar. Keeps the edit affordance where the content it edits lives, closest to today's in-place feel.
- **D-02:** The button label is dynamic: **"Add Recap"** when `Model.Quest.Recap` is empty/whitespace, **"Edit Recap"** when it already has content. Mirrors the existing empty-state-vs-content branching already present in this view (`!string.IsNullOrWhiteSpace(Model.Quest.Recap)`).
- Only shown when `ViewBag.CanEditRecap` is true — same authorization gate as today's inline form.

### Save & Cancel navigation
- **D-03:** The new Edit Recap page gets an explicit **Cancel** button alongside **Save Recap**, laid out `d-flex justify-content-between` — Cancel (secondary) left, Save Recap (primary) right — per this project's locked CLAUDE.md button-layout convention (same pattern `Quest/Edit.cshtml` already follows). Cancel discards any unsaved changes and returns to `QuestLog/Details`; Save persists via the existing `UpdateRecap` POST action, which already redirects to `Details` — no change needed there.

### Direct-URL access for non-editors
- **D-04:** If a user without edit rights (e.g. a player, or a DM who isn't this quest's DM) navigates directly to the new Edit Recap GET URL, the response is **403 Forbidden** — mirroring `UpdateRecap`'s existing, already-tested POST behavior (`Forbid()` when the caller isn't the quest's DM or an Admin; see `UpdateRecap_Player_IsForbiddenOrRedirected` in the integration test suite). Deliberately **not** the project's usual cross-tenant 404 convention (Phase 49/55) — this authorization check is role/ownership-based within the same group, not a cross-tenant existence-hiding case, and consistency with the sibling POST action on the same feature was judged more important than the general convention.

### Claude's Discretion
- Exact new action names/routes (e.g. `QuestLogController.EditRecap` GET+POST vs. an alternative) — implementation detail, no user-observable difference as long as it's reachable from the Details page's new button and both desktop + mobile stay consistent.
- Whether the GET action reuses the same `isCompletedOneShot`/`IsClosed` "is this a completed quest" guard `UpdateRecap` already has (recommended: yes, for consistency — a recap edit page shouldn't be reachable for an in-progress quest any more than the POST already prevents saving one).
- Exact Razor structure of the new Edit Recap view (modern-card header icon, help text wording) — mirror `Quest/Edit.cshtml`'s established modern-card pattern; carry over the existing textarea styling (10 rows desktop / 6 rows mobile, placeholder, "Share the story of this adventure with your players!" helper text) verbatim unless it looks wrong once built.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### The feature being restructured
- `QuestBoard.Service/Controllers/QuestBoard/QuestLogController.cs` — `Details` GET (lines 32-75, sets `ViewBag.CanEditRecap` at line 66) and `UpdateRecap` POST (lines 77-117, `[Authorize(Policy = "DungeonMasterOnly")]` + in-action `isQuestDm || isAdmin` check, `Forbid()` at line 111, redirects to `Details` at line 116). The new GET+POST edit actions belong on this controller, following the same authorization shape.
- `QuestBoard.Service/Views/QuestLog/Details.cshtml` (lines 97-136) — the inline recap form/display block to replace with a read-only display + entry-point button (D-01/D-02).
- `QuestBoard.Service/Views/QuestLog/Details.Mobile.cshtml` (lines 91-124) — same, mobile variant.
- `QuestBoard.Domain/Interfaces/IQuestService.cs` / `QuestService.cs:191` — `UpdateQuestRecapAsync(int questId, string recap, CancellationToken token)` — existing service method, unchanged, to be called from the new POST action exactly as `UpdateRecap` calls it today.

### The pattern to mirror (dedicated Edit page)
- `QuestBoard.Service/Views/Quest/Edit.cshtml` (+ `Edit.Mobile.cshtml`) — modern-card layout with `card-header modern-card-header` + icon, `card-body modern-card-body`, validation summary, `<hr>` before the button row. The new Edit Recap view should follow this exact structural convention per CLAUDE.md's "UI/UX Design Guidelines".
- `CLAUDE.md` "UI/UX Design Guidelines" section — locks the `modern-card`/`modern-card-header`/`modern-card-body` classes, `<hr>` before buttons, filled (not outline) colored buttons with FontAwesome + `me-2`, and the `d-flex justify-content-between` secondary-left/primary-right button layout (governs D-03).

### Precedent for the 403-not-404 decision (D-04)
- Existing integration tests (test project, `QuestLogController` recap tests) — `UpdateRecap_Player_IsForbiddenOrRedirected` and `UpdateRecap_NonOwnerAdmin_IsNotForbidden` already establish and verify the Forbid()/403 behavior this phase's GET action must match.
- `.planning/phases/49-fix-guild-members-page-missing-group-tenant-filtering/49-CONTEXT.md` and `.planning/phases/55-fix-cross-tenant-quest-leak-on-quest-board-quests-from-anoth/55-CONTEXT.md` — for contrast: these establish the project's general cross-tenant 404 convention, which D-04 deliberately does NOT apply here (this is an intra-group role/ownership check on an already-tested feature, not a cross-tenant existence-hiding case).

### Styling
- `QuestBoard.Service/wwwroot/css/quests.css` — `.recap-display-box` (lines 825-834) and `.recap-badge` (lines 891-898, unrelated to this phase, used on Quest Log Index).
- `QuestBoard.Service/wwwroot/css/quest-log-detail.mobile.css` (lines 59-72) — mobile `.recap-display-box` and textarea styling to carry over into the new mobile edit view.

No external ADRs/specs beyond the codebase references above — this originated as a one-line backlog title in `.planning/ROADMAP.md` (Phase 53) with no elaboration; all scope and behavior decisions above were derived from codebase inspection and confirmed with the user during this discussion.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `QuestService.UpdateQuestRecapAsync(int questId, string recap, CancellationToken token)` — already exists, already correct; the new POST action calls it identically to today's `UpdateRecap`.
- `Quest/Edit.cshtml`'s modern-card + validation-summary + button-row structure — direct template to copy for the new Edit Recap view (desktop + mobile).
- The existing "is this quest completed" guard (`isCompletedOneShot && !quest.IsClosed` check, duplicated in both `Details` GET and `UpdateRecap` POST) — reuse for the new GET action too (Claude's Discretion item above).

### Established Patterns
- Authorization for recap editing is two-layered: `[Authorize(Policy = "DungeonMasterOnly")]` at the action level, then a stricter in-action `isQuestDm || isAdmin` check scoped to *this specific quest's* DM (not just "any DM"). The new GET action must replicate both layers, not just the coarse policy.
- `ViewBag.BoardType` and `ViewBag.CanEditRecap` are both set in `Details` GET and consumed by the view — the new GET action needs its own equivalent ViewBag/ViewModel setup, it won't inherit these from `Details`.
- Cross-tenant/unauthorized responses in this codebase are usually 404 (Phase 49, Phase 55) — D-04 is a deliberate, documented exception because this is a same-group authorization check with an existing tested 403 precedent on the sibling POST action.

### Integration Points
- No new controller, service, or entity — only two new actions on the existing `QuestLogController` and one new view pair (`Views/QuestLog/EditRecap.cshtml` + `.Mobile.cshtml`, or whatever name is chosen per Claude's Discretion).
- `Details.cshtml`/`Details.Mobile.cshtml`'s Session Recap section shrinks to: read-only display (existing `recap-display-box` / empty-state markup, unchanged) + one new conditional button (D-01/D-02) when `CanEditRecap` is true.

</code_context>

<specifics>
## Specific Ideas

No verbatim phrasing requests from the user beyond the roadmap's one-line phase title ("Add dedicated Edit view for Quest recap so Details page is view-only") — all decisions above were derived from codebase inspection and confirmed via the three discussed gray areas (entry point, Save/Cancel navigation, direct-URL access).

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope. The "Session Recap Available" badge (Quest Log Index) was explicitly noted as already correct and out of scope, not deferred as unfinished work.

</deferred>

---

*Phase: 53-add-dedicated-edit-view-for-quest-recap-so-details-page-is-v*
*Context gathered: 2026-07-06*
