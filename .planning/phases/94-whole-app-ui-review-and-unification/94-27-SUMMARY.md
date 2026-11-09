---
phase: 94-whole-app-ui-review-and-unification
plan: 27
subsystem: planning
tags: [roadmap, area-phase-cut, coverage-proof, release-caveat]
requires:
  - phase: 94-25
    provides: the sign-off record and relayout list
  - phase: 94-26
    provides: the refreshed area-phase cut with post-foundation numbers
provides:
  - Phases 95 to 101 appended to the roadmap with scope, inherited decisions, definition of converted and constraints
  - A coverage proof that every view, stylesheet and email template has exactly one owner
affects: [phase-95, phase-96, phase-97, phase-98, phase-99, phase-100, phase-101]
tech-stack:
  added: []
  patterns: [scoped section fill after phase.add, coverage script over the audit's own cut text]
key-files:
  created:
    - .planning/phases/94-whole-app-ui-review-and-unification/94-27-CHECKPOINT.md
    - .planning/phases/95-quests-and-quest-log-ui-conversion/.gitkeep
    - .planning/phases/96-account-and-pickers-ui-conversion/.gitkeep
    - .planning/phases/97-events-series-calendar-and-agenda-ui-conversion/.gitkeep
    - .planning/phases/98-characters-contacts-dm-profile-and-players-ui-conversion/.gitkeep
    - .planning/phases/99-shop-and-shop-management-ui-conversion/.gitkeep
    - .planning/phases/100-admin-and-platform-ui-conversion/.gitkeep
    - .planning/phases/101-email-templates-restyle/.gitkeep
  modified:
    - .planning/ROADMAP.md
    - .planning/phases/94-whole-app-ui-review-and-unification/94-AUDIT.md
key-decisions:
  - "Operator approved the cut as proposed on 2026-11-09, with no change to order, splits or merges"
  - "The desktop top bar relayout is a verification check in phase 95, because the desktop shell already fixed it"
  - "home.mobile.css stays with phase 95: it is linked by Quest/Index.Mobile, not by the Home page"
requirements-completed: [D-06, D-18, D-19, D-07, D-05]
duration: 1 session
completed: 2026-11-09
status: complete
actuals:
  tokens: 22000
  tasks: 3
  commits: 4
---

# Phase 94 Plan 27: Area-phase cut gate Summary

The cut was proven to cover the whole app (121 view files, 45 stylesheets, 10 email components, each in exactly one phase), the operator approved it as proposed, and seven area phases (95 to 101) were appended to the roadmap with full scope and the release caveat.

## What was done

- **Task 1 (tracer), commit 1753ca09.** `coverage.py` parses each phase subsection of the cut in 94-AUDIT.md. It requires every manifest view file, every non-layout partial, every non-foundation stylesheet and every email component to appear in exactly one phase. `legacy-pages.css` is checked by line range (six disjoint ranges covering all 1,329 lines) and `mobile.css` by a single owner. `cut.md` (outside the repo, in the signoff folder) lists each phase with its append description, scope, counts, inherited answers, each relayout's BEFORE and AFTER, and a Release and deployment section with the `docker-publish.yml` and GHCR caveat.
- **Task 2 (decision checkpoint).** The operator reviewed the brief (`94-27-CHECKPOINT.md`, commit 44920af8) and answered "approve".
- **Task 3, commit 2a905665.** Seven `phase.add` calls in the approved order. Each section then got a scoped fill: Goal, Depends on, Scope, Guard tests to rewrite, Inherited decisions (answers, merges, every relayout with BEFORE and AFTER, the in-principle statement), Definition of converted (the ten points plus phase-specific ones) and Constraints (including the release caveat). Approval is recorded in 94-AUDIT.md as "Approved on 2026-11-09".

| Phase | Name |
|---|---|
| 95 | Quests and Quest Log UI conversion |
| 96 | Account and pickers UI conversion |
| 97 | Events, Series, Calendar and Agenda UI conversion |
| 98 | Characters, Contacts, DM profile and Players UI conversion |
| 99 | Shop and Shop management UI conversion |
| 100 | Admin and Platform UI conversion |
| 101 | Email templates restyle |

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] The cut text in 94-AUDIT.md overlapped or went stale**
- **Found during:** Task 1 (first coverage run)
- **Issue:** `Home/Index` was in two phases; Phase 3 and Phase 1 each named a shared partial the other owned; two Stylesheets bullets named `site.css`, which no longer exists; the desktop top bar relayout had no owner.
- **Fix:** Corrected the cut text so each item has one owner, and assigned the top bar item to phase 95.
- **Files modified:** 94-AUDIT.md
- **Commit:** 1753ca09

**2. [Orchestrator note] Top bar relayout is verification only**
- The orchestrator reported that the desktop shell already fixed the wrap (a 56px bar at every width, collapsing below 1200px, operator-accepted). Phase 95 states it as a verification check with no new work, and 94-AUDIT.md adjustment 12 says the same.

**3. [Observation] Appended phases sit after the Backlog section**
- `phase.add` appends at the end of ROADMAP.md, so phases 95 to 101 come after the parked Backlog entry (Phase 999.1). No existing text moved; the diff against the base is additions only.

**4. [Correction] Home stylesheet ownership**
- The checkpoint brief flagged `home.mobile.css` as possibly belonging to the Account phase. Checking showed that only `Views/Quest/Index.Mobile.cshtml` links it, so it correctly stays with phase 95. No move was made.

## Known Stubs

None. The appended phases carry "Plans: 0 plans" with a placeholder line, which is the normal unplanned state.

## Threat Flags

None. This plan changed planning documents only.

## Self-Check: PASSED

- Commits 1753ca09, 44920af8 and 2a905665 exist on the branch.
- The plan's Task 1 and Task 3 verify commands pass; coverage re-run after the final edits: zero missing, zero duplicated, zero extra.
- ROADMAP.md diff has no removed lines. STATE.md and WINDOWS.md were not touched.
