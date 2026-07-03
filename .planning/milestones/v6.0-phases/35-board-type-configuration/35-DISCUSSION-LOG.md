# Phase 35: Board Type Configuration - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-07-03
**Phase:** 35-board-type-configuration
**Areas discussed:** Create-form selector, Locked display on Edit, Group list visibility, Default & permanence warning

---

## Create-form selector

| Option | Description | Selected |
|--------|-------------|----------|
| Dropdown (select) | Matches existing app convention (`GroupRole` via `Html.GetEnumSelectList<T>()`); fastest to build, consistent look | ✓ |
| Radio buttons | Better suited to a binary, permanent, high-stakes choice; no existing pattern in this app | |
| Toggle cards | Two clickable panels with descriptions; most visual but no existing card-selector pattern | |

**User's choice:** Dropdown (select)
**Notes:** —

| Option | Description | Selected |
|--------|-------------|----------|
| Yes, helper text under the dropdown | Short static line explaining what One-Shot vs Campaign means | |
| No extra copy | Just the field label and enum option names | ✓ |

**User's choice:** No extra copy

---

## Locked display on Edit

| Option | Description | Selected |
|--------|-------------|----------|
| Plain text label | Static "Board Type: One-Shot" text, no form control at all | |
| Disabled/greyed select | `<select>` showing current value but disabled; visually consistent with Create's dropdown | ✓ |
| Badge/pill | Small colored badge next to group name/header | |

**User's choice:** Disabled/greyed select

| Option | Description | Selected |
|--------|-------------|----------|
| Silent no-op | Server never reads/binds BoardType from Edit POST; matches existing pattern of only copying `model.Name` | |
| Silent no-op + toast | Same server behavior, generic "Group updated" message shown | |

**User's choice:** "your choice" (deferred to Claude) — see Claude's Discretion below.

---

## Group list visibility

| Option | Description | Selected |
|--------|-------------|----------|
| Yes, badge in the list | Small badge/pill per row in Index.cshtml, similar to GroupRole badges in Members.cshtml | ✓ |
| No, Edit-only | Board type only visible by opening Edit; Index unchanged | |

**User's choice:** Yes, badge in the list

| Option | Description | Selected |
|--------|-------------|----------|
| Own column | New "Board Type" column between Name and Members (or Members and Created) | ✓ |
| Inline next to Name | Small badge in the same cell as group Name | |

**User's choice:** Own column
**Notes:** User first asked "which index.cshtml are we talking about here?" — clarified as `Areas/Platform/Views/Group/Index.cshtml` (the SuperAdmin group list in the Platform admin area), not a player-facing view. Question was then re-asked and answered.

---

## Default & permanence warning

| Option | Description | Selected |
|--------|-------------|----------|
| Yes, pre-select One-Shot | Matches BOARD-02's own default for existing groups; common case is path of least resistance | |
| No default (force a choice) | Dropdown starts blank/placeholder; SuperAdmin must actively pick one | ✓ |

**User's choice:** No default (force a choice)

| Option | Description | Selected |
|--------|-------------|----------|
| Yes, show the warning | Small muted text under the field stating the choice is permanent | ✓ |
| No warning | Rely on the field label alone | |

**User's choice:** Yes, show the warning

---

## Claude's Discretion

- **Tamper-handling on Edit POST:** User deferred ("your choice") between "Silent no-op" and "Silent no-op + toast." Chose plain silent no-op — the server should never read/bind `BoardType` from the posted `GroupEditViewModel`, and the existing "Group name updated." success message is left unchanged since nothing legitimate was blocked.
- **Badge styling:** Exact colors/CSS for the board type badge (on both Index and Edit) left to the planner, following whatever badge convention already exists for `GroupRole` in `Members.cshtml`.
- **Column ordering on Index.cshtml:** Whether "Board Type" sits between Name/Members or Members/Created is left to the planner.

## Deferred Ideas

None — discussion stayed within phase scope.
