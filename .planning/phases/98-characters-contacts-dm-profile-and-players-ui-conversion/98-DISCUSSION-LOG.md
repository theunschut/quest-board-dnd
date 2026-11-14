# Phase 98: Characters, Contacts, DM profile and Players UI conversion - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-11-13
**Phase:** 98-characters-contacts-dm-profile-and-players-ui-conversion
**Areas discussed:** Crop dialog controls, Character picture 404, Notes panel surface, Fixtures, Gate 1 contents, Characters Create/Edit merge, Main badge animation, Branch

Not asked, because they were already answered at G1 (2026-11-01) in `94-AUDIT.md`: Q-50 to Q-55, Q-97 to Q-102, Q-117 to Q-119, Q-132 (approve the addition) and Q-135 (Quests phase).

---

## Crop dialog controls

| Option | Description | Selected |
|--------|-------------|----------|
| Buttons + arrow keys | Control row (move four ways, grow, shrink, Reset) plus arrow keys on the focused selection, Shift+arrow resizes; Gate 1 mockup | ✓ |
| Buttons only | Same row, no keyboard handling on the selection | |
| Arrow keys + zoom slider | Keyboard move plus one size slider | |

**User's choice:** Buttons + arrow keys

## Character picture 404 (Phase 95 Gate 2 item 9, WINDOWS #31)

| Option | Description | Selected |
|--------|-------------|----------|
| Server-side placeholder | Views render the placeholder when there is no picture; no request, no inline onerror | ✓ |
| Controller serves a placeholder | GetCroppedPicture answers a default image | |
| Both | Views skip the request and the controller also answers a placeholder | |

**User's choice:** Server-side placeholder

## Contacts/Details notes panel surface

| Option | Description | Selected |
|--------|-------------|----------|
| Direction A glass card | Same opaque-scrimmed glass card on both layouts | ✓ |
| Parchment note slips | Each note on a parchment slip inside the card | |
| Decide at Gate 1 | Draw both | |

**User's choice:** Direction A glass card

## Fixtures

| Option | Description | Selected |
|--------|-------------|----------|
| New p98 accounts + board | qb-audit-p98* accounts and P98-named boards via the app's forms; kept afterwards | ✓ |
| Same, removed after Gate 2 | Same fixtures, deleted at the end | |
| Reuse existing qb-audit-* accounts | Add data to the existing Audit Board accounts | |

**User's choice:** New p98 accounts + board

## Gate 1 contents

| Option | Description | Selected |
|--------|-------------|----------|
| Add a look check | Also one mockup per page type matched against the Direction A renders | ✓ |
| Required AFTERs only | Relayouts, crop dialog and notes panel only | |

**User's choice:** Add a look check

## Characters Create/Edit merge

| Option | Description | Selected |
|--------|-------------|----------|
| Re-check, propose at Gate 1 | Research checks the criteria; merges only on explicit yes | ✓ |
| Keep them as twins | No merge proposal this phase | |

**User's choice:** Re-check, propose at Gate 1

## Main badge animation

| Option | Description | Selected |
|--------|-------------|----------|
| Static, no animation | Gold glow kept as a still style | ✓ |
| Pulse a few times, then stop | 2-3 pulses, none under reduced motion | |

**User's choice:** Static, no animation

## Branch

| Option | Description | Selected |
|--------|-------------|----------|
| Slices on the milestone branch | In this checkout; one v6.3.0 release via PR | ✓ |
| Own worktree, feature/phase-98 | Parallel with Phase 99, merged back later | |

**User's choice:** Slices on the milestone branch

## Claude's Discretion

- How the "has picture" value reaches the views and the placeholder's look
- How the crop controls drive Cropper.js v2, the step size, and the module structure
- Ports, dead/retired character styling, sheet consolidation, slice order and plan count

## Deferred Ideas

None.
