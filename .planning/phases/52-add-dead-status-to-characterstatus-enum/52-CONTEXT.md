# Phase 52: Add Dead status to CharacterStatus enum - Context

**Gathered:** 2026-07-06
**Status:** Ready for planning

<domain>
## Phase Boundary

Add a third `Dead` value to the `CharacterStatus` enum (currently `Active`/`Retired` only) and wire it through every surface that already displays or edits character status: Create/Edit dropdowns, Details/Index badges (desktop + mobile), the retire/reactivate toggle, and list sorting. No new controller actions, no new pages — this extends an existing status field, it does not add a new capability.

</domain>

<decisions>
## Implementation Decisions

### Setting the status
- **D-01:** No dedicated "Mark as Dead" action/button. `Dead` is set exactly like `Retired` is today — via the existing Status `<select>` dropdown on the Create and Edit character forms (`Html.GetEnumSelectList<CharacterStatus>()` picks up the new enum value automatically, no markup change needed on those two views).
- **D-02:** `Dead` is selectable on the **Create** character form too (not creation-restricted) — same dropdown, same as Edit. A user may want to log an already-deceased legacy character.

### Retire/Reactivate toggle button
- **D-03:** The existing `ToggleRetirement` action/button (`character.Status == Active ? Retired : Active`) is a binary flip and would silently **revive** a `Dead` character to `Active` if clicked, since `Dead != Active`. Fix: **hide the toggle button entirely** when `Status == Dead` (Details.cshtml and Details.Mobile.cshtml). Once a character is Dead, the only way to change their status is the Edit form's dropdown — no quick-toggle path back to Active.
- Do NOT make the toggle tri-state/cyclical — it stays a simple Active↔Retired flip, just gated off for Dead.

### Visual treatment
- **D-04:** Dead gets its own distinct styling, not reused from Retired:
  - Badge: dark badge (`bg-dark`) with a skull icon (`fa-skull`), replacing the `bg-secondary` + `fa-moon` "Retired" treatment — applies everywhere the Retired badge currently appears (Details.cshtml, Details.Mobile.cshtml, Index.cshtml card grid, Index.Mobile.cshtml list rows).
  - Card/row class: new `character-dead` CSS class (parallel to the existing `character-retired` class) — exact dimmed/grayscale treatment left to implementation, but it must be visually distinguishable from the Retired treatment at a glance.

### Sorting
- **D-05:** No new sort tier. Guild Members list keeps its current two-bucket sort (`Active` first, everything else alphabetically after) — `Dead` falls into the same "not Active" bucket as `Retired` today. Do not introduce a three-tier Active→Retired→Dead comparator.

### Quest signup (confirmed, not a gray area — no code change)
- `QuestController` already filters eligible signup characters with `Status == Active` (three call sites: character dropdown population, and two signup/vote validation checks). This is an equality check against `Active`, not an inequality against `Retired`, so `Dead` characters are automatically excluded from quest signup with zero code changes. Confirmed as correct existing behavior — do not touch these call sites beyond what's naturally needed.

### Claude's Discretion
- Exact grayscale/dimming intensity for the `character-dead` CSS class (D-04) — visually distinct from Retired is the only hard requirement.
- Whether `character-dead` reuses the existing `.character-retired` selector's structure/specificity or is written fresh — implementation detail.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

No external specs, ADRs, or PRDs exist for this phase — it originated as a one-line backlog title in `.planning/ROADMAP.md` (Phase 52) with no elaboration. All scope and behavior decisions above were derived from codebase inspection during this discussion and confirmed with the user.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `Html.GetEnumSelectList<CharacterStatus>()` in `Create.cshtml` and `Edit.cshtml` (+ Mobile variants) — auto-populates from the enum, so adding `Dead = 2` to `QuestBoard.Domain/Enums/CharacterStatus.cs` requires zero markup changes to the dropdowns themselves.
- `EntityProfileEnumCastTests.CharacterStatus_CastRoundTrips` (`QuestBoard.UnitTests/Services/EntityProfileEnumCastTests.cs`) iterates `Enum.GetValues<CharacterStatus>()` dynamically — automatically covers the new `Dead` value with zero test changes.
- `.retired-badge` / `character-retired` CSS classes — direct pattern to mirror for the new dead-specific badge/class (D-04).

### Established Patterns
- `CharacterEntity.Status` is stored as a plain `int` column (`QuestBoard.Repository/Entities/CharacterEntity.cs:23`), mapped via `(int)`/`(CharacterStatus)` casts in `EntityProfile.cs`. Adding an enum member is purely additive — **no EF Core migration needed**.
- Status badge rendering today is a binary `if/else` (Retired vs. Active) in 4 view files (Details.cshtml, Details.Mobile.cshtml, Index.cshtml, Index.Mobile.cshtml) — each needs a third branch for Dead.

### Integration Points
- `GuildMembersController.ToggleRetirement` (`QuestBoard.Service/Controllers/Characters/GuildMembersController.cs:276-297`) — needs the button-hide guard (D-03); the action method itself can stay as-is since the button that calls it will simply not render for Dead characters.
- `GuildMembersController.Index` sort (`ThenByDescending(c => c.Status == CharacterStatus.Active)`) — confirmed to need no change per D-05.
- `QuestController` character-eligibility checks (lines ~322, ~402, ~455, ~545) — confirmed correct as-is, no change needed.

</code_context>

<specifics>
## Specific Ideas

- Skull icon (`fa-skull`) + dark badge (`bg-dark`) for the Dead badge, explicitly called out by the user as the preferred icon/color pairing (vs. reusing Retired's moon/gray).

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope.

</deferred>

---

*Phase: 52-add-dead-status-to-characterstatus-enum*
*Context gathered: 2026-07-06*
