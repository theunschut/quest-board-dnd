---
phase: 96-account-and-pickers-ui-conversion
plan: 01
subsystem: review-environment
tags: [playwright, scratch-tooling, review-accounts, audit-board]
requires:
  - phase: 95-quests-and-quest-log-ui-conversion
    provides: phase-95 scratch tooling, Audit Board (id 7) review accounts, Playwright 1.64.1 reuse from Phase 94
provides:
  - phase-96 scratch root ~/qb-ui-review/phase-96 with run-app, stop-app, login, audit-shoot, register, roles-check tools
  - nine review role storage states for port 8000 (admin, superadmin, player, player2, player3, dm, dm2, multi, noboard)
  - working-tree app running on http://localhost:8000
affects: [96-02, all later plans in phase 96]
tech-stack:
  added: []
  patterns: [per-port storage states under state/<port>/, mobile pass via devices['Pixel 7'], idempotent app launcher with tree-identity and ownership check, token redaction in reports]
key-files:
  created: []
  modified: []
key-decisions:
  - "No repository file changes: all tooling, credentials and states live under ~/qb-ui-review/phase-96 (outside the repo)"
  - "Reused the Phase 94 playwright 1.64.1 install through a node_modules symlink; nothing installed"
  - "Linux substitutions per ~/qb-ui-review/phase-96/ENVIRONMENT.md: ports 8000/8001 instead of 8096/8097, bundled Chromium instead of Edge, ss and /proc instead of PowerShell"
  - "Reused the phase-95 Audit accounts and created only multi and noboard"
requirements-completed: [D-04, D-05, DoC-6, DoC-7]
duration: ~20 min (this resume leg, after the Task 1 checkpoint)
completed: 2026-11-12
status: complete
actuals:
  tokens: 6500
  tasks: 3
  commits: 1
---

# Phase 96 Plan 01: Review environment Summary

The phase-96 review harness is up and proven end to end: the working-tree app runs on port 8000, and Playwright renders authenticated pages as nine review roles under desktop and Pixel 7 mobile User-Agents, with the phone layout verified as selected on the mobile pass and no reset token reaching a report.

## What was done

- **Task 1 (checkpoint, resolved by the operator 2026-11-12):** Part A approved as a copy of the phase-94/95 playwright@1.64.1, reused through a symlink (nothing installed). Part B: review board "Audit Board" (id 7), "D-05 confirmed", "checkout free". This checkout is owned by phase 96; the parallel hotfix session uses its own worktree and port 8100, which was never touched. Adapted post-resume checks all passed: 1433 reachable, credentials.json holds the required roles, `appsettings.json` unchanged, 8000 and 8001 free before the app was started.
- **Task 2 (tracer):** created `~/qb-ui-review/phase-96/{tools,state,logs,tracer}`. Copied the phase-95 tools and re-pointed them to phase 96, then extended them to the plan contract:
  - `run-app.mjs` gained `--restart` and `--env KEY=VALUE`, a pid file that records the pid and the source tree, and the four-outcome listener rule (own + restart restarts; serves the tree exits 0; own but a different tree exits 1; not started by the tool exits 1 and is never stopped). Verified: a second run exits 0 without starting anything; a run against another tree exits 1 and prints port, expected and actual.
  - `login.mjs` now saves to `state/<port>/<role>.json` (also for 8000), and picks the board by clicking the visible element whose text equals the board name.
  - `audit-shoot.mjs` gained token redaction (url, finalUrl, requests, failed requests, console and page errors), abort of `/feeds/calendar/` requests, native-dialog dismissal recording only `{ dialog: true, type }`, and a screenshot name derived without the query string. `stateFor` is exported and `--probes` still loads `probes.mjs` only when it exists (none in phase 96 yet, so it is a no-op).
  - Tracer: `/Account/Profile` as admin, mobile `status 200, mobileLayout true`, desktop `status 200, mobileLayout false`, no console, page or request errors, no overflow. Redaction check: `/Account/SetPassword?userId=0&token=render-only` recorded as `token=REDACTED`, with the literal value absent from the record.
- **Task 3:** `register.mjs` created only the two missing accounts through Admin > Create user and Admin > Reset password. Saved all nine states and probed the role gates (4/4 pass in `roles-check.json`: player denied on `/Admin/Users`, dm gets the `/Quest/Create` form, admin gets `/Admin/Users`, superadmin gets `/platform/Group`).

## Review accounts

| Role | Account | Origin |
|------|---------|--------|
| admin, superadmin, player, dm | qb-audit-admin, qb-audit-platform, qb-audit-player, qb-audit-dm `@example.invalid` | reused from phase 95 |
| player2, player3, dm2 | qb-audit-player2, qb-audit-player3, qb-audit-dm2 `@example.invalid` | reused from phase 95, copied into the phase-96 credentials.json for later plans |
| multi (Corwin Ashdale, Player) | qb-audit-multi@example.invalid | created in this plan |
| noboard (Ilsa Thornwick, Player) | qb-audit-noboard@example.invalid | created in this plan; still on the Audit Board, removed in the next plan for the no-groups alert |

Wherever a later plan says `qb96-*`, read `qb-audit-*`. credentials.json is mode 600; no password appears in this file, in any output or in any commit.

## Deviations from Plan

### Operator-approved environment substitutions (not defects)

**1. [Environment] Windows plan assumptions replaced with Linux equivalents**
- **Issue:** the plan assumes a Windows host (ports 8096/8097, Microsoft Edge via `channel: 'msedge'`, PowerShell process lookup, Windows Integrated Auth, `python`, `qb96-*` accounts, a freshly installed playwright-core).
- **Fix:** ports 8000 (working tree) and 8001 (baseline, started by the next plan); bundled Chromium via `chromium.launch()`; `ss -ltnp` and `/proc/<pid>` for process lookup; SQL Server in docker `mssql-dev` on localhost:1433 (untouched); `python3`; `qb-audit-*` accounts; playwright 1.64.1 reused through a symlink with no install. State files live under `state/8000/` and `state/8001/`.
- **Files modified:** scratch tools only (outside the repo).

**2. [Environment] Role gate accepts extra roles**
- **Issue:** the plan's Task 3 gate asserts exactly six roles (admin, dm, multi, noboard, player, superadmin) and six state files.
- **Fix:** the operator asked for `player2`, `player3` and `dm2` to be carried over, so the gate asserts nine roles (the six plus those three) and nine state files, all present.

### Auto-fixed Issues

**3. [Rule 1 - Bug] login.mjs board click matched a hidden nav dropdown item**
- **Found during:** Task 3 (first `register.mjs` run, signing in as `player2`)
- **Issue:** for a user already routed past the board picker, the text "Audit Board" matched an invisible nav dropdown link, so the click timed out.
- **Fix:** the match is restricted to visible elements (`locator('visible=true')`); the re-run saved all nine states.
- **Files modified:** `~/qb-ui-review/phase-96/tools/login.mjs` (scratch)

## Auth gates

None beyond the planned Task 1 sign-off.

## Known Stubs

None. No repository files were created or modified by this plan.

## Threat Flags

None. `git grep -I -l "qb-audit-\|qb96-"` over the three source trees prints nothing; the source trees are unchanged; credentials.json and every state file are mode 600 under the user profile; reports redact token values and abort calendar-feed requests.

## State left behind

- Working-tree app running on `http://localhost:8000` (started by `run-app.mjs`, pid file `~/qb-ui-review/phase-96/logs/app-8000.pid`, log `logs/app-8000.log`). Later plans expect it running; stop it with `stop-app.mjs 8000` before `dotnet build` or `dotnet test` when a plan says so. Port 8001 is free for the baseline; port 8100 (hotfix session) was not touched.
- `~/qb-ui-review/phase-96/` holds `tools/`, `credentials.json`, `state/8000/` (nine roles), `roles-check.json`, `tracer/`, `logs/`.
- `.planning/STATE.md` carried an uncommitted modification from before this plan started (from the orchestrator); it is included in the final docs commit together with this plan's state updates.

## Self-Check: PASSED

Verified: Task 2 automated checks printed `ok` (twice); the Task 3 gate (adapted to nine roles) printed `ok`; credentials.json lists the nine roles; nine state files present; roles-check.json has four passing entries; `/health` answers 200 on port 8000; port 8001 is free; no repository source file changed.
