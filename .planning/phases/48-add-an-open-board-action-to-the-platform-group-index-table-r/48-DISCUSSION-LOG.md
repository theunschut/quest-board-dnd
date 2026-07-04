# Phase 48: Add an Open Board action to the /platform group index table - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-07-04
**Phase:** 48-add-an-open-board-action-to-the-platform-group-index-table-r
**Areas discussed:** Click behavior & destination, Return path to Platform, Mobile parity, Button styling

---

## Click behavior & destination

| Option | Description | Selected |
|--------|-------------|----------|
| Reuse SelectGroup as-is | POST to the existing `GroupPickerController.SelectGroup` action — sets `ActiveGroupId`/`ActiveGroupName` in session, redirects to `Quest/Index`. Zero new controller code. | ✓ |
| New dedicated Platform action | Add a new action in the Platform area's `GroupController` that duplicates the same session-setting + redirect logic. | |

**User's choice:** Reuse SelectGroup as-is
**Notes:** Matches the user's original framing of "same functionality as the GroupPicker" literally — reuse, not reimplementation.

---

## Return path to Platform

| Option | Description | Selected |
|--------|-------------|----------|
| Leave as-is | Existing 2-click path (user menu → Switch Group → GroupPicker's "Go to Platform" button) is accepted as sufficient; no nav changes. | ✓ |
| Add a nav shortcut back to Platform | Add a persistent "Platform" link/dropdown item visible only to SuperAdmins in the main nav. | |

**User's choice:** Leave as-is
**Notes:** Surfaced because today there is no direct nav link back to `/platform` from inside a group's board — confirmed via `_Layout.cshtml` grep showing zero "Platform" references outside `GroupPicker/Index.cshtml`. User accepted the existing indirect path rather than expanding this phase's footprint into shared nav layouts.

---

## Mobile parity

| Option | Description | Selected |
|--------|-------------|----------|
| Yes, add to both | Add Open Board to both `Index.cshtml` (desktop) and `Index.Mobile.cshtml`. | ✓ |
| Desktop only | Add only to the desktop view for now. | |

**User's choice:** Yes, add to both
**Notes:** Consistent with the project's active mobile-parity focus (Phase 43 in the same milestone).

---

## Button styling

| Option | Description | Selected |
|--------|-------------|----------|
| btn-primary + fa-door-open | Primary blue as the leftmost/main action; door-open icon reads as "enter". | |
| btn-primary + fa-dice-d20 | Ties visually to the One-Shot board-type badge icon already in the table. | |
| You decide | Leave exact color/icon to Claude's discretion. | ✓ |

**User's choice:** You decide
**Notes:** Recorded as Claude's Discretion in CONTEXT.md, defaulting to `btn-primary` + `fa-door-open`.

---

## Claude's Discretion

- Exact icon/color for the "Open Board" button (default: `btn-primary` + `fa-door-open`).
- Whether the button is shown for empty (0-member) groups — default: always shown, no member-count gate.

## Deferred Ideas

- Persistent nav-level shortcut back to `/platform` for SuperAdmins — deferred, see "Return path to Platform" above.
