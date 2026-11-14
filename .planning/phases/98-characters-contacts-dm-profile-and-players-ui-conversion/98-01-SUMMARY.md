---
phase: 98-characters-contacts-dm-profile-and-players-ui-conversion
plan: 01
subsystem: review-environment
tags: [ui-review, playwright, environment, email-suppression]
requires: []
provides:
  - scratch review environment under ~/qb-ui-review/phase-98 (outside the repository)
  - tools re-pointed to ports 8310/8311 with forced email suppression
  - proven desktop and mobile capture of /Characters
affects: [98-02, 98-03, all later Phase 98 plans]
tech-stack:
  added: []
  patterns: [tools copied from the previous phase and re-pointed; node_modules symlink instead of an install]
key-files:
  created: []
  modified: []
decisions:
  - "Requirement IDs for this phase are the CONTEXT decisions D-01..D-13, the Definition of converted items DoC-1..DoC-10 and the extras X-1..X-3, because REQUIREMENTS.md has no Phase 98 IDs; both gates are local packages with no review branch or ref"
metrics:
  tasks: 3
  commits: 1
status: complete
actuals:
  tokens: 0
  tasks: 3
  commits: 1
---

# Phase 98 Plan 01: Review environment and operator checkpoint Summary

Phase 98 review environment built under `~/qb-ui-review/phase-98/` (Phase 97 tooling re-pointed to ports 8310/8311, email suppression forced and verified on the running process), with `/Characters` captured under both desktop and Pixel 7 User-Agents. No repository source file changed; the only commit is this summary.

## Task 1: operator checkpoint (answered in the planning session)

- Operator confirmations: SQL Server up on localhost:1433, ports 8310/8311 free, p98 fixtures approved (accounts qb-audit-p98player, qb-audit-p98dm, qb-audit-p98empty; the five "P98 Audit" boards), email rule accepted.
- Migrator suite: the operator answered "Yes migrate is fine"; recorded as `migratorSuite: "yes"`, `answeredAt: 2026-11-13` in `operator-answers.json`. No migrator connection string was created or requested here.
- Post-resume checks run by the executor, all passed: nothing on 8310/8311, 1433 accepts a connection (`ok`), `appsettings.json` clean, branch `milestone/v9-rolling-improvements`.

## Task 2: scratch root, tooling and tracer capture

- Copied all Phase 97 tools (except `node_modules` and `publish-review.sh`), rewrote `phase-97` to `phase-98`, 8300 to 8310, 8301 to 8311 in every text file; the grep for leftovers is empty. `tools/node_modules` symlinks to the Phase 94 Playwright 1.64.1; nothing installed.
- `run-app.mjs` now forces `EmailSettings__SuppressSending=true`, `EmailSettings__SmtpServer=127.0.0.1`, `EmailSettings__SmtpPort=1025` after any `--env` pairs, refuses `--env EmailSettings__SuppressSending=false` (verified: exit 1, nothing started) and any other SMTP host/port, and after `/health` reads `/proc/<pid>/environ` of the listener and exits non-zero if an override is missing. Verified on the running 8310 process: both required values present. A second `run-app.mjs` on the same tree exited 0 and started nothing.
- Capture tools: `audit-shoot.mjs` already aborted `/feeds/calendar/`, dismissed dialogs without recording text and redacted tokens. `sweep.mjs` and `mockup-shoot.mjs` lacked the feed abort and dialog dismissal; both added, and `sweep.mjs`/`mockup-shoot.mjs` now record failed-request URLs without query strings (the Phase 96 redaction note is closed).
- `credentials.json` holds only the superadmin and admin entries (mode 600, never printed).
- Tracer: `/Characters` as superadmin on 8310: mobile 200 with `mobileLayout=true`, desktop 200 with `mobileLayout=false`, no console/page/failed-request errors, one PNG and `report.json` each under `tracer/desktop` and `tracer/mobile`. Tracer gate: verified end to end, expansion may proceed.
- The 8310 app (started by this toolset, pid under `logs/`) is left running for the next plan.

## Task 3: shared-file facts, environment note, roles

- `_Layout.cshtml`: global list app, tokens, base, components, shell, legacy-pages, shop, characters, contacts, dm-profile, image-crop (lines 18-29); legacy-pages and shop precede the four area sheets.
- `_Layout.Mobile.cshtml`: app, tokens, base, components, shell, shell.mobile, mobile (lines 14-21); links none of the four area sheets.
- `UiFoundationLayerGuardTests.cs`: `LayeredFiles` (line 54) and `NoLiteralFiles` (line 86) end at the agenda/calendar sheets; none of the four area sheets is listed.
- `MobileViewsTests.cs`: the eight named tests located with line ranges (DmProfile 539-553, Characters 1096-1109, CharacterDetails/Create/Edit 1120-1186, PlayersIndex 1197-1211, QuestEdit 1223-1242, DmEditProfile 1293-1307).
- `components.css`: `.modal` 589, modal 44px rule 621, `.row-nav-link` 681, `.avatar-32` 752, `.avatar-32--placeholder` 760, `.empty-state` 772.
- `quest-form.css` / `event-forms.css`: help-text, required-marker, validation and action-row selectors recorded.
- HEAD `dfd4fa2d`; `c781090c` is an ancestor of HEAD.
- Roles checked (`roles-check.json`, all pass, status 200): superadmin `/platform/Group`, admin `/Admin/Users`, admin `/Admin/CreateUser` with the form.
- `ENVIRONMENT.md` restates the phase contract (ports, forced email overrides and Mailpit rule, database and data rules, git rules, local-only gates).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 - Missing critical functionality] Feed abort and dialog handling missing from sweep.mjs and mockup-shoot.mjs**
- **Found during:** Task 2 (the plan's own verify requires the feed abort in `sweep.mjs`)
- **Fix:** added the `/feeds/calendar/` route abort and dialog dismissal, and query-free failed URLs
- **Files modified:** `~/qb-ui-review/phase-98/tools/sweep.mjs`, `~/qb-ui-review/phase-98/tools/mockup-shoot.mjs` (outside the repository)

**2. [Addition] roles-check.mjs**
- Added `tools/roles-check.mjs` to produce `roles-check.json`, since the plan names the file but no tool for it.

No repository source file was created or changed by this plan; no tests were run (the plan has none).

## Known Stubs

None.

## Threat Flags

None.

## Self-Check: PASSED

All five declared artifacts exist under `~/qb-ui-review/phase-98/` (ENVIRONMENT.md, tools/, credentials.json mode 600, operator-answers.json, shared-files.md); the tracer PNGs and reports exist; the Task 2 and Task 3 automated checks printed `ok`; `git status` for the source trees was clean before this summary.
