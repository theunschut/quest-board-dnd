---
phase: 94-whole-app-ui-review-and-unification
plan: 01
subsystem: ui-audit-tooling
tags: [playwright, audit, harness, mobile-detection, scratch-tooling]
requires: []
provides:
  - "Playwright capture harness (audit-shoot.mjs, login.mjs) that renders any route as any role under a desktop or mobile User-Agent on any port"
  - "Four audit accounts on the Audit Board (Player, DungeonMaster, Admin, SuperAdmin platform) with per-port Playwright storage states"
  - "Proof that the mobile pass selects the real mobile layout (Pixel 7 descriptor), not just a narrow desktop viewport"
affects: [94-02, 94-03]
tech-stack:
  added: ["playwright 1.64.1 + playwright-core 1.64.1 (scratch only, outside the repository)"]
  patterns: ["storageState per role per port", "Pixel 7 device descriptor for mobile selection", "submit scoped to the form that holds the filled field"]
key-files:
  created: []
  modified: []
decisions:
  - "Tooling and account state live only under ~/qb-ui-review/phase-94/ (mode 600 for credentials and states); nothing in the repository changes"
  - "Role storage states are per app instance: state/<role>.json for port 8000, state/<port>/<role>.json for any other port"
  - "Mobile pass uses devices['Pixel 7']; a 390 reflow check keeps the same UA, isMobile and hasTouch"
metrics:
  duration: "continuation (Tasks 2-3)"
  completed: 2026-11-01
status: complete
actuals:
  tokens: 4500
  tasks: 3
  commits: 1
---

# Phase 94 Plan 01: Audit Environment and Capture Harness Summary

Playwright capture harness plus four role accounts on a dedicated Audit Board, proven end to end: one authenticated page renders under both desktop and Pixel 7 mobile User-Agents, with the real `.Mobile.cshtml` layout selected only on the mobile pass.

## What was built

All output is scratch tooling under `~/qb-ui-review/phase-94/`; the repository is unchanged apart from this SUMMARY.

- `tools/login.mjs <role> [--port N]` — signs in with the `credentials.json` entry, picks the Audit Board on `/groups/pick`, saves `state/<role>.json` (port 8000) or `state/<port>/<role>.json` (mode 600). Also exports `login()`, `stateFile()` and `ROOT`.
- `tools/audit-shoot.mjs --routes <json|path|url> --ua desktop|mobile --role <role|none> --out <dir> [--width N] [--port N]` — desktop 1440x900 or `devices['Pixel 7']` (412 wide, isMobile, hasTouch); fixed timezone `Europe/Amsterdam`; per route waits for networkidle and `document.fonts.ready`, grows the viewport to document height, takes a full-page screenshot (`animations: 'disabled'`, `caret: 'hide'`) and appends a record (url, final URL, status, UA, viewport, console errors, page errors, failed requests, horizontal overflow, `mobileLayout`) to `report.json`. Exports `stateFor(role, port)`, which logs in once when the state file is missing and which the capture also re-runs once (retrying the route) when an authenticated route lands on `/Account/Login`.
- `tools/register.mjs` — creates the Player and DungeonMaster accounts through Admin > Create user and sets their passwords through Admin > Reset password using the Audit Admin session (idempotent); then records the player, dm and superadmin states.
- `tools/roles-check.mjs` — role-gate probe writing `roles-check.json`.

## Verification results

- Tracer (`/` as admin): desktop `status 200, mobileLayout false`; mobile `status 200, mobileLayout true`; both ended on `/quests` (not `/Account/Login`); one PNG and `report.json` per UA under `tracer/desktop` and `tracer/mobile`; no console, page or request errors; no horizontal overflow. Tracer verified end to end before the expansion task.
- `state/admin.json` mode 600; all four state files mode 600; `credentials.json` mode 600 listing exactly `admin, dm, player, superadmin`.
- `roles-check.json`: player on `/Admin/Users` ends on `/Account/AccessDenied`; dm `/Quest/Create` 200 with the form; admin `/Admin/Users` 200; superadmin `/Platform/Group` 200. All four `pass: true`.
- `git status --porcelain` in the worktree was empty before this SUMMARY; `pixelmatch` is absent; `localhost:8000/health` returns 200.

## Task commits

Tasks 2 and 3 change nothing in the repository (output lives outside it), so there are no per-task commits; the only commit is the SUMMARY.

## Deviations from Plan

### Task 1 (resolved by the orchestrator at the operator's explicit request, before this continuation)

**1. [Part A - route] Route (b) instead of (a)**
- The research scratchpad install no longer existed, so reuse (route a) was impossible. The operator approved route (b). `tools/` holds `package.json` (`"type": "module"`) and `node_modules` with only `playwright` and `playwright-core` 1.64.1; Chromium 156 headless shell comes from `~/.cache/ms-playwright`. No `pixelmatch` or `pngjs`.

**2. [Part B - environment] Operator-started services**
- SQL Server was the operator's existing `mssql-dev` container, started at the operator's request; the executor did not install, start or provision it. The user-secrets SQL-auth connection string was already set; `appsettings.json` is untouched.
- The working-tree dev app on port 8000 was started by the orchestrator from the main checkout via the launch config, not by the operator and not from this worktree. Consequence: the stored states for port 8000 belong to that instance's content root; any other source tree on another port needs its own state (the harness's `--port` and `stateFor` already handle this).

**3. [Part B - accounts] Bootstrapping delegated instead of the operator's own admin login**
- The operator chose to delegate. The orchestrator inserted `qb-audit-platform@example.invalid` directly into the local database with the SuperAdmin Identity role (no UI can grant SuperAdmin). Signed in as it, it created the "Audit Board" (id 7, One-Shot) through Platform > Group > Create, added itself as Player through Platform > Group > Members, and created `qb-audit-admin@example.invalid` (group role Admin) through Admin > Create user, with the password set through Admin > Reset password. This replaces the plan's step of the operator creating both accounts and granting the role.
- D-23 (as amended) confirmed by the operator: the executor creates Player and DungeonMaster through Admin > Create user; fantasy-named fixtures on the audit board are allowed in the next plan.

### Auto-fixed Issues (this continuation)

**4. [Rule 3 - Blocking] Board time zone source**
- **Found during:** Task 2
- **Issue:** The plan said to read the board zone from a `BoardClock` section in appsettings, but no such section exists in any appsettings file; the zone comes from `TimeZoneOptions.BoardTimeZoneId` (configuration section `TimeZone`), whose code default is `Europe/Amsterdam`.
- **Fix:** The harness pins `Europe/Amsterdam` as `BOARD_TZ` (exported from `audit-shoot.mjs`).

**5. [Task 3 detail] Password generation**
- `register.mjs` calls Python `secrets.token_urlsafe(18)` (24 characters) and repeats until the value has lower, upper, digit and `-`/`_` characters, so the Identity policy is met without guessing it. Passwords are written only to `credentials.json`.

## Auth gates

None during this continuation. Task 1 was an operator checkpoint, resolved as recorded above.

## Known Stubs

None.

## Threat Flags

None new. The two privileged accounts (Audit Admin, SuperAdmin platform) exist only in the operator's local database; `credentials.json` and the storage states are mode 600 under `~/qb-ui-review/phase-94/`; no passwords, tokens or member data appear in this file, in commits or in stdout. Captures so far were taken on the Audit Board only.

## Notes for later plans

- Always scope submits to the form that contains the field you filled (for example `page.locator('form', { has: page.locator('#Password') }).locator('button[type=submit]')`): every layout's nav contains a Logout form with a submit button.
- After login the app lands on `/quests`; `/` redirects there.
- Never start anything on port 8000 (working-tree app) or 8001 (reserved for the frozen baseline).

## Self-Check: PASSED

- Files exist: `~/qb-ui-review/phase-94/tools/{audit-shoot,login,register,roles-check}.mjs`, `tracer/desktop/{home.png,report.json}`, `tracer/mobile/{home.png,report.json}`, `state/{player,dm,admin,superadmin}.json`, `roles-check.json`.
- Task 2 and Task 3 automated verify commands pass.
