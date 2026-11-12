# Phase 97: Events, Series, Calendar and Agenda UI conversion - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-11-12
**Phase:** 97-events-series-calendar-and-agenda-ui-conversion
**Areas discussed:** "+N more" confirmation, Linked-board entries, Phase 95 handovers, Gates and mockups

Carried forward without asking: the G1 answers Q-40 to Q-49, Q-115, Q-116, Q-133 and Q-134; no
merges; the process decisions of Phases 95 and 96 (hybrid sign-off, two gates, iterate until
approved, re-captured BEFORE on the Linux machine, opening checkpoint, slices with one release).
The operator was sent the calendar-fit recording and the Direction A calendar render before the
questions.

---

## "+N more" confirmation (G1-O3)

| Question | Options | Selected |
|----------|---------|----------|
| How the day panel opens | Click/tap/Enter only / Also on hover (350ms) / Try the mockup first | Click/tap/Enter only |
| 20px compact rows vs the 24px target | Keep 20px, record exception / Go to 24px, fewer entries | Keep 20px, record exception |
| Footer at 720px | Footer below the fold / Footer must stay visible | Footer below the fold |
| Title truncation (about 13 and 16 characters) | Accept, card shows full / Drop the time to fit more title / You decide | You decide |

---

## Linked-board entries

| Question | Options | Selected |
|----------|---------|----------|
| Non-member linked entry for keyboard users | Stays inert, hover card only / Focusable, non-link | Stays inert, hover card only |
| Member linked entry | Yes, as today / Discuss | Yes, as today |
| Desktop calendar below about 992px | Cells grow, no fit rule / Switch to the agenda list / You decide | Cells grow, no fit rule |

---

## Phase 95 handovers

| Question | Options | Selected |
|----------|---------|----------|
| Quest Details weekday header (3.76:1 exception) | Fix it, retire the exception / Keep the exception | Fix it, retire the exception |
| About 18 direct `.ToString` date renderings | Move to Html.WallClock, same output / Leave as-is / You decide | Move to Html.WallClock, same output (new format entries may be added; existing behaviour untouched) |
| Ambient-clock guard for the partials | Yes, guard the partials / Only the test for the fix | Yes, guard the partials |

---

## Gates and mockups

| Question | Options | Selected |
|----------|---------|----------|
| Gate 1 contents (multi-select) | Events/Details reorder / Calendar port with real data / Truncation tuning | All three |
| Agenda/Index merge | Re-check, propose if it qualifies / Keep the twins | Re-check, propose if it qualifies |
| Slice order | Shared partials first / Calendar first / You decide | You decide |

---

## Claude's Discretion

- Compact-entry title truncation (shown at Gate 1)
- Slice order, boundaries and plan count
- How the slot count reaches the script; module structure for the calendar and series scripts
- How the board's today reaches the partials (from `IBoardClock`)
- Names of the format entries added for the WallClock move
- Quest Details' vote calendar keeps Phase 95's layout; "+N more" is for Calendar/Index only

## Deferred Ideas

None.
