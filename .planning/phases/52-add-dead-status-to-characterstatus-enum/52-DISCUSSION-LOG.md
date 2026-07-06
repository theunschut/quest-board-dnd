# Phase 52: Add Dead status to CharacterStatus enum - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-07-06
**Phase:** 52-add-dead-status-to-characterstatus-enum
**Areas discussed:** Set Dead flow, Toggle button fix, Creation option, Visual style, Sort order

---

## Set Dead flow

| Option | Description | Selected |
|--------|-------------|----------|
| Dedicated 'Mark as Dead' button | Add a distinct button/action next to 'Retire Character' with a confirm dialog, since character death is usually deliberate/significant. | |
| Edit dropdown only | Adding 'Dead' to the enum auto-populates the existing Status dropdown on Create/Edit — no new UI needed. | ✓ |

**User's choice:** Edit dropdown only
**Notes:** No dedicated action — reuse the existing Status select on Create/Edit forms.

---

## Toggle button fix

| Option | Description | Selected |
|--------|-------------|----------|
| Hide toggle button when Dead | Only Edit can change a Dead character's status; quick-toggle button disappears once Dead, preventing accidental revival. | ✓ |
| Make toggle tri-state aware | Button logic becomes explicit per-status instead of a binary flip. | |

**User's choice:** Hide toggle button when Dead
**Notes:** Prevents the existing `Status == Active ? Retired : Active` binary flip from silently reviving a Dead character to Active.

---

## Creation option

| Option | Description | Selected |
|--------|-------------|----------|
| Exclude from Create | Create form offers Active/Retired only; Dead becomes available later via Edit. | |
| Allow at creation too | Keep Create's dropdown using the same full enum list as Edit, no special-casing. | ✓ |

**User's choice:** Allow at creation too
**Notes:** Useful for logging a legacy/deceased character retroactively.

---

## Visual style

| Option | Description | Selected |
|--------|-------------|----------|
| Dark badge + skull icon | bg-dark badge with fa-skull, paired with a 'character-dead' card class, clearly distinct from Retired's bg-secondary + moon. | ✓ |
| Same treatment as Retired | Reuse the existing gray badge/moon-icon style and character-retired CSS class, just with 'Dead' text. | |
| Let me describe something specific | — | |

**User's choice:** Dark badge + skull icon
**Notes:** Applies everywhere the Retired badge/class currently appears (Details, Details.Mobile, Index, Index.Mobile).

---

## Sort order

| Option | Description | Selected |
|--------|-------------|----------|
| Same bucket as Retired | Active first, then everyone else (Retired + Dead together) alphabetically — no new sort tier. | ✓ |
| Dead sorts last | Three-tier sort: Active, then Retired, then Dead — requires a tri-state comparator. | |

**User's choice:** Same bucket as Retired
**Notes:** Keeps the existing boolean `ThenByDescending(c => c.Status == CharacterStatus.Active)` sort unchanged.

---

## Claude's Discretion

- Exact grayscale/dimming intensity for the new `character-dead` CSS class — must be visually distinct from Retired, specifics left to implementation.
- Whether `character-dead` reuses `.character-retired`'s structure/specificity or is written fresh.

## Deferred Ideas

None — discussion stayed within phase scope.
