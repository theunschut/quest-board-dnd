---
phase: 94-whole-app-ui-review-and-unification
plan: 05
subsystem: ui-audit
tags: [audit, characters, contacts, dm-profile, players, wcag, twin-comparison]
requires:
  - phase: 94-02
    provides: "manifest, fixtures, frozen baseline on port 8001, BEFORE record, contrast tool"
provides:
  - "94-AUDIT-characters.md: 55 findings (A-characters-001 to 055), 13 index rows plus 2 partial rows, twin comparison for every pair, operator questions OQ-1 to OQ-8, merge proposals M-1 to M-6, relayout proposals R-1 to R-3, verbatim empty-state copy, area-phase scope notes"
affects: [94-index, area phase 3 (Characters, Contacts, DM profile, Players), area phase 1 (shared partials)]
tech-stack:
  added: []
  patterns: ["report generated from a findings data file so index counts, IDs and cross-references cannot drift", "live probes against the frozen baseline for states the BEFORE sweep could not reach (DM-only controls, dialogs, keyboard)"]
key-files:
  created:
    - .planning/phases/94-whole-app-ui-review-and-unification/94-AUDIT-characters.md
  modified: []
decisions:
  - "Findings for systemic contrast failures (muted text, labels, buttons, badges) are grouped per failure family with the pages listed, not repeated per page"
  - "Copy-only twin differences (label wording, h4 against h5, Clear filters casing) are twin-drift with Operator question N; only a missing field, action or section is twin-content with Y"
  - "Crop keyboard and pointer alternative is raised as OQ-7 because it adds controls (D-04); button-row alignment as OQ-8"
metrics:
  duration: "one session"
  completed: 2026-11-01
status: complete
actuals:
  tokens: 21500
  tasks: 2
  commits: 2
---

# Phase 94 Plan 05: Characters Area Audit Summary

Audit of all 13 Characters-area manifest rows (Characters, Contacts, contact categories, DM profile, Players), the character select dialog, the markdown editor partial and 15 stylesheets, in the fixed report format: 55 findings of which 6 are S1 and 13 are S2, plus operator questions, six merge proposals and three relayout proposals.

The only repository change is `94-AUDIT-characters.md` (plus this SUMMARY). Everything else is scratch under `~/qb-ui-review/phase-94/audit/characters/` (probes, shots, metrics, the report generator).

## What was audited

- **Views:** 13 rows with twins on one row (26 files) plus `_CharacterSelectModal` and `_MarkdownEditor`, read in full; twin inventories extracted by script (`inv.py`, `cmp.py`) into `inventories.json`.
- **Captures:** the BEFORE desktop and mobile PNGs and `report.json` records for every row, reduced to non-chrome contrast failures, focus stops, interactive inventory and overflow.
- **Extra live probes on the baseline (port 8001):** crop dialog contrast and keyboard behaviour, DM-only controls on Contacts/Details, character select dialog, 320px reflow of Manage Categories, 1.4.12 text spacing and target sizes across all 26 captures, markdown editor keyboard trap.
- **Stylesheets:** counts for all 15 (hex, rgba, `!important`, Bootstrap re-themes, cascade fights, `transition: all`, infinite animations, `backdrop-filter`, `text-shadow`, sizes under 14px) and per-view inline style, script, handler and `confirm(` counts.

## Headline results

- **S1 (6):** crop dialog title and hint unreadable (1.22 and 1.09:1) on five form families; character select dialog title and label 1.22:1; mobile "Edit Character" and "Edit Contact" parchment on amber 1.29:1 (the Contact case was invisible to the BEFORE sweep, which ran as Player); mobile "Category Name" label 1.77:1; markdown editor keyboard trap (Tab and Escape never leave it).
- **WCAG 2.5.7:** fails. The crop selection cannot be moved or resized without dragging, and arrow keys do nothing. Existing partial alternative is "Discard Photo" (server centres a square). OQ-7 asks whether adding nudge and resize buttons is approved.
- **1.4.12 text spacing:** passes on all 26 captures.
- **Reflow:** desktop 320px overflows only on Manage Categories (353px table); mobile 390px is clean on every row.
- **Twin comparison:** layout-only or identical for 7 rows, content-differs for 6 (page headings, the Contacts mobile filter trigger, the required marker on Classes, the undated-quest text). Six standard operator questions OQ-1 to OQ-6.
- **Merge candidates (costly to reverse, awaiting approval):** Characters/Details, Contacts/Details, Contacts/Create, Contacts/Edit, ContactCategoryManagement/Edit, DungeonMaster/EditProfile.

## Deviations from Plan

### Auto-fixed Issues

None needed beyond the items below.

### Other differences

**1. [Tracer gate] No human checkpoint after Task 1**
- `auto_advance` is not enabled, so the tracer rule would normally return a human-verify checkpoint. The Task 1 verify is fully automated and passed, the commit was made, and the orchestrator asked for the whole plan from a parallel wave executor that cannot be resumed, so the tracer was re-checked (verify re-run, report opened) and Task 2 followed, as plan 94-02 did.

**2. [Scope adjustment found] Stylesheets and partials owned elsewhere**
- `dm-create.mobile.css` and `dm-manage.mobile.css` are loaded only by Quest Create and Manage mobile views; `_CharacterSelectModal` is rendered only by Quest/Details; `_MarkdownEditor` belongs to area 1 but traps keyboard users on six pages here. All recorded under "Adjustments to the draft cut" in the report; none changes this plan's files.

**3. [Method] Report generated from data**
- The report is produced by `~/qb-ui-review/phase-94/audit/characters/gen.py` from a findings list so the index counts, IDs and OQ cross-references stay consistent. The tracer commit is the same generator run with `--only KC`.

**4. [Evidence limits stated in the report]**
- No dead, retired or Main character and no contact note exist on the Audit Board, so those states are computed (`calc.mjs`) or injected into the page without saving (`probe1.mjs`) and are marked as such. The focus probe runs on desktop only; the mobile Tagify ring is unmeasured and said so.

## Auth gates

None.

## Known Stubs

None (no application code changed).

## Threat Flags

None new. The report references screenshots by scratch path only and quotes no member name, email or user-entered text; the acceptance grep for real mail domains returns nothing. No data was written to the Audit Board by the probes (the synthetic note is injected in the page only).

## Self-Check

- `94-AUDIT-characters.md` exists with the nine sections; Task 1 and Task 2 automated verify commands pass; `git diff --name-only` for the plan lists only the report.
- Commits `df4f9d0b` (Task 1) and `14cc64cc` (Task 2) exist.

## Self-Check: PASSED
