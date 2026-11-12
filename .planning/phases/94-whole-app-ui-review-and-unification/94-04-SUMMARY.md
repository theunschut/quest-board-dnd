---
phase: 94-whole-app-ui-review-and-unification
plan: 04
subsystem: ui-audit
tags: [audit, events, calendar, agenda, series, twin-comparison, contrast, accessibility]
requires:
  - phase: 94-02
    provides: "manifest, fixtures, frozen baseline on port 8001, BEFORE capture and report, contrast tool"
provides:
  - "94-AUDIT-events.md: Events, Series, Calendar and Agenda area audit with 51 findings, twin comparison, 10 operator questions, 2 relayout proposals, empty-state copy and area-phase scope notes"
affects: [94-AUDIT index plan, Events area phase, operator sign-off batch]
tech-stack:
  added: []
  patterns: ["live probes written under the audit scratch folder and run against the frozen baseline only", "contrast pairs computed with tools/contrast.mjs over B-med, B-99 and B-max"]
key-files:
  created:
    - .planning/phases/94-whole-app-ui-review-and-unification/94-AUDIT-events.md
  modified: []
decisions:
  - "Disclosure toggles that reveal the same content (roster, filter, legend toggle) are classed as layout differences; a heading, label, key or icon on one side only is a content difference and an operator question"
  - "No merge proposal: Calendar and Events/Index are content-differs; Agenda is layout-only but its mobile markup differs, so it is not CSS-only"
  - "Findings are numbered in severity order (S1 first) rather than by page"
metrics:
  duration: "one session"
  completed: 2026-11-01
status: complete
actuals:
  tokens: 13000
  tasks: 2
  commits: 1
---

# Phase 94 Plan 04: Events Area Audit Summary

One audit file for the Events, Series, Calendar and Agenda area: 51 findings in the fixed format, index rows for the seven views, a twin comparison from extracted inventories, ten operator questions in the standard wording, two relayout proposals and no merge proposals.

## What was produced

- `94-AUDIT-events.md` with the nine required sections (Scope, Index, Findings, Twin comparison, Operator questions, Merge proposals, Relayout proposals, Empty-state copy, Area-phase scope notes), plus per-stylesheet count lines for all six area stylesheets and an A-11 coverage list.
- Scratch only, under `~/qb-ui-review/phase-94/audit/events/`: `probe1..4.mjs` and their JSON output, `contrast-events.mjs` and `.txt`, `digest.py` and the digests, DM-view and recurrence screenshots. Nothing in the repository changed apart from the report and this summary.

## Headline results

- S1 (3): Events/Index desktop shows its attendance counts and date lines as cream on cream paper (1.04 to 1.06:1, from `.modern-card span {color:cream !important}` beating the cell colour); the shared availability badges paint cream labels, so the "Maybe" label is 1.29:1 on amber; the mobile Agenda and Events/Index page subtitle is dark ink on the bare board (1.11:1).
- S2 (23): in-card button fills below 4.5:1, calendar grid text 2.99 to 4.34 at B-max, 8 to 10px chip text, hover-only DM and time reveal that keyboard focus does not trigger, status by colour alone, unnamed month-navigation chevrons on desktop, the recurrence checkbox label pointing at a non-existent id, a `DateTime.Today` read in `_Calendar.cshtml` that is outside the ambient-clock guard list, 320px overflow on the desktop calendar header, a 4px overflow on the mobile Events/Details caused by a bare `.row`, cream focus ring on the cream paper row link, outline buttons that read as disabled.
- Twin comparison: Calendar/Index is content-differs (8 items, Q1 to Q8), Events/Index is content-differs (Q9, Q10), Agenda/Index is layout-only; the four twin-less views are recorded as "none (served under both)".
- Corrections to the brief: no view in this area uses `Html.WallClock` or `Html.LocalTime`, so there is no helper markup to preserve here; the seeded brand and footer questions belong to the layout audit.

## Deviations from Plan

### Auto-fixed Issues

None in code; the plan changes no application code.

### Notes

**1. [Judgment] Both tasks landed in one commit**
- The plan's Task 1 (Calendar Index in the final format) and Task 2 (the rest) write the same file; the file was built with Calendar Index first and completed in one pass, so there is one task commit (`ff37ba2f`) for both. The tracer's own verify (nine sections, index row, A-events-001, twin comparison) and Task 2's verify were both run and pass.

**2. [Judgment] Numbering**
- IDs run in severity order, so A-events-001 is the S1 Events/Index finding, not a Calendar one. Calendar Index is covered by findings 008 to 018, 022, 028, 029, 037 and 039 to 046.

**3. [Scope] Extra captures**
- DM views of Events/Details and a recurrence-on view of Events/Create were captured as scratch files (they are not in the BEFORE record, which used the Player role for Details); nothing in the BEFORE record or the shared tools was rewritten.

## Auth gates

None.

## Known Stubs

None (no source changed).

## Threat Flags

None new. The report names fixtures by id, quotes no member name, email or user-entered text, references screenshots by scratch path only, and the acceptance check for real mail domains passes (T-94-08 mitigated).

## Task commits

- `ff37ba2f` docs(94-04): audit Events, Series, Calendar and Agenda area (Tasks 1 and 2)

## Self-Check: PASSED

- File exists: `.planning/phases/94-whole-app-ui-review-and-unification/94-AUDIT-events.md`.
- Commit `ff37ba2f` exists on the worktree branch.
- Task 1 verify (sections `## Index` and `## Twin comparison`, an `A-events-nnn` ID, `Calendar/Index`) passes; Task 2 verify (all six view paths, `none (served under both)`, no real mail domain) passes.
- `git diff --name-only` for the plan lists only the report (plus this summary).
