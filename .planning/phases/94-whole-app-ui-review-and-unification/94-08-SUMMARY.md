---
phase: 94-whole-app-ui-review-and-unification
plan: 08
subsystem: ui-audit-admin-platform
tags: [audit, accessibility, contrast, twin-comparison, platform-layout, scratch-probes]
requires:
  - phase: 94-02
    provides: "manifest, fixtures, frozen baseline on port 8001, BEFORE record and capture tools"
provides:
  - "94-AUDIT-admin.md: 13 index rows, 44 findings (A-admin-001 to 044), twin comparison of 12 pairs, operator questions Q1 to Q9, merge proposals M-1 to M-3, relayout proposals R-1 to R-3, empty-state copy, stylesheet census, area-phase scope notes"
  - "Answer to which Platform layout phones receive: _Layout.Platform.Mobile for all seven Platform pages (probe-proven)"
affects: [94-index-audit, admin-and-platform-area-phase]
tech-stack:
  added: []
  patterns: ["read-only live probes of the frozen baseline written under the scratch audit folder", "contrast quoted from the BEFORE probe plus contrast.mjs for composite surfaces"]
key-files:
  created:
    - .planning/phases/94-whole-app-ui-review-and-unification/94-AUDIT-admin.md
  modified: []
decisions:
  - "No difference in the area is pre-recorded as deliberate: every twin content difference became an operator question (Q1 to Q9, ten entries)"
  - "Only three Platform pairs (Group Create, Edit, Delete) qualify for a merge proposal; table-to-card pairs do not meet the CSS-only rule"
  - "The BEFORE probe figure for the Platform navbar user name was discarded after a live computed-style check contradicted it"
metrics:
  duration: "one session"
  completed: 2026-11-01
status: complete
actuals:
  tokens: 40000
  tasks: 2
  commits: 2
---

# Phase 94 Plan 08: Admin and Platform Area Audit Summary

Admin and Platform audit in the fixed report format: 44 findings (S1 8, S2 14, S3 16, S4 6) over 13 views, twins compared by extracted inventories, and the Platform-on-phones question answered by probe: phones get `_Layout.Platform.Mobile`.

Only `94-AUDIT-admin.md` was added to the repository. Scratch probes and notes are under `~/qb-ui-review/phase-94/audit/admin/`; nothing there was committed.

## What was produced

- **Index and findings:** one index row per view (12 views plus Platform Group Index, twins on one row) with the eight fixed columns; every finding carries the fixed fields (class, severity, WCAG, evidence with scratch path and `file:line`, token or component fix, area phase, operator question).
- **Headline S1 items:** parchment text forced onto badge fills and paper cells by one bare-element rule (Campaign and Dungeon Master badges 1.55 and 1.29:1, Admin/Quests description 1.20:1), mailto links in Bootstrap blue on glass on Platform phones (1.05:1, the only rule lives in a sheet that layout does not load), dark ink on a dark modal on Platform Members (1.22:1), the outline Reset Password button (1.21:1), and the Platform desktop navbar pushing Logout off screen at 320px.
- **Twin comparison:** 3 identical, 3 layout-only, 6 content-differs, plus 1 view with no twin; none pre-recorded as deliberate. Notable content differences: mobile Admin/Users lacks the email and the Resend Welcome Email action, mobile Admin/Quests lacks the Description and the Dungeon Master email and has no Campaign status branch.
- **Platform layout on phones:** probe `probe-layout.mjs` shows all seven Platform pages render `_Layout.Platform.Mobile` (offcanvas `#platformMobileNav`, `body.mobile-layout`, `mobile.css` plus the page sheet, no `site.css` and no `modern-card.css`) under a mobile User-Agent. The PROJECT.md dead-code note is stale.
- **Measured probes added beyond the BEFORE record (all read-only, baseline port 8001):** 320px offender listing, Create New User modal computed styles, WCAG 1.4.12 text-spacing override on all 26 captures (passes), and a render-cost census (`backdrop-filter` nodes per page: up to 46 on Platform phone lists).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] The two tasks are one commit**
- **Found during:** Task 1 and Task 2
- **Issue:** the plan treats Task 1 (tracer page) and Task 2 (remaining views) as separate stages of the same single file. The tracer result was verified end to end (automated check passed on the Group/Index entry) but the file was built up across both stages and committed once, because an intermediate commit of a partial audit would add no value.
- **Fix:** one path-limited commit holds the complete report; the tracer gate (verify before expansion) was run before the remaining views were written.
- **Files modified:** `94-AUDIT-admin.md`
- **Commit:** 342a16d8

**2. [Rule 1 - Bug] BEFORE-probe figure not used**
- **Found during:** Task 2 (Platform navbar findings)
- **Issue:** the BEFORE record scores the Platform `span.navbar-text` at 3.63 and 2.56:1, but a live computed-style check returns `#F8F9FA` because `text-light` wins. Quoting the record would have reported a false failure.
- **Fix:** the figure was dropped from A-admin-014 and the discrepancy is noted in the finding.

None of the other work changed from the plan.

## Auth gates

None.

## Known Stubs

None (no application source changed).

## Threat Flags

None. The audit file quotes no member name, email or user-entered text (grep for real mail domains and for the audit account domain returns 0); screenshots are referenced by scratch path only; Admin/Users, Platform/Users, Platform/Members and Platform/Group/Index were captured but described structurally. No form was submitted and no account, role, board, membership or link set was touched; the only interaction was opening a modal client-side. One scratch screenshot of the modal (`members-modal-desktop.png`) shows other boards' member data and lives only under `~/qb-ui-review/phase-94/audit/admin/`.

## Self-Check: PASSED

- File exists: `.planning/phases/94-whole-app-ui-review-and-unification/94-AUDIT-admin.md` (nine required sections plus "Platform layout on phones" and "Stylesheet census").
- Commit 342a16d8 exists on the worktree branch; `git status` was clean apart from this SUMMARY before its commit.
- Task 1 and Task 2 automated verify commands pass; no real mail domain in the report.
