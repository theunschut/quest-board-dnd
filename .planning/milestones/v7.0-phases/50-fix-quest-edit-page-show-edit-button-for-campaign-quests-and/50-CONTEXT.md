# Phase 50: Fix quest edit page: show edit button for campaign quests and align field visibility with create page - Context

**Gathered:** 2026-07-05
**Status:** Ready for planning

<domain>
## Phase Boundary

Two related bugs in the Campaign-board-type quest flow, both traced to `QuestController.cs` / `Views/Quest/*.cshtml`:

1. **Manage page has no Edit button for Campaign quests.** `Views/Quest/Manage.cshtml` and `Manage.Mobile.cshtml` branch on `boardType == BoardType.Campaign` to render a minimal action row (Close/Reopen + Refresh Data only). Unlike the OneShot branch — which has "Edit Quest" (and "Delete") in two different sub-states — the Campaign branch has neither. Fix: add "Edit Quest" (and, per discussion, "Delete Quest") to the Campaign action row on both views.

2. **Edit page doesn't hide Campaign-irrelevant fields the way Create does.** `Views/Quest/Create.cshtml`/`Create.Mobile.cshtml` already wrap Challenge Rating, Total Player Count, the DM-Session-Only checkbox, and the Proposed Dates block in `@if (boardType != BoardType.Campaign)`. `Views/Quest/Edit.cshtml`/`Edit.Mobile.cshtml` show all four unconditionally — and don't even receive `ViewBag.BoardType` from `QuestController.Edit` GET (the Create action sets it; Edit does not). Fix: mirror Create's exact conditional on Edit's same four fields, and set `ViewBag.BoardType` in the `Edit` GET action.

**Not in scope:** `QuestController.Edit` POST already sanitizes these four fields to fixed defaults for Campaign quests server-side (lines 238-244) — this phase is a display-only fix, no service/domain/data changes. The pre-existing mismatch between Create's/Edit's sidebar tips text and Campaign board type is also not in scope (see D-06).

</domain>

<decisions>
## Implementation Decisions

### Manage page — Edit button for Campaign quests
- **D-01:** Add an "Edit Quest" button to the Campaign action row in both `Manage.cshtml` (desktop, lines 524-547) and `Manage.Mobile.cshtml` (mobile, lines 372-396). Style: reuse the exact pattern already used twice elsewhere in the same file for OneShot quests — `btn-primary` + `fa-edit` icon + "Edit Quest" text, linking to `Url.Action("Edit", "Quest", new { id = Model.Id })`.
- **D-02:** Placement: "Edit Quest" goes **before** Close/Reopen in the action row — mirrors the OneShot unfinalized row's existing order (Finalize, Edit, Delete), where content-editing actions precede state-transition actions.

### Manage page — Delete Quest parity (discovered during discussion, pulled into scope)
- **D-03:** Delete Quest is also missing from the Campaign action row (only reachable today via the `_QuestCard.cshtml` Delete button on the Quest Index page). User confirmed: add it to the Campaign Manage row too, in this same phase — same root cause as D-01 (the Campaign branch was built with a minimal Close/Reopen-only action set).
  - Style/placement: reuse the existing OneShot pattern verbatim — `btn-danger` + `fa-trash` + "Delete" (desktop) / "Delete Quest" (mobile), wired to the `deleteQuest(id)` JS function already defined in both `Manage.cshtml` and `Manage.Mobile.cshtml`'s `<script>` block (no new JS needed). Ordering: last in the action row, after Edit Quest and Close/Reopen — matching the OneShot unfinalized row's Finalize → Edit → Delete sequence.

### Edit page — field-hiding scope
- **D-04:** `Edit.cshtml` and `Edit.Mobile.cshtml` hide exactly the same 4 fields Create hides, using the identical `@if (boardType != BoardType.Campaign)` condition: Challenge Rating, Total Player Count, DM-Session-Only checkbox, and the entire Proposed Dates block (including its `<hr>`/warning-banner context where applicable). No field is kept visible read-only — full parity with Create, not a variant treatment.
- **D-05:** `QuestController.Edit` GET action must set `ViewBag.BoardType = await GetActiveBoardTypeAsync(token);` (mirroring the `Create` GET action) so the view has the value to branch on. The `Edit` POST action already resolves `boardType` internally (line 237) for its existing sanitization logic — no change needed there.

### Edit page — sidebar tips (considered, left unchanged)
- **D-06:** `Edit.cshtml`'s desktop-only "Quest Editing Tips" sidebar (entirely about proposed dates/votes) stays as-is for Campaign quests, matching `Create.cshtml`'s sidebar, which has the identical mismatch (lists date/player-count tips regardless of board type) and is explicitly not being fixed in this phase either. User confirmed: mirror what Create does today, don't fix a gap Create itself still has. `Edit.Mobile.cshtml` has no tips sidebar at all, so nothing to change there.

### Claude's Discretion
- None outstanding — all four discussed areas resolved to explicit decisions (D-01 through D-06).

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### The pattern being mirrored (Create page — already correct)
- `QuestBoard.Service/Views/Quest/Create.cshtml` (lines 34-83) — the `@if (boardType != BoardType.Campaign)` block wrapping Challenge Rating, Total Player Count, DM-Session-Only checkbox, and Proposed Dates. Edit's new conditional must match this shape exactly.
- `QuestBoard.Service/Views/Quest/Create.Mobile.cshtml` (lines 44-99) — same pattern, mobile variant.
- `QuestBoard.Service/Controllers/QuestBoard/QuestController.cs` — `Create` GET action (line 77: `ViewBag.BoardType = await GetActiveBoardTypeAsync(token);`) — the line to replicate in `Edit` GET.

### The bug (Manage page — missing actions for Campaign)
- `QuestBoard.Service/Views/Quest/Manage.cshtml` (lines 524-547) — the `@if (boardType == BoardType.Campaign)` action row to extend with Edit Quest (D-01/D-02) and Delete Quest (D-03).
- `QuestBoard.Service/Views/Quest/Manage.Mobile.cshtml` (lines 372-396) — same, mobile variant.
- `QuestBoard.Service/Views/Quest/Manage.cshtml` lines 341-353 (and `Manage.Mobile.cshtml` lines 351-366) — the OneShot unfinalized action row: the exact "Edit Quest" (`btn-primary`/`fa-edit`) and "Delete" (`btn-danger`/`fa-trash`, `onclick="deleteQuest(@Model.Id)"`) button markup to copy into the Campaign branch.
- `QuestBoard.Service/Views/Quest/Manage.cshtml` lines 676-692 (and `Manage.Mobile.cshtml` lines 402-418) — the existing `deleteQuest(id)` JS function in this file's `<script>` block; D-03 reuses it as-is, no new JS.

### The bug (Edit page — missing board-type conditional)
- `QuestBoard.Service/Views/Quest/Edit.cshtml` (lines 34-92) — the four fields to wrap in `@if (boardType != BoardType.Campaign)`: `Quest.ChallengeRating` (34-39), `Quest.TotalPlayerCount` (42-46), `Quest.DungeonMasterSession` checkbox (48-56), and the entire Proposed Dates block including its `HasExistingSignups` warning banner (58-92).
- `QuestBoard.Service/Views/Quest/Edit.Mobile.cshtml` (lines 48-94) — same four fields, mobile variant (no `HasExistingSignups` banner variant differences to worry about — same structure).
- `QuestBoard.Service/Controllers/QuestBoard/QuestController.cs` — `Edit` GET action (lines 138-183): currently has no `ViewBag.BoardType` assignment at all; add it per D-05. `Edit` POST action (lines 185-259, specifically 237-244) already has the correct server-side sanitization — confirms this is display-only, not a data-integrity bug.

No external ADRs/specs govern this — it's a targeted bug fix; requirements are fully captured in the decisions above.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `BoardType` enum (`QuestBoard.Domain.Enums`) + `QuestController.GetActiveBoardTypeAsync()` — already the single source of truth for board type resolution; both `Manage` and `Create` GET actions already call it. `Edit` GET just needs the same one-line call (D-05).
- `deleteQuest(id)` JS function — already present in both `Manage.cshtml` and `Manage.Mobile.cshtml`; D-03's new Delete button calls it directly, zero new script needed.
- `Url.Action("Edit", "Quest", new { id = Model.Id })` — already used elsewhere in the same Manage views (the "No Proposed Dates" alert, the Finalize form's button row) for the exact link D-01 needs.

### Established Patterns
- Board-type conditionals in this codebase are always a plain `@if (boardType != BoardType.Campaign)` / `@if (boardType == BoardType.Campaign)` Razor block, never a separate partial view or ViewComponent — Create.cshtml and Manage.cshtml both follow this. Edit.cshtml/Edit.Mobile.cshtml should follow the same shape for consistency, not a new pattern.
- Server-side sanitization of Campaign-irrelevant fields already happens independently in both `Create` POST (lines 103-111) and `Edit` POST (lines 238-244) — this phase only touches the GET-side rendering / view markup, never the sanitization logic itself.
- Action-row button ordering convention observed in this file: primary/content actions first (Finalize, Edit), destructive action last (Delete), secondary/utility action (Refresh Data) visually separated on the opposite side of the `d-flex justify-content-between` row. D-01/D-02/D-03 extend the Campaign row to follow this same convention.

### Integration Points
- No new controller actions, services, or ViewModels needed — this phase only edits existing Razor markup (`Manage.cshtml`, `Manage.Mobile.cshtml`, `Edit.cshtml`, `Edit.Mobile.cshtml`) plus one line in `QuestController.Edit` GET (`ViewBag.BoardType` assignment).
- `Close`/`Reopen`/`Delete`/`Edit` POST actions already have their own independent server-side `BoardType` guards (e.g., `Close`/`Reopen` reject non-Campaign quests at QuestController.cs:710-714/749-753) — adding UI buttons here doesn't introduce any new authorization surface, since the underlying actions were already safely reachable by URL.

</code_context>

<specifics>
## Specific Ideas

User's original report: "The quest manage page doesn't have an edit button if the board type is campaign... I need the edit button to be there in a campaign quest as well, so in all cases. But I noticed the create page hides elements in case of campaign type but the edit page does not. So this needs to be edited as well." Both halves confirmed via code inspection: Manage's Campaign branch genuinely has no Edit link (D-01), and Edit.cshtml/.Mobile.cshtml genuinely render all fields unconditionally with no `boardType` variable even declared (D-04/D-05).

During discussion, investigating the Manage page's Campaign action row surfaced a second, closely-related gap not mentioned in the original report: Delete Quest is also absent from that row (D-03). User chose to fix it in the same phase rather than defer, since it shares the exact same root cause as the Edit button gap.

</specifics>

<deferred>
## Deferred Ideas

None — all four discussed areas resolved into in-scope decisions (D-01 through D-06). The Edit/Create sidebar tips' pre-existing lack of Campaign-awareness was explicitly considered and left unchanged (D-06), not deferred as a future phase — it's an acknowledged pre-existing gap in Create that this phase deliberately does not expand scope to fix.

</deferred>

---

*Phase: 50-fix-quest-edit-page-show-edit-button-for-campaign-quests-and*
*Context gathered: 2026-07-05*
