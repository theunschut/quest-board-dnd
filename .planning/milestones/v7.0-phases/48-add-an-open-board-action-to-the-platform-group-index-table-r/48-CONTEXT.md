# Phase 48: Add an Open Board action to the /platform group index table - Context

**Gathered:** 2026-07-04
**Status:** Ready for planning

<domain>
## Phase Boundary

Add an "Open Board" action button to the Platform area's Group index table (`/platform/group`), positioned to the left of the existing "Members" and "Edit" actions. Clicking it sets the clicked group as the SuperAdmin's active group (same session state GroupPicker sets) and navigates straight into that group's quest board, removing the current detour through `/groups/pick`. Applies to both the desktop and mobile Platform Group index views.

</domain>

<decisions>
## Implementation Decisions

### Click behavior & destination
- **D-01:** "Open Board" reuses the existing `GroupPickerController.SelectGroup` POST action as-is — no new controller action. Render a small form (hidden `groupId` input + `@Html.AntiForgeryToken()`) per row, submitted like the card-click pattern in `Views/GroupPicker/Index.cshtml`. `returnUrl` is left empty, so `SelectGroup` falls through to its default `RedirectToAction("Index", "Quest")` — identical behavior to picking a group from `/groups/pick`.
- **D-02:** No confirmation dialog on click — this is a navigation action, not a destructive one.

### Return path to Platform
- **D-03:** Do not add a nav-level shortcut back to `/platform` in this phase. The existing path (user menu → "Switch Group" → GroupPicker's "Go to Platform" button, visible only to SuperAdmins) is accepted as sufficient. Explicitly out of scope — do not touch `_Layout.cshtml` / `_Layout.Mobile.cshtml` nav for this.

### Mobile parity
- **D-04:** Add the same "Open Board" button to both `Areas/Platform/Views/Group/Index.cshtml` (desktop) and `Areas/Platform/Views/Group/Index.Mobile.cshtml` (mobile), matching the existing per-row action-button pattern in each view.

### Claude's Discretion
- Exact icon/color for the new button. Recommended: `btn-primary` + `fa-door-open` (Members=`btn-info`, Edit=`btn-warning`, Delete=`btn-danger` are already taken; primary reads as "the leftmost/primary action," and `fa-door-open` reads as "enter" without colliding with the `fa-dice-d20`/`fa-book-open` board-type badge icons already in the same table). Follow the `me-2` icon-spacing and filled-button conventions from CLAUDE.md's UI/UX guidelines.
- Whether the button is shown/enabled for groups with 0 members (an empty group still has a valid, if empty, quest board) — default to always showing it, no member-count gate (unlike the existing Delete button, which IS gated on `MemberCount == 0`).

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Existing group-selection flow (the functionality being reused)
- `QuestBoard.Service/Controllers/GroupPickerController.cs` — `SelectGroup` POST action (lines 41-51): sets `SessionKeys.ActiveGroupId`/`ActiveGroupName`, redirects via `RedirectToLocal`. This is the exact functionality "Open Board" must trigger.
- `QuestBoard.Service/Views/GroupPicker/Index.cshtml` — reference implementation of the per-item `<form asp-action="SelectGroup" method="post">` + hidden `groupId`/`returnUrl` inputs + `@Html.AntiForgeryToken()` pattern to replicate.

### Views to modify
- `QuestBoard.Service/Areas/Platform/Views/Group/Index.cshtml` — desktop action-list table (Actions column, lines 52-65) where "Open Board" must be inserted to the left of "Members".
- `QuestBoard.Service/Areas/Platform/Views/Group/Index.Mobile.cshtml` — mobile per-card action-button row (lines 44-57), same insertion point.

### Controller (unchanged, but must not be duplicated)
- `QuestBoard.Service/Areas/Platform/Controllers/GroupController.cs` — the `Index` action feeding these views. No new action needed here per D-01.

No external ADRs/specs govern this — requirements are fully captured in the decisions above.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `GroupPickerController.SelectGroup` — already does exactly what "Open Board" needs (session write + redirect); reusing it means zero new backend code, per D-01.
- The card-click-submits-hidden-form pattern in `Views/GroupPicker/Index.cshtml` (lines 40-52) is the direct analog for a per-row form button in a table/card-list context.

### Established Patterns
- Platform Group index action buttons are anchor tags styled `btn btn-sm btn-{color}` with a leading FontAwesome icon + `me-1`/`me-2` spacing (`Members`=info, `Edit`=warning, `Delete`=danger, conditionally shown via `@if (item.MemberCount == 0)`). "Open Board" will instead be a `<form>` (POST + antiforgery), not a plain `<a>`, since it must submit `groupId` — this is the one structural difference from its siblings in the same action cell.
- Desktop/mobile Platform Group views are separate `.cshtml`/`.Mobile.cshtml` files with duplicated markup (no shared partial for the action buttons) — both must be edited per D-04.

### Integration Points
- `GroupPickerController` lives outside the `Platform` area (default area), so the form's `asp-controller="GroupPicker" asp-action="SelectGroup"` must NOT set `asp-area` (or must explicitly set `asp-area=""`) to avoid resolving into the `Platform` area's routing.
- `SessionKeys.ActiveGroupId`/`ActiveGroupName` (set by `SelectGroup`) are what `QuestController`/`GroupSessionMiddleware`/`IBoardTypeResolver` read to determine which group's board renders next — no changes needed there, this phase only triggers the existing write.

</code_context>

<specifics>
## Specific Ideas

User's original framing: "Open Board" should sit to the left of "Members" and "Edit" in the platform group index's action list, and have "the same functionality as the GroupPicker" — confirmed via discussion to mean literally reusing `GroupPickerController.SelectGroup`, not reimplementing equivalent logic.

</specifics>

<deferred>
## Deferred Ideas

- Persistent nav-level shortcut back to `/platform` for SuperAdmins after using Open Board — explicitly deferred (D-03). Today's path (user menu → Switch Group → "Go to Platform") stays as-is. Could become its own small phase later if it proves annoying in practice.

</deferred>

---

*Phase: 48-add-an-open-board-action-to-the-platform-group-index-table-r*
*Context gathered: 2026-07-04*
