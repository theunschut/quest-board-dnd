# Phase 35: Board Type Configuration - Context

**Gathered:** 2026-07-03
**Status:** Ready for planning

<domain>
## Phase Boundary

A SuperAdmin sets a group's `BoardType` (One-Shot or Campaign) when creating it via the existing Platform area `GroupController`. The choice is permanent — the Edit form displays it read-only and cannot change it, and existing groups default to One-Shot. Nothing outside group creation/editing branches on `BoardType` yet — that dispatch logic (quest posting/closing, navigation, emails) is Phases 36-37.

</domain>

<decisions>
## Implementation Decisions

### Create-form selector
- **D-01:** Board type is picked via a `<select>` dropdown on the Create form, using the app's existing enum-dropdown convention (`asp-items="Html.GetEnumSelectList<BoardType>()"`, same idiom as `GroupRole` in `Members.cshtml`/`CreateUser.cshtml`) — not radio buttons or toggle cards.
- **D-02:** No explanatory helper text about what One-Shot vs Campaign means is shown under the dropdown — just the field label and enum option names.
- **D-03:** The dropdown has no pre-selected default — it starts on a blank/placeholder option, forcing the SuperAdmin to actively choose one before the form validates. (This applies only to the Create form UI; the `BoardType` enum's underlying default/ordinal and the DB column's `defaultValue` for backfilling *existing* groups — required by success criterion 4 — are unaffected and still default those existing rows to One-Shot.)
- **D-04:** An explicit permanence warning is shown near the field (e.g. small muted text: "This cannot be changed after creation").

### Locked display on Edit
- **D-05:** The Edit form shows board type as a disabled/greyed `<select>` displaying the current value — visually consistent with the Create form's dropdown, but non-interactive.
- **D-06:** If the Edit form is tampered with (e.g. via devtools) and a changed board type is POSTed, the server silently ignores it — no error, no special message. This follows the existing Edit POST pattern where the handler only ever copies `model.Name` back onto the loaded entity; `BoardType` must not be read from the posted `GroupEditViewModel` (or must not exist as a bindable property on it at all) and must never be written back to the entity from that action.

### Group list visibility
- **D-07:** `Areas/Platform/Views/Group/Index.cshtml` (the SuperAdmin's group list — not any player-facing view) shows board type as a badge/pill in its own dedicated column, positioned between the existing Name and Members columns (or Members and Created — planner's call on exact ordering). Its `.Mobile.cshtml` counterpart should get the same treatment.

### Claude's Discretion
- Tamper handling on Edit POST: user selected "your choice" between "Silent no-op" and "Silent no-op + toast" — both are functionally silent (no value change); Claude chose plain silent no-op with the existing "Group name updated." success message left as-is, since nothing legitimate was blocked and no distinct messaging is needed.
- Exact badge color/styling for the board type pill (both on Index and Edit) is left to the planner/implementer, following whatever badge/pill CSS convention already exists in the app (e.g. how `GroupRole` badges are styled in `Members.cshtml`).
- Exact column ordering on Index.cshtml (Board Type before or after Members) is left to the planner.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Requirements & roadmap
- `.planning/ROADMAP.md` — Phase 35 section (goal, success criteria, BOARD-01/BOARD-02)
- `.planning/REQUIREMENTS.md` — BOARD-01, BOARD-02 definitions and Out of Scope table (board type locked at creation; no per-quest tagging; no rewards flow)
- `.planning/PROJECT.md` — Key Decisions table: `BoardType` dispatch uses C# switch expressions (matching `ShopService.CalculateItemPriceAsync`'s `ItemRarity` convention); reuse existing `QuestController`/`QuestService`/Areas, no new controller/Area

No external specs/ADRs beyond the project's own planning docs — requirements fully captured in decisions above.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `GroupEntity` (`QuestBoard.Repository/Entities/GroupEntity.cs`) — currently just `Id`, `Name`, `CreatedAt`, `UserGroups`. `BoardType` is a net-new column.
- `Group` domain model (`QuestBoard.Domain/Models/Group.cs`) — mirrors `GroupEntity` minus `UserGroups`; needs the new `BoardType` property added in parallel.
- `EntityProfile.cs` (`QuestBoard.Repository/Automapper/EntityProfile.cs:120-130`) — note this file lives in `QuestBoard.Repository`, not `QuestBoard.Domain` as CLAUDE.md's doc pointer states (stale doc reference, out of scope to fix here). The `UserGroupEntity ↔ UserGroup` mapping shows the established int↔enum `ForMember` pattern (`(GroupRole)src.GroupRole` / `(int)src.GroupRole`) to replicate for `BoardType` if it's stored as `int` on the entity.
- `GroupController` (`QuestBoard.Service/Areas/Platform/Controllers/GroupController.cs`) — `[Area("Platform")]`, `[Authorize(Policy = "SuperAdminOnly")]`. Create GET/POST (lines ~24-44) and Edit GET/POST (lines ~48-74) are the two actions this phase touches; both use `[ValidateAntiForgeryToken]` on POST.
- `GroupCreateViewModel` / `GroupEditViewModel` (`QuestBoard.Service/ViewModels/PlatformViewModels/`) — both currently only carry `Name` (+ `Id` on Edit). Per D-06, `GroupEditViewModel` should not carry a bindable `BoardType` that flows back into the entity.
- Enum-dropdown convention: `Html.GetEnumSelectList<TEnum>()` + `asp-items` — see `Members.cshtml:115-120` (`GroupRole`) and `Admin/CreateUser.cshtml`. Same idiom applies to `BoardType`.
- Badge rendering precedent: `Members.cshtml` renders `GroupRole` badges via manual if/else-if branching by enum value (no shared badge helper exists) — follow the same approach for `BoardType` badges on Index/Edit.
- Migration precedent for adding an int-backed enum column with a default value: `20260129155948_AddSignupRoleToPlayerSignup.cs` — `migrationBuilder.AddColumn<int>(name: "SignupRole", table: "PlayerSignups", type: "int", nullable: false, defaultValue: 0)`. This is the pattern to mirror for adding `BoardType` to `Groups` (defaulting existing rows to whichever ordinal is `OneShot`, satisfying success criterion 4).
- `ShopService.CalculateItemPriceAsync` (`QuestBoard.Domain/Services/ShopService.cs:49-62`) — the switch-expression convention already locked in for `BoardType` dispatch in later phases: `rarity switch { ItemRarity.Common => ..., _ => ... }`.

### Established Patterns
- Edit POST handlers only copy fields explicitly present on their ViewModel back onto the loaded entity (`group.Name = model.Name;` is the only assignment in `GroupController.Edit` POST) — this is the existing, implicit mechanism for protecting fields from being overwritten via Edit. No dedicated "locked field" pattern exists elsewhere in the codebase to copy instead.
- No `Details.cshtml` exists for Group — only `Index`, `Create`, `Edit`, `Delete`, `Members` (each with a `.Mobile.cshtml` counterpart). Any "read-only view" of a group's board type must live on Index or Edit, not a separate Details page.

### Integration Points
- `Areas/Platform/Views/Group/Create.cshtml` and `Edit.cshtml` (+ `.Mobile.cshtml` variants) — where the new dropdown/disabled-select fields are added.
- `Areas/Platform/Views/Group/Index.cshtml` (+ `.Mobile.cshtml`) — where the new Board Type badge column is added.
- New EF Core migration in `QuestBoard.Repository/Migrations/` adding the `BoardType` int column to `Groups` with `defaultValue: 0` (assuming `OneShot` is ordinal 0, per `PROJECT.md`'s `BoardType` enum / `Campaign`).

</code_context>

<specifics>
## Specific Ideas

- Permanence warning text example floated during discussion: "This cannot be changed after creation" (exact wording left to planner/implementer).
- No specific visual mockup given for the badge — follow existing badge conventions used for `GroupRole` in `Members.cshtml`.

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope.

</deferred>

---

*Phase: 35-board-type-configuration*
*Context gathered: 2026-07-03*
