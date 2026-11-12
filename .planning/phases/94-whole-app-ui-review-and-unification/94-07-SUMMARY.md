---
phase: 94-whole-app-ui-review-and-unification
plan: 07
subsystem: ui-audit
tags: [audit, account, group-picker, access-denied, wcag, twin-comparison]
requires:
  - phase: 94-02
    provides: "BEFORE captures, report.json, baseline app on port 8001, probe tools"
provides:
  - "94-AUDIT-account.md: index, 43 findings (A-account-001 to 043), twin comparison, 10 operator questions, 5 merge proposals, 2 relayout proposals, empty-state copy, stylesheet inventory, A-11 results, area-phase scope notes"
affects: [94-AUDIT index report, direction sign-off batch, area phase 5a]
tech-stack:
  added: []
  patterns: ["report generated from a structured findings list kept in scratch so ids, counts and the index cannot drift apart"]
key-files:
  created:
    - .planning/phases/94-whole-app-ui-review-and-unification/94-AUDIT-account.md
  modified: []
decisions:
  - "Merge proposals only for Login, ForgotPassword, SetPassword, ChangePassword and GroupPicker; Edit and Profile are content-differs and are not proposed"
  - "The document-title gap on the mobile Account views is a twin-content finding with six operator questions, not a silent fix"
  - "Findings against the shared layouts, modals and the global cream rule are marked Foundation (94), not area work"
metrics:
  duration: "one session"
  completed: 2026-11-01
status: complete
actuals:
  tokens: 17850
  tasks: 2
  commits: 2
---

# Phase 94 Plan 07: Account, Pickers and Access Denied Audit Summary

43 findings across the eight Account-area views (2 S1, 15 S2, 14 S3, 12 S4), measured against the frozen baseline, with every twin pair classified from extracted inventories and ten operator questions ready for the sign-off batch.

## What was done

- **Task 1 (tracer):** `94-AUDIT-account.md` created in the nine-section format with Account Login audited end to end (twelve findings, twin comparison, index row, Q-account-1). Verify passed and was re-run before expansion.
- **Task 2:** the other seven views, the area stylesheet, the picker layout and the A-11 checks were added; the report was regenerated from one findings list so the index counts and ids agree.

Only `94-AUDIT-account.md` and this summary are in the repository. Scratch output (probes, captures, the findings source and build script) is under `~/qb-ui-review/phase-94/audit/account/`.

## Headline findings

- **S1, A-account-031:** the picker's board cards are `div[onclick]`; a Tab walk goes Logout, Go to Platform, then leaves the page. A keyboard or screen-reader user with more than one board cannot enter the app.
- **S1, A-account-019:** on the populated desktop Profile the "If nothing happens..." line is `#6c757d` on glass (1.17:1 at B-max); the in-cell meta is about 2.56 by hand and the probe did not flag it.
- **A-account-007:** all six Account `.Mobile` views render an empty `<title>`; the desktop twins are titled. Raised as six twin-content questions.
- **A-account-021, 038:** the populated Profile overflows at 320px (503px), and the twin-less AccessDenied overflows by 4px on the mobile layout because its `.row` margins exceed the mobile container padding.
- Contrast: link blue 1.22, validation text 1.21 (mobile) and 1.97 (desktop), in-card buttons 2.19 to 4.30, nested glass on the picker 3.18 and 2.35, control borders 2.11 (desktop) and 1.30 (mobile).
- A-11: 3.3.8 and 3.3.7 pass (no paste blocking; confirm-password re-entry is a security exception); `autocomplete` tokens missing (1.3.5 on Edit); 1.4.12 produced no clipping.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] BEFORE set lacked the states the audit needed**
- **Found during:** Task 2
- **Issue:** the manifest row `Account-Profile` is labelled `calendar-subscriptions` but the capture shows the empty state; the signed-out AccessDenied and the validation states were never captured; SetPassword needs an emailed token.
- **Fix:** captured the missing states under `~/qb-ui-review/phase-94/audit/account/shots/` against port 8001. One calendar subscription was added on the Audit Board with the saved Player session and removed again through the page's own Delete form (net zero rows, session and credentials untouched). Validation states came from an empty submit in a throwaway context. SetPassword stays source-only and says so wherever it matters.
- **Files modified:** none in the repository.

**2. [Probe gap] The contrast probe does not score button labels**
- Button ratios (white on the desktop gradients, in-card blue and green) were computed from computed styles with `contrast.mjs`; the in-cell calendar meta ratio was also hand-computed because the probe did not flag it. Both are labelled as hand checks in the report.

No other deviations. The plan's own `git diff --name-only` condition holds: the repository changes are this report and this summary.

## Auth gates

None.

## Known Stubs

None (no source changed).

## Threat Flags

None. The report quotes no member name, email or user-entered text (it uses fixture ids and the fixture name "Audit Player" only), references screenshots by scratch path, and quotes none of the picker's board names; the acceptance grep for real mail domains is clean and no `example.invalid` address appears.

## Notes for the next plans

- The sign-off batch needs Q-account-1 to 10, merge proposals M1 to M5 and relayouts R1 and R2 from this report.
- The AFTER sweep should add the populated Profile (needs a fixture subscription), AccessDenied signed out and the Login validation state.
- Ask the foundation for `.modern-card--interactive`, the opaque modal surface, `--qb-fg-muted` and `--qb-danger-text` before area phase 5a starts.

## Self-Check: PASSED

- File exists: `.planning/phases/94-whole-app-ui-review-and-unification/94-AUDIT-account.md`.
- Commits exist: `8db102f5` (Task 1), `9e550634` (Task 2).
- Task 1 and Task 2 automated verify commands pass; no real mail domain in the report.
