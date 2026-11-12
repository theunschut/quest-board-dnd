---
phase: 94-whole-app-ui-review-and-unification
plan: 14
subsystem: ui-audit-index
tags: [audit, shell, layouts, toasts, cascade-layers, operator-questions, area-phase-cut]
requires:
  - phase: 94-02
    provides: "manifest, fixtures, frozen baseline on 8001, BEFORE record, capture, contrast and diff tools"
  - phase: 94-03..94-08
    provides: "the six area audits indexed here"
  - phase: 94-11
    provides: "self-hosted Bootstrap, Font Awesome and Cinzel and app.css, the base of the cascade probe"
provides:
  - "94-AUDIT-shell.md: 39 findings on the five layouts, both footers, the toasts and the backdrop, twin comparison, six operator questions, one relayout"
  - "94-AUDIT.md: index of 67 rows, totals (397 findings, 37 S1), all S1 findings, the cascade measurement, 138 numbered operator questions, relayout list with BEFORE and AFTER cells, 16 merge proposals, the proposed seven-phase area cut"
affects: [94-15, 94-16, 94-17 (G1 fills the mocked-page AFTER paths), area phases appended after sign-off]
tech-stack:
  added: []
  patterns: ["index generated from the area reports by scratch scripts so counts cannot drift", "cascade risk measured by a control copy plus a probe copy and per-element computed-style attribution"]
key-files:
  created:
    - .planning/phases/94-whole-app-ui-review-and-unification/94-AUDIT-shell.md
    - .planning/phases/94-whole-app-ui-review-and-unification/94-AUDIT.md
  modified: []
key-decisions:
  - "A control copy (untouched HEAD) was swept beside the probe copy so the layering effect is isolated from the Cinzel change plan 94-11 already accepted"
  - "Question duplicates are asked once: the GroupPicker footer (Q-shell-03 and Q-account-10) and OQ-QM-4 with OQ-QM-5, whose wording the Quests report repeated"
  - "Non-twin decisions (theme, editor host, dead files, posters, crop controls, today highlight, ownership, Shop functional breaks, the cut) are listed as their own group with two non-removal answers each"
  - "The proposed cut keeps the draft's seven phases, adjusts ownership of eleven items and proposes the order Foundation, 1, 5a, 2, 3, 4, 5b, 6"
metrics:
  duration: "one session"
  completed: 2026-11-01
status: complete
actuals:
  tokens: 120000
  tasks: 3
  commits: 3
---

# Phase 94 Plan 14: Shell Audit and Audit Index Summary

Shell audit (39 findings, none S1, 18 S2), a measured cascade probe showing that layering Bootstrap alone changes 63 of 118 captures by more than 0.1 percent outside the bar and footer, and every capture inside them, and one index file carrying 397 findings, 138 numbered operator questions, the relayout list, merge proposals and a seven-phase area cut.

## What was produced

- `94-AUDIT-shell.md`: 39 findings (a11y 17, twin-content 6, visual-bug 5, inconsistency 4, cascade-fight 3, perf 3, dead-code 1; S2 18, S3 19, S4 2) for the five layouts, both footers, `_Toasts` and the backdrop, from the BEFORE record plus read-only probes against the frozen baseline (navbar geometry per width and role, landmarks, settled focus rings, toasts injected into the live DOM only). Headline items: no skip link or labelled landmark anywhere; unnamed togglers and toast close buttons; a fixed bar 64px against a 56px offset that wraps to 76 to 99px between 992 and 1280px and covers the first content row; toasts over the Calendar link, the user menu and the phone menu button; error and warning toasts that auto-hide after five seconds; a 2.5 MB backdrop with a white page until it loads; footer text failing contrast on both layouts (3.63:1 desktop, 2.56:1 mobile); a settled focus ring on the bar of 1.02 to 1.05:1.
- `94-AUDIT.md`: Summary with all 37 S1 findings and the headline results, the 67-row Index (shell rows plus every row of the six area reports, every manifest view present), Totals by class and severity, the Cascade section, 138 operator questions, the Relayout list (29 page rows for 17 proposals), 16 merge proposals, the Dead dependencies link to `94-AUDIT-vendor.md` (not yet written by 94-13), and the Proposed area-phase cut (seven phases including the full Email templates phase).

## Cascade probe results (on scratch copies, port 8014, stopped afterwards)

- Probe: `git archive HEAD` copy with `@layer` plus two `layer(vendor)` imports in `app.css` and the Bootstrap and Font Awesome links removed from the five layouts. Control: an untouched second copy. 118 captures each; `$QBR/cascade-probe/{report.json,desktop,mobile,control-sweep}`, diffs and overlays beside them.
- Control against `before` differs only on 59 mobile captures (at most 0.52 percent: the Cinzel edges accepted in 94-11); desktop is exactly zero. So probe against control isolates the layering effect.
- All 118 captures change; 81 change outside the bar and footer; 63 by more than 0.1 percent of pixels. Causes, attributed per element by a computed-style snapshot: Bootstrap `!important` utilities in the vendor layer beat the legacy `!important` rules (`.text-muted` 28 rules, `.text-danger`, `.bg-dark`, `.p-2`, `.fs-6`, badge `text-dark`); two unlayered legacy rules win only because they are unlayered (`.navbar-nav .nav-link` padding, the bare `span` colour). Outcomes: footer opaque near-black with near-black text (1.22:1), Dungeon Master and Admin toggles red (1.05 to 2.19:1), muted text on glass 1.57 to 2.24:1, validation lines 1.21 to 1.93:1, nav links wider and wrapping; and a repair: the cream-on-cream attendance counts on Events/Index become readable.
- Part (b): 45 legacy `!important` colour or background rules naming a Bootstrap `text-*` or `bg-*` utility (28 `.text-muted`), regex stated and every file:line listed.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Control run added to the cascade probe**
- **Found during:** Task 2
- **Issue:** The plan diffs the probe against `before` (the frozen baseline), but the branch already self-hosts the vendor files and Cinzel (plan 94-11), so every mobile page differs for a reason unrelated to layering and the probe alone cannot be attributed.
- **Fix:** swept an unmodified copy of the same tree beside the probe and reported all three comparisons (probe against `before`, control against `before`, probe against control); also took a computed-style snapshot of both copies to name causes instead of guessing. No repository file is touched; scratch output is under `$QBR/cascade-probe/` and `$QBR/audit/shell/`.
- **Files modified:** none in the repository
- **Commit:** 7bdbd774

**2. [Rule 1 - Bug] BEFORE focus probe sampled mid-transition**
- **Found during:** Task 1
- **Issue:** the BEFORE record reports a cream 3px outline on every nav link, but the links carry `transition: all 0.3s`, so the probe sampled the start of the transition.
- **Fix:** re-probed the settled state (500 ms after Tab) and reported that instead (A-shell-003); the limit is stated in the Scope of `94-AUDIT-shell.md`.
- **Commit:** ed636d57

### Other differences

- **Tracer gate (Task 1):** `auto_advance` is not enabled, which normally means a human checkpoint after a tracer task. The Task 1 verify is fully automated and passed, the commit was made, and the orchestrator asked for the complete plan from a wave executor that cannot be resumed; the tracer was re-checked (verify re-run) before Task 2, as in plans 94-02 to 94-08.
- **Index generated by script:** the Index, totals, question batch, relayout and merge tables are produced from the area reports by scratch scripts under `$QBR/audit/shell/` (`parse-reports.py`, `parse-questions.py`, `build-audit.py`, `assemble.py`); counts match each report's own stated totals (100, 51, 55, 65, 43, 44, 39).
- **Question batch shape:** 138 questions: 94 twin-content, 16 merge, 17 relayout, 11 other decisions. Two duplicates in the area reports were asked once (GroupPicker footer; OQ-QM-4 and OQ-QM-5 share one wording). The characters report's OQ-8 is the same decision as relayout R-2 and is asked as the relayout. The non-twin decisions carry two answers each and none removes anything; the removal grep of the plan passes.
- **Vendor audit not yet present:** `94-AUDIT-vendor.md` (plan 94-13) is not in this base; the index links to it by name as the plan allows.

## Open items for the operator and orchestrator

- The cut proposes owners for the ownership gaps and asks (Q-134 to Q-137): `_Calendar` vote UI, `_CharacterSelectModal`, `Home/Index`, the Shop functional breaks (Return and Sell buttons, sell-confirm script, dead Preview and View Details). `ShopManagement/Create` (GroupId never set) is stated as a separate fix off `main`, outside the phase (also in `deferred-items.md`).
- Mobile Cinzel gate deviation (WINDOWS.md entry 7, from 94-11) is acknowledged and not re-litigated here; the control run confirms it is the only difference between the frozen baseline and the branch at desktop zero and mobile at most 0.52 percent.
- 94-17 Task 3 fills the mocked-page AFTER paths at G1; every mocked-page row already carries the fixed note.

## Auth gates

None.

## Known Stubs

None (documentation and scratch output only). The audit records existing stubs in the product (for example the Bulk Actions placeholder and the dead Preview button) in the area reports.

## Threat Flags

None new. The two files quote no member name, email or user-entered text (greps for mail domains and audit account names are clean), cite screenshots by scratch path only, and describe the Admin and Platform lists structurally. The cascade probe ran two extra app instances against the operator's local database for read-only page loads; the probe edits stayed in `$QBR/cascade-probe/src` (the repository's `app.css` has no `layer(vendor)`), and all three commits of this plan touch only the two audit files. Port 8014 is free; nothing of this plan is left running.

## Self-Check: PASSED

- Files exist: `94-AUDIT-shell.md`, `94-AUDIT.md` in the plan directory; scratch: `$QBR/cascade-probe/report.json`, `vs-before.json`, `vs-control.json`, `control-sweep/`, `$QBR/audit/shell/`.
- Commits exist: ed636d57 (Task 1), 7bdbd774 (Task 2), 88edfa5b (Task 3).
- Task 1, Task 2 and Task 3 automated verify commands pass (nine sections and a layout row; footer text, `cascade-probe`, `report.json`, `layer(vendor)` only in the scratch copy; Q-01, Email templates, `_EmailLayout.razor`, no removal answer, no real mail domain, every manifest view indexed, 29 relayout rows each with a mockup note or the not-yet-drawn line).
- `git show --name-only` for each commit lists only the two audit files (plus this SUMMARY in its own commit).
