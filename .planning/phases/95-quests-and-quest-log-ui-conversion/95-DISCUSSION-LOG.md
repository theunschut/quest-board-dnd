# Phase 95: Quests and Quest Log UI conversion - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-11-10
**Phase:** 95-quests-and-quest-log-ui-conversion
**Areas discussed:** Poster card relayout, Editor Tab behaviour, Phone signup pickers, Twin wording, Relayout sign-off flow, New merge candidates, Baseline on this machine, Delivery slicing

---

## Poster card relayout

The first question set (info prominence, seals, text-zone presentation, empty board) was set aside
when the operator clarified the intent in free text: the board is a fantasy notice board players
take quests from; all posters should be shown, each with padding that fits its holes and torn
edges; ruined posters are a nice-to-have; nobody reads the description on the poster, but a large
poster with little on it looks wrong; randomness makes it a living board; keep the seals instead of
a text status because a seal is easy to spot.

| Question | Options | Selected |
|----------|---------|----------|
| Description on the poster | Short teaser, sized to fit / Drop it entirely | Drop it entirely, and also retire the tall Poster3; all others random; show it in the mockup (free text) |
| Randomness | Stable per quest / Reshuffle each visit | Stable per quest |
| Ruined posters | Mixed into the rotation / Stretch goal | Mixed into the rotation |
| Seals | Seals only / Seals + small word | Seals only |
| Info after the title | Date / Adventurer count / Let the mockup show both | Let the mockup show both |
| Text zone | 2-3 strengths, switcher / One, the minimum | 2-3 strengths, switcher |
| Empty board (first pass) | Blank poster notice / Glass card like the phone | Free text: just the empty board, since the background is a quest board |
| Empty board (follow-up) | Bare board + hidden text / Bare board + one small line / Bare board, nothing else | Bare board + hidden text |
| WebP | Add WebP, keep PNG / Keep PNG only | Add WebP, keep PNG |

**Notes:** Poster2 is not really tall; its tail adds height but its content area is small. Many
ruined posters may not have enough room; the operator left that to the research and mockup step.

---

## Editor Tab behaviour

| Question | Options | Selected |
|----------|---------|----------|
| Keyboard trap fix | Tab leaves the editor / Tab indents, Esc then Tab leaves | Tab leaves the editor |
| Markdown hint | Visible hint under field / Focusable info button | Visible hint under field |

---

## Phone signup pickers

| Question | Options | Selected |
|----------|---------|----------|
| Control type | Plain selects, like desktop / Character modal + role select | Plain selects, like desktop |
| Spectator hides voting | Same as desktop / Keep voting visible | Same as desktop |

---

## Twin wording

| Question | Options | Selected |
|----------|---------|----------|
| Length-only label differences | One wording, full form / Keep phone abbreviations | One wording, full form |
| Cancel vs Back | 'Cancel' on both / Keep both wordings | 'Cancel' on both |

---

## Relayout sign-off flow

| Question | Options | Selected |
|----------|---------|----------|
| Draw first? | Hybrid / Mockup everything first / Build all, confirm after | Hybrid |
| Gates | Two gates / One gate per page | Two gates |
| If rejected | Converted, old layout / Iterate until approved | Iterate until approved |

---

## New merge candidates

| Question | Options | Selected |
|----------|---------|----------|
| Move/Index, Quest/Edit, Quest/CreateFollowUp | Bring proposals to a gate / Keep their twins | Bring proposals to a gate |

---

## Baseline on this machine

| Question | Options | Selected |
|----------|---------|----------|
| BEFORE screenshots and renders | Re-capture here + copy renders / Re-capture everything here / Run Phase 95 on the other machine | Re-capture here + copy renders |
| Accounts and fixtures | Checkpoint, like 94-01 / Already set up here | Checkpoint, like 94-01 |

---

## Delivery slicing

| Question | Options | Selected |
|----------|---------|----------|
| Release shape | Slice merges, one release / Release per slice | Slice merges, one release |

---

## Claude's Discretion

- Storage of per-poster text areas and the server-side fit estimate
- Poster hover behaviour (transform only, none under reduced motion)
- Stretched-link pattern for whole-card links
- Moving remaining `.ToString` dates onto the date helpers
- Script module structure; slice boundaries and plan count

## Deferred Ideas

None.
