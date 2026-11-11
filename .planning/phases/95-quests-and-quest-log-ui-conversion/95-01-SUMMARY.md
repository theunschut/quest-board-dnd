---
phase: 95-quests-and-quest-log-ui-conversion
plan: 01
subsystem: review-environment
tags: [playwright, scratch-tooling, review-accounts, audit-board]
requires:
  - phase: 94-whole-app-ui-review-and-unification
    provides: Playwright 1.64.1 scratch install, Audit Board (id 7), review accounts, Direction A renders
provides:
  - phase-95 scratch root ~/qb-ui-review/phase-95 with run-app, stop-app, login, audit-shoot, register tools
  - seven review role storage states for port 8000 (admin, superadmin, player, player2, player3, dm, dm2)
  - Direction A AFTER references for Quest Details and the quest forms
  - working-tree app running on http://localhost:8000
affects: [95-02, 95-03, all later plans in phase 95]
tech-stack:
  added: []
  patterns: [per-port storage states, mobile pass via devices['Pixel 7'], idempotent app launcher with tree-identity check]
key-files:
  created: []
  modified: []
key-decisions:
  - "No repository file changes: all tooling, credentials and states live under ~/qb-ui-review/phase-95 (outside the repo)"
  - "Reused the Phase 94 playwright 1.64.1 install through a node_modules symlink; nothing installed"
  - "The Audit Board held no suitable player2, player3 or dm2 account, so exactly those three were created"
requirements-completed: [D-23, D-22, DoC-6, DoC-7]
duration: ~25 min (this resume leg, after the Task 1 checkpoint)
completed: 2026-11-11
status: complete
actuals:
  tokens: 5900
  tasks: 3
  commits: 1
---

# Phase 95 Plan 01: Review environment Summary

The phase-95 review harness is up and proven end to end: the working-tree app runs on port 8000, and Playwright renders authenticated pages as seven review roles under desktop and Pixel 7 mobile User-Agents, with the phone layout verified as selected on the mobile pass.

## What was done

- **Task 1 (checkpoint, resolved by the operator 2026-11-11):** approved playwright@1.64.1 reuse, Audit Board, reuse of the Phase 94 admin and SuperAdmin accounts, "D-23 confirmed". Post-resume checks all passed: 1433 reachable, `appsettings.json` untouched, 8001 free.
- **Task 2 (tracer):** created `~/qb-ui-review/phase-95/{tools,state,logs,refs/direction-a}`; `package.json` (ES module) with `node_modules` symlinked to the Phase 94 install; ported `login.mjs`, `audit-shoot.mjs` (exports `stateFor`, `--probes` is a no-op until a `probes.mjs` exists), and new `run-app.mjs` / `stop-app.mjs` (Node ports of the shell tools). Copied the six Direction A PNGs into `refs/direction-a/`. `credentials.json` written mode 600. Tracer: `/` as admin redirects to `/quests`; mobile record `status 200, mobileLayout true`, desktop `status 200, mobileLayout false`, no console/page/request errors. A second `run-app.mjs` run exited 0 without starting a process (idempotent).
- **Task 3:** surveyed existing accounts, then registered only the missing ones through the app's own Admin pages, saved all seven states, and probed the role gates (4/4 pass in `roles-check.json`).

## Existing-account survey (before creating anything)

Done through `/Admin/Users` as the admin, with no SQL. The page lists the active board's members only; the Audit Board held four accounts:

| Name | Email | Role | Outcome |
|------|-------|------|---------|
| Audit Admin | qb-audit-admin@example.invalid | Administrator | reused as `admin` |
| Audit Platform | qb-audit-platform@example.invalid | SuperAdmin (listed as Player on the board) | reused as `superadmin` |
| Audit Player | qb-audit-player@example.invalid | Player | reused as `player` |
| Audit Dungeon Master | qb-audit-dm@example.invalid | Dungeon Master | reused as `dm` |

No `qb-audit-player2`, `player3` or `dm2`, and no other `@example.invalid` / `qb95` / review-style account was on the Audit Board. The only Dungeon Master present is "Audit Dungeon Master" (20 characters), which does not meet the long-name requirement for `dm2`, so it was not used for it. Created (confirmed genuinely new accounts: the app enqueued three welcome-email jobs and no "added to group" jobs):

| Name | Email | Role | Password |
|------|-------|------|----------|
| Tamsin Vael | qb-audit-player2@example.invalid | Player (`player2`) | set via Reset password, stored in credentials.json only |
| Orrin Duskmantle | qb-audit-player3@example.invalid | Player (`player3`) | same |
| Bartholomew Ashencroft of the Sunken Vale | qb-audit-dm2@example.invalid | Dungeon Master (`dm2`, 41-character name) | same |

Accounts belonging to other boards were not browsed (Admin > Users is board-scoped by design); had one of the three emails existed elsewhere, Create user would have attached it to the Audit Board rather than duplicating it.

## Operator answers recorded

- playwright version approved: **1.64.1** (reuse, no new install)
- review board: **Audit Board** (id 7)
- Content-Security-Policy in production (planner assumption 2): **none (live header check, Traefik config not read)**. Production through Traefik (questboard.theunschut.com) and directly on app CT 102 returns `Server: Kestrel` with no Content-Security-Policy or Content-Security-Policy-Report-Only header. They also send no X-Frame-Options, HSTS, X-Content-Type-Options, Referrer-Policy or Permissions-Policy. (Answer relayed by the orchestrator, checked read-only 2026-11-11.)

## Deviations from Plan

None - plan executed as written. Two small notes: (1) `/` redirects to `/quests` for the admin, so the tracer records `/quests` as the final URL; (2) `survey-users.mjs` and `roles-check.mjs` were added to the scratch tools to implement the operator's pre-creation survey and the Task 3 gate probes (scratch only, not repository files).

## Auth gates

None beyond the planned Task 1 sign-off.

## Known Stubs

None. No repository files were created or modified by this plan.

## Threat Flags

None. `git grep -l "qb-audit-"` over the three source trees prints nothing; `git status` over them is clean; credentials.json is mode 600 and no password appears in output or this file.

## State left behind

- Working-tree app running on `http://localhost:8000` (started by `run-app.mjs`, pid file in `~/qb-ui-review/phase-95/logs/app-8000.pid`); port 8001 free.
- `~/qb-ui-review/phase-95/` holds tools, credentials.json, `state/*.json` (seven roles), `roles-check.json`, `tracer/`, `refs/direction-a/` (six PNGs), `logs/`.

## Self-Check: PASSED

Verified: Task 2 automated check printed `ok` and `refs-ok`; Task 3 automated check printed `ok`; credentials.json lists exactly the seven roles; seven state files present; roles-check.json has four passing entries; `/health` answers 200 on port 8000; no repository source file changed.
